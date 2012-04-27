//-----------------------------------------------------------------------
// <copyright company="CoApp Project">
//     Changes Copyright (c) 2011 Garrett Serack . All rights reserved.
//     TaskScheduler Original Code from http://taskscheduler.codeplex.com/
// </copyright>
// <license>
//     The software is licensed under the Apache 2.0 License (the "License")
//     You may not use the software except in compliance with the License. 
// </license>
//-----------------------------------------------------------------------

// -----------------------------------------------------------------------
// Original Code: 
// * Copyright (c) 2003-2011 David Hall
// Permission is hereby granted, free of charge, to any person obtaining a
// copy of this software and associated documentation files (the "Software"), 
// to deal in the Software without restriction, including without limitation
// the rights to use, copy, modify, merge, publish, distribute, sublicense, 
// and/or sell copies of the Software, and to permit persons to whom the 
// Software is furnished to do so, subject to the following conditions:

// The above copyright notice and this permission notice shall be included 
// in all copies or substantial portions of the Software.

// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS
// OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL 
// THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR 
// OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, 
// ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR 
// OTHER DEALINGS IN THE SOFTWARE.

namespace CoApp.Toolkit.TaskService {
    using System;
    using System.ComponentModel;
    using System.Runtime.InteropServices;
    using System.Security.Principal;

    /// <summary>
    ///   Impersonation of a user. Allows to execute code under another user context. Please note that the account that instantiates the Impersonator class needs to have the 'Act as part of operating system' privilege set.
    /// </summary>
    internal class WindowsImpersonatedIdentity : IDisposable, IIdentity {
        private readonly WindowsImpersonationContext impersonationContext;
        private readonly WindowsIdentity identity;

        /// <summary>
        ///   Constructor. Starts the impersonation with the given credentials. Please note that the account that instantiates the Impersonator class needs to have the 'Act as part of operating system' privilege set.
        /// </summary>
        /// <param name="userName"> The name of the user to act as. </param>
        /// <param name="domainName"> The domain name of the user to act as. </param>
        /// <param name="password"> The password of the user to act as. </param>
        public WindowsImpersonatedIdentity(string userName, string domainName, string password) {
            var token = IntPtr.Zero;
            var tokenDuplicate = IntPtr.Zero;
            try {
                if (string.IsNullOrEmpty(userName) && string.IsNullOrEmpty(domainName) && string.IsNullOrEmpty(password)) {
                    identity = WindowsIdentity.GetCurrent();
                } else {
                    if (LogonUser(userName, domainName, password, LOGON32_LOGON_INTERACTIVE, LOGON32_PROVIDER_DEFAULT, ref token) != 0) {
                        if (DuplicateToken(token, 2, ref tokenDuplicate) != 0) {
                            identity = new WindowsIdentity(tokenDuplicate);
                            impersonationContext = identity.Impersonate();
                        } else {
                            throw new Win32Exception(Marshal.GetLastWin32Error());
                        }
                    } else {
                        throw new Win32Exception(Marshal.GetLastWin32Error());
                    }
                }
            } finally {
                if (token != IntPtr.Zero) {
                    CloseHandle(token);
                }
                if (tokenDuplicate != IntPtr.Zero) {
                    CloseHandle(tokenDuplicate);
                }
            }
        }

        public void Dispose() {
            if (impersonationContext != null) {
                impersonationContext.Undo();
            }
            if (identity != null) {
                identity.Dispose();
            }
        }

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern int LogonUser(string lpszUserName, string lpszDomain, string lpszPassword,
            int dwLogonType, int dwLogonProvider, ref IntPtr phToken);

        [DllImport("advapi32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern int DuplicateToken(IntPtr hToken, int impersonationLevel, ref IntPtr hNewToken);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
        private static extern bool CloseHandle(IntPtr handle);

        private const int LOGON32_LOGON_INTERACTIVE = 2;
        private const int LOGON32_PROVIDER_DEFAULT = 0;

        public string AuthenticationType {
            get {
                return identity == null ? null : identity.AuthenticationType;
            }
        }

        public bool IsAuthenticated {
            get {
                return identity == null ? false : identity.IsAuthenticated;
            }
        }

        public string Name {
            get {
                return identity == null ? null : identity.Name;
            }
        }
    }
}