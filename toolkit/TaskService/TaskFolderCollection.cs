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
    using V2;

    /// <summary>
    ///   Provides information and control for a collection of folders that contain tasks.
    /// </summary>
    public sealed class TaskFolderCollection : IEnumerable<TaskFolder> {
        private TaskFolder parent;
        private ITaskFolderCollection v2FolderList;
        private TaskFolder[] v1FolderList;

        internal TaskFolderCollection() {
            v1FolderList = new TaskFolder[0];
        }

        internal TaskFolderCollection(TaskFolder v1Folder) {
            parent = v1Folder;
            v1FolderList = new[] {v1Folder};
        }

        internal TaskFolderCollection(TaskFolder folder, ITaskFolderCollection iCollection) {
            parent = folder;
            v2FolderList = iCollection;
        }

        /// <summary>
        ///   Releases all resources used by this class.
        /// </summary>
        public void Dispose() {
            if (v1FolderList != null && v1FolderList.Length > 0) {
                v1FolderList[0].Dispose();
                v1FolderList[0] = null;
            }
            if (v2FolderList != null) {
                Marshal.ReleaseComObject(v2FolderList);
            }
        }

        /*
		/// <summary>
		/// Returns the index of the TaskFolder within the collection.
		/// </summary>
		/// <param name="item">TaskFolder to find.</param>
		/// <returns>Index of the TaskFolder; -1 if not found.</returns>
		public int IndexOf(TaskFolder item)
		{
			return IndexOf(item.Path);
		}

		/// <summary>
		/// Returns the index of the TaskFolder within the collection.
		/// </summary>
		/// <param name="path">Path to find.</param>
		/// <returns>Index of the TaskFolder; -1 if not found.</returns>
		public int IndexOf(string path)
		{
			if (v2FolderList != null)
			{
				for (int i = 0; i < v2FolderList.Count; i++)
				{
					if (v2FolderList[new System.Runtime.InteropServices.VariantWrapper(i)].Path == path)
						return i;
				}
				return -1;
			}
			else
				return (v1FolderList.Length > 0 && (path == string.Empty || path == "\\")) ? 0 : -1;
		}
		*/

        /// <summary>
        ///   Gets the specified folder from the collection.
        /// </summary>
        /// <param name="index"> The index of the folder to be retrieved. </param>
        /// <returns> A TaskFolder instance that represents the requested folder. </returns>
        public TaskFolder this[int index] {
            get {
                if (v2FolderList != null) {
                    return new TaskFolder(parent.TaskService, v2FolderList[++index]);
                }
                return v1FolderList[index];
            }
        }

        /// <summary>
        ///   Gets the specified folder from the collection.
        /// </summary>
        /// <param name="path"> The path of the folder to be retrieved. </param>
        /// <returns> A TaskFolder instance that represents the requested folder. </returns>
        public TaskFolder this[string path] {
            get {
                if (v2FolderList != null) {
                    return new TaskFolder(parent.TaskService, v2FolderList[path]);
                }
                if (v1FolderList != null && v1FolderList.Length > 0 && (path == string.Empty || path == "\\")) {
                    return v1FolderList[0];
                }
                throw new ArgumentException("Path not found");
            }
        }

        /*
		/// <summary>
		/// Determines whether the collection contains a specific key and value.
		/// </summary>
		/// <param name="item">The TaskFolder to locate in the collection.</param>
		/// <returns>true if item is found in the collection; otherwise, false.</returns>
		public bool Contains(TaskFolder item)
		{
			return IndexOf(item) != -1;
		}
		*/

        /// <summary>
        ///   Copies the elements of the ICollection to an Array, starting at a particular Array index.
        /// </summary>
        /// <param name="array"> The one-dimensional Array that is the destination of the elements copied from <see
        ///    cref="ICollection{T}" /> . The Array must have zero-based indexing. </param>
        /// <param name="arrayIndex"> The zero-based index in array at which copying begins. </param>
        internal void CopyTo(TaskFolder[] array, int arrayIndex) {
            if (arrayIndex < 0) {
                throw new ArgumentOutOfRangeException();
            }
            if (array == null) {
                throw new ArgumentNullException();
            }
            if (v2FolderList != null) {
                if (arrayIndex + this.Count > array.Length) {
                    throw new ArgumentException();
                }
                foreach (ITaskFolder f in v2FolderList) {
                    array[arrayIndex++] = new TaskFolder(parent.TaskService, f);
                }
            } else {
                if (arrayIndex + v1FolderList.Length > array.Length) {
                    throw new ArgumentException();
                }
                v1FolderList.CopyTo(array, arrayIndex);
            }
        }

        /// <summary>
        ///   Gets the number of items in the collection.
        /// </summary>
        public int Count {
            get {
                return (v2FolderList != null) ? v2FolderList.Count : v1FolderList.Length;
            }
        }

        /// <summary>
        ///   Gets a list of items in a collection.
        /// </summary>
        /// <returns> Enumerated list of items in the collection. </returns>
        public IEnumerator<TaskFolder> GetEnumerator() {
            var eArray = new TaskFolder[this.Count];
            this.CopyTo(eArray, 0);
            return new TaskFolderEnumerator(eArray);
        }

        IEnumerator IEnumerable.GetEnumerator() {
            return this.GetEnumerator();
        }

        private class TaskFolderEnumerator : IEnumerator<TaskFolder> {
            private TaskFolder[] folders;
            private IEnumerator iEnum;

            internal TaskFolderEnumerator(TaskFolder[] f) {
                folders = f;
                iEnum = f.GetEnumerator();
            }

            /// <summary>
            ///   Releases all resources used by this class.
            /// </summary>
            public void Dispose() {
            }

            public TaskFolder Current {
                get {
                    return iEnum.Current as TaskFolder;
                }
            }

            object IEnumerator.Current {
                get {
                    return this.Current;
                }
            }

            public bool MoveNext() {
                return iEnum.MoveNext();
            }

            public void Reset() {
                iEnum.Reset();
            }
        }
    }
}