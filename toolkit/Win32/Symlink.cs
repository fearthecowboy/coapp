//-----------------------------------------------------------------------
// <copyright company="CoApp Project">
//     Copyright (c) 2011 Garrett Serack . All rights reserved.
// </copyright>
// <license>
//     The software is licensed under the Apache 2.0 License (the "License")
//     You may not use the software except in compliance with the License. 
// </license>
//-----------------------------------------------------------------------

namespace CoApp.Toolkit.Win32 {
    using System;
    using System.IO;

    /// <summary>
    ///   Wrapper class to abstract out which symlink implementation to use on a given platform
    /// </summary>
    /// <remarks>
    /// </remarks>
    public class Symlink {
        /// <summary>
        ///   backing field for the ISymlink instance for handling symlinks. Vista+ gets a ModernSymlink instance.
        /// </summary>
        private static Lazy<ISymlink> _symlink = new Lazy<ISymlink>(() => {
            if (WindowsVersionInfo.IsVistaOrBeyond) {
                return new ModernSymlink();
            }
            return new LegacySymlink();
        });

        /// <summary>
        ///   Wrapper Method: Makes the file link.
        /// </summary>
        /// <param name = "linkPath">The link path.</param>
        /// <param name = "actualFilePath">The actual file path.</param>
        /// <remarks>
        /// </remarks>
        public static void MakeFileLink(string linkPath, string actualFilePath) {
            _symlink.Value.MakeFileLink(linkPath, actualFilePath);
        }

        /// <summary>
        ///   Wrapper Method: Makes the directory link.
        /// </summary>
        /// <param name = "linkPath">The link path.</param>
        /// <param name = "actualFolderPath">The actual folder path.</param>
        /// <remarks>
        /// </remarks>
        public static void MakeDirectoryLink(string linkPath, string actualFolderPath) {
            _symlink.Value.MakeDirectoryLink(linkPath, actualFolderPath);
        }

        /// <summary>
        ///   Wrapper Method: Changes the link target.
        /// </summary>
        /// <param name = "linkPath">The link path.</param>
        /// <param name = "newActualPath">The new actual path.</param>
        /// <remarks>
        /// </remarks>
        public static void ChangeLinkTarget(string linkPath, string newActualPath) {
            _symlink.Value.ChangeLinkTarget(linkPath, newActualPath);
        }

        /// <summary>
        ///   Wrapper Method: Deletes the symlink.
        /// </summary>
        /// <param name = "linkPath">The link path.</param>
        /// <remarks>
        /// </remarks>
        public static void DeleteSymlink(string linkPath) {
            _symlink.Value.DeleteSymlink(linkPath);
        }

        /// <summary>
        ///   Wrapper Method: Determines whether the specified link path is symlink.
        /// </summary>
        /// <param name = "linkPath">The link path.</param>
        /// <returns><c>true</c> if the specified link path is symlink; otherwise, <c>false</c>.</returns>
        /// <remarks>
        /// </remarks>
        public static bool IsSymlink(string linkPath) {
            try {
                return _symlink.Value.IsSymlink(linkPath);
            }
            catch {
            }
            return false;
        }

        /// <summary>
        ///   Wrapper Method: Determines whether the specified link path is symlink and that it points to an actual file or directory
        /// </summary>
        /// <param name = "linkPath">The link path.</param>
        /// <returns><c>true</c> if the specified link path is symlink and the target exists; otherwise, <c>false</c>.</returns>
        /// <remarks>
        /// </remarks>
        public static bool? IsValidLink(string linkPath) {
            if( IsSymlink(linkPath) ) {
                var actualPath = _symlink.Value.GetActualPath(linkPath);
                return File.Exists(actualPath) || Directory.Exists(actualPath);
            }
            return null;
        }

        /// <summary>
        ///   Wrapper Method: Determines whether the specified link path is a symlink and if it points to an invalid location.
        /// </summary>
        /// <param name = "linkPath">The link path.</param>
        /// <returns><c>true</c> if the specified link path is symlink and the target exists; otherwise, <c>false</c>.</returns>
        /// <remarks>
        /// </remarks>
        public static bool? IsInvalidLink(string linkPath) {
            if (IsSymlink(linkPath)) {
                var actualPath = _symlink.Value.GetActualPath(linkPath);
                return !File.Exists(actualPath) && !Directory.Exists(actualPath);
            }
            return null;
        }


        /// <summary>
        ///   Wrapper Method: Gets the actual path.
        /// </summary>
        /// <param name = "linkPath">The link path.</param>
        /// <returns></returns>
        /// <remarks>
        /// </remarks>
        public static string GetActualPath(string linkPath) {
            return _symlink.Value.GetActualPath(linkPath);
        }
    }
}