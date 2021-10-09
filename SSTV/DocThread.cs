using System;
using System.IO;
using System.Collections.Generic;
using System.Collections.Concurrent;

using SkiaSharp;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

using Play.Interfaces.Embedding;
using Play.Sound;
using Play.Sound.FFT;

namespace Play.SSTV
{
    /// <summary>
    /// Encapsulate everything I need to decode a SSTV image from a WAV file. In the
    /// future I'll make a version of this which is reading from an audio input device.
    /// </summary>
    public class ThreadWorker {
        readonly string _strFileName;
        readonly ConcurrentQueue<ESstvProperty> _oMsgQueue;

        protected readonly SSTVMode _oFixedMode;

        public SSTVMode NextMode => RxSSTV.Mode;
        public SSTVDraw  RxSSTV   { get; protected set; }

        SSTVDEM        _oSSTVDeModulator;

        public ThreadWorker( ConcurrentQueue<ESstvProperty> oMsgQueue, string strFileName, SSTVMode oMode ) {
            _strFileName = strFileName ?? throw new ArgumentNullException( "Filename is null" );
            _oMsgQueue   = oMsgQueue   ?? throw new ArgumentNullException( "Queue is null" );

            _oFixedMode = oMode; // Override the auto sense and just fix ourselves trying to receive a particular signal type.
        }

        public IEnumerator<int> GetReceiveFromFileTask( AudioFileReader oReader ) {
            var foo = new WaveToSampleProvider(oReader);

            int     iChannels = oReader.WaveFormat.Channels;
            int     iBits     = oReader.WaveFormat.BitsPerSample; 
            float[] rgBuff    = new float[1500]; // TODO: Make this scan line sized in the future.
            int     iRead     = 0;

            double From32to16( int i ) => rgBuff[i] * 32768;
            double From16to16( int i ) => rgBuff[i];

            Func<int, double> ConvertInput = iBits == 16 ? From16to16 : (Func<int, double>)From32to16;

            do {
                try {
                    iRead = foo.Read( rgBuff, 0, rgBuff.Length );
                    for( int i = 0; i< iRead; ++i ) {
                        _oSSTVDeModulator.Do( ConvertInput(i) );
                    }
				} catch( Exception oEx ) {
                    Type[] rgErrors = { typeof( NullReferenceException ),
                                        typeof( ArgumentNullException ),
                                        typeof( MMSystemException ),
                                        typeof( InvalidOperationException ),
										typeof( ArithmeticException ),
										typeof( IndexOutOfRangeException ) };
                    if( rgErrors.IsUnhandled( oEx ) )
                        throw;

                    _oMsgQueue.Enqueue( ESstvProperty.ThreadReadException );

					// Don't call _oWorkPlace.Stop() b/c we're already in DoWork() which will
					// try calling the _oWorker which will have been set to NULL!!
                    break; // Drop down so we can unplug from our Demodulator.
                }
                yield return 0; 
            } while( iRead == rgBuff.Length );
        }

        /// <summary>
        /// This is the entry point for our new thread. We load and use the decoder and 
        /// converter from this thread. The UI thread looks at the RX and 12 bitmaps
        /// from time to time. Errors are passed via message to the UI.
        /// </summary>
        public void DoWork() {
            try {
                using var oReader = new AudioFileReader(_strFileName); 

			    FFTControlValues oFFTMode  = FFTControlValues.FindMode( oReader.WaveFormat.SampleRate ); // RxSpec.Rate
			    SYSSET  sys       = new ();
			    SSTVDEM oDemod    = new SSTVDEM( sys,
												 oFFTMode.SampFreq, 
												 oFFTMode.SampBase, 
												 0 );

                _oSSTVDeModulator = oDemod;
			    RxSSTV            = new SSTVDraw( oDemod );

                oDemod.ShoutNextMode += Listen_NextRxMode;
                RxSSTV.ShoutTvEvents += Listen_TvEvents;

                if( _oFixedMode != null ) {
                    // Hard code our decoding mode... After set the callbacks
                    // since this causes a call to the ShoutNextMode()
                    _oSSTVDeModulator.Start( _oFixedMode );
                }

                for( IEnumerator<int> oIter = GetReceiveFromFileTask( oReader ); oIter.MoveNext(); ) {
                    RxSSTV.Process();
                }
                RxSSTV.Stop();
            } catch( Exception oEx ) {
                Type[] rgErrors = { typeof( DirectoryNotFoundException ),
                                    typeof( NullReferenceException ),
                                    typeof( ApplicationException ),
                                    typeof( FileNotFoundException ) };
                if( rgErrors.IsUnhandled( oEx ) )
                    throw;

                _oMsgQueue.Enqueue( ESstvProperty.ThreadWorkerException );
            }
        }

        /// <summary>
        /// Listen to the SSTVDraw object. I can probably stop listening to the decoder
        /// and have it do that directly.
        /// </summary>
        /// <seealso cref="Listen_NextRxMode"/>
        private void Listen_TvEvents( ESstvProperty eProp ) {
            _oMsgQueue.Enqueue( eProp );
        }

        /// <summary>
        /// Listen to the decoder when it spots a new image. DO NOT 
        /// enqueue ESstvProperty.SSTVMode to the UI msg queue. TmmSSTV
        /// will Shout that as a TvEvent.
        /// </summary>
        /// <remarks>The bitmap only changes when the mode changes and
        /// the next image isn't necessarily a different mode. I need to
        /// separate out those events.</remarks>
        private void Listen_NextRxMode( SSTVMode tvMode ) {
            try {
                RxSSTV.ModeTransition( tvMode ); // bitmap allocated in here.
            } catch( ArgumentOutOfRangeException ) {
            }
        }
    }
}
