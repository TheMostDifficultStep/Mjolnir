using System;
using System.Collections.Generic;
using System.Linq;

using Play.Interfaces.Embedding;
using Play.Pcm;

namespace Play.Integration {
	/// <summary>
	/// This is a worker enumerator that can be used to generate Morse code from a text file.
	/// Current, is the time in milliseconds until the next call must be made.
	/// </summary>
	public class MorseGenerator : IEnumerator<int> {
		readonly IPgBaseSite   _oSiteBase;
		readonly IPgPlayer     _oPlayer;
		readonly GenerateMorse _oDecoder;

		uint _uiWait = 0;
		bool _iRedundantDispose = false; // To detect redundant calls

		/// <exception cref="ArgumentNullException" />
		/// <exception cref="InvalidOperationException" >This can happen for a variety of reasons, check the inner exception.</exception>
		public MorseGenerator( IPgBaseSite oSiteBase ) {
			_oSiteBase = oSiteBase ?? throw new ArgumentNullException( "Need a site with hilight." );

			IPgSound      oSound = oSiteBase.Host.Services as IPgSound ?? throw new ArgumentNullException( "Host requires IPgSound." );
			Specification oSpec  = new Specification( 44100, 1, 0, 16 );

			_oPlayer  = oSound.CreateSoundPlayer( oSpec );
			_oDecoder = new GenerateMorse( oSpec, 800 );
		}

		public void Dispose() {
			if( _iRedundantDispose )
				return;

			if( _oPlayer != null )
				_oPlayer.Dispose();
			if( _oDecoder != null )
				_oDecoder.Dispose();

			_iRedundantDispose = true;
		}

		/// <summary>
		/// Assign some character enumerator here and we'll morse it!
		/// </summary>
		public IEnumerable<char> Signal {
			set {
				_oDecoder.Signal = value;
			}
		}

		public object Current => throw new NotImplementedException();
		public void   Reset() => throw new NotImplementedException();

		/// <summary>
		/// Returns the recommended time in milliseconds to "sleep", or do something else.
		/// </summary>
		int IEnumerator<int>.Current {
			get{ return (int)_uiWait; }
		}

		public bool MoveNext() {
			try {
				_uiWait = ( _oPlayer.Play( _oDecoder ) >> 1 ) + 1;
			} catch( NullReferenceException ) {
				_oSiteBase.LogError( "player","Problem with coder" );
			}

			return _oPlayer.Busy > 0;
		}

		private IPgPlayer GetPlayer( IPgSound oSound, Specification oSpec ) {
			try {
				return oSound.CreateSoundPlayer( oSpec );
			} catch( Exception oEx ) {
				Type[] rgErrors = { typeof( ArgumentException ),
									typeof( ArgumentNullException ),
									typeof( BadDeviceIdException ),
									typeof( InvalidHandleException ),
									typeof( MMSystemException ),
									typeof( NullReferenceException ) };
				if( !rgErrors.Contains( oEx.GetType() ) )
					throw;

				_oSiteBase.LogError( "sound", "Couldn't handle sound spec" );

				throw new InvalidOperationException( "Can't handle sound request.", oEx );
			}
		}
	}
}
