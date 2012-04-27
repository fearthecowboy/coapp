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
    using System.Windows.Forms;

    /// <summary>
    ///   Defines a delegate for Message handling
    /// </summary>
    public delegate void MessageEventHandler(object sender, ref Message msg, ref bool handled);

    /// <summary>
    ///   Inherits from System.Windows.Form.NativeWindow. Provides an Event for Message handling
    /// </summary>
    public class NativeWindowWithEvent : NativeWindow, IDisposable {
        private static NativeWindowWithEvent _instance;
        private static readonly Object InstanceLock = new Object();

        protected NativeWindowWithEvent() {
            var parms = new CreateParams();
            CreateHandle(parms);
        }

        public static NativeWindowWithEvent Instance {
            get {
                lock (InstanceLock) {
                    return _instance ?? (_instance = new NativeWindowWithEvent());
                }
            }
        }

        #region IDisposable Members

        public void Dispose() {
            if (Handle != IntPtr.Zero) {
                DestroyHandle();
            }
        }

        #endregion

        public event MessageEventHandler ProcessMessage;

        protected override void WndProc(ref Message m) {
            var handled = false;

            if (ProcessMessage != null) {
                ProcessMessage(this, ref m, ref handled);
            }

            if (!handled) {
                base.WndProc(ref m);
            }
        }
    }
}