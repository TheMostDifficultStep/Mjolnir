using System;
using System.Collections.Generic;

using Play.Edit;
using Play.Interfaces.Embedding;
using Play.Sound;

using static Play.Sound.SSTVDEM;

namespace Play.SSTV {
    public class SSTVTxFamilyDoc :
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

        public SSTVTxFamilyDoc(IPgBaseSite oSiteBase) : base(oSiteBase) {
            CheckColumn = 0; // Just to be clear.
        }

        protected bool _fIsNoneIncluded = false;

        public bool Load( IEnumerable<SSTVDEM.SSTVFamily> rgFamilies ) {
            if( rgFamilies == null ) {
                throw new ArgumentNullException();
            }

            _rgRows.Clear();

            PreLoadRows();

            foreach( SSTVDEM.SSTVFamily oFamily in rgFamilies  ) {
                _rgRows.Add( new DDRow( oFamily, CheckClrValue ) );
            }

            if( _rgRows.Count > 0 ) {
                _rgRows[0][(int)Column.Check].TryReplace( CheckSetValue );
            }

            RenumberAndSumate(); // Each row must be numbered, else cache messes up.
            Raise_DocLoaded  ();

            return true;
        }

        public bool InitNew() {
            return true;
        }

        protected virtual void PreLoadRows() { }

        /// <summary>
        /// Look up the family that supports the given mode and give it
        /// the check mark.
        /// </summary>
        /// <remarks>A window should NOT call this function. This function
        /// will be called by the decode when a new image is being
        /// received.</remarks>
        /// <exception cref="ArgumentNullException" />
        /// <exception cref="InvalidOperationException" />
        /// <seealso cref="SSTVRxFamilyDoc.PreLoadRows"/>
        public bool SelectFamily( TVFamily eFamily ) {
            if( !IsSingleCheck ) {
                throw new InvalidOperationException( "Doc must be in SingleCheck mode." );
            }
            if( !_fIsNoneIncluded && eFamily == TVFamily.None ) {
                throw new ArgumentOutOfRangeException("Can't use None for TX.");
            }

            Raise_DocUpdateBegin();

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
            Raise_DocUpdateEnd( IPgEditEvents.EditType.Column, null );

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

    }

    /// <summary>
    /// List SSTVFamilyList all available SSTV image types. 
    /// </summary>
    public class SSTVRxFamilyDoc :
        SSTVTxFamilyDoc
    {
        public SSTVFamily Auto { get; protected set; }

        public SSTVRxFamilyDoc(IPgBaseSite oSiteBase) : base(oSiteBase) {
        }

        ///<summary>The "none" family is NOT included int the Demodulator's
        ///list of fammilies. But it is part of the TVFamily enum.
        ///Side load the "auto" detect state so that I don't get this
        ///fake family mixed in with the valid families in the enumeration.
        ///</summary
        protected override void PreLoadRows() {
            Auto = new SSTVFamily( TVFamily.None, "Auto", typeof( SSTVModeNone ) );
            _rgRows.Add( new DDRow( Auto, CheckSetValue ) );

            _fIsNoneIncluded = true;
        }

        public void ResetFamily() {
            SelectFamily( Auto.TvFamily );
            HighLight = null;
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
            Time,
            Width,
            Height
        }

        protected List<SSTVMode> AllDescriptors = new List<SSTVMode>();
        public class DDRow : Row {
            public static new int ColumnCount => Enum.GetNames(typeof(Column)).Length;
            public DDRow( SSTVMode oMode, string strCheckMark ) {
                Mode = oMode ?? throw new ArgumentNullException();

                _rgColumns = new Line[ColumnCount];

                int iTime = (int)(( oMode.ScanWidthInMS * oMode.Resolution.Height ) / 1000 );

                _rgColumns[(int)Column.Check  ] = new TextLine( (int)Column.Check,   strCheckMark );
                _rgColumns[(int)Column.Version] = new TextLine( (int)Column.Version, oMode.Version );
                _rgColumns[(int)Column.Time   ] = new TextLine( (int)Column.Time,    iTime.ToString() + "s" );
                _rgColumns[(int)Column.Width  ] = new TextLine( (int)Column.Width,   oMode.Resolution.Width .ToString() );
                _rgColumns[(int)Column.Height ] = new TextLine( (int)Column.Height,  oMode.Resolution.Height.ToString() );
            }

            public SSTVMode Mode { get; set; }
        }

        //public event Action RegisterOnLoaded;
        public SSTVModeDoc(IPgBaseSite oSiteBase) : base(oSiteBase) {
            CheckColumn = 0; // Just to be clear.
        }

        public bool InitNew() {
            foreach( SSTVMode oMode in SSTVDEM.GenerateAllModes ) {
                AllDescriptors.Add( oMode );
            }

            return true;
        }

        /// <summary>
        /// When we are responding to the VIS code on the demodulator
        /// then the eLegacyMode is the mode recieved OR the user is
        /// moving from AUTO to force a listening mode. Else we're clearing
        /// all the descriptors, thus going back to AUTO.
        /// BUT for Tx we simply select the first matching item in the list.
        /// Would be nice if we could set the favorite mode for a given Tx
        /// family and set the check there.
        /// </summary>
        /// <remarks>
        /// It's kind of a drag. We want/need to have a mode descriptor
        /// that describes the listening mode we are in at all times. BUT 
        /// we would like the "auto" family to have no entries in the mode 
        /// list!! O.o
        /// </remarks>
        /// <param name="eFamily">The TVFamily we want to list</param>
        /// <param name="eLegacyMode">If the mode is smEND and we loaded
        /// from the list. Then select the first "default" item. Else
        /// try loading the elligble modes for that family.</param>
        public bool Load( TVFamily eFamily, AllSSTVModes eLegacyMode = AllSSTVModes.smEND ) {
            Clear();

            foreach( SSTVMode oMode in AllDescriptors ) {
                if( oMode.TvFamily == eFamily ) {
                    string strCheck = CheckClrValue;

                    if( eLegacyMode == oMode.LegacyMode )
                        strCheck = CheckSetValue;

                    _rgRows.Add( new DDRow( oMode, strCheck ) );
                }
            }

            // Go back and see if anything got checked due to a given mode
            // If not set the check to the first column if we have anything
            // to list. We are NOT sending a check event!!!
            if( CheckedRow is null && _rgRows.Count > 0 ) {
                _rgRows[0][CheckColumn].TryReplace( _strCheckValue );
            }

            RenumberAndSumate();

            Raise_DocLoaded();

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

        public SSTVMode SelectedMode {
            get {
                if( CheckedRow is DDRow oCheckRow ) 
                    return oCheckRow.Mode;

                return null;
            }
        }
    }

    public class SSTVTxTemplatesDoc :         
        EditMultiColumn,
        IPgLoad
    {
        public enum Column : int {
            Check = 0,
            Descr = 1
        }

        public class DDRow : Row {
            public static new int ColumnCount => Enum.GetNames(typeof(Column)).Length;

            public DDRow( string strDescription, string strCheck = "" ) {
                _rgColumns = new Line[ColumnCount];

                _rgColumns[(int)Column.Check ] = new TextLine( (int)Column.Check, strCheck );
                _rgColumns[(int)Column.Descr ] = new TextLine( (int)Column.Descr, strDescription );
            }

        }

        public SSTVTxTemplatesDoc(IPgBaseSite oSiteBase) : base(oSiteBase) {
            CheckColumn = 0;
        }

        /// <summary>
        /// Initialize the Layout Templates we know about. We depend on the
        /// fixed order right now.
        /// </summary>
        /// <seealso cref="DocSSTV.TemplateSet"/>
        public bool InitNew() {
            // The order for these must not be changed.
            _rgRows.Add( new DDRow( "Reply PnP" ) );
            _rgRows.Add( new DDRow( "General Msg" ) );
            _rgRows.Add( new DDRow( "General Msg Pnp" ));
            _rgRows.Add( new DDRow( "CQ Color Gradient" ));
            _rgRows.Add( new DDRow( "High Def Message" ));
            _rgRows.Add( new DDRow( "High Def CQ", CheckSetValue ));
            _rgRows.Add( new DDRow( "High Def Reply" ));
            _rgRows.Add( new DDRow( "High Def Reply Pnp" ));

            RenumberAndSumate();

            // Do NOT sent a check event. 
            DoParse();

            return true;
        }
    }
}
