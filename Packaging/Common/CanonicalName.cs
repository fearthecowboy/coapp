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
    using Toolkit.Exceptions;
    using Toolkit.Extensions;
    using Toolkit.Win32;

    public class CanonicalName : IComparable, IComparable<CanonicalName>, IEquatable<CanonicalName> {
        public static CanonicalName AllPackages = "*:*";
        public static CanonicalName CoAppPackages = "coapp:*";
        public static CanonicalName NugetPackages = "nuget:*";
        public static CanonicalName CoAppItself = "coapp:coapp.toolkit-*-any-1e373a58e25250cb";

        public PackageType PackageType { get; private set; }
        public string Name { get; private set; }
        public FlavorString Flavor { get; private set; }
        public FourPartVersion Version { get; private set; }
        public Architecture Architecture { get; private set; }
        public string PublicKeyToken { get; private set; }
        public bool MatchVersionOrGreater { get; private set; }
        public bool IsCanonical { get; private set; }
        private string _generalName;
        private string _wholeName;

        public bool IsPartial {
            get {
                return !IsCanonical || MatchVersionOrGreater || Version == 0;
            }
        }

        public string GeneralName {
            get {
                return _generalName ?? (_generalName = OtherVersionFilter.ToString());
            }
        }

        public string WholeName {
            get {
                if (_wholeName == null) {
                    if (PackageType == PackageType.CoApp) {
                        _wholeName = "{1}{2}".format(Name, Flavor);
                    } else if (PackageType == PackageType.NuGet) {
                        _wholeName = "{0}:{1}".format(PackageType, "(NOT IMPLEMENTED)");
                    } else if (PackageType == PackageType.Chocolatey) {
                        _wholeName = "{0}:{1}".format(PackageType, "(NOT IMPLEMENTED)");
                    } else if (PackageType == PackageType.Python) {
                        _wholeName = "{0}:{1}".format(PackageType, "(NOT IMPLEMENTED)");
                    } else if (PackageType == PackageType.Perl) {
                        _wholeName = "{0}:{1}".format(PackageType, "(NOT IMPLEMENTED)");
                    } else if (PackageType == PackageType.PHP) {
                        _wholeName = "{0}:{1}".format(PackageType, "(NOT IMPLEMENTED)");
                    }
                }
                return _wholeName ?? (_wholeName = "");
            }
        }

        private string _canonicalName;
        private int? _hashCode;

        private CanonicalName() {
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
                Name.IsWildcardMatch(packageCriteria.Name) &&
                    Flavor.IsWildcardMatch(packageCriteria.Flavor) &&
                        (Version == packageCriteria.Version || (packageCriteria.MatchVersionOrGreater && Version > packageCriteria.Version)) &&
                            (packageCriteria.Architecture == Architecture.Auto || packageCriteria.Architecture == Architecture) &&
                                PublicKeyToken.IsWildcardMatch(packageCriteria.PublicKeyToken);
        }

        public bool DiffersOnlyByVersion(CanonicalName otherPackage) {
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

        public override string ToString() {
            if (_canonicalName == null) {
                if (PackageType == PackageType.CoApp) {
                    _canonicalName = "{0}:{1}{2}-{3}{4}-{5}-{6}".format(PackageType, Name, Flavor, Version, MatchVersionOrGreater ? "+" : "", Architecture.InCanonicalFormat, PublicKeyToken);
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

        private static readonly char[] Slashes = new[] {'\\', '/'};

        private static readonly Regex CoappRx = new Regex(@"^(?<name>.+)(?<flavor>\[.+\])?(?<v1>-\d{1,5})(?<v2>\.\d{1,5})(?<v3>\.\d{1,5})(?<v4>\.\d{1,5})(?<arch>-any|-x86|-x64|-arm)(?<pkt>-[0-9a-f]{16})$", RegexOptions.IgnoreCase);

        private static readonly Regex PartialCoappRx =
            new Regex(@"^(?<name>.*?)?(?<flavor>\[.+\])?(?<v1>-\d{1,5}|-\*)?(?<v2>\.\d{1,5}|\.\*)?(?<v3>\.\d{1,5}|\.\*)?(?<v4>\.\d{1,5}|\.\*)?(<plus>\+)?(?<arch>-{1,2}any|-{1,2}x86|-{1,2}x64|-{1,2}arm|-{1,2}all|-\*)?(?<pkt>-{1,3}[0-9a-f]{16})?$",
                RegexOptions.IgnoreCase);

        private static void SetFieldsFromMatch(Match match, ref CanonicalName result) {
            var version1 = match.GetValue("v1", "0");
            var version2 = match.GetValue("v2", ".0");
            var version3 = match.GetValue("v3", ".0");
            var version4 = match.GetValue("v4", ".0");

            result.Name = match.GetValue("name");
            result.MatchVersionOrGreater = !(match.Groups["v1"].Success && match.Groups["v2"].Success && match.Groups["v3"].Success && match.Groups["v4"].Success) || match.Groups["plus"].Success;
            result.Version = version1 + version2 + version3 + version4;
            result.Flavor = match.GetValue("flavor");
            result.Architecture = match.GetValue("arch");
            result.PublicKeyToken = match.GetValue("pkt");
        }

        private static bool TryParseCoApp(string input, CanonicalName result) {
            if (input.IndexOfAny(Slashes) > -1) {
                return false;
            }

            var match = CoappRx.Match(input);
            if (match.Success) {
                // perfect canonical match for a name 
                SetFieldsFromMatch(match, ref result);
                result.PackageType = PackageType.CoApp;
                if (result.Version == 0) {
                    result.MatchVersionOrGreater = true;
                } else {
                    result.IsCanonical = true;
                }
                return true;
            }

            // after this point, we're only able to come up with a partial package name
            match = PartialCoappRx.Match(input);
            if (match.Success) {
                SetFieldsFromMatch(match, ref result);
                result.PackageType = PackageType.CoApp;
                result.IsCanonical = false;
                return true;
            }

            result.Name = input;
            result.Version = "0.0.0.0";
            result.MatchVersionOrGreater = true;
            result.PackageType = PackageType.CoApp;
            result.Flavor = null;
            result.Architecture = Architecture.Unknown;
            result.PublicKeyToken = null;
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
    }
}