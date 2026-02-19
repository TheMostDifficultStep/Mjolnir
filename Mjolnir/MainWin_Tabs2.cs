using Play.Edit;
using Play.Forms;
using Play.ImageViewer;
using Play.Interfaces.Embedding;
using Play.Rectangles;

using SkiaSharp;

using System;
using System.Drawing;
using System.Reflection;

namespace Mjolnir {
    /// <summary>
    /// Shows tabs for the views, with a close window button that appears
    /// when hovering over a particular tab.
    /// </summary>
    public class TabForMainWin : 
		TabWindow,
		IPgParent
	{
        public IPgParent Parentage => _oSiteView.Host;
        public IPgParent Services  => Parentage.Services;

        readonly MainWin      _oHost;
        readonly ImageSoloDoc _oCloserImg;
        readonly SKImageInfo  _sIconInfo = new SKImageInfo( 30, 30 , SKColorType.Rgb888x, SKAlphaType.Opaque );


        protected class TabSlot : IPgViewSite {
            readonly TabForMainWin _oHost;
            public TabSlot( TabForMainWin oParent ) {
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

        public TabForMainWin(IPgViewSite oSiteView, BaseEditor oDoc) : base(oSiteView, oDoc) {
            _oHost      = (MainWin)oSiteView.Host;
            _oCloserImg = new( new TabSlot( this ) );

            _oCloserImg.LoadResource( Assembly.GetExecutingAssembly(), 
                                      "Mjolnir.Content.icons8-close-window-94-2.png" );
            Parent = _oHost;
        }

        public override Size TabSize => new Size( 220, 44 );

        public override Size GetPreferredSize( Size oSize ) {
            if( Layout.Count < 2 ) {
                return new Size( oSize.Width, 0 );
            }
            return base.GetPreferredSize( oSize );
        }

        protected override LayoutRect CreateTab( Line oViewLine ) {
            LayoutPattern    oTabStat = new( LayoutRect.CSS.Pixels, 5, oViewLine, TabStatus );
			LayoutIcon       oTabIcon = new( TabIcon( (ViewSlot)oViewLine ), LayoutRect.CSS.Flex );
			LayoutSingleLine oTabText = new( new FTCacheWrap( oViewLine ), LayoutRect.CSS.None ) 
                                             { BgColor = SKColors.Transparent };
			_rgTextCache.Add(oTabText);

            LayoutSKBitmap   oTabKill = new( _oCloserImg ) 
                                            { Units = LayoutRect.CSS.Flex, Hidden = true, Border = new( 20, 20) };
            
            // Round up all the layouts into our tab object here.
			LayoutStackHorizontal oTab = new () { Spacing = 5, BackgroundColor = TabBackground, Extra = oViewLine };
				
            oTab.Add( oTabStat ); // Focus indicator Bar.
			oTab.Add( oTabIcon ); // Icon for the tab.
			oTab.Add( oTabText ); // Text for the tab.
            oTab.Add( oTabKill ); // Icon to indicate we want to close the view.

            return oTab;
        }

        /// <summary>
        /// Get the icon for the slot or just generate a red bitmap 
        /// as a standin. I should probably find some interesting bitmap
        /// for this case.
        /// </summary>
        /// <param name="oSlot">A view slot from the MainWin</param>
        public SKImage TabIcon( ViewSlot oSlot ) {
            if( oSlot.Icon != null )
                return SKImage.FromBitmap( oSlot.Icon );

            using SKSurface oSurface = SKSurface.Create( _sIconInfo );
            using SKPaint   oPaint   = new () { Color = SKColors.Red };

            oSurface.Canvas.DrawRect( 0, 0, _sIconInfo.Width, _sIconInfo.Height, oPaint );
            oSurface.Flush();

            return oSurface.Snapshot();
        }

        /// <summary>
        /// Note that the window only repaints if you call the base
        /// OnMouseLeave() event. Else we don't get the behavior we want.
        /// </summary>
        protected override void OnMouseLeave(EventArgs e) {
            try {
                foreach( LayoutStack oTab in Layout ) {
                    oTab.Item(3).Hidden = true;
                    oTab.LayoutChildren();
                }
            } catch( Exception oEx ) {
                Type[] rgErrors = { typeof( ArgumentOutOfRangeException ),
                                    typeof( NullReferenceException ) };
                if( rgErrors.IsUnhandled( oEx ) )
                    throw;
                _oSiteView.LogError( "Main Win Tabs", "Error in collection" );
            }

            base.OnMouseLeave(e);
        }

        protected override void OnCheckStatus() {
            try {
                foreach( LayoutStack oTab in Layout ) {
                    oTab.Item(3).Hidden = oTab != HoverTab;
                    oTab.LayoutChildren();
                }
            } catch( Exception oEx ) {
                Type[] rgErrors = { typeof( ArgumentOutOfRangeException ),
                                    typeof( NullReferenceException ) };
                if( rgErrors.IsUnhandled( oEx ) )
                    throw;
                _oSiteView.LogError( "Main Win Tabs", "Error in collection" );
            }
        }

        /// <summary>
        /// This gets called whenever the pattern tab needs to be drawn/painted
        /// </summary>
        /// <param name="iID">Id of the tab to return the requested info.
        /// obviously we are depending on the fact that the object in
        /// question, a line from a Editor that is a list of ViewSlot(s)!!</param>
        /// <returns>Focus status</returns>
        public override SKColor TabStatus( object oID ) {
            if( oID is ViewSlot oSlot ) {
                if( oSlot.Focused ) {
                    return _oStdUI.ColorsStandardAt( StdUIColors.BGSelectedFocus );
                }

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

        protected override void OnTabLeftClicked( LayoutStack oTab, SKPointI sPoint ) {
            if( oTab.Extra is ViewSlot oSlot ) {
                if( oTab.Item(3).IsInside( sPoint.X, sPoint.Y ) ) {
                    _oHost.ViewClose( oSlot );
                } else {
                    _oHost.ViewSelect( oSlot, fFocus:true );
                }
            }
        }
    }

}
