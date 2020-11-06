using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using System.IO;
using System.Diagnostics;
using System.Windows.Forms;
using System.Xml;
using System.Xml.XPath;
using System.Security;
using System.Reflection;
using System.Linq;

using Play.Interfaces.Embedding;
using Play.Rectangles;
using Play.Controls;
using Play.Parse.Impl;
using SkiaSharp;

namespace Play.Rectangles {
	/// <summary>
	/// It's a little bit wierd, but I can't seem to get this to work in the "Drawing" project, 
    /// where I tried including System.Windows.Forms. However, here it works? Don't know why
    /// But since anyone doing anything is going to include the "Editor" project. It's OK.
	/// </summary>
	public class LayoutControl : LayoutRect {
		Control _oControl;

		public LayoutControl( Control oView, LayoutRect.CSS eUnits, uint uiTrack ) : base( eUnits, uiTrack, 0 ) {
			_oControl = oView ?? throw new ArgumentNullException();

			this.SizeEvent += OnSizeEvent; // BUG: Looks leak worthy. ^_^;;
		}

		public LayoutControl( Control oView, LayoutRect.CSS eUnits ) : base( eUnits, 0, 0 ) {
			_oControl = oView ?? throw new ArgumentNullException();

			this.SizeEvent += OnSizeEvent;
		}

		private void OnSizeEvent(SmartRect o) {
			_oControl.Bounds = this.Rect;
		}

		public override uint TrackDesired(AXIS eParentAxis, int uiRail) {
			Size szProposed = eParentAxis == AXIS.HORIZ ? new Size( Width, uiRail ) : new Size( uiRail, Height );
			Size szPrefered = _oControl.GetPreferredSize( szProposed );
			int  iTrack     = eParentAxis == AXIS.HORIZ ? szPrefered.Width : szPrefered.Height;

			return (uint)iTrack;
		}

		public Control Guest {
			get { return _oControl; }
		}
	}
	
}

namespace Play.Edit {
    public delegate void HyperLink ( Line oLine, IPgWordRange oRange );

    public partial class EditWin : 
        Control, // Inherit from SmartRect in the future.
		IPgLoad<XmlElement>,
		IPgSave<XmlDocumentFragment>,
        IPgCommandView, 
        IPgTextView,
        IDisposable,
		IPgTools,
        ILineEvents,
        IPgSelectionHelper
    {
        static DashedPen _oPenError = new DashedPen(); // BUG: Not getting freed. But there is only one.
        
        protected readonly BaseEditor     _oDocument;   // Reference to the line editor we are using. 
        protected readonly IPgViewNotify  _oViewEvents; // Our site from the window manager (view interface).
        protected readonly IPgViewSite    _oSiteView;   // Our site from the window manager (base interface).
		protected readonly IPgStandardUI2 _oStdUI;

        protected Point _pntScreenTL = new Point( 0, 0 ); // Gets overwritten in OnSizeChanged()

        const int    _iSpacesPerTab = 4;
        bool         _fWrap         = true;
        int          _iAdvance      = 0; // Horizontal prefered position of cursor, world units.
        SmartRect    _rctDragBounds = null; // TODO: Move this into the selector.
        SizeF        _szScrollBars  = new SizeF( .1875F, .1875F );
		StdUIColors _eBgColor      = StdUIColors.BG;


        // see System.Collections.ReadOnlyCollectionBase for readonly collections.
        readonly static Keys[] _rgHandledKeys = { Keys.PageDown, Keys.PageUp, Keys.Down,
                                                  Keys.Up, Keys.Right, Keys.Left, Keys.Back,
                                                  Keys.Delete, Keys.Enter, Keys.Tab, 
                                                  Keys.Control | Keys.A, Keys.Control | Keys.F };

        readonly List<UInt32>                _rgTextColors     = new List<UInt32>(); // Text colors for the current document.
        readonly ICollection<ILineSelection> _rgSelectionTypes = new List<ILineSelection>( 3 );

        protected readonly LineRange _oLastCursor = new LineRange();
        protected CacheManager       _oCacheMan;
        protected bool               _fReadOnly;
        protected readonly Bitmap    _oIcon; // TODO: I should stick this on the document...
        protected readonly bool      _fSingleLine; // Little hack until I make single line editors.

        public Dictionary<string, HyperLink> HyperLinks { get; } = new Dictionary<string, HyperLink>();

        // These values must be updated on edits which will destroy list nodes.
        readonly TextSelector  _oTextSelector;
        protected readonly CaretPosition _oCaretPos;

      //readonly ScrollBar  _hScrollBar     = new HScrollBar();
        readonly ScrollBar2 _oScrollBarVirt = new ScrollBar2();

		PropDoc NavProps { get; }
        public event Navigation LineChanged;

		IPgAnonymousWorker    _oMorse;
		readonly List<string> _rgTools = new List<string>();
		int                   _iSelectedTool = 0;
		protected class DocSlot :
			IPgBaseSite
		{
			protected readonly EditWin _oHost;

			public DocSlot( EditWin oHost ) {
				_oHost = oHost ?? throw new ArgumentNullException();
			}

			public IPgParent Host => _oHost;

			public void LogError(string strMessage, string strDetails, bool fShow=true) {
				_oHost._oSiteView.LogError( strMessage, strDetails, fShow );
			}

			public void Notify( ShellNotify eEvent ) {
			}
		} // End class

        /// <summary>
        /// Basic setup for text viewer. You must call Load() or InitNew() to make this object usable.
        /// Make sure we don't do anything (like set event sinks) until AFTER we've
        /// called InitNew(). Else we'll blow up. Plus if the constructor fails other objects will point
		/// to this destroyed object!
        /// </summary>
		/// <exception cref="ArgumentNullException" />
		/// <exception cref="ArgumentException" />
        public EditWin( IPgViewSite oSiteView, BaseEditor p_oDocument, bool fReadOnly = false, bool fSingleLine = false ) {
			_oSiteView   = oSiteView ?? throw new ArgumentNullException( "Site must not be null!" );
			_oViewEvents = oSiteView.EventChain ?? throw new ArgumentException( "Site.EventChain must support IPgViewSiteEvents" );
 			_oStdUI      = oSiteView.Host.Services as IPgStandardUI2 ?? throw new ArgumentException( "Parent view must provide IPgStandardUI service" );

			_oDocument = p_oDocument ?? throw new ArgumentNullException();

			//Parent = oSiteView.Host as Control;

            InitializeComponent();

			NavProps = new PropDoc( new DocSlot( this ) );

            // BUG: The GetLine can crash with some custom line objects. If the document is empty and an empty
            //      line is attempted to be created but fails due to implementation error.
            _oCaretPos     = new CaretPosition( p_oDocument.GetLine(0) );
            _oTextSelector = new TextSelector( this );
            _fReadOnly     = fReadOnly;
            _fSingleLine   = fSingleLine;

			_eBgColor = fReadOnly ? StdUIColors.BGReadOnly : StdUIColors.BG;

			// https://icons8.com/
			_oIcon = ImageResourceHelper.GetImageResource( Assembly.GetExecutingAssembly(), @"Editor.Content.icon8-doc.png" );

            Array.Sort<Keys>( _rgHandledKeys );
            
            AllowDrop      = true;
            Cursor         = Cursors.IBeam;
            DoubleBuffered = true;

            _oScrollBarVirt.Dock = DockStyle.Left;
            _oScrollBarVirt.Scroll += new ScrollBarEvent( OnScrollBar ); // BUG: do on init....

            if (this.ContextMenuStrip == null) {
                ContextMenuStrip oMenu = new ContextMenuStrip();
                oMenu.Items.Add(new ToolStripMenuItem("Cut", null, new EventHandler(this.OnCut), Keys.Control | Keys.X));
                oMenu.Items.Add(new ToolStripMenuItem("Copy", null, new EventHandler(this.OnCopy), Keys.Control | Keys.C));
                oMenu.Items.Add(new ToolStripMenuItem("Paste", null, new EventHandler(this.OnPaste), Keys.Control | Keys.V));
                this.ContextMenuStrip = oMenu;
            }

            Controls.Add(_oScrollBarVirt);

			_rgTools.Add( "Edit" );
			_rgTools.Add( "Browse" );
			_rgTools.Add( "Morse" );
        }

		public IPgParent Parentage => _oSiteView.Host;
        public object    DocumentText  => _oDocument;
		public IPgParent Services  => Parentage.Services;

		public virtual WorkerStatus PlayStatus {
			get { return _oDocument.PlayStatus; }
		}

        /// <summary>
        /// Normally we'd set these values value the site. But the one case I really need this, for the
        /// find window search string, we set the site in this window, but tell the site it's guess
        /// is that of the parent of this window. Kind of messed up. So I'll stick this in here for
        /// awhile until I sort this out.
        /// </summary>
        public float ScrollBarWidths {
            set {
                _szScrollBars = new SizeF(value, value);
            }
        }

        public bool ReadOnly {
            get { return( _fReadOnly ); }
            set { _fReadOnly = value; }
        }

        /// <summary>
        /// This is kinda dumb. But we could easily overload any caller if our buffer was big
        /// enough. So we just return the first line in the document buffer.
        /// </summary>
        public override string Text {
            get {
                // Just a hack to get us off of the ground.
                return( _oDocument.GetLine(0).ToString() );
            }
        }

        // If a sub-site is generating an error and calling up we would potentially want to know 
        // which before forwarding on up. Especially if there are bunches of them. But since not, 
        // we'll just go with this.
        public void LogError( string strCatagory, string strDetails ) {
            if( _oSiteView != null ) {
                _oSiteView.LogError( strCatagory, strDetails );
            }
        }

		/// <summary>
		/// Of course this isn't valid if the cache elements haven't been measured yet.
		/// Still experimental.
		/// </summary>
		public override Size GetPreferredSize( Size oProposed ) {
			Size oSize = new Size( Width, Height );

			CacheRefresh( RefreshType.COMPLEX, RefreshNeighborhood.SCROLL );

			foreach( UniscribeCache oCache in _oCacheMan ) {
				if( oSize.Width < oCache.Width )
					oSize.Width = oCache.Width;
				if( oSize.Height < oCache.Height )
					oSize.Width = oCache.Width;
			}

			return oSize;
		}
			
		protected virtual void DecorNavPropsInit() {
			using( PropDoc.Manipulator oBulk = NavProps.EditProperties ) {
				oBulk.Add( "Line" );
				oBulk.Add( "Column" );
				oBulk.Add( "Character" );
				oBulk.Add( "Selection" );
				oBulk.Add( "Character Count" );
				oBulk.Add( "File Encoding" );
			}
		}

		protected virtual CacheManager CreateCacheManager() {
			return( new CacheManager( new CacheManSlot( this ),
									  Font, 
									  TextExtent ) );
		}

		private void OnBufferEvent(BUFFEREVENTS eEvent) {
			switch( eEvent ) {
				case BUFFEREVENTS.LOADED:
					Raise_Navigated( NavigationSource.API, _oCaretPos );
					break;
			}
		}

        /// <remarks>
        /// This object illustrates some of the problems of overriding behavior for objects that send events.
		/// If I use InitNew within Load, Load might set our carat, but InitNew would have initialized already
		/// and we send two events.
        /// </remarks>
        protected virtual bool InitNewInternal() {
			DecorNavPropsInit();

			foreach( SkiaSharp.SKColor sColor in _oStdUI.ColorsText ) {
				_rgTextColors.Add( Gdi32.SetRGB( sColor.Red, sColor.Green, sColor.Blue ) );
			}

			this.Font = _oStdUI.FontStandard;
          
			try {
				// Consider moving this into the SetWrapping() call since w/o wrapping we can dump
				// a fair amount of processing by using a different manager.
				_oCacheMan = CreateCacheManager();
			} catch( ArgumentNullException ) {
				LogError( "editor", "Unable to create CacheManager" );
				return( false );
			}

			// Kind of evil since we 'might' get called back even before we exit this proc!
			_oDocument.ListenerAdd(this);    // TODO, consider making this a normal .net event.
			_oDocument.CaretAdd(_oCaretPos); // Document moves our caret and keeps it in sync.

			_oDocument.HilightEvent += OnHighLightChanged;
			_oDocument.BufferEvent  += OnBufferEvent;

            HyperLinks.Add( "url",      OnBrowserLink );
            HyperLinks.Add( "callsign", OnCallSign );

            return ( true );
        }

        private void BrowserLink( string strUrl ) {
            try {
                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName        = strUrl,
                    UseShellExecute = true
                };
                Process.Start( psi );
            } catch( Exception oEx ) {
                Type[] rgErrors = { typeof( ObjectDisposedException ), 
                                    typeof( FileNotFoundException ),
                                    typeof( NullReferenceException ),
                                    typeof( System.ComponentModel.Win32Exception ) };
                if( rgErrors.IsUnhandled( oEx ) ) 
                    throw;
            }
        }
        
        private void OnBrowserLink( Line oLine, IPgWordRange oRange ) {
            BrowserLink( oLine.SubString( oRange.Offset, oRange.Length) );
        }

        private void OnCallSign( Line oLine, IPgWordRange oRange ) {
            BrowserLink( "http://www.qrz.com/db/" +  oLine.SubString( oRange.Offset, oRange.Length) );
        }

        public bool InitNew() {
			if( !InitNewInternal( ))
				return false;

			Raise_Navigated( NavigationSource.API, _oCaretPos );

			return true;
		}

		public bool Load( XmlElement xmlRoot ) {
            try {
				if( !InitNewInternal( ) )
					return( false );

				if (xmlRoot.SelectSingleNode("cursor") is XmlElement xmlCursor) {
					_oCaretPos.Offset = int.Parse(xmlCursor.GetAttribute("column"));
					_oCaretPos.Line   = _oDocument.GetLine(int.Parse(xmlCursor.GetAttribute("line")));

					// Note: If I have no width or height, cache loading will loose
					// it's place and we end up at the top. 
					this.ScrollToCaret();
				}
			} catch( Exception oEx ) {
				Type[] rgErrors = { typeof( XPathException ),
									typeof( NullReferenceException ),
									typeof( ArgumentNullException ),
									typeof( FormatException ) };
				if( rgErrors.IsUnhandled( oEx ) )
					throw;

				_oCaretPos.Offset = 0;
				_oCaretPos.Line   = _oDocument.GetLine( 0 );

				Raise_Navigated( NavigationSource.API, _oCaretPos );
			}
            return true;
        }

        public bool Save(XmlDocumentFragment xmlRoot )
        {
            XmlElement xmlCursor = xmlRoot.OwnerDocument.CreateElement( "cursor" );

            xmlCursor.SetAttribute( "line",   _oCaretPos.At    .ToString() );
            xmlCursor.SetAttribute( "column", _oCaretPos.Offset.ToString() );

            xmlRoot.AppendChild( xmlCursor );

            return true;
        }

        /// <summary>
        /// CR was pressed in the navigation tool window. We get our cursor back on screen and set focus to ourselves.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void Navigation_OnKeyPress(object sender, KeyPressEventArgs e)
        {
            ScrollToCaret();

            if( e.KeyChar == '\r' ) {
                this.Focus();
            }
        }

        /// <summary>
        /// Need to take some measurements. Sometimes this isn't even getting called. If that's the
        /// case we won't have our measurements we need and things will be screwy. Need to move
        /// the measurements outside of here. Note: Scroll bar width is set OnSizeChanged()
        /// </summary>
        protected override void OnHandleCreated(EventArgs e) {
            base.OnHandleCreated(e);

            if( _oCacheMan == null ) {
                // Probably user forgot to call InitNew() on us!
                return;
            }

            _pntScreenTL.X = _oScrollBarVirt.Width + 1; // Make it match where we render the cache.

            using( DisplayContext oDC = new DisplayContext( this ) ) {
                IntPtr hFont = this.Font.ToHfont();
                if( hFont != IntPtr.Zero ) {
                    using( new ItemContext( oDC.Handle, hFont ) ) {
                        _oCacheMan.InitNew( oDC.Handle, _iSpacesPerTab );
                    }
                    Gdi32.DeleteObject( hFont );
                }
            }

            ScrollBarRefresh();
            CaretIconRefreshLocation();

            Invalidate();
        }

        /// <summary>
        /// Always return true. I don't keep track of view specific data.
        /// </summary>
        /// <returns>true</returns>
        public bool IsDirty {
            get {
                return( true );
            }
        }

		public bool Wrap {
            get { return _fWrap; }
            set { _fWrap = value; }
        }

        public ICollection<ILineSelection> Selections {
            get { return( _rgSelectionTypes ); }
        }

        public bool SetFocus() {
            return( this.Focus() );
        }
        
        private void OnCut( object o, EventArgs e ) {
            ClipboardCutTo();
        }

        private void OnCopy( object o, EventArgs e )
        {
            ClipboardCopyTo();
        }

        private void OnPaste( object o, EventArgs e ) {
            ClipboardPasteFrom( Clipboard.GetDataObject(), ClipboardOperations.Default );
        }

        public void ClipboardCutTo() {
            ClipboardCopyTo();
            SelectionDelete();
        }

        public virtual void ClipboardCopyTo() {
            string     strSelection = string.Empty;
            DataObject oDataObject  = new DataObject();

			// If I've got a selection use it. Else use current caret pos. Note,
			// right mouse button press, removes any selection, moves the caret 
			// AND brings up the context menu. In the future we'll want to give the
			// option to choose the desired portion of any complex object.
			try {
				if( SelectionCount > 0 ) {
					strSelection = this.SelectionCopy();
				} else {
						IMemoryRange oSelection = FindFormattingUnderRange( _oCaretPos );
						if( oSelection != null ) {
							strSelection = _oCaretPos.Line.SubString( oSelection.Offset, oSelection.Length );
						}
 				}

				oDataObject.SetData( strSelection );
				Clipboard.SetDataObject( oDataObject );
			} catch( NullReferenceException ) {
			}
        }

        public void ClipboardPasteFrom( 
            object oDataSource, 
            Guid   sOperation ) 
        {
            if( _fReadOnly )
                return;

            if( oDataSource == null ) {
                _oSiteView.LogError( "Edit", "Data source argument null!" );
                return;
            }
            IDataObject oDataObject = oDataSource as IDataObject;
            if( oDataObject == null ) {
                _oSiteView.LogError( "Edit", "Caller must support IDataObject!" );
                return;
            }

            // TODO: This might be a dummy line. So we need dummies to be at 0.
            //       Still a work in progress. See oBulk.LineInsert() below...
            int iLine = _oCaretPos.At;

            if( sOperation == ClipboardOperations.Text ||
                sOperation == ClipboardOperations.Default 
              ) {
                if( oDataObject.GetDataPresent(typeof(System.String)) ) {
                    string strPaste = oDataObject.GetData(typeof(System.String)) as string;

                    using( Editor.Manipulator oBulk = new Editor.Manipulator( _oDocument ) ) {
                        SelectionDelete(); // TODO: Check effect of interleaved insert/delete.
                        if( _oCaretPos.Line != null ) {
                            using( TextReader oReader = new StringReader(strPaste)  ) {
                                oBulk.StreamInsert(_oCaretPos.At, _oCaretPos.Offset, oReader );
                            }
                        }
                    }
                }
                if( oDataObject.GetDataPresent(DataFormats.FileDrop) ) {
                    string[] rgFileDrop = oDataObject.GetData(DataFormats.FileDrop) as string[];

                    using( Editor.Manipulator oBulk = new Editor.Manipulator( _oDocument ) ) {
                        foreach( string strFile in rgFileDrop ) {
                            oBulk.LineInsert(iLine++, Path.GetFileName(strFile) );
                        }
                    }
                }                  
            }
            if( sOperation == ClipboardOperations.FileDrop ) {
                string[] rgFileDrop = oDataObject.GetData( DataFormats.FileDrop ) as string[];

                using( Editor.Manipulator oBulk = new Editor.Manipulator( _oDocument ) ) {
                    foreach( string strFile in rgFileDrop ) {
                        StringBuilder oNewLine = new StringBuilder();
                        oNewLine.Append( "<img src=\"" );
                        oNewLine.Append( Path.GetFileName( strFile ) );
                        oNewLine.Append( "\" " );
                        oNewLine.Append( "alt=\"hello\" " );
                        oNewLine.Append( "style=\"width:600;\" " );
                        oNewLine.Append( "/>" );

                        oBulk.LineInsert( iLine++, oNewLine.ToString() );
                    }
                }
            }
            if( sOperation == ClipboardOperations.Base64 ) {
                if( oDataObject.GetDataPresent(typeof(System.String)) ) {
                }
                if( oDataObject.GetDataPresent(DataFormats.FileDrop) ) {
                    PasteAsFileDropToBase64( oDataObject );
                }
            }
        }

        /// <summary>
        /// Convert a file drop into base64 URI! might be nice to do text too....
        /// </summary>
        protected void PasteAsFileDropToBase64( IDataObject oDataObject ) {
            if(  oDataObject == null ) 
                return;
            if( _fReadOnly )
                return;

            string[] rgFileDrop = oDataObject.GetData(DataFormats.FileDrop) as string[];
            using( Editor.Manipulator oBulk = new Editor.Manipulator( _oDocument ) ) {
                foreach( string strFile in rgFileDrop ) {
                    try {
                        string   strExtension = Path.GetExtension( strFile ).Replace( ".", string.Empty ).ToLower();
                        string[] rgExtensions = { "gif", "jpg", "jpeg", "png" };

                        if( !rgExtensions.Contains( strExtension ) )
                            break;

                        using( Stream oStream = new FileStream( strFile, FileMode.Open ) ) {
                            if( oStream.Length > 10000 ) {
                                _oSiteView.LogError( "Edit", "File is too big for inline base 64 insert: " + strFile );
                                break;
                            }
                            byte[] rgBS = new byte[oStream.Length];
                            oStream.Read( rgBS, 0, (int)oStream.Length );

                            // It's BS that I need to hand this the whole byte stream when I've got streams.
                            string strResult = Convert.ToBase64String( rgBS );

                            // BUG: Get the mime type from the file.
                            strResult = "data:image/" + 
                                        Path.GetExtension( strFile ).Replace( ".", string.Empty ) + 
                                        ";base64," + strResult;

                            using( TextReader oReader = new StringReader(strResult)  ) {
                                // BUG: Get the file extension and create scheme wrapper.
                                oBulk.StreamInsert(_oCaretPos.At, _oCaretPos.Offset, oReader );
                            }
                        }
                    } catch( Exception oEx ) {
                        Type[] rgErrors = { typeof( NotSupportedException ),      typeof( SecurityException ),
                                            typeof( FileNotFoundException ),      typeof( IOException ),
                                            typeof( DirectoryNotFoundException ), typeof( ArgumentOutOfRangeException ) };

                        _oSiteView.LogError( "Edit", "Can't complete request: " + oEx.GetType().ToString() + " for file: " + strFile );
                        if( rgErrors.IsUnhandled( oEx ) ) {
                            _oSiteView.LogError( "Edit", "Unexpected problem pasting file as base 64" );
                            throw new ApplicationException( "Can't handle file read request", oEx );
                        }
                    }
                }
            }                    
        }

        /// <summary>
        /// TODO: I've removed the "locate" event from the context menu. But I still like the idea but on
        /// the main menu, to scroll the region with the cursor on screen and find the element in any tool.
        /// This will probably be the "Find Caret" command. See about merge TextLocate and Navigate events
        /// </summary>
        private void OnLocate( object o, EventArgs e ) {
            int iStreamOffset = _oCaretPos.Line.CumulativeLength + _oCaretPos.Offset;

            Raise_Locate( iStreamOffset );
        }

        public event TextLocate Locate;

        protected virtual void Raise_Locate( int iStream ) {
			Locate?.Invoke(this, iStream);
		}

        public virtual void Raise_SelectionChanged() {
            _oDocument.Raise_BufferEvent( BUFFEREVENTS.FORMATTED );  
            // ScrollToCaret(); race and syncronization issues.
        }

        /// <summary>
        /// Refresh the carat visual position. If it's not in the range of the cached screen than
        /// park the physical cursor just off screen.
        /// </summary>
        private void CaretIconRefreshLocation() {
            Point pntCaretWorldLoc = new Point( 0, 0 ); 

            if( Focused != true )
                return;
			if( _oCacheMan == null ) // Can happen if InitNew() fails and we press forward.
				return;
                
            if( _oCacheMan.GlyphLineToPoint( _oCaretPos, out pntCaretWorldLoc ) ) {
                // this changes from world coordinates to client coordinates.
                // Check out ClientToWorld() call...
                SKPointI pntWorldTopLeft   = _oCacheMan.TextRect.GetPoint(LOCUS.UPPERLEFT);
                SKPointI pntCaretScreenLoc = new SKPointI( pntCaretWorldLoc.X - pntWorldTopLeft.X + TopLeft.X, 
                                                           pntCaretWorldLoc.Y - pntWorldTopLeft.Y + TopLeft.Y );

                User32.SetCaretPos(pntCaretScreenLoc.X, pntCaretScreenLoc.Y);
            } else {
                if( _oCacheMan.Count == 0 ) 
                    User32.SetCaretPos( _oScrollBarVirt.Width, 0 );
                else
                    User32.SetCaretPos( -10, -_oCacheMan.LineSpacing ); // Park it off screen.
            }
        }

		protected virtual void OnHighLightChanged() {
			Invalidate();
		}

        /// <summary>
        /// Offset from our world coordinates to screen (output window) coord's
        /// </summary>
        /// <param name="oCache"></param>
        /// <returns></returns>
        protected virtual PointF RenderAt( UniscribeCache oCache ) {
            return( _oCacheMan.RenderAt( oCache, _pntScreenTL ) );
        }

        /// <summary>
        /// Render all the lines at a particular color index. We print all the elements
        /// that are the same color all at the same time. The assumption is that this
        /// is quicker than changing the color as we go.
        /// </summary>
        /// <param name="hDC">IntPtr to a display context.</param>
        /// <param name="iColor">The color index to print.</param>
        /// <remarks></remarks>
        protected void RenderLines( IntPtr hDC, int iColor, bool fPaintingSelection ) {
			uint[] rgBGColorValue = { _oStdUI.ColorStandardPacked( _oDocument.PlayHighlightColor ),
									  _oStdUI.ColorStandardPacked( StdUIColors.BGWithCursor ) };
			uint   uiOldColor     = 0;
			int    iIndex         = -1;
            foreach( UniscribeCache oCache in _oCacheMan ) {
				bool[] rgBGColorKey = { _oDocument.HighLight != null && _oDocument.HighLight.At == oCache.At,
									    iColor >= 0 && oCache.At == _oCaretPos.At && !_fSingleLine };

				if( rgBGColorKey.Find( !fPaintingSelection, out iIndex ) ) {
					uiOldColor = Gdi32.SetBackColor( hDC, rgBGColorValue[iIndex] );
				}

                oCache.Render( hDC, _oCacheMan._hScriptCache, RenderAt( oCache ), iColor, null );

                if( iIndex > -1 )
                    Gdi32.SetBackColor( hDC, uiOldColor );
            }
        }

        /// <remarks>I tried painting transparent just for grins and it's very slow!
        /// BKMODES eMode = Gdi32.SetBkMode( hDC, BKMODES.TRANSPARENT );
        /// Gdi32.SetBkMode( hDC, eMode );
        /// </remarks>
        protected override void OnPaint( PaintEventArgs oE ) 
        {
            GraphicsContext oDC      = new GraphicsContext( oE.Graphics );
            IntPtr          hFont    = _oStdUI.FontStandard.ToHfont();
            ItemContext     oFontCtx = new ItemContext( oDC.Handle, hFont );
            Color           oBGColor = LOGBRUSH.CreateColor( _oStdUI.ColorStandardPacked( _eBgColor ) );

            try {
                Gdi32.SetBackColor( oDC.Handle, Gdi32.SetRGB( oBGColor.R, oBGColor.G, oBGColor.B ));

                // Render the lines one color at a time.
                for( int iColor = 0; iColor < _oStdUI.ColorsText.Count; ++iColor ) {
                    SKColor skFGColor = _oStdUI.ColorsText[iColor]; 
                    uint    uiFGColor = Gdi32.SetRGB( skFGColor.Red, skFGColor.Green, skFGColor.Blue );

                    Gdi32.SetTextColor( oDC.Handle, uiFGColor );
                    
                    RenderLines( oDC.Handle, iColor, false );
                }
                
                // Now prepare to render the selection.
                Gdi32.SetTextColor( oDC.Handle, _oStdUI.ColorStandardPacked( StdUIColors.TextSelected ) ); // White.
                if( this.Focused )
                    Gdi32.SetBackColor( oDC.Handle, _oStdUI.ColorStandardPacked( StdUIColors.BGSelectedFocus ) );
                else
                    Gdi32.SetBackColor( oDC.Handle, _oStdUI.ColorStandardPacked( StdUIColors.BGSelectedBlur ) );
                    
                foreach( ILineSelection oSelection in _rgSelectionTypes ) {
                    RenderLines( oDC.Handle, oSelection.ColorIndex, true );
                }

				_oCacheMan.RenderEOL( oDC.Handle, _rgSelectionTypes, _pntScreenTL );

                Gdi32.SetTextColor( oDC.Handle, Gdi32.SetRGB(   0,   0,   0)); // Black.
                Gdi32.SetBackColor( oDC.Handle, Gdi32.SetRGB( oBGColor.R, oBGColor.G, oBGColor.B ));

                using( new ItemContext( oDC.Handle, _oPenError.Handle ) ) {
                    _oCacheMan.RenderErrorMark( oDC.Handle, _pntScreenTL );
                }
            } catch( Exception oEx ) {
                Type[] rgErrors = { typeof( NullReferenceException ),
                                    typeof( ArgumentOutOfRangeException ),
									typeof( ArgumentNullException ),
									typeof( ArgumentException ),
									typeof( InvalidOperationException ) };
				if( rgErrors.IsUnhandled( oEx ) )
					throw;

                LogError( "Paint", "Problem painting document contents." );
            } finally {
                Gdi32.DeleteObject( hFont );
                oFontCtx.Dispose();
                oDC     .Dispose();
            }

            // Little bit of debug code to make sure the cache is the right size.
            //using( Pen oPen = new Pen( Color.Aquamarine, 3 ) ) {
            //    oE.Graphics.DrawRectangle( 
            //            oPen, 
            //            TopLeft.X,
            //            TopLeft.Y,
            //            _oCacheMan.TextRect.GetScalar(SCALAR.WIDTH ),
            //            _oCacheMan.TextRect.GetScalar(SCALAR.HEIGHT ) );
            //}
        }

		protected void PaintSpecialBackground( PaintEventArgs oPE, StdUIColors eColor, UniscribeCache oCache ) {
			try {
				using (Brush oBrush = new SolidBrush(LOGBRUSH.CreateColor(_oStdUI.ColorStandardPacked(eColor)))) {
					// TODO: Will need to add the code for left and right scrolling eventually.
					int iBufferTop = _oCacheMan.TextRect.GetScalar(SCALAR.TOP);

					Rectangle oEditRect = new Rectangle( 0,
														 oCache.Top - iBufferTop, // World to Client convert.
														 this.Width,
														 oCache.Height);
					oPE.Graphics.FillRectangle(oBrush, oEditRect);
				}
			} catch( Exception oEx ) {
                Type[] rgErrors = { typeof( NullReferenceException ),
                                    typeof( ArgumentOutOfRangeException ),
									typeof( ArgumentNullException ),
									typeof( ArgumentException ) };
				if( rgErrors.IsUnhandled( oEx ) )
					throw;

				_oSiteView.LogError( "paint", "Problem painting special backgrounds." );
			}
		}
		
		protected override void OnPaintBackground(PaintEventArgs oE) {
			try {
				// First paint our whole screen.
				using( Brush oBrush = new SolidBrush( LOGBRUSH.CreateColor( _oStdUI.ColorStandardPacked( _eBgColor ) ) ) ) {
					Rectangle oRectWhole = new Rectangle( 0, 0, this.Width, this.Height );
					oE.Graphics.FillRectangle( oBrush, oRectWhole ); 
				}
			} catch( Exception oEx ) {
                Type[] rgErrors = { typeof( NullReferenceException ),
                                    typeof( ArgumentOutOfRangeException ),
									typeof( ArgumentNullException ),
									typeof( ArgumentException ) };
				if( rgErrors.IsUnhandled( oEx ) )
					throw;

				_oSiteView.LogError( "paint", "Problem painting backgrounds." );
			}

			// Paint whole line it's special BG color.
			foreach( UniscribeCache oCache in _oCacheMan ) {
				if( _oCaretPos.Line      != null && oCache.At == _oCaretPos.At && !_fSingleLine )
					PaintSpecialBackground( oE, StdUIColors.BGWithCursor, oCache );
				if( _oDocument.HighLight != null && oCache.At == _oDocument.HighLight.At )
					PaintSpecialBackground( oE, _oDocument.PlayHighlightColor, oCache );
			}
        }

        /// <summary>
        /// We can get the focus even before we've painted for the first time.
        /// </summary>
        protected override void OnGotFocus(EventArgs e) {
            base.OnGotFocus( e );

            if( _oCaretPos.Line == null ) {
                _iAdvance = 0;
            }

            User32.CreateCaret( this.Handle, IntPtr.Zero, 2, Font.Height );
            CaretIconRefreshLocation(); 
            User32.ShowCaret  ( this.Handle );

            _oScrollBarVirt.Show( SHOWSTATE.Active );

            _oViewEvents.NotifyFocused( true );

            this.Invalidate();
        }

        protected override void OnLostFocus(EventArgs e) {
            base.OnLostFocus( e );

            User32.DestroyCaret();

            _oScrollBarVirt.Show( SHOWSTATE.Inactive );

            if( _oViewEvents != null )
                _oViewEvents.NotifyFocused( false );

            this.Invalidate();
        }

        /// <remarks>
        /// Since we track the cumulative count at the top of the cache, we never reach 100% as
        /// the percentage of the way thru the document via the top. The extent isn't precicely
        /// calculated either and so we're going to have small inaccuracies.
        /// </remarks>
        private void ScrollBarRefresh() {
            int iCharCount    = 0;
            int iStreamOffset = _oCacheMan.Count <= 0 ? 0 : int.MaxValue;
            int iLocalWidth   = 0;
            int iLocalHeight  = 1;

            foreach( UniscribeCache oCache in _oCacheMan ) {
                // Count all the buffered characters.
                iCharCount   += oCache.Line.ElementCount;
                iLocalHeight += oCache.Height;

                // Find the lowest character stream offset.
                if( iStreamOffset > oCache.Line.CumulativeLength )
                    iStreamOffset = oCache.Line.CumulativeLength;

                // In unwrapped mode we want the widest line.
                if( iLocalWidth < oCache.Width )
                    iLocalWidth = oCache.Width;
            }

            // BUG: Hacky business for setting our scroll bar height. I can do better.
            float flMultiplyer  = (float)_oCacheMan.TextRect[SCALAR.HEIGHT] / iLocalHeight;
            if( _oCacheMan.Count > 0 )
                _oScrollBarVirt.Refresh( TextShowing( iCharCount ) * flMultiplyer, TextShowing( iStreamOffset ) );
        }

        private float TextShowing( int iCount ) {
            return( _oDocument.Size > 0 ? (float)iCount / _oDocument.Size : (float)0 );
        }
        
        private void CacheRefresh( RefreshType eRefreshType, RefreshNeighborhood eNeighborhood ) {
            if( this.IsDisposed )
                return;

            // TODO: Make a callback so I won't need the font by default, only when asked for.
            //       Plus, it'll make font fallback work better!
			// Note: When we are initializing this object, our windows handle gets called on the first
			//       CreateGraphics() call made. 
            Graphics oGraphics = this.CreateGraphics();

            using( oGraphics ) {
                IntPtr hDC      = oGraphics.GetHdc();
                IntPtr hFont    = _oStdUI.FontStandard.ToHfont(); // Creates a handle we must destroy.
                IntPtr hFontOld = IntPtr.Zero;

                try {
                    if( hFont != IntPtr.Zero ) {
                        try {
                            hFontOld = Gdi32.SelectObject( hDC, hFont);
                            _oCacheMan.Refresh( hDC, eRefreshType, eNeighborhood );
                        } finally {
                            Gdi32.SelectObject( hDC, hFontOld );
                            Gdi32.DeleteObject( hFont );
                        }
                    }
                } finally {
                    oGraphics.ReleaseHdc();
                }
            }
        }

		/// <remarks>
		/// When you look at it. Refreshing the Cache after the size doesn't allow us to measure
		/// the text in case we want to set the size of the window! In the normal case, this is
		/// ok, but in the case of table layout it is a problem.
		/// </remarks>
        protected override void OnSizeChanged( EventArgs e ) {
            base.OnSizeChanged(e);

            // Probably forgot to call InitNew() on us!
            if( _oCacheMan == null ) {
				//LogError( "Editor", "Cache manager uninitialized" );
                return;
			}

            using( Graphics oGraphics = this.CreateGraphics() ) {
                int iWidth = (int)(oGraphics.DpiX * _szScrollBars.Width ); 
                _pntScreenTL = new Point(  iWidth + 1, 0 ); // Where to display the upperleft of the lines.

                _oScrollBarVirt.Width  = iWidth;
                _oScrollBarVirt.Height = this.Height;
            }

            _oCacheMan.OnViewSized( this.TextExtent );
            CacheRefresh( RefreshType.COMPLEX, RefreshNeighborhood.SCROLL );
        }

        /// <summary>
        /// Event handler for the vertical or horizontal scroll bar.
        /// </summary>
        void OnScrollBar( ScrollEvents e ) {
            switch( e ) {
                case ScrollEvents.ThumbPosition:
                    CacheRefresh( RefreshType.RESET, RefreshNeighborhood.SCROLL );
                    break;
                case ScrollEvents.ThumbTrack:
                    break;
                case ScrollEvents.First:
                case ScrollEvents.Last:
                    _oCacheMan.OnScrollBar_Vertical( e );
                    CacheRefresh( RefreshType.RESET, RefreshNeighborhood.SCROLL );
                    break;
                default:
                    _oCacheMan.OnScrollBar_Vertical( e );
                    CacheRefresh( RefreshType.COMPLEX, RefreshNeighborhood.SCROLL );
                    break;
            }
        }

        protected override void OnMouseWheel(MouseEventArgs e) {
            base.OnMouseWheel( e );
            
            _oCacheMan.OnMouseWheel( e.Delta );

            CacheRefresh( RefreshType.COMPLEX, RefreshNeighborhood.SCROLL );
        }

        /// <summary>
        /// Use this to find something to select when the user double clicks.
        /// </summary>
        /// <param name="oSearchPos">A LineRange containing the line and offset/length
        /// position use for the search.</param>
        /// <returns>Returns the formatting element under the Search position.</returns>
        protected IPgWordRange FindFormattingUnderRange( ILineRange oSearchPos ) {
            if( oSearchPos == null )
                throw new ArgumentNullException();
            if( oSearchPos.Line == null )
                return( null );

            IPgWordRange oTerminal = null;

            try { 
                foreach(IPgWordRange oRange in oSearchPos.Line.Formatting ) {
                    if( oSearchPos.Offset >= oRange.Offset &&
                        oSearchPos.Offset  < oRange.Offset + oRange.Length )
                    {
						// The first word we find is the best choice.
						if( oRange.IsWord ) {
							return oRange;
						}
						// Else the terminal under the carat is our best bet. But
                        // keep trying for better...
						if( oRange.IsTerm ) {
							oTerminal = oRange;
						}
                    }
                }
            } catch( Exception oEx ) { 
                Type[] rgErrors = { typeof( NullReferenceException ), 
                                    typeof( InvalidCastException )};
                if( rgErrors.IsUnhandled( oEx ))
                    throw;
            }

            return( oTerminal );
        }

        public Point TopLeft {
            get { return( _pntScreenTL ); }
        }

        protected Size TextExtent {
            get {
                Size sizeExtent = 
                    new Size( this.Width     - _oScrollBarVirt.Width, 
                              this.Height /* - _hScrollBar.Height */ );
                return( sizeExtent );
            }
        }

        /// <summary>
        /// Advance tells us how far along graphically, we are in the text stream
        /// from the left hand side, so if the cursor moves up or down we can try 
        /// to hit that same advance point. Updates the caret pos as a side effect.
        /// </summary>
        /// <param name="pntWorld">World Coordinates.</param>
        /// <remarks>Advance is modulo in the wrapped text case.</remarks>
        public void CaretAndAdvanceReset( Point pntWorld ) {
            _oCacheMan.CaretAndAdvanceReset( pntWorld, _oCaretPos, ref _iAdvance );
        }

        protected override void OnMouseDoubleClick( MouseEventArgs e ) {
            base.OnMouseDoubleClick(e);

            this.Select();

            CaretAndAdvanceReset( ClientToWorld( e.Location ) );

            _oTextSelector.Clear();

            // Double click, so look at the formatting, to see how much to select.
            IMemoryRange oRange = FindFormattingUnderRange( _oCaretPos );
            if( oRange != null ) {
                // TODO: This code might be better as a flyweight version of a TextSelect 
                // class. Then I could use each interchangibly depending on I want to auto 
                // select like here or manual select like in TextSelector.
                ILineSelection oSelStart = new LineRange( _oCaretPos.Line, 
                                                          oRange.Offset, 
                                                          oRange.Length, 
                                                          SelectionTypes.Start );
                _rgSelectionTypes.Add( oSelStart );

                _oDocument.CaretAdd( oSelStart ); // BUGBUG: Never get's removed. Use the TextSelector class!!!
                _oCacheMan.OnChangeFormatting( _rgSelectionTypes );
            }

            Invalidate();
            Update();
        } // end method
        
        /// <summary>
        /// Optionally initiate a drag drop operation. We only begin the drag
        /// after the user has moved the mouse outside the rectangle.
        /// </summary>
        /// <param name="e"></param>
        protected override void OnMouseDown(MouseEventArgs e) {
            base.OnMouseDown( e );
            Point pntWorldLoc = ClientToWorld( e.Location );

            this.Select();

            CaretAndAdvanceReset( pntWorldLoc );

            _rctDragBounds = null;
            
            if( ModifierKeys == Keys.Shift ) {
                // TODO: handle case when selection is empty but new
                //       cursor position is far enough to make a new selection.
                UpdateSelection( e.Location );
            } else {
                if( _oTextSelector.IsHit( pntWorldLoc ) ) {
                    if( e.Button == MouseButtons.Left ) {
                    // If there is a selection then this gets us ready to begin dragging.
                    Size oDragSize = SystemInformation.DragSize;

                    _rctDragBounds = new SmartRect( LOCUS.CENTER, 
                                                    (int)e.Location.X, 
                                                    (int)e.Location.Y, 
                                                    oDragSize.Width,
                                                    oDragSize.Height );
                    }
                } else {
                    _oTextSelector.Reset( _oCaretPos );
                }
            }
            CaretIconRefreshLocation();

            //Raise_Navigated( _oCaretPos );

            this.Invalidate();
        }

        protected Point ClientToWorld( Point pntMouse ) {
            SKPointI pntWorldTL = _oCacheMan.TextRect.GetPoint(LOCUS.UPPERLEFT);

            Point pntLocation = new Point( pntMouse.X - TopLeft.X + pntWorldTL.X,
                                           pntMouse.Y - TopLeft.Y + pntWorldTL.Y  );

            return( pntLocation );
        }

        /// <summary>
        /// Helper function to handle the mouse move and mouse up calls. If there is 
        /// a selection we move the cursor to show where the drop will occur. 
        /// </summary>
        protected void UpdateSelection( Point pntMouse ) {
            CaretAndAdvanceReset( ClientToWorld( pntMouse ) );

            _oTextSelector.NextLocation( _oCaretPos ); // Change what is selected.

            CaretIconRefreshLocation();
        }

        protected HyperLink HyperLinkFind( Point oLocation ) {
            if( _oCacheMan.GlyphPointToRange(ClientToWorld(oLocation), _oLastCursor ) != null ) {
                IPgWordRange oRange = FindFormattingUnderRange( _oLastCursor );
                if( oRange != null ) { 
                    foreach( KeyValuePair<string, HyperLink> oPair in HyperLinks ) { 
                        if( oRange.StateName == oPair.Key ) {
                            return oPair.Value;
                        }
                    }
                }
            }

            return null;
        }
        
        /// <summary>
        /// Begin the drag drop if the cursor moves outside the _rctDragging rect
        /// while the left button stays down.
        /// </summary>
        /// <remarks>
        /// We don't put anything on the clipboard during a drag drop operation.
        /// Only cut and past adds to the clipboard. As a matter of fact, if a clipboard
        /// operations occurs before our drag drop. That item will still be there after
        /// the drag drop.
        /// </remarks>
        protected override void OnMouseMove(MouseEventArgs e) {
            base.OnMouseMove( e );

            if( _iSelectedTool == 1 || ( ModifierKeys & Keys.Control ) != 0 ) {
                Cursor oNewCursor = Cursors.IBeam;
                if(_oCacheMan.GlyphPointToRange(ClientToWorld(e.Location), _oLastCursor ) != null ) {
                    IPgWordRange oRange = FindFormattingUnderRange( _oLastCursor );
                    if( oRange != null ) { 
                        foreach( KeyValuePair<string, HyperLink> oPair in HyperLinks ) { 
                            if( oRange.StateName == oPair.Key ) {
                                oNewCursor = Cursors.Hand;
                            }
                        }
                    }
                }
                Cursor = oNewCursor;
            } else { 
                if( Cursor != Cursors.IBeam )
                    Cursor = Cursors.IBeam;
            }

            if( ( e.Button & MouseButtons.Left ) == MouseButtons.Left &&
                e.Clicks == 0 ) 
            {
                if( _rctDragBounds != null ) {
                    // We have a selection and we pressed the left mouse key within it now we 
                    // are checking if mouse has moved far enough to warrent a drag drop operation.
                    if( !_rctDragBounds.IsInside( e.Location.X, e.Location.Y ) ) {
                        DataObject      oDataObject = new DataObject();
                      //DragDropEffects eEffect     = DragDropEffects.Move;

                        using( _oDocument.UndoMasterBegin() ) {
                            oDataObject.SetData( SelectionCopy() );

                            DragDropEffects eResult = DoDragDrop( oDataObject, DragDropEffects.Move );
                            if( ( eResult & DragDropEffects.Move ) != 0 ) {
                                SelectionDelete();
                            }
                        }

                        _oTextSelector.Clear();
                        Update();
                    } 
                } else {
                    // We had no selection or pressed left mouse key with the cursor outside of that 
                    // section and now we are creating a new selection.
                    UpdateSelection( e.Location );
                    Update();
                }
            }
        }

        /// <remarks>Whether we dropped or not we need to clear the drag bounds rect.
        /// So don't clear it in the OnDragDrop() event, we need to clear it
        /// here too in case we are not dragging.
        /// </remarks>
        protected override void OnMouseUp(MouseEventArgs e) {
            base.OnMouseUp( e );
            
            if( ( e.Button == MouseButtons.Left &&
                    ( _iSelectedTool == 1 ) || ( (ModifierKeys & Keys.Control) != 0) ) &&
                !TextSelector.IsSelected( Selections )
            ) {
                IPgWordRange oRange = FindFormattingUnderRange( _oLastCursor );
                HyperLink    oLink  = HyperLinkFind( e.Location );

                oLink?.Invoke(_oLastCursor.Line, oRange); 
            }

            _rctDragBounds = null;

            CaretIconRefreshLocation();

            // We ONLY navigate on the MouseUp portion of the mouse messages.
            Raise_Navigated( NavigationSource.UI, _oCaretPos );
        }

        static int LMOUSEBTN = 1;
      //static int RMOUSEBTN = 2;
        static int SHIFT     = 4;
        static int CTRL      = 8;
      //static int MMOUSEBTN = 16;
      //static int ALT       = 32;

        struct DropEffect {
            int             _iKeyState;
            DragDropEffects _eEffect;

            public DropEffect( int iKeyState, DragDropEffects eEffect ) {
                _iKeyState = iKeyState;
                _eEffect   = eEffect;
            }

            public bool IsHit( DragEventArgs oArg ) 
            {
                return( (( oArg.KeyState      & _iKeyState ) != 0 ) &&
                        (( oArg.AllowedEffect & _eEffect   ) != 0 ) );
            }

            public DragDropEffects Effect {
                get { return( _eEffect ); }
            }
        }

        DropEffect[] rgDropEffects = { //new DropEffect( CTRL + ALT, DragDropEffects.Link ), 
                                       //new DropEffect( ALT, DragDropEffects.Link ),
                                       new DropEffect( LMOUSEBTN | SHIFT, DragDropEffects.Move ),
                                       new DropEffect( LMOUSEBTN | CTRL,  DragDropEffects.Copy ),
                                       new DropEffect( LMOUSEBTN,         DragDropEffects.Move ) };

        protected override void OnDragOver( DragEventArgs oArg ) {
            base.OnDragOver(oArg);

            oArg.Effect = DragDropEffects.None;

            Point pntLocation = this.PointToClient( new Point(oArg.X, oArg.Y));
            Point pntWorldLoc = ClientToWorld( pntLocation );

            CaretAndAdvanceReset( pntWorldLoc );
            CaretIconRefreshLocation(); // BUG: We need to move the cursor when draging on ourself.
            
            // If we are both the source and target don't allow the drop if
            // we are dropping within our current selection.
            if( _oTextSelector.IsHit( pntWorldLoc ) ) {
                return;
            }

            // Can we handle the drop data type?
            if( !oArg.Data.GetDataPresent( "System.String", true) &&
                !oArg.Data.GetDataPresent( DataFormats.FileDrop, true ) ) {
                return;
            }

            // Can we handle the drop effect?
            foreach( DropEffect sDropEffect in rgDropEffects ) {
                if( sDropEffect.IsHit( oArg ) )
                {
                    oArg.Effect = sDropEffect.Effect;
                }
            }
            // Did we get anything? Narf!
        }

        /// <summary>
        /// The gods of DnD have ordained to drop onto us!
        /// </summary>
        /// <param name="oArg"></param>
        protected override void OnDragDrop( DragEventArgs oArg ) {
            base.OnDragDrop(oArg);

            if( oArg.Data.GetDataPresent(typeof(System.String)) ) {
                string item = oArg.Data.GetData(typeof(System.String)) as string;

                // Ok, I'm getting data, and I'm supposed to add it BEFORE
                // I've removed it from the source. Only a big deal if source and
                // target are the same document eh?
                if( ( oArg.Effect & DragDropEffects.Copy ) != 0 ||
                    ( oArg.Effect & DragDropEffects.Move ) != 0    )
                {
                    Point pntWorld = ClientToWorld( this.PointToClient( new Point(oArg.X, oArg.Y) ) );
                    UniscribeCache oCacheHit = null;

                    // Target's always going to be in the cache since it's the only place we can drop!
                    foreach( UniscribeCache oCache in _oCacheMan ) {
                        if( oCache.IsHit( pntWorld ) ) {
                            oCacheHit = oCache;
                            break;
                        }
                    }
                    // Got to do the insert AFTER walking the _oCacheMan b/c stream insert adds 
                    // lines to cache. See OnLineNew() callback! 
                    if( oCacheHit != null ) {
                        Line oLine = oCacheHit.Line;
                        int  iEdge = oCacheHit.GlyphPointToOffset( pntWorld );

                        // We can add in front of the EOL character.
                        Debug.Assert( iEdge <= oLine.ElementCount );

                        using( Editor.Manipulator oBulk = new Editor.Manipulator( _oDocument ) ) {
                            oBulk.StreamInsert( oLine.At, iEdge, new StringReader( item ) ); // TODO: Check line count good.
                        }
                    }
                }
            //} else if( oArg.Data.GetDataPresent(DataFormats.FileDrop) ) {
            //    string[] rgItems = oArg.Data.GetData(typeof(System.String)) as string[];
            //    MainWin  oHost   = (MainWin)_oSite.Host;

            //    if( ( oArg.Effect & DragDropEffects.Copy ) != 0 ) {
            //        foreach( string strItem in rgItems ) {
            //            oHost.CreateTextEditor( strItem );
            //        }
            //    }
            }

            this.Invalidate();
        }

        /// <summary>
        /// Select all. Doesn't invalidate window or refresh the cash, tho it does
        /// send a OnChangeFormatting() message.
        /// </summary>
        /// <remarks>If the user adds lines in another window, we should track them
        /// nicely with the new architecture. Need to give it the dual view test.</remarks>
        public void SelectionSetAll() {
            LineRange oSelectMiddle = new SelectAll();

            Selections.Clear();
            Selections.Add( oSelectMiddle );

            _oCacheMan.OnChangeFormatting( _rgSelectionTypes );

            Raise_Navigated( NavigationSource.UI, _oCaretPos );
        }

        /// <summary>
        /// This doesn't mess with any TextSelector objects but it does clear the
        /// selections that they set on the edits. Removes the selection from
        /// the caret's collection too. 
        /// </summary>
        /// <remarks>TODO: I'm not reliably removing these carets when they are no
        /// longer in use. I need a better way of clearing them. I think passing
        /// the view as an object identifier might be a better way to mark these.
        /// Then I can remove them all in one blow.</remarks>
        public void SelectionClear() {
            foreach( ILineSelection oSelect in _rgSelectionTypes ) {
                _oDocument.CaretRemove(oSelect);
            }

            _rgSelectionTypes.Clear();
            //_oTextSelector.Clear(); TODO: Text selector calls SelectionClear() chicken/egg problem.

            _oCacheMan.OnChangeFormatting( _rgSelectionTypes );

            //Raise_Navigated( _oCaretPos );
            Invalidate();
        }

        /// <summary>
        /// I HATE to say it, but this way of defining enumerators, kicks ass over trying to create
        /// some sort of FSA. Might want to move this method to the text selector class. 
        /// </summary>
        IEnumerable<ILineSelection> GetSelections() {
            Dictionary<SelectionTypes, ILineSelection> rgRanges = new Dictionary<SelectionTypes, ILineSelection>();

            foreach( ILineSelection oRange in _rgSelectionTypes ) {
                rgRanges.Add( oRange.SelectionType, oRange );
            }

            if( rgRanges.ContainsKey(SelectionTypes.Start)) 
                yield return( rgRanges[SelectionTypes.Start] );

            if( rgRanges.ContainsKey(SelectionTypes.Middle ) ) {
                SelectMiddle oMiddle = rgRanges[SelectionTypes.Middle] as SelectMiddle;

                if( oMiddle != null ) {
                    for( int iLine=oMiddle.StartLine.At + 1; iLine < oMiddle.EndLine.At; ++iLine ) {
                        oMiddle.Line = _oDocument.GetLine(iLine);
                        yield return( oMiddle );
                    }
                } else {
                    SelectAll oAll = rgRanges[SelectionTypes.Middle] as SelectAll;

                    if( oAll != null ) {
                        for( int i=0; i<_oDocument.ElementCount; ++i ) {
                            oAll.Line = _oDocument[i];
                            yield return( oAll );
                        }
                    }
                }
            }
            if( rgRanges.ContainsKey(SelectionTypes.End)) 
                yield return( rgRanges[SelectionTypes.End] );
        }

        /// <summary>
        /// This is a bit more efficient than calling GetSelections() since we don't actually
        /// need to visit all the nodes in the "selectall" case.
        /// </summary>
        protected long SelectionCount 
        {
            get {
                long iCharCount = 0;
                int  iEOLLen    = _oDocument.EOL.Length;

                foreach( ILineSelection oRange in _rgSelectionTypes ) {
                    if( oRange.SelectionType == SelectionTypes.Start ||
                        oRange.SelectionType == SelectionTypes.End ) {
                        iCharCount += oRange.Length;

                        if( oRange.IsEOLSelected )
                            iCharCount += iEOLLen;
                    }
                    if( oRange.SelectionType == SelectionTypes.Middle ) {
                        SelectMiddle oRangeSel = oRange as SelectMiddle;
                        SelectAll    oRangeAll = oRange as SelectAll;
                        if( oRangeSel != null ) {
                            // If we've got a range but it's not a SelectMiddle, that's a problem.
                            for( int i = oRangeSel.EndLine.At - 1; i>oRangeSel.StartLine.At; --i ) {
                                iCharCount += _oDocument.GetLineLength(i);
                            }
                        } else if( oRangeAll != null ) {
                             iCharCount += _oDocument.Size;
                        }
                    }
                }

                return( iCharCount );
            }
        }

        /// <summary>
        /// Build up a string based on the line selection. Multi-select not
        /// currently handled. Note: we don't care if it's a stream or column
        /// select! Both should work fine.
        /// </summary>
        /// <remarks>If any line length approaches Int32.MaxInt we might 
        /// blow up! Offset + Length needs to be less than Int32.MaxInt.
        /// Selection MIGHT include the mythical EOL character plus 1,
        /// meaning ONLY the EOL is desired. Perhaps instead I should
        /// put an property on the selection indicating EOL included instead.
        /// </remarks>
        public string SelectionCopy() {
            StringBuilder oBuilder = new StringBuilder();

            using( StringWriter oStream = new StringWriter( oBuilder ) ) {
                foreach( ILineSelection oSel in GetSelections() ) {
                    ILineSelection oRange = oSel;
                    Line           oLine  = oSel.Line; 

                    if( oRange.Offset <= oLine.ElementCount ) {
                        int  iRangeLen = oRange.Length;
                        int  iRangeOff = oRange.Offset;

                        // For the 'any' line length ranges, check before any
                        // arithmetic on the variable, else we might overflow!
                        if( iRangeLen > oLine.ElementCount )
                            iRangeLen = oLine.ElementCount;

                        if( iRangeOff < 0 )
                            iRangeOff = 0;

                        if( iRangeOff + iRangeLen > oLine.ElementCount ) {
                            iRangeLen = oLine.ElementCount - iRangeOff;
                        }

                        if( iRangeLen > 0 ) 
                            oLine.Save( oStream, iRangeOff, iRangeLen );

                        if( oRange.IsEOLSelected )
                            oStream.WriteLine();
                    }
                }

                oStream.Flush();

                if( oBuilder.Length == 0 ) {
                    if( _oCaretPos.Line != null ) {
                        _oCaretPos.Line.Save( oStream );
                    }
                }

                oStream.Flush();
            }

            return( oBuilder.ToString() );
        }

        /// <summary>
        /// Trim the end line but don't delete it even, if the whole line is selected. EOL can't be selected
        /// else it's a middle selection. Delete the middle lines first. Lastly the trim the start 
        /// which may merge with the remainder of the end which will now be trailing the start, but it's
        /// Line.At values will be wrong and must be re-computed, this usually done by the manipulator.
        /// It might be usefull to define a reverse enumerable method somewhat like GetSelection(), but
        /// of course since we are deleting lines, that makes such an enumerator trickier.
        /// </summary>
        public void SelectionDelete() {
            if( _fReadOnly ) {
                SelectionClear();
                return;
            }

            Dictionary<SelectionTypes, ILineSelection> _rgRanges = new Dictionary<SelectionTypes, ILineSelection>();
            foreach( ILineSelection oRange in _rgSelectionTypes ) {
                _rgRanges.Add( oRange.SelectionType, oRange );
            }
            using( Editor.Manipulator oBulk = _oDocument.CreateManipulator() ) {
                if( _rgRanges.ContainsKey( SelectionTypes.End ) ) {
                    ILineSelection oRange = _rgRanges[SelectionTypes.End];

                    oBulk.LineTextDelete( oRange.Line.At, oRange );
                }
                if( _rgRanges.ContainsKey(SelectionTypes.Middle ) ) {
                    SelectMiddle oRangeSel = _rgRanges[SelectionTypes.Middle] as SelectMiddle;
                    SelectAll    oRangeAll = _rgRanges[SelectionTypes.Middle] as SelectAll;
                    // BUG/TODO: If all middle selection then start line will be a middle? I can
                    //           fix this by redefining the SelectMiddle extents to be middle lines only.
                    if( oRangeSel != null ) {
                        // If we've got a range but it's not a SelectMiddle, that's a problem.
                        for( int i = oRangeSel.EndLine.At - 1; i>oRangeSel.StartLine.At; --i ) {
                            oBulk.LineDelete( i ); 
                        }
                    } else if( oRangeAll != null ) {
                         oBulk.DeleteAll();
                    }

                }
                if( _rgRanges.ContainsKey(SelectionTypes.Start ) ) {
                    ILineSelection oRange = _rgRanges[SelectionTypes.Start];

                    if( oRange.Offset < oRange.Line.ElementCount )
                        oBulk.LineTextDelete( oRange.At, oRange );
                    if( oRange.IsEOLSelected )
                        oBulk.LineMergeWithNext( oRange.At );
                }

                SelectionClear();
            } // end Bulk.

            Invalidate();
        }

        // Let Forms know what keys we want sent our way.
        protected override bool IsInputKey(Keys keyData) {
            int iIndex = Array.BinarySearch<Keys>( _rgHandledKeys, keyData );
            
            if( iIndex >= 0 )
                return( true );
                
            return base.IsInputKey( keyData );
        }

        protected virtual void Raise_Navigated( NavigationSource eSource, ILineRange oCarat ) {
            // TODO: only want to signal next two when line is actually changed.
            // Note: Currently used by FindWindow and ViewSiteLine objects.
            LineChanged?.Invoke( oCarat.At );

            _oSiteView.Notify( ShellNotify.BannerChanged );

            DecorNavigatorUpdate( eSource, oCarat );
        }

		/// <remarks>Note that List<T> throws ArgumentOutOfRangeException for the same cases 
		/// where arrays use IndexOutOfRangeException. It's a bitch I know.</remarks>
        protected virtual void DecorNavigatorUpdate( NavigationSource eSource, ILineRange oCarat ) {
            int iLine = 0, iIndex = 0, iLineCharCount = 0, iChar = 0;

            try {
                iLine          = oCarat.At + 1;
                iIndex         = oCarat.Offset;
                iLineCharCount = oCarat.Line.ElementCount;
                iChar          = oCarat.Line.ElementCount > oCarat.Offset ? oCarat.Line[oCarat.Offset] : 0;
			} catch( Exception oEx ) {
				Type[] rgErrors = { typeof( NullReferenceException ),
									typeof( IndexOutOfRangeException ),
									typeof( ArgumentOutOfRangeException ) };
				if( rgErrors.IsUnhandled( oEx ) )
					throw;

				LogError( "Nav properties", "Problem prepping data" );
            }

			using( PropDoc.Manipulator oBulk = NavProps.EditProperties ) {
				oBulk.Set( 0, iLine .ToString() + " of " + _oDocument.ElementCount.ToString() );
				oBulk.Set( 1, iIndex.ToString() + " of " + iLineCharCount.ToString() );
				oBulk.Set( 2, iChar .ToString() + ", 0x" + iChar.ToString( "x4" ) );
				oBulk.Set( 3, SelectionCount.ToString() );
				oBulk.Set( 4, _oDocument.Size.ToString() );
				oBulk.Set( 5, _oDocument.FileEncoding + " (" + _oDocument.FileStats + ")" );
			}
        }

        /// <remarks>It's a bummer that I have to call this method whenever the key's are pressed
        /// in order to have the scroll position track correctly to keep the cursor on screen.
        /// It would be nicer if it was part of the callback for the buffer change. Yet we would
        /// only update our scroll position if our view originated the keystroke. This is also
        /// a consideration if we have macro's that are view based versus document based.</remarks>
        /// <param name="iDir">-1, 1, and 0</param>
        protected void OnKeyDown_Arrows( Axis eAxis, int iDir ) {
            if( this.IsDisposed )
                return;

            _oTextSelector.Clear();

            switch( _oCacheMan.MoveCaret( eAxis, iDir, ref _iAdvance, _oCaretPos ) ) {
                case CaretMove.MISS:   // Complete miss.
                    CacheRefresh( RefreshType.RESET, RefreshNeighborhood.CARET );
                    break;
                case CaretMove.NEARBY: // Nearby but off screen.
                    CacheRefresh( RefreshType.COMPLEX, RefreshNeighborhood.CARET );
                    break;
                case CaretMove.LOCAL:  // Still on screen.
                    CaretIconRefreshLocation();
                    ScrollBarRefresh();
                    Invalidate(); // Repaints the background behind the new cursor position.
                    break;
            }

            Raise_Navigated( NavigationSource.UI, _oCaretPos );
        } // end method

        /// <summary>
        /// Paging is a vertical aspect thing. So don't need this same code for horizontal.
        /// </summary>
        protected void OnKeyDown_Page( ScrollEvents e ) {
            if( this.IsDisposed )
                return;

            _oCacheMan.OnScrollBar_Vertical( e );
            CacheRefresh( RefreshType.COMPLEX, RefreshNeighborhood.SCROLL );

            // Only care about the Y cooridnate. X we're going to override to the advance value.
            SKPointI pntWorld = _oCacheMan.TextRect.GetPoint(LOCUS.CENTER);
            pntWorld.X = _iAdvance; 

			_oCacheMan.GlyphPointToRange( new Point( pntWorld.X, pntWorld.Y ), _oCaretPos );

            CaretIconRefreshLocation();

            // While I don't REALLY know if the caret moved. It is as good as moved.
            Raise_Navigated( NavigationSource.UI, _oCaretPos );
        }

        protected void OnKeyDelete( bool fBack ) {
            if( _fReadOnly )
                return;

            if( TextSelector.IsSelected( _rgSelectionTypes ) ) {
                using( _oDocument.UndoMasterBegin() ) {
                    SelectionDelete();
                    //SelectionClear();
                }
            } else {
                using( Editor.Manipulator oBulk = _oDocument.CreateManipulator() ) {
                    if( fBack ) {
                        _oCaretPos.Back  ( oBulk );
                    } else {
                        _oCaretPos.Delete( oBulk );
                    }
                }
            }
            OnKeyDown_Arrows( Axis.Horizontal, 0 );
        }

        protected void OnKeyEnter(KeyEventArgs e) {
            if( _oViewEvents.IsCommandKey( CommandKey.Enter, (KeyBoardEnum)e.Modifiers ) )
                return;
            if( _fReadOnly || _fSingleLine )
                return;

            using( Editor.Manipulator oBulk = _oDocument.CreateManipulator() ) {
                if( TextSelector.IsSelected( _rgSelectionTypes ) ) {
                    using( _oDocument.UndoMasterBegin() ) {
                        SelectionDelete();
                        //SelectionClear();
                        _oCaretPos.Split( oBulk );
                    }
                } else {
                    _oCaretPos.Split( oBulk );
                }
            }
            OnKeyDown_Arrows( Axis.Horizontal, 0 );
        }
        
        /// <summary>
        /// Note: The difference between scroll bar scrolling and Page-Up/Down scrolling is a
        /// usability issue. We keep the caret on screen when we use the keyboard.
        /// </summary>
        protected override void OnKeyDown(KeyEventArgs e) {
            if( this.IsDisposed )
                return;

            //base.OnKeyDown( e ); // Not sure this is really needed for the control beneath. Probably bad actually.
            
            e.Handled = true;

            switch( e.KeyCode ) {
                case Keys.PageDown:
                    OnKeyDown_Page( ScrollEvents.LargeIncrement );
                    break;
                case Keys.PageUp:
                    OnKeyDown_Page( ScrollEvents.LargeDecrement );
                    break;
                case Keys.Down:
                    OnKeyDown_Arrows( Axis.Vertical, 1);
                    break;
                case Keys.Up:
                    OnKeyDown_Arrows( Axis.Vertical, -1);
                    break;
                case Keys.Right:
                    OnKeyDown_Arrows( Axis.Horizontal, 1);
                    break;
                case Keys.Left:
                    OnKeyDown_Arrows( Axis.Horizontal, -1);
                    break;
                case Keys.Home:
                    ScrollTo( EDGE.TOP );
                    break;
                case Keys.End:
                    ScrollTo( EDGE.BOTTOM);
                    break;
                    
                case Keys.Back:
                case Keys.Delete:
                    OnKeyDelete( e.KeyCode == Keys.Back );
                    break;

                case Keys.Enter:
                    OnKeyEnter( e );
                    break;
                case Keys.Tab:
                    _oViewEvents.IsCommandKey( CommandKey.Tab, (KeyBoardEnum)e.Modifiers );
                    break;
                case Keys.ControlKey:
                    if( _iSelectedTool != 1 ) { 
                        Cursor oNewCursor = Cursors.IBeam;
                        Point  oLocation  = Cursor.Position;

                        if ( HyperLinkFind( PointToClient( oLocation ) ) != null )
                            oNewCursor = Cursors.Hand;

                        Cursor = oNewCursor;
                    }
                    break;
            }
        }

        protected override void OnKeyUp(KeyEventArgs e)
        {
            if (this.IsDisposed)
                return;

            e.Handled = true;

            switch( e.KeyCode ) {
                case Keys.ControlKey:
                    if( _iSelectedTool != 1 )
                        Cursor = Cursors.IBeam;
                    break;
            }
        }

        /// <summary>
        /// Handle basic typography. Basic text going in, complete with repeating keys and etc. Special
        /// cases are handled by OnKeyDown()
        /// </summary>
        /// <remarks>We're getting this call even tho we sent handled on some of the keydown events.</remarks>
        /// <param name="e"></param>
        protected override void OnKeyPress(KeyPressEventArgs e)
        {
            if( this.IsDisposed )
               return;

 	        //base.OnKeyPress(e);

            // Ask the shell if this character is a command of some sort. Like when we're used in a dialog.
            // Tab is a shitter, for example g and G are two different characters. We don't need to know
            // the state of the shift key! But tab doesn't have that luxury. Sometimes computers suck!
            if( _oViewEvents.IsCommandPress( e.KeyChar ) )
                return;
            if( _fReadOnly )
                return;
 	         
            // Filter what we can handle... Is it a normal keystroke, we can handle TAB too.
 	        if( !char.IsControl( e.KeyChar ) || e.KeyChar == 0x0009 ) { 
                if( _oCaretPos.TrimOffset()  ){
                    if( TextSelector.IsSelected( _rgSelectionTypes ) ) {
                       using( _oDocument.UndoMasterBegin() ) {
                           SelectionDelete(); // Blast whatever was selected and replace with this char.
                           _oDocument.LineCharInsert( _oCaretPos.At, 
                                                      _oCaretPos.Offset, 
                                                      e.KeyChar);
                       }
                    } else {
                       _oDocument.LineCharInsert( _oCaretPos.At, 
                                                  _oCaretPos.Offset, 
                                                  e.KeyChar);
                    }
                }
                OnKeyDown_Arrows( Axis.Horizontal, 0 );
            }
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)	
        {
            if( this.IsDisposed )
                return( false );

            const int WM_KEYDOWN    = 0x100;
            const int WM_SYSKEYDOWN = 0x104;
               
            if ((msg.Msg == WM_KEYDOWN) || (msg.Msg == WM_SYSKEYDOWN))
            {
                switch(keyData) {
                    case Keys.Control | Keys.F:
                        _oViewEvents.IsCommandKey( CommandKey.Find, KeyBoardEnum.Control );
                        return( true );
                    case Keys.Control | Keys.A:
                        SelectionSetAll();
                        Invalidate();
                        return( true );

                    case Keys.Control | Keys.Z:
                        if( !_fReadOnly ) {
                            _oDocument.Undo();
                        }
                        return( true );

                    case Keys.Control | Keys.E: {
                        using( Graphics oGraph = this.CreateGraphics() ) {
                            float flDipX = oGraph.DpiX; 
                            using( GraphicsContext oDC = new GraphicsContext( oGraph ) ) {
                                EnumFontsHelper.Test2( oDC.Handle, flDipX );
                            }
                        }
                        return( true );
                    }
                    case Keys.Control | Keys.R: {
                        foreach ( FontFamily oneFontFamily in FontFamily.Families ) {
                            LogError( "Font", oneFontFamily.Name );
                        }
                        return( true );
                    }
                    case Keys.Delete: {
                        OnKeyDelete( false );
                        return( true );
                    }
                }
            } 

            return base.ProcessCmdKey( ref msg, keyData );
        } // end method

        public void OnLineNew( Line oLine )
        {
            if( this.IsDisposed )
                return;

            _oCacheMan.OnLineAdded( oLine );
        }

        public void OnLineDelete( Line oLine )
        {
            if( this.IsDisposed )
                return;

            _oCacheMan.OnLineDeleted( oLine );
        }
        
        public void OnLineUpdated( Line oLine, int iOffset, int iOldLen, int iNewLen ) {
            if( this.IsDisposed )
                return;

            _oCacheMan.OnLineUpdated( oLine );
        }

        /// <summary>
        /// Note: Don't Raise caret movement events here. We don't know if the caret really moved
        ///       and that can cause problems with focus management in the EditorViewSites implementation.
        /// </summary>
        protected void OnMultiFinished() {
            // BUG: If our document get's closed but somebody still points to it, it can
            // do an update on us. This window will be disposed and unusable. Need to
            // unlink the document events when this view get closed.
            if( this.IsDisposed )
                return;

            CacheRefresh( RefreshType.COMPLEX, RefreshNeighborhood.SCROLL );

            this.Invalidate();
        }

        protected void OnSingleFinished() {
            if( this.IsDisposed )
                return;

            CacheRefresh( RefreshType.SIMPLE, RefreshNeighborhood.SCROLL );

            this.Invalidate();
        }

        /// <summary>
        /// Parses roll in after the user changes the text.
        /// </summary>
        protected void OnFormatChanged() {
            _oCacheMan.OnChangeFormatting( this.Selections );

            this.Invalidate();
        }

        protected void OnWordsCounted() {
            DecorNavigatorUpdate( NavigationSource.API, _oCaretPos );
        }

        public virtual void OnEvent( BUFFEREVENTS eEvent ) {
            switch( eEvent ) {
                case BUFFEREVENTS.FORMATTED:
                    OnFormatChanged();
                    break;
                case BUFFEREVENTS.WORDSUPDATED:
                    OnWordsCounted();
                    break;
                case BUFFEREVENTS.MULTILINE:
                    OnMultiFinished();
                    break;
                case BUFFEREVENTS.SINGLELINE:
                    OnSingleFinished();
                    break;
                case BUFFEREVENTS.LOADED:
                    break;
            }
        }

        #region IPgTextView Members

        public bool Hidden {
            get {
                return( this.Visible );
            }

            set {
                throw new Exception("The method or operation is not implemented.");
            }
        }

        public SHOWSTATE ShowAs {
            get {
                if( this.Focused )
                    return( SHOWSTATE.Focused );
                else
                    return( SHOWSTATE.Inactive );
                // Need to clarify Active state.
            }
            set {
                throw new Exception("The method or operation is not implemented.");
            }
        }

		public void WordBreak( Line oLine, ICollection< IPgWordRange > rgWords ) {
			_oDocument.WordBreak( oLine, rgWords );
		}

        public TextPosition Caret {
            get { 
                try {
                    return new TextPosition( _oCaretPos.Line.At, _oCaretPos.Offset );
                } catch( NullReferenceException ) {
                    _oSiteView.LogError( "View", "CaretLine accessor exception" );
                    return new TextPosition( 0, 0 );
                }
            }
        }

        /// <summary>
        /// Only updates the screen if the caret is not already on screen.
        /// </summary>
        public void ScrollToCaret()
        {
            if( !_oCacheMan.IsHit( _oCaretPos ) ) {
                CacheRefresh( RefreshType.RESET, RefreshNeighborhood.CARET);
                CaretIconRefreshLocation();

                Raise_Navigated( NavigationSource.API, _oCaretPos );

                Invalidate();
            }
        }

        /// <summary>
        /// This is a little different than ScrollToCaret(). We're actually moving the caret,
        /// which requires us to update the _iAdvance.
        /// </summary>
		/// <seealso cref="CaretAndAdvanceReset"/>
        public void ScrollTo( EDGE eEdge )
        {
            Line oLine   = null;
            int  iOffset = 0;
                
            switch( eEdge ) {
                case EDGE.TOP:
                    oLine   = _oDocument.GetLine( 0 );
                    iOffset = 0;
                    break;
                case EDGE.BOTTOM:
                    oLine   = _oDocument.GetLine( _oDocument.ElementCount - 1 );
                    iOffset = oLine.ElementCount;
                    break;
            }

            if( oLine != null ) {
                _oCaretPos.Line   = oLine;
                _oCaretPos.Offset = iOffset;
            }

            ScrollToCaret();

            if( _oCacheMan.GlyphLineToPoint( _oCaretPos, out Point pntCaretWorldLoc ) ) {
                _iAdvance = pntCaretWorldLoc.X;
            }
        }

        /// <summary>
        /// Do selection based on Line/Offset position. But only selects up to the end of the line 
		/// where selection starts. This command moves the Caret.
        /// </summary>
        /// <returns>True if anything is attempted to select. No need to
        /// call document refresh for this operation.</returns>
        public bool SelectionSet( int iLine, int iOffset, int iLength ) {
            Line oLine = _oDocument.GetLine(iLine);

            // Remember caret length is always zero.
            _oCaretPos.Line   = oLine;
            _oCaretPos.Offset = iOffset + iLength;

            SelectionClear();

            _rgSelectionTypes.Add( new LineRange( oLine, iOffset, iLength, SelectionTypes.Start ) );
            _oCacheMan.OnChangeFormatting( _rgSelectionTypes ); // SelectionClear calls this too! Grrrr...

            // Used to do a cache refresh but I don't think it's necessary.
            Invalidate();

            Raise_Navigated( NavigationSource.API, _oCaretPos );

            return( true );
        }

        #endregion

        /// <summary>
        /// Find if the given world coordiate is within the selection
        /// </summary>
        /// <param name="pntLocation">Point in world coordiates.</param>
        /// <returns>True if the value is within the current selection if there is one!</returns>
        public bool IsSelectionHit( Point pntWorld ) {
            foreach( UniscribeCache oCache in _oCacheMan ) {
                foreach( LineRange oSelection in _rgSelectionTypes ) {
                    if( oSelection.IsHit( oCache.Line ) ) {
                        if( oCache.IsHit( pntWorld ) ) {
                            int iEdge = oCache.GlyphPointToOffset( pntWorld );

                            if( oSelection.SelectionType == SelectionTypes.Middle ) {
                                if( iEdge >= oSelection.Offset &&
                                    iEdge <= oSelection.Offset + oCache.Line.ElementCount ) 
                                {
                                    return( true );
                                }
                            } else {
                                if( iEdge >= oSelection.Offset &&
                                    iEdge <  oSelection.Offset + oSelection.Length )
                                {
                                    return( true );
                                }
                            }
                        }
                    }
                }
            } // end for
            return( false );
        }

        public virtual Image Iconic {
            get { return( _oIcon ); }
        }

        public string Banner {
            get { 
                try {
                    // Find first non blank character.
                    Line oLine  = _oCaretPos.Line;
                    int  iStart = 0;

                    while( iStart < oLine.ElementCount ) {
                        if( !char.IsWhiteSpace( oLine[iStart] ) )
                            break;
                        ++iStart;
                    }

                    return( oLine.SubString( iStart, 25 ) );
                } catch( NullReferenceException ) {
                    return( string.Empty );
                }
            }
        }

        public virtual bool Execute(Guid sGuid) {
			switch( sGuid ) {
				case var r when r == GlobalCommands.Delete:
					SelectionDelete();
					return true;
				case var r when r == GlobalCommands.Cut:
					ClipboardCutTo();
					return true;
				case var r when r == GlobalCommands.Copy:
					ClipboardCopyTo();
					return true;
				case var r when r == GlobalCommands.Paste:
					// If I ever need a clipboard 'param' I can get it via a site/host method.
					ClipboardPasteFrom( Clipboard.GetDataObject(), ClipboardOperations.Default );
					return true;
				case var r when r == GlobalCommands.StepLeft:
					OnKeyDown_Page( ScrollEvents.LargeDecrement );
					return true;
				case var r when r == GlobalCommands.StepRight:
					OnKeyDown_Page( ScrollEvents.LargeIncrement );
					return true;
				case var r when r == GlobalCommands.PasteAsImg:
					ClipboardPasteFrom( Clipboard.GetDataObject(), ClipboardOperations.FileDrop );
					return true;
				case var r when r == GlobalCommands.PasteAsText:
					ClipboardPasteFrom( Clipboard.GetDataObject(), ClipboardOperations.Text );
					return true;
				case var r when r == GlobalCommands.PasteAsBase64:
					ClipboardPasteFrom( Clipboard.GetDataObject(), ClipboardOperations.Base64 );
					return true;
				case var r when r == GlobalCommands.Undo:
					_oDocument.Undo();
					return true;
				case var r when r == GlobalCommands.SelectAll:
					SelectionSetAll();
					Invalidate();
					return true;
				case var r when r == GlobalCommands.Play:
					PlayMorse();
					return true;
				case var r when r == GlobalCommands.Stop:
					if( _oMorse != null ) {
						_oMorse.Stop();
						_oMorse = null;
					}
					return true;
			}

            // While it is kind of fun. I should probably move this functionality
            // to the morsepractice project instead.
			if( _oMorse != null ) {
				return _oMorse.Execute( sGuid );
			}

            return( false );
        }

        public virtual object Decorate( IPgViewSite oBaseSite, Guid sGuid ) {
			try {
				if (sGuid.Equals(GlobalDecorations.Properties)) {
					return new PropWin( oBaseSite, NavProps );
				}
				if ( sGuid.Equals( GlobalDecorations.Productions ) ) {
					return new EditWinProductions( oBaseSite, _oDocument );
				}
			} catch ( Exception oEx ) {
				Type[] rgErrors = { typeof( NotImplementedException ),
									typeof( NullReferenceException ),
									typeof( ArgumentException ),
									typeof( ArgumentNullException ) };
				if( rgErrors.IsUnhandled( oEx ) )
					throw;

				LogError( "decor", "Couldn't create EditWin decor: " + sGuid.ToString() );
			}

            return( null );
        }

		public virtual Guid Catagory {
            get {
                return( Guid.Empty );
            }
        }

		public string ToolName(int iTool) {
			try {
				return _rgTools[iTool];
			} catch( ArgumentOutOfRangeException ) {
				return "-out of bounds-";
			}
		}

		public Image ToolIcon(int iTool) {
			return null;
		}

		public int ToolCount => _rgTools.Count;

		public int ToolSelect { 
			get => _iSelectedTool; 
			set { 
				_iSelectedTool = value; 
				_oSiteView.Notify( ShellNotify.ToolChanged );
			}
		}

		protected void PlayMorse() {
			if( _oMorse != null ) {
				switch( _oMorse.Status ) {
					case WorkerStatus.FREE:
						_oMorse = null;
						break;
					case WorkerStatus.PAUSED:
						_oMorse.Start( 0 );
						return;
					case WorkerStatus.BUSY:
						return;
				}
			}

			try {
				IPgMorseWorkerCreater oCreate = (IPgMorseWorkerCreater)_oSiteView.Host.Services;
				_oMorse = oCreate.CreateMorseWorker( _oDocument[_oCaretPos.At].ToString() );
			} catch( Exception oEx ) {
				Type[] rgErrors = { typeof( NullReferenceException ),
									typeof( InvalidCastException ) };
				if( rgErrors.IsUnhandled( oEx ) )
					throw;
			}
		}
	}

	/// <summary>
	/// Don't let your head explode. This is an editwin to 
	/// show the productions on the _oDocumentOverride. BUT
	/// container of the productions in a child document.
	/// SO be careful when accessing _oDocument!!!
	/// </summary>
	public class EditWinProductions : EditWin {
		readonly BaseEditor _oDocumentOverride;

		public EditWinProductions( IPgViewSite oBaseSite, BaseEditor oDocument ) : 
			base( oBaseSite, oDocument.Productions, false, false ) 
		{
			_oDocumentOverride = oDocument ?? throw new ArgumentNullException( "Weird happenings in EdiwWinProperties!" );

			try {
				oDocument.ProductionsTrace = true;
			} catch( NotImplementedException oEx ) {
				throw new ArgumentException( "Document must support productions to use this window!", oEx );
			}
		}

		/// BUG: I'm noticing this is getting called really late.
		/// I'm probably not dealing with closing addornments properly.
		protected override void Dispose(bool disposing) {
			if( disposing ) {
                HyperLinks.Clear();
				_oDocumentOverride.ProductionsTrace = false;
			}
			base.Dispose(disposing);
		}
	}

	// 4/24/2019 : Obsolete way for making tabular data. I might revisit it. But for now it's dead.
	//public class EditWinTable : EditWin {
	//	public EditWinTable( IPgViewSite oBaseSite, BaseEditor p_oDocument, 
	//						 bool fReadOnly = false, bool fSingleLine = false) : 
	//		base( oBaseSite, p_oDocument, fReadOnly, fSingleLine ) 
	//	{
	//	}

	//	protected override CacheManager CreateCacheManager() {
	//		CacheManager oManager = base.CreateCacheManager();

	//		oManager.Left[1] = 150;
	//		oManager.Left.Add(0);

	//		return (oManager);
	//	}

	//}
}