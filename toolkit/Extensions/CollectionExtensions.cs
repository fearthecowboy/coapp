//-----------------------------------------------------------------------
// <copyright company="CoApp Project">
//     Original Copyright (c) 2009 Microsoft Corporation. All rights reserved.
//     Changes Copyright (c) 2010  Garrett Serack. All rights reserved.
// </copyright>
// <license>
//     The software is licensed under the Apache 2.0 License (the "License")
//     You may not use the software except in compliance with the License. 
// </license>
//-----------------------------------------------------------------------

// -----------------------------------------------------------------------
// Original Code: 
// (c) 2009 Microsoft Corporation -- All rights reserved
// This code is licensed under the MS-PL
// http://www.opensource.org/licenses/ms-pl.html
// Courtesy of the Open Source Techology Center: http://port25.technet.com
// -----------------------------------------------------------------------

namespace CoApp.Toolkit.Extensions {
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Linq;
    using System.Text;
    using Collections;
    using Exceptions;

    /// <summary>
    /// </summary>
    /// <remarks>
    /// </remarks>
    public static class CollectionExtensions {
        /// <summary>
        ///   Splits a string into a List of strings.
        /// </summary>
        /// <param name="str"> The STR. </param>
        /// <param name="separator"> The separator. </param>
        /// <returns> </returns>
        /// <remarks>
        /// </remarks>
        public static List<string> SplitToList(this string str, params char[] separator) {
            var result = new List<string>();
            if (!string.IsNullOrEmpty(str)) {
                result.AddRange(str.Split(separator));
            }

            return result;
        }

        /// <summary>
        ///   Removes duplicate strings from a list.
        /// </summary>
        /// <param name="collection"> The collection. </param>
        /// <param name="stringComparison"> The string comparison. </param>
        /// <returns> </returns>
        /// <remarks>
        /// </remarks>
        public static List<string> Uniq(this IEnumerable<string> collection, StringComparison stringComparison) {
            return Uniq((collection is List<string>) ? (collection as List<string>) : collection.ToList(), stringComparison);
        }

        /// <summary>
        ///   Removes duplicate strings from a list. Assumes Case Sensitivity.
        /// </summary>
        /// <param name="collection"> The collection. </param>
        /// <returns> </returns>
        /// <remarks>
        /// </remarks>
        public static List<string> Uniq(this IEnumerable<string> collection) {
            return Uniq((collection is List<string>) ? (collection as List<string>) : collection.ToList());
        }

        /// <summary>
        ///   Removes duplicate strings from a list.
        /// </summary>
        /// <param name="list"> The list. </param>
        /// <param name="stringComparison"> The string comparison. </param>
        /// <returns> </returns>
        /// <remarks>
        /// </remarks>
        public static List<string> Uniq(this List<string> list, StringComparison stringComparison) {
            for (var i = 0; i < list.Count; i++) {
                for (var j = list.Count - 1; j > i; j--) {
                    if (list[i].Equals(list[j], stringComparison)) {
                        list.RemoveAt(j);
                    }
                }
            }
            return list;
        }

        /// <summary>
        ///   Removes duplicate strings from a list. Assumes Case Sensitivity.
        /// </summary>
        /// <param name="list"> The list. </param>
        /// <returns> </returns>
        /// <remarks>
        /// </remarks>
        public static List<string> Uniq(this List<string> list) {
            for (var i = 0; i < list.Count; i++) {
                for (var j = list.Count - 1; j > i; j--) {
                    if (list[i].Equals(list[j])) {
                        list.RemoveAt(j);
                    }
                }
            }
            return list;
        }

        /// <summary>
        ///   Combines a list of strings into a single string seperated by seperator
        /// </summary>
        /// <param name="list"> The list. </param>
        /// <param name="separator"> The separator. </param>
        /// <returns> </returns>
        /// <remarks>
        /// </remarks>
        public static string Combine(this List<string> list, char separator) {
            var sb = new StringBuilder();
            foreach (var s in list) {
                if (sb.Length > 0) {
                    sb.Append(separator);
                }
                sb.Append(s);
            }
            return sb.ToString();
        }

        /// <summary>
        ///   Combines a list of strings into a single string seperated by seperator
        /// </summary>
        /// <param name="list"> The list. </param>
        /// <param name="separator"> The separator. </param>
        /// <returns> </returns>
        /// <remarks>
        /// </remarks>
        public static string Combine(this List<string> list, string separator) {
            var sb = new StringBuilder();
            foreach (var s in list) {
                if (sb.Length > 0) {
                    sb.Append(separator);
                }
                sb.Append(s);
            }
            return sb.ToString();
        }

        /// <summary>
        ///   Adds the contents of one collection to another.
        /// </summary>
        /// <typeparam name="T"> </typeparam>
        /// <param name="destination"> The destination. </param>
        /// <param name="collection"> The collection. </param>
        /// <remarks>
        /// </remarks>
        public static void AddRange<T>(this Collection<T> destination, IEnumerable<T> collection) {
            foreach (var i in collection) {
                destination.Add(i);
            }
        }

        /// <summary>
        ///   Determines whether the collection object is either null or an empty collection.
        /// </summary>
        /// <typeparam name="T"> </typeparam>
        /// <param name="collection"> The collection. </param>
        /// <returns> <c>true</c> if [is null or empty] [the specified collection]; otherwise, <c>false</c> . </returns>
        /// <remarks>
        /// </remarks>
        public static bool IsNullOrEmpty<T>(this IEnumerable<T> collection) {
            return collection == null || !collection.Any();
        }

        public static bool IsNullOrEmpty<T,V>(this IDictionary<T,V> dictionary) {
            return dictionary == null || !dictionary.Keys.Any();
        }

        public static TValue AddOrSet<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, TKey key, TValue value) where TValue : class {
            if (dictionary.ContainsKey(key)) {
                dictionary[key] = value;
            } else {
                dictionary.Add(key, value);
            }
            return value;
        }

        public static TValue GetOrAdd<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, TKey key, Func<TValue> valueFunction) where TValue : class {
            return dictionary.ContainsKey(key) ? dictionary[key] : dictionary.AddOrSet(key, valueFunction());
        }

        public static void AddObjectPairPair<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, object key, object value) where TKey : class where TValue : class {
            dictionary.Add(key as TKey, value as TValue);
        }

        public static void AddUnique<TValue>(this IList<TValue> list, TValue value) {
            if(object.ReferenceEquals(null, value)) {
                return;
            }
            
            if( !list.Contains(value)) {
                list.Add(value);
            }
        }

        public static void AddRangeUnique<TValue>(this IList<TValue> list, IEnumerable<TValue> values) {
            if (values == null) {
                return;
            }
            foreach(var item in values) {
                list.AddUnique(item);
            }
        }

        public static XList<TSource> ToXList<TSource>(this IEnumerable<TSource> source) {
            return source == null ? new XList<TSource>() : new XList<TSource>(source);
        }

        internal class IdentityFunction<TElement> {
            public static Func<TElement, TElement> Instance {
                get { return x => x; }
            }
        }

        public static XDictionary<TKey, TSource> ToXDictionary<TSource, TKey>(this IEnumerable<TSource> source, Func<TSource, TKey> keySelector) {
            return ToXDictionary<TSource, TKey, TSource>(source, keySelector, IdentityFunction<TSource>.Instance, null);
        }

        public static XDictionary<TKey, TSource> ToXDictionary<TSource, TKey>(this IEnumerable<TSource> source, Func<TSource, TKey> keySelector, IEqualityComparer<TKey> comparer) {
            return ToXDictionary<TSource, TKey, TSource>(source, keySelector, IdentityFunction<TSource>.Instance, comparer);
        }

        public static XDictionary<TKey, TElement> ToXDictionary<TSource, TKey, TElement>(this IEnumerable<TSource> source, Func<TSource, TKey> keySelector, Func<TSource, TElement> elementSelector) {
            return ToXDictionary<TSource, TKey, TElement>(source, keySelector, elementSelector, null);
        }

        public static XDictionary<TKey, TElement> ToXDictionary<TSource, TKey, TElement>(this IEnumerable<TSource> source, Func<TSource, TKey> keySelector, Func<TSource, TElement> elementSelector, IEqualityComparer<TKey> comparer) {
            if (source == null) {
                throw new CoAppException("ToXDictionary (source) value null.");
            }
            if (keySelector == null) {
                throw new CoAppException("ToXDictionary (keySelector) value null.");
            }
            if (elementSelector == null) {
                throw new CoAppException("ToXDictionary (elementSelector) value null.");
            }

            var d = new XDictionary<TKey, TElement>(comparer);
            foreach (var element in source) {
                d.Add(keySelector(element), elementSelector(element));
            }
            return d;
        }

#if !COAPP_ENGINE_CORE
        public static IDictionary<string, IEnumerable<string>> Merge(this IDictionary<string, IEnumerable<string>> result, IDictionary<string, IEnumerable<string>> more) {
            foreach (var k in more.Keys) {
                if (result.ContainsKey(k)) {
                    result[k] = result[k].Union(more[k]).Distinct();
                } else {
                    result.Add(k, more[k]);
                }
            }
            return result;
        }

        public static TValue GetOrDefault<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, TKey key) {
            TValue value;
            dictionary.TryGetValue(key, out value);
            return value;
        }

        /// <summary>
        ///   A way to use the Add method more like a Concat. Very useful for Aggregate. Instead of returning void, this returns the list itself.
        /// </summary>
        /// <typeparam name="T"> The type of the element </typeparam>
        /// <param name="list"> </param>
        /// <param name="item"> The item to add to the list </param>
        /// <returns> The list after the new <paramref name="item" /> has been added. </returns>
        public static IList<T> LAdd<T>(this IList<T> list, T item) {
            list.Add(item);

            return list;
        }

#endif
    }
}