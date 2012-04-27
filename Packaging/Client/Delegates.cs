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

namespace CoApp.Packaging.Client {
    using Common;
    using Toolkit.Pipes;

    public delegate void PackageInstallProgress(CanonicalName packageCanonicalName, int progress, int overallProgress);

    public delegate void PackageRemoveProgress(CanonicalName packageCanonicalName, int progress);

    public delegate void DownloadCompleted(string remoteLocation, string localLocation);

    public delegate void DownloadProgress(string remoteLocation, string localLocation, int progress);

    public delegate void PackageRemoved(CanonicalName canonicalName);

    public delegate void PackageInstalled(CanonicalName canonicalName);

    public delegate void UnableToDownloadPackage(CanonicalName canonicalName);

    public delegate IncomingCallDispatcher<IPackageManagerResponse> GetResponseDispatcher();

    internal delegate string GetCurrentRequestId();
}