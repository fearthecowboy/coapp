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

namespace CoApp.Packaging.Service {
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using Common;
    using Feeds;
    using PackageFormatHandlers;
    using Toolkit.Collections;
    using Toolkit.Extensions;
    using Toolkit.Tasks;

    public class SessionData {
        internal static SessionData NullSession = new SessionData(); // for requests that happen during a time without an actual session.
        
        internal Session Session;
        internal bool IsSystemCacheLoaded;
        internal bool LoggingWarnings;
        internal bool LoggingMessages;
        internal bool LoggingErrors;

        internal SessionPackageFeed SessionPackageFeed { get; set; }
        internal XList<PackageFeed> TransientFeeds = new XList<PackageFeed>();
        internal XList<string> DisposableFilenames  = new XList<string>();
        internal IEnumerable<string> SuppressedFeeds = Enumerable.Empty<string>();

        internal IDictionary<string, MsiProperties> MsiProperties = new XDictionary<string, MsiProperties>();
        internal IDictionary<string, PackageSessionData> PackageSessionData = new XDictionary<string, PackageSessionData>();
        internal IDictionary<string, PackageFeed > SessionPackageFeeds = new XDictionary<string, PackageFeed>();
        internal IDictionary<string, Recognizer.RecognitionInfo> RecognitionInfo = new XDictionary<string, Recognizer.RecognitionInfo>();
        internal IDictionary<string, Task>  RequestedFileTasks = new XDictionary<string, Task>();

        internal static SessionData Current {
            get {
                return Event<GetCurrentSession>.RaiseFirst() ?? NullSession;
            }
        }

        internal Task RequireRemoteFile<TResult>(string requestReference, IEnumerable<Uri> remoteLocations, string targetFilename, bool forceDownload, Func<RequestRemoteFileState, TResult> onCompletion ) {
            lock (RequestedFileTasks) {
                var completion = RequestedFileTasks[requestReference];

                if (completion != null) {
                    return completion.ContinueAlways(antecedent => onCompletion(completion.AsyncState as RequestRemoteFileState));
                }

                completion = RequestedFileTasks[requestReference] = new Task<TResult>(rrfState => onCompletion(rrfState as RequestRemoteFileState), new RequestRemoteFileState {
                    RequestReference = requestReference,
                    OriginalUrls = remoteLocations
                }, TaskCreationOptions.AttachedToParent);

                Event<GetResponseInterface>.RaiseFirst().RequireRemoteFile(requestReference, remoteLocations, targetFilename, forceDownload);

                return completion;
            }
        }

        internal SessionData() {
            
           
        }
    }
}
