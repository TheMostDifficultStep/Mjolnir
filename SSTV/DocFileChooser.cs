using System;
using System.Collections.Generic;
using System.IO;

using SkiaSharp;

using Play.Interfaces.Embedding;
using Play.Edit;
using Play.ImageViewer;
using Play.Parse;

namespace Play.SSTV {
    public delegate void DirectoryUpdated( string strDirectory );

    public class FileChooser : 
        IPgParent,
        IPgCommandBase,
        IDisposable,
        IPgLoadURL,
        IPgSaveURL
    {
        readonly IPgBaseSite _oSiteBase;

        readonly static string _strIcon = @"ImageViewer.Content.icon-folder.png";

        public bool       IsDirty   => false;
        public SKBitmap   Icon      => null;
        public IPgParent  Parentage => _oSiteBase.Host;
        public IPgParent  Services  => Parentage.Services;
        public string     Banner    => "Hello!";

        protected readonly IPgStandardUI2 _oStdUI;

        protected DirectoryInfo _oDirectory;
        public    FileEditor    FileList { get; }

        public event DirectoryUpdated DirectoryChange;

        public class DirectoryHyperLink : IPgWordRange {
            readonly int _iLength;
            readonly int _iClrIndex;

            public DirectoryHyperLink( int iLen, int iClrIndex ) {
                _iLength   = iLen;
                _iClrIndex = iClrIndex;
            }

            public bool IsWord => true;
            public bool IsTerm => true;

            public string StateName  => "chooser";
            public int    ColorIndex { get => _iClrIndex; set => throw new NotImplementedException(); }

            public int Offset { get => 1;        set => throw new NotImplementedException(); }
            public int Length { get => _iLength; set => throw new NotImplementedException(); }
        }

		public class FileWalkerDocSlot : 
			IPgBaseSite
		{
			readonly FileChooser _oDoc;

			public FileWalkerDocSlot( FileChooser oDoc ) {
				_oDoc = oDoc ?? throw new ArgumentNullException( "Image document must not be null." );
			}

			public void LogError( string strMessage, string strDetails, bool fShow=true ) {
				_oDoc.LogError( strMessage, "ImageWalker : " + strDetails );
			}

			public void Notify( ShellNotify eEvent ) {
			}

			public IPgParent Host => _oDoc;
		}

        public FileChooser( IPgBaseSite oSiteBase ) {
            _oSiteBase = oSiteBase ?? throw new ArgumentNullException( "Document needs a site." );
 			_oStdUI    = oSiteBase.Host.Services as IPgStandardUI2 ?? throw new ArgumentException( "Parent view must provide IPgStandardUI service" );

            FileList = new FileEditor( new FileWalkerDocSlot( this) );
        }

        public void Dispose() {
        }

        public void LogError( string strCategory, string strDetails ) {
            _oSiteBase.LogError( strCategory, strDetails );
        }

        public void Raise_TextLoaded() {
            DirectoryChange?.Invoke( CurrentDirectory );
        }

        public string CurrentDirectory {
            get { 
				try {
					return( _oDirectory.FullName );
				} catch( Exception oEx ) {
					Type[] rgError = { typeof( NullReferenceException ),
									   typeof( PathTooLongException ),
									   typeof( System.Security.SecurityException ) };
					if( rgError.IsUnhandled( oEx ) )
						throw;

					return( string.Empty );
				}
            }
        }

        public string CurrentURL { get { return CurrentDirectory; } }

        public string CurrentFullPath {
			get {
				try {
                    Line oSelected = FileList.CheckedLine;

                    if( oSelected == null )
                        oSelected = FileList[0];

					return( Path.Combine( CurrentDirectory, oSelected.ToString() ) );
				} catch( Exception oEx ) {
					Type[] rgErrors = { typeof( ArgumentException ),
										typeof( NullReferenceException ) };
					if( rgErrors.IsUnhandled( oEx ) )
						throw;

					return( string.Empty );
				}
			}
        }

        protected string FullPathFromLine( Line oLine ) {
			try {
				return( Path.Combine( CurrentDirectory, oLine.ToString() ) );
			} catch( ArgumentException ) {
				return( string.Empty );
			}
        }

        /// <summary>
        /// I want to load .jpg and .png but it clutters of the view and what
        /// I really want is to know if there is a picture the same as a wave
        /// file hanging about in the target directory. Going to need some updates
        /// to the Chooser editor to make that work.
        /// </summary>
        /// <param name="strExtn"></param>
        /// <returns></returns>
        protected bool IsFileExtensionUnderstood( string strExtn ) {
            return string.Equals( strExtn, ".wav" );
        }

        /// <summary>
        /// Load the filenames in the directory pointed to by the filepath.
        /// </summary>
        protected bool DirectoryRead( DirectoryInfo oDir ) {
            try {
                using( Editor.Manipulator oManip = FileList.CreateManipulator() ) {
                    // Need the gate AFTER the manipulator since it'll block the call back OnBufferChange()
                    // that gets called when the dispose on the manipulator get's called.
					FileLine oLine = oManip.LineAppendNoUndo( "[..]" ) as FileLine;
					oLine._fIsDirectory = true;
                    oLine.Formatting.Add( new DirectoryHyperLink( 2, 1 ) );

                    // 12/23/2015 : I could potentially put fileinfo's directly into my document. But 
					// We're more 'document' compatible if we use string paths.
                    List<FileInfo> rgFiles = new List<FileInfo>();
                    foreach( FileInfo oFile in oDir.GetFiles( "*.*", SearchOption.TopDirectoryOnly ) ) {
						if( !oFile.Attributes.HasFlag( FileAttributes.Hidden)) {
							rgFiles.Add( oFile );
						}
                    }

                    // Insert the directories first so they are at the top. Sort with NaturalCompare
                    DirectoryInfo[] rgDir = oDir.GetDirectories( "*.*" );
                    List<DirectoryInfo> rgDirList = new List<DirectoryInfo>( rgDir );
                    rgDirList.Sort((x,y) => ( NaturalCompare( x.Name, y.Name ) ) );

                    foreach( DirectoryInfo oDirChild in rgDirList ) {
						if( !oDirChild.Attributes.HasFlag( FileAttributes.Hidden)) {
							oLine = oManip.LineAppendNoUndo( "[" + oDirChild.Name + "]" ) as FileLine; // Don't load the path!!!

							oLine._dtModifiedDate = oDirChild.LastWriteTime;
							oLine._fIsDirectory   = true;
                            oLine.Formatting.Add( new DirectoryHyperLink( oDirChild.Name.Length, 1 ) );
						}
                    }

                    // Sort so newest files are at the top. Hence the "negative sign"
                    // TODO : should be able to sort any way at any time!!! very cool.
                    rgFiles.Sort( (x,y) => - ( x.CreationTime.CompareTo( y.CreationTime ) ) );

                    foreach( FileInfo oFile in rgFiles ) {
						// Want to override what we load in the dialog box version of this control
						// even tho' the thumbnails list will only load the understood file extensions.
                        if( IsFileExtensionUnderstood( oFile.Extension ) ) {
                            oLine = oManip.LineAppendNoUndo( oFile.Name ) as FileLine; // Don't load the path!!!

                            oLine._dtModifiedDate = oFile.LastWriteTime;
                        }
                    }

					return true;
                }
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

				LogError( "Image Walker", "Couldn't use the directory given." ); 
                FileList.Clear();
                using( Editor.Manipulator oManip = FileList.CreateManipulator() ) {
					FileLine oLineNew = oManip.LineAppendNoUndo( "[..]" ) as FileLine;
					oLineNew._fIsDirectory = true;
                    oLineNew.Formatting.Add( new DirectoryHyperLink( 2, 1 ) );
				}

				return false;
            }
        }

        /// <summary>
        /// This code is pretty much a de-factoring of the enumerators, taking advantage of the
        /// fact that I know I have array indexible elements instead of needing the enumerators.
        /// Don't get me wrong, I like enumerators as much as the next guy. But that seems like
        /// serious overkill for this problem. 
        /// http://www.interact-sw.co.uk/iangblog/2007/12/13/natural-sorting
        /// Ian Griffiths
        /// Thursday 13 December, 2007
        /// http://opensource.org/licenses/mit-license.php
        /// </summary>
        static public int NaturalCompare( string x, string y)
        {
            int iLen  = Math.Min( x.Length, y.Length );
            int iComp = x.Length - y.Length;

            if( iLen == 0 )
                return( iComp );

            int iResult = 0;
            int i       = 0;

            // Try exact match the most characters in both strings...
            while( true ) {
                iResult = char.ToLower( x[i] ) - char.ToLower( y[i] );

                if( i++ >= iLen-1 )
                    break;

                if (iResult != 0)
                    return( iResult );
            }
            // If we make it here. Then we made it to the last char
            // of the stortest string. Or both strings are same length.

            // If the last character in the shortest string is the
            // same as it's opposite. Then the longest string wins!
            if( iComp == 0 )
                return( iResult );

            // Else the last char in shortest string determines sort.
            return( iComp );
        }

        /// <summary>
        /// Only the directory viewer is actually capable of reloading our contents on demand.
        /// </summary>
        /// <remarks>
        /// Can't tell the difference between path's with ext and file with ext.
        /// c:/foo/bar.buzz
        /// c:/foo/bar/name.txt
        /// </remarks>
        public bool LoadAgain( string strFilePath ) {
            if( string.IsNullOrEmpty( strFilePath ) ) {
                LogError( "internal", "Image walker initialization parameter must be a filename." );
                return false;
			}

            FileList.Clear( fSendEvents:false ); 

			DirectoryInfo oDirectory;

            try {
				string strDirectory = string.Empty;

                if( Path.HasExtension( strFilePath ) )
                    strDirectory = Path.GetDirectoryName( strFilePath );
                else
                    strDirectory = strFilePath;

                oDirectory = new DirectoryInfo( strDirectory );
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

                LogError( "alert", "Couldn't use the directory given." ); 
                _oDirectory = null;

                return false;
            }

            if( !DirectoryRead( oDirectory ) ) {
				return false;
			}

			bool fNullOldDir = _oDirectory == null; // old dir is null first time around.
				
			if( !fNullOldDir )
				fNullOldDir = string.Compare( _oDirectory.FullName, oDirectory.FullName ) != 0;

			_oDirectory = oDirectory;
            
            Raise_TextLoaded();

            return true;
        }

        /// <summary>
        /// It should be possible to initialize w/o an directory. But at the moment it's not coded.
        /// </summary>
        /// <returns></returns>
        public bool InitNew() {
			return( true );
        }

        /// <summary>
        /// We're designed to load a directory. We do weird stuff if we load from string!!!
        /// </summary>
        public bool Load( TextReader oReader ) {
            return false;
        }

        /// <summary>
        /// Load the names of the children directories of parent of the current
        /// directory given. Load all the files of the current directory in the
        /// Reset() call. Call this or InitNew() once only.
        /// </summary>
        /// <param name="strFilePath"></param>
        /// <seealso cref="LoadAgain" />
        public bool LoadURL( string strFilePath ) {
            if( !InitNew() )
                return false;

            try {
                if( !LoadAgain( strFilePath ) )
                    return false;
            } catch( ApplicationException ) {
                return false;
            }

            return true;
        }

        public bool Save() {
            return true;
        }

        public void ClipboardCopyFrom() {
            // We can't deal with cut and paste where we actually move the file so we'll just ignore for now.
        }

        public bool Execute( Guid sGuid ) {
			if( sGuid == GlobalCommands.JumpParent ) {
				return LoadAgain( Path.Combine( CurrentDirectory, ".." ) );
			}
            return false;
        }
    }
}
