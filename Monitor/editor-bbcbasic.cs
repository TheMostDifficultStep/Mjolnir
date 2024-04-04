using Play.Interfaces.Embedding;
using Play.Edit; 
using Play.Parse;
using Play.Parse.Impl;

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
        IPgLoad<BinaryReader>
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
            ParseColumn      ( BasicRow.ColumnText, _oBasicGrammer );

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
        public void Compile( Editor oMachineCode ) {
            try {
                if( _rgRows[0] is BasicRow oRow ) {
                    foreach( IColorRange oRange in oRow.Text.Formatting ) {
                        if( oRange is MemoryState<char> oMState ) {
                            if( IsStateMatch( oMState, "start" ) ) {
                                Compile( oMState, oMachineCode );
                                return;
                            }
                        }
                    }
                }
            } catch( Exception oEx ) {
                Type[] rgErrors = { typeof( NullReferenceException ),
                                    typeof( ArgumentOutOfRangeException ) };
                if( rgErrors.IsUnhandled( oEx ) )
                    throw;
            }
            LogError( "Problem starting compile" );
        }

        /// <summary>
        /// We'll try to make this Span based in the future. But too many changes for now.
        /// </summary>
        /// <remarks>Assumes the element does NOT span multiple lines. :-( </remarks>
        public static string GetString( RowStream oStream, MemoryElem<char> oElem ) {
            Line oLine = oStream.SeekLine( oElem.Start, out int iLineOffset);
            return oLine.SubString( oElem.Offset, oElem.Length );
        }

        /// <summary>
        /// This is my first feeble attempt at a compiler. I'm going to target
        /// my the Z80 (emulator) since I want to target the agon in the end.
        /// Doesn't do much at present...
        /// </summary>
        /// <remarks>
        /// TODO: Make the parser return the start block!! so we an walk the 
        /// parse tree!!
        /// </remarks>
        public void Compile( in MemoryState<char> oMStart, in Editor oMachineCode ) {
            State<char>       oClassStart = _oBasicGrammer.FindState( "start" );
            int               iStart      = oClassStart.Bindings.IndexOfKey( "bbcbasic" );
            MemoryState<char> oStart      = oMStart.GetState( iStart );
            RowStream         oStream     = CreateColumnStream( BasicRow.ColumnText );

            Compiler oBuild = new Compiler( _oBasicGrammer, oStream, oMachineCode );

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
            int _iStatement;
            int _iContinue;
            int _iFCallName;
            int _iFParams;
            int _iNumber;

            Editor.Manipulator _oMechBulk;
            RowStream          _oStream;

            public Compiler( Grammer<char> oGrammer, 
                             RowStream     oStream,
                             Editor        oMachineCode           
            ) { 
                _oStream = oStream  ?? throw new ArgumentNullException( nameof( oStream ) );

                if( oGrammer == null )
                    throw new ArgumentNullException( nameof( oGrammer ) );
                if( oMachineCode == null )
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
            
                _oMechBulk = oMachineCode.CreateManipulator();
            } 

            public MemoryState<char> GetNode( MemoryState<char> oCurrent ) {
                return oCurrent.GetState( _iContinue );
            }

            public MemoryState<char> GetStatement( MemoryState<char> oCurrent ) {
                return oCurrent.GetState( _iStatement );
            }

            void LinePush( MemoryElem<char> oElem ) {
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

}