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

namespace CoApp.Packaging.Client.UI {
    using System;

    [Flags]
    public enum InstallChoice {
        _Unknown = 0,
        // this choice is informational, not valid
        _InvalidChoice = 0x10000000,

        // flags indicating that during the install, the package installed should be marked.
        _DoNotUpdate = 0x00010000,
        _DoNotUpgrade = 0x00020000,

        // flags indicating which actual package to install
        _InstallSpecificVersion = 0x00000001,
        _InstallLatestUpdate = 0x00000002,
        _InstallLatestUpgrade = 0x00000004,

        _Scenario1 = 0x01000000,
        _Scenario2 = 0x02000000,
        _Scenario3 = 0x04000000,
        _Scenario4 = 0x08000000,

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
        ThisVersionAlreadyInstalled = _InvalidChoice | 0x003
    }
}