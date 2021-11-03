using System;
using System.Drawing;
using System.Xml;
using System.Collections.Generic;
using System.Windows.Forms;
using System.IO;
using System.Reflection;

using SkiaSharp;

using Play.Interfaces.Embedding;
using Play.Rectangles;
using Play.Edit;
using Play.Parse;
using Play.ImageViewer;
using Play.Forms;
using Play.Sound;

namespace Play.SSTV {
	/// <summary>
	/// This view shows the raw scan line with our slot identifiers. Invaluable to spot alignment issues.
	/// </summary>
	public class ViewDiagnostics:
		Control,
		IPgParent,
		IPgLoad<XmlElement>,
		IPgSave<XmlDocumentFragment>,
		IPgCommandView,
		IDisposable
	{
		public static Guid   GUID { get; }  = new Guid( "{A7F75A46-1800-4605-87EC-2D8B960D1599}" );
		public static string _strIcon =  "Play.SSTV.icons8_tv.png";

		protected readonly IPgViewSite   _oSiteView;
		protected readonly DocSSTV       _oDocSSTV;

	  //protected readonly ImageViewSingle _oViewRx;      // Show the currently selected image.
		protected readonly ImageViewSingle _oViewSync;    // The sync bitmap.

		protected LayoutStack _oLayout = new LayoutStackVertical( 5 );

		protected class SSTVWinSlot :
			IPgViewSite,
			IPgShellSite
		{
			protected readonly ViewDiagnostics _oHost;

			public SSTVWinSlot( ViewDiagnostics oHost ) {
				_oHost = oHost ?? throw new ArgumentNullException();
			}

			public IPgParent Host => _oHost;

			public void LogError(string strMessage, string strDetails, bool fShow=true) {
				_oHost.LogError( strDetails );
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

		public IPgParent Parentage => _oSiteView.Host;
		public IPgParent Services  => Parentage.Services;
		public bool      IsDirty   => false;
		public string    Banner    { get; protected set; }
		public Image     Iconic    { get; }
		public Guid      Catagory  => GUID;

		protected readonly string _strBannerBase = "Debug Receive Window : ";

        public ViewDiagnostics( IPgViewSite oViewSite, DocSSTV oDocument ) {
			_oSiteView = oViewSite ?? throw new ArgumentNullException( "View requires a view site." );
			_oDocSSTV  = oDocument ?? throw new ArgumentNullException( "View requires a document." );

			Iconic = ImageResourceHelper.GetImageResource( Assembly.GetExecutingAssembly(), _strIcon );
			Banner = _strBannerBase + _oDocSSTV.TxImageList.CurrentDirectory;

		  //_oViewRx   = new ImageViewSingle( new SSTVWinSlot( this ), _oDocSSTV.ReceiveImage );
			_oViewSync = new ImageViewSingle( new SSTVWinSlot( this ), _oDocSSTV.SyncImage );
			_oViewSync.SetBorderOn();
		}

		protected override void Dispose( bool disposing ) {
			_oDocSSTV.PropertyChange -= Listen_PropertyChange;

			base.Dispose( disposing );
		}

		public void LogError( string strMessage ) {
			_oSiteView.LogError( "SSTV View", strMessage );
		}

		public bool InitNew() {
			if( !_oViewSync.InitNew() )
				return false;
			//if( !_oViewRx.InitNew() )
			//	return false;

			//_oViewRx  .Parent = this;
			_oViewSync.Parent = this;

            _oDocSSTV.PropertyChange += Listen_PropertyChange;

            //_oLayout.Add( new LayoutControl( _oViewRx,    LayoutRect.CSS.Percent, 60 ) );
            _oLayout.Add( new LayoutControl( _oViewSync , LayoutRect.CSS.Percent, 100 ) );

            OnSizeChanged( new EventArgs() );

			return true;
		}

        /// <summary>
        /// This is our event sink for property changes on the SSTV document.
        /// </summary>
        /// <remarks>Right now just update all, but we can just update the
        /// specific property in the future. You know, a color coded property, 
        /// light red or yellow on change would be a cool feature.</remarks>
        private void Listen_PropertyChange( SSTVEvents eProp ) {
			//_oViewRx  .Refresh();
			_oViewSync.Refresh();
        }

        public bool Load(XmlElement oStream) {
			InitNew();
			return true;
		}

		public bool Save(XmlDocumentFragment oStream) {
			return true;
		}

		protected override void OnSizeChanged(EventArgs e) {
			base.OnSizeChanged(e);

			_oLayout.SetRect( 0, 0, Width, Height );
			_oLayout.LayoutChildren();

            Invalidate();
		}

		public object Decorate(IPgViewSite oBaseSite,Guid sGuid) {
			try {
				if( sGuid.Equals(GlobalDecorations.Properties) ) {
					return new ViewSSTVProperties( oBaseSite, _oDocSSTV.RxProperties );
				}
				return false;
			} catch ( Exception oEx ) {
				Type[] rgErrors = { typeof( NotImplementedException ),
									typeof( NullReferenceException ),
									typeof( ArgumentException ),
									typeof( ArgumentNullException ) };
				if( rgErrors.IsUnhandled( oEx ) )
					throw;

				LogError( "Couldn't create SSTV decor: " + sGuid.ToString() );
			}

            return( null );
		}

		public bool Execute(Guid sGuid) {
			if( sGuid == GlobalCommands.Play ) {
				Banner = _strBannerBase + _oDocSSTV.RecChooser.CurrentFullPath;
				_oSiteView.Notify( ShellNotify.BannerChanged );
				_oDocSSTV.ReceiveFileReadBgThreadBegin( _oDocSSTV.RecChooser.CurrentFullPath );
				return true;
			}
			if( sGuid == GlobalCommands.Stop ) {
				_oDocSSTV.TransmitStop(); // Stop record or play.
				return true;
			}

			return false;
		}

        protected override void OnGotFocus( EventArgs e ) {
            base.OnGotFocus(e);
			_oSiteView.EventChain.NotifyFocused( true );
			Invalidate();
        }

        protected override void OnLostFocus( EventArgs e ) {
            base.OnGotFocus(e);
			_oSiteView.EventChain.NotifyFocused( false );
			Invalidate();
        }
        protected override void OnKeyDown(KeyEventArgs e) {
            if( this.IsDisposed )
                return;

            e.Handled = true;

            switch( e.KeyCode ) {
                case Keys.Right:
                case Keys.Enter:
                    break;
            }
        }
    }

    /// <summary>
	/// We'll turn that into optionally a dropdown in the future.
    /// </summary>
    public class ViewSSTVProperties : 
        ViewStandardProperties
     {
        public static Guid GUID {get;} = new Guid("{80C855E0-C2F6-4641-9A7C-B6A8A53B3FDF}");

		public ViewSSTVProperties( IPgViewSite oViewSite, DocProperties oDocument ) : base( oViewSite, oDocument ) {
		}
    }

    /// <summary>
	/// Little experiment for the property page of the receiver viewer.
    /// </summary>
    public class ViewRXProperties : 
        ViewStandardProperties
     {
        public DocSSTV SSTVDocument { get; }

		public ViewRXProperties( IPgViewSite oViewSite, DocSSTV docSSTV, DocProperties docProperties ) : base( oViewSite, docProperties ) {
			SSTVDocument = docSSTV ?? throw new ArgumentNullException( "docSSTV" );
		}

        public override void InitRows()
        {
            if( Layout2 is SmartTable oTable ) {
                foreach( RxProperties.Names eName in Enum.GetValues(typeof(RxProperties.Names)) ) {
                    switch( eName ) {
                        //case RxProperties.Names.RxPort:
                        //    PropertyInitRow( oTable, (int)eName, new CheckList( new WinSlot( this ), SSTVDocument.PortRxList ) );
                        //    break;
                        default:
                            PropertyInitRow( oTable, (int)eName ); // This creates a regular cacheline.
                            break;
                    }
                }
            }
        }
    }
	/// <summary>
	/// This view shows the single image being downloaded from the audio stream. 
	/// </summary>
	public class ViewRxImage : 
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
        public string Banner   => _strBaseTitle + _strDirectory;
        public Image  Iconic   { get; protected set; }
        public bool   IsDirty  => false;

        protected readonly IPgViewSite _oSiteView;

		readonly  List<string>           _rgToolBox     = new() { "File", "Port" };
		protected Tools                  _eToolSelected = Tools.File;
		protected static readonly string _strBaseTitle  = "MySSTV Receive";
		protected        string          _strDirectory  = string.Empty;

        DocSSTV _oDocSSTV;

		protected class SSTVWinSlot :
			IPgViewSite,
			IPgShellSite
		{
			protected readonly ViewRxImage _oHost;

			public SSTVWinSlot( ViewRxImage oHost ) {
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

		public ViewRxImage( IPgViewSite oSiteBase, DocSSTV oDocSSTV ) : 
			base( oSiteBase, oDocSSTV.ReceiveImage ) 
		{
			_oSiteView = oSiteBase ?? throw new ArgumentNullException( "SiteBase must not be null." );
			_oDocSSTV  = oDocSSTV  ?? throw new ArgumentNullException( "DocSSTV must not be null." );

			Iconic = ImageResourceHelper.GetImageResource( Assembly.GetExecutingAssembly(), _strIcon );

			SetBorderOn();
		}

        protected override void Dispose( bool fDisposing ) {
			if( fDisposing && !_fDisposed ) {
				_oDocSSTV.PropertyChange          -= OnPropertyChange_DocSSTV;
				_oDocSSTV.RecChooser.DirectoryChange -= OnDirectoryChange_Chooser;
			}
			base.Dispose( fDisposing );
        }

        public override bool InitNew() {
            if( !base.InitNew() )
				return false;

            _oDocSSTV.PropertyChange             += OnPropertyChange_DocSSTV;
            _oDocSSTV.RecChooser.DirectoryChange += OnDirectoryChange_Chooser;
            _oDocSSTV.RxModeList.CheckedEvent    += OnCheckedEvent_RxModeList;

			// Of course we'll blow up the shell if try in the constructor...
			OnDirectoryChange_Chooser( _oDocSSTV.RecChooser.CurrentDirectory );
			return true;
        }

        private void OnCheckedEvent_RxModeList( Line oLineChecked ) {
			_oDocSSTV.RequestModeChange( oLineChecked.Extra as SSTVMode );
        }

        private void OnDirectoryChange_Chooser( string strDirectory ) {
			_strDirectory = " : " + strDirectory;
			_oSiteView.Notify( ShellNotify.BannerChanged );
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
				switch( _eToolSelected ) {
					case Tools.File: {
						_oDocSSTV.ReceiveFileReadBgThreadBegin( _oDocSSTV.RecChooser.CurrentFullPath );
						return true;
						}
					case Tools.Port:
						_oDocSSTV.ReceiveLiveBegin();
						return true;
				}
			}
			if( sGuid == GlobalCommands.Stop ) {
				// BUG: What if we're launched with one tool and then the tool is
				//      changed midway and we hit stop. Need to sort that out.
				_oDocSSTV.ReceiveLiveStop();
			}

            return base.Execute(sGuid);
        }

		public object Decorate(IPgViewSite oBaseSite,Guid sGuid) {
			try {
				if( sGuid.Equals(GlobalDecorations.Properties) ) {
					return new ViewStandardProperties( oBaseSite, _oDocSSTV.RxProperties );
				}
				if( sGuid.Equals( GlobalDecorations.Outline ) ) {
					return new CheckList( oBaseSite, _oDocSSTV.RxModeList );
				}
				if( sGuid.Equals( GlobalDecorations.Options ) ) {
					return new TextDirView( oBaseSite, _oDocSSTV );
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

	public class TextDirView : EditWindow2 {
		protected readonly DocSSTV _oDocSSTV;

		public TextDirView( IPgViewSite oSiteView, DocSSTV oSSTV ) :
			base( oSiteView, oSSTV.RecChooser?.FileList, fReadOnly:true ) 
		{
			_oDocSSTV = oSSTV ?? throw new ArgumentNullException();

			_fCheckMarks = true;
			ToolSelect = 2; // BUG: change this to an enum in the future.
			HyperLinks.Add( "chooser", OnChooser );
		}

		public void OnChooser( Line oLine, IPgWordRange oRange ) {
			try {
				if( oLine is FileLine oFile ) {
					if( oFile._fIsDirectory ) {
						string strName = oFile.SubString( 1, oFile.ElementCount - 2 );
						if( !string.IsNullOrEmpty( strName ) ) {
							_oDocSSTV.RecChooser.LoadAgain( Path.Combine( _oDocSSTV.RecChooser.CurrentDirectory, strName ) );
							// BUG: Need to make the RxProp the one that gets changed and we catch an event to LoadAgain();
							_oDocSSTV.RxProperties.ValueUpdate( RxProperties.Names.SaveDir, _oDocSSTV.RecChooser.CurrentDirectory, Broadcast:true );
						}
					} else {
						_oDocSSTV.RecChooser.FileList.CheckedLine = oLine;
					}
				}
			} catch( Exception oEx ) { 
				Type[] rgErrors = { typeof( ArgumentOutOfRangeException ),
									typeof( ArgumentNullException ),
									typeof( ArgumentException ), 
									typeof( NullReferenceException ) };
				if( rgErrors.IsUnhandled( oEx ) ) {
					throw;
				}
			}
		}

        public override bool Execute( Guid sGuid ) {
			if( sGuid == GlobalCommands.JumpParent ) {
				return _oDocSSTV.RecChooser.Execute( sGuid );
			}

            return base.Execute(sGuid);
        }
    }
}
