﻿using System;
using System.Collections.Generic;
using System.Drawing;
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
        ScrollBar2 _oHost;
        bool       _fHovering  = false;
        SHOWSTATE  _eViewState = SHOWSTATE.Inactive;
        bool       _fIsHidden  = false;

        protected SKColor _oColorActive   = new SKColor(122, 164, 234);
        protected SKColor _oColorInActive = new SKColor(160, 160, 160);

        public abstract void Paint( SKCanvas skCanvas, IPgStandardUI oStdUI );

        /// <summary>
        /// Bug: We used to be able to get this on the Graphics pointer in GDI32 on paint.
        /// SKContro doesn't seem to have anything so hard coded for now but we 
        /// can still get from the window if we want.
        /// </summary>
        public SKPoint Dpi {
            get {
                return new SKPoint( 96, 96 );
            }
        }

        public ScrollBar2 Host {
            get{
                return( _oHost );
            }

            set {
                _oHost = value;
            }
        }

        public SKColor BaseColor {
            get {
                if( _fHovering ) {
                    return _oColorActive;
                }
                if( _eViewState == SHOWSTATE.Inactive ) {
                    return( _oColorInActive );
                }
                return _oColorActive;
            }
        }

        #region ISmartDragGuest Members

        /// <summary>
        /// If we are in hover mode stop hovering since another nearby object might have
        /// been chosen instead.
        /// </summary>
        public void HoverStop() {
            if( _fHovering ) {
                _fHovering = false;
                //OnAttribute();
            }
        }

        public bool Hovering {
            get {
                return ( _fHovering );
            }
        }

        public bool Hover( int p_iX, int p_iY, out bool fChanged ) {
            bool fIsInside = IsInside(p_iX, p_iY);
            
			fChanged = fIsInside != _fHovering;

            if( fChanged ) {
                _fHovering = fIsInside;
            }

            return fIsInside;
        }

        #endregion

        #region IPgVisibleObject2 Members

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

        public bool Help {
            get {
                return( false );
            }
            set {}
        }

        #endregion

        #region IPgObjectWithSite Members

        public IPgBaseSite HostSite {
            get {
                throw new NotImplementedException();
            }
            set {
                throw new NotImplementedException();
            }
        }

        #endregion

    } // End class

    public enum ScrollDirection {
        NONE,
        UP,
        DOWN,
        RIGHT,
        LEFT
    }

    public class Extremity : GadgetRect {
        ScrollDirection _eDir = ScrollDirection.NONE;

        public Extremity( ScrollDirection eDir ) {
            _eDir = eDir;
        }

        public override void Paint( SKCanvas skCanvas, IPgStandardUI oStdUI ) {
            SKPointI  pntCenter = this.GetPoint( LOCUS.CENTER );
            SmartRect rctDot    = new SmartRect( LOCUS.CENTER, 
                                                 pntCenter.X, pntCenter.Y, 
                                                 (int)(Dpi.X / 16), (int)(Dpi.Y / 16) );

            using SKPaint skPaint = new SKPaint() { Color = BaseColor };

            skCanvas.DrawRect( rctDot.Left, rctDot.Top, rctDot.Width, rctDot.Height, skPaint );
        }
    } // end class

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

    public class ScrollBar2 : SKControl {
        readonly GadgetRect _oUp     = new Extremity( ScrollDirection.UP);
        readonly GadgetRect _oDown   = new Extremity( ScrollDirection.DOWN);
        readonly GadgetRect _oMiddle = new Middle();
        readonly GadgetRect _oThumb  = new Thumb();

        readonly List<GadgetRect> _rgRender = new List<GadgetRect>();

        readonly Timer _oTimer           = new Timer();
        readonly int   _iPause           = 400;
        readonly int   _iInterval        = 60;
        SHOWSTATE      _eViewState       = SHOWSTATE.Inactive;
        ScrollEvents   _eLastScrollEvent = ScrollEvents.EndScroll;

        float _flExposureFraction = (float)0.1;
        float _flProgressFraction = (float)0.0;

        SmartGrabDrag _oDrag = null;

        public ScrollBar2() {
            _rgRender.Add( _oUp );
            _rgRender.Add( _oDown );
            _rgRender.Add( _oMiddle  );
            _rgRender.Add( _oThumb  );

            foreach( GadgetRect oRect in _rgRender ) {
                oRect.Host = this;
            }

            this.Cursor = Cursors.Hand;
            
            DoubleBuffered = true;

            _oTimer.Interval = _iPause;
            _oTimer.Tick += new EventHandler( OnTick);

            TabStop = false;
        }

        public event ScrollBarEvent Scroll;

        protected void Raise_Scroll( ScrollEvents e ) {
            _eLastScrollEvent = e;

            Scroll?.Invoke( e );
        }

        public void Show( SHOWSTATE eState ) {
            foreach( IPgVisibleObject2 oView in _rgRender ) {
                oView.ShowAs = eState;
            }

            _eViewState = eState;

            Invalidate();
        }

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

        void OnTick(object sender, EventArgs e) {
            Raise_Scroll( _eLastScrollEvent );

            // We start the wait time at a high value and then shorten
            // it while the user holds down the mouse.
            if( _oTimer.Interval > _iInterval ) {
                _oTimer.Interval = _iInterval;
            }
        }

        protected void StartScroll( ScrollEvents e ) {
            _oTimer.Interval = _iPause;
            _oTimer.Start();

            Raise_Scroll( e );
        }

        protected void Raise_ThumbFinished() {
            if( _oDrag != null ) {
                Raise_Scroll(ScrollEvents.ThumbPosition);

                _oDrag.Dispose();
                _oDrag = null;
            }
        }

        protected override void OnMouseDown( MouseEventArgs e ) {
            base.OnMouseDown(e);

            if( _oUp.IsInside( e.X, e.Y ) ) {
                //StartScroll(ScrollEvents.SmallDecrement );
                _flProgressFraction = 0;
                Raise_Scroll( ScrollEvents.First );
            }
            if( _oDown.IsInside( e.X, e.Y ) ) {
                //StartScroll(ScrollEvents.SmallIncrement );
                _flProgressFraction = 100;
                Raise_Scroll( ScrollEvents.Last );
            }
            if( _oMiddle.IsInside( e.X, e.Y ) ) {
                switch( _oThumb.IsWhere( e.X, e.Y ) ) {
                    case LOCUS.TOP:
                        StartScroll(ScrollEvents.LargeDecrement );
                        break;
                    case LOCUS.BOTTOM:
                        StartScroll(ScrollEvents.LargeIncrement );
                        break;
                    case LOCUS.CENTER:
                        _oDrag = new SmartGrabDrag( null, _oThumb, SET.RIGID, LOCUS.UPPERLEFT, e.X, e.Y );

                        Invalidate();
                        break;
                }
            }
        }

        protected override void OnMouseUp( MouseEventArgs e ) {
            base.OnMouseUp(e);
            _oTimer.Stop();

            Raise_ThumbFinished();

            Invalidate();
        }

        protected override void OnMouseLeave( EventArgs e ) {
            base.OnMouseLeave(e);

            _oTimer.Stop();

            Raise_ThumbFinished();

            // Set children to my view state.
            foreach( IPgVisibleObject2 oView in _rgRender ) {
                oView.ShowAs = _eViewState;
            }
            Invalidate();
        }

        protected override void OnMouseMove( MouseEventArgs e )
        {
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

        protected override void OnMouseHover( EventArgs e )
        {
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
                SetSizes( new SKPoint( 96, 96 ) );

                Color     clrBase = Color.FromArgb( 255, 235, 235, 240 );
                SmartRect rcWhole = new SmartRect( LOCUS.UPPERLEFT, 0, 0, this.Width, this.Height );

                using SKPaint skPaint = new SKPaint() { Color = new SKColor( clrBase.R, clrBase.G, clrBase.B ) };
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
