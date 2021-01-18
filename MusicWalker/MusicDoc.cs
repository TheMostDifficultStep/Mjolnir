using System;
using System.IO;
using System.Web;
//Depricated AAAARRRGGHGH!
//using System.ServiceModel;
//using System.ServiceModel.Web;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Net.Http;
using System.Reflection;

using Play.Interfaces.Embedding;
using Play.Sound;
using Play.Edit;
using Play.Parse.Impl;
using Play.Integration;
using Play.ImageViewer;

namespace Play.MusicWalker {
	/// <summary>
	/// An iterator to play a song list. Feed this to the program scheduler.
	/// </summary>
	/// <remarks>This is a great spot where yield return for making an iterator is super cool... BUT
	/// we need to clean up resourses right away as we go. Unfortunately when using the yield approach
	/// to build an iterator implementation, we can't clean up in the event the iterator is abandoned,
	/// since can't override Dispose(). So we've got to do all the work from scratch.
	/// Somewhere, I was reading you can't put unsafe code in an iterator, but if you want to live safely
	/// you shouldn't be using this code.
	/// </remarks>
	abstract public class SongWorker : IEnumerator<int> {
		protected readonly IPgBaseSite    _oSiteBase;

				  readonly IPgSound  _oSound;
		protected          IPgPlayer _oPlayer;
		protected          IPgReader _oDecoder;

		uint _uiWait = 0;

		public SongWorker( IPgBaseSite oSiteBase ) {
			_oSiteBase = oSiteBase ??  throw new ArgumentNullException( "Need a site with hilight." );
			_oSound    = oSiteBase.Host.Services as IPgSound ?? throw new ArgumentNullException( "Host requires IPgSound." );
		}

		/// <summary>
		/// Returns the recommended time in milliseconds to "sleep", or do something else.
		/// </summary>
		int IEnumerator<int>.Current {
			get{ return (int)_uiWait; }
		}

		object IEnumerator.Current => throw new NotImplementedException();

		abstract public string SongFileName();
		abstract public bool   SongMoveNext();

		protected bool GetNextSong() {
			do {
				if( _oDecoder != null ) {
					_oDecoder.Dispose();
					_oDecoder = null;
				}

				if( !SongMoveNext() )
					return false;

				_oDecoder = GetReader( SongFileName() );
			} while( _oDecoder == null );

			_oPlayer = GetPlayer( _oPlayer, _oDecoder.Spec );

			if( _oPlayer == null )
				return false;

			return true;
		}

		bool IEnumerator.MoveNext() {
			do {
				try {
					_uiWait = ( _oPlayer.Play( _oDecoder ) >> 1 ) + 1;
					if( _oDecoder.IsReading )
						return true;
					// If decoder is done, move on to the next song!
				} catch( NullReferenceException ) {
					_oSiteBase.LogError( "player","Problem with current song: " + SongFileName() );
				}
			} while( GetNextSong() );
			// TODO: If we move to the next song but it's a different Spec we might
			//       interrupt the player before it has bled off. Need to check that case.

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
			try {
				return( _oSound.CreateSoundDecoder( strFileName ) );
			} catch( Exception oEx ) {
				Type[] rgErrors = { typeof( FormatException ),
									typeof( FileNotFoundException ),
									typeof( InvalidOperationException ),
									typeof( NullReferenceException ) };
				if( rgErrors.IsUnhandled( oEx ) )
					throw;

				_oSiteBase.LogError( "sound", "Couldn't play : " + strFileName );
				return( null );
			}
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

	public class SongCredentials {
		readonly string _strFullName;

		public SongCredentials( string strFullName, int iAlbumIndex, int iSongIndex, int iID = -1 ) {
			_strFullName = strFullName ?? throw new ArgumentNullException( "filename for song must not be null" );

			AlbumIndex = iAlbumIndex;
			SongIndex  = iSongIndex;
			BatchID    = iID;
		}

		public int AlbumIndex { get; }
		public int SongIndex  { get; }
		public int BatchID    { get; }

		public override string ToString() {
			return _strFullName;
		}
	}

	public interface IPgPlayerSite {
		void OnSongClear();
		void OnSongPlay ( Line oLineFromPlayList );
	}

	public class SongWorker2 : SongWorker {
		readonly BaseEditor    _rgEditor;
		readonly IPgPlayerSite _oSitePlayer;

		public SongWorker2( IPgBaseSite oSiteBase, BaseEditor oEditor ) : base( oSiteBase ) {
			_oSitePlayer = oSiteBase as IPgPlayerSite ?? throw new ArgumentException( "Site must support IPgSitePlayer" );
			_rgEditor    = oEditor ?? throw new ArgumentNullException( "Need list of songs." );

			// If we fail the first song, we'll try to go to the next.
			_oDecoder = GetReader( SongFileName() );
			if( _oDecoder != null )
				_oPlayer = GetPlayer( _oPlayer, _oDecoder.Spec );
		}


		public override bool SongMoveNext() { 
			using( BaseEditor.Manipulator oManip = _rgEditor.CreateManipulator() ) {
				oManip.LineDelete( 0 ); // BUG: Make a no-undo version.
			}

			if( _rgEditor[0].Extra == null ) {
				_oSitePlayer.OnSongClear();
				return false;
			}
			return true; 
		}

		public override string SongFileName() {
			try {
				Line oLine = _rgEditor[0];

				_oSitePlayer.OnSongPlay( oLine );

				return oLine.Extra.ToString();
			} catch( NullReferenceException ) {
				_oSiteBase.LogError( "Player", "Internal problem retrieving song info" );
				return string.Empty;
			}
		}
	}

	public delegate void SongEvent();
	public delegate void SongStopped( int iAlbum, int iSong );

	//[ServiceContract]  
    public interface IMusicServer  
    {  
		//[OperationContract]
  //      [WebGet]  
        string Play( string music, string songindex ); 
		
		//[OperationContract]
  //      [WebGet]  
        string Stop();  
		
		//[OperationContract]
  //      [WebGet]  
        string Pause();  
 		
		//[OperationContract]
  //      [WebGet]  
        string Start();  
	}

	//[ServiceBehavior(
	//	InstanceContextMode=InstanceContextMode.Single )
	//]
	public class MusicCollection :
		IPgParent,
		IPgLoad<TextReader>,
		IPgSave<TextWriter>,
	  //IHttpHandler,
		IMusicServer,
		IDisposable 
	{
		protected readonly IPgBaseSite       _oSiteBase;
		protected readonly IPgFileSite       _oSiteFile;
		protected readonly IPgGrammers       _oGrammars;
		protected readonly IPgScheduler      _oScheduler;
		protected readonly IPgSound          _oSound;
		protected readonly IPgRoundRobinWork _oWorkPlace;
		protected readonly IPgRoundRobinWork _oTaskPlace;
		protected readonly Grammer<char>	 _oGrammarM3u;
		protected readonly MusicDocNowSlot   _oArtSlot;

		public Editor       Albums      { get; }
		public Editor       PlayList    { get; }
		public ImageSoloDoc IconSpeaker { get; }
		public ImageSoloDoc IconSettings{ get; }
		public ImageSoloDoc AlbumArtNow { get; }

		private SongCredentials _oSongCurrent     = null;
		private string          _strBaseDirectory = string.Empty;
		private int             _iBatch           = -1; // So we can delete a whole album from playlist.
	//  private ServiceHost     _oHost;

		private static readonly string     _strIconSpeaker  = @"MusicWalker.Content.icons8-voice-48.png";
		private static readonly string     _strIconSettings = @"MusicWalker.Content.icons8-radio-48.png";
		private static readonly HttpClient _oClient         = new HttpClient(); 

		private readonly Dictionary<string, PlayerProxy> _rgPlayerProxy = new Dictionary<string, PlayerProxy>();
		private readonly List<Task>                      _rgTasks       = new List<Task>();

		public class Illuminated : Editor {
			public Illuminated( IPgBaseSite oBaseSite ) : base( oBaseSite ) { 
			}

			public override WorkerStatus PlayStatus => ((MusicCollection)_oSiteBase.Host).PlayStatus;
		}

		public delegate void FirstDelgate  ();
		public delegate bool SecondDelegate( string Album, int StartSong );

		public abstract class PlayerProxy {
			public FirstDelgate	   xStop;
			public FirstDelgate	   xStart;
			public FirstDelgate	   xPause;
			public SecondDelegate  xQueue;
			public SecondDelegate  xPlay;
		}

		public class PlayerLocal : PlayerProxy {
			public PlayerLocal( MusicCollection oHost ) {
				if( oHost == null )
					throw new ArgumentNullException();

				xStop  = new FirstDelgate  ( oHost.PlayStop );
				xStart = new FirstDelgate  ( oHost.PlayStart );
				xPause = new FirstDelgate  ( oHost.PlayPause );
				xQueue = new SecondDelegate( oHost.PlayQueue );
				xPlay  = new SecondDelegate( oHost.PlayPlay );
			}
		}

		public class PlayerRemote : PlayerProxy {
			public PlayerRemote( MusicCollection oHost ) {
				if( oHost == null )
					throw new ArgumentNullException();

				xStop  = new FirstDelgate  ( oHost.RemoteStop  );
				xStart = new FirstDelgate  ( oHost.RemoteStart );
				xPause = new FirstDelgate  ( oHost.RemotePause );
				xPlay  = new SecondDelegate( oHost.RemotePlay  );
			}
		}

		protected class MusicDocSlot :
			IPgBaseSite
		{
			protected readonly MusicCollection _oHost;

			public MusicDocSlot( MusicCollection oHost ) {
				_oHost = oHost ?? throw new ArgumentNullException();
			}

			public IPgParent Host => _oHost;

			public void LogError(string strMessage, string strDetails, bool fShow=true) {
				_oHost.LogError( strMessage, strDetails, fShow );
			}

			public void Notify( ShellNotify eEvent ) {
				// Might want this value when we close to save the current playing list!!
			}
		}

		protected class MusicDocNowSlot : MusicDocSlot {
			ImageSoloDoc _oGuest;

			public int AlbumIndex { get; private set; } = -1;

			public MusicDocNowSlot( MusicCollection oHost ) : base( oHost ) {
			}

			/// <exception cref="ArgumentNullException" />
			public ImageSoloDoc Guest { 
				set { 
					if( _oGuest != null )
						throw new InvalidOperationException();

					_oGuest = value ?? throw new ArgumentNullException(); 
				} 
			}
			
			/// <summary>
			/// In the future we want to scale the album art, and save that version.
			/// </summary>
			/// <param name="iIndex"></param>
			public void Load( SongCredentials oSong ) {
				try {
					if( oSong == null ) {
						_oGuest.Raise_Clear();
						AlbumIndex = -1;
						return;
					}

					if( oSong.AlbumIndex == AlbumIndex )
						return;

					FileInfo oAlbumLineInfo = (FileInfo)_oHost.Albums[oSong.AlbumIndex].Extra;
					string   strFileName    = Path.Combine( oAlbumLineInfo.DirectoryName, "album.jpg" );

					using( Stream oReader = new FileStream( strFileName, FileMode.Open ) ) {
						_oGuest.Load( oReader );
					}
					AlbumIndex = oSong.AlbumIndex;
				} catch( Exception oEx ) {
					if( _oHost.ErrorsStandardFile.IsUnhandled( oEx ) )
						throw;
				}
			}
		}

		protected class MusicIterSlot : 
			MusicDocSlot,
			IPgPlayerSite
		{
			public MusicIterSlot(MusicCollection oHost) : base(oHost) {}

			public void OnSongClear() {
				try {
					_oHost.Albums.HighLight   = null;
					_oHost.SongCurrent        = null;
					_oHost.PlayList.HighLight = null;
				} catch( NullReferenceException ) {
					LogError( "player", "Trouble logging finished songs", true );
				}
			}

			public void OnSongPlay( Line oLine ) {
				try {
					 SongCredentials oSong = oLine.Extra as SongCredentials;

					_oHost.Albums.HighLight   = _oHost.Albums[oSong.AlbumIndex];
					_oHost.SongCurrent        = oSong;
					_oHost.PlayList.HighLight = oLine;
				} catch( Exception oEx ) {
					Type[] rgErrors = { typeof( NullReferenceException ), 
										typeof( InvalidCastException ) };
					if( rgErrors.IsUnhandled( oEx ) )
						throw oEx;

					LogError( "player", "Trouble logging playing songs", true );
				}
			}
		}

		public MusicCollection( IPgBaseSite oSite ) {
			_oSiteBase  = oSite ?? throw new ArgumentNullException();
			_oSiteFile  = oSite    as IPgFileSite  ?? throw new ArgumentException( "Document requeires IPgFileSite" );
			_oGrammars  = Services as IPgGrammers  ?? throw new ArgumentException( "Document requires IPgGrammerProvider" );
			_oSound     = Services as IPgSound     ?? throw new ArgumentException( "Document requires IPgSound");
            _oScheduler = Services as IPgScheduler ?? throw new ArgumentException( "Document requries IPgScheduler" );
            _oWorkPlace = _oScheduler.CreateWorkPlace() ?? throw new InvalidOperationException( "Couldn't create a worksite from scheduler.");
            _oTaskPlace = _oScheduler.CreateWorkPlace() ?? throw new InvalidOperationException( "Couldn't create a worksite from scheduler.");

			_oArtSlot   = new MusicDocNowSlot( this );

			try {
				_oGrammarM3u = (Grammer<char>)_oGrammars.GetGrammer( "m3u" );
			} catch( Exception oEx ) {
				Type[] rgErrors = { typeof( NullReferenceException ), 
									typeof( InvalidCastException ),
									typeof( FileNotFoundException ),
									typeof( GrammerNotFoundException ) };
				if( rgErrors.IsUnhandled( oEx ) )
					throw oEx;

				throw new InvalidOperationException( "Couldn't find the M3U Grammer.");
			}

			Albums       = new Illuminated ( new MusicDocSlot ( this ) );
			PlayList     = new Illuminated ( new MusicIterSlot( this ) );
			IconSpeaker  = new ImageSoloDoc( new MusicDocSlot ( this ) );
			IconSettings = new ImageSoloDoc( new MusicDocSlot ( this ) );
			AlbumArtNow  = new ImageSoloDoc( _oArtSlot );

			_oArtSlot.Guest = AlbumArtNow;

			_rgPlayerProxy.Add( "local",  new PlayerLocal ( this ) );
			_rgPlayerProxy.Add( "remote", new PlayerRemote( this ) );
		}

		public void Dispose() {
			_oWorkPlace.Stop();
			_oTaskPlace.Stop();

			//if( _oHost != null ) {
			//	try {
			//		_oHost.Close();
			//	} catch( Exception oEx ) {
			//		Type[] rgErrors = { typeof( InvalidOperationException ),
			//							typeof( ObjectDisposedException ),
			//						    typeof( CommunicationObjectFaultedException ),
			//							typeof( TimeoutException ) };
			//		if( rgErrors.IsUnhandled( oEx ) )
			//			throw;
			//		// BUG: Hmmm... Looks like we're not shutting this thing down properly. Getting the COFException.
			//		//      Need to figure out why.
			//	}
			//	_oHost = null;
			//}
		}

		public string FileBase => _oSiteFile.FileBase;

		/// <exception cref="InvalidOperationException" />
		public Stream GetEmbedding( string strResource ) {
			try {
				Assembly oAsm    = Assembly.GetExecutingAssembly();
				Stream   oStream = oAsm.GetManifestResourceStream( strResource );

				// It's weird, but it happens, so got to check.
				if( oStream == null )
					throw new InvalidOperationException();

				return( oStream );
			} catch( Exception oEx ) {
				Type[] rgErrors = { typeof( ArgumentNullException ),
									typeof( ArgumentException ),
									typeof( FileLoadException ),
									typeof( FileNotFoundException ),
									typeof( BadImageFormatException ),
									typeof( NotImplementedException ),
									typeof( InvalidOperationException ) };
				if( rgErrors.IsUnhandled( oEx ) )
					throw;

				LogError( "musiccollection", "Can't get embedded resource", false );

				throw new InvalidOperationException( "Problem with resource", oEx );
			}
		}
		public IPgParent Parentage => _oSiteBase.Host;
		public IPgParent Services  => Parentage.Services;

		public event SongEvent   SongEvent;
		public event SongStopped SongStopped;

		public PlayerProxy Commands {
			get {
				return _rgPlayerProxy["local"];
			}
		}

		/// <summary>
		/// Setting the song sends a SongEvent out to the listeners.
		/// </summary>
		public SongCredentials SongCurrent {
			get { return _oSongCurrent; }
			set {
				_oSongCurrent = value;

				_oArtSlot.Load( value );

				SongEvent?.Invoke();
			}
		}

		/// <summary>
		/// Raise the SongEvent. So external users can signal it.
		/// </summary>
		public void SongCurrent_Raise() {
			SongEvent?.Invoke();
		}

		public bool IsPlayListEmpty {
			get { return PlayList.ElementCount == 0; }
		}

		public void LogError( string strMessage, string strDetails, bool fShow ) {
			_oSiteBase.LogError( strMessage, strDetails, fShow );
		}

		readonly static Type[] _rgFileErrors = { 
			typeof( ArgumentNullException ),
			typeof( ArgumentException ),
			typeof( PathTooLongException ),
			typeof( NullReferenceException ),
			typeof( FileNotFoundException ),
			typeof( FileLoadException ),
			typeof( DirectoryNotFoundException ),
			typeof( IOException ),
			typeof( BadImageFormatException ),
			typeof( NotImplementedException ),
			typeof( InvalidCastException )
		};

		public Type[] ErrorsStandardFile => _rgFileErrors;

		public bool Load(TextReader oStream) {
			if( oStream == null )
				throw new ArgumentNullException();

			try {
				_strBaseDirectory = oStream.ReadLine();

				if( !Load(_strBaseDirectory) )
					return( false );

				IsDirty = false;
			} catch( Exception oEx ) {
				Type[] rgErrors = { typeof( IOException ),
									typeof( OutOfMemoryException ),
									typeof( ObjectDisposedException ),
									typeof( ArgumentOutOfRangeException ),
									typeof( ArgumentException ) };
				if( rgErrors.IsUnhandled( oEx ) )
					throw oEx;

				_oSiteBase.LogError( "musiccollection", "Couldn't read base music directory." );
				return( false );
			}
			try {
				using( Stream oReader = GetEmbedding( _strIconSpeaker )) {
					IconSpeaker.Load( oReader );
				}
				using( Stream oReader = GetEmbedding( _strIconSettings )) {
					IconSettings.Load( oReader );
				}
			} catch( InvalidOperationException ) {
				// something blew up, but it's not a big deal.
			}
			return( true );
		}

		/// <summary>
		/// Read from the base directory looking for all "m3u" files below.
		/// </summary>
		/// <param name="strStart">Top level directory to load files from.</param>
		/// <returns>true</returns>
		private bool Load( string strStart ) {
			if( string.IsNullOrEmpty( strStart ) )
				throw new ArgumentException();

			try {
				DirectoryInfo oDir = new DirectoryInfo( strStart );
				List<FileInfo> _rgFileInfo = new List<FileInfo>();

				foreach( FileInfo oFile in oDir.GetFiles( "*.m3u", SearchOption.AllDirectories ) ) {
					_rgFileInfo.Add( oFile );
				}

				_rgFileInfo.Sort( (oFile1, oFile2) => string.Compare( oFile1.Name, oFile2.Name ) );

				// I can't sort in the Editor because of my dumb "Lizards" solution. I'll trash that
				// and then this will be easier.
				using( Editor.Manipulator oManip = Albums.CreateManipulator() ) {
					foreach( FileInfo oFile in _rgFileInfo ) {
						if( !oFile.Attributes.HasFlag( FileAttributes.Hidden)) {
							Line oLine = oManip.LineAppend( Path.GetFileNameWithoutExtension( oFile.Name ) );
							oLine.Extra = oFile;
						}
					}
				}
			} catch( Exception oEx ) {
				Type[] rgErrors = { typeof( ArgumentNullException ),
									typeof( System.Security.SecurityException ),
									typeof( ArgumentException ),
									typeof( PathTooLongException ),
									typeof( IOException ),
									typeof( FileNotFoundException ),
									typeof( IndexOutOfRangeException ),
									typeof( ArgumentOutOfRangeException ),
									typeof( UnauthorizedAccessException ) };
				if( rgErrors.IsUnhandled( oEx ) )
					throw oEx;

				_oSiteBase.LogError( "musiccollection", "Trouble walking given directory structure: " + strStart, true );
				return( false );
			}

			try {
				// http://go.microsoft.com/fwlink/?LinkId=70353
				// netsh http add urlacl url=http://+:8000/music user=domain\username
				// Don't forget to punch a hole in the firewall too.
				uint          uiPort = 8000;
				StringBuilder sbUrl  = new StringBuilder();

				sbUrl.Append( "http://" );
				sbUrl.Append( Environment.MachineName );
				sbUrl.Append( ":" );
				sbUrl.Append( uiPort.ToString() );
				sbUrl.Append( "/music" );

			  //_oHost = new WebServiceHost(this, new Uri( sbUrl.ToString() ) );

				//ServiceMetadataBehavior smb = _oHost.Description.Behaviors.Find<ServiceMetadataBehavior>();
				//if (smb == null) {
				//	smb = new ServiceMetadataBehavior();
				//	_oHost.Description.Behaviors.Add(smb);
				//}
				//smb.HttpGetEnabled = true;

			  //_oHost.Open();
			} catch( Exception oEx ) {
				Type[] rgErrors = { typeof( InvalidOperationException ),
							      /*typeof( AddressAccessDeniedException ), // This happens if don't register URL
									typeof( AddressAlreadyInUseException )*/ }; 

				if( rgErrors.IsUnhandled( oEx ) )
					throw oEx;

				_oSiteBase.LogError( "musiccollection", "Couldn't start WebServiceHost. Running w/o web service.", true );
			}
			return( true );
		}

		/// <summary>
		/// This is an interesting case. It really doesn't make sense since we walk directories to find
		/// the music. We don't have any way add music. 
		/// </summary>
		/// <returns></returns>
		public bool InitNew() {
			LogError( "musiccollection", "This object requires a base directory for the music collection", true );
			return( false );
		}

		public bool Save(TextWriter oStream) {
			if( oStream == null )
				throw new ArgumentNullException();

			try {
				oStream.WriteLine( _strBaseDirectory );
				return( true );
			} catch( Exception oEx ) {
				Type[] rgErrors = { typeof( IOException ),
									typeof( OutOfMemoryException ),
									typeof( ObjectDisposedException ),
									typeof( ArgumentOutOfRangeException ),
									typeof( ArgumentException ) };
				if( rgErrors.IsUnhandled( oEx ) )
					throw oEx;

				_oSiteBase.LogError( "musiccollection", "Couldn't read base music directory." );
				return( false );
			}
		}

		public bool IsDirty { get; private set; } = false;

		/// <summary>
		/// When commanded by the remote, we have to search the album versus pointing right
		/// at it like when we're local. It's a bummer but a small thing. We do this so the
		/// UI can command either the remote box or this box.
		/// </summary>
		public bool PlayQueue( string strAlbum, int iStart ) {
			// Since the albums are sorted we could b-search.
			foreach( Line oLine in Albums ) {
				if( oLine.CompareTo( strAlbum ) == 0 ) {
					PlayQueue( oLine, iStart );
					return true;
				}
			}

			return false;
		}

		/// <summary>
		/// Load up any 'ol album we're passed. It might not be the currently displayed album
		/// so we've got to load it up. 
		/// </summary>
		/// <remarks>This is code that potentially be run in a task if the parser is taking
		/// too much time. My current computer is pretty fast and I'm not noticing anything.
		/// </remarks>
		public bool PlayQueue( Line oAlbum, int iStartSong ) {
			try {
				FileInfo oAlbumFileInfo = (FileInfo)oAlbum.Extra;

				// It's a shame we can't take advantage of the current album selected in the UI
				// because we might be getting our command from the remote.
				using( Editor oQueueAlbum = new Editor( new MusicDocSlot( this ) ) ) {
					using( TextReader oReader = new StreamReader( oAlbumFileInfo.FullName ) ) {
						oQueueAlbum.Load( oReader );
					}

					ParseSimpleText oParser = new ParseSimpleText( oQueueAlbum, _oGrammarM3u );

					if( oParser.Parse() ) {
						QueueSongs( oAlbum, oQueueAlbum, iStartSong );
					}
				}
			} catch( Exception oEx ) {
				Type[] rgErrors = { typeof( InvalidCastException ),
									typeof( ArgumentException ),
									typeof( ArgumentNullException ),
									typeof( NullReferenceException ),
									typeof( IOException ) };
				if( rgErrors.IsUnhandled( oEx ) )
					throw;

				return false;
			}
			return true;
		}

		/// <summary>
		/// The old way where we loaded the songs by simple line break.
		/// </summary>
		/// <param name="iAlbum">Album line offset in library.</param>
		/// <param name="oAlbumFileInfo">Directory info for the album.</param>
		/// <param name="rgSongs">Editor containing the songs.</param>
		/// <param name="iStartSong">Which line to start at.</param>
		protected void QueueSongsFlat( int iAlbum, FileInfo oAlbumFileInfo, Editor rgSongs, int iStartSong ) {
			// Right now, assume rgSongs is a clean m3u file...load up the playlist. 
			using ( BaseEditor.Manipulator oBulk = PlayList.CreateManipulator() ) {
				foreach( Line oLineSong in rgSongs ) {
					try {
						if( oLineSong.At >= iStartSong ) {
							Line oLine = oBulk.LineAppendNoUndo( oLineSong.ToString() );
					
							// NOTE: This won't work if there is any non relative path info in rgSongs
							oLine.Extra = new SongCredentials( Path.Combine( oAlbumFileInfo.DirectoryName, 
																			 oLineSong.ToString() ),
															   iAlbum, 
															   oLineSong.At,
															   _iBatch );
						}
					} catch( Exception oEx ) {
						Type[] _rgErrors = { typeof( NullReferenceException ),
											 typeof( ArgumentException ),
											 typeof( ArgumentNullException ),
											 typeof( PathTooLongException ),
											 typeof( InvalidCastException ) };
						if( _rgErrors.IsUnhandled( oEx ) )
							throw oEx;

						_oSiteBase.LogError( "player", "Couldn't play a song in your selection" );
					}
				}
			}
		}

		/// <summary>
		/// Que up a list of songs scraped from the editor's parse pass.
		/// If the song does not have a full path name, the directory location 
		/// of the editor will be used. Of course, if we are embedded, we won't
		/// have a file path and the player will complain and skip the song.
		/// TODO: Make a version that takes selection and loads selected songs.
		/// </summary>
		/// <remarks>There's a tiny possibility the player finishes playing by the time we
		/// finish all this work. Might need to send a play event.</remarks>
		protected void QueueSongs( Line oLineAlbum, Editor rgSongs, int iStartSong ) {
			if( oLineAlbum == null )
				throw new ArgumentNullException( "Album must not be null" );
			if( rgSongs == null )
				throw new ArgumentNullException( "Songs must not be null" );

			FileInfo oAlbumFileInfo;

			try {
				oAlbumFileInfo = (FileInfo)oLineAlbum.Extra;
			} catch( InvalidCastException ) {
				_oSiteBase.LogError( "player", "Ooops you broke the Album Extra info" );
				return;
			}

			_iBatch++;

			// We'll expect parsing to occur outside of here.
			using ( BaseEditor.Manipulator oBulk = PlayList.CreateManipulator() ) {
				foreach( Line oLineSong in rgSongs ) {
					if( oLineSong.At < iStartSong )
						continue;

					oLineSong.FindSong( delegate (IMemoryRange oElem) { 
											try {
												string strSong = oLineSong.SubString( oElem.Offset, oElem.Length );
												string strName = Path.GetFileName( strSong );

												if( strName.Length == strSong.Length )
													strSong = Path.Combine( oAlbumFileInfo.DirectoryName, strSong );

												Line oLine = oBulk.LineAppendNoUndo( oLineSong.ToString() );
					
												oLine.Extra	= new SongCredentials( strSong, oLineAlbum.At, oLineSong.At, _iBatch );
											} catch( Exception oEx ) {
												Type[] _rgErrors = { typeof( NullReferenceException ),
																	 typeof( ArgumentException ),
																	 typeof( ArgumentNullException ),
																	 typeof( PathTooLongException ),
																	 typeof( InvalidCastException ) };
												if( _rgErrors.IsUnhandled( oEx ) )
													throw oEx;

												_oSiteBase.LogError( "player", "Couldn't play a song in your selection" );
											}
										},
										"single" );
				}
			}
		}

		public WorkerStatus PlayStatus => _oWorkPlace.Status;

		public void PlayStop() {
			SongCredentials oCurrent = SongCurrent;

			_oWorkPlace.Stop();

			PlayList.Clear();
			Albums.HighLight = null;
			SongCurrent      = null;

			if( oCurrent != null )
				SongStopped?.Invoke( oCurrent.AlbumIndex, oCurrent.SongIndex );
		}

		public void PlayPause() {
			_oWorkPlace.Pause();

			Albums.HighLight_Raise();
			SongCurrent_Raise();
		}

		public bool PlayPlay( string strAlbum, int iStartSong ) {
			// TODO: Might be still playing last song, but play list is empty. Need to check that.
			if( IsPlayListEmpty ) {
				PlayQueue( strAlbum, iStartSong );
			}
			PlayStart();
			return true;
		}
		
		public void PlayStart() {
			switch( _oWorkPlace.Status ) {
				case WorkerStatus.FREE:
					if( !IsPlayListEmpty ) {
						try {
							_oWorkPlace.Queue( new SongWorker2( new MusicIterSlot( this ), PlayList ), 0 );
						} catch( Exception oEx ) {
							Type[] _rgErrors = { typeof( ArgumentException ),
												 typeof( ArgumentNullException ),
												 typeof( InvalidOperationException ) };
							if( _rgErrors.IsUnhandled( oEx ) )
								throw;

							_oSiteBase.LogError( "player", "Unable to queue up current request." );
							Albums.HighLight = null;
							SongCurrent      = null;
						}
					}
					break;
				case WorkerStatus.PAUSED:
					_oWorkPlace.Start( 0 );
					Albums.HighLight_Raise();
					SongCurrent_Raise();
					break;
				case WorkerStatus.BUSY:
					_oSiteBase.LogError( "player", "Sound is already playing" );
					break;
			}
		}

		public bool IsReusable => true;

		protected bool Matches( Uri uri, out Dictionary<string,string> rgDictionary ) {
			// "player?play={m3u}"
			throw new NotImplementedException();
		}

		protected bool QueueM3u( string m3u ) {
			throw new NotImplementedException();
		}

		//public void ProcessRequest(HttpContext context) {
		//	Uri uri = context.Request.Url;
		//	// compare URI to resource templates and find match
		//	if( Matches( uri, out Dictionary<string, string> vars )) {
		//		string m3u = vars["play"];

		//		switch (context.Request.HttpMethod)	{
		//			case "POST":
		//				if( QueueM3u( m3u )) {
		//					context.Response.ContentType = "text/plain; charset=UTF-8";
		//					context.Response.StatusCode  = 201;
		//					using( StreamWriter oWriter = new StreamWriter( context.Response.OutputStream, Encoding.UTF8 ) ) {
		//						oWriter.Write( "hello" );
		//					}
		//				}
		//				break;
		//			default:
		//				context.Response.ContentType = "text/plain; charset=UTF-8";
		//				context.Response.StatusCode  = 201;
		//				using( StreamWriter oWriter = new StreamWriter( context.Response.OutputStream, Encoding.UTF8 ) ) {
		//					oWriter.Write( "Operation Not Allowed" );
		//				}
		//				break;
		//		}
		//	}
		//}

		string IMusicServer.Play( string music, string songindex ) {
			string strDecodedMusic = HttpUtility.HtmlDecode( music );
			
			if( !int.TryParse( songindex, out int iIndex ) )
				iIndex = 0;

			switch( _oWorkPlace.Status ) {
				case WorkerStatus.FREE:
					PlayQueue( strDecodedMusic, iIndex );
					PlayStart();
					return "OK : Queued";
				case WorkerStatus.BUSY:
					return "OK : Playing";
				case WorkerStatus.PAUSED:
					PlayStart();
					return "OK";
			}

			return "OOPS";
		}

		string IMusicServer.Stop() {
			PlayStop();
			return "OK";
		}

		string IMusicServer.Pause() {
			switch( _oWorkPlace.Status ) {
				case WorkerStatus.PAUSED:
					return "OK";
				case WorkerStatus.BUSY:
					PlayPause();
					return "OK";
				case WorkerStatus.FREE:
					return "Empty";
			}
			return( "OOPS" );
		}

		string IMusicServer.Start() {
			switch( _oWorkPlace.Status ) {
				case WorkerStatus.PAUSED:
					PlayStart();
					return "OK";
				case WorkerStatus.BUSY:
					return "OK";
				case WorkerStatus.FREE:
					return "Empty";
			}
			return( "OOPS" );
		}

		/// <summary>
		/// This is a pre 'async' keyword implentation of a async task monitor. This is how we
		/// used to send music commands to net clients and wait for results. This could be
		/// re-written. But I never got the net client thing working much anyway.
		/// </summary>
		/// <returns>Returns the time in ms to wait for next call.</returns>
		public IEnumerator<int> EnumWatchTask() {
			while( true ) {
				do {
					int iIndex = _rgTasks.FindIndex( oTask => oTask.Status == TaskStatus.RanToCompletion );

					if( iIndex < 0 )
						break;

					if( _rgTasks[iIndex] is Task<string> oTaskString ) {
						LogError( "test", oTaskString.Result, true );
					}
					_rgTasks[iIndex].Dispose();
					_rgTasks.RemoveAt( iIndex );
				} while( true );

				if( _rgTasks.Count <= 0 )
					yield break;

				yield return 250;
			}
		}

		protected void TaskAdd( Task oTask ) {
			_rgTasks.Add( oTask );
			if( _oTaskPlace.Status == WorkerStatus.FREE )
				_oTaskPlace.Queue( EnumWatchTask(), 100 );
		}

		public bool RemotePlay( string strAlbum, int iStartSong ) {
			StringBuilder sbUrl = new StringBuilder();

			sbUrl.Append( "http://localhost:8000/music/play?music=" );
			sbUrl.Append( HttpUtility.HtmlEncode( strAlbum ) );
			sbUrl.Append( "&songindex=" );
			sbUrl.Append( HttpUtility.HtmlEncode( iStartSong.ToString() ) );

			// ContinueWith() runs in the background thread.
			TaskAdd( _oClient.GetStringAsync( sbUrl.ToString() ) );

			return true;
		}

		public void Remote( string strCommand ) {
			TaskAdd( _oClient.GetStringAsync( "http://localhost:8000/music/" + strCommand ) );
		}

		public void RemoteStop() {
			Remote( "stop" );
		}

		public void RemotePause() {
			Remote( "pause" );
		}

		public void RemoteStart() {
			Remote( "start" );
		}
	}
}
