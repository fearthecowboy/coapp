using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CoApp.Toolkit.Engine.Feeds {
    using System.IO;
    using Extensions;
    using PackageFormatHandlers;
    using Win32;

    internal class InstalledPackageFeed : PackageFeed {
        internal static string CanonicalLocation = "CoApp://InstalledPackages";
        internal static InstalledPackageFeed Instance = new InstalledPackageFeed();       

        /// <summary>
        /// contains the list of packages in the direcory. (may be recursive)
        /// </summary>
        private readonly List<Package> _packageList = new List<Package>();

        private InstalledPackageFeed() : base(CanonicalLocation) {
            
        }

        internal void PackageRemoved( Package package ) {
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

        public int Progress { get; set; }

        protected void Scan() {
            lock (this) {
                if (!Scanned || Stale) {
                    LastScanned = DateTime.Now;

                    // add the cached package directory, 'cause on backlevel platform, they taint the MSI in the installed files folder.
                    var installedFiles =
                        MSIBase.InstalledMSIFilenames.Union(PackageManagerSettings.CoAppPackageCache.FindFilesSmarter("*.msi")).ToArray();

                  
                    var count = installedFiles.Length;

                    installedFiles.AsParallel().ForAll(each => {
                        count--;
                        Progress = (count - installedFiles.Length)*100/installedFiles.Length;
                        var lookup = File.GetCreationTime(each).Ticks + each.GetHashCode();

                        if (!Cache.Contains(lookup)) {
                            var pkg = Package.GetPackageFromFilename(each);

                            if (pkg != null && pkg.IsInstalled) {
                                _packageList.Add(pkg);
                            } else {
                                // doesn't appear to be a coapp package
                                Cache.Add(lookup);
                            }
                        }
                    });

                    SaveCache();
                    Progress = 100;
                    Scanned = true;
                    Stale = false;
                }
            }
        }

        /// <summary>
        /// Finds packages based on the cosmetic name of the package.
        /// 
        /// Supports wildcard in pattern match.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="version"></param>
        /// <param name="arch"></param>
        /// <param name="publicKeyToken"></param>
        /// <param name="packageFilter">The package filter.</param>
        /// <returns></returns>
        /// <remarks></remarks>
        internal override IEnumerable<Package> FindPackages(string name, string version, string arch, string publicKeyToken) { 
            Scan();
            return from p in _packageList where
                (string.IsNullOrEmpty(name) || p.Name.IsWildcardMatch(name)) &&
                (string.IsNullOrEmpty(version) || p.Version.ToString().IsWildcardMatch(version)) &&
                (string.IsNullOrEmpty(arch) || p.Architecture.ToString().IsWildcardMatch(arch)) &&
                (string.IsNullOrEmpty(publicKeyToken) || p.PublicKeyToken.IsWildcardMatch(publicKeyToken)) select p;
        }
    }
}
