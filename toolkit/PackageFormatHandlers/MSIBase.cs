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
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Data.Common;
    using System.IO;
    using System.Linq;
    using Engine;
    using Engine.Exceptions;
    using Extensions;
    using Logging;
    using Microsoft.Deployment.WindowsInstaller;
    using Tasks;


    internal class MsiProperties : Dictionary<string, string> {
        internal string Filename { get; private set; }
        internal MsiProperties(string fileName ) {
            Filename = fileName;
        }
    }

    /// <summary>
    /// The base class for MSI based packages
    /// </summary>
    /// <remarks></remarks>
    internal class MSIBase : IPackageFormatHandler {
        /// <summary>
        /// Initializes a new instance of the <see cref="T:System.Object"/> class.
        /// </summary>
        /// <remarks></remarks>
        static MSIBase() {
            SetUIHandlersToSilent();
        }

        /// <summary>
        /// Sets the MSI UI handlers to silent.
        /// </summary>
        /// <remarks></remarks>
        protected static void SetUIHandlersToSilent() {
            Installer.SetInternalUI(InstallUIOptions.Silent);
            Installer.SetExternalUI(ExternalUI, InstallLogModes.Verbose);
        }

        /// <summary>
        /// silently handle the External UI for an MSI
        /// </summary>
        /// <param name="messageType">Type of the message.</param>
        /// <param name="message">The message.</param>
        /// <param name="buttons">The buttons.</param>
        /// <param name="icon">The icon.</param>
        /// <param name="defaultButton">The default button.</param>
        /// <returns></returns>
        /// <remarks></remarks>
        public static MessageResult ExternalUI(InstallMessage messageType, string message, MessageButtons buttons, MessageIcon icon,
            MessageDefaultButton defaultButton) {
            return MessageResult.OK;
        }

        /*
        /// <summary>
        /// Gets the package composition rules for the given package.
        /// </summary>
        /// <param name="package">The package.</param>
        /// <returns></returns>
        /// <remarks></remarks>
        public virtual IEnumerable<CompositionRule> GetCompositionRules(Package package) {
            throw new NotImplementedException();
        }
        */

        /// <summary>
        /// Gets the package composition data for the given package.
        /// </summary>
        /// <param name="package">The package.</param>
        /// <returns></returns>
        /// <remarks></remarks>        
        public virtual Composition GetCompositionData(Package package) {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Wrapper funciton: Determines whether the specified product code is installed.
        /// </summary>
        /// <param name="productCode">The product code.</param>
        /// <returns><c>true</c> if the specified product code is installed; otherwise, <c>false</c>.</returns>
        /// <remarks></remarks>
        public bool IsInstalled(Guid productCode) {
            try {
                lock (typeof(MSIBase)) {
                    using( var i = Installer.OpenProduct(productCode.ToString("B"))) {
                        if (i != null) {
                            i.Close();
                            return true;
                        }
                    }
                }
            }
            catch {
            }
            return false;
        }

        private static byte[] SSHeader = new byte[] { 0xd0, 0xcf, 0x11, 0xe0 };
        public static bool IsStructuredStorageFile( string path ) {
            try {
                if( File.Exists(path)) {
                    using (var f = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)) {
                        var bytes = new byte[4];
                        if (f.Read(bytes, 0, 4) == 4 && SSHeader.SequenceEqual(bytes)) {
                            return true;
                        }
                    }
                }
            } catch {
                
            }
            return false;
        }

        public static IEnumerable<string> InstalledMSIFilenames {
            get { 
                return ProductInstallation.AllProducts.Where( each => each.LocalPackage != null ).Select(each => each.LocalPackage);
            }
        }

        /*
        /// <summary>
        /// Scans Windows for all the installed MSIs.
        /// </summary>
        /// <remarks></remarks>
        public static void ScanInstalledMSIs() {
            var products = ProductInstallation.AllProducts.ToArray();
            var n = 0;
            var total = products.Count();

            foreach (var product in products) {
                var p = product;
                Package.GetPackageFromFilename(p.LocalPackage); // let the package manager figure out if this is a package we care about.
            }
        }
        */

        /// <summary>
        /// Installs the specified package.
        /// </summary>
        /// <param name="package">The package.</param>
        /// <param name="progress">The progress.</param>
        /// <remarks></remarks>
        public virtual void Install(Package package, Action<int> progress) {
            Logger.Error("YOU SHOULD NOT SEE THIS!");
            throw new NotImplementedException();
        }

        /// <summary>
        /// Removes the specified package.
        /// </summary>
        /// <param name="package">The package.</param>
        /// <param name="progress">The progress.</param>
        /// <remarks></remarks>
        public virtual void Remove(Package package, Action<int> progress) {
            Logger.Error("YOU SHOULD NOT SEE THIS!");
            throw new NotImplementedException();
        }

        /// <summary>
        /// Gets the MSI data as a stanard dataset
        /// </summary>
        /// <param name="localPackagePath">The local package path.</param>
        /// <returns></returns>
        /// <remarks></remarks>
        public static MsiProperties GetMsiProperties(string localPackagePath) {

            localPackagePath = localPackagePath.ToLower();

            try {
                var result = SessionCache<MsiProperties>.Value[localPackagePath];
                if (result != null) {
                    return result;
                }
            }
            catch {
                // no worry.
            }

            try {
                lock (typeof (MSIBase)) {
                    using (var database = new Database(localPackagePath, DatabaseOpenMode.ReadOnly)) {
                        using (var view = database.OpenView("SELECT Property, Value FROM Property")) {
                            view.Execute();

                            var result = new MsiProperties(localPackagePath);

                            foreach (var each in view) {
                                result.Add(each["Property"].ToString(), each["Value"].ToString());
                            }

                            try {
                                //  GS01: this seems hinkey too... the local package is sometimes getting added twice. prollly a race condition somewhere.
                                // if (SessionCache<MsiProperties>.Value[localPackagePath] != null) {
                                   //  return SessionCache<MsiProperties>.Value[localPackagePath];
                                // }
                                SessionCache<MsiProperties>.Value[localPackagePath] = result;
                            }
                            catch {

                            }
                            return result;
                        }
                    }
                }
            }
            catch (InstallerException) {
                throw new InvalidPackageException(InvalidReason.NotValidMSI, localPackagePath);
            }
        }
    }
}
