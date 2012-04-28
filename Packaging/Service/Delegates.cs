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
    using Toolkit.Collections;
    using Common;
    using System;

    internal delegate object GetSessionCache(Type type, Func<object> constructor);
    internal delegate IPackageManagerResponse GetResponseInterface();
    internal delegate EasyDictionary<string, PackageRequestData> GetRequestPackageDataCache();
    internal delegate bool CheckForPermission(PermissionPolicy policy);
    internal delegate string GetCanonicalizedPath(string path);
    internal delegate string GetCurrentRequestId();
    internal delegate void IndividualProgress(int percentComplete);
    internal delegate bool IsCancellationRequested();
}