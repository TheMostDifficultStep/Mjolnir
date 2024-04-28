using System;
using System.IO;

using Play.Interfaces.Embedding;

namespace Play.Edit {
    /// <summary>Simple string builder type class.</summary>
    /// <remarks>So looking over the comments in string builder, large objects are undesirable. That's 
    /// about 40k char's. (80k Bytes). StringBuilder get's around this by building in chunks...
    /// http://referencesource.microsoft.com/#mscorlib/system/text/stringbuilder.cs,adf60ee46ebd299f
    /// This has huge ramifications for displaying the line, since we supply GDI our buffer.
    /// TODO: Frankly, it seems like a lot of work for an edge case. But since the parser is stream based
    /// perhaps we could make a line object that uses the string builder, use it only in that case
    /// and then only display a message that the line is too big (we do that anyway) and then have a 
    /// feature that can offer to break big lines.
    /// </remarks>
    public class MyStringBuilder 
    {
        char[] _rgValue;
        int    _iLength=-1; // -1 indicates a reset.
        
        /// <summary>
        /// Initialize our class.
        /// </summary>
        /// <param name="strInit">String to initialize our character array with.</param>
        /// <remarks>There is no constructor that takes a character array since I can't
        /// read "a line" of text from TextReader.</remarks>
        public MyStringBuilder( string strInit )
        {
			if( strInit.Length < 1 ) {
				_rgValue = new char[2];
				return;
			}

            _rgValue = strInit.ToCharArray(); // BUG: If string is empty, let's leave _rgValue equal to null!
        }
        
        public MyStringBuilder( ReadOnlySpan<char> spInit )
        {
			if( spInit.Length < 1 ) {
				_rgValue = new char[2];
				return;
			}

            _rgValue = new char[spInit.Length];

            spInit.CopyTo( _rgValue );
        }

        public int Capacity
        {
            get {
                if( _rgValue == null )
                    return( 0 );

                return ( _rgValue.Length );
            }
            
            set {
                char[] rgNew = new char[value];
                int    iMaxLen = 0;
                if( _rgValue != null ) {
                    iMaxLen = _rgValue.Length < rgNew.Length ? _rgValue.Length : rgNew.Length;
                    
                    for( int i=0; i<iMaxLen; ++i ) {
                        rgNew[i] = _rgValue[i];
                    }
                }
                for( int i=iMaxLen; i<rgNew.Length; ++i ) {
                    rgNew[i] = '\0';
                }
                _rgValue = rgNew;
            }
        }

        /// <summary>
        /// Return the length of the this line. Does not include the EOL terminator.
        /// </summary>
        public int Length {
            get {
                if( _rgValue == null )
                    return( 0 );

                if( _iLength < 0 ) {
                    // Look for the null terminator. BUG this is probably obsolete anymore. I can keep track of the length.
                    int i = 0;
                    while( i < _rgValue.Length )
                    {
                        if( _rgValue[i] == 0 )
                            break;
                        ++i;
                    }
                    _iLength = i;
                } else {
                    if( _rgValue.Length < _iLength )
                        _iLength = _rgValue.Length; 
                }
            
                // Don't have one then the length is the number of characters.
                return( _iLength );
            }
        }

        /// <summary>
        /// Try to pivot ALL array operations into this procedure...
        /// </summary>
        /// <remarks>I used to throw an exception if the Start or Length 
        /// is less than 0. However, since we're already returning false if
        /// the edit is out of range. It seems better just to return false.
        /// Editors will often send start = -1, length 1 when user
        /// presses delete on an empty buffer. This could happen a lot
        /// It's safer to check here for all cases anyway.</remarks>
        public bool Replace( int iDelOff, int iDelLen, ReadOnlySpan<char> spInsert ) {
            if( iDelOff < 0 || iDelLen < 0 )
                return false;
            if( iDelOff + iDelLen > Length )
                return false;
            if( spInsert == null )
                spInsert = string.Empty;

            try {
                int iDiff = spInsert.Length - iDelLen;          // > 0 means we're adding stuff.

                if( iDiff >= 0 && Length + iDiff + 1 > Capacity ) { // bump up the Capacity if needed. 
                    Capacity = Length + iDiff + 1;
                }
                int iPush = iDelOff + iDelLen;                  // Shift everything past this index.

                if( iDiff != 0 && iPush < _iLength ) {
                    Array.ConstrainedCopy(      sourceArray:_rgValue, iPush, 
                                           destinationArray:_rgValue, iPush+iDiff, 
                                           _iLength - iPush );
                }
                foreach( char c in spInsert ) {
                    _rgValue[iDelOff++] = c;
                }
                _iLength += iDiff;
                _rgValue[_iLength] = (char)0;
            } catch( Exception oEx ) {
                Type[] rgErrors = { typeof( ArgumentNullException ),
                                    typeof( RankException ),
                                    typeof( ArrayTypeMismatchException ),
                                    typeof( InvalidCastException ),
                                    typeof( ArgumentOutOfRangeException ),
                                    typeof( ArgumentException ),
                                    typeof( NullReferenceException ),
                                    typeof( IndexOutOfRangeException ) };
                if( rgErrors.IsUnhandled( oEx ) )
                    throw;

                return false;
            }

            return true;
        }

		public void Empty() {
			_iLength = 0; 

			try {
				_rgValue[0] = (char)0;
			} catch( Exception oEx ) {
				Type[] rgErrors = { typeof( IndexOutOfRangeException ),
									typeof( NullReferenceException ) };

				if( rgErrors.IsUnhandled( oEx ) ) 
					throw;
			}
		}
        
        /// <summary>
        /// Return the character at the given index. If the index is less than zero
        /// then the character returned is '\0'. If the character is greater than
        /// the length of the line we return '\r'.
        /// TODO: Need to rethink this. Overly protective. Need to expect exceptions.
        /// </summary>
        /// <param name="iIndex">The index of the character to retrieve.</param>
        /// <returns>A character.</returns>
        public char this[int iIndex] {
            get {
                if( _rgValue == null )
                    return '\0';

                if( iIndex < 0 )
                    return '\0';
                    
                // Return just a single character EOF. The stream based reader will
                // get the \r but the line based one will not. BUG: The return char
                // should be inherited from the document and not always \r. Also won't
                // be correct in the EOF case!
                if( iIndex >= Length )
                    return '\r';

                return _rgValue[iIndex];
            }
        }

        /// <summary>
        /// Creates a new string at the start offset and length given.
        /// </summary>
        /// <param name="iStart">Starting offset.</param>
        /// <param name="iLength">Number of characters.</param>
        /// <returns>A string.</returns>
        /// <remarks>Since I'm only using this for the banner I simply
        /// return an empty string if encounter an error.
        /// exceptions.</remarks>
        public string SubString(int iStart, int iLength) {
            try {
                if( _rgValue == null )
                    return string.Empty;

                if( iStart >= this.Length )
                    iStart  = this.Length - 1;
                if( iStart < 0 )
                    iStart  = 0;
                if( iStart + iLength > this.Length )
                    iLength = this.Length - iStart;

                return new String( _rgValue, iStart, iLength );
            } catch( Exception oEx ) {
                Type[] rgErrors = { typeof( ArgumentOutOfRangeException ),
                                    typeof( ArgumentNullException ) };
                if( rgErrors.IsUnhandled( oEx ) )
                    throw;

                return String.Empty;
            }
        }

        /// <summary>
        /// The future, the glorious future...
        /// </summary>
        /// <exception cref="ArrayTypeMismatchException" />
        /// <exception cref="ArgumentOutOfRangeException" />
        public Span<char> SubSpan( int iStart, int iLength ) {
            return _rgValue.AsSpan( iStart, iLength );
        }

        public Span<char> AsSpan => _rgValue.AsSpan();

        /// <summary>
        /// Returns a string for the line. This allocates a new string on each
        /// call. Be careful!
        /// </summary>
        /// <returns></returns>
        public override string ToString() {
            if( _rgValue == null )
                return ( string.Empty );

            return ( new String(_rgValue, 0, this.Length) );
        }

        public void Save( TextWriter oStream, int iStart = 0, int iLength = int.MaxValue ) {
            if( _rgValue == null )
                return;

            if( iLength > this.Length - iStart )
                iLength = this.Length - iStart;
            if( iStart >= this.Length )
                iStart  = this.Length-1;
            if( iStart < 0 )
                iStart = 0;

            if( oStream != null )
                oStream.Write( _rgValue, iStart, iLength );
        }

        /// <summary>
        /// Try insert at the given index. 
        /// </summary>
        /// <param name="iIndex">The character position to begin inserting.</param>
        /// <param name="cChar">The character to insert. TODO: Should probably filter /n/r.</param>
        /// <returns>Returns false if the index is greater than the length or less than zero.</returns>
        [Obsolete]public virtual bool TryInsert( int iIndex, char cChar )
        {
            if( Length + 1 >= Capacity ) {
                Capacity += 10; // bump up the capacity by a little.
            }
            
            if( iIndex > Length || iIndex < 0 ) {
                return( false );
            }
            
            // Make some room
            _rgValue[Length+1] = '\0';
            for( int i = Length; i > iIndex; --i )
            {
                _rgValue[i] = _rgValue[i-1];
            }
            // Add the character given!
            _rgValue[iIndex] = cChar;
            _iLength = -1;

            return( true );
        }
        
        /// <summary>
        /// Try and insert the given string segment into our buffer.
        /// </summary>
        /// <param name="iDestOffset">Target position in our buffer</param>
        /// <param name="strSource">The string containing the source we wish to copy. 
        /// TODO: If /n/r in the string, it will go in the buffer, not good.</param>
        /// <param name="iSrcIndex">Start of segment in the source to copy from.</param>
        /// <param name="iSrcLength">Length of the segment to copy.</param>
        /// <returns>false if the destination position is outside of our buffer.</returns>
        /// <remarks>We take a string because it is immutable. There is no danger to the caller
        /// that we might remember the address and start monkeying around with it at some
        /// later time.</remarks>
        [Obsolete]public virtual bool TryInsert( int iDestOffset, ReadOnlySpan<char> strSource, int iSrcIndex, int iSrcLength )
        {
            // Make sure they don't want us copy to outside of ourselves. But it is ok to append at the very end.
            if( iDestOffset < 0 || iDestOffset > this.Length )
                return( false );
            // Make sure that the don't want to copy outside of their source!
            if( iSrcIndex < 0 || iSrcIndex + iSrcLength > strSource.Length )
                return( false );

            // Is there enough room for the next text block?
            int iNewLength = this.Length + iSrcLength;
            if( this.Capacity <= iNewLength ) {
                char[] rgNew = new char[this.Length + iSrcLength + 10];
                
                _rgValue.CopyTo( rgNew, 0 );
                _rgValue = rgNew; // Blast the old array.
            }
            
            // Now, just push the existing text behind the edit over...
            Array.Copy( _rgValue, iDestOffset, _rgValue, iDestOffset + iSrcLength, this.Length - iDestOffset ); 

            // And stuff the new stuff into the hole. Well, I guess I can trust Microsquish not to
            // mess with my character buffer later! ^_^;
            //strSource.CopyTo( iSrcIndex, _rgValue, iDestOffset, iSrcLength );
            for( int i = 0; i < iSrcLength; ++ i ) {
                _rgValue[iDestOffset+i] = strSource[iSrcIndex+i];
            }

            // We can do this since we allocate a little extra or have at least this much extra space left.
            _rgValue[iNewLength] = '\0';
            _iLength = -1;

            return( true );
        }
        
        /// <summary>
        /// Remove a section of characters.
        /// </summary>
        /// <param name="iIndex">start offset</param>
        /// <param name="iLength">Length</param>
        /// <param name="strRemoved">A copy of what was removed.</param>
        /// <returns>true if successful.</returns>
        [Obsolete]public virtual bool TryDelete( int iIndex, int iLength, out string strRemoved )
        {
            strRemoved = String.Empty;
            
            if( iIndex < 0 || iIndex > this.Length )
                return( false );
                
            if( iIndex == this.Length || iLength == 0 )
                return( true );
                
            int iMaxLength = this.Length - iIndex;
            if( iLength > iMaxLength )
                iLength = iMaxLength;
            if( iLength < 0 )
               return( false );                
                
            strRemoved = new String( _rgValue, iIndex, iLength );
            
            int iRightIndex  = iIndex + iLength;
            int iRightLength = this.Length - iRightIndex;
            
            // Move everything over in the string.
            if( iRightLength > 0 && iRightIndex < this.Length ) {
                Array.Copy( _rgValue, iRightIndex, _rgValue, iIndex, iRightLength );
                _rgValue[ iIndex + iRightLength ] = '\0';
            } else {
                _rgValue[ iIndex ] = '\0';
            }
            _iLength = -1;
            
            return( true );
        }
    }
}
