//-----------------------------------------------------------------------
// <copyright company="CoApp Project">
//     Copyright (c) 2011 Garrett Serack . All rights reserved.
// </copyright>
// <license>
//     The software is licensed under the Apache 2.0 License (the "License")
//     You may not use the software except in compliance with the License. 
// </license>
//-----------------------------------------------------------------------

namespace CoApp.Packaging.Common {
    using System.Collections.Generic;
    using System.Security.AccessControl;
    using System.Security.Principal;
    using CoApp.Toolkit.Win32;
    using System;
    using System.IO;
    using System.Linq;
    using CoApp.Toolkit.Configuration;
    using CoApp.Toolkit.Extensions;
    using CoApp.Packaging.Common.Exceptions;

    /// <summary>
    /// Provides access to settings of the package manager.
    /// </summary>
    /// <remarks></remarks>
    public class PackageManagerSettings {
        /// <summary>
        /// Registry view for the package manager settings
        /// </summary>
        public static RegistryView CoAppSettings = RegistryView.CoAppSystem[@"PackageManager"];

        /// <summary>
        /// Registry view for the volatile information key
        /// </summary>
        public static RegistryView CoAppInformation = RegistryView.CoAppSystem[@"Information"];

        /// <summary>
        /// registry view for the cached items (contents subject to being dropped at a whim)
        /// </summary>
        public static RegistryView CacheSettings = CoAppSettings[@".cache"];

        /// <summary>
        /// registry view for package-specific information.
        /// 
        /// This data is currently the only registry data in coapp that can't be rebuilt--this stores the "current" version of a given package.
        /// This is also where we will store flags like "blocked" or "required" 
        /// </summary>
        public static RegistryView PerPackageSettings = CoAppSettings[@".packageInformation"];

        /// <summary>
        /// registry view for feed-specific information.
        /// </summary>
        public static RegistryView PerFeedSettings = CoAppSettings[@".feedInformation"];


        /// <summary>
        /// Gets the default for the CoApp root folder.
        /// </summary>
        /// <remarks></remarks>
        private static string DefaultCoappRoot { get {return KnownFolders.GetFolderPath(KnownFolder.CommonApplicationData);} }

        static PackageManagerSettings() {
            CoAppInformation.IsVolatile = true;
#if COAPP_ENGINE_CORE
            // on startup of the engine, we wipe the contents of this key.
            WipeCoAppInformation();
#endif
        }

#if COAPP_ENGINE_CORE
        internal static void WipeCoAppInformation() {
            var keys = CoAppInformation.Subkeys;
            foreach(var key in keys) {
                CoAppInformation.DeleteSubkey(key);
            }
            CoAppInformation.DeleteValues();
        }
#endif 

        /// <summary>
        /// Gets or sets the coapp root directory.
        /// 
        /// May only change the value if the existing directory is empty.
        /// 
        /// If the directory can not be set, this will default to the DEFAULT_COAPP_ROOT location every time.
        /// </summary>
        /// <value>The coapp root directory.</value>
        /// <remarks></remarks>
        public static string CoAppRootDirectory {
            get {
                // string result = SystemStringSetting["RootDirectory"];
                var result = CoAppSettings["#Root"].StringValue;

                if (string.IsNullOrEmpty(result)) {
                    CoAppRootDirectory = result = DefaultCoappRoot;
                }

                if (!Directory.Exists(result)) {
                    throw new ConfigurationException("CoApp Root Directory does not exist", "RootDirectory",
                        "The Directory [{0}] did not get created.".format(result));
                }

                return result;
            }
            set {
                var newRootDirectory = value.GetFullPath();

                var rootDirectory = CoAppSettings["#Root"].StringValue;

                if (string.IsNullOrEmpty(rootDirectory)) {
                    rootDirectory = DefaultCoappRoot;
                }

                if (rootDirectory.Equals(newRootDirectory, StringComparison.CurrentCultureIgnoreCase)) {
                    if (!Directory.Exists(rootDirectory)) {
                        Directory.CreateDirectory(rootDirectory);
                    }
                    return;
                }

                if (Directory.Exists(rootDirectory)) {
                    if (Directory.EnumerateFileSystemEntries(rootDirectory).Count() > 0) {
                        throw new ConfigurationException(
                            "The CoApp RootDirectory can not be changed with contents in it.", "RootDirectory",
                            "Remove contents of the existing CoApp Root Directory before changing it [{0}]".format(
                                rootDirectory));
                    }

                    Directory.Delete(rootDirectory);
                }

                if (!Directory.Exists(newRootDirectory)) {
                    Directory.CreateDirectory(newRootDirectory);
                }

                // Warning: the user might not have permissions to set this value
                CoAppSettings["#RootDirectory"].StringValue = newRootDirectory;
            }
        }

        public static Dictionary<Architecture, string> _coAppInstalledDirectory;

        /// <summary>
        /// Gets the CoApp .installed directory (where the packages install to)
        /// </summary>
        /// <remarks></remarks>
        public static Dictionary<Architecture, string> CoAppInstalledDirectory {
            get {
                if (_coAppInstalledDirectory == null) {
                    var programFilesAny = KnownFolders.GetFolderPath(KnownFolder.ProgramFiles);
                    var programFilesX86 = KnownFolders.GetFolderPath(KnownFolder.ProgramFilesX86) ?? programFilesAny;

                    var any = Path.Combine(CoAppRootDirectory, "program files");
                    var x86  = Path.Combine(CoAppRootDirectory, "program files (x86)");
                    var x64  = Path.Combine(CoAppRootDirectory, "program files (x64)");

                    Symlink.MakeDirectoryLink(x86, programFilesX86);
                    Symlink.MakeDirectoryLink(any, programFilesAny);
                    if (Environment.Is64BitOperatingSystem) {
                        Symlink.MakeDirectoryLink(x64, programFilesAny);
                    }

                    _coAppInstalledDirectory = new Dictionary<Architecture, string> {
                        {Architecture.Any, any},
                        {Architecture.x86, x86},
                        {Architecture.x64, x64},
                    };
                }

                return _coAppInstalledDirectory;
            }
        }

        /// <summary>
        /// Gets the co app cache directory (where transient files are located).
        /// </summary>
        /// <remarks></remarks>
        public static string CoAppCacheDirectory {
            get {
                var result = Path.Combine(CoAppRootDirectory, ".cache");
                if (!Directory.Exists(result)) {
                    Directory.CreateDirectory(result);
                    var di = new DirectoryInfo(result);
                    di.Attributes = FileAttributes.Hidden;
                    var acl = di.GetAccessControl();
                    acl.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier(WellKnownSidType.AuthenticatedUserSid, null), FileSystemRights.Modify | FileSystemRights.CreateDirectories | FileSystemRights.CreateFiles, InheritanceFlags.ObjectInherit, PropagationFlags.InheritOnly, AccessControlType.Allow));
                    di.SetAccessControl(acl);
                }
                return result;
            }
        }

        /// <summary>
        /// Gets the coapp package cache.
        ///  
        /// Not currently used--this is where we could copy MSIs that we've installed 
        /// This may be necessary on XP, where the OS doesn't store the complete MSI.
        /// </summary>
        /// <remarks></remarks>
        public static string CoAppPackageCache {
            get {
                var result = Path.Combine(CoAppCacheDirectory, "packages");
                if (!Directory.Exists(result)) {
                    Directory.CreateDirectory(result);
                    var di = new DirectoryInfo(result);
                    di.Attributes = FileAttributes.Hidden;
                    var acl = di.GetAccessControl();
                    acl.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier(WellKnownSidType.AuthenticatedUserSid, null), FileSystemRights.Modify | FileSystemRights.CreateDirectories | FileSystemRights.CreateFiles, InheritanceFlags.ObjectInherit, PropagationFlags.InheritOnly, AccessControlType.Allow));
                    di.SetAccessControl(acl);
                }
                return result;
            }
        }
    }
}