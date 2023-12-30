using System.Text;
using System.Reflection;
using System.Text.RegularExpressions;

using Play.Interfaces.Embedding;
using Play.Edit; 

using z80;
using Play.Parse;

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
        public Z80Instr( string strName, JumpType eLabel = JumpType.None ) {
            Instr   = 0;
            Name    = strName ?? throw new ArgumentNullException();
            Length  = 1;
            Z80Type = Z80Types.Instruction;
            Jump    = eLabel;
        }
        public Z80Instr( byte bData ) {
            Instr   = bData;
            Name    = string.Empty;
            Length  = 1;
            Z80Type = Z80Types.Data;
            Jump    = JumpType.None;
        }

        public string   Name { get; }
        public byte     Instr { get; set; }
        public int      Length { get; set; }
        public Z80Types Z80Type { get; set; }
        public JumpType Jump { get; set; }

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
            _rgMain[0x01] = new Z80Instr( "ld bc {nn}" );
            _rgMain[0x02] = new Z80Instr( "ld(bc), a" );
            _rgMain[0x03] = new Z80Instr( "inc bc" );
            _rgMain[0x04] = new Z80Instr( "inc b" );
            _rgMain[0x05] = new Z80Instr( "dec b" );
            _rgMain[0x06] = new Z80Instr( "ld b, {n}" );
            _rgMain[0x07] = new Z80Instr( "rlca" );
            _rgMain[0x08] = new Z80Instr( "ex af, af'" );
            _rgMain[0x09] = new Z80Instr( "add hl, bc" );
            _rgMain[0x0a] = new Z80Instr( "ld a, (bc)" );
            _rgMain[0x0b] = new Z80Instr( "dec bc" );
            _rgMain[0x0c] = new Z80Instr( "inc c" );
            _rgMain[0x0d] = new Z80Instr( "dec c" );
            _rgMain[0x0e] = new Z80Instr( "ld c, {n}" );
            _rgMain[0x0f] = new Z80Instr( "rrca" );

            _rgMain[0x10] = new Z80Instr("djnz {d}"); // jump to n + pc
            _rgMain[0x11] = new Z80Instr("ld de, {nn}");
            _rgMain[0x12] = new Z80Instr("ld (de), a");
            _rgMain[0x13] = new Z80Instr("inc de");
            _rgMain[0x14] = new Z80Instr("inc e");
            _rgMain[0x15] = new Z80Instr("dec d");
            _rgMain[0x16] = new Z80Instr("ld d, {n}");
            _rgMain[0x17] = new Z80Instr("rla");
            _rgMain[0x18] = new Z80Instr("jr {d}" );
            _rgMain[0x19] = new Z80Instr("add hl, de");
            _rgMain[0x1a] = new Z80Instr("ld a, (de)");
            _rgMain[0x1b] = new Z80Instr("dec de");
            _rgMain[0x1c] = new Z80Instr("inc e");
            _rgMain[0x1d] = new Z80Instr("dec e");
            _rgMain[0x1e] = new Z80Instr("ld e, {n}");
            _rgMain[0x1f] = new Z80Instr("rra");

            _rgMain[0x20] = new Z80Instr("jr nz, d");
            _rgMain[0x21] = new Z80Instr("ld hl, {nn}");
            _rgMain[0x22] = new Z80Instr("ld ({nn}), hl");
            _rgMain[0x23] = new Z80Instr("inc hl");
            _rgMain[0x24] = new Z80Instr("inc h");
            _rgMain[0x25] = new Z80Instr("dec h");
            _rgMain[0x26] = new Z80Instr("ld h, {n}");
            _rgMain[0x27] = new Z80Instr("daa");
            _rgMain[0x28] = new Z80Instr("jr z, {d}");
            _rgMain[0x29] = new Z80Instr("add hl, hl");
            _rgMain[0x2a] = new Z80Instr("ld hl, ({nn})");
            _rgMain[0x2b] = new Z80Instr("dec hl");
            _rgMain[0x2c] = new Z80Instr("inc l");
            _rgMain[0x2d] = new Z80Instr("dec l");
            _rgMain[0x2e] = new Z80Instr("ld l, {n}");
            _rgMain[0x2f] = new Z80Instr("cpl");

            _rgMain[0x30] = new Z80Instr("jr nc, {d}" );
            _rgMain[0x31] = new Z80Instr("ld sp, {nn}" );
            _rgMain[0x32] = new Z80Instr("ld ({nn}), a");
            _rgMain[0x33] = new Z80Instr("inc sp" );
            _rgMain[0x34] = new Z80Instr("inc (hl)" );
            _rgMain[0x35] = new Z80Instr("dec (hl)" );
            _rgMain[0x36] = new Z80Instr("ld (hl), {n}" );
            _rgMain[0x37] = new Z80Instr("scf" ); // set c flag
            _rgMain[0x38] = new Z80Instr("jr c, {d}");
            _rgMain[0x39] = new Z80Instr("add hl, sp" );
            _rgMain[0x3a] = new Z80Instr("ld a, ({nn})" );
            _rgMain[0x3b] = new Z80Instr("dec sp" );
            _rgMain[0x3c] = new Z80Instr("inc a" );
            _rgMain[0x3d] = new Z80Instr("dec a" );
            _rgMain[0x3e] = new Z80Instr("ld a, {n}" );
            _rgMain[0x3f] = new Z80Instr("ccf" ); // invert carry flag

            _rgMain[0x40] = new Z80Instr("ld b, b" );
            _rgMain[0x41] = new Z80Instr("ld b, c" );
            _rgMain[0x42] = new Z80Instr("ld b, d" );
            _rgMain[0x43] = new Z80Instr("ld b, e" );
            _rgMain[0x44] = new Z80Instr("ld b, h" );
            _rgMain[0x45] = new Z80Instr("ld b, l" );
            _rgMain[0x46] = new Z80Instr("ld b, (hl)" );
            _rgMain[0x47] = new Z80Instr("ld b, a" );
            _rgMain[0x48] = new Z80Instr("ld c, b" );
            _rgMain[0x49] = new Z80Instr("ld c, c" );
            _rgMain[0x4a] = new Z80Instr("ld c, d" );
            _rgMain[0x4b] = new Z80Instr("ld c, e" );
            _rgMain[0x4c] = new Z80Instr("ld c, h" );
            _rgMain[0x4d] = new Z80Instr("ld c, l" );
            _rgMain[0x4e] = new Z80Instr("ld c, (hl)" );
            _rgMain[0x4f] = new Z80Instr("ld c, a" );

            _rgMain[0x50] = new Z80Instr("ld d, b");
            _rgMain[0x51] = new Z80Instr("ld d, c");
            _rgMain[0x52] = new Z80Instr("ld d, d");
            _rgMain[0x53] = new Z80Instr("ld d, e");
            _rgMain[0x54] = new Z80Instr("ld d, h");
            _rgMain[0x55] = new Z80Instr("ld d, l");
            _rgMain[0x56] = new Z80Instr("ld d, (hl)");
            _rgMain[0x57] = new Z80Instr("ld d, a" );
            _rgMain[0x58] = new Z80Instr("ld e, b");
            _rgMain[0x59] = new Z80Instr("ld e, c");
            _rgMain[0x5a] = new Z80Instr("ld e, d");
            _rgMain[0x5b] = new Z80Instr("ld e, e");
            _rgMain[0x5c] = new Z80Instr("ld e, h");
            _rgMain[0x5d] = new Z80Instr("ld e, l");
            _rgMain[0x5e] = new Z80Instr("ld e, (hl)");
            _rgMain[0x5f] = new Z80Instr("ld e, a");

            _rgMain[0x60] = new Z80Instr("ld h, b");
            _rgMain[0x61] = new Z80Instr("ld h, c");
            _rgMain[0x62] = new Z80Instr("ld h, d");
            _rgMain[0x63] = new Z80Instr("ld h, e");
            _rgMain[0x64] = new Z80Instr("ld h, h");
            _rgMain[0x65] = new Z80Instr("ld h, l");
            _rgMain[0x66] = new Z80Instr("ld h, (hl)");
            _rgMain[0x67] = new Z80Instr("ld h, a");
            _rgMain[0x68] = new Z80Instr("ld l, b");
            _rgMain[0x69] = new Z80Instr("ld l, c");
            _rgMain[0x6a] = new Z80Instr("ld l, d");
            _rgMain[0x6b] = new Z80Instr("ld l, e");
            _rgMain[0x6c] = new Z80Instr("ld l, h");
            _rgMain[0x6d] = new Z80Instr("ld l, l");
            _rgMain[0x6e] = new Z80Instr("ld l, (hl)");
            _rgMain[0x6f] = new Z80Instr("ld l, a");

            _rgMain[0x70] = new Z80Instr("ld (hl), b");
            _rgMain[0x71] = new Z80Instr("ld (hl), c");
            _rgMain[0x72] = new Z80Instr("ld (hl), d");
            _rgMain[0x73] = new Z80Instr("ld (hl), e");
            _rgMain[0x74] = new Z80Instr("ld (hl), h");
            _rgMain[0x75] = new Z80Instr("ld (hl), l");
            _rgMain[0x76] = new Z80Instr("halt");
            _rgMain[0x77] = new Z80Instr("ld (hl), a");
            _rgMain[0x78] = new Z80Instr("ld a, b");
            _rgMain[0x79] = new Z80Instr("ld a, c");
            _rgMain[0x7a] = new Z80Instr("ld a, d");
            _rgMain[0x7b] = new Z80Instr("ld a, e");
            _rgMain[0x7c] = new Z80Instr("ld a, h");
            _rgMain[0x7d] = new Z80Instr("ld a, l");
            _rgMain[0x7e] = new Z80Instr("ld a, (hl)");
            _rgMain[0x7f] = new Z80Instr("ld a, a");

            _rgMain[0x80] = new Z80Instr("add a, b");
            _rgMain[0x81] = new Z80Instr("add a, c");
            _rgMain[0x82] = new Z80Instr("add a, d");
            _rgMain[0x83] = new Z80Instr("add a, e");
            _rgMain[0x84] = new Z80Instr("add a, h");
            _rgMain[0x85] = new Z80Instr("add a, l");
            _rgMain[0x86] = new Z80Instr("add a, (hl)");
            _rgMain[0x87] = new Z80Instr("add a, a" );
            _rgMain[0x88] = new Z80Instr("adc a, b");
            _rgMain[0x89] = new Z80Instr("adc a, c");
            _rgMain[0x8a] = new Z80Instr("adc a, d");
            _rgMain[0x8b] = new Z80Instr("adc a, e");
            _rgMain[0x8c] = new Z80Instr("adc a, h");
            _rgMain[0x8d] = new Z80Instr("adc a, l");
            _rgMain[0x8e] = new Z80Instr("adc a, (hl)");
            _rgMain[0x8f] = new Z80Instr("adc a, a");

            _rgMain[0x90] = new Z80Instr("sub b");
            _rgMain[0x91] = new Z80Instr("sub c");
            _rgMain[0x92] = new Z80Instr("sub d");
            _rgMain[0x93] = new Z80Instr("sub e");
            _rgMain[0x94] = new Z80Instr("sub h");
            _rgMain[0x95] = new Z80Instr("sub l");
            _rgMain[0x96] = new Z80Instr("sub (hl)");
            _rgMain[0x97] = new Z80Instr("sub a");
            _rgMain[0x98] = new Z80Instr("sbc a, b");
            _rgMain[0x99] = new Z80Instr("sbc a, c");
            _rgMain[0x9a] = new Z80Instr("sbc a, d");
            _rgMain[0x9b] = new Z80Instr("sbc a, e");
            _rgMain[0x9c] = new Z80Instr("sbc a, h");
            _rgMain[0x9d] = new Z80Instr("sbc a, l");
            _rgMain[0x9e] = new Z80Instr("sbc a, (hl)");
            _rgMain[0x9f] = new Z80Instr("sbc a, a");

            _rgMain[0xa0] = new Z80Instr("and b");
            _rgMain[0xa1] = new Z80Instr("and c");
            _rgMain[0xa2] = new Z80Instr("and d");
            _rgMain[0xa3] = new Z80Instr("and e");
            _rgMain[0xa4] = new Z80Instr("and h");
            _rgMain[0xa5] = new Z80Instr("and l");
            _rgMain[0xa6] = new Z80Instr("and (hl)");
            _rgMain[0xa7] = new Z80Instr("and a");
            _rgMain[0xa8] = new Z80Instr("xor b");
            _rgMain[0xa9] = new Z80Instr("xor c");
            _rgMain[0xaa] = new Z80Instr("xor d");
            _rgMain[0xab] = new Z80Instr("xor e");
            _rgMain[0xac] = new Z80Instr("xor h");
            _rgMain[0xad] = new Z80Instr("xor l");
            _rgMain[0xae] = new Z80Instr("xor (hl)");
            _rgMain[0xaf] = new Z80Instr("xor a");

            _rgMain[0xb0] = new Z80Instr("or b");
            _rgMain[0xb1] = new Z80Instr("or c");
            _rgMain[0xb2] = new Z80Instr("or d");
            _rgMain[0xb3] = new Z80Instr("or e");
            _rgMain[0xb4] = new Z80Instr("or h");
            _rgMain[0xb5] = new Z80Instr("or l");
            _rgMain[0xb6] = new Z80Instr("or (hl)");
            _rgMain[0xb7] = new Z80Instr("or a");
            _rgMain[0xb8] = new Z80Instr("cp b");
            _rgMain[0xb9] = new Z80Instr("cp c");
            _rgMain[0xba] = new Z80Instr("cp d");
            _rgMain[0xbb] = new Z80Instr("cp e");
            _rgMain[0xbc] = new Z80Instr("cp h");
            _rgMain[0xbd] = new Z80Instr("cp l");
            _rgMain[0xbe] = new Z80Instr("cp (hl)");
            _rgMain[0xbf] = new Z80Instr("cp a");

            _rgMain[0xc0] = new Z80Instr("ret nz" );
            _rgMain[0xc1] = new Z80Instr("pop bc" );
            _rgMain[0xc2] = new Z80Instr("jp nz, {nn}" );
            _rgMain[0xc3] = new Z80Instr("jp {nn}" );
            _rgMain[0xc4] = new Z80Instr("call nz, {nn}" );
            _rgMain[0xc5] = new Z80Instr("push bc" );
            _rgMain[0xc6] = new Z80Instr("add a, {n}" );
            _rgMain[0xc7] = new Z80Instr("rst 00h" );
            _rgMain[0xc8] = new Z80Instr("ret z" );
            _rgMain[0xc9] = new Z80Instr("ret" );
            _rgMain[0xca] = new Z80Instr("jp z, {nn}" );
            _rgMain[0xcb] = new Z80Instr("Bit" );
            _rgMain[0xcc] = new Z80Instr("call z, {nn}" );
            _rgMain[0xcd] = new Z80Instr("call {nn}" );
            _rgMain[0xce] = new Z80Instr("adc a, {n}" );
            _rgMain[0xcf] = new Z80Instr("rst 08h" );

            _rgMain[0xd0] = new Z80Instr("ret nc" );
            _rgMain[0xd1] = new Z80Instr("pop de" );
            _rgMain[0xd2] = new Z80Instr("jp nc, {nn}" );
            _rgMain[0xd3] = new Z80Instr("out port({n}), a" );
            _rgMain[0xd4] = new Z80Instr("call nc, {nn}" );
            _rgMain[0xd5] = new Z80Instr("push de" );
            _rgMain[0xd6] = new Z80Instr("sub {n}" );
            _rgMain[0xd7] = new Z80Instr("rst 10h" );
            _rgMain[0xd8] = new Z80Instr("ret c" );
            _rgMain[0xd9] = new Z80Instr("exx" );
            _rgMain[0xda] = new Z80Instr("jmp c, {nn}" );
            _rgMain[0xdb] = new Z80Instr("in a, port({n})" );
            _rgMain[0xdc] = new Z80Instr("call c, {nn}" );
            _rgMain[0xdd] = new Z80Instr("->ix" );
            _rgMain[0xde] = new Z80Instr("sbc a, {n}" );
            _rgMain[0xdf] = new Z80Instr("rst 18h" );

            _rgMain[0xe0] = new Z80Instr("ret po unset" );
            _rgMain[0xe1] = new Z80Instr("pop hl" );
            _rgMain[0xe2] = new Z80Instr("jp po unset, {nn}" );
            _rgMain[0xe3] = new Z80Instr("ex (sp), hl" );
            _rgMain[0xe4] = new Z80Instr("call po unset, {nn}" );
            _rgMain[0xe5] = new Z80Instr("push hl" );
            _rgMain[0xe6] = new Z80Instr("and {n}" );
            _rgMain[0xe7] = new Z80Instr("rst 20h" );
            _rgMain[0xe8] = new Z80Instr("ret pe" );
            _rgMain[0xe9] = new Z80Instr("jp (hl)" );
            _rgMain[0xea] = new Z80Instr("jp pe set, {nn}" );
            _rgMain[0xeb] = new Z80Instr("ex de, hl" );
            _rgMain[0xec] = new Z80Instr("call pe, {nn}" );
            _rgMain[0xed] = new Z80Instr("Misc." );
            _rgMain[0xee] = new Z80Instr("xor {n}" );
            _rgMain[0xef] = new Z80Instr("rst 28h" );

            _rgMain[0xf0] = new Z80Instr("ret p");
            _rgMain[0xf1] = new Z80Instr("pop af");
            _rgMain[0xf2] = new Z80Instr("jp pc, {nn}");
            _rgMain[0xf3] = new Z80Instr("di");
            _rgMain[0xf4] = new Z80Instr("call pc, {nn}");
            _rgMain[0xf5] = new Z80Instr("push af");
            _rgMain[0xf6] = new Z80Instr("or {n}");
            _rgMain[0xf7] = new Z80Instr("rst 30h");
            _rgMain[0xf8] = new Z80Instr("ret m");
            _rgMain[0xf9] = new Z80Instr("ld sp, hl");
            _rgMain[0xfa] = new Z80Instr("jp m, {nn}");
            _rgMain[0xfb] = new Z80Instr("ei");
            _rgMain[0xfc] = new Z80Instr("call m, {nn}");
            _rgMain[0xfd] = new Z80Instr("IY");
            _rgMain[0xfe] = new Z80Instr("cp {n}");
            _rgMain[0xff] = new Z80Instr("rst 38h");

            InitNew();
        }

        public Z80Instr FindMain( int iIndex ) {
            return _rgMain[iIndex];
        }

        private void InitNew() {
            Regex oReg = new Regex("{n+}|{d+}", RegexOptions.IgnoreCase);

            for( int i=0; i<_rgMain.Length; i++ ) {
                _rgMain[i].Instr = (byte)i;
                Match oMatch = oReg.Match( _rgMain[i].Name );
                if( oMatch != null && oMatch.Success ) {
                    if( oMatch.Groups.Count > 1 ) 
                        throw new InvalidDataException("Unexpected z80 instruction" );

                    if( _rgMain[i].Name.StartsWith( "jp" ) ||
                        _rgMain[i].Name.StartsWith( "jr" ) ||
                        _rgMain[i].Name.StartsWith( "call" ) ) 
                    {
                        switch( _rgMain[i].Name[oMatch.Index+1] ) { // ignor first {
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

                    int iCount = oMatch.Length - 2; // Subtract {}
                    _rgMain[i].Length = iCount + 1;
                }
                
            }
        }
    }

    public class AssemblyEditor : BaseEditor {

        /// <remarks>
        /// It would be nice if we only allocate a comment line if we actually
        /// need it. Need to think about that for the future.
        /// </remarks>
        public class AsmLine : TextLine {
            public Line LineComment { get; }
            public AsmLine( int iLine, string strValue ) :
                base( iLine, strValue )
            {
                Extra       = new TextLine( 0, string.Empty ); // memory address display.
                LineComment = new TextLine( 0, string.Empty ); // Comment, needed or not.
            }
        }
        public AssemblyEditor(IPgBaseSite oSite) : base(oSite) {
        }
        protected override Line CreateLine( int iLine, string strValue )
        {
            return new AsmLine( iLine, strValue );
        }

        /// <summary>
        /// This is my new experiment for extra data on the line. Later
        /// I'll add a multi column line structure. But now I'll just
        /// query for our expected Line subclass!!
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public override Line GetLine( int iLine, int iColumn = 0 ) {
            Line oLine = base.GetLine( iLine, iColumn );

            if( iColumn == 0 )
                return oLine;

            if( iColumn == 1 && oLine is AsmLine oAsmLine )
                return oAsmLine.LineComment;

            throw new ArgumentOutOfRangeException();
        }
    }
    internal class Document_Monitor :
        IPgParent,
		IDisposable,
        IPgLoad<TextReader>,
        IPgSave<TextWriter>
    {
        protected readonly IPgBaseSite _oBaseSite;
        public IPgParent Parentage => _oBaseSite.Host;
        public IPgParent Services  => Parentage.Services;

        public AssemblyEditor Doc_Asm   { get; }
        public Editor Doc_Displ { get; }
        public Editor Doc_Outl  { get; }

        Z80Memory? _rgMemory = null;

        public bool IsDirty => Doc_Asm.IsDirty;

        public class DocSlot :
            IPgBaseSite
        {
            protected readonly Document_Monitor _oHost;

            public DocSlot( Document_Monitor oHost ) {
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

        protected Z80Definitions _oZ80Info = new();
        public Document_Monitor( IPgBaseSite oBaseSite ) {
            _oBaseSite = oBaseSite ?? throw new ArgumentNullException();

            Doc_Asm   = new ( new DocSlot( this ) );
            Doc_Displ = new ( new DocSlot( this ) );
            Doc_Outl  = new ( new DocSlot( this ) );
        }

        protected void LogError( string strLabel, string strMessage ) {
            _oBaseSite.LogError( strLabel, strMessage );
        }

        public void Dispose() {
        }

        private bool LoadMe() {
            List<byte> rgBytes = new List<byte>();

            rgBytes.Add( 0x31 ); // sp, nn
            rgBytes.Add( 0x00 );
            rgBytes.Add( 0x10 );

            rgBytes.Add( 0xc3 ); // jp nn

            int iStart_Patch = rgBytes.Count;

            rgBytes.Add( 0x00 );
            rgBytes.Add( 0x00 );

            int iHello = rgBytes.Count;

            rgBytes.Add( (byte)'h' );
            rgBytes.Add( (byte)'e' );
            rgBytes.Add( (byte)'l' );
            rgBytes.Add( (byte)'l' );
            rgBytes.Add( (byte)'o' );
            rgBytes.Add( 0x00 );

            int iStart = rgBytes.Count;
            rgBytes[iStart_Patch] = (byte)iStart; // just set low byte.

            rgBytes.Add( 0x21 );  // ld hl, nn
            rgBytes.Add( (byte)iHello );
            rgBytes.Add( 0x00 );

            int iLoop = rgBytes.Count;

            rgBytes.Add( 0x7e ); // ld a (hl)
            rgBytes.Add( 0xfe ); // cmp 0
            rgBytes.Add( 0x00 );
            rgBytes.Add( 0xca ); // C2 jmp nz nn; CA-> JMP Z NN
            rgBytes.Add( (byte)iStart );
            rgBytes.Add( 0x00 );
            rgBytes.Add( 0xd3 ); // Out, n
            rgBytes.Add( 0x03 );
            rgBytes.Add( 0x23 ); // inc hl
            rgBytes.Add( 0xc3 ); // jp nn
            rgBytes.Add( (byte)iLoop );
            rgBytes.Add( 0x00 );

            _rgMemory = new Z80Memory( new byte[rgBytes.Count], 0x0000 );

            for( int i =0; i<rgBytes.Count; ++i ) {
                _rgMemory[i] = rgBytes[i];
            }

            return true;
        }
        protected bool LoadTiny() {
            try {
                Assembly   oAssembly = Assembly.GetExecutingAssembly();
                string?   strAsmName = oAssembly.GetName().Name;
			    using Stream oStream = oAssembly.GetManifestResourceStream( strAsmName + ".tinybasic2dms.bin" );

                if( oStream == null )
                    return false;

                // This isn't necessarily the z80 emulator memory. Let's
                // see how this turns out. We can get the memory size needed by
                // reading the first "ld sp, nn" instruction.;
                byte[] rgRWRam = new byte[4096];
                int    iCount  = 0;

                for( int iByte = oStream.ReadByte();
                     iByte != -1;
                     iByte = oStream.ReadByte() ) 
                {
                    rgRWRam[iCount++] = (byte)iByte;
                }
                _rgMemory = new Z80Memory( rgRWRam, (ushort)iCount );
            } catch( NullReferenceException ) {
                return false;
            }

            return true;
        }

        public bool Load(TextReader oStream) {
            if( !Doc_Asm.Load( oStream ) )
                return false;

            if( !Doc_Displ.InitNew() )
                return false;

            if( !Doc_Outl.InitNew() )
                return false;

            return true;
        }

        public bool InitNew() {
            if( !Doc_Asm.InitNew() )
                return false;

            if( !Doc_Displ.InitNew() )
                return false;

            if( !Doc_Outl.InitNew() )
                return false;

            if( LoadTiny() ) {
                Dissassemble();
            }

            return true;
        }

        public bool Save(TextWriter oStream) {
            return Doc_Asm.Save( oStream );
        }

        public Z80Instr IdentifyMemory( int iAddr ) {
            if( _rgMemory == null )
                throw new InvalidProgramException( "Expected Ram Reference" );

            Z80Instr sInstr = _oZ80Info.FindMain( _rgMemory[iAddr] );

            return sInstr;
        }

        protected class Dissambler : 
            IDisposable
        {
            readonly Document_Monitor    _oDoc;
            readonly Editor.Manipulator  _oBulkAsm;
            readonly Editor.Manipulator  _oBulkOutline;
            readonly SortedSet<int>      _rgOutlineLabels = new();
            readonly Regex               _oRegEx          = new("{n+}|{d+}", RegexOptions.IgnoreCase);
            readonly StringBuilder       _sbBuilder       = new();
            readonly StringBuilder       _sbData          = new();
            readonly LinkedList<AsmData> _rgAsmData       = new();
            readonly Z80Memory           _rgRam;
            readonly Z80Definitions      _oZ80Info;

            struct AsmData {
                public byte _bData;
                public int  _iAddr;

                public AsmData( byte bDatam, int iAddr ) {
                    _bData = bDatam;
                    _iAddr = iAddr;
                }
            }

            public Dissambler( Document_Monitor oDoc ) {
                _oDoc     = oDoc           ?? throw new ArgumentNullException();
                _rgRam    = oDoc._rgMemory ?? throw new ArgumentNullException();
                _oZ80Info = oDoc._oZ80Info ?? throw new ArgumentNullException(); 

                _oBulkAsm     = oDoc.Doc_Asm .CreateManipulator();
                _oBulkOutline = oDoc.Doc_Outl.CreateManipulator();
            }

            public void Dispose() {
                _oBulkAsm    .Dispose();
                _oBulkOutline.Dispose();

                _oDoc.Doc_Asm .ClearDirty();
                _oDoc.Doc_Outl.ClearDirty();
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

            protected void ProcessInstruction(Z80Instr sInstr, int iAddr ) {
                _sbBuilder.Clear();

                Match oMatch   = _oRegEx.Match( sInstr.Name );
                Line? oAsmLine;

                if( oMatch != null && oMatch.Success ) {
                    int iNumber = 0;

                    switch( sInstr.Length ) {
                        case 0:
                            throw new InvalidDataException("Problem with z80 instr table" );
                        case 2:
                            iNumber = _rgRam[iAddr+1];
                            break;
                        case 3:
                            iNumber = _rgRam[iAddr+1] + _rgRam[iAddr+2] * 0x0100;
                            break;
                    }

                    // Append the instruction
                    for( int i = 0; i<oMatch.Index; i++ ) {
                        _sbBuilder.Append( sInstr.Name[i] );
                    }
                    // Append the number
                    string strNumber = iNumber.ToString( "X2" );
                    _sbBuilder.Append( strNumber );
                    // Append everything after the number.
                    for( int i = oMatch.Index + oMatch.Length; i<sInstr.Name.Length; i++ ) {
                        _sbBuilder.Append( sInstr.Name[i] );
                    }
                    oAsmLine = _oBulkAsm.LineAppend( _sbBuilder.ToString() );

                    int? iColorIndex;
                    if( sInstr.Jump != JumpType.None ) {
                        iColorIndex = 1;
                    } else {
                        iColorIndex = 2;
                    }

                    // Color the number
                    oAsmLine.Formatting.Add( 
                        new HyperLinkCpuJump( oMatch.Index,
                                              strNumber.Length,
                                              iColorIndex.Value ) );

                    if( !_rgOutlineLabels.Contains( iNumber ) ) {
                        if( sInstr.Jump == JumpType.Abs )
                            _rgOutlineLabels.Add( iNumber );
                        if( sInstr.Jump == JumpType.Rel )
                            _rgOutlineLabels.Add( iNumber + iAddr ); // +1, +2??
                    }
                } else {
                    oAsmLine = _oBulkAsm.LineAppend( sInstr.Name );
                }

                if( oAsmLine.Extra is TextLine oAddrLine ) {
                    oAddrLine.TryReplace( 0, oAddrLine.ElementCount, iAddr.ToString( "X4" ) );
                }
            }

            protected Z80Instr FindInfo( int iAddr ) {
                Z80Instr? sInstr;
                    
                if( iAddr < 0x56c ) {
                    sInstr = _oZ80Info.FindMain( _rgRam[iAddr] );
                } else {
                    sInstr = new Z80Instr( _rgRam[iAddr ] );
                }

                return sInstr.Value;
            }

            protected void WriteDataLn( int iColumnCount ) {
                if( _rgAsmData.First == null )
                    return;

                _sbData.Clear();
                string strAddr = _rgAsmData.First.Value._iAddr.ToString( "X4" );

                int iCount = 0;
                while( _rgAsmData.First != null && iCount++ < 20 ) {
                    byte bData = _rgAsmData.First.Value._bData;

                    if( bData < 0x20 || bData > 0x80 ) 
                    {
                        _sbData.Append( bData.ToString( "X2" ) );
                        _sbData.Append( ' ' );
                    } else {
                        _sbData.Append( ' ' );
                        _sbData.Append( (char)bData );
                        _sbData.Append( ' ' );
                    }
                    _rgAsmData.RemoveFirst();
                }
                Line oData = _oBulkAsm.LineAppend( _sbData.ToString() );
                if( oData.Extra is TextLine oAddrLine ) {
                    oAddrLine.TryReplace( 0, oAddrLine.ElementCount, strAddr );
                }
            }

            public void Dissassemble() {
                int       iAddr    = 0; 
                const int iColumns = 20;

                // Decode only the ROM section of our given memory.
                while( iAddr < _rgRam.RamStart ) {
                    Z80Instr sInstr = FindInfo( iAddr );

                    int iLineCount = _oDoc.Doc_Asm.ElementCount;
                    if( iLineCount % 10 == 1 ) {
                        Line oLine = _oDoc.Doc_Asm.GetLine( iLineCount - 1, 1 );

                        oLine.TryAppend( "Hello " + iLineCount.ToString() );
                    }

                    switch( sInstr.Z80Type ) {
                        case Z80Types.Instruction:
                            WriteDataLn( iColumns );

                            if( string.IsNullOrEmpty( sInstr.Name ) ) {
                                // Just put the instruction number and bail.
                                _oBulkAsm.LineAppend( _rgRam[iAddr].ToString() );
                                break;
                            }
                            ProcessInstruction( sInstr, iAddr );
                            break;
                        case Z80Types.Data:
                            _rgAsmData.AddLast( new AsmData(_rgRam[iAddr],iAddr ) );

                            if( _rgAsmData.Count >= iColumns ) {
                                WriteDataLn( iColumns );
                            }
                            break;
                    }
                    iAddr += sInstr.Length;
                }

                WriteDataLn( iColumns );

                // Take all the labels and stick them in the outline.
                foreach( int i in _rgOutlineLabels ) {
                    _oBulkOutline.LineAppend( i.ToString( "X" ) );
                }
            }
        }

        public void Dissassemble() {
            if( _rgMemory == null ) {
                LogError( "Error", "Load a binary first." );
                return;
            }

            using Dissambler oTool = new( this );

            oTool.Dissassemble();
        }
    }
}
