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
    using System.Collections;
    using System.Collections.Generic;
    using System.Runtime.InteropServices;
    using System.Text.RegularExpressions;
    using V1;
    using V2;

    /// <summary>
    ///   Contains all the tasks that are registered.
    /// </summary>
    /// <remarks>
    ///   Potentially breaking change in 1.6.2 and later where under V1 the list previously included the '.job' extension on the task name. This has been removed so that it is consistent with V2.
    /// </remarks>
    public sealed class TaskCollection : IEnumerable<Task>, IDisposable {
        private readonly TaskService svc;
        private Regex filter;
        private readonly TaskFolder fld;
        private ITaskScheduler v1TS;
        private readonly IRegisteredTaskCollection v2Coll;

        internal TaskCollection(TaskService svc, Regex filter = null) {
            this.svc = svc;
            Filter = filter;
            v1TS = svc.v1TaskScheduler;
        }

        internal TaskCollection(TaskFolder folder, IRegisteredTaskCollection iTaskColl, Regex filter = null) {
            svc = folder.TaskService;
            Filter = filter;
            fld = folder;
            v2Coll = iTaskColl;
        }

        /// <summary>
        ///   Releases all resources used by this class.
        /// </summary>
        public void Dispose() {
            v1TS = null;
            if (v2Coll != null) {
                Marshal.ReleaseComObject(v2Coll);
            }
        }

        /// <summary>
        ///   Gets the collection enumerator for the register task collection.
        /// </summary>
        /// <returns> An <see cref="System.Collections.IEnumerator" /> for this collection. </returns>
        public IEnumerator<Task> GetEnumerator() {
            if (v1TS != null) {
                return new V1TaskEnumerator(svc, filter);
            }
            return new V2TaskEnumerator(fld, v2Coll, filter);
        }

        IEnumerator IEnumerable.GetEnumerator() {
            return GetEnumerator();
        }

        internal class V1TaskEnumerator : IEnumerator<Task>, IDisposable {
            private readonly TaskService svc;
            private readonly IEnumWorkItems wienum;
            private ITaskScheduler m_ts;
            private Guid ITaskGuid = Marshal.GenerateGuidForType(typeof (ITask));
            private string curItem;
            private readonly Regex filter;

            /// <summary>
            ///   Internal constructor
            /// </summary>
            /// <param name="svc"> TaskService instance </param>
            /// <param name="filter"> The filter. </param>
            internal V1TaskEnumerator(TaskService svc, Regex filter = null) {
                this.svc = svc;
                this.filter = filter;
                m_ts = svc.v1TaskScheduler;
                wienum = m_ts.Enum();
                Reset();
            }

            /// <summary>
            ///   Retrieves the current task. See <see cref="System.Collections.IEnumerator.Current" /> for more information.
            /// </summary>
            public Task Current {
                get {
                    return new Task(svc, ICurrent);
                }
            }

            internal ITask ICurrent {
                get {
                    return m_ts.Activate(curItem, ref ITaskGuid);
                }
            }

            /// <summary>
            ///   Releases all resources used by this class.
            /// </summary>
            public void Dispose() {
                if (wienum != null) {
                    Marshal.ReleaseComObject(wienum);
                }
                m_ts = null;
            }

            object IEnumerator.Current {
                get {
                    return Current;
                }
            }

            /// <summary>
            ///   Moves to the next task. See MoveNext for more information.
            /// </summary>
            /// <returns> true if next task found, false if no more tasks. </returns>
            public bool MoveNext() {
                var names = IntPtr.Zero;
                var valid = false;
                do {
                    curItem = null;
                    uint uFetched = 0;
                    try {
                        wienum.Next(1, out names, out uFetched);
                        if (uFetched != 1) {
                            break;
                        }
                        using (var name = new CoTaskMemString(Marshal.ReadIntPtr(names))) {
                            curItem = name.ToString();
                        }
                        if (curItem.EndsWith(".job", StringComparison.InvariantCultureIgnoreCase)) {
                            curItem = curItem.Remove(curItem.Length - 4);
                        }
                    } catch {
                    } finally {
                        Marshal.FreeCoTaskMem(names);
                        names = IntPtr.Zero;
                    }

                    // If name doesn't match filter, look for next item
                    if (filter != null) {
                        if (!filter.IsMatch(curItem)) {
                            continue;
                        }
                    }

                    ITask itask = null;
                    try {
                        itask = ICurrent;
                        valid = true;
                    } catch {
                        valid = false;
                    } finally {
                        itask = null;
                    }
                } while (!valid);

                return (curItem != null);
            }

            /// <summary>
            ///   Reset task enumeration. See Reset for more information.
            /// </summary>
            public void Reset() {
                curItem = null;
                wienum.Reset();
            }

            internal string[] TaskNames {
                get {
                    var ret = new List<string>();
                    var names = IntPtr.Zero;
                    var valid = false;
                    do {
                        uint uFetched = 0;
                        try {
                            wienum.Next(50, out names, out uFetched);
                            if (uFetched == 0) {
                                break;
                            }

                            var cName = names;
                            for (uint i = 0; i < uFetched; cName = (IntPtr)((long)cName + Marshal.SizeOf(cName)), i++) {
                                using (var name = new CoTaskMemString(Marshal.ReadIntPtr(cName))) {
                                    var tempStr = name.ToString();
                                    if (tempStr.EndsWith(".job", StringComparison.InvariantCultureIgnoreCase)) {
                                        tempStr = tempStr.Remove(tempStr.Length - 4);
                                    }
                                    if (filter == null || filter.IsMatch(tempStr)) {
                                        ret.Add(tempStr);
                                    }
                                }
                            }
                        } catch {
                        } finally {
                            Marshal.FreeCoTaskMem(names);
                            names = IntPtr.Zero;
                        }
                    } while (!valid);

                    Reset();
                    return ret.ToArray();
                }
            }
        }

        internal class V2TaskEnumerator : IEnumerator<Task>, IDisposable {
            private IEnumerator iEnum;
            private readonly TaskFolder fld;
            private readonly Regex filter;

            internal V2TaskEnumerator(TaskFolder folder, IRegisteredTaskCollection iTaskColl, Regex filter = null) {
                fld = folder;
                iEnum = iTaskColl.GetEnumerator();
                this.filter = filter;
            }

            public Task Current {
                get {
                    return new Task(fld.TaskService, (IRegisteredTask)iEnum.Current);
                }
            }

            /// <summary>
            ///   Releases all resources used by this class.
            /// </summary>
            public void Dispose() {
                iEnum = null;
            }

            object IEnumerator.Current {
                get {
                    return Current;
                }
            }

            public bool MoveNext() {
                var hasNext = iEnum.MoveNext();
                if (!hasNext) {
                    return false;
                }

                while (hasNext && filter != null) {
                    if (filter.IsMatch(Current.Name)) {
                        break;
                    }
                    hasNext = iEnum.MoveNext();
                }

                return hasNext;
            }

            public void Reset() {
                iEnum.Reset();
            }
        }

        /// <summary>
        ///   Gets the number of registered tasks in the collection.
        /// </summary>
        public int Count {
            get {
                var i = 0;
                if (v2Coll != null) {
                    if (filter == null) {
                        return v2Coll.Count;
                    } else {
                        var v2te = new V2TaskEnumerator(fld, v2Coll, filter);
                        while (v2te.MoveNext()) {
                            i++;
                        }
                    }
                } else {
                    var v1te = new V1TaskEnumerator(svc, filter);
                    return v1te.TaskNames.Length;
                }
                return i;
            }
        }

        /// <summary>
        ///   Gets or sets the regular expression filter for task names.
        /// </summary>
        /// <value> The regular expression filter. </value>
        private Regex Filter {
            get {
                return filter;
            }
            set {
                var sfilter = value == null ? string.Empty : value.ToString().TrimStart('^').TrimEnd('$');
                if (sfilter == string.Empty || sfilter == "*") {
                    filter = null;
                } else {
                    if (value.ToString().TrimEnd('$').EndsWith("\\.job", StringComparison.InvariantCultureIgnoreCase)) {
                        filter = new Regex(value.ToString().Replace("\\.job", ""));
                    } else {
                        filter = value;
                    }
                }
            }
        }

        /// <summary>
        ///   Gets the specified registered task from the collection.
        /// </summary>
        /// <param name="index"> The index of the registered task to be retrieved. </param>
        /// <returns> A <see cref="Task" /> instance that contains the requested context. </returns>
        public Task this[int index] {
            get {
                var i = 0;
                if (v2Coll != null) {
                    if (filter == null) {
                        return new Task(svc, v2Coll[++index]);
                    } else {
                        var v2te = new V2TaskEnumerator(fld, v2Coll, filter);
                        while (v2te.MoveNext()) {
                            if (i++ == index) {
                                return v2te.Current;
                            }
                        }
                    }
                } else {
                    var v1te = new V1TaskEnumerator(svc, filter);
                    while (v1te.MoveNext()) {
                        if (i++ == index) {
                            return v1te.Current;
                        }
                    }
                }
                throw new ArgumentOutOfRangeException();
            }
        }

        /// <summary>
        ///   Gets the named registered task from the collection.
        /// </summary>
        /// <param name="name"> The name of the registered task to be retrieved. </param>
        /// <returns> A <see cref="Task" /> instance that contains the requested context. </returns>
        public Task this[string name] {
            get {
                if (v2Coll != null) {
                    return new Task(svc, v2Coll[name]);
                }

                var v1Task = svc.GetTask(name);
                if (v1Task != null) {
                    return v1Task;
                }

                throw new ArgumentOutOfRangeException();
            }
        }
    }

    /// <summary>
    ///   Collection of running tasks.
    /// </summary>
    public sealed class RunningTaskCollection : IEnumerable<RunningTask>, IDisposable {
        private readonly TaskService svc;
        private ITaskScheduler v1TS;
        private ITaskService v2Svc;
        private readonly IRunningTaskCollection v2Coll;

        internal RunningTaskCollection(TaskService svc) {
            this.svc = svc;
            v1TS = svc.v1TaskScheduler;
        }

        internal RunningTaskCollection(TaskService svc, IRunningTaskCollection iTaskColl) {
            this.svc = svc;
            v2Svc = svc.v2TaskService;
            v2Coll = iTaskColl;
        }

        /// <summary>
        ///   Releases all resources used by this class.
        /// </summary>
        public void Dispose() {
            v1TS = null;
            v2Svc = null;
            if (v2Coll != null) {
                Marshal.ReleaseComObject(v2Coll);
            }
        }

        /// <summary>
        ///   Gets an IEnumerator instance for this collection.
        /// </summary>
        /// <returns> An enumerator. </returns>
        public IEnumerator<RunningTask> GetEnumerator() {
            if (v2Coll != null) {
                return new RunningTaskEnumerator(svc, v2Coll);
            }
            return new V1RunningTaskEnumerator(svc);
        }

        IEnumerator IEnumerable.GetEnumerator() {
            return GetEnumerator();
        }

        internal class V1RunningTaskEnumerator : IEnumerator<RunningTask> {
            private readonly TaskService svc;
            private readonly TaskCollection.V1TaskEnumerator tEnum;

            internal V1RunningTaskEnumerator(TaskService svc) {
                this.svc = svc;
                tEnum = new TaskCollection.V1TaskEnumerator(svc);
            }

            public bool MoveNext() {
                if (tEnum.MoveNext()) {
                    if (tEnum.Current.State == TaskState.Running) {
                        return true;
                    }
                    return MoveNext();
                }
                return false;
            }

            public RunningTask Current {
                get {
                    return new RunningTask(svc, tEnum.ICurrent);
                }
            }

            /// <summary>
            ///   Releases all resources used by this class.
            /// </summary>
            public void Dispose() {
                tEnum.Dispose();
            }

            object IEnumerator.Current {
                get {
                    return Current;
                }
            }

            public void Reset() {
                tEnum.Reset();
            }
        }

        internal class RunningTaskEnumerator : IEnumerator<RunningTask>, IDisposable {
            private readonly TaskService svc;
            private ITaskService v2Svc;
            private IEnumerator iEnum;

            internal RunningTaskEnumerator(TaskService svc, IRunningTaskCollection iTaskColl) {
                this.svc = svc;
                v2Svc = svc.v2TaskService;
                iEnum = iTaskColl.GetEnumerator();
            }

            public RunningTask Current {
                get {
                    var irt = (IRunningTask)iEnum.Current;
                    IRegisteredTask task = null;
                    try {
                        task = TaskService.GetTask(v2Svc, irt.Path);
                    } catch {
                    }
                    if (task == null) {
                        return null;
                    }
                    return new RunningTask(svc, task, irt);
                }
            }

            /// <summary>
            ///   Releases all resources used by this class.
            /// </summary>
            public void Dispose() {
                v2Svc = null;
                iEnum = null;
            }

            object IEnumerator.Current {
                get {
                    return Current;
                }
            }

            public bool MoveNext() {
                return iEnum.MoveNext();
            }

            public void Reset() {
                iEnum.Reset();
            }
        }

        /// <summary>
        ///   Gets the number of registered tasks in the collection.
        /// </summary>
        public int Count {
            get {
                if (v2Coll != null) {
                    return v2Coll.Count;
                }
                var i = 0;
                var v1te = new V1RunningTaskEnumerator(svc);
                while (v1te.MoveNext()) {
                    i++;
                }
                return i;
            }
        }

        /// <summary>
        ///   Gets the specified running task from the collection.
        /// </summary>
        /// <param name="index"> The index of the running task to be retrieved. </param>
        /// <returns> A <see cref="RunningTask" /> instance. </returns>
        public RunningTask this[int index] {
            get {
                if (v2Coll != null) {
                    var irt = v2Coll[++index];
                    return new RunningTask(svc, TaskService.GetTask(svc.v2TaskService, irt.Path), irt);
                }

                var i = 0;
                var v1te = new V1RunningTaskEnumerator(svc);
                while (v1te.MoveNext()) {
                    if (i++ == index) {
                        return v1te.Current;
                    }
                }
                throw new ArgumentOutOfRangeException();
            }
        }
    }
}