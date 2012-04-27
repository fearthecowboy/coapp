//-----------------------------------------------------------------------
// <copyright company="CoApp Project">
//     Copyright (c) 2003 Motus Technologies Inc. (http://www.motus.com)
//     Copyright (c) 2004 Novell Inc. (http://www.novell.com)
//     Copyright (c) 2011 Eric Schultz
// </copyright>
//-----------------------------------------------------------------------

namespace CoApp.Toolkit.Crypto {
    // TODO If Win on ARM is big-endian, this will not work and we'll need BitConverterLE from Mono.Security
    using System;
    using System.Globalization;
    using System.IO;
    using System.Security.Cryptography;
    using BitConverterLE = System.BitConverter;

    /// <summary>
    ///   Originally from Mono project in Mono.Security assembly as Mono.Security.StrongName
    /// </summary>
    internal sealed class StrongNameCertificate {
        internal class StrongNameSignature {
            public byte[] Hash { get; set; }

            public byte[] Signature { get; set; }

            public UInt32 MetadataPosition { get; set; }

            public UInt32 MetadataLength { get; set; }

            public UInt32 SignaturePosition { get; set; }

            public UInt32 SignatureLength { get; set; }

            // delay signed -> flag = 0x01
            // strongsigned -> flag = 0x09
            public byte CliFlag { get; set; }

            public UInt32 CliFlagPosition { get; set; }
        }

        internal enum StrongNameOptions {
            Metadata,
            Signature
        }

        private RSA rsa;
        private byte[] publicKey;
        private byte[] keyToken;
        private string tokenAlgorithm;

        public StrongNameCertificate(RSA rsa) {
            if (rsa == null) {
                throw new ArgumentNullException("rsa");
            }

            RSA = rsa;
        }

        private void InvalidateCache() {
            publicKey = null;
            keyToken = null;
        }

        public bool CanSign {
            get {
                if (rsa == null) {
                    return false;
                } else {
                    // the hard way
                    try {
                        RSAParameters p = rsa.ExportParameters(true);
                        return ((p.D != null) && (p.P != null) && (p.Q != null));
                    } catch (CryptographicException) {
                        return false;
                    }
                }
            }
        }

        public RSA RSA {
            get {
                // if none then we create a new keypair
                if (rsa == null) {
                    rsa = RSA.Create();
                }
                return rsa;
            }
            set {
                rsa = value;
                InvalidateCache();
            }
        }

        public byte[] PublicKey {
            get {
                if (publicKey == null) {
                    byte[] keyPair = CryptoConvert.ToCapiKeyBlob(rsa, false);
                    // since 2.0 public keys can vary from 384 to 16384 bits
                    publicKey = new byte[32 + (rsa.KeySize >> 3)];

                    // The first 12 bytes are documented at:
                    // http://msdn.microsoft.com/library/en-us/cprefadd/html/grfungethashfromfile.asp
                    // ALG_ID - Signature
                    publicKey[0] = keyPair[4];
                    publicKey[1] = keyPair[5];
                    publicKey[2] = keyPair[6];
                    publicKey[3] = keyPair[7];
                    // ALG_ID - Hash (SHA1 == 0x8004)
                    publicKey[4] = 0x04;
                    publicKey[5] = 0x80;
                    publicKey[6] = 0x00;
                    publicKey[7] = 0x00;
                    // Length of Public Key (in bytes)

                    byte[] lastPart = BitConverterLE.GetBytes(publicKey.Length - 12);
                    publicKey[8] = lastPart[0];
                    publicKey[9] = lastPart[1];
                    publicKey[10] = lastPart[2];
                    publicKey[11] = lastPart[3];
                    // Ok from here - Same structure as keypair - expect for public key
                    publicKey[12] = 0x06; // PUBLICKEYBLOB
                    // we can copy this part
                    Buffer.BlockCopy(keyPair, 1, publicKey, 13, publicKey.Length - 13);
                    // and make a small adjustment 
                    publicKey[23] = 0x31; // (RSA1 not RSA2)
                }
                return (byte[])publicKey.Clone();
            }
        }

        public byte[] PublicKeyToken {
            get {
                if (keyToken == null) {
                    byte[] publicKey = PublicKey;
                    if (publicKey == null) {
                        return null;
                    }
                    HashAlgorithm ha = HashAlgorithm.Create(TokenAlgorithm);
                    byte[] hash = ha.ComputeHash(publicKey);
                    // we need the last 8 bytes in reverse order
                    keyToken = new byte[8];
                    Buffer.BlockCopy(hash, (hash.Length - 8), keyToken, 0, 8);
                    Array.Reverse(keyToken, 0, 8);
                }
                return (byte[])keyToken.Clone();
            }
        }

        public string TokenAlgorithm {
            get {
                if (tokenAlgorithm == null) {
                    tokenAlgorithm = "SHA1";
                }
                return tokenAlgorithm;
            }
            set {
                string algo = value.ToUpper(CultureInfo.InvariantCulture);
                if ((algo == "SHA1") || (algo == "MD5")) {
                    tokenAlgorithm = value;
                    InvalidateCache();
                } else {
                    throw new ArgumentException("Unsupported hash algorithm for token");
                }
            }
        }

        public byte[] GetBytes() {
            return CryptoConvert.ToCapiPrivateKeyBlob(RSA);
        }

        private UInt32 RVAtoPosition(UInt32 r, int sections, byte[] headers) {
            for (int i = 0; i < sections; i++) {
                UInt32 p = BitConverterLE.ToUInt32(headers, i*40 + 20);
                UInt32 s = BitConverterLE.ToUInt32(headers, i*40 + 12);
                var l = (int)BitConverterLE.ToUInt32(headers, i*40 + 8);
                if ((s <= r) && (r < s + l)) {
                    return p + r - s;
                }
            }
            return 0;
        }

        internal StrongNameSignature StrongHash(Stream stream, StrongNameOptions options) {
            var info = new StrongNameSignature();

            HashAlgorithm hash = HashAlgorithm.Create(TokenAlgorithm);
            var cs = new CryptoStream(Stream.Null, hash, CryptoStreamMode.Write);

            // MS-DOS Header - always 128 bytes
            // ref: Section 24.2.1, Partition II Metadata
            var mz = new byte[128];
            stream.Read(mz, 0, 128);
            if (BitConverterLE.ToUInt16(mz, 0) != 0x5a4d) {
                return null;
            }
            UInt32 peHeader = BitConverterLE.ToUInt32(mz, 60);
            cs.Write(mz, 0, 128);
            if (peHeader != 128) {
                var mzextra = new byte[peHeader - 128];
                stream.Read(mzextra, 0, mzextra.Length);
                cs.Write(mzextra, 0, mzextra.Length);
            }

            // PE File Header - always 248 bytes
            // ref: Section 24.2.2, Partition II Metadata
            var pe = new byte[248];
            stream.Read(pe, 0, 248);
            if (BitConverterLE.ToUInt32(pe, 0) != 0x4550) {
                return null;
            }
            if (BitConverterLE.ToUInt16(pe, 4) != 0x14c) {
                return null;
            }
            // MUST zeroize both CheckSum and Security Directory
            var v = new byte[8];
            Buffer.BlockCopy(v, 0, pe, 88, 4);
            Buffer.BlockCopy(v, 0, pe, 152, 8);
            cs.Write(pe, 0, 248);

            UInt16 numSection = BitConverterLE.ToUInt16(pe, 6);
            int sectionLength = (numSection*40);
            var sectionHeaders = new byte[sectionLength];
            stream.Read(sectionHeaders, 0, sectionLength);
            cs.Write(sectionHeaders, 0, sectionLength);

            UInt32 cliHeaderRVA = BitConverterLE.ToUInt32(pe, 232);
            UInt32 cliHeaderPos = RVAtoPosition(cliHeaderRVA, numSection, sectionHeaders);
            var cliHeaderSiz = (int)BitConverterLE.ToUInt32(pe, 236);

            // CLI Header
            // ref: Section 24.3.3, Partition II Metadata
            var cli = new byte[cliHeaderSiz];
            stream.Position = cliHeaderPos;
            stream.Read(cli, 0, cliHeaderSiz);

            UInt32 strongNameSignatureRVA = BitConverterLE.ToUInt32(cli, 32);
            info.SignaturePosition = RVAtoPosition(strongNameSignatureRVA, numSection, sectionHeaders);
            info.SignatureLength = BitConverterLE.ToUInt32(cli, 36);

            UInt32 metadataRVA = BitConverterLE.ToUInt32(cli, 8);
            info.MetadataPosition = RVAtoPosition(metadataRVA, numSection, sectionHeaders);
            info.MetadataLength = BitConverterLE.ToUInt32(cli, 12);

            if (options == StrongNameOptions.Metadata) {
                cs.Close();
                hash.Initialize();
                var metadata = new byte[info.MetadataLength];
                stream.Position = info.MetadataPosition;
                stream.Read(metadata, 0, metadata.Length);
                info.Hash = hash.ComputeHash(metadata);
                return info;
            }

            // now we hash every section EXCEPT the signature block
            for (int i = 0; i < numSection; i++) {
                UInt32 start = BitConverterLE.ToUInt32(sectionHeaders, i*40 + 20);
                var length = (int)BitConverterLE.ToUInt32(sectionHeaders, i*40 + 16);
                var section = new byte[length];
                stream.Position = start;
                stream.Read(section, 0, length);
                if ((start <= info.SignaturePosition) && (info.SignaturePosition < start + length)) {
                    // hash before the signature
                    var before = (int)(info.SignaturePosition - start);
                    if (before > 0) {
                        cs.Write(section, 0, before);
                    }
                    // copy signature
                    info.Signature = new byte[info.SignatureLength];
                    Buffer.BlockCopy(section, before, info.Signature, 0, (int)info.SignatureLength);
                    Array.Reverse(info.Signature);
                    // hash after the signature
                    var s = (int)(before + info.SignatureLength);
                    int after = (length - s);
                    if (after > 0) {
                        cs.Write(section, s, after);
                    }
                } else {
                    cs.Write(section, 0, length);
                }
            }

            cs.Close();
            info.Hash = hash.Hash;
            return info;
        }

        public bool Sign(string fileName) {
            bool result = false;
            StrongNameSignature sn;
            using (FileStream fs = File.OpenRead(fileName)) {
                sn = StrongHash(fs, StrongNameOptions.Signature);
                fs.Close();
            }
            if (sn.Hash == null) {
                return false;
            }

            byte[] signature = null;
            try {
                var sign = new RSAPKCS1SignatureFormatter(rsa);
                sign.SetHashAlgorithm(TokenAlgorithm);
                signature = sign.CreateSignature(sn.Hash);
                Array.Reverse(signature);
            } catch (CryptographicException) {
                return false;
            }

            using (FileStream fs = File.OpenWrite(fileName)) {
                fs.Position = sn.SignaturePosition;
                fs.Write(signature, 0, signature.Length);
                fs.Close();
                result = true;
            }
            return result;
        }
    }
}