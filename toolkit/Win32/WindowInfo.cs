﻿//-----------------------------------------------------------------------
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

    public struct WindowInfo {
        public Int16 atomWindowType;
        public Int32 cbSize;
        public Int32 cxWindowBorders;
        public Int32 cyWindowBorders;
        public Int32 dwExStyle;
        public Int32 dwStyle;
        public Int32 dwWindowStatus;
        public Rect rcClient;
        public Rect rcWindow;
        public Int16 wCreatorVersion;
    }
}