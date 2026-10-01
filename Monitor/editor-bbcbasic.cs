using Play.Edit; 
using Play.Interfaces.Embedding;
using Play.Parse;
using Play.Parse.Impl;

using System.Collections;

namespace Monitor {
    public class BasicRow : Row {
        public const int ColumnTarget = 2; // if line is target to a gosub
        public const int ColumnNumber = 0;
        public const int ColumnText   = 1;
        public BasicRow( int iBasicLine, ReadOnlySpan<char> rgText ) { 
            _rgColumns = new Line[3];

            string strLineNum = iBasicLine >= 0 ? iBasicLine.ToString() : "?";

            _rgColumns[0] = new TextLine( iBasicLine, strLineNum );
            _rgColumns[1] = new TextLine( 1,          rgText.ToString() );
            _rgColumns[2] = new TextLine( 0,          string.Empty );
        }

        public Line Text   => _rgColumns[ColumnText];
        public Line Number => _rgColumns[ColumnNumber];

        public static int ColumnCount => 2;
    }

    public class BasicEditor : 
        EditMultiColumn,
        IPgSave<BinaryWriter>,
        IPgLoad<BinaryReader>,
        IPgCommandBase
    {
        protected          bool              _fBinaryLoaded = false;
        protected readonly IPgFileSite       _oSiteFile;
        protected          bool              _fIsDirty = false;
        protected readonly IPgRoundRobinWork _oWorkPlace; 
        protected readonly Grammer<char>     _oBasicGrammer;
        public             Editor            DocProd { get; protected set; }
        public override bool IsDirty => _fIsDirty;

        public class BasicManipulator : IDisposable {
            BasicEditor _oDocument;

            public BasicManipulator( BasicEditor oDocument ) {
                _oDocument = oDocument ?? throw new ArgumentNullException();
            }

            /// <summary>
            /// Appends the line at the bottom of the file. But numbers
            /// it with the given basic line number.
            /// </summary>
            public void Append( int iBasNum, ReadOnlySpan<char> strLine ) {
                BasicRow oNew = new BasicRow( iBasNum, strLine );

                _oDocument._rgRows.Add( oNew );
            }

            /// <summary>
            /// Use to append unnumbered basic lines.
            /// </summary>
            /// <param name="spLine"></param>
            public void Append( ReadOnlySpan<char> spLine ) { 
                Append( -1, spLine );
            }

            public void Dispose() {
                _oDocument.RenumberAndSumate();
                _oDocument.DoParse          ();
            }
        }

		public class DocSlot:
			IPgBaseSite
		{
			protected BasicEditor _oHost;

			public DocSlot( BasicEditor oHost ) {
				_oHost = oHost ?? throw new ArgumentNullException();
			}

			public IPgParent Host => _oHost;

            public void LogError(string strMessage, string strDetails, bool fShow=true) {
				_oHost._oSiteBase.LogError( strMessage, strDetails, fShow );
			}

			public void Notify( ShellNotify eEvent ) {
			}
        } // End class


        /// <summary>
        /// I'd like to use this same object for multiple types of basics
        /// But to do that I need to load the different grammar dialects.
        /// I might have to make subclass documents but doesn't seem
        /// necessary right now.
        /// </summary>
        /// <exception cref="InvalidOperationException"></exception>
        public BasicEditor( IPgBaseSite oSite, string strExtn ) : base( oSite ) {
            _oSiteFile = (IPgFileSite)oSite;

            IPgScheduler oSchedular = (IPgScheduler)Services;
            IPgGrammers  oGServ     = (IPgGrammers)Services;

            DocProd        = new Editor( new DocSlot( this ) );

            _oWorkPlace    = oSchedular.CreateWorkPlace() ?? throw new InvalidOperationException( "Need the scheduler service in order to work. ^_^;" );
            _oBasicGrammer = (Grammer<char>)oGServ.GetGrammerByExtn( strExtn );
       }

        public Row InsertRow( int iLine, int iBasNum, string strValue ) {
            Row oNew = new BasicRow( iBasNum, strValue );

            _rgRows.Insert( iLine, oNew );

            RenumberAndSumate();

            return oNew;
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

        public bool InitNew() {
            InsertRow( 0, 10, string.Empty );

            return true;
        }

        /// <summary>
        /// Load in our txt basic file. Basically "line number in digits" "space" "basic..." 
        /// </summary>
        public bool Load( TextReader oReader ) {
            using BasicEditor.BasicManipulator oBulk = new ( this );

            Clear();

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

                    // Grab any whitespace that might precede the number
                    int i=0;
                    for( ; i<strLine.Length; ++i ) {
                        if( !char.IsWhiteSpace( strLine[i] ) )
                            break;
                    }
                    // Strip the number off of the start of the string.
                    for( ; i<strLine.Length; ++i ) {
                        if( !Char.IsDigit( strLine[i] )  ) 
                        {
                            spNumber = strLine.AsSpan()[0..i];
                            break;
                        }
                    }
                    // Take the rest and use as the basic commands. We
                    // assume that there is ONE space between the line
                    // number and the commands, but check just in case not...
                    if( i >= strLine.Length )
                        continue;

                    if( Char.IsWhiteSpace( strLine[i] ) )
                        ++i;

                    if( i >= strLine.Length )
                        continue;

                    spBasic = strLine.AsSpan().Slice( start:i, length: strLine.Length - i );
                    // Combine the line number and the basic commands.
                    if( int.TryParse( spNumber, out int iBasNum ) ) {
                        oBulk.Append( iBasNum, spBasic );
                    } else {
                        if( strLine.Length > 0 ) {
                            oBulk.Append( strLine );
                        }
                    }
                };

                DoParse();  
            } catch( Exception oEx ) {
                Type[] rgErrors = { typeof( IOException ),
                                    typeof( OutOfMemoryException ),
                                    typeof( ObjectDisposedException ),
                                    typeof( ArgumentOutOfRangeException ),
                                    typeof( IndexOutOfRangeException ) };
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
                foreach( Row oRow in this ) {
                    if( oRow is BasicRow oBasic ) {
                        oWriter.Write( oBasic.Number.AsSpan);
                        oWriter.Write(' ');
                        oWriter.WriteLine( oBasic.Text.AsSpan );
                    }
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
        public bool Save( TextWriter oWriter ) {
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
            DoParse();

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
                if( Old_CPU_Emulator._rgIOErrors.IsUnhandled( oEx ) )
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
            public BasicRow oSourceLine;
            public BasicRow oTargetLine;
        }

        /// <summary>
        /// Nothing fancy. Just looks through all the lines. Perf is no big deal since
        /// there's not very many lookups and we don't do it that often.
        /// </summary>
        /// <param name="spBasicLine">The line number we're looking for.</param>
        public Row? FindLineNumber( ReadOnlySpan<char> spBasicLine ) {
            foreach( Row oRow in this ) {
                if( oRow is BasicRow oBasic ) {
                    if( MemoryExtensions.CompareTo( spBasicLine, 
                                                    oBasic.Number.AsSpan, 
                                                    StringComparison.Ordinal ) == 0 ) {
                        return oRow;
                    }
                }
            }
            return null;
        }

        public static bool Contains( string[] rgValues, ReadOnlySpan<char> spSearch) {
            foreach( string strValue in rgValues ) {
                if( MemoryExtensions.CompareTo( spSearch, strValue, 
                                                StringComparison.OrdinalIgnoreCase ) == 0 )
                    return true;
            }
            return false;
        }

        public void Renumber() {
            try {
                State<char> oFunction = _oBasicGrammer.FindState( "function" ) ??
                    throw new ArgumentException("Can't find required state in grammar.");

                int iInstr = oFunction.Bindings.IndexOfKey( "keywords" );
                int iParms = oFunction.Bindings.IndexOfKey( "number" ); // For goto & gosub

                if( iInstr == -1 || iParms == -1 )
                    throw new ArgumentException( "Could not find required state bindings" );

                List<Remaps> rgRemap = new();

                // Look for all the goto's
                foreach( BasicRow oBasic in this ) {
                    foreach( IColorRange oRange in oBasic.Text.Formatting ) {
                        if( oRange is MemoryState<char> oMemory ) {
                            if( string.Compare( oMemory.StateName, "function" ) == 0 ) {
                                ReadOnlySpan<char> spFnName = oBasic.Text.SubSpan( oMemory.GetValue( iInstr ) );
                                string []  rgValues = { "goto", "gosub" };

                                if( Contains( rgValues, spFnName ) ) {
                                    try {
                                        IPgWordRange       wrParam = oMemory.GetValue( iParms  );
                                        ReadOnlySpan<char> spParam = oBasic.Text.SubSpan ( wrParam );
                                        if( FindLineNumber( spParam ) is BasicRow oTarget ) {
                                            rgRemap.Add( new Remaps() { oSourceLine = oBasic, 
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
                foreach( BasicRow oBasic in this ) {
                    oBasic.Number.Empty();
                    oBasic.Number.TryReplace( iBasicLine.ToString() );
                    iBasicLine += 10;
                }

                foreach( Remaps oMap in rgRemap ) {
                    oMap.oSourceLine.Text.TryReplace( oMap.oParamRange, 
                                                      oMap.oTargetLine.Number.AsSpan );
                }

                IsDirty = true;

                DoParse();
            } catch( Exception oEx ) {
                if( IsStdUnhandled( oEx ) )
                    throw;
            }
        }

        public static bool IsStdUnhandled( Exception oEx ) {
            Type[] rgErrors = { typeof( ArgumentException ),
                                typeof( ArgumentNullException ),
                                typeof( InvalidCastException ),
                                typeof( ArgumentOutOfRangeException ),
                                typeof( InvalidOperationException ),
                                typeof( InvalidProgramException ) };

            return rgErrors.IsUnhandled( oEx );
        }

        /// <summary>
        /// Of course, I could parse all in one go here but I want
        /// to wait 2 seconds before I attempt to do anything.
        /// </summary>
        public override void DoParse() {
            _oWorkPlace.Queue( GetParseEnum(), iWaitMS:2000 );
        }

        public IEnumerator<int> GetParseEnum() {
            RenumberAndSumate();
            DocProd.Clear    ();

            ParseColumn( BasicRow.ColumnText, _oBasicGrammer /*, OnProduction */ );

            Raise_DocFormatted();

            yield return 0;
        }

        public void OnProduction( Production<char> oProd, int iStart ) {
            if( true ) {
				try {
					string strMessage = iStart.ToString() 
										+ " " 
										+ oProd.ToString();
					DocProd.LineAppend( strMessage );
				} catch( NullReferenceException ) {
				}
            }
        }

        public void Test() {
            BbcBasic5 oBasic = new BbcBasic5();
            oBasic.Test( _oSiteBase );
        }

        public static bool IsStateMatch( MemoryState<char> oMState, string strName ) {
            return string.Compare( oMState.StateName, strName, ignoreCase:true ) == 0 ;
        }

        /// <summary>
        /// Look for the top of the parse tree and send that on to our
        /// assembler. Need to pull this out of my primary basic editor
        /// and put it in it's own document...
        /// </summary>
        /// <remarks>I seem to recall I used to "assemble" some assembler
        /// in that input param. But when I changed this to work on basic
        /// that makes this whole thing weird.
        /// TODO: Kind of weird I'm not just using the colorized parse
        /// </remarks>
        public void Compile() {
            try {
                RenumberAndSumate();

                State<char>      oStart  = _oBasicGrammer.FindState("start") ?? throw new InvalidOperationException( "Couldn't find start state" );
                DataStream<char> oStream = CreateColumnStream( BasicRow.ColumnText );
                Parser2          oParse  = new Parser2( oStart, oStream );

                foreach( int iProgress in oParse ) {
                }

                BasicCompiler oCompiler = new BasicCompiler( oStream );

                //oCompiler.Walk( oParse.MStart );
                oCompiler.Test();
                oCompiler.Save( _oSiteFile.FilePath );
            } catch( Exception oEx ) {
                Type[] rgErrors = { typeof( NullReferenceException ),
                                    typeof( ArgumentOutOfRangeException ),
                                    typeof( InvalidDataException ) };
                if( rgErrors.IsUnhandled( oEx ) )
                    throw;
                LogError( "Problem with compile" );
            }
        }

        public bool Execute(Guid sGuid) {
            if( sGuid == GlobalCommands.Play ) {
                Compile( ); 
                return true;
            }
            return false;
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
    }

    /// <summary>
    /// A parser to parse the document all in one go.
    /// </summary>
    public class Parser2 :
        IEnumerable<int> 
    {
        MyStack<MemoryElem<char>> _oStack = new MyStack<MemoryElem<char>>();
        int                       _iInput = 0;
        DataStream<char>          _oStream; 

        public OnParserException? ExceptionEvent;

        public MemoryState<char> MStart { get; }

        public Parser2( State<char> oStart, DataStream<char> oStream ) {
            _oStream = oStream ?? throw new ArgumentNullException();
            if( oStart == null )
                throw new ArgumentNullException();

			MStart = new MemoryState<char>( new ProdState<char>( oStart ), null );

            _oStack.Push( MStart );
        }

		/// <remarks>
		/// Handy, especially since I don't actually have a proper site. 
		/// </remarks>
        protected void LogException( Exception oEx, int iInput ) {
			ExceptionEvent?.Invoke(oEx, iInput);
		}

        protected bool Push( Production<char> oProduction, MemoryElem<char> oEParent )
        {
            try {
                MemoryState<char> oMParent = (MemoryState<char>)oEParent;

                oMParent.PathID = oProduction.Index;

                for( int iProdElem = oProduction.Count - 1; iProdElem >= 0; --iProdElem ) {
                    ProdElem<char>    oProdElem = oProduction[iProdElem];
                    MemoryElem<char>? oNextElem  = null;

					if( oProdElem is ProdState<char> oProdState ) {
					    oNextElem = new MemoryState   <char>(oProdState, oMParent );
					} else {
						oNextElem = new MemoryTerminal<char>(oProdElem,  oMParent );
					}

					if( oNextElem == null ) {
                        throw new InvalidOperationException();
                    }

                    _oStack.Push( oNextElem );

                    MemoryElem<char> oTemp = oMParent.Children;
                    oMParent.Children = oNextElem;
                    oNextElem.Next    = oTemp;
                }

                return true;
            } catch( Exception oEx ) {
                Type[] rgErrors = { typeof( ArgumentNullException ),
                                    typeof( NullReferenceException ),
                                    typeof( InvalidOperationException ),
                                    typeof( InvalidCastException ) };
				if( rgErrors.IsUnhandled( oEx ) )
					throw;
                LogException( oEx, _iInput );
            }
            return false;
        }
        public IEnumerator<int> GetEnumerator() {
            while( _oStack.Count > 0 ) {
                MemoryElem<char>  oNonTerm    = _oStack.Pop(); 
                Production<char>? oProduction = null;
		        int               iMatch      = 0;

		        if( oNonTerm.IsEqual( 30, _oStream, false, _iInput, out iMatch, out oProduction) ) {
                    if( oProduction == null ) { // it's a terminal or a binder
					    _iInput += iMatch;
				    } else {                    // it's a state.
					    if( !Push( oProduction, oNonTerm ) )
                            yield break;
				    }
                } else {
				    // This won't stop infinte loops on the (empty) terminal. But will stop other errors.
				    if( !_oStream.InBounds( _iInput ) )
					    throw new IndexOutOfRangeException();
			    }
			    yield return _iInput;
            }
        }

        IEnumerator IEnumerable.GetEnumerator() {
            return GetEnumerator();
        }
    }

    /// <summary>
    /// Walk the parse tree and attempt to build the program...
    /// </summary>
    public class BasicCompiler 
    {

        /// <summary>
        /// Right now assume patches are all 2 bytes...
        /// </summary>
        public abstract class Patch {
            public readonly int    iPotHole; // The addr that must be patched
            public readonly string strLabel; // The referenced label
            public Patch( int p_iPotHole, string p_strLabel ) {
                iPotHole = p_iPotHole; 
                strLabel = p_strLabel;
            }

            public abstract void Apply( List<byte> rgProgram, int iAddr );
        }

        public class PatchAbs : Patch {
            public PatchAbs(int p_iPotHole, string p_strLabel) : 
                base(p_iPotHole, p_strLabel) {
            }

            public override void Apply( List<byte> rgProgram, int iAddr) {
                rgProgram[iPotHole  ] = (byte) iAddr;
                rgProgram[iPotHole+1] = (byte)(iAddr >> 8);
            }
        }

        public class PatchRel : Patch {
            public PatchRel(int p_iPotHole, string p_strLabel) : 
                base(p_iPotHole, p_strLabel) {
            }

            public override void Apply( List<byte> rgProgram, int iAddr) {
                int iOffset = iAddr - iPotHole;

                if( int.Abs( iOffset ) > 128 )
                    throw new InvalidProgramException("Offset must be less than 128");

                rgProgram[iPotHole] = (byte)iOffset;
            }
        }

        DataStream<char>          _oStream;
        Dictionary< string, int > _rgVariables = []; // addresses.
        List<byte>                _rgProgram   = [];
        List< Patch >             _rgPatches   = [];
        Dictionary< string, int > _rgLabels    = [];
        int                       _iStackAddr  = 1000;
        int                       _iVarsAddr   = 1001;
        int                       _iVAddr; // Start at the stack and go down.
        Z80Definitions            _rgZ80Definitions;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="oStream">The basic program to be compiled.</param>
        /// <exception cref="ArgumentNullException"></exception>
        public BasicCompiler( DataStream<char> oStream ) {
            _oStream = oStream ?? throw new ArgumentNullException();
            _iVAddr = _iStackAddr;
            _rgZ80Definitions = new Z80Definitions();
        }

        public bool Save( string strFilePath ) {
            string strBinaryFile = Path.ChangeExtension( strFilePath, "bin" );

            using FileStream   oBinaryStream = File.OpenWrite( strBinaryFile );
            using BinaryWriter oWriter       = new BinaryWriter( oBinaryStream );

            foreach( byte bValue in _rgProgram ) {
                oWriter.Write( bValue );
            }
            oWriter.Flush();
            return true;
        }

        public void Test() {
            AddMain( 0x26, 0x08 );      // ld h, 8
            AddMain( 0xdd, 0x1e, 0x7 ); // ld e, 7
            AddMain( 0xcd );            // call
            UseLabl( "H_times_E" );
            AddMain( 0x76 );            // halt
            H_times_E();

            ApplyPatches();
        }

        public void Walk( MemoryState<char> oStart ) {
            if( oStart == null )
                throw new ArgumentNullException();

            MemoryElem<char> oNode = oStart;
            if( IsState( oNode, "start" ) ) {
                oNode = oNode.Children;
                while( oNode != null ) {
                    if( IsState( oNode, "bbcbasic" ) ) {
                        oNode = oNode.Children;
                        if( IsState( oNode, "let" ) ) {
                            WalkLet( oNode );
                        }
                        if( IsState( oNode, "print" ) ) {
                            WalkPrint( oNode );
                        }
                    }
                    oNode = oNode.Next;
                }
            }
        }

        protected void WalkPrint( MemoryElem<char> oNode ) {
            oNode = oNode.Children;

            CheckValue( oNode, "PRINT" );
            oNode = oNode.Next.Next;
            string strValue = GetValue( oNode );
            if( !_rgVariables.ContainsKey( strValue ) )
                throw new InvalidDataException( "Variable not defined" );
        }

        protected void AddVari( string strVariable, byte bValue ) {
            _rgProgram[_iVarsAddr] = bValue;
            _rgVariables.Add( strVariable, _iVarsAddr-- );
        }

        /// <summary>
        /// Copy the variable's address to the program
        /// </summary>
        /// <param name="strVariable"></param>
        protected void MemAddr( string strVariable ) {
            int iAddr = _rgVariables[strVariable];

            _rgProgram.Add( (byte)iAddr );
            _rgProgram.Add( (byte)(iAddr >> 8 ) );
        }

        /// <summary>
        /// Identify a portion of memory with a label.
        /// </summary>
        protected void AddLabl( string strLabel ) {
            _rgLabels.Add( strLabel, _rgProgram.Count );
        }

        /// <summary>
        /// Add an area in the program that will need to
        /// be patched in a second pass.
        /// </summary>
        /// <param name="strLabel">Name of the patch,
        /// NOTE: It might NOT be defined yet! So at this
        /// point, there is a possibility, you have patch
        /// requesnts that cannot be satisfied.</param>
        protected void UseLabl( string strLabel ) {
            _rgPatches.Add( new PatchAbs( _rgProgram.Count, strLabel ) );
            _rgProgram.Add( 0x00 );
            _rgProgram.Add( 0x00 );
        }

        /// <summary>
        /// I think I'll need another patch type...
        /// </summary>
        /// <param name="strLabel"></param>
        protected void JumpRel( string strLabel ) {
            _rgPatches.Add( new PatchRel( _rgProgram.Count, strLabel ) );
            _rgProgram.Add( 0x00 );
        }

        /// <summary>Add a byte, it's up to caller to make
        /// sure added values are proper instructions!! </summary>
        protected void AddMain( byte bValue ) {
            _rgProgram.Add( bValue );
        }
        protected void AddMain( byte bVal1, byte bVal2 ) {
            _rgProgram.Add( bVal1 );
            _rgProgram.Add( bVal2 );
        }
        protected void AddMain( byte bVal1, byte bVal2, byte bVal3 ) {
            _rgProgram.Add( bVal1 );
            _rgProgram.Add( bVal2 );
            _rgProgram.Add( bVal3 );
        }

        public void ApplyPatches() {
            foreach( Patch oPatch in _rgPatches ) {
                int iAddr = _rgLabels[oPatch.strLabel];
                oPatch.Apply( _rgProgram, iAddr );
            }
        }

        /// <see cref="Z80Definitions.FindInst(z80.Z80Memory, int)" />
        protected Z80Instr FindInst( int iAddr ) {
            byte iLowByte = _rgProgram[iAddr];

            switch( iLowByte ) {
                case 0xec:
                    return _rgZ80Definitions.BitI( _rgProgram[iAddr + 1]);
                case 0xed:
                    return _rgZ80Definitions.Misc( _rgProgram[iAddr + 1]);
                case 0xdd:
                    return _rgZ80Definitions.ExDD( _rgProgram[iAddr + 1]);
                case 0xfd:
                    return _rgZ80Definitions.ExFD( _rgProgram[iAddr + 1]);
                default: 
                    return _rgZ80Definitions.FindMain( iLowByte );
            }
        }


        protected void WalkLet( MemoryElem<char> oNode ) {
            oNode = oNode.Children;

            CheckValue( oNode, "LET" );
            oNode = oNode.Next.Next;
            CheckState( oNode, "assign" );
            oNode = oNode.Children;
            MemoryElem<char> oVar = oNode;
            CheckState( oVar, "vdecl" );

            string strVar = GetValue( oVar );
            if( _rgVariables.ContainsKey( strVar ) )
                throw new InvalidDataException( "Variable already defined" );

            _rgVariables.Add( strVar, _iVAddr );
            _iVAddr += 2; // 16 bit addr.

            if( oVar.Children is not null && 
                oVar.Children.Next is not null ) 
            {
                MemoryElem<char> oType = oVar.Children.Next;
                string strType = GetValue( oType ); // currently unused.
            }

            oNode = oNode.Next.Next;
            CheckValue( oNode, "=" );
            oNode = oNode.Next.Next;
            WalkExpression( oNode );

            AddMain( 0xe1 );                               // Pop HL
            AddMain( 0x22, 
                     (byte) _rgVariables[strVar], 
                     (byte)(_rgVariables[strVar] >> 8 ) ); // LD (ADDR), HL
        }

        /// <summary>
        /// Walk the expression generating code that will
        /// put the result in HL.
        /// </summary>
        /// <exception cref="InvalidDataException"></exception>
        protected void WalkExpression( MemoryElem<char> oNode ) {
            int iPathID = oNode.PathID;

            CheckState( oNode, "expression" );
            oNode = oNode.Children.Next;
            WalkTerm( oNode );

            switch( iPathID ) {
                case 0:
                    oNode = oNode.Next.Next;
                    string strOp = GetValue( oNode );

                    oNode = oNode.Next.Next;
                    WalkExpression( oNode );

                    if( strOp.Equals( "+" ) ) {
                        AddMain( 0xd1 ); // pop de (recent expr result)
                        AddMain( 0xe1 ); // pop hl (older  expr result)
                        AddMain( 0x19 ); // add hl, de
                        AddMain( 0xe5 ); // push hl
                    }
                    if( strOp.Equals( "-" ) ) {
                        // Sub HL, BC // is order correct? HL = HL - BC
                        // Push HL
                        AddMain( 0xb7 ); // or  a  (clear carry)
                        AddMain( 0xd1 ); // pop de (recent expr result)
                        AddMain( 0xe1 ); // pop hl (older  expr result)
                        AddMain( 0x42 ); // sbc HL, DE
                        AddMain( 0xe5 ); // push hl
                    }
                    break;
                case 1:
                    oNode = oNode.Next;
                    if( oNode != null )
                        throw new InvalidDataException( "Confused" );
                    break;
            }
        }
        
        /// <summary>
        /// http://z80-heaven.wikidot.com/advanced-math
        /// </summary>
        protected void H_times_E() {
            // Inputs:
            //   H and E
            // Outputs:
            //   HL is the product
            //   D is 0
            //   B is 0
            //   A,E,C are preserved
            // 12 bytes

            AddLabl( "H_times_E" );

            AddMain( 0x16, 0 ); // ld d,0
            AddMain( 0x6a );    // ld l,d (smaller instr than "ld l, 0" !)
            AddMain( 0x06, 8 ); // ld b,8

            AddLabl( "H_Loop" );

            AddMain( 0x29 );    // add hl,hl (like shift left)
            AddMain( 0x30, 3 ); // jr nc,$+3 (on to the next)
            AddMain( 0x19 );    // add hl,de (carry was set, add e)

            AddMain( 0x10 );    // 0x10 djnz iLoop (rel jump. loop on b)
            JumpRel( "H_Loop" );
            AddMain( 0xc9 );    // ret
        }

        protected void Asm( string _ ) {
        }

        public class FuncMaker : IDisposable {
            readonly State<char> _oStatement;
            readonly int _iBindLabel;
            readonly int _iBindInstr;
            readonly int _iBindParam;

            readonly State<char> _oParam;
            readonly int _iBindHex;
            readonly int _iBindValue;
            readonly int _iBindIndir;

            public FuncMaker( string strName, Grammer<char> oGrammar ) {
			    _oStatement = oGrammar.FindState( "statement" );
                _iBindLabel = _oStatement.Bindings.IndexOfKey( "label" );
                _iBindInstr = _oStatement.Bindings.IndexOfKey( "instr" );
                _iBindParam = _oStatement.Bindings.IndexOfKey( "params" );

                _oParam     = oGrammar.FindState( "param" );
                _iBindHex   = _oStatement.Bindings.IndexOfKey( "hex" );
                _iBindIndir = _oStatement.Bindings.IndexOfKey( "indir" );
                _iBindValue = _oStatement.Bindings.IndexOfKey( "value" );
            }

            public void Dispose() {
            }

            public static string GetStringBinding( 
                DataStream<char>  rgTextStream,
                MemoryState<char> oMemState, 
                int               iBindIndex 
            ) {
                try {
                    // Only the memory element has the stream offset. IColorRange is a line offset.
                    if( oMemState.GetValue( iBindIndex ) is MemoryElem<char> oMemory )
                        return rgTextStream.SubString( oMemory.Start, oMemory.Length );
                } catch( Exception oEx ) {
                    Type[] rgErrors = { typeof( NullReferenceException ),
                                        typeof( ArgumentOutOfRangeException ),
                                        typeof( InvalidProgramException ),
                                        typeof( InvalidCastException ) };
                    if( rgErrors.IsUnhandled( oEx ) )
                        throw;
                }
                return string.Empty;
            }
        
            public struct ParamType {
                public bool   fIndirect;
                public string strValue;
                public bool   fHex;
            }

            public void Asm( string strValue ) {
                TestCharStream      oStream = new ( strValue );
                MemoryState<char>   oMStart = new ( new ProdState<char>( _oStatement ), null );
                Parser2             oParse  = new ( _oStatement, oStream );

                foreach( int iProgress in oParse ) {
                }

                string strLabel = GetStringBinding( oStream, oMStart, _iBindLabel );
                string strInstr = GetStringBinding( oStream, oMStart, _iBindInstr );

                List<ParamType> rgParms = [];

                foreach( MemoryState<char> oParam in oMStart.EnumValues( _iBindParam ) ) {
                    ParamType oType = new();

                    oType.fHex      = oParam.GetValue( _iBindHex   ) is not null;
                    oType.fIndirect = oParam.GetValue( _iBindIndir ) is not null;
                    oType.strValue  = GetStringBinding( oStream, oParam, _iBindValue );

                    rgParms.Add( oType );
                }
            }

            public void Label( string strValue ) {
            }

            public void DB( string strLabel, byte bValue ) {
            }

        }

        protected void Rnd255() {
            AddLabl( "Rnd255" );

            AddVari( "seed", 0x42 );

            AddMain( 0x3a );       // ld a, (seed)
            MemAddr( "seed" );
            AddMain( 0xa7 );       // and a
            AddMain( 0x28 );       // jr z, .fix
            JumpRel( ".fix" );

            AddMain( 0xcb, 0x3f ); // srl a
            AddMain( 0x30 );
            JumpRel( ".save" );
            AddMain( 0xee, 0xb8 ); // XOR with the polynomial 

            AddLabl( ".seed" );
            AddMain( 0x32 );       // ld (seed), a
            MemAddr( "seed" );
            AddMain( 0xc9 );       // ret

            AddLabl( ".fix" );
            AddMain( 0xdd, 0x3e, 0x01 ); // ld a, 1
            AddMain( 0x32 );             // ld (seed), a
            MemAddr( "seed" );
            AddMain( 0xc9 );             // ret
        }

        /// <summary>
        /// Factors (+-) always end up on the left,
        /// Terms   (*/) always end up on the right.
        /// </summary>
        /// <param name="oNode"></param>
        /// <exception cref="InvalidDataException"></exception>
        protected void WalkTerm( MemoryElem<char> oNode ) {
            int iPathID = oNode.PathID;
            oNode = oNode.Children.Next;
            WalkFactor( oNode ); // Left side

            switch( iPathID ) {
                case 0:
                    oNode = oNode.Next.Next;
                    string strOp = GetValue( oNode );
                    oNode = oNode.Next;
                    WalkTerm( oNode ); // right side.

                    if( strOp.Equals( "*" ) ) {
                        AddMain( 0xe1 ); // Pop into HL
                        AddMain( 0xd1 ); // Pop into DE
                        H_times_E();
                        AddMain( 0xe5 ); // Push HL
                    }
                    if( strOp.Equals( "/" ) ) {
                        // stack     = recent expr result
                        // stack + 2 = older  expr result
                        AddMain( 0xe1 ); // Pop HL
                        // Call Div
                        AddMain( 0xe5 ); // Push HL
                    }
                    break;
                case 1:
                    oNode = oNode.Next;
                    if( oNode != null )
                        throw new InvalidDataException( "confused" );
                    break;
            }
        }

        /// <summary>
        /// Get the factor and push the result into HL
        /// </summary>
        /// <param name="oNode"></param>
        protected void WalkFactor( MemoryElem<char> oNode ) {
            int iPathID = oNode.PathID;
            CheckState( oNode, "factor" );
            oNode = oNode.Children;
            iPathID = oNode.PathID;
            CheckState( oNode, "primaryfactor" );
            oNode = oNode.Children;

            switch( iPathID ) {
                case 0:
                    CheckValue( oNode, "(" );
                    oNode = oNode.Next;
                    WalkExpression( oNode );
                    break;
                case 1: {
                    string strVar = GetValue( oNode );
                    int iValue = int.Parse( strVar );

                    AddMain( 0x21, 
                             (byte)(iValue & 0xff ), 
                             (byte)(iValue >> 8 ) ); // ld hl, nn
                    AddMain( 0xe5 );         // push hl
                    } 
                    break;
                case 3 : {    
                    string strVar = GetValue( oNode );
                    int iAddr = _rgVariables[strVar];

                    AddMain( 0x2a, 
                             (byte)_rgVariables[strVar],
                             (byte)(_rgVariables[strVar] >> 8 ) ); // ld hl, (nn)
                    AddMain( 0xe5 );        // push hl
                    }
                    break;
                case 4:
                    WalkBuiltInFunction( oNode.Children );
                    break;
            }
        } // end method

        public void WalkBuiltInFunction( MemoryElem<char> oNode ) {
            CheckState( oNode, "built-in-function-call" );
            oNode = oNode.Children;

            string strValue = GetValue( oNode ); // built in name...

            oNode = oNode.Next.Next.Next.Next;
            CheckState( oNode, "param-first" );
            oNode = oNode.Children;
            oNode = oNode.Next;

            // BUG: Walk actual expression, but now
            // just simple number, variable or built in.
            CheckState( oNode, "expression" );
            string strParam = GetValue( oNode );
            int    iParam   = int.Parse( strParam );

            AddMain( 0xcd );   // call nn
            UseLabl( "Rnd255" );
        }

        /// <remarks>Assumes the element does NOT span multiple lines. :-( </remarks>
        public string GetValue( MemoryElem<char> oElem ) {
            return _oStream.SubString( oElem.Start, oElem.Length );
        }

        public bool IsValue( MemoryElem<char> oElem, string strTest ) {
            return string.Compare( GetValue( oElem ), strTest, ignoreCase:true ) == 0;
        }
        
        public bool IsState( MemoryElem<char> oElem, string strTest ) {
            return string.Compare( oElem.StateName, strTest, ignoreCase:true ) == 0;
        }

        public void CheckState( MemoryElem<char> oElem, string strTest ) {
            if( !IsState( oElem, strTest ) )
                throw new InvalidDataException( "Confused" );
        }
        public void CheckValue( MemoryElem<char> oElem, string strTest ) {
            if( !IsValue( oElem, strTest ) )
                throw new InvalidDataException( "Confused" );
        }
    }
}