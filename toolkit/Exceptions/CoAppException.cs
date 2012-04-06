using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CoApp.Toolkit.Exceptions {
    using System.Diagnostics;
    using System.Runtime.Serialization;

    public class CoAppException : Exception {
        internal bool Logged;
        internal bool IsError;
        internal string stacktrace;

        public bool IsCanceled { get; set; }

        public void Cancel() {
            IsCanceled = true;
        }

        private void Log() {
            stacktrace = new StackTrace(2, true).ToString();

            if (IsError) {
                Logging.Logger.Error(this);
            } else {
                Logging.Logger.Warning(this);
            }
        }

        public CoAppException(bool skipLogging = false) {
            if (!skipLogging) {
                Log();
            }
            Logged = true;
        }

        public CoAppException(string message, bool skipLogging = false)
            : base(message) {
            if (!skipLogging) {
                Log();
            }
            Logged = true;
        }

        public CoAppException(String message, Exception innerException, bool skipLogging = false)
            : base(message, innerException) {
            if (!skipLogging) {
                Log();
            }
            Logged = true;
        }

        protected CoAppException(SerializationInfo info, StreamingContext context, bool skipLogging = false)
            : base(info, context) {
            if (!skipLogging) {
                Log();
            }
            Logged = true;
        }
    }
}
