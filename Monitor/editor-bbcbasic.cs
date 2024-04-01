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
            int _iStatement;
            int _iContinue;
            int _iFCallName;
            int _iFParams;
            int _iNumber;

            Editor.Manipulator    _oMechBulk;
            BaseEditor.LineStream _oStream;

            public Compiler( Grammer<char>         oGrammer, 
                             BaseEditor.LineStream oStream,
                             Editor                oMachineCode           
            ) { 
                _oStream = oStream  ?? throw new ArgumentNullException( nameof( oStream ) );

                if( oGrammer == null )
                    throw new ArgumentNullException( nameof( oGrammer ) );
                if( oMachineCode == null )
                    throw new ArgumentNullException( "Machine code file" );

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