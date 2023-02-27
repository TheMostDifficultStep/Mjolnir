using System;
using System.Xml;
using System.Collections.Generic;
using System.Text;

using Play.Interfaces.Embedding;

namespace Play.Parse.Impl {
    ///<summary>
    /// Common inherance for both the production elements and the memory elements.
    /// This way we can have either type of element on the stack. So potentially
    /// we don't need to generate the entire parse tree!!
    /// Note: States DO NOT inherit from this class since only production elements
    ///       can have ID's. States aren't unique within productions however
    ///       production states are unique. production states then point to states.
    ///</summary>
    public abstract class ProdBase<T> :
        Nonterminal<T>
    {
        public abstract string ID{ get; }
        public virtual  bool   IsBinding { get { return false; } }
		public virtual  bool   IsWord    { get { return false; } }
		public abstract bool   IsTerm    { get; }
		public virtual  bool   IsVisible { get { return true; } }

        public virtual  bool   Bind( ProdBase<T> oChild ) { return false; }

        // The only person calling this are the MemoryMarker & MemoryBinder
        // If I swap duties of the MemoryState and MemoryBinder I might be
        // able to get rid of this one.
        public virtual  void   SetEnd( int iIndex ) {
        }
    }
    
    /// <summary>
    /// All terminals and non terminals in a production inherit from this class.
    /// Tho' this class is not strictly required to parse and build a parse tree.
    /// It IS required so that we can bind to variables in the system. We can tell
    /// which production is using a particular class. This base class enables the 
    /// basic data binding system.
    /// Note: Binding IDs are different than State names. 
	/// Note: We don't implemnent ToString() here because it'll be overridden on
	/// the terminals or the ProdState.
    /// </summary>
    public abstract class ProdElem<T> : 
        ProdBase<T>,
        IPgLoad<XmlElement> 
    {
        private   string        _strID = String.Empty; // The name of the binding.
        private   Production<T> _oHost;
        protected int           _iMaxMatch = int.MaxValue;
		protected int           _iMinMatch = 1;
		private   bool          _fWord = false;

        // The particular production hosting us. 
        public Production<T> Host 
        {
            set { _oHost = value; }
            get { return( _oHost ); }
        }

        public override string ID
        {
            get {
                return( _strID );
            }
        }

		public override bool IsWord {
			get {
				return( _fWord );
			}
		}

		public override bool IsTerm {
			get {
				return( true );
			}
		}

        // Look for the name attribute on the non/term of a particular production.
        // <term class="foo" id="bindingname">
        //
        // Normally there is only one binding for a n/term however it used to be
        // possible to have more than one
        // <term class="foo" >
        //    <name name="bindingname1" />
        //    <name name="bindingname2" />
        // </term>
        public virtual bool Load(XmlElement p_xmlElement) 
        {
            string strID = p_xmlElement.GetAttribute("id");

            if( !string.IsNullOrEmpty( strID ) ) {
                _strID = strID;
            }

            string strMax = p_xmlElement.GetAttribute( "maxmatch" );
            if( !string.IsNullOrEmpty( strMax ) ) {
                int iTry = 0;
                if( int.TryParse( strMax, out iTry ) ) {
                    _iMaxMatch = iTry;
                }
            }

            string strMin = p_xmlElement.GetAttribute( "minmatch" );
            if( !string.IsNullOrEmpty( strMin ) ) {
                int iTry = 0;
                if( int.TryParse( strMin, out iTry ) ) {
                    _iMinMatch = iTry;
                }
            }

            string strIsWord = p_xmlElement.GetAttribute( "word" );
            if( !string.IsNullOrEmpty( strIsWord ) ) {
                if( string.Compare( strIsWord.ToLower(), "true" ) == 0 ) {
                    _fWord = true;
                }
            }
            return ( true );
        }

        public bool InitNew() 
        {
            return( false );
        }
    } // End ProdElem

    /// <summery>
    /// ProdState's are a little different than ProdTerm's. ProdTerm's are the actual instance
    /// of the terminal that want's to capture the data. ProdState's point to the state that
    /// knows how to process the productions. The idea is that we attempt to invoke
    /// the class in many productions but the data needed to select and push the productions is the
    /// same for all. This is a salient feature of this grammer implementation.
    /// Note: that the id of the state is different depending on which production it is found
    /// another reason we need this object as a stand in.
    /// </summery>
    public class ProdState<T> : ProdElem<T>
    {
        protected State<T> _oState;

        public ProdState(State<T> p_oState)
        {
            if (p_oState == null)
                throw (new ArgumentNullException());

            _oState = p_oState;
        }

        public State<T> Class
        {
            get
            {
                return (_oState);
            }
        }

		public override bool IsTerm {
			get {
				return( false );
			}
		}

        public override string ToString() 
        {
			if( !string.IsNullOrEmpty( ID ) )
				return (_oState.Name + "(id=" + ID + ")" ); 
			
			return( _oState.Name );
        }

        // This might get called when we are first trying to determine which production in a state
        // to use. We never attempt to bind data in this step. When the production is selected
        // and pushed on the stack. The actual memory terminals will recognize and store positions then.
        public override bool IsEqual(
                int           p_iMaxStack,
                DataStream<T> p_oText, 
                bool          p_fLookAhead, 
                int           p_iPos, 
            out int           p_iMatch, 
            out Production<T> p_oProd )
        {
            return (_oState.IsEqual( p_iMaxStack, p_oText, p_fLookAhead, p_iPos, out p_iMatch, out p_oProd));
        }

        public override bool IsBinding { 
            get { 
                return( _oState.Bindings.Count > 0 );
            }
        }

        /// <summary>
        /// Just bind the color since we are a singleton and can't actually
        /// store the child according to the bindings. Saves memory versus
        /// having a MemoryState just to convey color info.
        /// </summary>
        public override bool Bind( ProdBase<T> p_oProdChild ) {
            MemoryElem<T> l_oMemChild = p_oProdChild as MemoryElem<T>;
            if( l_oMemChild != null ) {
                int iIndex = _oState.Bindings.IndexOfKey( p_oProdChild.ID );
                
                if( iIndex > -1 ) {
                    BindingInfo oDecl = _oState.Bindings.Values[iIndex];

                    l_oMemChild.ColorIndex = oDecl.ColorIndex;

                    return( true );
                }
            }
            return (false);
        }
    } // End Class

    public class Production<T> {
        protected readonly StateSiteAbstract<T> _oSite;
        protected readonly List<ProdElem<T>>    _rgChildren = new List<ProdElem<T>>();
        private            int                  _iLook = 0; // Look ahead
        private            int                  _iID   = -1;

        public Production( IPgBaseSite oBaseSite ) {
            _oSite = oBaseSite as StateSiteAbstract<T> ?? throw new ArgumentException( "Site must support  StateSiteAbstract<T>" );
        }

        public IPgBaseSite Site {
            get { return _oSite; }
        }

        public string StateName {
            get { return State.Name; }
        }

        public int Index {
            get { return _iID; }
            set { _iID = value; }
        }

        public int Count {
            get {
                return ( _rgChildren.Count );
            }
        }

        public ProdElem<T> this[ int iIndex ] {
            get {
                if( _rgChildren.Count <= 0 ) {
                    _oSite.LogError( "internal error", "Production " + Index.ToString() + " contains no elements!" );
                    return( null );
                }
                return ( _rgChildren[iIndex] );
            }
        }

        public int LookAhead {
            get {
                return ( _iLook );
            }
        }

        protected void LogError( XmlElement p_oXmlPElem,
                                 string     p_strMessage )
        {
            StringBuilder sbError = new StringBuilder();
            State<T>      oState  = _oSite.Host as State<T>;

            sbError.Append( "There is a production in a state with an error.");
            sbError.Append( "\rState: " );
            sbError.Append( oState.Name );
            sbError.Append( "\rProduction: " );
            sbError.Append( p_oXmlPElem.OuterXml );
            sbError.Append( p_strMessage );

            LogError( sbError.ToString() );
        }

        protected void LogError( string strMessage ) 
        {
            _oSite.LogError( "internal error", strMessage );
        }

        protected State<T> State 
        {
            get {
                return (State<T>)_oSite.Host;
            }
        }

        public bool InitNew() {
            return( false );
        }

        protected ProdElem<T> CreateElem( Grammer<T> oGrammar, XmlElement xmlElem, IGrammerElemFactory<T> oElemFactory ) {
            switch (xmlElem.Name) {
                case "nont": {
                    string   strName = xmlElem.GetAttribute("state");
                    State<T> oState  = oGrammar.FindState(strName);

                    if( oState == null ) {
                        LogError( xmlElem, "Could not find referenced state: " + strName );
                        throw new GrammerImplementationError();
                    }

                    return new ProdState<T>(oState);
                }
                case "term": {
                    ProdElem<T> oElem = oElemFactory.CreateProdTerminal( xmlElem.GetAttribute("class") );
                    if( oElem == null ) {
                        LogError(xmlElem, "Unable to create requested terminal: " + xmlElem.GetAttribute("class") + ".");
                        throw new GrammerImplementationError();
                    }

                    return oElem;
                }
                default:
                    throw new GrammerImplementationError("Unexpected terminal type in production" );
            }
        }

        // Note: we read the productions in the bind method of the states, which
        // happens after read of the states. This is because we need to know 
        // all the states so we can bind to them here in the productions.
        public bool Load(
            XmlElement p_xmlProd // single production with elements.
        ) {
            Grammer<T>             oGrammar     = _oSite.Grammar;
            IGrammerElemFactory<T> oElemFactory = _oSite.Grammar as IGrammerElemFactory<T>;

            if( oGrammar == null ) {
                LogError( "could not cast grammar callback to Grammer<T>" );
                return( false );
            }
            if( oElemFactory == null ) {
                LogError( "could not cast grammar callback to IGrammerElemFactory<T>" );
                return( false );
            }
            if( p_xmlProd == null ) {
                LogError( "When loading the production reader, a valid xmlElement with the production must be provided!" );
                return( false );
            }

            XmlNodeList rgProductionElements = p_xmlProd.SelectNodes("term|nont|virt");

            if( rgProductionElements == null ) {
                LogError( "Could not find valid elements within the production." );
                return( false );
            }

            bool fInTerm = true;

            foreach( XmlNode oXmlPNode in rgProductionElements ) {
                XmlElement oXmlPElem = oXmlPNode as XmlElement;

                try {
                    ProdElem<T> oElem = CreateElem( oGrammar, oXmlPElem, oElemFactory );

                    if( !oElem.Load(oXmlPElem) ) {
                        LogError( oXmlPElem, "Error loading element." );
                        return( false );
                    }

                    if( fInTerm == true && !oElem.IsTerm )
                        fInTerm = false;
                    if( fInTerm )
                        _iLook++;

                    oElem.Host = this;
                    _rgChildren.Add( oElem );
                } catch ( Exception oEx ) {
                    Type[] rgErrors = { typeof( XmlException ),
                                        typeof( ArgumentException ),
                                        typeof( ArgumentNullException ),
                                        typeof( NullReferenceException ),
                                        typeof( GrammerImplementationError ) };
                    if( rgErrors.IsUnhandled( oEx ) )
                        throw;

                    LogError( oXmlPElem, "Unexpected XML Read error in your grammar." );
                    return( false );
                }
            }
            // If nothing got loaded then we don't want to risk loading a binding sentinal, it won't
            // have a MemoryState parent for this production and fail at runtime.
            if( _rgChildren.Count == 0 ) {
                LogError( "There no elements in a production for state: " + State.Name + "." );
                return ( false );
            }
            // We could GP fault if the bindings haven't been loaded yet. But if they are not
            // loaded then we can't do this operation correctly. So let's throw a specific exception.
            if( State.Bindings == null ) {
                LogError( "The state: " + State.Name + ", has not been loaded because" + 
                           " it's bindings have not been initialized." );
                return( false );
            }

			// TODO : Lookahead should be at least as large as all left side terminals. Override Lookahead?
            string strLookAhead = string.Empty;
            try {
                strLookAhead = p_xmlProd.GetAttribute("lookahead");
                if( !string.IsNullOrEmpty( strLookAhead ) )
                    _iLook = Convert.ToInt32( strLookAhead );
            } catch( Exception oEx ) {
                Type[] rgErrors = { typeof( FormatException ), 
                                    typeof( OverflowException ),
                                    typeof( XmlException ),
									typeof( NullReferenceException ) };
				if( rgErrors.IsUnhandled( oEx ) )
					throw;

                LogError( "Invalid lookahead: " + State.Name + "." );
            }

            // This means that we didn't find any starting terminals
            // So we allow the first non terminal to be searched.
            if( _iLook == 0 )
                _iLook = 1;

            if( _iLook > _rgChildren.Count ) {
                LogError( "The look ahead is greater then the" +
                           "\r number of elements in a production!" +
                           "\r State: " + State.Name + "." );
                return( false );
            }

            foreach( ProdElem<T> oChild in _rgChildren ) {
                if( oChild == null ) {
                    LogError( "All children for this production were not instantianted! State: " + State.Name + " Production: " + Index );
                    return( false );
                }
            }

            return ( true );
        }

        /// <summary>
        /// Write out the production in a generic sense. We typically call this
        /// method when the parser selects this production to push on the stack.
        /// Of course since we have not bound anything yet, we can't actually 
        /// show the parsed elements but only what they represent.
        /// </summary>
        /// <returns>a string representing the production.</returns>
        public override string ToString()
        {
            string strOutput = "";

            if( State == null )
                return ( strOutput );

            strOutput += State.Name;

            strOutput += " -> ";

            for( int i = 0; i < _rgChildren.Count; ++i ) {
                strOutput += _rgChildren[i].ToString();
                strOutput += " ";
            }

            return ( strOutput );
        }
    } // End class


    // These are the items in the <capture> block of the bnf xml file. I was
    // originally planning that the capture was a global concept. However,
    // css defies this. We might want to capture CSS at the tag level for inline,
    // and globally for the style block, meaning we might want to capture one
    // production element in multiple places. Also we see that at the tag level
    // we might not have "array" specified but at the global level it must be defined.
    public class BindingInfo {
        string m_strID;
        bool   m_fArray;
        bool   m_fID;
        string m_strName;
        int    m_iColorIndex;
        string m_strColorID = string.Empty;

        public BindingInfo(XmlElement p_oElem )
        {
            if( p_oElem == null )
                throw ( new ArgumentNullException() );

            m_fArray = ( "true" == p_oElem.GetAttribute("array") );
            m_fID    = ( "true" == p_oElem.GetAttribute("display") );

            m_strName    = p_oElem.GetAttribute( "name" );
            m_strID      = p_oElem.GetAttribute( "id" );
            m_strColorID = p_oElem.GetAttribute( "color" ); // This is a name like "tagstart" and not an actual color value.

            if( string.IsNullOrEmpty( m_strID ) )
                throw new ArgumentException( "Bindings must have an ID!" );

            if( 0 == string.Compare( m_strName, m_strID ) )
                throw new ArgumentException( "ID and Name values must not match on a declaration, ID: " + m_strID );
        }

        public bool IsArray
        { 
            get {
                return ( m_fArray ); 
            }
        }

        /// <summary>
        /// Global name of the binding. There should be no duplicates but I go back and
        /// forth on that issue. Because of that the global bindings is somewhat redundent.
        /// </summary>
        public String ID
        {
            get
            {
                return ( m_strID );
            }
        }

        public string Name {
            get { return( m_strName ); }
        }

        public bool IsDisplay
        {
            get { return ( m_fID ); }
        }

        public int ColorIndex
        {
            get
            {
                return( m_iColorIndex );
            }
        }

        /// <summary>
        /// The binding object has a reference to a color entry. Find the index
        /// so we can report it when asked. Faster than looking up the color id all the time.
        /// </summary>
        /// <param name="oGetter">So I don't need to expose an entire interface like a dictionary
        /// to this object I expose only the accessor via a delegate. Seems to be elegant yet at
        /// the same side heavy handed!</param>
        internal void BindColor( GetColorIndex oGetter )
        {
            m_iColorIndex = 0;

            if( oGetter != null ) {
                int iIndex = oGetter( m_strColorID );

                if( iIndex > -1 )
                    m_iColorIndex = iIndex;
            }
        }
 
    } // BindingInfo
}