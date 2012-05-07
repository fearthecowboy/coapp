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

namespace CoApp.Packaging.Client {
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using Common;
    using Common.Exceptions;
    using Toolkit.Extensions;
    using Toolkit.Logging;
    using Toolkit.Tasks;
    using Toolkit.Win32;

    public class PackageManager {
        private static readonly IPackageManager Remote = Session.RemoteService;

        #region FlexibleGetPackages
        public Task<IEnumerable<Package>> QueryPackages(string query, ulong? minVersion = null, ulong? maxVersion = null,
            bool? dependencies = null, bool? installed = null, bool? active = null, bool? required = null, bool? blocked = null, bool? latest = null,
            string location = null, bool? forceScan = null, bool? updates = null, bool? upgrades = null, bool? trimable = null) {

            string localPath = null;

            // there are three possibilities:
            // 1. The parameter is a file on disk, or a remote file.
            try {
                // some kind of local file?
                if( File.Exists(query) ) {
                    return PackagesFromLocalFile(query.EnsureFileIsLocal(), Path.GetDirectoryName(query.GetFullPath()));
                }
                Task.Factory.StartNew(() => {
                    var uri = new Uri(query);
                    var lp = uri.AbsolutePath.MakeSafeFileName().GenerateTemporaryFilename();
                    var remoteFile = new RemoteFile(uri, localPath, itemUri => {
                        Event<DownloadCompleted>.Raise(uri.AbsolutePath, lp); // got the file!
                        localPath = lp;
                    },
                        itemUri => {
                            localPath = null; // failed
                        },
                        (itemUri, percent) => Event<DownloadProgress>.Raise(uri.AbsolutePath, localPath, percent));
                    remoteFile.Get();

                    if (localPath != null) {
                        return PackagesFromLocalFile(localPath, null, minVersion, maxVersion, dependencies, installed, active, required, blocked, latest, location, forceScan, updates, upgrades, trimable).Result;
                    }
                    return Enumerable.Empty<Package>();
                });
            }
            catch {
                // ignore what can't be fixed
            }

            // 2. A directory path or filemask (ie, creating a one-time 'feed' )
            if (Directory.Exists(query) || query.IndexOf('\\') > -1 || query.IndexOf('/') > -1 || (query.IndexOf('*') > -1 && query.ToLower().EndsWith(".msi"))) {
                // specified a folder, or some kind of path that looks like a feed.
                // add it as a feed, and then get the contents of that feed.
                return (Remote.AddFeed(query, true) as Task<PackageManagerResponseImpl>).Continue(response => {
                    // a feed!
                    if (response.Feeds.Any()) {
                        var task = Remote.FindPackages(null, dependencies, installed, active, required, blocked, latest, null, null, response.Feeds.First().Location, forceScan, updates, upgrades, trimable) as Task<PackageManagerResponseImpl>;
                        return task.Continue(response1 =>response1.Packages.Where(package => (!((FourPartVersion?)minVersion).HasValue || package.Version >= (FourPartVersion?)minVersion) && (!((FourPartVersion?)maxVersion).HasValue || package.Version <= (FourPartVersion?)maxVersion))).Result;
                    }

                    query = query.GetFullPath();
                    return (Remote.AddFeed(query, true) as Task<PackageManagerResponseImpl>).Continue(response2 => {
                        if (response.Feeds.Any()) {
                            var task = Remote.FindPackages(null, dependencies, installed, active, required, blocked, latest, null, null, response2.Feeds.First().Location, forceScan, updates, upgrades, trimable) as Task<PackageManagerResponseImpl>;
                            return task.Continue( response1 => response1.Packages.Where( package => (!((FourPartVersion?)minVersion).HasValue || package.Version >= (FourPartVersion?)minVersion) && (!((FourPartVersion?)maxVersion).HasValue || package.Version <= (FourPartVersion?)maxVersion))).Result;
                        }
                        return Enumerable.Empty<Package>();
                    }).Result;
                });
            }

            // 3. A partial/canonical name of a package.
            return (Remote.FindPackages(query, dependencies, installed, active, required, blocked, latest, null, null, location, forceScan, updates, upgrades, trimable) as Task<PackageManagerResponseImpl>).Continue(response =>
                response.Packages.Where(package => (!((FourPartVersion?)minVersion).HasValue || package.Version >= (FourPartVersion?)minVersion) && (!((FourPartVersion?)maxVersion).HasValue || package.Version <= (FourPartVersion?)maxVersion)));
        }

        public Task<IEnumerable<Package>> QueryPackages(IEnumerable<string> queries, ulong? minVersion = null, ulong? maxVersion = null,
            bool? dependencies = null, bool? installed = null, bool? active = null, bool? required = null, bool? blocked = null, bool? latest = null,
            string location = null, bool? forceScan = null, bool? updates = null, bool? upgrades = null, bool? trimable = null) {
            if( queries.Any()) {
                return queries.Select(each => QueryPackages(each, minVersion, maxVersion, dependencies, installed, active, required, blocked, latest, location, forceScan, updates, upgrades, trimable)).Continue(results => results.SelectMany(result => result).Distinct());    
            }
            return QueryPackages("*", minVersion, maxVersion, dependencies, installed, active, required, blocked, latest, location, forceScan, updates, upgrades, trimable);
        }

        private Task<IEnumerable<Package>> PackagesFromLocalFile(string localPath, string originalDirectory, ulong? minVersion = null, ulong? maxVersion = null,
            bool? dependencies = null, bool? installed = null, bool? active = null, bool? required = null, bool? blocked = null, bool? latest = null,
            string location = null, bool? forceScan = null, bool? updates = null, bool? upgrades = null, bool? trimable = null ) {
            if( localPath.FileIsLocalAndExists()) {
                return (Remote.RecognizeFile("", localPath, "") as Task<PackageManagerResponseImpl>).Continue(response => {
                    // was that one or more package(s)?
                    if (response.Packages.Any()) {
                        if (!string.IsNullOrEmpty(originalDirectory)) {
                            Remote.AddFeed(originalDirectory, true);
                        }
                        return response.Packages;
                    }

                    // was that a feed?
                    if (response.Feeds.Any()) {
                        return (Remote.FindPackages(null, dependencies, installed, active, required, blocked, latest, null, null, response.Feeds.First().Location, forceScan, updates, upgrades, trimable) as Task<PackageManagerResponseImpl>).Continue(
                            response1 => response1.Packages.Where(package => (!((FourPartVersion?)minVersion).HasValue || package.Version >= (FourPartVersion?)minVersion) && (!((FourPartVersion?)maxVersion).HasValue || package.Version <= (FourPartVersion?)maxVersion))).Result;
                    }


                    // nothing we could recognize.
                    return Enumerable.Empty<Package>();
                });
            }
            return Enumerable.Empty<Package>().AsResultTask();
        }
        

#if old

        public Task<IEnumerable<Package>> GetPackagesEx(IEnumerable<string> parameters, ulong? minVersion = null, ulong? maxVersion = null,
            bool? dependencies = null, bool? installed = null, bool? active = null, bool? required = null, bool? blocked = null, bool? latest = null,
            string location = null, bool? forceScan = null, bool? updates = null, bool? upgrades = null, bool? trimable = null) {
            var p = parameters.ToArray();
            if (p.IsNullOrEmpty()) {
                return GetPackagesEx(string.Empty, minVersion, maxVersion, dependencies, installed, active, required, blocked, latest, location, forceScan, updates, upgrades, trimable);
            }

            // spawn the tasks off in parallel
            var tasks = p.Select(each => GetPackagesEx(each, minVersion, maxVersion, dependencies, installed, active, required, blocked, latest, location, forceScan, updates, upgrades, trimable)).ToArray();

            // return a task that is the sum of all the tasks.
            return tasks.Continue(packages => packages.SelectMany(each => each).Distinct());
        }

        public Task<IEnumerable<Package>> GetPackagesEx(string parameter, ulong? minVersion = null, ulong? maxVersion = null, bool? dependencies = null, bool? installed = null, bool? active = null, bool? required = null, bool? blocked = null,
            bool? latest = null, string location = null, bool? forceScan = null, bool? updates = null, bool? upgrades = null, bool? trimable = null) {
            if (parameter.IsNullOrEmpty()) {
                return (Remote.FindPackages(null, dependencies, installed, active, required, blocked, latest, null, null, location, forceScan, updates, upgrades, trimable) as Task<PackageManagerResponseImpl>).Continue(response => response.Packages);
            }

            if (File.Exists(parameter)) {
                var localPath = parameter.EnsureFileIsLocal();
                var originalDirectory = Path.GetDirectoryName(parameter.GetFullPath());
                // add the directory it came from as a session package feed

                if (!string.IsNullOrEmpty(localPath)) {
                    return (Remote.RecognizeFile(null, localPath, null) as Task<PackageManagerResponseImpl>).Continue(response => {
                        // a Package!
                        if (response.Packages.Any()) {
                            Remote.AddFeed(originalDirectory, true);
                            return response.Packages;
                        }

                        // a feed!
                        if (response.Feeds.Any()) {
                            return (Remote.FindPackages(null, dependencies, installed, active, required, blocked, latest, null, null, response.Feeds.First().Location, forceScan, updates, upgrades, trimable) as Task<PackageManagerResponseImpl>).Continue(
                                response1 =>
                                    response1.Packages.Where(
                                        package => (!((FourPartVersion?)minVersion).HasValue || package.Version >= (FourPartVersion?)minVersion) && (!((FourPartVersion?)maxVersion).HasValue || package.Version <= (FourPartVersion?)maxVersion))).Result;
                        }

                        // nothing I could discern.
                        return Enumerable.Empty<Package>();
                    });
                }
                // if we don't get back a local path for the file... this is pretty odd. DUnno what we should really do here yet.
                return Enumerable.Empty<Package>().AsResultTask();
            }

            if (Directory.Exists(parameter) || parameter.IndexOf('\\') > -1 || parameter.IndexOf('/') > -1 ||
                (parameter.IndexOf('*') > -1 && parameter.ToLower().EndsWith(".msi"))) {
                // specified a folder, or some kind of path that looks like a feed.
                // add it as a feed, and then get the contents of that feed.
                return (Remote.AddFeed(parameter, true) as Task<PackageManagerResponseImpl>).Continue(response => {
                    // a feed!
                    if (response.Feeds.Any()) {
                        var task = Remote.FindPackages(null, dependencies, installed, active, required, blocked, latest, null, null, response.Feeds.First().Location, forceScan, updates, upgrades, trimable) as Task<PackageManagerResponseImpl>;
                        return
                            task.Continue(
                                response1 =>
                                    response1.Packages.Where(
                                        package => (!((FourPartVersion?)minVersion).HasValue || package.Version >= (FourPartVersion?)minVersion) && (!((FourPartVersion?)maxVersion).HasValue || package.Version <= (FourPartVersion?)maxVersion))).Result;
                    }

                    parameter = parameter.GetFullPath();
                    return (Remote.AddFeed(parameter, true) as Task<PackageManagerResponseImpl>).Continue(response2 => {
                        if (response.Feeds.Any()) {
                            var task = Remote.FindPackages(null, dependencies, installed, active, required, blocked, latest, null, null, response2.Feeds.First().Location, forceScan, updates, upgrades, trimable) as Task<PackageManagerResponseImpl>;
                            return
                                task.Continue(
                                    response1 =>
                                        response1.Packages.Where(
                                            package => (!((FourPartVersion?)minVersion).HasValue || package.Version >= (FourPartVersion?)minVersion) && (!((FourPartVersion?)maxVersion).HasValue || package.Version <= (FourPartVersion?)maxVersion))).Result;
                        }
                        return Enumerable.Empty<Package>();
                    }).Result;
                });
            }

            // can only be a canonical name match, proceed with that.            
            return (Remote.FindPackages(parameter, dependencies, installed, active, required, blocked, latest, null, null, location, forceScan, updates, upgrades, trimable) as Task<PackageManagerResponseImpl>).Continue(response =>
                response.Packages.Where(package => (!((FourPartVersion?)minVersion).HasValue || package.Version >= (FourPartVersion?)minVersion) && (!((FourPartVersion?)maxVersion).HasValue || package.Version <= (FourPartVersion?)maxVersion)));
        }
#endif
        #endregion

        /// <summary>
        ///   Returns a collection of packages that are filtered to the platform switches passed on the command line.
        /// </summary>
        /// <param name="packages"> The collection to filter packages for </param>
        /// <param name="x86"> Accept x86 packages? </param>
        /// <param name="x64"> Accept x64 packages? </param>
        /// <param name="cpuany"> Accept CPUANY packages? </param>
        /// <returns> </returns>
        public IEnumerable<Package> FilterPackagesForPlatforms(IEnumerable<Package> packages, bool? x86 = null, bool? x64 = null, bool? cpuany = null) {
            if ((true == x64) || (true == x86) || (true == cpuany)) {
                return packages.Where(each => (x64 == true && each.Architecture == Architecture.x64) || (x86 == true && each.Architecture == Architecture.x86) || (cpuany == true && each.Architecture == Architecture.Any));
            }
            return packages;
        }

        /// <summary>
        ///   makes sure that there isn't a conflict in the packages that are being installed. This will call FilterPackagesForPlatforms, so you don't need to call that beforehand.
        /// </summary>
        /// <param name="packages"> the collection of packages to check for conflicts </param>
        /// <param name="x86"> </param>
        /// <param name="x64"> </param>
        /// <param name="cpuany"> </param>
        /// <returns> A filtered collection of packages to install </returns>
        public Task<IEnumerable<Package>> FilterConflictsForInstall(IEnumerable<Package> packages, bool? x86 = null, bool? x64 = null, bool? cpuany = null) {
            return Task.Factory.StartNew(() => {
                var isFiltering = (true == x64) || (true == x86) || (true == cpuany);

                var pkgs = FilterPackagesForPlatforms(packages, x86, x64, cpuany).Distinct().ToArray();

                // get an collection for each package of all the other packages that it matches.
                var packageFamilies = pkgs.Select(package => pkgs.Where(each => each.Name == package.Name && each.PublicKeyToken == package.PublicKeyToken && (!isFiltering || each.Architecture == package.Architecture)).ToArray()).ToArray();

                // get the packages that could be conflicting.
                var conflictedFamilies = packageFamilies.Where(eachFamily => eachFamily.Count() > 1);

                if (conflictedFamilies.Any()) {
                    var nonConflictedPackages = packageFamilies.Where(eachFamily => eachFamily.Count() == 1).Select(eachFamily => eachFamily.FirstOrDefault());

                    if (!isFiltering) {
                        var actualConflicts = new List<Package[]>();
                        foreach (var conflictedPackages in conflictedFamilies) {
                            // we're really only interested in one platform for a given package here (since the user didn't specify any preference directly)

                            if (Environment.Is64BitOperatingSystem) {
                                // if there is a single x64 package, take that.
                                var x64Pkgs = conflictedPackages.Where(each => each.Architecture == Architecture.x64).ToArray();
                                if (x64Pkgs.Count() == 1) {
                                    nonConflictedPackages = nonConflictedPackages.Union(x64Pkgs);
                                    continue;
                                }

                                if (x64Pkgs.Count() > 1) {
                                    // we've got more than one package of a single platform. just add it to the conflicts and move along.
                                    actualConflicts.Add(conflictedPackages);
                                    continue;
                                }
                            }

                            // if there are no x64 packages, see if there is a single x86 package, take that.
                            var x86Pkgs = conflictedPackages.Where(each => each.Architecture == Architecture.x86).ToArray();
                            if (x86Pkgs.Length == 1) {
                                nonConflictedPackages = nonConflictedPackages.Union(x86Pkgs);
                                continue;
                            }

                            // if we got here, that means no x64  and no x86 packages, and we should just have a plurality of cpuany packages.
                            // we've got more than one package of a single platform, or that means no x64  and no x86 packages, 
                            // and we just have a plurality of cpuany packages.

                            // Either way just add it to the conflicts and move along.
                            actualConflicts.Add(conflictedPackages);
                        }
                        if (actualConflicts.Any()) {
                            throw new ConflictedPackagesException(actualConflicts);
                        }

                        // hmm, we resolved our conflicts automagically.
                        return nonConflictedPackages;
                    }
                    // architectures are not factored in here. 
                    // we must have multiple packages of a given architecture.
                    throw new ConflictedPackagesException(conflictedFamilies);
                }

                return pkgs;
            }, TaskCreationOptions.AttachedToParent);
        }

        /// <summary>
        ///   This identifies the actual list of packages to install for a given requested set of packages
        /// </summary>
        /// <param name="packages"> </param>
        /// <param name="autoUpgrade"> </param>
        /// <param name="download"> </param>
        /// <returns> </returns>
        public Task<IEnumerable<Package>> IdentifyPackageAndDependenciesToInstall(IEnumerable<Package> packages, bool? autoUpgrade = null, bool? download = null) {
            var pTasks = packages.Select(package => Install(package.CanonicalName, autoUpgrade, pretend: true, download: download)).ToArray();

            return pTasks.ContinueAlways(antecedentTasks => {
                antecedentTasks.RethrowWhenFaulted();
                return antecedentTasks.SelectMany(each => each.Result).Distinct();
            });
        }

        public Task Elevate() {
            return Task.Factory.StartNew(
                () => {
                });
        }

        public Task<bool> VerifyFileSignature(string filename) {
            // make the remote call via the interface.
            return (Remote.VerifyFileSignature(filename) as Task<PackageManagerResponseImpl>).Continue(response => response.IsSignatureValid);
        }

        public Task<Package> GetLatestInstalledVersion(CanonicalName canonicalName) {
            return GetInstalledPackages(canonicalName).Continue(packages => packages.OrderByDescending(each => each.Version).FirstOrDefault());
        }

        public Task<PackageSet> GetPackageSet(CanonicalName canonicalName) {
            var result = new PackageSet();

            return GetPackage(canonicalName).Continue(package => {
                var tasks = new List<Task>();
                // the given package.
                result.Package = package;

                // get all the related packages
                var allPackages = GetAllVersionsOfPackage(canonicalName).Result.OrderByDescending(each => each.Version);

                result.InstalledPackages = allPackages.Where(each => each.IsInstalled).ToArray();
                result.InstalledNewerCompatable = NewestCompatablePackageIn(result.Package, result.InstalledPackages);
                result.InstalledNewer = result.InstalledPackages.FirstOrDefault(each => each.Version > result.Package.Version);

                result.InstalledNewest = result.InstalledPackages.FirstOrDefault();

                var notInstalledPackges = allPackages.Where(each => !each.IsInstalled).ToArray();
                result.AvailableNewerCompatible = NewestCompatablePackageIn(result.Package, notInstalledPackges);
                result.AvailableNewer = notInstalledPackges.FirstOrDefault(each => each.Version > result.Package.Version);

                result.InstalledOlderCompatable =
                    result.InstalledPackages.FirstOrDefault(
                        each => each.Version < result.Package.Version && result.Package.MinPolicy <= each.Version && result.Package.MaxPolicy >= each.Version);

                result.InstalledOlder =
                    result.InstalledPackages.FirstOrDefault(
                        each => each.Version < result.Package.Version && (result.Package.MinPolicy > each.Version || result.Package.MaxPolicy < each.Version));

                if (result.AvailableNewerCompatible == result.Package) {
                    result.AvailableNewerCompatible = null;
                }
                if (result.AvailableNewer == result.Package) {
                    result.AvailableNewer = null;
                }
                if (result.InstalledNewerCompatable == result.Package) {
                    result.InstalledNewerCompatable = null;
                }
                if (result.InstalledOlderCompatable == result.Package) {
                    result.InstalledOlderCompatable = null;
                }
                if (result.InstalledOlder == result.Package) {
                    result.InstalledOlder = null;
                }
                if (result.InstalledNewer == result.Package) {
                    result.InstalledNewer = null;
                }

                tasks.Add(GetTrimablePackages(canonicalName).ContinueWith(a2 => {
                    result.Trimable = !a2.IsFaulted ? a2.Result : Enumerable.Empty<Package>();
                }));

                if (result.InstalledNewer != null) {
                    tasks.Add(GetPackageDetails(result.InstalledNewer));
                }

                if (result.InstalledNewest != null) {
                    tasks.Add(GetPackageDetails(result.InstalledNewest));
                }

                if (result.InstalledNewerCompatable != null) {
                    tasks.Add(GetPackageDetails(result.InstalledNewerCompatable));
                }

                if (result.InstalledNewest != null) {
                    tasks.Add(GetPackageDetails(result.InstalledNewest));
                }

                if (result.InstalledOlder != null) {
                    tasks.Add(GetPackageDetails(result.InstalledOlder));
                }

                if (result.InstalledOlderCompatable != null) {
                    tasks.Add(GetPackageDetails(result.InstalledOlderCompatable));
                }

                if (result.AvailableNewer != null) {
                    tasks.Add(GetPackageDetails(result.AvailableNewer));
                }

                if (result.AvailableNewerCompatible != null) {
                    tasks.Add(GetPackageDetails(result.AvailableNewerCompatible));
                }

                if (result.Package != null) {
                    tasks.Add(GetPackageDetails(result.Package));
                }

                Task.WaitAll(tasks.ToArray());

                return result;
            });
        }

        private Package NewestCompatablePackageIn(Package aPackage, IEnumerable<Package> packages) {
            var result = aPackage;
            var pkgs = packages.OrderBy(each => each.Version).ToArray();
            Package pk;

            while ((pk = pkgs.FirstOrDefault(p => p.MinPolicy <= result.Version && p.MaxPolicy >= result.Version && result.Version < p.Version)) != null) {
                result = pk;
            }
            return result;
        }

        public Task<Package> GetLatestInstalledCompatableVersion(CanonicalName canonicalName) {
            return GetPackage(canonicalName).Continue(package => {
                var result = NewestCompatablePackageIn(package, GetInstalledPackages(canonicalName).Result);
                return result.IsInstalled ? result : null;
            });
        }

        public Task<bool> IsPackageBlocked(CanonicalName packageName) {
            return (Remote.FindPackages(packageName.GeneralName, blocked: true) as Task<PackageManagerResponseImpl>).Continue(response => response.Packages.Any());
        }

        public Task<bool> IsPackageMarkedRequested(CanonicalName canonicalName) {
            return GetPackage(canonicalName).Continue(package => package.IsClientRequired);
        }

        public Task<bool> IsPackageMarkedDoNotUpgrade(CanonicalName canonicalName) {
            return GetPackage(canonicalName).Continue(package => package.DoNotUpgrade);
        }

        public Task<bool> IsPackageMarkedDoNotUpdate(CanonicalName canonicalName) {
            return GetPackage(canonicalName).Continue(package => package.DoNotUpdate);
        }

        public Task<bool> IsPackageActive(CanonicalName canonicalName) {
            return GetPackage(canonicalName).Continue(package => package.IsActive);
        }

        public Task BlockPackage(CanonicalName packageName) {
            return SetPackageFlags(packageName, blocked: true);
        }

        public Task MarkPackageDoNotUpdate(CanonicalName canonicalName) {
            return SetPackageFlags(canonicalName, doNotUpdate: true);
        }

        public Task MarkPackageDoNotUpgrade(CanonicalName canonicalName) {
            return SetPackageFlags(canonicalName, doNotUpgrade: true);
        }

        public Task MarkPackageActive(CanonicalName canonicalName) {
            return SetPackageFlags(canonicalName, active: true);
        }

        public Task MarkPackageRequested(CanonicalName canonicalName) {
            return SetPackageFlags(canonicalName, requested: true);
        }

        public Task UnBlockPackage(CanonicalName packageName) {
            return SetPackageFlags(packageName, blocked: false);
        }

        public Task MarkPackageOkToUpdate(CanonicalName canonicalName) {
            return SetPackageFlags(canonicalName, doNotUpdate: false);
        }

        public Task MarkPackageOkToUpgrade(CanonicalName canonicalName) {
            return SetPackageFlags(canonicalName, doNotUpgrade: false);
        }

        public Task MarkPackageNotRequested(CanonicalName canonicalName) {
            return SetPackageFlags(canonicalName, requested: false);
        }

        private Task SetPackageFlags(CanonicalName canonicalName, bool? active = null, bool? requested = null, bool? blocked = null, bool? doNotUpdate = null, bool? doNotUpgrade = null) {
            // you can actually use a partial package name for this call.
            return Remote.SetPackage(canonicalName, active, requested, blocked, doNotUpdate, doNotUpgrade);
        }


        public Task<IEnumerable<Package>> GetActiveVersion(CanonicalName packageName) {
            return (Remote.FindPackages(packageName, active: true) as Task<PackageManagerResponseImpl>).Continue(response => response.Packages);
        }
        public Task<IEnumerable<Package>> GetActiveVersions(IEnumerable<CanonicalName> packageNames) {
            return packageNames.Select(GetActiveVersion).Continue(results => results.SelectMany(result => result).Distinct());
        }

        public Task<IEnumerable<Package>> GetUpdatablePackages(CanonicalName packageName) {
            return (Remote.FindPackages(packageName, updates: true) as Task<PackageManagerResponseImpl>).Continue(response => response.Packages);
        }
        public Task<IEnumerable<Package>> GetUpdatablePackages(IEnumerable<CanonicalName> packageNames) {
            return packageNames.Select(GetUpdatablePackages).Continue(results => results.SelectMany(result => result).Distinct());
        }

        public Task<IEnumerable<Package>> GetUpgradablePackages(CanonicalName packageName) {
            return (Remote.FindPackages(packageName, upgrades: true) as Task<PackageManagerResponseImpl>).Continue(response => response.Packages);
        }
        public Task<IEnumerable<Package>> GetUpgradablePackages(IEnumerable<CanonicalName> packageNames) {
            return packageNames.Select(GetUpgradablePackages).Continue(results => results.SelectMany(result => result).Distinct());
        }

        public Task<IEnumerable<Package>> GetTrimablePackages(CanonicalName packageName) {
            return (Remote.FindPackages(packageName, trimable: true) as Task<PackageManagerResponseImpl>).Continue(response => response.Packages);
        }
        public Task<IEnumerable<Package>> GetTrimablePackages(IEnumerable<CanonicalName> packageNames) {
            return packageNames.Select(GetTrimablePackages).Continue(results => results.SelectMany(result => result).Distinct());
        }

        public Task<IEnumerable<Package>> GetAllVersionsOfPackage(CanonicalName packageName) {
            return (Remote.FindPackages(packageName.OtherVersionFilter, trimable: true) as Task<PackageManagerResponseImpl>).Continue(response => response.Packages);
        }

        public Task<IEnumerable<Package>> GetInstalledPackages(CanonicalName packageName, bool? active = null, bool? requested = null, bool? blocked = null, string locationFeed = null) {
            return (Remote.FindPackages(packageName.OtherVersionFilter, installed: true, active: active, required: requested, blocked: blocked, location: locationFeed) as Task<PackageManagerResponseImpl>).Continue(response => response.Packages);
        }
        public Task<IEnumerable<Package>> GetInstalledPackages(IEnumerable<CanonicalName> packageNames, bool? active = null, bool? requested = null, bool? blocked = null, string locationFeed = null) {
            return packageNames.Select(each => GetInstalledPackages(each, active: active, requested: requested, blocked: blocked, locationFeed: locationFeed)).Continue(results => results.SelectMany(result => result).Distinct());
        }

        public Task<Package> GetPackageFromFile(string filename) {
            filename = filename.GetFullPath();
            if( File.Exists(filename)) {
                return QueryPackages(filename).Continue(packages => {
                    var pkg = packages.FirstOrDefault();
                    if (pkg == null) {
                        throw new UnknownPackageException("filename: {0}".format(filename));
                    }
                    return pkg;
                });    
            }
            return ((Package)null).AsCanceledTask();
        }

        /*
        public Task<IEnumerable<Package>> GetPackages(string packageName, FourPartVersion? minVersion = null, FourPartVersion? maxVersion = null, bool? dependencies = null, bool? installed = null,
            bool? active = null, bool? requested = null, bool? blocked = null, bool? latest = null, string locationFeed = null, bool? updates = null, bool? upgrades = null, bool? trimable = null) {
            return GetPackages(packageName.SingleItemAsEnumerable(), minVersion, maxVersion, dependencies, installed, active, requested, blocked, latest, locationFeed, updates, upgrades, trimable);
        }

        public Task<IEnumerable<Package>> GetPackages(IEnumerable<string> packageNames, FourPartVersion? minVersion = null, FourPartVersion? maxVersion = null, bool? dependencies = null,
            bool? installed = null, bool? active = null, bool? requested = null, bool? blocked = null, bool? latest = null, string locationFeed = null, bool? updates = null, bool? upgrades = null,
            bool? trimable = null) {
            return GetPackagesEx(packageNames, minVersion, maxVersion, dependencies, installed, active, requested, blocked, latest, locationFeed, false, updates, upgrades, trimable);
        }
        */
        private Task<TReturnType> InvalidCanonicalNameResult<TReturnType>(CanonicalName canonicalName) {
            var failedResult = new TaskCompletionSource<TReturnType>();
            failedResult.SetException(new InvalidCanonicalNameException(canonicalName));
            return failedResult.Task;
        }

        private Task<IEnumerable<Package>> Install(CanonicalName canonicalName, bool? autoUpgrade = null, bool? isUpdate = null, bool? isUpgrade = null, bool? pretend = null, bool? download = null) {
            if (!canonicalName.IsCanonical) {
                return InvalidCanonicalNameResult<IEnumerable<Package>>(canonicalName);
            }

            var completedThisPackage = false;

            CurrentTask.Events += new PackageInstallProgress((name, progress, overallProgress) => {
                if (overallProgress == 100) {
                    completedThisPackage = true;
                }
            });

            return (Remote.InstallPackage(canonicalName, autoUpgrade, false, download, pretend, isUpdate, isUpgrade) as Task<PackageManagerResponseImpl>).Continue(response => {
                if (response.PotentialUpgrades != null) {
                    throw new PackageHasPotentialUpgradesException(response.UpgradablePackage, response.PotentialUpgrades);
                }
                return response.Packages;
            });
        }

        public Task InstallPackage(CanonicalName canonicalName, bool? autoUpgrade = null) {
            return Install(canonicalName, autoUpgrade, false, false, false);
        }

        public Task UpgradeExistingPackage(CanonicalName canonicalName, bool? autoUpgrade = null) {
            return Install(canonicalName, autoUpgrade, false, true, false);
        }

        public Task UpdateExistingPackage(CanonicalName canonicalName, bool? autoUpgrade = null) {
            return Install(canonicalName, autoUpgrade, true, false, false);
        }

        public Task<IEnumerable<Package>> EnsurePackagesAndDependenciesAreLocal(CanonicalName canonicalName, bool? autoUpgrade = null) {
            return Install(canonicalName, autoUpgrade, false, false, true, true);
        }

        public Task<IEnumerable<Package>> WhatWouldBeInstalled(CanonicalName canonicalName, bool? autoUpgrade = null) {
            return Install(canonicalName, autoUpgrade, false, false, false, false);
        }

        public Task<int> RemovePackages(IEnumerable<CanonicalName> canonicalNames, bool forceRemoval) {
            var packagesToRemove = canonicalNames.ToList();

            return Task.Factory.StartNew(() => {
                int[] total = {0};
                int packageCount;

                // this will keep looping around as long as the last pass removed a package
                // it was way easier to do this than to figure out what the order of removal is. :P
                while ((packageCount = packagesToRemove.Count) > 0) {
                    var packageFailures = new List<Exception>();

                    foreach (var cn in packagesToRemove.ToArray()) {
                        var canonicalName = cn;
                        try {
                            // remove the package and wait for completion.
                            RemovePackage(canonicalName, forceRemoval).Wait();

                            packagesToRemove.Remove(canonicalName);
                            total[0] = total[0] + 1;
                        } catch (Exception exception) {
                            packageFailures.Add(exception);
                        }
                    }

                    if (packagesToRemove.Any() && packageCount == packagesToRemove.Count) {
                        // it didn't remove any on that pass. we're boned.
                        throw new AggregateException(packageFailures);
                    }
                }
                return total[0];
            }, TaskCreationOptions.AttachedToParent);
        }

        public Task RemovePackage(CanonicalName canonicalName, bool forceRemoval) {
            if (!canonicalName.IsCanonical) {
                return InvalidCanonicalNameResult<IEnumerable<Package>>(canonicalName);
            }

            return Remote.RemovePackage(canonicalName, forceRemoval);
        }

        private Task<LoggingSettings> SetLogging(bool? messages = null, bool? warnings = null, bool? errors = null) {
            return (Remote.SetLogging(messages, warnings, errors) as Task<PackageManagerResponseImpl>).Continue(response => response.LoggingSettingsResult);
        }

        public Task EnableMessageLogging() {
            Logger.Messages = true;
            return SetLogging(messages: true);
        }

        public Task DisableMessageLogging() {
            Logger.Messages = false;
            return SetLogging(messages: false);
        }

        public Task<bool> IsMessageLogging {
            get {
                return SetLogging().Continue(results => results.Messages);
            }
        }

        public Task EnableWarningLogging() {
            Logger.Warnings = true;
            return SetLogging(warnings: true);
        }

        public Task DisableWarningLogging() {
            Logger.Warnings = false;
            return SetLogging(messages: false);
        }

        public Task<bool> IsWarningLogging {
            get {
                return SetLogging().Continue(results => results.Warnings);
            }
        }

        public Task EnableErrorLogging() {
            Logger.Errors = true;
            return SetLogging(errors: true);
        }

        public Task DisableErrorLogging() {
            Logger.Errors = false;
            return SetLogging(errors: false);
        }

        public Task<bool> IsErrorLogging {
            get {
                return SetLogging().Continue(results => results.Errors);
            }
        }

        public Task<string> RemoveSystemFeed(string feedLocation) {
            return (Remote.RemoveFeed(feedLocation, false) as Task<PackageManagerResponseImpl>).Continue(response => feedLocation);
        }

        public Task<string> RemoveSessionFeed(string feedLocation) {
            return (Remote.RemoveFeed(feedLocation, true) as Task<PackageManagerResponseImpl>).Continue(response => feedLocation);
        }

        public Task<string> AddSystemFeed(string feedLocation) {
            return (Remote.AddFeed(feedLocation, false) as Task<PackageManagerResponseImpl>).Continue(response => response.Feeds.Select(each => each.Location).FirstOrDefault());
        }

        public Task<string> AddSessionFeed(string feedLocation) {
            return (Remote.AddFeed(feedLocation, true) as Task<PackageManagerResponseImpl>).Continue(response => response.Feeds.Select(each => each.Location).FirstOrDefault());
        }

        public Task SuppressFeed(string feedLocation) {
            return (Remote.SuppressFeed(feedLocation));
        }

        public Task SetFeed(string feedLocation, FeedState state) {
            return Remote.SetFeedFlags(feedLocation, state.ToString());
        }

        public Task<IEnumerable<Feed>> Feeds {
            get {
                return (Remote.ListFeeds() as Task<PackageManagerResponseImpl>).Continue(response => response.Feeds);
            }
        }

        public Task SetFeedStale(string feedLocation) {
            return Remote.SetFeedStale(feedLocation);
        }

        public Task SetAllFeedsStale() {
            return Feeds.Continue(feeds => {
                foreach (var feed in feeds) {
                    SetFeedStale(feed.Location).RethrowWhenFaulted();
                }
            });
        }

        public Task<Package> RefreshPackageDetails(CanonicalName canonicalName) {
            if (!canonicalName.IsCanonical) {
                return InvalidCanonicalNameResult<Package>(canonicalName);
            }

            return (Remote.GetPackageDetails(canonicalName) as Task<PackageManagerResponseImpl>).Continue(response => Package.GetPackage(canonicalName));
        }

        public Task<Package> GetPackage(CanonicalName canonicalName) {
            if (!canonicalName.IsCanonical) {
                return InvalidCanonicalNameResult<Package>(canonicalName);
            }

            var pkg = Package.GetPackage(canonicalName);

            if (pkg.IsPackageInfoStale) {
                return (Remote.FindPackages(canonicalName) as Task<PackageManagerResponseImpl>).Continue(response => Package.GetPackage(canonicalName));
            }

            return pkg.AsResultTask();
        }

        public Task<Package> GetPackageDetails(CanonicalName canonicalName) {
            if (!canonicalName.IsCanonical) {
                return InvalidCanonicalNameResult<Package>(canonicalName);
            }

            return GetPackage(canonicalName).Continue(package => GetPackageDetails(package).Result);
        }

        public Task<Package> GetPackageDetails(Package package) {
            if (package.IsPackageDetailsStale) {
                return GetPackage(package.CanonicalName).Continue(pkg => RefreshPackageDetails(pkg.CanonicalName).Result);
            }

            return package.Roles.IsNullOrEmpty() ? RefreshPackageDetails(package.CanonicalName) : package.AsResultTask();
        }

        public Task<bool> GetTelemetry() {
            return (Remote.GetTelemetry() as Task<PackageManagerResponseImpl>).Continue(response => response.OptedIn);
        }

        public Task SetTelemetry(bool optInToTelemetry) {
            return Remote.SetTelemetry(optInToTelemetry);
        }

        public Task CreateSymlink(string existingLocation, string newLink) {
            return Remote.CreateSymlink(existingLocation, newLink, LinkType.Symlink);
        }

        public Task CreateHardlink(string existingLocation, string newLink) {
            return Remote.CreateSymlink(existingLocation, newLink, LinkType.Hardlink);
        }

        public Task CreateShortcut(string existingLocation, string newLink) {
            return Remote.CreateSymlink(existingLocation, newLink, LinkType.Shortcut);
        }

        public Task RemoveFromPolicy(string policyName, string account) {
            return Remote.RemoveFromPolicy(policyName, account);
        }

        public Task AddToPolicy(string policyName, string account) {
            return Remote.AddToPolicy(policyName, account);
        }

        public Task<Policy> GetPolicy(string policyName) {
            return (Remote.GetPolicy(policyName) as Task<PackageManagerResponseImpl>).Continue(response => response.Policies.FirstOrDefault());
        }

        public Task<IEnumerable<Policy>> Policies {
            get {
                return (Remote.GetPolicy("*") as Task<PackageManagerResponseImpl>).Continue(response => response.Policies);
            }
        }

        /// <summary>
        /// </summary>
        /// <param name="taskName"> the name of the task. If a task with this name already exists, it will be overwritten. </param>
        /// <param name="executable"> </param>
        /// <param name="commandline"> </param>
        /// <param name="hour"> </param>
        /// <param name="minutes"> </param>
        /// <param name="dayOfWeek"> </param>
        /// <param name="intervalInMinutes"> how often the scheduled task should consider running (on Windows XP/2003, it's not possible to run as soon as possible after a task was missed. </param>
        /// <returns> </returns>
        public Task AddScheduledTask(string taskName, string executable, string commandline, int hour, int minutes, DayOfWeek? dayOfWeek, int intervalInMinutes) {
            return Remote.ScheduleTask(taskName, executable, commandline, hour, minutes, dayOfWeek, intervalInMinutes);
        }

        public Task RemoveScheduledTask(string taskName) {
            return Remote.RemoveScheduledTask(taskName);
        }

        public Task<ScheduledTask> GetScheduledTask(string taskName) {
            return (Remote.GetScheduledTasks(taskName) as Task<PackageManagerResponseImpl>).Continue(response => response.ScheduledTasks.FirstOrDefault());
        }

        public Task<IEnumerable<ScheduledTask>> ScheduledTasks {
            get {
                return (Remote.GetScheduledTasks("*") as Task<PackageManagerResponseImpl>).Continue(response => response.ScheduledTasks);
            }
        }

        public Task<IEnumerable<Publisher>> TrustedPublishers {
            get {
                return null;
            }
        }

        public Task<Publisher> AddTrustedPublisher(string publisherName, string publicKeyToken) {
            return null;
        }

        public Task RemoveTrustedPublisher(string publisherName, string publicKeyToken) {
            return null;
        }

        // GS01: TrustedPublishers Coming Soon.

        public Task RecognizeFile(string filename) {
            return Remote.RecognizeFile("", filename, "");
        }
    }
}