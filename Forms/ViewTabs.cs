﻿using System;
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
    /// We don't inherit from FormsWindow b/c there is no need for the 
    /// tab text to be editable.
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
            Layout = new LayoutFlowSquare_Fixed( TabSize );
            Layout.Spacing = 5;
            Layout.Padding.SetRect( 5, 5, 5, 5 );
        }

        public virtual Size TabSize { get { return new Size( 200, 44 ); } }

        public virtual bool InitNew() {
            try {
                Document.ListenerAdd( this );

                IPgMainWindow.PgDisplayInfo oInfo = new IPgMainWindow.PgDisplayInfo();
                if( _oSiteView.Host.TopWindow is IPgMainWindow oMainWin ) {
                    oInfo = oMainWin.MainDisplayInfo;
                }

                _uStdFont = _oStdUI.FontCache(_oStdUI.FaceCache(@"C:\windows\fonts\consola.ttf"), 10, oInfo.pntDpi );

			    foreach( Line oLine in Document ) {
                    if( AcceptItem( oLine ) ) {
				        Layout.Add( CreateTab( oLine ) );
                    }
			    }

                OnEvent( BUFFEREVENTS.MULTILINE );

                return true;
            } catch( ArgumentOutOfRangeException ) {
                return false;
            }
        }

        /// <summary>
        /// A little filter function to see if we really want to
        /// add the item into our display. Override to change behavior.
        /// </summary>
        /// <param name="oLine"></param>
        /// <returns></returns>
        protected virtual bool AcceptItem( Line oLine ) {
            return true;
        }

        protected override void Dispose( bool disposing ) {
            if( disposing ) {
                Document.ListenerRemove( this );
            }

            base.Dispose( disposing );
        }

        //protected override void OnGotFocus(EventArgs e) {
        //    base.OnGotFocus( e );

        //    Invalidate();
        //}

        //protected override void OnLostFocus(EventArgs e) {
        //    base.OnLostFocus(e);

        //}
        
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

        /// <summary>
        /// So we need some way to associate the view's tab with some thing
        /// it is referencing. In the normal case, a line in the text editor.
        /// Normally, I would have a site that I can put any ancillary info 
        /// like that. Here I kind of took a shortcut and just stuck the ID
        /// on the LayoutStack object, since layouts point to layouts
        /// recursively. 
        /// </summary>
        /// <param name="oViewLine"></param>
        /// <returns></returns>
        protected virtual LayoutRect CreateTab( Line oViewLine ) {
			LayoutIcon       oTabIcon = new( TabIcon( oViewLine ), LayoutRect.CSS.Flex );

			LayoutSingleLine oTabText = new LayoutSingleLine( new FTCacheWrap( oViewLine ), 
                                                              LayoutRect.CSS.None ) 
                                            { BgColor = SKColors.Transparent };
			_rgTextCache.Add(oTabText);

            LayoutPattern oTabStatus = new( LayoutRect.CSS.Pixels, 5, oViewLine, TabStatus );

            // Round up all the layouts into our tab object here.
			LayoutStackHorizontal oTab = new () { Spacing = 5, BackgroundColor = TabBackground, Extra = oViewLine };
				
            oTab.Add( oTabStatus ); // Bar to the left.
			oTab.Add( oTabIcon );   // Icon for the tab.
			oTab.Add( oTabText );   // Text for the tab.

            return oTab;
        }

        protected virtual void OnPaintBG(  SKCanvas skCanvas, SKPaint skPaint ) {
            skPaint.Color = _oStdUI.ColorsStandardAt( StdUIColors.BGReadOnly );

            skCanvas.DrawRect( 0, 0, Width, Height, skPaint );
        }

        protected override void OnPaintSurface(SKPaintSurfaceEventArgs e) {
            base.OnPaintSurface(e);

            SKCanvas skCanvas = e.Surface.Canvas;

            using SKPaint skPaint = new SKPaint();

            OnPaintBG( skCanvas, skPaint );

            // BUG: Unfortunately this also draws the text with the test
            // red color. Make sure it's clipped and we'll redraw on
            // next pass for now.
            // Note: The BG for the tab will be whatever the master layout
            //       object does before rendering it's children.
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
            if( AcceptItem( oLine ) ) {
                Layout.Add( CreateTab( oLine ) );

                OnSizeChanged( new EventArgs() );
            }
        }

        public void OnLineDelete(Line oLine) {
            // find the tab and remove it.
            for( int i=0; i<Layout.Count; ++i ) {
                LayoutRect oChild = Layout.Item(i);
                if( oChild is LayoutStack oTab ) {
                    if( oTab.Extra == oLine ) {
                        Layout.RemoveAt( i );
                    }
                }
            }
            // Find the text cache and remove it.
            for( int i=0; i<_rgTextCache.Count; ++i ) {
                if( _rgTextCache[i].Line == oLine ) {
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
                    foreach( LayoutSingleLine oLayout in _rgTextCache ) {
                        oLayout.Cache.Measure( _oStdUI.FontRendererAt( _uStdFont ) );
                        oLayout.OnChangeFormatting();
                        oLayout.Cache.OnChangeSize( oLayout.Width );
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
                            OnTabLeftClicked( oTab.Extra );
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

    public abstract class ButtonBar : TabControl {
        public ButtonBar(IPgViewSite oSiteView, BaseEditor oDoc ) : base( oSiteView, oDoc ) {
        }

        public override Size TabSize => new Size( 55, 40 );

        /// <summary>
        /// Unfortunately, we're kind of locked into the LayoutStack because it has the ID
        /// associated with it. The list implementation is too transparent to the use
        /// of this object right now.
        /// </summary>
        /// <param name="oLine"></param>
        /// <returns></returns>
        protected override LayoutRect CreateTab( Line oLine ) {
			LayoutIcon            oTabIcon   = new( TabIcon( oLine ), LayoutRect.CSS.Flex );
            LayoutPattern         oTabStatus = new( LayoutRect.CSS.Pixels, 5, oLine, TabStatus );
			LayoutStackHorizontal oTab       = new () { Spacing = 5, BackgroundColor = TabBackground, Extra = oLine };
				
            oTab.Add( oTabStatus );
			oTab.Add( oTabIcon );

            return oTab;
        }

        /// <summary>
        /// Make the background the title box colors. Might move this to the tab control too.
        /// </summary>
        /// <param name="skCanvas"></param>
        /// <param name="skPaint"></param>
        protected override void OnPaintBG(  SKCanvas skCanvas, SKPaint skPaint ) {
            skPaint.Color = _oStdUI.ColorsStandardAt( Focused ? StdUIColors.TitleBoxFocus : StdUIColors.TitleBoxBlur );

            skCanvas.DrawRect( 0, 0, Width, Height, skPaint );
        }

    }

}
