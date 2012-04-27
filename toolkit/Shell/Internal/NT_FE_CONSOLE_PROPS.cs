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
    internal struct NT_FE_CONSOLE_PROPS {
        public const UInt32 NT_FE_CONSOLE_PROPS_SIG = 0xA0000004;

        /// <summary>
        ///   Gets an empty structure with a valid data block header and sensible defaults.
        /// </summary>
        /// <returns> </returns>
        public static NT_FE_CONSOLE_PROPS AnEmptyOne() {
            var value = new NT_FE_CONSOLE_PROPS();

            value.SetDataBlockHeader();

            value.uCodePage = 0;

            return value;
        }

        /// <summary>
        ///   Sets the datablock header values for this sturcture.
        /// </summary>
        public void SetDataBlockHeader() {
            this.dbh.cbSize = unchecked((UInt32)Marshal.SizeOf(typeof (NT_FE_CONSOLE_PROPS)));
            this.dbh.dwSignature = NT_FE_CONSOLE_PROPS_SIG;
        }

        public DATABLOCK_HEADER dbh;

        public UInt32 uCodePage;
    }
}