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

namespace CoApp.Toolkit.Win32 {
    using System.IO;
    using Exceptions;
    using Extensions;

    public class ModernSymlink : ISymlink {
        #region ISymlink Members

        public void MakeFileLink(string linkPath, string actualFilePath) {
            linkPath = linkPath.GetFullPath();
            actualFilePath = GetActualPath(actualFilePath.GetFullPath());

            if (!File.Exists(actualFilePath)) {
                throw new FileNotFoundException("Cannot link to non-existent file", actualFilePath);
            }

            if (File.Exists(linkPath) && IsSymlink(linkPath)) {
                ReparsePoint.ChangeReparsePointTarget(linkPath, actualFilePath);
                return;
            }

            if (File.Exists(linkPath) || Directory.Exists(linkPath)) {
                throw new ConflictingFileOrFolderException(linkPath);
            }

            Kernel32.CreateSymbolicLink(linkPath, actualFilePath, 0);
        }

        public void MakeDirectoryLink(string linkPath, string actualFolderPath) {
            linkPath = linkPath.GetFullPath();
            actualFolderPath = GetActualPath(actualFolderPath.GetFullPath());

            if (!Directory.Exists(actualFolderPath)) {
                throw new FileNotFoundException("Cannot link to non-existent directory", actualFolderPath);
            }

            if (Directory.Exists(linkPath) && IsSymlink(linkPath)) {
                ReparsePoint.ChangeReparsePointTarget(linkPath, actualFolderPath);
                return;
            }

            if (File.Exists(linkPath) || Directory.Exists(linkPath)) {
                throw new ConflictingFileOrFolderException(linkPath);
            }

            Kernel32.CreateSymbolicLink(linkPath, actualFolderPath, 1);
        }

        public void ChangeLinkTarget(string linkPath, string newActualPath) {
            linkPath = linkPath.GetFullPath();
            newActualPath = GetActualPath(newActualPath.GetFullPath());

            if (!IsSymlink(linkPath)) {
                throw new PathIsNotSymlinkException(linkPath);
            }

            ReparsePoint.ChangeReparsePointTarget(linkPath, newActualPath);
        }

        public void DeleteSymlink(string linkPath) {
            linkPath = linkPath.GetFullPath();
            if (!File.Exists(linkPath) && !Directory.Exists(linkPath)) {
                return;
            }

            if (IsSymlink(linkPath)) {
                deleteSymlink(linkPath);
            }
            else {
                throw new PathIsNotSymlinkException(linkPath);
            }
        }

        public bool IsSymlink(string linkPath) {
            if (!ReparsePoint.IsReparsePoint(linkPath)) {
                return false;
            }

            var reparsePoint = ReparsePoint.Open(linkPath);
            return reparsePoint.IsSymlinkOrJunction;
        }

        public string GetActualPath(string linkPath) {
            linkPath = linkPath.GetFullPath();
            return ReparsePoint.GetActualPath(linkPath);
        }

        #endregion

        private void deleteSymlink(string linkPath) {
            linkPath.TryHardToDelete();
        }
    }
}