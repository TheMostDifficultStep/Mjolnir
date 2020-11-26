using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Windows.Forms;
using System.Drawing;

using SkiaSharp;
using SkiaSharp.Views.Desktop;

using Play.Interfaces.Embedding;
using Play.Edit;
using Play.Rectangles;

namespace Play.Forms {
	public class ContextMenuTest : SKControl {
		public IPgBaseSite    PgSite { get; }
		public IPgStandardUI2 StdUI  { get; }

		protected FTCacheLine Cache  { get; }

		protected uint _uiStdText; // Font index;

		public ContextMenuTest( IPgBaseSite oSiteBase ) {
			PgSite = oSiteBase ?? throw new ArgumentNullException( "Site for form control must not be null" );

 			StdUI      = PgSite.Host.Services as IPgStandardUI2 ?? throw new ArgumentException( "Parent view must provide IPgStandardUI service" );
            _uiStdText = StdUI.FontCache( StdUI.FaceCache( @"C:\windows\fonts\consola.ttf" ), 12, new SKSize( 96, 96 ) );

			Cache = new FTCacheLine( new TextLine( 0, "hello" ) );
			Cache.Update( StdUI.FontRendererAt( _uiStdText ) );
			Cache.OnChangeSize( 300 );
		}

		protected override void OnPaintSurface( SKPaintSurfaceEventArgs e ) {
			base.OnPaintSurface( e );

			using SKPaint skPaint = new SKPaint() { Color=SKColors.LightCoral };

			e.Surface.Canvas.DrawRect( 0, 0, Width, Height, skPaint );
			Cache.Render( e.Surface.Canvas, StdUI, new PointF( 0, 0 ) );
		}

        protected override void OnMouseDown( MouseEventArgs e ) {
            base.OnMouseDown(e);

			Hide();
        }
    }

}
