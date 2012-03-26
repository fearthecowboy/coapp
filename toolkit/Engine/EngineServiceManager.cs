//-----------------------------------------------------------------------
// <copyright company="CoApp Project">
//     Copyright (c) 2011 Garrett Serack. All rights reserved.
// </copyright>
// <license>
//     The software is licensed under the Apache 2.0 License (the "License")
//     You may not use the software except in compliance with the License. 
// </license>
//-----------------------------------------------------------------------

using CoApp.Toolkit.Exceptions;
using CoApp.Toolkit.Win32;
using Microsoft.Win32.SafeHandles;

namespace CoApp.Toolkit.Engine {
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.IO.Pipes;
    using System.Linq;
    using System.Reflection;
    using System.Runtime.InteropServices;
    using System.Security.Principal;
    using System.ServiceProcess;
    using System.Threading;
    using Exceptions;
    using Extensions;
    using Logging;
    using TimeoutException = System.TimeoutException;

    public static class EngineServiceManager {
        public const string CoAppServiceName = "CoApp Package Installer Service";
        public const string CoAppDisplayName = "CoApp Package Installer Service";

        public static bool IsServiceInstalled {
            get { return ServiceController.GetServices().Any(service => service.ServiceName == CoAppServiceName); }
        }

        private static readonly Lazy<ServiceController> _controller = new Lazy<ServiceController>(() => new ServiceController(CoAppServiceName));

        // some dumbass thought that they should just return the last value, forcing developers to 'refresh' to get the current value. 
        // Not quite sure WHY THIS EVER SOUNDED LIKE A GOOD IDEA!? IF I WANTED THE LAST F$(*&%^*ING VALUE, I'D HAVE CACHED IT MYSELF DUMBASS!
        public static ServiceControllerStatus Status { get {
            _controller.Value.Refresh();
            return _controller.Value.Status; 
        }}

        public static bool CanStop { get {
            _controller.Value.Refresh();
            return _controller.Value.CanStop;
        }}

        public static bool IsServiceRunning {
            get {
                return IsServiceInstalled && Status == ServiceControllerStatus.Running;
            }
        }

        public static void TryToStopService() {
            if (IsServiceInstalled) {
                if (Status != ServiceControllerStatus.Stopped && CanStop) {
                    _controller.Value.Stop();
                }
                return;
            }

            // kill any service processes that are currently running.
            KillServiceProcesses();
        }

       
        public static void TryToStartService(bool secondAttempt = false) {
            if (!IsServiceInstalled) {
                InstallAndStartService();
                return;
            }

            Logger.Warning("==[Trying to start Win32 Service]==");

            switch (Status) {
                case ServiceControllerStatus.ContinuePending:
                    Logger.Warning("==[State:Continuing]==");
                    // wait for it to continue.
                    try {
                        _controller.Value.WaitForStatus(ServiceControllerStatus.Running, new TimeSpan(0, 0, 0, 10));
                    }
                    catch (TimeoutException) {
                        throw new UnableToStartServiceException(
                            "Service is in the '{0}' state, and didn't respond before timing out.".format(Status.ToString()));
                    }
                    if (Status != ServiceControllerStatus.Running) {
                        if (secondAttempt) {
                            throw new UnableToStartServiceException(
                                "Service is in the '{0}' state, and was expected to be in the 'Running' state.".format(Status.ToString()));
                        }
                        TryToStartService(true);
                    }
                    break;
                case ServiceControllerStatus.PausePending:
                    Logger.Warning("==[State:Pausing]==");
                    try {
                        _controller.Value.WaitForStatus(ServiceControllerStatus.Paused, new TimeSpan(0, 0, 0, 10));
                    }
                    catch (TimeoutException) {
                        throw new UnableToStartServiceException(
                            "Service is in the '{0}' state, and didn't respond before timing out.".format(Status.ToString()));
                    }
                    if (Status == ServiceControllerStatus.Paused) {
                        TryToStartService();
                    }
                    if (Status != ServiceControllerStatus.Running) {
                        if (secondAttempt) {
                            throw new UnableToStartServiceException(
                                "Service is in the '{0}' state, and was expected to be in the 'Running' state.".format(Status.ToString()));
                        }
                        TryToStartService(true);
                    }
                    break;
                case ServiceControllerStatus.Paused:
                    Logger.Warning("==[State:Paused]==");
                    _controller.Value.Continue();
                    try {
                        _controller.Value.WaitForStatus(ServiceControllerStatus.Running, new TimeSpan(0, 0, 0, 10));
                    }
                    catch (TimeoutException) {
                        if (secondAttempt) {
                            throw new UnableToStartServiceException(
                                "Service is in the '{0}' state, and didn't respond before timing out.".format(Status.ToString()));
                        }
                        TryToStartService(true);
                    }
                    break;

                case ServiceControllerStatus.Running:
                    Logger.Warning("==[State:Running]==");
                    // duh!
                    break;

                case ServiceControllerStatus.StartPending:
                    Logger.Warning("==[State:Starting]==");
                    try {
                        _controller.Value.WaitForStatus(ServiceControllerStatus.Running, new TimeSpan(0, 0, 0, 10));
                    }
                    catch (TimeoutException) {
                        if (secondAttempt) {
                            throw new UnableToStartServiceException(
                                "Service is in the '{0}' state, and didn't respond before timing out.".format(Status.ToString()));
                        }
                        TryToStartService(true);
                    }
                    break;

                case ServiceControllerStatus.StopPending:
                    Logger.Warning("==[State:Stopping]==");
                    try {
                        _controller.Value.WaitForStatus(ServiceControllerStatus.Stopped, new TimeSpan(0, 0, 0, 10));
                    }
                    catch (TimeoutException) {
                        throw new UnableToStartServiceException(
                            "Service is in the '{0}' state, and didn't respond before timing out.".format(Status.ToString()));
                    }
                    if (Status == ServiceControllerStatus.Stopped) {
                        TryToStartService();
                    }
                    if (Status != ServiceControllerStatus.Running) {
                        if (secondAttempt) {
                            throw new UnableToStartServiceException(
                                "Service is in the '{0}' state, and was expected to be in the 'Running' state.".format(Status.ToString()));
                        }
                        TryToStartService(true);
                    }
                    break;

                case ServiceControllerStatus.Stopped:
                    Logger.Warning("==[State:Stopped]==");
                    _controller.Value.Start();
                    try {
                        _controller.Value.WaitForStatus(ServiceControllerStatus.Running, new TimeSpan(0, 0, 0, 10));
                    }   
                    catch (TimeoutException) {
                        if (secondAttempt) {
                            throw new UnableToStartServiceException(
                                "Service is in the '{0}' state, and didn't respond before timing out.".format(Status.ToString()));
                        }
                        TryToStartService(true);
                    }
                    break;
            }
        }

        public static bool Available { get {
            lock (typeof(EngineServiceManager)) {
                using (var kernelEvent = Kernel32.OpenEvent(0x00100000, false, "Global\\CoAppAvailable")) {
                    if (!kernelEvent.IsInvalid && Kernel32.WaitForSingleObject(kernelEvent, 0) == 0) {
                        return true;
                    }
                }
                return false;
            }
        }}

        public static bool StartingUp {
            get {
                using (var kernelEvent = Kernel32.OpenEvent(0x00100000, false, "Global\\CoAppStartingUp")) {
                    if (!kernelEvent.IsInvalid && Kernel32.WaitForSingleObject(kernelEvent, 0) == 0) {
                        return true;
                    }
                }
                return false;
            }
        }

        public static bool ShuttingDown {
            get {
                using (var kernelEvent = Kernel32.OpenEvent(0x00100000, false, "Global\\CoAppShuttingDown")) {
                    if (!kernelEvent.IsInvalid && Kernel32.WaitForSingleObject(kernelEvent, 0) == 0) {
                        return true;
                    }
                }
                return false;
            }
        }

        public static bool ShutdownRequested {
            get {
                using (var kernelEvent = Kernel32.OpenEvent(0x00100000, false, "Global\\CoAppShutdownRequested")) {
                    if (!kernelEvent.IsInvalid && Kernel32.WaitForSingleObject(kernelEvent, 0) == 0) {
                        return true;
                    }
                }
                return false;
            }
        }

        public static int EngineStartupStatus { 
            get {
                return PackageManagerSettings.CoAppInformation["StartupPercentComplete"].IntValue;
            } 
        }

        public static bool IsEngineResponding {
            get {
                return Available;
            }
        }

        public static void EnsureServiceIsResponding() {
            if( Available ) {
                // looks good to me!
                return;
            }

            if (!StartingUp && !ShuttingDown && !Available && AreAnyServiceExesRunning) {
                // Just...wait a moment to see if anything good is going to happen.
                Thread.Sleep(800);
            }

            var count = 1200; // 10 minutes.
            if (StartingUp) {
                while (StartingUp && 0 < count--) {
                    // it's just getting started. It should be fine in a few moments
                    Thread.Sleep(500);
                }

                if (!StartingUp) {
                    // try again now that it's out of this state.
                    EnsureServiceIsResponding();
                    return;
                }

                // um. it's still starting up? What's gone wrong?
                throw new CoAppException("CoApp Engine appears stuck in starting up state.");
            }


            count = 10;
            while(ShuttingDown && count > 0 ) {
                if (IsServiceRunning) {
                    // looks like we caught the win32 service shutting down.
                    // let's try to start it back up (it'll safely stop first, so no worries)
                    TryToStartService();
                    EnsureServiceIsResponding();
                    return;
                }
                // looks like an interactive version is shutting down. Let's wait a few seconds and check again.
                Thread.Sleep(2000);
                count--;
            }

            if( ShuttingDown ) {
                // hmm. looks like it's stuck shutting down an interactive version of the serivce
                // he's had long enough, let's kill him.
                KillServiceProcesses();
                TryToStartService();
                EnsureServiceIsResponding();
                return;
            }

            if( IsServiceRunning ) {
                // hmm, we're not available, startingup or shutting down. 
                // try to stop it and restart it then.
                TryToStopService();
                TryToStartService();
                EnsureServiceIsResponding();
                return;
            }

            // we're not running at all!
            if( !IsServiceInstalled) {
                InstallAndStartService();
                EnsureServiceIsResponding();
                return;
            }
           
            // hmm. just try to start it I guess.
            TryToStartService();
            EnsureServiceIsResponding();
        }

        private static void KillServiceProcesses() {
            foreach (var proc in Process.GetProcessesByName("coapp.service").Where(each => each.Id != Process.GetCurrentProcess().Id).ToArray()) {
                try {
                    proc.Kill();
                } catch {

                }
            }
        }

        private static bool AreAnyServiceExesRunning{get {
            return Process.GetProcessesByName("coapp.service").Any(each => each != Process.GetCurrentProcess());
        }}

        public static string CoAppServiceExecutablePath {
            get {
                string result = null;

                var root = PackageManagerSettings.CoAppRootDirectory;
                var binDirectory = Path.Combine(root, "bin");

                // look for $COAPP\bin\coapp.service.exe 
                // this will happen when the service has been installed and configureed at least once.
                if (Directory.Exists(binDirectory)) {
                    var serviceExes = binDirectory.FindFilesSmarter(@"**\coapp.service.exe").OrderByDescending(Version);
                    result = serviceExes.FirstOrDefault( each => !Symlink.IsSymlink(each) || File.Exists(Symlink.GetActualPath(each)));
                    if( result != null ) {
                        return result;
                    }
                }

                // Look in %program files%/outercurve...
                // this will happen when the service is installed via msi, but not initialized.
                var searchDirectory = Path.Combine(Environment.GetEnvironmentVariable("ProgramFiles"), "outercurve foundation");

                if (Directory.Exists(searchDirectory)) {
                    // get all of the coapp.service.exes and pick the one with the highest version
                    var serviceExes = searchDirectory.FindFilesSmarter(@"**\coapp.service.exe").OrderByDescending(Version);
                    
                    // ah, so we found some did we? Should never be a symlink in this case.
                    result = serviceExes.FirstOrDefault(each => !Symlink.IsSymlink(each));
                }
                return result;
            }
        }

        private static ulong Version(string path) {
            try {
                var info = FileVersionInfo.GetVersionInfo(path);
                var fv = info.FileVersion;
                if (!String.IsNullOrEmpty(fv)) {
                    fv = fv.Substring(0, fv.PositionOfFirstCharacterNotIn("0123456789.".ToCharArray()));
                }

                if (String.IsNullOrEmpty(fv)) {
                    return 0;
                }

                var vers = fv.Split('.');
                var major = vers.Length > 0 ? vers[0].ToInt32() : 0;
                var minor = vers.Length > 1 ? vers[1].ToInt32() : 0;
                var build = vers.Length > 2 ? vers[2].ToInt32() : 0;
                var revision = vers.Length > 3 ? vers[3].ToInt32() : 0;

                return (((UInt64)major) << 48) + (((UInt64)minor) << 32) + (((UInt64)build) << 16) + (UInt64)revision;
            } catch {
                return 0;
            }
        }

        private static void InstallAndStartService() {
            // we're going to try and install the service
            // make sure no other service processes are trying to run right now.
            KillServiceProcesses();

            // basically, we just need to find *any* coapp.service.exe and tell it to --auto-install
            // and it will do a better search than we could do anyway.
            var exe = CoAppServiceExecutablePath;
            if (exe == null) {
                throw new CoAppException("Unable to locate Installed CoApp Service.");
            }

            var processStartInfo = new ProcessStartInfo(exe) {
                Arguments = "--auto-install",
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                UseShellExecute = false,
            };

            Process.Start(processStartInfo).WaitForExit();

            if (IsServiceInstalled) {
                TryToStartService(); // make sure it is started too.
                return;
            }
            
            // uh, if we got here, we're not going to be able start Coapp...
            throw new CoAppException("Unable to start CoApp Service; the service executable is: '{0}'".format(exe));
        }
    }
}