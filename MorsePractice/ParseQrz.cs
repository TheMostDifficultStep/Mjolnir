using System;
using System.Linq;
using System.Text;

using Play.Interfaces.Embedding; 
using Play.Edit;
using Play.Integration;
using Play.Parse.Impl.Text;


namespace Play.MorsePractice {
    /// <summary>
    /// Parse the converted base64 HTML blob, CallSignSource, that we skimmed from the main document.
    /// </summary>
    public class ParseBioHTMLSkimmer : ParseHandlerHTML {
        DocNotes Document { get; }

        Editor   Bio => Document.CallSignBio;

        public ParseBioHTMLSkimmer( DocNotes oDocument ) : 
            base( oDocument.CallSignBioHtml ) 
        {
            Document = oDocument ?? throw new ArgumentNullException( "Document must not be null" );
        }

        /// <summary>
        /// The biography is a base64 blob we pull out of the main text. After it's parsed, we walk the collection
        /// to grab the text between the tags. It's sort of a crude text browser!! We don't actually parse the
        /// tag stream in this subclass we just walk it looking for text to stuff in our editor.
        /// </summary>
        public override void PassTwo() {
            try {
                Bio.Clear();

                if ( _rgTagBag.Count < 1 )
                    return;

                TagInfo       oLastTag       = _rgTagBag[0];
                string[]      rgLineBreakers = { "<p>", "<li>", "<br>", "<h1>", "<h2>", "<h3>", "<h4>" };
                string[]      rgLineBlocks   = { "<p>", "<h1>", "<h2>", "<h3>", "<h4>" };
                StringBuilder sbBioText      = new StringBuilder();
                
                for( int iTag = 1; iTag<_rgTagBag.Count; ++iTag ) {
                    TagInfo oTag   = _rgTagBag[iTag];
                    int     iStart = oLastTag._oMemState.End;
                    int     iEnd   = oTag    ._oMemState.Start - 1;
                    int     iLen   = iEnd - iStart + 1;

                    if( iLen > 0 ) {
                        int j, k=iEnd;
                        for( j=iStart; j<iStart+iLen; ++j ) {
                            if( !Char.IsWhiteSpace( _oStream[j] ) )
                                break;
                        }
                        //for( k=iEnd; k>=j; --k ) {
                        //    if (!Char.IsWhiteSpace( _oStream[k] ) )
                        //        break;
                        //}
                        int iTrim = k - j + 1;
                        if( iTrim > 0 ) 
                            sbBioText.Append( _oStream.SubString( j, iTrim ) );
                    }

                    if( oTag.MarkupType == MARKUPTYPE.ENTITY ) {
                        sbBioText.Append( oTag.Name );
                    }

                    if( rgLineBreakers.Contains( oTag.Name ) &&
                        oTag.MarkupType == MARKUPTYPE.START &&
                        sbBioText.Length > 0 ) 
                    {
                        string strNew = sbBioText.ToString();

                        if( !IsClearText( strNew ) ) {
                            Bio.LineAppend( strNew );
                            if( rgLineBlocks.Contains( oTag.Name ) )
                                Bio.LineAppend( string.Empty );
                            sbBioText.Clear();
                        }
                    }

                    oLastTag = oTag;
                }
            } catch( Exception oEx ) {
                Type[] rgErrors = { typeof( NullReferenceException ),
                                    typeof( ArgumentNullException ),
                                    typeof( ArgumentOutOfRangeException ),
                                    typeof( IndexOutOfRangeException ) };
                if( rgErrors.IsUnhandled( oEx ) )
                    throw;

                Bio.LineAppend( "Data Read Error..." );
            }
        }
    }

    /// <summary>
    /// Cheap attempt at skimming the address from the HTML. It doesn't work. 
    /// What I really need to do is to make a TAG grammer to pull this information.
    /// </summary>
    public class ParseQrzHTMLSkimmer : ParseHandlerHTML {
        DocNotes Document { get; }

        Editor   Address => Document.CallSignAddress;
        Editor   QrzPage => Document.CallSignPageHtml;

        public ParseQrzHTMLSkimmer( DocNotes oDocument ) : 
            base( oDocument.CallSignPageHtml ) 
        {
            Document = oDocument ?? throw new ArgumentNullException( "Document must not be null" );
        }

        public override void PassTwo() {
            try {
                Address.Clear();

                if ( _rgTagBag.Count < 1 )
                    return;

                TagInfo       oLastTag       = _rgTagBag[0];
                string[]      rgLineBreakers = { "<p>", "<li>", "<br>", "<h1>", "<h2>", "<h3>", "<h4>" };
                string[]      rgLineBlocks   = { "<p>", "<h1>", "<h2>", "<h3>", "<h4>" };
                string[]      rgParaStyles   = { "p8", "p7" };
                StringBuilder sbText         = new StringBuilder();
                DataStream<char> oPageStream = QrzPage.CreateStream();
                
                for( int iTag = 1; iTag<_rgTagBag.Count; ++iTag ) {
                    TagInfo oTag   = _rgTagBag[iTag];
                    int     iStart = oLastTag._oMemState.End;
                    int     iEnd   = oTag    ._oMemState.Start - 1;
                    int     iLen   = iEnd - iStart + 1;

                    if( iLen > 0 ) {
                        int j, k=iEnd;
                        for( j=iStart; j<iStart+iLen; ++j ) {
                            if( !Char.IsWhiteSpace( _oStream[j] ) )
                                break;
                        }
                        int iTrim = k - j + 1;
                        if( iTrim > 0 ) 
                            sbText.Append( _oStream.SubString( j, iTrim ) );
                    }

                    if( oTag.MarkupType == MARKUPTYPE.ENTITY ) {
                        sbText.Append( oTag.Name );
                    }

                    string strAttrib = oTag.GetAttributeValue(  oPageStream, "style" );

                    if( rgLineBreakers.Contains( oTag.Name ) &&
                        oTag.MarkupType == MARKUPTYPE.START &&
                        rgParaStyles.Contains( strAttrib ) &&
                        sbText.Length > 0 ) 
                    {
                        string strNew = sbText.ToString();

                        if( !IsClearText( strNew ) ) {
                            Address.LineAppend( strNew );
                            if( rgLineBlocks.Contains( oTag.Name ) )
                                Address.LineAppend( string.Empty );
                            sbText.Clear();
                        }
                    }

                    oLastTag = oTag;
                }
            } catch( Exception oEx ) {
                Type[] rgErrors = { typeof( NullReferenceException ),
                                    typeof( ArgumentNullException ),
                                    typeof( ArgumentOutOfRangeException ),
                                    typeof( IndexOutOfRangeException ) };
                if( rgErrors.IsUnhandled( oEx ) )
                    throw;

                Address.LineAppend( "Data Read Error..." );
            }
        }
    }
}
