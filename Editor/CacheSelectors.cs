using System;
using System.Collections;
using System.Collections.Generic;

using SkiaSharp;

using Play.Interfaces.Embedding;
using Play.Parse;

namespace Play.Edit {
    /// <seealso cref="CaretTracker" />
    public struct CaretInfo :
        IPgCaretInfo<Row> 
    {
        public CaretInfo( IPgCaretInfo<Row> oSource ) {
            Row    = oSource.Row ?? throw new ArgumentNullException();
            Column = oSource.Column;
            Offset = oSource.Offset;
        }

        public Row Row    { get; set; }

        public int Column { get; }

        public int Offset { get ; set ; }
        public int Length { readonly get => 0; set => throw new NotImplementedException(); }
    }

    public class SelectionEnumerable :
        IPgSelection
    {
        readonly IReadableBag<Row> _rgBag;
        readonly Selection         _oSlxnManager;

        public SelectionEnumerable( IReadableBag<Row> rgBag, Selection oSlxn ) {
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
        public IMemoryRange GetRange(int iIndex) {
            return _oSlxnManager[iIndex];
        }

        public IPgSelection.SlxnType PrepRanges( Row oRow ) {
            return _oSlxnManager.PrepRanges( oRow );
        }

        IEnumerator IEnumerable.GetEnumerator() {
            return GetEnumerator();
        }
    }

    public class SingleSelectionTask : IPgSelectionTask {
        readonly CacheMultiBase _oHost;

        /// <summary>
        /// If we double click, we might select a word so the caret
        /// will be some where inside the given range.
        /// </summary>
        /// <exception cref="ArgumentNullException"></exception>
        public SingleSelectionTask( CacheMultiBase oHost, IMemoryRange oRange ) {
            _oHost = oHost ?? throw new ArgumentNullException();
            ArgumentNullException.ThrowIfNull( oRange );

            if( oRange is not null ) {
                _oHost.Selector.SetWord( oHost, oRange );
            } else {
                _oHost.Selector.SetPin ( oHost );
            }
        }

        public void Dispose() {
            _oHost.Selector.Clear();
        }

        public bool Move( SKPointI pntPick ) {
            if( _oHost.IsInside( pntPick, out int iColumn ) &&
                iColumn == _oHost.Selector.Pin.Column ) 
            {
                _oHost.CaretAdvance( pntPick );
                _oHost.ReColor     ();

                return true;
            }

            return false;
        }
    }

    public abstract class Selection {
        public IPgCaretInfo<Row> End { get; protected set; }
        public CaretInfo         Pin { get; protected set; }

        protected readonly IColorRange      [] _rgSelections;
        protected readonly IColorRange      [] _rgCache; // Color ranges that the selections can use.

        public Selection( int iMaxCols ) {
            _rgSelections = new IColorRange[iMaxCols];
            _rgCache      = new IColorRange[iMaxCols];

            for( int i=0; i< _rgCache.Length; ++i ) {
                _rgCache[i] = new ColorRange( 0, 0, -1 );
            }
        }

        public abstract IPgSelectionTask DoSelection( CacheMultiBase oHost, IMemoryRange oRange );

        public abstract int StartAt { get; }
        public abstract int EndAt   { get; }

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
        /// Clear the selections array but don't reset the selection
        /// ranges. They might be in use temporarily by the caller.
        /// </summary>
        public void Clear() { 
            End = null;
            for( int i=0; i< _rgSelections.Length; ++i ) {
                _rgSelections[i] = null;
            }
        }

        /// <summary>
        /// Capture the current selection and stop updating it 
        /// even if caret moves. 
        /// </summary>
        public bool Freeze() {
            if( End is null || End.Row is null ) {
                End = null;
                return false;
            }

            End = new CaretInfo( End ); // Save the ending Caret position.
            return true;
        }

        public bool IsValid => End is not null;

        /// <summary>
        /// Call this at the start of the selection and
        /// we'll track relative to the location of the caret at this time.
        /// </summary>
        /// <param name="oCaret">First point to begin selection from.</param>
        public void SetPin( IPgCaretInfo<Row> oCaret ) {
            if( oCaret.Row == null )
                return;

            End = oCaret;                  // End is a live caret at this time.
            Pin = new CaretInfo( oCaret ); // Save the current Caret position.
        }

        public void SetWord( IPgCaretInfo<Row> oCaret, IMemoryRange oRange ) {
            if( oRange == null )
                return;

            oCaret.Offset = oRange.Offset;

            End = oCaret;
            // BUG: This might be a problem when I attempt to move the pin
            // around b/c of user edits in another window.
            Pin   = new CaretInfo(oCaret) { Offset = oRange.Offset + oRange.Length };
        }

        public abstract bool IsSingleColumn( out int iColumn );

        public abstract int RowCount {
            get;
        }

        /// <summary>
        /// Called to set up the selections for the current row.
        /// </summary>
        protected void Set( int iCol, int iOffset, int iLength ) {
            _rgSelections[iCol] = _rgCache[iCol];

            _rgSelections[iCol].Offset = iOffset;
            _rgSelections[iCol].Length = iLength;
        }

        public abstract IPgSelection.SlxnType PrepRanges( Row oRow );
    }

    /// <summary>
    /// This is going to be a single cell selector. Seems like it's really
    /// going to be the standard use case. >_<;;;
    /// </summary>
    public class SelectionSingle : Selection {
        public SelectionSingle( int iMaxCols) : base(iMaxCols) {
        }

        public override IPgSelectionTask DoSelection( CacheMultiBase oHost, IMemoryRange oRange ) {
            return new SingleSelectionTask( oHost, oRange );
        }

        public override int StartAt  => Pin.Row.At;

        public override int EndAt    => StartAt;

        /// <summary>
        /// A little non-sequitar, but this needs to be zero
        /// so the windowmulticolumn simply uses the cursor
        /// </summary>
        public override int RowCount {
            get {
                if( !IsValid )
                    return 0;

                try {
                    int iRows = int.Abs( Pin.Row.At - End.Row.At ) + 1;

                    if( iRows == 1 ) {
                        IPgSelection.SlxnType eSlxn = PrepRanges(End.Row);

                        if( eSlxn != IPgSelection.SlxnType.Equal )
                            return 1; // Technically an error for us, but meh...

                        if( Pin.Column != End.Column )
                            return 1;

                        return 0;
                    }

                    //IPgSelection.SlxnType eSlxn = IsSelection( Caret.Row );

                    //if( eSlxn != IPgSelection.SlxnType.Equal ) 
                    //    return 0; // Technically an error.

                    //int iCharCount = 0;
                    //foreach( IColorRange oSlxn in _rgSelections ) {
                    //    if( oSlxn != null )
                    //        iCharCount += oSlxn.Length;
                    //}
                    //if( iCharCount > 0 )
                    //    return 1;
                    return iRows;
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
        /// This sets the column selectors based on this row. I expect
        /// the selection to be pinned/frozen.
        /// </summary>
        /// <param name="oRow">Selections on the current row.</param>
        /// <returns>Since we are a single row selector, selection
        /// is either "equal" or "nothing"</returns>
        public override IPgSelection.SlxnType PrepRanges( Row oRow ) {
            try {
                // Clear out any previous selections.
                for( int i=0; i< _rgCache.Length; ++i ) {
                    _rgSelections[i] = null;
                }
                if( !IsValid ) {
                    return IPgSelection.SlxnType.None;
                }
                if( oRow != Pin.Row ) {
                    return IPgSelection.SlxnType.None;
                }

                if( End.Offset > Pin.Offset ) {
                    Set( Pin.Column, Pin.Offset, End.Offset - Pin.Offset );
                } else {
                    Set( Pin.Column, End.Offset, Pin.Offset - End.Offset );
                }

                return IPgSelection.SlxnType.Equal;
            } catch ( Exception oEx ) {
                Type[] rgErrors = { typeof( NullReferenceException ),
                                    typeof( ArgumentOutOfRangeException ) };
                if( rgErrors.IsUnhandled( oEx ) )
                    throw;
            }
            return IPgSelection.SlxnType.None; // Technically error. But let's see how it goes.
        }

        public override bool IsSingleColumn(out int iColumn) {
            iColumn = -1;

            if( !IsValid )
                return false;

            iColumn = Pin.Column;

            return true;
        }
    }

    /// <summary>
    /// This is a multicolumn stream selector. I'm not using it in the default
    /// case since it seems to be less useful. Selects multible rows and columns.
    /// </summary>
    public class SelectionStream : Selection {
        readonly IPgCaretInfo<Row>[] _rgRowHighLow = new IPgCaretInfo<Row>[2];
        readonly IPgCaretInfo<Row>[] _rgColHighLow = new IPgCaretInfo<Row>[2];

        public SelectionStream( int iMaxCols ) : base ( iMaxCols ) {
        }

        /// <summary>
        /// We need this for the single selection. But if we ever want multi column/row
        /// selection we'll need ti implement a selection task object.
        /// </summary>
        /// <exception cref="NotImplementedException"></exception>
        public override IPgSelectionTask DoSelection( CacheMultiBase oHost, IMemoryRange oRange ) {
            throw new NotImplementedException();
        }

        public override int StartAt => _rgRowHighLow[0].Row.At;
        public override int EndAt   => _rgRowHighLow[1].Row.At;

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
            if( End.Column > Pin.Column ) {
                _rgColHighLow[0] = Pin;
                _rgColHighLow[1] = End;
            } else {
                if( End.Column == Pin.Column &&
                    End.Offset >  Pin.Offset ) 
                {
                    _rgColHighLow[0] = Pin;
                    _rgColHighLow[1] = End;
                } else {
                    _rgColHighLow[0] = End;
                    _rgColHighLow[1] = Pin;
                }
            }

            bool fEqualCol = End.Column == Pin.Column;

            if( fEqualCol ) {
                int iCol = End.Column;

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

        /// <summary>
        /// This has the side effect of resetting the column selection
        /// values so you may query for those after this call or
        /// repaint properly.
        /// </summary>
        /// <returns>Overall selection type on the row.</returns>
        public override IPgSelection.SlxnType PrepRanges( Row oRow ) {
            try {
                // Clear out any previous selections.
                for( int i=0; i< _rgCache.Length; ++i ) {
                    _rgSelections[i] = null;
                }
                if( End == null || End.Row == null ) {

                    return IPgSelection.SlxnType.Equal;
                }

                if( End.Row.At > Pin.Row.At ) {
                    _rgRowHighLow[0] = Pin;
                    _rgRowHighLow[1] = End;
                } else {
                    _rgRowHighLow[0] = End;
                    _rgRowHighLow[1] = Pin;
                }
                if( oRow.At < _rgRowHighLow[0].Row.At )
                    return IPgSelection.SlxnType.None;
                if( oRow.At > _rgRowHighLow[1].Row.At )
                    return IPgSelection.SlxnType.None;

                if( End.Row.At == Pin.Row.At ) {
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

        public override int RowCount {
            get {
                if( End == null )
                    return 0;

                try {
                    int iRows = Math.Abs( End.Row.At - Pin.Row.At );

                    if( iRows > 0 )
                        return iRows + 1;

                    IPgSelection.SlxnType eSlxn = PrepRanges( End.Row );

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
        public override bool IsSingleColumn( out int iColumn ) {
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

}
