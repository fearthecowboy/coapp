//-----------------------------------------------------------------------
// <copyright company="CoApp Project">
//     Copyright (c) 2010 Garrett Serack . All rights reserved.
// </copyright>
// <license>
//     The software is licensed under the Apache 2.0 License (the "License")
//     You may not use the software except in compliance with the License. 
// </license>
//-----------------------------------------------------------------------

namespace CoApp.Packaging.Common {
    using System;
    using System.Collections.Generic;
    using Toolkit.ImpromptuInterface.Dynamic;
    using Toolkit.Win32;

    [UseNamedArgument]
    public interface IPackageManagerResponse {
        void NoPackagesFound();
        void PolicyInformation(string name, string description, IEnumerable<string> accounts);
        void SendSessionStarted(string sessionId);

        void PackageInformation(CanonicalName canonicalName, string localLocation, bool installed,
            bool blocked, bool required, bool clientRequired, bool active, bool dependent, FourPartVersion minPolicy, FourPartVersion maxPolicy, IEnumerable<Uri> remoteLocations, IEnumerable<Uri> feeds,
            IEnumerable<CanonicalName> dependencies, IEnumerable<CanonicalName> supercedentPackages);

        void PackageDetails(CanonicalName canonicalName, IDictionary<string, string> metadata,
            IEnumerable<string> iconLocations, IDictionary<string, string> licenses, IDictionary<string, string> roles,
            IEnumerable<string> tags, IDictionary<string, string> contributorUrls, IDictionary<string, string> contributorEmails);

        void FeedDetails(string location, DateTime lastScanned, bool session, bool suppressed, bool validated, string state);
        void FeedAdded(string location);
        void FeedRemoved(string location);
        void FeedSuppressed(string location);
        void NoFeedsFound();

        void InstallingPackageProgress(CanonicalName canonicalName, int percentComplete, int overallProgress);
        void InstalledPackage(CanonicalName canonicalName);
        void RemovingPackageProgress(CanonicalName canonicalName, int percentComplete);
        void RemovedPackage(CanonicalName canonicalName);
        void FailedPackageInstall(CanonicalName canonicalName, string filename, string reason);
        void FailedPackageRemoval(CanonicalName canonicalName, string reason);
        void PackageSatisfiedBy(string requestedCanonicalName, string satisfiedByCanonicalName);
        void PackageHasPotentialUpgrades(CanonicalName packageCanonicalName, IEnumerable<CanonicalName> supercedents);

        void RequireRemoteFile(string requestReference, IEnumerable<Uri> remoteLocations, string destination, bool force);
        void Recognized(string location);

        void SignatureValidation(string filename, bool isValid, string certificateSubjectName);
        
        void PermissionRequired(string policyRequired);
        void Error(string messageName, string argumentName, string problem);
        void Warning(string messageName, string argumentName, string problem);
        void FileNotFound(string filename);
        void UnknownPackage(CanonicalName canonicalName);
        void PackageBlocked(CanonicalName canonicalName);
        void FileNotRecognized(string filename, string reason);
        void UnexpectedFailure(string type, string failure, string stacktrace);
        void UnableToDownloadPackage(CanonicalName packageCanonicalName);
        void UnableToInstallPackage(CanonicalName packageCanonicalName);

        void SendKeepAlive();
        void OperationCanceled(string message);
        void ScheduledTaskInfo(string taskName, string executable, string commandline, int hour, int minutes, DayOfWeek? dayOfWeek, int intervalInMinutes);
        void CurrentTelemetryOption(bool optIntoTelemetryTracking);

        void Restarting();
        void SendShuttingDown();
        void LoggingSettings(bool messages, bool warnings, bool errors);

        void TaskComplete();
    }
}