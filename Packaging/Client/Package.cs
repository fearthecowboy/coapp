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
    using System.Linq;
    using Common;
    using Common.Model;
    using Toolkit.Collections;
    using Toolkit.Extensions;
    using Toolkit.Pipes;
    using Toolkit.Win32;

    public class Package : IPackage {
        private static readonly IDictionary<CanonicalName, Package> AllPackages = new XDictionary<CanonicalName, Package>();

        public static Package GetPackage(CanonicalName canonicalName) {
            lock (AllPackages) {
                return AllPackages.GetOrAdd(canonicalName, () => new Package {
                    CanonicalName = canonicalName
                });
            }
        }


        internal Package() {
            IsPackageInfoStale = true;
            IsPackageDetailsStale = true;
            RemoteLocations = Enumerable.Empty<Uri>();
            Feeds = Enumerable.Empty<Uri>();
            Dependencies = Enumerable.Empty<CanonicalName>();
        }

        public CanonicalName CanonicalName { get; set; }
        public string LocalPackagePath { get; set; }

        public string Name { get { return CanonicalName.Name; } }
        public FlavorString Flavor { get { return CanonicalName.Flavor; } }
        public FourPartVersion Version { get { return CanonicalName.Version; } }
        public PackageType PackageType { get { return CanonicalName.PackageType; } }
        public Architecture Architecture { get { return CanonicalName.Architecture; } }
        public string PublicKeyToken { get { return CanonicalName.PublicKeyToken; } }

        public BindingPolicy BindingPolicy { get; set; }

        public PackageDetails PackageDetails { get; set; }

        public bool IsInstalled { get; set; }
        public bool IsBlocked { get; set; }
        public bool IsRequired { get; set; }
        public bool IsClientRequired { get; set; }
        public bool IsActive { get; set; }
        public bool IsDependency { get; set; }

        public string DisplayName { get; set; }
        
        public string PackageItemText { get; set; }

        public bool DoNotUpdate { get; set; }
        public bool DoNotUpgrade { get; set; }

        public IPackage SatisfiedBy { get; set; }
        public IEnumerable<Uri> RemoteLocations { get; set; }
        public IEnumerable<Uri> Feeds { get; set; }
        public IEnumerable<CanonicalName> Dependencies { get; set; }

        public IEnumerable<IPackage> UpdatePackages { get; set; }
        public IEnumerable<IPackage> UpgradePackages { get; set; }
        public IEnumerable<IPackage> NewerPackages { get; set; }
        public IEnumerable<Role> Roles { get; set; }

        internal bool IsPackageInfoStale { get; set; }
        internal bool IsPackageDetailsStale { get; set; }
        
        public new string ToString() {
            return "TODO: IMPLEMENT";
        }

        public static bool TryParse(string text, out IPackage details) {
            details = null;
            return false;
        }
    };
}