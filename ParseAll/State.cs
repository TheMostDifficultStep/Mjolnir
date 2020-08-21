using System;
using System.Xml;
using System.Collections.Generic;

using Play.Interfaces.Embedding;

namespace Play.Parse.Impl {
    public class State<T> : 
        AbstractState<T>, 
		IPgParent,
        IPgLoadTwoStage<XmlElement>
    {
        private   Production<T>[]                 _rgProductions;
        protected SortedList<string, BindingInfo> _rgBindings;
        private   IPgBaseSite                     _oSite;
        private   GetColorIndex                   _oColorGetter;
        private   Grammer<T>                      _oGrammer;

        public class StateSite : StateSiteAbstract<T> {
            readonly State<T> _oState;

            public StateSite( State<T> oState ) {
				_oState = oState ?? throw new ArgumentNullException();
            }

            public override IPgParent  Host    => _oState;
            public override Grammer<T> Grammar => _oState.Grammar;

			public override void LogError(string strMessage, string strDetails, bool fShow=true) {
                _oState.ReportError( strMessage, strDetails );
            }
        }

        public State ( Grammer<T> oHost, IPgBaseSite oSite ) {
            _oGrammer = oHost ?? throw new ArgumentNullException( "Host must not be null" );
            _oSite    = oSite ?? throw new ArgumentNullException( "Site must not be null" );
        }

        public Grammer<T> Grammar {
            get { return _oGrammer; }
        }

		public IPgParent Parentage => _oSite.Host;
		public IPgParent Services  => Parentage.Services;

        public IPgBaseSite Site  {
            get { return _oSite; }
        }

        private void ReportError(  string strMessage, string strDetails ) 
        {
            _oSite.LogError( strMessage, strDetails );
        }

        public override Production<T>[] Productions {
            get {
                return _rgProductions;
            }
        }
        
        public SortedList<string, BindingInfo> Bindings
        {
            get {
                return( _rgBindings );
            }
        }

        public bool IsBinding {
            get { 
                try {
                    return( _rgBindings.Count > 0 ); 
                } catch( NullReferenceException  ) {
                    return( false );
                }
            }
        }

        public bool InitNew() {
            return( false );
        }

        public virtual bool Load( XmlElement p_elemState )
        {
            m_strName = p_elemState.GetAttribute("name");

            _oColorGetter = new GetColorIndex( Grammar.GetColorIndex );

            if( _oColorGetter == null ) {
                _oSite.LogError( "state read error", "Can't get grammer's color indexer" );
                return( false );
            }
            if( string.IsNullOrEmpty( m_strName  ) ) {
                _oSite.LogError( "state read error", "State must be named." );
                return( false );
            }

            XmlNodeList xmlDeclarations = p_elemState.SelectNodes("capture/binding");

            _rgBindings = new SortedList<string, BindingInfo>(xmlDeclarations.Count);

            for( int i = 0; i < xmlDeclarations.Count; ++i ) {
                XmlElement  xmlDeclare = (XmlElement)xmlDeclarations[i];
                BindingInfo oDeclare   = new BindingInfo(xmlDeclare);
                
                if( _rgBindings.ContainsKey( oDeclare.ID ) ) {
                    _oSite.LogError( "state read error", "bindings section of a state: " + m_strName + ", contains duplicates!" );
                    return( false );
                }

                _rgBindings.Add( oDeclare.ID, oDeclare );
            }

            return( true );
        }

        /// <summary>
        /// Read in the productions during the binding phase. This is necessary because
        /// we need references to all the states before we can bind any nonterminal in a production
        /// This is like OnLoadComplete.
        /// </summary>
        public virtual bool Bind( XmlElement p_oXmlState ) 
        {
            // Bind colors
            foreach( BindingInfo oDecl in _rgBindings.Values ) {
                oDecl.BindColor( _oColorGetter );
            }

            XmlNodeList         xmlProductionList = p_oXmlState.SelectNodes( "production" );
            List<Production<T>> rgProductions     = new List<Production<T>>();

            // Bind production to terminals and other states!
            int iProductionCount = 0;
            foreach( XmlElement xmlProduction in xmlProductionList ) {
				Production<T> oProduction = new Production<T>(new StateSite(this)) {
					Index = iProductionCount++
				};

				if( !oProduction.Load( xmlProduction ) ) {
                    return( false );
                }
                rgProductions.Add( oProduction );
            }

            _rgProductions = rgProductions.ToArray();

            return( true );
        }
    } // End State
}
