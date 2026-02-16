using System;
using System.Xml;

using Play.Interfaces.Embedding;

namespace Play.Parse.Impl.Text
{
    public class TextTermEmpty : ProdElem<char>
	{
		public override bool IsEqual( int p_iMaxStack, DataStream<char> p_oStream, bool fLookAhead, int p_iPos, out int p_iMatch, out Production<char> p_oProd )
		{
			p_oProd  = null;
			p_iMatch = 0;

			return( true );
		}
		
		public override string ToString() 
		{ 
		    return( "~" ); 
		}

		public override bool IsVisible => false;
	} 

    public class TextTermLiteral : ProdElem<char>
	{
        public override bool IsEqual(int p_iMaxStack, DataStream<char> p_oText, bool fLookAhead, int p_iPos, out int p_iMatch, out Production<char> p_oProd) {
            p_iMatch = 0;
            p_oProd  = null;
            
            while( p_oText.InBounds( p_iPos + p_iMatch ) && p_iMatch < _iMaxMatch )
            {
                char cChar = p_oText[ p_iPos + p_iMatch ];
                // This can happen when the literal is at the end of the line. Ignore control characters.
                if( cChar < 32 ) 
                    break;

                if( cChar > 127 || Char.IsLetter( cChar ) )
                    p_iMatch ++;
                else 
                    break;
            }
            return (p_iMatch > 0 );
        }
        
        public override string ToString() {
            return( "A" );
        }
	}

    /// <summary>
    /// This is dangerous and should be used carefully. This function will match only one non blank 
    /// character. I'm thinking this will be useful when I want a production to attempt to deal
    /// with errors in the input.
    /// </summary>
    [Obsolete]public class TextTermNotBlank : ProdElem<char>
    {
        public override bool IsEqual(int p_iMaxStack, DataStream<char> p_oText, bool fLookAhead, int p_iPos, out int p_iMatch, out Production<char> p_oProd) {
            p_iMatch = 0;
            p_oProd  = null;
            
            if( p_oText.InBounds( p_iPos + p_iMatch ) )
            {
                char cChar = p_oText[ p_iPos + p_iMatch ];
                if( !Char.IsWhiteSpace( cChar ) )
                    p_iMatch ++;
            }
            return (p_iMatch > 0 );
        }
        
        public override string ToString() {
            return( "! : " + ID );
        }
    }

	public class TextTermNumber : ProdElem<char>
	{
		public override bool IsEqual( int p_iMaxStack, DataStream<char> p_oStream, bool fLookAhead, int p_iPos, out int p_iMatch, out Production<char> p_oProd )
		{
			p_iMatch = 0;
			p_oProd  = null;

			while( p_oStream.InBounds( p_iPos + p_iMatch ) && p_iMatch < _iMaxMatch ) {
				char cChar = p_oStream[ p_iPos + p_iMatch ];
				if( ( cChar >= '0' && cChar <= '9' ) )
					p_iMatch++;
				else
					break;
			}

			return( p_iMatch > 0 && p_iMatch <= _iMaxMatch && p_iMatch >= _iMinMatch );
		}
		public override string ToString()
		{
			return( "1" );
		}
    }

    public abstract class TextTermBase : ProdElem<char>
	{
        protected int m_iMin;

        public TextTermBase( )
        {
            m_iMin = 1;
        } 

		/// <summary>
		/// "Occur" seems to be different than minmatch and maxmatch. Might
		/// want to revue that.
		/// </summary>
		/// <param name="p_xmlElement"></param>
		/// <returns></returns>
        public override bool Load( XmlElement p_xmlElement )
        {
            String strOccur = p_xmlElement.GetAttribute( "occur" );

            if( !base.Load( p_xmlElement ) )
				return( false );

            if (strOccur != null)
            {
                if (strOccur == "*")
                    m_iMin = 0;
                else
                    m_iMin = 1;
            }
            return (true);
        } 
		public override bool IsVisible => false;
    }


	/// <summary>
	/// 2019: Had tab, but tabbed columns and space are different.
	/// </summary>
	/// <remarks>Might specifically need a TabSpace terminal. Or perhaps
	/// I'll add a value attrib so you can specifically add tab.</remarks>
	public class TextTermSpace : TextTermBase
	{
        public TextTermSpace( ) { } 

        public override bool IsEqual( int p_iMaxStack, DataStream<char> p_oStream, bool fLookAhead, int p_iPos, out int p_iMatch, out Production<char> p_oProd )
		{
            p_iMatch = 0;
			p_oProd  = null;
			
			while( p_oStream.InBounds( p_iPos + p_iMatch ) ) {
                if( !Char.IsWhiteSpace( p_oStream[p_iPos + p_iMatch] ) ||
                     Char.IsControl   ( p_oStream[p_iPos + p_iMatch] ) )
                {
                    return p_iMatch >= m_iMin;
                }

                ++p_iMatch;
			}
            return p_iMatch >= m_iMin;
        } 

		public override string ToString()
		{
			return( "_" );
		}
	} 

	public class TextTermTab : TextTermBase
	{
        public TextTermTab( ) { } 

        public override bool IsEqual( int p_iMaxStack, DataStream<char> p_oStream, bool fLookAhead, int p_iPos, out int p_iMatch, out Production<char> p_oProd )
		{
            p_iMatch = 0;
			p_oProd  = null;
			
			while( p_oStream.InBounds( p_iPos + p_iMatch ) && p_iMatch < _iMaxMatch ) {
				char cChar = p_oStream[ p_iPos + p_iMatch ];
				if( cChar == '\t' )
					p_iMatch++;
				else
					break;
			}

			return( p_iMatch > 0 && p_iMatch <= _iMaxMatch && p_iMatch >= _iMinMatch );
        } 

		public override string ToString()
		{
			return( "/t" );
		}
	} 
    public class TextTermCR : TextTermBase
    {
        public TextTermCR() { } 

        /// <summary>
        /// Looking for either \n OR \r OR both!
        /// </summary>
        public override bool IsEqual(int iMaxStack, DataStream<char> oStream, bool fLookAhead, int iPos, out int r_iMatch, out Production<char> r_oProd)
        {
            r_iMatch = 0;
            r_oProd  = null;

            while( oStream.InBounds(iPos + r_iMatch) ) {
                char   cValue   = oStream[iPos + r_iMatch];
                string arrSpace = "\n\r";
                bool   fMatch   = false;

                for( int i = 0; i < arrSpace.Length; ++i) {
                    if( arrSpace[i] == cValue)
                        fMatch = true;
                }
                if( !fMatch ) {
                    return r_iMatch >= m_iMin;
                }

                ++r_iMatch;
            }
            return r_iMatch >= m_iMin;
        } 

        public override string ToString()
        {
            return ("EOL");
        }
	}

    /// <summary>
    /// It's not the most efficient thing. But at least we don't look ahead
    /// unless we are at a period. So for the general case of a standard
    /// file name we'll onlly be walking to the end of the extension!
    /// </summary>
    /// <remarks>Note: since we are reading the file as a stream we need
    /// to specifically look for CR/LF or we search the entire file!
    /// Might consider stopping at white space too?</remarks>
    public class TextTermFinalPeriod : ProdElem<char> {
        public TextTermFinalPeriod() { }
        public override bool IsEqual( int iMaxStack, DataStream<char> oStream, bool p_fLookAhead, 
                                      int iPos, out int r_iMatch, out Production<char> r_oProd)
        {
            r_iMatch = 0;
            r_oProd  = null;

            if( !oStream.InBounds( iPos ) ) {
                return false;
            }
            if( oStream[iPos] != '.' )
                return false;

            var oMatchCR = new TextTermCR();
            int iTemp    = 0;

            // Look ahead and see if there is another period!!
            while( oStream.InBounds(++iPos) ) {
                if( oStream[iPos] == '.' ) {
                    return false; // Found another period.
                }
                // Limit our search... consider checking for white space too..
                if( oMatchCR.IsEqual( iMaxStack, oStream, false, iPos, out iTemp, out r_oProd ) ) {
                    break;
                }
            }

            r_iMatch = 1;

            return true;
        }
    }

    // A simple base class to collect the "value" attribute from the XML BNF.
    // value is typically something like the token we are looking for.
    public abstract class ValueTerminal : ProdElem<char> 
	{
		protected string m_strValue;

		public ValueTerminal()
		{
            m_strValue = null;
		}

        public override bool Load( XmlElement p_xmlTerminal )
        {
            if( !base.Load( p_xmlTerminal ) )
				return( false );

            m_strValue = p_xmlTerminal.GetAttribute( "value" );
            return( m_strValue.Length > 0 );
        }

		public override string ToString()
		{
			return( m_strValue.ToLower() );
		}
	}

    public class TextTermToken : ValueTerminal
	{
		public override bool IsEqual( int p_iMaxStack, DataStream<char> p_oStream, bool fLookAhead, int p_iPos, out int p_iMatch, out Production<char> p_oProd )
		{
			int j = 0;

			p_iMatch = 0;
			p_oProd  = null;

			while( p_oStream.InBounds( p_iPos + p_iMatch ) && j < m_strValue.Length )
			{
				if( p_oStream[ p_iPos + p_iMatch ] == m_strValue[j]  )
					j++;
				else
					break;
				++p_iMatch;
			}
			return( p_iMatch == m_strValue.Length );
		}
	} 

	public class TextTermAnyOf : ValueTerminal
	{
		public override bool IsEqual( int p_iMaxStack, DataStream<char> p_oStream, bool fLookAhead, int p_iPos, out int p_iMatch, out Production<char> p_oProd )
		{
			p_iMatch = 0;
			p_oProd  = null;

			while( p_oStream.InBounds( p_iPos + p_iMatch ) && p_iMatch < _iMaxMatch )
			{
				bool fFound = false;
				for( int j=0; j < m_strValue.Length; ++j ) 
				{
					if( p_oStream[ p_iPos + p_iMatch ] == m_strValue[j]  ) 
					{
						fFound = true;
						break;
					}
				}
				if( !fFound )
					break;

				++p_iMatch;
			}
			return( p_iMatch > 0 );
		}
	} 
}
