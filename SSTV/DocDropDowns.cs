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
    public class SSTVFamilyDoc :
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

        public SSTVFamilyDoc(IPgBaseSite oSiteBase) : base(oSiteBase) {
            CheckColumn = 0; // Just to be clear.
        }

        public SSTVFamily Auto { get; protected set; }

        public bool Load( IEnumerable<SSTVDEM.SSTVFamily> rgFamilies ) {
            if( rgFamilies == null ) {
                throw new ArgumentNullException();
            }

            TrackerEnumerable oTE = new TrackerEnumerable( this );

            _rgRows.Clear();

            // Let's side load the "auto" detect state so that I don't get this
            // fake family mixed in with the valid families in the enumeration.
            {
                Auto = new SSTVFamily( TVFamily.None, "Auto", typeof( SSTVModeNone ) );
                _rgRows.Add( new DDRow( Auto, CheckSetValue ) );
            }

            foreach( SSTVDEM.SSTVFamily oFamily in rgFamilies  ) {
                _rgRows.Add( new DDRow( oFamily, CheckClrValue ) );
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
                if( CheckedRow is DDRow oFRow ) {
                    return oFRow.Family;
                }
                throw new InvalidOperationException( "Nothing Selected" );
            }
        }

        public void ResetFamily() {
            SelectFamily( Auto.TvFamily );
            HighLight = null;
        }

        /// <summary>
        /// Look up the family that supports the given mode and give it
        /// the check mark.
        /// </summary>
        /// <remarks>A window should NOT call this function. This function
        /// will be called by the decode when a new image is being
        /// received.</remarks>
        /// <exception cref="ArgumentNullException" />
        /// <exception cref="InvalidOperationException" />
        public bool SelectFamily( TVFamily eFamily ) {
            if( !IsSingleCheck ) {
                throw new InvalidOperationException( "Doc must be in SingleCheck mode." );
            }

            DDRow oSelected = null;

            foreach( DDRow oRow in _rgRows ) {
                oRow[(int)Column.Check].TryReplace( _strCheckClear );

                if( oSelected == null && oRow.Family.TvFamily == eFamily ) {
                    oRow[(int)Column.Check].TryReplace( _strCheckValue );

                    oSelected = oRow;
                }
            }

            // Techically we should NOT be missing a family. But clearly a problem.
            if( oSelected is null ) {
                return false;
            }

            // Do NOT sent a check event. 
            DoParse();

            return true;
        }
    }

    /// <summary>
    /// List SSTVModes for the currently selected family. 
    /// </summary>
    public class SSTVModeDoc :
        EditMultiColumn,
        IPgLoad< AllSSTVModes >
    {
        public enum Column : int {
            Check = 0,
            Version,
            Width,
            Height
        }

        protected List<SSTVMode> AllDescriptors = new List<SSTVMode>();
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

        public event Action RegisterOnLoaded;
        public SSTVModeDoc(IPgBaseSite oSiteBase) : base(oSiteBase) {
            CheckColumn = 0; // Just to be clear.
        }

        public bool InitNew() {
            foreach( SSTVMode oMode in SSTVDEM.GenerateAllModes ) {
                AllDescriptors.Add( oMode );
            }

            return true;
        }

        public bool Load( TVFamily eFamily, AllSSTVModes eLegacyMode = AllSSTVModes.smEND ) {
            TrackerEnumerable oTE = new TrackerEnumerable( this );

            Clear();

            foreach( SSTVMode oMode in AllDescriptors ) {
                if( oMode.TvFamily == eFamily ) {
                    string strCheck = eLegacyMode == oMode.LegacyMode ? CheckSetValue : CheckClrValue;

                    _rgRows.Add( new DDRow( oMode, strCheck ) );
                }
            }

            RenumberAndSumate();

            oTE.FinishUp( EditType.InsertRow ); // This will call DoParse();

            RegisterOnLoaded?.Invoke();

            return true;
        }

        public SSTVMode GetDescriptor( AllSSTVModes eLegacyMode ) {
            // Find the mode descriptor from the legacy mode enum
            foreach( SSTVMode oMode in AllDescriptors ) {
                if( oMode.LegacyMode == eLegacyMode ) {
                    return oMode;
                }
            }
            return null;
        }

        /// <summary>
        /// We are reloadable. Find the given mode we are in and 
        /// select the entire family into our display list.
        /// </summary>
        /// <remarks>
        /// We load all possible modes and then choose
        /// them from the particular family so we're not constantly allocating
        /// the mode descripters.
        /// </remarks>
        public bool Load( AllSSTVModes eLegacyMode ) {
            SSTVMode oGiven = GetDescriptor( eLegacyMode );

            if( oGiven != null ) {
                Load( oGiven.TvFamily, eLegacyMode );
                return true;
            } else {
                Clear();
            }

            return false;
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
            TextLayoutAdd( new LayoutRect( LayoutRect.CSS.Pixels, 20, 1L ), (int)SSTVFamilyDoc.Column.Check ); 
            TextLayoutAdd( new LayoutRect( LayoutRect.CSS.None,   20, 1L ), (int)SSTVFamilyDoc.Column.Family ); 

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

            // I'd really like to use flex. But that seems broken at present...
            TextLayoutAdd( new LayoutRect( LayoutRect.CSS.Pixels,  20, 1L ), (int)SSTVModeDoc.Column.Check ); 
            TextLayoutAdd( new LayoutRect( LayoutRect.CSS.Percent, 20, 1L ), (int)SSTVModeDoc.Column.Version ); 
            TextLayoutAdd( new LayoutRect( LayoutRect.CSS.Percent, 20, 1L ), (int)SSTVModeDoc.Column.Width ); 
            TextLayoutAdd( new LayoutRect( LayoutRect.CSS.Percent, 20, 1L ), (int)SSTVModeDoc.Column.Height ); 

            // Do this so we can return a desired height. O.o;;
            _oCacheMan.CacheRepair( null, true, true );

            return true;
        }
    }

    public class ViewSSTVModesAsList : WindowMultiColumn {
        public ViewSSTVModesAsList(IPgViewSite oViewSite, object oDocument) : base(oViewSite, oDocument) {
        }

        protected override bool Initialize() {
            if( !base.Initialize() )
                return false;

            // I'd really like to use flex. But that seems broken at present...
            TextLayoutAdd( new LayoutRect( LayoutRect.CSS.Pixels,  20, 1L ), (int)SSTVModeDoc.Column.Check ); 
            TextLayoutAdd( new LayoutRect( LayoutRect.CSS.Percent, 20, 1L ), (int)SSTVModeDoc.Column.Version ); 
            TextLayoutAdd( new LayoutRect( LayoutRect.CSS.Percent, 20, 1L ), (int)SSTVModeDoc.Column.Width ); 
            TextLayoutAdd( new LayoutRect( LayoutRect.CSS.Percent, 20, 1L ), (int)SSTVModeDoc.Column.Height ); 

            // Do this so we can return a desired height. O.o;;
            _oCacheMan.CacheRepair( null, true, true );

            return true;
        }
    }
}
