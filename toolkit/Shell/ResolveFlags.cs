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
    /// <summary>
    ///   These values map directly to the native SLR_* values for the IShellLink::Resolve() method
    /// </summary>
    public enum ResolveFlags {
        None = 0,

        /// <summary>
        ///   Do not display a dialog box if the link cannot be resolved.
        /// </summary>
        NoUi = 0x1,

        /// <summary>
        ///   Not used - ignored
        /// </summary>
        AnyMatch = 0x2,

        /// <summary>
        ///   If the link object has changed, update its path and list of identifiers. If SLR_UPDATE is set, you do not need to call IPersistFile::IsDirty to determine whether or not the link object has changed.
        /// </summary>
        Update = 0x4,

        /// <summary>
        ///   Do not update the link information.
        /// </summary>
        NoUpdate = 0x8,

        /// <summary>
        ///   Do not execute the search heuristics.
        /// </summary>
        NoSearch = 0x10,

        /// <summary>
        ///   Do not use distributed link tracking.
        /// </summary>
        NoTrack = 0x20,

        /// <summary>
        ///   Disable distributed link tracking. By default, distributed link tracking tracks removable media across multiple devices based on the volume name. It also uses the UNC path to track remote file systems whose drive letter has changed. Setting NoLinkInfo disables both types of tracking
        /// </summary>
        NoLinkInfo = 0x40,

        /// <summary>
        ///   Call the Windows Installer
        /// </summary>
        InvokeMSI = 0x80,

        /// <summary>
        ///   Windows XP and later.
        /// </summary>
        NoUIWithMessagePump = 0x101,

        /// <summary>
        ///   Windows 7 and later. Offer the option to delete the shortcut when this method is unable to resolve it, even if the shortcut is not a shortcut to a file.
        /// </summary>
        OfferDeleteWithoutFile = 0x201,

        /// <summary>
        ///   Windows 7 and later. Report as dirty if the target is a known folder and the known folder was redirected. This only works if the original target path was a file system path or ID list and not an aliased known folder ID list.
        /// </summary>
        KnownFolder = 0x400,

        /// <summary>
        ///   Windows 7 and later. Resolve the computer name in UNC targets that point to a local computer. This value is used with SLDF_KEEP_LOCAL_IDLIST_FOR_UNC_TARGET.
        /// </summary>
        MachineInLocaltarget = 0x800,

        /// <summary>
        ///   Windows 7 and later. Update the computer GUID and user SID if necessary.
        /// </summary>
        UpdateMachineAndSid = 0x1000
    }
}