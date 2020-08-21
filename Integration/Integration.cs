using System;
using System.Collections.Generic;
using System.Collections;
using System.IO;
using System.Linq;

using Play.Pcm;
using Play.Parse.Impl;
using Play.Edit;
using Play.Interfaces.Embedding;

/// <summary>
/// Integration is a spot where I can put helper functions for all the standard projects
/// that depend on "htmparse", "editor", and "mp3-play". And I can keep all those libraries
/// free of excessive interdependency.
/// </summary>
namespace Play.Integration {
	public interface IPgSound {
		/// <summary>
		/// Get a sound decoder for the given file type.
		/// </summary>
		/// <param name="strFileName">File to open.</param>
		/// <returns></returns>
		/// <exception cref="FormatException" />
		/// <exception cref="FileNotFoundException" />
		/// <exception cref="InvalidOperationException" />
		IPgReader CreateSoundDecoder( string strFileName ); // TODO: Would like to make this a stream at some point.
		IPgPlayer CreateSoundPlayer ( Specification oSpec );
	}

	/// <remarks>
	/// I can toss this after I've added IMemoryRange support to SongCredentials 
	/// </remarks>
	/// <seealso cref="SongCredentials"/>
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
				if( !rgErrors.Contains( oEx.GetType() ) )
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
				if( !rgErrors.Contains( oEx.GetType() ) )
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
}
