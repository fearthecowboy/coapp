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
    using System.Threading.Tasks;
    using Common;
    using Feeds;

    /// <summary>
    ///   Extension methods to make queries on package sets easier.
    /// </summary>
    /// <remarks>
    /// </remarks>
    public static class PackageCollectionExtensions {
        /// <summary>
        ///   Adds the feed location.
        /// </summary>
        /// <param name="feedCollection"> The feed collection. </param>
        /// <param name="feedLocation"> The feed location. </param>
        /// <returns> </returns>
        /// <remarks>
        ///   NOTE: This is probably gettin' refactored PFQ
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
                }, TaskContinuationOptions.AttachedToParent);
            }
            return Task.Factory.StartNew(() => {
            });
        }

        /// <summary>
        ///   Gets the feed locations.
        /// </summary>
        /// <param name="feedCollection"> The feed collection. </param>
        /// <returns> </returns>
        /// <remarks>
        ///   NOTE: This is probably gettin' refactored PFQ
        /// </remarks>
        internal static IEnumerable<string> GetFeedLocations(this ObservableCollection<PackageFeed> feedCollection) {
            return from feed in feedCollection select feed.Location;
        }

      

        internal static Package[] HighestPackages(this IEnumerable<Package> packageSet) {
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