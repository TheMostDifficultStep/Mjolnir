using System;
using System.Runtime.InteropServices;
using System.Text;

namespace Play.Sound {
	enum snd_pcm_stream_t { SND_PCM_STREAM_PLAYBACK = 0, SND_PCM_STREAM_CAPTURE, SND_PCM_STREAM_LAST = SND_PCM_STREAM_CAPTURE }
	enum snd_pcm_access_t { SND_PCM_ACCESS_MMAP_INTERLEAVED = 0, SND_PCM_ACCESS_MMAP_NONINTERLEAVED, SND_PCM_ACCESS_MMAP_COMPLEX, 
		SND_PCM_ACCESS_RW_INTERLEAVED, SND_PCM_ACCESS_RW_NONINTERLEAVED, SND_PCM_ACCESS_LAST = SND_PCM_ACCESS_RW_NONINTERLEAVED 
	}
	enum snd_pcm_format_t { 
	  SND_PCM_FORMAT_UNKNOWN = -1, SND_PCM_FORMAT_S8 = 0, SND_PCM_FORMAT_U8, SND_PCM_FORMAT_S16_LE, 
	  SND_PCM_FORMAT_S16_BE, SND_PCM_FORMAT_U16_LE, SND_PCM_FORMAT_U16_BE, SND_PCM_FORMAT_S24_LE, 
	  SND_PCM_FORMAT_S24_BE, SND_PCM_FORMAT_U24_LE, SND_PCM_FORMAT_U24_BE, SND_PCM_FORMAT_S32_LE, 
	  SND_PCM_FORMAT_S32_BE, SND_PCM_FORMAT_U32_LE, SND_PCM_FORMAT_U32_BE, SND_PCM_FORMAT_FLOAT_LE, 
	  SND_PCM_FORMAT_FLOAT_BE, SND_PCM_FORMAT_FLOAT64_LE, SND_PCM_FORMAT_FLOAT64_BE, SND_PCM_FORMAT_IEC958_SUBFRAME_LE, 
	  SND_PCM_FORMAT_IEC958_SUBFRAME_BE, SND_PCM_FORMAT_MU_LAW, SND_PCM_FORMAT_A_LAW, SND_PCM_FORMAT_IMA_ADPCM, 
	  SND_PCM_FORMAT_MPEG, SND_PCM_FORMAT_GSM, SND_PCM_FORMAT_S20_LE, SND_PCM_FORMAT_S20_BE, 
	  SND_PCM_FORMAT_U20_LE, SND_PCM_FORMAT_U20_BE, SND_PCM_FORMAT_SPECIAL = 31, SND_PCM_FORMAT_S24_3LE = 32, 
	  SND_PCM_FORMAT_S24_3BE, SND_PCM_FORMAT_U24_3LE, SND_PCM_FORMAT_U24_3BE, SND_PCM_FORMAT_S20_3LE, 
	  SND_PCM_FORMAT_S20_3BE, SND_PCM_FORMAT_U20_3LE, SND_PCM_FORMAT_U20_3BE, SND_PCM_FORMAT_S18_3LE, 
	  SND_PCM_FORMAT_S18_3BE, SND_PCM_FORMAT_U18_3LE, SND_PCM_FORMAT_U18_3BE, SND_PCM_FORMAT_G723_24, 
	  SND_PCM_FORMAT_G723_24_1B, SND_PCM_FORMAT_G723_40, SND_PCM_FORMAT_G723_40_1B, SND_PCM_FORMAT_DSD_U8, 
	  SND_PCM_FORMAT_DSD_U16_LE, SND_PCM_FORMAT_DSD_U32_LE, SND_PCM_FORMAT_DSD_U16_BE, SND_PCM_FORMAT_DSD_U32_BE, 
	  SND_PCM_FORMAT_LAST = SND_PCM_FORMAT_DSD_U32_BE, SND_PCM_FORMAT_S16 = SND_PCM_FORMAT_S16_LE, SND_PCM_FORMAT_U16 = SND_PCM_FORMAT_U16_LE, SND_PCM_FORMAT_S24 = SND_PCM_FORMAT_S24_LE, 
	  SND_PCM_FORMAT_U24 = SND_PCM_FORMAT_U24_LE, SND_PCM_FORMAT_S32 = SND_PCM_FORMAT_S32_LE, SND_PCM_FORMAT_U32 = SND_PCM_FORMAT_U32_LE, SND_PCM_FORMAT_FLOAT = SND_PCM_FORMAT_FLOAT_LE, 
	  SND_PCM_FORMAT_FLOAT64 = SND_PCM_FORMAT_FLOAT64_LE, SND_PCM_FORMAT_IEC958_SUBFRAME = SND_PCM_FORMAT_IEC958_SUBFRAME_LE, SND_PCM_FORMAT_S20 = SND_PCM_FORMAT_S20_LE, SND_PCM_FORMAT_U20 = SND_PCM_FORMAT_U20_LE 
	}

	//typedef void(* snd_async_callback_t)( IntPtr ipAsycHandle );
    public delegate void Snd_Async_Callback( IntPtr ipAsycHandle );

	unsafe public class Alsa : IPgPlayer {
		[DllImport ("libasound.so.2")] private static extern  int   snd_pcm_open ( ref void * pipPcmHandle, byte[] strName, snd_pcm_stream_t eStreamType, int iMode );
		[DllImport ("libasound.so.2")] private static extern void   snd_pcm_close( void * ipPcmHandle );
		[DllImport ("libasound.so.2")] private static extern  int   snd_pcm_drop ( void * ipPcmHandle );
		[DllImport ("libasound.so.2")] private static extern  int   snd_pcm_drain( void * ipPcmHandle );
		[DllImport ("libasound.so.2")] private static extern  int   snd_pcm_pause( void * ipPcmHandle, int enable);
		[DllImport ("libasound.so.2")] private static extern byte * snd_strerror ( int iError );

		[DllImport ("libasound.so.2")] private static extern  long   snd_pcm_avail_update                  ( void * pPcmHandle );
		[DllImport ("libasound.so.2")] private static extern  int    snd_async_add_pcm_handler             ( ref void * ppAsycHandle, void * pPcmHandle, ref Snd_Async_Callback callback, void * pPrivate_data );	
		[DllImport ("libasound.so.2")] private static extern  IntPtr snd_async_handler_get_pcm             ( void * pAsycHandle );
		[DllImport ("libasound.so.2")] private static extern  IntPtr snd_async_handler_get_callback_private( void * pAsycHandle );

		[DllImport ("libasound.so.2")] private static extern  int snd_pcm_hw_params_malloc         ( ref void * ppHwParams );
		[DllImport ("libasound.so.2")] private static extern  int snd_pcm_hw_params_any            ( void * pPcmHandle, void * pHwParams ); // Load current values.
		[DllImport ("libasound.so.2")] private static extern  int snd_pcm_hw_params_set_access     ( void * pPcmHandle, void * pHwParams, snd_pcm_access_t eAccess );
		[DllImport ("libasound.so.2")] private static extern  int snd_pcm_hw_params_set_format     ( void * pPcmHandle, void * pHwParams, snd_pcm_format_t eFormat );
		[DllImport ("libasound.so.2")] private static extern  int snd_pcm_hw_params_set_channels   ( void * pPcmHandle, void * pHwParams, uint iChannels );
		[DllImport ("libasound.so.2")] private static extern  int snd_pcm_hw_params_set_rate       ( void * pPcmHandle, void * pHwParams, uint uiRateHz, int dir);
		[DllImport ("libasound.so.2")] private static extern  int snd_pcm_hw_params_set_periods	   ( void * pPcmHandle, void * pHwParams, uint uiPeriods, int dir );
		[DllImport ("libasound.so.2")] private static extern  int snd_pcm_hw_params_set_buffer_size( void * pPcmHandle, void * pHwParams, ulong ulVal );
		[DllImport ("libasound.so.2")] private static extern  int snd_pcm_hw_params_set_period_time_near( void * pPcmHandle, void * pHwParam, [In, Out] ref uint uiValMicroSec, [In, Out] ref int iDir );
		[DllImport ("libasound.so.2")] private static extern  int snd_pcm_hw_params_set_buffer_time_near( void * pPcmHandle, void * pHwParam, [In, Out] ref uint uiValMicroSec, [In, Out] ref int iDir );
		[DllImport ("libasound.so.2")] private static extern  int snd_pcm_hw_params_get_period_size( void * pHwParam, ref uint uiFrames, [In, Out] ref int iDir );
		[DllImport ("libasound.so.2")] private static extern  int snd_pcm_hw_params                ( void * pPcmHandle, void * pHwParams ); // apply set params.
		[DllImport ("libasound.so.2")] private static extern void snd_pcm_hw_params_free           ( void * pHwParams );

		[DllImport ("libasound.so.2")] private static extern  int snd_pcm_prepare( void * ipPcmHandle );
		[DllImport ("libasound.so.2")] private static extern long snd_pcm_writei ( void * ipPcmHandle, void * pBuffer, ulong size);
		[DllImport ("libasound.so.2")] private static extern long snd_pcm_writen ( void * ipPcmHandle, void * pBuffer, ulong size);

		readonly Specification _oSpec;
		         void *        _pPcmHandle       = null;
		readonly string        _strPcmName       = "default"; // See command "aplay -l"
		readonly uint          _uiPeriods        = 4;
		readonly uint          _uiPlayBytesSize2 = 0;
		readonly uint          _uiPeriodFrames   = 0;
                 IntPtr        _ipPlayBuff       = IntPtr.Zero;
                 uint          _uiPlayByteSize   = 0;

		public Alsa( Specification oSpec ) {
			if( oSpec == null )
				throw new ArgumentNullException();
			_oSpec = oSpec;

			ASCIIEncoding oAscEncoder = new ASCIIEncoding();
			byte[]        rgName      = oAscEncoder.GetBytes( _strPcmName );

			if( snd_pcm_open( ref _pPcmHandle, rgName, snd_pcm_stream_t.SND_PCM_STREAM_PLAYBACK, 0 ) < 0) {
				throw new InvalidOperationException( "Error opening PCM device : " + _strPcmName );
			}

			void * pHwParams = null;
			int    iErr = 0;
			int    iDir = 0;
			try {
				iErr = snd_pcm_hw_params_malloc( ref pHwParams );
                if( iErr < 0 ) {
					string strErr = GetErrorString( iErr );
					throw new InvalidOperationException( "Couldn't create PCM Params Handle : " + strErr );
				}

				iErr = snd_pcm_hw_params_any( _pPcmHandle, pHwParams );
				if( iErr < 0 ) {
					string strErr = GetErrorString( iErr );
					throw new InvalidOperationException( "Couldn't Any the PCM Params Handle : " + strErr );
				}

				SetSampleStuffs( pHwParams );

				SetBufferStuffs( pHwParams, 50000 );

				if( snd_pcm_hw_params(_pPcmHandle, pHwParams ) < 0) {
					throw new InvalidOperationException( "Error setting HW params." );
				}

                uint uiPeriodSizeInFrames = 0;
                iErr = snd_pcm_hw_params_get_period_size( pHwParams, ref uiPeriodSizeInFrames, ref iDir );
                if( iErr < 0 ){
					throw new InvalidOperationException("snd_pcm_hw_params_get_period_size() failed : " + GetErrorString( iErr ) );
                }

                // A little test code to compare our calculated frames with the that given by ALSA.
				uint uiPeriodBytes  = oSpec.GetAlignedBufferSize( 50000 );
				uint uiPeriodFrames = oSpec.GetFrameCount( uiPeriodBytes );

                // Allocate our play buffer based on what ALSA thinks. BUG get play buffer size,
				// It's a little dodgy to be guessing the play size based on the period side. ^_^;;
                _uiPlayByteSize = oSpec.GetByteCount( uiPeriodSizeInFrames + 1 ) * _uiPeriods;
                _ipPlayBuff     = Marshal.AllocHGlobal( (int)( _uiPlayByteSize + oSpec.GetByteCount(1) ) );
                
				snd_pcm_hw_params_free( pHwParams );
                pHwParams = null;

				iErr = snd_pcm_prepare( _pPcmHandle );
				if( iErr < 0 ) {
					throw new InvalidOperationException( "Error in prepare : " + GetErrorString( iErr ) );
				}
			} catch( InvalidOperationException oEx ) {
				// Need to catch outofmemory exception too.
				if( pHwParams != null )
					snd_pcm_hw_params_free( pHwParams );
                
				snd_pcm_close( _pPcmHandle );
				_pPcmHandle = null;

				throw oEx;
			}
		}

        /// <summary>
        /// This is the easy stuff. Pretty clear what these values need to be.
        /// </summary>
        /// <param name="pHwParams">The hardware params object.</param>
		void SetSampleStuffs( void * pHwParams ) {
			int iErr = 0;

			iErr = snd_pcm_hw_params_set_access( _pPcmHandle, pHwParams, snd_pcm_access_t.SND_PCM_ACCESS_RW_INTERLEAVED );
            if( iErr < 0 ) {
                string strErr = GetErrorString( iErr );
                throw new InvalidOperationException( "Error setting access : " + strErr );
            }

            iErr = snd_pcm_hw_params_set_format( _pPcmHandle, pHwParams, snd_pcm_format_t.SND_PCM_FORMAT_S16 );
            if( iErr < 0 ) {
                string strErr = GetErrorString( iErr );
                throw new InvalidOperationException( "Error setting format : " + strErr );
            }

            iErr = snd_pcm_hw_params_set_rate( _pPcmHandle, pHwParams, (uint)_oSpec.Rate, 0);
            if( iErr < 0 ) {
                string strErr = GetErrorString( iErr );
                throw new InvalidOperationException( "Error setting rate : " + strErr );
            }

            iErr = snd_pcm_hw_params_set_channels( _pPcmHandle, pHwParams, (uint)_oSpec.Channels );
            if( iErr < 0 ) {
                string strErr = GetErrorString( iErr );
                throw new InvalidOperationException( "Error setting channels : " + strErr );
            }
        }

        /// <summary>
		/// This bit of magic is lifted from Xiph LibAO. I wanted to set the buffer sizes
		/// based on the periods and latency times I wanted but ALSA just does not want to
		/// play along. It looks like the Xiph guys just set the period time and buffer time
		/// and let ALSA give us the period size. Seems odd but let's see if it works.
        /// </summary>
        /// <param name="pHwParams">Parameters pointer</param>
        /// <param name="period_time_us">period time in microseconds.</param>
		void SetBufferStuffs( void * pHwParams, uint period_time_us ) {
			int  iErr           = 0;
            int  iDir           = 0;
            uint buffer_time_us = period_time_us * _uiPeriods;

            iErr = snd_pcm_hw_params_set_period_time_near(_pPcmHandle, pHwParams, ref period_time_us, ref iDir );
            if( iErr < 0 ){
                string strErr = GetErrorString( iErr );
				throw new InvalidOperationException("snd_pcm_hw_params_set_period_time_near() failed : " + strErr );
            }

			iDir = 0;
			iErr = snd_pcm_hw_params_set_buffer_time_near(_pPcmHandle, pHwParams, ref buffer_time_us, ref iDir );
            if( iErr < 0 ){
                string strErr = GetErrorString( iErr );
				throw new InvalidOperationException("snd_pcm_hw_params_set_buffer_time_near() failed : " + strErr );
            }
		}

		public Specification Spec => _oSpec;
		public uint          Busy => throw new NotImplementedException();

		string GetErrorString( int iError ) {
			StringBuilder sbBuild = new StringBuilder();

			byte * pbString = snd_strerror( iError );

			while( *pbString != 0 ) {
				sbBuild.Append( Convert.ToChar( *pbString ) );
				pbString++;
			}

			return( sbBuild.ToString() );
		}

		public uint Play( IPgReader oBuffer ) {
			if( oBuffer == null )
				throw new ArgumentNullException( "Buffer interface must not be null" );

			uint uiWait = 0;

            // We're getting one or two more frames than calculated on my Mint machine. 
			// Probably a "near" issue. But on my PI the number is just nonsense!
            // Also note we get the entire play buffer of frames available on first go around.
            // Most of the time I expect we'll only fill a period size of the buffer.
			long lAvailableFrames = snd_pcm_avail_update( _pPcmHandle );

			if( lAvailableFrames == 0 ) {
				return _oSpec.GetMSLatency( _uiPlayByteSize );
		    }
			if( lAvailableFrames < 0 ) {
                snd_pcm_prepare( _pPcmHandle );
                string strError = GetErrorString( (int)lAvailableFrames );
                return 0;
            }
			if( lAvailableFrames > _oSpec.GetFrameCount( _uiPlayByteSize ) ) {
				lAvailableFrames = _oSpec.GetFrameCount( _uiPlayByteSize );
			}

            uint uiAvailBytes = _oSpec.GetByteCount( (uint)lAvailableFrames );
			uint uiReadBytes  =  oBuffer.Read( _ipPlayBuff, uiAvailBytes );
		    uint uiReadFrames = _oSpec.GetFrameCount( uiReadBytes );

            // We'll need to do better for gapless, but this will do for now.
			if( uiReadFrames == 0 )
				return 0;

			long iFrames = snd_pcm_writei( _pPcmHandle, _ipPlayBuff.ToPointer(), uiReadFrames );
			if( iFrames < 0 || iFrames > _oSpec.GetFrameCount( _uiPlayByteSize ) )
			    snd_pcm_prepare( _pPcmHandle );

			uiWait += _oSpec.GetMSLatency( uiReadBytes );

			return uiWait;
		}

		private bool _fDisposed = false; // To detect redundant calls

		protected virtual void Dispose(bool disposing) {
			if (!_fDisposed) {
				if (disposing) {
					// TODO: dispose managed state (managed objects).
				}

				// TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
				// TODO: set large fields to null.
				if( _pPcmHandle != null ) {
					snd_pcm_close( _pPcmHandle ); 
					_pPcmHandle = null;
				}
				if( _ipPlayBuff != null ) {
					Marshal.FreeHGlobal( _ipPlayBuff );
					_ipPlayBuff = IntPtr.Zero;
				}

				_fDisposed = true;
			}
		}

		// TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
		~Alsa() {
			// Do not change this code. Put cleanup code in Dispose(bool disposing) above.
			Dispose(false);
		}

		// This code added to correctly implement the disposable pattern.
		public void Dispose() {
			// Do not change this code. Put cleanup code in Dispose(bool disposing) above.
			Dispose(true);
			// TODO: uncomment the following line if the finalizer is overridden above.
			GC.SuppressFinalize(this);
		}
	}
}
