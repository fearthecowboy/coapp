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
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Runtime.Serialization;
    using System.Text.RegularExpressions;
    using Collections;
    using Exceptions;
    using Extensions;
    using Text;

  

    /// <summary>
    ///   UrlEncodedMessages
    /// </summary>
    public class UrlEncodedMessage : IEnumerable<string> {

      
#if REMOVED        
        private static readonly IFormatter Formatter = new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();
#endif
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

            var parts = (rawMessage ?? "" ).Split(Query, StringSplitOptions.RemoveEmptyEntries);
            switch( parts.Length ) {
                case 0:
                    _data = new XDictionary<string, string>();
                    break;

                case 1:
                    Command = "";
                    _data = (parts.FirstOrDefault() ?? "").Split(_separator, StringSplitOptions.RemoveEmptyEntries).Select(
                        p => p.Split(Equal, StringSplitOptions.RemoveEmptyEntries))
                            .ToXDictionary(s => s[0].UrlDecode(),s => s.Length > 1 ? s[1].UrlDecode() : String.Empty);
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
                ? _data.Keys.Aggregate(string.IsNullOrEmpty(Command) ? "" : Command.UrlEncode() + "?", (current, k) => current + (!string.IsNullOrEmpty(_data[k]) ? (k.UrlEncode() + "=" + _data[k].UrlEncode() + _separatorString) : string.Empty))
                : Command.UrlEncode();
        }

        public string ToSmallerString() {
            return _data.Any()
                ? _data.Keys.Aggregate(string.IsNullOrEmpty(Command) ? "" : Command + "?", (current, k) => current + (!string.IsNullOrEmpty(_data[k]) ? (k + "=" + _data[k].Substring(0, Math.Min(_data[k].Length, 512)) + _separatorString) : string.Empty))
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

        public object GetValueAsArray(string collectionName, Type elementType, Type arrayType) {
            return elementType.IsParsable() ? GetValueAsArrayOfParsable(collectionName, elementType) : GetValueAsArrayOfComplex(collectionName, elementType);
        }

        public object GetValueAsIEnumerable(string collectionName, Type elementType, Type collectionType) {
            var collection = elementType.IsParsable() ? GetValueAsIEnumerableOfParsable(collectionName, elementType) : GetValueAsIEnumerableOfComplex(collectionName, elementType);
            if (collectionType.Name.StartsWith("IEnumerable")) {
                return collection;
            } else {
                // we need to get the collection and then insert the elements into the target type.
                IList result = (IList) collectionType.CreateInstance();
                foreach( var o in (IEnumerable)collection ) {
                    result.Add(o);
                }
                return result;
            }
        }

        public object GetValueAsEnum( string key, Type enumerationType) {
            var v = GetValueAsString(key);
            if( string.IsNullOrEmpty(v)) {
                return null;
            }
            return Enum.Parse(enumerationType, v);
        }

        public IEnumerable<KeyValuePair<string, string>> GetKeyValueStringPairs(string collectionName) {
            var rx = new Regex(@"^{0}\[(.*?)\]$".format(Regex.Escape(collectionName)));
            return from k in _data.Keys let match = rx.Match(k) where match.Success select new KeyValuePair<string, string>(match.Groups[1].Captures[0].Value.UrlDecode(), _data[k]);
        }

        public object GetValueAsDictionaryOfParsable(string collectionName, Type keyType, Type valueType, Type dictionaryType) {
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

        public object GetValueAsDictionaryOfComplex(string collectionName, Type keyType, Type valueType, Type dictionaryType) {
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

        public object GetValueAsDictionary(string collectionName, Type keyType, Type valueType, Type dictionaryType) {
            return valueType.IsParsable() ? GetValueAsDictionaryOfParsable(collectionName, keyType, valueType, dictionaryType) : GetValueAsDictionaryOfComplex(collectionName, keyType, valueType, dictionaryType);
        }

        public string GetValueAsString(string key) {
            return _data.ContainsKey(key) ? _data[key] : string.Empty;
        }

        public object GetValueAsPrimitive(string key, Type primitiveType) {
            return primitiveType.ParseString(_data.ContainsKey(key) ? _data[key] : null);
        }

        public object GetValueAsNullable(string key, Type primitiveType) {
            return _data.ContainsKey(key) ? primitiveType.ParseString(_data[key]) : null;
        }

        public object GetValueAsOther(string key, Type otherType, object o = null) {
            o = o ?? otherType.CreateInstance();

            var persistable = otherType.GetPersistableElements();
            foreach (var f in persistable.Fields) {
                f.SetValue(o, GetValue(key + "." + f.Name, f.FieldType));
            }

            foreach (var p in persistable.Properties) {
                p.SetValue(o, GetValue(key + "." + p.Name, p.PropertyType), null);
            }

            return null;
        }

    
        /// <summary>
        ///   Adds the specified key.
        /// </summary>
        /// <param name="key"> The key. </param>
        /// <param name="value"> The value. </param>
        /// <remarks>
        /// </remarks>
        public void Add(string key, string value) {
            if (!string.IsNullOrEmpty(value)) {
                _data[key] = value;
            }
        }

        public void AddKeyValuePair(string key, string elementName, object elementValue) {
            Add("{0}[{1}]".format(key, elementName.UrlEncode()), elementValue, elementValue.GetType());
        }

        public void AddKeyValueCollection(string key, IEnumerable<KeyValuePair<string, string>> collection) {
            foreach (var each in collection) {
                AddKeyValuePair(key, each.Key, each.Value);
            }
        }

        public void AddDictionary(string key, IDictionary collection) {
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
                    Add("{0}[{1}]".format(key, index++), s);
                }
            }
        }

        public void AddCollection(string key, IEnumerable values) {
            if (values != null) {
                var index = 0;
                for (var enmerator = values.GetEnumerator(); enmerator.MoveNext();) {
                    if (enmerator.Current != null ) {
                        Add("{0}[{1}]".format(key, index++), enmerator.Current, enmerator.Current.GetType() );    
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
        public IEnumerable<string> GetCollection(string collectionName) {
            var rx = new Regex(@"^{0}\[\d*\]$".format(Regex.Escape(collectionName)));
            return from k in _data.Keys where rx.IsMatch(k) select _data[k];
        }

        public void Add(string argName, object arg, Type argType) {
            if( arg == null ) {
                return;
            }
            
            if (_storeTypeInformation) {
                Add(argName+"$T$", argType.FullName);
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
                AddCollection(argName, (IEnumerable)arg);
                return;
            }

            // fall through to reflection-based serialization.
            var persistable = argType.GetPersistableElements();
            foreach( var f in persistable.Fields ) {
                Add(argName+"."+f.Name, f.GetValue(arg), f.FieldType);
            }

            foreach (var p in persistable.Properties) {
                Add(argName + "." + p.Name, p.GetValue(arg,null), p.PropertyType);
            }
            
        }

        public void Add(string key, object value) {
            Add( key, value, value.GetType() );
        }

        public static implicit operator string(UrlEncodedMessage value) {
            return value.ToString();
        }

        public object GetValue( string key , Type argType, object o = null) {
            var pi = argType.GetPersistableInfo();
            switch (pi.PersistableType) {
                case PersistableType.String:
                    return GetValueAsString(key);

                case PersistableType.Parseable:
                    return GetValueAsPrimitive(key, pi.Type);

                case PersistableType.Nullable:
                    return GetValueAsNullable(key, pi.Type);

                case PersistableType.Enumerable:
                    return GetValueAsIEnumerable(key, pi.ElementType, pi.Type);

                case PersistableType.Array:
                    return GetValueAsArray(key, pi.ElementType, pi.Type);

                case PersistableType.Dictionary:
                    return GetValueAsDictionary(key, pi.DictionaryKeyType, pi.DictionaryValueType, pi.Type);

                case PersistableType.Enumeration:
                    return GetValueAsEnum(key, pi.Type);

                case PersistableType.Other:
                    return GetValueAsOther(key, pi.Type, o);
            }

            return o;
        }

        public T DeserializeTo<T>(T intoInstance = default(T), string key = null) {
            return (T)GetValue(key, typeof(T), intoInstance);
        }
       
    }

    public static class UEMSerializationExtensions {
        public static UrlEncodedMessage Serialize( this object obj,string seperator = "&" , bool storeTypeNames = false) {
            return new UrlEncodedMessage(null, seperator, storeTypeNames) { { "", obj, obj.GetType() } };
        }
    }


    
}