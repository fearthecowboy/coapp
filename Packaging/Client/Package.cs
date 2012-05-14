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
                if(null != canonicalName && canonicalName.IsCanonical) {
                    return AllPackages.GetOrAdd(canonicalName, () => new Package {
                        CanonicalName = canonicalName
                    });
                }
                return null;
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
        [Persistable]
        public BindingPolicy BindingPolicy { get; set; }
        [Persistable]
        public PackageDetails PackageDetails { get; set; }
        [Persistable]
        public bool IsInstalled { get; set; }
        [Persistable]
        public bool IsBlocked { get; set; }
        [Persistable]
        public bool IsRequired { get; set; }
        [Persistable]
        public bool IsClientRequired { get; set; }
        [Persistable]
        public bool IsActive { get; set; }
        [Persistable]
        public bool IsDependency { get; set; }
        [Persistable]
        public string DisplayName { get; set; }
        [Persistable]
        public string PackageItemText { get; set; }
        [Persistable]
        public bool DoNotUpdate { get; set; }
        [Persistable]
        public bool DoNotUpgrade { get; set; }
        [Persistable]
        public IEnumerable<Uri> RemoteLocations { get; set; }
        [Persistable]
        public IEnumerable<Uri> Feeds { get; set; }
        [Persistable]
        public IEnumerable<Role> Roles { get; set; }

        public string LocalPackagePath { get; set; }

        [NotPersistable]
        public string Name { get { return CanonicalName.Name; } }
        [NotPersistable]
        public FlavorString Flavor { get { return CanonicalName.Flavor; } }
        [NotPersistable]
        public FourPartVersion Version { get { return CanonicalName.Version; } }
        [NotPersistable]
        public PackageType PackageType { get { return CanonicalName.PackageType; } }
        [NotPersistable]
        public Architecture Architecture { get { return CanonicalName.Architecture; } }
        [NotPersistable]
        public string PublicKeyToken { get { return CanonicalName.PublicKeyToken; } }


        [NotPersistable]
        public IPackage SatisfiedBy { get; set; }
        [NotPersistable]
        public IEnumerable<CanonicalName> Dependencies { get; set; }

        [NotPersistable]
        public IEnumerable<IPackage> UpdatePackages { get; set; }
        [NotPersistable]
        public IEnumerable<IPackage> UpgradePackages { get; set; }
        [NotPersistable]
        public IEnumerable<IPackage> NewerPackages { get; set; }

        [NotPersistable]
        internal bool IsPackageInfoStale { get; set; }

        [NotPersistable]
        internal bool IsPackageDetailsStale { get; set; }
    };
}