using System;
using System.Collections.Generic;
using System.Drawing;

using SkiaSharp;
using SkiaSharp.Views.Desktop;

using Play.Interfaces.Embedding;
using Play.Rectangles;

namespace Play.Edit {
	public class LayoutText : LayoutRect {
		public CacheWrapped Cache { get; }

		public LayoutText( CacheWrapped oCache, LayoutRect.CSS eUnits, uint uiTrack ) :
			base( eUnits, uiTrack, 1 ) 
		{
			Cache = oCache ?? throw new ArgumentNullException();
		}

		public override uint TrackDesired(AXIS eParentAxis, int uiRail) {
			if( eParentAxis == AXIS.VERT ) {
				if (Cache is CacheWrapped oWrap) {
					List<int> rgSides = new List<int>(2) { 0, (int)uiRail };
					oWrap.WrapSegmentsCreate( this.Width );
				}
			}

			int iValue = 0;
            
            if( eParentAxis == AXIS.HORIZ )
                iValue = Cache.Width;
            else
                iValue = Cache.Height;

			if( iValue < 0 )
				return 0;

			return (uint)iValue;
		}

        public void Paint( IntPtr hDC, IntPtr hScriptCache, int iColor ) {
            if( !Hidden ) { 
                Cache.Render( hDC, hScriptCache, new PointF( Left, Top ), iColor, 
                              new RECT( Left, Top, Right, Bottom  ) );
            }
        }

		public override void Invalidate() {
			Cache.Invalidate();
		}
	}

	public class LayoutText2 : LayoutRect {
		public FTCacheLine Cache { get; }
		public int         ColumnIndex { get; }

		public LayoutText2( FTCacheLine oCache, LayoutRect.CSS eUnits, uint uiTrack, int iCol ) :
			base( eUnits, uiTrack, 1 ) 
		{
			Cache = oCache ?? throw new ArgumentNullException();
			ColumnIndex = iCol;
		}

		/// <summary>
		/// I need to rethink the implementation of this.
		/// </summary>
		/// <param name="eParentAxis"></param>
		/// <param name="uiRail"></param>
		public override uint TrackDesired(AXIS eParentAxis, int uiRail) {
			int iValue = 0;
            
            if( eParentAxis == AXIS.HORIZ ) {
                iValue = Cache.UnwrappedWidth; 
			} else {
 				Cache.OnChangeSize( uiRail );
				iValue = Cache.Height;
			}

			if( iValue < 0 )
				return 0;

			return (uint)iValue;
		}

        public void Paint( SKCanvas skCanvas, IPgStandardUI2 oStdUI ) {
            if( !Hidden ) { 
				Cache.Render( skCanvas, oStdUI, new PointF( Left, Top ) );
            }
        }

		public override void Invalidate() {
			Cache.Invalidate();
		}
	}

	public class PropWin : SKControl,
		IPgLoad,
		IPgPropertyChanges
	{
		readonly IPgViewSite   _oSiteView;
		readonly PropDoc       _oDocument;
		readonly SmartTable    _oTable = new SmartTable( 20, LayoutRect.CSS.None ); // Add some slop. Not measuring well...
		readonly IPgStandardUI2 _oStdUI;
		readonly int            _iActiveColumn = 2; // Only one column of our text ever changes.

		protected List<LayoutText2> _rgCache       = new List<LayoutText2>();
		protected bool              _fCacheInvalid = true;
		protected uint				StdFontID { get; set; }

		public PropWin( IPgViewSite oSiteView, PropDoc oDocument ) {
			_oSiteView = oSiteView ?? throw new ArgumentNullException( "Site must not be null" );
			_oDocument = oDocument ?? throw new ArgumentNullException( "Document must not be null" );
 			_oStdUI    = oSiteView.Host.Services as IPgStandardUI2 ?? throw new ArgumentException( "Parent view must provide IPgStandardUI service" );
		}

		protected override void Dispose( bool fDisposing ) 
		{
			if( fDisposing ) {
				_oDocument.ListenerRemove( this );
			}
            base.Dispose(fDisposing);
		}

		public bool InitNew() {
            SKSize sResolution = new SKSize( 96, 96 ); // 106, 106
            using( Graphics oGraphics = this.CreateGraphics() ) {
                sResolution.Width  = oGraphics.DpiX;
                sResolution.Height = oGraphics.DpiY;
            }
            StdFontID = _oStdUI.FontCache( _oStdUI.FaceCache( @"C:\windows\fonts\consola.ttf" ), 12, sResolution );

			_oTable.Add( new LayoutRect( LayoutRect.CSS.Flex, 0, 40 ) );
			_oTable.Add( new LayoutRect( LayoutRect.CSS.Flex, 0, 10 ) );
			_oTable.Add( new LayoutRect( LayoutRect.CSS.None ) );

			LoadTable();

			_oDocument.ListenerAdd( this );

			return true;
		}

		protected void LoadTable() {
			// One cash instance that we reuse, since line measurements for it always the same.
			FTCacheWrap oDash = new FTCacheWrap( new TextLine( 0, "-" ) );

			foreach( PropertyItem oProperty in _oDocument ) {
				List<LayoutRect> rgRow = new List<LayoutRect>(2);

				LayoutText2 oName   = new LayoutText2( new FTCacheWrap( oProperty.Name  ), LayoutRect.CSS.Flex, 1, 0 );
				LayoutText2 oValue  = new LayoutText2( new FTCacheWrap( oProperty.Value ), LayoutRect.CSS.Flex, 1, 2 );
				LayoutText2 oSpacer = new LayoutText2( oDash,                              LayoutRect.CSS.Flex, 1, 1 );

                // Load the text cache up so we can measure the text
				_rgCache.Add( oName );
				_rgCache.Add( oSpacer );
				_rgCache.Add( oValue );

                // Load up the row with our display elements.
				rgRow.Add( oName );
				rgRow.Add( oSpacer );
				rgRow.Add( oValue );

				_oTable.AddRow( rgRow );
			}
		}

		protected void LogError( string strMessage, string strDetails ) {
			_oSiteView.LogError( strMessage, strDetails );
		}

		public void OnPropertyChanged(int iRow) {
			try {
                if( _oTable.RowItem( iRow ).Item( _iActiveColumn ) is LayoutText2 oCell ) {
					oCell.Cache.Invalidate();
				}
				_fCacheInvalid = true;
			} catch( Exception oEx ) {
				Type[] rgErrors = { typeof( ArgumentOutOfRangeException ), // Index out of range.
									typeof( InvalidCastException ),        // Unexpected table row contents.
									typeof( NullReferenceException ) };
				if( rgErrors.IsUnhandled( oEx ) )
					throw;
				LogError( "Property Changed", "Problem Invalidating given property row!" );
            }
        }

		public void OnPropertyDone( bool fChangedAll ) {
			try {
				foreach( LayoutStack oRow in _oTable.Rows ) {
					if( oRow.Item(_iActiveColumn) is LayoutText2 oCell ) {
						oCell.Cache.Invalidate();
					}
				}
				_fCacheInvalid = true;
			} catch( Exception oEx ) {
				Type[] rgErrors = { typeof( ArgumentOutOfRangeException ), // Index out of range.
									typeof( NullReferenceException ) };
				if( rgErrors.IsUnhandled( oEx ) )
					throw;
				LogError( "Property Cache", "Index out of range." );
            }
            Invalidate();
		}

		public void OnPropertiesClear() {
			_oTable .Clear();
			_rgCache.Clear();

			Invalidate();
		}

		public void OnPropertiesLoad() {
			LoadTable();
			_fCacheInvalid = true;

			Invalidate();
		}

		protected override void OnSizeChanged(EventArgs e) {
			base.OnSizeChanged(e);

			_oTable.SetRect( 0, 0, Width, Height );
			_oTable.LayoutChildren();

			Invalidate();
		}

		protected void CellsUpdateAll() {
			using( IPgFontRender oFR = _oStdUI.FontRendererAt( StdFontID ) ) {
				foreach( LayoutText2 oCell in _rgCache ) {
					// BUG: Got to figure out where I can init the static items once.
					if( oCell.Cache.IsInvalid ) {
						// TODO: Parse the cell.
						oCell.Cache.Update( oFR );
						oCell.Cache.OnChangeSize( int.MaxValue );
					}
				}
			}
			_fCacheInvalid = false;
		}

        protected override void OnPaintSurface( SKPaintSurfaceEventArgs e ) {
            base.OnPaintSurface(e);

			try {
				if( _fCacheInvalid ) {
					CellsUpdateAll();
					_oTable.SetRect( 0, 0, Width, Height );
					_oTable.LayoutChildren();
				}

				using( SKPaint skPaint = new SKPaint() ) {
					skPaint.Color = _oStdUI.ColorsStandardAt( StdUIColors.BGReadOnly );

					e.Surface.Canvas.DrawRect( new SKRect( 0, 0, this.Width, this.Height ), skPaint );

					//List<SKColor> rgColors = new List<SKColor>(3) { SKColors.Red, SKColors.White, SKColors.Blue };
				
					foreach( LayoutText2 oCell in _rgCache ) {
						//skPaint.Color = rgColors[oCell.ColumnIndex];
						//e.Surface.Canvas.DrawRect( oCell.SKRect, skPaint );
						oCell.Cache.Render( e.Surface.Canvas, _oStdUI, new PointF( oCell.Left, oCell.Top ) );
					}
				}
			} catch( Exception oEx ) {
				Type[] rgErrors = { typeof( NullReferenceException ),
									typeof( ArgumentOutOfRangeException ),
									typeof( ArgumentNullException ) };
				if( rgErrors.IsUnhandled( oEx ) )
					throw;
			}
        }

		protected override void OnGotFocus(EventArgs e) {
			base.OnGotFocus(e);

			_oSiteView.EventChain.NotifyFocused( true );

			Invalidate();
		}

		protected override void OnLostFocus(EventArgs e) {
			base.OnLostFocus(e);

			_oSiteView.EventChain.NotifyFocused( false );

			Invalidate();
		}
	}
}
