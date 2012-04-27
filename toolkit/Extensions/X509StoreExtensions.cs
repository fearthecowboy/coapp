//-----------------------------------------------------------------------
// <copyright company="CoApp Project">
//     Copyright (c) 2011 Eric Schultz. All rights reserved.
// </copyright>
// <license>
//     The software is licensed under the Apache 2.0 License (the "License")
//     You may not use the software except in compliance with the License. 
// </license>
//-----------------------------------------------------------------------

namespace CoApp.Toolkit.Extensions {
    using System.Linq;
    using System.Security.Cryptography;
    using System.Security.Cryptography.X509Certificates;

    public static class X509StoreExtensions {
        public const string CODE_SIGNING_OID = "1.3.6.1.5.5.7.3.3";

        public static X509Certificate2 FindCodeSigningCert(this X509Store pfx) {
            return (from c in pfx.Certificates.Cast<X509Certificate2>()
                where c.HasPrivateKey
                from ext in c.Extensions.Cast<X509Extension>().OfType<X509EnhancedKeyUsageExtension>()
                from oid in ext.EnhancedKeyUsages.Cast<Oid>()
                where oid.Value == CODE_SIGNING_OID
                select c).FirstOrDefault();
        }
    }
}