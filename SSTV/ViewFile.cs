using System;
using System.Xml;
using System.Collections.Generic;
using System.Text;

using Play.Interfaces.Embedding;
using Play.Rectangles;
using Play.Edit;
using Play.ImageViewer;
using Play.Forms;
using Play.Sound;

namespace Play.SSTV {
	public class WindowFileViewer : 
		WindowRxBase,
		IPgSave<XmlDocumentFragment>,
		IPgLoad<XmlElement>
	{
		public    static          Guid   GUID { get; } = new Guid("{C4521EB7-3F16-4576-80A0-24F5396C23F7}");
		protected static readonly string _strIcon = "Play.SSTV.icons8_tv.png";

		public    override Guid   Catagory     => GUID;
		protected override string IconResource => _strIcon;

		protected readonly ModeEditor     _rgRxModeList;
		protected readonly FileChooser    _rgWavFileList; // Recorded wave files.
		protected readonly ImageWalkerDir _rgDecodedImages;

		public class ModeEditor : Editor {
			public ModeEditor(IPgBaseSite oSite) : base(oSite) {
			}

			public override WorkerStatus PlayStatus => ((DocSSTV)_oSiteBase.Host).PlayStatus;

			public void LoadModes( IEnumerator<SSTVMode> iterMode, bool fAddResolution=true) {
				using BaseEditor.Manipulator oBulk = CreateManipulator();
				StringBuilder sbValue = new();

				while( iterMode.MoveNext() ) {
					SSTVMode oMode = iterMode.Current;

					sbValue.Clear();
					sbValue.Append( oMode.Name );
					Line oLine = oBulk.LineAppendNoUndo( sbValue.ToString() );

					oLine.Extra = oMode;
				}
			}
		}

		public WindowFileViewer( IPgViewSite oSiteBase, DocSSTV oDocSSTV ) : base( oSiteBase, oDocSSTV ) {
			_rgWavFileList   = new( new SSTVWinSlot( this ) );
			_rgDecodedImages = new( new SSTVWinSlot( this ) );

			_oViewRxImg      = new( new SSTVWinSlot( this ), _oDocSSTV.ReceiveImage );
			_oViewRxHistory  = new( new SSTVWinSlot( this ), _rgDecodedImages );
			_rgRxModeList    = new( new SSTVWinSlot( this ) );

			_oViewRxImg    .Parent = this;
			_oViewRxHistory.Parent = this;

			_oViewRxImg.SetBorderOn();
		}

        protected override void Dispose( bool fDisposing ) {
			if( fDisposing && !_fDisposed ) {
				_oDocSSTV     .PropertyChange  -= OnPropertyChange_DocSSTV;
				_rgWavFileList.DirectoryChange -= OnDirectoryChange_WaveFiles;
				_rgRxModeList .CheckedEvent    -= OnCheckedEvent_RxModeList;

				_oViewRxHistory.Dispose();
				_oViewRxImg    .Dispose();

				_fDisposed = true;
			}
			base.Dispose( fDisposing );
        }

        public virtual bool InitNew() {
			if( !_rgRxModeList.InitNew() )
				return false;

			_rgRxModeList.LoadModes( SSTVDEM.EnumModes() );

			string strMyDocs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments );

			if( !_rgWavFileList  .LoadURL( strMyDocs )) { 
				LogError( "File Viewer", "Couldn't view wave file load directory.");
			}
			if( !_rgDecodedImages.LoadURL( strMyDocs )) {
				LogError( "File Viewer", "Couldn't find pictures history directory for SSTV");
				return false;
			}

			if( !_oViewRxImg.InitNew() )
				return false;
			if( !_oViewRxHistory.InitNew() )
				return false;

			_oDocSSTV     .PropertyChange  += OnPropertyChange_DocSSTV;
			_rgWavFileList.DirectoryChange += OnDirectoryChange_WaveFiles;
			_rgRxModeList .CheckedEvent    += OnCheckedEvent_RxModeList;

			// Of course we'll blow up the shell if try in the constructor...
			OnDirectoryChange_WaveFiles( _rgWavFileList.CurrentDirectory );

            _oLayout.Add( new LayoutControl( _oViewRxImg,     LayoutRect.CSS.None ) );
            _oLayout.Add( new LayoutControl( _oViewRxHistory, LayoutRect.CSS.Pixels, 220 ) );

            OnSizeChanged( new EventArgs() );
			return true;
        }

        private void OnCheckedEvent_RxModeList( Line oLineChecked ) {
			_oDocSSTV.RequestModeChange( oLineChecked.Extra as SSTVMode );
        }

        private void OnDirectoryChange_WaveFiles( string strDirectory ) {
			// Not using this for now.
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
					_oViewRxImg.Refresh();
					break;
				default:
					_oViewRxImg.Invalidate();
					_oSiteView .Notify( ShellNotify.BannerChanged );
					break;
			}
        }

		public override object Decorate(IPgViewSite oBaseSite,Guid sGuid) {
			try {
				if( sGuid.Equals(GlobalDecorations.Properties) ) {
					return new WindowStandardProperties( oBaseSite, _oDocSSTV.RxProperties );
				}
				if( sGuid.Equals( GlobalDecorations.Outline ) ) {
					return new CheckList( oBaseSite, _rgRxModeList );
				}
				if( sGuid.Equals( GlobalDecorations.Options ) ) {
					return new WindowTextDir( oBaseSite, _rgWavFileList );
				}
				return base.Decorate( oBaseSite, sGuid );
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
        
		public override bool Execute( Guid sGuid ) {
			if( sGuid == GlobalCommands.Play ) {
				_oSiteView.Notify(ShellNotify.BannerChanged);
				_oDocSSTV.ReceiveFileReadBgThreadBegin(_rgWavFileList.CurrentFullPath);
				return true;
			}
			if ( sGuid == GlobalCommands.Stop ) {
				// BUG: What if we're launched with one tool and then the tool is
				//      changed midway and we hit stop. Need to sort that out.
				_oDocSSTV.ReceiveLiveStop();
				return true;
			}
			if( sGuid == GlobalCommands.Save ) {
				_oDocSSTV.SaveRxImage();
				return true; // make sure you return true or a docsstv.save gets called.
			}

			return false;
        }

        public bool Save( XmlDocumentFragment oStream ) {
            return true;
        }

        public bool Load( XmlElement oStream ) {
            return InitNew();
        }
    }
}
