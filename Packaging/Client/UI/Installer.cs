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

namespace CoApp.Packaging.Client.UI {
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Reflection;
    using System.Security.AccessControl;
    using System.Security.Principal;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Windows;
    using System.Windows.Forms;
    using System.Windows.Media.Imaging;
    using Common;
    using Toolkit.Extensions;
    using Toolkit.Logging;
    using Toolkit.Tasks;
    using Application = System.Windows.Application;
    using MessageBox = System.Windows.Forms.MessageBox;

    public class Installer : MarshalByRefObject, INotifyPropertyChanged {
        internal string MsiFilename;
        internal Task InstallTask;
        public event PropertyChangedEventHandler PropertyChanged;

        public event PropertyChangedEventHandler Finished;

        private InstallChoice _choice = InstallChoice.InstallSpecificVersion;

        public InstallChoice Choice {
            get {
                return _choice;
            }
            set {
                _choice = value;
                // PackageIcon = GetPackageBitmap(SelectedPackage.Icon);
                OnPropertyChanged();
                OnPropertyChanged("Choice");
            }
        }
        public Installer(string filename) {
            try {
                MsiFilename = filename;
                // was coapp just installed by the bootstrapper? 
                var tsk = Task.Factory.StartNew(() => {
                    if (((AppDomain.CurrentDomain.GetData("COAPP_INSTALLED") as string) ?? "false").IsTrue()) {
                        // we'd better make sure that the most recent version of the service is running.
                        EngineServiceManager.InstallAndStartService();
                    }
                    bool wasCreated;
                    var ewhSec = new EventWaitHandleSecurity();
                    ewhSec.AddAccessRule(new EventWaitHandleAccessRule(new SecurityIdentifier(WellKnownSidType.WorldSid, null), EventWaitHandleRights.FullControl, AccessControlType.Allow));
                    _ping = new EventWaitHandle(false, EventResetMode.ManualReset, "BootstrapperPing", out wasCreated, ewhSec);
                });

                var ts2= tsk.Continue(() => {
                    _packageManager.GetPackageFromFile(Path.GetFullPath(MsiFilename)).Continue(pkg => {
                        _packageManager.GetPackageDetails(pkg).Continue(() => {
                            PackageSet = pkg;
                        });
                    });
                });

                tsk.ContinueOnFail(error => {
                    DoError(InstallerFailureState.FailedToGetPackageFromFile, error);
                    ExitQuick();
                });

                try {
                    Application.ResourceAssembly = Assembly.GetExecutingAssembly();
                }
                catch {
                }

                ts2.Wait();

                _window = new InstallerMainWindow(this);
                _window.ShowDialog();

                if (Application.Current != null) {
                    Application.Current.Shutdown(0);
                }
                ExitQuick();
            }
            catch (Exception e) {
                DoError(InstallerFailureState.FailedToGetPackageDetails, e);
            }
        }

        public IEnumerable<RemoveCommand> RemoveChoices {
            get {
                if( !PackageInformationRetrieved) {
                    yield break;
                }

                var ct = PackageSet.InstalledPackages == null ? 0 : PackageSet.InstalledPackages.Count();
                if (ct > 0) {
                    if (ct == 1) {
                        yield return new RemoveCommand {
                            Text = "Remove current version ({0}) [Default]".format(PackageSet.InstalledNewest.Version),
                            CommandParam = () => RemovePackage(PackageSet.InstalledNewest.CanonicalName)
                        };
                    } else {
                        if (!PackageSet.Trimable.IsNullOrEmpty()) {
                            yield return new RemoveCommand {
                                Text = "Trim {0} unused versions".format(PackageSet.Trimable.Count()),
                                CommandParam = () => RemovePackages(PackageSet.Trimable.Select(each => each.CanonicalName).ToArray())
                            };
                        }

                        if (PackageSet.IsInstalled) {
                            yield return new RemoveCommand {
                                Text = "Remove this version ({0})".format(PackageSet.Version),
                                CommandParam = () => RemovePackage(PackageSet.CanonicalName)
                            };
                        }

                        if (PackageSet.InstalledNewest != null) {
                            yield return new RemoveCommand {
                                Text = "Remove current version ({0})".format(PackageSet.InstalledNewest.Version),
                                CommandParam = () => RemovePackage(PackageSet.InstalledNewest.CanonicalName)
                            };
                        }

                        yield return new RemoveCommand {
                            Text = "Remove all {0} versions [Default]".format(ct),
                            CommandParam = () => RemovePackages(PackageSet.InstalledPackages.Select(each => each.CanonicalName).ToArray())
                        };
                    }
                }
            }
        }

        public IEnumerable<InstSelection> InstallChoices {
            get {
                if (!PackageInformationRetrieved) {
                    _choice = InstallChoice._Unknown;
                    OnPropertyChanged("Choice");
                    yield break;
                }
                
                // When there isn't anything at all installed already
                if (PackageSet.InstalledNewest == null) {
                    if (PackageSet.AvailableNewest != null) {
                        yield return new InstSelection(InstallChoice.AutoInstallLatest, "Install the latest version of this package ({0})", PackageSet.AvailableNewest.Version);
                        _choice = InstallChoice.AutoInstallLatest;
                    }

                    if (PackageSet.AvailableNewestUpdate != null && PackageSet.AvailableNewestUpdate != PackageSet.AvailableNewest) {
                        yield return new InstSelection(InstallChoice.AutoInstallLatestCompatible, "Install the latest compatible version of this package ({0})", PackageSet.AvailableNewestUpdate.Version);
                        if (_choice == InstallChoice._Unknown) {
                            _choice = InstallChoice.AutoInstallLatest;
                        }
                    }

                    if (_choice == InstallChoice._Unknown) {
                        _choice = InstallChoice.InstallSpecificVersion;
                    }

                    yield return new InstSelection(InstallChoice.InstallSpecificVersion, "Install this version of package ({0})", PackageSet.Version);
                } else {
                    if (PackageSet == PackageSet.InstalledNewest) {
                        yield return new InstSelection(InstallChoice.ThisVersionAlreadyInstalled, "This version is currently installed ({0})", PackageSet.Version);
                        _choice = InstallChoice.ThisVersionAlreadyInstalled;

                        if (PackageSet.AvailableNewestUpdate != null && PackageSet.AvailableNewest == null) {
                            yield return new InstSelection(InstallChoice.UpdateToLatestVersion, "Update to the latest compatible version ({0})", PackageSet.AvailableNewestUpdate.Version);
                        }

                        if (PackageSet.AvailableNewestUpdate != null && PackageSet.AvailableNewest != null) {
                            yield return new InstSelection(InstallChoice.UpdateToLatestVersionNotUpgrade, "Update to the latest compatible version ({0})", PackageSet.AvailableNewestUpdate.Version);
                        }

                        if (PackageSet.AvailableNewest != null) {
                            yield return new InstSelection(InstallChoice.UpgradeToLatestVersion, "Upgrade to the latest version ({0})", PackageSet.AvailableNewest.Version);
                        }
                    } else if (PackageSet.InstalledNewest.Version > PackageSet.Version) {
                        // a newer version is already installed    
                        yield return new InstSelection(InstallChoice.NewerVersionAlreadyInstalled, "A newer version is currently installed ({0})", PackageSet.InstalledNewest.Version);
                        _choice = InstallChoice.NewerVersionAlreadyInstalled;

                        if (PackageSet.AvailableNewest != null) {
                            yield return new InstSelection(InstallChoice.UpgradeToLatestVersion2, "Upgrade to the latest version ({0})", PackageSet.AvailableNewest.Version);
                        }

                        if (PackageSet.AvailableNewestUpdate != null) {
                            yield return new InstSelection(InstallChoice.UpdateToLatestVersionNotUpgrade2, "Install the latest compatible version ({0})", PackageSet.AvailableNewestUpdate.Version);
                        }

                        if (PackageSet.IsInstalled) {
                            yield return new InstSelection(InstallChoice.ThisVersionAlreadyInstalled, "This version is currently installed ({0})", PackageSet.Version);
                        } else {
                            yield return new InstSelection(InstallChoice.InstallSpecificVersion2, "Install this version of package ({0})", PackageSet.Version);
                        }
                    } else {
                        // an older version is installed
                        yield return new InstSelection(InstallChoice.OlderVersionAlreadyInstalled, "A older version is currently installed ({0})", PackageSet.InstalledNewest.Version);
                        _choice = InstallChoice.OlderVersionAlreadyInstalled;

                        if (PackageSet.AvailableNewest != null) {
                            yield return new InstSelection(InstallChoice.UpgradeToLatestVersion2, "Upgrade to the latest version of this package ({0})", PackageSet.AvailableNewest.Version);
                        }

                        if (PackageSet.AvailableNewestUpdate != null) {
                            yield return new InstSelection(InstallChoice.AutoInstallLatestCompatible3, "Install the latest version of this package ({0})", PackageSet.AvailableNewest.Version);
                        }

                        yield return new InstSelection(InstallChoice.InstallSpecificVersion2, "Install this version of package ({0})", PackageSet.Version);
                    }
                }

                OnPropertyChanged("Choice");
            }
        }

        public bool PackageInformationRetrieved { get; set; }

        private Package _packageSet;

        private Package PackageSet {
            get {
                return _packageSet;
            }

            set {
                PackageInformationRetrieved = true;
                _packageSet = value;
                OnPropertyChanged();
                OnPropertyChanged("InstallChoices");
                OnPropertyChanged("RemoveChoices");
                OnPropertyChanged("Choice");
            }
        }

        public IPackage SelectedPackage {
            get {
                if (!PackageInformationRetrieved) {
                    return null;
                }

                if (Choice == InstallChoice.NewerVersionAlreadyInstalled) {
                    return PackageSet.InstalledNewest ?? PackageSet.InstalledNewestUpdate;
                }

                if (Choice == InstallChoice.OlderVersionAlreadyInstalled) {
                    return PackageSet.LatestInstalledThatUpgradesToThis ?? PackageSet.LatestInstalledThatUpdatesToThis;
                }

                if (Choice == InstallChoice.ThisVersionAlreadyInstalled) {
                    return PackageSet;
                }

                if (Choice.HasFlag(InstallChoice._InstallSpecificVersion)) {
                    return PackageSet;
                }

                if (Choice.HasFlag(InstallChoice._InstallLatestUpgrade)) {
                    return PackageSet.AvailableNewest;
                }

                if (Choice.HasFlag(InstallChoice._InstallLatestUpdate)) {
                    return PackageSet.AvailableNewestUpdate;
                }

                return PackageSet.InstalledNewest ?? PackageSet;
            }
        }

        public bool CanRemove {
            get {
                return RemoveChoices.Any();
            }
        }

        public bool CanInstall {
            get {
                return !SelectedPackage.IsInstalled;
            }
        }

        public bool ReadyToInstall {
            get {
                return SelectedPackage.IsInstalled == false;
            }
        }

        private int _progress;

        public int Progress {
            get {
                return _progress;
            }
            set {
                _progress = value;
                OnPropertyChanged("Progress");
            }
        }

        public Visibility RemoveButtonVisibility {
            get {
                return CanRemove ? Visibility.Visible : Visibility.Hidden;
            }
        }

        public Visibility CancelButtonVisibility {
            get {
                return CancelRequested ? Visibility.Hidden : Visibility.Visible;
            }
        }

        public bool IsInstalled {
            get {
                return SelectedPackage.IsInstalled;
            }
        }

        public string Organization {
            get {
                return  SelectedPackage.PackageDetails.Publisher.Name;
            }
        }

        public string Description {
            get {
                return SelectedPackage.PackageDetails.Description;
            }
        }

        public string Product {
            get {
                return "{0} - {1}".format(SelectedPackage.DisplayName, string.IsNullOrEmpty(SelectedPackage.PackageDetails.AuthorVersion) ? (string)SelectedPackage.Version : SelectedPackage.PackageDetails.AuthorVersion);
            }
        }

        public string ProductVersion {
            get {
                return SelectedPackage.Version;
            }
        }

        private bool _working;

        public bool IsWorking {
            get {
                return _working;
            }
            set {
                _working = value;
                OnPropertyChanged("Working");
            }
        }

        private bool _cancel;

        public bool CancelRequested {
            get {
                return _cancel;
            }
            set {
                if (IsWorking) {
                    // packageManager.StopInstall? 
                }
                _cancel = value;
                OnPropertyChanged("CancelRequested");
                OnPropertyChanged("CancelButtonVisibility");

                if (!IsWorking) {
                    OnFinished();
                }
                Task.Factory.StartNew(() => {
                    // worst case scenario, die quickly.
                    Thread.Sleep(1500);
                    ExitQuick();
                });
            }
        }

        private BitmapImage _packageIcon;

        public BitmapImage PackageIcon {
            get {
                return _packageIcon;
            }
            set {
                _packageIcon = value;
                OnPropertyChanged("PackageIcon");
            }
        }

        public string InstallButtonText {
            get {
                switch (Choice) {
                    case InstallChoice.UpgradeToLatestVersion:
                    case InstallChoice.UpgradeToLatestVersion2:
                    case InstallChoice.UpgradeToLatestVersion3:
                        return "Upgrade";

                    case InstallChoice.UpdateToLatestVersion:
                    case InstallChoice.UpdateToLatestVersionNotUpgrade:
                    case InstallChoice.UpdateToLatestVersionNotUpgrade2:
                        return "Update";
                }
                return "Install";
            }
        }

        private readonly PackageManager _packageManager = new PackageManager();
        private EventWaitHandle _ping;

        internal bool Ping {
            get {
                return _ping.WaitOne(0);
            }
            set {
                if (value) {
                    _ping.Set();
                } else {
                    _ping.Reset();
                }
            }
        }

        private readonly InstallerMainWindow _window;

       

        private void DoError(InstallerFailureState state, Exception error) {
            MessageBox.Show(error.StackTrace, error.Message, MessageBoxButtons.OK, MessageBoxIcon.Exclamation, MessageBoxDefaultButton.Button1);
            switch (state) {
                case InstallerFailureState.FailedToGetPackageFromFile:
                    break;

                default:
                    break;
            }
            ExitQuick();
        }

        internal void ExitQuick() {
            try {
                if (Application.Current != null) {
                    Application.Current.Shutdown(0);
                }
            } catch {
            }
            Environment.Exit(0);
        }

        private void OnFinished() {
            IsWorking = false;
            if (Finished != null) {
                Finished(this, new PropertyChangedEventArgs("Finished"));
            }
        }

        private static BitmapImage GetPackageBitmap(string iconData) {
            // if it's empty, return an empty image.
            if (!string.IsNullOrEmpty(iconData)) {
                // is it a reference to a file somewhere?
                try {
                    var uri = new Uri(iconData);
                    switch (uri.Scheme.ToLower()) {
                        case "http":
                        case "https":
                        case "ftp":
                            // download the file
                            var filename = "packageIcon".GenerateTemporaryFilename();
                            try {
                                new WebClient().DownloadFile(uri, filename);
                                if (File.Exists(filename)) {
                                    return GetPackageBitmap(filename);
                                }
                            } catch {
                            }
                            break;

                        case "file":
                            var image = new BitmapImage();
                            image.BeginInit();
                            using (var srcStream = File.OpenRead(uri.LocalPath)) {
                                image.StreamSource = srcStream;
                                image.EndInit();
                                return image;
                            }
                    }
                } catch {
                }

                // is it a base64 encoded image?
                try {
                    var image = new BitmapImage();
                    image.BeginInit();
                    using (var srcStream = new MemoryStream(Convert.FromBase64String(iconData))) {
                        image.StreamSource = srcStream;
                        image.EndInit();
                        return image;
                    }
                } catch {
                }
            }

            var img = new BitmapImage();
            img.Freeze();
            return img;
        }

        private void CancellationRequestedDuringInstall(string obj) {
            Logger.Message("Cancellation Requested during install (engine is restarting.)");
            // we *could* try to see if the service comes back here; or we could just kill this window.
            // if we kill the window, we *could* do a restart of the msi just to make sure that it gets installed 
            // (since, if the toolkit got updated as part of the install, the engine will restart)

            if (SelectedPackage.IsInstalled) {
                // it was done, lets just quit nicely
                OnFinished();
                return;
            }
            // otherwise, try again?
            Install();
        }

        private void CancellationRequestedDuringRemove(string obj) {
            Logger.Message("Cancellation Requested during remove (engine is restarting.)");

            if (!SelectedPackage.IsInstalled) {
                OnFinished();
                return;
            }
            // otherwise, try again?
            RemoveAll();
        }

        public void Install() {
            if (!IsWorking) {
                IsWorking = true;
                CurrentTask.Events += new PackageInstallProgress((name, progress, overallProgress) => {
                    Progress = overallProgress;
                });

                var instTask = _packageManager.InstallPackage(SelectedPackage.CanonicalName, autoUpgrade: false);

                instTask.Continue(() => OnFinished());

                instTask.ContinueOnFail(exception => DoError(InstallerFailureState.FailedToGetPackageFromFile, exception));
            }
        }

        public void RemoveAll() {
            RemovePackages(PackageSet.InstalledPackages.Select(each => each.CanonicalName).ToArray());
        }

        private void RemovePackage(CanonicalName canonicalVersion) {
            RemovePackages(new[] {canonicalVersion});
        }

        private void RemovePackages(CanonicalName[] canonicalVersions) {
            if (!IsWorking) {
                IsWorking = true;
                Task.Factory.StartNew(() => {
                    var taskCount = canonicalVersions.Length;
                    if (canonicalVersions.Length > 0) {
                        for (var index = 0; index < taskCount; index++) {
                            var taskNumber = index;
                            var v = canonicalVersions[index];
                            /*
                            Session.RemoteService.RemovePackage(
                                v, messages: new PackageManagerMessages {
                                    RemovingPackageProgress = (canonicalName, progress) => {
                                        Progress = (progress / taskCount) + taskNumber * 100 / taskCount;
                                    },
                                    RemovedPackage = (canonicalName) => {
                                        Package.GetPackage(canonicalName).Installed = false;
                                    },
                                    OperationCanceled = CancellationRequestedDuringRemove,
                                }).Wait();
                             * */
                        }
                    }
                }).ContinueWith(antecedent => OnFinished(), TaskContinuationOptions.AttachedToParent);
            }
        }

        protected void OnPropertyChanged(string name = null) {
            if (PropertyChanged != null) {
                if (name == null) {
                    foreach (var propertyName in GetType().GetProperties().Where(each => each.Name != "InstallChoices").Select(each => each.Name)) {
                        PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
                    }
                } else {
                    PropertyChanged(this, new PropertyChangedEventArgs(name));
                }
            }
        }
    }
}