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
    using Internal;

    public class ConsoleProperties : ICommiter, ICloneable {
        private const int LF_FACESIZE = 32;
        internal NT_CONSOLE_PROPS nt_console_props;
        private ColorTable colorTable;
        private ShellLink owner;

        public ConsoleProperties() {
            nt_console_props = NT_CONSOLE_PROPS.AnEmptyOne();
            colorTable = new ColorTable(this);
        }

        ///<summary>
        ///  Makes a copy of another ConsoleProperty
        ///</summary>
        ///<remarks>
        ///  Note that the 'owner' field is not copied here.
        ///</remarks>
        public ConsoleProperties(ConsoleProperties another) {
            nt_console_props = another.nt_console_props;
            colorTable = new ColorTable(this);
        }

        /// <summary>
        ///   This should be only called by a ShellLink constructor
        /// </summary>
        /// <param name="owner"> </param>
        internal ConsoleProperties(ShellLink owner)
            : this() {
            this.owner = owner;
        }

        /// <summary>
        ///   Gets or sets the Fill attribute for the console.
        /// </summary>
        public int FillAttribute {
            get {
                return nt_console_props.wFillAttribute;
            }
            set {
                checked {
                    nt_console_props.wFillAttribute = (UInt16)value;
                }
                ;
                this.Commit();
            }
        }

        /// <summary>
        ///   Gets or sets Fill attribute for console popups.
        /// </summary>
        public int PopupFillAttribute {
            get {
                return nt_console_props.wPopupFillAttribute;
            }
            set {
                checked {
                    nt_console_props.wPopupFillAttribute = (UInt16)value;
                }
                ;
                this.Commit();
            }
        }

        /// <summary>
        ///   gets or sets the console's screen buffer size. X is width, Y is height.
        /// </summary>
        public Coordinate ScreenBufferSize {
            get {
                return new Coordinate(nt_console_props.dwScreenBufferSize);
            }
            set {
                checked {
                    nt_console_props.dwScreenBufferSize = value.AsCOORD();
                }
                ;
                this.Commit();
            }
        }

        /// <summary>
        ///   gets or sets the console's window size. X is width, Y is height.
        /// </summary>
        public Coordinate WindowSize {
            get {
                return new Coordinate(nt_console_props.dwWindowSize);
            }
            set {
                checked {
                    nt_console_props.dwWindowSize = value.AsCOORD();
                }
                ;
                this.Commit();
            }
        }

        /// <summary>
        ///   gets or sets the console's window origin. X is left, Y is top.
        /// </summary>
        public Coordinate WindowOrigin {
            get {
                return new Coordinate(nt_console_props.dwWindowOrigin);
            }
            set {
                checked {
                    nt_console_props.dwWindowOrigin = value.AsCOORD();
                }
                ;
                this.Commit();
            }
        }

        /// <summary>
        ///   Gets or sets the font.
        /// </summary>
        public long Font {
            get {
                checked {
                    return (int)nt_console_props.nFont;
                }
            }
            set {
                checked {
                    nt_console_props.nFont = (UInt32)value;
                }
                ;
                this.Commit();
            }
        }

        /// <summary>
        ///   Gets or sets the console's input buffer size.
        /// </summary>
        public long InputBufferSize {
            get {
                checked {
                    return nt_console_props.nInputBufferSize;
                }
            }
            set {
                checked {
                    nt_console_props.nInputBufferSize = (UInt32)value;
                }
                ;
                this.Commit();
            }
        }

        /// <summary>
        ///   gets or sets the console's font size.
        /// </summary>
        public Coordinate FontSize {
            get {
                return new Coordinate(nt_console_props.dwFontSize);
            }
            set {
                checked {
                    nt_console_props.dwFontSize = value.AsCOORD();
                }
                ;
                this.Commit();
            }
        }

        /// <summary>
        ///   Gets or sets the console's font family.
        /// </summary>
        public long FontFamily {
            get {
                checked {
                    return nt_console_props.uFontFamily;
                }
            }
            set {
                checked {
                    nt_console_props.uFontFamily = (UInt32)value;
                }
                ;
                this.Commit();
            }
        }

        /// <summary>
        ///   Gets or sets the console's font weight.
        /// </summary>
        public long FontWeight {
            get {
                checked {
                    return nt_console_props.uFontWeight;
                }
            }
            set {
                checked {
                    nt_console_props.uFontWeight = (UInt32)value;
                }
                ;
                this.Commit();
            }
        }

        /// <summary>
        ///   Gets or sets the console's font face name.
        /// </summary>
        public string FaceName {
            get {
                if (nt_console_props.FaceName[0] == '\0') {
                    return string.Empty;
                }
                int lastChar;
                for (lastChar = 1; lastChar < LF_FACESIZE; ++lastChar) {
                    if (nt_console_props.FaceName[lastChar] == '\0') {
                        break;
                    }
                }

                var facename = new string(nt_console_props.FaceName, 0, lastChar);
                return facename;
            }
            set {
                if (string.IsNullOrWhiteSpace(value)) {
                    for (int i = 0; i < LF_FACESIZE; ++i) {
                        nt_console_props.FaceName[i] = '\0';
                    }
                    this.Commit();
                    return;
                }
                if (value.Length > LF_FACESIZE) {
                    string msg = string.Format("The value is too long for the FaceName.  It must be {0} or less in length.", LF_FACESIZE);
                    throw new ArgumentException(msg);
                }

                {
                    int i;
                    for (i = 0; i < value.Length; ++i) {
                        nt_console_props.FaceName[i] = value[i];
                    }
                    if (i < LF_FACESIZE) {
                        nt_console_props.FaceName[i] = '\0';
                    }
                    this.Commit();
                }
            }
        }

        /// <summary>
        ///   Gets or sets the console's cursor size.
        /// </summary>
        public long CursorSize {
            get {
                checked {
                    return nt_console_props.uCursorSize;
                }
            }
            set {
                checked {
                    nt_console_props.uCursorSize = (UInt32)value;
                }
                ;
                this.Commit();
            }
        }

        /// <summary>
        ///   Gets or sets the console's full screen flag.
        /// </summary>
        public bool FullScreen {
            get {
                return nt_console_props.bFullScreen;
            }
            set {
                nt_console_props.bFullScreen = value;
                this.Commit();
            }
        }

        /// <summary>
        ///   Gets or sets the console's quick edit flag.
        /// </summary>
        public bool QuickEdit {
            get {
                return nt_console_props.bQuickEdit;
            }
            set {
                nt_console_props.bQuickEdit = value;
                this.Commit();
            }
        }

        /// <summary>
        ///   Gets or sets the console's insert mode flag. True for insert mode, false for overrite
        /// </summary>
        public bool InsertMode {
            get {
                return nt_console_props.bInsertMode;
            }
            set {
                nt_console_props.bInsertMode = value;
                this.Commit();
            }
        }

        /// <summary>
        ///   Gets or sets the console's auto position flag. True to auto position the window.
        /// </summary>
        public bool AutoPosition {
            get {
                return nt_console_props.bAutoPosition;
            }
            set {
                nt_console_props.bAutoPosition = value;
                this.Commit();
            }
        }

        /// <summary>
        ///   Gets or sets the size of each console history buffer.
        /// </summary>
        public long HistoryBufferSize {
            get {
                checked {
                    return nt_console_props.uHistoryBufferSize;
                }
            }
            set {
                checked {
                    nt_console_props.uHistoryBufferSize = (UInt32)value;
                }
                ;
                this.Commit();
            }
        }

        /// <summary>
        ///   Gets or sets the number of history buffers for the console.
        /// </summary>
        public long NumberOfHistoryBuffers {
            get {
                checked {
                    return nt_console_props.uNumberOfHistoryBuffers;
                }
            }
            set {
                checked {
                    nt_console_props.uNumberOfHistoryBuffers = (UInt32)value;
                }
                ;
                this.Commit();
            }
        }

        /// <summary>
        ///   Gets or sets the console's histry no dupe flag. True if old duplicate history lists should be discarded, or false otherwise.
        public bool HistoryNoDup {
            get {
                return nt_console_props.bHistoryNoDup;
            }
            set {
                nt_console_props.bHistoryNoDup = value;
                this.Commit();
            }
        }

        /// <summary>
        ///   An array of color reference values for the console. Colors are specified as an index into this array.
        /// </summary>
        public ColorTable ColorTable {
            get {
                return this.colorTable;
            }
        }

        #region ICommiter Members

        public void Commit() {
            if (owner != null) {
                owner.WriteConsoleProperties();
            }
        }

        #endregion

        #region ICloneable Members

        public object Clone() {
            var clone = new ConsoleProperties(this);
            return clone;
        }

        #endregion
    }
}