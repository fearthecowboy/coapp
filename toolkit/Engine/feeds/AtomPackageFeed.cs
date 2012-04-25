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

namespace CoApp.Toolkit.Engine.Feeds {
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.ServiceModel.Syndication;
    using System.Threading;
    using System.Threading.Tasks;
    using Extensions;
    using Logging;
    using Model;
    using Model.Atom;
    using Tasks;
    using Toolkit.Exceptions;

    /// <summary>
    /// A package feed represented by a atom XML file
    /// 
    /// This may be a remote URL or a local xml file.
    /// </summary>
    /// <remarks></remarks>
    internal class AtomPackageFeed : PackageFeed {
        /// <summary>
        /// the remote location this feed is coming from. 
        /// 
        /// May be null if this file is local to start with
        /// </summary>
        private readonly Uri _remoteLocation;

        /// <summary>
        /// the local file on disk that contains the xml feed.
        /// </summary>
        private string _localLocation;

        /// <summary>
        /// the collection of packages found in this feed.
        /// </summary>
        private readonly List<Package> _packageList = new List<Package>();

        /// <summary>
        /// Initializes a new instance of the <see cref="PackageFeed"/> class.
        /// </summary>
        /// <param name="location">The local file location of the package feed.
        /// </param>
        /// <remarks></remarks>
        internal AtomPackageFeed(string location) : base(location) {
             _localLocation = location.CanonicalizePathIfLocalAndExists();
            if( string.IsNullOrEmpty(_localLocation) ) {
                // perhaps this is a remote file
                try {
                    var uri = new Uri(location);
                    _remoteLocation = uri;
                } catch {
                    throw new CoAppException("Invalid Atom Feed Location");
                }
            } 
        }
  
        /// <summary>
        /// Initializes a new instance of the <see cref="AtomPackageFeed"/> class.
        /// 
        /// From a remote URI location
        /// </summary>
        /// <param name="location">The URL of the remote package feed..</param>
        internal AtomPackageFeed(Uri location, string localFile = null) : this(location.AbsoluteUri) {
            if (localFile.FileIsLocalAndExists()) {
                _localLocation = localFile;
                Stale = false; // we just got the file, it's not stale. 
            }
        }

        internal static PackageDetails ReadPackageDetails(Uri remoteLocation, string localLocation, Package package) {
            return null;
        }

        private Task<bool> EnsureFileIsLocal() {
            if (!Stale && _localLocation.FileIsLocalAndExists()) {
                return true.AsResultTask();
            }

            if (_remoteLocation != null) {
                // do the remote thing (force the recognizer to acquire and load the file)
                lock (this) {
                    return Recognizer.Recognize(_remoteLocation.AbsoluteUri, Stale).ContinueWith(antecedent => {
                        if (antecedent.IsFaulted || antecedent.IsCanceled) {
                            return false;
                        }
                        if (antecedent.Result != null && antecedent.Result.IsAtom) {
                            _localLocation = antecedent.Result.FullPath;
                            return true;
                        }
                        return false;
                    }, TaskContinuationOptions.AttachedToParent);
                }
            }
            return false.AsResultTask();
        }

        /// <summary>
        /// Iterates thru the list of atom feed items and creates package representations of each item.
        /// </summary>
        /// <remarks></remarks>
        protected void Scan() {
            lock (this) {
                if (!Scanned || Stale) {
                    // bring the file local first
                    EnsureFileIsLocal().ContinueWith(
                        antecedent => {
                            if (antecedent.IsFaulted || antecedent.IsCanceled || !antecedent.Result) {
                                Scanned = false;
                                LastScanned = DateTime.MinValue;
                                return false;
                            }
                            Stale = false;

                            // we're good to load the file from the _localLocation
                            var feed = AtomFeed.LoadFile(_localLocation);

                            // since AtomFeeds are so nicely integrated with Package now, we can just get the packages from there :)
                            _packageList.AddRange(feed.Packages);

                            Scanned = true;
                            LastScanned = DateTime.Now;
                            return true;
                        }, TaskContinuationOptions.AttachedToParent).Wait(); // block on this actually finishing for now.
                }
            }
        }

        internal override IEnumerable<Package> FindPackages(string name, string version, string arch, string publicKeyToken) { 
            Scan();
            return from p in _packageList where
                (string.IsNullOrEmpty(name) || p.Name.IsWildcardMatch(name)) &&
                (string.IsNullOrEmpty(version) || p.Version.ToString().IsWildcardMatch(version)) &&
                (string.IsNullOrEmpty(arch) || p.Architecture.ToString().IsWildcardMatch(arch)) &&
                (string.IsNullOrEmpty(publicKeyToken) || p.PublicKeyToken.IsWildcardMatch(publicKeyToken)) select p;
        }
    }
}