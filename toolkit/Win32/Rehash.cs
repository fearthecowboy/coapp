//-----------------------------------------------------------------------
// <copyright company="CoApp Project">
//     Copyright (c) 2011 Garrett Serack . All rights reserved.
// </copyright>
// <license>
//     The software is licensed under the Apache 2.0 License (the "License")
//     You may not use the software except in compliance with the License. 
// </license>
//-----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CoApp.Toolkit.Win32 {
    using System.Diagnostics;
    using System.IO;
    using System.Reflection;
    using System.Security.Cryptography;
    using System.Threading;
    using System.Threading.Tasks;
    using Extensions;
    using Logging;
    using Microsoft.Win32.SafeHandles;

    internal static class Rehash {
        private static readonly SafeWaitHandle _globalResetEvent = Kernel32.CreateEvent(IntPtr.Zero, true, false, "Global\\CoApp.Reload.Environment");
        private static readonly Dictionary<ProcessorType, byte[]> _reHashDlls = new Dictionary<ProcessorType, byte[]>();
        
        static Rehash() {
            foreach( var arch in Enum.GetValues(typeof(ProcessorType)) ) {
                var rehashFilename = Assembly.GetExecutingAssembly().GetManifestResourceNames().Where(
                        each => each.Equals("CoApp.binaries.CoApp.Rehash.{0}.dll".format(arch.ToString()), StringComparison.CurrentCultureIgnoreCase)).
                        FirstOrDefault();

                if (string.IsNullOrEmpty(rehashFilename)) {
                    continue;
                }

                using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(rehashFilename)) {
                    if (stream == null) {
                        continue;
                    }

                    var dllFilename = Path.Combine(FilesystemExtensions.SystemTempFolder, rehashFilename);

                    if (File.Exists(dllFilename)) {
                        using (var existingFileStream = new FileStream(dllFilename, FileMode.Open, FileAccess.Read, FileShare.Read)) {
                            var streamHash = MD5.Create().ComputeHash(stream).ToHexString();
                            var fileHash = MD5.Create().ComputeHash(existingFileStream).ToHexString();

                            if (streamHash.Equals(fileHash, StringComparison.CurrentCultureIgnoreCase)) {
                                // oh, it's there already. you can use it.
                                _reHashDlls.Add((ProcessorType) arch, Encoding.ASCII.GetBytes(dllFilename));
                                continue;
                            }
                        }
                        // hmm. This dont' look like the one that we want 
                        dllFilename.TryHardToDelete();
                    }

                     // rewind the stream
                    stream.Position = 0;
                    using (var newFileStream = new FileStream(dllFilename, FileMode.Create)) {
                        stream.CopyTo(newFileStream);
                    }

                    _reHashDlls.Add((ProcessorType) arch, Encoding.ASCII.GetBytes(dllFilename));
                }
            }
        }

        internal static void ForceProcessToReloadEnvironment(params string[] processNames) {
            Task.Factory.StartNew(() => {
                foreach (var processName in processNames) {
                    var processes = Process.GetProcessesByName(processName);

                    // load the rehash dll into the target processes
                    if (processes.Any()) {
                        foreach (var proc in processes) {
                            //Logger.Message("Rehash: Going to rehash pid:{0} -- '{1}'", proc.Id, processName);
                            DoRehash(proc.Id);
                        }
                    }
                }

                // signal rehash to proceed.
                //Logger.Message("Rehash: Triggering Global Event");
                Kernel32.SetEvent(_globalResetEvent);
                Thread.Sleep(200); // give everyone a chance to wake up and do their job
                Kernel32.ResetEvent(_globalResetEvent);
            });
        }

        private static ProcessorType GetProcessProcessorType( SafeProcessHandle processHandle ) {
            if (processHandle == SafeProcessHandle.InvalidHandle) {
                return ProcessorType.Unknown;
            }

            if( WindowsVersionInfo.IsCurrentProcess64Bit ) {
                // on 64 bit windows, it either has to be x64 or x86
                var retVal = false;

                if (!Kernel32.IsWow64Process(processHandle, out retVal)) {
                    return ProcessorType.Unknown; // failure. Can't tell.
                }
                return retVal ? ProcessorType.X86 : ProcessorType.X64;
            }

            // if we're not on 64 bit Windows, it's gonna have to be the same 32 bit processor that the OS is.
            return WindowsVersionInfo.ProcessorType;
        }

        private static bool DoRehash(int processId) {
            SizeT written = 0;
            uint threadId = 0;

            // Find target
            var processHandle = Kernel32.OpenProcess(0x000F0000 | 0x00100000 | 0xFFFF /* PROCESS_ALL_ACCESS */, false, processId);
            if (processHandle.IsInvalid) {
                return false;
            }

            // Create some space in target memory space
            var processType = GetProcessProcessorType(processHandle);
            if(!_reHashDlls.ContainsKey(processType)) {
                Logger.Error("Rehash: Unable to get rehash DLL for {0}", processType);
                return false;
            }

            var pathInAscii = _reHashDlls[processType];
            var length = (uint) pathInAscii.Length;

            // before we actually allocate any memory in the target process,
            // let's make sure we get out module/fn pointers ok.
            var moduleHandle = Kernel32.GetModuleHandle("kernel32");
            if( moduleHandle.IsInvalid ) {
                Logger.Error("Rehash: Can't get Kernel32 Module Handle");
                // failed to get Module Handle to Kernel32? Whoa.
                return false;
            }

            var fnLoadLibrary = Kernel32.GetProcAddress(moduleHandle , "LoadLibraryA");
            if (fnLoadLibrary == IntPtr.Zero) {
                // Failed to get LoadLibraryA ptr.
                Logger.Error("Rehash: Can't get LoadLibraryA Handle");
                return false;
            }

            var remoteMemory = Kernel32.VirtualAllocEx(processHandle, IntPtr.Zero, length, AllocationType.Reserve | AllocationType.Commit, MemoryProtection.ExecuteReadWrite);
            if (remoteMemory == IntPtr.Zero) {
                // Target alloc mem error
                Logger.Error("Rehash: Can't allocate memory in process pid:{0}",processId);
                return false;
            }

            // Write library name into target memory space
            if (!Kernel32.WriteProcessMemory(processHandle, remoteMemory, pathInAscii , length, ref written) || written == 0) {
                // Target write mem error.
                // cleanup what we allocated?
                // var err = Marshal.GetLastWin32Error();
                Logger.Error("Rehash: Can't write memory in process pid:{0}", processId);
                Kernel32.VirtualFreeEx(processHandle, remoteMemory, length, AllocationType.Release);
                return false;
            }

            // flush the instruction cache, to make sure that the processor will see the changes.
            // Note: In the matrix, this can cause "deja vu"
            Kernel32.FlushInstructionCache(processHandle, IntPtr.Zero, 0);

            // tell the remote process to load our DLL (which does the actual rehash!)
            if (WindowsVersionInfo.IsVistaOrBeyond) {
                if( 0 != Ntdll.RtlCreateUserThread(processHandle, IntPtr.Zero, false, 0, IntPtr.Zero, IntPtr.Zero, fnLoadLibrary, remoteMemory, out threadId, IntPtr.Zero) ) {
                    Logger.Error("Rehash: Can't create remote thread (via RtlCreateUserThread) in pid:{0}", processId);
                    return false;
                }
            } else {
                if (
                    Kernel32.CreateRemoteThread(processHandle, IntPtr.Zero, 0, fnLoadLibrary, remoteMemory, CreateRemoteThreadFlags.None, out threadId).
                        IsInvalid) {
                    Logger.Error("Rehash: Can't create remote thread in pid:{0}", processId);
                    return false;
                }
            }
            //Logger.Warning("Rehash: Success with pid:{0}", processId);
            return true;
        }
    }
}
