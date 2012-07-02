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
    using System.Diagnostics;
    using System.IO;
    using System.IO.Pipes;
    using System.Linq;
    using System.Reflection;
    using System.Security.AccessControl;
    using System.Security.Principal;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Common;
    using Feeds;
    using Toolkit.Collections;
    using Toolkit.Extensions;
    using Toolkit.Logging;
    using Toolkit.Pipes;
    using Toolkit.Tasks;
    using Toolkit.Win32;

    /// <summary>
    /// </summary>
    /// <remarks>
    /// </remarks>
    public class Engine {
        /// <summary>
        /// </summary>
        private const string PipeName = @"CoAppInstaller";

        /// <summary>
        /// </summary>
        private const string OutputPipeName = @"CoAppInstaller-";

        public static bool IsInteractive { get; set; }

        private static readonly string[] CanonicalFolders = new[] {
            ".cache", 
            "ReferenceAssemblies", 
            "ReferenceAssemblies\\x86", 
            "ReferenceAssemblies\\x64", 
            "ReferenceAssemblies\\any", 
            "x86", 
            "x64", 
            "bin", 
            "powershell", 
            "lib", 
            "include", 
            "etc"
        };

        /// <summary>
        /// </summary>
        private const int Instances = -1;

        /// <summary>
        /// </summary>
        internal const int BufferSize = 2 * 1024 * 1024;

        /// <summary>
        /// </summary>
        private static readonly Lazy<Engine> _instance = new Lazy<Engine>(() => new Engine());

        /// <summary>
        /// </summary>
        private bool _isRunning;

        /// <summary>
        /// </summary>
        private CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();

        /// <summary>
        /// </summary>
        private PipeSecurity _pipeSecurity;

        private Task _engineTask;

        /// <summary>
        ///   Stops this instance.
        /// </summary>
        /// <remarks>
        /// </remarks>
        public static void RequestStop() {
            // this should stop the coapp engine.
            Signals.ShutdownRequested = true;
            Signals.ShuttingDown = true;
            _instance.Value._cancellationTokenSource.Cancel();
            _instance.Value._isRunning = false;
            
        }

        /// <summary>
        ///   Starts this instance.
        /// </summary>
        /// <remarks>
        /// </remarks>
        public static Task Start(bool interactive) {
            // this should spin up a task and start listening for commands
            IsInteractive = interactive;
            Logger.Warning("Starting up Engine in mode: {0}.", interactive ? "[Interactive]" : "[Service]");
            return _instance.Value.Main();
        }

        /// <summary>
        ///   Gets a value indicating whether this instance is running.
        /// </summary>
        /// <remarks>
        /// </remarks>
        public static bool IsRunning {
            get {
                return _instance.Value._isRunning;
            }
        }

        /// <summary>
        ///   Mains this instance.
        /// </summary>
        /// <remarks>
        /// </remarks>
        private Task Main() {
            if (IsRunning) {
                return _engineTask;
            }
            Signals.EngineStartupStatus = 0;

            if (!IsInteractive) {
                EngineServiceManager.EnsureServiceAclsCorrect();
            }

            var npmi = PackageManagerImpl.Instance;

            _cancellationTokenSource = new CancellationTokenSource();
            _isRunning = true;

            Signals.StartingUp = true;
            // make sure coapp is properly set up.
            Task.Factory.StartNew(() => {
                try {
                    Logger.Warning("CoApp Startup Beginning------------------------------------------");

                    // this ensures that composition rules are run for toolkit.
                    Signals.EngineStartupStatus = 5;
                    EnsureCanonicalFoldersArePresent();
                    Signals.EngineStartupStatus = 10;
                    var v = Package.GetCurrentPackageVersion(CanonicalName.CoAppItself);
                    if( v > 0 ) {
                        var p = InstalledPackageFeed.Instance.FindPackages(CanonicalName.CoAppItself).HighestPackages().FirstOrDefault();
                        if( p != null ) {
                            p.DoPackageComposition();
                            // put the coapp.exe symlink right into the OS directory. Yeah, evil, .... what are ya gonna do about that?
                            var coappexe = Path.Combine(p.PackageDirectory, "coapp.exe");
                            if (File.Exists(coappexe)) {
                                Symlink.MakeFileLink(Path.Combine(Environment.GetEnvironmentVariable("SYSTEMROOT") ?? "c:\\windows", "coapp.exe"), coappexe);
                            }
                        }
                    }
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
                } catch (Exception e) {
                    Logger.Error(e);
                    RequestStop();
                }
            });

            _engineTask = Task.Factory.StartNew(() => {
                _pipeSecurity = new PipeSecurity();
                _pipeSecurity.AddAccessRule(new PipeAccessRule(new SecurityIdentifier(WellKnownSidType.WorldSid, null), PipeAccessRights.ReadWrite, AccessControlType.Allow));
                _pipeSecurity.AddAccessRule(new PipeAccessRule(WindowsIdentity.GetCurrent().Owner, PipeAccessRights.FullControl, AccessControlType.Allow));

                // start a few listeners by default--each listener will also spawn a new empty one.
                StartListener();
                StartListener();
            }, _cancellationTokenSource.Token).AutoManage();

            _engineTask = _engineTask.ContinueWith(antecedent => {
                RequestStop();
                // ensure the sessions are all getting closed.
                Session.CancelAll();
                _engineTask = null;
            }, TaskContinuationOptions.AttachedToParent).AutoManage();
            return _engineTask;
        }

        private int listenerCount;

        private static XList<NamedPipeServerStream> nonConnectedListeners = new XList<NamedPipeServerStream>();

        /// <summary>
        ///   Starts the listener.
        /// </summary>
        /// <remarks>
        /// </remarks>
        private void StartListener() {
            if (_cancellationTokenSource.Token.IsCancellationRequested) {
                return;
            }

            try {
                if (IsRunning) {
                    Logger.Message("Starting New Listener {0}", listenerCount++);
                    var serverPipe = new NamedPipeServerStream(PipeName, PipeDirection.InOut, Instances, PipeTransmissionMode.Message, PipeOptions.Asynchronous,
                        BufferSize, BufferSize, _pipeSecurity);

                    nonConnectedListeners.Add(serverPipe);

                    var listenTask = Task.Factory.FromAsync(serverPipe.BeginWaitForConnection, serverPipe.EndWaitForConnection, serverPipe);

                    listenTask.ContinueWith(t => {

                        nonConnectedListeners.Remove(serverPipe);
                        if (t.IsCanceled || _cancellationTokenSource.Token.IsCancellationRequested || Signals.ShutdownRequested || Signals.ShuttingDown) {
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
                                    serverPipe.RunAsClient(() => {
                                        hasAccess = PermissionPolicy.Connect.HasPermission;
                                    });
                                    if (!hasAccess) {
                                        return;
                                    }
                                } catch {
                                    return;
                                }

                                // check for the required parameters. 
                                // close the session if they are not here.
                                if (string.IsNullOrEmpty(requestMessage["id"]) || string.IsNullOrEmpty(requestMessage["client"])) {
                                    return;
                                }
                                var isSync = requestMessage["async"].IsFalse();

                                if (isSync) {
                                    StartResponsePipeAndProcessMessages(requestMessage["client"], requestMessage["id"], serverPipe);
                                } else {
                                    Session.Start(requestMessage["client"], requestMessage["id"], serverPipe, serverPipe);
                                }
                            }).Wait();
                        }
                    }, _cancellationTokenSource.Token, TaskContinuationOptions.AttachedToParent, TaskScheduler.Current);
                }
            } catch /* (Exception e) */ {
                RequestStop();
            }
        }

        public static bool DoesTheServiceNeedARestart {
            get {
                // is this the coapp win32 service process, or is this interactive
                if (IsInteractive) {
                    Logger.Warning("Service doens't need a restart, since it's interactive");
                    return false;
                }

                Logger.Warning("Checking to see if service needs a restart");
                // what is the version of the process running?
                FourPartVersion currentVersion = Assembly.GetExecutingAssembly().GetName().Version;

                // what is the version of the installed toolkit
                var installedVersion = Package.GetCurrentPackageVersion(CanonicalName.CoAppItself);
                Logger.Warning("Running Version [{0}] == InstalledVersion [{1}]", currentVersion, installedVersion);

                return installedVersion > currentVersion;
            }
        }

        public static void RestartService(int msec = 0) {
            var response = Event<GetResponseInterface>.RaiseFirst();
            if (response != null) {
                response.Restarting();
            }

            Task.Factory.StartNew(() => {
                try {

                    Signals.ShutdownRequested = true;
                    foreach( var p in nonConnectedListeners ) {
                        p.Dispose();
                    }
                    if( msec > 0 ) {
                        Thread.Sleep(msec);
                    }
                    Logger.Message("Telling clients to go away.");
                    Session.NotifyClientsOfRestart();

                    Logger.Message("Service Restart Order Issued.");
                    // make sure nobody else can connect.
                    RequestStop();

                    
                    Logger.Message("Waiting up to 10 seconds for clients to disconnect.");
                    // I'll give you 10 seconds to get lost.
                    for (var i = 0; i < 100 && Session.HasActiveSessions; i++) {
                        Thread.Sleep(100);
                    }

                    if (Session.HasActiveSessions) {
                        Logger.Message("Forcing Disconnection of clients.");
                        Session.CancelAll();
                    }
                } catch (Exception e) {
                    Logger.Error(e);
                }
                Logger.Message("Clients should be disconnected; forcing restart");
                Process.Start(new ProcessStartInfo {
                    FileName = EngineServiceManager.CoAppServiceExecutablePath,
                    Arguments = "--restart",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                });
            });
        }

        internal static void EnsureCanonicalFoldersArePresent() {
            var root = PackageManagerSettings.CoAppRootDirectory;
            // try to make a convenience symlink:  %SYSTEMDRIVE%:\apps
            var appsdir = "{0}:\\apps".format(root[0]);
            if (!Directory.Exists(appsdir) && !File.Exists(appsdir)) {
                Symlink.MakeDirectoryLink("{0}:\\apps".format(root[0]), root);
            }

            var pathext=EnvironmentUtility.GetSystemEnvironmentVariable("PATHEXT");
            if( pathext.IndexOf(".lnk",StringComparison.CurrentCultureIgnoreCase) == -1 ) {
                EnvironmentUtility.SetSystemEnvironmentVariable("PATHEXT", pathext + ";.lnk");
            }

            foreach (var path in CanonicalFolders.Select(folder => Path.Combine(root, folder)).Where(path => !Directory.Exists(path))) {
                Directory.CreateDirectory(path);
            }

            // make sure system paths are updated.
            var binPath = Path.Combine(root, "bin");
            var psPath = Path.Combine(root, "powershell");

            if (!EnvironmentUtility.SystemPath.Contains(binPath)) {
                EnvironmentUtility.SystemPath = EnvironmentUtility.SystemPath.Prepend(binPath);
            }
            if (!EnvironmentUtility.PowershellModulePath.Contains(psPath)) {
                EnvironmentUtility.PowershellModulePath = EnvironmentUtility.PowershellModulePath.Prepend(psPath);
            }

            EnvironmentUtility.BroadcastChange();
        }

        /// <summary>
        ///   Starts the response pipe and process mesages.
        /// </summary>
        /// <param name="clientId"> The client id. </param>
        /// <param name="sessionId"> The session id. </param>
        /// <param name="serverPipe"> The server pipe. </param>
        /// <remarks>
        /// </remarks>
        private void StartResponsePipeAndProcessMessages(string clientId, string sessionId, NamedPipeServerStream serverPipe) {
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
            } catch (Exception e) {
                Logger.Error(e);
            }
        }
    }
}