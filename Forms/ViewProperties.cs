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
    public class TabOrder {
        public int At { get; }
        public Row Data { get; }

        public TabOrder( int iAt, Row oData ) {
            At   = iAt;
            Data = oData;
        }
    }

    /// <summary>
    /// This manager expects all the rows to be precached. Great for the
    /// property pages that have windows which would have to be created
    /// and destroyed on the fly, when usually nothing ever falls out of
    /// the cache.
    /// </summary>
    public class CacheMultiFixed : CacheMultiColumn {
        List<CacheRow> _rgFixedCache = new ();
        public CacheMultiFixed(ICacheManSite oSite, IPgFontRender oFont, List<SmartRect> rgColumns) : 
            base(oSite, oFont, rgColumns) 
        {
        }

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

        /// <summary>
        /// If we had the base.CreateCacheRow call into the host to get
        /// the row, we wouldn't need this call at all! O.o
        /// </summary>
        /// <param name="oCacheRow"></param>
        public void Add( CacheRow oCacheRow ) {
            if( _rgFixedCache.Count <= 0 )
                _oCaretRow = _oSiteList[ oCacheRow.At ];

            _rgFixedCache.Add( oCacheRow );
        }

        protected override Row GetTabOrderAtScroll() {
            int iIndex = (int)(_oSite.GetScrollProgress * _rgFixedCache.Count );

            return _oSite.TabStop( 0 );
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
                if( _rgFixedCache[i].Row == oBottom ) {
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
        protected CacheMultiFixed FixedCache { get; set; }
        protected List<TabOrder>  TabOrderList { get; set; } = new List<TabOrder>();

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

        /// <remarks>
        /// TODO: Make the FormsWindow base take the DocForms.
        /// </remarks>
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
        /// This solves the problem of displaying a subset of all the properties
        /// on the screen. We generate a TabOrder collection which is a list of
        /// PropertyRows that share the Line objects from the original DocProperties
        /// document. This way the TabOrder[n].At is n for all these elements!!
        /// </summary>
        protected class CacheManSiteSubSet :
            CacheManSite 
        {
            List<TabOrder> TabList { get; }
            public CacheManSiteSubSet(WindowStandardProperties oHost) : base(oHost) 
            {
                TabList = oHost.TabOrderList;
            }

            public override Row TabStop(int iIndex) {
                return TabList[iIndex].Data; 
            }

            /// <summary>
            /// Unfortunately, I can't seem to get around the fact that too many
            /// references to the Row are in the cache manager and so I can't 
            /// make a Tab order with mirrors so that the .At values would be
            /// sequential for the tab order. So we need to search our tab
            /// order elements on every move request. Fortunately it's a user
            /// input and thus we'll always have a timely responce.
            /// </summary>
            /// <param name="oRow"></param>
            /// <param name="iDir"></param>
            /// <returns></returns>
            public override Row TabOrder( Row oRow, int iDir ) {
                try {
                    TabOrder oCurrentTab = null;

                    foreach( TabOrder oTab in TabList ) {
                        if( oTab.Data == oRow ) {
                            oCurrentTab = oTab;
                            break;
                        }
                    }
                    if( oCurrentTab == null ) 
                        return null;

                    int iIndex = oCurrentTab.At + iDir;

                    if( iIndex >= TabList.Count )
                        return null;
                    if( iIndex < 0 ) 
                        return null;

                    return TabList[iIndex].Data;
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

        protected override CacheMultiColumn CreateCacheMan() {
            uint uiStdText  = _oStdUI.FontCache( _oStdUI.FaceCache( @"C:\windows\fonts\consola.ttf" ), 12, GetDPI() );
            FixedCache = new CacheMultiFixed( new CacheManSiteSubSet( this ), 
                                              _oStdUI.FontRendererAt( uiStdText ),
                                              _rgColumns ); 
            return FixedCache;
        }

        protected Row AddTabbedProperty( int iIndex ) {
            Row oOrigRow = Document[iIndex];

            TabOrderList.Add( new TabOrder( TabOrderList.Count, oOrigRow ) );

            return oOrigRow;
        }

        protected override bool Initialize() {
            if( !base.Initialize() )
                return false;

            _rgLayout.Add( new LayoutRect( LayoutRect.CSS.Percent, 30, 1L ) ); // Name
            _rgLayout.Add( new LayoutRect( LayoutRect.CSS.None,    70, 1L ) ); // Value;

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

            Row         oRow   = AddTabbedProperty( iIndex );
            FTCacheWrap oLabel = new FTCacheWrap( oRow[0] );

            oLabel.BgColor = _oStdUI.ColorsStandardAt( StdUIColors.BGReadOnly );

            CacheRow    oCache = new CacheRow2( oRow );
            oCache.CacheList.Add( oLabel );
            oCache.CacheList.Add( new CacheControl( oWinValue ) );

            FixedCache.Add( oCache );
        }

        public void PropertyInitRow( int iIndex ) {
            Row         oRow   = AddTabbedProperty( iIndex );
            CacheRow    oCache = new CacheRow2( oRow );
            FTCacheWrap oLabel = new FTCacheWrap( oRow[0] );
            FTCacheWrap oValue = new FTCacheWrap( oRow[1] );

            if( Document.ValueBgColor.TryGetValue(iIndex, out SKColor skBgColorOverride) ) {
                oValue.BgColor = skBgColorOverride;
            }
            oLabel.BgColor = _oStdUI.ColorsStandardAt(StdUIColors.BGReadOnly);

            oCache.CacheList.Add( oLabel );
            oCache.CacheList.Add( oValue );

            FixedCache.Add( oCache );
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
