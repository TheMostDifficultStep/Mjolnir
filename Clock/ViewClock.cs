using Play.Drawing;
using Play.Interfaces.Embedding;
using SkiaSharp;
using SkiaSharp.Views.Desktop;
using System;
using System.Reflection;
using System.Xml;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.Rebar;

namespace Play.Clock {
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
        protected        IPgViewSite _oViewSite;

		public static Guid Guid { get; } = Guid.Empty;
        public DocumentClock DocClock { get; }

        public IPgParent Parentage => _oViewSite.Host;

        public IPgParent Services  => Parentage.Services;

        public bool IsDirty  => false;

        public string Banner => "Clock";

        public SKBitmap Icon {get;}

        public Guid Catagory => Guid;

        public ViewAnalogClock( IPgViewSite oViewSite, DocumentClock oDocClock ) {
            DocClock   = oDocClock ?? throw new ArgumentNullException();
            _oViewSite = oViewSite ?? throw new ArgumentNullException();

			Icon       = SKImageResourceHelper.GetImageResource( Assembly.GetExecutingAssembly(), _strViewIcon );
        }

        public bool Load(XmlElement oStream) {
            return true;
        }

        public bool InitNew() {
            return true;
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

            rgAngle [0] = dtNow.Hour   * 30 % ( 360 * Half( dtNow.Minute ) );
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
