//-----------------------------------------------------------------------
// <copyright company="CoApp Project">
//     Copyright (c) 2011 Garrett Serack. All rights reserved.
// </copyright>
// <license>
//     The software is licensed under the Apache 2.0 License (the "License")
//     You may not use the software except in compliance with the License. 
// </license>
//-----------------------------------------------------------------------

namespace CoApp.Toolkit.Engine {
    using System.Text.RegularExpressions;
    using Extensions;

    internal static class MatchExtensions {
        internal static string Value(this Match match, string group, string _default = null) {
            return (match.Groups[group].Success ? match.Groups[group].Captures[0].Value : _default??string.Empty).Trim('-',' ');
        }
    }

    public class PackageName {
        private static readonly char[] _slashes = new [] {'\\', '/' };
        private static readonly Regex _canonicalName = new Regex(@"^(?<name>.+)(?<v1>-\d{1,5})(?<v2>\.\d{1,5})(?<v3>\.\d{1,5})(?<v4>\.\d{1,5})(?<arch>-any|-x86|-x64|-arm)(?<pkt>-[0-9a-f]{16})$", RegexOptions.IgnoreCase);
        private static readonly Regex _partialMatchFull =
             new Regex( @"^(?<name>.*?)?(?<v1>-\d{1,5}|-\*)?(?<v2>\.\d{1,5}|\.\*)?(?<v3>\.\d{1,5}|\.\*)?(?<v4>\.\d{1,5}|\.\*)?(?<arch>-{1,2}any|-{1,2}x86|-{1,2}x64|-{1,2}arm|-{1,2}all|-\*)?(?<pkt>-{1,3}[0-9a-f]{16})?$", RegexOptions.IgnoreCase);

        public string CanonicalName { get; private set; }
        public string Name { get; private set; }
        public string Version { get; private set; }
        public string Version1 { get; private set; }
        public string Version2 { get; private set; }
        public string Version3 { get; private set; }
        public string Version4 { get; private set; }
        public string Arch { get; private set; }
        public string PublicKeyToken { get; private set; }
      
        public bool IsPartialMatch { get { return !IsFullMatch && !Version.IsNullOrEmpty(); }}
        public bool IsFullMatch { get { return (!string.IsNullOrEmpty(CanonicalName)); } }

        private void SetFieldsFromMatch( Match match ) {
            Name = match.Value("name");

            Version1 = match.Value("v1","*");
            Version2 = match.Value("v2",".*");
            Version3 = match.Value("v3",".*");
            Version4 = match.Value("v4",".*");
            Arch = match.Value("arch");
            PublicKeyToken= match.Value("pkt");

            Version = Version1 + Version2 + Version3 + Version4;
        }

        private PackageName(string potentialPartialPackageName) {
            if (potentialPartialPackageName.IndexOfAny(_slashes) > -1) {
                return;
            }
                
            var match = _canonicalName.Match(potentialPartialPackageName.ToLower());
            if (match.Success) {
                // perfect canonical match for a name
                CanonicalName = potentialPartialPackageName;
                SetFieldsFromMatch(match);
                return;
            }

            match = _partialMatchFull.Match(potentialPartialPackageName);
            if( match.Success ) {
                SetFieldsFromMatch(match);
                return;
            }

            Name = potentialPartialPackageName;
            return;
        }

        public static PackageName Parse(string potentialPartialPackageName) {
            return new PackageName(potentialPartialPackageName);
        }
    }
}