using System;
using System.Reflection;
using System.Windows.Forms;
using System.IO;
using System.Xml;
using System.Xml.XPath;
using System.Runtime.InteropServices;

using SkiaSharp;

using Play.Drawing;
using Play.Interfaces.Embedding;
using Play.Rectangles;
using Play.Edit;
using Play.Forms;
using Play.Parse.Impl;
using Play.Parse;
using Play.Integration;
using Play.ImageViewer;

namespace Play.MusicWalker {

	/// <summary>
	/// New AlbumProperties derived from DocProperties instead of the old PropDoc
	/// </summary>
    public class AlbumProperties : DocProperties {
        public AlbumProperties(IPgBaseSite oSiteBase) : base(oSiteBase) {
        }
		protected class ParseLoadSlot : DocSlot,
			IParseEvents<char>
		{
			DataStream<char> _oStream;
			string           _strPropName;

			public ParseLoadSlot( AlbumProperties oDoc, DataStream<char> oStream ) : base( oDoc ) {
				_oStream = oStream ?? throw new ArgumentNullException();
			}

			public void OnMatch(ProdBase<char> oElem, int iStart, int iLength) {
				if( oElem is MemoryBinder<char> oBind ) {
					// TODO: Would be nice to do a line to line copy, but for now this will do.
					switch( oBind.Target.ID ) {
						case "pn": {
							_strPropName = _oStream.SubString( oBind.Start, iStart - oBind.Start );
							break;
							}
						case "pv": {
							string strPropValue = _oStream.SubString( oBind.Start, iStart - oBind.Start );

							_oHost.CreatePropertyPair( _strPropName, strPropValue );
							break;
							}
					}
				}
			}

			public void OnParserError(ProdBase<char> p_oMemory, int p_iStart) {
				LogError( "Property Parsing", "Couldn't parse property file." );
			}
		}

		protected void LogError( string strTitle, string strMsg ) {
			_oSiteBase.LogError( strTitle, strMsg );
		}

		/// <summary>
		/// In this case, we'll property parse a tab delimited text file and 
		/// construct the property array automagically. We're kind of doing a
		/// lot in a load and we could potentially use the task scheduler. 
		/// </summary>
		/// <remarks>BUG: Need to make sure the properties are at least accessible in the main 
		/// program grammer resources. Like "linebreaker" and "text" bnf's</remarks>
		public bool Load(TextReader oFileStream) {
			Grammer<char> oPropertyGrammar;

			Clear(); // Erase all properties/labels from the form. Starting over.

			try {
				oPropertyGrammar = (Grammer<char>)((IPgGrammers)Services).GetGrammer( "properties" );

				if( oPropertyGrammar == null ) {
					return false;
				}
			} catch ( Exception oEx ) {
                Type[] rgErrors = { typeof( NullReferenceException ),
                                    typeof( InvalidCastException ),
                                    typeof( ArgumentNullException ),
									typeof( GrammerNotFoundException ) };
                if( !rgErrors.IsUnhandled( oEx ) )
                    throw;

				LogError( "Property Parsing", "Couldn't get grammar for property assignment parsing." );
				return false;
			}

			// Raw file containing the tab separated properties.
			Editor oPropFile = new Editor( new DocSlot( this ) );
			
			if( !oPropFile.Load( oFileStream ) ) {
				LogError( "Property Parsing", "Couldn't read file for property assignment parsing." );
				return false;
			}

			State<char>         oStart  = oPropertyGrammar.FindState("start");
			DataStream<char>    oStream = oPropFile.CreateStream();
			MemoryState<char>   oMStart = new MemoryState<char>( new ProdState<char>( oStart ), null );
			ParseLoadSlot       oSlot   = new ParseLoadSlot( this, oStream );
			ParseIterator<char> oParser = new ParseIterator<char>( oStream, oSlot, oMStart );

			while( oParser.MoveNext() );

			ParsePropertyValues();

			RaiseLoadedEvent();

			return true;
		}

		/// <summary>
		/// Now that the properties have been loaded. Parse the form to hilight urls and quotes.
		/// </summary>
		/// <remarks>Something like this would be usefull on the DocProperties object.</remarks>
		public void ParsePropertyValues() {
			ParseHandlerText    oHandler = new ParseHandlerText( PropertyDoc, "text" );
			State<char>         oStart   = oHandler.Grammer.FindState("start");
			DataStream<char>    oStream  = PropertyDoc.CreateStream();
			MemoryState<char>   oMStart  = new MemoryState<char>( new ProdState<char>( oStart ), null );
			ParseIterator<char> oParser  = new ParseIterator<char>( oStream, oHandler, oMStart );

			while( oParser.MoveNext() );
		}

    }

    public class MusicWin : 
		Control,
		IPgParent,
		IPgTextView,
		IPgLoad<XmlElement>,
		IPgSave<XmlDocumentFragment>,
		IPgCommandView,
		IPgPlayStatus
	{
        readonly string          _strMusicIcon  = "MusicWalker.Content.icons8-music-24.png";
		readonly string          _strVolumeIcon = "MusicWalker.Content.volume.cur";
		readonly IPgViewSite     _oSiteView;
        readonly IPgViewNotify   _oViewEvents;

		readonly LayoutStackVertical   _rgLayoutTop   = new LayoutStackVertical() { Spacing = 5 };
		readonly LayoutStackHorizontal _rgLayoutTools = new LayoutStackHorizontal( 30, 1 ) { Spacing = 5 };

		int _iStartSong = 0;

        public MusicCollection Document        { get; }
        public ImageSoloDoc    AlbumArtCurrent { get; }
		public Editor          AlbumCurrent    { get; }
		public AlbumProperties AlbumProperties { get; } // Props for current album.

		LibraryWindow   ViewLibrary { get; }
		ImageViewSingle ViewSpeaker { get; }
		ImageViewSingle ViewAlbumArt{ get; }
		ImageViewSingle ViewSettings{ get; }
	  //ComboBox        ViewRecievers{ get; }

		public SKBitmap  Icon     { get; }
		public string    Banner   => Document.FileBase;
		public Guid      Catagory => Guid.Empty;

		public IPgParent Parentage => _oSiteView.Host;
		public IPgParent Services  => Parentage.Services;

		public bool IsDirty => ViewLibrary.IsDirty;

		public int CurrentAlbumIndex => ViewLibrary.Caret.Line;


		protected class MusicWinSlot :
			IPgBaseSite
		{
			protected readonly MusicWin _oHost;

			public MusicWinSlot( MusicWin oHost ) {
				_oHost = oHost ?? throw new ArgumentNullException();
			}

			public IPgParent Host => _oHost;

			public void LogError(string strMessage, string strDetails, bool fShow=true) {
				_oHost._oSiteView.LogError( strMessage, strDetails, fShow );
			}

			public void Notify( ShellNotify eEvent ) {
			}
		} // End class

		protected class MusicViewSlot : MusicWinSlot, IPgViewSite {
			public MusicViewSlot( MusicWin oHost ) : base( oHost ) {
			}

			public IPgViewNotify EventChain => _oHost._oSiteView.EventChain;
		}

		public class CurrentAlbum : Editor {
			public CurrentAlbum(IPgBaseSite oSite) : base(oSite) {
				new ParseHandlerText(this, "m3u");
			}

			public override WorkerStatus PlayStatus => ((MusicWin)_oSiteBase.Host).Document.PlayStatus;
		}

		public class LibraryWindow : EditWindow2 {
			MusicWin _oDoc;

			public LibraryWindow( IPgViewSite oBaseSite, MusicWin oDoc ) :
				base( oBaseSite, oDoc.Document.Albums, true, false ) 
			{
				_oDoc = oDoc ?? throw new ArgumentNullException( "Music window document must not be null." );

				if( this.ContextMenuStrip != null ) {
					this.ContextMenuStrip.Items.Add( new ToolStripMenuItem( "Queue",  null, new EventHandler( _oDoc.OnQueue  ),  Keys.Control | Keys.Q ) );
				}
			}

			protected override void Raise_Navigated( NavigationSource eSource, ILineRange oCarat ) {
				base.Raise_Navigated( eSource, oCarat );
				_oDoc.LoadCurrentAlbum();
			}

			public override void ClipboardCopyTo() {
				string     strSelection = string.Empty;
				DataObject oDataObject  = new DataObject();

				try {
					if( SelectionCount > 0 ) {
						strSelection = this.SelectionCopy();
					} else {
						try {
							strSelection = ((FileInfo)CaretPos.Line.Extra).FullName;
						} catch( InvalidCastException ) {
							strSelection = CaretPos.Line.ToString();
						}
 					}

					oDataObject.SetData( strSelection );
					Clipboard.SetDataObject( oDataObject );
				} catch( NullReferenceException ) {
				}
			}

			public FileInfo CaretFileInfo {
				get {
					return (FileInfo)CaretPos.Line.Extra;
				}
			}

			public string CaretText {
				get {
					return CaretPos.Line.ToString();
				}
			}
		}

        public MusicWin( IPgViewSite oBaseSite, MusicCollection oDoc ) {
			Document     = oDoc      ?? throw new ArgumentNullException( "Music Edit Win needs Music Document." );
			_oSiteView   = oBaseSite ?? throw new ArgumentNullException( "ViewSite must not be null for Music Window" );
			_oViewEvents = oBaseSite.EventChain ?? throw new ArgumentException( "Site must support EventChain" );

			Icon = SKImageResourceHelper.GetImageResource( Assembly.GetExecutingAssembly(), _strMusicIcon );

			AlbumArtCurrent = new ImageSoloDoc   ( new MusicWinSlot( this ));
			AlbumCurrent    = new CurrentAlbum   ( new MusicWinSlot( this ));
			AlbumProperties = new AlbumProperties( new MusicWinSlot( this ));

			ViewLibrary  = new LibraryWindow( new MusicViewSlot(this), this ) {
				Parent = this
			};
			ViewAlbumArt = new ImageViewButton( new MusicViewSlot( this ), oDoc.AlbumArtNow ) {
				Parent = this,
				CommandClick = GlobalCommands.PlayingShow
			};
			ViewSpeaker  = new ImageViewButton( new MusicViewSlot(this), oDoc.IconSpeaker ) {
				Parent = this,
				CommandWheelDown = GlobalCommands.VolumeUp,
				CommandWheelUp   = GlobalCommands.VolumeDown,
				CommandClick     = GlobalCommands.PlayToggle,
				Cursor           = GetCursor( _strVolumeIcon ) // Gets deleted on shutdown?
			};
			ViewSettings = new ImageViewSingle( new MusicViewSlot( this ), oDoc.IconSettings ) {
				Parent = this
			};
			//ViewRecievers = new ComboBox() {
			//	Parent = this, Font = ((IPgStandardUI)_oDocumentMusic.Services).FontMenu
			//};

			_rgLayoutTop.SetPoint( SET.STRETCH, LOCUS.UPPERLEFT, 0, 0 );
			_rgLayoutTop.Add(new LayoutControl( ViewLibrary, LayoutRect.CSS.None ));
		    _rgLayoutTop.Add( _rgLayoutTools );

			_rgLayoutTools.Add( new LayoutRect( LayoutRect.CSS.None ) );
			_rgLayoutTools.Add( new LayoutImageView( ViewAlbumArt, LayoutRect.CSS.Flex ) { Track = 33 } );
			_rgLayoutTools.Add( new LayoutImageView( ViewSpeaker,  LayoutRect.CSS.Flex ) { Track = 33 } );
			_rgLayoutTools.Add( new LayoutImageView( ViewSettings, LayoutRect.CSS.Flex ) { Track = 33 } );
			_rgLayoutTools.Add( new LayoutRect( LayoutRect.CSS.None ) );
		}

		protected Cursor GetCursor( string strResource ) {
			using( Stream oStream = Document.GetEmbedding( strResource ) ) {
				return new Cursor( oStream );
			}
		}

		protected void LogError( string strCatagory, string strDetails, bool fShow = true ) {
			_oSiteView.LogError( strCatagory,  strDetails, fShow );
		}

		bool _fDisposed = false;

		protected override void Dispose( bool disposing ) {
			if( disposing && !_fDisposed ) {
				ViewLibrary.LineChanged   -= ViewLibrary_LineChanged;
				Document.SongEvent -= OnSongEvent; 

				Icon           .Dispose();
				ViewLibrary    .Dispose();
				AlbumProperties.Dispose();

				_fDisposed = true;
			}

			base.Dispose( disposing );
		}

		private void ViewLibrary_LineChanged(int iLine) {
			_oSiteView.Notify( ShellNotify.DocumentDirty );
		}

		protected bool InitNewInternal() {
			Document.SongEvent += OnSongEvent; 

			// Failure is an option for these.
			ViewSpeaker    .InitNew();
			ViewAlbumArt   .InitNew();
			ViewSettings   .InitNew();
			AlbumProperties.InitNew();

			ViewLibrary.LineChanged += ViewLibrary_LineChanged;

			return true;
		}

		public bool InitNew() {
			if( !InitNewInternal() )
				return false;

			if( !ViewLibrary.InitNew() )
				return false;

			return true;
		}

		public bool Load( XmlElement xmlRoot ) {
			if( !InitNewInternal() )
				return false;

            try {
                XmlElement xmlEdit = (XmlElement)xmlRoot.SelectSingleNode( "Edit" );
				if( !ViewLibrary.Load( xmlEdit ) ) {
					LogError( "persist", "Couldn't load view state" );
				}
			} catch( Exception oEx ) {
				Type[] rgErrors = { typeof( XPathException ),
									typeof( NullReferenceException ),
									typeof( InvalidCastException ) };
				if( rgErrors.IsUnhandled( oEx ) )
					throw;

				return false;
			}

			return true;
		}

		public bool Save( XmlDocumentFragment xmlRoot ) {
			// Create a seperate element for the edit window since I might have different
			// elements save into our xml stream and I got to keep 'em apart.
			XmlDocumentFragment xmlFrag = xmlRoot.OwnerDocument.CreateDocumentFragment();
			XmlElement          xmlEdit = xmlRoot.OwnerDocument.CreateElement( "Edit" );

			if( !ViewLibrary.Save( xmlFrag ) ) {
				LogError( "persist", "Couldn't save view state." );
			}

			try {
				xmlEdit.AppendChild( xmlFrag );
				xmlRoot.AppendChild( xmlEdit );
			} catch( Exception oEx ) {
				Type[] rgErrors = { typeof( ArgumentException ),
									typeof( ArgumentNullException ),
									typeof( InvalidOperationException ),
									typeof( NullReferenceException ) };
				if( rgErrors.IsUnhandled( oEx ) )
					throw;

				return false;
			}

			return true;
		}

		protected override void OnSizeChanged( EventArgs e ) {
			base.OnSizeChanged(e);

			_rgLayoutTop.SetPoint( SET.STRETCH, LOCUS.LOWERRIGHT, Width, Height );
			_rgLayoutTop.LayoutChildren();
		}

        protected override void OnGotFocus(EventArgs e) {
            base.OnGotFocus( e );

            _oViewEvents.NotifyFocused( true );

            this.Invalidate();
        }

		private void OnQueue( object oSender, EventArgs oE ) {
            // TODO: Need to look at this. Since I basically know the ViewLibrary is pointing to the albums
            //       I can access the Document.Albums. But I'd rather access something like ViewLibrary.Document...
            //       Need to see why I can't do that.
			Document.Commands.xQueue( this.Document.Albums[ViewLibrary.Caret.Line].ToString(), StartSong:0 );
		}

		/// <summary>
		/// Track the outline carat position so when we play songs we start there.
		/// TODO: This won't worked for parsed music. But should be an easy-ish fix.
		/// </summary>
		/// <param name="iLine"></param>
		public void OnOutlineCaratMoved( int iLine ) {
			_iStartSong = iLine;
		}

		private void OnSongEvent() {
			try {
				SongCredentials oCurrentSong = Document.SongCurrent;

				if( oCurrentSong  != null &&
					oCurrentSong.AlbumIndex == ViewLibrary.Caret.Line ) 
				{
					AlbumCurrent.HighLight = AlbumCurrent[oCurrentSong.SongIndex];
				} else {
					AlbumCurrent.HighLight = null;
				}
				ViewLibrary.Invalidate();
			} catch( NullReferenceException ) {
				LogError( "player", "Couldn't syncronize playing song hilight" );
			}
		}

		public void LoadCurrentAlbum() {
			try {
				FileInfo oAlbumLineInfo = ViewLibrary.CaretFileInfo;

				LoadCurrentAlbum( oAlbumLineInfo.FullName,
								  Path.Combine( oAlbumLineInfo.DirectoryName, "album-properties.txt" ),
								  Path.Combine( oAlbumLineInfo.DirectoryName, "album.jpg" ) );
			} catch( Exception oEx ) {
				Type[] rgErrors = { typeof( InvalidCastException ),
									typeof( ArgumentException ),
									typeof( ArgumentNullException ),
									typeof( NullReferenceException ) };
				if( rgErrors.IsUnhandled( oEx ) )
					throw;

				_oSiteView.LogError( "internal", "Couldn't load current album", false );
			}
		}

		public void LoadCurrentAlbum( string strFullPath, string strPropPath, string strArtPath ) {
			try {
				using( TextReader oReader = new StreamReader( strPropPath ) ) {
					AlbumProperties.Load( oReader );
				}
			} catch( Exception oEx ) {
				if( Document.ErrorsStandardFile.IsUnhandled( oEx ) )
					throw;
				AlbumProperties.Clear();
				//_oSiteView.LogError( "internal", "Couldn't load album properties : " + strPropPath, false );
			}

			try {
				using Stream oReader2 = File.OpenRead( strArtPath );
				// BUG! FileStream fails to read the UNC Path to my server \\hefty3\....
				// BUT File.OpenRead.. has no problem.
				//using Stream oReader = new FileStream( strArtPath, FileMode.Open );
				AlbumArtCurrent.Load( oReader2 );
			} catch( Exception oEx ) {
				if( Document.ErrorsStandardFile.IsUnhandled( oEx ) )
					throw;

				AlbumArtCurrent.Raise_BitmapDispose();

				//_oSiteView.LogError( "internal", "Couldn't load album art : " + strArtPath, false );
			}

			try {
				using( TextReader oReader = new StreamReader( strFullPath ) ) {
					AlbumCurrent.Load( oReader );

					OnSongEvent(); // Check if we've got a hilight on the current song.
				}
			} catch( Exception oEx ) {
				if( Document.ErrorsStandardFile.IsUnhandled( oEx ) )
					throw;

				AlbumCurrent.Clear();

				_oSiteView.LogError( "internal", "Couldn't load album songs : " + strFullPath, fShow:false );
			}
		}

		public virtual object Decorate( IPgViewSite oBaseSite, Guid sGuid ) {
            if( sGuid.Equals( GlobalDecorations.Outline ) ) {
                return new MusicAlbumDecor( oBaseSite, this );
            }
			if( sGuid.Equals( GlobalDecorations.Properties ) ) {
				return new WindowStandardProperties( oBaseSite, AlbumProperties );
			}

			return false;
        }

		// https://docs.microsoft.com/en-us/windows/desktop/inputdev/wm-appcommand
        private const int APPCOMMAND_VOLUME_MUTE = 0x80000;
        private const int APPCOMMAND_VOLUME_UP   = 0xA0000;
        private const int APPCOMMAND_VOLUME_DOWN = 0x90000;
        private const int WM_APPCOMMAND = 0x319;

        [DllImport("user32.dll")]
        public static extern IntPtr SendMessageW( IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam );

		/// <remarks>Play command delegates exposed to the view seems a little weird.
		/// Make sure to implement the shell notify media event on the local and remote
		/// delegate implmentation. Since the remote stuff doesn't work, I'm not
		/// going to worry about it for now. 5/22/2022.</remarks>
		public virtual bool Execute( Guid sGuid ) {
			if( sGuid == GlobalCommands.Play ) {
				try {
					Document.Commands.xPlay( ViewLibrary.CaretText, _iStartSong );
				} catch( NullReferenceException ) {
					return false;
				}
				return true;
			}
			if( sGuid == GlobalCommands.Pause ) {
				Document.Commands.xPause();
				return true;
			}
			if( sGuid == GlobalCommands.Stop ) {
				Document.Commands.xStop();
				return true;
			}
			if( sGuid == GlobalCommands.PlayingShow ) {
				if( Document.SongCurrent != null ) {
					ViewLibrary.SelectionSet( Document.SongCurrent.AlbumIndex, iOffset:0, iLength:0 );
					ViewLibrary.ScrollToCaret();
				}
				return true;
			}
			if( sGuid == GlobalCommands.VolumeUp ) {
				SendMessageW( Handle, WM_APPCOMMAND, Handle, (IntPtr)APPCOMMAND_VOLUME_UP );
				return true;
			}
			if( sGuid == GlobalCommands.VolumeDown ) {
				SendMessageW( Handle, WM_APPCOMMAND, Handle, (IntPtr)APPCOMMAND_VOLUME_DOWN );
				return true;
			}
			if( sGuid == GlobalCommands.PlayToggle ) {
				switch( Document.PlayStatus ) {
					case WorkerStatus.BUSY:
						Document.Commands.xPause();
						break;
					case WorkerStatus.PAUSED:
						Document.Commands.xStart();
						break;
				}
				return true;
			}

			return false;
		}

		public TextPosition Caret    => ViewLibrary.Caret;
		public object       DocumentText => Document.Albums;

        public bool IsPlaying { get { 
			switch( Document.PlayStatus ) {
				case WorkerStatus.BUSY:
					return true;
				case WorkerStatus.PAUSED:
					return true;
			}
			return false;
		} }

        public SKColor BusyLight { get {
			switch( Document.PlayStatus ) {
				case WorkerStatus.BUSY:
					return SKColors.LightGreen;
				case WorkerStatus.PAUSED:
					return SKColors.Yellow;
			}
			return SKColors.Empty;
		} }

        public int PercentCompleted => throw new NotImplementedException();

        public void ScrollTo(EDGE eEdge) {
			ViewLibrary.ScrollTo( eEdge );
		}

		public void ScrollToCaret() {
			ViewLibrary.ScrollToCaret();
		}

		public bool SelectionSet(int iLine, int iOffset, int iLength) {
			return ViewLibrary.SelectionSet( iLine, iOffset, iLength );
		}

		public void SelectionClear() {
			ViewLibrary.SelectionClear();
		}
	}

}
