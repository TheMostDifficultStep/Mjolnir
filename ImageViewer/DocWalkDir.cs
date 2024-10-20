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
        //public override string Banner => CurrentFileName + " @ " + CurrentDirectory;

        public override string CurrentDirectory {
            get { 
				try {
                    // The directory can be null when we're loading up a net address
                    // that isn't available. Like when my lappie is not at home! >_<;;
                    if( _oDirectory != null )
					    return( _oDirectory.FullName );
				} catch( Exception oEx ) {
					Type[] rgError = { typeof( NullReferenceException ),
									   typeof( PathTooLongException ),
									   typeof( System.Security.SecurityException ) };
					if( rgError.IsUnhandled( oEx ) )
						throw;

				}
				return( string.Empty );
            }
        }

        public string CurrentURL { get { return CurrentFullPath; } }

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
						FileLine oLineNew      = oManip.LineAppendNoUndo( ".." ) as FileLine;
						oLineNew.IsDirectory = true;

                        // 12/23/2015 : I could potentially put fileinfo's directly into my document. But 
						// We're more 'document' compatible if we use string paths.
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
								oLineNew = oManip.LineAppendNoUndo( oDirChild.Name ) as FileLine; // Don't load the path!!!

								oLineNew.ModifiedDate = oDirChild.LastWriteTime;
								oLineNew.IsDirectory   = true;
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

                                oLineNew.ModifiedDate = oFile.LastWriteTime;
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
					oLineNew.IsDirectory = true;
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
                if( oLine.CompareTo( strFileName ) == 0 && !oFileLine.IsDirectory ) {
                    _oDisplayLine = oLine;
                    break;
                }
            }
			// If haven't found anything try getting first image in directory.
            if( Line.IsNullOrEmpty( _oDisplayLine ) ) {
				foreach( Line oLine in FileList ) {
					FileLine oFileLine = oLine as FileLine;
					if( IsLineUnderstood( oFileLine ) && !oFileLine.IsDirectory ) {
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

        public override bool Execute(Guid sGuid) {
            if( sGuid == GlobalCommands.Recycle ) {
                return LoadAgain( CurrentDirectory );
            }
            return base.Execute(sGuid);
        }

        /// <remarks>
        /// Note, it is possible to pass an invalid DirectoryInfo, so when we attempt
        /// to retrieve the sibling directories, we will fail.
        /// </remarks>
        /// <param name="oDir"></param>
        /// <returns></returns>
        public bool LoadChildren( DirectoryInfo oDir ) {
            _rgSiblings.Clear();

            if( oDir == null )
                return true;

            try {
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

            _rgSiblings.Sort((x,y) => FindStuff<string>.NaturalCompare( x.Name, y.Name ) );

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
                // Used to return false if fail. But let's ignore.
                LoadChildren( oDirectory.Parent );
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
			ImageSearch( Path.GetFileName( strFilePath ), true /* fNullOldDir */ );
            
            return true;
        }

        /// <summary>
        /// Instead of LoadAgain which flushes everything. This method just updates the
        /// the new element to show. Doesn't presently delete any old elements.
        /// </summary>
        /// <returns>True if there is a current directory to refresh.</returns>
        public bool Refresh() {
            if( _oDirectory == null )
                return false;

            List      <Line>     rgDelLines = new();
            LinkedList<FileInfo> rgNewFiles = new();
            // Load up the current state of the dir.
            foreach( FileInfo oFile in _oDirectory.GetFiles( "*.*", SearchOption.TopDirectoryOnly ) ) {
				if( !oFile.Attributes.HasFlag( FileAttributes.Hidden    )  &&
                    !oFile.Attributes.HasFlag( FileAttributes.Directory )  &&
                    IsFileExtensionUnderstood( oFile.Extension ) )
                {
					rgNewFiles.AddLast( oFile );
				}
            }

            //Compare old read with newest read.
            foreach( Line oLine in FileList ) {
                if( oLine is FileLine oFileLine && !oFileLine.IsDirectory ) {
                    // CompareTo() on the Line object generates a string...
                    bool fFound = false;
                    foreach( FileInfo oInfo in rgNewFiles ) {
                        if( oFileLine.CompareTo(oInfo.Name) == 0 ) {
                            // Remove the file from the list if it's already there.
                            rgNewFiles.Remove( oInfo );
                            fFound = true;
                            break;
                        }
                    }
                    if( !fFound ) {
                        rgDelLines.Add(oLine);
                    }
                }
            }
            if( rgDelLines.Count > 0 ) {
                // Go backwards from bottom and remove the lines.
                // Check if the deleted item was the display line.
            }
            // At this point only the files not in the old list are in the new files list.
            if( rgNewFiles.Count > 0 ) {
                // Could consider an insertion sort. Doing it lazy way right now.
                using Editor.Manipulator oManip = FileList.CreateManipulator();
                foreach( FileInfo oInfo in rgNewFiles ) {
                    for( int iLine = 0; iLine < FileList.ElementCount; ++iLine ) {
                        Line oLine = FileList[iLine];
                        if( oLine is FileLine oFileLine && !oFileLine.IsDirectory ) {
                            if( oFileLine.ModifiedDate < oInfo.LastWriteTime ) {
                                if( oManip.LineInsert( oFileLine.At, oInfo.Name ) is FileLine oNew ) {
                                    oNew.ModifiedDate = oInfo.LastWriteTime;
                                    break;
                                }
                            }
                        }
                    }
                }
                FileList.CharacterCount( 0 );
            }

            _oSiteWorkThumb.Queue( ThumbsCreateEnum( new SKSize( 100, 100 ) ), 0 );
            Raise_TextLoaded(); // Stupid but this is when we update the layout.

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

        /// <summary>
        /// Would be nice if we save the path with this object, somehow. But the
        /// normal TestSite saves to a file and all we want to do here is save the
        /// URL in a session file!!
        /// </summary>
        /// <seealso cref="Program.DirBrowserSlot.Save( bool )"/>
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
