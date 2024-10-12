using System.Data.Common;
using System.IO;

using Play.Drawing;
using Play.Interfaces.Embedding;
using Play.Edit;
using Play.Rectangles;
using SkiaSharp;
using System.Reflection;

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

        public class FMRow : Row {
            public enum Col :int {
                Type = 0,
                Name,
                Date,
                Size
            }

            static int ColumnCount = Enum.GetValues(typeof(Col)).Length;
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

                CreateColumn( Col.Type, strExt );
                CreateColumn( Col.Name, oFile.Name );
				CreateColumn( Col.Date, oFile.LastWriteTime.ToShortDateString() );
                CreateColumn( Col.Size, oFile.Length.ToString() );

                CheckForNulls();
            }

            public FMRow( DirectoryInfo oDir ) {
                _rgColumns = new Line[ColumnCount];


                CreateColumn( Col.Name, oDir.Name );
				CreateColumn( Col.Date, oDir.LastWriteTime.ToShortDateString() );
                CreateColumn( Col.Type, "\xe188" );
                CreateColumn( Col.Size, "--" );

                CheckForNulls();

                IsDirectory = true;
            }

            void CreateColumn( Col eCol, string strValue ) {
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

        public void ReadDir( string strFilePath ) {
            Clear();

			DirectoryInfo oDirectory;

            try {
                if( Path.HasExtension( strFilePath ) )
                    _strDirectory = Path.GetDirectoryName( strFilePath );
                else
                    _strDirectory = strFilePath;

                if( string.IsNullOrEmpty( _strDirectory ) ) {
                    LogError( "Problem locating desired directory." );
                    return;
                }

                oDirectory = new DirectoryInfo( _strDirectory );

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

                DoParse();
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
                                    typeof( PlatformNotSupportedException )
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
