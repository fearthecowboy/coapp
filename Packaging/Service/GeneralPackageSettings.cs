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

namespace CoApp.Packaging.Service {
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Common;
    using Toolkit.Extensions;

    internal class GeneralPackageSettings {
        internal static GeneralPackageSettings Instance = new GeneralPackageSettings();
        private GeneralPackageSettings() {
        }
        internal string this[int priority, CanonicalName canonicalName, string key] {
            get {
                var retval = string.IsNullOrEmpty(key) ? PackageManagerSettings.PerPackageSettings[priority.ToString()][canonicalName].StringValue : PackageManagerSettings.PerPackageSettings[priority.ToString()][canonicalName, key].StringValue;
                return string.IsNullOrEmpty(retval) ? null : retval;
            } 
            set {
                if (string.IsNullOrEmpty(key)) {
                    PackageManagerSettings.PerPackageSettings[priority.ToString()][canonicalName].StringValue = value.IsTrue() ? value : null;
                }
                else {
                    PackageManagerSettings.PerPackageSettings[priority.ToString()][canonicalName, key].StringValue = string.IsNullOrEmpty(value) ? null : value;
                }
            }
        }

        internal int WhoWins(CanonicalName negative, CanonicalName positive) {
            if( positive == negative ) {
                return 0;
            }

            foreach( var p in Priorities) {
                int pos = 0;
                int neg = 0;
                foreach (var key in GetCanonicalNames(p).Where(each=> this[p, each, null].IsTrue())) {
                    pos = Math.Max(positive.MatchQuality(key), pos);
                    neg = Math.Max(negative.MatchQuality(key), neg);
                }
                if( pos == neg) {
                    continue;
                }
                return pos - neg;
            }

            // didn't find a rule that can distinguish.
            // if the packages differ by version only, use that to decide
            if( positive.DiffersOnlyByVersion(negative) ) {
                return (((long)(ulong)positive.Version - (long)(ulong)negative.Version) > 0 ? 1 : -1);
            }

            // nothing to decide with!
            return 0;
        }

        internal string GetValue(CanonicalName canonicalName, string key) {
            string result = null;
            foreach (var p in Priorities) {
                int lastMatch = 0;
                foreach (var name in GetCanonicalNameStrings(p).Where(name => GetKeys(p, name).ContainsIgnoreCase(key))) {
                    var m = canonicalName.MatchQuality(name);
                    if (m > lastMatch) {
                        result = this[p, canonicalName, key];
                        lastMatch = m;
                    }
                }
            }
            return result;
        }

        internal void GetSettingsData(IPackageManagerResponse response) {
            foreach( var p in Priorities) {
                foreach( var n in GetCanonicalNameStrings(p)) {
                    foreach( var k in GetKeys(p, n) ) {
                        var v = this[p, n, k];
                        if( v != null ) {
                            response.GeneralPackageSetting(p, n, k, v);
                        }
                    }
                }
            }
        }

        private IEnumerable<CanonicalName> GetCanonicalNames(int priority) {
            return GetCanonicalNameStrings(priority).Select(each => (CanonicalName)each);
        }

        private IEnumerable<string> GetCanonicalNameStrings(int priority) {
            return PackageManagerSettings.PerPackageSettings[priority.ToString()].Subkeys;
        }

        private IEnumerable<string> GetKeys( int priority, string canonicalName ) {
            return PackageManagerSettings.PerPackageSettings[priority.ToString()][canonicalName].Subkeys;
        }

        internal IEnumerable<int> Priorities {
            get {
                int v = 0;
                return (from key in PackageManagerSettings.PerPackageSettings.Subkeys where int.TryParse(key, out v) select v).OrderByDescending(each => each).ToArray();
            }
        }   
     
    }
}