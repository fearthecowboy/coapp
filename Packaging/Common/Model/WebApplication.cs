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
    using System;
    using System.Xml.Serialization;
    using Toolkit.Collections;

    [XmlRoot(ElementName = "WebApplication", Namespace = "http://coapp.org/atom-package-feed-1.0")]
    public class WebApplication {
        [XmlElement(IsNullable = false)]
        public string Name { get; set; }

        /*
        [XmlArray(IsNullable = false)]
        public List<string> VirutalDirs { get; set; }
        */
    }

    [XmlRoot(ElementName = "WebApplication", Namespace = "http://coapp.org/atom-package-feed-1.0")]
    public class FauxApplication {
        [XmlElement(IsNullable = false)]
        public string Name { get; set; }

        [XmlElement(IsNullable = false)]
        public string InstallCommand { get; set; }

        [XmlElement(IsNullable = false)]
        public string InstallParameters { get; set; }

        [XmlElement(IsNullable = false)]
        public string RemoveCommand { get; set; }

        [XmlElement(IsNullable = false)]
        public string RemoveParameters { get; set; }

        [XmlElement(IsNullable = false)]
        public XDictionary<string,Uri> Downloads { get; set; }
    }

}