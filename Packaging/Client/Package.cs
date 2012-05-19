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
        private static readonly IDictionary<CanonicalName, Package> AllPackages = new XDictionary<CanonicalName, Package>();

        private BindingPolicy _bindingPolicy;
        private PackageDetails _packageDetails;

        private string _displayName;
        private bool _doNotUpdate;
        private bool _doNotUpgrade;
        private bool _isActive;
        private bool _isBlocked;
        private bool _isClientRequired;
        private bool _isDependency;
        private bool _isInstalled;
        private bool _isRequired;
        private string _packageItemText;

        private IPackage _availableNewest;
        private IPackage _availableNewestUpdate;
        private IPackage _availableNewestUpgrade;
        private IPackage _installedNewest;
        private IPackage _installedNewestUpdate;
        private IPackage _installedNewestUpgrade;
        private IPackage _latestInstalledThatUpdatesToThis;
        private IPackage _latestInstalledThatUpgradesToThis;
        private IPackage _satisfiedBy;

        private IEnumerable<IPackage> _newerPackages;
        private IEnumerable<IPackage> _dependencies;
        private IEnumerable<Uri> _feeds;
        private IEnumerable<IPackage> _installedPackages;
        private IEnumerable<Uri> _remoteLocations;
        private IEnumerable<Role> _roles;
        private IEnumerable<IPackage> _trimable;
        private IEnumerable<IPackage> _updatePackages;
        private IEnumerable<IPackage> _upgradePackages;

        internal Package() {
            IsPackageInfoStale = true;
            IsPackageDetailsStale = true;
            RemoteLocations = Enumerable.Empty<Uri>();
            Feeds = Enumerable.Empty<Uri>();
        }

        [Persistable]
        public bool IsRequired {
            get {
                DemandLoad();
                return _isRequired;
            }
            internal set {
                IsPackageInfoStale = false;
                _isRequired = value;
            }
        }

        [Persistable]
        public string PackageItemText {
            get {
                DemandLoad();
                return _packageItemText;
            }
            internal set {
                IsPackageInfoStale = false;
                _packageItemText = value;
            }
        }

        [NotPersistable]
        internal bool IsPackageInfoStale { get; set; }

        [NotPersistable]
        internal bool IsPackageDetailsStale { get; set; }

        public string LocalPackagePath { get; set; }

        #region IPackage Members

        [Persistable]
        public CanonicalName CanonicalName { get; internal set; }

        [Persistable]
        public BindingPolicy BindingPolicy {
            get {
                DemandLoad();
                return _bindingPolicy;
            }
            internal set {
                IsPackageInfoStale = false;
                _bindingPolicy = value;
            }
        }

        [Persistable]
        public PackageDetails PackageDetails {
            get {
                if (IsPackageDetailsStale) {
                    PackageManager.Instance.GetPackageDetails(this);
                }
                return _packageDetails;
            }
            internal set {
                IsPackageDetailsStale = false;
                _packageDetails = value;
            }
        }

        [Persistable]
        public bool IsInstalled {
            get {
                DemandLoad();
                return _isInstalled;
            }
            internal set {
                IsPackageInfoStale = false;
                _isInstalled = value;
            }
        }

        [Persistable]
        public bool IsBlocked {
            get {
                DemandLoad();
                return _isBlocked;
            }
            internal set {
                IsPackageInfoStale = false;
                _isBlocked = value;
            }
        }

        [Persistable]
        public bool IsClientRequired {
            get {
                DemandLoad();
                return _isClientRequired;
            }
            internal set {
                IsPackageInfoStale = false;
                _isClientRequired = value;
            }
        }

        [Persistable]
        public bool IsActive {
            get {
                DemandLoad();
                return _isActive;
            }
            internal set {
                IsPackageInfoStale = false;
                _isActive = value;
            }
        }

        [Persistable]
        public bool IsDependency {
            get {
                DemandLoad();
                return _isDependency;
            }
            internal set {
                IsPackageInfoStale = false;
                _isDependency = value;
            }
        }

        [Persistable]
        public string DisplayName {
            get {
                DemandLoad();
                return _displayName;
            }
            internal set {
                IsPackageInfoStale = false;
                _displayName = value;
            }
        }

        [Persistable]
        public bool DoNotUpdate {
            get {
                DemandLoad();
                return _doNotUpdate;
            }
            internal set {
                IsPackageInfoStale = false;
                _doNotUpdate = value;
            }
        }

        [Persistable]
        public bool DoNotUpgrade {
            get {
                DemandLoad();
                return _doNotUpgrade;
            }
            internal set {
                IsPackageInfoStale = false;
                _doNotUpgrade = value;
            }
        }

        [Persistable]
        public IEnumerable<Uri> RemoteLocations {
            get {
                DemandLoad();
                return _remoteLocations ?? Enumerable.Empty<Uri>();
            }
            internal set {
                IsPackageInfoStale = false;
                _remoteLocations = value;
            }
        }

        [Persistable]
        public IEnumerable<Uri> Feeds {
            get {
                DemandLoad();
                return _feeds ?? Enumerable.Empty<Uri>();
            }
            internal set {
                IsPackageInfoStale = false;
                _feeds = value;
            }
        }

        [Persistable]
        public IEnumerable<Role> Roles {
            get {
                DemandLoad();
                return _roles ?? Enumerable.Empty<Role>();
            }
            internal set {
                IsPackageInfoStale = false;
                _roles = value;
            }
        }

        [Persistable(DeserializeAsType = typeof (CanonicalName))]
        public IPackage InstalledNewestUpdate {
            get {
                DemandLoad();
                return _installedNewestUpdate;
            }
            internal set {
                IsPackageInfoStale = false;
                _installedNewestUpdate = value;
            }
        }

        [Persistable(DeserializeAsType = typeof (CanonicalName))]
        public IPackage InstalledNewestUpgrade {
            get {
                DemandLoad();
                return _installedNewestUpgrade;
            }
            internal set {
                IsPackageInfoStale = false;
                _installedNewestUpgrade = value;
            }
        }

        [Persistable(DeserializeAsType = typeof (CanonicalName))]
        public IPackage InstalledNewest {
            get {
                DemandLoad();
                return _installedNewest;
            }
            internal set {
                IsPackageInfoStale = false;
                _installedNewest = value;
            }
        }

        [Persistable(DeserializeAsType = typeof (CanonicalName))]
        public IPackage LatestInstalledThatUpdatesToThis {
            get {
                DemandLoad();
                return _latestInstalledThatUpdatesToThis;
            }
            internal set {
                IsPackageInfoStale = false;
                _latestInstalledThatUpdatesToThis = value;
            }
        }

        [Persistable(DeserializeAsType = typeof (CanonicalName))]
        public IPackage LatestInstalledThatUpgradesToThis {
            get {
                DemandLoad();
                return _latestInstalledThatUpgradesToThis;
            }
            internal set {
                IsPackageInfoStale = false;
                _latestInstalledThatUpgradesToThis = value;
            }
        }

        [Persistable(DeserializeAsType = typeof (CanonicalName))]
        public IPackage AvailableNewest {
            get {
                DemandLoad();
                return _availableNewest;
            }
            internal set {
                IsPackageInfoStale = false;
                _availableNewest = value;
            }
        }

        [Persistable(DeserializeAsType = typeof (CanonicalName))]
        public IPackage AvailableNewestUpdate {
            get {
                DemandLoad();
                return _availableNewestUpdate;
            }
            internal set {
                IsPackageInfoStale = false;
                _availableNewestUpdate = value;
            }
        }

        [Persistable(DeserializeAsType = typeof (CanonicalName))]
        public IPackage AvailableNewestUpgrade {
            get {
                DemandLoad();
                return _availableNewestUpgrade;
            }
            internal set {
                IsPackageInfoStale = false;
                _availableNewestUpgrade = value;
            }
        }

        [NotPersistable]
        public string Name {
            get {
                return CanonicalName.Name;
            }
        }

        [NotPersistable]
        public FlavorString Flavor {
            get {
                return CanonicalName.Flavor;
            }
        }

        [NotPersistable]
        public FourPartVersion Version {
            get {
                return CanonicalName.Version;
            }
        }

        [NotPersistable]
        public PackageType PackageType {
            get {
                return CanonicalName.PackageType;
            }
        }

        [NotPersistable]
        public Architecture Architecture {
            get {
                return CanonicalName.Architecture;
            }
        }

        [NotPersistable]
        public string PublicKeyToken {
            get {
                return CanonicalName.PublicKeyToken;
            }
        }

        [Persistable(DeserializeAsType = typeof (CanonicalName))]
        public IPackage SatisfiedBy {
            get {
                DemandLoad();
                return _satisfiedBy;
            }
            internal set {
                IsPackageInfoStale = false;
                _satisfiedBy = value;
            }
        }

        [Persistable(DeserializeAsType = typeof (IEnumerable<CanonicalName>))]
        public IEnumerable<IPackage> UpdatePackages {
            get {
                DemandLoad();
                return _updatePackages ?? Enumerable.Empty<IPackage>();
            }
            internal set {
                IsPackageInfoStale = false;
                _updatePackages = value;
            }
        }

        [Persistable(DeserializeAsType = typeof (IEnumerable<CanonicalName>))]
        public IEnumerable<IPackage> UpgradePackages {
            get {
                DemandLoad();
                return _upgradePackages ?? Enumerable.Empty<IPackage>();
            }
            internal set {
                IsPackageInfoStale = false;
                _upgradePackages = value;
            }
        }

        [Persistable(DeserializeAsType = typeof (IEnumerable<CanonicalName>))]
        public IEnumerable<IPackage> NewerPackages {
            get {
                DemandLoad();
                return _newerPackages ?? Enumerable.Empty<IPackage>();
            }
            internal set {
                IsPackageInfoStale = false;
                _newerPackages = value;
            }
        }

        [Persistable(DeserializeAsType = typeof (IEnumerable<CanonicalName>))]
        public IEnumerable<IPackage> InstalledPackages {
            get {
                DemandLoad();
                return _installedPackages ?? Enumerable.Empty<IPackage>();
            }
            internal set {
                IsPackageInfoStale = false;
                _installedPackages = value;
            }
        }

        [Persistable(DeserializeAsType = typeof (IEnumerable<CanonicalName>))]
        public IEnumerable<IPackage> Dependencies {
            get {
                DemandLoad();
                return _dependencies ?? Enumerable.Empty<IPackage>();
            }
            internal set {
                IsPackageInfoStale = false;
                _dependencies = value;
            }
        }

        [Persistable(DeserializeAsType = typeof (IEnumerable<CanonicalName>))]
        public IEnumerable<IPackage> Trimable {
            get {
                DemandLoad();
                return _trimable ?? Enumerable.Empty<IPackage>();
            }
            internal set {
                IsPackageInfoStale = false;
                _trimable = value;
            }
        }

        #endregion

        public static implicit operator CanonicalName(Package package) {
            return package.CanonicalName;
        }

        public static implicit operator Package(CanonicalName name) {
            return GetPackage(name);
        }

        public static Package GetPackage(CanonicalName canonicalName) {
            lock (AllPackages) {
                if (null != canonicalName && canonicalName.IsCanonical) {
                    return AllPackages.GetOrAdd(canonicalName, () => new Package {
                        CanonicalName = canonicalName
                    });
                }
                return null;
            }
        }

        private void DemandLoad() {
            if (IsPackageInfoStale) {
                PackageManager.Instance.GetPackage(CanonicalName);
            }
        }

        #region Nested type: Properties

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

            public static PropertyExpression<IPackage, IPackage> InstalledNewestUpdate = PropertyExpression<IPackage>.Create(p => p.InstalledNewestUpdate);
            public static PropertyExpression<IPackage, IPackage> InstalledNewestUpgrade = PropertyExpression<IPackage>.Create(p => p.InstalledNewestUpgrade);

            public static PropertyExpression<IPackage, IPackage> LatestInstalledThatUpdatesToThis = PropertyExpression<IPackage>.Create(p => p.LatestInstalledThatUpdatesToThis);
            public static PropertyExpression<IPackage, IPackage> LatestInstalledThatUpgradesToThis = PropertyExpression<IPackage>.Create(p => p.LatestInstalledThatUpgradesToThis);
            public static PropertyExpression<IPackage, IPackage> AvailableNewest = PropertyExpression<IPackage>.Create(p => p.AvailableNewest);
            public static PropertyExpression<IPackage, IPackage> AvailableNewestUpdate = PropertyExpression<IPackage>.Create(p => p.AvailableNewestUpdate);
            public static PropertyExpression<IPackage, IPackage> AvailableNewestUpgrade = PropertyExpression<IPackage>.Create(p => p.AvailableNewestUpgrade);
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

        public static class Filters {
            public static Filter<IPackage> InstalledPackages = Properties.Installed.Is(true);
            public static Filter<IPackage> PackagesWithUpdateAvailable = InstalledPackages & Properties.UpdatePackages.Any();
            public static Filter<IPackage> PackagesWithUpgradeAvailable = InstalledPackages & Properties.UpdatePackages.Any();
        }

        #endregion
    };
}