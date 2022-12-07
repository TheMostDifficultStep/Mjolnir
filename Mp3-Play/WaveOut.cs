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

// MSDN CreateSemaphore function
// https://msdn.microsoft.com/en-us/library/windows/desktop/ms682438(v=vs.85).aspx
// MSDN WaitForSingleObjectEx function
// https://msdn.microsoft.com/en-us/library/windows/desktop/ms687036(v=vs.85).aspx

// https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/lock-statement
// See also System.Threading.Semaphore

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Globalization;

namespace Play.Sound {
	public class MMHelpers {
        [DllImport("winmm.dll", SetLastError = true, CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Winapi)]
			public static extern MMSYSERROR waveInGetErrorText(MMSYSERROR mmrError, StringBuilder pszText, int cchText);
        [DllImport("winmm.dll", SetLastError = true, CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Winapi)]
			public static extern MMSYSERROR waveOutGetErrorText(MMSYSERROR mmrError, StringBuilder pszText, int cchText);
        [DllImport("winmm.dll", SetLastError = true, CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Winapi)]
			public static extern MMSYSERROR waveOutGetDevCaps( UInt32 uiDevID, ref WAVEOUTCAPS pWOC, UInt32 uiCapStructSize );
        [DllImport("winmm.dll", SetLastError = true, CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Winapi)]
			public static extern MMSYSERROR waveInGetDevCaps( UInt32 uiDevID, ref WAVEINCAPS pWOC, UInt32 uiCapStructSize );

		public static string GenerateErrorMessage( MMSYSERROR error, ErrorSource source ) {
			StringBuilder sbLastError   = new StringBuilder(255);
            MMSYSERROR    pullInfoError = MMSYSERROR.MMSYSERR_ERROR;

            switch (source)
            {
                case ErrorSource.WaveIn:
                    pullInfoError = waveInGetErrorText(error, sbLastError, sbLastError.Capacity + 1);
                    break;
                case ErrorSource.WaveOut:
                    pullInfoError = waveOutGetErrorText(error, sbLastError, sbLastError.Capacity + 1);
                    break;
            }

            if (pullInfoError != MMSYSERROR.MMSYSERR_NOERROR) {
				sbLastError.Append( "Problem analysing error..." );
            }

			sbLastError.Append( " (" );
			sbLastError.Append( ((int)error).ToString(CultureInfo.CurrentCulture) );
			sbLastError.AppendLine( ")." );

			return( sbLastError.ToString() );
		}

        public static void ThrowOnError( MMSYSERROR error, ErrorSource source ) {
            if (error == MMSYSERROR.MMSYSERR_NOERROR) {
                return;
            }

			string strDetails = GenerateErrorMessage( error, source );

			switch( error ) {
				case MMSYSERROR.MMSYSERR_INVALPARAM:
					throw new ArgumentException(strDetails);

				case MMSYSERROR.MMSYSERR_BADDEVICEID:
					throw new BadDeviceIdException(strDetails);

				case MMSYSERROR.MMSYSERR_INVALHANDLE:
					throw new InvalidHandleException();
			}

            throw new MMSystemException(strDetails);
        }

		/// <summary>
		/// Enumerate the output devices. Device ID corresponds to appearance in output list
		/// Device ID start at zero.
		/// </summary>
		public static IEnumerator<string> GetOutputDevices(){
			WAVEOUTCAPS sOutCaps      = new WAVEOUTCAPS();
			UInt32      uiOutCapsSize = (uint)Marshal.SizeOf( typeof( WAVEOUTCAPS ) );
			UInt32      uiDev         = 0;

			while( waveOutGetDevCaps( uiDev++, ref sOutCaps, uiOutCapsSize ) == MMSYSERROR.MMSYSERR_NOERROR ) {
				yield return sOutCaps.szPname;
			}
		}

		/// <summary>
		/// Enumerate the output devices. Device ID corresponds to appearance in output list
		/// Device ID start at zero.
		/// </summary>
		public static IEnumerator<string> GetInputDevices(){
			WAVEINCAPS sInCaps       = new WAVEINCAPS();
			UInt32     uiOutCapsSize = (uint)Marshal.SizeOf( typeof( WAVEINCAPS ) );
			UInt32     uiDev         = 0;

			while( waveInGetDevCaps( uiDev++, ref sInCaps, uiOutCapsSize ) == MMSYSERROR.MMSYSERR_NOERROR ) {
				yield return sInCaps.szPname;
			}
		}
	} /* end class */
	
	unsafe public class WmmPlayer : IPgPlayer 
	{
		/* flags for dwFlags field of WAVEHDR */
		const int WHDR_DONE      = 0x00000001;  /* done bit */
		const int WHDR_PREPARED  = 0x00000002;  /* set if this header has been prepared */
		const int WHDR_BEGINLOOP = 0x00000004;  /* loop start block */
		const int WHDR_ENDLOOP   = 0x00000008;  /* loop end block */
		const int WHDR_INQUEUE   = 0x00000010;  /* reserved for driver */

		private class ManagedHeader {
			WAVEHDR          _oWaveHeader             = new WAVEHDR();
			readonly IntPtr  _ipUnManagedHeader       = IntPtr.Zero; 
			readonly uint    _uiBufferCapacityInBytes = 0;

			/// <exception cref="ArgumentException"></exception>
			/// <exception cref="ArgumentOutOfRangeException"></exception>
			/// <exception cref="OutOfMemoryException"></exception>
			public ManagedHeader( IntPtr hWave, uint uiBytesPerBlock, uint uiID ) {
				if( hWave == IntPtr.Zero )
					throw new ArgumentException( "Wave device handle is empty." );
				if( uiBytesPerBlock == 0 || uiBytesPerBlock > 20000 ) // Arbitrary big number.
					throw new ArgumentOutOfRangeException( "Bytes per block zero or greater than 10000." );

				_uiBufferCapacityInBytes = uiBytesPerBlock;

				// We share space for your header AND it's data in one unmanaged HGlobal memory alloc.
				// makes free-ing up resources easer. But I don't want to do the math to allocate a single
				// unmanaged buffer for ALL our headers. I don't think it's worth it.
				// Add a machine word at the end just in case my math is bum.
				_ipUnManagedHeader = Marshal.AllocHGlobal(  Marshal.SizeOf(typeof(WAVEHDR) ) + (int)_uiBufferCapacityInBytes + sizeof(int) );

				if( _ipUnManagedHeader == IntPtr.Zero ) {
					throw new OutOfMemoryException( "Couldn't allocate unmanaged header and buffer." );
				}

				_oWaveHeader.dwBufferLength = 0;
				_oWaveHeader.dwUser         = (IntPtr)uiID;
				_oWaveHeader.dwFlags        = 0;
				_oWaveHeader.lpData         = _ipUnManagedHeader + Marshal.SizeOf(typeof(WAVEHDR) );
				_oWaveHeader.lpNext         = IntPtr.Zero;
				_oWaveHeader.dwLoops        = 0;
				_oWaveHeader.reserved       = 0;
			}

			public void CopyUnmanagedValue( out WAVEHDR sWaveHeader ) {
				sWaveHeader = (WAVEHDR)Marshal.PtrToStructure( _ipUnManagedHeader, typeof( WAVEHDR ) );
			}

			/// <summary>
			/// Is this header currently queued up for playing.
			/// </summary>
			public bool IsUsable {
				get {
					if( ( _oWaveHeader.dwFlags & WHDR_INQUEUE ) != 0 )
						return false;

					if( ( _oWaveHeader.dwFlags & WHDR_DONE ) != 0 )
						return true;

					return true; // Both are false so we can use it.
				}
			}

			/// <summary>
			/// Mark this header usable for queueing music. Set all flags to zero.
			/// Set buffered loaded length to zero.
			/// </summary>
			public void Recycle() {
				// Just clear the bits in question. Don't set all flags to 
				// zero else you'll get an UNPREPARED error at the very least.
				_oWaveHeader.dwFlags        &= ~WHDR_DONE;    // Clear the bit.
				_oWaveHeader.dwFlags        &= ~WHDR_INQUEUE; // Clear the bit.
				_oWaveHeader.dwBufferLength  = 0;
			}

			/// <summary>
			/// Capacity of this sound buffer in bytes.
			/// </summary>
			public uint Capacity {
				get { return( _uiBufferCapacityInBytes ); }
			}

			/// <exception cref="ArgumentException"></exception>
			/// <exception cref="BadDeviceIdException" ></exception>
			/// <exception cref="InvalidHandleException"></exception>
			/// <exception cref="MMSystemException"></exception>
			/// <remarks>Basically copy my managed wave header into the unmanaged
			/// _ipUnManagedHeader to set it. Also set the WHDR_DONE flag on all
			/// buffers so my Play() program knows the buffer is ready to go.</remarks>
			public void Prepare( IntPtr hWave ) {
				// copy my managed wave header into the unmanaged _ipUnManagedHeader to set it
				Marshal.StructureToPtr( _oWaveHeader, _ipUnManagedHeader, false);

				MMSYSERROR error = MMSYSERROR.MMSYSERR_NOERROR;

				error = waveOutPrepareHeader(hWave, _ipUnManagedHeader, Marshal.SizeOf(typeof(WAVEHDR) ));

				CopyUnmanagedValue( out WAVEHDR oWaveHeader );

				if( ( oWaveHeader.dwFlags & WHDR_PREPARED ) == 0 )
					error = MMSYSERROR.WAVERR_UNPREPARED;

				MMHelpers.ThrowOnError( error, ErrorSource.WaveOut );

				_oWaveHeader = oWaveHeader; // Copy the stuff back to ourself. 
			}

			public void UnPrepare( IntPtr hWave ) {
				MMSYSERROR eError =	MMSYSERROR.MMSYSERR_NOERROR;

				if( (( _oWaveHeader.dwFlags & WHDR_PREPARED ) != 0 ) && 
					_ipUnManagedHeader != IntPtr.Zero 
				) {
					eError = waveOutUnprepareHeader( hWave, _ipUnManagedHeader, Marshal.SizeOf(typeof(WAVEHDR) ));
				}
				MMHelpers.ThrowOnError( eError, ErrorSource.WaveOut );

				CopyUnmanagedValue( out WAVEHDR oHeader );
				_oWaveHeader.dwFlags = oHeader.dwFlags;
			}

			public void Dispose() {
				if( _ipUnManagedHeader != IntPtr.Zero ) {
					Marshal.FreeHGlobal( _ipUnManagedHeader );
				  //_ipUnManagedHeader = IntPtr.Zero;
					_oWaveHeader.lpData    = IntPtr.Zero;
				}
			}

			/// <summary>
			/// Read from the input buffer out wave out buffer capacity.
			/// </summary>
			/// <returns>True if got all data requested. False if only partial read down to and including 0.</returns>
			/// <exception cref="InvalidOperationException" />
			/// <remarks>
			/// By putting all the buffer slicing and dicing in the read buffer where it belongs, our
			/// write buffer code becomes INCREDIBLY easy! I'm being lazy and not checking the read interface
			/// is not null... the try block will catch the nullreferenceexception and we throw as a
			/// invalidoperationexception with an inner exception set. ^_^;;
			/// </remarks>
			public bool Write( IntPtr hWave, IPgReader oBuffer ) {
				try {
					// We might have less data than our capacity.
					_oWaveHeader.dwBufferLength = (int)oBuffer.Read( _oWaveHeader.lpData, _uiBufferCapacityInBytes ); 

					// Copy our managed structure to the unmanaged header.
					Marshal.StructureToPtr( _oWaveHeader, _ipUnManagedHeader, false);
				} catch( Exception oEx ) {
					Type[] rgErrors = {	typeof( ArgumentException ),
										typeof( NullReferenceException ),
										typeof( InvalidOperationException ) };
					// If it's not something we can handle, just re-throw.
					if( !rgErrors.Contains( oEx.GetType() ) ) {
						throw;
					}
					// BUG: Well, what I really need to do is report an error. I don't have a way to do that yet.
					return( false );
				}

				if( _oWaveHeader.dwBufferLength <= 0 )
					return( false ); // We're done!

				MMHelpers.ThrowOnError( waveOutWrite( hWave, _ipUnManagedHeader, Marshal.SizeOf(typeof(WAVEHDR) )), ErrorSource.WaveOut );

				return( _oWaveHeader.dwBufferLength == _uiBufferCapacityInBytes );
			}
		}

        [DllImport("winmm.dll", SetLastError = true, CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Winapi)]
			public static extern MMSYSERROR waveOutOpen(ref IntPtr phwo, int uDeviceID, ref WAVEFORMATEX oFmt, WaveOutProc dwCallback, IntPtr dwCallbackInstance, WaveOpenFlags dwFlags);
        [DllImport("winmm.dll", SetLastError = true, CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Winapi)]
			public static extern MMSYSERROR waveOutClose( IntPtr hHandle );
        [DllImport("winmm.dll", SetLastError = true, CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Winapi)]
			public static extern MMSYSERROR waveOutPrepareHeader(IntPtr hwo, IntPtr ipHeader, int cbwh);
        [DllImport("winmm.dll", SetLastError = true, CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Winapi)]
			public static extern MMSYSERROR waveOutUnprepareHeader(IntPtr hwo, IntPtr ipHeader, int cbwh);
        [DllImport("winmm.dll", SetLastError = true, CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Winapi)]
			public static extern MMSYSERROR waveOutWrite(IntPtr hwo, IntPtr ipHeader, int cbwh);
        [DllImport("winmm.dll", SetLastError = true, CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Winapi)]
			public static extern MMSYSERROR waveOutReset(IntPtr hwo);

		WAVEFORMATEX        _oFormat;
		IntPtr              _hWave             = IntPtr.Zero;
		readonly int        _iBlockCapacity    = 42;   // Bigger, longer play between interruptions, but longer bleed out.
		readonly uint       _uiSamplesPerBlock = 1024; // Was 42/512.
		List<ManagedHeader> _rgHeaders         = new List<ManagedHeader>();
      //WaveOutProc         _oCallback;
		uint				_uiWaitInMs; // Bleed time.

		public int DeviceID { get; protected set; }

		readonly Specification _oSpec;

		/// <summary>
		/// Set up a music player for use.
		/// </summary>
		/// <exception cref="ArgumentException" />
		/// <exception cref="ArgumentNullException" />
		/// <exception cref="BadDeviceIdException" />
		/// <exception cref="InvalidHandleException" />
		/// <exception cref="MMSystemException" />
		/// <seealso>https://learn.microsoft.com/en-us/windows/win32/api/synchapi/nf-synchapi-createeventa</seealso>/>
		public WmmPlayer( Specification oSpec, int iDeviceID ) {
		  //_oCallback = new WaveOutProc(this.InternalCallback);
			_oSpec     = oSpec ?? throw new ArgumentNullException();

			DeviceID = iDeviceID;

			_oFormat.wFormatTag       = (short)WaveFormatTag.Pcm; // Little-endian
			_oFormat.wBitsPerSample   = (short)oSpec.BitsPerSample;
			_oFormat.nSamplesPerSec   = oSpec.Rate;
			_oFormat.nChannels        = (short)oSpec.Channels;
			_oFormat.nBlockAlign      = (short)(_oFormat.nChannels * _oFormat.wBitsPerSample / 8);
			_oFormat.nAvgBytesPerSec  = _oFormat.nSamplesPerSec * _oFormat.nBlockAlign;
			_oFormat.cbSize           = 0;

			MMSYSERROR eError = MMSYSERROR.MMSYSERR_NOERROR;

			try {
				eError = waveOutOpen(ref _hWave, iDeviceID, ref _oFormat, null, IntPtr.Zero, WaveOpenFlags.CALLBACK_NULL );
			} catch (Exception oEx) {
				Type[] rgErrors = { typeof( EntryPointNotFoundException ),
									typeof( MissingMethodException ),
									typeof( NotSupportedException ),
									typeof( DllNotFoundException ),
									typeof( BadImageFormatException  ),
									typeof( MethodAccessException  ) };
				if( !rgErrors.Contains( oEx.GetType() ) )
					throw;

				throw new MMSystemException( "Problem opening sound device", oEx );
			}

            MMHelpers.ThrowOnError( eError, ErrorSource.WaveOut );

			// First step, just create the header and allocate memory. DO NOT PREP
			// the header!!! Separate step since if we get an exception in the constructor 
			// we can't actually dispose, and thus unprep, the new object!!
			for (uint iAlloc=0; iAlloc < _iBlockCapacity; ++iAlloc ) {
				try {
					_rgHeaders.Add( new ManagedHeader( _hWave, BytesPerBLock, iAlloc ) );
				} catch( Exception oEx ) {
					CleanAndThrow( oEx );
				}
			}

			// The headers are ready to go. Now let's try prepping them.
			// If we have a problem we can try to tear 'em all down.
			foreach( ManagedHeader oHeader in _rgHeaders ) {
				try {
					oHeader.Prepare( _hWave );
				} catch( Exception oEx ) {
					CleanAndThrow( oEx );
				}
			}
		} // end method

		/// <summary>
		/// Clean up on some kind of exception thrown in our constructor.
		/// </summary>
		/// <param name="oEx"></param>
		private void CleanAndThrow( Exception oEx ) {
			MMSYSERROR eSaved = MMSYSERROR.MMSYSERR_NOERROR;

			Type[] rgErrors = { typeof( ArgumentException ),
								typeof( ArgumentOutOfRangeException ),
								typeof( OutOfMemoryException ),
								typeof( BadDeviceIdException ),
								typeof( InvalidHandleException ),
								typeof( MMSystemException ) };

			// If it's an error we recognize, we'll try to clean up our unmanaged resources on the way out.
			if( rgErrors.Contains( oEx.GetType() ) ) {
				foreach( ManagedHeader oClean in _rgHeaders ) {
					oClean.UnPrepare( _hWave );
					oClean.Dispose();
				}
			}
			if( eSaved != MMSYSERROR.MMSYSERR_NOERROR )
				throw new MMSystemException( MMHelpers.GenerateErrorMessage( eSaved, ErrorSource.WaveOut ), oEx );

			throw oEx;
		}

		public Specification Spec {
			get { return( _oSpec ); }
		}

		public uint MilliSecPerBlock {
			get {
				return( (( _uiSamplesPerBlock * 1000 + (uint)_oFormat.nSamplesPerSec - 1) / (uint)_oFormat.nSamplesPerSec ) );
			}
		}

		public uint BytesPerBLock {
			get {
				return( (uint)_oFormat.nBlockAlign * _uiSamplesPerBlock );
			}
		}

		public uint Busy => _uiWaitInMs;

		/// <summary>
		/// Normally we would use the ipInstance variable to disambiguate the caller in unmanaged world.
		/// That value being the callback instance param of the waveOutOpen() call. But since we magically end 
		/// up back here as a delegate we're OK. Kinda magic. ^_^;;
		/// </summary>
		/// <remarks>This no longer used. I've read the callback can crash in certain instances of thread usage.</remarks>
		protected virtual void InternalCallback(IntPtr waveOutHandle, WaveOutMessage eMessage, IntPtr ipInstance, IntPtr ipWaveHeader, IntPtr dwReserved )
        {
            if( eMessage == WaveOutMessage.WriteDone) {
				try {
					// If we ever have the ability to resize the number of headers, we would NOT want to try that
					// WHILE the system is playing. We would have to re-visit this implementation.
					WAVEHDR oHeader = (WAVEHDR)Marshal.PtrToStructure( ipWaveHeader, typeof( WAVEHDR ) );

					//int uiIndex = (int)oHeader.dwUser;
					//if( uiIndex >= 0 && uiIndex < _rgHeaders.Count )
					_rgHeaders[(int)oHeader.dwUser].Recycle();
				} catch( Exception oEx ) {
					Type[] rgErrors = { typeof( ArgumentException ),
						                typeof( ArgumentNullException ),
										typeof( MissingMethodException ),
										typeof( NullReferenceException ),
										typeof( IndexOutOfRangeException ) };
					if( !rgErrors.Contains( oEx.GetType() ) )
						throw;
					// If we've got a problem in here we're SOL. Right now let's ignore the
					// problem. It should just fill the buffers, clog and we stop playing. 
				}
            }
        }

		/// <summary>
		/// Write the reader buffer to the device. This function load the device buffer and
		/// return immediately!!!
		/// </summary>
		/// <param name="oBuffer">Buffer containing sound data.</param>
		/// <param name="uiWaitInMs" >Returns the estimated maximum play time buffered.</param>
		/// <returns>The amount of time expected to play the consumed samples.</returns>
		/// <remarks>
		/// If we ever have the ability to resize the number of headers, we would NOT want to try that
		/// WHILE the system is playing. We would have to re-visit this implementation.
		/// </remarks>
		/// <exception cref="InvalidOperationException"/>
		/// <exception cref="ArgumentNullException" />
		public uint Play( IPgReader oBuffer ) {
			_uiWaitInMs = 0;

			try {
				// When we've finished, all our write buffers will be full, or the song is
				// done and we just need wait for the write buffers to be emptied.
				foreach( ManagedHeader oManagedHdr in _rgHeaders ) {
					oManagedHdr.CopyUnmanagedValue( out WAVEHDR oWaveHdr );

					// Make sure you sum time from ALL headers.
					if( ( oWaveHdr.dwFlags & WHDR_INQUEUE ) != 0 ) {
						_uiWaitInMs += MilliSecPerBlock;
					} else {
						if( ( oWaveHdr.dwFlags & WHDR_DONE ) != 0 ) {
							oManagedHdr.Recycle();
						}
						oManagedHdr.Write( _hWave, oBuffer );
						_uiWaitInMs += MilliSecPerBlock;
					}
				}
			} catch( Exception oEx ) {
				Type[] rgErrors = { typeof( ArgumentException ),
									typeof( BadDeviceIdException ),
									typeof( InvalidHandleException ),
									typeof( MMSystemException ),
									typeof( NullReferenceException ),
									typeof( ArgumentOutOfRangeException ) };
				if( !rgErrors.Contains( oEx.GetType() ) ) 
					throw;

				throw new MMSystemException( "Problem writing sound headers. Check inner exception.", oEx );
			}

			return _uiWaitInMs;
		}

		/// <summary>
		/// Put the thread to sleep until the player is finished.
		/// </summary>
		/// <see cref="_ao_wait_wave_headers"/>
		/// <remarks>
		/// So I think I've got this little bit of code from LIBAO _ao_wait_wave_headers() written
		/// by Benjamin Gerard, figured out...
		/// 
		/// I think what's happening is each time we check if we are done, we wait a little
		/// over half the time needed to finish up playing remaining blocks to be played. And we
		/// get it down where we'll wait half the time of the last block. So maximally we wait 1/2 
		/// active block's worth of time, with a minimum of 1 ms.
		/// 
		/// I was thinking of making each block report it's time so they could be variable and we
		/// still get an accurate time guess, but as the system is, each block is playing 13 ms
		/// of sound. NOBODY is notice that amount of waiting. It's not worth the effort and complexity.
		/// 
		/// It might be nice to have a max wait param. But we'll wait maximally our BlockCapacity *
		/// MSPerBlock or so.
		/// </remarks>
		protected void WaitUntilFinished() {
			while( true ) {
				int iBusyHeaders = 0;

				foreach(  ManagedHeader oHeader in _rgHeaders ) {
					if( !oHeader.IsUsable ) {
						++iBusyHeaders;
					}
				}
				if( iBusyHeaders <= 0 )
					break;

				// BUG : Ack! I should make sure the player is actually playing, else the big sleep!!! ^_^;
				System.Threading.Thread.Sleep( (int)((MilliSecPerBlock>>1)+1 ) * iBusyHeaders );
			}
		}

		public void Dispose() {
			MMSYSERROR eError = MMSYSERROR.MMSYSERR_NOERROR;

			if( _hWave != IntPtr.Zero ) {
				eError = waveOutReset( _hWave );
				// I Want to log the error. I'll need a site to do that.

				foreach( ManagedHeader oHeader in _rgHeaders ) {
					oHeader.UnPrepare( _hWave );
				}

				eError = waveOutClose( _hWave );

				foreach( ManagedHeader oHeader in _rgHeaders ) {
					oHeader.Dispose();
				}

				_hWave = IntPtr.Zero;
			
				// Better late than never!!
				MMHelpers.ThrowOnError( eError, ErrorSource.WaveOut );
			}
		}
	} // end class
} // end namespace
