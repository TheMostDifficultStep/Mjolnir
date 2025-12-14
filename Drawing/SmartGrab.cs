using System;
using System.Drawing;
using System.Drawing.Drawing2D;

using SkiaSharp;

using Play.Interfaces.Embedding;
using System.Collections.Generic;
using System.Collections;

namespace Play.Rectangles
{
    public interface ISmartDrag : IDisposable
    {
        SmartRect Guest{ get; }
        SmartRect Outer{ get; }
        void      Move(int p_iX, int p_iY );
      //void      Paint( Graphics ); // A paint method would be pretty spiffy, hmmm...
    }

    public struct GrabHandle {
        public readonly SmartRect _rctBox;
        public readonly LOCUS     _eLocus;
        public GrabHandle( SmartRect rctBox, LOCUS eLocus ) { 
            _rctBox = rctBox; 
            _eLocus = eLocus;
        }

        public bool IsInside( int iX, int iY ) {
            return _rctBox.IsInside( iX, iY );
        }
    }

    // I inherit from a SmartRect so I'm easy to manipulate. However, there advantages
    // to having no extent and deriving it completely from the Guest I host. For
    // example if the Guest size changes I won't update myself unless I have a callback.
    // The rect we inherit from is the "inner" rect. Outer rect lives as a property.
    public class SmartGrab :
        SmartRect,
        IEnumerable<GrabHandle>
    {
        protected SmartRect[] m_rgoHandles   = new SmartRect[12];
        protected int         m_iBorderWidth = 6;
        protected int         m_iHalfWidth   = 3;
        protected bool        m_fHovering    = false;
        protected SmartRect   m_rctGuest     = null;
      //protected RectSize    m_oMyHandler   = null;
        protected SmartRect   m_rcOuter      = new SmartRect();
        protected bool        m_fLiveDrag    = true;
        protected SHOWSTATE   m_eShowState   = SHOWSTATE.Inactive;
        protected bool[]      m_frgMoveable  = new bool[4];
        protected SCALAR      m_eMoveable    = 0;

		protected SmartRect[] _rgHandlesBorder = new SmartRect[4];
		protected SmartRect[] _rgHandlesCorner = new SmartRect[4];
		protected SmartRect[] _rgHandlesMiddle = new SmartRect[4];

        public enum HIT {
            NONE     = 0,
            EDGE     = 0x01,
            CORNER   = 0x02,
            MIDPOINT = 0x04,
            INSIDE   = 0x08
        }

        public bool Hidden { get; set; }
        public int  BorderWidth { get { return m_iBorderWidth; } }

        public SmartGrab(SmartRect p_rctGuest, int p_iBorder, bool fLiveDrag, SCALAR eMoveable ) :
            base(p_rctGuest) // copy coords to inner rect of ourselves.
        {
			m_rctGuest = p_rctGuest ?? throw new ArgumentNullException("The guest must not be null");

            if (p_iBorder < 2)
                throw new ArgumentException("Border width must be greater than one.");

            m_fLiveDrag = fLiveDrag;
            m_eMoveable = eMoveable;
            uint uiPower = 1;

            for( uint i = 0; i < m_frgMoveable.Length; ++i ) {
                uint uiResult = uiPower & (uint)eMoveable;
                m_frgMoveable[i] = uiResult != 0 ? true : false;
                uiPower *= 2;
            }

            // Listen to our guest's resizing events.
          //m_oMyHandler = new RectSize(OnGuestUpdate);
          //m_rctGuest.SizeEvent += m_oMyHandler;

            // Create all the smart rect's
            for (int i = 0; i < m_rgoHandles.Length; ++i)
                m_rgoHandles[i] = new SmartRect();

			for( int i = 0; i < 4; ++i ) {
				_rgHandlesBorder[i] = m_rgoHandles[i];
				_rgHandlesMiddle[i] = m_rgoHandles[i+4];
				_rgHandlesCorner[i] = m_rgoHandles[i+8];
			}

            // The rest are initialized with the border width and height.
            for (int i = 4; i < m_rgoHandles.Length; ++i)
                m_rgoHandles[i].SetPoint(SET.RIGID, LOCUS.LOWERRIGHT | LOCUS.EXTENT, p_iBorder, p_iBorder);

            m_iBorderWidth = p_iBorder;
            m_iHalfWidth   = p_iBorder >> 1;

            m_rcOuter.Copy = this;
            m_rcOuter.SetScalar(SET.INCR, SCALAR.ALL, m_iBorderWidth);

            UpdateHandles();
        }

        public void OnGuestUpdate(SmartRect oGuest )
        {
            if (!IsEqual(SCALAR.ALL, oGuest))
                Copy = oGuest;
        }

        public SHOWSTATE Show
        {
            get {
                return (m_eShowState);
            }

            set {
                m_eShowState = value;
            }
        }

        protected SmartRect[] Handles {
            get {
                return( m_rgoHandles );
            }
        }

        /// <summary>Our rectangle has changed so let's update the handles to follow.
        /// </summary> 
        /// <seealso cref="GetEnumerator"/>
        public virtual void UpdateHandles() {
            SCALAR[] l_rgSeq = { SCALAR.RIGHT, SCALAR.BOTTOM, SCALAR.LEFT, SCALAR.TOP };

            //m_oOuter.Copy = this;

            //// Inflate all to include the border size. Use this rect to set it up.
            //m_oOuter.SetScalar(SET.INCR, SCALAR.ALL, m_iBorderWidth);

            // Review: this offseting is coordinate system dependant. Shift the center up/left by
            // half the width of a middle box so we can use those values to create upper left of middle rects.
            SKPointI l_pntCenter = m_rcOuter.GetPoint(LOCUS.CENTER);
            l_pntCenter.X -= m_iHalfWidth;
            l_pntCenter.Y -= m_iHalfWidth;

            // Now push an edge of each inflated rect to the outer edge of
            // the Guest/view we are sizing to get our grab handles.
            // Stick the middle rects in the center and nudge each to the side
            // it occupies.
            for (uint i = 0; i < 4; ++i) {
                SCALAR l_uiScalar4 = l_rgSeq[i];
                SCALAR l_uiScalarO = l_rgSeq[(i + 2) % 4]; // Scalar on the opposite side.

                _rgHandlesBorder[i].Copy = m_rcOuter;
                _rgHandlesBorder[i].SetScalar(SET.STRETCH, l_uiScalar4, GetScalar(l_uiScalarO));

				_rgHandlesMiddle[i].SetPoint (SET.RIGID, LOCUS.UPPERLEFT, l_pntCenter.X, l_pntCenter.Y);
                _rgHandlesMiddle[i].SetScalar(SET.RIGID, l_uiScalar4, GetScalar(l_uiScalarO));
            }

            // Lastly simply calculate the corner rects so we can color them separately.
            for (uint i = 0; i < 4; ++i) {
                uint j = (i + 1) % 4;

                _rgHandlesCorner[i].Intersect(_rgHandlesBorder[i], _rgHandlesBorder[j]);
            }
        }

        // Our inner rect has changed so we need to modify the outer rect.
        protected override void OnSize() {
            // Copy our inner size to the outer and then inflate the outer.
            m_rcOuter.Copy = this;

            if( !Hidden )
                m_rcOuter.SetScalar(SET.INCR, SCALAR.ALL, m_iBorderWidth);

            base.OnSize();
        }

        public override bool LayoutChildren() {

            return true;
        }

        public override void Raise_OnSize(SmartRect p_rctOld)
        {
            base.Raise_OnSize(p_rctOld);

            UpdateHandles();

            if ( m_fLiveDrag && m_rctGuest != null)
                m_rctGuest.Copy = this;
        }

        public SmartRect Guest
        {
            get { return (m_rctGuest); }

            set
            {
				// If we have an old event sink, remove it.
				//m_rctGuest.SizeEvent -= m_oMyHandler;

                m_rctGuest = value ?? throw new ArgumentNullException("The guest must not be null"); // Take a pointer to the Guest.

                // Set our handler onto our new guest to listen for size events.
                //m_rctGuest.SizeEvent += m_oMyHandler;

                Copy = m_rctGuest;  // Copy the coordinates of it to ourselves.
                UpdateHandles();
            }
        }

        public HIT IsHit(int p_iX, int p_iY, out LOCUS pr_uiEdges) {
            LOCUS[] l_rguiSeq = { LOCUS.LEFT, LOCUS.TOP, LOCUS.RIGHT, LOCUS.BOTTOM };
            HIT     l_eType   = HIT.NONE;

            pr_uiEdges = 0;

            if (base.IsInside(p_iX, p_iY))
                l_eType = HIT.INSIDE;

            if( l_eType == HIT.NONE ) {
                // First try midpoint rects.
                for (int i = 0; i < 4; ++i) {
                    if( m_frgMoveable[i] ) {
                        if (_rgHandlesMiddle[i].IsInside(p_iX, p_iY)) {
                            pr_uiEdges = l_rguiSeq[i];
                            l_eType = HIT.MIDPOINT;
                        }
                    }
                }
            }

            if (l_eType == HIT.NONE) {
                // Then try edges. If hit two edges then it is a corner.
                for (int i = 0; i < 4; ++i) {
                    if( m_frgMoveable[i] ) {
                        if (_rgHandlesBorder[i].IsInside(p_iX, p_iY)) {
                            pr_uiEdges |= l_rguiSeq[i];
                            l_eType++; // 1=EDGE, 2=CORNER
                        }
                    }
                }
            }

            return (l_eType);
        }

        /// <summery>Our notion of what's "inside" is related to the the grab
        //			 handle itself versus what it is (wrapped) around.</summery>
        public override bool IsInside(int p_iX, int p_iY) {
            if( Hidden )
                return( false );

            HIT   l_eHit    = IsHit(p_iX, p_iY, out LOCUS l_eEdge );
            bool  l_fInside = false;

            if( l_eHit == HIT.CORNER ||
                l_eHit == HIT.EDGE   ||
                l_eHit == HIT.MIDPOINT )
                l_fInside = true;
            
            return( l_fInside );
        }

        /// <summary>
        /// This is the default way to start a drag operation.
        /// </summary>
        public SmartGrabDrag BeginDrag( int p_iX, int p_iY, SKPointI p_pntAspect, SmartRect p_rcViewBounds = null ) {
            HIT eHit = IsHit( p_iX, p_iY, out LOCUS l_eEdges );
            switch( eHit ) {
                case HIT.CORNER:
                case HIT.MIDPOINT:
                    return BeginAspectDrag( null, SET.STRETCH, eHit, l_eEdges, p_iX, p_iY, p_pntAspect, p_rcViewBounds );

                case HIT.INSIDE:
                    return new SmartGrabDrag( null, this, SET.RIGID, LOCUS.UPPERLEFT, p_iX, p_iY, p_rcViewBounds );
                case HIT.EDGE:
                    return new SmartGrabDrag( null, this, SET.RIGID, l_eEdges, p_iX, p_iY, p_rcViewBounds );
            }

            return null;
        }

		/// <summary>
		/// When dragging if it's a corner we might need to preserve aspect. This
		/// function can be overridden to provide a drag object that honors aspect.
        /// Note the HIT point has already been determined by the caller. This allows
        /// the user to overide the default hit that the points would produce
        /// by calling IsHit() on us.
		/// </summary>
		public virtual SmartGrabDrag BeginAspectDrag(
			DragFinished  p_oFinished,
            SET           p_eStretch,
            HIT           p_eHit,
            LOCUS         p_eEdges,
            int           p_iX, 
            int           p_iY,
            SKPoint       p_pntAspect,
            SmartRect     p_rcViewBounds = null
		) {
			return new SmartGrabDrag( p_oFinished, this, p_eStretch, p_eEdges, p_iX, p_iY, p_rcViewBounds );
		}

        public void HoverStop() {
            m_fHovering = false;
        }

        public bool HoverChanged(int p_iX, int p_iY)
        {
            bool l_fIsInside = IsInside(p_iX, p_iY);
            bool l_fChanged  = l_fIsInside != m_fHovering;

            if (l_fChanged) {
                m_fHovering = l_fIsInside;
            }

            return (l_fChanged);
        }

        public bool Hovering
        {
            get {
                return (m_fHovering);
            }
        }

        public override SmartRect Outer
        {
            get {
                return (m_rcOuter);
            }
        }

		public Color FocusColor {
			get {
				Color oFocusColor = Color.Empty;

                switch( m_eShowState ) {
                    case SHOWSTATE.Focused:
                        oFocusColor = SystemColors.Highlight;
                        break;
                    case SHOWSTATE.Active:
                        oFocusColor = Color.FromArgb( 255, 0xa0, 0xa0, 0xa0 );
                        break;
                    case SHOWSTATE.Inactive:
                        if( m_fHovering )
                            oFocusColor = Color.FromArgb( 255, 0xa0, 0xa0, 0xa0 );
                        else
                            oFocusColor = Color.FromArgb(255, 211, 211, 211); // Color.FromArgb( 000, 0x00, 0x55, 0xE5);
                        break;
                }

				return( oFocusColor );
			}
		}

        [Obsolete]public override void Paint(Graphics oGraphics) {
            Brush oEdgeBrush   = null;
            Brush oCornerBrush = null;

            if( Hidden )
                return;

            try {
                oCornerBrush = new SolidBrush(Color.White);
                oEdgeBrush   = new SolidBrush(FocusColor);

                // The main color of the border rect
                for (int i = 0; i < 4; ++i) {
                    Rectangle oRect = m_rgoHandles[i].Rect;

                    oGraphics.FillRectangle(oEdgeBrush, oRect);
                }

                if (Hovering) {
                    SCALAR[] l_rguiCorner = { SCALAR.LEFT   | SCALAR.TOP, 
                                              SCALAR.TOP    | SCALAR.RIGHT, 
                                              SCALAR.RIGHT  | SCALAR.BOTTOM, 
                                              SCALAR.BOTTOM | SCALAR.LEFT };

                    for (int i = 0; i < 4; ++i) {
                        if( m_frgMoveable[i] ) {
                            oGraphics.FillRectangle( oCornerBrush, _rgHandlesMiddle[i].Rect);
                        };
                        // If the and of the two values is the same then both
                        // bits are on and the value should be the same as the corner tested.
                        if( ( m_eMoveable & l_rguiCorner[i] ) == l_rguiCorner[i] ) {
                            oGraphics.FillRectangle( oCornerBrush, _rgHandlesCorner[i].Rect);
                        };
                    }
                }
            } catch( OverflowException ) {
            } finally {
                if (oCornerBrush != null)
                    oCornerBrush.Dispose();
                if( oEdgeBrush != null )
                    oEdgeBrush.Dispose();
            }
        }

        /// <summary>
        /// Enumerate all the grab handles so you can easily search them.
        /// </summary>
        /// <seealso cref="UpdateHandles">
        public IEnumerator<GrabHandle> GetEnumerator() {
            // If we change the initialization order of the rects, then this bit
            // of code will be all messed up. >_<;;
            LOCUS[] rgscMiddle   = { LOCUS.RIGHT, LOCUS.BOTTOM, LOCUS.LEFT, LOCUS.TOP };
            LOCUS[] l_rguiCorner = { LOCUS.UPPERRIGHT, 
                                     LOCUS.LOWERRIGHT, 
                                     LOCUS.LOWERLEFT, 
                                     LOCUS.UPPERLEFT };

            for (int i = 0; i < 4; ++i) {
                if( m_frgMoveable[i] ) {
                    yield return new GrabHandle( _rgHandlesMiddle[i], rgscMiddle[i] );
                };
                // If the and of the two values is the same then both
                // bits are on and the value should be the same as the corner tested.
                if( ( (LOCUS)m_eMoveable & l_rguiCorner[i] ) == l_rguiCorner[i] ) {
                    yield return new GrabHandle( _rgHandlesCorner[i], l_rguiCorner[i] );
                };
            }
        }

        IEnumerator IEnumerable.GetEnumerator() {
            return GetEnumerator();
        }
    } // class SmartGrab

	/// <summary>
	/// This a thin selection box tool. That also supports aspect ratio enforcement.
	/// </summary>
	public class SmartSelect : SmartGrab {
		public DragMode Mode { get; set; }

		public SmartSelect( ) : 
			base( new SmartRect(), 7, fLiveDrag:false, eMoveable:SCALAR.ALL ) 
		{
			Mode       = DragMode.FreeStyle;
            Invertable = false;
		}

        /// <summary>
        /// Oops, I forgot I overode this. I need to see if still necesssary. Probably not.
        /// </summary>
        /// <seealso cref="BeginAspectDrag"/>
		public override bool IsInside(int iX,int iY) {
			if( base.IsInside( iX, iY ) )
				return true;

			if( IsHit( iX, iX, out LOCUS eEdges ) == SmartGrab.HIT.INSIDE ) 
				return true;

			return false;
		}

        /// <summary>
        /// When Fixed Ratio is on, we must change our behavor for the "edge" and "middle" selector
        /// cases. Instead of sliding the edge, the whole rect moves. I probably could get the
        /// aspect to work in that case but I don't want to mess with it right now.
        /// </summary>
		public override SmartGrabDrag BeginAspectDrag(
			DragFinished  p_oFinished,
            SET           p_eStretch,
            HIT           p_eHit,
            LOCUS         p_eEdges,
            int           p_iX, 
            int           p_iY,
            SKPoint       p_pntAspect,
            SmartRect     p_rcViewBounds = null
		) {
            switch( p_eHit ) {
                case HIT.CORNER:
			        switch( Mode ) {
				        default:
				        case DragMode.FreeStyle:
					        return new SmartGrabDrag  ( p_oFinished, this, p_eStretch, p_eEdges, p_iX, p_iY, p_rcViewBounds );
				        case DragMode.FixedRatio:
					        return new SmartSelectDrag( p_oFinished, this, p_eStretch, p_eEdges, p_iX, p_iY, p_rcViewBounds, p_pntAspect );
			        }

                case HIT.MIDPOINT:
			        switch( Mode ) {
				        default:
				        case DragMode.FreeStyle:
					        return new SmartGrabDrag  ( p_oFinished, this, p_eStretch, p_eEdges, p_iX, p_iY, p_rcViewBounds );
				        case DragMode.FixedRatio:
					        return new SmartGrabDrag( p_oFinished, this, SET.RIGID, LOCUS.UPPERLEFT, p_iX, p_iY, p_rcViewBounds );
			        }
                case HIT.INSIDE:
                case HIT.EDGE:
                    return new SmartGrabDrag( p_oFinished, this, SET.RIGID, LOCUS.UPPERLEFT, p_iX, p_iY, p_rcViewBounds );
            }
            return null;
		}

		/// <summary>
		/// We draw our rectangle just outside of the selected area!!
		/// </summary>
		public override void Paint( Graphics oGraphics ) {
            if( Hidden )
                return;

            try {
                using( Brush oCornerBrush = new SolidBrush(FocusColor) ) {
					using( Brush oBrush = new HatchBrush( HatchStyle.DiagonalCross, 
						                       foreColor:Color.Black, 
											   backColor:Color.White ) ) {
						using( Pen oPen = new Pen( oBrush ) ) {
							SmartRect oBorder = new SmartRect( this );
							//oBorder  .SetScalar    ( SET.INCR, SCALAR.LEFT | SCALAR.TOP, 1 );
							oGraphics.DrawRectangle( oPen, oBorder.Rect);

							for (int i = 0; i < 4; ++i) {
								if( m_frgMoveable[i] ) {
									oGraphics.FillRectangle( oCornerBrush, _rgHandlesMiddle[i].Rect);
									oGraphics.FillRectangle( oCornerBrush, _rgHandlesCorner[i].Rect);
								}
							}
						}
					}
				}
			} catch( Exception oEx ) {
				Type[] rgErrors = { typeof( NullReferenceException ), 
									typeof( IndexOutOfRangeException ) };

				if( rgErrors.IsUnhandled( oEx ) )
					throw;
			}
		}

		public override void UpdateHandles() {
			base.UpdateHandles();
		}
	}

    public delegate void DragFinished( object oGuest, SKPointI pntLast );

    /// <summary>
    /// This object keeps track of the drag handle operations going on for the grab handle
    /// object. Technically we are a view on the guest document which looks like a smart
    /// rect to us. So it could also be a document but since the document coordinates typically
    /// aren't matched to the world space I don't recon that'll happen often.
    /// </summary>
    public class SmartGrabDrag : 
        SmartRect, 
        ISmartDrag
    {
        protected readonly SET          _eStretch        = SET.STRETCH;
        protected readonly LOCUS        _eEdges          = LOCUS.EMPTY;
        protected readonly SmartRect    _rcGuestOrigSize = new SmartRect(); // The dimensions that the guest started as.
        protected readonly SKPointI     _pntGuestStart;
        protected readonly SKPointI     _pntOffset;
		protected          SKPointI     _pntLastMove;
        protected readonly DragFinished _oFinished = null;
        protected readonly SmartRect    _rcViewBounds;
        protected readonly SmartRect    _rcTemp = new();

        public SmartGrabDrag(
            DragFinished  p_oFinished,
            SmartRect     p_oGuest,
            SET           p_eStretch,
            LOCUS         p_eEdges,
            int           p_iX, // Mouse pos at start of drag.
            int           p_iY,
            SmartRect     p_rctViewBounds )
        {
            Invertable            = false; // This is dependent on what you're doing reall. :-/
            Guest                 = p_oGuest ?? throw new ArgumentNullException("The guest may not be null");
            _eEdges               = p_eEdges;
            _eStretch             = p_eStretch;
            _rcGuestOrigSize.Copy = p_oGuest;
            this.Copy             = p_oGuest;
            _rcViewBounds         = p_rctViewBounds;

            // This is co-dependent on our implementation of Dragging on the guest. This means
            // we'll typically need subclasses to control the actual drag.
            _pntGuestStart = Guest.GetPoint( p_eEdges );
            _pntOffset     = new SKPointI( 0, 0 );

            if( ( p_eEdges & LOCUS.LEFT ) > 0 || ( p_eEdges & LOCUS.RIGHT ) > 0 ) {
                _pntOffset.X = _pntGuestStart.X - p_iX; 
            }
            if( ( p_eEdges & LOCUS.TOP ) > 0 || ( p_eEdges & LOCUS.BOTTOM ) > 0 ) {
                _pntOffset.Y = _pntGuestStart.Y - p_iY; 
            }

			_pntLastMove   = new SKPointI( p_iX, p_iY );

            _oFinished = p_oFinished;
        }

        public SmartRect Guest { get; }

        public override SmartRect Outer
        {
            get
            {
                return (Guest.Outer);
            }
        }

        /// <summary>
        /// this is the function we override if we want to change the drag behavior.
        /// </summary>
        protected virtual void SetPoint( int p_iX, int p_iY ) 
        {
            Guest.SetPoint(_eStretch, _eEdges, p_iX, p_iY);
        }

        /// <summary>
        /// Follow the tracking position (mouse) with this method.
        /// </summary>
        /// <param name="iX">New position X of tracking point.</param>
        /// <param name="iY">New position Y of tracking point.</param>
        /// <remarks>TODO: Would be nice if we stick to whatever side we bump
        /// into smoothly when the go past an edge.</remarks>
        /// <seealso cref="SmartGrab.BeginDrag"/>
        public virtual void Move(int iX, int iY)
        {
			_pntLastMove.X = iX;
			_pntLastMove.Y = iY;

            SKPointI pntTarget = new SKPointI(  iX + _pntOffset.X, iY + _pntOffset.Y );

            // This is going to move the guest unconstrained.
            if( _rcViewBounds == null ) {
                SetPoint( pntTarget.X, pntTarget.Y );
                return;
            }

            // Copy invertable prop else our temp might get inverted when it shouldn't!!
            _rcTemp.Copy       = Guest;
            _rcTemp.Invertable = Guest.Invertable;

            _rcTemp.SetPoint(_eStretch, _eEdges, pntTarget.X, pntTarget.Y);

            // Would love to unify this but haven't quite figured it out yet. >_<;;
            if( !_rcViewBounds.IsInside( _rcTemp ) ) {
                if( _eStretch == SET.RIGID ) {
                    // this is the case where we are moving the rectangle from
                    // the center. No stretching involved.
                    SKPointI pntLB = new( _rcViewBounds.Right  - _rcTemp.Width,
                                          _rcViewBounds.Bottom - _rcTemp.Height );
                    SmartRect rctBounds = new SmartRect( _rcViewBounds.Left,
                                                         _rcViewBounds.Top,
                                                         pntLB.X,
                                                         pntLB.Y );

                    if( _rcTemp.Top < rctBounds.Top )
                        _rcTemp.SetScalar(SET.RIGID, SCALAR.TOP, rctBounds.Top );
                    if( _rcTemp.Top > rctBounds.Bottom )
                        _rcTemp.SetScalar(SET.RIGID, SCALAR.TOP, rctBounds.Bottom );

                    if( _rcTemp.Left < rctBounds.Left )
                        _rcTemp.SetScalar(SET.RIGID, SCALAR.LEFT, rctBounds.Left );
                    if( _rcTemp.Left > rctBounds.Right )
                        _rcTemp.SetScalar(SET.RIGID, SCALAR.LEFT, rctBounds.Right );
                } else {
                    // ghis is the case where we are stretching a corner.
                    SKPointI pntMoving = _rcTemp.GetPoint( _eEdges );
                    SCALAR eY = (SCALAR)(_eEdges & ( LOCUS.TOP  | LOCUS.BOTTOM ));
                    SCALAR eX = (SCALAR)(_eEdges & ( LOCUS.LEFT | LOCUS.RIGHT  ));

                    if( eY != 0 ) {
                        if( pntMoving.Y < _rcViewBounds.Top )
                            _rcTemp.SetScalar(SET.STRETCH, eY, _rcViewBounds.Top );
                        if( pntMoving.Y > _rcViewBounds.Bottom )
                            _rcTemp.SetScalar(SET.STRETCH, eY, _rcViewBounds.Bottom );
                    }

                    if( eX != 0 ) {
                        if( pntMoving.X < _rcViewBounds.Left )
                            _rcTemp.SetScalar(SET.STRETCH, eX, _rcViewBounds.Left );
                        if( pntMoving.X > _rcViewBounds.Right )
                            _rcTemp.SetScalar(SET.STRETCH, eX, _rcViewBounds.Right );
                    }
                }
            }

            Guest.Copy = _rcTemp;
        }

        /// <summary>
        /// We are done moving. (Typically Mouse up) 
        /// </summary>
        public virtual void Dispose() {
			_oFinished?.Invoke( Guest, _pntLastMove );
		}
    } // class SmartGrabDrag

	public class SmartSelectDrag : SmartGrabDrag {
        float _flSlope;
        float _flIntercept;

		public SmartSelectDrag(
            DragFinished  p_oFinished,
            SmartRect     p_oGuest,
            SET           p_eStretch,
            LOCUS         p_eEdges,
            int           p_iX, 
            int           p_iY,
            SmartRect     p_rcViewBounds,
            SKPoint       p_pntAspect
            ) : base( p_oFinished, p_oGuest, p_eStretch, p_eEdges, p_iX, p_iY, p_rcViewBounds )
        {
		    SKPointI pntAnchorSide = new SKPointI( p_iX, p_iY ); // Shouldn't, need this default. But compiler is whining.
			LOCUS    eOppoEdge     = GetInvert( p_eEdges );

            // Avoid divide by zero problems.
            if( p_pntAspect.X == 0 ) {
                p_pntAspect.X = 1;
            }

			try {
				pntAnchorSide = p_oGuest.GetPoint( eOppoEdge );
			} catch( ArgumentOutOfRangeException ) {
				// If we get a meaningless eOppoEdge arg generated then we
                // try to pick something close.
				if( ( eOppoEdge & LOCUS.LEFT & LOCUS.TOP ) != 0 )
					pntAnchorSide = p_oGuest.GetPoint( LOCUS.UPPERLEFT );
				if( ( eOppoEdge & LOCUS.RIGHT & LOCUS.BOTTOM ) != 0 )
					pntAnchorSide = p_oGuest.GetPoint( LOCUS.LOWERRIGHT );
			}

            _flSlope = p_pntAspect.Y / (float)p_pntAspect.X;

            if( ( LOCUS.UPPERRIGHT == p_eEdges ) || ( LOCUS.LOWERLEFT  == p_eEdges ) )
                _flSlope = -_flSlope;

            _flIntercept = pntAnchorSide.Y - ( _flSlope * pntAnchorSide.X );
		}

		/// <summary>
		/// Finally support aspect draw.
		/// </summary>
        public override void Move(int p_iX, int p_iY) {
            p_iX += _pntOffset.X;
            p_iY += _pntOffset.Y;

            // Remember: our graphics is in quadrant IV (4) so we're upside down.
            // If our drag, pulls us past the bottom of the object, switch approach.
            if( p_iY > Bottom )
                p_iY = (int)( _flSlope * p_iX + _flIntercept );
            else
                p_iX = (int)( (p_iY - _flIntercept ) / _flSlope );

            if( _rcViewBounds == null ) {
                SetPoint( p_iX, p_iY );
                return;
            }

            // So we need to check the WHOLE rect when dragging from the CENTER
            // and moving the whole object instead of just an edge or corner!!
            _rcTemp.Copy = Guest;
            _rcTemp.SetPoint(_eStretch, _eEdges, p_iX, p_iY);

            if( _rcViewBounds.IsInside( _rcTemp ) ) {
                Guest.Copy = _rcTemp;
            }
        }
	}
}
