//-----------------------------------------------------------------------
// <copyright company="CoApp Project">
//     Copyright (c) 2011 Garrett Serack. All rights reserved.
// </copyright>
// <license>
//     The software is licensed under the Apache 2.0 License (the "License")
//     You may not use the software except in compliance with the License. 
// </license>
//-----------------------------------------------------------------------


namespace CoApp.Toolkit.Engine.Client {
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Configuration;
    using System.Reflection;
    using System.Security.Cryptography;
    using System.Threading.Tasks;
    using CoApp.Toolkit.Exceptions;
    using CoApp.Toolkit.Extensions;
    using CoApp.Toolkit.Tasks;
    using Logging;

    public delegate void RemoteFileFailed(Uri remoteLocation);

    public delegate void RemoteFileCompleted(Uri remoteLocation);

    public delegate void RemoteFileProgress(Uri remoteLocation, int percentComplete);

    public class RemoteFile {
        private const int BufferSize = 32768;

        public static IEnumerable<string> ServerSideExtensions = new[] {"asp", "aspx", "php", "jsp", "cfm"};

        protected Uri ActualRemoteLocation { get; set; }
        private FileStream _filestream;
        public readonly Uri RemoteLocation;
        private readonly string _localDirectory;
        private string _filename;
        private bool IsCanceled = false;
        private string _fullPath;
        private DateTime _lastModified;
        private long _contentLength;
        private HttpStatusCode _lastStatus = HttpStatusCode.NotImplemented;

        static RemoteFile() {
            // we need way more concurrent connections to a remote server.
            ServicePointManager.DefaultConnectionLimit = 100;

            //Get the assembly that contains the internal class 
            Assembly aNetAssembly = Assembly.GetAssembly(typeof (SettingsSection));
            if (aNetAssembly != null) {
                //Use the assembly in order to get the internal type for the internal class 
                Type aSettingsType = aNetAssembly.GetType("System.Net.Configuration.SettingsSectionInternal");
                if (aSettingsType != null) {
                    //Use the internal static property to get an instance of the internal settings class. 
                    //If the static instance isn't created allready the property will create it for us. 
                    object anInstance = aSettingsType.InvokeMember("Section",
                        BindingFlags.Static | BindingFlags.GetProperty | BindingFlags.NonPublic, null, null, new object[] {});
                    if (anInstance != null) {
                        //Locate the private bool field that tells the framework is unsafe header parsing should be allowed or not 
                        FieldInfo aUseUnsafeHeaderParsing = aSettingsType.GetField("useUnsafeHeaderParsing",
                            BindingFlags.NonPublic | BindingFlags.Instance);
                        if (aUseUnsafeHeaderParsing != null) {
                            aUseUnsafeHeaderParsing.SetValue(anInstance, true);
                        }
                    }
                }
            }
        }

        public void Cancel() {
            IsCanceled = true;
        }

        private readonly RemoteFileCompleted _completed;
        private readonly RemoteFileFailed _failed;
        private readonly RemoteFileProgress _progress;

        public RemoteFile(string remoteLocation, string localDestination, RemoteFileCompleted completed = null, RemoteFileFailed failed = null, RemoteFileProgress progress = null)
            : this(new Uri(remoteLocation), localDestination) {
        }

        public RemoteFile(Uri remoteLocation, string localDestination, RemoteFileCompleted completed = null, RemoteFileFailed failed = null, RemoteFileProgress progress = null) {
            _completed = completed ?? (location => {
            });
            _failed = failed ?? (location => {
            });
            _progress = progress ?? ((location, complete) => {
            });

            RemoteLocation = remoteLocation;
            var destination = localDestination.CanonicalizePath();

            _localDirectory = Path.GetDirectoryName(destination);

            if (!Directory.Exists(_localDirectory)) {
                Directory.CreateDirectory(_localDirectory);
            }

            if (Directory.Exists(destination)) {
                // they just gave us the local folder where to stick the download.
                // we'll have to figure out a filename...
                _localDirectory = destination;
            } else {
                _filename = Path.GetFileName(destination);
            }
        }

        public string Filename {
            get {
                return _fullPath ?? (_fullPath = (_filename != null ? Path.Combine(_localDirectory, _filename) : null));
            }
        }

        public void Get() {
            var webRequest = (HttpWebRequest)WebRequest.Create(RemoteLocation);
            webRequest.AllowAutoRedirect = true;
            webRequest.Method = WebRequestMethods.Http.Get;
            webRequest.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
            webRequest.Timeout = 15000;

            try {
                var response = webRequest.GetResponse() as HttpWebResponse;
                if (IsCanceled || response == null) {
                    _failed(RemoteLocation);
                    return;
                }

                var status = response.StatusCode;

                if (response.StatusCode != HttpStatusCode.OK) {
                    _failed(RemoteLocation);
                    return;
                }

                var lastModified = response.LastModified;
                var contentLength = response.ContentLength;
                ActualRemoteLocation = response.ResponseUri;

                // if we don't have a destination filename yet.
                if (string.IsNullOrEmpty(_filename)) {
                    _filename = response.ContentDispositionFilename();

                    if (string.IsNullOrEmpty(_filename)) {
                        _filename = ActualRemoteLocation.LocalPath.Substring(ActualRemoteLocation.LocalPath.LastIndexOf('/') + 1);
                        if (string.IsNullOrEmpty(_filename) || ServerSideExtensions.Contains(Path.GetExtension(_filename))) {
                            ActualRemoteLocation.GetLeftPart(UriPartial.Path).MakeSafeFileName();
                        }
                    }
                }

                // if we've already got a file here, let's compare what we have and see if we should proceed.
                if (Filename.FileIsLocalAndExists()) {
                    var md5 = string.Empty;
                    try {
                        if (response.Headers.AllKeys.ContainsIgnoreCase("x-ms-meta-MD5")) {
                            // it's coming from azure, check the value of the md5 and compare against the file on disk ... better than date/size matching.
                            md5 = response.Headers["x-ms-meta-MD5"].Trim();

                        } else if (response.Headers.AllKeys.ContainsIgnoreCase("Content-MD5")) {
                            md5 = response.Headers["Content-MD5"].Trim();
                            if (md5.EndsWith("=")) {
                                md5 = Convert.FromBase64CharArray(md5.ToCharArray(), 0, md5.Length).ToUtf8String();
                            }
                        }
                    } catch {
                        // something gone screwy?
                    }

                    if (!string.IsNullOrEmpty(md5)) {
                        var localMD5 = string.Empty;
                        using (var stream = new FileStream(Filename, FileMode.Open, FileAccess.Read, FileShare.Read)) {
                            localMD5 = MD5.Create().ComputeHash(stream).ToHexString();
                        }
                        if (string.Equals(md5, localMD5, StringComparison.CurrentCultureIgnoreCase)) {
                            // it's the same file. We're not doin nothing.
                            _completed(RemoteLocation);
                            return;
                        }
                        // only do the size/date comparison if the server doesn't provide an MD5
                    } else if (contentLength > 0 && lastModified.CompareTo(File.GetCreationTime(Filename)) <= 0 && contentLength == new FileInfo(Filename).Length) {
                        // file is identical to the one on disk.
                        // we're not going to reget it. :p
                        _completed(RemoteLocation);
                        return;
                    }

                    // there was a file here, but it doesn't look like what we want.
                    Filename.TryHardToDelete();
                }

                try {
                    using (var filestream = File.Open(Filename, FileMode.Create)) {
                        if (IsCanceled) {
                            _failed(RemoteLocation);
                            return;
                        }
                        var buffer = new byte[BufferSize];
                        var totalRead = 0;
                        var bytesRead = 0;

                        using (Stream stream = response.GetResponseStream()) {
                            try {
                                do {
                                    bytesRead = stream.Read(buffer, 0, BufferSize);
                                    filestream.Write(buffer, 0, bytesRead);
                                    totalRead += bytesRead;
                                    if (contentLength > 0) {
                                        _progress(RemoteLocation, (int)((totalRead * 100) / contentLength));
                                    }
                                } while (bytesRead != 0);
                            }
                            catch (Exception e) {
                                Logger.Error(e);
                                _failed(RemoteLocation);
                            }
                        }
                    }
                }
                catch (Exception ex) {
                    // if it fails during download, then we cleanup the file too.
                    if (File.Exists(Filename)) {
                        Filename.TryHardToDelete();
                        // we should return a failure to the calling task I think.
                    }
                }
                try {
                    var fi = new FileInfo(Filename);
                    File.SetCreationTime(Filename, lastModified);
                    File.SetLastWriteTime(Filename, lastModified);

                    if (contentLength == 0) {
                        contentLength = fi.Length;
                    }
                } catch {
                    // don't care if setting the timestamps fails.
                }
                _completed(RemoteLocation);
            }
            catch (WebException ex) {
                if ((int)ex.Status != 404) {
                    Logger.Error(ex);
                }
                _failed(RemoteLocation);
            }
            catch( Exception ex ) {
                // on other errors, remove the file.
                Logger.Error(ex);
                _failed(RemoteLocation);
            }
        }

        public Task GetAsync() {
            var webRequest = (HttpWebRequest)WebRequest.Create(RemoteLocation);
            webRequest.AllowAutoRedirect = true;
            webRequest.Method = WebRequestMethods.Http.Get;
            webRequest.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
            
            return Task.Factory.FromAsync<WebResponse>(webRequest.BeginGetResponse, (Func<IAsyncResult, WebResponse>)webRequest.BetterEndGetResponse, this).ContinueWith(asyncResult => {
                // Logging.Logger.Message("In FromAsync Task::::::{0}", RemoteLocation);
                try {
                    if (IsCanceled) {
                        _failed(RemoteLocation);
                        return;
                    }

                    var httpWebResponse = asyncResult.Result as HttpWebResponse;
                    _lastStatus = httpWebResponse.StatusCode;

                    if (httpWebResponse.StatusCode == HttpStatusCode.OK) {
                        _lastModified = httpWebResponse.LastModified;
                        _contentLength = httpWebResponse.ContentLength;
                        ActualRemoteLocation = httpWebResponse.ResponseUri;

                        if (IsCanceled) {
                            _failed(RemoteLocation);
                            return;
                        }

                        if (string.IsNullOrEmpty(_filename)) {
                            _filename = httpWebResponse.ContentDispositionFilename();

                            if (string.IsNullOrEmpty(_filename)) {
                                _filename = ActualRemoteLocation.LocalPath.Substring(ActualRemoteLocation.LocalPath.LastIndexOf('/') + 1);
                                if (string.IsNullOrEmpty(_filename) || ServerSideExtensions.Contains(Path.GetExtension(_filename))) {
                                    ActualRemoteLocation.GetLeftPart(UriPartial.Path).MakeSafeFileName();
                                }
                            }
                        }

                        try {
                            if (Filename.FileIsLocalAndExists()) {
                                var md5 = string.Empty;
                                try {
                                    if (httpWebResponse.Headers.AllKeys.ContainsIgnoreCase("x-ms-meta-MD5")) {
                                        // it's coming from azure, check the value of the md5 and compare against the file on disk ... better than date/size matching.
                                        md5 = httpWebResponse.Headers["x-ms-meta-MD5"].Trim();
                                    } else if (httpWebResponse.Headers.AllKeys.ContainsIgnoreCase("Content-MD5")) {
                                        md5 = httpWebResponse.Headers["Content-MD5"].Trim();
                                        if (md5.EndsWith("=")) {
                                            md5 = Convert.FromBase64CharArray(md5.ToCharArray(), 0, md5.Length).ToUtf8String();
                                        }
                                    }
                                } catch {
                                    // something gone screwy?
                                }

                                if (!string.IsNullOrEmpty(md5)) {
                                    var localMD5 = string.Empty;
                                    using (var stream = new FileStream(Filename, FileMode.Open, FileAccess.Read, FileShare.Read)) {
                                        localMD5 = MD5.Create().ComputeHash(stream).ToHexString();
                                    }

                                    if (string.Equals(md5, localMD5, StringComparison.CurrentCultureIgnoreCase)) {
                                        // it's the same file. We're not doin nothing.
                                        _completed(RemoteLocation);
                                        return;
                                    }

                                    // only do the size/date comparison if the server doesn't provide an MD5
                                } else if (_contentLength > 0 && _lastModified.CompareTo(File.GetCreationTime(Filename)) <= 0 && _contentLength == new FileInfo(Filename).Length) {
                                    // file is identical to the one on disk.
                                    // we're not going to reget it. :p
                                    _completed(RemoteLocation);
                                    return;
                                }
                            }

                            // we should open the file here, so that it's ready when we start the async read cycle.
                            if (_filestream != null) {
                                _failed(RemoteLocation);
                                throw new CoAppException("THIS VERY BAD AND UNEXPECTED. (Failed to close?)");
                            }

                            _filestream = File.Open(Filename, FileMode.Create);

                            if (IsCanceled) {
                                _failed(RemoteLocation);
                                return;
                            }

                            var tcs = new TaskCompletionSource<HttpWebResponse>(TaskCreationOptions.AttachedToParent);
                            tcs.Iterate(AsyncReadImpl(tcs, httpWebResponse));
                            return;
                        } catch {
                            // failed to actually create the file, or some other catastrophic failure.
                            _failed(RemoteLocation);
                            return;
                        }
                    }
                    // this is not good. 
                    _failed(RemoteLocation);

                } catch (AggregateException e) {
                    _failed(RemoteLocation);
                    // at this point, we've failed somehow
                    if (_lastStatus == HttpStatusCode.NotImplemented) {
                        // we never got started. Probably not found.
                    }
                    var ee = e.Flatten();
                    foreach (var ex in ee.InnerExceptions) {
                        var wex = ex as WebException;
                        if (wex != null) {
                            Console.WriteLine("Status:" + wex.Status);
                            Console.WriteLine("Response:" + wex.Response);
                            Console.WriteLine("Response:" + ((HttpWebResponse)wex.Response).StatusCode);
                        }

                        Console.WriteLine(ex.GetType());
                        Console.WriteLine(ex.Message);
                        Console.WriteLine(ex.StackTrace);
                    }
                } catch (Exception e) {
                    Console.WriteLine(e.GetType());
                    Console.WriteLine(e.Message);
                    Console.WriteLine(e.StackTrace);
                }

            }, TaskContinuationOptions.AttachedToParent);
        }

        private IEnumerable<Task> AsyncReadImpl(TaskCompletionSource<HttpWebResponse> tcs, HttpWebResponse httpWebResponse) {
            using (var responseStream = httpWebResponse.GetResponseStream()) {
                var total = 0L;
                var buffer = new byte[BufferSize];
                while (true) {
                    if (IsCanceled) {
                        _failed(RemoteLocation);
                        tcs.SetResult(null);
                        break;
                    }

                    var read = Task<int>.Factory.FromAsync(responseStream.BeginRead, responseStream.EndRead, buffer, 0,
                        buffer.Length, this);

                    yield return read;

                    var bytesRead = read.Result;
                    if (bytesRead == 0) {
                        break;
                    }

                    total += bytesRead;

                    _progress(RemoteLocation, (int)(_contentLength <= 0 ? total : (int)(total*100/_contentLength)));

                    // write to output file.
                    _filestream.Write(buffer, 0, bytesRead);
                    _filestream.Flush();
                }
                // end of the file!
                _filestream.Close();
                _filestream = null;

                try {
                    if (IsCanceled) {
                        _failed(RemoteLocation);
                        tcs.SetResult(null);
                    } else {
                        var fi = new FileInfo(Filename);
                        File.SetCreationTime(Filename, _lastModified);
                        File.SetLastWriteTime(Filename, _lastModified);

                        if (_contentLength == 0) {
                            _contentLength = fi.Length;
                        }
                        _completed(RemoteLocation);
                        tcs.SetResult(null);
                    }
                } catch (Exception e) {
                    Console.WriteLine(e.Message);
                    tcs.SetException(e);
                }
            }
        }
    }
}