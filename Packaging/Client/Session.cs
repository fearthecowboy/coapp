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

namespace CoApp.Packaging.Client {
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Dynamic;
    using System.IO.Pipes;
    using System.Linq;
    using System.Security.Principal;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Common;
    using Toolkit.Collections;
    using Toolkit.Exceptions;
    using Toolkit.Extensions;
    using Toolkit.ImpromptuInterface;
    using Toolkit.Logging;
    using Toolkit.Pipes;
    using Toolkit.Tasks;

    public class Session : OutgoingCallDispatcher {
        internal class ManualEventQueue : Queue<UrlEncodedMessage>, IDisposable {
            internal static readonly IDictionary<int, ManualEventQueue> EventQueues = new XDictionary<int, ManualEventQueue>();
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
                    foreach (var q in oldQueues) {
                        q.StillWorking = false;
                        q.ResetEvent.Set();
                    }
                }
            }
        }

        public static IPackageManager RemoteService;
        private static Session _instance = new Session();
        private static readonly ManualResetEvent IsBufferReady = new ManualResetEvent(false);

        private static NamedPipeClientStream _pipe;
        internal const int BufferSize = 1024*1024*2;

        public static int ActiveCalls {
            get {
                return ManualEventQueue.EventQueues.Keys.Count;
            }
        }

        public static bool IsServiceAvailable {
            get {
                return EngineServiceManager.Available;
            }
        }

        public static bool IsConnected {
            get {
                return IsServiceAvailable && _pipe != null && _pipe.IsConnected;
            }
        }

        private Session() : base(WriteAsync) {
            RemoteService = this.ActLike();
        }

        /// <summary>
        ///   This dispatcher wraps the dispatch of the remote call in a Task (by continuing on the Connect()) which allows the client to continue working asynchronously while the service is doing it's thing.
        /// </summary>
        /// <param name="binder"> </param>
        /// <param name="args"> </param>
        /// <param name="result"> </param>
        /// <returns> </returns>
        public override bool TryInvokeMember(InvokeMemberBinder binder, object[] args, out object result) {
            result = Connect().Continue(() => {
                using (var eventQueue = new ManualEventQueue()) {
                    // create return message handler
                    var responseHandler = new PackageManagerResponseImpl();
                    CurrentTask.Events += new GetCurrentRequestId(() => "" + Task.CurrentId);

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
                                if (!Event<GetResponseDispatcher>.RaiseFirst().DispatchSynchronous(eventQueue.Dequeue())) {
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
        ///   DEPRECATED Making this deprecated. Client library should be smart enough to connect without being told to.
        /// </summary>
        /// <param name="clientName"> </param>
        /// <param name="sessionId"> </param>
        /// <param name="millisecondsTimeout"> </param>
        public void ConnectAndWait(string clientName, string sessionId = null, int millisecondsTimeout = 5000) {
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

            lock (typeof (Session)) {
                if (_connectingTask == null) {
                    IsBufferReady.Reset();

                    _connectingTask = Task.Factory.StartNew(() => {
                        EngineServiceManager.EnsureServiceIsResponding();

                        sessionId = sessionId ?? Process.GetCurrentProcess().Id.ToString() + "/" + _autoConnectionCount++;

                        for (int count = 0; count < 5; count++) {
                            _pipe = new NamedPipeClientStream(".", "CoAppInstaller", PipeDirection.InOut, PipeOptions.Asynchronous, TokenImpersonationLevel.Impersonation);
                            try {
                                _pipe.Connect(500);
                                _pipe.ReadMode = PipeTransmissionMode.Message;
                                break;
                            } catch {
                                // it's not connecting.
                                _pipe = null;
                            }
                        }

                        if (_pipe == null) {
                            throw new CoAppException("Unable to connect to CoApp Service");
                        }

                        StartSession(clientName, sessionId);
                        Task.Factory.StartNew(ProcessMessages, TaskCreationOptions.None).AutoManage();
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
                                var rqid = responseMessage.GetValueAsNullable("rqid", typeof (int)) as int?;

                                // lazy log the response (since we're at the end of this task)
                                Logger.Message("Response:[{0}]{1}".format(rqid, responseMessage.ToSmallerString()));

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
            } catch (Exception e) {
                Logger.Message("Connection Terminating with Exception {0}/{1}", e.GetType(), e.Message);
            } finally {
                Disconnect();
            }
        }

        public static void Disconnect() {
            lock (typeof (Session)) {
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
        /// <param name="message"> The request. </param>
        /// <returns> </returns>
        /// <remarks>
        /// </remarks>
        private static void WriteAsync(UrlEncodedMessage message) {
            if (IsConnected) {
                try {
                    message.Add("rqid", Event<GetCurrentRequestId>.RaiseFirst());
                    _pipe.WriteLineAsync(message.ToString()).ContinueWith(antecedent => Logger.Error("Async Write Fail!? (1)"),
                        TaskContinuationOptions.OnlyOnFaulted);
                } catch /* (Exception e) */ {
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