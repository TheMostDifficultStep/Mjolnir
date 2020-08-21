using System;
using System.Xml;
using System.Collections.Generic;
using System.IO;

using Play.Interfaces.Embedding;

namespace Play.Parse.Impl {
	public class GrammerNotFoundException : SystemException {
		public GrammerNotFoundException() : base() {}
		public GrammerNotFoundException( string strMessage ) : base( strMessage ) {}
		public GrammerNotFoundException( string strMessage, Exception oInnerException ) : base( strMessage, oInnerException ) {}
	}
    public abstract class StateSiteAbstract<T> : IPgBaseSite {
        public abstract Grammer<T> Grammar { get; }
        public abstract IPgParent  Host{ get; }
        public abstract void       LogError( string s1, string s2, bool fShow=true );
        public virtual  FILESTATS  FileStatus { get { return( FILESTATS.UNKNOWN ); } }
		public virtual  void       Notify( ShellNotify eEvent ) {}
    }

    public enum ELEMPOSITION {
        start  = 0x01,
        middle = 0x04,
        end    = 0x02
    }

    /// <summary>
    /// Use this interface to return implementations of ProdState and ProdTerm elements.
    /// </summary>
    /// <typeparam name="T">Specify the stream type.</typeparam>
    public interface IGrammerElemFactory<T> {
        ProdElem<T> CreateProdTerminal( string p_strTermClass );
    }

    internal delegate int GetColorIndex( string strColor );

    /// <summary>This object reads the top XML BNF and creates instances of the states
    /// that make up the grammer. 
    /// </summary>
    public abstract class Grammer<T> :
		IPgParent,
        IPgLoad<TextReader>,
        IDisposable
    {
        readonly IPgBaseSite  _oSite;
		readonly IPgGrammarSite _oSiteGrammer;

        readonly Dictionary<string, StateSlot> _rgAvailableStates = new Dictionary<string, StateSlot>(); 

        // Not sure why I don't inherit from StateSiteAbstract.
        private class StateSlot : 
            IPgBaseSite 
        {
            readonly Grammer<T> _oHost;

            State<T>   _oGuest;
            XmlElement _oStorage;

            public StateSlot( Grammer<T> oHost, XmlElement oStorage ) 
            {
                _oHost    = oHost;
                _oStorage = oStorage;
            }

            public virtual IPgParent Host
            {
                get { return _oHost; }
            }

            public virtual object Services
            {
                get { return _oHost; }
            }

            public State<T> Guest {
                get { return( _oGuest ); }
                set {
                    _oGuest = value;
                    if( _oGuest == null )
                        throw new ArgumentNullException();
                }
            }

            public XmlElement Storage {
                get { return _oStorage; }
            }

            public virtual void LogError(string strMessage, string strDetails, bool fShow=true)
            {
                _oHost.SendError( strMessage, strDetails );
            }

            public virtual FILESTATS FileStatus { get { return( FILESTATS.UNKNOWN ); } }

			public object ServicesFromProgram => throw new NotImplementedException();

			public virtual void  Notify( ShellNotify eEvent ) {}

            public bool Bind() {
                bool fReturn = _oGuest.Bind( _oStorage );
                _oStorage = null;

                return( fReturn );
            }
        }

        public Grammer( IPgBaseSite oSite ) {
            _oSite        = oSite ?? throw new ArgumentNullException();
            _oSiteGrammer = oSite as IPgGrammarSite ?? throw new ArgumentException( "Site must support IGrammerSite." );
        }

		public IPgParent Parentage => _oSite.Host;
		public IPgParent Services  => Parentage.Services;

        private void SendError( string strMessage, string strDetails ) {
            _oSite.LogError( strMessage, strDetails );
        }
        
        public IPgBaseSite Site {
            get { return( _oSite ); }
        }

        public void Dispose() {
        }

        /// <summary>
        /// Returns false, since we don't support a "default" grammer. Tho' in the future
        /// we could load up an empty grammer and then fill it's productions via some editor.
        /// </summary>
        /// <returns></returns>
        public bool InitNew() {
            return( false );
        }

        private bool ReadColors( XmlDocument p_xmldocParse) {
            XmlNodeList xmllistColors = p_xmldocParse.SelectNodes( "bnfstuff/metadata/colortable/color" );

            if( xmllistColors == null ) {
                _oSite.LogError( "internal error", "Color node in the grammar not found!" );
                return( false );
            }

            try {
                foreach( XmlElement oNode in xmllistColors ) {
                    string strName  = oNode.GetAttribute( "name" );
                    string strValue = oNode.GetAttribute( "value" );

                    if( string.IsNullOrEmpty( strName ) )
                        return( false );
                    if( string.IsNullOrEmpty( strValue ) )
                        return( false );

                    _oSiteGrammer.AddColor( strName, strValue );
                }
            } catch( Exception oEx ) {
                Type[] rgErrors = { typeof( ArgumentException ),
                                    typeof( XmlException ),
									typeof( NullReferenceException ) };
				if( rgErrors.IsUnhandled( oEx ) )
					throw;

                return( false );
            }
            
            return( true );
        } 

        protected bool Load( ref XmlDocument p_xmldocParse  ) {
            if( p_xmldocParse == null )
                throw ( new ArgumentNullException() );

            XmlNodeList rgXmlStatesList = p_xmldocParse.SelectNodes("bnfstuff/bnftable/state");

            if( rgXmlStatesList == null || rgXmlStatesList.Count <= 0 ) {
                _oSite.LogError( "The supplied xml document does not contain a collection of states.", string.Empty );
                return( false );
            }

            // Read all colors available to our grammar production elements.
            if( !ReadColors( p_xmldocParse ) ) {
                _oSite.LogError( "Can't read colors in the grammar.", string.Empty );
                return( false );
            }

            // First read in all the states.
            try {
                foreach( XmlElement oXmlState in rgXmlStatesList ) {
                    StateSlot oSlot  = new StateSlot( this, oXmlState );
                    State<T>  oState = new State<T>( this, oSlot );

                    oSlot.Guest = oState;

                    if( !oState.Load(oXmlState) ) {
                        _oSite.LogError( "Unable to load state: " + oXmlState.GetAttribute( "name" ) + ".", string.Empty );
                        return( false );
                    }
                    
                    _rgAvailableStates.Add(oState.Name, oSlot );
                }
            } catch( Exception oEx ) {
                Type[] rgErrors = { typeof( ArgumentException ),
                                    typeof( ArgumentNullException ), 
                                    typeof( XmlException ),
                                    typeof( InvalidCastException ),
									typeof( NullReferenceException ) };
				if( rgErrors.IsUnhandled( oEx ) )
					throw;

                _oSite.LogError( "grammer read error", "Unable to add state to collection,\r" +
                                "probably a duplicate name." );
                return( false );
            }

            // Now that the states have been read we can walk the productions
            // and BIND the instantiated states to the production elements.
            foreach( KeyValuePair<string, StateSlot> oPair in _rgAvailableStates ) {
                if( !oPair.Value.Bind() ) {
                    _oSite.LogError( "Unable to bind state: " + oPair.Value.Guest.Name, string.Empty );
                    return( false );
                }
            }

            return ( true );
        } 

        /// <summary>
        /// Basically we load the entire xml document into the dom and have the 
        /// ParseStates object load up the collection.
        /// </summary>
        /// <param name="oStream"></param>
        /// <returns></returns>
        public bool Load( TextReader oStream ) {
            XmlDocument xmlparse = new XmlDocument();

            try {
                xmlparse.Load(oStream);
            } catch( Exception oEx ) {
                Type[] rgErrors = { typeof( NullReferenceException ),
									typeof( FileNotFoundException ), 
					                typeof (XmlException ) };

				if( rgErrors.IsUnhandled( oEx ) )
					throw;

                _oSite.LogError( "Grammer Error: " + oEx.Message, oEx.StackTrace );

                return( false );
            }

            return ( Load( ref xmlparse ) );
        }

        /// <summary>
        /// Find the state given by it's string identifier. We use this after we have loaded all
        /// the states and then we are ready to bind them to their productions element instances
        /// once that is done once after loading we won't need to use this 'expensive' call again.
        /// </summary>
        /// <param name="strStateName">The state to look for.</param>
        /// <returns>Returns an instance of a state. Null if not found.</returns>
        public State<T> FindState( String p_strStateName ) {
            StateSlot oGuestSite = null;
            
            if( _rgAvailableStates.TryGetValue(p_strStateName, out oGuestSite ) )
                return( oGuestSite.Guest );
            
            return( null );
        }

        /// <summary>
        /// The child states in this grammar call this when binding.
        /// </summary>
        /// <param name="strName">A name of a color mapping like "mailaddress" and not "red"</param>
        /// <returns>index to use for the color in the range interface.</returns>
        public int GetColorIndex( string strName ) {
            return( _oSiteGrammer.GetColorIndex( strName ) );
        }
    } // end class Grammer<T>

} // GeneralParse.Impl
