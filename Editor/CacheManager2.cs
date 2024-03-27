using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing; 
using System.Linq;

using SkiaSharp;

using Play.Interfaces.Embedding;
using Play.Rectangles;
using Play.Parse;

namespace Play.Edit {
    public enum CaretMove {
        LOCAL, // Onscreen
        MISS,
        NEARBY,
    }

    public enum RefreshType {
        RESET,   // Start over from caret or scroll pos.
        COMPLEX, // Elements have been deleated. Try to patch up.
        SIMPLE   // Probably just some text edits.
    }

    /// <summary>
    /// This is the primary object that holds all the measured lines for the currently displayed portion of text.
    /// </summary>
	/// <remarks>I'm not sure where I got the idea to break up the CacheManager/CacheBase but it is important
	/// to initialize the manager's _oTextRect</remarks>
    public class CacheManager2 :         
        IEnumerable<CacheRow>
    {
        protected readonly CacheManagerAbstractSite  _oSite      = null;
        // World coordinates of our view port. Do not confuse these with the
        // layout columns, those are different.
        readonly SmartRect                  _oTextRect  = new SmartRect();
        protected List<CacheRow>            _rgOldCache = new List<CacheRow>();
        protected readonly List<SmartRect>  _rgCacheMap;

        protected IPgFontRender Font       { get; }
        protected IPgGlyph      GlyphLt    { get; } // Our end of line character.
        public    int           LineHeight { get; } // Helps us determine scrolling distances.

        // TODO: Get the font from the site instead of from the constructor? Maybe?
        /// <remarks>Need to sort out the LineHeight accessor since the cache elements might be
        /// variable height, Need to make sure I'm using this correctly. Consider calling it
        /// "LineScroll"</remarks>
        public CacheManager2( CacheManagerAbstractSite oSite, IPgFontRender oFont, List<SmartRect> rgColumns ) :
			base() 
		{
			Font        = oFont     ?? throw new ArgumentNullException( "Need a font to get things rolling." );
			_oSite      = oSite     ?? throw new ArgumentNullException( "Cache manager is a sited object.");
            _rgCacheMap = rgColumns ?? throw new ArgumentNullException( "Columns list from Edit Window is null!" );

            GlyphLt    = Font.GetGlyph( 0x003c ); // we used to show carriage return as a '<' sign.
            LineHeight = (int)Font.LineHeight; // BUG: Cache elem's are variable height in general.
        }

        public IEnumerator<CacheRow> GetEnumerator() {
            return _rgOldCache.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() {
            return _rgOldCache.GetEnumerator();
        }

        protected void LogError( string strDetails ) {
            _oSite.LogError( "Text Manager", strDetails );
        }

        /// <summary>
        /// Going to have to be ultra careful before enabling this. Up to now we've assumed 1 space
        /// between lines and line height given by the font.
        /// </summary>
        public int LineSpacing { get; set; } = 1;

        /// <summary>
        /// Count of number of CacheRow objects inside the manager.
        /// </summary>
        public int Count {
            get { return _rgOldCache.Count; }
        }

        public SmartRect TextRect {
            get { return _oTextRect; }
        }

        public void OnMouseWheel( int iDelta ) {
            int iTopOld = _oTextRect[ SCALAR.TOP ];
            int iTopNew = iTopOld - ( 4 * iDelta / LineHeight );
            
            _oTextRect.SetScalar(SET.RIGID, SCALAR.TOP, iTopNew );
        }

        public void OnScrollBar_Vertical( ScrollEvents e ) {
            int iHeight       = _oTextRect[ SCALAR.HEIGHT ];
            int iTopOld       = _oTextRect[ SCALAR.TOP    ];
            int iSafetyMargin = 10; // Probably should be some % of font height.

            switch( e ) {
                // These events move incrementally from where we were.
                case ScrollEvents.LargeDecrement:
                    _oTextRect.SetScalar(SET.RIGID, SCALAR.TOP, iTopOld - iHeight - iSafetyMargin );
                    break; 
                case ScrollEvents.SmallDecrement:
                    _oTextRect.SetScalar(SET.RIGID, SCALAR.TOP, iTopOld - LineHeight );
                    break;
                case ScrollEvents.LargeIncrement:
                    _oTextRect.SetScalar(SET.RIGID, SCALAR.TOP, iTopOld + iHeight - iSafetyMargin );
                    break;
                case ScrollEvents.SmallIncrement:
                    _oTextRect.SetScalar(SET.RIGID, SCALAR.TOP, iTopOld + LineHeight );
                    break;

                // We can potentialy render less until this final end scroll comes in.
                case ScrollEvents.EndScroll:
                    break;

                // These events reset the coordinate system. NOTE: Scroll reset does this too. Probably redundant.
                case ScrollEvents.First:
                    _oTextRect.SetScalar(SET.RIGID, SCALAR.TOP, 0 );
                    break;
                case ScrollEvents.Last:
                    // TODO: Probably dangerous setting MaxValue. I could simply move it past the last screen full.
                    _oTextRect.SetScalar(SET.RIGID, SCALAR.BOTTOM, int.MaxValue - iHeight );
                    break;
                case ScrollEvents.ThumbPosition:
                    break;
                case ScrollEvents.ThumbTrack:
                    break;
            }
        }

        /// <summary>
        /// This effectively resets the cache manager. We create a seed element
        /// chosen by rough scroll position or cursor as specified by the neighborhood.
        /// The buffer rectangle is reset so it's top is zero and the top of the new cache
        /// element is zero. Old cache is untouched and will need to be cleared.
        /// </summary>
        protected CacheRow CacheReset( 
            RefreshNeighborhood eNeighborhood
        ) {
            // Where to set the first cache elem TOP? 0 or somewhere in the middle of the screen?
            // I think 0 is fine since we can go negative no problem.
            _oTextRect.SetPoint(SET.RIGID, LOCUS.UPPERLEFT, 0, 0 );

			// Ask our site for locating ourselves. Either based on our scroll
			// position or carat depending on how we were called.
			_oSite.Neighborhood( eNeighborhood, out Line oLine );
			if ( oLine == null )
                return null;

            CacheRow oRow = _rgOldCache.Find( item => item.At == oLine.At ); // TODO: Reconsider change back to item.Line == oLine
            if (oRow == null) // If can't find matching elem, create it.
                oRow = CreateRow( oLine );

            if( oRow == null )
                return null;

            RowUpdate( oRow );

            // Text rect is reset to UL => 0,0. Now set this element's top down a bit and build around it.
            switch( eNeighborhood ) {
                case RefreshNeighborhood.CARET:
                    oRow.Top = LineHeight * 2; // Match the slop in RefreshCache() else new elem will be outside rect on first load() and get flushed!
                    break;
                case RefreshNeighborhood.SCROLL:
                    oRow.Top = 0; // If this is bottom line. We'll accumulate backwards to fix up cache.
                    break;
            }

            return oRow;
        }

        /// <summary>
        /// Simply trying to find the topmost cache element we can use. No need to be sorted.
        /// If the cache is missing lines (insert for example) we won't notice.
        /// Deleted lines are removed from the cache the minute they are deleted. 
        /// </summary>
        /// <returns>Highest previously cached element that looks valid.</returns>
        protected CacheRow FindLeast()
        {
            if( _rgOldCache.Count == 0 )
                return( null );

            // Find the closest valid line to the top of the TextRect.
            CacheRow oLeast   = null;
            int      iMinDist = int.MaxValue;

            for( int iRow = 0; iRow < _rgOldCache.Count; ++iRow ) {
                CacheRow oCurrLine = _rgOldCache[iRow];
                // If the element overlaps the textrect or is inside, use it. Might be
                // nice to add a check if the line is still in the buffer or has been deleted.
                if( oCurrLine.Top    <= TextRect[SCALAR.BOTTOM] + LineHeight * 2 &&
                    oCurrLine.Bottom >= TextRect[SCALAR.TOP   ] - LineHeight * 2 ) 
                { 
                    // Is element's top closest to the top of the textrect.
                    int iDistance = Math.Abs( oCurrLine.Top - TextRect[SCALAR.TOP] );
                    if( iDistance < iMinDist ) {
                        oLeast   = oCurrLine;
                        iMinDist = iDistance;
                    }
                    // Is element's bottom closest to the top of the textrect.
                    iDistance = Math.Abs( oCurrLine.Bottom - TextRect[SCALAR.TOP] );
                    if( iDistance < iMinDist ) {
                        oLeast   = oCurrLine;
                        iMinDist = iDistance;
                    }
                }
            }

            return( oLeast );
        }

        /// <summary>
        /// Find the next line after the line pointed to by the current cache element.
        /// Use an old element or create a new element and stack in the given direction.
        /// </summary>
        /// <param name="oRow">Element where we are building up the cache from.</param>
        /// <param name="iDir">Which direction to try.</param>
        /// <returns>New or recycled cache element stacked above or below previous.</returns>
        protected CacheRow RefreshNext( CacheRow oRow, int iDir ) {
            if( oRow == null )
                return( null );

            CacheRow oPrev = oRow;
            int      iLine = oRow.At + iDir;

            // See if we already the line computations. Given the old cache is sorted by
            // line I could potentially do a b-search. This find is a linear operation.
            oRow = _rgOldCache.Find( item => item.At == iLine );
            if( oRow == null ) { // If can't find matching elem, create it.
                Line oLine = _oSite.GetLine(iLine);

                // If we reach the top or bottom, the next line is going to be null.
                if( oLine == null ) {
                    return( null );
                }

                oRow = CreateRow( oLine );
            }

            if( oRow != null ) {
                if( oRow.IsInvalid ) {
                    RowUpdate( oRow );
                }
                if( iDir < 0 ) {
                    oRow.Top = oPrev.Top - oRow.Height - LineSpacing;
                } else {
                    oRow.Top = oPrev.Bottom + LineSpacing;
                }
            }

            return( oRow );
        }
        
        /// <summary>
        /// Find new gaps and create new cache elements for them.
        /// Note: Can't search for the line with the binary search since we're thrashing the order of the cache.
        /// </summary>
        /// <returns>True if the cache has any elements.</returns>
        protected bool CacheRefresh( CacheRow oTop ) {
            if( oTop == null ) {
                _oSite.LogError( "view cache", "Unable to find least or reseed new cache! We're hosed!" );
                return false;
            }

            if( oTop.IsInvalid ) {
                RowUpdate( oTop );
            }

            List<CacheRow> rgNewCache = new List<CacheRow> {
                oTop // Stick our seed in the new cache.
            };

            CacheRow oRow = oTop; // Current starting point.

            // Build downwards towards larger line numbers.
            while( oRow != null && oRow.Bottom < TextRect[SCALAR.BOTTOM] ) {
                CacheRow oPrev = oRow;
                oRow = RefreshNext( oRow, +1 );
                if( oRow == null ) {
                    // We're at the bottom, Re-shift the rect bottom to match bottom line so we don't have gap.
                    // This way we don't keep scrolling elements forever out of view.
                    TextRect.SetScalar(SET.RIGID, SCALAR.BOTTOM, oPrev.Bottom );
                } else {
                    rgNewCache.Add( oRow );
                }
            };

            CacheRow oBottom = oRow; // Last downward element we saved.
            oRow = oTop;

            // Build upwards towards smaller line numbers.
            while( oRow != null && oRow.Top > TextRect[SCALAR.TOP ] ) {
                CacheRow oPrev = oRow;
                oRow = RefreshNext( oRow, -1 );
                if( oRow == null ) {
                    // We're at the top. Re-shift the rect top to match top line so we don't have a gap.
                    // this way we don't keep scrolling elements forever out of view.
                    TextRect.SetScalar(SET.RIGID, SCALAR.TOP, oPrev.Top );
                } else {
                    rgNewCache.Add( oRow );
                }
            }

            oRow = oBottom;

            // Go down one last time in case the rect was moved up by the upward building loop. That
            // would create a gap that the downward builder didn't know about.
            while( oRow != null && oRow.Bottom < TextRect[SCALAR.BOTTOM] ) {
                CacheRow oPrev = oRow;
                oRow = RefreshNext( oRow, +1 );
                if( oRow != null ) {
                    rgNewCache.Add( oRow );
                }
            };
            
			// Sort by line order in case we forget the array order isn't always the window position order.
           _rgOldCache = rgNewCache;
           _rgOldCache.Sort( (x, y) => x.At - y.At ); 

            return _rgOldCache.Count != 0;
        } // end method

        /// <summary>
        /// Remeasure the text, get all the colors, and finally
        /// calculate each element height based on the provided width.
        /// </summary>
        /// <remarks>Note that the CacheList length MIGHT be 
        /// less than the CacheMap length! NOTE: If one of the elements throw it
        /// messes us up for the whole row. :-/ </remarks>
        /// <seealso cref="CheckList"/>
        protected virtual void RowUpdate( CacheRow oRow ) {
			try {
                for( int i=0; i<oRow.CacheList.Count && i<_rgCacheMap.Count; ++i ) {
                    IPgCacheMeasures oElem = oRow.CacheList[i];

				    oElem.Measure            ( Font );
                    oElem.OnChangeFormatting( _oSite.Selections );
                    oElem.OnChangeSize      ( _rgCacheMap[i].Width  );
                }
			} catch( Exception oEx ) {
				Type[] rgErrors = { typeof( NullReferenceException ),
									typeof( ArgumentNullException ),
                                    typeof( ArgumentOutOfRangeException ) };
				if( rgErrors.IsUnhandled( oEx ) )
					throw;

                _oSite.LogError( "view cache", "Error on row update request. Row: " + oRow.At.ToString() );
			}
        }

        /// <summary>
        /// Simply update invalid elements and restack. Use this when simply editing within one line.
		/// We redo the elem.top in case a line grows in height.
        /// </summary>
        protected void CacheValidate() {
			try {
				CacheRow oPrev = null;
				foreach( CacheRow oRow in _rgOldCache ) {
					if( oRow.IsInvalid )
						RowUpdate( oRow );

					// NOTE: if the elements aren't stacked in line order, we've got a problem.
					if( oPrev != null )
						oRow.Top = oPrev.Bottom + LineSpacing;

					oPrev = oRow;
				}
			} catch( Exception oEx ) {
				Type[] rgErrors = { typeof( NullReferenceException ),
									typeof( ArgumentNullException ),
                                    typeof( ArgumentOutOfRangeException ) };
				if( !rgErrors.Contains( oEx.GetType() ))
					throw;

				_oSite.LogError( "view cache", "Couldn't validate cache elements." );
			}
        }

        /// <summary>Invalidate ALL window cash elements.</summary>
        /// <remarks>
        /// Sort of odd. I found out that on the OnMultiFinished is not marking the lines
        /// as invalid, even tho the implication is that everthing is updated. Probably
        /// never noticed b/c the line change events would invalidate lines, but for
        /// the new BBC basic line renumber, none of that is used. (at present 7/9/2023)
        /// </remarks>
        /// <seealso cref="EditWindow2.OnMultiFinished"/>
        public void Invalidate() {
            foreach( CacheRow oRow in this ) {
                foreach( FTCacheLine oElem in oRow.CacheList ) {
                    oElem.IsInvalid = true;
                }
            }
        }

        /// <summary>
        /// Call this function when the user has scrolled or resized the screen.
        /// Or lines got inserted or deleted from the document. 
        /// Note: At present, we don't really know when a cut or paste has occured. This is kind of a drag.
        /// </summary>
        public void Refresh( 
            RefreshType         eReset,
            RefreshNeighborhood eNeighborhood
        ) {
            // Sort cache by line number so we can possibly b-search. I know, it's line number dependency.
            // Of course, finding holes is going to depend on the document lines getting renumberd
            // before our call.
            _rgOldCache.Sort( (x, y) => x.At - y.At ); 

            CacheRow oStart = null;

            switch( eReset ) {
                case RefreshType.RESET:
                    oStart = CacheReset( eNeighborhood );
                    break;
                case RefreshType.COMPLEX:
                    oStart = FindLeast();
                    break;
                case RefreshType.SIMPLE:
                    CacheValidate();
                    return;
            }

            // Couldn't find anything fall back to scroll pos! This can happen if lines are deleted.
            if( oStart == null ) {
                oStart = CacheReset( RefreshNeighborhood.SCROLL );
            }
            if( oStart != null ) {
                CacheRefresh( oStart );
            }

            _oSite.OnRefreshComplete();
        } // end method

        /// <summary>
        /// Create a cached line element. There are a lot of dependencies on stuff in this object
        /// and so we create the element here and pass it out to be used.
        /// </summary>
        /// <param name="oLine">The line we are trying to display.</param>
        /// <returns>A new row enough information to display the line on the screen.</returns>
        /// <remarks> Be sure to call RowUpdate()
        /// after this call so that the lines can be measured.</remarks>
        /// <seealso cref="RowUpdate"/>
        protected virtual CacheRow CreateRow( Line oLine ) {
            if( oLine == null ) {
                _oSite.LogError( "view cache", "Guest line must not be null for screen cache element." );
                return null;
            }

            CacheRow oRow = new CacheRowSingle( oLine );

            FTCacheLine oElem;

			if( _oSite.IsWrapped() ) {
				oElem = new FTCacheWrap( oLine ); // Heavy duty guy.
			} else {
				oElem = new FTCacheLine( oLine ); // Simpler object.
			}

            oRow.CacheList.Add( oElem );

            return oRow;
        }

        /// <summary>
        /// Precaching elements that I need measurements from, like next and
        /// previous lines for caret movement. For example, if the caret is
        /// one inch from the left margin and I attempt to move up to a wrapped
        /// line which has not been cached yet. 
        /// 
        /// The only hitch is that we sort of assume precached line
        /// actually stays in the cache on refresh or we wasted some effort.
        /// 
        /// TODO: If the line isn't in the cache, I could attempt to move
        /// the text rect by one line height, note: CaratLocal()
        /// based on current font, refreshing the cache. But that seems
        /// a bit like overkill given I know I'm only crawling up or down.
        /// one line. Or perhaps better, simply make sure I've got a cached elem
        /// above and/or below the view port at all times.
        /// </summary>
        public CacheRow PreCache( int iLineAt ) {
            // Check if the given edit is in the cache.
            foreach( CacheRow oTemp in _rgOldCache ) {
                if( oTemp.At == iLineAt )
                    return( oTemp );
            }

            // Try to find the requested line in the editor.
            Line oLine = _oSite.GetLine( iLineAt );
            if( oLine == null )
                return( null );

            // Line is not currently in cache, make a new elem.
            CacheRow oRow = CreateRow( oLine );

            if( oRow == null )
                return( null );

            RowUpdate( oRow );

            // We sort of assume the new element is going to be at the top
            // or bottom of the view and set only it's vertical placement.
            // Also assumes Line.At implies order in the buffer.
            foreach( CacheRow oTemp in _rgOldCache ) {
                if( oTemp.At == oRow.At + 1 ) { // Is the cache elem above?
                    oRow.Bottom = oTemp.Top - LineSpacing;
                    break;
                }
                if( oTemp.At == oRow.At - 1 ) { // Is the cache elem below?
                    oRow.Top    = oTemp.Bottom + LineSpacing;
                    break;
                }
            }

            _rgOldCache.Add( oRow );

            return oRow;
        }

        public struct GraphemeCollection : IEnumerable<IPgGlyph> {
            FTCacheLine _oCache;
            PgCluster   _oCluster;
            public GraphemeCollection( FTCacheLine oCache, PgCluster oCluster ) {
                _oCache   = oCache   ?? throw new ArgumentNullException();
                _oCluster = oCluster ?? throw new ArgumentNullException();
            }

            public IEnumerator<IPgGlyph> GetEnumerator() {
                return _oCache.ClusterCharacters( _oCluster );
            }

            IEnumerator IEnumerable.GetEnumerator() {
                return GetEnumerator();
            }
        }

        public static IPgGlyph[] _rgEmptyGlyph = new IPgGlyph[0];

        /// <summary>List all the codepoints that make up this character.</summary>
        /// <remarks>In this brave new world. A single grapheme can be made up of
        /// many codepoints! This brings up the issue of editing these multi point
        /// grapheme's but I'll side step that for the moment.</remarks>
        /// <exception cref="ArgumentException" />
        public IEnumerable<IPgGlyph> EnumGrapheme( ILineRange oCaret ) {
            if( oCaret == null || oCaret.Line == null )
                throw new ArgumentException( "Caret or Line on Caret is empty" );

            CacheRow oRow = CacheLocate( oCaret.At );

            if( oRow != null && oRow[0] is FTCacheLine oCache ) {
                PgCluster   oCluster = oCache.ClusterAt( oCaret.Offset );

                if( oCluster != null )
                    return new GraphemeCollection( oCache, oCluster );
            }

            return _rgEmptyGlyph;
        }

        /// <summary>
        /// Move the given caret offset on glyph in the direction specified. There are no side effects.
        /// </summary>
        /// <param name="eAxis">Horizontal or Vertical</param>
        /// <param name="iDir">How much to move by, usually +1 or -1.</param>
        /// <param name="flAdvance">Distance from left to maintain when moving vertically. This value can be updated.</param>
        /// <param name="oCaret">The caret to be updated.</param>
        /// <returns>Instructions on how to refresh the cache after this movement.</returns>
        public CaretMove MoveCaret( Axis eAxis, int iDir, ref float flAdvance, ILineRange oCaret ) {
            if( oCaret == null ) {
                _oSite.LogError( "view cache", "Caret pointer must not be null." );
                return CaretMove.LOCAL;
            }

            int iOffset = oCaret.Offset;

            // If total miss, build a new screen based on the location of the caret.
            CacheRow oRow = CacheLocate( oCaret.At );
            if( oRow == null ) {
                return CaretMove.MISS;
            }

            if( iDir != 0 ) {
                // First, see if we can navigate within the line we are currently at.
                if( !oRow.CacheList[0].Navigate( eAxis, iDir, ref flAdvance, ref iOffset ) ) {
                    iDir = iDir < 0 ? -1 : 1; // Only allow move one line up or down.

					// _rgLeft[_rgLeft.Count-1] = _oTextRect.GetScalar( SCALAR.WIDTH );

                    try {
                        CacheRow oNext = PreCache( oRow.At + iDir );
                        if( oNext != null ) {
                            // Find out where to place the cursor as it moves to the next line.
                            iOffset = oNext.CacheList[0].OffsetBound( eAxis, iDir * -1, flAdvance );
                            oRow    = oNext;
                        }
                    } catch( ArgumentOutOfRangeException ) {
                        // We're not throwing yet in PreCache, but this is what we want.
                    }
                }

                // If going up or down ends up null, we won't be moving the caret.
                oCaret.Line   = oRow.Line;
                oCaret.Offset = iOffset;
            }

            return CaretLocal( oRow, iOffset );
        }

        /// <summary>
        /// Look for a hit in the TextArea.
        /// </summary>
        /// <param name="oCaret"></param>
        /// <returns></returns>
        public bool IsHit( ILineRange oCaret ) {
            CacheRow oRow = CacheLocate( oCaret.At );

            if( oRow != null ) {
                Point pntCaretLoc = oRow.CacheList[0].GlyphOffsetToPoint( oCaret.Offset );

                pntCaretLoc.Y += oRow.Top;

                bool fTopIn = _oTextRect.IsInside( pntCaretLoc.X, pntCaretLoc.Y );
                bool fBotIn = _oTextRect.IsInside( pntCaretLoc.X, pntCaretLoc.Y + LineHeight );

                return fTopIn || fBotIn;
            }

            return false;
        }

        /// When the carat moves we try to determine what to do to keep the screen tracking
        /// it. But thinking about this, it's odd. Since we know the line it's on. Seems we
        /// could do a better job tracking with line instead of the world location which can
        /// be indeterminant if the carat line bounces out of the cache!! Need to think about this.

        /// Because we can resize the window. It's possible that the cursor can end up well out of
        /// the cache buffer. Also, for big fonts, the height of the cursor is an issue since a sliver
        /// of the top can be visible at the bottom. but we really want better than that.
        /// Also note, the cursor can potentially hit on the top and bottom!

        /// <summary>
        /// Update the sliding window based the offset into the given cache element.
        /// If calling this the caret is already nearby, so try to locate.
        /// </summary>
        /// <param name="oRow">The cache element to consider.</param>
        /// <param name="iOffset">Offset into the given cache element.</param>
        public CaretMove CaretLocal( CacheRow oRow, int iOffset ) {
            CaretMove eMove       = CaretMove.LOCAL;
            Point     pntCaretLoc = oRow.CacheList[0].GlyphOffsetToPoint( iOffset );

            pntCaretLoc.Y += oRow.Top;

            LOCUS eHitTop = _oTextRect.IsWhere( pntCaretLoc.X, pntCaretLoc.Y );
            LOCUS eHitBot = _oTextRect.IsWhere( pntCaretLoc.X, pntCaretLoc.Y + LineHeight );

            if( ( eHitTop & LOCUS.TOP    ) != 0 ) {
                _oTextRect.SetScalar(SET.RIGID, SCALAR.TOP,    pntCaretLoc.Y );
                eMove = CaretMove.NEARBY;
            }
            if( ( eHitBot & LOCUS.BOTTOM ) != 0 ) {
                _oTextRect.SetScalar(SET.RIGID, SCALAR.BOTTOM, pntCaretLoc.Y + LineHeight );
                eMove = CaretMove.NEARBY;
            }

            return eMove;
        }

        /// <summary>
        /// We used to simply call oCache.Update(), however, word wrapping doesn't work
        /// unless we call the resize too. So call RowUpdate for completeness.
        /// </summary>
        /// <remarks>Note: We just update and don't check if any of the elements are Invalid.</remarks>
        /// <seealso cref="RowUpdate"/>
        public void OnLineUpdated( Line oLine ) {
            foreach( CacheRow oCacheRow in _rgOldCache ) {
                if( oCacheRow.Line == oLine ) {
                    RowUpdate( oCacheRow );
                }
            }
        }

        /// <summary>
        /// TODO: Should invalidate our host window.
        /// </summary>
        public void OnLineAdded( Line oLine ) {
            OnLineUpdated( oLine );
        }

        /// <summary>
        /// TODO: This is lame for bulk deletes but is fine for a single line. Look to see
        /// if the new line is sequentially somewhere within our cached lines.
        /// </summary>
        public void OnLineDeleted( Line oLine ) {
            for( int i = 0; i<_rgOldCache.Count; ++i ) {
                if( _rgOldCache[i].Line == oLine ) {
                    _rgOldCache.RemoveAt( i );
                    break;
                }
            }
        }

        ///<summary>When formatting changes, that's typically because of a text change.</summary>
        ///<remarks>In CacheRefresh we get the Selections from the CacheMan Site. But here
        ///         we require it as a parameter. Need to think about that.
        ///         Currently updating the entire cache, only the line who's text has
        ///         changed, needs the "update"</remarks> 
        public void OnChangeFormatting( ICollection<ILineSelection> rgSelection, int iWidth ) {
            foreach( CacheRow oRow in _rgOldCache ) {
                IPgCacheMeasures oCache = oRow.CacheList[0];
              //oCache.Update( Font ); Just can't call this here. Too slow.
                oCache.OnChangeFormatting( rgSelection );
                oCache.OnChangeSize( iWidth );
            }
        }

        /// <remarks>
        /// Not again we're only updating the text area formatting.
        /// </remarks>
        public void OnChangeSelection( ICollection<ILineSelection> rgSelection ) {
            foreach( CacheRow oRow in _rgOldCache ) {
                oRow.CacheList[0].OnChangeFormatting( rgSelection );
            }
        }

        /// <summary>
        /// This function behaves like the OnScroll() and OnMouse() events. It modifies the 
        /// position of the cache rectangle. You must call Refresh() on the cache 
        /// after making this call.
        /// </summary>
        /// <remarks>All we really need out of our text rect is it's scrolling position.
        /// By now the layout is set and the height and width of the visible colums is
        /// already calculated.</remarks>
        /// <param name="rgSize">The new size of the rectangle.</param>
        public void OnChangeSize( Size oSize ) {
            _oTextRect.SetPoint(SET.RIGID, LOCUS.UPPERLEFT | LOCUS.EXTENT, oSize.Width, oSize.Height );

            // We've got to make sure we're sorted properly so we don't scramble the screen
            // when aligning. Sort by line is self healing but makes us dependant on line number.
            // But Sort by Top obviates that line# dependancy. Since Refresh() is self healing
            // I'm going with sort by Top here, so I can try to remove my line number dependancy
            // in all of the manager. NOTE: Using overload for Comparisson<T> not IComparer.
            _rgOldCache.Sort( (x, y) => x.Top.CompareTo( y.Top ) ); 

            if( _rgOldCache.Count == 0 )
                return;

            try {
                // The height of the elements might change because of the width change
                // and so the following siblings need to have their top's reset!
                int iTop = _rgOldCache[0].Top;
                foreach( CacheRow oRow in this ) {
                    oRow.Top = iTop;
                    iTop     = oRow.Bottom + LineSpacing; // Aligning.

                    for( int i=0; i< _rgCacheMap.Count; ++i ) {
                        SmartRect oColumn = _rgCacheMap[i];
                        oRow.CacheList[i].OnChangeSize( oColumn.Width );
                    }
                }
            } catch( Exception oEx ) {
                // if the _rgCacheMap and the oRow.CacheList don't match
                // we might walk of the end of one or the other.
                Type[] rgErrors = { typeof( NullReferenceException ),
                                    typeof( ArgumentOutOfRangeException ) };
                if( rgErrors.IsUnhandled( oEx ))
                    throw;
            }
        }

        /// <summary>
        /// Find the requested line.
        /// </summary>
        /// <param name="iLine">Line identifier. Technically for use, this does not need to be an
        /// array index. But just a unique value sitting at the "At" property on the line.</param>
        /// <returns>The cache element representing that line.</returns>
        public CacheRow CacheLocate( int iLine ) {
            foreach( CacheRow oCache in _rgOldCache ) {
                if( oCache.At == iLine ) {
                    return( oCache );
                }
            }

            return null;
        }

        /// <summary>
        /// Return the line offset position converted to World coordinates. CumulativeHeight
        /// and Cumulative Width. This is used to position the carat.
        /// </summary>
        /// <remarks>Again hard coded for text area.</remarks>
        /// <param name="oCaratPos">The Line and offset see are seeking.</param>
        /// <param name="pntWorld">world relative graphics coordinates.</param>
        public bool GlyphLineToPoint( int iCacheColumn, ILineRange oCaratPos, out Point pntWorld ) {
            CacheRow oRow = CacheLocate( oCaratPos.At );

            if( oRow != null ) {
                // This one returns local row/col in points(pixels) 0,0 ul of FTCacheLine
                pntWorld = oRow.CacheList[iCacheColumn].GlyphOffsetToPoint( oCaratPos.Offset );
                // This adds the vertical offset of the world.
                pntWorld.Y += oRow.Top;
                return true;
            } else {
                pntWorld = new Point( 0, 0 );
            }
            
            return false;
        }

        /// <summary>
        /// Given a point location attempt to locate the nearest line/glyph.
        /// </summary>
        /// <remarks>At this point we've probably located the textarea column the mouse click
        /// has occurred and we want to find which FTCacheLine it hits.
        /// If we want to edit in other columns we can do so by passing an
        /// argument.</remarks>
        /// <param name="oWorldLoc">Graphics location of interest in world coordinates. Basically
        ///                         where the mouse clicked.</param>
        /// <param name="oCaret">This object line offset is updated to the closest line offset.</param>
        public IPgCacheMeasures GlyphPointToRange( ref EditWindow2.WorldLocator oWorldLoc, ILineRange oCaret ) {
            try {
                foreach( CacheRow oRow in _rgOldCache ) {
                    if( oRow.Top    <= oWorldLoc.Y &&
                        oRow.Bottom >= oWorldLoc.Y ) 
                    {
                        // Just bail on the first one. Something is up...
                        if( oWorldLoc._iColumn < 0 || 
                            oWorldLoc._iColumn >= oRow.CacheList.Count )
                            return null;

                        IPgCacheMeasures oCache = oRow.CacheList[oWorldLoc._iColumn];

                        oCaret.Line   = oRow.Line;
                        oCaret.Offset = oCache.GlyphPointToOffset(oRow.Top, oWorldLoc._pntLocation );

                        return oCache;
                    }
                }
            } catch( Exception oEx ) {
                Type[] rgErrors = { typeof( ArgumentOutOfRangeException ),
                                    typeof( ArgumentException ),
                                    typeof( NullReferenceException ) };
                if( rgErrors.IsUnhandled( oEx ) )
                    throw;
                LogError( "CacheManager2.GlyphPointToRange exception" );
            }

            return null;
        }

        /// <summary>
        /// Find the location to render primary textarea text.
        /// Note: Currently not actually being used! O.o
        /// </summary>
        /// <seealso cref="RenderAt( CacheRow oCache, SmartRect rcColumn )"/>
        public PointF RenderAt( CacheRow oCache, Point pntScreenTL ) {
            SKPointI pntWorldTopLeft  = TextRect.GetPoint(LOCUS.UPPERLEFT);
            PointF   pntRenderAt      = new PointF( pntScreenTL.X - pntWorldTopLeft.X, 
                                                    pntScreenTL.Y + oCache.Top - pntWorldTopLeft.Y );

            return pntRenderAt;
        }

        //protected void RenderEOL( 
        //    SKCanvas                    skCanvas, 
        //    SKPaint                     skPaint,
        //    List<SKColor>               rgStdColors,
        //    ICollection<ILineSelection> rgSelectionTypes, 
        //    Point                       pntScreenTL ) 
        //{
        //    try {
        //        foreach( CacheRow oRow in this ) {
        //            FTCacheLine oCache = oRow.CacheList[0]; // TODO: Might not hold up...
        //            foreach( ILineSelection oSelection in rgSelectionTypes ) {
        //                if( oSelection.IsEOLSelected && 
        //                    oSelection.IsHit( oCache.Line ) )
        //                {
        //                    oCache.RenderEOL( skCanvas, skPaint, RenderAt( oRow, pntScreenTL ), rgStdColors, GlyphLt );
        //                }
        //            }
        //        }
        //    } catch( Exception oEx ) {
        //        Type[] rgError = { typeof( NullReferenceException ),
        //                           typeof( ArgumentOutOfRangeException ),
        //                           typeof( ArgumentNullException ) };
        //        if( rgError.IsUnhandled( oEx ) )
        //            throw;
        //    }
        //} // end method

        /// <summary>
        /// Links are those little "error here" marks. Now we're multi column, but since
        /// errors are generally main text only this will probably still work.
        /// </summary>
        /// <param name="pntTopLeft">Top left of all the edits. We add the vertical extents
        /// to arrive at our position.</param>
        //protected void RenderErrorMark( SKCanvas skCanvas, SKPaint skPaint, Point pntScreenTL )
        //{
        //    foreach( CacheRow oRow in this ) {
        //        FTCacheLine oCache = oRow.CacheList[0];
        //        oCache.RenderLinks( skCanvas, skPaint, RenderAt( oRow, pntScreenTL ) );
        //    }
        //} // end method

		/// <summary>
        /// Advance tells us how far along graphically, we are in the text stream
        /// from the left hand side, so if the cursor moves up or down we can try 
        /// to hit that same advance point. Updates the caret pos as a side effect.
        /// </summary>
        /// <param name="pntWorld">World Coordinates.</param>
        /// <remarks>Advance is modulo in the wrapped text case.</remarks>
        public void CaretAndAdvanceReset( ref EditWindow2.WorldLocator sWorldLoc, ILineRange oCaretPos, ref float flAdvance ) {
            IPgCacheMeasures oCache = GlyphPointToRange( ref sWorldLoc, oCaretPos );
            if( oCache != null ) {
                Point oNewLocation = oCache.GlyphOffsetToPoint( oCaretPos.Offset );

                flAdvance = oNewLocation.X; 
            }
        }
   } // end class
}
