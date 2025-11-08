using System.Drawing;
using System.Reflection;

using SkiaSharp;

using Play.Edit;
using Play.Forms;
using Play.ImageViewer;
using Play.Interfaces.Embedding;
using Play.Rectangles;

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

        readonly MainWin      _oHost;
        readonly ImageSoloDoc _oCloserImg;

        protected class TabSlot : IPgViewSite {
            readonly MainWin_Tabs _oHost;
            public TabSlot( MainWin_Tabs oParent ) {
                _oHost = oParent;
            }

            public IPgParent Host => _oHost;

            public IPgViewNotify EventChain => _oHost._oSiteView.EventChain;

            public void LogError(string strMessage, string strDetails, bool fShow = true) {
                _oHost._oSiteView.LogError(strMessage, strDetails, fShow);
            }

            public void Notify(ShellNotify eEvent) {
                _oHost._oSiteView.Notify(eEvent);
            }
        }

        public MainWin_Tabs(IPgViewSite oSiteView, BaseEditor oDoc) : base(oSiteView, oDoc) {
            _oHost      = (MainWin)oSiteView.Host;
            _oCloserImg = new( new TabSlot( this ) );

            _oCloserImg.LoadResource( Assembly.GetExecutingAssembly(), 
                                      "Mjolnir.Content.icons8-close-48.png" );
        }

        public override Size TabSize => new Size( 220, 44 );

        public override Size GetPreferredSize( Size oSize ) {
            if( Layout.Count < 2 ) {
                return new Size( oSize.Width, 0 );
            }
            return base.GetPreferredSize( oSize );
        }

        protected override LayoutRect CreateTab(Line oViewLine) {
            LayoutRect oReturn = base.CreateTab(oViewLine);

            if( oReturn is LayoutStack oTab ) {
                oTab.Add( new LayoutBmpDoc( _oCloserImg ) { Units = LayoutRect.CSS.Flex } );
            }

            return oReturn;
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
