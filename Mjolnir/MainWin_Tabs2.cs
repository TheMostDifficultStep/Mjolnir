using System;
using System.Drawing;
using System.Xml;

using SkiaSharp;

using Play.Interfaces.Embedding;
using Play.Edit;
using Play.Forms;

namespace Mjolnir {
    /// <summary>
    /// MainWindow usage TabControl. Shows tabs for the views, 
    /// </summary>
    public class MainWin_Tabs : 
		TabControl,
		IPgParent
	{
        public IPgParent Parentage => _oSiteView.Host;
        public IPgParent Services  => Parentage.Services;

        MainWin _oHost;

        public MainWin_Tabs(IPgViewSite oSiteView, BaseEditor oDoc) : base(oSiteView, oDoc) {
            _oHost = (MainWin)oSiteView.Host;
        }

        public override Size GetPreferredSize( Size oSize ) {
            if( Layout.Count < 2 ) {
                return new Size( oSize.Width, 0 );
            }
            return base.GetPreferredSize( oSize );
        }

        /// <summary>
        /// This gets called whenever the tab needs to be drawn. Usually
        /// used for the status bar on the left of the icon.
        /// </summary>
        /// <param name="iID">Id of the tab to return the requested info.</param>
        /// <returns>Focus status</returns>
        public override SKColor TabStatus( object oID ) {
            if( oID is ViewSlot oSlot ) {
                if( oSlot.Focused )
                    return _oStdUI.ColorsStandardAt( StdUIColors.BGSelectedFocus );

                // Always draw the status if selected. (center view focused or not)
                if( _oHost.ViewSiteSelected == oSlot ) 
                    return SKColors.Gray;

                return TabBackground( oID );
            }
            
            return SKColors.White;
        }

        /// <summary>
        /// This gets called whenever the tab needs to be drawn. If you want
        /// the bg status to be anything more interesting you need to override
        /// the primary layout class of the tab (LayoutStack) for what it
        /// paints before painting the children.
        /// </summary>
        /// <param name="iID">Id of the tab to return the requested info.</param>
        /// <returns>Focus status</returns>
        public override SKColor TabBackground( object oID ) {
            SKColor clrBG = _oStdUI.ColorsStandardAt( StdUIColors.BGReadOnly );

            if( oID is ViewSlot oLine ) {
                if( oLine == HoverTab?.Extra ) 
                    return SKColors.LightYellow;

                if( oLine.IsPlaying )
                    return oLine.BusyLight;

                if( oLine.Focused )
                    return SKColors.LightCyan;
                
                if( _oHost.ViewSiteSelected == oLine )
                    return SKColors.LightGray;
            }
            return clrBG;
        }

        /// <summary>
        /// Ack! The bitmap on the view, is a windows bitmap and not an SKBitmap.
        /// I'll fix that later.
        /// </summary>
        /// <returns>Return a bitmap</returns>
        public override SKBitmap TabIcon( object oID ) {
            if( oID is ViewSlot oSlot ) {
                if( oSlot.Icon != null )
                    return oSlot.Icon;
            }

            SKBitmap skIcon = new( 30, 30, SKColorType.Rgb888x, SKAlphaType.Opaque );

            using SKPaint  oPaint  = new () { Color = SKColors.Red };
			using SKCanvas oCanvas = new ( skIcon );

			oCanvas.DrawRect( 0, 0, skIcon.Width, skIcon.Height, oPaint );

            return skIcon;
        }

        protected override void OnTabLeftClicked( object oID ) {
            if( oID is ViewSlot oSlot ) {
                _oHost.ViewSelect( oSlot, fFocus:true );
            }
        }
    }

}
