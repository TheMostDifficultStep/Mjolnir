using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

using SkiaSharp;
using SkiaSharp.Views.Desktop;

using Play.Rectangles;
using Play.Edit;
using Play.Interfaces.Embedding;

namespace Play.Forms {
    /// <summary>
    /// Experimental class for my tabs.
    /// </summary>
    /// <seealso cref="LayoutFlowSquare"/>
    public class LayoutFlowSquare_Fixed : LayoutFlowSquare_LayoutRect {
		public LayoutFlowSquare_Fixed( Size szSize ) : base( szSize ) {
			ItemSize = szSize;
		}

        /// <summary>
        /// Override this so we just use the given object dimesions.
        /// </summary>
        public override void ItemSizeCalculate() {
        }

	} // End class

    /// <summary>
    /// Creates a flow layout of tabs from the document provided.
    /// </summary>
    /// <remarks>
    /// We don't inherit from FormsWindow b/c there is no need for the tab text to be editable.
    /// </remarks>
    public abstract class TabControl : 
		SKControl,
		IPgLoad,
        ILineEvents,
        IDisposable
	{
        protected readonly IPgViewSite    _oSiteView;
		protected readonly IPgStandardUI2 _oStdUI;
        protected          uint           _uStdFont;

        protected          LayoutStack    HoverTab { get; set; }

        protected BaseEditor Document { get; }
        
        protected readonly List<LayoutSingleLine> _rgTextCache = new List<LayoutSingleLine>();

        public new LayoutFlowSquare_Fixed Layout { get; }

        public TabControl(IPgViewSite oSiteView, BaseEditor oDoc ) 
        {
            _oSiteView = oSiteView ?? throw new ArgumentNullException( nameof( oSiteView ) );
 			_oStdUI    = oSiteView.Host.Services as IPgStandardUI2 ?? throw new ArgumentException( "Parent view must provide IPgStandardUI service" );
            Document   = oDoc ?? throw new ArgumentNullException( nameof( oDoc ) );

            // Would be nice if height was a function of the text size. Someday!
            Layout = new LayoutFlowSquare_Fixed( new Size( 200, 44 ) );
            Layout.Spacing = 5;
        }

        public virtual bool InitNew() {
            try {
                Document.ListenerAdd( this );

                IPgMainWindow.PgDisplayInfo oInfo = new IPgMainWindow.PgDisplayInfo();
                if( _oSiteView.Host.TopWindow is IPgMainWindow oMainWin ) {
                    oInfo = oMainWin.MainDisplayInfo;
                }

                _uStdFont = _oStdUI.FontCache(_oStdUI.FaceCache(@"C:\windows\fonts\consola.ttf"), 10, oInfo.pntDpi );

			    foreach( Line oLine in Document ) {
				    Layout.Add( CreateTab( oLine ) );
			    }

                OnEvent( BUFFEREVENTS.MULTILINE );

                return true;
            } catch( ArgumentOutOfRangeException ) {
                return false;
            }
        }

        protected override void Dispose( bool disposing ) {
            if( disposing ) {
                Document.ListenerRemove( this );
            }

            base.Dispose( disposing );
        }

        /// <summary>
        /// This gets called whenever the tab needs to be drawn.
        /// </summary>
        /// <param name="iID">Id of the tab to return the requested info.</param>
        /// <returns>Focus status</returns>
        public virtual SKColor TabStatus( object oID ) {
            return SKColors.White;
        }

        /// <summary>
        /// This gets called whenever the tab needs to be drawn.
        /// </summary>
        /// <param name="iID">Id of the tab to return the requested info.</param>
        /// <returns>Focus status</returns>
        public virtual SKColor TabBackground( object oID ) {
            return _oStdUI.ColorsStandardAt( StdUIColors.BGReadOnly );
        }

        /// <summary>
        /// This gets called when a Tab is being created.
        /// </summary>
        /// <param name="iID"></param>
        /// <returns>Return a bitmap</returns>
        public abstract SKBitmap TabIcon( object oID );

        protected virtual ParentRect CreateTab( Line oViewLine ) {
			LayoutIcon       oTabIcon = new( TabIcon( oViewLine ), LayoutRect.CSS.Flex );

			LayoutSingleLine oTabText = new LayoutSingleLine( new FTCacheWrap( oViewLine ), 
                                                              LayoutRect.CSS.None ) 
                                            { BgColor = SKColors.Transparent };
			_rgTextCache.Add(oTabText);

            LayoutPattern oTabStatus = new( LayoutRect.CSS.Pixels, 5, oViewLine, TabStatus );

            // Round up all the layouts into our tab object here.
			LayoutStackHorizontal oTab = new () { Spacing = 5, BackgroundColor = TabBackground, ID = oViewLine };
				
            oTab.Add( oTabStatus );
			oTab.Add( oTabIcon );
			oTab.Add( oTabText );

            return oTab;
        }

        protected override void OnPaintSurface(SKPaintSurfaceEventArgs e) {
            base.OnPaintSurface(e);

            SKCanvas skCanvas = e.Surface.Canvas;

            using SKPaint skPaint = new SKPaint() { Color = _oStdUI.ColorsStandardAt( StdUIColors.BGReadOnly ) };

            skCanvas.DrawRect( 0, 0, Width, Height, skPaint );

            // BUG: Unfortunately this also draws the text with the test
            // red color. Make sure it's clipped and we'll redraw on
            // next pass for now.
			foreach( LayoutRect oTab in Layout ) {
                SKRect skClip = new SKRect( oTab.Left,  oTab.Top, 
                                            oTab.Right, oTab.Bottom );

                skCanvas.Save();
                skCanvas.ClipRect( skClip, SKClipOperation.Intersect );
				oTab    .Paint( skCanvas );
                skCanvas.Restore();
			}

            // This draws with the stdui. Need to block the above text some how.
            foreach( LayoutSingleLine oCache in _rgTextCache ) {
                SKRect skClip = new SKRect( oCache.Left,  oCache.Top,
                                            oCache.Right, oCache.Top + Layout.ItemSize.Height);

                skCanvas.Save();
                skCanvas.ClipRect(skClip, SKClipOperation.Intersect);
                oCache  .Paint(skCanvas, _oStdUI, this.Focused);
                skCanvas.Restore();
            }
        }

        public override Size GetPreferredSize( Size oSize ) {
            uint uiTrack = Layout.TrackDesired( TRACK.VERT, oSize.Width );
            return new Size( oSize.Width, (int)uiTrack );
        }

        protected override void OnSizeChanged(EventArgs e) {
            base.OnSizeChanged(e);

			Layout.SetRect( 0, 0, Width, Height );
			Layout.LayoutChildren();

            Invalidate();
        }

        public void OnLineNew(Line oLine) {
            Layout.Add( CreateTab( oLine ) );

            OnSizeChanged( new EventArgs() );
        }

        /// <summary>
        /// Need to check if the Line.At is constant or not. If not
        /// we'll need to update our ID's on the sub elements of
        /// the tab.
        /// </summary>
        public void OnLineDelete(Line oLine) {
            // find the tab and remove it.
            for( int i=0; i<Layout.Count; ++i ) {
                LayoutRect oChild = Layout.Item(i);
                if( oChild is LayoutStackHorizontal oTab ) {
                    if( oTab.ID == oLine ) {
                        Layout.RemoveAt( i );
                    }
                }
            }
            // Find the text cache and remove it.
            for( int i=0; i<_rgTextCache.Count; ++i ) {
                if( _rgTextCache[i].Cache.Line == oLine ) {
                    _rgTextCache.RemoveAt( i );
                }
            }
            OnSizeChanged( new EventArgs() );
        }

        public void OnLineUpdated(Line oLine, int iOffset, int iOldLen, int iNewLen) {
        }

        public void OnEvent(BUFFEREVENTS eEvent) {
            switch( eEvent ) {
                case BUFFEREVENTS.FORMATTED:
                case BUFFEREVENTS.SINGLELINE:
                case BUFFEREVENTS.MULTILINE:
                    foreach( LayoutSingleLine oCache in _rgTextCache ) {
                        oCache.Cache.Update( _oStdUI.FontRendererAt( _uStdFont ) );
                        oCache.OnChangeFormatting();
                        oCache.Cache.OnChangeSize( oCache.Width );
                    }
                    Invalidate();
                    break;
            }
        }

        protected override void OnMouseMove(MouseEventArgs e) {
            base.OnMouseMove(e);
            
            LayoutStack oHover = null;

            foreach( LayoutRect oRect in Layout ) {
                if( oRect.IsInside( e.X, e.Y ) ) {
                    if( oRect is LayoutStack oTab ) {
                        oHover = oTab;
                        break;
                    }
                }
            }

            if( oHover != HoverTab ) {
                Invalidate();
            }
            HoverTab = oHover;
        }

        protected override void OnMouseDown(MouseEventArgs e) {
            base.OnMouseDown(e);
            
            LayoutStack oHover = null;

            if( e.Button == MouseButtons.Left ) {
                foreach( LayoutRect oRect in Layout ) {
                    if( oRect.IsInside( e.X, e.Y ) ) {
                        if( oRect is LayoutStack oTab ) {
                            oHover = oTab;
                            OnTabLeftClicked( oTab.ID );
                            Invalidate(); // Focus might change, need a re-paint.
                            break;
                        }
                    }
                }
            }

            if( oHover != HoverTab ) {
                Invalidate();
            }
            HoverTab = oHover;
        }

        protected override void OnMouseLeave(EventArgs e) {
            base.OnMouseLeave(e);
            HoverTab = null;
            Invalidate();
        }

        protected abstract void OnTabLeftClicked( object ID );
    }

}
