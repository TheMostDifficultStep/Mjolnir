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
using Play.ImageViewer;

namespace Play.SSTV {
	/// <summary>
	/// This view shows both the SSTV image and the sync image. But I don't think I want to use
	/// this object.
	/// </summary>
	public class ViewRecieve:
		Control,
		IPgParent,
		IPgLoad<XmlElement>,
		IPgSave<XmlDocumentFragment>,
		IPgCommandView,
		IDisposable
	{
		public static Guid   ViewType { get; }  = new Guid( "{A7F75A46-1800-4605-87EC-2D8B960D1599}" );
		public static string _strIcon =  "Play.SSTV.icons8_tv.png";

		protected readonly IPgViewSite   _oSiteView;
		protected readonly DocSSTV       _oDocSSTV;

		protected readonly ImageViewSingle _oViewRx;      // Show the currently selected image.
		protected readonly ImageViewSingle _oViewSync;    // The sync bitmap.
		protected          int             _iCurrentMode = 0;

		// BUG: Consider moving this to the DocSSTV object so more views can use it.
		protected PropDoc ImageProperties { get; } // Container for properties to show for this window.

		protected LayoutStack _oLayout = new LayoutStackVertical( 5 );

		protected class SSTVWinSlot :
			IPgViewSite,
			IPgShellSite
		{
			protected readonly ViewRecieve _oHost;

			public SSTVWinSlot( ViewRecieve oHost ) {
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
		public string    Banner    => "Receive Window : " + _oDocSSTV.ImageList.CurrentDirectory;
		public Image     Iconic    { get; }
		public Guid      Catagory  => ViewType;

        public ViewRecieve( IPgViewSite oViewSite, DocSSTV oDocument ) {
			_oSiteView = oViewSite ?? throw new ArgumentNullException( "View requires a view site." );
			_oDocSSTV  = oDocument ?? throw new ArgumentNullException( "View requires a document." );

			Iconic = ImageResourceHelper.GetImageResource( Assembly.GetExecutingAssembly(), _strIcon );

            ImageProperties = new PropDoc        ( new SSTVWinSlot( this ) );
			_oViewRx        = new ImageViewSingle( new SSTVWinSlot( this ), _oDocSSTV.ReceiveImage );
			_oViewSync      = new ImageViewSingle( new SSTVWinSlot( this ), _oDocSSTV.SyncImage );
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
			if( !_oViewSync.InitNew() )
				return false;
			if( !_oViewRx.InitNew() )
				return false;

			_oViewRx  .Parent = this;
			_oViewSync.Parent = this;

			DecorPropertiesInit();

            _oLayout.Add( new LayoutControl( _oViewRx,    LayoutRect.CSS.Percent, 60 ) );
            _oLayout.Add( new LayoutControl( _oViewSync , LayoutRect.CSS.Percent, 40 ) );

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
				case ESstvProperty.RXImageNew:
					DecorPropertiesReLoad();
					break;
				case ESstvProperty.DownLoadTime:
					DecorPropertiesLoadTime();
					_oViewRx  .Invalidate();
					_oViewSync.Invalidate();
					//Refresh(); // Got to force it or it won't update. Hogging the loop I'll bet.
					break;
			}
        }

        protected void DecorPropertiesReLoad() {
			using (PropDoc.Manipulator oBulk = ImageProperties.EditProperties) {
				string strWidth  = string.Empty;
				string strHeight = string.Empty;
				string strName   = Path.GetFileName( _oDocSSTV.ImageList.CurrentFileName );
				string strMode   = "Unassigned";

				if( _oDocSSTV.ReceiveImage.Bitmap != null ) {
					strWidth  = _oDocSSTV.ReceiveImage.Bitmap.Width .ToString();
					strHeight = _oDocSSTV.ReceiveImage.Bitmap.Height.ToString();
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

		protected void DecorPropertiesLoadTime() {
			using (PropDoc.Manipulator oBulk = ImageProperties.EditProperties) {
                oBulk.Set( 3, _oDocSSTV.PercentFinished.ToString() + "%" );
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
					DecorPropertiesReLoad();
					return new PropWin( oBaseSite, ImageProperties );
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
				_oDocSSTV.RecordBeginTest2( new SKRectI( 0, 0, 320, 256 ) );
				return true;
			}
			if( sGuid == GlobalCommands.Stop ) {
				_oDocSSTV.PlayStop(); // Stop record or play.
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
	/// This view shows the single image being downloaded from the audio stream.
	/// </summary>
	public class SSTVReceiveImage : 
		ImageViewSingle, 
		IPgCommandView,
		IPgSave<XmlDocumentFragment>,
		IPgLoad<XmlElement>
	{
		public static Guid ViewType { get; } = new Guid( "{5213847C-8B38-49D8-AAE2-C870F5E6FB51}" );

        public Guid   Catagory => ViewType;
        public string Banner   => "SSTV Rx Image";
        public Image  Iconic   => null;
        public bool   IsDirty  => false;

		protected readonly IPgViewSite _oSiteView;

        DocSSTV _oDocSSTV;
		protected PropDoc ImageProperties { get; } // Container for properties to show for this window.

		protected class SSTVWinSlot :
			IPgViewSite,
			IPgShellSite
		{
			protected readonly SSTVReceiveImage _oHost;

			public SSTVWinSlot( SSTVReceiveImage oHost ) {
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

		public SSTVReceiveImage( IPgViewSite oSiteBase, DocSSTV oDocSSTV ) : base( oSiteBase, oDocSSTV.ReceiveImage ) {
			_oSiteView = oSiteBase ?? throw new ArgumentNullException( "SiteBase must not be null." );
			_oDocSSTV  = oDocSSTV  ?? throw new ArgumentNullException( "DocSSTV must not be null." );

            ImageProperties = new PropDoc( new SSTVWinSlot( this ) );
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
			if( !ImageProperties.InitNew() ) 
				return false;

            _oDocSSTV.PropertyChange += ListenDoc_PropertyChange;

			DecorPropertiesInit();

			return true;
        }

		protected virtual void DecorPropertiesInit() {
			using( PropDoc.Manipulator oBulk = ImageProperties.EditProperties ) {
				oBulk.Add( "Width" );
				oBulk.Add( "Height" );
				oBulk.Add( "Encoding" );
				oBulk.Add( "Received" );
				oBulk.Add( "Name" );
			}
		}

        /// <summary>
        /// This is our event sink for property changes on the SSTV document.
        /// </summary>
        /// <remarks>Right now just update all, but we can just update the
        /// specific property in the future. You know, a color coded property, 
        /// light red or yellow on change would be a cool feature.</remarks>
        private void ListenDoc_PropertyChange( ESstvProperty eProp ) {
			switch( eProp ) {
				case ESstvProperty.ALL:
				case ESstvProperty.RXImageNew:
					DecorPropertiesReLoad();
					break;
				case ESstvProperty.DownLoadFinished:
				case ESstvProperty.DownLoadTime:
					DecorPropertiesLoadTime();
					Invalidate();
					break;
			}
        }

        protected void DecorPropertiesReLoad() {
			using (PropDoc.Manipulator oBulk = ImageProperties.EditProperties) {
				string strWidth  = string.Empty;
				string strHeight = string.Empty;
				string strName   = Path.GetFileName( _oDocSSTV.ImageList.CurrentFileName );
				string strMode   = "Unassigned";

				if( _oDocSSTV.ReceiveImage.Bitmap != null ) {
					strWidth  = _oDocSSTV.ReceiveImage.Bitmap.Width .ToString();
					strHeight = _oDocSSTV.ReceiveImage.Bitmap.Height.ToString();
				}
				if( _oDocSSTV.RxMode != null ) {
					strMode   = _oDocSSTV.RxMode.Name;
				}
				if( _oDocSSTV.TransmitMode != null ) {
					strMode = _oDocSSTV.TransmitMode.Name;
				}

                oBulk.Set( 0, strWidth  );
                oBulk.Set( 1, strHeight );
                oBulk.Set( 2, strMode   );
                oBulk.Set( 3, "0%" );
				oBulk.Set( 4, strName   );
            }
		}

		protected void DecorPropertiesLoadTime() {
			using (PropDoc.Manipulator oBulk = ImageProperties.EditProperties) {
				float  flPercent = 0;
				if( _oDocSSTV.ReceiveImage.Bitmap != null ) {
					flPercent = ( _oDocSSTV.ScanLine * 100 / _oDocSSTV.ReceiveImage.Bitmap.Height ) ;
				}
                oBulk.Set( 3, flPercent.ToString() + "%" );
            }
		}

        public override bool Execute( Guid sGuid )
        {
			if( sGuid == GlobalCommands.Play ) {
				//_oDocSSTV.RecordBeginTest3();
				// \\hefty3\frodo\Documents\Radio\sstv\sstv-test-files\sstv_test-1.wav
				// C:\Users\Frodo\Documents\signals\ic-705\20201230\20201230_060411.wav
				// C:\Users\Frodo\Documents\signals\iss\2020-12-31

				_oDocSSTV.RecordBeginFileRead2( @"C:\Users\Frodo\Documents\signals\iss\2020-12-31\20201231_1743z.wav");
			}

            return base.Execute(sGuid);
        }

		public object Decorate(IPgViewSite oBaseSite,Guid sGuid) {
			try {
				if( sGuid.Equals(GlobalDecorations.Properties) ) {
					DecorPropertiesReLoad();
					return new PropWin( oBaseSite, ImageProperties );
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

        public bool Save( XmlDocumentFragment oStream )
        {
            return true;
        }

        public bool Load( XmlElement oStream )
        {
            return InitNew();
        }
    }
}
