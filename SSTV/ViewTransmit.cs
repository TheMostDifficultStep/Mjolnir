using System;
using System.Xml;
using System.Collections.Generic;
using System.Text;
using System.Windows.Forms;
using System.Threading.Tasks;
using System.Drawing;
using System.Reflection;

using SkiaSharp;
using SkiaSharp.Views.Desktop;

using Play.Interfaces.Embedding;
using Play.Rectangles;
using Play.Edit;
using Play.ImageViewer;
using Play.Sound;
using Play.Forms;
using Play.Drawing;

namespace Play.SSTV {
    /// <summary>
	/// Show a subset of the SSTV Properties. The Transmit one's only.
    /// </summary>
    public class ViewTxProperties : 
        WindowStandardProperties
     {
        public static Guid GUID {get;} = new Guid("{80C855E0-C2F6-4641-9A7C-B6A8A53B3FDF}");
		protected readonly DocSSTV            _oDocSSTV;
		protected readonly ViewTransmitDeluxe _oTxView;

        public readonly ComboBox _ddSSTVMode   = new ComboBox();
        public readonly ComboBox _ddSSTVFamily = new ComboBox();
		public readonly ComboBox _ddTemplates  = new ComboBox();

		public ViewTxProperties( IPgViewSite oViewSite, DocSSTV oDocSSTV ) : base( oViewSite, oDocSSTV.Properties )
		{
			_oDocSSTV = oDocSSTV; // No use throwing since base will throw nullrefexception first.
		}

		public ViewTxProperties( IPgViewSite oViewSite, ViewTransmitDeluxe oWindow, DocSSTV oDocSSTV ) : base( oViewSite, oDocSSTV.Properties )
		{
			_oDocSSTV = oDocSSTV; // No use throwing since base will throw nullrefexception first.
			_oTxView  = oWindow ?? throw new ArgumentNullException( "Owning Tx window must not be null" );
		}

		public readonly static int[] Subset = new int[] { 
			(int)SSTVProperties.Names.Tx_Progress,
			(int)SSTVProperties.Names.Rx_Mode,
			(int)SSTVProperties.Names.Tx_SrcDir,
			(int)SSTVProperties.Names.Tx_SrcFile,
			(int)SSTVProperties.Names.Tx_TheirCall,
			(int)SSTVProperties.Names.Tx_RST,
			(int)SSTVProperties.Names.Tx_Message
		};

		/// <summary>
		/// Might want to keep an eye on the Image property. It probably will behave itself as
		/// well as any editwindow would but, it is new as of 6/10/2022 and not fully vetted.
		/// Else, consider dealing with it in dispose. But I really dont think that'll be necessary.
		/// </summary>
        public override void InitRows() {
            base.InitRows(Subset);

			if( _oTxView != null ) {
				try {
					PropertyInitRow( (int)SSTVProperties.Names.Tx_LayoutSelect, 
									 _ddTemplates );
					// Call this once to set up the mode families.
					SSTVDEM.SSTVFamily oPD = null;
					foreach( SSTVDEM.SSTVFamily oFamily in new SSTVDEM.EnumerateFamilies() ) {
						int iMainIndex = _ddSSTVFamily.Items.Add( oFamily );
						if( oFamily._eFamily == TVFamily.PD ) {
							oPD = oFamily;
							_ddSSTVFamily.SelectedIndex = iMainIndex;
						}
					}
					if( oPD == null && _ddSSTVFamily.Items[0] is SSTVDEM.SSTVFamily oDefault ) {
						oPD = oDefault;
						_ddSSTVFamily.SelectedIndex = 0;
					}

					_ddSSTVFamily.SelectedIndexChanged += OnSelectedFamilyChanged;
					_ddSSTVFamily.AutoSize      = true;
					_ddSSTVFamily.TabIndex      = 0;
					_ddSSTVFamily.DropDownStyle = ComboBoxStyle.DropDownList;
					PropertyInitRow( (int)SSTVProperties.Names.Tx_FamilySelect, 
									 _ddSSTVFamily );

					_ddSSTVMode.SelectedIndexChanged += OnSelectedIndexChanged_ModeDropDown;
					_ddSSTVMode.AutoSize      = true;
					_ddSSTVMode.TabIndex      = 1;
					_ddSSTVMode.DropDownStyle = ComboBoxStyle.DropDownList;

					PropertyInitRow( (int)SSTVProperties.Names.Tx_ModeSelect, 
									 _ddSSTVMode );

					_oTxView.PopulateSubModes( oPD );

					foreach( Line oLine in _oDocSSTV.TemplateList ) {
						_ddTemplates.Items.Add( oLine );
					}
					_ddTemplates.SelectedIndexChanged += OnSelectedIndexChanged_TemplateDD;
					_ddTemplates.SelectedIndex = 0;
					_ddTemplates.AutoSize      = true;
					_ddTemplates.Name          = "Mode Sub Select";
					_ddTemplates.TabIndex      = 2;
					_ddTemplates.DropDownStyle = ComboBoxStyle.DropDownList;

				  //_oDocSSTV.TemplateList.CheckedEvent += OnCheckedEvent_TemplateList;
				  //desirable but we loop forever responding to SelectedIndexChanged
				  //events. Need to sort that out.
				} catch ( Exception oEx ) {
					Type[] rgErrors = { typeof( NullReferenceException ),
										typeof( ArgumentOutOfRangeException ) };
					if( rgErrors.IsUnhandled( oEx ) )
						throw;
					LogError( "Unable to set up transmission modes selectors" );
				}
			}

			PropertyInitRow( (int)SSTVProperties.Names.Rx_Window, 
							 new ImageViewSingle( new WinSlot( this ), _oDocSSTV.DisplayImage )  );

        }

        /// <summary>
        /// Normally, we want to get the document message that the checked
        /// line changed. But since the dropdown selectes itself we need
        /// to ignore the checked event later, else we get in an infinite
        /// loop.
        /// </summary>
        /// <seealso cref="PopulateSubModes"/>
        private void OnSelectedFamilyChanged(object sender, EventArgs e) {
            if( sender is ComboBox oFamilyCombo ) { 
				if( _ddSSTVMode.SelectedItem is SSTVMode oDDListMode ) {
					foreach( Line oLine in _oDocSSTV.TxModeList ) {
						if( oLine.Extra is SSTVMode oTxListMode &&
							oTxListMode.LegacyMode == oDDListMode.LegacyMode ) 
						{
							_oDocSSTV.TxModeList.CheckedLine = oLine;
						}
					}
				}
				_oTxView.PopulateSubModes( oFamilyCombo.SelectedItem as SSTVDEM.SSTVFamily );
            }
        }

        private void OnSelectedIndexChanged_ModeDropDown(object sender, EventArgs e) {
			if( _ddSSTVMode.SelectedItem is SSTVMode oNewMode ) {
				_oDocSSTV.TransmitModeSelection = oNewMode;
				_oDocSSTV.RenderComposite();
			}
        }

        /// <summary>
        /// Still need to sort out the messaging so we can keep all the
        /// selected items in sync.
        /// </summary>
        private void OnSelectedIndexChanged_TemplateDD(object sender, EventArgs e) {
            if( sender is ComboBox ddTemplates ) {
                _oDocSSTV.TemplateList.CheckedLine =
                    _oDocSSTV.TemplateList[ ddTemplates.SelectedIndex ];
            }
		    _oDocSSTV.RenderComposite();
        }

		/// <summary>
		/// It's a little clunky. But if any column in the TxProps get's a
		/// Enter key. Let's re-render the composite image. Would be nice to
		/// use my 2 second rule. But that get's blasted by the % image rec'd
		/// feature. Still working on this.
		/// </summary>
        protected override bool ProcessCmdKey( ref Message msg, Keys keyData )	
        {
            if( this.IsDisposed )
                return false;

            const int WM_KEYDOWN = 0x100;
               
            if( msg.Msg == WM_KEYDOWN ) {
                switch(keyData) {
                    case Keys.Enter:
						_oDocSSTV.RenderComposite();
						return true;
				}
			}

			return base.ProcessCmdKey( ref msg, keyData );
		}
    }

	/// <summary>
	/// This is a old view so we can select a transmit image. Basically a slightly
	/// motified directory viewer. Not in use currently.
	/// </summary>
	public class WinTransmitSolo: 
		WindowSoloImageNav 
	{
		public static Guid GUID { get; } = new Guid( "{5BC25D2B-3F4E-4339-935C-CFADC2650B35}" );

        public override Guid   Catagory => GUID;

        public override string Banner {
			get { 
				StringBuilder sbBanner = new StringBuilder();

				sbBanner.Append("MySSTV Transmit Basic");
				if( _oDocSSTV.PortTxList.CheckedLine is Line oLine ) {
					sbBanner.Append( " : " );
					sbBanner.Append( oLine.ToString() );
				}

				return sbBanner.ToString();
			} 
		}

        DocSSTV _oDocSSTV;

		public WinTransmitSolo( IPgViewSite oSiteBase, DocSSTV oDocSSTV ) : base( oSiteBase, oDocSSTV.TxImageList ) {
			_oDocSSTV = oDocSSTV ?? throw new ArgumentNullException( "oDocSSTV must not be null." );
		}

        protected override void Dispose( bool fDisposing )
        {
			if( fDisposing && !_fDisposed ) {
				_oDocSSTV.PropertyChange -= OnPropertyChange_DocSSTV;
			}
			base.Dispose( fDisposing );
        }

        public override bool InitNew()
        {
            if( !base.InitNew() )
				return false;

			ToolSelect = 0; // Crude but should be sufficient to freeze Skywalker...
            _oDocSSTV.PropertyChange     += OnPropertyChange_DocSSTV;
			_oDocSSTV.Send_TxImageAspect += OnTxImgAspect_DocSSTV;

			Aspect   = _oDocSSTV.TxResolution;
			DragMode = DragMode.FixedRatio;

			return true;
        }

        private void OnPropertyChange_DocSSTV( SSTVEvents eProp )
        {
            switch( eProp ) {
				case SSTVEvents.DownLoadTime:
					Invalidate();
					break;
			}
        }

		private void OnTxImgAspect_DocSSTV( SKPointI skAspect ) {
			Aspect = _oDocSSTV.TxResolution;
		}

        public override bool Execute( Guid sGuid ) {
			//if( sGuid == GlobalCommands.Play ) {
			//	// Need to update to new Deluxe window way of setting up the transmit.
			//    _oDocSSTV.TransmitBegin( null ); 
			//}
			if( sGuid == GlobalCommands.Stop ) {
				_oDocSSTV.TransmitStop();
			}

            return base.Execute( sGuid );
        }

        public override object Decorate( IPgViewSite oBaseSite, Guid sGuid ) {
			if( sGuid.Equals(GlobalDecorations.Properties) ) {
				return new ViewTxProperties( oBaseSite, _oDocSSTV );
			}
			// TODO: Turn back one when useful.
			//if( sGuid.Equals( GlobalDecorations.Outline ) ) {
			//	return new CheckList( oBaseSite, _oDocSSTV.TxModeList );
			//}
			if( sGuid.Equals( GlobalDecorations.Options ) ) {
				return new ImageViewIcons( oBaseSite, _oDocSSTV.TxImageList );
			}
            return base.Decorate( oBaseSite, sGuid );
        }
    }

	public class ToolInfo {
		public ToolInfo( string strLabel, Guid guidID ) {
			_strToolName = strLabel;
			_guidID   = guidID;
		}

		readonly public string   _strToolName;
		readonly public Guid     _guidID;

		public SKBitmap Icon { get; set; }
	}

    public static class TransmitCommands {
        public static readonly Guid Color     = new Guid( "{B3198F66-7698-4DFB-8B31-9643372B2B3E}" );
        public static readonly Guid Move      = new Guid( "{179898AD-1823-4F0E-BF27-11456C1EA8C8}" );
		public static readonly Guid Resize    = new Guid( "{84F921E1-BFB5-4BD7-9814-C53D48C90D1E}" );
        public static readonly Guid Text      = new Guid( "{C7F1DADB-A0A4-479C-B193-B38AFAEE5AB6}" );
      //public static readonly Guid Gallary   = new Guid( "{94975898-5AC1-427C-85CD-9E516646115D}" );
      //public static readonly Guid PnP       = new Guid( "{A1BB369C-4E73-4248-A6E1-07C5466C818C}" );
	  //public static readonly Guid Templates = new Guid( "{FE683CA1-1068-4BA0-A84E-CFE35900A06E}" );
      //public static readonly Guid Mode      = new Guid( "{56797520-C603-417C-858A-EF532E0652D2}" );
	}

	public static class ReceiveCommands {
        public static readonly Guid Mode      = new Guid( "{37BC4B62-5141-410A-B420-2C16983E3859}" );
	}
	
	/// <summary>
	/// This is the Toolbar adornment. The Tools buttons.
	/// </summary>
	public class WinTransmitTools : ButtonBar {
		IPgTools2 _oTools;
		public WinTransmitTools( IPgViewSite oSite, Editor oDoc, IPgTools2 oTools ) : base( oSite, oDoc ) {
			_oTools = oTools ?? throw new ArgumentException( nameof( oTools ) );
		}

        public override bool InitNew() {
            if( !base.InitNew() )
				return false;

            _oTools.ToolSelectChanged += OnToolSelectChanged;

			return true;
        }

        private void OnToolSelectChanged(object sender, int iIndex) {
            Invalidate();
        }

        protected override void OnTabLeftClicked(object ID) {
            if( ID is Line oLine ) {
				_oTools.ToolSelect = oLine.At;
			}
        }

        public override SKBitmap TabIcon( object ob) {
            if( ob is Line oLine ) {
                return ((ToolInfo)oLine.Extra).Icon;
            }

            throw new ArgumentException( "Argument must be of type : Line" );
        }

        public override SKColor TabBackground( object oID ) {
            SKColor skBG = _oStdUI.ColorsStandardAt( StdUIColors.BGReadOnly );

            if( oID is Line oLine ) {
                if( oLine == HoverTab?.Extra ) 
                    return SKColors.LightYellow;

                if( _oTools.ToolSelect == oLine.At )
                    return SKColors.LightCyan;

                // Need to distinguish between our window being part of the focus
                // chain or not.
            }
            return skBG;
        }

        public override SKColor TabStatus( object oID ) {
            if( oID is Line oLine ) {
                if( _oTools.ToolSelect == oLine.At )
                    return SKColors.Blue;
                return TabBackground( oLine.At );
            }
            
            return SKColors.White;
        }

    }

	/// <summary>
	/// This window supports a popup image selector, to select the portion of an
	/// image you wish to show. If you don't use it just the current image filling
	/// the screen is shown.
	/// </summary>
    public class ViewTransmitDeluxe : 
		SKControl,
		IPgParent,
		IPgCommandView,
		IPgSave<XmlDocumentFragment>,
		IPgLoad<XmlElement>,
		IPgTools2,
		IPgPlayStatus
	{
		public static Guid GUID { get; } = new Guid( "{3D6FF540-C03C-468F-84F9-86E3DE75F6C2}" );

        protected readonly DocSSTV     _oDocSSTV;
        protected readonly IPgViewSite _oSiteView;
		protected          bool        _fDisposed;
		protected readonly LayoutStack _oLayout = new LayoutStackVertical() { Spacing = 5 };

        public IPgParent Parentage => _oSiteView.Host;
        public IPgParent Services  => Parentage.Services;
        public SKBitmap	 Icon    { get; protected set; }
		public Image     Iconic    => null;
        public bool		 IsDirty   => false;
        public    Guid   Catagory  => GUID;
        protected string IconResource => "Play.SSTV.Content.icons8_camera.png";

		protected readonly ImageViewSingle  _wmTxImageComposite; 
		protected readonly ImageViewIcons   _wmTxViewChoices;
		protected readonly ImageViewIcons   _wmRxViewChoices;
		protected          WindowTxTools    _wmToolOptions;
		protected          ViewTxProperties _wmTxProperties;

		protected readonly Editor _rgToolIcons;
		protected          int    _iToolSelected = -1;

		protected bool     _fColorDialogUp = false;

        public event ToolEvent ToolSelectChanged; // Implements IPgTool2

        public string Banner {
			get { 
				StringBuilder sbBanner = new StringBuilder();

				sbBanner.Append("MySSTV Transmit Deluxe");
				if( _oDocSSTV.PortTxList.CheckedLine is Line oLine ) {
					sbBanner.Append( " : " );
					sbBanner.Append( oLine.ToString() );
				}

				return sbBanner.ToString();
			} 
		}

		public DocSSTV Document => _oDocSSTV;

		protected class SSTVWinSlot :
			IPgFileSite,
			IPgViewSite,
			IPgViewNotify
		{
			protected readonly ViewTransmitDeluxe _oHost;

			public ChildID ID { get;}

			public SSTVWinSlot(ViewTransmitDeluxe oHost, ChildID eID ) {
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

        public ViewTransmitDeluxe( IPgViewSite oSiteView, DocSSTV oDocSSTV )  {
			_oSiteView = oSiteView ?? throw new ArgumentNullException( nameof( oSiteView ) );
			_oDocSSTV  = oDocSSTV  ?? throw new ArgumentNullException( nameof( oDocSSTV  ) );

			_wmTxViewChoices    = new ImageViewIcons    ( new SSTVWinSlot( this, ChildID.TxImageChoices ),   oDocSSTV.TxImageList );
			_wmTxImageComposite = new ImageViewSingle   ( new SSTVWinSlot( this, ChildID.TxImageComposite ), oDocSSTV.TxBitmapComp );
			_wmRxViewChoices    = new ImageViewIcons    ( new SSTVWinSlot( this, ChildID.RxImageChoices ),   oDocSSTV.RxHistoryList );

			_wmTxViewChoices   .Parent = this;
			_wmTxImageComposite.Parent = this;
			_wmRxViewChoices   .Parent = this;


			_rgToolIcons = new Editor( new SSTVWinSlot( this, ChildID.None ) );
			Icon = SKImageResourceHelper.GetImageResource( Assembly.GetExecutingAssembly(), IconResource );
		}

        protected override void Dispose( bool fDisposing ) {
			if( fDisposing && !_fDisposed ) {
				_wmTxViewChoices   .Dispose();
				_wmTxImageComposite.Dispose();
				_wmRxViewChoices   .Dispose();

				_oDocSSTV.Properties.SubmitEvent -= OnSubmitEvent_SSTVProperties;

				_fDisposed = true;
			}
			base.Dispose( fDisposing );
        }

        public bool InitNew() {
			if( !_wmTxViewChoices   .InitNew() )
				return false;
			if( !_wmTxImageComposite.InitNew() )
				return false;
			if( !_wmRxViewChoices   .InitNew() )
				return false;

			InitTools();

			LayoutStack oHBLayout = new LayoutStackHorizontal( 220, 30 ) { Spacing = 5 };

			oHBLayout.Add( new LayoutControl( _wmTxViewChoices, LayoutRect.CSS.Percent, 70 ) );
			oHBLayout.Add( new LayoutControl( _wmRxViewChoices, LayoutRect.CSS.Percent, 30 ) );

			_oLayout .Add( new LayoutControl( _wmTxImageComposite, LayoutRect.CSS.None) );
            _oLayout .Add( oHBLayout );

            OnSizeChanged( new EventArgs() );
            _oDocSSTV.Properties.SubmitEvent += OnSubmitEvent_SSTVProperties;

			return true;
        }

        private void OnSubmitEvent_SSTVProperties(int[] obj) {
			int[] rgReCompose = {			
				(int)SSTVProperties.Names.Tx_MyCall,
				(int)SSTVProperties.Names.Tx_TheirCall,
				(int)SSTVProperties.Names.Tx_RST,
				(int)SSTVProperties.Names.Tx_Message 
			};

			int iRecompose = 0;
			foreach( int iProperty in rgReCompose ) {
				foreach( int i in ViewTxProperties.Subset ) {
					if( iProperty == i ) {
						++iRecompose;
					}
				}
			}

			if( iRecompose > 0 ) {
				_oDocSSTV.RenderComposite();
			}
        }

        protected override void OnSizeChanged(EventArgs e) {
			base.OnSizeChanged(e);

			_oLayout.SetRect( 0, 0, Width, Height );
			_oLayout.LayoutChildren();

            //Invalidate();
		}

        public void InitTools() {
			Dictionary< string, ToolInfo > rgIcons = new Dictionary<string, ToolInfo>();

			rgIcons.Add( "Color",     new ToolInfo( "icons8-color-wheel-2-48.png", TransmitCommands.Color   ));
			rgIcons.Add( "Move",      new ToolInfo( "icons8-move-48.png",		   TransmitCommands.Move    ));
			rgIcons.Add( "Resize",    new ToolInfo( "icons8-selection-60.png",     TransmitCommands.Resize  ));
			rgIcons.Add( "Text",      new ToolInfo( "icons8-text-64.png",		   TransmitCommands.Text    ));
		  //rgIcons.Add( "Gallery",   new ToolInfo( "icons8-gallery-64.png",	   TransmitCommands.Templates ));
		  //rgIcons.Add( "PnP",       new ToolInfo( "icons8-download-64.png",	   TransmitCommands.PnP     ));
		  //rgIcons.Add( "Templates", new ToolInfo( "icons8-upload-64.png",        TransmitCommands.Gallary ));
		  //rgIcons.Add( "Mode",      new ToolInfo( "icons8-audio-wave-48.png",    TransmitCommands.Mode    ));

			foreach( KeyValuePair<string,ToolInfo> oPair in rgIcons ) {
				Line     oLine = _rgToolIcons.LineAppend( oPair.Key, false );
				ToolInfo oInfo = oPair.Value;

				oInfo.Icon = _oDocSSTV.CreateIconic( "Play.SSTV.Content.TxWin." + oInfo._strToolName );
				oLine.Extra = oInfo;

				if( oInfo._guidID == TransmitCommands.Resize ) {
					_iToolSelected = oLine.At;
				}
			}
		}

		protected void LogError( string strMsg ) {
			_oSiteView.LogError( "Transmit Image", strMsg );
		}

        public int ToolCount => _rgToolIcons.ElementCount;

        public int ToolSelect { 
			get => _iToolSelected; 
			set {
				if( value < 0 || value > _rgToolIcons.ElementCount )
					throw new ArgumentOutOfRangeException(); 

				// BUG: Need a backdoor so we know the difference between programmatic
				// tool change and UI tool change.
				_iToolSelected = value;

				if( _rgToolIcons[value].Extra is ToolInfo oToolInfo ) {
					Execute( oToolInfo._guidID );
					// BUG: Hack experiment.
					if( _wmToolOptions != null )
						_wmToolOptions.Execute( oToolInfo._guidID  );
				}

				_oSiteView.Notify( ShellNotify.ToolChanged );
				ToolSelectChanged?.Invoke( this, value );
			}
		}

        public string ToolName(int iTool) {
			try {
				return _rgToolIcons[iTool].ToString();
			} catch( ArgumentOutOfRangeException ) {
				return "[unknown]";
			}
        }

		/// <summary>
		/// Can't implement this yet since my tools are SKImage types.
		/// </summary>
        public Image ToolIcon(int iTool) {
            return null;
        }

        public bool Execute( Guid sGuid ) {
			if( sGuid == GlobalCommands.Play ) {
				if( _oDocSSTV.RenderComposite() ) {
					_oDocSSTV.TransmitBegin( _wmTxProperties._ddSSTVMode.SelectedItem as SSTVMode ); 
				}
				return true;
			}
			if( sGuid == GlobalCommands.Stop ) {
				_oDocSSTV.TransmitStop();
				return true;
			}

            if( sGuid == GlobalCommands.StepLeft ) {
                _oDocSSTV.TxImageList.Next( -1 );
                return( true );
            }
            if( sGuid == GlobalCommands.StepRight ) {
                _oDocSSTV.TxImageList.Next( +1 );
                return( true );
            }
            if( sGuid == GlobalCommands.JumpParent ) {
                _oDocSSTV.TxImageList.DirectoryNext( 0 );
                return( true );
            }
            if( sGuid == GlobalCommands.JumpPrev ) {
               _oDocSSTV.TxImageList. DirectoryNext( -1 );
                return( true );
            }
            if( sGuid == GlobalCommands.JumpNext ) {
                _oDocSSTV.TxImageList.DirectoryNext( +1 );
                return( true );
            }
			// This is super cool but clunky.
			if( sGuid == TransmitCommands.Color ) {
				ShowColorDialog();
 			}
			if( sGuid == TransmitCommands.Resize ) {
				// This doesn't prevent us from opening another, but it's not a big deal
				// and I think I can live with it for now. TODO: Check if close ok even if
				// the dialog is open.
				WindowImageResize oDialog = new ( new SSTVWinSlot( this, ChildID.RxWindow ), _oDocSSTV );
				oDialog.InitNew();
				oDialog.Show();
				return true;
			}
			return false;
        }

		/// <summary>
		/// This helps get the dialog to come up on top. It's still possible to get it to
		/// fall behind the main window b/c we're running in a different thread than the window
		/// we launched in, so we can't set the owner. The Dialog will go behind if you press
		/// the color button again when the warning that the dialog is already open comes up.
		/// That causes the dialog to fall to the back and get lost. 
		/// </summary>
		/// <remarks>The best solution will be to make a non blocking async dialog.</remarks>
		protected class MyColorDialog : ColorDialog {
			const int WM_INITDIALOG = 0x0110;

            protected override IntPtr HookProc(IntPtr hWnd, int msg, IntPtr wparam, IntPtr lparam) {
				switch(msg)	{
					case WM_INITDIALOG:
						Edit.User32.SetForegroundWindow( hWnd );
						break;
				}
                return base.HookProc(hWnd, msg, wparam, lparam);
            }
        }

		/// <summary>
		/// Hacky way to get a color dialog up. I'll have to make a custom one in the future.
		/// ShowDialog() blocks, so we run it in a seperate thread.
		/// </summary>
		public async void ShowColorDialog() {
			if( _fColorDialogUp ) {
				LogError( "Color Dialog is already open" );
				return;
			}

			_fColorDialogUp = true;

			SKColor skResult = SKColors.Red;
			bool    fResult  = false;

			Action oTransmitAction = delegate () {
				ColorDialog colorDlg = new MyColorDialog();  
				colorDlg.AllowFullOpen  = true; 
				colorDlg.FullOpen       = true;
				colorDlg.AnyColor       = true;  
				colorDlg.SolidColorOnly = false;  
				colorDlg.Color          = Color.Red;
  
				fResult = colorDlg.ShowDialog() == DialogResult.OK;

				if( fResult ) {  
					skResult = new SKColor( colorDlg.Color.R, colorDlg.Color.G, colorDlg.Color.B, colorDlg.Color.A );
				}
			};

			Task oTask = new Task( oTransmitAction );

			oTask.Start();

			await oTask;

			if(fResult == true ) {
				_oDocSSTV.ForeColor = skResult;
				_oDocSSTV.RenderComposite();
			}
			_fColorDialogUp = false;
		}

        public object Decorate( IPgViewSite oBaseSite, Guid sGuid ) {
			if( sGuid.Equals(GlobalDecorations.Properties) ) {
				_wmTxProperties = new ViewTxProperties( oBaseSite, this, _oDocSSTV );
				return _wmTxProperties;
			}
			//if( sGuid.Equals( GlobalDecorations.Outline ) ) {
			//	//return new CheckList( oBaseSite, _oDocSSTV.TxModeList );
			//	//return new CheckList( oBaseSite, _oDocSSTV.TemplateList ) { ReadOnly = true }; // We'll be read/write in the future.
			//	return new EditWindow2( oBaseSite, _oDocSSTV.TxBitmapComp.Layers ) { ReadOnly = true };
			//}
			//if( sGuid.Equals( GlobalDecorations.Options ) ) {
			//	//return new CheckList( oBaseSite, _oDocSSTV.TemplateList ) { ReadOnly = true }; // We'll be read/write in the future.
			//	// BUG: This is super hacky, but just try for now. Since the addornment
			//	//      is handled by the shell, it can be closed and we have a zombie.
			//	_wmToolOptions = new WindowTxTools( oBaseSite, _oDocSSTV );
			//	return _wmToolOptions;
			//}
			if( sGuid.Equals( GlobalDecorations.ToolIcons ) ) {
				return new WinTransmitTools( oBaseSite, _rgToolIcons, this );
			}
			return null;
        }

        public bool IsPlaying { get { 
			return  _oDocSSTV.StateTx;
		} }

        public SKColor BusyLight { get {
			if( _oDocSSTV.StateTx  ) {
				return SKColors.OrangeRed;
			}
			return SKColors.Empty;
		} }

        public bool Load(XmlElement oStream) {
			if( !InitNew() )
				return false;

            return true;
        }

		public int PercentCompleted => throw new NotImplementedException();

        public bool Save(XmlDocumentFragment oStream) {
            return true;
        }

		/// <results>
		/// Not sure if I want this on the main view or on the properties window. It makes
		/// more sense here since I'm more likely to have instances I need to connect to
		/// instead of this static class reflection stuff I'm doing now.
		/// </results>
		/// <param name="oDesc"></param>
		/// <param name="oMode"></param>
        public void PopulateSubModes( SSTVDEM.SSTVFamily oDesc, SSTVMode oMode = null ) {
            if( oDesc._typClass.GetMethod( "EnumAllModes" ).Invoke( null, Array.Empty<object>() ) is IEnumerator<SSTVMode> itrModes ) {
                _wmTxProperties._ddSSTVMode.Items.Clear();

                while( itrModes.MoveNext() ) {
                    int iIndex = _wmTxProperties._ddSSTVMode.Items.Add( itrModes.Current );

                    if( oMode != null && 
                        itrModes.Current.LegacyMode == oMode.LegacyMode ) 
                    {
                        _wmTxProperties._ddSSTVMode.SelectedIndex = iIndex;
                    }
                }
                if( oMode == null ) {
                    _wmTxProperties._ddSSTVMode.SelectedIndex = 0;
                }
            }
        }
    }

}
