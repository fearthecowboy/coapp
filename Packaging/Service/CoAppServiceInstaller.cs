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
    using System.IO;
    using System.ComponentModel;
    using System.Configuration.Install;
    using System.ServiceProcess;
    using CoApp.Packaging.Common;

    [RunInstaller(true)]
    public class CoAppServiceInstaller : Installer {
        
        private readonly ServiceProcessInstaller _serviceProcessInstaller = new ServiceProcessInstaller();
        private readonly ServiceInstaller _serviceInstaller = new ServiceInstaller();

        public CoAppServiceInstaller() : this(false) {
            System.Environment.CurrentDirectory = System.Environment.GetEnvironmentVariable("tmp") ?? Path.Combine(System.Environment.GetEnvironmentVariable("systemroot"),"temp");
        }

        public CoAppServiceInstaller(bool useUserAccount) {
            _serviceProcessInstaller.Account = useUserAccount ? ServiceAccount.User : ServiceAccount.LocalSystem;
            _serviceProcessInstaller.Password = null;
            _serviceProcessInstaller.Username = null;

            _serviceInstaller.ServiceName = EngineServiceManager.CoAppServiceName;
            _serviceInstaller.DisplayName = EngineServiceManager.CoAppDisplayName;

            _serviceInstaller.StartType = ServiceStartMode.Automatic;

            Installers.AddRange(new Installer[] {_serviceProcessInstaller,_serviceInstaller});
        }
    }
}