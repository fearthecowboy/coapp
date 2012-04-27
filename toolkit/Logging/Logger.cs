namespace CoApp.Toolkit.Logging {
    using System;
    using System.Diagnostics;
    using System.Reflection;
    using System.Runtime.InteropServices;
    using System.Threading;
    using System.Threading.Tasks;
    using Exceptions;
    using Extensions;

#if COAPP_ENGINE_CORE
    using Packaging.Service;
    using Tasks;

#endif

    public static class Logger {
        private static readonly EventLog EventLog;
        private static readonly string Source;
        private static bool _ready;
        private static readonly short Pid;

#if COAPP_ENGINE_CORE
        private static bool _messages;
        public static bool Messages {
            get { return _messages || SessionCache<string>.Value["LogMessages"].IsTrue(); }
            set { _messages = value; }
        }
        private static bool _errors;
        public static bool Errors {
            get { return _errors || SessionCache<string>.Value["LogErrors"].IsTrue(); }
            set { _errors= value; }
        }
        private static bool _warnings;
        public static bool Warnings {
            get { return _warnings || SessionCache<string>.Value["LogWarnings"].IsTrue(); }
            set { _warnings = value; }
        }
#else
        public static bool Messages { get; set; }
        public static bool Errors { get; set; }
        public static bool Warnings { get; set; }
#endif

        [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
        internal static extern void OutputDebugString(string message);

        static Logger() {
            try {
                Pid = (short)Process.GetCurrentProcess().Id;
                Errors = true;
#if DEBUG
                // by default, we'll turn warnings on only in a debug version.
                Warnings = true;
#else
    // Warnings = false;
                Warnings = true; // let's just put this on until we get into RC.
#endif
                Messages = true;

                Source = (Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly()).Title();
                Source = Source.Trim('\0');

                if (String.IsNullOrEmpty(Source)) {
                    Source = "CoApp (misc)";
                }

                if (!EventLog.SourceExists(Source)) {
                    EventLog.CreateEventSource(Source, "CoApp");
                }
                EventLog = new EventLog("CoApp", ".", Source);

                Task.Factory.StartNew(() => {
                    while (!EventLog.SourceExists(Source)) {
                        Thread.Sleep(20);
                    }
                    _ready = true;
                });
            } catch {
                _ready = true;
            }
        }

        /// <devdoc>
        ///   <para>Writes an entry of the specified type with the
        ///     user-defined
        ///     <paramref name="eventId" />
        ///     and
        ///     <paramref name="category" />
        ///     to the event log, and appends binary data to 
        ///     the message. The Event Viewer does not interpret this data; it
        ///     displays raw data only in a combined hexadecimal and text format.</para>
        /// </devdoc>
        private static void WriteEntry(string message, EventLogEntryType type = EventLogEntryType.Information, short eventId = 0, short category = 0, byte[] rawData = null) {
            try {
                if (message.Length > 4096) {
                    message = message.Substring(0, 4096) + "==>[SNIPPED FOR BEREVITY]";
                }
                if (!_ready) {
                    Task.Factory.StartNew(() => {
                        for (var i = 0; i < 20 && !_ready; i++) {
                            Thread.Sleep(10);
                        }
                        // go ahead and try, but don't whine if this gets dropped.
                        try {
                            EventLog.WriteEntry(message, type, Pid, category, rawData);
                        } catch {
                        }
                    });
                } else {
                    try {
                        EventLog.WriteEntry(message, type, Pid, category, rawData);
                    } catch {
                    }
                }

                // we're gonna output this to dbgview too for now.
                if (eventId == 0 && category == 0) {
#if XCOAPP_ENGINE_CORE && DEBUG
                    Console.WriteLine(string.Format("«{0}/{1}»-{2}", type, Source, message.Replace("\r\n", "\r\n»")));
#endif
                    OutputDebugString(string.Format("«{0}/{1}»-{2}", type, Source, message.Replace("\r\n", "\r\n»")));
                } else {
#if XCOAPP_ENGINE_CORE && DEBUG
                    Console.WriteLine(string.Format("«{0}/{1}»({2}/{3})-{4}", type, Source, eventID, category, message.Replace("\r\n", "\r\n»")));
#endif
                    OutputDebugString(string.Format("«{0}/{1}»({2}/{3})-{4}", type, Source, eventId, category, message.Replace("\r\n", "\r\n»")));
                }
                if (!rawData.IsNullOrEmpty()) {
                    var rd = rawData.ToUtf8String().Replace("\r\n", "\r\n»");
                    if (!string.IsNullOrEmpty(rd) && rd.Length < 2048) {
                        OutputDebugString("   »RawData:" + rd);
                    } else {
                        OutputDebugString("   »RawData is [] bytes" + rawData.Length);
                    }
                }
            } catch {
            }
        }

        public static void Message(string message, params object[] args) {
            if (Messages) {
                WriteEntry(message.format(args));
            }
        }

        public static void MessageWithData(string message, string data, params object[] args) {
            if (Messages) {
                WriteEntry(message.format(args), rawData: data.ToByteArray());
            }
        }

        public static void Warning(string message, params object[] args) {
            if (Warnings) {
                WriteEntry(message.format(args), EventLogEntryType.Warning);
            }
        }

        public static void WarningWithData(string message, string data, params object[] args) {
            if (Warnings) {
                WriteEntry(message.format(args), EventLogEntryType.Warning, rawData: data.ToByteArray());
            }
        }

        public static void Warning(CoAppException exception) {
            if (Warnings) {
                if (!exception.Logged) {
                    exception.Logged = true;
                    if (exception.InnerException != null) {
                        WriteEntry("{0}/{1} - {2}".format(exception.GetType(), exception.InnerException.GetType(), exception.Message), EventLogEntryType.Warning, 0, 0, exception.stacktrace.ToByteArray());
                    } else {
                        WriteEntry("{0} - {1}".format(exception.GetType(), exception.Message), EventLogEntryType.Warning, 0, 0, exception.stacktrace.ToByteArray());
                    }
                }
            }
        }

        public static void Warning(Exception exception) {
            if (Warnings) {
                if (exception.InnerException != null) {
                    WriteEntry("{0}/{1} - {2}".format(exception.GetType(), exception.InnerException.GetType(), exception.Message), EventLogEntryType.Warning, 0, 0, exception.StackTrace.ToByteArray());
                } else {
                    WriteEntry("{0} - {1}".format(exception.GetType(), exception.Message), EventLogEntryType.Warning, 0, 0, exception.StackTrace.ToByteArray());
                }
            }
        }

        public static void Error(string message, params object[] args) {
            if (Errors) {
                WriteEntry(message.format(args), EventLogEntryType.Error);
            }
        }

        public static void ErrorWithData(string message, string data, params object[] args) {
            if (Errors) {
                WriteEntry(message.format(args), EventLogEntryType.Error, rawData: data.ToByteArray());
            }
        }

        public static void Error(CoAppException exception) {
            if (Errors) {
                if (!exception.Logged) {
                    exception.Logged = true;
                    if (exception.InnerException != null) {
                        WriteEntry("{0}/{1} - {2}".format(exception.GetType(), exception.InnerException.GetType(), exception.Message), EventLogEntryType.Error, 0, 0, exception.stacktrace.ToByteArray());
                    } else {
                        WriteEntry("{0} - {1}".format(exception.GetType(), exception.Message), EventLogEntryType.Error, 0, 0, exception.stacktrace.ToByteArray());
                    }
                }
            }
        }

        public static void Error(Exception exception) {
            if (Errors) {
                if (exception.InnerException != null) {
                    WriteEntry("{0}/{1} - {2}".format(exception.GetType(), exception.InnerException.GetType(), exception.Message), EventLogEntryType.Error, 0, 0, exception.StackTrace.ToByteArray());
                } else {
                    WriteEntry("{0} - {1}".format(exception.GetType(), exception.Message), EventLogEntryType.Error, 0, 0, exception.StackTrace.ToByteArray());
                }
            }
        }
    }
}