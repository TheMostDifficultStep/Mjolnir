using System.Text;

using Play.Edit;
using Play.Parse.Impl;
using Play.Parse;
using Play.Interfaces.Embedding;
using OpenTK.Audio.OpenAL;
using System;
using System.Runtime.CompilerServices;

namespace Monitor {
    /// <summary>
    /// This code is based on 2007 Matt Godbolt's Python implementation
    /// minus all the regular expression stuff.
    /// </summary>
    public class BbcBasic5 {
        public BbcBasic5() {
            TableBuilder();
        }

        // The list of BBC BASIC V tokens:
        // Base tokens, starting at 0x7f

        public static string[] _rgTokenStd = {
        "OTHERWISE"/* 7f */, "AND", "DIV", "EOR", "MOD", "OR", "ERROR", "LINE", "OFF", "STEP", 
        "SPC", "TAB(", "ELSE", "THEN", "<line no>" /* TODO */, "OPENIN", "PTR","PAGE", "TIME", "LOMEM", 

        "HIMEM", "ABS", "ACS", "ADVAL", "ASC","ASN", "ATN", "BGET", "COS", "COUNT", 
        "DEG", "ERL", "ERR","EVAL", "EXP", "EXT", "FALSE", "FN", "GET", "INKEY",

        "INSTR(", "INT", "LEN", "LN", "LOG", "NOT", "OPENUP", "OPENOUT", "PI","POINT(", 

        "POS", "RAD", "RND", "SGN", "SIN", "SQR", "TAN","TO", "TRUE", "USR", 
        "VAL", "VPOS", "CHR$", "GET$", "INKEY$","LEFT$(", "MID$(", "RIGHT$(", "STR$", "STRING$(", 

        "EOF", "<ESCFN>", "<ESCCOM>", "<ESCSTMT>", "WHEN", "OF", "ENDCASE", "ELSE" /*ELSE2*/, "ENDIF", "ENDWHILE", 
        "PTR", "PAGE", "TIME", "LOMEM", "HIMEM", "SOUND", "BPUT", "CALL", "CHAIN", "CLEAR", 
        "CLOSE", "CLG", "CLS", "DATA", "DEF", "DIM", "DRAW", "END", "ENDPROC", "ENVELOPE", 
        "FOR", "GOSUB", "GOTO", "GCOL", "IF", "INPUT", "LET", "LOCAL", "MODE", "MOVE", 
        "NEXT", "ON", "VDU", "PLOT", "PRINT", "PROC", "READ", "REM", "REPEAT", "REPORT", 
        "RESTORE", "RETURN", "RUN", "STOP", "COLOUR", "TRACE", "UNTIL", "WIDTH", "OSCLI" };

        // Referred to as "ESCFN" tokens in the source, starting at 0x8e.
        string[] _rgTokenFnc = { "SUM", "BEAT" };
        // Referred to as "ESCCOM" tokens in the source, starting at 0x8e.
        string[] _rgTokenCom = {
            "APPEND", "AUTO", "CRUNCH", "DELETE", "EDIT", "HELP", "LIST", "LOAD",
            "LVAR", "NEW", "OLD", "RENUMBER", "SAVE", "TEXTLOAD", "TEXTSAVE", "TWIN",
            "TWINO", "INSTALL" };
        // Referred to as "ESCSTMT", starting at 0x8e.
        string[] _rgTokenStm= {
            "CASE", "CIRCLE", "FILL", "ORIGIN", "PSET", "RECT", "SWAP", "WHILE",
            "WAIT", "MOUSE", "QUIT", "SYS", "INSTALL", "LIBRARY", "TINT", "ELLIPSE",
            "BEATS", "TEMPO", "VOICES", "VOICE", "STEREO", "OVERLAY" };

        public enum TokenType {
            Std, Fnc, Com, Stm
        }
        public struct TokenInfo {
            readonly public string    _strToken;
            readonly public TokenType _eType;
            readonly public byte      _bToken;
            readonly public byte      _bExtn;

            public TokenInfo( string strToken, TokenType tokenType, byte bValue ) {
                _strToken = strToken;
                _eType    = tokenType;
                _bToken   = bValue;

                switch( _eType ) {
                    case TokenType.Std:
                        _bExtn = 0;
                        break;
                    case TokenType.Fnc:
                        _bExtn = 0xc6;
                        break;
                    case TokenType.Com:
                        _bExtn = 0xc7;
                        break;
                    case TokenType.Stm:
                        _bExtn = 0xc8;
                        break;
                    default:
                        throw new InvalidProgramException();
                }
            }

            public override string ToString() => _strToken;
        }

        List<TokenInfo> _dcTokenLookup = new ();

        protected void TableBuilder() {
            byte bIndex = 0x7f;
            foreach( string strToken in _rgTokenStd ) {
                _dcTokenLookup.Add( new TokenInfo( strToken, TokenType.Std, bIndex++ ) );
            }

            bIndex = 0x8e;
            foreach( string strToken in _rgTokenFnc ) {
                _dcTokenLookup.Add( new TokenInfo( strToken, TokenType.Fnc, bIndex++ ) );
            }
            bIndex = 0x8e;
            foreach( string strToken in _rgTokenCom ) {
                _dcTokenLookup.Add( new TokenInfo( strToken, TokenType.Com, bIndex++ ) );
            }
            bIndex = 0x8e;
            foreach( string strToken in _rgTokenStm ) {
                _dcTokenLookup.Add( new TokenInfo( strToken, TokenType.Stm, bIndex++ ) );
            }

            int CompareToken( TokenInfo x, TokenInfo y ) {
                return string.Compare( x._strToken, y._strToken, ignoreCase:true );
            }

            _dcTokenLookup.Sort( CompareToken );
        }

        List<char> _rgLookup = new();
        /// <remarks>
        /// Consider adding support for version of BBC BASIC you're using.
        /// Right now my decoder reads BBC BASIC V. for the Agon computer.
        /// </remarks>
        protected TokenInfo GetToken( ReadOnlySpan<char> spToken ) {
            // Avoid allocating a string every time, but...
            _rgLookup.Clear();
            foreach( char c in spToken ) {
                _rgLookup.Add( c );
            }

            // It's a crock, but you cannot put a Span inside of a delegate.
            // https://github.com/Microsoft/referencesource/blob/master/mscorlib/system/string.cs
            int CompareTokenCaseInsensitive( TokenInfo spTry ) {
                for( int i = 0; i<Math.Min( _rgLookup.Count, spTry._strToken.Length) ; i++ ) {
                    char c1 = _rgLookup[i];
                    char c2 = spTry._strToken[i];

                    if( (c1 | c2) > 0x7F )
                        throw new InvalidDataException( "must be ascii comparison" );

                    // uppercase both chars - notice that we need just one compare per char
                    if ((uint)(c1 - 'a') <= (uint)('z' - 'a')) 
                        c1 -= (char)0x20;
                    if ((uint)(c2 - 'a') <= (uint)('z' - 'a')) 
                        c2 -= (char)0x20;

                    int iComp2 = c1 - c2;

                    if( c1 != c2 )
                        return c1 - c2;
                }
                return _rgLookup.Count - spTry._strToken.Length;
            }

            int iIndex = FindStuff<TokenInfo>.BinarySearch( _dcTokenLookup,
                                                            0, 
                                                            _dcTokenLookup.Count, 
                                                            CompareTokenCaseInsensitive );
            if( iIndex > 0 )
                return _dcTokenLookup[iIndex];

            throw new InvalidDataException( "Couldn't find token" );
        }

        // This rediculously obsfucated bit stream avoids line number 0x8b (139)
        // from looking like a ELSE token in the byte stream for basic!!
        // https://xania.org/200711/bbc-basic-line-number-format-part-2
        static int DecodeNumber( Span<byte> rgNumber ) {
            int r0, r1, r10;  
            
            r10 = rgNumber[0];
            r0 = r10 << 2;
            r1 = r0 & 0xc0;

            r10 = rgNumber[1];
            r1 ^= r10;

            r10 = rgNumber[2];
            r0 = r10 ^ (r0 << 2);
            r0 &= 0xff;
            r0 = r1 | (r0<<8);

            return r0;
        }

        /// <remarks>
        /// https://xania.org/200711/bbc-basic-line-number-format
        /// This algorithm splits the top two bits off each of the two bytes of 
        /// the 16-bit line number. These bits are combined (in binary as 00LlHh00), 
        /// exclusive-ORred with 0x54, and stored as the first byte of the 3-byte 
        /// sequence. The remaining six bits of each byte are then stored, in LO/HI 
        /// order, ORred with 0x40.
        /// Does not include the lead 0x8D token. Add that yourself in front.
        /// </remarks>
        /// <param name="iNumber"></param>
        /// <returns></returns>
        static void EncodeNumber( int iNumber, Span<byte> rgReturn ) {
            uint uNumber = (uint)iNumber;

            ulong uTopBitsHi = uNumber & 0xc000; // 11000000 00000000
            ulong uTopBitsLo = uNumber & 0x00c0; // 00000000 11000000

            // Make a byte that looks like ... (00LLHH00)
            uTopBitsHi = uTopBitsHi >> 12; // (8 + 4)
            uTopBitsLo = uTopBitsLo >> 2;

            byte bLead = (byte)(( uTopBitsHi | uTopBitsLo ) ^ 0x54 );
            byte bLo6  = (byte)((uNumber      & 0x3f ) | 0x40 );
            byte bHi6  = (byte)((uNumber >> 8 & 0x3f ) | 0x40 );

            rgReturn[0] = bLead; // I might have these backwards...
            rgReturn[1] = bLo6;
            rgReturn[2] = bHi6;
        }

        /// <summary>
        /// Take the plain text bbc basic and tokenize it for binary file. 
        /// </summary>
        /// <remarks>
        /// http://www.benryves.com/bin/bbcbasic/manual/Appendix_Tokeniser.htm
        /// https://www.ncus.org.uk/dsbbcoms.htm
        /// </remarks>
        /// <param name="oEdit">Input text editor.</param>
        /// <param name="oWriter">Output to BBC basic binary.</param>
        /// <exception cref="ArgumentNullException" />
        /// <exception cref="InvalidProgramException" />
        /// <exception cref="InvalidDataException" />
        public bool IO_Tokenize( BasicEditor oEdit, BinaryWriter oWriter ) {
            if( oWriter == null || oEdit == null )
                throw new ArgumentNullException();

            // I could fix this but maybe later... :-/
            if( !BitConverter.IsLittleEndian )
                throw new InvalidProgramException( "This procedure assumes LittleEndian" );

            try {
                int[]                  rgMapping     = new int[100]; // Map line char pos with a token.
                List<MemoryElem<char>> rgTokens      = new List<MemoryElem<char>>();
                List<byte>             rgOutput      = new List<byte>();
                byte[]                 rgNumEncoding = new byte[3];

                foreach( Line oLine in oEdit ) {
                    // Resize array to match line length.
                    if( rgMapping.Length < oLine.ElementCount )
                        rgMapping = new int[oLine.ElementCount];
                    // Init the array. Clear the tokens for the line.
                    for( int i = 0; i < rgMapping.Length; i++ )
                        rgMapping[i] = 0;
                    rgTokens.Clear();
                    rgOutput.Clear();

                    // Get the basic line number
                    if( oLine.Extra is not Line oBasicLineNumber ) {
                        throw new InvalidDataException( "Editor does not include basic line number info." );
                    }
                    short iBasicLineNumber = short.Parse( oBasicLineNumber.AsSpan );

                    // Map shows character token position. > 0 means it's a token.
                    // BUG: tokenize numbers!!
                    foreach( IColorRange oRange in oLine.Formatting ) {
                        if( oRange is MemoryElem<char> oToken && (
                            string.Compare( oToken.ID, "keywords"  ) == 0 ||
                            string.Compare( oToken.ID, "number" ) == 0 )
                        ) { 
                            rgTokens.Add( oToken );
                            for( int i = oRange.Offset; i< oRange.Offset + oToken.Length; i++ ) {
                                rgMapping[i] = rgTokens.Count; // One greater than actual index of token.
                            }
                        } 
                    }

                    // Tokenize the line and output to the output line.
                    for( int i = 0; i < oLine.ElementCount; i++ ) {
                        if( rgMapping[i] == 0 ) {
                            if( oLine[i] > 0x7f )
                                throw new InvalidDataException( "BBC basic V ascii text error - 1." );

                            rgOutput.Add( (byte)oLine[i] );
                        } else {
                            MemoryElem<char> oRange  = rgTokens[rgMapping[i] - 1];
                            Span<char>       spToken = oLine.Slice( oRange );

                            if( string.Compare( oRange.ID, "keywords" ) == 0 ) {
                                TokenInfo sToken = GetToken( spToken );
                                if( sToken._bExtn != 0x00 )
                                    rgOutput.Add( sToken._bExtn );
                                rgOutput.Add( sToken._bToken );
                            } else {
                                if( string.Compare( oRange.ID, "number" ) == 0 ) {
                                    EncodeNumber( int.Parse( spToken ), rgNumEncoding );
                                    rgOutput.Add( 0x8d );
                                    foreach( byte bToken in rgNumEncoding ) { 
                                        rgOutput.Add( bToken );
                                    }
                                } else {
                                    throw new InvalidDataException( "BBC basic V ascii text error - 2." );
                                }
                            }
                            i += oRange.Length - 1;
                        }
                    }
                    // Line length max is 255 ascii characters. Subtract line line, basic line num.
                    if( rgOutput.Count > 0xff - 4 ) {
                        StringBuilder sbError = new StringBuilder();

                        sbError.Append( "Line length greater than 255 chars. Basic Line Number: " );
                        sbError.Append( oBasicLineNumber.AsSpan );

                        throw new InvalidDataException( sbError.ToString() );
                    }

                    byte[] rgBytes = BitConverter.GetBytes( iBasicLineNumber );

                    oWriter.Write( (byte)(rgOutput.Count + 4 ));
                    oWriter.Write( (byte)rgBytes[0] ); // Low byte first.
                    oWriter.Write( (byte)rgBytes[1] );

                    foreach( byte bChar in rgOutput )
                        oWriter.Write( bChar );

                    oWriter.Write( (byte)0x0d );
                }

               // end of program.
                oWriter.Write( (byte)0 );
                oWriter.Write( (byte)255 );
                oWriter.Write( (byte)255 );

                oWriter.Flush();
            } catch( Exception oEx ) {
                Type[] rgErrors = { typeof( IndexOutOfRangeException ),
                                    typeof( ArgumentOutOfRangeException ),
                                    typeof( InvalidDataException ),
                                    typeof( IOException ),
                                    typeof( ObjectDisposedException ),
                                    typeof( FormatException ),
                                    typeof( NullReferenceException ) };
                if( rgErrors.IsUnhandled( oEx ) )
                    throw;
            }

            return true;
        }

        /// <summary>
        /// Replace all tokens in the line 'line' with their ASCII equivalent.
        /// Internal function used as a callback to the regular expression
        /// to replace tokens with their ASCII equivalents.
        /// </summary>
        /// <remarks>
        /// Any character value greater or equal to 0x7f is interpreted as a token.
        /// Note that 0X7F is ascii DEL key which is ignored and used by 
        /// bbc basic V as the OTHERWISE token.
        /// The normal BBC token space starts at 0x80
        /// </remarks>
        protected string ReadTokens( byte[] rgData ) {
            StringBuilder oSB = new();

            for( int i=0; i< rgData.Length; ++i ) {
                if( rgData[i] < 0x7F ) {
                    oSB.Append( Convert.ToChar( rgData[i] ) );
                } else {
                    switch( rgData[i] ) {
                        case 0xC6:
                            oSB.Append( _rgTokenFnc[rgData[++i] - 0x8E] );
                            break;
                        case 0xC7:
                            oSB.Append( _rgTokenCom[rgData[++i] - 0x8E] );
                            break;
                        case 0xC8:
                            oSB.Append( _rgTokenStm[rgData[++i] - 0x8E] );
                            break;
                        case 0xF4: // rem
                            for( ++i; i< rgData.Length; ++i ) {
                                oSB.Append( Convert.ToChar( rgData[i] ) );
                            }
                            break;
                        case 0xe4: // Gosub
                        case 0xe5: // Goto followed by Line number reference...
                            oSB.Append( _rgTokenStd[rgData[  i] - 0x7f] ); // keyword

                            while( rgData[++i] == 0x20 ) {
                                oSB.Append( ' ' );
                            }
                            if( rgData[i] == 0x8d ) {
                                ++i;
                                byte[] rgNumber = new byte[3];
                                for( int j=0; j<3;++j, ++i ) {
                                    rgNumber[j] = rgData[i];
                                }
                                string strGotoLine = DecodeNumber( rgNumber ).ToString();
                                oSB.Append( strGotoLine ); // line number
                            }
                            break;
                        default:
                            oSB.Append( _rgTokenStd[rgData[  i] - 0x7f] );
                            break;
                    }
                    
                }
            }

            return oSB.ToString();
        }

        /// <summary>
        /// Decode a single line from the binary file. Returns
        /// a tuple which is the bbc basic line number followed by
        /// the binary stream for the line.
        /// </summary>
        /// <param name="oReader">The binary stream</param>
        /// <returns>A tuple containing the bbc basic line number followed
        /// by the binary stream.</returns>
        Tuple<int, byte[]> GetLine( BinaryReader oReader ) {
            byte bLineLen = oReader.ReadByte();
            byte bLineLo  = oReader.ReadByte();
            byte bLineHi  = oReader.ReadByte();

            // This is the BBC basic line number in binary.
            UInt16 iLineNumber = (UInt16)(((bLineHi) & 0xFF) << 8 | (bLineLo) & 0xFF);

            if( iLineNumber == 0xFFFF )
                return new Tuple<int, byte[]>( iLineNumber, new byte[0] );

            byte[] data = oReader.ReadBytes( bLineLen - 4 );

            return new Tuple<int, byte[]>( iLineNumber, data );
        }

        /// <summary>
        /// Each line is stored as a sequence of bytes:
        /// 0x0d [line num hi] [line num lo] [line len] [data...]
        /// BBC BASIC V format file.
        /// Agon : [line len][line num lo][line num high][data...][ox0D]
        ///        [line len][line num lo][line num high][data...][ox0D]
        ///        ...
        /// </summary>
        /// <exception cref="InvalidProgramException"></exception>
        /// <param name="oReader">Input binary stream.</param>
        /// <param name="oEdit">Output to BBC basic text file editor.</param>
        void IO_Detokanize( BinaryReader oReader, BasicEditor oEdit ) {
            if( oReader == null || oEdit == null ) 
                throw new ArgumentNullException();

            try {
                using BasicEditor.BasicManipulator oBulk = new ( oEdit );

                while( true ) {
                    byte bLineLen = oReader.ReadByte();
                    byte bLineLo  = oReader.ReadByte();
                    byte bLineHi  = oReader.ReadByte();

                    // This is the BBC basic line number in binary.
                    UInt16 iLineNumber = (UInt16)(((bLineHi) & 0xFF) << 8 | (bLineLo) & 0xFF);

                    if( iLineNumber == 0xFFFF )
                        break;

                    byte[] data    = oReader.ReadBytes( bLineLen - 4 );
                    string strLine = ReadTokens(data);

                    oBulk.Append( iLineNumber, strLine );

                    if( oReader.ReadByte() != 0x0d )
                        throw new InvalidDataException( "Expected linefeed" );
                }
            } catch( Exception oEx ) {
                Type[] rgErrors = { typeof( EndOfStreamException ),
                                    typeof( InvalidProgramException ),
                                    typeof( InvalidDataException ),
                                    typeof( IOException ),
                                    typeof( ObjectDisposedException ),
                                    typeof( IndexOutOfRangeException ) };
                if( rgErrors.IsUnhandled( oEx ) )
                    throw;

                oEdit.LogError( "Bad program format. Is it really a binary BBC Basic file?" );
            }
        }
        
        public static void ToLower( string[] rgStrings ) {
            for( int i=0; i<rgStrings.Length; i++ ) {
                rgStrings[i] = rgStrings[i].ToLower();
            }
        }

        public void Start( string strFileName, BasicEditor oEdit ) {
            List<string> rgOutput = new();

            // Sort out lower/upper later.
            ToLower( _rgTokenStd );
            ToLower( _rgTokenFnc );
            ToLower( _rgTokenCom );
            ToLower( _rgTokenStm );

            using Stream       oStream = File.OpenRead( strFileName );
            using BinaryReader oReader = new BinaryReader(oStream);

            oEdit.Clear();

            IO_Detokanize( oReader, oEdit);
        }

        /// <summary>
        /// Simple byte dump of file contents using binary reader.
        /// </summary>
        /// <param name="strFileName">Source file</param>
        /// <param name="oEdit">Target editor.</param>
        public static void Dump( string strFileName, BaseEditor oEdit ) {
            using Stream       oStream = new FileStream  ( strFileName, FileMode.Open );
            using BinaryReader oReader = new BinaryReader( oStream,     Encoding.ASCII);

            oEdit.Clear();

            byte bByte;
            try {
                Line oLine = oEdit.LineAppend( String.Empty );
                int iIndex = 0;
                while( true ) {
                    bByte = oReader.ReadByte();
                    if( bByte == 0x0D ) {
                        iIndex = 0;
                        oLine = oEdit.LineAppend( "0D " );
                        oLine.Formatting.Add( new ColorRange( 0, 2, 1 ) );
                    } else {
                        if( bByte < 0x7f && bByte > 0x20 ) {
                            oLine.TryAppend( Convert.ToChar( bByte ).ToString() );
                        } else {
                            oLine.TryAppend( bByte.ToString( "X2" ) );
                            oLine.TryAppend( "h" );
                        }
                        oLine.TryAppend( " " );
                    }
                }
            } catch( EndOfStreamException ) {
            }
        }
        public void Test( IPgBaseSite oSite ) {
            Span<byte> rgResult = stackalloc byte[3];
            
            for( int i=0; i< 0xffff; ++i ) {
                EncodeNumber( i, rgResult );

                int iTest = DecodeNumber( rgResult );

                if( iTest != i )
                    oSite.LogError( "test", "BBC Basic Number encode/decode error" );
            }

            TokenInfo sToken = GetToken( "THEN" );
            
            if( sToken._bToken != 0x8c )
                oSite.LogError( "test", "Get token failure" );

            sToken = GetToken( "Sound" );
            if( sToken._bToken != 0xd4 )
                oSite.LogError( "test", "Get token failure" );

            sToken = GetToken( "Delete" );
            if( sToken._bExtn != 0xc7 || sToken._bToken != 0x91 )
                oSite.LogError( "test", "Get token failure" );
        }
    }

}