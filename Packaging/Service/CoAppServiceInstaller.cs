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
    using System.ComponentModel;
    using System.Configuration.Install;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.ServiceProcess;
    using Common;

    [RunInstaller(true)]
    public class CoAppServiceInstaller : Installer {
        private readonly ServiceProcessInstaller _serviceProcessInstaller = new ServiceProcessInstaller();
        private readonly ServiceInstaller _serviceInstaller = new ServiceInstaller();

        public CoAppServiceInstaller() : this(false) {
            Environment.CurrentDirectory = Environment.GetEnvironmentVariable("tmp") ?? Path.Combine(Environment.GetEnvironmentVariable("systemroot"), "temp");
        }

        public CoAppServiceInstaller(bool useUserAccount) {
            _serviceProcessInstaller.Account = useUserAccount ? ServiceAccount.User : ServiceAccount.LocalSystem;
            _serviceProcessInstaller.Password = null;
            _serviceProcessInstaller.Username = null;
            
            _serviceInstaller.ServiceName = EngineServiceManager.CoAppServiceName;
            _serviceInstaller.DisplayName = EngineServiceManager.CoAppDisplayName;

            _serviceInstaller.StartType = ServiceStartMode.Manual;

            Installers.AddRange(new Installer[] {_serviceProcessInstaller, _serviceInstaller});

            // works around a goofy bug in the installer stack.
            int n = 0;
            foreach( var eli in FindInstaller(Installers) ) {
                eli.Source = "CoAppLog"+(++n);
            }
        }

        private IEnumerable<EventLogInstaller> FindInstaller(InstallerCollection installers) {
            foreach (Installer installer in installers) {
                var eventLogInstaller = installer as EventLogInstaller;
                if (eventLogInstaller != null) {
                    yield return eventLogInstaller;
                }

                foreach (var i in FindInstaller(installer.Installers).Where(i => i != null)) {
                    yield return i;
                }
            }
        }
    }
}