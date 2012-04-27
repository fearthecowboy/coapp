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
    using System.Text;
    using Internal;

    /// <summary>
    ///   A coordinate. Values are limited to UInt16.MinValue and UInt16.MaxValue
    /// </summary>
    /// <remarks>
    ///   This structure maps to the native COORD data strcture. The values used here are ints so this class is CLS compliant.
    /// </remarks>
    public struct Coordinate : IEquatable<Coordinate> {
        private int x;
        private int y;

        /// <summary>
        ///   Create a coordinate with specified X and Y values.
        /// </summary>
        public Coordinate(int x, int y) {
            if (x > UInt16.MaxValue) {
                string msg = string.Format("must be <= {0}", UInt16.MaxValue);
                throw new ArgumentException(msg, "x");
            }
            if (x < UInt16.MinValue) {
                string msg = string.Format("must be >= {0}", UInt16.MinValue);
                throw new ArgumentException(msg, "x");
            }
            if (y > UInt16.MaxValue) {
                string msg = string.Format("must be <= {0}", UInt16.MaxValue);
                throw new ArgumentException(msg, "y");
            }
            if (y < UInt16.MinValue) {
                string msg = string.Format("must be >= {0}", UInt16.MinValue);
                throw new ArgumentException(msg, "y");
            }

            this.x = x;
            this.y = y;
        }

        /// <summary>
        ///   Make a copy of another Coordiante
        /// </summary>
        public Coordinate(Coordinate another) {
            x = another.X;
            y = another.Y;
        }

        internal Coordinate(COORD coord) {
            x = coord.X;
            y = coord.Y;
        }

        internal COORD AsCOORD() {
            unchecked {
                COORD coord;
                coord.X = (Int16)this.X;
                coord.Y = (Int16)this.Y;
                return coord;
            }
        }

        public override bool Equals(object obj) {
            if (obj is Coordinate) {
                var other = (Coordinate)obj;
                return this.Equals(other);
            }
            return false;
        }

        public override int GetHashCode() {
            return this.X.GetHashCode() + this.X.GetHashCode();
        }

        public override string ToString() {
            string xs = x.ToString();
            string ys = y.ToString();
            var sb = new StringBuilder(xs.Length + ys.Length + 1);
            sb.Append(xs);
            sb.Append(',');
            sb.Append(ys);
            return sb.ToString();
        }

        /// <summary>
        ///   Gets and sets the coordinates X value
        /// </summary>
        public int X {
            get {
                return x;
            }
            set {
                if (value > UInt16.MaxValue) {
                    string msg = string.Format("value for X must be <= {0}", UInt16.MaxValue);
                    throw new ArgumentException(msg);
                }
                if (value < UInt16.MinValue) {
                    string msg = string.Format("value for X must be >= {0}", UInt16.MinValue);
                    throw new ArgumentException(msg);
                }
                x = value;
            }
        }

        /// <summary>
        ///   Gets and sets the coordinates Y value
        /// </summary>
        public int Y {
            get {
                return y;
            }
            set {
                if (value > UInt16.MaxValue) {
                    string msg = string.Format("value for Y must be <= {0}", UInt16.MaxValue);
                    throw new ArgumentException(msg);
                }
                if (value < UInt16.MinValue) {
                    string msg = string.Format("value for Y must be >= {0}", UInt16.MinValue);
                    throw new ArgumentException(msg);
                }
                y = value;
            }
        }

        #region IEquatable<Coordinate> Members

        public bool Equals(Coordinate other) {
            if (this.X == other.X && this.Y == other.Y) {
                return true;
            }
            return false;
        }

        #endregion
    }
}