using System.Reflection;

using SkiaSharp;

using Play.Drawing;
using Play.Interfaces.Embedding;
using Play.Edit;
using Play.Forms;

namespace Play.FileManager {
    public class FileProperties :
        DocProperties 
    {
        public class BulkLoader2 :
            IDisposable
        {
            TrackerEnumerable _sTrack;
            FileProperties    _rgProp;

            public BulkLoader2( FileProperties oDocProps ) {
                _rgProp = oDocProps ?? throw new ArgumentNullException();
                _sTrack = new TrackerEnumerable( _rgProp );
            }

            public void Dispose() {
                _sTrack.FinishUp( IPgEditEvents.EditType.Rows, null );
                _rgProp.DoParse ();
            }

            public Line this[ FileProperties.Names eIndex ] {
                get {
                    return _rgProp[(int)eIndex][1];
                }
            }

            public void ValueUpdate( FileProperties.Names eIndex, Line oValue ) {
                 this[eIndex].TryReplace( oValue.AsSpan );
            }
            public void ValueUpdate( FileProperties.Names eIndex, string strValue ) {
                 this[eIndex].TryReplace( strValue );
            }
        }
        public enum Names : int {
            Time,
			Date,
            Size,
            Type,
            //TmpTop,
            //TmpBottom,
            //TmpRcTop,
            //TmpRcBottom,
        }

        public FileProperties(IPgBaseSite oSite) : base(oSite) {
            // Set up our basic list of values.
            foreach( Names eName in Enum.GetValues(typeof(Names)) ) {
                CreatePropertyPair( eName.ToString() );
            }
        }
    }
    /// <summary>
    /// This object will be the list of shortcut pinned directories
    /// the shell will save it somewhere by default.
    /// </summary>
    public class FileFavorites :
        EditMultiColumn,
        IPgLoad<TextReader>,
        IPgSave<TextWriter>
    {
        public class DRow : Row {
            public enum Col {
                ShortcutName = 0,
                FilePath,
                Type
            }
            static int ColumnCount = Enum.GetValues(typeof(Col)).Length;
            public Line this[Col eIndex] {
                get { return _rgColumns[(int)eIndex]; }
                set { _rgColumns[(int)eIndex] = value; }
            }
            public DRow( string strType, string strShortCut ) {
                _rgColumns = new Line[ColumnCount];

                this[Col.ShortcutName] = new TextLine( 0, strShortCut );
                this[Col.Type        ] = new TextLine( 0, strType );
                this[Col.FilePath    ] = new TextLine( 0, string.Empty );
            }

            public bool IsDirectory { get; set; } = false;
        }
        public FileFavorites(IPgBaseSite oSiteBase) : base(oSiteBase) {
        }

        public bool InitNew() {
            _rgRows.Add( new DRow( "\ue2af", "Images" ) );
            _rgRows.Add( new DRow( "\ue105", "Server Docs" ) );
            _rgRows.Add( new DRow( "\ue114", "Kittehs" ) );

            RenumberAndSumate();

            return true;
        }

        public bool Load(TextReader oStream) {
            throw new NotImplementedException();
        }

        public bool Save(TextWriter oStream) {
            throw new NotImplementedException();
        }

    }

    /// <summary>
    /// A little class to stand in for a MemoryElem at times. 
    /// </summary>
    public class DirRange : ColorRange {
        public DirRange( int iOffset, int iLength, int iColor ) : 
            base( iOffset, iLength, iColor ) 
        {
        }

        public override bool   IsWord    => true;
        public override string StateName => "DirJump";
    }

    public class FileRange : ColorRange {
        public FileRange( int iOffset, int iLength, int iColor ) : 
            base( iOffset, iLength, iColor ) 
        {
        }

        public override bool   IsWord    => true;
        public override string StateName => "FileJump";
    }

    /// <summary>
    /// This object will load a directory for display. Interesting, it seems there's
    /// no need to save anything. The shell will need to remember the URL from
    /// LoadURL and it's done...
    /// </summary>
    public class FileManager :
        EditMultiColumn,
        IPgLoadURL,
        IPgSaveURL
    {
        public string HomeURL => Environment.GetFolderPath(Environment.SpecialFolder.Personal);

        public readonly IPgStandardUI2 _oStdUI;
        //public event Action? Event_UpdateBanner;
        public class FMRow : Row {
            public enum DCol :int {
                Chck =0,
                Type,
                Name,
                Time,
                Date,
                Size,
            }

            static int ColumnCount = Enum.GetValues(typeof(DCol)).Length;
            public Line this[DCol eValue] => this[(int)eValue];

            public FMRow( FileInfo oFile ) {
                _rgColumns = new Line[ColumnCount];

                // TODO: Really cool to map to the assembly I'm likely to use to load!!
                //       used the config file to get the icons.
                string strExt = oFile.Extension;

                if( strExt.CompareTo( ".txt" ) == 0 ) {
                    strExt = "\xe160";
                }
                if( strExt.CompareTo( ".scraps" ) == 0 ) {
                    strExt = "\xe14d";
                }
                if( strExt.CompareTo( ".png" ) == 0 || 
                    string.Compare( strExt, ".jpg", ignoreCase:true ) == 0 ||
                    strExt.CompareTo( ".jpeg" ) == 0 ||
                    strExt.CompareTo( ".gif" ) == 0 ||
                    strExt.CompareTo( ".webp" ) == 0 ) {
                    strExt = "\xe156"; // e2af person
                }
                if( strExt.CompareTo( ".htm"  ) == 0 || 
                    strExt.CompareTo( ".html" ) == 0 ||
                    strExt.CompareTo( ".css"  ) == 0 ) {
                    strExt = "\xe12b";
                }
                if( strExt.CompareTo( ".zip" ) == 0 || 
                    strExt.CompareTo( ".pdf" ) == 0 ) {
                    strExt = "\xe295";
                }
                if( strExt.CompareTo( ".stdlog" ) == 0 || 
                    strExt.CompareTo( ".netlog" ) == 0 ) {
                    strExt = "\xe1d3";
                }
                if( strExt.CompareTo( ".weather" ) == 0 ) {
                    strExt = "\xe286";
                }

                if( strExt.CompareTo( ".mp3" ) == 0 || 
                    strExt.CompareTo( ".m3u" ) == 0 ||
                    strExt.CompareTo( ".music" ) == 0 ||
                    strExt.CompareTo( ".wav" ) == 0 ) {
                    strExt = "\xe189";
                }

                if( string.Compare( strExt, oFile.Extension, ignoreCase:true ) == 0 ) {
                    strExt = "\xe11b"; // question mark.
                }

                CreateColumn( DCol.Chck, string.Empty );
                CreateColumn( DCol.Type, strExt );
                CreateColumn( DCol.Name, oFile.Name );
                CreateColumn( DCol.Time, oFile.LastWriteTime.ToShortTimeString() );
				CreateColumn( DCol.Date, oFile.LastWriteTime.ToShortDateString() );
                CreateColumn( DCol.Size, oFile.Length.ToString("n0") );

                CheckForNulls(); 

                this[DCol.Type].Formatting.Add( new FileRange( 0, 10, 5 ) );

            }

            public FMRow( DirectoryInfo oDir ) {
                _rgColumns = new Line[ColumnCount];

                CreateColumn( DCol.Chck, string.Empty );
                CreateColumn( DCol.Type, "\xe188" );
                CreateColumn( DCol.Name, oDir.Name );
                CreateColumn( DCol.Time, oDir.LastWriteTime.ToShortTimeString() );
				CreateColumn( DCol.Date, oDir.LastWriteTime.ToShortDateString() );
                CreateColumn( DCol.Size, "--" );

                CheckForNulls();

              //We parse the files names now. So don't need name formatting.
              //this[DCol.Name].Formatting.Add( new ColorRange( 0, 256,  1 ) );
                this[DCol.Type].Formatting.Add( new DirRange  ( 0,  10, 11 ) );

                IsDirectory = true;
            }

            void CreateColumn( DCol eCol, string strValue ) {
				_rgColumns[(int)eCol] = new TextLine( (int)eCol, strValue );
            }

            public bool IsDirectory { get; } = false;
        }
        // So the ? suppositely tells the compiler that it is OK
        // for this value to be null, thus we need to check it in
        // all cases. As opposed to not using that and the compiler
        // will simply let you attempt to dereference the null value.
	    protected string? _strDirectory;


        // Move these to the main program when we get this working...
        public FileFavorites  DocFavs { get; protected set; }

		protected class DocSlot :
			IPgBaseSite
		{
			protected readonly FileManager _oHost;

			public DocSlot( FileManager oHost ) {
				_oHost = oHost ?? throw new ArgumentNullException( "Host" );
			}

			public IPgParent Host => _oHost;

            public void LogError(string strMessage, string strDetails, bool fShow=true) {
				_oHost.LogError( strDetails );
			}

			public void Notify( ShellNotify eEvent ) {
			}
		}
        public FileManager(IPgBaseSite oSiteBase) : base(oSiteBase) {
            _oStdUI = (IPgStandardUI2)Services;

            DocFavs  = new( new DocSlot( this ) );
        }

		public SKImage GetResource( string strName ) {
			Assembly oAsm   = Assembly.GetExecutingAssembly();
            string   strRes = "Play.FileManager.Content." + strName;

			return SKImageResourceHelper.GetImageResource( oAsm, strRes );
		}

        /// <summary>
        /// A little of a misnomer as we are just returning the dir and
        /// not an attendent file name with it.
        /// </summary>
        public string? CurrentURL => _strDirectory;

        /// <summary>
        /// This will go to the main program eventually.
        /// </summary>
        /// <returns></returns>
        public bool Initialize() {
            if( !DocFavs.InitNew() )
                return false;

            return true;
        }

        public bool LoadURL( string strURL ) {
            if( !Initialize() )
                return false;

            ReadDir( strURL );

//          DoParse();

            return true;
        }

        public bool InitNew() {
            if( !Initialize() )
                return false;

            ReadDir( HomeURL );

//          DoParse();

            return true;
        }

        public void JumpToParentDir() {
            if( string.IsNullOrEmpty( CurrentURL ) ) {
                LogError( "Arrghg. Directory confusion" );
                return;
            }

            string strPath = Path.Combine( CurrentURL, ".." );

            ReadDir( strPath );
        }

        public void ReadDir( string? strFilePath ) {
			DirectoryInfo oDirectory;

            try {
                if( Path.HasExtension( strFilePath ) )
                    strFilePath = Path.GetDirectoryName( strFilePath );

                if( string.IsNullOrEmpty( strFilePath ) ) {
                    LogError( "Problem locating desired directory." );
                    return;
                }

                oDirectory = new DirectoryInfo( strFilePath );

                ReadDir( oDirectory );
            } catch( Exception oEx ) {
                if( IsUnhandled( oEx ) )
                    throw new ApplicationException( "Unrecognized Directory Read Error", oEx );

                _oSiteBase.LogError( "alert", "Couldn't use the directory given." ); 
            }
        }

        protected static bool IsUnhandled( Exception oEx ) {
            Type[] rgErrors = { typeof( NullReferenceException ),
                                typeof( IOException ),
                                typeof( UnauthorizedAccessException ),
                                typeof( ArgumentException ),
                                typeof( ArgumentNullException ),
                                typeof( PathTooLongException ),
                                typeof( DirectoryNotFoundException ),
                                typeof( System.Security.SecurityException ),
                                typeof( ArgumentOutOfRangeException ),
                                typeof( PlatformNotSupportedException ),
                                typeof( InvalidDataException ),
                                typeof( KeyNotFoundException )
                              };
			return rgErrors.IsUnhandled( oEx );
        }

        protected void ReadDir( DirectoryInfo oDir ) {
            try {
                Clear();

                _strDirectory = oDir.FullName;

                // BUG: This can be ultra laggy. Would be really cool to put this on a 
                // bg thread. Start up will be quicker.
                List<FileInfo> rgFiles = new List<FileInfo>();
                foreach( FileInfo oFile in oDir.GetFiles( "*.*", SearchOption.TopDirectoryOnly ) ) {
					if( !oFile.Attributes.HasFlag( FileAttributes.Hidden)) {
						rgFiles.Add( oFile );
					}
                }

                // Insert the directories first so they are at the top. Sort with NaturalCompare
                DirectoryInfo[]     rgDir     = oDir.GetDirectories( "*.*" );
                List<DirectoryInfo> rgDirList = new List<DirectoryInfo>( rgDir );
                rgDirList.Sort((x,y) => FindStuff<string>.NaturalCompare( x.Name, y.Name ) );

                foreach( DirectoryInfo oDirChild in rgDirList ) {
					if( !oDirChild.Attributes.HasFlag( FileAttributes.Hidden)) {
                        FMRow oRow = new FMRow( oDirChild );
                        _rgRows.Add( oRow );
					}
                }

                // Sort so newest files are at the top. Hence the "negative sign"
                // TODO: I want to create a sort that groups files with similar names
                //       together with the newest file first and the rest following
                //       so I can easily see the versions.
                rgFiles.Sort( (x,y) => - ( x.LastWriteTime.CompareTo( y.LastWriteTime ) ) );

                foreach( FileInfo oFile in rgFiles ) {
					// Want to override what we load in the dialog box version of this control
					// even tho' the thumbnails list will only load the understood file extensions.
                    FMRow oRow = new FMRow( oFile );
                    _rgRows.Add( oRow );
                }

                RenumberAndSumate();

                DoParse();
                Raise_DocLoaded();
            } catch( Exception oEx ) {
				if( IsUnhandled( oEx ) )
					throw;

				LogError( "Couldn't use the directory given." ); 
                Clear();
            }
        }

        public bool Save() {
            return true;
        }

        /// <summary>
        /// Schedule a reparse since we don't want to be parsing and updating
        /// right in the middle of typing EVERY character.
        /// </summary>
        /// <remarks>We'll have to keep this here, but we can move the rest.</remarks>
        public override void DoParse() {
            try {
                ParseColumn( 2, "filename" );
            } catch( Exception oEx ) {
				if( IsUnhandled( oEx ) )
					throw;

				LogError( "Couldn't use the directory given." ); 
            }
        }
    }
}
