using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Printing;
using System.Windows.Forms;
using System.Reflection;
using System.Linq;
using System.Runtime.InteropServices;
using System.Web;
using System.Text;

using SkiaSharp;
using SkiaSharp.Views.Desktop;

using Play.Drawing;
using Play.Interfaces.Embedding;
using Play.Sound;
using Play.Edit;
using Play.Forms;
using Play.Parse.Impl;
using Play.Rectangles;

namespace Play.ImageViewer {
    unsafe public class FileOperationAPIWrapper {
        /// <summary>
        /// Flags for the SHFileOperation method. Original code from Eugene Cheverda on
        /// stack overflow... http://stackoverflow.com/questions/3282418/send-a-file-to-the-recycle-bin
        /// I cut it down a bit.
        /// </summary>
        [Flags]
        public enum FileOperationFlags : ushort
        {
            /// <summary>
            /// Do not show a progress dialog during the process
            /// </summary>
            FOF_SILENT = 0x0004,
            /// <summary>
            /// Do not ask the user to confirm selection
            /// </summary>
            FOF_NOCONFIRMATION = 0x0010,
            /// <summary>
            /// Delete the file to the recycle bin.  (Required flag to send a file to the recycle bin)
            /// </summary>
            FOF_ALLOWUNDO = 0x0040,
            /// <summary>
            /// Do not show the names of the files or folders that are being recycled.
            /// </summary>
            FOF_SIMPLEPROGRESS = 0x0100,
            /// <summary>
            /// Surpress errors, if any occur during the process.
            /// </summary>
            FOF_NOERRORUI = 0x0400,
            /// <summary>
            /// Don't operate on connected elements. Kinda rare-but you see it on saved mhtml style files.
            /// </summary>
            FOF_NO_CONNECTED_ELEMENTS = 0x2000,  
            /// <summary>
            /// Warn if files are too big to fit in the recycle bin and will need
            /// to be deleted completely.
            /// </summary>
            FOF_WANTNUKEWARNING = 0x4000,
        }

        /// <summary>
        /// File Operation Function Type for SHFileOperation
        /// </summary>
        public enum FileOperationType : uint
        {
            FO_MOVE   = 0x0001,
            FO_COPY   = 0x0002,
            FO_DELETE = 0x0003,
            FO_RENAME = 0x0004,
        }

        /// <summary>
        /// SHFILEOPSTRUCT for SHFileOperation from COM
        /// </summary>
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct SHFILEOPSTRUCT
        {

            public IntPtr hwnd;
            [MarshalAs(UnmanagedType.U4)]
                public FileOperationType wFunc;
            public string pFrom;
            public string pTo;
            public FileOperationFlags fFlags;
            [MarshalAs(UnmanagedType.Bool)]
                public bool fAnyOperationsAborted;
            public IntPtr hNameMappings;
            public string lpszProgressTitle;
        }

        // Use Marshal.GetLastWin32Error() to try to devine errors.
        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        private static extern int SHFileOperation(ref SHFILEOPSTRUCT FileOp);

        /// <summary>
        /// Send file to recycle bin. Delete if too large.
        /// </summary>
        /// <param name="path">Location of directory or file to recycle</param>
        public static bool FileRecycle(string path)
        {
            return FileDelete( path, 
                               //FileOperationFlags.FOF_NOCONFIRMATION | 
                                    FileOperationFlags.FOF_WANTNUKEWARNING |
                                    //FileOperationFlags.FOF_NOERRORUI | 
                                    //FileOperationFlags.FOF_SILENT |
                                    FileOperationFlags.FOF_ALLOWUNDO );

        }

        /// <summary>
        /// For completely silent delete use...
        ///     FileOperationFlags.FOF_NOCONFIRMATION | 
        ///     FileOperationFlags.FOF_NOERRORUI |
        ///     FileOperationFlags.FOF_SILENT;
        /// </summary>
        private static bool FileDelete(string strPath, FileOperationFlags flags)
        {
            int iReturn = 0;
            try
            {
                SHFILEOPSTRUCT fs = new SHFILEOPSTRUCT
                                        {
                                            wFunc  = FileOperationType.FO_DELETE,
                                            pFrom  = strPath + '\0', // Already zero terminated. But sometimes just misses if don't add one.
                                            fFlags = flags
                                        };
                iReturn = SHFileOperation(ref fs);
            }
            catch (Exception oEx ) { 
                Type[] rgErrors = { 
                    typeof( EntryPointNotFoundException ),
                    typeof( MissingMethodException ),
                    typeof( NotSupportedException ),
                    typeof( OutOfMemoryException ),
                    typeof( AccessViolationException ),
                    typeof( StackOverflowException ),
                    typeof( DllNotFoundException ),
                    typeof( BadImageFormatException ),
                    typeof( MethodAccessException )
				};

				if( rgErrors.IsUnhandled( oEx ) )
					throw;

                // Pretty evil to eat this exception. Need to look up standard interop errors.
                // and look up the return from SHFileOperation to throw equivalent exceptions
                // to File.Delete()
                return false;
            }
 
            return( iReturn == 0 );
       }
    }
    
    /// <summary>
    /// http://stackoverflow.com/questions/17099962/how-do-i-create-a-cursor-object-from-a-bitmap-object 
    /// user Walt D
    /// 
    /// Need to move this to my drawing namespace sometime.
    /// </summary>
    public struct IconInfo
    {
        public bool   fIcon;
        public int    xHotspot;
        public int    yHotspot;
        public IntPtr hbmMask;
        public IntPtr hbmColor;
    }

    unsafe public class User32 {
        public const Int32 WS_POPUP = unchecked((int)0x80000000);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetIconInfo(IntPtr hIcon, ref IconInfo pIconInfo);
        [DllImport("user32.dll")]
        public static extern IntPtr CreateIconIndirect(ref IconInfo icon);

        /// <summary>
        /// Create a cursor from a bitmap without resizing and with the specified
        /// hot spot. Load bmp from a "png" and you get transparency!
        /// This bit of code isn't unsafe, could move out if I want to.
        /// </summary>
        public static Cursor CreateCursorNoResize(Bitmap bmp, int xHotSpot, int yHotSpot)
        {
            IntPtr   ptr = bmp.GetHicon();
            IconInfo tmp = new IconInfo();

            GetIconInfo(ptr, ref tmp);
            tmp.xHotspot = xHotSpot;
            tmp.yHotspot = yHotSpot;
            tmp.fIcon    = false;

            ptr = CreateIconIndirect(ref tmp);

            return new Cursor(ptr);
        }
    }

    public class FileLine : TextLine {
        public long     FileSize     {get;set;} = 0;
        public DateTime ModifiedDate {get;set;}
		public bool     IsDirectory  {get;set;} = false;

        public FileLine( int iLine, string strValue) : base( iLine, strValue )
        { }
    }

    public class FileEditor : BaseEditor {
        public FileEditor( IPgBaseSite oSite ) : base( oSite ) {

        }
        protected override Line CreateLine( int iLine, string strValue )
        {
            return( new FileLine( iLine, strValue ) );
        }
    }
    
	/// <summary>
	/// Loads a single image from a stream, no navigation, left or right or anything.
    /// Don't confuse this with the DocWalker DirWalker objects and viewers.
	/// </summary>
	public class ImageSoloDoc : 
		ImageBaseDoc,
		IPgLoad<Stream>,
		IPgSave<Stream>
	{
		public ImageSoloDoc( IPgBaseSite oSiteBase ) : base( oSiteBase ) {
		}

		public void BitmapDispose() {
            if( Bitmap != null ) {
                SKBitmap skTemp = Bitmap;
                Bitmap = null; // This sends the event.
                skTemp.Dispose();
            }
		}
		
		public void Raise_BitmapDispose() {
			BitmapDispose();
			//Raise_ImageUpdated();
		}

		public virtual bool InitNew() {
            if( !Initialize() )
                return false;

			Raise_BitmapDispose();

			return true;
		}

        public bool LoadResource( Assembly oAssembly, string strResourceName ) {
			BitmapDispose();

            if( !Initialize() )
                return false;

			try {
                // Let's you peep in on all of them! ^_^
                // string[] rgStuff = oAssembly.GetManifestResourceNames();

				using( Stream oStream = oAssembly.GetManifestResourceStream( strResourceName )) {
					return Load( oStream );
				}
			} catch( Exception oEx ) {
				Type[] rgErrors = { typeof( NullReferenceException ), 
									typeof( ArgumentNullException ),
									typeof( ArgumentException ),
									typeof( FileLoadException ),
									typeof( BadImageFormatException ),
									typeof( NotImplementedException ) };
				if( !rgErrors.Contains( oEx.GetType() ) )
					throw;

				throw new InvalidOperationException( "Could not retrieve given image resource : " + strResourceName );
			}
        }

		public bool Load(Stream oStream) {
            ArgumentNullException.ThrowIfNull( oStream );

			BitmapDispose();

            try {
                Bitmap = SKBitmap.Decode( oStream );
			} catch( Exception oEx ) {
				if( _rgBmpLoadErrs.IsUnhandled( oEx ) )
					throw;

                _oSiteBase.LogError( "storage", "Couldn't read file for solo image...", false );
                return false;
			} finally {
				Raise_ImageUpdated();
			}
			return true;
		}

        /// <summary>
        /// Load our bitmap from the given bitmap. Use an Rgba8888 image to be compatible
        /// with my text printing system.
        /// </summary>
        /// <param name="srcImage">source image</param>
        /// <param name="rcSourcePortion">portion of the source image.</param>
        /// <param name="szDestSize">Size of destination bitmap to create.</param>
		public bool Load(SKBitmap srcImage, SKRectI rcSourcePortion, SKSizeI szDestSize ) {
			SmartRect rcDest = new SmartRect( LOCUS.UPPERLEFT, 0, 0, szDestSize.Width, szDestSize.Height );

			BitmapDispose();

			try {
				Bitmap = new SKBitmap( szDestSize.Width, szDestSize.Height, SKColorType.Rgba8888, SKAlphaType.Opaque );
                using( SKCanvas skCanvas = new SKCanvas( Bitmap ) ) {
                    using( SKPaint skPaint = new SKPaint() ) {
                        skPaint .FilterQuality = SKFilterQuality.High;
                        skCanvas.DrawBitmap( srcImage, 
                                             new SKRect( rcSourcePortion.Left, rcSourcePortion.Top, rcSourcePortion.Right, rcSourcePortion.Bottom ),
                                             new SKRect( rcDest.Left, rcDest.Top, rcDest.Right, rcDest.Bottom ) );
                    }
                }
				//using( Graphics g = Graphics.FromImage( _oBitmapDisplay ) ) {
				//	g.SmoothingMode      = SmoothingMode     .HighQuality;
				//	g.CompositingQuality = CompositingQuality.HighQuality;
				//	// Can't turn this one on. There's a bug causing a blackish line on the left and top of the image.
				//	//g.InterpolationMode  = InterpolationMode .HighQualityBicubic;

				//	g.DrawImage( srcImage, rcDest.Rect, rcSourcePortion, GraphicsUnit.Pixel );
				//}
			} catch( Exception oEx ) {
				Type[] rgErrors = { typeof( ArgumentException ),
									typeof( ArgumentNullException ),
									typeof( NullReferenceException ) };
				if( rgErrors.IsUnhandled( oEx ) )
					throw;

				return false;
			} finally {
				Raise_ImageUpdated();
			}

			return true;
		}

		public virtual bool IsDirty => false;

		private ImageCodecInfo GetEncoder(ImageFormat format) {  
			ImageCodecInfo[] codecs = ImageCodecInfo.GetImageDecoders();  
			foreach (ImageCodecInfo codec in codecs) {  
				if (codec.FormatID == format.Guid) {  
					return codec;  
				}  
			}  
			return null;  
		}
		
		public bool Save(Stream oStream) {
			try {
                // This is how it's supposed to work now. But it doesn't work.
                //using SKImage oImage     = SKImage.FromBitmap( Bitmap );
                //using SKData  oImageData = oImage.Encode( SKEncodedImageFormat.Jpeg, 80 );
                //oImageData.SaveTo( oStream );

                // Well, the skiasharp bitmap doesn't want to save, but the windows one is
                // happy to save. So just use it for now. This is weird because I'm having
                // no problem saving the thumbs in the zipfile for the ImageWalkDoc!
				ImageCodecInfo    oJpgEncoder    = GetEncoder(ImageFormat.Jpeg);  
				EncoderParameters oEncoderParams = new EncoderParameters(1);  

				// BUG: Get this from a ImageSoloDoc property in the future.
				oEncoderParams.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, 85L);  

                using Bitmap oBmp = Bitmap.ToBitmap();

                oBmp.Save( oStream, oJpgEncoder, oEncoderParams );

                // This is the old SKIA way but it's not working either.
                //Bitmap.Encode( oStream, SKEncodedImageFormat.Jpeg, 100 );

				return oStream.Length > 0;
			} catch( Exception oEx ) {
				Type[] rgErrors = { typeof( ArgumentNullException ),
									typeof( ExternalException ),
                                    typeof( NullReferenceException ) };
				if( rgErrors.IsUnhandled( oEx ))
					throw;

				_oSiteBase.LogError( "ImageSoloDoc", "Persist image error.");

				return false;
			}
		}

        protected override void PrintPageHandler( object sender, PrintPageEventArgs e ) {
            using Bitmap oCopy = Bitmap.ToBitmap();

            SmartRect rctDest   = new SmartRect();
            SmartRect rctSource = new SmartRect( 0, 0, Bitmap.Width, Bitmap.Height );

			ImageHelpers.ViewPortSizeMax( szBorder      : new Size( 0, 0 ), 
										  szView        : new Size( e.MarginBounds.Width, e.MarginBounds.Height ), 
				                          rctBitmapWorld: rctSource,
										  rctViewPortOut: rctDest );

            e.Graphics.DrawImage( oCopy, rctDest.Rect, rctSource.Rect, GraphicsUnit.Pixel );
        }
	}
 
    public delegate void ImagesUpdatedEvent();
    public delegate void TextParsedEvent();
    public delegate void TextLoadedEvent();

	/// <summary>
	/// Loads a single image from a file containing a list of images paths. Then navigate
	/// between the given images, always loading a single image at a time.
	/// </summary>
	public class ImageWalkerDoc : ImageSoloDoc,
        IPgCommandBase,
        IPgLoad<TextReader>,
        IPgSave<TextWriter>,
        IReadableBag<Line>, 
        IDisposable
    {
        protected readonly IPgFileSite       _oSiteFile;
        protected readonly IPgRoundRobinWork _oSiteWorkThumb;
        protected readonly IPgRoundRobinWork _oSiteWorkParse;
		protected readonly IPgRoundRobinWork _oWorkPlace;
        protected readonly Grammer<char>     _oGrammar;

        protected string _strIcon = @"ImageViewer.Content.icons8-cardboard-box-48.png"; // Can get overridden by subclass.
        protected int    _iBlockFilesEvent = 0;
        protected bool   _fDirtyDoc        = false;
        protected bool   _fDirtyThumbs     = false;
        protected Line   _oDisplayLine;

        /// <summary>
        /// This will be the property you can use to show where you are. The
        /// user can edit it and we can send that back to try to update.
        /// </summary>
        public Line CurrentShowPath { get; } = new TextLine( 0, string.Empty );
        public Line CurrentShowFile { get; } = new TextLine( 1, string.Empty );

        internal ImageProperties Properties { get; }
        internal FileEditor      FileList   { get; }

        static protected readonly Dictionary<PixelFormat, string> _rgPixelDescription = new Dictionary<PixelFormat,string>();

        /// <summary>
        /// This might be obsolete now that I have a TextLoaded event instead of having
        /// views listen to the FileList events directly from the FileList.
        /// </summary>
		public class GateFilesEvent : IDisposable {
			readonly ImageWalkerDoc _oDoc;

			public GateFilesEvent( ImageWalkerDoc oDoc ) {
				_oDoc = oDoc ?? throw new ArgumentNullException( "Can't initiate Block Files Semaphore." );
				_oDoc._iBlockFilesEvent++;
			}

			public bool Open {
				get {
					return( _oDoc._iBlockFilesEvent <= 1 );
				}
			}

			public void Dispose() {
				_oDoc._iBlockFilesEvent--;
			}
		}

        public class ImageProperties : DocProperties {
            public enum Names : int {
                Name,
                Width,
                Height,
                Depth,
                Modified,
                Size,
                Directory,
                MAX
            }

            public ImageProperties( IPgBaseSite oSiteBase ) : base( oSiteBase ) {
            }

            public override bool InitNew() {
                if( !base.InitNew() ) 
                    return false;

                for( int i=0; i<(int)Names.MAX; ++i ) {
                    CreatePropertyPair();
                }

                LabelUpdate( Names.Name,      "Name", new SKColor( red:0xff, green:0xbf, blue:0 ) );
                LabelUpdate( Names.Width,     "Width" );
                LabelUpdate( Names.Height,    "Height" );
                LabelUpdate( Names.Depth,     "Depth" );
                LabelUpdate( Names.Modified,  "Modified" );
                LabelUpdate( Names.Size,      "Size" );
                LabelUpdate( Names.Directory, "Dir" );

                ValuesEmpty();

                return true;
            }

            public void LabelUpdate( Names eName, string strLabel, SKColor? skBgColor = null ) {
                LabelUpdate( (int)eName, strLabel, skBgColor );
            }

            public void ValueUpdate( Names eName, string strValue, bool Broadcast = false ) {
                ValueUpdate( (int)eName, strValue, Broadcast );
            }

            public DocProperties.Manipulator CreateManipulator() {
                return new DocProperties.Manipulator( this );
            }
        }

        /// <summary>
        /// This is for our editor instance we are hosting!!
        /// </summary>
        public class ImageWalkerDocSlot : 
			IPgBaseSite,
			IPgFileSite
		{
			readonly ImageWalkerDoc _oDoc;

			/// <summary>
			/// This is for our editor instance we are hosting!!
			/// </summary>
			public ImageWalkerDocSlot( ImageWalkerDoc oDoc ) {
				_oDoc = oDoc ?? throw new ArgumentNullException( "Image document must not be null." );
			}

			public void LogError( string strMessage, string strDetails, bool fShow=true ) {
				_oDoc.LogError( strMessage, "ImageWalker : " + strDetails );
			}

			public void Notify( ShellNotify eEvent ) {
			}

			public IPgParent Host       => _oDoc;
			public FILESTATS FileStatus => FILESTATS.UNKNOWN;
			public string    FilePath   => string.Empty;
            public string    FileName   => string.Empty;

			public virtual Encoding FileEncoding => Encoding.Default;
		}

        // .bmp loads a bit slow. Need some sort "I'm loading" message for that
        // Let's just skip 'em for now. 'Course if shell click on it, shell will show a blank. ^_^;;
        // NOTE: Perhaps should load this list from the config file.
        // NOTE: 8/7/2020, Probably can handle bmp latency with the new ThumbsPopulateEnum...
        public static readonly string[] _rgFileExts = { ".jpeg", ".jpg", ".gif", ".png", ".webp" };

        public event ImagesUpdatedEvent  ThumbsUpdated;
        public event TextParsedEvent     TextParsed;
        public event TextLoadedEvent     TextLoaded;
        public event Action<ShellNotify> MediaEvent;

        public ImageWalkerDoc( IPgBaseSite oSiteBase ) : base( oSiteBase ) {
            _oSiteFile = (IPgFileSite)oSiteBase ?? throw new ArgumentNullException();

            IPgScheduler oScheduler = Services as IPgScheduler ?? throw new ArgumentException( "Host requires IPgScheduler" );

            _oSiteWorkParse = oScheduler.CreateWorkPlace() ?? throw new InvalidProgramException  ( "Could not create a worksite1 from scheduler.");
			_oSiteWorkThumb = oScheduler.CreateWorkPlace() ?? throw new InvalidOperationException( "Could not create a worksite2 from scheduler." );
            _oWorkPlace     = oScheduler.CreateWorkPlace() ?? throw new InvalidOperationException( "Could not create a worksite from scheduler." );

            if( Services is IPgGrammers oGrammars ) {
				_oGrammar = oGrammars.GetGrammer( "directory" ) as Grammer<char>; 
				if( _oGrammar == null )
					throw new GrammerNotFoundException( "Could not find directory grammer for image walker" );
			}

            if( _rgPixelDescription.Count == 0 ) {
                _rgPixelDescription.Add( PixelFormat.Format1bppIndexed, "1bit - Palette" );
                _rgPixelDescription.Add( PixelFormat.Format4bppIndexed, "4bit - Palette" );
                _rgPixelDescription.Add( PixelFormat.Format8bppIndexed, "8bit - Palette" );
                _rgPixelDescription.Add( PixelFormat.Format24bppRgb,    "24bit - RGB" );
                _rgPixelDescription.Add( PixelFormat.Format32bppRgb,    "32bit - RGB" );
                _rgPixelDescription.Add( PixelFormat.Format32bppArgb,   "32bit - ARGB" );
            }

			try {
                FileList   = new FileEditor     ( new ImageWalkerDocSlot( this ) );
                Properties = new ImageProperties( new ImageWalkerDocSlot( this ) );
            } catch( InvalidCastException ) {
                LogError( "DocWalker", "Couldn't host internal elements for ImageWalker.");
            }

            try {
				Icon = SKImageResourceHelper.GetImageResource( Assembly.GetExecutingAssembly(), _strIcon );
            } catch( InvalidOperationException ) {
                LogError( "ImageWalkDoc", "Having problem finding folder bitmap resource." );
            }

        }

        /// <summary>
        /// Show the file we are viewing and the file from which it came. 
        /// But if we're collecting scraps from the shell, we won't necessarily
        /// have a file name so just add "scraps"
        /// </summary>
        public virtual string Banner { 
            get {
                StringBuilder oBuilder = new();

                if( string.IsNullOrEmpty( _oSiteFile.FileName ) ) {
                    oBuilder.Append( "scraps" );
                } else {
                    oBuilder.Append( _oSiteFile.FileName );
                }

                return oBuilder.ToString();
        }   }
        public         SKImage  Icon { get; }

        internal void LogError( string strCatagory, string strMessage ) {
            _oSiteBase.LogError( strCatagory, strMessage );
        }

        public override void Dispose() {
			_oSiteWorkThumb.Stop();

            ThumbsDispose();

            if( Icon != null ) {
                Icon.Dispose();
            }
            if( _oBitmapUnknown != null ) {
                _oBitmapUnknown.Dispose();
                _oBitmapUnknown = null;
            }

			base.Dispose();
        }

		protected override bool Initialize() {
			if( !base.Initialize() )
				return false;

            if( !Properties.InitNew() )
                return false;

            //FileList.BufferEvent += OnFileList_BufferEvent;

            return( true );
        }

		public override bool InitNew() {
            if( !base.InitNew() ) 
                return( false );

            if( !FileList.InitNew() )
                return( false );

            return( true );
        }

        /// <summary>
        /// If the filename we saved in our stream is not there, we should put up an error
        /// on the screen but next/prev will work if can find the directory.
        /// BUG: Need to have my documents load from a stream and not a stream reader (I think).
        /// </summary>
        public virtual bool Load( TextReader oReader ) {
            if( !Initialize() )
                return( false );

            FileList.Load( oReader );

            Raise_TextLoaded();

            Next( 0 );

            _oSiteWorkThumb.Queue( ThumbsCreateEnum( new SKSize(100, 100)), 0 );
            _oSiteWorkParse.Queue( CreateParseWorker(), 0 );

            return( true );
        }

        public static bool IsFileExtensionUnderstood( string strFileExtn ) {
            if( string.IsNullOrEmpty(strFileExtn) )
                return false;
						
            try { 
                return _rgFileExts.Contains( strFileExtn.ToLower() );
            } catch( ArgumentException ) { 
                return false;
            }
        }

        /// <summary>
        /// The Line extra can be null and we still "understand" the line. Just
        /// looking for a file type that we care about.
        /// </summary>
		public bool IsLineUnderstood( Line oLine ) {
            try { 
			    return( IsFileExtensionUnderstood( Path.GetExtension( oLine.ToString() ) ) );
            } catch( Exception oEx ) { 
                Type[] rgErrors = { typeof( ArgumentException ),
                                    typeof( ArgumentNullException ),
                                    typeof( NullReferenceException )};
                if( rgErrors.IsUnhandled( oEx ))
                    throw;

                return false;
            }
		}

		/// <summary>
		/// TODO: I might be able to put this one in the integration class. There is another copy out there.
		/// </summary>
		//protected void CreateNavPropsParser() {
		//	IPgScheduler        oScheduler   = null;
		//	IPgGrammers oGrammars    = null;
		//	Grammer<char>       oTextGrammer = null;

		//	try {
		//		oScheduler   = (IPgScheduler) Services;
		//		oGrammars    = (IPgGrammers)  Services;
		//		oTextGrammer = (Grammer<char>)oGrammars.GetGrammer( "properties" );
		//	} catch( Exception oEx ) {
		//		Type[] rgError = { typeof( NullReferenceException ), 
		//						   typeof( InvalidCastException ),
		//						   typeof( FileNotFoundException ),
		//						   typeof( GrammerNotFoundException ) };
		//		if( !rgError.Contains( oEx.GetType() ))
		//			throw;

		//		_oSiteBase.LogError( "hosting", "Nav Props Parser constructor error.", true );
		//		return; // Do nothing.
		//	}
			
		//	try {
		//		// BUG: These will never get freed becauses the worksite never gets released.
		//		//      need to hook the parser handler to the document dispose.
		//		new ParseHandlerBase( oScheduler.CreateWorkPlace(), EditProperties, oTextGrammer );
		//	} catch( Exception oEx ) {
		//		Type[] rgError = { typeof( NullReferenceException ), 
		//						   typeof( ArgumentNullException ),
		//						   typeof( InvalidOperationException ) };
		//		if( !rgError.Contains( oEx.GetType() ))
		//			throw;
		//	}
		//}
  
        public virtual void DirectoryNext( int i ) {
        }

        public override bool IsDirty {
            get { 
                return( _fDirtyDoc );
            }
        }

        /// <summary>
        /// Unlike the ImageWalkerDir, here we actualy want to save
        /// our contents out as a file!!
        /// </summary>
        public bool Save(TextWriter oStream) {
            FileList.Save( oStream );
            _fDirtyDoc = false;
            return( true );
        }

        public virtual bool Save() {
            // You know, we could save a thumbnail file for this list. Maybe.
            return true;
        }

        /// <summary>
        /// Save Image thumbnails to the given zip archive. Note: we won't have any images to save
        /// if we didn't open a thumbnail viewer.
        /// </summary>
        /// <remarks>
        /// Saving seems a bit slow even for small images. 
        /// Note that the Bitmap object in .NET CORE now needs to READ from the stream it 
        /// is saving to!! 
        /// 6/18/2020. I get around this by  saving first to a memory stream, and then 
        ///            saving that object to the zip file.
        /// 2/19/2926. I'm using SKImage which seems to be well behaved.
        /// BUG: No need to save if no files were deleted.
        /// </remarks>
        protected void ThumbsSave( ZipArchive oZip ) {
            DateTime  oSavedTime   = DateTime.Now;
            Exception oLastErr     = null;

            foreach( Line oLine in FileList ) {
                try {
                    if( IsLineUnderstood( oLine ) && oLine.Extra is SKImage oImage ) {
                        string          strLineEncoded = HttpUtility.HtmlEncode( oLine.ToString() );
                        ZipArchiveEntry oZipEntry      = oZip.CreateEntry( strLineEncoded );

                        using Stream oZipStream   = oZipEntry.Open();
                        using SKData oEncodedData = oImage.Encode(SKEncodedImageFormat.Jpeg, 50);

                        oEncodedData.SaveTo( oZipStream );
                    }
                } catch( Exception oEx ) {
                    Type[] rgErrors = { typeof( NullReferenceException ),
                                        typeof( ArgumentNullException ),
                                        typeof( ObjectDisposedException ),
                                        typeof( ExternalException ),
                                        typeof( NotSupportedException ),
                                        typeof( NullReferenceException ),
                                        typeof( InvalidCastException ) };
                    if( rgErrors.IsUnhandled( oEx ) )
                        throw;

                    oLastErr = oEx;
                }
                if( DateTime.Now.Millisecond - oSavedTime.Millisecond > 250 &&
                    Cursor.Current != Cursors.WaitCursor ) 
                {
                    Cursor.Current = Cursors.WaitCursor;
                }
            }
            if( oLastErr != null )
                LogError( "ImageViewer", "Trouble persisting thumbnail images." );

            Cursor.Current = Cursors.Default;
        }

        /// <summary>
        /// Load up the thumbnails from a prexisting cache.
        /// </summary>
        protected void ThumbsLoad( ZipArchive oZip ) {
            ThumbsDispose();

            foreach (Line oLine in FileList ) {
                try {
                    string strLineEncoded = HttpUtility.HtmlEncode( oLine.ToString() );
                    ZipArchiveEntry oZipEntry = oZip.GetEntry( strLineEncoded );
                    // Looks like new behavior under .net core. I won't get an exception I'll get null entry. Speed improvement!
                    if( oZipEntry != null ) {
                        using Stream oZipStream = oZipEntry.Open();
                        oLine.Extra = SKImage.FromEncodedData( oZipStream );
                    }
                } catch( Exception oEx ) {
                    Type[] rgErrors = { typeof( NullReferenceException ),
                                        typeof( ArgumentNullException ),
                                        typeof( ArgumentException ),
                                        typeof( NotSupportedException ),
                                        typeof( ObjectDisposedException ),
                                        typeof( InvalidDataException ),
                                        typeof( ExternalException ) };
                    if( !rgErrors.Contains( oEx.GetType() ) ) {
                        throw new InvalidProgramException( "Trouble loading image thrumbnail's" );
                    }
                }
            }
            Raise_UpdatedThumbs( DirtyThumbs:false );
        }
        
        protected void ThumbsDispose() {
            foreach( Line oLine in FileList ) {
                // Get rid of the bitmap we are hiding in the extra.
                IDisposable oExtraDispose = oLine.Extra as IDisposable;
                if( oExtraDispose != null ) {
                    oExtraDispose.Dispose(); 
                    oLine.Extra = null;
                }
            }
        }

        /// <summary>
        /// This is very slow. That's why we're using a worker to load and scale all of these!
        /// </summary>
        protected SKImage ReScale( string strFullPath, SKSize skSize ) {
            try {
			    LayoutImageReference oTempRct = new LayoutImageReference( skSize );

                using Stream  oStream  = new FileStream( strFullPath, FileMode.Open );
			    using SKImage oImgTemp = SKImage.FromEncodedData( oStream );

			    return oTempRct.CreateReScaledImage( oImgTemp ); 
            } catch( Exception oEx ) {
                Type[] rgErrors = { typeof( ArgumentException ), 
                                    typeof( ArgumentNullException ),
                                    typeof( System.Security.SecurityException ),
                                    typeof( FileNotFoundException ), 
                                    typeof( UnauthorizedAccessException ),
                                    typeof( IOException ),
                                    typeof( DirectoryNotFoundException ), 
                                    typeof( PathTooLongException ),
                                    typeof( ArgumentOutOfRangeException ),
                                    typeof( NullReferenceException ) };
                if( rgErrors.IsUnhandled( oEx ) )
                    throw;

                LogError( "Image Walker", "Problem Resizing to thumbnail" );

                return null;
            }
        }

        /// <summary>
        /// This is an iterator that generates the thumb nail bitmaps! Very kewl!!
        /// </summary>
        /// <returns>Return time can wait until next call.</returns>
        public IEnumerator<int> ThumbsCreateEnum( SKSize skSize ) {
            int iCount = 0;

            // Look for thumbs that have not been rendered yet.
            foreach( Line oFileName in FileList ) {
                if( oFileName.Extra == null ) {
                    string strFileName = oFileName.ToString();
                    string strFileExtn = string.Empty;
                    try {
                        strFileExtn = Path.GetExtension( strFileName );
                    } catch( ArgumentException ) {
                        continue;
                    }
					if( ImageWalkerDoc.IsFileExtensionUnderstood( strFileExtn ) ) {
						oFileName.Extra = ReScale( Path.Combine( CurrentDirectory, oFileName.ToString() ), skSize ); 

                        if( ++iCount % 5 == 0 ) {
                            Raise_UpdatedThumbs();
                        }
						yield return( 0 );
					}
                }
            }
            if( iCount != 0 )
                Raise_UpdatedThumbs(); // finally notify all views one last time.
        }

        public Line CurrentElement {
            get {
                return( _oDisplayLine ); 
            }
        }

        /// <summary>
        /// Get file name without path.
        /// </summary>
        public string CurrentFileName {
            get { 
                try {
                    return Path.GetFileName( _oDisplayLine.ToString() ); 
                } catch( Exception oEx ) {
                    Type[] rgErr = { typeof( NullReferenceException ),
                                     typeof( ArgumentException ) };

                    if( !rgErr.Contains( oEx.GetType() ) )
                        throw new InvalidOperationException();
                }
                return( string.Empty );
            }
        }

        public virtual string CurrentDirectory {
            get { 
                try {
                    return( Path.GetDirectoryName( _oDisplayLine.ToString() ) );
                } catch( Exception oEx ) {
                    Type[] rgErr = { typeof( NullReferenceException ),
                                     typeof( ArgumentException ) };

                    if( !rgErr.Contains( oEx.GetType() ) )
                        throw new InvalidOperationException();
                }
                return string.Empty;
            }
        }

        protected virtual string FullPathFromLine( Line oLine ) {
            // If path is relative, I can't get the path from the shell unless I allow the document
            // to have it. IPgFileSite does not allow site to have it!? So if the current directory
            // is not the same as the path to our document. We can't load path relative images!
            return oLine.ToString();
        }

		public virtual string CurrentFullPath {
			get { return _oDisplayLine.ToString(); }
		}

        public Line this[int iIndex] { 
            get { return( FileList[iIndex] ); } 
        }

        public int ElementCount { get { return( FileList.ElementCount ); } }
        
        /// <summary>
        /// This Enumerator is a little different since the document
        /// and not the view is the keeper of the "pointer" to the
        /// selected line.
        /// </summary>
        public IEnumerator<ILineRange> CreateLineSearch() {
            int iStart = 0;

            if( _oDisplayLine != null )
                iStart = _oDisplayLine.At;

            return FileList.CreateLineSearch( iStart, 0 );
        }

        protected void ImageLoad( string strFileName ) {
            foreach( Line oLine in FileList ) {
                if( oLine.CompareTo( strFileName ) == 0 ) {
                    ImageLoad( oLine );
                    break;
                }
            }
        }

        /// <remarks>So I would love to just show the "_oDisplayLine" for the
        /// "CurrentShowPath" but unfortunately the DisplayLine pointer changes 
        /// all the time. Also, in the Directory walk case, it's a relative path.
        /// But in the Document walker it's a list of URL's which can be
        /// full path names or relative path. 
        /// </remarks>
        public bool ImageLoad( Line oLine, bool fSendImageUpdate = false ) {
            // If a bum path got inserted, it's still our current index.
			Line oDisplayOld = _oDisplayLine;

            _oDisplayLine = oLine;

			if( Line.IsNullOrEmpty( _oDisplayLine ) ||
                !IsLineUnderstood( _oDisplayLine ) )
            {
                BitmapDispose();
                Raise_ImageUpdated();
                return false;
            }
            try {
				FileLine oFileOld = oDisplayOld as FileLine;
                FileLine oFileNew = _oDisplayLine as FileLine;
                FileInfo oFile    = new FileInfo( CurrentFullPath );
                
                // Make sure these are set all the time so the property page has the values.
                oFileNew.ModifiedDate = oFile.LastWriteTime;
                oFileNew.FileSize     = oFile.Length;

				if( !fSendImageUpdate && !FileLine.IsNullOrEmpty( oFileOld ) ) {

					if( DateTime.Compare( oFileNew.ModifiedDate, oFileOld.ModifiedDate ) == 0 &&
						oFileNew.FileSize == oFileOld.FileSize &&
						Bitmap != null ) {
						return true;
					}
				}
			} catch( Exception oEx ) {
				if( _rgBmpLoadErrs.IsUnhandled( oEx ) )
					throw;
            }

            try {
                // But if this fails, we'll have an bad show path, oh well.
                using( Stream oStream = File.OpenRead( FullPathFromLine( _oDisplayLine ) ) ) {
                    Bitmap = SKBitmap.Decode( oStream );
                }

				return true;
			} catch( Exception oEx ) {
				if( _rgBmpLoadErrs.IsUnhandled( oEx ) )
					throw;

                _oSiteBase.LogError( "storage", "Couldn't read file..." + FullPathFromLine( _oDisplayLine ) );

                return false;
			}            
        }

        protected Line GetNextIndex( int p_iDir ) {
            if( p_iDir > 1 )
                p_iDir = 1;
            if( p_iDir < -1 )
                p_iDir = -1;

            // Note: When we're showing directories. They're never really empty since the ".." element is always present!
            if( FileList.ElementCount <= 0 ) { 
                return( new FileLine(0, string.Empty) );
            }

			int iNext = Line.IsNullOrEmpty(_oDisplayLine) ? 0 : _oDisplayLine.At;

			for( int i=0; i<FileList.ElementCount; ++i ) {
				iNext = ( iNext + p_iDir ) % FileList.ElementCount;

				if( iNext < 0 )
					iNext = FileList.ElementCount - 1;

				// Note: We want to skip directories, however if the icon's haven't been
				// populated, then all the line "extra" will be null and we can't find anything!
				// Use the FileLine._fIsDirectory flag. It's better.
				if( FileList[iNext] is FileLine oDirLine && !oDirLine.IsDirectory )
					break;
			}

            return( FileList[iNext] );
        }

        /// <summary>
        /// Doez what it sez.
        /// </summary>
        /// <param name="p_iDir"></param>
        public void Next( int p_iDir ) {
            ImageLoad( GetNextIndex( p_iDir ) );
        }

        public bool Given( int iIndex ) {
            return( ImageLoad( FileList[iIndex] ) );
        }

        /// <summary>
        /// We only delete the file from the file list. Subclasses will deal with the actual file.
        /// </summary>
        public virtual void CurrentFileDelete() {
            using( GateFilesEvent oGate = new GateFilesEvent( this ) ) {
                // Get rid of the bitmap we are hiding in the extra.
                IDisposable oExtraDispose = _oDisplayLine.Extra as IDisposable;
                if( oExtraDispose != null ) {
                    oExtraDispose.Dispose(); 
                    _oDisplayLine.Extra = null;
                }

                // TODO: Might be nice to listen in on the document and if line is deleted, THEN check
                // if we point at it and move next. Plus, easier to implement UNDO.
                // This returns the element we are going to delete when a directory is finally empty!!
                Line oNext = GetNextIndex( +1 );

                using( Editor.Manipulator oManip = FileList.CreateManipulator() ) {
                    oManip.LineDelete( _oDisplayLine.At );
                }

                // If the linecount is zero, then oNext will have been deleted and invalid.
                // But load index understands this and will reject it.
                if( !ImageLoad( oNext ) ) {
                    BitmapDispose();
                }
                Raise_TextLoaded   ();
                Raise_ImageUpdated ();
                Raise_UpdatedThumbs();
            }
        }

        public void ClipboardCopyTo() {
            DataObject oDataObject  = new DataObject();

            oDataObject.SetData( Path.Combine( CurrentDirectory, CurrentFileName ) );
            Clipboard.SetDataObject( oDataObject );
        }

        public virtual void ClipboardCopyFrom() {
            IDataObject oDataObject = Clipboard.GetDataObject();
            bool        fWasEmpty   = FileList.ElementCount == 0;

            using( GateFilesEvent oGate = new GateFilesEvent( this ) ) {
                if( oDataObject != null ) {
                    if( oDataObject.GetDataPresent(typeof(System.String)) ) {
                        string strFileName = oDataObject.GetData(typeof(System.String)) as string;

                        using( Editor.Manipulator oBulk = new Editor.Manipulator( FileList ) ) {
                            oBulk.LineAppendNoUndo( strFileName );
                        }

                        ImageLoad( strFileName );
                        Raise_TextLoaded   ();
                        Raise_DirtyDoc     ();

                        _oSiteWorkThumb.Queue(ThumbsCreateEnum(new SKSize(100, 100)), 0);
                        _oSiteWorkParse.Queue(CreateParseWorker(), 0);
                    }
                    if ( oDataObject.GetDataPresent(DataFormats.FileDrop) ) {
                        string[] rgFileDrop = oDataObject.GetData(DataFormats.FileDrop) as string[];

                        using( Editor.Manipulator oBulk = new Editor.Manipulator( FileList ) ) {
                            foreach( string strFile in rgFileDrop ) {
                                oBulk.LineAppendNoUndo( strFile );
                            }
                        }
                        ImageLoad( rgFileDrop[0] );
                        Raise_TextLoaded   ();
                        Raise_DirtyDoc     ();

                        _oSiteWorkThumb.Queue(ThumbsCreateEnum(new SKSize(100, 100)), 0);
                        _oSiteWorkParse.Queue(CreateParseWorker(), 0);
                    }
                }
            }
        }

        protected virtual void Raise_DirtyDoc() {
            _fDirtyDoc = true;
            _oSiteBase.Notify( ShellNotify.DocumentDirty );

            base.Raise_ImageUpdated();
        }

        public override void Raise_ImageUpdated() {
            CurrentShowPath.Empty();
            CurrentShowPath.TryAppend( CurrentDirectory );
            CurrentShowFile.Empty();
            CurrentShowFile.TryAppend( CurrentFileName );

            DecorNavigatorUpdate();

            base.Raise_ImageUpdated();
        }

        protected void Raise_UpdatedThumbs( bool DirtyThumbs = true ) {
            _fDirtyThumbs = DirtyThumbs;
			ThumbsUpdated?.Invoke();
        }

        protected void Raise_TextParsed() {
            TextParsed?.Invoke();
        }

        protected void Raise_TextLoaded() {
            TextLoaded?.Invoke();
        }

        /// <summary>
        /// BUG: Huh, doesn't look like it's getting used anymore. Might
        /// be able to remove this and the GateFilesEvent object.
        /// </summary>
        /// <param name="eEvent"></param>
        void OnFileList_BufferEvent(BUFFEREVENTS eEvent) {
            using( GateFilesEvent oGate = new GateFilesEvent( this ) ) {
                if( oGate.Open ) {
                    if( eEvent == BUFFEREVENTS.MULTILINE ||
                        eEvent == BUFFEREVENTS.SINGLELINE ) 
                    {
                        Raise_ImageUpdated();
                        Raise_UpdatedThumbs();
                        _oSiteWorkParse.Queue( CreateParseWorker(), 0 );
                    }
                }
            }
        }

        protected virtual void DecorNavigatorUpdate() {
            SKColorType skColorType;
            long        lSize       = 0;
            int         iWidth      = 0;
            int         iHeight     = 0;
            string      strName     = string.Empty;
            string      strDepth    = "Unknown";
            DateTime    dtModified  = DateTime.MinValue;
            FileLine    oFileLine   = _oDisplayLine as FileLine;

            if( Bitmap != null ) {
                iHeight     = Bitmap.Height;
                iWidth      = Bitmap.Width;
                skColorType = Bitmap.ColorType;
            }

            try {
                strName     = _oDisplayLine.ToString();
              //strDepth    = _rgPixelDescription[eDepth];
                dtModified  = oFileLine.ModifiedDate;
                lSize       = oFileLine.FileSize;
            } catch( Exception oEx ) {
                Type[] rgErrors = { typeof( NullReferenceException ),
                                    typeof( IndexOutOfRangeException ),
                                    typeof( ArgumentOutOfRangeException ),
                                    typeof( KeyNotFoundException ) };
                if( rgErrors.IsUnhandled( oEx ) )
                    throw;
            }

            using( ImageProperties.Manipulator oBulk = Properties.CreateManipulator() ) {
                oBulk.SetValue( (int)ImageProperties.Names.Width,     iWidth .ToString() );
                oBulk.SetValue( (int)ImageProperties.Names.Height,    iHeight.ToString() );
                oBulk.SetValue( (int)ImageProperties.Names.Depth,     strDepth );
                oBulk.SetValue( (int)ImageProperties.Names.Modified,  dtModified.ToShortDateString() );
                oBulk.SetValue( (int)ImageProperties.Names.Size,      lSize.ToString( "n0" ) + " Bytes" );
                oBulk.SetValue( (int)ImageProperties.Names.Name,      Path.GetFileName( strName ) );
                oBulk.SetValue( (int)ImageProperties.Names.Directory, CurrentShowPath.ToString() );
            }
        }

        protected void Push( Stack<ProdBase<char>> oStack, Production<char> p_oProduction, ProdBase<char> p_oParent )
        {
            try {
                for( int iProdElem = p_oProduction.Count - 1; iProdElem >= 0; --iProdElem ) {
                    ProdBase<char> oMemElem = p_oProduction[iProdElem]; 

                    if( oMemElem == null ) {
                        StringBuilder sbError = new StringBuilder();

                        sbError.AppendLine( "Problem with production..." );
                        sbError.Append( "State: " );
                        sbError.AppendLine( p_oProduction.StateName );
                        sbError.Append( "Production #: " );
                        sbError.AppendLine( p_oProduction.Index.ToString() );
                        sbError.Append( "Element #: " );
                        sbError.Append( iProdElem.ToString() );
                        sbError.AppendLine( "." );

                        throw new InvalidOperationException( sbError.ToString() );
                    }

                    oStack.Push( oMemElem );
                }
            } catch( Exception oEx ) {
                Type[] rgErrors = { typeof( ArgumentNullException ),
                                    typeof( NullReferenceException ),
                                    typeof( InvalidOperationException ) };
				if( rgErrors.IsUnhandled( oEx ) )
					throw;
                LogError( "parse", "trouble pushing productions" );
            }
        }

        protected void OnMatch( Stack<ProdBase<char>> oStack, BaseEditor.LineStream oStream, ProdBase<char> oElem, int iInput, int iMatch ) {
            // Null match is zero length so just ignore.
            if( iMatch > 0 ) {
                Line oLine  = oStream.SeekLine( iInput, out int iOffset);
                int  iColor = 0;

                try {
                    foreach( ProdBase<char> oParent in oStack ) {
						// Note: Seems odd, that this might be false. But I'm seeing it.
						if( oParent is ProdState<char> oState ) {
							int iIndex = oState.Class.Bindings.IndexOfKey( oElem.ID );
                
							if( iIndex > -1 ) {
								iColor = oState.Class.Bindings.Values[iIndex].ColorIndex;
								break;
							}
						}
                    }
                } catch( NullReferenceException ) {
					LogError( "ImageDocWalk", "Problem on match" );
                }
                oLine.Formatting.Add( new WordRange( iOffset, iMatch, iColor ));
            }
        }

		protected void OnError( Stack<ProdBase<char>> oStack, BaseEditor.LineStream oStream, int iInput, ProdBase<char> oElem ) {
            Line oLine = oStream.SeekLine( iInput, out int iOffset);

			StringBuilder oBuilder = new StringBuilder();

			oBuilder.Append( "Grammer error at Line: " );
			oBuilder.Append( oLine.At.ToString() );
			oBuilder.Append( "; Col: " );
			oBuilder.Append( iOffset.ToString() );
			oBuilder.Append( "; Elem: " );
			oBuilder.Append( oElem.ToString() );
			oBuilder.Append( "; Stack: " );

			foreach( ProdBase<char> oState in oStack ) {
				oBuilder.Append( "'");
				oBuilder.Append( oState.ToString() );
				oBuilder.Append( "'");
				oBuilder.Append( ",");
			}

			LogError( "parsing", oBuilder.ToString() );
		}
        
		/// <summary>
		/// 2019: This is kind of weird. I could use the parse iterator instead of
		/// hand coding all this. Try fixing that.
		/// </summary>
		/// <returns></returns>
        [Obsolete]public IEnumerator<int> CreateParseWorker() {
            Stack<ProdBase<char>> oStack        = new Stack<ProdBase<char>>();
            int                   iStreamLength = (int)FileList.CharacterCount( 0 ) + 1;
		    BaseEditor.LineStream oStream       = FileList.CreateStream();
		    int                   iInput        = 0;

            if( _oGrammar == null )
                yield break;

            oStack.Push( new ProdState<char>(_oGrammar.FindState("start")) );

            while( iInput < iStreamLength - 1 ) {
                if( oStack.Count < 1 ) {
                    Raise_TextParsed(); // BUG: This never happened before. Need to investigate.
                    yield break;
                }

                ProdBase<char>   oNonTerm    = null;
		        int              iMatch;
                Production<char> oProduction;

				try {
					oNonTerm = oStack.Pop();
					if( oNonTerm.IsEqual( 30, oStream, false, iInput, out iMatch, out oProduction) ) {
						if( oProduction == null ) {
							OnMatch( oStack, oStream, oNonTerm, iInput, iMatch ); // just match terminals.
							iInput += iMatch;
						} else {
							Push( oStack, oProduction, oNonTerm );
						}
					} else {
						// It's only an error if somewhere inside the stream, not at the end.
						if( iInput < iStreamLength - 1 )
							OnError( oStack, oStream, iInput, oNonTerm );
					}
				} catch ( Exception oEx ) {
					Type[] rgErrors = { typeof( ArgumentNullException ),     // MemoryEnd<T> can throw it on grammar bugs.
										typeof( InvalidOperationException ), // Push had problem with a production
										typeof( NullReferenceException ) };  // Usually this means one of the callbacks had a problem! ^_^
					if( rgErrors.IsUnhandled( oEx )) 
						throw;

					LogError( "DocWalkFile", "Parsing exception at term: " + oNonTerm.ToString() );
					oStack.Clear();
				}
                yield return( 0 );
            }
            
            Raise_TextParsed();
			yield return( 0 );
        }

		public virtual bool LoadAgain( string strDirectory ) {
			return( false );
		}

	    public class SongWorker : IEnumerator<int> {
		    protected readonly IPgBaseSite    _oSiteBase;

				      readonly IPgSound  _oSound;
		    protected          IPgPlayer _oPlayer;
		    protected          IPgReader _oDecoder;
            string                       _strFileName;

		    uint _uiWait = 0;

		    public SongWorker( IPgBaseSite oSiteBase, string strFileName ) {
                _strFileName = strFileName ?? throw new ArgumentNullException();

			    _oSiteBase = oSiteBase ??  throw new ArgumentNullException( "Need a site with hilight." );
			    _oSound    = oSiteBase.Host.Services as IPgSound ?? throw new ArgumentNullException( "Host requires IPgSound." );

                try {
				    _oDecoder = GetReader( _strFileName );
                } catch( Exception oEx ) {
                    Type[] rgErrors = { typeof( FormatException ),
						                typeof( FileNotFoundException ),
									    typeof( InvalidOperationException ),
									    typeof( NullReferenceException ) };
                    if( rgErrors.IsUnhandled( oEx ) )
                        throw;
                    throw new ArgumentException( "Bad format or file not found " );
                }
			    _oPlayer = GetPlayer( _oPlayer, _oDecoder.Spec );

			    if( _oPlayer == null )
				    throw new NullReferenceException();
		    }

		    /// <summary>
		    /// Returns the recommended time in milliseconds to "sleep", or do something else.
		    /// </summary>
		    int IEnumerator<int>.Current {
			    get{ return (int)_uiWait; }
		    }

		    object IEnumerator.Current => throw new NotImplementedException();

		    bool IEnumerator.MoveNext() {
                if( _oPlayer == null )
                    return false;

				try {
					_uiWait = ( _oPlayer.Play( _oDecoder ) >> 1 ) + 1;
					if( _oDecoder.IsReading )
						return true;
					// If decoder is done, move on to the next song!
				} catch( Exception oEx ) {
					Type[] rgErrors = { typeof( NullReferenceException ),
										typeof( ArgumentNullException ),
										typeof( MMSystemException ) };
					if( rgErrors.IsUnhandled( oEx ) )
						throw;

					_oSiteBase.LogError( "player", "Problem with current song: " + _strFileName );
				}

                if( _oSiteBase.Host is ImageWalkerDoc oDoc ) {
                    oDoc.NotifyMediaStatusChanged();
                }

			    return( false );
		    }

		    void IEnumerator.Reset() {
			    throw new NotImplementedException();
		    }

		    public IPgPlayer GetPlayer( IPgPlayer oPlayer, Specification oSpec ) {
			    try {
				    if (oPlayer == null)
					    oPlayer = _oSound.CreateSoundPlayer( oSpec );
				    else {
					    if (!oPlayer.Spec.CompareTo(oSpec)) {
						    oPlayer.Dispose();
						    oPlayer = _oSound.CreateSoundPlayer( oSpec );
					    }
				    }
				    return( oPlayer );
			    } catch( Exception oEx ) {
				    Type[] rgErrors = { typeof( ArgumentException ),
									    typeof( ArgumentNullException ),
									    typeof( BadDeviceIdException ),
									    typeof( InvalidHandleException ),
									    typeof( MMSystemException ) };
				    if( rgErrors.IsUnhandled( oEx ) )
					    throw;

				    _oSiteBase.LogError( "sound", "Couldn't handle sound spec" );

				    // TODO: In the future I'll make a fake player just so the system can move on.
			    }
			    return( null );
		    }

		    protected IPgReader GetReader( string strFileName ) {
				return( _oSound.CreateSoundDecoder( strFileName ) );
		    }

		    #region IDisposable Support
		    private bool _iRedundantDispose = false; // To detect redundant calls

		    /// <summary>
		    /// So this is all fine and good unless the managed dispose DOESN'T get called.
		    /// Because you can't call any methods on other objects in the finalizer since they
		    /// might be dead already! Since our player is a managed object wrapping an unmanaged
		    /// object, it creates a connundrum for us. ^_^;;
		    /// </summary>
		    /// <param name="fManagedDispose">true if NOT being called from the finalizer.</param>
		    protected virtual void Dispose(bool fManagedDispose) {
			    if( _iRedundantDispose )
				    return;

			    if( fManagedDispose ) {
				    if( _oPlayer != null )
					    _oPlayer.Dispose();
				    if( _oDecoder != null )
					    _oDecoder.Dispose();
			    }

			    // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
			    // TODO: set large fields to null.
			    _iRedundantDispose = true;
		    }

		    // TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
		    // ~SongWorker() {
		    //   // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
		    //   Dispose(false);
		    // }

		    // This code added to correctly implement the disposable pattern.
		    void IDisposable.Dispose() {
			    // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
			    Dispose(true);
			    // TODO: uncomment the following line if the finalizer is overridden above.
			    // GC.SuppressFinalize(this);
		    }
		    #endregion
	    }
		public void PlayStart() {
			switch( _oWorkPlace.Status ) {
				case WorkerStatus.FREE:
					try {
                        string strFileName = Path.Combine( CurrentDirectory, 
                                                           Path.GetFileNameWithoutExtension( CurrentFileName ) ) + ".mp3";

						_oWorkPlace.Queue( new SongWorker( new ImageWalkerDocSlot( this ), strFileName ), 0 );
					} catch( Exception oEx ) {
						Type[] _rgErrors = { typeof( ArgumentException ),
										     typeof( ArgumentNullException ),
											 typeof( InvalidOperationException ) };
						if( _rgErrors.IsUnhandled( oEx ) )
							throw;

						_oSiteBase.LogError( "player", "Unable to play current request." );
						//Albums.HighLight = null;
					}
					break;
				case WorkerStatus.PAUSED:
					_oWorkPlace.Start( 0 );
					break;
				case WorkerStatus.BUSY:
					_oSiteBase.LogError( "player", "Sound is already playing" );
					break;
			}
		}

        public void PlayPause() {
            _oWorkPlace.Pause();
        }

        public void PlayStop() {
            _oWorkPlace.Stop();
        }

        public void NotifyMediaStatusChanged() {
            MediaEvent?.Invoke( ShellNotify.MediaStatusChanged );
        }

		public WorkerStatus PlayStatus => _oWorkPlace.Status;

        public override bool Execute( Guid sGuid ) {
            if( sGuid == GlobalCommands.StepLeft ) {
                Next( -1 );
                return( true );
            }
            if( sGuid == GlobalCommands.StepRight ) {
                Next( +1 );
                return( true );
            }
            if( sGuid == GlobalCommands.JumpParent ) {
                DirectoryNext( 0 );
                return( true );
            }
            if( sGuid == GlobalCommands.JumpPrev ) {
                DirectoryNext( -1 );
                return( true );
            }
            if( sGuid == GlobalCommands.JumpNext ) {
                DirectoryNext( +1 );
                return( true );
            }
            if( sGuid == GlobalCommands.Paste ) {
                ClipboardCopyFrom();
                return( true );
            }
            if( sGuid == GlobalCommands.Copy ) {
                ClipboardCopyTo();
                return( true );
            }
            if( sGuid == GlobalCommands.Delete ) {
                CurrentFileDelete();
                return( true );
            }
            if( sGuid == GlobalCommands.Play ) {
                PlayStart();
                return( true );
            }

            return( base.Execute( sGuid ) );
        }
    } // end class

}
