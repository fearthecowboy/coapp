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

namespace CoApp.Packaging.Common.Model {
    using System.Xml.Serialization;
    using Toolkit.Win32;

    [XmlRoot(ElementName = "BindingPolicy", Namespace = "http://coapp.org/atom-package-feed-1.0")]
    public class BindingPolicy : IXmlSerializable {
        public FourPartVersion Minimum;
        public FourPartVersion Maximum;

        public System.Xml.Schema.XmlSchema GetSchema() { return null; }

        public void ReadXml(System.Xml.XmlReader reader) {
            reader.MoveToContent();

            Minimum = reader.GetAttribute("Minimum");
            Maximum = reader.GetAttribute("Maximum");

            var isEmptyElement = reader.IsEmptyElement; 
            reader.ReadStartElement();
            if (!isEmptyElement) {
                reader.ReadEndElement();
            }
        }

        public void WriteXml(System.Xml.XmlWriter writer) {
            if (Minimum != 0 && Maximum != 0) {
                writer.WriteAttributeString("Minimum", Minimum);
                writer.WriteAttributeString("Maximum", Maximum);
            }
        }
    }
}