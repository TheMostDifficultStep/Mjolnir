using System;
using System.Collections.Generic;
using System.Collections;
using System.Drawing;

using SkiaSharp.Views.Desktop;
using SkiaSharp;

using Play.Interfaces.Embedding;
using Play.Rectangles;
using Play.Edit;
using Play.Drawing;
using System.Reflection.Metadata;

namespace Play.Controls {

    /// <summary>
    /// This is basically a single line editor. But it has a column to signal a popup.
    /// </summary>
    public class ViewDropDown :
        SKControl,
        IPgParent,
        IPgLoad,
        IPgCommandBase,
        IPgEditEvents
    {
        public static Guid _sGuid = new Guid( "{B14BF576-61AE-4987-9618-439B6CF8AAA7}" );

        protected readonly IPgViewSite    _oSiteView;
        protected readonly IPgViewNotify  _oViewEvents;
		protected readonly IPgStandardUI2 _oStdUI;
        protected ushort StdFace { get; }
        protected uint   StdFont { get; }


        protected readonly LayoutStack       _rgLayout = new LayoutStackHorizontal() { Spacing = 5, Units = LayoutRect.CSS.Flex };
        protected readonly FTCacheLine       _oCacheLine; // this is our single text we are displaying.
        protected readonly Line              _oTextLine;
        protected readonly IPgDocTraits<Row> _oDocTraits; // Document holding our list.
        protected readonly IReadableBag<Row> _oDocBag;
        protected readonly ImageBaseDoc      _oBmpButton; // Bitmap for the button.
        protected readonly SmartRect         _rctWorldPort = new(); // The part of the bitmap we want to show.
        protected readonly SmartRect         _rctViewPort  = new(); // where to show the part of the bmp we want to show!

        public IPgParent Parentage => _oSiteView.Host;
        public IPgParent Services  => Parentage.Services;
        protected IPgFontRender FontRender => _oStdUI.FontRendererAt( StdFont );


        /// <summary>
        /// This is my implementation of a dropdown control. At present the text is not editable
        /// and I expect I won't need editing for some time.
        /// </summary>
        /// <param name="oViewSite">Site from our parent.</param>
        /// <param name="oDocument">A multiline/multicolumn doc holding our list.</param>
        /// <param name="oBitmap">Image to use for the dropdown button.</param>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="ArgumentException"></exception>
        public ViewDropDown( IPgViewSite oViewSite, object oDocument, ImageBaseDoc oBitmap ) {
            _oSiteView = oViewSite ?? throw new ArgumentNullException();
            _oStdUI    = Services as IPgStandardUI2 ?? throw new ArgumentException( "Parent view must provide IPgStandardUI service" );

            _oDocTraits = oDocument as IPgDocTraits<Row> ?? throw new ArgumentException( "Doc must support IPgDocTraits" );
            _oDocBag    = oDocument as IReadableBag<Row> ?? throw new ArgumentException( "Doc must support IReadableBag" );
            _oBmpButton = oBitmap ?? throw new ArgumentNullException( "Please supply a bitmap" );

            IPgMainWindow.PgDisplayInfo oInfo = new IPgMainWindow.PgDisplayInfo();
            if( _oSiteView.Host.TopWindow is IPgMainWindow oMainWin ) {
                oInfo = oMainWin.MainDisplayInfo;
            }
            SKPoint pntDPI = new SKPoint( oInfo.pntDpi.X, oInfo.pntDpi.Y );

            // BUG: This is a hot mess.
            StdFace = _oStdUI.FaceCache(@"C:\windows\fonts\consola.ttf");
            StdFont = _oStdUI.FontCache( StdFace, 12, pntDPI ); 

            _oTextLine  = new TextLine( 0, "-no selection-" );
            _oCacheLine = new FTCacheWrap( _oTextLine );

            // BUG: Get the checked line set it to our cacheline.
        }

        protected void LogError( string strMessage ) {
            _oSiteView.LogError( "Drop Down", strMessage );
        }

        public bool InitNew() {
            if( _oBmpButton.Bitmap == null )
                return false;

            _rgLayout.Add( new LayoutRect( LayoutRect.CSS.None   )); // Text.
            _rgLayout.Add( new LayoutRect( LayoutRect.CSS.Pixels, 12, (float)0.1 ) ); // Arrow bitmap.

            // Show the whole bitamp. Don't look for changes, not a high pri thing.
            _rctWorldPort.SetRect( 0, 0, _oBmpButton.Bitmap.Width, _oBmpButton.Bitmap.Height );

            _oDocTraits.CheckedEvent += On_DocTraits_CheckedEvent;
            return true;
        }

        protected override void Dispose(bool disposing) {
            _oDocTraits.CheckedEvent -= On_DocTraits_CheckedEvent;

            base.Dispose(disposing);
        }

        protected override void OnSizeChanged( EventArgs e ) {
            base.OnSizeChanged(e);

            _rgLayout.SetRect( 0, 0, Width, Height );
			_rgLayout.LayoutChildren();

            ImageHelpers.ViewPortSizeMax( new( 0, 0 ), new( _rgLayout.Item(1).Width, Height ), _rctWorldPort, _rctViewPort );

            ReDoText();
        }

        public override Size GetPreferredSize(Size proposedSize) {
            return new Size( Width, (int)FontRender.LineHeight + 4 );
        }

        protected void ReDoText() {
			_oCacheLine.Measure     ( FontRender );
            _oCacheLine.Colorize    ( (ILineRange)null ); // Add selection when have it.
            _oCacheLine.OnChangeSize( _rgLayout.Item(0).Width );
        }

        /// <summary>
        /// Paint BG depending on various highlight modes for the row/column.
        /// </summary>
        protected void PaintSquareBG( 
            SKCanvas       skCanvas, 
            IPgCacheRender oElemInfo,
            SmartRect      rctCRow 
        ) {
            try {
                using SKPaint skPaint = new SKPaint() {
                    BlendMode = SKBlendMode.Src,
                    Color     = _oStdUI.ColorsStandardAt( StdUIColors.BGReadOnly )
                };
                skCanvas.DrawRect( new SKRect( 0, 0, Width, Height ), skPaint);

                StdUIColors eBg = StdUIColors.Max;

                if( Focused )
                    eBg = StdUIColors.BGWithCursor;
                 // Since our textline is NOT from the doc list this works
                 // b/c we set the Line.At to the Row.At position!! ^_^
                if( _oDocTraits.HighLight?.At == _oCacheLine.Line.At )
                    eBg = _oDocTraits.PlayHighlightColor;

                if( oElemInfo.BgColor != SKColors.Transparent ) {
                    skPaint .BlendMode = SKBlendMode.Src;
                    skPaint .Color     = oElemInfo.BgColor;
                    skCanvas.DrawRect( rctCRow.SKRect, skPaint );
                    return;
                }
                if( eBg != StdUIColors.Max ) {
                    skPaint .BlendMode = SKBlendMode.Src;
                    skPaint .Color     = _oStdUI.ColorsStandardAt( eBg );
                    skCanvas.DrawRect( rctCRow.SKRect, skPaint );
                }
            } catch( NullReferenceException ) {
                LogError( "Problem painting row/col bg" );
            }
        }

        protected override void OnPaintSurface(SKPaintSurfaceEventArgs e) {
            base.OnPaintSurface(e);

            try {
                SKSurface skSurface = e.Surface;
                SKCanvas  skCanvas  = skSurface.Canvas;

                // Test pattern...
                //skPaint.Color = iCache % 2 == 0 ? SKColors.Blue : SKColors.Green;
                //skCanvas.DrawRect( rctSquare.SKRect, skPaint );
                PaintSquareBG( skCanvas, _oCacheLine, _rgLayout.Item(0) ); // Backgrounds.

                using SKPaint skPaintTx = new SKPaint() { FilterQuality = SKFilterQuality.High };

                _oCacheLine.Render(skCanvas, _oStdUI, skPaintTx, _rgLayout.Item(0), this.Focused );

                // Some kind of side effect going on with the Paint object in the Render() function...
                // So just use a new one... :-/
                using SKPaint skPaint = new SKPaint() { FilterQuality = SKFilterQuality.High,
                                                        BlendMode     = SKBlendMode.Src };

				skCanvas.DrawRect( _rgLayout.Item(1).SKRect, skPaint );
                skCanvas.DrawBitmap( _oBmpButton.Bitmap, _rctWorldPort.SKRect, _rgLayout.Item(1).SKRect,skPaint );
                
                // BG of multi column window is always ReadOnly... O.o

            } catch( Exception oEx ) {
                Type[] rgErrors = { typeof( NullReferenceException ),
                                    typeof( ArgumentOutOfRangeException ),
                                    typeof( ArgumentNullException ),
                                    typeof( ArgumentException ),
                                    typeof( InvalidOperationException ) };
                if( rgErrors.IsUnhandled( oEx ) )
                    throw;
            }
        }

        public bool Execute(Guid sGuid) {
            return false;
        }

        private void On_DocTraits_CheckedEvent( Row oRow ) {
            if( oRow != null ) {
                _oTextLine.TryReplace( 0, _oTextLine.ElementCount, oRow[0].AsSpan );
                _oTextLine.Summate( oRow.At, 0 );
            } else {
                _oTextLine.TryReplace( 0, _oTextLine.ElementCount, "-no selection-" );
                _oTextLine.Summate( -2, 0 );
            }

            ReDoText();
        }

        public IPgEditHandler CreateEditHandler() {
            throw new NotImplementedException();
        }

        public void OnDocFormatted() {
            _oCacheLine.Colorize( (ILineRange)null ); // Add selection when have it.
            Invalidate();
        }

        /// <summary>
        /// This is where you put the caret (and in the future, selections)
        /// so that the editor can enumerate all the values and keep them
        /// up to date.
        /// </summary>
        /// <remarks>
        /// We need this object since we have to ask where the caret is
        /// BEFORE the edit. AFTER the edit even if we use the existing cache, 
        /// the local x,y of the caret comes from the new line measurements
        /// and we end up with incorrect location information.
        /// </remarks>
        protected class EditHandler :
            IEnumerable<IPgCaretInfo<Row>>,
            IPgEditHandler
        {
            readonly ViewDropDown _oHost;

            public EditHandler( ViewDropDown oHost ) {
                _oHost = oHost ?? throw new ArgumentNullException();
            }

            public IEnumerator<IPgCaretInfo<Row>> GetEnumerator() {
                yield break;
            }

            /// <summary>
            /// This gets called at the end of the session.
            /// </summary>
            /// <param name="oRow">Null if the whole buffer should
            /// be measured.</param>
            public void OnUpdated( EditType eType, Row oRow ) {
                try {
                    if( oRow != null ) {
                        if( oRow.At == _oHost._oTextLine.At ) {
                            _oHost._oTextLine.TryReplace( oRow[0].AsSpan );
                        }
                    } else {
                        oRow = _oHost._oDocBag[ _oHost._oTextLine.At ];
                        _oHost._oTextLine.TryReplace( oRow[0].AsSpan );
                    }

                    _oHost.ReDoText();
                    _oHost.Invalidate();
                } catch( NullReferenceException ) {
                    _oHost.LogError( "ViewDropDown Edit Handler Error" );
                }
            }

            IEnumerator IEnumerable.GetEnumerator() {
                return GetEnumerator();
            }
        }
    }
}
