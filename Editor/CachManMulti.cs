using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;

using SkiaSharp;

using Play.Interfaces.Embedding;
using Play.Parse;
using Play.Rectangles;

namespace Play.Edit {
    public interface IPgCaretInfo<T> :
        IMemoryRange
    {
        T   Row    { get; }
        int Column { get; }
    }

    public interface ICacheManSite :
        IPgBaseSite
    {
        float GetScrollProgress { get; }
        void  OnRefreshComplete( float flProgress, float flVisiblePercent );
        void  OnCaretPositioned( SKPointI pntCaretbool, bool fVisible );
    }
    public class CacheMultiColumn:         
        IEnumerable<CacheRow>
    {
        protected readonly ICacheManSite     _oSite;
        protected readonly IReadableBag<Row> _oSiteList;
        // World coordinates of our view port. Do not confuse these with the
        // layout columns, those are different.
        protected readonly SmartRect       _oTextRect  = new SmartRect();
        protected readonly List<CacheRow>  _rgOldCache = new List<CacheRow>();
        protected readonly List<CacheRow>  _rgNewCache = new List<CacheRow>(); 

        protected readonly List<SmartRect> _rgColumnRects;
        protected readonly TextLine        _oDummyLine = new TextLine( -2, string.Empty );

        protected IPgFontRender Font       { get; }
        protected IPgGlyph      GlyphLt    { get; } // Our end of line character.
        public    int           LineHeight { get; } // Helps us determine scrolling distances.
        public    int           RowSpacing { get; set; } = 1;
        public    SKPointI      CaretSize  => new SKPointI( 2, LineHeight );

        protected Row   _oCaretRow; 
        protected int   _iCaretCol;
        protected int   _iCaretOff;
        protected float _fAdvance;

        /// <seealso cref="CaretInfo" /*
        protected class CaretTracker :
            IPgCaretInfo<Row>
        {
            readonly CacheMultiColumn _oHost;

            public CaretTracker( CacheMultiColumn oHost ) {
                _oHost = oHost ?? throw new ArgumentNullException();
            }

            public Row Row    => _oHost._oCaretRow;
            public int Column => _oHost._iCaretCol;
            public int Offset { 
                get => _oHost._iCaretOff;
                set => _oHost._iCaretOff = value;
            }
            public int Length { 
                get => 0;
                set => throw new NotImplementedException(); 
            }
        }

        /// <summary>
        /// This is where you put the caret (and in the future, selections)
        /// so that the editor can enumerate all the values and keep them
        /// up to date.
        /// </summary>
        /// <remarks>
        /// We need this object since we have to ask where the caret is
        /// BEFORE the edit. AFTER the edit even if we use the existing cache, 
        /// the local x,y of the caret comes from the new line measurements
        /// and we end up with incorrect location information.
        /// </remarks>
        protected class EditHandler :
            IEnumerable<IPgCaretInfo<Row>>,
            IPgEditHandler
        {
            readonly CacheMultiColumn _oHost;
            readonly bool             _fCaretVisible;

            public EditHandler( CacheMultiColumn oHost ) {
                _oHost         = oHost ?? throw new ArgumentNullException();
                _fCaretVisible = _oHost.IsCaretVisible( out SKPointI pntCaret );
            }

            public IEnumerator<IPgCaretInfo<Row>> GetEnumerator() {
                yield return new CaretTracker( _oHost );
            }

            /// <summary>
            /// This gets called at the end of the session.
            /// </summary>
            /// <param name="oRow">Null if the whole buffer should
            /// be measured.</param>
            public void OnUpdated( EditType eType, Row oRow ) {
                if( eType == EditType.DeleteRow ) {
                    if( _oHost._oCaretRow == oRow ) {
                        if( _oHost._oSiteList[ _oHost.CaretAt ] is Row oNext ) {
                            _oHost._oCaretRow = oNext;
                        }
                    }
                    _oHost.CacheRepair( null, _fCaretVisible, false );
                } else {
                    _oHost.CacheRepair( oRow, _fCaretVisible, oRow == null );
                }
            }

            IEnumerator IEnumerable.GetEnumerator() {
                return GetEnumerator();
            }
        }


        // TODO: Get the font from the site instead of from the constructor? Maybe?
        /// <remarks>Need to sort out the LineHeight accessor since the cache elements might be
        /// variable height, Need to make sure I'm using this correctly. Consider calling it
        /// "LineScroll"</remarks>
        public CacheMultiColumn( ICacheManSite oSite, IPgFontRender oFont, List<SmartRect> rgColumns ) :
			base() 
		{
			Font           = oFont     ?? throw new ArgumentNullException( "Need a font to get things rolling." );
			_oSite         = oSite     ?? throw new ArgumentNullException( "Cache manager is a sited object.");
            _oSiteList     = (IReadableBag<Row>)oSite;
            _rgColumnRects = rgColumns ?? throw new ArgumentNullException( "Columns list from Edit Window is null!" );

            GlyphLt    = Font.GetGlyph( 0x003c ); // we used to show carriage return as a '<' sign.
            LineHeight = (int)Font.LineHeight;    // BUG: Cache elem's are variable height in general.

            _oCaretRow = null;
            _iCaretCol = 0; // Make sure the column is edible :-/
            _iCaretOff = 0;
            _fAdvance  = 0;
        }

        protected virtual Row GetTabOrderAtScroll() {
            try {
                int iIndex = (int)(_oSite.GetScrollProgress * _oSiteList.ElementCount);
                if( iIndex >= _oSiteList.ElementCount )
                    iIndex = _oSiteList.ElementCount - 1;
                if( iIndex < 0 )
                    return null;

                return _oSiteList[ iIndex ];
            } catch( Exception oEx ) {
                if( IsUnhandledStdRpt( oEx ) )
                    throw;
            }

            return _oSiteList[0];
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

        static Type[] _rgStdErrors = { typeof( ArgumentNullException ),
                                       typeof( ArgumentException ),
                                       typeof( ArgumentOutOfRangeException ),
                                       typeof( IndexOutOfRangeException ),
                                       typeof( NullReferenceException ) };

        protected bool IsUnhandledStdRpt( Exception oEx ) {
            if( _rgStdErrors.IsUnhandled( oEx ) )
                return true;
            
            _oSite.LogError( "CacheMultiColumn", "Exception occurred" );

            return false;
        }

        public SmartRect TextRect {
            get { return _oTextRect; }
        }

        public int CaretAt {
            get {
                try {
                    if( _oCaretRow == null ) {
                        // Doc might be empty! O.o
                        if( _oSiteList[0] is not Row oRow )
                            return 0;

                        return oRow.At;
                    }
                    if( _oCaretRow.At < 0 )
                        return 0;

                    return _oCaretRow.At; 
                } catch( NullReferenceException ) {
                    _oSite.LogError( "Cacheman", "Lost track of Caret Row (exception)." );
                }
                return 0; // Will cause a cache reset on caret moves.
            }
        }

        public int CaretColumn => _iCaretCol;

        public void OnMouseWheel( int iDelta ) {
            if( _rgOldCache.Count <= 0 )
                return;

            int iTop = _rgOldCache[0].Top + ( 4 * iDelta / LineHeight );

            Scroll( iTop );
        }

        /// <summary>
        /// Slide the cache elements to the new top. Use this for scrolling
        /// items on the screen. No resize has occured.
        /// </summary>
        /// <param name="iTop">A value relative to the TextRect.</param>
        protected void Scroll( int iTop ) {
            foreach( CacheRow oCacheRow in _rgOldCache ) {
                oCacheRow.Top = iTop;
                iTop += oCacheRow.Height;
            }

            CacheRow oSeedCache = CacheLocateTop();

            if( oSeedCache == null )
                oSeedCache = CacheReset( RefreshNeighborhood.SCROLL );

            CacheWalker( oSeedCache, false );
        }

        /// <summary>
        /// This object will get called whenever there's an edit to
        /// the document, such as a typed character or cut/paste.
        /// It SHOULD NOT be called for format changes/reparse.
        /// </summary>
        public IPgEditHandler CreateDocEventObject() {
            return new EditHandler( this );
        }

        /// <seealso cref="CaretTracker" />
        public struct CaretInfo :
            IPgCaretInfo<Row> 
        {
            int iOffset = 0;
            public CaretInfo( CacheMultiColumn oHost ) {
                Row     = oHost._oCaretRow ?? throw new ArgumentNullException();
                Column  = oHost._iCaretCol;
                iOffset = oHost._iCaretOff;
            }

            public Row Row    { get; }

            public int Column { get; }

            public int Offset { get => iOffset; set => throw new NotImplementedException(); }
            public int Length { get => 0; set => throw new NotImplementedException(); }
        }

        public CaretInfo? CopyCaret() {
            if( _oCaretRow == null )
                _oCaretRow = _oSiteList[0];
            if( _oCaretRow == null )
                return null;

            return new CaretInfo( this );
        }

        protected void MoveWindows() {
            try {
                foreach( CacheRow oCacheRow in _rgOldCache ) {
                    foreach( IPgCacheMeasures oCMElem in oCacheRow.CacheList ) {
                    }
                    for( int iCacheCol=0; iCacheCol<oCacheRow.CacheList.Count; ++iCacheCol ) {
                        if( oCacheRow[iCacheCol] is IPgCacheWindow oCWElem ) {
                            SmartRect rctColumn = _rgColumnRects[iCacheCol];
                            SmartRect rctSquare = new SmartRect();

                            rctSquare.SetRect( rctColumn.Left, oCacheRow.Top, rctColumn.Right, oCacheRow.Bottom );
                            oCWElem  .MoveTo( rctSquare );
                        }
                    }
                }
            } catch( Exception oEx ) {
                if( !IsUnhandledStdRpt( oEx ) )
                    throw;

                LogError( "Problem moving sub windows in multi column cache" );
            }
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
                        int iIndex = (int)(_oSite.GetScrollProgress * _oSiteList.ElementCount);

                        oDocRow = _oSiteList[ iIndex ];
                        break;
                    case RefreshNeighborhood.CARET:
                        oDocRow = _oSiteList[ CaretAt ];
                        break;
                }
                if( oDocRow == null )
                    return null;

                oCacheRow = _rgOldCache.Find( item => item.Row == oDocRow ); 

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
            Row oNextDRow = _oSiteList[oPrevCache.At + iDir];

            if( oNextDRow == null )
                return null;

            CacheRow oNewCache = _rgOldCache.Find( x => x.Row == oNextDRow );

            // If we're reusing a cache, it's already measured!! ^_^
            if( oNewCache == null ) {
                oNewCache = CreateCacheRow( _oSiteList[ oNextDRow.At ] );
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
        protected void CacheRestackFromTop(int iTop) {
            foreach( CacheRow oCRow in _rgNewCache ) {
                oCRow.Top = iTop;

                iTop += oCRow.Height + RowSpacing;
            }
        }

        /// <summary>
        /// Try locate the top most cache line still in the visible and valid.
        /// </summary>
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
            if( _oSiteList[iRow] is Row oSearch ) {
                foreach( CacheRow oCache in _rgOldCache ) {
                    if( oCache.Row == oSearch ) {
                        return oCache;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Use this to generate a prefered size if that gets asked for
        /// form layouts. Make sure to call CacheRepair( null, true true )
        /// on init. This will work in the Property Page case since we've
        /// preloaded all the cache row's!!
        /// </summary>
        public int PreferedHeight {
            get {
                int iHeight = 0;
                foreach( CacheRow oCache in _rgOldCache ) {
                    iHeight += oCache.Height;
                }
                // So there's that "bearing y" from the origin I've never
                // really resolved, so just add some slush. :-/
                iHeight += LineHeight / 2;

                return iHeight;
            }
        }

        enum InsertAt { TOP,BOTTOM };

        /// <summary>
        /// New experimental positioning. The rule is: if the caret is on
        /// screen, keep it there. Else, repair as best you can, else 
        /// rebuild the screen from the current cache position. If you
        /// have scrolled the caret off screen I assume it's intentional.
        /// </summary>
        /// <remarks>BUG: Might need to if the row is invalid and
        /// remeasure such items. It's a little bigger job so I'll
        /// look into that later...</remarks>
        /// <param name="fMeasure">Remeasure all row items.</param>
        /// <param name="fFindCaret">Keep the caret on the screen</param>
        /// <param name="oPatch">Make sure this item is re-measured.</param>
        public void CacheRepair( Row oPatch, bool fFindCaret, bool fMeasure ) {
            try {
                CacheRow oSeedCache = null;

                foreach( CacheRow oCacheRow in _rgOldCache ) {
                    if( oCacheRow.Row == _oCaretRow &&
                        _oCaretRow    != null ) {
                        oSeedCache = oCacheRow;
                    }
                    if( oPatch        != null &&
                        oCacheRow.Row == oPatch ) {
                        RowMeasure(oCacheRow);
                    }
                }
                if( fFindCaret ) {
                    if( oSeedCache != null )
                        CaretSlideWindow( oSeedCache );
                }
                if( oSeedCache == null ) {
                    oSeedCache = CacheLocateTop();
                }

                oSeedCache ??= CacheReset( RefreshNeighborhood.SCROLL );

                // It's quite possible for the document to be EMPTY!
                // Or the caret isn't set and our cache is empty..
                // Probably should just force the caret to be on some line
                // at all times... :-/
                if( oSeedCache == null ) {
                    FinishUp( null, null );
                    return;
                }

                CacheWalker( oSeedCache, fMeasure );
            } catch( Exception oEx ) {
                if( IsUnhandledStdRpt( oEx ) )
                    throw;
            }
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

            CacheRow oCacheWithCaret = null; // If non null, caret might be visible...

            void NewCacheAdd( InsertAt ePos, CacheRow oNewCacheRow ) {
                if( oNewCacheRow.Row == _oCaretRow )
                    oCacheWithCaret = oNewCacheRow;

                if( ePos == InsertAt.BOTTOM )
                    _rgNewCache.Add( oNewCacheRow );
                if( ePos == InsertAt.TOP )
                    _rgNewCache.Insert( 0, oNewCacheRow );
            }

            _rgNewCache.Clear();
            NewCacheAdd( InsertAt.BOTTOM, oSeedCache );

            CacheRow oFallCache = oSeedCache; // First go down.
            while( oFallCache.Bottom < _oTextRect.Bottom ) {
                CacheRow oNewCache = CacheRecycle( oFallCache, 1, fRemeasure );
                if( oNewCache == null ) {
                    _oTextRect.SetScalar( SET.RIGID, SCALAR.BOTTOM, oFallCache.Bottom );
                    break;
                }

                NewCacheAdd( InsertAt.BOTTOM, oFallCache = oNewCache );
            }
            CacheRow oRiseCache = oSeedCache; // Then go up.
            while( oRiseCache.Top > _oTextRect.Top ) { 
                CacheRow oNewCache = CacheRecycle( oRiseCache, -1, fRemeasure );
                if( oNewCache == null ) {
                    _oTextRect.SetScalar( SET.RIGID, SCALAR.TOP, oRiseCache.Top );
                    break;
                }

                NewCacheAdd( InsertAt.TOP, oRiseCache = oNewCache );
            }
            CacheRow oLastCache = oFallCache; // Then try down to finish filling.
            while( oLastCache.Bottom < _oTextRect.Bottom ) { 
                CacheRow oNewCache = CacheRecycle( oLastCache, 1, fRemeasure );
                if( oNewCache == null )
                    break;

                NewCacheAdd( InsertAt.BOTTOM, oLastCache = oNewCache );
            }

            if( _oTextRect.Top != 0 && _rgNewCache.Count > 0 ) {
                int iOldTextTop = _oTextRect.Top;
                int iNewElemTop = _rgNewCache[0].Top;
                _oTextRect.SetScalar( SET.RIGID, SCALAR.TOP, 0 );
                CacheRestackFromTop( iNewElemTop - iOldTextTop );
            }

            _rgOldCache.Clear();
            _rgOldCache.AddRange( _rgNewCache );
            _rgNewCache.Clear();

            MoveWindows();

            FinishUp( oLastCache, oCacheWithCaret );
        }

        protected virtual void FinishUp( CacheRow oBottom, CacheRow oCaret ) {
            if( _rgOldCache.Count <= 0 ) {
                _oSite.OnRefreshComplete( 1, 1 );
                _oSite.OnCaretPositioned( new SKPointI( -1000,-1000), false );
                return;
            }

            int  iBottomRow    = ( oBottom == null ) ? 0 : oBottom.At;
            bool fCaretVisible = IsCaretNear( oCaret, out SKPointI pntCaret );

            _oSite.OnRefreshComplete( iBottomRow, _rgOldCache.Count / _oSiteList.ElementCount );
            _oSite.OnCaretPositioned( pntCaret,   fCaretVisible );
        }

        /// <summary>
        /// If data row containing caret is cached then it MIGHT be visible.
        /// return the coordinates of the caret relative to it's column.
        /// </summary>
        /// <remarks>We could intersect the rect for the caret and the TextRect, but
        /// we don't horzontally scroll. So the caret won't be off on the left or right.</remarks>
        /// <param name="oCaretCacheRow">Cache row representing the data row with the caret.</param>
        /// <param name="pntCaretTop">Location of the caret on the screen.</param>
        protected bool IsCaretNear( CacheRow oCaretCacheRow, out SKPointI pntCaretTop ) {
            pntCaretTop = new( -10, -10 ); // s/b offscreen in any top/left 0,0 window clent space.

            if( oCaretCacheRow == null )
                return false;

            try {
                // Left top coordinate of the caret offset.
                Point     pntCaretRelative  = oCaretCacheRow[_iCaretCol].GlyphOffsetToPoint( _iCaretOff );
                SmartRect oColumn           = _rgColumnRects[_iCaretCol];

                pntCaretTop = new SKPointI( pntCaretRelative.X + oColumn.Left,
                                            pntCaretRelative.Y + oCaretCacheRow.Top );
            } catch( Exception oEx ) {
                if( IsUnhandledStdRpt( oEx ) )
                    throw;
            }

            return true;
        }

        /// <summary>
        /// When the window get's the focus, we need to see if the caret needs to be shown.
        /// </summary>
        /// <param name="pntCaret">Caret position in window coordinates.</param>
        /// <returns>Caret on screen or not.</returns>
        public bool IsCaretVisible( out SKPointI pntCaret ) {
            CacheRow oCaretRow = CacheLocate( CaretAt );

            if( IsCaretNear( oCaretRow, out pntCaret ) ) {
                bool fTL = TextRect.IsInside( pntCaret.X, pntCaret.Y );
                bool fRB = TextRect.IsInside( pntCaret.X + CaretSize.X, pntCaret.Y + CaretSize.Y );

                bool fResult = fTL | fRB;
                return fResult;
            }

            return false;
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
                    Scroll( (int)(.80 * _oTextRect.Height ) );
                    break; 
                case ScrollEvents.LargeIncrement:
                    Scroll( (int)(.80 * - _oTextRect.Height ) );
                    break;
                case ScrollEvents.SmallDecrement:
                    Scroll( LineHeight );
                    break;
                case ScrollEvents.SmallIncrement:
                    Scroll( -LineHeight );
                    break;

                // We can potentialy render less until this final end scroll comes in.
                case ScrollEvents.EndScroll:
                case ScrollEvents.First:
                case ScrollEvents.Last:
                case ScrollEvents.ThumbPosition:
                case ScrollEvents.ThumbTrack:
                    CacheWalker( CacheReset( RefreshNeighborhood.SCROLL ), false );
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
                    IPgCacheMeasures oElem = oRow.CacheList[i];

				    oElem.Update            ( Font );
                    oElem.OnChangeFormatting( null );
                    oElem.OnChangeSize      ( _rgColumnRects[i].Width );
                }
			} catch( Exception oEx ) {
                if( IsUnhandledStdRpt( oEx ) )
                    throw;
			}
        }

        public void CacheReColor() {
            try {
                foreach( CacheRow oCacheRow in _rgOldCache ) {
                    RowMeasure( oCacheRow );
                }
			} catch( Exception oEx ) {
                if( IsUnhandledStdRpt( oEx ) )
                    throw;
			}
        }

        /// <summary>
        /// Create a cached line element. There are a lot of dependencies on stuff in this object
        /// and so we create the element here and pass it out to be used. Be sure to call 
        /// RowMeasure() after this call so that the lines can be measured.
        /// </summary>
        /// <param name="oRow">The row we want to display.</param>
        /// <remarks>Might be nice to have a call to the host and let them create the row.
        /// Would be excellent in the property page case!</remarks>
        /// <seealso cref="RowMeasure"/>
        protected virtual CacheRow CreateCacheRow( Row oDocRow ) {
            if( oDocRow == null )
                throw new ArgumentNullException();

            CacheRow oFreshCacheRow = new CacheRow2( oDocRow );

            // TODO: Get the r/w status of the column so we can set its bgcolor.
            // BUG : Doc columns might not match order of row columns!! >_<;;
            // Note: If we suddenly start typing in a dummy lines, we'll have problems..
            foreach( Line oLine in oDocRow ) {
                oFreshCacheRow.CacheList.Add( new FTCacheWrap( oLine == null ? _oDummyLine : oLine ) );
			}

            return oFreshCacheRow;
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

        /// <summary>
        /// Update the sliding window based the offset into the given cache element.
        /// We want to make sure the caret is always visible and doesn't slide off screen.
        /// The CacheWalker will normalize the window so the top is 0 (or whereever
        /// it should be)
        /// </summary>
        /// <param name="oCaret">The cache row where the caret is situated.</param>
        protected void CaretSlideWindow( CacheRow oCaret ) {
            if( IsCaretNear( oCaret, out SKPointI pntCaretLoc ) ) {
                LOCUS eHitTop = _oTextRect.IsWhere( pntCaretLoc.X, pntCaretLoc.Y );
                LOCUS eHitBot = _oTextRect.IsWhere( pntCaretLoc.X, pntCaretLoc.Y + LineHeight );

                if( ( eHitTop & LOCUS.TOP    ) != 0 ) {
                    _oTextRect.SetScalar(SET.RIGID, SCALAR.TOP,    pntCaretLoc.Y );
                }
                if( ( eHitBot & LOCUS.BOTTOM ) != 0 ) {
                    _oTextRect.SetScalar(SET.RIGID, SCALAR.BOTTOM, pntCaretLoc.Y + LineHeight );
                }
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
                CacheRow oCaretCacheRow = CacheLocate( CaretAt );
                if( oCaretCacheRow != null ) {
                    // First, see if we can navigate within the cache item we are currently at.
                    if( !oCaretCacheRow[_iCaretCol].Navigate( eAxis, iDir, ref _fAdvance, ref _iCaretOff ) ) {
                        // Now try moving vertically, but stay in the same column...
                        if( _oSiteList[ CaretAt + iDir ] is Row oDocRow ) {
                            CacheRow oNewCache = _rgOldCache.Find(item => item.Row == oDocRow);
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
                    CaretSlideWindow( oCaretCacheRow );
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

        public bool CaretTab( int iDir ) {
            if( iDir < -1 || iDir > 1 )
                return false;

            try {
                _iCaretCol += iDir;

                if( _iCaretCol < 0 )
                    _iCaretCol = _rgColumnRects.Count - 1;
                if( _iCaretCol < 0 )
                    _iCaretCol = 0;
                if( _iCaretCol >= _rgColumnRects.Count )
                    _iCaretCol = 0;

                _fAdvance  = 0;
                _iCaretOff = 0;

                if( CacheLocate( CaretAt ) is CacheRow oCaretRow ) {
                    if( iDir < 0 )
                        _iCaretOff = oCaretRow[_iCaretCol].LastOffset;

                    Point pntCaret = oCaretRow[_iCaretCol].GlyphOffsetToPoint( _iCaretOff );

                    pntCaret.X += _rgColumnRects[_iCaretCol].Left;
                    pntCaret.Y += oCaretRow.Top;

                    _oSite.OnCaretPositioned( new SKPointI( pntCaret.X, pntCaret.Y ), true );
                } else {
                    _oSite.OnCaretPositioned( new SKPointI( -1000, -1000 ), false );
                }

                return true;
            } catch( Exception oEx ) {
                if( IsUnhandledStdRpt( oEx ) )
                    throw;
            }
            return false;
        }

        public bool CaretReset( Row oRow, int iColumn ) {
            if( oRow == null )
                return false;

            _oCaretRow = oRow;
            _iCaretCol = 0;
            _iCaretOff = 0;

            CacheRow oCaret = CacheReset( RefreshNeighborhood.CARET );

            CacheWalker( oCaret, false );

            return true;
        }

        public bool CaretAdvance( SKPointI pntPick ) {
            try {
                for( int iColumn = 0; iColumn < _rgColumnRects.Count; iColumn++ ) {
                    SmartRect rctColumn = _rgColumnRects[iColumn];
                    if( rctColumn.IsInside( pntPick.X, pntPick.Y ) ) {
                        CacheRow oCacheRow = PointToCache( iColumn, pntPick, out int iOffset );
                        if( oCacheRow != null ) {
                            _fAdvance  = pntPick.X - rctColumn.Left;
                            _oCaretRow = _oSiteList[ oCacheRow.At ];
                            _iCaretCol = iColumn;
                            _iCaretOff = iOffset;

                            Point pntCaret = oCacheRow[iColumn].GlyphOffsetToPoint( _iCaretOff );

                            pntCaret.X += rctColumn.Left;
                            pntCaret.Y += oCacheRow.Top;

                            _oSite.OnCaretPositioned( new SKPointI( pntCaret.X, pntCaret.Y ), true );
                            return true;
                        }
                        break;
                    }
                }
            } catch( Exception oEx ) {
                if( IsUnhandledStdRpt( oEx ) )
                    throw;
            }
            return false;
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
                bool fCaretOnScreen = IsCaretVisible( out SKPointI pntCaret );
                CacheRepair( null, fCaretOnScreen, fMeasure:true ); 
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
        /// <param name="pntScreenLoc">Graphics location of interest in world coordinates. Basically
        ///                         where the mouse clicked.</param>
        public bool PointToRow( 
            int iColumn, SKPointI pntScreenLoc, out int iOffset, out Row oDataRow )
        {
            CacheRow oCacheRow = PointToCache( iColumn, pntScreenLoc, out int iLineOffset );
            if( oCacheRow != null && oCacheRow.Row is Row ) {
                oDataRow = oCacheRow.Row as Row;
                iOffset  = iLineOffset;

                return true;
            }

            iOffset  = -1;
            oDataRow = null;

            return false;
        }

        /// <summary>
        /// Return the CacheRow and Line offset for the given Column and screen point.
        /// </summary>
        /// <param name="iColumn">Column within we are searching.</param>
        /// <param name="pntScreenPick">A screen coordinate.</param>
        /// <param name="iOffset">Offset into the line if row is found.</param>
        /// <returns></returns>
        protected CacheRow PointToCache( int iColumn, SKPointI pntScreenPick, out int iOffset )
        {
            try {
                foreach( CacheRow oCacheRow in _rgOldCache ) {
                    if( oCacheRow.Top    <= pntScreenPick.Y &&
                        oCacheRow.Bottom >= pntScreenPick.Y ) 
                    {
                        IPgCacheMeasures oCache   = oCacheRow.CacheList[iColumn];
                        SKPointI         pntLocal = new SKPointI( pntScreenPick.X - _rgColumnRects[iColumn].Left,
                                                                  pntScreenPick.Y - _rgColumnRects[iColumn].Top );

                        iOffset = oCache.GlyphPointToOffset(oCacheRow.Top, pntLocal );

                        return oCacheRow;
                    }
                }
            } catch( Exception oEx ) {
                if( IsUnhandledStdRpt( oEx ) )
                    throw;
            }

            iOffset = -1;

            return null;
        }

        /// <summary>
        /// Move the caret to a new position. Usually as a result of moving the
        /// caret to some formatting/hyperlink/mouse position.
        /// </summary>
        /// <seealso cref="ScrollToCaret"/>
        /// <exception cref="ArgumentOutOfRangeException" />
        public bool SetCaretPositionAndScroll( int iDataRow, int iColumn, int iOffset, bool fMeasure = false ) {
            try {
                Row  oDataRow = _oSiteList[ iDataRow ];
                Line oLine    = oDataRow[iColumn];

                if( iOffset < 0 || iOffset >= oLine.ElementCount )
                    return false;

                _oCaretRow = oDataRow;
                _iCaretCol = iColumn;
                _iCaretOff = iOffset;

                CacheRow oCaretCacheRow = CacheLocate( CaretAt );

                if( oCaretCacheRow != null ) {
                    RowMeasure( oCaretCacheRow ); // Always measure caret row.
                } else {
                    oCaretCacheRow = CacheReset( RefreshNeighborhood.CARET );
                }

                CaretSlideWindow ( oCaretCacheRow );
                CacheWalker( oCaretCacheRow, fMeasure );
            } catch( Exception oEx ) {
                if( IsUnhandledStdRpt( oEx ) )
                    throw;

                return false;
            }

            return true;
        }

        public void ScrollToCaret() {
            CacheRow oCaretCacheRow = CacheLocate( CaretAt );

            if( oCaretCacheRow == null ) {
                oCaretCacheRow = CacheReset( RefreshNeighborhood.CARET );
            }

            CaretSlideWindow( oCaretCacheRow );
            CacheWalker     ( oCaretCacheRow );
        }

    } // end class

    /// <summary>
    /// This manager expects all the rows to be precached. Great for the
    /// property pages that have windows which would have to be created
    /// and destroyed on the fly, when usually nothing ever falls out of
    /// the cache.
    /// </summary>
    public class CacheMultiFixed : CacheMultiColumn {
        List<CacheRow> _rgFixedCache = new ();
        public CacheMultiFixed(ICacheManSite oSite, IPgFontRender oFont, List<SmartRect> rgColumns) : 
            base(oSite, oFont, rgColumns) 
        {
        }

        protected override CacheRow CreateCacheRow(Row oDocRow) {
            foreach( CacheRow oCacheRow in _rgFixedCache ) { 
                if( oCacheRow.Row == oDocRow ) {
                    RowMeasure( oCacheRow );
                    return oCacheRow;
                }
            }
            _oSite.LogError( "Cache Manager Multi", "Seem to have lost an data row..." );
            return base.CreateCacheRow(oDocRow);
        }

        /// <summary>
        /// If we had the base.CreateCacheRow call into the host to get
        /// the row, we wouldn't need this call at all! O.o
        /// </summary>
        /// <param name="oCacheRow"></param>
        public void Add( CacheRow oCacheRow ) {
            if( _rgFixedCache.Count <= 0 )
                _oCaretRow = _oSiteList[ oCacheRow.At ];

            _rgFixedCache.Add( oCacheRow );
        }

        [Obsolete]protected override Row GetTabOrderAtScroll() {
            int iIndex = (int)(_oSite.GetScrollProgress * _rgFixedCache.Count );

            return _oSiteList[iIndex];
        }

        protected override void FinishUp( CacheRow oBottom, CacheRow oCaret ) {
            if( _rgFixedCache.Count <= 0 ) {
                _oSite.OnRefreshComplete( 1, 1 );
                _oSite.OnCaretPositioned( new SKPointI( -1000,-1000), false );
                return;
            }

            bool fCaretVisible = IsCaretNear( oCaret, out SKPointI pntCaret );
            int  iFixedIndex   = 0;

            for( int i=0; i< _rgFixedCache.Count; ++i ) {
                if( _rgFixedCache[i].Row == oBottom ) {
                    iFixedIndex = i;
                    break;
                }
            }

            _oSite.OnRefreshComplete( (float)iFixedIndex       / _rgFixedCache.Count, 
                                      (float)_rgOldCache.Count / _rgFixedCache.Count );
            _oSite.OnCaretPositioned( pntCaret,   fCaretVisible );
        }

    }
}
