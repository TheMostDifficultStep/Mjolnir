using System;
using System.Collections.Generic;

using Play.Edit;
using Play.Interfaces.Embedding;
using Play.Sound;
using Play.Rectangles;
using Play.Controls;
using Play.Drawing;
using System.Drawing;
using System.Collections;
using static Play.Sound.SSTVDEM;

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
            public DDRow( SSTVDEM.SSTVFamily oFamily, string strCheck ) {
                Family = oFamily ?? throw new ArgumentNullException();

                _rgColumns = new Line[ColumnCount];

                _rgColumns[(int)Column.Check  ] = new TextLine( (int)Column.Check,  strCheck );
                _rgColumns[(int)Column.Family ] = new TextLine( (int)Column.Family, oFamily._strName );
            }

            public SSTVDEM.SSTVFamily Family { get; set; }
        }

        public SSTVFamilyList(IPgBaseSite oSiteBase) : base(oSiteBase) {
            CheckColumn = 0; // Just to be clear.
        }


        public bool Load( IEnumerable<SSTVDEM.SSTVFamily> rgFamilies ) {
            if( rgFamilies == null ) {
                throw new ArgumentNullException();
            }

            TrackerEnumerable oTE = new TrackerEnumerable( this );

            _rgRows.Clear();

            int iCount = 0;
            foreach( SSTVDEM.SSTVFamily oFamily in rgFamilies  ) {
                IPgDocCheckMarks.CheckTypes eType = (iCount==0) ? 
                    IPgDocCheckMarks.CheckTypes.Marked : 
                    IPgDocCheckMarks.CheckTypes.Clear;

                _rgRows.Add( new DDRow( oFamily, GetCheckValue( eType ) ) );
                iCount++;
            }

            RenumberAndSumate(); // Each row must be numbered, else cache messes up.

            oTE.FinishUp( EditType.DeleteRow );

            return true;
        }

        public bool InitNew() {
            return true;
        }

        /// <summary>
        /// Find the currently selected family. There should always be a single selection
        /// if not we are in an error. Throws exceptions if nothing selected or if more 
        /// than one item selected!!
        /// </summary>
        /// <exception cref="InvalidOperationException" />
        public SSTVDEM.SSTVFamily SelectedFamily {
            get {
                DDRow oSelected = null;
                foreach( DDRow oRow in _rgRows ) {
                    if( oRow[(int)SSTVFamilyList.Column.Check].AsSpan.CompareTo( _strCheckValue, StringComparison.Ordinal ) == 0 ) {
                        if( oSelected != null ) {
                            throw new InvalidOperationException( "Multi select happening on Single select question" );
                        }
                        oSelected = oRow;
                    }
                }
                if( oSelected == null ) {
                    throw new InvalidOperationException( "Nothing Selected" );
                }

                return oSelected.Family;
            }
        }
    }

    /// <summary>
    /// List SSTVModes for the currently selected family. 
    /// </summary>
    public class SSTVModeList :
        EditMultiColumn,
        IPgLoad< SSTVDEM.SSTVFamily >
    {
        public enum Column : int {
            Check = 0,
            Version,
            Width,
            Height
        }

        public class DDRow : Row {
            public static new int ColumnCount => Enum.GetNames(typeof(Column)).Length;
            public DDRow( SSTVMode oMode, string strCheckMark ) {
                Mode = oMode ?? throw new ArgumentNullException();

                _rgColumns = new Line[ColumnCount];

                _rgColumns[(int)Column.Check  ] = new TextLine( (int)Column.Check,   strCheckMark );
                _rgColumns[(int)Column.Version] = new TextLine( (int)Column.Version, oMode.Version );
                _rgColumns[(int)Column.Width  ] = new TextLine( (int)Column.Width,   oMode.Resolution.Width .ToString() );
                _rgColumns[(int)Column.Height ] = new TextLine( (int)Column.Height,  oMode.Resolution.Height.ToString() );
            }

            public SSTVMode Mode { get; set; }
        }

        public SSTVModeList(IPgBaseSite oSiteBase) : base(oSiteBase) {
        }

        /// <summary>
        /// We are reloadable. We clear out old rows in favor of the new enumerable.
        /// </summary>
        /// <exception cref="ArgumentNullException"></exception>
        public bool Load( SSTVDEM.SSTVFamily oFamily ) {
            if( oFamily == null )
                throw new ArgumentNullException();

            TrackerEnumerable oTE = new TrackerEnumerable( this );

            _rgRows.Clear();
            int iCount = 0;
            foreach( SSTVMode oMode in oFamily ) {
                // TODO: Add a default value to the entry so we can add the
                //       check mark to whoever!!
                IPgDocCheckMarks.CheckTypes eType = (iCount==0) ? 
                    IPgDocCheckMarks.CheckTypes.Marked : 
                    IPgDocCheckMarks.CheckTypes.Clear;

                _rgRows.Add( new DDRow( oMode, GetCheckValue( eType ) ) );
                iCount++;
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

    public class ViewModeDDEditBox : ViewDDEditBox {
        public ViewModeDDEditBox(IPgViewSite oViewSite, object oDocument) : 
            base(oViewSite, oDocument) {
        }

        public override ViewDDPopup CreatePopup() {
            return new ViewSSTVModesPopup( new WinSlot( this ), _oDocBag );
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
