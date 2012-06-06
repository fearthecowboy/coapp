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

namespace CoApp.Packaging.Client {
    using System.Collections.Generic;

    public class Policy {
        public string Name { get; internal set; }
        public string Description { get; internal set; }
        public IEnumerable<string> Members { get; internal set; }
        public bool IsEnabled;
    }
}