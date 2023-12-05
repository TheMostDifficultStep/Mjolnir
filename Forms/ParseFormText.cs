using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Play.Edit;
using Play.Interfaces.Embedding;
using Play.Parse;
using Play.Parse.Impl;

namespace Play.Forms {

    /// <summary>
    /// This is a specialized parse implementation for the SSTV Properties form.
    /// It will treat each line as a separate entity of the whole. So I can deal with
    /// the rapid updates happening on some items while others starve to receive
    /// a parse!!
    /// </summary>
    public class ParseFormText : 
        ILineEvents,
        IDisposable
    {
        readonly Dictionary<Line, long>     _rgParseEvents = new();
        readonly ProdState<char>            _oStart;
        readonly MyStack<ProdBase<char>>    _oStack;
        readonly MyStack<ProdBase<char>>.MyStackEnum _oStackEnum;
		         int                        _iInput;
        readonly BaseEditor                     _oDocument;
        readonly SubStream                  _oStream;
        readonly List<Line>                 _rgCleanUp = new();
                 IPgRoundRobinWork          _oWorker; 

        public ParseFormText( IPgRoundRobinWork oWorker, BaseEditor oText, string strLangName ) {
            Grammer<char> oLanguage;
			try {
				oLanguage = (Grammer<char>)((IPgGrammers)oText.Services).GetGrammer( strLangName );
                if( oLanguage == null )
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
			State<char> oStart = oLanguage.FindState("start") ?? throw new InvalidOperationException( "Couldn't find start state for given grammar." );

            _oStart     = new ProdState<char>( oStart );
            _oStack     = new();
            _oStackEnum = _oStack.GetEnumerator();
            _oDocument  = oText ?? throw new ArgumentNullException( "Editor must not be null" );
            _oStream    = new SubStream( _oDocument[0] );

            _oWorker    = oWorker;
        }

        /// <summary>
        /// It is possible to call this more than once. But the document
        /// will only add the listener once.
        /// </summary>
        /// <returns></returns>
        public bool InitNew() {
            _oDocument.ListenerAdd( this );

            return( true );
        }

        /// <summary>
        /// No unmanaged resources so this simple Dispose will work fine.
        /// </summary>
        public void Dispose() {
            _oDocument.ListenerRemove( this );
            _oWorker = null;
        }

        /// <summary>
        /// Note: When I raise the Raise_BufferEvent I'll end up in this
        /// method on myself. Be careful.
        /// </summary>
        /// <param name="eEvent"></param>
        public void OnEvent(BUFFEREVENTS eEvent) {
        }

        /// <summary>
        /// In general these events will never happen on a form after it
        /// is constructed.
        /// </summary>
        public void OnLineDelete(Line oLine) {
        }

        /// <summary>
        /// In general these events will never happen on a form after it
        /// is constructed.
        /// </summary>
        public void OnLineNew(Line oLine) {
        }

        public void OnLineUpdated(Line oLine, int iOffset, int iOldLen, int iNewLen) {
            if( _rgParseEvents.ContainsKey(oLine) ) {
                _rgParseEvents[oLine] = DateTime.Now.Ticks;
            } else {
                _rgParseEvents.Add( oLine, DateTime.Now.Ticks );
            }

            // This will get the worker going if it has been queued
            // but not yet running.
            if( _oWorker != null && _oWorker.Status == WorkerStatus.PAUSED ) {
                _oWorker.Start( 2000 );
            }
        }

        protected void Push( Production<char> p_oProduction, ProdBase<char> p_oParent )
        {
            try {
                if( !string.IsNullOrEmpty( p_oParent.ID ) || p_oParent.IsBinding || p_oParent.IsWord ) {
                    // Note : I don't even need the memorystate for a state if it's not bound or binding:
                    //        I save the start position in the memorybinder.
                    _oStack.Push( new MemoryBinder<char>( _iInput, p_oParent ) );
                }

                for( int iProdElem = p_oProduction.Count - 1; iProdElem >= 0; --iProdElem ) {
                    ProdBase<char> oMemElem = p_oProduction[iProdElem];

					if( oMemElem is ProdState<char> oProdState ) {
						// Don't push the memory binder at this point, if we do, we have to
						// wade thru inactive sibling binders by counting frames and etc. Waste of time.
						if (!string.IsNullOrEmpty(oProdState.ID) || oProdState.IsBinding || oProdState.IsWord) {
							oMemElem = new MemoryState<char>(oProdState, p_oParent as MemoryState<char>);
						}
					} else {
						// At present the listeners are just tossing these into the line color info.
						// And we might be capturing them as well (if ID is non-null).
						oMemElem = new MemoryTerminal<char>(p_oProduction[iProdElem], p_oParent as MemoryState<char>);
					}

                    _oStack.Push( oMemElem );
                }
            } catch( Exception oEx ) {
                Type[] rgErrors = { typeof( ArgumentNullException ),
                                    typeof( NullReferenceException ),
                                    typeof( InvalidOperationException ) };
				if( rgErrors.IsUnhandled( oEx ) )
					throw;
                LogError( oEx, _iInput );
            }
        }

        protected void LogError( Exception oEx, int iIndex ) {
            string strLine  = "At Line: " + _iInput.ToString();
            string strError = oEx == null ? strLine : oEx.Message + " " + strLine; 

            _oDocument.Site.LogError( "Form Parse Error", strError );
        }

        protected void LogError( int iLine, int iOffset ) {
            _oDocument.Site.LogError( "Form Parse Error", "Line " + iLine.ToString() + " offset " + iOffset.ToString() );
        }

        public void OnParserError( ProdBase<char> p_oMemory, int p_iStart ) {
            // Put some squiggly lines down.
        }

        protected void Bind( ProdBase<char> oProdBase ) 
        {
            _oStackEnum.Reset();
            while( _oStackEnum.MoveNext() ) {
                try {
                    if( _oStackEnum.Current.Bind( oProdBase ) )
                        break;
                } catch( NullReferenceException oEx ) {
                    LogError( oEx, _iInput );
                }
            }
        }

        public virtual void OnMatch( ProdBase<char> p_oElem, int p_iStream, int p_iLength ) {
            int iLine = -1;
            try {
                if( !string.IsNullOrEmpty( p_oElem.ID ) ) {
                    Bind( p_oElem );
                }
                // Note: We're super dependent on MemoryElem inheriting from
                // IPgWordRange in most places. Here, we want both terms and states.
                if( p_oElem is MemoryElem<char> oMemElem  ) { 
                    // Parser is totally stream based, talking to the base class.
                    // So have to do this here.
                    iLine = _oStream.Line.At;
                    oMemElem.Offset = p_iStream;
                    _oStream.Line.Formatting.Add(oMemElem);
                }
            } catch( NullReferenceException ) {
                LogError( iLine, p_iStream );
            }
        }
            
        public bool MoveNext() {
            try {
                if( _oStack.Count < 1 )
                    return false;

                ProdBase<char>   oNonTerm    = _oStack.Pop(); 
                Production<char> oProduction = null;
		        int              iMatch      = 0;

		        if( oNonTerm.IsEqual( 30, _oStream, false, _iInput, out iMatch, out oProduction) ) {
				    OnMatch( oNonTerm, _iInput, iMatch );

                    if( oProduction == null ) { // it's a terminal or a binder
					    _iInput += iMatch;
				    } else {                    // it's a state.
					    Push( oProduction, oNonTerm );
				    }
                } else {
				    OnParserError( oNonTerm, _iInput );
					// This won't stop infinite loops on the (empty) terminal. But will stop other errors.
					if( !_oStream.InBounds( _iInput ) )
						return false;
			    }
            } catch( Exception oEx ) {
                Type[] rgErrors = { typeof( ArgumentNullException ),     // MemoryEnd<T> can throw it on grammar bugs.
                                    typeof( InvalidOperationException ), // Push had problem with a production
                                    typeof( NullReferenceException ) };  // Usually this means one of the callbacks had a problem! ^_^
				if( rgErrors.IsUnhandled( oEx ) )
					throw;

                LogError( oEx, _iInput );
                return false;
            }
			return true;
        } // MoveNext()

        /// <summary>
        /// 
        /// </summary>
        /// <seealso cref="BaseEditor.Submit_Raise"/>
        public void Parse() {
			try {
                if( _rgParseEvents.Count > 0 ) {
                    _rgCleanUp.Clear();

                    DateTime sNow = DateTime.Now;
                    foreach( KeyValuePair<Line, long> oPair in _rgParseEvents ) {
                        TimeSpan elapsedSpan = new TimeSpan(sNow.Ticks - oPair.Value);
                        if( elapsedSpan.Seconds > 1 ) {
                            oPair.Key.Formatting.Clear();

                            _oStream.Reset( oPair.Key );
                            _oStack .Clear();
                            _iInput = 0;

                            _oStack.Push ( new MemoryState<char>( _oStart, null ) );

				            while( MoveNext() );

                            _rgCleanUp.Add( oPair.Key );
                        }
                    }
                    foreach( Line oLine in _rgCleanUp ) {
                        _rgParseEvents.Remove( oLine );
                    }

                    if( _rgCleanUp.Count > 0 ) {
                        _oDocument.Raise_BufferEvent(BUFFEREVENTS.FORMATTED);
                    }
                }
			} catch( Exception oEx ) {
                Type[] rgErrors = { typeof( ArgumentNullException ),
                                    typeof( NullReferenceException ) };
                if( rgErrors.IsUnhandled( oEx ) )
                    throw;

                LogError( oEx, _iInput );
			}
        }

        public void ParseAll() {
            foreach( Line oLine in _oDocument ) {
                oLine.Formatting.Clear();

                _oStream.Reset( oLine );
                _oStack .Clear();
                _iInput = 0;

                _oStack.Push ( new MemoryState<char>( _oStart, null ) );

				while( MoveNext() );
            }
            _oDocument.Raise_BufferEvent(BUFFEREVENTS.FORMATTED);
        }
    }
}
