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
    using Microsoft.Win32.SafeHandles;

    public enum CertSectionType : uint {
        None = 0,
        Any = 0xff
    }

    public class ImageHlp {
        [DllImport("imagehlp.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool ImageRemoveCertificate(SafeFileHandle fileHandle, uint index);

        [DllImport("Imagehlp.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool ImageEnumerateCertificates(SafeFileHandle fileHandle, CertSectionType wTypeFilter, out uint dwCertCount, IntPtr pIndices, int pIndexCount);
    }
}