//-----------------------------------------------------------------------
// <copyright company="CoApp Project">
//     Copyright (c) 2011  Garrett Serack. All rights reserved.
// </copyright>
// <license>
//     The software is licensed under the Apache 2.0 License (the "License")
//     You may not use the software except in compliance with the License. 
// </license>
//-----------------------------------------------------------------------

namespace CoApp.Toolkit.Win32 {
    using System;
    using System.ComponentModel;
    using System.Windows.Forms;

    /// <summary>
    ///   Handles a System Hotkey
    /// </summary>
    public class SystemHotkey : Component {
        private int code;

        protected Keys hotKey = Keys.None;

        public SystemHotkey(IContainer container) {
            container.Add(this);

            NativeWindowWithEvent.Instance.ProcessMessage += MessageEvent;
        }

        public SystemHotkey() {
            if (!DesignMode) {
                NativeWindowWithEvent.Instance.ProcessMessage += MessageEvent;
            }
        }

        public bool IsRegistered { set; get; }

        public Keys Shortcut {
            get {
                return hotKey;
            }
            set {
                if (DesignMode) {
                    return; //Don't register in Designmode
                }

                if ((IsRegistered) && (hotKey != value)) //Unregister previous registered Hotkey
                {
                    if (!UnregisterHotkey()) {
                        if (Error != null) {
                            Error(this, EventArgs.Empty);
                        }
                    }
                }

                hotKey = value;

                if (value != Keys.None) {
                    if (!RegisterHotkey(value)) {
                        if (Error != null) {
                            Error(this, EventArgs.Empty);
                        }
                    }
                }
            }
        }

        public event EventHandler Pressed;
        public event EventHandler Error;

        protected override void Dispose(bool disposing) {
            if (IsRegistered) {
                UnregisterHotkey();
            }
            NativeWindowWithEvent.Instance.ProcessMessage -= MessageEvent;
            base.Dispose(disposing);
        }

        protected void MessageEvent(object sender, ref Message m, ref bool handled) {
            if ((m.Msg == (int)Win32Msgs.WM_HOTKEY)) {
                if ((m.WParam == (IntPtr)code)) {
                    handled = true;

                    if (Pressed != null) {
                        Pressed(this, EventArgs.Empty);
                    }
                }
            }
        }

        protected bool UnregisterHotkey() {
            var result = false;
            if (IsRegistered) {
                result = User32.UnregisterHotKey(NativeWindowWithEvent.Instance.Handle, code);
            }
            IsRegistered = false;
            return result;
        }

        protected bool RegisterHotkey(Keys key) {
            var win32Key = key & ~(Keys.Alt | Keys.Control | Keys.Shift);
            var mod = ((key & Keys.Alt) != Keys.None ? KeyModifiers.MOD_ALT : 0) | ((key & Keys.Shift) != Keys.None ? KeyModifiers.MOD_SHIFT : 0) |
                ((key & Keys.Control) != Keys.None ? KeyModifiers.MOD_CONTROL : 0);
            code = ((int)mod << 16) + (int)win32Key;
            IsRegistered = User32.RegisterHotKey(NativeWindowWithEvent.Instance.Handle, code, (int)mod, (int)win32Key);

            return IsRegistered;
        }
    }
}