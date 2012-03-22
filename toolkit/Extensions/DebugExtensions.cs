//-----------------------------------------------------------------------
// <copyright company="CoApp Project">
//     Original Copyright (c) 2009 Microsoft Corporation. All rights reserved.
//     Changes Copyright (c) 2010  Garrett Serack. All rights reserved.
// </copyright>
// <license>
//     The software is licensed under the Apache 2.0 License (the "License")
//     You may not use the software except in compliance with the License. 
// </license>
//-----------------------------------------------------------------------

// -----------------------------------------------------------------------
// Original Code: 
// (c) 2009 Microsoft Corporation -- All rights reserved
// This code is licensed under the MS-PL
// http://www.opensource.org/licenses/ms-pl.html
// Courtesy of the Open Source Techology Center: http://port25.technet.com
// -----------------------------------------------------------------------

namespace CoApp.Toolkit.Extensions {
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using Tasks;

    public static class DebugExtensions {
        public static void Debug(this string buf, params object[] args) {
#if DEBUG
            System.Diagnostics.Debug.WriteLine(buf.format(args));
#endif
        }

        public static void Debug(this byte[] buf) {
#if DEBUG
            const int lnWidth = 16;

            System.Diagnostics.Debug.WriteLine(" Buffer Length: {0} [0x{0:x4}]".format(buf.Length));

            for (var x = 0; x < buf.Length; x += lnWidth) {
                for (var y = 0; y < lnWidth; y++) {
                    if (x + y >= buf.Length) {
                        System.Diagnostics.Debug.Write("   ");
                    }
                    else {
                        System.Diagnostics.Debug.Write(" {0:x2}".format(buf[x + y]));
                    }
                }
                System.Diagnostics.Debug.Write("    ");

                for (var y = 0; y < lnWidth; y++) {
                    if (x + y >= buf.Length) {
                        System.Diagnostics.Debug.Write(" ");
                    }
                    else {
                        var c = buf[x + y] < 32 || buf[x + y] > 127 ? '.' : (char) buf[x + y];
                        System.Diagnostics.Debug.Write("{0}".format(c));
                    }
                }
                System.Diagnostics.Debug.WriteLine("");
            }
            System.Diagnostics.Debug.WriteLine("");
#endif
        }

        public static void StackDump() {
#if DEBUG
            var stackTrace = new StackTrace();
            var frames = stackTrace.GetFrames();
            string txt = "";
            foreach (var f in frames) {
                if (f != null) {
                    var method = f.GetMethod();
                    var fnName = method.Name;
                    var cls = method.DeclaringType;
                    if (cls == null) {
                        cls = stackTrace.GetType();
                    }

                    var clsName = cls.Name;

                    var filters = new[] {"*Thread*", "*Enumerable*", "*__*", "*trace*", "*updated*", "*Task*"}; //"*`*",
                    var print = true;
                    foreach (var flt in filters) {
                        if (fnName.IsWildcardMatch(flt) || clsName.IsWildcardMatch(flt)) {
                            print = false;
                        }
                    }
                    if (print) {
                        txt += string.Format("<=[{1}.{0}]", fnName, clsName);
                    }
                }
            }
            System.Diagnostics.Debug.WriteLine("{0}", txt);
#endif
        }
    }

    public class DebugMessage : MessageHandlers<DebugMessage> {
        public Action<string> WriteLine;
    }
}

#if LINQPAD
namespace CoApp.Toolkit.Debug {
    using System.Diagnostics;
    using System.IO;
    using Extensions;
    using LINQPad;

    public static class DumpExtension {
        public static T Dump<T>(this T o) {
            var localUrl = "LinqPadDump.html".GenerateTemporaryFilename();
            using (var writer = Util.CreateXhtmlWriter(true, 100)) {
                writer.Write(o);
                File.WriteAllText(localUrl, writer.ToString());
            }
            Process.Start(localUrl);
            return o;
        }
    }
}
#endif 