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
    using System.Diagnostics;
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
    using Toolkit.Win32;
    using Application = System.Windows.Application;
    using MessageBox = System.Windows.Forms.MessageBox;

    public class Installer : MarshalByRefObject, INotifyPropertyChanged {
        internal string MsiFilename;
        //internal Task InstallTask;

        private string _finalText;
        private Package _primaryPackage;
        private Package _package;
        private bool _working;
        private bool _cancel;
        private int _progress;
        private BitmapImage _packageIcon;
        private readonly PackageManager _packageManager = new PackageManager();
        private EventWaitHandle _ping;
        private readonly InstallerMainWindow _window;

        public event PropertyChangedEventHandler PropertyChanged;
        public event PropertyChangedEventHandler Finished;

        private bool Quiet;
        internal bool Passive;
        private bool Remove;


        public Installer(string filename) {
            try {
                MsiFilename = filename;
                Quiet = ((AppDomain.CurrentDomain.GetData("QUIET") as string) ?? "false").IsTrue();
                Passive = ((AppDomain.CurrentDomain.GetData("PASSIVE") as string) ?? "false").IsTrue();
                Remove = ((AppDomain.CurrentDomain.GetData("REMOVE") as string) ?? "false").IsTrue();

                Logger.Message("Quiet {0}/Passive {1}/Remove {2}", Quiet, Passive, Remove);

                // was coapp just installed by the bootstrapper? 
                var tsk = Task.Factory.StartNew(() => {
                    if (((AppDomain.CurrentDomain.GetData("COAPP_INSTALLED") as string) ?? "false").IsTrue()) {
                        // we'd better make sure that the most recent version of the service is running.
                        EngineServiceManager.InstallAndStartService();
                        EnvironmentUtility.BroadcastChange();
                    }
                    bool wasCreated;
                    var ewhSec = new EventWaitHandleSecurity();
                    ewhSec.AddAccessRule(new EventWaitHandleAccessRule(new SecurityIdentifier(WellKnownSidType.WorldSid, null), EventWaitHandleRights.FullControl, AccessControlType.Allow));
                    _ping = new EventWaitHandle(false, EventResetMode.ManualReset, "BootstrapperPing", out wasCreated, ewhSec);
                });

                // force the explorer process to pick up changes to the environment.
                EnvironmentUtility.BroadcastChange();

                var ts2= tsk.Continue(() => {
                    // gets the package data for the likely suspects.
                    _packageManager.AddSessionFeed(Path.GetDirectoryName(Path.GetFullPath(MsiFilename))).Continue(() => {
                        _packageManager.GetPackageFromFile(Path.GetFullPath(MsiFilename)).Continue(pkg =>
                            Task.WaitAll(new[] { pkg.InstalledNewest, pkg.AvailableNewestUpdate, pkg.AvailableNewestUpgrade }
                                .Select(each => each != null ? _packageManager.GetPackage(each.CanonicalName, true)
                                .Continue(() => { _packageManager.GetPackageDetails(each.CanonicalName); })
                                : "".AsResultTask())
                            .UnionSingleItem(_packageManager.GetPackageDetails(pkg).Continue(() => { PrimaryPackage = pkg; }))
                            .ToArray()));
                    });
                });

                tsk.ContinueOnFail(error => {
                    DoError(InstallerFailureState.FailedToGetPackageFromFile, error);
                    ExitQuick();
                });
              
                try {
                    Application.ResourceAssembly = Assembly.GetExecutingAssembly();
                } catch {
                }

                ts2.Wait();
                
                if (!Quiet) {
                    _window = new InstallerMainWindow(this);
                    if( Remove ) {
                        _window.Loaded += (sender, args) => _window.RemoveButtonClick(sender, args);
                    } else if( Passive ) {
                        _window.Loaded += (sender, args) => _window.InstallButtonClick(sender, args);
                    }

                    _window.ShowDialog();

                    if (Application.Current != null) {
                        Application.Current.Shutdown(0);
                    }
                } else {
                    // when quiet
                    var discard = RemoveChoices.ToArray();
                    var discard2 = InstallChoices.ToArray();

                    if( Remove ) {
                        if (CanRemove) {
                            RemoveAll().Wait();
                        }
                    } else {
                        Logger.Message("Thinkin' about installin' {0}", CanInstall);
                        Debugger.Break();
                        if( CanInstall) {
                            Install().Wait();
                        }
                    }
                }
                ExitQuick();
            }
            catch (Exception e) {
                DoError(InstallerFailureState.FailedToGetPackageDetails, e);
            }
        }

        public string CancelButtonText {
            get {
                return _finalText != null ? "Close" : "Cancel";
            }
        }

        public string StatusText {
            get {
                if (_finalText != null) {
                    return _finalText;
                }

                if (!PackageInformationRetrieved) {
                    return "";
                }
                
                if( PrimaryPackage.IsInstalled) {
                    if(PrimaryPackage.AvailableNewestUpdate == null && PrimaryPackage.AvailableNewestUpgrade == null) {
                        if (PrimaryPackage == PrimaryPackage.InstalledNewest) {
                            return "This package ({0}) is currently installed and up-to-date.".format(PrimaryPackage.Version);
                        }
                        return "This package ({0}) is currently installed and up-to-date ({1}).".format(PrimaryPackage.Version, PrimaryPackage.AvailableNewest.Version);
                    }

                    if (PrimaryPackage.AvailableNewestUpdate == null) {
                        return "This package ({0}) is currently installed; an upgrade is available ({1})".format(PrimaryPackage.Version, PrimaryPackage.AvailableNewestUpgrade.Version);
                    }

                    return "This package ({0}) is currently installed; an update is available ({1})".format(PrimaryPackage.Version, PrimaryPackage.AvailableNewestUpdate.Version);
                }

                if( PrimaryPackage.InstalledNewest != null) {
                    if (PrimaryPackage.InstalledNewest.IsNewerThan(PrimaryPackage)) {
                        return "A newer version of this package ({0}) is currently installed.".format(PrimaryPackage.InstalledNewest.Version);
                    }
                    return "An older version of this package ({0}) is currently installed.".format(PrimaryPackage.InstalledNewest.Version);
                }
                return ""; // nothing currently installed.
            }
        }

        public IEnumerable<RemoveCommand> RemoveChoices {
            get {
                if( !PackageInformationRetrieved) {
                    yield break;
                }

                var ct = PrimaryPackage.InstalledPackages == null ? 0 : PrimaryPackage.InstalledPackages.Count();
                if (ct > 0) {
                    if (ct == 1) {
                        yield return new RemoveCommand {
                            Text = "Remove current version ({0}) [Default]".format(PrimaryPackage.InstalledNewest.Version),
                            CommandParam = () => RemovePackage(PrimaryPackage.InstalledNewest.CanonicalName)
                        };
                    } else {
                        if (!PrimaryPackage.TrimablePackages.IsNullOrEmpty()) {
                            yield return new RemoveCommand {
                                Text = "Trim {0} unused versions".format(PrimaryPackage.TrimablePackages.Count()),
                                CommandParam = () => RemovePackages(PrimaryPackage.TrimablePackages.Select(each => each.CanonicalName).ToArray())
                            };
                        }

                        if (PrimaryPackage.IsInstalled) {
                            yield return new RemoveCommand {
                                Text = "Remove this version ({0})".format(PrimaryPackage.Version),
                                CommandParam = () => RemovePackage(PrimaryPackage.CanonicalName)
                            };
                        }

                        if (PrimaryPackage.InstalledNewest != null) {
                            yield return new RemoveCommand {
                                Text = "Remove current version ({0})".format(PrimaryPackage.InstalledNewest.Version),
                                CommandParam = () => RemovePackage(PrimaryPackage.InstalledNewest.CanonicalName)
                            };
                        }

                        yield return new RemoveCommand {
                            Text = "Remove all {0} versions [Default]".format(ct),
                            CommandParam = () => RemovePackages(PrimaryPackage.InstalledPackages.Select(each => each.CanonicalName).ToArray())
                        };
                    }
                }
            }
        }


        public IEnumerable<InstSelection> InstallChoices {
            get {
                if (!PackageInformationRetrieved) {
                    yield break;
                }

                // When there isn't anything at all installed already
                if (PrimaryPackage.InstalledNewest == null) {
                    if (PrimaryPackage.AvailableNewest != null && PrimaryPackage.AvailableNewest != PrimaryPackage) {
                        yield return new InstSelection(PrimaryPackage.AvailableNewest, "Install the latest version of this package ({0})");
                    }

                    if (PrimaryPackage.AvailableNewestUpdate != null && PrimaryPackage.AvailableNewest != PrimaryPackage && PrimaryPackage.AvailableNewestUpdate != PrimaryPackage.AvailableNewest) {
                        yield return new InstSelection(PrimaryPackage.AvailableNewestUpdate, "Install the latest compatible version of this package ({0})");
                    }

                    yield return new InstSelection(PrimaryPackage, "Install this version of package ({0})");
                }
                else {
                    if (PrimaryPackage == PrimaryPackage.InstalledNewest) {
                        // this package is the newest one installed.
                        if (PrimaryPackage.AvailableNewestUpgrade != null) {
                            yield return new InstSelection(PrimaryPackage.AvailableNewestUpgrade, "Upgrade to the latest version ({0})");
                        }

                        if (PrimaryPackage.AvailableNewestUpdate != null ) {
                            yield return new InstSelection(PrimaryPackage.AvailableNewestUpdate, "Update to the latest compatible version ({0})");
                        }
                    }
                    else if (PrimaryPackage.InstalledNewest.Version > PrimaryPackage.Version) {
                        if (PrimaryPackage.AvailableNewestUpgrade != null && PrimaryPackage.AvailableNewestUpgrade.IsNewerThan(PrimaryPackage.InstalledNewest)) {
                            yield return new InstSelection( PrimaryPackage.AvailableNewestUpgrade, "Upgrade to the latest version ({0})");
                        }

                        if (PrimaryPackage.AvailableNewestUpdate != null && !PrimaryPackage.InstalledNewest.IsAnUpdateFor(PrimaryPackage.AvailableNewestUpdate)) {
                            yield return new InstSelection(PrimaryPackage.AvailableNewestUpdate, "Update to the latest compatible version ({0})");
                        }

                        if (!PrimaryPackage.IsInstalled) {
                            yield return new InstSelection(PrimaryPackage, "Install this version of package ({0})");
                        }
                    }
                    else {
                        if (PrimaryPackage.AvailableNewestUpgrade != null) {
                            yield return new InstSelection(PrimaryPackage.AvailableNewestUpgrade, "Upgrade to the latest version of this package ({0})");
                        }

                        if (PrimaryPackage.AvailableNewestUpdate != null) {
                            yield return new InstSelection(PrimaryPackage.AvailableNewestUpdate, "Update to the latest version of this package ({0})");
                        }

                        if (!PrimaryPackage.IsInstalled ) {
                            yield return new InstSelection(PrimaryPackage, "Install this version of package ({0})");
                        }
                    }
                }
            }
        }

        public bool PackageInformationRetrieved { get; set; }

        private Package PrimaryPackage {
            get {
                return _primaryPackage;
            }

            set {
                PackageInformationRetrieved = true;
                _primaryPackage = value;
                OnPropertyChanged("InstallChoices");
                OnPropertyChanged("RemoveChoices");
                SelectedPackage = _primaryPackage;
            }
        }


        public IPackage SelectedPackage {
            get {
                return _package ?? PrimaryPackage;
            }
            set {
                if (_package != value) {
                    _package = (Package)value;
                    OnPropertyChanged("SelectedPackage");
                    OnPropertyChanged();
                }
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

        public bool IsWorking {
            get {
                return _working;
            }
            set {
                _working = value;
                OnPropertyChanged("Working");
            }
        }

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
                if (SelectedPackage == PrimaryPackage || PrimaryPackage.InstalledNewest == null) {
                    return "Install";
                }
                if( SelectedPackage == PrimaryPackage.AvailableNewestUpdate) {
                    return "Update";
                }
                return "Upgrade";
            }
        }

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
            OnPropertyChanged("StatusText");
            OnPropertyChanged("CancelButtonText");
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

        public Task Install() {
            if (!IsWorking) {
                IsWorking = true;
                CurrentTask.Events += new PackageInstallProgress((name, progress, overallProgress) => {
                    Progress = overallProgress;
                });

                var instTask = _packageManager.Install(SelectedPackage.CanonicalName, autoUpgrade: false);

                instTask.Continue(() => {
                    _finalText = "The package ({0}) has been installed.".format(SelectedPackage.Version);
                    OnFinished();
                });
                
                instTask.ContinueOnFail(exception => DoError(InstallerFailureState.FailedToGetPackageFromFile, exception));
                return instTask;
            }
            return null;
        }

        public Task RemoveAll() {
            return RemovePackages(PrimaryPackage.InstalledPackages.Select(each => each.CanonicalName).ToArray());
        }

        private Task RemovePackage(CanonicalName canonicalVersion) {
            return RemovePackages(new[] {canonicalVersion});
        }

        private Task RemovePackages(CanonicalName[] canonicalVersions) {
            if (!IsWorking) {
                IsWorking = true;
                return Task.Factory.StartNew(() => {
                    var taskCount = canonicalVersions.Length;
                    if (canonicalVersions.Length > 0) {
                        int taskNumber = 0;
                        CurrentTask.Events += new PackageRemoveProgress((name, progress) => Progress = (progress/taskCount) + taskNumber*100/taskCount);

                        for (var index = 0; index < taskCount; index++) {
                            taskNumber = index;
                            _packageManager.RemovePackage(canonicalVersions[index], false).Wait();
                        }
                    }
                }).ContinueWith(antecedent => {
                    _finalText = "Packages have been removed.".format(SelectedPackage.Version);
                    OnFinished();
                }, TaskContinuationOptions.AttachedToParent);
            }
            return null;
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