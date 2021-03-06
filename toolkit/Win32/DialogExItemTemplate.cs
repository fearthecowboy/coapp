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
    using System.Runtime.InteropServices;

    /// <summary>
    ///   A control entry in an extended dialog template.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct DialogExItemTemplate {
        /// <summary>
        ///   Specifies the help context identifier for the dialog box window. When the system sends a WM_HELP message, it passes this value in the wContextId member of the HELPINFO structure.
        /// </summary>
        public UInt32 helpID;

        /// <summary>
        ///   Specifies extended windows styles.
        /// </summary>
        public UInt32 exStyle;

        /// <summary>
        ///   Specifies the style of the dialog box.
        /// </summary>
        public UInt32 style;

        /// <summary>
        ///   Specifies the x-coordinate, in dialog box units, of the upper-left corner of the dialog box.
        /// </summary>
        public Int16 x;

        /// <summary>
        ///   Specifies the y-coordinate, in dialog box units, of the upper-left corner of the dialog box.
        /// </summary>
        public Int16 y;

        /// <summary>
        ///   Specifies the width, in dialog box units, of the dialog box.
        /// </summary>
        public Int16 cx;

        /// <summary>
        ///   Specifies the height, in dialog box units, of the dialog box.
        /// </summary>
        public Int16 cy;

        /// <summary>
        ///   Specifies the control identifier.
        /// </summary>
        public Int32 id;
    }
}