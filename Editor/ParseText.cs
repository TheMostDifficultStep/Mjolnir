using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using System.IO;

using Play.Interfaces.Embedding;
using Play.Parse;
using Play.Parse.Impl;
using Play.Edit;

namespace Play.Integration {
    // 2/14/2017 : Nothing using this at the moment. 
	// 9/29/2018 : But pretty snazzy 'eh!
    public class WordCounter :
        IParseEvents<char>
    {
        readonly Grammer<char>       _oWordGrammar;
        readonly IPgRoundRobinWork   _oWorkPlace;
        readonly Editor              _oEditor;
        readonly Editor.LineStream   _oStream;

        ParseIterator<char> _oParseIter;

        public WordCounter( Editor oEditor ) {
            _oEditor = oEditor ?? throw new ArgumentNullException();
			// Set up a listener on the editor so we know when to parse.

			try {
				_oWordGrammar = (Grammer<char>)((IPgGrammers)oEditor.Services).GetGrammer( "line_breaker" );
			} catch ( Exception oEx ) {
                Type[] rgErrors = { typeof( NullReferenceException ),
                                    typeof( InvalidCastException ),
                                    typeof( ArgumentNullException ),
									typeof( ArgumentException ),
									typeof( FileNotFoundException ),
									typeof( GrammerNotFoundException ) };
                if( !rgErrors.Contains( oEx.GetType() ) )
                    throw;

				throw new InvalidOperationException( "Could not get grammar from editor services." );
			}

			try {
				_oWorkPlace = ((IPgScheduler)oEditor.Services).CreateWorkPlace();
			} catch( InvalidCastException ) {
				throw new InvalidOperationException( "Could not get grammar from editor services." );
			}
            _oStream = _oEditor.CreateStream();
        }

        public bool InitNew() {
			State<char> oStart = _oWordGrammar.FindState("start");

            _oParseIter = new ParseIterator<char>( _oStream, this, new MemoryState<char>( new ProdState<char>( oStart ), null ) );
            _oWorkPlace.Queue( null , 0 );

            return( true );
        }

        public void OnWork() {
            //int iWork = 0;
			if( _oParseIter != null ) {
                while( _oParseIter.MoveNext() ) {
                    //++iWork;
                    //if( iWork == 50 )
                    //    break;
                }
                OnFinish();
            }
        }

        private void OnFinish() {
            _oWorkPlace.Stop();
            _oEditor.WordCountUpdate();
        }

        /// <summary>
        /// Clearly if our counting get's interrupted while we're doing it we might be off. 
        /// I'm thinking the cache manager will heal any broken lines.
        /// </summary>
        void IParseEvents<char>.OnMatch( ProdBase<char> p_oElem, int p_iStart, int p_iLength ) {
            if( p_oElem.ID == "w" ) {
                int  iOffset = 0;
                Line oLine   = _oStream.SeekLine(p_iStart, out iOffset);

                if( oLine != null ) {
                    oLine.WordCount++;
                }
            }
        } 

        void IParseEvents<char>.OnParserError( ProdBase<char> p_oMemory, int p_iStart ) {
        }
    }

    /// <summary>
    /// This object is paired with an editor, so that the document can be parsed.
    /// 1/18/2016 : Replaced all winform controls for listing syntax errors, productions and 
    ///             outlining with my editor. This'll make it easier to implement tool windows.
    ///             for a multiview environment. TreeView is the only unmatched control.
	/// 9/29/2018 : Use new IPgParent.Services to access the grammars!!
    /// </summary>
    public class ParseHandlerText :
        IParseEvents<char>,
		IPgLoad<string>,
        IDisposable
    {
        readonly protected Editor        _oDocument;
        readonly protected Grammer<char> _oTextGrammar;

        readonly protected IPgRoundRobinWork _oWorkPlace; 
        readonly protected Editor.LineStream _oStream;

        private BufferEvent _oDocEvent;
        private DateTime    _dtStartParse;
        private bool        _fDisabled = false;
        
        // 1/18/2016 : Each is gonna need a site to be actually usable. Just starting this migration.
        private BaseEditor _rgDocSyntax  = null;
        private BaseEditor _rgDocProds   = null;
        private BaseEditor _rgDocOutline = null;

		private bool _fTraceProductions = false;

        private readonly StringBuilder _sbBytes = new StringBuilder();

		/// <summary>
		/// Standard text parser. See the ParseTags.cs file for more on the HTML parser.
		/// </summary>
		/// <exception cref="InvalidOperationException" />
        /// <exception cref="ArgumentOutOfRangeException" />
        /// <exception cref="ArgumentNullException" />
        public ParseHandlerText( Editor oDocument, string strLangName ) {
			_oDocument = oDocument ?? throw new ArgumentNullException( "Parser listener needs an editor to monitor" );

			try {
				_oTextGrammar = (Grammer<char>)((IPgGrammers)oDocument.Services).GetGrammer( strLangName );
                if ( _oTextGrammar == null )
                    throw new ArgumentOutOfRangeException();
			} catch ( Exception oEx ) {
                Type[] rgErrors = { typeof( NullReferenceException ),
                                    typeof( InvalidCastException ),
                                    typeof( ArgumentNullException ),
									typeof( FileNotFoundException ),
									typeof( GrammerNotFoundException ) };
                if( !rgErrors.Contains( oEx.GetType() ) )
                    throw;

				throw new InvalidOperationException( "Couldn't get grammar for parser." );
			}
            _oWorkPlace = ((IPgScheduler)_oDocument.Services).CreateWorkPlace() ?? throw new InvalidOperationException( "Need the scheduler service in order to work. ^_^;" );
            _oStream    = _oDocument.CreateStream() ?? throw new InvalidProgramException( "Parser Listener couldn't create a stream from the document" );

            // BUG BUG!! This is kind of evil. Really should be in an InitNew() stage. But since last step
            //           before exit we should be ok. And moving it now is a bear.
            _oDocument.BufferEvent += _oDocEvent = new BufferEvent(this.OnEvent);   
        }

        public Grammer<char> Grammer => _oTextGrammar;

		public BaseEditor ProductionsEdit {
			set {
				if( value == null )
					_fTraceProductions = false;

				_rgDocProds = value;
			}
		}
		public bool ProductionsTrace {
			get => _fTraceProductions;
			set {
				if( _rgDocProds != null ) {
					_fTraceProductions = value;
					if( value == true )
						_oWorkPlace.Queue( CreateWorker(), 0 ); // Force reparse.
				} else
					_fTraceProductions = false;
			}
		}

		/// <remarks>As much as I would like to do away with this step, it's best to keep
		/// these kind of pointer exchanges AFTER the constructor succeeds, else we might
		/// have a partially constructed object that threw an exception recieving events!
		/// </remarks>
        public bool InitNew() {
            _oWorkPlace.Queue( CreateWorker(), 0 );
            return true;
        }

		public bool Load(string oStream) {
			return false;
		}

        public bool DisableParsing {
            get { return ( _fDisabled ); }
            set {
                _fDisabled = value;

                if( _fDisabled ) {
                    _oDocument.ClearFormatting();
                } else {
                    _oWorkPlace.Queue( CreateWorker(), 0 );
                }
            } 
        }

        /// <summary>
        /// Note that the parser calls _Doc.Raise( BUFFEREVENTS.FORMATTED ) in OnFinished()
        /// we end up back here!! So be careful!! Note also, that we're not forwarding
        /// that FORMATTED event back out to the views here. Views only get the event
        /// via their IBufferEvents interface.
        /// </summary>
        public void OnEvent( BUFFEREVENTS eEvent ) {
            // 10/14/2018 : I should re-test the large file case. The scheduler is much
			// smarter now and I think it wouldn't be a problem.
            if (_oDocument.Size > 200000 || _oDocument.Size == 0 || _fDisabled ) {
                return;
            }

            if( eEvent == BUFFEREVENTS.MULTILINE || eEvent == BUFFEREVENTS.SINGLELINE )
                _oWorkPlace.Queue( CreateWorker(), 2000 );
            if( eEvent == BUFFEREVENTS.LOADED ) {
                _oWorkPlace.Queue( CreateWorker(), 0 );

            // _oWorkSite.Queue( CreateWordCounterWorker(), 0 );
            }
        }

        public virtual void OnStart()
        {
			try {
				if( _fTraceProductions )
					_rgDocProds.Clear();
				if( _rgDocSyntax != null )
					_rgDocSyntax.Clear();
				if( _rgDocOutline != null )
					_rgDocOutline.Clear();

				_dtStartParse = DateTime.Now;

				_sbBytes.Length = 0;

				_oDocument.ClearFormatting();
			} catch( NullReferenceException ) {
                _oDocument.LogError( "Text Parser, OnStart() null reference!" );
			}
        }

        public virtual void OnFinish()
        {
            TimeSpan tsElapse = DateTime.Now.Subtract( _dtStartParse );

			_oDocument.Raise_BufferEvent( BUFFEREVENTS.FORMATTED );  

            if( _rgDocSyntax != null ) {
                // This is really a sort of fyi that should show up in the bottom of the screen for a moment.
                _rgDocSyntax.LineAppend( "Elapsed parse time: " + ShowElapsedTime( tsElapse ) );
            }
        }

        /// <summary>
        /// When a terminal/nonterminal matches we get the event here. TODO: Unfortunately
        /// since we are no longer capturing only terminals, we're going to
        /// capture states as well. This means if we have a state captured with color, but
        /// it has sub elements with color they might not colorize correctly without some 
        /// changes here.
        /// </summary>
        public virtual void OnMatch( ProdBase<char> p_oElem, int p_iStream, int p_iLength ) {
            // Note: We're super dependent on MemoryElem inheriting from IPgWordRange in most places.
            if( p_oElem is MemoryElem<char> oMemElem  ) { // Capture's both terms and states.
                //if( oMemElem.ColorIndex > 0  ) {
                    Line oLine = _oStream.SeekLine( oMemElem.Start, out int iLineOffset);

                    // Parser is totally stream based, talking to the base class. So have to do this here.
                    oMemElem.Offset = iLineOffset;
                    oLine.Formatting.Add(oMemElem);

                    // Fun little experiment.
                    if( oMemElem.ID == "byte" && oMemElem.Length == 8 ) {
                        int iValue = 0;
                        for( int i=0; i<oMemElem.Length; ++i ) {
                            iValue <<= 1;
                            if( oLine[oMemElem.Offset+i] == '1' ) {
                                iValue |= 1;
                            }
                        }
                        char cValue = (char)iValue;
                        _sbBytes.Append( (char)iValue );
                    }
                //}
            }

			if( _fTraceProductions && p_oElem.IsTerm ) {
				StringBuilder sbMessage = new StringBuilder();

				sbMessage.Append( p_iStream.ToString() );
				sbMessage.Append( " '" );
                if( p_iLength == 1 ) {
                    switch( _oStream[p_iStream] ) {
                        case '\n':
                            sbMessage.Append( @"\n" );
                            break;
                        case '\r':
                            sbMessage.Append( @"\r" );
                            break;
                        default:
                            sbMessage.Append( _oStream[p_iStream] );
                            break;
                    }
                } else {
				    sbMessage.Append( _oStream.SubString( p_iStream, p_iLength ) );
                }
				sbMessage.Append( "' -- " );
                sbMessage.Append( p_oElem.ToString() );

				_rgDocProds.LineAppend( sbMessage.ToString() ); // BUG: Make a line append that takes a stringbuilder.
			}

            // This is the version for the [ ] task manager outliner.
            if (p_oElem is MemoryState<char> oMemState) { 
                if ( oMemState.StateName == "checkbox" ) {
                    // Add unchecked elements into the outline.
                    if( _oStream[oMemState.Start + 1] == ' ' ) {
                        Line oLine = _oStream.SeekLine( oMemState.Start );
                        if( _rgDocOutline != null )
                            _rgDocOutline.LineAppend( oLine.ToString() );
                            // new ListNode( p_iStart, oLine.ToString() );
                    }
                }
            }
        } 

        /// <summary>
        /// Generate an error message on error
        /// </summary>
        /// <param name="p_oMemory">Element on the stack that failed.</param>
        /// <param name="p_lStart">It's position in the stream.</param>
        /// <remarks>Tho the returned p_oMemory is most basic. We know we are using productions
        /// which generate MemoryElemChar types. As such we can cast up with no worries. Note:
        /// reporting errors adds a relatively significant amount of time to the parse. The listview 
        /// probably being the worst offender with all it's interop.</remarks>
        public void OnParserError( ProdBase<char> p_oMemory, int p_iStart ) {
            StringBuilder sbMessage = new StringBuilder( 100 );

            sbMessage.Append( "Markup Error! " ); 

			if( p_iStart < 0 ) {
				sbMessage.Append( "Parse underflow." );
			}
			if( _oStream.InBounds( p_iStart ) ) {
				int  iOffset = 0;
				Line oLine   = _oStream.SeekLine(p_iStart, out iOffset);
				int  iLine   = oLine.At;

				oLine.Formatting.Add( new ColorRange( iOffset, 1, -4 ) ); // BUG: hard coded color.
				oLine.Formatting.Add( new ColorRange( 0, oLine.ElementCount, 0 )); // Add a black element so we at least paint something!!
				sbMessage.Append( iLine.ToString() ); 
				sbMessage.Append( ", Offset: " );
				sbMessage.Append( iOffset.ToString() );
			} else {
				Line oLine   = _oDocument[ _oDocument.ElementCount - 1 ];
				int  lOffset = oLine.ElementCount;

				if( lOffset > 0 )
					lOffset--;
				
				oLine.Formatting.Add( new ColorRange( lOffset, 1, -4 ) );

				sbMessage.Append( "Parse overflow." );
			}

            // The terminal passed us might be a memory term or a prod term.
            // If it's a memory term, go to it's host to find the production it came from.
            MemoryElem<char> l_oMemory = p_oMemory as MemoryElem<char>;
            ProdElem<char>   oProdElem = p_oMemory as ProdElem<char>;

            if( l_oMemory != null ) {
                oProdElem = l_oMemory.ProdElem;
            }

            int iElem = 0;

            if( oProdElem != null ) {
                Production<char> oProd = oProdElem.Host;

                if( oProd != null ) {
                    // We have the production in the state and now find the particular
                    // element that failed so we can report it relative to it's state.
                    for( iElem =0; iElem < oProd.Count; ++iElem ) {
                        if( oProd[iElem] == oProdElem ) {
                            iElem++;
                            break;
                        }
                    }
                    sbMessage.Append( ". State: \"" );
                    sbMessage.Append( oProd.StateName );
                    sbMessage.Append( "\". Production: " ); 
                    sbMessage.Append( oProd.Index.ToString() );
                    sbMessage.Append( ". Element: " );
                    sbMessage.Append( iElem.ToString() );
                }
            }

            if( _rgDocSyntax != null ) {
                _rgDocSyntax.LineAppend( sbMessage.ToString() );
            }
			if( _fTraceProductions ) {
				_rgDocProds.LineAppend( sbMessage.ToString() );
			}
        }

        /// <summary>
        /// Useful for debugging grammers.
        /// </summary>
        void OnProduction( Production<char> p_oProd, int p_iStart ) {
            if( _fTraceProductions ) {
				try {
					string strMessage = p_iStart.ToString() 
										+ " \"" 
										+ _oStream.SubString( p_iStart, 5) 
										+ "\" : " 
										+ p_oProd.ToString();
					_rgDocProds.LineAppend( strMessage );
				} catch( NullReferenceException ) {
				}
            }
        }

        /// <summary>
        /// Report more serious issues.
        /// </summary>
        void OnParserException( Exception oEx, int iStart ) {
            _oDocument.Site.LogError( "parser", oEx.Message );
        }

        // TODO: This might be a great thing to add to the document site to report to the shell parse times.
        //       in more consistant manner instead of ad-hoc as I'm doing now.
        private static string ShowElapsedTime(TimeSpan tsElapse)
        {
            StringBuilder sbElapse = new StringBuilder();

            if( tsElapse.Minutes != 0 ) {
                sbElapse.Append( tsElapse.Minutes.ToString() );
                sbElapse.Append( " Minutes, " );
            }
            if( tsElapse.Seconds != 0 ) {
                sbElapse.Append( tsElapse.Seconds.ToString() );
                sbElapse.Append( " Seconds, " );
            }
            if( tsElapse.Milliseconds != 0 ) {
                sbElapse.Append( tsElapse.Milliseconds.ToString() );
                sbElapse.Append( " Milliseconds." );
            }

            return( sbElapse.ToString() );
        }

        /// <summary>
        /// 2/14/2017 : New way to do worker iterators!!! The only drawback is that I can't 
        /// get a destroy method called if we bail out in the middle. 
        /// </summary>
        /// <returns></returns>
        public IEnumerator<int> CreateWorker() {
			State<char> oStart  = _oTextGrammar.FindState("start");

			if( oStart == null ) {
                if( _rgDocSyntax != null ) 
                    _rgDocSyntax.LineAppend( "Could not find grammar start state" );
                yield break;
            }

            BaseEditor.LineStream oStream  = _oDocument.CreateStream();
            MemoryState<char>     oMStart  = new MemoryState<char>( new ProdState<char>( oStart ), null );
            ParseIterator<char>   oParser  = new ParseIterator<char>( oStream, this, oMStart );

            oParser.ProductionEvent = OnProduction;
            oParser.ExceptionEvent  = OnParserException;

            yield return( 0 );

            OnStart();

            while( oParser.MoveNext() ) {
                yield return( 0 );
            }

            OnFinish();
			yield return( 0 );
        }

        public virtual void Dispose()
        {
            _oWorkPlace.Stop(); // Removes us from the worklist, should make it easier to free us up.

            _oDocument.BufferEvent -= _oDocEvent;
        }
	} // Text implementation.

    /// <summary>
    /// Unlike the ParseHandlerText, this object parses provides no iterator and parses
    /// all in one go.
    /// </summary>
    /// <seealso cref="ParseHandlerText"/>
	public class ParseSimpleText : IParseEvents<char> {
		public Editor Document { get; }

		protected BaseEditor.LineStream _oStream;
		protected State<char>			_oStart;

		public ParseSimpleText( Editor oText, Grammer<char> oLanguage ) {
			Document = oText ?? throw new ArgumentNullException( "Editor must not be null" );

			if( oLanguage == null )
				throw new ArgumentNullException( "Language must not be null" );

			_oStream = Document.CreateStream() ?? throw new InvalidOperationException( "Couldn't create stream on given document" );
			_oStart  = oLanguage.FindState("start") ?? throw new InvalidOperationException( "Couldn't find start state" );

		}

		public void OnMatch( ProdBase<char> oElem, int iStream, int iLength ) {
            MemoryElem<char> oMemElem  = oElem as MemoryElem<char>;
            if( oMemElem != null ) { // Capture's both terms and states.
                Line oLine = _oStream.SeekLine( oMemElem.Start, out int iLineOffset);

                // Parser is totally stream based, talking to the base class. So have to do this here.
                oMemElem.Offset = iLineOffset;
                oLine.Formatting.Add(oMemElem);
            }
		}

		public void OnParserError(ProdBase<char> p_oMemory, int p_iStart) {
            StringBuilder sbMessage = new StringBuilder( 100 );

            sbMessage.Append( "Markup Error! " ); 

			if( p_iStart < 0 ) {
				sbMessage.Append( "Parse underflow." );
			}
			if( _oStream.InBounds( p_iStart ) ) {
				Line oLine  = _oStream.SeekLine(p_iStart, out int iOffset);

                sbMessage.Append( "Line : " );
				sbMessage.Append( oLine.At.ToString() ); 
				sbMessage.Append( ", Offset: " );
				sbMessage.Append( iOffset.ToString() );
			} else {
				sbMessage.Append( "Parse overflow." );
			}

            // The terminal passed us might be a memory term or a prod term.
            // If it's a memory term, go to it's host to find the production it came from.
            MemoryElem<char> l_oMemory = p_oMemory as MemoryElem<char>;
            ProdElem<char>   oProdElem = p_oMemory as ProdElem<char>;

            if( l_oMemory != null ) {
                oProdElem = l_oMemory.ProdElem;
            }

            int iElem = 0;

            if( oProdElem != null ) {
                Production<char> oProd = oProdElem.Host;

                if( oProd != null ) {
                    // We have the production in the state and now find the particular
                    // element that failed so we can report it relative to it's state.
                    for( iElem =0; iElem < oProd.Count; ++iElem ) {
                        if( oProd[iElem] == oProdElem ) {
                            iElem++;
                            break;
                        }
                    }
                    sbMessage.Append( ". State: \"" );
                    sbMessage.Append( oProd.StateName );
                    sbMessage.Append( "\". Production: " ); 
                    sbMessage.Append( oProd.Index.ToString() );
                    sbMessage.Append( ". Element: " );
                    sbMessage.Append( iElem.ToString() );
                }
            }

			Document.Site.LogError( "Parsing", sbMessage.ToString(), false );
		}

		public virtual bool Parse() {
			try {
				MemoryState<char>   oMStart  = new MemoryState<char>( new ProdState<char>( _oStart ), null );
				ParseIterator<char> oParser  = new ParseIterator<char>( _oStream, this, oMStart );

				Document.ClearFormatting();

				while( oParser.MoveNext() );
			} catch( NullReferenceException ) {
				return false;
			}
			return true;
		}

	}
}
