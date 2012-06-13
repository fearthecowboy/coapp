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
    using System.Collections.Generic;
    using System.Linq.Expressions;
    using System.Threading.Tasks;
    using Toolkit.ImpromptuInterface.Dynamic;

    using PkgFilter = System.Linq.Expressions.Expression<System.Func<Common.IPackage, bool>>;
    using CollectionFilter = Toolkit.Collections.XList<System.Linq.Expressions.Expression<System.Func<System.Collections.Generic.IEnumerable<Common.IPackage>, System.Collections.Generic.IEnumerable<Common.IPackage>>>>;


    [UseNamedArgument]
    public interface IPackageManager {

        Task FindPackages(CanonicalName canonicalName, PkgFilter filter, CollectionFilter collectionFilter, string location);

        Task GetPackageDetails(CanonicalName canonicalName);
        Task InstallPackage(CanonicalName canonicalName, bool? autoUpgrade, bool? force, bool? download, bool? pretend, CanonicalName replacingPackage);
        Task RemovePackage(CanonicalName canonicalName, bool? force);

        Task ListFeeds();
        Task RemoveFeed(string location, bool? session);
        Task AddFeed(string location, bool? session);
        Task SetFeedFlags(string location, FeedState feedState);
        Task SuppressFeed(string location);
        Task SetFeedStale(string feedLocation);

        Task VerifyFileSignature(string filename);

        Task SetGeneralPackageInformation(int priority, CanonicalName canonicalName, string key, string value);
        Task GetGeneralPackageInformation();

        Task SetPackageWanted(CanonicalName canonicalName, bool wanted);

        Task RecognizeFile(string requestReference, string localLocation, string remoteLocation);
        Task RecognizeFiles(IEnumerable<string> localLocations);
        Task UnableToAcquire(string requestReference);
        Task DownloadProgress(string requestReference, int? downloadProgress);

        Task GetPolicy(string policyName);
        Task AddToPolicy(string policyName, string account);
        Task RemoveFromPolicy(string policyName, string account);

        Task CreateSymlink(string existingLocation, string newLink, LinkType linkType);
        
        Task StopService();
        Task SetLogging(bool? messages, bool? warnings, bool? errors);
      
        Task ScheduleTask(string taskName, string executable, string commandline, int hour, int minutes, DayOfWeek? dayOfWeek, int intervalInMinutes);
        Task RemoveScheduledTask(string taskName);
        Task GetScheduledTasks(string taskName);

        Task AddTrustedPublisher(string publisherKeyToken);
        Task RemoveTrustedPublisher(string publisherKeyToken);
        Task GetTrustedPublishers();

        Task GetTelemetry();
        Task SetTelemetry(bool optin);

        Task SetConfigurationValue(string key, string valuename, string value);
        Task GetConfigurationValue(string key, string valuename);

        Task GetAtomFeed(IEnumerable<CanonicalName> canonicalNames);
    }
}
