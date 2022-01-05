using System;
using System.Drawing;
using System.Xml;
using System.Collections.Generic;
using System.Windows.Forms;
using System.Reflection;
using System.Text;

using SkiaSharp;
using SkiaSharp.Views.Desktop;

using Play.Interfaces.Embedding;
using Play.Rectangles;
using Play.Edit;
using Play.ImageViewer;
using Play.Forms;
using Play.Sound;

namespace Play.SSTV {
	/// <summary>
	/// This is a customized image dir/doc viewer that has the icons on the main 
	/// view area instead of in the outline. Fits in better with the SSTV system.
	/// </summary>
    public class WindowSSTVHistory :
        Control,
		IPgParent,
        IPgLoad<XmlElement>,
        IPgSave<XmlDocumentFragment>,
        IPgCommandView,
        IDisposable 
	{
		public static Guid GUID { get; } = new Guid( "{B746A6D3-B628-4AA8-BD82-BAC860B7BEEA}" );

		protected IPgViewSite _oSiteView;
		protected DocSSTV     _oDocSSTV;

        public Guid   Catagory => GUID;
        public string Banner   => "MySSTV History";
        public Image  Iconic   => null;
        public bool   IsDirty  => false;

        public IPgParent Parentage => _oSiteView.Host;

        public IPgParent Services  => Parentage.Services;

        protected WindowSoloImageNav _wmViewRxHistorySelected;
		protected ImageViewIcons     _wmViewRxHistory;

		protected readonly LayoutStack _oLayout = new LayoutStackVertical( 5 );

		protected class SSTVWinSlot :
			IPgViewSite,
			IPgShellSite
		{
			protected readonly WindowSSTVHistory _oHost;

			public ChildID ID { get; }

			public SSTVWinSlot( WindowSSTVHistory oHost, ChildID eID  ) {
				_oHost = oHost ?? throw new ArgumentNullException();
			}

			public IPgParent Host => _oHost;

			public void LogError(string strMessage, string strDetails, bool fShow=true) {
				_oHost._oSiteView.LogError( strMessage, strDetails, fShow );
			}

			public void Notify( ShellNotify eEvent ) {
				_oHost._oSiteView.Notify( eEvent );
			}

            public IPgViewNotify EventChain => _oHost._oSiteView.EventChain;

            public object AddView(Guid guidViewType, bool fFocus) {
                throw new NotImplementedException();
            }

            public void FocusMe() {
                throw new NotImplementedException();
            }

            public void FocusCenterView() {
                throw new NotImplementedException();
            }

            public IEnumerable<IPgCommandView> EnumerateSiblings => throw new NotImplementedException();

            public uint SiteID => throw new NotImplementedException();
        }


		protected void LogError( string strMessage, string strDetails ) {
			_oSiteView.LogError( strMessage, strDetails );
		}

		public WindowSSTVHistory( IPgViewSite oViewSite, DocSSTV oDocSSTV ) {
			_oSiteView = oViewSite ?? throw new ArgumentNullException( nameof( oViewSite ) );
			_oDocSSTV  = oDocSSTV  ?? throw new ArgumentNullException( nameof( oDocSSTV  ) );

			_wmViewRxHistorySelected     = new( new SSTVWinSlot( this, ChildID.HistoryNavWindow ), _oDocSSTV.RxHistoryList );
			_wmViewRxHistory = new( new SSTVWinSlot( this, ChildID.HistoryIconsWindow ), _oDocSSTV.RxHistoryList ); 

			_wmViewRxHistorySelected    .Parent = this;
			_wmViewRxHistory.Parent = this;

			_wmViewRxHistorySelected.SetBorderOn();
		}

		public bool InitNew() {
			if( !_wmViewRxHistorySelected.InitNew() )
				return false;
			if( !_wmViewRxHistory.InitNew() ) 
				return false;

            _oLayout.Add( new LayoutControl( _wmViewRxHistorySelected, LayoutRect.CSS.None ) );
            _oLayout.Add( new LayoutControl( _wmViewRxHistory,         LayoutRect.CSS.Pixels, 230 ) );

            OnSizeChanged( new EventArgs() );

			return true;
		}

		protected override void OnSizeChanged(EventArgs e) {
			base.OnSizeChanged(e);

			_oLayout.SetRect( 0, 0, Width, Height );
			_oLayout.LayoutChildren();

            Invalidate();
		}

        public object Decorate(IPgViewSite oBaseSite, Guid sGuid) {
			try {
				if( sGuid.Equals(GlobalDecorations.Properties) ) {
					return _wmViewRxHistorySelected.Decorate( oBaseSite, sGuid );
				}
				return false;
			} catch ( Exception oEx ) {
				Type[] rgErrors = { typeof( NotImplementedException ),
									typeof( NullReferenceException ),
									typeof( ArgumentException ),
									typeof( ArgumentNullException ) };
				if( rgErrors.IsUnhandled( oEx ) )
					throw;

				LogError( "SSTV", "Couldn't create SSTV decor: " + sGuid.ToString() );
			}

            return( null );
		}

        public bool Execute(Guid sGuid) {
            return _wmViewRxHistorySelected.Execute( sGuid );
        }

        public bool Load(XmlElement oStream) {
            if( !InitNew() )
				return false;

			return true;
        }

        public bool Save(XmlDocumentFragment oStream) {
            return true;
        }
    }

    /// <summary>
	/// This viewer shows a subset of all SSTV Properties. Those for the Receiver only.
    /// </summary>
    public class WindowRxProperties : 
        WindowStandardProperties
     {
        public DocSSTV SSTVDocument { get; }

		public WindowRxProperties( IPgViewSite oViewSite, DocSSTV docSSTV ) : base( oViewSite, docSSTV.StdProperties ) {
			SSTVDocument = docSSTV ?? throw new ArgumentNullException( nameof( docSSTV ) );
		}

        public override void InitRows() {
			int[] rgShow = { 
				(int)SSTVProperties.Names.Std_Process,
				(int)SSTVProperties.Names.Rx_Mode,
				(int)SSTVProperties.Names.Rx_Width,
				(int)SSTVProperties.Names.Rx_Height,
				(int)SSTVProperties.Names.Rx_Progress,
				(int)SSTVProperties.Names.Rx_SaveDir,
				(int)SSTVProperties.Names.Rx_SaveName,
				(int)SSTVProperties.Names.Tx_TheirCall,
				(int)SSTVProperties.Names.Tx_RST
			};

			InitRows( rgShow );
        }

		// Use this for debugging if necessary.
		//protected override void OnDocumentEvent( BUFFEREVENTS eEvent ) {
		//	base.OnDocumentEvent( eEvent );
		//}
    }

	/// <summary>
	/// This view shows the single image being downloaded from the audio stream.
	/// This is the original receiver window, but now I use the integrated 
	/// rx/history window, WindowDeviceViewer
	/// </summary>
	public class WindowSoloRx : 
		ImageViewSingle, 
		IPgCommandView,
		IPgSave<XmlDocumentFragment>,
		IPgLoad<XmlElement>,
		IPgTools
	{
		public enum Tools : int {
			File = 0,
			Port = 1
		}

		public static Guid GUID { get; } = new Guid( "{5213847C-8B38-49D8-AAE2-C870F5E6FB51}" );
		public static string _strIcon =  "Play.SSTV.icons8_tv.png";

        public Guid   Catagory => GUID;
        public Image  Iconic   { get; protected set; }
        public bool   IsDirty  => false;

        protected readonly IPgViewSite _oSiteView;

		readonly  List<string>           _rgToolBox     = new() { "File", "Port" };
		protected Tools                  _eToolSelected = Tools.File;
		protected static readonly string _strBaseTitle  = "MySSTV Receive";

        DocSSTV _oDocSSTV;

		protected class SSTVWinSlot :
			IPgViewSite,
			IPgShellSite
		{
			protected readonly WindowSoloRx _oHost;

			public SSTVWinSlot( WindowSoloRx oHost ) {
				_oHost = oHost ?? throw new ArgumentNullException();
			}

			public IPgParent Host => _oHost;

			public void LogError(string strMessage, string strDetails, bool fShow=true) {
				_oHost._oSiteView.LogError( strMessage, strDetails, fShow );
			}

			public void Notify( ShellNotify eEvent ) {
				_oHost._oSiteView.Notify( eEvent );
			}

            public object AddView( Guid guidViewType, bool fFocus ) {
                return null;
            }

            public void FocusMe() {
                throw new NotImplementedException();
            }

            public void FocusCenterView() {
                throw new NotImplementedException();
            }

            public IPgViewNotify EventChain => _oHost._oSiteView.EventChain;

            public IEnumerable<IPgCommandView> EnumerateSiblings => throw new NotImplementedException();

            public uint SiteID => throw new NotImplementedException();
        }

        public string Banner {
			get { 
				StringBuilder sbBanner = new StringBuilder();

				sbBanner.Append( _strBaseTitle );
				if( _oDocSSTV.PortRxList.CheckedLine is Line oLine ) {
					sbBanner.Append( " : " );
					sbBanner.Append( oLine.ToString() );
				}

				return sbBanner.ToString();
			} 
		}

		public WindowSoloRx( IPgViewSite oSiteBase, DocSSTV oDocSSTV ) : 
			base( oSiteBase, oDocSSTV.DisplayImage ) 
		{
			_oSiteView = oSiteBase ?? throw new ArgumentNullException( "SiteBase must not be null." );
			_oDocSSTV  = oDocSSTV  ?? throw new ArgumentNullException( "DocSSTV must not be null." );

			Iconic = ImageResourceHelper.GetImageResource( Assembly.GetExecutingAssembly(), _strIcon );

			SetBorderOn();
		}

        protected override void Dispose( bool fDisposing ) {
			if( fDisposing && !_fDisposed ) {
				_oDocSSTV.PropertyChange             -= OnPropertyChange_DocSSTV;
			}
			base.Dispose( fDisposing );
        }

        public override bool InitNew() {
            if( !base.InitNew() )
				return false;

            _oDocSSTV.PropertyChange += OnPropertyChange_DocSSTV;

			return true;
        }

        /// <summary>
        /// This is our event sink for property changes on the SSTV document.
        /// </summary>
        /// <remarks>Right now just update all, but we can just update the
        /// specific property in the future. You know, a color coded property, 
        /// light red or yellow on change would be a cool feature.</remarks>
        private void OnPropertyChange_DocSSTV( SSTVEvents eProp ) {
			switch( eProp ) {
				case SSTVEvents.DownLoadFinished:
					Refresh();
					break;
				default:
					Invalidate();
					_oSiteView.Notify( ShellNotify.BannerChanged );
					break;
			}
        }

        protected override void OnGotFocus(EventArgs e) {
            base.OnGotFocus( e );

            Invalidate();
        }

        protected override void OnLostFocus(EventArgs e) {
            base.OnLostFocus(e);

            Invalidate();
        }
        
        protected override void OnMouseDown(MouseEventArgs e) {
            this.Select();
		}

        public override bool Execute( Guid sGuid ) {
			if( sGuid == GlobalCommands.Play ) {
				_oDocSSTV.ReceiveLiveBegin();
				return true;
			}
			if( sGuid == GlobalCommands.Stop ) {
				// BUG: What if we're launched with one tool and then the tool is
				//      changed midway and we hit stop. Need to sort that out.
				_oDocSSTV.ReceiveLiveStop();
			}

			if( sGuid == GlobalCommands.Save ) {
				_oDocSSTV.ReceiveSave();
				return true; // make sure you return true or a docsstv.save (settings) gets called.
			}

            return base.Execute(sGuid);
        }

		public object Decorate(IPgViewSite oBaseSite,Guid sGuid) {
			try {
				if( sGuid.Equals(GlobalDecorations.Properties) ) {
					return new WindowRxProperties( oBaseSite, _oDocSSTV );
				}
				if( sGuid.Equals( GlobalDecorations.Outline ) ) {
					return new CheckList( oBaseSite, _oDocSSTV.RxModeList );
				}
				return false;
			} catch ( Exception oEx ) {
				Type[] rgErrors = { typeof( NotImplementedException ),
									typeof( NullReferenceException ),
									typeof( ArgumentException ),
									typeof( ArgumentNullException ) };
				if( rgErrors.IsUnhandled( oEx ) )
					throw;

				LogError( "SSTV", "Couldn't create SSTV decor: " + sGuid.ToString() );
			}

            return( null );
		}

        public bool Save( XmlDocumentFragment oStream ) {
            return true;
        }

        public bool Load( XmlElement oStream ) {
            return InitNew();
        }

        public int ToolCount => _rgToolBox.Count;

        public int ToolSelect { 
			get => (int)_eToolSelected; 
			set {
				_eToolSelected = (Tools)value;

				_oSiteView.Notify( ShellNotify.ToolChanged );
			}
		}

        public string ToolName( int iTool ) {
            return _rgToolBox[iTool];
        }

        public Image ToolIcon( int iTool ) {
            return null;
        }
    }
	public enum ChildID
	{
		RxWindow,
		HistoryNavWindow,
		HistoryIconsWindow,
		TxImageChoice,
		TxImageChoices,
		TxImageComposite,
		RxImageChoices,
		None
	}

	public abstract class WindowStaggardBase : 
		SKControl, 
		IPgParent,
		IPgCommandView
	{
        protected readonly DocSSTV     _oDocSSTV;
        protected readonly IPgViewSite _oSiteView;
		protected          bool        _fDisposed;

		protected abstract string IconResource { get; }

        public IPgParent Parentage => _oSiteView.Host;
        public IPgParent Services  => Parentage.Services;
        public abstract Guid Catagory { get; }
        public Image	 Iconic    { get; protected set; }
        public bool		 IsDirty   => false;

		protected readonly LayoutStack     _oLayout = new LayoutStackVertical( 5 );
		protected readonly LayoutStaggared _rgStaggaredLayout = new (5);

		protected class SSTVWinSlot :
			IPgFileSite,
			IPgViewSite,
			IPgShellSite,
			IPgViewNotify
		{
			protected readonly WindowStaggardBase _oHost;

			public ChildID ID { get;}

			public SSTVWinSlot(WindowStaggardBase oHost, ChildID eID ) {
				_oHost = oHost ?? throw new ArgumentNullException();
				ID     = eID;
			}

			public IPgParent Host => _oHost;

			public void LogError(string strMessage, string strDetails, bool fShow=true) {
				_oHost._oSiteView.LogError( strMessage, strDetails, fShow );
			}

			public void Notify( ShellNotify eEvent ) {
				_oHost._oSiteView.Notify( eEvent );
			}

            public object AddView( Guid guidViewType, bool fFocus ) {
                return null;
            }

            public void FocusMe() {
                throw new NotImplementedException();
            }

            public void FocusCenterView() {
                throw new NotImplementedException();
            }

            public void NotifyFocused(bool fSelect) {
				_oHost.BringChildToFront( ID );
                _oHost._oSiteView.EventChain.NotifyFocused( fSelect );
            }

            public bool IsCommandKey(CommandKey ckCommand, KeyBoardEnum kbModifier) {
                return _oHost._oSiteView.EventChain.IsCommandKey( ckCommand, kbModifier );
            }

            public bool IsCommandPress(char cChar) {
                return _oHost._oSiteView.EventChain.IsCommandPress( cChar );
            }

            public IPgViewNotify EventChain => this;

            public IEnumerable<IPgCommandView> EnumerateSiblings => throw new NotImplementedException();

            public uint SiteID => throw new NotImplementedException();

            public FILESTATS FileStatus => FILESTATS.UNKNOWN;

            public Encoding FileEncoding => Encoding.Default;

            public string FilePath => _oHost._oDocSSTV.StdProperties[SSTVProperties.Names.Rx_SaveDir].ToString();

            public string FileBase => _oHost._oDocSSTV.StdProperties[SSTVProperties.Names.Rx_SaveName].ToString();
        }

        public abstract string Banner { get; }

		public WindowStaggardBase( IPgViewSite oSiteBase, DocSSTV oDocSSTV ) {
			_oSiteView = oSiteBase ?? throw new ArgumentNullException( "SiteBase must not be null." );
			_oDocSSTV  = oDocSSTV  ?? throw new ArgumentNullException( "DocSSTV must not be null." );

			Iconic = ImageResourceHelper.GetImageResource( Assembly.GetExecutingAssembly(), IconResource );
		}

        public void LogError( string strCatagory, string strDetails ) {
            _oSiteView.LogError( strCatagory, strDetails );
        }

		/// <summary>
		/// Use this function to bring one of the staggarded windows up to the front.
		/// You can also select the windows as well, but that gets a little tricky at times.
		/// </summary>
		protected abstract void BringChildToFront( ChildID eID );


        protected override void OnGotFocus(EventArgs e) {
            base.OnGotFocus( e );

			_oSiteView.EventChain.NotifyFocused(true);

			Invalidate();
        }

        protected override void OnLostFocus(EventArgs e) {
            base.OnLostFocus(e);

			_oSiteView.EventChain.NotifyFocused(false);

            Invalidate();
        }
        
        protected override void OnMouseDown(MouseEventArgs e) {
            this.Select();
		}

		public abstract bool Execute( Guid e );

		public abstract object Decorate(IPgViewSite oBaseSite,Guid sGuid);

		protected override void OnSizeChanged(EventArgs e) {
			base.OnSizeChanged(e);

			_oLayout.SetRect( 0, 0, Width, Height );
			_oLayout.LayoutChildren();

            Invalidate();
		}

		protected override void OnPaintSurface( SKPaintSurfaceEventArgs e ) {
			base.OnPaintSurface(e);

            try {
                SKSurface skSurface = e.Surface;
                SKCanvas  skCanvas  = skSurface.Canvas;

				// Need to get that StdUIColors.BGSelectedLightFocus from the StdUi at some point.
				using SKPaint skPaint = new ();
				skPaint .Color = Focused ? new SKColor(207, 234, 255) : SKColors.LightGray;
				skCanvas.DrawRect( 0, 0, Width, Height, skPaint );
            } catch( Exception oEx ) {
                Type[] rgErrors = { typeof( NullReferenceException ),
                                    typeof( ArgumentOutOfRangeException ),
                                    typeof( ArgumentNullException ),
                                    typeof( ArgumentException ),
                                    typeof( InvalidOperationException ) };
                if( rgErrors.IsUnhandled( oEx ) )
                    throw;
            }
		}
	} // End WindowStaggardBase

	/// <summary>
	/// The audio device and file receive windows are two different
	/// windows now. This is the base class for these two windows.
	/// </summary>
	public abstract class WindowRxBase : WindowStaggardBase { 
		protected ImageViewSingle _wmViewRxImg;
		protected ImageViewIcons  _wmViewRxHistory;

		public WindowRxBase( IPgViewSite oSiteBase, DocSSTV oDocSSTV ) : base( oSiteBase, oDocSSTV ) {
			Iconic = ImageResourceHelper.GetImageResource( Assembly.GetExecutingAssembly(), IconResource );

			_wmViewRxImg     = new( new SSTVWinSlot( this, ChildID.RxWindow      ),    _oDocSSTV.DisplayImage );
			_wmViewRxHistory = new( new SSTVWinSlot( this, ChildID.HistoryNavWindow ), _oDocSSTV.RxHistoryList  ); 

			_wmViewRxImg    .Parent = this;
			_wmViewRxHistory.Parent = this;

			_wmViewRxImg.SetBorderOn();
		}

        protected override void Dispose( bool fDisposing ) {
			if( fDisposing && !_fDisposed ) {
				_wmViewRxHistory.Dispose();
				_wmViewRxImg    .Dispose();

				_fDisposed = true;
			}
			base.Dispose( fDisposing );
        }

		public virtual bool InitNew() {
			if( !_wmViewRxHistory.InitNew() )
				return false;
			if( !_wmViewRxImg.InitNew() )
				return false;

			return true;
		}

		protected override void BringChildToFront( ChildID eID ) {
			switch( eID ) {
				case ChildID.RxWindow:
					_wmViewRxImg.BringToFront();
					break;
			}
		}


		public override object Decorate(IPgViewSite oBaseSite,Guid sGuid) {
			try {
				if( sGuid.Equals(GlobalDecorations.Properties) ) {
					return new WindowRxProperties( oBaseSite, _oDocSSTV );
				}
				if( sGuid.Equals( GlobalDecorations.Outline ) ) {
					return new CheckList( oBaseSite, _oDocSSTV.RxModeList );
				}
				return false;
			} catch ( Exception oEx ) {
				Type[] rgErrors = { typeof( NotImplementedException ),
									typeof( NullReferenceException ),
									typeof( ArgumentException ),
									typeof( ArgumentNullException ) };
				if( rgErrors.IsUnhandled( oEx ) )
					throw;

				LogError( "SSTV", "Couldn't create SSTV decor: " + sGuid.ToString() );
			}

            return( null );
		}
	} // End WindowRxBase

	/// <summary>
	/// Spiffy new window that shows the receive directory as icons.
	/// </summary>
	public class WindowDeviceViewer : 
		WindowRxBase,
		IPgSave<XmlDocumentFragment>,
		IPgLoad<XmlElement>
	{
		public    static          Guid   GUID { get; } = new Guid( "{955742A3-79D3-4789-B93B-B4225C641057}" );
		protected static readonly string _strIcon      = "Play.SSTV.icons8_tv.png";

		public    override Guid   Catagory     => GUID;
		protected override string IconResource => _strIcon;

		protected readonly WindowSoloImageNav _wnSoloImageNav;

        public override string Banner {
			get { 
				StringBuilder sbBanner = new StringBuilder();

				sbBanner.Append( "MySSTV Device Receive" );
				if( _oDocSSTV.PortRxList.CheckedLine is Line oLine ) {
					sbBanner.Append( " : " );
					sbBanner.Append( oLine.ToString() );
				}

				return sbBanner.ToString();
			} 
		}

		public WindowDeviceViewer( IPgViewSite oSiteBase, DocSSTV oDocSSTV ) : base( oSiteBase, oDocSSTV ) {
			_wnSoloImageNav = new( new SSTVWinSlot( this, ChildID.HistoryNavWindow   ), _oDocSSTV.RxHistoryList  );

			_wnSoloImageNav.Parent = this;

			_wmViewRxImg.SetBorderOn();
		}

        protected override void Dispose( bool fDisposing ) {
			if( fDisposing && !_fDisposed ) {
				_oDocSSTV.PropertyChange -= OnPropertyChange_DocSSTV;

				_wnSoloImageNav.Dispose();
			}
			base.Dispose( fDisposing );
        }

        public override bool InitNew() {
			if( !base           .InitNew() )
				return false;
			if( !_wnSoloImageNav.InitNew() )
				return false;

            _oDocSSTV.PropertyChange += OnPropertyChange_DocSSTV;

			_rgStaggaredLayout.Add(new LayoutControl(_wmViewRxImg,    LayoutRect.CSS.None) );
			_rgStaggaredLayout.Add(new LayoutControl(_wnSoloImageNav, LayoutRect.CSS.None) );

			_oLayout.Add( _rgStaggaredLayout );
            _oLayout.Add( new LayoutControl( _wmViewRxHistory, LayoutRect.CSS.Pixels, 220 ) );

            OnSizeChanged( new EventArgs() );
			return true;
        }

        /// <summary>
        /// Does what it sez.
        /// </summary>
        /// <remarks>This is the first time I've actually tried to devine which child is
        /// speaking to me via a Slot that's not from the shell. And I see that the children
        /// aren't required to set themselves on the site. I'll need to think about that,
        /// but for now, sort 'em all out this way.</remarks>
        protected override void BringChildToFront( ChildID eID ) {
			switch( eID ) {
				case ChildID.HistoryIconsWindow:
				case ChildID.HistoryNavWindow:
					_wnSoloImageNav.BringToFront();
					break;
				default:
					base.BringChildToFront( eID );
					break;
			}
		}

        /// <summary>
        /// This is our event sink for property changes on the SSTV document.
        /// </summary>
        /// <remarks>Right now just update all, but we can just update the
        /// specific property in the future. You know, a color coded property, 
        /// light red or yellow on change would be a cool feature.</remarks>
        private void OnPropertyChange_DocSSTV( SSTVEvents eProp ) {
			switch( eProp ) {
				case SSTVEvents.DownLoadFinished:
				case SSTVEvents.SSTVMode:
					_wmViewRxImg.BringToFront();
					break;
				default:
					_oSiteView  .Notify( ShellNotify.BannerChanged );
					break;
			}
			_wmViewRxImg.Invalidate();
        }

        public override bool Execute( Guid sGuid ) {
			if( sGuid == GlobalCommands.Play ) {
				_oDocSSTV.ReceiveLiveBegin();
				return true;
			}
			if( sGuid == GlobalCommands.Stop ) {
				_oDocSSTV.ReceiveLiveStop();
				return true;
			}
			if( sGuid == GlobalCommands.Save ) {
				_oDocSSTV.ReceiveSave();
				return true; // make sure you return true or a docsstv.save (settings) gets called.
			}
			if( sGuid == GlobalCommands.JumpPrev) { 
				_wmViewRxImg.BringToFront();
				return true;
			}
			if( sGuid == GlobalCommands.JumpNext) {
				_wnSoloImageNav.BringToFront();
				return true;
			}
			if( sGuid == GlobalCommands.JumpParent ) {
				_oDocSSTV.RxHistoryList.Execute( sGuid );
			}
			if( sGuid == GlobalCommands.Delete ) {
				if( _wnSoloImageNav.Focused ) {
					_wnSoloImageNav.Execute( sGuid );
				}
			}
			if( sGuid == GlobalCommands.Copy ) {
				if( _wnSoloImageNav.Focused ) {
					_wnSoloImageNav.Execute( sGuid );
				}
			}
			return false;
        }

        public bool Save( XmlDocumentFragment oStream ) {
            return true;
        }

        public bool Load( XmlElement oStream ) {
            return InitNew();
        }
    }

	/// <summary>
	/// Layout the rect's inside in a stagagared manner. Going to move this to
	/// the Drawing project once things settle down.
	/// </summary>
	/// <seealso cref="LayoutStack"/>
	public class LayoutStaggared : ParentRect {
		protected readonly List<LayoutRect> _rgLayout = new List<LayoutRect>();

        public LayoutStaggared( uint uiMargin ) : base( uiMargin ) {
        }

        public LayoutStaggared( CSS eUnits, uint uiMargin, uint uiTrack, float flMaxPercent ) : 
			base(eUnits, uiMargin, uiTrack, flMaxPercent)
        {
        }

        public override int Count => _rgLayout.Count;

        public override void Clear() {
            _rgLayout.Clear();
        }

        public override IEnumerator<LayoutRect> GetEnumerator() {
            return _rgLayout.GetEnumerator();
        }

        public override LayoutRect Item( int iIndex ) {
            return _rgLayout[iIndex];
        }

		public void Add( LayoutRect oRect ) {
			_rgLayout.Add(oRect);
		}

		public override bool LayoutChildren() {
			int			  iCount   = _rgLayout.Count;
			int			  iStagger = 40;
			SKPointI	  pntUL    = new SKPointI( Left, Top );
			SKPointI	  pntLR    = new SKPointI( Right, Bottom );

			for (int iNext = 0; iNext < iCount; iNext++) {
				int iPrev = iCount - iNext - 1;

				_rgLayout[iNext].SetRect( Left   + iNext * iStagger,
										  Top    + iNext * iStagger,
										  Right  - iPrev * iStagger,
										  Bottom - iPrev * iStagger );
			}

			return true;
		}
		public override uint TrackDesired(TRACK eParentAxis, int uiRail) { 
			switch( eParentAxis ) {
				case TRACK.HORIZ:
					return (uint)Width;
				case TRACK.VERT:
					return (uint)Height;
				default:
					throw new ArgumentOutOfRangeException();
			}
		}
	}
}
