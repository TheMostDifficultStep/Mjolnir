using System;
using System.Collections.Generic;
using System.Windows.Forms;

using SkiaSharp;
using SkiaSharp.Views.Desktop;

using Play.Rectangles;
using Play.Interfaces.Embedding;

namespace Play.Controls {
    public delegate void ScrollBarEvent( ScrollEvents e );

    public abstract class GadgetRect : 
        SmartRect,
        IPgVisibleObject2
    {
        private   SHOWSTATE _eViewState     = SHOWSTATE.Inactive;
        private   bool      _fIsHidden      = false;
        protected SKColor   _oColorActive   = new SKColor(122, 164, 234);
        protected SKColor   _oColorInActive = new SKColor(160, 160, 160);

        public abstract void Paint( SKCanvas skCanvas, IPgStandardUI oStdUI );

        /// <summary>
        /// Bug: We used to be able to get this on the Graphics pointer in GDI32 on paint.
        /// SKControl doesn't seem to have anything so hard coded for now.
        /// </summary>
        [Obsolete]public SKPoint Dpi {
            get {
                return new SKPoint( 96, 96 );
            }
        }

        public SKColor BaseColor {
            get {
                if( _eViewState == SHOWSTATE.Inactive ) {
                    return( _oColorInActive );
                }
                return _oColorActive;
            }
        }

        public virtual SHOWSTATE ShowAs {
            get {
                return ( _eViewState );
            }

            set {
                _eViewState = value;
            }
        }

        public bool Hidden {
            set { _fIsHidden = value; }
            get { return( _fIsHidden ); }
        }
    } // End class

    public enum ScrollDirection {
        NONE,
        UP,
        DOWN,
        RIGHT,
        LEFT
    }

    public class Extremity : GadgetRect {
        public Extremity() {
        }

        public override void Paint( SKCanvas skCanvas, IPgStandardUI oStdUI ) {
            SKPointI  pntCenter = this.GetPoint( LOCUS.CENTER );
            SmartRect rctDot    = new SmartRect( LOCUS.CENTER, 
                                                 pntCenter.X, pntCenter.Y, 
                                                 (int)(Dpi.X / 16), (int)(Dpi.Y / 16) );

            using SKPaint skPaint = new SKPaint() { Color = BaseColor };

            skCanvas.DrawRect( rctDot.Left, rctDot.Top, rctDot.Width, rctDot.Height, skPaint );
        }
    }

    public class Middle : GadgetRect {
        public override void Paint( SKCanvas skCanvas, IPgStandardUI oStdUI ) {
            base.Paint( skCanvas );
        }
    }

    public class ThumbBase : GadgetRect {
        public override void Paint( SKCanvas skCanvas, IPgStandardUI oStdUI ) {
            if( Hidden ) 
                return;

            int iWidth = this[SCALAR.WIDTH];

            if( iWidth >= 0.1875F * Dpi.X ) {
                iWidth = (int)( Dpi.X / 7 );
            }

            int iLeft = (( this[SCALAR.WIDTH] - iWidth ) / 2 ) + 1;

            SmartRect rctWhole = new SmartRect( LOCUS.UPPERLEFT,
                                                iLeft,
                                                this[SCALAR.TOP],
                                                iWidth-1,
                                                this[SCALAR.HEIGHT] );

            using SKPaint skPaint = new SKPaint() { Color = BaseColor };

            skCanvas.DrawRect( rctWhole.Left, rctWhole.Top, rctWhole.Width, rctWhole.Height, skPaint );
        }
    }

    public class Thumb : ThumbBase {
        public override void Paint( SKCanvas skCanvas, IPgStandardUI oStdUI ) {
            if( Hidden ) 
                return;

            base.Paint( skCanvas, oStdUI ); // This paints our base color.

            // Turn this off if we're very narrow. Also I'm thinking I'd actually rather only light this up when
            // hovering instead of only when we're active.
            if( this[SCALAR.WIDTH] >= 0.1875F * Dpi.X && this.ShowAs == SHOWSTATE.Active ) {
                SKPointI  pntCenter = this.GetPoint( LOCUS.CENTER );

                SmartRect rctBadge = new SmartRect( LOCUS.CENTER,
                                                    pntCenter.X, pntCenter.Y, 
                                                    (int)(Dpi.X / 18), 
                                                    (int)(Dpi.Y / 18) );

                if( this.GetScalar(SCALAR.HEIGHT) > (int)(Dpi.Y / 16) + 2 ) {
                    using SKPaint skPaint = new SKPaint() { Color = SKColors.White };
                    skCanvas.DrawRect( rctBadge.Left, rctBadge.Top, rctBadge.Width, rctBadge.Height, skPaint );
                }
            }
        }
    }

    public class ScrollBar2 : SKControl, IPgParent {
        protected readonly IPgBaseSite       _oSite; // Doesn't need a view site at present.
        protected readonly IPgRoundRobinWork _oSiteWorkThumb;

        readonly GadgetRect _oUp     = new Extremity(); // Page up   region
        readonly GadgetRect _oDown   = new Extremity(); // Page down region
        readonly GadgetRect _oMiddle = new Middle(); // The area where the thumb lives.
        readonly GadgetRect _oThumb  = new Thumb();  // The thumb itself!

        readonly List<GadgetRect> _rgRender = new List<GadgetRect>();

        public event ScrollBarEvent Scroll;

        SHOWSTATE _eViewState = SHOWSTATE.Inactive;

        float _flExposureFraction = (float)0.1;
        float _flProgressFraction = (float)0.0;

        public IPgParent Parentage => _oSite.Host;
        public IPgParent Services  => Parentage.Services;

        SmartGrabDrag _oDrag = null;

        public ScrollBar2( IPgBaseSite oSite ) {
            _oSite = oSite ?? throw new ArgumentNullException( "Site must not be null" );

            if( Services is IPgScheduler oScheduler ) {
                _oSiteWorkThumb = oScheduler.CreateWorkPlace(); // Just disables auto page scroll.
            }

            _rgRender.Add( _oUp );
            _rgRender.Add( _oDown );
            _rgRender.Add( _oMiddle  );
            _rgRender.Add( _oThumb  );

            this.Cursor = Cursors.Hand;
            
            DoubleBuffered = true;
            TabStop        = false;
        }

        /// <summary>
        /// Call this for any type of scroll event
        /// </summary>
        protected void Raise_Scroll( ScrollEvents e ) {
            Scroll?.Invoke( e );
        }

        /// <summary>
        /// Call this when finished scrolling, cleans up after drag operations
        /// and stops page scroll worker.
        /// </summary>
        protected void Raise_ScrollFinished() {
            _oSiteWorkThumb.Stop();

            if( _oDrag != null ) {
                Raise_Scroll(ScrollEvents.ThumbPosition);

                _oDrag.Dispose();
                _oDrag = null;
            }
        }

        /// <summary>
        /// Wait 200ms before starting the worker, just in case we cancel the job
        /// before beginning page scroll repeat.
        /// </summary>
        /// <param name="e">The type of scroll we want to repeat.</param>
        protected void RepeaterStart( ScrollEvents e ) {
            _oSiteWorkThumb.Queue( RepeaterCreate( e ), 200 );
            Raise_Scroll( e ); // Need this one bump, in case just want to scroll only one page.
        }

        /// <summary>
        /// This is our worker object that is called back by the round robin scheduler.
        /// </summary>
        /// <param name="e">The type of scroll event we want to trigger.</param>
        /// <returns>Time in milliseconds we'd like until the next event.</returns>
        public IEnumerator<int> RepeaterCreate( ScrollEvents e) {
            while( true ) {
                Raise_Scroll( e );
                yield return 60;
            }
        }
        
        public void Show( SHOWSTATE eState ) {
            foreach( IPgVisibleObject2 oView in _rgRender ) {
                oView.ShowAs = eState;
            }

            _eViewState = eState;

            Invalidate();
        }

        /// <summary>
        /// We'll end up back here after OnTick events.
        /// </summary>
        /// <seealso cref="RaiseScroll"/>
        public void Refresh( float flExposureFraction, float flProgressFraction ) {
            if( flExposureFraction > 1 )
                flExposureFraction = 1;
            if( flExposureFraction < 0 )
                flExposureFraction = 0;

            if( flProgressFraction > 1 )
                flProgressFraction = 1;
            if( flProgressFraction < 0 )
                flProgressFraction = 0;

            _flExposureFraction = flExposureFraction;
            _flProgressFraction = flProgressFraction;

            Invalidate();
        }

        public float Progress {
            get {
                return( _flProgressFraction );
            }

            set {
                Refresh( _flExposureFraction, value );
            }
        }

        public float Exposure {
            get {
                return( _flExposureFraction );
            }

            set {
                Refresh( value, _flProgressFraction );
            }
        }

        protected override void OnSizeChanged(EventArgs e) {
            base.OnSizeChanged(e);
            Invalidate();
        }

        protected override void OnMouseDown( MouseEventArgs e ) {
            base.OnMouseDown(e);

            if( _oUp.IsInside( e.X, e.Y ) ) {
                _flProgressFraction = 0;
                Raise_Scroll( ScrollEvents.First );
            }
            if( _oDown.IsInside( e.X, e.Y ) ) {
                _flProgressFraction = 100;
                Raise_Scroll( ScrollEvents.Last );
            }
            if( _oMiddle.IsInside( e.X, e.Y ) ) {
                switch( _oThumb.IsWhere( e.X, e.Y ) ) {
                    case LOCUS.TOP:
                        RepeaterStart(ScrollEvents.LargeDecrement );
                        break;
                    case LOCUS.BOTTOM:
                        RepeaterStart(ScrollEvents.LargeIncrement );
                        break;
                    case LOCUS.CENTER:
                        _oDrag = new SmartGrabDrag( null, _oThumb, SET.RIGID, LOCUS.UPPERLEFT, e.X, e.Y, null );

                        Invalidate();
                        break;
                }
            }
        }

        protected override void OnMouseUp( MouseEventArgs e ) {
            base.OnMouseUp(e);

            Raise_ScrollFinished();

            Invalidate();
        }

        protected override void OnMouseLeave( EventArgs e ) {
            base.OnMouseLeave(e);

            Raise_ScrollFinished();

            // Set children to my view state.
            foreach( IPgVisibleObject2 oView in _rgRender ) {
                oView.ShowAs = _eViewState;
            }
            Invalidate();
        }

        protected override void OnMouseMove( MouseEventArgs e ) {
            base.OnMouseMove(e);

            if( _oDrag != null ) {
                _oDrag.Move( e.X, e.Y );

                int iDiff   = _oThumb[SCALAR.TOP] - _oMiddle[SCALAR.TOP];
                int iHeight = MiddleHeight;

                if( iDiff < 0 )
                    iDiff = 0;
                if( iDiff > MiddleHeight )
                    iDiff = MiddleHeight;

                _flProgressFraction = iDiff / (float)iHeight;

                // Rememeber! The user might reset us ie. change the progressfraction
                // in the scroll event callback!
                Raise_Scroll(ScrollEvents.ThumbTrack);

                Invalidate();
            }

            if( _oUp.IsInside( e.X, e.Y ) ) {
                Cursor = Cursors.PanNorth;
            }
            if( _oDown.IsInside( e.X, e.Y ) ) {
                Cursor = Cursors.PanSouth;
            }
            if( _oMiddle.IsInside( e.X, e.Y ) ) {
                switch( _oThumb.IsWhere( e.X, e.Y ) ) {
                    case LOCUS.TOP:
                        Cursor = Cursors.Hand;
                        return;
                    case LOCUS.BOTTOM:
                        Cursor = Cursors.Hand;
                        return;
                    case LOCUS.CENTER:
                        Cursor = Cursors.SizeNS;
                        return;
                }
            }
        }

        protected override void OnMouseHover( EventArgs e ) {
            base.OnMouseHover(e);

            foreach( IPgVisibleObject2 oView in _rgRender ) {
                oView.ShowAs = SHOWSTATE.Active;
            }

            Invalidate();
        }

        private int MiddleHeight {
            get {
                int iMiddleHeight = this.Height - _oDown[SCALAR.HEIGHT ] - _oUp[SCALAR.HEIGHT ];

                if( iMiddleHeight < 0 )
                    iMiddleHeight = 0;

                return( iMiddleHeight );
            }
        }

        /// <summary>
        /// This is pretty simple, but we could replace this code with the new
        /// LayoutStack object. As a matter of fact, we'll do that if we
        /// ever must have a horizontal scroll bar. 
        /// </summary>
        private void SetSizes( SKPoint pntDpi ) {
            _oUp  .SetRect( LOCUS.UPPERLEFT,
                              0,
                              0,
                              this.Width,
                              (int)(pntDpi.Y * .1875) );
            _oDown.SetRect( LOCUS.UPPERLEFT,
                              0,
                              _oUp[SCALAR.BOTTOM ],
                              this.Width,
                              (int)(pntDpi.Y * .1875) );

            int iMiddleHeight = MiddleHeight;

            _oMiddle .SetRect( LOCUS.UPPERLEFT,
                               0,
                               _oDown[SCALAR.BOTTOM ],
                               this.Width,
                               iMiddleHeight  );

            int iThumbHeight = (int)(iMiddleHeight * _flExposureFraction);
            int iMinHeight   = (int)(pntDpi.X * 3 / 10);
            if( iThumbHeight < iMinHeight ) {
                iThumbHeight = iMinHeight;
            }

            //int iDelta  = _oMiddle.GetScalar(SCALAR.HEIGHT) - iExposure;
            int iOffset = (int)(iMiddleHeight * _flProgressFraction);
            if( iOffset < 0 )
                iOffset = 0;

            _oThumb.SetRect(LOCUS.UPPERLEFT,
                            _oMiddle[SCALAR.LEFT ],
                            iOffset + _oMiddle[SCALAR.TOP],
                            _oMiddle[SCALAR.WIDTH ],
                            iThumbHeight );

            if( _oThumb[SCALAR.BOTTOM] > _oMiddle[SCALAR.BOTTOM] ) {
                // don't let the thumb's bottom wander past the bottom of the middle.
                _oThumb .SetRect(LOCUS.LOWERLEFT,
                                  _oMiddle[SCALAR.LEFT  ],
                                  _oMiddle[SCALAR.BOTTOM],
                                  _oMiddle[SCALAR.WIDTH ],
                                  iThumbHeight );
            }

            _oThumb.Hidden = _oThumb[SCALAR.HEIGHT] >= _oMiddle[SCALAR.HEIGHT];
        }

        protected override void OnPaintSurface( SKPaintSurfaceEventArgs e ) {
            base.OnPaintSurface(e);

            try {
                IPgMainWindow.PgDisplayInfo oInfo = new IPgMainWindow.PgDisplayInfo();
                if( _oSite.Host.TopWindow is IPgMainWindow oMainWin ) {
                    oInfo = oMainWin.MainDisplayInfo;
                }

                SetSizes( new SKPoint( oInfo.pntDpi.X, oInfo.pntDpi.Y ) ); 

                SKColor   clrBase = new SKColor( 235, 235, 240 );
                SmartRect rcWhole = new SmartRect( LOCUS.UPPERLEFT, 0, 0, this.Width, this.Height );

                using SKPaint skPaint = new SKPaint() { Color = clrBase };
                e.Surface.Canvas.DrawRect( rcWhole.Left, rcWhole.Top, rcWhole.Width, rcWhole.Height, skPaint );

                foreach( GadgetRect oRect in _rgRender ) {
                    oRect.Paint( e.Surface.Canvas, null );
                }
            } catch( Exception oEx ) {
                Type[] rgErrors = { typeof( ArgumentException ),
                                    typeof( ArgumentNullException ),
                                    typeof( NullReferenceException ) };
                if( rgErrors.IsUnhandled( oEx ) )
                    throw;
            }
        }
    }
}
