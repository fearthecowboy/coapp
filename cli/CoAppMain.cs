//-----------------------------------------------------------------------
// <copyright company="CoApp Project">
//     Copyright (c) 2011 Garrett Serack. All rights reserved.
// </copyright>
// <license>
//     The software is licensed under the Apache 2.0 License (the "License")
//     You may not use the software except in compliance with the License. 
// </license>
//-----------------------------------------------------------------------

namespace CoApp.CLI {
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Resources;
    using System.Threading;
    using System.Threading.Tasks;
    using Packaging.Client;
    using Packaging.Common;
    using Packaging.Common.Exceptions;
    using Properties;
    using Toolkit.Console;
    using Toolkit.Exceptions;
    using Toolkit.Extensions;
    using Toolkit.Linq;
    using Toolkit.Logging;
    using Toolkit.Tasks;

    /// <summary>
    /// Main Program for command line coapp tool
    /// </summary>
    /// <remarks></remarks>
    public class CoAppMain : AsyncConsoleProgram {
        private bool _terse = false;
        private bool _verbose = false;
        
        private bool? _force = null;
        
        private string _location = null;
        private bool? _pretend = null;
        private bool? _autoUpgrade = null;
        private int _priority = 50;

        private bool? _x64 = null;
        private bool? _x86 = null;
        private bool? _cpuany = null;

        private bool IsFiltering { get { return (true == _x64) || (true == _x86) || (true == _cpuany); } }

        /// <summary>
        /// Gets the res.
        /// </summary>
        /// <remarks></remarks>
        protected override ResourceManager Res {
            get { return Resources.ResourceManager; }
        }

        /// <summary>
        /// Main entrypoint for CLI.
        /// </summary>
        /// <param name="args">The command line arguments</param>
        /// <returns>int value representing the ERRORLEVEL.</returns>
        /// <remarks></remarks>coapp.service
        private static int Main(string[] args) {
#if DEBUG
            if( Debugger.IsAttached ) {
                Thread.Sleep(2000);
            }
#endif 
            return new CoAppMain().Startup(args);
        }

        private readonly List<Task> preCommandTasks = new List<Task>();

        private static List<string>  activeDownloads = new List<string>();
        private Filter<IPackage> pkgFilter;
        private Expression<Func<IEnumerable<IPackage>, IEnumerable<IPackage>>> collectionFilter;

        private readonly PackageManager _packageManager = new PackageManager();

        /// <summary>
        /// The (non-static) startup method
        /// </summary>
        /// <param name="args">The command line arguments.</param>
        /// <returns>Process return code.</returns>
        /// <remarks></remarks>
        protected override int Main(IEnumerable<string> args) {

            CurrentTask.Events += new DownloadProgress((remoteLocation, location, progress) => {
                if (!activeDownloads.Contains(remoteLocation)) {
                    activeDownloads.Add(remoteLocation);
                }
                "Downloading {0}".format(remoteLocation.UrlDecode()).PrintProgressBar(progress);
            });

            CurrentTask.Events += new DownloadCompleted((remoteLocation, locallocation) => {
                if (activeDownloads.Contains(remoteLocation)) {
                    Console.WriteLine();
                    activeDownloads.Remove(remoteLocation);
                }
            });

            try {
                #region command line parsing

                var options = args.Where(each => each.StartsWith("--")).Switches();
                var parameters = args.Where(each => !each.StartsWith("--")).Parameters().ToArray();

                foreach (var arg in options.Keys) {
                    var argumentParameters = options[arg];
                    var last = argumentParameters.LastOrDefault();
                    var lastAsBool = string.IsNullOrEmpty(last) || last.IsTrue();

                    switch (arg) {
                            /* options  */
                        case "min-version":
                            pkgFilter &= Package.Properties.Version.IsGreaterThanOrEqual(last);
                            break;

                        case "max-version":
                            pkgFilter &= Package.Properties.Version.IsLessThanOrEqual(last);
                            break;

                        case "installed":
                            pkgFilter &= Package.Properties.Installed.Is(lastAsBool);
                            break;

                        case "active":
                            pkgFilter &= Package.Properties.Active.Is(lastAsBool);
                            break;

                        case "wanted":
                            pkgFilter &= Package.Properties.Wanted.Is(lastAsBool);
                            break;

                        case "blocked":
                            pkgFilter &= Package.Properties.Blocked.Is(lastAsBool);
                            break;

                        case "trimable":
                            pkgFilter &= Package.Filters.Trimable;
                            break;

                        case "latest":
                            collectionFilter = collectionFilter.Then(p=> p.HighestPackages());
                            break;

                        case "force":
                            _force = lastAsBool;
                            break;

                        case "force-scan":
                        case "force-rescan":
                        case "scan":
                        case "rescan":
                            preCommandTasks.Add(_packageManager.SetAllFeedsStale());
                            break;

                        case "pretend":
                            _pretend= lastAsBool;
                            break;

                        case "auto-upgrade":
                            _autoUpgrade = lastAsBool;
                            break;

                        case "exact":
                            _autoUpgrade = false;
                            break;

                        case "use-feed":
                        case "feed":
                            _location = last;
                            break;

                        case "verbose":
                            _verbose = lastAsBool;
                            Logger.Errors = true;
                            Logger.Messages = true;
                            Logger.Warnings = true;
                            _packageManager.EnableMessageLogging();
                            _packageManager.EnableWarningLogging();
                            _packageManager.EnableErrorLogging();
                            break;

                            /* global switches */
                        case "load-config":
                            // all ready done, but don't get too picky.
                            break;

                        case "nologo":
                            this.Assembly().SetLogo(string.Empty);
                            break;

                        case "terse":
                            this.Assembly().SetLogo(string.Empty);
                            _terse = true;
                            _verbose = false;
                            break;

                        case "x64":
                            _x64 = true;
                            break;

                        case "x86":
                            _x86 = true;
                            break;

                        case "any":
                        case "cpuany":
                            _cpuany = true;
                            break;

                        case "all":
                            _x64 = true;
                            _x86 = true;
                            _cpuany = true;
                            break;

                        case "priority":
                            switch( last ) {
                                case "highest":
                                    _priority = 100;
                                    break;
                                case "high":
                                    _priority = 75;
                                    break;
                                case "normal":
                                case "default":
                                    _priority = 50;
                                    break;
                                case "low":
                                    _priority = 25;
                                    break;
                                case "lowest":
                                    _priority = 0;
                                    break;
                                default:
                                    _priority = last.ToInt32(50);
                                    break;
                            }
                            break;

                        case "help":
                            return Help();

                        default:
                            throw new ConsoleException(Resources.UnknownParameter, arg);
                    }
                }

                Logo();

                if (!parameters.Any()) {
                    throw new ConsoleException(Resources.MissingCommand);
                }

                #endregion
              
                Task task = null;
                if (parameters.IsNullOrEmpty()) {
                    return Help();
                }
                string command = string.Empty;

                if (parameters[0].ToLower().EndsWith(".msi")) {
                    var files = parameters.FindFilesSmarter().ToArray();
                    if( files.Length > 0 ) {
                        // assume install if just given filenames 
                        command = "install";
                        parameters = files;
                    }
                }

                if (string.IsNullOrEmpty(command)) {
                    command = parameters.FirstOrDefault();
                    parameters = parameters.Skip(1).ToArray();
                }

                if (!command.StartsWith("-")) {
                    command = command.ToLower();
                }

                switch (command) {
                    case "-?":
                        return Help();
                     
                    case "test":
                       // pkgFilter &= Package.Properties.Installed.Is(true) & Package.Properties.Active.Is(true) & Package.Properties.UpdatePackages.Any();
                        // collectionFilter = collectionFilter.Then(p => p.HighestPackages().OrderByDescending(each => each.Version));
                        // collectionFilter = collectionFilter.Then(pkgs => pkgs.HighestPackages());
                        
                        task = preCommandTasks.Continue(() => _packageManager.FindPackages(CanonicalName.AllPackages, Package.Filters.PackagesWithUpgradeAvailable, collectionFilter, _location))
                            .Continue(packages => {
                                if (packages.IsNullOrEmpty()) {
                                    PrintNoPackagesFound(parameters);
                                    return;
                                }
                               PrintPackages(packages);

                            });

                        break;

                    case "-l":
                    case "list":
                    case "list-package":
                    case "list-packages":
                        if( !parameters.Any() || parameters[0] == "*" ) {
                            collectionFilter = collectionFilter.Then(p => p.HighestPackages());
                        }

                        task = preCommandTasks.Continue(() => _packageManager.QueryPackages(parameters, pkgFilter, collectionFilter, _location)
                            .Continue(packages => {
                                if (packages.IsNullOrEmpty()) {
                                    PrintNoPackagesFound(parameters);
                                    return;
                                } 
                                PrintPackages(packages);
                            }));
                        break;

                    case "-w":
                    case "wanted":
                    case "want":
                        task = preCommandTasks.Continue(() => _packageManager.QueryPackages(parameters, pkgFilter & Package.Filters.InstalledPackages, collectionFilter, _location)
                            .Continue(packages => {
                                if (packages.IsNullOrEmpty()) {
                                    PrintNoPackagesFound(parameters);
                                    return;
                                }
                                var pkgs = packages.ToArray();

                                Console.WriteLine("Setting {0} packages to 'wanted':", pkgs.Length);

                                foreach( var p in packages ) {
                                    _packageManager.SetPackageWanted(p.CanonicalName, true);
                                }

                                // refresh
                                pkgs.Select(each => _packageManager.GetPackage(each.CanonicalName)).Continue( p => PrintPackages(p));
                            }));
                        break;

                    case "-W":
                    case "drop":
                    case "unwanted":
                    case "unwant":
                    case "donotwant":
                        task = preCommandTasks.Continue(() => _packageManager.QueryPackages(parameters, pkgFilter & Package.Filters.InstalledPackages, collectionFilter, _location)
                            .Continue(packages => {
                                if (packages.IsNullOrEmpty()) {
                                    PrintNoPackagesFound(parameters);
                                    return;
                                }
                                var pkgs = packages.ToArray();

                                Console.WriteLine("Setting {0} packages to 'unwanted':", pkgs.Length);

                                foreach (var p in packages) {
                                    _packageManager.SetPackageWanted(p.CanonicalName, false);
                                }

                                // refresh
                                pkgs.Select(each => _packageManager.GetPackage(each.CanonicalName)).Continue(p => PrintPackages(p));
                            }));
                        break;

                    case "block":
                    case "block-package":
                    case "-b":
                        task = preCommandTasks.Continue(() => {
                                foreach (var cn in parameters.Select(v => (CanonicalName)v)) {
                                    _packageManager.SetGeneralPackageInformation(_priority, cn, "state", PackageState.Blocked.ToString());
                                    Console.WriteLine("Blocking '{0}' at priority {1}.",cn.ToString(), _priority  );
                                }
                            });
                        break;

                    case "lock-package":
                    case "lock":
                    case "-B":
                        task = preCommandTasks.Continue(() => {
                            foreach (var cn in parameters.Select(v => (CanonicalName)v)) {
                                _packageManager.SetGeneralPackageInformation(_priority, cn, "state", PackageState.DoNotChange.ToString());
                                Console.WriteLine("Locking '{0}' at priority {1}.", cn.ToString(), _priority);
                            }
                        });
                        break;

                    case "updateable":
                    case "-d":
                        task = preCommandTasks.Continue(() => {
                            foreach (var cn in parameters.Select(v => (CanonicalName)v)) {
                                _packageManager.SetGeneralPackageInformation(_priority, cn, "state", PackageState.Updatable.ToString());
                                Console.WriteLine("Setting updatable on '{0}' at priority {1}.", cn.ToString(), _priority);
                            }
                        });
                        break;

                    case "upgradable":
                    case "-G":
                        task = preCommandTasks.Continue(() => {
                            foreach (var cn in parameters.Select(v => (CanonicalName)v)) {
                                _packageManager.SetGeneralPackageInformation(_priority, cn, "state", PackageState.Updatable.ToString());
                                Console.WriteLine("Setting upgradable on '{0}' at priority {1}.", cn.ToString(), _priority);
                            }
                        });
                        break;


                    case "-i":
                    case "install":
                    case "install-package":
                    case "install-packages":
                        if (!parameters.Any()) {
                            throw new ConsoleException(Resources.InstallRequiresPackageName);
                        }
                        task = preCommandTasks.Continue(() =>InstallPackages(parameters));
                        break;

                    case "-r":
                    case "remove":
                    case "uninstall":
                    case "remove-package":
                    case "remove-packages":
                    case "uninstall-package":
                    case "uninstall-packages":
                        if (!parameters.Any()) {
                            throw new ConsoleException(Resources.RemoveRequiresPackageName);
                        }

                        task = preCommandTasks.Continue(() =>RemovePackages(parameters));
                        break;

                    case "-L":
                    case "feed":
                    case "feeds":
                    case "list-feed":
                    case "list-feeds":
                        task = preCommandTasks.Continue((Func<Task>)ListFeeds); 
                        break;


                    case "-U":
                    case "upgrade":
                    case "upgrade-package":
                    case "upgrade-packages":
                        Console.WriteLine("UPDATE CURRENTLY DISABLED. CHECK BACK SOON");
                        // task = preCommandTasks.Continue(() => _packageManager.GetUpgradablePackages(parameters)).Continue(pkgs => PrintPackages(pkgs));
                        break;

                    case "-u":
                    case "update":
                    case "update-package":
                    case "update-packages":
                        Console.WriteLine("UPDATE CURRENTLY DISABLED. CHECK BACK SOON");

                        // task = preCommandTasks.Continue(() => _packageManager.GetUpdatablePackages(parameters)).Continue(pkgs => PrintPackages(pkgs));
                        // task = preCommandTasks.Continue(() => _packageManager.GetUpdatablePackages(parameters)).Continue( packages => Update(packages) );
                        break;

                    case "-A":
                    case "add-feed":
                    case "add-feeds":
                    case "add":
                        if (!parameters.Any()) {
                            throw new ConsoleException(Resources.AddFeedRequiresLocation);
                        }
                        task =  preCommandTasks.Continue(() => AddFeed(parameters));
                        break;

                    case "-D":
                    case "-R":
                    case "delete":
                    case "delete-feed":
                    case "delete-feeds":
                        if (!parameters.Any()) {
                            throw new ConsoleException(Resources.DeleteFeedRequiresLocation);
                        }
                        task = preCommandTasks.Continue(() => DeleteFeed(parameters));
                        break;

                    case "-t":
                    case "trim-packages":
                    case "trim-package":
                    case "trim":
                        pkgFilter &= Package.Filters.Trimable;
                        task = preCommandTasks.Continue(() =>RemovePackages(parameters));
                        break;

                    case "set-feed-active":
                    case "feed-active":
                    case "activate-feed":
                        task = preCommandTasks.Continue(() => MatchFeeds(parameters)).Continue(feeds => {
                            feeds.Select(each => _packageManager.SetFeed(each, FeedState.Active)).ToArray();
                        });
                        
                        break;
                    case "set-feed-passive":
                    case "feed-passive":
                    case "passivate-feed":
                        task = preCommandTasks.Continue(() => MatchFeeds(parameters)).Continue(feeds => {
                            feeds.Select(each => _packageManager.SetFeed(each, FeedState.Passive)).ToArray();
                        });
                        break;
                    case "set-feed-ignored":
                    case "set-feed-ignore":
                    case "feed-ignored":
                    case "feed-ignore":
                    case "disable-feed":
                        task = preCommandTasks.Continue(() => MatchFeeds(parameters)).Continue(feeds => {
                            feeds.Select(each => _packageManager.SetFeed(each, FeedState.Ignored)).ToArray();
                        });
                        break;
#if DEPRECATED
                    case "-a":
                    case "activate":
                    case "activate-package":
                    case "activate-packages":
                        task = preCommandTasks.Continue(() => _packageManager.QueryPackages(parameters, pkgFilter & Package.Properties.Installed.Is(true),null, _location)
                            .Continue(packages => Activate(parameters, packages)));

                        break;
#endif
                    case "-g":
                    case "get-packageinfo":
                    case "info":
                        task = preCommandTasks.Continue(() => _packageManager.QueryPackages(parameters,pkgFilter,null, _location)
                            .Continue(packages => GetPackageInfo(parameters,packages)));
                        break;

                    case "enable-telemetry":
                        task = preCommandTasks.Continue(() => _packageManager.SetTelemetry(true)).ContinueAlways((a)=> {
                            Console.WriteLine("Telemetry is currently set to : {0}", _packageManager.GetTelemetry().Result ? "Enabled" : "Disabled");
                        });
                        break;

                    case "disable-telemetry":
                        task = preCommandTasks.Continue(() => _packageManager.SetTelemetry(false)).ContinueAlways((a) => {
                            Console.WriteLine("Telemetry is currently set to : {0}", _packageManager.GetTelemetry().Result ? "Enabled" : "Disabled");
                        });
                        break;

                    case "create-symlink":
                        if (parameters.Count() != 2) {
                            throw new ConsoleException("Create-symlink requires two parameters: existing-location and new-link");
                        }
                        task = preCommandTasks.Continue(() =>  _packageManager.CreateSymlink(parameters.First().GetFullPath(), parameters.Last().GetFullPath()));
                        break;

                    case "create-hardlink":
                        if (parameters.Count() != 2) {
                            throw new ConsoleException("Create-hardlink requires two parameters: existing-location and new-link");
                        }
                        task = preCommandTasks.Continue(() =>  _packageManager.CreateHardlink(parameters.First().GetFullPath(), parameters.Last().GetFullPath()));
                        break;

                    case "create-shortcut":
                        if (parameters.Count() != 2) {
                            throw new ConsoleException("Create-shortcut requires two parameters: existing-location and new-link");
                        }
                        task = preCommandTasks.Continue(() =>  _packageManager.CreateShortcut(parameters.First().GetFullPath(), parameters.Last().GetFullPath()));
                        break;

                    case "-p" :
                    case "list-policies":
                    case "list-policy":
                    case "policies":
                        task = preCommandTasks.Continue(() =>  ListPolicies() );
                        break;

                    case "add-to-policy": {
                        if (parameters.Count() != 2) {
                            throw new ConsoleException("Add-to-policy requires two parameters (policy name and account)");
                        }

                        var policyName = parameters.First();
                        var account = parameters.Last();

                        task = preCommandTasks.Continue(() => {
                            _packageManager.GetPolicy(policyName).Continue(policy => {
                                // found the policy, so continue.
                                _packageManager.AddToPolicy(policyName, account).Continue(() => {
                                    Console.WriteLine("Account '{0} added to policy '{1}", account, policyName);
                                    ListPolicies(policyName);
                                });
                            });
                        });
                    }
                        break;

                    case "remove-from-policy": {
                            if (parameters.Count() != 2) {
                                throw new ConsoleException("remove-from-policy requires two parameters (policy name and account)");
                            }

                            var policyName = parameters.First();
                            var account = parameters.Last();

                            task = preCommandTasks.Continue(() => {
                                _packageManager.GetPolicy(policyName).Continue(policy => {
                                    // found the policy, so continue.
                                    _packageManager.RemoveFromPolicy(policyName, account).Continue(() => {
                                        Console.WriteLine("Account '{0} removed from policy '{1}", account, policyName);
                                        ListPolicies(policyName);
                                    });
                                });
                            });
                        }
                        break;

                    default:
                        throw new ConsoleException(Resources.UnknownCommand, command);
                }

                task.ContinueOnCanceled(() => {
                    // the task was cancelled, and presumably dealt with.
                    Fail("Operation Canceled.");
                });

                task.ContinueOnFail((exception) => {
                    exception = exception.Unwrap();
                    if (!(exception is OperationCanceledException)) {
                        var phpue = exception as PackageHasPotentialUpgradesException;
                        if (phpue != null) {
                            // we've been told something we've asked for has a newer package available, and we didn't tell it that we either wanted it or an auto-upgrade
                            PrintPotentialUpgradeInformation(phpue.UnsatisfiedPackage, phpue.SatifactionOptions);
                            phpue.Cancel(); // marks this exception as handled.
                            return;
                        }

                        // handle coapp exceptions as cleanly as possible.
                        var ce = exception as CoAppException;
                        if (ce != null) {
                            Fail("Alternative");
                            Fail(ce.Message);

                            ce.Cancel();
                            return;
                        }
                    }

                    // hmm. The plan did not work out so well. 
                    Fail("Error (???): {0}-{1}\r\n\r\n{2}", exception.GetType(), exception.Message, exception.StackTrace);
                    
                });

                task.Continue(() => {
                    Console.WriteLine("Done.");
                }).Wait();
            }
            catch (ConsoleException failure) {
                Fail("{0}\r\n\r\n    {1}", failure.Message, Resources.ForCommandLineHelp);
                CancellationTokenSource.Cancel();
            }
            return 0;
        }
#if DEPRECATED
        private Task DoNotUpdate(IEnumerable<string> parameters, IEnumerable<Package> packages) {
            if (!packages.Any()) {
                PrintNoPackagesFound(parameters);
                return "".AsResultTask();
            }

            var remoteTasks = packages.Select(package => _packageManager.MarkPackageDoNotUpdate(package.CanonicalName)).ToArray();
            remoteTasks.ContinueOnFail(ex => FailOnExceptions(ex));
            return remoteTasks.Continue(() => {
                Console.WriteLine("Marked packages as 'do-not-update' :");
                foreach (var pkg in packages) {
                    Console.WriteLine("   {0}", pkg.CanonicalName);
                }
            });
        }

        private Task DoUpdate(IEnumerable<string> parameters, IEnumerable<Package> packages) {
            if (!packages.Any()) {
                PrintNoPackagesFound(parameters);
                return "".AsResultTask();
            }

            var remoteTasks = packages.Select(package => _packageManager.MarkPackageOkToUpdate(package.CanonicalName)).ToArray();
            remoteTasks.ContinueOnFail(ex => FailOnExceptions(ex));
            return remoteTasks.Continue(() => {
                Console.WriteLine("Marked packages as 'do-update' :");
                foreach (var pkg in packages) {
                    Console.WriteLine("   {0}", pkg.CanonicalName);
                }
            });
        }
        private Task DoNotUpgrade(IEnumerable<string>  parameters, IEnumerable<Package> packages) {
            if (!packages.Any()) {
                PrintNoPackagesFound(parameters);
                return "".AsResultTask();
            }

            var remoteTasks = packages.Select(package => _packageManager.MarkPackageDoNotUpgrade(package.CanonicalName)).ToArray();
            remoteTasks.ContinueOnFail(ex => FailOnExceptions(ex));
            return remoteTasks.Continue(() => {
                Console.WriteLine("Marked packages as 'do-not-upgrade' :");
                foreach (var pkg in packages) {
                    Console.WriteLine("   {0}", pkg.CanonicalName);
                }
            });
        }
        private Task DoUpgrade(IEnumerable<string> parameters, IEnumerable<Package> packages) {
            if (!packages.Any()) {
                PrintNoPackagesFound(parameters);
                return "".AsResultTask();
            }

            var remoteTasks = packages.Select(package => _packageManager.MarkPackageOkToUpgrade(package.CanonicalName)).ToArray();
            remoteTasks.ContinueOnFail(ex => FailOnExceptions(ex));
            return remoteTasks.Continue(() => {
                Console.WriteLine("Marked packages as 'do-upgrade' :");
                foreach (var pkg in packages) {
                    Console.WriteLine("   {0}", pkg.CanonicalName);
                }
            });
        }
#endif
        private Task AddFeed(IEnumerable<string> feeds) {
            var tasks = feeds.Select(each => _packageManager.AddSystemFeed(each));
            return tasks.ContinueAlways(antecedents => {
                foreach( var ex in antecedents.Where(each => each.IsFaulted).Select(each => each.Exception.Unwrap()) ) {
                    var coappEx = ex as CoAppException;
                    if (coappEx != null) {
                        Console.WriteLine("    {0}", ex.Message);
                        coappEx.Cancel();
                    }
                }
                
                foreach (var f in antecedents.Where(each => !each.IsFaulted).Select(each => each.Result)) {
                    Console.WriteLine("Adding Feed: {0}", f);
                }

            });
        }

        private Task<IEnumerable<string>>  MatchFeeds(IEnumerable<string> feeds) {
            return _packageManager.Feeds.Continue(systemFeeds => {
                var locations = systemFeeds.Select(each => each.Location).ToArray();

                foreach (var notFeed in feeds.Where(each => !locations.ContainsIgnoreCase(each))) {
                    Console.WriteLine("Skipping '{0}' -- is not registered as a system feed.", notFeed);
                }

                return locations.Where(each => feeds.ContainsIgnoreCase(each));
            });
        }

        private Task DeleteFeed(IEnumerable<string> feeds) {
            var systemFeeds = _packageManager.Feeds.Result.Select(each => each.Location).ToArray();
            
            foreach( var notFeed in feeds.Where(each => !systemFeeds.ContainsIgnoreCase(each))) {
                Console.WriteLine("Skipping '{0}' -- is not registered as a system feed.", notFeed);
            }

            //var tasks = feeds.Where(each => systemFeeds.ContainsIgnoreCase(each)).Select(each => _packageManager.RemoveSystemFeed(each));
            var tasks = systemFeeds.Where(each => feeds.ContainsIgnoreCase(each)).Select(each => _packageManager.RemoveSystemFeed(each));
            return tasks.ContinueAlways(antecedents => {
                foreach (var ex in antecedents.Where(each => each.IsFaulted).Select(each => each.Exception.Unwrap())) {
                    var coappEx = ex as CoAppException;
                    if (coappEx != null) {
                        Console.WriteLine("    {0}", ex.Message);
                        coappEx.Cancel();
                    }
                }

                foreach (var f in antecedents.Where(each => !each.IsFaulted).Select(each => each.Result)) {
                    Console.WriteLine("Removing Feed: {0}", f);
                }
            });
        }

        private void FailOnExceptions(Exception exception) {
            if (exception is OperationCanceledException) {
                // it's been dealt with.
                return;
            }

            var ae = exception as AggregateException;
            IEnumerable<CoAppException> exceptions = (exception as CoAppException).SingleItemAsEnumerable();
            if (ae != null) {
                exceptions = from each in ae.InnerExceptions let fpre = each as CoAppException where fpre != null select fpre;
            }

            if (!exceptions.IsNullOrEmpty()) {
                foreach (var ex in exceptions) {
                    Fail("{0}", ex.Message);
                    ex.Cancel();
                }
            }
        }

        private void GetPackageInfo(IEnumerable<string> parameters, IEnumerable<Package> packages) {
             if (!packages.Any()) {
                PrintNoPackagesFound(parameters);
                return;
            }
#if BROKEN            
            packages.Select(package => _packageManager.GetPackageDetails(package.CanonicalName)).ToArray().Continue(detailedPackages => {
                var length0 = detailedPackages.Max(each => Math.Max(Math.Max(each.Name.Length, each.Architecture.ToString().Length), each.PublisherName.Length)) + 1;
                var length1 = detailedPackages.Max(each => Math.Max(Math.Max(((string)each.Version).Length, each.AuthorVersion.Length), each.PublisherUrl.Length)) + 1;

                foreach (var package in detailedPackages) {
                    var date = DateTime.FromFileTime(long.Parse(package.PublishDate));
                    Console.WriteLine("-----------------------------------------------------------");
                    Console.WriteLine("Package: {0}", package.DisplayName);
                    Console.WriteLine("  Name: {{0,-{0}}}      Architecture:{{1,-{1}}} ".format(length0, length1), package.Name, package.Architecture);
                    Console.WriteLine("  Version: {{0,-{0}}}   Author Version:{{1,-{1}}} ".format(length0, length1), package.Version, package.AuthorVersion);
                    Console.WriteLine("  Published:{0}", date.ToShortDateString());
                    Console.WriteLine("  Local Path:{0}", package.LocalPackagePath);
                    Console.WriteLine("  Publisher: {{0,-{0}}} Location:{{1,-{1}}} ".format(length0, length1), package.PublisherName, package.PublisherUrl);
                    Console.WriteLine("  Installed: {0,-6} Blocked:{1,-6} Required:{2,-6} Active:{3,-6}", package.IsInstalled, package.IsBlocked,
                        package.IsRequired, package.IsActive);
                    Console.WriteLine("  Summary: {0}", package.Summary);
                    Console.WriteLine("  Description: {0}", package.Description);
                    Console.WriteLine("  Copyright: {0}", package.Copyright);
                    Console.WriteLine("  License: {0}", package.License);
                    Console.WriteLine("  License URL: {0}", package.LicenseUrl);
                    if (!package.Tags.IsNullOrEmpty()) {
                        Console.WriteLine("  Tags: {0}", package.Tags.Aggregate((current, each) => current + "," + each));
                    }

                    if (package.RemoteLocations.Any()) {
                        Console.WriteLine("  Remote Locations:");
                        foreach (var location in package.RemoteLocations) {
                            Console.WriteLine("    {0}", location);
                        }
                    }

                    if (package.Dependencies.Any()) {
                        Console.WriteLine("  Package Dependencies:");
                        foreach (var dep in package.Dependencies) {
                            Console.WriteLine("    {0}", dep);
                        }
                    }
                }
                Console.WriteLine("-----------------------------------------------------------");
            });
#endif             
        }


        
        /// <summary>
        /// Lists the packages.
        /// </summary>
        /// <param name="parameters">The parameters.</param>
        /// <remarks></remarks>
        private void PrintPackages(IEnumerable<Package> packages) {
            if (_terse) {
                foreach (var package in packages) {
                    Console.WriteLine("{0} # Installed:{1}", package.CanonicalName, package.IsInstalled);
                }
            }
            else if (packages.Any()) {
                (from pkg in packages
                    orderby pkg.Name
                    select new {
                        pkg.Name,
                        Version = pkg.Version,
                        Arch = pkg.Architecture,
                        Status = (pkg.IsInstalled ? "Installed " + (pkg.IsBlocked ? "Blocked " : "") + (pkg.IsWanted ? "Wanted ": pkg.IsDependency ? "Dependency " : "")+ (pkg.IsActive ? "Active " : "" ) : ""),
                        Location = pkg.IsInstalled ? "(installed)" : !string.IsNullOrEmpty(pkg.LocalPackagePath) ? pkg.LocalPackagePath : (pkg.RemoteLocations.IsNullOrEmpty() ? "<unknown>" :  pkg.RemoteLocations.FirstOrDefault().AbsoluteUri.UrlDecode()),
                    }).ToTable().ConsoleOut();
            }
            else {
                Console.WriteLine("No packages found.");
            }
        }

        private Task ListFeeds() {
            return _packageManager.Feeds.ContinueWith(
                antecedent => {
                    antecedent.RethrowWhenFaulted();

                    var feeds = antecedent.Result;
                    if( feeds.IsNullOrEmpty()) {
                        Console.WriteLine("No Feeds Found.");
                        return;
                    }

                    feeds.Select( each => new {
                         Location = each.Location,
                         Feed_Updated = each.LastScanned == DateTime.MinValue ? "(not scanned)" :  each.LastScanned.ToShortDateString() +" "+each.LastScanned.ToShortTimeString() 
                    }).ToTable().ConsoleOut();

                }, TaskContinuationOptions.AttachedToParent);
        }

        private void UpdatePackages(IEnumerable<Package> packages) {
            ProcessPackages( packages, IsUpdate:true);
        }

        private void InstallPackages(IEnumerable<Package> packages) {
            ProcessPackages(packages);
        }

        private void UpgradePackages(IEnumerable<Package> packages) {
            ProcessPackages(packages, IsUpgrade: true);
        }

        private void ProcessPackages( IEnumerable<Package> packages , bool IsUpdate = false, bool IsUpgrade = false) {
            
        }
        
        private Task InstallPackages(IEnumerable<string> parameters) {
            
            // when this line is placed in the inner scope, the whole thing gets breaky.!?
            CurrentTask.Events += new PackageInstallProgress((canonicalName, progress, overall) => "Installing: {0}".format(canonicalName).PrintProgressBar(progress));

            // given what the user requested, what packages are they really asking for?
            return _packageManager.QueryPackages(parameters, pkgFilter, collectionFilter, _location).Continue(packages => {
                // we got back a package collection for what the user passed in.

                // but, we *can* get back an empty collection...
                if (packages.IsNullOrEmpty()) {
                    PrintNoPackagesFound(parameters);
                    return;
                }

                // we have a collection of packages that the user has requested.
                // first, lets auto-filter out ones that we can obviously see are not what they wanted.
                var findConflictTask = _packageManager.FilterConflictsForInstall(packages, _x86, _x64, _cpuany);

                // hmm. had a problem filtering out conflicts.
                findConflictTask.ContinueOnFail(exception => {
                    Console.WriteLine("Conflict!");
                    Console.WriteLine("{0} == {1}", exception.Message, exception.StackTrace);
                });


                findConflictTask.Continue(filteredPackages => {
                    if (!filteredPackages.Any(each => !each.IsInstalled)) {
                        Console.WriteLine("The following packages are already installed:\r\n");
                        PrintPackages(filteredPackages);
                        return;
                    }

                    // lets get the package install plan.
                    var getPackagePlanTask = _packageManager.IdentifyPackageAndDependenciesToInstall(filteredPackages, _autoUpgrade);

                   

                    // if we get a good plan back
                    getPackagePlanTask.Continue(allPackages => {
                        PrintPackageInstallPlan(allPackages, filteredPackages);
                        // actually run the installer for each package in our original collection
                        if (_pretend == true) {
                            Console.WriteLine(" --pretend specified, skipping install.");
                            return;
                        }


                        foreach (var p in filteredPackages) {
                            try {
                                _packageManager.Install(p.CanonicalName, _autoUpgrade).Continue(() => Console.WriteLine()).Wait();
                            }
                            catch (Exception failed) {
                                failed = failed.Unwrap();
                                Console.WriteLine("Installation failed!");
                                Console.WriteLine("{0} == {1}", failed.Message, failed.StackTrace);
                            }
                        }

                    });
                });
            });
        }

        private Task RemovePackages(IEnumerable<string> parameters ) {

            CurrentTask.Events += new PackageRemoveProgress((name, progress) => "Removing {0}".format(name).PrintProgressBar(progress));

            var removePackagesTask = _packageManager.QueryPackages(parameters,pkgFilter & Package.Properties.Installed.Is(true),null, _location)
                .Continue(packagesToRemove => {
                    if (packagesToRemove.IsNullOrEmpty()) {
                        PrintNoPackagesFound(parameters);
                        return 0;
                    }
                    
                    return _packageManager.RemovePackages(packagesToRemove.Select(each => each.CanonicalName), _force == true).Continue( total => {
                        Console.WriteLine();
                        return total;
                    }).Result;
                });        
                

            removePackagesTask.ContinueOnFail(exception => {
                if( exception is OperationCanceledException ) {
                    // it's been dealt with.
                    return;
                }
                var ae = exception as AggregateException;
                IEnumerable<FailedPackageRemoveException> fpres = (exception as FailedPackageRemoveException).SingleItemAsEnumerable();
                if( ae != null ) {
                    fpres = from each in ae.InnerExceptions let fpre = each as FailedPackageRemoveException where fpre != null select fpre;
                }

                if( !fpres.IsNullOrEmpty()) {
                    Fail("The following packages failed to remove:");
                    foreach( var failedPackage in fpres) {
                        Console.WriteLine("   {0}", failedPackage.Reason);
                        failedPackage.Cancel();
                    }
                }
            });

            return removePackagesTask.Continue((total => {
                Console.WriteLine("\r\nSuccessfully removed {0} packages", total);
            }));
        }

        private static void foot2() {
          
        }

        private void ListPolicies(string policyName = null) {
            _packageManager.Policies.Continue(policies => {
                if (!string.IsNullOrEmpty(policyName)) {
                    policies = policies.Where(each => each.Name == policyName);
                }
                foreach (var policy in policies) {
                    Console.WriteLine("\r\nPolicy: {0} -- {1} ", policy.Name, policy.Description);
                    foreach (var account in policy.Members) {
                        Console.WriteLine("   {0}", account);
                    }
                }
            });
        }

        private void PrintPotentialUpgradeInformation(Package unsatisfiedPackage, IEnumerable<Package> satifactionOptions) {
            Console.WriteLine("The requested package '{0}' has an versions that supercede it:", unsatisfiedPackage.CanonicalName);
            foreach (var p in satifactionOptions) {
                Console.WriteLine("\r\n      {0}", p.CanonicalName);
            }
            Console.WriteLine("\r\nEither use --auto-upgrade to select the most recent package");
            Console.WriteLine("or use --exact to use what was specified");
        }

        private void PrintNoPackagesFound(IEnumerable<string> parameters) {
            Fail("Unable to find any packages matching :");
            foreach (var p in parameters) {
                Console.WriteLine("   {0}", p);
            }
        }

        private void PrintPackageInstallPlan(IEnumerable<Package> allPackages, IEnumerable<Package> requestedPackages) {
            if (!_verbose) {
                (from pkg in allPackages.Where(each => !each.IsInstalled)
                 let getsSatisfied = !(pkg.SatisfiedBy == null || pkg.SatisfiedBy == pkg)
                 where !getsSatisfied
                 orderby pkg.Name
                 select new {
                     pkg.Name,
                     pkg.Version,
                     Arch = pkg.Architecture,
                     Type = getsSatisfied ? "(superceded)" : requestedPackages.Contains(pkg) ? "Requested" : "Dependency",
                     Location =
                         getsSatisfied
                             ? "Satisfied by {0}".format(pkg.SatisfiedBy.CanonicalName)
                             : !string.IsNullOrEmpty(pkg.LocalPackagePath)
                                 ? pkg.LocalPackagePath : (pkg.RemoteLocations.IsNullOrEmpty() ? (pkg.IsInstalled? "(installed)" : "") : pkg.RemoteLocations.FirstOrDefault().AbsoluteUri)
                     // Satisfied_By = getsSatisfied ? "" : pkg.SatisfiedBy.CanonicalName ,
                     // Satisfied_By = pkg.SatisfiedBy == null ? pkg.CanonicalName : pkg.SatisfiedBy.CanonicalName ,
                     // Status = pkg.IsInstalled ? "Installed" : "will install",
                 }).OrderBy(each => each.Type).ToTable().ConsoleOut();
            }
            else {
                // print out the install plan of all packages
                (from pkg in allPackages.Where(each => !each.IsInstalled)
                 let getsSatisfied = !(pkg.SatisfiedBy == null || pkg.SatisfiedBy == pkg)
                 orderby pkg.Name
                 select new {
                     pkg.Name,
                     Version = pkg.Version,
                     Arch = pkg.Architecture,
                     Type = getsSatisfied ? "(superceded)" : requestedPackages.Contains(pkg) ? "Requested" : "Dependency",
                     Location =
                         getsSatisfied
                             ? "Satisfied by {0}".format(pkg.SatisfiedBy.CanonicalName)
                             : !string.IsNullOrEmpty(pkg.LocalPackagePath)
                                 ? pkg.LocalPackagePath : (pkg.RemoteLocations.IsNullOrEmpty() ?(pkg.IsInstalled? "(installed)" : "") : pkg.RemoteLocations.FirstOrDefault().AbsoluteUri)
                     // Satisfied_By = getsSatisfied ? "" : pkg.SatisfiedBy.CanonicalName ,
                     // Satisfied_By = pkg.SatisfiedBy == null ? pkg.CanonicalName : pkg.SatisfiedBy.CanonicalName ,
                     // Status = pkg.IsInstalled ? "Installed" : "will install",
                 }).OrderBy(each => each.Type).ToTable().ConsoleOut();
            }
        }

        private void Verbose(string text, params object[] objs) {
            if (true == _verbose) {
                Console.WriteLine(text.format(objs));
            }
        }
    }
}