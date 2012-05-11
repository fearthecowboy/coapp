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
    using System.Collections.Generic;
    using System.Linq;
    using System.Runtime.Serialization;
    using System.Xml;
    using System.Xml.Serialization;
    using Toolkit.Collections;
    using Toolkit.Extensions;
    using Toolkit.Pipes;

#if COAPP_ENGINE_CORE_DEPRECATED
    using Atom;
    using Packaging.Service;
    using System.IO;
#endif

    [XmlRoot(ElementName = "Details", Namespace = "http://coapp.org/atom-package-feed-1.0")]
    public class PackageDetails {
        // Elements marked with XmlIgnore won't persist in the package feed as themselves
        // they get persisted as elements in the Atom Format (so that we have a suitable Atom feed to look at)
        public PackageDetails() {
            Publisher = new Identity();
            Contributors = new XList<Identity>();
        }

        [XmlElement(IsNullable = false)]
        public string AuthorVersion { get; set; }

        [XmlElement(IsNullable = false)]
        public string BugTracker { get; set; }

        [XmlIgnore]
        public XList<Uri> Icons { get; set; }

        [XmlElement(IsNullable = false)]
        public XList<License> Licenses { get; set; }

        [XmlElement(IsNullable = false)]
        public bool IsNsfw { get; set; }

        /// <summary>
        ///   -100 = DEATHLY_UNSTABLE ... 0 == release ... +100 = CERTIFIED_NEVER_GONNA_GIVE_YOU_UP.
        /// </summary>
        [XmlElement(IsNullable = false)]
        public sbyte Stability { get; set; }

        [XmlIgnore]
        public string SummaryDescription { get; set; }

        [XmlIgnore]
        public DateTime PublishDate { get; set; }

        [XmlIgnore]
        public Identity Publisher { get; set; }

        [XmlIgnore]
        public XList<Identity> Contributors { get; set; }

        [XmlIgnore]
        public string CopyrightStatement { get; set; }

        [XmlIgnore]
        public XList<string> Tags { get; set; }

        [XmlIgnore]
        public XList<string> Categories { get; set; }

        [XmlIgnore]
        public string Description { get; set; }

#if COAPP_ENGINE_CORE_DEPRECATED
        internal string GetAtomItemText(Package package) {
            var item = new AtomItem(package);
            using (var sw = new StringWriter()) {
                using (var xw = XmlWriter.Create(sw)) {
                    item.SaveAsAtom10(xw);
                }
                return sw.ToString();
            }
        }
#endif

        // soak up anything we don't recognize
        [XmlAnyAttribute]
        public XmlAttribute[] UnknownAttributes;

        [XmlAnyElement]
        public XmlElement[] UnknownElements;
    }
}