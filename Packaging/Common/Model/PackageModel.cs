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
    using System.IO;
    using System.Linq;
    using System.Xml;
    using System.Xml.Serialization;
    using Atom;
    using CoApp.Toolkit.Extensions;
    using CoApp.Toolkit.Exceptions;
    using CoApp.Toolkit.Win32;

#if COAPP_ENGINE_CORE
    using Packaging.Service;
#endif

    [XmlRoot(ElementName = "Package", Namespace = "http://coapp.org/atom-package-feed-1.0")]
    public class PackageModel {
        // Elements marked with XmlIgnore won't persist in the package feed as themselves
        // they get persisted as elements in the Atom Format (so that we have a suitable Atom feed to look at)

        public PackageModel() {
            XmlSerializer = new XmlSerializer(GetType());
            PackageDetails = new PackageDetails();
        }

        [XmlIgnore]
        public CanonicalName CanonicalName { get; set; }

        [XmlAttribute("CanonicalName")]
        public string CanonicalNameSurrogate {
            get {
                return CanonicalName.ToString();
            }
            set {
                CanonicalName = CanonicalName.Parse(value);
            }
        }

        [XmlAttribute]
        public string Name { 
            get {
                return CanonicalName.Name;
            } 
            set {
                // ignore this value when deserializing, check just for consistency.
                if (null != CanonicalName && CanonicalName.Name != value) {
                    throw new CoAppException("PackageModel.Name is a read-only field, and deserializing is inconsistent (value:'{0}', should be:'{1}')".format(value, CanonicalName.Name));
                }
            }
        }

        [XmlAttribute]
        public string Flavor {
            get {
                return CanonicalName.Flavor;
            }
            set {
                if (null != CanonicalName && CanonicalName.Flavor != value) {
                    throw new CoAppException("PackageModel.Flavor is a read-only field, and deserializing is inconsistent (value:'{0}', should be:'{1}')".format(value, CanonicalName.Flavor));
                }
            }
        }

        [XmlIgnore]
        public PackageType PackageType { get {
            return CanonicalName.PackageType;
        } }

        // Workaround to get around stupid .NET limitation of not being able to let a class/struct serialize as an attribute. #FAIL
        [XmlAttribute("PackageType")]
        public string PackageTypeSurrogate {
            get {
                return PackageType;
            }
            set {
                // ignore this value when deserializing, check just for consistency.
                if (null != CanonicalName && CanonicalName.PackageType != value) {
                    throw new CoAppException("PackageModel.PackageType is a read-only field, and deserializing is inconsistent (value:'{0}', should be:'{1}')".format(value, CanonicalName.PackageType));
                }
            }
        }

        [XmlIgnore]
        public Architecture Architecture { get {
            return CanonicalName.Architecture;
        }}

        // Workaround to get around stupid .NET limitation of not being able to let a class/struct serialize as an attribute. #FAIL
        [XmlAttribute("Architecture")]
        public string ArchitectureSurrogate {
            get {
                return Architecture;
            }
            set {
                // ignore this value when deserializing, check just for consistency.
                if (null != CanonicalName && CanonicalName.Architecture != value) {
                    throw new CoAppException("PackageModel.Architecture is a read-only field, and deserializing is inconsistent (value:'{0}', should be:'{1}')".format(value, CanonicalName.Architecture));
                }
            }
        }

        [XmlIgnore]
        public FourPartVersion Version { get {
            return CanonicalName.Version;
        } }

        // Workaround to get around stupid .NET limitation of not being able to let a class/struct serialize as an attribute. #FAIL
        [XmlAttribute("Version")]
        public string VersionSurrogate {
            get {
                return Version;
            }
            set {
                if (null != CanonicalName && CanonicalName.Version != value) {
                    throw new CoAppException("PackageModel.Version is a read-only field, and deserializing is inconsistent (value:'{0}', should be:'{1}')".format(value, CanonicalName.Version));
                }
            }
        }

        [XmlAttribute]
        public string PublicKeyToken {
            get {
                return CanonicalName.PublicKeyToken;
            }
            set {
                if (null != CanonicalName && CanonicalName.PublicKeyToken != value) {
                    throw new CoAppException("PackageModel.PublicKeyToken is a read-only field, and deserializing is inconsistent (value:'{0}', should be:'{1}')".format(value, CanonicalName.PublicKeyToken));
                }
            }
        }


        [XmlAttribute]
        public string DisplayName { get; set; }

        [XmlAttribute]
        public string Vendor;

        [XmlIgnore]
        public FourPartVersion BindingPolicyMinVersion { get; set; }

        [XmlIgnore]
        public FourPartVersion BindingPolicyMaxVersion { get; set; }

        // Workaround to get around stupid .NET serialization of structs being a PITA.
        [XmlElement("BindingPolicyMinVersion", IsNullable = false)]
        public string BindingPolicyMinVersionSurrogate {
            get {
                return BindingPolicyMinVersion.ToString();
            }
            set {
                BindingPolicyMinVersion = FourPartVersion.Parse(value);
            }
        }

        // Workaround to get around stupid .NET serialization of structs being a PITA.
        [XmlElement("BindingPolicyMaxVersion", IsNullable = false)]
        public string BindingPolicyMaxVersionSurrogate {
            get {
                return BindingPolicyMaxVersion.ToString();
            }
            set {
                BindingPolicyMaxVersion = FourPartVersion.Parse(value);
            }
        }

        [XmlAttribute]
        public string RelativeLocation { get; set; }

        [XmlAttribute]
        public string Filename { get; set; }

        [XmlArray(IsNullable = false)]
        public List<Role> Roles { get; set; }

        [XmlArray(IsNullable = false)]
        public List<string> PackageDependencies { get; set; }

        [XmlArray(IsNullable = false)]
        public List<Feature> Features { get; set; }

        // must be a canonically recognized feature.

        [XmlArray(IsNullable = false)]
        public List<Feature> RequiredFeatures { get; set; }

        [XmlArray(IsNullable = false)]
        public List<string> PackageFeeds {
            get {
                return Feeds.IsNullOrEmpty() ? new List<string>() : Feeds.Select(each => each.AbsoluteUri).ToList();
            }
            set {
                Feeds = new List<Uri>(value.Select(each => each.ToUri()));
            }
        }

        [XmlElement(IsNullable = false, ElementName = "Details")]
        public PackageDetails PackageDetails { get; set; }

        [XmlIgnore]
        public string CosmeticName {
            get {
                return "{0}-{1}-{2}".format(Name, Version.ToString(), Architecture);
            }
        }

        [XmlIgnore]
        public List<Uri> Locations { get; set; }

        [XmlIgnore]
        public List<Uri> Feeds { get; set; }

        [XmlIgnore]
        public Composition CompositionData { get; set; }

        [XmlIgnore]
        public XmlSerializer XmlSerializer;

        // soak up anything we don't recognize
        [XmlAnyAttribute]
        public XmlAttribute[] UnknownAttributes;

        [XmlAnyElement]
        public XmlElement[] UnknownElements;
    }

    [XmlRoot(ElementName = "Details", Namespace = "http://coapp.org/atom-package-feed-1.0")]
    public class PackageDetails {
        // Elements marked with XmlIgnore won't persist in the package feed as themselves
        // they get persisted as elements in the Atom Format (so that we have a suitable Atom feed to look at)
        public PackageDetails() {
            Publisher = new Identity();
            Contributors = new List<Identity>();
        }

        [XmlElement(IsNullable = false)]
        public string AuthorVersion { get; set; }

        [XmlElement(IsNullable = false)]
        public string BugTracker { get; set; }

        [XmlArray(IsNullable = false)]
        public List<string> IconLocations {
            get {
                return Icons.IsNullOrEmpty() ? new List<string>() : Icons.Select(each => each.AbsoluteUri).ToList();
            }
            set {
                Icons = new List<Uri>(value.Select(each => each.ToUri()));
            }
        }

        [XmlIgnore]
        public List<Uri> Icons { get; set; }

        [XmlArray(IsNullable = false)]
        public List<License> Licenses { get; set; }

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
        public List<Identity> Contributors { get; set; }

        [XmlIgnore]
        public string CopyrightStatement { get; set; }

        [XmlIgnore]
        public List<string> Tags { get; set; }

        [XmlIgnore]
        public List<string> Categories { get; set; }

        [XmlIgnore]
        public string Description { get; set; }

#if COAPP_ENGINE_CORE
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