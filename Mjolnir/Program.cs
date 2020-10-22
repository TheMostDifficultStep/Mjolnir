using System;
using System.Collections.Generic;
using System.Windows.Forms;
using System.IO;
using System.Xml;
using System.Xml.XPath;
using System.Text;
using System.Reflection;
using System.Drawing;
using System.Linq;

using SkiaSharp;

using Play.Interfaces.Embedding; 
using Play.Parse.Impl;
using Play.Edit;
using Play.Sound;
using Play.Integration; // tag grammer used here... Hmmmm.. check this out. Probably move to Play.Parse.Impl

namespace Mjolnir {
    public delegate void UpdateAllTitlesFor( IDocSlot oSlot );

    public partial class Program : 
		IPgParent,
		IPgLoad<TextReader>,
		IPgSave<TextWriter>,
		IPgScheduler,
		IPgSound,
		IPgMorseWorkerCreater,
		IPgGrammers,
		IPgStandardUI2,
		IDisposable
    {
        public static Guid FindDialog  { get; } = new Guid( "{231E4D61-499A-427E-A1D3-EC4A579A5E6D}" );
        public static Guid MatchesView { get; } = new Guid( "{827841DE-DC4B-4970-B539-A32ED521E4FE}" );
        public static Guid ViewSelector{ get; } = new Guid( "{195E19DB-4BCE-4CAE-BE02-263536F00851}" );
        public static Guid MainWin     { get; } = new Guid( "{B091DED3-33C8-4BD1-8390-CA568CA7F9FC}" );

        string _strDefaultGrammarDir = String.Empty;

        List<IPgController2> Controllers { get; }  = new List<IPgController2>();

        readonly SKColor[]     _rgStdColors  = new SKColor[(int)StdUIColors.Max ];
        readonly List<SKColor> _rgTxtColors  = new List<SKColor>();

        readonly List<ColorMap>                   _rgGrammarColors = new List<ColorMap>();
        readonly Dictionary<string, ExtensionMap> _rgExtensionMap  = new Dictionary<string, ExtensionMap>();
        readonly Dictionary<string, LangSlot>     _rgLanguageSite  = new Dictionary<string, LangSlot>();    // Load on demand
        readonly Dictionary<string, GrammerMap>   _rgGrammarMap    = new Dictionary<string, GrammerMap>();  // Initialized on startup.

        protected            bool  _fSessionDirty = false;

        protected readonly List<IDocSlot> _rgDocSites = new List<IDocSlot>(); 
        int    _iDocCount = 0;    // Running count of files created for uniqueness. TODO deal with overflow.
        string _strLastPath;

		public MainWin MainWindow { get; private set; }

        public event UpdateAllTitlesFor EventUpdateTitles;

		readonly Mpg123Factory            _oMp3Factory = new Mpg123Factory();
        readonly Timer                    _oTimer      = new Timer();
        readonly List <IPgRoundRobinWork> _rgWorkers   = new List<IPgRoundRobinWork>();

        protected Editor _oDoc_Alerts;
        protected Editor _oDoc_Recents;

        // The textslots and xmlslots we could make cache the editor pointers on load
        // so we spot load errors sooner instead of later after the program boots.
        protected InternalSlot      _oDocSlot_Scraps;
        protected TextSlot          _oDocSlot_Alerts;
        protected TextSlot          _oDocSlot_Results;
        protected XmlSlot           _oDocSlot_Recents;
        protected InternalSlot      _oDocSlot_Fonts;
        protected XmlSlot           _oDocSlot_SearchKey;
        protected ComplexXmlSlot    _oDocSlot_Find;
        protected Program.TextSlot  _oDocSite_Session; // Hosting ourself, so don't be confused! ^_^;
          
        /// <summery>Views can use this to create views on the scrapbook</summery>
        public InternalSlot ScrapBookSlot => _oDocSlot_Scraps;
        public XmlSlot      RecentsSlot   => _oDocSlot_Recents;
        public TextSlot     SessionSlot   => _oDocSite_Session;
		public TextSlot     ResultsSlot   => _oDocSlot_Results;
		public XmlSlot      SearchSlot    => _oDocSlot_SearchKey;
        public IDocSlot     FindSlot      => _oDocSlot_Find;
        public IDocSlot     AlertSlot     => _oDocSlot_Alerts;

        // BUG: I'm dithering on FontMenu living on the program or just the main window.
		public Font         FontMenu      { get; } = new Font( "Segoe UI Symbol", 11 ); // Segoe UI Symbol, So we can show our play/pause stuff.
        public Font         FontStandard  { get; } = new Font( "Consolas", 11 ); // Consolas
		public bool         IsDirty       => _fSessionDirty;

		protected Alerts _oWin_AlertsForm;

        readonly Dictionary<string, Assembly> _rgAddOns = new Dictionary<string,Assembly>();

        private FTManager _oFTManager;

        // https://docs.microsoft.com/en-us/xamarin/xamarin-forms/platform/other/gtk?tabs=windows
        [STAThread]
        static void Main(string[] rgArgs) {
            Application.OleRequired();
            Application.SetHighDpiMode(HighDpiMode.SystemAware);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.AddMessageFilter(new MouseWheelMessageFilter());

			using( Program oProgram = new Program() ) {
			    XmlDocument xmlConfig = new XmlDocument();

                // Tecnically it's not the end of the world if we can't read our
                // config. But we'd be really messed up. So until I've fixed all the
                // problems with a config error, just bail on the whole thing.
                try {
			        oProgram.Initialize( xmlConfig );
                } catch( Exception oEx ) {
                    oProgram.TryLogXmlError( oEx, "Couldn't read global config." );
				    return;
                }
                // It's tempting to put the main window stuff in the initialize procedure, 
                // but I might separate the xmlConfig for the program and the window and
                // then I could set up the window on demand, based on the persistance data.
			    try {
				    oProgram.SessionLoad( rgArgs, xmlConfig );
                }
                catch ( Exception oEx ) {
                    oProgram.TryLogXmlError( oEx, "Couldn't configure Main window." );

                    return;
			    }

				try {
					Application.Run( oProgram.MainWindow );
				} catch( Exception oEx ) {
					oProgram.LogError( "internal", oEx.Message );
                    oProgram.BombOut( oEx );
					// BUG: This would be a great place for use to write out the alerts
					//      and this last error to a file somewhere. ^_^;;
					Console.WriteLine( oEx.Message );
				}
			}
        }

        private Program() {
            _oTimer.Interval = 100;
            _oTimer.Tick += new EventHandler( OnTimer ); // BUG: shoudn't do this in the constructor.

            PlainTextController = new ControllerForPlainText(this); // BUG: shoudn't do this in the constructor.

            // New color table system.
            try {
                _rgStdColors[ (int)StdUIColors.BGWithCursor]    = new SKColor( 230, 230, 230 ); // BGWithCursor,    Unselected w/  cursor. 
			    _rgStdColors[ (int)StdUIColors.BGSelectedFocus] = new SKColor(  49, 106, 197 ); // BGSelectedFocus, Selected   w/  focus blue.
			    _rgStdColors[ (int)StdUIColors.BGSelectedBlur]  = new SKColor( 130, 130, 130 ); // BGSelectedBlur,  Selected   w/o focus grey.
			    _rgStdColors[ (int)StdUIColors.BG]              = new SKColor( 255, 255, 255 ); // BG,              Unselected w/o cursor. (was 244)
			    _rgStdColors[ (int)StdUIColors.BGReadOnly]      = new SKColor( 220, 220, 220 ); // BGReadOnly,      Some kind of light grey.
			    _rgStdColors[ (int)StdUIColors.Text]            = new SKColor(   0,   0,   0 ); // Text,            Black.
			    _rgStdColors[ (int)StdUIColors.TextSelected]    = new SKColor( 255, 255, 255 ); // TextSelected,    White.
			    _rgStdColors[ (int)StdUIColors.MusicLine]       = new SKColor( 200, 255, 200 ); // MusicLine,       Light green.
			    _rgStdColors[ (int)StdUIColors.MusicLinePaused] = new SKColor( 255, 255, 150 ); // MusicLinePaused, Light yellow.
            } catch( IndexOutOfRangeException ) {
                // BUG: I'd like to log it but nothing is ready to go at this point! I should probably make
                //      an array to hold REALLY EARLY errors like this and then spew 'em when we're we're able.
            }

            _oFTManager = new FTManager();
        }

		public void Dispose() {
			// BUG : I need to dispose any open workers.
			//foreach( IPgRoundRobinWork oWorker in _rgWorkers ) {
			//}

			MainWindow  .Dispose(); // BUG: Opps, this might be redundant. Check it.
			_oMp3Factory.Dispose();
			FontStandard.Dispose();
			FontMenu    .Dispose();
		}

        public string AppDataPath => Environment.GetFolderPath( Environment.SpecialFolder.LocalApplicationData) + "\\pg\\mjolnir";
        public string UserProfile => Environment.ExpandEnvironmentVariables("%USERPROFILE%");
        public string UserDocs    => Environment.GetFolderPath(Environment.SpecialFolder.Personal);

		public IPgParent Parentage => null;
		public IPgParent Services  => this;

		public ICollection<IDocSlot> DocSlots            => _rgDocSites;
        public IPgController2        PlainTextController { get; }
        public Editor                DocRecents          => _oDoc_Recents;

		public void AlertsShow() {
			try {
				if( _oWin_AlertsForm == null )
					_oWin_AlertsForm = new Alerts( this );

				_oWin_AlertsForm.Show();
			} catch( Exception oEx ) {
                Type[] rgErrors = { typeof( InvalidOperationException ),
                                    typeof( NullReferenceException ) };
                if( rgErrors.IsUnhandled( oEx ) )
                    throw;
				// Log this error, but don't try to show this window, looks like it's busted.
				LogError( null, "internal", "Coudn't open Alerts window.", false );
			}
		}

        public void BombOut( Exception oOutGoingEx ) {
            try {
                string strFile = Path.Combine(AppDataPath, "bombout.txt" );
                using ( Stream oStream = new FileStream( strFile, FileMode.Create, FileAccess.Write ) ) {
                    using( StreamWriter oWrite = new StreamWriter( oStream ) ) {
                        oWrite.Write( oOutGoingEx.StackTrace );
                    }
                }
            } catch( Exception oEx ) {
                Type[] rgErrors = { typeof( ArgumentException ),
                                    typeof( ArgumentNullException ),
                                    typeof( ArgumentOutOfRangeException ),
                                    typeof( NotSupportedException ),
                                    typeof( System.Security.SecurityException ),
                                    typeof( FileNotFoundException ),
                                    typeof( IOException ),
                                    typeof( DirectoryNotFoundException ),
                                    typeof( PathTooLongException ),
                                    typeof( InvalidDataException )
                                  };
                if( rgErrors.IsUnhandled( oEx ) ) {
                    throw;
                }
            }
        }

		/// <summary>
		/// In this case these would be global documents you might want to show all the time.
		/// Or use for some kind of "first load" document display. Not currently called.
		/// </summary>
        protected void InitializeDocuments( XmlDocument xmlDocument ) {
            XmlNodeList lstDocs = xmlDocument.SelectNodes("config/mainwindow/documents/document");

            foreach( XmlElement xeDoc in lstDocs ) {
                MainWindow.DocumentShow( xeDoc.GetAttribute("path") );
            }
        }

		/// <summary>
		/// TODO, 1/2/2018 : Hmmm... I see we're loading the assemblies, but we need to grab any controllers
		/// out of it to really be useful. This looks like a great next step to work on.
		/// </summary>
        protected void InitializePlugins( XmlDocument xmlDoc ) {
            XmlNodeList lstTypes = xmlDoc.SelectNodes("config/addons/add");

            foreach( XmlElement xeType in lstTypes ) {
                string strFile = xeType.GetAttribute( "assembly" );
                string strID   = xeType.GetAttribute( "id" );

                try {
                    if( !string.IsNullOrEmpty( strFile ) ) {
                        _rgAddOns.Add( strID, Assembly.LoadFile( strFile ) );
                    }
                } catch( Exception oEx ) {
                    Type[] rgErrors = { typeof( FileLoadException ), 
                                        typeof( FileNotFoundException ), 
                                        typeof( BadImageFormatException ),
                                        typeof( ArgumentException ) };
                    if( rgErrors.IsUnhandled( oEx ) )
                        throw;

                    LogError( null, "internal", "Can't load plugin, " + strFile );
                }
            }
        }

        public void RecentsAddListener( ILineEvents oLineEvents ) {
            _oDoc_Recents.ListenerAdd( oLineEvents );
        }

        private string LastPath {
            get { 
                if( string.IsNullOrEmpty( _strLastPath ) )
                    return( _strLastPath );

                return( _strLastPath );
            }

            set {
                if( !string.IsNullOrEmpty( value ) )
                    _strLastPath = value;

                _strLastPath = string.Empty;
            }
        }

        public void LogError( string strCatagory, string strDetails ) {
            LogError( null, strCatagory, strDetails );
        }

		// This keeps us from re-entering the Show alerts when we are attempting to show alerts! ^_^;
		bool _fLogging = false;

		/// <summary>
		/// We're a pretty complicated program. Sometimes we try to log errors before we've set up
		/// our internals! Right now those events are dropped since the Doc_Alerts might not be set
		/// up yet. 
		/// BUG: Set up a simple string array to collect startup errors. We can pop up a
		/// dialog, or write a file and then die. That would be way useful.
		/// </summary>
        public void LogError( IPgBaseSite oSite, string strCatagory, string strDetails, bool fAlert = true ) {
            if( strDetails == null )
                strDetails = string.Empty;
            if( strCatagory == null )
                strCatagory = "Error";

			// If we're already logging an error we might be doing it from within the
			// alerts doc! Don't want to log indefinitely!!
			if( !_fLogging ) { // Note : Not multithread safe. Not that anything else is... ^_^;;
				_fLogging = true;
				try {
					_oDoc_Alerts.LineAppend( strCatagory + "..." + strDetails );
				} catch( NullReferenceException ) {
				}
				if( fAlert ) {
					AlertsShow(); // BUG: We need to set up event sinks on our main window.
				}
				_fLogging = false;
			}
        }

        private void Raise_UpdateTitles( IDocSlot oSlot ) {
            EventUpdateTitles?.Invoke(oSlot);
        }

        /// <summary>
        /// Sites use this to generate a unique title for new documents. 
        /// </summary>
        private int DocCount {
            get { return _iDocCount++; }
        }
        
        public int AlertCount {
            get { return _oDoc_Alerts.ElementCount; }
        }
		/// <summary>
		/// Load up the program global settings. This is not the same as loading session state.
		/// </summary>
        protected void Initialize( XmlDocument xmlConfig ) {
            // This works only because the plain text editor doesn't use the parser.
            // TODO: Maybe we can tack one on AFTER the grammars have successfully loaded.
            // So then we get all the parser features!!
            _oDocSlot_Alerts = new InternalSlot(this, "Alerts");
            _oDocSlot_Alerts.CreateDocument();
            _oDocSlot_Alerts.InitNew();
            _oDoc_Alerts = (Editor)_oDocSlot_Alerts.Document;

            // BUG: If this fails, we're borked. Need to make us more resilient to failing to load config.
            if ( !LoadConfigDoc( xmlConfig ) )
                throw new ApplicationException( "Couldn't load configuration" );

            // Store our cached fonts so we can look 'em up quickly.
            _oDocSlot_Fonts = new InternalSlot(this, "Fonts");
            _oDocSlot_Fonts.CreateDocument();
            try {
                if( _oDocSlot_Fonts.Document is Editor docFonts ) {
                    using( Editor.Manipulator oManip = docFonts.CreateManipulator() ) {
				        foreach( XmlNode xmlNode in xmlConfig.SelectNodes("config/fonts/font") ) {
					        if (xmlNode is XmlElement xmlElem) {
						        Line oLine = oManip.LineAppendNoUndo( xmlElem.InnerText );
                                oLine.Extra = xmlElem.GetAttribute("system");
                            }
				        }
                    }
                    foreach( Line oLine in docFonts ) {
                        FaceCache( oLine.ToString() );
                    }
                }
            } catch( Exception oEx ) {
                TryLogXmlError( oEx, "Couldn't load program fonts." );
            }

            InitializeLanguages( xmlConfig );

            // Parser color table. BUG: Technically this can get new colors over time.
			foreach( ColorMap oMap in _rgGrammarColors ) {
				Color sColor = Color.FromName( oMap._strValue );
                _rgTxtColors.Add( new SKColor( sColor.R, sColor.G, sColor.B ) );
			}

            // old init alerts here, but moved to top...

            InitializePlugins    ( xmlConfig );
            InitializeControllers();

            _oDocSlot_Recents = new XmlSlot(this, ".txt", "Recent" );
            _oDocSlot_Recents.CreateDocument();
            _oDocSlot_Recents.InitNew();
            _oDoc_Recents = (Editor)_oDocSlot_Recents.Document;

            using( Editor.Manipulator oManip = _oDoc_Recents.CreateManipulator() ) {
				try {
					XmlNodeList lstFaves = xmlConfig.SelectNodes("config/mainwindow/favorites/fav");
					for( int i=0; i<lstFaves.Count; ++i ) {
						if (lstFaves[i] is XmlElement xmlNode) {
							Line oLine = oManip.LineAppendNoUndo(xmlNode.GetAttribute("src"));
						}
					}
				} catch( XPathException ) {
				}
            }

			_oDocSlot_Scraps = new InternalSlot(this, ".scraps", "Scraps");
            _oDocSlot_Scraps.CreateDocument();
			_oDocSlot_Scraps.InitNew();

 			// BUG: it's part of the window session load/init sequence. And the MainWin is trying
			// to get at the parse handler in it's constructor. So we've got to InitNew/Load before
			// that. So I'll InitNew() now and let load get called subsequently...for now. ^_^;;
			// NOTE: I would love to swap parsers on the fly (txt/regex) for the search key!!!!
			//ControllerForParsedText oFactory = new ControllerForParsedText( this );
			_oDocSlot_SearchKey = new XmlSlot( this, ".regex", "Find String" );
            _oDocSlot_SearchKey.CreateDocument();
			_oDocSlot_SearchKey.InitNew(); 

            _oDocSlot_Results = new InternalSlot( this, ".txt", "Find Results" );
            _oDocSlot_Results.CreateDocument();
            _oDocSlot_Results.InitNew();

            _oDocSlot_Find = new ComplexXmlSlot( this );
            _oDocSlot_Find.CreateDocument();
            _oDocSlot_Find.InitNew();
        }

        /// <summary>
        /// Basically all the errors that can happen while we're trying to load
        /// up or configuration xml file and our session xlm file.
        /// </summary>
		void TryLogXmlError(Exception oEx, string strMessage)
        {
            Type[] rgErrors = { typeof( XPathException ),
                                typeof( XmlException ),
                                typeof( NullReferenceException ),
                                typeof( InvalidOperationException ),
                                typeof( ArgumentNullException ),
                                typeof( FormatException ),
                                typeof( OverflowException ),
                                typeof( GrammerNotFoundException ),
                                typeof( InvalidCastException ),
                                typeof( ApplicationException ) };
            if (rgErrors.IsUnhandled(oEx))
                throw oEx;

            this.LogError("program session", strMessage);
        }

        /// <summary>
        /// Self hosting ourselves! Look for the first session in the command line. 
        /// If found, Load() from it. Else, InitNew(). 
        /// </summary>
        protected void SessionLoad( string[] rgArgs, XmlDocument xmlConfig ) {
			List<string> rgArgsClean = new List<string>(5);
			int          iPvs        = -1;

			try {
				for( int i=0; i<rgArgs.Length; ++i ) {
					// Just lowercase a copy of the extension don't mess with the
					// case of the string! Many objects are file name case sensitive!
					string strExt = Path.GetExtension( rgArgs[i] ).ToLower();

					if( Path.GetExtension( strExt ) != ".pvs" ) {
						rgArgsClean.Add( rgArgs[i] );
					} else {
						iPvs = i;
					}
				}
			} catch( Exception oEx ) {
				Type[] rgErrors = { typeof( NullReferenceException ),
									typeof( ArgumentException ),
									typeof( ArgumentNullException ) };
				if( rgErrors.IsUnhandled( oEx ) )
					throw;
			}

            // MainWindow references this, so got to initialize it first.
			_oDocSite_Session = new Program.SessonSlot( this );

            // BUG: In the future, we'll move this into the program initnew/load.
            MainWindow = new MainWin(this);
            MainWindow.Initialize(xmlConfig);

            try {
				if( iPvs < 0 ) {
					_oDocSite_Session.InitNew();
				} else {
					_oDocSite_Session.Load( rgArgs[iPvs] );
				}
			} catch( Exception oEx ) {
				Type[] rgErrors = { typeof( NullReferenceException ),
									typeof( ArgumentException ),
									typeof( ArgumentNullException ),
									typeof( InvalidOperationException ),
									typeof( IndexOutOfRangeException ) };
				if( rgErrors.IsUnhandled( oEx ) )
					throw;

                LogError( "Session", "Session Init/Load error" );
			}

            MainWindow.DocumentShowAll(rgArgsClean);
        }

        public bool InitNew() {
			if( !MainWindow.InitNew() ) {
                LogError( "program initnew", "Couldn't initialize main window." );
				return false;
			}

			_fSessionDirty = false;

			return true;
		}

		/// <summary>
		/// Sesson load method. Unlike Initialize these can techically fail and it would not
        /// be catastropic. I also Load the window here, which is slighly weird since I refused
        /// to Initialize the main window within the Program.Initialize() code. I feel this is
        /// OK, since there is nothing wrong with using our session stream to save window data.
        /// We just need a better "view manager" for top level windows.
		/// </summary>
		/// <seealso cref="SessionDirtySet"/>
        /// <seealso cref="Initialize"/>
		public bool Load( TextReader oSessionStream ) {
            XmlDocument xmlSession    = new XmlDocument();
			XmlNodeList rgSessionDocs = null;

            try {
                xmlSession.Load( oSessionStream );
				rgSessionDocs = xmlSession.SelectNodes("Session/Documents/FileName");
            } catch( Exception oEx ) {
                TryLogXmlError( oEx, "Couldn't read xml config." );
            }

			try {
				// Load up the documents.
				foreach( XmlElement xmlDoc in rgSessionDocs ) {
					if (string.IsNullOrEmpty(xmlDoc.InnerText) )
						throw new InvalidOperationException( "filename innertext failure");

					int iDocID = int.Parse(xmlDoc.GetAttribute("docid") );

					// BUG: Need to add some code to validate Doc ID.
					IDocSlot oDocSite = DocumentCreate( xmlDoc.InnerText, iDocID );

					// If oDocSite is null, I should create a place holder document that you can use
					// to edit the link or try again some time.
					if( oDocSite == null )
						throw new InvalidOperationException( "Couldn't create document" );
				}
			} catch( Exception oEx ) {
				TryLogXmlError( oEx, "Trouble reading documents from session file" );
			}

            // Load up the sessions recents.
			try {
				XmlElement xmlFaves = xmlSession.SelectSingleNode( "Session/Recent" ) as XmlElement;
				_oDoc_Recents.Clear(); // Clear the global favorites.
				RecentsSlot.Load( xmlFaves );

                // Sometimes blanks get in there. Remove them.
                using( Editor.Manipulator oManip = _oDoc_Recents.CreateManipulator() ) {
                    for( int iLine = _oDoc_Recents.ElementCount - 1; iLine >= 0; --iLine ) {
                        if( _oDoc_Recents[iLine].ElementCount <= 0 )
                            oManip.LineDelete( iLine );
                    }
                }
			} catch( Exception oEx ) {
                TryLogXmlError( oEx, "Couldn't read recents list." );
			}

            // Load up the find string.
			try {
                XmlElement xmlFindString = xmlSession.SelectSingleNode( "Session/FindString" ) as XmlElement;
                SearchSlot.Load( xmlFindString );
			} catch( Exception oEx ) {
                TryLogXmlError( oEx, "Couldn't read find string." );
			}

            // BUG: We can improve our fault tolerance by improving the chance that InitNew get's called
            //      in the event of an xmlexception or failed load.
			try {
                XmlElement xmlMainWindow = xmlSession.SelectSingleNode( "Session/Windows/Window[@name='MainWindow']" ) as XmlElement;
				if( MainWindow is IPgLoad<XmlElement> oWinLoad ) {
					if( !oWinLoad.Load( xmlMainWindow ) ) 
						throw new InvalidOperationException();
				} else {
					MainWindow.InitNew();
				}
			} catch( Exception oEx ) {
                TryLogXmlError( oEx, "Couldn't load main window session state." );
			}

            // TODO: See SessionSetDirty() for notes about this. It should not be dirty right from initialization
            //       but as we load views it sets the dirty bit. Need to figure a way around that. Especially if
            //       setting the bit starts sending notifications.
            _fSessionDirty = false;

			MainWindow.SetTitle(); // Little hack for the session state.
                
			return true;
		}

		public bool Save( TextWriter oSessionStream ) {
			void LogError( Exception oEx, string strMessage ) {
				Type[] rgErrors = { typeof( XPathException ),
									typeof( XmlException ),
									typeof( NullReferenceException ),
									typeof( InvalidOperationException ) };
				if( rgErrors.IsUnhandled( oEx ) )
					throw oEx;

                this.LogError( "program session", "Couldn't read find string." );
			}

            XmlDocument xmlRoot    = new XmlDocument();
            XmlElement  xmlSession = xmlRoot.CreateElement( "Session" );
			XmlElement  xmlWindows = xmlRoot.CreateElement( "Windows" );
            XmlElement  xmlDocs    = xmlRoot.CreateElement( "Documents" ); 

			try {
				xmlRoot   .AppendChild( xmlSession );
				xmlSession.AppendChild( xmlDocs );
				xmlSession.AppendChild( xmlWindows );
			} catch( Exception oEx ) {
				LogError( oEx, "Problem creating session/documents in session stream" );
				return false;
			}

            foreach( IDocSlot oDocSite in DocSlots ) {
                // If It doesn't have a file name I'm just dropping it from the session.
                if( !string.IsNullOrEmpty( oDocSite.FileName ) ) {
                    XmlElement xmlFile = xmlRoot.CreateElement( "FileName" );

					xmlFile.SetAttribute( "docid", oDocSite.ID.ToString() );
                    xmlFile.InnerText = oDocSite.FileName;

                    xmlDocs.AppendChild( xmlFile );
                }
            }

            if (  RecentsSlot != null ) {
                XmlElement xmlFaves = xmlRoot.CreateElement( "Recent" );
                xmlSession.AppendChild( xmlFaves );
                // Since we can't control how much goes into the recent's file. We'll
                // just lob off the bottom of file. Only save the top ten elements.
                using( Editor.Manipulator oManip = DocRecents.CreateManipulator() ) { 
                    while( DocRecents.ElementCount > 10 ) { 
                        oManip.LineDelete( DocRecents.ElementCount - 1 );
                    }
                }
                RecentsSlot.Save( xmlFaves );
            }

            if( SearchSlot != null ) {
                XmlElement xmlFindKey = xmlRoot.CreateElement( "FindString" );
                xmlSession.AppendChild( xmlFindKey );
                SearchSlot.Save( xmlFindKey );
            }

			try {
				XmlDocumentFragment xmlFrag       = xmlRoot.CreateDocumentFragment();
                XmlElement          xmlMainWindow = xmlRoot.CreateElement( "Window" );

				xmlMainWindow.SetAttribute( "name", "MainWindow" );

				if( !MainWindow.Save( xmlFrag ) ) {
					this.LogError( "program", "Main Window save error" );
					throw new InvalidOperationException("Couldn't save main window.");
				}

				xmlMainWindow.AppendChild( xmlFrag );
				xmlWindows.AppendChild( xmlMainWindow );
            } catch( Exception oEx ) {
                LogError( oEx, "Couldn't save Main window xml fragment into session." );
            }

            try {
                xmlRoot.Save( oSessionStream );
            } catch( Exception oEx ) {
                LogError( oEx, "Couldn't xave xml session into stream." );
            }

            _fSessionDirty = false;

            return( true );
		}

        public void DocumentsSaveAll() {
            foreach( IDocSlot oSite in _rgDocSites ) {
                if( oSite.IsDirty )
                    oSite.Save(false);
            }
            // Only save session it if it's got a file name. If not then don't bother the user.
            if( !string.IsNullOrEmpty( _oDocSite_Session.FileName ) && _oDocSite_Session.IsDirty ) {
                _oDocSite_Session.Save(false);
            }
        }

        public void DocumentsClean( IDocSlot oDocSite ) {
            // Check if we want to remove the document. Documents like the built in Find-Results and Messages
            // will have a reference from us blocking the document removal.
            if( _rgDocSites.Contains( oDocSite ) && oDocSite.Reference <= 0 ) {
                _rgDocSites.Remove( oDocSite );
                // This will close any associated tool windows. We can't get here if the tool window
                // view holds a reference on the docsite.
                oDocSite.Dispose();
            }
        }

        public class PlainTextDesc :
            PgDocumentDescriptor 
        {
            public PlainTextDesc( string strFileExtn, IPgController2 oController ) :
                base( strFileExtn, typeof( IPgLoad<TextReader> ), 0, oController ) 
            { }

            public string Message {
                get {
                    StringBuilder sbMessage = new StringBuilder();

                    sbMessage.Append( "Couldn't find a controller for: " );
                    sbMessage.Append( FileExtn );
                    sbMessage.Append( ", trying " );
                    sbMessage.Append( Controller.PrimaryExtension );
                    sbMessage.Append( "." );

                    return sbMessage.ToString();
                }
            }
        }

        /// <summary>
        /// What we really should do is save the controller for the document type we actually
        /// want to use in these case and just grab it. Basically we want Editor for text and
        /// ImageWalkerFile for images.
        /// </summary>
        /// <param name="strFileExtn"></param>
        /// <returns></returns>
		[Obsolete]public IPgController2 GetController( string strFileExtn, bool fSendMessage = false ) {
            PlainTextDesc        oPlainDesc  = new PlainTextDesc( strFileExtn, PlainTextController );
            PgDocumentDescriptor oDocDesc    = oPlainDesc;

            foreach( IPgController2 oTryMe in Controllers ) {
                PgDocumentDescriptor oTryDesc = oTryMe.Suitability( strFileExtn );
                if( oTryDesc.CompareTo( oDocDesc ) > 0 &&
                    oTryDesc.StgReqmnt == typeof( IPgLoad<TextReader> ) ) {
                    oDocDesc = oTryDesc;
                }
            }

            if( oDocDesc == oPlainDesc && fSendMessage ) {
                LogError( null, "host", oPlainDesc.Message );
            }

            return( oDocDesc.Controller );
        }

        /// <summary>
        /// Create a new document from file name. We try supported file types first then browsable second.
        /// but we don't rank in either catagory. Need to look into that.
        /// </summary>
        /// <param name="strFileName">FileName with path or simply extension like ".foo"</param>
        /// <param name="fLoadFile">True if want to Load() the file, false if simply InitNew().</param>
        public IDocSlot DocumentCreate( string strFileName, int iID = -1 ) {
			try {
				_iDocCount++; // BUG: Deal with overflow. Make the collection a dictionary.
				if( _iDocCount <= iID )
					_iDocCount = iID + 1;
			} catch( OverflowException ) {
				LogError( "embedding", "Internal document count error" );
				return null;
			}

            IDocSlot      oDocSite;
            string        strFileExtn = Path.GetExtension(strFileName).ToLower();
            PlainTextDesc oPlainDesc  = new PlainTextDesc( strFileExtn, PlainTextController );
            
            try {
                PgDocumentDescriptor oDocDesc = oPlainDesc;
                Program.BaseSlot     oNewSite = null;

                // Rank documents by priority. We can add a choose if more than on doc with pri > 0.
                foreach( IPgController2 oTryMe in Controllers ) {
                    PgDocumentDescriptor oTryDesc = oTryMe.Suitability( strFileExtn );
                    if( oTryDesc.CompareTo( oDocDesc ) > 0 ) {
                        oDocDesc = oTryDesc;
                    }
                }
                if( oDocDesc == oPlainDesc ) {
                    LogError( null, "host", oPlainDesc.Message );
                }

                switch( oDocDesc.StgReqmnt ) {
                    case var r when ( r == typeof( IPgLoad<TextReader> ) ):
                        oNewSite = new Program.TextSlot( this, oDocDesc.Controller, strFileExtn, iID);
                        break;
                    case var r when ( r == typeof( IPgLoadURL ) ):
                        oNewSite = new Program.DirBrowserSlot( this, oDocDesc.Controller, strFileExtn, iID);
                        break;
                    default:
                        LogError( null, "hosting", "Unable to create site for document." );
                        return  null;
                }

                try {
                    oDocSite = (IDocSlot)oNewSite; // If fails, we made an implementation mistake.
                } catch( InvalidCastException ) {
                    LogError( oNewSite, "hosting", "Unable to Convert site to abstract class." );
                    return null;
                }

                if( !oNewSite.CreateDocument() ) {
                    return null;
                }

                bool fBoot = false;
                try {
                    // TODO: This function trims the path. So we can't load directories only :-/
                    if( string.IsNullOrEmpty( Path.GetFileNameWithoutExtension( strFileName ) ) ) { 
                        fBoot = oNewSite.InitNew();
                    } else { 
                        fBoot = oNewSite.Load( strFileName );
                    }
                } catch( Exception oEx ) {
                    Type[] rgErrors = { typeof( NullReferenceException ),
                                        typeof( ArgumentException ),
                                        typeof( ArgumentNullException ) };
                    LogError( null, "hosting", "Error initializaing/loading guest : " + strFileName );
                    if( rgErrors.IsUnhandled( oEx ) ) {
                        throw;
                    }
                }

                if( !fBoot ) {
                    LogError( oNewSite, "hosting", "Unable to Initialize or Load new Document." );
					oNewSite.Dispose(); // Kill the guest/zombie the site. But sendit thru, to be bookmarked.
                }
            } catch( Exception oEx ) {
                // I should use ApplicationException instead of InvalidProgramException to signal app errors.
                Type[] rgErrors = { typeof( NullReferenceException ),
                                    typeof( ArgumentException ),
                                    typeof( ArgumentNullException ) };
                LogError( null, "hosting", "Error attempting to host : " + strFileName );
                if( rgErrors.IsUnhandled( oEx ) ) {
                    throw;
                }
                return null;
            }

            // Note: If the view doesn't get loaded we can't unload the site! But that's ok, 
			//       we want to save the reference in any .pvs file.
            _rgDocSites.Add( oDocSite );

            // Note: If the object has a problem with the load and doesn't store the Filename,
            //       we can get an empty string here... 
			if( !string.IsNullOrEmpty(oDocSite.FileName) ) {
				bool fFound = false;
                foreach( Line oLine in _oDoc_Recents ) {
                    if( oLine.Compare( oDocSite.FileName, fIgnoreCase:true ) == 0 ) {
                        fFound = true;
                        break;
                    }
                }
				if( !fFound  ) {
					_oDoc_Recents.LineInsert( oDocSite.FileName );
				}
			}

			_fSessionDirty = true;
			return( oDocSite );
        }

        public void SessionSave( bool fAtNew ) {
			try {
				//if( !WarnOnOpenDocuments() ) {
					_oDocSite_Session.Save(fAtNew);
				//}
			} catch( NullReferenceException ) {
				LogError( "mainwindow", "Session save site is null." );
			}
        }

 		/// <seealso cref="Load(TextReader)"/>
        public void SessionDirtySet() {
            _fSessionDirty = true;
            // TODO: Send event to views.
        }

        public bool SessionDirty {
            get { return( _fSessionDirty ); }
        }

        /// <summary>Grab whatever is on the clipboard and paste into scraps collection.</summary>
        /// <remarks>Used to use  ImageWalkerDoc.ClipboardCopyFrom() directly.</remarks>
        public void ScrapBookCopyClipboard() {
            try {
                ((IPgCommandBase)_oDocSlot_Scraps.Document).Execute( GlobalCommands.Paste );
            } catch( Exception oEx ) {
                Type[] rgErrors = { typeof( NullReferenceException ),
                                    typeof( InvalidCastException ) };
                if( rgErrors.IsUnhandled( oEx ) )
                    throw;

				LogError( "scraps", "Couldn't load into scrap from clipboard.");
            }
        }

        public IEnumerator<string> EnumFileExtensions() 
        {
            foreach( KeyValuePair<string,ExtensionMap> oPair in _rgExtensionMap ) {
                yield return( oPair.Key );
            }
        }

        /// <summary>
        /// Map a file extention to a language. Pops up UI on fail.
        /// </summary>
        /// <param name="strFileExtn"></param>
        /// <returns>Name of a grammer that corresponds to the file extension.</returns>
        public ExtensionMap GetMapping( string strFileExtn ) {
            ExtensionMap oMap = null;

            if( string.IsNullOrEmpty( strFileExtn ) ) {
                LogError( "internal", "map grammer, argument null." );
                oMap = new ExtensionMap(); // Empty entry.
            }

            if( !_rgExtensionMap.TryGetValue(  strFileExtn.ToLower(), out oMap ) ) {
                oMap = new ExtensionMap(); // Empty entry.
            }

            return( oMap );
        }

        /// <summary>
        /// The languages we support, and the extensions to map them
        /// to are preloaded. Here we look them up and return a
        /// language object that contains the ready to use grammer.
        /// </summary>
        /// <param name="strExtension"></param>
        /// <returns>Text grammer for an editor window</returns>
        /// <remarks>If we're part of a loop we'll annoy the user with
        /// a million error messages. It would be nicer to have them part
        /// of a general MainWindow error list. Maybe also add UI so that
        /// the user can map new languages.</remarks>
        public Grammer<char> GetGrammer( ExtensionMap oMap ) {
            LangSlot  oGrammarSite = null;

            if( oMap.IsEmpty ) {
                oGrammarSite = GetMappedGrammerSite( "text" );
            } else {
                oGrammarSite = GetMappedGrammerSite(oMap._strGrammar);
            }

            // Some business for "addon's" on a per map entry basis, or file extension. It is
            // for pre written parse event handlers over non "text" types. Since text types can just use built in services.
            //Assembly oAssembly = null;
            //if( _rgAddOns.TryGetValue( oMap.AssemblyID, out oAssembly ) ) {
            //    try {
            //        p_oController = (PgController)oAssembly.CreateInstance( oMap.Handler ); // Activator.
            //    } catch( Exception oEx ) {
            //        Type[] rgErrors = { typeof( MissingMethodException ),
            //                            typeof( InvalidCastException ) };

            //        if( !rgErrors.Contains( oEx.GetType() ) )
            //            throw new ApplicationException( "Controller is not compatible.", oEx );

            //        LogError( null, "hosting error", "Controller does not inherit from PgController or no public constructor found: " + oMap.AssemblyID + "," + oMap.Handler );
            //    }
            //}

            // If grammar is null, we'll depend on the GetMappedGrammerSite() call to actually have returned an error message.
            // So much can go wrong trying to find the proper grammer!
            if( oGrammarSite == null ) {
                return( null );
            }

            Grammer<char> oGrammar = oGrammarSite.Guest as Grammer<char>;
            if( oGrammar == null ) {
                LogError( "grammer", "The built in language tools only support 'char' streams." );
                return( null );
            }

            return( oGrammar );
        }

        public IEnumerator<ColorMap> GetSharedGrammarColors() {
            return( _rgGrammarColors.GetEnumerator() );
        }

        public int AddColor(string strName, string strValue) {
            for( int i=0; i<_rgGrammarColors.Count; ++i ) {
                if( strName == _rgGrammarColors[i]._strName ) {
                    return( i );
                }
            }
            _rgGrammarColors.Add( new ColorMap( strName, strValue ) );

            return( _rgGrammarColors.Count - 1 );
        }

        public int GetColorIndex(string strName)
        {
            for( int i=0; i<_rgGrammarColors.Count; ++i ) {
                if( strName == _rgGrammarColors[i]._strName ) {
                    return( i );
                }
            }

            return( 0 );
        }

        /// <summary>
        /// Look for a config file and load the first one if found. It gets read by other proc's later.
        /// </summary>
        private bool LoadConfigDoc( XmlDocument xmlConfig ) {
            string strAppDataDir = AppDataPath;

            string strConfigName = "config.phree";
            string strAppConfig  = strAppDataDir + "\\" + strConfigName;
            string strAppDefault = Path.GetDirectoryName( Application.ExecutablePath ) + "\\" + strConfigName;

            try {
                // Does our app config directory exist? If not, create it.
                if( !Directory.Exists( strAppDataDir ) ) {
                    Directory.CreateDirectory( strAppDataDir );
                }

                // Does our app config file exist? If not, create it in our appdata section.
                if( !File.Exists( strAppConfig ) ) {
                    Assembly oAssembly = Assembly.GetExecutingAssembly();
                    string   strResource = "Mjolnir.Content." + strConfigName;
                    using (Stream oStream = oAssembly.GetManifestResourceStream(strResource)) {
                        xmlConfig.Load( oStream );
                        //InitializePathsToGrammer( Path.GetDirectoryName( Application.ExecutablePath ), xmlDocument);
                        xmlConfig.Save( strAppConfig );
                    }
                } else {
                    xmlConfig.Load( strAppConfig );
                }
            } catch( Exception oEx ) {
                Type[] rgHandled = { typeof( FileNotFoundException ), 
                                     typeof( XmlException ),
                                     typeof( DirectoryNotFoundException ),
									 typeof( PathTooLongException ),
									 typeof( DirectoryNotFoundException ),
									 typeof( IOException ),
									 typeof( NotSupportedException ),
                                     typeof( NullReferenceException ),
                                     typeof( BadImageFormatException ) };

                LogError( "config", "Unable to read given config file" );

                if( rgHandled.IsUnhandled( oEx ))
                    throw;

                return( false );
            }

            return( true );
        }

        public bool WarnOnOpenDocuments() {
            StringBuilder sbDocs  = new StringBuilder();
            bool          fCancel = false;

            foreach( IDocSlot oDocSite in _rgDocSites ) {
                fCancel |= oDocSite.IsDirty; 
                if( oDocSite.IsDirty ) {
                    sbDocs.AppendLine( oDocSite.TitleShort );
                }
            }

            if(fCancel ) {
                MessageBox.Show( sbDocs.ToString(), "There are unsaved files!" );
            }

            return fCancel;
        }

        public void InitializeControllers() {
			Controllers.Add( new ControllerForParsedText( this ) );
            Controllers.Add( new ControllerForHtml( this ));

            // In the future I'll make these packages load on the fly and I can remove
            // hard dependencies to these assemblies!!
			Controllers.Add( new Play.ImageViewer  .ImageBrowserScrapsController() );
			Controllers.Add( new Play.ImageViewer  .ImageBrowserDirController() );
			Controllers.Add( new Play.MusicWalker  .MusicWalkerController() );
			Controllers.Add( new Play.MusicWalker  .M3uController() );
			Controllers.Add( new Play.MorsePractice.MorseController() );
        }

        /// <summary>
        /// Read in the grammars and the mappings to the file types.
        /// </summary>
        /// <param name="xmlDocument">XML DOM pointing to the configuration.</param>
        private void InitializeLanguages( XmlDocument xmlDocument ) {
            XmlElement  xmlGrammars;
            XmlNodeList lstGrammar;
			try {
				xmlGrammars = (XmlElement)xmlDocument.SelectSingleNode( "config/languages/grammars" );
				lstGrammar  = xmlDocument.SelectNodes("config/languages/grammars/grammar");
			} catch( XPathException ) {
				Type[] rgErrors = { typeof( XPathException ),
									typeof( InvalidCastException ) };
				LogError( "xmlconfig", "Unable to find grammar language data in xml" );
				return;
			}

            _strDefaultGrammarDir = xmlGrammars.GetAttribute( "defaultdirectory" );
            if( string.IsNullOrEmpty( _strDefaultGrammarDir ) )
                _strDefaultGrammarDir = string.Empty;

            foreach( XmlElement xeGrammar in lstGrammar ) {
                GrammerMap oGrammarMap = new GrammerMap();

                if( oGrammarMap.Load( xeGrammar ) ) {
                    // Note: This isn't the same as reading the BNF! We've only read the config file!!!
                    if( _rgGrammarMap.ContainsKey( oGrammarMap.Name ) ) {
                        LogError( "xmlconfig", "Configuration already contains this grammar: " + oGrammarMap.Name );
                    } else {
                        // Try using the default grammar directory if no path on filname.
                        if( !Path.IsPathRooted( oGrammarMap.FileName ) && !string.IsNullOrEmpty( _strDefaultGrammarDir ) ) {
                            // Try substute the %profile% if it's there.
                            string strDefPath = _strDefaultGrammarDir.Replace( "%profile%", UserProfile );
                            strDefPath = Path.Combine( strDefPath, Path.GetFileName( oGrammarMap.FileName ) );
                            oGrammarMap.FileName = strDefPath;
                        }
                        _rgGrammarMap.Add( oGrammarMap.Name, oGrammarMap );
                    }
                } else {
                    LogError( "xmlconfig", "Unable to load grammar map: " + oGrammarMap.Name );
                }
            }

			Assembly oAsm       = Assembly.GetExecutingAssembly();
			string   strAsmName = oAsm.GetName().Name;

            // BUG: Grammar is lazy read. If file is wrong, we lost our chance to fix it!!!
            // Here I Force read the one's I absolutely require.
            if( GetMappedGrammerSite( "text" ) == null ) {
                GrammerMap oMapText = new GrammerMap( "text", "text", strAsmName + ".Content.text2.bnf" );
                _rgGrammarMap.Remove( "text" );
                _rgGrammarMap.Add( oMapText.Name, oMapText );
            }
            if( GetMappedGrammerSite( "line_breaker" ) == null ) {
                GrammerMap oMapText = new GrammerMap( "line_breaker", "words", strAsmName + ".Content.linebreaker.bnf" );
                _rgGrammarMap.Remove( "line_breaker" );
                _rgGrammarMap.Add( oMapText.Name, oMapText );
            }

            XmlNodeList lstMaps;
			try {
				lstMaps = xmlDocument.SelectNodes("config/languages/maps/map");
			} catch( XPathException ) {
				LogError( "xmlconfig", "couldn't find file mappings in xmlconfig" );
				return;
			}

            foreach( XmlElement xeMap in lstMaps ) {
                ExtensionMap oMap = new ExtensionMap();
                if( oMap.Load( xeMap ) ) {
                    if( _rgExtensionMap.ContainsKey( oMap.Extension ) ) {
                        LogError( "xmlconfig", "Configuration already contains this file map: " + oMap.Extension );
                    } else {
                        // Check if the grammer it's mapped to, exists!!
                        if( !_rgGrammarMap.ContainsKey( oMap._strGrammar ) ) {
                            LogError( "xmlconfig", "Extension does not point to a valid grammar: " + oMap.Extension );
                        } else {
                            _rgExtensionMap.Add( oMap.Extension, oMap );
                        }
                    }
                } else {
                    LogError( "template", "A map entry is erroneous: '" + oMap.Extension + "'" );
                }
            }

            // If didn't load any extensions add a .txt one. We need this in a few places where I'm still looking for 
            // this extn hard coded. We create new files based on extension in general. The user can always rename the file
            // in a "save as" operation.
            if( _rgExtensionMap.Count == 0 ) {
                ExtensionMap oMap = new ExtensionMap( ".txt", "text" );
                _rgExtensionMap.Add( oMap.Extension, oMap );
            }
        }

        private GetMappedGrammerErrors GetMappedGrammerSite( string strLanguage, out LangSlot p_oLangSite ) 
        {
            p_oLangSite = null;

            // See if we've already loaded the grammer for the language in question.
            if( _rgLanguageSite.ContainsKey( strLanguage ) ) {
                p_oLangSite = _rgLanguageSite[strLanguage];
                return( GetMappedGrammerErrors.OK );
            } 

            if( _rgGrammarMap.Count == 0 )
                return( GetMappedGrammerErrors.NoGrammars );

            // First see if it's a language in the list of grammars we know anything about. If not we can't load it.
            if( !_rgGrammarMap.ContainsKey( strLanguage ) )
                return( GetMappedGrammerErrors.NotMappedLang );

            // Try to demand load the language.
            GrammerMap          oGrammarMap = _rgGrammarMap[ strLanguage ];
            IPgLoad<TextReader> oGrammar    = null;
            LangSlot            oSite       = new LangSlot( this, oGrammarMap ); 

            switch( oGrammarMap.StreamType ) {
                case "text" :
                    oGrammar = new TextGrammer( oSite );
                    break;
                case "tags" :
                    oGrammar = new TagGrammer( oSite );
                    break;
                case "words" :
                    oGrammar = new SimpleWordBreaker( oSite );
                    break;
            }

            if( oGrammar == null )
                return( GetMappedGrammerErrors.UnrecognizedStreamType );

            try {
                oSite.Guest = (IDisposable)oGrammar;
            } catch( Exception oEx ) {
                Type[] rgErrors = { typeof( InvalidCastException ),
                                    typeof( ArgumentException ),
                                    typeof( NullReferenceException )
                                  };
                if( rgErrors.IsUnhandled( oEx ))
                    throw;

                return( GetMappedGrammerErrors.NotLoadable );
            }

            if( !oSite.Load() ) {
                return( GetMappedGrammerErrors.NotLoadable );
            }

            _rgLanguageSite.Add(strLanguage, oSite );
            p_oLangSite = oSite;

            return( GetMappedGrammerErrors.OK );
        }

        public LangSlot GetMappedGrammerSite(string strLanguage )
        {
			GetMappedGrammerErrors eError = GetMappedGrammerSite(strLanguage, out LangSlot oSite);

			//if ( eError != GetMappedGrammerErrors.OK ) {
   //             LogGrammerError( eError, strLanguage );
   //         }

            return( oSite );
        }

        private void LogGrammerError(GetMappedGrammerErrors eLoadErrors, string strLanguage ) {
            StringBuilder sbError = new StringBuilder();

            switch (eLoadErrors)
            {
                case GetMappedGrammerErrors.NoGrammars:
                    sbError.Append( "Strange, there are no languages mapped for your text files? Check your language settings." );
                    break;
                case GetMappedGrammerErrors.NotMappedLang:
                    sbError.Append( "The file type does not have a language mapped to it? I'll attempt to open as text. Check your language settings." );
                    break;
                case GetMappedGrammerErrors.NotLoadable:
                    sbError.Append( "There was a problem loading the language information for this file type. The grammar might be invalid." );
                    break;
                case GetMappedGrammerErrors.UnrecognizedStreamType:
                    sbError.Append( "The stream type for the language of this file type is not one understood by this program. Check your language settings." );
                    break;
                case GetMappedGrammerErrors.NotMappedExtn:
                    sbError.Append( "The file type is not recognized." );
                    break;
                default:
                    sbError.Append( "Unrecognized error trying to retrieve a language." );
                    break;
            }

            if( eLoadErrors != GetMappedGrammerErrors.OK ) {
                LogError( "config", "File Extension Not Understood for language: " + strLanguage + " " + sbError.ToString() );
            }
        }

		/// <summary>
		/// Create a work place.
		/// </summary>
		/// <remarks>A workplace doesn't actually add to the burden of the round robin scheduler
		/// until work is actually queued up.</remarks>
        public IPgRoundRobinWork CreateWorkPlace() {
            return( new Program.RoundRobinWorkPlace(this) );
        }

		private void WorkerPlaceQue( IPgRoundRobinWork oWorker ) {
            if( !_rgWorkers.Contains( oWorker ) ) {
                _rgWorkers.Add( oWorker );
                _oTimer.Start();
            }
        }

		private void WorkerPlaceRemove( IPgRoundRobinWork oWorker ) {
			_rgWorkers.Remove( oWorker );
		}

		private static long TimeInMSElapsed( long lStartTick ) {
			return( ( DateTime.Now.Ticks - lStartTick ) / 10000 );
		}

		private void TimerStart() {
			if( _rgWorkers.Count > 0 ) {
				_oTimer.Start();
			}
		}
  
		private void OnTimer( Object state, EventArgs args ) {
            Queue<RoundRobinWorkPlace> rgWorkQue  = new Queue<RoundRobinWorkPlace>();
			long				       lStartTick = DateTime.Now.Ticks;
			int                        iWaiting   = 0;

			// Load up workers that are queued up. Workplaces that are instiated
			// but not queued up, don't waist any time!! Paused workers do however
			// have us checking the appointment.
            foreach( RoundRobinWorkPlace oSlot in _rgWorkers ) {
                if( oSlot.Appointment > 0 ) {
					if( oSlot.Appointment <= lStartTick ) {
						rgWorkQue.Enqueue( oSlot );
					}
					++iWaiting;
                }
            }

			// We can haved paused workers. Their appointment time is -1 (infinite wait)
            if( iWaiting == 0 )
                _oTimer.Stop();

			// This will be max amount of time a working task can wait before starving.
			uint uiWaitMaxInMs = 100;

			// The idea here is to give everyone some time. Then if any time is left
			// over they all get another go around. This way nobody get's starved for
			// time. But it also means we might overshoot the minimum wait time.
			long lElapsed =  TimeInMSElapsed( lStartTick );
            while( rgWorkQue.Count > 0 && TimeInMSElapsed( lStartTick ) < _oTimer.Interval ) {
				int iActiveWorkers = rgWorkQue.Count; // Got to save this since it's changing on the fly.
				while( iActiveWorkers-- > 0 ) {
					uint                uiWaitInMs = uint.MaxValue;
					RoundRobinWorkPlace oSite      = rgWorkQue.Dequeue();

					// Don't waist time on workers that are waiting for something to do.
					if( oSite.Appointment <= DateTime.Now.Ticks ) {
						if( !oSite.DoWork( ref uiWaitInMs ) ) {
							oSite.Stop(); // All Done! Clean up.
						} else {
							if( uiWaitInMs <= 0 ) {
								rgWorkQue.Enqueue( oSite ); // work can't wait!
							} else {
								if( uiWaitInMs < uiWaitMaxInMs )
									uiWaitMaxInMs = uiWaitInMs;
							}
						}
					}
				}
            }

            if( _rgWorkers.Count < 1 )
                _oTimer.Stop();
			else
				_oTimer.Interval = (int)uiWaitMaxInMs;
        }

		public IPgReader CreateSoundDecoder(string strFileName) {
			return _oMp3Factory.CreateFor( strFileName );
		}

        /// <exception cref="InvalidOperationException" />
		public IPgPlayer CreateSoundPlayer( Specification oSpec ) {
            try {
    			return new WmmPlayer( oSpec, -1);
            } catch (Exception oEx) {
                Type[] rgErrors = { typeof( ArgumentException ),
                                    typeof( ArgumentNullException ),
                                    typeof( BadDeviceIdException ),
                                    typeof( InvalidHandleException ),
                                    typeof( MMSystemException ),
                                    typeof( NullReferenceException ) };
                if (!rgErrors.Contains(oEx.GetType()))
                    throw;

                LogError("sound", "Couldn't handle sound spec");

                throw new InvalidOperationException("Can't handle sound request.", oEx);
            }
        }

        /// <summary>
        /// BUG: This is for the text editor. I suppose I could remove that cabability from
        /// it and then I could move the MorseGenerator into the MorsePractice project.
        /// </summary>
        /// <param name="oText"></param>
        /// <returns></returns>
		public IPgAnonymousWorker CreateMorseWorker(IEnumerable<char> oText) {
            MorseGenerator    oMorse  = new MorseGenerator(new SimpleSlot(this));
            IPgRoundRobinWork oWorker = CreateWorkPlace();

            oMorse.Signal = oText;

            oWorker.Queue(oMorse, 0);

            return oWorker;
		}

		/// <summary>
		/// Get a grammar from the system.
		/// </summary>
		/// <param name="strName">Name of grammer, not it's file extension.</param>
		/// <returns>a grammar object. You must cast it to your data type.</returns>
		public object GetGrammer(string strLanguage ) {
            LangSlot oSite = GetMappedGrammerSite( strLanguage );

            if( oSite != null)
                return( oSite.Guest );

            return( null );
		}
        /// <summary>
        /// 
        /// </summary>
        /// <param name="strFontFace">path to the font to use.</param>
        /// <returns>FaceID</returns>
        public UInt16 FaceCache( string strFontFace ) {
            return _oFTManager.FaceCache( strFontFace );
        }

        public uint FontCache( ushort uiFace, uint uiHeight, SKSize skResolution ) {
            return _oFTManager.FaceCacheSize( uiFace, uiHeight, skResolution );
        }

        public IPgFontRender FontRendererAt( uint uiRenderID ) {
            return _oFTManager.GetFontRenderer( uiRenderID );
        }

        public IPgFontRender FontStandardAt( string strName, SKSize skResolution ) {
            throw new NotImplementedException();
        }

        /// <summary>
        /// These are standardized colors for our user interface.
        /// </summary>
        public SKColor ColorsStandardAt( StdUIColors eColor ) {
            try {
                return _rgStdColors[ (int)eColor ];
            } catch( IndexOutOfRangeException ) {
                return SKColors.Black;
            }
        }

        public IReadOnlyList<SKColor> ColorsText {
            get { return _rgTxtColors; }
        }

        [Obsolete]private static UInt32 SetRGB(byte r, byte g, byte b) {
            return ( (UInt32)( r | ( (UInt16)g << 8 ) ) | ( ( (UInt32)b << 16 ) ) );
        }

        /// <summary>
        /// Return's the standard line color from the index.
        /// </summary>
        /// <remarks>This is only for combatibility with GDI32. We'll phase this out.</remarks>
        [Obsolete]public virtual UInt32 ColorStandardPacked( StdUIColors eColor ) {
            try {
                SKColor sColor = _rgStdColors[(int)eColor ];
                return( SetRGB( sColor.Red, sColor.Green, sColor.Blue ) );
            } catch( IndexOutOfRangeException ) {
                return( 0 );
            }
        }

	} // End class

	/// <summary>
	/// This is a worker enumerator that can be used to generate Morse code from a text file.
	/// Current, is the time in milliseconds until the next call must be made.
	/// </summary>
    /// <remarks>You might think this belongs in the "MorsePractice" module, but it's
    /// super overkill to load that entire module just to get this. So here it is. ^_^;;
    /// I might move it back, but I'll have to remove morse from the base editor first.</remarks>
	public class MorseGenerator : IEnumerator<int> {
		readonly IPgBaseSite   _oSiteBase;
		readonly IPgPlayer     _oPlayer;
		readonly GenerateMorse _oDecoder;

		uint _uiWait            = 0;
		bool _iRedundantDispose = false; // To detect redundant calls

		/// <exception cref="ArgumentNullException" />
		/// <exception cref="InvalidOperationException" >This can happen for a variety of reasons, check the inner exception.</exception>
		public MorseGenerator( IPgBaseSite oSiteBase ) {
			_oSiteBase = oSiteBase ?? throw new ArgumentNullException( "Need a site with hilight." );

			IPgSound      oSound = oSiteBase.Host.Services as IPgSound ?? throw new ArgumentNullException( "Host requires IPgSound." );
			Specification oSpec  = new Specification( 44100, 1, 0, 16 );

			_oPlayer  = oSound.CreateSoundPlayer( oSpec );
			_oDecoder = new GenerateMorse( oSpec, 800 );
		}

		public void Dispose() {
			if( _iRedundantDispose )
				return;

			if( _oPlayer != null )
				_oPlayer.Dispose();
			if( _oDecoder != null )
				_oDecoder.Dispose();

			_iRedundantDispose = true;
		}

		/// <summary>
		/// Assign some character enumerator here and we'll morse it!
		/// </summary>
		public IEnumerable<char> Signal {
			set {
				_oDecoder.Signal = value;
			}
		}

		public object Current => throw new NotImplementedException();
		public void   Reset() => throw new NotImplementedException();

		/// <summary>
		/// Returns the recommended time in milliseconds to "sleep", or do something else.
		/// </summary>
		int IEnumerator<int>.Current {
			get{ return (int)_uiWait; }
		}

		public bool MoveNext() {
			try {
				_uiWait = ( _oPlayer.Play( _oDecoder ) >> 1 ) + 1;
			} catch( NullReferenceException ) {
				_oSiteBase.LogError( "player","Problem with coder" );
			}

			return _oPlayer.Busy > 0;
		}
	}
} // End namespace