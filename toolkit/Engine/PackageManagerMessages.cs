//-----------------------------------------------------------------------
// <copyright company="CoApp Project">
//     Copyright (c) 2010 Garrett Serack . All rights reserved.
// </copyright>
// <license>
//     The software is licensed under the Apache 2.0 License (the "License")
//     You may not use the software except in compliance with the License. 
// </license>
//-----------------------------------------------------------------------

namespace CoApp.Toolkit.Engine {
    using System;
    using System.Collections.Generic;
    using Tasks;

#if COAPP_ENGINE_CLIENT 
    using Client;
#endif 

    public class PackageManagerMessages : MessageHandlers<PackageManagerMessages> {


#if COAPP_ENGINE_CORE 
        public Action<Package, IEnumerable<Package>> PackageInformation;
#else
        public Action<Package> PackageInformation;
        /// <summary>
        /// policy name, description ,accounts
        /// </summary>
        public Action<string,string, IEnumerable<string>> PolicyInformation;

        public Action<bool, bool, bool> LoggingSettings;
#endif
        public Action<Package> PackageDetails;
        public Action NoPackagesFound;
        /// <summary>
        /// location, lastScanned, isSession, isSuppressed, isValidated
        /// </summary>
        public Action<string, DateTime, bool, bool, bool> FeedDetails;
       
        /// <summary>
        /// canonicalName, current package progress, overallProgress of all packages
        /// </summary>
        public Action<string, int, int> InstallingPackageProgress;

        /// <summary>
        /// canonicalName, current package progress
        /// </summary>
        public Action<string, int> RemovingPackageProgress;
        /// <summary>
        /// canonicalName
        /// </summary>
        public Action<string> InstalledPackage;
        /// <summary>
        /// canonicalName
        /// </summary>
        public Action<string> RemovedPackage;
        /// <summary>
        /// canonicalName, filename, reason
        /// </summary>
        public Action<string,string,string> FailedPackageInstall;

        /// <summary>
        /// canonicalName, reason
        /// </summary>
        public Action<string,string> FailedPackageRemoval;
        /// <summary>
        /// canonicalName, remoteLocations, localFolder, force
        /// </summary>
        public Action<string, IEnumerable<string>, string, bool> RequireRemoteFile;
        /// <summary>
        /// filename, isValid, certificate subject name
        /// </summary>
        public Action<string, bool, string> SignatureValidation;

        /// <summary>
        /// policyName
        /// </summary>
        public Action<string> PermissionRequired;

        /// <summary>
        /// string arg1, string arg2, string arg3
        /// </summary>
        public Action<string, string, string > Error;
        /// <summary>
        /// string arg1, string arg2, string arg3
        /// </summary>
        public Action<string, string, string> Warning;
        /// <summary>
        /// feed location
        /// </summary>
        public Action<string> FeedAdded;
        /// <summary>
        /// feed location
        /// </summary>
        public Action<string> FeedRemoved;
        /// <summary>
        /// feed location
        /// </summary>
        public Action<string> FeedSuppressed;
        public Action NoFeedsFound;
        /// <summary>
        /// file path
        /// </summary>
        public Action<string> FileNotFound;
        /// <summary>
        /// canonical name
        /// </summary>
        public Action<string> UnknownPackage;
        /// <summary>
        /// canonical name
        /// </summary>
        public Action<string> PackageBlocked;
        /// <summary>
        /// filename, reason
        /// </summary>
        public Action<string,string> FileNotRecognized;
        public Action<string> Recognized;

        /// <summary>
        /// not used
        /// </summary>
        public Action<string> OperationCanceled;
        
        /// <summary>
        /// 
        /// </summary>
        public Action<Exception> UnexpectedFailure;
     
        public Action<Package, IEnumerable<Package>> PackageHasPotentialUpgrades;
        public Action<Package> UnableToDownloadPackage;
        public Action<Package> UnableToInstallPackage;
        public Action<Package, IEnumerable<Package>> UnableToResolveDependencies;

        /// <summary>
        /// original, satisfied by
        /// </summary>
        public Action<Package,Package> PackageSatisfiedBy;

        public Action<bool> CurrentTelemetryOption;

        public Action Restarting;

        public Action<string, string, string, int, int, DayOfWeek?, int> ScheduledTaskInfo;

        public string RequestId;
    }
}