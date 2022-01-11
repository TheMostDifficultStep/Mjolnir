using System;
using System.Collections.Generic;
using System.Linq;
using System.Drawing;
using System.Collections;
using SkiaSharp;

namespace Play.Rectangles {
	/// <summary>
	/// Parent determines layout direction.
	/// </summary>
	public class LayoutRect : SmartRect {
		protected CSS _eLayout;

		public enum CSS {
			Percent,
			Pixels,
			Flex,
			None,
			Empty
		}

		public LayoutRect( CSS eLayout ) : base() { 
			_eLayout        = eLayout;
			Track           = 0;
			TrackMaxPercent = 1;
		}

		public LayoutRect(CSS eLayout, uint uiTrack, float flMaxPercent) : base() {
			_eLayout        = eLayout;
			Track           = uiTrack;
			TrackMaxPercent = flMaxPercent;
		}

		public CSS   Units { get { return Hidden ? CSS.Empty : _eLayout; } set { _eLayout = value; } }
		public uint  Track { get; set; } // TODO: Make this a float.
		public float TrackMaxPercent { get; } // TODO : Use minmax object.
		public int   Span  { get; set; } = 0; // CSS span value minus 1. Bummer here but shared with SmartTable.
		public bool  Hidden = false;

		public virtual uint TrackDesired( TRACK eParentAxis, int uiRail ) { return Track; }
		public virtual void Invalidate() { }
	}

	public abstract class ParentRect : 
		LayoutRect,
		IEnumerable<LayoutRect>
	{
		protected readonly uint _uiMargin = 0;

		public ParentRect( uint uiMargin ) : base( CSS.None ) {
			_uiMargin = uiMargin;
		}

		protected ParentRect( CSS eUnits, uint uiMargin, uint uiTrack, float flMaxPercent ) : 
			base( eUnits, uiTrack, flMaxPercent ) 
		{
			_uiMargin = uiMargin;
		}

		public abstract void                    Clear();
		public abstract int                     Count { get; }
		public abstract LayoutRect              Item( int iIndex );
		public abstract IEnumerator<LayoutRect> GetEnumerator();

		IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

		public override void Paint(Graphics oGraphics) {
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
	}

	public abstract class LayoutStack : ParentRect
	{
		protected readonly List<LayoutRect> _rgLayout = new List<LayoutRect>();

		public TRACK Direction { get; set; }

		public LayoutStack( TRACK eAxis, uint uiMargin ) : 
			base( uiMargin ) 
		{
			Direction = eAxis;
		}
        public LayoutStack(TRACK eAxis, CSS eUnits, uint uiMargin) :
            base(eUnits, uiMargin, 0, 0 )
        {
            Direction = eAxis;
        }
        
        protected LayoutStack( TRACK eAxis, uint uiMargin, uint uiTrackFromParent, float flMaxPercent ) : 
			base( CSS.Pixels, uiMargin, uiTrackFromParent, flMaxPercent ) 
		{
			Direction = eAxis;
		}

		public override void                    Clear()          => _rgLayout.Clear();
		public override int                     Count            => _rgLayout.Count;
		public override IEnumerator<LayoutRect> GetEnumerator()  => _rgLayout.GetEnumerator();
		/// <exception cref="ArgumentOutOfRangeException" />
		public override LayoutRect              Item(int iIndex) => _rgLayout[iIndex];

		public virtual void Add( LayoutRect oNew ) => _rgLayout.Add( oNew );

		protected uint Gaps {
			get {
				int iCount = Count - 1;

				foreach( LayoutRect oRect in this ) {
					if( oRect.Hidden )
						iCount--;
				}
				if( iCount > 0 )
					return (uint)iCount * _uiMargin;

				return 0;
			}
		}

		public override uint TrackDesired( TRACK eParentAxis, int uiRailsExtent ) {
			return (uint)GetTrack( eParentAxis ).Distance; // BUG: Revisit this.
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

				Extent extTrack        = GetTrack( Direction );
				uint   uiRailsDistance = (uint)extRail .Distance; 
				long   iTrackAvailable = extTrack.Distance - Gaps; // This is how much available track.
				long   iTrackRemaining = iTrackAvailable;
				uint   uiCssNoneCount  = 0; // Count of the number of layout "none" objects.

				uint[] _rgTrack = new uint[ Count ];

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

				// We can't have both Percent and None objects, so if we have ANY "none"
				// objects, we just punt on the percent objects.
				if( uiCssNoneCount == 0 ) {
					// Percent objects take up the remaining space.
					for( int i = 0; i<Count; ++i ) {
						LayoutRect oChild = Item(i);
			
						if( oChild.Units == CSS.Percent ) {
							_rgTrack[i] = (uint)( ((float)oChild.Track / 100f ) * uiRemainingExtent );
							//iTrackDistance -= _rgTrack[i];
						}
					}
				} else {
					// Divy up the remaining space equally among the "None" objects.
					for( int i = 0; i<Count; ++i ) {
						LayoutRect oChild = Item(i);

						if( oChild.Units == CSS.None ) {
							_rgTrack[i] = (uint)( ((float)1/uiCssNoneCount ) * uiRemainingExtent );
							//iTrackDistance -= _rgTrack[i];
						}
					}
				}

				// So once the track is set, some objects might want more height (rail).
				//for( int i = 0; i<Count; ++i ) {
				//	uint uiItemRail = Item(i).TrackDesired( Direction, (int)_rgTrack[i] );
				//	if( uiItemRail > uiRailsDistance )
				//		uiRailsDistance = uiItemRail;
				//}
				//extRail.Stop = extRail.Start + (int)uiRailsDistance;

				// This is a "box car" for current section of track we are calculating. ^_^
				Extent extCarriageTrack = new Extent( extTrack.Start, 0 ); 

				// This is fortuous, we only set the client position/size at the end of all our work.
				// Since setting the rect may send painting calculation style events.
				for( int i = 0; i<Count; ++i ) {
					extCarriageTrack.Stop = extCarriageTrack.Start + (int)_rgTrack[i];

					SetRect( extRail, extCarriageTrack, Item(i) );
					// TODO: There might be other layout managers in the future. But this will do for now.
					if( Item(i) is ParentRect oStacker ) {
						oStacker.LayoutChildren();
					}

					extCarriageTrack.Start = extCarriageTrack.Stop + ( Item(i).Hidden ? 0 : (int)_uiMargin );
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
	}

	public class LayoutStackVertical : LayoutStack {
		public LayoutStackVertical( uint uiMargin ) : base( TRACK.VERT, uiMargin ) {
		}

		public LayoutStackVertical( uint uiMargin, uint uiTrack, float flMaxPercent ) : 
			base( TRACK.VERT, uiMargin, uiTrack, flMaxPercent ) 
		{
		}

		protected override void SetRect( Extent pntRails, Extent pntTrack, SmartRect oClient ) {
			oClient.SetRect( pntRails.Start, pntTrack.Start, pntRails.Stop, pntTrack.Stop );
		}
	}

	public class LayoutStackHorizontal : LayoutStack {
		public LayoutStackHorizontal( uint uiMargin ) : base( TRACK.HORIZ, uiMargin ) {
		}

		public LayoutStackHorizontal( uint uiMargin, uint uiTrack, float flMaxPercent ) : 
			base( TRACK.HORIZ, uiMargin, uiTrack, flMaxPercent ) 
		{
		}

		protected override void SetRect( Extent pntRails, Extent pntTrack, SmartRect oClient ) {
			oClient.SetRect( pntTrack.Start, pntRails.Start, pntTrack.Stop, pntRails.Stop );
		}
	}

	/// <summary>
	/// A table object computes row rail size as the max track needed by a given cell on that row.
	/// </summary>
	public class SmartTable : ParentRect {
        readonly LayoutStack _oRowStack;
        readonly LayoutStack _oColStack;

		public SmartTable( uint uiMargin, CSS eUnits ) : base( eUnits, uiMargin, 0, 100 ) {
            _oRowStack = new LayoutStackVertical  ( uiMargin );
            _oColStack = new LayoutStackHorizontal( uiMargin );
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

        public virtual void Add( LayoutRect oColumn) {
            _oColStack.Add( oColumn );
        }

        /// <summary>
        /// You'll need to reload the columns if you make this call.
        /// </summary>
        public override void Clear() {
			_oRowStack.Clear();
		}

		public void AddRow( List<LayoutRect> rgRow ) {
            LayoutStack oNewRowRect = new LayoutStackHorizontal(_uiMargin);

            foreach( LayoutRect oCell in rgRow ) {
                oNewRowRect.Add( oCell );
            }

            _oRowStack.Add( oNewRowRect );
		}

        // See PropWin.cs in the Editor for the reason we need the column flex stuff.
        public bool LayoutColumnWidth( int iWidth, int iHeight ) {
            _oColStack.SetRect( 0, 0, iWidth, iHeight );

            // In the flex columns look for the longest cell in that column.
            // We kind of need a minimum height per row which might make the 
            // TrackDesired guess a bit more accurate in general.
            // Example: Flex Image followed by a multi line text box!
            // at least max track percent blocks the column width.
            int iColumn = 0;
        	foreach( LayoutRect oColumn in _oColStack ) {
                if( oColumn.Units == CSS.Flex ) {
                    oColumn.Track = 0;
                    foreach( LayoutStack oRow in _oRowStack ) {
                        LayoutRect oCell          = oRow.Item(iColumn);
        				uint       uiTrackDesired = oCell.TrackDesired(TRACK.HORIZ, iHeight);

                        if( oColumn.Track < uiTrackDesired ) {
                            oColumn.Track = uiTrackDesired;
        				}
        			}
                }
                iColumn++;
        	}

            // This works because a normal LayoutRect will use whatever Track value is set
            // on it for the TrackDesired() which is called in flex mode.
            _oColStack.LayoutChildren();

            // At this point the columns do not have their rail start and stop values set,
            // or their track start and stop. Only the track segments are done.

            return true;
        }

        public override uint TrackDesired(TRACK eParentAxis, int iRail) {
            switch( eParentAxis ) {
                case TRACK.HORIZ:
                    return (uint)Width;
                case TRACK.VERT:
                    HeightDesired( iRail );
                    return _oRowStack.Track;
            }

            return( 0 );
        }

        /// <summary>
        /// Width of the table needs to be set, but the height can vary if the table is set to CSS flex layout.
        /// Currently we're not set up to scroll the contents of the table if the height is not enough.
        /// </summary>
        public bool HeightDesired( int iWidth ) {
			try {
                LayoutColumnWidth( iWidth, 10000 );

                _oRowStack.Track = 0;

                foreach ( LayoutStack oRow in _oRowStack ) {
                    // Find the max cell height for the row.
                    int iColumn = 0;
                    oRow.Track  = 0;
                    foreach( LayoutRect oCell in oRow ) {
                        int  iLeft   = _oColStack.Item(iColumn             ).Left;
                        int  iRight  = _oColStack.Item(iColumn + oCell.Span).Right;
                        uint uiTrack = oCell.TrackDesired( TRACK.VERT, iRight - iLeft );

						if( uiTrack > oRow.Track )
							oRow.Track = uiTrack;
                        iColumn += oCell.Span + 1; // Make sure we skip over the spanned cell!!
					}
                    _oRowStack.Track += oRow.Track + _uiMargin; // BUG: Margins are all messed up.
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

                if( _eLayout != CSS.Flex ) // BUG: This seems odd. Should flex be on for row height calculation??
                    HeightDesired( Width );
                else
                    LayoutColumnWidth( Width, Height );

                int iRowStart = Top;
                int iColumn   = 0;
                foreach (LayoutStack oRow in _oRowStack) {
                    // Go and set all the cell dimensions, now that we know row height.
                    iColumn = 0;
                    foreach (LayoutRect oCell in oRow)
                    {
                        int iLeft       = _oColStack.Item(iColumn).Left;
                        int iRight      = _oColStack.Item(iColumn + oCell.Span).Right;
                        int iTop        = iRowStart;
                        int iBottom     = iRowStart + (int)oRow.Track;
                        int iHeightDiff = (int)(oRow.Track - oCell.TrackDesired(TRACK.VERT, iRight - iLeft));

                        // vertical justify...BUG need this switched off/on
                        if (iHeightDiff > 0) {
                            iTop += iHeightDiff / 2;
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
		public readonly SmartRect Margin = new SmartRect();
		public Size ItemSize;
		public bool Springy = true;

		public LayoutFlowSquare( Size szSize, uint uiMargin ) : 
			base( CSS.Pixels, uiMargin, 0, 1f ) 
		{
			ItemSize = szSize;
		}

		public LayoutFlowSquare( CSS eUnits, uint uiMargin ) :
			base( eUnits, uiMargin, 0, 1f ) 
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
				szSize.Width  -= Margin.Left + Margin.Right;
				szSize.Height -= Margin.Top  + Margin.Bottom;

				szSize.Width  -= _uiMargin * pntGaps.X;
				szSize.Height -= _uiMargin * pntGaps.Y;

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
		protected int FindDimensions( List<int> rgColumns ) {
			int iRight  = Left;
			int iHeight = 0;
			int iCount  = 0;

            foreach( LayoutRect oRect in this ) {
				int iRun = iRight;

				iRun += Margin.Left;
                iRun += ItemSize.Width;
                iRun += Margin.Right;

				// Allow the first one in all cases.
                if( iRun > Right && iCount > 0 ) 
                    break;

				rgColumns.Add( iRight + Margin.Left );
				iRight = iRun;
				// Bug: For varying height items use TrackDesired. 
				if( ItemSize.Height > iHeight )
					iHeight = ItemSize.Height;
				++iCount;
            };

            // This this makes the columns spring loaded.
            float fDiff = Right - iRight;
			if( fDiff > 0 ) {
				int iIncr = (int)(fDiff/(rgColumns.Count+1));
				int iAcum = iIncr;

				for( int iCol = 0; iCol<rgColumns.Count; ++iCol ) { 
					rgColumns[iCol] += iAcum;
					if( Springy )
						iAcum += iIncr;
				}
			}
			return iHeight; // Height of each row. No margin calc included.
		}

		public override bool LayoutChildren() {
			ItemSizeCalculate();

            List<int> rgColumns = new List<int>();
			int       iHeight   = FindDimensions( rgColumns );

            for( int i = 0, iTop = Top + Margin.Top; i < Count;  ) {
                foreach( int iStart in rgColumns ) {
                    Item(i).SetRect( LOCUS.UPPERLEFT, iStart, iTop, ItemSize.Width, ItemSize.Height );

                    // TODO: This is still a bit hacky. Might be best to add LayoutChildren() to any LayoutRect?
					if( Item(i) is ParentRect oStacker ) {
						oStacker.LayoutChildren();
					}

                    if( ++i >= Count )
                        break;
                }
                iTop += ( iHeight + Margin.Bottom + Margin.Top );
            };

			return true;
		}
	} // End class

	/// <summary>
	/// Implementation for the LayoutFlowSqure abstract class.
	/// </summary>
	/// <seealso cref="LayoutFlowSquare"/>
	public class LayoutFlowSquare_LayoutRect : LayoutFlowSquare {
		readonly List<LayoutRect> _rgLayout = new List<LayoutRect>();

		public LayoutFlowSquare_LayoutRect( Size szSize, uint uiMargin ) : base( szSize, uiMargin ) {
		}

		public LayoutFlowSquare_LayoutRect( CSS eUnits, uint uiMargin ) : base( eUnits, uiMargin ) {
		}

		public override void                    Clear()          => _rgLayout.Clear();
		public override LayoutRect              Item(int iIndex) => _rgLayout[iIndex];
		public override IEnumerator<LayoutRect> GetEnumerator()  => _rgLayout.GetEnumerator();
		public override int                     Count            => _rgLayout.Count;

		public void Add( LayoutRect oItem ) => _rgLayout.Add( oItem );
	} // End class
}
