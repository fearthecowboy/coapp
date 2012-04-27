//-----------------------------------------------------------------------
// <copyright company="CoApp Project">
//     Changes Copyright (c) 2011 Garrett Serack. All rights reserved.
// </copyright>
// Original Source: http://www.pinvoke.net/default.aspx/wintrust.winverifytrust
// <license>
//     The software is licensed under the Apache 2.0 License (the "License")
//     You may not use the software except in compliance with the License. 
// </license>
//-----------------------------------------------------------------------

namespace CoApp.Toolkit.Crypto {
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Runtime.InteropServices;
    using System.Security.Cryptography.Pkcs;
    using System.Security.Cryptography.X509Certificates;
    using Win32;
    using FILETIME = System.Runtime.InteropServices.ComTypes.FILETIME;

    public enum WinTrustDataUIChoice : uint {
        All = 1,
        None = 2,
        NoBad = 3,
        NoGood = 4
    }

    public enum WinTrustDataRevocationChecks : uint {
        None = 0x00000000,
        WholeChain = 0x00000001
    }

    public enum WinTrustDataChoice : uint {
        File = 1,
        Catalog = 2,
        Blob = 3,
        Signer = 4,
        Certificate = 5
    }

    public enum WinTrustDataStateAction : uint {
        Ignore = 0x00000000,
        Verify = 0x00000001,
        Close = 0x00000002,
        AutoCache = 0x00000003,
        AutoCacheFlush = 0x00000004
    }

    [Flags]
    public enum WinTrustDataProvFlags : uint {
        UseIe4TrustFlag = 0x00000001,
        NoIe4ChainFlag = 0x00000002,
        NoPolicyUsageFlag = 0x00000004,
        RevocationCheckNone = 0x00000010,
        RevocationCheckEndCert = 0x00000020,
        RevocationCheckChain = 0x00000040,
        RevocationCheckChainExcludeRoot = 0x00000080,
        SaferFlag = 0x00000100,
        HashOnlyFlag = 0x00000200,
        UseDefaultOsverCheck = 0x00000400,
        LifetimeSigningFlag = 0x00000800,
        CacheOnlyUrlRetrieval = 0x00001000 // affects CRL retrieval and AIA retrieval
    }

    public enum WinTrustDataUIContext : uint {
        Execute = 0,
        Install = 1
    }

    public enum WinVerifyTrustResult : uint {
        Success = 0,
        ProviderUnknown = 0x800b0001, // The trust provider is not recognized on this system
        ActionUnknown = 0x800b0002, // The trust provider does not support the specified action
        SubjectFormUnknown = 0x800b0003, // The trust provider does not support the form specified for the subject
        SubjectNotTrusted = 0x800b0004, // The subject failed the specified verification action
        UntrustedRootCert = 0x800B0109 //A certificate chain processed, but terminated in a root certificate which is not trusted by the trust provider. 
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal class WinTrustFileInfo {
        private UInt32 StructSize = (UInt32)Marshal.SizeOf(typeof (WinTrustFileInfo));
        private IntPtr FilePath; // required, file name to be verified
        private IntPtr hFile = IntPtr.Zero; // optional, open handle to FilePath
        private IntPtr pgKnownSubject = IntPtr.Zero; // optional, subject type if it is known

        public WinTrustFileInfo(String _filePath) {
            FilePath = Marshal.StringToCoTaskMemAuto(_filePath);
        }

        ~WinTrustFileInfo() {
            Marshal.FreeCoTaskMem(FilePath);
        }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public class WinTrustData {
        private UInt32 StructSize = (UInt32)Marshal.SizeOf(typeof (WinTrustData));
        private IntPtr PolicyCallbackData = IntPtr.Zero;
        private IntPtr SIPClientData = IntPtr.Zero;
        // required: UI choice
        private WinTrustDataUIChoice UIChoice = WinTrustDataUIChoice.None;
        // required: certificate revocation check options
        private WinTrustDataRevocationChecks RevocationChecks = WinTrustDataRevocationChecks.None;
        // required: which structure is being passed in?
        private WinTrustDataChoice UnionChoice = WinTrustDataChoice.File;
        // individual file
        private IntPtr FileInfoPtr;
        private WinTrustDataStateAction StateAction = WinTrustDataStateAction.Ignore;
        private IntPtr StateData = IntPtr.Zero;
        private String URLReference;
        private WinTrustDataProvFlags ProvFlags = WinTrustDataProvFlags.SaferFlag;
        private WinTrustDataUIContext UIContext = WinTrustDataUIContext.Execute;

        // constructor for silent WinTrustDataChoice.File check
        public WinTrustData(String filename) {
            var wtfiData = new WinTrustFileInfo(filename);
            FileInfoPtr = Marshal.AllocCoTaskMem(Marshal.SizeOf(typeof (WinTrustFileInfo)));
            Marshal.StructureToPtr(wtfiData, FileInfoPtr, false);
        }

        ~WinTrustData() {
            Marshal.FreeCoTaskMem(FileInfoPtr);
        }
    }

    public class Verifier {
        private static readonly IntPtr INVALID_HANDLE_VALUE = new IntPtr(-1);
        // GUID of the action to perform
        private const string WINTRUST_ACTION_GENERIC_VERIFY_V2 = "{00AAC56B-CD44-11d0-8CC2-00C04FC295EE}";

        public static bool HasValidSignature(string fileName) {
            try {
                var wtd = new WinTrustData(fileName);
                var guidAction = new Guid(WINTRUST_ACTION_GENERIC_VERIFY_V2);
                WinVerifyTrustResult result = WinTrust.WinVerifyTrust(INVALID_HANDLE_VALUE, guidAction, wtd);
                bool ret = (result == WinVerifyTrustResult.Success);
                return ret;
            } catch (Exception) {
                return false;
            }
        }

        public static Dictionary<string, string> GetPublisherInformation(string filename) {
            var result = new Dictionary<string, string>();
            try {
                var cert = new X509Certificate2(filename);
                var fields = cert.Subject.Split(new[] {','}, StringSplitOptions.RemoveEmptyEntries);
                // var result = fields.Select(f => f.Split('=')).Where(s => s.Length > 1).ToDictionary(s => s[0], s => s[1]);

                result.Add("PublisherName", fields[0].Split('=')[1]);
            } catch (Exception) {
            }

            return result;
        }

        public static string GetPublisherName(string filename) {
            try {
                var cert = new X509Certificate2(filename);
                var fields = cert.Subject.Split(new[] {','}, StringSplitOptions.RemoveEmptyEntries);
                return fields[0].Split('=')[1];
            } catch (Exception) {
            }

            return null;
        }

        public static void GetSignatureInformation(string filename) {
            if (HasValidSignature(filename)) {
                var cert = new X509Certificate2(filename);
                Console.WriteLine("Cert: {0}", cert.Subject);

                var ch = new X509Chain();

                ch.ChainPolicy.RevocationFlag = X509RevocationFlag.ExcludeRoot;

                ch.ChainPolicy.RevocationMode = X509RevocationMode.Online;

                ch.ChainPolicy.UrlRetrievalTimeout = new TimeSpan(0, 1, 0);

                ch.ChainPolicy.VerificationFlags = X509VerificationFlags.AllFlags;

                ch.Build(cert);

                ch.ChainPolicy.VerificationFlags = X509VerificationFlags.AllFlags;
                ch.ChainPolicy.RevocationMode = X509RevocationMode.Online;

                Console.WriteLine("Chain Information");

                Console.WriteLine("Chain revocation flag: {0}", ch.ChainPolicy.RevocationFlag);
                Console.WriteLine("Chain revocation mode: {0}", ch.ChainPolicy.RevocationMode);
                Console.WriteLine("Chain verification flag: {0}", ch.ChainPolicy.VerificationFlags);
                Console.WriteLine("Chain verification time: {0}", ch.ChainPolicy.VerificationTime);
                Console.WriteLine("Chain status length: {0}", ch.ChainStatus.Length);
                Console.WriteLine("Chain application policy count: {0}", ch.ChainPolicy.ApplicationPolicy.Count);
                Console.WriteLine("Chain certificate policy count: {0} {1}", ch.ChainPolicy.CertificatePolicy.Count, Environment.NewLine);
                //Output chain element information.
                Console.WriteLine("Chain Element Information");
                Console.WriteLine("Number of chain elements: {0}", ch.ChainElements.Count);

                foreach (var element in ch.ChainElements) {
                    Console.WriteLine("Element issuer name: {0}", element.Certificate.Issuer);
                    Console.WriteLine("Element certificate valid until: {0}", element.Certificate.NotAfter);
                    Console.WriteLine("Element certificate is valid: {0}", element.Certificate.Verify());
                    Console.WriteLine("Element error status length: {0}", element.ChainElementStatus.Length);
                    Console.WriteLine("Element information: {0}", element.Information);
                    Console.WriteLine("Number of element extensions: {0}{1}", element.Certificate.Extensions.Count, Environment.NewLine);
                }

                if (ch.ChainStatus.Length > 0) {
                    for (int index = 0; index < ch.ChainStatus.Length; index++) {
                        Console.WriteLine(ch.ChainStatus[index].Status);
                        Console.WriteLine(ch.ChainStatus[index].StatusInformation);
                    }
                }

                Console.WriteLine("Cert Valid?: {0}", cert.Verify());
            }
        }

        public static bool IsTimestamped(string filename) {
            try {
                int encodingType;
                int contentType;
                int formatType;
                IntPtr certStore = IntPtr.Zero;
                IntPtr cryptMsg = IntPtr.Zero;
                IntPtr context = IntPtr.Zero;

                if (!WinCrypt.CryptQueryObject(
                    WinCrypt.CERT_QUERY_OBJECT_FILE,
                    Marshal.StringToHGlobalUni(filename),
                    WinCrypt.CERT_QUERY_CONTENT_FLAG_ALL,
                    WinCrypt.CERT_QUERY_FORMAT_FLAG_ALL,
                    0,
                    out encodingType,
                    out contentType,
                    out formatType,
                    ref certStore,
                    ref cryptMsg,
                    ref context)) {
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                }

                //expecting contentType=10; CERT_QUERY_CONTENT_PKCS7_SIGNED_EMBED 
                //Logger.LogInfo(string.Format("Querying file '{0}':", filename));
                //Logger.LogInfo(string.Format("  Encoding Type: {0}", encodingType));
                //Logger.LogInfo(string.Format("  Content Type: {0}", contentType));
                //Logger.LogInfo(string.Format("  Format Type: {0}", formatType));
                //Logger.LogInfo(string.Format("  Cert Store: {0}", certStore.ToInt32()));
                //Logger.LogInfo(string.Format("  Crypt Msg: {0}", cryptMsg.ToInt32()));
                //Logger.LogInfo(string.Format("  Context: {0}", context.ToInt32()));

                // Get size of the encoded message.
                int cbData = 0;
                if (!WinCrypt.CryptMsgGetParam(
                    cryptMsg,
                    WinCrypt.CMSG_ENCODED_MESSAGE, //Crypt32.CMSG_SIGNER_INFO_PARAM,
                    0,
                    IntPtr.Zero,
                    ref cbData)) {
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                }

                var vData = new byte[cbData];

                // Get the encoded message.
                if (!WinCrypt.CryptMsgGetParam(
                    cryptMsg,
                    WinCrypt.CMSG_ENCODED_MESSAGE, //Crypt32.CMSG_SIGNER_INFO_PARAM,
                    0,
                    vData,
                    ref cbData)) {
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                }

                var signedCms = new SignedCms();
                signedCms.Decode(vData);

                foreach (var signerInfo in signedCms.SignerInfos) {
                    foreach (var unsignedAttribute in signerInfo.UnsignedAttributes) {
                        if (unsignedAttribute.Oid.Value == WinCrypt.szOID_RSA_counterSign) {
                            foreach (var counterSignInfo in signerInfo.CounterSignerInfos) {
                                foreach (var signedAttribute in counterSignInfo.SignedAttributes) {
                                    if (signedAttribute.Oid.Value == WinCrypt.szOID_RSA_signingTime) {
                                        var fileTime = new FILETIME();
                                        int fileTimeSize = Marshal.SizeOf(fileTime);
                                        IntPtr fileTimePtr = Marshal.AllocCoTaskMem(fileTimeSize);
                                        Marshal.StructureToPtr(fileTime, fileTimePtr, true);

                                        var buffdata = new byte[fileTimeSize];
                                        Marshal.Copy(fileTimePtr, buffdata, 0, fileTimeSize);

                                        var buffSize = (uint)buffdata.Length;

                                        uint encoding = WinCrypt.X509_ASN_ENCODING | WinCrypt.PKCS_7_ASN_ENCODING;

                                        var rsaSigningTime = (UIntPtr)(uint)Marshal.StringToHGlobalAnsi(WinCrypt.szOID_RSA_signingTime);

                                        byte[] pbData = signedAttribute.Values[0].RawData;
                                        var ucbData = (uint)pbData.Length;

                                        bool workie = WinCrypt.CryptDecodeObject(encoding, rsaSigningTime, pbData, ucbData, 0, buffdata, ref buffSize);

                                        if (workie) {
                                            IntPtr fileTimePtr2 = Marshal.AllocCoTaskMem(buffdata.Length);
                                            Marshal.Copy(buffdata, 0, fileTimePtr2, buffdata.Length);
                                            var fileTime2 = (FILETIME)Marshal.PtrToStructure(fileTimePtr2, typeof (FILETIME));

                                            long hFT2 = (((long)fileTime2.dwHighDateTime) << 32) + ((uint)fileTime2.dwLowDateTime);

                                            DateTime dte = DateTime.FromFileTime(hFT2);
                                            Console.WriteLine(dte.ToString());
                                        } else {
                                            throw new Win32Exception(Marshal.GetLastWin32Error());
                                        }
                                    }
                                }
                            }

                            return true;
                        }
                    }
                }
            } catch (Exception) {
                // no logging
            }

            return false;
        }
    }
}