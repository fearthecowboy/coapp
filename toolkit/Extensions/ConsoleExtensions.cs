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
    using System.Linq;
    using System.Reflection;
    using Microsoft.Win32.SafeHandles;
    using Win32;

    public class ConsoleColors : IDisposable {
        private readonly ConsoleColor fg;
        private readonly ConsoleColor bg;

        public ConsoleColors(ConsoleColor fg, ConsoleColor bg) {
            this.fg = Console.ForegroundColor;
            this.bg = Console.BackgroundColor;
            Console.ForegroundColor = fg;
            Console.BackgroundColor = bg;
        }

        public void Dispose() {
            Console.ForegroundColor = fg;
            Console.BackgroundColor = bg;
        }
    }

    public static class ConsoleExtensions {
        public static Lazy<SafeFileHandle> StandardOutputHandle = new Lazy<SafeFileHandle>(() => {
            return Kernel32.GetStdHandle(StandardHandle.OUTPUT);
        });

        public static Lazy<SafeFileHandle> StandardInputHandle = new Lazy<SafeFileHandle>(() => {
            return Kernel32.GetStdHandle(StandardHandle.INPUT);
        });

        public static Lazy<SafeFileHandle> StandardErrorHandle = new Lazy<SafeFileHandle>(() => {
            return Kernel32.GetStdHandle(StandardHandle.ERROR);
        });

        public static bool OutputRedirected {
            get {
                return FileType.Char != Kernel32.GetFileType(StandardOutputHandle.Value);
            }
        }

        public static bool InputRedirected {
            get {
                return FileType.Char != Kernel32.GetFileType(StandardInputHandle.Value);
            }
        }

        public static bool ErrorRedirected {
            get {
                return FileType.Char != Kernel32.GetFileType(StandardErrorHandle.Value);
            }
        }

        public static bool IsConsole {
            get {
                try {
                    return Console.BufferWidth != 0;
                } catch {
                }
                return false;
            }
        }

        public static void PrintProgressBar(this string message, long progress) {
            if (!OutputRedirected) {
                if (progress > -1) {
                    if (progress <= 100) {
                        if (Console.BufferWidth < (message.Length + 5)) {
                            message = message.Substring(0, Console.BufferWidth - 12);
                        }

                        var sz = Console.BufferWidth - (message.Length + 5);
                        var done = (int)((progress*sz)/100);

                        Console.Write("\r{0} [{1}] ", message, "".PadRight(done, '=').PadRight(sz, ' '));
                    } else {
                        Console.Write("\r{0} [{1} ]", message, progress);
                    }
                } else {
                    Console.Write("\r".PadRight(Console.BufferWidth - 1, ' '));
                }
            }
        }

        private static string SafeGet(this Object obj, PropertyInfo propertyInfo, string @default = "") {
            try {
                var v = propertyInfo.GetValue(obj, null);
                return v != null ? v.ToString() : @default;
            } catch {
                return @default;
            }
        }

        private static string SafeGet(this Object obj, FieldInfo fieldInfo, string @default = "") {
            try {
                var v = fieldInfo.GetValue(obj);
                return v != null ? v.ToString() : @default;
            } catch {
                return @default;
            }
        }

        private static string Justify(this string str, int width, int justification) {
            var result = str.PadLeft(justification == 2 ? 0 : justification == 3 ? (width - str.Length)/2 : width);
            if (result.Length > width) {
                int sz = (result.Length - (width + 3))/2;
                result = result.Substring(0, sz) + " ... " + result.Substring(result.Length - sz);
            }
            return result;
        }

        private static string TrimTo(this string s, int sz) {
            return s.Length < sz ? s : s.Substring(s.Length - sz);
        }

        private static string[] JustifyAll(this IList<string> elements, IList<int> widths, IList<int> justifications) {
            var count = elements.Count;
            var result = new string[count];
            for (var i = 0; i < count; i++) {
                result[i] = elements[i].PadRight(justifications[i] == 2 ? 0 : justifications[i] == 3 ? ((widths[i] - elements[i].Length)/2) + elements[i].Length : widths[i]);

                if (result[i].Length > widths[i]) {
                    if (widths[i] < 15) {
                        result[i] = result[i].Substring(0, (widths[i] - 3)) + "...";
                    } else {
                        int keep = widths[i]/2;
                        result[i] = result[i].Substring(0, keep - 1) + "..." + result[i].Substring(result[i].Length - (keep - 2 + (widths[i] & 1)));
                    }
                }
            }
            return result;
        }

        public static IEnumerable<string> ToTable(this IEnumerable<object> data, int maxWidth = 500) {
            if (!data.Any()) {
                return "Collection does not contain any data.".SingleItemAsEnumerable();
            }
            var fields = data.First().GetType().GetProperties();
            var columnTitles = (from field in fields select field.Name.Replace("_", " ").Trim()).ToArray();
            var rows = data.Select(row => (from field in fields select row.SafeGet(field)).ToArray()).ToArray();
            var columnJustifications = (from field in fields select (field.Name.StartsWith("_") ? 1 : 0) + (field.Name.EndsWith("_") ? 2 : 0)).ToArray();
            var columnWidths = fields.ByIndex().Select(index => Math.Max((from row in rows select row[index].Length).Max(), columnTitles[index].Length)).ToArray();
            var tSize = columnWidths.Sum() + columnWidths.Length;
            maxWidth -= 5;
            /*
            var minColSize = 10;
            
            if( tSize > maxWidth ) {
                var overage = tSize - maxWidth-6; 
                var bigColumns = columnWidths.Where(width => width > minColSize).Sum();
                var ratio = 100*overage/bigColumns;
                for (int i = 0; i < columnWidths.Length;i++  ) {
                    if( columnWidths[i] > minColSize ) {
                        columnWidths[i]= ((columnWidths[i] * ratio) /  100)-1;
                    }
                }
                tSize = columnWidths.Sum() + columnWidths.Length;
            }
            */
            // var fmt = "|" + string.Join("", columnWidths.ByIndex().Select(n => "{{{0},{1}}}|".format(n, columnWidths[n])));
            var fmt = string.Join("", columnWidths.ByIndex().Select(n => "{{{0},{1}}}|".format(n, columnWidths[n]))).Trim('|');
            var breaker = "-".PadLeft(tSize + 1, '-');

            var result = new List<string>();
            // result.Add(breaker);
            result.Add(fmt.format(JustifyAll(columnTitles, columnWidths, columnJustifications)));
            result.Add(breaker);
            result.AddRange(rows.Select(row => fmt.format(JustifyAll(row, columnWidths, columnJustifications))));
            result.Add(breaker);

            return result;
        }

        public static void ConsoleOut(this IEnumerable<string> strings) {
            foreach (var s in strings) {
                Console.WriteLine(s);
            }
        }
    }
}