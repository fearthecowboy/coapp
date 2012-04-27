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

    [XmlRoot(ElementName = "WebApplication", Namespace = "http://coapp.org/atom-package-feed-1.0")]
    public class WebApplication {
        [XmlElement(IsNullable = false)]
        public string Name { get; set; }

        /*
        [XmlArray(IsNullable = false)]
        public List<string> VirutalDirs { get; set; }
        */
    }
}