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

namespace CoApp.Toolkit.Tasks {
    using System;
    using System.Collections.Generic;
    using System.Linq;

    public delegate object GetSessionCache(Type type, Func<object> constructor);

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

    public class SessionCache<T> : Cache<T> where T : class {
        private static Dictionary<Type, object> _nullSessionCache = new Dictionary<Type, object>();

        public new static SessionCache<T> Value {
            get {
                SessionCache<T> result = null;
                try {
                    result = (Event<GetSessionCache>.Raise(typeof (T), () => new SessionCache<T>())) as SessionCache<T>;
                } catch {
                }
                if (result == null) {
                    var type = typeof (T);
                    lock (_nullSessionCache) {
                        if (!_nullSessionCache.ContainsKey(type)) {
                            _nullSessionCache.Add(type, new SessionCache<T>());
                        }
                        result = _nullSessionCache[type] as SessionCache<T>;
                    }
                }
                return result;
            }
        }

        public override T this[string index] {
            get {
                if (index == null) {
                    return default(T);
                }
                // check current cache.
                return _cache.ContainsKey(index) ? _cache[index] : GetAndRememberDelegateValue(index) ?? Cache<T>.Value[index];
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

        public override IEnumerable<string> Keys {
            get {
                return _cache.Keys.Union(Cache<T>.Value.Keys);
            }
        }

        public override IEnumerable<T> Values {
            get {
                return _cache.Values.AsEnumerable().Union(Cache<T>.Value.Values);
            }
        }

        public IEnumerable<string> SessionKeys {
            get {
                return _cache.Keys;
            }
        }

        public IEnumerable<T> SessionValues {
            get {
                return _cache.Values.AsEnumerable();
            }
        }
    }
}