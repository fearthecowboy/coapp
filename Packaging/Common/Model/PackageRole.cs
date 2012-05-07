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

namespace CoApp.Packaging.Common.Model {
    using System.Xml.Serialization;

    /// <summary>
    ///   Different types of package roles
    /// </summary>
    /// <remarks>
    /// </remarks>
    public enum PackageRole {
        /// <summary>
        ///   Shared Library (.NET Assembly, or native DLL)
        /// </summary>
        [XmlEnum("Assembly")]
        Assembly,

        /// <summary>
        ///   Developer Library (.NET assembly or .lib/.h files)
        /// </summary>
        [XmlEnum("DeveloperLibrary")]
        DeveloperLibrary,

        /// <summary>
        ///   Source Code MSI
        /// </summary>
        [XmlEnum("SourceCode")]
        SourceCode,

        /// <summary>
        ///   Application (binaries, etc)
        /// </summary>
        [XmlEnum("Application")]
        Application,

        /// <summary>
        ///   Device Driver
        /// </summary>
        [XmlEnum("Driver")]
        Driver,

        /// <summary>
        ///   A web-application (registers with a web server)
        /// </summary>
        [XmlEnum("WebApplication")]
        WebApplication,

        /// <summary>
        ///   Win32 Service (registers with SC)
        /// </summary>
        [XmlEnum("Service")]
        Service,
    }
}