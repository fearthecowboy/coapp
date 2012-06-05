//-----------------------------------------------------------------------
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

namespace CoApp.ElevationProxy {
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.IO.Pipes;
    using System.Linq;
    using System.Reflection;
    using System.Runtime.InteropServices;
    using System.Security.AccessControl;
    using System.Security.Principal;
    using System.Text;
    using System.Threading.Tasks;
    using System.Windows.Forms;

    public class ElevationProxyMain {
        internal const int BufferSize = 1024*1024*2;

        [StructLayout(LayoutKind.Sequential)]
        public struct SidIdentifierAuthority {
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 6, ArraySubType = UnmanagedType.I1)]
            public byte[] Value;
        }

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("advapi32.dll", EntryPoint = "AllocateAndInitializeSid")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool AllocateAndInitializeSid([In] ref SidIdentifierAuthority pIdentifierAuthority, byte nSubAuthorityCount, uint nSubAuthority0, uint nSubAuthority1, uint nSubAuthority2, uint nSubAuthority3, uint nSubAuthority4,
            uint nSubAuthority5, int nSubAuthority6, uint nSubAuthority7, out IntPtr pSid);

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool CheckTokenMembership(IntPtr TokenHandle, IntPtr SidToCheck, out bool IsMember);

        /// <summary>
        /// </summary>
        private PipeSecurity _pipeSecurity;

        private string ServicePipeName = "CoAppInstaller";
        private string ClientPipeName = "RANDOMSTRING";

        private NamedPipeClientStream _toServerPipe;
        private NamedPipeServerStream _fromClientPipe;


        internal static void ElevateSelf() {
            try {
                var ntAuth = new SidIdentifierAuthority();
                ntAuth.Value = new byte[] {0, 0, 0, 0, 0, 5};

                var psid = IntPtr.Zero;
                bool isAdmin;
                if (AllocateAndInitializeSid(ref ntAuth, 2, 0x00000020, 0x00000220, 0, 0, 0, 0, 0, 0, out psid) && CheckTokenMembership(IntPtr.Zero, psid, out isAdmin) && isAdmin) {
                    return; // yes, we're an elevated admin
                }
            } catch {
                // :) Seems that we need to elevate?
            }

            // we're not an admin I guess.
            try {
                var process = new Process {
                    StartInfo = {
                        UseShellExecute = true,
                        WorkingDirectory = Environment.CurrentDirectory,
                        FileName = Assembly.GetEntryAssembly().Location,
                        Verb = "runas",
                        Arguments = Environment.GetCommandLineArgs().Skip(1).Aggregate(string.Empty, (current, each) => current + " \"" + each + "\"").Trim(),
                        ErrorDialog = true,
                        ErrorDialogParentHandle = GetForegroundWindow(),
                        WindowStyle = ProcessWindowStyle.Maximized,
                    }
                };

                if (!process.Start()) {
                    throw new Exception();
                }

                // since we want the parent process to be able to wait for this, we'll wait for the child process.
                process.WaitForExit();
            } catch {
                // nWindow.Fail(LocalizedMessage.IDS_REQUIRES_ADMIN_RIGHTS, "The installer requires administrator permissions.");
            }

            // we should have elevated, or failed to. either way, GTFO.
            Environment.Exit(0);
        }

        internal void ConnectToCoApp() {
            for (var count = 0; count < 5; count++) {
                _toServerPipe = new NamedPipeClientStream(".", ServicePipeName, PipeDirection.InOut, PipeOptions.Asynchronous, TokenImpersonationLevel.Impersonation);
                try {
                    _toServerPipe.Connect(500);
                    _toServerPipe.ReadMode = PipeTransmissionMode.Message;
                    break;
                }
                catch (Exception e) {
                    System.Windows.Forms.MessageBox.Show(string.Format("{0} - {1}\r\n{2}", e.GetType(), e.Message, e.StackTrace), "CoApp Elevation Proxy", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    _toServerPipe = null;
                }
            }

            if (_toServerPipe == null) {
                throw new Exception("Unable to connect to CoApp Service");
            }
        }


        private void ListenFromClient() {
            try {
                _pipeSecurity = new PipeSecurity();
                _pipeSecurity.AddAccessRule(new PipeAccessRule(new SecurityIdentifier(WellKnownSidType.WorldSid, null), PipeAccessRights.ReadWrite, AccessControlType.Allow));
                _pipeSecurity.AddAccessRule(new PipeAccessRule(WindowsIdentity.GetCurrent().Owner, PipeAccessRights.FullControl, AccessControlType.Allow));
                _fromClientPipe = new NamedPipeServerStream(ClientPipeName, PipeDirection.InOut, 10, PipeTransmissionMode.Message, PipeOptions.Asynchronous, BufferSize, BufferSize, _pipeSecurity);
                _fromClientPipe.WaitForConnection();
            }  catch( Exception e ) {
                System.Windows.Forms.MessageBox.Show(string.Format("{0} - {1}\r\n{2}", e.GetType(), e.Message, e.StackTrace), "CoApp Elevation Proxy", MessageBoxButtons.OK, MessageBoxIcon.Error);
                _fromClientPipe = null;
            }

            if (_toServerPipe == null) {
                throw new Exception("Unable to listen for CoApp client");
            }
        }

        private static void Main(string[] args) {

            if( args.Length <1 ) {
                System.Windows.Forms.MessageBox.Show("Required: PipeName", "CoApp Elevation Proxy", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            ElevateSelf();
            var epm = new ElevationProxyMain {
                ClientPipeName = args[0]
            };
            
            epm.Go();
        }

        private void AsyncCopyTo( Stream fromPipe, Stream toPipe ) {
            var buffer = new byte[BufferSize];
            fromPipe.BeginRead(buffer, 0, BufferSize, ar => {
                var i = fromPipe.EndRead(ar);
                if (i > 0) {
                    toPipe.Write(buffer, 0, i);
                }
            }, null).AsyncWaitHandle.WaitOne();
        }

        private void Go() {
            try {
                ConnectToCoApp();
                ListenFromClient();

                var t1 = Task.Factory.StartNew(() => {
                    while (_toServerPipe.IsConnected && _fromClientPipe.IsConnected) {
                        AsyncCopyTo(_fromClientPipe, _toServerPipe);
                    }
                });

                var t2 = Task.Factory.StartNew(() => {
                    while (_toServerPipe.IsConnected && _fromClientPipe.IsConnected) {
                        AsyncCopyTo(_toServerPipe, _fromClientPipe);
                    }
                });

                Task.WaitAny(new[] {t1, t2});
                _fromClientPipe.Close();
                _toServerPipe.Close();
            } catch( Exception e ) {
                MessageBox.Show(string.Format("{0} - {1}\r\n{2}", e.GetType(), e.Message, e.StackTrace), "CoApp Elevation Proxy", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}