using System;
using System.Collections.Generic;
using System.Windows.Forms;

using Play.Interfaces.Embedding;
using Play.Parse.Impl;
using Play.Parse.Impl.Text;

namespace Mjolnir {

    /// <summary>
    /// Let's try to create the named states using reflection! This is just
    /// half built at present.
    /// </summary>
    public class factory 
    {
        Dictionary<string, Type> m_rgTypes = new Dictionary<string, Type>();
        
        public void Add( string key, Type type )
        {
            m_rgTypes.Add( key, type );
        }
        
        public ProdElem<char> this[ string strIndex ] {
            get {
                Type type = m_rgTypes[strIndex];
                
                return( null );
            }
        } 
    }

    /// <summary>
    /// This is the object that holds the grammer for a particular
    /// language. There will be only one instance of this per language.
    /// It will spew error windows on error. I probably want to separate
    /// that ui so I can run in a programmatic mode.
    /// </summary>
    public class TextGrammer :
        Grammer<char>,
        IGrammerElemFactory<char> 
    {
        public TextGrammer( IPgBaseSite oSite ) : base( oSite ) {
        }

        ProdElem<char> IGrammerElemFactory<char>.CreateProdTerminal( string strTermClass ) {
            ProdElem<char> oElem = null;

            // TODO: mypunct

            switch( strTermClass ) {
                case "mytoken":
                    oElem = new TextTermToken();
                    break;
                case "myliteral":
                    oElem = new TextTermLiteral();
                    break;
                case "mynumber":
                    oElem = new TextTermNumber();
                    break;
                case "myspace":
                    oElem = new TextTermSpace();
                    break;
                case "mycr":
                    oElem = new TextTermCR();
                    break;
                case "myEmpty":
                    oElem = new TextTermEmpty();
                    break;
                case "myanyof":
                    oElem = new TextTermAnyOf();
                    break;
                case "mynotblank":
                    oElem = new TextTermNotBlank();
                    break;
            }

            return( oElem );
        }
    }

    public class UITermMouse :  ProdElem<object>
	{
		public override bool IsEqual( 
            int p_iMaxStack, 
            DataStream<object> p_oStream, 
            bool fLookAhead, 
            int p_iPos, 
            out int p_iMatch, 
            out Production<object> p_oProd )
		{
			p_iMatch = 0;
			p_oProd  = null;

			while( p_oStream.InBounds( p_iPos + p_iMatch ) )
			{
                MouseEventArgs oMouseEvent = p_oStream[ p_iPos + p_iMatch ] as MouseEventArgs;

				if( oMouseEvent != null )
				    ++p_iMatch;
			}
			return( p_iMatch > 0 );
		}
	} 

    public class UITermKeyUp :  ProdElem<object>
	{
		public override bool IsEqual( 
            int p_iMaxStack, 
            DataStream<object> p_oStream, 
            bool fLookAhead, 
            int p_iPos, 
            out int p_iMatch, 
            out Production<object> p_oProd )
		{
			p_iMatch = 0;
			p_oProd  = null;

			while( p_oStream.InBounds( p_iPos + p_iMatch ) )
			{
                KeyEventArgs oMouseEvent = p_oStream[ p_iPos + p_iMatch ] as KeyEventArgs;

				if( oMouseEvent != null )
				    ++p_iMatch;
			}
			return( p_iMatch > 0 );
		}
	} 

    /// <summary>
    /// This is the object that holds the grammer for a particular
    /// language. There will be only one instance of this per language.
    /// It will spew error windows on error. I probably want to separate
    /// that ui so I can run in a programmatic mode.
    /// </summary>
    public class UIEventGrammer :
        Grammer<object>,
        IGrammerElemFactory<object> 
    {
        public UIEventGrammer( IPgBaseSite oSite ) : base( oSite ) {
        }

        ProdElem<object> IGrammerElemFactory<object>.CreateProdTerminal( string strTermClass ) {
            ProdElem<object> oElem = null;

            switch( strTermClass ) {
                case "mymouse":
                    oElem = new UITermMouse();
                    break;
                case "mykeyup":
                    oElem = new UITermKeyUp();
                    break;
            }

            return( oElem );
        }
    }

}
