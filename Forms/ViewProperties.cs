using System;
using System.Collections.Generic;
using System.Windows.Forms;
using System.Drawing; // size variable being used...

using SkiaSharp;

using Play.Rectangles;
using Play.Interfaces.Embedding;
using Play.Edit;
using Play.Parse;
using static Play.Forms.DocProperties; // weird?

namespace Play.Forms {
    /// <summary>
    /// View the DocProperties object. This makes two columns, label on the left
	/// and value on the right. It is capable of adding an editwindow for the values.
	/// We'll turn that into optionally a dropdown in the future.
    /// </summary>
    /// <seealso cref="DocProperties"/>
    public class WindowStandardProperties : 
        WindowMultiColumn,
        IPgParent,
        IPgLoad
     {
        protected DocProperties Document{ get; }
        protected List<Row>     TabList { get; set; } = new ();
        protected List<int>     TabOrder{ get; } = new List<int>();

		public SKColor BgColorDefault { get; protected set; }

		protected class WinSlot :
			IPgViewSite
		{
			protected readonly WindowStandardProperties _oHost;

			public WinSlot( WindowStandardProperties oHost ) {
				_oHost = oHost ?? throw new ArgumentNullException();
			}

			public IPgParent Host => _oHost;

			public void LogError(string strMessage, string strDetails, bool fShow=true) {
				_oHost._oSiteView.LogError( strMessage, strDetails );
			}

			public void Notify( ShellNotify eEvent ) {
				_oHost._oSiteView.Notify( eEvent );
			}

            public IPgViewNotify EventChain => _oHost._oSiteView.EventChain;
        }

        public WindowStandardProperties( IPgViewSite oSiteView, DocProperties oDocument ) : 
            base( oSiteView, oDocument ) 
        {
            Document = oDocument ?? throw new ArgumentNullException( "ViewStandardProperties's Document is null." );

			BgColorDefault = _oStdUI.ColorsStandardAt( StdUIColors.BG );
        }

        protected override void Dispose(bool disposing) {
            if( disposing ) {
                // Close child windows?
            }
            base.Dispose(disposing);
        }

        /// <summary>
        /// WARNING: This gets called BEFORE our constructor. O.o Unusual side effect of
        /// overriding a base class function which is called in the base class's constructor!!!
        /// Pretty evil. TODO: Let's move this to the InitNew() step on the Window.
        /// We'll loose readonly status on the _oCacheMan on the base class. But gain
        /// a bit in readability/understandability.
        /// </summary>
        /// <remarks>At present we double down on the evil by initializing the List for the
        /// fixed cache here. That way the CacheManSiteSubSet can find FixedRows already
        /// established. It's nicer to have the fixedcache rows established here in this
        /// class because it's where we are calling PropertyInitRow() in the first place.
        /// </remarks>
        protected override CacheMultiBase CreateCacheMan() {
            CacheMultiBase oCacheMan = new CacheMultiBase( new CacheManSiteSubSet( this ) ); 
            return oCacheMan;
        }

        protected class CacheManSiteSubSet :
            CacheManSite 
        {
            IReadOnlyList<int> TabOrder { get; } 
            public CacheManSiteSubSet( WindowStandardProperties oHost) : base(oHost) 
            {
                // The base host doesn't understand tab order but WSP does.
                TabOrder = oHost.TabOrder;
            }

            public override Row TabStop(int iIndex) {
                return this[TabOrder[iIndex]];
            }

            public override int TabCount => TabOrder.Count;

            /// <summary>
            /// Since the Views on the property page must be established once
            /// only. We can take advantage of the fixed cache to supply us with
            /// the TabOrder!! BUT if doc reloaded the cache rows are invalid!!
            /// </summary>
            /// <param name="oRow">Current row we want to navigate from.</param>
            /// <param name="iDir">Direction from that point. +1, -1 typically.</param>
            /// <returns></returns>
            public override Row GetNextTab( Row oRow, int iDir ) {
                try {
                    int iIndex = -1;

                    for( int i=0; i<TabOrder.Count; ++i ) {
                        if( TabOrder[i] == oRow.At ) {
                            iIndex = i;
                            break;
                        }
                    }
                    if( iIndex < 0 ) {
                        return null;
                    }

                    iIndex += iDir;

                    if( iIndex >= TabOrder.Count )
                        return null;
                    if( iIndex < 0 ) 
                        return null;

                    return this[TabOrder[iIndex]];
                } catch( Exception oEx ) {
                    Type[] rgErrors = { typeof( NullReferenceException ),
                                        typeof( ArgumentOutOfRangeException ) };
                    if( rgErrors.IsUnhandled( oEx ) )
                        throw;

                    LogError( "Tab Order", "Problem with tab list" );
                }

                return null;
            }
        } // End CacheManSiteSubset

        protected override bool Initialize() {
            if( !base.Initialize() )
                return false;

            TextLayoutAdd( new LayoutRect( LayoutRect.CSS.Percent, 30, 1L ), PropertyRow.ColumnLabel ); 
            TextLayoutAdd( new LayoutRect( LayoutRect.CSS.None,    70, 1L ), PropertyRow.ColumnValue ); 

            InitRows();

            // This remeasures things so OnSizeChange() responds properly.
            _oCacheMan.CacheRepair( null, fFindCaret:false, fMeasure:true );

            // This certainly does not belong on the base form, but here
            // it is a little more reasonable.
            HyperLinks.Add( "callsign", OnCallSign );
            HyperLinks.Add( "url",      OnURL );

            return true;
        }

        public void PropertyInitRow( int iIndex, Control oWinValue ) {
            // If the value is a multi-line value make an editor. And ignore the value line (if there is one).
            if( oWinValue is IPgLoad oWinLoad ) {
                oWinLoad.InitNew();
            }
            oWinValue.Parent = this;

            Row         oRow   = Document[ iIndex ];
            FTCacheWrap oLabel = new FTCacheWrap( oRow[0] );
            CacheRow    oCRow  = new CacheRow2  ( oRow );

            oLabel.BgColor = _oStdUI.ColorsStandardAt( StdUIColors.BGReadOnly );

            oCRow.CacheColumns.Add( oLabel );
            oCRow.CacheColumns.Add( new CacheControl( oWinValue ) { MaxHeight = 800 });

            _oCacheMan.Add( oCRow );

            TabOrder.Add( iIndex );
        }

        public void PropertyInitRow( int iIndex ) {
            Row         oDRow  = Document[ iIndex ];
            CacheRow    oCRow  = new CacheRow2  ( oDRow );
            FTCacheWrap oLabel = new FTCacheWrap( oDRow[0] );
            FTCacheWrap oValue = new FTCacheWrap( oDRow[1] );

            if( Document.ValueBgColor.TryGetValue(iIndex, out SKColor skBgColorOverride) ) {
                oValue.BgColor = skBgColorOverride;
            }
            oLabel.BgColor = _oStdUI.ColorsStandardAt(StdUIColors.BGReadOnly);

            oCRow.CacheColumns.Add( oLabel );
            oCRow.CacheColumns.Add( oValue );

            _oCacheMan.Add( oCRow );

            TabOrder.Add( iIndex );
        }

        /// <summary>
        /// this is the default InitRows() function that set's everything. However InitNew() might
        /// get overriden and call the InitRows( int[] ) on a subset. 
        /// </summary>
        public virtual void InitRows() {
            try {
                for( int i = 0; i< Document.PropertyCount; ++i ) {
                    PropertyInitRow( i );
                }
            } catch( Exception oEx ) {
                if( IsStdErrorUnhandled( oEx ) )
                    throw;

                LogError( "Bad property page index list" );
            }
        }

        public virtual void InitRows( int[] rgShow ) {
            try {
                foreach( int iIndex in rgShow ) { 
                    PropertyInitRow( iIndex );
                }
            } catch( Exception oEx ) {
                if( IsStdErrorUnhandled( oEx ) )
                    throw;

                LogError( "Bad property page index tab list" );
            }
        }

        public override Size GetPreferredSize(Size proposedSize) {
            return new Size( Width, _oCacheMan.PreferedHeight );
        }

        protected bool IsStdErrorUnhandled( Exception oEx ) {
            Type[] rgErrors = { typeof( IndexOutOfRangeException ),
                                typeof( ArgumentOutOfRangeException ),
                                typeof( NullReferenceException ),
                                typeof( ArgumentNullException ) };
            return( rgErrors.IsUnhandled( oEx ) );
        }

        protected void OnCallSign( Row oRow, int iColumn, IPgWordRange oRange ) {
            BrowserLink( "http://www.qrz.com/db/" +  oRow[1].SubString( oRange.Offset, oRange.Length) );
        }

        protected void OnURL( Row oRow, int iColumn, IPgWordRange oRange ) {
            BrowserLink( oRow[1].SubString( oRange.Offset, oRange.Length) );
        }

        public void OnFormUpdate(IEnumerable<Line> rgUpdates) {
            _oCacheMan.CacheRepair( null, true, true );
        }

        public void OnFormClear() {
            _oCacheMan.CacheRepair( null, true, true );
        }

        public void OnFormLoad() {
            _oCacheMan.CacheRepair( null, true, true );
        }
    } // End Class
    
}
