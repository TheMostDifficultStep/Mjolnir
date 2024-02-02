using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

using SkiaSharp;

using Play.Interfaces.Embedding;
using Play.Parse;
using Play.Rectangles;
using System.Data.Common;

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
        int   CaretRow    { get; }
        int   CaretColumn { get; }
        int   CaretOffset { get; }

        bool SetCaretPositionAndScroll( int iRow, int iColumn, int iOffset ); 
    }
    public interface ICacheManSite :
        IPgBaseSite
    {
        Row            GetRowAtScroll();
        Row            GetRowAtIndex( int iIndex );
        void           OnRefreshComplete( Row oRowBottom, int iRowCount );

        //ICollection<ILineSelection> Selections{ get; }
    }
    public class CacheMultiColumn:         
        IEnumerable<CacheRow>,
        ICaretLocation
    {
        protected readonly ICacheManSite    _oSite;
        // World coordinates of our view port. Do not confuse these with the
        // layout columns, those are different.
        readonly SmartRect                  _oTextRect  = new SmartRect();
        protected List<CacheRow>            _rgOldCache = new List<CacheRow>();
        protected List<CacheRow>            _rgNewCache = new List<CacheRow>(); 

        protected readonly List<SmartRect>  _rgColumnRects;
        protected readonly TextLine         _oDummyLine = new TextLine( -2, string.Empty );

        protected IPgFontRender Font       { get; }
        protected IPgGlyph      GlyphLt    { get; } // Our end of line character.
        public    int           LineHeight { get; } // Helps us determine scrolling distances.
        public    int           RowSpacing { get; set; } = 1;

        protected Row   _oCaretRow; 
        protected int   _iCaretCol;
        protected int   _iCaretOff;
        protected float _fAdvance;

        static Type[] _rgStdErrors = { typeof( ArgumentNullException ),
                                       typeof( ArgumentException ),
                                       typeof( ArgumentOutOfRangeException ),
                                       typeof( IndexOutOfRangeException ),
                                       typeof( NullReferenceException ) };

        // TODO: Get the font from the site instead of from the constructor? Maybe?
        /// <remarks>Need to sort out the LineHeight accessor since the cache elements might be
        /// variable height, Need to make sure I'm using this correctly. Consider calling it
        /// "LineScroll"</remarks>
        public CacheMultiColumn( ICacheManSite oSite, IPgFontRender oFont, List<SmartRect> rgColumns ) :
			base() 
		{
			Font           = oFont     ?? throw new ArgumentNullException( "Need a font to get things rolling." );
			_oSite         = oSite     ?? throw new ArgumentNullException( "Cache manager is a sited object.");
            _rgColumnRects = rgColumns ?? throw new ArgumentNullException( "Columns list from Edit Window is null!" );

            GlyphLt    = Font.GetGlyph( 0x003c ); // we used to show carriage return as a '<' sign.
            LineHeight = (int)Font.LineHeight; // BUG: Cache elem's are variable height in general.

            _oCaretRow = _oSite.GetRowAtIndex( 0 );
            _iCaretCol = 0; // Make sure the column is edible :-/
            _iCaretOff = 0;
            _fAdvance  = 0;
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

        protected bool IsUnhandledStdRpt( Exception oEx ) {
            if( _rgStdErrors.IsUnhandled( oEx ) )
                return true;
            
            _oSite.LogError( "CacheMultiColumn", "Exception occurred" );

            return false;
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

        public int CaretRow {
            get {
                try {
                    if( _oCaretRow.At < 0 )
                        _oSite.LogError( "Cacheman", "Zombie Caret Row." );

                    return _oCaretRow.At; 
                } catch( NullReferenceException ) {
                    _oSite.LogError( "Cacheman", "Lost track of Caret Row (exception)." );
                }
                return 0; // Will cause a cache reset on caret moves.
            }
        }

        public int CaretColumn => _iCaretCol;

        public int CaretOffset => _iCaretOff;

        public void OnMouseWheel( int iDelta ) {
            int iTop = _rgOldCache[0].Top + ( 4 * iDelta / LineHeight );

            CacheScroll( iTop );
        }

        /// <summary>
        /// Slide the cache elements to the new top.
        /// </summary>
        /// <param name="iTop">A value relative to the TextRect.</param>
        protected void CacheScroll( int iTop ) {
            foreach( CacheRow oCacheRow in _rgOldCache ) {
                oCacheRow.Top = iTop;
                iTop += oCacheRow.Height;
            }

            CacheRow oSeedCache = CacheLocateTop();

            if( oSeedCache == null )
                oSeedCache = CacheReset( RefreshNeighborhood.SCROLL );

            CacheWalker( oSeedCache, false );
        }

        public void CacheResetFromThumb() {
            CacheWalker( CacheReset( RefreshNeighborhood.SCROLL ), false );
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
        /// This effectively resets the cache manager. We create a seed element
        /// chosen by rough scroll position or cursor as specified by the neighborhood.
        /// The buffer rectangle is reset so it's top is zero and the top of the new cache
        /// element is zero. Old cache is untouched and will need to be cleared.
        /// </summary>
        /// <seealso cref="CacheRecycle"/>
        protected CacheRow CacheReset( RefreshNeighborhood eNeighborhood ) {
            CacheRow oCacheRow = null;

            try {
		        // Ask our site for locating ourselves. Either based on our scroll
		        // position or carat depending on how we were called.
		        Row oDocRow = null;
                switch( eNeighborhood ) {
                    case RefreshNeighborhood.SCROLL:
                        oDocRow = _oSite.GetRowAtScroll( );
                        break;
                    case RefreshNeighborhood.CARET:
                        oDocRow = _oSite.GetRowAtIndex( CaretRow );
                        break;
                }
                if( oDocRow == null )
                    return null;

                oCacheRow = _rgOldCache.Find( item => item.At == oDocRow.At ); 

                if( oCacheRow == null ) // If can't find matching elem, create it.
                    oCacheRow = CreateCacheRow( oDocRow );

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
                if( IsUnhandledStdRpt( oEx ) )
                    throw;
            }

            return oCacheRow;
        }

        /// <summary>
        /// Try to get a new row first from the existing old cache.
        /// If that fails, make a new CacheRow. Returns null if
        /// the data element is out of bounds of the document.
        /// </summary>
        /// <param name="iDataRow">Which data row we want to represent.</param>
        /// <seealso cref="CacheReset"/>
        protected CacheRow CacheRecycle( CacheRow oPrevCache, int iDir, bool fRemeasure = false ) {
            Row oNextDRow = _oSite.GetRowAtIndex( oPrevCache.At + iDir );

            if( oNextDRow == null )
                return null;

            // TODO: Given the values are sorted I could bubble search.
            CacheRow oNewCache = _rgOldCache.Find( x => x.At == oNextDRow.At );

            // If we're reusing a cache, it's already measured!! ^_^
            if( oNewCache == null ) {
                oNewCache = CreateCacheRow( _oSite.GetRowAtIndex( oNextDRow.At ) );
                fRemeasure = true;
            }
            if( fRemeasure ) 
                RowMeasure( oNewCache );

            if( iDir > 0 )
                oNewCache.Top = oPrevCache.Bottom + RowSpacing;
            else
                oNewCache.Top = oPrevCache.Top - oNewCache.Height - RowSpacing;

            return oNewCache;
        }

        /// <summary>
        /// Right now we assume the cache managed part is basically
        /// starting from the top of the available window. This doesn't
        /// have to be and we could use the TextRect.Top as the starter.
        /// We would have to update the CacheWalker too...
        /// </summary>
        protected void CacheRestackFromTop() {
            int iTop = 0;
            foreach( CacheRow oCRow in _rgNewCache ) {
                oCRow.Top = iTop;

                iTop += oCRow.Height + RowSpacing;
            }
        }

        protected CacheRow CacheLocateTop() {
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
        /// Find the requested line.
        /// </summary>
        /// <param name="iRow">Line identifier. Technically for use, this does not need to be an
        /// array index. But just a unique value sitting at the "At" property on the line.</param>
        /// <returns>The cache element representing that line. or NULL</returns>
        protected CacheRow CacheLocate( int iRow ) {
            foreach( CacheRow oCache in _rgOldCache ) {
                if( oCache.At == iRow ) {
                    return oCache;
                }
            }

            return null;
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
        protected void CacheWalker( CacheRow oSeedCache, bool fRemeasure = false ) {
            if( oSeedCache == null ) {
                LogError( "Cache construction error" );
                return;
            }

            if( fRemeasure ) {
                RowMeasure( oSeedCache );
            }

            _rgNewCache.Clear();
            _rgNewCache.Add( oSeedCache );

            CacheRow oFallCache = oSeedCache; // First go down.
            while( oFallCache.Bottom < _oTextRect.Bottom ) {
                CacheRow oNewCache = CacheRecycle( oFallCache, 1, fRemeasure );
                if( oNewCache == null ) {
                    _oTextRect.SetScalar( SET.RIGID, SCALAR.BOTTOM, oFallCache.Bottom );
                    break;
                }

                oFallCache  = oNewCache;
                _rgNewCache.Add( oNewCache );
            }
            CacheRow oRiseCache = oSeedCache; // Then go up.
            while( oRiseCache.Top > _oTextRect.Top ) { 
                CacheRow oNewCache = CacheRecycle( oRiseCache, -1, fRemeasure );
                if( oNewCache == null ) {
                    _oTextRect.SetScalar( SET.RIGID, SCALAR.TOP, oRiseCache.Top );
                    break;
                }

                oRiseCache  = oNewCache;
                _rgNewCache.Insert( 0, oNewCache );
            }
            CacheRow oLastCache = oFallCache; // Then try down to finish filling.
            while( oLastCache.Bottom < _oTextRect.Bottom ) { 
                CacheRow oNewCache = CacheRecycle( oLastCache, 1, fRemeasure );
                if( oNewCache == null )
                    break;

                oLastCache = oNewCache;
                _rgNewCache.Add( oNewCache );
            }

            if( _oTextRect.Top != 0 ) {
                _oTextRect.SetScalar( SET.RIGID, SCALAR.TOP, 0 );
                CacheRestackFromTop();
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
        /// <remarks>BUG: Need to set the caret on scroll from keystrokes...</remarks>
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
        /// For now the main text area is our primary editing zone. The rest won't
        /// be editable for now.
        /// </summary>
        /// <remarks>Note that the CacheList length MIGHT be less than 
        /// the _rgColumnRects length!</remarks>
        /// <seealso cref="CheckList"/>
        protected virtual void RowMeasure( CacheRow oRow ) {
            try {
                for( int i=0; i<oRow.CacheList.Count && i<_rgColumnRects.Count; ++i ) {
                    FTCacheLine oElem = oRow.CacheList[i];

				    oElem.Update            ( Font /*, range */ );
                    oElem.OnChangeFormatting( null );
                    oElem.OnChangeSize      ( _rgColumnRects[i].Width );
                }
			} catch( Exception oEx ) {
                if( IsUnhandledStdRpt( oEx ) )
                    throw;
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
        protected virtual CacheRow CreateCacheRow( Row oDocRow ) {
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

        /// <summary>BUG: Fix this later... List all the codepoints that make up this character.</summary>
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
        /// Update the sliding window based the offset into the given cache element.
        /// We want to make sure the caret is always visible and doesn't slide off screen.
        /// The CacheWalker will normalize the window so the top is 0 (or whereever
        /// it should be)
        /// </summary>
        /// <param name="oCaret">The cache row where the caret is situated.</param>
        protected void CaretLocal( CacheRow oCaret ) {
            Point pntCaretLoc = oCaret.CacheList[_iCaretCol].GlyphOffsetToPoint( _iCaretOff );

            pntCaretLoc.Y += oCaret.Top;

            LOCUS eHitTop = _oTextRect.IsWhere( pntCaretLoc.X, pntCaretLoc.Y );
            LOCUS eHitBot = _oTextRect.IsWhere( pntCaretLoc.X, pntCaretLoc.Y + LineHeight );

            if( ( eHitTop & LOCUS.TOP    ) != 0 ) {
                _oTextRect.SetScalar(SET.RIGID, SCALAR.TOP,    pntCaretLoc.Y );
            }
            if( ( eHitBot & LOCUS.BOTTOM ) != 0 ) {
                _oTextRect.SetScalar(SET.RIGID, SCALAR.BOTTOM, pntCaretLoc.Y + LineHeight );
            }
        }

        /// <summary>
        /// Try to move the given caret offset on glyph in the direction specified.
        /// </summary>
        /// <param name="eAxis">Horizontal or Vertical</param>
        /// <param name="iDir">How much to move by, +1 or -1.</param>
        /// <exception cref="ArgumentOutOfRangeException" />
        public void CaretMove( Axis eAxis, int iDir ) {
            if( !( iDir == 1 || iDir == -1 ) )
                throw new ArgumentOutOfRangeException();

            try {
                CacheRow oCaretCacheRow = CacheLocate( CaretRow );
                if( oCaretCacheRow != null ) {
                    // First, see if we can navigate within the cache item we are currently at.
                    if( !oCaretCacheRow[_iCaretCol].Navigate( eAxis, iDir, ref _fAdvance, ref _iCaretOff ) ) {
                        // Now try moving vertically, but stay in the same column...
                        Row oDocRow = _oSite.GetRowAtIndex( CaretRow + iDir);
                        if( oDocRow != null ) {
                            CacheRow oNewCache = _rgOldCache.Find(item => item.At == oDocRow.At);
                            if( oNewCache == null ) {
                                oNewCache = CreateCacheRow(oDocRow);
                                RowMeasure( oNewCache );
                            }
                            if( iDir > 0 ) {
                                oNewCache.Top = oCaretCacheRow.Bottom + RowSpacing;
                            }
                            _oCaretRow = oDocRow;
                            _iCaretOff = oNewCache[_iCaretCol].OffsetBound( eAxis, iDir * -1, _fAdvance );
                            oCaretCacheRow = oNewCache;
                        }
                    }
                    CaretLocal( oCaretCacheRow );
                } else {
                    // Total miss, build a new screen based on the location of the caret.
                    oCaretCacheRow = CacheReset( RefreshNeighborhood.CARET );
                }

                CacheWalker( oCaretCacheRow );
            } catch( Exception oEx ) {
                if( IsUnhandledStdRpt( oEx ) )
                    throw;
            }
        }

        /// <summary>
        /// We used to simply call oCache.Update(), however, word wrapping doesn't work
        /// unless we call the resize too. So call RowUpdate for completeness.
        /// </summary>
        /// <remarks>Note: We just update and don't check if any of the elements are Invalid.</remarks>
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
        // see OnChangeSelection
        public void OnChangeFormatting( ICollection<ILineSelection> rgSelection ) {
            foreach( CacheRow oRow in _rgOldCache ) {
                foreach( FTCacheLine oCacheCol in oRow.CacheList ) {
                  //oCacheCol.Update( Font ); 
                    oCacheCol.OnChangeFormatting( rgSelection );
                  //oCacheCol.OnChangeSize( iWidth );
                }
            }
        }

        /// <remarks>
        /// Not again we're only updating the text area formatting.
        /// </remarks>
        //  see OnChangeFormatting
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
            try {
                // If the height changed, our buffer is all messed up.
                // There will be rows missing. And if the width changed
                // the amount of rows show gets affected too.
                // So on rebuild force a remeasure of ALL columns.
                CacheRow oSeedCache = CacheLocateTop();

                if( oSeedCache == null )
                    oSeedCache = CacheReset( RefreshNeighborhood.SCROLL );

                CacheWalker( oSeedCache, fRemeasure:true );
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
        /// Given a point location attempt to locate the nearest cached line/glyph.
        /// </summary>
        /// <remarks>At this point we've probably located the textarea column the mouse click
        /// has occurred and we want to find which FTCacheLine it hits.
        /// If we want to edit in other columns we can do so by passing an
        /// argument.</remarks>
        /// <param name="pntWorldLoc">Graphics location of interest in world coordinates. Basically
        ///                         where the mouse clicked.</param>
        /// <param name="oCaret">This object line offset is updated to the closest line offset.</param>
        public bool PointToRow( 
            int iColumn, SKPointI pntWorldLoc, out int iOffset, out int iRow )
        {
            try {
                foreach( CacheRow oCacheRow in _rgOldCache ) {
                    if( oCacheRow.Top    <= pntWorldLoc.Y &&
                        oCacheRow.Bottom >= pntWorldLoc.Y ) 
                    {
                        FTCacheLine oCache   = oCacheRow.CacheList[iColumn];
                        SKPointI    pntLocal = new SKPointI( pntWorldLoc.X - _rgColumnRects[iColumn].Left,
                                                             pntWorldLoc.Y - _rgColumnRects[iColumn].Top );

                        iOffset = oCache   .GlyphPointToOffset(oCacheRow.Top, pntLocal );
                        iRow    = oCacheRow.At;

                        return true;
                    }
                }
            } catch( Exception oEx ) {
                if( IsUnhandledStdRpt( oEx ) )
                    throw;
            }

            iOffset = -1;
            iRow    = -1;

            return false;
        }

		/// <summary>
        /// Advance tells us how far along graphically, we are in the text stream
        /// from the left hand side, so if the cursor moves up or down we can try 
        /// to hit that same advance point. Updates the caret pos as a side effect.
        /// </summary>
        /// <param name="pntWorld">Point in our cache space.</param>
        /// <remarks>Advance is modulo in the wrapped text case. Also a miss is
        /// is ok since user is probably using this on a mouse click.</remarks>
        public void CaretAndAdvanceReset( SKPointI pntWorldLoc ) {
            try {
                for( int iColumn=0; iColumn<_rgColumnRects.Count; ++iColumn ) {
                    SmartRect oColumn = _rgColumnRects[iColumn];

                    if( oColumn.IsInside( pntWorldLoc.X, pntWorldLoc.Y ) ) {
                        // Only care if the point is within the cache.
                        if( PointToRow( iColumn, pntWorldLoc, out int iOff, out int iRow ) ) {
                            _oCaretRow = _oSite.GetRowAtIndex( iRow );
                            _iCaretCol = iColumn;
                            _iCaretOff = iOff;
                            _fAdvance  = pntWorldLoc.X - oColumn.Left;
                        }
                    }
                }
            } catch( Exception oEx ) {
                if( IsUnhandledStdRpt( oEx ) )
                    throw;
            }
        }

        /// <summary>
        /// Move the caret to a new position. Usually as a result of moving the
        /// caret to some formatting/hyperlink/mouse position.
        /// </summary>
        /// <seealso cref="ScrollToCaret"/>
        /// <exception cref="ArgumentOutOfRangeException" />
        public bool SetCaretPositionAndScroll( int iDataRow, int iColumn, int iOffset ) {
            try {
                Row  oDataRow = _oSite.GetRowAtIndex( iDataRow );
                Line oLine    = oDataRow[iColumn];

                if( iOffset < 0 || iOffset >= oLine.ElementCount )
                    return false;

                _oCaretRow = oDataRow;
                _iCaretCol = iColumn;
                _iCaretOff = iOffset;

                ScrollToCaret();
            } catch( Exception oEx ) {
                if( IsUnhandledStdRpt( oEx ) )
                    throw;

                return false;
            }

            return true;
        }

        public void ScrollToCaret() {
            CacheRow oCaretCacheRow = CacheLocate( _oCaretRow.At );

            if( oCaretCacheRow != null ) {
                CaretLocal( oCaretCacheRow );
            } else {
                oCaretCacheRow = CacheReset( RefreshNeighborhood.CARET );
            }

            CacheWalker( oCaretCacheRow );
        }

    } // end class
}
