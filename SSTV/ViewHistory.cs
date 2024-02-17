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
    /// This view gives us both the RX and the TX directory for choosing images.
    /// </summary>
    public class WindowSSTVChooser :
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
        public string Banner   => "MySSTV Chooser";
        public bool   IsDirty  => false;
        public Image  Iconic { get; }
		public SKBitmap Icon { get; }

        public IPgParent Parentage => _oSiteView.Host;

        public IPgParent Services  => Parentage.Services;

        //protected WindowSoloImageNav _wmViewRxHistorySelected;
		protected ImageViewIcons     _wmViewRxHistory;
        protected ImageViewIcons     _wnViewTxImages;

		protected readonly LayoutStack _oLayout = new LayoutStackHorizontal() { Spacing = 5 };

		protected class WinSlot :
			IPgViewSite
		{
			protected readonly WindowSSTVChooser _oHost;

			public WinSlot( WindowSSTVChooser oHost ) {
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

        /// <summary>
        /// This is the first time I've put an outline window inside it's originating view.
        /// It's nice b/c I can access my owner's internal variables, but a pain in that
        /// it takes so much space up in the host class! Also, who knows if it's subclassable
        /// in any manner?
        /// </summary>
        public class WindowChooserOutline :
            Control,
            IPgParent,
            IPgLoad
        {
		    protected IPgViewSite _oSiteView;
            WindowSSTVChooser _wnHistory;

            public IPgParent Parentage => _oSiteView.Host;
            public IPgParent Services  => Parentage.Services;

            protected readonly LayoutStack  _oLayout = new LayoutStackVertical();
            protected readonly LayoutRect[] _rgFlock = new LayoutRect[2];


            protected WindowSoloImageNav _wnViewRxHistorySelected;
            protected WindowSoloImageNav _wnViewTxImageSelected;
            private readonly ComboBox    _ddModeMain = new ComboBox();


		    protected class WinSlot :
			    IPgViewSite
		    {
			    protected readonly WindowChooserOutline _oHost;

			    public ChildID ID { get; }

			    public WinSlot( WindowChooserOutline oHost ) {
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

            public WindowChooserOutline( IPgViewSite oViewSite, WindowSSTVChooser wnHistory ) {
                _oSiteView = oViewSite ?? throw new ArgumentNullException( nameof( oViewSite ) );
                _wnHistory = wnHistory ?? throw new ArgumentNullException( nameof( wnHistory ) );

			    _wnViewRxHistorySelected = new( new WinSlot( this ), wnHistory._oDocSSTV.RxHistoryList ); 
                _wnViewTxImageSelected   = new( new WinSlot( this ), wnHistory._oDocSSTV.TxImageList   );

			    _wnViewRxHistorySelected.Parent = this;
                _wnViewTxImageSelected  .Parent = this;
                _ddModeMain             .Parent = this;
            }

            public bool InitNew() {
                if( !_wnViewRxHistorySelected.InitNew() )
                    return false;
                if( !_wnViewTxImageSelected.InitNew() )
                    return false;
                if( !InitModes() )
                    return false;

                _rgFlock[0] = new LayoutControl( _wnViewRxHistorySelected, LayoutRect.CSS.None );
                _rgFlock[1] = new LayoutControl( _wnViewTxImageSelected,   LayoutRect.CSS.None );

                _oLayout.Add( new LayoutCenter ( _ddModeMain, LayoutRect.CSS.Pixels, 50 ) );
                _oLayout.Add( _rgFlock[0] );
                _oLayout.Add( _rgFlock[1] );

                // This sets our display up and then calls OnSizeChanged()...
                OnSelectedIndexChanged_RxTx( null, new EventArgs() );

                _ddModeMain.SelectedIndexChanged += OnSelectedIndexChanged_RxTx;
                _wnHistory._oDocSSTV.RxHistoryList.ImageUpdated += OnImageUpdated_RxHistoryList;
                _wnHistory._oDocSSTV.TxImageList  .ImageUpdated += OnImageUpdated_TxImageList;
                this.Disposed += OnDisposed_This;

                return true;
            }

            /// <summary>
            /// I don't recall EVER having to do this, usually I can simply override
            /// the dispose. Maybe this is a side effect of declaring the class within
            /// the WinSSTVHistory? I don't know.
            /// </summary>
            private void OnDisposed_This(object sender, EventArgs e) {
                _wnHistory._oDocSSTV.RxHistoryList.ImageUpdated -= OnImageUpdated_RxHistoryList;
                _wnHistory._oDocSSTV.TxImageList  .ImageUpdated -= OnImageUpdated_TxImageList;
                this.Disposed -= OnDisposed_This;
            }

            private void OnImageUpdated_RxHistoryList() {
                if( _ddModeMain.SelectedIndex != 0 )
                    _ddModeMain.SelectedIndex = 0;
            }

            private void OnImageUpdated_TxImageList() {
                if( _ddModeMain.SelectedIndex != 1 )
                    _ddModeMain.SelectedIndex = 1;
            }

            private bool InitModes() {
                _ddModeMain.Items.Add( "Rx Choices" );
                _ddModeMain.Items.Add( "Tx Choices" );

                _ddModeMain.AutoSize      = true;
                _ddModeMain.Name          = "Image Chooser Select";
                _ddModeMain.TabIndex      = 0;
                _ddModeMain.SelectedIndex = 0;
                _ddModeMain.DropDownStyle = ComboBoxStyle.DropDownList;
                _ddModeMain.Parent        = this;

                return true;
            }
            protected void OptionHideAll() {
                foreach( LayoutRect oRect in _rgFlock ) {
                    oRect.Hidden = true;
                }
            }

            private void OnSelectedIndexChanged_RxTx( object sender, EventArgs e ) {
                OptionHideAll();

                try {
                    _rgFlock[_ddModeMain.SelectedIndex].Hidden = false;
                } catch( IndexOutOfRangeException ) {
                    _oSiteView.LogError( "Outline Selector", "More options than views." );
                }

                OnSizeChanged( new EventArgs() );
            }

		    protected override void OnSizeChanged(EventArgs e) {
			    base.OnSizeChanged(e);

			    _oLayout.SetRect( 0, 0, Width, Height );
			    _oLayout.LayoutChildren();

                Invalidate();
		    }

        } // End Outline implementation.

        /// <summary>
	    /// This viewer shows a subset of all SSTV Properties. Those for the Receiver only.
        /// </summary>
        public class WindowChooserProperties : 
            WindowStandardProperties
         {
            public DocSSTV SSTVDocument { get; }

		    public WindowChooserProperties( IPgViewSite oViewSite, DocSSTV docSSTV ) : base( oViewSite, docSSTV.Properties ) {
			    SSTVDocument = docSSTV ?? throw new ArgumentNullException( nameof( docSSTV ) );
		    }

            public override void InitRows() {
			    int[] rgShow = { 
				    (int)SSTVProperties.Names.Rx_SaveDir,
                    (int)SSTVProperties.Names.Tx_SrcDir,
				    (int)SSTVProperties.Names.Rx_HistoryFile,
                    (int)SSTVProperties.Names.Tx_SrcFile,
				    (int)SSTVProperties.Names.Tx_TheirCall,
				    (int)SSTVProperties.Names.Tx_RST
			    };

			    InitRows( rgShow );
            }

		    // Use this for debugging if necessary.
		    //protected override void OnDocumentEvent( BUFFEREVENTS eEvent ) {
		    //	base.OnDocumentEvent( eEvent );
		    //}
        } // End Properties implementation

		protected void LogError( string strMessage, string strDetails ) {
			_oSiteView.LogError( strMessage, strDetails );
		}

		public WindowSSTVChooser( IPgViewSite oViewSite, DocSSTV oDocSSTV ) {
			_oSiteView = oViewSite ?? throw new ArgumentNullException( nameof( oViewSite ) );
			_oDocSSTV  = oDocSSTV  ?? throw new ArgumentNullException( nameof( oDocSSTV  ) );

			//_wmViewRxHistorySelected     = new( new SSTVWinSlot( this, ChildID.HistoryNavWindow ), _oDocSSTV.RxHistoryList );
			_wmViewRxHistory = new( new WinSlot( this ), _oDocSSTV.RxHistoryList ); 
            _wnViewTxImages  = new( new WinSlot( this ), _oDocSSTV.TxImageList   );

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
				if( sGuid.Equals(GlobalDecorations.Properties) ) {
					return new WindowChooserProperties( oBaseSite, _oDocSSTV );
				}
                if( sGuid.Equals(GlobalDecorations.Outline ) ) {
                    return new WindowChooserOutline( oBaseSite, this );
                    //return new WindowSoloImageNav( oBaseSite, _oDocSSTV.RxHistoryList );
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

    /// <summary>
    /// This view gives us only the RX images in the largest display possible.
    /// </summary>
    public class WindowSSTVHistory :
        Control,
		IPgParent,
        IPgLoad<XmlElement>,
        IPgSave<XmlDocumentFragment>,
        IPgCommandView,
        IDisposable 
	{
		public static Guid GUID { get; } = new Guid( "{8050E760-7FFE-49C2-BE14-3659954A8F69}" );
		protected static string _strIconResource = "Play.SSTV.Content.icons8-history-64.png";

		protected IPgViewSite _oSiteView;
		protected DocSSTV     _oDocSSTV;

        protected LayoutStackVertical _oLayout = new();
        public Guid   Catagory => GUID;
        public string Banner   => "MySSTV Rx History";
        public bool   IsDirty  => false;
        public Image  Iconic { get; }
		public SKBitmap Icon { get; }

        public IPgParent Parentage => _oSiteView.Host;

        public IPgParent Services  => Parentage.Services;

        protected WindowSoloImageNav _wmViewRxHistorySelected;
        protected ImageViewIcons     _wmViewRxHistoryClxn;

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
				    (int)SSTVProperties.Names.Rx_HistoryFile,
				    (int)SSTVProperties.Names.Tx_TheirCall,
				    (int)SSTVProperties.Names.Tx_RST
			    };

			    InitRows( rgShow );

			    PropertyInitRow( (int)SSTVProperties.Names.Rx_Window, 
							     new ImageViewSingle( new WinSlot( this ), SSTVDocument.DisplayImage )  );

				//PropertyInitRow( (int)SSTVProperties.Names.Rx_HistoryIcons, 
				//				   new ImageViewIcons( new WinSlot( this ), SSTVDocument.RxHistoryList )  );
            }

		    // Use this for debugging if necessary.
		    //protected override void OnDocumentEvent( BUFFEREVENTS eEvent ) {
		    //	base.OnDocumentEvent( eEvent );
		    //}
        } // End Properties implementation

		protected class WinSlot :
			IPgViewSite
		{
			protected readonly WindowSSTVHistory _oHost;

			public WinSlot( WindowSSTVHistory oHost ) {
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

		protected void LogError( string strMessage, string strDetails ) {
			_oSiteView.LogError( strMessage, strDetails );
		}

		public WindowSSTVHistory( IPgViewSite oViewSite, DocSSTV oDocSSTV ) {
			_oSiteView = oViewSite ?? throw new ArgumentNullException( nameof( oViewSite ) );
			_oDocSSTV  = oDocSSTV  ?? throw new ArgumentNullException( nameof( oDocSSTV  ) );

			_wmViewRxHistorySelected = new( new WinSlot( this ), _oDocSSTV.RxHistoryList );

			_wmViewRxHistorySelected.Parent = this;

            _wmViewRxHistoryClxn     = new( new WinSlot( this ), _oDocSSTV.RxHistoryList );
            _wmViewRxHistoryClxn    .Parent = this;

			//_wmViewRxHistorySelected.SetBorderOn();

			Icon = oDocSSTV.CreateIconic( _strIconResource );
		}

		public bool InitNew() {
            if( !_wmViewRxHistorySelected.InitNew() )
                return false;
            if( !_wmViewRxHistoryClxn.InitNew() )
                return false;

            _oLayout.Add( new LayoutControl( _wmViewRxHistorySelected, LayoutRect.CSS.Percent, 70 ) );
            _oLayout.Add( new LayoutControl( _wmViewRxHistoryClxn ,    LayoutRect.CSS.Percent, 30 ) );

			return true;
		}

		protected override void OnSizeChanged(EventArgs e) {
			base.OnSizeChanged(e);

            // _wmViewRxHistorySelected.Bounds = this.ClientRectangle;
            _oLayout.SetRect( 0, 0, Width, Height );
            _oLayout.LayoutChildren();
		}

        public object Decorate(IPgViewSite oBaseSite, Guid sGuid) {
			try {
				if( sGuid.Equals(GlobalDecorations.Properties) ) {
					return new WindowHistoryProperties( oBaseSite, _oDocSSTV );
				}
                if( sGuid.Equals(GlobalDecorations.Outline ) ) {
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

        /// <summary>
        /// The shell intercepts some keys, like DEL for example, it then sends
        /// a global command. We need to forward that.
        /// </summary>
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
}
