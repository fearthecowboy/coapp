//-----------------------------------------------------------------------
// <copyright company="CoApp Project">
//     Copyright (c) 2011 Garrett Serack . All rights reserved.
// </copyright>
// <license>
//     The software is licensed under the Apache 2.0 License (the "License")
//     You may not use the software except in compliance with the License. 
// </license>
//-----------------------------------------------------------------------

namespace CoApp.Toolkit.Engine.Client {
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
    using System.Windows.Input;
    using System.Windows.Media.Imaging;
    using Extensions;
    using Logging;
    using Toolkit.Exceptions;
    using UI;
    using Application = System.Windows.Application;
    using MessageBox = System.Windows.Forms.MessageBox;


    [Flags]
    public enum InstallChoice {
        _Unknown = 0,
        // this choice is informational, not valid
        _InvalidChoice  = 0x10000000,

        // flags indicating that during the install, the package installed should be marked.
        _DoNotUpdate    = 0x00010000,
        _DoNotUpgrade   = 0x00020000,
        
        // flags indicating which actual package to install
        _InstallSpecificVersion = 0x00000001,
        _InstallLatestUpdate    = 0x00000002,
        _InstallLatestUpgrade   = 0x00000004,


        _Scenario1 = 0x01000000,
        _Scenario2 = 0x02000000,
        _Scenario3  = 0x04000000,
        _Scenario4  = 0x08000000,

        AutoInstallLatest = _Scenario1 | _InstallLatestUpgrade,
        AutoInstallLatestCompatible = _Scenario1 | _DoNotUpgrade | _InstallLatestUpdate,
        InstallSpecificVersion = _Scenario1 | _DoNotUpdate | _InstallSpecificVersion,

        UpdateToLatestVersion = _Scenario2 | _InstallLatestUpdate,
        UpdateToLatestVersionNotUpgrade = _Scenario2 | _DoNotUpgrade | _InstallLatestUpdate,
        UpgradeToLatestVersion = _Scenario2 | _InstallLatestUpgrade,

        UpgradeToLatestVersion2 = _Scenario3 | _InstallLatestUpgrade,
        UpdateToLatestVersionNotUpgrade2 = _Scenario3 | _DoNotUpgrade | _InstallLatestUpdate,
        InstallSpecificVersion2 = _Scenario3 | _DoNotUpdate | _DoNotUpgrade | _InstallSpecificVersion,

        UpgradeToLatestVersion3 = _Scenario4 | _InstallLatestUpgrade,
        // UpdateToLatestVersion3          = _Scenario4 | _DoNotUpgrade | 0x002,
        AutoInstallLatestCompatible3 = _Scenario4 | _DoNotUpgrade | _InstallLatestUpdate,
        InstallSpecificVersion3 = _Scenario4 | _DoNotUpdate | _InstallSpecificVersion,

        NewerVersionAlreadyInstalled = _InvalidChoice | 0x001,
        OlderVersionAlreadyInstalled = _InvalidChoice | 0x002,
        ThisVersionAlreadyInstalled  = _InvalidChoice | 0x003
    }

    internal enum InstallerFailureState {
        FailedToGetPackageFromFile,
        FailedToGetPackageDetails
    }

    public class InstSelection {
        public InstSelection(InstallChoice key, string value, params object[] args) {
            Key = key;
            Value = value.format(args);
        }

        public InstallChoice Key { get; set; }
        public string Value{ get; set; }
    }

    public class RemoveCommand {
        public string Text { get; set; }
        public Action CommandParam { get; set; }
    }

    public class Installer : MarshalByRefObject, INotifyPropertyChanged {
        internal string MsiFilename;
        internal Task InstallTask;
        public event PropertyChangedEventHandler PropertyChanged;

        public event PropertyChangedEventHandler Ready;
        public event PropertyChangedEventHandler Finished;
        
        private InstallChoice _choice = InstallChoice.InstallSpecificVersion;
        public InstallChoice Choice {
            get { return _choice; }
            set { 
                _choice = value;
                // PackageIcon = GetPackageBitmap(SelectedPackage.Icon);
                OnPropertyChanged();
                OnPropertyChanged("Choice");
            }
        }
      
        public IEnumerable<RemoveCommand> RemoveChoices {
            get {
                if (HasPackage) {
                    var ct = PackageSet.InstalledPackages == null ? 0: PackageSet.InstalledPackages.Count();
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

                            if (PackageSet.Package.IsInstalled) {
                                yield return new RemoveCommand {
                                    Text = "Remove this version ({0})".format(PackageSet.Package.Version),
                                    CommandParam = () => RemovePackage(PackageSet.Package.CanonicalName)
                                };
                            }

                            if (PackageSet.InstalledNewest != null ) {
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
        }

        public IEnumerable<InstSelection> InstallChoices {
            get {
                _choice = InstallChoice._Unknown;

                if (HasPackage) {
                    // When there isn't anything at all installed already
                    if (PackageSet.InstalledNewest == null) {
                        if (PackageSet.AvailableNewer != null) {
                            yield return new InstSelection(InstallChoice.AutoInstallLatest, "Install the latest version of this package ({0})", PackageSet.AvailableNewer.Version);
                            _choice = InstallChoice.AutoInstallLatest;
                        }

                        if (PackageSet.AvailableNewerCompatible != null && PackageSet.AvailableNewerCompatible != PackageSet.AvailableNewer) {
                            yield return new InstSelection(InstallChoice.AutoInstallLatestCompatible, "Install the latest compatible version of this package ({0})", PackageSet.AvailableNewerCompatible.Version);
                            if (_choice == InstallChoice._Unknown) {
                                _choice = InstallChoice.AutoInstallLatest;
                            }
                        }

                        if (_choice == InstallChoice._Unknown) {
                            _choice = InstallChoice.InstallSpecificVersion;
                        }

                        yield return new InstSelection(InstallChoice.InstallSpecificVersion, "Install this version of package ({0})", PackageSet.Package.Version);
                    } else {
                        if (PackageSet.Package == PackageSet.InstalledNewest) {

                            yield return new InstSelection(InstallChoice.ThisVersionAlreadyInstalled, "This version is currently installed ({0})", PackageSet.Package.Version);
                            _choice = InstallChoice.ThisVersionAlreadyInstalled;

                            if (PackageSet.AvailableNewerCompatible != null && PackageSet.AvailableNewer == null) {
                                yield return new InstSelection(InstallChoice.UpdateToLatestVersion, "Update to the latest compatible version ({0})", PackageSet.AvailableNewerCompatible.Version);
                            }

                            if (PackageSet.AvailableNewerCompatible != null && PackageSet.AvailableNewer != null) {
                                yield return new InstSelection(InstallChoice.UpdateToLatestVersionNotUpgrade, "Update to the latest compatible version ({0})", PackageSet.AvailableNewerCompatible.Version);
                            }

                            if (PackageSet.AvailableNewer != null) {
                                yield return new InstSelection(InstallChoice.UpgradeToLatestVersion, "Upgrade to the latest version ({0})", PackageSet.AvailableNewer.Version);
                            }

                        } else if (PackageSet.InstalledNewest.Version > PackageSet.Package.Version) {
                            // a newer version is already installed    
                            yield return new InstSelection(InstallChoice.NewerVersionAlreadyInstalled, "A newer version is currently installed ({0})", PackageSet.InstalledNewest.Version);
                            _choice = InstallChoice.NewerVersionAlreadyInstalled;

                            if (PackageSet.AvailableNewer != null) {
                                yield return new InstSelection(InstallChoice.UpgradeToLatestVersion2, "Upgrade to the latest version ({0})", PackageSet.AvailableNewer.Version);
                            }

                            if (PackageSet.AvailableNewerCompatible != null) {
                                yield return new InstSelection(InstallChoice.UpdateToLatestVersionNotUpgrade2, "Install the latest compatible version ({0})", PackageSet.AvailableNewerCompatible.Version);
                            }

                            if (PackageSet.Package.IsInstalled) {
                                yield return new InstSelection(InstallChoice.ThisVersionAlreadyInstalled, "This version is currently installed ({0})", PackageSet.Package.Version);
                            } else {
                                yield return new InstSelection(InstallChoice.InstallSpecificVersion2, "Install this version of package ({0})", PackageSet.Package.Version);
                            }
                        } else {
                            // an older version is installed
                            yield return new InstSelection(InstallChoice.OlderVersionAlreadyInstalled, "A older version is currently installed ({0})", PackageSet.InstalledNewest.Version);
                            _choice = InstallChoice.OlderVersionAlreadyInstalled;

                            if (PackageSet.AvailableNewer != null) {
                                yield return new InstSelection(InstallChoice.UpgradeToLatestVersion2, "Upgrade to the latest version of this package ({0})", PackageSet.AvailableNewer.Version);
                            }

                            if (PackageSet.AvailableNewerCompatible != null) {
                                yield return new InstSelection(InstallChoice.AutoInstallLatestCompatible3, "Install the latest version of this package ({0})", PackageSet.AvailableNewer.Version);
                            }

                            yield return new InstSelection(InstallChoice.InstallSpecificVersion2, "Install this version of package ({0})", PackageSet.Package.Version);
                        }
                    }
                }
                OnPropertyChanged("Choice");
            }
        }

        private PackageSet _packageSet;
        private PackageSet PackageSet {
            get {
                return _packageSet;
            }

            set {
                _packageSet = value;
                OnPropertyChanged();
                OnPropertyChanged("InstallChoices");
                OnPropertyChanged("RemoveChoices");
                OnPropertyChanged("Choice");
                if (Ready != null) {
                    Ready(this, new PropertyChangedEventArgs("Choice"));
                }
            }
        }

        public Package SelectedPackage { get {
            if( PackageSet == null ) {
                return null;
            }

            if (Choice == InstallChoice.NewerVersionAlreadyInstalled) {
                return PackageSet.InstalledNewer ?? PackageSet.InstalledNewerCompatable;
            }

            if (Choice == InstallChoice.OlderVersionAlreadyInstalled) {
                return PackageSet.InstalledOlder ?? PackageSet.InstalledOlderCompatable;
            }

            if (Choice == InstallChoice.ThisVersionAlreadyInstalled) {
                return PackageSet.Package;
            }

            if( Choice.HasFlag(InstallChoice._InstallSpecificVersion)) {
                return PackageSet.Package;
            }

            if (Choice.HasFlag(InstallChoice._InstallLatestUpgrade)) {
                return PackageSet.AvailableNewer;
            }

            if (Choice.HasFlag(InstallChoice._InstallLatestUpdate)) {
                return PackageSet.AvailableNewerCompatible;
            }

            return PackageSet.InstalledNewest ?? PackageSet.Package;
        } }

        public bool CanRemove { get { return HasPackage && RemoveChoices.Any(); } }
        public bool CanInstall { get { return HasPackage && !SelectedPackage.IsInstalled; } }
        public bool HasPackage { get { return SelectedPackage != null; } }

        public bool ReadyToInstall {
            get { return HasPackage && SelectedPackage.IsInstalled == false; }
        }
        
        private int _progress;
        public int Progress {
            get { return _progress; } set { _progress = value; OnPropertyChanged("Progress");}
        }

        public Visibility RemoveButtonVisibility { get { return CanRemove ? Visibility.Visible : Visibility.Hidden; } }
        public Visibility CancelButtonVisibility { get { return CancelRequested ? Visibility.Hidden : Visibility.Visible; } }

        public bool IsInstalled { get { return HasPackage && SelectedPackage.IsInstalled; } }

        public string Organization { get { return HasPackage ? SelectedPackage.PublisherName : string.Empty; } }
        public string Description { get { return HasPackage ? SelectedPackage.Description : string.Empty; } }

        public string Product {
            get {
                return HasPackage ? "{0} - {1}".format(SelectedPackage.DisplayName, string.IsNullOrEmpty(SelectedPackage.AuthorVersion) ? (string)SelectedPackage.Version : SelectedPackage.AuthorVersion) : string.Empty;
            }
        }

        public string ProductVersion {
            get {
                return HasPackage ? (string)SelectedPackage.Version : string.Empty;
            }
        }

        private bool _working;
        public bool IsWorking {
            get { return _working; }
            set {
                _working = value;
                OnPropertyChanged("Working");
            }
        }

        private bool _cancel;
        public bool CancelRequested {
            get { return _cancel; }
            set {
                if( IsWorking) {
                    // packageManager.StopInstall? 
                }
                _cancel = value;
                OnPropertyChanged("CancelRequested");
                OnPropertyChanged("CancelButtonVisibility");

                if( !IsWorking ) {
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
        public BitmapImage PackageIcon { get { return _packageIcon; }
            set {
                _packageIcon = value;
                OnPropertyChanged("PackageIcon");
            }
        }

        public string InstallButtonText {
            get {
                switch( Choice ) {
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
        private EasyPackageManager _easyPackageManager = new EasyPackageManager();
        private EventWaitHandle _ping;

        internal bool Ping { get {
            return _ping.WaitOne(0);
        } 
        set {
            if( value ) {
                _ping.Set();
            } else {
                _ping.Reset();
            }
        }}

        private InstallerMainWindow window;
        public Installer(string filename) {
            try {
                MsiFilename = filename;
                Task.Factory.StartNew(() => {
                    // was coapp just installed by the bootstrapper? 
                    if (((AppDomain.CurrentDomain.GetData("COAPP_INSTALLED") as string) ?? "false").IsTrue()) {
                        // we'd better make sure that the most recent version of the service is running.
                        EngineServiceManager.InstallAndStartService();
                    }
                    InstallTask = LoadPackageDetails();
                });
                
                bool wasCreated;
                var ewhSec = new EventWaitHandleSecurity();
                ewhSec.AddAccessRule(new EventWaitHandleAccessRule(new SecurityIdentifier(WellKnownSidType.WorldSid, null), EventWaitHandleRights.FullControl, AccessControlType.Allow));
                _ping = new EventWaitHandle(false, EventResetMode.ManualReset, "BootstrapperPing", out wasCreated, ewhSec);

                // if we got this far, CoApp must be running. 
                try {
                    Application.ResourceAssembly = Assembly.GetExecutingAssembly();
                }
                catch { }

                window = new InstallerMainWindow(this);
                window.ShowDialog();

                if (Application.Current != null) {
                    Application.Current.Shutdown(0);
                }
                ExitQuick();
            } catch (Exception e) {
                DoError(InstallerFailureState.FailedToGetPackageDetails, e);

            }
        }

        private void DoError(InstallerFailureState state, Exception error) {
            MessageBox.Show(error.StackTrace, error.Message,MessageBoxButtons.OK,MessageBoxIcon.Exclamation,MessageBoxDefaultButton.Button1);
            switch( state ) {
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
            }
            catch {
            }
            Environment.Exit(0);
        }

        private Task LoadPackageDetails() {
            return  _easyPackageManager.GetPackageFromFile(Path.GetFullPath(MsiFilename)).ContinueWith(antecedent => {
                if (antecedent.IsFaulted) {
                    DoError(InstallerFailureState.FailedToGetPackageFromFile, antecedent.Exception.Unwrap());
                    return;
                }

                _easyPackageManager.GetPackageDetails(antecedent.Result).ContinueWith(antecedent2 => {
                    if( antecedent2.IsFaulted ) {
                        DoError(InstallerFailureState.FailedToGetPackageDetails, antecedent2.Exception.Unwrap());
                        return;
                    }

                    // PackageSet = new PackageSet {Package = antecedent2.Result};
                    // PackageIcon = GetPackageBitmap(SelectedPackage.Icon);

                    // now get additonal package information...
                    _easyPackageManager.GetPackageSet(antecedent2.Result.CanonicalName).ContinueWith(
                        antecedent3 => {
                            if (antecedent3.IsFaulted) {
                                DoError(InstallerFailureState.FailedToGetPackageDetails, antecedent3.Exception.Unwrap());
                                return;
                            }
                            PackageSet = antecedent3.Result;
                            Task.Factory.StartNew(() => {
                                Thread.Sleep(140);
                                window.Dispatcher.Invoke((Action)(window.FixFont));
                            });
                        });
                });
            });
        }

        private void OnFinished() {
            IsWorking = false;
            if (Finished != null) {
                Finished(this, new PropertyChangedEventArgs("Finished"));
            }
        }

        private static BitmapImage GetPackageBitmap( string iconData ) {
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
                                if( File.Exists(filename)) {
                                    return GetPackageBitmap(filename);
                                }
                            } catch { }
                            break;

                        case "file":
                            var image = new BitmapImage();
                            image.BeginInit();
                            using( var srcStream = File.OpenRead(uri.LocalPath)) {
                                image.StreamSource = srcStream;
                                image.EndInit();
                                return image;
                            }
                            break;
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
            if( !IsWorking) {
                IsWorking = true;

                var instTask = _easyPackageManager.InstallPackage(SelectedPackage.CanonicalName, autoUpgrade: false, installProgress: (canonicalName, progress, overallProgress) => { Progress = overallProgress; });
                
                instTask.Continue(() => {
                    OnFinished();    
                });

                instTask.ContinueOnFail((exception) => {
                    DoError( InstallerFailureState.FailedToGetPackageFromFile, exception);
                });

            }
        }

        public void RemoveAll() {
            RemovePackages(PackageSet.InstalledPackages.Select(each => each.CanonicalName).ToArray());
        }

        private void RemovePackage( string canonicalVersion ) {
            RemovePackages(new []{ canonicalVersion});
        }

        private void RemovePackages(string[] canonicalVersions) {
            if (!IsWorking) {
                IsWorking = true;
                Task.Factory.StartNew(()=> {
                    var taskCount = canonicalVersions.Length;
                    if (canonicalVersions.Length > 0) {
                        for (var index = 0; index < taskCount; index++) {
                            var taskNumber = index;
                            var v = canonicalVersions[index];

                            PackageManager.Instance.RemovePackage(
                                v, messages: new PackageManagerMessages {
                                    RemovingPackageProgress = (canonicalName, progress) => {
                                        Progress = (progress / taskCount) + taskNumber * 100 / taskCount;
                                    },
                                    RemovedPackage = (canonicalName) => {
                                        Package.GetPackage(canonicalName).IsInstalled = false;
                                    },
                                    OperationCanceled = CancellationRequestedDuringRemove,
                                }).Wait();
                        }
                    }
                    }).ContinueWith(antecedent => {OnFinished();}, TaskContinuationOptions.AttachedToParent);

            }
        }


        protected void OnPropertyChanged(string name=null) {
            if (PropertyChanged != null) {
                if (name == null) {
                    foreach (var propertyName in GetType().GetProperties().Where(each => each.Name != "InstallChoices" ).Select(each => each.Name)) {
                        PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
                    }
                } else {
                    PropertyChanged(this, new PropertyChangedEventArgs(name));
                }
            }
        }
    }
}