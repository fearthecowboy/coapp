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

namespace CoApp.Packaging.Service.Feeds {
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using Common;
    using PackageFormatHandlers;
    using Toolkit.Extensions;
    using Toolkit.Tasks;

    internal class InstalledPackageFeed : PackageFeed {
        internal static string CanonicalLocation = "CoApp://InstalledPackages";
        internal static InstalledPackageFeed Instance = new InstalledPackageFeed();

        /// <summary>
        ///   contains the list of packages in the direcory. (may be recursive)
        /// </summary>
        private readonly List<Package> _packageList = new List<Package>();

        private InstalledPackageFeed() : base(CanonicalLocation) {
        }

        internal void PackageRemoved(Package package) {
            lock (this) {
                if (_packageList.Contains(package)) {
                    _packageList.Remove(package);
                }
            }
        }

        internal void PackageInstalled(Package package) {
            lock (this) {
                if (!_packageList.Contains(package)) {
                    _packageList.Add(package);
                }
            }
        }

        protected void ScanInstalledMSIs() {
            Task.Factory.StartNew(() => {
                var systemInstalledFiles = MSIBase.InstalledMSIFilenames.ToArray();
                foreach (var each in systemInstalledFiles) {
                    var lookup = File.GetCreationTime(each).Ticks + each.GetHashCode();
                    if (!Cache.Contains(lookup)) {
                        var pkg = Package.GetPackageFromFilename(each);
                        if (pkg != null && pkg.IsInstalled) {
                            _packageList.Add(pkg);
                        } else {
                            // doesn't appear to be a coapp package
                            Cache.Add(lookup);
                            SaveCache();
                        }
                    }
                }
            }, TaskCreationOptions.LongRunning).AutoManage();
        }

        protected void Scan() {
            lock (this) {
                if (!Scanned || Stale) {
                    LastScanned = DateTime.Now;
                    ScanInstalledMSIs(); // kick off the system package task. It's ok if this doesn't get done in a hurry.

                    // add the cached package directory, 'cause on backlevel platform, they taint the MSI in the installed files folder.
                    var coAppInstalledFiles = PackageManagerSettings.CoAppPackageCache.FindFilesSmarter("*.msi").ToArray();

                    coAppInstalledFiles.AsParallel().ForAll(each => {
                        var lookup = File.GetCreationTime(each).Ticks + each.GetHashCode();
                        if (!Cache.Contains(lookup)) {
                            var pkg = Package.GetPackageFromFilename(each);

                            if (pkg != null && pkg.IsInstalled) {
                                _packageList.Add(pkg);
                            } else {
                                // doesn't appear to be a coapp package
                                Cache.Add(lookup);
                                SaveCache();
                            }
                        }
                    });
                    Scanned = true;
                    Stale = false;
                }
            }
        }

        /// <summary>
        ///   Finds packages based on the cosmetic name of the package. Supports wildcard in pattern match.
        /// </summary>
        /// <returns> </returns>
        /// <remarks>
        /// </remarks>
        internal override IEnumerable<Package> FindPackages(CanonicalName canonicalName) {
            Scan();
            return _packageList.Where(each => each.CanonicalName.Matches(canonicalName));
        }
    }
}