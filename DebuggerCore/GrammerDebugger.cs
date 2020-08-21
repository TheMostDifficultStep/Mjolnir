using System;
using System.Collections.Generic;
using System.Linq;

using Play.Interfaces.Embedding;
using Play.Parse.Impl;
using Play.Edit;

namespace Play.Debug
{
    public class GrammerDebugger :
		IPgParent,
        IPgLoad<string>
    {
        protected IPgBaseSite   _oSiteBase;
        protected Editor        _rgEditGrammer;
        protected Grammer<char> _oLanguage = null;

		public IPgParent Parentage           => _oSiteBase.Host;
		public IPgParent Services => Parentage.Services;

		/// <summary>
		/// This is for our editor instance we are hosting!!
		/// </summary>
		public class DocSlot : 
			IPgBaseSite
		{
			readonly GrammerDebugger _oHost;

			public Editor _oDocument;

			/// <summary>
			/// This is for our editor instance we are hosting!!
			/// </summary>
			public DocSlot( GrammerDebugger oHost ) {
				_oHost = oHost ?? throw new ArgumentNullException();
			}

			public IPgParent Host                => _oDocument;
			public object    ServicesFromProgram => _oDocument.Services;
			public FILESTATS FileStatus          => FILESTATS.UNKNOWN;

			public void LogError( string strMessage, string strDetails, bool fShow=true ) {
				_oHost.LogError( strMessage, "Debugger: " + strDetails );
			}

			public void Notify( ShellNotify eEvents ) {
			}
		}

        public GrammerDebugger( IPgBaseSite oSite ) {
            if( oSite == null )
                throw new ArgumentNullException();

            _oSiteBase = oSite;
        }

        public IPgBaseSite GetSite()
        {
            return( _oSiteBase );
        }


        public void LogError( string strCatagory, string strDetail )
        {
            try {
                _oSiteBase.LogError( strCatagory, strDetail );
            } catch( NullReferenceException ) {
            }
        }

        public bool InitNew()
        {
			DocSlot oSite = new DocSlot( this );
            _rgEditGrammer = new Editor( oSite );

            oSite._oDocument = _rgEditGrammer;

            return( true );
        }

        public bool Load(string strGrammerName )
        {
            if( !InitNew() )
                return( false );

            try {
                IPgGrammers oGrammars = Services as IPgGrammers;
               
                _oLanguage = ( Grammer<char>)oGrammars.GetGrammer( strGrammerName );
            } catch( Exception oEx ) {
                Type[] rgError = { typeof(NullReferenceException),
                                   typeof(InvalidCastException),
								   typeof(GrammerNotFoundException)};
                if( !rgError.Contains( oEx.GetType() ))
                    throw new InvalidProgramException( "Could not load requested grammer", oEx );

                return( false );
            }

            // BUG: Need to the file name for the grammar. Need access to the MappedGrammer object.
			//      But what if grammer is an embedded object, need to figure out access.

            return( true );
        }
    }
}
