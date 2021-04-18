using System;
using System.Drawing;
using System.Xml;
using System.Collections.Generic;
using System.Windows.Forms;
using System.Reflection;

using SkiaSharp;

using Play.Interfaces.Embedding;
using Play.Rectangles;
using Play.Edit;
using Play.ImageViewer;

namespace Play.SSTV {
	/// <summary>
	/// This is the older fancy transmit viewer where the outline was the mode selector and a secondary
	/// view was the directory of images. I'm going to make this an all in one viewer soon.
	/// </summary>
	public class ViewTransmit:
		Control,
		IPgParent,
		IPgLoad<XmlElement>,
		IPgSave<XmlDocumentFragment>,
		IPgCommandView,
		IPgTools,
		IDisposable
	{
		public static Guid   ViewType { get; }  = new Guid( "{CED824F5-2C17-418C-9559-84D6B4F571FC}" );
		public static string _strIcon =  "Play.SSTV.icons8_camera.png";

		protected readonly IPgViewSite    _oSiteView;
		protected readonly DocSSTV        _oDocSSTV;	 // Main document.
		protected readonly ImageViewSolo  _oViewImage;   // Show the currently selected image.
		protected readonly ImageViewIcons _oViewChoices; // Show the image choices.

		protected LayoutStack _oLayout = new LayoutStackVertical( 5 );

		public IPgParent Parentage => _oSiteView.Host;
		public IPgParent Services  => Parentage.Services;
		public bool      IsDirty   => false;
		public string    Banner    => "MySSSTV Transmit Image : " + _oDocSSTV.TxImageList.CurrentDirectory;
		public Image     Iconic    { get; }
		public Guid      Catagory  => ViewType;

		protected class SSTVWinSlot :
			IPgViewSite,
			IPgShellSite
		{
			protected readonly ViewTransmit _oHost;

			public SSTVWinSlot( ViewTransmit oHost ) {
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

        public ViewTransmit( IPgViewSite oViewSite, DocSSTV oDocument ) {
			_oSiteView = oViewSite ?? throw new ArgumentNullException( "View requires a view site." );
			_oDocSSTV  = oDocument ?? throw new ArgumentNullException( "View requires a document." );

			Iconic = ImageResourceHelper.GetImageResource( Assembly.GetExecutingAssembly(), _strIcon );

			_oViewImage     = new ImageViewSolo ( new SSTVWinSlot( this ), _oDocSSTV.TxImageList );
			_oViewChoices   = new ImageViewIcons( new SSTVWinSlot( this ), _oDocSSTV.TxImageList );
		}

		/// <summary>
		/// Used to use this for displaying the image at the top. 
		/// </summary>
		protected class ImageViewTx : ImageViewSolo {
			public ImageViewTx( IPgViewSite oBaseSite, ImageWalkerDoc oDoc ) : base( oBaseSite, oDoc ) {
			}

			protected override void ViewPortSizeMax( SmartRect rctBitmap, SmartRect rctViewPort ) {
				base.ViewPortSizeMax( rctBitmap, rctViewPort );

				rctViewPort.SetScalar( SET.RIGID, SCALAR.TOP, 0 );
			}
		}

		protected override void Dispose( bool disposing ) {
			_oDocSSTV.PropertyChange -= Listen_PropertyChange;

			base.Dispose( disposing );
		}

		public void LogError( string strMessage ) {
			_oSiteView.LogError( "SSTV View", strMessage );
		}

		public bool InitNew() {
			if( !_oViewChoices.InitNew() )
				return false;
			if( !_oViewImage.InitNew() )
				return false;

			_oViewImage   .Parent = this;
			_oViewChoices .Parent = this;

			_oViewImage.Aspect   = _oDocSSTV.Resolution;
			_oViewImage.DragMode = DragMode.FixedRatio;

            _oDocSSTV.PropertyChange += Listen_PropertyChange;

            _oLayout.Add( new LayoutControl( _oViewImage,   LayoutRect.CSS.None ) );        // image
            _oLayout.Add( new LayoutControl( _oViewChoices, LayoutRect.CSS.Pixels, 250 ) ); // choices

            OnSizeChanged( new EventArgs() );

			return true;
		}

        /// <summary>
        /// This is our event sink for property changes on the SSTV document.
		/// Only need to do something when the mode changes and we reset our
		/// selection aspect ratio.
        /// </summary>
        /// <remarks>Right now just update all, but we can just update the
        /// specific property in the future. You know, a color coded property, 
        /// light red or yellow on change would be a cool feature.</remarks>
        private void Listen_PropertyChange( ESstvProperty eProp ) {
			switch( eProp ) {
				case ESstvProperty.SSTVMode:
					_oViewImage.Aspect = _oDocSSTV.Resolution;
					break;
			}
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
					return new PropWin( oBaseSite, _oDocSSTV.Properties );
				}
				if( sGuid.Equals( GlobalDecorations.Outline ) ) {
					//EditWindow2 oView = new EditWindow2( oBaseSite, _oDocSSTV.ModeList, true );

					//oView.LineChanged += Listen_ViewMode_LineChanged;

					//return oView;
					return new ImageViewIcons( oBaseSite, _oDocSSTV.TxImageList );
				}
				return null;
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
				_oDocSSTV.TxBegin( _oViewImage.Selection.SKRect );
				return true;
			}
			if( sGuid == GlobalCommands.Stop ) {
				_oDocSSTV.PlayStop();
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

        public int ToolCount => _oViewImage.ToolCount;

        public int ToolSelect { 
			get => _oViewImage.ToolSelect; 
			set { _oViewImage.ToolSelect = value; } 
		}

        public string ToolName( int iTool ) {
            return _oViewImage.ToolName( iTool );
        }

        public Image ToolIcon( int iTool ) {
            return _oViewImage.ToolIcon( iTool );
        }
    }

	/// <summary>
	/// This is a new view so we can select a transmit image. Basically a slightly motified directory viewer.
	/// </summary>
	public class SSTVTxImage: 
		ImageViewSolo 
	{
		public static Guid GUID { get; } = new Guid( "{5BC25D2B-3F4E-4339-935C-CFADC2650B35}" );

        public override Guid   Catagory => GUID;
        public override string Banner   => "SSTV Tx Image : " + _oDocSSTV.TxImageList.CurrentDirectory;

        DocSSTV _oDocSSTV;

		public SSTVTxImage( IPgViewSite oSiteBase, DocSSTV oDocSSTV ) : base( oSiteBase, oDocSSTV.TxImageList ) {
			_oDocSSTV = oDocSSTV ?? throw new ArgumentNullException( "oDocSSTV must not be null." );
		}

        protected override void Dispose( bool fDisposing )
        {
			if( fDisposing && !_fDisposed ) {
				_oDocSSTV.PropertyChange -= ListenDoc_PropertyChange;
			}
			base.Dispose( fDisposing );
        }

        public override bool InitNew()
        {
            if( !base.InitNew() )
				return false;

			ToolSelect = 0; // Crude but should be sufficient to freeze Skywalker...
            _oDocSSTV.PropertyChange += ListenDoc_PropertyChange;

			Aspect   = _oDocSSTV.Resolution;
			DragMode = DragMode.FixedRatio;

			return true;
        }

        private void ListenDoc_PropertyChange( ESstvProperty eProp )
        {
            switch( eProp ) {
				case ESstvProperty.DownLoadTime:
					Invalidate();
					break;
				case ESstvProperty.SSTVMode:
					Aspect = _oDocSSTV.Resolution;
					break;
			}
        }

        public override bool Execute( Guid sGuid ) {
			if( sGuid == GlobalCommands.Play ) {
			    _oDocSSTV.TxBegin( this.Selection.SKRect );   // Normal tx button behavior.
			  //_oDocSSTV.RecordBeginTest2( Selection.SKRect ); // Test reception button behavior.
			}
			if( sGuid == GlobalCommands.Stop ) {
				_oDocSSTV.PlayStop();
			}

            return base.Execute( sGuid );
        }

        public override object Decorate( IPgViewSite oBaseSite, Guid sGuid ) {
			if( sGuid.Equals(GlobalDecorations.Properties) ) {
				return new PropWin( oBaseSite, _oDocSSTV.Properties );
			}
			if( sGuid.Equals( GlobalDecorations.Outline ) ) {
				return new ImageViewIcons( oBaseSite, _oDocSSTV.TxImageList );
			}
			if( sGuid.Equals( GlobalDecorations.Options ) ) {
				return new CheckList( oBaseSite, _oDocSSTV.ModeList );
			}
            return base.Decorate( oBaseSite, sGuid );
        }
    }

	/// <summary>
	/// A little subclass of the editwindow to turn on the check marks. turn on readonly and have multiline.
	/// </summary>
	public class CheckList : EditWindow2 {
		public CheckList( IPgViewSite oSite, Editor oEditor ) : base( oSite, oEditor, fReadOnly:true, fSingleLine:false ) {
			_fCheckMarks = true;
		}
	}
}
