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
    using System.Runtime.InteropServices;

    /// <summary>
    ///   The structure represents a security identifier (SID) and its attributes. SIDs are used to uniquely identify users or groups.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct SidAndAttributes {
        public IntPtr Sid;
        public Int32 Attributes;
    }
}