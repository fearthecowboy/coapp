//-----------------------------------------------------------------------
// <copyright company="CoApp Project">
//     Changes Copyright (c) 2011 Garrett Serack . All rights reserved.
//     TaskScheduler Original Code from http://taskscheduler.codeplex.com/
// </copyright>
// <license>
//     The software is licensed under the Apache 2.0 License (the "License")
//     You may not use the software except in compliance with the License. 
// </license>
//-----------------------------------------------------------------------

// -----------------------------------------------------------------------
// Original Code: 
// * Copyright (c) 2003-2011 David Hall
// Permission is hereby granted, free of charge, to any person obtaining a
// copy of this software and associated documentation files (the "Software"), 
// to deal in the Software without restriction, including without limitation
// the rights to use, copy, modify, merge, publish, distribute, sublicense, 
// and/or sell copies of the Software, and to permit persons to whom the 
// Software is furnished to do so, subject to the following conditions:

// The above copyright notice and this permission notice shall be included 
// in all copies or substantial portions of the Software.

// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS
// OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL 
// THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR 
// OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, 
// ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR 
// OTHER DEALINGS IN THE SOFTWARE.

namespace CoApp.Toolkit.TaskService {
    using System.Text.RegularExpressions;

    /// <summary>
    ///   Represents a wildcard running on the <see cref="System.Text.RegularExpressions" /> engine.
    /// </summary>
    public class Wildcard : Regex {
        /// <summary>
        ///   Initializes a wildcard with the given search pattern and options.
        /// </summary>
        /// <param name="pattern"> The wildcard pattern to match. </param>
        /// <param name="options"> A combination of one or more <see cref="System.Text.RegularExpressions.RegexOptions" /> . </param>
        public Wildcard(string pattern,
            RegexOptions options = RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.IgnorePatternWhitespace)
            : base(WildcardToRegex(pattern), options) {
        }

        /// <summary>
        ///   Converts a wildcard to a regex.
        /// </summary>
        /// <param name="pattern"> The wildcard pattern to convert. </param>
        /// <returns> A regex equivalent of the given wildcard. </returns>
        public static string WildcardToRegex(string pattern) {
            var s = "^" + Escape(pattern) + "$";
            s = Replace(s, @"(?<!\\)\\\*", @".*"); // Negative Lookbehind
            s = Replace(s, @"\\\\\\\*", @"\*");
            s = Replace(s, @"(?<!\\)\\\?", @"."); // Negative Lookbehind
            s = Replace(s, @"\\\\\\\?", @"\?");
            return Replace(s, @"\\\\\\\\", @"\\");
        }
    }
}