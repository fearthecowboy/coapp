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
    ///   Native function calls using NTDLL
    /// </summary>
    public static class Ntdll {
        [DllImport("ntdll.dll", CallingConvention = CallingConvention.StdCall)]
        public static extern UInt32 RtlAcquirePrivilege(ref UInt32 Privilege, UInt32 NumPriv, UInt32 Flags, ref IntPtr ReturnedState);

        [DllImport("ntdll.dll", CallingConvention = CallingConvention.StdCall)]
        public static extern UInt32 RtlReleasePrivilege(IntPtr ReturnedState);

        [DllImport("ntdll.dll")]
        //public static extern UInt32 RtlCreateUserThread(SafeProcessHandle processHandle, IntPtr lpThreadSecurity, bool createSuspended, int stackZeroBits, IntPtr stackReserved, IntPtr stackCommit, IntPtr startAddress, IntPtr parameter, out uint threadId, out IntPtr clientId);
        public static extern UInt32 RtlCreateUserThread(SafeProcessHandle processHandle, IntPtr lpThreadSecurity, bool createSuspended, int stackZeroBits, IntPtr stackReserved, IntPtr stackCommit, IntPtr startAddress, IntPtr parameter, out uint threadId,
            IntPtr clientId);
    }
}