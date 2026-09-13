using Play.Drawing;
using Play.Edit; 
using Play.Forms;
using Play.Interfaces.Embedding;
using Play.Parse;
using SkiaSharp;
using System.Globalization;
using System.Reflection;
using System.Security;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using z80;
using static Monitor.Z80Dissambler;

namespace Monitor {

    public enum Z80Types {
        Instruction,
        Data
    }

    public enum JumpType {
        None,
        Abs,
        Rel
    }
    public class Z80Instr {
        byte _bExt = 0;

        public string     Name { get; }
        public string     Params { get; }
        public byte       Instr { get; set; }
        public int        Length { get; set; }
        public Z80Types   Z80Type { get; set; }
        public JumpType   Jump { get; set; }
        public bool       IsCall { get; set; }     
        public IMemoryRange? NumberLocation { get; set; }
        public byte       InstrExt { 
                              get { return _bExt; } 
                              set { _bExt = value; Length = 2; }
                          }

        public Z80Instr( string strName, string? strParams = null ) {
            Instr   = 0;
            Name    = strName   ?? throw new ArgumentNullException();
            Params  = strParams ?? string.Empty;
            Length  = 1;
            Z80Type = Z80Types.Instruction;
            Jump    = JumpType.None;
            IsCall  = false;
            NumberLocation  = null;
        }
        public Z80Instr( byte bData ) {
            Instr   = bData;
            Name    = string.Empty;
            Params  = string.Empty;
            Length  = 1;
            Z80Type = Z80Types.Data;
            Jump    = JumpType.None;
            IsCall  = false;
            NumberLocation  = null;
        }

        public override string ToString() {
            return Name + " : " + Length.ToString();
        }
    }

    public class Z80Definitions {
        Z80Instr[] _rgMain = new Z80Instr[256];
        Z80Instr[] _rgMisc = new Z80Instr[256];  // ED
        Z80Instr[] _rgBitI = new Z80Instr[256];  // CB
        Z80Instr[] _rgExDD = new Z80Instr[256];  
        Z80Instr[] _rgExFD = new Z80Instr[256];  

        /// <exception cref="ArgumentException" />
        /// <exception cref="ArgumentNullException" />
        /// <exception cref="ArgumentOutOfRangeException" />
        /// <exception cref="InvalidDataException" />
        public Z80Definitions() { 
            Add( "00", "Nop" );
            Add( "01", "ld", "bc {nn}" );
            Add( "02", "ld", "(bc), a" );
            Add( "03", "inc", "bc" );
            Add( "04", "inc", "b" );
            Add( "05", "dec", "b" );
            Add( "06", "ld", "b, {n}" );
            Add( "07", "rlca" );
            Add( "08", "ex", "af, af" );
            Add( "09", "add", "hl, bc" );
            Add( "0a", "ld", "a, (bc)" );
            Add( "0b", "dec", "bc" );
            Add( "0c", "inc", "c" );
            Add( "0d", "dec", "c" );
            Add( "0e", "ld", "c, {n}" );
            Add( "0f", "rrca" );

            Add( "10", "djnz", "{d}"); // jump to n + pc
            Add( "11", "ld", "de, {nn}");
            Add( "12", "ld", "(de), a");
            Add( "13", "inc", "de");
            Add( "14", "inc", "d");
            Add( "15", "dec", "d");
            Add( "16", "ld", "d, {n}");
            Add( "17", "rla");
            Add( "18", "jr", "{d}" );
            Add( "19", "add", "hl, de");
            Add( "1a", "ld", "a, (de)");
            Add( "1b", "dec", "de");
            Add( "1c", "inc", "e");
            Add( "1d", "dec", "e");
            Add( "1e", "ld", "e, {n}");
            Add( "1f", "rra");

            Add( "20", "jr", "nz, d");
            Add( "21", "ld", "hl, {nn}");
            Add( "22", "ld", "({nn}), hl");
            Add( "23", "inc", "hl");
            Add( "24", "inc", "h");
            Add( "25", "dec", "h");
            Add( "26", "ld", "h, {n}");
            Add( "27", "daa");
            Add( "28", "jr", "z, {d}");
            Add( "29", "add", "hl, hl");
            Add( "2a", "ld", "hl, ({nn})");
            Add( "2b", "dec", "hl");
            Add( "2c", "inc", "l");
            Add( "2d", "dec", "l");
            Add( "2e", "ld", "l, {n}");
            Add( "2f", "cpl");

            Add( "30", "jr", "nc, {d}" );
            Add( "31", "ld", "sp, {nn}" );
            Add( "32", "ld", "({nn}), a");
            Add( "33", "inc", "sp" );
            Add( "34", "inc", "(hl)" );
            Add( "35", "dec", "(hl)" );
            Add( "36", "ld", "(hl), {n}" );
            Add( "37", "scf" ); // set c flag
            Add( "38", "jr", "c, {d}");
            Add( "39", "add", "hl, sp" );
            Add( "3a", "ld", "a, ({nn})" );
            Add( "3b", "dec", "sp" );
            Add( "3c", "inc", "a" );
            Add( "3d", "dec", "a" );
            Add( "3e", "ld", "a, {n}" );
            Add( "3f", "ccf" ); // invert carry flag

            Add( "40", "ld", "b, b" );
            Add( "41", "ld", "b, c" );
            Add( "42", "ld", "b, d" );
            Add( "43", "ld", "b, e" );
            Add( "44", "ld", "b, h" );
            Add( "45", "ld", "b, l" );
            Add( "46", "ld", "b, (hl)" );
            Add( "47", "ld", "b, a" );
            Add( "48", "ld", "c, b" );
            Add( "49", "ld", "c, c" );
            Add( "4a", "ld", "c, d" );
            Add( "4b", "ld", "c, e" );
            Add( "4c", "ld", "c, h" );
            Add( "4d", "ld", "c, l" );
            Add( "4e", "ld", "c, (hl)" );
            Add( "4f", "ld", "c, a" );

            Add( "50", "ld", "d, b");
            Add( "51", "ld", "d, c");
            Add( "52", "ld", "d, d");
            Add( "53", "ld", "d, e");
            Add( "54", "ld", "d, h");
            Add( "55", "ld", "d, l");
            Add( "56", "ld", "d, (hl)");
            Add( "57", "ld", "d, a" );
            Add( "58", "ld", "e, b");
            Add( "59", "ld", "e, c");
            Add( "5a", "ld", "e, d");
            Add( "5b", "ld", "e, e");
            Add( "5c", "ld", "e, h");
            Add( "5d", "ld", "e, l");
            Add( "5e", "ld", "e, (hl)");
            Add( "5f", "ld", "e, a");

            Add( "60", "ld", "h, b");
            Add( "61", "ld", "h, c");
            Add( "62", "ld", "h, d");
            Add( "63", "ld", "h, e");
            Add( "64", "ld", "h, h");
            Add( "65", "ld", "h, l");
            Add( "66", "ld", "h, (hl)");
            Add( "67", "ld", "h, a");
            Add( "68", "ld", "l, b");
            Add( "69", "ld", "l, c");
            Add( "6a", "ld", "l, d");
            Add( "6b", "ld", "l, e");
            Add( "6c", "ld", "l, h");
            Add( "6d", "ld", "l, l");
            Add( "6e", "ld", "l, (hl)");
            Add( "6f", "ld", "l, a");

            Add( "70", "ld", "(hl), b");
            Add( "71", "ld", "(hl), c");
            Add( "72", "ld", "(hl), d");
            Add( "73", "ld", "(hl), e");
            Add( "74", "ld", "(hl), h");
            Add( "75", "ld", "(hl), l");
            Add( "76", "halt");
            Add( "77", "ld", "(hl), a");
            Add( "78", "ld", "a, b");
            Add( "79", "ld", "a, c");
            Add( "7a", "ld", "a, d");
            Add( "7b", "ld", "a, e");
            Add( "7c", "ld", "a, h");
            Add( "7d", "ld", "a, l");
            Add( "7e", "ld", "a, (hl)");
            Add( "7f", "ld", "a, a");

            Add( "80", "add", "a, b");
            Add( "81", "add", "a, c");
            Add( "82", "add", "a, d");
            Add( "83", "add", "a, e");
            Add( "84", "add", "a, h");
            Add( "85", "add", "a, l");
            Add( "86", "add", "a, (hl)");
            Add( "87", "add", "a, a" );
            Add( "88", "adc", "a, b");
            Add( "89", "adc", "a, c");
            Add( "8a", "adc", "a, d");
            Add( "8b", "adc", "a, e");
            Add( "8c", "adc", "a, h");
            Add( "8d", "adc", "a, l");
            Add( "8e", "adc", "a, (hl)");
            Add( "8f", "adc", "a, a");

            Add( "90", "sub", "b");
            Add( "91", "sub", "c");
            Add( "92", "sub", "d");
            Add( "93", "sub", "e");
            Add( "94", "sub", "h");
            Add( "95", "sub", "l");
            Add( "96", "sub", "(hl)");
            Add( "97", "sub", "a");
            Add( "98", "sbc", "a, b");
            Add( "99", "sbc", "a, c");
            Add( "9a", "sbc", "a, d");
            Add( "9b", "sbc", "a, e");
            Add( "9c", "sbc", "a, h");
            Add( "9d", "sbc", "a, l");
            Add( "9e", "sbc", "a, (hl)");
            Add( "9f", "sbc", "a, a");

            Add( "a0", "and", "b");
            Add( "a1", "and", "c");
            Add( "a2", "and", "d");
            Add( "a3", "and", "e");
            Add( "a4", "and", "h");
            Add( "a5", "and", "l");
            Add( "a6", "and", "(hl)");
            Add( "a7", "and", "a");
            Add( "a8", "xor", "b");
            Add( "a9", "xor", "c");
            Add( "aa", "xor", "d");
            Add( "ab", "xor", "e");
            Add( "ac", "xor", "h");
            Add( "ad", "xor", "l");
            Add( "ae", "xor", "(hl)");
            Add( "af", "xor", "a");

            Add( "b0", "or", "b");
            Add( "b1", "or", "c");
            Add( "b2", "or", "d");
            Add( "b3", "or", "e");
            Add( "b4", "or", "h");
            Add( "b5", "or", "l");
            Add( "b6", "or", "(hl)");
            Add( "b7", "or", "a");
            Add( "b8", "cp", "b");
            Add( "b9", "cp", "c");
            Add( "ba", "cp", "d");
            Add( "bb", "cp", "e");
            Add( "bc", "cp", "h");
            Add( "bd", "cp", "l");
            Add( "be", "cp", "(hl)");
            Add( "bf", "cp", "a");

            Add( "c0", "ret", "nz" );
            Add( "c1", "pop", "bc" );
            Add( "c2", "jp", "nz, {nn}" );
            Add( "c3", "jp", "{nn}" );
            Add( "c4", "call", "nz, {nn}" );
            Add( "c5", "push", "bc" );
            Add( "c6", "add", "a, {n}" );
            Add( "c7", "rst", "00" ); // hex value
            Add( "c8", "ret", "z" );
            Add( "c9", "ret" );
            Add( "ca", "jp", "z, {nn}" );
            Add( "cb", "Bit" );
            Add( "cc", "call", "z, {nn}" );
            Add( "cd", "call", "{nn}" );
            Add( "ce", "adc", "a, {n}" );
            Add( "cf", "rst", "08" );

            Add( "d0", "ret", "nc" );
            Add( "d1", "pop", "de" );
            Add( "d2", "jp", "nc, {nn}" );
            Add( "d3", "out", "port({n}), a" );
            Add( "d4", "call", "nc, {nn}" );
            Add( "d5", "push", "de" );
            Add( "d6", "sub", "{n}" );
            Add( "d7", "rst", "10" );
            Add( "d8", "ret", "c" );
            Add( "d9", "exx" );
            Add( "da", "jp", "c, {nn}" );
            Add( "db", "in", "a, port({n})" );
            Add( "dc", "call", "c, {nn}" );
            Add( "dd", "->ix" ); // not supported as yet...
            Add( "de", "sbc", "a, {n}" );
            Add( "df", "rst", "18" );

            Add( "e0", "ret po unset" );
            Add( "e1", "pop", "hl" );
            Add( "e2", "jp po unset", "{nn}" );
            Add( "e3", "ex", "(sp), hl" );
            Add( "e4", "call po unset", "{nn}" );
            Add( "e5", "push", "hl" );
            Add( "e6", "and", "{n}" );
            Add( "e7", "rst", "20" );
            Add( "e8", "ret", "pe" );
            Add( "e9", "jp", "(hl)" );
            Add( "ea", "jp pe set", "{nn}" );
            Add( "eb", "ex", "de, hl" );
            Add( "ec", "call pe", "{nn}" );
            Add( "ed", "Misc." );
            Add( "ee", "xor", "{n}" );
            Add( "ef", "rst", "28" );

            Add( "f0", "ret p");
            Add( "f1", "pop", "af");
            Add( "f2", "jp", "pc, {nn}");
            Add( "f3", "di");
            Add( "f4", "call pc", "{nn}");
            Add( "f5", "push", "af");
            Add( "f6", "or", "{n}");
            Add( "f7", "rst", "30");
            Add( "f8", "ret", "m");
            Add( "f9", "ld", "sp, hl");
            Add( "fa", "jp", "m, {nn}");
            Add( "fb", "ei");
            Add( "fc", "call m", "{nn}");
            Add( "fd", "IY");
            Add( "fe", "cp", "{n}");
            Add( "ff", "rst", "38");

            Add( "ed42", "sbc", "hl, bc" );
            Add( "ed52", "sbc", "hl, de" );
            Add( "ed62", "sbc", "hl, hl" );
            Add( "ed72", "sbc", "hl, sp" );
            Add( "edb0", "ldir" );
            Add( "edb1", "cpir" );
            Add( "edb2", "inir" );
            Add( "edb2", "otir" );
            Add( "edb8", "lddr" );
            Add( "edb9", "cpdr" );
            Add( "edba", "intr" );
            Add( "edbb", "ottr" );

            Add( "CB24", "sla", "h" );

            Add( "DD24", "inc", "ixh" );
            Add( "DD2C", "inc", "ixl" );
            Add( "DD25", "dec", "ixh" );
            Add( "DD2d", "dec", "ixl" );
            Add( "DD09", "add", "ix, bc" );
            Add( "DD94", "sub", "a, IXH" );
            Add( "DD84", "add", "a, ixh" );
            Add( "DD26nn", "ld" , "ixh, n" );
            Add( "DD2Enn", "ld" , "ixl, n" );

            Add( "fd24",   "inc", "iyh" );
            Add( "FD2c",   "inc", "iyl" );
            Add( "FD25",   "dec", "iyh" );
            Add( "FD2d",   "dec", "iyl" );
            Add( "FDb6nn", "or" , "(iy+d)" );
            Add( "FD09",   "add", "iy, bc" );
            Add( "FD26nn", "ld" , "iyh, n" ); //11T
            Add( "FD2Enn", "ld" , "iyl, n" ); //11T

            InitNew();
        }

        protected void Add( string strByteCode, string strName, string? strParams = null ) {
            try {
                int  iLen  = strByteCode.Length >> 1; // 1, 2, 3 result.
                byte bExtn = 0;
                byte bBase = Convert.ToByte( strByteCode[0..2], 16 );

                // Two byte instruction. Leading is the extn.
                // unless it takes an numerical argument.
                if( iLen > 1 && strByteCode[2] != 'n' ) {
                    bExtn = bBase;
                    bBase = Convert.ToByte( strByteCode[2..4], 16 );
                }

                Z80Instr oInstr = new ( strName, strParams ) { Length = iLen };

                if( bExtn != 0 ) {
                    oInstr.InstrExt = bExtn;
                }

                switch( bExtn ) {
                    case 0:
                        _rgMain[bBase] = oInstr;
                        break;
                    case 0xfd:
                        _rgExFD[bBase] = oInstr;
                        break;
                    case 0xdd:
                        _rgExDD[bBase] = oInstr;
                        break;
                    case 0xcb:
                        _rgBitI[bBase] = oInstr;
                        break;
                    case 0xed:
                        _rgMisc[bBase] = oInstr;
                        break;
                    default:
                        throw new NotSupportedException();
                }
            } catch( Exception oEx ) {
                Type[] rgErrors = [ typeof( ArgumentException ),
                                    typeof( ArgumentNullException ),
                                    typeof( FormatException ),
                                    typeof( OverflowException ) ];
                if( rgErrors.IsUnhandled( oEx ) )
                    throw;

                // throw this exception so the shell knows it's safe enough
                // to abort the load but continue execution. You know, I
                // should probably create a new exception for "module" failure...
                throw new ApplicationException( "Problem loading Z80 Defs", oEx );
            }
        }

        /// <summary>
        /// This is the old entry to find the instruction use
        /// FindInst instead!
        /// </summary>
        /// <param name="iIndex"></param>
        /// <returns></returns>
        [Obsolete]public Z80Instr FindMain( int iIndex ) {
            return _rgMain[iIndex];
        }

        /// <seealso cref="BasicCompiler.FindInst(int)" />
        public Z80Instr FindInst( Z80Memory _rgRam, int iAddr ) {
            byte iLowByte = _rgRam[iAddr];
            byte iHiByte  = _rgRam[iAddr + 1 ];

            Z80Instr oInst;

            switch( iLowByte ) {
                case 0xec:
                    oInst = BitI( iHiByte );
                    break;
                case 0xed:
                    oInst = Misc( iHiByte );
                    break;
                case 0xdd:
                    oInst = ExDD( iHiByte );
                    break;
                case 0xfd:
                    oInst = ExFD( iHiByte );
                    break;
                default: 
                    return _rgMain[ iLowByte ];
            }
            // This will work for all but the 3 byte instructions!
            if( oInst is null ) {
                oInst = new Z80Instr( "udoc", "?" ) { 
                    InstrExt = iHiByte, 
                    Instr = iLowByte,
                    NumberLocation = new ColorRange( 0, 0 ) };
            }
            return oInst;
        }


        public Z80Instr this [ int iIndex ] {
            get => _rgMain[iIndex];
        }

        public Z80Instr Misc( int iIndex ) {
            return _rgMisc[iIndex];
        }
        public Z80Instr BitI( int iIndex ) {
            return _rgBitI[iIndex];
        }

        public Z80Instr ExDD( int iIndex ) {
            return _rgExDD[iIndex];
        }

        public Z80Instr ExFD( int iIndex ) {
            return _rgExFD[iIndex];
        }

        /// <summary>
        /// Initialize our instruction definitions. Currently called in the
        /// constructor of this class, which is a little evil. But fix later.
        /// </summary>
        /// <exception cref="InvalidDataException"></exception>
        private void InitNew() {
            Regex oReg = new Regex("{n+}|{d+}", RegexOptions.IgnoreCase);

            InitArray( _rgMain, oReg );
            InitArray( _rgBitI, oReg );
            InitArray( _rgMisc, oReg );
            InitArray( _rgExDD, oReg );
            InitArray( _rgExFD, oReg );
        }

        private void InitArray( Z80Instr[] rgInstrs, Regex oReg ) {
            for( int i=0; i<rgInstrs.Length; i++ ) {
                Z80Instr oInstr = rgInstrs[i];
                // not all the biti instr's are defined.
                if( oInstr is not null ) {
                    Match oMatch = oReg.Match( oInstr.Params );

                    Setup( oInstr, i, oMatch );
                }
            }
        }

        private void Setup( Z80Instr oInstr, int iIndex, Match oMatch ) {
            if( oInstr == null )
                throw new ArgumentNullException();

            oInstr.Instr = (byte)iIndex;

            if( !oMatch.Success ) {
                if( string.Compare( oInstr.Name, "rst"  ) == 0 ) {
                    oInstr.Jump = JumpType.Abs;
                }
                return;
            }

            if( oMatch.Groups.Count > 1 ) 
                throw new InvalidDataException("Unexpected z80 instruction" );

            oInstr.NumberLocation = new ColorRange( oMatch.Index, oMatch.Length ); 

            if( string.Compare( oInstr.Name, "call" ) == 0 )
                oInstr.IsCall = true;

            if( string.Compare( oInstr.Name, "jp"   ) == 0 ||
                string.Compare( oInstr.Name, "jr"   ) == 0 ||
                oInstr.IsCall )
            {
                switch( oInstr.Params[oMatch.Index+1] ) { // ignore first '{'
                    case 'n':
                        oInstr.Jump = JumpType.Abs;
                        break;
                    case 'd':
                        oInstr.Jump = JumpType.Rel;
                        break;
                    default:
                        throw new InvalidDataException("Unexpected z80 jump type" );
                }
            }
            switch( oMatch.Length ) {
                case 0:
                    throw new InvalidDataException("Problem with z80 instr table" );
                case 3: // "{n}"
                    oInstr.Length += 1;
                    break;
                case 4: // "{nn}"
                    oInstr.Length += 2;
                    break;
                default:
                    break;
            }
        }
    }

    public class PortsDazzle : IPorts {
        DocumentMonitor Mon { get; }
        public PortsDazzle( DocumentMonitor oMon ) { 
            Mon = oMon ?? throw new ArgumentNullException();
        }

        public string Name => "dazzler";

        public bool NMI  => false;
        public bool MI   => false;
        public byte Data => 0x00;

        public byte ReadPort(ushort usAddress) {
            byte bLowAddr = (byte)( 0x00ff & usAddress );
            byte bValue   = 0;

            switch( bLowAddr ) {
                case 0x00:
                    return 0;
              //case 0x01: // Looking for F for 'freeze' and 0x18, Ctrl-X for 'cancel' 
                case 0x02:
                    Mon.Doc_Terminal.Buffer.TryDequeue( out bValue );
                    break;
            }

            return bValue;
        }

        /// <summary>
        /// The address might have the Acc set as the high byte
        /// It's some weird implementation deal. Might be a deal
        /// breaker for instructions that send a 16 bit addr AND
        /// the Acc? Like OUT (BC)
        /// E & F represent output ports here.
        /// </summary>
        /// <remarks>
        /// Dazzler Modes Overview:
        /// * 32 x 32 Color Mode: Used by KScope; 
        ///   each nibble (4 bits) defines color and intensity 
        ///   for a pixel on a low-resolution grid.
        /// * 64 x 64 Color Mode: Uses a 2K 
        ///   memory block with 2 bits per pixel. 
        /// * 64 x 64 / 128 x 128 
        ///   Monochrome Modes: Higher-resolution black-and-white 
        ///   configurations.
        /// 
        /// I don't implement the packed bpp modes (yet) but I'm
        /// adding 8 bit mono mode. And in the future 8 bit color.
        /// </remarks>
        public void WritePort(ushort usAddress, byte bValue) {
            byte          bLowAddr = (byte)( 0x00ff & usAddress );
            DazzleDisplay oDaz     = Mon.Doc_Display;

            switch( bLowAddr ) {
                case 0x01:
                case 0x02:
                    // This has been getting tiny basic term output!!
                    Mon.Doc_Terminal.AppendChar( Convert.ToChar( bValue ) );
                    break;
                case 0x0e:
                    byte bDazzleOffs = (byte)( bValue & 0x7f );
                    bool bDazzleOn   =       ( bValue & 0x80 ) > 0;
                    int  iDazzleAddr = bDazzleOffs * 0x200;

                    oDaz.Address = iDazzleAddr;
                    break;
                case 0x0f:
                    // Color Modes (D4 = 0, D0 = lsb)
                    //      0x00 : 32 x 32 Color Mode (Uses a 512-byte buffer)
                    //      0x20 : 64 x 64 Color Mode (Uses a 2 kB buffer) 
                    // 2. Monochrome "X4" Modes (D4 = 1)
                    //      0x1X :  64 x  64 Monochrome Mode (Uses a 512-byte buffer)
                    //      0x3X : 128 x 128 Monochrome Mode (Uses a 2 kB buffer)
                    oDaz.Mono         = ( bValue  & 0x10 ) > 0;
                    oDaz.BitsPerPixel = ( bValue <= 0x30 ) ? 4 : 8;

                    switch( bValue ) {
                        case 0x20:
                        case 0x30:
                            oDaz.SetSize( DazzleDisplay.ImageSizes.S64x64 );
                            break;
                        case 0x00:
                        case 0x10:
                            oDaz.SetSize( DazzleDisplay.ImageSizes.S32x32 );
                            break;

                        case 0x40:
                        case 0x50:
                            oDaz.SetSize( DazzleDisplay.ImageSizes.S32x32 );
                            break;
                        case 0x80:
                        case 0x90:
                            oDaz.SetSize( DazzleDisplay.ImageSizes.S64x64 );
                            break;
                        case 0xC0:
                        case 0xD0:
                            oDaz.SetSize( DazzleDisplay.ImageSizes.S128x128 );
                            break;
                    }
                    break;
                case 0x10:
                    Mon.RefreshDisplay();
                    break;
            }
        }
    }

    public class PortsTinyBasic : IPorts {
        DocumentMonitor Mon { get; }

        public PortsTinyBasic( DocumentMonitor oMon ) { 
            Mon = oMon ?? throw new ArgumentNullException();
        }

        public string Name => "tiny";

        public bool NMI  => false;
        public bool MI   => false;
        public byte Data => 0x00;

        /// <summary>
        /// From device to CPU
        /// </summary>
        public byte ReadPort(ushort usAddress) {
            byte bLowAddr = (byte)( 0x00ff & usAddress );
            byte bValue   = 1;

            switch( bLowAddr ) {
                case 0x03:
                    // This is constantly getting polled. This might be like
                    // Polling input port status... 
                    // MUST return bin 01 to get the tiny basic to write prompt on port 2.
                    // MUST return bin 10 to get the tiny basic to read  text   on port 2.
                    if( Mon.Doc_Terminal.Buffer.Count > 0 )
                        return (byte)( 2 | bValue );

                    return bValue;
                case 0x02:
                    // The queue throws an exception if it's empty... this might
                    // happen if we've got this "device" plugged in but the program
                    // is expecting something else... O.o
                    if( Mon.Doc_Terminal.Buffer.Count <= 0 )
                        return 0;

                    // My attempt at Term->CPU communicate. is hit if return 2.
                    return Convert.ToByte( Mon.Doc_Terminal.Buffer.Dequeue() );
                default:
                    throw new NotImplementedException();
            }
        }

        /// <summary>
        /// From CPU to device
        /// </summary>
        //_rgFromDev3.Enqueue( Convert.ToChar( bValue ) );
        public void WritePort(ushort usAddress, byte bValue) {
            byte bLowAddr = (byte)(   0x00ff & usAddress );
            byte bHiAddr  = (byte)( ( 0xff00 & usAddress ) >> 8 );

            switch( bLowAddr ) {
                case 0x03:
                    // 4PIO...
                    //         D2, D1, D0 control                IN (2), IN (1), IN (0)
                    // D4, D3,            control OUT(1), OUT(0)
                    //  0   1   1   1   0 : out(0), in(2), in(1)
                    //  1   0   1   1   1 : out(1), in(2), in(1), in(0)
                    break;
                case 0x02:
                    // This has been getting tiny basic term output!!
                    Mon.Doc_Terminal.AppendChar( Convert.ToChar( bValue ) );
                    break;
                case 0x01:
                    // BUG: This looks wrong need to put the key queue over
                    //      in the terminal and see if any key here.
                    //_rgToDevice.Enqueue( Convert.ToChar( bValue ) );
                    break;
            }
        }
    }

    public class DocumentMonitor :
        IPgParent,
		IDisposable,
        IPgLoadUrl,
        IPgSaveUrl
    {
        protected readonly IPgBaseSite       _oBaseSite;
        protected readonly IPgRoundRobinWork _oWorkPlace; 

        public IPgParent Parentage => _oBaseSite.Host;
        public IPgParent Services  => Parentage.Services;

        // Move these to doc prop's later...
        protected string _strBinaryFileName = string.Empty;
        public    string FileName { get; protected set; }  = string.Empty;
        protected SortedSet<ushort> _rgBreakPoints = new SortedSet<ushort>();

        public    Z80Memory      Memory { get; }
        protected Z80Definitions _rgZ80Def;
        public    Z80            Cpu { get; protected set; }
        protected bool           _fCpm = false;
        protected ushort         _usStartAddr = 0;

        public event Action<int>? RefreshScreen;
        public void Raise_RefreshScreen() { RefreshScreen?.Invoke(0 ); }

        public AsmEditor         Doc_Asm     { get; }
        public Editor            Doc_Outl    { get; } // Call address list.
        public DazzleDisplay     Doc_Display { get; }
        public MonitorProperties Doc_Props   { get; }
        public DocTerminal       Doc_Terminal{ get; }

        protected List<int>? _rgLabels;

        public bool IsDirty => Doc_Asm.IsDirty;

        public class DocSlot :
            IPgBaseSite
        {
            protected readonly DocumentMonitor _oHost;

            public DocSlot( DocumentMonitor oHost ) {
                _oHost = oHost;
            }
            public IPgParent Host => _oHost;

            public void LogError(string strMessage, string strDetails, bool fShow = true) {
                _oHost._oBaseSite.LogError(strMessage, strDetails, fShow);
            }

            public void Notify(ShellNotify eEvent) {
                _oHost._oBaseSite.Notify( eEvent );
            }
        }

        public class MonitorProperties : DocProperties {
		    public enum Labels : int {
			    Acc = 0,
                Flags,
			    BC,
                DE,
                HL,
                SP,
                PC,
                IX,
                IY,
                Halt
		    }

            public MonitorProperties(IPgBaseSite oSiteBase) : base(oSiteBase) {
            }

            public override bool InitNew() {
                if( !base.InitNew() )
				    return false;

                foreach( Labels eLabel in Enum.GetValues(typeof(Labels))) {
				    CreatePropertyPair( eLabel.ToString() );
                }

              //LabelUpdate( (int)Labels.Caret, "Caret Addr", SKColors.LightGoldenrodYellow );
              //Unfortunately, this would require the view / property page communication, which
              //while possible, is a bit of a pain. So I just added an address column to the
              //debug screen.

			    return true;
            }

            public void Update( DocumentMonitor oMon ) {
                using Manipulator oBulk = new Manipulator( oMon.Doc_Props );
                StringBuilder sbFlags = new();

                sbFlags.Append( "S:" );
                sbFlags.Append( ( oMon.Cpu.Flags & Z80.Fl_S ) > 0 ? "1" : "0" );
                sbFlags.Append( " Z:" );
                sbFlags.Append( ( oMon.Cpu.Flags & Z80.Fl_Z ) > 0 ? "1" : "0" );
                sbFlags.Append( " H:" );
                sbFlags.Append( ( oMon.Cpu.Flags & Z80.Fl_H ) > 0 ? "1" : "0" );
                sbFlags.Append( " PV:" );
                sbFlags.Append( ( oMon.Cpu.Flags & Z80.Fl_PV ) > 0 ? "1" : "0" );
                sbFlags.Append( " N:" );
                sbFlags.Append( ( oMon.Cpu.Flags & Z80.Fl_N ) > 0 ? "1" : "0" );
                sbFlags.Append( " C:" );
                sbFlags.Append( ( oMon.Cpu.Flags & Z80.Fl_C ) > 0 ? "1" : "0" );

                oBulk.SetValue( (int)Labels.Acc,   oMon.Cpu.Ac.ToString( "X2" ) );
                oBulk.SetValue( (int)Labels.Flags, sbFlags.ToString() );
                oBulk.SetValue( (int)Labels.BC,    oMon.Cpu.Bc.ToString( "X4" ) );
                oBulk.SetValue( (int)Labels.DE,    oMon.Cpu.De.ToString( "X4" ) );
                oBulk.SetValue( (int)Labels.HL,    oMon.Cpu.Hl.ToString( "X4" ) );
                oBulk.SetValue( (int)Labels.SP,    oMon.Cpu.Sp.ToString( "X4" ) );
                oBulk.SetValue( (int)Labels.PC,    oMon.Cpu.Pc.ToString( "X4" ) );
                oBulk.SetValue( (int)Labels.IX,    oMon.Cpu.Ix.ToString( "X4" ) );
                oBulk.SetValue( (int)Labels.IY,    oMon.Cpu.Iy.ToString( "X4" ) );
                oBulk.SetValue( (int)Labels.Halt,  oMon.Cpu.Halt ? "yes" : "no" );
              //oBulk.SetValue( (int)Labels.Caret, oMon.Z80Memory[oMon._cpuZ80.Pc].ToString( "X4" ) );
            }

            public void Blank() {
                using Manipulator oBulk = new Manipulator( this );

                oBulk.SetValue( (int)Labels.Acc,   "--" );
                oBulk.SetValue( (int)Labels.Flags, "--" );
                oBulk.SetValue( (int)Labels.BC,    "----" );
                oBulk.SetValue( (int)Labels.DE,    "----" );
                oBulk.SetValue( (int)Labels.HL,    "----" );
                oBulk.SetValue( (int)Labels.SP,    "----" );
                oBulk.SetValue( (int)Labels.PC,    "----" );
                oBulk.SetValue( (int)Labels.IX,    "----" );
                oBulk.SetValue( (int)Labels.IY,    "----" );
                oBulk.SetValue( (int)Labels.Halt,  "?" );
            }
        } // end class MonitorProperties

        public DocumentMonitor( IPgBaseSite oBaseSite ) {
            _oBaseSite  = oBaseSite ?? throw new ArgumentNullException();
            _oWorkPlace = ((IPgScheduler)Services).CreateWorkPlace() ?? throw new InvalidProgramException();

            _rgZ80Def = new Z80Definitions();
            Memory    = new Z80Memory( (int)Math.Pow( 2, 16 ) );

            // Default ports might get updated at load phase
            Cpu   = new Z80( Memory, new PortsTinyBasic( this ) );

            Doc_Asm     = new ( new DocSlot( this ) );
            Doc_Outl    = new ( new DocSlot( this ) );
            Doc_Display = new ( new DocSlot( this ) );
            Doc_Props   = new ( new DocSlot( this ) );
            Doc_Terminal= new ( new DocSlot( this ) );
        }

        protected void LogError( string strLabel, string strMessage ) {
            _oBaseSite.LogError( strLabel, strMessage );
        }

        public void Dispose() {
            Doc_Props.SubmitEvent -= OnSubmitEvent_CpuProperties;
            Memory   .WriteTrap   -= WriteTrap_Memory;

            _oWorkPlace.Stop();
        }

		public SKImage GetResource( string strName ) {
			Assembly oAsm   = Assembly.GetExecutingAssembly();
            string   strRes = oAsm.GetName().Name + ".Content." + strName;

			return SKImageResourceHelper.GetImageResource( oAsm, strRes );
		}

        public ushort PC => Cpu.Pc;

        /// <summary>
        /// This is needed by the embedded Doc_Asm to determine
        /// what's going on with the CPU.
        /// BUG: not sure I need this now the asmeditor handles.
        /// </summary>
        /// <seealso cref="AsmEditor.PlayStatus"/>
        public WorkerStatus PlayStatus {
            get{
                if( _oWorkPlace.Status != WorkerStatus.FREE ) 
                    return _oWorkPlace.Status; 

                //if( _cpuZ80.Pc != 0 )
                //    return WorkerStatus.BUSY;

                return WorkerStatus.FREE;
            }
        }

        protected void OnSubmitEvent_CpuProperties( int[] rgChangedProps ) {
            foreach( int iProp in rgChangedProps ) {
                switch( iProp ) {
                    case (int)MonitorProperties.Labels.PC:
                        Cpu.Pc = (ushort)Doc_Props.ValueAsHex( iProp );
                        break;
                    case (int)MonitorProperties.Labels.Acc:
                        Cpu.Ac = (byte  )Doc_Props.ValueAsHex( iProp );
                        break;
                }
            }
            Doc_Asm       .UpdateHighlightLine( Cpu.Pc );
            RefreshScreen?.Invoke( 0 );
        }


        /// <summary>
        /// Start address and memory size hard coded atm but
        /// later we'll make those property page stuff.
        /// </summary>
        /// <seealso cref="LoadUrl">
        public bool InitNew() {
            if( !Doc_Outl.InitNew() )
                return false;

            if( !Doc_Asm.InitNew() )
                return false;

            if( !Doc_Display.InitNew() )
                return false;

            if( !Doc_Props.InitNew() )
                return false;

            if( !Doc_Terminal.InitNew() )
                return false;

            Doc_Props.SubmitEvent += OnSubmitEvent_CpuProperties;
            Memory   .WriteTrap   += WriteTrap_Memory;

            StatusUpdate ();

            return true;
        }

        private void WriteTrap_Memory(int iAddr ) {
            //Doc_Display.Load(Memory.RawMemory);
            //RefreshScreen?.Invoke(0);
        }

        public bool Save() {
            bool fSaved = false;
            try {
                FileInfo   oFile       = new (FileNameForSymbols);
                FileStream oByteStream = oFile.OpenWrite(); 

                using( StreamWriter oWriter = new ( oByteStream, ASCIIEncoding.UTF8 ) ) {
                    fSaved = SaveSymbols( oWriter );
					oWriter.Flush();
                }
            } catch( Exception oEx ) {
				if( _rgFileErrors.IsUnhandled( oEx ) )
					throw;

                LogError( "Save", "Couldn't save symbol table" );
            }
            return fSaved;
        }

        public string Moniker => _strBinaryFileName;

        /// <seealso cref="LoadSymbols(Stream)"
        protected bool SaveSymbols(TextWriter oStream) {
            try {
                XmlDocument xmlDoc      = new XmlDocument();
                XmlElement  xmlRoot     = xmlDoc.CreateElement( "root" );
                XmlElement  xmlBinary   = xmlDoc.CreateElement( "binary" );
                XmlElement  xmlPort     = xmlDoc.CreateElement( "ports" );
                XmlElement  xmlComments = xmlDoc.CreateElement( "documenting" );

                xmlDoc .AppendChild( xmlRoot );
                xmlRoot.AppendChild( xmlBinary ); // Just so we know for sure.
                xmlRoot.AppendChild( xmlPort );
                xmlRoot.AppendChild( xmlComments );

                xmlBinary.InnerText = FileName;
                xmlBinary.SetAttribute( "cpm", _fCpm.ToString().ToLower() );
                if( !_fCpm ) {
                    string strAddr = "0x" +  _usStartAddr.ToString( "X4" );
                    xmlBinary.SetAttribute( "address", strAddr );
                }

                xmlPort  .InnerText = Cpu.Ports.Name;

                foreach( Row oNote in Doc_Asm ) {
                    if( oNote is AsmRow oInstr &&
                        ( // oInstr.Label  .ElementCount > 0 ||
                          oInstr.Comment.ElementCount > 0    ) ) 
                    {
                        XmlElement xmlNote = xmlDoc.CreateElement( "note" );

                        xmlNote.SetAttribute( "addr", oInstr.AddressMap.ToString() );
                      //xmlNote.SetAttribute( "lbl",  oInstr.Label.     ToString() );

                        string? strComment = oInstr.Comment.ToString();
                        if( !string.IsNullOrEmpty( strComment ) ) {
                            xmlNote.InnerText = strComment;
                        }

                        xmlComments.AppendChild( xmlNote );
                    }
                }

                xmlDoc.Save( oStream );
                Doc_Asm.IsDirty = false;
            } catch( Exception oEx ) {
                Type[] rgErrors = { typeof( NullReferenceException ),
                                    typeof( IOException ),
                                    typeof( OutOfMemoryException ),
                                    typeof( ObjectDisposedException ),
                                    typeof( ArgumentOutOfRangeException ),
                                    typeof( XmlException ) };
                if( rgErrors.IsUnhandled( oEx ) )
                    throw;

                return false;
            }

            return true;
        }

		public static readonly Type[] _rgFileErrors = { 
					typeof( ArgumentNullException ),
					typeof( ArgumentException ),
					typeof( NullReferenceException ),
					typeof( DirectoryNotFoundException ),
					typeof( IOException ),
					typeof( UnauthorizedAccessException ),
					typeof( PathTooLongException ),
					typeof( SecurityException ),
                    typeof( InvalidOperationException ),
                    typeof( NotSupportedException ),
                    typeof( FileNotFoundException ) };

        /// <summary>
        /// This is an unusual document in that it is simply an XML
        /// file with pointers to the actual files we are interested in:
        /// 1) The binary which we will dissassemble.
        /// 2) The source comments we are adding.
        /// 3) Markers for the code/data portions of the binary file.
        /// </summary>
        /// <param name="oStream"></param>
        /// <returns></returns>
        protected bool LoadMemory( Stream oStream, int iCount, bool fComFile ) {
            if( oStream == null )
                throw new ArgumentNullException();

            // This isn't necessarily the z80 emulator memory. Let's
            // see how this turns out. Memory size is still tricky.
            // Well add that to property pages and .asmprg file.
            //byte[] rgRWRam = new byte[64000];

            for( int iByte = oStream.ReadByte();
                 iByte != -1;
                 iByte = oStream.ReadByte() ) 
            {
                if( iCount + 1 > Memory.Length )
                    return false;

                // Do this to avoid any cpu write traps
                // that may exist on the memory.
                Memory.RawMemory[iCount++] = ((byte)iByte);
                //Memory[iCount++] = (byte)iByte;
            }

            Memory.Reset( Memory.RawMemory, (ushort)iCount, fComFile );

            return true;
        }

        protected bool LoadBinaryFile( string strFileName, int iAddr, bool fComFile ) {
            if( string.IsNullOrEmpty( strFileName ) )
                return false;

                  FileInfo   oFile     = new FileInfo(strFileName);
            using FileStream oStream   = oFile.OpenRead();

            try {
                if( !LoadMemory( oStream, iAddr, fComFile ) ) {
                    return false;
                }

                return true;
			} catch( Exception oEx ) {
				if( _rgFileErrors.IsUnhandled( oEx ) )
					throw;

                LogError( "asmprg", "Died trying to read binary file : " + strFileName );
            }
            return false;
        }

        /// <summary>We need to load the asmprg file first to determin
        /// if the file is a cpm program or not. Then load memory and
        /// dissassemble so we are ready to load the symbols</summary>
        /// <remarks>TODO: would be nice to have a default in case
        /// we don't have an outboard asmprg file.</remarks>
        /// <seealso cref="SaveSymbols" />
        protected bool LoadSymbols( Stream oReader ) {
            XmlDocument xmlDoc = new XmlDocument();
            xmlDoc.Load( oReader );

            if( xmlDoc.SelectSingleNode( "root" ) is XmlNode xmlRoot) {
                if( xmlRoot.SelectSingleNode( "ports" ) is XmlElement xmlPort ) {
                    switch( xmlPort.InnerText ) {
                        case "dazzler":
                            Cpu.Ports = new PortsDazzle( this );
                            break;
                        case "tiny":
                            Cpu.Ports = new PortsTinyBasic( this );
                            break;
                        default:
                            LogError( "Loading", "Using default Tiny Basic Ports" );
                            break;
                    }
                }
                if( xmlRoot.SelectSingleNode( "binary" ) is XmlElement xmlBinary ) {
                    _fCpm = string.Compare( xmlBinary.GetAttribute( "cpm" ), "true" ) == 0;
                    if( _fCpm ) {
                        _usStartAddr = 0x100;
                    } else {
                        string strAddr = xmlBinary.GetAttribute( "address" );
                        if( !string.IsNullOrEmpty( strAddr ) ) {
                            if( strAddr.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) {
                                strAddr = strAddr.Substring(2);
                            }
                            _usStartAddr = ushort.Parse( strAddr, NumberStyles.HexNumber);
                        } else {
                            LogError( "Symbols", "Couldn't find start address! Defaulting to 0." );
                        }
                    }
                    Cpu.Pc = _usStartAddr;

                    if( !LoadBinaryFile( _strBinaryFileName, _usStartAddr, _fCpm ) ) {
                        return false;
                    }
                    Dissassemble();

                }

                // This is only valid if disassembled the binary first ... :-)
                if( xmlRoot.SelectNodes( "documenting/note" ) is XmlNodeList rgNodes ) {
                    foreach( XmlNode xmlNote in rgNodes ) {
                        if( xmlNote is XmlElement xmlElem ) {
                            if( xmlElem.GetAttribute( "addr" ) is string strAddr ) {
                                if( int.TryParse( strAddr, out int iAddr )) {
                                    Doc_Asm.FindRowAtAddress( iAddr, out AsmRow? oAsm ); 
                                    if( oAsm != null ) {
                                        //if( xmlElem.GetAttribute( "lbl" ) is string strLabel ) {
                                        //    oAsm[0].TryReplace( strLabel );
                                        //}
                                        if( xmlElem.InnerText is string strComment ) {
                                            oAsm.Comment.TryReplace( strComment );
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }

            return true;
        } // End LoadXml

        protected string FileNameForSymbols {
            get {
                string  strFileName = Path.GetFileNameWithoutExtension( FileName ) + ".asmprg";
                string? strFileDir  = Path.GetDirectoryName( _strBinaryFileName );

                string strFilePath;
                if( strFileDir is not null )
                    strFilePath = Path.Combine( strFileDir, strFileName );
                else 
                    strFilePath = strFileName;
            
                return strFilePath;
            }
        }

        protected void LoadSymbols() {
            try {
                FileInfo         oFile   = new FileInfo( FileNameForSymbols );
                using FileStream oStream = oFile.OpenRead();

                LoadSymbols( oStream );
			} catch( Exception oEx ) {
				if( _rgFileErrors.IsUnhandled( oEx ) )
					throw;

                LogError( "asmprg", "Died trying to symbol file : " + FileNameForSymbols );
            }
        }

        /// <summary>
        /// Only call this once.
        /// </summary>
        /// <seealso cref="InitNew"/>
        public bool LoadUrl( string strUrl ) {
            if( !Doc_Outl    .InitNew() ) // do this first. Dissassembler needs it.
                return false;
            if( !Doc_Display .InitNew() )
                return false;
            if( !Doc_Props   .InitNew() )
                return false;
            if( !Doc_Terminal.InitNew() )
                return false;

            Doc_Props.SubmitEvent += OnSubmitEvent_CpuProperties;
            Memory   .WriteTrap   += WriteTrap_Memory;


            try {
                if( string.IsNullOrEmpty( strUrl ) )
                    return false;

                      FileInfo   oFile     = new FileInfo(strUrl);
                using FileStream oStream   = oFile.OpenRead();
                      string     strExtn   = oFile.Extension;

				_strBinaryFileName = oFile.FullName; 
                FileName           = oFile.Name;

                LoadSymbols  ();
                PatchUpLabels( _rgLabels );
                StatusUpdate ();
            } catch( Exception oEx ) {
                Type[] rgErrors = { typeof( IOException ),
                                    typeof( OutOfMemoryException ),
                                    typeof( ObjectDisposedException ),
                                    typeof( ArgumentOutOfRangeException ),
                                    typeof( ArgumentException ),
                                    typeof( XmlException ) };
                if( rgErrors.IsUnhandled( oEx ) )
                    throw;

                return false;
            }
            return true;
        }

        public void Dissassemble( ) {
            if( Memory == null ) {
                LogError( "Monitor", "Load a binary first." );
                return;
            }

            try {
                Doc_Asm.Clear();

                using Z80Dissambler oDeCompile = 
                    new Z80Dissambler( _rgZ80Def, Memory, Doc_Asm, LogError );

                oDeCompile.Dissassemble();

                _rgLabels = oDeCompile.Labels;
            } catch( Exception oEx ) {
                Type[] rgErrors = { typeof( NullReferenceException ),
                                    typeof( ArgumentNullException ),
                                    typeof( ArgumentException ) };
                if( rgErrors.IsUnhandled( oEx ) )
                    throw;

                LogError( "Monitor", "Null Ref Exception in Dissassembler." );
            }
        }

        /// <summary>
        /// Do this AFTER we've attempted to load the symbols so we
        /// can have discriptive text in teh outline!
        /// </summary>
        public void PatchUpLabels( List<int>? rgOutlineLabels ) {
            if( rgOutlineLabels is null ) {
                LogError( "disassemble", "odd that the labels list is null" );
                return;
            }

            Doc_Outl.Clear();

            using BaseEditor.Manipulator oBulkOutline = Doc_Outl.CreateManipulator();

            // Take all the labels and stick them in the outline.
            foreach( int i in rgOutlineLabels ) {
                // TODO: Add label/jump formattng to line.
                string strAddr = i.ToString( "X" ); // Memory offset displayed in HEX.
                Line   oJump   = oBulkOutline.LineAppend( strAddr );
                oJump.Formatting.Add( new HyperLinkCpuJump( 0, strAddr.Length, 1 ) );

                // Go thru the assembler and update the address
                // column entry if that row is a jump target.
                foreach( Row oRow in Doc_Asm ) {
                    if( oRow is AsmRow oAsmRow &&
                        oAsmRow.AddressMap == i )
                    {
                        if( oAsmRow.Label != null ) {
                            oAsmRow.Label.TryReplace( i.ToString( "X" ) );
                        } else {
                            // Spew an error
                        }
                        oJump.TryAppend( " " + oAsmRow.Comment );
                        break;
                    }
                }
            }
        }

        protected static Type[] _rgStdErrors = 
            { typeof( NullReferenceException ),
              typeof( IndexOutOfRangeException ),
              typeof( ArgumentOutOfRangeException ) };

        protected void StatusUpdate() {
            try {
              //Doc_Asm    .Mirror( Memory );
                Doc_Asm    .UpdateHighlightLine( Cpu.Pc );
                Doc_Props  .Update( this );

                RefreshScreen?.Invoke( 0 ); // Hilight and props.

                Doc_Display.Load( Memory.RawMemory );
            } catch( Exception oEx ) {
                if( _rgStdErrors.IsUnhandled( oEx ) )
                    throw;

                LogError( "Cpu", "Status Update Error" );
            }
        }

        /// <summary>
        /// Just update the Dazzle (graphics) display. 
        /// </summary>
        public void RefreshDisplay() {
            Doc_Display.Load( Memory.RawMemory );
        }

        public void DazzleTestPattern() {
            Doc_Display.GenerateTestPattern( Memory.RawMemory );
            Doc_Display.Load               ( Memory.RawMemory );

            RefreshScreen?.Invoke( 0 );
        }

        /// <summary>
        /// Right now I don't get any event in particular when the
        /// user set's break points. So we call this function liberaly
        /// to reset things. TODO: We'll fix that later...
        /// </summary>
        protected void LoadBreakpoints() {
            _rgBreakPoints.Clear();
            foreach( Row oRow in Doc_Asm ) {
                if( oRow is AsmRow oAsm ) {
                    if( oAsm.Break.ElementCount > 0 ) {
                        _rgBreakPoints.Add( (ushort)oAsm.AddressMap );
                    }
                }
            }
        }

        /// <summary>
        /// This is where we execute the processor in free running mode.
        /// We'll do 1000 iterations and then yield to the foreground for
        /// a bit.
        /// </summary>
        public IEnumerator<int> GetProcessor() {
            if( Cpu == null ) {
                LogError( "Monitor", "CPU not available" );
                yield break;
            }

            LoadBreakpoints();

            while( true ) {
                for( int i=0; i<1000; ++i ) {
                    if( _rgBreakPoints.Count > 0 &&
                        _rgBreakPoints.Contains( Cpu.Pc ) ) 
                    {
                        StatusUpdate();
                        _oWorkPlace.Pause();
                        yield return int.MaxValue;
                    }
                    try {
                        Cpu.Parse();
                    } catch( Exception oEx ) {
                        if( _rgStdErrors.IsUnhandled( oEx ) )
                            throw;

                        LogError( "CPU", "Cpu error." );

                        StatusUpdate();
                        yield break;
                    }

                    if( Cpu.Halt ) {
                        StatusUpdate();
                        yield break;
                    }
                }
                Doc_Display.Load( Memory.RawMemory );
                //RefreshScreen?.Invoke( 0 );

                yield return 0;
            }
        }

        public void CpuStart() {
            Doc_Asm  .HighLight = null;
            Doc_Props.Blank();

            switch( _oWorkPlace.Status ) {
                case WorkerStatus.FREE:
                    _oWorkPlace.Stop();
                    _oWorkPlace.Queue( GetProcessor(), 0 );
                    break;
                case WorkerStatus.PAUSED:
                case WorkerStatus.BUSY:
                    LoadBreakpoints();
                    _oWorkPlace.Start( 0 );
                    break;
            }
        }

        public void CpuStop() {
            _oWorkPlace.Stop();
            StatusUpdate();
        }

        /// <summary>
        /// Stop the CPU. Of course, the CPU is already waiting
        /// to execute it's next command. We just hijack it.
        /// If I ever go multi threaded, this might need revisiting
        /// since I need the state of the CPU before the pause...
        /// </summary>
        public void CpuBreak() {
            _oWorkPlace.Pause();
            StatusUpdate();
        }

        public void CpuStep() {
            try {
                LoadBreakpoints();
                switch( _oWorkPlace.Status ) {
                    case WorkerStatus.FREE:
                    case WorkerStatus.PAUSED:
                        Cpu.Parse();

                        StatusUpdate();
                        break;
                    case WorkerStatus.BUSY:
                        LogError( "CPU", "Pause to single step" );
                        break;
                    default:
                        if( Cpu.Halt ) 
                            LogError( "CPU", "Cpu is halted." );
                        else
                            LogError( "CPU", "Confused." );
                        _oWorkPlace.Stop();
                        break;
                }
            } catch( Exception oEx ) {
                if( _rgStdErrors.IsUnhandled( oEx ) )
                    throw;
                LogError( "execute", "cpu confused" );
            }
        }

        public void CpuRecycle() {
            try {
                Cpu.Reset();
                Cpu.Pc            = _usStartAddr;
                Doc_Asm.HighLight = null;
                Doc_Display .Clear();
                Doc_Terminal.Clear();

                PatchUpLabels( _rgLabels );
                StatusUpdate ();
            } catch( Exception oEx ) {
                if( _rgStdErrors.IsUnhandled( oEx ) )
                    throw;
                LogError( "CPU", "Reset problem" );
            }
        }

        /// <summary>
        /// Queue up the keystrokes so the cpu can grab them if it wants.
        /// </summary>
        public void TerminalKeyPress( char cKey ) {
            Doc_Terminal.Buffer.Enqueue( Convert.ToByte( cKey ) );
        }

        public bool Execute( Guid sCmnd ) {
            if( sCmnd == GlobalCommands.JumpNext ) {
                CpuStep();
                return true;
            }
            if( sCmnd == GlobalCommands.Play ) {
                CpuStart();
                return true;
            }
            if( sCmnd == GlobalCommands.Stop ) {
                CpuStop();
                return true;
            }
            if( sCmnd == GlobalCommands.Pause ) {
                CpuBreak();
                return true;
            }
            if( sCmnd == GlobalCommands.Recycle ) {
                CpuRecycle();
                return true;
            }

            return false;
        }
    }

    public class Z80Dissambler : 
        IDisposable
    {
        readonly AsmEditor.Mangler     _oBulkAsm;
        readonly SortedSet<int>        _rgOutlineLabels = new();
        readonly StringBuilder         _sbBuilder       = new();
        readonly StringBuilder         _sbData          = new();
        readonly Z80Memory             _rgRam;
        readonly Z80Definitions        _oZ80Info;
        readonly Action<string,string> _fnLogError;

        struct AsmData {
            public byte _bData;
            public int  _iAddr;

            public AsmData( byte bDatam, int iAddr ) {
                _bData = bDatam;
                _iAddr = iAddr;
            }
        }

        public Z80Dissambler( 
            Z80Definitions        oDefinitions, 
            Z80Memory             rgMemory, 
            AsmEditor             oAsmDoc,
            Action<string,string> fnLogError
        ) {
            _rgRam        = rgMemory     ?? throw new ArgumentNullException();
            _oZ80Info     = oDefinitions ?? throw new ArgumentNullException(); 
            _fnLogError   = fnLogError   ?? throw new ArgumentNullException();

            _oBulkAsm     = new AsmEditor.Mangler( oAsmDoc );
        }

        public void Dispose() {
            _oBulkAsm    .Dispose();
        }

        public class HyperLinkCpuJump : 
            ColorRange
        {
            public override bool IsWord => true;

            public override string StateName => "CpuJump";

            public HyperLinkCpuJump( int iOffset, int iLength, int iColorIndex ) :
                base( iOffset, iLength, iColorIndex ) 
            {
            }
        }

        /// <summary>
        /// Process a single instruction.
        /// </summary>
        protected void ProcessInstruction(Z80Instr sInstr, int iAddr ) {
            try {
                _sbBuilder.Clear();

                Row?  oNewRow;

                int iNumber = 0;

                switch( sInstr.Length ) {
                    case 1:
                        break;
                    case 2: // "{n}"
                        iNumber = _rgRam[iAddr+1];
                        break;
                    case 3: // "{nn}"
                        iNumber = _rgRam[iAddr+1] + _rgRam[iAddr+2] * 0x0100;
                        break;
                    default:
                        _fnLogError( "Dissembler", "Problem with z80 instr table" );
                        return;
                }

                // Our newer 2 and 3 byte instructions are failing this test
                // so let's disable it for now. All seems to be ok.
                //if( sInstr.NumberLocation == null && sInstr.Length > 1 ) {
                //    _fnLogError( "Dissembler", "Inconsistant z80 instr" );
                //    return;
                //}

                if( sInstr.NumberLocation != null ) {
                    Line oParms = new TextLine( 0, sInstr.Params );

                    // Append the number
                    string strNumber = iNumber.ToString( sInstr.Length == 3 ? "X4" : "X2" );

                    if( !oParms.TryReplace( sInstr.NumberLocation.Offset, 
                                            sInstr.NumberLocation.Length, 
                                            strNumber ) ) {
                        _fnLogError( "Dissembler", "Unable to replace number arg in z80 instr" );
                        return;
                    }
                    oNewRow = _oBulkAsm.Append( sInstr.Name.ToUpper(),
                                                oParms.ToString() );

                    int iColorIndex;
                    if( sInstr.Jump == JumpType.None ) {
                        iColorIndex = 2;
                    } else {
                        iColorIndex = 1;
                    }

                    // Color the number
                    if( oNewRow is AsmRow oAsm ) {
                        oAsm.Param.Formatting.Add( 
                            new HyperLinkCpuJump( sInstr.NumberLocation.Offset,
                                                  strNumber.Length,
                                                  iColorIndex ) );

                        if( !_rgOutlineLabels.Contains( iNumber ) ) {
                            if( sInstr.Jump == JumpType.Abs )
                                _rgOutlineLabels.Add( iNumber );
                            if( sInstr.Jump == JumpType.Rel )
                                _rgOutlineLabels.Add( iNumber + iAddr ); // +1, +2??
                        }
                    }
                } else {
                    oNewRow = _oBulkAsm.Append( sInstr.Name.ToUpper(),  
                                                sInstr.Params );

                    if( string.Compare( "rst", sInstr.Name ) == 0 ) {
                        iNumber = int.Parse( sInstr.Params, System.Globalization.NumberStyles.HexNumber );
                        if( oNewRow is AsmRow oAsm ) {
                            oAsm.Param.Formatting.Add( new HyperLinkCpuJump( 0, sInstr.Params.Length, 1 ) ); // Hacky...
                            _rgOutlineLabels.Add( iNumber );
                        }
                    }
                }

                IColorRange oCodeColor = new ColorRange( 0, 2, 4 ); // Arghgh, don't actuall know the color...
                IColorRange oAddrColor = new ColorRange( 0, 8, 1 );

                if( oNewRow is AsmRow oAsmRow ) {
                    for( int i = 0; i < sInstr.Length; ++i ) {
                        _sbBuilder.Append( _rgRam[iAddr+i].ToString( "X2" ) );
                        oAsmRow.Bytes.TryAppend( ToChar( _rgRam[iAddr+i] ) );
                    }
                    oAsmRow.Code.TryReplace( _sbBuilder.ToString() );
                    oAsmRow.Code.Formatting.Add( oCodeColor );
                    oAsmRow.Addr.TryReplace( iAddr.ToString( "X4" ) );
                    oAsmRow.Addr.Formatting.Add( oAddrColor );
                    oAsmRow.AddressMap = iAddr;
                }
            } catch( Exception oEx ) {
                Type[] rgErrors = { typeof( NullReferenceException ),
                                    typeof( ArgumentNullException ),
                                    typeof( ArgumentException ),
                                    typeof( ArgumentOutOfRangeException ),
                                    typeof( FormatException ),
                                    typeof( OverflowException ),
                                    typeof( InvalidDataException ) };
                if( rgErrors.IsUnhandled( oEx ) )
                    throw;

                _fnLogError( "Dissembler", "Big failure in pass" );
            }
        }

        protected static char ToChar( byte bValue ) {
            if( bValue >= 0x20 && bValue <= 0x7d ) { // Upper
                return (char)bValue;
            }
            return '.';
        }

        protected Z80Instr FindInfo( int iAddr ) {
            Z80Instr oInstr;
             
            // BUG: hard coded for "tinybasic", just an experiment
            //if( iAddr >= 0x6A1 || ( iAddr >= 0xa6 && iAddr < 0xba ) ) {
            // BUG: hard coded for "kscope"
            //if( iAddr >= 0x196 ) {

            //if( iAddr >= 0x196 ) {
            //    sInstr = new Z80Instr( _rgRam[iAddr ] );
            //} else {
                //oInstr = _oZ80Info.FindMain( _rgRam[iAddr] );
                oInstr = _oZ80Info.FindInst( _rgRam, iAddr );
            //}

            return oInstr;
        }



        /// <summary>
        /// Write the byte as a ascii value or hex if not readible.
        /// </summary>
        protected void WriteDataLn( Z80Instr sInstr, int iAddr ) {
            _sbData.Clear();

            if( sInstr.Instr < 0x20 || sInstr.Instr > 0x80 ) {
                _sbData.Append(sInstr.Instr.ToString("X2")+"H");
            } else {
                _sbData.Append((char)sInstr.Instr);
            }

            Row oData = _oBulkAsm.Append( _sbData.ToString() );
            if( oData is AsmRow oAsmRow ) {
                oAsmRow.AddressMap = iAddr;
                oAsmRow.Label.TryReplace( iAddr.ToString( "X" ) );
            }
        }

        public void Dissassemble() {
            int iAddr = 0; 

            // Decode only the ROM section of our given memory.
            while( iAddr < _rgRam.RamStart ) {
                Z80Instr sInstr = FindInfo( iAddr );

                switch( sInstr.Z80Type ) {
                    case Z80Types.Instruction:
                        if( string.IsNullOrEmpty( sInstr.Name ) ) {
                            // Just put the instruction machine code and bail.
                            _oBulkAsm.Append( _rgRam[iAddr].ToString(), string.Empty );
                            break;
                        }
                        ProcessInstruction( sInstr, iAddr );
                        break;
                    case Z80Types.Data:
                        WriteDataLn( sInstr, iAddr );
                        break;
                }
                iAddr += sInstr.Length;
            }
        } // end method

        public List<int> Labels {
            get {
                return _rgOutlineLabels.ToList<int>();
            }
        }
    } // end class
}
