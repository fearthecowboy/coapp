//-----------------------------------------------------------------------
// <copyright company="CoApp Project">
//     Copyright (c) 2010-2012 Garrett Serack and CoApp Contributors. 
//     Contributors can be discovered using the 'git log' command.
//     All rights reserved.
// </copyright>
// <license>
//     The software is licensed under the Apache 2.0 License (the "License")
//     You may not use the software except in compliance with the License. 
// </license>
//-----------------------------------------------------------------------

namespace CoApp.Toolkit.Engine {
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;
    using Crypto;
    using Extensions;
    using Feeds;
    using Logging;
    using PackageFormatHandlers;
    using Pipes;
    using Shell;
    using Tasks;
    using Toolkit.Exceptions;
    using Win32;

    public class NewPackageManager : IPackageManager {
        private static Task FinishedSynchronously { get {return CoTask.AsResultTask<object>(null); }}

        public static NewPackageManager Instance = new NewPackageManager();
        private static readonly Regex CanonicalNameParser = new Regex(@"^(.*)-(\d{1,5}\.\d{1,5}\.\d{1,5}\.\d{1,5})-(any|x86|x64|arm)-([0-9a-f]{16})$",RegexOptions.IgnoreCase);

        private readonly List<ManualResetEvent> _manualResetEvents = new List<ManualResetEvent>();
        internal static IncomingCallDispatcher<NewPackageManager> Dispatcher = new IncomingCallDispatcher<NewPackageManager>(Instance);

        private bool CancellationRequested {
            get { return Event<IsCancellationRequested>.RaiseFirst(); }
        }

        private NewPackageManager() {
            // always load the Installed Package Feed.
            PackageFeed.GetPackageFeedFromLocation(InstalledPackageFeed.CanonicalLocation);
        }

        /// <summary>
        /// feeds that we should try to load as system feeds
        /// </summary>
        private IEnumerable<string> SystemFeedLocations {
            get {
                if (PackageManagerSettings.CoAppSettings["#feedLocations"].HasValue) {
                    return PackageManagerSettings.CoAppSettings["#feedLocations"].StringsValue;
                }
                // defaults to the installed packages feed 
                // and the default coapp feed.
                return "http://coapp.org/feed".SingleItemAsEnumerable();
            }
        }

        private IEnumerable<string> SessionFeedLocations {
            get { 
                return  SessionCache<IEnumerable<string>>.Value["session-feeds"] ?? Enumerable.Empty<string>();
            }
        }

        private void AddSessionFeed( string feedLocation ) {
            lock (this) {
                feedLocation = feedLocation.CanonicalizePathWithWildcards();
                var sessionFeeds = SessionFeedLocations.Union(feedLocation.SingleItemAsEnumerable()).Distinct();
                SessionCache<IEnumerable<string>>.Value["session-feeds"] = sessionFeeds.ToArray();
            }
        }

        private void AddSystemFeed(string feedLocation) {
            lock (this) {
                feedLocation = feedLocation.CanonicalizePathWithWildcards();

                var systemFeeds = SystemFeedLocations.Union(feedLocation.SingleItemAsEnumerable()).Distinct();
                PackageManagerSettings.CoAppSettings["#feedLocations"].StringsValue = systemFeeds.ToArray();
            }
        }

        private void RemoveSessionFeed(string feedLocation) {
            lock (this) {
                feedLocation = feedLocation.CanonicalizePathWithWildcards();

                var sessionFeeds = from emove in SessionFeedLocations where !emove.Equals(feedLocation, StringComparison.CurrentCultureIgnoreCase) select emove;
                SessionCache<IEnumerable<string>>.Value["session-feeds"] = sessionFeeds.ToArray();
                
                // remove it from the cached feeds
                SessionCache<PackageFeed>.Value.Clear(feedLocation);
            }
        }

        private void RemoveSystemFeed(string feedLocation) {
            lock (this) {
                feedLocation = feedLocation.CanonicalizePathWithWildcards();

                var systemFeeds = from feed in SystemFeedLocations where !feed.Equals(feedLocation, StringComparison.CurrentCultureIgnoreCase) select feed;
                PackageManagerSettings.CoAppSettings["#feedLocations"].StringsValue = systemFeeds.ToArray();

                // remove it from the cached feeds
                Cache<PackageFeed>.Value.Clear(feedLocation);
            }
        }
        
        internal IEnumerable<Task> LoadSystemFeeds() {
            // load system feeds

            var systemCacheLoaded = SessionCache<string>.Value["system-cache-loaded"];
            if (systemCacheLoaded.IsTrue()) {
                yield break;
            }

            SessionCache<string>.Value["system-cache-loaded"] = "true";
            
            foreach (var f in SystemFeedLocations) {
                var feedLocation = f;
                yield return PackageFeed.GetPackageFeedFromLocation(feedLocation).ContinueWith(antecedent => {
                    if (antecedent.Result != null) {
                        Cache<PackageFeed>.Value[feedLocation] = antecedent.Result;
                    }
                    else {
                        Logger.Error("Feed {0} was unable to load.", feedLocation);
                    }
                }, TaskContinuationOptions.AttachedToParent);
            }
        }

        public Task FindPackages(string canonicalName, string name, string version, string arch, string publicKeyToken,
            bool? dependencies, bool? installed, bool? active, bool? required, bool? blocked, bool? latest,
            int? index, int? maxResults, string location, bool? forceScan, bool? updates, bool? upgrades, bool? trimable) {

            IPackageManagerResponse response = Event<GetResponseInterface>.RaiseFirst();

            if (CancellationRequested) {
                Event<GetResponseInterface>.RaiseFirst().OperationCanceled("find-package");
                return FinishedSynchronously;
            }

            if (Event<CheckForPermission>.RaiseFirst(PermissionPolicy.EnumeratePackages)) {

                UpdateIsRequestedFlags();

                // get basic list of packages based on primary characteristics
                if (!string.IsNullOrEmpty(canonicalName)) {
                    // if canonical name is passed, override name,version,pkt,arch with the parsed canonicalname.
                    var match = CanonicalNameParser.Match(canonicalName.ToLower());
                    if (!match.Success) {
                        Event<GetResponseInterface>.RaiseFirst().Error("find-packages", "canonical-name",
                            "Canonical name '{0}' does not appear to be a valid canonical name".format(canonicalName));
                        return FinishedSynchronously;
                    }

                    name = match.Groups[1].Captures[0].Value;
                    version = match.Groups[2].Captures[0].Value;
                    arch = match.Groups[3].Captures[0].Value;
                    publicKeyToken = match.Groups[4].Captures[0].Value;
                }

                if (forceScan == true) {
                    foreach (var feed in Feeds) {
                        feed.Stale = true;
                    }
                }

                var results = SearchForPackages(name, version, arch, publicKeyToken, location).ToArray();
                // filter results of list based on secondary filters


                // if we are upgrading or installing, we need to find packages that are already installed.
                var i = (upgrades == true || updates == true);
                if (i) {
                    installed = installed ?? i;
                }


                results = (from package in results
                    where
                        (installed == null || package.IsInstalled == installed) && (active == null || package.IsActive == active) &&
                            (required == null || package.IsRequired == required) && (blocked == null || package.IsBlocked == blocked)


                    select package).ToArray();

                if (updates == true) {
                    // if the client is asking for Updates:
                    //      - get the packages that match the search criteria. 'pkgs'
                    //      - filter 'pkgs' so that every package in the list is the most recent binary compatible one.
                    //      - filter out 'do-not-update' and 'blocked' packages (unless passing in blocked flag)
                    //      - for each package in 'pkgs', find out if there is a higher binary compatible one available that is not installed.
                    //          - add that to the list of results; return the distinct results

                    var tmp = results.ToArray();

                    results = tmp.Where(each =>
                        !tmp.Any(x => x.IsAnUpdateFor(each)) &&
                            !each.DoNotUpdate &&
                                (blocked ?? !each.IsBlocked)).ToArray();

                    // set the new results set 
                    results =
                        (from r in results
                        let familyPackages = SearchForPackages(r.Name, null, r.Architecture, r.PublicKeyToken).Where(each => !each.IsInstalled).OrderByDescending(each => each.Version)
                        select familyPackages.FirstOrDefault(each => each.IsAnUpdateFor(r))
                        into updatePkg
                        where updatePkg != null
                        select updatePkg).ToArray();
                } else if (upgrades == true) {
                    // if the client is asking for Upgrades:
                    //      - get list packages that meet the search criteria. 'pkgs'. 
                    //      - filter out 'do-not-upgrade' and 'do-not-update' and 'blocked' packages--(unless passing in blocked flag)
                    //      - (by definition upgrades exclude updates)
                    //      - for each packge in 'pkgs' find out if ther is a higher (non-compatible) version that is not installed.
                    //          - add that to the list of results; return the distinct results
                    results = results.Where(each =>
                        !each.DoNotUpgrade &&
                            !each.DoNotUpdate &&
                                (blocked ?? !each.IsBlocked)).ToArray();

                    results =
                        (from r in results
                        let familyPackages = SearchForPackages(r.Name, null, r.Architecture, r.PublicKeyToken).Where(each => !each.IsInstalled).OrderByDescending(each => each.Version)
                        select familyPackages.FirstOrDefault(each => each.IsAnUpgradeFor(r))
                        into upgradePkg
                        where upgradePkg != null
                        select upgradePkg).ToArray();

                } else if (trimable == true) {

                }

                // if the client is asking for Trimable packages
                //      -  get the list of packages that meet the search criteria. 'pkgs'.
                //      -  a package is not 'trimable' if:
                //          - if it is marked client-requested 
                //          - if it is a required dependency of another package that is client requested and that doesn't have a binary compatible update insatlled.
                //          - it is blocked (unless passing in the blocked flag)
                //          - it is marked 'do-not-update'

                // only the latest?
                if (latest == true) {
                    results = results.HighestPackages();
                }

                // if the client has asked for the dependencies as well, include them in the result set.
                // otherwise the client will get the names in 
                if (dependencies == true) {
                    // grab the dependencies too.
                    var deps = results.SelectMany(each => each.InternalPackageData.Dependencies).Distinct();

                    if (latest == true) {
                        deps = deps.HighestPackages();
                    }

                    results = results.Union(deps).Distinct().ToArray();
                }

                // paginate the results
                if (index.HasValue) {
                    results = results.Skip(index.Value).ToArray();
                }

                if (maxResults.HasValue) {
                    results = results.Take(maxResults.Value).ToArray();
                }
                
                if (results.Any()) {
                    foreach (var pkg in results) {
                        var package = pkg;
                        if (CancellationRequested) {
                            Event<GetResponseInterface>.RaiseFirst().OperationCanceled("find-packages");
                            return FinishedSynchronously;
                        } 

                        var supercedents = (from p in SearchForPackages(package.Name, null, package.Architecture.ToString(), package.PublicKeyToken)
                            where p.InternalPackageData.PolicyMinimumVersion <= package.Version && p.InternalPackageData.PolicyMaximumVersion >= package.Version
                            select p).OrderByDescending(p => p.Version).ToArray();

                        PackageInformation(response, package, supercedents.Select(each => each.CanonicalName));
                    }
                } else {
                    Event<GetResponseInterface>.RaiseFirst().NoPackagesFound();
                }
            }
            return FinishedSynchronously;
        }

        public Task GetPackageDetails(string canonicalName) {
            if (CancellationRequested) {
                Event<GetResponseInterface>.RaiseFirst().OperationCanceled("get-package-details");
                return FinishedSynchronously;
            }

            var package = GetSinglePackage(canonicalName, "get-package-details");
            if (package == null) {
                return FinishedSynchronously;
            }

            Event<GetResponseInterface>.RaiseFirst().PackageDetails(package.CanonicalName, new Dictionary<string, string> {
                {"description", package.PackageDetails.Description},
                {"summary", package.PackageDetails.SummaryDescription},
                {"display-name", package.DisplayName},
                {"copyright", package.PackageDetails.CopyrightStatement},
                {"author-version", package.PackageDetails.AuthorVersion},
            },
                package.PackageDetails.IconLocations,
                package.PackageDetails.Licenses.ToDictionary(each => each.Name, each => each.Text),
                package.InternalPackageData.Roles.ToDictionary(each => each.Name, each => each.PackageRole.ToString()),
                package.PackageDetails.Tags,
                package.PackageDetails.Contributors.ToDictionary(each => each.Name, each => each.Location.AbsoluteUri),
                package.PackageDetails.Contributors.ToDictionary(each => each.Name, each => each.Email));
            return FinishedSynchronously;
        }

        public Task InstallPackage(string canonicalName, bool? autoUpgrade, bool? force, bool? download, bool? pretend, bool? isUpdating, bool? isUpgrading) {
            if (CancellationRequested) {
                Event<GetResponseInterface>.RaiseFirst().OperationCanceled("install-package");
                return FinishedSynchronously;
            }

            double[] overallProgress = {0.0};
            double[] eachTaskIsWorth = {0.0};
            var numberOfPackagesToInstall = 0;
            var numberOfPackagesToDownload = 0;

            using (var manualResetEvent = new ManualResetEvent(true)) {
                try {
                    lock (_manualResetEvents) {
                        _manualResetEvents.Add(manualResetEvent);
                    }

                    var packagesTriedToDownloadThisTask = new List<Package>();
                    var package = GetSinglePackage(canonicalName, "install-package");

                    if (package == null) {
                        Event<GetResponseInterface>.RaiseFirst().UnknownPackage(canonicalName);
                        return FinishedSynchronously;
                    }

                    if (package.IsBlocked) {
                        Event<GetResponseInterface>.RaiseFirst().PackageBlocked(canonicalName);
                        return FinishedSynchronously;
                    }

                    var installedPackages = SearchForInstalledPackages(package.Name, null, package.Architecture.ToString(), package.PublicKeyToken).ToArray();
                    var installedCompatibleVersions = installedPackages.Where(each => each.Version >= package.InternalPackageData.PolicyMinimumVersion && each.Version <= package.InternalPackageData.PolicyMaximumVersion).ToArray();

                    // is the user authorized to install this?
                    var highestInstalledPackage = installedPackages.HighestPackages().FirstOrDefault();

                    if (highestInstalledPackage != null && highestInstalledPackage.Version < package.Version) {
                        if (!Event<CheckForPermission>.RaiseFirst(PermissionPolicy.UpdatePackage)) {
                            return FinishedSynchronously;
                        }
                    } else {
                        if (!Event<CheckForPermission>.RaiseFirst(PermissionPolicy.InstallPackage)) {
                            return FinishedSynchronously;
                        }
                    }

                    // if this is an update, 
                    //      - check to see if there is a compatible package already installed that is marked do-not-update
                    //        fail if so.
                    if (isUpdating == true && installedCompatibleVersions.Any(each => each.DoNotUpdate)) {
                        Event<GetResponseInterface>.RaiseFirst().PackageBlocked(canonicalName);
                    }

                    // if this is an upgrade, 
                    //      - check to see if this package has the do-not-upgrade flag.
                    if (isUpgrading == true && package.DoNotUpgrade) {
                        Event<GetResponseInterface>.RaiseFirst().PackageBlocked(canonicalName);
                    }

                    // mark the package as the client requested.
                    package.PackageSessionData.DoNotSupercede = (false == autoUpgrade);
                    package.PackageSessionData.UpgradeAsNeeded = (true == autoUpgrade);
                    package.PackageSessionData.IsClientSpecified = true;

                    // the resolve-acquire-install-loop
                    do {
                        // if the world changes, this will get set somewhere between here and the 
                        // other end of the do-loop.
                        manualResetEvent.Reset();

                        if (CancellationRequested) {
                            Event<GetResponseInterface>.RaiseFirst().OperationCanceled("install-package");
                            return FinishedSynchronously;
                        }

                        IEnumerable<Package> installGraph;
                        try {
                            installGraph = GenerateInstallGraph(package).ToArray();
                        } catch (OperationCompletedBeforeResultException) {
                            // we encountered an unresolvable condition in the install graph.
                            // messages should have already been sent.
                            Event<GetResponseInterface>.RaiseFirst().FailedPackageInstall(canonicalName, package.InternalPackageData.LocalLocation,
                                "One or more dependencies are unable to be resolved.");
                            return FinishedSynchronously;
                        }

                        // seems like a good time to check if we're supposed to bail...
                        if (CancellationRequested) {
                            Event<GetResponseInterface>.RaiseFirst().OperationCanceled("install-package");
                            return FinishedSynchronously;
                        }
                        var response = Event<GetResponseInterface>.RaiseFirst();
                        if (download == false && pretend == true) {
                            // we can just return a bunch of foundpackage messages, since we're not going to be 
                            // actually installing anything, nor trying to download anything.
                            foreach (var p in installGraph) {
                                PackageInformation(response,p);
                            }
                            return FinishedSynchronously;
                        }

                        // we've got an install graph.
                        // let's see if we've got all the files
                        var missingFiles = (from p in installGraph where !p.InternalPackageData.HasLocalLocation select p).ToArray();

                        if (download == true) {
                            // we want to try downloading all the files that we're missing, regardless if we've tried before.
                            // unless we've already tried in this task once. 
                            foreach (var p in missingFiles.Where(packagesTriedToDownloadThisTask.Contains)) {
                                packagesTriedToDownloadThisTask.Add(p);
                                p.PackageSessionData.CouldNotDownload = false;
                            }
                        }

                        if (numberOfPackagesToInstall != installGraph.Count() || numberOfPackagesToDownload != missingFiles.Count()) {
                            // recalculate the rest of the install progress based on the new install graph.
                            numberOfPackagesToInstall = installGraph.Count();
                            numberOfPackagesToDownload = missingFiles.Count();

                            eachTaskIsWorth[0] = (1.0 - overallProgress[0])/(numberOfPackagesToInstall + numberOfPackagesToDownload);
                        }

                        if (missingFiles.Any()) {
                            // we've got some packages to install that don't have files.
                            foreach (var p in missingFiles.Where(p => !p.PackageSessionData.HasRequestedDownload)) {
                                Event<GetResponseInterface>.RaiseFirst().RequireRemoteFile(p.CanonicalName,
                                    p.InternalPackageData.RemoteLocations, PackageManagerSettings.CoAppPackageCache, false);

                                p.PackageSessionData.HasRequestedDownload = true;
                            }
                        } else {
                            if (pretend == true) {
                                // we can just return a bunch of found-package messages, since we're not going to be 
                                // actually installing anything, and everything we needed is downloaded.
                                foreach (var p in installGraph) {
                                    PackageInformation(response,p);
                                }
                                return FinishedSynchronously;
                            }

                            var failed = false;
                            // no missing files? Check
                            // complete install graph? Check

                            foreach (var p in installGraph) {
                                var pkg = p;
                                // seems like a good time to check if we're supposed to bail...
                                if (CancellationRequested) {
                                    Event<GetResponseInterface>.RaiseFirst().OperationCanceled("install-package");
                                    return FinishedSynchronously;
                                }
                                var validLocation = pkg.PackageSessionData.LocalValidatedLocation;

                                try {
                                    if (!pkg.IsInstalled) {
                                        if (string.IsNullOrEmpty(validLocation)) {
                                            // can't find a valid location
                                            Event<GetResponseInterface>.RaiseFirst().FailedPackageInstall(pkg.CanonicalName, pkg.InternalPackageData.LocalLocation, "Can not find local valid package");
                                            pkg.PackageSessionData.PackageFailedInstall = true;
                                        } else {

                                            var lastProgress = 0;
                                            // GS01: We should put a softer lock here to keep the client aware that packages 
                                            // are being installed on other threads...
                                            lock (typeof (MSIBase)) {
                                                if (EngineService.DoesTheServiceNeedARestart) {
                                                    // something has changed where we need restart the service before we can continue.
                                                    // and the one place we don't wanna be when we issue a shutdown in in Install :) ...
                                                    EngineService.RestartService();
                                                    Event<GetResponseInterface>.RaiseFirst().OperationCanceled("install-package");
                                                    return FinishedSynchronously;
                                                }

                                                pkg.Install(percentage => {
                                                    overallProgress[0] += ((percentage - lastProgress)*eachTaskIsWorth[0])/100;
                                                    lastProgress = percentage;
                                                    Event<GetResponseInterface>.RaiseFirst().InstallingPackageProgress(pkg.CanonicalName, percentage, (int)(overallProgress[0]*100));
                                                });
                                            }
                                            overallProgress[0] += ((100 - lastProgress)*eachTaskIsWorth[0])/100;
                                            Event<GetResponseInterface>.RaiseFirst().InstallingPackageProgress(pkg.CanonicalName, 100, (int)(overallProgress[0]*100));
                                            Event<GetResponseInterface>.RaiseFirst().InstalledPackage(pkg.CanonicalName);
                                            Signals.InstalledPackage(pkg.CanonicalName);
                                        }
                                    }
                                } catch (Exception e) /* (PackageInstallFailedException pife)  */ {
                                    Logger.Error("FAILED INSTALL");
                                    Logger.Error(e);

                                    Event<GetResponseInterface>.RaiseFirst().FailedPackageInstall(pkg.CanonicalName, validLocation, "Package failed to install.");
                                    pkg.PackageSessionData.PackageFailedInstall = true;

                                    if (!pkg.PackageSessionData.AllowedToSupercede) {
                                        throw new OperationCompletedBeforeResultException(); // user specified packge as critical.
                                    }
                                    failed = true;
                                    break;
                                }
                            }
                            if (!failed) {
                                if (isUpdating == true) {
                                    // if this is marked as an update
                                    // remove REQUESTED flag from all older compatible version 
                                    foreach (var pkg in installedCompatibleVersions) {
                                        pkg.IsClientRequested = false;
                                    }

                                }
                                if (isUpgrading == true) {
                                    // if this is marked as an update
                                    // remove REQUESTED flag from all older compatible version 
                                    foreach (var pkg in installedPackages) {
                                        pkg.IsClientRequested = false;
                                    }
                                }

                                // W00T ... We did it!
                                // check for restart required...
                                if (EngineService.DoesTheServiceNeedARestart) {
                                    // something has changed where we need restart the service before we can continue.
                                    // and the one place we don't wanna be when we issue a shutdown in in Install :) ...
                                    Event<GetResponseInterface>.RaiseFirst().Restarting();
                                    EngineService.RestartService();
                                    return FinishedSynchronously;
                                }
                                return FinishedSynchronously;
                            }

                            // otherwise, let's run it thru again. maybe it'll come together.
                        }

                        //----------------------------------------------------------------------------
                        // wait until either the manualResetEvent is set, but check every second or so
                        // to see if the client has cancelled the operation.
                        while (!manualResetEvent.WaitOne(500)) {
                            if (CancellationRequested) {
                                Event<GetResponseInterface>.RaiseFirst().OperationCanceled("install-package");
                                return FinishedSynchronously;
                            }

                            // we can also use this opportunity to update progress on any outstanding download tasks.
                            overallProgress[0] += missingFiles.Sum(missingFile => ((missingFile.PackageSessionData.DownloadProgressDelta*eachTaskIsWorth[0])/100));
                        }
                    } while (true);

                } catch (OperationCompletedBeforeResultException) {
                    // can't continue with options given.
                    return FinishedSynchronously;
                } finally {
                    // remove manualResetEvent from the mre list
                    lock (_manualResetEvents) {
                        _manualResetEvents.Remove(manualResetEvent);
                    }
                }
            }
        }

        public Task DownloadProgress(string canonicalName, int? downloadProgress) {
            try {
                // it takes a non-trivial amount of time to lookup a package by its name.
                // so, we're going to cache the package in the session.
                // of course if there isn't one, (because we're downloading soemthing we don't know what it's actualy canonical name is)
                // we don't want to try looking up each time again, since that's the worst-case-scenario, we have to
                // cache the fact that we have cached nothing.
                // /facepalm.

                Package package;

                var cachedPackageName = SessionCache<string>.Value["cached-the-lookup" + canonicalName];

                if (cachedPackageName == null) {
                    SessionCache<string>.Value["cached-the-lookup" + canonicalName] = "yes";

                    package = GetSinglePackage(canonicalName, "download-progress", true);

                    if (package != null) {
                        SessionCache<Package>.Value[canonicalName] = package;
                    }
                } else {
                    package = SessionCache<Package>.Value[canonicalName];
                }

                if (package != null) {
                    package.PackageSessionData.DownloadProgress = Math.Max(package.PackageSessionData.DownloadProgress, downloadProgress.GetValueOrDefault());
                }
            } catch {
                // suppress any exceptions... we just don't care!
            }
            SessionCache<string>.Value["busy" + canonicalName] = null;
            return FinishedSynchronously;
        }
        public Task ListFeeds(int? index, int? maxResults) {
            if (CancellationRequested) {
                Event<GetResponseInterface>.RaiseFirst().OperationCanceled("list-feeds");
                return FinishedSynchronously;
            }

            var canFilterSession = Event<CheckForPermission>.RaiseFirst(PermissionPolicy.EditSessionFeeds);
            var canFilterSystem = Event<CheckForPermission>.RaiseFirst(PermissionPolicy.EditSystemFeeds);

            var activeSessionFeeds = SessionCache<PackageFeed>.Value.SessionValues;
            var activeSystemFeeds = Cache<PackageFeed>.Value.Values;

            var x = from feedLocation in SystemFeedLocations
                let theFeed = activeSystemFeeds.FirstOrDefault(each => each.IsLocationMatch(feedLocation))
                let validated = theFeed != null
                select new {
                    feed = feedLocation,
                    LastScanned = validated ? theFeed.LastScanned : DateTime.MinValue,
                    session = false,
                    suppressed = canFilterSystem && BlockedScanLocations.Contains(feedLocation),
                    validated,
                };

            var y = from feedLocation in SessionFeedLocations
                let theFeed = activeSessionFeeds.FirstOrDefault(each => each.IsLocationMatch(feedLocation))
                let validated = theFeed != null
                select new {
                    feed = feedLocation,
                    LastScanned = validated ? theFeed.LastScanned : DateTime.MinValue,
                    session = true,
                    suppressed = canFilterSession && BlockedScanLocations.Contains(feedLocation),
                    validated,
                };

            var results = x.Union(y).ToArray();

            // paginate the results
            if (index.HasValue) {
                results = results.Skip(index.Value).ToArray();
            }

            if (maxResults.HasValue) {
                results = results.Take(maxResults.Value).ToArray();
            }

            if (results.Any()) {
                foreach (var f in results) {
                    var state = PackageManagerSettings.PerPackageSettings[f.feed, "state"].StringValue;
                    if (string.IsNullOrEmpty(state)) {
                        state = "active";
                    }
                    Event<GetResponseInterface>.RaiseFirst().FeedDetails(f.feed, f.LastScanned, f.session, f.suppressed, f.validated, state);
                }
            } else {
                Event<GetResponseInterface>.RaiseFirst().NoFeedsFound();
            }
            return FinishedSynchronously;
        }

        public Task RemoveFeed(string location, bool? session) {

            if (CancellationRequested) {
                Event<GetResponseInterface>.RaiseFirst().OperationCanceled("remove-feed");
                return FinishedSynchronously;
            }

            // Note: This may need better lookup/matching for the location
            // as location can be a fuzzy match.

            if (session ?? false) {
                // session feed specfied
                if (Event<CheckForPermission>.RaiseFirst(PermissionPolicy.EditSessionFeeds)) {
                    RemoveSessionFeed(location);
                    Event<GetResponseInterface>.RaiseFirst().FeedRemoved(location);
                }
            } else {
                // system feed specified
                if (Event<CheckForPermission>.RaiseFirst(PermissionPolicy.EditSystemFeeds)) {
                    RemoveSystemFeed(location);
                    Event<GetResponseInterface>.RaiseFirst().FeedRemoved(location);
                }
            }
            return FinishedSynchronously;
        }

        public Task  AddFeed(string location, bool? session) {
            if (CancellationRequested) {
                Event<GetResponseInterface>.RaiseFirst().OperationCanceled("add-feed");
                return FinishedSynchronously;
            }

            if (session ?? false) {
                // new feed is a session feed
                // session feed specfied
                if (Event<CheckForPermission>.RaiseFirst(PermissionPolicy.EditSessionFeeds)) {
                    // check if it is already a system feed
                    if (SystemFeedLocations.Contains(location)) {
                        Event<GetResponseInterface>.RaiseFirst().Warning("add-feed", "location", "location '{0}' is already a system feed".format(location));
                        return FinishedSynchronously;
                    }

                    if (SessionFeedLocations.Contains(location)) {
                        Event<GetResponseInterface>.RaiseFirst().Warning("add-feed", "location", "location '{0}' is already a session feed".format(location));
                        return FinishedSynchronously;
                    }

                    // add feed to the session feeds.
                    PackageFeed.GetPackageFeedFromLocation(location).ContinueWith(antecedent => {
                        var foundFeed = antecedent.Result;
                        if (foundFeed != null) {

                            AddSessionFeed(location);
                            Event<GetResponseInterface>.RaiseFirst().FeedAdded(location);

                            if (foundFeed != SessionPackageFeed.Instance || foundFeed != InstalledPackageFeed.Instance) {
                                SessionCache<PackageFeed>.Value[location] = foundFeed;
                            }
                        }
                        else {
                            Event<GetResponseInterface>.RaiseFirst().Error("add-feed", "location",
                                "failed to recognize location '{0}' as a valid package feed".format(location));
                            Logger.Error("Feed {0} was unable to load.", location);
                        }
                    }, TaskContinuationOptions.AttachedToParent);
                }
            }
            else {
                // new feed is a system feed
                if (Event<CheckForPermission>.RaiseFirst(PermissionPolicy.EditSystemFeeds)) {
                    if (SystemFeedLocations.Contains(location)) {
                        Event<GetResponseInterface>.RaiseFirst().Warning("add-feed", "location", "location '{0}' is already a system feed".format(location));
                        return FinishedSynchronously;
                    }

                    // add feed to the system feeds.
                    PackageFeed.GetPackageFeedFromLocation(location).ContinueWith(antecedent => {
                        var foundFeed = antecedent.Result;
                        if (foundFeed != null) {

                            AddSystemFeed(location);
                            Event<GetResponseInterface>.RaiseFirst().FeedAdded(location);

                            if (foundFeed != SessionPackageFeed.Instance || foundFeed != InstalledPackageFeed.Instance) {
                                Cache<PackageFeed>.Value[location] = foundFeed;
                            }
                        } else {
                            Event<GetResponseInterface>.RaiseFirst().Error("add-feed", "location", "failed to recognize location '{0}' as a valid package feed".format(location));
                            Logger.Error("Feed {0} was unable to load.", location);
                        }
                    }, TaskContinuationOptions.AttachedToParent);
                }
            }
            return FinishedSynchronously;
        }

        public Task VerifyFileSignature(string filename) {
            if (CancellationRequested) {
                Event<GetResponseInterface>.RaiseFirst().OperationCanceled("verify-signature");
                return FinishedSynchronously;
            }

            if (string.IsNullOrEmpty(filename)) {
                Event<GetResponseInterface>.RaiseFirst().Error("verify-signature", "filename", "parameter 'filename' is required to verify a file");
                return FinishedSynchronously;
            }

            var location = Event<GetCanonicalizedPath>.RaiseFirst(filename);

            if (!File.Exists(location)) {
                Event<GetResponseInterface>.RaiseFirst().FileNotFound(location);
                return FinishedSynchronously;
            }

            var r = Verifier.HasValidSignature(location);
            Event<GetResponseInterface>.RaiseFirst().SignatureValidation(location, r, r ? Verifier.GetPublisherInformation(location)["PublisherName"] : null);
            return FinishedSynchronously;
        }

        public Task SetPackage(string canonicalName, bool? active, bool? required, bool? blocked, bool? doNotUpdate, bool? doNotUpgrade) {
            if (CancellationRequested) {
                Event<GetResponseInterface>.RaiseFirst().OperationCanceled("set-package");
                return FinishedSynchronously;
            }

            var package = GetSinglePackage(canonicalName, "set-package");

            if (package == null) {
                Event<GetResponseInterface>.RaiseFirst().UnknownPackage(canonicalName);
                return FinishedSynchronously;
            }

            if (!package.IsInstalled) {
                Event<GetResponseInterface>.RaiseFirst().Error("set-package", "canonical-name", "package '{0}' is not installed.".format(canonicalName));
                return FinishedSynchronously;
            }

            // seems like a good time to check if we're supposed to bail...
            if (CancellationRequested) {
                Event<GetResponseInterface>.RaiseFirst().OperationCanceled("set-package");
                return FinishedSynchronously;
            }

            if (true == active) {
                if (Event<CheckForPermission>.RaiseFirst(PermissionPolicy.ChangeActivePackage)) {
                    package.SetPackageCurrent();
                }
            }

            if (false == active) {
                if (Event<CheckForPermission>.RaiseFirst(PermissionPolicy.ChangeActivePackage)) {
                    var pqg = SearchForInstalledPackages(package.Name, null, package.Architecture.ToString(), package.PublicKeyToken).HighestPackages().FirstOrDefault();
                    if( pqg != null ) {
                        pqg.SetPackageCurrent();
                    }
                }
            }

            if (true == required) {
                if (Event<CheckForPermission>.RaiseFirst(PermissionPolicy.ChangeRequiredState)) {
                    package.IsRequired = true;
                }
            }

            if (false == required) {
                if (Event<CheckForPermission>.RaiseFirst(PermissionPolicy.ChangeRequiredState)) {
                    package.IsRequired = false;
                }
            }

            if (true == blocked) {
                if (Event<CheckForPermission>.RaiseFirst(PermissionPolicy.ChangeBlockedState)) {
                    package.IsBlocked = true;
                }
            }

            if (false == blocked) {
                if (Event<CheckForPermission>.RaiseFirst(PermissionPolicy.ChangeBlockedState)) {
                    package.IsBlocked = false;
                }
            }

            if (true == doNotUpdate) {
                if (Event<CheckForPermission>.RaiseFirst(PermissionPolicy.InstallPackage)) {
                    package.DoNotUpdate = true;
                }
            }
            if (false == doNotUpdate) {
                if (Event<CheckForPermission>.RaiseFirst(PermissionPolicy.InstallPackage)) {
                    package.DoNotUpdate = false;
                }
            }

            if (true == doNotUpgrade) {
                if (Event<CheckForPermission>.RaiseFirst(PermissionPolicy.InstallPackage)) {
                    package.DoNotUpgrade = true;
                }
            }
            if (false == doNotUpgrade) {
                if (Event<CheckForPermission>.RaiseFirst(PermissionPolicy.InstallPackage)) {
                    package.DoNotUpgrade = false;
                }
            }
            PackageInformation(Event<GetResponseInterface>.RaiseFirst(),package);
            return FinishedSynchronously;
        }

        public Task RemovePackage(string canonicalName, bool? force) {
            if (CancellationRequested) {
                Event<GetResponseInterface>.RaiseFirst().OperationCanceled("remove-package");
                return FinishedSynchronously;
            }

            if (Event<CheckForPermission>.RaiseFirst(PermissionPolicy.RemovePackage)) {
                var package = GetSinglePackage(canonicalName, "remove-package");
                if (package == null) {
                    Event<GetResponseInterface>.RaiseFirst().UnknownPackage(canonicalName);
                    return FinishedSynchronously;
                }

                if (package.Name.Equals("coapp.toolkit", StringComparison.CurrentCultureIgnoreCase) && package.PublicKeyToken.Equals("1e373a58e25250cb") && package.IsActive) {
                    Event<GetResponseInterface>.RaiseFirst().Error("remove-package", "canonical-name", "Active CoApp Engine may not be removed");
                    return FinishedSynchronously;
                }

                if (!package.IsInstalled) {
                    Event<GetResponseInterface>.RaiseFirst().Error("remove-package", "canonical-name", "package '{0}' is not installed.".format(canonicalName));
                    return FinishedSynchronously;
                }

                if (package.IsBlocked) {
                    Event<GetResponseInterface>.RaiseFirst().PackageBlocked(canonicalName);
                    return FinishedSynchronously;
                }
                if (true != force) {
                    UpdateIsRequestedFlags();
                    if (package.PackageSessionData.IsDependency) {
                        Event<GetResponseInterface>.RaiseFirst().FailedPackageRemoval(canonicalName,
                            "Package '{0}' is a required dependency of another package.".format(canonicalName));
                        return FinishedSynchronously;
                    }

                }
                // seems like a good time to check if we're supposed to bail...
                if (CancellationRequested) {
                    Event<GetResponseInterface>.RaiseFirst().OperationCanceled("remove-package");
                    return FinishedSynchronously;
                }

                try {
                    package.Remove(percentage => Event<GetResponseInterface>.RaiseFirst().RemovingPackageProgress(package.CanonicalName, percentage));

                    Event<GetResponseInterface>.RaiseFirst().RemovingPackageProgress(canonicalName, 100);
                    Event<GetResponseInterface>.RaiseFirst().RemovedPackage(canonicalName);

                    Signals.RemovedPackage(canonicalName);
                } catch (OperationCompletedBeforeResultException e) {
                    Event<GetResponseInterface>.RaiseFirst().FailedPackageRemoval(canonicalName, e.Message);
                    return FinishedSynchronously;
                }
            }
            return FinishedSynchronously;
        }

        public Task UnableToAcquire(string canonicalName) {


            if (CancellationRequested) {
                Event<GetResponseInterface>.RaiseFirst().OperationCanceled("unable-to-acquire");
                return FinishedSynchronously;
            }


            if (canonicalName.IsNullOrEmpty()) {
                Event<GetResponseInterface>.RaiseFirst().Error("unable-to-acquire", "canonical-name", "canonical-name is required.");
                return FinishedSynchronously;
            }

            // if there is a continuation task for the canonical name that goes along with this, 
            // we should continue with that task, and get the heck out of here.
            // 

            var package = GetSinglePackage(canonicalName, null);
            if (package != null) {
                package.PackageSessionData.CouldNotDownload = true;
            }

            var continuationTask = SessionCache<Task<Recognizer.RecognitionInfo>>.Value[canonicalName];
            SessionCache<Task<Recognizer.RecognitionInfo>>.Value.Clear(canonicalName);
            Updated(); // do an updated regardless.

            if (continuationTask != null) {
                // notify threads that we're not going to be able to get that file.
                var state = continuationTask.AsyncState as RequestRemoteFileState;
                if (state != null) {
                    state.LocalLocation = null;
                }

                continuationTask.Continue(() => Updated());

                // the task can run, 
                if (continuationTask.Status == TaskStatus.Created) {
                    continuationTask.Start();
                }
            }
            return FinishedSynchronously;
        }

        public Task RecognizeFile(string canonicalName, string localLocation, string remoteLocation) {

            if (string.IsNullOrEmpty(localLocation)) {
                Event<GetResponseInterface>.RaiseFirst().Error("recognize-file", "local-location", "parameter 'local-location' is required to recognize a file");
                return FinishedSynchronously;
            }

            if (CancellationRequested) {
                Event<GetResponseInterface>.RaiseFirst().OperationCanceled("recognize-file");
                return FinishedSynchronously;
            }


            var location = Event<GetCanonicalizedPath>.RaiseFirst(localLocation);
            if (location.StartsWith(@"\\")) {
                // a local unc path was passed. This isn't allowed--we need a file on a local volume that
                // the user has access to.
                Event<GetResponseInterface>.RaiseFirst().Error("recognize-file", "local-location",
                    "local-location '{0}' appears to be a file on a remote server('{1}') . Recognized files must be local".format(localLocation, location));
                return FinishedSynchronously;
            }

            if (!File.Exists(location)) {
                Event<GetResponseInterface>.RaiseFirst().FileNotFound(location);
                return FinishedSynchronously;
            }

            // if there is a continuation task for the canonical name that goes along with this, 
            // we should continue with that task, and get the heck out of here.
            // 
            if (!canonicalName.IsNullOrEmpty()) {
                var continuationTask = SessionCache<Task<Recognizer.RecognitionInfo>>.Value[canonicalName];
                SessionCache<Task<Recognizer.RecognitionInfo>>.Value.Clear(canonicalName);
                if (continuationTask != null) {
                    var state = continuationTask.AsyncState as RequestRemoteFileState;
                    if (state != null) {
                        state.LocalLocation = localLocation;
                    }

                    if (continuationTask.Status == TaskStatus.Created) {
                        continuationTask.Start();
                    }

                    return FinishedSynchronously;
                }
            }

            // otherwise, we'll call the recognizer 
            Recognizer.Recognize(location).ContinueWith(antecedent => {
                if (antecedent.IsFaulted) {
                    Event<GetResponseInterface>.RaiseFirst().FileNotRecognized(location, "Unexpected error recognizing file.");
                    return;
                }

                if (antecedent.Result.IsPackageFile) {
                    var package = Package.GetPackageFromFilename(location);
                    if (package != null) {
                        // mark it download 100%
                        package.PackageSessionData.DownloadProgress = 100;

                        SessionPackageFeed.Instance.Add(package);

                        PackageInformation(Event<GetResponseInterface>.RaiseFirst(),package);
                        Event<GetResponseInterface>.RaiseFirst().Recognized(localLocation);
                    }
                    return;
                }

                if (antecedent.Result.IsPackageFeed) {
                    Event<GetResponseInterface>.RaiseFirst().FeedAdded(location);
                    Event<GetResponseInterface>.RaiseFirst().Recognized(location);
                }

                // if this isn't a package file, then there is something odd going on here.
                // we don't accept non-package files willy-nilly. 
                Event<GetResponseInterface>.RaiseFirst().FileNotRecognized(location, "File isn't a package, and doesn't appear to have been requested. ");
            }, TaskContinuationOptions.AttachedToParent);
            return FinishedSynchronously;
        }

        private void PackageInformation(IPackageManagerResponse response,  Package package,IEnumerable<string> supercedents = null  ) {
            if( package != null ) {
                supercedents = supercedents ?? Enumerable.Empty<string>();
                response.PackageInformation(package.CanonicalName, package.InternalPackageData.LocalLocation, package.Name, package.Version, package.Architecture, package.PublicKeyToken, package.IsInstalled, package.IsBlocked,
                    package.IsRequired, package.IsClientRequested, package.IsActive, package.PackageSessionData.IsDependency, package.InternalPackageData.PolicyMinimumVersion, package.InternalPackageData.PolicyMaximumVersion,
                    package.InternalPackageData.RemoteLocations, package.InternalPackageData.Dependencies.Select(each => each.CanonicalName), supercedents);
            }
        }

        public Task SetFeedFlags(string location, string activePassiveIgnored) {
            if (CancellationRequested) {
                Event<GetResponseInterface>.RaiseFirst().OperationCanceled("set-feed");
                return FinishedSynchronously;
            }

            if (Event<CheckForPermission>.RaiseFirst(PermissionPolicy.EditSystemFeeds)) {
                activePassiveIgnored = activePassiveIgnored ?? string.Empty;

                switch (activePassiveIgnored.ToLower()) {
                    case "active":
                        PackageManagerSettings.PerPackageSettings[location, "state"].StringValue = "active";
                        break;
                    case "passive":
                        PackageManagerSettings.PerPackageSettings[location, "state"].StringValue = "passive";
                        break;
                    case "ignored":
                        PackageManagerSettings.PerPackageSettings[location, "state"].StringValue = "ignored";
                        break;
                }
            }
            return FinishedSynchronously;
        }

        public Task SuppressFeed(string location) {
            if (CancellationRequested) {
                Event<GetResponseInterface>.RaiseFirst().OperationCanceled("suppress-feed");
                return FinishedSynchronously;
            }

            var suppressedFeeds = SessionCache<List<string>>.Value["suppressed-feeds"] ?? new List<string>();

            lock (suppressedFeeds) {
                if (!suppressedFeeds.Contains(location)) {
                    suppressedFeeds.Add(location);
                    SessionCache<List<string>>.Value["suppressed-feeds"] = suppressedFeeds;
                }
            }
            Event<GetResponseInterface>.RaiseFirst().FeedSuppressed(location);
            return FinishedSynchronously;
        }

        internal void Updated() {
            foreach (var mre in _manualResetEvents) {
                mre.Set();
            }
        }

        private Package GetSinglePackage(string canonicalName, string messageName, bool suppressErrors = false) {
            // name != null?
            if( string.IsNullOrEmpty(canonicalName)) {
                if (messageName != null && !suppressErrors) {
                    Event<GetResponseInterface>.RaiseFirst().Error(messageName, "canonical-name",
                        "Canonical name '{0}' does not appear to be a valid canonical name".format(canonicalName));
                }
                return null;
            }

            // if canonical name is passed, override name,version,pkt,arch with the parsed canonicalname.
            var match = CanonicalNameParser.Match(canonicalName.ToLower());
            if( !match.Success ) {

                if (messageName != null && !suppressErrors) {
                    Event<GetResponseInterface>.RaiseFirst().Error(messageName, "canonical-name",
                        "Canonical name '{0}' does not appear to be a valid canonical name".format(canonicalName));
                }
                return null;
            }

            var pkg = SearchForPackages(match.Groups[1].Captures[0].Value, match.Groups[2].Captures[0].Value, match.Groups[3].Captures[0].Value,
                match.Groups[4].Captures[0].Value).ToArray();

            if( !pkg.Any()) {
                if (messageName != null && !suppressErrors) {
                    Event<GetResponseInterface>.RaiseFirst().UnknownPackage(canonicalName);
                }
                return null;
            }

            if( pkg.Count() > 1 ) {
                if (messageName != null && !suppressErrors) {
                    Event<GetResponseInterface>.RaiseFirst().Error(messageName, "canonical-name",
                        "Canonical name '{0}' matches more than one package.".format(canonicalName));
                }
                return null; 
            }

            return pkg.FirstOrDefault();
        }

        internal List<string> BlockedScanLocations {
            get { return SessionCache<List<string>>.Value["suppressed-feeds"] ?? new List<string>(); }
        }

        internal IEnumerable<PackageFeed> Feeds { get {
            try {
                // ensure that the system feeds actually get loaded.
                Task.WaitAll(LoadSystemFeeds().ToArray());

                var canFilterSession = Event<CheckForPermission>.RaiseFirst(PermissionPolicy.EditSessionFeeds);
                var canFilterSystem = Event<CheckForPermission>.RaiseFirst(PermissionPolicy.EditSystemFeeds);
                var feedFilters = BlockedScanLocations;

                return new PackageFeed[] {
                    SessionPackageFeed.Instance, InstalledPackageFeed.Instance
                }.Union(from feed in Cache<PackageFeed>.Value.Values where !canFilterSystem || !feed.IsLocationMatch(feedFilters) select feed).Union(
                    from feed in SessionCache<PackageFeed>.Value.SessionValues where !canFilterSession || !feed.IsLocationMatch(feedFilters) select feed);
            } catch( Exception e ) {
                Logger.Error(e);
                throw;
            }
        }}


#region package scanning

        /// <summary>
        /// Gets packages from all visible feeds based on criteria
        /// </summary>
        /// <param name="name"></param>
        /// <param name="version"></param>
        /// <param name="arch"></param>
        /// <param name="publicKeyToken"></param>
        /// <param name="location"> </param>
        /// <returns></returns>
        internal IEnumerable<Package> SearchForPackages(string name, string version, string arch, string publicKeyToken, string location = null) {
            try {
                var feeds = string.IsNullOrEmpty(location) ? Feeds : Feeds.Where(each => each.IsLocationMatch(location));
                if (location != null || ((!string.IsNullOrEmpty(version)) && version.IndexOf("*") == -1)) {
                    // asking a specific feed, or they are not asking for more than one version of a given package.
                    // or asking for a specific version.
                    return feeds.SelectMany(each => each.FindPackages(name, version, arch, publicKeyToken)).Distinct().ToArray();
                }

                var feedLocations = Feeds.Select(each => each.Location);
                var packages = feeds.SelectMany(each => each.FindPackages(name, version, arch, publicKeyToken)).Distinct().ToArray();

                var otherFeeds = packages.SelectMany(each => each.InternalPackageData.FeedLocations).Distinct().Where(each => !feedLocations.Contains(each));
                // given a list of other feeds that we're not using, we can search each of those feeds for newer versions of the packages that we already have.
                var tf = TransientFeeds(otherFeeds);
                return packages.Union(packages.SelectMany(p => tf.SelectMany(each => each.FindPackages(p.Name, version, p.Architecture, p.PublicKeyToken)))).Distinct().ToArray();
            }
            catch (InvalidOperationException) {
                // this can happen if the collection changes during the operation (and can actually happen in the middle of .ToArray() 
                // since, locking the hell out of the collections isn't worth the effort, we'll just try again on this type of exception
                // and pray the collection won't keep changing :)
                return SearchForPackages(name, version, arch, publicKeyToken, location);
            }
        }

        /// <summary>
        /// This returns an collection of feed objects when given a list of feed locations.
        /// The items are cached in the session, so if mutliple calls ask for repeat items, it's not creating new objects all the time.
        /// </summary>
        /// <param name="locations"> List of feed locations </param>
        /// <returns></returns>
        internal IEnumerable<PackageFeed> TransientFeeds( IEnumerable<string> locations ) {
            var locs = locations.ToArray();
            var tf = SessionCache<List<PackageFeed>>.Value["TransientFeeds"] ?? (SessionCache<List<PackageFeed>>.Value["TransientFeeds"] = new List<PackageFeed>());
            var existingLocations = tf.Select(each => each.Location);
            var newLocations = locs.Where(each => !existingLocations.Contains(each));
            var tasks = newLocations.Select(PackageFeed.GetPackageFeedFromLocation).ToArray();
            var newFeeds = tasks.Where(each => each.Result != null).Select(each => each.Result);
            tf.AddRange(newFeeds);
            return tf.Where(each => locs.Contains(each.Location));
        }

        /// <summary>
        /// Gets just installed packages based on criteria
        /// </summary>
        /// <param name="name"></param>
        /// <param name="version"></param>
        /// <param name="arch"></param>
        /// <param name="publicKeyToken"></param>
        /// <returns></returns>
        internal IEnumerable<Package> SearchForInstalledPackages(string name, string version, string arch, string publicKeyToken) {
            return InstalledPackageFeed.Instance.FindPackages(name, version, arch, publicKeyToken);
        }

        internal IEnumerable<Package> InstalledPackages {
            get { return InstalledPackageFeed.Instance.FindPackages(null, null, null, null); }
        }

        internal IEnumerable<Package> AllPackages {
            get { return SearchForPackages(null, null, null, null); }
        }

        #endregion

        /// <summary>
        /// This generates a list of files that need to be installed to sastisy a given package.
        /// </summary>
        /// <param name="package"></param>
        /// <param name="hypothetical"> </param>
        /// <returns></returns>
        private IEnumerable<Package> GenerateInstallGraph(Package package, bool hypothetical = false) {
            if (package.IsInstalled) {
                if (!package.PackageRequestData.NotifiedClientThisSupercedes) {
                    Event<GetResponseInterface>.RaiseFirst().PackageSatisfiedBy(package.CanonicalName, package.CanonicalName);
                    package.PackageRequestData.NotifiedClientThisSupercedes = true;
                }

                yield break;
            }

            var packageData = package.PackageSessionData;

            if (!package.PackageSessionData.IsPotentiallyInstallable) {
                if (hypothetical) {
                    yield break;
                } 
                
                // otherwise
                throw new OperationCompletedBeforeResultException();
            }

            if (!packageData.DoNotSupercede) {
                var installedSupercedents = SearchForInstalledPackages(package.Name, null, package.Architecture.ToString(), package.PublicKeyToken);

                if( package.PackageSessionData.IsClientSpecified || hypothetical )  {
                    // this means that we're talking about a requested package
                    // and not a dependent package and we can liberally construe supercedent 
                    // as anything with a highger version number
                    installedSupercedents =  (from p in installedSupercedents where p.Version > package.Version select p).OrderByDescending(p => p.Version).ToArray();

                } else {
                    // otherwise, we're installing a dependency, and we need something compatable.
                    installedSupercedents =  (from p in installedSupercedents 
                                where p.InternalPackageData.PolicyMinimumVersion <= package.Version &&
                                      p.InternalPackageData.PolicyMaximumVersion >= package.Version select p).OrderByDescending(p => p.Version).ToArray();
                }
                var installedSupercedent = installedSupercedents.FirstOrDefault();
                if (installedSupercedent != null ) {
                    if (!installedSupercedent.PackageRequestData.NotifiedClientThisSupercedes) {
                        Event<GetResponseInterface>.RaiseFirst().PackageSatisfiedBy(package.CanonicalName, installedSupercedent.CanonicalName);
                        installedSupercedent.PackageRequestData.NotifiedClientThisSupercedes = true;
                    }
                    yield break; // a supercedent package is already installed.
                }

                // if told not to supercede, we won't even perform this check 
                packageData.Supercedent = null;
                
                var supercedents = SearchForPackages(package.Name, null, package.Architecture.ToString(), package.PublicKeyToken).ToArray();

                if( package.PackageSessionData.IsClientSpecified || hypothetical )  {
                    // this means that we're talking about a requested package
                    // and not a dependent package and we can liberally construe supercedent 
                    // as anything with a highger version number
                    supercedents =  (from p in supercedents where p.Version > package.Version select p).OrderByDescending(p => p.Version).ToArray();

                } else {
                    // otherwise, we're installing a dependency, and we need something compatable.
                    supercedents =  (from p in supercedents 
                                where p.InternalPackageData.PolicyMinimumVersion <= package.Version &&
                                      p.InternalPackageData.PolicyMaximumVersion >= package.Version select p).OrderByDescending(p => p.Version).ToArray();
                }

                if (supercedents.Any()) {
                    if (packageData.AllowedToSupercede) {
                        foreach (var supercedent in supercedents) {
                            IEnumerable<Package> children;
                            try {
                                children = GenerateInstallGraph(supercedent, true);
                            }
                            catch {
                                // can't be satisfied with that supercedent.
                                // we can quietly move along here.
                                continue;
                            }

                            // we should tell the client that we're making a substitution.
                            if (!supercedent.PackageRequestData.NotifiedClientThisSupercedes) {
                                Event<GetResponseInterface>.RaiseFirst().PackageSatisfiedBy(package.CanonicalName, supercedent.CanonicalName);
                                supercedent.PackageRequestData.NotifiedClientThisSupercedes = true;
                            }

                            if( supercedent.Name == package.Name ) {
                                supercedent.PackageSessionData.IsClientSpecified = package.PackageSessionData.IsClientSpecified;   
                            }
                            
                            // since we got to this spot, we can assume that we can 
                            // supercede this package with the results of the successful
                            // GIG call.
                            foreach (var child in children) { 
                                yield return child;
                            }

                            // if we have a supercedent, then this package's dependents are moot.)
                            yield break;
                        }
                    }
                    else {
                        // the user hasn't specifically asked us to supercede, yet we know of 
                        // potential supercedents. Let's force the user to make a decision.
                        // throw new PackageHasPotentialUpgradesException(packageToSatisfy, supercedents);
                        Event<GetResponseInterface>.RaiseFirst().PackageHasPotentialUpgrades(package.CanonicalName, supercedents.Select(each => each.CanonicalName));
                        throw new OperationCompletedBeforeResultException();
                    }
                }
            }

            if (packageData.CouldNotDownload) {
                if (!hypothetical) {
                    Event<GetResponseInterface>.RaiseFirst().UnableToDownloadPackage(package.CanonicalName);
                }
                throw new OperationCompletedBeforeResultException();
            }

            if (packageData.PackageFailedInstall) {
                if (!hypothetical) {
                    Event<GetResponseInterface>.RaiseFirst().UnableToInstallPackage(package.CanonicalName);
                }
                throw new OperationCompletedBeforeResultException();
            }

            var childrenFailed = false;
            foreach( var d in package.InternalPackageData.Dependencies ) {
                IEnumerable<Package> children;
                try {
                    children = GenerateInstallGraph(d);
                }
                catch {
                    Logger.Message("Generating install graph for child dependency failed [{0}]",d.CanonicalName);
                    childrenFailed = true;
                    continue;
                }

                if (!childrenFailed) {
                    foreach (var child in children)
                        yield return child;
                }
            }

            if(childrenFailed) {
                throw new OperationCompletedBeforeResultException();
            }
            
            yield return package;
        }
 
        private void UpdateIsRequestedFlags() {
            lock (this) {
                var installedPackages = InstalledPackages.ToArray();

                foreach (var p in installedPackages) {
                    p.PackageSessionData.IsDependency = false;
                }

                foreach( var package in installedPackages.Where(each=>each.IsRequired)) {
                    package.UpdateDependencyFlags();
                }
            }
        }


        public Task GetPolicy(string policyName) {
            var policies = PermissionPolicy.AllPolicies.Where(each => each.Name.IsWildcardMatch(policyName)).ToArray();

            foreach (var policy in policies) {
                Event<GetResponseInterface>.RaiseFirst().PolicyInformation(policy.Name, policy.Description, policy.Accounts);
            }
            if (policies.IsNullOrEmpty()) {
                Event<GetResponseInterface>.RaiseFirst().Error("get-policy", "name", "policy '{0}' not found".format(policyName));
            }
            return FinishedSynchronously;
        }

        public Task AddToPolicy(string policyName, string account) {
            if (Event<CheckForPermission>.RaiseFirst(PermissionPolicy.ModifyPolicy)) {
                PermissionPolicy.AllPolicies.FirstOrDefault(each => each.Name.Equals(policyName, StringComparison.CurrentCultureIgnoreCase)).With(policy => {
                    try {
                        policy.Add(account);
                    } catch {
                        Event<GetResponseInterface>.RaiseFirst().Error("remove-from-policy", "account", "policy '{0}' could not remove account '{1}'".format(policyName, account));
                    }
                }, () => Event<GetResponseInterface>.RaiseFirst().Error("remove-from-policy", "name", "policy '{0}' not found".format(policyName)));
            }
            return FinishedSynchronously;
        }

        public Task RemoveFromPolicy( string policyName, string account) {
            if (Event<CheckForPermission>.RaiseFirst(PermissionPolicy.ModifyPolicy)) {
                PermissionPolicy.AllPolicies.FirstOrDefault(each => each.Name.Equals(policyName, StringComparison.CurrentCultureIgnoreCase)).With(policy => {
                    try {
                        policy.Remove(account);
                    } catch {
                        Event<GetResponseInterface>.RaiseFirst().Error("remove-from-policy", "account", "policy '{0}' could not remove account '{1}'".format(policyName, account));
                    }
                }, () => Event<GetResponseInterface>.RaiseFirst().Error("remove-from-policy", "name", "policy '{0}' not found".format(policyName)));
            }
            return FinishedSynchronously;
        }

        public Task CreateSymlink(string existingLocation, string newLink, LinkType linkType) {
            if (Event<CheckForPermission>.RaiseFirst(PermissionPolicy.Symlink)) {
                if (string.IsNullOrEmpty(existingLocation)) {
                    Event<GetResponseInterface>.RaiseFirst().Error("symlink", "existing-location", "location is null/empty. ");
                    return FinishedSynchronously;
                }

                if (string.IsNullOrEmpty(newLink)) {
                    Event<GetResponseInterface>.RaiseFirst().Error("symlink", "new-link", "new-link is null/empty.");
                    return FinishedSynchronously;
                }

                try {
                    if (existingLocation.FileIsLocalAndExists()) {
                        // source is a file
                        switch (linkType) {
                            case LinkType.Symlink:
                                Symlink.MakeFileLink(newLink, existingLocation);
                                break;

                            case LinkType.Hardlink:
                                Kernel32.CreateHardLink(newLink, existingLocation, IntPtr.Zero);
                                break;

                            case LinkType.Shortcut:
                                ShellLink.CreateShortcut(newLink, existingLocation);
                                break;
                        }
                    } else if (existingLocation.DirectoryExistsAndIsAccessible()) {
                        // source is a folder
                        switch (linkType) {
                            case LinkType.Symlink:
                                Symlink.MakeDirectoryLink(newLink, existingLocation);
                                break;

                            case LinkType.Hardlink:
                                Kernel32.CreateHardLink(newLink, existingLocation, IntPtr.Zero);
                                break;

                            case LinkType.Shortcut:
                                ShellLink.CreateShortcut(newLink, existingLocation);
                                break;
                        }
                    } else {
                        Event<GetResponseInterface>.RaiseFirst().Error("symlink", "existing-location", "can not make symlink for location '{0}'".format(existingLocation));
                    }
                } catch (Exception exception) {
                    Event<GetResponseInterface>.RaiseFirst().Error("symlink", "", "Failed to create symlink -- error: {0}".format(exception.Message));
                }
            }
            return FinishedSynchronously;
        }

        public Task SetFeedStale(string feedLocation) {
            PackageFeed.GetPackageFeedFromLocation(feedLocation).Continue(feed => {
                feed.Stale = true;
            });
            return FinishedSynchronously;
        }

        public Task StopService() {
            if (Event<CheckForPermission>.RaiseFirst(PermissionPolicy.StopService)) {
                EngineServiceManager.TryToStopService();
            }
            return FinishedSynchronously;
        }

        public Task SetLogging(bool? messages, bool? warnings, bool? errors) {
            if (messages.HasValue) {
                SessionCache<string>.Value["LogMessages"] = messages.ToString();
            }
                
            if (errors.HasValue) {
                SessionCache<string>.Value["LogErrors"] = errors.ToString();
            }
                
            if (warnings.HasValue) {
                SessionCache<string>.Value["LogWarnings"] = warnings.ToString();
            }
            Event<GetResponseInterface>.RaiseFirst().LoggingSettings(Logger.Messages, Logger.Warnings, Logger.Errors);
            return FinishedSynchronously;
        }
        public Task ScheduleTask(string taskName, string executable, string commandline, int hour, int minutes, DayOfWeek? dayOfWeek, int intervalInMinutes) {
            return FinishedSynchronously;
        }
        public Task RemoveScheduledTask(string taskName) {
            return FinishedSynchronously;
        }
        public Task GetScheduledTasks(string taskName) {
            return FinishedSynchronously;
        }
        public Task AddTrustedPublisher() {
            return FinishedSynchronously;
        }
        public Task RemoveTrustedPublisher() {
            return FinishedSynchronously;
        }
        public Task GetTrustedPublishers() {

            return FinishedSynchronously;
        }
        public Task GetTelemetry() {
            return FinishedSynchronously;
        }
        public Task SetTelemetry(bool optin) {
            return FinishedSynchronously;
        }
    }
}
