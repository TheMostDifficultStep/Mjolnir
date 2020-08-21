using System;
using System.Text;
using System.IO;
using System.Reflection;
using System.Xml;

using Play.Interfaces.Embedding; 

namespace Mjolnir {
    public partial class Program {
		/// <summary>
		/// Use this to host grammars. Nice to NOT inherit from BaseSlot because BaseSlot is
		/// used for documents which have views. I don't intend to host grammers in the same manner.
		/// We'll create a separate debugging document for grammers.
		/// </summary>
        public class LangSlot : 
            IPgBaseSite,
            IPgGrammarSite
        {
            readonly Program    _oDoc;
            readonly GrammerMap _oMap;

            protected IPgLoad<TextReader> _oGuestLoad;

            public LangSlot( Program oDoc, GrammerMap oMap ) {
                _oMap = oMap ?? throw new ArgumentNullException( "Grammar map pointer is null" );
                _oDoc = oDoc ?? throw new ArgumentNullException( "Pointer to main program is null" );
            }

            public object Guest {
                set {
					if( _oGuestLoad != null )
						throw new InvalidOperationException( "Guest may only be set once!" );

                    _oGuestLoad = (IPgLoad<TextReader>)value;
                }

                get {
                    return( _oGuestLoad );
                }
            }

     #region IPgBaseSite
            public IPgParent Host                => _oDoc;
			public object    ServicesFromProgram => _oDoc.Services;

            public void LogError(string strCatagory, string strDetails, bool fShow=true) {
                _oDoc.LogError( this, strCatagory, strDetails, fShow );
            }

            public void Notify( ShellNotify eEvent ) { }
    #endregion

    #region IGrammarSite 
            public int AddColor(string strName, string strValue) {
                return( _oDoc.AddColor( strName, strValue ) );
            }

            /// <summary>
            /// Since this is only called to set up the grammer index references it's not going
            /// to hurt performance after the grammer is loaded.
            /// </summary>
            /// <param name="strName"></param>
            /// <returns></returns>
            public int GetColorIndex(string strName) {
                return( _oDoc.GetColorIndex( strName ) );
            }
    #endregion

           public bool Load() {
                if( _oMap.IsResource ) {
                    if( !LoadResource( _oMap.FileName ) )
                        return( false );
                } else {
                    // By default, go to the user profile and search for a "bnf" directory.
                    string strPath = _oMap.FileName;
                    if( !Path.IsPathRooted( strPath ) ) {
                        strPath = Path.Combine( _oDoc.UserProfile, "bnf" );
                        strPath = Path.Combine( strPath, _oMap.FileName );
                    }

                    if( !Load( strPath ) ) 
                        return( false );
                }
                return( true );
            }

            /// <summary>
            /// While we do have this same code living in the TextSlot class. Our code is abreviated
            /// Since we don't have quite the same requirements as a normal Text Document so I'm
            /// ok with the duplication of effort.
            /// </summary>
            public bool Load( string strFileName ) {
                if( string.IsNullOrEmpty( strFileName ))
                    return( false );

                try {
                    FileInfo   oFile       = new System.IO.FileInfo(strFileName);
                    FileStream oByteStream = oFile.OpenRead(); // by default StreamReader closes the stream.
                    // Overridable versions of StreamReader can prevent that in higher versions of .net

                    using( StreamReader oReader = new StreamReader( oByteStream, new UTF8Encoding(false) ) ) {
                        try {
                            return( _oGuestLoad.Load( oReader ) );
                        } catch( NullReferenceException ) {
                            LogError( "grammer", "Died trying to load : " + strFileName );
                        }
                    }
                } catch( Exception oEx ) {
                    Type[] rgTypes = { typeof( DirectoryNotFoundException ),
                                       typeof( FileNotFoundException ),
                                       typeof( IOException ),
                                       typeof( ArgumentException ), // if the drive doesn't exist.
                                       typeof( NullReferenceException ) }; 

                    if( rgTypes.IsUnhandled( oEx ))
                        throw new InvalidProgramException( "Trouble accessing grammer file.");

                    LogError( "grammer", "Could not find or file is currently open :" + strFileName );
                }

                return( false );
            }

            public bool LoadResource( string strResourceName ) {
                Assembly oAssembly = null;
                Stream   oStream   = null;

                try {
                    oAssembly = Assembly.GetExecutingAssembly();
                    oStream   = oAssembly.GetManifestResourceStream( strResourceName );

                    using( StreamReader oReader = new StreamReader( oStream ) ) {
                        return( _oGuestLoad.Load( oReader ) ); 
                    }
                } catch( Exception oEx ) {
                    Type[] rgErrors = { typeof( ArgumentNullException ),   typeof( ArgumentException ),
                                        typeof( FileLoadException ),       typeof( FileNotFoundException ),
                                        typeof( BadImageFormatException ), typeof( NullReferenceException ) };
                    if( rgErrors.IsUnhandled( oEx ) )
                        throw new InvalidProgramException( "Trouble accessing grammer resource.");

                    LogError( "grammer", "Embedded object read error. Tried to read: " + strResourceName );
                }

                return( false );
            }
        }
    }

    public class ExtensionMap {
               string _strAssemblyID;
        public string _strTypeName;
        public string _strGrammar;
        public string _strExtension;

        public ExtensionMap() {
            _strAssemblyID      = string.Empty;
            _strGrammar         = string.Empty;
            _strTypeName        = string.Empty;
            _strExtension       = string.Empty;
        }

        public ExtensionMap( string strExtension, string strGrammar ) {
            _strExtension = strExtension;
            _strGrammar   = strGrammar;

            _strAssemblyID      = string.Empty;
            _strTypeName        = string.Empty;
        }

        public bool IsEmpty {
            get { return( string.IsNullOrEmpty( _strGrammar ) ); }
        }

        public bool Load( XmlElement oXmlElem ) {
            if( oXmlElem == null )
                return( false );

            _strGrammar         = oXmlElem.GetAttribute( "grammar" );
            _strExtension       = oXmlElem.GetAttribute( "extn" );
            _strAssemblyID      = oXmlElem.GetAttribute( "addon" );
            _strTypeName        = oXmlElem.GetAttribute( "handler" );

            if( !string.IsNullOrEmpty( oXmlElem.GetAttribute( "encoding" ) ) ) // obsolete.
                return( false );
            if( string.IsNullOrEmpty( _strGrammar ) )
                return( false );
            if( string.IsNullOrEmpty( _strExtension ) )
                return( false );

            return( true );
        }

        public string Extension {
            get { return( _strExtension ); }
        }

        public string AssemblyID {
            get { return( _strAssemblyID ); }
        }

        public string Handler {
            get { return( _strTypeName ); }
        }
    }

    /// <summary>
    /// At some point we'll inherit from IPgPersistXmlElement. This really should just
    /// be a dictionary object?
    /// </summary>
    public class GrammerMap {
        private string _strName;       // Name of the grammer.
        private string _strStreamType; // Type of data stream the grammer is handling.
        private string _strFilePath;   // location of the XML bnf file.
        private bool   _fResource = false;

        /// <summary>
        /// This little hacknoid is made so we can simply list resources and load them from our assembly instead of a file.
        /// This is in case we loose track of our external BNF's. The user can at least load up the editor and fix things.
        /// </summary>
        /// <param name="strName">Name of the grammer</param>
        /// <param name="strType">Type of data stream the grammer is for.</param>
        /// <param name="strResourceName">name of the embedded resource.</param>
        public GrammerMap( string strName, string strType, string strResourceName ) {
            _strName       = strName;
            _strStreamType = strType;
            _strFilePath   = strResourceName;
            _fResource     = true;
        }

        /// <summary>This is the standard constructor (does nothing) which then must be loaded from an XmlElem (outboard file)</summary>
        public GrammerMap() {
        }

        public bool Load( XmlElement oXmlElem ) {
            // Add exception handling later. Sooner than later, It needs to
            // bubble up so we can fix errors.
            _strName       = oXmlElem.GetAttribute( "name" );
            _strStreamType = oXmlElem.GetAttribute( "type" ); // I'm thinking this should be a classname in a plugin.
            _strFilePath   = oXmlElem.GetAttribute( "file" );

            if( _strName == string.Empty )
                return( false );
            if( _strStreamType == string.Empty )
                return( false );
            if( _strFilePath == string.Empty )
                return( false );

            return( true );
        }

        public string Name {
            get { return( _strName ); }
        }

        public string FileName {
            get { return( _strFilePath ); }
            set { _strFilePath = value; }
        }

        public string StreamType {
            get { return( _strStreamType); }
        }

        public bool IsResource {
            get { return( _fResource ); }
        }
    }
}
