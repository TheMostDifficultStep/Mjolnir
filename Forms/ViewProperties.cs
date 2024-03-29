using System;
using System.Collections.Generic;
using System.Windows.Forms;

using SkiaSharp;

using Play.Rectangles;
using Play.Interfaces.Embedding;
using Play.Edit;
using Play.Parse;
using System.Drawing;

namespace Play.Forms {
    /// <summary>
    /// This manager expects all the rows to be precached. Great for the
    /// property pages that have windows which would have to be created
    /// and destroyed on the fly, when usually nothing ever falls out of
    /// the cache.
    /// </summary>
    public class CacheMultiFixed : CacheMultiColumn {
        IReadOnlyList<CacheRow> _rgFixedCache;
        public CacheMultiFixed(
            ICacheManSite           oSite, 
            IReadOnlyList<CacheRow> oCacheAccess,
            IPgFontRender           oFont, 
            List<SmartRect>         rgColumns
        ) : 
            base(oSite, oFont, rgColumns) 
        {
            _rgFixedCache = oCacheAccess ?? throw new ArgumentNullException();
        }

        public IEnumerable<CacheRow> FixedCache => _rgFixedCache;
        protected override CacheRow CreateCacheRow(Row oDocRow) {
            foreach( CacheRow oCacheRow in _rgFixedCache ) { 
                if( oCacheRow.Row == oDocRow ) {
                    RowMeasure( oCacheRow );
                    return oCacheRow;
                }
            }
            _oSite.LogError( "Cache Manager Multi", "Seem to have lost an data row..." );
            return base.CreateCacheRow(oDocRow);
        }

        protected override void FinishUp( CacheRow oBottom, CacheRow oCaret ) {
            if( _rgFixedCache.Count <= 0 ) {
                _oSite.OnRefreshComplete( 1, 1 );
                _oSite.OnCaretPositioned( new SKPointI( -1000,-1000), false );
                return;
            }

            bool fCaretVisible = IsCaretNear( oCaret, out SKPointI pntCaret );
            int  iFixedIndex   = 0;

            for( int i=0; i< _rgFixedCache.Count; ++i ) {
                if( _rgFixedCache[i] == oBottom ) {
                    iFixedIndex = i;
                    break;
                }
            }

            _oSite.OnRefreshComplete( (float)iFixedIndex       / _rgFixedCache.Count, 
                                      (float)_rgOldCache.Count / _rgFixedCache.Count );
            _oSite.OnCaretPositioned( pntCaret,   fCaretVisible );
        }
    }

    /// <summary>
    /// View the DocProperties object. This makes two columns, label on the left
	/// and value on the right. It is capable of adding an editwindow for the values.
	/// We'll turn that into optionally a dropdown in the future.
    /// </summary>
    /// <seealso cref="DocProperties"/>
    public class WindowStandardProperties : 
        WindowMultiColumn,
        IPgParent,
        IPgLoad,
        IPgFormEvents // BUG: Need to work on this interface...
     {
        protected DocProperties   Document   { get; }
        protected List<Row>       TabList    { get; set; } = new ();
        protected List<CacheRow>  FixedRows  { get; set; }

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

        protected class CacheManSiteSubSet :
            CacheManSite 
        {
            IReadOnlyList<CacheRow> CacheAccess { get; } 
            public CacheManSiteSubSet(WindowStandardProperties oHost) : base(oHost) 
            {
                CacheAccess = oHost.FixedRows;
            }

            public override Row TabStop(int iIndex) {
                return CacheAccess[iIndex].Row; 
            }

            public override int TabCount => CacheAccess.Count;

            /// <summary>
            /// Since the Views on the property page must be established once
            /// only. We can take advantage of the fixed cache to supply us with
            /// the TabOrder!!
            /// </summary>
            /// <param name="oRow">Current row we want to navigate from.</param>
            /// <param name="iDir">Direction from that point. +1, -1 typically.</param>
            /// <returns></returns>
            public override Row TabOrder( Row oRow, int iDir ) {
                try {
                    int iIndex = -1;

                    for( int i=0; i<CacheAccess.Count; ++i ) {
                        Row oTab = CacheAccess[i].Row;
                        if( oTab == oRow ) {
                            iIndex = i;
                            break;
                        }
                    }
                    if( iIndex < 0 ) 
                        return null;

                    iIndex += iDir;

                    if( iIndex >= CacheAccess.Count )
                        return null;
                    if( iIndex < 0 ) 
                        return null;

                    return CacheAccess[iIndex].Row;
                } catch( Exception oEx ) {
                    Type[] rgErrors = { typeof( NullReferenceException ),
                                        typeof( ArgumentOutOfRangeException ) };
                    if( rgErrors.IsUnhandled( oEx ) )
                        throw;

                    LogError( "Tab Order", "Problem with tab list" );
                }

                return null;
            }
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
        protected override CacheMultiColumn CreateCacheMan() {
            uint uiStdText  = _oStdUI.FontCache( _oStdUI.FaceCache( @"C:\windows\fonts\consola.ttf" ), 12, GetDPI() );

            FixedRows  = new List<CacheRow>(); // Slightly evil... >_<;;
            CacheMultiFixed oCacheMan = new ( new CacheManSiteSubSet( this ), 
                                              FixedRows as IReadOnlyList<CacheRow>,
                                              _oStdUI.FontRendererAt( uiStdText ),
                                              _rgColumns ); 
            return oCacheMan;
        }

        protected override bool Initialize() {
            if( !base.Initialize() )
                return false;

            _rgLayout .Add( new LayoutRect( LayoutRect.CSS.Percent, 30, 1L ) ); // Name
            _rgLayout .Add( new LayoutRect( LayoutRect.CSS.None,    70, 1L ) ); // Value;

            _rgColumns.Add( _rgLayout.Item( 1 ) );
            _rgColumns.Add( _rgLayout.Item( 2 ) );

            InitRows();

            // Do this so we can return a desired height. O.o;;
            _oCacheMan.CacheRepair( null, true, true );

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
            CacheRow    oCache = new CacheRow2  ( oRow );

            oLabel.BgColor = _oStdUI.ColorsStandardAt( StdUIColors.BGReadOnly );

            oCache.CacheList.Add( oLabel );
            oCache.CacheList.Add( new CacheControl( oWinValue ) { MaxHeight = 800 });

            FixedRows.Add( oCache );
        }

        public void PropertyInitRow( int iIndex ) {
            Row         oRow   = Document[ iIndex ];
            CacheRow    oCache = new CacheRow2  ( oRow );
            FTCacheWrap oLabel = new FTCacheWrap( oRow[0] );
            FTCacheWrap oValue = new FTCacheWrap( oRow[1] );

            if( Document.ValueBgColor.TryGetValue(iIndex, out SKColor skBgColorOverride) ) {
                oValue.BgColor = skBgColorOverride;
            }
            oLabel.BgColor = _oStdUI.ColorsStandardAt(StdUIColors.BGReadOnly);

            oCache.CacheList.Add( oLabel );
            oCache.CacheList.Add( oValue );

            FixedRows.Add( oCache );
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
