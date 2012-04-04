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

namespace CoApp.Toolkit.Exceptions {
    public class FailedPackageRemoveException : CoAppException {
        public string CanonicalName { get; internal set; }
        public string Reason { get; internal set; }
        public FailedPackageRemoveException (string canonicalName, string reason, params object[] args) {
            CanonicalName = canonicalName;
            Reason = string.Format(reason, args);
        }
    }

    public class PackageBlockedException : CoAppException {
        public string CanonicalName { get; internal set; }

        public PackageBlockedException(string canonicalName) {
            CanonicalName = canonicalName;
        }
    }

    public class UnknownPackageException : CoAppException {
        public string CanonicalName { get; internal set; }

        public UnknownPackageException(string canonicalName) {
            CanonicalName = canonicalName;
        }
    }

    public class RequiresPermissionException : CoAppException {
        public string PolicyName { get; internal set; }

        public RequiresPermissionException(string policyName) {
            PolicyName = policyName;
        }
    }

    /* public class OperationCanceledException : CoAppException {
        public string Reason { get; internal set; }

        public OperationCanceledException(string reason) {
            Reason = reason;
        }
    }
    */

    public class InvalidCanonicalNameException : CoAppException {
        public string CanonicalName { get; internal set; }

        public InvalidCanonicalNameException(string canonicalName) {
            CanonicalName = canonicalName;
        }
    }

}