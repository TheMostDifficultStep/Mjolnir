using Play.Interfaces.Embedding;
using Play.Edit; 
using Play.Parse;
using Play.Parse.Impl;
using System.Data.Common;
using System.Collections;

namespace Monitor {
    public class BasicRow : Row {
        public const int ColumnNumber = 0;
        public const int ColumnText   = 1;
        public BasicRow( int iBasicLine, ReadOnlySpan<char> rgText ) { 
            _rgColumns = new Line[2];

            string strLineNum = iBasicLine >= 0 ? iBasicLine.ToString() : "?";

            _rgColumns[0] = new TextLine( iBasicLine, strLineNum );
            _rgColumns[1] = new TextLine( 1,          rgText.ToString() );
        }

        public Line Text   => _rgColumns[ColumnText];
        public Line Number => _rgColumns[ColumnNumber];

        public static new int ColumnCount => 2;
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
                    if( Char.IsWhiteSpace( strLine[i] ) )
                        ++i;

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
        public override void DoParse() {
            _oWorkPlace.Queue( GetParseEnum(), iWaitMS:2000 );
        }

        public IEnumerator<int> GetParseEnum() {
            RenumberAndSumate();

            ParseColumn( BasicRow.ColumnText, _oBasicGrammer );

            Raise_DocFormatted();

            yield return 0;
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
        /// that makes this whole thing weird.</remarks>
        public void Compile() {
            try {
                RenumberAndSumate();

                State<char>      oStart  = _oBasicGrammer.FindState("start") ?? throw new InvalidOperationException( "Couldn't find start state" );
                DataStream<char> oStream = CreateColumnStream( BasicRow.ColumnText );
                Parser2          oParse  = new Parser2( oStart, oStream );

                foreach( int iProgress in oParse ) {
                }

                BasicCompiler oCompiler = new BasicCompiler( oParse.MStart, oStream );
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
        protected class CompilerOld {
            // BUG: Would be nice if checked if any -1. Make a version that
            // throws exception in the future!!
            int _iStatement;
            int _iContinue;
            int _iFCallName;
            int _iFParams;
            int _iNumber;

            Editor.Manipulator _oMechBulk;
            RowStream          _oStream;

            public CompilerOld( Grammer<char> oGrammer, 
                             RowStream     oStream,
                             List<byte>    rgBytes           
            ) { 
                _oStream = oStream  ?? throw new ArgumentNullException( nameof( oStream ) );

                if( oGrammer == null )
                    throw new ArgumentNullException( nameof( oGrammer ) );
                if( rgBytes == null )
                    throw new ArgumentNullException( "Bbc Basic file is needed." );

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
            } 

            public MemoryState<char> GetNode( MemoryState<char> oCurrent ) {
                return oCurrent.GetState( _iContinue );
            }

            public MemoryState<char> GetStatement( MemoryState<char> oCurrent ) {
                return oCurrent.GetState( _iStatement );
            }

            void LinePush(MemoryElem<char> oElem) {
                //_oMechBulk.LineAppend(GetString(_oStream, oElem));
            }

            void LineAppend( string strValue ) {
                //_oMechBulk.LineAppend( strValue );
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
                if( IsStateMatch( oExpression, "term" ) ) {
                }
                if( IsStateMatch( oExpression, "factor" ) ) {
                    MemoryElem<char> oNumber = oExpression.GetValue( _iNumber );
                    // Either a complicated state...
                    if( oNumber is MemoryState<char> oNumState ) {
                        if( IsStateMatch( oNumState, "vdecl" ) ) {
                            LinePush( oNumState );
                        }
                        if( IsStateMatch( oNumState, "built-in-function-call" ) ) {
                            FCallName( oNumState );
                        }
                    } else {
                        // Or a simple number
                        if( oNumber != null ) {
                            LinePush( oNumber );
                        }
                    }
                }
            }

            public void FCallName( MemoryState<char> oStatement ) {
                MemoryElem<char> oFCallName = oStatement.GetValue( _iFCallName );
                if( oFCallName != null ) {
                    LinePush( oFCallName );

                    foreach( MemoryElem<char> oParam in oStatement.EnumValues( _iFParams ) ) {
                        if( oParam is MemoryState<char> oExpression ) {
                            Expression( oExpression );
                        } else {
                            LinePush( oParam );
                        }
                    }
                }
            }
        }
    }

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

    public class BasicCompiler 
    {
        DataStream<char>          _oStream;
        Dictionary< string, int > _rgVariables = new ();
        int                       _iStackPointer = 100;
        List<string>              _rgTest = new List<string>();
        public BasicCompiler( MemoryState<char> oMStart, DataStream<char> oStream ) {
            if( oMStart == null )
                throw new ArgumentNullException();
            _oStream = oStream ?? throw new ArgumentNullException();

            Walk( oMStart );
        }

        protected void Walk( MemoryState<char> oStart ) {
            MemoryElem<char> oNode = oStart;
            while( oNode != null ) {
                if( IsState( oNode, "start" ) ) {
                    oNode = oNode.Children;
                    if( IsState( oNode, "basic" ) ) {
                        oNode = oNode.Children;
                        if( IsState( oNode, "let" ) ) {
                            WalkLet( oNode );
                        }
                    }
                }
                oNode = oNode.Next;
            }
        }

        protected void WalkLet( MemoryElem<char> oNode ) {
            oNode = oNode.Children;

          //CheckValue( oNode, "LET" );
            oNode = oNode.Next.Next;
            CheckState( oNode, "assign" );
            oNode = oNode.Children;
            MemoryElem<char> oVar = oNode;
            CheckState( oVar, "var" );

            int iVariable = _iStackPointer;
            string strVar = GetValue( oVar );
            _rgVariables.Add( strVar, _iStackPointer );
            _iStackPointer -= 2; // int16

            MemoryElem<char> oType = oVar.Children.Next;
            string strType = GetValue( oType );

            oNode = oNode.Next.Next;
            CheckValue( oNode, "=" );
            oNode = oNode.Next.Next;
            WalkExpression( oNode );

            // Pop BC
            // ld [_iStackPointer], BC
        }

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
                        // Pop BC
                        // Pop HL
                        // Add HL, BC
                        // Push HL
                        _rgTest.Add( "Pop 2" );
                        _rgTest.Add( "Add HL, BC" );
                        _rgTest.Add( "Push HL" );
                    }
                    if( strOp.Equals( "-" ) ) {
                        // Pop BC - recent expr result
                        // Pop HL - older  expr result
                        // Sub HL, BC // is order correct? HL = HL - BC
                        // Push HL
                        _rgTest.Add( "Pop 2" );
                        _rgTest.Add( "Sub HL, BC" );
                        _rgTest.Add( "Push HL" );
                    }
                    break;
                case 1:
                    oNode = oNode.Next;
                    if( oNode != null )
                        throw new InvalidDataException( "Confused" );
                    break;
            }
        }

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
                        // Pop  values.
                        // Call Mult
                        // Push HL
                        _rgTest.Add( "Pop 2 ints" );
                        _rgTest.Add( "Call Mult" );
                        _rgTest.Add( "Push HL" );
                    }
                    if( strOp.Equals( "/" ) ) {
                        // stack     = recent expr result
                        // stack + 2 = older  expr result
                        // Pop values.
                        // Call Div
                        // Push HL
                        _rgTest.Add( "Pop 2 ints" );
                        _rgTest.Add( "Call Div" );
                        _rgTest.Add( "Push HL" );
                    }
                    break;
                case 1:
                    oNode = oNode.Next;
                    if( oNode != null )
                        throw new InvalidDataException( "confused" );
                    break;
            }
        }

        protected void WalkFactor( MemoryElem<char> oNode ) {
            int iPathID = oNode.PathID;
            CheckState( oNode, "factor" );
            oNode = oNode.Children;

            switch( iPathID ) {
                case 0:
                    CheckValue( oNode, "(" );
                    oNode = oNode.Next;
                    WalkExpression( oNode );
                    break;
                case 1:
                    string strNumber = GetValue( oNode );
                    // LD   BC, int.Parse( strNumber ) // direct
                    // Push BC
                    _rgTest.Add( "LD   BC, int.Parse( strNumber )" );
                    _rgTest.Add( "Push BC"  );
                    break;
                case 2:
                    CheckState( oNode, "var" );
                    string strVar = GetValue( oNode.Children );
                    // LD   BC, _rgVariables[ strVar ] indirect
                    // Push BC
                    _rgTest.Add( "LD   BC, _rgVariables[ strVar ]" );
                    _rgTest.Add( "Push BC"  );
                    break;
                case 3:
                    CheckState( oNode, "built-in-function-call" );
                    break;
            }
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