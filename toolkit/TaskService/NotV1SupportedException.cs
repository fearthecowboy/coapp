//-----------------------------------------------------------------------
// <copyright company="CoApp Project">
//     Changes Copyright (c) 2011 Garrett Serack . All rights reserved.
//     TaskScheduler Original Code from http://taskscheduler.codeplex.com/
// </copyright>
// <license>
//     The software is licensed under the Apache 2.0 License (the "License")
//     You may not use the software except in compliance with the License. 
// </license>
//-----------------------------------------------------------------------

// -----------------------------------------------------------------------
// Original Code: 
// * Copyright (c) 2003-2011 David Hall
// Permission is hereby granted, free of charge, to any person obtaining a
// copy of this software and associated documentation files (the "Software"), 
// to deal in the Software without restriction, including without limitation
// the rights to use, copy, modify, merge, publish, distribute, sublicense, 
// and/or sell copies of the Software, and to permit persons to whom the 
// Software is furnished to do so, subject to the following conditions:

// The above copyright notice and this permission notice shall be included 
// in all copies or substantial portions of the Software.

// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS
// OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL 
// THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR 
// OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, 
// ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR 
// OTHER DEALINGS IN THE SOFTWARE.

namespace CoApp.Toolkit.TaskService {
    using System;
    using System.Diagnostics;

    /// <summary>
    ///   Abstract class for throwing a method specific exception.
    /// </summary>
    [DebuggerStepThrough]
    public abstract class TSNotSupportedException : Exception {
        private readonly string myMessage;

        internal TSNotSupportedException() {
            var stackTrace = new StackTrace();
            var stackFrame = stackTrace.GetFrame(2);
            var methodBase = stackFrame.GetMethod();
            myMessage = string.Format("{0}.{1} is not supported on {2}", methodBase.DeclaringType.Name, methodBase.Name, LibName);
        }

        internal TSNotSupportedException(string message) {
            myMessage = message;
        }

        /// <summary>
        ///   Gets a message that describes the current exception.
        /// </summary>
        public override string Message {
            get {
                return myMessage;
            }
        }

        internal abstract string LibName { get; }
    }

    /// <summary>
    ///   Thrown when the calling method is not supported by Task Scheduler 1.0.
    /// </summary>
    [DebuggerStepThrough]
    public class NotV1SupportedException : TSNotSupportedException {
        internal NotV1SupportedException() {
        }

        internal NotV1SupportedException(string message) : base(message) {
        }

        internal override string LibName {
            get {
                return "Task Scheduler 1.0";
            }
        }
    }

    /// <summary>
    ///   Thrown when the calling method is not supported by Task Scheduler 1.0.
    /// </summary>
    [DebuggerStepThrough]
    public class NotV2SupportedException : TSNotSupportedException {
        internal NotV2SupportedException() {
        }

        internal NotV2SupportedException(string message) : base(message) {
        }

        internal override string LibName {
            get {
                return "Task Scheduler 2.0 (1.2)";
            }
        }
    }
}