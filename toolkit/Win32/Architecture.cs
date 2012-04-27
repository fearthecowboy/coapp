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
using System.Diagnostics;
using CoApp.Toolkit.Extensions;

namespace CoApp.Toolkit.Win32 {

    public struct Architecture : IComparable, IComparable<Architecture>, IEquatable<Architecture> {
        public static readonly Architecture Unknown = new Architecture { _architecture = ArchType.Unknown };
        public static readonly Architecture Auto = new Architecture { _architecture = ArchType.Auto };
        public static readonly Architecture Any = new Architecture { _architecture = ArchType.Any };
        public static readonly Architecture x86 = new Architecture { _architecture = ArchType.x86 };
        public static readonly Architecture x64 = new Architecture { _architecture = ArchType.x64 };
        public static readonly Architecture arm = new Architecture { _architecture = ArchType.arm };

        private enum ArchType {
            Unknown = 0,
            Auto,
            Any,
            x86,
            x64,
            arm,
        }

        private ArchType _architecture;

        public override string ToString() {
            switch (_architecture) {
                case ArchType.Auto:
                    return "auto";
                case ArchType.Any:
                    return "any";
                case ArchType.x86:
                    return "x86";
                case ArchType.x64:
                    return "x64";
                case ArchType.arm:
                    return "arm";
                default:
                    return "unknown";
            }
        }

        public string InCanonicalFormat {
            get {
                switch (_architecture) {
                    case ArchType.Auto:
                        return "*";
                    case ArchType.Any:
                        return "any";
                    case ArchType.x86:
                        return "x86";
                    case ArchType.x64:
                        return "x64";
                    case ArchType.arm:
                        return "arm";
                    default:
                        return "unknown";
                }
            }
        }

        public string ProcessorArchitecture {
            get {
                switch (_architecture) {
                    case ArchType.Auto:
                        return "*";
                    case ArchType.Any:
                        return "msil";
                    case ArchType.x86:
                        return "x86";
                    case ArchType.x64:
                        return "amd64";
                    case ArchType.arm:
                        return "arm";
                    default:
                        return "*";
                }
            }
        }

        public static implicit operator string(Architecture architecture) {
            return architecture.ToString();
        }

        public static implicit operator Architecture(string architecture) {
            return new Architecture() { _architecture = StringToArch(architecture) };
        }

        private static ArchType StringToArch(string architecture) {
            if( string.IsNullOrEmpty(architecture)) {
                return ArchType.Unknown;
            }

            switch (architecture.ToLower()) {
                case "*":
                case "all":
                case "auto":
                    return ArchType.Auto;

                case "any":
                case "anycpu":
                    return ArchType.Any;

                case "x86":
                case "win32":
                    return ArchType.x86;

                case "x64":
                case "amd64":
                case "em64t":
                case "intel64":
                    return ArchType.x64;

                case "arm":
                case "woa":
                    return ArchType.arm;

                default:
                    return ArchType.Unknown;
            }
        }

        public static bool operator ==(Architecture a, Architecture b) {
            return a._architecture == b._architecture;
        }
        public static bool operator !=(Architecture a, Architecture b) {
            return a._architecture != b._architecture;
        }

        public static bool operator ==(Architecture a, string b) {
            return a._architecture == StringToArch(b);
        }
        public static bool operator !=(Architecture a, string b) {
            return a._architecture != StringToArch(b);
        }

        public override bool Equals(object o) {
            return o is Architecture && Equals((Architecture)o);
        }
        public bool Equals(Architecture other) {
            return other._architecture == _architecture;
        }
        public bool Equals(String other) {
            return _architecture == StringToArch(other);
        }
        public override int GetHashCode() {
            return (int)_architecture;
        }
        public static bool operator <(Architecture a, Architecture b) {
            return a._architecture < b._architecture;
        }
        public static bool operator >(Architecture a, Architecture b) {
            return a._architecture > b._architecture;
        }

        public int CompareTo(object other) {
            if( other == null ) {
                return 1;
            }
            return other is Architecture ? _architecture.CompareTo(((Architecture)other)._architecture) : _architecture.CompareTo(StringToArch(other.ToString()));
        }

        public int CompareTo(Architecture other) {
            return _architecture.CompareTo(other._architecture);
        }

        public static Architecture Parse(string input) {
            return new Architecture { _architecture = StringToArch(input) };
        }

        public static bool TryParse(string input, out Architecture ret) {
            ret._architecture = StringToArch(input);
            return true;
        } 
    }


    public struct TwoPartVersion : IComparable, IComparable<TwoPartVersion>,IEquatable<TwoPartVersion> {
        private uint _version;

        public override string ToString() {
            return UIntToString(_version);
        }

        public static implicit operator uint(TwoPartVersion version) {
            return version._version;
        }

        public static implicit operator string(TwoPartVersion version) {
            return version.ToString();
        }

        public static implicit operator TwoPartVersion(uint version) {
            return new TwoPartVersion { _version = version };
        }
        public static implicit operator TwoPartVersion(string version) {
            return new TwoPartVersion() { _version = StringToUInt(version) };
        }
        private static string UIntToString(uint version) {
            return String.Format("{0}.{1}", (version >> 16) & 0xFFFF, (version) & 0xFFFF);
        }

        private static uint StringToUInt(string version) {
            if (String.IsNullOrEmpty(version)) {
                return 0;
            }

            var vers = version.Split('.');
            var major = vers.Length > 0 ? vers[0].ToInt32(0) : 0;
            var minor = vers.Length > 1 ? vers[1].ToInt32(0) : 0;

            return (((uint) major) << 16) + (uint) minor;
        }
        public static implicit operator FourPartVersion(TwoPartVersion version) {
            return ((ulong)version) << 32;
        }

        public static bool operator ==(TwoPartVersion a, TwoPartVersion b) {
            return a._version == b._version;
        }
        public static bool operator !=(TwoPartVersion a, TwoPartVersion b) {
            return a._version != b._version;
        }
        public static bool operator <(TwoPartVersion a, TwoPartVersion b) {
            return a._version < b._version;
        }
        public static bool operator >(TwoPartVersion a, TwoPartVersion b) {
            return a._version > b._version;
        }
        public override bool Equals(object o) {
            return o is TwoPartVersion && Equals((TwoPartVersion)o);
        }
        public bool Equals(TwoPartVersion other) {
            return other._version == _version;
        }
        public override int GetHashCode() {
            return (int)_version;
        }
        public static implicit operator TwoPartVersion(FileVersionInfo versionInfo) {
            return new TwoPartVersion {_version = ((uint) versionInfo.FileMajorPart << 16) | (uint) versionInfo.FileMinorPart};
        }
        public int CompareTo(object obj) {
            return obj is TwoPartVersion ? _version.CompareTo(((TwoPartVersion)obj)._version) :
                obj is FourPartVersion ? _version.CompareTo(((ulong)(FourPartVersion)obj)) :
                obj is ulong ? _version.CompareTo((ulong)obj) : 
                obj is uint ? _version.CompareTo((uint)obj) :
                obj is string ? _version.CompareTo(((TwoPartVersion)(string)obj)._version) : 
                0;
        }

        public int CompareTo(TwoPartVersion other) {
            return _version.CompareTo(other._version);
        }

        public static TwoPartVersion Parse(string input) {
            return new TwoPartVersion  { _version = StringToUInt(input) };
        }
        public static bool TryParse(string input, out TwoPartVersion ret) {
            ret._version = StringToUInt(input);
            return true;
        } 
    }

    public struct FourPartVersion : IComparable, IComparable<FourPartVersion>,IEquatable<FourPartVersion> {
        private ulong _version;

        public override string ToString() {
            return ULongToString(_version);
        }

        public static implicit operator ulong(FourPartVersion version) {
            return version._version;
        }

        public static implicit operator Version(FourPartVersion version) {
            return new Version((int)((version >> 48) & 0xFFFF), (int)((version >> 32) & 0xFFFF), (int)((version >> 16) & 0xFFFF),(int)((version) & 0xFFFF));
        }
        public static implicit operator string(FourPartVersion version) {
            return version.ToString();
        }
        public static implicit operator FourPartVersion(Version version) {
            return new FourPartVersion { _version = ((ulong)version.Major << 48) + ((ulong)version.Minor << 32) + ((ulong)version.Build << 16) + (ulong)version.Revision };
        }
        public static implicit operator FourPartVersion(ulong version) {
            return new FourPartVersion { _version = version };
        }
        public static implicit operator FourPartVersion(string version) {
            return new FourPartVersion() { _version = StringToULong(version) };
        }

        private static string ULongToString( ulong version ) {
            return String.Format("{0}.{1}.{2}.{3}", (version >> 48) & 0xFFFF, (version >> 32) & 0xFFFF, (version >> 16) & 0xFFFF, (version) & 0xFFFF);
        }

        private static ulong StringToULong(string version ) {
            if (String.IsNullOrEmpty(version)) {
                return 0L;
            }

            var vers = version.Split('.');
            var major = vers.Length > 0 ? vers[0].ToInt32(0) : 0;
            var minor = vers.Length > 1 ? vers[1].ToInt32(0) : 0;
            var build = vers.Length > 2 ? vers[2].ToInt32(0) : 0;
            var revision = vers.Length > 3 ? vers[3].ToInt32(0) : 0;

            return (((UInt64)major) << 48) + (((UInt64)minor) << 32) + (((UInt64)build) << 16) + (UInt64)revision;
        }

        public static implicit operator TwoPartVersion(FourPartVersion version) {
            return ((uint)(version >> 32)) ;
        }

        public static bool operator ==(FourPartVersion a, FourPartVersion b) {
            return a._version == b._version;
        }

        public static bool operator !=(FourPartVersion a, FourPartVersion b) {
            return a._version != b._version;
        }
        public static bool operator <(FourPartVersion a, FourPartVersion b) {
            return a._version < b._version;
        }
        public static bool operator >(FourPartVersion a, FourPartVersion b) {
            return a._version > b._version;
        }
        public override bool Equals(object o) {
            return o is FourPartVersion && Equals((FourPartVersion)o);
        }
        public bool Equals(FourPartVersion other) {
            return other._version == _version;
        }
        public override int GetHashCode() {
            return _version.GetHashCode();
        }

        public int CompareTo(object obj) {
            return obj is FourPartVersion ? _version.CompareTo(((FourPartVersion)obj)._version) :
                obj is TwoPartVersion ? _version.CompareTo(((uint)(TwoPartVersion)obj)) :
                obj is ulong ? _version.CompareTo((ulong)obj) :
                obj is uint ? _version.CompareTo((uint)obj) :
                obj is string ? _version.CompareTo(((FourPartVersion)(string)obj)._version) : 
                0;
        }

        public int CompareTo(FourPartVersion other) {
            return _version.CompareTo(other._version);
        }

        public static implicit operator FourPartVersion(FileVersionInfo versionInfo) {
            return new FourPartVersion { _version = ((ulong)versionInfo.FileMajorPart << 48) | ((ulong)versionInfo.FileMinorPart << 32) | ((ulong)versionInfo.FileBuildPart << 16) | (ulong)versionInfo.FilePrivatePart  };
        }

        public static FourPartVersion Parse(string input) {
            return new FourPartVersion {_version = StringToULong(input) };
        }
        public static bool TryParse(string input, out FourPartVersion ret) {
            ret._version = StringToULong(input);
            return true;
        } 
    }
}