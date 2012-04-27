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
    using System.Collections.Generic;
    using System.Linq;
    using CoApp.Toolkit.Extensions;

    public class FlavorString : IComparable, IComparable<FlavorString>, IEquatable<FlavorString> {
        private readonly IEnumerable<string> _strings;
        private readonly string _string;
        private int? _hashCode;

        public FlavorString(string flavor) {
            // null flavor means no flavor
            if( string.IsNullOrEmpty(flavor)) {
                _string = "";
                _hashCode = 0x4A3B9853;
                _strings = Enumerable.Empty<string>();
                return;
            }

            // remove any extra brackets
            flavor = flavor.TrimStart('[').TrimEnd(']');
            
            // gotta check again after its trimmed.
            if (string.IsNullOrEmpty(flavor)) {
                _string = "";
                _hashCode = 0x4A3B9853;
                _strings = Enumerable.Empty<string>();
                return;
            }

            var i = flavor.IndexOf('-');
            if( i > -1 ) {
                _strings = flavor.Split(new[] {'-'}, StringSplitOptions.RemoveEmptyEntries);
                _string = "[{0}]".format(_string = _strings.Aggregate("", (current, each) => current + "-" + each));
            }
            _string = "[{0}]".format(flavor);
            _strings = flavor.SingleItemAsEnumerable();
        }

        public static implicit operator string(FlavorString flavor) {
            return flavor.ToString();
        }

        public static implicit operator FlavorString(string flavor) {
            return new FlavorString(flavor);
        }

        public bool IsWildcardMatch(FlavorString flavor) {
            if( (flavor._string.Contains("*") ||  flavor._string.Contains("?")) && _string.IsWildcardMatch(flavor._string)) {
                return true;
            }
            return Equals(flavor);
        }

        public override string ToString() {
            return _string ;
        }
        public static bool operator ==(FlavorString a, FlavorString b) {
            if (ReferenceEquals(a, b))
                return true;

            if (ReferenceEquals(a, null) || ReferenceEquals(b, null))
                return false;

            return a.Equals(b);
        }

        public static bool operator !=(FlavorString a, FlavorString b) {
            return !(a == b);
        }
        public static bool operator ==(FlavorString a, string b) {
            return a == new FlavorString(b);
        }
        public static bool operator !=(FlavorString a, string b) {
            return !(a == new FlavorString(b));
        }
        public override bool Equals(object o) {
            if (ReferenceEquals(this, o))
                return true;
            if (ReferenceEquals(o, null))
                return false;
            if(o is FlavorString) {
                return Equals(o as FlavorString);
            }
            return Equals(o.ToString());
        }
        public bool Equals(string s) {
            return !ReferenceEquals(s, null) && Equals(new FlavorString(s));
        }

        public bool Equals(FlavorString flavor) {
            // we're gonna cheat a bit here.
            // if the length of the collections match and the hashcodes match, I'm fairly confident that the lists are a match.
            return flavor._strings.Count() == _strings.Count() && flavor.GetHashCode() == GetHashCode();
        }

        public static bool operator <(FlavorString a, FlavorString b) {
            return a.CompareTo(b) < 0;
        }
        public static bool operator >(FlavorString a, FlavorString b) {
            return a.CompareTo(b) > 0;
        }

        public override int GetHashCode() {
            if (_hashCode == null) {
                _hashCode = 0;
                foreach (var each in _strings) {
                    _hashCode = _hashCode ^ each.GetHashCode();
                }
            }
            return (int)(_hashCode);
        }

        public int CompareTo(object obj) {
            if( ReferenceEquals(obj, null)) {
                return -1;
            }
            if (ReferenceEquals(obj, this)) {
                return 0;
            }
            if (obj is FlavorString) {
                return CompareTo(obj as FlavorString);
            }
            return CompareTo(obj.ToString());
        }

        public int CompareTo(FlavorString other) {
            if (ReferenceEquals(other, null)) {
                return 1;
            }
            if (ReferenceEquals(other, this)) {
                return 0;
            }
            if( Equals(other)) {
                return 0;
            }

            var s = _strings.OrderBy(each => each).ToArray();
            var t = other._strings.OrderBy(each => each).ToArray();

            for( var i= 0; i< s.Length && i < t.Length ; i++ ) {
                var n = s[i].CompareTo(t[i]);
                if( n != 0 ) {
                    return n;
                }
            }

            return s.Length - t.Length;
        }
    }
}