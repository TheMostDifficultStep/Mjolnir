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

		int _iNextRead = 0;

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

			public void AddBuffer( IntPtr hWaveIn ) {
				// dwFlags &= ~WHDR_DONE; clear the done flag and add the buffer.
				MMHelpers.ThrowOnError( waveInAddBuffer( hWaveIn, _ipUnManagedHeader, Marshal.SizeOf(typeof(WAVEHDR) ) ), ErrorSource.WaveIn );
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

		protected override ManagedHeader CreateHeader( uint uiId ) {
			return new ReadHeader( _hWave, BytesPerHeader, uiId );
		}

		byte[] _rgBytes;

        public WmmReader( Specification oSpec, int iDeviceID ) : base( oSpec, iDeviceID ) {
			// Headers all prepped now just add 'em into the system.
			try {
				foreach( ReadHeader oHeader in _rgHeaders ) {
					oHeader.AddBuffer( _hWave );
				}
			} catch( Exception oEx ) {
				CleanAndThrow( oEx );
			}
			_rgBytes = new byte[BytesPerHeader];
			// Call start recording to get things going.
        }

		public void ProcessBuffer( int iRead ) {
		}

		public uint Read() {
			// The only scary thing is if for SOOOME reason the 
			// a header never get's loaded. We'll be stuck waiting
			// forever!
			while( _rgHeaders[_iNextRead] is ReadHeader oHeader ) {
				if( oHeader.IsUsable ) {
					ProcessBuffer( oHeader.Read( _hWave, _rgBytes ) );

					oHeader.Recycle();
					oHeader.AddBuffer( _hWave );

					if( ++_iNextRead > _rgHeaders.Count )
						_iNextRead = 0;
				} else {
					break;
				}
			}

			// By now they should be all newly queued up or waiting.
			return (uint)(MilliSecPerHeader * _rgHeaders.Count / 2);
		}
    }
}