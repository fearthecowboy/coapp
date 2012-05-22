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
    using Model;
    using Toolkit.Extensions;
    using Toolkit.Win32;
#if COAPP_ENGINE_CORE
    using Service;
#else
    using Client;
#endif

    [ImplementedBy(Types = new Type[1]{typeof(Package)})]
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
        bool IsWanted { get; }
        bool IsActive { get;  }
        bool IsDependency { get;  }
        bool IsTrimable { get; }

        string DisplayName { get;}

        IPackage SatisfiedBy { get; }
        IEnumerable<Uri> RemoteLocations { get; }
        IEnumerable<Uri> Feeds { get; }
        IEnumerable<Role> Roles { get; }

        /// <summary>
        ///   The newest version of this package that is installed and newer than the given package and is binary compatible.
        /// </summary>
        IPackage InstalledNewestUpdate { get; }

        /// <summary>
        ///   The newest version of this package that is installed and newer than the given package and is not binary compatible.
        /// </summary>
        IPackage InstalledNewestUpgrade { get; }

        /// <summary>
        ///   The newest version of this package that is installed, and newer than the given package
        /// </summary>
        IPackage InstalledNewest { get; }


        /// <summary>
        ///   The newest package that is currently installed, that the given package is a compatible update for.
        /// </summary>
        IPackage LatestInstalledThatUpdatesToThis { get; }


        /// <summary>
        ///   The newest package that is currently installed, that the given package is a non-compatible update for.
        /// </summary>
        IPackage LatestInstalledThatUpgradesToThis { get; }


        /// <summary>
        ///   The latest version of the package that is available that is newer than the current package.
        /// </summary>
        IPackage AvailableNewest { get; }

        /// <summary>
        ///   The latest version of the package that is available and is binary compatable with the given package
        /// </summary>
        IPackage AvailableNewestUpdate { get; }

        /// <summary>
        ///   The latest version of the package that is available and is binary compatable with the given package
        /// </summary>
        IPackage AvailableNewestUpgrade { get; }


        /// <summary>
        ///   All Installed versions of this package
        /// </summary>
        IEnumerable<IPackage> InstalledPackages { get; }

        /// <summary>
        ///   The list of packages that are an update for this package
        /// </summary>
        IEnumerable<IPackage> UpdatePackages { get; }

        /// <summary>
        ///   The list of packages that are an upgrade for this package
        /// </summary>
        IEnumerable<IPackage> UpgradePackages { get; }

        /// <summary>
        ///   The list of packages that are newer than this package (Updates+Upgrades)
        /// </summary>
        IEnumerable<IPackage> NewerPackages { get; }

        /// <summary>
        /// The packages that this package depends on.
        /// </summary>
        IEnumerable<IPackage> Dependencies { get; }

        /// <summary>
        ///   All the trimable packages for this package
        /// </summary>
        IEnumerable<IPackage> TrimablePackages { get; }

        PackageState PackageState { get; }
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
        public static IEnumerable<IPackage> HighestPackages(this IEnumerable<IPackage> packageSet) {
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

    public enum PackageState {
        Blocked,
        DoNotChange,
        Updatable,
        Upgradable,
    }
}
