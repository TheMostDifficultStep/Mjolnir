using System.Text;
using System.Text.RegularExpressions;
using System.Security;
using System.Xml;

using z80;

using Play.Interfaces.Embedding;
using Play.Edit; 
using Play.Parse;
using Play.Forms;
using Play.Drawing;
using SkiaSharp;
using System.Reflection;

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
    public struct Z80Instr {
        public string     Name { get; }
        public string     Params { get; }
        public byte       Instr { get; set; }
        public int        Length { get; set; }
        public Z80Types   Z80Type { get; set; }
        public JumpType   Jump { get; set; }
        public IMemoryRange? Number { get; set; }

        public Z80Instr( string strName, string strParams = null ) {
            Instr   = 0;
            Name    = strName   ?? throw new ArgumentNullException();
            Params  = strParams ?? string.Empty;
            Length  = 1;
            Z80Type = Z80Types.Instruction;
            Jump    = JumpType.None;
            Number  = null;
        }
        public Z80Instr( byte bData ) {
            Instr   = bData;
            Name    = string.Empty;
            Params  = string.Empty;
            Length  = 1;
            Z80Type = Z80Types.Data;
            Jump    = JumpType.None;
            Number  = null;
        }

        public override string ToString() {
            return Name + " : " + Length.ToString();
        }
    }

    public class Z80Definitions {
        Z80Instr[] _rgMain = new Z80Instr[256];

        /// <exception cref="ArgumentException" />
        /// <exception cref="ArgumentNullException" />
        /// <exception cref="ArgumentOutOfRangeException" />
        /// <exception cref="InvalidDataException" />
        public Z80Definitions() { 
            _rgMain[0x00] = new Z80Instr( "Nop" );
            _rgMain[0x01] = new Z80Instr( "ld", "bc {nn}" );
            _rgMain[0x02] = new Z80Instr( "ld", "(bc), a" );
            _rgMain[0x03] = new Z80Instr( "inc", "bc" );
            _rgMain[0x04] = new Z80Instr( "inc", "b" );
            _rgMain[0x05] = new Z80Instr( "dec", "b" );
            _rgMain[0x06] = new Z80Instr( "ld", "b, {n}" );
            _rgMain[0x07] = new Z80Instr( "rlca" );
            _rgMain[0x08] = new Z80Instr( "ex", "af, af" );
            _rgMain[0x09] = new Z80Instr( "add", "hl, bc" );
            _rgMain[0x0a] = new Z80Instr( "ld", "a, (bc)" );
            _rgMain[0x0b] = new Z80Instr( "dec", "bc" );
            _rgMain[0x0c] = new Z80Instr( "inc", "c" );
            _rgMain[0x0d] = new Z80Instr( "dec", "c" );
            _rgMain[0x0e] = new Z80Instr( "ld", "c, {n}" );
            _rgMain[0x0f] = new Z80Instr( "rrca" );

            _rgMain[0x10] = new Z80Instr("djnz", "{d}"); // jump to n + pc
            _rgMain[0x11] = new Z80Instr("ld", "de, {nn}");
            _rgMain[0x12] = new Z80Instr("ld", "(de), a");
            _rgMain[0x13] = new Z80Instr("inc", "de");
            _rgMain[0x14] = new Z80Instr("inc", "e");
            _rgMain[0x15] = new Z80Instr("dec", "d");
            _rgMain[0x16] = new Z80Instr("ld", "d, {n}");
            _rgMain[0x17] = new Z80Instr("rla");
            _rgMain[0x18] = new Z80Instr("jr", "{d}" );
            _rgMain[0x19] = new Z80Instr("add", "hl, de");
            _rgMain[0x1a] = new Z80Instr("ld", "a, (de)");
            _rgMain[0x1b] = new Z80Instr("dec", "de");
            _rgMain[0x1c] = new Z80Instr("inc", "e");
            _rgMain[0x1d] = new Z80Instr("dec", "e");
            _rgMain[0x1e] = new Z80Instr("ld", "e, {n}");
            _rgMain[0x1f] = new Z80Instr("rra");

            _rgMain[0x20] = new Z80Instr("jr", "nz, d");
            _rgMain[0x21] = new Z80Instr("ld", "hl, {nn}");
            _rgMain[0x22] = new Z80Instr("ld", "({nn}), hl");
            _rgMain[0x23] = new Z80Instr("inc", "hl");
            _rgMain[0x24] = new Z80Instr("inc", "h");
            _rgMain[0x25] = new Z80Instr("dec", "h");
            _rgMain[0x26] = new Z80Instr("ld", "h, {n}");
            _rgMain[0x27] = new Z80Instr("daa");
            _rgMain[0x28] = new Z80Instr("jr", "z, {d}");
            _rgMain[0x29] = new Z80Instr("add", "hl, hl");
            _rgMain[0x2a] = new Z80Instr("ld", "hl, ({nn})");
            _rgMain[0x2b] = new Z80Instr("dec", "hl");
            _rgMain[0x2c] = new Z80Instr("inc", "l");
            _rgMain[0x2d] = new Z80Instr("dec", "l");
            _rgMain[0x2e] = new Z80Instr("ld", "l, {n}");
            _rgMain[0x2f] = new Z80Instr("cpl");

            _rgMain[0x30] = new Z80Instr("jr", "nc, {d}" );
            _rgMain[0x31] = new Z80Instr("ld", "sp, {nn}" );
            _rgMain[0x32] = new Z80Instr("ld", "({nn}), a");
            _rgMain[0x33] = new Z80Instr("inc", "sp" );
            _rgMain[0x34] = new Z80Instr("inc", "(hl)" );
            _rgMain[0x35] = new Z80Instr("dec", "(hl)" );
            _rgMain[0x36] = new Z80Instr("ld", "(hl), {n}" );
            _rgMain[0x37] = new Z80Instr("scf" ); // set c flag
            _rgMain[0x38] = new Z80Instr("jr", "c, {d}");
            _rgMain[0x39] = new Z80Instr("add", "hl, sp" );
            _rgMain[0x3a] = new Z80Instr("ld", "a, ({nn})" );
            _rgMain[0x3b] = new Z80Instr("dec", "sp" );
            _rgMain[0x3c] = new Z80Instr("inc", "a" );
            _rgMain[0x3d] = new Z80Instr("dec", "a" );
            _rgMain[0x3e] = new Z80Instr("ld", "a, {n}" );
            _rgMain[0x3f] = new Z80Instr("ccf" ); // invert carry flag

            _rgMain[0x40] = new Z80Instr("ld", "b, b" );
            _rgMain[0x41] = new Z80Instr("ld", "b, c" );
            _rgMain[0x42] = new Z80Instr("ld", "b, d" );
            _rgMain[0x43] = new Z80Instr("ld", "b, e" );
            _rgMain[0x44] = new Z80Instr("ld", "b, h" );
            _rgMain[0x45] = new Z80Instr("ld", "b, l" );
            _rgMain[0x46] = new Z80Instr("ld", "b, (hl)" );
            _rgMain[0x47] = new Z80Instr("ld", "b, a" );
            _rgMain[0x48] = new Z80Instr("ld", "c, b" );
            _rgMain[0x49] = new Z80Instr("ld", "c, c" );
            _rgMain[0x4a] = new Z80Instr("ld", "c, d" );
            _rgMain[0x4b] = new Z80Instr("ld", "c, e" );
            _rgMain[0x4c] = new Z80Instr("ld", "c, h" );
            _rgMain[0x4d] = new Z80Instr("ld", "c, l" );
            _rgMain[0x4e] = new Z80Instr("ld", "c, (hl)" );
            _rgMain[0x4f] = new Z80Instr("ld", "c, a" );

            _rgMain[0x50] = new Z80Instr("ld", "d, b");
            _rgMain[0x51] = new Z80Instr("ld", "d, c");
            _rgMain[0x52] = new Z80Instr("ld", "d, d");
            _rgMain[0x53] = new Z80Instr("ld", "d, e");
            _rgMain[0x54] = new Z80Instr("ld", "d, h");
            _rgMain[0x55] = new Z80Instr("ld", "d, l");
            _rgMain[0x56] = new Z80Instr("ld", "d, (hl)");
            _rgMain[0x57] = new Z80Instr("ld", "d, a" );
            _rgMain[0x58] = new Z80Instr("ld", "e, b");
            _rgMain[0x59] = new Z80Instr("ld", "e, c");
            _rgMain[0x5a] = new Z80Instr("ld", "e, d");
            _rgMain[0x5b] = new Z80Instr("ld", "e, e");
            _rgMain[0x5c] = new Z80Instr("ld", "e, h");
            _rgMain[0x5d] = new Z80Instr("ld", "e, l");
            _rgMain[0x5e] = new Z80Instr("ld", "e, (hl)");
            _rgMain[0x5f] = new Z80Instr("ld", "e, a");

            _rgMain[0x60] = new Z80Instr("ld", "h, b");
            _rgMain[0x61] = new Z80Instr("ld", "h, c");
            _rgMain[0x62] = new Z80Instr("ld", "h, d");
            _rgMain[0x63] = new Z80Instr("ld", "h, e");
            _rgMain[0x64] = new Z80Instr("ld", "h, h");
            _rgMain[0x65] = new Z80Instr("ld", "h, l");
            _rgMain[0x66] = new Z80Instr("ld", "h, (hl)");
            _rgMain[0x67] = new Z80Instr("ld", "h, a");
            _rgMain[0x68] = new Z80Instr("ld", "l, b");
            _rgMain[0x69] = new Z80Instr("ld", "l, c");
            _rgMain[0x6a] = new Z80Instr("ld", "l, d");
            _rgMain[0x6b] = new Z80Instr("ld", "l, e");
            _rgMain[0x6c] = new Z80Instr("ld", "l, h");
            _rgMain[0x6d] = new Z80Instr("ld", "l, l");
            _rgMain[0x6e] = new Z80Instr("ld", "l, (hl)");
            _rgMain[0x6f] = new Z80Instr("ld", "l, a");

            _rgMain[0x70] = new Z80Instr("ld", "(hl), b");
            _rgMain[0x71] = new Z80Instr("ld", "(hl), c");
            _rgMain[0x72] = new Z80Instr("ld", "(hl), d");
            _rgMain[0x73] = new Z80Instr("ld", "(hl), e");
            _rgMain[0x74] = new Z80Instr("ld", "(hl), h");
            _rgMain[0x75] = new Z80Instr("ld", "(hl), l");
            _rgMain[0x76] = new Z80Instr("halt");
            _rgMain[0x77] = new Z80Instr("ld", "(hl), a");
            _rgMain[0x78] = new Z80Instr("ld", "a, b");
            _rgMain[0x79] = new Z80Instr("ld", "a, c");
            _rgMain[0x7a] = new Z80Instr("ld", "a, d");
            _rgMain[0x7b] = new Z80Instr("ld", "a, e");
            _rgMain[0x7c] = new Z80Instr("ld", "a, h");
            _rgMain[0x7d] = new Z80Instr("ld", "a, l");
            _rgMain[0x7e] = new Z80Instr("ld", "a, (hl)");
            _rgMain[0x7f] = new Z80Instr("ld", "a, a");

            _rgMain[0x80] = new Z80Instr("add", "a, b");
            _rgMain[0x81] = new Z80Instr("add", "a, c");
            _rgMain[0x82] = new Z80Instr("add", "a, d");
            _rgMain[0x83] = new Z80Instr("add", "a, e");
            _rgMain[0x84] = new Z80Instr("add", "a, h");
            _rgMain[0x85] = new Z80Instr("add", "a, l");
            _rgMain[0x86] = new Z80Instr("add", "a, (hl)");
            _rgMain[0x87] = new Z80Instr("add", "a, a" );
            _rgMain[0x88] = new Z80Instr("adc", "a, b");
            _rgMain[0x89] = new Z80Instr("adc", "a, c");
            _rgMain[0x8a] = new Z80Instr("adc", "a, d");
            _rgMain[0x8b] = new Z80Instr("adc", "a, e");
            _rgMain[0x8c] = new Z80Instr("adc", "a, h");
            _rgMain[0x8d] = new Z80Instr("adc", "a, l");
            _rgMain[0x8e] = new Z80Instr("adc", "a, (hl)");
            _rgMain[0x8f] = new Z80Instr("adc", "a, a");

            _rgMain[0x90] = new Z80Instr("sub", "b");
            _rgMain[0x91] = new Z80Instr("sub", "c");
            _rgMain[0x92] = new Z80Instr("sub", "d");
            _rgMain[0x93] = new Z80Instr("sub", "e");
            _rgMain[0x94] = new Z80Instr("sub", "h");
            _rgMain[0x95] = new Z80Instr("sub", "l");
            _rgMain[0x96] = new Z80Instr("sub", "(hl)");
            _rgMain[0x97] = new Z80Instr("sub", "a");
            _rgMain[0x98] = new Z80Instr("sbc", "a, b");
            _rgMain[0x99] = new Z80Instr("sbc", "a, c");
            _rgMain[0x9a] = new Z80Instr("sbc", "a, d");
            _rgMain[0x9b] = new Z80Instr("sbc", "a, e");
            _rgMain[0x9c] = new Z80Instr("sbc", "a, h");
            _rgMain[0x9d] = new Z80Instr("sbc", "a, l");
            _rgMain[0x9e] = new Z80Instr("sbc", "a, (hl)");
            _rgMain[0x9f] = new Z80Instr("sbc", "a, a");

            _rgMain[0xa0] = new Z80Instr("and", "b");
            _rgMain[0xa1] = new Z80Instr("and", "c");
            _rgMain[0xa2] = new Z80Instr("and", "d");
            _rgMain[0xa3] = new Z80Instr("and", "e");
            _rgMain[0xa4] = new Z80Instr("and", "h");
            _rgMain[0xa5] = new Z80Instr("and", "l");
            _rgMain[0xa6] = new Z80Instr("and", "(hl)");
            _rgMain[0xa7] = new Z80Instr("and", "a");
            _rgMain[0xa8] = new Z80Instr("xor", "b");
            _rgMain[0xa9] = new Z80Instr("xor", "c");
            _rgMain[0xaa] = new Z80Instr("xor", "d");
            _rgMain[0xab] = new Z80Instr("xor", "e");
            _rgMain[0xac] = new Z80Instr("xor", "h");
            _rgMain[0xad] = new Z80Instr("xor", "l");
            _rgMain[0xae] = new Z80Instr("xor", "(hl)");
            _rgMain[0xaf] = new Z80Instr("xor", "a");

            _rgMain[0xb0] = new Z80Instr("or", "b");
            _rgMain[0xb1] = new Z80Instr("or", "c");
            _rgMain[0xb2] = new Z80Instr("or", "d");
            _rgMain[0xb3] = new Z80Instr("or", "e");
            _rgMain[0xb4] = new Z80Instr("or", "h");
            _rgMain[0xb5] = new Z80Instr("or", "l");
            _rgMain[0xb6] = new Z80Instr("or", "(hl)");
            _rgMain[0xb7] = new Z80Instr("or", "a");
            _rgMain[0xb8] = new Z80Instr("cp", "b");
            _rgMain[0xb9] = new Z80Instr("cp", "c");
            _rgMain[0xba] = new Z80Instr("cp", "d");
            _rgMain[0xbb] = new Z80Instr("cp", "e");
            _rgMain[0xbc] = new Z80Instr("cp", "h");
            _rgMain[0xbd] = new Z80Instr("cp", "l");
            _rgMain[0xbe] = new Z80Instr("cp", "(hl)");
            _rgMain[0xbf] = new Z80Instr("cp", "a");

            _rgMain[0xc0] = new Z80Instr("ret", "nz" );
            _rgMain[0xc1] = new Z80Instr("pop", "bc" );
            _rgMain[0xc2] = new Z80Instr("jp", "nz, {nn}" );
            _rgMain[0xc3] = new Z80Instr("jp", "{nn}" );
            _rgMain[0xc4] = new Z80Instr("call", "nz, {nn}" );
            _rgMain[0xc5] = new Z80Instr("push", "bc" );
            _rgMain[0xc6] = new Z80Instr("add", "a, {n}" );
            _rgMain[0xc7] = new Z80Instr("rst", "00" ); // hex value
            _rgMain[0xc8] = new Z80Instr("ret", "z" );
            _rgMain[0xc9] = new Z80Instr("ret" );
            _rgMain[0xca] = new Z80Instr("jp", "z, {nn}" );
            _rgMain[0xcb] = new Z80Instr("Bit" );
            _rgMain[0xcc] = new Z80Instr("call", "z, {nn}" );
            _rgMain[0xcd] = new Z80Instr("call", "{nn}" );
            _rgMain[0xce] = new Z80Instr("adc", "a, {n}" );
            _rgMain[0xcf] = new Z80Instr("rst", "08" );

            _rgMain[0xd0] = new Z80Instr("ret", "nc" );
            _rgMain[0xd1] = new Z80Instr("pop", "de" );
            _rgMain[0xd2] = new Z80Instr("jp", "nc, {nn}" );
            _rgMain[0xd3] = new Z80Instr("out", "port({n}), a" );
            _rgMain[0xd4] = new Z80Instr("call", "nc, {nn}" );
            _rgMain[0xd5] = new Z80Instr("push", "de" );
            _rgMain[0xd6] = new Z80Instr("sub", "{n}" );
            _rgMain[0xd7] = new Z80Instr("rst", "10" );
            _rgMain[0xd8] = new Z80Instr("ret", "c" );
            _rgMain[0xd9] = new Z80Instr("exx" );
            _rgMain[0xda] = new Z80Instr("jp", "c, {nn}" );
            _rgMain[0xdb] = new Z80Instr("in", "a, port({n})" );
            _rgMain[0xdc] = new Z80Instr("call", "c, {nn}" );
            _rgMain[0xdd] = new Z80Instr("->ix" ); // not supported as yet...
            _rgMain[0xde] = new Z80Instr("sbc", "a, {n}" );
            _rgMain[0xdf] = new Z80Instr("rst", "18" );

            _rgMain[0xe0] = new Z80Instr("ret po unset" );
            _rgMain[0xe1] = new Z80Instr("pop", "hl" );
            _rgMain[0xe2] = new Z80Instr("jp po unset", "{nn}" );
            _rgMain[0xe3] = new Z80Instr("ex", "(sp), hl" );
            _rgMain[0xe4] = new Z80Instr("call po unset", "{nn}" );
            _rgMain[0xe5] = new Z80Instr("push", "hl" );
            _rgMain[0xe6] = new Z80Instr("and", "{n}" );
            _rgMain[0xe7] = new Z80Instr("rst", "20" );
            _rgMain[0xe8] = new Z80Instr("ret", "pe" );
            _rgMain[0xe9] = new Z80Instr("jp", "(hl)" );
            _rgMain[0xea] = new Z80Instr("jp pe set", "{nn}" );
            _rgMain[0xeb] = new Z80Instr("ex", "de, hl" );
            _rgMain[0xec] = new Z80Instr("call pe", "{nn}" );
            _rgMain[0xed] = new Z80Instr("Misc." );
            _rgMain[0xee] = new Z80Instr("xor", "{n}" );
            _rgMain[0xef] = new Z80Instr("rst", "28" );

            _rgMain[0xf0] = new Z80Instr("ret p");
            _rgMain[0xf1] = new Z80Instr("pop", "af");
            _rgMain[0xf2] = new Z80Instr("jp", "pc, {nn}");
            _rgMain[0xf3] = new Z80Instr("di");
            _rgMain[0xf4] = new Z80Instr("call pc", "{nn}");
            _rgMain[0xf5] = new Z80Instr("push", "af");
            _rgMain[0xf6] = new Z80Instr("or", "{n}");
            _rgMain[0xf7] = new Z80Instr("rst", "30");
            _rgMain[0xf8] = new Z80Instr("ret", "m");
            _rgMain[0xf9] = new Z80Instr("ld", "sp, hl");
            _rgMain[0xfa] = new Z80Instr("jp", "m, {nn}");
            _rgMain[0xfb] = new Z80Instr("ei");
            _rgMain[0xfc] = new Z80Instr("call m", "{nn}");
            _rgMain[0xfd] = new Z80Instr("IY");
            _rgMain[0xfe] = new Z80Instr("cp", "{n}");
            _rgMain[0xff] = new Z80Instr("rst", "38");

            InitNew();
        }

        public Z80Instr FindMain( int iIndex ) {
            return _rgMain[iIndex];
        }

        private void InitNew() {
            Regex oReg = new Regex("{n+}|{d+}", RegexOptions.IgnoreCase);

            for( int i=0; i<_rgMain.Length; i++ ) {
                _rgMain[i].Instr = (byte)i;
                Match oMatch = oReg.Match( _rgMain[i].Params );
                if( oMatch != null && oMatch.Success ) {
                    if( oMatch.Groups.Count > 1 ) 
                        throw new InvalidDataException("Unexpected z80 instruction" );

                    _rgMain[i].Number = new ColorRange( oMatch.Index, oMatch.Length ); 

                    if( string.Compare( _rgMain[i].Name, "jp"   ) == 0 ||
                        string.Compare( _rgMain[i].Name, "jr"   ) == 0 ||
                        string.Compare( _rgMain[i].Name, "call" ) == 0 )
                    {
                        switch( _rgMain[i].Params[oMatch.Index+1] ) { // ignore first '{'
                            case 'n':
                                _rgMain[i].Jump = JumpType.Abs;
                                break;
                            case 'd':
                                _rgMain[i].Jump = JumpType.Rel;
                                break;
                            default:
                                throw new InvalidDataException("Unexpected z80 jump type" );
                        }
                    }
                    switch( oMatch.Length ) {
                        case 0:
                            throw new InvalidDataException("Problem with z80 instr table" );
                        case 3: // "{n}"
                            _rgMain[i].Length += 1;
                            break;
                        case 4: // "{nn}"
                            _rgMain[i].Length += 2;
                            break;
                        default:
                            break;
                    }

                } else {
                    if( string.Compare( _rgMain[i].Name, "rst"  ) == 0 ) {
                        _rgMain[i].Jump = JumpType.Abs;
                    }
                }
                
            }
        }
    }

    public class DocumentMonitor :
        IPgParent,
		IDisposable,
        IPgLoad<TextReader>,
        IPgSave<TextWriter>
    {
        protected readonly IPgBaseSite       _oBaseSite;
        protected readonly IPgRoundRobinWork _oWorkPlace; 

        public IPgParent Parentage => _oBaseSite.Host;
        public IPgParent Services  => Parentage.Services;

        // Move these to doc prop's later...
        protected string _strBinaryFileName = string.Empty;
        public    string FileName { get; protected set; }  = string.Empty;
        protected SortedSet<ushort> _rgBreakPoints = new SortedSet<ushort>();


        protected readonly Z80Memory      _rgMemory;
        protected          Z80Definitions _rgZ80Def;
        protected readonly Z80            _cpuZ80;

        public AsmEditor         Doc_Asm     { get; }
        public DataSegmDoc       Doc_Segm    { get; }
        public Editor            Doc_Outl    { get; }
        public DazzleDisplay     Doc_Display { get; }
        public MonitorProperties Doc_Props   { get; }

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
                Halted
		    }

            public MonitorProperties(IPgBaseSite oSiteBase) : base(oSiteBase) {
            }

            public override bool InitNew() {
                if( !base.InitNew() )
				    return false;

                foreach( Labels eLabel in Enum.GetValues(typeof(Labels))) {
				    CreatePropertyPair( eLabel.ToString() );
                }

			    return true;
            }

            public void Update( DocumentMonitor oMon ) {
                using Manipulator oBulk = new Manipulator( oMon.Doc_Props );
                StringBuilder sbFlags = new();

                sbFlags.Append( "S:" );
                sbFlags.Append( ( oMon._cpuZ80.Flags & (byte)Z80.Fl.S ) > 0 ? "1" : "0" );
                sbFlags.Append( " Z:" );
                sbFlags.Append( ( oMon._cpuZ80.Flags & (byte)Z80.Fl.Z ) > 0 ? "1" : "0" );
                sbFlags.Append( " H:" );
                sbFlags.Append( ( oMon._cpuZ80.Flags & (byte)Z80.Fl.H ) > 0 ? "1" : "0" );
                sbFlags.Append( " PV:" );
                sbFlags.Append( ( oMon._cpuZ80.Flags & (byte)Z80.Fl.PV ) > 0 ? "1" : "0" );
                sbFlags.Append( " N:" );
                sbFlags.Append( ( oMon._cpuZ80.Flags & (byte)Z80.Fl.N ) > 0 ? "1" : "0" );
                sbFlags.Append( " C:" );
                sbFlags.Append( ( oMon._cpuZ80.Flags & (byte)Z80.Fl.C ) > 0 ? "1" : "0" );

                oBulk.SetValue( (int)Labels.Acc, oMon._cpuZ80.Ac.ToString( "X2" ) );
                oBulk.SetValue( (int)Labels.Flags, sbFlags.ToString() );
                oBulk.SetValue( (int)Labels.BC,  oMon._cpuZ80.Bc.ToString( "X4" ) );
                oBulk.SetValue( (int)Labels.DE,  oMon._cpuZ80.De.ToString( "X4" ) );
                oBulk.SetValue( (int)Labels.HL,  oMon._cpuZ80.Hl.ToString( "X4" ) );
                oBulk.SetValue( (int)Labels.SP,  oMon._cpuZ80.Sp.ToString( "X4" ) );
                oBulk.SetValue( (int)Labels.PC,  oMon._cpuZ80.Pc.ToString( "X4" ) );
                oBulk.SetValue( (int)Labels.Halted, oMon._cpuZ80.Halt ? "yes" : "no" );
           }
        } // end class MonitorProperties

        public DocumentMonitor( IPgBaseSite oBaseSite ) {
            _oBaseSite  = oBaseSite ?? throw new ArgumentNullException();
            _oWorkPlace = ((IPgScheduler)Services).CreateWorkPlace() ?? throw new InvalidProgramException();

            _rgZ80Def = new Z80Definitions();
            _rgMemory = new Z80Memory();
            _cpuZ80   = new Z80( _rgMemory, new EmptyPorts( this ) );

            Doc_Asm     = new ( new DocSlot( this ) );
            Doc_Segm    = new ( new DocSlot( this ) );
            Doc_Outl    = new ( new DocSlot( this ) );
            Doc_Display = new ( new DocSlot( this ) );
            Doc_Props   = new ( new DocSlot( this ) );
        }

        protected void LogError( string strLabel, string strMessage ) {
            _oBaseSite.LogError( strLabel, strMessage );
        }

        public void Dispose() {
            _oWorkPlace.Stop();
        }

		public SKBitmap GetResource( string strName ) {
			Assembly oAsm   = Assembly.GetExecutingAssembly();
            string   strRes = oAsm.GetName().Name + ".Content." + strName;

			return SKImageResourceHelper.GetImageResource( oAsm, strRes );
		}

        public ushort PC => _cpuZ80.Pc;

        /// <summary>
        /// This is needed by the embedded Doc_Asm to determine
        /// what's going on with the CPU.
        /// </summary>
        public WorkerStatus PlayStatus {
            get{
                if( _oWorkPlace.Status != WorkerStatus.FREE ) 
                    return _oWorkPlace.Status; 

                if( _cpuZ80.Pc != 0 )
                    return WorkerStatus.BUSY;

                return WorkerStatus.FREE;
            }
        }

        /// <summary>
        /// Start address and memory size hard coded atm but
        /// later we'll make those property page stuff.
        /// </summary>
        /// <returns></returns>
        public bool InitNew() {
            if( !Doc_Outl.InitNew() )
                return false;

            if( !Doc_Segm.InitNew() )
                return false;

            if( !Doc_Asm.InitNew() )
                return false;

            if( !Doc_Display.InitNew() )
                return false;

            if( !Doc_Props.InitNew() )
                return false;

            Dissassemble();

            return true;
        }

        public bool Save(TextWriter oStream) {
            try {
                XmlDocument xmlDoc      = new XmlDocument();
                XmlElement  xmlRoot     = xmlDoc.CreateElement( "root" );
                XmlElement  xmlBinary   = xmlDoc.CreateElement( "binary" );
                XmlElement  xmlComments = xmlDoc.CreateElement( "documenting" );
                XmlElement  xmlDataSeg  = xmlDoc.CreateElement( "datasegments" );

                xmlDoc .AppendChild( xmlRoot );
                xmlRoot.AppendChild( xmlBinary );
                xmlRoot.AppendChild( xmlComments );

                xmlBinary.InnerText = _strBinaryFileName;

                foreach( Row oNote in Doc_Asm ) {
                    if( oNote is AsmRow oInstr &&
                        ( // oInstr.Label  .ElementCount > 0 ||
                          oInstr.Comment.ElementCount > 0    ) ) 
                    {
                        XmlElement xmlNote = xmlDoc.CreateElement( "note" );

                        xmlNote.SetAttribute( "addr", oInstr.AddressMap.ToString() );
                      //xmlNote.SetAttribute( "lbl",  oInstr.Label.     ToString() );
                        xmlNote.InnerText = oInstr.Comment.ToString();

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
        protected bool LoadMemory( Stream oStream, bool fComFile ) {
            if( oStream == null )
                throw new ArgumentNullException();

            // This isn't necessarily the z80 emulator memory. Let's
            // see how this turns out. Memory size is still tricky.
            // Well add that to property pages and .asmprg file.
            byte[] rgRWRam = new byte[4096];
            int    iCount  = fComFile ? 0x100 : 0x00; 

            for( int iByte = oStream.ReadByte();
                 iByte != -1;
                 iByte = oStream.ReadByte() ) 
            {
                if( iCount + 1 > rgRWRam.Length )
                    return false;

                rgRWRam[iCount++] = (byte)iByte;
            }

            _rgMemory.Reset( rgRWRam, (ushort)iCount, fComFile );

            return true;
        }

        protected bool LoadBinaryFile( string strFileName ) {
            if( string.IsNullOrEmpty( strFileName ) )
                return false;

                  Encoding   utf8NoBom = new UTF8Encoding(false);
                  FileInfo   oFile     = new FileInfo(strFileName);
            using FileStream oStream   = oFile.OpenRead();
                  string     strExtn   = oFile.Extension;

            bool fComFile = string.Compare( strExtn, ".Com", ignoreCase: true ) == 0; 

            try {
				_strBinaryFileName = oFile.FullName; 
                FileName           = oFile.Name;

                if( !LoadMemory( oStream, fComFile ) ) {
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

        public bool Load( TextReader oReader ) {
            if( !Doc_Outl.InitNew() ) // do this first. Dissassembler needs it.
                return false;
            if( !Doc_Display.InitNew() )
                return false;
            if( !Doc_Props.InitNew() )
                return false;

            try {
                XmlDocument xmlDoc = new XmlDocument();
                xmlDoc.Load( oReader );

                if( xmlDoc.SelectSingleNode( "root" ) is XmlNode xmlRoot) {
                    if( xmlRoot.SelectSingleNode( "binary" ) is XmlElement xmlBinary ) {
                        if( !LoadBinaryFile( xmlBinary.InnerText ) )
                            return false;
                    }
                    if( xmlRoot.SelectNodes( "datasegments" ) is XmlNodeList rgxCodeData ) {
                        if( !Doc_Segm.LoadSegments( rgxCodeData ) )
                            return false;
                    }

                    // I'd rather not dissassmble unless requested. But I need that in
                    // order to populate the comments if we have saved them previously...
                    Dissassemble();

                    // This is only valid if disassembled the binary first ... :-/
                    // BUG: I'll need to sort this out eventually.
                    foreach( XmlNode xmlNote in xmlRoot.SelectNodes( "documenting/note" ) ) {
                        if( xmlNote is XmlElement xmlElem ) {
                            if( xmlElem.GetAttribute( "addr" ) is string strAddr ) {
                                if( int.TryParse( strAddr, out int iAddr )) {
                                    if( Doc_Asm.FindRowAtAddress( iAddr, out AsmRow oAsm ) ) {
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
            if( _rgMemory == null ) {
                LogError( "Monitor", "Load a binary first." );
                return;
            }

            try {
                Doc_Asm .Clear();
                Doc_Outl.Clear();

                using Z80Dissambler oDeCompile = 
                    new Z80Dissambler( _rgZ80Def, _rgMemory, Doc_Outl, Doc_Asm, LogError );

                oDeCompile.Dissassemble();
            } catch( Exception oEx ) {
                Type[] rgErrors = { typeof( NullReferenceException ),
                                    typeof( ArgumentNullException ),
                                    typeof( ArgumentException ) };
                if( rgErrors.IsUnhandled( oEx ) )
                    throw;

                LogError( "Monitor", "Null Ref Exception in Dissassembler." );
            }
        }

        public class EmptyPorts : IPorts {
            DocumentMonitor Mon { get; }
            public EmptyPorts( DocumentMonitor oMon ) { 
                Mon = oMon ?? throw new ArgumentNullException();
            }

            public bool NMI  => false;
            public bool MI   => false;
            public byte Data => 0x00;

            public byte ReadPort(ushort usAddress) {
                byte bLowAddr = (byte)( 0x00ff & usAddress );
                byte bValue   = 0;

                switch( bLowAddr ) {
                    case 0x02:
                        // When the dazzle check for a key. Update display.
                        Mon.Doc_Display.Load( Mon._rgMemory.RawMemory, 0x200 );
                        Mon.Doc_Display.Raise_ImageUpdated();
                        return 0;
                }

                return bValue;
            }

            public void WritePort(ushort usAddress, byte bValue) {
                byte bLowAddr = (byte)( 0x00ff & usAddress );

                switch( bLowAddr ) {
                    case 0x0f:
                        // Check size from bValue
                        if( ( bValue & (1 << 5) ) > 0 )
                            Mon.Doc_Display.SetSize( DazzleDisplay.ImageSizes.SixtyFour );
                        else
                            Mon.Doc_Display.SetSize( DazzleDisplay.ImageSizes.ThirtyTwo );
                        break;
                    case 0x0e:
                        byte bDazzleOffs = (byte)( bValue & 0x7f );
                        bool bDazzleOn   = ( bValue & 0x80 ) > 0;
                        int  iDazzleAddr = bDazzleOffs * 0x200;
                        break;
                }
            }
        }

        protected static Type[] _rgStdErrors = 
            { typeof( NullReferenceException ),
              typeof( IndexOutOfRangeException ),
              typeof( ArgumentOutOfRangeException ) };

        protected void StatusUpdate() {
            try {
                Doc_Asm    .UpdateHighlightLine( _cpuZ80.Pc );
                Doc_Props  .Update( this );
                Doc_Display.Load( _rgMemory.RawMemory, 0x200 );
                Doc_Display.Raise_ImageUpdated();
            } catch( Exception oEx ) {
                if( _rgStdErrors.IsUnhandled( oEx ) )
                    throw;

                LogError( "Cpu", "Status Update Error" );
            }
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
            if( _cpuZ80 == null ) {
                LogError( "Monitor", "CPU not available" );
                yield break;
            }

            LoadBreakpoints();

            while( true ) {
                for( int i=0; i<1000; ++i ) {
                    if( _rgBreakPoints.Count > 0 &&
                        _rgBreakPoints.Contains( _cpuZ80.Pc ) ) 
                    {
                        StatusUpdate();
                        _oWorkPlace.Pause();
                        yield return int.MaxValue;
                    }
                    try {
                        _cpuZ80.Parse();
                    } catch( Exception oEx ) {
                        if( _rgStdErrors.IsUnhandled( oEx ) )
                            throw;

                        LogError( "CPU", "Cpu error." );

                        StatusUpdate();
                        yield break;
                    }

                    if( _cpuZ80.Halt ) {
                        StatusUpdate();
                        yield break;
                    }
                }

                Doc_Display.Load( _rgMemory.RawMemory, 0x200 );
                Doc_Display.Raise_ImageUpdated();
                yield return 10;
            }
        }

        public void CpuStart() {
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
            _cpuZ80.Reset();
            Doc_Asm.HighLight = null;
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
                        _cpuZ80    .Parse();

                        Doc_Asm    .UpdateHighlightLine( _cpuZ80.Pc );
                        Doc_Props  .Update( this );
                        Doc_Display.Load( _rgMemory.RawMemory, 0x200 );
                        break;
                    case WorkerStatus.BUSY:
                        // We might have set timout infinite.
                        _oWorkPlace.Start(0);
                        break;
                    default:
                        if( _cpuZ80.Halt ) 
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
            _cpuZ80.Reset();
            Doc_Asm.HighLight = null;
            Doc_Display.Clear();
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
        readonly Editor.Manipulator    _oBulkOutline;
        readonly SortedSet<int>        _rgOutlineLabels = new();
        readonly StringBuilder         _sbBuilder       = new();
        readonly StringBuilder         _sbData          = new();
        readonly Z80Memory             _rgRam;
        readonly Z80Definitions        _oZ80Info;
        readonly IEnumerable<Row>      _oAsmEnu;
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
            Editor                oOutDoc,
            AsmEditor             oAsmDoc,
            Action<string,string> fnLogError
        ) {
            _rgRam        = rgMemory     ?? throw new ArgumentNullException();
            _oZ80Info     = oDefinitions ?? throw new ArgumentNullException(); 
            _oAsmEnu      = oAsmDoc      ?? throw new ArgumentNullException();
            _fnLogError   = fnLogError   ?? throw new ArgumentNullException();

            _oBulkAsm     = new AsmEditor.Mangler( oAsmDoc );
            _oBulkOutline = oOutDoc.CreateManipulator();
        }

        public void Dispose() {
            _oBulkAsm    .Dispose();
            _oBulkOutline.Dispose();
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

                if( sInstr.Number == null && sInstr.Length > 1 ) {
                    _fnLogError( "Dissembler", "Inconsistant z80 instr" );
                    return;
                }

                if( sInstr.Number != null ) {
                    Line oLine = new TextLine( 0, sInstr.Params );
                    // Append the number
                    string strNumber = iNumber.ToString( sInstr.Length == 3 ? "X4" : "X2" );

                    if( !oLine.TryReplace( sInstr.Number.Offset, sInstr.Number.Length, strNumber ) ) {
                        _fnLogError( "Dissembler", "Unable to replace number arg in z80 instr" );
                        return;
                    }

                    oNewRow = _oBulkAsm.Append( sInstr.Name.ToUpper(), oLine.ToString() );

                    int iColorIndex;
                    if( sInstr.Jump == JumpType.None ) {
                        iColorIndex = 2;
                    } else {
                        iColorIndex = 1;
                    }

                    // Color the number
                    if( oNewRow is AsmRow oAsm ) {
                        oAsm.Param.Formatting.Add( 
                            new HyperLinkCpuJump( sInstr.Number.Offset,
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
                    oNewRow = _oBulkAsm.Append( sInstr.Name.ToUpper(), sInstr.Params );
                    if( string.Compare( "rst", sInstr.Name ) == 0 ) {
                        iNumber = int.Parse( sInstr.Params, System.Globalization.NumberStyles.HexNumber );
                        if( oNewRow is AsmRow oAsm ) {
                            oAsm.Param.Formatting.Add( new HyperLinkCpuJump( 0, sInstr.Params.Length, 1 ) ); // Hacky...
                            _rgOutlineLabels.Add( iNumber );
                        }
                    }
                }

                if( oNewRow is AsmRow oAsmRow ) {
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

        protected Z80Instr FindInfo( int iAddr ) {
            Z80Instr? sInstr;
             
            // BUG: hard coded for "tinybasic", just an experiment
            //if( iAddr >= 0x6A1 || ( iAddr >= 0xa6 && iAddr < 0xba ) ) {
            // BUG: hard coded for "kscope"
            //if( iAddr >= 0x196 ) {

            //if( iAddr >= 0x196 ) {
            //    sInstr = new Z80Instr( _rgRam[iAddr ] );
            //} else {
                sInstr = _oZ80Info.FindMain( _rgRam[iAddr] );
//            }

            return sInstr.Value;
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

            // Take all the labels and stick them in the outline.
            foreach( int i in _rgOutlineLabels ) {
                _oBulkOutline.LineAppend( i.ToString( "X" ) );

                // Go thru the assembler and update the address
                // column entry if that row is a jump target.
                foreach( Row oRow in _oAsmEnu ) {
                    if( oRow is AsmRow oAsmRow &&
                        oAsmRow.AddressMap == i )
                    {
                        if( oAsmRow.Label != null ) {
                            oAsmRow.Label.TryReplace( i.ToString( "X" ) );
                        } else {
                            // Spew an error
                        }
                    }
                }
            }
        } // end method
    } // end class
}
