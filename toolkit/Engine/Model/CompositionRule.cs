//-----------------------------------------------------------------------
// <copyright company="CoApp Project">
//     Copyright (c) 2011 Garrett Serack . All rights reserved.
// </copyright>
// <license>
//     The software is licensed under the Apache 2.0 License (the "License")
//     You may not use the software except in compliance with the License. 
// </license>
//-----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using CoApp.Toolkit.Engine.Model.Roles;

namespace CoApp.Toolkit.Engine {
    using System.Xml.Serialization;

    [XmlRoot(ElementName = "Composition", Namespace = "http://coapp.org/atom-package-feed-1.0")]
    public class Composition {
        [XmlArray(IsNullable = false)]
        public List<CompositionRule> CompositionRules { get; set; }

        [XmlArray(IsNullable = false)]
        public List<DeveloperLibrary> DeveloperLibraries { get; set; }

        [XmlArray(IsNullable = false)]
        public List<WebApplication> WebApplications { get; set; }

        [XmlArray(IsNullable = false)]
        public List<CoApp.Toolkit.Engine.Model.Roles.Service> Services { get; set; }

        [XmlArray(IsNullable = false)]
        public List<SourceCode> SourceCodes { get; set; }

        [XmlArray(IsNullable = false)]
        public List<Driver> Drivers { get; set; }

        // soak up anything we don't recognize
        [XmlAnyAttribute]
        public XmlAttribute[] UnknownAttributes;

        [XmlAnyElement]
        public XmlElement[] UnknownElements;
    }

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
        public string Value { get { return Source; } }

        [XmlIgnore]
        public string Key { get { return Destination; } }

        [XmlAttribute]
        public string Parameters { get; set; }

        [XmlAttribute]
        public string Category { get; set; }
    }
}
