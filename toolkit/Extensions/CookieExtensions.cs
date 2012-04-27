//-----------------------------------------------------------------------
// <copyright company="CoApp Project">
//     Original Copyright (c) 2009 Microsoft Corporation. All rights reserved.
//     Changes Copyright (c) 2010  Garrett Serack. All rights reserved.
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
    using System.IO;
    using System.Net;
    using System.Text;

    /*  NETSCAPE COOKIE FORMAT:
 * ------------------------
    domain  - The domain that created AND that can read the variable.
    flag    - A TRUE/FALSE value indicating if all machines within a given domain can access the variable. This value is set automatically by the browser, depending on the value you set for domain.
    path    - The path within the domain that the variable is valid for.
    secure  - A TRUE/FALSE value indicating if a secure connection with the domain is needed to access the variable.
    expiration - The UNIX time that the variable will expire on. UNIX time is defined as the number of seconds since Jan 1, 1970 00:00:00 GMT.
    name    - The name of the variable.
    value   - The value of the variable.
 */

    /* Set-Cookie cookie format:
     * -------------------------
     * Set-Cookie: foo=bar; path=/; expires Mon, 09-Dec-2002 13:46:00 GMT
         name of the cookie,                "foo="
         value of the cookie,               "bar"
         expiration date of the cookie,     "expires=" ...
         path the cookie is valid for,      "path=" ...
         domain the cookie is valid for,    "domain=" ... (must be .myserver.com or .www.myserver.com)
         need for a secure connection to exist to use the cookie.

        Two of these are mandatory (its name and its value). The other four can be set manually or automatically.
        Each parameter is separated by a semicolon when set explicitly.
     *

     IE Cookie File Format:
     ---------------------
    Look at various parameters are used in this.
    [0x01] The Variable Name
    [0x02] The Value for the Variable
    [0x03] The Website of the Cookie’s Owner
    [0x04] Optional Flags
    [0x05] The Most Significant Integer for Expired Time, in FILETIME Format
    [0x06] The Least Significant Integer for Expired Time, in FILETIME Format
    [0x07] The Most Significant Integer for Creation Time, in FILETIME Format
    [0x08] The Least Significant Integer for Creation Time, in FILETIME Format
    [0x09] The Cookie Record Delimiter (a * character)
     */

    public static class CookieExtensions {
        public static void AppendEncodedCookie(this StringBuilder stringBuilder, string name, string value) {
            if (stringBuilder.Length != 0) {
                stringBuilder.Append("&");
            }
            stringBuilder.Append(name);
            stringBuilder.Append("=");
            stringBuilder.Append(value.UrlEncode());
        }

        public static string GetEncodedCookies(this List<Cookie> cookieList) {
            var result = new StringBuilder();
            foreach (var cookie in cookieList) {
                result.AppendEncodedCookie(cookie.Name, cookie.Value);
            }
            return result.ToString();
        }

        public static void LoadCookiesFromNSFile(this List<Cookie> cookieList, string filename) {
            var cookieLines = File.ReadAllLines(filename);
            foreach (var c in cookieLines) {
                if (c.StartsWith(";") || c.StartsWith("#")) {
                    continue;
                }
                try {
                    var bits = c.Split('\t');
                    var cookie = new Cookie(bits[5], bits[6]) {
                        Domain = bits[0],
                        Path = bits[2],
                        Secure = bits[3].IsTrue(),
                        Expires = (new DateTime(1970, 1, 1, 0, 0, 0)).AddSeconds(bits[4].ToInt32())
                    };
                    cookieList.AddCookie(cookie);
                }
                    // ReSharper disable EmptyGeneralCatchClause
                catch (Exception) {
                }
                // ReSharper restore EmptyGeneralCatchClause
            }
        }

        public static void SaveCookiesToNSFile(this List<Cookie> cookieList, string filename) {
            var sb = new StringBuilder();
            sb.AppendLine("# Cookie File generated by CookieJar");
            sb.AppendLine("#------------------------------------");

            foreach (var c in cookieList) {
                var unixTime = (int)(c.Expires - new DateTime(1970, 1, 1)).TotalSeconds;
                if (unixTime < 0) {
                    unixTime = 0;
                }
                sb.AppendLine("{0}\t{1}\t{2}\t{3}\t{4}\t{5}\t{6}".format(c.Domain, "TRUE", /* c.Path */ "/", ("" + c.Secure).ToUpper(), unixTime, c.Name, c.Value));
            }

            File.WriteAllText(filename, sb.ToString());
        }

        public static void ExpireCookies(this List<Cookie> cookieList) {
            for (var i = 0; i < cookieList.Count; i++) {
                if (cookieList[i].Expires.CompareTo(DateTime.Now) < 0) {
                    cookieList.RemoveAt(i);
                    i--;
                }
            }
        }

        public static void LoadCookiesFromIEFile(this List<Cookie> cookieList, string filename) {
        }

        public static void SaveCookiesToIEFilestring(this List<Cookie> cookieList, string filename) {
        }

        private static void GetKeyValue(string txt, char seperator, out string key, out string value) {
            var pos = txt.IndexOf(seperator);
            key = txt.Substring(0, pos).Trim();
            value = txt.Substring(pos + 1).Trim();
        }

        public static void AddCookie(this List<Cookie> cookieList, string setCookieFormat) {
            var bits = setCookieFormat.Split(';');
            string key, value;
            GetKeyValue(bits[0], '=', out key, out value);
            var c = new Cookie(key, value);
            for (var i = 1; i < bits.Length; i++) {
                GetKeyValue(bits[1], '=', out key, out value);
                switch (key) {
                    case "expires":
                        c.Expires = DateTime.Parse(value);
                        break;
                    case "path":
                        c.Path = value;
                        break;
                    case "domain":
                        c.Domain = value;
                        break;
                    case "secure":
                        c.Secure = value == "true";
                        break;
                }
            }
            cookieList.AddCookie(c);
        }

        public static void AddCookie(this List<Cookie> cookieList, Cookie cookie) {
            for (var i = 0; i < cookieList.Count; i++) {
                var current = cookieList[i];
                if (current.Name == cookie.Name && current.Domain == cookie.Domain && current.Path == cookie.Path) {
                    cookieList[i] = cookie;
                    return;
                }
            }
            cookieList.Add(cookie);
        }

        public static void AddCookies(this List<Cookie> cookieList, CookieCollection cookies) {
            foreach (Cookie c in cookies) {
                cookieList.AddCookie(c.Clone());
            }
        }

        public static CookieContainer GetCookieContainer(this List<Cookie> cookieList) {
            var cc = new CookieContainer();
            foreach (var c in cookieList) {
                cc.Add(c.Clone());
            }
            return cc;
        }

        public static List<Cookie> Clone(this List<Cookie> cookieList) {
            var cc = new List<Cookie>();
            foreach (var c in cookieList) {
                cc.Add(c.Clone());
            }
            return cc;
        }

        public static Cookie Clone(this Cookie cookie) {
            return new Cookie {
                Name = cookie.Name,
                Value = cookie.Value,
                Path = cookie.Path,
                Domain = cookie.Domain,
                Comment = cookie.Comment,
                CommentUri = cookie.CommentUri,
                Discard = cookie.Discard,
                Expired = cookie.Expired,
                Expires = cookie.Expires,
                HttpOnly = cookie.HttpOnly,
                Port = cookie.Port,
                Secure = cookie.Secure,
                Version = cookie.Version
            };
        }
    }
}