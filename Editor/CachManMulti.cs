using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

using SkiaSharp;

using Play.Interfaces.Embedding;
using Play.Parse;
using Play.Rectangles;

namespace Play.Edit {
    public interface IRowRange : IMemoryRange {
        Row Row { get; }
    }

    public enum Trek {
        Next,
        Prev
    }

    public enum Bump {
        OK,
        BumpLow,
        BumpHigh,
        Stop
    }

    /// <summary>
    /// Note: column order on screen might not be the same as that
    /// for the document. O.o
    /// </summary>
    public interface ICaretLocation {
        Row   CurrentRow  { get; }
        Line  CurrentLine { get; }
        int   CharOffset  { get; }
        float Advance     { get; }

        bool SetCaretPosition( int iRow, int iColumn, int iOffset ); // Vertical move
        bool SetCaretPosition( int iColumn, float fAdvance );        // Horizontal move

    }
    public interface ICacheManSite :
        IPgBaseSite
    {
        Row            GetRowAtHood( RefreshNeighborhood eHood );
        Row            GetRowAtIndex( int iIndex );
        void           OnRefreshComplete( Row oRowBottom, int iRowCount );

        //ICollection<ILineSelection> Selections{ get; }
    }
    public class CacheMultiColumn:         
        IEnumerable<CacheRow>
    {
        protected readonly ICacheManSite    _oSite;
        // World coordinates of our view port. Do not confuse these with the
        // layout columns, those are different.
        readonly SmartRect                  _oTextRect  = new SmartRect();
        protected List<CacheRow>            _rgOldCache = new List<CacheRow>();
        protected List<CacheRow>            _rgNewCache = new List<CacheRow>(); 

        protected readonly List<SmartRect>  _rgCacheMap;
        protected readonly TextLine         _oDummyLine = new TextLine( -2, string.Empty );

        protected IPgFontRender Font       { get; }
        protected IPgGlyph      GlyphLt    { get; } // Our end of line character.
        public    int           LineHeight { get; } // Helps us determine scrolling distances.
        public    int           RowSpacing { get; set; } = 1;

        // TODO: Get the font from the site instead of from the constructor? Maybe?
        /// <remarks>Need to sort out the LineHeight accessor since the cache elements might be
        /// variable height, Need to make sure I'm using this correctly. Consider calling it
        /// "LineScroll"</remarks>
        public CacheMultiColumn( ICacheManSite oSite, IPgFontRender oFont, List<SmartRect> rgColumns ) :
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
        /// Count of number of CacheRow objects inside the manager.
        /// </summary>
        public int Count {
            get { return _rgOldCache.Count; }
        }

        public SmartRect TextRect {
            get { return _oTextRect; }
        }

        public void OnMouseWheel( int iDelta ) {
            int iTop = _rgOldCache[0].Top + ( 4 * iDelta / LineHeight );

            CacheScroll( iTop );
        }

        protected void CacheScroll( int iTop ) {
            foreach( CacheRow oCacheRow in _rgOldCache ) {
                oCacheRow.Top = iTop;
                iTop += oCacheRow.Height;
            }

            CacheRow oSeedCache = FindTop();

            if( oSeedCache == null )
                oSeedCache = CacheReset( RefreshNeighborhood.SCROLL );

            LukeCacheWalker( oSeedCache );
        }

        public void CacheResetFromThumb() {
            LukeCacheWalker( CacheReset( RefreshNeighborhood.SCROLL ) );
        }

        /// <summary>
        /// If part of the cache row is overrlapping the visible area,
        /// we need to clip that off so we calculate the proper number of
        /// rows we need to fill the screen.
        /// </summary>
        /// <param name="oCacheRow">CRow with top & bottom set.</param>
        protected int ClippedHeight( CacheRow oCacheRow ) {
            // Above the visible portion.
            if( oCacheRow.Bottom < 0 )
                return 0;

            // Below the visible portion.
            if( oCacheRow.Top > _oTextRect.Height )
                return 0;

            int iTop = oCacheRow.Top;
            int iBot = oCacheRow.Bottom;

            // Top is peaking over the visible portion with bottom inside or below.
            if( oCacheRow.Top < 0 ) 
                iTop = 0;

            if( oCacheRow.Bottom > _oTextRect.Height )
                iBot = _oTextRect.Height;

            int iHeight = iBot - iTop + 1;

            if( iHeight < 0 )
                return 0;

            return iHeight;
        }

        /// <summary>
        /// Try to get a new row first from the existing old cache.
        /// If that fails, make a new CacheRow. Returns null if
        /// the data element is out of bounds of the document.
        /// </summary>
        /// <param name="iDataRow">Which data row we want to represent.</param>
        protected CacheRow RecycleCacheRow( CacheRow oPrevCache, int iDir ) {
            Row oNextDRow = _oSite.GetRowAtIndex( oPrevCache.At + iDir );

            if( oNextDRow == null )
                return null;

            // TODO: Given the values are sorted I could bubble search.
            CacheRow oNewCache = _rgOldCache.Find( x => x.At == oNextDRow.At );

            // If we're reusing a cache, it's already measured!! ^_^
            if( oNewCache == null ) {
                oNewCache = CreateRow( _oSite.GetRowAtIndex( oNextDRow.At ) );
                RowMeasure( oNewCache );
            }

            if( iDir > 0 )
                oNewCache.Top = oPrevCache.Bottom + RowSpacing;
            else
                oNewCache.Top = oPrevCache.Top - oNewCache.Height - RowSpacing;

            return oNewCache;
        }

        protected void RestackNewCacheFromTop() {
            int iTop = 0;
            foreach( CacheRow oCRow in _rgNewCache ) {
                oCRow.Top = iTop;

                iTop += oCRow.Height + RowSpacing;
            }
        }

        public CacheRow FindTop() {
            CacheRow oSeedCache = null;
            if( _rgOldCache.Count > 0 ) {
                int iTop = _oTextRect.Height;
                foreach( CacheRow oTestRow in _rgOldCache ) {
                    if( ClippedHeight( oTestRow ) > 0 && oTestRow.At >= 0 ) {
                        if( oTestRow.Top < iTop ) {
                            oSeedCache = oTestRow;
                            iTop     = oTestRow.Top;
                        }
                    }
                }
            }

            return oSeedCache;
        }

        /// <summary>
        /// New code for re-building the cache. Not entirely different
        /// than the original. Omits the sliding window concept.
        /// Calls OnRefreshComplete() on the site when finished.
        /// </summary>
        /// <remarks>The key to remember is that if any element overlaps the
        /// visible area, the outside part DOES NOT contribute. That's why
        /// we check the clipped height!!</remarks>
        /// <seealso cref="ICacheManSite.OnRefreshComplete"/>
        public void LukeCacheWalker( CacheRow oSeedCache ) {
            if( oSeedCache == null ) {
                LogError( "Cache construction error" );
                return;
            }

            _rgNewCache.Clear();
            _rgNewCache.Add( oSeedCache );

            int      iDistance  = 0;
            CacheRow oFallCache = oSeedCache; // First go down.
            while( oFallCache.Bottom < _oTextRect.Height ) {
                CacheRow oNewCache = RecycleCacheRow( oFallCache, 1 );
                if( oNewCache == null )
                    break;

                iDistance  += ClippedHeight( oNewCache );
                oFallCache  = oNewCache;
                _rgNewCache.Add( oNewCache );
            }
            CacheRow oRiseCache = oSeedCache; // Then go up.
            while( oRiseCache.Top > 0 ) { 
                CacheRow oNewCache = RecycleCacheRow( oRiseCache, -1 );
                if( oNewCache == null ) 
                    break;

                iDistance  += ClippedHeight( oNewCache );
                oRiseCache  = oNewCache;
                _rgNewCache.Insert( 0, oNewCache );
            }
            CacheRow oLastCache = oFallCache; // Then try down to finish filling.
            while( iDistance < _oTextRect.Height ) { 
                CacheRow oNewCache = RecycleCacheRow( oLastCache, 1 );
                if( oNewCache == null )
                    break;

                iDistance += oNewCache.Height + RowSpacing;
                oLastCache = oNewCache;
                _rgNewCache.Add( oNewCache );
            }

            if( oRiseCache.Top > 0 ) {
                RestackNewCacheFromTop();
            }

            _rgOldCache.Clear();
            _rgOldCache.AddRange( _rgNewCache );
            _rgNewCache.Clear();

            Row oLastDRow = null;

            if( oLastCache is CacheRow2 oLastCache2 )
                oLastDRow = oLastCache2.DataRow;

            _oSite.OnRefreshComplete( oLastDRow, _rgOldCache.Count );
        }

        /// <summary>
        /// Visual Studio editor moves the caret too. But if we scroll with the
        /// mouse or scroll bar they don't move the caret... hmmm...
        /// </summary>
        /// <param name="e"></param>
        public void OnScrollBar_Vertical( ScrollEvents e ) {
            switch( e ) {
                // These events move incrementally from where we were.
                case ScrollEvents.LargeDecrement:
                    CacheScroll( (int)(.80 * _oTextRect.Height ) );
                    break; 
                case ScrollEvents.LargeIncrement:
                    CacheScroll( (int)(.80 * - _oTextRect.Height ) );
                    break;
                case ScrollEvents.SmallDecrement:
                    CacheScroll( LineHeight );
                    break;
                case ScrollEvents.SmallIncrement:
                    CacheScroll( -LineHeight );
                    break;

                // We can potentialy render less until this final end scroll comes in.
                case ScrollEvents.EndScroll:
                case ScrollEvents.First:
                case ScrollEvents.Last:
                case ScrollEvents.ThumbPosition:
                case ScrollEvents.ThumbTrack:
                    CacheResetFromThumb();
                    break;
            }
        }

        /// <summary>
        /// This effectively resets the cache manager. We create a seed element
        /// chosen by rough scroll position or cursor as specified by the neighborhood.
        /// The buffer rectangle is reset so it's top is zero and the top of the new cache
        /// element is zero. Old cache is untouched and will need to be cleared.
        /// </summary>
        /// <seealso cref="PreCache(Row)"/>
        /// <seealso cref="RecycleCacheRow"/>
        protected CacheRow CacheReset( RefreshNeighborhood eNeighborhood ) {
            CacheRow oCacheRow = null;

            try {
		        // Ask our site for locating ourselves. Either based on our scroll
		        // position or carat depending on how we were called.
		        Row oDocRow = _oSite.GetRowAtHood( eNeighborhood );
                if( oDocRow == null )
                    return null;

                oCacheRow = _rgOldCache.Find( item => item.At == oDocRow.At ); 

                if( oCacheRow == null ) // If can't find matching elem, create it.
                    oCacheRow = CreateRow( oDocRow );

                RowMeasure( oCacheRow );

                // Text rect is reset to UL => 0,0. Now set this element's top down a bit and build around it.
                switch( eNeighborhood ) {
                    case RefreshNeighborhood.CARET:
                        oCacheRow.Top = LineHeight * 2; // Match the slop in RefreshCache() else new elem will be outside rect on first load() and get flushed!
                        break;
                    case RefreshNeighborhood.SCROLL:
                        oCacheRow.Top = 0; // If this is bottom line. We'll accumulate backwards to fix up cache.
                        break;
                }
            } catch( Exception oEx ) {
                Type[] rgErrors = { typeof( NullReferenceException ),
                                    typeof( ArgumentNullException ) };
                if( rgErrors.IsUnhandled( oEx ) )
                    throw;
            }

            return oCacheRow;
        }

        /// <summary>
        /// For now the main text area is our primary editing zone. The rest won't
        /// be editable for now.
        /// </summary>
        /// <remarks>Note that the CacheList length MIGHT be less than the CacheMap length!</remarks>
        /// <seealso cref="CheckList"/>
        protected virtual void RowMeasure( CacheRow oRow ) {
            for( int i=0; i<oRow.CacheList.Count && i<_rgCacheMap.Count; ++i ) {
                ElemUpdate( oRow.CacheList[i], _rgCacheMap[i].Width );
            }
        }

        protected void ElemUpdate( FTCacheLine oElem, int iWidth, IMemoryRange oRange = null ) {
			try {
				oElem.Update            ( Font, oRange );
              //oElem.OnChangeFormatting( _oSite.Selections );
                oElem.OnChangeSize      ( iWidth );
			} catch( Exception oEx ) {
				Type[] rgErrors = { typeof( NullReferenceException ),
									typeof( ArgumentNullException ),
                                    typeof( ArgumentOutOfRangeException ) };
				if( !rgErrors.Contains( oEx.GetType() ))
					throw;

                _oSite.LogError( "view cache", "Update request on empty element" );
			}
        }

        /// <summary>
        /// Simply update invalid elements and restack. Use this when simply editing within one line.
		/// We redo the elem.top in case a line grows in height.
        /// </summary>
        protected void Validate() {
			try {
				CacheRow oPrev = null;
				foreach( CacheRow oRow in _rgOldCache ) {
					if( oRow.IsInvalid )
						RowMeasure( oRow );

					// NOTE: if the elements aren't stacked in line order, we've got a problem.
					if( oPrev != null )
						oRow.Top = oPrev.Bottom + RowSpacing; // BUG: this looks wrong...

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

        /// <summary>Invalidate ALL window cash elements. probably the same code
        /// as OnChangeSize. Might unify if possible...</summary>
        /// <remarks>
        /// Sort of odd. I found out that on the OnMultiFinished is not marking the lines
        /// as invalid, even tho the implication is that everthing is updated. Probably
        /// never noticed b/c the line change events would invalidate lines, but for
        /// the new BBC basic line renumber, none of that is used. (at present 7/9/2023)
        /// </remarks>
        /// <seealso cref="EditWindow2.OnMultiFinished"/>
        /// <seealso cref="OnChangeSize"/>
        public void Invalidate() {
            foreach( CacheRow oRow in this ) {
                foreach( FTCacheLine oElem in oRow.CacheList ) {
                    oElem.Invalidate();
                }
            }
        }

        /// <summary>
        /// Create a cached line element. There are a lot of dependencies on stuff in this object
        /// and so we create the element here and pass it out to be used.
        /// </summary>
        /// <param name="oLine">The line we are trying to display.</param>
        /// <returns>A new row enough information to display the line on the screen.</returns>
        /// <remarks> Be sure to call RowUpdate()
        /// after this call so that the lines can be measured.</remarks>
        /// <seealso cref="RowMeasure"/>
        protected virtual CacheRow CreateRow( Row oDocRow ) {
            if( oDocRow == null )
                throw new ArgumentNullException();

            CacheRow oCacheRow = new CacheRow2( oDocRow );

            // TODO: Change this to use a dummy cache element too...
            foreach( Line oLine in oDocRow ) {
				Line oTemp = oLine;
                if( oTemp == null ) {
                    oTemp = _oDummyLine;
                }
                FTCacheLine oElem = new FTCacheWrap( oTemp );
                oCacheRow.CacheList.Add( oElem );
			}

            return oCacheRow;
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
        /// a bit like overkill given I know I'm only crawling up or down
        /// one line. Or perhaps better, simply make sure I've got a cached elem
        /// above and/or below the view port at all times.
        /// </summary>
        [Obsolete]public CacheRow PreCache( Row oNextRow ) {
            // Check if the given edit is in the cache.
            foreach( CacheRow oTemp in _rgOldCache ) {
                if( oTemp.At == oNextRow.At )
                    return oTemp;
            }

            // Line is not currently in cache, make a new elem.
            CacheRow oRow = CreateRow( oNextRow );

            RowMeasure( oRow ); // Need to know CacheRow height...

            // We sort of assume the new element is going to be at the top
            // or bottom of the view and set only it's vertical placement.
            // Also assumes Line.At implies order in the buffer.
            foreach( CacheRow oTemp in _rgOldCache ) {
                if( oTemp.At == oRow.At + 1 ) { // Is the cache elem above?
                    oRow.Bottom = oTemp.Top - RowSpacing;
                    break;
                }
                if( oTemp.At == oRow.At - 1 ) { // Is the cache elem below?
                    oRow.Top    = oTemp.Bottom + RowSpacing;
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

            if( oRow == null ) 
                return _rgEmptyGlyph;

            FTCacheLine oCache   = oRow.CacheList[0];
            PgCluster   oCluster = oCache.ClusterAt( oCaret.Offset );

            if( oCluster == null )
                return _rgEmptyGlyph;

            return new GraphemeCollection( oCache, oCluster );
        }

        /// <summary>
        /// Move the given caret offset on glyph in the direction specified. There are no side effects.
        /// </summary>
        /// <param name="eAxis">Horizontal or Vertical</param>
        /// <param name="iDir">How much to move by, usually +1 or -1.</param>
        /// <param name="flAdvance">Distance from left to maintain when moving vertically. This value can be updated.</param>
        /// <param name="oCaret">The caret to be updated.</param>
        /// <returns>Instructions on how to refresh the cache after this movement.</returns>
        public CaretMove MoveCaret( Axis           eAxis, 
                                    int            iDir, 
                                    ICaretLocation oCaretPos ) 
        {
            if( oCaretPos == null )
                throw new ArgumentNullException();
            if( iDir == 0 )
                throw new ArgumentOutOfRangeException();

            // If total miss, build a new screen based on the location of the caret.
            CacheRow oCacheRow = CacheLocate( oCaretPos.CurrentRow.At );
            if( oCacheRow == null ) {
                return CaretMove.MISS;
            }

            // First, see if we can navigate within the row we are currently at.
            if( !oCacheRow.CacheList[0].Navigate( eAxis, iDir, oCaretPos ) ) {
                iDir = iDir < 0 ? -1 : 1; // Only allow move one line up or down.
                // Now try moving vertically...
                Row oDocRow = _oSite.GetRowAtIndex( oCaretPos.CurrentRow.At + iDir );
                if( oDocRow != null ) {
                    CacheRow oNext = PreCache( oDocRow );

                    // Find out where to place the cursor as it moves to the next line.
                    int iOffset = oNext.CacheList[0].OffsetBound( eAxis, iDir * -1, oCaretPos.Advance );

                    oCaretPos.SetCaretPosition( oCacheRow.At, 0, iOffset );
                }
            }

            return CaretLocal( oCacheRow, oCaretPos );
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
        protected CaretMove CaretLocal( CacheRow oRow, ICaretLocation oCaretPos ) {
            CaretMove eMove       = CaretMove.LOCAL;
            Point     pntCaretLoc = oRow.CacheList[0].GlyphOffsetToPoint( oCaretPos.CharOffset );

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
        /// <seealso cref="ElemUpdate"/>
        public void OnLineUpdated( Line oLine ) {
            foreach( CacheRow oRow in _rgOldCache ) {
                if( oRow.Line == oLine ) {
                    RowMeasure( oRow );
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

        public void UpdateAllRows() {
            foreach( CacheRow oRow in _rgOldCache ) {
                RowMeasure( oRow );
            }
        }

        /// <summary>
        /// I need to make the Row available on the CacheRow but this will work for
        /// now I think.
        /// </summary>
        public void UpdateRow( Row oDocRow ) {
            foreach( CacheRow oCacheRow in _rgOldCache ) {
                if( oCacheRow.At == oDocRow.At ) {
                    RowMeasure( oCacheRow );
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
                FTCacheLine oCache = oRow.CacheList[0];
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
        public void OnChangeSize() {
            if( _rgOldCache.Count == 0 )
                return;

            try {
                // Let the existing items remeasure.
                foreach( CacheRow oRow in this ) {
                    for( int i=0; i< _rgCacheMap.Count; ++i ) {
                        SmartRect   oColumn = _rgCacheMap[i];
                        FTCacheLine oCache  = oRow.CacheList[i];

                        oCache.OnChangeSize( oColumn.Width );
                    }
                }
                // Call this in case we need to add new rows...
                CacheRow oSeedCache = FindTop();

                if( oSeedCache == null )
                    oSeedCache = CacheReset( RefreshNeighborhood.SCROLL );

                LukeCacheWalker( oSeedCache );
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
        public FTCacheLine GlyphPointToRange( ref EditWindow2.WorldLocator oWorldLoc, ILineRange oCaret ) {
            foreach( CacheRow oRow in _rgOldCache ) {
                if( oRow.Top    <= oWorldLoc.Y &&
                    oRow.Bottom >= oWorldLoc.Y ) 
                {
                    FTCacheLine oCache = oRow.CacheList[oWorldLoc._iColumn];

                    oCaret.Line   = oCache.Line;
                    oCaret.Offset = oCache.GlyphPointToOffset(oRow.Top, oWorldLoc._pntLocation );

                    return oCache;
                }
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

        public void RenderEOL( 
            SKCanvas                    skCanvas, 
            SKPaint                     skPaint,
            List<SKColor>               rgStdColors,
            ICollection<ILineSelection> rgSelectionTypes, 
            Point                       pntScreenTL ) 
        {
            try {
                foreach( CacheRow oRow in this ) {
                    FTCacheLine oCache = oRow.CacheList[0]; // TODO: Might not hold up...
                    foreach( ILineSelection oSelection in rgSelectionTypes ) {
                        if( oSelection.IsEOLSelected && 
                            oSelection.IsHit( oCache.Line ) )
                        {
                            oCache.RenderEOL( skCanvas, skPaint, RenderAt( oRow, pntScreenTL ), rgStdColors, GlyphLt );
                        }
                    }
                }
            } catch( Exception oEx ) {
                Type[] rgError = { typeof( NullReferenceException ),
                                   typeof( ArgumentOutOfRangeException ),
                                   typeof( ArgumentNullException ) };
                if( rgError.IsUnhandled( oEx ) )
                    throw;
            }
        } // end method

        /// <summary>
        /// Links are those little "error here" marks. Now we're multi column, but since
        /// errors are generally main text only this will probably still work.
        /// </summary>
        /// <param name="pntTopLeft">Top left of all the edits. We add the vertical extents
        /// to arrive at our position.</param>
        public void RenderErrorMark( SKCanvas skCanvas, SKPaint skPaint, Point pntScreenTL )
        {
            foreach( CacheRow oRow in this ) {
                FTCacheLine oCache = oRow.CacheList[0];
                oCache.RenderLinks( skCanvas, skPaint, RenderAt( oRow, pntScreenTL ) );
            }
        } // end method

		/// <summary>
        /// Advance tells us how far along graphically, we are in the text stream
        /// from the left hand side, so if the cursor moves up or down we can try 
        /// to hit that same advance point. Updates the caret pos as a side effect.
        /// </summary>
        /// <param name="pntWorld">World Coordinates.</param>
        /// <remarks>Advance is modulo in the wrapped text case.</remarks>
        public void CaretAndAdvanceReset( ref EditWindow2.WorldLocator sWorldLoc, ILineRange oCaretPos, ref float flAdvance ) {
            FTCacheLine oCache = GlyphPointToRange( ref sWorldLoc, oCaretPos );
            if( oCache != null ) {
                Point oNewLocation = oCache.GlyphOffsetToPoint( oCaretPos.Offset );

                flAdvance = oNewLocation.X; 
            }
        }
   } // end class
}
