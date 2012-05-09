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

namespace CoApp.Packaging.Service {
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Common;
    using Feeds;
    using PackageFormatHandlers;
    using Toolkit.Collections;
    using Toolkit.Crypto;
    using Toolkit.Exceptions;
    using Toolkit.Extensions;
    using Toolkit.Logging;
    using Toolkit.Pipes;
    using Toolkit.Shell;
    using Toolkit.Tasks;
    using Toolkit.Win32;

    public class PackageManagerImpl : IPackageManager {
        private static Task FinishedSynchronously {
            get {
                return CoTask.AsResultTask<object>(null);
            }
        }

        public static PackageManagerImpl Instance = new PackageManagerImpl();

        private readonly List<ManualResetEvent> _manualResetEvents = new List<ManualResetEvent>();
        internal static IncomingCallDispatcher<PackageManagerImpl> Dispatcher = new IncomingCallDispatcher<PackageManagerImpl>(Instance);

        private bool CancellationRequested {
            get {
                return Event<IsCancellationRequested>.RaiseFirst();
            }
        }

        private PackageManagerImpl() {
            // always load the Installed Package Feed.
            PackageFeed.GetPackageFeedFromLocation(InstalledPackageFeed.CanonicalLocation);
        }

        /// <summary>
        ///   feeds that we should try to load as system feeds
        /// </summary>
        private IEnumerable<string> SystemFeedLocations {
            get {
                lock (typeof(PackageManagerImpl)) {
                    if (!PackageManagerSettings.CoAppSettings["#feedLocations"].HasValue) {

                        PackageManagerSettings.CoAppSettings["#feedLocations"].StringsValue = new[] {
                            "http://coapp.org/current",
                            "http://coapp.org/archive",
                            "http://coapp.org/unstable"
                        };

                        PackageManagerSettings.PerPackageSettings["http://coapp.org/archive", "state"].StringValue = "passive";
                        PackageManagerSettings.PerPackageSettings["http://coapp.org/unstable", "state"].StringValue = "ignored";
                    }
                }

                return PackageManagerSettings.CoAppSettings["#feedLocations"].StringsValue;
            }
        }

        private IEnumerable<string> SessionFeedLocations {
            get {
                return SessionCache<IEnumerable<string>>.Value["session-feeds"] ?? Enumerable.Empty<string>();
            }
        }

        private void AddSessionFeed(string feedLocation) {
            lock (this) {
                if (!feedLocation.IsWebUri()) {
                    feedLocation = feedLocation.CanonicalizePathWithWildcards();
                }

                var sessionFeeds = SessionFeedLocations.Union(feedLocation.SingleItemAsEnumerable()).Distinct();
                SessionCache<IEnumerable<string>>.Value["session-feeds"] = sessionFeeds.ToArray();
            }
        }

        private void AddSystemFeed(string feedLocation) {
            lock (this) {
                if( !feedLocation.IsWebUri()) {
                    feedLocation = feedLocation.CanonicalizePathWithWildcards();
                }
                var systemFeeds = SystemFeedLocations.Union(feedLocation.SingleItemAsEnumerable()).Distinct();
                PackageManagerSettings.CoAppSettings["#feedLocations"].StringsValue = systemFeeds.ToArray();
            }
        }

        private void RemoveSessionFeed(string feedLocation) {
            lock (this) {
                if (!feedLocation.IsWebUri()) {
                    feedLocation = feedLocation.CanonicalizePathWithWildcards();
                }

                var sessionFeeds = from emove in SessionFeedLocations where !emove.Equals(feedLocation, StringComparison.CurrentCultureIgnoreCase) select emove;
                SessionCache<IEnumerable<string>>.Value["session-feeds"] = sessionFeeds.ToArray();

                // remove it from the cached feeds
                SessionCache<PackageFeed>.Value.Clear(feedLocation);
            }
        }

        private void RemoveSystemFeed(string feedLocation) {
            lock (this) {
                if (!feedLocation.IsWebUri()) {
                    feedLocation = feedLocation.CanonicalizePathWithWildcards();
                }

                var systemFeeds = from feed in SystemFeedLocations where !feed.Equals(feedLocation, StringComparison.CurrentCultureIgnoreCase) select feed;
                PackageManagerSettings.CoAppSettings["#feedLocations"].StringsValue = systemFeeds.ToArray();

                // remove it from the cached feeds
                Cache<PackageFeed>.Value.Clear(feedLocation);
            }
        }

        internal void EnsureSystemFeedsAreLoaded() {
            // do a cheap check first (so that this session never gets blocked unnecessarily).
            if (SessionCache<string>.Value["system-cache-loaded"].IsTrue()) {
                return;
            }

            lock (this) {
                if (SessionCache<string>.Value["system-cache-loaded"].IsTrue()) {
                    return;
                }

                Task.WaitAll(SystemFeedLocations.Select(each => PackageFeed.GetPackageFeedFromLocation(each).ContinueAlways(antecedent => {
                    if (antecedent.Result != null) {
                        Cache<PackageFeed>.Value[each] = antecedent.Result;
                    }
                    else {
                        Logger.Error("Feed {0} was unable to load.", each);
                    }
                })).ToArray());

                // mark this session as 'yeah, done that already'
                SessionCache<string>.Value["system-cache-loaded"] = "true";
            }
        }

        public Task FindPackages(CanonicalName canonicalName, bool? dependencies, bool? installed, bool? active, bool? required, bool? blocked, bool? latest,
            int? index, int? maxResults, string location, bool? forceScan, bool? updates, bool? upgrades, bool? trimable) {
            var response = Event<GetResponseInterface>.RaiseFirst();

            if (CancellationRequested) {
                response.OperationCanceled("find-package");
                return FinishedSynchronously;
            }

            canonicalName = canonicalName ?? CanonicalName.CoAppPackages;

            if (Event<CheckForPermission>.RaiseFirst(PermissionPolicy.EnumeratePackages)) {
                UpdateIsRequestedFlags();

                if (forceScan == true) {
                    foreach (var feed in Feeds) {
                        feed.Stale = true;
                    }
                }

                var results = SearchForPackages(canonicalName, location).ToArray();
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
                            let familyPackages = SearchForPackages(r.CanonicalName.OtherVersionFilter).Where(each => !each.IsInstalled).OrderByDescending(each => each.CanonicalName.Version)
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
                            let familyPackages = SearchForPackages(r.CanonicalName.OtherVersionFilter).Where(each => !each.IsInstalled).OrderByDescending(each => each.CanonicalName.Version)
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
                    var deps = results.SelectMany(each => each.Dependencies).Distinct();

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
                            response.OperationCanceled("find-packages");
                            return FinishedSynchronously;
                        }

                        var supercedents = (from p in SearchForPackages(package.CanonicalName.OtherVersionFilter)
                            where p.IsAnUpdateFor(package)
                            select p).OrderByDescending(p => p.CanonicalName.Version).ToArray();

                        PackageInformation(response, package, supercedents.Select(each => each.CanonicalName));
                    }
                } else {
                    response.NoPackagesFound();
                }
            }
            return FinishedSynchronously;
        }

        public Task GetPackageDetails(CanonicalName canonicalName) {
            var response = Event<GetResponseInterface>.RaiseFirst();

            if (CancellationRequested) {
                response.OperationCanceled("get-package-details");
                return FinishedSynchronously;
            }
            if (canonicalName.IsPartial) {
                response.Error("Invalid Canonical Name", "GetPackageDetails", "Canonical name '{0}' is not a complete canonical name".format(canonicalName));
            }
            var package = SearchForPackages(canonicalName).FirstOrDefault();

            if (package == null) {
                return FinishedSynchronously;
            }


            response.PackageDetails(package.CanonicalName, new XDictionary<string, string> {
                {"description", package.PackageDetails.Description},
                {"summary", package.PackageDetails.SummaryDescription},
                {"display-name", package.DisplayName},
                {"copyright", package.PackageDetails.CopyrightStatement},
                {"author-version", package.PackageDetails.AuthorVersion},
            },
               package.PackageDetails.IconLocations,
               package.PackageDetails.Licenses.ToXDictionary(each => each.Name, each => each.Text),
               package.Roles.ToXDictionary(each => each.Name, each => each.PackageRole.ToString()),
               package.PackageDetails.Tags,
               package.PackageDetails.Contributors.ToXDictionary(each => each.Name, each => each.Location.AbsoluteUri),
               package.PackageDetails.Contributors.ToXDictionary(each => each.Name, each => each.Email));
            return FinishedSynchronously;
        }

        public Task InstallPackage(CanonicalName canonicalName, bool? autoUpgrade, bool? force, bool? download, bool? pretend, bool? isUpdating, bool? isUpgrading) {
            var response = Event<GetResponseInterface>.RaiseFirst();

            if (CancellationRequested) {
                response.OperationCanceled("install-package");
                return FinishedSynchronously;
            }

            double[] overallProgress = {0.0};
            double[] eachTaskIsWorth = {0.0};
            int[] lastProgress = {0};
            Package currentPackageInstalling = null;
            var numberOfPackagesToInstall = 0;
            var numberOfPackagesToDownload = 0;

            CurrentTask.Events += new IndividualProgress(percentage => {
                overallProgress[0] += ((percentage - lastProgress[0])*eachTaskIsWorth[0])/100;
                lastProgress[0] = percentage;
                // ReSharper disable PossibleNullReferenceException ... this is what I really want. :[
                // ReSharper disable AccessToModifiedClosure
                response.InstallingPackageProgress(currentPackageInstalling.CanonicalName, percentage, (int)(overallProgress[0]*100));
                // ReSharper restore AccessToModifiedClosure
                // ReSharper restore PossibleNullReferenceException
            });

            using (var manualResetEvent = new ManualResetEvent(true)) {
                try {
                    lock (_manualResetEvents) {
                        _manualResetEvents.Add(manualResetEvent);
                    }

                    var packagesTriedToDownloadThisTask = new List<Package>();
                    if (canonicalName.IsPartial) {
                        response.Error("Invalid Canonical Name", "InstallPackage", "Canonical name '{0}' is not a complete canonical name".format(canonicalName));
                    }
                    var package = SearchForPackages(canonicalName).FirstOrDefault();

                    if (package == null) {
                        response.UnknownPackage(canonicalName);
                        return FinishedSynchronously;
                    }

                    if (package.IsBlocked) {
                        response.PackageBlocked(canonicalName);
                        return FinishedSynchronously;
                    }

                    var installedPackages = SearchForInstalledPackages(package.CanonicalName.OtherVersionFilter).ToArray();
                    var installedCompatibleVersions = installedPackages.Where(package.IsAnUpdateFor).ToArray();

                    // is the user authorized to install this?
                    var highestInstalledPackage = installedPackages.HighestPackages().FirstOrDefault();

                    if (highestInstalledPackage != null && highestInstalledPackage.CanonicalName.Version < package.CanonicalName.Version) {
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
                        response.PackageBlocked(canonicalName);
                    }

                    // if this is an upgrade, 
                    //      - check to see if this package has the do-not-upgrade flag.
                    if (isUpgrading == true && package.DoNotUpgrade) {
                        response.PackageBlocked(canonicalName);
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
                            response.OperationCanceled("install-package");
                            return FinishedSynchronously;
                        }

                        IEnumerable<Package> installGraph;
                        try {
                            installGraph = GenerateInstallGraph(package).ToArray();
                        } catch (OperationCompletedBeforeResultException) {
                            // we encountered an unresolvable condition in the install graph.
                            // messages should have already been sent.
                            response.FailedPackageInstall(canonicalName, package.LocalLocations.FirstOrDefault(),
                                "One or more dependencies are unable to be resolved.");
                            return FinishedSynchronously;
                        }

                        // seems like a good time to check if we're supposed to bail...
                        if (CancellationRequested) {
                            response.OperationCanceled("install-package");
                            return FinishedSynchronously;
                        }
                        
                        if (download == false && pretend == true) {
                            // we can just return a bunch of foundpackage messages, since we're not going to be 
                            // actually installing anything, nor trying to download anything.
                            foreach (var p in installGraph) {
                                PackageInformation(response, p);
                            }
                            return FinishedSynchronously;
                        }

                        // we've got an install graph.
                        // let's see if we've got all the files
                        var missingFiles = (from p in installGraph where !p.HasLocalLocation select p).ToArray();

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
                                response.RequireRemoteFile(p.CanonicalName,
                                    p.RemoteLocations, PackageManagerSettings.CoAppPackageCache, false);

                                p.PackageSessionData.HasRequestedDownload = true;
                            }
                        } else {
                            if (pretend == true) {
                                // we can just return a bunch of found-package messages, since we're not going to be 
                                // actually installing anything, and everything we needed is downloaded.
                                foreach (var p in installGraph) {
                                    PackageInformation(response, p);
                                }
                                return FinishedSynchronously;
                            }

                            var failed = false;
                            // no missing files? Check
                            // complete install graph? Check

                            foreach (var p in installGraph) {
                                currentPackageInstalling = p;
                                // seems like a good time to check if we're supposed to bail...
                                if (CancellationRequested) {
                                    response.OperationCanceled("install-package");
                                    return FinishedSynchronously;
                                }
                                var validLocation = currentPackageInstalling.PackageSessionData.LocalValidatedLocation;

                                try {
                                    if (!currentPackageInstalling.IsInstalled) {
                                        if (string.IsNullOrEmpty(validLocation)) {
                                            // can't find a valid location
                                            response.FailedPackageInstall(currentPackageInstalling.CanonicalName, currentPackageInstalling.LocalLocations.FirstOrDefault(), "Can not find local valid package");
                                            currentPackageInstalling.PackageSessionData.PackageFailedInstall = true;
                                        } else {
                                            lastProgress[0] = 0;
                                            // GS01: We should put a softer lock here to keep the client aware that packages 
                                            // are being installed on other threads...
                                            lock (typeof (MSIBase)) {
                                                if (Engine.DoesTheServiceNeedARestart) {
                                                    // something has changed where we need restart the service before we can continue.
                                                    // and the one place we don't wanna be when we issue a shutdown in in Install :) ...
                                                    Engine.RestartService();
                                                    response.OperationCanceled("install-package");
                                                    return FinishedSynchronously;
                                                }

                                                // install progress is now handled by the delegate at the beginning of this function.
                                                currentPackageInstalling.Install();
                                            }
                                            overallProgress[0] += ((100 - lastProgress[0])*eachTaskIsWorth[0])/100;
                                            response.InstallingPackageProgress(currentPackageInstalling.CanonicalName, 100, (int)(overallProgress[0]*100));
                                            response.InstalledPackage(currentPackageInstalling.CanonicalName);
                                            Signals.InstalledPackage(currentPackageInstalling.CanonicalName);
                                        }
                                    }
                                } catch (Exception e) /* (PackageInstallFailedException pife)  */ {
                                    Logger.Error("FAILED INSTALL");
                                    Logger.Error(e);

                                    response.FailedPackageInstall(currentPackageInstalling.CanonicalName, validLocation, "Package failed to install.");
                                    currentPackageInstalling.PackageSessionData.PackageFailedInstall = true;

                                    if (!currentPackageInstalling.PackageSessionData.AllowedToSupercede) {
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
                                    foreach (var eachPkg in installedCompatibleVersions) {
                                        eachPkg.IsClientRequested = false;
                                    }
                                }
                                if (isUpgrading == true) {
                                    // if this is marked as an update
                                    // remove REQUESTED flag from all older compatible version 
                                    foreach (var eachPkg in installedPackages) {
                                        eachPkg.IsClientRequested = false;
                                    }
                                }

                                // W00T ... We did it!
                                // check for restart required...
                                if (Engine.DoesTheServiceNeedARestart) {
                                    // something has changed where we need restart the service before we can continue.
                                    // and the one place we don't wanna be when we issue a shutdown in in Install :) ...
                                    response.Restarting();
                                    Engine.RestartService();
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
                                response.OperationCanceled("install-package");
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

        public Task DownloadProgress(string requestReference, int? downloadProgress) {
            try {
                // it takes a non-trivial amount of time to lookup a package by its name.
                // so, we're going to cache the package in the session.
                // of course if there isn't one, (because we're downloading soemthing we don't know what it's actualy canonical name is)
                // we don't want to try looking up each time again, since that's the worst-case-scenario, we have to
                // cache the fact that we have cached nothing.
                // /facepalm.

                Package package;

                var cachedPackageName = SessionCache<string>.Value["cached-the-lookup" + requestReference];

                if (cachedPackageName == null) {
                    SessionCache<string>.Value["cached-the-lookup" + requestReference] = "yes";

                    package = SearchForPackages(requestReference).FirstOrDefault();

                    if (package != null) {
                        SessionCache<Package>.Value[requestReference] = package;
                    }
                } else {
                    package = SessionCache<Package>.Value[requestReference];
                }

                if (package != null) {
                    package.PackageSessionData.DownloadProgress = Math.Max(package.PackageSessionData.DownloadProgress, downloadProgress.GetValueOrDefault());
                }
            } catch {
                // suppress any exceptions... we just don't care!
            }
            SessionCache<string>.Value["busy" + requestReference] = null;
            return FinishedSynchronously;
        }

        public Task ListFeeds(int? index, int? maxResults) {
            var response = Event<GetResponseInterface>.RaiseFirst();

            if (CancellationRequested) {
                response.OperationCanceled("list-feeds");
                return FinishedSynchronously;
            }

            var canFilterSession = Event<QueryPermission>.RaiseFirst(PermissionPolicy.EditSessionFeeds);
            var canFilterSystem = Event<QueryPermission>.RaiseFirst(PermissionPolicy.EditSystemFeeds);

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
                        state = "Active";
                    }
                    response.FeedDetails(f.feed, f.LastScanned, f.session, f.suppressed, f.validated, state);
                }
            } else {
                response.NoFeedsFound();
            }
            return FinishedSynchronously;
        }

        public Task RemoveFeed(string location, bool? session) {
            var response = Event<GetResponseInterface>.RaiseFirst();
            if (CancellationRequested) {
                response.OperationCanceled("remove-feed");
                return FinishedSynchronously;
            }

            // Note: This may need better lookup/matching for the location
            // as location can be a fuzzy match.

            if (session ?? false) {
                // session feed specfied
                if (Event<CheckForPermission>.RaiseFirst(PermissionPolicy.EditSessionFeeds)) {
                    RemoveSessionFeed(location);
                    response.FeedRemoved(location);
                }
            } else {
                // system feed specified
                if (Event<CheckForPermission>.RaiseFirst(PermissionPolicy.EditSystemFeeds)) {
                    RemoveSystemFeed(location);
                    response.FeedRemoved(location);
                }
            }
            return FinishedSynchronously;
        }

        public Task AddFeed(string location, bool? session) {
            var response = Event<GetResponseInterface>.RaiseFirst();

            if (CancellationRequested) {
                response.OperationCanceled("add-feed");
                return FinishedSynchronously;
            }

            if (session ?? false) {
                // new feed is a session feed
                // session feed specfied
                if (Event<CheckForPermission>.RaiseFirst(PermissionPolicy.EditSessionFeeds)) {
                    // check if it is already a system feed
                    if (SystemFeedLocations.Contains(location)) {
                        response.Warning("add-feed", "location", "location '{0}' is already a system feed".format(location));
                        return FinishedSynchronously;
                    }

                    if (SessionFeedLocations.Contains(location)) {
                        response.Warning("add-feed", "location", "location '{0}' is already a session feed".format(location));
                        return FinishedSynchronously;
                    }

                    // add feed to the session feeds.
                    PackageFeed.GetPackageFeedFromLocation(location).ContinueWith(antecedent => {
                        var foundFeed = antecedent.Result;
                        if (foundFeed != null) {
                            AddSessionFeed(location);
                            response.FeedAdded(location);

                            if (foundFeed != SessionPackageFeed.Instance || foundFeed != InstalledPackageFeed.Instance) {
                                SessionCache<PackageFeed>.Value[location] = foundFeed;
                            }
                        } else {
                            response.Error("add-feed", "location",
                                "failed to recognize location '{0}' as a valid package feed".format(location));
                            Logger.Error("Feed {0} was unable to load.", location);
                        }
                    }, TaskContinuationOptions.AttachedToParent);
                }
            } else {
                // new feed is a system feed
                if (Event<CheckForPermission>.RaiseFirst(PermissionPolicy.EditSystemFeeds)) {
                    if (SystemFeedLocations.Contains(location)) {
                        response.Warning("add-feed", "location", "location '{0}' is already a system feed".format(location));
                        return FinishedSynchronously;
                    }

                    // add feed to the system feeds.
                    PackageFeed.GetPackageFeedFromLocation(location).ContinueWith(antecedent => {
                        var foundFeed = antecedent.Result;
                        if (foundFeed != null) {
                            AddSystemFeed(location);
                            response.FeedAdded(location);

                            if (foundFeed != SessionPackageFeed.Instance || foundFeed != InstalledPackageFeed.Instance) {
                                Cache<PackageFeed   >.Value[location] = foundFeed;
                            }
                        } else {
                            response.Error("add-feed", "location", "failed to recognize location '{0}' as a valid package feed".format(location));
                            Logger.Error("Feed {0} was unable to load.", location);
                        }
                    }, TaskContinuationOptions.AttachedToParent);
                }
            }
            return FinishedSynchronously;
        }

        public Task VerifyFileSignature(string filename) {
            var response = Event<GetResponseInterface>.RaiseFirst();
            
            if (CancellationRequested) {
                response.OperationCanceled("verify-signature");
                return FinishedSynchronously;
            }

            if (string.IsNullOrEmpty(filename)) {
                response.Error("verify-signature", "filename", "parameter 'filename' is required to verify a file");
                return FinishedSynchronously;
            }

            var location = Event<GetCanonicalizedPath>.RaiseFirst(filename);

            if (!File.Exists(location)) {
                response.FileNotFound(location);
                return FinishedSynchronously;
            }

            var r = Verifier.HasValidSignature(location);
            response.SignatureValidation(location, r, r ? Verifier.GetPublisherInformation(location)["PublisherName"] : null);
            return FinishedSynchronously;
        }

        public Task SetPackage(CanonicalName canonicalName, bool? active, bool? required, bool? blocked, bool? doNotUpdate, bool? doNotUpgrade) {
            var response = Event<GetResponseInterface>.RaiseFirst();

            if (CancellationRequested) {
                response.OperationCanceled("set-package");
                return FinishedSynchronously;
            }

            if (canonicalName.IsPartial) {
                response.Error("Invalid Canonical Name", "SetPackage", "Canonical name '{0}' is not a complete canonical name".format(canonicalName));
            }
            var package = SearchForPackages(canonicalName).FirstOrDefault();

            if (package == null) {
                response.UnknownPackage(canonicalName);
                return FinishedSynchronously;
            }

            if (!package.IsInstalled) {
                response.Error("set-package", "canonical-name", "package '{0}' is not installed.".format(canonicalName));
                return FinishedSynchronously;
            }

            // seems like a good time to check if we're supposed to bail...
            if (CancellationRequested) {
                response.OperationCanceled("set-package");
                return FinishedSynchronously;
            }

            if (true == active) {
                if (Event<CheckForPermission>.RaiseFirst(PermissionPolicy.ChangeActivePackage)) {
                    package.SetPackageCurrent();
                }
            }

            if (false == active) {
                if (Event<CheckForPermission>.RaiseFirst(PermissionPolicy.ChangeActivePackage)) {
                    var pqg = SearchForInstalledPackages(package.CanonicalName.OtherVersionFilter).HighestPackages().FirstOrDefault();
                    if (pqg != null) {
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
            PackageInformation(response, package);
            return FinishedSynchronously;
        }

        public Task RemovePackage(CanonicalName canonicalName, bool? force) {
            var response = Event<GetResponseInterface>.RaiseFirst();
            
            if (CancellationRequested) {
                response.OperationCanceled("remove-package");
                return FinishedSynchronously;
            }

            if (Event<CheckForPermission>.RaiseFirst(PermissionPolicy.RemovePackage)) {
                if (canonicalName.IsPartial) {
                    response.Error("Invalid Canonical Name", "InstallPackage", "Canonical name '{0}' is not a complete canonical name".format(canonicalName));
                }
                var package = SearchForPackages(canonicalName).FirstOrDefault();

                if (package == null) {
                    response.UnknownPackage(canonicalName);
                    return FinishedSynchronously;
                }

                if (package.CanonicalName.Matches(CanonicalName.CoAppItself) && package.IsActive) {
                    response.Error("remove-package", "canonical-name", "Active CoApp Engine may not be removed");
                    return FinishedSynchronously;
                }

                if (!package.IsInstalled) {
                    response.Error("remove-package", "canonical-name", "package '{0}' is not installed.".format(canonicalName));
                    return FinishedSynchronously;
                }

                if (package.IsBlocked) {
                    response.PackageBlocked(canonicalName);
                    return FinishedSynchronously;
                }
                if (true != force) {
                    UpdateIsRequestedFlags();
                    if (package.PackageSessionData.IsDependency) {
                        response.FailedPackageRemoval(canonicalName,
                            "Package '{0}' is a required dependency of another package.".format(canonicalName));
                        return FinishedSynchronously;
                    }
                }
                // seems like a good time to check if we're supposed to bail...
                if (CancellationRequested) {
                    response.OperationCanceled("remove-package");
                    return FinishedSynchronously;
                }

                try {
                    // send back the progress to the client.
                    CurrentTask.Events += new IndividualProgress(percentage => response.RemovingPackageProgress(package.CanonicalName, percentage));

                    package.Remove();

                    response.RemovingPackageProgress(canonicalName, 100);
                    response.RemovedPackage(canonicalName);

                    Signals.RemovedPackage(canonicalName);
                } catch (OperationCompletedBeforeResultException e) {
                    response.FailedPackageRemoval(canonicalName, e.Message);
                    return FinishedSynchronously;
                }
            }
            return FinishedSynchronously;
        }

        public Task UnableToAcquire(string requestReference) {
            var response = Event<GetResponseInterface>.RaiseFirst();
            
            if (CancellationRequested) {
                response.OperationCanceled("unable-to-acquire");
                return FinishedSynchronously;
            }

            if (null == requestReference) {
                response.Error("unable-to-acquire", "requestReference", "requestReference is required.");
                return FinishedSynchronously;
            }

            // if there is a continuation task for the canonical name that goes along with this, 
            // we should continue with that task, and get the heck out of here.
            // 

            var continuationTask = SessionCache<Task<Recognizer.RecognitionInfo>>.Value[requestReference];
            SessionCache<Task<Recognizer.RecognitionInfo>>.Value.Clear(requestReference);
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
            var canonicalName = CanonicalName.Parse(requestReference);
            if (canonicalName.IsCanonical) {
                var package = SearchForPackages(requestReference).FirstOrDefault();
                if (package != null) {
                    package.PackageSessionData.CouldNotDownload = true;
                }
            }

            return FinishedSynchronously;
        }

        public Task RecognizeFile(string requestReference, string localLocation, string remoteLocation) {
            var response = Event<GetResponseInterface>.RaiseFirst();
            
            if (string.IsNullOrEmpty(localLocation)) {
                response.Error("recognize-file", "local-location", "parameter 'local-location' is required to recognize a file");
                return FinishedSynchronously;
            }

            if (CancellationRequested) {
                response.OperationCanceled("recognize-file");
                return FinishedSynchronously;
            }

            var location = Event<GetCanonicalizedPath>.RaiseFirst(localLocation);
            if (location.StartsWith(@"\\")) {
                // a local unc path was passed. This isn't allowed--we need a file on a local volume that
                // the user has access to.
                response.Error("recognize-file", "local-location",
                    "local-location '{0}' appears to be a file on a remote server('{1}') . Recognized files must be local".format(localLocation, location));
                return FinishedSynchronously;
            }

            if (!File.Exists(location)) {
                response.FileNotFound(location);
                return FinishedSynchronously;
            }

            // if there is a continuation task for the canonical name that goes along with this, 
            // we should continue with that task, and get the heck out of here.
            // 
            if (null != requestReference) {
                var continuationTask = SessionCache<Task<Recognizer.RecognitionInfo>>.Value[requestReference];
                SessionCache<Task<Recognizer.RecognitionInfo>>.Value.Clear(requestReference);
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
                    response.FileNotRecognized(location, "Unexpected error recognizing file.");
                    return;
                }

                if (antecedent.Result.IsPackageFile) {
                    var package = Package.GetPackageFromFilename(location);
                    if (package != null) {
                        // mark it download 100%
                        package.PackageSessionData.DownloadProgress = 100;

                        SessionPackageFeed.Instance.Add(package);

                        PackageInformation(response, package);
                        response.Recognized(localLocation);
                    }
                    return;
                }

                if (antecedent.Result.IsPackageFeed) {
                    response.FeedAdded(location);
                    response.Recognized(location);
                }

                // if this isn't a package file, then there is something odd going on here.
                // we don't accept non-package files willy-nilly. 
                response.FileNotRecognized(location, "File isn't a package, and doesn't appear to have been requested. ");
            }, TaskContinuationOptions.AttachedToParent);
            return FinishedSynchronously;
        }

        private void PackageInformation(IPackageManagerResponse response, Package package, IEnumerable<CanonicalName> supercedents = null) {
            if (package != null) {
                supercedents = supercedents ?? Enumerable.Empty<CanonicalName>();
                response.PackageInformation(package.CanonicalName, package.LocalLocations.FirstOrDefault(), package.IsInstalled, package.IsBlocked,
                    package.IsRequired, package.IsClientRequested, package.IsActive, package.PackageSessionData.IsDependency, package.BindingPolicy == null ? 0 : package.BindingPolicy.Minimum, package.BindingPolicy == null ? 0 : package.BindingPolicy.Maximum,
                    package.RemoteLocations, package.FeedLocations, package.Dependencies.Select(each => each.CanonicalName), supercedents);
            }
        }

        public Task SetFeedFlags(string location, string activePassiveIgnored) {
            var response = Event<GetResponseInterface>.RaiseFirst();
            
            if (CancellationRequested) {
                response.OperationCanceled("set-feed");
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
            var response = Event<GetResponseInterface>.RaiseFirst();
            
            if (CancellationRequested) {
                response.OperationCanceled("suppress-feed");
                return FinishedSynchronously;
            }

            var suppressedFeeds = SessionCache<List<string>>.Value["suppressed-feeds"] ?? new List<string>();

            lock (suppressedFeeds) {
                if (!suppressedFeeds.Contains(location)) {
                    suppressedFeeds.Add(location);
                    SessionCache<List<string>>.Value["suppressed-feeds"] = suppressedFeeds;
                }
            }
            response.FeedSuppressed(location);
            return FinishedSynchronously;
        }

        internal void Updated() {
            foreach (var mre in _manualResetEvents) {
                mre.Set();
            }
        }

        internal List<string> BlockedScanLocations {
            get {
                return SessionCache<List<string>>.Value["suppressed-feeds"] ?? new List<string>();
            }
        }

        internal PackageFeed[] Feeds {
            get {
                try {
                    EnsureSystemFeedsAreLoaded();

                    var canFilterSession = Event<QueryPermission>.RaiseFirst(PermissionPolicy.EditSessionFeeds);
                    var canFilterSystem = Event<QueryPermission>.RaiseFirst(PermissionPolicy.EditSystemFeeds);
                    var filters = BlockedScanLocations.ToArray();
                    if( filters.IsNullOrEmpty()) {
                        return new PackageFeed[] { SessionPackageFeed.Instance, InstalledPackageFeed.Instance}.Union(Cache<PackageFeed>.Value.Values).Union(SessionCache<PackageFeed>.Value.SessionValues).ToArray();
                    }
                    return new PackageFeed[] {
                        SessionPackageFeed.Instance, InstalledPackageFeed.Instance
                    }.Union(from feed in Cache<PackageFeed>.Value.Values where !canFilterSystem || !feed.IsLocationMatch(filters) select feed)
                     .Union(from feed in SessionCache<PackageFeed>.Value.SessionValues where !canFilterSession || !feed.IsLocationMatch(filters) select feed).ToArray();
                } catch (Exception e) {
                    Logger.Error(e);
                    throw;
                }
            }
        }

        #region package scanning

        /// <summary>
        ///   Gets packages from all visible feeds based on criteria
        /// </summary>
        /// <param name="canonicalName"> </param>
        /// <param name="location"> </param>
        /// <returns> </returns>
        internal IEnumerable<Package> SearchForPackages(CanonicalName canonicalName, string location = null) {
            try {
                var allfeeds = Feeds;

                // get the filtered list of feeds.
                var feeds = string.IsNullOrEmpty(location) ? allfeeds : allfeeds.Where(each => each.IsLocationMatch(location)).ToArray();

                if (!string.IsNullOrEmpty(location ) || canonicalName.IsCanonical) {
                    // asking a specific feed, or they are not asking for more than one version of a given package.
                    // or asking for a specific version.
                    return feeds.SelectMany(each => each.FindPackages(canonicalName)).Distinct().ToArray();
                }

                var feedLocations = allfeeds.Select(each => each.Location);
                var packages = feeds.SelectMany(each => each.FindPackages(canonicalName)).Distinct().ToArray();

                var otherFeeds = packages.SelectMany(each => each.FeedLocations).Distinct().Where(each => !feedLocations.Contains(each.AbsoluteUri));
                // given a list of other feeds that we're not using, we can search each of those feeds for newer versions of the packages that we already have.
                var tf = TransientFeeds(otherFeeds);
                return packages.Union(packages.SelectMany(p => tf.SelectMany(each => each.FindPackages(p.CanonicalName.OtherVersionFilter)))).Distinct().ToArray();
            } catch (InvalidOperationException) {
                // this can happen if the collection changes during the operation (and can actually happen in the middle of .ToArray() 
                // since, locking the hell out of the collections isn't worth the effort, we'll just try again on this type of exception
                // and pray the collection won't keep changing :)
                Logger.Message("PERF HIT [REPORT THIS IF THIS IS CONSISTENT!]: Rerunning SearchForPackages!");
                return SearchForPackages(canonicalName, location);
            }
        }

        /// <summary>
        ///   This returns an collection of feed objects when given a list of feed locations. The items are cached in the session, so if mutliple calls ask for repeat items, it's not creating new objects all the time.
        /// </summary>
        /// <param name="locations"> List of feed locations </param>
        /// <returns> </returns>
        internal IEnumerable<PackageFeed> TransientFeeds(IEnumerable<Uri> locations) {
            var locs = locations.ToArray();
            var tf = SessionCache<List<PackageFeed>>.Value["TransientFeeds"] ?? (SessionCache<List<PackageFeed>>.Value["TransientFeeds"] = new List<PackageFeed>());
            var existingLocations = tf.Select(each => each.Location);
            var newLocations = locs.Where(each => !existingLocations.Contains(each.AbsoluteUri));
            var tasks = newLocations.Select(each => PackageFeed.GetPackageFeedFromLocation(each.AbsoluteUri )).ToArray();
            var newFeeds = tasks.Where(each => each.Result != null).Select(each => each.Result);
            tf.AddRange(newFeeds);
            return tf.Where(each => locs.Contains(each.Location.ToUri()));
        }

        /// <summary>
        ///   Gets just installed packages based on criteria
        /// </summary>
        /// <param name="canonicalName"> </param>
        /// <returns> </returns>
        internal IEnumerable<Package> SearchForInstalledPackages(CanonicalName canonicalName) {
            return InstalledPackageFeed.Instance.FindPackages(canonicalName);
        }

        internal IEnumerable<Package> InstalledPackages {
            get {
                return InstalledPackageFeed.Instance.FindPackages(CanonicalName.AllPackages);
            }
        }
        #endregion

        /// <summary>
        ///   This generates a list of files that need to be installed to sastisy a given package.
        /// </summary>
        /// <param name="package"> </param>
        /// <param name="hypothetical"> </param>
        /// <returns> </returns>
        private IEnumerable<Package> GenerateInstallGraph(Package package, bool hypothetical = false) {
            var response = Event<GetResponseInterface>.RaiseFirst();
            
            if (package.IsInstalled) {
                if (!package.PackageRequestData.NotifiedClientThisSupercedes) {
                    response.PackageSatisfiedBy(package.CanonicalName, package.CanonicalName);
                    package.PackageRequestData.NotifiedClientThisSupercedes = true;
                }

                yield break;
            }

            var packageData = package.PackageSessionData;

            if (!packageData.DoNotSupercede) {
                var installedSupercedents = SearchForInstalledPackages(package.CanonicalName.OtherVersionFilter);

                if (package.PackageSessionData.IsClientSpecified || hypothetical) {
                    // this means that we're talking about a requested package
                    // and not a dependent package and we can liberally construe supercedent 
                    // as anything with a highger version number
                    installedSupercedents = (from p in installedSupercedents where p.CanonicalName.Version > package.CanonicalName.Version select p).OrderByDescending(p => p.CanonicalName.Version).ToArray();
                } else {
                    // otherwise, we're installing a dependency, and we need something compatable.
                    installedSupercedents = (from p in installedSupercedents where p.IsAnUpdateFor(package) select p).OrderByDescending(p => p.CanonicalName.Version).ToArray();
                }
                var installedSupercedent = installedSupercedents.FirstOrDefault();
                if (installedSupercedent != null) {
                    if (!installedSupercedent.PackageRequestData.NotifiedClientThisSupercedes) {
                        response.PackageSatisfiedBy(package.CanonicalName, installedSupercedent.CanonicalName);
                        installedSupercedent.PackageRequestData.NotifiedClientThisSupercedes = true;
                    }
                    yield break; // a supercedent package is already installed.
                }

                // if told not to supercede, we won't even perform this check 
                packageData.Supercedent = null;

                var supercedents = SearchForPackages(package.CanonicalName.OtherVersionFilter).ToArray();

                if (package.PackageSessionData.IsClientSpecified || hypothetical) {
                    // this means that we're talking about a requested package
                    // and not a dependent package and we can liberally construe supercedent 
                    // as anything with a highger version number
                    supercedents = (from p in supercedents where p.CanonicalName.Version > package.CanonicalName.Version select p).OrderByDescending(p => p.CanonicalName.Version).ToArray();
                } else {
                    // otherwise, we're installing a dependency, and we need something compatable.
                    supercedents = (from p in supercedents where p.IsAnUpdateFor(package) select p).OrderByDescending(p => p.CanonicalName.Version).ToArray();
                }

                if (supercedents.Any()) {
                    if (packageData.AllowedToSupercede) {
                        foreach (var supercedent in supercedents) {
                            IEnumerable<Package> children;
                            try {
                                children = GenerateInstallGraph(supercedent, true);
                            } catch {
                                // can't be satisfied with that supercedent.
                                // we can quietly move along here.
                                continue;
                            }

                            // we should tell the client that we're making a substitution.
                            if (!supercedent.PackageRequestData.NotifiedClientThisSupercedes) {
                                response.PackageSatisfiedBy(package.CanonicalName, supercedent.CanonicalName);
                                supercedent.PackageRequestData.NotifiedClientThisSupercedes = true;
                            }

                            if (supercedent.CanonicalName.DiffersOnlyByVersion(package.CanonicalName)) {
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
                    } else {
                        // the user hasn't specifically asked us to supercede, yet we know of 
                        // potential supercedents. Let's force the user to make a decision.
                        // throw new PackageHasPotentialUpgradesException(packageToSatisfy, supercedents);
                        response.PackageHasPotentialUpgrades(package.CanonicalName, supercedents.Select(each => each.CanonicalName));
                        throw new OperationCompletedBeforeResultException();
                    }
                }
            }

            // if this isn't potentially installable, 
            if (!package.PackageSessionData.IsPotentiallyInstallable) {
                if (hypothetical) {
                    yield break;
                }

                // otherwise
                throw new OperationCompletedBeforeResultException();
            }


            if (packageData.CouldNotDownload) {
                if (!hypothetical) {
                    response.UnableToDownloadPackage(package.CanonicalName);
                }
                throw new OperationCompletedBeforeResultException();
            }

            if (packageData.PackageFailedInstall) {
                if (!hypothetical) {
                    response.UnableToInstallPackage(package.CanonicalName);
                }
                throw new OperationCompletedBeforeResultException();
            }

            var childrenFailed = false;
            foreach (var d in package.Dependencies) {
                IEnumerable<Package> children;
                try {
                    children = GenerateInstallGraph(d);
                } catch {
                    Logger.Message("Generating install graph for child dependency failed [{0}]", d.CanonicalName);
                    childrenFailed = true;
                    continue;
                }

                if (!childrenFailed) {
                    foreach (var child in children) {
                        yield return child;
                    }
                }
            }

            if (childrenFailed) {
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

                foreach (var package in installedPackages.Where(each => each.IsRequired)) {
                    package.UpdateDependencyFlags();
                }
            }
        }

        public Task GetPolicy(string policyName) {
            var response = Event<GetResponseInterface>.RaiseFirst();
            var policies = PermissionPolicy.AllPolicies.Where(each => each.Name.NewIsWildcardMatch(policyName)).ToArray();

            foreach (var policy in policies) {
                response.PolicyInformation(policy.Name, policy.Description, policy.Accounts);
            }
            if (policies.IsNullOrEmpty()) {
                response.Error("get-policy", "name", "policy '{0}' not found".format(policyName));
            }
            return FinishedSynchronously;
        }

        public Task AddToPolicy(string policyName, string account) {
            var response = Event<GetResponseInterface>.RaiseFirst();

            if (Event<CheckForPermission>.RaiseFirst(PermissionPolicy.ModifyPolicy)) {
                PermissionPolicy.AllPolicies.FirstOrDefault(each => each.Name.Equals(policyName, StringComparison.CurrentCultureIgnoreCase)).With(policy => {
                    try {
                        policy.Add(account);
                    } catch {
                        response.Error("remove-from-policy", "account", "policy '{0}' could not remove account '{1}'".format(policyName, account));
                    }
                }, () => response.Error("remove-from-policy", "name", "policy '{0}' not found".format(policyName)));
            }
            return FinishedSynchronously;
        }

        public Task RemoveFromPolicy(string policyName, string account) {
            var response = Event<GetResponseInterface>.RaiseFirst();

            if (Event<CheckForPermission>.RaiseFirst(PermissionPolicy.ModifyPolicy)) {
                PermissionPolicy.AllPolicies.FirstOrDefault(each => each.Name.Equals(policyName, StringComparison.CurrentCultureIgnoreCase)).With(policy => {
                    try {
                        policy.Remove(account);
                    } catch {
                        response.Error("remove-from-policy", "account", "policy '{0}' could not remove account '{1}'".format(policyName, account));
                    }
                }, () => response.Error("remove-from-policy", "name", "policy '{0}' not found".format(policyName)));
            }
            return FinishedSynchronously;
        }

        public Task CreateSymlink(string existingLocation, string newLink, LinkType linkType) {
            var response = Event<GetResponseInterface>.RaiseFirst();
            if (Event<CheckForPermission>.RaiseFirst(PermissionPolicy.Symlink)) {
                if (string.IsNullOrEmpty(existingLocation)) {
                    response.Error("symlink", "existing-location", "location is null/empty. ");
                    return FinishedSynchronously;
                }

                if (string.IsNullOrEmpty(newLink)) {
                    response.Error("symlink", "new-link", "new-link is null/empty.");
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
                        response.Error("symlink", "existing-location", "can not make symlink for location '{0}'".format(existingLocation));
                    }
                } catch (Exception exception) {
                    response.Error("symlink", "", "Failed to create symlink -- error: {0}".format(exception.Message));
                }
            }
            return FinishedSynchronously;
        }

        public Task SetFeedStale(string feedLocation) {
            PackageFeed.GetPackageFeedFromLocation(feedLocation).Continue(feed => {
                if (feed != null) {
                    feed.Stale = true;
                }
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
            var response = Event<GetResponseInterface>.RaiseFirst();

            if (messages.HasValue) {
                SessionCache<string>.Value["LogMessages"] = messages.ToString();
            }

            if (errors.HasValue) {
                SessionCache<string>.Value["LogErrors"] = errors.ToString();
            }

            if (warnings.HasValue) {
                SessionCache<string>.Value["LogWarnings"] = warnings.ToString();
            }
            response.LoggingSettings(Logger.Messages, Logger.Warnings, Logger.Errors);
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