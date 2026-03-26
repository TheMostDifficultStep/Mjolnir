using System;
using System.Collections;
using System.Collections.Generic;

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

    public abstract class Selection {
        public IPgCaretInfo<Row> Caret { get; protected set; }
        public CaretInfo         Pin   { get; protected set; }

        protected readonly IColorRange      [] _rgSelections;
        protected readonly IColorRange      [] _rgCache; // Color ranges that the selections can use.

        protected bool _fFrozen = true;
        public Selection( int iMaxCols ) {
            _rgSelections = new IColorRange[iMaxCols];
            _rgCache      = new IColorRange[iMaxCols];

            for( int i=0; i< _rgCache.Length; ++i ) {
                _rgCache[i] = new ColorRange( 0, 0, -1 );
            }
        }

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
            Caret = null;
            for( int i=0; i< _rgSelections.Length; ++i ) {
                _rgSelections[i] = null;
            }
            _fFrozen = true;
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

        public abstract IPgSelection.SlxnType IsSelection( Row oRow );
    }

    /// <summary>
    /// This is going to be a single cell selector. Seems like it's really
    /// going to be the standard use case. >_<;;;
    /// </summary>
    public class SelectionSingle : Selection {
        public SelectionSingle(int iMaxCols) : base(iMaxCols) {
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

        protected bool IsValid {
            get {
                if ( Pin.Row is null )
                    return false;
                if( Caret is null || Caret.Row is null )
                    return false;

                return true;
            }
        }

        public override IPgSelection.SlxnType IsSelection(Row oRow) {
            try {
                // Clear out any previous selections.
                for( int i=0; i< _rgCache.Length; ++i ) {
                    _rgSelections[i] = null;
                }
                if( !IsValid )
                    return IPgSelection.SlxnType.None;

                if( oRow != Pin.Row ) {
                    return IPgSelection.SlxnType.None;
                }

                if( Caret.Offset > Pin.Offset ) {
                    Set( Pin.Column, Pin.Offset,   Caret.Offset - Pin.Offset );
                } else {
                    Set( Pin.Column, Caret.Offset, Pin.Offset - Caret.Offset );
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

        /// <summary>
        /// This has the side effect of resetting the column selection
        /// values so you may query for those after this call or
        /// repaint properly.
        /// </summary>
        /// <returns>Overall selection type on the row.</returns>
        public override IPgSelection.SlxnType IsSelection( Row oRow ) {
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

        public override int RowCount {
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
