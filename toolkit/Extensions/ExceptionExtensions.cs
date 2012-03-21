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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CoApp.Toolkit.Extensions {
    public static class ExceptionExtensions {
        public static Exception Unwrap(this Exception exception) {
            var aggregate = exception as AggregateException;
            if( aggregate != null ) {
                var result = aggregate.Flatten().InnerExceptions.FirstOrDefault();
                return result is AggregateException ? result.Unwrap() : result;
            }
            return exception;
        }
    }
}
