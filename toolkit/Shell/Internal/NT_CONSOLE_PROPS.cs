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
    internal struct NT_CONSOLE_PROPS {
        public const UInt32 NT_CONSOLE_PROPS_SIG = 0xA0000002;

        /// <summary>
        ///   Gets an empty structure with a valid data block header and sensible defaults.
        /// </summary>
        public static NT_CONSOLE_PROPS AnEmptyOne() {
            var value = new NT_CONSOLE_PROPS();

            value.SetDataBlockHeader();

            value.wFillAttribute = 15;
            value.wPopupFillAttribute = 245;
            value.dwScreenBufferSize.X = 80;
            value.dwScreenBufferSize.Y = 300;
            value.dwWindowSize.X = 80;
            value.dwWindowSize.Y = 25;
            value.dwWindowOrigin.X = 0;
            value.dwWindowOrigin.Y = 0;
            value.nFont = 12;
            value.nInputBufferSize = 0;
            value.dwFontSize = new COORD(0, 12);
            value.uFontFamily = 54;
            value.uFontWeight = 400;
            value.FaceName = new char[32] {
                'C', 'o', 'n', 's', 'o', 'l', 'a', 's', '\0', '\0',
                '\0', '\0', '\0', '\0', '\0', '\0', '\0', '\0', '\0', '\0',
                '\0', '\0', '\0', '\0', '\0', '\0', '\0', '\0', '\0', '\0',
                '\0', '\0'
            };
            value.uCursorSize = 25;
            value.bFullScreen = false;
            value.bQuickEdit = false;
            value.bInsertMode = true;
            value.bAutoPosition = true;
            value.uHistoryBufferSize = 50;
            value.uNumberOfHistoryBuffers = 4;
            value.bHistoryNoDup = false;

            value.ColorTable = new UInt32[16] {
                0x0, // Black - 0
                0x800000, // Dark Blue - 8388608
                0x8000, // Dark Green - 32768
                0x808000, // Teal - 8421376
                0x80, // Dark Red - 128
                0x800080, // Dark Purple - 8388736
                0x8080, // Olive- 32896
                0xC0C0C0, // Light Grey - 12632256
                0x808080, // Dark Grey - 8421504
                0xFF0000, // Blue - 16711680
                0xFF00, // Light Green - 65280
                0xFFFF00, // Cyan -16776960
                0xFF, // Red - 255
                0xFF00FF, // Chartruse - 16711935
                0xFFFF, // Yellow - 65535
                0xFFFFFF // White - 16777215
            };

            return value;
        }

        /// <summary>
        ///   Sets the datablock header values for this sturcture.
        /// </summary>
        public void SetDataBlockHeader() {
            this.dbh.cbSize = unchecked((UInt32)Marshal.SizeOf(typeof (NT_CONSOLE_PROPS)));
            this.dbh.dwSignature = NT_CONSOLE_PROPS_SIG;
        }

        public DATABLOCK_HEADER dbh;

        public UInt16 wFillAttribute;
        public UInt16 wPopupFillAttribute;
        public COORD dwScreenBufferSize;
        public COORD dwWindowSize;
        public COORD dwWindowOrigin;
        public UInt32 nFont;
        public UInt32 nInputBufferSize;
        public COORD dwFontSize;
        public UInt32 uFontFamily;
        public UInt32 uFontWeight;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
        public char[] FaceName;

        public UInt32 uCursorSize;

        [MarshalAs(UnmanagedType.Bool)]
        public bool bFullScreen;

        [MarshalAs(UnmanagedType.Bool)]
        public bool bQuickEdit;

        [MarshalAs(UnmanagedType.Bool)]
        public bool bInsertMode;

        [MarshalAs(UnmanagedType.Bool)]
        public bool bAutoPosition;

        public UInt32 uHistoryBufferSize;
        public UInt32 uNumberOfHistoryBuffers;

        [MarshalAs(UnmanagedType.Bool)]
        public bool bHistoryNoDup;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
        public UInt32[] ColorTable;
    }
}