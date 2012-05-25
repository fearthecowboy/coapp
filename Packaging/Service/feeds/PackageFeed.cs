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

namespace CoApp.Packaging.Service.Feeds {
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using Common;
    using Toolkit.Collections;
    using Toolkit.Extensions;
    using Toolkit.Tasks;

    /// <summary>
    ///   The common implementation of the features for a package feed.
    /// </summary>
    /// <remarks>
    /// </remarks>
    internal class PackageFeed : IComparable {
        /// <summary>
        ///   The collection of all the feeds known to the system at the current time. This indexes feeds based on the location string used to create the feed object.
        /// </summary>
        private static readonly IDictionary<string, PackageFeed> AllFeeds = new XDictionary<string, PackageFeed>();

        /// <summary>
        ///   indicates if the current feed has already scanned the contents How this is used is up to the child class.
        /// </summary>
        private bool _scanned;

        private bool _stale;

        /// <summary>
        ///   What is known about the feed's location (url, file, type of file, etc)
        /// </summary>
        internal Recognizer.RecognitionInfo RecognitionInfo;

        /// <summary>
        ///   Gets or sets the location. This can be a file, a directory or a URL.
        /// </summary>
        /// <value> The location of the feed. </value>
        /// <remarks>
        /// </remarks>
        internal string Location { get; private set; }

        /// <summary>
        ///   Gets or sets a value indicating whether this <see cref="PackageFeed" /> is scanned.
        /// </summary>
        /// <value> <c>true</c> if scanned; otherwise, <c>false</c> . </value>
        /// <remarks>
        /// </remarks>
        internal bool Scanned {
            get {
                return _scanned;
            }
            set {
                _scanned = value;
                PackageManagerImpl.Instance.Updated();
            }
        }

        internal virtual bool Stale {
            get {
                return _stale || DateTime.Now.Subtract(LastScanned) >= new TimeSpan(12, 0, 0);
            }
            set {
                if (value == false) {
                    LastScanned = DateTime.Now;
                }
                _stale = value;
            }
        }

        /// <summary>
        ///   Initializes a new instance of the <see cref="PackageFeed" /> class.
        /// </summary>
        /// <param name="location"> The location. </param>
        /// <remarks>
        /// </remarks>
        protected PackageFeed(string location) {
            Location = location;
        }

        #region IComparable Members

        /// <summary>
        ///   Compares the current instance with another object of the same type and returns an integer that indicates whether the current instance precedes, follows, or occurs in the same position in the sort order as the other object.
        /// </summary>
        /// <param name="obj"> An object to compare with this instance. </param>
        /// <returns> A value that indicates the relative order of the objects being compared. The return value has these meanings: Value Meaning Less than zero This instance is less than <paramref
        ///    name="obj" /> . Zero This instance is equal to <paramref name="obj" /> . Greater than zero This instance is greater than <paramref
        ///    name="obj" /> . </returns>
        /// <exception cref="T:System.ArgumentException">
        ///   <paramref name="obj" />
        ///   is not the same type as this instance.</exception>
        /// <remarks>
        /// </remarks>
        public int CompareTo(object obj) {
            if (RecognitionInfo.IsURL == ((PackageFeed)obj).RecognitionInfo.IsURL) {
                return 0;
            }

            if (RecognitionInfo.IsURL) {
                return 1;
            }

            return -1;
        }

        #endregion

        /// <summary>
        ///   Gets the package feed from the location. This will first attempt to look up a matching instance in the AllFeeds collection (so that multiple requests for the same feed return a single object) It asks the recognizer to identify the location, and creates a specific subclass instance based on the results. If it cannot identify or read the target, the task will return null.
        /// </summary>
        /// <param name="location"> The feed location (url, file, directory). </param>
        /// <returns> A Task with a return value of the PackageFeed. May be null if invalid. </returns>
        /// <remarks>
        /// </remarks>
        internal static Task<PackageFeed> GetPackageFeedFromLocation(string location) {
            if (InstalledPackageFeed.CanonicalLocation.Equals(location, StringComparison.CurrentCultureIgnoreCase)) {
                return ((PackageFeed)InstalledPackageFeed.Instance).AsResultTask();
            }

            if (SessionPackageFeed.CanonicalLocation.Equals(location, StringComparison.CurrentCultureIgnoreCase)) {
                return ((PackageFeed)SessionPackageFeed.Instance).AsResultTask();
            }

            if (PackageManagerSettings.PerFeedSettings[location.UrlEncodeJustBackslashes(), "state"].GetEnumValue<FeedState>() == FeedState.Ignored) {
                return ((PackageFeed)null).AsResultTask();
            }

            return Recognizer.Recognize(location).ContinueWith(antecedent => {
                var info = antecedent.Result;
                PackageFeed result = null;

                string locationKey = null;

                if (info.IsPackageFeed) {
                    if (info.IsFolder) {
                        locationKey = Path.Combine(info.FullPath, info.Filter);

                        if (AllFeeds.ContainsKey(locationKey)) {
                            return AllFeeds[locationKey];
                        }

                        result = new DirectoryPackageFeed(info.FullPath, info.Filter);
                    } else if (info.IsFile) {
                        if (AllFeeds.ContainsKey(info.FullPath)) {
                            return AllFeeds[info.FullPath];
                        }

                        if (info.IsAtom) {
                            result = new AtomPackageFeed(info.FullPath);
                        }

                        /*
                        if (info.IsArchive) {
                            result = new ArchivePackageFeed(info.FullPath);
                        }
                         * */
                    }
                        // TODO: URL based feeds
                    else if (info.IsURL) {
                        if (AllFeeds.ContainsKey(info.FullUrl.AbsoluteUri)) {
                            return AllFeeds[info.FullUrl.AbsoluteUri];
                        }

                        if (info.IsAtom) {
                            result = new AtomPackageFeed(info.FullUrl, info.FullPath);
                        }
                    }
                } else if (info.IsPackageFile) {
                    // SessionPackageFeed.Instance.Add(Package.GetPackageFromFilename(info.FullPath));

                    result = new DirectoryPackageFeed(Path.GetDirectoryName(info.FullPath), Path.GetFileName(info.FullPath));

                    // Hack of the day:
                    // Since, I have to look up this file as a feed again later, based on the original path (likely, an http:// location)
                    // we have to forcably set the location in the feed itself to reflect this.
                    result.Location = location;
                    locationKey = location;
                }

                if (result != null) {
                    result.RecognitionInfo = info;
                    lock (AllFeeds) {
                        if (!AllFeeds.ContainsKey(locationKey ?? result.Location)) {
                            AllFeeds.Add(locationKey ?? result.Location, result);
                        } else {
                            result = AllFeeds[locationKey ?? result.Location];
                        }
                        // GS01: TODO: This is a crappy way of avoiding a deadlock when the same feed has been requested twice by two different threads.
                    }
                }

                return result;
            }, TaskContinuationOptions.AttachedToParent);
        }

        internal FeedState FeedState { get {
            return PackageManagerSettings.PerPackageSettings[Location, "state"].GetEnumValue<FeedState>(); 
        }} 

        internal bool IsLocationMatch(IEnumerable<string> locations) {
            return locations.Any(IsLocationMatch);
        }

        internal bool IsLocationMatch(string location) {
            return location.Equals(Location, StringComparison.CurrentCultureIgnoreCase);
        }

        /// <summary>
        ///   Finds the packages.
        /// </summary>
        /// <param name="canonicalName"> </param>
        /// <returns> </returns>
        /// <remarks>
        /// </remarks>
        internal virtual IEnumerable<Package> FindPackages(CanonicalName canonicalName) {
            throw new NotImplementedException();
        }

        internal DateTime LastScanned = DateTime.MinValue;

        protected HashSet<long> Cache {
            get {
                lock (typeof (PackageFeed)) {
                    if (_nonCoAppMSIFiles != null) {
                        return _nonCoAppMSIFiles;
                    }

                    _nonCoAppMSIFiles = new HashSet<long>();

                    var cache = PackageManagerSettings.CacheSettings["#nonCoAppPackageMap"].BinaryValue.ToArray();
                    if (!cache.IsNullOrEmpty()) {
                        using (var ms = new MemoryStream(cache)) {
                            var binaryReader = new BinaryReader(ms);
                            var count = binaryReader.ReadInt32();
                            for (var i = 0; i < count; i++) {
                                var value = binaryReader.ReadInt64();
                                if (!_nonCoAppMSIFiles.Contains(value)) {
                                    _nonCoAppMSIFiles.Add(value);
                                }
                            }
                        }
                    }
                    return _nonCoAppMSIFiles;
                }
            }
        }

        protected void SaveCache() {
            lock (typeof (PackageFeed)) {
                using (var ms = new MemoryStream()) {
                    var binaryWriter = new BinaryWriter(ms);

                    // order of the following is very important.
                    binaryWriter.Write(_nonCoAppMSIFiles.Count);
                    foreach (var val in _nonCoAppMSIFiles) {
                        binaryWriter.Write(val);
                    }

                    PackageManagerSettings.CacheSettings["#nonCoAppPackageMap"].BinaryValue = ms.GetBuffer();
                }
            }
        }

        private HashSet<long> _nonCoAppMSIFiles;
    }
}