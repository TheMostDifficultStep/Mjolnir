using System;
using System.Collections.Generic;
using System.Windows.Forms;

using SkiaSharp;

using Play.Rectangles;
using Play.Interfaces.Embedding;
using Play.Edit;
using Play.Parse;

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
        IPgLoad,
        IPgFormEvents
     {
        protected DocProperties   Document { get; }
        protected CacheMultiFixed FixedCache { get; set; }

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

        protected override CacheMultiColumn CreateCacheMan() {
            uint uiStdText  = _oStdUI.FontCache( _oStdUI.FaceCache( @"C:\windows\fonts\consola.ttf" ), 12, GetDPI() );
            FixedCache = new CacheMultiFixed( new CacheManSite( this ), 
                                         _oStdUI.FontRendererAt( uiStdText ),
                                         _rgColumns ); 
            return FixedCache;
        }

        protected override bool Initialize() {
            if( !base.Initialize() )
                return false;

            _rgLayout.Add( new LayoutRect( LayoutRect.CSS.Percent, 30, 1L ) ); // Name
            _rgLayout.Add( new LayoutRect( LayoutRect.CSS.None,    70, 1L ) ); // Value;

            _rgColumns.Add( _rgLayout.Item( 1 ) );
            _rgColumns.Add( _rgLayout.Item( 2 ) );

            InitRows();

            // This certainly does not belong on the base form, but here
            // it is a little more reasonable.
            HyperLinks.Add( "callsign", OnCallSign );

            return true;
        }

        public void PropertyInitRow( int iIndex, Control oWinValue ) {
            // If the value is a multi-line value make an editor. And ignore the value line (if there is one).
            if( oWinValue is IPgLoad oWinLoad ) {
                oWinLoad.InitNew();
            }
            oWinValue.Parent = this;

            Row         oRow         = _oDocList[iIndex];
            FTCacheWrap oLabel       = new FTCacheWrap( oRow[0] );

            oLabel.BgColor = _oStdUI.ColorsStandardAt( StdUIColors.BGReadOnly );

            CacheRow    oNewCacheRow = new CacheRow2( oRow );
            oNewCacheRow.CacheList.Add( oLabel );
            oNewCacheRow.CacheList.Add( new CacheControl( oWinValue ) );

            FixedCache.Add( oNewCacheRow );
        }

        public void PropertyInitRow( int iIndex ) {
            Row         oRow         = _oDocList[iIndex];
            CacheRow    oNewCacheRow = new CacheRow2( oRow );
            FTCacheWrap oLabel       = new FTCacheWrap( oRow[0] );
            FTCacheWrap oValue       = new FTCacheWrap( oRow[1] );

            if( Document.ValueBgColor.TryGetValue(iIndex, out SKColor skBgColorOverride) ) {
                oValue.BgColor = skBgColorOverride;
            }
            oLabel.BgColor = _oStdUI.ColorsStandardAt(StdUIColors.BGReadOnly);

            oNewCacheRow.CacheList.Add( oLabel );
            oNewCacheRow.CacheList.Add( oValue );

            FixedCache.Add( oNewCacheRow );
        }

        /// <summary>
        /// this is the default InitRows() function that set's everything. However InitNew() might
        /// get overriden and call the InitRows( int[] ) on a subset. 
        /// </summary>
        public virtual void InitRows() {
            try {
                List<int> rgTabOrder = new List<int>();
                for( int i = 0; i< Document.PropertyCount; ++i ) {
                    PropertyInitRow( i );
                    rgTabOrder.Add( i );
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

        public void OnPropertyEvent( BUFFEREVENTS eEvent ) {
            //OnDocumentEvent( BUFFEREVENTS.MULTILINE );
        }

        public void OnFormUpdate(IEnumerable<Line> rgUpdates) {
            Invalidate();
        }

        public void OnFormFormat(IEnumerable<Line> rgUpdates) {
            _oCacheMan.CacheReColor();
        }

        public void OnFormClear() {
            _oCacheMan.CacheRepair( null, true, true );
        }

        public void OnFormLoad() {
            _oCacheMan.CacheRepair( null, true, true );
        }
    } // End Class
    
}
