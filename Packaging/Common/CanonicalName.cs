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
    using System.Text.RegularExpressions;
    using System.Xml;
    using System.Xml.Schema;
    using System.Xml.Serialization;
    using Toolkit.Exceptions;
    using Toolkit.Extensions;
    using Toolkit.Win32;

    [XmlRoot(ElementName = "CanonicalName", Namespace = "http://coapp.org/atom-package-feed-1.0")]
    public class CanonicalName : IComparable, IComparable<CanonicalName>, IEquatable<CanonicalName>, IXmlSerializable {
        private static readonly char[] Slashes;
        private static readonly Regex CoappRx;
        private static readonly Regex PartialCoappRx;

        public static CanonicalName AllPackages;
        public static CanonicalName CoAppPackages;
        public static CanonicalName NugetPackages;
        public static CanonicalName CoAppItself;
        public static CanonicalName CoAppDevtools;
        
        static CanonicalName() {
            Slashes = new[] { '\\', '/' };
            CoappRx = new Regex(@"^(?<name>.+?)(?<flavor>\[.+\])?(?<v1>-\d{1,5})(?<v2>\.\d{1,5})(?<v3>\.\d{1,5})(?<v4>\.\d{1,5})(?<arch>-any|-x86|-x64|-arm)(?<pkt>-[0-9a-f]{16})$", RegexOptions.IgnoreCase);
            PartialCoappRx = new Regex(@"^(?<name>.*?)?(?<flavor>\[.*\])?(?<v1>-\d{1,5}|-\*)?(?<v2>\.\d{1,5}|\.\*)?(?<v3>\.\d{1,5}|\.\*)?(?<v4>\.\d{1,5}|\.\*)?(?<plus>\+)?(?<arch>-{1,2}any|-{1,2}x86|-{1,2}x64|-{1,2}arm|-{1,2}all|-{1,2}auto|-\*)?(?<pkt>-{1,3}[0-9a-f]{16}|-\*)?$", RegexOptions.IgnoreCase);

            AllPackages = "*:*";
            CoAppPackages = "coapp:*";
            NugetPackages = "nuget:*";
            CoAppItself = "coapp:coapp-*-any-1e373a58e25250cb";
            CoAppDevtools = "coapp:coapp.devtools-*-any-1e373a58e25250cb";
        }

        public PackageType PackageType { get; private set; }
        public string Name { get; private set; }
        public FlavorString Flavor { get; private set; }
        public FourPartVersion Version { get; private set; }
        public Architecture Architecture { get; private set; }
        public string PublicKeyToken { get; private set; }
        public bool MatchVersionOrGreater { get; private set; }
        public bool IsCanonical { get; private set; }
        private string _generalName;
        private string _simpleName;
        private string _canonicalName;
        private string _packageName;
        private int? _hashCode;

        public bool IsPartial {
            get {
                return !IsCanonical || MatchVersionOrGreater || Version == 0;
            }
        }

        public string GeneralName {
            get {
                if( _generalName == null ) {
                    var allVersions = OtherVersionFilter;
                    if (IsPartial) {
                        // we have think a bit more for general names on partial matches.

                        // we can't match a partial name on a flavor.
                        if (allVersions.Flavor.ToString().Contains("*")) {
                            allVersions.Flavor = "";
                        }

                        // we can't match a wildcard on architecture. Default to Any.
                        if (allVersions.Architecture == Architecture.Auto || allVersions.Architecture == Architecture.Unknown) {
                            allVersions.Architecture = Architecture.Any;
                        }

                        allVersions.Name = Name.Replace("*", "");
                    }
                    _generalName = allVersions.ToString();
                }
                return _generalName;
            }
        }

        public string SimpleName {
            get {
                if (_simpleName == null) {
                    if (PackageType == PackageType.CoApp) {
                        _simpleName = "{0}{1}".format(Name, Flavor);
                    } else if (PackageType == PackageType.NuGet) {
                        _simpleName = "{0}:{1}".format(PackageType, "(NOT IMPLEMENTED)");
                    } else if (PackageType == PackageType.Chocolatey) {
                        _simpleName = "{0}:{1}".format(PackageType, "(NOT IMPLEMENTED)");
                    } else if (PackageType == PackageType.Python) {
                        _simpleName = "{0}:{1}".format(PackageType, "(NOT IMPLEMENTED)");
                    } else if (PackageType == PackageType.Perl) {
                        _simpleName = "{0}:{1}".format(PackageType, "(NOT IMPLEMENTED)");
                    } else if (PackageType == PackageType.PHP) {
                        _simpleName = "{0}:{1}".format(PackageType, "(NOT IMPLEMENTED)");
                    }
                }
                return _simpleName ?? (_simpleName = "");
            }
        }

        public string PackageName {
            get {
                return _packageName ?? (_packageName = ToString().Substring(_canonicalName.IndexOf(':') + 1));
            }
        }

        internal CanonicalName() {
        }

        public CanonicalName(string canonicalName) {
            TryParseImpl(canonicalName, this);
        }

        public bool Matches(CanonicalName packageCriteria) {
            if (null == packageCriteria) {
                return false;
            }

            if (IsPartial) {
                throw new CoAppException("CanonicalName.Matches() may not be called on a Partial Package Name.");
            }

            // looking for a single package 
            if (packageCriteria.IsCanonical) {
                return Equals(packageCriteria);
            }

            return PackageType == packageCriteria.PackageType &&
                Name.NewIsWildcardMatch(packageCriteria.Name) &&
                    Flavor.IsWildcardMatch(packageCriteria.Flavor) &&
                        (Version == packageCriteria.Version || (packageCriteria.MatchVersionOrGreater && Version > packageCriteria.Version)) &&
                            (packageCriteria.Architecture == Architecture.Auto || packageCriteria.Architecture == Architecture) &&
                                PublicKeyToken.NewIsWildcardMatch(packageCriteria.PublicKeyToken);
        }

        public int MatchQuality(CanonicalName packageCriteria) {
            if (null == packageCriteria) {
                return 0;
            }

            if( !Matches(packageCriteria) ) {
                return 0;
            }

            int result =1;

            if (Name.Equals(packageCriteria.Name, StringComparison.CurrentCultureIgnoreCase)) {
                result++;
            }

            if( Flavor == packageCriteria.Flavor) {
                result++;
            }
            
            if( Version == packageCriteria.Version) {
                result++;
            }

            if (packageCriteria.Architecture == Architecture) {
                result++;
            }

            if( PublicKeyToken == packageCriteria.PublicKeyToken) {
                result++;
            }

            return result;
        }

        public bool DiffersOnlyByVersion(CanonicalName otherPackage) {
            if( IsPartial) {
                return PackageType == otherPackage.PackageType &&
                    Name == otherPackage.Name &&
                        otherPackage.Flavor.IsWildcardMatch(Flavor) &&
                            Architecture == otherPackage.Architecture &&
                                PublicKeyToken == otherPackage.PublicKeyToken;
            }

            return PackageType == otherPackage.PackageType &&
                Name == otherPackage.Name &&
                    Flavor == otherPackage.Flavor &&
                        Architecture == otherPackage.Architecture &&
                            PublicKeyToken == otherPackage.PublicKeyToken;
        }

        public CanonicalName OtherVersionFilter {
            get {
                return new CanonicalName {
                    PackageType = PackageType,
                    Name = Name,
                    Flavor = Flavor,
                    Version = 0,
                    Architecture = Architecture,
                    PublicKeyToken = PublicKeyToken,
                    MatchVersionOrGreater = true,
                    IsCanonical = false,
                };
            }
        }

        public CanonicalName OtherArchitectureFilter {
            get {
                return new CanonicalName {
                    PackageType = PackageType,
                    Name = Name,
                    Flavor = Flavor,
                    Version = Version,
                    Architecture = Architecture.Auto,
                    PublicKeyToken = PublicKeyToken,
                    MatchVersionOrGreater = MatchVersionOrGreater,
                    IsCanonical = false,
                };
            }
        }

       
        public override string ToString() {
            if (_canonicalName == null) {
                if (PackageType == PackageType.CoApp) {
                    _canonicalName = "{0}:{1}{2}-{3}{4}-{5}-{6}".format(PackageType, Name, Flavor, Version, (MatchVersionOrGreater ? "+" : ""), Architecture.InCanonicalFormat, PublicKeyToken);
                } else if (PackageType == PackageType.NuGet) {
                    _canonicalName = "{0}:{1}".format(PackageType, "(NOT IMPLEMENTED)");
                } else if (PackageType == PackageType.Chocolatey) {
                    _canonicalName = "{0}:{1}".format(PackageType, "(NOT IMPLEMENTED)");
                } else if (PackageType == PackageType.Python) {
                    _canonicalName = "{0}:{1}".format(PackageType, "(NOT IMPLEMENTED)");
                } else if (PackageType == PackageType.Perl) {
                    _canonicalName = "{0}:{1}".format(PackageType, "(NOT IMPLEMENTED)");
                } else if (PackageType == PackageType.PHP) {
                    _canonicalName = "{0}:{1}".format(PackageType, "(NOT IMPLEMENTED)");
                }
            }
            return _canonicalName ?? (_canonicalName = "");
        }

        public static implicit operator string(CanonicalName canonicalName) {
            if( ReferenceEquals(canonicalName,null)) {
                return null;
            }
            return canonicalName.ToString();
        }

        public static implicit operator Guid(CanonicalName canonicalName) {
            return canonicalName.ToString().CreateGuid();
        }

        public static implicit operator CanonicalName(string canonicalName) {
            return new CanonicalName(canonicalName);
        }

        public static bool operator ==(CanonicalName a, CanonicalName b) {
            if (ReferenceEquals(a, b)) {
                return true;
            }

            if (ReferenceEquals(a, null) || ReferenceEquals(b, null)) {
                return false;
            }

            return a.Equals(b);
        }

        public static bool operator !=(CanonicalName a, CanonicalName b) {
            if (ReferenceEquals(a, b)) {
                return false;
            }

            if (ReferenceEquals(a, null) || ReferenceEquals(b, null)) {
                return true;
            }

            return !a.Equals(b);
        }

        public static bool operator ==(CanonicalName a, string b) {
            if (ReferenceEquals(a, null) && ReferenceEquals(b, null)) {
                return true;
            }

            if (ReferenceEquals(a, null) || ReferenceEquals(b, null)) {
                return false;
            }

            CanonicalName pcn;
            return TryParse(b, out pcn) && a.Equals(pcn);
        }

        public static bool operator !=(CanonicalName a, string b) {
            if (ReferenceEquals(a, null) && ReferenceEquals(b, null)) {
                return false;
            }

            if (ReferenceEquals(a, null) || ReferenceEquals(b, null)) {
                return true;
            }

            CanonicalName pcn;
            return !TryParse(b, out pcn) || !a.Equals(pcn);
        }

        public override bool Equals(object o) {
            return o is CanonicalName && Equals((CanonicalName)o);
        }

        public bool Equals(CanonicalName other) {
            if (ReferenceEquals(other, null)) {
                return false;
            }
            return other.GetHashCode() == GetHashCode();
        }

        public bool Equals(String other) {
            CanonicalName pcn;
            return TryParse(other, out pcn) && Equals(pcn);
        }

        public override int GetHashCode() {
            return (int)(_hashCode ?? (_hashCode = ToString().GetHashCode()));
        }

        public static bool operator <(CanonicalName a, CanonicalName b) {
            return a.CompareTo(b) < 0;
        }

        public static bool operator >(CanonicalName a, CanonicalName b) {
            return a.CompareTo(b) > 0;
        }

        public static bool operator <=(CanonicalName a, CanonicalName b) {
            return a.CompareTo(b) <= 0;
        }

        public static bool operator >=(CanonicalName a, CanonicalName b) {
            return a.CompareTo(b) >= 0;
        }

        public int CompareTo(object other) {
            if (ReferenceEquals(other, null)) {
                return 1;
            }
            if (ReferenceEquals(other, this)) {
                return 0;
            }

            var b = other as CanonicalName;

            if (((object)b) == null) {
                CanonicalName c;
                if (!TryParse(other.ToString(), out c)) {
                    return 1;
                }
                b = c;
            }
            // compares in the following order:
            // packagetype => Name => Flavor => Architecture => Version => PublicKeyToken

            return PackageType.CompareTo(b.PackageType).WithCompareResult(-1, 1,
                Name.CompareTo(b.Name).WithCompareResult(-1, 1,
                    (Flavor).CompareTo(b.Flavor).WithCompareResult(-1, 1,
                        (Architecture).CompareTo(b.Architecture).WithCompareResult(-1, 1,
                            (Version).CompareTo(b.Version).WithCompareResult(-1, 1,
                                (PublicKeyToken).CompareTo(b.PublicKeyToken).WithCompareResult(-1, 1, 0)
                                )
                            )
                        )
                    )
                );
        }

        public int CompareTo(CanonicalName other) {
            if (ReferenceEquals(other, null)) {
                return 1;
            }
            if (ReferenceEquals(other, this)) {
                return 0;
            }

            // compares in the following order:
            // packagetype => Name => Flavor => Architecture => Version => PublicKeyToken

            return PackageType.CompareTo(other.PackageType).WithCompareResult(-1, 1,
                Name.CompareTo(other.Name).WithCompareResult(-1, 1,
                    (Flavor).CompareTo(other.Flavor).WithCompareResult(-1, 1,
                        (Architecture).CompareTo(other.Architecture).WithCompareResult(-1, 1,
                            (Version).CompareTo(other.Version).WithCompareResult(-1, 1,
                                (PublicKeyToken).CompareTo(other.PublicKeyToken).WithCompareResult(-1, 1, 0)
                                )
                            )
                        )
                    )
                );
        }

        public static CanonicalName Parse(string input) {
            CanonicalName result;
            return TryParse(input, out result) ? result : null;
        }

        public static bool TryParse(string input, out CanonicalName result) {
            // send me a null, and you sleep with the fishes.
            if (input == null) {
                result = null;
                return false;
            }
            var output = new CanonicalName();
            var success = TryParseImpl(input, output);
            result = success ? output : null;
            return success;
        }

        private static bool TryParseImpl(string input, CanonicalName result) {
            // send me a null, and you sleep with the fishes.
            if (input == null) {
                return false;
            }

            // an empty string is equivalent to "coapp:*"
            if (string.IsNullOrEmpty(input)) {
                input = "coapp:*";
            }

            var i = input.IndexOf(':');
            if (i == -1) {
                return TryParseCoApp(input, result);
            }

            var pt = PackageType.Parse(input.Substring(0, i));
            var pkgName = input.Substring(i + 1).ToLower();
            if (pt == PackageType.CoApp) {
                return TryParseCoApp(pkgName, result);
            }
            if (pt == PackageType.NuGet) {
                return TryParseNuGet(pkgName, result);
            }
            if (pt == PackageType.Chocolatey) {
                return TryParseChocolatey(pkgName, result);
            }
            if (pt == PackageType.Python) {
                return TryParsePython(pkgName, result);
            }
            if (pt == PackageType.Perl) {
                return TryParsePerl(pkgName, result);
            }
            if (pt == PackageType.PHP) {
                return TryParsePHP(pkgName, result);
            }

            throw new CoAppException("Unhandled Package Type");
        }

        

        private static void SetFieldsFromMatch(Match match, ref CanonicalName result, bool isCanonical) {
            result.IsCanonical = isCanonical;

            var version1 = match.GetValue("v1", "0");
            var version2 = match.GetValue("v2", ".0");
            var version3 = match.GetValue("v3", ".0");
            var version4 = match.GetValue("v4", ".0");

            result.Name = match.GetValue("name").IfNullOrEmpty("*");
            result.MatchVersionOrGreater = !(match.Groups["v1"].Success && match.Groups["v2"].Success && match.Groups["v3"].Success && match.Groups["v4"].Success) || match.Groups["plus"].Success;
            
            result.Version = version1 + version2 + version3 + version4;
            
            // no version is always a wildcard match.
            if (result.Version == 0) {
                result.MatchVersionOrGreater = true;
            }

            // if we are a wildcard match (however it happened), we're not canonical
            if (result.MatchVersionOrGreater) {
                // this isn't canonical. 
                result.IsCanonical = false;
            }

            result.Architecture = match.GetValue("arch").IfNullOrEmpty("*");
            
            if (result.Architecture == Architecture.Unknown || result.Architecture == Architecture.Auto) {
                // if the architecture is unknown/auto, we're not canonical
                result.Architecture = Architecture.Auto;
                isCanonical = false;
            }

            // an empty flavor is a valid value. if we're not going to be a canonical name anyway, then go for wide open.
            result.Flavor = isCanonical ? match.GetValue("flavor") : match.GetValue("flavor").IfNullOrEmpty("*");

            result.PublicKeyToken = match.GetValue("pkt").IfNullOrEmpty("*");
        }

        private static bool TryParseCoApp(string input, CanonicalName result) {
            if (input.IndexOfAny(Slashes) > -1) {
                return false;
            }

            var match = CoappRx.Match(input);
            if (match.Success) {
                // perfect canonical match for a name 
                SetFieldsFromMatch(match, ref result,true);
                result.PackageType = PackageType.CoApp;
                return true;
            }

            // after this point, we're only able to come up with a partial package name
            match = PartialCoappRx.Match(input);
            if (match.Success) {
                SetFieldsFromMatch(match, ref result, false);
                result.PackageType = PackageType.CoApp;
                return true;
            }

            // we're going to assume we got a package name, and the rest is wildcard.
            result.Name = input;
            result.Version = 0;
            result.MatchVersionOrGreater = true;
            result.PackageType = PackageType.CoApp;
            result.Flavor = "*";
            result.Architecture = Architecture.Auto;
            result.PublicKeyToken = "*";
            result.IsCanonical = false;

            return true;
        }

        private static bool TryParseNuGet(string input, CanonicalName result) {
            return false;
        }

        private static bool TryParseChocolatey(string input, CanonicalName result) {
            return false;
        }

        private static bool TryParsePython(string input, CanonicalName result) {
            return false;
        }

        private static bool TryParsePerl(string input, CanonicalName result) {
            return false;
        }

        private static bool TryParsePHP(string input, CanonicalName result) {
            return false;
        }

        public XmlSchema GetSchema() {
            return null;
        }

        public void ReadXml(XmlReader reader) {
            reader.MoveToContent();
            var isEmptyElement = reader.IsEmptyElement;
            reader.ReadStartElement();
            if (!isEmptyElement) {
                TryParseImpl(reader.ReadString(), this);
                reader.ReadEndElement();
            }
        }

        public void WriteXml(XmlWriter writer) {
            writer.WriteString(ToString());
        }
    }
}