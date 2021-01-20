using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

///<summary>
///  Copyright (c) Dragonaur
///
///  Permission is hereby granted, free of charge, to any person obtaining
///  a copy of this software and associated documentation files (the
///  "Software"), to deal in the Software without restriction, including
///  without limitation the rights to use, copy, modify, merge, publish,
///  distribute, sublicense, and/or sell copies of the Software, and to
///  permit persons to whom the Software is furnished to do so, subject to
///  the following conditions:
///
///  The above copyright notice and this permission notice shall be
///  included in all copies or substantial portions of the Software.
///
///  THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
///  EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
///  MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
///  NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS
///  BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN
///  ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN
///  CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
///  SOFTWARE
///</summary>

using System.IO;

namespace Play.Sound {
	enum Mpg123_Errors : int {
		MPG123_OK         =  0,   /* Success */
		MPG123_DONE       = -12,  /* Message: Track ended. Stop decoding. */
		MPG123_NEW_FORMAT = -11,  /* Message: Output format will be different on next call. Note that some libmpg123 versions between 1.4.3 and 1.8.0 insist on you calling mpg123_getformat() after getting this message code. Newer verisons behave like advertised: You have the chance to call mpg123_getformat(), but you can also just continue decoding and get your data. */
		MPG123_NEED_MORE  = -10,  /* Message: For feed reader: "Feed me more!" (call mpg123_feed() or mpg123_decode() with some new input data). */
		MPG123_ERR        = -1,   /* Generic Error */
	}

	/// <summary>This is a reader which reads mp3 files and returns PCM Little Endian (intel x86) order.</summary>
	public unsafe class Mpg123 : AbstractReader {
		[DllImport ("libmpg123-0.dll")] private static extern void *        mpg123_new( char * pcDecoder, ref Mpg123_Errors iErr );
		[DllImport ("libmpg123-0.dll")] private static extern Mpg123_Errors mpg123_open( void * pMpg, byte[] rgFileName );
		[DllImport ("libmpg123-0.dll")] private static extern UInt32        mpg123_outblock( void * pMpg ); // Must use 64 bit lib.
		[DllImport ("libmpg123-0.dll")] private static extern Mpg123_Errors mpg123_getformat( void * pMpg, ref long lRate, ref int iChannels, ref int iEncoding );
		[DllImport ("libmpg123-0.dll")] private static extern Mpg123_Errors mpg123_read( void * pMpg, byte * rgBuffer, UInt32 uiBufSize, ref UInt32 uiRead );
		[DllImport ("libmpg123-0.dll")] private static extern int           mpg123_close( void * pMpg );
		[DllImport ("libmpg123-0.dll")] private static extern void          mpg123_delete( void * pMpg );
		[DllImport ("libmpg123-0.dll")] private static extern int           mpg123_encsize( int iEncoding );

		//[DllImport ("libmpg123.so.0")] private static extern void *        mpg123_new( char * pcDecoder, ref Mpg123_Errors iErr );
		//[DllImport ("libmpg123.so.0")] private static extern Mpg123_Errors mpg123_open( void * pMpg, string strFileName );
		//[DllImport ("libmpg123.so.0")] private static extern UInt32        mpg123_outblock( void * pMpg ); // Must use 64 bit lib.
		//[DllImport ("libmpg123.so.0")] private static extern Mpg123_Errors mpg123_getformat( void * pMpg, ref long lRate, ref int iChannels, ref int iEncoding );
		//[DllImport ("libmpg123.so.0")] private static extern Mpg123_Errors mpg123_read( void * pMpg, byte * rgBuffer, UInt32 uiBufSize, ref UInt32 uiRead );
		//[DllImport ("libmpg123.so.0")] private static extern int           mpg123_close( void * pMpg );
		//[DllImport ("libmpg123.so.0")] private static extern void          mpg123_delete( void * pMpg );
		//[DllImport ("libmpg123.so.0")] private static extern int           mpg123_encsize( int iEncoding );

		void * _pMpg = null;

		/// <remarks>Future challenge. Read from stream.</remarks>
		/// <exception cref="InvalidOperationException" />
		/// <exception cref="FileLoadException" />
		/// <exception cref="FormatException" />
		public Mpg123( string strFileName ) {
			Mpg123_Errors eErr = 0;

			// If create parameters you can delete them after getting the main handle.
			_pMpg = mpg123_new( null, ref eErr );
			if( eErr != Mpg123_Errors.MPG123_OK )
				throw new InvalidOperationException( "Couldn't initialize a new MPG123 decoder. (" + eErr.ToString() + ")" );

			byte[] rgFileName = Encoding.UTF8.GetBytes(strFileName);

			if (  mpg123_open( _pMpg, rgFileName) != Mpg123_Errors.MPG123_OK )
				throw new FileNotFoundException( "Couldn't open file requested" );

			_rgBuffer = new byte[mpg123_outblock( _pMpg )];

			long lRate     = 0;
			int  iChannels = 0;
			int  iEncoding = 0;

			if( mpg123_getformat( _pMpg, ref lRate, ref iChannels, ref iEncoding ) != Mpg123_Errors.MPG123_OK )
				throw new FormatException( "Couldn't get mp3 format" );

			Spec = new Specification( lRate, iChannels, iEncoding, mpg123_encsize( iEncoding ) * 8  );
		}

		public override void Dispose() {
			if( _pMpg != null ) {
				mpg123_close ( _pMpg );
				mpg123_delete( _pMpg );
                _pMpg = null;
			}
		}

		protected override uint BufferReload(uint uiRequest) {
			uint ulBuffered = 0;

			fixed( byte * pbBuffer = &_rgBuffer[0] ) {
				Mpg123_Errors eError = mpg123_read( _pMpg, pbBuffer, (uint)_rgBuffer.Length, ref ulBuffered );
				switch( eError ) {
					case Mpg123_Errors.MPG123_DONE:
						return( 0 );
					case Mpg123_Errors.MPG123_OK:
						if( ulBuffered == 0 )
							throw new InvalidOperationException( "Unexpected zero length buffer read." );
						break;
					default:
						throw new InvalidOperationException( "Could not read buffer: Error (" + eError.ToString() + ")" );
				}
			}

			return ulBuffered;
		}

	}

	public class Mpg123FFTSupport : Mpg123 {
		public double[] Target { get; set; }

		protected double      _dbT = 0; // Keep track of where we are in the signal.
		protected BlockCopies _oBlockCopy;

		public Mpg123FFTSupport( string strFileName ) : base( strFileName ) {
			// Unfortunately we can't know the decimation until the FFT mode is retrieved
			// and we can't know that until the MP3 data rate has been read. >_<;;
			_oBlockCopy = new BlockCopies( 1, Spec.Channels, 0 );
		}

		public void Init( int iDecimation, int iChannelUsed ) {
			_oBlockCopy = new BlockCopies( iDecimation, Spec.Channels, iChannelUsed );
		}

		/// <summary>
		/// Generate a test signal. Override BufferReload when you want to test.
		/// Don't delete this implementation. It's a great example. Notibly "_dbT".
		/// </summary>
		/// <param name="uiRequest">Minimum of data to load into the buffer.</param>
		/// <returns></returns>
        protected  uint BufferReload2( uint uiRequest ) {
            unsafe {
                fixed( void * pSource = _rgBuffer ) {
                    short * pShortSrc = (short*)pSource;
					int     iSamples  = _rgBuffer.Length / Spec.BitsPerSample * 8;

					for( int i = 0; i < iSamples; _dbT += 1 / (double)Spec.Rate ) {
						double dbSample = 0;

                        dbSample += 180  * Math.Sin(Math.PI * 2 *  400 * _dbT);
                        dbSample += 180  * Math.Sin(Math.PI * 2 * 1200 * _dbT);
                        dbSample += 1500 * Math.Sin(Math.PI * 2 * 2900 * _dbT);

                        //dbSample += 1500;

                        for( int j=0; j<Spec.Channels; ++j ) {
							pShortSrc[i++] = (short)dbSample;
						}
					}
                }
            }
			return (uint)_rgBuffer.Length;
        }

        protected override bool BufferCopy( ref int iTrg, byte[] rgBuffer, uint uiBuffered, ref uint uiBuffUsed ) {
            return _oBlockCopy.ReadAsSigned16Bit( Target, ref iTrg, rgBuffer, uiBuffered, ref uiBuffUsed );
        }
    }

	/// <summary>
	/// This is the factory object to create MP3 decoder instances. Should only create one of these.
	/// </summary>
	public class Mpg123Factory : IDisposable {
		//[DllImport ("/usr/local/lib/libmpg123.so")] private static extern int mpg123_init();
		//[DllImport ("/usr/local/lib/libmpg123.so")] private static extern int mpg123_exit();
		[DllImport ("libmpg123-0.dll")] private static extern int mpg123_init();
		[DllImport ("libmpg123-0.dll")] private static extern int mpg123_exit();
		

		static int iCount = 0;

		public Mpg123Factory() {
			if( iCount++ == 0 )
				mpg123_init(); // looking for libmpg123-0.dll
			if( iCount > 1 )
				throw new InvalidOperationException( "Factory object should be created only once." );
		}

		public Mpg123 CreateFor( string strFileName ) {
			return( new Mpg123( strFileName ) );
		}

		public void Dispose() {
			if( --iCount == 0 )
				mpg123_exit();
			if( iCount < 0 )
				throw new InvalidOperationException( "Factory object should be destroyed only once." );
		}
	}
}
