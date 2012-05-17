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
    using System.ComponentModel;
    using System.Linq;
    
    using System.Reflection;
    using Collections;
    

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class NotPersistableAttribute : Attribute {
    }

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property )]
    public class PersistableAttribute: Attribute {
        internal string Name = null;
        internal Type SerializeAsType = null;
        internal Type DeserializeAsType = null;

        public PersistableAttribute( string name = null, Type serializeAsType= null, Type deserializeAsType = null) {
            Name = name;
            SerializeAsType = serializeAsType;
            DeserializeAsType = deserializeAsType;
        }
    }

    internal enum PersistableCategory {
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
        internal PersistableInfo(Type type) {
            Type = type;

            if (type.IsEnum) {
                PersistableCategory = PersistableCategory.Enumeration;
                return;
            }

            if (type == typeof(string)) {
                PersistableCategory = PersistableCategory.String;
                return;
            }

            if (typeof(IDictionary).IsAssignableFrom(type)) {
                PersistableCategory = PersistableCategory.Dictionary;
                if (type.IsGenericType) {
                    var genericArguments = type.GetGenericArguments();
                    DictionaryKeyType = genericArguments[0];
                    DictionaryValueType = genericArguments[1];
                } else {
                    DictionaryKeyType = typeof(object);
                    DictionaryValueType = typeof(object);
                }
                return;
            }

            if (typeof(IEnumerable).IsAssignableFrom(type)) {
                PersistableCategory = PersistableCategory.Enumerable;
                ElementType = type.IsGenericType ? type.GetGenericArguments()[0] : typeof(object);
                return;
            }

            if (type.IsGenericType) {
                // better be Nullable
                switch (type.Name.Split('`')[0]) {
                    case "Nullable":
                        PersistableCategory = PersistableCategory.Nullable;
                        NullableType = type.GetGenericArguments()[0];
                        return;
                }
            }

            if (type.IsArray) {
                // an array of soemthing.
                PersistableCategory = PersistableCategory.Array;
                ElementType = type.GetElementType();
                return;
            }

            if (type.IsParsable()) {
                PersistableCategory = PersistableCategory.Parseable;
                return;
            }

            PersistableCategory = PersistableCategory.Other;
        }

        internal PersistableCategory PersistableCategory { get; set; }
        internal Type Type { get; set; }
        internal Type ElementType { get; set; }
        internal Type DictionaryKeyType { get; set; }
        internal Type DictionaryValueType { get; set; }
        internal Type NullableType  { get; set; }
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

    public class PersistablePropertyInformation {
        public string Name;
        public Type SerializeAsType;
        public Type DeserializeAsType;
        public Type ActualType;
        public Action<object, object,object[]> SetValue;
        public Func<object, object[], object> GetValue;
    }

    public static class TypeExtensions {
        private static readonly IDictionary<Type, MethodInfo> TryParsers = new XDictionary<Type, MethodInfo>();
        private static readonly IDictionary<Type, ConstructorInfo> TryStrings = new XDictionary<Type, ConstructorInfo>();
        private static readonly MethodInfo CastMethod = typeof(Enumerable).GetMethod("Cast");
        private static readonly MethodInfo ToArrayMethod = typeof(Enumerable).GetMethod("ToArray");
        private static readonly IDictionary<Type, MethodInfo> CastMethods = new XDictionary<Type, MethodInfo>();
        private static readonly IDictionary<Type, MethodInfo> ToArrayMethods = new XDictionary<Type, MethodInfo>();
        private static readonly IDictionary<Type, MethodInfo> OpImplicitMethods = new XDictionary<Type, MethodInfo>();
        public static readonly IDictionary<Type, Type> TypeSubtitution = new XDictionary<Type, Type>();

        public static PersistableInfo GetPersistableInfo(this Type t) {
            return AutoCache.Get(t, () => new PersistableInfo(t));
        }

        public static PersistablePropertyInformation[] GetPersistableElements(this Type type) {
            return AutoCache.Get(type, () => 
                (from each in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    let persistableAttribute = each.GetCustomAttributes(typeof (PersistableAttribute), true).FirstOrDefault() as PersistableAttribute
                where
                    !each.IsInitOnly && !each.GetCustomAttributes(typeof (NotPersistableAttribute), true).Any() && 
                    (each.IsPublic || persistableAttribute != null)
                select new PersistablePropertyInformation {
                    SetValue = (o, o1, arg3) => each.SetValue(o, o1),
                    GetValue = (o, objects) => each.GetValue(o),
                    SerializeAsType = (persistableAttribute != null ? persistableAttribute.SerializeAsType : null) ?? each.FieldType,
                    DeserializeAsType = (persistableAttribute != null ? persistableAttribute.DeserializeAsType : null) ?? each.FieldType,
                    ActualType = each.FieldType,
                    Name = (persistableAttribute != null ? persistableAttribute.Name : null) ?? each.Name
                 }).Union((from each in type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    let setMethodInfo = each.GetSetMethod(true)
                    let getMethodInfo = each.GetGetMethod(true)
                    let persistableAttribute = each.GetCustomAttributes(typeof (PersistableAttribute), true).FirstOrDefault() as PersistableAttribute
                where
                    ((setMethodInfo != null && getMethodInfo != null) &&
                    !each.GetCustomAttributes(typeof (NotPersistableAttribute), true).Any() &&
                    (each.GetSetMethod(true).IsPublic && each.GetGetMethod(true).IsPublic)) ||
                    persistableAttribute != null
                select new PersistablePropertyInformation {
                    SetValue = setMethodInfo != null ? new Action<object, object, object[]>(each.SetValue) : null,
                    GetValue = getMethodInfo != null ? new Func<object, object[], object>(each.GetValue) : null,
                    SerializeAsType = (persistableAttribute != null ? persistableAttribute.SerializeAsType : null) ?? each.PropertyType,
                    DeserializeAsType = (persistableAttribute != null ? persistableAttribute.DeserializeAsType : null) ?? each.PropertyType,
                    ActualType = each.PropertyType,
                    Name = (persistableAttribute != null ? persistableAttribute.Name : null) ?? each.Name
                })).ToArray()
            );

            /*
                    Fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Where(each => 
                        !each.IsInitOnly && !each.GetCustomAttributes(typeof (NotPersistableAttribute), true).Any() && (
                            each.IsPublic || 
                            each.GetCustomAttributes(typeof (PersistableAttribute), true).Any())
                        ).ToArray(),
                    */
            /*
                    Properties = type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Where(each => {

                        var sm = each.GetSetMethod(true);
                        var gm = each.GetGetMethod(true);

                        return 
                            ((sm != null && gm != null) && 
                                !each.GetCustomAttributes(typeof (NotPersistableAttribute), true).Any() && 
                                (each.GetSetMethod(true).IsPublic && each.GetGetMethod(true).IsPublic)) ||
                            each.GetCustomAttributes(typeof (PersistableAttribute), true).Any();
                    }).ToArray()*/


        }

        private static MethodInfo GetOpImplicit(Type sourceType, Type destinationType) {
            lock( OpImplicitMethods ) {
                if( !OpImplicitMethods.ContainsKey(sourceType) ) {
                    var opImplicit = 
                        (from method in sourceType.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                            where method.Name == "op_Implicit" && method.ReturnType == destinationType && method.GetParameters()[0].ParameterType == sourceType
                            select method).Union(
                        (from method in destinationType.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                            where method.Name == "op_Implicit" && method.ReturnType == destinationType && method.GetParameters()[0].ParameterType == sourceType
                            select method
                        )).FirstOrDefault();

                    OpImplicitMethods.Add(sourceType, opImplicit);
                    return opImplicit;
                }
                return OpImplicitMethods[sourceType];
            }
        }

        public static object ImplicitlyConvert(this object obj, Type destinationType) {
            if( obj == null ) {
                return null;
            }
            if( destinationType == typeof(string)) {
                return obj.ToString();
            }
            
            var opImplicit = GetOpImplicit(obj.GetType(), destinationType);
            if( opImplicit != null ) {
                return opImplicit.Invoke(null, new[] {obj});
            }
            return obj;
        }

        public static bool ImplicitlyConvertsTo(this Type type, Type destinationType) {
            if (type == destinationType || typeof(string) == destinationType) {
                return true;
            }
            return GetOpImplicit(type, destinationType) != null;
        }

        public static object ToArrayOfType(this IEnumerable<object> enumerable, Type collectionType) {
            return ToArrayMethods.GetOrAdd(collectionType, () => ToArrayMethod.MakeGenericMethod(collectionType))
                .Invoke(null, new[] { enumerable.CastToIEnumerableOfType( collectionType ) });
        }

        public static object CastToIEnumerableOfType(this IEnumerable<object> enumerable, Type collectionType  ) {
            return CastMethods.GetOrAdd(collectionType, () => CastMethod.MakeGenericMethod(collectionType)).Invoke(null, new object[] { enumerable });
        }

        public static object CreateInstance(this Type type) {
            return Activator.CreateInstance(TypeSubtitution[type] ?? type, true);
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