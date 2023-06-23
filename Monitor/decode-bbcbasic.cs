using System.Text;

using Play.Edit;

namespace Monitor {
    /// <summary>
    /// This code is based on 2007 Matt Godbolt's Python implementation
    /// minus all the regular expression stuff.
    /// </summary>
    public class Detokenize {
        // The list of BBC BASIC V tokens:
        // Base tokens, starting at 0x7f

        public static string[] stdTokens = {
        "OTHERWISE", /* 7f */ "AND", "DIV", "EOR", "MOD", "OR", "ERROR", "LINE", "OFF", "STEP", 
        "SPC", "TAB(", "ELSE", "THEN", "<line>" /* TODO */, "OPENIN", "PTR","PAGE", "TIME", "LOMEM", 

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
        string[] cfnTokens = { "SUM", "BEAT" };
        // Referred to as "ESCCOM" tokens in the source, starting at 0x8e.
        string[] comTokens = {
            "APPEND", "AUTO", "CRUNCH", "DELET", "EDIT", "HELP", "LIST", "LOAD",
            "LVAR", "NEW", "OLD", "RENUMBER", "SAVE", "TEXTLOAD", "TEXTSAVE", "TWIN",
            "TWINO", "INSTALL" };
        // Referred to as "ESCSTMT", starting at 0x8e.
        string[] stmTokens= {
            "CASE", "CIRCLE", "FILL", "ORIGIN", "PSET", "RECT", "SWAP", "WHILE",
            "WAIT", "MOUSE", "QUIT", "SYS", "INSTALL", "LIBRARY", "TINT", "ELLIPSE",
            "BEATS", "TEMPO", "VOICES", "VOICE", "STEREO", "OVERLAY" };

        // This rediculously obsfucated bit stream avoids line number 0x8b (139)
        // from looking like a ELSE token in the byte stream for basic!!
        // https://xania.org/200711/bbc-basic-line-number-format-part-2
        protected int DecodeLine( byte[] rgNumber ) {
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

        // Replace all tokens in the line 'line' with their ASCII equivalent.
        // Internal function used as a callback to the regular expression
        // to replace tokens with their ASCII equivalents.
        // Any character value greater or equal to 0x7f is interpreted as a token
        protected string Detokanize( Tuple<int,byte[]> oLine ) {
            StringBuilder oSB = new();

            oSB.Append( oLine.Item1.ToString() );
            oSB.Append( ' ' );

            byte[] rgData = oLine.Item2;
            for( int i=0; i< rgData.Length; ++i ) {
                if( rgData[i] < 0x75 ) {
                    oSB.Append( Convert.ToChar( rgData[i] ) );
                } else {
                    switch( rgData[i] ) {
                        case 0xC6:
                            oSB.Append( cfnTokens[rgData[++i] - 0x8E] );
                            break;
                        case 0xC7:
                            oSB.Append( comTokens[rgData[++i] - 0x8E] );
                            break;
                        case 0xC8:
                            oSB.Append( stmTokens[rgData[++i] - 0x8E] );
                            break;
                        case 0xF4: // rem
                            for( ++i; i< rgData.Length; ++i ) {
                                oSB.Append( Convert.ToChar( rgData[i] ) );
                            }
                            break;
                        case 0xe4: // Gosub
                        case 0xe5: // Goto followed by Line number reference...
                            oSB.Append( stdTokens[rgData[  i] - 0x7f] ); // keyword

                            byte[] rgNumber = new byte[3];
                            ++i;
                            for( int j=0; j<3;++j, ++i ) {
                                rgNumber[j] = rgData[i];
                            }
                            oSB.Append( DecodeLine( rgNumber ).ToString() ); // line number
                            break;
                        default:
                            oSB.Append( stdTokens[rgData[  i] - 0x7f] );
                            break;
                    }
                    
                }
            }

            return oSB.ToString();
        }

        Tuple<int, byte[]> DecodeLine( BinaryReader oReader ) {
            byte bLineLen = oReader.ReadByte();
            byte bLineHi  = oReader.ReadByte();
            byte bLineLo  = oReader.ReadByte();

            int iLine = (short)(((bLineHi) & 0xFF) << 8 | (bLineLo) & 0xFF);

            if( iLine == 0xFFFF )
                return new Tuple<int, byte[]>( iLine, new byte[0] );

            byte[] data = oReader.ReadBytes( bLineLen - 4 );

            return new Tuple<int, byte[]>( iLine, data );
        }

        // Each line is stored as a sequence of bytes:
        // 0x0d [line num hi] [line num lo] [line len] [data...]
        // BBC BASIC V format file.
        // Agon : [line len][line num lo][line num high]
        //        [ox0D][line len][line num lo][line num high]
        //        [ox0D][line len][line num lo][line num high]
        //        ...
        List< Tuple<int,byte[]>> ReadLines( BinaryReader oReader ) {
            List<Tuple<int, byte[]>> rgLines = new();
            while( true ) {
                Tuple<int, byte[]> oLine = DecodeLine( oReader );

                rgLines.Append( oLine );

                if( oLine.Item1 == 0xFFFF )
                    break;

                if( oReader.ReadByte() != 0x0D )
                    throw new InvalidProgramException( "Bad Read" );

            }
            return rgLines;
        }

        void Decode( BinaryReader oReader, List<string> rgOutput ) {
            // Decode binary data 'data' and write the result to 'output'."""
            List<Tuple<int, byte[]>> rgLines = ReadLines( oReader );

            foreach( Tuple<int,byte[]> oTuple in rgLines ) {
                string strLine = Detokanize(oTuple);

                rgOutput.Append( strLine );
            }
        }

        public void Start( string strFileName ) {
            List<string> rgOutput = new();

            using Stream       oStream = File.OpenRead( strFileName );
            using BinaryReader oReader = new BinaryReader(oStream);

            Decode( oReader, rgOutput);
        }

        public void Dump( string strFileName, Editor oEdit ) {
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
    }
}