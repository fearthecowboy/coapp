//-----------------------------------------------------------------------
// <copyright company="CoApp Project">
//     Copyright (c) 2011 Garrett Serack . All rights reserved.
// </copyright>
// <license>
//     The software is licensed under the Apache 2.0 License (the "License")
//     You may not use the software except in compliance with the License. 
// </license>
//-----------------------------------------------------------------------

namespace CoApp.Toolkit.Extensions {
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Collections.Specialized;
    using System.Linq;

    /// <summary>
    ///   Represents a dynamic data collection that provides notifications when items get added, removed, or when the whole list is refreshed and allows sorting.
    /// </summary>
    /// <typeparam name="T"> The type of elements in the collection. </typeparam>
    public class SortedObservableCollection<T> : ObservableCollection<T> where T : IComparable {
        protected bool skipEvent;

        protected override void InsertItem(int index, T item) {
            lock (this) {
                base.InsertItem(index, item);
                skipEvent = true;
                var sorted = this.OrderBy(x => x).ToList();
                for (var i = 0; i < sorted.Count(); i++) {
                    Move(IndexOf(sorted[i]), i);
                }
                skipEvent = false;
            }
        }

        /// <summary>
        ///   suppresses events when skipevent == true. This makes it so that an insert doesn't cause a whole bunch of events.
        /// </summary>
        /// <param name="e"> </param>
        protected override void OnCollectionChanged(NotifyCollectionChangedEventArgs e) {
            if (!skipEvent) {
                base.OnCollectionChanged(e);
            }
        }

        /// <summary>
        ///   Sorts the items of the collection in ascending order according to a key.
        /// </summary>
        /// <typeparam name="TKey"> The type of the key returned by <paramref name="keySelector" /> . </typeparam>
        /// <param name="keySelector"> A function to extract a key from an item. </param>
        public void Sort<TKey>(Func<T, TKey> keySelector) {
            InternalSort(Items.OrderBy(keySelector));
        }

        /// <summary>
        ///   Sorts the items of the collection in ascending order according to a key.
        /// </summary>
        /// <typeparam name="TKey"> The type of the key returned by <paramref name="keySelector" /> . </typeparam>
        /// <param name="keySelector"> A function to extract a key from an item. </param>
        /// <param name="comparer"> An <see cref="IComparer{T}" /> to compare keys. </param>
        public void Sort<TKey>(Func<T, TKey> keySelector, IComparer<TKey> comparer) {
            InternalSort(Items.OrderBy(keySelector, comparer));
        }

        /// <summary>
        ///   Moves the items of the collection so that their orders are the same as those of the items provided.
        /// </summary>
        /// <param name="sortedItems"> An <see cref="IEnumerable{T}" /> to provide item orders. </param>
        private void InternalSort(IEnumerable<T> sortedItems) {
            var sortedItemsList = sortedItems.ToList();

            foreach (var item in sortedItemsList) {
                Move(IndexOf(item), sortedItemsList.IndexOf(item));
            }
        }
    }

    /// <summary>
    ///   Extension method to sort a regular observable collection.
    /// </summary>
    public static class ObservableExtensions {
        public static void Sort<T>(this ObservableCollection<T> collection) where T : IComparable {
            var sorted = collection.OrderBy(x => x).ToList();
            for (var i = 0; i < sorted.Count(); i++) {
                collection.Move(collection.IndexOf(sorted[i]), i);
            }
        }
    }
}