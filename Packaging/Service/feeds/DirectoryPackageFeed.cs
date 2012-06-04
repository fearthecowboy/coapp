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
    using Common;
    using Toolkit.Extensions;

    /// <summary>
    ///   Creates a package feed from a local filesystem directory.
    /// </summary>
    /// <remarks>
    /// </remarks>
    internal class DirectoryPackageFeed : PackageFeed {
        /// <summary>
        ///   contains the list of packages in the direcory. (may be recursive)
        /// </summary>
        private readonly List<Package> _packageList = new List<Package>();

        private readonly string _path;

        /// <summary>
        ///   the wildcard patter for matching files in this feed.
        /// </summary>
        private readonly string _filter;

        /// <summary>
        ///   Initializes a new instance of the <see cref="DirectoryPackageFeed" /> class.
        /// </summary>
        /// <param name="location"> The directory to scan. </param>
        /// <param name="patternMatch"> The wildcard pattern match files agains. </param>
        /// <remarks>
        /// </remarks>
        internal DirectoryPackageFeed(string location, string patternMatch) : base(location) {
            _path = location;
            _filter = patternMatch ?? "*.msi";  // TODO: evenutally, we have to expand this to detect other types.
        }

        private int _lastCount;

        internal override bool Stale {
            get {
                return base.Stale || (_path.DirectoryEnumerateFilesSmarter(_filter, false ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly).Count() != _lastCount);
            }
            set {
                base.Stale = value;
            }
        }

        /// <summary>
        ///   Scans the directory for all packages that match the wildcard. For each file found, it will ask the recognizer to identify if the file is a package (any kind of package) This will only scan the directory if the Scanned property is false.
        /// </summary>
        /// <remarks>
        ///   NOTE: Some of this may get refactored to change behavior before the end of the beta2.
        /// </remarks>
        protected void Scan() {
            lock (this) {
                if (!Scanned || Stale) {
                    LastScanned = DateTime.Now;

                    // GS01: BUG: recursive now should use ** in pattern match.
                    var files = _path.DirectoryEnumerateFilesSmarter(
                        _filter, false ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly /*, PackageManagerImpl.Instance.BlockedScanLocations*/).ToArray();

                    _lastCount = files.Count();

                    var pkgFiles = from file in files
                        where Recognizer.Recognize(file).Result.IsPackageFile
                        // Since we know this to be local, it'm ok with blocking on the result.
                        select file;

                    foreach (var pkg in pkgFiles.Select(Package.GetPackageFromFilename).Where(pkg => pkg != null)) {
                        pkg.FeedLocations.AddUnique(Location.ToUri());

                        if (!_packageList.Contains(pkg)) {
                            _packageList.Add(pkg);
                        }
                    }
                    Stale = false;
                    Scanned = true;
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