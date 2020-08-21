using System;
using System.Collections.Generic;
using System.IO;

using Play.Interfaces.Embedding;

namespace Play.Parse
{
    /// <summary>
    /// A simple little stream on a string class. Just for testing and fun.
    /// </summary>
	public class TestCharStream : DataStream<char> 
	{
		string m_strData;
		int    m_iPos = 0;

		public TestCharStream( string p_strData )
		{
			m_strData = p_strData;
		}

		public void Seek( int p_iDistance ) 
		{
			Position = m_iPos + p_iDistance;
		}

		public override bool InBounds( int p_iPos )
		{
			return( p_iPos < m_strData.Length );
		}

		public override int Position 
		{
			get 
			{
				return( m_iPos );
			}
			set
			{
				if( value < 0 )
					throw new ArgumentOutOfRangeException();
				if( value >= m_strData.Length )
					throw new ArgumentOutOfRangeException();

				m_iPos = value;
			}
		}

		public override char this [int iPos ] 
		{
			get 
			{
				m_iPos = iPos;
				return( m_strData[iPos] );
			}
		}

		public override string SubString( int iPos, int iLen ) 
		{
			m_iPos = iPos;
			return( m_strData.Substring( iPos, iLen ) );
		}
	}

    /// <summary>
    /// Another experimental object to test my line mapping algorithm.
    /// Simply break the line at every 20 characters.
    /// </summary>
    public class TestLineStream : DataStream<char> 
	{
		List<string> m_rgLines = new List<string>();
		int   [] m_rgStart = new int[100];

		int m_iLen = 0;
		int m_iPos = 0;
		int m_iLine = 0;
		int m_iOffs = 0;

		public TestLineStream( string p_strData )
		{
			int i = 0;
			int j = 0;

			m_iLen = p_strData.Length;

			while( i< m_iLen ) 
			{
				int iSub = m_iLen - i;

				if( iSub > 20 )
					iSub = 20;

				m_rgLines[j] = p_strData.Substring( i, iSub );
				m_rgStart[j] = i;
				i+=20;
				j+=1;
			}
		}

		public override bool InBounds( int p_iPos )
		{
			return( p_iPos < m_iLen );
		}

		public override int Position 
		{
			get 
			{
				return( m_iPos );
			}
			set
			{
			}
		}

		public void Reset( int p_iPos )
		{
			int l_iLine = m_iLine;
			int l_iOffs = m_iOffs;

			l_iOffs = p_iPos - m_rgStart[l_iLine];
			while( l_iOffs >= m_rgLines[ l_iLine ].Length )
			{
				l_iLine++;
                // OOops! This should be a count and not the length. There may be roome for 100
                // elements but only 80 are set!
				if( l_iLine >= m_rgLines.Count ) 
					throw new ArgumentOutOfRangeException();
				if( l_iLine < 0 )
					throw new ArgumentOutOfRangeException();
				l_iOffs = p_iPos - m_rgStart[l_iLine];
			}

			while( l_iOffs < 0 ) 
			{
				l_iLine--;
                if (l_iLine >= m_rgLines.Count)
					throw new ArgumentOutOfRangeException();
				if( l_iLine < 0 )
					throw new ArgumentOutOfRangeException();
				l_iOffs = p_iPos - m_rgStart[l_iLine];
			}

			m_iLine = l_iLine;
			m_iOffs = l_iOffs;
			m_iPos  = p_iPos;
		}

		public override char this [int iPos ] 
		{
			get 
			{
				Reset( iPos );
				return( m_rgLines[m_iLine][m_iOffs] );
			}
		}
		
		public List<string> Lines
		{
		    get {
		        return( m_rgLines );
		    }
		}

        /// <remarks>Yes we are possibly concatinating strings but it's not likely to happen often. Most
        ///          substrings are very short length.</remarks>
		public override string SubString( int iPos, int iLen ) 
		{
			string strReturn = string.Empty;

			while( iLen > 0 ) 
			{
				int iSub = iLen;

				Reset( iPos );

				if( m_iOffs + iSub >= m_rgLines[m_iLine].Length )
					iSub = m_rgLines[m_iLine].Length - m_iOffs;

				strReturn += m_rgLines[m_iLine].Substring( m_iOffs, iSub );
				iLen -= iSub;
				iPos += iSub;
			}

			return( strReturn );
		}
	}

    /// <summary>
    /// Thinking of making a stream reader on a file stream so
    /// I can parse the file directly instead of using the library
    /// But then I need to handle millions of encodings and 
    /// the framework already does that. I probably won't finish this.
    /// </summary>
    public class FileStreamReader : DataStream<char> {
        int          m_iPos = 0;
        StreamReader m_oReader;

        public FileStreamReader( String strFileName ) {
            m_oReader = new StreamReader( strFileName );
        }

        public void Seek(int p_iDistance) {
        }

        public override bool InBounds(int p_iPos) {
            return (false);
        }

        public override int Position {
            get {
                return (m_iPos);
            }
            set {
                if (value < 0)
                    throw new ArgumentOutOfRangeException();
                if (value >= 0)
                    throw new ArgumentOutOfRangeException();

                m_iPos = value;
            }
        }

        public override char this[int iPos] {
            get {
                return ( 's' );
            }
        }

        public override string SubString(int iPos, int iLen) {
            return ( string.Empty );
        }
    }

}
