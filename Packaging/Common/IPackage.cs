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

namespace CoApp.Packaging.Common {
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Runtime.Serialization;
    using Model;
    using Toolkit.Win32;

    public interface IPackage {
        CanonicalName CanonicalName { get; }
        string Name { get; }
        FlavorString Flavor { get; }
        FourPartVersion Version { get; }
        PackageType PackageType { get; }
        Architecture Architecture { get; }
        string PublicKeyToken { get; }
        BindingPolicy BindingPolicy { get; }
        PackageDetails PackageDetails { get;  }

        bool IsInstalled { get; }
        bool IsBlocked { get; }
        bool IsClientRequired { get; }
        bool IsActive { get;  }
        bool IsDependency { get;  }

        bool DoNotUpdate { get; }
        bool DoNotUpgrade { get;}

        string DisplayName { get;}

        IPackage SatisfiedBy { get; }

        IEnumerable<Uri> RemoteLocations { get; }
        IEnumerable<Uri> Feeds { get; }
        IEnumerable<CanonicalName> Dependencies { get; }
        IEnumerable<IPackage> UpdatePackages { get; }
        IEnumerable<IPackage> UpgradePackages { get; }
        IEnumerable<IPackage> NewerPackages { get; }
        IEnumerable<Role> Roles { get; }
    }

    public static class PackageExtensions {
        /// <summary>
        /// Determines if this package is a supercedent of an older package.
        /// 
        /// Note:This should be the only method that does comparisons on the package policy *ANYWHERE*
        /// </summary>
        /// <param name="olderPackage"></param>
        /// <returns></returns>
        public static bool IsAnUpdateFor(this IPackage package, IPackage olderPackage) {
            return package.CanonicalName.DiffersOnlyByVersion(olderPackage.CanonicalName) &&
                package.BindingPolicy != null &&
                    package.BindingPolicy.Minimum <= olderPackage.CanonicalName.Version &&
                    package.BindingPolicy.Maximum >= olderPackage.CanonicalName.Version;
        }

        public static bool IsAnUpgradeFor(this IPackage package, IPackage olderPackage) {
            return package.IsNewerThan(olderPackage) && !package.IsAnUpdateFor(olderPackage);
        }

        public static bool IsNewerThan(this IPackage package, IPackage olderPackage) {
            return package.CanonicalName.DiffersOnlyByVersion(olderPackage.CanonicalName) &&
                package.CanonicalName.Version > olderPackage.CanonicalName.Version;
        }

        /// <summary>
        ///   This gets the highest version of all the packages in the set.
        /// </summary>
        /// <param name="packageSet"> The package set. </param>
        /// <returns> the filtered colleciton of packages </returns>
        /// <remarks>
        /// </remarks>
        internal static IPackage[] HighestPackages(this IEnumerable<IPackage> packageSet) {
            var all = packageSet.OrderByDescending(p => p.CanonicalName.Version).ToArray();
            if (all.Length > 1) {
                var filters = all.Select(each => each.CanonicalName.OtherVersionFilter).Distinct().ToArray();
                if (all.Length != filters.Length) {
                    // only do the filter if there is actually packages of differing versions.
                    return filters.Select(each => all.FirstOrDefault(pkg => pkg.CanonicalName.Matches(each))).ToArray();
                }
            }
            return all;
        }
    }
}
