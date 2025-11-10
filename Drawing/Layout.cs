using System;
using System.Collections.Generic;
using System.Linq;
using System.Drawing;
using System.Collections;

using SkiaSharp;

namespace Play.Rectangles {

	/// <summary>
	/// This used to be in the Play.Edit name space. But it's more 
	/// convenient here so we can specify what kind of justify we
	/// want for text inside a Layout... This enum also lives on the
	/// FTCacheLine. Maybe I can move to the embedding namespace?
	/// </summary>
	public enum Align {
        Left,
        Center,
        Right
    }

	public class LayoutRect : SmartRect {
		public CSS       Style   { get; set; }
		public SmartRect Padding { get; } = new SmartRect();
		public Align     Justify { get; set; } = Align.Left;

		public enum CSS {
			Percent,
			Pixels,
			Flex,
			None,  // None takes up remaining track.
			Hidden // Hidden takes up no track and s/b is hidden.
		}

		public LayoutRect( ) : base() { 
			Style           = CSS.None; 
			Track           = 0;
			TrackMaxPercent = 1;
		}
		public LayoutRect( CSS eLayout ) : base() { 
			Style        = eLayout;
			Track           = 0;
			TrackMaxPercent = 1;
		}

		public LayoutRect(CSS eLayout, uint uiTrack, float flMaxPercent) : base() {
			Style           = eLayout;
			Track           = uiTrack;
			TrackMaxPercent = flMaxPercent;
		}

		public CSS   Units { get { return Hidden ? CSS.Hidden : Style; } set { Style = value; } }
		public uint  Track { get; set; } // TODO: Make this a float.
		public float TrackMaxPercent { get; set; } // TODO : Use minmax object.
		public int   Span  { get; set; } = 0; // CSS span value minus 1. Bummer here but shared with SmartTable.
		public virtual bool  Hidden { get; set; } = false;
		public CSS   Layout { get => Style; set { Style = value; } }

		public virtual uint TrackDesired( TRACK eParentAxis, int uiRail ) { return Track; }
		public virtual void Invalidate() { }
        public virtual void PaintBackground( SKCanvas skCanvas ) { }
	}

    public abstract class ParentRect : 
		LayoutRect,
		IEnumerable<LayoutRect>
	{
		public uint Spacing { get; set; } = 0; 

		public ParentRect( ) : base( CSS.None ) {
		}

		protected ParentRect( CSS eUnits, uint uiTrack, float flMaxPercent ) : 
			base( eUnits, uiTrack, flMaxPercent ) 
		{
		}

		public abstract void                    Clear();
		public abstract int                     Count { get; }
		public abstract LayoutRect              Item( int iIndex );
		public abstract IEnumerator<LayoutRect> GetEnumerator();

		IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

		[Obsolete] public override void Paint(Graphics oGraphics) {
			foreach( LayoutRect oRect in this ) {
				if( !oRect.Hidden ) {
					oRect.Paint( oGraphics );
				}
			}
		}

		public override void Paint( SKCanvas skCanvas) {
			foreach( LayoutRect oRect in this ) {
				if( !oRect.Hidden ) {
					oRect.Paint( skCanvas );
				}
			}
		}

		public override bool Hidden { 
			set {
				base.Hidden = value;
				foreach( LayoutRect oRect in this ) {
					oRect.Hidden = value;
				}
			}
		}

	}

	/// <summary>
	/// So it turns out you can stick a LayoutControl directly as the top level
	/// layout and get it to size properly. Even tho the LayoutChildren() event
	/// on that object is not implemented. This is because the LayoutControl
	/// uses the SizeEvent on the SmartRect to re-size itself.
	/// </summary>
    [Obsolete]public class LayoutSingle : LayoutRect {
		SmartRect _oSolo;
        public LayoutSingle(CSS eLayout, SmartRect oSolo ) : base( eLayout ) {
			_oSolo = oSolo ?? throw new ArgumentNullException( nameof( oSolo ) );
        }

		public override bool LayoutChildren() {
			_oSolo.Copy = this;
			_oSolo.LayoutChildren();

			return true;
		}
        public override void Paint(Graphics p_oGraphics) {
            _oSolo.Paint(p_oGraphics);
        }
    }

	/// <summary>
	/// Simple wrapper so we can get grab handle inside of a layout.
	/// </summary>
    public class LayoutGrab : LayoutRect {
		SmartGrab _oSolo;
		SmartRect _oTemp = new SmartRect();
        public LayoutGrab(CSS eLayout, SmartGrab oSolo ) : base( eLayout ) {
			_oSolo = oSolo ?? throw new ArgumentNullException( nameof( oSolo ) );
        }

		public override bool LayoutChildren() {
			_oTemp.Copy = this;
			_oTemp.Inflate( -1, _oSolo.BorderWidth );

			_oSolo.Copy = _oTemp;
			_oSolo.LayoutChildren();

			return true;
		}

        public override void Paint(Graphics p_oGraphics) {
            _oSolo.Paint(p_oGraphics);
        }

        public override bool Hidden { 
			get => _oSolo.Hidden;
			set => _oSolo.Hidden = value; 
		}
    }

	/// <summary>
	/// Expermental drag object for layout objects.
	/// </summary>
    public class SmartDragLayout : SmartGrabDrag {
		LOCUS      _eEdgeOppo;
		LayoutRect _oLayout;
		TRACK      _eDir;
		Action< bool, SKPointI> _oCallback;
        public SmartDragLayout( Action< bool, SKPointI> oCallback, LayoutRect oGuest, TRACK eDir, LOCUS p_eEdges, int p_iX, int p_iY) : 
			base( null, oGuest, SET.STRETCH, p_eEdges, p_iX, p_iY, null )
		{
			_oCallback = oCallback ?? throw new ArgumentNullException( nameof( oCallback ) );
			_eEdgeOppo = SmartRect.GetInvert( p_eEdges );
			_oLayout   = oGuest;
			_eDir      = eDir;
        }

        public override void Dispose() {
			_oCallback( false, new SKPointI( 0, 0 ) );
		}

		protected static int HighPass( int iValue ) {
			if( iValue < 0 )
				return 0;

			return iValue;
		}

        protected override void SetPoint(int p_iX, int p_iY) {
			SKPointI pntOppo  = Guest.GetPoint( _eEdgeOppo );

			if( _eDir == TRACK.HORIZ ) {
				if( ( _eEdgeOppo & LOCUS.LEFT ) > 0 )
					_oLayout.Track = (uint)HighPass( p_iX - pntOppo.X );
				else
					_oLayout.Track = (uint)HighPass( pntOppo.X - p_iX );
			}
			if( _eDir == TRACK.VERT ) {
				if( ( _eEdgeOppo & LOCUS.TOP ) > 0 )
					_oLayout.Track = (uint)HighPass( p_iY - pntOppo.Y );
				else
					_oLayout.Track = (uint)HighPass( pntOppo.Y - p_iY );
			}

			bool fHide = _oLayout.Track == 0 ? true : false;

			if( _oLayout.Hidden != fHide )
				_oLayout.Hidden = fHide;
        }

        public override void Move(int iX, int iY) {
            base.Move(iX, iY);
			_oCallback( true, new SKPointI( iX, iY ) );
        }
    }

    public abstract class LayoutStack : ParentRect
	{
		protected readonly List<LayoutRect> _rgLayout = new List<LayoutRect>();

		public Func< object, SKColor > BackgroundColor = null;
		public TRACK                   Direction { get; set; }
		public object			       Extra { get; set; } = null;

		public LayoutStack( TRACK eAxis ) : 
			base( ) 
		{
			Direction = eAxis;
		}
        public LayoutStack(TRACK eAxis, CSS eUnits) :
            base(eUnits, 0, 0 )
        {
            Direction = eAxis;
        }
        
        protected LayoutStack( TRACK eAxis, uint uiTrackFromParent, float flMaxPercent ) : 
			base( CSS.Pixels, uiTrackFromParent, flMaxPercent ) 
		{
			Direction = eAxis;
		}

		public override void                    Clear()          => _rgLayout.Clear();
		public override int                     Count            => _rgLayout.Count;
		public override IEnumerator<LayoutRect> GetEnumerator()  => _rgLayout.GetEnumerator();
		/// <exception cref="ArgumentOutOfRangeException" />
		public override LayoutRect              Item(int iIndex) => _rgLayout[iIndex];

		public List<LayoutRect> Children => _rgLayout;

		public virtual void Add( LayoutRect oNew ) => _rgLayout.Add( oNew );

		protected uint Gaps {
			get {
				int iCount = Count - 1;

				foreach( LayoutRect oRect in this ) {
					if( oRect.Hidden )
						iCount--;
				}
				if( iCount > 0 )
					return (uint)iCount * Spacing;

				return 0;
			}
		}

		public override uint TrackDesired( TRACK eParentAxis, int uiRailsExtent ) {
			return (uint)GetTrack( eParentAxis ).Distance; // BUG: Revisit this.
		}

		/// <summary>
		/// Subtract the padding from the track.
		/// </summary>
		public Extent GetTrackPadded {
			get {
				int iStart = 0; 
				int iEnd   = 0;

				if( Direction == TRACK.HORIZ ) {
					iStart = Left; 
					iEnd   = Right;
					if( Padding[SCALAR.LEFT] > 0 )
						iStart += Padding[SCALAR.LEFT];
					if( Padding[SCALAR.RIGHT] > 0 )
						iEnd -= Padding[SCALAR.RIGHT];
				}
				if( Direction == TRACK.VERT ) {
					iStart = Top; 
					iEnd   = Bottom;
					if( Padding[SCALAR.TOP] > 0 )
						iStart += Padding[SCALAR.TOP];
					if( Padding[SCALAR.BOTTOM] > 0 )
						iEnd -= Padding[SCALAR.BOTTOM];
				}

				return new Extent( iStart, iEnd );
			}
		}

		/// <remarks>
		/// TODO: The only place we use the margins, is between the objects. I'd like to have a finer
		/// degree of control. Left, top etc margin. 
		/// Note, we can fail silently if the user isn't checking the return value. I'm seeing some
		/// GetTrack() underflow at startup. Need to review the adornment layout code at start up.
		/// </remarks>
		public override bool LayoutChildren() {
			try {
				if( Count <= 0 )
					return true;

				Extent extRail = GetRail( Direction );

				if( extRail.Distance <= 0 )
					return false;

				// We're padding track but not rail at present. :-/
				Extent extTrack        = GetTrackPadded;
				uint   uiRailsDistance = (uint)extRail.Distance; 
				long   iTrackAvailable = extTrack.Distance - Gaps; // This is how much available track.
				long   iTrackRemaining = iTrackAvailable;
				uint   uiCssNoneCount  = 0; // Count of the number of layout "none" objects.

				Span<uint> _rgTrack = stackalloc uint[ Count ];

				// Layout all the constant sized objects first.
				for( int i = 0; i<Count; ++i ) {
					LayoutRect oChild = Item(i);

					if( oChild.Units == CSS.None )
						++uiCssNoneCount;

					if( oChild.Units == CSS.Pixels ) {
						uint uiDesired = oChild.Track;
						if( oChild.TrackMaxPercent > 0f && oChild.TrackMaxPercent <= 1f ) {
							uint uiChildMax = (uint)(iTrackAvailable * oChild.TrackMaxPercent);
							if( uiDesired > uiChildMax )
								uiDesired = uiChildMax;
						}
						_rgTrack[i]     = uiDesired;
						iTrackRemaining -= (int)_rgTrack[i];
					}
				}

				if( iTrackRemaining < 0 )
					iTrackRemaining = 0;

				// Flex objects work intelligently based on desired width or alternatively height.
				for( int i = 0; i<Count; ++i ) {
					LayoutRect oChild = Item(i);

					if( oChild.Units == CSS.Flex ) {
						uint uiDesired = oChild.TrackDesired( Direction, (int)uiRailsDistance );

						// This is sub optimal if one of the children WANTS LESS than the space 
						// available. For example: a short list of items below an image. Then we could
						// let the image object be larger than it's percent max.
						if( oChild.TrackMaxPercent > 0f && oChild.TrackMaxPercent <= 1f ) {
							uint uiChildMax = (uint)(iTrackAvailable * oChild.TrackMaxPercent);
							if( uiDesired > uiChildMax )
								uiDesired = uiChildMax;
						}
						_rgTrack[i]     = uiDesired;
						iTrackRemaining -= _rgTrack[i];
					}
				}

				if( iTrackRemaining < 0 )
					iTrackRemaining = 0;

				// Note 1: Need to add the margins between each object to the remaining track extent.
				uint uiRemainingExtent = (uint)iTrackRemaining;

				// Percent objects now take their slice.
				for( int i = 0; i<Count; ++i ) {
					LayoutRect oChild = Item(i);
			
					if( oChild.Units == CSS.Percent ) {
						_rgTrack[i] = (uint)( ((float)oChild.Track / 100f ) * uiRemainingExtent );
						iTrackRemaining -= _rgTrack[i];
					}
				}

				uiRemainingExtent = (uint)iTrackRemaining;

				// Hopefully there's some space left for the "none" objects. You
				// would like at least some mininmum of pixels each. :-/
				if( uiCssNoneCount != 0) {
					// Divy up the remaining space equally among the "None" objects.
					for( int i = 0; i<Count; ++i ) {
						LayoutRect oChild = Item(i);

						if( oChild.Units == CSS.None ) {
							_rgTrack[i] = (uint)( ((float)1/uiCssNoneCount ) * uiRemainingExtent );
						}
					}
				}

				// This is a "box car" for current section of track we are calculating. ^_^
				Extent extCarriageTrack = new Extent( extTrack.Start, 0 ); 

				// This is fortuous, we only set the client position/size at the end of all our work.
				// Since setting the rect may send painting calculation style events.
				for( int i = 0; i<Count; ++i ) {
					extCarriageTrack.Stop = extCarriageTrack.Start + (int)_rgTrack[i];

					SetRect( extRail, extCarriageTrack, Item(i) );
					Item(i).LayoutChildren();

					extCarriageTrack.Start = extCarriageTrack.Stop + ( Item(i).Hidden ? 0 : (int)Spacing );
				}
				return true;
			} catch( Exception oEx ) {
				Type[] rgErrors = { typeof( InvalidOperationException ),
									typeof( NullReferenceException ),
									typeof( ArgumentOutOfRangeException ) };
				if( !rgErrors.Contains( oEx.GetType() ) )
					throw;

				return false;
			}
		}

		protected virtual void SetRect( Extent pntRails, Extent pntTrack, SmartRect oClient ) {
			if( Direction == TRACK.VERT ) {
				oClient.SetRect( pntRails.Start, pntTrack.Start, pntRails.Stop, pntTrack.Stop );
			} else {
				oClient.SetRect( pntTrack.Start, pntRails.Start, pntTrack.Stop, pntRails.Stop );
			}
		}

        public override void Paint(SKCanvas skCanvas) {
			PaintBackground( skCanvas );
            base.Paint(skCanvas);
        }

        public override void PaintBackground(SKCanvas skCanvas) {
			if( BackgroundColor != null ) {
				SKPaint skPaint  = new SKPaint() { Color = BackgroundColor( Extra ) };
				skCanvas.DrawRect( this.SKRect, skPaint );
			}
        }
    }

	public class LayoutStackVertical : LayoutStack {
		public LayoutStackVertical( ) : base( TRACK.VERT ) {
		}

		public LayoutStackVertical( uint uiTrack, float flMaxPercent ) : 
			base( TRACK.VERT, uiTrack, flMaxPercent ) 
		{
		}

		protected override void SetRect( Extent pntRails, Extent pntTrack, SmartRect oClient ) {
			oClient.SetRect( pntRails.Start, pntTrack.Start, pntRails.Stop, pntTrack.Stop );
		}
	}

	public class LayoutStackHorizontal : LayoutStack {
		public LayoutStackHorizontal( ) : base( TRACK.HORIZ ) {
		}

		public LayoutStackHorizontal( uint uiTrack, float flMaxPercent ) : 
			base( TRACK.HORIZ, uiTrack, flMaxPercent ) 
		{
		}

		protected override void SetRect( Extent pntRails, Extent pntTrack, SmartRect oClient ) {
			oClient.SetRect( pntTrack.Start, pntRails.Start, pntTrack.Stop, pntRails.Stop );
		}
	}

	/// <summary>
	/// This one we expect the layout direction to change on the fly. It 
	/// could be the case for both vertical and horzontal, but I hate having
	/// the switch if it really isn't needed ^_^;;
	/// </summary>
	public class LayoutStackChoosy : LayoutStack {
		public LayoutStackChoosy( ) : base( TRACK.HORIZ ) {
		}

		public LayoutStackChoosy( uint uiTrack, float flMaxPercent ) : 
			base( TRACK.HORIZ, uiTrack, flMaxPercent ) 
		{
		}

		protected override void SetRect( Extent pntRails, Extent pntTrack, SmartRect oClient ) {
			switch( Direction ) {
				case  TRACK.HORIZ:
					oClient.SetRect( pntTrack.Start, pntRails.Start, pntTrack.Stop, pntRails.Stop );
					break;
				case TRACK.VERT:
					oClient.SetRect( pntRails.Start, pntTrack.Start, pntRails.Stop, pntTrack.Stop );
					break;
				default:
					throw new NotImplementedException();
			}
		}
	}

    public class LayoutStackBgGradient : LayoutStack {
        public List<SKColor> Colors { get; } = new List<SKColor>();
        public List<float>   Points { get; } = new List<float>();
        public LayoutStackBgGradient( TRACK eTrack ) : base( eTrack ) {
        }

        public override void PaintBackground(SKCanvas skCanvas) {
            if( Colors.Count <= 0 ) {
                base.PaintBackground( skCanvas );
                return;
            }

            using SKPaint skPaint = new() { BlendMode = SKBlendMode.SrcATop, IsAntialias = true };

            if( Points.Count <= 0 || Points.Count != Colors.Count ) {
				float flDivisor = Colors.Count - 1;
				Points.Clear();
                for( int i = 0; i < Colors.Count; ++i ) {
                    float flPoint = i / flDivisor;
                    Points.Add( flPoint );
                }
            }

            // Create linear gradient from left to Right
            skPaint.Shader = SKShader.CreateLinearGradient(
                                new SKPoint( Left,  Top),
                                new SKPoint( Right, Bottom),
                                Colors.ToArray(),
                                Points.ToArray(),
                                SKShaderTileMode.Repeat );

            try {
                skCanvas.DrawRect( Left, Top, Width, Height, skPaint );
            } catch( Exception oEx ) {
                Type[] rgErrors = { typeof( ArgumentNullException ),
									typeof( ArgumentException ),
									typeof( NullReferenceException ),
									typeof( OverflowException ),
									typeof( AccessViolationException ) };
                if( !rgErrors.Contains( oEx.GetType() ) )
                    throw;
            }
        }
    }

	/// <summary>
	/// A table object computes row rail size as the max track needed by a given cell on that row.
	/// </summary>
	[Obsolete]public class LayoutTable : ParentRect {
        readonly LayoutStack _oRowStack;
        readonly LayoutStack _oColStack;

		public class LayoutColumn : LayoutRect {
			readonly LayoutStack _oRowStack;
			readonly uint        _uiColumn;
			
			public LayoutColumn( uint uiColumn, LayoutStack oRowStack ) : 
				base() 
			{
				_oRowStack = oRowStack ?? throw new ArgumentNullException();
				_uiColumn  = uiColumn;
			}

			/// <summary>
			/// It's a bit of a bummer that I'm tracking the entire table. 
			/// But a scrolling table is likely to be fixed height anyway, so
			/// this might turn out ok.
			/// </summary>
			public override uint TrackDesired( TRACK eDir, int iRail ) {
				if( Units == CSS.Flex ) {
					foreach( LayoutStack oRow in _oRowStack ) {
						LayoutRect oCell          = oRow.Item( (int)_uiColumn );
        				uint       uiTrackDesired = oCell.TrackDesired(TRACK.HORIZ, iRail );

						// Look for max column width desired out of all the rows.
						if( Track < uiTrackDesired ) {
							Track = uiTrackDesired;
        				}
        			}
				}

				return Track;
			}
		}

		public LayoutTable( uint uiSpacing, CSS eUnits ) : base( eUnits, 0, 100 ) {
			Spacing    = uiSpacing;

            _oRowStack = new LayoutStackVertical  ( ) { Spacing = this.Spacing };
            _oColStack = new LayoutStackHorizontal( ) { Spacing = this.Spacing };
        }

        public override int Count => _oRowStack.Count;

        public override LayoutRect Item( int iIndex ) {
            return _oRowStack.Item( iIndex );
        }

        public virtual LayoutStack RowItem(int iIndex)
        {
            return ((LayoutStack)_oRowStack.Item(iIndex));
        }

        public LayoutStack Rows {
            get { return _oRowStack; }
        }

		/// <remarks>
		/// These columns have access to the rows! And we're not introducing any circular
		/// references which is mainly undesirable. But they allow the stack layout code
		/// to do double duty layout for the table columns!!
		/// </remarks>
		public virtual void AddColumn( CSS eLayout, int iTrack ) {
			if( eLayout == CSS.Flex ) {
				_oColStack.Add( new LayoutTable.LayoutColumn( (uint)_oColStack.Count, _oRowStack ) 
									{ TrackMaxPercent = iTrack, Units = eLayout } 
							  );
			} else {
				_oColStack.Add( new LayoutTable.LayoutColumn( (uint)_oColStack.Count, _oRowStack ) 
									{ Track = (uint)iTrack, Units = eLayout } 
							  );
			}
		}

        /// <summary>
        /// You'll need to reload the columns if you make this call.
        /// </summary>
        public override void Clear() {
			_oRowStack.Clear();
		}

		public void AddRow( List<LayoutRect> rgRow ) {
            LayoutStack oNewRowRect = new LayoutStackHorizontal() { Spacing = this.Spacing };

            foreach( LayoutRect oCell in rgRow ) {
                oNewRowRect.Add( oCell );
            }

            _oRowStack.Add( oNewRowRect );
		}

        public override uint TrackDesired(TRACK eParentAxis, int iRail) {
            switch( eParentAxis ) {
                case TRACK.HORIZ:
                    return (uint)Width;
                case TRACK.VERT:
                    HeightDesired( iRail );
                    return _oRowStack.Track;
            }

            return 0;
        }

        /// <summary>
        /// Width of the table needs to be set, but the height can vary if the table is set to CSS flex layout.
        /// Currently we're not set up to scroll the contents of the table if the height is not enough.
        /// </summary>
        public bool HeightDesired( int iWidth ) {
			try {
                _oRowStack.Track = 0;

                foreach ( LayoutStack oRowItem in _oRowStack ) {
                    // Find the max cell height for the row.
                    int iColumn = 0; // which cell on the row we're looking at.
                    oRowItem.Track  = 0;
                    foreach( LayoutRect oCell in oRowItem ) {
                        int  iLeft   = _oColStack.Item(iColumn             ).Left;
                        int  iRight  = _oColStack.Item(iColumn + oCell.Span).Right;
						int  iSpan   = iRight - iLeft;
                        uint uiTrack = oCell.TrackDesired( TRACK.VERT, iSpan );
						// Update the row track size if cell is bigger.
						if( uiTrack > oRowItem.Track )
							oRowItem.Track = uiTrack;
                        iColumn += oCell.Span + 1; // Make sure we skip over the spanned cell!!
					}
                    _oRowStack.Track += oRowItem.Track + Spacing; // BUG: Margins are all messed up.
                }
            } catch ( Exception oEx ) {
				Type[] rgErrors = { 
					typeof( NullReferenceException ),
					typeof( ArgumentOutOfRangeException ),
					typeof( InvalidCastException ) };
				if( !rgErrors.Contains( oEx.GetType() ) )
					throw;

				return false;
			}

			return true;
		}

        /// <summary>
        /// Might need to think about this. When in flex mode we depend on the TrackDesired() being
        /// called, so we can calculate the minimum height needed for each row. If we aren't in flex
        /// mode, TrackDesired isn't called. Need to look at this a bit closer.
        /// </summary>
        /// <returns></returns>
        public override bool LayoutChildren() {
            try {

                if( Style == CSS.Flex ) {
					_oColStack.Width  = Width;
					_oColStack.Height = 10000;
					_oColStack.LayoutChildren();
				}

                HeightDesired( Width );

                int iRowStart = Top;
                int iColumn   = 0;
                foreach (LayoutStack oRow in _oRowStack) {
                    // Go and set all the cell dimensions, now that we know row height.
                    iColumn = 0;
                    foreach (LayoutRect oCell in oRow) {
                        int iLeft       = _oColStack.Item(iColumn).Left;
                        int iRight      = _oColStack.Item(iColumn + oCell.Span).Right;
                        int iTop        = iRowStart;
                        int iBottom     = iRowStart + (int)oRow.Track;
                        int iHeightDiff = (int)(oRow.Track - oCell.TrackDesired(TRACK.VERT, iRight - iLeft));

                        // vertical justify...BUG need this switched off/on
                        if (iHeightDiff > 0) {
                            iTop    += iHeightDiff / 2;
                            iBottom -= iHeightDiff / 2;

                            if (iBottom < iTop)
                                iBottom = iTop;
                        }
                        oCell.SetRect(iLeft, iTop, iRight, iBottom);
                        iColumn += oCell.Span + 1;
                    }
                    iRowStart += (int)oRow.Track + 3; // + _uiMargin; no margin between rows for now...
                }
            } catch (Exception oEx ) {
				Type[] rgErrors = {
                    typeof( NullReferenceException ),
                    typeof( ArgumentOutOfRangeException ),
                    typeof( InvalidCastException ) };
				if( !rgErrors.Contains(oEx.GetType() ) )
					throw;

				return false;
			}

            return true;
        }

        public override IEnumerator<LayoutRect> GetEnumerator() {
            foreach (LayoutRect oRow in _oRowStack) {
                if (oRow is LayoutStack oStack)
                    yield return oStack;
            }
        }

        public override void Paint( SKCanvas skCanvas ) {
			PaintBackground( skCanvas );

			int i=0;
			using SKPaint skPaint = new SKPaint();
            foreach( SmartRect oColumn in _oColStack ) {
				SKColor skColor = ( i++ % 2 == 0 ) ? SKColors.White : SKColors.LightGray;
				skPaint.Color = skColor;
				skCanvas.DrawRect( oColumn.Left, oColumn.Top, oColumn.Width, oColumn.Height, skPaint );
			}
        }
    } // End class.

	public abstract class LayoutFlowSquare : ParentRect {
        protected List<int> _rgColumns = new List<int>();
		public Size ItemSize;
		public bool Springy = true;

		public LayoutFlowSquare( Size szSize ) : 
			base( CSS.Pixels, 0, 1f ) 
		{
			ItemSize = szSize;
		}

		public LayoutFlowSquare( CSS eUnits ) :
			base( eUnits, 0, 1f ) 
		{
			ItemSize = new Size( 0, 0 );
		}

		/// <summary>
		/// Assuming the items are square, figure out how many can fit in
		/// the area of the parent. Elements will be shrunk to fit as long
		/// as the resulting layout is close to n rows by n columns.
		/// </summary>
		public virtual void ItemSizeCalculate() {
			double dRoot     = Math.Sqrt( Count );
			double dCeiling  = Math.Ceiling( dRoot );
			double dSquare   = dCeiling * dCeiling;
			Point  pntExtent = new Point( (int)dCeiling, (int)dCeiling );
			SizeF  szSize    = new SizeF( Width, Height ); // Space we are filling.

			// If we don't use all the rows, stretch the Height by one row.
			if( Count <= dSquare - dCeiling )
				pntExtent = new Point( pntExtent.X, pntExtent.Y-1 );

			Point pntGaps    = new Point( pntExtent.X > 0 ? pntExtent.X - 1 : 0,
										  pntExtent.Y > 0 ? pntExtent.Y - 1 : 0 );

			if( Count > 1 ) {
				szSize.Width  -= Padding.Left + Padding.Right;
				szSize.Height -= Padding.Top  + Padding.Bottom;

				szSize.Width  -= (int)Spacing * pntGaps.X;
				szSize.Height -= (int)Spacing * pntGaps.Y;

				szSize.Width  /= pntExtent.X;
				szSize.Height /= pntExtent.Y;

				if( szSize.Width < 0 )
					szSize.Width = 0;
				if( szSize.Height < 0 )
					szSize.Height = 0;
			}

			ItemSize = new Size( (int)szSize.Width, (int)szSize.Height );
		}

        /// <summery>Try to figure out how many thumbs will fit on all rows if the
		/// objects are all the same width, given by the ItemSize member.</summery>
		/// <seealso cref="ItemSize"/>
		protected int FindDimensions() {
			int iRight  = Left;
			int iHeight = 0;
			int iCount  = 0;

            _rgColumns.Clear();

            foreach( LayoutRect oRect in this ) {
				int iRun = iRight;

				iRun += Padding.Left;
                iRun += ItemSize.Width;
                iRun += Padding.Right;

				// Allow the first one in all cases.
                if( iRun > Right && iCount > 0 ) 
                    break;

				_rgColumns.Add( iRight + Padding.Left );
				iRight = iRun;
				// Bug: For varying height items use TrackDesired. 
				if( ItemSize.Height > iHeight )
					iHeight = ItemSize.Height;
				++iCount;
            };

            // This this makes the columns spring loaded.
            float fDiff = Right - iRight;
			if( fDiff > 0 ) {
				int iIncr = (int)(fDiff/(_rgColumns.Count+1));
				int iAcum = iIncr;

				for( int iCol = 0; iCol<_rgColumns.Count; ++iCol ) { 
					_rgColumns[iCol] += iAcum;
					if( Springy )
						iAcum += iIncr;
				}
			}
			return iHeight; // Height of each row. No margin calc included.
		}

		public override bool LayoutChildren() {
			ItemSizeCalculate();

			int iHeight = FindDimensions();

            for( int i = 0, iTop = Top + Padding.Top; i < Count;  ) {
                foreach( int iStart in _rgColumns ) {
                    Item(i).SetRect( LOCUS.UPPERLEFT, iStart, iTop, ItemSize.Width, ItemSize.Height );
					Item(i).LayoutChildren();

                    if( ++i >= Count )
                        break;
                }
                iTop += iHeight + (int)Spacing;
            };

			return true;
		}

        public override uint TrackDesired(TRACK eParentAxis, int uiRail) {
			int    iHeight = FindDimensions();
			double dblRows = Math.Ceiling( Count / (double)_rgColumns.Count );

			uint uiSpace = dblRows > 1.0 ? (uint)(dblRows - 1) * Spacing : 0;

            return (uint)( dblRows * iHeight + uiSpace + Padding.Bottom + Padding.Top );
        }
    } // End class

	/// <summary>
	/// Implementation for the LayoutFlowSqure abstract class.
	/// </summary>
	/// <seealso cref="LayoutFlowSquare"/>
	public class LayoutFlowSquare_LayoutRect : LayoutFlowSquare {
		readonly List<LayoutRect> _rgLayout = new List<LayoutRect>();

		public LayoutFlowSquare_LayoutRect( Size szSize ) : base( szSize ) {
		}

		public LayoutFlowSquare_LayoutRect( CSS eUnits ) : base( eUnits ) {
		}

		public override void                    Clear()          => _rgLayout.Clear();
		public override LayoutRect              Item(int iIndex) => _rgLayout[iIndex];
		public override IEnumerator<LayoutRect> GetEnumerator()  => _rgLayout.GetEnumerator();
		public override int                     Count            => _rgLayout.Count;

		public virtual void RemoveAt( int i ) => _rgLayout.RemoveAt( i );

		public void Add( LayoutRect oItem ) => _rgLayout.Add( oItem );
	} // End class
}
