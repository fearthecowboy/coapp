﻿//-----------------------------------------------------------------------
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
    using System.Collections.Generic;
    using System.Linq;
    using System.Security.Principal;
    using Common;
    using Exceptions;
    using Toolkit.Configuration;
    using Toolkit.Extensions;
    using Toolkit.Win32;

    public class PermissionPolicy {
        private static readonly RegistryView Policies = PackageManagerSettings.CoAppSettings["Policy"];
        private static readonly SecurityIdentifier AdministratorsGroup = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null);
        internal string Name;
        internal string Description;
        internal static IEnumerable<PermissionPolicy> AllPolicies = Enumerable.Empty<PermissionPolicy>();
        private readonly IEnumerable<WellKnownSidType> _defaults;
        private IEnumerable<SecurityIdentifier> _groups;

        private readonly RegistryView _policyView;

        internal static PermissionPolicy Connect;
        internal static PermissionPolicy EnumeratePackages;
        internal static PermissionPolicy UpdatePackage ;
        internal static PermissionPolicy InstallPackage;
        internal static PermissionPolicy RemovePackage ;
        internal static PermissionPolicy ChangeRequiredState ;
        internal static PermissionPolicy ChangeState;
        internal static PermissionPolicy EditSystemFeeds;
        internal static PermissionPolicy EditSessionFeeds;
        internal static PermissionPolicy PauseService;
        internal static PermissionPolicy StopService ;
        internal static PermissionPolicy ModifyPolicy ;
        internal static PermissionPolicy EditSchedule ;
        internal static PermissionPolicy Symlink;

        static PermissionPolicy() {
            Connect = new PermissionPolicy("Connect", "Allows access to communicate with the CoApp Service", new[] { WellKnownSidType.WorldSid });
            EnumeratePackages = new PermissionPolicy("EnumeratePackages", "Allows access to query the system for installed packages", new[] { WellKnownSidType.WorldSid });
            UpdatePackage = new PermissionPolicy("UpdatePackage", "Allows a newer version of an package that is currently installed to be installed", new[] { WellKnownSidType.WorldSid });

            InstallPackage = new PermissionPolicy("InstallPackage", "Allows a new package to be installed", new[] { WellKnownSidType.BuiltinAdministratorsSid });
            RemovePackage = new PermissionPolicy("RemovePackage", "Allows a package to be removed", new[] { WellKnownSidType.BuiltinAdministratorsSid });

            ChangeRequiredState = new PermissionPolicy("ChangeRequiredState", "Allows a user to change whether a given package is required (user requested)", new[] { WellKnownSidType.BuiltinAdministratorsSid });
            ChangeState = new PermissionPolicy("ChangeState", "Allows a user to change whether any state on a given package", new[] { WellKnownSidType.BuiltinAdministratorsSid });

            EditSystemFeeds = new PermissionPolicy("EditSystemFeeds", "Allows users to edit remembered feeds for the system", new[] { WellKnownSidType.BuiltinAdministratorsSid });
            EditSessionFeeds = new PermissionPolicy("EditSessionFeeds", "Allows users to edit remembered feeds for the session", new[] { WellKnownSidType.WorldSid });

            PauseService = new PermissionPolicy("PauseService", "Allows users to place the CoApp Service into a suspended (paused) state", new[] { WellKnownSidType.BuiltinAdministratorsSid });
            StopService = new PermissionPolicy("StopService", "Allows users to stop the CoApp Service", new[] { WellKnownSidType.BuiltinAdministratorsSid });
            ModifyPolicy = new PermissionPolicy("ModifyPolicy", "Allows users to change policy values for CoApp", new[] { WellKnownSidType.BuiltinAdministratorsSid });

            EditSchedule = new PermissionPolicy("EditSchedule", "Allows users to edit any CoApp scheduled tasks", new[] { WellKnownSidType.BuiltinAdministratorsSid });

            Symlink = new PermissionPolicy("Symlink", "Allows users to create and edit symlinks", new[] { WellKnownSidType.BuiltinAdministratorsSid });    
        }

        private PermissionPolicy(string name, string description, IEnumerable<WellKnownSidType> defaults) {
            Name = name;
            Description = description;
            _defaults = defaults;
            _policyView = Policies["#" + Name];
            Refresh();
            AllPolicies = AllPolicies.UnionSingleItem(this).ToArray();
        }

        internal IEnumerable<string> Sids {
            get {
                lock (this) {
                    return _groups.Select(each => each.ToString());
                }
            }
        }

        internal IEnumerable<string> Accounts {
            get {
                lock (this) {
                    return _groups.Select(each => each.Translate(typeof (NTAccount)) as NTAccount).Where(each => each != null).Select(each => each.ToString());
                }
            }
        }

        private void Refresh() {
            var policies = _policyView.StringsValue.ToArray();
            _groups = policies.Any() ? policies.Select(each => new SecurityIdentifier(each)) : _defaults.Select(each => new SecurityIdentifier(each, null));
        }

        private SecurityIdentifier FindSid(string account) {
            SecurityIdentifier sid;
            try {
                // first, let's try this as a sid (SDDL) string
                sid = new SecurityIdentifier(account);
                return sid;
            } catch {
            }

            try {
                // maybe it's an account/group name
                var name = new NTAccount(account);
                sid = (SecurityIdentifier)name.Translate(typeof (SecurityIdentifier));
                if (sid != null) {
                    return sid;
                }
            } catch {
            }

            throw new UnknownAccountException(account);
        }

        internal void Add(string account) {
            lock (this) {
                var sid = FindSid(account);

                if (!_groups.Contains(sid)) {
                    _policyView.StringsValue = _groups.UnionSingleItem(sid).Select(each => each.ToString());
                }
                Refresh();
            }
        }

        internal void Remove(string account) {
            lock (this) {
                if (account == "*") {
                    // reset to default.
                    _policyView.StringsValue = null;
                    Refresh();
                    return;
                }

                var sid = FindSid(account);

                if (_groups.Contains(sid)) {
                    _policyView.StringsValue = _groups.Where(each => each != sid).Select(each => each.ToString());
                }
                Refresh();
            }
        }

    
        /// <summary>
        ///   Determines whether the user has access to the policy. Run this while impersonating the user
        /// </summary>
        internal bool HasPermission {
            get {
                var principal = new WindowsPrincipal(WindowsIdentity.GetCurrent());

                if (WindowsVersionInfo.IsVistaOrBeyond) {
                    // manual check against administrator permissions.
                    if (_groups.Contains(AdministratorsGroup)) {
                        if (AdminPrivilege.IsProcessElevated()) {
                            return true;
                        }
                    }
                    return _groups.Where(each => each != AdministratorsGroup).Any(principal.IsInRole);
                }

                return _groups.Any(principal.IsInRole);
            }
        }
    }
}