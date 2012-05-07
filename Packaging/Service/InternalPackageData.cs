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
    using Toolkit.Collections;
    using Toolkit.Extensions;

    internal class InternalPackageData : NotifiesPackageManager {
        private readonly Package _package;

        internal readonly XList<Uri> RemoteLocations  = new XList<Uri>();
        internal readonly XList<Uri> FeedLocations  = new XList<Uri>();
        internal readonly XList<string> LocalLocations  = new XList<string>();
        internal BindingPolicy BindingPolicy;

        internal readonly XList<Role> Roles = new XList<Role>();
        internal readonly XList<Feature> Features = new XList<Feature>();
        internal readonly XList<Feature> RequiredFeatures  = new XList<Feature>();

        public readonly ObservableCollection<Package> Dependencies = new ObservableCollection<Package>();

        internal InternalPackageData(Package package) {
            _package = package;
            Dependencies.CollectionChanged += (x, y) => Changed();
        }

        public bool HasLocalLocation {
            get {
                return !LocalLocations.IsNullOrEmpty();
            }
        }

        public bool HasRemoteLocation {
            get {
                return !RemoteLocations.IsNullOrEmpty();
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