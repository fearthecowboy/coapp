//-----------------------------------------------------------------------
// <copyright company="CoApp Project">
//     Original (c) Author: Richard G Russell (Foredecker) 
//     Changes Copyright (c) 2010  Garrett Serack, CoApp Contributors. All rights reserved.
// </copyright>
// <license>
//     The software is licensed under the Apache 2.0 License (the "License")
//     You may not use the software except in compliance with the License. 
// </license>
//-----------------------------------------------------------------------

// -----------------------------------------------------------------------
// Original Code: 
// (c) Author: Richard G Russell (Foredecker) 
// This code is licensed under the MS-PL
// http://www.opensource.org/licenses/ms-pl.html
// -----------------------------------------------------------------------

namespace CoApp.Toolkit.Shell.Internal {
    using System;
    using System.Runtime.InteropServices;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct EXP_SZ_LINK {
        public const UInt32 EXP_SZ_LINK_SIG = 0xA0000001; // LPEXP_SZ_LINK (target)
        public const int MAX_PATH = 260;

        /// <summary>
        ///   Gets an empty structure with a valid data block header and sensible defaults.
        /// </summary>
        public static EXP_SZ_LINK AnEmptyOne() {
            var value = new EXP_SZ_LINK();

            value.SetDataBlockHeader();

            value.szTarget = new sbyte[MAX_PATH];
            value.swzTarget = new char[MAX_PATH];

            return value;
        }

        /// <summary>
        ///   Sets the datablock header values for this sturcture.
        /// </summary>
        public void SetDataBlockHeader() {
            this.dbh.cbSize = unchecked((UInt32)Marshal.SizeOf(typeof (EXP_SZ_LINK)));
            this.dbh.dwSignature = EXP_SZ_LINK_SIG;
        }

        public DATABLOCK_HEADER dbh;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = MAX_PATH)]
        public sbyte[] szTarget; // ANSI target name w/EXP_SZ in it

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = MAX_PATH)]
        public char[] swzTarget; // UNICODE target name w/EXP_SZ in it
    }
}