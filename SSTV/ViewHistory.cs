using System;
using System.Drawing;
using System.Xml;
using System.Collections.Generic;
using System.Windows.Forms;

using SkiaSharp;
using SkiaSharp.Views.Desktop;

using Play.Interfaces.Embedding;
using Play.Rectangles;
using Play.Edit;
using Play.ImageViewer;
using Play.Forms;

namespace Play.SSTV {
    /// <summary>
	/// This viewer shows a subset of all SSTV Properties. Those for the Receiver only.
    /// </summary>
    public class WindowHistoryProperties : 
        WindowStandardProperties
     {
        public DocSSTV SSTVDocument { get; }

		public WindowHistoryProperties( IPgViewSite oViewSite, DocSSTV docSSTV ) : base( oViewSite, docSSTV.Properties ) {
			SSTVDocument = docSSTV ?? throw new ArgumentNullException( nameof( docSSTV ) );
		}

        public override void InitRows() {
			int[] rgShow = { 
				(int)SSTVProperties.Names.Rx_SaveDir,
                (int)SSTVProperties.Names.Tx_SrcDir,
				(int)SSTVProperties.Names.Rx_HistoryFile,
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

    public class WindowChooserTools : 
        SKControl,
		IPgLoad
    {
        private readonly IPgViewSite       _oViewSite;
        private readonly WindowSSTVHistory _wnViewHist;

        private readonly LayoutStack _oLayout    = new LayoutStackHorizontal() { Spacing = 5 };
        private readonly ComboBox    _ddModeMain = new ComboBox();

        private bool _bProcessCheckModeList = false;

        public WindowChooserTools( IPgViewSite oViewSite, WindowSSTVHistory wnViewHist ) { 
            _oViewSite  = oViewSite  ?? throw new ArgumentNullException( nameof( oViewSite ) );
            _wnViewHist = wnViewHist ?? throw new InvalidProgramException( "Can't find document for view" );

            Parent = (Control)_oViewSite.Host;
        }

        public bool InitNew() {
            InitModes();

            OnSizeChanged( new EventArgs() );

            return true;
        }

        /// <summary>
        /// Set up the dual dropdowns for the SSTV node tool options.
        /// </summary>
        private void InitModes() {
            _ddModeMain.Items.Add( "Rx Choices" );
            _ddModeMain.Items.Add( "Tx Choices" );

            _ddModeMain.SelectedIndexChanged += OnSelectedModeChanged;
            _ddModeMain.AutoSize      = true;
            _ddModeMain.Name          = "Image Chooser Select";
            _ddModeMain.TabIndex      = 0;
            _ddModeMain.SelectedIndex = 0;
            _ddModeMain.DropDownStyle = ComboBoxStyle.DropDownList;
            _ddModeMain.Parent        = this;

            _oLayout.Add( new LayoutRect( LayoutRect.CSS.None ) );
            _oLayout.Add( new LayoutControl( _ddModeMain, LayoutRect.CSS.Pixels, 150 ) );
            _oLayout.Add( new LayoutRect( LayoutRect.CSS.None ) );
        }

        /// <summary>
        /// TODO: If there are multiple TX windows open, they might get out of
        /// sync with the composition. Still need to sort that all out.
        /// But we're close.
        /// </summary>
        /// <seealso cref="PopulateSubModes"/>
        private void OnSelectedModeChanged(object sender, EventArgs e) {
            if( !_bProcessCheckModeList ) {
                _bProcessCheckModeList = true;
                if( _ddModeMain.SelectedIndex == 0 ) {
                }
                if( _ddModeMain.SelectedIndex == 1 ) {
                }
                _bProcessCheckModeList = false;
            }
        }

        protected override void OnSizeChanged( EventArgs e ) { 
            _oLayout.SetRect( 0, 0, Width, Height );
            _oLayout.LayoutChildren();
        }
    }

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
		protected static string _strIconResource = "Play.SSTV.Content.icons8-file-folder-48.png";

		protected IPgViewSite _oSiteView;
		protected DocSSTV     _oDocSSTV;

        public Guid   Catagory => GUID;
        public string Banner   => "MySSTV Images";
        public Image  Iconic { get; }
		public SKBitmap Icon { get; }
        public bool   IsDirty  => false;

        public IPgParent Parentage => _oSiteView.Host;

        public IPgParent Services  => Parentage.Services;

        //protected WindowSoloImageNav _wmViewRxHistorySelected;
		protected ImageViewIcons     _wmViewRxHistory;
        protected ImageViewIcons     _wnViewTxImages;

		protected readonly LayoutStack _oLayout = new LayoutStackHorizontal() { Spacing = 5 };

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

			//_wmViewRxHistorySelected     = new( new SSTVWinSlot( this, ChildID.HistoryNavWindow ), _oDocSSTV.RxHistoryList );
			_wmViewRxHistory = new( new SSTVWinSlot( this, ChildID.HistoryIconsWindow ), _oDocSSTV.RxHistoryList ); 
            _wnViewTxImages  = new( new SSTVWinSlot( this, ChildID.TxImageChoices     ), _oDocSSTV.TxImageList   );

			//_wmViewRxHistorySelected    .Parent = this;
			_wmViewRxHistory.Parent = this;
            _wnViewTxImages .Parent = this;

			//_wmViewRxHistorySelected.SetBorderOn();

			Icon = oDocSSTV.CreateIconic( _strIconResource );
		}

		public bool InitNew() {
			//if( !_wmViewRxHistorySelected.InitNew() )
			//	return false;
			if( !_wmViewRxHistory.InitNew() ) 
				return false;
            if( !_wnViewTxImages.InitNew() )
                return false;

            //_oLayout.Add( new LayoutControl( _wmViewRxHistorySelected, LayoutRect.CSS.None ) );
            _oLayout.Add( new LayoutControl( _wnViewTxImages,  LayoutRect.CSS.None ) );
            _oLayout.Add( new LayoutControl( _wmViewRxHistory, LayoutRect.CSS.None ) );

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
                if( sGuid.Equals(GlobalDecorations.Options ) ) {
                    return new WindowChooserTools( oBaseSite, this );
                }
				if( sGuid.Equals(GlobalDecorations.Properties) ) {
					return new WindowHistoryProperties( oBaseSite, _oDocSSTV );
				}
                if( sGuid.Equals(GlobalDecorations.Outline ) ) {
                    return new WindowSoloImageNav( oBaseSite, _oDocSSTV.RxHistoryList );
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
            //return _wmViewRxHistorySelected.Execute( sGuid );
            return false;
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
