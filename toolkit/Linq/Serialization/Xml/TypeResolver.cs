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

namespace CoApp.Toolkit.Linq.Serialization.Xml {
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Reflection;
    using System.Reflection.Emit;
    using System.Threading;
    using System.Xml.Linq;

    public sealed class TypeResolver {
        private Dictionary<AnonTypeId, Type> anonymousTypes = new Dictionary<AnonTypeId, Type>();
        private ModuleBuilder moduleBuilder;
        private int anonymousTypeIndex;

        /// <summary>
        ///   KnownTypes for DataContractSerializer. Only needs to hold the element type, not the collection or array type.
        /// </summary>
        public ReadOnlyCollection<Type> knownTypes { get; private set; }

        private HashSet<Assembly> assemblies = new HashSet<Assembly> {
            typeof (ExpressionType).Assembly,
            typeof (string).Assembly,
            typeof (List<>).Assembly,
            typeof (XElement).Assembly,
            Assembly.GetExecutingAssembly(),
            Assembly.GetEntryAssembly(),
        };

        /// <summary>
        ///   Relying on the constructor only, to load all possible (including IEnumerable, IQueryable, Nullable, Array) Types into memory, may not scale well.
        /// </summary>
        /// <param name="assemblies"> </param>
        /// <param name="knownTypes"> </param>
        public TypeResolver(IEnumerable<Assembly> @assemblies = null, IEnumerable<Type> @knownTypes = null) {
            var asmname = new AssemblyName();
            asmname.Name = "AnonymousTypes";
            var assemblyBuilder = Thread.GetDomain().DefineDynamicAssembly(asmname, AssemblyBuilderAccess.Run);
            moduleBuilder = assemblyBuilder.DefineDynamicModule("AnonymousTypes");
            if (@assemblies != null) {
                foreach (var a in @assemblies) {
                    this.assemblies.Add(a);
                }
            }
            foreach (var n in AppDomain.CurrentDomain.GetAssemblies()) {
                this.assemblies.Add(n);
            }

            var simpleTypes = from t in typeof (String).Assembly.GetTypes()
                where
                    (t.IsPrimitive || t == typeof (String) || t.IsEnum)
                        && !(t == typeof (IntPtr) || t == typeof (UIntPtr))
                select t;

            this.knownTypes = new ReadOnlyCollection<Type>(new List<Type>(simpleTypes.Union(@knownTypes ?? Type.EmptyTypes)));
        }

        public bool HasMappedKnownType(Type input) {
            Type knownType;
            return HasMappedKnownType(input, out knownType);
        }

        /// <summary>
        ///   Checks if the input Type is "mapped" or otherwise somehow related (e.g. Array) to a KnownType found in this.KnownTypes.
        /// </summary>
        /// <param name="input"> </param>
        /// <param name="knownType"> </param>
        /// <returns> </returns>
        public bool HasMappedKnownType(Type input, out Type knownType) //out suggestedType?
        {
            var copy = new HashSet<Type>(knownTypes); //to prevent duplicates.
            knownType = null;
            //suggestedType = null;
            //generic , array , IEnumerable types, IQueryable, Nullable Types...
            foreach (var existing in knownTypes) {
                if (input == existing) {
                    knownType = existing;
                    //suggestedType = knownType;					
                    return true;
                } else if (input == existing.MakeArrayType()
                    || input == typeof (IEnumerable<>).MakeGenericType(existing)
                        || IsIEnumerableOf(input, existing)) {
                    copy.Add(input);
                    knownTypes = new ReadOnlyCollection<Type>(new List<Type>(copy));
                    knownType = existing;
                    //suggestedType = existing.MakeArrayType();
                    return true;
                } else if (existing.IsValueType && input == typeof (Nullable<>).MakeGenericType(existing)) {
                    copy.Add(input);
                    knownTypes = new ReadOnlyCollection<Type>(new List<Type>(copy));
                    knownType = existing;
                    //suggestedType = existing;//Nullable.Value instead
                    return true;
                }
            }

            return false; // knownType != null;
        }

        //public static bool IsTypeRelated(Type existing, Type input)
        //{
        //    return existing == input
        //            || (input == existing.MakeArrayType())// || input.IsArray && input.GetElementType() == existing)// |
        //            || (input == typeof(IEnumerable<>).MakeGenericType(existing))
        //            || IsIEnumerableOf(input, existing)
        //            || (existing.IsValueType && input == typeof(Nullable<>).MakeGenericType(existing));
        //}

        //protected virtual Type ResolveTypeFromString(string typeString) { return null; }
        //protected virtual string ResolveStringFromType(Type type) { return null; }

        public Type GetType(string typeName, IEnumerable<Type> genericArgumentTypes) {
            return GetType(typeName).MakeGenericType(genericArgumentTypes.ToArray());
        }

        public Type GetType(string typeName) {
            Type type;
            if (string.IsNullOrEmpty(typeName)) {
                throw new ArgumentNullException("typeName");
            }

            #region// First - try all replacers

            //type = ResolveTypeFromString(typeName);
            //type = typeReplacers.Select(f => f(typeName)).FirstOrDefault();
            //if (type != null)
            //    return type;

            #endregion

            // If it's an array name - get the element type and wrap in the array type.
            if (typeName.EndsWith("[]")) {
                return GetType(typeName.Substring(0, typeName.Length - 2)).MakeArrayType();
            }

            if (knownTypes.Any(k => k.FullName == typeName)) {
                return knownTypes.First(k => k.FullName == typeName);
            }

            // try all loaded types
            foreach (var assembly in assemblies) {
                type = assembly.GetType(typeName);
                if (type != null) {
                    return type;
                }
            }

            // Second - try just plain old Type.GetType()
            type = Type.GetType(typeName, false, true);
            if (type != null) {
                return type;
            }

            throw new ArgumentException("Could not find a matching type", typeName);
        }

        internal static string GetNameOfExpression(Expression e) {
            string name;
            if (e is LambdaExpression) {
                name = typeof (LambdaExpression).Name;
            } else if (e is ParameterExpression) {
                name = typeof (ParameterExpression).Name; //PrimitiveParameterExpression?
            } else if (e is BinaryExpression) {
                name = typeof (BinaryExpression).Name; //SimpleBinaryExpression?
            } else if (e is MethodCallExpression) {
                name = typeof (MethodCallExpression).Name; //MethodCallExpressionN?
            } else {
                name = e.GetType().Name;
            }

            return name;
        }

        public MethodInfo GetMethod(Type declaringType, string name, Type[] parameterTypes, Type[] genArgTypes) {
            var methods = from mi in declaringType.GetMethods()
                where mi.Name == name
                select mi;
            foreach (var method in methods) {
                // Would be nice to remvoe the try/catch
                try {
                    var realMethod = method;
                    if (method.IsGenericMethod) {
                        realMethod = method.MakeGenericMethod(genArgTypes);
                    }
                    var methodParameterTypes = realMethod.GetParameters().Select(p => p.ParameterType);
                    if (MatchPiecewise(parameterTypes, methodParameterTypes)) {
                        return realMethod;
                    }
                } catch (ArgumentException) {
                    continue;
                }
            }
            return null;
        }

        private bool MatchPiecewise<T>(IEnumerable<T> first, IEnumerable<T> second) {
            var firstArray = first.ToArray();
            var secondArray = second.ToArray();
            if (firstArray.Length != secondArray.Length) {
                return false;
            }
            for (var i = 0; i < firstArray.Length; i++) {
                if (!firstArray[i].Equals(secondArray[i])) {
                    return false;
                }
            }
            return true;
        }

        //vsadov: need to take ctor parameters too as they do not 
        //necessarily match properties order as returned by GetProperties
        public Type GetOrCreateAnonymousTypeFor(string name, NameTypePair[] properties, NameTypePair[] ctr_params) {
            var id = new AnonTypeId(name, properties.Concat(ctr_params));
            if (anonymousTypes.ContainsKey(id)) {
                return anonymousTypes[id];
            }

            //vsadov: VB anon type. not necessary, just looks better
            var anon_prefix = "<>f__AnonymousType";
            var anonTypeBuilder = moduleBuilder.DefineType(anon_prefix + anonymousTypeIndex++, TypeAttributes.Public | TypeAttributes.Class);

            var fieldBuilders = new FieldBuilder[properties.Length];
            var propertyBuilders = new PropertyBuilder[properties.Length];

            for (var i = 0; i < properties.Length; i++) {
                fieldBuilders[i] = anonTypeBuilder.DefineField("_generatedfield_" + properties[i].Name, properties[i].Type, FieldAttributes.Private);
                propertyBuilders[i] = anonTypeBuilder.DefineProperty(properties[i].Name, PropertyAttributes.None, properties[i].Type, new Type[0]);
                var propertyGetterBuilder = anonTypeBuilder.DefineMethod("get_" + properties[i].Name, MethodAttributes.Public, properties[i].Type, new Type[0]);
                var getterILGenerator = propertyGetterBuilder.GetILGenerator();
                getterILGenerator.Emit(OpCodes.Ldarg_0);
                getterILGenerator.Emit(OpCodes.Ldfld, fieldBuilders[i]);
                getterILGenerator.Emit(OpCodes.Ret);
                propertyBuilders[i].SetGetMethod(propertyGetterBuilder);
            }

            var constructorBuilder = anonTypeBuilder.DefineConstructor(MethodAttributes.HideBySig | MethodAttributes.Public | MethodAttributes.Public, CallingConventions.Standard, ctr_params.Select(prop => prop.Type).ToArray());
            var constructorILGenerator = constructorBuilder.GetILGenerator();
            for (var i = 0; i < ctr_params.Length; i++) {
                constructorILGenerator.Emit(OpCodes.Ldarg_0);
                constructorILGenerator.Emit(OpCodes.Ldarg, i + 1);
                constructorILGenerator.Emit(OpCodes.Stfld, fieldBuilders[i]);
                constructorBuilder.DefineParameter(i + 1, ParameterAttributes.None, ctr_params[i].Name);
            }
            constructorILGenerator.Emit(OpCodes.Ret);

            //TODO - Define ToString() and GetHashCode implementations for our generated Anonymous Types
            //MethodBuilder toStringBuilder = anonTypeBuilder.DefineMethod();
            //MethodBuilder getHashCodeBuilder = anonTypeBuilder.DefineMethod();

            var anonType = anonTypeBuilder.CreateType();
            anonymousTypes.Add(id, anonType);
            return anonType;
        }

        #region static methods

        public static Type GetElementType(Type collectionType) {
            var ienum = FindIEnumerable(collectionType);
            if (ienum == null) {
                return collectionType;
            }
            return ienum.GetGenericArguments()[0];
        }

        private static Type FindIEnumerable(Type collectionType) {
            if (collectionType == null || collectionType == typeof (string)) {
                return null;
            }
            if (collectionType.IsArray) {
                return typeof (IEnumerable<>).MakeGenericType(collectionType.GetElementType());
            }
            if (collectionType.IsGenericType) {
                foreach (var arg in collectionType.GetGenericArguments()) {
                    var ienum = typeof (IEnumerable<>).MakeGenericType(arg);
                    if (ienum.IsAssignableFrom(collectionType)) {
                        return ienum;
                    }
                }
            }
            var ifaces = collectionType.GetInterfaces();
            if (ifaces != null && ifaces.Length > 0) {
                foreach (var iface in ifaces) {
                    var ienum = FindIEnumerable(iface);
                    if (ienum != null) {
                        return ienum;
                    }
                }
            }
            if (collectionType.BaseType != null && collectionType.BaseType != typeof (object)) {
                return FindIEnumerable(collectionType.BaseType);
            }
            return null;
        }

        /// <summary>
        /// </summary>
        /// <param name="enumerableType"> the candidate enumerable Type in question </param>
        /// <param name="elementType"> is the candidate type a IEnumerable of elementType </param>
        /// <returns> </returns>
        public static bool IsIEnumerableOf(Type enumerableType, Type elementType) {
            if (elementType.MakeArrayType() == enumerableType) {
                return true;
            }

            if (!enumerableType.IsGenericType) {
                return false;
            }
            var typeArgs = enumerableType.GetGenericArguments();
            if (typeArgs.Length != 1) {
                return false;
            }
            if (!elementType.IsAssignableFrom(typeArgs[0])) {
                return false;
            }
            if (!typeof (IEnumerable<>).MakeGenericType(typeArgs).IsAssignableFrom(enumerableType)) {
                return false;
            }
            return true;
        }

        public static bool HasBaseType(Type thisType, Type baseType) {
            while (thisType.BaseType != null && thisType.BaseType != typeof (Object)) {
                if (thisType.BaseType == baseType) {
                    return true;
                }
                thisType = thisType.BaseType;
            }

            return false;
        }

        public static IEnumerable<Type> GetBaseTypes(Type expectedType) {
            var list = new List<Type>();
            list.Add(expectedType);
            if (expectedType.IsArray) {
                expectedType = expectedType.GetElementType();
                list.Add(expectedType);
            } else {
                list.Add(expectedType.MakeArrayType());
            }

            while (expectedType.BaseType != null && expectedType.BaseType != typeof (Object)) {
                expectedType = expectedType.BaseType;
                list.Add(expectedType);
            }
            return list;
        }

        /// <summary>
        ///   For determining KnownType(s) to declare on a DataContract
        /// </summary>
        /// <param name="baseType"> </param>
        /// <returns> </returns>
        public static List<Type> GetDerivedTypes(Type baseType) {
            var a = baseType.Assembly;
            var derived = from anyType in a.GetTypes()
                where HasBaseType(anyType, baseType)
                select anyType;
            var list = derived.ToList();
            return list;
        }

        public static bool IsNullableType(Type t) {
            return t.IsValueType && t.Name == "Nullable`1";
        }

        public static bool HasInheritedProperty(Type declaringType, PropertyInfo pInfo) {
            if (pInfo.DeclaringType != declaringType) {
                return true;
            }

            while (declaringType.BaseType != null && declaringType.BaseType != typeof (Object)) {
                foreach (var baseP in declaringType.BaseType.GetProperties()) {
                    if (baseP.Name == pInfo.Name && baseP.PropertyType == pInfo.PropertyType) {
                        return true;
                    }
                }
                declaringType = declaringType.BaseType;
            }
            return false;
        }

        public static string ToGenericTypeFullNameString(Type t) {
            if (t.FullName == null && t.IsGenericParameter) {
                return t.GenericParameterPosition == 0 ? "T" : "T" + t.GenericParameterPosition;
            }

            if (!t.IsGenericType) {
                return t.FullName;
            }

            var value = t.FullName.Substring(0, t.FullName.IndexOf('`')) + "<";
            var genericArgs = t.GetGenericArguments();
            var list = new List<string>();
            for (var i = 0; i < genericArgs.Length; i++) {
                value += "{" + i + "},";
                var s = ToGenericTypeFullNameString(genericArgs[i]);
                list.Add(s);
            }
            value = value.TrimEnd(',');
            value += ">";
            value = string.Format(value, list.ToArray<string>());
            return value;
        }

        public static string ToGenericTypeNameString(Type t) {
            var fullname = ToGenericTypeFullNameString(t);
            fullname = fullname.Substring(fullname.LastIndexOf('.') + 1).TrimEnd('>');
            return fullname;
        }

        #endregion

        #region nested classes

        public class NameTypePair {
            public string Name { get; set; }
            public Type Type { get; set; }

            public override int GetHashCode() {
                return Name.GetHashCode() + Type.GetHashCode();
            }

            public override bool Equals(object obj) {
                if (!(obj is NameTypePair)) {
                    return false;
                }
                var other = obj as NameTypePair;
                return Name.Equals(other.Name) && Type.Equals(other.Type);
            }
        }

        private class AnonTypeId {
            public string Name { get; private set; }
            public IEnumerable<NameTypePair> Properties { get; private set; }

            public AnonTypeId(string name, IEnumerable<NameTypePair> properties) {
                Name = name;
                Properties = properties;
            }

            public override int GetHashCode() {
                var result = Name.GetHashCode();
                foreach (var ntpair in Properties) {
                    result += ntpair.GetHashCode();
                }
                return result;
            }

            public override bool Equals(object obj) {
                if (!(obj is AnonTypeId)) {
                    return false;
                }
                var other = obj as AnonTypeId;
                return (Name.Equals(other.Name)
                    && Properties.SequenceEqual(other.Properties));
            }
        }

        #endregion
    }
}