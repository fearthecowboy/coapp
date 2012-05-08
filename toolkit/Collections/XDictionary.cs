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

namespace CoApp.Toolkit.Collections {
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using System.Runtime.Serialization;
    using System.Xml;
    using System.Xml.Schema;
    using System.Xml.Serialization;
    using Extensions;

    /// <summary>
    ///   This behaves like a regular dictionary, except: 
    ///     - add operations will silently replace existing values the 
    ///     - inedexer [] will silently add values 
    ///     - Getting values will return default(TValue) instead of throwing on no element. 
    ///     - setting a value to default(TValue) removes the key
    /// </summary>
    /// <typeparam name="TKey"> </typeparam>
    /// <typeparam name="TValue"> </typeparam>
    [XmlRoot("Dictionary")]
    public class XDictionary<TKey, TValue> : IDictionary<TKey, TValue>, ISerializable, IDictionary, IXmlSerializable {
        public delegate void CollectionChanged(IDictionary<TKey, TValue> source);
        private readonly InternalDictionary<TKey, TValue> _base;
        public event CollectionChanged Changed = (collection) => {};

        /// <summary>
        ///   Creates a new instance of Dictionary.
        /// </summary>
        /// <param name="defaultValue"> Default value to use when a requested key is not present in the collection. </param>
        public XDictionary(TValue defaultValue)
            : this() {
            Default = defaultValue;
        }

        public XDictionary() {
            _base = new InternalDictionary<TKey, TValue>();
        }

        public XDictionary(int capacity) {
            _base = new InternalDictionary<TKey, TValue>(capacity);
        }

        public XDictionary(IEqualityComparer<TKey> comparer) {
            _base = new InternalDictionary<TKey, TValue>(comparer);
        }

        public XDictionary(IDictionary<TKey, TValue> dictionary) {
            _base = new InternalDictionary<TKey, TValue>(dictionary);
        }

        protected XDictionary(SerializationInfo info, StreamingContext context) {
            _base = new InternalDictionary<TKey, TValue>(info, context);
        }

        /// <summary>
        ///   Gets or sets the default value returned when a non-existing key is requested from the Dictionary.
        /// </summary>
        public TValue Default { get; set; }

        /// <summary>
        ///   Gets or sets the value associated with the key.
        /// </summary>
        /// <param name="key"> Key into the collection. </param>
        /// <returns> Value associated with the key, or if the key is not a part of the collection, then the value specified by the Default property. </returns>
        public virtual TValue this[TKey key] {
            get {
                return _base.ContainsKey(key) ? _base[key] : Default;
            }
            set {
                if (!_base.ContainsKey(key)) {
                    _base.Add(key, value);
                } else {
                    _base[key] = value;
                }
                Changed(this);
            }
        }

        public int RemoveAll(ICollection<TValue> values) {
            if (values == null) {
                throw new ArgumentNullException("values");
            }

            var keys = new List<TKey>(values.Count);
            keys.AddRange(from entry in this from value in values where entry.Value.Equals(value) select entry.Key);
            // get matching keys for the specified values

            return RemoveAll(keys);
        }

        public int RemoveAll(ICollection<TKey> keys) {
            if (keys == null) {
                throw new ArgumentNullException("keys");
            }

            var ret = keys.Count(key => _base.Remove(key));
            Changed(this);

            return ret;
        }

        public bool ContainsKey(TKey key) {
            return _base.ContainsKey(key);
        }

        public void AddPair( object k, object v) {
            Add((TKey)k, (TValue)v);
        }

        public void Add(TKey key, TValue value) {
            object o = value;
            if (o == null || value.Equals(Default)) {
                _base.Remove(key);
                Changed(this);
                return;
            }

            if (_base.ContainsKey(key)) {
                _base[key] = value;
            } else {
                _base.Add(key, value);
            }
            Changed(this);
        }

        public void Add(KeyValuePair<TKey, TValue> item) {
            this[item.Key] = item.Value;
        }

        public bool Remove(TKey key) {
            var result = _base.Remove(key);
            Changed(this);
            return result;
        }

        public bool TryGetValue(TKey key, out TValue value) {
            return _base.TryGetValue(key, out value);
        }

        public ICollection<TKey> Keys {
            get {
                return _base.Keys;
            }
        }

        public ICollection<TValue> Values {
            get {
                return _base.Values;
            }
        }

        void ICollection<KeyValuePair<TKey, TValue>>.Add(KeyValuePair<TKey, TValue> item) {
            ((ICollection<KeyValuePair<TKey, TValue>>)_base).Add(item);
            Changed(this);
        }

        public void Clear() {
            _base.Clear();
            Changed(this);
        }

        bool ICollection<KeyValuePair<TKey, TValue>>.Contains(KeyValuePair<TKey, TValue> item) {
            return ((ICollection<KeyValuePair<TKey, TValue>>)_base).Contains(item);
        }

        void ICollection<KeyValuePair<TKey, TValue>>.CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex) {
            ((ICollection<KeyValuePair<TKey, TValue>>)_base).CopyTo(array, arrayIndex);
        }

        bool ICollection<KeyValuePair<TKey, TValue>>.Remove(KeyValuePair<TKey, TValue> item) {
            var result=  ((ICollection<KeyValuePair<TKey, TValue>>)_base).Remove(item);
            Changed(this);
            return result;
        }

        public int Count {
            get {
                return _base.Count;
            }
        }

        bool ICollection<KeyValuePair<TKey, TValue>>.IsReadOnly {
            get {
                return ((ICollection<KeyValuePair<TKey, TValue>>)_base).IsReadOnly;
            }
        }

        IEnumerator IEnumerable.GetEnumerator() {
            return ((IEnumerable<KeyValuePair<TKey, TValue>>)this).GetEnumerator();
        }

        IEnumerator<KeyValuePair<TKey, TValue>> IEnumerable<KeyValuePair<TKey, TValue>>.GetEnumerator() {
            return _base.GetEnumerator();
        }

        void ISerializable.GetObjectData(SerializationInfo info, StreamingContext context) {
            _base.GetObjectData(info, context);
        }

        XmlSchema IXmlSerializable.GetSchema() {
            return null;
        }

        void IXmlSerializable.ReadXml(XmlReader reader) {
            // some types can be stored easily as attributes while others require their own XML rendering
            Func<TKey> readKey;
            Func<TValue> readValue;

            var isAttributable = new { Key = typeof(TKey).IsParsable(), Value = typeof(TValue).IsParsable() };

            // keys
            if (isAttributable.Key) {
                //readKey = () => (TKey)Convert.ChangeType(reader.GetAttribute("key"), typeof (TKey));
                readKey = () => (TKey)typeof(TKey).ParseString(reader.GetAttribute("key"));
            } else {
                var keySerializer = new XmlSerializer(typeof (TKey));
                readKey = () => {
                    while (reader.Name != "key") {
                        reader.Read();
                    }
                    reader.ReadStartElement("key");
                    var key = (TKey)keySerializer.Deserialize(reader);
                    reader.ReadEndElement();
                    return key;
                };
            }

            // values
            if (isAttributable.Value && isAttributable.Key) {
                // readValue = () => (TValue)Convert.ChangeType(reader.GetAttribute("value"), typeof (TValue));
                readValue = () => (TValue)typeof(TValue).ParseString(reader.GetAttribute("value"));
            } else {
                var valueSerializer = new XmlSerializer(typeof (TValue));
                readValue = () => {
                   while (reader.Name == "item") {
                     reader.Read();
                   }
                    // reader.ReadStartElement("value");
                    var value = (TValue)valueSerializer.Deserialize(reader);
                    // reader.ReadEndElement();
                    return value;
                };
            }

            var wasEmpty = reader.IsEmptyElement;
            reader.Read();

            if (wasEmpty) {
                return;
            }

            while (reader.NodeType != XmlNodeType.EndElement) {
                //while (reader.Name != "item" || reader.NodeType == System.Xml.XmlNodeType.EndElement)
                //    reader.Read();
                while (reader.NodeType == XmlNodeType.Whitespace) {
                    reader.Read();
                }
                var key = readKey();
                var value = readValue();
                Add(key, value);

                if (!isAttributable.Key || !isAttributable.Value) {
                    reader.ReadEndElement();
                } else {
                    reader.Read();
                }
                while (reader.NodeType == XmlNodeType.Whitespace) {
                    reader.Read();
                }
            }
            reader.ReadEndElement();
        }

        void IXmlSerializable.WriteXml(XmlWriter writer) {
            Action<TKey> writeKey;
            Action<TValue> writeValue;

            var isAttributable = new {Key = typeof (TKey).IsParsable(), Value = typeof (TValue).IsParsable()};

            if (isAttributable.Key) {
                writeKey = v => writer.WriteAttributeString("key", v.ToString());
            } else {
                var keySerializer = new XmlSerializer(typeof (TKey));
                writeKey = v => {
                    writer.WriteStartElement("key");
                    keySerializer.Serialize(writer, v);
                    writer.WriteEndElement();
                };
            }

            // when keys aren't attributable, neither are values
            if (isAttributable.Value && isAttributable.Key) {
                writeValue = v => writer.WriteAttributeString("value", v.ToString());
            } else {
                var valueSerializer = new XmlSerializer(typeof (TValue));
                writeValue = v => {
                    // writer.WriteStartElement("value");

                    valueSerializer.Serialize(writer, v);

                    // writer.WriteEndElement();
                };
            }

            foreach (var key in Keys) {
                writer.WriteStartElement("item");

                writeKey(key);
                writeValue(this[key]);

                writer.WriteEndElement();
            }
        }

        /// <summary>
        ///   Determines if a type is simple enough to where it can be stored as an attribute instead of in its own node.
        /// </summary>
        /// <param name="t"> Type to determine attributability of. </param>
        /// <returns> True if the type can be stored as an attribute of a node instead of its own dedicated node (and thus requiring more serialization work). </returns>
       //  private static bool IsAttributable(Type t) {
            // return _attributableTypes.Contains(t);
        // }

        void IDictionary.Add(object key, object value) {
            this.Add((TKey)key, (TValue)value);
        }

        void IDictionary.Clear() {
            this.Clear();
        }

        bool IDictionary.Contains(object key) {
            return this.ContainsKey((TKey)key);
        }

        IDictionaryEnumerator IDictionary.GetEnumerator() {
            throw new NotImplementedException();
        }

        bool IDictionary.IsFixedSize {
            get {
                return false;
            }
        }

        bool IDictionary.IsReadOnly {
            get {
                return ((ICollection<KeyValuePair<TKey, TValue>>)this).IsReadOnly;
            }
        }

        ICollection IDictionary.Keys {
            get {
                return (ICollection)this.Keys;
            }
        }

        void IDictionary.Remove(object key) {
            this.Remove((TKey)key);
        }

        ICollection IDictionary.Values {
            get {
                return (ICollection)this.Values;
            }
        }

        object IDictionary.this[object key] {
            get {
                return this[(TKey)key];
            }
            set {
                this[(TKey)key] = (TValue)value;
            }
        }

        void ICollection.CopyTo(Array array, int index) {
            var pos = index;
            foreach (var value in this.Values) {
                array.SetValue(value, pos);
                pos++;
            }
        }

        int ICollection.Count {
            get {
                return this.Count;
            }
        }

        bool ICollection.IsSynchronized {
            get {
                throw new NotImplementedException();
            }
        }

        object ICollection.SyncRoot {
            get {
                throw new NotImplementedException();
            }
        }

        public IDisposable Subscribe(IObserver<IDictionary<TKey, TValue>> observer) {
            throw new NotImplementedException();
        }
    }
}