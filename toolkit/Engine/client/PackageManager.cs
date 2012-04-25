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

namespace CoApp.Toolkit.Engine.Client {
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Dynamic;
    using System.IO;
    using System.IO.Pipes;
    using System.Linq;
    using System.Security.Principal;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Exceptions;
    using Extensions;
    using ImpromptuInterface;
    using Logging;
    using Pipes;
    using Tasks;
    using Toolkit.Exceptions;
    using Win32;

    public delegate void PackageInstallProgress(string packageCanonicalName, int progress, int overallProgress);
    public delegate void PackageRemoveProgress(string packageCanonicalName, int progress);
    public delegate void DownloadCompleted(string remoteLocation, string localLocation);
    public delegate void DownloadProgress(string remoteLocation, string localLocation, int progress);
    internal delegate string GetCurrentRequestId();

    public delegate IncomingCallDispatcher<IPackageManagerResponse> GetResponseDispatcher();

    public class CallResponse : IPackageManagerResponse {
        private static readonly IPackageManager PM = PackageManager.RemoteService;

        private readonly Lazy<List<Package>> _packages = new Lazy<List<Package>>(() => new List<Package>());
        private readonly Lazy<List<Feed>> _feeds = new Lazy<List<Feed>>(() => new List<Feed>());
        private readonly Lazy<List<Policy>> _policies = new Lazy<List<Policy>>(() => new List<Policy>());
        private readonly Lazy<List<ScheduledTask>> _scheduledTasks = new Lazy<List<ScheduledTask>>(() => new List<ScheduledTask>());
        

        private readonly IncomingCallDispatcher<IPackageManagerResponse> _dispatcher;

        internal LoggingSettings LoggingSettingsResult;
        internal bool EngineRestarting;
        internal bool NoPackages;
        internal bool IsSignatureValid;
        internal bool OptedIn;

        internal IEnumerable<Package> Packages { get { return _packages.IsValueCreated ? _packages.Value.Distinct() : Enumerable.Empty<Package>();}}
        internal IEnumerable<Feed> Feeds { get { return _feeds.IsValueCreated ? _feeds.Value.Distinct() : Enumerable.Empty<Feed>(); } }
        internal IEnumerable<Policy> Policies { get { return _policies.IsValueCreated ? _policies.Value.Distinct() : Enumerable.Empty<Policy>(); } }
        internal IEnumerable<ScheduledTask> ScheduledTasks { get { return _scheduledTasks.IsValueCreated ? _scheduledTasks.Value.Distinct() : Enumerable.Empty<ScheduledTask>(); } }

        internal string OperationCanceledReason;
        internal Package UpgradablePackage;
        internal IEnumerable<Package> PotentialUpgrades;
        internal static Dictionary<string, Task> CurrentDownloads = new Dictionary<string, Task>();
        
        public CallResponse() {
            // this makes sure that all response messages are getting sent back to here.
            _dispatcher = new IncomingCallDispatcher<IPackageManagerResponse>(this);
            CurrentTask.Events += new GetResponseDispatcher(() => _dispatcher);
        }

        internal void Clear() {
            EngineRestarting = false;
            NoPackages = false;
            IsSignatureValid = false;
            OperationCanceledReason = null;
        }

        public void ThrowWhenFaulted(Task antecedent) {
            // do not get all fussy when the engine is restarting.
            if (EngineRestarting) {
                return;
            }

            if (!string.IsNullOrEmpty(OperationCanceledReason)) {
                throw new OperationCanceledException(OperationCanceledReason);
            }

            antecedent.RethrowWhenFaulted();
        }

        public void NoPackagesFound() {
            NoPackages = true;
        }

        public void PolicyInformation(string name, string description, IEnumerable<string> accounts) {
            _policies.Value.Add(new Policy { Name = name, Description = description, Members = accounts});
        }

        public  void SendSessionStarted(string sessionId) {
            // throw new NotImplementedException();
        }

        public void PackageInformation(string canonicalName, string localLocation, string name, FourPartVersion version, Architecture architecture, string publicKeyToken, bool installed, bool blocked, bool required, bool clientRequired, bool active, bool dependent, FourPartVersion minPolicy, FourPartVersion maxPolicy, IEnumerable<string> remoteLocations, IEnumerable<string> dependencies, IEnumerable<string> supercedentPackages) {
            if (!Environment.Is64BitOperatingSystem && architecture == Architecture.x64) {
                // skip x64 packages from the result set if you're not on an x64 system.
                return;
            }

            var result = Package.GetPackage(canonicalName);
            result.LocalPackagePath = localLocation;
            result.Name = name;
            result.Version = version;
            result.MinPolicy = minPolicy;
            result.MaxPolicy = maxPolicy;
            result.Architecture = architecture;
            result.PublicKeyToken = publicKeyToken;
            // result.ProductCode = prod
            result.IsInstalled = installed;
            result.IsBlocked = blocked;
            result.IsRequired = required;
            result.IsClientRequired = clientRequired;
            result.IsActive = active;
            result.IsDependency = dependent;
            result.RemoteLocations = remoteLocations;
            result.Dependencies = dependencies;
            result.SupercedentPackages = supercedentPackages;
            result.IsPackageInfoStale = false;
            _packages.Value.Add(result);
        }

        public void PackageDetails(string canonicalName, Dictionary<string, string> metadata, IEnumerable<string> iconLocations, Dictionary<string, string> licenses, Dictionary<string, string> roles, IEnumerable<string> tags, IDictionary<string, string> contributorUrls, IDictionary<string, string> contributorEmails) {
            var result = Package.GetPackage(canonicalName);
        }

        public void FeedDetails(string location, DateTime lastScanned, bool session, bool suppressed, bool validated, string state) {
            _feeds.Value.Add( new Feed {
                Location = location,
                LastScanned = lastScanned,
                IsSession = session,
                IsSuppressed = suppressed,
                FeedState = state.ParseEnum(FeedState.active)
            });
        }

        public void InstallingPackageProgress(string canonicalName, int percentComplete, int overallProgress) {
            Event<PackageInstallProgress>.Raise(canonicalName, percentComplete, overallProgress);
        }

        public void RemovingPackageProgress(string canonicalName, int percentComplete) {
            Event<PackageRemoveProgress>.Raise(canonicalName, percentComplete);
        }

        public void InstalledPackage(string canonicalName) {
            _packages.Value.Add(Package.GetPackage(canonicalName));
        }

        public void RemovedPackage(string canonicalName) {
            _packages.Value.Add(Package.GetPackage(canonicalName));
        }

        public void FailedPackageInstall(string canonicalName, string filename, string reason) {
            throw new CoAppException("Package Failed Install {0} => {1}".format(canonicalName, reason));
        }

        public void FailedPackageRemoval(string canonicalName, string reason) {
            throw new FailedPackageRemoveException(canonicalName, reason);
        }

        public void RequireRemoteFile(string canonicalName, IEnumerable<string> remoteLocations, string destination, bool force) {
            var targetFilename = Path.Combine(destination, canonicalName);
            lock (CurrentDownloads) {

                if (CurrentDownloads.ContainsKey(targetFilename)) {
                    // wait for this guy to respond (which should give us what we need)
                    CurrentDownloads[targetFilename].Continue(() => {
                        if (File.Exists(targetFilename)) {
                            Event<DownloadCompleted>.Raise(canonicalName, targetFilename);
                            PM.RecognizeFile(canonicalName, targetFilename, remoteLocations.FirstOrDefault());
                        }
                        return;
                    });
                    return;
                }

                // gotta download the file...
                var task = Task.Factory.StartNew(() => {
                    foreach (var location in remoteLocations) {
                        try {
                            // a filesystem location (remote or otherwise)
                            var uri = new Uri(location);
                            if (uri.IsFile) {
                                // try to copy the file local.
                                var remoteFile = uri.AbsoluteUri.CanonicalizePath();

                                // if this fails, we'll just move down the line.
                                File.Copy(remoteFile, targetFilename);
                                PM.RecognizeFile(canonicalName, targetFilename, uri.AbsoluteUri);
                                return;
                            }

                            // A web location
                            Task progressTask = null;
                            var success = false;
                            var rf = new RemoteFile(uri, targetFilename,
                                completed: (itemUri) => {
                                    PM.RecognizeFile(canonicalName, targetFilename, uri.AbsoluteUri);
                                    Event<DownloadCompleted>.Raise(canonicalName, targetFilename);
                                    // remove it from the list of current downloads
                                    CurrentDownloads.Remove(targetFilename);
                                    success = true;
                                },

                                failed: (itemUri) => {
                                    success = false;
                                },

                                progress: (itemUri, percent) => {
                                    if (progressTask == null) {
                                        // report progress to the engine
                                        progressTask = PM.DownloadProgress(canonicalName, percent);
                                        progressTask.Continue(() => {
                                            progressTask = null;
                                        });
                                    }

                                    Event<DownloadProgress>.Raise(canonicalName, targetFilename,percent);
                                });

                            rf.Get();

                            if (success && File.Exists(targetFilename)) {
                                return;
                            }

                        }
                        catch (Exception e) {
                            // bogus, dude.
                            // try the next one.
                            Logger.Error(e);
                        }
                        // loop around and try again?
                    }

                    // was there a file there from before?
                    if (File.Exists(targetFilename)) {
                        Event<DownloadCompleted>.Raise(canonicalName, targetFilename);
                        PM.RecognizeFile(canonicalName, targetFilename, remoteLocations.FirstOrDefault());
                    }

                    // remove it from the list of current downloads
                    CurrentDownloads.Remove(targetFilename);

                    // if we got here, that means we couldn't get the file. too bad, so sad.
                    PM.UnableToAcquire(canonicalName);
                }, TaskCreationOptions.AttachedToParent);

                CurrentDownloads.Add(targetFilename, task);
            }

        }

        public void SignatureValidation(string filename, bool isValid, string certificateSubjectName) {
            IsSignatureValid = isValid;
        }

        public void PermissionRequired(string policyRequired) {
            throw new RequiresPermissionException(policyRequired);
        }

        public void Error(string messageName, string argumentName, string problem) {
            if (messageName == "add-feed") {
                throw new CoAppException(problem);
            }
            throw new CoAppException("Message Argument Exception [{0}/{1}/{2}]".format(messageName, argumentName, problem));
        }

        public void Warning(string messageName, string argumentName, string problem) {
            // throw new NotImplementedException();
        }

        public void PackageSatisfiedBy(string requestedCanonicalName, string satisfiedByCanonicalName) {
            var pkg = Package.GetPackage(requestedCanonicalName);
            pkg.SatisfiedBy = Package.GetPackage(satisfiedByCanonicalName);
            _packages.Value.Add(pkg);
        }

        public void FeedAdded(string location) {
            _feeds.Value.Add( new Feed {Location = location});
        }

        public void FeedRemoved(string location) {
            _feeds.Value.Add(new Feed { Location = location });
        }

        public void FileNotFound(string filename) {
            throw new NotImplementedException();
        }

        public void UnknownPackage(string canonicalName) {
            throw new UnknownPackageException(canonicalName);
        }

        public void PackageBlocked(string canonicalName) {
            throw new PackageBlockedException(canonicalName);
        }

        public void FileNotRecognized(string filename, string reason) {
            throw new NotImplementedException();
        }

        public void UnexpectedFailure(string type, string failure, string stacktrace) {
            throw new CoAppException(failure);
        }

        public void FeedSuppressed(string location) {
            _feeds.Value.Add(new Feed { Location = location });
        }

        public void SendKeepAlive() {
            throw new NotImplementedException();
        }

        public void OperationCanceled(string message) {
            OperationCanceledReason = message;
        }

        public void PackageHasPotentialUpgrades(string packageCanonicalName, IEnumerable<string> supercedents) {
            UpgradablePackage = Package.GetPackage(packageCanonicalName);
            PotentialUpgrades = supercedents.Select(Package.GetPackage);
        }

        public void ScheduledTaskInfo(string taskName, string executable, string commandline, int hour, int minutes, DayOfWeek? dayOfWeek, int intervalInMinutes) {
            _scheduledTasks.Value.Add(new ScheduledTask {
                Name = taskName,
                Executable = executable,
                CommandLine = commandline,
                Hour = hour,
                Minutes = minutes,
                DayOfWeek = dayOfWeek,
                IntervalInMinutes = intervalInMinutes
            });
        }

        public void CurrentTelemetryOption(bool optIntoTelemetryTracking) {
            OptedIn = optIntoTelemetryTracking;
        }

        public void NoFeedsFound() {
            // throw new NotImplementedException();
        }

        public void Restarting() {
            EngineRestarting = true;
            // throw an exception here to quickly short circuit the rest of this call
            throw new Exception("restarting");
        }

        public void SendShuttingDown() {
            // nothing to do here but smile!
        }

        public void UnableToDownloadPackage(string packageCanonicalName) {
            throw new NotImplementedException();
        }

        public void UnableToInstallPackage(string packageCanonicalName) {
            throw new PackageInstallFailedException(Package.GetPackage(packageCanonicalName));
        }

        public void Recognized(string location) {
            // nothing to do here but smile!
        }

        public void TaskComplete() {
            // nothing to do here but smile!
        }

        public void LoggingSettings(bool messages, bool warnings, bool errors) {
            LoggingSettingsResult = new LoggingSettings {Messages = messages, Warnings = warnings, Errors = errors};
        }
    }

    public class PackageManager : OutgoingCallDispatcher {
        internal class ManualEventQueue : Queue<UrlEncodedMessage>, IDisposable {
            internal static readonly Dictionary<int, ManualEventQueue> EventQueues = new Dictionary<int, ManualEventQueue>();
            internal readonly ManualResetEvent ResetEvent = new ManualResetEvent(true);
            internal bool StillWorking;

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
                ResetEvent.Set();
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
                        q.StillWorking = false;
                        q.ResetEvent.Set();
                    }
                }
            }
        }

        public static IPackageManager RemoteService;
        private static PackageManager _instance = new PackageManager();
        private static readonly ManualResetEvent IsBufferReady = new ManualResetEvent(false);

        private static NamedPipeClientStream _pipe;
        internal const int BufferSize = 1024*1024*2;

        public static int ActiveCalls {
            get { return ManualEventQueue.EventQueues.Keys.Count; }
        }

        public static bool IsServiceAvailable {
            get { return EngineServiceManager.Available; }
        }

        public static bool IsConnected {
            get {  return IsServiceAvailable && _pipe != null && _pipe.IsConnected; }
        }

        private PackageManager() : base(WriteAsync) {
            RemoteService = this.ActLike();
        }

        /// <summary>
        /// This dispatcher wraps the dispatch of the remote call in a Task (by continuing on the Connect()) 
        /// which allows the client to continue working asynchronously while the service
        /// is doing it's thing.
        /// </summary>
        /// <param name="binder"></param>
        /// <param name="args"></param>
        /// <param name="result"></param>
        /// <returns></returns>
        public override bool TryInvokeMember(InvokeMemberBinder binder, object[] args, out object result) {
            result = Connect().Continue(() => {
                using (var eventQueue = new ManualEventQueue()) {
                    // create return message handler
                    var responseHandler = new CallResponse();
                    CurrentTask.Events += new GetCurrentRequestId(() => ""+Task.CurrentId);

                    do {
                        // unhook the old one if it's there.
                        responseHandler.Clear();

                        // send OG message here!
                        object callResult;
                        base.TryInvokeMember(binder, args, out callResult);

                        // will return when the final message comes thru.
                        eventQueue.StillWorking = true;

                        while (eventQueue.StillWorking && eventQueue.ResetEvent.WaitOne()) {
                            eventQueue.ResetEvent.Reset();
                            while (eventQueue.Count > 0) {
                                if(!Event<GetResponseDispatcher>.RaiseFirst().DispatchSynchronous(eventQueue.Dequeue())) {
                                    eventQueue.StillWorking = false;
                                }
                            }
                        }
                    } while (responseHandler.EngineRestarting);

                    // this returns the final response back via the Task<*> 
                    return responseHandler;
                }
            });
            return true;
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

        internal static Task Connect() {
            return Connect(Process.GetCurrentProcess().Id.ToString());
        }

        private static Task _connectingTask;
        private static int _autoConnectionCount;

        internal static Task Connect(string clientName, string sessionId = null) {
            if (IsConnected) {
                return "Completed".AsResultTask();
            }

            lock (typeof(PackageManager)) {
                if (_connectingTask == null) {
                    IsBufferReady.Reset();

                    _connectingTask = Task.Factory.StartNew(() => {
                        EngineServiceManager.EnsureServiceIsResponding();

                        sessionId = sessionId ?? Process.GetCurrentProcess().Id.ToString() + "/" + _autoConnectionCount++;

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
                    }, TaskCreationOptions.AttachedToParent);
                }
            }

            return _connectingTask;
        }

        
        private static void ProcessMessages() {
            var incomingMessage = new byte[BufferSize];
            IsBufferReady.Set();

            try {
                do {
                    // we need to wait for the buffer to become available.
                    IsBufferReady.WaitOne();
                    
                    // now we claim the buffer 
                    IsBufferReady.Reset();

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

                                // lazy log the response (since we're at the end of this task)
                                Logger.Message("Response:{0}".format(responseMessage.ToSmallerString()));

                                var rqid = responseMessage.GetValueAsNullable("rqid", typeof(int)) as int?;
                                try {
                                    var queue = ManualEventQueue.GetQueue(rqid.GetValueOrDefault());
                                    if (queue != null) {
                                        queue.Enqueue(responseMessage);
                                    }
                                    //else {
                                    // GS01 : Need to put in protocol version detection.  
                                    //}
                                } catch {
                                }
                            }
                            // it's ok to let the next readTask use the buffer, we've got the data out & queued.
                            IsBufferReady.Set();
                        }).AutoManage();

                    // this wait just makes sure that we're only asking for one message at a time
                    // but does not throttle the messages themselves.
                    // readTask.Wait();
                } while (IsConnected);
            }
            catch (Exception e) {
                Logger.Message("Connection Terminating with Exception {0}/{1}", e.GetType(), e.Message);
            }
            finally {
                Disconnect();
            }
        }

        public static void Disconnect() {
            lock (typeof(PackageManager)) {
                _connectingTask = null;

                try {
                    if (_pipe != null) {
                        // ensure all queues are stopped and cleared out.
                        ManualEventQueue.ResetAllQueues();
                        IsBufferReady.Set();
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

        /// <summary>
        ///   Writes the message to the stream asyncly.
        /// </summary>
        /// <param name = "message">The request.</param>
        /// <returns></returns>
        /// <remarks>
        /// </remarks>
        private static void WriteAsync(UrlEncodedMessage message) {
            if (IsConnected) {
                try {
                    message.Add("rqid", Event<GetCurrentRequestId>.RaiseFirst());
                    _pipe.WriteLineAsync(message.ToString()).ContinueWith(antecedent => { Console.WriteLine("Async Write Fail!? (1)"); },
                        TaskContinuationOptions.OnlyOnFaulted);
                }
                catch /* (Exception e) */ {
                    
                }
            }
        }

        private static void StartSession(string clientId, string sessionId) {
            WriteAsync(new UrlEncodedMessage("StartSession") {
                {"client", clientId},
                {"id", sessionId},
                {"rqid", sessionId},
            });
        }
    }
}