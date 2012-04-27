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
    /// <summary>
    ///   Represents th reason that the package file is considered invalid.
    /// </summary>
    /// <remarks>
    /// </remarks>
    public enum InvalidReason {
        /// <summary>
        ///   The package isn't an MSI
        /// </summary>
        NotValidMSI,

        /// <summary>
        ///   The package isn't a coapp-style MSI
        /// </summary>
        NotCoAppMSI,

        /// <summary>
        ///   the package is a coapp msi that doesn't conform right (old version?).
        /// </summary>
        MalformedCoAppMSI,
    }
}