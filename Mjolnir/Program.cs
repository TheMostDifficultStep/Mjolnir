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
using System.Collections;

using SkiaSharp;

using Play.Interfaces.Embedding; 
using Play.Parse.Impl;
using Play.Edit;
using Play.Sound;
using Play.Integration;

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
        protected class Use :
            IEnumerable<string>,
            IPgLoad<XmlElement>
        {
            protected List<string> _rgExtensions = new List<string>();
            protected string       _strDescr;
            public Guid   GUID        { get; protected set; }
            public string Name        { get; protected set; }
            public string Description { get; protected set; }
            public IPgController2 Controller { get; set; }

            public IEnumerator<string> GetEnumerator() {
                return _rgExtensions.GetEnumerator();
            }

            public bool InitNew() {
                return false;
            }

            public bool Load(XmlElement oXml) {
                if( oXml == null) 
                    throw new ArgumentNullException();

                try {
                    if( oXml.SelectNodes( "e" ) is XmlNodeList rgUses ) {
                        foreach( XmlElement oNode in rgUses ) {
                            string strFileExtn = oNode.GetAttribute( "v" );

                            if( strFileExtn is null )
                                return false;

                            _rgExtensions.Add( strFileExtn );
                        }
                    }
                    Name = oXml.GetAttribute( "name" );

                    string strGuid = oXml.GetAttribute( "guid" );

                    if( string.IsNullOrEmpty(strGuid ) ) {
                        GUID = Guid.NewGuid();
                    } else {
                        GUID = new Guid( strGuid );
                    }

                    if( oXml.SelectSingleNode( "desc" ) is XmlElement oXmlDesc ) {
                        Description = oXml.InnerText;
                    }
                    if( string.IsNullOrEmpty( Description ) )
                        return false;
                } catch( Exception oEx ) {
                    Type[] rgErrors = { typeof( XPathException ),
                                        typeof( ArgumentNullException ),
                                        typeof( FormatException ),
                                        typeof( OverflowException ) };
                    if( rgErrors.IsUnhandled( oEx ) )
                        throw;

                    return false;
                }

                return true;
            }

            IEnumerator IEnumerable.GetEnumerator() {
                return GetEnumerator();
            }
        }

        protected class AssemblyStub :
            IEnumerable<Use>,
            IPgLoad<XmlElement>
        {
            public string FilePath { get; protected set; }
            public string FileName { get; protected set; }
            public string TypeName { get; protected set; }

            public    List<Use>          _rgUses = new();
            protected IControllerFactory _oFactory;

            public override string ToString() {
                return FileName;
            }

            public IEnumerator<Use> GetEnumerator() {
                return _rgUses.GetEnumerator();
            }

            IEnumerator IEnumerable.GetEnumerator() {
                return GetEnumerator();
            }
            public bool InitNew() {
                return false;
            }

            public bool Load( XmlElement oXml ) {
                if( oXml == null )
                    throw new ArgumentNullException();
                if( _rgUses.Count != 0 )
                    throw new InvalidProgramException();

                try {
                    FilePath = Path.GetDirectoryName( Assembly.GetExecutingAssembly().Location );
                    FileName = oXml.GetAttribute    ( "assm" );
                    TypeName = oXml.GetAttribute    ( "factory" );

                    if( string.IsNullOrEmpty( FileName ) )
                        return false;

                    foreach( XmlElement oNode in oXml.SelectNodes( "use" ) ) {
                        Use oUse = new Use();
                        if( !oUse.Load( oNode ) )
                            return false;
                        _rgUses.Add( oUse );
                    }
                } catch( Exception oEx ) {
                    Type[] rgErrors = { typeof( NotSupportedException ),
                                        typeof( XPathException ),
                                        typeof( PathTooLongException ),
                                        typeof( NullReferenceException ) };   
                    if( rgErrors.IsUnhandled( oEx ) )
                        throw;

                    return false;
                }


                return true;
            }
            /// <param name="sG">Guid for the use case controller we are looking for on this stub.</param>
            public IPgController2 GetController( Guid sG ) {
                if( _oFactory == null ) {
                    try {
                        Assembly oAsm         = Assembly.LoadFile( Path.Combine( FilePath, FileName ) );
                        Type     oFactoryType = oAsm    .GetType ( TypeName );

                        _oFactory = (IControllerFactory)Activator.CreateInstance( oFactoryType );
                    
                        foreach( Use oUse in _rgUses ) {
                            oUse.Controller = _oFactory.GetController( oUse.GUID );
                        }
                    } catch( Exception oEx ) {
                        Type[] rgErrors = { typeof( ArgumentException ),
                                            typeof( ArgumentNullException ),
                                            typeof( InvalidCastException ),
                                            typeof( ArgumentOutOfRangeException ),
                                            typeof( FileLoadException ),
                                            typeof( FileNotFoundException ),
                                            typeof( BadImageFormatException ),
                                            typeof( NotSupportedException ),
                                            typeof( TargetInvocationException ),
                                            typeof( MethodAccessException ), 
                                            typeof( MemberAccessException ),
                                            typeof( MissingMethodException ),
                                            typeof( NullReferenceException ),
                                            typeof( TypeLoadException )
                                             };
                        if( rgErrors.IsUnhandled( oEx ) ) 
                            throw;
                    }
                }
                // TODO: Technically it's ok to return a NULL controller for a particular
                // use. But I'd like to do a better job of reporting it. I should
                // probably give each stub a site to report back to...
                foreach( Use oUse in _rgUses ) {
                    if( sG == oUse.GUID ) {
                        return oUse.Controller;
                    }
                }
                throw new ArgumentOutOfRangeException( "Use ID not found" );
            }

        }

        public static Guid Clock       { get; } = new Guid( "{DEA39235-7E0A-4539-88A0-2FB775E7A8CC}" );
        public static Guid FindDialog  { get; } = new Guid( "{231E4D61-499A-427E-A1D3-EC4A579A5E6D}" );
        public static Guid ViewSelector{ get; } = new Guid( "{195E19DB-4BCE-4CAE-BE02-263536F00851}" );
        public static Guid MainWin     { get; } = new Guid( "{B091DED3-33C8-4BD1-8390-CA568CA7F9FC}" );

        string _strDefaultGrammarDir = String.Empty;

        List<IPgController2> Controllers { get; }  = new List<IPgController2>();

        readonly Dictionary<string, SKColor> _rgDefColors     = new Dictionary<string, SKColor>(StringComparer.OrdinalIgnoreCase);
        readonly SKColor[]                   _rgStdColors     = new SKColor[(int)StdUIColors.Max ];
        readonly List<ColorMap>              _rgGrammarColors = new List<ColorMap>();

        readonly Dictionary<string, ExtensionMap> _rgExtensionMap  = new Dictionary<string, ExtensionMap>();
        readonly Dictionary<string, LangSlot>     _rgLanguageSite  = new Dictionary<string, LangSlot>();    // Load on demand
        readonly Dictionary<string, GrammerMap>   _rgGrammarMap    = new Dictionary<string, GrammerMap>();  // Initialized on startup.

        protected bool _fSessionDirty = false;

        protected readonly List<IDocSlot> _rgDocSites = new List<IDocSlot>(); 
        int    _iDocCount = 0;    // Running count of files created for uniqueness. TODO deal with overflow.
        string _strLastPath;

		public MainWin MainWindow { get; private set; }

        public event UpdateAllTitlesFor EventUpdateTitles;

		readonly Mpg123Factory            _oMp3Factory = new Mpg123Factory();
        readonly Timer                    _oTimer      = new Timer();
        readonly List <IPgRoundRobinWork> _rgWorkers   = new List<IPgRoundRobinWork>();

        protected Editor      _oDoc_Alerts;
        protected Editor      _oDoc_Recents;
        public SearchResults Doc_Results { get; protected set; }

        // The textslots and xmlslots we could make cache the editor pointers on load
        // so we spot load errors sooner instead of later after the program boots.
        protected InternalSlot      _oDocSlot_Scraps;
        protected TextSlot          _oDocSlot_Alerts;
        protected XmlSlot           _oDocSlot_Recents;
        protected InternalSlot      _oDocSlot_Fonts;
        protected XmlSlot           _oDocSlot_SearchKey;
        protected ComplexXmlSlot    _oDocSlot_Find;
        protected InternalSlot      _oDocSlot_Clock;
        protected Program.TextSlot  _oDocSite_Session; // Hosting ourself, so don't be confused! ^_^;
          
        /// <summery>Views can use this to create views on the scrapbook</summery>
        public InternalSlot ScrapBookSlot => _oDocSlot_Scraps;
        public XmlSlot      RecentsSlot   => _oDocSlot_Recents;
        public TextSlot     SessionSlot   => _oDocSite_Session;
		public XmlSlot      SearchSlot    => _oDocSlot_SearchKey;
        public IDocSlot     FindSlot      => _oDocSlot_Find;
        public IDocSlot     AlertSlot     => _oDocSlot_Alerts;
        public InternalSlot ClockSlot     => _oDocSlot_Clock;
        public DirSlot      HomeDocSlot   { get; protected set; }

        // BUG: I'm dithering on FontMenu living on the program or just the main window.
		public Font         FontMenu      { get; } = new Font( "Segoe UI Symbol", 11 ); // Segoe UI Symbol, So we can show our play/pause stuff.
		public bool         IsDirty       => _fSessionDirty;

		protected Alerts _oWin_AlertsForm;

        //readonly Dictionary<string, Assembly> _rgAddOns = new Dictionary<string,Assembly>();
        readonly List<AssemblyStub> _rgAssemblyStubs = new ();

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
                    string strError = "Couldn't read global config.";
                    oProgram.TryLogXmlError( oEx, strError );
                    oProgram.BombOut( oEx, strError );
				    return;
                }
                // It's tempting to put the main window stuff in the initialize procedure, 
                // but I might separate the xmlConfig for the program and the window and
                // then I could set up the window on demand, based on the persistance data.
			    try {
				    oProgram.SessionLoad( rgArgs, xmlConfig );
                } catch ( Exception oEx ) {
                    string strError2 = "Couldn't configure Main window.";
                    if( oProgram.TryLogXmlError( oEx, strError2 ) ) {
 					    oProgram.LogError( "internal", oEx.Message );
                        oProgram.BombOut( oEx, strError2 );
                        return;
                    }
			    }

                oProgram.MainWindow.Parent = null;
                oProgram.MainWindow.Show();

				try {
					Application.Run( new MyApplicationContext( oProgram.MainWindow ) );
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
                _rgStdColors[ (int)StdUIColors.BGWithCursor]         = new SKColor( 225, 225, 225 ); // Text Unselected w/ cursor, para hilight. 
			    _rgStdColors[ (int)StdUIColors.BGSelectedFocus]      = new SKColor(  49, 106, 197 ); // Text Selected   w/ focus blue.
			    _rgStdColors[ (int)StdUIColors.BGSelectedBlur]       = new SKColor( 130, 130, 130 ); // Text Selected   w/o focus grey.
			    _rgStdColors[ (int)StdUIColors.BG]                   = new SKColor( 255, 255, 255 ); // Regular text bg. (was 244)
			    _rgStdColors[ (int)StdUIColors.BGReadOnly]           = new SKColor( 240, 240, 240 ); // Some kind of light grey.
			    _rgStdColors[ (int)StdUIColors.Text]                 = new SKColor(   0,   0,   0 ); // Black.
			    _rgStdColors[ (int)StdUIColors.TextSelected]         = new SKColor( 255, 255, 255 ); // White.
			    _rgStdColors[ (int)StdUIColors.MusicLine]            = new SKColor( 200, 255, 200 ); // Light green.
			    _rgStdColors[ (int)StdUIColors.MusicLinePaused]      = new SKColor( 255, 255, 150 ); // Light yellow.
                _rgStdColors[ (int)StdUIColors.TitleBoxBlur]         = new SKColor( 211, 211, 211 ); // Un focused title bar and grab border.
                _rgStdColors[ (int)StdUIColors.TitleBoxFocus]        = new SKColor( 112, 165, 234 ); // Focused title bar and grab border (blue)
                _rgStdColors[ (int)StdUIColors.BGNoEditText]         = new SKColor( 220, 220, 220 ); // Text area can be selected, but not edited.
                _rgStdColors[ (int)StdUIColors.BGSelectedLightFocus] = new SKColor( 207, 234, 255 ); // Focused img background.
            } catch( IndexOutOfRangeException ) {
                // BUG: I'd like to log it but nothing is ready to go at this point! I should probably make
                //      an array to hold REALLY EARLY errors like this and then spew 'em when we're we're able.
            }

            Doc_Results = new SearchResults( new TransientSlot( this ) );

            _oFTManager = new FTManager();
        }

		public void Dispose() {
			// BUG : I need to dispose any open workers.
			//foreach( IPgRoundRobinWork oWorker in _rgWorkers ) {
			//}

            // It has happened that we bail on startup and don't get the window created.
            if( MainWindow != null ) {
			    MainWindow.Dispose(); 
            }

			_oMp3Factory.Dispose();
			FontMenu    .Dispose();
		}

        public string AppDataPath => Environment.GetFolderPath( Environment.SpecialFolder.LocalApplicationData) + "\\pg\\mjolnir";
        public string UserProfile => Environment.ExpandEnvironmentVariables("%USERPROFILE%");
        public string UserDocs    => Environment.GetFolderPath(Environment.SpecialFolder.Personal);

		public IPgParent Parentage => null;
		public IPgParent Services  => this;
        public IPgParent TopWindow => null; // Maybe change to MainWindow so there's at least one?

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

        public void BombOut( Exception oOutGoingEx, string strText = "" ) {
            try {
                string strFile = Path.Combine(AppDataPath, "crashout.txt" );
                using ( Stream oStream = new FileStream( strFile, FileMode.Create, FileAccess.Write ) ) {
                    using( StreamWriter oWrite = new StreamWriter( oStream ) ) {
                        if( !string.IsNullOrEmpty( strText ) ) {
                            oWrite.WriteLine( strText );
                        }
                        if( oOutGoingEx != null ) {
                            oWrite.WriteLine( oOutGoingEx.StackTrace );
                        }
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
        /// This is just a dictionary of favorite color types. This is different than the
        /// GrammerColors which represent the colors in actual use. It is possible that
        /// there are custom colors in the grammar not described by the Default table.
        /// (Tho highly unlikely)
        /// </summary>
        protected void InitializeColorTable() {
            _rgDefColors.Add( "AliceBlue", new SKColor(4293982463u) );
            _rgDefColors.Add( "AntiqueWhite", new SKColor(4294634455u) );
            _rgDefColors.Add( "Aqua", new SKColor(4278255615u));
            _rgDefColors.Add( "Aquamarine", new SKColor(4286578644u) );
            _rgDefColors.Add( "Azure", new SKColor(4293984255u));
            _rgDefColors.Add( "Beige", new SKColor(4294309340u));
            _rgDefColors.Add( "Bisque", new SKColor(4294960324u));
            _rgDefColors.Add( "Black",  new SKColor(4278190080u));
            _rgDefColors.Add( "BlanchedAlmond",  new SKColor(4294962125u));
            _rgDefColors.Add( "Blue",  new SKColor(4278190335u));
            _rgDefColors.Add( "BlueViolet",  new SKColor(4287245282u));
            _rgDefColors.Add( "Brown", new SKColor(4289014314u));
            _rgDefColors.Add( "BurlyWood",  new SKColor(4292786311u));
            _rgDefColors.Add( "CadetBlue",  new SKColor(4284456608u));
            _rgDefColors.Add( "Chartreuse",  new SKColor(4286578432u));
            _rgDefColors.Add( "Chocolate", new SKColor(4291979550u));
            _rgDefColors.Add( "Coral", new SKColor(4294934352u));
            _rgDefColors.Add( "CornflowerBlue",  new SKColor(4284782061u));
            _rgDefColors.Add( "Cornsilk", new SKColor(4294965468u));
            _rgDefColors.Add( "Crimson",  new SKColor(4292613180u));
            _rgDefColors.Add( "Cyan",  new SKColor(4278255615u));
            _rgDefColors.Add( "DarkBlue",  new SKColor(4278190219u));
            _rgDefColors.Add( "DarkCyan",  new SKColor(4278225803u));
            _rgDefColors.Add( "DarkGoldenrod",  new SKColor(4290283019u));
            _rgDefColors.Add( "DarkGray",  new SKColor(4289309097u));
            _rgDefColors.Add( "DarkGreen",  new SKColor(4278215680u));
            _rgDefColors.Add( "DarkKhaki",  new SKColor(4290623339u));
            _rgDefColors.Add( "DarkMagenta",  new SKColor(4287299723u));
            _rgDefColors.Add( "DarkOliveGreen",  new SKColor(4283788079u));
            _rgDefColors.Add( "DarkOrange",  new SKColor(4294937600u));
            _rgDefColors.Add( "DarkOrchid",  new SKColor(4288230092u));
            _rgDefColors.Add( "DarkRed", new SKColor(4287299584u));
            _rgDefColors.Add( "DarkSalmon",  new SKColor(4293498490u));
            _rgDefColors.Add( "DarkSeaGreen",  new SKColor(4287609995u));
            _rgDefColors.Add( "DarkSlateBlue", new SKColor(4282924427u));
            _rgDefColors.Add( "DarkSlateGray", new SKColor(4281290575u));
            _rgDefColors.Add( "DarkTurquoise", new SKColor(4278243025u));
            _rgDefColors.Add( "DarkViolet", new SKColor(4287889619u));
            _rgDefColors.Add( "DeepPink", new SKColor(4294907027u));
            _rgDefColors.Add( "DeepSkyBlue", new SKColor(4278239231u));
            _rgDefColors.Add( "DimGray", new SKColor(4285098345u));
            _rgDefColors.Add( "DodgerBlue", new SKColor(4280193279u));
            _rgDefColors.Add( "Firebrick", new SKColor(4289864226u));
            _rgDefColors.Add( "FloralWhite", new SKColor(4294966000u));
            _rgDefColors.Add( "ForestGreen", new SKColor(4280453922u));
            _rgDefColors.Add( "Fuchsia", new SKColor(4294902015u));
            _rgDefColors.Add( "Gainsboro",  new SKColor(4292664540u));
            _rgDefColors.Add( "GhostWhite",  new SKColor(4294506751u));
            _rgDefColors.Add( "Gold", new SKColor(4294956800u));
            _rgDefColors.Add( "Goldenrod", new SKColor(4292519200u));
            _rgDefColors.Add( "Gray", new SKColor(4286611584u));
            _rgDefColors.Add( "Green", new SKColor(4278222848u));
            _rgDefColors.Add( "GreenYellow", new SKColor(4289593135u));
            _rgDefColors.Add( "Honeydew", new SKColor(4293984240u));
            _rgDefColors.Add( "HotPink", new SKColor(4294928820u));
            _rgDefColors.Add( "IndianRed", new SKColor(4291648604u));
            _rgDefColors.Add( "Indigo", new SKColor(4283105410u));
            _rgDefColors.Add( "Ivory", new SKColor(4294967280u));
            _rgDefColors.Add( "Khaki", new SKColor(4293977740u));
            _rgDefColors.Add( "Lavender", new SKColor(4293322490u));
            _rgDefColors.Add( "LavenderBlush", new SKColor(4294963445u));
            _rgDefColors.Add( "LawnGreen", new SKColor(4286381056u));
            _rgDefColors.Add( "LemonChiffon", new SKColor(4294965965u));
            _rgDefColors.Add( "LightBlue", new SKColor(4289583334u));
            _rgDefColors.Add( "LightCoral", new SKColor(4293951616u));
            _rgDefColors.Add( "LightCyan",  new SKColor(4292935679u));
            _rgDefColors.Add( "LightGoldenrodYellow",  new SKColor(4294638290u));
            _rgDefColors.Add( "LightGray",  new SKColor(4292072403u));
            _rgDefColors.Add( "LightGreen",  new SKColor(4287688336u));
            _rgDefColors.Add( "LightPink",  new SKColor(4294948545u));
            _rgDefColors.Add( "LightSalmon",  new SKColor(4294942842u));
            _rgDefColors.Add( "LightSeaGreen",  new SKColor(4280332970u));
            _rgDefColors.Add( "LightSkyBlue",  new SKColor(4287090426u));
            _rgDefColors.Add( "LightSlateGray",  new SKColor(4286023833u));
            _rgDefColors.Add( "LightSteelBlue",  new SKColor(4289774814u));
            _rgDefColors.Add( "LightYellow",  new SKColor(4294967264u));
            _rgDefColors.Add( "Lime",  new SKColor(4278255360u));
            _rgDefColors.Add( "LimeGreen",  new SKColor(4281519410u));
            _rgDefColors.Add( "Linen",  new SKColor(4294635750u));
            _rgDefColors.Add( "Magenta",  new SKColor(4294902015u));
            _rgDefColors.Add( "Maroon",  new SKColor(4286578688u));
            _rgDefColors.Add( "MediumAquamarine",  new SKColor(4284927402u));
            _rgDefColors.Add( "MediumBlue",  new SKColor(4278190285u));
            _rgDefColors.Add( "MediumOrchid",  new SKColor(4290401747u));
            _rgDefColors.Add( "MediumPurple", new SKColor(4287852763u));
            _rgDefColors.Add( "MediumSeaGreen",  new SKColor(4282168177u));
            _rgDefColors.Add( "MediumSlateBlue",  new SKColor(4286277870u));
            _rgDefColors.Add( "MediumSpringGreen",  new SKColor(4278254234u));
            _rgDefColors.Add( "MediumTurquoise",  new SKColor(4282962380u));
            _rgDefColors.Add( "MediumVioletRed",  new SKColor(4291237253u));
            _rgDefColors.Add( "MidnightBlue",  new SKColor(4279834992u));
            _rgDefColors.Add( "MintCream",  new SKColor(4294311930u));
            _rgDefColors.Add( "MistyRose",  new SKColor(4294960353u));
            _rgDefColors.Add( "Moccasin",  new SKColor(4294960309u));
            _rgDefColors.Add( "NavajoWhite",  new SKColor(4294958765u));
            _rgDefColors.Add( "Navy",  new SKColor(4278190208u));
            _rgDefColors.Add( "OldLace",  new SKColor(4294833638u));
            _rgDefColors.Add( "Olive",  new SKColor(4286611456u));
            _rgDefColors.Add( "OliveDrab",   new SKColor(4285238819u));
            _rgDefColors.Add( "Orange",   new SKColor(4294944000u));
            _rgDefColors.Add( "OrangeRed",   new SKColor(4294919424u));
            _rgDefColors.Add( "Orchid",  new SKColor(4292505814u));
            _rgDefColors.Add( "PaleGoldenrod",   new SKColor(4293847210u));
            _rgDefColors.Add( "PaleGreen",   new SKColor(4288215960u));
            _rgDefColors.Add( "PaleTurquoise",   new SKColor(4289720046u));
            _rgDefColors.Add( "PaleVioletRed",   new SKColor(4292571283u));
            _rgDefColors.Add( "PapayaWhip",  new SKColor(4294963157u));
            _rgDefColors.Add( "PeachPuff",   new SKColor(4294957753u));
            _rgDefColors.Add( "Peru",   new SKColor(4291659071u));
            _rgDefColors.Add( "Pink",   new SKColor(4294951115u));
            _rgDefColors.Add( "Plum",   new SKColor(4292714717u));
            _rgDefColors.Add( "PowderBlue",   new SKColor(4289781990u));
            _rgDefColors.Add( "Purple",   new SKColor(4286578816u));
            _rgDefColors.Add( "Red",  new SKColor(4294901760u));
            _rgDefColors.Add( "RosyBrown",   new SKColor(4290547599u));
            _rgDefColors.Add( "RoyalBlue",   new SKColor(4282477025u));
            _rgDefColors.Add( "SaddleBrown",   new SKColor(4287317267u));
            _rgDefColors.Add( "Salmon",   new SKColor(4294606962u));
            _rgDefColors.Add( "SandyBrown",   new SKColor(4294222944u));
            _rgDefColors.Add( "SeaGreen",   new SKColor(4281240407u));
            _rgDefColors.Add( "SeaShell",   new SKColor(4294964718u));
            _rgDefColors.Add( "Sienna",   new SKColor(4288696877u));
            _rgDefColors.Add( "Silver",   new SKColor(4290822336u));
            _rgDefColors.Add( "SkyBlue",   new SKColor(4287090411u));
            _rgDefColors.Add( "SlateBlue",   new SKColor(4285160141u));
            _rgDefColors.Add( "SlateGray",   new SKColor(4285563024u));
            _rgDefColors.Add( "Snow",   new SKColor(4294966010u));
            _rgDefColors.Add( "SpringGreen",   new SKColor(4278255487u));
            _rgDefColors.Add( "SteelBlue",   new SKColor(4282811060u));
            _rgDefColors.Add( "Tan",   new SKColor(4291998860u));
            _rgDefColors.Add( "Teal",   new SKColor(4278222976u));
            _rgDefColors.Add( "Thistle",   new SKColor(4292394968u));
            _rgDefColors.Add( "Tomato",   new SKColor(4294927175u));
            _rgDefColors.Add( "Turquoise",   new SKColor(4282441936u));
            _rgDefColors.Add( "Violet",   new SKColor(4293821166u));
            _rgDefColors.Add( "Wheat",   new SKColor(4294303411u));
            _rgDefColors.Add( "White",   new SKColor(uint.MaxValue));
            _rgDefColors.Add( "WhiteSmoke",   new SKColor(4294309365u));
            _rgDefColors.Add( "Yellow",   new SKColor(4294967040u));
            _rgDefColors.Add( "YellowGreen",   new SKColor(4288335154u));
            _rgDefColors.Add( "Transparent",   new SKColor(16777215u));
            _rgDefColors.Add( "Empty",   new SKColor(0u));
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
        /// <seealso cref="InitializeControllers"/>
        /// <seealso cref="GetController"/>
        protected void InitializePlugins( XmlDocument xmlDoc ) {
            XmlNodeList lstTypes = xmlDoc.SelectNodes("config/addons/add");

            foreach( XmlElement xeType in lstTypes ) {
                try {
                    AssemblyStub oEntry = new AssemblyStub();

                    if( oEntry.Load( xeType ) ) {
                        _rgAssemblyStubs.Add( oEntry );
                    } else {
                        throw new FileLoadException();
                    }
                } catch( Exception oEx ) {
                    Type[] rgErrors = { typeof( FileLoadException ), 
                                        typeof( FileNotFoundException ), 
                                        typeof( BadImageFormatException ),
                                        typeof( ArgumentException ) };
                    if( rgErrors.IsUnhandled( oEx ) )
                        throw;

                    LogError( null, "internal", "Can't load plugin: " + xeType.OuterXml );
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
            {
                //PgDocDescr oDescr = GetController( ".txt" );
                PgDocDescr oDescr = PlainTextController.Suitability( ".txt" );
                if( oDescr.StgReqmnt != typeof( IPgLoad<TextReader> ) )
                    throw new InvalidProgramException();

                // TODO: Maybe we can tack a parser on AFTER the grammars have
                // successfully loaded. So then we get all the parser features!!
                _oDocSlot_Alerts = new InternalSlot(this, oDescr, "Alerts");
                _oDocSlot_Alerts.CreateDocument();
                _oDocSlot_Alerts.InitNew();
                _oDoc_Alerts = (Editor)_oDocSlot_Alerts.Document;
            }

            // BUG: If this fails, we're borked. Need to make us more resilient to failing to load config.
            if ( !LoadConfigDoc( xmlConfig ) )
                throw new ApplicationException( "Couldn't load configuration" );

            {
                PgDocDescr oDescr = PlainTextController.Suitability( ".txt" );
                if( oDescr.StgReqmnt != typeof( IPgLoad<TextReader> ) )
                    throw new InvalidProgramException();

                // Store our cached fonts so we can look 'em up quickly.
                _oDocSlot_Fonts = new InternalSlot(this, oDescr, "Fonts");
                _oDocSlot_Fonts.CreateDocument();
            }
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

            InitializeColorTable();
            InitializeLanguages( xmlConfig );

            if( _rgGrammarColors.Count <= 0 ) {
                LogError( "Grammars", "No text colors. Adding a default Black" );
                // TODO: See the program constructor for the shell colors. Black might not
                // work if we have a std bg color of black instead of the usual white.
                _rgGrammarColors.Add( new ColorMap( "black", "black", SKColors.Black ) );
            }

            InitializePlugins    ( xmlConfig );
            InitializeControllers();

            {
                PgDocDescr oDescr = GetController( ".txt" );
                if( oDescr.StgReqmnt != typeof( IPgLoad<TextReader> ) )
                    throw new InvalidProgramException();

                _oDocSlot_Recents = new XmlSlot(this, oDescr, "Recent" );
                _oDocSlot_Recents.CreateDocument();
                _oDocSlot_Recents.InitNew();
                _oDoc_Recents = (Editor)_oDocSlot_Recents.Document;
            }

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

            {
                PgDocDescr oDescr = GetController( ".scraps" );
                if( oDescr.StgReqmnt != typeof( IPgLoad<TextReader> ) )
                    throw new InvalidProgramException();

			    _oDocSlot_Scraps = new InternalSlot( this, oDescr, "Scraps");
                _oDocSlot_Scraps.CreateDocument();
			    _oDocSlot_Scraps.InitNew();
            }

            {
 			    // BUG: it's part of the window session load/init sequence. And the MainWin is trying
			    // to get at the parse handler in it's constructor. So we've got to InitNew/Load before
			    // that. So I'll InitNew() now and let load get called subsequently...for now. ^_^;;
                PgDocDescr oDescr = GetController( ".search" );
                if( oDescr.StgReqmnt != typeof( IPgLoad<TextReader> ) )
                    throw new InvalidProgramException();

			    _oDocSlot_SearchKey = new XmlSlot( this, oDescr, "Find String" );
                _oDocSlot_SearchKey.CreateDocument();
			    _oDocSlot_SearchKey.InitNew(); 
                if( _oDocSlot_SearchKey.Document is Editor oEdit ) {
                    oEdit.LineInsert( string.Empty );
                }
            }

            {
                IPgController2 oTopLevelController = new ControllerForTopLevelWindows( this );
                PgDocDescr oDocDesc = oTopLevelController.Suitability( ".finddialog" );
                // We'll move the search key into the complexxmlslot's doc eventually.
                _oDocSlot_Find = new ComplexXmlSlot( this, oDocDesc, "Find Dialog" );
                _oDocSlot_Find.CreateDocument();
                _oDocSlot_Find.InitNew();

                oDocDesc = oTopLevelController.Suitability( ".clock" );
                _oDocSlot_Clock = new InternalSlot( this, oDocDesc, "Clock" );
                _oDocSlot_Clock.CreateDocument();
                _oDocSlot_Clock.InitNew();
            }
            {
                PgDocDescr oDescr = GetController( ".fileman" );
                HomeDocSlot = new DirSlot( this, oDescr.Controller, oDescr.FileExtn );
                HomeDocSlot.CreateDocument();
                HomeDocSlot.InitNew();
            }
        }

        /// <summary>
        /// Basically all the errors that can happen while we're trying to load
        /// up or configuration xml file and our session xlm file.
        /// </summary>
        /// <returns>True if recommend exit.</returns>
		bool TryLogXmlError(Exception oEx, string strMessage)
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
                                typeof( ApplicationException ),
                                typeof( ArgumentOutOfRangeException ) };
            if (rgErrors.IsUnhandled(oEx))
               return true;

            this.LogError("program session", strMessage);
            return false;
        }

        /// <summary>
        /// Self hosting ourselves! Look for the first session in the command line. 
        /// If found, Load() from it. Else, InitNew(). Here "load" is a misnomer because
        /// there is no "SessionInit" so be aware.
        /// </summary>
        protected void SessionLoad( string[] rgArgs, XmlDocument xmlConfig ) {
			List<string> rgArgsClean = new List<string>(5);
			int          iPvs        = -1;

			Type[] rgErrors = { typeof( NullReferenceException ),
								typeof( ArgumentException ),
								typeof( ArgumentNullException ),
								typeof( InvalidOperationException ),
								typeof( IndexOutOfRangeException ),
                                typeof( ArgumentOutOfRangeException ) };

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
				if( rgErrors.IsUnhandled( oEx ) )
					throw;
			}

            // TODO: This isn't the greatest controller for the session. :-/
            // But the guest is overridden with "this" so the Create doc/view
            // stuff won't be activated. Let's make a special controller later...
            PgDocDescr oDescr = PlainTextController.Suitability( ".txt" );
            // MainWindow references this, so got to initialize it first.
			_oDocSite_Session = new Program.SessonSlot( this, oDescr );

            // BUG: In the future, we'll move this into the program initnew/load.
            //      Even better. At least show a window with the error.
            try {
                MainWindow = new MainWin(this);
                MainWindow.Initialize(xmlConfig);
            } catch( Exception oEx ) {
				if( rgErrors.IsUnhandled( oEx ) )
					throw;

                LogError( "Session", "Session Mainwindow Init Error" );
            }

            try {
				if( iPvs < 0 ) {
                    // This case is a little weird. Since InitNew has no params
                    // we just create doc/view pairs outside and then init
                    // the program (which then init's the main window). Doesn't
                    // seem to blow up and simplifes code path. Go with it for now.
                    MainWindow.DocumentShowAll(rgArgsClean);

					_oDocSite_Session.InitNew();
				} else {
                    // Loads the documents AND the saved views/positions in main window.
					_oDocSite_Session.Load( rgArgs[iPvs] );
				}
			} catch( Exception oEx ) {
				if( rgErrors.IsUnhandled( oEx ) )
					throw;

                LogError( "Session", "Session Init/Load error" );
			}
        }

        public bool InitNew() {
			if( !MainWindow.InitNew() ) {
                LogError( "program initnew", "Couldn't initialize main window." );
				return false;
			}

            // Forms act different than a document. They can't be empty.
            if( SearchSlot.Document is Editor oSearchKey ) {
                oSearchKey.LineAppend( string.Empty, fUndoable:false ); 
            }

            if( _rgDocSites.Count == 0 ) {
                MainWindow.ViewCreate( HomeDocSlot, Guid.Empty );
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
                if( rgSessionDocs != null ) {
				    foreach( XmlElement xmlDoc in rgSessionDocs ) {
					    if (string.IsNullOrEmpty(xmlDoc.InnerText) )
						    throw new InvalidOperationException( "filename innertext failure");

					    int    iDocID  = int.Parse(xmlDoc.GetAttribute("docid") );
                        string strExtn = xmlDoc.GetAttribute( "extn" );
                        string strPath = xmlDoc.InnerText;

                        // If path only then try adding the persisted extn
                        // so the system can figure out what document to load.
                        if( string.IsNullOrEmpty( Path.GetExtension(strPath) ) ) {
                            strPath += strExtn;
                        }

                        // BUG: Need to add some code to validate Doc ID.
                        IDocSlot oDocSite = DocumentCreate( strPath, iDocID );

					    // If oDocSite is null, I should create a place holder document that you can use
					    // to edit the link or try again some time.
					    if( oDocSite == null )
						    throw new InvalidOperationException( "Couldn't create document" );
				    }
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

            // Forms are a little different than documents. Need to flesh that out.
            if( SearchSlot.Document is Editor oSearchKey &&
                oSearchKey.ElementCount < 1 ) 
            {
                oSearchKey.LineAppend( string.Empty, fUndoable:false );
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
                if( !string.IsNullOrEmpty( oDocSite.FilePath ) ) {
                    XmlElement xmlFile = xmlRoot.CreateElement( "FileName" );

					xmlFile.SetAttribute( "docid", oDocSite.ID.ToString() );
                    xmlFile.InnerText = oDocSite.FilePath;

                    // ImageViewer and FileManager are path only but...
                    if( oDocSite is DirSlot oDirSite ) {
                        xmlFile.SetAttribute( "extn", oDirSite._strFileExt );
                    }

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
            if( !string.IsNullOrEmpty( _oDocSite_Session.FilePath ) && _oDocSite_Session.IsDirty ) {
                _oDocSite_Session.Save(false);
            }
        }

        public bool DocumentsClean( IDocSlot oDocSite ) {
            // Check if we want to remove the document. Documents like the built in Find-Results and Messages
            // will have a reference from us blocking the document removal.
            if( _rgDocSites.Contains( oDocSite ) && oDocSite.Reference <= 0 ) {
                _rgDocSites.Remove( oDocSite );
                // This will close any associated tool windows. We can't get here if the tool window
                // view holds a reference on the docsite.
                oDocSite.Dispose();
                return true;
            }
            return false;
        }

        /// <summary>
        /// Try to find the best controller for the given file extention. If there's a
        /// duplicate I'm going to return an error. 
        /// </summary>
        /// <param name="strFileExtn"></param>
        /// <remarks>I'll make a version of this which uses GUID's to find the controller.
        /// So we can avoid possible overlaps of file extensions.</remarks>
        /// <seealso cref="InitializePlugins">
		public PgDocDescr GetController( string strFileExtn, bool fSendMessage = false ) {
            PgDocDescr oPlainDesc  = PlainTextController.Suitability( strFileExtn );
            PgDocDescr oDocDesc    = oPlainDesc;
            try {
                #if true
                string     strExtnChk;

                if( strFileExtn.Length > 0 && strFileExtn[0] == '.' ) {
                    strExtnChk = strFileExtn.Substring( 1 );
                } else {
                    strExtnChk = strFileExtn;
                }

                IPgController2 oController = null;

                foreach( AssemblyStub oStub in _rgAssemblyStubs ) {
                    foreach( Use oUse in oStub ) {
                        foreach( string strExtn in oUse ) {
                            if( string.Compare( strExtn, strExtnChk, ignoreCase: true ) == 0 ) {
                                oController = oStub.GetController( oUse.GUID );
                            }
                        }
                    }
                }
                if( oController != null ) {
                    PgDocDescr oTryDesc = oController.Suitability( strFileExtn );
                    if( oTryDesc.CompareTo( oDocDesc ) >= 0 )
                        return oTryDesc;
                }
                #endif

                foreach( IPgController2 oTryMe in Controllers ) {
                    PgDocDescr oTryDesc = oTryMe.Suitability( strFileExtn );
                    if( oTryDesc.CompareTo( oDocDesc ) >= 0 ) {
                        oDocDesc = oTryDesc;
                    }
                }

                if( oDocDesc == oPlainDesc && fSendMessage ) {
                    LogError( "Controllers", "No controller match, trying Plain Text" );
                }
            } catch( Exception oEx ) {
                Type[] rgErrors = { typeof( NullReferenceException ),
                                    typeof( ArgumentOutOfRangeException ),
                                    typeof( IndexOutOfRangeException ) };
                if( rgErrors.IsUnhandled( oEx ) )
                    throw;

                LogError( "Controllers", "A controller returned a null Suitability object. Going with best match so far." );
            }

            return oDocDesc;
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

            IDocSlot   oDocSite;
            string     strFileExtn = Path.GetExtension(strFileName).ToLower();
            
            try {
                PgDocDescr       oDocDesc = GetController( strFileExtn, fSendMessage:true );
                Program.BaseSlot oNewSite = null;

                switch( oDocDesc.StgReqmnt ) {
                    case var r when ( r == typeof( IPgLoad<TextReader> ) ):
                        oNewSite = new Program.TextSlot( this, oDocDesc.Controller, strFileExtn, iID);
                        break;
                    case var r when ( r == typeof( IPgLoadURL ) ):
                        oNewSite = new Program.DirSlot( this, oDocDesc.Controller, strFileExtn, iID);
                        break;
                    case var r when ( r == typeof( IPgLoad<BinaryReader> ) ):
                        oNewSite = new Program.BinarySlot( this, oDocDesc.Controller, strFileExtn, iID);
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
			if( !string.IsNullOrEmpty(oDocSite.FilePath) ) {
				bool fFound = false;
                foreach( Line oLine in _oDoc_Recents ) {
                    if( oLine.Compare( oDocSite.FilePath, IgnoreCase:true ) == 0 ) {
                        fFound = true;
                        break;
                    }
                }
				if( !fFound  ) {
					_oDoc_Recents.LineInsert( oDocSite.FilePath );
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

        public IEnumerable<string> FileExtnList => new FileExtCollection( this );

        public struct FileExtCollection : IEnumerable<string> {
            private readonly Program _oHost;
            public FileExtCollection( Program oHost ) {
                _oHost = oHost ?? throw new ArgumentNullException();
            }
            public IEnumerator<string> GetEnumerator() {
                foreach( KeyValuePair<string,ExtensionMap> oPair in _oHost._rgExtensionMap ) {
                    yield return( oPair.Key );
                }
            }

            IEnumerator IEnumerable.GetEnumerator() {
                return GetEnumerator();
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
        /// This will be the new system that throws exceptions.
        /// </summary>
        /// <exception cref="ArgumentException"></exception>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public ExtensionMap GetMappingEx( string strFileExtn ) {
            if( string.IsNullOrEmpty( strFileExtn ) ) {
                LogError( "internal", "map grammer, argument null." );
                throw new ArgumentException();
            }
            if( !_rgExtensionMap.TryGetValue(  strFileExtn.ToLower(), out ExtensionMap oMap ) ) {
                throw new ArgumentOutOfRangeException();
            }

            return oMap;
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
            try {
                if( oMap.IsEmpty ) {
                    return (Grammer<char>)GetMappedGrammerSiteNew( "text" ).Guest;
                }

                return (Grammer<char>)GetMappedGrammerSiteNew( oMap._strGrammar ).Guest;
            } catch( Exception oEx ) {
                Type[] rgErrors = { typeof( ArgumentException ),
                                    typeof( InvalidCastException ) };
                if( rgErrors.IsUnhandled( oEx ) )
                    throw;

                LogError( "grammer", "The built in language tools only support 'char' streams." );
                return null;
            }
        }

        public IEnumerator<ColorMap> GetSharedGrammarColors() {
            return _rgGrammarColors.GetEnumerator();
        }

        /// <summary>
        /// This adds a named color, typically from a grammar into the shared
        /// color table space. Get the RGB value from the programs build in
        /// default color names. I'd like to move away from this model, since
        /// I never made a true grammar merging functionality.
        /// </summary>
        /// <param name="strName">This is the functional use of the color.</param>
        /// <param name="strValue">This is the human readable name of the color.</param>
        /// <seealso cref="GrammarTextColor"/>
        public int AddColor(string strName, string strValue) {
            for( int i=0; i<_rgGrammarColors.Count; ++i ) {
                if( strName == _rgGrammarColors[i]._strName ) {
                    return i;
                }
            }
            if( !_rgDefColors.TryGetValue( strValue, out SKColor sNewColor ) ) {
                sNewColor = SKColors.Black;
            }

            _rgGrammarColors.Add( new ColorMap( strName, strValue, sNewColor ) );

            return _rgGrammarColors.Count - 1;
        }

        /// <summary>
        /// Look up the color by it's usage and not it's color name value
        /// For example "functioncall, red". Might be duplicates and so it's
        /// first come first served. I'll look into fixing that later. BUG 7/10/2023
        /// </summary>
        public int GetColorIndex(string strUsage) {
            for( int i=0; i<_rgGrammarColors.Count; ++i ) {
                if( string.Compare( strUsage, 
                                    _rgGrammarColors[i]._strName,
                                    ignoreCase:true ) == 0 ) {
                    return i;
                }
            }

            return 0;
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
                    sbDocs.AppendLine( oDocSite.Title );
                }
            }

            if(fCancel ) {
                MessageBox.Show( sbDocs.ToString(), "There are unsaved files!" );
            }

            return fCancel;
        }
        
        /// <summary>
        /// Loads up all the document controllers that we can possible use. This is
        /// a bit of a drag since it'll load assemblies that we don't really need.
        /// </summary>
        /// <seealso cref="InitializePlugins"/>
        public void InitializeControllers() {
            Controllers.Add( new ControllerForResults   () );
			Controllers.Add( new ControllerForParsedText( this ) );
            Controllers.Add( new ControllerForHtml      ( this ));
            Controllers.Add( new ControllerForSearch    () );
            Controllers.Add( new Play.Clock        .SolarController() );
            Controllers.Add( new Play.FileManager  .FileManController() );

            // We still have a project dependency for these items but only so
            // the dll's will get loaded into the debug directory for testing.
            // Elsewise we could even remove the project dependencies.
			//Controllers.Add( new Play.ImageViewer  .ImageBrowserScrapsController() );
			//Controllers.Add( new Play.ImageViewer  .ImageBrowserDirController() );
			//Controllers.Add( new Play.MusicWalker  .MusicWalkerController() );
			//Controllers.Add( new Play.MusicWalker  .M3uController() );
            //Controllers.Add( new Play.MusicWalker  .MP3Controller() );
			//Controllers.Add( new Play.MorsePractice.MorseController2() );
			//Controllers.Add( new Play.MorsePractice.MorseController3() );
            //Controllers.Add( new Play.MorsePractice.MorseController4() );
            //Controllers.Add( new Play.SSTV         .MySSTVController() );
            //Controllers.Add( new Monitor           .NewMonitorController() );
            //Controllers.Add( new Monitor           .BBCBasicBinaryController() );
            //Controllers.Add( new Monitor           .BBCBasicTextController() );
            //Controllers.Add( new Kanji_Practice    .KanjiController() );
            //Controllers.Add( new AddressBook       .Controller() );
            //Controllers.Add( new Scanner           .ScannerController() );
        }

        protected class EmbeddedGrammars {
            public EmbeddedGrammars( string strName, string strPlace ) {
                _strName  = strName;
                _strPlace = strPlace;
            }
            public readonly string _strName;
            public readonly string _strPlace;
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

            List<EmbeddedGrammars> rgPreLoad = new();

            // These are text "type" grammars. line_breaker (word "type") is not needed at present.
            rgPreLoad.Add( new EmbeddedGrammars( "text",      "text2.bnf"     ) );
            rgPreLoad.Add( new EmbeddedGrammars( "directory", "directory.bnf" ) );
            rgPreLoad.Add( new EmbeddedGrammars( "m3u",       "m3u.bnf"       ) );

            // Force read the grammars I'd really like. BUG: Add a flag to quiet the warnings...
            foreach( EmbeddedGrammars oEmbed in rgPreLoad ) {
                if( GetMappedGrammerSite( oEmbed._strName ) == null ) {
                    LogError( "Grammars", "Trying embedded : " + oEmbed._strPlace );
                    GrammerMap oMapText = new GrammerMap( oEmbed._strName, "text", strAsmName + ".Content." + oEmbed._strPlace );
                    _rgGrammarMap.Remove( oEmbed._strName );
                    _rgGrammarMap.Add( oMapText.Name, oMapText );
                    // Do this so the grammar actually loads from it's bnf file.
                    if( GetMappedGrammerSite( oEmbed._strName ) == null )
                        LogError( "Grammars", "Couldn't load internal text grammar." );
                    else
                        LogError( "Grammars", "Successful load of embedded grammar : " + oEmbed._strPlace );
                }
            }
            // I'd have to do more work for this one but we don't need the line breaker anymore.
            //if( GetMappedGrammerSite( "line_breaker" ) == null ) {
            //    GrammerMap oMapText = new GrammerMap( "line_breaker", "words", strAsmName + ".Content.linebreaker.bnf" );
            //    _rgGrammarMap.Remove( "line_breaker" );
            //    _rgGrammarMap.Add( oMapText.Name, oMapText );
            //}

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

        private LangSlot GetMappedGrammerSiteNew( string strLanguage ) 
        {
            // See if we've already loaded the grammer for the language in question.
            if( _rgLanguageSite.ContainsKey( strLanguage ) ) {
                return _rgLanguageSite[strLanguage];
            } 

            if( _rgGrammarMap.Count == 0 )
                throw new ArgumentException( "No Grammars maps loaded." );

            // First see if it's a language in the list of grammars we know anything about. If not we can't load it.
            if( !_rgGrammarMap.ContainsKey( strLanguage ) )
                throw new ArgumentException( "Language Not Mapped." );

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
                throw new ArgumentException( "Unrecognized Grammer Stream Type." );

            try {
                oSite.Guest = (IDisposable)oGrammar;
            } catch( Exception oEx ) {
                Type[] rgErrors = { typeof( InvalidCastException ),
                                    typeof( ArgumentException ),
                                    typeof( NullReferenceException )
                                  };
                if( rgErrors.IsUnhandled( oEx ))
                    throw;

                throw new ArgumentException( "Grammar Missing Proper Interface." );
            }

            if( !oSite.Load() ) {
                throw new ArgumentException( "Could not read persistance file." );
            }

            _rgLanguageSite.Add(strLanguage, oSite );

            return oSite;
        }

        public LangSlot GetMappedGrammerSite( string strLanguage ) {
			try {
                return GetMappedGrammerSiteNew(strLanguage);
            } catch( ArgumentException e ) {
                LogError( "grammar", e.Message );

                return null;
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
            //return new Mpg123FFTSupport( strFileName );
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
            try {
                return GetMappedGrammerSiteNew( strLanguage ).Guest;
            } catch (Exception oEx ) {
                Type[] rgErrors = { typeof( ArgumentException ),
                                    typeof( NullReferenceException ) };
                if (rgErrors.IsUnhandled( oEx ))
                    throw;

                LogError( "Grammer", "Couldn't Find Grammer Requested." );
            }

            return null;
		}

        public object GetGrammerByExtn( string strExtn ) {
            try {
                return GetGrammer( GetMappingEx( strExtn )._strGrammar );
            } catch( Exception oEx) {
                Type[] rgErrors = { typeof( ArgumentException ),
                                    typeof( NullReferenceException ) };
                if (rgErrors.IsUnhandled( oEx ))
                    throw;

                LogError( "Grammer", "Couldn't Find Grammer Requested by Extension." );
            }

            return null;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="strFontFace">path to the font to use.</param>
        /// <returns>FaceID</returns>
        public UInt16 FaceCache( string strFontFace ) {
            return _oFTManager.FaceCache( strFontFace );
        }

        public uint FontCache( ushort uiFace, uint uiHeightInPoints, SKPoint skResolution ) {
            return _oFTManager.FaceCacheSize( uiFace, uiHeightInPoints, skResolution );
        }

        public IPgFontRender FontRendererAt( uint uiRenderID ) {
            return _oFTManager.GetFontRenderer( uiRenderID );
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

        /// <summary>
        /// Return the color from index to the merged grammar color table.
        /// </summary>
        /// <remarks>It would be nice if the colors weren't all shared
        /// amoung all the grammars UNLESS NECESSARY. Different grammers might describe
        /// the same function using different colors and then it's whoever
        /// added it first wins.</remarks>
        /// <seealso cref="AddColor"/>
        public SKColor GrammarTextColor( int i ) {
            return _rgGrammarColors[i]._sColor;
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