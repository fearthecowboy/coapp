//-----------------------------------------------------------------------
// <copyright company="CoApp Project">
//     ResourceLib Original Code from http://resourcelib.codeplex.com
//     Original Copyright (c) 2008-2009 Vestris Inc.
//     Changes Copyright (c) 2011 Garrett Serack . All rights reserved.
// </copyright>
// <license>
// MIT License
// You may freely use and distribute this software under the terms of the following license agreement.
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated 
// documentation files (the "Software"), to deal in the Software without restriction, including without limitation 
// the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and 
// to permit persons to whom the Software is furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in all copies or substantial portions of 
// the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO 
// THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE 
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, 
// TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE
// </license>
//-----------------------------------------------------------------------

namespace CoApp.Toolkit.Win32 {
    using System;

    /// <summary>
    ///   Kernel32.dll interop functions.
    /// </summary>
    public abstract class Kernel32Contants {
        /// <summary>
        ///   If this value is used, the system maps the file into the calling process's virtual address space as if it were a data file.
        /// </summary>
        public const uint LOAD_LIBRARY_AS_DATAFILE = 0x00000002;

        /// <summary>
        ///   If this value is used, and the executable module is a DLL, the system does not call DllMain for process and thread initialization and termination.
        /// </summary>
        public const uint DONT_RESOLVE_DLL_REFERENCES = 0x00000001;

        /// <summary>
        ///   If this value is used and lpFileName specifies an absolute path, the system uses the alternate file search strategy.
        /// </summary>
        public const uint LOAD_WITH_ALTERED_SEARCH_PATH = 0x00000008;

        /// <summary>
        ///   If this value is used, the system does not perform automatic trust comparisons on the DLL or its dependents when they are loaded.
        /// </summary>
        public const uint LOAD_IGNORE_CODE_AUTHZ_LEVEL = 0x00000010;

        /// <summary>
        ///   Neutral primary language ID.
        /// </summary>
        public const UInt16 LANG_NEUTRAL = 0;

        /// <summary>
        ///   US-English primary language ID.
        /// </summary>
        public const UInt16 LANG_ENGLISH = 9;

        /// <summary>
        ///   Neutral sublanguage ID.
        /// </summary>
        public const UInt16 SUBLANG_NEUTRAL = 0;

        /// <summary>
        ///   US-English sublanguage ID.
        /// </summary>
        public const UInt16 SUBLANG_ENGLISH_US = 1;

        /// <summary>
        ///   CREATEPROCESS_MANIFEST_RESOURCE_ID is used primarily for EXEs. If an executable has a resource of type RT_MANIFEST, ID CREATEPROCESS_MANIFEST_RESOURCE_ID, Windows will create a process default activation context for the process. The process default activation context will be used by all components running in the process. CREATEPROCESS_MANIFEST_RESOURCE_ID can also used by DLLs. When Windows probe for dependencies, if the dll has a resource of type RT_MANIFEST, ID CREATEPROCESS_MANIFEST_RESOURCE_ID, Windows will use that manifest as the dependency.
        /// </summary>
        public const UInt16 CREATEPROCESS_MANIFEST_RESOURCE_ID = 1;

        /// <summary>
        ///   ISOLATIONAWARE_MANIFEST_RESOURCE_ID is used primarily for DLLs. It should be used if the dll wants private dependencies other than the process default. For example, if an dll depends on comctl32.dll version 6.0.0.0. It should have a resource of type RT_MANIFEST, ID ISOLATIONAWARE_MANIFEST_RESOURCE_ID to depend on comctl32.dll version 6.0.0.0, so that even if the process executable wants comctl32.dll version 5.1, the dll itself will still use the right version of comctl32.dll.
        /// </summary>
        public const UInt16 ISOLATIONAWARE_MANIFEST_RESOURCE_ID = 2;

        /// <summary>
        ///   When ISOLATION_AWARE_ENABLED is defined, Windows re-defines certain APIs. For example LoadLibraryExW is redefined to IsolationAwareLoadLibraryExW.
        /// </summary>
        public const UInt16 ISOLATIONAWARE_NOSTATICIMPORT_MANIFEST_RESOURCE_ID = 3;
    }
}