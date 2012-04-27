//-----------------------------------------------------------------------
// <copyright company="CoApp Project">
//     Copyright (c) 2011 Garrett Serack . All rights reserved.
// </copyright>
// <license>
//     The software is licensed under the Apache 2.0 License (the "License")
//     You may not use the software except in compliance with the License. 
// </license>
//-----------------------------------------------------------------------

namespace CoApp.Toolkit.PackageFormatHandlers {
    public class NuGet : IPackageFormatHandler {
        public void Install(Engine.Package packagePath, System.Action<int> progress = null) {
            throw new System.NotImplementedException();
        }

        public void Remove(Engine.Package packagePath, System.Action<int> progress = null) {
            throw new System.NotImplementedException();
        }

        public bool IsInstalled(string productCode) {
            throw new System.NotImplementedException();
        }


        public System.Collections.Generic.IEnumerable<Engine.CompositionRule> GetCompositionRules(Engine.Package package) {
            throw new System.NotImplementedException();
        }
    }
}
