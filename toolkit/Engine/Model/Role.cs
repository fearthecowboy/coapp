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

using System.Collections.Generic;
using System.Xml.Serialization;

namespace CoApp.Toolkit.Engine.Model {
    [XmlRoot(ElementName = "Role", Namespace = "http://coapp.org/atom-package-feed-1.0")]
    public class Role {
        [XmlElement(IsNullable = false)]
        public string Name { get; set; }

        [XmlElement(IsNullable = false)]
        public PackageRole PackageRole { get; set; }
    }

    [XmlRoot(ElementName = "Feature", Namespace = "http://coapp.org/atom-package-feed-1.0")]
    public class Feature {
        [XmlElement(IsNullable = false)]
        public string Name { get; set; }

        [XmlElement(IsNullable = false)]
        public string VersionInfo { get; set; }
    }
}

namespace CoApp.Toolkit.Engine.Model.Roles {

    [XmlRoot(ElementName = "DeveloperLibrary", Namespace = "http://coapp.org/atom-package-feed-1.0")]
    public class DeveloperLibrary {
        [XmlElement(IsNullable = false)]
        public string Name { get; set; }

        [XmlArray(IsNullable = false)]
        public List<string> HeaderFolders { get; set; }

        [XmlArray(IsNullable = false)]
        public List<string> DocumentFolders { get; set; }

        [XmlArray(IsNullable = false)]
        public List<string> LibraryFiles { get; set; }

        [XmlArray(IsNullable = false)]
        public List<string> ReferenceAssemblyFiles { get; set; }
    }

    [XmlRoot(ElementName = "WebApplication", Namespace = "http://coapp.org/atom-package-feed-1.0")]
    public class WebApplication {
        [XmlElement(IsNullable = false)]
        public string Name { get; set; }
        /*
        [XmlArray(IsNullable = false)]
        public List<string> VirutalDirs { get; set; }
        */
    }

    [XmlRoot(ElementName = "Service", Namespace = "http://coapp.org/atom-package-feed-1.0")]
    public class Service {
        [XmlElement(IsNullable = false)]
        public string Name { get; set; }
    }

    [XmlRoot(ElementName = "SourceCode", Namespace = "http://coapp.org/atom-package-feed-1.0")]
    public class SourceCode {
        [XmlElement(IsNullable = false)]
        public string Name { get; set; }
        /*
        [XmlArray(IsNullable = false)]
        public List<string> SourceDirs? { get; set; }
        */
    }

    [XmlRoot(ElementName = "Driver", Namespace = "http://coapp.org/atom-package-feed-1.0")]
    public class Driver {
        [XmlElement(IsNullable = false)]
        public string Name { get; set; }
    }
}