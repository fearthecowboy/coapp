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

namespace CoApp.Toolkit.Extensions {
    using System.Collections.Generic;
    using System.Linq;
    using System.Security.Cryptography;
    using System.Security.Cryptography.X509Certificates;
    using Collections;
    using Crypto;

    public static class X509Certificate2Extensions {
        public static IDictionary<string, string> GetSubjectNameParts(this X509Certificate2 cert) {
            var result = new XDictionary<string, string>();

            foreach (var bits in cert.SubjectName.Name.SplitToList(',').Select(each => each.Split('='))) {
                var newKey = bits[0].Trim(' ');
                result.Add(result.ContainsKey(newKey) ? newKey + result.Keys.Where(key => key.StartsWith(newKey)).Count() : newKey, bits[1]);
            }

            return result;
        }

        public static byte[] GetPublicKey(this X509Certificate2 cert) {
            var sn = new StrongNameCertificate(cert.PublicKey.Key as RSA);
            return sn.PublicKey;
        }

        public static byte[] GetPublicKeyToken(this X509Certificate2 cert) {
            var sn = new StrongNameCertificate(cert.PublicKey.Key as RSA);
            return sn.PublicKeyToken;
        }

        public static string GetPktAsString(this X509Certificate2 cert) {
            return cert.GetPublicKeyToken().ToHexString();
        }

        /// <summary>
        ///   Checks that two certificate are equal. Verifies that the public keys and expiration dates for <paramref name="first" /> and <paramref
        ///    name="second" /> are the same.
        /// </summary>
        /// <param name="first"> The first certificate to compare </param>
        /// <param name="second"> The second certificate to compare </param>
        /// <returns> True if the certificates are equal, otherwise false </returns>
        public static bool IsEqualTo(this X509Certificate2 first, X509Certificate2 second) {
            return first.GetPublicKey() == second.GetPublicKey() &&
                first.NotAfter == second.NotAfter;
        }

        public static bool FinishStrongNaming(this X509Certificate2 cert, string fileToDelaySign) {
            var sn = new StrongNameCertificate(cert.PrivateKey as RSA);
            return sn.Sign(fileToDelaySign);
        }
    }
}