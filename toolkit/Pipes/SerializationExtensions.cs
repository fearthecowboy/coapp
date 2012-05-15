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

namespace CoApp.Toolkit.Pipes {
    public static class SerializationExtensions {
        public static UrlEncodedMessage Serialize( this object obj,string seperator = "&" , bool storeTypeNames = false) {
            return new UrlEncodedMessage(null, seperator, storeTypeNames) { { null, obj, obj.GetType() } };
        }
        public static UrlEncodedMessage Serialize<T>(this object obj, string seperator = "&", bool storeTypeNames = false) {
            return new UrlEncodedMessage(null, seperator, storeTypeNames) { { null, obj, typeof(T) } };
        }
    }
}