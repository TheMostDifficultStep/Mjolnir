using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Play.Interfaces.Embedding;
using Play.Parse.Impl;
using Play.Edit;
using Play.ImageViewer;

namespace Play.MusicWalker {
	// move this to a MusicSupport.cs module.
	[Obsolete]public class SongEntryExpanded : SongEntry {
		readonly string _strExpandedSongPath;

		public SongEntryExpanded( Line oLine, IMemoryRange oElem, string strBaseDir ) : base( oLine, oElem ) {
			_strExpandedSongPath = Expand( strBaseDir );
		}

		public override string ToString() {
			return( _strExpandedSongPath );
		}

		protected string Expand( string strPlayDir ) {
			try {
				string strSong    = base.ToString();
				string strSongDir = Path.GetDirectoryName( strSong );

				if (string.IsNullOrEmpty(strSongDir) ) {
					return( Path.Combine( strPlayDir, strSong ) );
				}
				return( strSong );
			} catch( NullReferenceException ) {
				return( string.Empty );
			}
		}
	}

	public class EditorWithMusic : Editor {
		protected readonly IPgScheduler      _oScheduler;
		protected readonly IPgRoundRobinWork _oWorker;

		public class SongWorker1 : SongWorker {
			readonly IEnumerator<SongEntry> _iterSongs;
			readonly IPgSiteHilight         _oSiteHighlight;

			public SongWorker1( IPgBaseSite oSiteBase, ICollection<SongEntry> rgSongs ) : base( oSiteBase ) {
				_oSiteHighlight = oSiteBase as IPgSiteHilight ?? throw new ArgumentException( "Site must support IPgSiteHilight" );
				if( rgSongs == null )
					throw new ArgumentNullException( "Need list of songs." );

				_iterSongs = rgSongs.GetEnumerator() ?? throw new ArgumentException( "Couldn't get iterator from song collection." );

				if( !GetNextSong() )
					throw new ArgumentException( "Couldn't play any of the songs" );
			}

			public override bool SongMoveNext() { 
				if( !_iterSongs.MoveNext() ) {
					_oSiteHighlight.OnLineClear();
					return false;
				}
				return true;
			}

			public override string SongFileName() { 
				_oSiteHighlight.OnLineCurrent( _iterSongs.Current.Line, _iterSongs.Current.Elem );

				return _iterSongs.Current.ToString();
			}
		}

        public EditorWithMusic( IPgBaseSite oSite ) : base( oSite ) {
			_oScheduler = Services as IPgScheduler ?? throw new ArgumentException( "Host must implement IPgScheduler" );
			_oWorker    = _oScheduler.CreateWorkPlace();
        }

		public Line GetCurrentSong {
			get { return HighLight; }
		}

		public override WorkerStatus PlayStatus => _oWorker.Status;

		public void PlayStop() {
			_oWorker.Stop();

			HighLight = null;
		}

		public void PlayPause() {
			_oWorker.Pause();

			HighLight_Raise();
		}

		/// <summary>
		/// Used to grab the song from the parse, but now we just read line by line.
		/// TODO: Make a version that takes selection and loads selected songs.
		/// </summary>
		public void QueueSongs( Line oStart ) {
			List<SongEntry> rgPlay = new List<SongEntry>();
			bool            fLoad  = false;

			try {
				foreach( Line oLine in this ) {
					if( oLine.At == oStart.At )
						fLoad = true;
					if( fLoad  ) {
						//oLine.FindSong( delegate( IMemoryRange oElem ) { 
						//					rgPlay.Add( new SongEntryExpanded(oLine, oElem, _oSiteFile.FilePath) ); 
						//				},
						//				"single");
						rgPlay.Add( new SongEntryExpanded( oLine, 
														   new ColorRange( 0, oLine.ElementCount, -1 ), 
														   _oSiteFile.FilePath) );
					}
				}
			} catch( Exception oEx ) {
				Type[] _rgErrors = { typeof( NullReferenceException ),
									 typeof( ArgumentException ),
									 typeof( ArgumentNullException ),
									 typeof( PathTooLongException ) };
				if( !_rgErrors.Contains( oEx.GetType() ) )
					throw;

				_oSiteBase.LogError( "sound", "Unable to find songs.", true );
				return;
			}


			try {
				_oWorker.Queue( new SongWorker1( new DocSlot( this ), rgPlay ), 0 );
			} catch( ArgumentException ) {
				_oSiteBase.LogError( "sound", "Unable to queue up current request." );
				HighLight = null;
			}
		}

		public void Play( Line oStart ) {
			switch( _oWorker.Status ) {
				case WorkerStatus.FREE:
					QueueSongs( oStart );
					break;
				case WorkerStatus.PAUSED:
					_oWorker.Start( 0 );
					HighLight_Raise();
					break;
				case WorkerStatus.BUSY:
					_oSiteBase.LogError( "sound", "Sound is already playing" );
					break;
			}
		}

		public override void Dispose() {
			_oWorker.Stop(); // TODO: Check our site is removed from round robin.
		}
	}

	/// <summary>
	/// Editor which supports album art.
	/// </summary>
	/// <remarks>Used to implement from EditorWithParser. Now untangled it still works but no colorization atm.</remarks>
	public class M3UDocument : EditorWithMusic {
		public ImageWalkerDir AlbumArt        { get; }
		public PropDoc        AlbumProperties { get; }

        public M3UDocument( IPgBaseSite oSite ) : base( oSite ) {
			AlbumArt        = new ImageWalkerDir( new DocSlot( this ) ); // Note: ImageWalker needs a better IPgFileSite implementation.
			AlbumProperties = new PropDoc       ( new DocSlot( this ) ); //       need to subclass this DocSlot.
        }

		private void AlbumPropertiesLoad() {
			string strFullPath = string.Empty;
			try {
				strFullPath = Path.Combine( FilePath, "album-properties.txt" );

				using( TextReader oReader = new StreamReader( strFullPath ) ) {
					AlbumProperties.Load( oReader );
				}
			} catch( Exception oEx ) {
				Type[] rgErrors = { typeof( ArgumentNullException ),
									typeof( ArgumentException ),
									typeof( PathTooLongException ),
									typeof( NullReferenceException ),
									typeof( FileNotFoundException ),
									typeof( DirectoryNotFoundException ),
									typeof( IOException ) };
				if( rgErrors.IsUnhandled( oEx ))
					throw;

				_oSiteBase.LogError( "internal", "Couldn't initialize album property document contents for : " + strFullPath, false );
			}
		}

		private void AlbumArtLoad() {
			string strFullPath = string.Empty;
			try {
				strFullPath = Path.Combine( FilePath, "album.jpg" );
				// Ok to not load art if not found. We'll still show default icon.
				// Unless the load fails really bad. Would expect an exception in that case.
				AlbumArt.LoadURL( strFullPath );
			} catch( Exception oEx ) {
				Type[] rgErrors = { typeof( ArgumentNullException ),
									typeof( ArgumentException ),
									typeof( PathTooLongException ),
									typeof( NullReferenceException ) };
				if( rgErrors.IsUnhandled( oEx ))
					throw;

				_oSiteBase.LogError( "internal", "Couldn't initialize album art document contents for : " + strFullPath, false );
			}
		}

		public override bool InitNew() {
			if( !base.InitNew() )
				return false;

			if( !AlbumArt.InitNew() )
				return false;

			if( !AlbumProperties.InitNew() )
				return false;

			return( true );
		}

		public override bool Load(TextReader oReader) {
			if( !base.Load(oReader) )
				return( false );

			AlbumArtLoad();
			AlbumPropertiesLoad();

			return( true );
		}

		public override void Dispose() {
			base.Dispose();
		}
	}

}
