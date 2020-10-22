using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;
using System.Globalization;

using Play.Interfaces.Embedding;

using Play.Parse;
using Play.Parse.Impl;
using Play.Parse.Impl.Text;

namespace Play.Edit {
    public class TextTermPunctuation : ProdElem<char>
    {
        public override bool IsEqual(int p_iMaxStack, DataStream<char> p_oText, bool fLookAhead, int p_iPos, out int p_iMatch, out Production<char> p_oProd) {
            p_iMatch = 0;
            p_oProd  = null;
            
            while( p_oText.InBounds( p_iPos + p_iMatch ) )
            {
                char cChar = p_oText[ p_iPos + p_iMatch ];
                if( !Char.IsPunctuation( cChar ) )
                    break;
                p_iMatch ++;
            }
            return (p_iMatch > 0 );
        }
        
        public override string ToString() {
            return( "!" );
        }
    }

    /// <summary>
    /// Trying to add &lt;head&gt; recognition is a nightmare since you can't tell that
    /// from an equation "x&lt;y" which you wouldn't mind if it breaks the words! I would
    /// like to figure out a merged breaker based on the html, versus a simple breaker like we
    /// have going in this file.
    /// </summary>
    public class TextTermAlphaNum : ProdElem<char>
    {
        public override bool IsEqual(int p_iMaxStack, DataStream<char> p_oText, bool fLookAhead, int p_iPos, out int p_iMatch, out Production<char> p_oProd) {
            p_iMatch = 0;
            p_oProd  = null;
            
            while( p_oText.InBounds( p_iPos + p_iMatch ) )
            {
                char cChar = p_oText[ p_iPos + p_iMatch ];
                UnicodeCategory oCatagory = Char.GetUnicodeCategory( cChar );

                switch( oCatagory ) {
                    case UnicodeCategory.LowercaseLetter:
                    case UnicodeCategory.UppercaseLetter:
                    case UnicodeCategory.LetterNumber:
                    case UnicodeCategory.NonSpacingMark:
                    case UnicodeCategory.OtherLetter:
                    case UnicodeCategory.OtherNumber:
                    case UnicodeCategory.DecimalDigitNumber:
                        p_iMatch ++;
                        break;
                    default:
                        return( p_iMatch > 0 );
                }
            }
            return (p_iMatch > 0 );
        }
        
        public override string ToString() {
            return( "a1" );
        }
    }

    /// <summary>
    /// Dangerous! Just get one character at a time.
    /// </summary>
    public class TextTermEverything : ProdElem<char>
    {
        public override bool IsEqual(int p_iMaxStack, DataStream<char> p_oText, bool fLookAhead, int p_iPos, out int p_iMatch, out Production<char> p_oProd) {
            p_iMatch = 0;
            p_oProd  = null;
            
            if( p_oText.InBounds( p_iPos + p_iMatch ) )
            {
                char cChar = p_oText[ p_iPos + p_iMatch ];
                if( !Char.IsSeparator( cChar ) )
                    p_iMatch ++;
            }
            return (p_iMatch > 0 );
        }
        
        public override string ToString() {
            return( "$" );
        }
    }

    /// <summary>
    /// It will spew error windows on error. I probably want to separate
    /// that ui so I can run in a programmatic mode.
    /// This is a base level word breaker. I should probably have some sort of
    /// system that works with the primary grammer for selection. But this one should
    /// ALWAYS work, or we won't do line wrapping correctly.
    /// </summary>
    public class SimpleWordBreaker :
        Grammer<char>,
        IGrammerElemFactory<char> 
    {
        public SimpleWordBreaker( IPgBaseSite oSite ) : base( oSite ) {
        }

        ProdElem<char> IGrammerElemFactory<char>.CreateProdTerminal( string strTermClass ) {
            ProdElem<char> oElem = null;

            switch( strTermClass ) {
                case "myspace":
                    oElem = new TextTermSpace();
                    break;
                case "myEmpty":
                    oElem = new TextTermEmpty();
                    break;
                case "myAlphaNum":
                    oElem = new TextTermAlphaNum();
                    break;
                case "myToken":
                    oElem = new TextTermToken();
                    break;
                case "myAnyOf":
                    oElem = new TextTermAnyOf();
                    break;
                case "myEverything":
                    oElem = new TextTermEverything();
                    break;
                case "myPunc":
                    oElem = new TextTermPunctuation();
                    break;
            }

            return( oElem );
        }
    }

    /// <summary>
    /// An instance of this object is paired with an editor, so that
    /// the document word broken for the viewers.
    /// </summary>
    public class WordBreakerHandler :
        IParseEvents<char>
    {
        private DataStream<char>          _oStream;
        private Grammer<char>             _oGrammer;
        private ParseIterator<char>       _oParseIter;
        private ICollection<IPgWordRange> _rgWords;

        public WordBreakerHandler( Grammer<char> oLanguage ) {
            _oGrammer = oLanguage ?? throw new ArgumentNullException( "Language must not be null" );
        }

        /// <summary>
        /// Set up the parse iterator.
        /// </summary>
        /// <param name="oStream">The stream to parse against.</param>
        /// <param name="oStartState">The start state to begin with.</param>
        public void Parse( DataStream<char> oStream, ICollection<IPgWordRange> rgWords ) {
            try {
                _rgWords = rgWords;
                _oStream = oStream;

                State<char> oStart = _oGrammer.FindState( "start" );

                if( oStart != null ) {
                    _oParseIter = new ParseIterator<char>( _oStream, this, new MemoryState<char>( new ProdState<char>( oStart ), null ) );

                    _rgWords.Clear();

                    while( _oParseIter.MoveNext() );
                }
            } catch( NullReferenceException ) {
            } finally {
                _rgWords = null; // Just clear our reference. We were filling the collection for someone else.
            }
        }

        /// <summary>
        /// Shag the word and save it away.
        /// </summary>
        /// <param name="p_oTerm">The element that has been matched.</param>
        /// <param name="p_lStart">Stream position.</param>
        /// <param name="p_lLength">Length of the match.</param>
        void IParseEvents<char>.OnMatch( ProdBase<char> oElem, int iStart, int iLength ) {
            // We'll re-use these later.
            if( oElem.IsTerm ) {
                _rgWords.Add(new WordRange(iStart, iLength, 0 ));
            }
        } 

        /// <summary>
        /// If for some reason we get a parse error on the words we can simply hard
        /// break on the width of the screen.
        /// </summary>
        /// <param name="p_oMemory">Element on the stack that failed.</param>
        /// <param name="p_lStart">It's position in the stream.</param>
        void IParseEvents<char>.OnParserError( ProdBase<char> p_oMemory, int p_iStart ) {
            // TODO: Implement fall back.
        }
    } // Text implementation.

}

