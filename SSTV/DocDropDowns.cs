using System;
using System.Collections.Generic;
using System.Drawing;

using Play.Edit;
using Play.Interfaces.Embedding;
using Play.Sound;
using Play.Rectangles;
using Play.Controls;
using Play.Drawing;

namespace Play.SSTV {
    public class SSTVFamilyList :
        EditMultiColumn,
        IPgLoad< IEnumerable<SSTVDEM.SSTVFamily> >
    {
        public class DDRow : Row {
            public const int ColumnCheck   = 0;
            public const int ColumnFamily  = 1;

            public static new int ColumnCount => 2;
            public DDRow( SSTVDEM.SSTVFamily oFamily ) {
                _rgColumns = new Line[2];

                Family = oFamily ?? throw new ArgumentNullException();

                _rgColumns[ColumnCheck  ] = new TextLine( ColumnCheck,   CheckedMark(false) );
                _rgColumns[ColumnFamily ] = new TextLine( ColumnFamily,   oFamily._strName );
            }

            public SSTVDEM.SSTVFamily Family { get; set; }

            protected string CheckedMark( bool fChecked ) {
                return fChecked ? "*" : "";
            }

            public bool IsChecked {
                get {
                    return _rgColumns[ColumnCheck].ElementCount > 0;
                }
                set {
                    _rgColumns[ColumnCheck].TryReplace( CheckedMark( value ) );
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
        IPgLoad< IEnumerator<SSTVMode> >
    {
        public class DDRow : Row {
            public const int ColumnCheck   = 0;
            public const int ColumnVersion = 1;
            public const int ColumnWidth   = 2;
            public const int ColumnHeight  = 3;

            public static new int ColumnCount => 4;
            public DDRow( SSTVMode oMode ) {
                _rgColumns = new Line[5];

                Mode = oMode ?? throw new ArgumentNullException();

                _rgColumns[ColumnCheck  ] = new TextLine( ColumnCheck,   CheckedMark(false) );
                _rgColumns[ColumnVersion] = new TextLine( ColumnVersion, oMode.Version );
                _rgColumns[ColumnWidth  ] = new TextLine( ColumnWidth,   oMode.Resolution.Width .ToString() );
                _rgColumns[ColumnHeight ] = new TextLine( ColumnHeight,  oMode.Resolution.Height.ToString() );
            }

            public SSTVMode Mode { get; set; }

            protected string CheckedMark( bool fChecked ) {
                return fChecked ? "*" : "";
            }

            public bool IsChecked {
                get {
                    return _rgColumns[ColumnCheck].ElementCount > 0;
                }
                set {
                    _rgColumns[ColumnCheck].TryReplace( CheckedMark( value ) );
                }
            }
        }

        public SSTVModeList(IPgBaseSite oSiteBase) : base(oSiteBase) {
        }


        public bool Load( IEnumerator<SSTVMode> itrMode ) {
            TrackerEnumerable oTE = new TrackerEnumerable( this );

            _rgRows.Clear();

            if( itrMode == null ) {
                throw new ArgumentNullException();
            }

            while( itrMode.MoveNext() ) {
                _rgRows.Add( new DDRow( itrMode.Current ) );
            }

            oTE.FinishUp( EditType.DeleteRow );

            return true;
        }

        public bool InitNew() {
            return true;
        }
    }

    public class ViewFamilyDropDown : ViewDropDown {
        public ViewFamilyDropDown(IPgViewSite oViewSite, object oDocument, ImageBaseDoc oBitmap) : 
            base(oViewSite, oDocument, oBitmap) {
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

            TextLayoutAdd( new LayoutRect( LayoutRect.CSS.Pixels, 11, 1L ), SSTVFamilyList.DDRow.ColumnCheck ); 
            TextLayoutAdd( new LayoutRect( LayoutRect.CSS.None,   20, 1L ), SSTVFamilyList.DDRow.ColumnFamily ); 

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

            TextLayoutAdd( new LayoutRect( LayoutRect.CSS.Pixels, 20, 1L ), SSTVModeList.DDRow.ColumnCheck ); 
            TextLayoutAdd( new LayoutRect( LayoutRect.CSS.Flex,   20, 1L ), SSTVModeList.DDRow.ColumnVersion ); 
            TextLayoutAdd( new LayoutRect( LayoutRect.CSS.Flex,   20, 1L ), SSTVModeList.DDRow.ColumnWidth ); 
            TextLayoutAdd( new LayoutRect( LayoutRect.CSS.Flex,   20, 1L ), SSTVModeList.DDRow.ColumnHeight ); 

            // Do this so we can return a desired height. O.o;;
            _oCacheMan.CacheRepair( null, true, true );

            return true;
        }

    }
}
