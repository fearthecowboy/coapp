//-----------------------------------------------------------------------
// <copyright company="CoApp Project">
//     Copyright (c) 2011 Garrett Serack . All rights reserved.
// </copyright>
// <license>
//     The software is licensed under the Apache 2.0 License (the "License")
//     You may not use the software except in compliance with the License. 
// </license>
//-----------------------------------------------------------------------

using CoApp.Toolkit.Engine.Model.Roles;

namespace CoApp.Toolkit.Engine {
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.IO;
    using System.Linq;
    using Configuration;
    using Crypto;
    using Exceptions;
    using Extensions;
    using Feeds;
    using Logging;
    using Model;
    using PackageFormatHandlers;
    using Shell;
    using Tasks;
    using Toolkit.Exceptions;
    using Win32;

    public class Package : NotifiesPackageManager {
        private string _canonicalName;
        private bool? _isInstalled;
        private PackageDetails _packageDetails;
        private InternalPackageData _internalPackageData;
        internal IPackageFormatHandler PackageHandler;

        public string CanonicalName { get {
            if( _canonicalName == null && Version > 0 ) {
                _canonicalName = "{0}-{1}-{2}-{3}".format(Name, Version.ToString(), Architecture, PublicKeyToken).ToLowerInvariant();
            }
            return _canonicalName;
        } }

        public string Name { get; internal set; }
        public FourPartVersion Version { get; internal set; }
        public Architecture Architecture { get; internal set; }
        public string PublicKeyToken { get; internal  set; }
        public Guid? ProductCode { get; internal set; }
        public string Vendor { get; internal set; }

        internal string DisplayName { get; set; }

        internal string PackageDirectory { get { return Path.Combine(TargetDirectory, Vendor.MakeSafeFileName(), CanonicalName); } }
        internal string TargetDirectory { get { return PackageManagerSettings.CoAppInstalledDirectory[Architecture];  } }
        /// <summary>
        /// Gets the package details object.
        /// 
        /// if _packageDetails is null, it tries to get the data from the cache (probably by use of a delegate)
        /// </summary>
        internal PackageDetails PackageDetails { 
            get { return _packageDetails ?? (_packageDetails = Cache<PackageDetails>.Value[ProductCode.ToString()] ); }
        }

        internal InternalPackageData InternalPackageData {
            get { return _internalPackageData ?? (_internalPackageData = new InternalPackageData(this)); }
        }

        internal PackageSessionData PackageSessionData { get { return SessionCache<PackageSessionData>.Value[ProductCode.ToString()] ?? (SessionCache<PackageSessionData>.Value[ProductCode.ToString()] = new PackageSessionData(this)); } }
        internal PackageRequestData PackageRequestData { get { return RequestCache<PackageRequestData>.Value[ProductCode.ToString()] ?? (RequestCache<PackageRequestData>.Value[ProductCode.ToString()] = new PackageRequestData(this)); } }

        public bool IsInvalid {
            get { return string.IsNullOrEmpty(_canonicalName) && (Version == 0);  }
        }

        public bool IsInstalled {
            get {
                return _isInstalled ?? (_isInstalled = ((Func<bool>) (() => {
                    try {
                        Changed();
                        if (PackageHandler != null && ProductCode != null ) {
                            return PackageHandler.IsInstalled(ProductCode.Value);
                        }

                        return false;
                    }
                    catch {

                    }
                    return false;
                }))()).Value;
            }
            set {
                if( _isInstalled != value ) {
                    if( value ) {
                        InstalledPackageFeed.Instance.PackageInstalled( this );
                    }
                    else {
                        InstalledPackageFeed.Instance.PackageRemoved(this);
                    }
                }
                _isInstalled = value;
            }
        }

        public bool IsSameFamily(Package olderPackage) {
            return Name == olderPackage.Name &&
                Architecture == olderPackage.Architecture &&
                PublicKeyToken == olderPackage.PublicKeyToken;
        }

        public bool IsAnUpdateFor( Package olderPackage ) {
            return IsSameFamily(olderPackage) &&
                InternalPackageData.PolicyMinimumVersion <= olderPackage.Version &&
                InternalPackageData.PolicyMaximumVersion >= olderPackage.Version;
        }

        public bool IsAnUpgradeFor(Package olderPackage) {
            return
                IsSameFamily(olderPackage) &&
                Version > olderPackage.Version && 
                !(InternalPackageData.PolicyMinimumVersion <= olderPackage.Version &&
                InternalPackageData.PolicyMaximumVersion >= olderPackage.Version);
        }

        public bool IsActive {
            get { 
                return GetCurrentPackageVersion(Name, PublicKeyToken) == Version;
            }
        }

        public string GeneralName {
            get { return "{0}-{1}".format(Name, PublicKeyToken).ToLowerInvariant(); }
        }

        public string CosmeticName {
            get { return "{0}-{1}-{2}".format(Name, Version.ToString(), Architecture).ToLowerInvariant(); }
        }

        /// <summary>
        /// the collection of all known packages
        /// </summary>
        private static readonly ObservableCollection<Package> _packages = new ObservableCollection<Package>();

        internal static Package GetPackageFromProductCode(Guid? productCode) {
            if (productCode != null ) {
                lock (_packages) {
                    var pkg = _packages.FirstOrDefault(package => package.ProductCode == productCode);
                    if (pkg == null) {
                        // where the only thing we know is product code.
                        pkg = new Package(productCode);
                        _packages.Add(pkg);
                    }
                    return pkg;
                }
            }
            return null; // only happens if the productCode isn't a guid.
        }

        internal static  Package GetPackageFromCanonicalName(string canonicalName) {
            lock (_packages) {
                var packageName = PackageName.Parse(canonicalName);
                if (packageName.IsFullMatch) {
                    return GetPackage(packageName.Name, packageName.Version, packageName.Arch, packageName.PublicKeyToken,null);
                }
            }
            return null; // only happens if the canonicalName isn't a canonicalName.
        }

        internal static Package GetPackageFromFilename(string filename) {
            filename = filename.CanonicalizePathIfLocalAndExists();

            if (!File.Exists(filename)) {
                PackageManagerMessages.Invoke.GetSession().FileNotFound(filename);
                return null;
            }

            Package pkg;

            lock (_packages) {
                pkg = (_packages.FirstOrDefault(
                    package =>
                    package.InternalPackageData.HasLocalLocation &&
                    package.InternalPackageData.LocalLocations.Contains(filename)));
            }

            return pkg ?? CoAppMSI.GetCoAppPackageFileInformation(filename);
        }

        internal static Package GetPackage(string packageName, FourPartVersion version, Architecture architecture, string publicKeyToken, Guid? productCode) {
            Package pkg;

            // try via just the package product code
            if (productCode != null) {
                lock (_packages) {
                    pkg = _packages.FirstOrDefault(package => package.ProductCode == productCode);
                }

                if (pkg != null) {
                    // if we *have* this package somewhere, but don't know its name, 
                    // we can now fill *that* in.
                    if (string.IsNullOrEmpty(pkg.Name)) {
                        pkg.Name = packageName;
                        pkg.Architecture = architecture;
                        pkg.Version = version;
                        pkg.PublicKeyToken = publicKeyToken;
                    }
                       return pkg;
                }
            }

            lock (_packages) {
                pkg = (_packages.Where(package =>
                            package.Architecture == architecture && package.Version == version && package.PublicKeyToken == publicKeyToken &&
                                package.Name.Equals(packageName, StringComparison.CurrentCultureIgnoreCase))).FirstOrDefault();

                // we've tried finding it a couple of ways, and got back nothing for our trouble.
                // we'll create an package with the details we have, and pass that back.
                if (pkg == null) {
                    pkg = new Package(packageName, architecture, version, publicKeyToken, productCode);
                    _packages.Add(pkg);
                }
            }

            if( pkg.ProductCode == null ) {
                // if we did find a package and its product code was empty, we can fill that in now if we have it. 
                // or, conversely, we'll generate it from the canonicalname.
                pkg.ProductCode = productCode ?? pkg.CanonicalName.CreateGuid();
            }

            return pkg;
        }

        private Package() {
            Name = string.Empty;
            Version = 0;
            Architecture = Architecture.Unknown;
            PublicKeyToken = string.Empty;
        }

        private Package(Guid? productCode) : this() {
            ProductCode = productCode;
        }

        private Package(string name, Architecture architecture, FourPartVersion version, string publicKeyToken, Guid? productCode) : this(productCode) {
            Name = name;
            Version = version;
            Architecture = architecture;
            PublicKeyToken = publicKeyToken;
        }

        /// <summary>
        /// This drops any data from the object that isn't minimally neccessary for 
        /// the smooth running of the package manager.
        /// </summary>
        internal void DropDetails() {
            // drop the package details. If it's needed again, there should be a delegate to grab it 
            // from the MSI or Feed.
            _packageDetails = null;
            Cache<PackageDetails>.Value.Clear(CanonicalName);
        }

        #region Install/Remove
        public void Install(Action<int> progress = null) {
            try {
                EnsureCanonicalFoldersArePresent();

                var currentVersion = GetCurrentPackageVersion(Name, PublicKeyToken);

                PackageHandler.Install(this, progress);
                IsInstalled = true;
                
                Logger.Message("MSI Install of package [{0}] SUCCEEDED.", CanonicalName );

                if (Version > currentVersion) {
                    SetPackageCurrent();
                    DoPackageComposition(true);
                    Logger.Message("Set Current Version [{0}] SUCCEEDED.", CanonicalName);
                }
                else {
                    DoPackageComposition(false);
                    Logger.Message("Package Composition [{0}] SUCCEEDED.", CanonicalName);
                }
                if( PackageSessionData.IsClientSpecified ) {
                    IsRequired = true;
                }
            }
            catch (Exception e) {
                Logger.Error("Package Install Failure [{0}] => [{1}].\r\n{2}", CanonicalName, e.Message, e.StackTrace);
                
                //we could get here and the MSI had installed but nothing else
                PackageHandler.Remove(this, null);
                IsInstalled = false;
                throw new PackageInstallFailedException(this);
            }
        }

        public void Remove(Action<int> progress = null) {
            try {
                Logger.Message("Attempting to undo package composition");
                UndoPackageComposition();

                Logger.Message("Attempting to remove MSI");
                PackageHandler.Remove(this, progress);
                IsInstalled = false;

                Logger.Message("Deleting Package data Subkey from registry");
                PackageManagerSettings.PerPackageSettings.DeleteSubkey(CanonicalName);
            }
            catch (Exception e) {
                Logger.Error(e);
                PackageManagerMessages.Invoke.GetSession().FailedPackageRemoval(CanonicalName, "GS01: I'm not sure of the reason... ");
                throw new OperationCompletedBeforeResultException();
            }
            finally {
                try {
                    // this will activate the next one in line
                    GetCurrentPackageVersion(Name, PublicKeyToken);
                    // GS01: fix this to rerun package composition on prior version.
                }
                catch (Exception e)  {
                    // boooo!
                    Logger.Error(e);
                    Logger.Error("failed setting active package for {0}-{1}", Name, PublicKeyToken);
                    PackageManagerSettings.PerPackageSettings.DeleteSubkey(GeneralName);
                }
            }
        }
        #endregion

        static private readonly string[] CanonicalFolders = new string[] { ".cache", "ReferenceAssemblies", "ReferenceAssemblies\\x86", "ReferenceAssemblies\\x64", "ReferenceAssemblies\\any", "x86", "x64", "bin", "powershell", "lib", "include", "etc" };
        

        internal static void EnsureCanonicalFoldersArePresent() {
            var root = PackageManagerSettings.CoAppRootDirectory;
            // try to make a convenience symlink:  %SYSTEMDRIVE%:\apps
            var appsdir = "{0}:\\apps".format(root[0]);
            if (!Directory.Exists(appsdir) && !File.Exists(appsdir)) {
                Symlink.MakeDirectoryLink("{0}:\\apps".format(root[0]), root);
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

        #region Package Composition 


        private static readonly Lazy<Dictionary<string, string>> DefaultMacros = new Lazy<Dictionary<string, string>>(() => {
            var root = PackageManagerSettings.CoAppRootDirectory;
            return new Dictionary<string, string>() {
                { "apps" , root },
                { "cache" , Path.Combine(root, ".cache" )},
                { "assemblies" , Path.Combine(root, "ReferenceAssemblies" )},
                { "referenceassemblies" , Path.Combine(root, "ReferenceAssemblies" )},
                { "x86", Path.Combine(root, "x86" )},
                { "x64", Path.Combine(root, "x64" )},
                { "bin", Path.Combine(root, "bin" )},
                { "powershell", Path.Combine(root, "powershell" )},
                { "lib", Path.Combine(root, "lib" )},
                { "include", Path.Combine(root, "include" )},
                { "etc", Path.Combine(root, "etc" )},
                { "allprograms", KnownFolders.GetFolderPath(KnownFolder.CommonPrograms)},
                
            };
        });

            /// <summary>
        /// V1 of the Variable Resolver.
        /// 
        /// </summary>
        /// <param name="text"></param>
        /// <returns></returns>
        internal string ResolveVariables(string text) {
            if (string.IsNullOrEmpty(text)) {
                return string.Empty;
            }

            return text.FormatWithMacros((macro) => {
                if(DefaultMacros.Value.ContainsKey(macro) ) {
                    return DefaultMacros.Value[macro];
                }

                switch( macro.ToLower() ) {
                    case "packagedir":
                    case "packagedirectory":
                    case "packagefolder":
                        return PackageDirectory;

                    case "targetdirectory":
                        return TargetDirectory;

                    case "publishedpackagedir":
                    case "publishedpackagedirectory":
                    case "publishedpackagefolder":
                        return @"${apps}\${productname}";

                    case "productname":
                    case "packagename":
                        return Name;

                    case "version" :
                        return Version.ToString();

                    case "arch" :
                    case "architecture":
                        return Architecture.ToString();

                    case "canonicalname":
                        return CanonicalName;

                    case "cosmeticname" :
                        return CosmeticName;

                }
                return null;
            });
        }

        internal void UpdateDependencyFlags() {
            foreach (var dependentPackage in InternalPackageData.Dependencies.Where(each=>!each.IsRequired)) {
                // find each dependency that is the policy-preferred version, and mark it as currentlyrequested.
                var supercedentPackage = (from supercedent in NewPackageManager.Instance.SearchForInstalledPackages(dependentPackage.Name, null, dependentPackage.Architecture.ToString(), dependentPackage.PublicKeyToken)
                    where supercedent.InternalPackageData.PolicyMinimumVersion <= dependentPackage.Version && supercedent.InternalPackageData.PolicyMaximumVersion >= dependentPackage.Version
                    select supercedent).OrderByDescending(p => p.Version).FirstOrDefault();

                (supercedentPackage??dependentPackage).UpdateDependencyFlags();
            }
            // if this isn't already set, do it.
            if( !IsRequired ) {
                PackageSessionData.IsDependency = true;
            }
        }

        public IEnumerable<CompositionRule> ImplicitRules {
            get {
                foreach (var r in InternalPackageData.Roles) {
                    var role = r;
                    switch (role.PackageRole) {
                        case PackageRole.Application:
                            yield return new CompositionRule() {
                                Action = CompositionAction.SymlinkFolder,
                                Destination = "${publishedpackagedir}",
                                Source = "${packagedir}",
                                Category = null,
                            };
                            break;
                        case PackageRole.DeveloperLibrary:
                            foreach( var devLib in InternalPackageData.DeveloperLibraries.Where( each => each.Name == role.Name) ) {
                                // expose the reference assemblies 
                                if( !devLib.ReferenceAssemblyFiles.IsNullOrEmpty() ) {
                                    foreach( var asmFile in devLib.ReferenceAssemblyFiles ) {
                                        
                                        yield return new CompositionRule() {
                                            Action = CompositionAction.SymlinkFile,
                                            Destination = "${referenceassemblies}\\${arch}\\"+ Path.GetFileName(asmFile),
                                            Source = "${packagedir}\\" + asmFile,
                                            Category = null
                                        };

                                        yield return new CompositionRule() {
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

                                        yield return new CompositionRule() {
                                            Action = CompositionAction.SymlinkFile,
                                            Destination = "${lib}\\${arch}\\" + libFileName,
                                            Source = "${packagedir}\\" + libFile,
                                            Category = null
                                        };

                                        yield return new CompositionRule() {
                                            Action = CompositionAction.SymlinkFile,
                                            Destination = "${lib}\\${arch}\\"+libFileWithoutExtension+"-${version}"+libFileExtension,
                                            Source = "${packagedir}\\" + libFile,
                                            Category = null
                                        };
                                    }
                                }

                                if (!devLib.HeaderFolders.IsNullOrEmpty()) {
                                    foreach (var headerFolder in devLib.HeaderFolders) {

                                        yield return new CompositionRule() {
                                            Action = CompositionAction.SymlinkFolder,
                                            Destination = "${include}\\" + devLib.Name,
                                            Source = "${packagedir}\\" + headerFolder,
                                            Category = null
                                        };

                                        yield return new CompositionRule() {
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
                if( path.IsSimpleSubPath() ){
                    path= Path.Combine(parentPath , path);
                }

                path = path.GetFullPath();

                if( parentPath.IsSubPath(path)) {
                    return path;
                }

            } catch(Exception e) {
                Logger.Error(e);
            }

            Logger.Error("ERROR: path '{0}' must resolve to be a child of '{1}' (resolves to '{2}')", variable, parentPath, path);
            return null;
        }

        public void DoPackageComposition(bool makeCurrent) {
            // GS01: if package composition fails, and we're in the middle of installing a package
            // we should roll back the package install.
            var rules = ImplicitRules.Union(InternalPackageData.CompositionRules).ToArray();

            var packagedir = ResolveVariables("${packagedir}\\");
            var appsdir = ResolveVariables("${apps}\\");
            
            foreach (var rule in rules.Where(r => r.Action == CompositionAction.FileCopy)) {
                var destination = ResolveVariablesAndEnsurePathParentage(appsdir, rule.Destination);
                var source = ResolveVariablesAndEnsurePathParentage(packagedir, rule.Source);
                
                // file copy operations may only manipulate files in the package directory.
                if( string.IsNullOrEmpty(source) ) {
                    Logger.Error("ERROR: Illegal file copy rule. Source must be in package directory [{0}] => [{1}]", rule.Destination, destination);
                    continue;
                }

                if (string.IsNullOrEmpty(destination)) {
                    Logger.Error("ERROR: Illegal file copy rule. Destination must be in package directory [{0}] => [{1}]", source, rule.Source);
                    continue;
                }

                if( !File.Exists(source) ) {
                    Logger.Error("ERROR: Illegal file copy rule. Source file does not exist [{0}] => [{1}]", source, destination);
                    continue;
                }
                try {
                    var destParent = Path.GetDirectoryName(destination);
                    if (!Directory.Exists(destParent)) {
                        Directory.CreateDirectory(destParent);
                    }
                    File.Copy(source, destination, true);
                }
                catch (Exception e) {
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
                var dir = ResolveVariablesAndEnsurePathParentage(packagedir, rule.Source+"\\");

                if( string.IsNullOrEmpty(link) ) {
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
                    }
                    catch (Exception) {
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
                    if (!Directory.Exists(Path.GetDirectoryName(link))) {
                        Directory.CreateDirectory(Path.GetDirectoryName(link));
                    }

                    try {
                        Logger.Message("Creating file Symlink [{0}] => [{1}]", link, file);
                        Symlink.MakeFileLink(link, file);
                    }
                    catch (Exception) {
                        Logger.Error("Warning: File Symlink Link Failed. [{0}] => [{1}]", link, file);
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
                    if (!Directory.Exists(Path.GetDirectoryName(shortcutPath))) {
                        Logger.Message("Creating Shortcut [{0}] => [{1}]", shortcutPath, target);
                        Directory.CreateDirectory(Path.GetDirectoryName(shortcutPath));
                    }

                    ShellLink.CreateShortcut(shortcutPath, target);
                }
            }

            foreach (var rule in rules.Where(r => r.Action == CompositionAction.EnvironmentVariable)) {
                var environmentVariable = ResolveVariables(rule.Key);
                var environmentValue = ResolveVariables(rule.Value);

                switch( environmentVariable.ToLower() ) {
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
                        Logger.Message("Package may not set environment variable '{0}'", environmentValue );
                        break;

                    default:
                        EnvironmentUtility.SetSystemEnvironmentVariable(environmentVariable, environmentValue);
                        break;
                }
            }

            
            var view = Architecture == Architecture.x64 ? RegistryView.System["SOFTWARE"] :RegistryView.System["SOFTWARE\\Wow6432Node"];

            foreach (var rule in rules.Where(r => r.Action == CompositionAction.Registry)) {
                var regKey = ResolveVariables(rule.Key);
                var regValue = ResolveVariables(rule.Value);

                view[regKey].StringValue = regValue;
            }

        }

        public void UndoPackageComposition() {
            var rules = ImplicitRules.Union(InternalPackageData.CompositionRules);


            foreach (var link in from rule in rules.Where(r => r.Action == CompositionAction.Shortcut)
                let target = this.ResolveVariables(rule.Source).GetFullPath()
                let link = this.ResolveVariables(rule.Destination).GetFullPath()
                where ShellLink.PointsTo(link, target)
                select link) {
                link.TryHardToDelete();
            }

            foreach (var link in from rule in rules.Where(r => r.Action == CompositionAction.SymlinkFile)
                let target = this.ResolveVariables(rule.Source).GetFullPath()
                let link = this.ResolveVariables(rule.Destination).GetFullPath()
                where File.Exists(target) && File.Exists(link) && Symlink.IsSymlink(link) && Symlink.GetActualPath(link).Equals(target)
                select link) {
                Symlink.DeleteSymlink(link);
            }

            foreach (var link in from rule in rules.Where(r => r.Action == CompositionAction.SymlinkFolder)
                let target = this.ResolveVariables(rule.Source).GetFullPath()
                let link = this.ResolveVariables(rule.Destination).GetFullPath()
                where File.Exists(target) && Symlink.IsSymlink(link) && Symlink.GetActualPath(link).Equals(target)
                select link) {
                Symlink.DeleteSymlink(link);
            }
        }

        internal static FourPartVersion GetCurrentPackageVersion(string packageName, string publicKeyToken) {
            var installedVersionsOfPackage = from pkg in NewPackageManager.Instance.InstalledPackages
                where pkg.Name.Equals(packageName, StringComparison.CurrentCultureIgnoreCase) &&
                      pkg.PublicKeyToken.Equals(publicKeyToken, StringComparison.CurrentCultureIgnoreCase)
                orderby pkg.Version descending
                select pkg;

            var latestPackage = installedVersionsOfPackage.FirstOrDefault();

            // clean as we go...
            if (latestPackage == null) {
                PackageManagerSettings.PerPackageSettings["{0}-{1}".format(packageName, publicKeyToken), "CurrentVersion"].Value = null;
                return 0;
            }

            FourPartVersion ver = (ulong)PackageManagerSettings.PerPackageSettings[latestPackage.GeneralName, "CurrentVersion"].LongValue;

            if (ver == 0 || installedVersionsOfPackage.FirstOrDefault(p => p.Version == ver) == null) {
                latestPackage.SetPackageCurrent();
                return latestPackage.Version;
            }

            return ver;
        }

        /// <summary>
        ///  Indicates that the client specifically requested the package, or is the dependency of a requested package
        /// </summary>
        public bool IsRequired { 
            get { return IsClientRequested || PackageSessionData.IsDependency; } 
            set { IsClientRequested = value; }
        }

        /// <summary>
        ///  Indicates that the client specifically requested the package
        /// </summary>
        public bool IsClientRequested {
            get { return PackageSessionData.PackageSettings["#Requested"].BoolValue; }
            set { PackageSessionData.PackageSettings["#Requested"].BoolValue = value;}
        }
     
        public bool DoNotUpdate {
            get { return PackageSessionData.PackageSettings["#DoNotUpdate"].BoolValue; }
            set { PackageSessionData.PackageSettings["#DoNotUpdate"].BoolValue = value; }
        }

        public bool IsBlocked {
            get { return PackageSessionData.GeneralPackageSettings["#Blocked"].BoolValue; }
            set { PackageSessionData.GeneralPackageSettings["#Blocked"].BoolValue = value; }
        }

        public bool DoNotUpgrade {
            get { return PackageSessionData.GeneralPackageSettings["#DoNotUpgrade"].BoolValue; }
            set { PackageSessionData.GeneralPackageSettings["#DoNotUpgrade"].BoolValue = value; }
        }

        public void SetPackageCurrent() {
            if (!IsInstalled) {
                throw new PackageNotInstalledException(this);
            }

            if (Version == (ulong) PackageSessionData.GeneralPackageSettings["#CurrentVersion"].LongValue) {
                return; // it's already set to the current version.
            }

            DoPackageComposition(true);
            PackageSessionData.GeneralPackageSettings["#CurrentVersion"].LongValue = (long)(ulong) Version;
            
        }
         #endregion
    }

    internal class InternalPackageData : NotifiesPackageManager {
        private readonly Package _package;

        private string _canonicalPackageLocation;
        private string _canonicalFeedLocation;

        private string _primaryLocalLocation;
        private string _primaryRemoteLocation;
        private string _primaryFeedLocation;

        private readonly List<Uri> _remoteLocations = new List<Uri>();
        private readonly List<string> _feedLocations = new List<string>();
        private readonly List<string> _localLocations = new List<string>();

        // set once only:
        internal FourPartVersion PolicyMinimumVersion { get; set; }
        internal FourPartVersion PolicyMaximumVersion { get; set; }

        public readonly List<Role> Roles = new List<Role>();
        public List<Feature> Features { get; set; }
        public List<Feature> RequiredFeatures { get; set; } 

        // public readonly List<PackageAssemblyInfo> Assemblies = new List<PackageAssemblyInfo>();
        public readonly ObservableCollection<Package> Dependencies = new ObservableCollection<Package>();

        public string CanonicalPackageLocation {
            get { return _canonicalPackageLocation; }
            set {
                try {
                    RemoteLocation = _canonicalPackageLocation = new Uri(value).AbsoluteUri;
                }
                catch {
                }
            }
        }

        public string CanonicalFeedLocation {
            get { return _canonicalFeedLocation; }
            set {
                FeedLocation = _canonicalFeedLocation = value;
            }
        }

        public string CanonicalSourcePackageLocation { get; set; }
        
        internal InternalPackageData(Package package) {
            _package = package;
             Dependencies.CollectionChanged += (x, y) => Changed();
        }

        public bool IsPackageSatisfied {
            get { return _package.IsInstalled || !string.IsNullOrEmpty(LocalLocation) && RemoteLocation != null && _package.PackageSessionData.Supercedent != null; }
        }

        public bool HasLocalLocation {
            get { return !string.IsNullOrEmpty(LocalLocation); }
        }

        public bool HasRemoteLocation {
            get { return !string.IsNullOrEmpty(RemoteLocation); }
        }

        public IEnumerable<string> LocalLocations { get { return _localLocations.ToArray(); } }
        public IEnumerable<string> RemoteLocations { get { return _remoteLocations.Select(each => each.AbsoluteUri).ToArray(); } }
        public IEnumerable<string> FeedLocations { get { return _feedLocations.ToArray(); } }

        public string LocalLocation { 
            get {
                if (_primaryLocalLocation.FileIsLocalAndExists()) {
                    return _primaryLocalLocation;
                }
                // use the setter to remove non-viable locations.
                LocalLocation = null;

                // whatever is primary after the set is good for me.
                return _primaryLocalLocation;
            }
            set {
                lock (_localLocations) {
                    try {
                        var location = value.CanonicalizePathIfLocalAndExists();

                        if (!string.IsNullOrEmpty(location)) {
                            // this location is acceptable.
                            _primaryLocalLocation = location;
                            if (!_localLocations.Contains(location)) {
                                _localLocations.Add(location);
                            }
                            return;
                        }
                    } catch {
                        // file couldn't canonicalize.
                    }

                    _primaryLocalLocation = null;

                    // try to find an acceptable local location from the list 
                    foreach (var path in _localLocations.Where(path => path.FileIsLocalAndExists())) {
                        _primaryLocalLocation = path;
                        break;
                    }
                }
            }
        }

        public string RemoteLocation {
            get {
                if (!string.IsNullOrEmpty(_canonicalPackageLocation)) {
                    return _canonicalPackageLocation;
                }

                if (!string.IsNullOrEmpty(_primaryRemoteLocation)) {
                    return _primaryRemoteLocation;
                }

                // use the setter to remove non-viable locations.
                RemoteLocation = null;

                // whatever is primary after the set is good for me.
                return _primaryRemoteLocation;
            }

            set {
                lock (_remoteLocations) {
                    if (!string.IsNullOrEmpty(value)) {
                        try {
                            var location = new Uri(value);

                            // this location is acceptable.
                            _primaryRemoteLocation = location.AbsoluteUri;

                            if (!_remoteLocations.Contains(location)) {
                                _remoteLocations.Add(location);
                            }

                            return;
                        }
                        catch {
                            // path couldn't be expressed as a URI?.
                        }
                    }
                    
                    // set it as the first viable remote location.
                    var uri = _remoteLocations.FirstOrDefault();
                    _primaryRemoteLocation = uri == null ? null : uri.AbsoluteUri;
                }
            }
        }

        public string FeedLocation {
            get {
                if (!string.IsNullOrEmpty(_canonicalFeedLocation)) {
                    return _canonicalFeedLocation;
                }

                if (!string.IsNullOrEmpty(_primaryFeedLocation)) {
                    return _primaryFeedLocation;
                }

                // use the setter to remove non-viable locations.
                FeedLocation = null;

                // whatever is primary after the set is good for me.
                return _primaryFeedLocation;
            }

            set {
                lock (_feedLocations) {
                    if (!string.IsNullOrEmpty(value)) {
                        _primaryFeedLocation= value;
                        if (!_feedLocations.Contains(value)) {
                            _feedLocations.Add(value);
                        }
                        return;
                    }

                    // set it as the first viable remote location.
                    var location = _feedLocations.FirstOrDefault();
                    _primaryFeedLocation = string.IsNullOrEmpty(location) ? null : location;
                }
            }
        }

        private Composition CompositionData {get { return _compositionData ?? (_compositionData = _package.PackageHandler.GetCompositionData(_package)); }}

        private Composition _compositionData;
        public IEnumerable<CompositionRule> CompositionRules { get { return CompositionData.CompositionRules ?? Enumerable.Empty<CompositionRule>(); } }
        public IEnumerable<WebApplication> WebApplications { get { return CompositionData.WebApplications ?? Enumerable.Empty<WebApplication>(); } }
        public IEnumerable<DeveloperLibrary> DeveloperLibraries { get { return CompositionData.DeveloperLibraries?? Enumerable.Empty<DeveloperLibrary>(); } }
        public IEnumerable<CoApp.Toolkit.Engine.Model.Roles.Service> Services { get { return CompositionData.Services ?? Enumerable.Empty<CoApp.Toolkit.Engine.Model.Roles.Service>(); } }
        public IEnumerable<Driver> Drivers { get { return CompositionData.Drivers ?? Enumerable.Empty<Driver>(); } }
        public IEnumerable<SourceCode> SourceCodes { get { return CompositionData.SourceCodes ?? Enumerable.Empty<SourceCode>(); } }
        
    }

    /// <summary>
    /// This stores information that is really only relevant to the currently running 
    /// Session, not between sessions.
    /// 
    /// The instance of this is bound to the Session.
    /// </summary>  
    internal class PackageSessionData : NotifiesPackageManager {
        internal bool DoNotSupercede; // TODO: it's possible these could be contradictory
        internal bool UpgradeAsNeeded; // TODO: it's possible these could be contradictory
        internal bool IsClientSpecified;
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
            get { return _supercedent; }
            set {
                if (value != _supercedent) {
                    _supercedent = value;
                    Changed();
                }
            }
        }

        public bool PackageFailedInstall {
            get { return _packageFailedInstall; }
            set {
                if (_packageFailedInstall != value) {
                    _packageFailedInstall = value;
                    Changed();
                }
            }
        }

        /*
        public bool Supercedes(Package p)
        {
            return Architecture == p.Architecture &&
                   PublicKeyToken == p.PublicKeyToken &&
                   Name.Equals(p.Name, StringComparison.CurrentCultureIgnoreCase) &&
                   p.Version <= PolicyMaximumVersion && p.Version >= PolicyMinimumVersion;
        }
        */
        
        public bool CouldNotDownload {
            get { return _couldNotDownload; }
            set {
                if (value != _couldNotDownload) {
                    _couldNotDownload = value;
                    Changed();
                }
            }
        }

        public bool AllowedToSupercede {
            get { return UpgradeAsNeeded || (!IsClientSpecified && !DoNotSupercede) && IsPotentiallyInstallable; }
        }

        public bool IsPotentiallyInstallable {
            get {
                return !PackageFailedInstall && (_package.InternalPackageData.HasLocalLocation || !CouldNotDownload && _package.InternalPackageData.HasRemoteLocation);
            }
        }

        public bool CanSatisfy { get; set; }

        
        public string LocalValidatedLocation {
            get {
                if (!string.IsNullOrEmpty(_localValidatedLocation) && _localValidatedLocation.FileIsLocalAndExists()) {
                    return _localValidatedLocation;
                }

                var location = _package.InternalPackageData.LocalLocation;
                if (string.IsNullOrEmpty(location)) {
                    // there are no local locations at all for this package?
                    return _localValidatedLocation = null;
                }

                if (Verifier.HasValidSignature(location)) {
                    PackageManagerMessages.Invoke.GetSession().SignatureValidation(location, true, Verifier.GetPublisherName(location));
                    return _localValidatedLocation = location;
                }
                PackageManagerMessages.Invoke.GetSession().SignatureValidation(location, false, null);

                var result = _package.InternalPackageData.LocalLocations.Any(Verifier.HasValidSignature) ? location : null;

                PackageManagerMessages.Invoke.GetSession().SignatureValidation(result, !string.IsNullOrEmpty(result), string.IsNullOrEmpty(result) ? null : Verifier.GetPublisherName(result));

                // GS01: if all the locations end up invalid, and we're trying to remove a package 

                return _localValidatedLocation = result;
            }
        }

        private RegistryView _generalPackageSettings;
        internal RegistryView GeneralPackageSettings { get {
            return
                _generalPackageSettings ?? (_generalPackageSettings = PackageManagerSettings.PerPackageSettings["{0}-{1}".format(_package.Name, _package.PublicKeyToken)]);
        }}

        private RegistryView _packageSettings;
        internal RegistryView PackageSettings { get {
            return _packageSettings ?? (_packageSettings = PackageManagerSettings.PerPackageSettings[_package.CanonicalName]);
        }}

        private int _lastProgress;
        public int DownloadProgress { get; set; }
        public int DownloadProgressDelta { get {
            var p = DownloadProgress;
            var result = p - _lastProgress;
            if (result < 0)
                return 0;

            _lastProgress = p;
            return result;
        }}
    }

    /// <summary>
    /// This stores information that is really only relevant to the currently running 
    /// request, not between sessions.
    /// 
    /// The instance of this is bound to the Session.
    /// </summary>
    internal class PackageRequestData : NotifiesPackageManager {
        private Package _package;

        internal bool NotifiedClientThisSupercedes;

        internal PackageRequestData(Package package) {
            _package = package;
        }
    }

    public class NotifiesPackageManager {
        internal static void Changed() {
            // notify the Registrar that a change has occured in a package.
            NewPackageManager.Instance.Updated();
        }
    }
}