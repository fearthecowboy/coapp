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
    using Toolkit.Collections;
    using Toolkit.Tasks;

    public class SessionCache<T> : Cache<T> where T : class {
        private static IDictionary<Type, object> _nullSessionCache = new XDictionary<Type, object>();

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