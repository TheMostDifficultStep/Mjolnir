using System;
using System.Xml;
using System.Collections.Generic;
using System.Text;
using System.Windows.Forms;
using System.Threading;
using System.Threading.Tasks;

using SkiaSharp;

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

    public class ViewTransmitDeluxe: 
		WindowStaggardBase,
		IPgSave<XmlDocumentFragment>,
		IPgLoad<XmlElement>,
		IPgTools
	{
		public static Guid GUID { get; } = new Guid( "{3D6FF540-C03C-468F-84F9-86E3DE75F6C2}" );

        public    override Guid   Catagory => GUID;
        protected override string IconResource => "Play.SSTV.Content.icons8_camera.png";

		protected readonly WindowSoloImageNav _wmTxImageChoice;
		protected readonly ImageViewSingle    _wmTxImageComposite;
		protected readonly ImageViewIcons     _wmTxViewChoices;
		protected readonly ImageViewIcons     _wmRxViewChoices;

		protected readonly Editor _rgToolIcons;
		protected          int    _iToolSelected = -1;

		protected bool     _fColorDialogUp = false;

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

        public ViewTransmitDeluxe( IPgViewSite oSiteBase, DocSSTV oDocSSTV ) : base( oSiteBase, oDocSSTV ) {
			_wmTxImageChoice    = new WindowSoloImageNav( new SSTVWinSlot( this, ChildID.TxImageChoice ),    oDocSSTV.TxImageList );
			_wmTxViewChoices    = new ImageViewIcons    ( new SSTVWinSlot( this, ChildID.TxImageChoices ),   oDocSSTV.TxImageList );
			_wmTxImageComposite = new ImageViewSingle   ( new SSTVWinSlot( this, ChildID.TxImageComposite ), oDocSSTV.TxBitmapComp );
			_wmRxViewChoices    = new ImageViewIcons    ( new SSTVWinSlot( this, ChildID.RxImageChoices ),   oDocSSTV.RxHistoryList );

			_wmTxImageChoice   .Parent = this;
			_wmTxViewChoices   .Parent = this;
			_wmTxImageComposite.Parent = this;
			_wmRxViewChoices   .Parent = this;

			_rgToolIcons = new Editor( new SSTVWinSlot( this, ChildID.None ) );

			_wmTxImageChoice.SetBorderOn();
		}

        protected override void Dispose( bool fDisposing ) {
			if( fDisposing && !_fDisposed ) {
				_oDocSSTV.PropertyChange             -= OnPropertyChange_SSTVDoc;
				_oDocSSTV.TemplateList .CheckedEvent -= OnCheckedEvent_TemplateList;
				_oDocSSTV.TxModeList   .CheckedEvent -= OnCheckedEvent_TxModeList;
				_oDocSSTV.RxHistoryList.ImageUpdated -= OnImageUpdated_RxHistoryList;
				_oDocSSTV.TxImageList  .ImageUpdated -= OnImageUpdated_TxImageList;
				_oDocSSTV.Send_TxImageAspect         -= OnTxImageAspect_SSTVDoc;

				_wmTxImageChoice   .Dispose();
				_wmTxViewChoices   .Dispose();
				_wmTxImageComposite.Dispose();
				_wmRxViewChoices   .Dispose();

				_fDisposed = true;
			}
			base.Dispose( fDisposing );
        }

        public bool InitNew() {
			if( !_wmTxImageChoice   .InitNew() )
				return false;
			if( !_wmTxViewChoices   .InitNew() )
				return false;
			if( !_wmTxImageComposite.InitNew() )
				return false;
			if( !_wmRxViewChoices   .InitNew() )
				return false;

            _oDocSSTV.PropertyChange             += OnPropertyChange_SSTVDoc;
            _oDocSSTV.TemplateList .CheckedEvent += OnCheckedEvent_TemplateList;
			_oDocSSTV.TxModeList   .CheckedEvent += OnCheckedEvent_TxModeList;
            _oDocSSTV.RxHistoryList.ImageUpdated += OnImageUpdated_RxHistoryList;
			_oDocSSTV.TxImageList  .ImageUpdated += OnImageUpdated_TxImageList;
            _oDocSSTV.Send_TxImageAspect         += OnTxImageAspect_SSTVDoc;

			_wmTxImageChoice.ToolSelect = 0; 
			_wmTxImageChoice.Aspect     = _oDocSSTV.TxResolution;
			_wmTxImageChoice.DragMode   = DragMode.FixedRatio;

			InitTools();

			_rgStaggaredLayout.Add(new LayoutControl( _wmTxImageComposite, LayoutRect.CSS.None) );
			_rgStaggaredLayout.Add(new LayoutControl( _wmTxImageChoice,    LayoutRect.CSS.None) );

			LayoutStack oHBLayout = new LayoutStackHorizontal( 220, 30 ) { Spacing = 5 };

			oHBLayout.Add( new LayoutControl( _wmTxViewChoices, LayoutRect.CSS.Percent, 50 ) );
			oHBLayout.Add( new LayoutControl( _wmRxViewChoices, LayoutRect.CSS.Percent, 50 ) );

			_oLayout.Add( _rgStaggaredLayout );
            _oLayout.Add( oHBLayout );

            OnSizeChanged( new EventArgs() );

			return true;
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

        private void OnTxImageAspect_SSTVDoc(SKPointI skAspect ) {
            _wmTxImageChoice.Aspect = skAspect;
        }

        private void OnImageUpdated_RxHistoryList() {
            RenderComposite();
        }

        private void OnImageUpdated_TxImageList() {
			_wmTxImageChoice.SelectAll();
            RenderComposite();
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
            _oDocSSTV.TemplateSet( oLineChecked.At );
			RenderComposite();
        }

		protected void OnCheckedEvent_TxModeList( Line oLineChecked ) {
			try {
				_wmTxImageChoice.Aspect = _oDocSSTV.TxResolution;

				_oDocSSTV.TemplateSet( oLineChecked.At );
				RenderComposite();
			} catch( NullReferenceException ) {
				LogError( "Transmit", "Problem setting aspect for template" );
			}
		}

        private void OnPropertyChange_SSTVDoc( SSTVEvents eProp ) {
            switch( eProp ) {
				// BUG: This is probably bubkus. Check it and remove.
				case SSTVEvents.DownLoadTime:
					_wmTxImageChoice.Invalidate();
					break;
			}
        }

        public  override bool Execute( Guid sGuid ) {
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
			if( sGuid == GlobalCommands.Delete ) {
				if( _wmTxImageChoice.Focused ) {
					_wmTxImageChoice.Execute( sGuid );
				}
			}
			if( sGuid == GlobalCommands.Copy ) {
				if( _wmTxImageChoice.Focused ) {
					_wmTxImageChoice.Execute( sGuid );
				}
				//if( _wmTxImageComposite.Focused ) {
				//	RenderComposite();
				//	_oDocSSTV.TxBitmapComp.Execute( sGuid );
				//}
			}
			// This is super cool but clunky.
			if( sGuid == TransmitCommands.Color ) {
				ShowColorDialog();
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
				LogError( "Dialog", "Color Dialog is already open" );
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

        public override object Decorate( IPgViewSite oBaseSite, Guid sGuid ) {
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

		protected bool RenderComposite() {
			// sometimes we get events while we're sending. Let's block render for now.
			if( _oDocSSTV.StateTx ) {
				LogError( "Transmit", "Already Playing" );
				return false;
			}

			SSTVMode oMode = _oDocSSTV.TransmitModeSelection;
			if( oMode != null ) {
				SKRectI rcComp = new SKRectI( 0, 0, oMode.Resolution.Width, oMode.Resolution.Height);
				SKSizeI ptComp = new SKSizeI( oMode.Resolution.Width, oMode.Resolution.Height );
				SKSizeI szDest = new SKSizeI( _wmTxImageChoice.Aspect.X, _wmTxImageChoice.Aspect.Y );

				if( _wmTxImageChoice.Selection.IsEmpty() ) {
					_wmTxImageChoice.Execute( GlobalCommands.SelectAll );
				}

				_oDocSSTV.TxBitmapSnip.Load( _oDocSSTV.TxBitmap, _wmTxImageChoice.Selection.SKRect, szDest ); 
				_oDocSSTV.TxBitmapComp.Load( _oDocSSTV.TxBitmap, rcComp, ptComp ); // Render needs this, for now.

				int iTemplate = _oDocSSTV.TemplateList.CheckedLine is Line oChecked ? oChecked.At : 0;

				_oDocSSTV.TemplateSet( iTemplate );
				_oDocSSTV.TxBitmapComp.RenderImage();

				return true;
			} else {
				_oDocSSTV.TxBitmapSnip.BitmapDispose(); // TODO: I'd really like to have the error image up.
				LogError( "Transmit", "Problem prepping template for transmit." );
			}

			return false;
		}

        protected override void BringChildToFront(ChildID eID) {
			switch( eID ) {
				case ChildID.TxImageComposite:
					RenderComposite();
					_wmTxImageComposite.BringToFront();
					break;
				case ChildID.TxImageChoice:
					_wmTxImageChoice.BringToFront();
					break;
			}
        }
    }

}
