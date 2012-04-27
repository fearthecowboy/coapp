//-----------------------------------------------------------------------
// <copyright company="CoApp Project">
//     Copyright (c) 2010-2012 Garrett Serack and CoApp Contributors. 
//     Contributors can be discovered using the 'git log' command.
//     All rights reserved.
// </copyright>
// <license>
//     The software is licensed under the Apache 2.0 License (the "License")
//     You may not use the software except in compliance with the License. 
// </license>
//-----------------------------------------------------------------------

namespace CoApp.Packaging.Common {
    using System;

    [Flags]
    internal enum SERVICE_ACCESS {
        SERVICE_INTERROGATE = 0x0080,
        SERVICE_PAUSE_CONTINUE = 0x0040,
        SERVICE_QUERY_CONFIG = 0x0001,
        SERVICE_QUERY_STATUS = 0x0004,
        SERVICE_START = 0x0010,
        SERVICE_STOP = 0x0020,
        SERVICE_COAPP = SERVICE_INTERROGATE | SERVICE_PAUSE_CONTINUE | SERVICE_QUERY_CONFIG | SERVICE_QUERY_STATUS | SERVICE_START | SERVICE_STOP
    }
}