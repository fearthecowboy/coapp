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
    using Text;

    public enum ResponseType {
        String,
        Binary,
        File
    };

    public enum RequestType {
        GET,
        POST,
        HEAD
    };

    public static class WebExtensions {
        private static CredentialCache credentialCache;
        public static List<Cookie> Cookies = new List<Cookie>();
        public static bool KeepCookiesClean;

        public static int BufferChunkSize = 1024*8; // 8k?

        public static void AddBasicAuthCredentials(this WebRequest obj, string url, string username, string password) {
            if (credentialCache == null) {
                credentialCache = new CredentialCache();
            }

            var uri = new Uri(url);
            credentialCache.Add(new Uri("{0}://{1}:{2}".format(uri.Scheme, uri.Host, uri.Port)), "Basic", new NetworkCredential(username, password));
        }

        public static void AppendFormData(this StringBuilder stringBuilder, string name, string value) {
            if (stringBuilder.Length != 0) {
                stringBuilder.Append("&");
            }
            stringBuilder.Append(name);
            stringBuilder.Append("=");
            stringBuilder.Append(value.UrlEncode());
        }

        public static StringBuilder GetFormData(this Dictionary<string, string> data) {
            var result = new StringBuilder();
            foreach (var key in data.Keys) {
                result.AppendFormData(key, data[key]);
            }
            return result;
        }

        public static object Service(this Uri url, RequestType requestType, ResponseType responseType, out int resultCode) {
            return Service(url, requestType, responseType, out resultCode, null, null);
        }

        public static object Service(this Uri url, RequestType requestType, ResponseType responseType, out int resultCode, Dictionary<string, string> formData) {
            return Service(url, requestType, responseType, out resultCode, null, formData);
        }

        public static object Service(this Uri url, RequestType requestType, ResponseType responseType, out int resultCode, string outputFilename) {
            return Service(url, requestType, responseType, out resultCode, outputFilename, null);
        }

        public static object Service(this Uri url, RequestType requestType, ResponseType responseType, out int resultCode, string outputFilename, Dictionary<string, string> formData) {
            object result = null;
            resultCode = -1;

            var webRequest = (HttpWebRequest)WebRequest.Create(url);
            webRequest.CookieContainer = Cookies.GetCookieContainer();

            switch (requestType) {
                case RequestType.POST:
                    webRequest.Method = "POST";
                    webRequest.ContentType = "application/x-www-form-urlencoded";

                    var encodedFormData = Encoding.UTF8.GetBytes(GetFormData(formData).ToString());
                    using (var requestStream = webRequest.GetRequestStream()) {
                        requestStream.Write(encodedFormData, 0, encodedFormData.Length);
                    }
                    break;
                case RequestType.GET:
                    webRequest.Method = "GET";
                    if (formData != null) {
                        var ub = new UriBuilder(url) {
                            Query = GetFormData(formData).ToString()
                        };
                        url = ub.Uri;
                    }
                    break;
            }

            try {
                if (credentialCache != null) {
                    webRequest.Credentials = credentialCache;
                    webRequest.PreAuthenticate = true;
                }
                var webResponse = webRequest.GetResponse();

                if (!KeepCookiesClean) {
                    Cookies.AddCookies(webRequest.CookieContainer.GetCookies(webResponse.ResponseUri));
                }

                switch (responseType) {
                    case ResponseType.String:
                        result = GetStringResponse(webResponse);
                        resultCode = 200;
                        break;
                    case ResponseType.Binary:
                        result = GetBinaryResponse(webResponse);
                        resultCode = 200;
                        break;
                    case ResponseType.File:
                        result = GetBinaryFileResponse(webResponse, outputFilename);
                        resultCode = 200;
                        break;
                }
            } catch {
                resultCode = 0;
            }
            return result;
        }

        private static string GetStringResponse(WebResponse response) {
            string result;
            using (var sr = new StreamReader(response.GetResponseStream())) {
                result = sr.ReadToEnd();
                sr.Close();
            }
            return result;
        }

        private static byte[] GetBinaryResponse(WebResponse response) {
            int bytesReceived;
            var byteBuffer = new byte[BufferChunkSize];

            using (var buffer = new MemoryStream()) {
                using (var stream = response.GetResponseStream()) {
                    do {
                        bytesReceived = stream.Read(byteBuffer, 0, BufferChunkSize);
                        if (bytesReceived > 0) {
                            buffer.Write(byteBuffer, 0, bytesReceived);
                        }
                    } while (bytesReceived > 0);
                }
                return buffer.ToArray();
            }
        }

        private static string GetBinaryFileResponse(WebResponse response, string outputFilename) {
            int bytesReceived;
            var byteBuffer = new byte[BufferChunkSize];

            using (var buffer = File.Create(outputFilename)) {
                using (var stream = response.GetResponseStream()) {
                    do {
                        bytesReceived = stream.Read(byteBuffer, 0, BufferChunkSize);
                        if (bytesReceived > 0) {
                            buffer.Write(byteBuffer, 0, bytesReceived);
                        }
                    } while (bytesReceived > 0);
                }
                return outputFilename;
            }
        }

        public static string Get(this Uri url) {
            int resultCode;
            return Service(url, RequestType.GET, ResponseType.String, out resultCode, null, null) as string;
        }

        public static string Get(this string url, params object[] args) {
            int resultCode;
            var result = Service(new Uri(string.Format(url, args)), RequestType.GET, ResponseType.String, out resultCode);
            return result as string;
        }

        public static byte[] GetBinary(this string url, params object[] args) {
            int resultCode;
            var result = Service(new Uri(string.Format(url, args)), RequestType.GET, ResponseType.Binary, out resultCode);
            return result as byte[];
        }

        public static string GetBinaryFile(this string url, string outputFilename, params object[] args) {
            int resultCode;
            var result = Service(new Uri(string.Format(url, args)), RequestType.GET, ResponseType.Binary, out resultCode, outputFilename);
            return result as string;
        }

        public static string Post(this string url, Dictionary<string, string> args) {
            int resultCode;
            var result = Service(new Uri(url), RequestType.POST, ResponseType.String, out resultCode, args);
            return result as string;
        }

        public static byte[] PostBinary(this string url, Dictionary<string, string> args) {
            int resultCode;
            var result = Service(new Uri(url), RequestType.POST, ResponseType.Binary, out resultCode);
            return result as byte[];
        }

        public static string PostBinaryFile(this string url, string outputFilename, Dictionary<string, string> args) {
            int resultCode;
            var result = Service(new Uri(url), RequestType.POST, ResponseType.Binary, out resultCode, outputFilename);
            return result as string;
        }

        public static Uri CanonicalizeUri(this string uri) {
            var finalUri = new Uri(uri, UriKind.RelativeOrAbsolute);
            if (!finalUri.IsAbsoluteUri) {
                finalUri = new Uri(new Uri("http://localhost"), finalUri);
            }

            return finalUri;
        }

        public static Uri MakeAbsolute(this Uri baseUri, string relativeUrl) {
            // first see if it's a URL or a fragment
            var uri = new Uri(relativeUrl, UriKind.RelativeOrAbsolute);
            if (!uri.IsAbsoluteUri) {
                uri = new Uri(baseUri, relativeUrl);
            }
            return uri;
        }

        public static string ContentDispositionFilename(this HttpWebResponse httpWebResponse) {
            try {
                var disposition = httpWebResponse.Headers["Content-Disposition"];
                if (!string.IsNullOrEmpty(disposition)) {
                    var position = disposition.IndexOf("filename=");

                    if (position > -1) {
                        var result = HttpUtility.UrlDecode(disposition.Substring(position + 1).Trim());
                        if (!string.IsNullOrEmpty(result)) {
                            return result;
                        }
                    }
                }
            } catch {
            }
            return null;
        }

        public static bool IsHttpScheme(this Uri uri, bool acceptHttps = true) {
            return (uri.Scheme == Uri.UriSchemeHttp || (acceptHttps && uri.Scheme == Uri.UriSchemeHttps));
        }
    }
}