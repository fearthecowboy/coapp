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
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using Common;
    using Toolkit.Collections;
    using Toolkit.Configuration;
    using Toolkit.Crypto;
    using Toolkit.Extensions;
    using Toolkit.Tasks;

    /// <summary>
    ///   This stores information that is really only relevant to the currently running Session, not between sessions. The instance of this is bound to the Session.
    /// </summary>
    internal class PackageSessionData : NotifiesPackageManager {
        internal bool DoNotSupercede; // TODO: it's possible these could be contradictory
        internal bool UpgradeAsNeeded; // TODO: it's possible these could be contradictory
        internal bool IsWanted;
        internal bool HasRequestedDownload;

        internal bool IsDependency;

        private bool _couldNotDownload;
        private Package _supercedent;
        private bool _packageFailedInstall;
        private readonly Package _package;
        private string _localValidatedLocation;

        internal PackageSessionData(Package package) {
            _package = package;
        }

        public Package Supercedent {
            get {
                return _supercedent;
            }
            set {
                if (value != _supercedent) {
                    _supercedent = value;
                    Changed();
                }
            }
        }

        public bool PackageFailedInstall {
            get {
                return _packageFailedInstall;
            }
            set {
                if (_packageFailedInstall != value) {
                    _packageFailedInstall = value;
                    Changed();
                }
            }
        }

        public bool CouldNotDownload {
            get {
                return _couldNotDownload;
            }
            set {
                if (value != _couldNotDownload) {
                    _couldNotDownload = value;
                    Changed();
                }
            }
        }

        public bool AllowedToSupercede {
            get {
                return UpgradeAsNeeded || (!IsWanted && !DoNotSupercede) && IsPotentiallyInstallable;
            }
        }

        public bool IsPotentiallyInstallable {
            get {
                return !PackageFailedInstall && (_package.HasLocalLocation || !CouldNotDownload && _package.HasRemoteLocation);
            }
        }

        public bool CanSatisfy { get; set; }

        public string LocalValidatedLocation {
            get {
                var remoteInterface = Event<GetResponseInterface>.RaiseFirst();

                if (!string.IsNullOrEmpty(_localValidatedLocation) && _localValidatedLocation.FileIsLocalAndExists()) {
                    return _localValidatedLocation;
                }

                foreach (var loc in _package.LocalLocations) {
                    var location = loc.CanonicalizePathIfLocalAndExists();

                    if (!string.IsNullOrEmpty(location)) {
                        var result = Verifier.HasValidSignature(location);
                        if (remoteInterface != null) {
                            // only call this when we're connected to a client
                            Event<GetResponseInterface>.RaiseFirst().SignatureValidation(location, result, result ? Verifier.GetPublisherName(location) : null);
                        }

                        if (result) {
                            // looks valid, return it. 
                            return (_localValidatedLocation = location);
                        }
                    }
                }

                // there are no local locations at all for this package?
                return null;
            }
        }

        private RegistryView _packageSettings;

        internal RegistryView PackageSettings {
            get {
                return _packageSettings ?? (_packageSettings = PackageManagerSettings.PerPackageSettings[_package.CanonicalName]);
            }
        }

        private int _lastProgress;
        public int DownloadProgress { get; set; }

        public int DownloadProgressDelta {
            get {
                var p = DownloadProgress;
                var result = p - _lastProgress;
                if (result < 0) {
                    return 0;
                }

                _lastProgress = p;
                return result;
            }
        }

        public bool WaitForFileDownloads() {
            if( _downloadQueue.IsNullOrEmpty()) {
                return true;
            }
            var result = _downloadQueue.All(each => each.Result != null);
            _downloadQueue = null;
            return result;
        }

        private XList<Task<string>> _downloadQueue;

        public void DownloadFile(string url, string destination) {
            var response = Event<GetResponseInterface>.RaiseFirst();
            if (response == null) {
                return;
            }

            lock (SessionCache<Task<string>>.Value) {
                Task<string> completion = SessionCache<Task<string>>.Value[url];

                if (completion != null) {
                    return; // completion.ContinueAlways(antecedent => antecedent.Result);
                }

                // otherwise, let's create a delegate to run when the file gets resolved.
                completion = new Task<string>(rrfState => {
                    var state = rrfState as RequestRemoteFileState;

                    if (state == null || string.IsNullOrEmpty(state.LocalLocation) || !File.Exists(state.LocalLocation)) {
                        // didn't fill in the local location? -- this happens when the client can't download.
                        return null;
                    }

                    if (!state.LocalLocation.Equals(destination, StringComparison.CurrentCultureIgnoreCase)) {
                        File.Copy(state.LocalLocation, destination);
                    }

                    return destination;
                }, new RequestRemoteFileState {
                    OriginalUrl = url
                }, TaskCreationOptions.AttachedToParent);

                // store the task until the client tells us that it has the file.
                SessionCache<Task<string>>.Value[url] = completion;
                _downloadQueue = _downloadQueue ?? new XList<Task<string>>();
                _downloadQueue.Add(completion);
            }

            response.RequireRemoteFile(null, new Uri(url).SingleItemAsEnumerable(), PackageManagerSettings.CoAppCacheDirectory, false);
        }
    }
}