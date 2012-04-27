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
    using System.Runtime.InteropServices;
    using System.Security;

    /// <summary>
    ///   Defines the methods that are called by the Task Scheduler service to manage a COM handler.
    /// </summary>
    /// <remarks>
    ///   This interface must be implemented for a task to perform a COM handler action. When the Task Scheduler performs a COM handler action, it creates and activates the handler and calls the methods of this interface as needed. For information on specifying a COM handler action, see the <see
    ///    cref="ComHandlerAction" /> class.
    /// </remarks>
    [ComImport, Guid("839D7762-5121-4009-9234-4F0D19394F04"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown),
     SuppressUnmanagedCodeSecurity]
    public interface ITaskHandler {
        /// <summary>
        ///   Called to start the COM handler. This method must be implemented by the handler.
        /// </summary>
        /// <param name="pHandlerServices"> An <c>IUnkown</c> interface that is used to communicate back with the Task Scheduler. </param>
        /// <param name="Data"> The arguments that are required by the handler. These arguments are defined in the <see
        ///    cref="ComHandlerAction.Data" /> property of the COM handler action. </param>
        void Start([In, MarshalAs(UnmanagedType.IUnknown)] object pHandlerServices, [In, MarshalAs(UnmanagedType.BStr)] string Data);

        /// <summary>
        ///   Called to stop the COM handler. This method must be implemented by the handler.
        /// </summary>
        /// <param name="pRetCode"> The return code that the Task Schedule will raise as an event when the COM handler action is completed. </param>
        void Stop([MarshalAs(UnmanagedType.Error)] out int pRetCode);

        /// <summary>
        ///   Called to pause the COM handler. This method is optional and should only be implemented to give the Task Scheduler the ability to pause and restart the handler.
        /// </summary>
        void Pause();

        /// <summary>
        ///   Called to resume the COM handler. This method is optional and should only be implemented to give the Task Scheduler the ability to resume the handler.
        /// </summary>
        void Resume();
    }

    /// <summary>
    ///   Provides the methods that are used by COM handlers to notify the Task Scheduler about the status of the handler.
    /// </summary>
    [ComImport, Guid("EAEC7A8F-27A0-4DDC-8675-14726A01A38A"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown),
     SuppressUnmanagedCodeSecurity]
    public interface ITaskHandlerStatus {
        /// <summary>
        ///   Tells the Task Scheduler about the percentage of completion of the COM handler.
        /// </summary>
        /// <param name="percentComplete"> A value that indicates the percentage of completion for the COM handler. </param>
        /// <param name="statusMessage"> The message that is displayed in the Task Scheduler UI. </param>
        void UpdateStatus([In] short percentComplete, [In, MarshalAs(UnmanagedType.BStr)] string statusMessage);

        /// <summary>
        ///   Tells the Task Scheduler that the COM handler is completed.
        /// </summary>
        /// <param name="taskErrCode"> The error code that the Task Scheduler will raise as an event. </param>
        void TaskCompleted([In, MarshalAs(UnmanagedType.Error)] int taskErrCode);
    }
}