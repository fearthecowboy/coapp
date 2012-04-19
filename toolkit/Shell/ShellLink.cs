//-----------------------------------------------------------------------
// <copyright company="CoApp Project">
//     Original (c) Author: Richard G Russell (Foredecker) 
//     Changes Copyright (c) 2010  Garrett Serack, CoApp Contributors. All rights reserved.
// </copyright>
// <license>
//     The software is licensed under the Apache 2.0 License (the "License")
//     You may not use the software except in compliance with the License. 
// </license>
//-----------------------------------------------------------------------

// -----------------------------------------------------------------------
// Original Code: 
// (c) Author: Richard G Russell (Foredecker) 
// This code is licensed under the MS-PL
// http://www.opensource.org/licenses/ms-pl.html
// Acquired from : http://foredecker.wordpress.com/2010/12/05/c-ishelllink-com-interop-example/
// -----------------------------------------------------------------------

namespace CoApp.Toolkit.Shell {
    using System;
    using System.IO;
    using System.Runtime.InteropServices;
    using System.Text;
    using Extensions;
    using Internal;

    public class ShellLink : IDisposable {
        #region Fields

        private ShellLinkCoClass theShellLinkObject;
        private IShellLink shellLink;
        private IShellLinkDataList dataList;
        private ConsoleProperties consoleProperties;

        private const int MAX_PATH = 260;

        #endregion

        public static ShellLink CreateShortcut(string shortcutPath, string actualFilePath, string description = null, string workingDirectory = null, string arguments = null) {
            shortcutPath = shortcutPath.GetFullPath();
            actualFilePath = actualFilePath.GetFullPath();
            if (!System.IO.Path.HasExtension(shortcutPath))
                shortcutPath += ".LNK";

            var link = new ShellLink(shortcutPath);
            link.Path = actualFilePath;

            link.WorkingDirectory = workingDirectory ?? System.IO.Path.GetDirectoryName(actualFilePath);

            if (description != null)
                link.Description = description;

            if (arguments != null)
                link.Arguments = arguments;

            link.Save(shortcutPath);
            return link;
        }

        public static bool IsShellLink(string shortcutPath) {
            shortcutPath = shortcutPath.GetFullPath();

            if( File.Exists(shortcutPath) ) {
                try {
                    var shortcut = Load(shortcutPath);
                    return true;
                }
                catch {
                    return false;
                }
            }
            return false;
        }

        public static bool? IsInvalidShortcut( string shortcutPath) {
            shortcutPath = shortcutPath.GetFullPath();

            if (File.Exists(shortcutPath)) {
                try {
                    var target =  Load(shortcutPath).Path;
                    if( string.IsNullOrEmpty(target)) {
                        // not a file-system shortcut?
                        return false;
                    }
                    try {
                        var uri = new Uri(target);
                        if( uri.IsHttpScheme()) {
                            // web links are 'valid'
                            return false;
                        }
                    } catch {
                        
                    }
                    target = Environment.ExpandEnvironmentVariables(target).GetFullPath();

                    return !(File.Exists(target) || Directory.Exists(target));
                }
                catch {
                    // likely not actually a shortcut.
                }
            }
            return null;
        }

        public static bool PointsTo(string shortcutPath, string targetPath) {
            shortcutPath = shortcutPath.GetFullPath();
            targetPath = targetPath.GetFullPath();

            if (File.Exists(shortcutPath)) {
                try {
                    return Load(shortcutPath).Path.GetFullPath().Equals(targetPath, StringComparison.CurrentCultureIgnoreCase);
                }
                catch {
                    return false;
                }
            }
            return false;
        }

        #region Construction and Disposal

        /// <summary>
        ///   Create an empty shell link object
        /// </summary>
        public ShellLink() {
            theShellLinkObject = new ShellLinkCoClass();
            shellLink = (IShellLink) theShellLinkObject;
            dataList = (IShellLinkDataList) theShellLinkObject;
            consoleProperties = new ConsoleProperties(this);
        }

        /// <summary>
        ///   Load a shell link from a file.
        /// </summary>
        /// <param name = "linkFilePath">the path to the file</param>
        public ShellLink(string linkFilePath) : this() {
            if (File.Exists(linkFilePath)) {
                ((IPersistFile) shellLink).Load(linkFilePath, (int) STGM_FLAGS.STGM_READ);
                ReadConsoleProperties();
            }
        }

        ~ShellLink() {
            Dispose();
        }

        public void Dispose() {
            GC.SuppressFinalize(this);

            if (dataList != null) {
                Marshal.ReleaseComObject(dataList);
                dataList = null;
            }

            if (shellLink != null) {
                Marshal.ReleaseComObject(shellLink);
                shellLink = null;
            }

            if (theShellLinkObject != null) {
                Marshal.ReleaseComObject(theShellLinkObject);
                theShellLinkObject = null;
            }
        }

        #endregion

        /// <summary>
        ///   Save a shortcut to a file.  The shell requires a '.lnk' file extension.
        /// </summary>
        /// <remarks>
        ///   If the file exists it is silently overwritten.
        /// </remarks>
        /// <param name = "lnkPath">The path to the saved file. </param>
        public void Save(string lnkPath) {
            ((IPersistFile) shellLink).Save(lnkPath, true);
        }

        /// <summary>
        ///   Load a shortcut from a file.
        /// </summary>
        /// <param name = "linkPath">A path to the file.</param>
        public static ShellLink Load(string linkPath) {
            var result = new ShellLink();
            ((IPersistFile)result.shellLink).Load(linkPath, (int)STGM_FLAGS.STGM_READ);
            result.ReadConsoleProperties();
            return result;
        }

        /// <summary>
        ///   Get or sets the target of the shell link. The getter for this property uses the SLGP_RAWPATH flags.
        /// </summary>
        public String Path {
            get {
                var sb = new StringBuilder(WIN32_FIND_DATAW.MAX_PATH);
                WIN32_FIND_DATAW findData;
                shellLink.GetPath(sb, sb.Capacity, out findData, SLGP_FLAGS.SLGP_RAWPATH);
                return sb.ToString();
            }

            set { shellLink.SetPath(value); }
        }

        /// <summary>
        ///   Gets the the path to the shortcut (.lnk) file using the SLGP_SHORTPATH flag
        /// </summary>
        public string ShortPath {
            get {
                var sb = new StringBuilder(WIN32_FIND_DATAW.MAX_PATH);
                WIN32_FIND_DATAW findData;
                shellLink.GetPath(sb, sb.Capacity, out findData, SLGP_FLAGS.SLGP_SHORTPATH);
                return sb.ToString();
            }
        }

        /// <summary>
        ///   Gets the the path to the shortcut (.lnk) file using the SLGP_UNCPRIORITY flag
        /// </summary>
        public string UncPriorityPath {
            get {
                var sb = new StringBuilder(WIN32_FIND_DATAW.MAX_PATH);
                WIN32_FIND_DATAW findData;
                shellLink.GetPath(sb, sb.Capacity, out findData, SLGP_FLAGS.SLGP_UNCPRIORITY);
                return sb.ToString();
            }
        }

        /// <summary>
        ///   The command line arguments to the shortcut.
        /// </summary>
        public String Arguments {
            get {
                var sb = new StringBuilder(260);
                shellLink.GetArguments(sb, sb.Capacity);
                return sb.ToString();
            }

            set { shellLink.SetArguments(value); }
        }

        /// <summary>
        ///   Attempts to find the target of a Shell link, even if it has been moved or renamed.
        /// </summary>
        /// <param name = "flags">Flags that control the resolution process</param>
        public void Resolve(ResolveFlags flags) {
            shellLink.Resolve(IntPtr.Zero, (SLR_FLAGS) flags);
        }

        /// <summary>
        ///   Attempts to find the target of a Shell link, even if it has been moved or renamed.
        /// </summary>
        /// <param name = "hwnd">A handle to the window that the Shell will use as the parent for a dialog box. The Shell displays the dialog box if it needs to prompt the user for more information while resolving a Shell link.</param>
        /// <param name = "flags">Flags that control the resolution process</param>
        public void Resolve(IntPtr hwnd, ResolveFlags flags) {
            shellLink.Resolve(hwnd, (SLR_FLAGS) flags);
        }

        /// <summary>
        ///   Attempts to find the target of a Shell link, even if it has been moved or renamed.
        /// </summary>
        /// <param name = "flags">Flags that control the resolution process</param>
        /// <param name = "noUxTimeoutMs">The timeout, in ms, to wait for resolution when there is no UX</param>
        public void Resolve(ResolveFlags flags, int noUxTimeoutMs) {
            if ((flags & ResolveFlags.NoUi) == 0) {
                throw new ArgumentException("This methiod requires that the ResolveFlags.NoUi flag is set in the flags parameter.");
            }

            if (noUxTimeoutMs > short.MaxValue) {
                throw new ArgumentException(string.Format("the nouxTimeoutMs value must be <= {0}", short.MaxValue));
            }

            unchecked {
                flags = flags & (ResolveFlags) 0x0000FFFF;
                flags |= (ResolveFlags) (noUxTimeoutMs << 16);
            }

            shellLink.Resolve(IntPtr.Zero, (SLR_FLAGS) flags);
        }

        /// <summary>
        ///   Gets or sets the shortcut's working directory.
        /// </summary>
        public String WorkingDirectory {
            get {
                var sb = new StringBuilder(260);
                shellLink.GetWorkingDirectory(sb, sb.Capacity);
                return sb.ToString();
            }

            set { shellLink.SetWorkingDirectory(value); }
        }

        /// <summary>
        ///   Gets or sets the shortcut's description
        /// </summary>
        public String Description {
            get {
                var sb = new StringBuilder(260);
                shellLink.GetDescription(sb, sb.Capacity);
                return sb.ToString();
            }
            set { shellLink.SetDescription(value); }
        }

        /// <summary>
        ///   Gets and sets the location of the shortcut's ICON.  This may return an empty IconLocatoin object, one where the path property is empty.
        /// </summary>
        public IconLocation IconLocation {
            get {
                var sb = new StringBuilder(MAX_PATH);
                int iIcon;
                if (shellLink.GetIconLocation(sb, sb.Capacity, out iIcon) < 0) {
                    return new IconLocation();
                }
                return new IconLocation(sb.ToString(), iIcon);
            }

            set { shellLink.SetIconLocation(value.Path, value.Index); }
        }

        /// <summary>
        ///   Gets and sets the show command for shell link's object.
        /// </summary>
        public ShowWindowCommand ShowCommand {
            get {
                int showCmd;
                if (shellLink.GetShowCmd(out showCmd) < 0) {
                    return ShowWindowCommand.Hide;
                }
                return (ShowWindowCommand) showCmd;
            }
            set { shellLink.SetShowCmd((int) value); }
        }

        /// <summary>
        ///   Gets or sets the Shell Link Data Flags for a shell link
        /// </summary>
        public ShellLinkFlags Flags {
            get {
                UInt32 flags;
                dataList.GetFlags(out flags);
                return (ShellLinkFlags) flags;
            }
            set { dataList.SetFlags((UInt32) value); }
        }

        /// <summary>
        ///   True if the Shell Link has an NT_CONSOLE_PROPS data block.
        /// </summary>
        public bool HasConsoleProperties {
            get {
                IntPtr ppDataBlock;
                Int32 hr = dataList.CopyDataBlock(NT_CONSOLE_PROPS.NT_CONSOLE_PROPS_SIG, out ppDataBlock);

                if (hr < 0) {
                    return false;
                }
                else {
                    Marshal.FreeHGlobal(ppDataBlock);
                    return true;
                }
            }
        }

        /// <summary>
        ///   Gets the console properties for a shell link.  If HasConsoleProperties is false, then this 
        ///   property returns a ConsoleProperties that contains sensible default values.
        /// </summary>
        public ConsoleProperties ConsoleProperties {
            get { return this.consoleProperties; }
        }

        /// <summary>
        ///   Is true if the shell link as an NT_FE_CONSOLE_PROPS data block.
        /// </summary>
        public bool HasCodePage {
            get {
                IntPtr ppDataBlock;
                Int32 hr = dataList.CopyDataBlock(NT_FE_CONSOLE_PROPS.NT_FE_CONSOLE_PROPS_SIG, out ppDataBlock);

                if (hr < 0) {
                    return false;
                }
                else {
                    Marshal.FreeHGlobal(ppDataBlock);
                    return true;
                }
            }
        }

        /// <summary>
        ///   Gets or sets the code page for the console.  if there is no code page then the value for this property is zero.
        ///   Setting this propety to zero removes the assocated NT_FE_CONSOLE_PROPS data block from the shell link.  
        ///   When in doubt, use the Windows 1252 code page.
        /// </summary>
        /// <exception cref = "OverflowExeption">Thrown if the set value cannot be converted to a UInt32 wihtout overflow.</exception>
        public long CodePage {
            get {
                IntPtr ppDataBlock;
                Int32 hr = dataList.CopyDataBlock(NT_FE_CONSOLE_PROPS.NT_FE_CONSOLE_PROPS_SIG, out ppDataBlock);

                if (hr < 0) {
                    return 0;
                }

                var nt_fe_console_props = (NT_FE_CONSOLE_PROPS) Marshal.PtrToStructure(ppDataBlock, typeof (NT_FE_CONSOLE_PROPS));
                Marshal.FreeHGlobal(ppDataBlock);
                return (nt_fe_console_props.uCodePage);
            }

            set {
                dataList.RemoveDataBlock(NT_FE_CONSOLE_PROPS.NT_FE_CONSOLE_PROPS_SIG);

                if (value == 0) {
                    return;
                }

                UInt32 uCodePage;
                checked {
                    uCodePage = (UInt32) value;
                }

                NT_FE_CONSOLE_PROPS nt_fe_console_props = NT_FE_CONSOLE_PROPS.AnEmptyOne();
                nt_fe_console_props.uCodePage = uCodePage;

                // pin the structure, add it to the shell link, then un-pin it.
                GCHandle handle = GCHandle.Alloc(nt_fe_console_props, GCHandleType.Pinned); // pin the value
                dataList.AddDataBlock(GCHandle.ToIntPtr(handle));
                handle.Free(); // un-pin the value
            }
        }

        /// <summary>
        ///   Is true if the shell link has an EXP_SZ_LINK datablock with the EXP_SZ_LINK_SIG signature.
        /// </summary>
        public bool HasExpSzLink {
            get {
                IntPtr ppDataBlock;
                Int32 hr = dataList.CopyDataBlock(EXP_SZ_LINK.EXP_SZ_LINK_SIG, out ppDataBlock);

                if (hr < 0) {
                    return false;
                }
                else {
                    Marshal.FreeHGlobal(ppDataBlock);
                    return true;
                }
            }
        }

        /// <summary>
        ///   Get and sets the EXP_SZ_LINK property for a shell link. If there is no link then the property 
        ///   value is an empty string. Setting this to null, an empty string, or a string that is all white space 
        ///   removes the EXP_SZ_LINK data block with the EXP_SZ_LINK_SIG signature from the assocated shell link.
        /// </summary>
        public string ExpSzLink {
            get {
                IntPtr ppDataBlock;
                Int32 hr = dataList.CopyDataBlock(EXP_SZ_LINK.EXP_SZ_LINK_SIG, out ppDataBlock);

                if (hr < 0) {
                    return string.Empty;
                }

                var exp_sz_link = (EXP_SZ_LINK) Marshal.PtrToStructure(ppDataBlock, typeof (EXP_SZ_LINK));
                Marshal.FreeHGlobal(ppDataBlock);
                var value = new string(exp_sz_link.swzTarget);
                return value;
            }
            set {
                dataList.RemoveDataBlock(EXP_SZ_LINK.EXP_SZ_LINK_SIG);

                if (string.IsNullOrWhiteSpace(value)) {
                    return;
                }

                if (value.Length >= EXP_SZ_LINK.MAX_PATH) {
                    throw new ArgumentException(string.Format("The value must be less than {0} characters in lenght.", EXP_SZ_LINK.MAX_PATH));
                }

                EXP_SZ_LINK exp_sz_link = EXP_SZ_LINK.AnEmptyOne();

                value.CopyTo(0, exp_sz_link.swzTarget, 0, exp_sz_link.swzTarget.Length - 1);
                exp_sz_link.swzTarget[value.Length] = '\0';

                exp_sz_link.szTarget.Initialize(); // make this all zeros.

                GCHandle handle = GCHandle.Alloc(value, GCHandleType.Pinned); // pin the value

                dataList.AddDataBlock(GCHandle.ToIntPtr(handle));

                handle.Free(); // un-pin the value
            }
        }

        /// <summary>
        ///   Is true if the shell link has an EXP_SZ_LINK datablock with the EXP_SZ_ICON_SIG signature.
        /// </summary>
        public bool HasExpSzIcon {
            get {
                IntPtr ppDataBlock;
                Int32 hr = dataList.CopyDataBlock(EXP_SZ_ICON.EXP_SZ_ICON_SIG, out ppDataBlock);

                if (hr < 0) {
                    return false;
                }
                else {
                    Marshal.FreeHGlobal(ppDataBlock);
                    return true;
                }
            }
        }

        /// <summary>
        ///   Get and sets the EXP_SZ_ICON property for a shell link. If there is no link then the property 
        ///   value is an empty string. Setting this to null, an empty string, or a string that is all white space 
        ///   removes the EXP_SZ_LINK data block with the EXP_SZ_ICON_SIG signature from the assocated shell link.
        /// </summary>
        public string ExpSzIcon {
            get {
                IntPtr ppDataBlock;
                Int32 hr = dataList.CopyDataBlock(EXP_SZ_ICON.EXP_SZ_ICON_SIG, out ppDataBlock);

                if (hr < 0) {
                    return string.Empty;
                }

                var exp_sz_icon = (EXP_SZ_ICON) Marshal.PtrToStructure(ppDataBlock, typeof (EXP_SZ_ICON));
                Marshal.FreeHGlobal(ppDataBlock);
                var value = new string(exp_sz_icon.swzTarget);
                return value;
            }
            set {
                dataList.RemoveDataBlock(EXP_SZ_ICON.EXP_SZ_ICON_SIG);

                if (string.IsNullOrWhiteSpace(value)) {
                    return;
                }

                if (value.Length >= EXP_SZ_ICON.MAX_PATH) {
                    throw new ArgumentException(string.Format("The value must be less than {0} characters in length.", EXP_SZ_ICON.MAX_PATH));
                }

                EXP_SZ_ICON exp_sz_link = EXP_SZ_ICON.AnEmptyOne();

                value.CopyTo(0, exp_sz_link.swzTarget, 0, exp_sz_link.swzTarget.Length - 1);
                exp_sz_link.swzTarget[value.Length] = '\0';

                exp_sz_link.szTarget.Initialize(); // make this all zeros.

                GCHandle handle = GCHandle.Alloc(value, GCHandleType.Pinned); // pin the value

                dataList.AddDataBlock(GCHandle.ToIntPtr(handle));

                handle.Free(); // un-pin the value
            }
        }

        /// <summary>
        ///   True if the shell link ha a EXP_DARWIN_LINK data block.
        /// </summary>
        public bool HasDarwinLink {
            get {
                IntPtr ppDataBlock;
                Int32 hr = dataList.CopyDataBlock(EXP_DARWIN_LINK.EXP_DARWIN_ID_SIG, out ppDataBlock);

                if (hr < 0) {
                    return false;
                }
                else {
                    Marshal.FreeHGlobal(ppDataBlock);
                    return true;
                }
            }
        }

        /// <summary>
        ///   Get and sets the EXP_DARWIN_LINK property for a shell link. If there is no link then the property 
        ///   value is an empty string. Setting this to null, an empty string, or a string that is all white space 
        ///   removes the EXP_DARWIN_LINK data block from the assocated shell link.
        /// </summary>
        public string DarwinLink {
            get {
                IntPtr ppDataBlock;
                Int32 hr = dataList.CopyDataBlock(EXP_DARWIN_LINK.EXP_DARWIN_ID_SIG, out ppDataBlock);

                if (hr < 0) {
                    return string.Empty;
                }

                var exp_darwin_link = (EXP_DARWIN_LINK) Marshal.PtrToStructure(ppDataBlock, typeof (EXP_DARWIN_LINK));
                Marshal.FreeHGlobal(ppDataBlock);
                var value = new string(exp_darwin_link.szwDarwinID);
                return value;
            }
            set {
                dataList.RemoveDataBlock(EXP_DARWIN_LINK.EXP_DARWIN_ID_SIG);

                if (string.IsNullOrWhiteSpace(value)) {
                    return;
                }

                if (value.Length >= EXP_DARWIN_LINK.MAX_PATH) {
                    throw new ArgumentException(string.Format("The value must be less than {0} characters in lenght.", EXP_SZ_ICON.MAX_PATH));
                }

                EXP_DARWIN_LINK exp_darwin_link = EXP_DARWIN_LINK.AnEmptyOne();

                value.CopyTo(0, exp_darwin_link.szwDarwinID, 0, exp_darwin_link.szwDarwinID.Length - 1);
                exp_darwin_link.szwDarwinID[value.Length] = '\0';

                exp_darwin_link.szDarwinID.Initialize(); // make this all zeros.

                GCHandle handle = GCHandle.Alloc(value, GCHandleType.Pinned); // pin the value

                dataList.AddDataBlock(GCHandle.ToIntPtr(handle));

                handle.Free(); // un-pin the value
            }
        }

        #region Internal Shell Support

        /// <summary>
        ///   Removes a data block
        /// </summary>
        /// <param name = "signature">The signature of the data block</param>
        /// <exception cref = "ArgumentException">Thrown if the signature is not supported.</exception>
        internal void RemoveData(UInt32 signature) {
            switch (signature) {
                case NT_CONSOLE_PROPS.NT_CONSOLE_PROPS_SIG:
                case NT_FE_CONSOLE_PROPS.NT_FE_CONSOLE_PROPS_SIG:
                case EXP_SZ_LINK.EXP_SZ_LINK_SIG:
                case EXP_SZ_ICON.EXP_SZ_ICON_SIG:
                case EXP_SPECIAL_FOLDER.EXP_SPECIAL_FOLDER_SIG:
                case EXP_DARWIN_LINK.EXP_DARWIN_ID_SIG:
                    dataList.RemoveDataBlock(signature);
                    return;

                default:
                    throw new ArgumentException("signature is invalid.");
            }
        }

        /// <summary>
        ///   Read the console properties from the shell link
        /// </summary>
        /// <returns>True if they exists and were read.  False if they did not exist.</returns>
        internal bool ReadConsoleProperties() {
            IntPtr ppDataBlock;
            Int32 hr = dataList.CopyDataBlock(NT_CONSOLE_PROPS.NT_CONSOLE_PROPS_SIG, out ppDataBlock);

            if (hr < 0) {
                return false;
            }

            var nt_console_props = (NT_CONSOLE_PROPS) Marshal.PtrToStructure(ppDataBlock, typeof (NT_CONSOLE_PROPS));
            Marshal.FreeHGlobal(ppDataBlock);

            this.consoleProperties.nt_console_props = nt_console_props;

            return true;
        }

        /// <summary>
        ///   Write the current NT_CONSOLE_PROPS properties to the link.
        /// </summary>
        internal void WriteConsoleProperties() {
            RemoveData(NT_CONSOLE_PROPS.NT_CONSOLE_PROPS_SIG);

            IntPtr dataBlock = Marshal.AllocCoTaskMem(Marshal.SizeOf(this.consoleProperties.nt_console_props));

            Marshal.StructureToPtr(this.consoleProperties.nt_console_props, dataBlock, false);

            dataList.AddDataBlock(dataBlock);

            Marshal.FreeCoTaskMem(dataBlock);
        }

        /// <summary>
        ///   Get and sets the Special Folder property for a shell link.
        /// </summary>
        internal EXP_SPECIAL_FOLDER ExpSpecialFolder {
            get {
                EXP_SPECIAL_FOLDER value;
                IntPtr ppDataBlock;
                Int32 hr = dataList.CopyDataBlock(EXP_SPECIAL_FOLDER.EXP_SPECIAL_FOLDER_SIG, out ppDataBlock);

                if (hr < 0) {
                    return new EXP_SPECIAL_FOLDER();
                }

                value = (EXP_SPECIAL_FOLDER) Marshal.PtrToStructure(ppDataBlock, typeof (EXP_SPECIAL_FOLDER));
                Marshal.FreeHGlobal(ppDataBlock);
                return value;
            }

            set {
                dataList.RemoveDataBlock(EXP_SPECIAL_FOLDER.EXP_SPECIAL_FOLDER_SIG);

                value.dbh.cbSize = unchecked((UInt32) Marshal.SizeOf(typeof (EXP_SPECIAL_FOLDER)));
                value.dbh.dwSignature = EXP_SPECIAL_FOLDER.EXP_SPECIAL_FOLDER_SIG;

                GCHandle handle = GCHandle.Alloc(value, GCHandleType.Pinned); // pin the value

                dataList.AddDataBlock(GCHandle.ToIntPtr(handle));

                handle.Free(); // un-pin the value
            }
        }

        #endregion
    }
}