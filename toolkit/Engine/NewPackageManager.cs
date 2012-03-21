//-----------------------------------------------------------------------
// <copyright company="CoApp Project">
//     Copyright (c) 2011 Garrett Serack . All rights reserved.
// </copyright>
// <license>
//     The software is licensed under the Apache 2.0 License (the "License")
//     You may not use the software except in compliance with the License. 
// </license>
//-----------------------------------------------------------------------


namespace CoApp.Toolkit.Engine {
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;
    using Crypto;
    using Exceptions;
    using Extensions;
    using Feeds;
    using Logging;
    using PackageFormatHandlers;
    using Tasks;
    using Toolkit.Exceptions;

    public class NewPackageManager {

        public static NewPackageManager Instance = new NewPackageManager();
        private static Regex _canonicalNameParser = new Regex(@"^(.*)-(\d{1,5}\.\d{1,5}\.\d{1,5}\.\d{1,5})-(any|x86|x64|arm)-([0-9a-f]{16})$",RegexOptions.IgnoreCase);

        private List<ManualResetEvent> manualResetEvents = new List<ManualResetEvent>();


        private bool CancellationRequested {
            get { return PackageManagerSession.Invoke.CancellationRequested(); }
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
                var result = SessionCache<IEnumerable<string>>.Value["session-feeds"];
                return result.IsNullOrEmpty() ? Enumerable.Empty<string>() : result;
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
                        LogMessage("Feed {0} was unable to load.", feedLocation);
                    }
                }, TaskContinuationOptions.AttachedToParent);
            }
        }

        private void LogMessage(string message, params object[] objs) {
            string msg = message.format(objs);
            // do something with the message?
        }

        public Task FindPackages( string canonicalName, string name, string version, string arch, string publicKeyToken,
            bool? dependencies, bool? installed, bool? active, bool? required, bool? blocked, bool? latest,
            int? index, int? maxResults, string location, bool? forceScan, bool? updates, bool? upgrades , bool? trimable , PackageManagerMessages messages) {

            var t = Task.Factory.StartNew(() => {
                if (messages != null) {
                    messages.Register();
                }

                if (CancellationRequested) {
                    PackageManagerMessages.Invoke.OperationCancelled("find-package");
                    return;
                }

                if (!PackageManagerSession.Invoke.CheckForPermission(PermissionPolicy.EnumeratePackages)) {
                    PackageManagerMessages.Invoke.PermissionRequired("EnumeratePackages");
                    return;
                }

                UpdateIsRequestedFlags();

                // get basic list of packages based on primary characteristics
                if (!string.IsNullOrEmpty(canonicalName)) {
                    // if canonical name is passed, override name,version,pkt,arch with the parsed canonicalname.
                    var match = _canonicalNameParser.Match(canonicalName.ToLower());
                    if (!match.Success) {
                        PackageManagerMessages.Invoke.Error("find-packages", "canonical-name",
                            "Canonical name '{0}' does not appear to be a valid canonical name".format(canonicalName));
                        return;
                    }

                    name = match.Groups[1].Captures[0].Value;
                    version = match.Groups[2].Captures[0].Value;
                    arch = match.Groups[3].Captures[0].Value;
                    publicKeyToken = match.Groups[4].Captures[0].Value;
                }

                if( forceScan == true ) {
                    foreach( var feed in Feeds ) {
                        feed.Stale = true;
                    }
                }

                var results = SearchForPackages(name, version, arch, publicKeyToken, location);
                // filter results of list based on secondary filters

                results = from package in results
                    where
                        (installed == null || package.IsInstalled == installed) && (active == null || package.IsActive == active) &&
                            (required == null || package.IsRequired == required) && (blocked == null || package.IsBlocked == blocked)

                    select package;

                // if the client is asking for Updates:
                //      - get the packages that match the search criteria. 'pkgs'
                //      - filter out 'do-not-update' and 'blocked' packages (unless passing in blocked flag)
                //      - filter 'pkgs' so that every package in the list is the most recent binary compatible one.
                //      - for each package in 'pkgs', find out if there is a higher binary compatible one available that is not installed.
                //          - add that to the list of results; return the distinct results

                // if the client is asking for Upgrades:
                //      - get list packages that meet the search criteria. 'pkgs'. 
                //      - filter out 'do-not-upgrade' and 'do-not-update' and 'blocked' packages--(unless passing in blocked flag)
                //      - (by definition upgrades exclude updates)
                //      - for each packge in 'pkgs' find out if ther is a higher (non-compatible) version that is not installed.
                //          - add that to the list of results; return the distinct results

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

                    results = results.Union(deps).Distinct();
                }

                // paginate the results
                if (index.HasValue) {
                    results = results.Skip(index.Value);
                }

                if (maxResults.HasValue) {
                    results = results.Take(maxResults.Value);
                }

                if (results.Any()) {
                    foreach (var package in results) {
                        if (CancellationRequested) {
                            PackageManagerMessages.Invoke.OperationCancelled("find-packages");
                            return;
                        }

                        // otherwise, we're installing a dependency, and we need something compatable.
                        var supercedents = (from p in SearchForPackages(package.Name, null, package.Architecture.ToString(), package.PublicKeyToken)
                            where p.InternalPackageData.PolicyMinimumVersion <= package.Version && p.InternalPackageData.PolicyMaximumVersion >= package.Version
                            select p).OrderByDescending(p => p.Version).ToArray();

                        PackageManagerMessages.Invoke.PackageInformation(package, supercedents);
                    }
                }
                else {
                    PackageManagerMessages.Invoke.NoPackagesFound();
                }

            }, TaskCreationOptions.AttachedToParent);
            return t;
        }

        public Task GetPackageDetails(string canonicalName, PackageManagerMessages messages) {
            var t = Task.Factory.StartNew(() => {
                if (messages != null) {
                    messages.Register();
                }

                if (CancellationRequested) {
                    PackageManagerMessages.Invoke.OperationCancelled("get-package-details");
                    return;
                }

                var package = GetSinglePackage(canonicalName, "get-package-details");
                if (package == null) {
                    return;
                }

                PackageManagerMessages.Invoke.PackageDetails(package);
            }, TaskCreationOptions.AttachedToParent);
            return t;
        }

        public Task InstallPackage(string canonicalName, bool? autoUpgrade, bool? force, bool? download, bool? pretend,bool? isUpdating, bool? isUpgrading, PackageManagerMessages messages) {
            var t = Task.Factory.StartNew(() => {
                if (messages != null) {
                    messages.Register();
                }

                if (CancellationRequested) {
                    PackageManagerMessages.Invoke.OperationCancelled("install-package");
                    return;
                }

                var overallProgress = 0.0;
                var eachTaskIsWorth = 0.0;
                var numberOfPackagesToInstall = 0;
                var numberOfPackagesToDownload = 0;

                using (var manualResetEvent = new ManualResetEvent(true)) {
                    try {
                        lock (manualResetEvents) {
                            manualResetEvents.Add(manualResetEvent);
                        }

                        var packagesTriedToDownloadThisTask = new List<Package>();

                        var package = GetSinglePackage(canonicalName, "install-package");

                        if (package == null) {
                            PackageManagerMessages.Invoke.UnknownPackage(canonicalName);
                            return;
                        }

                        // is the user authorized to install this?
                        var highestInstalledPackage =
                            SearchForInstalledPackages(package.Name, null, package.Architecture.ToString(), package.PublicKeyToken).HighestPackages();
                        if (highestInstalledPackage.Any() && highestInstalledPackage.FirstOrDefault().Version < package.Version) {
                            if (package.IsBlocked) {
                                PackageManagerMessages.Invoke.PackageBlocked(canonicalName);
                                return;
                            }

                            if (!PackageManagerSession.Invoke.CheckForPermission(PermissionPolicy.UpdatePackage)) {
                                PackageManagerMessages.Invoke.PermissionRequired("UpdatePackage");
                                return;
                            }
                        }
                        else {
                            if (!PackageManagerSession.Invoke.CheckForPermission(PermissionPolicy.InstallPackage)) {
                                PackageManagerMessages.Invoke.PermissionRequired("InstallPackage");
                                return;
                            }
                        }

                        // if this is an update, 
                        //      - check to see if there is a compatible package already installed that is marked do-not-update
                        //        fail if so.
                        
                        // if this is an upgrade, 
                        //      - check to see if this package has the do-not-upgrade flag.


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
                                PackageManagerMessages.Invoke.OperationCancelled("install-package");
                                return;
                            }

                            IEnumerable<Package> installGraph = null;
                            try {
                                installGraph = GenerateInstallGraph(package).ToArray();
                            }
                            catch (OperationCompletedBeforeResultException) {
                                // we encountered an unresolvable condition in the install graph.
                                // messages should have already been sent.
                                PackageManagerMessages.Invoke.FailedPackageInstall(canonicalName, package.InternalPackageData.LocalLocation,
                                    "One or more dependencies are unable to be resolved.");
                                return;
                            }

                            // seems like a good time to check if we're supposed to bail...
                            if (CancellationRequested) {
                                PackageManagerMessages.Invoke.OperationCancelled("install-package");
                                return;
                            }

                            if (download == false && pretend == true) {
                                // we can just return a bunch of foundpackage messages, since we're not going to be 
                                // actually installing anything, nor trying to download anything.
                                foreach( var p in installGraph) {
                                    PackageManagerMessages.Invoke.PackageInformation(package, Enumerable.Empty<Package>());
                                }
                                return;
                            }

                            // we've got an install graph.
                            // let's see if we've got all the files
                            var missingFiles = from p in installGraph where !p.InternalPackageData.HasLocalLocation select p;

                            if( download == true ) {
                                // we want to try downloading all the files that we're missing, regardless if we've tried before.
                                // unless we've already tried in this task once. 
                                foreach( var p in missingFiles.Where( each => packagesTriedToDownloadThisTask.Contains(each)) ) {
                                    packagesTriedToDownloadThisTask.Add(p);
                                    p.PackageSessionData.CouldNotDownload = false;
                                }
                            }

                            if( numberOfPackagesToInstall != installGraph.Count() || numberOfPackagesToDownload != missingFiles.Count() ) {
                                // recalculate the rest of the install progress based on the new install graph.
                                numberOfPackagesToInstall = installGraph.Count();
                                numberOfPackagesToDownload = missingFiles.Count();

                                eachTaskIsWorth = (1.0 - overallProgress)/(numberOfPackagesToInstall + numberOfPackagesToDownload);
                            }

                            if (missingFiles.Any()) {
                                // we've got some packages to install that don't have files.
                                foreach (var p in missingFiles.Where(p => !p.PackageSessionData.HasRequestedDownload)) {
                                    PackageManagerMessages.Invoke.RequireRemoteFile(p.CanonicalName,
                                        p.InternalPackageData.RemoteLocations, PackageManagerSettings.CoAppPackageCache, false);

                                    p.PackageSessionData.HasRequestedDownload = true;
                                }
                            }
                            else {
                                if( pretend == true ) {
                                    // we can just return a bunch of found-package messages, since we're not going to be 
                                    // actually installing anything, and everything we needed is downloaded.
                                    foreach (var p in installGraph) {
                                        PackageManagerMessages.Invoke.PackageInformation(p, Enumerable.Empty<Package>());
                                    }
                                    return;
                                }

                                var failed = false;
                                // no missing files? Check
                                // complete install graph? Check

                                foreach (var p in installGraph) {
                                    var pkg = p;
                                    // seems like a good time to check if we're supposed to bail...
                                    if (CancellationRequested) {
                                        PackageManagerMessages.Invoke.OperationCancelled("install-package");
                                        return;
                                    }
                                    var validLocation = package.PackageSessionData.LocalValidatedLocation;

                                    try {
                                        if (!pkg.IsInstalled) {
                                            if (string.IsNullOrEmpty(validLocation)) {
                                                // can't find a valid location
                                                PackageManagerMessages.Invoke.FailedPackageInstall(pkg.CanonicalName, pkg.InternalPackageData.LocalLocation, "Can not find local valid package");
                                                pkg.PackageSessionData.PackageFailedInstall = true;
                                            }
                                            else {

                                                var lastProgress = 0;
                                                // GS01: We should put a softer lock here to keep the client aware that packages 
                                                // are being installed on other threads...
                                                lock (typeof (MSIBase)) {
                                                    if (EngineService.DoesTheServiceNeedARestart) {
                                                        // something has changed where we need restart the service before we can continue.
                                                        // and the one place we don't wanna be when we issue a shutdown in in Install :) ...
                                                        EngineService.RestartService();
                                                        PackageManagerMessages.Invoke.OperationCancelled("install-package");
                                                        return;
                                                    }

                                                    pkg.Install(percentage => {
                                                        overallProgress += ((percentage - lastProgress) *eachTaskIsWorth)/100;
                                                        lastProgress = percentage;
                                                        PackageManagerMessages.Invoke.InstallingPackageProgress(pkg.CanonicalName, percentage, (int)(overallProgress*100));
                                                    });
                                                }
                                                overallProgress += ((100 - lastProgress)*eachTaskIsWorth)/100;
                                                PackageManagerMessages.Invoke.InstallingPackageProgress(pkg.CanonicalName, 100,(int)(overallProgress*100));
                                                PackageManagerMessages.Invoke.InstalledPackage(pkg.CanonicalName);
                                                Signals.InstalledPackage(pkg.CanonicalName);
                                            }
                                        }
                                    }
                                    catch (Exception e ) /* (PackageInstallFailedException pife)  */ {
                                        Logger.Error("FAILED INSTALL");
                                        Logger.Error(e);

                                        PackageManagerMessages.Invoke.FailedPackageInstall(pkg.CanonicalName, validLocation, "Package failed to install.");
                                        pkg.PackageSessionData.PackageFailedInstall = true;

                                        if (!pkg.PackageSessionData.AllowedToSupercede) {
                                            throw new OperationCompletedBeforeResultException(); // user specified packge as critical.
                                        }
                                        failed = true;
                                        break;
                                    }
                                }
                                if (!failed) {
                                    if( isUpdating == true ) {
                                        // if this is marked as an update
                                        // remove REQUESTED flag from all older compatible version 
                                        
                                    }
                                    if (isUpgrading == true) {
                                        // if this is marked as an update
                                        // remove REQUESTED flag from all older compatible version 

                                    }

                                    // W00T ... We did it!
                                    // check for restart required...
                                    if (EngineService.DoesTheServiceNeedARestart) {
                                        // something has changed where we need restart the service before we can continue.
                                        // and the one place we don't wanna be when we issue a shutdown in in Install :) ...
                                        EngineService.RestartService();
                                        PackageManagerMessages.Invoke.OperationCancelled("install-package");
                                        return;
                                    }
                                    return;
                                }

                                // otherwise, let's run it thru again. maybe it'll come together.
                            }

                            //----------------------------------------------------------------------------
                            // wait until either the manualResetEvent is set, but check every second or so
                            // to see if the client has cancelled the operation.
                            while (!manualResetEvent.WaitOne(500)) {
                                if (CancellationRequested) {
                                    PackageManagerMessages.Invoke.OperationCancelled("install-package");
                                    return;
                                }

                                // we can also use this opportunity to update progress on any outstanding download tasks.
                                overallProgress += missingFiles.Sum(missingFile => ((missingFile.PackageSessionData.DownloadProgressDelta*eachTaskIsWorth)/100));
                            }
                        } while (true);

                    }
                    catch (OperationCompletedBeforeResultException) {
                        // can't continue with options given.
                        return;
                    }
                    finally {
                        // remove manualResetEvent from the mre list
                        lock (manualResetEvents) {
                            manualResetEvents.Remove(manualResetEvent);
                        }
                    }
                }

            }, TaskCreationOptions.AttachedToParent);
            return t;
        }


        public Task DownloadProgress( string canonicalName , int? downloadProgress,PackageManagerMessages messages ) {
            var t = Task.Factory.StartNew(() => {
                if (messages != null) {
                    messages.Register();
                }
                try {
                    // it takes a non-trivial amount of time to lookup a package by its name.
                    // so, we're going to cache the package in the session.
                    // of course if there isn't one, (because we're downloading soemthing we don't know what it's actualy canonical name is)
                    // we don't want to try looking up each time again, since that's the worst-case-scenario, we have to
                    // cache the fact that we have cached nothing.
                    // /facepalm.

                    Package package;

                    var cachedPackageName = SessionCache<string>.Value["cached-the-lookup" + canonicalName];

                    if( cachedPackageName == null ) {
                        SessionCache<string>.Value["cached-the-lookup" + canonicalName] = "yes";

                        package = GetSinglePackage(canonicalName, "download-progress", true);

                        if (package != null) {
                            SessionCache<Package>.Value[canonicalName] = package;
                        }
                    } else {
                        package = SessionCache<Package>.Value[ canonicalName];
                    }
                    
                    if (package != null) {
                        package.PackageSessionData.DownloadProgress = Math.Max(package.PackageSessionData.DownloadProgress, downloadProgress.GetValueOrDefault());
                    }
                } catch {
                    // suppress any exceptions... we just don't care!
                }
                SessionCache<string>.Value["busy" + canonicalName] = null;
            }, TaskCreationOptions.AttachedToParent);
            return t;
        }


        public Task ListFeeds(int? index, int? maxResults, PackageManagerMessages messages) {
            var t = Task.Factory.StartNew(() => {

                if (messages != null) {
                    messages.Register();
                }

                if (CancellationRequested) {
                    PackageManagerMessages.Invoke.OperationCancelled("list-feeds");
                    return;
                }


                var canFilterSession = PackageManagerSession.Invoke.CheckForPermission(PermissionPolicy.EditSessionFeeds);
                var canFilterSystem = PackageManagerSession.Invoke.CheckForPermission(PermissionPolicy.EditSystemFeeds);

                var activeSessionFeeds = SessionCache<PackageFeed>.Value.SessionValues;
                var activeSystemFeeds = Cache<PackageFeed>.Value.Values;

                var x = from feedLocation in SystemFeedLocations
                    let theFeed = activeSystemFeeds.Where(each => each.IsLocationMatch(feedLocation)).FirstOrDefault()
                    let validated = theFeed != null
                    select new {
                        feed = feedLocation,
                        LastScanned = validated ? theFeed.LastScanned : DateTime.FromFileTime(0),
                        session = false,
                        suppressed = canFilterSystem && BlockedScanLocations.Contains(feedLocation),
                        validated,
                    };

                var y = from feedLocation in SessionFeedLocations
                    let theFeed = activeSessionFeeds.Where(each => each.IsLocationMatch(feedLocation)).FirstOrDefault()
                    let validated = theFeed != null
                    select new {
                        feed = feedLocation,
                        LastScanned = validated ? theFeed.LastScanned : DateTime.FromFileTime(0),
                        session = true,
                        suppressed = canFilterSession && BlockedScanLocations.Contains(feedLocation),
                        validated,
                    };

                var results = x.Union(y);

                // paginate the results
                if (index.HasValue) {
                    results = results.Skip(index.Value);
                }

                if (maxResults.HasValue) {
                    results = results.Take(maxResults.Value);
                }

                if (results.Any()) {
                    foreach (var f in results) {
                        PackageManagerMessages.Invoke.FeedDetails(f.feed, f.LastScanned, f.session, f.suppressed, f.validated);
                    }
                }
                else {
                    PackageManagerMessages.Invoke.NoFeedsFound();
                }


            }, TaskCreationOptions.AttachedToParent);
            return t;
        }

        public Task RemoveFeed(string location, bool? session, PackageManagerMessages messages) {
            var t = Task.Factory.StartNew(() => {
                if (messages != null) {
                    messages.Register();
                }

                if (CancellationRequested) {
                    PackageManagerMessages.Invoke.OperationCancelled("remove-feed");
                    return;
                }

                // Note: This may need better lookup/matching for the location
                // as location can be a fuzzy match.

                if (session ?? false) {
                    // session feed specfied
                    if (!PackageManagerSession.Invoke.CheckForPermission(PermissionPolicy.EditSessionFeeds)) {
                        PackageManagerMessages.Invoke.PermissionRequired("EditSessionFeeds");
                        return;
                    }

                    RemoveSessionFeed(location);
                    PackageManagerMessages.Invoke.FeedRemoved(location);
                }
                else {
                    // system feed specified
                    if (!PackageManagerSession.Invoke.CheckForPermission(PermissionPolicy.EditSystemFeeds)) {
                        PackageManagerMessages.Invoke.PermissionRequired("EditSystemFeeds");
                        return;
                    }

                    RemoveSystemFeed(location);
                    PackageManagerMessages.Invoke.FeedRemoved(location);
                }
            }, TaskCreationOptions.AttachedToParent);
            return t;
        }

        public Task AddFeed(string location, bool? session, PackageManagerMessages messages) {
            var t = Task.Factory.StartNew(() => {
                if (messages != null) {
                    messages.Register();
                }

                if (CancellationRequested) {
                    PackageManagerMessages.Invoke.OperationCancelled("add-feed");
                    return;
                }

                if (session ?? false) {
                    // new feed is a session feed
                    // session feed specfied
                    if (!PackageManagerSession.Invoke.CheckForPermission(PermissionPolicy.EditSessionFeeds)) {
                        PackageManagerMessages.Invoke.PermissionRequired("EditSessionFeeds");
                        return;
                    }

                    // check if it is already a system feed
                    if (SystemFeedLocations.Contains(location)) {
                        PackageManagerMessages.Invoke.Warning("add-feed", "location", "location '{0}' is already a system feed".format(location));
                        return;
                    }

                    if (SessionFeedLocations.Contains(location)) {
                        PackageManagerMessages.Invoke.Warning("add-feed", "location", "location '{0}' is already a session feed".format(location));
                        return;
                    }

                    AddSessionFeed(location);
                    PackageManagerMessages.Invoke.FeedAdded(location);

                    // add feed to the session feeds.
                    PackageFeed.GetPackageFeedFromLocation(location).ContinueWith(antecedent => {
                        var foundFeed = antecedent.Result;
                        if (foundFeed != null) {
                            if (foundFeed != SessionPackageFeed.Instance || foundFeed != InstalledPackageFeed.Instance ) {
                                SessionCache<PackageFeed>.Value[location] = foundFeed;    
                            }
                        }
                        else {
                            PackageManagerMessages.Invoke.Error("add-feed", "location",
                                "failed to recognize location '{0}' as a valid package feed".format(location));
                            LogMessage("Feed {0} was unable to load.", location);
                        }
                    }, TaskContinuationOptions.AttachedToParent);

                }
                else {
                    // new feed is a system feed
                    if (!PackageManagerSession.Invoke.CheckForPermission(PermissionPolicy.EditSystemFeeds)) {
                        PackageManagerMessages.Invoke.PermissionRequired("EditSystemFeeds");
                        return;
                    }

                    if (SystemFeedLocations.Contains(location)) {
                        PackageManagerMessages.Invoke.Warning("add-feed", "location", "location '{0}' is already a system feed".format(location));
                        return;
                    }

                    AddSystemFeed(location);
                    PackageManagerMessages.Invoke.FeedAdded(location);

                    // add feed to the system feeds.
                    PackageFeed.GetPackageFeedFromLocation(location).ContinueWith(antecedent => {
                        var foundFeed = antecedent.Result;
                        if (foundFeed != null) {
                            if (foundFeed != SessionPackageFeed.Instance || foundFeed != InstalledPackageFeed.Instance) {
                                Cache<PackageFeed>.Value[location] = foundFeed;
                            }
                        }
                        else {
                            PackageManagerMessages.Invoke.Error("add-feed", "location",
                                "failed to recognize location '{0}' as a valid package feed".format(location));
                            LogMessage("Feed {0} was unable to load.", location);
                        }
                    }, TaskContinuationOptions.AttachedToParent);
                }

            }, TaskCreationOptions.AttachedToParent);
            return t;
        }

        public Task VerifyFileSignature(string filename, PackageManagerMessages messages) {
            var t = Task.Factory.StartNew(() => {
                if (messages != null) {
                    messages.Register();
                }

                if (CancellationRequested) {
                    PackageManagerMessages.Invoke.OperationCancelled("verify-signature");
                    return;
                }

                if (string.IsNullOrEmpty(filename)) {
                    PackageManagerMessages.Invoke.Error("verify-signature", "filename", "parameter 'filename' is required to verify a file");
                    return;
                }

                var location = PackageManagerSession.Invoke.GetCanonicalizedPath(filename);

                if (!File.Exists(location)) {
                    PackageManagerMessages.Invoke.FileNotFound(location);
                    return;
                }

                var r = Verifier.HasValidSignature(location);
                if (r) {
                    PackageManagerMessages.Invoke.SignatureValidation(location, r, Verifier.GetPublisherInformation(location)["PublisherName"]);
                }
                else {
                    PackageManagerMessages.Invoke.SignatureValidation(location, false, null);
                }

            }, TaskCreationOptions.AttachedToParent);
            return t;
        }

        public Task SetPackage(string canonicalName, bool? active, bool? required, bool? blocked, bool? doNotUpdate, bool? doNotUpgrade, PackageManagerMessages messages) {
            var t = Task.Factory.StartNew(() => {
                if (messages != null) {
                    messages.Register();
                }

                if (CancellationRequested) {
                    PackageManagerMessages.Invoke.OperationCancelled("set-package");
                    return;
                }

                var package = GetSinglePackage(canonicalName, "set-package");

                if (package == null) {
                    PackageManagerMessages.Invoke.UnknownPackage(canonicalName);
                    return;
                }

                if (!package.IsInstalled) {
                    PackageManagerMessages.Invoke.Error("set-package", "canonical-name", "package '{0}' is not installed.".format(canonicalName));
                    return;
                }

                // seems like a good time to check if we're supposed to bail...
                if (CancellationRequested) {
                    PackageManagerMessages.Invoke.OperationCancelled("set-package");
                    return;
                }

                if (true == active) {
                    if (!PackageManagerSession.Invoke.CheckForPermission(PermissionPolicy.ChangeActivePackage)) {
                        PackageManagerMessages.Invoke.PermissionRequired("ChangeActivePackage");
                    }
                    else {
                        package.SetPackageCurrent();
                    }
                }

                if (false == active) {
                    if (!PackageManagerSession.Invoke.CheckForPermission(PermissionPolicy.ChangeActivePackage)) {
                        PackageManagerMessages.Invoke.PermissionRequired("ChangeActivePackage");
                    }
                    else {
                        SearchForInstalledPackages(package.Name, null, package.Architecture.ToString(), package.PublicKeyToken).HighestPackages().FirstOrDefault().
                            SetPackageCurrent();
                    }
                }

                if (true == required) {
                    if (!PackageManagerSession.Invoke.CheckForPermission(PermissionPolicy.ChangeRequiredState)) {
                        PackageManagerMessages.Invoke.PermissionRequired("ChangeRequiredState");
                    }
                    else {
                        package.IsRequired = true;
                    }
                }

                if (false == required) {
                    if (!PackageManagerSession.Invoke.CheckForPermission(PermissionPolicy.ChangeRequiredState)) {
                        PackageManagerMessages.Invoke.PermissionRequired("ChangeRequiredState");
                    }
                    else {
                        package.IsRequired = false;
                    }
                }

                if (true == blocked) {
                    if (!PackageManagerSession.Invoke.CheckForPermission(PermissionPolicy.ChangeBlockedState)) {
                        PackageManagerMessages.Invoke.PermissionRequired("ChangeBlockedState");
                    }
                    else {
                        package.IsBlocked = true;
                    }
                }

                if (false == blocked) {
                    if (!PackageManagerSession.Invoke.CheckForPermission(PermissionPolicy.ChangeBlockedState)) {
                        PackageManagerMessages.Invoke.PermissionRequired("ChangeBlockedState");
                    }
                    else {
                        package.IsBlocked = false;
                    }
                }

                if( true == doNotUpdate) {
                    if (!PackageManagerSession.Invoke.CheckForPermission(PermissionPolicy.InstallPackage)) {
                        PackageManagerMessages.Invoke.PermissionRequired("InstallPackage");
                    } else {
                        package.DoNotUpdate = true;
                    }
                }
                if (false == doNotUpdate) {
                    if (!PackageManagerSession.Invoke.CheckForPermission(PermissionPolicy.InstallPackage)) {
                        PackageManagerMessages.Invoke.PermissionRequired("InstallPackage");
                    } else {
                        package.DoNotUpdate = false;
                    }
                }

                if (true == doNotUpgrade) {
                    if (!PackageManagerSession.Invoke.CheckForPermission(PermissionPolicy.InstallPackage)) {
                        PackageManagerMessages.Invoke.PermissionRequired("InstallPackage");
                    } else {
                        package.DoNotUpgrade = true;
                    }
                }
                if (false == doNotUpgrade) {
                    if (!PackageManagerSession.Invoke.CheckForPermission(PermissionPolicy.InstallPackage)) {
                        PackageManagerMessages.Invoke.PermissionRequired("InstallPackage");
                    } else {
                        package.DoNotUpgrade = false;
                    }
                }

                PackageManagerMessages.Invoke.PackageInformation(package, Enumerable.Empty<Package>());

            }, TaskCreationOptions.AttachedToParent);
            return t;
        }

        public Task RemovePackage(string canonicalName, bool? force ,PackageManagerMessages messages) {
            var t = Task.Factory.StartNew(() => {
                if (messages != null) {
                    messages.Register();
                }

                if (CancellationRequested) {
                    PackageManagerMessages.Invoke.OperationCancelled("remove-package");
                    return;
                }


                if (!PackageManagerSession.Invoke.CheckForPermission(PermissionPolicy.RemovePackage)) {
                    PackageManagerMessages.Invoke.PermissionRequired("RemovePackage");
                    return;
                }

                var package = GetSinglePackage(canonicalName, "remove-package");
                if (package == null) {
                    PackageManagerMessages.Invoke.UnknownPackage(canonicalName);
                    return;
                }

                if (package.Name.Equals( "coapp.toolkit", StringComparison.CurrentCultureIgnoreCase ) && package.PublicKeyToken.Equals("1e373a58e25250cb") && package.IsActive ) {
                    PackageManagerMessages.Invoke.Error("remove-package", "canonical-name", "Active CoApp Engine may not be removed");
                    return;
                }

                if (!package.IsInstalled) {
                    PackageManagerMessages.Invoke.Error("remove-package", "canonical-name", "package '{0}' is not installed.".format(canonicalName));
                    return;
                }

                if (package.IsBlocked) {
                    PackageManagerMessages.Invoke.PackageBlocked(canonicalName);
                    return;
                }
                if (true != force) {
                    UpdateIsRequestedFlags();
                    if (package.PackageSessionData.IsDependency) {
                        PackageManagerMessages.Invoke.FailedPackageRemoval(canonicalName,
                            "Package '{0}' is a required dependency of another package.".format(canonicalName));
                        return;
                    }

                }
                // seems like a good time to check if we're supposed to bail...
                if (CancellationRequested) {
                    PackageManagerMessages.Invoke.OperationCancelled("remove-package");
                    return;
                }

                try {
                    package.Remove((percentage) => PackageManagerMessages.Invoke.RemovingPackageProgress(package.CanonicalName, percentage));

                    PackageManagerMessages.Invoke.RemovingPackageProgress(canonicalName, 100);
                    PackageManagerMessages.Invoke.RemovedPackage(canonicalName);
                    
                    Signals.RemovedPackage(canonicalName);
                }
                catch (OperationCompletedBeforeResultException e) {
                    PackageManagerMessages.Invoke.FailedPackageRemoval(canonicalName, e.Message);
                    return;
                }

            }, TaskCreationOptions.AttachedToParent);
            return t;
        }

        public Task UnableToAcquire(string canonicalName, PackageManagerMessages messages) {
            var t = Task.Factory.StartNew(() => {
                if (messages != null) {
                    messages.Register();
                }

                if (CancellationRequested) {
                    PackageManagerMessages.Invoke.OperationCancelled("unable-to-acquire");
                    return;
                }


                if (canonicalName.IsNullOrEmpty()) {
                    PackageManagerMessages.Invoke.Error("unable-to-acquire", "canonical-name", "canonical-name is required.");
                    return;
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
                Updated(); // notify threads that we're not going to be able to get that file.

                if (continuationTask != null) {
                    var state = continuationTask.AsyncState as RequestRemoteFileState;
                    if (state != null) {
                        state.LocalLocation = null;
                    }

                    // the task can run, 
                    continuationTask.Start();
                    return;
                }
            }, TaskCreationOptions.AttachedToParent);
            return t;
        }

        public Task RecognizeFile(string canonicalName, string localLocation, string remoteLocation, PackageManagerMessages messages) {
            var t = Task.Factory.StartNew(() => {
                if (messages != null) {
                    messages.Register();
                }
                if (string.IsNullOrEmpty(localLocation)) {
                    PackageManagerMessages.Invoke.Error("recognize-file", "local-location", "parameter 'local-location' is required to recognize a file");
                    return;
                }

                if (CancellationRequested) {
                    PackageManagerMessages.Invoke.OperationCancelled("recognize-file");
                    return;
                }


                var location = PackageManagerSession.Invoke.GetCanonicalizedPath(localLocation);
                if (location.StartsWith(@"\\")) {
                    // a local unc path was passed. This isn't allowed--we need a file on a local volume that
                    // the user has access to.
                    PackageManagerMessages.Invoke.Error("recognize-file", "local-location",
                        "local-location '{0}' appears to be a file on a remote server('{1}') . Recognized files must be local".format(localLocation, location));
                    return;
                }

                if (!File.Exists(location)) {
                    PackageManagerMessages.Invoke.FileNotFound(location);
                    return;
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
                        continuationTask.Start();
                        return;
                    }
                }

                // otherwise, we'll call the recognizer 
                Recognizer.Recognize(location).ContinueWith(antecedent => {
                    if (antecedent.IsFaulted) {
                        PackageManagerMessages.Invoke.FileNotRecognized(location, "Unexpected error recognizing file.");
                        return;
                    }

                    if (antecedent.Result.IsPackageFile) {
                        var package = Package.GetPackageFromFilename(location);
                        if (package != null) {
                            // mark it download 100%
                            package.PackageSessionData.DownloadProgress = 100;

                            SessionPackageFeed.Instance.Add(package);

                            PackageManagerMessages.Invoke.PackageInformation(package, Enumerable.Empty<Package>());
                            PackageManagerMessages.Invoke.Recognized(localLocation);
                        }
                        return;
                    }

                    if (antecedent.Result.IsPackageFeed) {
                        PackageManagerMessages.Invoke.FeedAdded(location);
                        PackageManagerMessages.Invoke.Recognized(location);
                    }

                    // if this isn't a package file, then there is something odd going on here.
                    // we don't accept non-package files willy-nilly. 
                    PackageManagerMessages.Invoke.FileNotRecognized(location, "File isn't a package, and doesn't appear to have been requested. ");
                    return;
                }, TaskContinuationOptions.AttachedToParent);

            }, TaskCreationOptions.AttachedToParent);
            return t;
        }

        public Task SuppressFeed(string location, PackageManagerMessages messages) {
            var t = Task.Factory.StartNew(() => {
                if (messages != null) {
                    messages.Register();
                }

                if (CancellationRequested) {
                    PackageManagerMessages.Invoke.OperationCancelled("suppress-feed");
                    return;
                }


                var suppressedFeeds = SessionCache<List<string>>.Value["suppressed-feeds"] ?? new List<string>();

                lock (suppressedFeeds) {
                    if (!suppressedFeeds.Contains(location)) {
                        suppressedFeeds.Add(location);
                        SessionCache<List<string>>.Value["suppressed-feeds"] = suppressedFeeds;
                    }
                }

                PackageManagerMessages.Invoke.FeedSuppressed(location);
            }, TaskCreationOptions.AttachedToParent);
            return t;
        }


        internal void Updated() {
            foreach (var mre in manualResetEvents) {
                mre.Set();
            }
        }

        private Package GetSinglePackage(string canonicalName, string messageName, bool suppressErrors = false) {
            // name != null?
            if( string.IsNullOrEmpty(canonicalName)) {
                if (messageName != null && !suppressErrors) {
                    PackageManagerMessages.Invoke.Error(messageName, "canonical-name",
                        "Canonical name '{0}' does not appear to be a valid canonical name".format(canonicalName));
                }
                return null;
            }

            // if canonical name is passed, override name,version,pkt,arch with the parsed canonicalname.
            var match = _canonicalNameParser.Match(canonicalName.ToLower());
            if( !match.Success ) {

                if (messageName != null && !suppressErrors) {
                    PackageManagerMessages.Invoke.Error(messageName, "canonical-name",
                        "Canonical name '{0}' does not appear to be a valid canonical name".format(canonicalName));
                }
                return null;
            }

            var pkg = SearchForPackages(match.Groups[1].Captures[0].Value, match.Groups[2].Captures[0].Value, match.Groups[3].Captures[0].Value,
                match.Groups[4].Captures[0].Value);

            if( !pkg.Any()) {
                if (messageName != null && !suppressErrors) {
                    PackageManagerMessages.Invoke.UnknownPackage(canonicalName);
                }
                return null;
            }

            if( pkg.Count() > 1 ) {
                if (messageName != null && !suppressErrors) {
                    PackageManagerMessages.Invoke.Error(messageName, "canonical-name",
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

                var canFilterSession = PackageManagerSession.Invoke.CheckForPermission(PermissionPolicy.EditSessionFeeds);
                var canFilterSystem = PackageManagerSession.Invoke.CheckForPermission(PermissionPolicy.EditSystemFeeds);
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
        /// <returns></returns>
        internal IEnumerable<Package> SearchForPackages(string name, string version, string arch, string publicKeyToken, string location = null) {
            var feeds = string.IsNullOrEmpty(location) ? Feeds : Feeds.Where(each => each.IsLocationMatch(location));
            return feeds.SelectMany(each => each.FindPackages(name, version, arch, publicKeyToken)).Distinct().ToArray();
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
        /// <returns></returns>
        private IEnumerable<Package> GenerateInstallGraph(Package package, bool hypothetical = false) {
            if (package.IsInstalled) {
                if (!package.PackageRequestData.NotifiedClientThisSupercedes) {
                    PackageManagerMessages.Invoke.PackageSatisfiedBy(package, package);
                    package.PackageRequestData.NotifiedClientThisSupercedes = true;
                }

                yield break;
            }

            var packageData = package.PackageSessionData;

            if (!package.PackageSessionData.IsPotentiallyInstallable) {
                yield break;
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
                        PackageManagerMessages.Invoke.PackageSatisfiedBy(package, installedSupercedent);
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
                                PackageManagerMessages.Invoke.PackageSatisfiedBy(package, supercedent);
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
                        PackageManagerMessages.Invoke.PackageHasPotentialUpgrades(package, supercedents);
                        throw new OperationCompletedBeforeResultException();
                    }
                }
            }

            if (packageData.CouldNotDownload) {
                if (!hypothetical) {
                    PackageManagerMessages.Invoke.UnableToDownloadPackage(package);
                }
                throw new OperationCompletedBeforeResultException();
            }

            if (packageData.PackageFailedInstall) {
                if (!hypothetical) {
                    PackageManagerMessages.Invoke.UnableToInstallPackage(package);
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
                    childrenFailed = true;
                    continue;
                }

                foreach (var child in children)
                    yield return child;
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
    }
}
