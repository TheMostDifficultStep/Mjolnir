using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

using SkiaSharp;

using Play.Rectangles;
using Play.Interfaces.Embedding;

namespace Play.Controls
{
    public delegate void ScrollBarEvent( ScrollEvents e );

    public class ControlRect : 
        SmartRect,
        IPgVisibleObject2
    {
        ScrollBar2 _oHost;
        bool       _fHovering  = false;
        SHOWSTATE  _eViewState = SHOWSTATE.Inactive;
        bool       _fIsHidden  = false;

        protected Color _oColorActive   = Color.FromArgb(122, 164, 234);
        protected Color _oColorInActive = Color.FromArgb(160, 160, 160);

        public ScrollBar2 Host {
            get{
                return( _oHost );
            }

            set {
                _oHost = value;
            }
        }

        public Color GetBaseColor() {
            if( _fHovering ) {
                return _oColorActive;
            }
            if( _eViewState == SHOWSTATE.Inactive ) {
                return( _oColorInActive );
            }
            return( _oColorActive );
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

        public IPgBaseSite HostSite
        {
            get
            {
                throw new NotImplementedException();
            }
            set
            {
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

    public class Extremity : ControlRect
    {
        ScrollDirection _eDir = ScrollDirection.NONE;

        public Extremity( ScrollDirection eDir ) {
            _eDir = eDir;
        }

        public override void Paint(System.Drawing.Graphics p_oGraphics)
        {
          //base.Paint(p_oGraphics);

            Rectangle rctPerimeter = this.Rect;
            SKPointI  pntCenter    = this.GetPoint( LOCUS.CENTER );
            SmartRect rctDot       = new SmartRect( LOCUS.CENTER, 
                                                    pntCenter.X, pntCenter.Y, 
                                                    (int)(p_oGraphics.DpiX / 16), (int)(p_oGraphics.DpiY / 16) );

          using( Brush oCustom = new SolidBrush(Color.FromArgb( 255, 240, 240, 240 ) ) ) {
                using( Brush oBase = new SolidBrush( GetBaseColor() ) ) {
                    //p_oGraphics.FillRectangle( oCustom,  this.Rect );
                    p_oGraphics.SmoothingMode = SmoothingMode.AntiAlias;
                    p_oGraphics.FillEllipse( oBase, rctDot.Rect );
                    p_oGraphics.SmoothingMode = SmoothingMode.None;
                }
            }
        }
    } // end class

    public class Vent : ControlRect
    {
        public override void Paint(System.Drawing.Graphics p_oGraphics)
        {
          //base.Paint(p_oGraphics);

            if( this[SCALAR.WIDTH] <= 0 || this[SCALAR.HEIGHT] <= 0 )
                return;

            Rectangle rctPerimeter = this.Rect;
            SKPointI  pntCenter    = this.GetPoint( LOCUS.CENTER );
            SmartRect rctDot       = new SmartRect( LOCUS.CENTER, 
                                                    pntCenter.X, pntCenter.Y, 
                                                    (int)(p_oGraphics.DpiX / 9 ), (int)(p_oGraphics.DpiY / 9) );

            //using( Brush oCustom = new SolidBrush(Color.FromArgb( 255, 240, 240, 240 ) ) ) {
            //    p_oGraphics.FillRectangle( oCustom,  this.Rect );
            //}
            using( Brush oBase = new SolidBrush( GetBaseColor() ) ) {
                p_oGraphics.FillRectangle( oBase, rctDot.Rect );
            }
        }
    } // end class

    public class Middle : ControlRect {
        public override void Paint(Graphics p_oGraphics)
        {
            base.Paint(p_oGraphics);
            //SmartRect rcRight = new SmartRect( this );
            //SmartRect rcLeft  = new SmartRect( this );
            //SmartRect rcSquare = new SmartRect( this[SCALAR.LEFT], this[SCALAR.TOP], this[SCALAR.LEFT] + this[SCALAR.WIDTH], this[SCALAR.TOP] + this[SCALAR.WIDTH] );

            //int       iSliver = (int)( this[SCALAR.WIDTH] / 3.5 );

            //rcRight.SetScalar( SET.FLOAT, SCALAR.LEFT,  this[SCALAR.WIDTH] - iSliver - 1);
            //rcLeft .SetScalar( SET.FLOAT, SCALAR.RIGHT, iSliver );

            //Color clrBase = Color.FromArgb( 255, 235, 235, 240 );
            //Color clrEdge = Color.FromArgb( 255, 210, 210, 220 );

            //// I like a little bit of a contrast with the border when it's not lit so you can see the scroll region.
            //// hence 227 instead of 240. When I have a real color managment system I'll toss all this hard code.
            //using( Brush oSolid = new SolidBrush( clrBase ) ) {  // was 240
            //    p_oGraphics.FillRectangle( oSolid, this.Rect );
            //}
            //if( this[SCALAR.WIDTH] >= .1875F * p_oGraphics.DpiX ) {
            //    using( Brush oLeftBrush = new LinearGradientBrush( rcLeft.Rect, clrEdge, clrBase,LinearGradientMode.Horizontal ) ) {
            //        p_oGraphics.FillRectangle( oLeftBrush,  rcLeft.Rect );
            //    }
            //    using( Brush oTopBrush = new LinearGradientBrush(rcSquare.Rect,  clrEdge, clrBase,LinearGradientMode.Vertical ) ) {
            //        p_oGraphics.FillRectangle( oTopBrush,   rcSquare.Rect );
            //    }
            //    using( Brush oRightBrush = new LinearGradientBrush( rcRight.Rect,  clrBase, clrEdge, LinearGradientMode.Horizontal ) ) {
            //        p_oGraphics.FillRectangle( oRightBrush, rcRight.Rect );
            //    }
            //}
        }
    }

    public class ThumbBase : ControlRect {
        public override void Paint(Graphics p_oGraphics)
        {
            if( Hidden ) 
                return;

            base.Paint(p_oGraphics);

            int iWidth = this[SCALAR.WIDTH];

            if( iWidth >= 0.1875F * p_oGraphics.DpiX ) {
                //if( this[SCALAR.HEIGHT] > 0.5F * p_oGraphics.DpiX ) 
                    iWidth = (int)( p_oGraphics.DpiX / 7 );
            }

            int iLeft = (( this[SCALAR.WIDTH] - iWidth  ) / 2 ) + 1;

            SmartRect rctWhole = new SmartRect( LOCUS.UPPERLEFT,
                                                iLeft,
                                                this[SCALAR.TOP],
                                                iWidth-1,
                                                this[SCALAR.HEIGHT] );

            SmartRect rctTop = new SmartRect( LOCUS.UPPERLEFT, 
                                              rctWhole[SCALAR.LEFT],  
                                              rctWhole[SCALAR.TOP], 
                                              rctWhole[SCALAR.WIDTH],
                                              rctWhole[SCALAR.WIDTH] );
            SmartRect rctBot = new SmartRect( LOCUS.LOWERLEFT, 
                                              rctWhole[SCALAR.LEFT],  
                                              rctWhole[SCALAR.BOTTOM], 
                                              rctWhole[SCALAR.WIDTH], 
                                              rctWhole[SCALAR.WIDTH] );

            SKPointI  pntTopCenter = rctTop.GetPoint( LOCUS.CENTER );
            SKPointI  pntBotCenter = rctBot.GetPoint( LOCUS.CENTER );

            SmartRect rctSlider = new SmartRect( LOCUS.UPPERLEFT, 
                                                 rctWhole[SCALAR.LEFT], 
                                                 pntTopCenter.Y, 
                                                 rctWhole[SCALAR.WIDTH], 
                                                 pntBotCenter.Y - pntTopCenter.Y);


            using( Brush oGray = new SolidBrush( GetBaseColor() ) ) {
                p_oGraphics.SmoothingMode = SmoothingMode.AntiAlias;
                p_oGraphics.FillEllipse  ( oGray, rctTop.Rect );
                p_oGraphics.FillEllipse  ( oGray, rctBot.Rect );

                p_oGraphics.FillRectangle( oGray, rctSlider.Rect );
                p_oGraphics.SmoothingMode = SmoothingMode.None;
            }

        }
    }

    public class Thumb : ThumbBase {
        public override void Paint(Graphics p_oGraphics)
        {
            if( Hidden ) 
                return;

            base.Paint(p_oGraphics);

            // Turn this off if we're very narrow. Also I'm thinking I'd actually rather only light this up when
            // hovering instead of only when we're active.
            if( this[SCALAR.WIDTH] >= 0.1875F * p_oGraphics.DpiX && this.ShowAs == SHOWSTATE.Active ) {
                SKPointI  pntCenter = this.GetPoint( LOCUS.CENTER );

                SmartRect rctBadge = new SmartRect( LOCUS.CENTER,
                                                    pntCenter.X, pntCenter.Y, 
                                                    (int)(p_oGraphics.DpiX / 18), 
                                                    (int)(p_oGraphics.DpiY / 18) );

                //p_oGraphics.SmoothingMode = SmoothingMode.AntiAlias;

                if( this.GetScalar(SCALAR.HEIGHT) > (int)(p_oGraphics.DpiY / 16) + 2 ) {
                    using( Brush oWhite = new SolidBrush( Color.White ) ) {
                        p_oGraphics.FillRectangle( oWhite, rctBadge.Rect );
                    }
                }
                p_oGraphics.SmoothingMode = SmoothingMode.None;
            }
        }
    }

    public class Shadow : ThumbBase {
        public Shadow() : base() {
            _oColorActive   = Color.FromArgb( 160, 160, 160, 160);
            _oColorInActive = Color.FromArgb( 160, 160, 160, 160);
        }
    }

    public class Thumb2 : ControlRect {
        public override void Paint(Graphics p_oGraphics)
        {
            if( Hidden )
                return;

            base.Paint(p_oGraphics);

            SKPointI pntCenter = this.GetPoint( LOCUS.CENTER );

            Point[] rgPoints = { new Point( this.GetScalar(SCALAR.RIGHT), this.GetScalar(SCALAR.TOP) ), 
                                 new Point( this.GetScalar(SCALAR.LEFT ), pntCenter.Y ), 
                                 new Point( this.GetScalar(SCALAR.RIGHT), this.GetScalar(SCALAR.BOTTOM) ) };

            SmartRect rctButton = new SmartRect( LOCUS.CENTER,
                                                 pntCenter.X, pntCenter.Y, 
                                                 (int)(p_oGraphics.DpiX / 16), (int)(p_oGraphics.DpiY / 16) );

            p_oGraphics.SmoothingMode = SmoothingMode.AntiAlias;

            using( Brush oGray = new SolidBrush( GetBaseColor() ) ) {
                p_oGraphics.FillPolygon( oGray, rgPoints );
            }
        }
    }

    public class ScrollBar2 : Control
    {
        readonly ControlRect _oUp     = new Extremity( ScrollDirection.UP);
        readonly ControlRect _oDown   = new Extremity( ScrollDirection.DOWN);
        readonly ControlRect _oMiddle = new Middle();
        readonly ControlRect _oThumb  = new Thumb();
        readonly Shadow      _oHisto  = new Shadow();
        readonly ControlRect _oVent   = new Vent();

        readonly List<ControlRect> _rgRender = new List<ControlRect>();

        readonly Timer _oTimer = new Timer();
        readonly int   _iPause = 400;
        readonly int   _iInterval = 60;
        ScrollEvents   _eLastScrollEvent = ScrollEvents.EndScroll;
        SHOWSTATE     _eViewState = SHOWSTATE.Inactive;

        float _flExposureFraction = (float)0.1;
        float _flProgressFraction = (float)0.0;

        SmartGrabDrag _oDrag = null;

        public ScrollBar2() {
            _rgRender.Add( _oUp );
            _rgRender.Add( _oDown );
            _rgRender.Add( _oMiddle  );
            _rgRender.Add( _oHisto );
            _rgRender.Add( _oThumb  );
            _rgRender.Add( _oVent );

            foreach( ControlRect oRect in _rgRender ) {
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

            if( Scroll != null ) {
                Scroll( e );
            }
        }

        public void Show( SHOWSTATE eState ) {
            foreach( IPgVisibleObject2 oView in _rgRender ) {
                oView.ShowAs = eState;
            }

            _eViewState = eState;

            Invalidate();
        }

        public void Refresh( float flExposureFraction, float flProgressFraction ) 
        {
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

            _oHisto.Hidden = true;

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

        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
            Invalidate();
        }

        void OnTick(object sender, EventArgs e)
        {
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

            _oHisto.Rect = _oThumb.Rect;
            _oHisto.ShowAs = SHOWSTATE.Active;

            Raise_Scroll( e );
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
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
                        _oHisto.Rect = _oThumb.Rect;

                        Invalidate();
                        break;
                }
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            _oTimer.Stop();

            Raise_ThumbFinished();

            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);

            _oTimer.Stop();

            Raise_ThumbFinished();

            // Set children to my view state.
            foreach( IPgVisibleObject2 oView in _rgRender ) {
                oView.ShowAs = _eViewState;
            }
            Invalidate();
        }

        protected void Raise_ThumbFinished() {
            if( _oDrag != null ) {
                Raise_Scroll(ScrollEvents.ThumbPosition);

                _oDrag.Dispose();
                _oDrag = null;
            }
            _oHisto.Hidden = true;
        }

        protected override void OnMouseMove(MouseEventArgs e)
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

            if( _oVent.IsInside( e.X, e.Y ) ) {
                Cursor = Cursors.SizeAll;
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

        protected override void OnMouseHover(EventArgs e)
        {
            base.OnMouseHover(e);

            foreach( IPgVisibleObject2 oView in _rgRender ) {
                oView.ShowAs = SHOWSTATE.Active;
            }

            Invalidate();
        }

        private int MiddleHeight {
            get {
                int iMiddleHeight = this.Height - _oDown[SCALAR.HEIGHT ] - _oUp[SCALAR.HEIGHT ] - _oVent[SCALAR.HEIGHT];

                if( iMiddleHeight < 0 )
                    iMiddleHeight = 0;

                return( iMiddleHeight );
            }
        }

        private void SetSizes( Graphics oGraphics ) {
            int      iMinWidth = (int)(oGraphics.DpiY * .1875);

            _oVent.SetRect(LOCUS.UPPERLEFT,
                              0,
                              0,
                              this.Width,
                              (int)( this.Width >= iMinWidth ? iMinWidth : 0 ) );
            _oUp  .SetRect(LOCUS.UPPERLEFT,
                              0,
                              _oVent[SCALAR.BOTTOM ],
                              this.Width,
                              (int)(oGraphics.DpiY * .1875) );
            _oDown.SetRect(LOCUS.UPPERLEFT,
                              0,
                              _oUp[SCALAR.BOTTOM ],
                              this.Width,
                              (int)(oGraphics.DpiY * .1875) );

            int iMiddleHeight = MiddleHeight;

            _oMiddle .SetRect(LOCUS.UPPERLEFT,
                              0,
                              _oDown[SCALAR.BOTTOM ],
                              this.Width,
                              iMiddleHeight  );

            int iThumbHeight = (int)(iMiddleHeight * _flExposureFraction);
            int iMinHeight   = (int)(oGraphics.DpiX * 3 / 10);
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

            //VIEWSTATE vsThumb = _oThumb.Show;
            //if( iThumbHeight >= iMiddleHeight )
            //    _oThumb.Show = VIEWSTATE.Hide;
        }

        private void PaintRightStrip( Graphics oGraphics ) {
            SmartRect rcWhole = new SmartRect( LOCUS.UPPERLEFT, 0, 0, this.Width, this.Height );
            SmartRect rcRight = new SmartRect( rcWhole );
            int       iSliver = (int)( rcWhole[SCALAR.WIDTH] / 3.5 );
            Color     clrEdge = Color.FromArgb( 255, 210, 210, 220 );
            Color     clrBase = Color.FromArgb( 255, 235, 235, 240 );

            rcRight.SetScalar( SET.STRETCH, SCALAR.LEFT,  rcWhole[SCALAR.WIDTH] - iSliver - 1);

            if( rcWhole[SCALAR.WIDTH] >= .1875F * oGraphics.DpiX ) {
                using( Brush oRightBrush = new LinearGradientBrush( rcRight.Rect,  clrBase, clrEdge, LinearGradientMode.Horizontal ) ) {
                    oGraphics.FillRectangle( oRightBrush, rcRight.Rect );
                }
            }
        }

        protected override void OnPaint( PaintEventArgs p_oE )
        {
            Graphics oGraphics = p_oE.Graphics;

            SetSizes( p_oE.Graphics );

            Color     clrBase = Color.FromArgb( 255, 235, 235, 240 );
            SmartRect rcWhole = new SmartRect( LOCUS.UPPERLEFT, 0, 0, this.Width, this.Height );
            using( Brush oSolid = new SolidBrush( clrBase ) ) {  
                oGraphics.FillRectangle( oSolid, rcWhole.Rect );
            }

            // TODO: Turn on if our bg color is close to the editor bg color.
            // PaintRightStrip();

            try {
                foreach( SmartRect oRect in _rgRender ) {
                    oRect.Paint( oGraphics );
                }
            } catch( ArgumentException ) {
            }

            //_oThumb.Show = vsThumb;
        }
    }
}
