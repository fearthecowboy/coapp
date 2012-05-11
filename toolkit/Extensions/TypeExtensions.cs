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

namespace CoApp.Toolkit.Extensions {
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using Collections;

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class SkipAttribute : Attribute {
    }

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property )]
    public class IncludeAttribute : Attribute {
    }


    internal enum PersistableType {
        Parseable,
        Array,
        Enumerable,
        Dictionary,
        Nullable,
        String,
        Enumeration,
        Other
    }

    public class PersistableInfo {
        internal PersistableInfo(Type persistableType) {
            if (persistableType.IsEnum) {
                PersistableType = PersistableType.Enumeration;
                Type = persistableType;
                return;
            } 

            if (persistableType == typeof(string)) {
                PersistableType = PersistableType.String;
                Type = persistableType;
                return;
            } 

            if( typeof(IDictionary).IsAssignableFrom(persistableType) ) {
                PersistableType = PersistableType.Dictionary;
                Type = persistableType;
                if( persistableType.IsGenericType) {
                    var genericArguments = persistableType.GetGenericArguments();
                    DictionaryKeyType = genericArguments[0];
                    DictionaryValueType = genericArguments[1];
                } else {
                    DictionaryKeyType = typeof(object);
                    DictionaryValueType = typeof(object);
                }
                return;
            }

            if (typeof(IEnumerable).IsAssignableFrom(persistableType)) {
                PersistableType = PersistableType.Enumerable;
                Type = persistableType;
                ElementType = persistableType.IsGenericType ? persistableType.GetGenericArguments()[0]: typeof(object);
                return;
            }
            
            if (persistableType.IsGenericType) {
                // better be Nullable
                switch (persistableType.Name.Split('`')[0]) {
                    case "Nullable":
                        PersistableType = PersistableType.Nullable;
                        NullableType = persistableType.GetGenericArguments()[0];
                        return;
                }
            }
            
            if (persistableType.IsArray) {
                // an array of soemthing.
                PersistableType = PersistableType.Array;
                ElementType = persistableType.GetElementType();
                Type = persistableType;
                return;
            }

            if (persistableType.IsParsable()) {
                PersistableType = PersistableType.Parseable;
                Type = persistableType;
                return;
            }

            PersistableType = PersistableType.Other;
            Type = persistableType;
        }

        internal PersistableType PersistableType { get; set; }
        internal Type Type { get; set; }
        internal Type ElementType { get; set; }
        internal Type DictionaryKeyType { get; set; }
        internal Type DictionaryValueType { get; set; }
        internal Type NullableType {
            get {
                return Type;
            }
            set {
                Type = value;
            }
        }
    }

    public static class AutoCache {
        private static class C<TKey, TValue> {
            internal static readonly IDictionary<TKey, TValue> Cache = new XDictionary<TKey, TValue>();
        }
        public static TValue Get<TKey, TValue>(TKey key, Func<TValue> valueFunc) {
            if (!C<TKey, TValue>.Cache.ContainsKey(key)) {
                C<TKey, TValue>.Cache[key] = valueFunc();
            }
            return C<TKey, TValue>.Cache[key];
        }
    }

    public class PersistableElements {
        public FieldInfo[] Fields { get; set; }
        public PropertyInfo[] Properties { get; set; }
    }

    public static class TypeExtensions {
        private static readonly IDictionary<Type, MethodInfo> TryParsers = new XDictionary<Type, MethodInfo>();
        private static readonly IDictionary<Type, ConstructorInfo> TryStrings = new XDictionary<Type, ConstructorInfo>();
        private static readonly MethodInfo CastMethod = typeof(Enumerable).GetMethod("Cast");
        private static readonly MethodInfo ToArrayMethod = typeof(Enumerable).GetMethod("ToArray");
        private static readonly IDictionary<Type, MethodInfo> CastMethods = new XDictionary<Type, MethodInfo>();
        private static readonly IDictionary<Type, MethodInfo> ToArrayMethods = new XDictionary<Type, MethodInfo>();

        public static PersistableInfo GetPersistableInfo(this Type t) {
            return AutoCache.Get(t, () => new PersistableInfo(t));
        }

        public static PersistableElements GetPersistableElements(this Type type) {
            return AutoCache.Get(type, () => 
                new PersistableElements {
                    Fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Where(each => 
                        !each.IsInitOnly && !each.GetCustomAttributes(typeof (SkipAttribute), true).Any() && (
                            each.IsPublic || 
                            each.GetCustomAttributes(typeof (IncludeAttribute), true).Any())
                        ).ToArray(),

                    Properties = type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Where(each => {

                        var sm = each.GetSetMethod(true);
                        var gm = each.GetGetMethod(true);

                        return (sm != null && gm != null) && !each.GetCustomAttributes(typeof (SkipAttribute), true).Any() && (
                            (each.GetSetMethod(true).IsPublic && each.GetGetMethod(true).IsPublic) ||
                                each.GetCustomAttributes(typeof (IncludeAttribute), true).Any());
                    }).ToArray()
                });
        
        }

        public static object ToArrayOfType(this IEnumerable<object> enumerable, Type collectionType) {
            return ToArrayMethods.GetOrAdd(collectionType, () => ToArrayMethod.MakeGenericMethod(collectionType))
                .Invoke(null, new[] { enumerable.CastToIEnumerableOfType( collectionType ) });
        }

        public static object CastToIEnumerableOfType(this IEnumerable<object> enumerable, Type collectionType  ) {
            return CastMethods.GetOrAdd(collectionType, () => CastMethod.MakeGenericMethod(collectionType)).Invoke(null, new object[] { enumerable });
        }

        public static bool IsCreateable(this Type type) {
            return AutoCache.Get(type, () => type.GetConstructor(BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null)) != null;
        }

        public static object CreateInstance(this Type type) {
            return Activator.CreateInstance(type);
            // return AutoCache.Get(type, () => type.GetConstructor(BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null)).Invoke(null);
        }

        private static MethodInfo GetTryParse(Type parsableType) {
            lock (TryParsers) {
                if (!TryParsers.ContainsKey(parsableType)) {
                    if (parsableType.IsPrimitive || parsableType.IsValueType || parsableType.GetConstructor(new Type[] {}) != null) {
                        TryParsers.Add(parsableType, parsableType.GetMethod("TryParse", new[] {typeof (string), parsableType.MakeByRefType()}));
                    } else {
                        // if they don't have a default constructor, 
                        // it's not going to be 'parsable'
                        TryParsers.Add(parsableType, null);
                    }
                }
            }
            return TryParsers[parsableType];
        }

        private static ConstructorInfo GetStringConstructor(Type parsableType) {
            lock (TryStrings) {
                if (!TryStrings.ContainsKey(parsableType)) {
                    TryStrings.Add(parsableType, parsableType.GetConstructor(new[] {typeof (string)}));
                }
            }
            return TryStrings[parsableType];
        }

        public static bool IsConstructableFromString(this Type stringableType) {
            return GetStringConstructor(stringableType) != null;
        }

        public static bool IsParsable(this Type parsableType) {
            if (parsableType.IsDictionary() || parsableType.IsArray) {
                return false;
            }
            return parsableType.IsEnum || parsableType == typeof(string) || GetTryParse(parsableType) != null || IsConstructableFromString(parsableType);
        }

        public static object ParseString(this Type parsableType, string value) {
            if (parsableType.IsEnum) {
                return Enum.Parse(parsableType, value);
            }

            if( parsableType == typeof(string)) {
                return value;
            }

            var tryParse = GetTryParse(parsableType);

            if (tryParse != null) {
                if (!string.IsNullOrEmpty(value)) {
                    var pz = new[] {value, Activator.CreateInstance(parsableType)};
                    
                    // returns the default value if it's not successful.
                    tryParse.Invoke(null, pz);
                    return pz[1];
                }
                return Activator.CreateInstance(parsableType);
            }

            return value == null ? null : GetStringConstructor(parsableType).Invoke(new object[] {value});
        }

        public static bool IsDictionary(this Type dictionaryType) {
            return typeof (IDictionary).IsAssignableFrom(dictionaryType);
        }

        public static bool IsIEnumerable(this Type ienumerableType) {
            return typeof (IEnumerable).IsAssignableFrom(ienumerableType);
        }
    }
}