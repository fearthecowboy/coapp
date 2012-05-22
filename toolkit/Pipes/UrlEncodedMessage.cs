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

namespace CoApp.Toolkit.Pipes {
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.RegularExpressions;
    using Collections;
    using Extensions;

    /// <summary
    ///   UrlEncodedMessages
    /// </summary>
    public class UrlEncodedMessage : IEnumerable<string> {
        private static readonly IDictionary<Type, TypeInstantiator> TypeSubstitution = new XDictionary<Type, TypeInstantiator>();

        public delegate object TypeInstantiator(UrlEncodedMessage message, string key, Type t);
        public static void AddTypeSubstitution<T>(TypeInstantiator typeInstantiator) {
            TypeSubstitution.Add(typeof(T), typeInstantiator);
        }

        /// <summary>
        /// </summary>
        private static readonly char[] Query = new[] { '?' };
        
        /// <summary>
        /// </summary>
        private readonly char[] _separator = new[] {'&' };
        private readonly string _separatorString = "&";
        /// <summary>
        /// </summary>
        private static readonly char[] Equal = new[] { '=' };

        /// <summary>
        /// </summary>
        public string Command;

        /// <summary>
        /// </summary>
        private readonly IDictionary<string, string> _data;

        private bool _storeTypeInformation;

        /// <summary>
        ///   Initializes a new instance of the <see cref="UrlEncodedMessage" /> class.
        /// </summary>
        /// <param name="rawMessage"> The raw message. </param>
        /// <param name="seperator"> </param>
        /// <param name="storeTypeInformation"> </param>
        /// <remarks>
        /// </remarks>
        public UrlEncodedMessage(string rawMessage = null, string seperator = "&", bool storeTypeInformation = false) {
            _separatorString = seperator;
            _separator = seperator.ToCharArray();
            _storeTypeInformation = storeTypeInformation;
            rawMessage = rawMessage ?? string.Empty;

            
            var parts = rawMessage.Split(Query, StringSplitOptions.RemoveEmptyEntries);
            switch( parts.Length ) {
                case 0:
                    _data = new XDictionary<string, string>();
                    break;

                case 1:
                    if (rawMessage.IndexOf("=") > -1) {
                        Command = string.Empty;
                        _data = (parts.FirstOrDefault() ?? string.Empty).Split(_separator, StringSplitOptions.RemoveEmptyEntries).Select(
                            p => p.Split(Equal, StringSplitOptions.RemoveEmptyEntries))
                                .ToXDictionary(s => s[0].UrlDecode(), s => s.Length > 1 ? s[1].UrlDecode() : String.Empty);
                        break;
                    }

                    Command = rawMessage;
                    _data = new XDictionary<string, string>();
                    break;

                default:
                    Command = parts.FirstOrDefault().UrlDecode();
                    // ReSharper disable PossibleNullReferenceException (the parts has two or more!)
                    _data = parts.Skip(1).FirstOrDefault().Split(_separator, StringSplitOptions.RemoveEmptyEntries).Select(
                        p => p.Split(Equal, StringSplitOptions.RemoveEmptyEntries))
                            .ToXDictionary(s => s[0].UrlDecode(),s => s.Length > 1 ? s[1].UrlDecode() : String.Empty);
                    // ReSharper restore PossibleNullReferenceException
                    break;
            }
        }

        /// <summary>
        ///   Initializes a new instance of the <see cref="UrlEncodedMessage" /> class.
        /// </summary>
        /// <param name="command"> The command. </param>
        /// <param name="data"> The data. </param>
        /// <param name="seperator"> </param>
        /// <param name="storeTypeInformation"> </param>
        /// <remarks>
        /// </remarks>
        public UrlEncodedMessage(string command, IDictionary<string, string> data, string seperator = "&", bool storeTypeInformation = false) {
            _separatorString = seperator;
            _separator = seperator.ToCharArray();
            _storeTypeInformation = storeTypeInformation;
            Command = command;
            _data = data;
        }

        /// <summary>
        ///   Returns a <see cref="System.String" /> that represents this instance.
        /// </summary>
        /// <returns> A <see cref="System.String" /> that represents this instance. </returns>
        /// <remarks>
        /// </remarks>
        public override string ToString() {
            return _data.Any()
                ? _data.Keys.Aggregate(string.IsNullOrEmpty(Command) ? string.Empty : Command.UrlEncode() + "?", (current, k) => current + (!string.IsNullOrEmpty(_data[k]) ? (k.UrlEncode() + "=" + _data[k].UrlEncode() + _separatorString) : string.Empty))
                : Command.UrlEncode();
        }

        public string ToSmallerString() {
            return _data.Any()
                ? _data.Keys.Aggregate(string.IsNullOrEmpty(Command) ? string.Empty : Command + "?", (current, k) => current + (!string.IsNullOrEmpty(_data[k]) ? (k + "=" + _data[k].Substring(0, Math.Min(_data[k].Length, 512)) + _separatorString) : string.Empty))
                : Command;
        }

        /// <summary>
        ///   Returns an enumerator that iterates through a collection.
        /// </summary>
        /// <returns> An <see cref="T:System.Collections.IEnumerator" /> object that can be used to iterate through the collection. </returns>
        /// <remarks>
        /// </remarks>
        IEnumerator IEnumerable.GetEnumerator() {
            return GetEnumerator();
        }
        /// <summary>
        ///   Returns an enumerator that iterates through the collection.
        /// </summary>
        /// <returns> A <see cref="T:System.Collections.Generic.IEnumerator`1" /> that can be used to iterate through the collection. </returns>
        /// <remarks>
        /// </remarks>
        public IEnumerator<string> GetEnumerator() {
            return _data.Keys.GetEnumerator();
        }
        public string this[string key] {
            get {
                return GetValueAsString(key);
            }
            set {
                Add(key, value);
            }
        }

        private const string CollectionEx = @"^{0}\[(\d*)\]$";
        private const string ComplexCollectionEx = @"^{0}\[(\d*?)\]{1}$";
        

        private IEnumerable<string> GetCollectionOfString( string collectionName ) {
            var rx = new Regex(CollectionEx.format(Regex.Escape(collectionName)));
            return (from k in _data.Keys let match = rx.Match(k) where match.Success select new {index = match.Groups[1].Captures[0].Value.UrlDecode().ToInt32(), value = _data[k]}).OrderBy(each => each.index).Select(each => each.value );
        }

        private IEnumerable<object> GetCollectionOfParsable(string collectionName, Type elementType) {
            var rx = new Regex(CollectionEx.format(Regex.Escape(collectionName)));
            return (from k in _data.Keys let match = rx.Match(k) where match.Success select new { index = match.Groups[1].Captures[0].Value.UrlDecode().ToInt32(), value = _data[k] }).OrderBy(each => each.index).Select(each => elementType.ParseString(each.value));
        }

        private object GetValueAsArrayOfParsable(string collectionName, Type elementType) {
            if (elementType == typeof(string)) {
                return GetCollectionOfString(collectionName).ToArray();
            }
            return GetCollectionOfParsable(collectionName, elementType).ToArrayOfType(elementType);
        }

        private object GetValueAsArrayOfComplex(string collectionName, Type elementType) {
            var rx = new Regex(@"^{0}\[(\d*?)\](.*)$".format(Regex.Escape(collectionName)));
            return (from k in _data.Keys
                    let match = rx.Match(k)
                    where match.Success
                    select GetValue(string.Format("{0}[{1}]", collectionName, match.Groups[1].Captures[0].Value), elementType)).ToArrayOfType(elementType);
        }

        private object GetValueAsIEnumerableOfParsable(string collectionName, Type elementType) {
            if (elementType == typeof(string)) {
                return GetCollectionOfString(collectionName);
            }
            return GetCollectionOfParsable(collectionName, elementType).CastToIEnumerableOfType(elementType);
        }

        private object GetValueAsIEnumerableOfComplex(string collectionName, Type elementType) {
            var rx = new Regex(@"^{0}\[(\d*?)\](.*)$".format(Regex.Escape(collectionName)));
            return (from k in _data.Keys
                          let match = rx.Match(k)
                          where match.Success
                          select GetValue(string.Format("{0}[{1}]", collectionName, match.Groups[1].Captures[0].Value), elementType)).CastToIEnumerableOfType(elementType);
        }

        private object GetValueAsArray(string collectionName, Type elementType, Type arrayType) {
            return elementType.IsParsable() ? GetValueAsArrayOfParsable(collectionName, elementType) : GetValueAsArrayOfComplex(collectionName, elementType);
        }

        private object GetValueAsIEnumerable(string collectionName, Type elementType, Type collectionType) {
            var collection = elementType.IsParsable() ? GetValueAsIEnumerableOfParsable(collectionName, elementType) : GetValueAsIEnumerableOfComplex(collectionName, elementType);

            if (collectionType.Name.StartsWith("IEnumerable")) {
                return collection;
            } 

            // we need to get the collection and then insert the elements into the target type.
            var result = (IList) collectionType.CreateInstance();
            foreach( var o in (IEnumerable)collection ) {
                result.Add(o);
            }
            return result;
        }

        private object GetValueAsEnum( string key, Type enumerationType) {
            var v = GetValueAsString(key);
            if( string.IsNullOrEmpty(v)) {
                return null;
            }
            return Enum.Parse(enumerationType, v);
        }

        private IEnumerable<KeyValuePair<string, string>> GetKeyValueStringPairs(string collectionName) {
            var rx = new Regex(@"^{0}\[(.*?)\]$".format(Regex.Escape(collectionName)));
            return from k in _data.Keys let match = rx.Match(k) where match.Success select new KeyValuePair<string, string>(match.Groups[1].Captures[0].Value.UrlDecode(), _data[k]);
        }

        private object GetValueAsDictionaryOfParsable(string collectionName, Type keyType, Type valueType, Type dictionaryType) {
            IDictionary result;

            if ((dictionaryType.Name.StartsWith("IDictionary")) || (dictionaryType.Name.StartsWith("XDictionary"))) {
                result = (IDictionary)Activator.CreateInstance(typeof(XDictionary<,>).MakeGenericType(keyType, valueType));
            }
            else {
                result = (IDictionary)Activator.CreateInstance(typeof(Dictionary<,>).MakeGenericType(keyType, valueType));
            }
            foreach (var each in GetKeyValueStringPairs(collectionName)) {
                result.Add(keyType.ParseString(each.Key), valueType.ParseString(each.Value));
            }
            return result;
        }

        private object GetValueAsDictionaryOfComplex(string collectionName, Type keyType, Type valueType, Type dictionaryType) {
            var rx = new Regex(@"^{0}\[(.*?)\](.*)$".format(Regex.Escape(collectionName)));
            var results = from k in _data.Keys
                          let match = rx.Match(k)
                          where match.Success
                          select new KeyValuePair<string, object>(match.Groups[1].Captures[0].Value.UrlDecode(), GetValue(string.Format("{0}[{1}]", collectionName, match.Groups[1].Captures[0].Value), valueType));
            
            IDictionary result;

            if ((dictionaryType.Name.StartsWith("IDictionary") ) || (dictionaryType.Name.StartsWith("XDictionary"))) {
                result = (IDictionary)Activator.CreateInstance(typeof(XDictionary<,>).MakeGenericType(keyType, valueType));
            }
            else {
                result = (IDictionary)Activator.CreateInstance(typeof(Dictionary<,>).MakeGenericType(keyType, valueType));
            }

            foreach (var each in results) {
                result.Add(keyType.ParseString(each.Key), each.Value);
            }
            return result;
        }

        private object GetValueAsDictionary(string collectionName, Type keyType, Type valueType, Type dictionaryType) {
            return valueType.IsParsable() ? GetValueAsDictionaryOfParsable(collectionName, keyType, valueType, dictionaryType) : GetValueAsDictionaryOfComplex(collectionName, keyType, valueType, dictionaryType);
        }

        private string GetValueAsString(string key) {
            key = key ?? string.Empty;
            return _data.ContainsKey(key) ? _data[key] : string.Empty;
        }

        private object GetValueAsPrimitive(string key, Type primitiveType) {
            return primitiveType.ParseString(_data.ContainsKey(key) ? _data[key] : null);
        }

        private object GetValueAsNullable(string key, Type nullableType) {
            return _data.ContainsKey(key) ? nullableType.ParseString(_data[key]) : null;
        }

        private object GetValueAsOther(string key, Type otherType, object o = null) {
            if (o == null) {
                var instantiator = TypeSubstitution[otherType];
                o = instantiator != null ? instantiator(this, key, otherType) : otherType.CreateInstance();
                if( o == null ) {
                    return null;
                }

                otherType = o.GetType();
            }

            foreach( var p in otherType.GetPersistableElements()) {
                if( p.SetValue != null ) {
                    var v = GetValue(FormatKey(key, p.Name), p.DeserializeAsType);
                    if( v == null ) {
                        p.SetValue(o, GetValue(FormatKey(key, p.Name), p.DeserializeAsType), null);
                        continue;
                    }

                    if ( (!p.ActualType.IsInstanceOfType(v)) && p.DeserializeAsType.ImplicitlyConvertsTo(p.ActualType)) {
                        v = v.ImplicitlyConvert(p.ActualType);
                    }

                    p.SetValue(o, v, null);
                }
            }

            return o;
        }

        private string FormatKey(string key, string subkey=null) {
            if(string.IsNullOrEmpty(key)) {
                key = ".";
            }
            if( !string.IsNullOrEmpty(subkey)) {
                key = key.EndsWith(".") ? key + subkey : key + "." + subkey;
            }
            return key;
        }

        private string FormatKeyIndex(string key, int index ) {
            if (string.IsNullOrEmpty(key)) {
                key = ".";
            }
            return "{0}[{1}]".format(key, index);
        }

        private string FormatKeyIndex(string key, string index) {
            if (string.IsNullOrEmpty(key)) {
                key = ".";
            }
            return "{0}[{1}]".format(key, index.UrlEncode());
        }

        /// <summary>
        ///   Adds the specified key.
        /// </summary>
        /// <param name="key"> The key. </param>
        /// <param name="value"> The value. </param>
        /// <remarks>
        /// </remarks>
        public void Add(string key, string value) {
            key = FormatKey(key);
            if (!string.IsNullOrEmpty(value)) {
                _data[key] = value;
            }
        }

        public void AddKeyValuePair(string key, string elementName, object elementValue) {
            Add(FormatKeyIndex(key, elementName), elementValue, elementValue.GetType());
        }

        public void AddKeyValueCollection(string key, IEnumerable<KeyValuePair<string, string>> collection) {
            key = FormatKey(key);
            foreach (var each in collection) {
                AddKeyValuePair(key, each.Key, each.Value);
            }
        }

        public void AddDictionary(string key, IDictionary collection) {
            key = FormatKey(key);
            foreach (var each in collection.Keys) {
                if( each.GetType().IsParsable() ) {
                    AddKeyValuePair(key, each.ToString(), collection[each]);
                }
            }
        }

        public void AddStringCollection(string key, IEnumerable<string> values) {
            if (values != null) {
                var index = 0;
                foreach (var s in values.Where(s => !string.IsNullOrEmpty(s))) {
                    Add(FormatKeyIndex(key, index++), s);
                }
            }
        }

        public void AddCollection(string key, IEnumerable values, Type serializeElementAsType) {
            if (values != null) {
                var index = 0;
                for (var enmerator = values.GetEnumerator(); enmerator.MoveNext();) {
                    if (enmerator.Current != null ) {
                        Add(FormatKeyIndex(key, index++), enmerator.Current, serializeElementAsType);    
                    }
                }
            }
        }
      

        /// <summary>
        ///   Gets the collection.
        /// </summary>
        /// <param name="collectionName"> The key for the collection. </param>
        /// <returns> </returns>
        /// <remarks>
        /// </remarks>
        public IEnumerable<string> GetCollection(string key) {
            key = FormatKey(key);
            var rx = new Regex(@"^{0}\[\d*\]$".format(Regex.Escape(key)));
            return from k in _data.Keys where rx.IsMatch(k) select _data[k];
        }

        public void Add(string argName, object arg, Type argType) {
            argName = FormatKey(argName);
            if( arg == null ) {
                return;
            }

            if (arg.GetType().ImplicitlyConvertsTo(argType)) {
                arg = arg.ImplicitlyConvert(argType);
            }

            if (_storeTypeInformation) {
                Add(argName+"$T$", argType.FullName);
            }

            var custom = CustomSerializer.GetCustomSerializer(argType);
            if (custom != null) {
                custom.SerializeObject(this, argName,arg );
            }

            if (argType == typeof(string) || argType.IsEnum || argType.IsParsable()) {
                Add(argName, arg.ToString());
                return;
            }

            if (argType.IsDictionary()) {
                AddDictionary(argName, (IDictionary)arg);
                return;
            }

            if (argType.IsArray || argType.IsIEnumerable()) {
                AddCollection(argName, (IEnumerable)arg, argType.GetPersistableInfo().ElementType);
                return;
            }

            // fall through to reflection-based serialization.
            foreach (var p in argType.GetPersistableElements()) {
                if (p.GetValue != null) {
                    Add(FormatKey(argName, p.Name), p.GetValue(arg, null), p.SerializeAsType);
                }
            }
        }

        public void Add(string key, object value) {
            Add( key, value, value.GetType() );
        }

        public static implicit operator string(UrlEncodedMessage value) {
            return value.ToString();
        }

        public T GetValue<T>(string key) {
            return (T)GetValue(key, typeof (T));
        }

        public object GetValue(string key , Type argType, object o = null) {
            key = FormatKey(key);

            var custom = CustomSerializer.GetCustomSerializer(argType);
            if (custom != null) {
                return custom.DeserializeObject(this, key);
            }
            
            var pi = (TypeExtensions.TypeSubtitution[argType] ?? argType).GetPersistableInfo();

            switch (pi.PersistableCategory) {
                case PersistableCategory.String:
                    return GetValueAsString(key);

                case PersistableCategory.Parseable:
                    return GetValueAsPrimitive(key, pi.Type);

                case PersistableCategory.Nullable:
                    return GetValueAsNullable(key, pi.NullableType);

                case PersistableCategory.Enumerable:
                    return GetValueAsIEnumerable(key, pi.ElementType, pi.Type);

                case PersistableCategory.Array:
                    return GetValueAsArray(key, pi.ElementType, pi.Type);

                case PersistableCategory.Dictionary:
                    return GetValueAsDictionary(key, pi.DictionaryKeyType, pi.DictionaryValueType, pi.Type);

                case PersistableCategory.Enumeration:
                    return GetValueAsEnum(key, pi.Type);

                case PersistableCategory.Other:
                    return GetValueAsOther(key, pi.Type, o);
            }
            return o;
        }

        public T DeserializeTo<T>(T intoInstance = default(T), string key = null) {
            return (T)GetValue(key, typeof(T), intoInstance);
        }
    }
}