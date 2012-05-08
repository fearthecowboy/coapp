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
    internal enum ServiceAccess {
        ServiceInterrogate = 0x0080,
        ServicePauseContinue = 0x0040,
        ServiceQueryConfig = 0x0001,
        ServiceQueryStatus = 0x0004,
        ServiceStart = 0x0010,
        ServiceStop = 0x0020,
        ServiceCoapp = ServiceInterrogate | ServicePauseContinue | ServiceQueryConfig | ServiceQueryStatus | ServiceStart | ServiceStop
    }
}