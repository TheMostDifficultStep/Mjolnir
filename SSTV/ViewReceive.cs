using System;
using System.Drawing;
using System.Xml;
using System.Collections.Generic;
using System.Windows.Forms;
using System.Reflection;
using System.Text;

using SkiaSharp;
using SkiaSharp.Views.Desktop;

using Play.Drawing;
using Play.Interfaces.Embedding;
using Play.Rectangles;
using Play.Edit;
using Play.ImageViewer;
using Play.Forms;
using Play.Sound;
using Play.Controls;

namespace Play.SSTV {
	/// <summary>
	/// This viewer shows a subset of all SSTV Properties. Those for the Receiver only.
	/// </summary>
	public class WindowRxProperties : 
        WindowStandardProperties
     {
        public DocSSTV SSTVDocument { get; }

        public readonly ComboBox _ddSSTVMode   = new ComboBox(); // Set parent??
        public readonly ComboBox _ddSSTVFamily = new ComboBox();

		protected readonly Dictionary<TVFamily, int > _rgFamilyLookup = new ();

		public WindowRxProperties( IPgViewSite oViewSite, DocSSTV docSSTV ) : base( oViewSite, docSSTV.Properties ) {
			SSTVDocument = docSSTV ?? throw new ArgumentNullException( nameof( docSSTV ) );
		}

        protected override void Dispose(bool disposing) {
			if( disposing ) {
                SSTVDocument.RxModeList.CheckedEvent -= OnCheckedEvent_RxModeList;
                //SSTVDocument.PropertyChange          -= OnPropertyChange_SSTVDocument;
			}
            base.Dispose(disposing);
        }

        public override void InitRows() {
			int[] rgShow = { 
				(int)SSTVProperties.Names.Std_Process,
				(int)SSTVProperties.Names.Rx_Mode,
				(int)SSTVProperties.Names.Rx_Width,
				(int)SSTVProperties.Names.Rx_Height,
				(int)SSTVProperties.Names.Rx_Progress,
				(int)SSTVProperties.Names.Rx_SaveDir,
				(int)SSTVProperties.Names.Tx_TheirCall,
				(int)SSTVProperties.Names.Tx_RST
			};

			base.InitRows( rgShow );

			try {
				//SSTVFamilyList   oDDDocument = new SSTVFamilyList  (new WinSlot(this));

				//oDDDocument.Load( new SSTVDEM.EnumerateFamilies() );

				//PropertyInitRow((int)SSTVProperties.Names.Rx_FamilySelect,
				//				 new ViewFamilyDropDown( new WinSlot(this), oDDDocument )
				//			   );

				SSTVDocument.RxModeList.CheckedEvent += OnCheckedEvent_RxModeList;
                //SSTVDocument.PropertyChange          += OnPropertyChange_SSTVDocument;

				// Call this once to set up the populate the (mode) families.
				IEnumerator<SSTVDEM.SSTVFamily> itrFamily = SSTVDEM.EnumFamilies();
				_ddSSTVFamily.Items.Add( "Auto" );
				_rgFamilyLookup.Add( TVFamily.None, 0 );
				while( itrFamily.MoveNext() ) {
					int iMainIndex = _ddSSTVFamily.Items.Add( itrFamily.Current );
					_rgFamilyLookup.Add( itrFamily.Current._eFamily, iMainIndex );
				}
				_ddSSTVFamily.SelectedIndex = 0;
				
				PopulateRxModes();

				_ddSSTVFamily.SelectionChangeCommitted += OnSelectionChangeCommitted_Family;
				_ddSSTVFamily.AutoSize      = true;
				_ddSSTVFamily.TabIndex      = 0;
				_ddSSTVFamily.DropDownStyle = ComboBoxStyle.DropDownList;
				PropertyInitRow((int)SSTVProperties.Names.Rx_FamilySelect,
								 _ddSSTVFamily);

				_ddSSTVMode.SelectionChangeCommitted += OnSelectionChangeCommitted_Mode;
				_ddSSTVMode.AutoSize      = true;
				_ddSSTVMode.TabIndex      = 1;
				_ddSSTVMode.DropDownStyle = ComboBoxStyle.DropDownList;

				PropertyInitRow( (int)SSTVProperties.Names.Rx_ModeSelect, 
								 _ddSSTVMode );

				PropertyInitRow( (int)SSTVProperties.Names.Rx_Diagnostic, 
								 new ImageViewSingle( new WinSlot( this ), SSTVDocument.SyncImage ) );
				PropertyInitRow( (int)SSTVProperties.Names.Rx_SignalLevel, 
								 new ImageViewSingle( new WinSlot( this ), SSTVDocument.SignalLevel )  );
			} catch ( Exception oEx ) {
				Type[] rgErrors = { typeof( NullReferenceException ),
									typeof( ArgumentOutOfRangeException ),
									typeof( ApplicationException ) };
				if( rgErrors.IsUnhandled( oEx ) )
					throw;
				LogError( "Unable to set up receive mode selectors" );
			}
        }

   //     private void OnPropertyChange_SSTVDocument(SSTVEvents eProp) {
			//try { 
			//	Line oHighlight = SSTVDocument.RxModeList.HighLight;
			//	if( oHighlight != null && oHighlight.Extra is SSTVMode oMode ) {
			//		PopulateRxModes( oMode );
			//	}
			//} catch ( NullReferenceException ) {
			//}
   //     }

        /// <summary>
        /// This gets called whenever one of the dropdowns gets COMMITED by the user here.
        /// </summary>
        private void ChangeModeAtList( SSTVMode oMode ) {
			try {
				foreach( Line oLine in SSTVDocument.RxModeList ) {
					if( oLine.Extra is SSTVMode oLineMode && 
						oLineMode.LegacyMode == oMode.LegacyMode )
					{
						SSTVDocument.RxModeList.CheckedLine = oLine;
					}
				}
			} catch( Exception oEx ) {
				Type[] rgErrors = { typeof( NullReferenceException ),
									typeof( ArgumentOutOfRangeException ) };
				if( rgErrors.IsUnhandled( oEx ) )
					throw;

				LogError( "RXProperties ChangeModeAtList unexpected." );
			}
		}

        private void OnCheckedEvent_RxModeList(Line oLineChecked) {
			PopulateRxModes(oLineChecked.Extra as SSTVMode );
        }

		/// <summary>
		/// Process user event. This will generate a returning OnCheckedEvent_RxModeList
		/// event. Which should also cause the SSTVDocument to listen for the given mode.
		/// </summary>
        private void OnSelectionChangeCommitted_Mode(object sender, EventArgs e) {
			try {
				if( _ddSSTVMode.SelectedItem is SSTVMode oNewMode ) {
					ChangeModeAtList( oNewMode );
				}
			} catch( Exception oEx ) {
				Type[] rgErrors = { typeof( NullReferenceException ),
									typeof( ArgumentOutOfRangeException ) };
				if( rgErrors.IsUnhandled( oEx ) )
					throw;

				LogError( "RXProperties OnSelectionChangeCommitted_Mode unexpected." );
			}
		}

        /// <summary>
        /// This is the event we want. If the UI changes then I want to do 
        /// something about it. Else we'll use the document events to
        /// change our selected index.
        /// </summary>
        /// <seealso cref="PopulateSubModes"/>
        private void OnSelectionChangeCommitted_Family(object sender, EventArgs e) {
			try {
				if( _ddSSTVFamily.SelectedItem is SSTVDEM.SSTVFamily oNewFamily ) {
					foreach( Line oLine in SSTVDocument.RxModeList ) {
						if( oLine.Extra is SSTVMode oMode ) {
							if( oMode.Family == oNewFamily._eFamily ) {
								ChangeModeAtList( oMode );
								return;
							}
						}
					}
				} else {
					SSTVDocument.RxModeList.CheckedLine = SSTVDocument.RxModeList[0];
				}
			} catch( Exception oEx ) {
				Type[] rgErrors = { typeof( NullReferenceException ),
									typeof( ArgumentOutOfRangeException ) };
				if( rgErrors.IsUnhandled( oEx ) )
					throw;

				LogError( "RXProperties OnSelectionChangeCommitted_Family unexpected." );
			}
        }

		/// <summary>
		/// Populate the Rx mode / family dropdowns.
		/// </summary>
		/// <param name="oModeSelect">The only time this parameter is deliberatly
		/// null is during initialization. After that it's either a mode
		/// or we want to be in "auto" detect mode.</param>
        public void PopulateRxModes( SSTVMode oModeSelect = null ) {
			_ddSSTVMode.Items.Clear();

			try {
				// If I've got a new mode, select the family from that, regardless
				// if the family is already properly selected. This is ok since we
				// won't get any additional events from doing this.
				if( oModeSelect != null ) { 
					if( _rgFamilyLookup.TryGetValue( oModeSelect.Family, out int iIndex ) ) {
						_ddSSTVFamily.SelectedIndex = iIndex;
					}
				} else {
					_ddSSTVFamily.SelectedIndex = 0; // back to auto.
					_ddSSTVMode  .SelectedIndex = -1;
				}
				// By this point we should have a family, now populate the modes.
				if( _ddSSTVFamily.SelectedItem is SSTVDEM.SSTVFamily oNewFamily ) {
					foreach( Line oLine in SSTVDocument.RxModeList ) {
						if( oLine.Extra is SSTVMode oMode ) {
							if( oMode.Family == oNewFamily._eFamily ) {
								int iIndex = _ddSSTVMode.Items.Add( /*oLine*/ oMode );
								if( oModeSelect != null && oMode.LegacyMode == oModeSelect.LegacyMode )
									_ddSSTVMode.SelectedIndex = iIndex;
							}
						}
					}
				}
				if( _ddSSTVMode.SelectedIndex == -1 && _ddSSTVMode.Items.Count > 0 ) {
					_ddSSTVMode.SelectedIndex = 0;
				}
			} catch( Exception oEx ) {
				Type[] rgErrors = { typeof( NullReferenceException ),
									typeof( ArgumentOutOfRangeException ) };
				if( rgErrors.IsUnhandled( oEx ) )
					throw;

				LogError( "RXProperties PopulateRxModes unexpected." );
			}
        }

		// Uncomment this if you want to see this event get called
		// if you set the debugger on the event raise, you only get
		// to see the first sink and no others.
		//protected override void OnDocumentEvent(  BUFFEREVENTS eEvent ) {
		//	base.OnDocumentEvent( eEvent );
		//}
    }

	/// <summary>
	/// This view shows the single image being downloaded from the audio stream.
	/// This is the original receiver window, and the one I'm using as of 3/16/2023
	/// </summary>
	public class WindowSoloRx : 
		ImageViewSingle, 
		IPgCommandView,
		IPgSave<XmlDocumentFragment>,
		IPgLoad<XmlElement>,
		IPgPlayStatus
	{
		public static Guid GUID { get; } = new Guid( "{5213847C-8B38-49D8-AAE2-C870F5E6FB51}" );
		public static string _strIcon =  "Play.SSTV.Content.icons8_tv.png";

        public Guid     Catagory => GUID;
		public SKBitmap Icon { get; }
		public Image    Iconic  => null;
        public bool     IsDirty => false;

        protected readonly IPgViewSite _oSiteView;

		protected static readonly string _strBaseTitle  = "MySSTV Receive";

        DocSSTV _oDocSSTV;

        // see System.Collections.ReadOnlyCollectionBase for readonly collections.
        readonly static Keys[] _rgHandledKeys = { Keys.Shift | Keys.Right,  Keys.Shift | Keys.Left, Keys.Back,
                                                  Keys.Delete, Keys.Enter };
		protected class WinSlot :
			IPgViewSite
		{
			protected readonly WindowSoloRx _oHost;

			public WinSlot( WindowSoloRx oHost ) {
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

			Icon = SKImageResourceHelper.GetImageResource( Assembly.GetExecutingAssembly(), _strIcon );

            Array.Sort<Keys>( _rgHandledKeys );

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

        protected override bool IsInputKey(Keys keyData) {
            int iIndex = Array.BinarySearch<Keys>(_rgHandledKeys, keyData);

            if (iIndex >= 0)
                return (true);

            return base.IsInputKey( keyData );
        }

        public bool IsPlaying { get { 
			switch( _oDocSSTV.StateRx  ) {
				case DocSSTV.DocSSTVMode.DeviceRead:
				case DocSSTV.DocSSTVMode.FileRead:
					return true;
			}
			return false;
		} }

        public SKColor BusyLight { get {
			switch( _oDocSSTV.StateRx  ) {
				case DocSSTV.DocSSTVMode.DeviceRead:
					return SKColors.LightGreen;
				case DocSSTV.DocSSTVMode.FileRead:
					return SKColors.LightSalmon;
			}
			return SKColors.Empty;
		} }


        public int PercentCompleted => throw new NotImplementedException();

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
        
        protected override void OnKeyDown(KeyEventArgs e) {
            if( this.IsDisposed )
                return;

            e.Handled = true;
			int iDistance = e.Shift ? 3 : 10; // If shift pressed, move slower.

            switch( e.KeyCode ) {
                case Keys.Right:
					_oDocSSTV.PostBGMessage( TVMessage.Message.Intercept, -iDistance );
					break;
                case Keys.Left:
					_oDocSSTV.PostBGMessage( TVMessage.Message.Intercept, +iDistance );
					break;
                case Keys.Enter:
                    break;
            }
        }

        protected override void OnMouseDown(MouseEventArgs e) {
            this.Select();
		}

        protected override void OnMouseWheel(MouseEventArgs e) {
            base.OnMouseWheel(e);

			bool fShiftPressed = (Control.ModifierKeys & Keys.Shift) != Keys.None;
			int  iDistance     = fShiftPressed ? 15 : 30; // in 100ths

			if(  e.Delta < 0 )
				iDistance *= -1;

			_oDocSSTV.PostBGMessage( TVMessage.Message.Frequency, iDistance );
        }

        public override bool Execute( Guid sGuid ) {
			if( sGuid == GlobalCommands.Recycle ) {
				_oDocSSTV.ResetMode();
				return true;
			}
			if( sGuid == GlobalCommands.Delete ) {
				_oDocSSTV.PostBGMessage( TVMessage.Message.ClearImage );
				return true;
			}
			if( sGuid == GlobalCommands.Play ) {
				_oDocSSTV.ReceiveLiveBegin();
				return true;
			}
			if( sGuid == GlobalCommands.Stop ) {
				// BUG: What if we're launched with one tool and then the tool is
				//      changed midway and we hit stop. Need to sort that out.
				_oDocSSTV.ReceiveLiveStop();
				return true;
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
				//if( sGuid.Equals( GlobalDecorations.Outline ) ) {
				//	return new CheckList( oBaseSite, _oDocSSTV.RxModeList );
				//}
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
        public SKBitmap	 Icon    { get; protected set; }
		public Image Iconic => null;
        public bool		 IsDirty   => false;

		protected readonly LayoutStack     _oLayout = new LayoutStackVertical() { Spacing = 5 };
		protected readonly LayoutStaggared _rgStaggaredLayout = new () { Spacing = 5 };

		protected class WinSlot :
			IPgFileSite,
			IPgViewSite,
			IPgViewNotify
		{
			protected readonly WindowStaggardBase _oHost;

			public ChildID ID { get;}

			public WinSlot(WindowStaggardBase oHost, ChildID eID ) {
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

            public void NotifyFocused(bool fSelect) {
				if( fSelect == true ) {
					_oHost.BringChildToFront( ID );
					_oHost._oSiteView.EventChain.NotifyFocused( fSelect );
				}
            }

            public bool IsCommandKey(CommandKey ckCommand, KeyBoardEnum kbModifier) {
                return _oHost._oSiteView.EventChain.IsCommandKey( ckCommand, kbModifier );
            }

            public bool IsCommandPress(char cChar) {
                return _oHost._oSiteView.EventChain.IsCommandPress( cChar );
            }

            public IPgViewNotify EventChain => this;

            public FILESTATS FileStatus => FILESTATS.UNKNOWN;

            public Encoding FileEncoding => Encoding.Default;

            public string FilePath => _oHost._oDocSSTV.Properties[SSTVProperties.Names.Rx_SaveDir];

            public string FileName => String.Empty;
        }

        public abstract string Banner { get; }

		public WindowStaggardBase( IPgViewSite oSiteBase, DocSSTV oDocSSTV ) {
			_oSiteView = oSiteBase ?? throw new ArgumentNullException( "SiteBase must not be null." );
			_oDocSSTV  = oDocSSTV  ?? throw new ArgumentNullException( "DocSSTV must not be null." );

			Icon = oDocSSTV.CreateIconic( IconResource );
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

		public WindowRxBase( IPgViewSite oSiteBase, DocSSTV oDocSSTV ) : 
			base( oSiteBase, oDocSSTV ) 
		{
			_wmViewRxImg     = new( new WinSlot( this, ChildID.RxWindow      ),    _oDocSSTV.DisplayImage );
			_wmViewRxHistory = new( new WinSlot( this, ChildID.HistoryNavWindow ), _oDocSSTV.RxHistoryList  ); 

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
				// Property pane contains two dropdowns for this now.
				//if( sGuid.Equals( GlobalDecorations.Outline ) ) {
				//	return new CheckList( oBaseSite, _oDocSSTV.RxModeList );
				//}
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

        public override bool Execute(Guid e) {
			if( e == GlobalCommands.Recycle ) {
				_oDocSSTV.ResetMode();
				return true;
			}
            return false;
        }
    } // End WindowRxBase

	/// <summary>
	/// Spiffy new window that shows the receive directory as icons. But not
	/// the main window I'm using at present 3/16/2023
	/// </summary>
	public class WindowDeviceViewer : 
		WindowRxBase,
		IPgSave<XmlDocumentFragment>,
		IPgLoad<XmlElement>
	{
		public    static   Guid   GUID { get; } = new Guid( "{955742A3-79D3-4789-B93B-B4225C641057}" );

		public    override Guid   Catagory     => GUID;
		protected override string IconResource => "Play.SSTV.Content.icons8_tv.png";

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
			_wnSoloImageNav = new( new WinSlot( this, ChildID.HistoryNavWindow   ), _oDocSSTV.RxHistoryList  );

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
				case SSTVEvents.ModeChanged:
					_wmViewRxImg.BringToFront();
					break;
				case SSTVEvents.DownLoadTime:
					break;
				default:
					_oSiteView.Notify( ShellNotify.BannerChanged );
					break;
			}
			_wmViewRxImg.Invalidate();
        }

        public override bool Execute( Guid sGuid ) {
			if( sGuid == GlobalCommands.Delete ) {
				_oDocSSTV.PostBGMessage( TVMessage.Message.ClearImage );
				return true;
			}
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

        public LayoutStaggared() : base() {
        }

        public LayoutStaggared( CSS eUnits, uint uiTrack, float flMaxPercent ) : 
			base(eUnits, uiTrack, flMaxPercent)
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
