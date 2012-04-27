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

    /// <summary>
    ///   Exception for when a given package file isn't valid
    /// </summary>
    /// <remarks>
    /// </remarks>
    public class InvalidPackageException : CoAppException {
        /// <summary>
        ///   the path to the file that is invalid
        /// </summary>
        public string PackagePath;

        /// <summary>
        ///   the Reason we consider the package invalid
        /// </summary>
        public InvalidReason Reason;

        /// <summary>
        ///   Initializes a new instance of the <see cref="InvalidPackageException" /> class.
        /// </summary>
        /// <param name="reason"> The reason. </param>
        /// <param name="packagePath"> The package path. </param>
        /// <remarks>
        /// </remarks>
        public InvalidPackageException(InvalidReason reason, string packagePath) : base(true) {
            PackagePath = packagePath;
            Reason = reason;
        }
    }
}