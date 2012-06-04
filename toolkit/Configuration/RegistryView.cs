//-----------------------------------------------------------------------
// <copyright company="CoApp Project">
//     Copyright (c) 2010 Garrett Serack. All rights reserved.
// </copyright>
// <license>
//     The software is licensed under the Apache 2.0 License (the "License")
//     You may not use the software except in compliance with the License. 
// </license>
//-----------------------------------------------------------------------

namespace CoApp.Toolkit.Configuration {
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using Extensions;
    using Microsoft.Win32;
    using Pipes;

    /// <summary>
    ///   A simplified view into the registry
    /// </summary>
    /// <remarks>
    /// </remarks>
    public class RegistryView {
        /// <summary>
        ///   Backing field for ClassesRoot
        /// </summary>
        private static readonly Lazy<RegistryView> _classesRoot = new Lazy<RegistryView>(() => new RegistryView(RegistryKey.OpenBaseKey(RegistryHive.ClassesRoot, Microsoft.Win32.RegistryView.Registry64)));

        /// <summary>
        ///   Backing field for System
        /// </summary>
        private static readonly Lazy<RegistryView> _system = new Lazy<RegistryView>(() => new RegistryView(RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, Microsoft.Win32.RegistryView.Registry64)));

        /// <summary>
        ///   Backing field for User
        /// </summary>
        private static readonly Lazy<RegistryView> _user = new Lazy<RegistryView>(() => new RegistryView(RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, Microsoft.Win32.RegistryView.Registry64)));

        /// <summary>
        ///   Backing field for CoAppSystem
        /// </summary>
        private static readonly Lazy<RegistryView> _coappSystem = new Lazy<RegistryView>(() => System[@"Software\CoApp\"]);

        /// <summary>
        ///   Backing field for CoAppUser
        /// </summary>
        private static readonly Lazy<RegistryView> _coappUser = new Lazy<RegistryView>(() => User[@"Software\CoApp\"]);

        /// <summary>
        ///   Backing field for ApplicationSystem
        /// </summary>
        private static readonly Lazy<RegistryView> _applicationSystem = new Lazy<RegistryView>(() => CoAppSystem[Assembly.GetEntryAssembly().Title()]);

        /// <summary>
        ///   Backing field for Application User
        /// </summary>
        private static readonly Lazy<RegistryView> _applicationUser = new Lazy<RegistryView>(() => CoAppUser[Assembly.GetEntryAssembly().Title()]);

        /// <summary>
        ///   Gets the View for the HKLM registry hive.
        /// </summary>
        /// <remarks>
        /// </remarks>
        public static RegistryView System {
            get {
                return _system.Value;
            }
        }

        /// <summary>
        ///   Gets the View for the HKCU registry hive.
        /// </summary>
        /// <remarks>
        /// </remarks>
        public static RegistryView User {
            get {
                return _user.Value;
            }
        }

        /// <summary>
        ///   Gets the View for the HKLM\Software\CoApp registry node. Used for apps to have system-level data for CoApp Applications
        /// </summary>
        /// <remarks>
        /// </remarks>
        public static RegistryView CoAppSystem {
            get {
                return _coappSystem.Value;
            }
        }

        /// <summary>
        ///   Gets the View for the HKCU\Software\CoApp registry node. Used for apps to have user-specific data for CoApp Applications
        /// </summary>
        /// <remarks>
        /// </remarks>
        public static RegistryView CoAppUser {
            get {
                return _coappUser.Value;
            }
        }

        /// <summary>
        ///   Gets the system-level, application specific Registry View. Appends the Application Name to the Registry Key
        /// </summary>
        /// <remarks>
        /// </remarks>
        public static RegistryView ApplicationSystem {
            get {
                return _applicationSystem.Value;
            }
        }

        /// <summary>
        ///   Gets the user-specific, application specific Registry View. Appends the Application Name to the Registry Key
        /// </summary>
        /// <remarks>
        /// </remarks>
        public static RegistryView ApplicationUser {
            get {
                return _applicationUser.Value;
            }
        }

        /// <summary>
        ///   Gets the View for the HKCR registry hive.
        /// </summary>
        /// <remarks>
        /// </remarks>
        public static RegistryView ClassesRoot {
            get {
                return _classesRoot.Value;
            }
        }

        /// <summary>
        ///   The current registry key that this object is a view into
        /// </summary>
        private readonly RegistryKey _rootKey;

        /// <summary>
        ///   the subkey below the root key
        /// </summary>
        private readonly string _subKey;

        /// <summary>
        ///   the registry value name
        /// </summary>
        private readonly string _valueName;

        /// <summary>
        ///   Initializes a new instance of the <see cref="RegistryView" /> class. Sets the root to the given registry key, no subkey, no valuename
        /// </summary>
        /// <param name="rootKey"> The root key. </param>
        /// <remarks>
        /// </remarks>
        protected RegistryView(RegistryKey rootKey) {
            _rootKey = rootKey;
            _subKey = null;
            _valueName = null;
        }

        /// <summary>
        ///   Initializes a new instance of the <see cref="RegistryView" /> class. Sets the root to the given registry key, uses the given string to set the subkey and valuename (subkey and valuename are delimited by the first hash character (#) Ensures that the key doesn't start or end with backslashes Ensures that the valuename doesn't start or end with hashes
        /// </summary>
        /// <param name="rootKey"> The root key. </param>
        /// <param name="subKey"> The sub key. </param>
        /// <remarks>
        /// </remarks>
        protected RegistryView(RegistryKey rootKey, string subKey) {
            _rootKey = rootKey;

            var p = subKey.IndexOf("#");
            _subKey = ((p > -1) ? subKey.Substring(0, p) : subKey).Trim('\\');
            _valueName = (p > -1) ? subKey.Substring(p + 1).Trim('#') : null;
        }

        /// <summary>
        ///   Initializes a new instance of the <see cref="RegistryView" /> class. Sets the root to the given registry key, uses the given strings to set the subkey and valuename Ensures that the key doesn't start or end with backslashes Ensures that the valuename doesn't start or end with hashes
        /// </summary>
        /// <param name="rootKey"> The root key. </param>
        /// <param name="subKey"> The sub key. </param>
        /// <param name="valuename"> The valuename. </param>
        /// <remarks>
        /// </remarks>
        protected RegistryView(RegistryKey rootKey, string subKey, string valuename) {
            _rootKey = rootKey;
            _subKey = subKey.Trim('\\');
            _valueName = valuename.Trim('#');
        }

        public bool IsVolatile { get; set; }

        /// <summary>
        ///   Gets the RegistryKey used for reading values.
        /// </summary>
        /// <remarks>
        /// </remarks>
        private RegistryKey ReadableKey {
            get {
                try {
                    return string.IsNullOrEmpty(_subKey) ? _rootKey : _rootKey.OpenSubKey(_subKey);
                } catch {
                    return null;
                }
            }
        }

        /// <summary>
        ///   Gets the RegistryKey used for writing values.
        /// </summary>
        /// <remarks>
        /// </remarks>
        private RegistryKey WriteableKey {
            get {
                try {
                    return string.IsNullOrEmpty(_subKey) ? _rootKey : (IsVolatile ? _rootKey.CreateSubKey(_subKey, RegistryKeyPermissionCheck.ReadWriteSubTree, RegistryOptions.Volatile) : _rootKey.CreateSubKey(_subKey));
                } catch {
                    return null;
                }
            }
        }

        /// <summary>
        ///   Gets the list of subkeys the current node contains.
        /// </summary>
        /// <remarks>
        /// </remarks>
        public IEnumerable<string> Subkeys {
            get {
                using (var key = ReadableKey) {
                    return key != null ? key.GetSubKeyNames() : Enumerable.Empty<string>();
                }
            }
        }

        /// <summary>
        ///   Gets or sets the value for this current node (sets the default value for the key if the valuename isn't set). Treats the value as a string value (REG_SZ)
        /// </summary>
        /// <value> The string value. </value>
        /// <remarks>
        /// </remarks>
        public string StringValue {
            get {
                return Value as string ?? string.Empty;
            }
            set {
                Value = value;
            }
        }

        /// <summary>
        ///   Gets or sets the value for this current node (sets the default value for the key if the valuename isn't set). Treats the value as an Int32 value (REG_DWORD). Attempts to coerce any string value to an integer. Will truncate a Int64 value down to an Int32 value Defaults to 0
        /// </summary>
        /// <value> The value as an integer. </value>
        /// <remarks>
        /// </remarks>
        public int IntValue {
            get {
                var result = 0;
                var value = Value;

                if (value is long || value is int) {
                    return (int)value;
                }

                if (value is string) {
                    Int32.TryParse(value as string, out result);
                }

                return result;
            }
            set {
                Value = value;
            }
        }

        /// <summary>
        ///   Gets or sets the value for this current node (sets the default value for the key if the valuename isn't set). Treats the value as an Int64 value (REG_DWORD). Attempts to coerce any string value to an Int64. Defaults to 0
        /// </summary>
        /// <value> The value as an Int64. </value>
        /// <remarks>
        /// </remarks>
        public long LongValue {
            get {
                long result = 0;
                var value = Value;

                if (value is long || value is int) {
                    return (long)value;
                }

                if (value is string) {
                    Int64.TryParse(value as string, out result);
                }

                return result;
            }
            set {
                Value = value;
            }
        }

        /// <summary>
        ///   Gets the value for this current node as an enum. Defaults to the enum's Default Value if parsing fails.
        /// </summary>
        /// <returns> the enum value of the node </returns>
        /// <remarks>
        /// </remarks>
        public T GetEnumValue<T>() where T : struct, IConvertible {
            return StringValue.CastToEnum<T>();
        }

        /// <summary>
        ///   Sets the value of the node to the textual represnentation of the enum
        /// </summary>
        /// <typeparam name="T"> </typeparam>
        /// <param name="value"> The value to set the node to. </param>
        /// <remarks>
        /// </remarks>
        public void SetEnumValue<T>(T value) where T : struct, IConvertible {
            StringValue = value.CastToString();
        }

        /// <summary>
        ///   Gets or sets the current node's value as an encrypted string (user-account encrypted for user values, machine-key encrypted for all others). Uses the DPAPI stuff way down deep.
        /// </summary>
        /// <value> The unencrypted string value. </value>
        /// <remarks>
        /// </remarks>
        public string EncryptedStringValue {
            get {
                return _rootKey == Registry.CurrentUser
                    ? (Value as IEnumerable<byte>).UnprotectForUser()
                    : (Value as IEnumerable<byte>).UnprotectForMachine();
            }
            set {
                Value = _rootKey == Registry.CurrentUser ? value.ProtectForUser() : value.ProtectForMachine();
            }
        }

        /// <summary>
        ///   Gets or sets the current node's value as an encrypted string (user-account encrypted for user values, machine-key encrypted for all others). Uses the DPAPI stuff way down deep.
        /// </summary>
        /// <value> The unencrypted string value. </value>
        /// <remarks>
        /// </remarks>
        public string[] EncryptedStringsValue {
            get {
                var serialized = _rootKey == Registry.CurrentUser
                    ? (Value as IEnumerable<byte>).UnprotectForUser()
                    : (Value as IEnumerable<byte>).UnprotectForMachine();
                if(! string.IsNullOrEmpty(serialized) ) {
                    return new UrlEncodedMessage(serialized).DeserializeTo<string[]>();
                }
                return new string[0];
            }
            set {
                Value = _rootKey == Registry.CurrentUser ?  value.Serialize().ToString().ProtectForUser() :  value.Serialize().ToString().ProtectForMachine();
            }
        }

        /// <summary>
        ///   Gets or sets the current node's value as an encrypted string of bytes (user-account encrypted for user values, machine-key encrypted for all others). Uses the DPAPI stuff way down deep.
        /// </summary>
        /// <value> The unencrypted value as a collection of bytes. </value>
        /// <remarks>
        /// </remarks>
        public IEnumerable<byte> EncryptedBinaryValue {
            get {
                return _rootKey == Registry.CurrentUser
                    ? (Value as IEnumerable<byte>).UnprotectBinaryForUser()
                    : (Value as IEnumerable<byte>).UnprotectBinaryForMachine();
            }
            set {
                Value = _rootKey == Registry.CurrentUser ? value.ProtectBinaryForUser() : value.ProtectBinaryForMachine();
            }
        }

        /// <summary>
        ///   Gets or sets the current node's value as a boolean value. recognizes 'true' or '1' as true, everthing else is considered false.
        /// </summary>
        /// <value> <c>true</c> if the node value is 1 or 'true'; otherwise, <c>false</c> . </value>
        /// <remarks>
        /// </remarks>
        public bool BoolValue {
            get {
                object value = Value as string ?? string.Empty;
                return (value.ToString().IsTrue() || value.ToString().Equals("1"));
            }
            set {
                Value = (value ? "true" : "false");
            }
        }

        /// <summary>
        ///   Gets or sets the current node as a collection of strings
        /// </summary>
        /// <value> The collection of strings. </value>
        /// <remarks>
        /// </remarks>
        public IEnumerable<string> StringsValue {
            get {
                var data = Value;
                if (data == null) {
                    return Enumerable.Empty<string>();
                }
                if (data is string) {
                    return new[] {data as string};
                }
                if (data is string[]) {
                    return data as string[];
                }
                return Enumerable.Empty<string>();
            }
            set {
                Value = value.ToArray();
            }
        }

        /// <summary>
        ///   Gets or sets the current node as a collection of bytes
        /// </summary>
        /// <value> The binary value. </value>
        /// <remarks>
        /// </remarks>
        public IEnumerable<byte> BinaryValue {
            get {
                return Value as byte[] ?? new byte[0];
            }
            set {
                Value = value.ToArray();
            }
        }

        /// <summary>
        ///   Raw accessor to get or set the value of the key Chooses the type for the value based on the object passed in.
        /// </summary>
        /// <value> The value to set the node to. </value>
        /// <remarks>
        /// </remarks>
        public object Value {
            get {
                using (var key = ReadableKey) {
                    return key != null ? key.GetValue(_valueName, null) : null;
                }
            }
            set {
                using (var key = WriteableKey) {
                    if (key != null) {
                        if (value == null) {
                            if (key.GetValue(_valueName, null) != null) {
                                key.DeleteValue(_valueName);
                            }
                        } else if (value is long) {
                            key.SetValue(_valueName, value, RegistryValueKind.QWord);
                        } else if (value is int) {
                            key.SetValue(_valueName, value, RegistryValueKind.DWord);
                        } else if (value is string[]) {
                            key.SetValue(_valueName, value, RegistryValueKind.MultiString);
                        } else if (value is byte[]) {
                            key.SetValue(_valueName, value, RegistryValueKind.Binary);
                        } else {
                            key.SetValue(_valueName, value);
                        }
                        // key.SetValue(_valueName,value,RegistryValueKind.ExpandString);
                    }
                }
            }
        }

        public IEnumerable<string> ValueNames {
            get {
                using (var key = ReadableKey) {
                    return key.GetValueNames();
                }
            }
        }

        public void DeleteValues() {
            using (var key = ReadableKey) {
                if (key != null) {
                    foreach (var v in key.GetValueNames()) {
                        key.DeleteValue(v);
                    }
                }
            }
        }

        public bool HasValue {
            get {
                using (var key = ReadableKey) {
                    return key != null && key.GetValue(_valueName, null) != null;
                }
            }
        }

        /// <summary>
        ///   Attempts to deletes the subkey. Silently fails without warning.
        /// </summary>
        /// <param name="subkey"> The subkey. </param>
        /// <remarks>
        /// </remarks>
        public void DeleteSubkey(string subkey) {
            using (var key = WriteableKey) {
                if (key != null) {
                    try {
                        key.DeleteSubKeyTree(subkey);
                    }
                        // ReSharper disable EmptyGeneralCatchClause
                    catch {
                        // ReSharper restore EmptyGeneralCatchClause
                    }
                }
            }
        }

        /// <summary>
        ///   Gets the <see cref="CoApp.Toolkit.Configuration.RegistryView" /> with the specified key and value name.
        /// </summary>
        /// <remarks>
        /// </remarks>
        public RegistryView this[string keyName, string valueName] {
            get {
                return new RegistryView(_rootKey, _subKey + @"\" + keyName, valueName) {IsVolatile = IsVolatile};
            }
        }

        /// <summary>
        ///   Gets the <see cref="CoApp.Toolkit.Configuration.RegistryView" /> with the specified key and value name (seperatedby hash character).
        /// </summary>
        /// <remarks>
        /// </remarks>
        public RegistryView this[string keyName] {
            get {
                return new RegistryView(_rootKey, _subKey + @"\" + keyName) {IsVolatile = IsVolatile};
                ;
            }
        }
    }
}