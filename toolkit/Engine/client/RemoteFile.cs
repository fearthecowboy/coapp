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

    public class UniqueInstance<T> :IDisposable where T : class {
        private static readonly Dictionary<string, T> instances = new Dictionary<string, T>();
        private string ___key;

        internal static T GetInstance(Func<T> constructor, string key ) {
            if (!instances.ContainsKey(key)) {
                var v = constructor();
                (v as UniqueInstance<T>).___key = key;
                instances.Add(key, v);
            }
            return instances[key];
        }

        protected static void Remove(string key) {
            lock (instances) {
                if( instances.ContainsKey(key)) {
                    instances.Remove(key);
                }
            }
        }

        internal static T GetInstance<T1>(Func<T1,T> constructor, T1 c1) {
            return GetInstance (() => constructor(c1), c1.ToString());
        }

        internal static T GetInstance<T1>(Func<T> constructor, T1 c1) {
            return GetInstance (constructor, c1.ToString());
        }

        internal static T GetInstance<T1,T2>(Func<T1,T2,T> constructor, T1 c1, T2 c2) {
            return GetInstance(() => constructor(c1, c2), "" + c1 + c2);
        }

        internal static T GetInstance<T1,T2>(Func<T> constructor, T1 c1, T2 c2) {
            return GetInstance(constructor, "" + c1 + c2);
        }

        internal static void Remove<T1>(T1 c1) {
            Remove( c1.ToString());
        }
        internal static void Remove<T1,T2>(T1 c1, T2 c2) {
            Remove( ""+ c1 + c2 );
        }

        public void Dispose() {
            instances.Remove(___key);
        }
    }   

    public class RemoteFileMessages:MessageHandlers<RemoteFileMessages> {
        public delegate void RemoteFileFailed(Uri remoteLocation);
        public delegate void RemoteFileCompleted(Uri remoteLocation);
        public delegate void RemoteFileProgress(Uri remoteLocation, int percentComplete);

        public RemoteFileFailed Failed;
        public RemoteFileCompleted Completed;
        public RemoteFileProgress Progress;
    }

    public class RemoteFile:UniqueInstance<RemoteFile> {
        public static IEnumerable<string> ServerSideExtensions = new[] {"asp", "aspx", "php", "jsp", "cfm"};
        private const int BUFFER_SIZE = 32768;

        private FileStream _filestream;
        public readonly Uri RemoteLocation;
        private readonly string _localDirectory;
        private string _filename;
        private Task _getTask = null;
        private bool IsCancelled = false;
        private string _fullPath;
        private DateTime _lastModified;
        private long _contentLength;
        private HttpStatusCode _lastStatus = HttpStatusCode.NotImplemented;

       static RemoteFile() {
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

        public static RemoteFile GetRemoteFile(string remoteLocation, string localDestination) {
            return GetInstance((r,l) => new RemoteFile(r, l), remoteLocation ,localDestination );
        }

        public static RemoteFile GetRemoteFile(Uri remoteLocation, string localDestination) {
            return GetInstance((r,l) => new RemoteFile(r, l), remoteLocation ,localDestination );
        }

        private RemoteFile(string remoteLocation, string localDestination) : this(new Uri(remoteLocation), localDestination) {
        }

        private RemoteFile(Uri remoteLocation, string localDestination) {
            RemoteLocation = remoteLocation;
            var destination = localDestination.CanonicalizePath();

            _localDirectory = Path.GetDirectoryName(destination);
            
            if(!Directory.Exists(_localDirectory)) {
                Directory.CreateDirectory(_localDirectory);
            }

            if(Directory.Exists(destination)) {
                // they just gave us the local folder where to stick the download.
                // we'll have to figure out a filename...
                _localDirectory = destination;
            } else {
                _filename = Path.GetFileName(destination);
            }
        }

        public bool IsFile {
            get { return RemoteLocation.IsFile; }
        }

        public bool IsInternet {
            get { return IsHttp || IsFtp; }
        }

        public bool IsHttp {
            get { return RemoteLocation.IsHttpScheme(); }
        }

        public bool IsFtp {
            get { return RemoteLocation.Scheme.Equals("ftp"); }
        }

        public string Filename {
            get {
                return _fullPath ?? (_fullPath = (_filename != null ? Path.Combine(_localDirectory, _filename) : null));
            }
        }

        public Task Get(RemoteFileMessages messages = null ) {
            lock (this) {
                if (_getTask != null && !_getTask.IsCompleted) {
                    return _getTask;
                }

                var webRequest = (HttpWebRequest) WebRequest.Create(RemoteLocation);
                webRequest.AllowAutoRedirect = true;
                webRequest.Method = WebRequestMethods.Http.Get;
                webRequest.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;

                return Task.Factory.FromAsync<WebResponse>(webRequest.BeginGetResponse, (Func<IAsyncResult, WebResponse>)webRequest.BetterEndGetResponse , this).ContinueWith(asyncResult => {
                    if (messages != null) {
                        messages.Register();
                    }
                    try {
                            if (IsCancelled) {
                                _cancel();
                            }
                            
                            var httpWebResponse = asyncResult.Result as HttpWebResponse;
                            _lastStatus = httpWebResponse.StatusCode;

                            if (httpWebResponse.StatusCode == HttpStatusCode.OK) {
                                _lastModified = httpWebResponse.LastModified;
                                _contentLength = httpWebResponse.ContentLength;
                                ActualRemoteLocation = httpWebResponse.ResponseUri;

                                if (IsCancelled) {
                                    _cancel();
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
                                    if( Filename.FileIsLocalAndExists() ) {
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

                                        if( !string.IsNullOrEmpty(md5) ) {
                                            var localMD5 = string.Empty;
                                            using( var stream = new FileStream(Filename, FileMode.Open, FileAccess.Read, FileShare.Read) ) {
                                                localMD5  = MD5.Create().ComputeHash(stream).ToHexString();
                                            }

                                            if( string.Equals(md5, localMD5, StringComparison.CurrentCultureIgnoreCase)) {
                                                // it's the same file. We're not doin nothing.
                                                RemoteFileMessages.Invoke.Completed(RemoteLocation);
                                                return;
                                            }
                                            
                                            // only do the size/date comparison if the server doesn't provide an MD5
                                        } else if (_contentLength > 0 && _lastModified.CompareTo(File.GetCreationTime(Filename)) <= 0 && _contentLength == new FileInfo(Filename).Length) {
                                            // file is identical to the one on disk.
                                            // we're not going to reget it. :p
                                            RemoteFileMessages.Invoke.Completed(RemoteLocation);
                                            return;
                                        }
                                    }

                                    // we should open the file here, so that it's ready when we start the async read cycle.
                                    if (_filestream != null) {
                                        throw new CoAppException("THIS VERY BAD AND UNEXPECTED. (Failed to close?)");
                                    }

                                    _filestream = File.Open(Filename, FileMode.Create);

                                    if (IsCancelled) {
                                        _cancel();
                                        return;
                                    }

                                    var tcs = new TaskCompletionSource<HttpWebResponse>(TaskCreationOptions.AttachedToParent);
                                    tcs.Iterate(AsyncReadImpl(tcs, httpWebResponse));
                                    return;
                                }
                                catch {
                                    // failed to actually create the file, or some other catastrophic failure.
                                    _cancel();
                                    return;
                                }
                            }
                            // this is not good. 
                        RemoteFileMessages.Invoke.Failed(RemoteLocation);
                        
                        }
                        catch (AggregateException e) {
                            RemoteFileMessages.Invoke.Failed(RemoteLocation);
                            // at this point, we've failed somehow
                            if (_lastStatus == HttpStatusCode.NotImplemented) {
                                // we never got started. Probably not found.
                            }
                            var ee = e.Flatten();
                            foreach (var ex in ee.InnerExceptions) {
                                var wex = ex as WebException;
                                if( wex != null ) {
                                    Console.WriteLine("Status:" + wex.Status);
                                    Console.WriteLine("Response:" + wex.Response);
                                    Console.WriteLine("Response:" + ((HttpWebResponse)wex.Response).StatusCode);
                                }

                                Console.WriteLine(ex.GetType());
                                Console.WriteLine(ex.Message);
                                Console.WriteLine(ex.StackTrace);
                            }
                        }
                        catch (Exception e) {
                            Console.WriteLine(e.GetType());
                            Console.WriteLine(e.Message);
                            Console.WriteLine(e.StackTrace);
                        }
                    
                }, TaskContinuationOptions.AttachedToParent);
            }
        }

        private void _cancel() {
            RemoteFileMessages.Invoke.Failed(RemoteLocation);
        }

        public void Cancel() {
            
        }

        protected Uri ActualRemoteLocation { get; set; }

        private IEnumerable<Task> AsyncReadImpl(TaskCompletionSource<HttpWebResponse> tcs, HttpWebResponse httpWebResponse) {
            using (var responseStream = httpWebResponse.GetResponseStream()) {
                var total = 0L;
                var buffer = new byte[BUFFER_SIZE];
                while (true) {
                    if (IsCancelled) {
                        _cancel();
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
                    
                    RemoteFileMessages.Invoke.Progress(RemoteLocation, (int) (_contentLength <= 0 ? total : (int) (total*100/_contentLength)));

                    // write to output file.
                    _filestream.Write(buffer, 0, bytesRead);
                    _filestream.Flush();
                }
                // end of the file!
                _filestream.Close();
                _filestream = null;

                try {
                    if (IsCancelled) {
                        _cancel();
                        tcs.SetResult(null);
                    }

                    var fi = new FileInfo(Filename);
                    File.SetCreationTime(Filename, _lastModified);
                    File.SetLastWriteTime(Filename,_lastModified);

                    if (_contentLength == 0) {
                        _contentLength = fi.Length;
                    }
                    RemoteFileMessages.Invoke.Completed(RemoteLocation);
                    tcs.SetResult(null);
                }
                catch (Exception e) {
                    Console.WriteLine(e.Message);
                    tcs.SetException(e);
                }
            }
        }
    }

  
}