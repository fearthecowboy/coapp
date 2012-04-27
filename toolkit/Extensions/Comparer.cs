//-----------------------------------------------------------------------
// <copyright company="CoApp Project">
//     Copyright (c) 2011 Garrett Serack. All rights reserved.
// </copyright>
// <license>
//     The software is licensed under the Apache 2.0 License (the "License")
//     You may not use the software except in compliance with the License. 
// </license>
//-----------------------------------------------------------------------

namespace CoApp.Toolkit.Extensions {
    using System;
    using System.Collections.Generic;
    using System.Text.RegularExpressions;

    /// <summary>
    /// A generic implementation of a comparer that takes a delegate to compare with
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <remarks></remarks>
    public class Comparer<T> : IComparer<T> {
        /// <summary>
        /// 
        /// </summary>
        private readonly Func<T, T, int> _compareFuction;
        /// <summary>
        /// Initializes a new instance of the <see cref="Comparer&lt;T&gt;"/> class.
        /// </summary>
        /// <param name="compareFn">The compare fn.</param>
        /// <remarks></remarks>
        public Comparer(Func<T,T,int> compareFn) {
            _compareFuction = compareFn;
        }
        /// <summary>
        /// Compares two objects and returns a value indicating whether one is less than, equal to, or greater than the other.
        /// </summary>
        /// <param name="x">The first object to compare.</param>
        /// <param name="y">The second object to compare.</param>
        /// <returns>Value
        /// Condition
        /// Less than zero
        /// <paramref name="x"/> is less than <paramref name="y"/>.
        /// Zero
        /// <paramref name="x"/> equals <paramref name="y"/>.
        /// Greater than zero
        /// <paramref name="x"/> is greater than <paramref name="y"/>.</returns>
        /// <remarks></remarks>
        public int Compare(T x, T y) {
            return _compareFuction(x,y);
        }
    }

    /// <summary>
    /// A generic implementation of a EqualityComparer that takes a delegate to compare with
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <remarks></remarks>
     public class EqualityComparer<T> : IEqualityComparer<T> {
         /// <summary>
         /// 
         /// </summary>
        private readonly Func<T, T, bool> _equalityCompareFn;
        /// <summary>
        /// 
        /// </summary>
        private readonly Func<T, int> _getHashCodeFn;
        /// <summary>
        /// Initializes a new instance of the <see cref="EqualityComparer&lt;T&gt;"/> class.
        /// </summary>
        /// <param name="equalityCompareFn">The equality compare fn.</param>
        /// <param name="getHashCodeFn">The get hash code fn.</param>
        /// <remarks></remarks>
        public EqualityComparer(Func<T,T,bool> equalityCompareFn,Func<T,int> getHashCodeFn) {
            _equalityCompareFn= equalityCompareFn;
            _getHashCodeFn = getHashCodeFn;
        }

        /// <summary>
        /// Determines whether the specified objects are equal.
        /// </summary>
        /// <param name="x">The first object of type <paramref name="T"/> to compare.</param>
        /// <param name="y">The second object of type <paramref name="T"/> to compare.</param>
        /// <returns>true if the specified objects are equal; otherwise, false.</returns>
        /// <remarks></remarks>
         public bool Equals(T x, T y) {
             return _equalityCompareFn(x, y);
         }

         /// <summary>
         /// Returns a hash code for this instance.
         /// </summary>
         /// <param name="obj">The <see cref="T:System.Object"/> for which a hash code is to be returned.</param>
         /// <returns>A hash code for this instance, suitable for use in hashing algorithms and data structures like a hash table.</returns>
         /// <exception cref="T:System.ArgumentNullException">
         /// The type of <paramref name="obj"/> is a reference type and <paramref name="obj"/> is null.
         ///   </exception>
         /// <remarks></remarks>
         public int GetHashCode(T obj) {
             return _getHashCodeFn(obj);
         }
     }

     public static class CompareExtensions {
         public static bool WithCompareResult(this int result, Func<bool> onLess, Func<bool> onMore, Func<bool> onEqual) {
             return result < 0 ? onLess() : (result > 0 ? onMore() : onEqual());
         }
         public static bool WithCompareResult(this int result, bool lessResult, bool moreResult, bool equalResult) {
             return result < 0 ? lessResult : (result > 0 ? moreResult : equalResult);
         }
         public static int WithCompareResult(this int result, int lessResult, int moreResult, int equalResult) {
             return result < 0 ? lessResult : (result > 0 ? moreResult : equalResult);
         }
     }

    public static class RegexExtensions {
        public static string GetValue(this Match match, string group, string _default = null) {
            return (match.Groups[@group].Success ? match.Groups[@group].Captures[0].Value : _default ?? String.Empty).Trim('-', ' ');
        }
    }
}
