using System;
using System.Reflection;
using System.Xml;

using SkiaSharp;
using SkiaSharp.Views.Desktop;

using Play.Drawing;
using Play.Edit;
using Play.Interfaces.Embedding;
using Play.Rectangles;

namespace Play.Clock {
    /// <summary>
    /// Since this at present is an adornment. We don't implement Load/Save.
    /// </summary>
    public class ViewDigitalClock :
        WindowMultiColumn,
        //IPgLoad<XmlElement>,
        //IPgSave<XmlDocumentFragment>,
        IPgParent,
        IPgCommandView
    {
        private   readonly string      _strViewIcon  = "Play.Clock.Content.icon_clock.gif";
        protected readonly IPgViewSite _oViewSite;

		public static Guid Guid { get; } = new Guid("AC48BBDF-C10E-4B03-BBFF-074F0445D372");
        public Guid      Catagory  => Guid;
        public string    Banner    => "World Clock";
        public SKBitmap  Icon      { get; }
        protected uint   ClockFont { get; }

        protected DocumentClock Document { get; }

        public ViewDigitalClock( IPgViewSite oViewSite, DocumentClock oDocClock ) : 
            base( oViewSite, oDocClock ) 
        {
            Document   = oDocClock ?? throw new ArgumentNullException( "Clock document must not be null." );
            _oViewSite = oViewSite;

			Icon       = SKImageResourceHelper.GetImageResource( Assembly.GetExecutingAssembly(), _strViewIcon );
            _fReadOnly = true;

            try {
                ClockFont = StdUI.FontCache( StdUI.FaceCache( @"C:\Users\hanaz\AppData\Local\Microsoft\Windows\Fonts\seven segment.ttf" ), 16, DPI );
            } catch( ApplicationException ) {
                ClockFont = StdFont;
            }

            _oCacheMan.RenderClxn.Add( ClockFont, StdUI.FontRendererAt( ClockFont ) );
        }

        protected override void Dispose( bool disposing ) {
            if( disposing ) {
                Document.ClockEvent -= OnClockUpdated;
            }
            base.Dispose(disposing);
        }

        protected override bool Initialize() {
            if( !base.Initialize() ) 
                return false;

            Document.ClockEvent += OnClockUpdated;

            TextLayoutAdd( new LayoutRect( LayoutRect.CSS.Flex ), ClockRow.ColumnTime); // time
            TextLayoutAdd( new LayoutRect( LayoutRect.CSS.Flex ), ClockRow.ColumnZone); // zones.
            TextLayoutAdd( new LayoutRect( LayoutRect.CSS.None ), ClockRow.ColumnDate); // date

            return true;
        }

        public object Decorate(IPgViewSite oBaseSite, Guid sGuid) {
            return null;
        }

        public override bool Execute(Guid sGuid) {
            return false;
        }

        protected override void OnSizeChanged(EventArgs e) {
            foreach( CacheRow oCRow in _oCacheMan ) {
                oCRow[0].FontID = ClockFont;
                oCRow[1].FontID = ClockFont;
                oCRow[2].FontID = ClockFont;
                if( oCRow.Row.At % 2 != 1 ) { // odd
                    foreach( IPgCacheRender oElem in oCRow ) {
                        oElem.BgColor = new SKColor( 201, 250, 201);
                    }
                }
            }

            //Figure this out later...
            //foreach( CacheRow oRow in _oCacheMan ) {
            //    foreach( IPgCacheRender oCache in oRow ) {
            //        oCache.BgColor = _oStdUI.ColorsStandardAt(StdUIColors.BGNoEditText);
            //    }
            //}

            base.OnSizeChanged(e);
        }

        public void OnClockUpdated() {
            // This remeasures all the items.
            OnSizeChanged( new EventArgs() );
        }
    }

    static class CanvasExtensions {
        public static void DrawPolyLine( this SKCanvas oCanvas, SKPoint[] rgPoints, SKPaint sPaint ) {
            for( int i = 0; i< rgPoints.Length-1; ++i ) {
                oCanvas.DrawLine( rgPoints[i], rgPoints[i+1], sPaint );
            }
        }

        public static void DrawPolyCircle( this SKCanvas oCanvas, SKPoint[] rgPoints, SKPaint sPaint ) {
            for( int i = 0; i< rgPoints.Length; ++i ) {
                oCanvas.DrawCircle( rgPoints[i], 10, sPaint );
            }
        }
    }

    public class ViewAnalogClock : 
        SKControl, 
        IPgParent,
		IPgLoad<XmlElement>,
		IPgSave<XmlDocumentFragment>,
        IPgCommandView
    {
        private readonly string      _strViewIcon  = "Play.Clock.Content.icon_clock.gif";
        protected readonly IPgViewSite   _oViewSite;
        protected readonly IPgViewNotify _oViewNotify;


		public static Guid Guid { get; } = Guid.Empty;
        public DocumentClock DocClock { get; }

        public IPgParent Parentage => _oViewSite.Host;

        public IPgParent Services  => Parentage.Services;

        public bool IsDirty  => false;

        public string Banner => "Clock";

        public SKBitmap Icon {get;}

        public Guid Catagory => Guid;

        public ViewAnalogClock( IPgViewSite oViewSite, DocumentClock oDocClock ) {
            DocClock     = oDocClock ?? throw new ArgumentNullException();
            _oViewSite   = oViewSite ?? throw new ArgumentNullException();

            _oViewNotify = _oViewSite.EventChain;

			Icon         = SKImageResourceHelper.GetImageResource( Assembly.GetExecutingAssembly(), _strViewIcon );
        }

        protected override void Dispose(bool disposing) {
            DocClock.ClockEvent -= ClockEvent;
        }

        protected override void OnGotFocus(EventArgs e) {
            _oViewNotify.NotifyFocused( true );
            DocClock.SetTimeout( DocumentClock.ClockUpdateInterval.Fast );
            Invalidate();
        }

        protected override void OnLostFocus(EventArgs e) {
			_oViewNotify.NotifyFocused( false );
            DocClock.SetTimeout( DocumentClock.ClockUpdateInterval.Slow );
            Invalidate();
        }

        public bool Load(XmlElement oStream) {
            return InitNew();
        }

        public bool InitNew() {
            DocClock.ClockEvent += ClockEvent;
            return true;
        }

        private void ClockEvent() {
            Invalidate();
        }

        public bool Save(XmlDocumentFragment oStream) {
            return true;
        }

        /// <summary>
        /// Destructively rotates the point. This code isn't working in
        /// my case when the X value comming in is NON-zero! This is why
        /// we can draw the face of the clock. Those points seem to
        /// rotate in the opposite direction!!! 
        /// Leaving it for now. But the hands are drawn by setting the
        /// rotation on the canvas.
        /// </summary>
        /// <remarks>Probably because MM_ISOTROPIC flips the Y axis.</remarks>
        protected void RotatePoint( ref SKPoint pntPoint, int iAngle ) {
            SKPoint      pntTemp    = new();
            const double dbl2Pi     = Math.PI * 2;
            double       dblRadians = dbl2Pi * iAngle / 360;

            pntTemp.X = (float)( pntPoint.X * Math.Cos( dblRadians ) +
                                 pntPoint.Y * Math.Sin( dblRadians ) );
            pntTemp.Y = (float)( pntPoint.Y * Math.Cos( dblRadians ) +
                                 pntPoint.X * Math.Sin( dblRadians ) );

            pntPoint = pntTemp;
        }
       
        /// <summary>
        /// This draws the face not including the arms.
        /// </summary>
        protected void DrawTicks( SKCanvas oCanvas ) {
            int     iAngle   = 0;
            SKPoint pntPoint = new SKPoint();
            SKPaint sPaint   = new() { Color = SKColors.LightBlue };

            while( iAngle < 360 ) {
                pntPoint.X = 0;
                pntPoint.Y = 900;

                RotatePoint( ref pntPoint, iAngle ); // See description.

                int iRadius = iAngle % 5 != 0 ? 15 : 60;

                oCanvas.DrawCircle( pntPoint.X, pntPoint.Y, iRadius, sPaint );

                iAngle += 6;
            }
        }

        /// <summary>
        /// I thought 0 divided by anything is 0!! O.o;; Seems to
        /// throw an exception s.
        /// </summary>
        /// <remarks>Could I just shift left?</remarks>
        /// <param name="iInput">The integer to half.</param>
        public int Half( int iInput ) {
            int iHalf = 1;

            if( iInput != 0 )
                iHalf = iInput / 2;
            if( iHalf == 0 )
                iHalf = 1;

            return iHalf;
        }

        //private int _iMinute = 0;

        /// <summary>
        /// For SOME reason every other point is rotating in the OPPOSITE direction!!
        /// That makes the hands look weird at certain points.
        /// </summary>
        /// <param name="oCanvas"></param>
        /// <param name="fSecondsOnly">Show only the seconds hand.</param>
        protected void DrawHands( SKCanvas oCanvas, bool fSecondsOnly ) {
            SKPoint[] rgHours = { new ( 0, -150 ),
                                  new ( 100,  0 ),
                                  new ( 0,  600 ),
                                  new (-100,  0 ),
                                  new ( 0, -150 ) };
            SKPoint[] rgMins  = { new ( 0, -200 ),
                                  new ( 50,   0 ),
                                  new ( 0,  800 ),
                                  new (-50,   0 ),
                                  new ( 0, -200 ) };
            SKPoint[] rgSecs  = { new ( 0, 0 ),
                                  new ( 0, 800 ) };

            SKPaint     sPaint   = new();
            int[]       rgAngle  = new int[3];
            SKPoint[][] rgPoints = new SKPoint[3][];
            SKColor[]   rgColors = new SKColor[3];
            DateTime    dtNow    = DateTime.Now;

          //Test code here...
          //DateTime    dtNow    = new DateTime( new DateOnly( 2026, 1, 24 ), new TimeOnly( 8, _iMinute++ % 60 ) );

            rgPoints[0] = rgHours;
            rgPoints[1] = rgMins;
            rgPoints[2] = rgSecs;

            rgAngle [0] = dtNow.Hour   * 30 % 360 + Half( dtNow.Minute );
            rgAngle [1] = dtNow.Minute * 6;
            rgAngle [2] = dtNow.Second * 6;

            rgColors[0] = SKColors.Green;
            rgColors[1] = SKColors.DarkBlue;
            rgColors[2] = SKColors.Red;

            for( int iHand = fSecondsOnly ? 2 : 0; iHand < 3; ++iHand ) {
                sPaint.Color = rgColors[iHand];
                // This overrides the old value and sets up the new
                // rotation. It's not cumulative thankfully!
                oCanvas.RotateDegrees( -rgAngle[iHand] );

                oCanvas.TotalMatrix.MapPoints( rgPoints[iHand] );

                oCanvas.DrawPolyLine( rgPoints[iHand], sPaint );
            }
        }

        protected override void OnPaintSurface(SKPaintSurfaceEventArgs e) {
            base.OnPaintSurface(e);

            SKCanvas oCanvas  = e.Surface.Canvas;
            SKPaint  sPaint   = new() { Color = SKColors.White }; // TODO: STDUI.
            SKRect rctDevice  = oCanvas.DeviceClipBounds;
            SKSize sszLogical = new SKSize( 2000, 2000 );

            SKPoint pntScale = new SKPoint( rctDevice.Width  / sszLogical.Width,
                                            rctDevice.Height / sszLogical.Height );

            // MM_ISOTROPIC requires uniform scaling: choose the smaller scale
            float fScale = Math.Min( pntScale.X, pntScale.Y );

            try {
                oCanvas.Save();

                oCanvas.DrawRect ( 0, 0, Width, Height, sPaint );

                oCanvas.Translate( rctDevice.Width / 2, rctDevice.Height / 2 );
                oCanvas.Scale    ( fScale, -fScale );

                DrawTicks( oCanvas );
                DrawHands( oCanvas, fSecondsOnly:false );
            } catch( NullReferenceException ) {
            } finally {
                oCanvas.Restore();
            }
        }

        public object Decorate(IPgViewSite oBaseSite, Guid sGuid) {
            return null;
        }

        public bool Execute(Guid sGuid) {
            return false;
        }
    }
}
