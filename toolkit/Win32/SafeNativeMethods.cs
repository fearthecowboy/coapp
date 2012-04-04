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
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.IO;
    using System.Linq;
    using System.Runtime.InteropServices;
    using System.Text;
    using Microsoft.Win32.SafeHandles;
    using Properties;

    /// <summary>
    ///   Safe native methods. (part of alternate streams stuff)
    /// </summary>
    internal static class SafeNativeMethods {
        #region Constants and flags

        public const int MaxPath = 256;
        private const string LongPathPrefix = @"\\?\";
        public const char StreamSeparator = ':';
        public const int DefaultBufferSize = 0x1000;

        private const int ErrorFileNotFound = 2;

        // "Characters whose integer representations are in the range from 1 through 31, 
        // except for alternate streams where these characters are allowed"
        // http://msdn.microsoft.com/en-us/library/aa365247(v=VS.85).aspx
        private static readonly char[] InvalidStreamNameChars = Path.GetInvalidFileNameChars().Where(c => c < 1 || c > 31).ToArray();

        #endregion

        #region Utility Structures

        public struct Win32StreamInfo {
            public FileStreamAttributes StreamAttributes;
            public string StreamName;
            public long StreamSize;
            public FileStreamType StreamType;
        }

        #endregion

        #region Utility Methods

        private static int MakeHRFromErrorCode(int errorCode) {
            return (-2147024896 | errorCode);
        }

        private static string GetErrorMessage(int errorCode) {
            var lpBuffer = new StringBuilder(0x200);
            if (0 != Kernel32.FormatMessage(0x3200, IntPtr.Zero, errorCode, 0, lpBuffer, lpBuffer.Capacity, IntPtr.Zero)) {
                return lpBuffer.ToString();
            }

            return string.Format(Resources.Culture, Resources.Error_UnknownError, errorCode);
        }

        private static void ThrowIOError(int errorCode, string path) {
            switch (errorCode) {
                case 0: {
                    break;
                }
                case 2: // File not found
                {
                    if (string.IsNullOrEmpty(path)) {
                        throw new FileNotFoundException();
                    }
                    throw new FileNotFoundException(null, path);
                }
                case 3: // Directory not found
                {
                    if (string.IsNullOrEmpty(path)) {
                        throw new DirectoryNotFoundException();
                    }
                    throw new DirectoryNotFoundException(string.Format(Resources.Culture, Resources.Error_DirectoryNotFound, path));
                }
                case 5: // Access denied
                {
                    if (string.IsNullOrEmpty(path)) {
                        throw new UnauthorizedAccessException();
                    }
                    throw new UnauthorizedAccessException(string.Format(Resources.Culture, Resources.Error_AccessDenied_Path, path));
                }
                case 15: // Drive not found
                {
                    if (string.IsNullOrEmpty(path)) {
                        throw new DriveNotFoundException();
                    }
                    throw new DriveNotFoundException(string.Format(Resources.Culture, Resources.Error_DriveNotFound, path));
                }
                case 32: // Sharing violation
                {
                    if (string.IsNullOrEmpty(path)) {
                        throw new IOException(GetErrorMessage(errorCode), MakeHRFromErrorCode(errorCode));
                    }
                    throw new IOException(string.Format(Resources.Culture, Resources.Error_SharingViolation, path), MakeHRFromErrorCode(errorCode));
                }
                case 80: // File already exists
                {
                    if (!string.IsNullOrEmpty(path)) {
                        throw new IOException(string.Format(Resources.Culture, Resources.Error_FileAlreadyExists, path), MakeHRFromErrorCode(errorCode));
                    }
                    break;
                }
                case 87: // Invalid parameter
                {
                    throw new IOException(GetErrorMessage(errorCode), MakeHRFromErrorCode(errorCode));
                }
                case 183: // File or directory already exists
                {
                    if (!string.IsNullOrEmpty(path)) {
                        throw new IOException(string.Format(Resources.Culture, Resources.Error_AlreadyExists, path), MakeHRFromErrorCode(errorCode));
                    }
                    break;
                }
                case 206: // Path too long
                {
                    throw new PathTooLongException();
                }
                case 995: // Operation canceled
                {
                    throw new OperationCanceledException();
                }
                default: {
                    Marshal.ThrowExceptionForHR(MakeHRFromErrorCode(errorCode));
                    break;
                }
            }
        }

        public static void ThrowLastIOError(string path) {
            var errorCode = Marshal.GetLastWin32Error();
            if (0 != errorCode) {
                var hr = Marshal.GetHRForLastWin32Error();
                if (0 <= hr) {
                    throw new Win32Exception(errorCode);
                }
                ThrowIOError(errorCode, path);
            }
        }

        public static NativeFileAccess ToNative(this FileAccess access) {
            NativeFileAccess result = 0;
            if (FileAccess.Read == (FileAccess.Read & access)) {
                result |= NativeFileAccess.GenericRead;
            }
            if (FileAccess.Write == (FileAccess.Write & access)) {
                result |= NativeFileAccess.GenericWrite;
            }
            return result;
        }

        public static string BuildStreamPath(string filePath, string streamName) {
            var result = filePath;
            if (!string.IsNullOrEmpty(filePath)) {
                if (1 == result.Length) {
                    result = ".\\" + result;
                }
                result += StreamSeparator + streamName + StreamSeparator + "$DATA";
                if (MaxPath <= result.Length) {
                    result = LongPathPrefix + result;
                }
            }
            return result;
        }

        public static void ValidateStreamName(string streamName) {
            if (!string.IsNullOrEmpty(streamName) && -1 != streamName.IndexOfAny(InvalidStreamNameChars)) {
                throw new ArgumentException(Resources.Error_InvalidFileChars);
            }
        }

        public static int SafeGetFileAttributes(string name) {
            if (string.IsNullOrEmpty(name)) {
                throw new ArgumentNullException("name");
            }

            var result = Kernel32.GetFileAttributes(name);
            if (-1 == result) {
                var errorCode = Marshal.GetLastWin32Error();
                if (ErrorFileNotFound != errorCode) {
                    ThrowLastIOError(name);
                }
            }

            return result;
        }

        public static bool SafeDeleteFile(string name) {
            if (string.IsNullOrEmpty(name)) {
                throw new ArgumentNullException("name");
            }

            var result = Kernel32.DeleteFile(name);
            if (!result) {
                var errorCode = Marshal.GetLastWin32Error();
                if (ErrorFileNotFound != errorCode) {
                    ThrowLastIOError(name);
                }
            }

            return result;
        }

        public static SafeFileHandle SafeCreateFile(string path, NativeFileAccess access, FileShare share, IntPtr security, FileMode mode,
            NativeFileAttributesAndFlags flags, IntPtr template) {
            var result = Kernel32.CreateFile(path, access, share, security, mode, flags, template);
            if (!result.IsInvalid && FileType.Disk != Kernel32.GetFileType(result)) {
                result.Dispose();
                throw new NotSupportedException(string.Format(Resources.Culture, Resources.Error_NonFile, path));
            }
            return result;
        }

        private static long GetFileSize(string path, SafeFileHandle handle) {
            var result = 0L;
            if (null != handle && !handle.IsInvalid) {
                long value;
                if (Kernel32.GetFileSizeEx(handle, out value)) {
                    result = value;
                }
                else {
                    ThrowLastIOError(path);
                }
            }

            return result;
        }

        public static long GetFileSize(string path) {
            var result = 0L;
            if (!string.IsNullOrEmpty(path)) {
                using (var handle = SafeCreateFile(path, NativeFileAccess.GenericRead, FileShare.Read, IntPtr.Zero, FileMode.Open, 0, IntPtr.Zero)) {
                    result = GetFileSize(path, handle);
                }
            }

            return result;
        }

        public static IList<Win32StreamInfo> ListStreams(string filePath) {
            if (string.IsNullOrEmpty(filePath)) {
                throw new ArgumentNullException("filePath");
            }
            if (-1 != filePath.IndexOfAny(Path.GetInvalidPathChars())) {
                throw new ArgumentException(Resources.Error_InvalidFileChars, "filePath");
            }

            var result = new List<Win32StreamInfo>();

            using (
                var hFile = SafeCreateFile(filePath, NativeFileAccess.GenericRead, FileShare.Read, IntPtr.Zero, FileMode.Open,
                    NativeFileAttributesAndFlags.BackupSemantics, IntPtr.Zero)) {
                using (var hName = new StreamName()) {
                    if (!hFile.IsInvalid) {
                        var streamId = new Win32StreamId();
                        var dwStreamHeaderSize = Marshal.SizeOf(streamId);
                        var finished = false;
                        var context = IntPtr.Zero;
                        int bytesRead;
                        string name;

                        try {
                            while (!finished) {
                                // Read the next stream header:
                                if (!Kernel32.BackupRead(hFile, ref streamId, dwStreamHeaderSize, out bytesRead, false, false, ref context)) {
                                    finished = true;
                                }
                                else if (dwStreamHeaderSize != bytesRead) {
                                    finished = true;
                                }
                                else {
                                    // Read the stream name:
                                    if (0 >= streamId.StreamNameSize) {
                                        name = null;
                                    }
                                    else {
                                        hName.EnsureCapacity(streamId.StreamNameSize);
                                        if (!Kernel32.BackupRead(hFile, hName.MemoryBlock, streamId.StreamNameSize, out bytesRead, false, false, ref context)) {
                                            name = null;
                                            finished = true;
                                        }
                                        else {
                                            // Unicode chars are 2 bytes:
                                            name = hName.ReadStreamName(bytesRead >> 1);
                                        }
                                    }

                                    // Add the stream info to the result:
                                    if (!string.IsNullOrEmpty(name)) {
                                        result.Add(new Win32StreamInfo {
                                            StreamType = (FileStreamType) streamId.StreamId,
                                            StreamAttributes = (FileStreamAttributes) streamId.StreamAttributes,
                                            StreamSize = streamId.Size,
                                            StreamName = name
                                        });
                                    }

                                    // Skip the contents of the stream:
                                    int bytesSeekedLow,
                                        bytesSeekedHigh;

                                    if (!finished &&
                                        !Kernel32.BackupSeek(hFile, (int) (streamId.Size & 0xffffffff), (int) (streamId.Size >> 32), out bytesSeekedLow,
                                            out bytesSeekedHigh, ref context)) {
                                        finished = true;
                                    }
                                }
                            }
                        }
                        finally {
                            // Abort the backup:
                            Kernel32.BackupRead(hFile, hName.MemoryBlock, 0, out bytesRead, true, false, ref context);
                        }
                    }
                }
            }

            return result;
        }

        #endregion
    }
}