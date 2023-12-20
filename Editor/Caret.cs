using System;

namespace Play.Edit {
    /// <summary>
    /// A converter class to deal with pixel to line/character positioning. I created
    /// this after the EditTrack. So there will be some duplicity. I'll make
    /// the EditTracker dependent on this object eventually.
    /// </summary>
    /// 

    public interface IPgCacheCaret : ILineRange {
        int Advance { get; set; }
    }

    public class SimpleLineCaret :
        ILineRange
    {
        protected Line _oLine;
        protected int  _iOffset = 0;

        public SimpleLineCaret( Line oLine, int iLineOffset ) {
            if( oLine == null )
                throw new ArgumentNullException();
            if( iLineOffset < 0 )
                iLineOffset = 0;
            if( iLineOffset > oLine.ElementCount )
                iLineOffset = oLine.ElementCount;

            _iOffset = iLineOffset;
            _oLine   = oLine;
        }

		public int ColorIndex {
			get { return( 0 ); }
		}

        public override string ToString() {
            return( "(" + _iOffset.ToString() + "...) " + _oLine.SubString( 0, 50 ) );
        }

        public int At {
            get { return( Line.At ); }
        }

        /// <summary>
        /// Returns the current line relative offset.
        /// </summary>
        public int Offset {
            get {
                return( _iOffset );
            }
            
            set {
                if( value > _oLine.ElementCount )
                    value = _oLine.ElementCount;
                if( value <= 0 )
                    value = 0;

                _iOffset = value;
            }
        }
        
        public int Length {
            get {
                return( 0 );
            }
            
            set {
                throw new ArgumentOutOfRangeException("Caret length is always zero" );
            }
        }
        
        /// <summary>
        /// Return the line we are situated on.
        /// </summary>
        /// <remarks>We need to keep a line pointer instead of an At index in case
        /// lines above us get deleted! We auto track that way. </remarks>
        public Line Line {
            get {
                return( _oLine );
            }
            
            set {
                _oLine = value ?? throw new ArgumentNullException();
            }
        }
    }
    
    public class CaretPosition : SimpleLineCaret 
    {
        public int _iColumn = 0;
        public CaretPosition( Line oLine ) : base( oLine, 0 ) {
        }

        /// <summary>
        /// Check that the offset that we are currently at is within the
        /// max and min of the current line. If not move it within.
        /// We have to do this since we try to stay in the same column 
        /// until forced to "reset."
        /// </summary>
        public bool TrimOffset()
        {
            if( _oLine == null )
                return( false );

            if( _iOffset > _oLine.ElementCount )
                _iOffset = _oLine.ElementCount;
            if( _iOffset < 0 )
                _iOffset = 0;

            return( true );
        }
        
        /// <summary>
        /// Split the current line at the current offset.
        /// </summary>
        public void Split( Editor.Manipulator oBulk )
        {
            if( TrimOffset() ) {
                oBulk.LineSplit( _oLine.At, _iOffset);
            }
        }
        
        /// <summary>
        /// Delete the character at the current offset.
        /// </summary>
        public void Delete( Editor.Manipulator oBulk )
        {
            if( TrimOffset() ) {
                if( _iOffset == _oLine.ElementCount )
                    oBulk.LineMergeWithNext( _oLine.At );
                else
                    oBulk.LineTextDelete( _oLine.At, new ColorRange( _iOffset, 1, -1 ) );
            }
        }
        
        /// <summary>
        /// Delete the character at the previous offset.
        /// </summary>
        public void Back( Editor.Manipulator oBulk )
        {
            if( TrimOffset() ) {
                if( _iOffset == 0 ) {
                    if( oBulk.IsHit( _oLine.At - 1 ) ) {
                        oBulk.LineMergeWithNext( _oLine.At - 1 );
                    }
                } else {
                    oBulk.LineTextDelete( _oLine.At, new ColorRange( _iOffset - 1, 1, -1 ) );
                }
            }
        }
    } // end class
} // end namespace
