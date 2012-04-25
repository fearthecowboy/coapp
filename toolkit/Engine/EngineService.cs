//-----------------------------------------------------------------------
// <copyright company="CoApp Project">
//     Copyright (c) 2010 Garrett Serack . All rights reserved.
// </copyright>
// <license>
//     The software is licensed under the Apache 2.0 License (the "License")
//     You may not use the software except in compliance with the License. 
// </license>
//-----------------------------------------------------------------------

using CoApp.Toolkit.Engine.Feeds;
using CoApp.Toolkit.Utility;
using CoApp.Toolkit.Win32;

namespace CoApp.Toolkit.Engine {
    using System;
    using System.Diagnostics;
    using System.IO.Pipes;
    using System.Reflection;
    using System.Runtime.InteropServices;
    using System.Security.AccessControl;
    using System.Security.Principal;
    using System.ServiceProcess;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Extensions;
    using Logging;
    using Microsoft.Win32.SafeHandles;
    using Pipes;
    using Tasks;

    internal static class Signals {
        /*
        [StructLayout(LayoutKind.Sequential)]
        public struct SECURITY_ATTRIBUTES {
            public int nLength;
            public IntPtr lpSecurityDescriptor;
            public int bInheritHandle;
        }
        */

        static Signals() {
            bool wasCreated;
            var ewhSec = new EventWaitHandleSecurity();
            ewhSec.AddAccessRule(new EventWaitHandleAccessRule(new SecurityIdentifier(WellKnownSidType.WorldSid, null), EventWaitHandleRights.Synchronize | EventWaitHandleRights.Modify, AccessControlType.Allow));

            _availableEvent = new EventWaitHandle(false, EventResetMode.ManualReset, "Global\\CoAppAvailable", out wasCreated, ewhSec);
            _startingupEvent = new EventWaitHandle(false, EventResetMode.ManualReset, "Global\\CoAppStartingUp", out wasCreated, ewhSec);
            _shuttingdownEvent = new EventWaitHandle(false, EventResetMode.ManualReset, "Global\\CoAppShuttingDown", out wasCreated, ewhSec);
            _shuttingdownRequestedEvent = new EventWaitHandle(false, EventResetMode.ManualReset, "Global\\CoAppShutdownRequested", out wasCreated, ewhSec);
            _installedEvent = new EventWaitHandle(false, EventResetMode.ManualReset, "Global\\CoAppInstalledPackage", out wasCreated, ewhSec);
            _removedEvent = new EventWaitHandle(false, EventResetMode.ManualReset, "Global\\CoAppRemovedPackage", out wasCreated, ewhSec);
        }

        // private static readonly SafeWaitHandle _availableEvent = Kernel32.CreateEvent(IntPtr.Zero, true, false, "Global\\CoAppAvailable");
        private static readonly EventWaitHandle _availableEvent;
        private static readonly EventWaitHandle _startingupEvent;
        private static readonly EventWaitHandle _shuttingdownEvent;
        private static readonly EventWaitHandle _shuttingdownRequestedEvent;
        private static readonly EventWaitHandle _installedEvent;
        private static readonly EventWaitHandle _removedEvent;

        private static bool _available;
        public static bool Available {

            get { return _available; }
            set {
                _available = value;
                _availableEvent.Reset();

                if (value) {
                    StartingUp = false;
                    ShuttingDown = false;
                    _availableEvent.Set();
                }
                
            }
        }

        private static bool _startingUp;
        public static bool StartingUp {
            get { return _startingUp; }
            set {
                _startingUp = value;
                _startingupEvent.Reset();

                if (value) {
                    Available = false;
                    ShuttingDown = false;
                    _startingupEvent.Set();
                }
            }
        }

        private static bool _shuttingDown;
        public static bool ShuttingDown {
            get { return _shuttingDown; }
            set {
                _shuttingDown = value;
                _shuttingdownEvent.Reset();
                if (value) {
                    StartingUp = false;
                    Available = false;
                    _shuttingdownEvent.Set();
                }
            }
        }

        private static bool _shutdownRequested;
        public static bool ShutdownRequested {
            get { return _shutdownRequested; }
            set {
                _shutdownRequested = value;
                _shuttingdownRequestedEvent.Reset();
                if (value) {
                    _shuttingdownRequestedEvent.Set();
                }
            }
        }

        public static void InstalledPackage(string canonicalPackageName) {
            Task.Factory.StartNew(() => {
                PackageManagerSettings.CoAppInformation["InstalledPackages"].StringsValue =
                    PackageManagerSettings.CoAppInformation["InstalledPackages"].StringsValue.UnionSingleItem(canonicalPackageName);
                _installedEvent.Reset();
                _installedEvent.Set();
                Thread.Sleep(100); // give everyone a chance to wake up and do their job
                _installedEvent.Reset();
            });
        }

        public static void RemovedPackage(string canonicalPackageName) {
            Task.Factory.StartNew(() => {
                PackageManagerSettings.CoAppInformation["RemovedPackages"].StringsValue =
                    PackageManagerSettings.CoAppInformation["RemovedPackages"].StringsValue.UnionSingleItem(canonicalPackageName);
                _removedEvent.Reset();
                _removedEvent.Set();
                Thread.Sleep(100); // give everyone a chance to wake up and do their job
                _removedEvent.Reset();
            });
        }

        public static int EngineStartupStatus { 
            get {
                return PackageManagerSettings.CoAppInformation["StartupPercentComplete"].IntValue;
            } 
            set {
                PackageManagerSettings.CoAppInformation["StartupPercentComplete"].IntValue = value;
                if (value > 0 && value < 100) {
                    StartingUp = true;
                }
            }
        }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <remarks></remarks>
    public class EngineService {
        /// <summary>
        /// 
        /// </summary>
        private const string PipeName = @"CoAppInstaller";
        /// <summary>
        /// 
        /// </summary>
        private const string OutputPipeName = @"CoAppInstaller-";

        public static bool IsInteractive { get; set; }

        /// <summary>
        /// 
        /// </summary>
        private const int Instances = -1;
        /// <summary>
        /// 
        /// </summary>
        internal const int BufferSize = 8192;
        /// <summary>
        /// 
        /// </summary>
        private static readonly Lazy<EngineService> _instance = new Lazy<EngineService>(() => new EngineService());

        /// <summary>
        /// 
        /// </summary>
        private bool _isRunning;

        /// <summary>
        /// 
        /// </summary>
        private CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
        /// <summary>
        /// 
        /// </summary>
        private PipeSecurity _pipeSecurity;

        private Task _engineService;
        /// <summary>
        /// Stops this instance.
        /// </summary>
        /// <remarks></remarks>
        public static void RequestStop() {
            // this should stop the coapp engine.
            _instance.Value._cancellationTokenSource.Cancel();
            _instance.Value._isRunning = false;
            Signals.ShutdownRequested = true;
        }

        /// <summary>
        /// Starts this instance.
        /// </summary>
        /// <remarks></remarks>
        public static Task Start(bool interactive) {
            // this should spin up a task and start listening for commands
            IsInteractive = interactive;
            Logger.Warning("Starting up Engine in mode: {0}." , interactive ? "[Interactive]":"[Service]");
            return _instance.Value.Main();
        }

        /// <summary>
        /// Gets a value indicating whether this instance is running.
        /// </summary>
        /// <remarks></remarks>
        public static bool IsRunning {
            get { return _instance.Value._isRunning; }
        }

        /// <summary>
        /// Mains this instance.
        /// </summary>
        /// <remarks></remarks>
        private Task Main() {
            if (IsRunning) {
                return _engineService;
            }
            Signals.EngineStartupStatus = 0;

            if(!IsInteractive) {
                EngineServiceManager.EnsureServiceAclsCorrect();
            }

            var npmi = NewPackageManager.Instance;

            _cancellationTokenSource = new CancellationTokenSource();
            _isRunning = true;
            
            Signals.StartingUp = true;
            // make sure coapp is properly set up.
            Task.Factory.StartNew(() => {
                try {
                    Logger.Warning("CoApp Startup Beginning------------------------------------------");

                    // this ensures that composition rules are run for toolkit.
                    Signals.EngineStartupStatus = 5;
                    Package.EnsureCanonicalFoldersArePresent();
                    Signals.EngineStartupStatus = 10;
                    var v = Package.GetCurrentPackageVersion("coapp.toolkit", "1e373a58e25250cb");
                    Signals.EngineStartupStatus = 95;
                    Logger.Warning("CoApp Version : " + v);

                    /*
                     * what can we do if the right version isn't here?
                     * 
                    FourPartVersion thisVersion = Assembly.GetExecutingAssembly().Version();
                    if( thisVersion > v ) {
                        
                    }
                     * */

                    // Completes startup. 

                    Signals.EngineStartupStatus = 100;
                    Signals.Available = true;
                    Logger.Warning("CoApp Startup Finished------------------------------------------");
                } catch (Exception e ) {
                    Logger.Error(e);
                    RequestStop();
                }
            });

            _engineService = Task.Factory.StartNew(() => {
                _pipeSecurity = new PipeSecurity();
                _pipeSecurity.AddAccessRule(new PipeAccessRule(new SecurityIdentifier(WellKnownSidType.WorldSid,null ), PipeAccessRights.ReadWrite, AccessControlType.Allow));
                _pipeSecurity.AddAccessRule(new PipeAccessRule(WindowsIdentity.GetCurrent().Owner, PipeAccessRights.FullControl, AccessControlType.Allow));

                // start a few listeners by default--each listener will also spawn a new empty one.
                StartListener();
                StartListener();
            }, _cancellationTokenSource.Token).AutoManage();
            
            _engineService = _engineService.ContinueWith(antecedent => {
                RequestStop();
                // ensure the sessions are all getting closed.
                Session.CancelAll();
                _engineService = null;
            }, TaskContinuationOptions.AttachedToParent).AutoManage();
            return _engineService;
        }


        private int listenerCount;
        /// <summary>
        /// Starts the listener.
        /// </summary>
        /// <remarks></remarks>
        private void StartListener() {
            if (_cancellationTokenSource.Token.IsCancellationRequested) {
                return;
            }

            try {
                if (IsRunning) {
                    Logger.Message("Starting New Listener {0}", listenerCount++);
                    var serverPipe = new NamedPipeServerStream(PipeName, PipeDirection.InOut, Instances, PipeTransmissionMode.Message, PipeOptions.Asynchronous,
                        BufferSize, BufferSize, _pipeSecurity);
                    var listenTask = Task.Factory.FromAsync(serverPipe.BeginWaitForConnection, serverPipe.EndWaitForConnection, serverPipe);

                    listenTask.ContinueWith(t => {
                        if (t.IsCanceled || _cancellationTokenSource.Token.IsCancellationRequested) {
                            return;
                        }

                        StartListener(); // spawn next one!

                        if (serverPipe.IsConnected) {
                            var serverInput = new byte[BufferSize];

                            serverPipe.ReadAsync(serverInput, 0, serverInput.Length).AutoManage().ContinueWith(antecedent => {
                                var rawMessage = Encoding.UTF8.GetString(serverInput, 0, antecedent.Result);
                                if (string.IsNullOrEmpty(rawMessage)) {
                                    return;
                                }

                                var requestMessage = new UrlEncodedMessage(rawMessage);

                                // first command must be "startsession"
                                if (!requestMessage.Command.Equals("StartSession", StringComparison.CurrentCultureIgnoreCase)) {
                                    return;
                                }

                                // verify that user is allowed to connect.
                                try {
                                    var hasAccess = false;
                                    serverPipe.RunAsClient(() => { hasAccess = PermissionPolicy.Connect.HasPermission; });
                                    if (!hasAccess)
                                        return;
                                }
                                catch {
                                    return;
                                }

                                // check for the required parameters. 
                                // close the session if they are not here.
                                if (string.IsNullOrEmpty(requestMessage.GetValueAsString("id")) || string.IsNullOrEmpty(requestMessage.Data["client"])) {
                                    return;
                                }
                                var isAsync = requestMessage.GetValueAsNullable("async", typeof(bool)) as bool?;

                                if (isAsync.HasValue && isAsync.Value == false) {
                                    StartResponsePipeAndProcessMesages(requestMessage.GetValueAsString("client"), requestMessage.GetValueAsString("id"), serverPipe);
                                }
                                else {
                                    Session.Start(requestMessage.GetValueAsString("client"), requestMessage.GetValueAsString("id"), serverPipe, serverPipe);
                                }
                            }).Wait();
                        }


                    }, _cancellationTokenSource.Token, TaskContinuationOptions.AttachedToParent, TaskScheduler.Current);

                }
            }
            catch /* (Exception e) */ {
                RequestStop();
            }
        }

        public static bool DoesTheServiceNeedARestart {
            get {
                // is this the coapp win32 service process, or is this interactive
                if( IsInteractive ) {
                    Logger.Warning("Service doens't need a restart, since it's interactive");
                    return false;
                }

                Logger.Warning("Checking to see if service needs a restart");
                // what is the version of the process running?
                FourPartVersion currentVersion = Assembly.GetExecutingAssembly().GetName().Version;

                // what is the version of the installed toolkit
                var installedVersion = Package.GetCurrentPackageVersion("coapp.toolkit", "1e373a58e25250cb");
                Logger.Warning("Running Version [{0}] == InstalledVersion [{1}]",currentVersion , installedVersion );

                return installedVersion > currentVersion;
            }
        }

        public static void RestartService() {
            Task.Factory.StartNew(() => {
                try {
                    Logger.Message("Service Restart Order Issued.");
                    // make sure nobody else can connect.
                    RequestStop();

                    // tell the clients to go away.
                    Logger.Message("Telling clients to go away.");
                    Session.NotifyClientsOfRestart();

                    Logger.Message("Waiting up to 10 seconds for clients to disconnect.");
                    // I'll give you 10 seconds to get lost.
                    for (var i = 0; i < 100 && Session.HasActiveSessions; i++) {
                        Thread.Sleep(100);
                    }

                    if (Session.HasActiveSessions) {
                        Logger.Message("Forcing Disconnection of clients.");
                        Session.CancelAll();
                    }
                } catch(Exception e) {
                    Logger.Error(e);
                }
                Logger.Message("Clients should be disconnected; forcing restart");
                Process.Start(new ProcessStartInfo {
                    FileName =EngineServiceManager.CoAppServiceExecutablePath,
                    Arguments = "--restart",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                });
            });
        }


        /// <summary>
        /// Starts the response pipe and process mesages.
        /// </summary>
        /// <param name="clientId">The client id.</param>
        /// <param name="sessionId">The session id.</param>
        /// <param name="serverPipe">The server pipe.</param>
        /// <remarks></remarks>
        private void StartResponsePipeAndProcessMesages(string clientId, string sessionId, NamedPipeServerStream serverPipe) {
            try {
                var channelname = OutputPipeName + sessionId;
                var responsePipe = new NamedPipeServerStream(channelname, PipeDirection.Out, Instances, PipeTransmissionMode.Message, PipeOptions.Asynchronous,
                    BufferSize, BufferSize, _pipeSecurity);
                Task.Factory.FromAsync(responsePipe.BeginWaitForConnection, responsePipe.EndWaitForConnection, responsePipe,
                    TaskCreationOptions.AttachedToParent).ContinueWith(t => {
                        if (responsePipe.IsConnected) {
                            Session.Start(clientId, sessionId, serverPipe, responsePipe);
                        }
                    }, TaskContinuationOptions.AttachedToParent);
            }
            catch (Exception e) {
                Logger.Error(e);
            }
        }
    }
}