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

namespace CoApp.Packaging.Common {
    using System;
    using Toolkit.Exceptions;

    public struct PackageType : IComparable, IComparable<PackageType>, IEquatable<PackageType> {
        public static readonly PackageType CoApp = new PackageType {_packageType = PkgType.CoApp};
        public static readonly PackageType NuGet = new PackageType {_packageType = PkgType.NuGet};
        public static readonly PackageType Chocolatey = new PackageType {_packageType = PkgType.Chocolatey};
        public static readonly PackageType Python = new PackageType {_packageType = PkgType.Python};
        public static readonly PackageType PHP = new PackageType {_packageType = PkgType.PHP};
        public static readonly PackageType NodeJS = new PackageType {_packageType = PkgType.NodeJS};
        public static readonly PackageType Perl = new PackageType {_packageType = PkgType.Perl};

        private enum PkgType {
            CoApp = 0,
            NuGet,
            Chocolatey,
            Python,
            PHP,
            NodeJS,
            Perl,
        }

        private PkgType _packageType;

        public override string ToString() {
            switch (_packageType) {
                case PkgType.CoApp:
                    return "coapp";
                case PkgType.NuGet:
                    return "nuget";
                case PkgType.Chocolatey:
                    return "chocolatey";
                case PkgType.Python:
                    return "python";
                case PkgType.PHP:
                    return "php";
                case PkgType.NodeJS:
                    return "nodejs";
                case PkgType.Perl:
                    return "perl";
            }
            throw new CoAppException("Unsupported package format.");
        }

        public static implicit operator string(PackageType packageType) {
            return packageType.ToString();
        }

        public static implicit operator PackageType(string packageType) {
            return new PackageType {_packageType = StringToPackageType(packageType)};
        }

        private static PkgType StringToPackageType(string packageType) {
            if (string.IsNullOrEmpty(packageType)) {
                return PkgType.CoApp;
            }

            switch (packageType.ToLower()) {
                case "":
                case "*":
                case "auto":
                case "coapp":
                    return PkgType.CoApp;

                case "nuget":
                    return PkgType.NuGet;

                case "python":
                case "pypi":
                case "egg":
                    return PkgType.Python;

                case "chocolatey":
                case "chocolate":
                    return PkgType.Chocolatey;

                case "php":
                case "pear":
                case "pecl":
                    return PkgType.PHP;

                case "node":
                case "nodejs":
                case "npm":
                    return PkgType.NodeJS;

                case "perl":
                case "cpan":
                    return PkgType.Perl;
            }
            throw new CoAppException("Unrecognized package type");
        }

        public static bool operator ==(PackageType a, PackageType b) {
            return a._packageType == b._packageType;
        }

        public static bool operator !=(PackageType a, PackageType b) {
            return a._packageType != b._packageType;
        }

        public static bool operator ==(PackageType a, string b) {
            return a._packageType == StringToPackageType(b);
        }

        public static bool operator !=(PackageType a, string b) {
            return a._packageType != StringToPackageType(b);
        }

        public override bool Equals(object o) {
            return o is PackageType && Equals((PackageType)o);
        }

        public bool Equals(PackageType other) {
            return other._packageType == _packageType;
        }

        public bool Equals(String other) {
            return _packageType == StringToPackageType(other);
        }

        public override int GetHashCode() {
            return (int)_packageType;
        }

        public static bool operator <(PackageType a, PackageType b) {
            return a._packageType < b._packageType;
        }

        public static bool operator >(PackageType a, PackageType b) {
            return a._packageType > b._packageType;
        }

        public static bool operator <=(PackageType a, PackageType b) {
            return a._packageType <= b._packageType;
        }

        public static bool operator >=(PackageType a, PackageType b) {
            return a._packageType >= b._packageType;
        }


        public int CompareTo(object other) {
            if (other == null) {
                return 1;
            }
            return other is PackageType ? _packageType.CompareTo(((PackageType)other)._packageType) : _packageType.CompareTo(StringToPackageType(other.ToString()));
        }

        public int CompareTo(PackageType other) {
            return _packageType.CompareTo(other._packageType);
        }

        public static PackageType Parse(string input) {
            return new PackageType {_packageType = StringToPackageType(input)};
        }

        public static bool TryParse(string input, out PackageType ret) {
            ret._packageType = StringToPackageType(input);
            return true;
        }
    }
}