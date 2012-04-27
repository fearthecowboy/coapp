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
    using System.Collections.Generic;

    public class PackageSet {
        public Package Package;

        /// <summary>
        ///   The newest version of this package that is installed and newer than the given package and is binary compatible.
        /// </summary>
        public Package InstalledNewerCompatable;

        /// <summary>
        ///   The newest version of this package that is installed, and newer than the given package
        /// </summary>
        public Package InstalledNewer;

        /// <summary>
        ///   The newest package that is currently installed, that the given package is a compatible update for.
        /// </summary>
        public Package InstalledOlderCompatable;

        /// <summary>
        ///   The newest package that is currently installed, that the give package is an upgrade for.
        /// </summary>
        public Package InstalledOlder;

        /// <summary>
        ///   The latest version of the package that is available that is newer than the current package.
        /// </summary>
        public Package AvailableNewer;

        /// <summary>
        ///   The latest version of the package that is available and is binary compatable with the given package
        /// </summary>
        public Package AvailableNewerCompatible;

        /// <summary>
        ///   The latest version that is installed.
        /// </summary>
        public Package InstalledNewest;

        /// <summary>
        ///   All Installed versions of this package
        /// </summary>
        public IEnumerable<Package> InstalledPackages;

        /// <summary>
        ///   All the trimable packages for this package
        /// </summary>
        public IEnumerable<Package> Trimable;
    }
}