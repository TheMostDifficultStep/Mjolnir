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

// https://docs.microsoft.com/en-us/dotnet/framework/interop/marshaling-classes-structures-and-unions

// MSDN Audio Streaming Sample Code
// https://msdn.microsoft.com/en-us/library/windows/desktop/dd317593(v=vs.85).aspx

using System;
using System.Runtime.InteropServices;
using System.Collections.Generic;

namespace Play.Sound {
	unsafe public class WmmReader : WmmDevice {
		// These need to live on a class. So might as well have them here.
        [DllImport("winmm.dll", SetLastError = true, CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Winapi)]
			public static extern MMSYSERROR waveInOpen(ref IntPtr phwo, int uDeviceID, ref WAVEFORMATEX oFmt, WaveOutProc dwCallback, IntPtr dwCallbackInstance, WaveOpenFlags dwFlags);
        [DllImport("winmm.dll", SetLastError = true, CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Winapi)]
			public static extern MMSYSERROR waveInClose( IntPtr hHandle );
        [DllImport("winmm.dll", SetLastError = true, CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Winapi)]
			public static extern MMSYSERROR waveInPrepareHeader(IntPtr hwo, IntPtr ipHeader, int cbwh);
        [DllImport("winmm.dll", SetLastError = true, CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Winapi)]
			public static extern MMSYSERROR waveInUnprepareHeader(IntPtr hwo, IntPtr ipHeader, int cbwh);
        [DllImport("winmm.dll", SetLastError = true, CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Winapi)]
			public static extern MMSYSERROR waveInReset(IntPtr hwo);
        [DllImport("winmm.dll", SetLastError = true, CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Winapi)]
			public static extern MMSYSERROR waveInAddBuffer( IntPtr hWaveIn, IntPtr ipHeader, int iWH );
        [DllImport("winmm.dll", SetLastError = true, CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Winapi)]
			public static extern MMSYSERROR waveInStart( IntPtr hwo);
        [DllImport("winmm.dll", SetLastError = true, CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Winapi)]
			public static extern MMSYSERROR waveInStop( IntPtr hwo );

		private class ReadHeader : ManagedHeader {
			public ReadHeader( IntPtr hWave, uint uiBytesPerBlock, uint uiID ) :
				base( hWave, uiBytesPerBlock, uiID ) 
			{
			}

			public override MMSYSERROR DoPrepare( IntPtr hWave, IntPtr hHeader, int iSize ) {
				return waveInPrepareHeader( hWave, _ipUnManagedHeader, Marshal.SizeOf(typeof(WAVEHDR) ));
			}

			public override MMSYSERROR UnPrepare( IntPtr hWave, IntPtr hHeader, int iSize ) {
				return waveInUnprepareHeader( hWave, _ipUnManagedHeader, Marshal.SizeOf(typeof(WAVEHDR) ));
			}

			/// <exception cref="ArgumentException" />
			/// <exception cref="BadDeviceIdException" />
			/// <exception cref="InvalidHandleException" />
			/// <exception cref="MMSystemException" />
			public override void AddBuffer( IntPtr hWaveIn ) {
				_oWaveHeader.dwFlags &= ~WHDR_DONE;    // Clear the bit.
				_oWaveHeader.dwFlags &= ~WHDR_INQUEUE; // Clear the bit.

				Marshal  .StructureToPtr( _oWaveHeader, _ipUnManagedHeader, false ); // Managed -> Unmanaged.
				MMHelpers.ThrowOnError  ( waveInAddBuffer( hWaveIn, _ipUnManagedHeader, Marshal.SizeOf(typeof(WAVEHDR) ) ), ErrorSource.WaveIn );
			}
		}

        protected override MMSYSERROR Reset() {
            return waveInReset( _hWave );
        }

        protected override MMSYSERROR Close() {
            return waveInClose( _hWave );
        }

		protected override MMSYSERROR Open() {
			return waveInOpen(ref _hWave, DeviceID, ref _oFormat, null, IntPtr.Zero, WaveOpenFlags.CALLBACK_NULL );
		}

		public void RecordStart() {
			MMHelpers.ThrowOnError(waveInStart( _hWave ), ErrorSource.WaveIn );
		}

		public void RecordStop() {
			MMHelpers.ThrowOnError(waveInStop( _hWave ), ErrorSource.WaveIn );
		}

		protected override ManagedHeader CreateHeader( uint uiId ) {
			return new ReadHeader( _hWave, BytesPerHeader, uiId );
		}

		readonly byte[]               _rgBytes;
        readonly BlockCopies          _oWaveReader;
		readonly Queue<ManagedHeader> _quHeaders = new();

		/// <summary>
		/// While the sound might be multi channel. We only listen for channel 0.
		/// </summary>
		/// <param name="oSpec">Audio parameters.</param>
		/// <param name="iDeviceID">Wave device id.</param>
        public WmmReader( Specification oSpec, int iDeviceID ) : base( oSpec, iDeviceID ) {
			// Headers all prepped now just add 'em into the system.
			try {
				foreach( ReadHeader oHeader in _rgHeaders ) {
					oHeader.AddBuffer( _hWave );
					_quHeaders.Enqueue( oHeader );
				}
			} catch( Exception oEx ) {
				CleanAndThrow( oEx );
			}

			_rgBytes     = new byte[BytesPerHeader];
			_oWaveReader = new ( 1, oSpec.Channels, 0, oSpec.BitsPerSample );

			// Call start recording to get things going.
        }

		/// <summary>Read as many of the headers as are ready. 
		/// Only reading sound from a single channel.</summary>
		/// <remarks>
		/// By this time the headers are all enqueued. And we're checking
		/// the first one to see if it is ready. Keep processing ready headers
		/// until we get to the first non ready element.
		/// The only scary thing is if for SOOOME reason the a
		/// header never get's loaded, sound will stop. We currently
		/// have no way to reset.
		/// </remarks>
		/// <param name="oQueue">You might think a stream would be what you
		/// want but turns out a queue does the job neatly.</param>
		/// <returns>The amount of buffered capture time in milliseconds.</returns>
		/// <exception cref="InvalidOperationException" />
		/// <exception cref="ArgumentException" />
		/// <exception cref="BadDeviceIdException" />
		/// <exception cref="InvalidHandleException" />
		/// <exception cref="MMSystemException" />
		public uint Read( Queue<short> oQueue ) {
			if( _quHeaders.Count <= 0 )
				throw new InvalidOperationException( "Signal Queue is Empty!" );
			if( _quHeaders.Count > _rgHeaders.Count )
				throw new InvalidOperationException( "Unexpected # of headers in microphone queue!" );

			while( _quHeaders.TryPeek( out ManagedHeader oHeader ) ) {
				oHeader.Refresh();

				if( !oHeader.IsUsable )
					break;

				_quHeaders.Dequeue();

				int iRead = oHeader.Read( _hWave, _rgBytes );

				foreach( short sSample in new BlockCopies.SampleEnumerable( _oWaveReader, _rgBytes, iRead ) ) {
					oQueue.Enqueue( sSample );
				}

				oHeader.AddBuffer( _hWave );
				_quHeaders.Enqueue( oHeader );
			}

			// By now they should be all newly queued up or waiting.
			return (uint)(MilliSecPerHeader * _rgHeaders.Count - 1 );
		}
    }
}