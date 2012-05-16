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
    using System.Collections.Generic;
    using System.Linq.Expressions;
    using System.Xml.Linq;
    using Collections;
    using Linq.Serialization.Xml;

    public abstract class CustomSerializer {
        internal abstract void SerializeObject(UrlEncodedMessage msg, string key, object obj);
        internal abstract object DeserializeObject(UrlEncodedMessage msg, string key);

        public static readonly ExpressionXmlSerializer ExpressionXmlSerializer = new ExpressionXmlSerializer();
        protected static readonly IDictionary<Type, CustomSerializer> Serializers = new XDictionary<Type, CustomSerializer>();

        public static void Add<T>(CustomSerializer<T> serializer) {
            Serializers.Add(typeof(T), serializer);
        }

        public static CustomSerializer GetCustomSerializer(Type type) {
            return Serializers.ContainsKey(type) ? Serializers[type] : null;
        }

        static CustomSerializer() {
            // Serializer for System.Type
            Add(new CustomSerializer<Type>((message, key, serialize) => message.Add(key, serialize.FullName), (message, key) => Type.GetType(message[key], false, true)));

            // Serializer for System.Linq.Expressions.Expression
            Add(new CustomSerializer<Expression>((message, key, serialize) => message.Add(key, ExpressionXmlSerializer.Serialize(serialize).ToString(SaveOptions.OmitDuplicateNamespaces | SaveOptions.DisableFormatting)), (message, key) => ExpressionXmlSerializer.Deserialize(message[key])));

            // XElement
            Add(new CustomSerializer<XElement>((message, key, serialize) => message.Add(key,serialize.ToString(SaveOptions.OmitDuplicateNamespaces | SaveOptions.DisableFormatting)), (message, s) => XElement.Parse(message[s])));
        }
    }

    public class CustomSerializer<T> : CustomSerializer {
        public delegate T MessageDeserialize(UrlEncodedMessage message, string key);
        public delegate void MessageSerialize(UrlEncodedMessage message, string key, T objectToSerialize);
        private readonly MessageSerialize _serialize;
        private readonly MessageDeserialize _deserialize;

        public CustomSerializer(MessageSerialize serializer, MessageDeserialize deserializer) {
            _serialize = serializer;
            _deserialize = deserializer;
        }

        internal override void SerializeObject(UrlEncodedMessage msg, string key, object obj) {
            _serialize(msg, key, (T)obj);
        }
        internal override object DeserializeObject(UrlEncodedMessage msg, string key) {
            return _deserialize(msg, key);
        }
    }
}
