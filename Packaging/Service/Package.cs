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
    using System.IO;
    using System.Linq;
    using Common;
    using Common.Exceptions;
    using Common.Model;
    using Exceptions;
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

    public class Package : NotifiesPackageManager {
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

        internal CanonicalName CanonicalName;
        internal IPackageFormatHandler PackageHandler;
        internal BindingPolicy BindingPolicy;
        internal string Vendor { get; set; }
        internal string DisplayName { get; set; }
        internal PackageDetails PackageDetails { get; set; }
        private bool? _isInstalled;
        private Composition _compositionData;

        internal readonly XList<Uri> RemoteLocations = new XList<Uri>();
        internal readonly XList<Uri> FeedLocations = new XList<Uri>();
        internal readonly XList<string> LocalLocations = new XList<string>();
        internal readonly XList<Role> Roles = new XList<Role>();
        internal readonly XList<Feature> Features = new XList<Feature>();
        internal readonly XList<Feature> RequiredFeatures = new XList<Feature>();
        internal readonly ObservableCollection<Package> Dependencies = new ObservableCollection<Package>();
        
        private Package(CanonicalName canonicalName) {
            Packages.Changed += x => Changed();
            Dependencies.CollectionChanged += (x, y) => Changed();
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
            var installedVersionsOfPackage = PackageManagerImpl.Instance.InstalledPackages.Where(each => canonicalName.DiffersOnlyByVersion(each.CanonicalName)).OrderByDescending(each => each.CanonicalName.Version);
            var latestPackage = installedVersionsOfPackage.FirstOrDefault();

            // clean as we go...
            if (latestPackage == null) {
                PackageManagerSettings.PerPackageSettings[canonicalName.GeneralName, "CurrentVersion"].Value = null;
                return 0;
            }

            // is there a version set?
            FourPartVersion ver = (ulong)PackageManagerSettings.PerPackageSettings[canonicalName.GeneralName, "CurrentVersion"].LongValue;

            // if not (or it's not set to an installed package), let's set it to the latest version of the package.
            if (ver == 0 || installedVersionsOfPackage.FirstOrDefault(p => p.CanonicalName.Version == ver) == null) {
                latestPackage.SetPackageCurrent();
                return latestPackage.CanonicalName.Version;
            }
            return ver;
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
                return SessionCache<PackageSessionData>.Value[CanonicalName] ?? (SessionCache<PackageSessionData>.Value[CanonicalName] = new PackageSessionData(this));
            }
        }

        internal PackageRequestData PackageRequestData {
            get {
                var cache = Event<GetRequestPackageDataCache>.RaiseFirst();
                return cache[CanonicalName] ?? (cache[CanonicalName] = new PackageRequestData(this));
            }
        }

        internal bool IsInstalled {
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

        /// <summary>
        /// Determines if this package is a supercedent of an older package.
        /// 
        /// Note:This should be the only method that does comparisons on the package policy *ANYWHERE*
        /// </summary>
        /// <param name="olderPackage"></param>
        /// <returns></returns>
        public bool IsAnUpdateFor(Package olderPackage) {
            return CanonicalName.DiffersOnlyByVersion(olderPackage.CanonicalName) && 
                BindingPolicy != null && 
                    BindingPolicy.Minimum <= olderPackage.CanonicalName.Version &&
                    BindingPolicy.Maximum >= olderPackage.CanonicalName.Version;
        }

        public bool IsAnUpgradeFor(Package olderPackage) {
            return CanonicalName.DiffersOnlyByVersion(olderPackage.CanonicalName) &&
                CanonicalName.Version > olderPackage.CanonicalName.Version && !IsAnUpdateFor(olderPackage);
        }

        public bool IsActive {
            get {
                return GetCurrentPackageVersion(CanonicalName) == CanonicalName.Version;
            }
        }

        public void Install() {
            try {
                Engine.EnsureCanonicalFoldersArePresent();

                var currentVersion = GetCurrentPackageVersion(CanonicalName);

                PackageHandler.Install(this);
                IsInstalled = true;

                Logger.Message("MSI Install of package [{0}] SUCCEEDED.", CanonicalName);

                if (CanonicalName.Version > currentVersion) {
                    SetPackageCurrent();
                    DoPackageComposition(true);
                    Logger.Message("Set Current Version [{0}] SUCCEEDED.", CanonicalName);
                } else {
                    DoPackageComposition(false);
                    Logger.Message("Package Composition [{0}] SUCCEEDED.", CanonicalName);
                }
                if (PackageSessionData.IsClientSpecified) {
                    IsRequired = true;
                }
            } catch (Exception e) {
                Logger.Error("Package Install Failure [{0}] => [{1}].\r\n{2}", CanonicalName, e.Message, e.StackTrace);

                //we could get here and the MSI had installed but nothing else
                PackageHandler.Remove(this);
                IsInstalled = false;
                throw new PackageInstallFailedException(this);
            }
        }

        public void Remove() {
            try {
                Logger.Message("Attempting to undo package composition");
                UndoPackageComposition();

                Logger.Message("Attempting to remove MSI");
                PackageHandler.Remove(this);
                IsInstalled = false;

                Logger.Message("Deleting Package data Subkey from registry");
                PackageManagerSettings.PerPackageSettings.DeleteSubkey(CanonicalName);
            } catch (Exception e) {
                Logger.Error(e);
                Event<GetResponseInterface>.RaiseFirst().FailedPackageRemoval(CanonicalName, "GS01: I'm not sure of the reason... ");
                throw new OperationCompletedBeforeResultException();
            } finally {
                try {
                    // this will activate the next one in line
                    GetCurrentPackageVersion(CanonicalName);
                    // GS01: fix this to rerun package composition on prior version.
                } catch (Exception e) {
                    // boooo!
                    Logger.Error(e);
                    Logger.Error("failed setting active package for {0}", CanonicalName.GeneralName);
                    PackageManagerSettings.PerPackageSettings.DeleteSubkey(CanonicalName.GeneralName);
                }
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
                        return @"${apps}\${productname}";

                    case "productname":
                    case "packagename":
                        return CanonicalName.WholeName;

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

        internal void UpdateDependencyFlags() {
            foreach (var dpkg in Dependencies.Where(each => !each.IsRequired)) {
                var dependentPackage = dpkg;

                // find each dependency that is the policy-preferred version, and mark it as currentlyrequested.
                var supercedentPackage = (from supercedent in PackageManagerImpl.Instance.SearchForInstalledPackages(dependentPackage.CanonicalName.OtherVersionFilter)
                    where supercedent.IsAnUpdateFor(dependentPackage)
                    select supercedent).OrderByDescending(p => p.CanonicalName.Version).FirstOrDefault();

                (supercedentPackage ?? dependentPackage).UpdateDependencyFlags();
            }
            // if this isn't already set, do it.
            if (!IsRequired) {
                PackageSessionData.IsDependency = true;
            }
        }

        public IEnumerable<CompositionRule> ImplicitRules {
            get {
                foreach (var r in Roles) {
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
                                            Destination = "${referenceassemblies}\\${arch}\\${productname}-${version}\\" + Path.GetFileName(asmFile),
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

        public void DoPackageComposition(bool makeCurrent) {
            // GS01: if package composition fails, and we're in the middle of installing a package
            // we should roll back the package install.
            var rules = ImplicitRules.Union(CompositionRules).ToArray();

            var packagedir = ResolveVariables("${packagedir}\\");
            var appsdir = ResolveVariables("${apps}\\");

            foreach (var rule in rules.Where(r => r.Action == CompositionAction.FileCopy)) {
                var destination = ResolveVariablesAndEnsurePathParentage(appsdir, rule.Destination);
                var source = ResolveVariablesAndEnsurePathParentage(packagedir, rule.Source);

                // file copy operations may only manipulate files in the package directory.
                if (string.IsNullOrEmpty(source)) {
                    Logger.Error("ERROR: Illegal file copy rule. Source must be in package directory [{0}] => [{1}]", rule.Destination, destination);
                    continue;
                }

                if (string.IsNullOrEmpty(destination)) {
                    Logger.Error("ERROR: Illegal file copy rule. Destination must be in package directory [{0}] => [{1}]", source, rule.Source);
                    continue;
                }

                if (!File.Exists(source)) {
                    Logger.Error("ERROR: Illegal file copy rule. Source file does not exist [{0}] => [{1}]", source, destination);
                    continue;
                }
                try {
                    var destParent = Path.GetDirectoryName(destination);
                    if (!string.IsNullOrEmpty(destParent)) {
                        if (!Directory.Exists(destParent)) {
                            Directory.CreateDirectory(destParent);
                        }
                        File.Copy(source, destination, true);
                    }
                } catch (Exception e) {
                    Logger.Error(e);
                }
            }

            foreach (var rule in rules.Where(r => r.Action == CompositionAction.FileRewrite)) {
                var destination = ResolveVariablesAndEnsurePathParentage(appsdir, rule.Destination);
                var source = ResolveVariablesAndEnsurePathParentage(packagedir, rule.Source);

                // file copy operations may only manipulate files in the package directory.
                if (string.IsNullOrEmpty(source)) {
                    Logger.Error("ERROR: Illegal file rewrite rule. Source must be in package directory [{0}] => [{1}]", rule.Destination, destination);
                    continue;
                }

                if (string.IsNullOrEmpty(destination)) {
                    Logger.Error("ERROR: Illegal file rewrite rule. Destination must be in package directory [{0}] => [{1}]", source, rule.Source);
                    continue;
                }

                if (!File.Exists(source)) {
                    Logger.Error("ERROR: Illegal file rewrite rule. Source file does not exist [{0}] => [{1}]", source, destination);
                    continue;
                }

                File.WriteAllText(destination, ResolveVariables(File.ReadAllText(source)));
            }

            foreach (var rule in rules.Where(r => r.Action == CompositionAction.SymlinkFolder)) {
                var link = ResolveVariablesAndEnsurePathParentage(appsdir, rule.Destination);
                var dir = ResolveVariablesAndEnsurePathParentage(packagedir, rule.Source + "\\");

                if (string.IsNullOrEmpty(link)) {
                    Logger.Error("ERROR: Illegal folder symlink rule. Destination location '{0}' must be a subpath of {1}", rule.Destination, appsdir);
                    continue;
                }

                if (string.IsNullOrEmpty(dir)) {
                    Logger.Error("ERROR: Illegal folder symlink rule. Source folder '{0}' must be a subpath of {1}", rule.Source, packagedir);
                    continue;
                }

                if (!Directory.Exists(dir)) {
                    Logger.Error("ERROR: Illegal folder symlink rule. Source folder '{0}' does not exist.", dir);
                    continue;
                }

                if (makeCurrent || !Directory.Exists(link)) {
                    try {
                        Logger.Message("Creatign Directory Symlink [{0}] => [{1}]", link, dir);
                        Symlink.MakeDirectoryLink(link, dir);
                    } catch (Exception) {
                        Logger.Error("Warning: Directory Symlink Link Failed. [{0}] => [{1}]", link, dir);
                    }
                }
            }

            foreach (var rule in rules.Where(r => r.Action == CompositionAction.SymlinkFile)) {
                var link = ResolveVariablesAndEnsurePathParentage(appsdir, rule.Destination);
                var file = ResolveVariablesAndEnsurePathParentage(packagedir, rule.Source);

                if (string.IsNullOrEmpty(link)) {
                    Logger.Error("ERROR: Illegal file symlink rule. Destination location '{0}' must be a subpath of {1}", rule.Destination, appsdir);
                    continue;
                }

                if (string.IsNullOrEmpty(file)) {
                    Logger.Error("ERROR: Illegal file symlink rule. Source file '{0}' must be a subpath of {1}", rule.Source, packagedir);
                    continue;
                }

                if (!File.Exists(file)) {
                    Logger.Error("ERROR: Illegal folder symlink rule. Source file '{0}' does not exist.", file);
                    continue;
                }

                if (makeCurrent || !File.Exists(link)) {
                    var parentDir = Path.GetDirectoryName(link);
                    if (!string.IsNullOrEmpty(parentDir)) {
                        if (!Directory.Exists(parentDir)) {
                            Directory.CreateDirectory(parentDir);
                        }
                        try {
                            Logger.Message("Creating file Symlink [{0}] => [{1}]", link, file);
                            Symlink.MakeFileLink(link, file);
                        } catch (Exception) {
                            Logger.Error("Warning: File Symlink Link Failed. [{0}] => [{1}]", link, file);
                        }
                    }
                }
            }

            foreach (var rule in rules.Where(r => r.Action == CompositionAction.Shortcut)) {
                var shortcutPath = ResolveVariables(rule.Destination).GetFullPath();
                var target = ResolveVariablesAndEnsurePathParentage(packagedir, rule.Source);

                if (string.IsNullOrEmpty(target)) {
                    Logger.Error("ERROR: Illegal shortcut rule. Source file '{0}' must be a subpath of {1}", rule.Source, packagedir);
                    continue;
                }

                if (!File.Exists(target)) {
                    Logger.Error("ERROR: Illegal shortcut rule. Source file '{0}' does not exist.", target);
                    continue;
                }

                if (makeCurrent || !File.Exists(shortcutPath)) {
                    var parentDir = Path.GetDirectoryName(shortcutPath);
                    if (!string.IsNullOrEmpty(parentDir)) {
                        if (!Directory.Exists(parentDir)) {
                            Directory.CreateDirectory(parentDir);
                        }
                        Logger.Message("Creating Shortcut [{0}] => [{1}]", shortcutPath, target);
                        ShellLink.CreateShortcut(shortcutPath, target);
                    }
                }
            }

            foreach (var rule in rules.Where(r => r.Action == CompositionAction.EnvironmentVariable)) {
                var environmentVariable = ResolveVariables(rule.Key);
                var environmentValue = ResolveVariables(rule.Value);

                switch (environmentVariable.ToLower()) {
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
                        Logger.Message("Package may not set environment variable '{0}'", environmentValue);
                        break;

                    default:
                        EnvironmentUtility.SetSystemEnvironmentVariable(environmentVariable, environmentValue);
                        break;
                }
            }

            var view = CanonicalName.Architecture == Architecture.x64 ? RegistryView.System["SOFTWARE"] : RegistryView.System["SOFTWARE\\Wow6432Node"];

            foreach (var rule in rules.Where(r => r.Action == CompositionAction.Registry)) {
                var regKey = ResolveVariables(rule.Key);
                var regValue = ResolveVariables(rule.Value);

                view[regKey].StringValue = regValue;
            }
        }

        public void UndoPackageComposition() {
            var rules = ImplicitRules.Union(CompositionRules).ToArray();

            foreach (var link in from rule in rules.Where(r => r.Action == CompositionAction.Shortcut)
                let target = ResolveVariables(rule.Source).GetFullPath()
                let link = ResolveVariables(rule.Destination).GetFullPath()
                where ShellLink.PointsTo(link, target)
                select link) {
                link.TryHardToDelete();
            }

            foreach (var link in from rule in rules.Where(r => r.Action == CompositionAction.SymlinkFile)
                let target = ResolveVariables(rule.Source).GetFullPath()
                let link = ResolveVariables(rule.Destination).GetFullPath()
                where File.Exists(target) && File.Exists(link) && Symlink.IsSymlink(link) && Symlink.GetActualPath(link).Equals(target)
                select link) {
                Symlink.DeleteSymlink(link);
            }

            foreach (var link in from rule in rules.Where(r => r.Action == CompositionAction.SymlinkFolder)
                let target = ResolveVariables(rule.Source).GetFullPath()
                let link = ResolveVariables(rule.Destination).GetFullPath()
                where File.Exists(target) && Symlink.IsSymlink(link) && Symlink.GetActualPath(link).Equals(target)
                select link) {
                Symlink.DeleteSymlink(link);
            }
        }

        /// <summary>
        ///   Indicates that the client specifically requested the package, or is the dependency of a requested package
        /// </summary>
        public bool IsRequired {
            get {
                return IsClientRequested || PackageSessionData.IsDependency;
            }
            set {
                IsClientRequested = value;
            }
        }

        /// <summary>
        ///   Indicates that the client specifically requested the package
        /// </summary>
        public bool IsClientRequested {
            get {
                return PackageSessionData.PackageSettings["#Requested"].BoolValue;
            }
            set {
                PackageSessionData.PackageSettings["#Requested"].BoolValue = value;
            }
        }

        public bool DoNotUpdate {
            get {
                return PackageSessionData.PackageSettings["#DoNotUpdate"].BoolValue;
            }
            set {
                PackageSessionData.PackageSettings["#DoNotUpdate"].BoolValue = value;
            }
        }

        public bool IsBlocked {
            get {
                return PackageSessionData.GeneralPackageSettings["#Blocked"].BoolValue;
            }
            set {
                PackageSessionData.GeneralPackageSettings["#Blocked"].BoolValue = value;
            }
        }

        public bool DoNotUpgrade {
            get {
                return PackageSessionData.GeneralPackageSettings["#DoNotUpgrade"].BoolValue;
            }
            set {
                PackageSessionData.GeneralPackageSettings["#DoNotUpgrade"].BoolValue = value;
            }
        }

        public void SetPackageCurrent() {
            if (!IsInstalled) {
                throw new PackageNotInstalledException(this);
            }

            if (CanonicalName.Version == (ulong)PackageSessionData.GeneralPackageSettings["#CurrentVersion"].LongValue) {
                return; // it's already set to the current version.
            }

            DoPackageComposition(true);
            PackageSessionData.GeneralPackageSettings["#CurrentVersion"].LongValue = (long)(ulong)CanonicalName.Version;
        }

        public bool HasLocalLocation {
            get {
                return !LocalLocations.IsNullOrEmpty();
            }
        }

        public bool HasRemoteLocation {
            get {
                return !RemoteLocations.IsNullOrEmpty();
            }
        }

        private Composition CompositionData {
            get {
                return _compositionData ?? (_compositionData = PackageHandler.GetCompositionData(this));
            }
        }

        public IEnumerable<CompositionRule> CompositionRules {
            get {
                return CompositionData.CompositionRules ?? Enumerable.Empty<CompositionRule>();
            }
        }

        public IEnumerable<WebApplication> WebApplications {
            get {
                return CompositionData.WebApplications ?? Enumerable.Empty<WebApplication>();
            }
        }

        public IEnumerable<DeveloperLibrary> DeveloperLibraries {
            get {
                return CompositionData.DeveloperLibraries ?? Enumerable.Empty<DeveloperLibrary>();
            }
        }

        public IEnumerable<Service> Services {
            get {
                return CompositionData.Services ?? Enumerable.Empty<Service>();
            }
        }

        public IEnumerable<Driver> Drivers {
            get {
                return CompositionData.Drivers ?? Enumerable.Empty<Driver>();
            }
        }

        public IEnumerable<SourceCode> SourceCodes {
            get {
                return CompositionData.SourceCodes ?? Enumerable.Empty<SourceCode>();
            }
        }
    }


}