using System.Drawing;
using System;

using Play.Interfaces.Embedding;
using SkiaSharp;

namespace Play.Rectangles {
    public static class ColorExt {
        public static Color ToGdiColor( this SKColor skColor ) {
            return Color.FromArgb( skColor.Alpha,
                                   skColor.Red, 
                                   skColor.Blue, 
                                   skColor.Green );
        }
         public static SKColor ToSKColor( this Color gdiColor ) {
            return new SKColor( gdiColor.R,
                                gdiColor.G,
                                gdiColor.B, 
                                gdiColor.A );
        }
   }

	/// <remarks>Note that Binders are never hidden!! ^_^;;</remarks>
    public class SmartBinder : 
        LayoutRect
    {
        protected bool               _fHovering = false;
		protected readonly SmartRect _rcMiddle  = new SmartRect();
        public int       Orientation{ get; set; } = 0;
		public SHOWSTATE Show       { get; set; } = SHOWSTATE.Inactive;

		public SmartBinder( int iTrack ) : base( CSS.Pixels, (uint)iTrack, 1 ) {
            _rcMiddle.Width  = (int)Track;
            _rcMiddle.Height = (int)Track;
		}

        /// <seealso cref="SmartGrab.BarColor"
        protected SKColor BarColor {
            get {
                SKColor sFocusColor = SKColors.Transparent;

                switch( Show ) {
                    case SHOWSTATE.Focused:
                        sFocusColor = SystemColors.Highlight.ToSKColor();
                        break;
                    case SHOWSTATE.Active:
                        sFocusColor = new SKColor(255, 0xa0, 0xa0, 0xa0);
                        break;
                    case SHOWSTATE.Inactive:
                        if( _fHovering )
                            sFocusColor = SKColors.LightBlue; 
                        else
                            sFocusColor = SKColors.LightGray; 
                        break;
                }
                return sFocusColor;
            }
        }

        [Obsolete]public override void Paint( Graphics p_oGraphics ) {
            base.Paint(p_oGraphics);

            using( Brush oFocusBrush = new SolidBrush( BarColor.ToGdiColor() )) {
                try {
                    p_oGraphics.FillRectangle(oFocusBrush, this.Rect );
                } catch( OverflowException ) {
                    // Inside out rectangle I'd wager.
                }
            }
        }

        protected override void OnSize() {
            SKPointI l_pntCenter = GetPoint(LOCUS.CENTER);

            l_pntCenter.X -= (int)Track >> 1;
            l_pntCenter.Y -= (int)Track >> 1;

            _rcMiddle.SetPoint (SET.RIGID, LOCUS.UPPERLEFT, l_pntCenter.X, l_pntCenter.Y);
        }

        /// <summary>
        /// Here we see a good example of sending the Standard UI interface to this object!!
        /// </summary>
        /// <param name="skCanvas"></param>
        public override void Paint( SKCanvas skCanvas ) {
            base.Paint(skCanvas);

            using SKPaint skPaint = new SKPaint();

            skPaint.Color = BarColor;
            skPaint.Style = SKPaintStyle.Fill;

            skCanvas.DrawRect( this.SKRect, skPaint );

            if( _fHovering ) {
                skPaint.Color = SKColors.White;

                skCanvas.DrawRect( _rcMiddle.SKRect, skPaint );
            }
        }
        #region ISmartDragGuest Members

        /// <summary>
        /// If we are in hover mode stop hovering since another nearby object might have
        /// been chosen instead.
        /// </summary>
        public void HoverStop() {
            _fHovering = false;
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
    }

    /// <summary>
    /// Glue the edges of two rectangles together. This object manages
    /// moving both edges at the same time when it itself is moved!
    /// </summary>
    public class SmartSpacer : 
        SmartBinder
    {
        SmartRect _oOne;
        SCALAR    _eSideOne;
        SmartRect _oTwo;
        SCALAR    _eSideTwo;

        public SmartSpacer( TRACK eDir, SmartRect oOne, SmartRect oTwo, int iPixelTrack ) :
			base( iPixelTrack )
        {
            _oOne = oOne;
            _oTwo = oTwo;

            _eSideOne = eDir == TRACK.VERT ? SCALAR.BOTTOM : SCALAR.RIGHT;
            _eSideTwo = eDir == TRACK.VERT ? SCALAR.TOP    : SCALAR.LEFT;
        }

        /// <summary>
        /// When we are resized/moved we need to move our neighbors too! Be sure
		/// to recalculate the track percentages for moved objects.
        /// </summary>
        protected override void OnSize() {
            base.OnSize();

            _oOne.SetScalar(SET.STRETCH, _eSideOne, this.GetScalar(_eSideTwo) );
            _oTwo.SetScalar(SET.STRETCH, _eSideTwo, this.GetScalar(_eSideOne) );
        }
    } // End SmartGlue

    public class SmartSpacerDrag : SmartGrabDrag
    {
		TRACK _eAxis;

        /// <summary>
        /// This constructor is used to drag the spacer and thus re-size the adornments
        /// on the left and right (or top and bottom).
        /// </summary>
        public SmartSpacerDrag(
            DragFinished oFinished,
            SmartRect    oGuest,
			TRACK		 eAxis,
            int          iX,
            int          iY ) : 
            base( oFinished, oGuest, SET.RIGID, LOCUS.UPPERLEFT, iX, iY, null )
        {
			_eAxis = eAxis;
        }

        /// <summary>
        /// Lock the drag operation on the spacer to be either vertical or horzontal.
        /// </summary>
        protected override void SetPoint( int iX, int iY ) 
        {
            switch( _eAxis ) {
                case TRACK.VERT:
                    // Dragging us up and down.
                    Guest.SetPoint(SET.RIGID, LOCUS.UPPERLEFT, this.GetScalar(SCALAR.LEFT), iY );
                    break;
                case TRACK.HORIZ:
                    // Dragging us left and right.
                    Guest.SetPoint(SET.RIGID, LOCUS.UPPERLEFT, iX, this.GetScalar(SCALAR.TOP));
                    break;
            }
        }
    }
}
