using System;
using System.Collections.Generic;
using System.Text;
using System.IO; // for filenotfoundexception.
using System.Linq;

using Play.Interfaces.Embedding;
using Play.Parse;
using Play.Parse.Impl;
using Play.Parse.Impl.Text;
using Play.Edit;

// Might move this to Play.Edit name space....
namespace Play.Integration {
    public class TagGrammer :
        Grammer<TagInfo>,
        IGrammerElemFactory<TagInfo> 
    {
        public TagGrammer( IPgBaseSite oSite ) : base( oSite ) {
        }

        ProdElem<TagInfo> IGrammerElemFactory<TagInfo>.CreateProdTerminal( string strTermClass ) {
            ProdElem<TagInfo> oElem = null;

            switch( strTermClass ) {
                case "myTagStart" :
                    oElem = new TagMarkupTerm( MARKUPTYPE.START );
                    break;
                case "myTagEnd" :
                    oElem = new TagMarkupTerm( MARKUPTYPE.END );
                    break;
                case "myTagSelf" :
                    oElem = new TagMarkupTerm( MARKUPTYPE.SELF );
                    break;
                case "myDirective" :
                    oElem = new TagMarkupTerm( MARKUPTYPE.DIRECTIVE );
                    break;
                case "myDTD" :
                    oElem = new TagMarkupTerm( MARKUPTYPE.DTD );
                    break;
                case "myEmpty" :
                    oElem = new TagEmpty();
                    break;
                case "" :
                    oElem = new TagLastTerm();
                    break;
            }

            return( oElem );
        }
    }

    class ParseEventsTag :
        IParseEvents<TagInfo>
    {
        readonly Grammer<TagInfo> _oGrammer;

        BaseEditor.LineStream _oTextStream;
        TagStream         _oTagStream;
        MemoryStateTag    _oStart;

        public ParseEventsTag( Grammer<TagInfo> p_oGrammer ) {
            _oGrammer = p_oGrammer ?? throw new ArgumentNullException();
        }

        /// <summary>
        /// Parse everthing all in one go. We're finished when we return from this call. 
        /// TODO: Since we've upgraded our ErrorList and ProductionsList to my editor, 
        ///       we can't use simple winforms controls.
        /// </summary>
        /// <param name="rgTags">List of tags to parse.</param>
        /// <param name="oTextStream">Text stream where the tags came from. So we can get tag names and etc.</param>
        public void Parse( List<TagInfo> rgTags, BaseEditor.LineStream oTextStream ) {
            if( oTextStream == null )
                throw new ArgumentNullException();

            State<TagInfo> oStart = _oGrammer.FindState("start");
            if( oStart != null ) {
                _oTextStream = oTextStream;
                _oTagStream  = new TagStream( rgTags );
                _oStart      = new MemoryStateTag( null, new ProdState<TagInfo>(oStart));

                ParseIterator<TagInfo> oParseIter = new ParseIterator<TagInfo>( _oTagStream, this, _oStart );
                oParseIter.ProductionEvent = OnProduction;

                // We parse all in one go!
		        while( oParseIter.MoveNext() );
                //LoadTree2( _oTagStream );
            }
        }

        public void OnMatch(ProdBase<TagInfo> p_oMemTerm, int p_iStart, int p_iLength) {
            // MemoryStateTag oMemState = p_oMemTerm as MemoryStateTag;
        }

        public void OnProduction(Production<TagInfo> p_oProd, int p_iStart) {
            //string strMessage = p_iStart.ToString() + " '" + 
            //                    _oTagStream[p_iStart].ToString() + "' : " + 
            //                    p_oProd.ToString();
            //if( _oProductionList != null )
            //    _oProductionList.Items.Add( new ListNode( p_iStart, strMessage ) );
        }

        public void OnParserError(ProdBase<TagInfo> p_oMemory, int p_iStart ) {
            int iStart = 0;

            try {
                TagInfo oTagInfo = _oTagStream[p_iStart];

                iStart = oTagInfo._oMemState.Start;
            } catch( Exception oEx ) {
                Type[] rgErrors = { typeof( ArgumentOutOfRangeException ),
                                    typeof( NullReferenceException ) };
				if( rgErrors.IsUnhandled( oEx ) )
					throw;
            }

            int    iLine   = _oTextStream.SeekIndex( iStart, out int iOffset );
            string strHost = string.Empty;

            if( p_oMemory is MemoryElemTag oMemTag ) {
                if( oMemTag.ProdElem.Host is Production<TagInfo> oProd ) {
                    strHost = oProd.StateName;
                }
            }

            StringBuilder sbMessage = new StringBuilder();
            sbMessage.Append( "Schema Error At Line: " );
            sbMessage.Append( iLine.ToString() );
            sbMessage.Append( ", Offset: " );
            sbMessage.Append( iOffset.ToString() );
            sbMessage.Append( ". State: " );
            sbMessage.Append( strHost );

            // BUG: Need to send this somewhere... ^_^;;
            //_oErrorsList.Items.Add( new ListNode( iStart, sbMessage.ToString() ) );
        }

    }

    /// <summary>
    /// This class overrides the text handler so that at the end of text parsing, we do 
    /// another "tag" parsing pass, using an embedded HTML tag parser. Registers a callback
    /// so, it does it's work whenever the document changes.
    /// </summary>
    public class ParseHandlerHTML : ParseHandlerText
    {
        readonly private   TagGrammer     _oTagGrammer;
        readonly private   ParseEventsTag _oTagHandler;
        readonly protected List<TagInfo>  _rgTagBag = new List<TagInfo>(100);
        readonly protected string         _strTagGrammerName = "html_4_trad";

        public ParseHandlerHTML( BaseEditor oDocument ) : base( oDocument, "html" ) {
            try {
                _oTagGrammer = (TagGrammer)((IPgGrammers)_oDocument.Services).GetGrammer(_strTagGrammerName);
            } catch (Exception oEx) {
                string strMessage = "Couldn't find grammar: " + _strTagGrammerName;
                Type[] rgErrors = { typeof( NullReferenceException ),
                                    typeof( InvalidCastException ),
                                    typeof( FileNotFoundException ), // TODO: Migrate away from this.
            						typeof( GrammerNotFoundException ) };
                if (!rgErrors.Contains(oEx.GetType()))
                    throw;

                _oDocument.Site.LogError("parse handler", strMessage);
                throw new InvalidOperationException(strMessage);
            }

            _oTagHandler = new ParseEventsTag( _oTagGrammer );
        }

        // I could probably find a better home for this.
        public static bool IsClearText( string strValue ) {
            foreach( char cChar in strValue ) {
                if( !Char.IsWhiteSpace( cChar ) )
                    return false;
            }
            return true;
        }

        public override void OnStart()
        {
            base.OnStart();

            _rgTagBag.Clear();
        }

        public override void OnFinish()
        {
            base.OnFinish();

            if (_rgTagBag.Count != 0 && _oTagHandler != null) {
                // States hit OnMatch() before all the productions have triggered.
                // so any bindings won't be filled in yet, so we do this now.
                foreach( TagInfo oTag in _rgTagBag ) {
                    oTag.InitNew( _oStream );
                }
                PassTwo();
            }
        }

        /// <summary>
        /// This is a second pass where we parse the tag bag that has been generated in the first parse of the text.
        /// </summary>
        public virtual void PassTwo() {
            _oTagHandler.Parse(_rgTagBag, _oStream);
        }

        static readonly private string[] _rgInterestingTags = { "tag", "style.end", "scripttagend", "htmlcomment" };

        public override void OnMatch(ProdBase<char> p_oElem, int p_iStream, int p_iLength)
        {
            base.OnMatch(p_oElem, p_iStream, p_iLength);

            // Look for tag matches and put them into the tag bag for the outline.
            if( p_oElem is MemoryState<char> oMemState ) {
                if( _rgInterestingTags.Contains(oMemState.StateName) ) {
                    _rgTagBag.Add( new TagInfo( oMemState, _oStream ));
                }
                if( oMemState.StateName == "entity" ) {
                    _rgTagBag.Add( new TagEntity( oMemState, _oStream ));
                }
            }
        }

        public override void Dispose()
        {
            _rgTagBag.Clear();
            
            base.Dispose();
        }
    }

}

