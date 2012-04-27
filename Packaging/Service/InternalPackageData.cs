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
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Linq;
    using Common.Model;
    using Toolkit.Extensions;
    using Toolkit.Win32;

    internal class InternalPackageData : NotifiesPackageManager {
        private readonly Package _package;

        private string _canonicalPackageLocation;
        private string _canonicalFeedLocation;

        private string _primaryLocalLocation;
        private string _primaryRemoteLocation;
        private string _primaryFeedLocation;

        private readonly List<Uri> _remoteLocations = new List<Uri>();
        private readonly List<string> _feedLocations = new List<string>();
        private readonly List<string> _localLocations = new List<string>();

        // set once only:
        internal FourPartVersion PolicyMinimumVersion { get; set; }
        internal FourPartVersion PolicyMaximumVersion { get; set; }

        public readonly List<Role> Roles = new List<Role>();
        public List<Feature> Features { get; set; }
        public List<Feature> RequiredFeatures { get; set; }

        // public readonly List<PackageAssemblyInfo> Assemblies = new List<PackageAssemblyInfo>();
        public readonly ObservableCollection<Package> Dependencies = new ObservableCollection<Package>();

        public string CanonicalPackageLocation {
            get {
                return _canonicalPackageLocation;
            }
            set {
                try {
                    RemoteLocation = _canonicalPackageLocation = new Uri(value).AbsoluteUri;
                } catch {
                }
            }
        }

        public string CanonicalFeedLocation {
            get {
                return _canonicalFeedLocation;
            }
            set {
                FeedLocation = _canonicalFeedLocation = value;
            }
        }

        public string CanonicalSourcePackageLocation { get; set; }

        internal InternalPackageData(Package package) {
            _package = package;
            Dependencies.CollectionChanged += (x, y) => Changed();
        }

        public bool IsPackageSatisfied {
            get {
                return _package.IsInstalled || !string.IsNullOrEmpty(LocalLocation) && RemoteLocation != null && _package.PackageSessionData.Supercedent != null;
            }
        }

        public bool HasLocalLocation {
            get {
                return !string.IsNullOrEmpty(LocalLocation);
            }
        }

        public bool HasRemoteLocation {
            get {
                return !string.IsNullOrEmpty(RemoteLocation);
            }
        }

        public IEnumerable<string> LocalLocations {
            get {
                return _localLocations.ToArray();
            }
        }

        public IEnumerable<string> RemoteLocations {
            get {
                return _remoteLocations.Select(each => each.AbsoluteUri).ToArray();
            }
        }

        public IEnumerable<string> FeedLocations {
            get {
                return _feedLocations.ToArray();
            }
        }

        public string LocalLocation {
            get {
                if (_primaryLocalLocation.FileIsLocalAndExists()) {
                    return _primaryLocalLocation;
                }
                // use the setter to remove non-viable locations.
                LocalLocation = null;

                // whatever is primary after the set is good for me.
                return _primaryLocalLocation;
            }
            set {
                lock (_localLocations) {
                    try {
                        var location = value.CanonicalizePathIfLocalAndExists();

                        if (!string.IsNullOrEmpty(location)) {
                            // this location is acceptable.
                            _primaryLocalLocation = location;
                            if (!_localLocations.Contains(location)) {
                                _localLocations.Add(location);
                            }
                            return;
                        }
                    } catch {
                        // file couldn't canonicalize.
                    }

                    _primaryLocalLocation = null;

                    // try to find an acceptable local location from the list 
                    foreach (var path in _localLocations.Where(path => path.FileIsLocalAndExists())) {
                        _primaryLocalLocation = path;
                        break;
                    }
                }
            }
        }

        public string RemoteLocation {
            get {
                if (!string.IsNullOrEmpty(_canonicalPackageLocation)) {
                    return _canonicalPackageLocation;
                }

                if (!string.IsNullOrEmpty(_primaryRemoteLocation)) {
                    return _primaryRemoteLocation;
                }

                // use the setter to remove non-viable locations.
                RemoteLocation = null;

                // whatever is primary after the set is good for me.
                return _primaryRemoteLocation;
            }

            set {
                lock (_remoteLocations) {
                    if (!string.IsNullOrEmpty(value)) {
                        try {
                            var location = new Uri(value);

                            // this location is acceptable.
                            _primaryRemoteLocation = location.AbsoluteUri;

                            if (!_remoteLocations.Contains(location)) {
                                _remoteLocations.Add(location);
                            }

                            return;
                        } catch {
                            // path couldn't be expressed as a URI?.
                        }
                    }

                    // set it as the first viable remote location.
                    var uri = _remoteLocations.FirstOrDefault();
                    _primaryRemoteLocation = uri == null ? null : uri.AbsoluteUri;
                }
            }
        }

        public string FeedLocation {
            get {
                if (!string.IsNullOrEmpty(_canonicalFeedLocation)) {
                    return _canonicalFeedLocation;
                }

                if (!string.IsNullOrEmpty(_primaryFeedLocation)) {
                    return _primaryFeedLocation;
                }

                // use the setter to remove non-viable locations.
                FeedLocation = null;

                // whatever is primary after the set is good for me.
                return _primaryFeedLocation;
            }

            set {
                lock (_feedLocations) {
                    if (!string.IsNullOrEmpty(value)) {
                        _primaryFeedLocation = value;
                        if (!_feedLocations.Contains(value)) {
                            _feedLocations.Add(value);
                        }
                        return;
                    }

                    // set it as the first viable remote location.
                    var location = _feedLocations.FirstOrDefault();
                    _primaryFeedLocation = string.IsNullOrEmpty(location) ? null : location;
                }
            }
        }

        private Composition CompositionData {
            get {
                return _compositionData ?? (_compositionData = _package.PackageHandler.GetCompositionData(_package));
            }
        }

        private Composition _compositionData;

        public IEnumerable<CompositionRule> CompositionRules {
            get {
                return CompositionData.CompositionRules ?? Enumerable.Empty<CompositionRule>();
            }
        }

        public IEnumerable<WebApplication> WebApplications {
            get {
                return CompositionData.WebApplications ?? Enumerable.Empty<WebApplication>();
            }
        }

        public IEnumerable<DeveloperLibrary> DeveloperLibraries {
            get {
                return CompositionData.DeveloperLibraries ?? Enumerable.Empty<DeveloperLibrary>();
            }
        }

        public IEnumerable<Service> Services {
            get {
                return CompositionData.Services ?? Enumerable.Empty<Service>();
            }
        }

        public IEnumerable<Driver> Drivers {
            get {
                return CompositionData.Drivers ?? Enumerable.Empty<Driver>();
            }
        }

        public IEnumerable<SourceCode> SourceCodes {
            get {
                return CompositionData.SourceCodes ?? Enumerable.Empty<SourceCode>();
            }
        }
    }
}