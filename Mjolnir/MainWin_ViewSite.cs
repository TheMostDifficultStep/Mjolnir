using System;
using System.Windows.Forms;
using System.Xml;
using System.Collections.Generic;

using SkiaSharp;

using Play.Interfaces.Embedding;
using Play.Edit;
using Play.Rectangles;

namespace Mjolnir {
    /// <summary>
    /// This little object gives us a way to match a ToolStripMenuItem back to the
    /// Tool ID it is tracking!
    /// </summary>
	public class MenuItemWithID : 
        ToolStripMenuItem
    {
		public int ID { get; }

		public MenuItemWithID( int iID, string strValue, EventHandler oHandler ) :
			base( strValue, null, oHandler ) 
        {
            ID = iID;
		}
	}
    
    /// <summary>
    /// Finally inherit from TextLine. The up side is the view's banner text is stored
    /// as a mutable buffer, the down side is most people want a string. So I save a copy
    /// as string anyway. This object goes straight into the ViewSelector document.
    /// </summary>
    public class ViewSlot : 
        TextLine,
        IPgViewSite,
        IPgViewNotify,
		IPgShellSite,
        IDisposable
    {
        protected readonly MainWin  _oHost;    // Back pointer to the container.
        private   readonly IDocSlot _oDocSite; // Pointer to the document site our view site is showing.

        protected Control                      _oViewControl;
				  IPgSave<XmlDocumentFragment> _oViewSaveXml;
		protected IPgLoad<XmlElement>          _oViewLoadXml;
		protected IPgCommandView               _oViewCommand;
				  IPgTools                     _oViewTools;

        static   UInt32 _iIDCount;
        readonly UInt32 _iID;

        ToolStripMenuItem         _oMenuItem; // This is our entry in the list of windows the shell is showing.
		protected LayoutRect      _oLayout;   // Put our new Framelet here.

		private List<MenuItemWithID> ToolBox { get; }
		public  virtual LayoutRect   Layout  { get { return _oLayout; } }

		public Guid ViewType { get; private set; }

		///<exception cref="ArgumentNullException" />
        internal ViewSlot( MainWin oHost, IDocSlot oDocSite, Guid guidViewType ) :
            base( 0, string.Empty ) 
        {
            _iID       = _iIDCount;
            _iIDCount += 1;

            _oHost    = oHost    ?? throw new ArgumentNullException( "view needs a valid host to operate." );
            _oDocSite = oDocSite ?? throw new ArgumentNullException( "view needs a valid document site to operate." );

			ToolBox = new List<MenuItemWithID>(3);

            //CacheTitle = new CacheWrapped( ShortTitle ); 
			//CacheTitle.Words.Add(_oRangeText);

			ViewType = guidViewType;
        }

        public void ViewCreate( Guid guidViewType ) {
            try {
                GuestAssign( (Control)_oDocSite.Controller.CreateView( this, _oDocSite.Document, guidViewType ) );
            } catch( Exception oEx ) {
                Type[] rgErrors = { typeof( InvalidOperationException ),
                                    typeof( ApplicationException ),
                                    typeof( ArgumentException ),
                                    typeof( ArgumentNullException ),
                                    typeof( InvalidCastException ) };
                if( rgErrors.IsUnhandled( oEx ) )
                    throw;

                LogError( "View", "Unable to creat view on document." );
                throw new ApplicationException( "Unable to creat view", oEx );
            }
        }

        /// <summary>
        /// This method is for internal access to the control.
        /// BUG: This doesn't match how I'm using GuestSet (on doc slots) now a days.
        /// Plus, I'd rather this object is a IPgParent and not CONTROL!.
        /// </summary>
        internal Control Guest {
            get { return (_oViewControl); }
			[Obsolete]set { GuestAssign( value ); }
        }

		/// <exception cref="ArgumentNullException" />
		/// <exception cref="ArgumentException" />
		protected virtual void GuestAssign( Control oGuest ) {
            _oViewControl = oGuest ?? throw new ArgumentNullException( "View site needs a guest to be valid." );
            _oViewCommand = oGuest as IPgCommandView ?? throw new ArgumentException( "view needs IPgCommand." );
			_oViewSaveXml = oGuest as IPgSave<XmlDocumentFragment> ?? throw new ArgumentException( "view needs IPgSave<XmlDocumentFragment>" );
			_oViewLoadXml = oGuest as IPgLoad<XmlElement> ?? throw new ArgumentException( "view needs IPgLoad<XmlElement>." );
			_oViewTools   = oGuest as IPgTools; // Ok to be null.

            try { 
              // TODO: Need to merge this value to what I'm using for long/short names
                _oMenuItem = new ToolStripMenuItem( _oViewCommand.Banner, null, new EventHandler(this.OnMenuSelectView));
		        _oLayout   = new LayoutControl( oGuest, LayoutRect.CSS.Pixels );
	          //_oLayout   = new FrameletForView( this, (uint)(this._oHost.Document.FontStandard.Height * 1.5 ) );
            } catch( Exception oEx ) { 
                throw new ArgumentException( "Unable to prep ViewSite for new View.", oEx );
            }
            DocumentReferenceIncrement( +1 );
		}

		/// <summary>
		/// Might want to reinvestigate the dispose pattern for this one.
		/// </summary>
        public override void Dispose() {
			if( _oViewControl != null ) {
				_oViewControl.Hide();
				_oViewControl.Site = null;
				_oViewControl.Dispose();
				_oViewControl = null;
			}

			// Dispose after the guest, since it'll call our OnFocus as it is destroyed!
			if( _oMenuItem != null ) {
 				_oMenuItem.Dispose();
				_oMenuItem = null;
			}

			ToolBox.Clear();

			// TODO: Should probably check if we've been disposed of to be safe. ^_^;;
            DocumentReferenceIncrement( -1 );
            base.Dispose();
        }

        protected virtual void DocumentReferenceIncrement( int i ) {
            _oDocSite.Reference += i;
        }

        public string LastPath {
            get { return( _oDocSite.LastPath ); }
        }

        public uint ID {
            get { return( _iID ); }
        }
        
        internal SKBitmap Icon { get { return _oViewCommand.Icon; } }

        /// <summary>
        /// Go to the guest view and ask for it's bitmap to be used as the icon.
        /// But here's the rub. You can create an icon handle from a bitmap then
        /// call Icon.FromHandle() to create an Icon but that little shit won't 
        /// take control of the handle. Then the lazy form simply takes a reference
        /// to the given icon, meaning unless you clear the form icon first you 
        /// can't destroy the handle you're stuck with. Sooooo....
        /// </summary>
    //    protected void CreateIcon() {
    //        IntPtr ipHIcon = IntPtr.Zero;
    //        try {
    //            Bitmap oBitmap = (Bitmap)_oViewCommand.Iconic;
				//if( oBitmap != null ) {
				//	ipHIcon = oBitmap.GetHicon();
				//	using( Icon oIcon = Icon.FromHandle( ipHIcon ) ) {
				//		Icon = (Icon)oIcon.Clone();
				//	}
				//}
    //        } catch( InvalidCastException ) {
				//// this is kinda cool. If our image is something super modern and cool, I'll have
				//// to create the icon differently. ^_^;
    //        } finally {
    //            if( ipHIcon != IntPtr.Zero )
    //                MyExtensions.DestroyIcon( ipHIcon );
    //        }
    //    }

		/// <summary>
		/// Got to the view and ask it for all the tools.
		/// </summary>
		internal void ToolsInit() {
			if( _oViewTools != null ) {
				for( int i = 0; i < _oViewTools.ToolCount; ++i ) {
					ToolBox.Add( new MenuItemWithID( i, _oViewTools.ToolName( i ), OnToolClicked ) );
				}
			}
		}

		internal void ToolsMenuLoad( ToolStripMenuItem oTools ) {
			try {
				oTools.DropDownItems.Clear();

				foreach( MenuItemWithID oItem in ToolBox ) {
					oItem.Checked = oItem.ID == _oViewTools.ToolSelect;
					oTools.DropDownItems.Add( oItem );
				}
			} catch( NullReferenceException ) {
			}
		}

		protected void OnToolClicked(object sender, EventArgs e) {
			try {
				if (sender is MenuItemWithID oItem) {
					_oViewTools.ToolSelect = oItem.ID;
				}
			} catch( NullReferenceException )  {
			}
		}

		internal virtual bool InitNew() {
			try {
				if( !_oViewLoadXml.InitNew() ) // BUG: A parse finish from the scheduler is showing up before we call this!
					return( false );

				ToolsInit();
			} catch( Exception oEx ) {
				Type[] rgErrors = { typeof( ArgumentException ), 
					                typeof( ArgumentNullException ),
									typeof( NullReferenceException ),
									typeof( InvalidOperationException ) };
				if( rgErrors.IsUnhandled( oEx ) )
					throw;

				_oHost.LogError( this, "storage", "Can't initialize view." );
				return( false );
			}

            //CreateIcon();

            // This keeps us from tabbing into our document and then blasting a bunch
            // of spaces into the document because we tabbed our way into it. The document 
            // still accepts tabs, but you have to specifically set focus to it.
            _oViewControl.TabStop = false;

            UpdateTitle();
        
            return( true );
        }

        internal bool Load( XmlElement xmlRoot ) {
			try {
				if( !_oViewLoadXml.Load( xmlRoot ) ) {
					LogError( "storage", "Couldn't load view from xml." );
					return( false );
				}
				ToolsInit();
			} catch( Exception oEx ) {
				Type[] rgErrors = { typeof( ArgumentException ), 
					                typeof( ArgumentNullException ),
									typeof( NullReferenceException ),
									typeof( InvalidOperationException ) };
				if( rgErrors.IsUnhandled( oEx ) )
					throw;

				LogError( "storage", "Couldn't load view from string" );
				return( false );
			}

            //CreateIcon();

            _oViewControl.TabStop = false;

            return( true );
        }

        internal bool Save( XmlDocumentFragment oWriter ) {
			try {
				return( _oViewSaveXml.Save( oWriter ) );
			} catch( Exception oEx ) {
				Type[] rgErrors = { typeof( ArgumentException ), 
					                typeof( ArgumentNullException ),
									typeof( NullReferenceException ),
									typeof( InvalidOperationException ) };
				if( rgErrors.IsUnhandled( oEx ) )
					throw;

				LogError( "storage", "Couldn't save view to text writer" );
			}
			return( false );
        }

       /// <summary>
        /// Every view links to the document site that spawned it. In this manner we can
        /// track when the last view on the document has been closed and thus close the
        /// document as well.
        /// </summary>
        internal IDocSlot DocumentSite {
            get{ return( _oDocSite ); }
        }

        public virtual FILESTATS FileStatus { get { return( FILESTATS.UNKNOWN ); } }

        public ToolStripMenuItem MenuItem {
            get{ return( _oMenuItem ); }
        }

        public virtual void OnNavigate( int iLine, int iOffset ) { }

        /// <summary>
        ///     Set the focus to the control at this site. Focus doesn't actually work until the
        ///     window handle get's created. But Select works everytime.
        /// </summary>
        /// <remarks>In windowless, we would call something like IPgWindowless::Blur() on the
        ///     windowless control loosing focus and call IPgWindowless::Focus() 
        ///     on windowless control gaining focus. Windowless controls just like
        ///     windowed controls are required to call OnFocus() on our site which
        ///     finally sets the current view.
        /// </remarks>
        public void SetFocus() {
			try {
				// When the view gets the focus message that signals the shell to BringToFront() it.
				_oViewControl.Select();
				_oViewControl.Focus();
			} catch( NullReferenceException ) {
				LogError( "windowing", "Guest is null!", true );
			}
        }

        public bool Focused {
            get { 
				try {
					return( _oViewControl.ContainsFocus ); 
				} catch( NullReferenceException ) {
					LogError( "windowing", "Guest is null!", false );
					return( false );
				}
			}
        }

		/// <summary>
		/// When the user selects a view from the menu, we switch back to solo view if not in solo view.
		/// </summary>
		/// <seealso cref="MainWin.OnViewAll"/>
        public void OnMenuSelectView( object sender, EventArgs e ) {
			_oHost._eLayout = MainWin.TOPLAYOUT.Solo;
            _oHost.ViewSelect( this, true );
        }

        // BUG: We can change the view a number of ways. Need to unify.
        internal void BringToFront() {
			try {
				_oViewControl.BringToFront();
			} catch( NullReferenceException ) {
				LogError( "windowing", "Guest is null!", false );
			}
        }

        /// <summary>
        /// 12/15/2015 : I use this when changing views to get the cursor back on screen.
        ///              Need a command like ScrollToPrimaryEdit() or some such. But I might
        ///              not even want to do that.
        /// TODO: Make this some sort of IPgCommand
        /// </summary>
        internal void ScrollToPrimaryEdit() {
            IPgTextView oGuestText = _oViewControl as IPgTextView;
            if( oGuestText != null )
                oGuestText.ScrollToCaret();
        }
        
        public virtual IPgParent     Host       => _oHost;
		public virtual IPgViewNotify EventChain => this;

        public virtual void LogError( string strMessage, string strDetails, bool fShow=true ) {
            _oHost.LogError( this, strMessage, strDetails, fShow );
        }

        /// <summary>
        /// When the window we host gets the focus it is required to
        /// call our site here so we can update the UI.
        /// </summary>
        /// <remarks>I used to have focus problems on create of the window. It
        /// might be that's not bothering me now so I've commented out the
        /// redundant and possibly infinite loop causing, guest focus.</remarks>
        public virtual void NotifyFocused( bool fSelect ) {
            try {
                if( fSelect ) {
                    _oViewControl.BringToFront();

                    _oHost.OnViewFocused( this );
                } else {
                    _oHost.OnViewBlurred( this );
                }
            } catch( NullReferenceException ) {
                LogError( "internal", "View site, MenuItem is probably null OnFocused()." );
            }
        }

        public virtual bool IsCommandPress( char cChar ) {
            return( false );
        }

        public virtual bool IsCommandKey( CommandKey ckKey, KeyBoardEnum kbModifiers ) {
            if( ckKey == CommandKey.Find && kbModifiers == KeyBoardEnum.Control ) {
                SmartHerderBase oHerder = _oHost.DecorOpen( "find", true );
                if( oHerder != null ) {
                    oHerder.AdornmentFocus( null ); // since find is a solo, we don't need it's view site!
                }
                return( true );
            }
            return( false );
        }

        /// <summary>
        /// This is a cache of the title so we don't keep generating it whenever we switch
        /// views and the main window asks for this value over and over.
        /// </summary>
        public string Title { get; protected set; }

        public void UpdateTitle() {
            // So it turns out on dispose, sometimes children getting un-focus 
            // events send them up and we'll bomb out. I don't think an exception
            // and error message is called for yet.
			try {
				int iViewID = _oHost.ViewTitleID( this );

                Empty();
				TryAppend( _oViewCommand.Banner );

				if( iViewID > -1 ) {
                    TryAppend( ", " );
                    TryAppend( iViewID.ToString() );
                    TryAppend( " of " );
                    TryAppend( DocumentSite.Reference.ToString() );
				}
			} catch ( NullReferenceException ) {
				TryAppend( "View" );
			}

            Title = ToString();
            // Dispose will set the menuitem to null it's a good idea to check it.
            if( _oMenuItem != null ) {
                _oMenuItem.Text = Title;
            }
        }

        public bool IsTextInvalid {  get { return false; /* CacheTitle.IsInvalid; */ } }

        public void SetLayout( MainWin.TOPLAYOUT eLayout ) {
            //switch( eLayout ) {
            //    case MainWin.TOPLAYOUT.Multi:
            //        Layout.TitleHidden = false;
            //        break;
            //    case MainWin.TOPLAYOUT.Solo:
            //        Layout.TitleHidden = true;
            //        break;
            //}
        }

        public virtual void Notify( ShellNotify eEvent ) {
			switch( eEvent ) {
				case ShellNotify.BannerChanged:
					UpdateTitle();
                    // This updates all the view banners in the view list and calls SetTitle()
                    _oHost.UpdateAllTitlesFor( this._oDocSite ); 
					break;

				case ShellNotify.DocumentDirty:
					_oHost.SessionDirtySet( true ); 
					break;

				case ShellNotify.ToolChanged:
					foreach( MenuItemWithID oTool in ToolBox ) {
						oTool.Checked = oTool.ID == _oViewTools.ToolSelect;
					}
					break;
			}
		}


        /// <summary>
        /// This command is originated in the shell for the currently focused
        /// view. We turn around and ask our document to ask the shell for a
        /// save. At this point we could as for a save of our settings too! ^_^
        /// </summary>
        /// <returns></returns>
        public void SaveDocument( bool fAtNewLocation ) {
            // TODO: When the shell has to put up a dialog it's going to be a
            // modal one, thus pushing a message loop. That's a bit of a crock. Perhaps I
            // need a save/savecomplete protocol.
            _oDocSite.Save(fAtNewLocation);

            _oHost.UpdateAllTitlesFor( _oDocSite );
        }

        public bool Execute( Guid sCommand ) {
			try {
				return _oViewCommand.Execute( sCommand );
			} catch( NullReferenceException ) {
				LogError( "commands", "Guest does not support IPgCommand" );
				return false;
			}
        }

        // TODO: Figure out how to pass the filename back on a per view basis. 
        public virtual string FileName {
            get {
                return( _oDocSite.FileName );
            }
        }

        /// <summary>
        /// Request the shell to create a new view on this view's document.
        /// </summary>
		/// <remarks>
		/// I'd like to make this a service. But as the main program is the only source 
		/// for services that won't work if we ever support more than one top level window.
		/// </remarks>
        public object AddView(Guid guidViewType, bool fFocus) {
            // If we've got a view already of the given view type, just use that.
            // This looks like the ONLY place that uses the catagory method... Sigh. BUG.
            foreach( IPgCommandView oSibling in _oHost.EnumViews( _oDocSite ) ) {
                if( oSibling.Catagory == guidViewType ) {
                    _oHost.CurrentView = oSibling;
                    return oSibling;
                }
            }
			try {
				EditorShowEnum eShow = fFocus ? EditorShowEnum.FOCUS : EditorShowEnum.SILENT;
				ViewSlot       oSlot = _oHost.ViewCreate( _oDocSite, guidViewType, eShow );

				return oSlot.Guest;
			} catch( NullReferenceException ) {
				return null;
			}
        }

        public IEnumerable<IPgCommandView> EnumerateSiblings {
            get {
                return _oHost.EnumViews( _oDocSite );
            }
        }

        public void FocusMe() {
            _oHost.CurrentView = Guest;
        }

        public void FocusCenterView() {
			_oHost.FocusCurrentView();
		}

        public uint SiteID { get { return _iID; } }
	}

    /// <summary>
    /// This view slot is so that we don't create lock counts that keep the
    /// main window open. We use this for the various decor views.
    /// </summary>
	internal class NonRefCountSlot : ViewSlot {
		///<exception cref="ArgumentNullException" />
        public NonRefCountSlot( MainWin oHost, IDocSlot oDocSite, Guid oGuid ) :
            base( oHost, oDocSite, oGuid )
        {
        }

		public override IPgViewNotify EventChain => this;

		/// <remarks>
		/// A little bit evil, in the future whe should probably have our viewsite inherit from
		/// the DecorSite which is the restricted one.
		/// </remarks>
		/// <param name="oGuest"></param>
		protected override void GuestAssign( Control oGuest ) {
            _oViewControl = oGuest ?? throw new ArgumentNullException( "Decor view site needs a guest to be valid." );
			_oViewCommand = oGuest as IPgCommandView ?? throw new ArgumentException( "view must support IPgCommand" );
		}

		/// <remarks>
		/// Override so we don't try to change the title or set the icon like a normal 
		/// view would do. 
		/// </remarks>
		/// <seealso cref="DecorSlot"/>
        internal override bool InitNew() {
			try {
				if( Guest is IPgLoad oGuestLoad ) {
					if( !oGuestLoad.InitNew() ) // BUG: A parse finish from the scheduler is showing up before we call this!
						return( false );
				} else {
					LogError( "Initialize", "Guest does not support IPgLoad" );
				}
			} catch( Exception oEx ) {
				Type[] rgErrors = { typeof( ArgumentException ), 
					                typeof( ArgumentNullException ),
									typeof( NullReferenceException ),
									typeof( InvalidOperationException ) };
				if( rgErrors.IsUnhandled( oEx ) )
					throw;

				_oHost.LogError( this, "storage", "Can't initialize view." );
				return false;
			}
			return true;
		}

        /// <summery> Ignore any ref counting.</summery>
        protected override void DocumentReferenceIncrement( int i ) {
        }
	}

	/// <summary>
	/// BUG: Inheriting from ViewSite is probably not the best thing.
	/// Commands for each tool window means something different, for one
	/// ViewSelector document is hosted by MainWin and NOT Program,
	/// thus the view site cannot produce a IDocSlot.
	/// </summary>
	internal class DecorSlot : NonRefCountSlot {
		SmartHerderBase _oHerder;

		///<exception cref="ArgumentNullException" />
        public DecorSlot( MainWin oHost, IDocSlot oDocSite, SmartHerderBase oHerder ) :
            base( oHost, oDocSite, Guid.Empty )
        {
			_oHerder = oHerder ?? throw new ArgumentNullException( "Herder required." );
        }

		public override IPgViewNotify EventChain => this;

		/// <remarks>
		/// A little bit evil, in the future whe should probably have our viewsite inherit from
		/// the DecorSite which is the restricted one.
        /// BUG: Look into changing GuestAssign to GuestSet to match others.
		/// </remarks>
		protected override void GuestAssign( Control oGuest ) {
            _oViewControl = oGuest ?? throw new ArgumentNullException( "Decor view site needs a guest to be valid." );
		}

        /// <summary>
        /// Shell focus events are for the tools to track the current document view.
        /// If a tool get's the focus, it's not the same.
        /// </summary>
        /// <param name="fSelect"></param>
        public override void NotifyFocused(bool fSelect) {
			if( fSelect )
				_oHerder.OnFocused();
			else
				_oHerder.OnBlurred();

			_oHost.Invalidate();
        }

        /// <summary>
        /// A dialog control on the side should not accept either TAB and ENTER key.
        /// </summary>
        public override bool IsCommandKey( CommandKey ckKey, KeyBoardEnum kbModifiers ) {
            switch( ckKey ) {
                case CommandKey.Tab:
                    return( true );
            }
            return( false );
        }
        public override bool IsCommandPress( char cChar ) {
            switch( cChar ) {
                case '\r':
                    return( true );
            }
            return( false );
        }

		public override void Notify( ShellNotify eEvent ) {
			// Ignore this event from decor. These are usually recycled views that don't know
			// they are being used as decor.
		}
	}

    /// <summary>
    /// This object begs the question, where is the best place to put all the specialized behavior on these complex objects
    /// utilizing my text editor. I'm thinking that I should subclass the edit win, then I can leverage my view creation system.
    /// But I'm going to go with this for the moment... 4/7/2020
    /// </summary>
    /// <remarks>We don't bring to top on views when the user navigates the view window. I prefer hitting the space bar like
    /// a button to make the view switch.</remarks>
    internal class ViewSelectorSlot : DecorSlot {
        readonly ViewsEditor _oDoc_Views;
                 IPgTextView _oViewText; 

        public ViewSelectorSlot(MainWin oHost, IDocSlot oDocSite, SmartHerderBase oHerder ) :
            base(oHost, oDocSite, oHerder)
        {
            _oDoc_Views = oDocSite.Document as ViewsEditor ?? throw new ArgumentException( "Document must support a ViewSite Editor" );
        }

		protected override void GuestAssign( Control oGuest ) {
            base.GuestAssign( oGuest );

            _oViewText = oGuest as IPgTextView ?? throw new ArgumentException( "Control must support IPgTextView" );

            oGuest.Cursor      = Cursors.Hand;
            //oGuest.ContextMenu = new ContextMenu();

            //oGuest.ContextMenu.MenuItems.Add( new MenuItem( "Goto",  new EventHandler( MenuGotoView ),        Shortcut.CtrlG ) );
            //oGuest.ContextMenu.MenuItems.Add( new MenuItem( "Close", new EventHandler( MenuCloseViewCommand), Shortcut.Del ) );

            _oHost.ViewChanged += OnHost_ViewChanged; // This should be on the InitNew. If we fail init we're still wired up.
		}

        private void OnHost_ViewChanged( object oView ) {
            try {
                foreach( ViewSlot oViewLine in _oDoc_Views ) {
                    // Just want to see if the objects are the same.
                    if( oViewLine.Guest == oView )
                        _oDoc_Views.HighLight = oViewLine;
                }
            } catch( NullReferenceException ) {
                LogError( "View Selector", "Problem monitering switch." );
            }
        }

        public override bool IsCommandPress( char cChar ) {
            switch( cChar ) {
                case ' ':
                    GotoView( fFocus:false );
                    return( true );
                case '\r':
                    GotoView( fFocus:true );
                    return( true );
                case '\u001B': // escape: Just go back to view.
                    _oHost.SetFocusAtCenter();
                    return( true );
            }
            return( false );
        }

        protected void GotoView( bool fFocus ) {
			try {
                _oHost.ViewSelect( _oDoc_Views[_oViewText.Caret.Line], fFocus );
			} catch( Exception oEx ) {
                Type[] rgErrors = { typeof( NullReferenceException ),
                                    typeof( IndexOutOfRangeException ),
                                    typeof( ArgumentOutOfRangeException ) };
                if( rgErrors.IsUnhandled( oEx ) )
                    throw;

                LogError( "View Selector", "Error trying to focus view." );
			}
        }

        protected void MenuGotoView( object s, EventArgs e ) {
            GotoView( fFocus:true );
        }

        protected void MenuCloseViewCommand( object s, EventArgs e ) {
			try {
				_oHost.ViewClose( _oDoc_Views[_oViewText.Caret.Line] );
			} catch( Exception oEx ) {
                Type[] rgErrors = { typeof( NullReferenceException ),
                                    typeof( IndexOutOfRangeException ),
                                    typeof( ArgumentOutOfRangeException ) };
                if( rgErrors.IsUnhandled( oEx ) )
                    throw;

                LogError( "View Selector", "Error trying close view." );
			}
        }

        public override void Dispose() {
            _oViewControl.Dispose();
            _oHost.ViewChanged -= OnHost_ViewChanged;
        }
    }
}
