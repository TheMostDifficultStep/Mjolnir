using System;
using System.Collections.Generic;

using Play.Edit;
using Play.Interfaces.Embedding;
using Play.Sound;
using Play.Rectangles;
using Play.Controls;
using Play.Drawing;

namespace Play.SSTV {
    /// <summary>
    /// List SSTVFamilyList all available SSTV image types. 
    /// </summary>
    public class SSTVFamilyList :
        EditMultiColumn,
        IPgLoad< IEnumerable<SSTVDEM.SSTVFamily> >
    {
        public enum Column : int {
            Check = 0,
            Family
        }

        public class DDRow : Row {
            public static new int ColumnCount => Enum.GetNames(typeof(Column)).Length;
            public DDRow( SSTVDEM.SSTVFamily oFamily ) {
                Family = oFamily ?? throw new ArgumentNullException();

                _rgColumns = new Line[ColumnCount];

                _rgColumns[(int)Column.Check  ] = new TextLine( (int)Column.Check,  CheckedMark(false) );
                _rgColumns[(int)Column.Family ] = new TextLine( (int)Column.Family, oFamily._strName );
            }

            public SSTVDEM.SSTVFamily Family { get; set; }

            protected string CheckedMark( bool fChecked ) {
                return fChecked ? "\x2714" : "";
            }

            public bool IsChecked {
                get {
                    return _rgColumns[(int)Column.Check].ElementCount > 0;
                }
                set {
                    _rgColumns[(int)Column.Check].TryReplace( CheckedMark( value ) );
                }
            }
        }

        public SSTVFamilyList(IPgBaseSite oSiteBase) : base(oSiteBase) {
        }


        public bool Load( IEnumerable<SSTVDEM.SSTVFamily> rgFamilies ) {
            if( rgFamilies == null ) {
                throw new ArgumentNullException();
            }

            TrackerEnumerable oTE = new TrackerEnumerable( this );

            _rgRows.Clear();

            foreach( SSTVDEM.SSTVFamily oFamily in rgFamilies  ) {
                _rgRows.Add( new DDRow( oFamily ) );
            }

            RenumberAndSumate(); // Each row must be numbered, else cache messes up.

            oTE.FinishUp( EditType.DeleteRow );

            return true;
        }

        public bool InitNew() {
            return true;
        }
    }

    /// <summary>
    /// List SSTVModes for the currently selected family. 
    /// </summary>
    public class SSTVModeList :
        EditMultiColumn,
        IPgLoad< IEnumerable<SSTVMode> >
    {
        public enum Column : int {
            Check = 0,
            Version,
            Width,
            Height
        }

        public class DDRow : Row {
            public static new int ColumnCount => Enum.GetNames(typeof(Column)).Length;
            public DDRow( SSTVMode oMode ) {
                Mode = oMode ?? throw new ArgumentNullException();

                _rgColumns = new Line[ColumnCount];

                _rgColumns[(int)Column.Check  ] = new TextLine( (int)Column.Check,   CheckedMark(false) );
                _rgColumns[(int)Column.Version] = new TextLine( (int)Column.Version, oMode.Version );
                _rgColumns[(int)Column.Width  ] = new TextLine( (int)Column.Width,   oMode.Resolution.Width .ToString() );
                _rgColumns[(int)Column.Height ] = new TextLine( (int)Column.Height,  oMode.Resolution.Height.ToString() );
            }

            public SSTVMode Mode { get; set; }

            protected string CheckedMark( bool fChecked ) {
                return fChecked ? "\x2714" : "";
            }

            public bool IsChecked {
                get {
                    return _rgColumns[(int)Column.Check].ElementCount > 0;
                }
                set {
                    _rgColumns[(int)Column.Check].TryReplace( CheckedMark( value ) );
                }
            }
        }

        public SSTVModeList(IPgBaseSite oSiteBase) : base(oSiteBase) {
        }

        public bool Load( IEnumerable<SSTVMode> rgModes ) {
            if( rgModes == null ) {
                throw new ArgumentNullException();
            }

            TrackerEnumerable oTE = new TrackerEnumerable( this );

            _rgRows.Clear();

            foreach( SSTVMode oMode in rgModes ) {
                _rgRows.Add( new DDRow( oMode ) );
            }

            RenumberAndSumate();

            oTE.FinishUp( EditType.DeleteRow );

            return true;
        }

        public bool InitNew() {
            return true;
        }
    }

    public class ViewFamilyDDEditBox : ViewDDEditBox {
        public ViewFamilyDDEditBox(IPgViewSite oViewSite, object oDocument) : 
            base(oViewSite, oDocument) {
        }

        public override ViewDDPopup CreatePopup() {
            return new ViewSSTVFamilyPopup( new WinSlot( this ), _oDocBag );
        }
    }

    public class ViewSSTVFamilyPopup: ViewDDPopup {
        public ViewSSTVFamilyPopup( IPgViewSite oView, object oDocument ) : 
            base(oView, oDocument) 
        {
        }

        protected override bool Initialize() {
            if( !base.Initialize() )
                return false;

            // TODO: Check the width of a checkmark at the current font... :-/
            TextLayoutAdd( new LayoutRect( LayoutRect.CSS.Pixels, 15, 1L ), (int)SSTVFamilyList.Column.Check ); 
            TextLayoutAdd( new LayoutRect( LayoutRect.CSS.None,   20, 1L ), (int)SSTVFamilyList.Column.Family ); 

            // Do this so we can return a desired height. O.o;;
            _oCacheMan.CacheRepair( null, true, true );

            return true;
        }

    }

    public class ViewSSTVModesPopup : ViewDDPopup {
        public ViewSSTVModesPopup( IPgViewSite oView, object oDocument ) : 
            base(oView, oDocument) 
        {
        }

        protected override bool Initialize() {
            if( !base.Initialize() )
                return false;

            TextLayoutAdd( new LayoutRect( LayoutRect.CSS.Pixels, 15, 1L ), (int)SSTVModeList.Column.Check ); 
            TextLayoutAdd( new LayoutRect( LayoutRect.CSS.Flex,   20, 1L ), (int)SSTVModeList.Column.Version ); 
            TextLayoutAdd( new LayoutRect( LayoutRect.CSS.Flex,   20, 1L ), (int)SSTVModeList.Column.Width ); 
            TextLayoutAdd( new LayoutRect( LayoutRect.CSS.Flex,   20, 1L ), (int)SSTVModeList.Column.Height ); 

            // Do this so we can return a desired height. O.o;;
            _oCacheMan.CacheRepair( null, true, true );

            return true;
        }

    }
}
