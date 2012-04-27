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
    using System.Runtime.ExceptionServices;
    using System.Runtime.InteropServices;
    using System.Text;
    using System.Text.RegularExpressions;
    using Extensions;
    using Microsoft.Win32.SafeHandles;

    /// <summary>
    ///   A low-level interface to mucking with reparse points on NTFS see: http://msdn.microsoft.com/en-us/library/cc232007(v=prot.10).aspx
    /// </summary>
    /// <remarks>
    /// </remarks>
    public class ReparsePoint {
        /// <summary>
        ///   This prefix indicates to NTFS that the path is to be treated as a non-interpreted path in the virtual file system.
        /// </summary>
        private const string NonInterpretedPathPrefix = @"\??\";

        /// <summary>
        /// </summary>
        private static Regex UncPrefixRx = new Regex(@"\\\?\?\\UNC\\");

        /// <summary>
        /// </summary>
        private static Regex DrivePrefixRx = new Regex(@"\\\?\?\\[a-z,A-Z]\:\\");

        /// <summary>
        /// </summary>
        private static Regex VolumePrefixRx = new Regex(@"\\\?\?\\Volume");

        /// <summary>
        /// </summary>
        private ReparseData _reparseDataData;

        /// <summary>
        ///   Prevents a default instance of the <see cref="ReparsePoint" /> class from being created. Populates the data from the buffer pointed to by the pointer.
        /// </summary>
        /// <param name="buffer"> The buffer. </param>
        /// <remarks>
        /// </remarks>
        private ReparsePoint(IntPtr buffer) {
            if (buffer == IntPtr.Zero) {
                throw new ArgumentNullException("buffer");
            }

            _reparseDataData = (ReparseData)Marshal.PtrToStructure(buffer, typeof (ReparseData));
        }

        /// <summary>
        ///   Gets a value indicating whether this instance is symlink or junction.
        /// </summary>
        /// <remarks>
        /// </remarks>
        public bool IsSymlinkOrJunction {
            get {
                return (_reparseDataData.ReparseTag == IoReparseTag.MountPoint || _reparseDataData.ReparseTag == IoReparseTag.Symlink) && !IsMountPoint;
            }
        }

        /// <summary>
        ///   Gets a value indicating whether this instance is relative symlink.
        /// </summary>
        /// <remarks>
        /// </remarks>
        public bool IsRelativeSymlink {
            get {
                return _reparseDataData.ReparseTag == IoReparseTag.Symlink && (_reparseDataData.PathBuffer[0] & 1) == 1;
            }
        }

        /// <summary>
        ///   Gets a value indicating whether this instance is mount point.
        /// </summary>
        /// <remarks>
        /// </remarks>
        public bool IsMountPoint {
            get {
                return VolumePrefixRx.Match(SubstituteName).Success;
            }
        }

        /// <summary>
        ///   Gets the "print name" of the reparse point
        /// </summary>
        /// <remarks>
        /// </remarks>
        public string PrintName {
            get {
                var extraOffset = _reparseDataData.ReparseTag == IoReparseTag.Symlink ? 4 : 0;
                return _reparseDataData.PrintNameLength > 0
                    ? Encoding.Unicode.GetString(_reparseDataData.PathBuffer, _reparseDataData.PrintNameOffset + extraOffset, _reparseDataData.PrintNameLength)
                    : string.Empty;
            }
        }

        /// <summary>
        ///   Gets the "substitute name" of the reparse point.
        /// </summary>
        /// <remarks>
        /// </remarks>
        public string SubstituteName {
            get {
                var extraOffset = _reparseDataData.ReparseTag == IoReparseTag.Symlink ? 4 : 0;
                return _reparseDataData.SubstituteNameLength > 0
                    ? Encoding.Unicode.GetString(_reparseDataData.PathBuffer, _reparseDataData.SubstituteNameOffset + extraOffset,
                        _reparseDataData.SubstituteNameLength)
                    : string.Empty;
            }
        }

        /// <summary>
        ///   Gets the file handle to the reparse point.
        /// </summary>
        /// <param name="reparsePoint"> The reparse point. </param>
        /// <param name="accessMode"> The access mode. </param>
        /// <returns> </returns>
        /// <remarks>
        /// </remarks>
        private static SafeFileHandle GetReparsePointHandle(string reparsePoint, NativeFileAccess accessMode) {
            var reparsePointHandle = Kernel32.CreateFile(reparsePoint, accessMode, FileShare.Read | FileShare.Write | FileShare.Delete, IntPtr.Zero,
                FileMode.Open, NativeFileAttributesAndFlags.BackupSemantics | NativeFileAttributesAndFlags.OpenReparsePoint, IntPtr.Zero);

            if (Marshal.GetLastWin32Error() != 0) {
                throw new IOException("Unable to open reparse point.", Marshal.GetExceptionForHR(Marshal.GetHRForLastWin32Error()));
            }

            return reparsePointHandle;
        }

        /// <summary>
        ///   Determines whether a given path is a reparse point
        /// </summary>
        /// <param name="path"> The path. </param>
        /// <returns> <c>true</c> if [is reparse point] [the specified path]; otherwise, <c>false</c> . </returns>
        /// <remarks>
        /// </remarks>
        public static bool IsReparsePoint(string path) {
            return (File.GetAttributes(path) & FileAttributes.ReparsePoint) == FileAttributes.ReparsePoint;
        }

        /// <summary>
        ///   Gets the actual path of a reparse point
        /// </summary>
        /// <param name="linkPath"> The link path. </param>
        /// <returns> </returns>
        /// <remarks>
        /// </remarks>
        public static string GetActualPath(string linkPath) {
            if (!IsReparsePoint(linkPath)) {
                // if it's not a reparse point, return the path given.
                return linkPath;
            }

            var reparsePoint = Open(linkPath);

            var target = reparsePoint.SubstituteName.NormalizePath();

            if (reparsePoint.IsRelativeSymlink) {
                target = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(linkPath), target));
            }

            return target;
        }

        /// <summary>
        ///   Opens the specified path as a reparse point. throws if it's not a reparse point.
        /// </summary>
        /// <param name="path"> The path. </param>
        /// <returns> </returns>
        /// <remarks>
        /// </remarks>
        public static ReparsePoint Open(string path) {
            if (!IsReparsePoint(path)) {
                throw new IOException("Path is not reparse point");
            }

            using (var handle = GetReparsePointHandle(path, NativeFileAccess.GenericRead)) {
                if (handle == null) {
                    throw new IOException("Unable to get information about reparse point.");
                }

                var outBufferSize = Marshal.SizeOf(typeof (ReparseData));
                var outBuffer = Marshal.AllocHGlobal(outBufferSize);

                try {
                    int bytesReturned;
                    var result = Kernel32.DeviceIoControl(handle, ControlCodes.GetReparsePoint, IntPtr.Zero, 0, outBuffer, outBufferSize, out bytesReturned,
                        IntPtr.Zero);

                    if (!result) {
                        var error = Marshal.GetLastWin32Error();
                        if (error == ReparsePointError.NotAReparsePoint) {
                            throw new IOException("Path is not a reparse point.", Marshal.GetExceptionForHR(Marshal.GetHRForLastWin32Error()));
                        }

                        throw new IOException("Unable to get information about reparse point.", Marshal.GetExceptionForHR(Marshal.GetHRForLastWin32Error()));
                    }
                    return new ReparsePoint(outBuffer);
                } finally {
                    Marshal.FreeHGlobal(outBuffer);
                }
            }
        }

        /// <summary>
        ///   Creates the junction.
        /// </summary>
        /// <param name="junctionPath"> The junction path. </param>
        /// <param name="targetDirectory"> The target directory. </param>
        /// <returns> </returns>
        /// <remarks>
        /// </remarks>
        public static ReparsePoint CreateJunction(string junctionPath, string targetDirectory) {
            junctionPath = junctionPath.GetFullPath();
            targetDirectory = targetDirectory.GetFullPath();

            if (!Directory.Exists(targetDirectory)) {
                throw new IOException("Target path does not exist or is not a directory.");
            }

            if (Directory.Exists(junctionPath) || File.Exists(junctionPath)) {
                throw new IOException("Junction path already exists.");
            }

            Directory.CreateDirectory(junctionPath);

            using (var handle = GetReparsePointHandle(junctionPath, NativeFileAccess.GenericWrite)) {
                var substituteName = Encoding.Unicode.GetBytes(NonInterpretedPathPrefix + targetDirectory);
                var printName = Encoding.Unicode.GetBytes(targetDirectory);

                var reparseDataBuffer = new ReparseData {
                    ReparseTag = IoReparseTag.MountPoint,
                    SubstituteNameOffset = 0,
                    SubstituteNameLength = (ushort)substituteName.Length,
                    PrintNameOffset = (ushort)(substituteName.Length + 2),
                    PrintNameLength = (ushort)printName.Length,
                    PathBuffer = new byte[0x3ff0],
                };

                reparseDataBuffer.ReparseDataLength = (ushort)(reparseDataBuffer.PrintNameLength + reparseDataBuffer.PrintNameOffset + 10);

                Array.Copy(substituteName, reparseDataBuffer.PathBuffer, substituteName.Length);
                Array.Copy(printName, 0, reparseDataBuffer.PathBuffer, reparseDataBuffer.PrintNameOffset, printName.Length);

                var inBufferSize = Marshal.SizeOf(reparseDataBuffer);
                var inBuffer = Marshal.AllocHGlobal(inBufferSize);

                try {
                    Marshal.StructureToPtr(reparseDataBuffer, inBuffer, false);

                    int bytesReturned;
                    var result = Kernel32.DeviceIoControl(handle, ControlCodes.SetReparsePoint, inBuffer, reparseDataBuffer.ReparseDataLength + 8, IntPtr.Zero,
                        0, out bytesReturned, IntPtr.Zero);

                    if (!result) {
                        Directory.Delete(junctionPath);
                        throw new IOException("Unable to create junction point.", Marshal.GetExceptionForHR(Marshal.GetHRForLastWin32Error()));
                    }

                    return Open(junctionPath);
                } finally {
                    Marshal.FreeHGlobal(inBuffer);
                }
            }
        }

        /// <summary>
        ///   Creates the symlink.
        /// </summary>
        /// <param name="symlinkPath"> The symlink path. </param>
        /// <param name="linkTarget"> The link target. </param>
        /// <returns> </returns>
        /// <remarks>
        /// </remarks>
        [HandleProcessCorruptedStateExceptions]
        public static ReparsePoint CreateSymlink(string symlinkPath, string linkTarget) {
            symlinkPath = symlinkPath.GetFullPath();
            linkTarget = linkTarget.GetFullPath();

            if (Directory.Exists(symlinkPath) || File.Exists(symlinkPath)) {
                throw new IOException("Symlink path already exists.");
            }

            if (Directory.Exists(linkTarget)) {
                Directory.CreateDirectory(symlinkPath);
            } else if (File.Exists(linkTarget)) {
                File.Create(symlinkPath).Close();
            } else {
                throw new IOException("Target path does not exist or is not a directory.");
            }

            // dark magic kung-fu to get privilige to create symlink.
            var state = IntPtr.Zero;
            UInt32 privilege = 35;
            Ntdll.RtlAcquirePrivilege(ref privilege, 1, 0, ref state);

            using (var handle = GetReparsePointHandle(symlinkPath, NativeFileAccess.GenericWrite)) {
                var substituteName = Encoding.Unicode.GetBytes(NonInterpretedPathPrefix + linkTarget);
                var printName = Encoding.Unicode.GetBytes(linkTarget);
                var extraOffset = 4;

                var reparseDataBuffer = new ReparseData {
                    ReparseTag = IoReparseTag.Symlink,
                    SubstituteNameOffset = 0,
                    SubstituteNameLength = (ushort)substituteName.Length,
                    PrintNameOffset = (ushort)(substituteName.Length + 2),
                    PrintNameLength = (ushort)printName.Length,
                    PathBuffer = new byte[0x3ff0],
                };

                reparseDataBuffer.ReparseDataLength = (ushort)(reparseDataBuffer.PrintNameLength + reparseDataBuffer.PrintNameOffset + 10 + extraOffset);

                Array.Copy(substituteName, 0, reparseDataBuffer.PathBuffer, extraOffset, substituteName.Length);
                Array.Copy(printName, 0, reparseDataBuffer.PathBuffer, reparseDataBuffer.PrintNameOffset + extraOffset, printName.Length);

                var inBufferSize = Marshal.SizeOf(reparseDataBuffer);
                var inBuffer = Marshal.AllocHGlobal(inBufferSize);

                try {
                    Marshal.StructureToPtr(reparseDataBuffer, inBuffer, false);

                    int bytesReturned;
                    var result = Kernel32.DeviceIoControl(handle, ControlCodes.SetReparsePoint, inBuffer, reparseDataBuffer.ReparseDataLength + 8, IntPtr.Zero,
                        0, out bytesReturned, IntPtr.Zero);

                    if (!result) {
                        if (Directory.Exists(symlinkPath)) {
                            Directory.Delete(symlinkPath);
                        } else if (File.Exists(symlinkPath)) {
                            File.Delete(symlinkPath);
                        }

                        throw new IOException("Unable to create symlink.", Marshal.GetExceptionForHR(Marshal.GetHRForLastWin32Error()));
                    }
                    return Open(symlinkPath);
                } finally {
                    Marshal.FreeHGlobal(inBuffer);
                    try {
                        if (state != IntPtr.Zero) {
                            Ntdll.RtlReleasePrivilege(state);
                        }
                    } catch {
                        // sometimes this doesn't work so well
                    }
                }
            }
        }

        /// <summary>
        ///   Changes the reparse point target.
        /// </summary>
        /// <param name="reparsePointPath"> The reparse point path. </param>
        /// <param name="newReparsePointTarget"> The new reparse point target. </param>
        /// <returns> </returns>
        /// <remarks>
        /// </remarks>
        [HandleProcessCorruptedStateExceptions]
        public static ReparsePoint ChangeReparsePointTarget(string reparsePointPath, string newReparsePointTarget) {
            reparsePointPath = reparsePointPath.GetFullPath();
            newReparsePointTarget = newReparsePointTarget.GetFullPath();
            var oldReparsePointTarget = GetActualPath(reparsePointPath).GetFullPath();
            if (newReparsePointTarget.Equals(oldReparsePointTarget, StringComparison.CurrentCultureIgnoreCase)) {
                return Open(reparsePointPath);
            }

            if (!IsReparsePoint(reparsePointPath)) {
                throw new IOException("Path is not a reparse point.");
            }

            if (Directory.Exists(reparsePointPath)) {
                if (!Directory.Exists(newReparsePointTarget)) {
                    throw new IOException("Reparse point is a directory, but no directory exists for new reparse point target.");
                }
            } else if (File.Exists(reparsePointPath)) {
                if (!File.Exists(newReparsePointTarget)) {
                    throw new IOException("Reparse point is a file, but no file exists for new reparse point target.");
                }
            } else {
                throw new IOException("Reparse Point is not a file or directory ?");
            }

            var reparsePoint = Open(reparsePointPath);
            var isSymlink = reparsePoint._reparseDataData.ReparseTag == IoReparseTag.Symlink;

            if (reparsePoint._reparseDataData.ReparseTag != IoReparseTag.MountPoint && reparsePoint._reparseDataData.ReparseTag != IoReparseTag.Symlink) {
                throw new IOException("ChangeReparsePointTarget only works on junctions and symlink reparse points.");
            }

            // dark magic kung-fu to get privilige to create symlink.
            var state = IntPtr.Zero;
            UInt32 privilege = 35;
            if (isSymlink) {
                Ntdll.RtlAcquirePrivilege(ref privilege, 1, 0, ref state);
            }

            using (var handle = GetReparsePointHandle(reparsePointPath, NativeFileAccess.GenericWrite)) {
                var substituteName = Encoding.Unicode.GetBytes(NonInterpretedPathPrefix + newReparsePointTarget);
                var printName = Encoding.Unicode.GetBytes(newReparsePointTarget);
                var extraOffset = isSymlink ? 4 : 0;

                reparsePoint._reparseDataData.SubstituteNameOffset = 0;
                reparsePoint._reparseDataData.SubstituteNameLength = (ushort)substituteName.Length;
                reparsePoint._reparseDataData.PrintNameOffset = (ushort)(substituteName.Length + 2);
                reparsePoint._reparseDataData.PrintNameLength = (ushort)printName.Length;
                reparsePoint._reparseDataData.PathBuffer = new byte[0x3ff0];

                reparsePoint._reparseDataData.ReparseDataLength =
                    (ushort)(reparsePoint._reparseDataData.PrintNameLength + reparsePoint._reparseDataData.PrintNameOffset + 10 + extraOffset);

                Array.Copy(substituteName, 0, reparsePoint._reparseDataData.PathBuffer, extraOffset, substituteName.Length);
                Array.Copy(printName, 0, reparsePoint._reparseDataData.PathBuffer, reparsePoint._reparseDataData.PrintNameOffset + extraOffset, printName.Length);

                var inBufferSize = Marshal.SizeOf(reparsePoint._reparseDataData);
                var inBuffer = Marshal.AllocHGlobal(inBufferSize);

                try {
                    Marshal.StructureToPtr(reparsePoint._reparseDataData, inBuffer, false);

                    int bytesReturned;
                    var result = Kernel32.DeviceIoControl(handle, ControlCodes.SetReparsePoint, inBuffer, reparsePoint._reparseDataData.ReparseDataLength + 8,
                        IntPtr.Zero, 0, out bytesReturned, IntPtr.Zero);

                    if (!result) {
                        throw new IOException("Unable to modify reparse point.", Marshal.GetExceptionForHR(Marshal.GetHRForLastWin32Error()));
                    }
                    return Open(reparsePointPath);
                } finally {
                    if (isSymlink) {
                        try {
                            if (state != IntPtr.Zero) {
                                Ntdll.RtlReleasePrivilege(state);
                            }
                        } catch {
                            // sometimes this doesn't work so well
                        }
                    }
                    Marshal.FreeHGlobal(inBuffer);
                }
            }
        }
    }
}