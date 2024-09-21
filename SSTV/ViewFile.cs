using System;
using System.Xml;
using System.Collections.Generic;
using System.Text;
using System.IO;

using Play.Interfaces.Embedding;
using Play.Rectangles;
using Play.Edit;
using Play.ImageViewer;
using Play.Forms;
using Play.Sound;
using Play.Parse;

namespace Play.SSTV {
    public class ViewFileProperties : 
        WindowStandardProperties
     {
        public static Guid GUID {get;} = new Guid("{80C855E0-C2F6-4641-9A7C-B6A8A53B3FDF}");

		public ViewFileProperties( IPgViewSite oViewSite, DocProperties oDocument ) : base( oViewSite, oDocument ) {
		}

        public override void InitRows() {
			int[] rgShow = { 
				(int)SSTVProperties.Names.Std_Process,
				(int)SSTVProperties.Names.Rx_Mode,
				(int)SSTVProperties.Names.Rx_Width,
				(int)SSTVProperties.Names.Rx_Height,
				(int)SSTVProperties.Names.Rx_Progress,
				(int)SSTVProperties.Names.Rx_SaveDir,
			};

            base.InitRows(rgShow);
        }
    }

	/// <summary>
	/// This window is for the file chooser.
	/// </summary>
	public class WindowTextDir : CheckList {
		protected readonly FileChooser _rgFileList;

		public WindowTextDir( IPgViewSite oSiteView, FileChooser rgFileList ) :
			base( oSiteView, rgFileList?.FileList, fReadOnly:true ) 
		{
			_rgFileList = rgFileList ?? throw new ArgumentNullException();


			ToolSelect   = 2; // BUG: change this to an enum in the future.

			HyperLinks.Add( "chooser", OnChooser );
		}

		protected void OnChooser( Line oLine, IPgWordRange _ ) { 
			try {
				if( oLine is FileLine oFile ) {
					if( oFile._fIsDirectory ) {
						string strName = oFile.SubString( 1, oFile.ElementCount - 2 );
						if( !string.IsNullOrEmpty( strName ) ) {
							_rgFileList.LoadAgain( Path.Combine(_rgFileList.CurrentDirectory, strName ) );
						}
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

		protected override void TextAreaChecked( Line oLine ) {
			if( oLine is FileLine oFileLine && !oFileLine._fIsDirectory ) { 
				base.TextAreaChecked( oLine );
			}
		}

		public override bool Execute( Guid sGuid ) {
			if( sGuid == GlobalCommands.JumpParent ) {
				return _rgFileList.Execute( sGuid );
			}

            return base.Execute(sGuid);
        }
    } // End class WindowTextDir

	public class WindowFileViewer : 
		WindowRxBase,
		IPgSave<XmlDocumentFragment>,
		IPgLoad<XmlElement>
	{
		public    static          Guid   GUID { get; } = new Guid("{C4521EB7-3F16-4576-80A0-24F5396C23F7}");

		protected static readonly string _strBaseTitle2 = "MySSTV : File Viewer";
		public    override Guid   Catagory     => GUID;
		protected override string IconResource => "Play.SSTV.Content.icons8_tv.png";

		protected readonly FileChooser    _rgWavFileList; // Recorded wave files.
		protected readonly ImageWalkerDir _rgDecodedImages;

        public override string Banner {
			get { return "MySSTV File Reader"; } 
		}

		public WindowFileViewer( IPgViewSite oSiteBase, DocSSTV oDocSSTV ) : base( oSiteBase, oDocSSTV ) {
			_rgWavFileList   = new( new WinSlot( this, ChildID.None ) );
			_rgDecodedImages = new( new WinSlot( this, ChildID.None ) );

			//_wmViewRxImg      = new( new SSTVWinSlot( this, ChildID.RxWindow ), _oDocSSTV.ReceiveImage );
			//_wmViewRxHistory  = new( new SSTVWinSlot( this, ChildID.None     ), _rgDecodedImages );

			//_wmViewRxImg    .Parent = this;
			//_wmViewRxHistory.Parent = this;

			_wmViewRxImg.SetBorderOn();
		}

        protected override void Dispose( bool fDisposing ) {
			if( fDisposing && !_fDisposed ) {
				_oDocSSTV     .PropertyChange  -= OnPropertyChange_DocSSTV;
				_rgWavFileList.DirectoryChange -= OnDirectoryChange_WaveFiles;
			}
			base.Dispose( fDisposing );
        }

        public override bool InitNew() {
			if( !base.InitNew() )
				return false;

			string strMyDocs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments );

			if( !_rgWavFileList  .LoadURL( strMyDocs )) { 
				LogError( "File Viewer", "Couldn't view wave file load directory.");
			}
			if( !_rgDecodedImages.LoadURL( strMyDocs )) {
				LogError( "File Viewer", "Couldn't find pictures history directory for SSTV");
				return false;
			}

			if( !_wmViewRxImg.InitNew() )
				return false;
			if( !_wmViewRxHistory.InitNew() )
				return false;

			_oDocSSTV     .PropertyChange  += OnPropertyChange_DocSSTV;
			_rgWavFileList.DirectoryChange += OnDirectoryChange_WaveFiles;

			// Of course we'll blow up the shell if try in the constructor...
			OnDirectoryChange_WaveFiles( _rgWavFileList.CurrentDirectory );

            _oLayout.Add( new LayoutControl( _wmViewRxImg,     LayoutRect.CSS.None ) );
            _oLayout.Add( new LayoutControl( _wmViewRxHistory, LayoutRect.CSS.Pixels, 220 ) );

            OnSizeChanged( new EventArgs() );
			return true;
        }

        private void OnDirectoryChange_WaveFiles( string strDirectory ) {
			// BUG: this will blass any loaded value when you open this window
			//      on InitNew(). Probably should use a different file list.
			_oDocSSTV.RxHistoryList.LoadAgain( strDirectory );
        }

        /// <summary>
        /// This is our event sink for property changes on the SSTV document.
        /// </summary>
        /// <remarks>Right now just update all, but we can just update the
        /// specific property in the future. You know, a color coded property, 
        /// light red or yellow on change would be a cool feature.</remarks>
        private void OnPropertyChange_DocSSTV( SSTVEvents eProp ) {
			_wmViewRxImg.Invalidate();
			_oSiteView  .Notify( ShellNotify.BannerChanged );
        }

		public override object Decorate(IPgViewSite oBaseSite,Guid sGuid) {
			try {
				if( sGuid.Equals(GlobalDecorations.Properties) ) {
					return new ViewFileProperties( oBaseSite, _oDocSSTV.Properties );
				}
				if( sGuid.Equals( GlobalDecorations.Outline ) ) {
					return new WindowTextDir( oBaseSite, _rgWavFileList );
				}
				if( sGuid.Equals( GlobalDecorations.Options ) ) {
				  //return new CheckList( oBaseSite, _oDocSSTV.RxModeList );
					return new WindowFileTools( oBaseSite, _oDocSSTV );
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
				//_oDocSSTV.ReceiveFileRead( _rgWavFileList.CurrentFullPath, _oDocSSTV.RxModeList.ChosenMode );
				return true;
			}
			if( sGuid == GlobalCommands.Stop ) {
				// Might be a good place for a thread cancellation token here.
			}
			if( sGuid == GlobalCommands.Save ) {
				// We shouldn't need to do anything here, except I do override the behavior
				// so we don't attempt to save the settings.
				return true;
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
