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

namespace CoApp.Toolkit.Engine.Client {
    using System;

    public class ScheduledTask {
        public string Name { get; set; }
        public string Executable { get; set; }
        public string CommandLine { get; set; }
        public int Hour { get; set; }
        public int Minutes { get; set; }
        public DayOfWeek? DayOfWeek { get; set; }
        public int IntervalInMinutes { get; set; }
    }
}