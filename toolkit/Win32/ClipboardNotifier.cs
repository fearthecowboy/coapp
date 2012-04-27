//-----------------------------------------------------------------------
// <copyright company="CoApp Project">
//     Copyright (c) 2011 Garrett Serack . All rights reserved.
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

    public class ClipboardNotifier : Component {
        private IntPtr nextWindow;

        public ClipboardNotifier(IContainer container) : this() {
            container.Add(this);
        }

        public ClipboardNotifier() {
            if (!DesignMode) {
                NativeWindowWithEvent.Instance.ProcessMessage += MessageEvent;
                nextWindow = User32.SetClipboardViewer(NativeWindowWithEvent.Instance.Handle);
            }
        }

        public event EventHandler Changed;

        protected override void Dispose(bool disposing) {
            NativeWindowWithEvent.Instance.ProcessMessage -= MessageEvent;
            User32.ChangeClipboardChain(NativeWindowWithEvent.Instance.Handle, nextWindow);
            base.Dispose(disposing);
        }

        protected void MessageEvent(object sender, ref Message m, ref bool handled) {
            //Handle WM_Hotkey event
            if (handled) {
                return;
            }

            switch ((Win32Msgs)m.Msg) {
                case Win32Msgs.WM_DRAWCLIPBOARD:
                    handled = true;

                    if (Changed != null) {
                        Changed(this, EventArgs.Empty);
                    }

                    User32.SendMessage(nextWindow, m.Msg, m.WParam, m.LParam);

                    break;

                case Win32Msgs.WM_CHANGECBCHAIN:
                    if (m.WParam == nextWindow) {
                        nextWindow = m.LParam;
                    } else {
                        User32.SendMessage(nextWindow, m.Msg, m.WParam, m.LParam);
                    }
                    break;
            }
        }
    }
}