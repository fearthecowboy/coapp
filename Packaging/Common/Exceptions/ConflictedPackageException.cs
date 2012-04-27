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

namespace CoApp.Packaging.Common.Exceptions {
    using System.Collections.Generic;
    using CoApp.Toolkit.Exceptions;
    
#if COAPP_ENGINE_CORE
    using Packaging.Service;
#endif
#if COAPP_ENGINE_CLIENT
    using CoApp.Packaging.Client;
#endif
    public class ConflictedPackagesException : CoAppException {
        public readonly IEnumerable<Package[]> Packages;

        public ConflictedPackagesException(IEnumerable<Package[]> packages) {
            Packages = packages;
        }
    }
}