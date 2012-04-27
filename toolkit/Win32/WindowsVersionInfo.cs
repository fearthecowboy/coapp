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

    public enum ProcessorType {
        Unknown,
        X86,
        X64,
        Arm
    }

    /// <summary>
    ///   Wrapper class for accessing information about the current Windows environment
    /// </summary>
    /// <remarks>
    /// </remarks>
    public static class WindowsVersionInfo {
        /// <summary>
        ///   Gets a value indicating whether this instance is vista or beyond.
        /// </summary>
        /// <remarks>
        /// </remarks>
        public static bool IsVistaOrBeyond {
            get {
                return Environment.OSVersion.Version.Major > 5;
            }
        }

        public static bool IsVistaOrPrior {
            get {
                return Environment.OSVersion.Version.Major < 6 || (Environment.OSVersion.Version.Major == 6 && Environment.OSVersion.Version.Minor == 0);
            }
        }

        /// <summary>
        ///   Gets a value indicating whether this process is running as a 32 bit process.
        /// </summary>
        /// <remarks>
        /// </remarks>
        public static bool IsCurrentProcess32Bit {
            get {
                return !Environment.Is64BitProcess;
            }
        }

        /// <summary>
        ///   Gets a value indicating whether this process is running as a 64 bit process.
        /// </summary>
        /// <remarks>
        /// </remarks>
        public static bool IsCurrentProcess64Bit {
            get {
                return Environment.Is64BitProcess;
            }
        }

        /// <summary>
        ///   Gets a value indicating whether the current OS is 32 bit
        /// </summary>
        /// <remarks>
        /// </remarks>
        public static bool IsOS32Bit {
            get {
                return !Environment.Is64BitOperatingSystem;
            }
        }

        /// <summary>
        ///   Gets a value indicating whether the current OS is 64 bit
        /// </summary>
        /// <remarks>
        /// </remarks>
        public static bool IsOS64Bit {
            get {
                return Environment.Is64BitOperatingSystem;
            }
        }

        public static ProcessorType ProcessorType {
            get {
                // TODO: needs to be updated with proper check for arm on 32bit 
                if (IsOS64Bit) {
                    return ProcessorType.X64;
                }

                return ProcessorType.X86;
            }
        }
    }
}