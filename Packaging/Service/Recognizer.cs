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
    using System.IO;
    using System.Threading.Tasks;
    using Common;
    using Common.Model.Atom;
    using PackageFormatHandlers;
    using Toolkit.Extensions;
    using Toolkit.Logging;
    using Toolkit.Tasks;

    internal class Recognizer {
        private static Task<RecognitionInfo> CacheAndReturnTask(string itemPath, RecognitionInfo recognitionInfo) {
            lock (typeof(Recognizer)) {
                return (SessionData.Current.RecognitionInfo[itemPath] = recognitionInfo).AsResultTask();
            }
        }

        private static RecognitionInfo Cache(string itemPath, RecognitionInfo recognitionInfo) {
            lock (typeof(Recognizer)) {
                return SessionData.Current.RecognitionInfo[itemPath] = recognitionInfo;
            }
        }

        internal static Task<RecognitionInfo> Recognize(string item, bool forceRescan = false) {
            var cachedResult = SessionData.Current.RecognitionInfo[item];

            if (cachedResult != null) {
                if (forceRescan) {
                    SessionData.Current.RecognitionInfo[item] = null;
                } else {
                    return cachedResult.AsResultTask();
                }
            }
            
            try {
                var location = new Uri(item);
                if (!location.IsFile) {
                    // some sort of remote item.
                    // since we can't do anything with a remote item directly, 
                    // we have to issue a request to the client to get it for us

                    // before we go down this, check to see if we asked for it in this session 
                    // in the last five minutes or so. We don't need to pound away at a URL for
                    // no reason.
                    var peek = SessionData.Current.RecognitionInfo[location.AbsoluteUri];
                    if( peek != null ) {
                        if( DateTime.Now.Subtract(peek.LastAccessed) < new TimeSpan(0,5,0) ) {
                            return peek.AsResultTask();
                        }
                    }

                    // since we're expecting that the canonicalname will be used as a filename 
                    // in the .cache directory, we need to generate a safe filename based on the 
                    // data in the URL
                    var safeCanonicalName = location.GetLeftPart(UriPartial.Path).MakeSafeFileName();

                    return SessionData.Current.RequireRemoteFile(safeCanonicalName, location.SingleItemAsEnumerable(), PackageManagerSettings.CoAppPackageCache, forceRescan, state => {
                        if (state == null || string.IsNullOrEmpty(state.LocalLocation)) {
                            // didn't fill in the local location? -- this happens when the client can't download.
                            return Cache(location.AbsoluteUri, new RecognitionInfo {
                                FullPath = location.AbsoluteUri,
                                FullUrl = location,
                                IsURL = true,
                                IsInvalid = true,
                            });
                        }

                        var newLocation = new Uri(state.LocalLocation);
                        if (newLocation.IsFile) {
                            var continuedResult = Recognize(state.LocalLocation).Result;

                            // create the result object 
                            var result = new RecognitionInfo {
                                FullUrl = location,
                            };

                            result.CopyDetailsFrom(continuedResult);
                            result.IsURL = true;

                            return Cache(item, result);
                        }

                        // so, the callback comes, but it's not a file. 
                        // session cache it 
                        return Cache(location.AbsoluteUri,new RecognitionInfo {
                            FullPath = location.AbsoluteUri,
                            IsInvalid = true,
                        });
                    }) as Task<RecognitionInfo>;
                }

                //----------------------------------------------------------------
                // we've managed to find a file system path.
                // let's figure out what it is.
                var localPath = location.LocalPath;

                if (localPath.Contains("?") || localPath.Contains("*")) {
                    // looks like a wildcard package feed.
                    // which is a directory feed with a filter.
                    var i = localPath.IndexOfAny(new[] {'*', '?'});

                    var lastSlash = localPath.LastIndexOf('\\', i);
                    var folder = localPath.Substring(0, lastSlash);
                    if (Directory.Exists(folder)) {
                        return CacheAndReturnTask(item, new RecognitionInfo {
                            FullPath = folder,
                            Filter = localPath.Substring(lastSlash + 1),
                            IsFolder = true,
                            IsPackageFeed = true
                        });
                    }
                }

                if (Directory.Exists(localPath)) {
                    // it's a directory.
                    // which means that it's a package feed.
                    return CacheAndReturnTask(item, new RecognitionInfo {
                        FullPath = localPath,
                        Filter = "*.msi", // TODO: evenutally, we have to expand this to detect other types.
                        IsFolder = true,
                        IsPackageFeed = true,
                    });
                }

                if (File.Exists(localPath)) {
                    var ext = Path.GetExtension(localPath);
                    var result = new RecognitionInfo {
                        IsFile = true,
                        FullPath = localPath
                    };

                    switch (ext) {
                        case ".msi":
                            result.IsCoAppMSI = CoAppMSI.IsValidPackageFile(localPath);
                            result.IsLegacyMSI = !result.IsCoAppMSI;
                            result.IsPackageFile = true;
                            break;

                        case ".nupkg":
                            result.IsNugetPackage = true;
                            result.IsPackageFile = true;
                            break;

                        case ".exe":
                            result.IsLegacyEXE = true;
                            result.IsPackageFile = true;
                            break;

                        case ".zip":
                        case ".cab":
                        case ".rar":
                        case ".7z":
                            result.IsArchive = true;
                            result.IsPackageFeed = true;
                            break;

                        default:
                            // guess based on file contents
                            try {
                                if (CoAppMSI.IsValidPackageFile(localPath)) {
                                    result.IsCoAppMSI = true;
                                    result.IsPackageFile = true;
                                }
                            } catch {
                                // not a coapp file...
                            }

                            if (localPath.IsXmlFile()) {
                                try {
                                    // this could be an atom feed
                                    var feed = AtomFeed.LoadFile(localPath);
                                    if (feed != null) {
                                        result.IsPackageFeed = true;
                                        result.IsAtom = true;
                                    }
                                } catch {
                                    // can't seem to figure out what this is. 
                                    result.IsInvalid = true;
                                }
                            }
                            break;
                    }
                    return CacheAndReturnTask(item, result);
                }
            } catch (UriFormatException) {
            }
            // item wasn't able to match any known URI, UNC or Local Path format.
            // or was file not found
            return new RecognitionInfo {
                FullPath = item,
                IsInvalid = true,
            }.AsResultTask();
        }

        #region Nested type: RecognitionInfo

        internal class RecognitionInfo {
            internal DateTime LastAccessed = DateTime.Now;
            internal string Filter { get; set; }
            internal string FullPath { get; set; }

            internal Uri FullUrl { get; set; }

            internal bool IsUnknown {
                get {
                    return !(IsPackageFeed | IsPackageFile);
                }
            }

            internal bool IsInvalid { get; set; }

            internal bool IsPackageFile { get; set; }
            internal bool IsPackageFeed { get; set; }

            internal bool IsURL { get; set; }
            internal bool IsFile { get; set; }
            internal bool IsFolder { get; set; }

            internal bool IsMSI {
                get {
                    return IsCoAppMSI | IsLegacyMSI;
                }
            }

            internal bool IsCoAppMSI { get; set; }
            internal bool IsLegacyMSI { get; set; }
            internal bool IsLegacyEXE { get; set; }

            internal bool IsNugetPackage { get; set; }
            internal bool IsOpenwrapPackage { get; set; }

            internal bool IsArchive { get; set; }
            internal bool IsAtom { get; set; }

            internal bool IsCoAppODataService { get; set; }
            internal bool IsNugetODataService { get; set; }


            internal void CopyDetailsFrom(RecognitionInfo fileInfo) {
                FullPath = fileInfo.FullPath;
                IsInvalid = fileInfo.IsInvalid;
                IsPackageFile = fileInfo.IsPackageFile;
                IsPackageFeed = fileInfo.IsPackageFeed;
                IsCoAppMSI = fileInfo.IsCoAppMSI;
                IsLegacyMSI = fileInfo.IsLegacyMSI;
                IsLegacyEXE = fileInfo.IsLegacyEXE;
                IsNugetPackage = fileInfo.IsNugetPackage;
                IsOpenwrapPackage = fileInfo.IsOpenwrapPackage;
                IsArchive = fileInfo.IsArchive;
                IsAtom = fileInfo.IsAtom;
            }
        }

        #endregion
    }
}