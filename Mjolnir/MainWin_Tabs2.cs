﻿using System;
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
    public class WindowTextTabs : 
		TabControl,
		IPgParent,
		IPgCommandView,
		IPgLoad<XmlElement>,
		IPgSave<XmlDocumentFragment>
	{
        public static readonly Guid ViewType = new Guid( "{18C24090-7BD4-401F-9B89-644CD9C14687}" );

        public Guid      Catagory  => ViewType;
        public string    Banner    => "Test Meh";
        public Image     Iconic    => null;
        public bool      IsDirty   => false;

        public IPgParent Parentage => _oSiteView.Host;
        public IPgParent Services  => Parentage.Services;

        public WindowTextTabs(IPgViewSite oSiteView, Editor oDoc) : base(oSiteView, oDoc) {
        }

        /// <summary>
        /// This gets called whenever the tab needs to be drawn.
        /// </summary>
        /// <param name="iID">Id of the tab to return the requested info.</param>
        /// <returns>Focus status</returns>
        public override SKColor TabStatus( object oID ) {
            if( oID is Line oLine ) {
                return oLine.At == 1 ? SKColors.Blue : TabBackground( oLine.At );
            }
            
            return SKColors.White;
        }

        /// <summary>
        /// This gets called whenever the tab needs to be drawn.
        /// </summary>
        /// <param name="iID">Id of the tab to return the requested info.</param>
        /// <returns>Focus status</returns>
        public override SKColor TabBackground( object oID ) {
            SKColor skBG = _oStdUI.ColorsStandardAt( StdUIColors.BGReadOnly );

            if( oID is Line oLine ) {
                return oLine.At == 1 ? SKColors.LightCyan : skBG;
            }
            return skBG;
        }

        /// <summary>
        /// This gets called when a Tab is being created.
        /// </summary>
        /// <param name="iID"></param>
        /// <returns>Return a bitmap</returns>
        public override SKBitmap TabIcon( object oID ) {
            SKBitmap skIcon = new( 30, 30, SKColorType.Rgb888x, SKAlphaType.Opaque );

            using SKPaint  oPaint  = new () { Color = SKColors.Red };
			using SKCanvas oCanvas = new ( skIcon );

			oCanvas.DrawRect( 0, 0, skIcon.Width, skIcon.Height, oPaint );

            return skIcon;
        }

        public bool Save(XmlDocumentFragment oStream) {
            return true;
        }

        public bool Load(XmlElement oStream) {
            return true;
        }

        public object Decorate(IPgViewSite oBaseSite, Guid sGuid) {
            return null;
        }

        public bool Execute(Guid sGuid) {
            return false;
        }

    }

    public class MainWin_Tabs : 
		TabControl,
		IPgParent
	{
        public IPgParent Parentage => _oSiteView.Host;
        public IPgParent Services  => Parentage.Services;

        public MainWin_Tabs(IPgViewSite oSiteView, BaseEditor oDoc) : base(oSiteView, oDoc) {
        }

        /// <summary>
        /// This gets called whenever the tab needs to be drawn.
        /// </summary>
        /// <param name="iID">Id of the tab to return the requested info.</param>
        /// <returns>Focus status</returns>
        public override SKColor TabStatus( object oID ) {
            if( oID is ViewSlot oLine ) {
                return oLine.Focused ? SKColors.Blue : TabBackground( oLine.At );
            }
            
            return SKColors.White;
        }

        /// <summary>
        /// This gets called whenever the tab needs to be drawn.
        /// </summary>
        /// <param name="iID">Id of the tab to return the requested info.</param>
        /// <returns>Focus status</returns>
        public override SKColor TabBackground( object oID ) {
            SKColor skBG = _oStdUI.ColorsStandardAt( StdUIColors.BGReadOnly );

            if( oID is ViewSlot oLine ) {
                return oLine.Focused ? SKColors.LightCyan : skBG;
            }
            return skBG;
        }

        /// <summary>
        /// Ack! The bitmap on the view, is a windows bitmap and not an SKBitmap.
        /// I'll fix that later.
        /// </summary>
        /// <returns>Return a bitmap</returns>
        public override SKBitmap TabIcon( object oID ) {
            SKBitmap skIcon = new( 30, 30, SKColorType.Rgb888x, SKAlphaType.Opaque );

            using SKPaint  oPaint  = new () { Color = SKColors.Red };
			using SKCanvas oCanvas = new ( skIcon );

			oCanvas.DrawRect( 0, 0, skIcon.Width, skIcon.Height, oPaint );

            return skIcon;
        }

    }

}
