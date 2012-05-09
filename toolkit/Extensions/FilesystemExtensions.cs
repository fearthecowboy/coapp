//-----------------------------------------------------------------------
// <copyright company="CoApp Project">
//     Original Copyright (c) 2009 Microsoft Corporation. All rights reserved.
//     Changes Copyright (c) 2011 Eric Schultz, 2010  Garrett Serack. All rights reserved.
// </copyright>
// <license>
//     The software is licensed under the Apache 2.0 License (the "License")
//     You may not use the software except in compliance with the License. 
// </license>
//-----------------------------------------------------------------------

// -----------------------------------------------------------------------
// Original Code: 
// (c) 2009 Microsoft Corporation -- All rights reserved
// This code is licensed under the MS-PL
// http://www.opensource.org/licenses/ms-pl.html
// Courtesy of the Open Source Techology Center: http://port25.technet.com
// -----------------------------------------------------------------------

namespace CoApp.Toolkit.Extensions {
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Security.Cryptography;
    using System.Text;
    using System.Text.RegularExpressions;
    using Exceptions;
    using Logging;
    using Microsoft.Win32;
    using Shell;
    using Win32;

#if COAPP_ENGINE_CORE
    using Tasks;
    using Packaging.Service;
#endif

    public class PushDirectory : IDisposable {
        private readonly string _originalDirectory;

        public PushDirectory(string path) {
            _originalDirectory = Environment.CurrentDirectory;
            Environment.CurrentDirectory = path;
        }

        public void Dispose() {
            Environment.CurrentDirectory = _originalDirectory;
        }
    }

    /// <summary>
    ///   Functions related to handling things regarding files and filesystems.
    /// </summary>
    /// <remarks>
    /// </remarks>
    public static class FilesystemExtensions {
        /// <summary>
        ///   a running counter of for funtions wanting to number files with increasing numbers.
        /// </summary>
        private static int _counter = Process.GetCurrentProcess().Id << 16;

        public static int Counter {
            get {
                return ++_counter;
            }
        }

        public static string CounterHex {
            get {
                return Counter.ToString("x8");
            }
        }

        /// <summary>
        ///   A hashset of strings that has already been fullpath'd
        /// </summary>
        private static readonly HashSet<string> FullPathCache = new HashSet<string>();

        /// <summary>
        ///   the Kernel filename prefix string for paths that should not be interpreted. Just nod, and keep goin'
        /// </summary>
        private const string NonInterpretedPathPrefix = @"\??\";

        /// <summary>
        ///   regular expression to identify a UNC path returned by the Kernel. (needed for path normalization for reparse points)
        /// </summary>
        private static readonly Regex UncPrefixRx = new Regex(@"\\\?\?\\UNC\\");

        /// <summary>
        ///   regular expression to match a drive letter in a low level path (needed for path normalization for reparse points)
        /// </summary>
        private static readonly Regex DrivePrefixRx = new Regex(@"\\\?\?\\[a-z,A-Z]\:\\");

#pragma warning disable 169
        /// <summary>
        ///   regular expression to identify a volume mount point (needed for path normalization for reparse points)
        /// </summary>
        private static readonly Regex VolumePrefixRx = new Regex(@"\\\?\?\\Volume");
#pragma warning restore 169

        /// <summary>
        ///   Apparently, Eric has gone insane? NOTE: subject to cleanup.
        /// </summary>
        private static readonly Regex InvalidDoubleWcRx = new Regex(@"\\.+\*\*|\*\*[^\\]+\\|\*\*\\\*\*");

        private static readonly char[] WildcardChars = new[] {
            '*', '?'
        };

        public static string SystemTempFolder {
            get {
                return Environment.GetEnvironmentVariable("temp", EnvironmentVariableTarget.Machine);
            }
        }

        public static string OriginalTempFolder;

        static FilesystemExtensions() {
            OriginalTempFolder = Path.GetTempPath();
            ResetTempFolder();
        }

        public static void ResetTempFolder() {
            // set the temporary folder to be a child of the User temporary folder
            // based on the application name
            var appTempPath = Path.Combine(OriginalTempFolder, (Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly()).GetName().Name);
            if (!Directory.Exists(appTempPath)) {
                Directory.CreateDirectory(appTempPath);
            }

            TempPath = Path.Combine(appTempPath, (Directory.EnumerateDirectories(appTempPath).Count() + 1).ToString());
            if (!Directory.Exists(TempPath)) {
                Directory.CreateDirectory(TempPath);
            }

            Environment.SetEnvironmentVariable("TMP", TempPath);
            Environment.SetEnvironmentVariable("TEMP", TempPath);

            TryToHandlePendingRenames(true);
        }

#if COAPP_ENGINE_CORE 

    // in the engine, we'd like disposable filenames to be session-local
        private static List<string> DisposableFilenames {
            get { return SessionCache<List<string>>.Value["DisposableFilenames"] ?? (SessionCache<List<string>>.Value["DisposableFilenames"] = new List<string>()); }
        }
#else
        private static readonly List<string> DisposableFilenames = new List<string>();
#endif
        private static bool _triedCleanup;

        public static void RemoveTemporaryFiles() {
            foreach (var f in DisposableFilenames.ToArray().Where(File.Exists)) {
                f.TryHardToDelete();
            }

            // this is a wee bit more aggressive than I'm comfortable with.
            // RemoveInvalidSymlinks();

            // and try to clean up as we leave the process too.
            TryToHandlePendingRenames(true);
        }

        public static void RemoveInvalidSymlinks(string directory = null, int maxdepth = 3) {
            directory = directory ?? KnownFolders.GetFolderPath(KnownFolder.CommonApplicationData);
            try {
                foreach (var file in Directory.GetFiles(directory)) {
                    try {
                        if (Symlink.IsInvalidLink(file) == true) {
                            Symlink.DeleteSymlink(file);
                        }
                    } catch {
                        // skip what can not be done.        
                    }
                }
            } catch {
                // skip what can not be done.
            }

            try {
                foreach (var folder in Directory.GetDirectories(directory)) {
                    try {
                        if (Symlink.IsInvalidLink(folder) == true) {
                            Symlink.DeleteSymlink(folder);
                            continue;
                        }
                    } catch {
                        // skip what can not be done.
                    }
                    if (maxdepth > 0) {
                        RemoveInvalidSymlinks(folder, maxdepth - 1);
                    }
                }
            } catch {
                // skip what can not be done.
            }
        }

        public static bool? IsFolderEmpty(string foldername) {
            foldername = foldername.GetFullPath();
            if (Directory.Exists(foldername)) {
                return !Directory.GetFileSystemEntries(foldername).Any();
            }
            return null;
        }

        public static bool RemoveEssentiallyEmptyFolders(string folderName) {
            folderName = folderName.GetFullPath();

            try {
                if (Symlink.IsInvalidLink(folderName) == true) {
                    Symlink.DeleteSymlink(folderName);
                }
            } catch {
                // just keep truckin'
            }

            // skip symlinked folders that contain anything, they're not our responsibility.
            if (Symlink.IsSymlink(folderName) && (IsFolderEmpty(folderName) == false)) {
                return false;
            }
            var failed = false;

            try {
                if (Directory.Exists(folderName)) {
                    // remove em if they are empty
                    failed = Directory.GetDirectories(folderName).Aggregate(failed, (current, folder) => current | (!RemoveEssentiallyEmptyFolders(folder)));

                    // any left over?
                    if (failed || IsFolderEmpty(folderName) == false) {
                        return false;
                    }

                    // try to remove 
                    Directory.Delete(folderName);

                    // if it's gone, we're good!
                    return !Directory.Exists(folderName);
                }
            } catch {
                // don't do what can't be done.
                return false;
            }

            return true;
        }

        public static void RemoveDeadLnks(string folder) {
            var lnkFiles = DirectoryEnumerateFilesSmarter(folder, "**.lnk", SearchOption.AllDirectories).ToArray();
            foreach (var lnk in lnkFiles.Where(lnk => (ShellLink.IsInvalidShortcut(lnk) == true))) {
                TryHardToDelete(lnk);
                var parentFolder = Path.GetDirectoryName(lnk);
                RemoveEssentiallyEmptyFolders(parentFolder);
            }
        }

        public static string MarkFileTemporary(this string filename) {
#if !DEBUG
            lock(typeof(FilesystemExtensions)) {
                DisposableFilenames.Add(filename);
            }
#endif
            return filename;
        }

        public static string GetFileInTempFolder(this string filename) {
            var p = Path.Combine(TempPath, filename);
            if (File.Exists(p)) {
                p.TryHardToDelete();
            }

            return MarkFileTemporary(p);
        }

        public static string TempPath { get; private set; }

        public static string GenerateTemporaryFilename(this string filename) {
            string ext = null;
            string name = null;
            string folder = null;

            if (! string.IsNullOrEmpty(filename)) {
                ext = Path.GetExtension(filename);
                name = Path.GetFileNameWithoutExtension(filename);
                folder = Path.GetDirectoryName(filename);
            }

            if (string.IsNullOrEmpty(ext)) {
                ext = ".tmp";
            }
            if (string.IsNullOrEmpty(folder)) {
                folder = TempPath;
            }

            name = Path.Combine(folder, "tmpFile." + CounterHex + (string.IsNullOrEmpty(name) ? ext : "." + name + ext));

            if (File.Exists(name)) {
                name.TryHardToDelete();
            }

            return MarkFileTemporary(name);
        }

        /// <summary>
        ///   Determines if the childPath is a sub path of the rootPath
        /// </summary>
        /// <param name="rootPath"> The root path. </param>
        /// <param name="childPath"> The child path. </param>
        /// <returns> <c>true</c> if [is sub path] [the specified root path]; otherwise, <c>false</c> . </returns>
        /// <remarks>
        /// </remarks>
        public static bool IsSubPath(this string rootPath, string childPath) {
            return Path.GetFullPath(childPath).StartsWith(Path.GetFullPath(rootPath), StringComparison.CurrentCultureIgnoreCase);
        }

        /// <summary>
        ///   Gets the portion of the childPath that is a sub path of the parentPath Returns string.Empty if the childPath is not a sub path of the parent.
        /// </summary>
        /// <param name="parentPath"> The parent path. </param>
        /// <param name="childPath"> The child path. </param>
        /// <returns> </returns>
        /// <remarks>
        /// </remarks>
        public static string GetSubPath(this string parentPath, string childPath) {
            var parent = Path.GetFullPath(parentPath);
            var child = Path.GetFullPath(childPath);
            if (child.StartsWith(parent, StringComparison.CurrentCultureIgnoreCase)) {
                return child.Substring(parent.Length).Trim('/', '\\');
            }
            return string.Empty;
        }

        /// <summary>
        ///   Changes the file extension to another extension.
        /// </summary>
        /// <param name="currentFilename"> The current filename. </param>
        /// <param name="newExtension"> The new extension. </param>
        /// <returns> </returns>
        /// <remarks>
        /// </remarks>
        public static string ChangeFileExtensionTo(this string currentFilename, string newExtension) {
            return Path.Combine(Path.GetDirectoryName(currentFilename) ?? "", Path.GetFileNameWithoutExtension(currentFilename) + (newExtension.StartsWith(".") ? newExtension : "." + newExtension));
        }

        /// <summary>
        ///   Gets the relative path between two paths.
        /// </summary>
        /// <param name="currentDirectory"> The current directory. </param>
        /// <param name="pathToMakeRelative"> The path to make relative. </param>
        /// <returns> </returns>
        /// <remarks>
        /// </remarks>
        public static string RelativePathTo(this string currentDirectory, string pathToMakeRelative) {
            if (string.IsNullOrEmpty(currentDirectory)) {
                throw new ArgumentNullException("currentDirectory");
            }

            if (string.IsNullOrEmpty(pathToMakeRelative)) {
                throw new ArgumentNullException("pathToMakeRelative");
            }

            currentDirectory = Path.GetFullPath(currentDirectory);
            pathToMakeRelative = Path.GetFullPath(pathToMakeRelative);

            if (!Path.GetPathRoot(currentDirectory).Equals(Path.GetPathRoot(pathToMakeRelative), StringComparison.CurrentCultureIgnoreCase)) {
                return pathToMakeRelative;
            }

            var relativePath = new List<string>();
            var currentDirectoryElements = currentDirectory.Split(Path.DirectorySeparatorChar);
            var pathToMakeRelativeElements = pathToMakeRelative.Split(Path.DirectorySeparatorChar);
            var commonDirectories = 0;

            for (; commonDirectories < Math.Min(currentDirectoryElements.Length, pathToMakeRelativeElements.Length); commonDirectories++) {
                if (
                    !currentDirectoryElements[commonDirectories].Equals(pathToMakeRelativeElements[commonDirectories], StringComparison.CurrentCultureIgnoreCase)) {
                    break;
                }
            }

            for (var index = commonDirectories; index < currentDirectoryElements.Length; index++) {
                if (currentDirectoryElements[index].Length > 0) {
                    relativePath.Add("..");
                }
            }

            for (var index = commonDirectories; index < pathToMakeRelativeElements.Length; index++) {
                relativePath.Add(pathToMakeRelativeElements[index]);
            }

            return string.Join(Path.DirectorySeparatorChar.ToString(), relativePath);
        }

        /// <summary>
        ///   Generates a filename based of a template that can contain many different values
        /// </summary>
        /// <param name="filename"> The filename. </param>
        /// <param name="filenameHint"> The filename hint. </param>
        /// <returns> </returns>
        /// <remarks>
        ///   {filename} - substitutes for the original filename, no extension {ext} original extension {folder} - original folder {subfolder} - original folder without leading / {count} - a running count of the files downloaded. {date} - the current date (y-m-d) {date-long} - the date in long format {time} - the current time (Hh:mm:ss) {time-long} - the current time in long fmt {ticks} - the current timestamp as tics
        /// </remarks>
        public static string GenerateTemplatedFilename(this string filename, string filenameHint) {
            var result = filename;

            if (!filenameHint.StartsWith("\\\\")) {
                if (filenameHint.StartsWith("/") || filenameHint.StartsWith("\\")) {
                    filenameHint = Environment.CurrentDirectory.Substring(0, 2) + filenameHint;
                }

                if (filenameHint.IndexOf(":") == -1) {
                    filenameHint = Path.Combine(Environment.CurrentDirectory, filenameHint);
                }
            }

            var uri = new Uri(filenameHint);
            // filenameHint = uri.AbsolutePath;

            var localPath = uri.LocalPath.Replace("/", "\\");

            result = result.Replace(@"{filename}", Path.GetFileNameWithoutExtension(localPath));
            result = result.Replace(@"{ext}", Path.GetExtension(localPath));
            result = result.Replace(@"{folder}", Path.GetDirectoryName(localPath));
            var pr = Path.GetPathRoot(localPath);
            if (!string.IsNullOrEmpty(pr)) {
                result = result.Replace(@"{subfolder}", Path.GetDirectoryName(localPath).Remove(0, pr.Length));
            }

            result = result.Replace(@"{count}", "" + CounterHex);
            result = result.Replace(@"{date}", DateTime.Now.ToString("yyyy-MM-dd"));
            result = result.Replace(@"{date-long}", DateTime.Now.ToString("MMMM dd YYYY"));
            result = result.Replace(@"{time}", DateTime.Now.ToString("hh-mm-ss"));
            result = result.Replace(@"{time-long}", DateTime.Now.ToString("HH-mm-ss-ffff"));
            result = result.Replace(@"{ticks}", "" + DateTime.Now.Ticks);

            return result;
        }

        /// <summary>
        ///   Generates a filename on a somewhat different template.
        /// </summary>
        /// <param name="filename"> The filename. </param>
        /// <param name="parameters"> The parameters. </param>
        /// <returns> </returns>
        /// <remarks>
        ///   {date} - the current date (y-m-d) {date-long} - the date in long format {time} - the current time (Hh:mm:ss) {time-long} - the current time in long fmt {ticks} - the current timestamp as tics {counter} - a running counter
        /// </remarks>
        public static string FormatFilename(this string filename, params string[] parameters) {
            var result = filename;
            result = result.Replace(@"{counter}", "" + CounterHex);
            result = result.Replace(@"{date}", DateTime.Now.ToString("yyyy-MM-dd"));
            result = result.Replace(@"{date-long}", DateTime.Now.ToString("MMMM dd YYYY"));
            result = result.Replace(@"{time}", DateTime.Now.ToString("hhmmss"));
            result = result.Replace(@"{time-long}", DateTime.Now.ToString("HHmmssffff"));
            result = result.Replace(@"{ticks}", "" + DateTime.Now.Ticks);

            return result.format(parameters);
        }

        public static IEnumerable<string> DirectoryEnumerateFilesSmarter(this string path, SearchOption searchOption) {
            // finds all the files in a set of subdirectories, softly skipping when access is blocked
            var files = Enumerable.Empty<string>();
            try {
                files = Directory.EnumerateFiles(path);
            } catch {
            }

            foreach (var file in files) {
                yield return file;
            }

            if (searchOption == SearchOption.AllDirectories) {
                var directories = Enumerable.Empty<string>();
                try {
                    directories = Directory.EnumerateDirectories(path);
                } catch {
                }
                foreach (var directory in directories) {
                    files = DirectoryEnumerateFilesSmarter(directory, searchOption);
                    foreach (var file in files) {
                        yield return file;
                    }
                }
            }
        }

        /// <summary>
        ///   Enumerates files in a directory, smarter than Direcotry.EnumerateFiles (ie, doesn't throw, when it can't access something)
        /// </summary>
        /// <param name="path"> The path. </param>
        /// <param name="searchPattern"> The search pattern. </param>
        /// <param name="searchOption"> The search option. </param>
        /// <param name="skipPathPatterns"> The skip path patterns. </param>
        /// <returns> </returns>
        /// <remarks>
        /// </remarks>
        public static IEnumerable<string> DirectoryEnumerateFilesSmarter(this string path, string searchPattern, SearchOption searchOption) {
            return
                DirectoryEnumerateFilesSmarter(path.ToLower(), searchOption).Where(
                    file => file.ToLower().NewIsWildcardMatch(searchPattern.ToLower(), true, path.ToLower()));
        }

        /// <summary>
        ///   cleans up a path if it is a filesystem path, uri or wildcard. returns the original unmodified path if it is not.
        /// </summary>
        /// <param name="path"> </param>
        /// <returns> </returns>
        public static string CanonicalizePathWithWildcards(this string path) {
            try {
                return CanonicalizePath(path.Replace("*", "$$STAR$$").Replace("?", "$$QUERY$$")).Replace("$$STAR$$", "*").Replace("$$QUERY$$", "?").ToLower().TrimEnd('\\');
            } catch {
            }
            // if
            return path;
        }

        /// <summary>
        ///   A front end to DirectoryEnumerateFilesSmarter that allows for wildcards in the path or serarch mask (and expands it out to a full path first.) it also constrains the path as much as possible to keep from enumerating directories that will never match files.
        /// </summary>
        /// <param name="pathMask"> The path mask. </param>
        /// <param name="searchOption"> The search option. </param>
        /// <returns> </returns>
        /// <remarks>
        /// </remarks>
        public static IEnumerable<string> FindFilesSmarter(this string pathMask, string subpathMask = null) {
            var path = pathMask.Replace("*", "$$STAR$$").Replace("?", "$$QUERY$$");
            if (!string.IsNullOrEmpty(subpathMask)) {
                path = Path.Combine(path, subpathMask.Replace("*", "$$STAR$$").Replace("?", "$$QUERY$$"));
            }
            path = path.GetFullPath().Replace("$$STAR$$", "*").Replace("$$QUERY$$", "?");

            var i = path.IndexOfAny(WildcardChars);
            if (i == -1) {
                // then there isn't any wildcards in the search. That should probably be pretty easy :S
                i = path.Length;
            }
            // find the last slash before the first wildcard.
            var j = path.LastIndexOf("\\", i);

            // the mask is everything past that point
            var mask = path.Substring(j + 1);

            // the root path is everything before 
            path = path.Substring(0, j);

            // call DEFS with the minimally constrained path & mask
            return path.ToLower().DirectoryEnumerateFilesSmarter(mask.ToLower(),
                mask.IndexOf("**") > -1 ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly);
        }

        /// <summary>
        ///   finds matches for a collection of filenames using FindFilesSmarter (above)
        /// </summary>
        /// <param name="pathMasks"> The path masks. </param>
        /// <returns> </returns>
        /// <remarks>
        /// </remarks>
        public static IEnumerable<string> FindFilesSmarter(this IEnumerable<string> pathMasks) {
            return pathMasks.Aggregate(Enumerable.Empty<string>(), (current, p) => current.Union(p.FindFilesSmarter()));
        }

#if !COAPP_ENGINE_CORE
        /// <summary>
        ///   always call IsWildcardMatch with a prefix!!!!
        /// </summary>
        /// <param name="pathMask"> </param>
        /// <param name="pathPrefix"> </param>
        /// <returns> </returns>
        public static IEnumerable<string> FindFilesSmarterComplex(this string pathMask, string pathPrefix = null) {
            //pathMask safety
            if (String.IsNullOrEmpty(pathMask)) {
                return FindFilesSmarterComplex(pathPrefix);
            }
            if (InvalidDoubleWcRx.IsMatch(pathMask)) {
                throw new ArgumentException("The wildcard path {0} is invalid.".format(pathMask));
            }

            pathPrefix = String.IsNullOrEmpty(pathPrefix) ? Directory.GetCurrentDirectory() : pathPrefix;

            pathMask = pathMask.Replace("/", "\\");
            var nextPart = pathMask.GetNextPart();
            var onLastPart = nextPart.Item2 == "";

            if (nextPart.Item1 == "**") {
                if (onLastPart) {
                    //we just get every file from here on down

                    return Directory.EnumerateFiles(pathPrefix, "*", SearchOption.AllDirectories);
                } else {
                    var partAfterWildcard = nextPart.Item2.GetNextPart();

                    var nextPartIsLast = partAfterWildcard.Item2 == "";

                    if (nextPartIsLast) {
                        return Directory.EnumerateFiles(pathPrefix, partAfterWildcard.Item1, SearchOption.AllDirectories).
                            Aggregate(Enumerable.Empty<string>(),
                                (output, d) => output.Concat(pathPrefix.RelativePathTo(d).FindFilesSmarterComplex(pathPrefix)));
                    }
                    var dirs = Directory.EnumerateDirectories(pathPrefix, partAfterWildcard.Item1, SearchOption.AllDirectories);

                    return dirs.
                        Aggregate(Enumerable.Empty<string>(),
                            (output, d) =>
                                output.Concat(
                                    (pathPrefix.RelativePathTo(d) + "\\" + partAfterWildcard.Item2).FindFilesSmarterComplex(pathPrefix)));
                }
            }
            if (nextPart.Item1.Contains("*")) {
                if (onLastPart) {
                    return Directory.EnumerateFiles(pathPrefix, nextPart.Item1).
                        Aggregate(Enumerable.Empty<string>(),
                            (output, d) => output.Concat(Path.GetFileName(d).FindFilesSmarterComplex(pathPrefix)));
                }
                var dirs = Directory.EnumerateDirectories(pathPrefix, nextPart.Item1);

                return dirs.
                    Aggregate(Enumerable.Empty<string>(),
                        (output, d) => output.Concat((Path.GetFileName(d) + "\\" + nextPart.Item2).FindFilesSmarterComplex(pathPrefix)));
            }
            //recursively keep going
            var newPathPrefix = pathPrefix;

            if (!String.IsNullOrEmpty(nextPart.Item1)) {
                newPathPrefix += "\\" + nextPart.Item1;
            }

            if (onLastPart) {
                return File.Exists(newPathPrefix) ? newPathPrefix.SingleItemAsEnumerable() : Enumerable.Empty<string>();
            }
            return Directory.Exists(newPathPrefix)
                ? nextPart.Item2.FindFilesSmarterComplex(newPathPrefix)
                : Enumerable.Empty<string>();
        }

        public static IEnumerable<string> FindFilesSmarterComplex(this IEnumerable<string> pathMasks) {
            return pathMasks.Aggregate(Enumerable.Empty<string>(), (current, p) => current.Union(p.FindFilesSmarterComplex()));
        }
#endif

        /// <summary>
        ///   Gets the name of a file minus it's extension, ie: if the file name is "test.exe", returns "test".
        /// </summary>
        /// <param name="fi"> The fi. </param>
        /// <returns> </returns>
        /// <remarks>
        /// </remarks>
        public static string NameWithoutExt(this FileInfo fi) {
            return fi.Name.Remove(fi.Name.Length - fi.Extension.Length);
        }

        /// <summary>
        ///   Directories the exists and is accessible.
        /// </summary>
        /// <param name="path"> The path. </param>
        /// <returns> </returns>
        /// <remarks>
        /// </remarks>
        public static bool DirectoryExistsAndIsAccessible(this string path) {
            try {
                return Directory.Exists(path);
            } catch {
            }
            return false;
        }

        /// <summary>
        ///   Writes all bytes from the contents of a memorystream to file (as a binary file).
        /// </summary>
        /// <param name="ms"> The ms. </param>
        /// <param name="path"> The path. </param>
        /// <remarks>
        /// </remarks>
        public static void WriteAllBytesToFile(this MemoryStream ms, string path) {
            if (string.IsNullOrEmpty(path)) {
                throw new ArgumentNullException("path", "Invalid Path");
            }

            using (var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read)) {
                stream.Write(ms.GetBuffer(), 0, (int)ms.Length);
            }
        }

        /// <summary>
        ///   Reads the contents of a file into a memory stream.
        /// </summary>
        /// <param name="ms"> The ms. </param>
        /// <param name="path"> The path. </param>
        /// <remarks>
        /// </remarks>
        public static void ReadAllBytesFromFile(this MemoryStream ms, string path) {
            if (string.IsNullOrEmpty(path)) {
                throw new ArgumentNullException("path", "Invalid Path");
            }

            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read)) {
                ms.SetLength(stream.Length);
                stream.Read(ms.GetBuffer(), 0, (int)stream.Length);
            }
        }

        public static void TryToHandlePendingRenames(bool force = false) {
            try {
                if (force || !_triedCleanup) {
                    _triedCleanup = true;

                    var localMachine = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
                    var sessionManager = localMachine.CreateSubKey(@"SYSTEM\CurrentControlSet\Control\Session Manager");
                    if (sessionManager == null) {
                        return;
                    }

                    if (sessionManager.GetValueNames().ContainsIgnoreCase("PendingFileRenameOperations")) {
                        var pfrops = (String[])sessionManager.GetValue("PendingFileRenameOperations");
                        var skipped = new List<string>();
                        if (!pfrops.IsNullOrEmpty()) {
                            for (var i = 0; i < pfrops.Length; i += 2) {
                                var src = pfrops[i];
                                var dest = pfrops[i + 1];

                                try {
                                    var srcNormal = NormalizePath(src);
                                    if (Directory.Exists(srcNormal)) {
                                        if (string.IsNullOrEmpty(dest)) {
                                            Directory.Delete(srcNormal, true);
                                            if (Directory.Exists(srcNormal)) {
                                                // didn't delete? put it back in the list.
                                                skipped.Add(src + "\0");
                                            }
                                            continue;
                                        }
                                        // A directory rename operation. we'll leave these alone
                                        skipped.Add(src);
                                        skipped.Add(dest);
                                        continue;
                                    }

                                    if (!File.Exists(srcNormal)) {
                                        continue; // we're dropping files that don't exist anymore at all.
                                    }

                                    if (string.IsNullOrEmpty(dest)) {
                                        // delete op
                                        Kernel32.DeleteFile(srcNormal);
                                        if (File.Exists(srcNormal)) {
                                            // didn't work. No problem. Add it back to the list.
                                            skipped.Add(src + "\0");
                                        }
                                        continue;
                                    }

                                    // it's a move op
                                    Kernel32.MoveFileEx(srcNormal, NormalizePath(dest), MoveFileFlags.MOVEFILE_REPLACE_EXISTING);
                                    if (File.Exists(srcNormal)) {
                                        skipped.Add(src);
                                        skipped.Add(dest);
                                    }
                                    continue;
                                } catch (Exception) {
                                    skipped.Add(src);
                                    skipped.Add(dest);
                                }
                            }
                            sessionManager.SetValue("PendingFileRenameOperations", skipped.ToArray(), RegistryValueKind.MultiString);
                        }
                    }
                    sessionManager.Close();
                }
            } catch {
                // no worry if this doesn't go. it's a best-effort-kind of thing
            }
        }

        public static void TryHardToDelete(this string location) {
            if (Directory.Exists(location)) {
                try {
                    Directory.Delete(location, true);
                } catch {
                    // didn't take, eh?
                }
            }

            if (File.Exists(location)) {
                try {
                    File.Delete(location);
                } catch {
                    // didn't take, eh?
                }
            }

            // if it is still there, move and mark it.
            if (File.Exists(location) || Directory.Exists(location)) {
                try {
                    // move the file to the tmp file
                    // and tell the OS to remove it next reboot.
                    var tmpFilename = location.GenerateTemporaryFilename(); // generates a unique filename but not a file!
                    Kernel32.MoveFileEx(location, tmpFilename, MoveFileFlags.MOVEFILE_REPLACE_EXISTING);

                    if (File.Exists(location) || Directory.Exists(location)) {
                        // of course, if the tmpFile isn't on the same volume as the location, this doesn't work.
                        // then, last ditch effort, let's rename it in the current directory
                        // and then we can hide it and mark it for cleanup .
                        tmpFilename = Path.Combine(Path.GetDirectoryName(location), "tmp." + CounterHex + "." + Path.GetFileName(location));
                        Kernel32.MoveFileEx(location, tmpFilename, MoveFileFlags.MOVEFILE_REPLACE_EXISTING);
                        if (File.Exists(tmpFilename) || Directory.Exists(location)) {
                            // hide the file for convenience.
                            File.SetAttributes(tmpFilename, File.GetAttributes(tmpFilename) | FileAttributes.Hidden);
                        }
                    }

                    // Now we mark the locked file to be deleted upon next reboot (or until another coapp app gets there)
                    Kernel32.MoveFileEx(File.Exists(tmpFilename) ? tmpFilename : location, null, MoveFileFlags.MOVEFILE_DELAY_UNTIL_REBOOT);
                } catch {
                    // really. Hmmm. 
                    // Logger.Error(e);
                }

                if (File.Exists(location)) {
                    // Logger.Error("Unable to forcably remove file '{0}'. This can't be good.", location);
                }
            }
            return;
        }

        /// <summary>
        ///   This makes sure a file is writable by moving the original, copying this one back then deleting the original (or at least tryinghardtodelete the original). This allows us to modify locked files in place.
        /// </summary>
        /// <param name="filename"> </param>
        public static void TryHardToMakeFileWriteable(this string filename) {
            filename = filename.GetFullPath();

            if (File.Exists(filename)) {
                var tmpFilename = filename.GenerateTemporaryFilename();
                File.Move(filename, tmpFilename);
                File.Copy(tmpFilename, filename);
                TryHardToDelete(tmpFilename);
            }
        }

        /// <summary>
        ///   This makes a temporary copy of a file that we can work with.
        /// </summary>
        /// <param name="filename"> </param>
        public static string CreateWritableWorkingCopy(this string filename) {
            filename = filename.GetFullPath();
            if (File.Exists(filename)) {
                // generates a temporary filename in the temp directory based off the given filename
                var tmpFilename = Path.GetFileName(filename).GenerateTemporaryFilename();
                File.Delete(tmpFilename);
                File.Copy(filename, tmpFilename);
                return tmpFilename;
            }
            return null;
        }

        /// <summary>
        ///   Writes the whole byte array to a filestream. (lazy!)
        /// </summary>
        /// <param name="fileStream"> The file stream. </param>
        /// <param name="data"> The data. </param>
        /// <returns> </returns>
        /// <remarks>
        /// </remarks>
        public static int Write(this FileStream fileStream, byte[] data) {
            fileStream.Write(data, 0, data.Length);
            return data.Length;
        }

        /// <summary>
        ///   Returns the full path of a string. Short circuts the process if the string is a known full path already. (ie, the result of a preivious GetFullPath())
        /// </summary>
        /// <param name="path"> The path. </param>
        /// <returns> </returns>
        /// <remarks>
        /// </remarks>
        public static string GetFullPath(this string path) {
            if (FullPathCache.Contains(path)) {
                return path;
            }
            try {
                if( path.IndexOfAny(Path.GetInvalidPathChars()) == -1 ) {
                    path = Path.GetFullPath(path.Trim('"'));
                    FullPathCache.Add(path);    
                }
            } catch {
            }
            return path;
        }

        /// <summary>
        ///   Translates paths starting with \??\ to regular paths.
        /// </summary>
        /// <param name="path"> The path. </param>
        /// <returns> </returns>
        /// <remarks>
        /// </remarks>
        public static string NormalizePath(this string path) {
            if (path.StartsWith(NonInterpretedPathPrefix)) {
                if (UncPrefixRx.Match(path).Success) {
                    path = UncPrefixRx.Replace(path, @"\\");
                }

                if (DrivePrefixRx.Match(path).Success) {
                    path = path.Replace(NonInterpretedPathPrefix, "");
                }
            }
            if (path.EndsWith("\\")) {
                var couldBeFilePath = path.Substring(0, path.Length - 1);
                if (File.Exists(couldBeFilePath)) {
                    path = couldBeFilePath;
                }
            }

            return path;
        }

        /// <summary>
        ///   This takes a string that is representative of a filename and tries to create a path that can be considered the 'canonical' path. path on drives that are mapped as remote shares are rewritten as their \\server\share\path
        /// </summary>
        /// <returns> </returns>
        public static string CanonicalizePath(this string path, bool IsPotentiallyRelativePath = true) {
            Uri pathUri = null;
            try {
                pathUri = new Uri(path);
                if (!pathUri.IsFile) {
                    // perhaps try getting the fullpath
                    try {
                        pathUri = new Uri(path.GetFullPath());
                    } catch {
                        throw new PathIsNotFileUriException(path, pathUri);
                    }
                }

                // is this a unc path?
                if (string.IsNullOrEmpty(pathUri.Host)) {
                    // no, this is a drive:\path path
                    // use API to resolve out the drive letter to see if it is a remote 
                    var drive = pathUri.Segments[1].Replace('/', '\\'); // the zero segment is always just '/' 

                    var sb = new StringBuilder(512);
                    var size = sb.Capacity;

                    var error = MPR.WNetGetConnection(drive, sb, ref size);
                    if (error == 0) {
                        if (pathUri.Segments.Length > 2) {
                            return pathUri.Segments.Skip(2).Aggregate(sb.ToString().Trim(), (current, item) => current + item);
                        }
                    }
                }
                // not a remote (or resovably-remote) path or 
                // it is already a path that is in it's correct form (via localpath)
                return pathUri.LocalPath;
            } catch (UriFormatException) {
                // we could try to see if it is a relative path...
                if (IsPotentiallyRelativePath) {
                    return CanonicalizePath(path.GetFullPath(), false);
                }
                throw new ArgumentException("specified path can not be resolved as a file name or path (unc, url, localpath)", path);
            }
        }

        /// <summary>
        ///   Returns the MD5 Hash of a file as an uppercase hex string
        /// </summary>
        /// <param name="filename"> the filename to retrive the hash for </param>
        /// <returns> a string containing the md5 hash (32 characters) </returns>
        public static string GetFileMD5(this string filename) {
            using (var stream = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read)) {
                return MD5.Create().ComputeHash(stream).ToHexString().ToUpper();
            }
        }

        public static string GetFileSHA1(this string filename) {
            using (var stream = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read)) {
                return SHA1.Create().ComputeHash(stream).ToHexString().ToLower();
            }
        }

        /// <summary>
        ///   Gets the next part. Note: Make Eric document this?
        /// </summary>
        /// <param name="path"> The path. </param>
        /// <returns> </returns>
        /// <remarks>
        /// </remarks>
        private static Tuple<string, string> GetNextPart(this string path) {
            var indexOfSlash = path.IndexOf('\\');
            return indexOfSlash == -1
                ? new Tuple<string, string>(path, "")
                : new Tuple<string, string>(path.Substring(0, indexOfSlash), path.Substring(indexOfSlash + 1));
        }

        /// <summary>
        ///   Replaces Unix style file path separators (/) with Windows style (\).
        /// </summary>
        /// <param name="filepath"> The filepath. </param>
        /// <returns> </returns>
        /// <remarks>
        /// </remarks>
        public static string FixFilepathSlashes(this string filepath) {
            return filepath.Replace(@"/", @"\");
        }

        /// <summary>
        ///   Tells whether a given path is a simple subpath. A simple subpath has the following characteristics: - No drive letter or colon - Does not start with a slash - Does not contain any path part sections consisting of just "." or ".." - Does not contain wildcards
        /// </summary>
        /// <param name="path"> the path to check </param>
        /// <returns> True if it is a simple subpath, false otherwise. </returns>
        /// <remarks>
        /// </remarks>
        public static bool IsSimpleSubPath(this string path) {
            var temp = path.FixFilepathSlashes();
            if (temp.Contains(":") || temp.Contains("*")) {
                return false;
            }
            if (temp.StartsWith(@"\")) {
                return false;
            }
            var pathParts = temp.Split('\\');
            if (pathParts.Any((i) => i == ".." || i == ".")) {
                return false;
            }

            return true;
        }

        public static string EnsureFileIsLocal(this string filename, string localFolder = null) {
            localFolder = localFolder ?? TempPath;
            var fullpath = filename.CanonicalizePath();

            if (File.Exists(fullpath)) {
                if (fullpath.StartsWith(@"\\")) {
                    var localCopy = Path.Combine(localFolder, Path.GetFileName(fullpath));
                    File.Copy(fullpath, localCopy);
                    return localCopy;
                }
                return fullpath;
            }
            return null;
        }

        public static string CanonicalizePathIfLocalAndExists(this string filename) {
            if (string.IsNullOrEmpty(filename)) {
                return null;
            }

            try {
                var fullpath = filename.CanonicalizePath();
                if (!fullpath.StartsWith(@"\\") && File.Exists(fullpath)) {
                    return fullpath;
                }
            } catch {
            }
            return null;
        }

        public static bool FileIsLocalAndExists(this string filename) {
            return !string.IsNullOrEmpty(CanonicalizePathIfLocalAndExists(filename));
        }

        public static IEnumerable<string> GetMinimalPaths(this IEnumerable<string> paths) {
            var allPaths = paths.ToArray();

            if (allPaths.Any() && allPaths.Skip(1).Any()) {
                IEnumerable<IEnumerable<string>> newPaths = allPaths.Select(each => each.GetFullPath()).Select(each => each.Split('\\'));
                while (newPaths.All(each => each.FirstOrDefault() == newPaths.FirstOrDefault().FirstOrDefault())) {
                    newPaths = newPaths.Select(each => each.Skip(1));
                }
                return newPaths.Select(each => each.Aggregate((current, value) => current + "\\" + value));
            }
            return allPaths.Select(Path.GetFileName);
        }
    }
}