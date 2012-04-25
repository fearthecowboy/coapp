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

    public class Feed {
        public string Location { get; internal set; }
        public DateTime LastScanned { get; internal set; }
        public bool IsSession { get; internal set; }
        public bool IsSuppressed { get; internal set; }
        public FeedState FeedState { get; internal set; }
    }
}