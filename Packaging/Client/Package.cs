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
    using Toolkit.Linq;
    using Toolkit.Win32;

    public class Package : IPackage {
        public static class Properties {
            public static PropertyExpression<IPackage, CanonicalName> CanonicalName = PropertyExpression<IPackage>.Create(p => p.CanonicalName);
            public static PropertyExpression<IPackage, string> Name = PropertyExpression<IPackage>.Create(p => p.Name);
            public static PropertyExpression<IPackage, FlavorString> Flavor = PropertyExpression<IPackage>.Create(p => p.Flavor);
            public static PropertyExpression<IPackage, FourPartVersion> Version = PropertyExpression<IPackage>.Create(p => p.Version);
            public static PropertyExpression<IPackage, PackageType> PackageType = PropertyExpression<IPackage>.Create(p => p.PackageType);
            public static PropertyExpression<IPackage, Architecture> Architecture = PropertyExpression<IPackage>.Create(p => p.Architecture);
            public static PropertyExpression<IPackage, string> PublicKeyToken = PropertyExpression<IPackage>.Create(p => p.PublicKeyToken);
            public static PropertyExpression<IPackage, BindingPolicy> BindingPolicy = PropertyExpression<IPackage>.Create(p => p.BindingPolicy);

            public static PropertyExpression<IPackage, bool> Installed = PropertyExpression<IPackage>.Create(p => p.IsInstalled);
            public static PropertyExpression<IPackage, bool> Blocked = PropertyExpression<IPackage>.Create(p => p.IsBlocked);
            public static PropertyExpression<IPackage, bool> ClientRequired = PropertyExpression<IPackage>.Create(p => p.IsClientRequired);
            public static PropertyExpression<IPackage, bool> Active = PropertyExpression<IPackage>.Create(p => p.IsActive);
            public static PropertyExpression<IPackage, bool> Dependency = PropertyExpression<IPackage>.Create(p => p.IsDependency);

            public static PropertyExpression<IPackage, bool> DoNotUpdate = PropertyExpression<IPackage>.Create(p => p.DoNotUpdate);
            public static PropertyExpression<IPackage, bool> DoNotUpgrade = PropertyExpression<IPackage>.Create(p => p.DoNotUpgrade);

            public static PropertyExpression<IPackage, string> DisplayName = PropertyExpression<IPackage>.Create(p => p.DisplayName);
            public static PropertyExpression<IPackage, IPackage> SatisfiedBy = PropertyExpression<IPackage>.Create(p => p.SatisfiedBy);

            public static PropertyExpression<IPackage, IEnumerable<Uri>> RemoteLocations = PropertyExpression<IPackage>.Create(p => p.RemoteLocations);
            public static PropertyExpression<IPackage, IEnumerable<Uri>> Feeds = PropertyExpression<IPackage>.Create(p => p.Feeds);
            public static PropertyExpression<IPackage, IEnumerable<Role>> Roles = PropertyExpression<IPackage>.Create(p => p.Roles);

            public static PropertyExpression<IPackage, IEnumerable<string>> Tags = PropertyExpression<IPackage>.Create(p => (IEnumerable<string>)p.PackageDetails.Tags);
            public static PropertyExpression<IPackage, IEnumerable<string>> Categories = PropertyExpression<IPackage>.Create(p => (IEnumerable<string>)p.PackageDetails.Categories);
            public static PropertyExpression<IPackage, IEnumerable<Uri>> Icons = PropertyExpression<IPackage>.Create(p => (IEnumerable<Uri>)p.PackageDetails.Icons);

            public static PropertyExpression<IPackage, string> AuthorVersion = PropertyExpression<IPackage>.Create(p => p.PackageDetails.AuthorVersion);
            public static PropertyExpression<IPackage, string> BugTracker = PropertyExpression<IPackage>.Create(p => p.PackageDetails.BugTracker);
            public static PropertyExpression<IPackage, bool> IsNsfw = PropertyExpression<IPackage>.Create(p => p.PackageDetails.IsNsfw);
            public static PropertyExpression<IPackage, sbyte> Stability = PropertyExpression<IPackage>.Create(p => p.PackageDetails.Stability);
            public static PropertyExpression<IPackage, string> SummaryDescription = PropertyExpression<IPackage>.Create(p => p.PackageDetails.SummaryDescription);
            public static PropertyExpression<IPackage, DateTime> PublishDate = PropertyExpression<IPackage>.Create(p => p.PackageDetails.PublishDate);
            public static PropertyExpression<IPackage, string> CopyrightStatement = PropertyExpression<IPackage>.Create(p => p.PackageDetails.CopyrightStatement);
            public static PropertyExpression<IPackage, string> Description = PropertyExpression<IPackage>.Create(p => p.PackageDetails.Description);

            public static PropertyExpression<IPackage, IPackage> InstalledNewerCompatable = PropertyExpression<IPackage>.Create(p => p.InstalledNewerCompatable);
            public static PropertyExpression<IPackage, IPackage> InstalledNewer = PropertyExpression<IPackage>.Create(p => p.InstalledNewer);
            public static PropertyExpression<IPackage, IPackage> InstalledOlderCompatable = PropertyExpression<IPackage>.Create(p => p.InstalledOlderCompatable);
            public static PropertyExpression<IPackage, IPackage> InstalledOlder = PropertyExpression<IPackage>.Create(p => p.InstalledOlder);
            public static PropertyExpression<IPackage, IPackage> AvailableNewer = PropertyExpression<IPackage>.Create(p => p.AvailableNewer);
            public static PropertyExpression<IPackage, IPackage> AvailableNewerCompatible = PropertyExpression<IPackage>.Create(p => p.AvailableNewerCompatible);
            public static PropertyExpression<IPackage, IPackage> InstalledNewest = PropertyExpression<IPackage>.Create(p => p.InstalledNewest);

            public static PropertyExpression<IPackage, IEnumerable<IPackage>> InstalledPackages = PropertyExpression<IPackage>.Create(p => p.InstalledPackages);
            public static PropertyExpression<IPackage, IEnumerable<IPackage>> UpdatePackages = PropertyExpression<IPackage>.Create(p => p.UpdatePackages);
            public static PropertyExpression<IPackage, IEnumerable<IPackage>> UpgradePackages = PropertyExpression<IPackage>.Create(p => p.UpgradePackages);
            public static PropertyExpression<IPackage, IEnumerable<IPackage>> NewerPackages = PropertyExpression<IPackage>.Create(p => p.NewerPackages);
            public static PropertyExpression<IPackage, IEnumerable<IPackage>> Dependencies = PropertyExpression<IPackage>.Create(p => p.Dependencies);
            public static PropertyExpression<IPackage, IEnumerable<IPackage>> Trimable = PropertyExpression<IPackage>.Create(p => p.Trimable);

            // public static PropertyExpression<IPackage, Identity> Publisher = PropertyExpression<IPackage>.Create(p => p.PackageDetails.Publisher);
            // public static PropertyExpression<IPackage, XList<License>> Licenses = PropertyExpression<IPackage>.Create(p => p.PackageDetails.Licenses);
            // public static PropertyExpression<IPackage, XList<Identity>> Contributors = PropertyExpression<IPackage>.Create(p => p.PackageDetails.Contributors);



        }

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
        
        [NotPersistable]
        public IPackage InstalledNewerCompatable { get { return PackageManager.Instance.GetPackage(_installedNewerCompatable).Result; } } 
        [NotPersistable]
        public IPackage InstalledNewer { get { return PackageManager.Instance.GetPackage(_installedNewer).Result; } }
        [NotPersistable]
        public IPackage InstalledOlderCompatable { get { return PackageManager.Instance.GetPackage(_installedOlderCompatable).Result; } }
        [NotPersistable]
        public IPackage InstalledOlder { get { return PackageManager.Instance.GetPackage(_installedOlder).Result; } }
        [NotPersistable] 
        public IPackage AvailableNewer { get { return PackageManager.Instance.GetPackage(_availableNewer).Result; } }
        [NotPersistable]
        public IPackage AvailableNewerCompatible { get { return PackageManager.Instance.GetPackage(_availableNewerCompatible).Result; } }
        [NotPersistable]
        public IPackage InstalledNewest { get { return PackageManager.Instance.GetPackage(_installedNewest).Result; } }

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
        public IPackage SatisfiedBy { get { return null != _satisfiedBy ? PackageManager.Instance.GetPackage(_satisfiedBy).Result : null; } }
        [NotPersistable]
        public IEnumerable<IPackage> UpdatePackages { get { return PackageManager.Instance.GetPackages(_updatePackages).Result; } }
        [NotPersistable]
        public IEnumerable<IPackage> UpgradePackages { get { return PackageManager.Instance.GetPackages(_upgradePackages).Result; } }
        [NotPersistable]
        public IEnumerable<IPackage> NewerPackages { get { return PackageManager.Instance.GetPackages(_newerPackages).Result; } }
        [NotPersistable]
        public IEnumerable<IPackage> InstalledPackages { get { return PackageManager.Instance.GetPackages(_installedPackages).Result; } }
        [NotPersistable]
        public IEnumerable<IPackage> Dependencies { get { return PackageManager.Instance.GetPackages(_dependencies).Result; } }
        [NotPersistable]
        public IEnumerable<IPackage> Trimable { get { return PackageManager.Instance.GetPackages(_trimable).Result; } }

        [Persistable(name: "NewerPackages")]
        private IEnumerable<CanonicalName> _newerPackages;
        [Persistable(name: "UpdatePackages")]
        private IEnumerable<CanonicalName> _updatePackages;
        [Persistable(name: "UpgradePackages")]
        private IEnumerable<CanonicalName> _upgradePackages;
        [Persistable(name: "Dependencies")]
        private IEnumerable<CanonicalName> _dependencies;
        [Persistable(name: "InstalledPackages")]
        private IEnumerable<CanonicalName> _installedPackages;
        [Persistable(name: "Trimable")]
        private IEnumerable<CanonicalName> _trimable;
        [Persistable(name: "InstalledNewerCompatable")]
        private CanonicalName _installedNewerCompatable;
        [Persistable(name: "InstalledNewer")]
        private CanonicalName _installedNewer;
        [Persistable(name: "InstalledOlderCompatable")]
        private CanonicalName _installedOlderCompatable;
        [Persistable(name: "InstalledOlder")]
        private CanonicalName _installedOlder;
        [Persistable(name: "AvailableNewer")]
        private CanonicalName _availableNewer;
        [Persistable(name: "AvailableNewerCompatible")]
        private CanonicalName _availableNewerCompatible;
        [Persistable(name: "InstalledNewest")]
        private CanonicalName _installedNewest;
        [Persistable(name: "SatisfiedBy")]
        private CanonicalName _satisfiedBy;
       

        [NotPersistable]
        internal bool IsPackageInfoStale { get; set; }

        [NotPersistable]
        internal bool IsPackageDetailsStale { get; set; }

        public string LocalPackagePath { get; set; }
    };
}