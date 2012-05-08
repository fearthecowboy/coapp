//-----------------------------------------------------------------------
// <copyright company="CoApp Project">
//     Copyright (c) 2010-2012 Garrett Serack and CoApp Contributors. 
//     Contributors can be discovered using the 'git log' command.
//     All rights reserved.
// </copyright>
// <license>
//     The software is licensed under the Apache 2.0 License (the "License")
//     You may not use the software except in compliance with the License. 
// </license>
//-----------------------------------------------------------------------

namespace CoApp.Toolkit.Collections {
    using System;
    using System.Collections.Generic;
    using System.Runtime.Serialization;

    /// <remarks>
    ///   Needed because this indexer of System.Collections.Generic.DictionaryTKeyTValue is not virtual.
    /// </remarks>
    [Serializable]
    internal class InternalDictionary<TKey, TValue> : Dictionary<TKey, TValue> {
        public InternalDictionary() {
        }

        public InternalDictionary(SerializationInfo info, StreamingContext context) : base(info, context) {
        }

        public InternalDictionary(int capacity) : base(capacity) {
        }

        public InternalDictionary(IEqualityComparer<TKey> comparer) : base(comparer) {
        }

        public InternalDictionary(IDictionary<TKey, TValue> dictionary) : base(dictionary) {
        }
    }
}