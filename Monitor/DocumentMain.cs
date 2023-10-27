using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Forms;

using Play.Interfaces.Embedding;
using Play.Edit; 
using Play.Parse;
using Play.Parse.Impl;
using Play.Integration;

namespace Monitor {
    public class BasicEditor : 
        BaseEditor,
        IPgSave<BinaryWriter>,
        IPgLoad<BinaryReader>
    {
        bool _fBinaryLoaded = false;
        protected readonly Grammer<char> _oGrammer;

        public class BasicManipulator : IDisposable {
            BasicEditor _oDocument;

            public BasicManipulator( BasicEditor oDocument ) {
                _oDocument = oDocument ?? throw new ArgumentNullException();
            }

            public void Append( int iBasic, string strLine ) {
                Line oNew = new TextLine( _oDocument._rgLines.Count, strLine );

                oNew.Extra = new TextLine( iBasic, iBasic.ToString() );

                _oDocument._rgLines.Add( oNew );
            }

            public void Append( int iBasic, ReadOnlySpan<char> strLine ) {
                Line oNew = new TextLine( _oDocument._rgLines.Count, strLine );

                oNew.Extra = new TextLine( iBasic, iBasic.ToString() );

                _oDocument._rgLines.Add( oNew );
            }

            public void Append( ReadOnlySpan<char> spLine ) { 
                Line oNew = new TextLine( _oDocument._rgLines.Count, spLine );

                oNew.Extra = new TextLine( -1, "?" );

                _oDocument._rgLines.Add( oNew );
            }

            public void Dispose() {
                _oDocument.CharacterCount( 0 );
                _oDocument.Raise_MultiFinished();
            }
        }
        public BasicEditor( IPgBaseSite oSite ) : base( oSite ) {
			try {
				// A parser is matched one per text document we are loading.
				ParseHandlerText oParser = new ParseHandlerText( this, "bbcbasic" );
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

        protected override Line CreateLine( int iLine, string strValue )
        {
            Line oNew = new TextLine( iLine, strValue );

            int iBasicNumber = iLine;// ( iLine + 1 ) * 10;

            oNew.Extra = new TextLine( -1, "?" );

            return( oNew );
        }

        /// <summary>
        /// Typically you'll get an error that the file is already open if it's
        /// the file servicing our object. Not sure why since the stream should
        /// have been closed after the load. But this makes doubly sure we're not
        /// trying to use our own file. 
        /// TODO: Might be nice to integrate with shell so I can check all files
        /// in use by the shell.
        /// </summary>
        public bool IsOverwrite( string strFileName ) {
            if( string.Compare( _oSiteFile.FilePath, 
                                strFileName, ignoreCase:true ) == 0 ) 
            {
                _oSiteBase.LogError( "Save", 
                                     "Can't overwrite working file! Try another name." );
                return true;
            }
            return false;
        }

        /// <summary>
        /// Load in our txt basic file. Basically "line number in digits" "space" "basic..." 
        /// </summary>
        public override bool Load( TextReader oReader ) {
            using BasicEditor.BasicManipulator oBulk = new ( this );

            Clear( fSendEvents: false );

            try {
                string?            strLine  = null;
                ReadOnlySpan<char> spNumber = null;
                ReadOnlySpan<char> spBasic  = null; 
                while( true ) {
                    // char[] rgLine = stackalloc char[300]
                    // ReadOnlySpan<char> spLine = oReader.ReadLine( ref rgLine )
                    strLine = oReader.ReadLine();

                    if( strLine == null )
                        break;

                    int i=0;
                    // Strip the number off of the start of the string.
                    for( ; i<strLine.Length; ++i ) {
                        if( !Char.IsDigit( strLine[i] ) ) {
                            spNumber = strLine.AsSpan()[0..i];
                            break;
                        }
                    }
                    // Take the rest and use as the basic commands. We
                    // assume that there is ONE space between the line
                    // number and the commands, but check just in case not...
                    if( Char.IsWhiteSpace( strLine[i] ) )
                        ++i;

                    spBasic = strLine.AsSpan().Slice( start:i, length: strLine.Length - i );
                    // Combine the line number and the basic commands.
                    if( int.TryParse( spNumber, out int iNumber ) ) {
                        oBulk.Append( iNumber, spBasic );
                    } else {
                        if( strLine.Length > 0 ) {
                            oBulk.Append( strLine );
                        }
                    }
                };

                Raise_BufferEvent( BUFFEREVENTS.LOADED );  
            } catch( Exception oEx ) {
                Type[] rgErrors = { typeof( IOException ),
                                    typeof( OutOfMemoryException ),
                                    typeof( ObjectDisposedException ),
                                    typeof( ArgumentOutOfRangeException ) };
                if( rgErrors.IsUnhandled( oEx ) )
                    throw;

                return false;
            }

            _fIsDirty = false;

            return true;
        }

        /// <summary>
        /// Use this function to do a side save. This does not clear the
        /// dirty bit for document. 
        /// </summary>
        public bool SaveSide( TextWriter oWriter ) {
            try {
                foreach( Line oLine in this ) {
                    if( oLine.Extra is Line oNumber ) {
                        oWriter.Write(oNumber.AsSpan);
                        oWriter.Write(' ');
                    }
                    oWriter.WriteLine(oLine.AsSpan);
                }
                if( !_fBinaryLoaded )
                    _fIsDirty = false;
            } catch( Exception oE ) {
                Type[] rgErrors = {
                    typeof( FormatException ),
                    typeof( IOException ),
                    typeof( ObjectDisposedException ),
                    typeof( ArgumentNullException )
                };

                if( rgErrors.IsUnhandled( oE ) )
                    throw;

                _oSiteBase.LogError( "editor", oE.Message );
                return false;
            } finally {
                oWriter.Flush();
            }
            
            return true;
        }

        /// <summary>
        /// This is how the shell saves. Only the shell should call this
        /// function. Use SaveSide for "saveas" operations.
        /// </summary>
        /// <remarks>TODO: You know, this is why I should have the dirty bit saved
        /// on the site the shell manages!! Then I wouldn't have this problem!!</remarks>
        /// <seealso cref="SaveSide"/>
        public override bool Save( TextWriter oWriter ) {
            if( !SaveSide( oWriter ) )
                return false;

            _fIsDirty = false;
            return true;
        }

        /// <summary>
        /// OUr persistant storage in the binary file case.
        /// DO NOT CALL THIS FUNCTION TO SIDE LOAD... or Edit/Insert...
        /// </summary>
        /// <param name="oReader"></param>
        /// <returns></returns>
        public bool Load( BinaryReader oReader ) {
            _fBinaryLoaded = true;
            Clear();

            BbcBasic5 oBasic = new BbcBasic5();

            bool fReturn = oBasic.IO_Detokanize( oReader, this );
            // Want the Edit Window banner to update...
            Raise_BufferEvent( BUFFEREVENTS.LOADED );

            // We're basically side loading and the manipulator is
            // dirtying the document. Clear it since this is our
            // persistant storage primary load.
            _fIsDirty = false;

            return fReturn;
        }

        public bool SaveSide( BinaryWriter oWriter ) {
            try {
                BbcBasic5 oBasic = new BbcBasic5();

                oBasic.IO_Tokenize( this, oWriter );
            } catch( Exception oEx ) {
                if( MonitorDocument._rgIOErrors.IsUnhandled( oEx ) )
                    throw;
                LogError( "File may be r/o, path too long, unauthorized." );
                return false;
            }

            return true;
        }

        /// <summary>
        /// This is how the shell saves. Only the shell should call this
        /// function. Use SaveSide for "saveas" operations.
        /// </summary>
        /// <seealso cref="SaveSide"/>
        public bool Save( BinaryWriter oWriter ) {
            if( !SaveSide( oWriter ) )
                return false;

            _fIsDirty = false;
            return true;
        }

        struct Remaps {
            public IMemoryRange oParamRange;
            public Line oSourceLine;
            public Line oTargetLine;
        }

        /// <summary>
        /// Nothing fancy. Just looks through all the lines. Perf is no big deal since
        /// there's not very many lookups and we don't do it that often.
        /// </summary>
        /// <param name="spBasicLine">The line number we're looking for.</param>
        public Line? FindLineNumber( ReadOnlySpan<char> spBasicLine ) {
            foreach( Line oLine in this ) {
                if( oLine.Extra is Line oNumber ) {
                    if( MemoryExtensions.CompareTo( spBasicLine, oNumber.AsSpan, StringComparison.Ordinal ) == 0 ) {
                        return oLine;
                    }
                }
            }
            return null;
        }

        public static bool Contains( string[] rgValues, ReadOnlySpan<char> spSearch) {
            foreach( string strValue in rgValues ) {
                if( MemoryExtensions.CompareTo( spSearch, strValue, StringComparison.OrdinalIgnoreCase ) == 0 )
                    return true;
            }
            return false;
        }

        public void Renumber() {
            State<char> oFunction = _oGrammer.FindState( "function" );

            if( oFunction == null )
                throw new ArgumentException("Can't find required state in grammar.");

            int iInstr = oFunction.Bindings.IndexOfKey( "keywords" );
            int iParms = oFunction.Bindings.IndexOfKey( "number" ); // For goto & gosub

            if( iInstr == -1 || iParms == -1 )
                throw new ArgumentException( "Could not find required state bindings" );

            List<Remaps> rgRemap = new();

            // Look for all the goto's
            foreach( Line oLine in this ) {
                foreach( IColorRange oRange in oLine.Formatting ) {
                    if( oRange is MemoryState<char> oMemory ) {
                        if( string.Compare( oMemory.StateName, "function" ) == 0 ) {
                            ReadOnlySpan<char> spFnName = oLine.SubSpan( oMemory.GetValue( iInstr ) );
                            string []  rgValues = { "goto", "gosub" };

                            if( Contains( rgValues, spFnName ) ) {
                                try {
                                    IPgWordRange       wrParam = oMemory.GetValue( iParms  );
                                    ReadOnlySpan<char> spParam = oLine  .SubSpan ( wrParam );
                                    if( FindLineNumber( spParam ) is Line oTarget ) {
                                        rgRemap.Add( new Remaps() { oSourceLine = oLine, 
                                                                    oTargetLine = oTarget,
                                                                    oParamRange = wrParam } );
                                    }
                                } catch( Exception oEx ) {
                                    Type[] rgErrors = { typeof( ArgumentOutOfRangeException ),
                                                        typeof( InvalidCastException ),
                                                        typeof( NullReferenceException ) };
                                    if( rgErrors.IsUnhandled( oEx ) )
                                        throw;
                                }
                            }
                        }
                    }
                }
            }

            // Renumber the lines.
            int iBasicLine = 10;
            foreach( Line oLine in this ) {
                if( oLine.Extra is Line oNumber ) {
                    oNumber.Empty();
                    oNumber.TryAppend( iBasicLine.ToString() );
                }
                iBasicLine += 10;
            }

            foreach( Remaps oMap in rgRemap ) {
                if( oMap.oTargetLine.Extra is Line oNumber ) {
                    oMap.oSourceLine.TryReplace( oMap.oParamRange.Offset, oMap.oParamRange.Length, oNumber.AsSpan );
                }
            }

            SetDirty();

            Raise_MultiFinished();
        }
        public void Test() {
            BbcBasic5 oBasic = new BbcBasic5();
            oBasic.Test( _oSiteBase );
        }

        public static bool IsStateMatch( MemoryState<char> oMState, string strName ) {
            return string.Compare( oMState.StateName, strName, ignoreCase:true ) == 0 ;
        }

        /// <summary>
        /// This is a poster child for why the start event on the parser needs
        /// to past the first state found in the parse tree.
        /// </summary>
        public void Compile( Editor oMachineCode ) {
            foreach( IColorRange oRange in this[0].Formatting ) {
                if( oRange is MemoryState<char> oMState ) {
                    if( IsStateMatch( oMState, "start" ) ) {
                        Compile( oMState, oMachineCode );
                        return;
                    }
                }
            }
            LogError( "Problem starting compile" );
        }

        /// <summary>
        /// We'll try to make this Span based in the future. But too many changes for now.
        /// </summary>
        /// <returns></returns>
        public static string GetString( LineStream oStream, MemoryElem<char> oElem ) {
            Line oLine = oStream.SeekLine( oElem.Start, out int iLineOffset);
            return oLine.SubString( oElem.Offset, oElem.Length );
        }

        /// <summary>
        /// This is my first attempt at a compiler. I'm going to target
        /// my emulator. And the emulator will be roughly z-80 since I want
        /// to target the agon in the end.
        /// </summary>
        /// <remarks>TODO: Make the parser return the start block!!
        /// so we an walk the parse tree!!</remarks>
        public void Compile( in MemoryState<char> oMStart, in Editor oMachineCode ) {
            State<char>            oClassStart = _oGrammer.FindState( "start" );
            int                    iStart      = oClassStart.Bindings.IndexOfKey( "bbcbasic" );
            MemoryState<char>      oStart      = oMStart  .GetState( iStart );
            BasicEditor.LineStream oStream     = this.CreateStream();

            Compiler oBuild = new Compiler( _oGrammer, oStream, oMachineCode );

            MemoryState<char>  oNode = oBuild.GetNode( oStart );
            while( oNode != null ) {
                MemoryState<char> oStatement = oBuild.GetStatement( oNode );
                if( oStatement != null ) {
                    if( IsStateMatch( oStatement, "function" ) ) {
                        oBuild.FCallName( oStatement );
                    }
                }

                oNode = oBuild.GetNode( oNode ); // Could make it array access.
            }
        }

        /* 
            For example, in BNF, the classic expression grammar is:

             <expr> ::= <term> "+" <expr>
                     |  <term>

             <term> ::= <factor> "*" <term>
                     |  <factor>

             <factor> ::= "(" <expr> ")"
                       |  <const>

             <const> ::= integer
         */
        protected class Compiler {
            // BUG: Would be nice if checked if any -1. Make a version that
            // throws exception in the future!!
            int _iStart ;
            int _iStatement;
            int _iContinue;
            int _iFCallName;
            int _iFParams;
            int _iNumber;
            int _iFactorExp;

            Editor.Manipulator    _oMechBulk;
            Grammer<char>         _oGrammer;
            BaseEditor.LineStream _oStream;
            Editor                _oMachineCode;

            public Compiler( Grammer<char>         oGrammer, 
                             BaseEditor.LineStream oStream,
                             Editor                oMachineCode           
            ) { 
                _oGrammer = oGrammer ?? throw new ArgumentNullException( nameof( oGrammer ) );
                _oStream  = oStream  ?? throw new ArgumentNullException( nameof( oStream ) );

                // Let's look up all the bindings just once at the start!!
                State<char> oClassBasic= oGrammer.FindState( "bbcbasic" );
                State<char> oClassFCall= oGrammer.FindState( "function" );
                State<char> oClassFacto= oGrammer.FindState( "factor" );

                // BUG: Would be nice if checked if any -1. Make a version that
                // throws exception in the future!!
                _iStatement  = oClassBasic.Bindings.IndexOfKey( "statement" );
                _iContinue   = oClassBasic.Bindings.IndexOfKey( "bbcbasic" );
                _iFCallName  = oClassFCall.Bindings.IndexOfKey( "procname" );
                _iFParams    = oClassFCall.Bindings.IndexOfKey( "params" );
                _iNumber     = oClassFacto.Bindings.IndexOfKey( "number" );
                _iFactorExp = oClassFacto.Bindings.IndexOfKey( "factorexp" );
            
                _oMechBulk = oMachineCode.CreateManipulator();
            } 

            public MemoryState<char> GetNode( MemoryState<char> oCurrent ) {
                return oCurrent.GetState( _iContinue );
            }

            public MemoryState<char> GetStatement( MemoryState<char> oCurrent ) {
                return oCurrent.GetState( _iStatement );
            }

            void LineAppend( MemoryElem<char> oElem ) {
                _oMechBulk.LineAppend( GetString( _oStream, oElem ) );
            }

            void LineAppend( string strValue ) {
                _oMechBulk.LineAppend( strValue );
            }

            /* 
            The classic expression grammar is:

            <expr>   ::= <term> "+" <expr>
                      |  <term>
            <term>   ::= <factor> "*" <term>
                      |  <factor>
            <factor> ::= "(" <expr> ")"
                      |  <const>
            <const>  ::= integer
            */
            protected void Expression( MemoryState<char> oExpression ) {
                if( IsStateMatch( oExpression, "factor" ) ) {
                    MemoryElem<char> oNumber = oExpression.GetValue( _iNumber );
                    if( oNumber is MemoryState<char> oNumState ) {
                        if( IsStateMatch( oNumState, "vdecl" ) ) {
                            LineAppend( oNumState );
                        }
                        if( IsStateMatch( oNumState, "built-in-function-call" ) ) {
                            FCallName( oNumState );
                        }
                    } else {
                        if( oNumber != null ) {
                            LineAppend( oNumber );
                        } else {
                            MemoryState<char> oFactorExpr = oExpression.GetState( _iFactorExp );
                            Expression( oFactorExpr );
                        }
                    }
                }
            }

            public void FCallName( MemoryState<char> oStatement ) {
                MemoryElem<char> oFCallName = oStatement.GetValue( _iFCallName );
                if( oFCallName != null ) {
                    LineAppend( oFCallName );

                    foreach( MemoryElem<char> oParam in oStatement.EnumValues( _iFParams ) ) {
                        if( oParam is MemoryState<char> oExpression ) {
                            Expression( oExpression );
                        } else {
                            LineAppend( oParam );
                        }
                    }
                }
            }
        }
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

    /// <summary>
    /// This is a little cpu emulator. I started out as working towards a 6502 but
    /// now that I have an Agon Light 2 running BBC basic I'll probably pivot towards
    /// the Z80.
    /// </summary>
    public class MonitorDocument :
        IPgParent,
		IDisposable,
        IPgLoad<TextReader>,
        IPgSave<TextWriter>,
        IPgLoad<BinaryReader>,
        IPgSave<BinaryWriter>
    {
        protected readonly IPgBaseSite _oBaseSite;
                                
        public static readonly Type[] _rgIOErrors = { 
            typeof( ArgumentException ),
            typeof( ArgumentNullException ),
            typeof( NotSupportedException ),
            typeof( FileNotFoundException ),
            typeof( IOException ),
            typeof( System.Security.SecurityException ),
            typeof( DirectoryNotFoundException ),
            typeof( UnauthorizedAccessException ),
            typeof( PathTooLongException ),
            typeof( ArgumentOutOfRangeException ) };

        protected readonly IPgFileSite _oFileSite;

        protected Dictionary< string, Action> _dctInstructions = new();
        protected Dictionary< string, int >   _dctStatusNames  = new();
        protected Dictionary< int, int >      _dctPowerOfTwo   = new();

        protected readonly Grammer<char> _oGrammer;
        public bool IsDirty => BasicDoc.IsDirty; 

        public IPgParent Parentage => _oBaseSite.Host;
        public IPgParent Services  => Parentage.Services;

        public Editor        TextCommands { get; } // Machine code like commands for the emulator.
        public BasicEditor   BasicDoc     { get; } // High level basic.
        public Editor        AssemblyDoc  { get; } // Assembly to be compiled to machine code.
        public CpuProperties Properties { get; }
        public Editor        DumpDocument { get; }

        protected readonly List<Line>                               _rgRegisters = new();
        protected readonly List<Line>                               _rgStatusBit = new();
        protected readonly Dictionary<string, List<AsmInstruction>> _rgInstr     = new();

        protected bool _fIsBinaryLoad = false;
        public bool BinaryLoaded { get { return _fIsBinaryLoad; } }

        protected void LogError( string strLabel, string strMessage ) {
            _oBaseSite.LogError( strLabel, strMessage );
        }

        public string? StatusBitAsString( StatusBits eBit ) {
            string? strReturn = _rgStatusBit[(int)eBit].ToString();

            return strReturn;
        }

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

        public event Action<int>? RefreshScreen;

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

        public class FileSlot :
            DocSlot,
            IPgFileSite 
        {

            public FileSlot( MonitorDocument oHost ) : base( oHost ) {
            }
            public FILESTATS FileStatus => _oHost._oFileSite.FileStatus;

            public Encoding FileEncoding => _oHost._oFileSite.FileEncoding;

            public string FilePath => _oHost._oFileSite.FilePath;

            public string FileName => _oHost._oFileSite.FileName;
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
            _oFileSite = oSite as IPgFileSite ?? throw new ArgumentException( "Need a IPgFileSite" );

            TextCommands = new ProgramFile  ( new DocSlot ( this ) );
            BasicDoc     = new BasicEditor  ( new FileSlot( this ) );
            AssemblyDoc  = new Editor       ( new FileSlot( this ) );
            Properties   = new CpuProperties( new DocSlot ( this ) );
            DumpDocument = new Editor       ( new DocSlot ( this ) );

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

            // This is 6502 flag positions, Don't presently match my status lines.
            _dctPowerOfTwo.Add(   1, 0 ); // carry
            _dctPowerOfTwo.Add(   2, 1 ); // zero
            _dctPowerOfTwo.Add(   4, 2 ); // int disable
            _dctPowerOfTwo.Add(   8, 3 ); // decimal
            _dctPowerOfTwo.Add(  16, 4 ); // break was triggered
            _dctPowerOfTwo.Add(  32, 5 ); // unused.
            _dctPowerOfTwo.Add(  64, 6 ); // overflow
            _dctPowerOfTwo.Add( 128, 7 ); // negative.

			//try {
			//	// A parser is matched one per text document we are loading.
			//	ParseHandlerText oParser = new ParseHandlerText( AssemblyDoc, "bbcbasic" );
   //             _oGrammer = oParser.Grammer;
			//} catch( Exception oEx ) {
   //             Type[] rgErrors = { typeof( NullReferenceException ),
   //                                 typeof( InvalidCastException ),
   //                                 typeof( ArgumentNullException ),
			//						typeof( ArgumentException ),
			//						typeof( InvalidOperationException ),
			//						typeof( InvalidProgramException ) };
   //             if( rgErrors.IsUnhandled( oEx ) )
   //                 throw;

			//	throw new InvalidOperationException( "Couldn't create parse handler for given text.", oEx );
			//}
        }

        // See ca 1816 warning.
        public void Dispose() {
        }

        public bool Initialize() {
            if( !Properties.InitNew() )
                return false;
            if( !DumpDocument.InitNew() )
                return false;

            InitInstructions();

            _rgRegisters.Add( Properties[(int)CpuProperties.Properties.Register_0] );
            _rgRegisters.Add( Properties[(int)CpuProperties.Properties.Register_1] );
            _rgRegisters.Add( Properties[(int)CpuProperties.Properties.Register_2] );
            _rgRegisters.Add( Properties[(int)CpuProperties.Properties.Register_3] );

            _rgStatusBit.Add( Properties[(int)StatusBits.Overflow ] );
            _rgStatusBit.Add( Properties[(int)StatusBits.Carry    ] );
            _rgStatusBit.Add( Properties[(int)StatusBits.Zero     ] );
            _rgStatusBit.Add( Properties[(int)StatusBits.Negative ] );

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

            if( !BasicDoc.InitNew() )
                return false;

            if( !AssemblyDoc.InitNew() )
                return false;

            if( !Initialize() ) 
                return false;

            return true;
        }

        public bool Load(TextReader oStream) {
            _fIsBinaryLoad = false;

            if( !BasicDoc.Load( oStream ) )
                return false;

            if( !AssemblyDoc.InitNew() )
                return false;

            if( !TextCommands.InitNew() )
                return false;

            if( !Initialize() ) 
                return false;

            return true;
        }

        public bool Save(TextWriter oWriter ) {
            return BasicDoc.Save( oWriter );
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
            Line oRegister = _rgRegisters[iRegister];
            oRegister.Empty();
            oRegister.TryAppend( strData );
        }

        protected void RegisterWrite( int iRegister, int iData ) {
            Line oRegister = _rgRegisters[iRegister];
            oRegister.Empty();
            oRegister.TryAppend( iData.ToString() );
        }

        protected int RegisterRead( int iRegister ) {
            try {
                Line oRegister = _rgRegisters[iRegister];

                if( oRegister.ElementCount == 0 )
                    return 0;

                if( int.TryParse( oRegister.ToString(), out int iData ) ) {
                    return iData;
                }
            } catch( ArgumentOutOfRangeException oEx ) {
                _oBaseSite.LogError( "Program", "Illegal register" );
                throw new InvalidOperationException(  "Illegal register", oEx );
            }

            _oBaseSite.LogError( "Program", "Illegal value in register" );
            throw new InvalidOperationException();
        }

        protected float RegisterReadFloat( int iRegister ) {
            Line oRegister = _rgRegisters[iRegister];

            if( oRegister.ElementCount == 0 )
                return 0;

            if( float.TryParse( oRegister.ToString(), out float flData ) ) {
                return flData;
            }

            _oBaseSite.LogError( "Program", "Illegal value in register" );
            throw new InvalidOperationException();
        }

        protected void RegisterWriteFloat( int iRegister, float flData ) {
            Line oRegister = _rgRegisters[iRegister];
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
            get { return Properties.ValueGetAsInt((int)CpuProperties.Properties.Program_Counter); } 
            set { 
                Properties.ValueUpdate( (int)CpuProperties.Properties.Program_Counter, value.ToString() ); 
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
            int iC      = StatusBitAsInt( StatusBits.Carry );
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

        public int StatusBitAsInt( StatusBits eWhichBit ) {
            string strStatusBit = StatusBitAsString(eWhichBit);

            if( string.IsNullOrEmpty( strStatusBit ) )
                return 0;

            if( !int.TryParse( strStatusBit, out int iFlagValue ) ) {
                throw new InvalidOperationException();
            }
            return iFlagValue;
        }

        public void SetStatusBits( char cNegative, char cZero, char cCarry ) {
            Properties.ValueUpdate( (int)StatusBits.Negative, cNegative.ToString() );
            Properties.ValueUpdate( (int)StatusBits.Zero,     cZero    .ToString() );
            Properties.ValueUpdate( (int)StatusBits.Carry,    cCarry   .ToString() );

            Properties.PropertyDoc.Raise_BufferEvent( BUFFEREVENTS.FORMATTED );
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
                if( !int.TryParse( _rgStatusBit[iStatusLine].ToString(), out int iFlagValue ) ) {
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
                    RefreshScreen?.Invoke(0);
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

        private static bool FileCheck( string strFileName ) {
            FileAttributes oAttribs;
            bool           fIsFile  = false;

            try {
                oAttribs = File.GetAttributes(strFileName);
                fIsFile  = ( oAttribs & FileAttributes.Directory ) == 0;
            } catch( Exception oEx ) {
                if( _rgIOErrors.IsUnhandled( oEx ) ) {
                    throw;
                }
            }

            return fIsFile;
        }

        /// <summary>
        /// Reload the main document with the given file. Right now we just assume
        /// it's BBC basic binary. In the future I'll have some sort of Insert As...
        /// </summary>
        public void SideLoad() {
            // It's blocking but what can you do...
            try {
                using( OpenFileDialog oDialog = new OpenFileDialog() ) {
                    oDialog.Filter = "BBC Binary|*.bbc|Basic Binary|*.bas";

                    if( oDialog.ShowDialog() == DialogResult.OK ) {
                        if( FileCheck( oDialog.FileName ) ) {
                            // Don't put this in the FileOk event because after that event
                            // the dialog returns focus to where it came from and we lose
                            // focus from our newly opened view.
                            BbcBasic5 oBasic = new BbcBasic5();

                            oBasic.Load( oDialog.FileName, BasicDoc );
                        }
                    }
                }
            } catch( Exception oEx ) {
                if( _rgIOErrors.IsUnhandled( oEx ) )
                    throw;
                LogError( "Load", "Problem with given file." );
            }
        }

        public void SaveAsDialog() {
            using( SaveFileDialog oSave = new SaveFileDialog() ) {
                oSave.Filter       = "BBC Binary|*.bbc|Basic Binary|*.bas|Text File|*.btx";
                oSave.Title        = "Save Basic File";
                oSave.AddExtension = true;
                oSave.FileName     = Path.GetFileNameWithoutExtension( _oFileSite.FileName );
                oSave.FilterIndex  = BinaryLoaded ? 3 : 1; // opposite type we are persisted as.
                
                if( oSave.ShowDialog() == DialogResult.OK ) {
                    if( string.IsNullOrEmpty( oSave.FileName ) ) {
                        return;
                    }
                    // NOTE: that the FilterIndex property is one-based.
                    switch( oSave.FilterIndex ) {
                        case 1:
                        case 2:
                            SideSaveBinary( oSave.FileName );
                            break;
                        case 3:
                            SideSaveText( oSave.FileName );
                            break;
                    }
                }
            }
        }

        public void SideSaveBinary( string strFileName ) {
            if( BasicDoc.IsOverwrite( strFileName ) )
                return;

            try {
                using FileStream oStream = new FileStream( strFileName,
                                                           FileMode.Create, 
                                                           FileAccess.Write );
                using BinaryWriter oWriter = new BinaryWriter( oStream, Encoding.ASCII );
                BasicDoc.SaveSide( oWriter );
            } catch( Exception oEx ) {
                if( _rgIOErrors.IsUnhandled( oEx ) )
                    throw;
                LogError( "Save", "File may be r/o, path too long, unauthorized." );
            }
        }

        public void SideSaveText( string strFileName ) {
            if( BasicDoc.IsOverwrite( strFileName ) ) {
                LogError( "Save", "That file exists, can't overwrite (yet)." );
                return;
            }

            try {
                using FileStream oStream = new FileStream( strFileName,
                                                           FileMode.Create, 
                                                           FileAccess.Write );
                using TextWriter oWriter = new StreamWriter( oStream, Encoding.UTF8 );
                BasicDoc.SaveSide( oWriter );
            } catch( Exception oEx ) {
                if( _rgIOErrors.IsUnhandled( oEx ) )
                    throw;
                LogError( "Save", "File may be r/o, path too long, unauthorized." );
            }
        }

        /// <summary>
        /// Do a memory dump of the given file in the file system. Open a view on the dump file.
        /// </summary>
        public void DumpBinaryFile( IPgViewSite oViewSite ) {
           if( oViewSite is IPgShellSite oShellSite ) {
                IPgCommandView? oFoundView = null;
                foreach( IPgCommandView oView in oShellSite.EnumerateSiblings ) {
                    if( oView.Catagory == MonitorController.DumpWindowGUID ) {
                        oFoundView = oView;
                        break;
                    }
                }
                // It's blocking but what can you do...
                using( OpenFileDialog oDialog = new OpenFileDialog() ) {
                    if( oDialog.ShowDialog() == DialogResult.OK ) {
                        if( FileCheck( oDialog.FileName ) ) {
                            // Don't put this in the FileOk event because after that event
                            // the dialog returns focus to where it came from and we lose
                            // focus from our newly opened view.
                            BbcBasic5.Dump( oDialog.FileName, DumpDocument );

                            if( oFoundView == null ) {
                                oShellSite.AddView( MonitorController.DumpWindowGUID, fFocus: true );
                            } else {
                                oShellSite.FocusTo( oFoundView );
                            }
                        }
                    }
                }
            }
        }

        public bool Load(BinaryReader oReader ) {
            _fIsBinaryLoad = true;

            if( !BasicDoc.Load( oReader ) )
                return false;

            if( !AssemblyDoc.InitNew() )
                return false;

            if( !TextCommands.InitNew() )
                return false;

            if( !Initialize() ) 
                return false;

            return true;
        }


        /// <summary>
        /// The binary version of our loader.
        /// </summary>
        /// <seealso cref="SideSaveBinary(string)"/>
        /// <seealso cref="SideSaveText(string)"/>
        public bool Save(BinaryWriter oWriter) {
            return BasicDoc.Save( oWriter );
        }
    }
}
