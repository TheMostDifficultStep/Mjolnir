using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;

using SkiaSharp;

using Play.Interfaces.Embedding;
using Play.Edit;

namespace Play.ImageViewer {
    public class ImageWalkerDir : 
        ImageWalkerDoc,
        IPgLoadURL,
        IPgSaveURL
    {
        DirectoryInfo                _oDirectory;
        readonly List<DirectoryInfo> _rgSiblings = new List<DirectoryInfo>();
        Editor                       _oSiblings;

        readonly string _strThumbsFileName = "MjolnirThumbs.zip";

        public ImageWalkerDir( IPgBaseSite oSiteBase ) : base( oSiteBase ) {
            _strIcon = @"ImageViewer.Content.icon-folder.png";
        }

        // At present we're never dirty in the directory case.
        protected override void Raise_DirtyDoc() {
            Raise_ImageUpdated();
        }

        /// <summary>
        /// Show the file we're pointed at, and then the directory we are perusing.
        /// </summary>
        public override string Banner => CurrentFileName + " @ " + CurrentDirectory;

        public override string CurrentDirectory {
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

        public override string CurrentFullPath {
			get {
				try {
					return( Path.Combine( CurrentDirectory, _oDisplayLine.ToString() ) );
				} catch( Exception oEx ) {
					Type[] rgErrors = { typeof( ArgumentException ),
										typeof( NullReferenceException ) };
					if( rgErrors.IsUnhandled( oEx ) )
						throw;

					return( string.Empty );
				}
			}
        }

        protected override string FullPathFromLine( Line oLine ) {
			try {
				return( Path.Combine( CurrentDirectory, oLine.ToString() ) );
			} catch( ArgumentException ) {
				return( string.Empty );
			}
        }

        protected void ThumbsLoad( string strFileName ) {
            try {
                // This is an odd bird. If the file does not exist, we still get a zero length stream!
                using( Stream oStream = new FileStream( strFileName, FileMode.Open, FileAccess.Read ) ) {
                    if( oStream.Length > 0 ) {
                        using( ZipArchive oZip = new ZipArchive( oStream ) ) {
                            ThumbsLoad( oZip );
                        }
                    }
                }
            } catch( Exception oEx ) {
                Type[] rgErrors = { typeof( ArgumentException ),
                                    typeof( ArgumentNullException ),
                                    typeof( ArgumentOutOfRangeException ),
                                    typeof( NotSupportedException ),
                                    typeof( System.Security.SecurityException ),
                                    typeof( FileNotFoundException ),
                                    typeof( IOException ),
                                    typeof( DirectoryNotFoundException ),
                                    typeof( PathTooLongException ),
                                    typeof( InvalidDataException )
                                  };
                if( rgErrors.IsUnhandled( oEx ) ) {
                    throw;
                }
            }
        }

        /// <summary>
        /// Saves all the the thumbs into a new thumbs zip file.
        /// </summary>
        /// <remarks>
        /// Load seems to be quick. But save is relatively expensive. We'll need to implement an
        /// incremental save at some point.
        /// </remarks>
        public virtual void ThumbsSaveAll( string strFileName ) {
            try {
                using( Stream oStream = new FileStream( strFileName, FileMode.Create, FileAccess.ReadWrite ) ) {
                    using( ZipArchive oZip = new ZipArchive( oStream, ZipArchiveMode.Create ) ) {
                        ThumbsSave( oZip );
                    }
                }
                _fDirtyThumbs = false;
            } catch( Exception oEx ) {
                Type[] rgErrors = { typeof( ArgumentException ),
                                    typeof( ArgumentNullException ),
                                    typeof( ArgumentOutOfRangeException ),
                                    typeof( NotSupportedException ),
                                    typeof( System.Security.SecurityException ),
                                    typeof( FileNotFoundException ),
                                    typeof( IOException ),
                                    typeof( DirectoryNotFoundException ),
                                    typeof( PathTooLongException ),
                                    typeof( UnauthorizedAccessException ),
                                  };
                if( rgErrors.IsUnhandled( oEx ) )
                    throw new ApplicationException( "Couldn't Save Thumbs.", oEx );

                LogError( "thumbnail cache", "Can't access the thumbnail cache. Is the file locked?" );
            }
        }

		/// <summary>
		/// In subclasses of this object we might want to show all files (in some manner ) and not just images.
		/// </summary>
		/// <param name="strExt">file extension</param>
		/// <returns></returns>
		protected virtual bool IsFileShown( string strExt ) {
			return( IsFileExtensionUnderstood( strExt )	);	
		}

        /// <summary>
        /// Load the filenames in the directory pointed to by the filepath.
        /// </summary>
        protected bool DirectoryRead( DirectoryInfo oDir ) {
            try {
                using( Editor.Manipulator oManip = FileList.CreateManipulator() ) {
                    // Need the gate AFTER the manipulator since it'll block the call back OnBufferChange()
                    // that gets called when the dispose on the manipulator get's called.
                    using( GateFilesEvent oGate = new GateFilesEvent( this ) ) {
						FileLine oLineNew = oManip.LineAppendNoUndo( ".." ) as FileLine;
						oLineNew._fIsDirectory = true;

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
								oLineNew = oManip.LineAppendNoUndo( oDirChild.Name ) as FileLine; // Don't load the path!!!

								oLineNew._dtModifiedDate = oDirChild.LastWriteTime;
								oLineNew._fIsDirectory   = true;
							}
                        }

                        // Sort so newest files are at the top. Hence the "negative sign"
                        // TODO : should be able to sort any way at any time!!! very cool.
                        rgFiles.Sort( (x,y) => - ( x.CreationTime.CompareTo( y.CreationTime ) ) );

                        foreach( FileInfo oFile in rgFiles ) {
							// Want to override what we load in the dialog box version of this control
							// even tho' the thumbnails list will only load the understood file extensions.
                            if( IsFileExtensionUnderstood( oFile.Extension ) ) {
                                oLineNew = oManip.LineAppendNoUndo( oFile.Name ) as FileLine; // Don't load the path!!!

                                oLineNew._dtModifiedDate = oFile.LastWriteTime;
                            }
                        }

						return true;
                    }
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

				_oSiteBase.LogError( "Image Walker", "Couldn't use the directory given." ); 
                FileList.Clear();
                using( Editor.Manipulator oManip = FileList.CreateManipulator() ) {
					FileLine oLineNew = oManip.LineAppendNoUndo( ".." ) as FileLine;
					oLineNew._fIsDirectory = true;
				}

				return false;
            }
        }

        /// <summary>
        /// Try to find the given filename in the currently loaded directory and 
        /// load up the display bitmap with it.
        /// </summary>
        /// <param name="strFileName"></param>
        protected virtual void ImageSearch( string strFileName, bool fSendImageUpdate = false ) {
			// First try to find matching file in the file list.
            foreach( Line oLine in FileList ) {
				FileLine oFileLine = oLine as FileLine;
                if( oLine.CompareTo( strFileName ) == 0 && !oFileLine._fIsDirectory ) {
                    _oDisplayLine = oLine;
                    break;
                }
            }
			// If haven't found anything try getting first image in directory.
            if( Line.IsNullOrEmpty( _oDisplayLine ) ) {
				foreach( Line oLine in FileList ) {
					FileLine oFileLine = oLine as FileLine;
					if( IsLineUnderstood( oFileLine ) && !oFileLine._fIsDirectory ) {
						_oDisplayLine = oLine;
						break;
					}
				}
			}
			// Finally just pick the first file.
            if( Line.IsNullOrEmpty( _oDisplayLine ) ) {
                _oDisplayLine = FileList[0];
				fSendImageUpdate = true;
			}

            ImageLoad( _oDisplayLine, fSendImageUpdate );
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

        public override void DirectoryNext( int p_iDirection ) {
            try {
                if( p_iDirection == 0 ) {
                    LoadAgain( Path.Combine( CurrentDirectory, ".." ) );
                } else {
					int iFound = _rgSiblings.FindIndex((x) => ( x.Name.Equals( _oDirectory.Name, StringComparison.OrdinalIgnoreCase ) ) );
					if( iFound < 0 )
						return;

                    int iIndex = iFound + p_iDirection;
                    if( iIndex < 0 || iIndex >= _rgSiblings.Count )
                        return;

                    LoadAgain(_rgSiblings[iIndex].FullName);
                }
            } catch( Exception oEx ) {
                Type[] rgErrors = { typeof( ArgumentException ),
                                    typeof( ArgumentNullException ),
                                    typeof( NullReferenceException ),
                                    typeof( ApplicationException ) };
                if( rgErrors.IsUnhandled( oEx ) )
                    throw new ApplicationException( "Unrecognized Directory Read Error", oEx );

                LogError( "ImageViewer", "Couldn't move to next directory" );
                return;
            }
        }

		/// <remarks>
		/// Note, it is possible to pass an invalid DirectoryInfo, so when we attempt
		/// to retrieve the sibling directories, we will fail.
		/// </remarks>
		/// <param name="oDir"></param>
		/// <returns></returns>
        public bool LoadChildren( DirectoryInfo oDir ) {
            try {
                _rgSiblings.Clear();

                foreach( DirectoryInfo oSibling in oDir.GetDirectories() ) {
                    _rgSiblings.Add( oSibling );
                }
            } catch( Exception oEx ) {
                Type[] rgErrors = { typeof( NullReferenceException ),
                                    typeof( DirectoryNotFoundException ),
                                    typeof( System.Security.SecurityException ),
                                    typeof( UnauthorizedAccessException ) };
                if( rgErrors.IsUnhandled( oEx ) )
                    throw new ApplicationException( "Couldn't load children of given directory.", oEx );

                return false;
            }

            _rgSiblings.Sort((x,y) => ( NaturalCompare( x.Name, y.Name ) ) );

            using( Editor.Manipulator oManip = _oSiblings.CreateManipulator() ) {
                foreach( DirectoryInfo oCurrent in _rgSiblings ) {
                    oManip.LineAppend( oCurrent.Name );
                }
            }
			return true;
        }

        /// <summary>
        /// Only the directory viewer is actually capable of reloading our contents on demand.
        /// Need to redo the view's thumb collection.
        /// </summary>
        /// <remarks>
        /// Can't tell the difference between path's with ext and file with ext.
        /// c:/foo/bar.buzz
        /// c:/foo/bar/name.txt
        /// </remarks>
        public override bool LoadAgain( string strFilePath ) {
            if( string.IsNullOrEmpty( strFilePath ) ) {
                _oSiteBase.LogError( "internal", "Image walker initialization parameter must be a filename." );
                return false;
			}

            // Save to the old directory thumbnail images.
            if( _oDirectory != null && _fDirtyThumbs ) {
                ThumbsSaveAll( Path.Combine( CurrentDirectory, _strThumbsFileName ) );
            }

            ThumbsDispose();

            FileList.Clear( fSendEvents:false ); 
			_rgSiblings.Clear();

			DirectoryInfo oDirectory;

            try {
				string strDirectory = string.Empty;

                if( Path.HasExtension( strFilePath ) )
                    strDirectory = Path.GetDirectoryName( strFilePath );
                else
                    strDirectory = strFilePath;

                if( string.IsNullOrEmpty( strDirectory ) )
                    strDirectory = Directory.GetCurrentDirectory();

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

                _oSiteBase.LogError( "alert", "Couldn't use the directory given." ); 
                _oDirectory = null;
                Raise_ImageUpdated();
                Raise_UpdatedThumbs();

                return false;
            }

			// BUG: Still not quite sorted in the case we don't load the directory.
            if( oDirectory.Parent != oDirectory ) {
				// Load the siblings of the directory we are going to read.
                if( !LoadChildren( oDirectory.Parent ) )
					return false;
            }

            if( !DirectoryRead( oDirectory ) ) {
				return false;
			}

			ThumbsLoad( Path.Combine( oDirectory.FullName, _strThumbsFileName ) );
            // this would be a good place to send a filelist loadeded event.
            Raise_TextLoaded();
            _oSiteWorkThumb.Queue( ThumbsCreateEnum( new SKSize( 100, 100 ) ), 0 );
            _oSiteWorkParse.Queue( CreateParseWorker(), 0 );

			bool fNullOldDir = _oDirectory == null; // old dir is null first time around.
				
			if( !fNullOldDir )
				fNullOldDir = string.Compare( _oDirectory.FullName, oDirectory.FullName ) != 0;

			_oDirectory = oDirectory;
			ImageSearch( Path.GetFileName( strFilePath ), fNullOldDir );
            
            return true;
        }

        /// <summary>
        /// It should be possible to initialize w/o an directory. But at the moment it's not coded.
        /// </summary>
        /// <returns></returns>
        public override bool InitNew() {
            if( !base.InitNew() )
                return( false );

            try {
                _oSiblings = new Editor( new ImageWalkerDocSlot( this ) );
            } catch( InvalidCastException) {
                _oSiteBase.LogError( "subhost", "ImageWalkDir can't supply correct site to an Editor.");
                return( false );
            }
            if( !_oSiblings.InitNew() )
                return( false );

			return( true );
        }

        /// <summary>
        /// We're designed to load a directory. We do weird stuff if we load from string!!!
        /// </summary>
        public override bool Load( TextReader oReader ) {
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

        public override bool Save() {
            // A little risky to depend on the dirty flag. But these are afterall, just thumnails.
            if( _fDirtyThumbs ) {
                ThumbsSaveAll( Path.Combine( CurrentDirectory, _strThumbsFileName ) );
                _fDirtyThumbs = false;
            }
            return true;
        }

        public override void CurrentFileDelete() {
            bool fDeleted = true;

			// This is a disaster if we delete the entire directory above us!! I've done it!
			if( _oDisplayLine.CompareTo( ".." ) == 0 )
				return;

            try {
                // If we are just a collection of images we must not actually delete the file on disk, just the ref.
                fDeleted = FileOperationAPIWrapper.FileRecycle( CurrentFullPath );
            } catch( Exception oEx ) {
                Type[] rgErrors = { typeof( ArgumentException ),
                                    typeof( ArgumentNullException ),
                                    typeof( DirectoryNotFoundException ),
                                    typeof( IOException ),
                                    typeof( NotSupportedException ),
                                    typeof( PathTooLongException ),
                                    typeof( UnauthorizedAccessException ) 
								  };
				if( rgErrors.IsUnhandled( oEx ) )
					throw;
                
                if( oEx.GetType() == typeof( IOException ) )
                    _oSiteBase.LogError( "alert", "Couldn't delete file, probably in use" );
                if( oEx.GetType() == typeof( UnauthorizedAccessException ) )
                    _oSiteBase.LogError( "alert", "You don't have proper priviledge to delete the file." );
            }

            if( !fDeleted ) {
                return;
            }

            // Raise_DirtyDoc(); Looks duplicative.

            base.CurrentFileDelete();
        }

        public override void ClipboardCopyFrom() {
            // We can't deal with cut and paste where we actually move the file so we'll just ignore for now.
        }
    }
}
