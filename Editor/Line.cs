using System;
using System.Collections.Generic;
using System.IO;
using Play.Interfaces.Embedding;
using Play.Parse;

namespace Play.Edit {
    /// <summary>
    /// A simple little stream on the single line.
    /// </summary>
	public class SubStream : DataStream<char> 
	{
		Line   _oLine;
		int    _iPos = 0;

		public SubStream( Line oLine )
		{
			_oLine = oLine ?? throw new ArgumentNullException();
		}

        public Line Line => _oLine;

        public void Reset( Line oLine ) {
            if( oLine != null )
                _oLine = oLine;

            _iPos = 0;
        }

		public void Seek( int p_iDistance ) 
		{
			Position = _iPos + p_iDistance;
		}

		public override bool InBounds( int p_iPos )
		{
			return( p_iPos < _oLine.ElementCount );
		}

		public override int Position 
		{
			get 
			{
				return( _iPos );
			}
			set
			{
				if( value < 0 )
					throw new ArgumentOutOfRangeException();
				if( value >= _oLine.ElementCount )
					throw new ArgumentOutOfRangeException();

				_iPos = value;
			}
		}

		public override char this [int iPos ] 
		{
			get 
			{
				_iPos = iPos;
				return( _oLine[iPos] );
			}
		}

		public override string SubString( int iPos, int iLen ) 
		{
			_iPos = iPos;
			return( _oLine.SubString( iPos, iLen ) );
		}
	}

    /// <summary>
    /// abstract for operations that can happen on single line. I no longer inherit from any kind of
    /// string builder, because the editor can now hold any object that can generate a text string.
    /// Either mutable for editing, or Immutable for readonly.
    /// </summary>
    public abstract class Line :
        IComparable<Line>,
        IComparable<string>,
        IReadableBag<char>,
        IDisposable
    {
        readonly ICollection<IColorRange> _rgFormatting = new List<IColorRange>();

        int    _iLine             = -1;
        int    _iCumulativeLength = 0; // Num of chars infront of us.
        int    _iWordCount        = 0;

        public bool IsDirty { get; protected set; }
        public void ClearDirty() { IsDirty = false; }

        public Line( int iLine ) {
            _iLine = iLine;
        }

        public static bool IsNullOrEmpty( Line oLine ) {
            if( oLine == null )
                return( true );
            if( oLine.At < 0 )
                return( true );
            if( string.IsNullOrEmpty( oLine.ToString() ) )
                return( true );

            return( false );
        }

        /// <summary>
        /// A member variable that the user of the editor can use to store
        /// line relavent data.
        /// </summary>
        /// <remarks>TODO/BUG: Let's upgrade to IPgWordRange, we'll have to touch
        /// a number of places, the manipulator for one.</remarks>
        public ICollection<IColorRange> Formatting {
            get {
                return ( _rgFormatting );
            }
        }

        /// <summary>
        /// Use this function to set the cumulative count of characters before this line. 
        /// </summary>
        /// <param name="iSum">The number of characters in front of this line!</param>
        /// <returns>Cumulative count plus the length of this line.</returns>
        public int Summate(int iLine, int iSum) {
            _iLine             = iLine;
            _iCumulativeLength = iSum;

            return ( iSum + this.ElementCount + 1 ); // add one for the EOL marker.
        }

        /// <summary>
        /// Return the number of characters in front of this line.
        /// </summary>
        public int CumulativeLength {
            get {
                return ( _iCumulativeLength );
            }
        }

        /// <summary>
        /// Word count for this line. The cache manager will update the line as the user edits but as of
        /// 7/18/2012 we don't have a start parse over the entire file for word count.
        /// </summary>
        public int WordCount {
            get {
                return( _iWordCount );
            }
            set { 
                _iWordCount = value;
            }
        }

        /// <summary>
        /// Get the line position. The line element is held within an external collection
        /// You can get that index via this member.
        /// </summary>
        public int At {
            get {
                return( _iLine );
            }
        }
        
        public int CompareTo(Line oOther) {
            return( _iLine - oOther.At );
        }

        public int CompareTo( string other ) {
           return other.CompareTo( this.ToString() );
        }

        public int Compare(string strOther, bool IgnoreCase = false ) {
            int iDiff = strOther.Length - this.ElementCount;

            if( iDiff != 0 )
                return iDiff;

            for (int i = 0; i < strOther.Length; i++ ) {
                if( IgnoreCase )
                    iDiff = char.ToLower( strOther[i] ) - char.ToLower( this[i] );
                else
                    iDiff = strOther[i].CompareTo( this[i] );
                
                if( iDiff != 0 ) {
                    return iDiff;
                }
            }

            return 0;
        }

        /// <summary>
        /// Extra spot for document users. The document itself won't use this.
        /// Note: It won't get overtly disposed. So don't forget any unmanaged goodies in there.
        /// </summary>
        public abstract Object Extra {
            get;
            set;
        }

        public SubStream GetStream() {
            return( new SubStream( this ) );
        }

        /// <summary>
        /// A little helper to try to keep the formatting inline with the text until next parse.
        /// </summary>
        protected void FormattingShiftInsert( int iOffset, int iLength ) {
            foreach( IColorRange oRange in Formatting ) {
                Marker.ShiftInsert(oRange, iOffset, iLength);
            }
        }

        public virtual char[]  Buffer { get { return( null ); } }

        public abstract int    ElementCount { get; }
        public virtual char   this[int iIndex] { get { return( '\0' ); } }

        public abstract string SubString( int iStart, int iLength ); 
      //public override string ToString() { return new string( Buffer, 0, ElementCount ); } // TODO TextLine() we can remove it's implementation.
        
        public virtual bool TryInsert( int iIndex, char cChar ) { return( false ); }
        public virtual bool TryInsert( int iDestOffset, string strSource, int iSrcIndex, int iSrcLength ) { return( false ); }
        public virtual bool TryDelete( int iIndex, int iLength, out string strRemoved ) { strRemoved = string.Empty; return( false ); }
        public virtual bool TryAppend( string strValue ) { return TryInsert( ElementCount, strValue, 0, strValue.Length ); }

		public virtual void Empty() { }

        public virtual void Dispose() { _iLine = -1; }

        public abstract void Save( TextWriter oSteam, int iOffset = 0, int iLength = int.MaxValue );
    }

    /// <summary>
    /// Mutable array of text. 
    /// </summary>
    /// <remarks>I'd love to use string builder in the future, but that class isn't friendly
    /// enough for me to get at the chunks for display in the GDI. I'll probably always need
    /// my own implementation. Since the vast majority of text more like less than 120 unicode
    /// char's we're ok in general. I won't display text over 60,000 characters anyway.
    /// BUG: I should drop that down to 40K chars and live with that until I chunk the data.
    /// But in all honesty it would probably be more cost effective to make a feature that
    /// uses the associated parser to offer to break big lines.</remarks>
    public class TextLine : Line
    {
        readonly MyStringBuilder _sbBuffer;
        object _oExtra;

        /// <summary>
        /// The string to initalize my line with. I take a string 
        /// mainly due to the fact the line reader on the stream returns a string.
        /// </summary>
        /// <param name="strValue">A string to copy into our line.</param>
        public TextLine( int iLine, string strValue) : base( iLine )
        {
            _sbBuffer = new MyStringBuilder( strValue );
        }

        public override void Save( TextWriter oStream, int iStart = 0, int iLength = int.MaxValue )
        {
            _sbBuffer.Save( oStream, iStart, iLength );
        }

        public override char[] Buffer {
            get {
                return( _sbBuffer.Buffer );
            }
        }

        public override char this[int iIndex] {
            get {
                return( _sbBuffer[iIndex] );
            }
        }

        public override int ElementCount {
            get {
                return( _sbBuffer.Length );
            }
        }

        public override string ToString()
        {
            return( _sbBuffer.ToString() );
        }

        /// <exception cref="ArgumentOutOfRangeException" />
        /// <exception cref="ArgumentNullException" />
        public override string SubString( int iStart, int iLength ) {
            return( _sbBuffer.SubString( iStart, iLength ) );
        } 

        /// <summary>
        /// Insert the string into this line of text.
        /// </summary>
        /// <param name="iDestOffset">Position to push to the right.</param>
        /// <param name="strSource">Source of insert.</param>
        /// <param name="iSrcIndex">Where in the source to start.</param>
        /// <param name="iSrcLength">How much of the source to copy.</param>
        /// <returns></returns>
        public override bool TryInsert( int iDestOffset, string strSource, int iSrcIndex, int iSrcLength ) {
            bool fReturn = _sbBuffer.TryInsert(iDestOffset, strSource, iSrcIndex, iSrcLength);

            if( fReturn ) {
                FormattingShiftInsert( iDestOffset, iSrcLength );
                IsDirty = true;
            }
            return( fReturn );
        }

        /// <summary>
        /// Insert a single character at the given index.
        /// </summary>
        /// <param name="iIndex">Index to push to the right.</param>
        /// <param name="cChar">Character to put at the index.</param>
        /// <returns></returns>
        public override bool TryInsert( int iIndex, char cChar ) {
            bool fReturn = _sbBuffer.TryInsert(iIndex, cChar);

            if( fReturn ) {
                FormattingShiftInsert( iIndex, 1 );
                IsDirty = true;
            }

            return( fReturn );
        }

        public override bool TryDelete( int iIndex, int iLength, out string strRemoved ) {
            if( _sbBuffer.TryDelete( iIndex, iLength, out strRemoved ) ) {
                IsDirty = true;
                return true;
            }

            return false;
        }

		public override void Empty() {
            if( _sbBuffer.Length > 0 )
                IsDirty = true;

			_sbBuffer.Empty();
		}

		/// <summary>
		/// Extra spot for document users. The document itself and views on it shouldn't use this. But we'll see how that goes.
		/// Note: It won't get overtly disposed. So don't stick any unmanaged goodies in there.
		/// </summary>
		public override Object Extra {
            get { 
                return( _oExtra );
            }
            set {
                _oExtra = value;
            }
        }

    } // TextLine
}
