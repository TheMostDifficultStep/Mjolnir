﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using System.Collections.ObjectModel;

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
        Row   TabOrder( Row oRow, int iDir );
        int   TabCount { get; }
        Row   TabStop( int iIndex );

        uint          FontCache( uint uiHeight ); // Cache the height we want.
        IPgFontRender FontUse  ( uint uiFont );   // Get renderer for cached font.

        float GetScrollProgress { get; }
        void  OnRefreshComplete( float flProgress, float flVisiblePercent );
        void  OnCaretPositioned( SKPointI pntCaretbool, bool fVisible );

        ReadOnlyCollection<ColumnInfo> Columns { get; }
    }

    public class CacheMultiColumn:         
        IEnumerable<CacheRow>,
        IPgEditEvents,
        IPgCaretInfo<Row>
    {
        protected readonly ICacheManSite     _oSite;
        protected readonly IReadableBag<Row> _oSiteList;
        // World coordinates of our view port. Do not confuse these with the
        // layout columns, those are different.
        protected readonly SmartRect       _oTextRect  = new SmartRect();
        protected readonly List<CacheRow>  _rgOldCache = new List<CacheRow>();
        protected readonly List<CacheRow>  _rgNewCache = new List<CacheRow>(); 
        protected readonly TextLine        _oDummyLine = new TextLine( -2, string.Empty );
        protected readonly ReadOnlyCollection<ColumnInfo> _rgColumnInfo;

        public    SelectionManager Selector   { get; }
        protected IPgFontRender    Font       { get; }
        protected IPgGlyph         GlyphLt    { get; } // Our end of line character.
        public    IPgGlyph         GlyphCheck { get; } // Our check character.
        public    int              LineHeight { get; } // Helps us determine scrolling distances.
        public    int              RowSpacing { get; set; } = 1;
        public    SKPointI         CaretSize  => new SKPointI( 2, LineHeight );

        protected Row   _oCaretRow; 
        protected int   _iCaretCol;
        protected int   _iCaretOff;
        protected bool  _fCaretVisible;
        protected float _fAdvance;

        /// <seealso cref="CaretInfo" />
        /// <seealso cref="SelectionManager" />
        /// <seealso cref="IPgCaretInfo{T}"/>

        // I can return myself as a caret. I might just
        // box this up into an object.
        public Row Row    => _oCaretRow;
        public int Column => _iCaretCol;
        public int Offset { 
            get => _iCaretOff;
            set => _iCaretOff = value;
        }
        public int Length { 
            get => 0;
            set => throw new NotImplementedException(); 
        }

        protected class SelectionEnumerable :
            IPgSelection
        {
            readonly IReadableBag<Row> _rgBag;
            readonly SelectionManager  _oSlxnManager;

            public SelectionEnumerable( IReadableBag<Row> rgBag, SelectionManager oSlxn ) {
                _rgBag        = rgBag ?? throw new ArgumentNullException();
                _oSlxnManager = oSlxn ?? throw new ArgumentNullException();
            }

            public IEnumerator<Row> GetEnumerator() {
                for( int i=_oSlxnManager.StartAt; i<=_oSlxnManager.EndAt; ++i ) {
                    yield return _rgBag[i];
                }
            }

            /// <summary>
            /// After retrieving a row from the enumeration you can call this
            /// function to determine the selection on each column.
            /// </summary>
            public IMemoryRange AtColumn(int iIndex) {
                return _oSlxnManager[iIndex];
            }

            public IPgSelection.SlxnType IsSelection(Row oRow) {
                return _oSlxnManager.IsSelection( oRow );
            }

            IEnumerator IEnumerable.GetEnumerator() {
               return GetEnumerator();
            }
        }

        public class SelectionManager {
            public IPgCaretInfo<Row> Caret { get; protected set; }
            public CaretInfo         Pin   { get; protected set; }

            readonly IPgCaretInfo<Row>[] _rgRowHighLow = new IPgCaretInfo<Row>[2];
            readonly IPgCaretInfo<Row>[] _rgColHighLow = new IPgCaretInfo<Row>[2];
            readonly IColorRange      [] _rgSelections;
            readonly IColorRange      [] _rgCache; // Color ranges that the selections can use.

            bool _fFrozen = true;
            public SelectionManager( int iMaxCols ) {
                _rgSelections = new IColorRange[iMaxCols];
                _rgCache      = new IColorRange[iMaxCols];

                for( int i=0; i< _rgCache.Length; ++i ) {
                    _rgCache[i] = new ColorRange( 0, 0, -1 );
                }
            }

            /// <summary>
            /// Retreive the current color range selection for the given column.
            /// Remember to call IsSelection( Row ) first for the whole row.
            /// </summary>
            /// <param name="iIndex">Column selection.</param>
            /// <exception cref="IndexOutOfRangeException" />
            public IColorRange this [ int iIndex ] {
                get { return _rgSelections[iIndex]; }
            }

            /// <summary>
            /// Call this at the start of the selection and
            /// we'll track relative to the location of the caret at this time.
            /// </summary>
            /// <param name="oCaret">First point to begin selection from.</param>
            public void SetPin( IPgCaretInfo<Row> oCaret ) {
                if( oCaret.Row == null )
                    return;

                Caret    = oCaret;
                Pin      = new CaretInfo( oCaret );
                _fFrozen = false;
            }

            public void SetWord( IPgCaretInfo<Row> oCaret, IMemoryRange oRange ) {
                if( oRange == null )
                    return;

                oCaret.Offset = oRange.Offset;

                Caret = oCaret;
                // BUG: This might be a problem when I attempt to move the pin
                // around b/c of user edits in another window.
                Pin   = new CaretInfo(oCaret) { Offset = oRange.Offset + oRange.Length };
            }

            /// <summary>
            /// Capture the current selection and stop updating it 
            /// even if caret moves. 
            /// </summary>
            public void Freeze() {
                if( Caret == null || Caret.Row == null )
                    return;

                Caret    = new CaretInfo( Caret );
                _fFrozen = true;
            }

            public bool IsFrozen => _fFrozen;

            /// <summary>
            /// Clear the selections array but don't reset the selection
            /// ranges. They might be in use temporarily by the caller.
            /// </summary>
            public void Clear() { 
                Caret = null;
                for( int i=0; i< _rgCache.Length; ++i ) {
                    _rgSelections[i] = null;
                }
                _fFrozen = true;
            }

            protected void Set( int iCol, int iOffset, int iLength ) {
                _rgSelections[iCol] = _rgCache[iCol];

                _rgSelections[iCol].Offset = iOffset;
                _rgSelections[iCol].Length = iLength;
            }

            /// <summary>
            /// This is the case where top & bottom row's are different.
            /// So we have no selection until the column and then
            /// full selection from that column on.
            /// ______|___+++|++++++
            /// </summary>
            protected void TopRow( Row oRow ) {
                int i=_rgRowHighLow[0].Column;

                // First selection, from offset to end of Low.
                Set( i, _rgRowHighLow[0].Offset, 
                        oRow[i].ElementCount - _rgRowHighLow[0].Offset );
                
                // Remaining selections. Select all.
                while( ++i < oRow.Count ) {
                    Set( i, 0, oRow[i].ElementCount );
                }
            }

            /// <summary>
            /// This is the case where top & bottom row's are different.
            /// So we have selections until the column and then
            /// no selection from that column on.
            /// ++++++|+++___|______
            /// </summary>
            protected void BotRow( Row oRow ) {
                // Full selection on the left.
                int i = 0;
                while( i < _rgRowHighLow[1].Column && i < oRow.Count ) {
                    Set( i, 0, oRow[i].ElementCount );
                    ++i;
                }
                // selection, from 0 to offset of high.
                if( i < oRow.Count ) {
                    Set( i, 0, _rgRowHighLow[1].Offset );
                }
                // no remaining selections.
            }

            /// <summary>
            /// This is the case where top & bottom row's are same.
            /// So we have selections until the column and then
            /// no selection from that column on.
            ///             Column : 0      1      2
            /// fEqualCol is true  : ______|_++++_|______
            /// fEqualCol is false : ___+++|++++++|+_____
            /// </summary>
            protected void EquRow( Row oRow ) {
                if( Caret.Column > Pin.Column ) {
                    _rgColHighLow[0] = Pin;
                    _rgColHighLow[1] = Caret;
                } else {
                    if( Caret.Column == Pin.Column &&
                        Caret.Offset >  Pin.Offset ) 
                    {
                        _rgColHighLow[0] = Pin;
                        _rgColHighLow[1] = Caret;
                    } else {
                        _rgColHighLow[0] = Caret;
                        _rgColHighLow[1] = Pin;
                    }
                }

                bool fEqualCol = Caret.Column == Pin.Column;

                if( fEqualCol ) {
                    int iCol = Caret.Column;

                    Set( iCol, _rgColHighLow[0].Offset, 
                               _rgColHighLow[1].Offset -
                               _rgColHighLow[0].Offset );
                } else {
                    int iLo = _rgColHighLow[0].Column;
                    int iHi = _rgColHighLow[1].Column;

                    Set( iLo, _rgColHighLow[0].Offset, 
                              oRow[iLo].ElementCount - 
                              _rgColHighLow[0].Offset );

                    for( int i=iLo+1; i<iHi; ++i ) {
                        Set( i, 0, oRow[i].ElementCount );
                    }

                    Set( iHi, 0, _rgColHighLow[1].Offset );
                }
            }

            public int StartAt => _rgRowHighLow[0].Row.At;
            public int EndAt   => _rgRowHighLow[1].Row.At;

            /// <summary>
            /// This has the side effect of resetting the column selection
            /// values so you may query for those after this call or
            /// repaint properly.
            /// </summary>
            /// <returns>Overall selection type on the row.</returns>
            public IPgSelection.SlxnType IsSelection( Row oRow ) {
                try {
                    // Clear out any previous selections.
                    for( int i=0; i< _rgCache.Length; ++i ) {
                        _rgSelections[i] = null;
                    }
                    if( Caret == null || Caret.Row == null )
                        return IPgSelection.SlxnType.None;

                    if( Caret.Row.At > Pin.Row.At ) {
                        _rgRowHighLow[0] = Pin;
                        _rgRowHighLow[1] = Caret;
                    } else {
                        _rgRowHighLow[0] = Caret;
                        _rgRowHighLow[1] = Pin;
                    }
                    if( oRow.At < _rgRowHighLow[0].Row.At )
                        return IPgSelection.SlxnType.None;
                    if( oRow.At > _rgRowHighLow[1].Row.At )
                        return IPgSelection.SlxnType.None;

                    if( Caret.Row.At == Pin.Row.At ) {
                        EquRow( oRow );
                        return IPgSelection.SlxnType.Equal;
                    }

                    if( oRow.At == _rgRowHighLow[0].Row.At ) {
                        TopRow( oRow );
                        return IPgSelection.SlxnType.Top;
                    }
                    if( oRow.At == _rgRowHighLow[1].Row.At ) {
                        BotRow( oRow );
                        return IPgSelection.SlxnType.Bottom;
                    }
                    
                    // Middle... select everything.
                    for( int iCol=0; iCol<oRow.Count; ++iCol ) {
                        Set( iCol, 0, oRow[iCol].ElementCount );
                    }
                    return IPgSelection.SlxnType.Middle;
                } catch ( Exception oEx ) {
                    Type[] rgErrors = { typeof( NullReferenceException ),
                                        typeof( ArgumentOutOfRangeException ) };
                    if( rgErrors.IsUnhandled( oEx ) )
                        throw;
                }
                return IPgSelection.SlxnType.None; // Technically error. But let's see how it goes.
            }

            public int RowCount {
                get {
                    if( Caret == null )
                        return 0;

                    try {
                        int iRows = Math.Abs( Caret.Row.At - Pin.Row.At );

                        if( iRows > 0 )
                            return iRows + 1;

                        IPgSelection.SlxnType eSlxn = IsSelection( Caret.Row );

                        if( eSlxn != IPgSelection.SlxnType.Equal ) 
                            return 0; // Technically an error.

                        int iCharCount = 0;
                        foreach( IColorRange oSlxn in _rgSelections ) {
                            if( oSlxn != null )
                                iCharCount += oSlxn.Length;
                        }
                        if( iCharCount > 0 )
                            return 1;
                    } catch( Exception oEx ) {
                        Type[] rgErrors = { typeof( NullReferenceException ),
                                            typeof( ArgumentOutOfRangeException ) };
                        if( rgErrors.IsUnhandled( oEx ) )
                            throw;
                    }
                    return 0;
                }
            }

            /// <summary>
            /// Count the columns in the current row selection that
            /// have any selection in them.
            /// </summary>
            /// <param name="iColumn">Return selected column count.</param>
            /// <returns>True if there is only one column.</returns>
            public bool IsSingleColumn( out int iColumn ) {
                iColumn = -1;

                int iCount  = 0;
                for( int i=0; i<_rgSelections.Length; ++i ) {
                    if( _rgSelections[i] != null ) {
                        iColumn = i;
                        ++iCount;
                    }
                }
                return iCount == 1;
            }
        } // End class

        /// <remarks>Need to sort out the LineHeight accessor since the cache elements might be
        /// variable height, Need to make sure I'm using this correctly. Consider calling it
        /// "LineScroll"</remarks>
        public CacheMultiColumn( ICacheManSite oSite ) :
			base() 
		{
			_oSite        = oSite     ?? throw new ArgumentNullException( "Cache manager is a sited object.");
            _oSiteList    = (IReadableBag<Row>)oSite;
            _rgColumnInfo = oSite.Columns ?? throw new ArgumentNullException( "Columns list from Edit Window is null!" );
            Selector      = new SelectionManager( 20 ); // Argh, rgColumns.Count not set yet... :-/

			Font       = oSite.FontUse( oSite.FontCache( 12 ) )  ?? throw new ArgumentNullException( "Need a font to get things rolling." );
            GlyphLt    = Font.GetGlyph( 0x003c ); // we used to show carriage return as a '<' sign.
            GlyphCheck = Font.GetGlyph( 0x2714 ); // The multi column editor has this value...
            LineHeight = (int)Font.LineHeight;    // BUG: Cache elem's are variable height in general.

            _oCaretRow = null;
            _iCaretCol = 0; // Make sure the column is editable :-/
            _iCaretOff = 0;
            _fAdvance  = 0;
        }

        /// <summary>
        /// Return a location representative for the scroll bar. Always returns
        /// a value, bounded by 0 or last element, UNLESS, the cache is empty.
        /// </summary>
        protected virtual Row GetTabOrderAtScroll() {
            try {
                int iIndex = (int)( _oSite.GetScrollProgress * _oSite.TabCount );

                if( iIndex >= _oSite.TabCount )
                    iIndex = _oSite.TabCount - 1;
                if( iIndex < 0 )
                    iIndex = 0;

                return _oSite.TabStop(iIndex);
            } catch( Exception oEx ) {
                if( IsUnhandledStdRpt( oEx ) )
                    throw;
            }

            return _oSite.TabStop( 0 );
        }

        /// <summary>
        /// BUG: Let's try to phase this out. I typically need to call IsSelection on
        /// each line to get the selection type anyway. That get's done in this
        /// enumerator so it's wasting time.
        /// </summary>
        public IPgSelection Selection => new SelectionEnumerable( _oSiteList, Selector );

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

        /// <summary>
        /// Return the row the caret is at. 
        /// </summary>
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

        /// <summary>
        /// Both these functions would be better served by making the caret
        /// a first class object in the CacheMult. I'll do that soon.
        /// </summary>
        public int CaretColumn => _iCaretCol;
        public int CaretOffset { 
            set { _iCaretOff = value; }
            get { return _iCaretOff; }
        }

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

        /// <seealso cref="CaretTracker" />
        public struct CaretInfo :
            IPgCaretInfo<Row> 
        {
            public CaretInfo( CacheMultiColumn oHost ) {
                Row    = oHost._oCaretRow ?? throw new ArgumentNullException();
                Column = oHost._iCaretCol;
                Offset = oHost._iCaretOff;
            }

            public CaretInfo( IPgCaretInfo<Row> oSource ) {
                Row    = oSource.Row ?? throw new ArgumentNullException();
                Column = oSource.Column;
                Offset = oSource.Offset;
            }

            public Row Row    { get; }

            public int Column { get; }

            public int Offset { get ; set ; }
            public int Length { readonly get => 0; set => throw new NotImplementedException(); }
        }

        public CaretInfo? CopyCaret() {
            // TODO: Maybe a CaretNullException would work here!!
            if( _oCaretRow == null )
                _oCaretRow = _oSiteList[0];
            if( _oCaretRow == null )
                return null;

            return new CaretInfo( this );
        }

        /// <summary>
        /// If there are window objects as elements then we need to move those specifically.
        /// </summary>
        protected void MoveWindows() {
            try {
                foreach( CacheRow oCacheRow in _rgOldCache ) {
                    //foreach( IPgCacheMeasures oCMElem in oCacheRow.CacheList ) {
                    //}
                    for( int iCacheCol=0; iCacheCol<oCacheRow.CacheColumns.Count; ++iCacheCol ) {
                        if( oCacheRow[iCacheCol] is IPgCacheWindow oCWElem ) {
                            SmartRect rctColumn = _rgColumnInfo[iCacheCol]._rcBounds;
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
                        oDocRow = GetTabOrderAtScroll();
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
            Row oNextDRow = _oSite.TabOrder( oPrevCache.Row, iDir );

            if( oNextDRow == null )
                return null;

            CacheRow oNewCache = _rgOldCache.Find( x => x.Row == oNextDRow );

            // Little hack for now.
            if( oNewCache != null && oNextDRow.Deleted )
                oNewCache = null;

            // If we're reusing a cache, it's already measured!! ^_^
            if( oNewCache == null ) {
                oNewCache = CreateCacheRow( oNextDRow ); // _oSiteList[ oNextDRow.At ]
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
        /// <remarks>Requiring the row.At to be > 0 at present is the only way
        /// to detect deleted lines not yet flushed. But if the property page
        /// forgets to initialize the line number we end up failing here.
        /// </remarks>
        protected CacheRow CacheLocateTop() {
            CacheRow oSeedCache = null;
            if( _rgOldCache.Count > 0 ) {
                int iTop = _oTextRect.Height;
                foreach( CacheRow oTestRow in _rgOldCache ) {
                    if( ClippedHeight( oTestRow ) > 0 && 
                        oTestRow.At >= 0 && 
                        oTestRow.Row.Deleted == false 
                    ) {
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
        /// <remarks>If the Caret has not been set yet, it will be at row -2.</remarks>
        protected CacheRow CacheLocate( int iRow ) {
            if( iRow < 0 )
                return null;
            if( iRow >= _oSiteList.ElementCount )
                return null;

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
        /// First remove all deleted lines that might be presently
        /// cached. Also, check if the data row that we are pointing
        /// to got deleted as well!!! Depending on how damaged our
        /// cache is we might be down to scroll position to recover. 
        /// </summary>
        protected void CacheFlushDeleted() {
            try {
                _rgNewCache.Clear();

                foreach( CacheRow oCRow in _rgOldCache ) {
                    Row oDRow = oCRow.Row;

                    if( oDRow.Deleted ) {
                        if( oDRow == _oCaretRow ) {
                            _oCaretRow = null;
                            _iCaretOff = 0;
                            // Leave column intact.
                        }
                    } else {
                        _rgNewCache.Add( oCRow );
                    }
                }

                _rgOldCache.Clear();

                foreach( CacheRow oCRow in _rgNewCache ) {
                    _rgOldCache.Add( oCRow );
                }
            } catch( Exception oEx ) {
                if( IsUnhandledStdRpt( oEx ) )
                    throw;
            }
        }

        /// <summary>
        /// New Positioning. The rule is: if the caret is on
        /// screen, keep it there. Else, repair as best you can, else 
        /// rebuild the screen from the current cache position. If you
        /// have scrolled the caret off screen I assume it's intentional.
        /// Unlike the cachewalker which deals with scrolling. This function
        /// repairs the cache after random row editing.
        /// </summary>
        /// <remarks>BUG: if the row is invalid might need to 
        /// remeasure such items. It's a little bigger job so I'll
        /// look into that later...
        /// Also note that the _rgOldCache might be empty on first run!
        /// </remarks>
        /// <param name="fMeasure">Remeasure all row items.</param>
        /// <param name="fFindCaret">Keep the caret on the screen</param>
        /// <param name="oPatch">Make sure this item is re-measured.</param>
        /// <seealso cref="CacheWalker(CacheRow, bool)"/>
        public void CacheRepair( Row oPatch, bool fFindCaret, bool fMeasure ) {
            try {
                CacheFlushDeleted();

                CacheRow oSeedCache = null;

                foreach( CacheRow oCacheRow in _rgOldCache ) {
                    if( oCacheRow.Row == _oCaretRow ) {
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

            float flPercent = 1;
            float flProgres = 0;

            if( _oSite.TabCount > 0 ) {
                flPercent = _rgOldCache.Count / (float)_oSite.TabCount;
                flProgres = iBottomRow        / (float)_oSite.TabCount;
            }
            _oSite.OnRefreshComplete( flProgres, flPercent );
            _oSite.OnCaretPositioned( pntCaret,  fCaretVisible );
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
                SmartRect oColumn           = _rgColumnInfo[_iCaretCol]._rcBounds;

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
            try {
                CacheRow oCaretRow = CacheLocate( CaretAt );

                if( IsCaretNear( oCaretRow, out pntCaret ) ) {
                    bool fTL = TextRect.IsInside( pntCaret.X, pntCaret.Y );
                    bool fRB = TextRect.IsInside( pntCaret.X + CaretSize.X, pntCaret.Y + CaretSize.Y );

                    bool fResult = fTL | fRB;
                    return fResult;
                }
            } catch( Exception oEx ) {
                if( _rgStdErrors.IsUnhandled( oEx ) )
                    throw;

                pntCaret = new( -10, -10 ); // s/b offscreen in any top/left 0,0 window clent space.
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
                case ScrollEvents.ThumbPosition:
                case ScrollEvents.ThumbTrack:
                    CacheWalker( CacheReset( RefreshNeighborhood.SCROLL ), false );
                    break;
                case ScrollEvents.First:
                    CacheWalker( CreateCacheRow( _oSite.TabStop( 0 ) ), true );
                    break;
                case ScrollEvents.Last:
                    CacheWalker( CreateCacheRow( _oSite.TabStop( _oSite.TabCount-1 ) ), true );
                    break;
            }
        }

        /// <remarks>Note that the CacheList length MIGHT be less than 
        /// the _rgColumnRects length!</remarks>
        /// <seealso cref="CheckList"/>
        protected virtual void RowMeasure( CacheRow oCacheRow ) {
            try {
                Selector.IsSelection( oCacheRow.Row );

                for( int i=0; i<oCacheRow.CacheColumns.Count && i<_rgColumnInfo.Count; ++i ) {
                    IPgCacheMeasures oMeasure = oCacheRow.CacheColumns[i];
                    if( oMeasure is FTCacheLine oElem ) {
				        oElem.Measure ( Font );
                        oElem.Colorize( Selector[i] );
                    }
                    oMeasure.OnChangeSize( _rgColumnInfo[i]._rcBounds.Width );
                }
			} catch( Exception oEx ) {
                if( IsUnhandledStdRpt( oEx ) )
                    throw;
			}
        }

        public bool IsSelecting => !Selector.IsFrozen;

        public void BeginSelect() {
            Selector.SetPin( this );
        }

        public void EndSelect() {
            Selector.Freeze();
        }

        /// <summary>
        /// Copy the entire selection. Stick a TAB between columns.
        /// </summary>
        /// <seealso cref="EditMultiColumn.RowDelete(IPgSelection)"/>
        public string SelectionCopy() {
            StringBuilder oBuilder = new();

            try {
			    if( Selector.RowCount > 0 ) {
                    int iCountRow = 0;
                    foreach( Row oRow in Selection ) {
                        int iCountCol = 0;
                        // Just want CR -between- lines.
                        if( iCountRow++ > 0 ) {
                            oBuilder.AppendLine();
                        }

                        if( Selector.IsSelection( oRow ) != IPgSelection.SlxnType.None ) {
                            for( int i=0; i<oRow.Count; i++ ) {
                                if( Selector[i] != null ) {
                                    if( iCountCol++ > 0 ) {
                                        oBuilder.Append( '\t' );
                                    }
                                    oBuilder.Append( oRow[i].SubSpan( Selector[i] ) );
                                }
                            }
                        }
                    }
 			    }
            } catch( Exception oEx ) {
                if( IsUnhandledStdRpt( oEx ) )
                    throw;
            }

            return oBuilder.ToString();
        }

        public void CacheReMeasure() {
            try {
                foreach( CacheRow oCacheRow in _rgOldCache ) {
                    RowMeasure( oCacheRow );
                }
			} catch( Exception oEx ) {
                if( IsUnhandledStdRpt( oEx ) )
                    throw;
			}
        }

        public void CacheReColor() {
            try {
                foreach( CacheRow oCacheRow in _rgOldCache ) {
                    Selector.IsSelection( oCacheRow.Row );

                    for( int i=0; i<oCacheRow.CacheColumns.Count && i<_rgColumnInfo.Count; ++i ) {
                        IPgCacheMeasures oElem = oCacheRow.CacheColumns[i];

                        oElem.Colorize( Selector[i] );
                    }
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
            for( int i = 0; i<oDocRow.Count; ++i ) {
                Line        oLine      = oDocRow[i];
                FTCacheLine oCacheLine = new FTCacheWrap( oLine == null ? _oDummyLine : oLine );
                ColumnInfo  oInfo      = _rgColumnInfo[i];

                oCacheLine.Justify = oInfo._rcBounds.Justify;

                oFreshCacheRow.CacheColumns.Add( oCacheLine );
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
        /// Need to clear selection when caret moves
        /// </summary>
        /// <param name="eAxis">Horizontal or Vertical</param>
        /// <param name="iDir">How much to move by, +1 or -1.</param>
        /// <exception cref="ArgumentOutOfRangeException" />
        public void CaretMove( Axis eAxis, int iDir ) {
            if( !( iDir == 1 || iDir == -1 ) )
                throw new ArgumentOutOfRangeException();

            try {
                Selector.Clear();
                CacheReColor();

                CacheRow oCaretCacheRow = CacheLocate( CaretAt );
                if( oCaretCacheRow != null ) {
                    // First, see if we can navigate within the cache item we are currently at.
                    if( !oCaretCacheRow[_iCaretCol].Navigate( eAxis, iDir, ref _fAdvance, ref _iCaretOff ) ) {
                        // Now try moving vertically, but stay in the same column...
                        if( _oSite.TabOrder( _oCaretRow, iDir ) is Row oDocRow ) {
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

        /// <summary>
        /// User pressed the Tab key.
        /// </summary>
        /// <param name="iDir">Go in the direction given.</param>
        public bool CaretTab( int iDir ) {
            if( iDir < -1 || iDir > 1 )
                return false;

            try {
                _iCaretCol += iDir;

                if( _iCaretCol < 0 )
                    _iCaretCol = _rgColumnInfo.Count - 1;
                if( _iCaretCol < 0 )
                    _iCaretCol = 0;
                if( _iCaretCol >= _rgColumnInfo.Count )
                    _iCaretCol = 0;

                _fAdvance  = 0;
                _iCaretOff = 0;

                if( CacheLocate( CaretAt ) is CacheRow oCaretRow ) {
                    if( iDir < 0 )
                        _iCaretOff = oCaretRow[_iCaretCol].LastOffset;

                    Point pntCaret = oCaretRow[_iCaretCol].GlyphOffsetToPoint( _iCaretOff );

                    pntCaret.X += _rgColumnInfo[_iCaretCol]._rcBounds.Left;
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
        
        /// <summary>
        /// This is a bit like our scroll commands.
        /// </summary>
        /// <seealso cref="ScrollToCaret"/>
        public bool CaretReset( Row oRow, int iColumn ) {
            if( oRow == null )
                return false;

            _oCaretRow = oRow;
            _iCaretCol = iColumn;
            _iCaretOff = 0;

            CacheRow oCaret = CacheReset( RefreshNeighborhood.CARET );

            CacheWalker( oCaret, false );

            return true;
        }

        /// <summary>
        /// Find what row the pick is in. Useful for menu style windows.
        /// </summary>
        /// <param name="pntPick">Location to test.</param>
        /// <param name="iReturn">Column hit. if Pick is between columns return -1</param>
        /// <returns>The hit row.</returns>
        public CacheRow IsRowHit( SKPointI pntPick, out int iReturn ) {
            iReturn = -1;
            try {
                // First find which row we hit. 
                foreach( CacheRow oCacheRow in _rgOldCache ) {
                    if( oCacheRow.Top    <= pntPick.Y &&
                        oCacheRow.Bottom >= pntPick.Y ) 
                    {
                        // If we hit any row, now see if pick is within any column
                        for( int iColumn = 0; iColumn < _rgColumnInfo.Count; iColumn++ ) {
                            SmartRect rctColumn = _rgColumnInfo[iColumn]._rcBounds;
                            if( rctColumn.IsInside( pntPick.X, pntPick.Y ) ) {
                                iReturn = iColumn;
                            }
                        }
                        return oCacheRow;
                    }
                }
            } catch( Exception oEx ) {
                if( IsUnhandledStdRpt( oEx ) )
                    throw;
            }
            return null;
        }

        /// <summary>
        /// This moves the caret and we expect it's position to be updated w/o
        /// any window invalidate since we are using the windows caret.
        /// </summary>
        /// <remarks>I could probably take advantage of the new IsRowHit...</remarks>
        /// <seealso cref="IsRowHit"/>
        public bool CaretAdvance( SKPointI pntPick ) {
            try {
                for( int iColumn = 0; iColumn < _rgColumnInfo.Count; iColumn++ ) {
                    SmartRect rctColumn = _rgColumnInfo[iColumn]._rcBounds;
                    // First find the column the pick is in. PointToCache will then search each cache row.
                    if( rctColumn.IsInside( pntPick.X, pntPick.Y ) ) {
                        CacheRow oCacheRow = PointToCache( iColumn, pntPick, out int iOffset );
                        if( oCacheRow != null ) {
                            _fAdvance  = pntPick.X - rctColumn.Left;
                            _oCaretRow = oCacheRow.Row;
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
        /// Calculate the height of the current cache given the current layout.
        /// </summary>
        public int HeightCached { 
            get {
                int iHeight = 0;

			    foreach( CacheRow oCache in this ) {
				    iHeight += (int)oCache.Height + 1; // BUG: Make '1' a variable.
			    }

                if( iHeight < LineHeight )
                    iHeight = LineHeight;

                return iHeight;
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
                oDataRow = oCacheRow.Row;
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
                        SmartRect        oColumn  = _rgColumnInfo[iColumn]._rcBounds;
                        IPgCacheMeasures oCache   = oCacheRow.CacheColumns[iColumn];
                        SKPointI         pntLocal = new SKPointI( pntScreenPick.X - oColumn.Left,
                                                                  pntScreenPick.Y - oColumn.Top );

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
        /// TODO: Just make this thing take a IMemoryRange or something...
        /// </summary>
        /// <seealso cref="ScrollToCaret"/>
        /// <exception cref="ArgumentOutOfRangeException" />
        public bool SetCaretPositionAndScroll( 
            int iDataRow, int iColumn, int iOffset, 
            int iLength, bool fMeasure = false 
        ) {
            try {
                Row  oDataRow = _oSiteList[ iDataRow ];
                Line oLine    = oDataRow[iColumn];

                // If elemcount is zero it's ok for offset to be zero.
                if( iOffset < 0 || iOffset > oLine.ElementCount )
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

                Selector.SetWord( Caret2, new ColorRange( iOffset, iLength ) );
                CacheReColor();
            } catch( Exception oEx ) {
                if( IsUnhandledStdRpt( oEx ) )
                    throw;

                return false;
            }

            return true;
        }


        /// <seealso cref="CaretReset"/>
        public void ScrollToCaret() {
            CacheRow oCaretCacheRow = CacheLocate( CaretAt );

            if( oCaretCacheRow == null ) {
                oCaretCacheRow = CacheReset( RefreshNeighborhood.CARET );
            }

            CaretSlideWindow( oCaretCacheRow );
            CacheWalker     ( oCaretCacheRow );
        }

        /// <see cref="IPgEditEvents">
        /// <remarks>Might just make it so cache can send an
        /// invalidate() message via the site and I wouldn't
        /// need to implement IPgEditEvents on the Window!!</remarks>
        public virtual void OnDocUpdateBegin() {
            _fCaretVisible = IsCaretVisible( out SKPointI _ );
        }

        public virtual void OnDocUpdateEnd( IPgEditEvents.EditType eType, Row oRow ) {
            CacheRepair( oRow, _fCaretVisible, fMeasure:true );
        }

        public virtual void OnDocFormatted() {
            CacheReColor();
        }

        public virtual void OnDocLoaded() {

            if( _oSiteList.ElementCount > 0 ) {
                Row oRow = _oSiteList[0];

                // would be nice to have a default column from the site.
                _oCaretRow = oRow;
                _iCaretOff = 0;

                CacheWalker( CreateCacheRow( oRow ), fRemeasure:true );
            }
        }

        public IPgCaretInfo<Row> Caret2 => this;

    } // end class
}
