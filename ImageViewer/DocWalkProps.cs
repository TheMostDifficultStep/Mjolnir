using System;
using System.Collections.Generic;

using Play.Interfaces.Embedding;
using Play.Edit;
using Play.Parse;
using Play.Parse.Impl;

namespace Play.ImageViewer {
	/// <summary>
	/// This is the parse handler for our image properties. 
	/// </summary>
    public class ParseHandlerBase :
        IParseEvents<char>,
        IDisposable
    {
        readonly private Editor            _oDocument;
        readonly private Grammer<char>     _oLanguage;
        readonly private IPgRoundRobinWork _oWorkSite; 
        readonly private Editor.LineStream _oStream;

        private BufferEvent _oDocEvent;
        private bool        _fDisabled = false;
        
		/// <remarks>I could probably the work site from the document services.</remarks>
		/// <exception cref="ArgumentNullException" />
		/// <exception cref="InvalidOperationException" />
        public ParseHandlerBase( IPgRoundRobinWork oWorkSite, Editor oDocument, Grammer<char> oLanguage ) {
            _oLanguage = oLanguage ?? throw new ArgumentNullException( "No language, no parse!");
            _oDocument = oDocument ?? throw new ArgumentNullException( "Parse document most not be null!" );
            _oWorkSite = oWorkSite ?? throw new ArgumentNullException( "Parse needs a work site to work!");
            _oStream   = oDocument.CreateStream() ?? throw new InvalidOperationException( "Parser couldn't create a stream from the document" );

            _oDocument.BufferEvent += _oDocEvent = new BufferEvent(this.OnEvent);   
        }

        public bool DisableParsing {
            get { return ( _fDisabled ); }
            set {
                _fDisabled = value;

                if( _fDisabled ) {
                    _oDocument.ClearFormatting();
                } else {
                    _oWorkSite.Queue( CreateWorker(), 0 );
                }
            } 
        }

        public virtual void OnEvent( BUFFEREVENTS eEvent ) {
            if( _fDisabled ) {
                return;
            }

			// Note: We get Format events from ourself, which fortunately we ignore.
            if( eEvent == BUFFEREVENTS.MULTILINE  || 
				eEvent == BUFFEREVENTS.SINGLELINE ||
				eEvent == BUFFEREVENTS.LOADED ) {
                _oWorkSite.Queue( CreateWorker(), 0 );
            }
        }

        /// <summary>
        /// When a terminal/nonterminal matches we get the event here. TODO: Unfortunately
        /// since we are no longer capturing only terminals, we're going to
        /// capture states as well. This means if we have a state captured with color, but
        /// it has sub elements with color they might not colorize correctly without some 
        /// changes here.
        /// </summary>
        /// <param name="p_oTerm">The element that has been matched.</param>
        /// <param name="p_lStart">Stream position.</param>
        /// <param name="p_lLength">Length of the match.</param>
        public virtual void OnMatch( ProdBase<char> p_oElem, int p_iStream, int p_iLength ) {
			Line oLine = _oStream.SeekLine( p_iStream, out int iLineOffset);

			if( p_oElem is MemoryElem<char> oMemElem && p_oElem.IsWord ) {
				oMemElem.Offset = iLineOffset;
				oLine.Formatting.Add(oMemElem);
			} else {
				if( p_oElem is ProdBase<char> oProdElem && p_oElem.IsTerm ) {
					oLine.Formatting.Add( new ColorRange( iLineOffset, p_iLength, 0 ) ); // BUG: WordRange?
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
        public virtual void OnParserError( ProdBase<char> p_oMemory, int p_iStart ) {
			if( _oStream.InBounds( p_iStart ) ) {
				Line oLine = _oStream.SeekLine(p_iStart,out int iOffset);
				int  iLine   = oLine.At;

				oLine.Formatting.Add( new ColorRange( iOffset, 1, -4 ) ); // BUG: hard coded color.
			} else {
				Line oLine   = _oDocument[ _oDocument.ElementCount - 1 ];
				int  lOffset = oLine.ElementCount;

				if( lOffset > 0 )
					lOffset--;
				
				oLine.Formatting.Add( new ColorRange( lOffset, 1, -4 ) );
			}
        }

        /// <summary>
        /// Report more serious issues.
        /// </summary>
        void OnParserException( Exception oEx, int iStart ) {
            _oDocument.Site.LogError( "parser", oEx.Message );
        }

        /// <summary>
        /// 2/14/2017 : New way to do worker iterators!!! The only drawback is that I can't 
        /// get a destroy method called if we bail out in the middle. 
        /// </summary>
        /// <returns></returns>
        public IEnumerator<int> CreateWorker() 
        {
			State<char> oStart  = _oLanguage.FindState("start");

            MemoryState<char>   oMStart  = new MemoryState<char>( new ProdState<char>( oStart ), null );
            ParseIterator<char> oParser  = new ParseIterator<char>( _oStream, this, oMStart );

            oParser.ExceptionEvent  = OnParserException;

            yield return( 0 );

			_oDocument.ClearFormatting();

            while( oParser.MoveNext() ) {
                yield return( 0 );
            }

			_oDocument.Raise_BufferEvent( BUFFEREVENTS.FORMATTED );  

			yield return( 0 );
        }

        public virtual void Dispose()
        {
            _oWorkSite.Stop(); // Removes us from the worklist, should make it easier to free us up.

            _oDocument.BufferEvent -= _oDocEvent;
        }
	}

	public class EditWinProperties : EditWin {
		public EditWinProperties( IPgViewSite oBaseSite, BaseEditor p_oDocument,bool fReadOnly = false,bool fSingleLine = false) : 
			base( oBaseSite, p_oDocument, fReadOnly, fSingleLine) 
		{
		}

		/// <summary>
		/// This is a poster child for the problem of needing to do a parse before you can 
		/// really determine the widths of any columns! Probably need a max width but then
		/// reset the column if things are narrower for that.
		/// </summary>
		/// <returns></returns>
		protected override CacheManager CreateCacheManager() {
			CacheManager oManager = base.CreateCacheManager();

            oManager.Width = 150; // BUG: probably obsolete.

			return (oManager);
		}

	}
}
