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
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using Common;
    using Common.Exceptions;
    using Common.Model;
    using Feeds;
    using PackageFormatHandlers;
    using Toolkit.Collections;
    using Toolkit.Configuration;
    using Toolkit.Exceptions;
    using Toolkit.Extensions;
    using Toolkit.Logging;
    using Toolkit.Shell;
    using Toolkit.Tasks;
    using Toolkit.Win32;
    
    public class Package : NotifiesPackageManager, IPackage {
        private static readonly XDictionary<CanonicalName, Package> Packages = new XDictionary<CanonicalName, Package>();
       
        private static readonly IDictionary<string, string> DefaultMacros = new XDictionary<string, string>{
                {"apps", PackageManagerSettings.CoAppRootDirectory},
                {"cache", Path.Combine(PackageManagerSettings.CoAppRootDirectory, ".cache")},
                {"assemblies", Path.Combine(PackageManagerSettings.CoAppRootDirectory, "ReferenceAssemblies")},
                {"referenceassemblies", Path.Combine(PackageManagerSettings.CoAppRootDirectory, "ReferenceAssemblies")},
                {"x86", Path.Combine(PackageManagerSettings.CoAppRootDirectory, "x86")},
                {"x64", Path.Combine(PackageManagerSettings.CoAppRootDirectory, "x64")},
                {"bin", Path.Combine(PackageManagerSettings.CoAppRootDirectory, "bin")},
                {"powershell", Path.Combine(PackageManagerSettings.CoAppRootDirectory, "powershell")},
                {"lib", Path.Combine(PackageManagerSettings.CoAppRootDirectory, "lib")},
                {"include", Path.Combine(PackageManagerSettings.CoAppRootDirectory, "include")},
                {"etc", Path.Combine(PackageManagerSettings.CoAppRootDirectory, "etc")},
                {"allprograms", KnownFolders.GetFolderPath(KnownFolder.CommonPrograms)},
            };

        public static implicit operator CanonicalName(Package package) {
            return package.CanonicalName;
        }

        public static implicit operator Package(CanonicalName name) {
            return GetPackage(name);
        }

        [Persistable]
        public CanonicalName CanonicalName { get; private set; }
        [Persistable]
        public string DisplayName { get; set; }
        [Persistable]
        public BindingPolicy BindingPolicy { get; set; }
        [Persistable]
        public IEnumerable<Role> Roles { get { return PackageRoles; } }

        [Persistable]
        public IEnumerable<Uri> RemoteLocations { get { return RemotePackageLocations; } }
        [Persistable]
        public PackageDetails PackageDetails { get; set; }
        [Persistable]
        public IEnumerable<Uri> Feeds { get { return FeedLocations; } }

        [Persistable(SerializeAsType = typeof(CanonicalName))]
        public IPackage InstalledNewest { get { return PackageRequestData.InstalledNewest.Value; } }

        [Persistable(SerializeAsType= typeof(CanonicalName))]
        public IPackage InstalledNewestUpdate { get { return PackageRequestData.InstalledNewestUpdate.Value; } }
        [Persistable(SerializeAsType= typeof(CanonicalName))]
        public IPackage InstalledNewestUpgrade { get { return PackageRequestData.InstalledNewestUpgrade.Value; } }

        [Persistable(SerializeAsType= typeof(CanonicalName))]
        public IPackage LatestInstalledThatUpdatesToThis { get { return PackageRequestData.LatestInstalledThatUpdatesToThis.Value; } }
        [Persistable(SerializeAsType= typeof(CanonicalName))]
        public IPackage LatestInstalledThatUpgradesToThis { get { return PackageRequestData.LatestInstalledThatUpgradesToThis.Value; } }

        [Persistable(SerializeAsType= typeof(CanonicalName))]
        public IPackage AvailableNewest { get { return PackageRequestData.AvailableNewest.Value; } }
        [Persistable(SerializeAsType= typeof(CanonicalName))]
        public IPackage AvailableNewestUpdate { get { return PackageRequestData.AvailableNewestUpdate.Value; } }
        [Persistable(SerializeAsType= typeof(CanonicalName))]
        public IPackage AvailableNewestUpgrade { get { return PackageRequestData.AvailableNewestUpgrade.Value; } }

        [Persistable(SerializeAsType= typeof(IEnumerable<CanonicalName>))]
        public IEnumerable<IPackage> InstalledPackages { get { return PackageRequestData.InstalledPackages.Value; } }

        [Persistable(SerializeAsType= typeof(IEnumerable<CanonicalName>))]
        public IEnumerable<IPackage> Dependencies { get { return PackageDependencies; } }

        [Persistable(SerializeAsType= typeof(IEnumerable<CanonicalName>))]
        public IEnumerable<IPackage> TrimablePackages { get { return PackageRequestData.TrimablePackages.Value; } }

        [Persistable(SerializeAsType= typeof(CanonicalName))]
        public IPackage SatisfiedBy { get { return PackageRequestData.SatisfiedBy.Value; } }

        [Persistable(SerializeAsType= typeof(IEnumerable<CanonicalName>))]
        public IEnumerable<IPackage> NewerPackages { get { return PackageRequestData.NewerPackages.Value; } }

        [Persistable(SerializeAsType= typeof(IEnumerable<CanonicalName>))]
        public IEnumerable<IPackage> UpdatePackages { get { return PackageRequestData.UpdatePackages.Value; } }

        [Persistable(SerializeAsType= typeof(IEnumerable<CanonicalName>))]
        public IEnumerable<IPackage> UpgradePackages { get { return PackageRequestData.UpgradePackages.Value; } }


        [NotPersistable]
        public bool IsDependency { get {
            return PackageSessionData.IsDependency;
        } }

        /// <summary>
        ///   Indicates that the client specifically requested the package
        /// </summary>
        [Persistable]
        public bool IsWanted {
            get {
                return PackageSessionData.PackageSettings["#Wanted"].BoolValue;
            }
            set {
                PackageSessionData.PackageSettings["#Wanted"].BoolValue = value;
            }
        }

        [Persistable]
        public bool IsBlocked {
            get {
                return PackageState == PackageState.Blocked;
            }
        }

        [Persistable]
        public bool IsTrimable {
            get {
                return (!(IsWanted || IsDependency)) && PackageState > PackageState.DoNotChange;
            }
        }

        [NotPersistable]
        public string Name { get { return CanonicalName.Name; } }
        [NotPersistable]
        public FlavorString Flavor { get { return CanonicalName.Flavor; } }
        [NotPersistable]
        public FourPartVersion Version { get { return CanonicalName.Version; } }
        [NotPersistable]
        public PackageType PackageType { get { return CanonicalName.PackageType; } }
        [NotPersistable]
        public Architecture Architecture { get { return CanonicalName.Architecture; } }
        [NotPersistable]
        public string PublicKeyToken { get { return CanonicalName.PublicKeyToken; } }

        internal IPackageFormatHandler PackageHandler;
        internal string Vendor { get; set; }

        private bool? _isInstalled;
        private Composition _compositionData;

        internal readonly XList<Uri> RemotePackageLocations = new XList<Uri>();
        internal readonly XList<Uri> FeedLocations = new XList<Uri>();

        internal readonly XList<string> LocalLocations = new XList<string>();
        internal readonly XList<Role> PackageRoles = new XList<Role>();
        internal readonly XList<Feature> Features = new XList<Feature>();
        internal readonly XList<Feature> RequiredFeatures = new XList<Feature>();

        internal readonly ObservableCollection<Package> PackageDependencies = new ObservableCollection<Package>();
        
        private Package(CanonicalName canonicalName) {
            Packages.Changed += x => Changed();
            PackageDependencies.CollectionChanged += (x, y) => Changed();
            CanonicalName = canonicalName;
        }

        internal static Package GetPackageFromFilename(string filename) {
            filename = filename.CanonicalizePathIfLocalAndExists();

            if (!File.Exists(filename)) {
                Event<GetResponseInterface>.RaiseFirst().FileNotFound(filename);
                return null;
            }

            Package pkg;

            lock (Packages) {
                pkg = (Packages.Values.FirstOrDefault(package =>package.HasLocalLocation && package.LocalLocations.Contains(filename)));
            }

            // if we didn't find it by looking at the packages in memory, and seeing if it matches a known path.
            // try package handlers to see if we can find one that will return a valid package for it.

            pkg = pkg ?? CoAppMSI.GetCoAppPackageFileInformation(filename);

            // pkg = pkg ?? NugetPackageHandler.GetCoAppPackageFileInformation(filename);
            // pkg = pkg ?? PythonPackageHandler.GetCoAppPackageFileInformation(filename); // etc.

            return pkg;
        }

        internal static Package GetPackage(CanonicalName canonicalName) {
            if (!canonicalName.IsCanonical) {
                throw new CoAppException("GetPackage requries that CanonicalName must not be a partial name.");
            }

            lock (Packages) {
                return Packages.GetOrAdd(canonicalName, () => new Package(canonicalName));
            }
        }

        internal static FourPartVersion GetCurrentPackageVersion(CanonicalName canonicalName) {
            var activePkg = PackageManagerImpl.Instance.InstalledPackages.Where(each => canonicalName.DiffersOnlyByVersion(each.CanonicalName))
                .OrderBy(each => each, new Toolkit.Extensions.Comparer<Package>((packageA, packageB) => GeneralPackageSettings.Instance.WhoWins(packageA, packageB)))
                .FirstOrDefault();
            return activePkg == null ? 0 : activePkg.Version;
        }

        internal string PackageDirectory {
            get {
                return Path.Combine(BaseInstallDirectory, Vendor.MakeSafeFileName(), CanonicalName.PackageName);
            }
        }

        internal string BaseInstallDirectory {
            get {
                return PackageManagerSettings.CoAppInstalledDirectory[CanonicalName.Architecture];
            }
        }

        internal PackageSessionData PackageSessionData {
            get {
                return SessionData.Current.PackageSessionData.GetOrAdd(CanonicalName, () => new PackageSessionData(this));
            }
        }

        internal PackageRequestData PackageRequestData {
            get {
                var cache = Event<GetRequestPackageDataCache>.RaiseFirst();
                lock (cache) {
                    return cache[CanonicalName] ?? (cache[CanonicalName] = new PackageRequestData(this));
                }
            }
        }

        [Persistable]
        public bool IsInstalled {
            get {
                if (!_isInstalled.HasValue) {
                    lock (this) {
                        try {
                            Changed();
                            if (PackageHandler != null) {
                                return true == (_isInstalled = PackageHandler.IsInstalled(CanonicalName));
                            }
                        } catch {
                        }
                        _isInstalled = false;
                    }
                }
                return _isInstalled.Value;
            }
            set {
                if (_isInstalled != value) {
                    if (value) {
                        InstalledPackageFeed.Instance.PackageInstalled(this);
                    } else {
                        InstalledPackageFeed.Instance.PackageRemoved(this);
                    }
                }
                _isInstalled = value;
            }
        }
        
        [Persistable]
        public bool IsActive {
            get {
                return PackageRequestData.ActivePackage.Value == this;
            }
        }
       
        internal void Install() {
            try {
                Engine.EnsureCanonicalFoldersArePresent();

                PackageHandler.Install(this);
                if (PackageSessionData.IsWanted) {
                    IsWanted = true;
                }
                IsInstalled = true;
                Logger.Message("MSI Install of package [{0}] SUCCEEDED.", CanonicalName);
                DoPackageComposition();
                Logger.Message("Package Composition [{0}] SUCCEEDED.", CanonicalName);

            } catch (Exception e) {
                Logger.Error("Package Install Failure [{0}] => [{1}].\r\n{2}", CanonicalName, e.Message, e.StackTrace);
                Remove();

                IsInstalled = false;
                throw new PackageInstallFailedException(this);
            }
        }

        internal void Remove() {
            try {
                Logger.Message("Attempting to undo package composition");
                UndoPackageComposition();
            } catch {
                // if something goes wrong in removing package composition, keep uninstalling.
            }

            try {
                Logger.Message("Attempting to remove MSI");
                PackageHandler.Remove(this);

                // clean up the package directory if it hangs around.
                PackageDirectory.TryHardToDelete();

                IsInstalled = false;

                PackageManagerSettings.PerPackageSettings.DeleteSubkey(CanonicalName);
            } catch (Exception e) {
                Logger.Error(e);
                Event<GetResponseInterface>.RaiseFirst().FailedPackageRemoval(CanonicalName, "GS01: During package removal, things went horribly wrong.... ");
                throw new OperationCompletedBeforeResultException();
            } 
        }

        /// <summary>
        ///   V1 of the Variable Resolver.
        /// </summary>
        /// <param name="text"> </param>
        /// <returns> </returns>
        internal string ResolveVariables(string text) {
            if (string.IsNullOrEmpty(text)) {
                return string.Empty;
            }

            return text.FormatWithMacros(macro => {
                if (DefaultMacros.ContainsKey(macro)) {
                    return DefaultMacros[macro];
                }

                switch (macro.ToLower()) {
                    case "packagedir":
                    case "packagedirectory":
                    case "packagefolder":
                        return PackageDirectory;

                    case "targetdirectory":
                        return BaseInstallDirectory;

                    case "publishedpackagedir":
                    case "publishedpackagedirectory":
                    case "publishedpackagefolder":
                        return @"${apps}\${simplename}";

                    case "productname":
                    case "packagename":
                        return CanonicalName.PackageName;

                    case "simplename":
                        return CanonicalName.SimpleName;

                    case "version":
                        return CanonicalName.Version.ToString();

                    case "arch":
                    case "architecture":
                        return CanonicalName.Architecture;

                    case "canonicalname":
                        return CanonicalName;
                }
                return null;
            });
        }

        internal void MarkDependenciesAsDepenency() {
            foreach (var dpkg in PackageDependencies.Where(each => !each.IsWanted)) {
                var dependentPackage = (dpkg.SatisfiedBy as Package);
                if (dependentPackage != null) {
                    // find each dependency that is the policy-preferred version, and mark it as currently requested.
                    dependentPackage.MarkDependenciesAsDepenency();
                }
            }
            // if this isn't already set, do it.
            if (!IsWanted) {
                PackageSessionData.IsDependency = true;
            }
        }

        internal IEnumerable<CompositionRule> ImplicitRules {
            get {
                foreach (var r in PackageRoles) {
                    var role = r;
                    switch (role.PackageRole) {
                        case PackageRole.Application:
                            yield return new CompositionRule {
                                Action = CompositionAction.SymlinkFolder,
                                Destination = "${publishedpackagedir}",
                                Source = "${packagedir}",
                                Category = null,
                            };
                            break;
                        case PackageRole.DeveloperLibrary:
                            foreach (var devLib in DeveloperLibraries.Where(each => each.Name == role.Name)) {
                                // expose the reference assemblies 
                                if (!devLib.ReferenceAssemblyFiles.IsNullOrEmpty()) {
                                    foreach (var asmFile in devLib.ReferenceAssemblyFiles) {
                                        yield return new CompositionRule {
                                            Action = CompositionAction.SymlinkFile,
                                            Destination = "${referenceassemblies}\\${arch}\\" + Path.GetFileName(asmFile),
                                            Source = "${packagedir}\\" + asmFile,
                                            Category = null
                                        };

                                        yield return new CompositionRule {
                                            Action = CompositionAction.SymlinkFile,
                                            Destination = "${referenceassemblies}\\${arch}\\${simplename}-${version}\\" + Path.GetFileName(asmFile),
                                            Source = "${packagedir}\\" + asmFile,
                                            Category = null
                                        };
                                    }
                                }

                                if (!devLib.LibraryFiles.IsNullOrEmpty()) {
                                    foreach (var libFile in devLib.LibraryFiles) {
                                        var libFileName = Path.GetFileName(libFile);

                                        var libFileWithoutExtension = Path.GetFileNameWithoutExtension(libFileName);
                                        var libFileExtension = Path.GetExtension(libFileName);

                                        yield return new CompositionRule {
                                            Action = CompositionAction.SymlinkFile,
                                            Destination = "${lib}\\${arch}\\" + libFileName,
                                            Source = "${packagedir}\\" + libFile,
                                            Category = null
                                        };

                                        yield return new CompositionRule {
                                            Action = CompositionAction.SymlinkFile,
                                            Destination = "${lib}\\${arch}\\" + libFileWithoutExtension + "-${version}" + libFileExtension,
                                            Source = "${packagedir}\\" + libFile,
                                            Category = null
                                        };
                                    }
                                }

                                if (!devLib.HeaderFolders.IsNullOrEmpty()) {
                                    foreach (var headerFolder in devLib.HeaderFolders) {
                                        yield return new CompositionRule {
                                            Action = CompositionAction.SymlinkFolder,
                                            Destination = "${include}\\" + devLib.Name,
                                            Source = "${packagedir}\\" + headerFolder,
                                            Category = null
                                        };

                                        yield return new CompositionRule {
                                            Action = CompositionAction.SymlinkFolder,
                                            Destination = "${include}\\" + devLib.Name + "-${version}",
                                            Source = "${packagedir}\\" + headerFolder,
                                            Category = null
                                        };
                                    }
                                }

                                if (!devLib.DocumentFolders.IsNullOrEmpty()) {
                                    foreach (var docFolder in devLib.DocumentFolders) {
                                        // not exposing document folders yet.
                                    }
                                }
                            }

                            break;
                        case PackageRole.Assembly:
                            break;
                        case PackageRole.SourceCode:
                            break;
                        case PackageRole.Driver:
                            break;
                        case PackageRole.Service:
                            break;
                        case PackageRole.WebApplication:
                            break;
                        case PackageRole.Faux:
                            foreach (var fauxApplication in FauxApplications) {
                                if (fauxApplication.Name == role.Name || (string.IsNullOrEmpty(fauxApplication.Name) && string.IsNullOrEmpty(role.Name))) {
                                    foreach (var dest in fauxApplication.Downloads.Keys) {
                                        yield return new CompositionRule {
                                            Action = CompositionAction.DownloadFile,
                                            Destination = "${packagedir}\\" + dest,
                                            Source = fauxApplication.Downloads[dest].AbsoluteUri,
                                        };
                                    }
                                    if( !string.IsNullOrEmpty(fauxApplication.InstallCommand) ) {
                                        yield return new CompositionRule {
                                            Action = CompositionAction.InstallCommand,
                                            Source = fauxApplication.InstallCommand,
                                            Destination = fauxApplication.InstallParameters
                                        };    
                                    }
                                    if (!string.IsNullOrEmpty(fauxApplication.RemoveCommand)) {
                                        yield return new CompositionRule {
                                            Action = CompositionAction.RemoveCommand,
                                            Source = fauxApplication.RemoveCommand,
                                            Destination = fauxApplication.RemoveParameters
                                        };
                                    }
                                }
                            }
                            break;
                    }
                }
            }
        }

        private string ResolveVariablesAndEnsurePathParentage(string parentPath, string variable) {
            parentPath = parentPath.GetFullPath();

            var path = ResolveVariables(variable);

            try {
                if (path.IsSimpleSubPath()) {
                    path = Path.Combine(parentPath, path);
                }

                path = path.GetFullPath();

                if (parentPath.IsSubPath(path)) {
                    return path;
                }
            } catch (Exception e) {
                Logger.Error(e);
            }

            Logger.Error("ERROR: path '{0}' must resolve to be a child of '{1}' (resolves to '{2}')", variable, parentPath, path);
            return null;
        }

        public bool RequiresTrustedPublisher {
            get {
                return FauxApplications.Any();
            }
        }

        private IEnumerable<CompositionRule> ResolvedRules { 
            get {
                var packagedir = ResolveVariables("${packagedir}\\");
                var appsdir = ResolveVariables("${apps}\\");

                foreach( var rule in ImplicitRules.Union(CompositionRules)) {
                    switch( rule.Action) {
                        case CompositionAction.FileCopy :
                        case CompositionAction.SymlinkFile:
                        case CompositionAction.FileRewrite:
                            yield return new CompositionRule {
                                Action = rule.Action,
                                Destination = ResolveVariablesAndEnsurePathParentage(appsdir, rule.Destination),
                                Source = ResolveVariablesAndEnsurePathParentage(packagedir, rule.Source)
                            };
                            break;
                        
                        case CompositionAction.Shortcut:
                            yield return new CompositionRule {
                                Action = rule.Action,
                                Destination = ResolveVariablesAndEnsurePathParentage(appsdir, rule.Destination).GetFullPath(),
                                Source = ResolveVariablesAndEnsurePathParentage(packagedir, rule.Source )
                            };
                            break;
                        case CompositionAction.SymlinkFolder:
                            yield return new CompositionRule {
                                Action = rule.Action,
                                Destination = ResolveVariablesAndEnsurePathParentage(appsdir, rule.Destination),
                                Source = ResolveVariablesAndEnsurePathParentage(packagedir, rule.Source + "\\")
                            };
                            break;

                        case CompositionAction.EnvironmentVariable:
                        case CompositionAction.Registry:
                            yield return new CompositionRule {
                                Action = rule.Action,
                                Destination = ResolveVariables(rule.Key),
                                Source = ResolveVariables(rule.Value)
                            };
                            break;

                        case CompositionAction.DownloadFile:
                            yield return new CompositionRule {
                                Action = rule.Action,
                                Destination = ResolveVariablesAndEnsurePathParentage(packagedir, rule.Destination),
                                Source = rule.Source
                            };
                            break;

                        case CompositionAction.InstallCommand:
                        case CompositionAction.RemoveCommand:
                            yield return new CompositionRule {
                                Action = rule.Action,
                                Destination = ResolveVariables(rule.Destination),
                                Source = ResolveVariables(rule.Source)
                            };
                            break;
                    }
                }
                
            }
        }

        private bool ExecuteCommand(string command, string parameters) {
            if( string.IsNullOrEmpty(command)) {
                return true;
            }

            var psi = new ProcessStartInfo {
                FileName = command.Contains("\\") ? command : EnvironmentUtility.FindInPath( command, PackageDirectory+";"+EnvironmentUtility.EnvironmentPath),
                Arguments = parameters,
                CreateNoWindow = false,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                WorkingDirectory = PackageDirectory
            };
            if(! psi.FileName.Contains("\\") ) {
                Logger.Error("Target execute command does not have a full path. '{0}'", psi.FileName);
            }

            try {
                var proc = Process.Start(psi);
                var stdOut = Task.Factory.StartNew(() => proc.StandardOutput.ReadToEnd());
                var stdErr = Task.Factory.StartNew(() => proc.StandardError.ReadToEnd());

                proc.WaitForExit();

                if (proc.ExitCode != 0) {
                    Logger.Error("Failed Execute Command StdOut: \r\n{0}", stdOut.Result);
                    Logger.Error("Failed Execute Command StdError: \r\n{0}", stdErr.Result);
                    return false;
                }
                return true;
            } catch(Exception e) {
                Logger.Error(e);
                
            }
            return false;
        }


        private bool ApplyRule( CompositionRule rule ) {
            switch (rule.Action) {
                case CompositionAction.DownloadFile:
                    if(!File.Exists(rule.Destination)) {
                        PackageSessionData.DownloadFile(rule.Source, rule.Destination);
                    }
                    return true;

                case CompositionAction.InstallCommand:
                    return ExecuteCommand(rule.Source, rule.Destination);

                case CompositionAction.RemoveCommand:
                    // we never 'apply' remove commands. Just remove em'
                    return true; 

                case CompositionAction.FileCopy:
                    // file copy operations may only manipulate files in the package directory.
                    if (string.IsNullOrEmpty(rule.Source)) {
                        Logger.Error("ERROR: Illegal file copy rule. Source must be in package directory [{0}] => [{1}]", rule.Source, rule.Destination);
                        return false;
                    }

                    if (string.IsNullOrEmpty(rule.Destination)) {
                        Logger.Error("ERROR: Illegal file copy rule. Destination must be in package directory [{0}] => [{1}]", rule.Source, rule.Destination);
                        return false;
                    }

                    if (!File.Exists(rule.Source)) {
                        Logger.Error("ERROR: Illegal file copy rule. Source file does not exist [{0}] => [{1}]", rule.Source, rule.Destination);
                        return false;
                    }
                    try {
                        var destParent = Path.GetDirectoryName(rule.Destination);
                        if (!string.IsNullOrEmpty(destParent)) {
                            if (!Directory.Exists(destParent)) {
                                Directory.CreateDirectory(destParent);
                            }
                            File.Copy(rule.Source, rule.Destination, true);
                        }
                    }
                    catch (Exception e) {
                        Logger.Error(e);
                    }
                    return true;

                case CompositionAction.FileRewrite:
                    // file copy operations may only manipulate files in the package directory.
                    if (string.IsNullOrEmpty(rule.Source)) {
                        Logger.Error("ERROR: Illegal file rewrite rule. Source must be in package directory [{0}] => [{1}]", rule.Source, rule.Destination);
                        return false;
                    }

                    if (string.IsNullOrEmpty(rule.Destination)) {
                        Logger.Error("ERROR: Illegal file rewrite rule. Destination must be in package directory [{0}] => [{1}]", rule.Source, rule.Destination);
                        return false;
                    }

                    if (!File.Exists(rule.Source)) {
                        Logger.Error("ERROR: Illegal file rewrite rule. Source file does not exist [{0}] => [{1}]", rule.Source, rule.Destination);
                        return false;
                    }
                    File.WriteAllText(rule.Destination, ResolveVariables(File.ReadAllText(rule.Source)));
                    return true;

                case CompositionAction.SymlinkFile:
                    if (string.IsNullOrEmpty(rule.Destination)) {
                        Logger.Error("ERROR: Illegal file symlink rule. Destination location '{0}' must be a subpath of apps dir", rule.Destination);
                        return false;
                    }

                    if (string.IsNullOrEmpty(rule.Source)) {
                        Logger.Error("ERROR: Illegal file symlink rule. Source file '{0}' must be a subpath of package directory", rule.Source);
                        return false;
                    }

                    if (!File.Exists(rule.Source)) {
                        Logger.Error("ERROR: Illegal folder symlink rule. Source file '{0}' does not exist.", rule.Source);
                        return false;
                    }

                    var parentDir = Path.GetDirectoryName(rule.Destination);
                    if (!string.IsNullOrEmpty(parentDir)) {
                        if (!Directory.Exists(parentDir)) {
                            Directory.CreateDirectory(parentDir);
                        }
                        try {
                            // Logger.Message("Creating file Symlink [{0}] => [{1}]", rule.Destination, rule.Source);
                            Symlink.MakeFileLink(rule.Destination, rule.Source);
                        }
                        catch (Exception) {
                            Logger.Error("Warning: File Symlink Link Failed. [{0}] => [{1}]", rule.Destination, rule.Source);
                        }
                    }
                    return true;

                case CompositionAction.SymlinkFolder:
                    if (string.IsNullOrEmpty(rule.Destination)) {
                        Logger.Error("ERROR: Illegal folder symlink rule. Destination location '{0}' must be a subpath of appsdir", rule.Destination);
                        return false;
                    }

                    if (string.IsNullOrEmpty(rule.Source)) {
                        Logger.Error("ERROR: Illegal folder symlink rule. Source folder '{0}' must be a subpath of package directory{1}", rule.Source);
                        return false;
                    }

                    if (!Directory.Exists(rule.Source)) {
                        Logger.Error("ERROR: Illegal folder symlink rule. Source folder '{0}' does not exist.", rule.Source);
                        return false;
                    }

                    try {
                        // Logger.Message("Creatign Directory Symlink [{0}] => [{1}]", rule.Destination, rule.Source);
                        Symlink.MakeDirectoryLink(rule.Destination, rule.Source);
                    }
                    catch (Exception) {
                        Logger.Error("Warning: Directory Symlink Link Failed. [{0}] => [{1}]", rule.Destination, rule.Source);
                        return false;
                    }
                    return true;

                case CompositionAction.Shortcut:
                    if (string.IsNullOrEmpty(rule.Source)) {
                        Logger.Error("ERROR: Illegal shortcut rule. Source file '{0}' must be a subpath of package directory", rule.Source);
                        return false;
                    }

                    if (!File.Exists(rule.Source)) {
                        Logger.Error("ERROR: Illegal shortcut rule. Source file '{0}' does not exist.", rule.Source);
                        return false;
                    }

                    var pDir = Path.GetDirectoryName(rule.Destination);
                    if (!string.IsNullOrEmpty(pDir)) {
                        if (!Directory.Exists(pDir)) {
                            Directory.CreateDirectory(pDir);
                        }
                        // Logger.Message("Creating Shortcut [{0}] => [{1}]", rule.Destination, rule.Source);
                        ShellLink.CreateShortcut(rule.Destination, rule.Source);
                    }

                    return true;

                case CompositionAction.EnvironmentVariable:
                    switch (rule.Key.ToLower()) {
                        case "path":
                        case "pathext":
                        case "psmodulepath":
                        case "comspec":
                        case "temp":
                        case "tmp":
                        case "username":
                        case "windir":
                        case "allusersprofile":
                        case "appdata":
                        case "commonprogramfiles":
                        case "commonprogramfiles(x86)":
                        case "commonprogramw6432":
                        case "computername":
                        case "current_cpu":
                        case "FrameworkVersion":
                        case "homedrive":
                        case "homepath":
                        case "logonserver":
                        case "number_of_processors":
                        case "os":
                        case "processor_architecture":
                        case "processor_identifier":
                        case "processor_level":
                        case "processor_revision":
                        case "programdata":
                        case "programfiles":
                        case "programfiles(x86)":
                        case "programw6432":
                        case "prompt":
                        case "public":
                        case "systemdrive":
                        case "systemroot":
                        case "userdomain":
                        case "userprofile":
                            Logger.Warning("Package may not set environment variable '{0}'", rule.Key);
                            return true;

                        default:
                            EnvironmentUtility.SetSystemEnvironmentVariable(rule.Key, rule.Value);
                            return true;
                    }
                    

                case CompositionAction.Registry:
                    if( CanonicalName.Architecture == Architecture.x64 && Environment.Is64BitOperatingSystem ) {
                        RegistryView.System["SOFTWARE"][rule.Key].StringValue = rule.Value;
                    } else {
                        RegistryView.System["SOFTWARE\\Wow6432Node"][rule.Key].StringValue = rule.Value;
                    }
                    return true;

            }
            return true;
        }

        internal void DoPackageComposition() {
            // GS01: if package composition fails, and we're in the middle of installing a package
            // we should roll back the package install.
            var rulesThatSuperceedMine = InstalledPackageFeed.Instance.FindPackages(CanonicalName.AllPackages).Where(package => !WinsVersus(package)).SelectMany(package => package.ResolvedRules.Where(each => each.Action != CompositionAction.DownloadFile && each.Action != CompositionAction.InstallCommand && each.Action != CompositionAction.RemoveCommand)).ToArray();

            foreach (var rule in ResolvedRules.Where(rule => !rulesThatSuperceedMine.Any(each => each.Destination.Equals(rule.Destination, StringComparison.CurrentCultureIgnoreCase))).OrderBy(rule => rule.Action)) {
                // if we've past the downloads, we need to block on them finishing. No downloads is a quick skip.
                if( rule.Action > CompositionAction.DownloadFile ) {
                    if (!PackageSessionData.WaitForFileDownloads() ) {
                        throw new CoAppException("Failed to download one or more dependent files.");
                    }
                }

                if (!ApplyRule(rule)) {
                    // throw if not successful?
                    // think on this. GS01
                }
            }
        }

        internal void UndoPackageComposition() {
            
            var rulesThatISuperceed = InstalledPackageFeed.Instance.FindPackages(CanonicalName.AllPackages)
                .Except(this.SingleItemAsEnumerable())
                .OrderBy( each => each , new Toolkit.Extensions.Comparer<Package>((packageA, packageB) => GeneralPackageSettings.Instance.WhoWins(packageA, packageB)))
                .Where(WinsVersus)
                .SelectMany(package => package.ResolvedRules.Select( rule => new { package, rule }))
                .WhereDistinct(each => each.rule.Destination)
                .ToArray();
            
            var rulesThatSuperceedMine = InstalledPackageFeed.Instance.FindPackages(CanonicalName.AllPackages).Where(package => !WinsVersus(package)).SelectMany(package => package.ResolvedRules).ToArray();

            foreach (var rule in ResolvedRules) {
                // there are three possibilities
                if( rule.Action == CompositionAction.DownloadFile || rule.Action == CompositionAction.InstallCommand) {
                    //skip these types of rules.
                    continue;
                }

                // never supercede RemoveCommand.
                if (rule.Action != CompositionAction.RemoveCommand) {
                    // 1. my rule was already superceded by another rule: do nothing.
                    if (rulesThatSuperceedMine.Any(each => each.Destination.Equals(rule.Destination, StringComparison.CurrentCultureIgnoreCase))) {
                        continue;
                    }

                    // 2. my rule was superceding another: run that rule.
                    var runRule = rulesThatISuperceed.FirstOrDefault(each => each.rule.Destination.Equals(rule.Destination, StringComparison.CurrentCultureIgnoreCase));
                    if (runRule != null) {

                        runRule.package.ApplyRule(runRule.rule);
                        continue;
                    }
                }

                // 3. my rule should be the current rule, let's undo it.
                switch (rule.Action) {
                    case CompositionAction.Shortcut:
                         if( ShellLink.PointsTo(rule.Destination, rule.Source)) {
                             rule.Destination.TryHardToDelete();
                         }
                        break;
                    case CompositionAction.SymlinkFile:
                        if( File.Exists(rule.Destination) && Symlink.IsSymlink(rule.Destination) && Symlink.GetActualPath(rule.Destination).Equals(rule.Source)) {
                            Symlink.DeleteSymlink(rule.Destination);
                        }
                        break;

                    case CompositionAction.SymlinkFolder:
                        if (Symlink.IsSymlink(rule.Destination) && Symlink.GetActualPath(rule.Destination).Equals(rule.Source)) {
                            Symlink.DeleteSymlink(rule.Destination);
                        }
                        break;
                    case CompositionAction.Registry:
                        if (CanonicalName.Architecture == Architecture.x64 && Environment.Is64BitOperatingSystem) {
                            RegistryView.System["SOFTWARE"][rule.Key].StringValue = null;
                        }
                        else {
                            RegistryView.System["SOFTWARE\\Wow6432Node"][rule.Key].StringValue = null;
                        }
                        break;
                    case CompositionAction.EnvironmentVariable:
                        // not implemented yet.
                        break;

                    case CompositionAction.RemoveCommand:
                        ExecuteCommand(rule.Source, rule.Destination);
                        break;
                }
            }
        }

        internal bool HasLocalLocation {
            get {
                return !LocalLocations.IsNullOrEmpty();
            }
        }

        internal bool HasRemoteLocation {
            get {
                return !RemotePackageLocations.IsNullOrEmpty();
            }
        }

        private Composition CompositionData {
            get {
                return _compositionData ?? (_compositionData = PackageHandler.GetCompositionData(this));
            }
        }

        internal IEnumerable<CompositionRule> CompositionRules {
            get {
                return CompositionData.CompositionRules ?? Enumerable.Empty<CompositionRule>();
            }
        }

        internal IEnumerable<WebApplication> WebApplications {
            get {
                return CompositionData.WebApplications ?? Enumerable.Empty<WebApplication>();
            }
        }

        internal IEnumerable<DeveloperLibrary> DeveloperLibraries {
            get {
                return CompositionData.DeveloperLibraries ?? Enumerable.Empty<DeveloperLibrary>();
            }
        }

        internal IEnumerable<FauxApplication> FauxApplications {
            get {
                return CompositionData.FauxApplications ?? Enumerable.Empty<FauxApplication>();
            }
        }

        internal IEnumerable<Service> Services {
            get {
                return CompositionData.Services ?? Enumerable.Empty<Service>();
            }
        }

        internal IEnumerable<Driver> Drivers {
            get {
                return CompositionData.Drivers ?? Enumerable.Empty<Driver>();
            }
        }

        internal IEnumerable<SourceCode> SourceCodes {
            get {
                return CompositionData.SourceCodes ?? Enumerable.Empty<SourceCode>();
            }
        }

        [Persistable]
        public PackageState PackageState { get { return PackageRequestData.State.Value; } }

        internal bool WinsVersus(Package vsPackage) {
            return GeneralPackageSettings.Instance.WhoWins(CanonicalName, vsPackage.CanonicalName) <= 0;
        }
    }
}