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
    using Toolkit.Exceptions;

    public class PackageNotFoundException : CoAppException {
        public string PackagePath;

        public PackageNotFoundException(string packagePath) {
            PackagePath = packagePath;
        }
    }
}