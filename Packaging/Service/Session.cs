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
    using System.IO.Pipes;
    using System.Linq;
    using System.Security.Principal;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Common;
    using Toolkit.Collections;
    using Toolkit.Extensions;
    using Toolkit.ImpromptuInterface;
    using Toolkit.Logging;
    using Toolkit.Pipes;
    using Toolkit.Tasks;
    using Toolkit.Win32;

    /// <summary>
    /// </summary>
    /// <remarks>
    /// </remarks>
    public class Session {
#if DEBUG
        // keep the reconnect window at 5 seconds for debugging
        private static readonly TimeSpan MaxDisconenctedWait = new TimeSpan(0, 0, 0, 2);
#else
    // twenty seconds in the real world
        private static TimeSpan MaxDisconenctedWait = new TimeSpan(0, 0, 00, 20);

#endif
        private static TimeSpan _synchronousClientHeartbeat = new TimeSpan(0, 0, 0, 0, 650);

        protected static DateTime LastActivity { get; set; }

        /// <summary>
        /// </summary>
        private static readonly List<Session> ActiveSessions = new List<Session>();

        /// <summary>
        /// </summary>
        private readonly string _clientId;

        /// <summary>
        /// </summary>
        private readonly string _sessionId;

        /// <summary>
        /// </summary>
        private readonly string _userId;

        /// <summary>
        /// </summary>
        private readonly bool _isElevated;

        /// <summary>
        /// </summary>
        private NamedPipeServerStream _serverPipe;

        /// <summary>
        /// </summary>
        private NamedPipeServerStream _responsePipe;

        private bool _ended;

        private readonly ManualResetEvent _resetEvent = new ManualResetEvent(true);

        private bool _waitingForClientResponse;
        private readonly Task _task;
        private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
        private readonly bool _isAsychronous = true;

        private readonly OutgoingCallDispatcher _outgoingDispatcher;
        private readonly IPackageManagerResponse _dispatcher;

        private bool Connected {
            get {
                return _resetEvent.WaitOne(0);
            }
            set {
                if (value) {
                    _resetEvent.Set();
                } else {
                    _resetEvent.Reset();
                }
            }
        }

        public static void CancelAll() {
            while (HasActiveSessions) {
                var session = ActiveSessions.FirstOrDefault();
                if (session != null) {
                    session.End();
                }
            }
        }

        private static void Add(Session session) {
            lock (ActiveSessions) {
                ActiveSessions.Add(session);
            }
        }

        public static void NotifyClientsOfRestart() {
            foreach (var s in ActiveSessions.ToArray()) {
                // s.Restarting(); GS01?
                // cancel everyone.
                s._cancellationTokenSource.Cancel();
            }
        }

        public static bool HasActiveSessions {
            get {
                return ActiveSessions.Any();
            }
        }

        /// <summary>
        ///   Starts the session.
        /// </summary>
        /// <param name="clientId"> The client id. </param>
        /// <param name="sessionId"> The session id. </param>
        /// <param name="serverPipe"> The server pipe. </param>
        /// <param name="responsePipe"> The response pipe. </param>
        /// <remarks>
        /// </remarks>
        public static void Start(string clientId, string sessionId, NamedPipeServerStream serverPipe, NamedPipeServerStream responsePipe) {
            var isElevated = false;
            var userId = string.Empty;

            serverPipe.RunAsClient(() => {
                var identity = WindowsIdentity.GetCurrent();
                if (identity != null) {
                    userId = identity.Name;
                    isElevated = AdminPrivilege.IsProcessElevated();
                }
            });

            var existingSessions = (from session in ActiveSessions
                where session._clientId == clientId && session._sessionId == sessionId && isElevated == session._isElevated && session._userId == userId
                select session).ToList();

            if (existingSessions.Any()) {
                if (existingSessions.Count() > 1) {
                    // multiple matching sessions? This isn't good. Shut em all down to be safe 
                    foreach (var each in existingSessions) {
                        each.End();
                    }
                } else {
                    var session = existingSessions.FirstOrDefault();
                    if (session != null) {
                        // found just one session.
                        session._serverPipe = serverPipe;
                        session._responsePipe = responsePipe;
                        Logger.Message("Rejoining existing session...");
                        // session.SendSessionStarted(sessionId);
                        session.Connected = true;
                        session.SendQueuedMessages();
                    }
                    return;
                }
            } else {
                // if the exact session isn't there, find any that are partial matches, and shut em down.
                foreach (
                    var each in (from session in ActiveSessions where session._clientId == clientId && session._sessionId == sessionId select session).ToList()
                    ) {
                    each.End();
                }
            }
            // no viable matching session.
            // Let's start a new one.
            Add(new Session(clientId, sessionId, serverPipe, responsePipe, userId, isElevated));
            Logger.Message("Starting new session...");
        }

        public void End() {
            if (!_ended) {
                _ended = true;

                // remove this session.
                lock (ActiveSessions) {
                    ActiveSessions.Remove(this);
                }

                if (!HasActiveSessions) {
                    Task.Factory.StartNew(() => {
                        Thread.Sleep(61*60*1000); // 11 minutes
                        if (!HasActiveSessions && DateTime.Now.Subtract(LastActivity) > new TimeSpan(0, 60, 0)) {
                            // no active sessions
                            // more than 60 minutes since last one.
                            // nighty-night!
                            EngineServiceManager.TryToStopService();
                            Logger.Message("Service getting sleepy. Going nighty-night");
                        }
                    });
                }

                Logger.Message("Ending Client: [{0}]-[{1}]".format(_clientId, _sessionId));

                // end any outstanding tasks as gracefully as we can.
                _cancellationTokenSource.Cancel();

                // drop all our local session data.
                _sessionCache.Clear();
                _sessionCache = null;

                // close and clean up the pipes. 
                Disconnect();

                GC.Collect();
            }
        }

        private void Disconnect() {
            _bufferReady.Set();

            lock (this) {
                if (!Connected) {
                    return;
                }
                Connected = false;
            }

            Logger.Message("disposing of pipes: [{0}]-[{1}]".format(_clientId, _sessionId));
            try {
                if (_serverPipe != null) {
                    _serverPipe.Close();
                }
                _serverPipe = null;

                if (!_isAsychronous && _responsePipe != null) {
                    _responsePipe.Close();
                }
                _responsePipe = null;
            } catch (Exception e) {
                Logger.Error(e);
            }

            // clean up anything that can be cleaned up.
            FilesystemExtensions.RemoveTemporaryFiles();
        }

        /// <summary>
        ///   Initializes a new instance of the <see cref="Session" /> class.
        /// </summary>
        /// <param name="clientId"> The client id. </param>
        /// <param name="sessionId"> The session id. </param>
        /// <param name="serverPipe"> The server pipe. </param>
        /// <param name="responsePipe"> The response pipe. </param>
        /// <param name="userId"> </param>
        /// <param name="isElevated"> </param>
        /// <remarks>
        /// </remarks>
        protected Session(string clientId, string sessionId, NamedPipeServerStream serverPipe, NamedPipeServerStream responsePipe, string userId,
            bool isElevated) {
            _clientId = clientId;
            _sessionId = sessionId;
            _serverPipe = serverPipe;
            _responsePipe = responsePipe;
            _userId = userId;
            _isElevated = isElevated;
            _isAsychronous = serverPipe == responsePipe;
            Connected = true;

            _outgoingDispatcher = new OutgoingCallDispatcher(WriteAsync);
            _dispatcher = _outgoingDispatcher.ActLike<IPackageManagerResponse>();

            // this session task
            _task = Task.Factory.StartNew(ProcessMesages, _cancellationTokenSource.Token);

            // this task is not attached to a parent anywhere.
            _task.AutoManage();

            // when the task is done, call end.
            _task.ContinueWith(antecedent => End());
            
        }

        private bool IsCanceled {
            get {
                return _cancellationTokenSource.Token.IsCancellationRequested;
            }
        }

        private readonly Queue<string> _outputQueue = new Queue<string>();
        private Task _queueProcessingTask;

        private Task SendQueuedMessages() {
            lock (this) {
                // if another thread is processing this queue, let it.
                if (_queueProcessingTask != null) {
                    return _queueProcessingTask;
                }

                // unlike most other places, this task isn't attached to the parent
                return (_queueProcessingTask = Task.Factory.StartNew(DrainQueue));
            }
        }

        private void DrainQueue() {
            while (true) {
                // if we're done processing messages, make sure nobody thinks we still are before quitting
                lock (this) {
                    bool anymessages;
                    lock (_outputQueue) {
                        anymessages = _outputQueue.Any();
                    }

                    if (_responsePipe == null || IsCanceled || !anymessages) {
                        _queueProcessingTask = null;
                        return;
                    }
                }

                try {
                    string msg;

                    lock (_outputQueue) {
                        msg = _outputQueue.Peek();
                    }

                    // write it out 
                    _responsePipe.WriteLineAsync(msg).Wait();

                    // if the wait() didn't throw, pop the item off the queue
                    lock (_outputQueue) {
                        _outputQueue.Dequeue();
                    }
                } catch /* (Exception e) */ {
                    // hmm. if the wait() threw, we're disconnected.
                    Disconnect();
                }
            }
        }

        /// <summary>
        ///   Writes the message to the stream asyncly.
        /// </summary>
        /// <param name="message"> The request. </param>
        /// <returns> </returns>
        /// <remarks>
        /// </remarks>
        public void WriteAsync(UrlEncodedMessage message) {
            // bail if we're cancelling this request
            if (IsCanceled) {
                return;
            }

            // first, attach a request id to the message
            try {
                message.Add("rqid", Event<GetCurrentRequestId>.RaiseFirst());
            } catch {
                // no worries if we can't get that.
            }

            // next toss the message the queue 
            lock (_outputQueue) {
                _outputQueue.Enqueue(message.ToString());
            }

            if (Connected) {
                SendQueuedMessages();
            }
        }

        private Dictionary<Type, object> _sessionCache = new Dictionary<Type, object>();
        private readonly ManualResetEvent _bufferReady = new ManualResetEvent(true);

        /// <summary>
        ///   Processes the mesages.
        /// </summary>
        /// <remarks>
        /// </remarks>
        private void ProcessMesages() {
            // instantiate the Asynchronous Package Session object (ie, like thread-local-storage, but really, 
            // it's session-local-storage. So for this task and all its children, this will serve up data.

            CurrentTask.Events += new CheckForPermission(policy => {
                try {
                    var result = false;
                    _serverPipe.RunAsClient(() => {
                        result = policy.HasPermission;
                    });
                    if (!result) {
                        Event<GetResponseInterface>.RaiseFirst().PermissionRequired(policy.Name);
                    }
                    return result;
                } catch {
                    // may have been disconnected?
                    if (!_serverPipe.IsConnected) {
                        Disconnect();
                    }
                }
                return false;
            });

            CurrentTask.Events += new IsCancellationRequested(() => _cancellationTokenSource.Token.IsCancellationRequested);
            CurrentTask.Events += new GetCanonicalizedPath(path => {
                var result = path;
                _serverPipe.RunAsClient(() => {
                    try {
                        result = path.CanonicalizePath();
                    } catch {
                        // path didn't canonicalize. Pity.
                    }
                });
                return result;
            });

            CurrentTask.Events += new GetSessionCache((type, constructor) => {
                lock (_sessionCache) {
                    if (!_sessionCache.ContainsKey(type)) {
                        _sessionCache.Add(type, constructor());
                    }
                    return _sessionCache[type];
                }
            });

            Task<int> readTask = null;
            // SendSessionStarted(_sessionId);

            var serverInput = new byte[Engine.BufferSize];

            while (Engine.IsRunning) {
                if (!Connected) {
                    readTask = null;

                    if (IsCanceled) {
                        return;
                    }

                    Logger.Message("Waiting for client to reconnect.");
                    _resetEvent.WaitOne(MaxDisconenctedWait);
                    _waitingForClientResponse = true; // debug, always drop session on timeout.

                    if (IsCanceled || (_waitingForClientResponse && !Connected)) {
                        // we're disconnected, we've waited for the duration, 
                        // we're assuming the client isn't coming back.
                        // End(); // get out of the function ... 
                        return;
                    }
                    continue;
                }

                try {
                    if (IsCanceled) {
                        return;
                    }

                    // if there is currently a task reading the from the stream, let's skip it this time.
                    if (_bufferReady.WaitOne() && Connected) {
                        _bufferReady.Reset();

                        try {
                            // when the readasync command can finally complete, then we know that
                            // it's ok to ask it to read again.
                            readTask = _serverPipe.ReadAsync(serverInput, 0, serverInput.Length).AutoManage();

                            readTask.ContinueWith(antecedent => {
                                if (antecedent.IsFaulted || antecedent.IsCanceled || _serverPipe == null || !_serverPipe.IsConnected) {
                                    Disconnect();
                                    return;
                                }

                                if (antecedent.Result >= Engine.BufferSize) {
                                    _bufferReady.Set();
                                    Event<GetResponseInterface>.RaiseFirst().UnexpectedFailure("CoAppException", "Message size exceeds maximum size allowed.", "");
                                    return;
                                }

                                try {
                                    var rawMessage = Encoding.UTF8.GetString(serverInput, 0, antecedent.Result);
                                    var requestMessage = new UrlEncodedMessage(rawMessage);

                                    if (string.IsNullOrEmpty(requestMessage)) {
                                        return;
                                    }

                                    if (IsCanceled) {
                                        Event<GetResponseInterface>.RaiseFirst().OperationCanceled("Service is shutting down");
                                    } else {
                                        Logger.Message("Request:[{0}]{1}".format(requestMessage.GetValueAsString("rqid"), requestMessage.ToSmallerString()));

                                       
                                        var packageRequestData = new EasyDictionary<string, PackageRequestData>();
                                        var rqid = requestMessage.GetValueAsString("rqid");

                                        CurrentTask.Events += new GetCurrentRequestId(() => rqid );
                                        CurrentTask.Events += new GetResponseInterface(() => _dispatcher);
                                        CurrentTask.Events += new GetRequestPackageDataCache(() => packageRequestData);

                                        var dispatchTask = PackageManagerImpl.Dispatcher.Dispatch(requestMessage);
                                        dispatchTask.ContinueOnFail(failure => {
                                            if (!IsCanceled) {
                                                Logger.Error(failure);
                                                Event<GetResponseInterface>.RaiseFirst().UnexpectedFailure(failure.GetType().Name, failure.Message, failure.StackTrace);
                                            }
                                        });

                                        dispatchTask.ContinueAlways(dt => Event<GetResponseInterface>.RaiseFirst().TaskComplete());
                                    }
                                } finally {
                                    // whatever, after this point let the messages flow!
                                    _bufferReady.Set();
                                }
                            }).AutoManage();
                            readTask.ContinueOnFail(failure => {
                                if (!IsCanceled) {
                                    Logger.Error(failure);
                                    Event<GetResponseInterface>.RaiseFirst().UnexpectedFailure(failure.GetType().Name, failure.Message, failure.StackTrace);
                                }
                            });
                        } catch /* (Exception e) */ {
                            // if the pipe is broken, let's move to the disconnected state
                            Disconnect();
                        }
                    }
                    if (_isAsychronous) {
                        if (readTask != null) {
                            readTask.Wait(_cancellationTokenSource.Token);
                        }
                    } else {
                        if (readTask != null) {
                            readTask.Wait((int)_synchronousClientHeartbeat.TotalMilliseconds, _cancellationTokenSource.Token);
                        }
                    }

                    if (IsCanceled) {
                        return;
                    }

                    if (!_isAsychronous) {
                        Event<GetResponseInterface>.RaiseFirst().SendKeepAlive();
                    }
                } catch (AggregateException ae) {
                    if (IsCanceled) {
                        // ok, I'll assume you know what you're doing.
                        return;
                    }

                    foreach (var e in ae.Flatten().InnerExceptions) {
                        if (e.GetType() == typeof (IOException)) {
                            // pipe got disconnected.
                            return;
                        }
                        Logger.Error(e);
                    }
                } catch (Exception e) {
                    if (IsCanceled) {
                        // ok, I'll assume you know what you're doing.
                        return;
                    }

                    // something broke. Could be a closed pipe.
                    Logger.Error(e);
                }
            }
        }
    }
}