using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Collections.Generic;

namespace Play.Sound {
	/// <summary>
	/// A little experiment with the fourier equation. This is a "naive" solution in
	/// that it's the O(n^2) version. Does not scale up. Need to get my hands on a FFT.
	/// </summary>
	public class Fourier {
		private const int    _iSamples  = 4400;
		private List<double> _rgSamples = new List<double>(_iSamples);
		public  List<double> Results { get; } = new List<double>(_iSamples);
		public  double       Max;

		/// <summary>
		/// A little experiment with the fourier equation. I generate a signal
		/// here in float -1 less than or equal x less than or equal 1.
		/// </summary>
		public Fourier() {
			double dStep = Math.PI * 2 / _rgSamples.Capacity;

			for( double rad = 0; rad < Math.PI * 2; rad += dStep ) {
				_rgSamples.Add( Math.Sin( rad * 200 ) + Math.Sin( rad * 450 ) / 2 );
				//_rgSamples.Add( Math.Sin( rad * 400 ) );
			} 
		}

		public void Sum() {
			FourierSum();
		}

		public void DCTSum() {
			double dSquareRoot = Math.Sqrt( 2 / (double)_rgSamples.Count );
			double dTwoDivN    = 2 / (double)_rgSamples.Count;
			double dDoubleSamp = 2 * _rgSamples.Count;
			int    iStep = 1; // Step MUST be 1 or we miss the frequency entirely, exp: if freq = cos( x * 400 ) (x < 2*pi)

			Max = 0;

			for( int k=1; k<_rgSamples.Count - 1; k += iStep ) {
				try {
					double X   = 0;
					double tmp = Math.PI * k / dDoubleSamp;

					for( int n=0; n<_rgSamples.Count; ++n ) {
						double rad = tmp * ( 2*n + 1 );

						X += _rgSamples[n] * Math.Cos( rad );
					}

					X = dTwoDivN * X;

					if( Max < X )
						Max = X;

					Results.Add( X );
				} catch( OverflowException ) {
				}
			}
		}

		/// <summary>
		/// Given our generated signal we run the DFT and generate the spectral
		/// results.
		/// </summary>
		public void FourierSum() {
			double dCoef = 2*Math.PI / _rgSamples.Count;
			int    iStep = 1;

			Max = 0;

		    for( int k=1; k<_rgSamples.Count/2; k += iStep ) {
		  //for( int k=440; k<460; k += iStep ) { // Tuning my radio works!! ^_^
				try {
					double X = 0;
					double Y = 0;

					for( int n=0; n<_rgSamples.Count; ++n ) {
						double rad = dCoef * k * n;

						X += _rgSamples[n] * Math.Cos( rad );
						Y += _rgSamples[n] * Math.Sin( rad );
					}
					X = X * 2 * iStep / _rgSamples.Count;
					Y = Y * 2 * iStep / _rgSamples.Count;

					double dMagn = Math.Sqrt( X * X + Y * Y );
				  //double dAngl = Math.Atan2( Y, X );

					if( Max < dMagn )
						Max = dMagn;

					Results.Add( dMagn );
				} catch( OverflowException ) {
				}
			}
		}
	}

	/// <summary>
	/// Juggling differing sizes of the decoded data and the player buffers is a pain in the
	/// rear. This class does the hard work of aligning the buffered decoded data with the
	/// requests from the players. You simply load your buffer when requested.
	/// </summary>
	/// <seealso cref="BufferReload" />
	public abstract class AbstractReader : IPgReader {
		protected byte[] _rgBuffer  = null;
		private   uint   _ulRead     = 0;
		private   uint   _ulBuffered = 0;

		public virtual void Dispose() {	}

		public abstract Specification Spec { get; }

		public bool IsReading {
			get { return _ulBuffered > 0; }
		}

		/// <summary>
		/// Return the number of bytes of sampling that will last the given time. The value be block aligned.
		/// </summary>
		/// <param name="flTime">Time in seconds.</param>
		/// <returns>Amount of samples to generate generated.</returns>
		/// <remarks>So floats are weird and we can get odd numbers even tho' we've got block align.
		/// So first round the time multiplied by the bit block rate, THEN mult by the block align size.</remarks>
		[Obsolete("Move to Specification")]public uint BytesNeeded( float flTime ) {
			return (uint)Math.Round( Spec.Rate * flTime ) * Spec.BlockAlign;
		}

		public unsafe UInt32 Read( IntPtr ipAddress, UInt32 uiBytesWanted ) {
			return( Read( ipAddress, null, 0, uiBytesWanted ) );
		}

		public unsafe UInt32 Read( byte[] rgAddress, int iStartIndex, UInt32 uiBytesWanted ) {
			return( Read( IntPtr.Zero, rgAddress, iStartIndex, uiBytesWanted ) );
        }

		/// <summary>
		/// Convert the integer into a series of bytes based on the endianess of
		/// the system it was installed under. 
		/// </summary>
		/// <param name="iValue">Integer to convert</param>
		/// <param name="bytes">Pointer to a byte array.</param>
		/// <param name="iWordSize">Word size requested.</param>
		/// <remarks>Might be fun to check for overflow.</remarks>
		/// <exception cref="IndexOutOfRangeException" >Your word size was bigger than 4.</exception>
		protected static int GetBytes( int   iValue, ref byte[] bytes, int iWordSize ) {
			int[] rgPow = { 0, 8, 16, 24 };

			if( BitConverter.IsLittleEndian ) {
				for( int i=0; i<iWordSize; ++i ) {
					bytes[i] = (byte) ((iValue >> rgPow[i] ) & 0xFF);
				}
			} else {
				for( int i=0; i<iWordSize; ++i ) {
					bytes[iWordSize-i-1] = (byte) ((iValue >> rgPow[i] ) & 0xFF);
				}
			}

			return iWordSize;
		}
		public    static int GetBytes( short sValue, ref byte[] bytes ) => GetBytes( sValue, ref bytes, 2 );
		public    static int GetBytes( int   iValue, ref byte[] bytes ) => GetBytes( iValue, ref bytes, 4 );

		/// <summary>
		/// Implement your data generator in here and you get automatic buffer alignment.
		/// </summary>
		/// <param name="uiRequest">You should load at least this much block aligned data. Size in bytes.</param>
		/// <returns>Returns how much actually buffered.</returns>
		protected abstract uint BufferReload( uint uiRequest );

		/// <summary>
		/// Read maximally the number of bytes wanted. Might return less than wanted up to 0.
		/// But why an unsafe buffer? Because the consumer of this data is an unmanaged device
		/// anyway so why hassle with anything else.
		/// </summary>
		/// <param name="ipPriAddr">Start address of unmanaged buffer.</param>
		/// <param name="uiBytesWanted">Number of bytes to copy into unmanaged buffer.</param>
		/// <returns>How many bytes actually retrieved. Can be zero, meaning the file has been completely decoded</returns>
		/// <remarks>
		/// Let's put all the nasty math of reading the decoded mp3 info chunks and copying those
		/// waveout header chucks of a different size, all in one place!! This will be great since otherwise
		/// I'll have potentially two different devices that would have to do the same math! Now I've got it
		/// all in one place. In addition, the external user doesn't need to keep track of how much of the 
		/// buffer they've read, we do that here!!
		/// 
		/// I've marked this unsafe because we're slicing and dicing buffer copies and the
		/// buffer we are copying into is unmanaged. So there is a chance I can blow up if I've got
		/// any math errors in here!</remarks>
		/// <exception cref="InvalidOperationException" />
		private unsafe UInt32 Read( IntPtr ipPriAddr, byte[] rgAltAddr, int iAltStartIndex, UInt32 uiBytesWanted ) {
			uint uiBytesConsumed = 0;

			do {
				uint uiCopy = uiBytesWanted - uiBytesConsumed;

				// If the user first enters zero bytes wanted, it's actually an argument error.
				// but next time round it definitely means we messed up somehow.
				if( uiCopy == 0 )
					throw new InvalidOperationException( "We're confused." );

				// We've exhausted the current buffer and must load with the next block.
				if( _ulRead >= _ulBuffered ) {
					try {
						_ulRead     = 0;
						_ulBuffered = BufferReload( uiCopy );
					} catch( Exception oEx ) {
						Type[] rgErrors = { typeof( InvalidOperationException ),
											typeof( NullReferenceException ) };
						if( !rgErrors.Contains( oEx.GetType() ) )
							throw;

						_ulBuffered = 0;
						return 0;
					}
				}

				// Make sure we don't try to copy outside of the loaded part of the buffer!
				if( _ulRead + uiCopy > _ulBuffered )
					uiCopy = _ulBuffered - _ulRead;

				// This shouldn't happen unless the BufferReload() comes up empty.
				if( uiCopy == 0 )
					return 0;

				try {
					if( ipPriAddr != IntPtr.Zero ) // Unsafe blit! ^_^;;
					    Marshal.Copy( _rgBuffer, (int)_ulRead, ipPriAddr + (int)uiBytesConsumed, (int)uiCopy );
					if( rgAltAddr != null )        // Safe blit! 
						Buffer.BlockCopy( _rgBuffer, (int)_ulRead, rgAltAddr, (int)uiBytesConsumed + iAltStartIndex, (int)uiCopy );
				} catch( Exception oEx ) {
					Type[] rgErrors = { typeof( ArgumentOutOfRangeException ),
										typeof( ArgumentNullException ),
										typeof( ArgumentException ),
										typeof( NullReferenceException ) };
					if( !rgErrors.Contains( oEx.GetType() ) )
						throw;

					return 0; // Actually throwing some sort of buffer copy exception would be better.
				}

				uiBytesConsumed += uiCopy;
				_ulRead         += uiCopy;
			} while( uiBytesConsumed < uiBytesWanted);

			return( uiBytesConsumed );
		}
	} // class 

	/// <summary>
	/// A "tapper" is a buffer sitting between a reader generating PCM data and it's player.
	/// This tapper is designed to store a second worth of PCM data.
	/// </summary>
	public abstract class Tapper : AbstractReader {
		IPgReader DataSource { get; }
		byte[]    DataCopy   { get; }
		int       Blocked    { get; set; }

		public Tapper( IPgReader oSource ) : base() {
			DataSource = oSource      ?? throw new ArgumentNullException();
			Spec       = oSource.Spec ?? throw new ArgumentException();

			_rgBuffer = new byte[ Spec.BlockAlign * Spec.Rate ]; // Our abstract reader's buffer
			DataCopy  = new byte[ Spec.BlockAlign * Spec.Rate ]; // The copy we want to keep.
		}

		public override Specification Spec { get; }

		protected override uint BufferReload(uint uiRequest) {
			uint uiRead = DataSource.Read( _rgBuffer, 0, (uint)_rgBuffer.Length );

			if( Blocked <= 0 ) {
				Blocked = 1;
				// I have a (possibly) second worth of PCM data! Time to party!!
				Buffer.BlockCopy( _rgBuffer, 0, DataCopy, 0, (int)uiRead );
				OnReloaded( uiRead);
				Blocked--;
			}

			return uiRead;
		}

		public abstract void OnReloaded( uint uiRead );
	}

	public class GenerateSin : AbstractReader {
		public GenerateSin( Specification oSpec, uint uiFreq ) {
			Spec = oSpec;
			GenerateBuffer( uiFreq );
		}

		public override Specification Spec { get; }

		/// <summary>
		/// Allocate and load the buffer with the signal in little endian order.
		/// This will allocate a buffer large enough for one period.
		/// </summary>
		/// <param name="uiFreq">Frequency in hertz</param>
		protected void GenerateBuffer( uint uiFreq ) {
			float power             = (float)Math.Pow( 2, 15 ) - 1;
			float flSamplesPerCycle = Spec.Rate / (float)uiFreq;
			int   iWordSize         = Spec.BitsPerSample / 8;

			_rgBuffer = new byte[(int)Math.Round( flSamplesPerCycle ) * iWordSize ];

			float  dStep = (float)Math.PI * 2 / flSamplesPerCycle;
			double rad   = 0;

			Byte[] rgBytes = new Byte[4];

			try {
				for( int i = 0; i< _rgBuffer.Length; i=i+iWordSize ) {
					short iSample = (short)(Math.Sin( rad ) * power );

					for( int j =0; j < GetBytes( iSample, ref rgBytes ); ++j ) {
						_rgBuffer[i+j] = rgBytes[j]; 
					}

					rad += dStep;
				} 
			} catch( Exception oEx ) {
				Type[] rgErrors = { typeof( NullReferenceException ),
									typeof( IndexOutOfRangeException ) };
				if( !rgErrors.Contains( oEx.GetType() ) )
					throw;

				throw new InvalidOperationException( "Trouble generating sound.", oEx );
			}
		}

		/// <remarks>
		/// Since we've generated one period of this signal, we don't need to do
		/// anything on load request. Just reuse our buffer!
		/// </remarks>
		protected override uint BufferReload( uint uiRequest ) {
			return (uint)_rgBuffer.Length;
		}
	} // end class

	public class GenerateMorse : AbstractReader {
		readonly GenerateSin                _oToneReader;
		static   Dictionary< char, string > _rgMorse;

		float _fMaxTime = 3;     // Time in seconds.
		float _fDotTime = 0.06F; // 60 milliseconds
		float _fDashTime;
		float _fBetweenSymbols;
		float _fBetweenLetters;
		float _fBetweenWords;

		IEnumerator<char> _oEnumChars;

		/// <summary>
		/// Prep us for generating morse code PCM.
		/// </summary>
		/// <param name="oSpec">WaveOut settings.</param>
		/// <remarks>http://www.codebug.org.uk/learn/step/541/morse-code-timing-rules/</remarks>
		public GenerateMorse(Specification oSpec, uint Freq = 800 ) {
			Spec = oSpec ?? throw new ArgumentNullException();

		    _fDashTime       = _fDotTime * 3;
			_fBetweenSymbols = _fDotTime;
		    _fBetweenLetters = _fDotTime * LetterSpacing; // s/b 3 dots.
		    _fBetweenWords   = _fDotTime * WordSpacing;   // s/b 7 dots.

			_oToneReader = new GenerateSin( oSpec, Freq );
			_rgBuffer    = new byte[BytesNeeded( _fMaxTime )];

			Signal = " ";

			LoadMorse();
		}

		public override void Dispose() {
			if( _oEnumChars != null ) {
				_oEnumChars.Dispose();
				_oEnumChars = null;
			}
			base.Dispose();
		}

		public uint WordSpacing   { get; set; } = 8;
		public uint LetterSpacing { get; set; } = 28;

		public override Specification Spec { get; }

		public IEnumerable<char> Signal {
			set {
				_oEnumChars = value.GetEnumerator();
			}
		}

		protected uint ReadTone( uint uiStartIndex, float flTime ) {
			return _oToneReader.Read( _rgBuffer, (int)uiStartIndex, BytesNeeded( flTime ) );
		}

		protected uint ReadBlank( uint iStartIndex, float flTime ) {
			uint uiBytesNeeded = BytesNeeded( flTime );

			for( int i=0; i<uiBytesNeeded; ++i )
				_rgBuffer[(int)iStartIndex+i] = 0;

			return uiBytesNeeded;
		}

		/// <summary>
		/// Generate a single morse character per pass.
		/// </summary>
		/// <param name="uiRequestBytes">We're ignoring this value.</param>
		/// <returns></returns>
		protected override uint BufferReload( uint uiRequestBytes ) {
			uint ulBuffered = 0;
			
			try {
				if( _oEnumChars.MoveNext() ) {
					if( _oEnumChars.Current == ' ' ) {
						ulBuffered += ReadBlank( ulBuffered, _fBetweenWords );
						return ulBuffered;
					}

					ulBuffered += ReadBlank( ulBuffered, _fBetweenLetters );

					int iSymbols = 0;
					foreach( char c in _rgMorse[Char.ToLower( _oEnumChars.Current )] ) {
						if( iSymbols++ > 0 )
							ulBuffered += ReadBlank( ulBuffered, _fBetweenSymbols );

						switch( c ) {
							case '.':
								ulBuffered += ReadTone( ulBuffered, _fDotTime );
								break;
							case '_':
							case '-':
								ulBuffered += ReadTone( ulBuffered, _fDashTime );
								break;
						}
					}
				}
			} catch( Exception oEx ) {
				Type[] rgErrors = { typeof( NullReferenceException ),
									typeof( KeyNotFoundException ) };
				if( !rgErrors.Contains( oEx.GetType() ) )
					throw;
				
				ulBuffered += ReadBlank( ulBuffered, _fBetweenWords );
			}

			return ulBuffered;
		}

		protected void LoadMorse() {
			if( _rgMorse != null )
				return;

			_rgMorse = new Dictionary<char,string>(36) {
				{ 'a',"._" },
				{ 'b',"_..." },
				{ 'c',"_._." },
				{ 'd',"_.." },
				{ 'e',"." },
				{ 'f',".._." },
				{ 'g',"--." },
				{ 'h',"...." },
				{ 'i',".." },
				{ 'j',".---" },
				{ 'k',"-.-" },
				{ 'l',".-.." },
				{ 'm',"--" },
				{ 'n',"-." },
				{ 'o',"---" },
				{ 'p',".--." },
				{ 'q',"--.-" },
				{ 'r',".-." },
				{ 's',"..." },
				{ 't',"-" },
				{ 'u',"..-" },
				{ 'v',"...-" },
				{ 'w',".--" },
				{ 'x',"-..-" },
				{ 'y',"-.--" },
				{ 'z',"--.." },

				{ '1',".----" },
				{ '2',"..---" },
				{ '3',"...--" },
				{ '4',"....-" },
				{ '5',"....." },
				{ '6',"-...." },
				{ '7',"--..." },
				{ '8',"---.." },
				{ '9',"----." },
				{ '0',"-----" },


				{ '.',".-.-.-" },
				{ ',',"--..--" },
				{ '?',"..--.." },
				{ '/',"-..-." },
				{ '@',".--.-." },
				{ '=',"-...-" }, // sort of an odd ball.

				{ ' '," " }
			};
		}
	}
}
