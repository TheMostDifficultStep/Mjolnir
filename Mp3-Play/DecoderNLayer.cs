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
		readonly  float[] _flBuffer;
		readonly  int     _iBytesPerSample = 2;
		readonly  byte[]  _rgBytes = new byte[2];

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

		/// <summary>
		/// see: naudio convert normalized sound to 16 bit, query
		/// /// </summary>
		protected override uint BufferReload(uint uiRequest) {
			uint ulSamples  = 0;
		    //const float flConv = 32767f;

			ulSamples = (uint)_oReader.ReadSamples( _flBuffer, 0, _flBuffer.Length );

			for( uint i = 0; i<ulSamples; ++i) {
				//short iSixteenBit = (short)(_flBuffer[i] * flConv );
				short iSixteenBit = (short)(((int) ((_flBuffer[i] * 32767f) + 32768.5f)) - (int)32768);
				BinaryPrimitives.WriteInt16LittleEndian( _rgBytes, iSixteenBit );

				uint j= i*2;
				_rgBuffer[j]   = _rgBytes[0];
				_rgBuffer[j+1] = _rgBytes[1];
			}

			return ulSamples * (uint)_iBytesPerSample;
		}
	}

}
