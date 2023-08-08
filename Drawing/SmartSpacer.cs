using System.Drawing;
using System;

using Play.Interfaces.Embedding;
using SkiaSharp;

namespace Play.Rectangles {
	/// <remarks>Note that Binders are never hidden!! ^_^;;</remarks>
    public class SmartBinder : 
        LayoutRect
    {
        protected bool _fHovering  = false;

        public int       Orientation{ get; set; } = 0;
		public SHOWSTATE Show       { get; set; } = SHOWSTATE.Inactive;

		public SmartBinder( int iPixelExtent ) : base( CSS.Pixels, (uint)iPixelExtent, 1 ) {
		}

        [Obsolete]public override void Paint( Graphics p_oGraphics ) {
            base.Paint(p_oGraphics);

            Color oFocusColor = Color.Empty;

            switch( Show ) {
                case SHOWSTATE.Focused:
                    oFocusColor = Color.FromArgb(255, 0x00, 0x55, 0xE5);
                    break;
                case SHOWSTATE.Active:
                    oFocusColor = Color.FromArgb(125, 0x00, 0x55, 0xE5);
                    break;
                case SHOWSTATE.Inactive:
                    if( _fHovering )
                        oFocusColor = Color.FromArgb(255, 0xa0, 0xa0, 0xa0);
                    else
                        oFocusColor = Color.FromArgb(000, 0x00, 0x55, 0xE5);
                    break;
            }

            using( Brush oFocusBrush = new SolidBrush(oFocusColor )) {
                p_oGraphics.FillRectangle(oFocusBrush, this.Rect );
            }
        }

        /// <summary>
        /// Here we see a good example of sending the Standard UI interface to this object!!
        /// </summary>
        /// <param name="skCanvas"></param>
        public override void Paint( SKCanvas skCanvas ) {
            base.Paint(skCanvas);

            SKColor oFocusColor = SKColors.Transparent;

            switch( Show ) {
                case SHOWSTATE.Focused:
                    oFocusColor = new SKColor(255, 0x00, 0x55, 0xE5);
                    break;
                case SHOWSTATE.Active:
                    oFocusColor = new SKColor(125, 0x00, 0x55, 0xE5);
                    break;
                case SHOWSTATE.Inactive:
                    if( _fHovering )
                        oFocusColor = new SKColor(255, 0xa0, 0xa0, 0xa0);
                    else
                        oFocusColor = new SKColor(000, 0x00, 0x55, 0xE5);
                    break;
            }

            using SKPaint skPaint = new SKPaint();
            skPaint .Color = oFocusColor;
            skCanvas.DrawRect( this.SKRect, skPaint );
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
