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

namespace CoApp.Toolkit.Engine.Model {
    /// <summary>
    /// Different types of package roles
    /// </summary>
    /// <remarks></remarks>
    public enum PackageRole {
        /// <summary>
        /// Shared Library (.NET Assembly, or native DLL)
        /// </summary>
        Assembly,
        
        /// <summary>
        /// Developer Library (.NET assembly or .lib/.h files)
        /// </summary>
        DeveloperLibrary,
        
        /// <summary>
        /// Source Code MSI
        /// </summary>
        SourceCode,
        
        /// <summary>
        /// Application (binaries, etc)
        /// </summary>
        Application,

        /// <summary>
        /// Device Driver
        /// </summary>
        Driver,

        /// <summary>
        /// A web-application (registers with a web server)
        /// </summary>
        WebApplication,

        /// <summary>
        /// Win32 Service (registers with SC)
        /// </summary>
        Service,

    }
}


