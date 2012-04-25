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

namespace CoApp.Toolkit.Console {
    using Microsoft.Win32.SafeHandles;
    using Win32;

    /// <summary>
    ///   Declarations of some Console API functions and structures.
    /// </summary>
    public static class ConsoleApi {
        public static void SendStringToStdIn(string text) {
            var stdIn = Kernel32.GetStdHandle(StandardHandle.INPUT);
            foreach (var c in text) {
                SendCharacterToStream(stdIn, c);
            }
        }

        private static void SendCharacterToStream(SafeFileHandle hIn, char c) {
            var count = 0;
            var keyInputRecord = new KeyInputRecord {
                bKeyDown = true,
                wRepeatCount = 1,
                wVirtualKeyCode = 0,
                wVirtualScanCode = 0,
                UnicodeChar = c,
                dwControlKeyState = 0
            };
            Kernel32.WriteConsoleInput(hIn, keyInputRecord, 1, out count);
            keyInputRecord.bKeyDown = false;
            Kernel32.WriteConsoleInput(hIn, keyInputRecord, 1, out count);
        }
    }
}