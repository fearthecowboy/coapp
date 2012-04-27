//-----------------------------------------------------------------------
// <copyright company="CoApp Project">
//     Copyright (c) 2011 Garrett Serack. All rights reserved.
// </copyright>
// <license>
//     The software is licensed under the Apache 2.0 License (the "License")
//     You may not use the software except in compliance with the License. 
// </license>
//-----------------------------------------------------------------------

namespace CoApp.Packaging.Common.Exceptions {
    using CoApp.Toolkit.Extensions;
    using CoApp.Toolkit.Exceptions;

    public class UnableToStartServiceException : CoAppException {
        public string Reason;
        
        public UnableToStartServiceException( string reason ) :base("Unable to start service: {0}".format(reason)) {
            Reason = reason;
        }
    }

    public class UnableToStopServiceException : System.Exception {
        public string Reason;

        public UnableToStopServiceException(string reason)
            : base("Unable to start service: {0}".format(reason)) {
            Reason = reason;
        }
    }
}