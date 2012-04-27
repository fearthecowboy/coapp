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
    using System.Threading.Tasks;
    using CoApp.Toolkit.ImpromptuInterface.Dynamic;

    [UseNamedArgument]
    public interface IPackageManager {
        Task FindPackages(CanonicalName canonicalName, bool? dependencies = null, bool? installed = null, bool? active = null, bool? required = null, bool? blocked = null, bool? latest = null,
            int? index = null, int? maxResults = null, string location = null, bool? forceScan = null, bool? updates = null, bool? upgrades = null, bool? trimable = null);

        Task GetPackageDetails(CanonicalName canonicalName);
        Task InstallPackage(CanonicalName canonicalName, bool? autoUpgrade, bool? force, bool? download, bool? pretend, bool? isUpdating, bool? isUpgrading);
        Task DownloadProgress(CanonicalName canonicalName, int? downloadProgress);
        Task ListFeeds(int? index = null, int? maxResults = null);
        Task RemoveFeed(string location, bool? session);
        Task AddFeed(string location, bool? session);
        Task VerifyFileSignature(string filename);
        Task SetPackage(CanonicalName canonicalName, bool? active, bool? required, bool? blocked, bool? doNotUpdate, bool? doNotUpgrade);
        Task RemovePackage(CanonicalName canonicalName, bool? force);
        Task UnableToAcquire(CanonicalName canonicalName);
        Task RecognizeFile(CanonicalName canonicalName, string localLocation, string remoteLocation);
        Task SetFeedFlags(string location, string activePassiveIgnored);
        Task SuppressFeed(string location);
        Task GetPolicy(string policyName);
        Task AddToPolicy(string policyName, string account);
        Task RemoveFromPolicy(string policyName, string account);
        Task CreateSymlink(string existingLocation, string newLink, LinkType linkType);
        Task SetFeedStale(string feedLocation);
        Task StopService();
        Task SetLogging(bool? messages, bool? warnings, bool? errors);
        Task ScheduleTask(string taskName, string executable, string commandline, int hour, int minutes, DayOfWeek? dayOfWeek, int intervalInMinutes);
        Task RemoveScheduledTask(string taskName);
        Task GetScheduledTasks(string taskName);
        Task AddTrustedPublisher();
        Task RemoveTrustedPublisher();
        Task GetTrustedPublishers();
        Task GetTelemetry();
        Task SetTelemetry(bool optin);
    }
}