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

namespace CoApp.Toolkit.Engine.Client {
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using Exceptions;
    using Extensions;
    using Logging;
    using Tasks;
    using Toolkit.Exceptions;
    using Win32;
    using OperationCanceledException = System.OperationCanceledException;

    public class Feed {
        public string Location { get; internal set; }
        public DateTime LastScanned { get; internal set; }
        public bool IsSession { get; internal set; }
        public bool IsSuppressed { get; internal set; }
    }

    public class Policy {
        public string Name { get; internal set; }
        public string Description { get; internal set; }
        public IEnumerable<string> Members { get; internal set; }
    }

    public class LoggingSettings {
        public bool Messages { get; internal set; }
        public bool Warnings { get; internal set; }
        public bool Errors { get; internal set; }
    }

    public class ScheduledTask {
        public string Name { get; set; }
        public string Executable { get; set; }
        public string CommandLine { get; set; }
        public int Hour { get; set; }
        public int Minutes { get; set; }
        public DayOfWeek? DayOfWeek { get; set; }
        public int IntervalInMinutes { get; set; }
    }

    public class Publisher {
        public string Name { get; set; }
        public string PublicKeyToken { get; set; }
    }

    public class PackageSet {
        public Package Package;

        /// <summary>
        ///   The newest version of this package that is installed and newer than the given package and is binary compatible.
        /// </summary>
        public Package InstalledNewerCompatable;

        /// <summary>
        ///   The newest version of this package that is installed, and newer than the given package
        /// </summary>
        public Package InstalledNewer;

        /// <summary>
        ///   The newest package that is currently installed, that the given package is a compatible update for.
        /// </summary>
        public Package InstalledOlderCompatable;

        /// <summary>
        ///   The newest package that is currently installed, that the give package is an upgrade for.
        /// </summary>
        public Package InstalledOlder;

        /// <summary>
        ///   The latest version of the package that is available that is newer than the current package.
        /// </summary>
        public Package AvailableNewer;

        /// <summary>
        ///   The latest version of the package that is available and is binary compatable with the given package
        /// </summary>
        public Package AvailableNewerCompatible;

        /// <summary>
        ///   The latest version that is installed.
        /// </summary>
        public Package InstalledNewest;

        /// <summary>
        ///   All Installed versions of this package
        /// </summary>
        public IEnumerable<Package> InstalledPackages;

        /// <summary>
        ///   All the trimable packages for this package
        /// </summary>
        public IEnumerable<Package> Trimable;
    }

    public class EasyPackageManager {
        internal class RemoteCallResponse : PackageManagerMessages {
            internal bool EngineRestarting;
            internal bool NoPackages;
            // internal Exception Exception;

            internal string OperationCanceledReason;

            internal Action<string, string, int> DownloadProgress;
            internal Action<string, string> DownloadCompleted;

            internal static Dictionary <string, Task> _currentDownloads = new Dictionary<string, Task>();

            public RemoteCallResponse() {
                PermissionRequired = permission => {
                    throw new RequiresPermissionException(permission);
                };

                OperationCanceled = reason => {
                    OperationCanceledReason = reason;
                };

                UnexpectedFailure = exception => {
                    throw exception;
                };

                NoPackagesFound = () => {
                    NoPackages = true;
                };

                Error = (message, parameter, reason) => {
                    if( message == "add-feed" ) {
                        throw new CoAppException(reason);
                    }
                    throw new CoAppException("Message Argument Exception [{0}/{1}/{2}]".format(message, parameter, reason));
                };

                PackageSatisfiedBy = (original, satisfiedBy) => {
                    original.SatisfiedBy = satisfiedBy;
                };

                UnknownPackage = s => {
                    throw new UnknownPackageException(s);
                };

                FailedPackageRemoval = (canonicalname, reason) => {
                    throw new FailedPackageRemoveException(canonicalname, reason);
                };

                FailedPackageInstall = (canonicalname, filename, reason) => {
                    throw new CoAppException("Package Failed Install {0} => {1}".format(canonicalname, reason));
                };

                Restarting = () => {
                    EngineRestarting = true;
                    // throw an exception here to quickly short circuit the rest of this call
                    throw new Exception("restarting");
                };

                PackageBlocked = canonicalname => {
                    throw new PackageBlockedException(canonicalname);
                };

                RequireRemoteFile = (canonicalName, remoteLocations, targetFolder, force) => {
                    var targetFilename = Path.Combine(targetFolder, canonicalName);
                    lock (_currentDownloads) {

                        if (_currentDownloads.ContainsKey(targetFilename)) {
                                // wait for this guy to respond (which should give us what we need)
                                _currentDownloads[targetFilename].Continue(() => {
                                    if (File.Exists(targetFilename)) {
                                        if (DownloadProgress != null) {
                                            DownloadCompleted(canonicalName, targetFilename);
                                        }
                                        PackageManager.Instance.RecognizeFile(canonicalName, targetFilename, remoteLocations.FirstOrDefault(), this);
                                    }
                                    return;
                                });
                            return;
                        }

                        // gotta download the file...
                        var task = Task.Factory.StartNew(() => {
                            foreach (var location in remoteLocations) {
                                try {

                                    // a filesystem location (remote or otherwise)
                                    var uri = new Uri(location);
                                    if (uri.IsFile) {
                                        // try to copy the file local.
                                        var remoteFile = uri.AbsoluteUri.CanonicalizePath();

                                        // if this fails, we'll just move down the line.
                                        File.Copy(remoteFile, targetFilename);
                                        PackageManager.Instance.RecognizeFile(canonicalName, targetFilename, uri.AbsoluteUri, this);
                                        return;
                                    }

                                    // A web location
                                    Task progressTask = null;
                                    var success = false;
                                    var rf = new RemoteFile(uri, targetFilename,
                                        completed : (itemUri) => {
                                            PackageManager.Instance.RecognizeFile(canonicalName, targetFilename, uri.AbsoluteUri, this);
                                            if (DownloadCompleted != null) {
                                                // remove it from the list of current downloads
                                                _currentDownloads.Remove(targetFilename);

                                                // give the user notification
                                                DownloadCompleted(itemUri.AbsoluteUri, targetFilename);
                                                success = true;
                                            }
                                        },

                                        failed : (itemUri) => {
                                            // Logger.Message("FAILED DOWNLOAD FILE : {0}", uri);
                                            success = false;
                                        },

                                        progress : (itemUri, percent) => {
                                            if (progressTask == null) {
                                                // report progress to the engine
                                                progressTask = PackageManager.Instance.DownloadProgress(canonicalName, percent);
                                                progressTask.Continue(() => {
                                                    progressTask = null;
                                                });
                                            }

                                            // report progress to the user
                                            if (DownloadProgress != null) {
                                                DownloadProgress(itemUri.AbsoluteUri, targetFolder, percent);
                                            }
                                        });

                                    rf.Get();

                                    if (success && File.Exists(targetFilename)) {
                                        return;
                                    }

                                } catch (Exception e) {
                                    // bogus, dude.
                                    // try the next one.
                                    Logger.Error(e);
                                }
                                // loop around and try again?
                            }

                            // was there a file there from before?
                            if (File.Exists(targetFilename)) {
                                if (DownloadProgress != null) {
                                    DownloadCompleted(canonicalName, targetFilename);
                                }
                                PackageManager.Instance.RecognizeFile(canonicalName, targetFilename, remoteLocations.FirstOrDefault(), this);
                            }

                            // remove it from the list of current downloads
                            _currentDownloads.Remove(targetFilename);
                            
                            // if we got here, that means we couldn't get the file. too bad, so sad.
                            PackageManager.Instance.UnableToAcquire(canonicalName, new PackageManagerMessages());
                        }, TaskCreationOptions.AttachedToParent);

                        _currentDownloads.Add(targetFilename, task);
                    }
                };

                Register();
            }

            public void ThrowWhenFaulted(Task antecedent) {
                // do not get all fussy when the engine is restarting.
                if (EngineRestarting) {
                    return;
                }

                if (!string.IsNullOrEmpty(OperationCanceledReason)) {
                    throw new OperationCanceledException(OperationCanceledReason);
                }

                antecedent.RethrowWhenFaulted();
            }
        }

        private readonly Action<string, string, int> _downloadProgress;
        private readonly Action<string, string> _downloadCompleted;

        public EasyPackageManager(Action<string, string, int> downloadProgress = null, Action<string, string> downloadCompleted = null) {
            _downloadProgress = downloadProgress ?? ((s, s2, i) => {
            });
            _downloadCompleted = downloadCompleted ?? ((s, i) => {
            });
        }

        /// <summary>
        ///   Returns a collection of packages that are filtered to the platform switches passed on the command line.
        /// </summary>
            /// <param name="packages"> The collection to filter packages for </param>
        /// <param name="x86"> Accept x86 packages? </param>
        /// <param name="x64"> Accept x64 packages? </param>
        /// <param name="cpuany"> Accept CPUANY packages? </param>
        /// <returns> </returns>
        public IEnumerable<Package> FilterPackagesForPlatforms(IEnumerable<Package> packages, bool? x86 = null, bool? x64 = null, bool? cpuany = null) {
            if ((true == x64) || (true == x86) || (true == cpuany)) {
                return packages.Where(each => (x64 == true && each.Architecture == Architecture.x64) || (x86 == true && each.Architecture == Architecture.x86) || (cpuany == true && each.Architecture == Architecture.Any));
            }
            return packages;
        }

        /// <summary>
        ///   makes sure that there isn't a conflict in the packages that are being installed. This will call FilterPackagesForPlatforms, so you don't need to call that beforehand.
        /// </summary>
        /// <param name="packages"> the collection of packages to check for conflicts </param>
        /// <param name="x86"> </param>
        /// <param name="x64"> </param>
        /// <param name="cpuany"> </param>
        /// <returns> A filtered collection of packages to install </returns>
        public Task<IEnumerable<Package>> FilterConflictsForInstall(IEnumerable<Package> packages, bool? x86 = null, bool? x64 = null, bool? cpuany = null) {
            return Task.Factory.StartNew(() => {
                var isFiltering = (true == x64) || (true == x86) || (true == cpuany);

                var pkgs = FilterPackagesForPlatforms(packages, x86, x64, cpuany).Distinct().ToArray();

                // get an collection for each package of all the other packages that it matches.
                var packageFamilies = pkgs.Select(package => pkgs.Where(each => each.Name == package.Name && each.PublicKeyToken == package.PublicKeyToken && (!isFiltering || each.Architecture == package.Architecture)).ToArray()).ToArray();

                // get the packages that could be conflicting.
                var conflictedFamilies = packageFamilies.Where(eachFamily => eachFamily.Count() > 1);

                if (conflictedFamilies.Any()) {
                    var nonConflictedPackages = packageFamilies.Where(eachFamily => eachFamily.Count() == 1).Select(eachFamily => eachFamily.FirstOrDefault());

                    if (!isFiltering) {
                        var actualConflicts = new List<Package[]>();
                        foreach (var conflictedPackages in conflictedFamilies) {
                            // we're really only interested in one platform for a given package here (since the user didn't specify any preference directly)

                            if (Environment.Is64BitOperatingSystem) {
                                // if there is a single x64 package, take that.
                                var x64Pkgs = conflictedPackages.Where(each => each.Architecture == Architecture.x64).ToArray();
                                if (x64Pkgs.Count() == 1) {
                                    nonConflictedPackages = nonConflictedPackages.Union(x64Pkgs);
                                    continue;
                                }

                                if (x64Pkgs.Count() > 1) {
                                    // we've got more than one package of a single platform. just add it to the conflicts and move along.
                                    actualConflicts.Add(conflictedPackages);
                                    continue;
                                }
                            }

                            // if there are no x64 packages, see if there is a single x86 package, take that.
                            var x86Pkgs = conflictedPackages.Where(each => each.Architecture == Architecture.x86).ToArray();
                            if (x86Pkgs.Length == 1) {
                                nonConflictedPackages = nonConflictedPackages.Union(x86Pkgs);
                                continue;
                            }

                            // if we got here, that means no x64  and no x86 packages, and we should just have a plurality of cpuany packages.
                            // we've got more than one package of a single platform, or that means no x64  and no x86 packages, 
                            // and we just have a plurality of cpuany packages.

                            // Either way just add it to the conflicts and move along.
                            actualConflicts.Add(conflictedPackages);
                            continue;
                        }
                        if (actualConflicts.Any()) {
                            throw new ConflictedPackagesException(actualConflicts);
                        }

                        // hmm, we resolved our conflicts automagically.
                        return nonConflictedPackages;
                    }
                    // architectures are not factored in here. 
                    // we must have multiple packages of a given architecture.
                    throw new ConflictedPackagesException(conflictedFamilies);
                }

                return pkgs;
            }, TaskCreationOptions.AttachedToParent);
        }

        /// <summary>
        ///   This identifies the actual list of packages to install for a given requested set of packages
        /// </summary>
        /// <param name="packages"> </param>
        /// <returns> </returns>
        public Task<IEnumerable<Package>> IdentifyPackageAndDependenciesToInstall(IEnumerable<Package> packages, bool? autoUpgrade = null, bool? download = null) {
            var pTasks = packages.Select(package => Install(package.CanonicalName, autoUpgrade, pretend: true, download: download)).ToArray();

            return pTasks.ContinueAlways(antecedentTasks => {
                antecedentTasks.RethrowWhenFaulted();

                return antecedentTasks.SelectMany(each => each.Result).Distinct();
            });
        }

        public Task Elevate() {
            return Task.Factory.StartNew(
                () => {
                });
        }

        public Task<bool> VerifyFileSignature(string filename) {
            var result = false;
            var handler = new RemoteCallResponse {
                SignatureValidation = (file, isValid, subject) => {
                    result = isValid;
                }
            };

            return PackageManager.Instance.VerifyFileSignature(filename, handler).ContinueAlways(antecedent => {
                if (handler.EngineRestarting) {
                    return VerifyFileSignature(filename).Result;
                }
                handler.ThrowWhenFaulted(antecedent);
                return result;
            });
        }

        public Task<Package> GetLatestInstalledVersion(string canonicalName) {
            return GetInstalledPackages(canonicalName).ContinueAlways(antecedent => {
                antecedent.RethrowWhenFaulted();
                return antecedent.Result.OrderByDescending(each => each.Version).FirstOrDefault();
            });
        }

        public Task<PackageSet> GetPackageSet(string canonicalName) {
            var result = new PackageSet();

            return GetPackage(canonicalName).Continue(package => {
                var tasks = new List<Task>();
                // the given package.
                result.Package = package;

                // get all the related packages
                var allPackages = GetAllVersionsOfPackage(canonicalName).Result.OrderByDescending(each => each.Version);

                result.InstalledPackages = allPackages.Where(each => each.IsInstalled).ToArray();
                result.InstalledNewerCompatable = NewestCompatablePackageIn(result.Package, result.InstalledPackages);
                result.InstalledNewer = result.InstalledPackages.FirstOrDefault(each => each.Version > result.Package.Version);

                result.InstalledNewest = result.InstalledPackages.FirstOrDefault();

                var notInstalledPackges = allPackages.Where(each => !each.IsInstalled).ToArray();
                result.AvailableNewerCompatible = NewestCompatablePackageIn(result.Package, notInstalledPackges);
                result.AvailableNewer = notInstalledPackges.FirstOrDefault(each => each.Version > result.Package.Version);

                result.InstalledOlderCompatable =
                    result.InstalledPackages.FirstOrDefault(
                        each => each.Version < result.Package.Version && result.Package.MinPolicy <= each.Version && result.Package.MaxPolicy >= each.Version);

                result.InstalledOlder =
                    result.InstalledPackages.FirstOrDefault(
                        each => each.Version < result.Package.Version && (result.Package.MinPolicy > each.Version || result.Package.MaxPolicy < each.Version));

                if (result.AvailableNewerCompatible == result.Package) {
                    result.AvailableNewerCompatible = null;
                }
                if (result.AvailableNewer == result.Package) {
                    result.AvailableNewer = null;
                }
                if (result.InstalledNewerCompatable == result.Package) {
                    result.InstalledNewerCompatable = null;
                }
                if (result.InstalledOlderCompatable == result.Package) {
                    result.InstalledOlderCompatable = null;
                }
                if (result.InstalledOlder == result.Package) {
                    result.InstalledOlder = null;
                }
                if (result.InstalledNewer == result.Package) {
                    result.InstalledNewer = null;
                }

                tasks.Add(GetTrimablePackages(canonicalName).ContinueWith(a2 => {
                    result.Trimable = !a2.IsFaulted ? a2.Result : Enumerable.Empty<Package>();
                }));

                if (result.InstalledNewer != null) {
                    tasks.Add(GetPackageDetails(result.InstalledNewer));
                }

                if (result.InstalledNewest != null) {
                    tasks.Add(GetPackageDetails(result.InstalledNewest));
                }

                if (result.InstalledNewerCompatable != null) {
                    tasks.Add(GetPackageDetails(result.InstalledNewerCompatable));
                }

                if (result.InstalledNewest != null) {
                    tasks.Add(GetPackageDetails(result.InstalledNewest));
                }

                if (result.InstalledOlder != null) {
                    tasks.Add(GetPackageDetails(result.InstalledOlder));
                }

                if (result.InstalledOlderCompatable != null) {
                    tasks.Add(GetPackageDetails(result.InstalledOlderCompatable));
                }

                if (result.AvailableNewer != null) {
                    tasks.Add(GetPackageDetails(result.AvailableNewer));
                }

                if (result.AvailableNewerCompatible != null) {
                    tasks.Add(GetPackageDetails(result.AvailableNewerCompatible));
                }

                if (result.Package != null) {
                    tasks.Add(GetPackageDetails(result.Package));
                }

                Task.WaitAll(tasks.ToArray());

                return result;
            });
        }

        private Package NewestCompatablePackageIn(Package aPackage, IEnumerable<Package> packages) {
            var result = aPackage;
            var pkgs = packages.OrderBy(each => each.Version).ToArray();
            Package pk;

            while ((pk = pkgs.FirstOrDefault(p => p.MinPolicy <= result.Version && p.MaxPolicy >= result.Version && result.Version < p.Version)) != null) {
                result = pk;
            }

            return result;
        }

        public Task<Package> GetLatestInstalledCompatableVersion(string canonicalName) {
            return GetPackage(canonicalName).ContinueWith(antecedent => {
                antecedent.RethrowWhenFaulted();
                if (antecedent.Result == null) {
                    throw new UnknownPackageException(canonicalName);
                }

                var result = NewestCompatablePackageIn(antecedent.Result, GetInstalledPackages(canonicalName).Result);
                return !result.IsInstalled ? null : result;
            }, TaskContinuationOptions.AttachedToParent);
        }

        public Task<Package> GetActiveVersion(string packageName) {
            return GetPackages(packageName, null, null, null, null, true).ContinueWith(antecedent => {
                antecedent.RethrowWhenFaulted();
                return antecedent.Result.FirstOrDefault();
            }, TaskContinuationOptions.AttachedToParent);
        }

        public Task<bool> IsPackageBlocked(string packageName) {
            return GetPackages(packageName, null, null, null, null, null, null, true).ContinueWith(antecedent => {
                antecedent.RethrowWhenFaulted();
                return antecedent.Result.Any();
            }, TaskContinuationOptions.AttachedToParent);
        }

        public Task<bool> IsPackageMarkedRequested(string canonicalName) {
            return GetPackage(canonicalName).ContinueWith(antecedent => {
                antecedent.RethrowWhenFaulted();
                return antecedent.Result.IsClientRequired;
            }, TaskContinuationOptions.AttachedToParent);
        }

        public Task<bool> IsPackageMarkedDoNotUpgrade(string canonicalName) {
            return GetPackage(canonicalName).ContinueWith(antecedent => {
                antecedent.RethrowWhenFaulted();
                return antecedent.Result.DoNotUpgrade;
            }, TaskContinuationOptions.AttachedToParent);
        }

        public Task<bool> IsPackageMarkedDoNotUpdate(string canonicalName) {
            return GetPackage(canonicalName).ContinueWith(antecedent => {
                antecedent.RethrowWhenFaulted();
                return antecedent.Result.DoNotUpdate;
            }, TaskContinuationOptions.AttachedToParent);
        }

        public Task<bool> IsPackageActive(string canonicalName) {
            return GetPackage(canonicalName).ContinueWith(antecedent => {
                antecedent.RethrowWhenFaulted();
                return antecedent.Result.IsActive;
            }, TaskContinuationOptions.AttachedToParent);
        }

        public Task BlockPackage(string packageName) {
            return SetPackageFlags(packageName, blocked: true);
        }

        public Task MarkPackageDoNotUpdate(string canonicalName) {
            return SetPackageFlags(canonicalName, doNotUpdate: true);
        }

        public Task MarkPackageDoNotUpgrade(string canonicalName) {
            return SetPackageFlags(canonicalName, doNotUpgrade: true);
        }

        public Task MarkPackageActive(string canonicalName) {
            return SetPackageFlags(canonicalName, active: true);
        }

        public Task MarkPackageRequested(string canonicalName) {
            return SetPackageFlags(canonicalName, requested: true);
        }

        public Task UnBlockPackage(string packageName) {
            return SetPackageFlags(packageName, blocked: false);
        }

        public Task MarkPackageOkToUpdate(string canonicalName) {
            return SetPackageFlags(canonicalName, doNotUpdate: false);
        }

        public Task MarkPackageOkToUpgrade(string canonicalName) {
            return SetPackageFlags(canonicalName, doNotUpgrade: false);
        }

        public Task MarkPackageNotRequested(string canonicalName) {
            return SetPackageFlags(canonicalName, requested: false);
        }

        private Task SetPackageFlags(string canonicalName, bool? active = null, bool? requested = null, bool? blocked = null, bool? doNotUpdate = null, bool? doNotUpgrade = null) {
            var failed = ValidateCanonicalName<IEnumerable<Package>>(canonicalName);
            if (failed != null) {
                return failed;
            }
            var handler = new RemoteCallResponse();

            return PackageManager.Instance.SetPackage(canonicalName, active, requested, blocked, doNotUpdate, doNotUpgrade).ContinueWith(antecedent => {
                if (handler.EngineRestarting) {
                    SetPackageFlags(canonicalName, active, requested, blocked, doNotUpdate, doNotUpgrade).Wait();
                    return;
                }
                handler.ThrowWhenFaulted(antecedent);
            }, TaskContinuationOptions.AttachedToParent);
        }

        public Task<IEnumerable<Package>> GetUpdatablePackages(string packageName) {
            return GetPackages(packageName, null, null, null, null, null, null, null, null, null, true);
        }

        public Task<IEnumerable<Package>> GetUpgradablePackages(string packageName) {
            return GetPackages(packageName, null, null, null, null, null, null, null, null, null, null, true);
        }

        public Task<IEnumerable<Package>> GetTrimablePackages(string packageName) {
            return GetPackages(packageName, null, null, null, null, null, null, null, null, null, null, null, true);
        }

        public Task<IEnumerable<Package>> GetUpdatablePackages(IEnumerable<string> packageNames) {
            return GetPackages(packageNames, null, null, null, null, null, null, null, null, null, true);
        }

        public Task<IEnumerable<Package>> GetUpgradablePackages(IEnumerable<string> packageNames) {
            return GetPackages(packageNames, null, null, null, null, null, null, null, null, null, null, true);
        }

        public Task<IEnumerable<Package>> GetTrimablePackages(IEnumerable<string> packageNames) {
            return GetPackages(packageNames, null, null, null, null, null, null, null, null, null, null, null, true);
        }

        public Task<IEnumerable<Package>> GetAllVersionsOfPackage(string packageName) {
            var parsedName = PackageName.Parse(packageName);
            return GetPackages(parsedName.Name + "-*.*.*.*-*-" + parsedName.PublicKeyToken);
        }

        public Task<IEnumerable<Package>> GetInstalledPackages(string packageName, bool? active = null, bool? requested = null, bool? blocked = null, string locationFeed = null) {
            return GetPackages(packageName, null, null, null, true, active, requested, blocked, null, locationFeed);
        }

        public Task<IEnumerable<Package>> GetInstalledPackages(IEnumerable<string> packageName, bool? active = null, bool? requested = null, bool? blocked = null, string locationFeed = null) {
            return GetPackages(packageName, null, null, null, true, active, requested, blocked, null, locationFeed);
        }

        public Task<Package> GetPackageFromFile(string filename) {
            return GetPackages(filename).ContinueWith(antecedent => {
                antecedent.RethrowWhenFaulted();
                var pkg = antecedent.Result.FirstOrDefault();
                if (pkg == null) {
                    throw new UnknownPackageException("filename: {0}".format(filename));
                }
                return pkg;
            }, TaskContinuationOptions.AttachedToParent);
        }

        public Task<IEnumerable<Package>> GetPackages(string packageName, FourPartVersion? minVersion = null, FourPartVersion? maxVersion = null, bool? dependencies = null, bool? installed = null,
            bool? active = null, bool? requested = null, bool? blocked = null, bool? latest = null, string locationFeed = null, bool? updates = null, bool? upgrades = null, bool? trimable = null) {
            return GetPackages(packageName.SingleItemAsEnumerable(), minVersion, maxVersion, dependencies, installed, active, requested, blocked, latest, locationFeed, updates, upgrades, trimable);
        }

        public Task<IEnumerable<Package>> GetPackages(IEnumerable<string> packageNames, FourPartVersion? minVersion = null, FourPartVersion? maxVersion = null, bool? dependencies = null,
            bool? installed = null, bool? active = null, bool? requested = null, bool? blocked = null, bool? latest = null, string locationFeed = null, bool? updates = null, bool? upgrades = null,
            bool? trimable = null) {

            var handler = new RemoteCallResponse {
                DownloadProgress = _downloadProgress,
                DownloadCompleted = _downloadCompleted,
            };

            if (packageNames.IsNullOrEmpty()) {
                packageNames = "*".SingleItemAsEnumerable();
            }

            return
                PackageManager.Instance.GetPackages(packageNames, minVersion, maxVersion, dependencies, installed, active, requested, blocked, latest, locationFeed, false, updates, upgrades, trimable,
                    handler).ContinueWith(antecedent => {
                        if (handler.EngineRestarting) {
                            return GetPackages(packageNames, minVersion, maxVersion, dependencies, installed, active, requested, blocked, latest, locationFeed, updates, upgrades, trimable).Result;
                        }
                        handler.ThrowWhenFaulted(antecedent);

                        if (!Environment.Is64BitOperatingSystem) {
                            // remove x64 packages from the result set if you're not on an x64 system.
                            return antecedent.Result.Where(each => each.Architecture != Architecture.x64);
                        }

                        return antecedent.Result;
                    }, TaskContinuationOptions.AttachedToParent);
        }

        private Task<TReturnType> ValidateCanonicalName<TReturnType>(string canonicalName) {
            if (!PackageName.Parse(canonicalName).IsFullMatch) {
                var failedResult = new TaskCompletionSource<TReturnType>();
                failedResult.SetException(new InvalidCanonicalNameException(canonicalName));
                return failedResult.Task;
            }
            return null;
        }

        private Task<IEnumerable<Package>> Install(string canonicalName, bool? autoUpgrade = null, bool? isUpdate = null, bool? isUpgrade = null, bool? pretend = null, bool? download = null,
            Action<string, int, int> installProgress = null,
            Action<string> packageInstalled = null) {
            var failed = ValidateCanonicalName<IEnumerable<Package>>(canonicalName);
            if (failed != null) {
                return failed;
            }


            var completedThisPackage = false;

            var result = new List<Package>();
            Package upgradablePackage = null;
            IEnumerable<Package> potentialUpgrades = null;

            var handler = new RemoteCallResponse {
                PackageInformation = (package) => result.Add(package),

                PackageSatisfiedBy = (pkg, satisfiedBy) => {
                    pkg.SatisfiedBy = satisfiedBy;
                    result.Add(pkg);
                },

                InstallingPackageProgress = (packageName, progress, overall) => {
                    if (overall == 100) {
                        completedThisPackage = true;
                    }
                    if (installProgress != null) {
                        installProgress(packageName, progress, overall);
                    }
                },

                PackageHasPotentialUpgrades = (package, supercedents) => {
                    upgradablePackage = package;
                    potentialUpgrades = supercedents;
                },

                InstalledPackage = packageInstalled,
                DownloadProgress = _downloadProgress,
                DownloadCompleted = _downloadCompleted,
            };

            return PackageManager.Instance.InstallPackage(canonicalName, autoUpgrade, false, download, pretend, isUpdate, isUpgrade, handler).ContinueWith(
                antecedent => {
                    if (handler.EngineRestarting) {
                        if (!completedThisPackage) {
                            return Install(canonicalName, autoUpgrade, isUpdate, isUpgrade, pretend, download, installProgress, packageInstalled).Result;
                        }
                    } else {
                        if (potentialUpgrades != null) {
                            throw new PackageHasPotentialUpgradesException(upgradablePackage, potentialUpgrades);
                        }
                        handler.ThrowWhenFaulted(antecedent);
                    }
                    return result.Distinct();
                }, TaskContinuationOptions.AttachedToParent);
        }

        public Task InstallPackage(string canonicalName, bool? autoUpgrade = null,
            Action<string, int, int> installProgress = null,
            Action<string> packageInstalled = null) {
            return Install(canonicalName, autoUpgrade, false, false, false, null, installProgress, packageInstalled);
        }

        public Task UpgradeExistingPackage(string canonicalName, bool? autoUpgrade = null,
            Action<string, int, int> installProgress = null,
            Action<string> packageInstalled = null) {
            return Install(canonicalName, autoUpgrade, false, true, false, null, installProgress, packageInstalled);
        }

        public Task UpdateExistingPackage(string canonicalName, bool? autoUpgrade = null,
            Action<string, int, int> installProgress = null,
            Action<string> packageInstalled = null) {
            return Install(canonicalName, autoUpgrade, true, false, false, null, installProgress, packageInstalled);
        }

        public Task<IEnumerable<Package>> EnsurePackagesAndDependenciesAreLocal(string canonicalName, bool? autoUpgrade = null) {
            return Install(canonicalName, autoUpgrade, false, false, true, true);
        }

        public Task<IEnumerable<Package>> WhatWouldBeInstalled(string canonicalName, bool? autoUpgrade = null) {
            return Install(canonicalName, autoUpgrade, false, false, false, false);
        }

        public Task<int> RemovePackages(IEnumerable<string> canonicalNames, bool forceRemoval, Action<string, int> packageRemovalProgress = null, Action<string> packageRemoveCompleted = null) {
            var packagesToRemove = canonicalNames.ToList();
            
            return Task.Factory.StartNew(() => {
                int[] total = {0};
                int packageCount;

                // this will keep looping around as long as the last pass removed a package
                // it was way easier to do this than to figure out what the order of removal is. :P
                while ((packageCount = packagesToRemove.Count) > 0) {
                    var packageFailures = new List<Exception>();

                    foreach (var cn in packagesToRemove.ToArray()) {
                        var canonicalName = cn;
                        try {
                            // remove the package and wait for completion.
                            RemovePackage(canonicalName, forceRemoval, packageRemovalProgress, packageRemoveCompleted).Wait();

                            packagesToRemove.Remove(canonicalName);
                            total[0] = total[0] + 1;
                        } catch( Exception exception) {
                            packageFailures.Add(exception);
                        }
                    }

                    if (packagesToRemove.Any() && packageCount == packagesToRemove.Count) {
                        // it didn't remove any on that pass. we're boned.
                        throw new AggregateException(packageFailures);
                    }
                }
                return total[0];
            }, TaskCreationOptions.AttachedToParent);
        }

        public Task RemovePackage(string canonicalName, bool forceRemoval, Action<string, int> packageRemovalProgress = null, Action<string> packageRemoveCompleted = null) {
            var failed = ValidateCanonicalName<int>(canonicalName);
            if (failed != null) {
                return failed;
            }

            var handler = new RemoteCallResponse {
                RemovingPackageProgress = packageRemovalProgress,
                RemovedPackage = packageRemoveCompleted,
            };

            return PackageManager.Instance.RemovePackage(canonicalName, forceRemoval, handler).ContinueWith(
                antecedent => {
                    if (handler.EngineRestarting) {
                        RemovePackage(canonicalName, forceRemoval).Wait();
                        return;
                    }
                    handler.ThrowWhenFaulted(antecedent);
                }, TaskContinuationOptions.AttachedToParent);
        }

        private Task<LoggingSettings> SetLogging(bool? messages = null, bool? warnings = null, bool? errors = null) {
            LoggingSettings result = null;

            var handler = new RemoteCallResponse {
                LoggingSettings = (m, w, e) => {
                    result = new LoggingSettings {Messages = m, Warnings = w, Errors = e};
                }
            };

            return PackageManager.Instance.SetLogging(messages, warnings, errors, handler).ContinueWith(antecedent => {
                if (handler.EngineRestarting) {
                    return SetLogging(messages, warnings, errors).Result;
                }
                handler.ThrowWhenFaulted(antecedent);
                return result;
            }, TaskContinuationOptions.AttachedToParent);
        }

        public Task EnableMessageLogging() {
            Logger.Messages = true;
            return SetLogging(messages: true);
        }

        public Task DisableMessageLogging() {
            Logger.Messages = false;
            return SetLogging(messages: false);
        }

        public Task<bool> IsMessageLogging {
            get {
                return SetLogging().ContinueWith(
                    antecedent => {
                        antecedent.RethrowWhenFaulted();
                        return antecedent.Result.Messages;
                    }, TaskContinuationOptions.AttachedToParent);
            }
        }

        public Task EnableWarningLogging() {
            Logger.Warnings = true;
            return SetLogging(warnings: true);
        }

        public Task DisableWarningLogging() {
            Logger.Warnings = false;
            return SetLogging(messages: false);
        }

        public Task<bool> IsWarningLogging {
            get {
                return SetLogging().ContinueWith(
                    antecedent => {
                        antecedent.RethrowWhenFaulted();
                        return antecedent.Result.Warnings;
                    }, TaskContinuationOptions.AttachedToParent);
            }
        }

        public Task EnableErrorLogging() {
            Logger.Errors = true;
            return SetLogging(errors: true);
        }

        public Task DisableErrorLogging() {
            Logger.Errors = false;
            return SetLogging(errors: false);
        }

        public Task<bool> IsErrorLogging {
            get {
                return SetLogging().ContinueWith(
                    antecedent => {
                        antecedent.RethrowWhenFaulted();
                        return antecedent.Result.Errors;
                    }, TaskContinuationOptions.AttachedToParent);
            }
        }

        public Task<string> RemoveSystemFeed(string feedLocation) {
            var handler = new RemoteCallResponse();

            return PackageManager.Instance.RemoveFeed(feedLocation, false, handler).ContinueWith(antecedent => {
                if (handler.EngineRestarting) {
                    return RemoveSystemFeed(feedLocation).Result;
                }
                handler.ThrowWhenFaulted(antecedent);
                return feedLocation;
            }, TaskContinuationOptions.AttachedToParent);
        }

        public Task<string> RemoveSessionFeed(string feedLocation) {
            var handler = new RemoteCallResponse();

            return PackageManager.Instance.RemoveFeed(feedLocation, true, handler).ContinueWith(antecedent => {
                if (handler.EngineRestarting) {
                    return RemoveSessionFeed(feedLocation).Result;
                    
                }
                handler.ThrowWhenFaulted(antecedent);
                return feedLocation;
            }, TaskContinuationOptions.AttachedToParent);
        }

        public Task<string> AddSystemFeed(string feedLocation) {
            var handler = new RemoteCallResponse {
                FeedAdded = location => {
                    // do something when a feed is added? Do we really care?
                }
            };

            return PackageManager.Instance.AddFeed(feedLocation, false, handler).ContinueWith(antecedent => {
                if (handler.EngineRestarting) {
                    return AddSystemFeed(feedLocation).Result;
                }

                handler.ThrowWhenFaulted(antecedent);
                return feedLocation;
            }, TaskContinuationOptions.AttachedToParent);
        }

        public Task<string> AddSessionFeed(string feedLocation) {
            var handler = new RemoteCallResponse {
                FeedAdded = location => {
                    // do something when a feed is added? Do we really care?
                }
            };

            return PackageManager.Instance.AddFeed(feedLocation, true, handler).ContinueWith(antecedent => {
                if (handler.EngineRestarting) {
                    return AddSessionFeed(feedLocation).Result;
                    
                }
                handler.ThrowWhenFaulted(antecedent);
                return feedLocation;
            }, TaskContinuationOptions.AttachedToParent);
        }

        public Task SuppressFeed(string feedLocation) {
            var handler = new RemoteCallResponse();

            return PackageManager.Instance.SuppressFeed(feedLocation, handler).ContinueWith(antecedent => {
                if (handler.EngineRestarting) {
                    SuppressFeed(feedLocation);
                    return;
                }
                handler.ThrowWhenFaulted(antecedent);
            }, TaskContinuationOptions.AttachedToParent);
        }

        public Task<IEnumerable<Feed>> Feeds {
            get {
                var result = new List<Feed>();
                var handler = new RemoteCallResponse {
                    FeedDetails = (location, lastScanned, isSession, isSuppressed, isValidated) => result.Add(new Feed {Location = location, LastScanned = lastScanned, IsSession = isSession, IsSuppressed = isSuppressed})
                };

                return PackageManager.Instance.ListFeeds(messages: handler).ContinueWith(antecedent => {
                    if (handler.EngineRestarting) {
                        // if we got a restarting message in the middle of this, try the request again from the beginning.
                        return Feeds.Result;
                    }

                    handler.ThrowWhenFaulted(antecedent);

                    return (IEnumerable<Feed>)result;
                }, TaskContinuationOptions.AttachedToParent);
            }
        }

        public Task SetFeedStale(string feedLocation) {
            var handler = new RemoteCallResponse();

            return PackageManager.Instance.SetFeedStale(feedLocation, handler).ContinueWith(antecedent => {
                if (handler.EngineRestarting) {
                    SetFeedStale(feedLocation).Wait();
                    return;
                }

                handler.ThrowWhenFaulted(antecedent);
            }, TaskContinuationOptions.AttachedToParent);
        }

        public Task SetAllFeedsStale() {
            return Feeds.ContinueWith(antecedent => {
                antecedent.RethrowWhenFaulted();

                foreach (var feed in antecedent.Result) {
                    SetFeedStale(feed.Location).RethrowWhenFaulted();
                }
            }, TaskContinuationOptions.AttachedToParent);
        }

        public Task<Package> RefreshPackageDetails(string canonicalName) {
            var failed = ValidateCanonicalName<Package>(canonicalName);
            if (failed != null) {
                return failed;
            }

            var handler = new RemoteCallResponse();

            return PackageManager.Instance.GetPackageDetails(canonicalName, handler).ContinueWith(antecedent => {
                if (handler.EngineRestarting) {
                    return RefreshPackageDetails(canonicalName).Result;
                }

                handler.ThrowWhenFaulted(antecedent);

                return Package.GetPackage(canonicalName);
            }, TaskContinuationOptions.AttachedToParent);
        }

        public Task<Package> GetPackage(string canonicalName) {
            var failed = ValidateCanonicalName<Package>(canonicalName);
            if (failed != null) {
                return failed;
            }

            var pkg = Package.GetPackage(canonicalName);
            if (pkg.IsPackageInfoStale) {
                // no data retrieved yet at all.
                var handler = new RemoteCallResponse();

                PackageManager.Instance.FindPackages(canonicalName, messages: handler).ContinueWith(antecedent => {
                    if (handler.EngineRestarting) {
                        return GetPackage(canonicalName).Result;
                    }
                    handler.ThrowWhenFaulted(antecedent);
                    return Package.GetPackage(canonicalName);
                }, TaskContinuationOptions.AttachedToParent);
            }
            return pkg.AsResultTask();
        }

        public Task<Package> GetPackageDetails(string canonicalName) {
            var failed = ValidateCanonicalName<Package>(canonicalName);
            if (failed != null) {
                return failed;
            }

            return GetPackage(canonicalName).ContinueWith(
                antecedent => {
                    antecedent.RethrowWhenFaulted();
                    return GetPackageDetails(antecedent.Result).Result;
                }, TaskContinuationOptions.AttachedToParent);
        }

        public Task<Package> GetPackageDetails(Package package) {
            if (package.IsPackageDetailsStale) {
                return GetPackage(package.CanonicalName).ContinueWith(antecedent => {
                    antecedent.RethrowWhenFaulted();
                    return RefreshPackageDetails(package.CanonicalName).Result;
                }, TaskContinuationOptions.AttachedToParent);
            }

            return package.Roles.IsNullOrEmpty() ? RefreshPackageDetails(package.CanonicalName) : package.AsResultTask();
        }

        public Task<bool> GetTelemetry() {
            var telemetryResult = false;

            var handler = new RemoteCallResponse {
                CurrentTelemetryOption = result => {
                    telemetryResult = result;
                }
            };

            return PackageManager.Instance.GetTelemetry(handler).ContinueWith(antecedent => {
                if (handler.EngineRestarting) {
                    return GetTelemetry().Result;
                }

                // take care of error conditions...
                handler.ThrowWhenFaulted(antecedent);

                return telemetryResult;
            }, TaskContinuationOptions.AttachedToParent);
        }

        public Task SetTelemetry(bool optInToTelemetry) {
            var handler = new RemoteCallResponse();

            return PackageManager.Instance.SetTelemetry(optInToTelemetry, handler).ContinueWith(antecedent => {
                if (handler.EngineRestarting) {
                    SetTelemetry(optInToTelemetry).Wait();
                    return;
                }

                // take care of error conditions...
                handler.ThrowWhenFaulted(antecedent);
            }, TaskContinuationOptions.AttachedToParent);
        }

        public Task CreateSymlink(string existingLocation, string newLink) {
            var handler = new RemoteCallResponse();

            return PackageManager.Instance.CreateSymlink(existingLocation, newLink, LinkType.Symlink, handler).ContinueWith(
                antecedent => {
                    if (handler.EngineRestarting) {
                        CreateSymlink(existingLocation, newLink).Wait();
                        return;
                    }

                    // take care of error conditions...
                    handler.ThrowWhenFaulted(antecedent);
                }, TaskContinuationOptions.AttachedToParent);
        }

        public Task CreateHardlink(string existingLocation, string newLink) {
            var handler = new RemoteCallResponse();

            return PackageManager.Instance.CreateSymlink(existingLocation, newLink, LinkType.Hardlink, handler).ContinueWith(
                antecedent => {
                    if (handler.EngineRestarting) {
                        CreateHardlink(existingLocation, newLink).Wait();
                        return;
                    }

                    // take care of error conditions...
                    handler.ThrowWhenFaulted(antecedent);
                }, TaskContinuationOptions.AttachedToParent);
        }

        public Task CreateShortcut(string existingLocation, string newLink) {
            var handler = new RemoteCallResponse();

            return PackageManager.Instance.CreateSymlink(existingLocation, newLink, LinkType.Shortcut, handler).ContinueWith(
                antecedent => {
                    if (handler.EngineRestarting) {
                        CreateShortcut(existingLocation, newLink).Wait();
                        return;
                    }

                    // take care of error conditions...
                    handler.ThrowWhenFaulted(antecedent);
                }, TaskContinuationOptions.AttachedToParent);
        }

        public Task RemoveFromPolicy(string policyName, string account) {
            var handler = new RemoteCallResponse();

            return PackageManager.Instance.RemoveFromPolicy(policyName, account, handler).ContinueWith(antecedent => {
                if (handler.EngineRestarting) {
                    RemoveFromPolicy(policyName, account);
                    return;
                }

                // take care of error conditions...
                handler.ThrowWhenFaulted(antecedent);
            }, TaskContinuationOptions.AttachedToParent);
        }

        public Task AddToPolicy(string policyName, string account) {
            var handler = new RemoteCallResponse();

            return PackageManager.Instance.AddToPolicy(policyName, account, handler).ContinueWith(antecedent => {
                if (handler.EngineRestarting) {
                    AddToPolicy(policyName, account).Wait();
                    return;
                }

                // take care of error conditions...
                handler.ThrowWhenFaulted(antecedent);
            }, TaskContinuationOptions.AttachedToParent);
        }

        public Task<Policy> GetPolicy(string policyName) {
            Policy result = null;
            var handler = new RemoteCallResponse {
                PolicyInformation = (name, description, members) => {
                    result = new Policy {Name = name, Description = description, Members = members};
                }
            };

            return PackageManager.Instance.GetPolicy(policyName, handler).ContinueWith(antecedent => {
                if (handler.EngineRestarting) {
                    return GetPolicy(policyName).Result;
                }

                // take care of error conditions...
                handler.ThrowWhenFaulted(antecedent);

                if( result == null ) {
                    throw new CoAppException("Policy '{0}' does not exist".format(policyName));
                }

                return result;
            }, TaskContinuationOptions.AttachedToParent);
        }

        public Task<IEnumerable<Policy>> Policies {
            get {
                var result = new List<Policy>();

                var handler = new RemoteCallResponse {
                    PolicyInformation = (name, description, members) => result.Add(new Policy {Name = name, Description = description, Members = members})
                };
                    
                return PackageManager.Instance.GetPolicy("*", handler).ContinueWith(antecedent => {
                    if (handler.EngineRestarting) {
                        return Policies.Result;
                    }

                    // take care of error conditions...
                    handler.ThrowWhenFaulted(antecedent);

                    return (IEnumerable<Policy>)result;
                }, TaskContinuationOptions.AttachedToParent);
            }
        }

        /// <summary>
        /// </summary>
        /// <param name="taskName"> the name of the task. If a task with this name already exists, it will be overwritten. </param>
        /// <param name="executable"> </param>
        /// <param name="commandline"> </param>
        /// <param name="hour"> </param>
        /// <param name="minutes"> </param>
        /// <param name="dayOfWeek"> </param>
        /// <param name="intervalInMinutes"> how often the scheduled task should consider running (on Windows XP/2003, it's not possible to run as soon as possible after a task was missed. </param>
        /// <returns> </returns>
        public Task AddScheduledTask(string taskName, string executable, string commandline, int hour, int minutes, DayOfWeek? dayOfWeek, int intervalInMinutes) {
            var handler = new RemoteCallResponse();

            return PackageManager.Instance.AddScheduledTask(taskName, executable, commandline, hour, minutes, dayOfWeek, intervalInMinutes, handler).ContinueWith(antecedent => {
                if (handler.EngineRestarting) {
                    AddScheduledTask(taskName, executable, commandline, hour, minutes, dayOfWeek, intervalInMinutes);
                    return;
                }
                antecedent.RethrowWhenFaulted();
            }, TaskContinuationOptions.AttachedToParent);
        }

        public Task RemoveScheduledTask(string taskName) {
            var handler = new RemoteCallResponse();

            return PackageManager.Instance.RemoveScheduledTask(taskName, handler).ContinueWith(antecedent => {
                if (handler.EngineRestarting) {
                    RemoveScheduledTask(taskName);
                    return;
                }
                antecedent.RethrowWhenFaulted();
            }, TaskContinuationOptions.AttachedToParent);
        }

        public Task<ScheduledTask> GetScheduledTask(string taskName) {
            ScheduledTask result = null;

            var handler = new RemoteCallResponse {
                ScheduledTaskInfo = (name, executable, commandline, hour, minutes, dayOfWeek, intervalInMinutes) => {
                    result = new ScheduledTask {
                        Name = name,
                        Executable = executable,
                        CommandLine = commandline,
                        Hour = hour,
                        Minutes = minutes,
                        DayOfWeek = dayOfWeek,
                        IntervalInMinutes = intervalInMinutes
                    };
                }
            };

            return PackageManager.Instance.GetScheduledTask(taskName, handler).ContinueWith(antecedent => {
                if (handler.EngineRestarting) {
                    return GetScheduledTask(taskName).Result;
                }
                antecedent.RethrowWhenFaulted();
                return result;
            }, TaskContinuationOptions.AttachedToParent);
        }

        public Task<IEnumerable<ScheduledTask>> ScheduledTasks {
            get {
                var result = new List<ScheduledTask>();

                var handler = new RemoteCallResponse {
                    ScheduledTaskInfo = (name, executable, commandline, hour, minutes, dayOfWeek, intervalInMinutes) => result.Add(new ScheduledTask {
                        Name = name,
                        Executable = executable,
                        CommandLine = commandline,
                        Hour = hour,
                        Minutes = minutes,
                        DayOfWeek = dayOfWeek,
                        IntervalInMinutes = intervalInMinutes
                    })
                };

                return PackageManager.Instance.GetScheduledTask("*", handler).ContinueWith(antecedent => {
                    if (handler.EngineRestarting) {
                        return ScheduledTasks.Result;
                    }
                    antecedent.RethrowWhenFaulted();
                    return result;
                }, TaskContinuationOptions.AttachedToParent);
            }
        }

        public Task RecognizeFile(string filename) {
            return null;
        }

        public Task<IEnumerable<Publisher>> TrustedPublishers {
            get {
                return null;
            }
        }

        public Task<Publisher> AddTrustedPublisher(string publisherName, string publicKeyToken) {
            return null;
        }

        public Task RemoveTrustedPublisher(string publisherName, string publicKeyToken) {
            return null;
        }
        // GS01: TrustedPublishers Coming Soon.
    }
}