using NLayer;
using System;
using System.Buffers.Binary;
using System.IO;

namespace Play.Sound {
	/// <summary>Developed for the NLayer system from teh NAudio guys.</summary>
	/// <remarks>
	/// This almost works. Hearing some popping so I've got a problem
	/// somewhere. 
	/// </remarks>
	public class NLayerMP3 : AbstractReader {
		readonly MpegFile _oReader;
		readonly float[] _flBuffer;
		readonly int     _iBytesPerSample = 2;
		readonly byte[]  _rgBytes = new byte[2];
		readonly Random  _oRandom = new Random();

		/// <remarks>Future challenge. Read from stream.</remarks>
		/// <exception cref="InvalidOperationException" />
		/// <exception cref="FileLoadException" />
		/// <exception cref="FormatException" />
		public NLayerMP3( string strFileName ) {
			_oReader = new MpegFile( strFileName );

			long lRate			 = _oReader.SampleRate;
			int  iChannels		 = _oReader.Channels;
			int  iEncoding		 = 0;

			Spec = new Specification( lRate, iChannels, iEncoding, _iBytesPerSample * 8  );

			_rgBuffer = new byte [ lRate * iChannels * _iBytesPerSample ];
			_flBuffer = new float[ lRate * iChannels ];
		}

		public override void Dispose() {
			_oReader?.Dispose();
		}

		protected float GenerateTpdfNoise {
			get {
				// Generate two uniform random numbers (e.g., between -0.5 and 0.5) and sum them
				float rand1 = _oRandom.NextSingle() - 0.5f;
				float rand2 = _oRandom.NextSingle() - 0.5f;

				return rand1 + rand2; // Range approx -1.0 to 1.0 (of the LSB magnitude)
			}
		}

		/// <summary>
		/// see: naudio convert normalized sound to 16 bit, query
		/// https://www.kvraudio.com/forum/viewtopic.php?t=566788
		/// /// </summary>
		/// <param name="uiRequest">I seem to be ignoring this and just
		/// try to fill the buffer to it's max.</param>
		protected override uint BufferReload( uint uiRequest ) {
			uint        ulSamples = 0;
			const int   iMax      = 32767;
			const int   iMin      = -iMax - 1;
		    const float flConv    = iMax;

			ulSamples = (uint)_oReader.ReadSamples( _flBuffer, 0, _flBuffer.Length );

			for( uint i = 0; i<ulSamples; ++i) {
				int iSample16 = (int)(_flBuffer[i] * flConv + GenerateTpdfNoise );

				if( iSample16 > iMax ) {
					iSample16 = iMax;
				} else if( iSample16 < iMin ) {
					iSample16 = iMin;
				}
				
				BinaryPrimitives.WriteInt16LittleEndian( _rgBytes, (short)iSample16 );

				uint j= i*2;
				_rgBuffer[j]   = _rgBytes[0];
				_rgBuffer[j+1] = _rgBytes[1];
			}

			return ulSamples * (uint)_iBytesPerSample;
		}
	}

}
