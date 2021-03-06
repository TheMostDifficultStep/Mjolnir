﻿using System;
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
	public class ViewRxAndSync:
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

		protected readonly ImageViewSingle _oViewRx;      // Show the currently selected image.
		protected readonly ImageViewSingle _oViewSync;    // The sync bitmap.

		protected LayoutStack _oLayout = new LayoutStackVertical( 5 );

		protected class SSTVWinSlot :
			IPgViewSite,
			IPgShellSite
		{
			protected readonly ViewRxAndSync _oHost;

			public SSTVWinSlot( ViewRxAndSync oHost ) {
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
		public string    Banner    => "Debug Receive Window : " + _oDocSSTV.TxImageList.CurrentDirectory;
		public Image     Iconic    { get; }
		public Guid      Catagory  => GUID;

        public ViewRxAndSync( IPgViewSite oViewSite, DocSSTV oDocument ) {
			_oSiteView = oViewSite ?? throw new ArgumentNullException( "View requires a view site." );
			_oDocSSTV  = oDocument ?? throw new ArgumentNullException( "View requires a document." );

			Iconic = ImageResourceHelper.GetImageResource( Assembly.GetExecutingAssembly(), _strIcon );

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
			if( !_oViewSync.InitNew() )
				return false;
			if( !_oViewRx.InitNew() )
				return false;

			_oViewRx  .Parent = this;
			_oViewSync.Parent = this;

            _oDocSSTV.PropertyChange += Listen_PropertyChange;

            _oLayout.Add( new LayoutControl( _oViewRx,    LayoutRect.CSS.Percent, 60 ) );
            _oLayout.Add( new LayoutControl( _oViewSync , LayoutRect.CSS.Percent, 40 ) );

            OnSizeChanged( new EventArgs() );

			return true;
		}

        /// <summary>
        /// This is our event sink for property changes on the SSTV document.
        /// </summary>
        /// <remarks>Right now just update all, but we can just update the
        /// specific property in the future. You know, a color coded property, 
        /// light red or yellow on change would be a cool feature.</remarks>
        private void Listen_PropertyChange( ESstvProperty eProp ) {
			_oViewRx  .Refresh();
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
					return new PropWin( oBaseSite, _oDocSSTV.Properties );
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
	/// This view shows the single image being downloaded from the audio stream. Haven't sorted
	/// out how to switch between the microphone (usb in) and selecting an audio stream.
	/// </summary>
	public class SSTVRxImage : 
		ImageViewSingle, 
		IPgCommandView,
		IPgSave<XmlDocumentFragment>,
		IPgLoad<XmlElement>,
		IPgTools
	{
		public static Guid GUID { get; } = new Guid( "{5213847C-8B38-49D8-AAE2-C870F5E6FB51}" );
		public static string _strIcon =  "Play.SSTV.icons8_tv.png";

        public Guid   Catagory => GUID;
        public string Banner   => _strBanner;
        public Image  Iconic   { get; protected set; }
        public bool   IsDirty  => false;

        protected readonly IPgViewSite _oSiteView;

		readonly List<string>   _rgToolBox = new List<string>() { "File", "File w/o VIS", "Port" };
		protected int           _iToolSelected = 0;
		protected static string _strBaseTitle = "MySSTV Receive";
		protected string        _strBanner    = _strBaseTitle;

        DocSSTV _oDocSSTV;

		protected class SSTVWinSlot :
			IPgViewSite,
			IPgShellSite
		{
			protected readonly SSTVRxImage _oHost;

			public SSTVWinSlot( SSTVRxImage oHost ) {
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

		public SSTVRxImage( IPgViewSite oSiteBase, DocSSTV oDocSSTV ) : 
			base( oSiteBase, oDocSSTV.ReceiveImage ) 
		{
			_oSiteView = oSiteBase ?? throw new ArgumentNullException( "SiteBase must not be null." );
			_oDocSSTV  = oDocSSTV  ?? throw new ArgumentNullException( "DocSSTV must not be null." );

			Iconic = ImageResourceHelper.GetImageResource( Assembly.GetExecutingAssembly(), _strIcon );
		}

        protected override void Dispose( bool fDisposing ) {
			if( fDisposing && !_fDisposed ) {
				_oDocSSTV.PropertyChange -= ListenDoc_PropertyChange;
			}
			base.Dispose( fDisposing );
        }

        public override bool InitNew() {
            if( !base.InitNew() )
				return false;

            _oDocSSTV.PropertyChange += ListenDoc_PropertyChange;

			return true;
        }

        /// <summary>
        /// This is our event sink for property changes on the SSTV document.
        /// </summary>
        /// <remarks>Right now just update all, but we can just update the
        /// specific property in the future. You know, a color coded property, 
        /// light red or yellow on change would be a cool feature.</remarks>
        private void ListenDoc_PropertyChange( ESstvProperty eProp ) {
			switch( eProp ) {
				case ESstvProperty.DownLoadFinished:
					Refresh();
					break;
				default:
					Invalidate();
					break;
			}
        }

        public override bool Execute( Guid sGuid ) {
			if( sGuid == GlobalCommands.Play ) {
				switch( _iToolSelected ) {
					case 0:
						// TODO: Change our title to match what we're trying to show!!
						_strBanner = _strBaseTitle + " : " + _oDocSSTV.Chooser.CurrentFullPath;
						_oSiteView.Notify( ShellNotify.BannerChanged );
						_oDocSSTV.RecordBeginFileRead2( _oDocSSTV.Chooser.CurrentFullPath );
						return true;
					case 1:
						LogError( "SSTV Command", "Audio Port not supported yet" );
						return true;
				}
			}

            return base.Execute(sGuid);
        }

		public object Decorate(IPgViewSite oBaseSite,Guid sGuid) {
			try {
				if( sGuid.Equals(GlobalDecorations.Properties) ) {
					return new PropWin( oBaseSite, _oDocSSTV.Properties );
				}
				if( sGuid.Equals( GlobalDecorations.Options ) ) {
					return new CheckList( oBaseSite, _oDocSSTV.ModeList );
				}
				if( sGuid.Equals( GlobalDecorations.Outline ) ) {
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
			get => _iToolSelected; 
			set {
				_iToolSelected = value;
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
			base( oSiteView, oSSTV.Chooser?.FileList, fReadOnly:true ) 
		{
			_oDocSSTV = oSSTV ?? throw new ArgumentNullException();

			_fCheckMarks = true;
			ToolSelect = 2;
			HyperLinks.Add( "chooser", OnChooser );
		}

		public void OnChooser( Line oLine, Parse.Impl.IPgWordRange oRange ) {
			try {
				if( oLine is FileLine oFile ) {
					if( oFile._fIsDirectory ) {
						string strName = oFile.SubString( 1, oFile.ElementCount - 2 );
						if( !string.IsNullOrEmpty( strName ) ) {
							_oDocSSTV.Chooser.LoadAgain( Path.Combine( _oDocSSTV.Chooser.CurrentDirectory, strName ) );
						}
					} else {
						_oDocSSTV.Chooser.FileList.CheckedLine = oLine;
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
	}
}
