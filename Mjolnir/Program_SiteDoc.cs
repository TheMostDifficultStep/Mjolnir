using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Forms;
using System.IO;
using System.Security;
using System.Reflection;

using Play.Interfaces.Embedding; 
using Play.Parse.Impl;

namespace Mjolnir {
    /// <summary>
    /// In order to accomodate more view types, let's expose very little behavior to the shell
    /// in general. The shell needs very little from the document (as you can see).
    /// 
    /// Actually these slots should live on the program class. But I lazily tacked them on the
    /// MainWindow when I first tacked this all together. I call them slots because they are
    /// implementations of site's for guests. The guest document see's this object as a site.
    /// The shell see's it as a slot. Because it might have it's own site from it's container!!
    /// 
    /// This interface is for the Shell's benefit and not the guests. Upward facing so to speak.
    /// </summary>
    public interface IDocSlot {
		int         ID { get; }
        IDisposable Document { get; }
        bool        IsDirty  { get; }
        bool        Save( bool fNewLocation );
        void        Dispose();
        string      Title     { get; } // Slightly redundant, but more flexible than FileName. Do not use for Views. (they have their own system)
        string      FileName  { get; }
        int         Reference { get; set; }
        bool        InitNew();
        bool        Load( string strFileName ); // Kinda limits us to file types. But it's all the shell really supports anyway.
        string      LastPath { get; }

        IPgController2           Controller { get; }
        IEnumerable<IPgViewType> ViewTypes  { get; } // BUG: Probably can get from the controller now...
    }

    public partial class Program {
        public abstract class BaseSlot : 
            IPgBaseSite,
			IPgFileSite
        {
            protected readonly Program        _oHost;
            protected readonly int            _iDocCount;
            protected readonly IPgController2 _oController;

            protected IDisposable _oGuestDispose;

            protected int       _iReferences = 0;
            protected string    _strFileName = string.Empty;
            protected FILESTATS _eFileStats  = FILESTATS.UNKNOWN;
            protected readonly string _strFileExt;

            public BaseSlot( Program oProgram, IPgController2 oController, string strFileExt, int iID = -1 ) {
                _oHost       = oProgram    ?? throw new ArgumentNullException( "Program" );
                _strFileExt  = strFileExt  ?? throw new ArgumentNullException( "File Extension" );
                _oController = oController ?? throw new ArgumentNullException( "Controller" );

                if( _oController as IEnumerable<IPgViewType> == null )
                    throw new ArgumentException( "IEnumerable<IPgViewType> not found on controller" );

				_iDocCount = ( iID < 0 ) ? _oHost.DocCount : iID;
            }

 			public IPgParent Host => _oHost;
			public int       ID   => _iDocCount;

            public abstract void Notify( ShellNotify eEvent );
            public virtual  void LogError( string strMessage, string strDetails, bool fShow=true ) {
                _oHost.LogError( this, strMessage, strDetails, fShow );
            }

			public IPgController2 Controller {
                get { return( _oController ); }
            }

            /// <summary>
            /// This helps us keep track of views open on the document. If when references
            /// drop to zero we can close the document.
            /// </summary>
            public int Reference {
                get { return( _iReferences ); }
                set { _iReferences = value; }
            }

            public bool CreateDocument() {
                if( _oGuestDispose != null ) {
                    LogError( "The document has already been created for this slot" );
                    return false;
                }

                IDisposable oDoc;
                try {
                    oDoc  = _oController.CreateDocument( this, _strFileExt );
                } catch( Exception oEx ) {
                    Type[] rgErrors = { typeof( InvalidCastException ),
                                        typeof( ArgumentException ),
                                        typeof( NullReferenceException ),
										typeof( ArgumentNullException ),
										typeof( GrammerNotFoundException ), // BUG: not sure if gonna keep this case...
                                        typeof( NotImplementedException ),
										typeof( InvalidOperationException ), // Old error on generic failure.
                                        typeof( ApplicationException ) }; // new error on generic failure.
                    LogError( "hosting", "Unable provide guest with required interfaces.");

                    if( rgErrors.IsUnhandled( oEx ) )
                        throw;

                    return false;
                }

                try {
                    GuestSet( oDoc );
                } catch( Exception oEx  ) { 
                        Type[] rgErrors = { typeof( InvalidCastException ),
                                            typeof( ArgumentException ),
                                            typeof( NullReferenceException ),
											typeof( ArgumentNullException ) };
                        LogError( "hosting", "Guest does not support required interfaces.");

                        if( rgErrors.IsUnhandled( oEx ) )
                            throw;

                        return false;
                }

                return true;
            }

            /// <summary>
            /// While the slot/site is provided to the guest directly via it's constructor. We
            /// assigns the guest back to ourselves in a second step, so that we can query for
            /// other interfaces that subclasses might need. This avoids weird constructor
            /// warnings if we try to do it all at once. And makes makes for guests to create 
            /// READONLY links to the client site, tho' it does make it a bit more difficult for hosts. 
            /// But I don't mind the difficulty on the host side since we only have to write it once.
            /// </summary>
            protected virtual void GuestSet( IDisposable oGuest ) {
                _oGuestDispose = oGuest ?? throw new ArgumentNullException();
            }

            public virtual IDisposable Document {
                get { 
                    return( _oGuestDispose ); 
                }
            }

            public virtual IEnumerable<IPgViewType> ViewTypes {
                get { return( _oController as IEnumerable<IPgViewType>); }
            }

            public virtual bool InitNew() {
                return( false );
            }

            public virtual bool Load( string strFilename ) {
                return( false );
            }

            public virtual void Dispose() {
                if( _oGuestDispose != null ) {
                    _oGuestDispose.Dispose();
                    _oGuestDispose = null;
                }
            }
 
            /// <summary>
            /// Our document wishes to report an error.
            /// </summary>
            /// <param name="strMessage"></param>
            protected void LogError( string strMessage ) {
                LogError( "alert", strMessage );
            }

			public string FilePath {
				get { 
					try {
						return( Path.GetDirectoryName( FileName ) ); 
					} catch( NullReferenceException ) {
						return( string.Empty );
					}
				}
			}

            public string FileBase {
				get { 
					try {
						return( Path.GetFileName( FileName ) ); 
					} catch( NullReferenceException ) {
						return( string.Empty );
					}
				}
            }

            public virtual string FileName {
                get {
                    return( _strFileName );
                }

                set {
                    if( !string.IsNullOrEmpty( value ) ) {
                        _strFileName = value;
                    }
                }
            }

            protected void ExplainFileException( Exception oE ) {
				Dictionary<Type, string> rgMessages = new Dictionary<Type, string> {
					{ typeof( UnauthorizedAccessException ), "I think the file is read only!" },
					{ typeof( PathTooLongException        ), "The path is too long." },
					{ typeof( DirectoryNotFoundException  ), "I can't find the given directory!" },
				};

                string strError = String.Empty;

				if ( rgMessages.ContainsKey( oE.GetType() ) ) {
                    strError = rgMessages[ oE.GetType() ];
                } else {
                    strError = "Your save failed!";
                }

                LogError( "save", strError );
            }

            protected virtual void NewFileTitleAppend( StringBuilder sbTitle ) {
                sbTitle.Append( "?" );
            }

            /// <summary>
            /// We could almost remove this. The one advantage it has is that if the file name
            /// has not been chosen yet, we'll ask our subclasses for a value to use for the name
            /// and thus not be an empty string.
            /// </summary>
			public virtual string Title {
                get {
                    StringBuilder sbTitle = new StringBuilder( FileName );

                    if( string.IsNullOrEmpty( FileName ) ) {
                        NewFileTitleAppend( sbTitle );
                    }

                    if( IsDirty ) {
                        sbTitle.Append( "*" );
                    }

                    return( sbTitle.ToString() );
                }
            }

            //public virtual string TitleShort {
            //    get {
            //        StringBuilder sbTitle = new StringBuilder();

            //        if( string.IsNullOrEmpty( FileName ) ) {
            //            NewFileTitleAppend( sbTitle );
            //        } else {
            //            sbTitle.Append( Path.GetFileName( FileName ) );
            //        }

            //        if( IsDirty ) {
            //            sbTitle.Append( "*" );
            //        }

            //        return( sbTitle.ToString() );
            //    }
            //}

            public abstract bool IsDirty {
                get;
            }

            public FILESTATS FileStatus {
                get { return( _eFileStats ); }
            }

			public virtual Encoding FileEncoding {
				get { return( Encoding.Default ); }
			}
		}

        /// <summary>
        /// 1/26/2016: So, requiring my documents to inherit from a textreader is way more flexible than if I used a stream.
        /// You can imbed them easier in XML documents for example. BOM and encoding is handled by the caller. The only
        /// case this will be a bit of a drag is for bitmaps. I'll probably create a BinarySlot subclass of BaseSlot in that case.
        /// </summary>
        public class TextSlot : 
            BaseSlot,
            IPgFileSite,
            IDocSlot 
        {
            protected IPgLoad<TextReader> _oGuestLoad;
            protected IPgSave<TextWriter> _oGuestSave;

		    Encoding        _oEncoding = new UTF8Encoding( false, true ); // Don't emit BOM, tho I think it ignores me anyway.
			bool            _fLoaded   = false;

            public TextSlot( 
                Program        oProgram,
                IPgController2 oController,
                string         strFileExtension,
				int            iID = -1
		    ) : base( oProgram, oController, strFileExtension, iID ) {
            }

            // Debug helper
            public override string ToString() {
                return Title;
            }

			/// <summary>
			/// Would be nice if TextSlots return the base editor type.
			/// </summary>
            protected override void GuestSet( IDisposable value ) {
                base.GuestSet( value ); // mucho importante!!!

                try {
                    _oGuestLoad = (IPgLoad<TextReader>)value;
                    _oGuestSave = value as IPgSave<TextWriter>; // Language sites can't save. TODO: Refactor sites.
                } catch( InvalidCastException oEx ) {
                    throw new ArgumentException( "document dosen't support required interfaces.", oEx );
                }
            }

            public override string FileName {
                set {
                    if( !string.IsNullOrEmpty( value ) ) {
                        _strFileName = value;
                        _oHost.Raise_UpdateTitles( this );
                    }
                }
            }

            protected override void NewFileTitleAppend(StringBuilder sbTitle) {
                if( string.IsNullOrEmpty( _strFileExt ) ) {
                    base.NewFileTitleAppend(sbTitle);
                } else {
                    sbTitle.Append( _strFileExt );
                }
            }

            public string LastPath {
                get {
                    // If we've got a filename try that path first. 
                    if( string.IsNullOrEmpty( FileName ) || 
                        string.IsNullOrEmpty( Path.GetFileNameWithoutExtension( FileName ) ) ) 
                    {
                        return( _oHost.LastPath );
                    }

                    return( Path.GetDirectoryName( FileName ) );
                }
            }

            protected bool CheckLocation( bool fNewLocation ) {
                string strLastPath = _oHost.LastPath;

                // If we've got a filename try that path first. 
                if( string.IsNullOrEmpty( FileName ) || 
                    string.IsNullOrEmpty( Path.GetFileNameWithoutExtension( FileName ) ) )
                    fNewLocation = true;
                else
                    strLastPath = Path.GetDirectoryName( FileName );

                if( fNewLocation == true ) {
                    SaveFileDialog oDialog = new SaveFileDialog();

                    oDialog.InitialDirectory = strLastPath;
                    oDialog.ShowDialog();

                    if( oDialog.FileName == null || oDialog.FileName.Length == 0 || !oDialog.CheckPathExists ) {
                        LogError( "Please supply a valid file name for your next Save request. ^_^;" );
                        return( false );
                    }

                    FileName = oDialog.FileName;
                }

                return( true );
            }

            internal IEnumerator<ColorMap> Colors {
                get { return( _oHost.GetSharedGrammarColors() ); }
            }

            public override bool IsDirty {
                get { 
                    if( _oGuestSave == null )
                        return( false );

                    return( _oGuestSave.IsDirty ); 
                }
            }

            public override Encoding FileEncoding {
                get { return( _oEncoding ); }
            }

            /// <summary>
            /// The shell is requesting a save. This request should come when an editor window is focused.
            /// Be careful, the document can call back to it's site (that's us) on the Error() method 
            /// before returning! 
            /// </summary>
            /// <returns>True if saved successfully.</returns>
            /// <remarks>This is interesting in that the Save is going to originate from a displayed view,
            /// so really the best error would include the "Banner" from the view. But we don't know
            /// about it down here and so use the "Title" instead.</remarks>
            public bool Save( bool fAtNewLocation ) {
                if( _oGuestSave == null ) {
                    LogError( "Cannot persist " + Title, ". The object does not support IPgSave<TextWriter>." );
                    return false;
                }
				if( !_fLoaded ) {
					return true;
				}

                if( !CheckLocation( fAtNewLocation ) )
                    return( false );

                bool fSaved = false;

                try {
                    // Note: By default StreamWriter closes a stream when provided. Newer versions of .net provide leaveOpen flag.
                    //       Let's just use streamwriter with filename direcly since we're not dealing with binary objects yet. 
                    using( StreamWriter oWriter = new StreamWriter( FileName, false, _oEncoding ) ) {
                        fSaved = _oGuestSave.Save( oWriter );
						oWriter.Flush();
                    }
                } catch( Exception oEx ) {
					Type[] rgErrors = { 
						typeof( ArgumentNullException ),
						typeof( ArgumentException ),
						typeof( DirectoryNotFoundException ),
						typeof( IOException ),
						typeof( UnauthorizedAccessException ),
						typeof( PathTooLongException ),
						typeof( SecurityException ),
						typeof( NullReferenceException ) };

					if( rgErrors.IsUnhandled( oEx ) )
						throw;

                    ExplainFileException( oEx );
                }

                _oHost.Raise_UpdateTitles( this );

                return( fSaved );
            }

            /// <summary>
            /// When our guest is successfully loaded/inited call this sproc. We don't do anything by default.
            /// </summary>
            protected virtual void OnLoaded() {
            }

            /// <summary>
            /// Init the guest from the given file name. This should only be called once per instance. If we can't Init
            /// we should dispose this object since it will be in an invalid state.
            /// </summary>
            /// <returns>true if successful.</returns>
            public override bool InitNew() {
                if( _oGuestLoad == null ) {
                    LogError( "document", "Init failure, this object does not support IPgLoad<TextReader>: " + Title );
                    return( false );
                }

                if( !_oGuestLoad.InitNew() )
                    return( false );

				_fLoaded = true;

                OnLoaded(); // For our subclasses.

                return( true );
            }

            /// <summary>
            /// Load the guest from the given file name. This should only be called once per instance. If we can't load
            /// we should dispose this object since it will be in an invalid state.
            /// BUG: If we can't load the file stream, we bomb out before even initializing the program. That's
            /// seriously broken. We're really in a MUST LOAD situation.
            /// </summary>
            /// <returns>true if successful.</returns>
            public override bool Load( string strFileName ) {
                if( _oGuestLoad == null ) {
                    LogError( "document", "Load failure, his object does not support IPgLoad<TextReader>: " + Title );
                    return( false );
                }

                Encoding utf8NoBom = new UTF8Encoding(false);

                try {
                    FileInfo oFile = new System.IO.FileInfo(strFileName);

                    FileStream oByteStream = oFile.OpenRead(); // by default StreamReader closes the stream.
                    // Overridable versions of StreamReader can prevent that in higher versions of .net
                    using( StreamReader oReader = new StreamReader( oByteStream, utf8NoBom ) ) {
                        try {
							FileName = strFileName; // Guests sometimes need this when loading.

							if( oFile.IsReadOnly )
								_eFileStats = FILESTATS.READONLY;
							else
								_eFileStats = FILESTATS.READWRITE;

                            if( _fLoaded = _oGuestLoad.Load( oReader ) ) {
                                // Make sure you get the encoding AFTER you've read the file, else it'll be
                                // uninitialized. Not sure if encoding can change multiple times? This might
                                // bomb if I have a weird unicode file with no BOM.
                                bool fNoBOM = Equals(oReader.CurrentEncoding, utf8NoBom);
				                _oEncoding = fNoBOM ? utf8NoBom : oReader.CurrentEncoding;
                            }
						} catch( Exception oEx ) {
							Type[] rgErrors = { typeof( IOException ),
												typeof( ArgumentException ), 
												typeof( NullReferenceException ) }; 

							if( rgErrors.IsUnhandled( oEx ) )
								throw;

                            LogError( "Died trying to load : " + strFileName );
                        }
                    }

                    if( _fLoaded ) {
                        _oHost.LastPath = Path.GetDirectoryName( strFileName );
                        OnLoaded(); // For our subclasses.
                    }
                } catch( Exception oEx ) {
                    Type[] rgErrors = { typeof( DirectoryNotFoundException ),
                                        typeof( FileNotFoundException ),
                                        typeof( IOException ),
                                        typeof( ArgumentException ),
										typeof( ArgumentNullException ), // if the drive doesn't exist.
                                        typeof( NullReferenceException ),
                                        typeof( NotSupportedException ) }; 

					if( rgErrors.IsUnhandled( oEx ) )
						throw;

                    LogError( "Could not find or session is currently open :" + strFileName );
                    _fLoaded = false;
                }

                return ( _fLoaded );
            }

            public bool LoadResource( string strResourceName ) {
                Assembly     oAssembly = null;
                Stream       oStream   = null;
                StreamReader oReader   = null;
                bool         fReturn   = false;

                if( _oGuestLoad == null ) {
                    LogError( "internal", "Guest document does not support IPgLoad<TestReader>" );
                    return( false );
                }

                try {
                    oAssembly = Assembly.GetExecutingAssembly();
                    oStream   = oAssembly.GetManifestResourceStream( strResourceName );

                    using( oReader = new StreamReader( oStream ) ) {
                        fReturn = _oGuestLoad.Load( oReader ); 

                        if( fReturn ) {
                            FileName     = strResourceName;
                            _oEncoding   = oReader.CurrentEncoding;
                            _eFileStats  = FILESTATS.READONLY;

                            OnLoaded(); // For our subclasses.
                        }
                    }
                } catch( Exception oEx ) {
                    Type[] rgErrors = { typeof( ArgumentNullException ), typeof( ArgumentException ),
                                        typeof( FileLoadException ),     typeof( FileNotFoundException ),
                                        typeof( BadImageFormatException ) };
                    if( rgErrors.IsUnhandled( oEx ) ) 
						throw;

                    LogError( "LoadResource", "Tried to read: " + strResourceName );
                }

                return( fReturn );
            }

            public override void Notify( ShellNotify eEvent ) {
				switch( eEvent ) {
					case ShellNotify.DocumentDirty:
						_oHost.Raise_UpdateTitles( this );
						break;
				}
            }

            /// <summary>
            /// Clean up after all the stuff we put into the Main Window which
            /// is our host.
            /// </summary>
            public override void Dispose() {
				_oGuestLoad = null;
				_oGuestSave = null;

                base.Dispose();
            }
        }

        /// <summary>
        /// Special little class just for the site holding our Session data, 
		/// Alerts, Scrapbook and SearchResults.
        /// Consider caching IPgCommandBase from the guest.
        /// </summary>
        public class InternalSlot :
            TextSlot
        {
            protected readonly string _strName;

            public InternalSlot(
                Program oProgram,
                string  strControllerExt,
                string  strName // Need localized values.

		    ) : base( oProgram, oProgram.GetController( strControllerExt ), strControllerExt ) {
                _strName = strName;
            }
            
            public InternalSlot(
                Program oProgram,
                string  strName // Need localized values.

		    ) : base( oProgram, oProgram.PlainTextController, ".txt" ) {
                _strName = strName;
            }

            public InternalSlot(
                Program        oProgram,
                IPgController2 oController
            ) : base( oProgram, oController, string.Empty ) {
            }
        
            public InternalSlot(
                Program        oProgram,
                IPgController2 oController,
                string         strExtension
            ) : base( oProgram, oController, strExtension ) {
            }
        }

		/// <summary>
		/// Work in process. Might not need it 
		/// </summary>
		/// <seealso cref="InternalSlot"/>
		//public class EmbeddedSlot: IPgBaseSite {
		//	protected readonly Program _oProgram;

  //          protected IPgLoad<TextReader> _oGuestLoad;
  //          protected IPgSave<TextWriter> _oGuestSave;

		//	public EmbeddedSlot(Program oProgram, IPgController2 oController )
		//	{
		//		_oProgram = oProgram ?? throw new ArgumentNullException();
		//	}

		//	public bool IsDirty {
		//		get {
		//			try {
		//				return _oGuestSave.IsDirty;
		//			} catch( NullReferenceException ) {
		//				LogError( "embedding", "Guest does not support IPgSave<TextWriter>" );
		//			}
		//			return false;
		//		}
		//	}

		//	public IPgParent Host => _oProgram;

		//	public void LogError(string strMessage,string strDetails,bool fShow = true) {
		//		_oProgram.LogError( this, strMessage, strDetails, fShow );
		//	}

		//	public void Notify(ShellNotify eEvent) {
		//		switch( eEvent ) {
		//			case ShellNotify.DocumentDirty:
		//				// Route this to our embedding document.
		//				break;
		//		}
		//	}
		//}

		/// <summary>
		/// Session is special in that it controls the title for the main window. We're hosting ourself!
		/// </summary>
		public class SessonSlot : InternalSlot {
            public SessonSlot(
                Program oProgram
		    ) : base( oProgram, "Sesson" ) {
				GuestSet( oProgram );
            }
        
            /// <summary>
            /// Change the behavior for our session title case. Just want the filename and no path.
            /// </summary>
            public override string Title {
                get {
					string strFileOnly = Path.GetFileName( FileName );

					if( string.IsNullOrEmpty( strFileOnly ) )
						return( string.Empty );

                    StringBuilder sbTitle = new StringBuilder();

                    sbTitle.Append( strFileOnly );
                    if( IsDirty ) {
                        sbTitle.Append( "*" );
                    }

                    return( sbTitle.ToString() );
                }
            }

			/// <summary>
			/// Session state is dirty ONLY if the user decides to persist their session data. 
			/// </summary>
			public override bool IsDirty {
				get {
					return _oHost.IsDirty && !string.IsNullOrEmpty( FileName );
				}
			}
		}

        /// <summary>
        /// A persists differently from a normal doc browser. Usually persisting to a given
        /// file per directory. So let's override the Title Long/Short behavior 
        /// </summary>
        public class DirBrowserSlot : 
            BaseSlot,
            IDocSlot
        {
            IPgLoadURL _oGuestLoad;
            IPgSaveURL _oGuestSave;

            public DirBrowserSlot( Program oProgram, IPgController2 oController, string strFileExtn, int iID = -1 ) : 
                base( oProgram, oController, strFileExtn, iID ) 
            {
            }

            /// <summary>
            /// Be aware that the guest might be in an zombie state and unable to return
            /// a path and may return string empty. We should save the path independently
            /// of the object just in case of this problem.
            /// </summary>
			public override string FileName { 
				get { return( _oGuestLoad.CurrentURL ); } 
				set => base.FileName = value; 
			}

            /// <summary>
            /// Basically any object here must support the Load/Save URL interfaces.
            /// </summary>
            /// <exception cref="InvalidCastException" />
			protected override void GuestSet( IDisposable value ) {
                base.GuestSet( value );

                _oGuestLoad = (IPgLoadURL)value;
                _oGuestSave = (IPgSaveURL)value;
            }

            // TODO : Look at this. The filename saved should just be a directory.
            public string LastPath {
                get {
                    // If we've got a filename try that path first. 
                    if( string.IsNullOrEmpty( FileName ) || 
                        string.IsNullOrEmpty( Path.GetFileNameWithoutExtension( FileName ) ) ) 
                    {
                        return( _oHost.LastPath );
                    }

                    return( Path.GetDirectoryName( FileName ) );
                }
            }

            public override bool InitNew() {
                return( _oGuestLoad.InitNew() );
            }

            /// <summary>
            /// Browsers don't generally load from a persisted file but view the directory given starting
            /// at the given file. If a browser does persist via a ".browser" file, we'll have to figure
            /// out some way to deal with both cases in this class. Side step that for now by not allowing
            /// save via this object.
            /// </summary>
            public override bool Load( string strFileName ) {
                FileName = strFileName; 

                return _oGuestLoad.LoadURL( strFileName );
            }

            /// <summary>
            /// TODO: So even tho' we started from from a directory. We could persist to a file our
            /// directory we are viewing. But I'll need to get a name for the file. And then disambiguate
            /// the load case above. Let's punt for now.
            /// </summary>
            public bool Save( bool fRename ) {
                if( _oGuestSave == null ) {
                    LogError( "Cannot persist " + Title, ". The object does not support IPgSaveURL." );
                    return( false );
                }

                _oGuestSave.Save();

                _oHost.Raise_UpdateTitles( this );

                return true; // Probably should check save but let's do that after up on dot net core 3.
            }

            /// <summary>
            /// Since we can perist thumbs. Let's check if dirty or not.
            /// </summary>
            public override bool IsDirty { 
                get { return _oGuestSave.IsDirty; }
            }

            public override void Notify( ShellNotify eEvent ) {
				switch( eEvent ) {
					case ShellNotify.DocumentDirty:
						// BUG set isDirty flag. Gonna need to add a bunch of plumbing to set the title
						// based on the current file in the document versus the persisted file! Sigh.
						//_oHost.OnDocDirtyChanged( this );
						_oHost.Raise_UpdateTitles( this );
						break;
				}
            }
        }
    }    
}
