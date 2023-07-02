using System;
using System.Text;
using System.Collections.Generic;

using Play.Interfaces.Embedding;

namespace Play.Edit {
    public partial class BaseEditor {
        /// <summary>
        /// Provide stream access to the line editor object!!! Kewlness factor very high!
        /// TODO: I think I want a new abstract class that provides the SeekLine behavior
        /// coupled with an abstract line implementation. I probably just need:
        /// Formatting, At, and ToString methods and the editor could potentially hold
        /// anything in the read only case!
        /// </summary>
        /// <remarks>This class along with the Manipulator bein </remarks>
        public class LineStream : DataStream<char> {
            StringBuilder m_sbSubString = new StringBuilder();
            IList<Line>   m_rgLines;
            BaseEditor    m_oEditor;

            int  m_iPos    = -1;
            int  m_iLine   = 0;
            int  m_iOffs   = 0;
            char m_cCached = '\0';

            public LineStream( BaseEditor oEdit ) {
                m_oEditor = oEdit ?? throw new ArgumentNullException( "Editor must not be null" );
                m_rgLines = oEdit._rgLines ?? throw new ArgumentNullException( "Editor lines array must not be null" );

                if( m_rgLines.Count > 0 && m_rgLines[0].ElementCount > 0 )
                    m_cCached = m_rgLines[0][0];
            }

            public override bool InBounds(int p_iPos) {
                return (p_iPos < m_oEditor.Size);
            }

            public override int Position {
                get {
                    return (m_iPos);
                }
                set {
                    Seek( value );
                }
            }

            /// <summary>
            /// Seek our position pointer to be at the given position from the start of the
            /// stream.
            /// </summary>
            /// <remarks>I've noticed we re-seek the same position many many times.
            /// So I've optimized the call to return quickly if the next seek is the
            /// same as the last seek!</remarks>
            /// <param name="p_iPos">Stream position from start.</param>
            public bool Seek(int p_iPos) {
                if( p_iPos == m_iPos ) // Most seeks are to the same position!! Optimize that!
                    return( true );

                // If lines get deleted since our last search we might be out of bounds.
                int l_iLine = m_iLine < m_rgLines.Count ? m_iLine : 0;

                try {
                    int l_iOffs = p_iPos - m_rgLines[l_iLine].CumulativeLength;

                    while (l_iOffs >= m_rgLines[l_iLine].ElementCount + 1) {
                        l_iLine++;
                        if (l_iLine >= m_rgLines.Count)
                            return( false );
                        if (l_iLine < 0)
                            return( false );
                        l_iOffs = p_iPos - m_rgLines[l_iLine].CumulativeLength;
                    }

                    while (l_iOffs < 0) {
                        l_iLine--;
                        if (l_iLine >= m_rgLines.Count)
                            return( false );
                        if (l_iLine < 0)
                            return( false );
                        l_iOffs = p_iPos - m_rgLines[l_iLine].CumulativeLength;
                    }

                    m_iLine   = l_iLine;
                    m_iOffs   = l_iOffs;
                    m_iPos    = p_iPos;
                    m_cCached = m_rgLines[m_iLine][m_iOffs];
                } catch( ArgumentOutOfRangeException ) {
                    // Empty files the parser might seek and it's ok to fail quietly in that case.
                    if( m_rgLines.Count < 1 ) {
                        return( false );
                    }
                    if( m_oEditor.Site != null ) {
                        m_oEditor.Site.LogError( "editor", "Problem seeking within the document." );
                    }
                    return( false );
                }

                return( true );
            }

            /// <summary>
            /// This function will work most efficiently if you seek very
            /// near your last position. Random seeking will be more expensive.
            /// </summary>
            /// <param name="iPos">The position to retrieve.</param>
            /// <returns>Character at this position.</returns>
            public override char this[int iPos] {
                get {
                    if( Seek( iPos ) ) {
                        return ( m_cCached );
                    } else {
                        return ( '\0' );
                    }
                }
            }

            /// <summary>
            /// Return a substring of the desired length.
            /// This function will work most efficiently if you seek very
            /// near your last position. Random seeking will be more expensive.
            /// This is not used by the parser and could be on a different interface.
            /// </summary>
            /// <param name="iPos">Starting position.</param>
            /// <param name="iLen">Length, may span multiple lines.</param>
            /// <returns></returns>
            public override string SubString(int iPos, int iLen) {
                m_sbSubString.Length = 0;

                while (iLen > 0) {
                    int iSubLen = iLen;

                    if( !Seek(iPos) )
                        break;

                    try {
                        // Clip if the substring is longer than one line of text.
                        // We'll pick up the rest of the string on the next pass.
                        if (m_iOffs + iSubLen >= m_rgLines[m_iLine].ElementCount)
                            iSubLen = m_rgLines[m_iLine].ElementCount - m_iOffs;

                        if( iSubLen > 0 ) {
                            m_sbSubString.Append( m_rgLines[m_iLine].SubString(m_iOffs, iSubLen) );
                            iLen -= iSubLen;
                            iPos += iSubLen;
                        } else {
                            // While we insert the EOL as a string, it only counts as one character in the line stream.
                            m_sbSubString.Append( m_oEditor.EOL );
                            ++iPos;
                            --iLen;
                        }
                    } catch( ArgumentOutOfRangeException ) {
                        if( m_oEditor.Site != null ) {
                            m_oEditor.Site.LogError( "editor", "Out of bounds when trying to build a substring." );
                        }
                        // Sometimes our math is wrong and we blow out. It's usually because 
                        // the user is typeing while we are parsing it'll clear up later. 
                        // We need to capture buffer events in the stream object.
                    }
                }

                return ( m_sbSubString.ToString() );
            }
        
            /// <summary>
            /// Get closest line at the given position. In the future I want to return an
            /// interface supporting. Formatting, At, ToString
            /// </summary>
            /// <param name="p_iPos"></param>
            /// <param name="p_iOffset"></param>
            /// <returns>Always returns a line even if it's a dummy</returns>
            public virtual Line SeekLine( int p_iPos, out int p_iOffset )
            {
                return( m_oEditor.GetLine( SeekIndex( p_iPos, out p_iOffset ) ) );
            }

            public virtual Line SeekLine( int p_iPos )
            {
                int iOffset;
                return( m_oEditor.GetLine( SeekIndex( p_iPos, out iOffset ) ) );
            }

            /// <summary>
            /// Seek closest line at given position. Internal position is modified
            /// </summary>
            /// <param name="iStreamOffset">Stream offset</param>
            /// <param name="p_iOffset">Corresponding Line offset</param>
            /// <returns></returns>
            public virtual int SeekIndex( int iStreamOffset, out int p_iOffset )
            {
                if( Seek( iStreamOffset ) ) {       
                    p_iOffset = m_iOffs;
                    return (m_iLine);
                }
                p_iOffset = 0;
                return( 0 );
            }
        }
    }
}
