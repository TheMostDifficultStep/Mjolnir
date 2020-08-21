using System;

using Play.Interfaces.Embedding;
using Play.Edit;

namespace Play.MusicWalker {
	class PlayListWindow : EditWin {
		public Editor SongProperties { get; }
		public Editor ListOutline    { get; }

		readonly MusicCollection _oMusicDoc;

		public class CurrentAlbum : Editor {
			public CurrentAlbum(IPgBaseSite oSite) : base(oSite) {
			}

			public override WorkerStatus PlayStatus => ((PlayListWindow)_oSiteBase.Host).PlayStatus;
		}

		//protected class PLDocSlot :
		//	IPgBaseSite
		//{
		//	protected readonly PlayListWindow _oHost;

		//	public PLDocSlot( PlayListWindow oHost ) {
		//		_oHost = oHost ?? throw new ArgumentNullException();
		//	}

		//	public IPgParent Host => _oHost;

		//	public void LogError(string strMessage, string strDetails, bool fShow=true) {
		//		_oHost.LogError( strMessage, strDetails );
		//	}

		//	public void Notify( ShellNotify eEvent ) {
		//	}
		//} // End class

		public PlayListWindow( IPgViewSite oSite, MusicCollection oDoc ) : base( oSite, oDoc.PlayList ) {
			_oMusicDoc = oDoc ?? throw new ArgumentNullException( "Document must not be null." );

			_oMusicDoc.SongEvent += DocSongEvent;

			SongProperties = new Editor      ( new DocSlot( this ) );
			ListOutline    = new CurrentAlbum( new DocSlot( this ) );
		}

		/// <summary>
		/// If Play or Pause occurs, we need to update the color of our selected line.
		/// </summary>
		private void DocSongEvent() {
			Invalidate();
			ListOutline.HighLight_Raise();
		}

		protected override bool InitNewInternal() {
			if( !base.InitNewInternal() )
				return false;

			LoadAlbumSummery();

			return true;
		}

		protected override void Raise_Navigated( NavigationSource eSource, ILineRange oCarat ) {
			base.Raise_Navigated( eSource, oCarat );
			FindOutlineHilight();
			LoadSongProperties();
		}

		/// <summary>
		/// Watching events on our playlist.
		/// </summary>
		public override void OnEvent(BUFFEREVENTS eEvent) {
			base.OnEvent(eEvent);

			switch( eEvent ) {
				case BUFFEREVENTS.LOADED:
				case BUFFEREVENTS.MULTILINE:
				case BUFFEREVENTS.SINGLELINE:
					LoadAlbumSummery();
					break;
			}
		}

		protected void LoadSongProperties() {
			// Load the Song properties under the Carat.
		}

		/// <summary>
		/// Walk the play list and find the album associated with a batch.
		/// </summary>
		protected void LoadAlbumSummery() {
			int iBatchCurrent = -1;
			using( Editor.Manipulator oOutline = ListOutline.CreateManipulator() ) {
				oOutline.DeleteAll();

				foreach( Line oLine in _oMusicDoc.PlayList ) {
					if (oLine.Extra is SongCredentials oSong) {
						if (oSong.BatchID != iBatchCurrent ) {
							Line oAssocAlbum = oOutline.LineAppendNoUndo(_oMusicDoc.Albums[oSong.AlbumIndex].ToString());
							iBatchCurrent = oSong.BatchID;
							oAssocAlbum.Extra = oSong; // Borrow the song for the BatchID.
							// In the future save the batch index, and album index so we can help edit the play list.
						}
					}
				}
			}
			FindOutlineHilight();
		}

		protected void FindOutlineHilight() {
			try {
				// If no song is hilighted then we've got nuthn'
				if( _oMusicDoc.PlayList.HighLight == null ) {
					ListOutline.HighLight = null;
					return;
				}

				// Elsewise find the album associated with the current song.
				int iSongBatch = ((SongCredentials)_oMusicDoc.PlayList.HighLight.Extra).BatchID;

				foreach( Line oLineInOutline in ListOutline ) {
					int iAlbumBatch = ((SongCredentials)oLineInOutline.Extra).BatchID;

					if( iSongBatch == iAlbumBatch ) {
						ListOutline.HighLight = oLineInOutline;
					}
				}
			} catch( Exception oEx ) {
				Type[] rgErrors = { typeof( NullReferenceException ),
									typeof( InvalidCastException ) };
				if( rgErrors.IsUnhandled( oEx ) )
					throw oEx;
			}
		}

		public override object Decorate( IPgViewSite oBaseSite, Guid sGuid ) {
			if( sGuid.Equals( GlobalDecorations.Outline ) ) {
				return new EditWin( oBaseSite, ListOutline, true ) { Wrap = false };
			}
			if( sGuid.Equals( GlobalDecorations.Properties ) ) { // Bit rate and etc.
				return new EditWin( oBaseSite, SongProperties, true );
			}

            return base.Decorate( oBaseSite, sGuid );
        }

		public override bool Execute( Guid sGuid ) {
			if( sGuid == GlobalCommands.Play ) {
				if( _oMusicDoc.IsPlayListEmpty ) {
					LogError( "player", "Return to the Albums view and queue up a new playlist" );
				} else {
					_oMusicDoc.PlayStart();
				}
				ListOutline.HighLight_Raise();
				return true;
			}
			if( sGuid == GlobalCommands.Pause ) {
				_oMusicDoc.PlayPause();
				return true;
			}
			if( sGuid == GlobalCommands.Stop ) {
				_oMusicDoc.PlayStop();
				return true;
			}

			return base.Execute( sGuid );
		}
	}
}
