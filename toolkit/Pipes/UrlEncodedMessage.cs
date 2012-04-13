//-----------------------------------------------------------------------
// <copyright company="CoApp Project">
//     Copyright (c) 2010 Garrett Serack . All rights reserved.
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
    using Extensions;

    /// <summary>
    /// Helper class to create/read UrlEncodedMessages
    /// 
    /// 
    /// </summary>
    /// <remarks>
    /// NOTE: EXPLICITLY IGNORE, NOT READY FOR TESTING.
    /// </remarks>
    public class UrlEncodedMessage : IEnumerable<string> {

        public class UrlEncodedMessageValue {
            private readonly string _value;
            public UrlEncodedMessageValue(string value) {
                _value = value;
            }

            public override string  ToString() {
                return _value ?? string.Empty;
            }

            public static implicit operator string(UrlEncodedMessageValue value) {
                return value._value ?? string.Empty;
            }

            public static implicit operator int?(UrlEncodedMessageValue value) {
                if (value._value == null)
                    return null;

                int outVal;
                if (Int32.TryParse(value._value, out outVal)) {
                    return outVal;
                }
                return null;
            }

             public static implicit operator long?(UrlEncodedMessageValue value) {
                if (value._value == null)
                    return null;

                long outVal;
                if (Int64.TryParse(value._value, out outVal)) {
                    return outVal;
                }
                return null;
            }

            public static implicit operator bool?(UrlEncodedMessageValue value) {
                if (value._value == null)
                    return null;

                switch (value._value.ToLower()) {
                    case "true":
                        return true;
                        
                    case "false":
                        return false;
                        
                    default:
                        return null;
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        private static readonly char[] _query = new[] {
            '?'
        };

        /// <summary>
        /// 
        /// </summary>
        private static readonly char[] _separator = new[] {
            '&'
        };

        /// <summary>
        /// 
        /// </summary>
        private static readonly char[] _equals = new[] {
            '='
        };

        /// <summary>
        /// 
        /// </summary>
        public string Command;

        /// <summary>
        /// 
        /// </summary>
        internal IDictionary<string, string> Data;

        /// <summary>
        /// Initializes a new instance of the <see cref="UrlEncodedMessage"/> class.
        /// </summary>
        /// <param name="rawMessage">The raw message.</param>
        /// <remarks></remarks>
        public UrlEncodedMessage(string rawMessage) {
            var parts = rawMessage.Split(_query, StringSplitOptions.RemoveEmptyEntries);
            Command = (parts.FirstOrDefault() ?? "" ).UrlDecode().ToLower();
            Data = (parts.Skip(1).FirstOrDefault() ?? "").Split(_separator, StringSplitOptions.RemoveEmptyEntries).Select(
                p => p.Split(_equals, StringSplitOptions.RemoveEmptyEntries)).ToDictionary(
                    s => s[0].UrlDecode(),
                    s => s.Length > 1 ? s[1].UrlDecode() : String.Empty);
            
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="UrlEncodedMessage"/> class.
        /// </summary>
        /// <param name="command">The command.</param>
        /// <param name="data">The data.</param>
        /// <remarks></remarks>
        public UrlEncodedMessage( string command, IDictionary<string, string> data ) {
            Command = command;
            Data = data;
        }

        public UrlEncodedMessageValue this[string key] {
            get { return new UrlEncodedMessageValue( Data.ContainsKey(key) ? Data[key] : null ); }
        }

        /// <summary>
        /// Returns a <see cref="System.String"/> that represents this instance.
        /// </summary>
        /// <returns>A <see cref="System.String"/> that represents this instance.</returns>
        /// <remarks></remarks>
        public override string ToString() {
            return Data.Any()
                ? Data.Keys.Aggregate(Command.UrlEncode().ToLower() + "?", (current, k) => current + (!string.IsNullOrEmpty(Data[k]) ? (k.UrlEncode() + "=" + Data[k].UrlEncode() + "&") : string.Empty))
                : Command.UrlEncode();
        }

        public string ToSmallerString() {
            return Data.Any()
                ? Data.Keys.Aggregate(Command.UrlEncode().ToLower() + "?", (current, k) => current + (!string.IsNullOrEmpty(Data[k]) ? (k.UrlEncode() + "=" + Data[k].Substring(0, Math.Min(Data[k].Length, 512)).UrlEncode() + "&") : string.Empty))
                : Command.UrlEncode();
        }

        /// <summary>
        /// Returns an enumerator that iterates through a collection.
        /// </summary>
        /// <returns>An <see cref="T:System.Collections.IEnumerator"/> object that can be used to iterate through the collection.</returns>
        /// <remarks></remarks>
        IEnumerator IEnumerable.GetEnumerator() {
            return GetEnumerator();
        }

        /// <summary>
        /// Adds the specified key.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="value">The value.</param>
        /// <remarks></remarks>
        public void Add(string key, string value) {
            if (!string.IsNullOrEmpty(value)) {
                if (Data.ContainsKey(key)) {
                    Data[key] = value;
                }
                else {
                    Data.Add(key, value);
                }
            }
        }

        /// <summary>
        /// Adds the specified key.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="value">The value.</param>
        /// <remarks></remarks>
        public void Add(string key, bool? value) {
            if (value != null) {
                if (Data.ContainsKey(key)) {
                    Data[key] = value.ToString();
                }
                else {
                    Data.Add(key, value.ToString());
                }
            }
        }

        /// <summary>
        /// Adds the specified key.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="value">The value.</param>
        /// <remarks></remarks>
        public void Add(string key, int? value) {
            if (value != null) {
                if (Data.ContainsKey(key)) {
                    Data[key] = value.ToString();
                }
                else {
                    Data.Add(key, value.ToString());
                }
            }
        }

        public void AddKeyValuePair( string key, string elementName, string elementValue) {
            Add("{0}[{1}]".format(key, elementName.UrlEncode()), elementValue);
        }

        public void AddKeyValueCollection( string key, IEnumerable<KeyValuePair<string, string>> collection ) {
            foreach (var each in collection) {
                AddKeyValuePair(key, each.Key, each.Value);
            }
        }

        public void AddCollection( string key, IEnumerable<string>  values ) {
            if (!values.IsNullOrEmpty()) {
                var index = 0;
                foreach (var s in values.Where(s => !string.IsNullOrEmpty(s))) {
                    Add("{0}[{1}]".format(key, index++), s);
                }
            }
        }

        /// <summary>
        /// Returns an enumerator that iterates through the collection.
        /// </summary>
        /// <returns>A <see cref="T:System.Collections.Generic.IEnumerator`1"/> that can be used to iterate through the collection.</returns>
        /// <remarks></remarks>
        public IEnumerator<string> GetEnumerator() {
            return Data.Keys.GetEnumerator();
        }

        /// <summary>
        /// Gets the collection.
        /// </summary>
        /// <param name="collectionName">The key for the collection.</param>
        /// <returns></returns>
        /// <remarks></remarks>
        public IEnumerable<string> GetCollection(string collectionName) {
            var rx = new Regex(@"^{0}\[\d*\]$".format(Regex.Escape(collectionName)));
            return from k in Data.Keys where rx.IsMatch(k) select Data[k];
        }

        /// <summary>
        /// Gets the collection.
        /// </summary>
        /// <param name="collectionName">The key for the collection.</param>
        /// <returns></returns>
        /// <remarks></remarks>
        public IEnumerable<KeyValuePair<string,string>> GetKeyValuePairs(string collectionName) {
            var rx = new Regex(@"^{0}\[(.*)\]$".format(Regex.Escape(collectionName)));
            return from k in Data.Keys let match = rx.Match(k) where match.Success select new KeyValuePair<string,string> (match.Groups[1].Captures[0].Value.UrlDecode(), Data[k]);
        }

        public static implicit operator string(UrlEncodedMessage value) {
            return value.ToString();
        }
    }
}