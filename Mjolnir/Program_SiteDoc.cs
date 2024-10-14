using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Forms;
using System.IO;
using System.Security;
using System.Reflection;

using Play.Interfaces.Embedding; 
using Play.Parse.Impl;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.Window;

namespace Mjolnir {
    /// <summary>
    /// In order to accomodate more view types, let's expose very little behavior to the shell
    /// in general. The shell needs very little from the document (as you can see).
    /// 
    /// I call site implementations, slots, because they are implementations of site's for document. The 
    /// guest document see's this object as a site. The shell see's it as a slot. Because it 
    /// might have it's own site from it's container!!
    /// 
    /// This particular interface is for the Shell's benefit and not the guests. Upward facing so to speak.
    /// </summary>
    public interface IDocSlot {
		int         ID { get; }
        IDisposable Document { get; }
        bool        IsDirty  { get; }
        bool        Save( bool fNewLocation );
        void        Dispose();
        string      Title     { get; } // Slightly redundant, but more flexible than FileName. Do not use for Views. (they have their own system)
        /// <summary>
        /// Full file name path, dir and ext. Don't want to be using for session
        /// save. Only titles, but I'm trying to sort that out.
        /// </summary>
        string      FilePath  { get; }
        /// <summary>
        /// This is the verified
        /// directory to the file. It will never include the a file name.
        /// in the case that the file has no extension.
        /// </summary>
        string      FileDir   { get; }
        /// <summary>
        /// This is only the name of the file. Even if it has no extension.
        /// </summary>
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
            protected string    _strFileName = string.Empty; // Just file name if available.
            protected string    _strFileDir  = string.Empty; // Just the path w/ no filename.
            protected string    _strFilePath = string.Empty; // Full path and name.
            protected FILESTATS _eFileStats  = FILESTATS.UNKNOWN;

            public readonly string _strFileExt;

			public static readonly Type[] _rgFileErrors = { 
						typeof( ArgumentNullException ),
						typeof( ArgumentException ),
						typeof( NullReferenceException ),
						typeof( DirectoryNotFoundException ),
						typeof( IOException ),
						typeof( UnauthorizedAccessException ),
						typeof( PathTooLongException ),
						typeof( SecurityException ),
                        typeof( InvalidOperationException ),
                        typeof( NotSupportedException ),
                        typeof( FileNotFoundException ) };

            /// <remarks>TODO: Would be nice to update this to use the PgDocDescr object
            /// instead of the oController/strFileExt pair. </remarks>
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

            public string LastPath {
                get {
                    // If we've got a filename try that path first. 
                    if( string.IsNullOrEmpty( FilePath ) || 
                        string.IsNullOrEmpty( Path.GetFileNameWithoutExtension( FilePath ) ) ) 
                    {
                        return _oHost.LastPath;
                    }

                    return FileDir;
                }
            }

            public virtual string FileDir => _strFileDir;

            /// <summary>
            /// Just the file name. No path.
            /// </summary>
            public virtual string FileName {
				get { 
					try {
						return _strFileName; 
					} catch( NullReferenceException ) {
						return string.Empty;
					}
				}
            }

            protected string CheckLocation( bool fNewLocation ) {
                string strLastPath = _oHost.LastPath;

                // If we've got a filename try that path first. 
                if( string.IsNullOrEmpty( FilePath ) || 
                    string.IsNullOrEmpty( Path.GetFileNameWithoutExtension( FilePath ) ) )
                    fNewLocation = true;
                else
                    strLastPath = Path.GetDirectoryName( FilePath );

                if( fNewLocation == true ) {
                    SaveFileDialog oDialog = new SaveFileDialog();

                    oDialog.InitialDirectory = strLastPath;
                    oDialog.ShowDialog();

                    if(  oDialog.FileName        == null || 
                         oDialog.FileName.Length == 0    || 
                        !oDialog.CheckPathExists ) 
                    {
                        LogError( "Please supply a valid file name for your next Save request. ^_^;" );
                        return null;
                    }

                    return oDialog.FileName;
                }

                return FilePath;
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

            /// <summary>
            /// By being abstract, it tells the user that they've got to implement
            /// something. I used have a virtual return false here.
            /// </summary>
            public abstract bool InitNew() ;

            /// <summary>
            /// By being abstract, it tells the user that they've got to implement
            /// something. I used have a virtual return false here.
            /// </summary>
            public abstract bool Load( string strFilename );

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

            /// <summary>
            /// Full file name and path. Use for titles and such. DO NOT
            /// USE for opening files. Obviously, SAVE the file FIRST before
            /// calling this property!
            /// </summary>
            /// <remarks>We want to use the FileInfo() object for when we
            /// are setting paths on the image viewer which never has a file
            /// name and Path.GetFileName() will return c:\test\me "me" in
            /// this case. FileInfo does not make that mistake...</remarks>
            /// <seealso cref="FileName"/>
            /// <seealso cref="FilePath"/>
            /// <seealso cref="FileDir"/>
            public virtual string FilePath {
                set {
                    FileInfo oFileInfo = new FileInfo( value );
                    if( oFileInfo.Exists ) {
                        _strFileName = oFileInfo.Name;
                        _strFileDir  = oFileInfo.DirectoryName;
                    } else {
                        _strFileName = string.Empty;
                        _strFileDir  = oFileInfo.FullName;
                    }
                    // We cache this b/c it get's called a lot for
                    // the view's title bar.
                    _strFilePath = oFileInfo.FullName;

                    //if( !string.IsNullOrEmpty( value ) ) {
                    //    _strFileName = value;
                    //}
                }

                get {
                    try {
                        return _strFilePath;
                    } catch( NullReferenceException ) {
                        return string.Empty;
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
                    StringBuilder sbTitle = new StringBuilder( FilePath );

                    if( string.IsNullOrEmpty( FilePath ) ) {
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

            protected override void NewFileTitleAppend(StringBuilder sbTitle) {
                if( string.IsNullOrEmpty( _strFileExt ) ) {
                    base.NewFileTitleAppend(sbTitle);
                } else {
                    sbTitle.Append( _strFileExt );
                }
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

                string strPath = CheckLocation( fAtNewLocation );
                if( strPath == null )
                    return false;

                bool fSaved = false;

                try {
                    // Note: By default StreamWriter closes a stream when provided. Newer versions of .net provide leaveOpen flag.
                    //       Let's just use streamwriter with filename direcly since we're not dealing with binary objects yet. 
                    using( StreamWriter oWriter = new StreamWriter( strPath, false, _oEncoding ) ) {
                        fSaved = _oGuestSave.Save( oWriter );
						oWriter.Flush();
                    }
                } catch( Exception oEx ) {
					if( _rgFileErrors.IsUnhandled( oEx ) )
						throw;

                    ExplainFileException( oEx );
                }

                FilePath = strPath;
                // If I don't call this, then the session (if using) doesn't
                // wipe the astrisk off of the title. 
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
                    FileInfo oFile = new FileInfo(strFileName);

                    FileStream oByteStream = oFile.OpenRead(); // by default StreamReader closes the stream.
                    // Overridable versions of StreamReader can prevent that in higher versions of .net
                    using( StreamReader oReader = new StreamReader( oByteStream, utf8NoBom ) ) {
                        try {
							FilePath = oFile.FullName; // Guests sometimes need this when loading.

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
							if( _rgFileErrors.IsUnhandled( oEx ) )
								throw;

                            LogError( "Died trying to load : " + strFileName );
                        }
                    }

                    if( _fLoaded ) {
                        _oHost.LastPath = Path.GetDirectoryName( strFileName );
                        OnLoaded(); // For our subclasses.
                    }
                } catch( Exception oEx ) {
					if( _rgFileErrors.IsUnhandled( oEx ) )
						throw;

                    LogError( "Could not find or session is currently open :" + strFileName );
                    _fLoaded = false;
                }

                return ( _fLoaded );
            }

            public override void Notify( ShellNotify eEvent ) {
				switch( eEvent ) {
                    case ShellNotify.MediaStatusChanged:
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

        public class BinarySlot :
            BaseSlot,
            IPgFileSite,
            IDocSlot {

            protected IPgLoad<BinaryReader> _oGuestLoad;
            protected IPgSave<BinaryWriter> _oGuestSave;

            public BinarySlot(Program oProgram, IPgController2 oController, string strFileExt, int iID = -1) : 
                base(oProgram, oController, strFileExt, iID) 
            {
            }

            public override bool IsDirty {
                get { 
                    if( _oGuestSave != null )
                        return _oGuestSave.IsDirty;
                    return false;
                }
            }

            protected override void GuestSet( IDisposable value ) {
                base.GuestSet( value ); // mucho importante!!!

                try {
                    _oGuestLoad = (IPgLoad<BinaryReader>)value;
                    _oGuestSave = value as IPgSave<BinaryWriter>; 
                } catch( InvalidCastException oEx ) {
                    throw new ArgumentException( "document doesn't support required interfaces.", oEx );
                }
            }

            /// <remarks>You know I could probably make a templatized base object... 7/7/2023</remarks>
            public override void Notify( ShellNotify eEvent ) {
				switch( eEvent ) {
                    case ShellNotify.MediaStatusChanged:
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

            public override bool InitNew() {
                if( _oGuestLoad == null ) {
                    LogError( "document", "Init failure, this object does not support IPgLoad<BinaryReader>: " + Title );
                    return false;
                }

                if( !_oGuestLoad.InitNew() )
                    return false;

                // Might want to save load state so I can see if we get called again...

                return true;
            }

            public override bool Load(string strFileName) {
                if( _oGuestLoad == null ) {
                    LogError( "document", "Load failure, his object does not support IPgLoad<BinaryReader>: " + Title );
                    return( false );
                }

                try {
                    FileInfo oFile = new FileInfo(strFileName);

                    FileStream oByteStream = oFile.OpenRead(); 

                    using( BinaryReader oReader = new BinaryReader( oByteStream ) ) {
                        try {
							FilePath = oFile.FullName; // Guests sometimes need this when loading.

							if( oFile.IsReadOnly )
								_eFileStats = FILESTATS.READONLY;
							else
								_eFileStats = FILESTATS.READWRITE;

                            if( !_oGuestLoad.Load( oReader ) )
                                return false;

                            _oHost.LastPath = Path.GetDirectoryName( strFileName );
						} catch( Exception oEx ) {
							if( _rgFileErrors.IsUnhandled( oEx ) )
								throw;

                            LogError( "Died trying to load : " + strFileName );
                        }
                    }
                } catch( Exception oEx ) {
					if( _rgFileErrors.IsUnhandled( oEx ) )
						throw;

                    LogError( "Could not find or session is currently open :" + strFileName );
                }

                return true;
            }
            public bool Save(bool fAtNewLocation) {
                if( _oGuestSave == null ) {
                    LogError( "Cannot persist " + Title, ". The object does not support IPgSave<TextWriter>." );
                    return false;
                }
				//if( !_fLoaded ) {
				//	return true;
				//}

                string strPath = CheckLocation( fAtNewLocation );

                if( strPath == null )
                    return false;

                bool fSaved = false;

                try {
                    FileInfo   oFile       = new FileInfo(strPath);
                    FileStream oByteStream = oFile.OpenWrite(); 

                    oByteStream.SetLength( 0 ); // the best way? :-/

                    using( BinaryWriter oWriter = new BinaryWriter( oByteStream, ASCIIEncoding.UTF8 ) ) {
                        fSaved = _oGuestSave.Save( oWriter );
						oWriter.Flush();
                    }
                } catch( Exception oEx ) {
					if( _rgFileErrors.IsUnhandled( oEx ) )
						throw;

                    ExplainFileException( oEx );
                }

                FilePath = strPath;
                //_oHost.Raise_UpdateTitles( this );

                return fSaved;
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
                Program        oProgram,
                PgDocDescr     oDescriptor,
                string         strName
            ) : base( oProgram, oDescriptor.Controller, oDescriptor.FileExtn ) {
                _strName = strName ?? throw new ArgumentNullException( nameof( strName ) );
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
            public SessonSlot( Program oProgram, PgDocDescr oDescr )
		    : base( oProgram, oDescr, "Sesson" ) {
				GuestSet( oProgram );
            }
        
            /// <summary>
            /// Change the behavior for our session title case. Just want the filename and no path.
            /// </summary>
            public override string Title {
                get {
					string strFileOnly = FileName;

					if( string.IsNullOrEmpty( strFileOnly ) )
						return string.Empty;

                    StringBuilder sbTitle = new StringBuilder();

                    sbTitle.Append( strFileOnly );
                    if( IsDirty ) {
                        sbTitle.Append( '*' );
                    }

                    return sbTitle.ToString();
                }
            }

			/// <summary>
			/// Session state is dirty ONLY if the user decides to persist their session data. 
			/// </summary>
			public override bool IsDirty {
				get {
					return _oHost.IsDirty && !string.IsNullOrEmpty( FilePath );
				}
			}
		}

        /// <summary>
        /// A persists differently from a normal doc browser. Usually persisting to a given
        /// file per directory. So let's override the Title Long/Short behavior 
        /// </summary>
        public class DirSlot : 
            BaseSlot,
            IDocSlot
        {
            IPgLoadURL _oGuestLoad;
            IPgSaveURL _oGuestSave;

            public DirSlot( Program oProgram, IPgController2 oController, string strFileExtn, int iID = -1 ) : 
                base( oProgram, oController, strFileExtn, iID ) 
            {
            }

            /// <summary>
            /// TODO: Be aware that the guest might be in an zombie state and unable to return
            /// a path and may return string empty. We should save the path independently
            /// of the object just in case of this problem.
            /// </summary>
			public override string FilePath { 
				get { return _oGuestLoad.CurrentURL; } 
				set => base.FilePath = value; 
			}

            /// <summary>
            /// This problem between the file and the path and the dir
            /// has been brewing for while. I Probably need to update
            /// the IPgLoadURL interface to return the file and the dir
            /// seperately. 
            /// </summary>
            public override string FileDir {
                get {
                    return Path.GetDirectoryName( _oGuestLoad.CurrentURL );
                }
            }

            /// <summary>
            /// IPgLoadURL probably needs updating b/c files that don't have
            /// an extension can fool the Path parser. 
            /// </summary>
            public override string FileName {
                get {
                    return Path.GetFileName( _oGuestLoad.CurrentURL );
                }
            }

            /// <summary>
            /// Basically any object here must support the Load/Save URL interfaces.
            /// </summary>
            /// <exception cref="InvalidCastException" />
			protected override void GuestSet( IDisposable value ) {
                base.GuestSet( value );

                _oGuestLoad = (IPgLoadURL)value;
                _oGuestSave = value as IPgSaveURL;
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
                FilePath = strFileName; 

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
                    return true;
                }

                _oGuestSave.Save();

                _oHost.Raise_UpdateTitles( this );

                return true; // Probably should check save but let's do that after up on dot net core 3.
            }

            /// <summary>
            /// Since we can perist thumbs. Let's check if dirty or not.
            /// </summary>
            public override bool IsDirty { 
                get { 
                    if( _oGuestSave == null )
                        return false;
                    
                    return _oGuestSave.IsDirty; 
                }
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
