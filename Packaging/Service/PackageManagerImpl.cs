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
    using System.Linq.Expressions;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Xml.Linq;
    using Common;
    using Common.Model.Atom;
    using Feeds;
    using PackageFormatHandlers;
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

        static PackageManagerImpl() {
            // Serializer for System.Linq.Expressions.Expression
            CustomSerializer.Add(new CustomSerializer<Expression<Func<IPackage, bool>>>((message, key, serialize) => message.Add(key, CustomSerializer.ExpressionXmlSerializer.Serialize(serialize).ToString(SaveOptions.OmitDuplicateNamespaces | SaveOptions.DisableFormatting)), (message, key) => (Expression<Func<IPackage, bool>>)CustomSerializer.ExpressionXmlSerializer.Deserialize(message[key])));
            CustomSerializer.Add(new CustomSerializer<Expression<Func<IEnumerable<IPackage>, IEnumerable<IPackage>>>>((message, key, serialize) => message.Add(key, CustomSerializer.ExpressionXmlSerializer.Serialize(serialize).ToString(SaveOptions.OmitDuplicateNamespaces | SaveOptions.DisableFormatting)), (message, key) => (Expression<Func<IEnumerable<IPackage>, IEnumerable<IPackage>>>)CustomSerializer.ExpressionXmlSerializer.Deserialize(message[key])));
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
                    if (!PackageManagerSettings.PerFeedSettings.Subkeys.Any()) {
                        PackageManagerSettings.PerFeedSettings["http://coapp.org/current".UrlEncodeJustBackslashes(), "state"].SetEnumValue(FeedState.Active);
                        PackageManagerSettings.PerFeedSettings["http://coapp.org/archive".UrlEncodeJustBackslashes(), "state"].SetEnumValue(FeedState.Passive);
                        PackageManagerSettings.PerFeedSettings["http://coapp.org/unstable".UrlEncodeJustBackslashes(), "state"].SetEnumValue(FeedState.Ignored);
                    }
                }

                return PackageManagerSettings.PerFeedSettings.Subkeys.Select(each => each.UrlDecode());
            }
        }

        private IEnumerable<string> SessionFeedLocations {
            get {
                return SessionData.Current.SessionPackageFeeds.Keys;
            }
        }

        private void AddSessionFeed(string feedLocation) {
            lock (this) {
                if (!feedLocation.IsWebUri()) {
                    feedLocation = feedLocation.CanonicalizePathWithWildcards();
                }
                SessionData.Current.SessionPackageFeeds.GetOrAdd(feedLocation, ()=> null);
            }
        }

        private void AddSystemFeed(string feedLocation) {
            lock (this) {
                if( !feedLocation.IsWebUri()) {
                    feedLocation = feedLocation.CanonicalizePathWithWildcards();
                }

                PackageManagerSettings.PerFeedSettings[feedLocation.UrlEncodeJustBackslashes(), "state"].SetEnumValue(FeedState.Active);
            }
        }

        private void RemoveSessionFeed(string feedLocation) {
            lock (this) {
                if (!feedLocation.IsWebUri()) {
                    feedLocation = feedLocation.CanonicalizePathWithWildcards();
                }

                // remove it from the cached feeds
                SessionData.Current.SessionPackageFeeds.Remove(feedLocation);
            }
        }

        private void RemoveSystemFeed(string feedLocation) {
            lock (this) {
                if (!feedLocation.IsWebUri()) {
                    feedLocation = feedLocation.CanonicalizePathWithWildcards();
                }
                PackageManagerSettings.PerFeedSettings.DeleteSubkey(feedLocation.UrlEncodeJustBackslashes());

                // remove it from the cached feeds
                Cache<PackageFeed>.Value.Clear(feedLocation);
            }
        }

        internal void EnsureSystemFeedsAreLoaded() {
            // do a cheap check first (so that this session never gets blocked unnecessarily).

            if (SessionData.Current.IsSystemCacheLoaded) {
                return;
            }

            lock (this) {
                if (SessionData.Current.IsSystemCacheLoaded) {
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
                SessionData.Current.IsSystemCacheLoaded  = true;
            }
        }

        public Task FindPackages(CanonicalName canonicalName, Expression<Func<IPackage, bool>> filter, Expression<Func<IEnumerable<IPackage>, IEnumerable<IPackage>>> collectionFilter, string location) {
            var response = Event<GetResponseInterface>.RaiseFirst();

            if (CancellationRequested) {
                response.OperationCanceled("find-package");
                return FinishedSynchronously;
            }

            canonicalName = canonicalName ?? CanonicalName.CoAppPackages;

            if (Event<CheckForPermission>.RaiseFirst(PermissionPolicy.EnumeratePackages)) {
                UpdateDependencyFlags();

                IEnumerable<IPackage> query = filter == null ? SearchForPackages(canonicalName, location) : SearchForPackages(canonicalName, location).Where(each => filter.Compile()(each));

                if( collectionFilter != null ) {
                    query = collectionFilter.Compile()(query);
                }

                var results = (Package[]) query.ToArrayOfType(typeof(Package));
                
                if (results.Length > 0 ) {
                    foreach (var pkg in results) {
                        if (CancellationRequested) {
                            response.OperationCanceled("find-packages");
                            return FinishedSynchronously;
                        }
                        response.PackageInformation(pkg);
                    }
                }
                else {
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


            response.PackageDetails(package.CanonicalName, package.PackageDetails);
            return FinishedSynchronously;
        }

        public Task InstallPackage(CanonicalName canonicalName, bool? autoUpgrade, bool? force, bool? download, bool? pretend, CanonicalName replacingPackage) {
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

            var unwantedPackages = new List<Package>();
            if( null != replacingPackage ) {
                if( replacingPackage.DiffersOnlyByVersion(canonicalName)) {
                    unwantedPackages.AddRange(SearchForPackages(replacingPackage));
                }
            }

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

                    var installedPackages = package.InstalledPackages.ToArray();
                    

                    // is the user authorized to install this?
                    if (null != replacingPackage) {
                        if (replacingPackage.DiffersOnlyByVersion(canonicalName)) {
                            if (!Event<CheckForPermission>.RaiseFirst(PermissionPolicy.UpdatePackage)) {
                                return FinishedSynchronously;
                            }
                        }
                    } else {
                        if( package.LatestInstalledThatUpdatesToThis != null ) {
                            if (!Event<CheckForPermission>.RaiseFirst(PermissionPolicy.UpdatePackage)) {
                                return FinishedSynchronously;
                            }
                        } else {
                            if (!Event<CheckForPermission>.RaiseFirst(PermissionPolicy.InstallPackage)) {
                                return FinishedSynchronously;
                            }
                        }
                    }
                   

                    // if this is an explicit update or upgrade, 
                    //      - check to see if there is a compatible package already installed that is marked do-not-update
                    //        fail if so.
                    if (null != replacingPackage && unwantedPackages.Any( each => each.IsBlocked )) {
                        response.PackageBlocked(canonicalName);
                        return FinishedSynchronously;
                    }


                    // mark the package as the client requested.
                    package.PackageSessionData.DoNotSupercede = (false == autoUpgrade);
                    package.PackageSessionData.UpgradeAsNeeded = (true == autoUpgrade);
                    package.PackageSessionData.IsWanted = true;

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
                            UpdateDependencyFlags();
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
                                response.PackageInformation(p);
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
                                SessionData.Current.RequireRemoteFile(p.CanonicalName,
                                    p.RemotePackageLocations, PackageManagerSettings.CoAppPackageCache, false,(rrfState) => {
                                        Updated(); //shake loose anything that might be waiting for this.
                                        return rrfState.LocalLocation;
                                    });

                                p.PackageSessionData.HasRequestedDownload = true;
                            }
                        } else {
                            // check to see if this package requires trust.
                            bool ok = true;
                            foreach (var pkg in installGraph.Where(pkg => pkg.RequiresTrustedPublisher && !TrustedPublishers.ContainsIgnoreCase(pkg.PublicKeyToken))) {
                                response.FailedPackageInstall(pkg.CanonicalName, pkg.LocalLocations.FirstOrDefault(), "Package requires a trusted publisher key of '{0}'.".format(pkg.PublicKeyToken));
                                ok = false;
                            }
                            if( ok == false) {
                                return FinishedSynchronously;
                            }

                            if (pretend == true) {
                                // we can just return a bunch of found-package messages, since we're not going to be 
                                // actually installing anything, and everything we needed is downloaded.
                                foreach (var p in installGraph) {
                                    response.PackageInformation(p);
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
                                if( unwantedPackages.Any()) {
                                    foreach (Package eachPkg in unwantedPackages) {
                                        eachPkg.IsWanted = false;
                                    }
                                } else {
                                    var olderpkgs = package.InstalledPackages.Where(each => each.IsWanted && package.IsNewerThan(each)).ToArray();
                                    if( olderpkgs.Length > 0 ) {
                                        //anthing older? 

                                        if( olderpkgs.Length > 1) {
                                            // hmm. more than one.
                                            // is there just a single thing we're updating?
                                            olderpkgs = olderpkgs.Where(package.IsAnUpdateFor).ToArray();
                                        }

                                        // if we can get down to one, let's unwant that.
                                        if (olderpkgs.Length == 1) {
                                            ((Package)olderpkgs[0]).IsWanted = false;
                                        } 
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
                
                var cn = new CanonicalName(requestReference);

                if (null != cn && cn.IsCanonical) {
                    var package = SearchForPackages(cn).FirstOrDefault();
                    if (package != null) {
                        package.PackageSessionData.DownloadProgress = Math.Max(package.PackageSessionData.DownloadProgress, downloadProgress.GetValueOrDefault());
                    }
                }
            } catch {
                // suppress any exceptions... we just don't care!
            }
            return FinishedSynchronously;
        }

        public Task ListFeeds() {
            var response = Event<GetResponseInterface>.RaiseFirst();

            if (CancellationRequested) {
                response.OperationCanceled("list-feeds");
                return FinishedSynchronously;
            }
            EnsureSystemFeedsAreLoaded();

            var canFilterSession = Event<QueryPermission>.RaiseFirst(PermissionPolicy.EditSessionFeeds);
            var canFilterSystem = Event<QueryPermission>.RaiseFirst(PermissionPolicy.EditSystemFeeds);

            var activeSessionFeeds = SessionData.Current.SessionPackageFeeds;
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
                    feedstate = PackageManagerSettings.PerPackageSettings[feedLocation, "state"].GetEnumValue<FeedState>()
                };

            var y = from feedLocation in SessionFeedLocations
                let theFeed = activeSessionFeeds[feedLocation]
                let validated = theFeed != null
                select new {
                    feed = feedLocation,
                    LastScanned = validated ? theFeed.LastScanned : DateTime.MinValue,
                    session = true,
                    suppressed = canFilterSession && BlockedScanLocations.Contains(feedLocation),
                    validated,
                    feedstate = FeedState.Active
                };

            var results = x.Union(y).ToArray();
           
            if (results.Length > 0 ) {
                foreach (var f in results) {
                    response.FeedDetails(f.feed, f.LastScanned, f.session, f.suppressed, f.validated, f.feedstate);
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
                                SessionData.Current.SessionPackageFeeds[location] = foundFeed;
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
                    UpdateDependencyFlags();
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

            var continuationTask = SessionData.Current.RequestedFileTasks.GetAndRemove(requestReference);

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

        public Task RecognizeFiles(IEnumerable<string> localLocations) {
            Parallel.ForEach(localLocations, each => RecognizeFile(null, each, null));
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
                var continuationTask = SessionData.Current.RequestedFileTasks.GetAndRemove(requestReference);

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
            Logger.Message("Calling Recognizer with {0}", location);
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

                        response.PackageInformation(package);
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

        public Task SetFeedFlags(string location, FeedState feedState) {
            var response = Event<GetResponseInterface>.RaiseFirst();
            
            if (CancellationRequested) {
                response.OperationCanceled("set-feed");
                return FinishedSynchronously;
            }

            if (Event<CheckForPermission>.RaiseFirst(PermissionPolicy.EditSystemFeeds)) {
                PackageManagerSettings.PerPackageSettings[location, "state"].SetEnumValue(feedState);
            }
            return FinishedSynchronously;
        }

        public Task SuppressFeed(string location) {
            var response = Event<GetResponseInterface>.RaiseFirst();
            
            if (CancellationRequested) {
                response.OperationCanceled("suppress-feed");
                return FinishedSynchronously;
            }

            

            lock (SessionData.Current) {
                SessionData.Current.SuppressedFeeds = SessionData.Current.SuppressedFeeds.UnionSingleItem(location).Distinct();
            }

            response.FeedSuppressed(location);
            return FinishedSynchronously;
        }

        internal void Updated() {
            foreach (var mre in _manualResetEvents) {
                mre.Set();
            }
        }

        internal IEnumerable<string> BlockedScanLocations {
            get {
                return SessionData.Current.SuppressedFeeds;
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
                        return new PackageFeed[] { SessionPackageFeed.Instance, InstalledPackageFeed.Instance}.Union(Cache<PackageFeed>.Value.Values).Union(SessionData.Current.SessionPackageFeeds.Values).ToArray();
                    }
                    return new PackageFeed[] {SessionPackageFeed.Instance, InstalledPackageFeed.Instance}
                        .Union(from feed in Cache<PackageFeed>.Value.Values where !canFilterSystem || !feed.IsLocationMatch(filters) select feed)
                        .Union(from feed in SessionData.Current.SessionPackageFeeds.Values  where !canFilterSession || !feed.IsLocationMatch(filters) select feed)
                        .ToArray();
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
        internal IEnumerable<Package> SearchForPackages(CanonicalName canonicalName, string location = null, bool lookDeep = false) {
            try {
                lock (this) {
                    if (!string.IsNullOrEmpty(location) ) {
                        // asking a specific feed. just use that one.
                        var feed = Feeds.FirstOrDefault(each => each.IsLocationMatch(location));
                        if( feed == null ) {
                            return Enumerable.Empty<Package>();
                        }
                        return feed.FindPackages(canonicalName).Distinct().ToArray();
                    }

                    var feeds = Feeds.Where(each => (lookDeep & each.FeedState == FeedState.Passive) || each.FeedState == FeedState.Active).ToArray();

                    if( canonicalName.IsCanonical || lookDeep == false) {
                        // they are not asking for more than one version of a given package.
                        // or we're just not interested in going deep.
                        var result = feeds.SelectMany(each => each.FindPackages(canonicalName)).Distinct().ToArray();
                        if (result.Length != 0 || lookDeep == false) { 
                            return result;
                        }
                    }

                    // we're either searching for more than a single package, or we didn't find what we were looking for,
                    
                    var feedLocations = feeds.Select(each => each.Location).ToArray();
                    var packages = feeds.SelectMany(each => each.FindPackages(canonicalName)).Distinct().ToArray();

                    var otherFeeds = packages.SelectMany(each => each.FeedLocations).Distinct().Where(each => !feedLocations.Contains(each.AbsoluteUri));
                    // given a list of other feeds that we're not using, we can search each of those feeds for newer versions of the packages that we already have.
                    var tf = TransientFeeds(otherFeeds, lookDeep);
                    return packages.Union(packages.SelectMany(p => tf.SelectMany(each => each.FindPackages(p.CanonicalName.OtherVersionFilter)))).Distinct().ToArray();
                }
            } catch (InvalidOperationException) {
                // this can happen if the collection changes during the operation (and can actually happen in the middle of .ToArray() 
                // since, locking the hell out of the collections isn't worth the effort, we'll just try again on this type of exception
                // and pray the collection won't keep changing :)
                Logger.Message("PERF HIT [REPORT THIS IF THIS IS CONSISTENT!]: Rerunning SearchForPackages!");
                return SearchForPackages(canonicalName, location, lookDeep);
            }
        }

        /// <summary>
        ///   This returns an collection of feed objects when given a list of feed locations. The items are cached in the session, so if mutliple calls ask for repeat items, it's not creating new objects all the time.
        /// </summary>
        /// <param name="locations"> List of feed locations </param>
        /// <returns> </returns>
        internal IEnumerable<PackageFeed> TransientFeeds(IEnumerable<Uri> locations, bool lookDeep) {
            var locs = locations.ToArray();
            var tf = SessionData.Current.TransientFeeds;

            var existingLocations = tf.Select(each => each.Location);
            var newLocations = locs.Where(each => !existingLocations.Contains(each.AbsoluteUri));
            var tasks = newLocations.Select(each => PackageFeed.GetPackageFeedFromLocation(each.AbsoluteUri )).ToArray();
            var newFeeds = tasks.Where(each => each.Result != null).Select(each => each.Result);
            tf.AddRange(newFeeds);
            return tf.Where(each => locs.Contains(each.Location.ToUri()) && (each.FeedState == FeedState.Active || (each.FeedState == FeedState.Passive && lookDeep)));
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
                IEnumerable<IPackage> installedSupercedents;

                if (package.PackageSessionData.IsWanted || hypothetical) {
                    // this means that we're talking about a requested package
                    // and not a dependent package and we can liberally construe supercedent 
                    // as anything with a highger version number
                    installedSupercedents = (from p in package.InstalledPackages where p.CanonicalName.Version > package.CanonicalName.Version select p).OrderByDescending(p => p.CanonicalName.Version).ToArray();
                } else {
                    // otherwise, we're installing a dependency, and we need something compatable.
                    installedSupercedents = (from p in package.InstalledPackages where p.IsAnUpdateFor(package) select p).OrderByDescending(p => p.CanonicalName.Version).ToArray();
                }
                var installedSupercedent = installedSupercedents.FirstOrDefault();
                if (installedSupercedent != null) {
                    if (!(installedSupercedent as Package).PackageRequestData.NotifiedClientThisSupercedes) {
                        response.PackageSatisfiedBy(package.CanonicalName, installedSupercedent.CanonicalName);
                        (installedSupercedent as Package).PackageRequestData.NotifiedClientThisSupercedes = true;
                    }
                    yield break; // a supercedent package is already installed.
                }

                // if told not to supercede, we won't even perform this check 
                packageData.Supercedent = null;

                var supercedents = SearchForPackages(package.CanonicalName.OtherVersionFilter).ToArray();

                if (package.PackageSessionData.IsWanted || hypothetical) {
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
                                supercedent.PackageSessionData.IsWanted = package.PackageSessionData.IsWanted;
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
            foreach (var d in package.PackageDependencies) {
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

        private void UpdateDependencyFlags() {
            lock (this) {
                var installedPackages = InstalledPackages.ToArray();

                foreach (var p in installedPackages) {
                    p.PackageSessionData.IsDependency = false;
                }

                foreach (var package in installedPackages.Where(each => each.IsWanted)) {
                    package.MarkDependenciesAsDepenency();
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
                SessionData.Current.LoggingMessages = messages.Value;
            }

            if (errors.HasValue) {
                SessionData.Current.LoggingErrors= errors.Value;
            }

            if (warnings.HasValue) {
                SessionData.Current.LoggingWarnings= warnings.Value;
            }
            response.LoggingSettings(Logger.Messages, Logger.Warnings, Logger.Errors);
            return FinishedSynchronously;
        }

        public Task SetGeneralPackageInformation(int priority, CanonicalName canonicalName, string key, string value){
            GeneralPackageSettings.Instance[priority, canonicalName, key] = value;
            return FinishedSynchronously;
        }

        public Task GetGeneralPackageInformation() {
            GeneralPackageSettings.Instance.GetSettingsData(Event<GetResponseInterface>.RaiseFirst());
            return FinishedSynchronously;
        }

        public Task SetPackageWanted(CanonicalName canonicalName, bool wanted) {
            Package.GetPackage(canonicalName).IsWanted = wanted;
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

        public Task AddTrustedPublisher(string publisherKeyToken) {
            if (Event<CheckForPermission>.RaiseFirst(PermissionPolicy.ModifyPolicy) && publisherKeyToken != null && publisherKeyToken.Length == 16) {
                TrustedPublishers = TrustedPublishers.UnionSingleItem(publisherKeyToken).Distinct().ToArray();    
            }
            return FinishedSynchronously;
        }

        public Task RemoveTrustedPublisher(string publisherKeyToken) {
            if (Event<CheckForPermission>.RaiseFirst(PermissionPolicy.ModifyPolicy) && publisherKeyToken !=null && publisherKeyToken.Length == 16) {
                TrustedPublishers = TrustedPublishers.Where(each => !each.Equals(publisherKeyToken, StringComparison.CurrentCultureIgnoreCase)).Distinct().ToArray();
            }
            return FinishedSynchronously;
        }

        public Task GetTrustedPublishers() {
            Event<GetResponseInterface>.RaiseFirst().TrustedPublishers(TrustedPublishers);
            return FinishedSynchronously;
        }

        internal string[] TrustedPublishers {
            get {
                lock (this) {
                    var tp = PackageManagerSettings.CoAppSettings["#TrustedPublishers"].EncryptedStringsValue;
                    return tp.Length == 0 ? new []{CanonicalName.CoAppItself.PublicKeyToken} : tp;
                }
            }
            set {
                lock (this) {
                    PackageManagerSettings.CoAppSettings["#TrustedPublishers"].EncryptedStringsValue = value;
                }
            }
        }

        public Task GetTelemetry() {
            Event<GetResponseInterface>.RaiseFirst().CurrentTelemetryOption(PackageManagerSettings.CoAppSettings["#TelemetryOptIn"].BoolValue);
            return FinishedSynchronously;
        }

        public Task SetTelemetry(bool optin) {
            if (Event<CheckForPermission>.RaiseFirst(PermissionPolicy.ModifyPolicy)) {
                PackageManagerSettings.CoAppSettings["#TelemetryOptIn"].BoolValue = optin;
            }
            return FinishedSynchronously;
        }

        public Task GetAtomFeed(IEnumerable<CanonicalName> canonicalNames) {
            var response = Event<GetResponseInterface>.RaiseFirst();
            var feed = new AtomFeed();
            var pkgs = canonicalNames.Select(each => SearchForPackages(each).FirstOrDefault()).Where(each => null != each);
            feed.Add(pkgs);
            response.AtomFeedText(feed.ToString());
            return FinishedSynchronously;
        }
    }
}