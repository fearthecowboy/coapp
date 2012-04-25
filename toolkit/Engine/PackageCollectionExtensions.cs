//-----------------------------------------------------------------------
// <copyright company="CoApp Project">
//     Copyright (c) 2011 Garrett Serack . All rights reserved.
// </copyright>
// <license>
//     The software is licensed under the Apache 2.0 License (the "License")
//     You may not use the software except in compliance with the License. 
// </license>
//-----------------------------------------------------------------------

namespace CoApp.Toolkit.Engine {
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Linq;
    using System.Threading.Tasks;
    using Extensions;
    using Feeds;

    /// <summary>
    /// Extension methods to make queries on package sets easier.
    /// </summary>
    /// <remarks></remarks>
    public static class PackageCollectionExtensions {

        /// <summary>
        /// Adds the feed location.
        /// </summary>
        /// <param name="feedCollection">The feed collection.</param>
        /// <param name="feedLocation">The feed location.</param>
        /// <returns></returns>
        /// <remarks>
        /// NOTE: This is probably gettin' refactored PFQ
        /// </remarks>
        internal static Task AddFeedLocation(this ObservableCollection<PackageFeed> feedCollection, string feedLocation) {
            if (!(from feed in feedCollection
                where feed.Location.Equals(feedLocation, StringComparison.CurrentCultureIgnoreCase)
                select feed).Any()) {
                return PackageFeed.GetPackageFeedFromLocation(feedLocation).ContinueWith(antecedent => {
                    if (antecedent.Result != null) {
                        if (
                            !(from feed in feedCollection
                                where feed.Location.Equals(feedLocation, StringComparison.CurrentCultureIgnoreCase)
                                select feed).Any()) {
                            feedCollection.Add(antecedent.Result);
                        }
                    }
                }, TaskContinuationOptions.AttachedToParent );
            }
            return Task.Factory.StartNew(() => { });
        }

        /// <summary>
        /// Gets the feed locations.
        /// </summary>
        /// <param name="feedCollection">The feed collection.</param>
        /// <returns></returns>
        /// <remarks>
        /// NOTE: This is probably gettin' refactored PFQ
        /// </remarks>
        internal static IEnumerable<string> GetFeedLocations(this ObservableCollection<PackageFeed> feedCollection) {
            return from feed in feedCollection select feed.Location;
        }

        /// <summary>
        /// finds packages that match the given wildcard-mask
        /// </summary>
        /// <param name="packageSet">The package set to search thru.</param>
        /// <param name="wildcardMatch">The cosmetic name to match against.</param>
        /// <returns></returns>
        /// <remarks></remarks>
        internal static IEnumerable<Package> Match(this IEnumerable<Package> packageSet, string wildcardMatch) {
            return from p in packageSet where p.CosmeticName.IsWildcardMatch(wildcardMatch) select p;
        }

        /// <summary>
        /// This gets the highest version of all the packages in the set.
        /// </summary>
        /// <param name="packageSet">The package set.</param>
        /// <returns>the filtered colleciton of packages</returns>
        /// <remarks></remarks>
        internal static Package[] HighestPackages(this IEnumerable<Package> packageSet ) {
            var all = packageSet.ToArray();
            if (all.Length > 1) {
                var names = (from p in all group p by new { p.Name, p.Architecture }).ToArray();
                return names.Select(each => (from package in all 
                                             from uniq in names
                                             where package.Name == each.Key.Name && package.Architecture == each.Key.Architecture
                                             select package).OrderByDescending(p => p.Version).FirstOrDefault()).ToArray();
            }
            return all;
        }

        /* Beta1 
        
        public static IEnumerable<Package> SupercedentPackages(this IEnumerable<Package> packageSet, Package package) {
            // DebugMessage.Invoke.WriteLine(string.Format( "Scanning for package [{0}]", package.Name.SingleItemAsEnumerable()));
            // anything superceedent in the list of known packages?
            Registrar.ScanForPackages(package);

            return packageSet.Where(p => p.Architecture == package.Architecture &&
                p.PublicKeyToken == package.PublicKeyToken &&
                p.Name.Equals(package.Name, StringComparison.CurrentCultureIgnoreCase) &&
                p.PolicyMinimumVersion <= package.Version &&
                p.PolicyMaximumVersion >= package.Version).OrderByDescending(p => p.Version);
        }

        public static IEnumerable<Package> AllVersionsOfPackage(this IEnumerable<Package> packageSet, Package package)
        {
            return from p in packageSet
                   where p.Architecture == package.Architecture &&
                         p.PublicKeyToken == package.PublicKeyToken &&
                         p.Name.Equals(package.Name, StringComparison.CurrentCultureIgnoreCase)
                   select p;
        }

        public static IEnumerable<IGrouping<Package, Package>> LatestSupercedentPackages(this IEnumerable<Package> packageSet, Package package)
        {
            var tempPkgs = packageSet.AllVersionsOfPackage(package).OrderByDescending((p) => p.Version);
            PrivateGroup currentGroup = null;
            foreach (var p in tempPkgs)
            {
                if (currentGroup == null)
                {
                    currentGroup = new PrivateGroup {Key = p};
                    continue;
                }
                if (currentGroup.Key.Supercedes(p))
                {
                    currentGroup.Add(p);
                }
                else
                {
                    yield return currentGroup;
                    currentGroup = new PrivateGroup {Key = p};
                }

            }

            yield break;
        }


        private class PrivateGroup : IGrouping<Package, Package>
        {
            private readonly IList<Package> _pkgs = new List<Package>();
            private Package _key;
            public Package Key { 
                get { return _key; }
                set { _key = value;
                Add(_key);} 
            }

            public IEnumerator<Package> GetEnumerator()
            {
                return _pkgs.GetEnumerator();
            }

            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() {
                return GetEnumerator();
            }

            public void Add(Package pkg)
            {
                _pkgs.Add(pkg);
            }
        }
beta 1 */ 
    }
}
