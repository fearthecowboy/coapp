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

namespace CoApp.Service {
    using System.Configuration.Install;
    using System.Diagnostics;
    using System.IO;
    using System.Reflection;
    using System.ServiceProcess;
    using Packaging.Common;
    using Packaging.Service;
    using Toolkit.Win32;

    public class CoAppService : ServiceBase {
        public CoAppService() {
            ServiceName = EngineServiceManager.CoAppServiceName;
        }

        protected override void OnStart(string[] args) {
            EngineService.Start(false);
        }

        protected override void OnStop() {
            EngineService.RequestStop();
        }

        public static void Uninstall() {
            if (EngineServiceManager.IsServiceInstalled) {
                // fyi, this will fail in an interesting way if the MMC is running
                // it won't completely uninstall the service, it'll mark it as deleted.
                // we should encourage the closing of MMC here before uninstalling :D
                // http://projectdream.org/wordpress/2007/05/30/the-specified-service-has-been-marked-for-deletion/

                ManagedInstallerClass.InstallHelper(new[] {
                    "/u", Assembly.GetEntryAssembly().Location
                });
            }
        }

        public static void Install(string username = null, string password = null) {
            if (!EngineServiceManager.IsServiceInstalled) {
                //http://arcanecode.com/2007/05/23/windows-services-in-c-adding-the-installer-part-3/
                ManagedInstallerClass.InstallHelper(string.IsNullOrEmpty(username)
                    ? new[] {
                        Assembly.GetEntryAssembly().Location
                    }
                    : new[] {
                        "/username=" + username, "/password=" + password, Assembly.GetEntryAssembly().Location
                    });
            }
        }

        public static int AutoInstall() {
            if (EngineServiceManager.IsServiceInstalled) {
                EngineServiceManager.TryToStartService();
                return 0;
            }

            var serviceExe = EngineServiceManager.CoAppServiceExecutablePath;
            if (serviceExe != null) {
                if (AutoInstallService(serviceExe)) {
                    return 0;
                }
            }

            return 1;
        }

        private static bool AutoInstallService(string path) {
            if (!File.Exists(path)) {
                return false;
            }

            var root = PackageManagerSettings.CoAppRootDirectory;
            var coappBinDirectory = Path.Combine(root, "bin");
            if (!Directory.Exists(coappBinDirectory)) {
                Directory.CreateDirectory(coappBinDirectory);
            }
            var canonicalServiceExePath = Path.Combine(coappBinDirectory, "coapp.service.exe");

            if (Symlink.IsSymlink(path)) {
                // we found a symlink,
                if (!File.Exists(Symlink.GetActualPath(path))) {
                    // it is invalid anyway. trash it, try again.
                    Symlink.DeleteSymlink(path);
                }
                return false;
            }

            try {
                Symlink.MakeFileLink(canonicalServiceExePath, path);

                // hey we found one where it's supposed to be!
                var processStartInfo = new ProcessStartInfo(canonicalServiceExePath) {
                    Arguments = "--start",
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    UseShellExecute = false,
                };

                var process = Process.Start(processStartInfo);
                process.WaitForExit();

                // after it exits, lets see if we've got an installed service
                if (EngineServiceManager.IsServiceInstalled) {
                    // YAY!. We're outta here!
                    EngineServiceManager.TryToStartService();
                    return true;
                }
            } catch {
                // hmm. not working...
            }
            return false;
        }
    }
}