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
    using System.Linq;

    public class Cache<T> where T : class {
        public static Cache<T> Value = new Cache<T>();

        protected Dictionary<string, T> _cache = new Dictionary<string, T>();
        protected Dictionary<string, List<Func<string, T>>> _delegateCache = new Dictionary<string, List<Func<string, T>>>();

        protected T GetAndRememberDelegateValue(string index) {
            T result = null;
            lock (_delegateCache) {
                lock (_cache) {
                    if (_delegateCache.ContainsKey(index)) {
                        foreach (var dlg in _delegateCache[index]) {
                            result = dlg(index);
                            if (result != null) {
                                _cache.Add(index, result);
                                break;
                            }
                        }
                    }
                }
            }
            return result;
        }

        public virtual T this[string index] {
            get {
                if (index == null) {
                    return default(T);
                }
                return _cache.ContainsKey(index) ? _cache[index] : GetAndRememberDelegateValue(index);
            }
            set {
                lock (_cache) {
                    if (_cache.ContainsKey(index)) {
                        _cache[index] = value;
                    } else {
                        _cache.Add(index, value);
                    }
                }
            }
        }

        /// <summary>
        ///   Adds a function delegate to the cache that can get the value requested This adds the delegate at the bottom of the list of possible functions that can get the value requested.
        /// </summary>
        /// <param name="index"> </param>
        /// <param name="delegte"> </param>
        public virtual void Add(string index, Func<string, T> delegte) {
            lock (_delegateCache) {
                if (!_delegateCache.ContainsKey(index)) {
                    _delegateCache.Add(index, new List<Func<string, T>>());
                }
                _delegateCache[index].Add(delegte);
            }
        }

        public virtual void Insert(string index, Func<string, T> delegte) {
            lock (_delegateCache) {
                if (!_delegateCache.ContainsKey(index)) {
                    _delegateCache.Add(index, new List<Func<string, T>>());
                }
                _delegateCache[index].Insert(0, delegte);
            }
        }

        public virtual void ReplaceOrAdd(string index, Func<string, T> delegte) {
            lock (_delegateCache) {
                if (!_delegateCache.ContainsKey(index)) {
                    _delegateCache.Add(index, new List<Func<string, T>>());
                }
                _delegateCache[index].Clear();
                _delegateCache[index].Insert(0, delegte);
            }
        }

        public virtual void Clear() {
            lock (_cache) {
                _cache.Clear();
            }
        }

        public virtual void Clear(string index) {
            lock (_cache) {
                if (_cache.ContainsKey(index)) {
                    _cache.Remove(index);
                }
            }
        }

        public virtual void Wipe() {
            _cache.Clear();
            lock (_delegateCache) {
                _delegateCache.Clear();
            }
        }

        public virtual void Wipe(string index) {
            Clear(index);
            lock (_delegateCache) {
                if (_delegateCache.ContainsKey(index)) {
                    _delegateCache.Remove(index);
                }
            }
        }

        public virtual IEnumerable<string> Keys {
            get {
                return _cache.Keys;
            }
        }

        public virtual IEnumerable<T> Values {
            get {
                return _cache.Values.AsEnumerable();
            }
        }
    }
}