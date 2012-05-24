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

    [XmlRoot(ElementName = "Feature", Namespace = "http://coapp.org/atom-package-feed-1.0")]
    public class Feature {

        

        [XmlElement(IsNullable = false)]
        public string Name { get; set; }

        [XmlElement(IsNullable = false)]
        public string VersionInfo { get; set; }

        protected bool Equals(Feature other) {
            return string.Equals(Name, other.Name) && string.Equals(VersionInfo, other.VersionInfo);
        }

        public override bool Equals(object obj) {
            if (ReferenceEquals(null, obj)) {
                return false;
            }
            if (ReferenceEquals(this, obj)) {
                return true;
            }
            if (obj.GetType() != typeof (Feature)) {
                return false;
            }
            return Equals((Feature)obj);
        }

        public override int GetHashCode() {
            unchecked {
                return ((Name != null ? Name.GetHashCode() : 0)*397) ^ (VersionInfo != null ? VersionInfo.GetHashCode() : 0);
            }
        }
        public static bool operator ==(Feature a, Feature b) {
            if (ReferenceEquals(a, b)) {
                return true;
            }
            if (ReferenceEquals(a, null) || ReferenceEquals(b, null)) {
                return false;
            }
            return a.Name == b.Name && a.VersionInfo == b.VersionInfo;
        }

        public static bool operator !=(Feature a, Feature b) {
            return !(a == b);
        }
    }
}