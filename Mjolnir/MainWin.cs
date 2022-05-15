using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using System.Drawing;
using System.Windows.Forms;
using System.IO;
using System.Xml;
using System.Xml.XPath;
using System.Runtime.InteropServices;
using System.Linq;

using SkiaSharp;

using Play.Interfaces.Embedding; 
using Play.Rectangles;
using Play.Edit;
using Play.Parse;

namespace Mjolnir {
    /// <summary>
    /// This identifies the sides that contain herders. However there are other parts
    /// of the layouts that do NOT have hearders in them. Namely the new Tabs window.
    /// </summary>
    public enum SideIdentify {
        Left,
        Right,
        Bottom,
        Tools,
        Options, // Tool Options, I'll probably depricate the old options decor.
    }

    /// <summary>
    /// The form where all the other window's live for the Retro (normal) desktop case.
    /// </summary>
    public partial class MainWin :
        Form,
		IPgParent,
        IPgLoad<XmlElement>,
        IPgSave<XmlDocumentFragment>,
        IEnumerable<SmartHerderBase>,
		IPgMainWindow
    {
        public Font       DecorFont        { get; } = new Font( "Segoe UI Symbol", 12 ) ?? throw new InvalidOperationException("Main Window could not load Decor font."); 
		public SolidBrush ToolsBrushActive { get; } = new SolidBrush( Color.FromArgb( 255, 112, 165, 234 ) ) ?? throw new InvalidOperationException("Main Window could not create tools color brush."); 
        public Font		  ToolsFont		   => Document.FontStandard;

        readonly SmartGrab _rcFrame = new SmartGrab( new SmartRect( LOCUS.UPPERLEFT, 50, 50, 300, 300 ),  5, true, SCALAR.ALL );
        readonly int       _iMargin = 5;  

        readonly Dictionary<SideIdentify, SideRect> _rgSideInfo = new Dictionary<SideIdentify, SideRect>(5);

        readonly SmartRect _rctDragBounds     = new SmartRect( LOCUS.CENTER, 0, 0, 0, 0 ); // Might want to subclass a smartrect with ISmartDrag methods...
        ISmartDrag         _oDrag             = null; // This is our standard drag drop object.

        MenuStrip         _oTopMenu           = null;
        ToolStripMenuItem _miRecentsMenu      = null;
        ToolStripMenuItem _miViewListMenu     = null;
        ToolStripMenuItem _miDecorMenu        = null;
		ToolStripMenuItem _miToolsMenu        = null;
        ToolStripMenuItem _miViewCreate       = null;
        ContextMenuStrip  _oContextMenu       = new ContextMenuStrip();
        Point             _pntContextLocation = new Point(); // context menu object has no activation position! Go figure!

        readonly MainWinDecorEnum _oDecorEnum;

        readonly Dictionary<string, SmartHerderBase> _rgShepards  = new Dictionary<string, SmartHerderBase>(); 

		readonly ParentRect          _oLayout2;
        readonly LayoutStackVertical _oLayoutPrimary; // New general layout.
  
		ViewSlot _oSelectedWinSite = null;
        IDocSlot _oSelectedDocSite = null;

        protected ViewsEditor _oDoc_ViewSelector;
        protected bool        _fIsClosing = false;
		internal  TOPLAYOUT   _eLayout    = TOPLAYOUT.Solo; // Once layout 1&2 are normalized I won't need this.

        protected SCRIPT_FONTPROPERTIES _sDefFontProps = new SCRIPT_FONTPROPERTIES();
        protected IntPtr                _hScriptCache  = IntPtr.Zero;
		protected bool                  _fTextInvalid  = true;

		public enum TOPLAYOUT {
			Solo,
			Multi
		}

        /// <summary>
        /// New ViewSite implementation for sub views of this window that are not part
        /// of the main document system. This is for degenerate tools of the main window.
        /// </summary>
		protected class WinSlot :
			IPgViewSite, 
            IPgViewNotify
		{
			protected readonly MainWin _oHost;

			public WinSlot( MainWin oHost ) {
				_oHost = oHost ?? throw new ArgumentNullException();
			}

			public IPgParent Host => _oHost;

			public void LogError(string strMessage, string strDetails, bool fShow=true) {
				_oHost.LogError( this, strMessage, strDetails, fShow );
			}

			public void Notify( ShellNotify eEvent ) {
			}

            public IPgViewNotify EventChain => this;

            public void NotifyFocused  ( bool fSelect ) { }
            public bool IsCommandKey   ( CommandKey ckCommand, KeyBoardEnum kbModifier ) { return false; }
            public bool IsCommandPress ( char cChar ) { return false; }
        }

        /// <remarks>1/14/2022, Normally I would initialize layouts in the InitNew() but I'll
        /// leave this for now.</remarks>
		/// <exception cref="ArgumentNullException" />
        public MainWin( Program oDocument ) {
            Document = oDocument ?? throw new ArgumentNullException(  nameof( oDocument ) );

            // This could be in the initialize/initnew() steps, but it's nice to have
            // these as readonly variables. I'll leave it for now.
            _oLayoutPrimary = new LayoutStackVertical() { Spacing = 5 };
            // This one probably won't work anymore. And we'll certainly lose all the
            // docking windows b/c those are docked by the layout now.
			_oLayout2       = new LayoutFlowSquare_MainWin( this ) { Spacing = 5 };

            _oDecorEnum = new MainWinDecorEnum( this );
        }

		public IPgParent Parentage => Document;
		public IPgParent Services  => Parentage.Services; // BUG: UUUhhh I think I should return =this=, but I'll break things if I change it now.
        public IPgParent TopWindow => this; // Buck stops here!
		public Program   Document { get; }
		public SmartRect Frame    { get { return _rcFrame; } }

        /// <summary date="4/7/2020" >
        /// Honestly this could live right on the main window. But I'm sure the names would
        /// collide with the existing view management code on the main window. So I'll put all this
        /// here for now.
        /// </summary>
        public class ControllerForMainWindow : IPgController2, IEnumerable<IPgViewType>
        {
            MainWin _oMainWin;

            public ControllerForMainWindow( MainWin oMainWin ) {
                _oMainWin = oMainWin ?? throw new ArgumentNullException();
            }

            public string PrimaryExtension => string.Empty;

            public IDisposable CreateDocument(IPgBaseSite oSite, string strExtension) {
                try {
                    if( string.Compare( strExtension, ".views" ) == 0 )
                        return new ViewsEditor( oSite );
                } catch( Exception oEx  ) { 
                    Type[] rgErrors = { typeof( InvalidCastException ),
                                        typeof( ArgumentException ),
                                        typeof( NullReferenceException ),
										typeof( ArgumentNullException ) };
                    _oMainWin.LogError( oSite, "hosting", "Guest does not support required interfaces.");

                    if( rgErrors.IsUnhandled( oEx ) )
                        throw;
                }
                return null;
            }

            /// <summary>
            /// This is a new use for a controller. So it's a bit confusing. The main window is
            /// basically the controller. The Document parameter is the document within the MainWindow
            /// we are managing. In some regard we don't know where the document came from! Currently
            /// the view embedding interfaces don't give us access to the document site. 
            /// </summary>
            /// <remarks>You know, I should make the view site able to return a document or document site.</remarks>
            public IDisposable CreateView(IPgViewSite oViewSite, object oDocument, Guid guidViewType) {
                try {
                    if( guidViewType == Program.ViewSelector ) {
                        EditWindow2 oEditWin = new EditWindow2(oViewSite, (BaseEditor)oDocument, fReadOnly:true, fSingleLine:false);
                        
                        oEditWin.HyperLinks.Add( "ViewSwitch", OnHyperViewSwitch );
                        oEditWin.ToolSelect = 1;
                        oEditWin.Wrap       = false;

                        return oEditWin;
                    }
                } catch( Exception oEx ) {
                    Type[] rgErrors = { typeof( NullReferenceException ),
                                        typeof( InvalidCastException ),
                                        typeof( ArgumentNullException ) };
                    if( rgErrors.IsUnhandled( oEx ) )
                        throw new InvalidProgramException( "Unexpected error creating editor view.", oEx );

                    _oMainWin.Document.LogError( "Main Window children", "Could not create a child window for the main window" );
                }

			    throw new ArgumentOutOfRangeException( "Don't recognize top level window requested!" );
            }

            /// <summary>
            /// It's a little hacky to add the call back here, but let's go for it.
            /// </summary>
            private void OnHyperViewSwitch( Line oLine, IPgWordRange oRange ) {
                try { 
                    if( oLine is ViewSlot oViewLine ) {
                        _oMainWin.ViewSelect( oViewLine, fFocus:true );
                    }
                } catch( NullReferenceException ) {
                }
            }

            public IEnumerator<IPgViewType> GetEnumerator() {
 	            yield return new ViewType( "View Select", Program.ViewSelector );
            }

            IEnumerator IEnumerable.GetEnumerator() {
                return GetEnumerator();
            }

            public PgDocumentDescriptor Suitability(string strExtension) {
                throw new NotImplementedException();
            }
        }

        /// <summary>
        /// The Main window didn't have it's own document embedding slot. This is different than a view
        /// slot or a program document slot. I used to want the views selection to live on the program
        /// but I'm changing my mind and I'm slowly migrating it to the MainWindow. I'm hardcoding this
        /// instance to the ViewSelector and can sort it all out later. The main complexity is that
        /// decor views want access to an IDocSlot on the document. That sux and we need to work on that.
        /// </summary>
        /// <remarks>1/14/2022 : Might be a bit of overkill with a controller. </remarks>
        protected class DocSlot : IPgBaseSite, IDocSlot  {
            readonly MainWin                 _oHost;
            readonly ControllerForMainWindow _oController;
                     IDisposable             _oGuest;
                     IPgLoad                 _oGuestLoad;

            public DocSlot( MainWin oMainWin ) {
                _oHost       = oMainWin ?? throw new ArgumentNullException( "Main window reference must not be null" );
                _oController = new ControllerForMainWindow( _oHost );
            }

            public IPgParent      Host       => _oHost;
            public int            ID         => 0;
            public IDisposable    Document   => _oGuest;
            public bool           IsDirty    => false;
            public string         Title      => "View Selector";
            public string         FileName   => string.Empty;
            public IPgController2 Controller => _oController;

            public IEnumerable<IPgViewType> ViewTypes => _oController;

            public int    Reference { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
            public string LastPath => throw new NotImplementedException();

            public void Dispose() {
                _oGuest.Dispose();
            }

            public void LogError(string strMessage, string strDetails, bool fShow = true) {
                _oHost.LogError( this, strMessage, strDetails, fShow );
            }

            public bool InitNew() {
                try {
                    _oGuest     = Controller.CreateDocument( this, ".views" ) ?? throw new ApplicationException("Couldn't assign document");
                    _oGuestLoad = (IPgLoad)_oGuest;
                } catch( Exception oEx ) {
                    Type[] rgErrors = { typeof( ApplicationException ),
                                        typeof( InvalidCastException ) };
                    if( rgErrors.IsUnhandled( oEx ) )
                        throw;

                    return false;
                }

                return _oGuestLoad.InitNew();
            }

            public bool Load(string strFileName) {
                throw new NotImplementedException();
            }

            public void Notify(ShellNotify eEvent) {
            }

            public bool Save(bool fNewLocation) {
                throw new NotImplementedException();
            }
        }

        /// <summary>
        /// Load our global configuration. This is different than per instance session state
		/// which is loaded via a standard InitNew/Load API.
        /// </summary>
        /// <remarks>
        /// 12/3/2018 : It would be nice if the window was at least minimally functional if any
        /// of the XML reads fail, but we're not quite there yet.
        ///</remarks>
		///<exception cref="ArgumentNullException" />
		///<seealso cref="Program.InitNew"
		///<seealso cref="Program.Load(TextReader)"/>
        public void Initialize( XmlDocument xmlConfig ) {
			if( xmlConfig == null )
				throw new ArgumentNullException();

            InitializeMenu    ( xmlConfig );
            InitializeEdges   ( xmlConfig );
            // It's super dangerous to be calling InitializeLocation which moves the window
            // BEFORE WE'VE InitNew/Loaded the Program.
            InitializeLocation( xmlConfig );
            InitializeShepards( xmlConfig );

            // Looking this over, all of this stuff below should probably be in InitNew()/Load()...
            // Seems a bit clunky with a controller here. I might pull that apart...
            DocSlot oViewSitesSlot = new DocSlot(this);
            oViewSitesSlot.InitNew();
            _oDoc_ViewSelector = (ViewsEditor)oViewSitesSlot.Document;

            // This needs to follow the view selector document assignment.
            MainWin_Tabs oTabs = new(new WinSlot(this), _oDoc_ViewSelector);
            oTabs.Parent = this;
            oTabs.Layout.Padding.SetRect( 5, 5, 5, 0 );
            oTabs.InitNew();

            // Set up our primary layout here...
            LayoutStackVertical   oInner  = new() { Spacing = 5 };
            LayoutStackHorizontal oCenter = new() { Spacing = 5 }; // { CSS = None }

            oInner.Add( _rgSideInfo[SideIdentify.Options] ); 
            oInner.Add( new LayoutGrab( LayoutRect.CSS.None, _rcFrame ) ); 

            oCenter.Padding.SetRect( 5, 0, 5, 0 );

            oCenter.Add( _rgSideInfo[SideIdentify.Left] );
            oCenter.Add( _rgSideInfo[SideIdentify.Tools] );
            oCenter.Add( oInner ); 
            oCenter.Add( _rgSideInfo[SideIdentify.Right] ); 

            _oLayoutPrimary.Add( new LayoutControl( oTabs,     LayoutRect.CSS.Flex, 40) ); 
            _oLayoutPrimary.Add( new LayoutControl( _oTopMenu, LayoutRect.CSS.Flex, 34 ) ); 
            _oLayoutPrimary.Add( oCenter); 
            _oLayoutPrimary.Add( _rgSideInfo[SideIdentify.Bottom] );

            _oLayoutPrimary.Padding.Bottom = 5;

            DecorMenuReload();
        }

        //protected override void Dispose(bool disposing) {
        //    if (disposing) {
        //        Document.EventUpdateTitles -= UpdateAllTitlesFor;
        //    }

        //    if( _hScriptCache != IntPtr.Zero ) {
        //        PgUniscribe.ScriptFreeCache( ref _hScriptCache );
        //        _hScriptCache = IntPtr.Zero;
        //    }

        //    base.Dispose(disposing);
        //}

        // This old style drag drop was an attempt to support the feature but it's
        // recommended that OLE drag drop is used. http://msdn2.microsoft.com/en-us/library/bb776904(VS.85).aspx
        // See also System.Runtime.InteropServices Marshal::GetComInterfaceForObject() 
        private const int  WS_EX_ACCEPTFILES = 0x00000010;
        private const int  WM_DROPFILES      = 0x0233;
        private const uint DQ_FILECOUNT      = 0xFFFFFFFF;

        /*
        protected override CreateParams CreateParams {
            get {
                CreateParams cp = base.CreateParams;
                cp.ExStyle = cp.ExStyle | WS_EX_ACCEPTFILES;
                return cp;
            }
        }
        */

        //[DllImport("shell32.dll", CharSet = CharSet.Auto)]
        //public static extern int DragQueryFile(IntPtr hDrop, int iFile, StringBuilder lpszFile, int cch);
        //[DllImport("shell32.dll")]
        //static extern void DragFinish(IntPtr hDrop);
        //[DllImport("shell32.dll")]
        //static extern void DragAcceptFiles( IntPtr hWnd, bool fAccept );

        //[DllImport( "ole32.DLL", EntryPoint= "RegisterDragDrop", SetLastError= true)]
        //public static extern UInt32 RegisterDragDrop( IntPtr hWin, IntPtr iDragDrop );

        /*
        protected override void WndProc(ref Message m) {
            if (m.Msg == WM_DROPFILES) {
                int           iNumChars      = DragQueryFile(m.WParam, 0, null, 0);
                StringBuilder sbDropFilePath = new StringBuilder(iNumChars + 1);

                DragQueryFile(m.WParam, 0, sbDropFilePath, iNumChars + 1);
                DragFinish(m.WParam);

                string strItem = sbDropFilePath.ToString();
                //MainWin  oHost = (MainWin)_oSite.Host;
                this.CreateTextEditor( strItem );
            }
            base.WndProc(ref m);
        }
        */

        protected override void OnHandleCreated(EventArgs e) {
            base.OnHandleCreated(e);

            // TODO: We do a lot in our constructor. But not all the children windows (like FindWindow) are constructed when
            // we load our state, as such they won't know the selected view. I'm going to try this here for now. I thinking 
            // what we really need is a multi-step construction. First we wire up our window and wait until we have our handle and can display
            // messages. Then load up children, then send this event.
            if (_oSelectedWinSite != null && ViewChanged != null)
                ViewChanged(_oSelectedWinSite.Guest as IPgParent ); // BUG, guest really needs to be a IPgParent.

			if( _hScriptCache == IntPtr.Zero ) { 
                using( Graphics oG = this.CreateGraphics() ) { 
                    using( GraphicsContext oDC = new GraphicsContext( oG ) ) {
 				        using( new ItemContext( oDC.Handle, ToolsFont.ToHfont() ) ) {
				            _sDefFontProps.Load( oDC.Handle, ref _hScriptCache );
                        }
                    }
                }
            }

            //RegisterDragDrop( this.Handle, Marshal.GetComInterfaceForObject( this, typeof( Microsoft.VisualStudio.OLE.Interop.IDropTarget ) ) );
        }

        protected override void OnDragEnter(DragEventArgs drgevent) {
            base.OnDragEnter(drgevent);
        }

        internal IEnumerator<ColorMap> SharedGrammarColors {
			get {
				return Document.GetSharedGrammarColors();
			}
        }

        public Bitmap BitmapCreateFromChar( string strText ) {
            Size   oSize = new Size( 16, 16 );
            Bitmap oBmp  = new Bitmap( oSize.Width, oSize.Height );

            using( Graphics g = Graphics.FromImage(oBmp) ) {
                using( Font oFont = new Font( "Segoe MDL2 Assets", 10f, FontStyle.Regular ) ) {
                    float  fPixels = oFont.SizeInPoints * oFont.FontFamily.GetCellDescent( FontStyle.Regular ) / oFont.FontFamily.GetEmHeight(FontStyle.Regular);
                    PointF oPointF = new PointF( 0, fPixels );

                    g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;

                    g.DrawString( strText, oFont, Brushes.Black, oPointF );
                }
            }

            return( oBmp );
        }
        
        /// <summary>
        /// Initialize the paths to the grammars on first load. This is when we are copying our
        /// config file to the user's appdata area.
        /// </summary>
        /// <param name="strInstallDir">Path where the executable lives</param>
        /// <param name="xmlDocument">A parsed config file.</param>
        protected void InitializePathsToGrammer( string strInstallDir, XmlDocument xmlDocument ) {
            XmlNodeList lstTypes = xmlDocument.SelectNodes("config/languages/grammars/grammar");

            foreach( XmlElement xeType in lstTypes ) {
                string strFile = Path.GetFileName( xeType.GetAttribute( "file" ) );
                xeType.SetAttribute( "file", strInstallDir + "\\" + strFile );
            }
        }

        /// <summary>
        /// Prep us for moving more of this info into the config xml file,
        /// instead of hard coded as is.
        /// </summary>
        protected struct SideStuff {
            public SideStuff( TRACK p_eTrack, int p_iInit ) {
                eTrack = p_eTrack;
                iInit  = p_iInit;
            }

            public TRACK eTrack;
            public int   iInit;
        }
        
        /// <summary>
        /// Find the values we persisted for our border margins.
        /// </summary>
        /// <param name="xmlDocument">Pointer to the xml document with our settings.</param>
        protected void InitializeEdges( XmlDocument xmlDocument ) {
            XmlElement xmlElem = xmlDocument.SelectSingleNode( "config/mainwindow/margin" ) as XmlElement;

            Dictionary<string, SideStuff> _rgDim = new Dictionary<string, SideStuff>();

            // These need to match the SideIdentify items in the config. 
            // BUG: Fix so we won't fail of the xml in "margin" is empty.
			_rgDim.Add( "left",    new SideStuff( TRACK.VERT,  250 ) );
			_rgDim.Add( "right",   new SideStuff( TRACK.VERT,  250 ) );
			_rgDim.Add( "bottom",  new SideStuff( TRACK.HORIZ, 100 ) );
            _rgDim.Add( "tools",   new SideStuff( TRACK.VERT,   65 ) );
            _rgDim.Add( "options", new SideStuff( TRACK.HORIZ,  30 ) );

            // Read in all the edge values.
            try {
                if( xmlElem != null ) {
                    foreach( SideIdentify eSide in Enum.GetValues( typeof( SideIdentify ) ) ) {
                        string strSide = eSide.ToString().ToLower();
                        string strSize = xmlElem.GetAttribute( strSide );
                        if( !string.IsNullOrEmpty( strSize ) ) { 
                            if( int.TryParse( xmlElem.GetAttribute( strSide ), out int iValue ) ) {
                                SideStuff sStuff  = _rgDim[strSide];
							    _rgSideInfo.Add(eSide, new SideRect( sStuff.eTrack ) { 
                                    Spacing  = 5,
                                    SideInit = sStuff.iInit, // size when opened for the first time.
                                    Track    = (uint)iValue,
                                    Layout   = LayoutRect.CSS.Pixels
                                } );
                                if( eSide == SideIdentify.Bottom ) {
                                    _rgSideInfo[SideIdentify.Bottom].Padding.SetRect( 5, 0, 5, 0 );
                                }
						    } else {
							    LogError( null, "initialization", "Couldn't read margin from config: " + strSide );
						    }
                        }
                    }
                }
            } catch( Exception oE ) {
                Type[] rgError = { typeof( KeyNotFoundException ),
                                   typeof( ArgumentException ),
                                   typeof( FormatException ) };

                if(rgError.IsUnhandled( oE ) )
                    throw;

				LogError( null, "initialization", "General Edge init error. Check margins in config." );

                _rgSideInfo.Clear();

                foreach( SideIdentify eSide in Enum.GetValues( typeof( SideIdentify ) ) ) {
                    string    strSide = eSide.ToString().ToLower();
                    SideStuff sStuff  = _rgDim[strSide];
                    _rgSideInfo.Add( eSide, new SideRect( sStuff.eTrack ) { 
                        Spacing  = 5,
                        SideInit = sStuff.iInit, 
                        Track    = (uint)sStuff.iInit, 
                        Layout   = LayoutRect.CSS.Pixels } );
                }
            }

            _rcFrame.Hidden     = false;
            _rcFrame.Show       = SHOWSTATE.Inactive;
        }

        /// <summary>
        /// Make sure you call this after InitializeDocking()! The size window calls back on us.
        /// Load
        ///     string initLocation = Properties.Settings.Default.InitialLocation; // saved string
        ///     // parse and load location/size
        /// Save
        ///     location = RestoreBounds.Location;
        ///     size     = RestoreBounds.Size;
        ///     string initLocation = string.Join(",", location.X, location.Y, size.Width, size.Height);
        ///     Properties.Settings.Default.InitialLocation = initLocation;
        ///     Properties.Settings.Default.Save();
        /// </summary>
        /// <param name="xmlDocument"></param>
        protected void InitializeLocation( XmlDocument xmlDocument ) {
            int   [] rgMain = new int[4];
			string[] rgDims = { "left", "top", "width", "height" };
            try {
				if (xmlDocument.SelectSingleNode("config/mainwindow/location") is XmlElement xmlElem) {
					for (int i = 0; i < rgDims.Length; ++i) {
						int.TryParse(xmlElem.GetAttribute(rgDims[i]), out rgMain[i]);
					}
				}
			} catch( Exception oE ) {
                Type[] rgError = { typeof( ArgumentException ),
                                   typeof( FormatException ),
                                   typeof( NullReferenceException ) };

                if(rgError.IsUnhandled( oE ) )
                    throw;

                rgMain[0] = 10;
                rgMain[1] = 10;
                rgMain[2] = 800;
                rgMain[3] = 600;
            }

            SuspendLayout();

            StartPosition = FormStartPosition.Manual;
            Location = new Point( rgMain[0], rgMain[1] );
            Size     = new Size ( rgMain[2], rgMain[3] ); // ends up making LayoutFrame() call.

            ResumeLayout();
        }

		/// <summary>
		/// 12/5/2018 : We assume the main program has either called InitNew or Load by now. In the future,
		/// I'll probably stick this code in the InitNew and Load sequence. We're not quite there yet.
		/// </summary>
		/// <param name="xmlConfig"></param>
        protected void InitializeMenu( XmlDocument xmlConfig ) {
			_oTopMenu = new MyMenuStrip() {
				Font        = Document.FontMenu,
				AutoSize    = false,
				LayoutStyle = ToolStripLayoutStyle.Flow
			};

			// Save Menu shortcut.
			//ToolStripMenuItem oSaveShortcut = new ToolStripMenuItem( string.Empty, oSaveBitmap, new EventHandler(this.OnDocSave));
            _oTopMenu.Items.Add(new ToolStripMenuItem( MyExtensions.str3InchFloppy, null, new EventHandler(this.OnDocSave) ));

            // File Menu.
            ToolStripMenuItem oFileMenu = new ToolStripMenuItem("File");
            _oTopMenu.Items.Add( oFileMenu );

            List<ToolStripMenuItem> rgSubMenu = new List<ToolStripMenuItem>();
            rgSubMenu.Add( new ToolStripMenuItem("Html",    BitmapCreateFromChar( "\xE12b" ), new EventHandler(this.OnDocNewHtml  )));
            rgSubMenu.Add( new ToolStripMenuItem("Text",    BitmapCreateFromChar( "\xE185" ), new EventHandler(this.OnDocNewText  )));
            rgSubMenu.Add( new ToolStripMenuItem("M3u",     BitmapCreateFromChar( "\xE189" ), new EventHandler(this.OnDocNewM3u   )));
            rgSubMenu.Add( new ToolStripMenuItem("Scraps",  BitmapCreateFromChar( "\xE0a5" ), new EventHandler(this.OnDocNewScraps)));
            rgSubMenu.Add( new ToolStripMenuItem("Morse",   BitmapCreateFromChar( "\xE113" ), new EventHandler(this.OnDocNewMorse )));
            rgSubMenu.Add( new ToolStripMenuItem("Net Log", BitmapCreateFromChar( "\xE113" ), new EventHandler(this.OnDocNewNetLogger)));
            rgSubMenu.Add( new ToolStripMenuItem("Std Log", BitmapCreateFromChar( "\xE113" ), new EventHandler(this.OnDocNewStdLogger)));
            rgSubMenu.Add( new ToolStripMenuItem("SSTV",    BitmapCreateFromChar( "\xE114" ), new EventHandler(this.OnDocNewSSTV  )));

            oFileMenu.DropDownItems.Add(new ToolStripMenuItem("New",      BitmapCreateFromChar( "\xE295" ), rgSubMenu.ToArray() ) );
            oFileMenu.DropDownItems.Add(new ToolStripMenuItem( "Open...", BitmapCreateFromChar( "\xE132" ), new EventHandler(this.OnDocOpen), Keys.Control | Keys.O ));
            oFileMenu.DropDownItems.Add(new ToolStripMenuItem("Paste",    BitmapCreateFromChar( "\xE16C" ), new EventHandler(this.OnDocPaste)));
            List<ToolStripMenuItem> rgSubMenu4 = new List<ToolStripMenuItem>();
            oFileMenu.DropDownItems.Add( _miRecentsMenu = new ToolStripMenuItem("Recent", BitmapCreateFromChar( "\xE1a5" ) ) );

			foreach( Line oLine in Document.DocRecents ) {
                ToolStripMenuItem oMenuItem = new MenuItemWithLine( oLine, new EventHandler(OnDocFavorites) );

                _miRecentsMenu.DropDownItems.Add( oMenuItem );
			}

            oFileMenu.DropDownItems.Add(new ToolStripSeparator() );
            _miViewCreate = new ToolStripMenuItem("View", BitmapCreateFromChar( "\xE187" ), rgSubMenu4.ToArray() );
            oFileMenu.DropDownItems.Add( _miViewCreate );

            oFileMenu.DropDownItems.Add(new ToolStripSeparator() ); //---
            oFileMenu.DropDownItems.Add(new ToolStripMenuItem("Save",       BitmapCreateFromChar( "\xe105" ), new EventHandler(this.OnDocSave), Keys.Control | Keys.S ));
            oFileMenu.DropDownItems.Add(new ToolStripMenuItem("Save As...", BitmapCreateFromChar( "\xe159" ), new EventHandler(this.OnDocSaveAs)));
            oFileMenu.DropDownItems.Add(new ToolStripMenuItem("Save All",   BitmapCreateFromChar( "\xe158" ), new EventHandler(this.OnDocSaveAll), Keys.Control | Keys.Shift | Keys.O ));
          //oFileMenu.DropDownItems.Add(new ToolStripMenuItem("Run",        null, new EventHandler(this.OnRun)));
            oFileMenu.DropDownItems.Add(new ToolStripSeparator() ); //---

            ToolStripMenuItem[] rgSubMenu3 = new ToolStripMenuItem[3] {
                new ToolStripMenuItem("Name",        BitmapCreateFromChar( "\xE132" ), new EventHandler(this.OnFileCopyName)),
                new ToolStripMenuItem("Directory",   BitmapCreateFromChar( "\xE188" ), new EventHandler(this.OnFileCopyDirectory)),
                new ToolStripMenuItem("Path",        BitmapCreateFromChar( "\xE1DA" ), new EventHandler(this.OnFileCopyPath))
            };

            oFileMenu.DropDownItems.Add(new ToolStripMenuItem("Copy",   BitmapCreateFromChar( "\xE16D" ), rgSubMenu3 ) );

            List<ToolStripMenuItem> rgSubMenuPlay = new List<ToolStripMenuItem>();
            rgSubMenuPlay.Add(new ToolStripMenuItem("Sequential", BitmapCreateFromChar("\xE149"), new EventHandler(this.OnDocPlaySeqential)));
            rgSubMenuPlay.Add(new ToolStripMenuItem("Random",     BitmapCreateFromChar("\xE14b"), new EventHandler(this.OnDocPlayRandom)));
            oFileMenu.DropDownItems.Add(new ToolStripMenuItem("Play", BitmapCreateFromChar("\xE189"), rgSubMenuPlay.ToArray()));

            oFileMenu.DropDownItems.Add(new ToolStripSeparator() ); //---

            oFileMenu.DropDownItems.Add(new ToolStripMenuItem("Close",  BitmapCreateFromChar( "\xe10a" ), new EventHandler(this.OnFileClose))); // s/b file close, close all views and file!

            // Edit Menu.
            ToolStripMenuItem oEditMenu = new ToolStripMenuItem("Edit");
            _oTopMenu.Items.Add( oEditMenu );

            oEditMenu.DropDownItems.Add(new ToolStripMenuItem("Cut",   BitmapCreateFromChar( "\xE16B" ), new EventHandler(this.OnEditCut),   Keys.Control | Keys.X));
            oEditMenu.DropDownItems.Add(new ToolStripMenuItem("Copy",  BitmapCreateFromChar( "\xE16D" ), new EventHandler(this.OnEditCopy),  Keys.Control | Keys.C));
            oEditMenu.DropDownItems.Add(new ToolStripMenuItem("Paste", BitmapCreateFromChar( "\xE16C" ), new EventHandler(this.OnEditPaste), Keys.Control | Keys.V));

            ToolStripMenuItem[] rgSubMenu2 = new ToolStripMenuItem[3] {
                new ToolStripMenuItem("Img",    BitmapCreateFromChar( "\xE114" ), new EventHandler(this.OnEditPasteAsImg)),
                new ToolStripMenuItem("Text",   BitmapCreateFromChar( "\xE185" ), new EventHandler(this.OnEditPasteAsText)),
                new ToolStripMenuItem("Base64", BitmapCreateFromChar( "\xE159" ), new EventHandler(this.OnEditPasteAsBase64 ))
            };

            oEditMenu.DropDownItems.Add(new ToolStripMenuItem("Paste as",   BitmapCreateFromChar( "\xE16C" ), rgSubMenu2 ) );
          //oEditMenu.DropDownItems.Add(new ToolStripMenuItem("Insert...",  BitmapCreateFromChar( "\xE16F" ), new EventHandler( this.OnEditInsert ) ));
            oEditMenu.DropDownItems.Add(new ToolStripSeparator() ); //---

            oEditMenu.DropDownItems.Add(new ToolStripMenuItem("Undo",       BitmapCreateFromChar( "\xE10e" ), new EventHandler(this.OnEditUndo),    Keys.Control | Keys.Z ));
            oEditMenu.DropDownItems.Add(new ToolStripMenuItem("Delete",     BitmapCreateFromChar( "\xE107" ), new EventHandler(this.OnEditDelete ), Keys.Delete ) );

            oEditMenu.DropDownItems.Add(new ToolStripSeparator()); //---

            List<ToolStripMenuItem> rgSubMenuLock = new List<ToolStripMenuItem> {
                new ToolStripMenuItem("Accept", BitmapCreateFromChar( "\xE1f7" ), new EventHandler(this.OnEditUnlock )),
                new ToolStripMenuItem("Block",  BitmapCreateFromChar( "\xE1f6" ), new EventHandler(this.OnEditLock ))
            };
            oEditMenu.DropDownItems.Add(new ToolStripMenuItem("Text", BitmapCreateFromChar("\xE144"), rgSubMenuLock.ToArray()));

            oEditMenu.DropDownItems.Add(new ToolStripSeparator() ); //---
            oEditMenu.DropDownItems.Add(new ToolStripMenuItem("Select All", BitmapCreateFromChar( "\xE14e" ), new EventHandler(this.OnEditSelectAll), Keys.Control | Keys.A));
			oEditMenu.DropDownItems.Add(new ToolStripMenuItem("Select Off", BitmapCreateFromChar( "\x0000" ), new EventHandler(this.OnEditSelectOff), Keys.Control | Keys.D));
            oEditMenu.DropDownItems.Add(new ToolStripMenuItem("Collect",    BitmapCreateFromChar( "\xE0e7" ), new EventHandler(this.OnCollect),  Keys.Control | Keys.L ));
            oEditMenu.DropDownItems.Add(new ToolStripMenuItem("Find",       BitmapCreateFromChar( "\xE1a3" ), new EventHandler(this.OnEditFind), Keys.Control | Keys.F));

			_miToolsMenu = new ToolStripMenuItem( "Tool" );
			_oTopMenu.Items.Add( _miToolsMenu );

            _miDecorMenu = new ToolStripMenuItem( "Dock" );
            _oTopMenu.Items.Add(_miDecorMenu);

            _miViewListMenu = new ToolStripMenuItem("Views");
			_miViewListMenu.DropDownItems.Add( new ToolStripMenuItem( "Show All", BitmapCreateFromChar( MyExtensions.strHome ), this.OnViewAll ) );

            _oTopMenu.Items.Add(_miViewListMenu);

            ToolStripMenuItem oSettingsMenu = new ToolStripMenuItem("Session");
            _oTopMenu.Items.Add(oSettingsMenu);
            oSettingsMenu.DropDownItems.Add(new ToolStripMenuItem("Settings!",  BitmapCreateFromChar( "\xE12a" ), new EventHandler(this.OnConfigOpen )));
            oSettingsMenu.DropDownItems.Add(new ToolStripMenuItem("Open...",    BitmapCreateFromChar( "\xE132" ), new EventHandler(this.OnSessionView  )));
            oSettingsMenu.DropDownItems.Add(new ToolStripMenuItem("Save",       BitmapCreateFromChar( "\xe105" ), new EventHandler(this.OnSessionSave  )));
            oSettingsMenu.DropDownItems.Add(new ToolStripMenuItem("Save As...", BitmapCreateFromChar( "\xe159" ), new EventHandler(this.OnSessionSaveAs)));
            oSettingsMenu.DropDownItems.Add(new ToolStripSeparator() );
            oSettingsMenu.DropDownItems.Add(new ToolStripMenuItem("Alerts!",       BitmapCreateFromChar( "\xE1de" ), new EventHandler(this.OnSessionOpenAlerts )));
            oSettingsMenu.DropDownItems.Add(new ToolStripMenuItem("Find Results!", BitmapCreateFromChar( "\xE179" ), new EventHandler(this.OnSessionOpenResults)));
            oSettingsMenu.DropDownItems.Add(new ToolStripMenuItem("Recent List!",  BitmapCreateFromChar( "\xE1a5" ), new EventHandler(this.OnSessionOpenRecents)));
            oSettingsMenu.DropDownItems.Add(new ToolStripMenuItem("Scrap Book!",   BitmapCreateFromChar( "\xE1d3" ), new EventHandler(this.OnSessionOpenScraps )));
            oSettingsMenu.DropDownItems.Add(new ToolStripSeparator() );
            oSettingsMenu.DropDownItems.Add(new ToolStripMenuItem("About!",     BitmapCreateFromChar( "\xe0a5" ), new EventHandler( this.OnHelpAbout )));  // "\xE2c0" doesn't work. :-(
                                                                                                                                                           //oSettingsMenu.DropDown.MouseLeave += OnDropDown_MouseLeave;

            if (_oContextMenu != null) {
                _oContextMenu.Items.Add(new ToolStripMenuItem("Minimize")); // Just place holders for now. 
                _oContextMenu.Items.Add(new ToolStripMenuItem("Maximize"));
                _oContextMenu.Items.Add(new ToolStripMenuItem("Close", null, new EventHandler(this.OnDecorCloseCommand)));
                _oContextMenu.Items.Add(new ToolStripMenuItem("Menu",  null, new EventHandler(this.OnDecorMenuOpenCommand)));

                _oContextMenu.Opened += new EventHandler(OnContextMenuPopup);
                this.ContextMenuStrip = _oContextMenu;
            }

            ToolStripMenuItem oParent = new ToolStripMenuItem(MyExtensions.strGoParent,  null, new EventHandler(this.OnEditJumpParent ) ) {
				ForeColor = Color.Goldenrod
			};
            _oTopMenu.Items.Add( oParent );
            _oTopMenu.Items.Add(new ToolStripMenuItem(MyExtensions.strGoLeft,    null, new EventHandler(this.OnEditJumpPrev) ) );
            _oTopMenu.Items.Add(new ToolStripMenuItem(MyExtensions.strGoRight,   null, new EventHandler(this.OnEditJumpNext) ) );

			ToolStripMenuItem oPlay = new ToolStripMenuItem(MyExtensions.strPlay, null, new EventHandler(this.OnEditPlay)) {
				ForeColor = Color.DarkGreen
			};
			_oTopMenu.Items.Add( oPlay );
            _oTopMenu.Items.Add(new ToolStripMenuItem(MyExtensions.strPause,     null, new EventHandler(this.OnEditPause ) ) );

			ToolStripMenuItem oStop = new ToolStripMenuItem(MyExtensions.strStop, null, new EventHandler(this.OnEditStop ) ) {
				ForeColor = Color.DarkRed
			};
			_oTopMenu.Items.Add( oStop );

			ToolStripMenuItem oViewNextSibling = new ToolStripMenuItem(MyExtensions.strHome, null, new EventHandler(this.OnViewNextSibling ) ) {
				ForeColor = Color.DarkBlue
			};
			_oTopMenu.Items.Add( oViewNextSibling );

			ToolStripMenuItem oViewNextAll = new ToolStripMenuItem(MyExtensions.strNextView, null, new EventHandler(this.OnViewNextAll ) ) {
				ForeColor = Color.DarkBlue
			};
			_oTopMenu.Items.Add( oViewNextAll );

			MakeDropdownOnHover();
        }

		private void OnDocPlayRandom(object sender,EventArgs e) {
		}

		private void OnDocPlaySeqential(object sender,EventArgs e) {
		}

		/// <summary>
		/// Switch the layout so we see all the views!
		/// </summary>
		/// <seealso cref="ViewSlot.OnMenuSelectView"/>
		private void OnViewAll(object sender,EventArgs e) {
			_eLayout = TOPLAYOUT.Multi;
			LayoutFrame();
		}

		private void OnDropDown_MouseLeave(object sender, EventArgs e) {
			((ToolStripDropDown)sender).Hide();
		}

		/// <summary>
		/// Override menu behavior so we get pop-up menus.
		/// </summary>
		/// <remarks>Unfortunately we can't dispatch the pop up as easily. I've commented out for now.</remarks>
		private void MakeDropdownOnHover() {
			/*
			_oTopMenu.Items.OfType<ToolStripMenuItem>().ToList().ForEach(x =>
				{
					x.MouseEnter += (obj, arg) => { 
						if( ActiveForm != null ) {
							((ToolStripDropDownItem)obj).ShowDropDown();
						}
					};
					// CLose, but can't get the second level pop ups. Grrrr
					//x.DropDown.MouseLeave += (obj, arg) => {
					//	((ToolStripDropDown)x.DropDown).Hide();
					//};
				});		
			*/
		}

		IEnumerator<SmartHerderBase> IEnumerable<SmartHerderBase>.GetEnumerator() {
            return( ShepardEnum() );
        }

        IEnumerator IEnumerable.GetEnumerator() {
            return( ShepardEnum() );
        }

        void OnContextMenuPopup(object sender, EventArgs e) {
            _pntContextLocation = this.PointToClient(Cursor.Position);

            // First check if we're hovering over a herder to determine if we need to grey some stuff.
            SmartHerderBase oHerderFound = null;
            foreach (SmartHerderBase oHerder in this) {
                if (oHerder.IsInside(_pntContextLocation.X, _pntContextLocation.Y)) {
                    oHerderFound = oHerder;
                }
            }

            // Second check to see if the menu herder is showing. 
            SmartHerderBase oHerderMenu = Shepardfind("menu");

            // Now set up our show/grey stuff in our menu.
            string[] rgContexts = { "minimize", "maximize", "close" }; // BUG: hard coded values.
            foreach (ToolStripMenuItem oMenuItem in _oContextMenu.Items) {
                bool fFoundContext = false;
                foreach (string strContext in rgContexts) {
                    // There's a "name" value on the oMenuItem which is probably better since it won't be localized!!!
                    // need to set up the menu so that value get's set instead of depending on "text" property.
                    if (string.Compare(strContext, oMenuItem.Text, true) == 0) {
                        fFoundContext = true;
                    }
                }
                if (fFoundContext) {
                    oMenuItem.Enabled = oHerderFound != null;
                }
                if (string.Compare("menu", oMenuItem.Text, true) == 0 && oHerderMenu != null) { // BUG: hard coded values.
                    oMenuItem.Enabled = !oHerderMenu.Hidden;
                }
            }
        }

        private void OpenOrShowFirstView( IDocSlot oDocSite ) {
            if( oDocSite == null )
                return;

            ViewSlot oViewSite = _oDoc_ViewSelector.FindFirstView( oDocSite );

            if( oViewSite == null ) {
                ViewCreate( oDocSite, Guid.Empty );
            } else {
                oViewSite.SetFocus();
            }
        }

        private void OnSessionOpenAlerts( object s, EventArgs e ) 
        {
            OpenOrShowFirstView( Document.AlertSlot );
        }
    
        private void OnSessionOpenResults( object s, EventArgs e ) 
        {
            OpenOrShowFirstView( Document.ResultsSlot );
        }

        private void OnSessionOpenRecents(object sender, EventArgs e)
        {
            OpenOrShowFirstView( Document.RecentsSlot );
        }

        private void OnSessionOpenScraps(object sender, EventArgs e )
        {
            OpenOrShowFirstView( Document.ScrapBookSlot );
        }

        /// <summary>
        /// Internal error clearing house. This'll be a way for me to track errors in the
        /// system. Anyone can call this but I expect most errors to come the the sites.
        /// </summary>
        /// <remarks>If we get more than one error coming along then we'll get multiple pop
        /// ups. What we should do is open the window with an Alert view in it and just reuse
        /// the open window for the subsequent messages.</remarks>
        /// <param name="oSite">Might be null!</param>
        /// <param name="strCatagory"></param>
        public void LogError( IPgBaseSite oSite, string strCatagory, string strDetails, bool fShow=true ) {
            Document.LogError( oSite, strCatagory, strDetails, fShow );
        }

        /// <summary>
        /// Warn not to close if there are open documents.
        /// </summary>
		/// <remarks>I need to see if there is a difference between the user requesting
		/// a close and the system shutting down.</remarks>
        protected override void OnClosing(CancelEventArgs e)
        {
            base.OnClosing(e);

            _fIsClosing = true;

            e.Cancel = Document.WarnOnOpenDocuments();

			// I'd rather ask the user if they want to save first then close. But that needs a
			// better dialog box than I have a the moment. So just auto save.
			if( Document.SessionSlot.IsDirty ) {
				Document.SessionSave( false );
				//MessageBox.Show( "The session is unsaved.", "Session" );
				//e.Cancel = true;
			}
        }

        /// <summary>A disposed would be better but windows are a bit different then everyone else.</summary>
        //protected override void OnClosed( EventArgs e ) {
        //    base.OnClosed(e);
        //}

        /// <summary>
        /// Closing our main window.
        /// </summary>
        protected override void OnFormClosed( FormClosedEventArgs e ) {
            base.OnFormClosed(e);

            foreach( ViewSlot oLine in _oDoc_ViewSelector ) {
                oLine.Dispose();
            }
            _oDoc_ViewSelector.Clear();

			// BUG: These belong in Dispose() method.
			DecorFont       .Dispose();
            ToolsBrushActive.Dispose();

            foreach( IDocSlot oSlot in Document.DocSlots ) {
                oSlot.Dispose();
            }
        }

        public string VersionInfo {
            get {
                return( System.Diagnostics.FileVersionInfo.GetVersionInfo(System.Reflection.Assembly.GetExecutingAssembly().Location).ToString() );
            }
        }

        /// <summary>
        /// Open up a new .txt document and fill it with the Version Info of this program.
        /// </summary>
        /// <remarks>TODO: Set the path to the last used path. Do not give the file extention a name
        /// or the system will attempt to load that file from the documents directory. ".txt" will
        /// simply create a text document and call InitNew() on it.</remarks>
        public void OnHelpAbout( object sender, EventArgs e ) {
			if (EditorInitNewShow(".txt") is Editor oDocAbout) {
				using (Editor.Manipulator oManip = oDocAbout.CreateManipulator()) {
					using (TextReader oReader = new StringReader(VersionInfo)) {
						for (string strLine = string.Empty; strLine != null; strLine = oReader.ReadLine()) {
							// Unfortunately, right from the get go there's an empty line I don't want.
							if (!string.IsNullOrEmpty(strLine))
								oManip.LineInsert(oDocAbout.ElementCount, strLine);
						}
					}
                    oManip.LineInsert( oDocAbout.ElementCount, "Icons courtesy of... https://icons8.com/" );
				}
			}
		}

        /// <summary>
        /// Just open the raw file for edit. They'll need to save and reload to update the file.
        /// </summary>
        /// <remarks>See also Application.UserAppDataPath()</remarks>
        public void OnConfigOpen( object sender, EventArgs e ) {
            StringBuilder sbConfig = new StringBuilder();

			try {
				sbConfig.Append( Environment.GetFolderPath( Environment.SpecialFolder.LocalApplicationData) );
				sbConfig.Append( "\\pg\\mjolnir\\" );
				sbConfig.Append( "config.phree" );

				DocumentShow( sbConfig.ToString() );
			} catch( NullReferenceException ) {
				LogError( null, "session", "Couldn't find template string, or something!" );
			}
        }

        public void OnSessionSave( object sender, EventArgs e ) {
            Document.SessionSave( fAtNew: false );
        }

        public void OnSessionSaveAs( object sender, EventArgs e ) {
            Document.SessionSave( fAtNew: true );
        }

        /// <summary>
        /// Attempt to open a view on the session document. This should be a read only document since
        /// typing in it won't affect anything and it will get overwritten in the end. If we didn't 
        /// open the shell on a session then there is no session and the doc site is null.
        /// Remember, the guest of the session site is the MAIN WINDOW!!! ^_^; It's not a editor and
        /// we can't open a view on it.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        public void OnSessionView( object sender, EventArgs e ) {
            Program.TextSlot oSessionSlot = Document.SessionSlot; 
            if( _oDoc_ViewSelector.FindFirstView( oSessionSlot ) == null ) {
                if( string.IsNullOrEmpty( oSessionSlot.FileName ) )
                    LogError( oSessionSlot, "host", "The current session has not been persisted." );
                else
                    DocumentShow( oSessionSlot.FileName );
            }
        }

        public void OnDocNewText( object sender, EventArgs e ) {
            EditorInitNewShow( ".txt" );
        }

        public void OnDocNewHtml( object sender, EventArgs e ) {
            EditorInitNewShow( ".html" );
        }

        public void OnDocNewM3u( object sender, EventArgs e ) {
            EditorInitNewShow( ".m3u" );
        }

        public void OnDocNewScraps( object sender, EventArgs e ) {
            EditorInitNewShow( ".scraps" );
        }

        public void OnDocNewMorse( object sender, EventArgs e ) {
            EditorInitNewShow( ".morse" );
        }

        public void OnDocNewNetLogger( object sender, EventArgs e ) {
            EditorInitNewShow( ".netlog" );
        }

        public void OnDocNewStdLogger( object sender, EventArgs e ) {
            EditorInitNewShow( ".stdlog" );
        }

        public void OnDocNewSSTV( object sender, EventArgs e ) {
            EditorInitNewShow( ".mysstv" );
        }

        public void OnDocOpen( object sender, EventArgs e ) {
            ViewSlot oViewSite   = this.ViewSiteSelected;
            string   strLastPath = Document.UserDocs;

            // Look at the currently active view and find the lastpath for that view.
            if( oViewSite != null ) {
                strLastPath = oViewSite.LastPath;
            }

            using( OpenFileDialog oDialog = new OpenFileDialog() ) {
                oDialog.InitialDirectory = strLastPath;

                if( oDialog.ShowDialog() == DialogResult.OK ) {
                    if( FileCheck( oDialog.FileName ) ) {
                        // Don't put this in the FileOk event because after that event
                        // the dialog returns focus to where it came from and we lose
                        // focus from our newly opened view.
                        DocumentShow( oDialog.FileName );
                    }
                }
            }
        }

        public void OnDocFavorites( object sender, EventArgs e ) 
        {
			if (sender is MenuItemWithLine oItem) {
                Document.DocRecents.LineMove( 0, oItem.DocumentLine );

                _miRecentsMenu.DropDownItems.Remove( oItem );
                _miRecentsMenu.DropDownItems.Insert( 0, oItem );

                this.DocumentShow(oItem.Text);
			}
		}

        public void OnDocPaste( object sender, EventArgs e ) {
            IDataObject pDataObject = Clipboard.GetDataObject();
            if( pDataObject.GetDataPresent( DataFormats.FileDrop ) ) {
                string[] rgItems   = pDataObject.GetData(DataFormats.FileDrop) as string[];
                ViewSlot oViewSite = null;

                //TODO: If more than one file to open, I should warn the user and put up a select box.
                foreach( string strFileName in rgItems ) {
                    oViewSite = this.DocumentShow( strFileName, EditorShowEnum.SILENT );
                }

				// Open select and focus the last one.
                if( oViewSite != null )
                    ViewSelect( oViewSite, fFocus:true );
                return;
            }
            if( pDataObject.GetDataPresent( DataFormats.Text ) ) {
                string strItem = pDataObject.GetData(DataFormats.Text) as string;

                this.DocumentShow( strItem );
                return;
            }
        }

        /// <summary>
        /// Look at the file given to determine if valid.
        /// </summary>
        private bool FileCheck( string strFileName ) {
            FileAttributes oAttribs;
            bool           fIsFile  = false;

            try {
                oAttribs = File.GetAttributes(strFileName);
                fIsFile  = ( oAttribs & FileAttributes.Directory ) == 0;
            } catch( Exception oEx ) {
                Type[] rgErrors = { typeof( ArgumentException ),
                                    typeof( PathTooLongException ),
                                    typeof( NotSupportedException ),
                                    typeof( FileNotFoundException ),
                                    typeof( DirectoryNotFoundException ) };

                if( rgErrors.IsUnhandled( oEx ) ) {
                    throw;
                }
            }

            return( fIsFile );
        }

        public void OnDocSave( object sender, EventArgs e ) {
            ViewSlot oViewSite = this.ViewSiteSelected;

            if( oViewSite != null ) {
				if( !oViewSite.Execute( GlobalCommands.Save ) )
					oViewSite.SaveDocument( fAtNewLocation: false );
            } else {
                // TODO: I should couple my alerts with a message box and log the error.
                MessageBox.Show( "First set the central window focus to a " +
                                 "document you wish to save. Thanks!", 
                                 "I'm confused! ^_^;" );
            }
        }

        public void OnDocSaveAs( object sender, EventArgs e ) {
            ViewSlot oViewSite = this.ViewSiteSelected;

            if( oViewSite != null ) {
				if( !oViewSite.Execute( GlobalCommands.SaveAs ) )
					oViewSite.SaveDocument( fAtNewLocation: true );
            } else {
                // TODO: I should couple my alerts with a message box and log the error.
                MessageBox.Show( "First set the central window focus to a " +
                                 "document you wish to save. Thanks!", 
                                 "I'm confused! ^_^;" );
            }
        }

        /// <summary>
        /// We need to re-set the menu title for any other view open on 
        /// the same document, since we number the sibling views.
        /// </summary>
        /// <param name="oDocSite"></param>
        public void UpdateAllTitlesFor( IDocSlot oDocSite )
        {
            foreach( ViewSlot oSiblingLine in _oDoc_ViewSelector ) {
                try {
                    if( oSiblingLine.DocumentSite == oDocSite ) {
                        oSiblingLine.UpdateTitle();
                        _oDoc_ViewSelector.Raise_AfterLineUpdate( oSiblingLine, 0, 0, 0 );
                    }
                } catch( NullReferenceException ) {
                }
            }
            SetTitle();

            _oDoc_ViewSelector.Raise_MultiFinished();
        }

        public void OnDocSaveAll( object sender, EventArgs e ) {
            Document.DocumentsSaveAll();
        }

        // A little bit of code to check if there are non-printable characters in a file.
        // Make it a command on the view of the text editor.
		//public void OnRun2( object sender, EventArgs e ) {
  //          ViewSlot oViewSite = this.ViewSiteSelected;

  //          if( oViewSite != null ) {
  //              try {

		//			if (oViewSite.DocumentSite.Document is Editor oEditor) {
		//				Editor.LineStream oStream = oEditor.CreateStream();
		//				int iPos = 0;
		//				bool fAnyErr = false;
		//				while (oStream.Seek(iPos) && oStream.InBounds(iPos)) {
		//					if ((oStream[iPos] < 32 || oStream[iPos] > 255) && oStream[iPos] != 13 && oStream[iPos] != 10) {
		//						int iOffset = 0;
		//						Line oLine = oStream.SeekLine(iPos, out iOffset);
		//						int iLine = oLine.At + 1;
		//						int iChar = oStream[iPos];
		//						oViewSite.LogError("Error", "Garbage @ " + iLine.ToString() + " " + iOffset.ToString() + " : " + iChar.ToString("d"));
		//						fAnyErr = true;
		//					}
		//					++iPos;
		//				}
		//				if (!fAnyErr) {
		//					oViewSite.LogError("OK", "Document is clean!");
		//				}
		//			}
		//		} catch( NullReferenceException ) {
  //                  oViewSite.LogError( "internal", "Bombed trying to look for non-printables" );
  //              }
  //          }
		//}

        /// <remarks>
        /// This is an example of a reason to have the sites to be defined within the
        /// MainWin class defn. OnDocDirty could be a private method which they can access
        /// instead of a internal method.
        /// </remarks>
        internal void OnDocDirtyChanged( IDocSlot oSite ) {
            if( !_fIsClosing ) {
                UpdateAllTitlesFor( oSite );
            }
        }

        /// <summary>
        /// Set our main title.
        /// </summary>
        internal void SetTitle() {
            StringBuilder sbTitle = new StringBuilder();

            sbTitle.Append( "Mjolnir" );

            if( _oSelectedWinSite != null ) {
                sbTitle.Append( " | " );
                sbTitle.Append( _oSelectedWinSite.Title );

                //this.Icon = _oSelectedWinSite.Icon;
            }

			// Followed by session name. If I've got one.
			string strSessionName = Document.SessionSlot.Title;
			if( !string.IsNullOrEmpty( strSessionName ) ) {
                sbTitle.Append( " | " );
				sbTitle.Append( strSessionName );
			}

            base.Text = sbTitle.ToString();
        }

        public string Banner {
            get { return(base.Text ); }
        }

        /// <summary>
        /// Close the view the user has currently selected. Ask if he/she wants
        /// to save first. 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        protected void OnFileClose( object sender, EventArgs e ) {
            ViewClose( this.ViewSiteSelected );
        }

        /// <summary>
        /// Close a view we have opened. It might be one of the documents we are viewing OR something like
        /// the find results, or output messeges views. Reference counting only comes from doc views and we only
        /// care to ask about asking about save in those cases anyway. 
        /// </summary>
        /// <param name="oViewSite"></param>
        internal void ViewClose( ViewSlot oViewSite ) {
            if( oViewSite != null ) {
                // Clear this since the view controlling, the lifetime of the icon, is dying.
                this.Icon = null; 

                // if doc count is 1 we ask if they want to save. This is different from the
                // view remove call which checks if there are any references left. 
                if( oViewSite.DocumentSite.IsDirty &&
                    oViewSite.DocumentSite.Reference == 1 ) 
                {
                    DialogResult eResult = MessageBox.Show( "Would you like to save your work for this document: " + 
                                                            oViewSite.DocumentSite.Title + "?",
                                                            "Caution!",
                                                            MessageBoxButtons.YesNoCancel );
                    if( eResult != DialogResult.Cancel ) {
                        if( eResult == DialogResult.Yes ) {
                            oViewSite.SaveDocument( false );
                        } else {
                            ViewRemove( oViewSite );
                        }
                    }
                } else {
                    ViewRemove( oViewSite );
                }
            } else {
                MessageBox.Show("Try setting the central window focus to a document view you wish to close.",
                                "I'm confused?! ^_^;");
            }
        }

        protected void OnFileCopyName( object sender, EventArgs e ) {
            if( _oSelectedWinSite != null ) {
                DataObject oDataObject  = new DataObject();
				try {
					oDataObject.SetData( Path.GetFileName( _oSelectedWinSite.FileName ) );
					Clipboard.SetDataObject( oDataObject );
				} catch( ArgumentException ) {
					_oSelectedWinSite.LogError( "clipboard", "Malformed file name." );
				}
            }
        }

        protected void OnFileCopyDirectory( object sender, EventArgs e ) {
            if( _oSelectedWinSite != null ) {
                DataObject oDataObject  = new DataObject();
				try {
					oDataObject.SetData( Path.GetDirectoryName( _oSelectedWinSite.FileName ) );
					Clipboard.SetDataObject( oDataObject );         
				} catch( ArgumentException ) {
					_oSelectedWinSite.LogError( "clipboard", "Malformed directory path." );
				}
            }
        }

        protected void OnFileCopyPath( object sender, EventArgs e ) {
            if( _oSelectedWinSite != null ) {
                DataObject oDataObject  = new DataObject();

                oDataObject.SetData( _oSelectedWinSite.FileName );
                Clipboard.SetDataObject( oDataObject );                
            }
        }

        protected void OnEditUnlock( object sender, EventArgs e ) {
            if (_oSelectedWinSite != null)
                _oSelectedWinSite.Execute(GlobalCommands.ReadWrite );
        }

        protected void OnEditLock( object sender, EventArgs e ) {
            if (_oSelectedWinSite != null)
                _oSelectedWinSite.Execute(GlobalCommands.ReadOnly );
        }

        protected void OnEditCut( object sender, EventArgs e ) {
            if( _oSelectedWinSite != null )
                _oSelectedWinSite.Execute( GlobalCommands.Cut );
        }

        protected void OnEditCopy( object sender, EventArgs e ) {
            if( _oSelectedWinSite != null )
                _oSelectedWinSite.Execute( GlobalCommands.Copy );
        }

        protected void OnEditPaste( object sender, EventArgs e ) {
            if( _oSelectedWinSite != null )
                _oSelectedWinSite.Execute( GlobalCommands.Paste );
        }

        protected void OnEditPasteAsImg( object sender, EventArgs e ) {
            if( _oSelectedWinSite != null )
                _oSelectedWinSite.Execute( GlobalCommands.PasteAsImg );
        }

        protected void OnEditPasteAsText( object sender, EventArgs e ) {
            if( _oSelectedWinSite != null )
                _oSelectedWinSite.Execute( GlobalCommands.PasteAsText );
        }
        protected void OnEditInsert( object sender, EventArgs e ) {
            if( _oSelectedWinSite != null )
                _oSelectedWinSite.Execute( GlobalCommands.Insert );
        }
        protected void OnEditPasteAsBase64( object sender, EventArgs e ) {
            if( _oSelectedWinSite != null )
                _oSelectedWinSite.Execute( GlobalCommands.PasteAsBase64 );
        }

        protected void OnEditDelete( object sender, EventArgs e ) {
            // Need to be extra careful on this one. B/C selected view is not the
            // same as where the focus is in the shell, but command always lands here.
            // TODO: Need to sort out all these commands.
            if( _oSelectedWinSite != null && _oSelectedWinSite.Focused )
                _oSelectedWinSite.Execute( GlobalCommands.Delete );
        }

        protected void OnCollect( object sender, EventArgs e ) {
            if( _oSelectedWinSite != null ) {
                _oSelectedWinSite.Execute( GlobalCommands.Copy );
                try {
                    Document.ScrapBookCopyClipboard();

                    ViewSlot oViewSite = _oDoc_ViewSelector.FindFirstView( Document.ScrapBookSlot );

                    if( oViewSite == null ) {
                        ViewCreate( Document.ScrapBookSlot, Guid.Empty, EditorShowEnum.SILENT ); // Want to open but NOT set focus. ^_^;
                    }
                } catch( NullReferenceException ) {
                    LogError( null, "shell", "Couldn't collect object to scrapbook." );
                }
            }
        }

        protected void ViewSelectPrevious() {
            try {
                int iLine = 0;
                if( _oDoc_ViewSelector.Find( _oSelectedWinSite, out iLine ) ) {
                    if( --iLine < 0 ) {
                        // By virtue of finding the view in the collection we are assured
                        // element count is always greater than zero.
                        iLine = _oDoc_ViewSelector.ElementCount - 1;
                    }
                    ViewSlot oLine = _oDoc_ViewSelector[iLine];
                    ViewSelect( oLine, true );
                }
            } catch( NullReferenceException ) {
                LogError( null, "shell", "Unexpected null reference while focusing previous view" );
            }
        }

        protected void ViewSelectNext() {
            try {
                if( _oDoc_ViewSelector.Find( _oSelectedWinSite, out int iLine ) ) {
                    // Find the next view on the same document.
                    for( int iView = 0; iView<_oDoc_ViewSelector.ElementCount; ++iView ) {
                        iLine = ++iLine % _oDoc_ViewSelector.ElementCount;
                        ViewSlot oLine = _oDoc_ViewSelector[iLine];
                        if( oLine.DocumentSite == _oSelectedDocSite ||
                            Keyboard.IsKeyDown( Keys.LShiftKey ) ||
                            Keyboard.IsKeyDown( Keys.RShiftKey ) ) { 
                            ViewSelect( oLine, true );
                            break;
                        }
                    }
                }
            } catch( NullReferenceException ) {
                LogError( null, "shell", "Unexpected null reference while focusing next view" );
            }
        }

        protected void ViewSelectNextAll() {
            try {
                if( _oDoc_ViewSelector.Find( _oSelectedWinSite, out int iLine ) ) {
                    iLine = ++iLine % _oDoc_ViewSelector.ElementCount;
                    ViewSlot oLine = _oDoc_ViewSelector[iLine];
                    ViewSelect( oLine, true );
                }
            } catch( NullReferenceException ) {
                LogError( null, "shell", "Unexpected null reference while focusing next view" );
            }
        }

        protected void OnEditMoveLeft( object sender, EventArgs e ) {
            if( _oSelectedWinSite != null ) {
                if( Keyboard.IsKeyDown( Keys.LControlKey ) ||
                    Keyboard.IsKeyDown( Keys.RControlKey )    ) 
                {
                    ViewSelectPrevious();
                } else {
                    _oSelectedWinSite.Execute( GlobalCommands.StepLeft);
                }
            }
        }

        protected void OnEditMoveRight( object sender, EventArgs e ) {
            if( _oSelectedWinSite != null ) {
                if( Keyboard.IsKeyDown( Keys.LControlKey ) ||
                    Keyboard.IsKeyDown( Keys.RControlKey )    ) 
                {
                    ViewSelectNext();
                } else {
                    _oSelectedWinSite.Execute( GlobalCommands.StepRight);
                }
            }
        }

        protected void OnEditJumpParent( object sender, EventArgs e ) {
            if( _oSelectedWinSite != null ) {
                _oSelectedWinSite.Execute( GlobalCommands.JumpParent);
            }
        }

        protected void OnEditPlay( object sender, EventArgs e ) {
            if( _oSelectedWinSite != null ) {
                _oSelectedWinSite.Execute( GlobalCommands.Play);
            }
        }

        protected void OnEditPause( object sender, EventArgs e ) {
            if( _oSelectedWinSite != null ) {
                _oSelectedWinSite.Execute( GlobalCommands.Pause );
            }
        }

        protected void OnEditStop( object sender, EventArgs e ) {
            if( _oSelectedWinSite != null ) {
                _oSelectedWinSite.Execute( GlobalCommands.Stop );
            }
        }

        protected void OnEditJumpPrev( object sender, EventArgs e ) {
            if( _oSelectedWinSite != null ) {
                _oSelectedWinSite.Execute( GlobalCommands.JumpPrev);
            }
        }

        protected void OnEditJumpNext( object sender, EventArgs e ) {
            if( _oSelectedWinSite != null ) {
                _oSelectedWinSite.Execute( GlobalCommands.JumpNext);
            }
        }

        protected void OnEditSelectAll( object sender, EventArgs e ) {
            if( _oSelectedWinSite != null )
                _oSelectedWinSite.Execute( GlobalCommands.SelectAll );
        }

        protected void OnEditSelectOff( object sender, EventArgs e ) {
            if( _oSelectedWinSite != null )
                _oSelectedWinSite.Execute( GlobalCommands.SelectOff );
        }

        protected void OnEditUndo( object sender, EventArgs e ) {
            if( _oSelectedWinSite != null )
                _oSelectedWinSite.Execute( GlobalCommands.Undo );
        }

        protected void OnEditFind( object sender, EventArgs e ) 
        {
            SmartHerderBase oHerder = DecorOpen( "find", true );
            if( oHerder != null ) {
                oHerder.AdornmentFocus( null ); // since find is a solo, we don't need it's view site!
            }
        }

        protected override void OnShown( EventArgs e ) {
            base.OnShown(e);
            
            LayoutFrame();
        }
        
        protected override void OnSizeChanged(EventArgs e) {
            base.OnSizeChanged(e);

			// BUG: This exposes the issue of what to do when the window gets too small.
			//      Currently the inside layou starts setting things negative. Evil.
			if( WindowState != FormWindowState.Minimized )
				LayoutFrame();

			// Note: I don't think I want to dirty the session just b/c of a size change.
			//if( !Document.SessionDirty ) {
			//	Document.SessionDirtySet();
			//	SetTitle();
			//}
        }

		protected override void OnLocationChanged(EventArgs e) {
			base.OnLocationChanged(e);

			// Note: I don't think I want to dirty the session just b/c of a loc change.
			//if( !Document.SessionDirty ) {
			//	Document.SessionDirtySet();
			//	SetTitle();
			//}
		}

        /// <summary>
        /// Find the new side for the herder/shepard. Note we can't really
        /// reuse the MeasureShepardPercents() since dragging on the bottom side
        /// to move a shepard to the left most position isn't possible since
        /// that position can intercept the left side herders and move it over
        /// there! We'll have to add some smarts to place the herder based on
        /// the center of each herder in the target side.
        /// </summary>
        /// <param name="oGuest"></param>
        protected void FinishedShepardDrag( object oGuest, SKPointI pntLast )
        {
            SmartHerderBase oShepard        = (SmartHerderBase)oGuest;
            SideIdentify    eOldOrientation = oShepard.Orientation;

            // Look in our side boxes to see where the drag ended.
            foreach( KeyValuePair<SideIdentify, SideRect> oPair in _rgSideInfo ) {
                SmartRect oRect = oPair.Value;

                if( oRect.IsInside( pntLast.X, pntLast.Y ) ) {
                    oShepard.Orientation = oPair.Key;
                    break;
                }
            }

            // Update the remaining herders in the old orientation.
            if( eOldOrientation != oShepard.Orientation ) {
				LayoutLoadShepardsAt( (SideIdentify)eOldOrientation );
				DecorShuffleSide    ( eOldOrientation ); // close side if no other decor wants it.
			}

            // Update the new target.
            LayoutLoadShepardsAt( (SideIdentify)oShepard.Orientation );
            LayoutFrame();
        }
        
        /// <summary>
        /// New call back for center dragging. This one is called constantly as the
        /// layout needs to be signaled for refresh. Even tho we basically call
        /// OnSizeChanged() for ever mouse move. I think it's nice to put the update
        /// control here versus just doing it every time in the OnMouseMove()
        /// </summary>
        /// <param name="fDragging">true if dragging, false if finished.</param>
        /// <param name="pntLast"></param>
        protected void CenterDrag( bool fDragging, SKPointI pntLast ) {
            if( !fDragging ) {
                foreach( SideIdentify eID in Enum.GetValues( typeof( SideIdentify ) ) ) {
                    if( _rgSideInfo[eID].Hidden ) {
                        foreach( IPgMenuVisibility oMenuItem in DecorSettings ) {
                            if (oMenuItem.Shepard.Orientation == eID ) {
                                oMenuItem.Checked        = false;
                                oMenuItem.Shepard.Hidden = true;
                            }
                        }
                    }
                }
            }
            OnSizeChanged( new EventArgs() );
        }

        /// <summary>
        /// 1/19/2022: Still called by the system. I'd love to convert to a Skia OnPaint version
        /// but can't yet. Maybe I can just use a control instead of a form. That's the
        /// next step!
        /// </summary>
        /// <param name="oArgs"></param>
        protected override void OnPaint( PaintEventArgs oArgs )
        {
            try {
                LayoutPaint( oArgs.Graphics );
            } catch( Exception oEx ) {
                Type[] rgErrors = { typeof( ExternalException ),
                                    typeof( NullReferenceException ) };
                if( rgErrors.IsUnhandled( oEx ) )
                    throw;

                this.LogError( null, oEx.Message, oEx.StackTrace );
            }
        }

        /// <summary>
        /// Do a hit test of the mouse position to see if it is on any
        /// edge, midpoint or corner of the inside grab handle object.
        /// </summary>
        /// <remarks>This is a little clunky. I really should have the
        /// layouts themselves do the adjustments. That way if you change
        /// layout's it'll just work. But I'm going to stick with
        /// this one for awhile.</remarks>
        protected ISmartDrag TryDrag() {
			if( _oDrag != null )
				return( _oDrag );
            
            ISmartDrag    oDrag     = null;
			SKPointI      pntCenter = _rctDragBounds.GetPoint( LOCUS.CENTER );
            SmartGrab.HIT eHitAt    = _rcFrame.IsHit(pntCenter.X, pntCenter.Y, out LOCUS uiHitEdges);

            if (eHitAt == SmartGrab.HIT.EDGE ||
                eHitAt == SmartGrab.HIT.MIDPOINT ||
                eHitAt == SmartGrab.HIT.CORNER) 
            {
                LayoutRect oGuest   = null;
                LOCUS      eNewEdge = LOCUS.EMPTY;
                TRACK      eDir     = TRACK.VERT;

                if( ( uiHitEdges & LOCUS.LEFT ) != 0 ) {
                    oGuest   = _rgSideInfo[ SideIdentify.Left ];
                    eNewEdge = LOCUS.RIGHT;
                    eDir     = TRACK.HORIZ;
                }
                if( ( uiHitEdges & LOCUS.RIGHT ) != 0 ) {
                    oGuest   = _rgSideInfo[ SideIdentify.Right ];
                    eNewEdge = LOCUS.LEFT;
                    eDir     = TRACK.HORIZ;
                }
                if( ( uiHitEdges & LOCUS.BOTTOM ) != 0 ) {
                    oGuest   = _rgSideInfo[ SideIdentify.Bottom ];
                    eNewEdge = LOCUS.TOP;
                    eDir     = TRACK.VERT;
                }

                if( oGuest == null )
                    return null;

                oDrag = new SmartDragLayout( CenterDrag, oGuest, eDir, eNewEdge, pntCenter.X, pntCenter.Y);
            } else {
				foreach( SideRect oSide in _rgSideInfo.Values ) {
					oDrag = oSide.SpacerDragTry( pntCenter.X, pntCenter.Y );
					if( oDrag != null )
						break;
				}
                if( oDrag == null ) {
                    // New section for moving herders.
                    foreach( SmartHerderBase oShepard in this ) {
                        if( oShepard.IsInside( pntCenter.X, pntCenter.Y ) ) {
                            oDrag = new SmartHerderDrag( new DragFinished( FinishedShepardDrag ), oShepard, pntCenter.X, pntCenter.Y );
                            break;
                        }
                    }
                }
            }

            return (oDrag);
        }

        /// <summary>We don't actually begin the drag until the mouse
        /// has moved a short distance from this start position.</summary>
        /// <remarks>
        /// Currently we only select herder's (adornment) windows. Want to 
        /// drag and drop them too.
        /// </remarks>
        /// <param name="e"></param>
        protected override void OnMouseDown(MouseEventArgs e) {
            base.OnMouseDown(e);
            
            if( e.Button == MouseButtons.Left ) {
                Size oDragSize = SystemInformation.DragSize;

                _rctDragBounds.SetRect( LOCUS.CENTER,
                                        (int)e.Location.X, 
                                        (int)e.Location.Y, 
                                        oDragSize.Width,
                                        oDragSize.Height );

                foreach( SmartHerderBase oShepard in this ) {
                    if( oShepard.IsInside( e.X, e.Y ) ) {
                        oShepard.AdornmentFocus( _oSelectedWinSite );
                    }
                }
                Invalidate();
            }
        }

        /// <summary>
        /// If the mousedown starts in the window (expected) but you mouse
        /// up outside the main window, we still get the event. Doesn't seem like
        /// old behavior, but clearly we rely on it here to end any drag ops.
        /// </summary>
        /// <param name="e"></param>
        protected override void OnMouseUp(MouseEventArgs e) {
            base.OnMouseUp(e);
            
            if (_oDrag != null) {
                _oDrag.Move( e.X, e.Y );
                _oDrag.Dispose();
                _oDrag = null;

                Invalidate();
            }
        }
        
        protected override void OnMouseMove(MouseEventArgs e) {
            base.OnMouseMove(e);

            this.Cursor = Cursors.Default;
            
            // If exit the drag bounds then we're actually moving something.
            if( e.Button == MouseButtons.Left && !_rctDragBounds.IsInside( e.X, e.Y ) ) {
				_oDrag = TryDrag();
			}

            if( _oDrag != null ) {
                _oDrag.Move(e.X, e.Y);
                Invalidate(); 
            } else {
                if( _rcFrame.IsInside( e.X, e.Y ) ) {
                    LOCUS eWhere = _rcFrame.Guest.IsWhere( e.X, e.Y );
                    if( eWhere == LOCUS.TOP ||
                        eWhere == LOCUS.BOTTOM )
                        this.Cursor = Cursors.SizeNS;
                    if( eWhere == LOCUS.LEFT ||
                        eWhere == LOCUS.RIGHT )
                        this.Cursor = Cursors.SizeWE;
                }
                if( _rcFrame.HoverChanged( e.X, e.Y ) ) {
                    Invalidate();
                }
				foreach( SideRect oSide in _rgSideInfo.Values ) {
					if( oSide.Hover( e.X, e.Y, out bool fChanged ) ) {
						switch( oSide.Direction ) {
							case TRACK.VERT:
								this.Cursor = Cursors.SizeNS; break;
							case TRACK.HORIZ:
								this.Cursor = Cursors.SizeWE; break;
						}
					}
					if( fChanged )
						Invalidate();
				}
            }
        }

        protected override void OnMouseLeave( EventArgs e ) {
            base.OnMouseLeave(e);

            this.Cursor = Cursors.Default;

            if( _oDrag == null ) {
                _rcFrame.HoverStop();
				foreach( SideRect oSide in _rgSideInfo.Values ) {
					oSide.HoverStop();
				}
            } else {
                _oDrag.Dispose();
                _oDrag = null;
            }
            Invalidate();
        }

        /// <summary>
        /// Add the view but DO NOT SET FOCUS to it. That's someone else's problemo.
        /// </summary>
        internal bool ViewAdd( ViewSlot oViewSite ) {
            _miViewListMenu.DropDownItems.Add( oViewSite.MenuItem );

            // Focus/Select doesn't to work till the object becomes
            // visible. At this point the object is not visible :-(
            // Setting visible to true won't work either.

            oViewSite.Guest.Parent = this;           // When called, the view gets an OnHandleCreated() call!
            oViewSite.Guest.Bounds = _rcFrame.Rect; // When called, the view gets an OnSizeChanged() call!
            oViewSite.Guest.Text = "Mjolnir - " + oViewSite.Title; // We never see this, but... ^_^;

            this.Controls.Add( oViewSite.Guest );

            _oDoc_ViewSelector.Add( oViewSite ); 

            // Since using a generic TextSite calling doc dirty doesn't change our session flag.
            // because a dirty guest document doesn't affect our session. (Unless it was embedded ^_^;)
            //_oDocSite_Session.OnDocDirty();
            SessionDirtySet( true );

            UpdateAllTitlesFor( oViewSite.DocumentSite );

            return( true );
        }

        internal void ViewRemove( ViewSlot oViewSiteToClose )
        {
            if( oViewSiteToClose == null )
                return;

            Control oViewToClose = oViewSiteToClose.Guest;

            if( !_oDoc_ViewSelector.Contains( oViewSiteToClose ) )
                return;
            if( oViewToClose == null )
                return;

            // There is no need to remove this edit win child from our Controls collection.
            // It is removed when Dispose() is called on the edit win child.
            // Remove the view from the Windows list dropdown menu.
            _miViewListMenu.DropDownItems.Remove( oViewSiteToClose.MenuItem );
            // Remove any tools/decorations dependent on the view about to be closed.
            foreach( SmartHerderBase oHerder in this ) {
                oHerder.AdornmentRemove( oViewSiteToClose );
            }

            // Save the related doc site so we can see if any other views are open.
            IDocSlot oDocSite = oViewSiteToClose.DocumentSite;

            // Clean up any addornments before the view gets disposed!!
            foreach( SmartHerderBase oHerder in this ) {
                oHerder.AdornmentRemove( oDocSite );
            }

            // This disposes of the ViewSite!! And it's contents.
            _oDoc_ViewSelector.Remove( oViewSiteToClose );

            ViewSlot oViewNext = null;
            if( _oDoc_ViewSelector.ElementCount != 0 && this.ViewSiteSelected.ID == oViewSiteToClose.ID ) {
                // Just get the top one in the list. It would be nicer if it was based on some sort of MRU.
                oViewNext = _oDoc_ViewSelector[0];
            }

            ViewSelect( oViewNext, true );

            // If the ref count on the docsite is zero it will be disposed of as well.
            Document.DocumentsClean( oDocSite );

            SessionDirtySet( true );
        }

        static int iViewSelectedCount = 0;

        /// <summary>
        /// Call this function to change which view is being controlled by the shell.
        /// We no longer call this function as focus changes on the views. If we
        /// ever allow more than one view "stacked" in the main window area and want
        /// to select one for focus, we'll have to re-think this.
        /// </summary>
        /// <param name="oViewSite">The view site to switch to.</param>
        /// <remarks>
        /// NOTE: This method can be re-entrant! If we pop up a warning dialog, that will
        /// change the current view, while we're processing a view change!!! Block that
        /// with the iViewSelectedCount. I should try using a lock.
		/// NOTE: We won't work correctly if a view is focused but not selected!!
		/// Be careful when using EditorShowEnum.SILENT
        /// </remarks>
		/// <seealso cref="OnViewFocused"/>
        public void ViewSelect( ViewSlot oViewSite, bool fFocus ) {
            try {
                iViewSelectedCount++;
                if( iViewSelectedCount > 1 )
                    return;

                if( oViewSite == null ) {
                    _oSelectedWinSite = null;
                    _oSelectedDocSite = null;
                    base.Text = "Phree Bee";
                    Icon = null;
                    InsideShow = SHOWSTATE.Inactive;
					SetTitle();
                    return;
                }

                if( _oSelectedWinSite != oViewSite )
                    ViewChanged?.Invoke(oViewSite.Guest as IPgParent ); // BUG: Guest needs to be a IPgParent.

                _oSelectedWinSite = oViewSite;
                _oSelectedDocSite = oViewSite.DocumentSite;

                // Let's not dirty the session on switch view. I'd rather dirty it on 
                // add/del view that seem more important.
				//Document.SessionDirtySet();

                SetTitle();

                if( fFocus ) 
                    InsideShow = SHOWSTATE.Focused;
                else
                    InsideShow = SHOWSTATE.Inactive; // s/b active, but I seem to find that confusing!

                // Put the check mark "on" for just our selected view.
                foreach( object oItem in _miViewListMenu.DropDownItems ) {
					// Safe way to walk this collection, you can throw anything in it.
					if( oItem is ToolStripMenuItem oMenuItem ) {
						oMenuItem.Checked = ( oMenuItem == _oSelectedWinSite.MenuItem );
					}
                }
				_oSelectedWinSite.ToolsMenuLoad( _miToolsMenu );
                ViewTypesMenuLoad( _oSelectedWinSite );

                // First put our new window to front and set the focus
                // This keeps forms from takking focus from old window that had the focus
                // and assigning it to any of it's children when parent gets hidden...
				_oSelectedWinSite.BringToFront();

                if( fFocus )
                    _oSelectedWinSite.SetFocus();

                // Now with the focus on the new client go ahead and hide everyone
                // else for good measure.
				for( IEnumerator<ViewSlot> oEnum = ViewEnumerator(); oEnum.MoveNext(); ) {
                    if( oEnum.Current.Guest != oViewSite.Guest ) {
					    oEnum.Current.Guest.Visible = false;
                    }
				}
				
				LayoutFrame();

                DecorShuffle();
			} catch( Exception oEx ) {
				Type[] rgErrors = { typeof( NullReferenceException ) };
				if( rgErrors.IsUnhandled( oEx ) )
					throw;

				LogError( null, "viewswitch", "Exception thrown on view switch attempt!" );
            } finally {
                iViewSelectedCount--;
                if( iViewSelectedCount < 0 )
                    iViewSelectedCount = 0;

                Invalidate();
            }
        }

        public void OnViewFocused( ViewSlot oViewSite ) {
            InsideShow = SHOWSTATE.Focused;
            SetTitle();

			// While the view decor is smart enough to notify the herder of the focus thru
			// OnFocus on the ViewSite.EventChain, it's up to the center view to notify
			// the herder's to go to "Inactive" state.
			foreach( SmartHerderBase oHerder in this ) {
				oHerder.OnBlurred();
			}
            // Formatted is losely what happened. Might want to revisit.
            _oDoc_ViewSelector.Raise_BufferEvent( BUFFEREVENTS.FORMATTED );

            Invalidate();
        }

        /// <summary>
        /// Leave the previous doc and view selected since we might just
        /// be minimized or another app is being used.
        /// </summary>
        /// <param name="oViewSite">The view site reporting this event.</param>
        /// <remarks>
        /// When a new view is selected the old selected view will
        /// send this event. It's a drag since we really only care
        /// when a view looses focus to a tool bar.</remarks>
        public void OnViewBlurred( ViewSlot oViewSite ) {
            InsideShow = SHOWSTATE.Inactive;
            _oDoc_ViewSelector.Raise_BufferEvent( BUFFEREVENTS.FORMATTED );
            Invalidate();
        }

        /// <summary>
        /// For the selected view, go to the document and find the view types it can create.
        /// </summary>
        protected void ViewTypesMenuLoad( ViewSlot oViewSite ) {
            try {
                _miViewCreate.DropDownItems.Clear();

                if( oViewSite != null ) {
                    _miViewCreate.Enabled = true;
                    foreach( IPgViewType oViewType in oViewSite.DocumentSite.ViewTypes ) {
                        _miViewCreate.DropDownItems.Add( new ViewTypeMenuItem( oViewType, new EventHandler(this.OnViewCreate)) );
                    }
                } else {
                    _miViewCreate.Enabled = false;
                }
            } catch( NullReferenceException ) {
            }
        }

		private void OnViewNextSibling(object sender, EventArgs e) {
			ViewSelectNext();
		}

        private void OnViewNextAll( object sender, EventArgs e ) {
            ViewSelectNextAll();
        }

        protected void OnViewClone( object oSender, EventArgs e ) 
        {
            if( _oSelectedWinSite != null ) {
                ViewCreate( _oSelectedWinSite.DocumentSite as IDocSlot, Guid.Empty );
            }
        }

        protected void OnViewCreate( object oSender, EventArgs e ) 
        {
            ViewTypeMenuItem ovtSender = oSender as ViewTypeMenuItem;
            try {
                ViewCreate( _oSelectedWinSite.DocumentSite as IDocSlot, ovtSender.ID );
            } catch( NullReferenceException ) {
                LogError( null, "menu", "No Menu item, or Current View" );
            }
        }

        protected void OnViewMaximize( object oSender, EventArgs e ) {
            DecorHide(); 
        }

        protected void OnViewRestore( object sender, EventArgs e ) {
            DecorShow();
        }

		protected void OnDecorToggle( object sender, EventArgs e ) {
			DecorToggle();
		}

        public ViewSlot ViewSiteSelected {
            get { return( _oSelectedWinSite ); }
        }

        public void SetFocusAtCenter() {
            if( _oSelectedWinSite != null ) {
                _oSelectedWinSite.SetFocus();
            }
        }

        /// <summary>
        /// Create a new empty document.
        /// </summary>
        /// <param name="strLangExtn">The file type to create. For example "html" </param>
        public object EditorInitNewShow( string strFileExtn ) {
            IDocSlot oDocSite = Document.DocumentCreate( strFileExtn );

            if( oDocSite == null )
                return( null );

            ViewCreate( oDocSite, Guid.Empty );

            return( oDocSite.Document );
        }
        
        /// <summary>
        /// This gets called by ViewCreate() and Load() {session}. First step of creating a view.
        /// BUG: Throw an ApplicationException, since there are two callers who don't check
        ///      the return value. But I'll have to fix up the other two that do. >_<;;
        /// </summary>
        private ViewSlot ViewCreateBase( IDocSlot oDocSlot, Guid guidViewType ) {
            ViewSlot oViewSlot = null;
            try {
                oViewSlot = new ViewSlot( this, oDocSlot, guidViewType );
            } catch( Exception oEx ) {
                Type[] rgErrors = { typeof( ArgumentNullException ), 
                                    typeof( ArgumentException ),
                                    typeof( InvalidOperationException )
                                  };
				if( rgErrors.IsUnhandled( oEx ) )
					throw;
				
                LogError( null, "internal", "Can't create view for document: " + oDocSlot.Title );
				return null;
            }

            try {
				// If the document is null (zombie) then we'll create a bookmark view for it.
				if( oDocSlot.Document == null ) {
					oViewSlot.Guest = new ViewBookmark( oViewSlot );
				} else {
					oViewSlot.Guest = (Control)oDocSlot.Controller.
									  CreateView( oViewSlot,
								         			oDocSlot.Document, 
								         			guidViewType );
				}
            } catch( Exception oEx ) {
                // Exceptions can come from within CreateView() AND .Guest assignment.
                Type[] rgErr = { typeof( ArgumentException ),
                                 typeof( ArgumentNullException ),
                                 typeof( NullReferenceException ),
								 typeof( InvalidCastException ),
                                 typeof( InvalidOperationException ) };
                if( rgErr.IsUnhandled( oEx ) ) 
					throw;

                LogError( null, "view", "unable to create new view on document : " + oDocSlot.FileName );
				// BUG: It's possible the document remains open with no view if the view create fails!
                //      6/17/2020: I've got the ViewBookmark there. I should try creating one!
                return null;
            }

            return oViewSlot;
        }

        /// <summary>
        /// This is our primary way of bringing up a view on a document. If you don't use this
		/// call, remember to shuffle the document decor after a new view is created.
        /// </summary>
        /// <returns>A viewsite from the view created to edit the given document (site)</returns>
        public ViewSlot ViewCreate( IDocSlot oDocSite, Guid guidViewType, EditorShowEnum eShow = EditorShowEnum.FOCUS ) {
            ViewSlot oViewSite = ViewCreateBase( oDocSite, guidViewType );
            if( oViewSite == null )
                return( null );

            if( !oViewSite.InitNew() ) {
                oViewSite.Dispose();
                return( null );
            }

            ViewAdd( oViewSite );

			if( eShow == EditorShowEnum.FOCUS )
				ViewSelect(oViewSite, true );

            return( oViewSite );
        }

        #region IPgMainWindow
        /// <summary>
        /// Note the "filename" my be either an actual file name OR a directory. If it is
        /// a file name we want to create a normal file reading site for it. But if it
        /// is a directory the persistance isn't the same. I still haven't sorted it all out.
        /// I need to pust the "IsFile" deal down farther I think.
        /// </summary>
        /// <param name="strFileName"></param>
        /// <param name="eShow"></param>
        /// <returns></returns>
        /// <remarks>This function is only public because the main Program document calls us, 
        /// having direct access to the MainWindow object. I should probably make this
        /// "internal" so only the program has access to the ViewSlot. The other public variant
        /// does not have this problem. And it is exposed externally as well.</remarks>
        /// <seealso cref="DocumentShow"/>
        public ViewSlot DocumentShow( string strFileName, EditorShowEnum eShow = EditorShowEnum.FOCUS ) {
            return DocumentShow( strFileName, Guid.Empty, eShow );
        }

        public IPgMainWindow.PgDisplayInfo MainDisplayInfo {
            get {
                IPgMainWindow.PgDisplayInfo oInfo = new IPgMainWindow.PgDisplayInfo();

                using( Graphics oGraphics = this.CreateGraphics() ) {
                    oInfo.pntDpi.X = oGraphics.DpiX;
                    oInfo.pntDpi.Y = oGraphics.DpiY;
                }
                oInfo.pntSize.X = Screen.PrimaryScreen.Bounds.Width;
                oInfo.pntSize.Y = Screen.PrimaryScreen.Bounds.Height;

                return oInfo;
            }
        }
        #endregion

        public int DocumentShow( string strFileName, Guid guidViewType, bool fShow ) {
            ViewSlot oViewSlot = DocumentShow( strFileName, guidViewType, fShow ? EditorShowEnum.FOCUS : EditorShowEnum.SILENT );
            
            if( oViewSlot != null )
                return (int)oViewSlot.ID;

            return -1; // the one reason uint's suck! Sigh.
        }

        protected ViewSlot DocumentShow( string strFileName, Guid guidViewType, EditorShowEnum eShow ) {
            string strExtn;

            try {
                strExtn = Path.GetExtension( strFileName );
            } catch( Exception oEx ) {
                Type[] rgErrors = { typeof( ArgumentException ),
                                    typeof( ArgumentNullException ) };

                if( rgErrors.IsUnhandled( oEx ) ) {
                    throw;
                }
                LogError( null, "file", "Malformed Filename." );

                return null;
            }

			IDocSlot oDocSlot = Document.DocumentCreate( strFileName );

			if( oDocSlot == null )
				return null;

            return ViewCreate( oDocSlot, Guid.Empty, eShow );
        }


        /// <summary>
        /// Request this window to show the documents in this list.
        /// </summary>
        public void DocumentShowAll( List<string> rgArgsClean ) {
			try {
				if( rgArgsClean.Count <= 0 )
					return;

				string[] rgExts = { ".gif", ".png", ".jpg", ".scraps", ".bmp" };

				// If the last command line doc loaded is a image, hide the decor/frame
                string strExtn = Path.GetExtension( rgArgsClean.Last<string>() );
                strExtn = strExtn?.ToLower();
				if( rgExts.Contains( strExtn ) ) {
					DecorHide();
				}

				for( int i = 0; i<rgArgsClean.Count; ++i ) {
					EditorShowEnum eShow = i == rgArgsClean.Count - 1 ? EditorShowEnum.FOCUS : EditorShowEnum.SILENT;

					DocumentShow( rgArgsClean[i], eShow );
				}
			} catch( Exception oEx ) {
				Type[] rgErrors = { typeof( NullReferenceException ),
									typeof( ArgumentException ),
									typeof( ArgumentNullException ) };
				if( rgErrors.IsUnhandled( oEx ) )
					throw;

				return;
			}
        }

        /// Return the currently active view. Might return NULL. Active view might not be currently
        /// focused!! Tool windows or Dialogs might be focused.
        public object CurrentView {
            get { 
                if( _oSelectedWinSite != null )
                    return( _oSelectedWinSite.Guest as IPgTextView );
 
                return( null );
            }

            set {
                foreach( ViewSlot oLine in _oDoc_ViewSelector ) {
                    if( oLine.Guest == value ) {
                        ViewSelect( oLine, true );
                    }
                }
            }
        }

		public void FocusCurrentView() {
			if( _oSelectedWinSite != null )
				_oSelectedWinSite.SetFocus();
		}

        public IPgRoundRobinWork CreateWorkSite() {
            return( Document.CreateWorkPlace() );
        }

        public event ViewChanged ViewChanged;

        private class ViewEnumerable : IEnumerable<IPgCommandView> {
            MainWin  _oOwner;
            IDocSlot _oSiteDoc;

            public ViewEnumerable( MainWin oOwner, IDocSlot oSiteDoc ) {
                if( oOwner == null )
                    throw new ArgumentException( "Owner parameter must not be null" );

                _oOwner   = oOwner;
                _oSiteDoc = oSiteDoc;
            }

            public IEnumerator< IPgCommandView > GetEnumerator() {
                return( _oOwner.GetViewsEnumerator( _oSiteDoc ) );
            }

            IEnumerator IEnumerable.GetEnumerator() {
                return( _oOwner.GetViewsEnumerator( _oSiteDoc ) );
            }
        }

        public IEnumerable<IPgCommandView> EnumViews( IDocSlot oSiteDoc ) {
            return( new ViewEnumerable( this, oSiteDoc ) );
        }

        private IEnumerator<IPgCommandView> GetViewsEnumerator( IDocSlot oSiteDoc ) {
            foreach( ViewSlot oLine in _oDoc_ViewSelector ) {
                if( oLine.DocumentSite == oSiteDoc ) {
					if (oLine.Guest is IPgCommandView oViewSibling)
						yield return (oViewSibling);
				}
            }
        }

        internal int AddColor(string strName, string strValue)
        { 
            return( Document.AddColor( strName, strValue ) );
        }

        /// <summary>
        /// Since this is only called to set up the grammer index references it's not going
        /// to hurt performance after the grammer is loaded.
        /// </summary>
        /// <param name="strName"></param>
        /// <returns></returns>
        internal int GetColorIndex(string strName)
        {
            return( Document.GetColorIndex( strName ) );
        }

        /// <summary>
        /// This is our IPgSave[TextWriter]; implementation.
        /// </summary>
        public bool IsDirty
        {
            get { return( Document.SessionDirty ); }
        }

        /// <summary>
        /// Right now our main window controls dirty b/c the main prog object can't call back on the window
        /// to get it to set it's titles. So I'll leave it until I sort it out.
        /// </summary>
		/// <remarks>This gets called by ViewAdd() when loading windows. Techically that's wrong.
		/// We should replace the call with a delegate, so we can ignore on load, and THEN set new delegate.</remarks>
        public void SessionDirtySet( bool fValue )
        {
            if( fValue )
                Document.SessionDirtySet();

            SetTitle();
        }

        /// <summary>
        /// Count the viewsites that have the same docsite as this one, and return
        /// this one's number in that count, if needed.
        /// </summary>
        /// <param name="oViewSite"></param>
        /// <returns></returns>
        public int ViewTitleID( ViewSlot oViewSite )
        {
            int iID    = -1;
            int iCount = 0;

            foreach( ViewSlot oLine in _oDoc_ViewSelector ) {
                try {
                    if( oLine.DocumentSite == oViewSite.DocumentSite )
                        iCount++;
                    if( oLine == oViewSite )
                        iID = iCount;
                } catch( NullReferenceException ) {
                }
            }
            //for( int i = 0; i<_rgViewSites.Count; ++i ) {
            //    ViewSite oCurViewSite = _rgViewSites[i];
            //    if( oCurViewSite.DocumentSite == oViewSite.DocumentSite )
            //        iCount++;
            //    if( oViewSite == oCurViewSite )
            //        iID = iCount;
            //}

            // If we don't have more than one, then we don't need to add the view count to the title.
            if( iCount >= 2 ) {
                return( iID );
            }

            return( -1 );
        }

        /// <summary>
        /// These are the addornment windows that are solo. That is, they apply to what ever
        /// document is currently displayed. Adornments that bind to a single document are created
        /// by that document's controller. I might need to modify this concept for my new SQL addornment.
        /// BUG: If the herder/shepard is for non-solo objects but we pass a null site, we crash!
        /// BUG: We could migrate this to the IPgCommand pattern and create these decorations on demand
        /// from the main window. Especially since the matches and alerts views aren't typically used.
        /// </summary>
        protected void InitializeSoloWindows() {
            try {
				if( _oTopMenu != null ) {
					_oTopMenu.Dock        = DockStyle.None;
					_oTopMenu.AutoSize    = false;
					_oTopMenu.LayoutStyle = ToolStripLayoutStyle.Flow;
					_oTopMenu.CanOverflow = false;
                    _oTopMenu.Parent      = this;

					//DecorAdd( "menu", _oTopMenu );
				} else {
					LogError( null, "Main Window", "Top menu was not created!" );
				}

                // BUG: This is a little bit of a problem since the docslot is hosted by the program but the
                // view is on the main window. So we're not using the best controller for the view creation.
				DecorSlot oAlertsSite = new DecorSlot( this, Document.AlertSlot, Shepardfind( "alerts" ) );
                oAlertsSite.ViewCreate( Guid.Empty );
				oAlertsSite.InitNew();
				DecorAdd( "alerts", oAlertsSite.Guest );

                // BUG: We're using a general controller from the program on the InternalSlot, what we really
                // need is a specialized controller for these internal views. Because this view is just defaulting
                // to the standard EditWin. I'd like it to be an EditWindow2, but I can't change the current controller
                // without effecting views I don't want to change. SeeAlso the "alerts" above.
				DecorSlot oResultsSite = new DecorSlot( this, Document.ResultsSlot, Shepardfind( "matches" ) );
                oResultsSite.ViewCreate( Program.MatchesView );
                oResultsSite.InitNew();
                DecorAdd( "matches", oResultsSite.Guest );

				DecorSlot oFindSite = new DecorSlot( this, Document.FindSlot, Shepardfind( "find" ) );
                oFindSite.ViewCreate( Program.FindDialog );
				oFindSite.InitNew();
				DecorAdd( "find", oFindSite.Guest );

                DecorSlot oSelectorSite = new ViewSelectorSlot(this, _oDoc_ViewSelector.Site as IDocSlot, Shepardfind( "views" ));
                oSelectorSite.ViewCreate( Program.ViewSelector );
                oSelectorSite.InitNew();
                DecorAdd( "views", oSelectorSite.Guest);

                DecorSlot oClockSite = new DecorSlot( this, Document.ClockSlot, Shepardfind( "clock" ) );
                oClockSite.ViewCreate( Program.Clock );
                oClockSite.InitNew();
                DecorAdd( "clock", oClockSite.Guest);

            } catch( Exception oEx ) {
				Type[] rgErrors = { typeof( ArgumentNullException ),
									typeof( ArgumentException ),
									typeof( NullReferenceException ),
									typeof( ApplicationException ),
									typeof( InvalidCastException ) };

                if( rgErrors.IsUnhandled( oEx ) )
                    throw;

                LogError( null, "internal", "Coundn't add one of the adornments." );
				// We're probably leak'n in this case. But it's not normal and I'm not going to worry about cleanup here.
            }
        }

        /// <remarks>
		/// At this point pretty satisfied with the two step window load. Any controller implementation
        /// could easily deal with the two step initialization of a window. Mainly the goal is to have
        /// the window to be usable even if the more complex Load() fails. And if InitNew() fails we're
        /// probably in real bad shape anyway. ^_^;;
		/// </remarks>
        public bool InitNew() {
            //Document.SearchSlot.InitNew();

            InitializeSoloWindows();

            Document.RecentsAddListener( new MainWin_Recent( this ) );
            Document.EventUpdateTitles += UpdateAllTitlesFor;

			ViewSlot oLastSlot = null;

			// If for some reason we can't find persistance data for the main win, but we've got
			// some documents. Then we'll just create a default view for any doc's we find.
			foreach( IDocSlot oDocSlot in Document.DocSlots ) {
				try {
					if( oDocSlot.Document == null )
						continue;

					ViewSlot oViewSite = ViewCreateBase( oDocSlot, Guid.Empty );

					if( oViewSite.InitNew() ) {
						ViewAdd( oViewSite );
						oLastSlot = oViewSite;
					} else {
						oViewSite.Dispose();
					}
				} catch( NullReferenceException ) {
				}
			}

			ViewSelect( oLastSlot, true );

            SetTitle();

            return( true );
        }

		/// <summary>
		/// 12/3/2018 : New way of loading session.
		/// </summary>
		public new bool Load( XmlElement xmlWinRoot ) {
			void LogError( Exception oEx, string strMessage ) {
				Type[] rgErrors = { typeof( XPathException ),
									typeof( XmlException ),
									typeof( FormatException ),
									typeof( ArgumentException ),
									typeof( NullReferenceException ),
									typeof( InvalidOperationException ),
									typeof( ArgumentOutOfRangeException ) };
				if( rgErrors.IsUnhandled( oEx ) )
					throw oEx;

                this.LogError( null, "window session", strMessage );
			}

            InitializeSoloWindows();

            Document.RecentsAddListener( new MainWin_Recent( this ) );
            Document.EventUpdateTitles += UpdateAllTitlesFor;

			try {
				if( xmlWinRoot.SelectSingleNode( "Location" ) is XmlElement xmlLocation ) {
					int x = int.Parse( xmlLocation.GetAttribute( "x" ) );
					int y = int.Parse( xmlLocation.GetAttribute( "y" ) );
					int width  = int.Parse( xmlLocation.GetAttribute( "width" ) );
					int height = int.Parse( xmlLocation.GetAttribute( "height" ) );

					// Check for nonsensical values, this happens. >:-(
					if( x < 0 || x > 4096 )
						x = 100;
					if( y < 0 || y > 2160 )
						y = 100;
					if( width < 100 || width > 2000 )
						width  = 500;
					if( height < 100 || height > 2000 )
						height = 500;

					this.Location = new Point( x, y );
					this.Size     = new Size ( width, height );
				}
			} catch( Exception oEx ) {
				LogError( oEx, "Problem reading main window position");
			}

            ViewSlot oFocusedWinSite = null;

			foreach( IDocSlot oDocSlot in Document.DocSlots ) {
				int iBirthCount = 0;
				try {
					XmlNodeList rgXmlViews = xmlWinRoot.SelectNodes( "Views/View[@docid='" + oDocSlot.ID.ToString() + "']");

					foreach( XmlElement xmlView in rgXmlViews ) {
						Guid     oViewGuid = Guid.Parse( xmlView.GetAttribute( "guid" ) );
						ViewSlot oViewSite = ViewCreateBase( oDocSlot, oViewGuid );

						if( oViewSite.Load( xmlView ) ) {
							if( xmlView.GetAttribute( "Focused" ) == "true" )
								oFocusedWinSite = oViewSite;

							ViewAdd( oViewSite );
							iBirthCount++;
						} else {
							oViewSite.Dispose();
						}
					}
				} catch( Exception oEx ) {
					LogError( oEx, "Error loading views from session" );
				}
				try {
					// Unloaded sites become zombies so we'll save the reference.
					if( iBirthCount == 0 ) {
						if( oDocSlot.Document != null ) {
							this.LogError( null, "mainwin session", "Couldn't find saved view for saved document, creating one default." );
						}
						// If I couldn't load anything from the saved state. Just create one new view.
						ViewSlot oViewSite = ViewCreateBase( oDocSlot, Guid.Empty );
						if( oViewSite.InitNew() ) {
							ViewAdd( oViewSite );
						} else {
							oViewSite.Dispose();
						}
					}
				} catch( Exception oEx ) {
					LogError( oEx, "Error loading views from session" );
				}
			}

            if( oFocusedWinSite == null ) {
                if( _oDoc_ViewSelector.ElementCount > 0 )
                    oFocusedWinSite = _oDoc_ViewSelector[0];
            }

			ViewSelect( oFocusedWinSite, true );

            if( _oSelectedWinSite != null ) {
                _oSelectedWinSite.ScrollToPrimaryEdit(); // 12/15/2015 : bs, view should do it automagically in load. 
            }

            DecorLoad   ( xmlWinRoot );
            DecorShuffle();
            SetTitle    ();

            return( true );
		}
        
        /// <summary>
        /// Save ourselves into this xml fragment.
        /// </summary>
        public bool Save( XmlDocumentFragment xmlOurRoot ) {
			void LogError( Exception oEx, string strMessage ) {
				Type[] rgErrors = { typeof( XPathException ),
									typeof( XmlException ),
									typeof( NullReferenceException ),
									typeof( InvalidOperationException ) };
				if( rgErrors.IsUnhandled( oEx ) )
					throw oEx;

                this.LogError( null, "window session", strMessage );
			}

			int        iViewCount = 0;
			XmlElement xmlViews   = null;
			
			try {
				xmlViews = xmlOurRoot.OwnerDocument.CreateElement( "Views" );
				xmlOurRoot.AppendChild( xmlViews );
			} catch( Exception oEx ) {
				LogError( oEx, "Couldn't create views subcollection" );
			}

            foreach( ViewSlot oLine in _oDoc_ViewSelector ) {
				try {
					ViewSlot   oViewSlot = oLine;
					XmlElement xmlView   = xmlOurRoot.OwnerDocument.CreateElement( "View" );

					xmlView.SetAttribute( "docid",  oViewSlot.DocumentSite.ID.ToString() );
					xmlView.SetAttribute( "guid",   oViewSlot.ViewType.ToString() );
					xmlView.SetAttribute( "order",  iViewCount.ToString() );

					if( _oSelectedWinSite == oViewSlot ) {
						xmlView.SetAttribute( "Focused", "true" );
					}

					xmlViews.AppendChild( xmlView );
					XmlDocumentFragment xmlFrag = xmlOurRoot.OwnerDocument.CreateDocumentFragment();

					if( !oViewSlot.Save( xmlFrag ) ) {
						this.LogError( null, "mainwin", "Main Window couldn't save view state." );
					}

					xmlView.AppendChild( xmlFrag );
				} catch( Exception oEx ) {
					LogError( oEx, "Trouble saving a view" );
				}
                iViewCount++;
            }

			XmlElement xmlLocation = null;
			try {
				xmlLocation = xmlOurRoot.OwnerDocument.CreateElement( "Location" );

				xmlLocation.SetAttribute( "x", Left.ToString() );
				xmlLocation.SetAttribute( "y", Top .ToString() );
				xmlLocation.SetAttribute( "width", Width.ToString() );
				xmlLocation.SetAttribute( "height", Height.ToString() );

				xmlOurRoot.AppendChild( xmlLocation );
			} catch( Exception oEx ) {
				LogError( oEx, "Couldn't create views subcollection" );
			}

            DecorSave( xmlOurRoot );

            SetTitle();

            return( true );
        }

		public IEnumerator<ViewSlot> ViewEnumerator() {
			foreach( ViewSlot oLine in _oDoc_ViewSelector ) {
				yield return oLine;
			}
		}
	}

}