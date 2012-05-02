//-----------------------------------------------------------------------
// <copyright company="CoApp Project">
//     Copyright (c) 2011 Garrett Serack . All rights reserved.
// </copyright>
// <license>
//     The software is licensed under the Apache 2.0 License (the "License")
//     You may not use the software except in compliance with the License. 
// </license>
//-----------------------------------------------------------------------

namespace CoApp.Toolkit.Collections {
    using System.Collections;
    using System.Collections.Generic;

    /// <summary>
    ///   This behaves like a regular dictionary, except: add operations will silently replace existing values the inedexer [] will silently add values Getting values will return default(TValue) instead of throwing on no element. setting a value to null removes the key
    /// </summary>
    /// <typeparam name="TKey"> </typeparam>
    /// <typeparam name="TValue"> </typeparam>
    public class EasyDictionary<TKey, TValue> : IDictionary<TKey, TValue> where TKey : class where TValue : class {
        private readonly IDictionary<TKey, TValue> _dictionary;

        public EasyDictionary() {
            _dictionary = new Dictionary<TKey, TValue>();
        }

        public EasyDictionary(IDictionary<TKey, TValue> dictionary) {
            _dictionary = dictionary;
        }

        public void AddPair(object key, object value) {
            Add( key as TKey, value as TValue);
        }

        public void Add(TKey key, TValue value) {
            // ReSharper disable HeuristicUnreachableCode
            if (value == null) {
                // what if value is already null, eh? 
                _dictionary.Remove(key);
                return;
            }
            // ReSharper restore HeuristicUnreachableCode

            if (_dictionary.ContainsKey(key)) {
                _dictionary[key] = value;
            } else {
                _dictionary.Add(key, value);
            }
        }

        public bool ContainsKey(TKey key) {
            return _dictionary.ContainsKey(key);
        }

        public ICollection<TKey> Keys {
            get {
                return _dictionary.Keys;
            }
        }

        public bool Remove(TKey key) {
            return _dictionary.Remove(key);
        }

        public bool TryGetValue(TKey key, out TValue value) {
            return _dictionary.TryGetValue(key, out value);
        }

        public ICollection<TValue> Values {
            get {
                return _dictionary.Values;
            }
        }

        public TValue this[TKey key] {
            get {
                return _dictionary.ContainsKey(key) ? _dictionary[key] : default(TValue);
            }
            set {
                if (!_dictionary.ContainsKey(key)) {
                    _dictionary.Add(key, value);
                }
                _dictionary[key] = value;
            }
        }

        public void Add(KeyValuePair<TKey, TValue> item) {
            this[item.Key] = item.Value;
        }

        public void Clear() {
            _dictionary.Clear();
        }

        public bool Contains(KeyValuePair<TKey, TValue> item) {
            return _dictionary.Contains(item);
        }

        public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex) {
            _dictionary.CopyTo(array, arrayIndex);
        }

        public int Count {
            get {
                return _dictionary.Count;
            }
        }

        public bool IsReadOnly {
            get {
                return _dictionary.IsReadOnly;
            }
        }

        public bool Remove(KeyValuePair<TKey, TValue> item) {
            return _dictionary.Remove(item);
        }

        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator() {
            return _dictionary.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() {
            return GetEnumerator();
        }
    }
}