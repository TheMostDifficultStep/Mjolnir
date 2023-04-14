using System.Collections;
using System.Collections.Generic;
using System.Text;

using Play.Interfaces.Embedding;
using Play.Edit; 
using Play.Forms;
using Play.Integration;
using Play.Parse;
using Play.Parse.Impl;
using System.Drawing;

namespace Monitor {
    public class MonitorController : Controller {
        public MonitorController() {
            _rgExtensions.Add( ".nibble" );
        }
        public override IDisposable CreateDocument(IPgBaseSite oSite, string strExtension) {
            return new MonitorDocument( oSite );
        }

        public override IDisposable CreateView(IPgViewSite oViewSite, object oDocument, Guid guidViewType) {
            if( oDocument is MonitorDocument oMonitorDoc ) {
			    try {
				    if( guidViewType == Guid.Empty )
					    return new WindowFrontPanel( oViewSite, oMonitorDoc );

				    return new WindowFrontPanel( oViewSite, oMonitorDoc );
                } catch( Exception oEx ) {
                    Type[] rgErrors = { typeof( NullReferenceException ),
                                        typeof( InvalidCastException ),
                                        typeof( ArgumentNullException ),
									    typeof( ArgumentException ) };
                    if( rgErrors.IsUnhandled( oEx ) )
                        throw;
                }
            }

			throw new InvalidOperationException( "Controller couldn't create view for Monitor document." );
        }

        public override IEnumerator<IPgViewType> GetEnumerator() {
 	        yield return new ViewType( "MainView", Guid.Empty );
        }
    }

    public enum StatusReg : int {
        Negative,
        Zero,
        Carry,
        Overflow
    }

    public enum AddrModes {
        Acc, // Non-Indexed,non memory
        Imm,
        Imp,

        Rel, // Non-Indexed memory ops
        Abs,
        Zpg,
        Ind,

        Abs_Indx, // Indexed memory ops
        Zpg_Indx,
        Indx_Indr,
        Indr_Indx
    }

    public class AsmInstruction {
        public readonly string    _strInst;
        public readonly AddrModes _eMode;
        public readonly bool      _fIndexed;
        public readonly bool      _fMemory;

        public AsmInstruction( string strInst, AddrModes eMode, bool fIndexed, bool fMemory ) {
            _strInst  = strInst;
            _eMode    = eMode;
            _fIndexed = fIndexed;
            _fMemory  = fMemory;
        }

        public override string ToString() {
            return _strInst + '-' + _eMode.ToString();
        }
    }

    public class MonitorDocument :
        IPgParent,
		IDisposable,
        IPgLoad<TextReader>,
        IPgSave<TextWriter>
    {
        protected readonly IPgBaseSite _oBaseSite;
        protected Dictionary< string, Action> _dctInstructions = new();
        protected Dictionary< string, int >   _dctStatusNames  = new();
        protected Dictionary< int, int >      _dctPowerOfTwo   = new();

        protected readonly Grammer<char> _oGrammer;
        public bool IsDirty => false;

        public IPgParent Parentage => _oBaseSite.Host;
        public IPgParent Services  => Parentage.Services;

        public Editor        TextCommands { get; }
        public Editor        AssemblyDoc  { get; }

        public DocProperties FrontDisplay { get; }
        public List<Line>    StatusLine   { get; } = new();
        public Editor        LablEdit     { get; }
        public List<Line>    Registers    { get; } = new();

        protected readonly Dictionary<string, List<AsmInstruction>> _rgInstr = new();

        public enum StatusBits : int {
            Overflow = 0, 
            Carry,
            Zero,
            Negative,
            s4,
            s5,
            s6,
            s7
        }

        public enum Arithmetic {
            Add,
            Mult,
            Sub,
            Div
        }

        public event Action<int> RefreshScreen;

        public class DocSlot :
            IPgBaseSite
        {
            protected readonly MonitorDocument _oHost;

            public DocSlot( MonitorDocument oHost ) {
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

        public class ProgramFile : Editor {
            public ProgramFile( IPgBaseSite oBaseSite ) : base( oBaseSite ) { }

            public override WorkerStatus PlayStatus => ( _oSiteBase.Host as MonitorDocument ).PlayStatus;
        }

        /// <summary>
        /// This would really benefit from having a HALT flag on the cpu.
        /// </summary>
        public WorkerStatus PlayStatus { 
            get { 
                //if( _iCurrent == 0 )
                //    return WorkerStatus.FREE; 
                //if( _iCurrent < TextCommands.ElementCount )
                    return WorkerStatus.BUSY; 

                // return WorkerStatus.PAUSED;
            } 
        }
        public MonitorDocument( IPgBaseSite oSite ) {
            _oBaseSite = oSite ?? throw new ArgumentNullException( "Site to document must not be null." );

            TextCommands = new ProgramFile  ( new DocSlot( this ) );
            AssemblyDoc  = new Editor       ( new DocSlot( this ) );
            FrontDisplay = new DocProperties( new DocSlot( this ) );
            LablEdit     = new Editor       ( new DocSlot( this ) );

            _dctInstructions.Add( "load-imm", Inst_LoadImm ); // load, reg, data (lda, ldx, ldy)
            _dctInstructions.Add( "load-abs", Inst_LoadAbs ); // load, reg, addr of data.
            _dctInstructions.Add( "save",     Inst_Save ); // save, reg, address to save to. (sta, stx, sty)
            _dctInstructions.Add( "halt",     Inst_Halt );
            _dctInstructions.Add( "jump-imm", Inst_JumpImm ); // unconditional jump.
            _dctInstructions.Add( "comp-abs", Inst_CompAbs ); // (cmp {a}, cpx, cpy)
            _dctInstructions.Add( "comp-imm", Inst_CompImm);  // (cmp {a}, cpx, cpy)
            _dctInstructions.Add( "brat-imm", Inst_BranchTrueImm ); // branch true, flag, addr;
            _dctInstructions.Add( "braf-imm", Inst_BranchFalseImm ); // branch false, flag, addr;
            _dctInstructions.Add( "incr",     Inst_Increment );
            _dctInstructions.Add( "decr",     Inst_Decrement );
            _dctInstructions.Add( "addi",     Inst_Add );
            _dctInstructions.Add( "addf",     Inst_AddFloat );
            _dctInstructions.Add( "muli",     Inst_Multiply );
            _dctInstructions.Add( "mulf",     Inst_MultiplyFloat );
            _dctInstructions.Add( "subi",     Inst_Subtract );
            _dctInstructions.Add( "subf",     Inst_SubtractFloat );
            _dctInstructions.Add( "divi",     Inst_Divide );
            _dctInstructions.Add( "divf",     Inst_DivideFloat );
            _dctInstructions.Add( "move",     Inst_MoveReg );

            _dctStatusNames.Add( "zero",     (int)StatusBits.Zero );
            _dctStatusNames.Add( "carry",    (int)StatusBits.Carry );
            _dctStatusNames.Add( "neg",      (int)StatusBits.Negative );
            _dctStatusNames.Add( "negative", (int)StatusBits.Negative );

            // This is 6502 flag positions, Don't presently mach my status lines.
            _dctPowerOfTwo.Add(   1, 0 ); // carry
            _dctPowerOfTwo.Add(   2, 1 ); // zero
            _dctPowerOfTwo.Add(   4, 2 ); // int disable
            _dctPowerOfTwo.Add(   8, 3 ); // decimal
            _dctPowerOfTwo.Add(  16, 4 ); // break was triggered
            _dctPowerOfTwo.Add(  32, 5 ); // unused.
            _dctPowerOfTwo.Add(  64, 6 ); // overflow
            _dctPowerOfTwo.Add( 128, 7 ); // negative.

			try {
				// A parser is matched one per text document we are loading.
				ParseHandlerText oParser = new ParseHandlerText( AssemblyDoc, "asm" );
                _oGrammer = oParser.Grammer;
			} catch( Exception oEx ) {
                Type[] rgErrors = { typeof( NullReferenceException ),
                                    typeof( InvalidCastException ),
                                    typeof( ArgumentNullException ),
									typeof( ArgumentException ),
									typeof( InvalidOperationException ),
									typeof( InvalidProgramException ) };
                if( rgErrors.IsUnhandled( oEx ) )
                    throw;

				throw new InvalidOperationException( "Couldn't create parse handler for given text.", oEx );
			}
        }

        // See ca 1816 warning.
        public void Dispose() {
        }

        public bool Initialize() {
            if( !FrontDisplay.InitNew() )
                return false;

            if( !LablEdit.InitNew() )
                return false;

            Editor PropValues = FrontDisplay.PropertyDoc;
            for( int i = 0; i<8; ++i ) {
                StatusLine.Add( PropValues.LineAppend( "0", fUndoable:false ) );
            }

            LablEdit.LineAppend( "Data",    fUndoable:false ); // 0
            LablEdit.LineAppend( "...",     fUndoable:false );
            LablEdit.LineAppend( "Status",  fUndoable:false );
            LablEdit.LineAppend( "",        fUndoable:false ); // 3
            LablEdit.LineAppend( "High",    fUndoable:false );
            LablEdit.LineAppend( "Low",     fUndoable:false );


            for( int iRegister = 0; iRegister < 6; ++iRegister ) {
                Registers.Add( PropValues.LineAppend( "0", fUndoable:false ) );
                switch( iRegister ) {
                    case 4:
                        LablEdit.LineAppend( "Stack   (" + iRegister.ToString() + ")", fUndoable:false );
                        break;
                    case 5:
                        LablEdit.LineAppend( "Program (" + iRegister.ToString() + ")", fUndoable:false );
                        break;
                    default:
                        LablEdit.LineAppend( "Register" + iRegister.ToString(), fUndoable:false );
                        break;
                }
            }

            LablEdit.LineAppend( "Negative", false ); // 12
            LablEdit.LineAppend( "Zero",     false );
            LablEdit.LineAppend( "Carry",    false );
            LablEdit.LineAppend( "Overflow", false );

            InitInstructions();

            return true;
        }

        public void InitInstructions() {
            Add( "load", new AsmInstruction( "ea", AddrModes.Imm, false, false ) );
            Add( "load", new AsmInstruction( "eb", AddrModes.Abs, false, true  ) );
            Add( "addi", new AsmInstruction( "cc", AddrModes.Imp, false, false ) );
            Add( "save", new AsmInstruction( "b0", AddrModes.Imp, false, true  ) );
            Add( "comp", new AsmInstruction( "a0", AddrModes.Abs, false, true  ) );
            Add( "brat", new AsmInstruction( "10", AddrModes.Imm, false, true  ) );
            Add( "halt", new AsmInstruction( "20", AddrModes.Imp, false, false ) );
        }

        protected void Add( string strName, AsmInstruction oInstr ) {
            if( _rgInstr.TryGetValue( strName, out List<AsmInstruction> rgOps  ) ) {
                rgOps.Add( oInstr );
            } else {
                List<AsmInstruction> rgNewOps = new ();
                rgNewOps.Add( oInstr );
                _rgInstr.Add( strName, rgNewOps );
            }
        }

        public bool InitNew() {
            if( !TextCommands.InitNew() )
                return false;

            if( !AssemblyDoc.InitNew() )
                return false;

            if( !Initialize() ) 
                return false;

            return true;
        }

        public bool Save(TextWriter oStream) {
            return TextCommands.Save( oStream );
        }

        public bool Load(TextReader oStream) {
            if( !TextCommands.Load( oStream ) )
                return false;

            if( !Initialize() ) 
                return false;

            return true;
        }



        public void CompileAsm() {
            TextCommands.Clear();

            // Let's look up all the bindings just once at the start!!
            State<char> oClassStatement = _oGrammer.FindState( "statement" );
            State<char> oClassParam     = _oGrammer.FindState( "param" );

            // Would be nice if checked if any -1.
            int iInstr = oClassStatement.Bindings.IndexOfKey( "instr" );
            int iParms = oClassStatement.Bindings.IndexOfKey( "params" );

            int iValue = oClassParam    .Bindings.IndexOfKey( "value" );
            int iHex   = oClassParam    .Bindings.IndexOfKey( "hex" );
            int iImm   = oClassParam    .Bindings.IndexOfKey( "immed" );

            BaseEditor.LineStream oAssmStream = AssemblyDoc.CreateStream();

            using( BaseEditor.Manipulator oBulk = TextCommands.CreateManipulator() ) {
                StringBuilder  oBuild = new();
                List<string> rgValues = new(3);

                foreach( Line oLine in AssemblyDoc ) {
                    oBuild  .Clear();
                    rgValues.Clear();
                    bool fImm = false;
                    foreach( IColorRange oRange in oLine.Formatting ) {
                        if( oRange is MemoryState<char> oState &&
                            oState.StateName == "statement"  )
                        {
                            // I want line offsets not stream offsets. We can do this since
                            // no datum (that we care about) will extend past one line.
                            IColorRange oInstr = oState.GetValue(iInstr);
                            string      sInstr = string.Empty;

                            if( oInstr != null ) {
                                sInstr = oLine.SubString( oInstr.Offset, oInstr.Length );
                            }
                            if( !string.IsNullOrEmpty( sInstr ) ) {
                                foreach( MemoryState<char> oParam in oState.EnumValues(iParms) ) {
                                    IColorRange oValue = oParam.GetValue( iValue );
                                    IColorRange oImm   = oParam.GetValue( iImm   );

                                    if( oValue != null ) {
                                        string strValue = oLine.SubString( oValue.Offset, oValue.Length );
                                        rgValues.Add( strValue );

                                        if( oImm != null && !fImm ) {
                                            fImm = true;
                                        }
                                    }
                                }
                            }

                            if( _rgInstr.TryGetValue( sInstr, out List<AsmInstruction> rgOps ) ) {
                                AsmInstruction oInstPick = null;
                                foreach( AsmInstruction oInst in rgOps ) {
                                    if( fImm == true ) {
                                        if( oInst._eMode == AddrModes.Imm ) {
                                            oInstPick = oInst;
                                            break;
                                        }
                                    } else {
                                        if( oInst._fMemory == true ) {
                                            oInstPick = oInst;
                                            break;
                                        }
                                    }
                                }

                                if( oInstPick != null ) {
                                    oBuild.Append( sInstr );
                                    switch( oInstPick._eMode ) {
                                        case AddrModes.Imm:
                                            oBuild.Append( "_imm" );
                                            break;
                                        case AddrModes.Abs:
                                            oBuild.Append( "_abs" );
                                            break;
                                    }

                                    oBulk.LineAppend( oBuild.ToString() );
                                    foreach( string strValue in rgValues ) {
                                        oBulk.LineAppend( strValue );
                                    }
                                }
                            } else {
                                _oBaseSite.LogError( "Parsing", "Unrecognized instruction, Line : " + oLine.At.ToString() );
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Need to think thru representations. Since now I have "floating point"
        /// numbers. Probably should just write whatever string is given.
        /// </summary>
        /// <param name="iRegister"></param>
        /// <param name="strData"></param>
        /// <exception cref="InvalidOperationException"></exception>
        protected void RegisterWrite( int iRegister, string strData ) {
            Line oRegister = Registers[iRegister];
            oRegister.Empty();
            oRegister.TryAppend( strData );
        }

        protected void RegisterWrite( int iRegister, int iData ) {
            Line oRegister = Registers[iRegister];
            oRegister.Empty();
            oRegister.TryAppend( iData.ToString() );
        }

        protected int RegisterRead( int iRegister ) {
            Line oRegister = Registers[iRegister];

            if( int.TryParse( oRegister.ToString(), out int iData ) ) {
                return iData;
            }

            _oBaseSite.LogError( "Program", "Illegal value in register" );
            throw new InvalidOperationException();
        }

        protected float RegisterReadFloat( int iRegister ) {
            Line oRegister = Registers[iRegister];

            if( float.TryParse( oRegister.ToString(), out float flData ) ) {
                return flData;
            }

            _oBaseSite.LogError( "Program", "Illegal value in register" );
            throw new InvalidOperationException();
        }

        protected void RegisterWriteFloat( int iRegister, float flData ) {
            Line oRegister = Registers[iRegister];
            oRegister.Empty();
            oRegister.TryAppend( flData.ToString() );
        }

        // this won't run, just keeping it for reference.
        protected void Old_RegisterLoad( int iRegister, string strData ) {
            List<Line> oRegister = new(); // = Registers[iRegister];
            if( int.TryParse( strData, out int iData ) ) {
                for( int i=0; i<4; ++i ) {
                    Line oBit = oRegister[i];
                    bool bBit = ( iData & 1 ) > 0;

                    oBit.Empty();
                    oBit.TryAppend( bBit.ToString() );

                    iData = iData >> 1;
                }
                if( iData > 0 )
                    _oBaseSite.LogError( "Overflow", "register " + iRegister.ToString() );
            }
        }

        // this won't run, just keeping it for reference.
        protected int Old_RegisterRead( int iRegister ) {
            int        iData     = 0;
            List<Line> oRegister = new(); // Registers[iRegister];

            for( int i =0; i<oRegister.Count; ++i ) {
                if( oRegister[i].CompareTo( "True" ) == 0 )
                    iData += (int)Math.Pow( 2, i );
            }

            return iData;
        }
        public void ProgramReset() {
            PC = 0;
        }

        public int PC { 
            get { return RegisterRead( 5 ); } 
            set { 
                RegisterWrite( 5, value.ToString() ); 
                TextCommands.Raise_BufferEvent( BUFFEREVENTS.FORMATTED );
            } 
        }

        public void Inst_LoadImm() {
            int    iRegister = int.Parse( TextCommands[++PC].ToString() );
            string strData   = TextCommands[++PC].ToString();

            RegisterWrite( iRegister, strData );
            ++PC;
        }

        public void Inst_Add() {
            ArithmeticInteger( Arithmetic.Add );
        }
        public void Inst_Multiply() {
            ArithmeticInteger( Arithmetic.Mult );
        }
        public void Inst_Subtract() {
            ArithmeticInteger( Arithmetic.Sub);
        }
        public void Inst_Divide() {
            ArithmeticInteger( Arithmetic.Div );
        }
       /// <summary>
        /// doesn't set flags or anything. Annnd not using flags
        /// either. But I'm thinking about it.
        /// </summary>
        private void ArithmeticInteger( Arithmetic eOperand ) {
            int iA      = RegisterRead( 0 );
            int iB      = RegisterRead( 1 );
            int iC      = GetStatusBit( StatusBits.Carry );
            int iResult = 0;
            
            switch( eOperand ) {
                case Arithmetic.Mult: 
                    iResult = iA * iB;
                    break;
                case Arithmetic.Add: 
                    iResult = iA + iB;// + iC;
                    break;
                case Arithmetic.Sub: 
                    //int iNotC = iC != 0 ? 0 : 1;
                    iResult = iA - iB;// - iNotC;
                    break;
                case Arithmetic.Div: 
                    iResult = iA / iB;
                    break;
                default:
                    throw new InvalidOperationException( "bad Arithmetic operation" );
            }

            RegisterWrite( 0, iResult.ToString() );
            ++PC;
        }

        public void Inst_MultiplyFloat() {
            ArithmeticFloat( Arithmetic.Mult );
        }
        public void Inst_AddFloat() {
            ArithmeticFloat( Arithmetic.Add );
        }
        public void Inst_SubtractFloat() {
            ArithmeticFloat( Arithmetic.Sub );
        }
        public void Inst_DivideFloat() {
            ArithmeticFloat( Arithmetic.Div );
        }
        private void ArithmeticFloat( Arithmetic eOperand ) {
            float flA      = RegisterReadFloat( 0 );
            float flB      = RegisterReadFloat( 1 );
            float flResult = 0;
            
            switch( eOperand ) {
                case Arithmetic.Mult: 
                    flResult = flA * flB;
                    break;
                case Arithmetic.Add: 
                    flResult = flA + flB;
                    break;
                case Arithmetic.Sub: 
                    flResult = flA - flB;
                    break;
                case Arithmetic.Div: 
                    flResult = flA / flB;
                    break;
                default:
                    throw new InvalidOperationException( "bad floating point operation" );
            }

            RegisterWrite( 0, flResult.ToString( ) );
            ++PC;
        }
        /// <summary>
        /// Need to revisit this for floating point.
        /// </summary>
        public void Inst_LoadAbs() {
            int    iRegister = int.Parse( TextCommands[++PC].ToString() );
            string strAddr   = TextCommands[++PC].ToString();
            if( int.TryParse( strAddr, out int iAddr ) ) {
                string strData = TextCommands[iAddr].ToString();
                RegisterWrite( iRegister, strData );
            }
            ++PC;
        }

        public void Inst_Save() {
            int iRegister = int.Parse( TextCommands[++PC].ToString() );
            int iAddress  = int.Parse( TextCommands[++PC].ToString() );
            int iData     = RegisterRead( iRegister );
            using( Editor.Manipulator oBulk = TextCommands.CreateManipulator() ) {
                string strData = iData.ToString();
                oBulk.LineTextDelete( iAddress, null );
                oBulk.LineTextInsert( iAddress, 0, strData, 0, strData.Length );
            }
            ++PC;
        }

        public void Inst_JumpImm() {
            PC = int.Parse( TextCommands[++PC].ToString() );
        }

        public void Inst_MoveReg() {
            int iSource = int.Parse( TextCommands[++PC].ToString() );
            int iTarget = int.Parse( TextCommands[++PC].ToString() );

            ++PC;

            RegisterWrite( iTarget, RegisterRead( iSource ) );
        }

        public void Inst_Halt() {
            PC = TextCommands.ElementCount;
        }

        public int GetStatusBit( StatusBits eWhichBit ) {
            if( !int.TryParse( StatusLine[(int)eWhichBit].ToString(), out int iFlagValue ) ) {
                throw new InvalidOperationException();
            }
            return iFlagValue;
        }

        public void SetStatusBits( char cNegative, char cZero, char cCarry ) {
            StatusLine[(int)StatusBits.Negative].Empty();
            StatusLine[(int)StatusBits.Negative].TryInsert( 0, cNegative );
            StatusLine[(int)StatusBits.Zero    ].Empty();
            StatusLine[(int)StatusBits.Zero    ].TryInsert( 0, cZero);
            StatusLine[(int)StatusBits.Carry   ].Empty();
            StatusLine[(int)StatusBits.Carry   ].TryInsert( 0, cCarry );

            FrontDisplay.PropertyDoc.Raise_BufferEvent( BUFFEREVENTS.FORMATTED );
        }

        public void Inst_CompAbs() {
            int    iRegister = int.Parse( TextCommands[++PC].ToString() );
            string strAddr   = TextCommands[++PC].ToString();

            ++PC;

            if( int.TryParse( strAddr, out int iAddr ) ) {
                string strData = TextCommands[iAddr].ToString();

                Compare( iRegister, strData );
                return;
            }
            _oBaseSite.LogError( "Invalid Op", "Bad address or data at address" );
        }

        /// <summary>
        ///Condition 	        N 	Z 	C
        ///Register < Memory 	1 	0 	0
        ///Register = Memory 	0 	1 	1
        ///Register > Memory 	0 	0 	1         
        ///</summary>
        public void Compare( int iRegister, string strData ) {
            if( int.TryParse( strData, out int iData ) ) {
                int iRegisterData = RegisterRead( iRegister );
                if( iRegisterData < iData ) {
                    SetStatusBits( '1', '0', '0' );
                } else {
                    if( iRegisterData > iData ) {
                        SetStatusBits( '0', '0', '1' );
                    } else {
                        SetStatusBits( '0', '1', '1' ); // eq. carry s/b 1 but use 0 for now.
                    }
                }
                return;
            }
            _oBaseSite.LogError( "Invalid Op", "Bad address or data at address" );
        }

        public void Inst_CompImm() {
            int    iRegister = int.Parse( TextCommands[++PC].ToString() );
            string strData   = TextCommands[++PC].ToString();

            ++PC;

            Compare( iRegister, strData );
        }

        /// <summary>
        /// Branch if the value of the given flag is true.
        /// </summary>
        /// <exception cref="InvalidOperationException"></exception>
        public void Inst_BranchTrueImm() {
            Inst_Branch_Imm( true );
        }

        public void Inst_BranchFalseImm() {
            Inst_Branch_Imm( false );
        }
        public void Inst_Branch_Imm( bool fOnTrue ) {
            string strFlag = TextCommands[++PC].ToString(); // Which flag to use.
            string strAddr = TextCommands[++PC].ToString();

            ++PC;

            if( int.TryParse( strAddr, out int iBranchAddr ) ) {
                // First look for a flag label.
                int iStatusLine = -1;
                if( !_dctStatusNames.TryGetValue( strFlag, out iStatusLine ) ) {
                    // Next look for a bit flag, as a base 10 number.
                    // TODO: Add binary parse ... 00010000 for example.
                    if( !int.TryParse( strFlag, out int iStatusBit ) ) {
                        throw new InvalidOperationException();
                    }
                    // Convert the number to which status flag line.
                    if( !_dctPowerOfTwo.TryGetValue( iStatusBit, out iStatusLine ) ) {
                        throw new InvalidOperationException();
                    }
                }
                // Get the value of the requested status flag. 0 or 1.
                if( !int.TryParse( StatusLine[iStatusLine].ToString(), out int iFlagValue ) ) {
                    throw new InvalidOperationException();
                }
                bool fResult = fOnTrue ? iFlagValue != 0 : iFlagValue == 0;
                if( fResult ) 
                    PC = iBranchAddr;
                return;
            }
            throw new InvalidOperationException();
        }

        public void Inst_Increment() {
            int iRegister = int.Parse( TextCommands[++PC].ToString() );

            ++PC;

            int iRegisterData = RegisterRead( iRegister );
            RegisterWrite( iRegister, ++iRegisterData );
        }

        public void Inst_Decrement() {
            int iRegister = int.Parse( TextCommands[++PC].ToString() );

            ++PC;

            int iRegisterData = RegisterRead( iRegister );
            RegisterWrite( iRegister, --iRegisterData );
        }

        /// <summary>
        /// So it turns out when your not single stepping you're ending
        /// up doing screen refreshes on the visible registers for every
        /// instruction. That's something to consider doing something
        /// about if I ever want this to run fast(er)
        /// </summary>
        /// <param name="fNotStep"></param>
        public void ProgramRun( bool fNotStep = true ) {
            int iCnt = 0; // Just a hack to prevent infinite loops. Need something nicer.
            int iMax = (int)Math.Pow( 10, 5);
            try {
                do {
                    Line oInst = TextCommands[PC];
                    if( !_dctInstructions.TryGetValue( oInst.ToString(), out Action delInstruction ) ) {
                        _oBaseSite.LogError( "Execution", "Illegal instruction" );
                        return;
                    }

                    delInstruction();

                    TextCommands.HighLight = TextCommands[PC];
                    RefreshScreen(0);
                } while( PC < TextCommands.ElementCount && fNotStep && ++iCnt < iMax );
            } catch( Exception oEx ) {
                Type[] rgErrors = { typeof( NullReferenceException ),
                                    typeof( ArgumentNullException ), 
                                    typeof( FormatException ), 
                                    typeof( OverflowException ),
                                    typeof( InvalidOperationException ) };
                if( rgErrors.IsUnhandled( oEx ) )
                    throw;

                _oBaseSite.LogError( "Execution", "Problem in instruction decoder" );
            }
        }

    }
}