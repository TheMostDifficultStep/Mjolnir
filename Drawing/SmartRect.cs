using System;
using System.Drawing; // We'll be able to finally remove this when we update our dependencies to Skia.

using SkiaSharp;

namespace Play.Rectangles {
	public enum TRACK {
		VERT,
		HORIZ
	}

	public struct Extent {
		public int Start { get; set; }
		public int Stop  { get; set; }

		public Extent( int iStart, int iStop ) {
			Start = iStart;
			Stop  = iStop;
		}

		public override string ToString() {
			return Start.ToString() + " " + Stop.ToString();
		}

		public int Distance { get { return Stop - Start; } } // See SmartRect Width and Height!! +1 issue.
	}

    public interface ISmartRectSearch 
    {
        void OnQueryHit( SmartRect oHit );
    }

	/// <summary>
	/// Rectangle has a new size.
	/// </summary>
	/// <param name="oNewBounds">Pointer to the sized smart rectangle.</param>
    public delegate void RectSize( SmartRect oNewBounds );

    public enum SMARTSIZE
    {
        Normal,
        Minimized,
        Maxmized
    }

    [FlagsAttribute]
    public enum SCALAR : uint
    {
        LEFT   = 0x01, // 2^EDGE.LEFT
        TOP    = 0x02, // 2^EDGE.TOP
        RIGHT  = 0x04, // 2^EDGE.RIGHT
        BOTTOM = 0x08, // 2^EDGE.BOTTOM
        WIDTH  = 0x10,
        HEIGHT = 0x20,
        ALL    = LEFT | TOP | RIGHT | BOTTOM
    }
        
    [FlagsAttribute]
    public enum LOCUS : uint {
        EMPTY      = 0,
        LEFT       = SCALAR.LEFT,
        TOP        = SCALAR.TOP, 
        RIGHT      = SCALAR.RIGHT,
        BOTTOM     = SCALAR.BOTTOM,
        WIDTH      = SCALAR.WIDTH,
        HEIGHT     = SCALAR.HEIGHT,
        EXTENT     = 0x40,
        ORIGIN     = 0x80,
        CENTER     = 0x100,
        UPPERLEFT  = TOP    | LEFT,
        LOWERRIGHT = BOTTOM | RIGHT,
        UPPERRIGHT = TOP    | RIGHT,
        LOWERLEFT  = BOTTOM | LEFT,
        ALL        = LEFT | TOP | RIGHT | BOTTOM
    }

    public enum SET {
        STRETCH = 0x00, // simply set the edge, which stretches the whole rect.
        INCR    = 0x01, // increment by the value (+=)
        RIGID   = 0x02  // keep the extent the same.
    }

    /// <summary>
    /// We really want to be a class and not a struct since we're inheriting from this with
    /// objects we don't want to be struct.
    /// </summary>
    public class SmartRect {
        protected int[] m_rgiCur = new int[4];

        protected SMARTSIZE _eSizing = SMARTSIZE.Normal;

        public bool Invertable { get; set; } = true; // Allow inverted rects.

        public enum SIDE : int {
            LEFT   = 0,
            TOP    = 1,
            RIGHT  = 2,
            BOTTOM = 3
        }

        /// <summary>
        /// A little converter to get us from a int to a SCALAR.
        /// </summary>
        /// <param name="iSide">An integer to convert.</param>
        /// <returns>Scalar value of input integer (power of 2)</returns>
        public static SCALAR ToScalar( int iSide ) {
            return( (SCALAR)( 1 << iSide ) );
        }

        public SmartRect() {
            for (int i = 0; i < m_rgiCur.Length; ++i)
                m_rgiCur[i] = 0;
        }

        public SmartRect( int iLeft, int iTop, int iRight, int iBottom ) {
            m_rgiCur[(int)SIDE.LEFT]   = iLeft;
            m_rgiCur[(int)SIDE.TOP]    = iTop;
            m_rgiCur[(int)SIDE.RIGHT]  = iRight;
            m_rgiCur[(int)SIDE.BOTTOM] = iBottom;
        }

        public SmartRect(int p_iEdge) {
            for (int i = 0; i < m_rgiCur.Length; ++i )
            {
                m_rgiCur[i] = p_iEdge;
            }
        }

        public SmartRect(SmartRect p_rctCopy) :
            this(p_rctCopy.m_rgiCur)
        {
        }

        public SmartRect(int[] p_rglEdges) {
            if (p_rglEdges == null)
                throw (new ArgumentNullException("The edges pointer is null."));

            int iLen = p_rglEdges.Length;

            if (iLen < 4)
                throw (new ArgumentException("The array passed is not large enough!"));

            for (int i = 0; i < iLen; ++i)
                m_rgiCur[i] = p_rglEdges[i];
        }

        public SmartRect(LOCUS p_uiEdge, int p_iX, int p_iY, int p_iWidth, int p_iHeight) {
            m_rgiCur[(int)SIDE.LEFT]   = 0;
            m_rgiCur[(int)SIDE.TOP]    = 0;
            m_rgiCur[(int)SIDE.RIGHT]  = p_iWidth;
            m_rgiCur[(int)SIDE.BOTTOM] = p_iHeight;

            if (p_uiEdge == LOCUS.CENTER)
                SetRectCenter(p_iX, p_iY);
            else
                SetRectStretch( SET.RIGID, p_uiEdge, p_iX, p_iY);
        }

		/// <summary>
		/// Attempts to invert the LOCUS. UPPERLEFT to LOWERRIGHT for exmple.
		/// </summary>
		public static LOCUS GetInvert( LOCUS eEdges ) {
			LOCUS eOppoEdge = LOCUS.EMPTY;

			if( ( eEdges & LOCUS.LEFT ) != 0 )
				eOppoEdge |= LOCUS.RIGHT;
			if( ( eEdges & LOCUS.RIGHT ) != 0 )
				eOppoEdge |= LOCUS.LEFT;
			if( ( eEdges & LOCUS.TOP ) != 0 )
				eOppoEdge |= LOCUS.BOTTOM;
			if( ( eEdges & LOCUS.BOTTOM ) != 0 )
				eOppoEdge |= LOCUS.TOP;

			return eOppoEdge;
		}

		/// <exception cref="ArgumentOutOfRangeException" />
		public float GetSlope( LOCUS eThisEdge ) {
			SKPointI pntThisSide = GetPoint( eThisEdge );
			SKPointI pntThatSide = GetPoint( GetInvert( eThisEdge ) );
			SKPointI pntRatio    = new SKPointI( pntThisSide.X - pntThatSide.X, pntThisSide.Y - pntThatSide.Y );
			float    flSlope     = pntRatio.Y / (float)pntRatio.X;

			return( flSlope );
		}

		/// <exception cref="ArgumentOutOfRangeException" />
		public float GetIntercept( float flSlope, LOCUS eThisEdge ) {
			SKPointI pntLine = GetPoint( eThisEdge );
			return pntLine.Y - ( pntLine.X * flSlope );
		}

        public SMARTSIZE Sizing
        {
            get { return (_eSizing); }
            set { _eSizing = value; }
        }

		/// <remarks>I've been slowly tweaking the behaver from it's
        /// cruddy first implentation to a better design. At last
        /// OnSize does NOT initiate the event invoke. </remarks>
        public virtual void Raise_OnSize(SmartRect p_rctOld)
        {
			OnSize();
            SizeEvent?.Invoke(this);
        }

        public event RectSize SizeEvent;

        protected virtual void OnSize()
        {
		}

        [Obsolete]public virtual void Paint( Graphics p_oGraphics )
        {
        }

        public virtual void Paint( SKCanvas skCanvas )
        {
        }

        /// <summary>
        /// Recenter the rectangle to the given position.
        /// </summary>
        /// <param name="p_lSetX"></param>
        /// <param name="p_lSetY"></param>
        private void SetRectCenter(
            int p_lSetX,
            int p_lSetY)
        {
            SKPointI pntExtent = this.GetPoint(LOCUS.EXTENT);

            p_lSetX -= ( pntExtent.X >> 1 );
            p_lSetY -= ( pntExtent.Y >> 1 );

            SetRectStretch( SET.RIGID, LOCUS.UPPERLEFT, p_lSetX, p_lSetY);
        }

        // This function is a balance of form and function. I could make it take an array
        // of four points, or a SmartRect for that matter and then set accordingly. HOWEVER,
        // I expect we'll typically want to the the following things. Inflate on all 4, 2 or
        // 1 side based on x and y are the same. Or stretch/set one of the corners. 
        // DO NOT CALL OnUpdate() in this function.
        private void SetRectStretch(
            SET   p_uiStretch,
            LOCUS p_uiEdges,
            int   p_lSetX,
            int   p_lSetY)
        {
            int[] rgAdjust = { p_lSetX, p_lSetY }; // 0 is the X coord, 1 is the Y coord.
            int[] rgSign   = { -1, -1, 1, 1 };     // left, top, right, bottom ^_^;
            uint  uiScalar = 1;
            bool  fRigid   = (p_uiStretch & SET.RIGID) != 0;
            bool  fIncr    = (p_uiStretch & SET.INCR)  != 0;

            for (uint iEdgeCheck = 0; iEdgeCheck < 4; ++iEdgeCheck) {
                // Only check the edge being changed.
                if (((LOCUS)uiScalar & p_uiEdges) != 0) {
                    uint iAdIdx  = (iEdgeCheck % 2);     // Mod 2 of any edge, see l_rglAdjust
                    uint iEdgeO  = (iEdgeCheck + 2) % 4; // Compute the opposite edge.
                    int  iEdgeChkVal;                    // Edge we're starting with...

                    if( fIncr)
                        iEdgeChkVal = m_rgiCur[iEdgeCheck] + rgAdjust[iAdIdx] * rgSign[iEdgeCheck];
                    else
                        iEdgeChkVal =  rgAdjust[iAdIdx];

                    // Prevent the rect from going inside out!! NOTE: Had a bug, keep an eye out...
                    // If a rigid move there's no way to invert unless rect is already inverted.
                    if( fRigid ) {
                        int lExtent = m_rgiCur[iEdgeO] - m_rgiCur[iEdgeCheck];
                        m_rgiCur[iEdgeCheck] = iEdgeChkVal;
                        m_rgiCur[iEdgeO    ] = iEdgeChkVal + lExtent;
                    } else {
                        // Note! One side might attempt to invert and won't get set while
                        //       it's partner (x vs y) might be moved successfully!!
                        //       This is typically desirable for bmp selection operations!
                        if( !Invertable ) {
                            if( ( iEdgeChkVal - m_rgiCur[iEdgeO] ) * rgSign[iEdgeCheck] >= 0 )
                                m_rgiCur[iEdgeCheck] = iEdgeChkVal;
                        } else {
                            m_rgiCur[iEdgeCheck] = iEdgeChkVal;
                        }
                    }
                }
                uiScalar = uiScalar << 1;
            }
        } // End SetRectStretch

        private void SetRectExtent(
            LOCUS p_uiEdges,
            int   p_lSetX, // Extent in the X
            int   p_lSetY) // Extent in the Y
        {
            int[] l_rglAdjust = { p_lSetX, p_lSetY };
            int[] l_rgiSign   = { 1, 1, -1, -1 }; // This depends on coordinate system.
            uint  l_uiScalar  = 1;

            if (((int)p_uiEdges & 0xF) == 0)
                throw new ArgumentOutOfRangeException("Extent must be relative to an edge");

            for (uint iEdge4 = 0; iEdge4 < 4; ++iEdge4)
            {
                if (((LOCUS)l_uiScalar & p_uiEdges) != 0)
                {
                    uint iEdge2 = (iEdge4 % 2);     // Mod 2 of any edge, see l_rglAdjust
                    uint iEdgeO = (iEdge4 + 2) % 4; // Compute the opposite edge.

                    m_rgiCur[iEdgeO] = m_rgiCur[iEdge4] + l_rglAdjust[iEdge2] * l_rgiSign[iEdge4];
                }
                l_uiScalar = l_uiScalar << 1;
            }
        } // End SetRectExtent

        public void SetRect(LOCUS p_uiEdge, int p_lX, int p_lY, int p_lWidth, int p_lHeight)
        {
            SmartRect rctOld = new SmartRect(m_rgiCur);

            m_rgiCur[(int)SIDE.LEFT] = 0;
            m_rgiCur[(int)SIDE.TOP] = 0;
            m_rgiCur[(int)SIDE.RIGHT] = p_lWidth;
            m_rgiCur[(int)SIDE.BOTTOM] = p_lHeight;

            if (p_uiEdge == LOCUS.CENTER)
                SetRectCenter(p_lX, p_lY);
            else
                SetRectStretch( SET.RIGID, p_uiEdge, p_lX, p_lY);

            Raise_OnSize(rctOld);
        }

        public void SetRect( int iLeft, int iTop, int iRight, int iBottom )
        {
            SmartRect rctOld = new SmartRect(m_rgiCur);

            m_rgiCur[(int)SIDE.LEFT]   = iLeft;
            m_rgiCur[(int)SIDE.TOP]    = iTop;
            m_rgiCur[(int)SIDE.RIGHT]  = iRight;
            m_rgiCur[(int)SIDE.BOTTOM] = iBottom;

            Raise_OnSize(rctOld);
        }

        public void CopyFrom( SKRectI rctSource ) {
            SmartRect rctOld = new SmartRect(m_rgiCur);

            m_rgiCur[(int)SIDE.LEFT]   = rctSource.Left;
            m_rgiCur[(int)SIDE.TOP]    = rctSource.Top;
            m_rgiCur[(int)SIDE.RIGHT]  = rctSource.Right;
            m_rgiCur[(int)SIDE.BOTTOM] = rctSource.Bottom;

            Raise_OnSize(rctOld);
        }

        public SmartRect Copy {
            get {
                return (new SmartRect(m_rgiCur));
            }
            set {
                // Save our old dimensions first...
                SmartRect rctOld = new SmartRect(m_rgiCur);

                // Copy into us.
                for (int i = 0; i < 4; ++i) {
                    m_rgiCur[i] = value.m_rgiCur[i];
                }

                Raise_OnSize(rctOld);
            }
        }

        /// <summary>
        /// This will mostly go away when we update all our graphics to the Skia API.
        /// </summary>
        public Rectangle Rect
        {
            get {
                return new Rectangle( Left, Top, Width, Height );
            }
            set {
                SmartRect rctOld = new SmartRect(m_rgiCur);

                m_rgiCur[(int)SIDE.LEFT]   = value.Left;
                m_rgiCur[(int)SIDE.RIGHT]  = value.Right;
                m_rgiCur[(int)SIDE.TOP]    = value.Top;
                m_rgiCur[(int)SIDE.BOTTOM] = value.Bottom;

                Raise_OnSize(rctOld);
            }
        }

        public SKRectI SKRect {
            get {
                return new SKRectI( Left, Top, Right, Bottom );
            }
            set {
                SmartRect rctOld = new SmartRect(m_rgiCur);

                m_rgiCur[(int)SIDE.LEFT]   = value.Left;
                m_rgiCur[(int)SIDE.RIGHT]  = value.Right;
                m_rgiCur[(int)SIDE.TOP]    = value.Top;
                m_rgiCur[(int)SIDE.BOTTOM] = value.Bottom;

                Raise_OnSize(rctOld);
            }
        }

        public SKSizeI SKSize {
            get {
                return new SKSizeI( Width, Height );
            }
        }

        public SKPointI GetCenter() {
            SKPointI pntReturn = new SKPointI( 0, 0 );

            pntReturn.X = m_rgiCur[(int)SIDE.RIGHT] - m_rgiCur[(int)SIDE.LEFT];
            pntReturn.X = m_rgiCur[(int)SIDE.LEFT] + (pntReturn.X >> 1);

            pntReturn.Y = m_rgiCur[(int)SIDE.BOTTOM] - m_rgiCur[(int)SIDE.TOP];
            pntReturn.Y = m_rgiCur[(int)SIDE.TOP] + (pntReturn.Y >> 1);

            return pntReturn;
        }

		/// <exception cref="ArgumentOutOfRangeException" />
        public SKPointI GetPoint( LOCUS p_uiEdges )
        {
            SKPointI pntReturn = new SKPointI( 0, 0 );
            // Of course I want the origin to be whereever but for now
            // we'll just make it the upper left.
            if (p_uiEdges == LOCUS.ORIGIN)
                p_uiEdges = LOCUS.UPPERLEFT;

            switch (p_uiEdges)
            {
                case LOCUS.CENTER:
                    pntReturn = GetCenter();
                    break;
                case LOCUS.EXTENT:
                    pntReturn.X = m_rgiCur[(int)SIDE.RIGHT] - m_rgiCur[(int)SIDE.LEFT];
                    pntReturn.Y = m_rgiCur[(int)SIDE.BOTTOM] - m_rgiCur[(int)SIDE.TOP];
                    break;

                case LOCUS.LEFT:
                case LOCUS.TOP:
                case LOCUS.RIGHT:
                case LOCUS.BOTTOM:
                case LOCUS.UPPERLEFT:
                case LOCUS.LOWERRIGHT:
                case LOCUS.UPPERRIGHT:
                case LOCUS.LOWERLEFT:
                    //pntReturn = GetCenter();

                    if ((p_uiEdges & LOCUS.LEFT) != 0)
                        pntReturn.X = m_rgiCur[(int)SIDE.LEFT];
                    if ((p_uiEdges & LOCUS.TOP) != 0)
                        pntReturn.Y = m_rgiCur[(int)SIDE.TOP];
                    if ((p_uiEdges & LOCUS.RIGHT) != 0)
                        pntReturn.X = m_rgiCur[(int)SIDE.RIGHT];
                    if ((p_uiEdges & LOCUS.BOTTOM) != 0)
                        pntReturn.Y = m_rgiCur[(int)SIDE.BOTTOM];
                    break;

                default:
                    throw (new ArgumentOutOfRangeException("p_uiEdges", p_uiEdges, "Unrecognized combination of flags"));
            }

            return pntReturn;
        }

        public void SetPoint(
            SET   p_eStretch,
            LOCUS p_uiEdges,
            int   p_iX,
            int   p_iY )
        {
            SmartRect rctOld = new SmartRect(m_rgiCur);

            if( ( p_uiEdges & LOCUS.EXTENT ) != 0 )
            {
                SetRectExtent(p_uiEdges, p_iX, p_iY);
            }
            else if( p_uiEdges == LOCUS.CENTER )
            {
                int l_dwExtentX = GetScalar( SCALAR.WIDTH ),
                    l_dwExtentY = GetScalar( SCALAR.HEIGHT );

                // Quick divide by two.
                p_iX -= (l_dwExtentX >> 1);
                p_iY -= (l_dwExtentY >> 1);

                SetRect(LOCUS.UPPERLEFT, p_iX, p_iY, l_dwExtentX, l_dwExtentY);
            }
            else
                SetRectStretch( p_eStretch, p_uiEdges, p_iX, p_iY);

            Raise_OnSize(rctOld);
        } // SetPoint

        /// <summary>
        /// Sides let us access the rect with indicies: 0, 1, 2, 3.
        /// </summary>
        /// <param name="p_iSide"></param>
        /// <returns></returns>
        public int GetSide( int p_iSide ) {
            if( p_iSide < 0 )
                throw new ArgumentOutOfRangeException( "Value must not be less than zero" );
            if( p_iSide > m_rgiCur.Length )
                throw new ArgumentOutOfRangeException( "Value must not be greater than " + m_rgiCur.Length.ToString() );

            return( m_rgiCur[p_iSide] );
        }

        public int this[SCALAR eIndex] {
            get { return( GetScalar( eIndex ) ); }
        }

        public int this[int iIndex] {
            get { return( GetSide( iIndex ) ); }
            set {
                if( iIndex < 0 )
                    throw new ArgumentOutOfRangeException("Value must not be less than zero");
                if( iIndex > m_rgiCur.Length )
                    throw new ArgumentOutOfRangeException("Value must not be greater than " + m_rgiCur.Length.ToString());

                m_rgiCur[iIndex] = value; 
            }
        }

        public int Left {
            get { return( m_rgiCur[(int)SIDE.LEFT] ); }
			set { SetScalar( SET.STRETCH, SCALAR.LEFT, value ); }
        }

        public int Top {
            get { return( m_rgiCur[(int)SIDE.TOP] ); }
			set { SetScalar( SET.STRETCH, SCALAR.TOP, value ); }
        }

        public int Right {
            get { return( m_rgiCur[(int)SIDE.RIGHT] ); }
			set { SetScalar( SET.STRETCH, SCALAR.RIGHT, value ); }
        }

        public int Bottom {
            get { return( m_rgiCur[(int)SIDE.BOTTOM] ); }
			set { SetScalar( SET.STRETCH, SCALAR.BOTTOM, value ); }
        }

        public int Width {
            get { 
                return (m_rgiCur[(int)SIDE.RIGHT] - m_rgiCur[(int)SIDE.LEFT] ); // BUG: got problem with +1
            }
			set {
				SetScalar( SET.STRETCH, SCALAR.RIGHT, this.Left + value );
			}
        }

        public int Height {
            get {
                return (m_rgiCur[(int)SIDE.BOTTOM] - m_rgiCur[(int)SIDE.TOP] );
            }
			set {
				//SetScalar( SET.STRETCH, SCALAR.TOP, this.Bottom + value );
				SetScalar( SET.STRETCH, SCALAR.BOTTOM, this.Top + value );
			}
        }

        /// <summary>
        /// The rail is the distance between the tracks, perpendicular to the 
        /// direction of travel.
        /// </summary>
        /// <param name="eAxis"></param>
        /// <returns></returns>
		public Extent GetRail( TRACK eAxis ) {
			switch( eAxis ) {
				case TRACK.HORIZ:
					return new Extent( Top, Bottom );
				case TRACK.VERT:
					return new Extent( Left, Right );
			}

			throw new InvalidOperationException( "Weird");
		}

		/// <exception cref="InvalidOperationException" />
		public Extent GetTrack( TRACK eAxis ) {
			switch( eAxis ) {
				case TRACK.HORIZ:
					if( Width < 0 )
						throw new InvalidOperationException( "Track Width cannot be negative." );
					return new Extent( Left, Right );
				case TRACK.VERT:
					if( Height < 0 )
						throw new InvalidOperationException( "Track height cannot be negative." );

					return new Extent( Top, Bottom );
			}

			throw new InvalidOperationException( "Weird");
		}

		public int GetExtent( TRACK eAxis ) {
			switch( eAxis ) {
				case TRACK.HORIZ:
					return Width;
				case TRACK.VERT:
					return Height;
			}

			throw new InvalidOperationException( "Weird");
		}

        /// <summary>
        /// Scalar's let us access the rectangle with flags 1, 2, 4, 8
        /// </summary>
        /// <param name="p_uiEdges"></param>
        /// <returns></returns>
        public int GetScalar( SCALAR p_uiEdges)
        {
            int  lRet     = 0;
            int  lCount   = 0;
            uint uiScalar = 1;

            // These probably s/b absolute value's
            if (p_uiEdges == SCALAR.WIDTH)
                return ( this.Width );
            if (p_uiEdges == SCALAR.HEIGHT)
                return ( this.Height );

            // This is slow I could do better.
            for (int i = 0; i < 4; ++i)
            {
                if ((p_uiEdges & (SCALAR)uiScalar) != 0)
                {
                    lRet = m_rgiCur[i];
                    ++lCount;
                }
                uiScalar = uiScalar << 1;
            }

            if (lCount > 1)
                throw (new ArgumentOutOfRangeException("p_uiEdges", p_uiEdges, "invalid value for GetScalar"));

            return (lRet);
        }

        public void SetScalar(
            SET    p_eStretch,
            SCALAR p_uiEdges,
            int    p_lScalar )
        {
            SmartRect rctOld = new SmartRect(m_rgiCur);

            SetRectStretch( p_eStretch, (LOCUS)p_uiEdges, p_lScalar, p_lScalar);

            // Send the old rect in the message. The new rect can be retrieved from the resulting rect!
            Raise_OnSize(rctOld);
        }

        /// <summary>
        /// Check if the edge specified is the same for given rect
        /// </summary>
        /// <param name="p_uiEdges"></param>
        /// <param name="p_oComp"></param>
        /// <returns></returns>
        public bool IsEqual(SCALAR p_uiEdges, SmartRect p_oComp)
        {
            bool fRet = true;
            bool[] rgfCheck = new bool[4];
            uint uiCurEdge = 1;

            for (uint i = 0; i < 4; ++i)
            {
                if ((p_uiEdges & (SCALAR)uiCurEdge) != 0)
                    fRet &= p_oComp.GetScalar((SCALAR)uiCurEdge) == m_rgiCur[i];
                uiCurEdge = uiCurEdge << 1;
            }

            return (fRet);
        }

        public virtual void SearchChildren(int p_iX, int p_iY, ISmartRectSearch p_oSearch )
        {
            if (IsInside(p_iX, p_iY))
                p_oSearch.OnQueryHit( this );
        }

        /// <summary>
        /// Determine if the position is inside the rect.
        /// </summary>
        /// <returns>We've got a little problem brewing here. Since at present
        /// all the corners of the rect are INSIDE. A rect of 0, 0, 0, 0 will return
        /// a hit for x=0, y=0. If the rect is empty the width/height must be -1!</returns>
        public virtual bool IsInside(int p_iX, int p_iY)
        {
            // This is really bad, we are completely coordinate system
            // dependent. Need to fix that.
            if (p_iX >= m_rgiCur[(int)SIDE.LEFT] &&
                p_iX <  m_rgiCur[(int)SIDE.RIGHT] &&
                p_iY >= m_rgiCur[(int)SIDE.TOP] &&
                p_iY <  m_rgiCur[(int)SIDE.BOTTOM])
                return (true);

            return( false );
        }

        // If any side outside of theother then not Intersecting.
        public bool IsIntersecting( SmartRect oOther ) {
            if( oOther.GetScalar(SCALAR.LEFT )  > m_rgiCur[(int)SIDE.RIGHT]  ||
                oOther.GetScalar(SCALAR.RIGHT)  < m_rgiCur[(int)SIDE.LEFT ]  ||
                oOther.GetScalar(SCALAR.TOP)    > m_rgiCur[(int)SIDE.BOTTOM] ||
                oOther.GetScalar(SCALAR.BOTTOM) < m_rgiCur[(int)SIDE.TOP ] )
                return( false );

            return( true );
        }

        public bool IsInside( SmartRect p_oTarget )
        {
            // This is really bad, we are completely coordinate system
            // dependent. Need to fix that.
            if (p_oTarget.GetScalar(SCALAR.LEFT)   >= m_rgiCur[(int)SIDE.LEFT] &&
                p_oTarget.GetScalar(SCALAR.RIGHT)  <  m_rgiCur[(int)SIDE.RIGHT] &&
                p_oTarget.GetScalar(SCALAR.TOP)    >= m_rgiCur[(int)SIDE.TOP] &&
                p_oTarget.GetScalar(SCALAR.BOTTOM) <  m_rgiCur[(int)SIDE.BOTTOM])
                return (true);

            return (false);
        }

        /// <summary>
        /// we are completely coordinate system dependent. Need to fix that.
        /// </summary>
        /// <param name="p_iX"></param>
        /// <param name="p_iY"></param>
        /// <returns></returns>
        public LOCUS IsWhere( int p_iX, int p_iY ) {
            LOCUS eLocus = LOCUS.EMPTY;

            if( IsInside(p_iX, p_iY) )
                return ( LOCUS.CENTER );

            if( p_iX < m_rgiCur[(int)SIDE.LEFT] )
                eLocus |= LOCUS.LEFT;
            if( p_iX > m_rgiCur[(int)SIDE.RIGHT] )
                eLocus |= LOCUS.RIGHT;
            if( p_iY < m_rgiCur[(int)SIDE.TOP] )
                eLocus |= LOCUS.TOP;
            if( p_iY > m_rgiCur[(int)SIDE.BOTTOM] )
                eLocus |= LOCUS.BOTTOM;

            return ( eLocus );
        }

        public virtual void Intersect(SmartRect rctOne, SmartRect rctTwo)
        {
            SKRectI skRect = rctTwo.SKRect;
            skRect.Intersect( rctOne.SKRect );
            SKRect = skRect;
        }

        // Another coordinate system dependent call that I'm hacking.
        public virtual void Union(SmartRect p_oOther)
        {
            if (m_rgiCur[(int)SIDE.LEFT] > p_oOther.m_rgiCur[(int)SIDE.LEFT])
                m_rgiCur[(int)SIDE.LEFT] = p_oOther.m_rgiCur[(int)SIDE.LEFT];
            if (m_rgiCur[(int)SIDE.TOP] > p_oOther.m_rgiCur[(int)SIDE.TOP])
                m_rgiCur[(int)SIDE.TOP] = p_oOther.m_rgiCur[(int)SIDE.TOP];
            if (m_rgiCur[(int)SIDE.RIGHT] < p_oOther.m_rgiCur[(int)SIDE.RIGHT])
                m_rgiCur[(int)SIDE.RIGHT] = p_oOther.m_rgiCur[(int)SIDE.RIGHT];
            if (m_rgiCur[(int)SIDE.BOTTOM] < p_oOther.m_rgiCur[(int)SIDE.BOTTOM])
                m_rgiCur[(int)SIDE.BOTTOM] = p_oOther.m_rgiCur[(int)SIDE.BOTTOM];
        }

        public virtual void Inflate( int p_iMultiplyer, SmartRect p_oIncr ) {
            int[] rgiSides = { -1, -1, 1, 1 }; // Coordinate system dependent.

            for( int i = 0; i < 4; ++i ) {
                m_rgiCur[i] += p_iMultiplyer * rgiSides[i] * p_oIncr.m_rgiCur[i];
            }
        }

        public void Inflate( int p_iMultiplyer, int[] rgIncr ) {
            int[] rgiSides = { -1, -1, 1, 1 }; // Coordinate system dependent.

            for( int i = 0; i < 4; ++i ) {
                m_rgiCur[i] += p_iMultiplyer * rgiSides[i] * rgIncr[i];
            }
        }

        public virtual void Inflate( int p_iMultiplyer, int iSize ) {
            int[] rgiSides = { -1, -1, 1, 1 }; // Coordinate system dependent.

            for (int i = 0; i < 4; ++i)
            {
                m_rgiCur[i] += p_iMultiplyer * rgiSides[i] * iSize;
            }
        }

        public virtual SmartRect Outer
        {
            get
            {
                return( this );
            }
        }

        /// <summary>
        /// Make the rectangl zero Extent.
        /// </summary>
        /// <param name="p_uiEdge">The rect corners are set to this edge.</param>
        public virtual void Empty(LOCUS p_uiEdge)
        {
            SKPointI oCorner = GetPoint( p_uiEdge );
            LOCUS uiOppo  = ( ~p_uiEdge ) & (LOCUS)0xF;

            SetPoint(SET.STRETCH, uiOppo, oCorner.X, oCorner.Y);            
        }

        public virtual bool IsEmpty()
        {
            SKPointI oExtent = GetPoint(LOCUS.EXTENT);

            return(oExtent.X == 0 || oExtent.Y == 0) ;
        }

        public override string ToString()
        {
            string strValue = "";

            strValue += "(";
            for (int i = 0; i < m_rgiCur.Length; ++i)
            {
                strValue += m_rgiCur[i].ToString();
                strValue += ",";
            }
            strValue += ")";

            return (strValue);
        }

		public virtual bool LayoutChildren() {
			return true;
		}

		/// <summary>
		/// Little helper function. Given an aspect ratio determine the height
		/// given a fixed width or vice versa.
		/// </summary>
		/// <param name="flAspectWH">Width divided by Height.</param>
		/// <param name="iFixed">the size of the fixed dimension.</param>
		/// <param name="eDir">The fixed dimension.</param>
		/// <returns></returns>
		public static uint ExtentDesired( float flAspectWH, uint uiFixed, TRACK eDir ) {
			switch( eDir ) {
				case TRACK.HORIZ:
					return (uint)(uiFixed * flAspectWH);

				case TRACK.VERT:
					return (uint)(uiFixed / flAspectWH );
			}
			
			throw new ArgumentException( "Can only handle width or height" );
		}
    } // class SmartRect

}
