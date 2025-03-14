﻿using System;
using System.Drawing;
using System.Xml;
using System.Collections.Generic;
using System.Windows.Forms;

using SkiaSharp;

using Play.Interfaces.Embedding;
using Play.Rectangles;
using Play.ImageViewer;
using Play.Forms;

namespace Play.SSTV {
    /// <summary>
	/// This viewer shows a subset of all SSTV Properties. Those for the Diagnostics window only.
    /// </summary>
    public class WindowDiagnosticProperties : 
        WindowStandardProperties
     {
        public DocSSTV SSTVDocument { get; }

		public WindowDiagnosticProperties( IPgViewSite oViewSite, DocSSTV docSSTV ) : base( oViewSite, docSSTV.Properties ) {
			SSTVDocument = docSSTV ?? throw new ArgumentNullException( nameof( docSSTV ) );
		}

        public override void InitRows() {
			int[] rgShow = { 
				(int)SSTVProperties.Names.Std_Process,
				(int)SSTVProperties.Names.Rx_Mode,
				(int)SSTVProperties.Names.Rx_Progress,
			};

			InitRows( rgShow );
        }
    }


	/// <summary>
	/// This view shows the raw scan line with our slot identifiers. Invaluable to spot alignment issues.
	/// </summary>
	/// <remarks>Moved from ViewDevice and the Receive windows.</remarks>
	public class WindowDiagnostics:
		Control,
		IPgParent,
		IPgLoad<XmlElement>,
		IPgSave<XmlDocumentFragment>,
		IPgCommandView,
		IDisposable
	{
		public static Guid GUID { get; } = new Guid( "{A7F75A46-1800-4605-87EC-2D8B960D1599}" );

		public SKBitmap Icon { get; }

		protected static readonly string _strIcon =  "Play.SSTV.Content.icons8-system-diagnostic-48.png";
		protected readonly IPgViewSite   _oSiteView;
		protected readonly DocSSTV       _oDocSSTV;

		protected readonly ImageViewSingle _wmViewSync;    // The sync bitmap.

		protected LayoutStack _oLayout = new LayoutStackVertical() { Spacing = 5 };

		protected class SSTVWinSlot :
			IPgViewSite
		{
			protected readonly WindowDiagnostics _oHost;

			public SSTVWinSlot( WindowDiagnostics oHost ) {
				_oHost = oHost ?? throw new ArgumentNullException( nameof( oHost ) );
			}

			public IPgParent Host => _oHost;

			public void LogError(string strMessage, string strDetails, bool fShow=true) {
				_oHost.LogError( strDetails );
			}

			public void Notify( ShellNotify eEvent ) {
				_oHost._oSiteView.Notify( eEvent );
			}

            public IPgViewNotify EventChain => _oHost._oSiteView.EventChain;
        }

		public IPgParent Parentage => _oSiteView.Host;
		public IPgParent Services  => Parentage.Services;
		public bool      IsDirty   => false;
		public Image     Iconic    { get; }
		public Guid      Catagory  => GUID;
        public string    Banner => "MySSTV Diagnostics";

        public WindowDiagnostics( IPgViewSite oViewSite, DocSSTV oDocument ) {
			_oSiteView = oViewSite ?? throw new ArgumentNullException( "View requires a view site." );
			_oDocSSTV  = oDocument ?? throw new ArgumentNullException( "View requires a document." );

			Icon = oDocument.CreateIconic( _strIcon );
 

			_wmViewSync = new ImageViewSingle( new SSTVWinSlot( this ), _oDocSSTV.SyncImage );
			_wmViewSync.SetBorderOn();
		}

		protected override void Dispose( bool disposing ) {
			_oDocSSTV.PropertyChange -= Listen_PropertyChange;

			base.Dispose( disposing );
		}

		public void LogError( string strMessage ) {
			_oSiteView.LogError( "SSTV View", strMessage );
		}

		public bool InitNew() {
			if( !_wmViewSync.InitNew() )
				return false;

			_wmViewSync.Parent = this;

            _oDocSSTV.PropertyChange += Listen_PropertyChange;

			// I'm going to leave the layout object since I might add more
			// to the diagnostics screen later.
            _oLayout.Add( new LayoutControl( _wmViewSync , LayoutRect.CSS.Percent, 100 ) );

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
			switch( eProp ) {
				case SSTVEvents.DownLoadFinished:
					_wmViewSync.Refresh();
					break;
				default:
					Invalidate();
					_oSiteView.Notify( ShellNotify.BannerChanged );
					_wmViewSync.Invalidate();
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
				if( sGuid.Equals(GlobalDecor.Properties) ) {
					return new WindowDiagnosticProperties( oBaseSite, _oDocSSTV );
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

        protected override void OnMouseWheel(MouseEventArgs e) {
            base.OnMouseWheel(e);

			bool fShiftPressed = (Control.ModifierKeys & Keys.Shift) != Keys.None;

			int iDistance = fShiftPressed ? 15 : 30; // in 100ths

			if(  e.Delta < 0 )
				iDistance *= -1;

			_oDocSSTV.PostBGMessage( TVMessage.Message.Frequency, iDistance );
        }
    }

}
