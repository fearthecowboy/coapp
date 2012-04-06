//-----------------------------------------------------------------------
// <copyright company="CoApp Project">
//     Original Copyright (c) 2009 Microsoft Corporation. All rights reserved.
//     Changes Copyright (c) 2011 Eric Schultz, 2010  Garrett Serack. All rights reserved.
//     Version regex string from Wix toolkit
// </copyright>
// <license>
//     The software is licensed under the Apache 2.0 License (the "License")
//     You may not use the software except in compliance with the License. 
// </license>
//-----------------------------------------------------------------------

// -----------------------------------------------------------------------
// Original Code: 
// (c) 2009 Microsoft Corporation -- All rights reserved
// This code is licensed under the MS-PL
// http://www.opensource.org/licenses/ms-pl.html
// Courtesy of the Open Source Techology Center: http://port25.technet.com
// -----------------------------------------------------------------------

namespace CoApp.Toolkit.Extensions {
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Runtime.Remoting.Metadata.W3cXsd2001;
    using System.Security.Cryptography;
    using System.Text;
    using System.Text.RegularExpressions;
    using Ionic.Zlib;
    using Text;

    /// <summary>
    /// Extensions for strings. Whheeee
    /// </summary>
    /// <remarks></remarks>
    public static class StringExtensions {
        /// <summary>
        /// a string with just letters, numbers and underscores. 
        /// 
        /// Used as a filter somewhere.
        /// </summary>
        public const string LettersNumbersUnderscores = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz1234567890_";
        /// <summary>
        /// a string with just letters, numbers, underscores and dashes. 
        /// 
        /// Used as a filter somewhere.
        /// </summary>
        public const string LettersNumbersUnderscoresAndDashes = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz1234567890_-";
        /// <summary>
        /// a string with just letters, numbers, underscores, dashes and dots. 
        /// 
        /// Used as a filter somewhere.
        /// </summary>
        public const string LettersNumbersUnderscoresAndDashesAndDots = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz1234567890_-.";

        /// <summary>
        /// These are crazy, but valid filepath characters that cause regexs to puke and fail.
        /// </summary>
        private static readonly string[] _validFpCharsThatHurtRegexs = { @"\", "$", "^", "{", "[", "(", "|", ")", "+", "." };

        //putting regexs here so they're only compiled once.
        #pragma warning disable 169
        /// <summary>
        /// 
        /// </summary>
        private static Regex _versionRegex = new Regex( @"^\d{1,5}\.\d{1,5}\.\d{1,5}\.\d{1,5}$");
        #pragma warning restore 169

        /// <summary>
        /// What? 
        /// Note: Eric is this yours?
        /// </summary>
        private static readonly Regex _badDirIdCharsRegex = new Regex(@"\s|\.|\-|\\");

        /// <summary>
        /// a two-part version regex.
        /// </summary>
        private static readonly Regex _majorMinorRegex = new Regex(@"^\d{1,5}\.\d{1,5}$");

        //TODO this SUCKS. Thanks MS.
        /// <summary>
        /// Email regex. Needs revising
        /// </summary>
        private static readonly Regex _emailRegex = new Regex(@"^(?<name>\S+)@(?<domain>\S+)$");

        // ReSharper disable InconsistentNaming
        /// <summary>
        /// Formats the specified format string.
        /// </summary>
        /// <param name="formatString">The format string.</param>
        /// <param name="args">The args.</param>
        /// <returns></returns>
        /// <remarks></remarks>
        public static string format(this string formatString, params object[] args) {
            return String.Format(formatString, args);
        }
        // ReSharper restore InconsistentNaming

        /// <summary>
        /// Prints the specified format string.
        /// </summary>
        /// <param name="formatString">The format string.</param>
        /// <param name="args">The args.</param>
        /// <remarks></remarks>
        public static void Print(this string formatString, params object[] args) {
            Console.WriteLine(formatString, args);
        }

        /// <summary>
        /// Errors the specified format string.
        /// </summary>
        /// <param name="formatString">The format string.</param>
        /// <param name="args">The args.</param>
        /// <remarks></remarks>
        public static void Error(this string formatString, params object[] args) {
            Console.Error.WriteLine(formatString, args);
        }

        /// <summary>
        /// Matches the specified input.
        /// </summary>
        /// <param name="input">The input.</param>
        /// <param name="rxExpression">The rx expression.</param>
        /// <returns></returns>
        /// <remarks></remarks>
        public static Match Match(this string input, string rxExpression) {
            return new Regex(rxExpression).Match(input);
        }

        /// <summary>
        /// Matches a regular expression, ignoring case.
        /// </summary>
        /// <param name="input">The input.</param>
        /// <param name="rxExpression">The rx expression.</param>
        /// <returns></returns>
        /// <remarks></remarks>
        public static Match MatchIgnoreCase(this string input, string rxExpression) {
            return new Regex(rxExpression, RegexOptions.IgnoreCase).Match(input);
        }

        /// <summary>
        /// coerces a string to an int32, defaults to zero.
        /// </summary>
        /// <param name="str">The STR.</param>
        /// <returns></returns>
        /// <remarks></remarks>
        public static int ToInt32(this string str) {
            return str.ToInt32(0);
        }

        /// <summary>
        /// coerces a string to an int32, defaults to zero.
        /// </summary>
        /// <param name="str">The STR.</param>
        /// <param name="defaultValue">The default value if the string isn't a valid int.</param>
        /// <returns></returns>
        /// <remarks></remarks>
        public static int ToInt32(this string str, int defaultValue = 0) {
            int i;
            return Int32.TryParse(str, out i) ? i : defaultValue;
        }

        /// <summary>
        /// returns true when the string contains only the characters passed in.
        /// </summary>
        /// <param name="str">The STR.</param>
        /// <param name="characters">The characters.</param>
        /// <returns></returns>
        /// <remarks></remarks>
        public static bool OnlyContains(this string str, char[] characters) {
            return str.Select(ch => characters.Any(t => ch == t)).All(found => found);
        }

        /// <summary>
        /// returns true when the string contains only the characters in the string passed in .
        /// </summary>
        /// <param name="str">The STR.</param>
        /// <param name="characters">The characters.</param>
        /// <returns></returns>
        /// <remarks></remarks>
        public static bool OnlyContains(this string str, string characters) {
            return OnlyContains(str, characters.ToCharArray());
        }

        /// <summary>
        /// Gets the position of the first character that is not in the collection of characters passed in.
        /// </summary>
        /// <param name="str">The STR.</param>
        /// <param name="characters">The characters.</param>
        /// <returns></returns>
        /// <remarks></remarks>
        public static int PositionOfFirstCharacterNotIn(this string str, char[] characters) {
            var p = 0;
            while (p < str.Length) {
                if (!characters.Contains(str[p])) {
                    return p;
                }
                p++;
            }
            return p;
        }

        /// <summary>
        /// Gets the position of the first character that is not in the string passed in.
        /// </summary>
        /// <param name="str">The STR.</param>
        /// <param name="characters">The characters.</param>
        /// <returns></returns>
        /// <remarks></remarks>
        public static int PositionOfFirstCharacterNotIn(this string str, string characters) {
            return PositionOfFirstCharacterNotIn(str, characters.ToCharArray());
        }

        /// <summary>
        /// Creates a GUID from an MD5 value of the string passed in.
        /// </summary>
        /// <param name="str">The STR.</param>
        /// <returns></returns>
        /// <remarks></remarks>
        public static Guid CreateGuid(this string str) {
            Guid guid;
            if (!Guid.TryParse(str, out guid)) {
                guid = new Guid(MD5.Create().ComputeHash(Encoding.UTF8.GetBytes(str)));
            }
            return guid;
        }


        /// <summary>
        /// wildcard cache for IsWildcardMatch (so we're not rebuilding the regex every time)
        /// </summary>
        private static readonly Dictionary<string, Regex> _wildcards = new Dictionary<string, Regex>();

        /// <summary>
        /// Determines if a given string is a match for the given wildcard pattern.
        /// </summary>
        /// <param name="text">The text.</param>
        /// <param name="wildcardMask">The wildcard mask.</param>
        /// <param name="ignorePrefix">The ignore prefix.</param>
        /// <param name="escapePrefix">if set to <c>true</c> [escape prefix].</param>
        /// <returns><c>true</c> if [is wildcard match] [the specified text]; otherwise, <c>false</c>.</returns>
        /// <remarks></remarks>
        public static bool IsWildcardMatch(this string text, string wildcardMask, string ignorePrefix = null, bool escapePrefix = true) {
            //find out if the wildcard is rooted?
            if (Path.GetPathRoot(wildcardMask) == String.Empty) {
                ignorePrefix = String.IsNullOrEmpty(ignorePrefix) ? (text.Contains("\\") ?  @".*\\?" : string.Empty ): escapePrefix  ? Regex.Escape(ignorePrefix) : ignorePrefix;
            } else {
                ignorePrefix = String.Empty;
            }

            var key = wildcardMask + ignorePrefix;
            if (_wildcards.ContainsKey(key)) {
                return _wildcards[key].IsMatch(text);
            }

            if (wildcardMask.EndsWith("**")) {
                wildcardMask += @"\*";
            }
            var regexStuff = '^' + ignorePrefix;

            var regexPart2 = wildcardMask.CommentEach(_validFpCharsThatHurtRegexs);
            regexPart2 = regexPart2.Replace("?", @".");
            regexPart2 = regexPart2.Replace("**", @"?");
            regexPart2 = regexPart2.Replace("*", @"[^\\\/\<\>\|]*");
            regexPart2 = regexPart2.Replace("?", @"[^\<\>\|]*");

            regexStuff += regexPart2 + '$';

            var mask = new Regex(regexStuff, RegexOptions.IgnoreCase);
            /*
            var mask =
                new Regex(
                    '^' + ignorePrefix +
                        (wildcardMask.Replace(".", @"[.]").Replace(@"\", @"\\").Replace("?", @".").Replace("+", @"\+").Replace("**",
                            @"?") // temporarily move it so the next one doesn't clobber
                            .Replace("*", @"[^\\\/\<\>\|]*") //     \/\<\>\|
                            .Replace("?", @"[^\<\>\|]*") + '$'), RegexOptions.IgnoreCase);*/

            
            lock (_wildcards) {
                if (!_wildcards.ContainsKey(key)) {
                    _wildcards.Add(key, mask);
                }
            }
            return mask.IsMatch(text);
        }

        /// <summary>
        /// wildcard cache for IsWildcardMatch (so we're not rebuilding the regex every time)
        /// </summary>
        private static readonly Dictionary<string, Regex> _newWildcards = new Dictionary<string, Regex>();
        private static Regex EscapeFilepathCharacters = new Regex(@"([\\|\$|\^|\{|\[|\||\)|\+|\.|\]|\}|\/])");

        private static Regex WildcardToRegex( string wildcard, string noEscapePrefix = "^" ) {
            return new Regex(noEscapePrefix + EscapeFilepathCharacters.Replace(wildcard, "\\$1")
                .Replace("?", @".")
                .Replace("**", @"?")
                .Replace("*", @"[^\\\/\<\>\|]*")
                .Replace("?", @".*") + '$');
            
        }

        /// <summary>
        /// The new implementation of the wildcard matching function.
        /// 
        /// Rules: 
        ///   * means matches anything except slash or backslash 
        ///   ? means match any one character
        ///   ** means matches anything 
        /// </summary>
        /// <param name="text"></param>
        /// <param name="wildcardMask"></param>
        /// <returns></returns>
        public static bool NewIsWildcardMatch(this string text, string wildcardMask, bool isMatchingLocation = false, string currentLocation = null) {
            string key;
            
            if (!isMatchingLocation) {
                key = (currentLocation ?? "" )+ wildcardMask;
                if (!_newWildcards.ContainsKey(key)) {
                    _newWildcards.Add(key, WildcardToRegex(key));
                }
                
                return _newWildcards[key].IsMatch(text);
            }

            key = wildcardMask + (currentLocation ?? "" );
            if (!_newWildcards.ContainsKey(key)) {
                var prefix = currentLocation == null
                    ? @".*[\\|\/]"
                    : Regex.Escape((currentLocation.EndsWith("\\") || currentLocation.EndsWith("/")
                        ? currentLocation : currentLocation + (text.Contains("\\") ? "\\" : (text.Contains("/") ? "/" : ""))));
                _newWildcards.Add(key, WildcardToRegex(wildcardMask, prefix));
            }
            
            return _newWildcards[key].IsMatch(text);
        }

        /// <summary>
        /// A case insensitive Contains() method.
        /// </summary>
        /// <param name="source">The source.</param>
        /// <param name="value">The value.</param>
        /// <returns><c>true</c> if [contains ignore case] [the specified source]; otherwise, <c>false</c>.</returns>
        /// <remarks></remarks>
        public static bool ContainsIgnoreCase(this IEnumerable<string> source, string value) {
            return (from each in source where each.Equals(value, StringComparison.CurrentCultureIgnoreCase) select each).Any();
        }

        /// <summary>
        /// Runs IsWildcardMatch on a collection.
        /// </summary>
        /// <param name="source">The source.</param>
        /// <param name="value">The value.</param>
        /// <param name="ignorePrefix">The ignore prefix.</param>
        /// <param name="escapePrefix">if set to <c>true</c> [escape prefix].</param>
        /// <returns><c>true</c> if [has wildcard match] [the specified source]; otherwise, <c>false</c>.</returns>
        /// <remarks></remarks>
        public static bool HasWildcardMatch(this IEnumerable<string> source, string value, string ignorePrefix = null,
            bool escapePrefix = true) {
            return source.Any(wildcard => value.IsWildcardMatch(wildcard, wildcard.Contains(@"\\") ? ignorePrefix : null, escapePrefix));
        }

        /// <summary>
        /// Determines whether the specified input has wildcards.
        /// </summary>
        /// <param name="input">The input.</param>
        /// <returns><c>true</c> if the specified input has wildcards; otherwise, <c>false</c>.</returns>
        /// <remarks></remarks>
        public static bool HasWildcards(this string input)
        {          
            return input.Contains('*');
        }

        /// <summary>
        /// Determines whether the specified text equal to "true" (ignoring case).
        /// </summary>
        /// <param name="text">The text.</param>
        /// <returns><c>true</c> if the specified text is true; otherwise, <c>false</c>.</returns>
        /// <remarks></remarks>
        public static bool IsTrue(this string text) {
            return text != null && text.Equals("true", StringComparison.CurrentCultureIgnoreCase);
        }

        /// <summary>
        /// Determines whether the specified text is equal to "false" (ignoring case).
        /// </summary>
        /// <param name="text">The text.</param>
        /// <returns><c>true</c> if the specified text is false; otherwise, <c>false</c>.</returns>
        /// <remarks></remarks>
        public static bool IsFalse(this string text) {
            return text != null && text.Equals("false", StringComparison.CurrentCultureIgnoreCase);
        }

        /// <summary>
        /// Determines whether the specified text is a boolean (true or false).
        /// </summary>
        /// <param name="text">The text.</param>
        /// <returns><c>true</c> if the specified text is boolean; otherwise, <c>false</c>.</returns>
        /// <remarks></remarks>
        public static bool IsBoolean(this string text)
        {
            return text.IsTrue() || text.IsFalse();
        }

        /// <summary>
        /// Encodes the string as an array of UTF8 bytes.
        /// </summary>
        /// <param name="text">The text.</param>
        /// <returns></returns>
        /// <remarks></remarks>
        public static byte[] ToByteArray(this string text) {
            return Encoding.UTF8.GetBytes(text);
        }

        /// <summary>
        /// Creates a string from a collection of UTF8 bytes
        /// </summary>
        /// <param name="bytes">The bytes.</param>
        /// <returns></returns>
        /// <remarks></remarks>
        public static string ToUtf8String(this IEnumerable<byte> bytes) {
            return Encoding.UTF8.GetString(bytes.ToArray());
        }

        /// <summary>
        /// encrypts the given collection of bytes with the machine key and salt (defaults to "CoAppToolkit")
        /// </summary>
        /// <param name="binaryData">The binary data.</param>
        /// <param name="salt">The salt.</param>
        /// <returns></returns>
        /// <remarks></remarks>
        public static IEnumerable<byte> ProtectBinaryForMachine(this IEnumerable<byte> binaryData, string salt = "CoAppToolkit") {
            return ProtectedData.Protect(binaryData.ToArray(), salt.ToByteArray(), DataProtectionScope.LocalMachine);
        }

        /// <summary>
        /// encrypts the given collection of bytes with the user key and salt (defaults to "CoAppToolkit")
        /// </summary>
        /// <param name="binaryData">The binary data.</param>
        /// <param name="salt">The salt.</param>
        /// <returns></returns>
        /// <remarks></remarks>
        public static IEnumerable<byte> ProtectBinaryForUser(this IEnumerable<byte> binaryData, string salt = "CoAppToolkit") {
            return ProtectedData.Protect(binaryData.ToArray(), salt.ToByteArray(), DataProtectionScope.CurrentUser);
        }

        /// <summary>
        /// encrypts the given string with the machine key and salt (defaults to "CoAppToolkit")
        /// </summary>
        /// <param name="text">The text.</param>
        /// <param name="salt">The salt.</param>
        /// <returns></returns>
        /// <remarks></remarks>
        public static IEnumerable<byte> ProtectForMachine(this string text, string salt = "CoAppToolkit") {
            return ProtectBinaryForMachine((text ?? String.Empty).ToByteArray(), salt);
        }

        /// <summary>
        /// encrypts the given string with the machine key and salt (defaults to "CoAppToolkit")
        /// </summary>
        /// <param name="text">The text.</param>
        /// <param name="salt">The salt.</param>
        /// <returns></returns>
        /// <remarks></remarks>
        public static IEnumerable<byte> ProtectForUser(this string text, string salt = "CoAppToolkit") {
            return ProtectBinaryForUser((text ?? String.Empty).ToByteArray(), salt);
        }

        /// <summary>
        /// decrypts the given collection of bytes with the user key and salt (defaults to "CoAppToolkit")
        /// 
        /// returns an empty collection of bytes on failure
        /// </summary>
        /// <param name="binaryData">The binary data.</param>
        /// <param name="salt">The salt.</param>
        /// <returns></returns>
        /// <remarks></remarks>
        public static IEnumerable<byte> UnprotectBinaryForUser(this IEnumerable<byte> binaryData, string salt = "CoAppToolkit") {
            if (binaryData.IsNullOrEmpty()) {
                return Enumerable.Empty<byte>();
            }

            try {
                return ProtectedData.Unprotect(binaryData.ToArray(), salt.ToByteArray(), DataProtectionScope.CurrentUser);
            }
            catch {
                /* suppress */
            }
            return Enumerable.Empty<byte>();
        }

        /// <summary>
        /// decrypts the given collection of bytes with the machine key and salt (defaults to "CoAppToolkit")
        /// 
        /// returns an empty collection of bytes on failure
        /// </summary>
        /// <param name="binaryData">The binary data.</param>
        /// <param name="salt">The salt.</param>
        /// <returns></returns>
        /// <remarks></remarks>
        public static IEnumerable<byte> UnprotectBinaryForMachine(this IEnumerable<byte> binaryData, string salt = "CoAppToolkit") {
            if (binaryData.IsNullOrEmpty()) {
                return Enumerable.Empty<byte>();
            }

            try {
                return ProtectedData.Unprotect(binaryData.ToArray(), salt.ToByteArray(), DataProtectionScope.LocalMachine);
            }
            catch {
                /* suppress */
            }
            return Enumerable.Empty<byte>();
        }

        /// <summary>
        /// decrypts the given collection of bytes with the user key and salt (defaults to "CoAppToolkit")
        /// and returns a string from the UTF8 representation of the bytes.
        /// 
        /// returns an empty string on failure
        /// </summary>
        /// <param name="binaryData">The binary data.</param>
        /// <param name="salt">The salt.</param>
        /// <returns></returns>
        /// <remarks></remarks>
        public static string UnprotectForUser(this IEnumerable<byte> binaryData, string salt = "CoAppToolkit") {
            var data = binaryData.UnprotectBinaryForUser(salt);
            return data.Any() ? data.ToUtf8String() : String.Empty;
        }

        /// <summary>
        /// decrypts the given collection of bytes with the machine key and salt (defaults to "CoAppToolkit")
        /// and returns a string from the UTF8 representation of the bytes.
        /// 
        /// returns an empty string on failure
        /// </summary>
        /// <param name="binaryData">The binary data.</param>
        /// <param name="salt">The salt.</param>
        /// <returns></returns>
        /// <remarks></remarks>
        public static string UnprotectForMachine(this IEnumerable<byte> binaryData, string salt = "CoAppToolkit") {
            var data = binaryData.UnprotectBinaryForMachine(salt);
            return data.Any() ? data.ToUtf8String() : String.Empty;
        }

        /// <summary>
        /// returns a UInt64 of a standard version string. 
        /// 
        /// Returns 0 for parts that are not valid.
        /// </summary>
        /// <param name="version">The version.</param>
        /// <returns></returns>
        /// <remarks></remarks>
        public static UInt64 VersionStringToUInt64(this string version) {
            if (String.IsNullOrEmpty(version)) {
                return 0;
            }
            var vers = version.Split('.');
            var major = vers.Length > 0 ? vers[0].ToInt32(0) : 0;
            var minor = vers.Length > 1 ? vers[1].ToInt32(0) : 0;
            var build = vers.Length > 2 ? vers[2].ToInt32(0) : 0;
            var revision = vers.Length > 3 ? vers[3].ToInt32(0) : 0;

            return (((UInt64) major) << 48) + (((UInt64) minor) << 32) + (((UInt64) build) << 16) + (UInt64) revision;
        }

        /// <summary>
        /// returns a standard version string for a UInt64 version
        /// </summary>
        /// <param name="version">The version.</param>
        /// <returns></returns>
        /// <remarks></remarks>
        public static string UInt64VersiontoString(this UInt64 version) {
            return String.Format("{0}.{1}.{2}.{3}", (version >> 48) & 0xFFFF, (version >> 32) & 0xFFFF, (version >> 16) & 0xFFFF,
                (version) & 0xFFFF);
        }

        /// <summary>
        /// Calculates the MD5 hash of a string. Additionally all the letters in the hash are in uppercase.
        /// </summary>
        /// <param name="input">a string to a calculate the hash for</param>
        /// <returns>MD5 hash of the string</returns>
        /// <remarks></remarks>
        public static string MD5Hash(this string input) {
            using (var hasher = MD5.Create()) {
                return hasher.ComputeHash(Encoding.Unicode.GetBytes(input)).Aggregate(String.Empty,
                    (current, b) => current + b.ToString("x2").ToUpper());
            }
        }

        /// <summary>
        /// Creates the public key token given a public key..
        /// 
        /// Note: Does this work?
        /// </summary>
        /// <param name="publicKey">The public key.</param>
        /// <returns></returns>
        /// <remarks></remarks>
        public static string CreatePublicKeyToken(this IEnumerable<byte> publicKey) {
            var m = new SHA1Managed();
            var hashBytes = m.ComputeHash(publicKey.ToArray());
            var last8BytesReversed = hashBytes.Reverse().Take(8);

            return new SoapHexBinary(last8BytesReversed.ToArray()).ToString();
        }

        /// <summary>
        /// Creates a safe directory ID for MSI for a possibly non-safe one.
        /// </summary>
        /// <param name="input">The input.</param>
        /// <returns>Your safe directory ID</returns>
        /// <remarks></remarks>
        public static string MakeSafeDirectoryId(this string input) {
            return _badDirIdCharsRegex.Replace(input, "_");
        }

        public static string MakeSafeFileName(this string input ) {
            return new Regex(@"-+").Replace(new Regex(@"[^\d\w\[\]_\-\. ]").Replace(input, "-"), "-");
        }

        public static string MakeAttractiveFilename(this string input) {
            return CultureInfo.CurrentCulture.TextInfo.ToTitleCase(input.MakeSafeFileName().ToLower());
        }
        /// <summary>
        /// Checks if a string is a valid version string x.x.x.x
        /// TODO: this allows x to have values LARGER than the max number
        /// for part of a version string. NEED TO FIX
        /// </summary>
        /// <param name="input">a string to be checked</param>
        /// <param name="strict">should be strict?</param>
        /// <returns>true if it the string is a valid version, false otherwise</returns>
        /// <remarks></remarks>
        public static bool IsValidVersion(this string input, bool strict = true) {
            return input.VersionStringToUInt64().UInt64VersiontoString().Equals(input);
        }



        /// <summary>
        /// Extends the version.
        /// </summary>
        /// <param name="input">The input.</param>
        /// <returns></returns>
        /// <remarks></remarks>
        public static string ExtendVersion(this string input) {
            return input.VersionStringToUInt64().UInt64VersiontoString();
        }


        /// <summary>
        /// Checks if a string is a valid major.minor version string x.x
        /// TODO: this allows x to have values LARGER than the max number
        /// for part of a version string. NEED TO FIX
        /// </summary>
        /// <param name="input">a string to be checked</param>
        /// <returns>true if it the string is a valid major.minor version, false otherwise</returns>
        /// <remarks></remarks>
        public static bool IsValidMajorMinorVersion(this string input) {
            return _majorMinorRegex.IsMatch(input);
        }


        /// <summary>
        /// Gzips the specified input.
        /// </summary>
        /// <param name="input">The input.</param>
        /// <returns></returns>
        /// <remarks></remarks>
        public static byte[] Gzip(this string input) {
            var memStream = new MemoryStream();
            using (var gzStr = new GZipStream(memStream, CompressionMode.Compress, CompressionLevel.BestCompression)) {
                gzStr.Write(input.ToByteArray(), 0, input.ToByteArray().Length);
            }

            return memStream.ToArray();
        }

        /// <summary>
        /// Gzips to base64.
        /// </summary>
        /// <param name="input">The input.</param>
        /// <returns></returns>
        /// <remarks></remarks>
        public static string GzipToBase64(this string input) {
            return String.IsNullOrEmpty(input) ? input : Convert.ToBase64String(Gzip(input));
        }

        /// <summary>
        /// Gunzips from base64.
        /// </summary>
        /// <param name="input">The input.</param>
        /// <returns></returns>
        /// <remarks></remarks>
        public static string GunzipFromBase64(this string input) {
            if (String.IsNullOrEmpty(input))
                return input;
            try
            {
                return Gunzip(Convert.FromBase64String(input));
            }
            catch
            {
                return input;
            }
            
            
        }

        /// <summary>
        /// Gunzips the specified input.
        /// </summary>
        /// <param name="input">The input.</param>
        /// <returns></returns>
        /// <remarks></remarks>
        public static string Gunzip(this byte[] input) {
            var bytes = new List<byte>();
            using (var gzStr = new GZipStream(new MemoryStream(input), CompressionMode.Decompress)) {
                var bytesRead = new byte[512];
                while (true) {
                    var numRead = gzStr.Read(bytesRead, 0, 512);
                    if (numRead > 0) {
                        bytes.AddRange(bytesRead.Take(numRead));
                    }
                    else {
                        break;
                    }
                }
            }

            return bytes.ToArray().ToUtf8String();
        }


        /// <summary>
        /// Determines whether the specified email is email.
        /// </summary>
        /// <param name="email">The email.</param>
        /// <returns><c>true</c> if the specified email is email; otherwise, <c>false</c>.</returns>
        /// <remarks></remarks>
        public static bool IsEmail(this string email) {
            return _emailRegex.IsMatch(email);
        }

        /// <summary>
        /// Replaces the each.
        /// 
        /// Eric ? 
        /// </summary>
        /// <param name="input">The input.</param>
        /// <param name="oldValues">The old values.</param>
        /// <param name="newValues">The new values.</param>
        /// <returns></returns>
        /// <remarks></remarks>
        public static string ReplaceEach(this string input, IEnumerable<string> oldValues, IEnumerable<string> newValues) {
            //TODO I feel like there's a LINQ-ier way to do this.

            if (oldValues.Count() != newValues.Count())
            {
                return null;
            }
            


            //oldValues.Aggregate(input, (output, ))


            return oldValues.Zip(newValues, (first, second) => new { first, second }).Aggregate(input, (accm, x) => accm.Replace(x.first, x.second));

        }

        /// <summary>
        /// Escapes items in a given string for regex.
        /// </summary>
        /// <param name="input">The input.</param>
        /// <param name="toComment">To comment.</param>
        /// <returns></returns>
        /// <remarks></remarks>
        public static string CommentEach(this string input, IEnumerable<string> toComment)
        {
            return input.ReplaceEach(toComment, toComment.Select((s) => @"\" + s));
        }


        /// is this supposed to be deleted?
        /// <summary>
        /// Gets the key token from full key.
        /// 
        /// Does this work?
        /// </summary>
        /// <param name="fullKey">The full key.</param>
        /// <returns></returns>
        /// <remarks></remarks>
        public static byte[] GetKeyTokenFromFullKey(this byte[] fullKey)
        {
            SHA1CryptoServiceProvider csp = new SHA1CryptoServiceProvider();
            byte[] hash = csp.ComputeHash(fullKey);

            byte[] token = new byte[8];
            for (int i = 0; i < 8; i++ )
                token[i] = hash[hash.Length - (i+1)];

            return hash;
        }


        /// <summary>
        /// Creates a hex representaion of a collection of bytes
        /// </summary>
        /// <param name="bytes">The bytes.</param>
        /// <returns></returns>
        /// <remarks></remarks>
        public static string ToHexString(this IEnumerable<byte> bytes) {
            var sb = new StringBuilder();
            foreach (var b in bytes) {
                sb.Append(b.ToString("x2"));
            }
            return sb.ToString();
        }

        /// <summary>
        /// Encodes a string into HTML encoding format, encoding control characters as well.
        /// </summary>
        /// <param name="s">The s.</param>
        /// <returns></returns>
        /// <remarks></remarks>
        public static string HtmlEncode(this string s) {
            s = WebUtility.HtmlEncode(s);
            var sb = new StringBuilder(s.Length + 100);

            for(var p = 0; p < s.Length; p++)
                sb.Append(s[p] < 31 ? String.Format("&#x{0:x2};", (int) s[p]) : "" + s[p]);

            return sb.ToString();
        }

        /// <summary>
        /// decodes an HTML encoded string
        /// </summary>
        /// <param name="s">The string</param>
        /// <returns></returns>
        /// <remarks></remarks>
        public static string HtmlDecode(this string s) {
            return WebUtility.HtmlDecode(s);
        }

        /// <summary>
        /// encodes the string as a url encoded string
        /// </summary>
        /// <param name="s">The s.</param>
        /// <returns></returns>
        /// <remarks></remarks>
        public static string UrlEncode(this string s) {
            return HttpUtility.UrlEncode(s);
        }

        /// <summary>
        /// decodes the URL encoded string
        /// </summary>
        /// <param name="s">The s.</param>
        /// <returns></returns>
        /// <remarks></remarks>
        public static string UrlDecode(this string s) {
            return HttpUtility.UrlDecode(s);
        }

        public static string CamelCaseToDashed(this string camelCaseText, char separator = '-') {
            return new string(camelCaseToDashed(camelCaseText,separator).ToArray());
        }

        private static IEnumerable<char> camelCaseToDashed(this string camelCaseText, char separator='-' ) {
            foreach( var ch in camelCaseText ) {
                if( Char.IsUpper(ch) ) {
                    yield return separator;
                }
                yield return Char.ToLower(ch);
            }
        }

        public delegate string GetMacroValueDelegate(string valueName);



        private static readonly Regex[] Macros = new[] {
            new Regex(@"(\$\{(.*?)\})"), new Regex(@"(\$\%7B(.*?)\%7D)")
        };
        

        private static string ProcessMacroInternal( this string value, GetMacroValueDelegate getMacroValue , object eachItem = null) {
            bool keepGoing;
            do {
                keepGoing = false;
                foreach (var macro in Macros) {
                    var matches = macro.Matches(value);
                    foreach (var m in matches) {
                        var match = m as Match;
                        var innerMacro = match.Groups[2].Value;
                        var outerMacro = match.Groups[1].Value;

                        string replacement = null;

                        // get the first responder.
                        foreach (GetMacroValueDelegate del in getMacroValue.GetInvocationList()) {
                            replacement = del(innerMacro);
                            if (replacement != null)
                                break;
                        }

                        if (eachItem != null) {
                            // try resolving it as an ${each.property} style.
                            // the element at the front is the 'this' value
                            // just trim off whatever is at the front up to and including the first dot.
                            try {
                                if (innerMacro.Contains(".")) {
                                    innerMacro = innerMacro.Substring(innerMacro.IndexOf('.') + 1).Trim();
                                    var r = eachItem.SimpleEval(innerMacro).ToString();
                                    value = value.Replace(outerMacro, r);
                                    keepGoing = true;
                                }
                            }
                            catch {
                                // meh. screw em'
                            }
                        }

                        if (replacement != null) {
                            value = value.Replace(outerMacro, replacement);
                            keepGoing = true;
                            break;
                        }
                    }
                }
            } while (keepGoing);
            return value;
        }

        public static string FormatWithMacros(this string value, GetMacroValueDelegate getMacroValue, object eachItem = null, GetMacroValueDelegate preprocessValue = null, GetMacroValueDelegate postprocessValue = null) {
            if (preprocessValue != null) {
                foreach (StringExtensions.GetMacroValueDelegate preprocess in preprocessValue.GetInvocationList()) {
                    value = preprocess(value);
                }
            }
            
            if (getMacroValue != null) {
                value = value.ProcessMacroInternal(getMacroValue, eachItem); // no macro handler?, just return 
            }

            if (postprocessValue != null) {
                foreach (StringExtensions.GetMacroValueDelegate postprocess in postprocessValue.GetInvocationList()) {
                    value = postprocess(value);
                }
            }
           
            return value;
        }

        public static Uri ToUri(this string stringUri) {
            try {
                return new Uri(stringUri);
            } catch {
                
            }
            return null;
        }

        public static string IfNullOrEmpty(this string text, string defaultText) {
            return string.IsNullOrEmpty(text) ? defaultText : text;
        }

    }
}
