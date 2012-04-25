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
    using ImpromptuInterface.Dynamic;
    using Win32;

    [UseNamedArgument] 
    public interface IPackageManagerResponse {
        void NoPackagesFound();
        void PolicyInformation(string name, string description, IEnumerable<string> accounts ) ;
        void SendSessionStarted(string sessionId);
        void PackageInformation(string canonicalName, string localLocation, string name, FourPartVersion version, Architecture architecture, string publicKeyToken, bool installed,
            bool blocked, bool required, bool clientRequired, bool active, bool dependent, FourPartVersion minPolicy, FourPartVersion maxPolicy, IEnumerable<string> remoteLocations,
            IEnumerable<string> dependencies, IEnumerable<string> supercedentPackages) ;

        void PackageDetails(string canonicalName, Dictionary<string, string> metadata,
            IEnumerable<string> iconLocations, Dictionary<string, string> licenses, Dictionary<string, string> roles,
            IEnumerable<string> tags, IDictionary<string, string> contributorUrls, IDictionary<string, string> contributorEmails);

        void FeedDetails(string location, DateTime lastScanned, bool session, bool suppressed, bool validated, string state);
        void InstallingPackageProgress(string canonicalName, int percentComplete, int overallProgress) ;
        void RemovingPackageProgress(string canonicalName, int percentComplete) ;
        void InstalledPackage(string canonicalName) ;
        void RemovedPackage(string canonicalName) ;
        void FailedPackageInstall(string canonicalName, string filename, string reason) ;
        void FailedPackageRemoval(string canonicalName, string reason) ;
        void RequireRemoteFile(string canonicalName, IEnumerable<string> remoteLocations, string destination, bool force) ;
        void SignatureValidation(string filename, bool isValid, string certificateSubjectName) ;
        void PermissionRequired(string policyRequired) ;
        void Error(string messageName, string argumentName, string problem) ;
        void Warning(string messageName, string argumentName, string problem) ;
        void PackageSatisfiedBy(string requestedCanonicalName, string satisfiedByCanonicalName ) ;
        void FeedAdded(string location) ;
        void FeedRemoved(string location) ;
        void FileNotFound(string filename) ;
        void UnknownPackage(string canonicalName) ;
        void PackageBlocked(string canonicalName) ;
        void FileNotRecognized(string filename, string reason) ;
        void UnexpectedFailure(string type, string failure, string stacktrace);
        void FeedSuppressed(string location) ;
        void SendKeepAlive() ;
        void OperationCanceled(string message) ;
        void PackageHasPotentialUpgrades(string packageCanonicalName, IEnumerable<string> supercedents) ;
        void ScheduledTaskInfo(string taskName, string executable, string commandline, int hour, int minutes, DayOfWeek? dayOfWeek, int intervalInMinutes) ;
        void CurrentTelemetryOption(bool optIntoTelemetryTracking) ;
        void NoFeedsFound() ;
        void Restarting() ;
        void SendShuttingDown() ;
        void UnableToDownloadPackage(string packageCanonicalName) ;
        void UnableToInstallPackage(string packageCanonicalName) ;
        void Recognized(string location) ;
        void TaskComplete();
        void LoggingSettings(bool messages, bool warnings, bool errors);
    }
}