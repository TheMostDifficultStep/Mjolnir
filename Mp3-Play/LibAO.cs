using System;
using System.Runtime.InteropServices;

namespace Play.Sound {
	enum AO_Byte_Format : int {
		AO_FMT_LITTLE = 1,
		AO_FMT_BIG    = 2,
		AO_FMT_NATIVE = 4
	}

	unsafe struct ao_sample_format {
		public ao_sample_format(  int iBits, int iRate, int iChannels ) {
			bits = iBits;
			rate = iRate;
			channels = iChannels;
			byte_format = (int)AO_Byte_Format.AO_FMT_NATIVE;
			matrix = null;
		}

		public int    bits;        /* bits per sample */
		public int    rate;        /* samples per second (in a single channel) */
		public int    channels;    /* number of audio channels */
		public int    byte_format; /* Byte ordering in sample, see constants below */
        public byte * matrix;      /* input channel location/ordering */
	}

	/// <summary>
	/// This is my second wrapper for the Xiph LibAO library. It's super crude since
	/// Xiph makes a blocking Play call.
	/// 
	/// So it turns out that they're blocking on the sleep which I put outside my code.
	/// I'm working on a IPgPlayer using the Advanced Linux Sound Architecture. When I
	/// finish that I'll dee six this one.
	/// </summary>
	unsafe public class LibAO : IPgPlayer {
		[DllImport ("libao.so")] private static extern void   ao_initialize();
		[DllImport ("libao.so")] private static extern void * ao_open_live( int iDriverID, void * fmt, void * pAOopt );
		[DllImport ("libao.so")] private static extern int    ao_default_driver_id();
		[DllImport ("libao.so")] private static extern int    ao_play( void * pAO, IntPtr pbBuffer, UInt32 uiPlay );
		[DllImport ("libao.so")] private static extern int    ao_close( void * pAO );
		[DllImport ("libao.so")] private static extern void   ao_shutdown();

		readonly Specification _oSpec;
		readonly uint          _uiBufferSize = 0;
		readonly uint          _uiPlayMS     = 100;
		         void *        _pAO          = null;
                 IntPtr        _ipBuffer     = IntPtr.Zero;
        
		public LibAO( Specification oSpec ) {
			_oSpec = oSpec ?? throw new ArgumentNullException();

			_uiBufferSize = oSpec.GetAlignedBufferSize( _uiPlayMS );
			_ipBuffer = Marshal.AllocHGlobal( (int)_uiBufferSize );

			ao_initialize();

			ao_sample_format fmt = new ao_sample_format( _oSpec.BitsPerSample, _oSpec.Rate, _oSpec.Channels );

			int iDefaultDriver = ao_default_driver_id();
			_pAO = ao_open_live( iDefaultDriver, &fmt, null );
		}

		public uint Play( IPgReader oBuffer ) {
			UInt32 uiRead = oBuffer.Read( _ipBuffer, _uiBufferSize );
			ao_play( _pAO, _ipBuffer, uiRead );
            
            // ao_play is syncronous so don't wait around.
			return( 0 ); 
		}

		public Specification Spec {
			get { return( _oSpec ); }
		}

		public uint Busy => throw new NotImplementedException();

		public void Dispose() {
			ao_close( _pAO );
			ao_shutdown();
			Marshal.FreeHGlobal( _ipBuffer );
			_ipBuffer = IntPtr.Zero;
		}
	} // End Class
} // End Namespace
