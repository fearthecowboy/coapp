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
    using Properties;
    using V1;
    using V2;

    /// <summary>
    ///   Provides the methods that are used to add to, remove from, and get the triggers of a task.
    /// </summary>
    public sealed class TriggerCollection : IEnumerable<Trigger>, IDisposable {
        private ITask v1Task;
        private ITaskDefinition v2Def;
        private readonly ITriggerCollection v2Coll;

        internal TriggerCollection(ITask iTask) {
            v1Task = iTask;
        }

        internal TriggerCollection(ITaskDefinition iTaskDef) {
            v2Def = iTaskDef;
            v2Coll = v2Def.Triggers;
        }

        /// <summary>
        ///   Releases all resources used by this class.
        /// </summary>
        public void Dispose() {
            if (v2Coll != null) {
                Marshal.ReleaseComObject(v2Coll);
            }
            v2Def = null;
            v1Task = null;
        }

        /// <summary>
        ///   Gets the collection enumerator for this collection.
        /// </summary>
        /// <returns> The <see cref="IEnumerator{T}" /> for this collection. </returns>
        public IEnumerator<Trigger> GetEnumerator() {
            if (v1Task != null) {
                return new V1TriggerEnumerator(v1Task);
            }
            return new V2TriggerEnumerator(v2Coll);
        }

        IEnumerator IEnumerable.GetEnumerator() {
            return GetEnumerator();
        }

        internal sealed class V1TriggerEnumerator : IEnumerator<Trigger> {
            private ITask iTask;
            private short curItem = -1;

            internal V1TriggerEnumerator(ITask task) {
                iTask = task;
            }

            public Trigger Current {
                get {
                    return Trigger.CreateTrigger(iTask.GetTrigger((ushort)curItem));
                }
            }

            /// <summary>
            ///   Releases all resources used by this class.
            /// </summary>
            public void Dispose() {
                iTask = null;
            }

            object IEnumerator.Current {
                get {
                    return Current;
                }
            }

            public bool MoveNext() {
                if (++curItem >= iTask.GetTriggerCount()) {
                    return false;
                }
                return true;
            }

            public void Reset() {
                curItem = -1;
            }
        }

        internal sealed class V2TriggerEnumerator : IEnumerator<Trigger> {
            private IEnumerator iEnum;

            internal V2TriggerEnumerator(ITriggerCollection iColl) {
                iEnum = iColl.GetEnumerator();
            }

            #region IEnumerator<Trigger> Members

            public Trigger Current {
                get {
                    return Trigger.CreateTrigger((ITrigger)iEnum.Current);
                }
            }

            #endregion

            #region IDisposable Members

            /// <summary>
            ///   Releases all resources used by this class.
            /// </summary>
            public void Dispose() {
                iEnum = null;
            }

            #endregion

            #region IEnumerator Members

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

            #endregion
        }

        /// <summary>
        ///   Gets the number of triggers in the collection.
        /// </summary>
        public int Count {
            get {
                if (v2Coll != null) {
                    return v2Coll.Count;
                }
                return v1Task.GetTriggerCount();
            }
        }

        /// <summary>
        ///   Add an unbound <see cref="Trigger" /> to the task.
        /// </summary>
        /// <param name="unboundTrigger"> <see cref="Trigger" /> derivative to add to the task. </param>
        /// <returns> Bound trigger. </returns>
        public Trigger Add(Trigger unboundTrigger) {
            if (v2Def != null) {
                unboundTrigger.Bind(v2Def);
            } else {
                unboundTrigger.Bind(v1Task);
            }
            return unboundTrigger;
        }

        /// <summary>
        ///   Add a new trigger to the collections of triggers for the task.
        /// </summary>
        /// <param name="taskTriggerType"> The type of trigger to create. </param>
        /// <returns> A <see cref="Trigger" /> instance of the specified type. </returns>
        public Trigger AddNew(TaskTriggerType taskTriggerType) {
            if (v1Task != null) {
                ushort idx;
                return Trigger.CreateTrigger(v1Task.CreateTrigger(out idx), Trigger.ConvertToV1TriggerType(taskTriggerType));
            }

            return Trigger.CreateTrigger(v2Coll.Create(taskTriggerType));
        }

        internal void Bind() {
            foreach (var t in this) {
                t.SetV1TriggerData();
            }
        }

        /// <summary>
        ///   Clears all triggers from the task.
        /// </summary>
        public void Clear() {
            if (v2Coll != null) {
                v2Coll.Clear();
            } else {
                for (var i = Count - 1; i >= 0; i--) {
                    RemoveAt(i);
                }
            }
        }

        /// <summary>
        ///   Gets a specified trigger from the collection.
        /// </summary>
        /// <param name="index"> The index of the trigger to be retrieved. </param>
        /// <returns> Specialized <see cref="Trigger" /> instance. </returns>
        public Trigger this[int index] {
            get {
                if (v2Coll != null) {
                    return Trigger.CreateTrigger(v2Coll[++index]);
                }
                return Trigger.CreateTrigger(v1Task.GetTrigger((ushort)index));
            }
            set {
                if (Count <= index) {
                    throw new ArgumentOutOfRangeException("index", index, "Index is not a valid index in the TriggerCollection");
                }
                RemoveAt(index);
                Insert(index, value);
            }
        }

        /// <summary>
        ///   Inserts an trigger at the specified index.
        /// </summary>
        /// <param name="index"> The zero-based index at which trigger should be inserted. </param>
        /// <param name="trigger"> The trigger to insert into the list. </param>
        public void Insert(int index, Trigger trigger) {
            var pushItems = new Trigger[Count - index];
            for (var i = index; i < Count; i++) {
                pushItems[i - index] = (Trigger)this[i].Clone();
            }
            for (var j = Count - 1; j >= index; j--) {
                RemoveAt(j);
            }
            Add(trigger);
            for (var k = 0; k < pushItems.Length; k++) {
                Add(pushItems[k]);
            }
        }

        /// <summary>
        ///   Removes the trigger at a specified index.
        /// </summary>
        /// <param name="index"> Index of trigger to remove. </param>
        /// <exception cref="ArgumentOutOfRangeException">Index out of range.</exception>
        public void RemoveAt(int index) {
            if (index >= Count) {
                throw new ArgumentOutOfRangeException("index", index, "Failed to remove Trigger. Index out of range.");
            }
            if (v2Coll != null) {
                v2Coll.Remove(++index);
            } else {
                v1Task.DeleteTrigger((ushort)index); //Remove the trigger from the Task Scheduler
            }
        }

        /// <summary>
        ///   Returns a <see cref="System.String" /> that represents the triggers in this collection.
        /// </summary>
        /// <returns> A <see cref="System.String" /> that represents the triggers in this collection. </returns>
        public override string ToString() {
            if (Count == 1) {
                return this[0].ToString();
            }
            if (Count > 1) {
                return Resources.MultipleTriggers;
            }
            return string.Empty;
        }
    }
}