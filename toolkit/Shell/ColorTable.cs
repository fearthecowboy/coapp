//-----------------------------------------------------------------------
// <copyright company="CoApp Project">
//     Original (c) Author: Richard G Russell (Foredecker) 
//     Changes Copyright (c) 2010  Garrett Serack, CoApp Contributors. All rights reserved.
// </copyright>
// <license>
//     The software is licensed under the Apache 2.0 License (the "License")
//     You may not use the software except in compliance with the License. 
// </license>
//-----------------------------------------------------------------------

// -----------------------------------------------------------------------
// Original Code: 
// (c) Author: Richard G Russell (Foredecker) 
// This code is licensed under the MS-PL
// http://www.opensource.org/licenses/ms-pl.html
// -----------------------------------------------------------------------

namespace CoApp.Toolkit.Shell {
    using System;
    using System.Collections;
    using System.Collections.Generic;

    public class ColorTable : IList<long>, ICommiter {
        private ConsoleProperties owner;

        internal ColorTable(ConsoleProperties owner) {
            this.owner = owner;
        }

        #region IList<long> Members

        public int IndexOf(long item) {
            UInt32 value;
            checked {
                value = (UInt32)item;
            }
            for (int i = 0; i < owner.nt_console_props.ColorTable.Length; i++) {
                if (owner.nt_console_props.ColorTable[i] == value) {
                    return i;
                }
            }

            return -1;
        }

        public void Insert(int index, long item) {
            throw new NotImplementedException();
        }

        public void RemoveAt(int index) {
            throw new NotImplementedException();
        }

        public long this[int index] {
            get {
                return owner.nt_console_props.ColorTable[index];
            }
            set {
                checked {
                    owner.nt_console_props.ColorTable[index] = (UInt32)value;
                }
                ;
                this.Commit();
            }
        }

        #endregion

        #region ICollection<long> Members

        public void Add(long item) {
            throw new NotImplementedException();
        }

        public void Clear() {
            for (int i = 0; i < owner.nt_console_props.ColorTable.Length; i++) {
                owner.nt_console_props.ColorTable[i] = 0;
            }
            this.Commit();
        }

        public bool Contains(long item) {
            return this.IndexOf(item) >= 0;
        }

        public void CopyTo(long[] array, int arrayIndex) {
            for (int i = 0; i < owner.nt_console_props.ColorTable.Length; i++) {
                array[i + arrayIndex] = owner.nt_console_props.ColorTable[i];
            }
        }

        public int Count {
            get {
                return owner.nt_console_props.ColorTable.Length;
            }
        }

        public bool IsReadOnly {
            get {
                return false;
            }
        }

        public bool Remove(long item) {
            throw new NotImplementedException();
        }

        #endregion

        #region IEnumerable<long> Members

        public IEnumerator<long> GetEnumerator() {
            throw new NotImplementedException();
        }

        #endregion

        #region IEnumerable Members

        IEnumerator IEnumerable.GetEnumerator() {
            throw new NotImplementedException();
        }

        #endregion

        #region ICommiter Members

        public void Commit() {
            if (owner != null) {
                owner.Commit();
            }
        }

        #endregion
    }
}