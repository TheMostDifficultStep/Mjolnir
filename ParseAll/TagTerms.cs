using System;
using System.Xml;
using System.Collections.Generic;
using System.Collections;
using System.Text;

using Play.Interfaces.Embedding;

namespace Play.Parse.Impl.Text
{
    public enum MARKUPTYPE {
        START = 0,
        END,
        SELF,
        DIRECTIVE,
        DTD,
        ENTITY,
        NONE
    }

    public static class ParseHelpers {
        /// <summary>
        /// Use the memory state to find the named attribute's position in the given data stream.
        /// This is code that basically useful for the HTML grammars.
        /// </summary>
        /// <remarks>This function as originally in the main executable "parsefragments.cs". I've moved
        /// it here as a helper function.</remarks>
        public static string GetTagAttribute( this MemoryState<char> oChild, DataStream<char> oTextStream, string strAttribName ) {
            try { 
                string    strReturn = string.Empty;
                int       iIndex    = oChild.IndexOfBinding( "attribs" );
                ArrayList rgAttribs = (ArrayList)oChild.Values[iIndex] as ArrayList;

                if( rgAttribs != null ) {
                    foreach( MemoryState<char> oAttrib in rgAttribs ) {
                        int iAttrName = oAttrib.IndexOfBinding( "attribname" );
                        int iAttrValu = oAttrib.IndexOfBinding( "attribvalue" );
                        MemoryElem<char> oAttribName = oAttrib.Values[iAttrName] as MemoryElem<char>;
                        string strTestAttribName = oTextStream.SubString( oAttribName.Start, oAttribName.Length );

                        if( string.Compare( strTestAttribName, strAttribName, true ) == 0 ) {
                            MemoryElem<char> oAttribValu = oAttrib.Values[iAttrValu] as MemoryElem<char>;
                            strReturn = oTextStream.SubString( oAttribValu.Start, oAttribValu.Length );
                        }
                    }
                }
            
                return ( strReturn );
            } catch( Exception oEx ) { 
                Type[] rgErrors = { typeof( NullReferenceException ),
                                    typeof( ArgumentNullException ) };
                if( rgErrors.IsUnhandled( oEx ) )  {
                    throw;
                }
				return string.Empty;
            }
        }
	}

    /// <summary>
    /// This is the data type for the tag stream. It is like the "char" type in a stream
    /// of char that we parse for markup.
    /// </summary>
    public class TagInfo {
        public MARKUPTYPE        _eType;
        public string            _strName;
        public string            _strTagName;
        public MemoryState<char> _oMemState;
        public object            _oExtra;

        public TagInfo( MemoryState<char> oMemState, DataStream<char> oTextStream ) {
            _oMemState  = oMemState ?? throw new ArgumentNullException( "Memstate on TagInfo must not be null!");

            _strTagName = string.Empty;
            _eType      = MARKUPTYPE.NONE;
        }

        public MARKUPTYPE MarkupType {
            get { return( _eType ); }
        }

        public override string ToString() {
            return( _strName );
        }

        public string Name {
            get { return( _strName ); }
        }

       /// <summary>
        /// Get data we collected on this node.
        /// </summary>
        /// <param name="rgTextStream">The parsed data stream, text.</param>
        /// <param name="rgDecl">The collection of all bindings.</param>
        /// <param name="oMemElem">The element containing the stream positioning.</param>
        /// <param name="strIndex">The datum we are looking for.</param>
        /// <returns></returns>
        public static string GetBinding( 
            DataStream<char>                rgTextStream,
            SortedList<string, BindingInfo> rgDecl, 
            MemoryElem<char>                oMemElem, 
            string                          strIndex 
        ) {
            int iIndex = rgDecl.IndexOfKey( strIndex );

            if( iIndex > -1 ) {
                // Only the memory element has the stream offset. IColorRange is a line offset.
                MemoryElem<char> oMemory = oMemElem.Values[iIndex] as MemoryElem<char>;
                if( oMemory != null )
                    return( rgTextStream.SubString( oMemory.Start, oMemory.Length ) );
            }

            return( string.Empty );
        }

        /// <summary>
        /// This isn't like load where we're loading persistant data. We need access
        /// to the text stream to cache some of the tag values into ourselves. We can only 
        /// do this AFTER the state has been fully parsed. I should make a new event to 
        /// return this.
        /// </summary>
        /// <param name="oTextStream">The text we parsed to get our tag.</param>
        /// <returns>true if everything ready to roll.</returns>
        public virtual bool InitNew( DataStream<char> oTextStream ) 
        {
            SortedList<string,BindingInfo> rgDecl = _oMemState.Class.Bindings;

            if( rgDecl == null )
                return( false );

            string strTagLeft = GetBinding( oTextStream, rgDecl, _oMemState, "starttagstart" );
            string strTagRight= GetBinding( oTextStream, rgDecl, _oMemState, "starttagend" );
            string strTagName = GetBinding( oTextStream, rgDecl, _oMemState, "tagname" );

            if(        "<"  == strTagLeft && "/>" == strTagRight ) {
                _eType = MARKUPTYPE.SELF;
            } else if( "<"  == strTagLeft && ">" == strTagRight ) {
                _eType = MARKUPTYPE.START;
            } else if( "</" == strTagLeft && ">" == strTagRight ) {
                _eType = MARKUPTYPE.END;
            } else if( "<?" == strTagLeft && "?>" == strTagRight ) {
                _eType = MARKUPTYPE.DIRECTIVE;
            } else if( "<!" == strTagLeft && ">" == strTagRight ) {
                _eType = MARKUPTYPE.DTD;
            }

            _strTagName = strTagName;
            _strName    = strTagLeft + strTagName + strTagRight;

            return true;
        }

        public string GetAttributeValue( DataStream<char> oStream, string strID ) {
            return _oMemState.GetTagAttribute( oStream, strID );
        }
    }

    public class TagEntity : TagInfo {
        public TagEntity( MemoryState<char> oMemState, DataStream<char> oTextStream ) :
            base( oMemState, oTextStream ) 
        {
        }

        public override bool InitNew(DataStream<char> oTextStream)
        {
            SortedList<string,BindingInfo> rgDecl = _oMemState.Class.Bindings;

            if( rgDecl == null )
                return( false );

            string strEntityType  = GetBinding( oTextStream, rgDecl, _oMemState, "entitytype" );
            string strEntityValue = GetBinding( oTextStream, rgDecl, _oMemState, "entityvalue");

            _eType = MARKUPTYPE.ENTITY;

            if( strEntityType == "&#" )
                try {
                    _strName += Char.Parse( strEntityValue );
                } catch( Exception oEx ) {
                    Type[] rgErrors = { typeof( ArgumentNullException ),
                                        typeof( FormatException ) };
                    if( rgErrors.IsUnhandled( oEx ) )
                        throw;
                }
            if( strEntityType == "&" ) {
                switch( strEntityValue ) {
                    case "quot":
                        _strName += '"';
                        break;
                    case "amp":
                        _strName += '&';
                        break;
                    case "apos":
                        _strName += '\'';
                        break;
                }
            }

            return true;
        }
    }

    public class TagEmpty : ProdElem<TagInfo> {
        public override string ToString() 
        { 
            return ("The Empty Tag."); 
        }

        public override bool IsEqual(
                int                 p_iMaxStack,
                DataStream<TagInfo> p_oStream, 
                bool                p_fLookAhead, 
                int                 p_iPos, 
            out int                 p_iMatch, 
            out Production<TagInfo> p_oProd
        ) {
            p_iMatch = 0;
            p_oProd  = null;

            return( p_oStream.InBounds( p_iPos ) );
        }
    }

    public class TagLastTerm : TagEmpty
    {
        public override string ToString()
        {
            return ("PEND");
        }
    }

    /// <summary>
    /// This is the base class for many of our terminals. The grammer may have
    /// a "value" attribute, which specifies the instance of term we are looking for.
    /// Strictly speaking this does not need to be abstract since the TagMarkupTerm
    /// sub-class is so simple, but that might change.
    /// </summary>
    public abstract class ProdTermTag : ProdElem<TagInfo>
    {
        protected string _strTagName;

        public override string ToString() 
        { 
            return ("abstract term"); 
        }

        public override bool Load( XmlElement p_xmlTerminal )
        {
            if( !base.Load( p_xmlTerminal ) )
				return( false );

            _strTagName = p_xmlTerminal.GetAttribute( "value" );
            return( true );
        }
    }

	public class TagMarkupTerm : ProdTermTag 
	{
        MARKUPTYPE _eMarkupType;

        public TagMarkupTerm( MARKUPTYPE eType ) {
            _eMarkupType = eType;
        }

        public MARKUPTYPE MarkupType {
            get { return( _eMarkupType ); }
        }

		public override string ToString()
		{
            StringBuilder sbReturn = new StringBuilder();

            sbReturn.Append( "'" );
            if( _eMarkupType == MARKUPTYPE.END )
                sbReturn.Append( "/" );
            sbReturn.Append( _strTagName );
            if( _eMarkupType == MARKUPTYPE.SELF )
                sbReturn.Append( "/" );
            sbReturn.Append( "'" );

            return( sbReturn.ToString() );
		}

        public override bool IsEqual(
                int                 p_iMaxStack,
                DataStream<TagInfo> p_oStream, 
                bool                p_fLookAhead, 
                int                 p_iPos, 
            out int                 p_iMatch, 
            out Production<TagInfo> p_oProd
        ) {
            p_iMatch = 0;
            p_oProd  = null;

            if( p_oStream.InBounds( p_iPos ) ) {
                TagInfo oInfo = p_oStream[p_iPos];

                if( oInfo != null && _strTagName == string.Empty || oInfo._strTagName == _strTagName ) {
                    if( oInfo._eType == _eMarkupType ) {
                        p_iMatch = 1;
                        return( true );
                    }
                }
            }

            return( false );
        }
	}

}