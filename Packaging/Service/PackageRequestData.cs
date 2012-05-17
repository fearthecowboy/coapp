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
    using System.Collections.Generic;
    using System.Linq;
    using Common;
    using Feeds;

    /// <summary>
    ///   This stores information that is really only relevant to the currently running request, not between sessions. The instance of this is bound to the Session.
    /// </summary>
    internal class PackageRequestData : NotifiesPackageManager {
        private Package _package;

        internal bool NotifiedClientThisSupercedes;

        internal PackageRequestData(Package package) {
            _package = package;
        }

        // don't calculate this more than once per request.
        private IPackage[] _installedPackages;
        internal IEnumerable<IPackage> InstalledPackages { get {
            return _installedPackages ?? (_installedPackages = InstalledPackageFeed.Instance.FindPackages(_package.CanonicalName.OtherVersionFilter).OrderByDescending(each => each.Version).ToArray());
        } }

        private IPackage[] _otherPackages;
        internal IEnumerable<IPackage> OtherPackages {
            get {
                return _otherPackages ?? (_otherPackages = PackageManagerImpl.Instance.SearchForPackages(_package.CanonicalName.OtherVersionFilter).OrderByDescending(each => each.Version).ToArray());
            }
        }
    }
}