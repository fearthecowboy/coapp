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
    using System.Collections.Generic;
    using System.Xml;
    using System.Xml.Schema;
    using System.Xml.Serialization;
    using Extensions;

    [XmlRoot("List", Namespace = "http://coapp.org/atom-package-feed-1.0")]
    public class XList<T> : List<T>, IXmlSerializable {
        readonly string _elementName = typeof(T).Name;
        
        public XList()
            : base() {
        }

        public XList(int capacity)
            : base(capacity) {
        }

        public XList(IEnumerable<T> collection)
            : base(collection) {
        }

        public XmlSchema GetSchema() {
            return null;
        }

        public void ReadXml(XmlReader reader) {
            // some types can be stored easily as attributes while others require their own XML rendering
            Func<T> readValue;
            var isParsable = typeof (T).IsParsable();
            // values
            if (isParsable) {
                readValue = () => {
                    reader.ReadStartElement(_elementName, "http://coapp.org/atom-package-feed-1.0");
                    var value = (T)typeof (T).ParseString(reader.ReadString());
                    reader.ReadEndElement();
                    return value;
                };
            }
            else {
                var valueSerializer = new XmlSerializer(typeof(T));
                readValue = () => {


                    while (!reader.LocalName.Equals(_elementName, StringComparison.CurrentCultureIgnoreCase)) {
                        reader.Read();
                    }
                    
                    //  reader.ReadStartElement(_elementName);
                    var value = (T)valueSerializer.Deserialize(reader);
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
                while (reader.NodeType == XmlNodeType.Whitespace) {
                    reader.Read();
                }
              
                var value = readValue();
                Add(value);

                while (reader.NodeType == XmlNodeType.Whitespace) {
                    reader.Read();
                }
            }
            reader.ReadEndElement();
        }

        public void WriteXml(XmlWriter writer) {
            Action<T> writeElement;

            if (typeof(T).IsParsable()) {
                writeElement = v => {
                    writer.WriteStartElement(_elementName, "http://coapp.org/atom-package-feed-1.0");
                    writer.WriteValue(v.ToString());
                    writer.WriteEndElement();
                };
            }
            else {
                var valueSerializer = new XmlSerializer(typeof(T));
                writeElement = v => valueSerializer.Serialize(writer, v);
            }

            foreach (var each in this) {
                writeElement(each);
            }
        }
    }
}