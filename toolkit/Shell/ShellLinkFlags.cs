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
// -----------------------------------------------------------------------

namespace CoApp.Toolkit.Shell {
    using System;

    /// <summary>
    ///   These flag values mapp to the native SHELL_LINK_DATA_FLAGS Enumeration. See MSDN
    /// </summary>
    [Flags]
    public enum ShellLinkFlags {
        /// <summary>
        ///   Default value used when no other flag is explicitly set.
        /// </summary>
        None = 0x00000000,

        /// <summary>
        ///   Default value used when no other flag is explicitly set.
        /// </summary>
        Default = 0x00000000,

        /// <summary>
        ///   The Shell link was saved with an ID list
        /// </summary>
        HasIdList = 0x00000001,

        /// <summary>
        ///   The Shell link was saved with link information to enable distributed tracking. This information is used by .lnk files to locate the target if the targets's path has changed. It includes information such as volume label and serial number, although the specific stored information can change from release to release.
        /// </summary>
        HasLinkInfo = 0x00000002,

        /// <summary>
        ///   The link has a name.
        /// </summary>
        HasName = 0x00000004,

        /// <summary>
        ///   The link has a relative path.
        /// </summary>
        HasRelativePath = 0x00000008,

        /// <summary>
        ///   The link has a working directory.
        /// </summary>
        HasWorkingDirectory = 0x00000010,

        /// <summary>
        ///   The link has arguments.
        /// </summary>
        HasArguments = 0x00000020,

        /// <summary>
        ///   The link has an icon location.
        /// </summary>
        HasIconLocation = 0x00000040,

        /// <summary>
        ///   Stored strings are Unicode.
        /// </summary>
        Unicode = 0x00000080,

        /// <summary>
        ///   Prevents the storage of link tracking information. If this flag is set, it is less likely, though not impossible, that a target can be found by the link if that target is moved.
        /// </summary>
        ForceNoLinkInfo = 0x00000100,

        /// <summary>
        ///   The link contains expandable environment strings such as %windir%.
        /// </summary>
        HasExpSz = 0x00000200,

        /// <summary>
        ///   Causes a 16-bit target application to run in a separate Virtual DOS Machine (VDM)/Windows on Windows (WOW).
        /// </summary>
        RunInSeparate = 0x00000400,

        /// <summary>
        ///   Not supported. Note that as of Windows Vista, this value is no longer defined.
        /// </summary>
        HasLogo3Id = 0x00000800,

        /// <summary>
        ///   The link is a special Windows Installer link.
        /// </summary>
        HasDarwinId = 0x00001000,

        /// <summary>
        ///   Causes the target application to run as a different user.
        /// </summary>
        RunAsUser = 0x00002000,

        /// <summary>
        ///   The icon path in the link contains an expandable environment string such as such as %windir%.
        /// </summary>
        HasExpIconSz = 0x00004000,

        /// <summary>
        ///   Prevents the use of ID list alias mapping when parsing the ID list from the path.
        /// </summary>
        NoPidlAlias = 0x00008000,

        /// <summary>
        ///   Forces the use of the UNC name (a full network resource name), rather than the local name
        /// </summary>
        ForceUncName = 0x00010000,

        /// <summary>
        ///   Causes the target of this link to launch with a shim layer active. A shim is an intermediate DLL that facilitates compatibility between otherwise incompatible software services. Shims are typically used to provide version compatibility.
        /// </summary>
        RunWithShimLayer = 0x00020000,

        /// <summary>
        ///   Windows Vista and later. Disable object ID distributed tracking information.
        /// </summary>
        ForceNoLinkTrack = 0x00040000,

        /// <summary>
        ///   Windows Vista and later. Enable the caching of target metadata into the link file.
        /// </summary>
        EnableTargetMetadata = 0x000800000,

        /// <summary>
        ///   Windows 7 and later. Disable shell link tracking.
        /// </summary>
        DisableLinkpathTracking = 0x00100000,

        /// <summary>
        ///   Windows Vista and later. Disable known folder tracking information.
        /// </summary>
        DisableKnownFolderRelativeTracking = 0x00200000,

        /// <summary>
        ///   Windows 7 and later. Disable known folder alias mapping when loading the IDList during deserialization.
        /// </summary>
        NoKfAlias = 0x00400000,

        /// <summary>
        ///   Windows 7 and later. Allow link to point to another shell link as long as this does not create cycles.
        /// </summary>
        AllowLinkToLInk = 0x00800000,

        /// <summary>
        ///   Windows 7 and later. Remove alias when saving the IDList
        /// </summary>
        UnaliasOnSave = 0x01000000,

        /// <summary>
        ///   Windows 7 and later. Recalculate the IDList from the path with the environmental variables at load time, rather than persisting the IDList.
        /// </summary>
        PreferEnvironmentPath = 0x02000000,

        /// <summary>
        ///   Windows 7 and later. If the target is a UNC location on a local machine, keep the local IDList target in addition to the remote target.
        /// </summary>
        KeepLocalIDListForUncTarget = 0x04000000,

        /// <summary>
        ///   Valid values for W7
        /// </summary>
        W7Valid = 0x07FFF7FF,

        /// <summary>
        ///   Valid Values For W8
        /// </summary>
        VistaValid = 0x003FF7FF,

        /// <summary>
        ///   Reserved, do not use
        /// </summary>
        Reserved = -2147483648
    }
}