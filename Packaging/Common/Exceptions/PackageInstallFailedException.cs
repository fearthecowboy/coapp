//-----------------------------------------------------------------------
// <copyright company="CoApp Project">
//     Copyright (c) 2011 Garrett Serack . All rights reserved.
// </copyright>
// <license>
//     The software is licensed under the Apache 2.0 License (the "License")
//     You may not use the software except in compliance with the License. 
// </license>
//-----------------------------------------------------------------------

namespace CoApp.Packaging.Common.Exceptions {
    using CoApp.Toolkit.Exceptions;
    
#if COAPP_ENGINE_CORE
    using Packaging.Service;
#endif
    
#if COAPP_ENGINE_CLIENT
    using CoApp.Packaging.Client;
#endif

    internal class PackageInstallFailedException : CoAppException {
        internal Package FailedPackage;

        internal PackageInstallFailedException(Package failedPackage) {
            FailedPackage = failedPackage;
        }
    }
}