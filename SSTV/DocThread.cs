using System;
using System.IO;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Threading;

using SkiaSharp;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

using Play.Interfaces.Embedding;
using Play.Sound;
using Play.Sound.FFT;

namespace Play.SSTV
{
    public class ThreadWorkerBase {
        protected readonly ConcurrentQueue<ESstvProperty> _oMsgQueue;

        protected readonly SSTVMode _oFixedMode;

        public SSTVMode NextMode => SSTVDraw.Mode;

        public SSTVDraw SSTVDraw        { get; protected set; }
        public SSTVDEM  SSTVDeModulator { get; protected set; }

        public virtual string SuggestedFileName => string.Empty;

        public ThreadWorkerBase( ConcurrentQueue<ESstvProperty> oMsgQueue, SSTVMode oMode ) {
            _oMsgQueue  = oMsgQueue   ?? throw new ArgumentNullException( "Queue is null" );

            _oFixedMode = oMode; // Override the auto sense and just fix ourselves trying to receive a particular signal type.
        }
    }

    /// <summary>
    /// Encapsulate everything I need to decode a SSTV image from a WAV file. In the
    /// future I'll make a version of this which is reading from an audio input device.
    /// </summary>
    public class ThreadWorker : ThreadWorkerBase {
        public readonly string _strFileName;
        public override string SuggestedFileName => _strFileName;

        public ThreadWorker( ConcurrentQueue<ESstvProperty> oMsgQueue, string strFileName, SSTVMode oMode ) : base( oMsgQueue, oMode ){
            _strFileName = strFileName ?? throw new ArgumentNullException( "Filename is null" );
        }

        public IEnumerator<int> GetReceiveFromFileTask( WaveStream oStream ) {
            var oReader = new WaveToSampleProvider(oStream);

            int     iChannels = oStream.WaveFormat.Channels;
            int     iBits     = oStream.WaveFormat.BitsPerSample; 
            float[] rgBuff    = new float[1500]; // TODO: Make this scan line sized in the future.
            int     iRead     = 0;

            double From32to16( int i ) => rgBuff[i] * 32768;
            double From16to16( int i ) => rgBuff[i];

            Func<int, double> ConvertInput = iBits == 16 ? From16to16 : (Func<int, double>)From32to16;

            do {
                try {
                    iRead = oReader.Read( rgBuff, 0, rgBuff.Length );
                    for( int i = 0; i< iRead; ++i ) {
                        SSTVDeModulator.Do( ConvertInput(i) );
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
                using var oStream = new AudioFileReader(_strFileName); 

			    FFTControlValues oFFTMode  = FFTControlValues.FindMode( oStream.WaveFormat.SampleRate ); // RxSpec.Rate
			    SYSSET  sys       = new ();
			    SSTVDEM oDemod    = new SSTVDEM( sys,
												 oFFTMode.SampFreq, 
												 oFFTMode.SampBase, 
												 0 );

                SSTVDeModulator = oDemod;
			    SSTVDraw        = new SSTVDraw( oDemod );

                oDemod.ShoutNextMode += Listen_NextRxMode;
                SSTVDraw.ShoutTvEvents += Listen_TvEvents;

                if( _oFixedMode != null ) {
                    // Hard code our decoding mode... After set the callbacks
                    // since this causes a call to the ShoutNextMode()
                    SSTVDeModulator.Start( _oFixedMode );
                }

                for( IEnumerator<int> oIter = GetReceiveFromFileTask( oStream ); oIter.MoveNext(); ) {
                    SSTVDraw.Process();
                }
                SSTVDraw.Stop();
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
                SSTVDraw.ModeTransition( tvMode ); // bitmap allocated in here.
            } catch( ArgumentOutOfRangeException ) {
            }
        }
    }

    public class ThreadWorker2 : ThreadWorkerBase {
        protected readonly ConcurrentQueue<double> _oDataQueue; 
        protected readonly WaveFormat              _oDataFormat;

        public override string SuggestedFileName => DateTime.Now.ToString();

        public ThreadWorker2( WaveFormat oFormat, ConcurrentQueue<ESstvProperty> oMsgQueue, ConcurrentQueue<double> oDataQueue, SSTVMode oMode ) : base( oMsgQueue, oMode ) {
            _oDataQueue  = oDataQueue ?? throw new ArgumentNullException( "oDataQueue" );
            _oDataFormat = oFormat    ?? throw new ArgumentNullException( "oFormat" );
        }

        /// <summary>
        /// Listen to the SSTVDraw object. And forward those events outside our
        /// thread envelope.
        /// </summary>
        /// <seealso cref="Listen_NextRxMode"/>
        private void OnSSTVDrawEvent( ESstvProperty eProp ) {
            _oMsgQueue.Enqueue( eProp );
        }

        // https://markheath.net/post/how-to-record-and-play-audio-at-same

        /// <summary>
        /// This is the entry point for our new thread. We load and use the decoder and 
        /// converter from this thread. The UI thread looks at the RX and 12 bitmaps
        /// from time to time. Errors are passed via message to the UI.
        /// </summary>
        public void DoWork() {
            try {
			    FFTControlValues oFFTMode = FFTControlValues.FindMode( _oDataFormat.SampleRate ); // RxSpec.Rate
			    SYSSET           oSys     = new ();

			    SSTVDeModulator  = new SSTVDEM( oSys,
										        oFFTMode.SampFreq, 
										        oFFTMode.SampBase, 
										        0 );
			    SSTVDraw         = new SSTVDraw( SSTVDeModulator );

                // Set the callbacks first since Start() will try to use the callback.
                SSTVDeModulator.ShoutNextMode += new NextMode( SSTVDraw.ModeTransition );
                SSTVDraw       .ShoutTvEvents += OnSSTVDrawEvent;

                if( _oFixedMode != null ) {
                    SSTVDeModulator.Start( _oFixedMode );
                }

                do {
                    try {
                        if( !_oDataQueue.IsEmpty ) {
                            do {
                                if( _oDataQueue.TryDequeue( out double dblValue ) )
                                    SSTVDeModulator.Do( dblValue );
                                else
                                    break; // I should send an exception to the ui. Let it go for now.
                            } while( !_oDataQueue.IsEmpty );
                            SSTVDraw.Process();
                        }
                        Thread.Sleep( 25 );
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
                    }
                } while( true );
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
    }
}
