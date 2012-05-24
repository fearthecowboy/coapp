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

    [XmlRoot(ElementName = "Role", Namespace = "http://coapp.org/atom-package-feed-1.0")]
    public class Role {
        [XmlAttribute]
        public string Name { get; set; }

        [XmlAttribute]
        public PackageRole PackageRole { get; set; }

        public static bool operator ==(Role a, Role b) {
            if (ReferenceEquals(a, b)) {
                return true;
            }
            if (ReferenceEquals(a, null) || ReferenceEquals(b, null)) {
                return false;
            }
            return a.Name == b.Name && a.PackageRole == b.PackageRole;
        }

        public static bool operator !=(Role a, Role b) {
            return !(a == b);
        }

        protected bool Equals(Role other) {
            return string.Equals(Name, other.Name) && Equals(PackageRole, other.PackageRole);
        }

        public override bool Equals(object obj) {
            if (ReferenceEquals(null, obj)) {
                return false;
            }
            if (ReferenceEquals(this, obj)) {
                return true;
            }
            if (obj.GetType() != typeof (Role)) {
                return false;
            }
            return Equals((Role)obj);
        }

        public override int GetHashCode() {
            unchecked {
                return ((Name != null ? Name.GetHashCode() : 0)*397) ^ PackageRole.GetHashCode();
            }
        }
    }
}