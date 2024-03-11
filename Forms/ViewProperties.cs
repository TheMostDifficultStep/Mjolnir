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
        protected DocProperties                   Document   { get; }
        protected CacheMultiFixed                 FixedCache { get; set; }
        protected List<DocProperties.PropertyRow> TabOrder   { get; } = new List<DocProperties.PropertyRow>();

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
            CacheManSite {
            WindowStandardProperties Host2 { get; }
            public CacheManSiteSubSet(WindowStandardProperties oHost) : base(oHost) {
                Host2 = oHost;
            }

            public override Row this[int iIndex] {
                get { 
                    if( iIndex >= Host2.TabOrder.Count )
                        return null;
                    if( iIndex < 0 ) 
                        return null;

                    return Host2.TabOrder[iIndex]; }
            }
        }

        protected override CacheMultiColumn CreateCacheMan() {
            uint uiStdText  = _oStdUI.FontCache( _oStdUI.FaceCache( @"C:\windows\fonts\consola.ttf" ), 12, GetDPI() );
            FixedCache = new CacheMultiFixed( new CacheManSiteSubSet( this ), 
                                              _oStdUI.FontRendererAt( uiStdText ),
                                              _rgColumns ); 
            return FixedCache;
        }

        protected DocProperties.PropertyRow AddMirrorProperty( int iIndex ) {
            Row oOrigRow = Document[iIndex];

            DocProperties.PropertyRow oMirrorRow = new (oOrigRow);
            oMirrorRow.At = TabOrder.Count;

            TabOrder.Add( oMirrorRow );

            return oMirrorRow;
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

            Row         oRow   = AddMirrorProperty( iIndex );
            FTCacheWrap oLabel = new FTCacheWrap( oRow[0] );

            oLabel.BgColor = _oStdUI.ColorsStandardAt( StdUIColors.BGReadOnly );

            CacheRow    oCache = new CacheRow2( oRow );
            oCache.CacheList.Add( oLabel );
            oCache.CacheList.Add( new CacheControl( oWinValue ) );

            FixedCache.Add( oCache );
        }

        public void PropertyInitRow( int iIndex ) {
            Row         oRow   = AddMirrorProperty( iIndex );
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
