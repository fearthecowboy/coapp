//-----------------------------------------------------------------------
// <copyright company="CoApp Project">
//     Copyright (c) 2011 Garrett Serack . All rights reserved.
// </copyright>
// <license>
//     The software is licensed under the Apache 2.0 License (the "License")
//     You may not use the software except in compliance with the License. 
// </license>
//-----------------------------------------------------------------------

namespace CoApp.Packaging.Common.Model {
    /// <summary>
    /// The type of action for this composition rule
    /// </summary>
    /// <remarks></remarks>
    public enum CompositionAction {
        /// <summary>
        /// Create a symlink to a file
        /// </summary>
        SymlinkFile,
        /// <summary>
        /// Create a symlink to a folder
        /// </summary>
        SymlinkFolder,
        /// <summary>
        /// Create a .lnk shortcut to a file
        /// </summary>
        Shortcut,

        /// <summary>
        /// Creates an evironment variable
        /// </summary>
        EnvironmentVariable,

        /// <summary>
        /// Creates a registry key
        /// </summary>
        Registry,

        /// <summary>
        /// Copies a file from one place to another
        /// </summary>
        FileCopy,

        /// <summary>
        /// Copies a file from one place to another, processes ${macros} in file
        /// </summary>
        FileRewrite,
    }
}