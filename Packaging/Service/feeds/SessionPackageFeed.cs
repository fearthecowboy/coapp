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
    using System.Linq;
    using Common;

    internal class SessionPackageFeed : PackageFeed {
        internal static string CanonicalLocation = "CoApp://SessionPackages";

        internal static SessionPackageFeed Instance {
            get {
                return SessionData.Current.SessionPackageFeed ?? (SessionData.Current.SessionPackageFeed = new SessionPackageFeed());    
            }
        }

        /// <summary>
        ///   contains the list of packages in the direcory. (may be recursive)
        /// </summary>
        private readonly List<Package> _packageList = new List<Package>();

        private SessionPackageFeed()
            : base(CanonicalLocation) {
            Scanned = true;
            LastScanned = DateTime.Now;
        }

        internal void Add(Package package) {
            if (!_packageList.Contains(package)) {
                _packageList.Add(package);
                Scanned = true;
                LastScanned = DateTime.Now;
                PackageManagerImpl.Instance.Updated();
            }
        }

        /// <summary>
        ///   Finds packages based on the canonical details of the package. Supports wildcard in pattern match.
        /// </summary>
        /// <param name="canonicalName"> </param>
        /// <returns> </returns>
        /// <remarks>
        /// </remarks>
        internal override IEnumerable<Package> FindPackages(CanonicalName canonicalName) {
            return _packageList.Where(each => each.CanonicalName.Matches(canonicalName));
        }
    }
}