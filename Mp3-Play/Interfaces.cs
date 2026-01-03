///<summary>
///  Copyright (c) https://github.com/TheMostDifficultStep
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

using System;

namespace Play.Sound {
	// Moved from integration. I'm not really winning much by having it there.
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

	/// <summary>
	/// A little class I'm using to save the decoded data specification. So you can pass
	/// around the pertinant data all in one go.
	/// </summary>
	public class Specification {
		readonly uint _ulRate;
		readonly uint _uiChannels;
		readonly int  _iEncoding;
		readonly uint _uiBitsPerSample;
		readonly uint _uiByteRatePerSec;

		public Specification() {
		}

		/// <remarks>you can get the bits per sample via encoding. But since I need an
		/// mpg123 function to do it, I'll just save the value.</remarks>
		public Specification( long lRate, int iChannels, int iEncoding, int iBits ) {
			_ulRate           = (uint)lRate; // samples per second.
			_uiChannels       = (uint)iChannels;
			_iEncoding        = iEncoding; 
			_uiBitsPerSample  = (uint)iBits;
			_uiByteRatePerSec = (uint)( lRate * BytesPerSampleXChannels );
		}

		/// <summary>
		/// Returns the number of bytes per sample, including multiple channels.
		/// </summary>
		public uint BytesPerSampleXChannels {
			get {
				return (uint)(Channels * BitsPerSample / 8);
			}
		}

        /// <summary>
		/// Use this function to determine the buffer size necessary for the amount of play time
		/// desired.
        /// </summary>
		/// <returns>Size of buffer needed for the amount of play time desired.</returns>
        /// <param name="uiMilliSeconds">Play time wanted</param>
		[Obsolete("Not actually obsolete, but looks incorrectly implemented to me.")]
		public uint GetAlignedBufferSize( uint uiMilliSeconds ) {
			return( (uint)(uiMilliSeconds * _uiByteRatePerSec / 1000 ) );
		}

		/// <summary>
		/// Return the number of bytes of sampling that will last the given time. The value be block aligned.
		/// </summary>
		/// <param name="flTime">Time in seconds.</param>
		/// <returns>Amount of samples to generate generated.</returns>
		/// <remarks>So floats are weird and we can get odd numbers even tho' we've got block align.
		/// So first round the time multiplied by the bit block rate, THEN mult by the block align size.</remarks>
		public uint BytesNeeded( float flTime ) {
			return (uint)Math.Round( Rate * flTime ) * BytesPerSampleXChannels;
		}


		/// <summary>
		/// Return the frame count given the number of bytes in a buffer. Assumes no partial frames.
		/// You should be fine if you allocated the buffer using GetAlignedBufferSize();
		/// </summary>
		/// <seealso cref="GetAlignedBufferSize"/>
		public uint GetFrameCount( uint uiBytes ) {
			return uiBytes / BytesPerSampleXChannels;
		}

		public uint GetByteCount( uint uiFrames ) {
			return uiFrames * BytesPerSampleXChannels;
		}

		/// <summary>
		/// We assume the buffer has been loaded with whole frames and there are no fragments.
		/// A frame is the bit size * number of channels.
		/// </summary>
		/// <param name="uiBytes">Aligned size in bytes of the buffer.</param>
		/// <returns>Latency in milliseconds</returns>
		public uint GetMSLatency( uint uiBytes ) {
			return( (uint)( ( 1000 * uiBytes ) / _uiByteRatePerSec  ) );
		}
		
		/// <summary>
		/// Samples per second
		/// </summary>
		public int Rate {
			get { return( (int)_ulRate ); }
		}

		public int Channels {
			get { return( (int)_uiChannels ); }
		}

		public int Encoding {
			get { return( _iEncoding ); }
		}

		public int BitsPerSample {
			get { return( (int)_uiBitsPerSample ); }
		}

		public bool CompareTo( Specification oSpec ) {
			if( _ulRate != oSpec._ulRate )
				return( false );
			if( _uiChannels != oSpec._uiChannels )
				return( false );
			if( _iEncoding != oSpec.Encoding )
				return( false );
            if( _uiBitsPerSample != oSpec._uiBitsPerSample )
                return( false );

			return( true );
		}
	}

	public interface IPgReader : IDisposable {
		bool          IsReading { get; }
		UInt32        Read( IntPtr ipBuffer, UInt32 uiRequestedBytes );
	  //UInt32        Read( byte[] rgBuffer, int iStartIndex, UInt32 uiRequestedBytes );
		Specification Spec { get; }
	}

	public interface IPgPlayer : IDisposable {
		uint          Play( IPgReader oBuffer );
		Specification Spec { get; }
		uint		  Busy { get; }
		int           DeviceID { get; }
	}
	public interface IFrequencyConverter {
		double Do( double s );
		double DoWarmUp( double s ) { return 0; } // only needed by the pll filter.
		void   Clear();
		void   SetWidth( FrequencyLookup look );
		double OffsetCorrect( double dblAdjustedFrequency );
	}
} // End namespace
