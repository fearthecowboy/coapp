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
    using Properties;
    using V1;
    using V2;

    /// <summary>
    ///   Collection that contains the actions that are performed by the task.
    /// </summary>
    /// <remarks>
    ///   A Task Scheduler 1.0 task can only contain a single <see cref="ExecAction" /> .
    /// </remarks>
    public sealed class ActionCollection : IEnumerable<Action>, IDisposable {
        private ITask v1Task;
        private ITaskDefinition v2Def;
        private IActionCollection v2Coll;

        internal ActionCollection(ITask task) {
            v1Task = task;
        }

        internal ActionCollection(ITaskDefinition iTaskDef) {
            v2Def = iTaskDef;
            v2Coll = iTaskDef.Actions;
        }

        /// <summary>
        ///   Releases all resources used by this class.
        /// </summary>
        public void Dispose() {
            v1Task = null;
            v2Def = null;
            v2Coll = null;
        }

        /// <summary>
        ///   Adds an action to the task.
        /// </summary>
        /// <param name="action"> A derived <see cref="Action" /> class. </param>
        /// <returns> The bound <see cref="Action" /> that was added to the collection. </returns>
        public Action Add(Action action) {
            if (v2Def != null) {
                action.Bind(v2Def);
            } else {
                action.Bind(v1Task);
            }
            return action;
        }

        /// <summary>
        ///   Adds a new <see cref="Action" /> instance to the task.
        /// </summary>
        /// <param name="actionType"> Type of task to be created </param>
        /// <returns> Specialized <see cref="Action" /> instance. </returns>
        public Action AddNew(TaskActionType actionType) {
            if (v1Task != null) {
                return new ExecAction(v1Task);
            }

            return Action.CreateAction(v2Coll.Create(actionType));
        }

        /// <summary>
        ///   Clears all actions from the task.
        /// </summary>
        public void Clear() {
            if (v2Coll != null) {
                v2Coll.Clear();
            } else {
                Add(new ExecAction());
            }
        }

        /// <summary>
        ///   Inserts an action at the specified index.
        /// </summary>
        /// <param name="index"> The zero-based index at which action should be inserted. </param>
        /// <param name="action"> The action to insert into the list. </param>
        public void Insert(int index, Action action) {
            if (v2Coll == null && Count > 0) {
                throw new NotV1SupportedException("Only a single action is allowed.");
            }

            var pushItems = new Action[Count - index];
            for (var i = index; i < Count; i++) {
                pushItems[i - index] = (Action)this[i].Clone();
            }
            for (var j = Count - 1; j >= index; j--) {
                RemoveAt(j);
            }
            Add(action);
            for (var k = 0; k < pushItems.Length; k++) {
                Add(pushItems[k]);
            }
        }

        /// <summary>
        ///   Removes the action at a specified index.
        /// </summary>
        /// <param name="index"> Index of action to remove. </param>
        /// <exception cref="ArgumentOutOfRangeException">Index out of range.</exception>
        public void RemoveAt(int index) {
            if (index >= Count) {
                throw new ArgumentOutOfRangeException("index", index, "Failed to remove action. Index out of range.");
            }
            if (v2Coll != null) {
                v2Coll.Remove(++index);
            } else if (index == 0) {
                Add(new ExecAction());
            } else {
                throw new NotV1SupportedException("There can be only a single action and it cannot be removed.");
            }
        }

        /// <summary>
        ///   Returns a <see cref="System.String" /> that represents the actions in this collection.
        /// </summary>
        /// <returns> A <see cref="System.String" /> that represents the actions in this collection. </returns>
        public override string ToString() {
            if (Count == 1) {
                return this[0].ToString();
            }
            if (Count > 1) {
                return Resources.MultipleActions;
            }
            return string.Empty;
        }

        /// <summary>
        ///   Gets or sets a an action at the specified index.
        /// </summary>
        /// <value> The zero-based index of the action to get or set. </value>
        public Action this[int index] {
            get {
                if (v2Coll != null) {
                    return Action.CreateAction(v2Coll[++index]);
                }
                if (index == 0) {
                    return new ExecAction(v1Task.GetApplicationName(), v1Task.GetParameters(), v1Task.GetWorkingDirectory());
                }
                throw new ArgumentOutOfRangeException();
            }
            set {
                if (Count <= index) {
                    throw new ArgumentOutOfRangeException("index", index, "Index is not a valid index in the ActionCollection");
                }
                RemoveAt(index);
                Insert(index, value);
            }
        }

        /// <summary>
        ///   Gets or sets the identifier of the principal for the task.
        /// </summary>
        public string Context {
            get {
                if (v2Coll != null) {
                    return v2Coll.Context;
                }
                return string.Empty;
            }
            set {
                if (v2Coll != null) {
                    v2Coll.Context = value;
                } else {
                    throw new NotV1SupportedException();
                }
            }
        }

        /// <summary>
        ///   Gets the number of actions in the collection.
        /// </summary>
        public int Count {
            get {
                if (v2Coll != null) {
                    return v2Coll.Count;
                }
                return ((string)v1Task.GetApplicationName()).Length == 0 ? 0 : 1;
            }
        }

        /// <summary>
        ///   Gets or sets an XML-formatted version of the collection.
        /// </summary>
        public string XmlText {
            get {
                if (v2Coll != null) {
                    return v2Coll.XmlText;
                }
                throw new NotV1SupportedException();
            }
            set {
                if (v2Coll != null) {
                    v2Coll.XmlText = value;
                } else {
                    throw new NotV1SupportedException();
                }
            }
        }

        /// <summary>
        ///   Retrieves an enumeration of each of the actions.
        /// </summary>
        /// <returns> Returns an object that implements the <see cref="IEnumerator" /> interface and that can iterate through the <see
        ///    cref="Action" /> objects within the <see cref="ActionCollection" /> . </returns>
        public IEnumerator<Action> GetEnumerator() {
            if (v2Coll != null) {
                return new Enumerator(this);
            }
            return new Enumerator(v1Task);
        }

        IEnumerator IEnumerable.GetEnumerator() {
            return GetEnumerator();
        }

        internal class Enumerator : IEnumerator<Action> {
            private ITask v1Task;
            private int v1Pos = -1;
            private IEnumerator v2Enum;
            private ActionCollection parent;

            internal Enumerator(ITask task) {
                v1Task = task;
            }

            internal Enumerator(ActionCollection iColl) {
                parent = iColl;
                if (iColl.v2Coll != null) {
                    v2Enum = iColl.v2Coll.GetEnumerator();
                }
            }

            public Action Current {
                get {
                    if (v2Enum != null) {
                        var iAction = v2Enum.Current as IAction;
                        if (iAction != null) {
                            return Action.CreateAction(iAction);
                        }
                    }
                    if (v1Pos == 0) {
                        return new ExecAction(v1Task.GetApplicationName(), v1Task.GetParameters(), v1Task.GetWorkingDirectory());
                    }
                    throw new InvalidOperationException();
                }
            }

            /// <summary>
            ///   Releases all resources used by this class.
            /// </summary>
            public void Dispose() {
                v1Task = null;
                v2Enum = null;
            }

            object IEnumerator.Current {
                get {
                    return Current;
                }
            }

            public bool MoveNext() {
                if (v2Enum != null) {
                    return v2Enum.MoveNext();
                }
                return ++v1Pos == 0;
            }

            public void Reset() {
                if (v2Enum != null) {
                    v2Enum.Reset();
                }
                v1Pos = -1;
            }
        }
    }
}