using System;
using System.Xml;
using System.Collections.Generic;
using System.Text;
using System.Windows.Forms;
using System.Threading.Tasks;

using SkiaSharp;
using SkiaSharp.Views.Desktop;


using Play.Interfaces.Embedding;
using Play.Rectangles;
using Play.Edit;
using Play.ImageViewer;
using Play.Sound;
using Play.Forms;
using System.Drawing;

namespace Play.SSTV {
    /// <summary>
	/// Show a subset of the SSTV Properties. The Transmit one's only.
    /// </summary>
    public class ViewTxProperties : 
        WindowStandardProperties
     {
        public static Guid GUID {get;} = new Guid("{80C855E0-C2F6-4641-9A7C-B6A8A53B3FDF}");

		public ViewTxProperties( IPgViewSite oViewSite, DocProperties oDocument ) : base( oViewSite, oDocument ) {
		}

        public override void InitRows() {
			int[] rgShow = { 
				(int)SSTVProperties.Names.Tx_Progress,
				(int)SSTVProperties.Names.Tx_SrcDir,
				(int)SSTVProperties.Names.Tx_SrcFile,
				(int)SSTVProperties.Names.Std_Time,
				(int)SSTVProperties.Names.Tx_MyCall,
				(int)SSTVProperties.Names.Tx_TheirCall,
				(int)SSTVProperties.Names.Tx_RST,
				(int)SSTVProperties.Names.Tx_Message };

            base.InitRows(rgShow);
        }
    }

	/// <summary>
	/// A little subclass of the editwindow to turn on the check marks. turn on readonly and have multiline.
	/// </summary>
	public class CheckList : EditWindow2 {
		public CheckList( IPgViewSite oSite, Editor oEditor ) : base( oSite, oEditor, fReadOnly:true, fSingleLine:false ) {
			_fCheckMarks = true;
			ToolSelect   = 2; // BUG: change this to an enum in the future.
		}
	}

	/// <summary>
	/// This is a old view so we can select a transmit image. Basically a slightly
	/// motified directory viewer. I'm going to leave it, in case I need a simplified
	/// veiwer for some reason.
	/// </summary>
	public class ViewTransmitSolo: 
		WindowSoloImageNav 
	{
		public static Guid GUID { get; } = new Guid( "{5BC25D2B-3F4E-4339-935C-CFADC2650B35}" );

        public override Guid   Catagory => GUID;

        public override string Banner {
			get { 
				StringBuilder sbBanner = new StringBuilder();

				sbBanner.Append("MySSTV Transmit");
				if( _oDocSSTV.PortTxList.CheckedLine is Line oLine ) {
					sbBanner.Append( " : " );
					sbBanner.Append( oLine.ToString() );
				}

				return sbBanner.ToString();
			} 
		}

        DocSSTV _oDocSSTV;

		public ViewTransmitSolo( IPgViewSite oSiteBase, DocSSTV oDocSSTV ) : base( oSiteBase, oDocSSTV.TxImageList ) {
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
				return new ViewTxProperties( oBaseSite, _oDocSSTV.Properties );
			}
			if( sGuid.Equals( GlobalDecorations.Outline ) ) {
				return new CheckList( oBaseSite, _oDocSSTV.TxModeList );
			}
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
        // Clipboard functions
        public static readonly Guid Color   = new Guid( "{B3198F66-7698-4DFB-8B31-9643372B2B3E}" );
        public static readonly Guid Move    = new Guid( "{179898AD-1823-4F0E-BF27-11456C1EA8C8}" );
        public static readonly Guid Text    = new Guid( "{C7F1DADB-A0A4-479C-B193-B38AFAEE5AB6}" );
        public static readonly Guid Gallary = new Guid( "{94975898-5AC1-427C-85CD-9E516646115D}" );
        public static readonly Guid PnP     = new Guid( "{A1BB369C-4E73-4248-A6E1-07C5466C818C}" );
		public static readonly Guid Main    = new Guid( "{FE683CA1-1068-4BA0-A84E-CFE35900A06E}" );
        public static readonly Guid Mode    = new Guid( "{56797520-C603-417C-858A-EF532E0652D2}" );
		public static readonly Guid Resize  = new Guid( "{84F921E1-BFB5-4BD7-9814-C53D48C90D1E}" );
	}
	
	public class WinTransmitTools : ButtonBar {
		IPgTools _oTools;
		public WinTransmitTools( IPgViewSite oSite, Editor oDoc, IPgTools oTools ) : base( oSite, oDoc ) {
			_oTools = oTools ?? throw new ArgumentException( nameof( oTools ) );
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
                if( oLine == HoverTab?.ID ) 
                    return SKColors.LightYellow;

                if( _oTools.ToolSelect == oLine.At )
                    return SKColors.LightCyan;

                // Need to distinguish between our window being part of the focus
                // chain or not.
            }
            return skBG;
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
		IPgTools
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

		protected readonly ImageViewSingle    _wmTxImageComposite;
		protected readonly ImageViewIcons     _wmTxViewChoices;
		protected readonly ImageViewIcons     _wmRxViewChoices;

		protected readonly Editor _rgToolIcons;
		protected          int    _iToolSelected = -1;

		protected bool     _fColorDialogUp = false;
		public SmartRect Selection { get; } = new SmartRect();

        public string Banner {
			get { 
				StringBuilder sbBanner = new StringBuilder();

				sbBanner.Append("MySSTV Transmit");
				if( _oDocSSTV.PortTxList.CheckedLine is Line oLine ) {
					sbBanner.Append( " : " );
					sbBanner.Append( oLine.ToString() );
				}

				return sbBanner.ToString();
			} 
		}

		protected class SSTVWinSlot :
			IPgFileSite,
			IPgViewSite,
			IPgShellSite,
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

            public IEnumerable<IPgCommandView> EnumerateSiblings => throw new NotImplementedException();

            public uint SiteID => throw new NotImplementedException();

            public FILESTATS FileStatus => FILESTATS.UNKNOWN;

            public Encoding FileEncoding => Encoding.Default;

            public string FilePath => _oHost._oDocSSTV.Properties[SSTVProperties.Names.Rx_SaveDir].ToString();

            public string FileBase => String.Empty;
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
		}

        protected override void Dispose( bool fDisposing ) {
			if( fDisposing && !_fDisposed ) {
				_oDocSSTV.TemplateList .CheckedEvent -= OnCheckedEvent_TemplateList;
				_oDocSSTV.TxModeList   .CheckedEvent -= OnCheckedEvent_TxModeList;
				_oDocSSTV.RxHistoryList.ImageUpdated -= OnImageUpdated_RxHistoryList;
				_oDocSSTV.TxImageList  .ImageUpdated -= OnImageUpdated_TxImageList;

				_wmTxViewChoices   .Dispose();
				_wmTxImageComposite.Dispose();
				_wmRxViewChoices   .Dispose();

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

            _oDocSSTV.TemplateList .CheckedEvent += OnCheckedEvent_TemplateList;
			_oDocSSTV.TxModeList   .CheckedEvent += OnCheckedEvent_TxModeList;
            _oDocSSTV.RxHistoryList.ImageUpdated += OnImageUpdated_RxHistoryList;
			_oDocSSTV.TxImageList  .ImageUpdated += OnImageUpdated_TxImageList;

			InitTools();

			LayoutStack oHBLayout = new LayoutStackHorizontal( 220, 30 ) { Spacing = 5 };

			oHBLayout.Add( new LayoutControl( _wmTxViewChoices, LayoutRect.CSS.Percent, 50 ) );
			oHBLayout.Add( new LayoutControl( _wmRxViewChoices, LayoutRect.CSS.Percent, 50 ) );

			_oLayout .Add( new LayoutControl( _wmTxImageComposite, LayoutRect.CSS.None) );
            _oLayout .Add( oHBLayout );

			RenderComposite();

            OnSizeChanged( new EventArgs() );

			return true;
        }

		protected override void OnSizeChanged(EventArgs e) {
			base.OnSizeChanged(e);

			_oLayout.SetRect( 0, 0, Width, Height );
			_oLayout.LayoutChildren();

            //Invalidate();
		}

        public void InitTools() {
			Dictionary< string, ToolInfo > rgIcons = new Dictionary<string, ToolInfo>();

			rgIcons.Add( "Color",   new ToolInfo( "icons8-color-wheel-2-48.png", TransmitCommands.Color   ));
			rgIcons.Add( "Move",    new ToolInfo( "icons8-move-48.png",		     TransmitCommands.Move    ));
			rgIcons.Add( "Text",    new ToolInfo( "icons8-text-64.png",		     TransmitCommands.Text    ));
			rgIcons.Add( "Gallery", new ToolInfo( "icons8-gallery-64.png",		 TransmitCommands.Gallary ));
			rgIcons.Add( "PnP",     new ToolInfo( "icons8-download-64.png",	     TransmitCommands.PnP     ));
			rgIcons.Add( "Main",    new ToolInfo( "icons8-measure-64.png",       TransmitCommands.Main    ));
			rgIcons.Add( "Mode",    new ToolInfo( "icons8-audio-wave-48.png",    TransmitCommands.Mode    ));
			rgIcons.Add( "Resize",  new ToolInfo( "icons8-resize-100.png",       TransmitCommands.Resize  ));

			foreach( KeyValuePair<string,ToolInfo> oPair in rgIcons ) {
				Line     oLine = _rgToolIcons.LineAppend( oPair.Key, false );
				ToolInfo oInfo = oPair.Value;

				oInfo.Icon = _oDocSSTV.CreateIconic( "Play.SSTV.Content.TxWin." + oInfo._strToolName );
				oLine.Extra = oInfo;

				if( oInfo._guidID == TransmitCommands.Main ) {
					_iToolSelected = oLine.At;
				}
			}
		}

		protected void LogError( string strMsg ) {
			_oSiteView.LogError( "Transmit Image", strMsg );
		}

		public bool RenderComposite() {
			try {
				if( Selection.IsEmpty() ) {
					Selection.SetRect( LOCUS.UPPERLEFT, 0, 0,
									   _oDocSSTV.TxImageList.Bitmap.Width,
									   _oDocSSTV.TxImageList.Bitmap.Height );
				}

				return _oDocSSTV.RenderComposite( Selection.SKRect );
			} catch( NullReferenceException ) {
				LogError( "Try selecting an image first" );
			}
			return false;
		}

        private void OnImageUpdated_RxHistoryList() {
            RenderComposite();
        }

        private void OnImageUpdated_TxImageList() {
			try {
				Selection.SetRect( 0, 0, _oDocSSTV.TxImageList.Bitmap.Width, _oDocSSTV.TxImageList.Bitmap.Height );
				RenderComposite();
			} catch ( NullReferenceException ) { 
			}
        }
        protected SSTVMode SSTVModeSelection { 
			get {
                if( _oDocSSTV.TxModeList.CheckedLine == null )
                    _oDocSSTV.TxModeList.CheckedLine = _oDocSSTV.TxModeList[_oDocSSTV.RxModeList.CheckedLine.At];

                if( _oDocSSTV.TxModeList.CheckedLine.Extra is SSTVMode oMode )
					return oMode;

				return null;
			}
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
				}
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
        private void OnCheckedEvent_TemplateList(Line oLineChecked) {
			RenderComposite();
        }

		protected void OnCheckedEvent_TxModeList( Line oLineChecked ) {
			try {
				_oDocSSTV.TemplateSet( oLineChecked.At );
				RenderComposite();
			} catch( NullReferenceException ) {
				LogError( "Problem setting aspect for template" );
			}
		}

        public bool Execute( Guid sGuid ) {
			if( sGuid == GlobalCommands.Play ) {
                if( SSTVModeSelection is SSTVMode oMode ) {
					if( RenderComposite() ) {
						_oDocSSTV.TransmitBegin( oMode ); 
					}
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
				RenderComposite();
			}
			_fColorDialogUp = false;
		}

        public object Decorate( IPgViewSite oBaseSite, Guid sGuid ) {
			if( sGuid.Equals(GlobalDecorations.Properties) ) {
				return new ViewTxProperties( oBaseSite, _oDocSSTV.Properties );
			}
			if( sGuid.Equals( GlobalDecorations.Outline ) ) {
				return new CheckList( oBaseSite, _oDocSSTV.TxModeList );
			}
			if( sGuid.Equals( GlobalDecorations.Options ) ) {
				return new CheckList( oBaseSite, _oDocSSTV.TemplateList ) { ReadOnly = true }; // We'll be read/write in the future.
			}
			if( sGuid.Equals( GlobalDecorations.ToolIcons ) ) {
				return new WinTransmitTools( oBaseSite, _rgToolIcons, this );
			}
			return null;
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

}
