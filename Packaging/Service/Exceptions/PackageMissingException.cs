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

namespace CoApp.Packaging.Service.Exceptions {
    using System;
    using Toolkit.Exceptions;

    internal class PackageMissingException : CoAppException {
        public string Arch { get; set; }
        public string PublicKeyToken { get; set; }
        public string Name { get; set; }
        public UInt64 Version { get; set; }

        public PackageMissingException(string name, string arch, UInt64 version, string publicKeyToken) {
            Name = name;
            Arch = arch;
            Version = version;
            PublicKeyToken = publicKeyToken;
        }
    }
}