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
    /// <summary>
    ///   Holds a reference to an icon in a file. An icon locatoin object is 'empty' if its path is zero length.
    /// </summary>
    public class IconLocation {
        private string path = string.Empty;
        private int index;

        /// <summary>
        ///   Creates an empty icon location
        /// </summary>
        public IconLocation() {
            this.path = string.Empty;
            this.index = 0;
        }

        /// <summary>
        ///   Create a icon locatoin object
        /// </summary>
        /// <param name="path"> The path to the file containing the icon </param>
        /// <param name="index"> The index of the icon. </param>
        public IconLocation(string path, int index) {
            this.path = path;
            this.index = index;
        }

        /// <summary>
        ///   Gets the path to the file contaning the icon.
        /// </summary>
        public string Path {
            get {
                return path;
            }
        }

        /// <summary>
        ///   Gets the index of the icon in the file.
        /// </summary>
        public int Index {
            get {
                return index;
            }
        }

        /// <summary>
        ///   True of the icon location is empty.
        /// </summary>
        public bool IsEmpty {
            get {
                return string.IsNullOrWhiteSpace(path);
            }
        }
    }
}