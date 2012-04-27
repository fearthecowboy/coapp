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
    using System.Security.AccessControl;
    using System.Security.Principal;
    using System.Threading;
    using System.Threading.Tasks;
    using Common;
    using Toolkit.Extensions;

    internal static class Signals {
        static Signals() {
            bool wasCreated;
            var ewhSec = new EventWaitHandleSecurity();
            ewhSec.AddAccessRule(new EventWaitHandleAccessRule(new SecurityIdentifier(WellKnownSidType.WorldSid, null), EventWaitHandleRights.Synchronize | EventWaitHandleRights.Modify, AccessControlType.Allow));

            AvailableEvent = new EventWaitHandle(false, EventResetMode.ManualReset, "Global\\CoAppAvailable", out wasCreated, ewhSec);
            StartingupEvent = new EventWaitHandle(false, EventResetMode.ManualReset, "Global\\CoAppStartingUp", out wasCreated, ewhSec);
            ShuttingdownEvent = new EventWaitHandle(false, EventResetMode.ManualReset, "Global\\CoAppShuttingDown", out wasCreated, ewhSec);
            ShuttingdownRequestedEvent = new EventWaitHandle(false, EventResetMode.ManualReset, "Global\\CoAppShutdownRequested", out wasCreated, ewhSec);
            InstalledEvent = new EventWaitHandle(false, EventResetMode.ManualReset, "Global\\CoAppInstalledPackage", out wasCreated, ewhSec);
            RemovedEvent = new EventWaitHandle(false, EventResetMode.ManualReset, "Global\\CoAppRemovedPackage", out wasCreated, ewhSec);
        }

        private static readonly EventWaitHandle AvailableEvent;
        private static readonly EventWaitHandle StartingupEvent;
        private static readonly EventWaitHandle ShuttingdownEvent;
        private static readonly EventWaitHandle ShuttingdownRequestedEvent;
        private static readonly EventWaitHandle InstalledEvent;
        private static readonly EventWaitHandle RemovedEvent;

        private static bool _available;

        public static bool Available {
            get {
                return _available;
            }
            set {
                _available = value;
                AvailableEvent.Reset();

                if (value) {
                    StartingUp = false;
                    ShuttingDown = false;
                    AvailableEvent.Set();
                }
            }
        }

        private static bool _startingUp;

        public static bool StartingUp {
            get {
                return _startingUp;
            }
            set {
                _startingUp = value;
                StartingupEvent.Reset();

                if (value) {
                    Available = false;
                    ShuttingDown = false;
                    StartingupEvent.Set();
                }
            }
        }

        private static bool _shuttingDown;

        public static bool ShuttingDown {
            get {
                return _shuttingDown;
            }
            set {
                _shuttingDown = value;
                ShuttingdownEvent.Reset();
                if (value) {
                    StartingUp = false;
                    Available = false;
                    ShuttingdownEvent.Set();
                }
            }
        }

        private static bool _shutdownRequested;

        public static bool ShutdownRequested {
            get {
                return _shutdownRequested;
            }
            set {
                _shutdownRequested = value;
                ShuttingdownRequestedEvent.Reset();
                if (value) {
                    ShuttingdownRequestedEvent.Set();
                }
            }
        }

        public static void InstalledPackage(string canonicalPackageName) {
            Task.Factory.StartNew(() => {
                PackageManagerSettings.CoAppInformation["InstalledPackages"].StringsValue =
                    PackageManagerSettings.CoAppInformation["InstalledPackages"].StringsValue.UnionSingleItem(canonicalPackageName);
                InstalledEvent.Reset();
                InstalledEvent.Set();
                Thread.Sleep(100); // give everyone a chance to wake up and do their job
                InstalledEvent.Reset();
            });
        }

        public static void RemovedPackage(string canonicalPackageName) {
            Task.Factory.StartNew(() => {
                PackageManagerSettings.CoAppInformation["RemovedPackages"].StringsValue =
                    PackageManagerSettings.CoAppInformation["RemovedPackages"].StringsValue.UnionSingleItem(canonicalPackageName);
                RemovedEvent.Reset();
                RemovedEvent.Set();
                Thread.Sleep(100); // give everyone a chance to wake up and do their job
                RemovedEvent.Reset();
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
}