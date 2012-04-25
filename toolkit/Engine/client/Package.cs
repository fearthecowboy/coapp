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

using CoApp.Toolkit.Engine.Model;
using CoApp.Toolkit.Extensions;
using CoApp.Toolkit.Win32;

namespace CoApp.Toolkit.Engine.Client {
    using System;
    using System.Collections.Generic;
    using System.Linq;

    public class Package {
        private static readonly Dictionary<string, Package> _allPackages = new Dictionary<string, Package>();

        public static Package GetPackage(string canonicalName) {
            lock (_allPackages) {
                if (_allPackages.ContainsKey(canonicalName)) {
                    return _allPackages[canonicalName];
                }

                var packageName = PackageName.Parse(canonicalName);

                var result = new Package {
                    CanonicalName = canonicalName,
                    Name = packageName.Name,
                    Architecture = packageName.Arch,
                    Version = packageName.Version,
                    PublicKeyToken = packageName.PublicKeyToken
                };

                _allPackages.Add(canonicalName, result);
                return result;
            }
        }

        protected Package() {
            IsPackageInfoStale = true;
            IsPackageDetailsStale = true;
            Tags = Enumerable.Empty<string>();
            RemoteLocations = Enumerable.Empty<string>();
            Dependencies = Enumerable.Empty<string>();
            SupercedentPackages = Enumerable.Empty<string>();
        }

        public string CanonicalName { get; set; }
        public string LocalPackagePath{ get; set; }
        public string Name{ get; set; }
        public string Flavor { get; internal set; }
        public FourPartVersion Version{ get; set; }
        public PackageType PackageType { get; set; }
        public FourPartVersion MinPolicy{ get; set; }
        public FourPartVersion MaxPolicy { get; set; }
        public Architecture Architecture { get; set; }
        public string PublicKeyToken{ get; set; }
        public bool IsInstalled{ get; set; }
        public bool IsBlocked{ get; set; }
        public bool IsRequired{ get; set; }
        public bool IsClientRequired{ get; set; }
        public bool IsActive{ get; set; }
        public bool IsDependency{ get; set; }
        public string Description{ get; set; }
        public string Summary{ get; set; }
        public string DisplayName{ get; set; }
        public string Copyright{ get; set; }
        public string AuthorVersion{ get; set; }
        public string Icon{ get; set; }
        public string License{ get; set; }
        public string LicenseUrl{ get; set; }
        public string PublishDate{ get; set; }
        public string PublisherName{ get; set; }
        public string PublisherUrl{ get; set; }
        public string PublisherEmail{ get; set; }
        public string ProductCode { get; set; }
        public string PackageItemText { get; set; }

        public bool DoNotUpdate { get; set; }
        public bool DoNotUpgrade { get; set; }

        public Package SatisfiedBy{ get; set; }

        public IEnumerable<string> Tags{ get; set; }  
        public IEnumerable<string> RemoteLocations{ get; set; }
        public IEnumerable<string> Dependencies{ get; set; }
        public IEnumerable<string> SupercedentPackages{ get; set; }
        public IEnumerable<Role> Roles { get; set; }

        internal bool IsPackageInfoStale { get; set; }
        internal bool IsPackageDetailsStale { get; set; }

        public bool IsCompatableWith(Package package) {
            return package.Version > Version
                ? package.MinPolicy <= package.Version && package.MaxPolicy >= Version : MinPolicy <= package.Version && MaxPolicy >= package.Version;
        }
    };
}