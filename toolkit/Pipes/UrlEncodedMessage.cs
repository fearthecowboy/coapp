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
    using System.Reflection;
    using System.Text.RegularExpressions;
    using Collections;
    using Exceptions;
    using Extensions;

    /// <summary>
    ///   Helper class to create/read UrlEncodedMessages
    /// </summary>
    /// <remarks>
    ///   NOTE: EXPLICITLY IGNORE, NOT READY FOR TESTING.
    /// </remarks>
    public class UrlEncodedMessage : IEnumerable<string> {
        /// <summary>
        /// </summary>
        private static readonly char[] _query = new[] {
            '?'
        };

        /// <summary>
        /// </summary>
        private static readonly char[] _separator = new[] {
            '&'
        };

        /// <summary>
        /// </summary>
        private static readonly char[] _equals = new[] {
            '='
        };

        /// <summary>
        /// </summary>
        public string Command;

        /// <summary>
        /// </summary>
        internal IDictionary<string, string> Data;

        /// <summary>
        ///   Initializes a new instance of the <see cref="UrlEncodedMessage" /> class.
        /// </summary>
        /// <param name="rawMessage"> The raw message. </param>
        /// <remarks>
        /// </remarks>
        public UrlEncodedMessage(string rawMessage) {
            var parts = rawMessage.Split(_query, StringSplitOptions.RemoveEmptyEntries);
            Command = (parts.FirstOrDefault() ?? "").UrlDecode();
            Data = (parts.Skip(1).FirstOrDefault() ?? "").Split(_separator, StringSplitOptions.RemoveEmptyEntries).Select(
                p => p.Split(_equals, StringSplitOptions.RemoveEmptyEntries)).ToXDictionary(
                    s => s[0].UrlDecode(),
                    s => s.Length > 1 ? s[1].UrlDecode() : String.Empty);
        }

        /// <summary>
        ///   Initializes a new instance of the <see cref="UrlEncodedMessage" /> class.
        /// </summary>
        /// <param name="command"> The command. </param>
        /// <param name="data"> The data. </param>
        /// <remarks>
        /// </remarks>
        public UrlEncodedMessage(string command, IDictionary<string, string> data) {
            Command = command;
            Data = data;
        }

        public string this[string key] {
            get {
                return GetValueAsString(key);
            }
            set {
                Add(key, value);
            }
        }

        public object GetValueAsArray(string collectionName, Type elementType, Type arrayType) {
            var rx = new Regex(@"^{0}\[\d*\]$".format(Regex.Escape(collectionName)));
            if (elementType == typeof (string)) {
                return (from k in Data.Keys where rx.IsMatch(k) select Data[k]).ToArray();
            }
            
            if (elementType.IsParsable()) {
                return _toArrayMethods.GetOrAdd(elementType, () => ToArrayMethod.MakeGenericMethod(elementType)).Invoke(null, new object[] {
                    _castMethods.GetOrAdd(elementType, () => CastMethod.MakeGenericMethod(elementType))
                    .Invoke(null, new object[] { (from k in Data.Keys where rx.IsMatch(k) select elementType.ParseString(Data[k])) })});
            }
            throw new CoAppException("Unsupported Array type '{0}' (must support tryparse)".format(elementType.Name));
        }

        private static MethodInfo CastMethod = typeof(Enumerable).GetMethod("Cast");
        private static MethodInfo ToArrayMethod = typeof(Enumerable).GetMethod("ToArray");
        private static IDictionary<Type, MethodInfo> _castMethods = new XDictionary<Type, MethodInfo>();
        private static IDictionary<Type, MethodInfo> _toArrayMethods = new XDictionary<Type, MethodInfo>();

        public object GetValueAsIEnumerable(string collectionName, Type elementType, Type collectionType) {
            var rx = new Regex(@"^{0}\[\d*\]$".format(Regex.Escape(collectionName)));
            if (elementType == typeof (string)) {
                return from k in Data.Keys where rx.IsMatch(k) select Data[k];
            }

            if (elementType.IsParsable()) {
                return _castMethods.GetOrAdd(elementType, () => CastMethod.MakeGenericMethod(elementType)).Invoke(null, new object[] { (from k in Data.Keys where rx.IsMatch(k) select elementType.ParseString(Data[k])) });
            }

            throw new CoAppException("Unsupported IEnumerable type '{0}' (must support tryparse or string constructor)".format(elementType.Name));
        }

        public object GetValueAsEnum( string key, Type enumerationType) {
            var v = GetValueAsString(key);
            if( string.IsNullOrEmpty(v)) {
                return null;
            }
            return Enum.Parse(enumerationType, v);
        }

        public object GetValueAsDictionary(string collectionName, Type keyType, Type valueType, Type dictionaryType) {
            var rx = new Regex(@"^{0}\[(.*)\]$".format(Regex.Escape(collectionName)));
            var pairs = from k in Data.Keys let match = rx.Match(k) where match.Success select new { key = match.Groups[1].Captures[0].Value.UrlDecode(), value = Data[k]};
            dynamic result;

            if ((dictionaryType.Name.IndexOf("IDictionary") > -1) || (dictionaryType.Name.IndexOf("XDictionary") > -1)) {
                result = Activator.CreateInstance(typeof(XDictionary<,>).MakeGenericType(keyType, valueType));
            } else {
                result = Activator.CreateInstance(typeof(XDictionary<,>).MakeGenericType(keyType, valueType));    
            }
            
            
            foreach (var each in pairs) {
                try {
                    
                    result.AddPair(keyType.ParseString(each.key), keyType.ParseString(each.value));
                } catch (Exception e ) {
                    Console.WriteLine(e.Message);
                    Console.WriteLine(e.StackTrace);
                }
            }
            return result;
            /*
            if (keyType == typeof (string) && valueType.IsParsable()) {
                return keys.ToXDictionary(key => key, key => valueType.ParseString(Data[key]));
            }
            if (keyType.IsParsable() && valueType == typeof (string)) {
                dynamic result = Activator.CreateInstance(typeof (Dictionary<,>).MakeGenericType(keyType, valueType));
                foreach( var key in keys ) {
                    result.Add(keyType.ParseString(key), Data[key]);
                }
                // return keys.ToXDictionary(key => keyType.ParseString(key), key => Data[key]);
            }
            if (keyType.IsParsable() && valueType.IsParsable()) {
                return keys.ToXDictionary(key => keyType.ParseString(key), key => valueType.ParseString(Data[key]));
            }
            */
            throw new CoAppException("Unsupported Dictionary type '{0}/{1}' (keys and values must support tryparse)".format(keyType.Name, valueType.Name));
        }

        public string GetValueAsString(string key) {
            return Data.ContainsKey(key) ? Data[key] : string.Empty;
        }

        public object GetValueAsPrimitive(string key, Type primitiveType) {
            return primitiveType.ParseString(Data.ContainsKey(key) ? Data[key] : null);
        }

        public object GetValueAsNullable(string key, Type primitiveType) {
            return Data.ContainsKey(key) ? primitiveType.ParseString(Data[key]) : null;
        }

        /// <summary>
        ///   Returns a <see cref="System.String" /> that represents this instance.
        /// </summary>
        /// <returns> A <see cref="System.String" /> that represents this instance. </returns>
        /// <remarks>
        /// </remarks>
        public override string ToString() {
            return Data.Any()
                ? Data.Keys.Aggregate(Command.UrlEncode() + "?", (current, k) => current + (!string.IsNullOrEmpty(Data[k]) ? (k.UrlEncode() + "=" + Data[k].UrlEncode() + "&") : string.Empty))
                : Command.UrlEncode();
        }

        public string ToSmallerString() {
            return Data.Any()
                ? Data.Keys.Aggregate(Command + "?", (current, k) => current + (!string.IsNullOrEmpty(Data[k]) ? (k + "=" + Data[k].Substring(0, Math.Min(Data[k].Length, 512)) + "&") : string.Empty))
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
        ///   Adds the specified key.
        /// </summary>
        /// <param name="key"> The key. </param>
        /// <param name="value"> The value. </param>
        /// <remarks>
        /// </remarks>
        public void Add(string key, string value) {
            if (!string.IsNullOrEmpty(value)) {
                if (Data.ContainsKey(key)) {
                    Data[key] = value;
                } else {
                    Data.Add(key, value);
                }
            }
        }

        /// <summary>
        ///   Adds the specified key.
        /// </summary>
        /// <param name="key"> The key. </param>
        /// <param name="value"> The value. </param>
        /// <remarks>
        /// </remarks>
        public void Add(string key, bool? value) {
            if (value != null) {
                if (Data.ContainsKey(key)) {
                    Data[key] = value.ToString();
                } else {
                    Data.Add(key, value.ToString());
                }
            }
        }

        /// <summary>
        ///   Adds the specified key.
        /// </summary>
        /// <param name="key"> The key. </param>
        /// <param name="value"> The value. </param>
        /// <remarks>
        /// </remarks>
        public void Add(string key, int? value) {
            if (value != null) {
                if (Data.ContainsKey(key)) {
                    Data[key] = value.ToString();
                } else {
                    Data.Add(key, value.ToString());
                }
            }
        }

        public void AddKeyValuePair(string key, string elementName, string elementValue) {
            Add("{0}[{1}]".format(key, elementName.UrlEncode()), elementValue);
        }

        public void AddKeyValueCollection(string key, IEnumerable<KeyValuePair<string, string>> collection) {
            foreach (var each in collection) {
                AddKeyValuePair(key, each.Key, each.Value);
            }
        }

        public void AddDictionary(string key, IDictionary collection) {
            foreach (var each in collection.Keys) {
                var val = collection[each] ?? "";
                AddKeyValuePair(key, each.ToString(), val.ToString());
            }
        }

        public void AddCollection(string key, IEnumerable<string> values) {
            if (!values.IsNullOrEmpty()) {
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
                    Add("{0}[{1}]".format(key, index++), enmerator.Current.ToString());
                }
            }
        }

        /// <summary>
        ///   Returns an enumerator that iterates through the collection.
        /// </summary>
        /// <returns> A <see cref="T:System.Collections.Generic.IEnumerator`1" /> that can be used to iterate through the collection. </returns>
        /// <remarks>
        /// </remarks>
        public IEnumerator<string> GetEnumerator() {
            return Data.Keys.GetEnumerator();
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
            return from k in Data.Keys where rx.IsMatch(k) select Data[k];
        }

        /// <summary>
        ///   Gets the collection.
        /// </summary>
        /// <param name="collectionName"> The key for the collection. </param>
        /// <returns> </returns>
        /// <remarks>
        /// </remarks>
        public IEnumerable<KeyValuePair<string, string>> GetKeyValuePairs(string collectionName) {
            var rx = new Regex(@"^{0}\[(.*)\]$".format(Regex.Escape(collectionName)));
            return from k in Data.Keys let match = rx.Match(k) where match.Success select new KeyValuePair<string, string>(match.Groups[1].Captures[0].Value.UrlDecode(), Data[k]);
        }

        public static implicit operator string(UrlEncodedMessage value) {
            return value.ToString();
        }
    }
}