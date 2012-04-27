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

    [XmlRoot(ElementName = "CompositionRule", Namespace = "http://coapp.org/atom-package-feed-1.0")]
    public class CompositionRule {
        [XmlAttribute]
        public CompositionAction Action { get; set; }

        // AKA "Key"
        [XmlAttribute]
        public string Destination { get; set; }

        // AKA "Value"
        [XmlAttribute]
        public string Source { get; set; }

        [XmlIgnore]
        public string Value {
            get {
                return Source;
            }
        }

        [XmlIgnore]
        public string Key {
            get {
                return Destination;
            }
        }

        [XmlAttribute]
        public string Parameters { get; set; }

        [XmlAttribute]
        public string Category { get; set; }
    }
}