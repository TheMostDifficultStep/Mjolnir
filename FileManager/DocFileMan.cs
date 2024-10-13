using System.IO;
using System.Reflection;

using SkiaSharp;

using Play.Drawing;
using Play.Interfaces.Embedding;
using Play.Edit;
using Play.Rectangles;
using Play.Parse;

namespace Play.FileManager {
    /// <summary>
    /// This object will be the list of shortcut pinned directories
    /// the shell will save it somewhere by default.
    /// </summary>
    public class FilePins :
        EditMultiColumn,
        IPgLoad<TextReader>,
        IPgSave<TextWriter>
    {
        public class FPRow : Row {
            public enum Col {
                ShortcutName = 0,
                FileName,
                Type
            }
            static int ColumnCount = Enum.GetValues(typeof(Col)).Length;
            public FPRow() {
                _rgColumns = new Line[3];

                for ( int i = 0; i < ColumnCount; i++ ) {
                    _rgColumns[i] = new TextLine( 0, string.Empty );
                }
            }

            public bool IsDirectory { get; set; } = false;
        }
        public FilePins(IPgBaseSite oSiteBase) : base(oSiteBase) {
        }

        public bool InitNew() {
            throw new NotImplementedException();
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
    public class WordRange2 : ColorRange {
        public WordRange2( int iOffset, int iLength, int iColor ) : 
            base( iOffset, iLength, iColor ) 
        {
        }

        public override bool   IsWord    => true;
        public override string StateName => "DirJump";
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
        public class FMRow : Row {
            public enum DCol :int {
                Chck =0,
                Type,
                Name,
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
                    strExt.CompareTo( ".webp" ) == 0 ) {
                    strExt = "\xe156"; // e2af person
                }
                if( strExt.CompareTo( ".htm" ) == 0 || 
                    strExt.CompareTo( ".html" ) == 0 ) {
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
				CreateColumn( DCol.Date, oFile.LastWriteTime.ToShortDateString() );
                CreateColumn( DCol.Size, oFile.Length.ToString() );

                CheckForNulls();

                this[DCol.Type].Formatting.Add( new ColorRange( 0,  10, 5 ) );
            }

            public FMRow( DirectoryInfo oDir ) {
                _rgColumns = new Line[ColumnCount];

                CreateColumn( DCol.Chck, string.Empty );
                CreateColumn( DCol.Name, oDir.Name );
				CreateColumn( DCol.Date, oDir.LastWriteTime.ToShortDateString() );
                CreateColumn( DCol.Type, "\xe188" );
                CreateColumn( DCol.Size, "--" );

                CheckForNulls();

                this[DCol.Name].Formatting.Add( new ColorRange( 0, 256,  1 ) );
              //this[DCol.Type].Formatting.Add( new ColorRange( 0,  10, 11 ) );
                this[DCol.Type].Formatting.Add( new WordRange2( 0,  10, 11 ) );

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

        public FileManager(IPgBaseSite oSiteBase) : base(oSiteBase) {
            _oStdUI = (IPgStandardUI2)Services;
        }

		public SKBitmap GetResource( string strName ) {
			Assembly oAsm   = Assembly.GetExecutingAssembly();
            string   strRes = "Play.FileManager.Content." + strName;

			return SKImageResourceHelper.GetImageResource( oAsm, strRes );
		}

        public string CurrentURL => _strDirectory;

        public bool LoadURL( string strURL ) {
            ReadDir( strURL );

            DoParse();

            return true;
        }

        public bool InitNew() {
            ReadDir( HomeURL );

            DoParse();

            return true;
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
                Type[] rgErrors = { typeof( IOException ),
                                    typeof( UnauthorizedAccessException ),
                                    typeof( ArgumentException ),
                                    typeof( ArgumentNullException ),
                                    typeof( PathTooLongException ),
                                    typeof( DirectoryNotFoundException ),
									typeof( System.Security.SecurityException ),
                                    typeof( ApplicationException ) };
                if( rgErrors.IsUnhandled( oEx ) )
                    throw new ApplicationException( "Unrecognized Directory Read Error", oEx );

                _oSiteBase.LogError( "alert", "Couldn't use the directory given." ); 
            }
        }

        protected void ReadDir( DirectoryInfo oDir ) {
            try {
                Clear();

                _strDirectory = oDir.FullName;

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
                rgFiles.Sort( (x,y) => - ( x.CreationTime.CompareTo( y.CreationTime ) ) );

                foreach( FileInfo oFile in rgFiles ) {
					// Want to override what we load in the dialog box version of this control
					// even tho' the thumbnails list will only load the understood file extensions.
                    FMRow oRow = new FMRow( oFile );
                    _rgRows.Add( oRow );
                }

                RenumberAndSumate();

                Raise_DocFormatted();
            } catch( Exception oEx ) {
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
                                    typeof( InvalidDataException )
                                  };
				if( rgErrors.IsUnhandled( oEx ) )
					throw;

				LogError( "Couldn't use the directory given." ); 
                Clear();
            }
        }

        public bool Save() {
            return true;
        }
    }
}
