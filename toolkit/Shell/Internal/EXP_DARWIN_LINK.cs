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
    internal struct EXP_DARWIN_LINK {
        public const UInt32 EXP_DARWIN_ID_SIG = 0xA0000006;
        public const int MAX_PATH = 260;

        /// <summary>
        ///   Gets an empty structure with a valid data block header and sensible defaults.
        /// </summary>
        public static EXP_DARWIN_LINK AnEmptyOne() {
            var value = new EXP_DARWIN_LINK();

            value.SetDataBlockHeader();

            value.szDarwinID = new sbyte[MAX_PATH];
            value.szwDarwinID = new char[MAX_PATH];

            return value;
        }

        /// <summary>
        ///   Sets the datablock header values for this sturcture.
        /// </summary>
        public void SetDataBlockHeader() {
            this.dbh.cbSize = unchecked((UInt32)Marshal.SizeOf(typeof (EXP_DARWIN_LINK)));
            this.dbh.dwSignature = EXP_DARWIN_ID_SIG;
        }

        public DATABLOCK_HEADER dbh;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public sbyte[] szDarwinID; // ANSI darwin ID associated with link

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public char[] szwDarwinID; // UNICODE darwin ID associated with link
    }
}