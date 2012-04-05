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

using System.Reflection;
using CoApp.Toolkit.Engine.Model;

namespace CoApp.Toolkit.Engine.Client {
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.IO.Pipes;
    using System.Linq;
    using System.Net;
    using System.Security.Principal;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Extensions;
    using Logging;
    using Pipes;
    using Tasks;
    using Toolkit.Exceptions;
    using Win32;

    public class PackageManager {
        internal class ManualEventQueue : Queue<UrlEncodedMessage>, IDisposable {
            internal static readonly Dictionary<int, ManualEventQueue> EventQueues = new Dictionary<int, ManualEventQueue>();
            private readonly ManualResetEvent _resetEvent = new ManualResetEvent(true);
            private bool _stillWorking;

            public ManualEventQueue() {
                var tid = Task.CurrentId.GetValueOrDefault();
                if (tid == 0) {
                    throw new CoAppException("Cannot create a ManualEventQueue outside of a task.");
                }
                lock (EventQueues) {
                    EventQueues.Add(tid, this);
                }
            }

            public new void Enqueue(UrlEncodedMessage message) {
                base.Enqueue(message);
                _resetEvent.Set();
            }

            public void Dispose() {
                lock (EventQueues) {
                    EventQueues.Remove(Task.CurrentId.GetValueOrDefault());
                }
            }

            public static ManualEventQueue GetQueue(int taskId) {
                lock (EventQueues) {
                    return EventQueues.ContainsKey(taskId) ? EventQueues[taskId] : null;
                }
            }

            internal static void ResetAllQueues() {
                if (EventQueues.Any()) {
                    Logger.Warning("Forcing clearing out event queues in client library");
                    var oldQueues = EventQueues.Values.ToArray();
                    //EventQueues.Clear();
                    foreach( var q in oldQueues ) {
                        q._stillWorking = false;
                        q._resetEvent.Set();
                    }
                }
            }
            
            internal void DispatchResponses() {
                _stillWorking = true;

                while (_stillWorking && _resetEvent.WaitOne()) {
                    _resetEvent.Reset();
                    while (Count > 0) {
                        if (!Dispatch(Dequeue())) {
                            _stillWorking = false;
                        }
                    }
                }
            }
        }


        public static PackageManager Instance = new PackageManager();
        private NamedPipeClientStream _pipe;
        internal const int BufferSize = 1024*1024*2;

        public int ActiveCalls {
            get { return ManualEventQueue.EventQueues.Keys.Count; }
        }

        public bool IsServiceAvailable {
            get { return EngineServiceManager.Available; }
        }

        public bool IsConnected {
            get {  return IsServiceAvailable && _pipe != null && _pipe.IsConnected; }
        }

        private PackageManager() {
        }

        /// <summary>
        /// DEPRECATED Making this deprecated. Client library should be smart enough to connect without being told to.
        /// 
        /// </summary>
        /// <param name="clientName"></param>
        /// <param name="sessionId"></param>
        /// <param name="millisecondsTimeout"></param>
        public void ConnectAndWait(string clientName, string sessionId = null,int millisecondsTimeout = 5000 ) {
            Connect(clientName, sessionId).Wait(millisecondsTimeout);
        }

        public Task Connect() {
            return Connect(Process.GetCurrentProcess().Id.ToString());
        }

        private Task ConnectingTask;
        private int autoConnectionCount;

        public Task Connect(string clientName, string sessionId = null) {
            
            if (IsConnected && _isBufferReady.WaitOne(0) ) {
                return "Completed".AsResultTask();
            }

            lock (this) {
                if (ConnectingTask == null) {
                    _isBufferReady.Reset();

                    ConnectingTask = Task.Factory.StartNew(() => {
                        EngineServiceManager.EnsureServiceIsResponding();

                        sessionId = sessionId ?? Process.GetCurrentProcess().Id.ToString() + "/" + autoConnectionCount++;

                        for (int count = 0; count < 5; count++) {
                            _pipe = new NamedPipeClientStream(".", "CoAppInstaller", PipeDirection.InOut,PipeOptions.Asynchronous,TokenImpersonationLevel.Impersonation);
                            try {
                                _pipe.Connect(500);
                                _pipe.ReadMode = PipeTransmissionMode.Message;
                                break;
                            }
                            catch {
                                // it's not connecting.
                                _pipe = null;
                            }
                        }

                        if (_pipe == null) {
                            throw new CoAppException("Unable to connect to CoApp Service");
                        }

                        StartSession(clientName, sessionId);

                        Task.Factory.StartNew(ProcessMessages,TaskCreationOptions.None).AutoManage();
                        _isBufferReady.WaitOne();
                    }, TaskCreationOptions.AttachedToParent);
                }
            }

            return ConnectingTask;
        }

        private readonly ManualResetEvent _isBufferReady = new ManualResetEvent(false);
        private void ProcessMessages() {
            var incomingMessage = new byte[BufferSize];
            _isBufferReady.Set();

            try {
                do {
                    // we need to wait for the buffer to become available.
                    _isBufferReady.WaitOne();
                    
                    // now we claim the buffer 
                    _isBufferReady.Reset();

                    var readTask = _pipe.ReadAsync(incomingMessage, 0, BufferSize);

                    readTask.ContinueWith(
                        antecedent => {
                            if (antecedent.IsCanceled || antecedent.IsFaulted || !IsConnected) {
                                Disconnect();
                                return;
                            }
                            if (antecedent.Result > 0) {
                                var rawMessage = Encoding.UTF8.GetString(incomingMessage, 0, antecedent.Result);
                                var responseMessage = new UrlEncodedMessage(rawMessage);
                                int? rqid = responseMessage["rqid"];
                                try {
                                    var queue = ManualEventQueue.GetQueue(rqid.GetValueOrDefault());
                                    if (queue != null) {
                                        queue.Enqueue(responseMessage);
                                    }
                                } catch {
                                    //  Console.WriteLine("Unable to queue the response to the right request event queue!");
                                    // Console.WriteLine("    Response:{0}", responseMessage.Command);
                                    // not able to queue up the response to the right task?
                                }

                                // it's ok to let the next readTask use the buffer, we've got the data out & queued.
                                _isBufferReady.Set();
                                
                                // lazy log the response (since we're at the end of this task)
                                Logger.Message("Response:{0}".format(responseMessage.ToSmallerString()));
                            } else {
                                _isBufferReady.Set();
                            }
                        }).AutoManage();

                    // this wait just makes sure that we're only asking for one message at a time
                    // but does not throttle the messages themselves.
                    readTask.Wait();
                } while (IsConnected);
            }
            catch (Exception e) {
                Logger.Message("Connection Terminating with Exception {0}/{1}", e.GetType(), e.Message);
            }
            finally {
                Disconnect();
            }
        }

        public void Disconnect() {
            lock (this) {
                try {
                    if (_pipe != null) {
                        // ensure all queues are stopped and cleared out.
                        ManualEventQueue.ResetAllQueues();
                        _isBufferReady.Set();
                        var pipe = _pipe;
                        _pipe = null;
                        pipe.Close();
                        pipe.Dispose();
                    }
                } catch {
                    // just close it!
                }
            }
        }

        // V1.1 api
        public Task<IEnumerable<Package>> GetPackages(IEnumerable<string> parameters, ulong? minVersion = null, ulong? maxVersion = null,
            bool? dependencies = null, bool? installed = null, bool? active = null, bool? required = null, bool? blocked = null, bool? latest = null,
            string location = null, bool? forceScan = null, bool? updates = null, bool? upgrades = null, bool? trimable = null,  PackageManagerMessages messages = null) {
            Connect(); 

            if (parameters.IsNullOrEmpty()) {
                return GetPackages(string.Empty, minVersion, maxVersion, dependencies, installed, active, required, blocked, latest, location, forceScan,updates , upgrades , trimable, messages);
            }

            // spawn the tasks off in parallel
            var tasks = parameters.Select(each => GetPackages(each, minVersion, maxVersion, dependencies, installed, active, required, blocked, latest, location, forceScan, updates, upgrades, trimable, messages)).ToArray();

            // return a task that is the sum of all the tasks.
            return Task<IEnumerable<Package>>.Factory.ContinueWhenAll((Task[])tasks, antecedents => {
                var faulted = tasks.Where(each => each.IsFaulted);
                if( faulted.Any()) {
                    throw faulted.FirstOrDefault().Exception.Flatten().InnerExceptions.FirstOrDefault();
                }
               return tasks.SelectMany(each => each.Result).Distinct();
            },
                TaskContinuationOptions.AttachedToParent);
        }

        // V1.1 API
        public Task<IEnumerable<Package>> GetPackages(string parameter, ulong? minVersion = null, ulong? maxVersion = null, bool? dependencies = null, bool? installed = null, bool? active = null, bool? required = null, bool? blocked = null, bool? latest = null, string location = null, bool? forceScan = null, bool? updates = null, bool? upgrades = null, bool? trimable = null, PackageManagerMessages messages = null) {
            Connect();

            var packages = new List<Package>();
            if (parameter.IsNullOrEmpty()) {
                return FindPackages( /* canonicalName:*/
                    null, /* name */null, /* version */null, /* arch */ null, /* pkt */null, dependencies, installed, active, required, blocked, latest,
                    /* index */null, /* max-results */null, location, forceScan, updates , upgrades , trimable, new PackageManagerMessages {
                        PackageInformation = package => packages.Add(package),
                    }.Extend(messages)).ContinueWith(antecedent => {
                        if( antecedent.IsFaulted || antecedent.IsCanceled ) {
                            throw antecedent.Exception.Flatten().InnerExceptions.FirstOrDefault();
                        }
                        return packages as IEnumerable<Package>;
                    }, TaskContinuationOptions.AttachedToParent);
            }

            Package singleResult = null;
            string feedAdded = null;

            if (File.Exists(parameter)) {
                var localPath = parameter.EnsureFileIsLocal();
                var originalDirectory = Path.GetDirectoryName(parameter.GetFullPath());
                // add the directory it came from as a session package feed

                if (!string.IsNullOrEmpty(localPath)) {
                    return RecognizeFile(null, localPath, null, new PackageManagerMessages {
                        PackageInformation = package => { singleResult = package; },
                        FeedAdded = feedLocation => { feedAdded = feedLocation; }
                    }.Extend(messages)).ContinueWith(antecedent => {
                        if (singleResult != null) {
                            return AddFeed(originalDirectory, true, new PackageManagerMessages {
                                // don't have to handle any messages here...
                            }.Extend(messages)).ContinueWith(antecedent2 => singleResult.SingleItemAsEnumerable(), TaskContinuationOptions.AttachedToParent).
                                Result;
                        }

                        // if it was a feed, then continue with the big query
                        if (string.IsNullOrEmpty(feedAdded)) {
                            return InternalGetPackages(null, minVersion, maxVersion, dependencies, installed, active, required, blocked, latest, feedAdded,forceScan, updates , upgrades , trimable,  messages).Result;
                        }

                        // if we get here, that means that we didn't recognize the file. 
                        // we're gonna return an empty collection at this point.
                        return Enumerable.Empty<Package>();
                    }, TaskContinuationOptions.AttachedToParent);
                }
                // if we don't get back a local path for the file... this is pretty odd. DUnno what we should really do here yet.
                return Enumerable.Empty<Package>().AsResultTask();
            }
            
            if (Directory.Exists(parameter) || parameter.IndexOf('\\') > -1 || parameter.IndexOf('/') > -1 ||
                (parameter.IndexOf('*') > -1 && parameter.ToLower().EndsWith(".msi"))) {
                // specified a folder, or some kind of path that looks like a feed.
                // add it as a feed, and then get the contents of that feed.
                return AddFeed(parameter, true, new PackageManagerMessages {
                    FeedAdded = feedLocation => { feedAdded = feedLocation; }, 
                    Error = (s, s1, arg3) => {
                        // suppress this error when guessing this is a feed.
                    }
                }.Extend(messages)).ContinueWith(antecedent => {
                    // if it was a feed, then continue with the big query
                    if (!string.IsNullOrEmpty(feedAdded)) {
                        // this overrides any passed in locations with just the feed added.
                        return InternalGetPackages(null, minVersion, maxVersion, dependencies, installed, active, required, blocked, latest, feedAdded, forceScan, updates , upgrades , trimable,  messages).Result;
                    }

                    // maybe it's a relative path from where we are.
                    // let's try that before giving up.
                    parameter = parameter.GetFullPath();
                    AddFeed(parameter, true, new PackageManagerMessages {
                        FeedAdded = feedLocation => {
                            feedAdded = feedLocation;
                        }, 
                        Error = (s, s1, arg3) => {
                            // suppress this error when guessing this is a feed.
                        }
                    }.Extend(messages)).Wait();

                    // if it was a feed, then continue with the big query
                    if (!string.IsNullOrEmpty(feedAdded)) {
                        // this overrides any passed in locations with just the feed added.
                        return InternalGetPackages(null, minVersion, maxVersion, dependencies, installed, active, required, blocked, latest, feedAdded, forceScan, updates, upgrades, trimable, messages).Result;
                    }

                    // if we get here, that means that we didn't recognize the file. 
                    // we're gonna return an empty collection at this point.
                    return Enumerable.Empty<Package>();
                }, TaskContinuationOptions.AttachedToParent);
            }
            // can only be a canonical name match, proceed with that.            
            return InternalGetPackages(PackageName.Parse(parameter), minVersion, maxVersion, dependencies, installed, active, required, blocked, latest, location, forceScan, updates , upgrades , trimable,  messages);
        }

        private Task<IEnumerable<Package>> InternalGetPackages(PackageName packageName, FourPartVersion? minVersion , FourPartVersion? maxVersion ,bool? dependencies , bool? installed , bool? active , bool? required , bool? blocked , bool? latest ,string location , bool? forceScan , bool? updates , bool? upgrades , bool? trimable , PackageManagerMessages messages ) {
            var packages = new List<Package>();

            return FindPackages(packageName != null && packageName.IsFullMatch ? packageName.CanonicalName : null, packageName == null ? null : packageName.Name,
                packageName == null ? null : packageName.Version, packageName == null ? null : packageName.Arch,
                packageName == null ? null : packageName.PublicKeyToken, dependencies, installed, active, required, blocked, latest, null, null, location,
                forceScan, updates, upgrades, trimable, new PackageManagerMessages {
                    PackageInformation = package => {
                        if ((!minVersion.HasValue || package.Version >= minVersion) &&
                            (!maxVersion.HasValue || package.Version <= maxVersion)) {
                            packages.Add(package);
                        }
                    },
                }.Extend(messages)).ContinueWith(antecedent => { 
                        if( antecedent.IsFaulted || antecedent.IsCanceled ) {
                            throw antecedent.Exception.Flatten().InnerExceptions.FirstOrDefault();
                        }
                    return packages as IEnumerable<Package>;
                }, TaskContinuationOptions.AttachedToParent);
        }
        // v1.1 api
        public Task FindPackages(string canonicalName = null, string name = null, string version = null, string arch = null, string publicKeyToken = null,
            bool? dependencies = null, bool? installed = null, bool? active = null, bool? required = null, bool? blocked = null, bool? latest = null,
            int? index = null, int? maxResults = null, string location = null, bool? forceScan = null,bool? updates = null, bool? upgrades = null, bool? trimable = null, PackageManagerMessages messages = null) {

            return Connect().ContinueWith((antecedent) => {
                if (messages != null) {
                    messages.Register();
                }
                using (var eventQueue = new ManualEventQueue()) {
                    WriteAsync(new UrlEncodedMessage("find-packages") {
                        {"canonical-name", canonicalName},
                        {"name", name},
                        {"version", version},
                        {"arch", arch},
                        {"public-key-token", publicKeyToken},
                        {"dependencies", dependencies},
                        {"installed", installed},
                        {"active", active},
                        {"required", required},
                        {"blocked", blocked},
                        {"latest", latest},
                        {"index", index},
                        {"max-results", maxResults},
                        {"location", location},
                        {"force-scan", forceScan},
                        {"updates", updates},
                        {"upgrades", upgrades},
                        {"trimable", trimable},
                        {"rqid", Task.CurrentId},
                    });

                    // will return when the final message comes thru.
                    eventQueue.DispatchResponses();
                }
            }, TaskContinuationOptions.OnlyOnRanToCompletion).AutoManage();
        }

        public Task GetPackageDetails(string canonicalName, PackageManagerMessages messages = null) {
            return Connect().ContinueWith((antecedent) => {
                if (messages != null) {
                    messages.Register();
                }
                using (var eventQueue = new ManualEventQueue()) {
                    WriteAsync(new UrlEncodedMessage("get-package-details") {
                        {"canonical-name", canonicalName},
                        {"rqid", Task.CurrentId},
                    });

                    // will return when the final message comes thru.
                    eventQueue.DispatchResponses();
                }
            }, TaskContinuationOptions.OnlyOnRanToCompletion).AutoManage();
        }

        public Task InstallPackage(string canonicalName, bool? autoUpgrade = null, bool? force = null, bool? download = null, bool? pretend = null, PackageManagerMessages messages = null) {
            return InstallPackage(canonicalName, autoUpgrade, force, download, pretend, null, null, messages);
        }

        public Task InstallPackage(string canonicalName, bool? autoUpgrade, bool? force , bool? download , bool? pretend , bool? isUpdating, bool? isUpgrading, PackageManagerMessages messages = null) {

            return Connect().ContinueWith((antecedent) => {
                var msgs = new PackageManagerMessages {
                    InstalledPackage = (pkgCanonicalName) => {
                        if( !PackageManagerSettings.CoAppSettings["#Telemetry"].StringValue.IsFalse() ) {
                            // ping the coapp server to tell it that a package installed
                            try {
                                var uniqId = PackageManagerSettings.CoAppSettings["#AnonymousId"].StringValue; 
                                if( string.IsNullOrEmpty(uniqId) || uniqId.Length != 32 ) {
                                    uniqId = Guid.NewGuid().ToString("N");
                                    PackageManagerSettings.CoAppSettings["#AnonymousId"].StringValue = uniqId;
                                }
                                
                                Logger.Message("Pinging `http://coapp.org/telemetry/?anonid={0}&pkg={1}` ".format(uniqId, pkgCanonicalName));
                                var req =
                                    HttpWebRequest.Create("http://coapp.org/telemetry/?anonid={0}&pkg={1}".format(uniqId, pkgCanonicalName));
                                req.BetterGetResponse().Close();
                            } catch {
                                // who cares...
                            }
                        }

                        if (messages != null && messages.InstalledPackage != null) {
                            messages.InstalledPackage(pkgCanonicalName);
                        }
                    }
                }.Extend(messages);

                msgs.Register();
                using (var eventQueue = new ManualEventQueue()) {
                    WriteAsync(new UrlEncodedMessage("install-package") {
                        {"canonical-name", canonicalName},
                        {"auto-upgrade", autoUpgrade},
                        {"force", force},
                        {"download", download},
                        {"pretend", pretend},
                        {"is-update", isUpdating},
                        {"is-upgrade", isUpgrading},
                        {"rqid", Task.CurrentId},
                    });

                    // will return when the final message comes thru.
                    eventQueue.DispatchResponses();
                }
            }, TaskContinuationOptions.OnlyOnRanToCompletion).AutoManage();
        }

        public Task ListFeeds(int? index = null, int? maxResults = null, PackageManagerMessages messages = null) {
            return  Connect().ContinueWith((antecedent) => {
                if (messages != null) {
                    messages.Register();
                }
                using (var eventQueue = new ManualEventQueue()) {
                    WriteAsync(new UrlEncodedMessage("find-feeds") {
                        {"index", index},
                        {"max-results", maxResults},
                        {"rqid", Task.CurrentId},
                    });

                    // will return when the final message comes thru.
                    eventQueue.DispatchResponses();
                }
            }, TaskContinuationOptions.OnlyOnRanToCompletion).AutoManage();
        }

        public Task RemoveFeed(string location, bool? session = null, PackageManagerMessages messages = null) {
            return Connect().ContinueWith((antecedent) => {
                if (messages != null) {
                    messages.Register();
                }
                using (var eventQueue = new ManualEventQueue()) {
                    WriteAsync(new UrlEncodedMessage("remove-feed") {
                        {"location", location},
                        {"session", session},
                        {"rqid", Task.CurrentId},
                    });

                    // will return when the final message comes thru.
                    eventQueue.DispatchResponses();
                }
            }, TaskContinuationOptions.OnlyOnRanToCompletion).AutoManage();
        }

        public Task AddFeed(string location, bool? session = null, PackageManagerMessages messages = null) {
            return Connect().ContinueWith((antecedent) => {
                if (messages != null) {
                    messages.Register();
                }
                using (var eventQueue = new ManualEventQueue()) {
                    WriteAsync(new UrlEncodedMessage("add-feed") {
                        {"location", location},
                        {"session", session},
                        {"rqid", Task.CurrentId},
                    });

                    // will return when the final message comes thru.
                    eventQueue.DispatchResponses();
                }
            }, TaskContinuationOptions.OnlyOnRanToCompletion).AutoManage();
        }

        public Task VerifyFileSignature(string filename, PackageManagerMessages messages = null) {
            return Connect().ContinueWith((antecedent) => {
                if (messages != null) {
                    messages.Register();
                }
                using (var eventQueue = new ManualEventQueue()) {
                    WriteAsync(new UrlEncodedMessage("verify-file-signature") {
                        {"filename", filename},
                        {"rqid", Task.CurrentId},
                    });

                    // will return when the final message comes thru.
                    eventQueue.DispatchResponses();
                }
            }, TaskContinuationOptions.OnlyOnRanToCompletion).AutoManage();
        }

        public Task SetPackage(string canonicalName, bool? active , bool? required , bool? blocked , PackageManagerMessages messages ) {
            return SetPackage(canonicalName, active, required, blocked, null, null, messages);
        }

        public Task SetPackage(string canonicalName, bool? active = null, bool? required = null, bool? blocked = null, bool? doNotUpdate = null, bool? doNotUpgrade = null, PackageManagerMessages messages = null) {
            return Connect().ContinueWith((antecedent) => {
                if (messages != null) {
                    messages.Register();
                }
                using (var eventQueue = new ManualEventQueue()) {
                    WriteAsync(new UrlEncodedMessage("set-package") {
                        {"canonical-name", canonicalName},
                        {"active", active},
                        {"required", required},
                        {"blocked", blocked},
                        {"do-not-update", doNotUpdate},
                        {"do-not-upgrade", doNotUpgrade},
                        // active-configuration-name
                        {"rqid", Task.CurrentId},
                    });

                    // will return when the final message comes thru.
                    eventQueue.DispatchResponses();
                }
            }, TaskContinuationOptions.OnlyOnRanToCompletion).AutoManage();
        }

        public Task SetFeedStale(string feedLocation, PackageManagerMessages messages = null) {
            return Connect().ContinueWith((antecedent) => {
                if (messages != null) {
                    messages.Register();
                }
                using (var eventQueue = new ManualEventQueue()) {
                    WriteAsync(new UrlEncodedMessage("set-feed-stale") {
                        {"feed-name", feedLocation},
                        {"rqid", Task.CurrentId},
                    });

                    // will return when the final message comes thru.
                    eventQueue.DispatchResponses();
                }
            }, TaskContinuationOptions.OnlyOnRanToCompletion).AutoManage();
        }

        public Task RemovePackage(string canonicalName, bool? force = null, PackageManagerMessages messages = null) {
            return Connect().ContinueWith((antecedent) => {
                if (messages != null) {
                    messages.Register();
                }
                using (var eventQueue = new ManualEventQueue()) {
                    WriteAsync(new UrlEncodedMessage("remove-package") {
                        {"canonical-name", canonicalName},
                        {"force", force},
                        {"rqid", Task.CurrentId},
                    });

                    // will return when the final message comes thru.
                    eventQueue.DispatchResponses();
                }
            }, TaskContinuationOptions.OnlyOnRanToCompletion).AutoManage();
        }

        public Task UnableToAcquire(string canonicalName, PackageManagerMessages messages = null) {
            return Connect().ContinueWith((antecedent) => {
                if (messages != null) {
                    messages.Register();
                }
                using (var eventQueue = new ManualEventQueue()) {
                    WriteAsync(new UrlEncodedMessage("unable-to-acquire") {
                        {"canonical-name", canonicalName},
                        {"rqid", Task.CurrentId},
                    });

                    // will return when the final message comes thru.
                    eventQueue.DispatchResponses();
                }
            }, TaskContinuationOptions.OnlyOnRanToCompletion).AutoManage();
        }

        public Task DownloadProgress(string canonicalName, int progress, PackageManagerMessages messages = null) {
            return Connect().ContinueWith((antecedent) => {
                if (messages != null) {
                    messages.Register();
                }
                using (var eventQueue = new ManualEventQueue()) {
                    WriteAsync(new UrlEncodedMessage("download-progress") {
                        {"canonical-name", canonicalName},
                        {"progress", progress.ToString()},
                        {"rqid", Task.CurrentId},
                    });

                    // will return when the final message comes thru.
                    eventQueue.DispatchResponses();
                }
            }, TaskContinuationOptions.OnlyOnRanToCompletion).AutoManage();
        }

        public Task RecognizeFile(string canonicalName, string localLocation, string remoteLocation, PackageManagerMessages messages = null) {
            return Connect().ContinueWith((antecedent) => {
                if (messages != null) {
                    messages.Register();
                }
                using (var eventQueue = new ManualEventQueue()) {
                    WriteAsync(new UrlEncodedMessage("recognize-file") {
                        {"canonical-name", canonicalName},
                        {"local-location", localLocation},
                        {"remote-location", remoteLocation},
                        {"rqid", Task.CurrentId},
                    });

                    // will return when the final message comes thru.
                    eventQueue.DispatchResponses();
                }
            }, TaskContinuationOptions.OnlyOnRanToCompletion).AutoManage();
        }

        public Task SuppressFeed(string location, PackageManagerMessages messages = null) {
            return Connect().ContinueWith((antecedent) => {
                if (messages != null) {
                    messages.Register();
                }
                using (var eventQueue = new ManualEventQueue()) {
                    WriteAsync(new UrlEncodedMessage("suppress-feed") {
                        {"location", location},
                        {"rqid", Task.CurrentId},
                    });

                    // will return when the final message comes thru.
                    eventQueue.DispatchResponses();
                }
            }, TaskContinuationOptions.OnlyOnRanToCompletion).AutoManage();
        }

        public Task SetLogging( bool? Messages, bool? Warnings, bool? Errors ) {
            return SetLogging(Messages, Warnings, Errors);
        }

        public Task SetLogging(bool? Messages = null, bool? Warnings = null, bool? Errors = null, PackageManagerMessages messages = null) {
            return Connect().ContinueWith((antecedent) => {
                using (var eventQueue = new ManualEventQueue()) {
                    WriteAsync(new UrlEncodedMessage("set-logging") {
                        {"messages", Messages},
                        {"warnings", Warnings},
                        {"errors", Errors},
                        {"rqid", Task.CurrentId},
                    });

                    // will return when the final message comes thru.
                    eventQueue.DispatchResponses();
                }
            }, TaskContinuationOptions.OnlyOnRanToCompletion).AutoManage();
        }

        public Task GetPolicy(string policyName, PackageManagerMessages messages = null) {
            return Connect().ContinueWith((antecedent) => {
                if (messages != null) {
                    messages.Register();
                }

                using (var eventQueue = new ManualEventQueue()) {
                    WriteAsync(new UrlEncodedMessage("get-policy") {
                        {"name", policyName},
                        {"rqid", Task.CurrentId},
                    });

                    // will return when the final message comes thru.
                    eventQueue.DispatchResponses();
                }
            }, TaskContinuationOptions.OnlyOnRanToCompletion).AutoManage();
        }

        public Task AddToPolicy(string policyName, string account ,PackageManagerMessages messages = null) {
            return Connect().ContinueWith((antecedent) => {
                if (messages != null) {
                    messages.Register();
                }

                using (var eventQueue = new ManualEventQueue()) {
                    WriteAsync(new UrlEncodedMessage("add-to-policy") {
                        {"name", policyName},
                        {"account", account},
                        {"rqid", Task.CurrentId},
                    });

                    // will return when the final message comes thru.
                    eventQueue.DispatchResponses();
                }
            }, TaskContinuationOptions.OnlyOnRanToCompletion).AutoManage();
        }

        public Task RemoveFromPolicy(string policyName, string account, PackageManagerMessages messages = null) {
            return Connect().ContinueWith((antecedent) => {
                if (messages != null) {
                    messages.Register();
                }

                using (var eventQueue = new ManualEventQueue()) {
                    WriteAsync(new UrlEncodedMessage("remove-from-policy") {
                        {"name", policyName},
                        {"account", account},
                        {"rqid", Task.CurrentId},
                    });

                    // will return when the final message comes thru.
                    eventQueue.DispatchResponses();
                }
            }, TaskContinuationOptions.OnlyOnRanToCompletion).AutoManage();
        }

        public Task CreateSymlink(string existingLocation, string newLink, LinkType linkType,  PackageManagerMessages messages = null) {
            return Connect().ContinueWith((antecedent) => {
                  if (messages != null) {
                      messages.Register();
                  }
                  using (var eventQueue = new ManualEventQueue()) {
                      WriteAsync(new UrlEncodedMessage("symlink") {
                        {"existing-location", existingLocation},
                        {"new-link", newLink},
                        {"link-type", linkType.ToString()},
                        {"rqid", Task.CurrentId},
                    });

                      // will return when the final message comes thru.
                      eventQueue.DispatchResponses();
                  }
            }, TaskContinuationOptions.OnlyOnRanToCompletion).AutoManage();
          }

        public Task AddScheduledTask(string taskName, string executable, string commandline, int hour, int minutes, DayOfWeek? dayOfWeek, int intervalInMinutes, PackageManagerMessages messages = null) {
            return Connect().ContinueWith((antecedent) => {
                if (messages != null) {
                    messages.Register();
                }
                using (var eventQueue = new ManualEventQueue()) {
                    WriteAsync(new UrlEncodedMessage("schedule-task") {
                        {"name", taskName},
                        {"executable", executable},
                        {"command-line", commandline},
                        {"hour", hour},
                        {"minutes", minutes},
                        {"day-of-week", dayOfWeek != null ? dayOfWeek.Value.ToString():null },
                        {"interval", intervalInMinutes},
                        {"rqid", Task.CurrentId},
                    });

                    // will return when the final message comes thru.
                    eventQueue.DispatchResponses();
                }
            }, TaskContinuationOptions.OnlyOnRanToCompletion).AutoManage();
        }

        public Task RemoveScheduledTask(string taskName, PackageManagerMessages messages = null) {
            return Connect().ContinueWith((antecedent) => {
                if (messages != null) {
                    messages.Register();
                }
                using (var eventQueue = new ManualEventQueue()) {
                    WriteAsync(new UrlEncodedMessage("remove-scheduled-task") {
                        {"name", taskName},
                        {"rqid", Task.CurrentId},
                    });

                    // will return when the final message comes thru.
                    eventQueue.DispatchResponses();
                }
            }, TaskContinuationOptions.OnlyOnRanToCompletion).AutoManage();
        }

        public Task GetScheduledTask(string taskName, PackageManagerMessages messages = null) {
            return Connect().ContinueWith((antecedent) => {
                if (messages != null) {
                    messages.Register();
                }
                using (var eventQueue = new ManualEventQueue()) {
                    WriteAsync(new UrlEncodedMessage("get-scheduled-tasks") {
                        {"name", taskName},
                        {"rqid", Task.CurrentId},
                    });

                    // will return when the final message comes thru.
                    eventQueue.DispatchResponses();
                }
            }, TaskContinuationOptions.OnlyOnRanToCompletion).AutoManage();
        }

        public Task SetTelemetry(bool optIntoTelemetryTracking, PackageManagerMessages messages = null) {
            return Connect().ContinueWith((antecedent) => {
                if (messages != null) {
                    messages.Register();
                }
                using (var eventQueue = new ManualEventQueue()) {
                    WriteAsync(new UrlEncodedMessage("set-telemetry") {
                        {"opt-in", optIntoTelemetryTracking},
                        {"rqid", Task.CurrentId},
                    });

                    // will return when the final message comes thru.
                    eventQueue.DispatchResponses();
                }
            }, TaskContinuationOptions.OnlyOnRanToCompletion).AutoManage();
        }

        public Task GetTelemetry(PackageManagerMessages messages = null) {
            return Connect().ContinueWith((antecedent) => {
                if (messages != null) {
                    messages.Register();
                }
                using (var eventQueue = new ManualEventQueue()) {
                    WriteAsync(new UrlEncodedMessage("get-telemetry") {
                        {"rqid", Task.CurrentId},
                    });

                    // will return when the final message comes thru.
                    eventQueue.DispatchResponses();
                }
            }, TaskContinuationOptions.OnlyOnRanToCompletion).AutoManage();
        }

        internal static bool Dispatch(UrlEncodedMessage responseMessage = null) {
            switch (responseMessage.Command) {
                case "failed-package-install":
                    PackageManagerMessages.Invoke.FailedPackageInstall(responseMessage["canonical-name"], responseMessage["filename"], responseMessage["reason"]);
                    break;

                case "failed-package-remove":
                    PackageManagerMessages.Invoke.FailedPackageRemoval(responseMessage["canonical-name"], responseMessage["reason"]);
                    break;

                case "feed-added":
                    PackageManagerMessages.Invoke.FeedAdded(responseMessage["location"]);
                    break;

                case "feed-removed":
                    PackageManagerMessages.Invoke.FeedRemoved(responseMessage["location"]);
                    break;

                case "feed-suppressed":
                    PackageManagerMessages.Invoke.FeedSuppressed(responseMessage["location"]);
                    break;

                case "file-not-found":
                    PackageManagerMessages.Invoke.FileNotFound(responseMessage["filename"]);
                    break;

                case "found-feed":
                    PackageManagerMessages.Invoke.FeedDetails(responseMessage["location"], new DateTime( long.Parse(responseMessage["last-scanned"]) ), 
                        (bool?) responseMessage["session"] ?? false, (bool?) responseMessage["suppressed"] ?? false,
                        (bool?) responseMessage["validated"] ?? false);
                    break;

                case "found-package":
                    var result = Package.GetPackage(responseMessage["canonical-name"]);

                    result.LocalPackagePath = responseMessage["local-location"];
                    result.Name = responseMessage["name"];
                    result.Version = (FourPartVersion)(string)responseMessage["version"];
                    result.MinPolicy= (FourPartVersion)(string)responseMessage["min-policy"];
                    result.MaxPolicy = (FourPartVersion)(string)responseMessage["max-policy"];
                    result.Architecture = ((string)responseMessage["arch"]);
                    result.PublicKeyToken = responseMessage["public-key-token"];
                    result.ProductCode = responseMessage["product-code"];
                    result.IsInstalled = (bool?) responseMessage["installed"] ?? false;
                    result.IsBlocked = (bool?) responseMessage["blocked"] ?? false;
                    
                    result.IsRequired = (bool?) responseMessage["required"] ?? false;
                   
                    result.IsClientRequired = (bool?) responseMessage["client-required"] ?? false;
                    
                    result.IsActive = (bool?) responseMessage["active"] ?? false;
                    result.IsDependency = (bool?) responseMessage["dependent"] ?? false;
                    result.RemoteLocations = responseMessage.GetCollection("remote-locations");
                    result.Dependencies = responseMessage.GetCollection("dependencies");
                    result.SupercedentPackages = responseMessage.GetCollection("supercedent-packages");
                    result.IsPackageInfoStale = false;

                    PackageManagerMessages.Invoke.PackageInformation(result);
                    break;

                case "installed-package":
                    EnvironmentUtility.BroadcastChange();
                    PackageManagerMessages.Invoke.InstalledPackage(responseMessage["canonical-name"]);
                    break;

                case "installing-package":
                    PackageManagerMessages.Invoke.InstallingPackageProgress(responseMessage["canonical-name"], (int?) responseMessage["percent-complete"] ?? 0,(int?) responseMessage["overall-percent-complete"] ?? 0);
                    break;

                case "message-argument-error":
                    PackageManagerMessages.Invoke.Error(responseMessage["message"], responseMessage["parameter"], responseMessage["reason"]);
                    break;

                case "message-warning":
                    PackageManagerMessages.Invoke.Warning(responseMessage["message"], responseMessage["parameter"], responseMessage["reason"]);
                    break;

                case "no-feeds-found":
                    PackageManagerMessages.Invoke.NoFeedsFound();
                    break;

                case "no-packages-found":
                    PackageManagerMessages.Invoke.NoPackagesFound();
                    break;

                case "operation-canceled":
                    PackageManagerMessages.Invoke.OperationCanceled(responseMessage["message"]);
                    return false;

                case "operation-requires-permission":
                    PackageManagerMessages.Invoke.PermissionRequired(responseMessage["policy-required"]);
                    break;

                case "package-satisfied-by":
                    PackageManagerMessages.Invoke.PackageSatisfiedBy(Package.GetPackage(responseMessage["canonical-name"]), Package.GetPackage(responseMessage["satisfied-by"]));
                    break;

                case "package-details":
                    var details = Package.GetPackage(responseMessage["canonical-name"]);
                    details.Description = responseMessage["description"];

                    details.Summary = responseMessage["summary"];
                    details.DisplayName = responseMessage["display-name"];
                    details.Copyright = responseMessage["copyright"];
                    details.AuthorVersion = responseMessage["author-version"];
                    details.Icon = responseMessage["icon"];
                    details.License = responseMessage["license"];
                    details.LicenseUrl = responseMessage["license-url"];
                    details.PublishDate = responseMessage["publish-date"];
                    details.PublisherName = responseMessage["publisher-name"];
                    details.PublisherUrl = responseMessage["publisher-url"];
                    details.PublisherEmail = responseMessage["publisher-email"];
                    details.Tags = responseMessage.GetCollection("tags");
                    details.PackageItemText = responseMessage["package-item-text"];
                    details.Roles =
                        responseMessage.GetKeyValuePairs("role").Select(
                            each => new Role { Name = each.Key, PackageRole = (PackageRole)Enum.Parse(typeof(PackageRole), each.Value, true) });
                    

                    /*
                    if (!package.PackageDetails.Contributors.IsNullOrEmpty()) {
                        msg.AddCollection("contributor-name", package.PackageDetails.Contributors.Select(each => each.Name));
                        msg.AddCollection("contributor-url", package.PackageDetails.Contributors.Select(each => each.Url));
                        msg.AddCollection("contributor-email", package.PackageDetails.Contributors.Select(each => each.Email));
                    }
                     * */
                    details.IsPackageDetailsStale = false;
                    PackageManagerMessages.Invoke.PackageDetails(details);
                    break;

                case "package-has-potential-upgrades":
                    var supercedents = responseMessage.GetCollection("supercedent-packages");
                    var pkg = responseMessage["canonical-name"];
                    // first, make sure we have the canonical package
                    PackageManager.Instance.GetPackages(pkg).Continue(pkgResult => {
                        supercedents.Select(each => PackageManager.Instance.GetPackages(each)).Continue(superpkgs => {
                            PackageManagerMessages.Invoke.PackageHasPotentialUpgrades(pkgResult.FirstOrDefault(), superpkgs.SelectMany(each => each));
                        });
                    });
                    
                    
                    break;

                case "package-is-blocked":
                    PackageManagerMessages.Invoke.PackageBlocked(responseMessage["canonical-name"]);
                    break;

                case "removed-package":
                    PackageManagerMessages.Invoke.RemovedPackage(responseMessage["canonical-name"]);
                    break;

                case "removing-package":
                    PackageManagerMessages.Invoke.RemovingPackageProgress(responseMessage["canonical-name"], (int?) responseMessage["percent-complete"] ?? 0);
                    break;

                case "require-remote-file":
                    PackageManagerMessages.Invoke.RequireRemoteFile(responseMessage["canonical-name"], responseMessage.GetCollection("remote-locations"),
                        responseMessage["destination"], (bool?) responseMessage["force"] ?? false);
                    break;

                case "signature-validation":
                    PackageManagerMessages.Invoke.SignatureValidation(responseMessage["filename"], (bool?) responseMessage["is-valid"] ?? false,
                        responseMessage["certificate-subject-name"]);
                    break;

                case "unable-to-recognize-file":
                    PackageManagerMessages.Invoke.FileNotRecognized(responseMessage["filename"], responseMessage["reason"]);
                    break;

                case "unexpected-failure":
                    // PackageManagerMessages.Invoke.UnexpectedFailure( responseMessage["type"], responseMessage["message"], responseMessage["stacktrace"]);
                    break;

                case "unknown-package":
                    PackageManagerMessages.Invoke.UnknownPackage(responseMessage["canonical-name"]);
                    break;

                case "unknown-command":
                    Console.WriteLine("Unknown command!");
                    break;

                case "policy":
                    PackageManagerMessages.Invoke.PolicyInformation(responseMessage["name"], responseMessage["description"], responseMessage.GetCollection("accounts"));
                    break;

                case "telemetry":
                    PackageManagerMessages.Invoke.CurrentTelemetryOption((bool?)responseMessage["opt-in"]??false);
                    break;

                case "restarting":
                    PackageManagerMessages.Invoke.Restarting();
                    // disconnect from the engine, and let the client reconnect on the next call.
                    Instance.Disconnect();
                    break;

                case "done-set-logging" :
                    PackageManagerMessages.Invoke.LoggingSettings((bool?)responseMessage["is-logging-messages"] ?? false, (bool?)responseMessage["is-logging-warnings"] ?? false, (bool?)responseMessage["is-logging-errors"]??false);
                    break;

                case "scheduled-task-info" :
                    PackageManagerMessages.Invoke.ScheduledTaskInfo(responseMessage["name"], responseMessage["executable"], responseMessage["command-line"], (int?)responseMessage["hour"] ?? 0, (int?)responseMessage["minutes"] ?? 0, (DayOfWeek?)(int?)responseMessage["day-of-week"], (int?)responseMessage["interval"]??0);
                    break;

                case "task-complete":
                    return false;
            }

            return true;
        }

        /// <summary>
        ///   Writes the message to the stream asyncly.
        /// </summary>
        /// <param name = "message">The request.</param>
        /// <returns></returns>
        /// <remarks>
        /// </remarks>
        private void WriteAsync(UrlEncodedMessage message) {
            if (IsConnected) {
                try {
                    _pipe.WriteLineAsync(message.ToString()).ContinueWith(antecedent => { Console.WriteLine("Async Write Fail!? (1)"); },
                        TaskContinuationOptions.OnlyOnFaulted);
                }
                catch /* (Exception e) */ {
                    
                }
            }
        }

        private void StartSession(string clientId, string sessionId) {
            WriteAsync(new UrlEncodedMessage("start-session") {
                {"client", clientId},
                {"id", sessionId},
                {"rqid", sessionId},
            });
        }
    }
}