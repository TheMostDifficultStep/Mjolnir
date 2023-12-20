using System;

using Play.Parse;

namespace Play.Edit {

    public class ColorRange :
        IColorRange,
        IPgWordRange
    {
        protected int _iOffset = -1;
        protected int _iLength = 0;
        protected int _iColorIndex = 0;

        public ColorRange(int iOffset, int iLength, int iColorIndex ) {
            _iOffset     = iOffset;
            _iLength     = iLength;
            _iColorIndex = iColorIndex;
        }
        
        public ColorRange(int iOffset, int iLength ) {
            _iOffset     = iOffset;
            _iLength     = iLength;
            _iColorIndex = -1;
        }
        
        public int Offset {
            get {
                return _iOffset;
            }
            
            set {
                if( value <= 0 )
                    value = 0;
                _iOffset = value;
            }
        }

        public int Length {
            get {
                return _iLength;
            }
            set {
                if( _iLength != int.MaxValue ) {
                    if( value <= 0 )
                        value = 0;
                    _iLength = value;
                }
            }
        }

        public int ColorIndex {
            get {
                return _iColorIndex;
            }

            set {
                _iColorIndex = value;
            }
        }
        public virtual bool IsWord => false;
        public bool         IsTerm => true;
        public virtual string StateName => "Manual Color Range";
    } // end class

    /// <summary>
    /// A little class to stand in for a MemoryElem at times. 
    /// </summary>
    public class WordRange : ColorRange, IPgWordRange {
        public WordRange( int iOffset, int iLength, int iColor ) : base( iOffset, iLength, iColor ) {
        }

        public bool   IsWord    => false;
        public bool   IsTerm    => true;

        public string StateName => string.Empty;
    }

    /// <summary>
    /// Unlike the ColorRange class, this object is more tuned to be line/offset
    /// based. This is basically a "default" formatting object we can use on 
    /// lines that have parse errors and we used this for the Selection control.
    /// </summary>
    public class LineRange :
        ColorRange,
        ILineSelection,
        IDisposable 
    {
        protected Line _oLine          = null;
        protected bool _fIsEOLSelected = false;
        
        public LineRange() : base( -1, 0, 0 ) {
        }

        public LineRange( Line oLine, int iOffset, int iLength, SelectionTypes eSlxnTypes ) :
            base( iOffset, iLength, (int)eSlxnTypes ) 
        {
            _oLine = oLine;
        }
        
        public void Reset( Line oLine, int iOffset, int iLength ) {
            _oLine   = oLine;
            _iOffset = iOffset;
            _iLength = iLength;
            _fIsEOLSelected = false;
        }
        
        public void Reset() {
            _iOffset = -1;
            _iLength = 0;
            _oLine   = null;
            _fIsEOLSelected = false;
        }
        
        /// <summary>
        /// Set the length based on a start and end offset. The
        /// rightmost character, or the highest character position is
        /// outside of the resulting range.
        /// </summary>
        /// <param name="oLine"   >The line we are resetting</param>
        /// <param name="iEdgeOne">One end of the range.</param>
        /// <param name="iEdgeTwo">Other end of the range.</param>
        public void ResetExtent( Line oLine, int iEdgeOne, int iEdgeTwo )
        {
            int iLength = iEdgeOne - iEdgeTwo;

            if( iLength < 0 ) {
                Reset( oLine, iEdgeOne, -iLength );
            } else {
                Reset( oLine, iEdgeTwo, iLength );
            }
        }

        public int At {
            get { return( _oLine.At ); }
        }
        
        public Line Line {
            get { return( _oLine ); }
            set { _oLine = value; }
        }

        public virtual bool IsHit( Line oLine ) {
            if( _oLine == null )
                return( false );

            return( oLine == _oLine );
        }

        /// <summary>
        /// Return a stream position based on the line we are pointing to.
        /// returns -1 if line or offset is not set!
        /// </summary>
        public int Start {
            get {
                if( _oLine == null )
                    return( -1 );
                if( _iOffset < 0 )
                    return( -1 );

                return( _oLine.CumulativeLength + _iOffset );
            }
        }

        /// <summary>
        /// Return the last position as a stream. -1 if line or offset is not set.
        /// </summary>
        public int End {
            get {
                int iStart = Start;

                if( iStart < 0 )
                    return( -1 );

                return( iStart + Length );
            }
        }

        // This won't work since SelectMiddle has no _oLine value.
        //new public int Length {
        //    get {
        //        if( _iLength > _oLine.Length )
        //            return( _oLine.Length );

        //        return( _iLength );
        //    }
        //    set {
        //        base.Length = value;
        //    }
        //}

        public bool IsEOLSelected {
            get {
                return( _fIsEOLSelected );
            }
            set {
                _fIsEOLSelected = value;
            }
        }

        public SelectionTypes SelectionType {
            get {
                return( (SelectionTypes)_iColorIndex );
            }
            set {
                _iColorIndex = (int)value;
            }
        }

        public override string ToString()
        {
            if( _oLine != null )
                return( _oLine.SubString( this._iOffset, this._iLength ) );

            return( string.Empty );
        }

        /// <summary>
        /// Leaves the index, length, and color values intact. Only clears the Line.
        /// </summary>
        public virtual void Dispose() {
            _oLine = null;
        }
    } // LineRange
}
