using System.Text;

using Play.Edit;
using Play.Parse.Impl;
using Play.Parse;
using Play.Interfaces.Embedding;
using OpenTK.Audio.OpenAL;
using System;

namespace Monitor {
    /// <summary>
    /// This code is based on 2007 Matt Godbolt's Python implementation
    /// minus all the regular expression stuff.
    /// </summary>
    public class Detokenize {
        public Detokenize() {
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

        enum TokenType {
            Std, Fnc, Com, Stm
        }
        struct TokenInfo {
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
        protected byte GetToken( ReadOnlySpan<char> spToken ) {
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
                    if ((uint)(c1 - 'a') <= (uint)('z' - 'a')) 
                        c1 -= (char)0x20;

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
                return _dcTokenLookup[iIndex]._bToken;

            throw new InvalidDataException( "Couldn't find token" );
        }

        // This rediculously obsfucated bit stream avoids line number 0x8b (139)
        // from looking like a ELSE token in the byte stream for basic!!
        // https://xania.org/200711/bbc-basic-line-number-format-part-2
        public int DecodeNumber( byte[] rgNumber ) {
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
        /// The algorithm used splits the top two bits off each of the two bytes of 
        /// the 16-bit line number. These bits are combined (in binary as 00LlHh00), 
        /// exclusive-ORred with 0x54, and stored as the first byte of the 3-byte 
        /// sequence. The remaining six bits of each byte are then stored, in LO/HI 
        /// order, ORred with 0x40.
        /// Does not include the lead 0x8D token. Add that yourself in front.
        /// </remarks>
        /// <param name="iNumber"></param>
        /// <returns></returns>
        public byte[] EncodeNumber( int iNumber ) {
            byte[] rgReturn = new byte[3];
            uint uNumber = (uint)iNumber;

            try {
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

            } catch( FormatException ) {
            }

            return rgReturn;
        }

        /// <summary>
        /// Take the plain text bbc basic and tokenize it for binary file. 
        /// </summary>
        /// <remarks>
        /// http://www.benryves.com/bin/bbcbasic/manual/Appendix_Tokeniser.htm
        /// https://www.ncus.org.uk/dsbbcoms.htm
        /// </remarks>
        protected bool Tokanize( BasicEditor oEdit, BinaryWriter oWriter ) {
            if( oWriter == null )
                throw new ArgumentNullException();

            // I could fix this but maybe later... :-/
            if( !BitConverter.IsLittleEndian )
                throw new InvalidProgramException( "This procedure assumes LittleEndian" );

            try {
                int[]                  rgMapping = new int[100]; // Map line char pos with a token.
                List<MemoryElem<char>> rgTokens  = new List<MemoryElem<char>>();
                List<byte>             rgOutput  = new List<byte>();
                bool                   fNext     = false;

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
                            string.Compare( oToken.ID, "keyword"  ) == 0 ||
                            string.Compare( oToken.ID, "number" ) == 0 )
                        ) { 
                            rgTokens.Add( oToken );
                            for( int i = oRange.Offset; i< oToken.Length; i++ ) {
                                rgMapping[i] = rgTokens.Count; // One greater than actual index of token.
                            }
                        } 
                    }

                    // Tokenize the line and output to the output line.
                    for( int i = 0; i < oLine.ElementCount; i++ ) {
                        if( rgMapping[i] == 0 ) {
                            if( oLine[i] > 0x7f )
                                throw new InvalidDataException( "BBC basic V ascii text error." );

                            rgOutput.Add( (byte)oLine[i] );
                        } else {
                            MemoryElem<char> oRange = rgTokens[rgMapping[i] - 1];
                            Span<char> spToken = oLine.Slice( oRange );

                            if( string.Compare( oRange.ID, "keyword" ) == 0 ) {
                                byte bToken = GetToken( spToken );
                                rgOutput.Add( bToken );
                            } else {
                                byte[] rgNumEncoding = EncodeNumber( int.Parse( spToken ) );
                                foreach( byte bToken in rgNumEncoding ) { 
                                    rgOutput.Add( bToken );
                                }
                            }
                            i += oRange.Length;

                        }
                    }
                    // Line length max is 255 ascii characters.
                    if( rgOutput.Count > 0xff ) {
                        StringBuilder sbError = new StringBuilder();

                        sbError.Append( "Line length greater than 255 chars. Basic Line Number: " );
                        sbError.Append( oBasicLineNumber.AsSpan );

                        throw new InvalidDataException( sbError.ToString() );
                    }

                    byte[] rgBytes = BitConverter.GetBytes( iBasicLineNumber );

                    // Only put line feed on the line that is AFTER the first line.
                    if( fNext )
                        oWriter.Write( 0x0d );

                    oWriter.Write( (byte)rgOutput.Count );
                    oWriter.Write( rgBytes[0] ); // Low byte first.
                    oWriter.Write( rgBytes[1] );

                    foreach( byte bChar in rgOutput )
                        oWriter.Write( bChar );

                    fNext = true;
                }

                // end of program.
                oWriter.Write( 0 );
                oWriter.Write( 255 );
                oWriter.Write( 255 );
            } catch( Exception oEx ) {
                Type[] rgErrors = { typeof( IndexOutOfRangeException ),
                                    typeof( ArgumentOutOfRangeException ),
                                    typeof( InvalidDataException ) };
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
        /// The normal BBC token space which starts at 0x80
        /// </remarks>
        protected string Detokanize( Tuple<int,byte[]> oLine ) {
            StringBuilder oSB = new();

            //oSB.Append( oLine.Item1.ToString() );
            //oSB.Append( ' ' );

            byte[] rgData = oLine.Item2;
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

        Tuple<int, byte[]> DecodeLine( BinaryReader oReader ) {
            byte bLineLen = oReader.ReadByte();
            byte bLineLo  = oReader.ReadByte();
            byte bLineHi  = oReader.ReadByte();

            UInt16 iLine = (UInt16)(((bLineHi) & 0xFF) << 8 | (bLineLo) & 0xFF);

            if( iLine == 0xFFFF )
                return new Tuple<int, byte[]>( iLine, new byte[0] );

            byte[] data = oReader.ReadBytes( bLineLen - 4 );

            return new Tuple<int, byte[]>( iLine, data );
        }

        // Each line is stored as a sequence of bytes:
        // 0x0d [line num hi] [line num lo] [line len] [data...]
        // BBC BASIC V format file.
        // Agon : [line len][line num lo][line num high][data...]
        //        [ox0D][line len][line num lo][line num high][data...]
        //        [ox0D][line len][line num lo][line num high][data...]
        //        ...
        List< Tuple<int,byte[]>> ReadLines( BinaryReader oReader ) {
            List<Tuple<int, byte[]>> rgLines = new();
            while( true ) {
                Tuple<int, byte[]> oLine = DecodeLine( oReader );

                rgLines.Add( oLine );

                if( oLine.Item1 == 0xFFFF )
                    break;

                if( oReader.ReadByte() != 0x0D )
                    throw new InvalidProgramException( "Bad Read" );

            }
            return rgLines;
        }

        void Decode( BinaryReader oReader, BasicEditor oEdit ) {
            // Decode binary data 'data' and write the result to 'output'.
            try {
                List<Tuple<int, byte[]>> rgLines = ReadLines( oReader );
                using BasicEditor.BasicManipulator oBulk = new ( oEdit );

                foreach( Tuple<int,byte[]> oTuple in rgLines ) {
                    string strLine = Detokanize(oTuple);

                    if( oTuple.Item1 != 0xffff )
                        oBulk.Append( oTuple.Item1, strLine );
                }
            } catch( InvalidProgramException ) {
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

            Decode( oReader, oEdit);
        }

        /// <summary>
        /// Simple byte dump of file contents using binary reader.
        /// </summary>
        /// <param name="strFileName">Source file</param>
        /// <param name="oEdit">Target editor.</param>
        public static void Dump( string strFileName, Editor oEdit ) {
            using Stream oStream = new FileStream( strFileName,FileMode.Open );
            using BinaryReader oReader = new BinaryReader(oStream,Encoding.ASCII);

            oEdit.Clear();

            byte bByte;
            try {
                Line oLine = oEdit.LineAppend( String.Empty );
                while( true ) {
                    bByte = oReader.ReadByte();
                    if( bByte == 0x0D ) {
                        oLine = oEdit.LineAppend( "0D " );
                    } else {
                        if( bByte < 0x7f ) {
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
            byte[] rgResult = EncodeNumber( 139 );

            int iTest = DecodeNumber( rgResult );

            if( iTest != 139 )
                oSite.LogError( "test", "BBC Basic Number encode/decode error" );

            byte bToken = GetToken( "THEN" );
            
        }
    }

    public class EnTokenize {
        List<byte> _rgLineOutput = new();

        public EnTokenize() { 
        }

        public byte Convert( Line oLine ) {
            _rgLineOutput.Clear();

            return 0;
        }

        public bool Start( BasicEditor oEdit, string strFileName ) {
            if( oEdit == null )
                throw new ArgumentNullException() ;

            using Stream oStream = new FileStream( strFileName,FileMode.CreateNew );
            using BinaryWriter oWriter = new BinaryWriter(oStream,Encoding.ASCII);

            foreach( Line oLine in oEdit ) {
                if( oLine.Extra is not Line oNumber )
                    return false;

                if( !int.TryParse( oNumber.AsSpan, out int iLine ) )
                    return false;

                if( iLine > Math.Pow( 2, 16 ) )
                    return false;

                byte[] bytes = BitConverter.GetBytes( (short)iLine );

                byte bLength = Convert( oLine );

                oWriter.Write( bLength );
                oWriter.Write( bytes[0] ); // Lo line num
                oWriter.Write( bytes[1] ); // Hi line num
            }

            return true;
        }

    }
}