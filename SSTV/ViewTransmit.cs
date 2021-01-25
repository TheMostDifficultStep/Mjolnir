using System;
using System.Drawing;
using System.Xml;
using System.Collections.Generic;
using System.Windows.Forms;
using System.IO;

using SkiaSharp;

using Play.Interfaces.Embedding;
using Play.Rectangles;
using Play.Edit;
using Play.ImageViewer;

namespace Play.SSTV {
	public class ViewTransmit:
		Control,
		IPgParent,
		IPgLoad<XmlElement>,
		IPgSave<XmlDocumentFragment>,
		IPgCommandView,
		IPgTools,
		IDisposable
	{
		public static Guid ViewType { get; }  = new Guid( "{CED824F5-2C17-418C-9559-84D6B4F571FC}" );

		protected readonly IPgViewSite   _oSiteView;
		protected readonly DocSSTV       _oDocSSTV;

		protected readonly EditWindow2   _oViewMode;  // List the modes for the generators.
		protected readonly ImageViewTx   _oViewImage; // Show the currently selected image.

		protected PropDoc ImageProperties { get; } // Container for properties to show for this window.

		protected LayoutStack _oLayout = new LayoutStackHorizontal( 5 );

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

		public IPgParent Parentage => _oSiteView.Host;
		public IPgParent Services  => Parentage.Services;
		public bool      IsDirty   => false;
		public string    Banner    => "Transmit Window : " + _oDocSSTV.ImageList.CurrentDirectory;
		public Image     Iconic    => null;
		public Guid      Catagory  => ViewType;

        public ViewTransmit( IPgViewSite oViewSite, DocSSTV oDocument ) {
			_oSiteView = oViewSite ?? throw new ArgumentNullException( "View requires a view site." );
			_oDocSSTV  = oDocument ?? throw new ArgumentNullException( "View requires a document." );

            ImageProperties = new PropDoc( new SSTVWinSlot( this ) );

			_oViewMode  = new EditWindow2( new SSTVWinSlot( this ), _oDocSSTV.ModeList, true );
			_oViewImage = new ImageViewTx( new SSTVWinSlot( this ), _oDocSSTV.ImageList );
		}

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
			if( !ImageProperties.InitNew() )
                return false;
			if( !_oViewMode.InitNew() )
				return false;
			if( !_oViewImage.InitNew() )
				return false;

			_oViewImage.Parent = this;
			_oViewMode .Parent = this;

			_oViewImage.Aspect   = new SKPointI( 320, 256 );
			_oViewImage.DragMode = DragMode.FixedRatio;

			DecorPropertiesInit();

            _oLayout.Add( new LayoutControl( _oViewImage, LayoutRect.CSS.None ) ); // image
            _oLayout.Add( new LayoutControl( _oViewMode,  LayoutRect.CSS.Pixels, 150 ) ); // mode

            OnSizeChanged( new EventArgs() );

			return true;
		}

		protected virtual void DecorPropertiesInit() {
            _oDocSSTV.PropertyChange += Listen_PropertyChange;
			using( PropDoc.Manipulator oBulk = ImageProperties.EditProperties ) {
				oBulk.Add( "Width" );
				oBulk.Add( "Height" );
				oBulk.Add( "Encoding" );
				oBulk.Add( "Sent" );
				oBulk.Add( "Name" );
			}
		}

		/// <summary>
		/// This is our event sink for property changes on the SSTV document.
		/// </summary>
		/// <remarks>Right now just update all, but we can just update the
		/// specific property in the future. You know, a color coded property, 
		/// light red or yellow on change would be a cool feature.</remarks>
        private void Listen_PropertyChange( ESstvProperty eProp ) {
			switch( eProp ) {
				case ESstvProperty.ALL:
				case ESstvProperty.TXImage:
					DecorPropertiesReLoad();
					break;
			}
        }

        protected void DecorPropertiesReLoad() {
			using (PropDoc.Manipulator oBulk = ImageProperties.EditProperties) {
				string strWidth  = string.Empty;
				string strHeight = string.Empty;
				string strName   = Path.GetFileName( _oDocSSTV.ImageList.CurrentFileName );
				string strMode   = "Unassigned";

				if( _oDocSSTV.Bitmap != null ) {
					strWidth  = _oDocSSTV.Bitmap.Width .ToString();
					strHeight = _oDocSSTV.Bitmap.Height.ToString();
				}
				if( _oDocSSTV.TransmitMode != null ) {
					strMode = _oDocSSTV.TransmitMode.Name;
				}

                oBulk.Set( 0, strWidth  );
                oBulk.Set( 1, strHeight );
                oBulk.Set( 3, "0%"      );
                oBulk.Set( 2, strMode   );
				oBulk.Set( 4, strName   );
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

			// Limit the top to 500. Look's too goofy if larger. If our window
			// is smaller set the layout rect to that height.
			int iHeight = 500;
			if( Height < iHeight )
				iHeight = Height;

			_oLayout.SetRect( 0, 0, Width, iHeight );
			_oLayout.LayoutChildren();

            Invalidate();
		}

		public object Decorate(IPgViewSite oBaseSite,Guid sGuid) {
			try {
				if( sGuid.Equals(GlobalDecorations.Properties) ) {
					DecorPropertiesReLoad();
					return new PropWin( oBaseSite, ImageProperties );
				}
				if( sGuid.Equals( GlobalDecorations.Outline ) ) {
					return _oViewImage.Decorate( oBaseSite, sGuid );
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
				_oDocSSTV.PlayBegin( _oViewMode.Caret.Line, _oViewImage.Selection.SKRect ); 
				return true;
			}
			if( sGuid == GlobalCommands.Stop ) {
				_oDocSSTV.PlayStop();
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

}
