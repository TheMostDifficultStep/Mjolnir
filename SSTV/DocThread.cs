using System;
using System.IO;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Threading;
using System.Text;

using NAudio.Wave;
using NAudio.Wave.SampleProviders;

using Play.Interfaces.Embedding;
using Play.Sound;
using Play.Sound.FFT;
using System.Collections;

namespace Play.SSTV
{
    public class ThreadWorkerBase {
        protected readonly ConcurrentQueue<SSTVEvents> _oMsgQueue;

        protected readonly SSTVMode _oFixedMode;

        public SSTVMode NextMode => SSTVDraw.Mode;

        public SSTVDraw SSTVDraw        { get; protected set; }
        public SSTVDEM  SSTVDeModulator { get; protected set; }

        public virtual string SuggestedFileName => string.Empty;

        public ThreadWorkerBase( ConcurrentQueue<SSTVEvents> oMsgQueue, SSTVMode oMode ) {
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

        public ThreadWorker( ConcurrentQueue<SSTVEvents> oMsgQueue, string strFileName, SSTVMode oMode ) : base( oMsgQueue, oMode ){
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

                    _oMsgQueue.Enqueue( SSTVEvents.ThreadReadException );

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

                oDemod.Send_NextMode += Listen_NextRxMode;
                SSTVDraw.Send_TvEvents += Listen_TvEvents;

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

                _oMsgQueue.Enqueue( SSTVEvents.ThreadWorkerException );
            }
        }

        /// <summary>
        /// Listen to the SSTVDraw object. I can probably stop listening to the decoder
        /// and have it do that directly.
        /// </summary>
        /// <seealso cref="Listen_NextRxMode"/>
        private void Listen_TvEvents( SSTVEvents eProp ) {
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

    public class ThreadWorker2 : ThreadWorkerBase, IEnumerable<double> {
        protected readonly ConcurrentQueue<double>    _oDataQueue; 
        protected readonly WaveFormat                 _oDataFormat;
        protected readonly ConcurrentQueue<TVMessage> _oOutQueue;

        public override string SuggestedFileName {
            get {
                DateTime sNow = DateTime.Now;
                StringBuilder sbName = new StringBuilder();

                sbName.Append( sNow.Year  .ToString( "D4" ) );
                sbName.Append( '-' );
                sbName.Append( sNow.Month .ToString( "D2" ) );
                sbName.Append( '-' );
                sbName.Append( sNow.Day   .ToString( "D2" ) );
                sbName.Append( '_' );
                sbName.Append( sNow.Hour  .ToString( "D2" ) );
                sbName.Append( sNow.Minute.ToString( "D2" ) );
                sbName.Append( 'p' );
               
                return sbName.ToString();
            }
        }

        // This is the errors we generally handle in our work function.
        protected static Type[] _rgStdErrors = { typeof( NullReferenceException ),
                                                 typeof( ArgumentNullException ),
                                                 typeof( MMSystemException ),
                                                 typeof( InvalidOperationException ),
										         typeof( ArithmeticException ),
										         typeof( IndexOutOfRangeException ) };
        protected static Type[] _rgInitErrors = { typeof( DirectoryNotFoundException ),
                                                typeof( NullReferenceException ),
                                                typeof( ApplicationException ),
                                                typeof( FileNotFoundException ) };

        public ThreadWorker2( WaveFormat oFormat, 
                              ConcurrentQueue<SSTVEvents> oMsgQueue, 
                              ConcurrentQueue<double> oDataQueue, 
                              ConcurrentQueue<TVMessage> oOutQueue ) : 
            base( oMsgQueue, null )
        {
            _oDataQueue  = oDataQueue ?? throw new ArgumentNullException( "oDataQueue" );
            _oDataFormat = oFormat    ?? throw new ArgumentNullException( "oFormat" );
            _oOutQueue   = oOutQueue  ?? throw new ArgumentNullException( "oOutQueue" );
        }

        /// <summary>
        /// Listen to the SSTVDraw object. And forward those events outside our
        /// thread envelope.
        /// </summary>
        /// <seealso cref="Listen_NextRxMode"/>
        private void OnSSTVDrawEvent( SSTVEvents eProp ) {
            _oMsgQueue.Enqueue( eProp );
        }

        /// <summary>
        /// This is the entry point for our new thread. We load and use the decoder and 
        /// converter from this thread. The UI thread looks at the RX and 12 bitmaps
        /// from time to time. Errors are passed via message to the UI.
        /// </summary>
        /// <remarks>I'll move this to the base class after I sort things out.</remarks>
        public void DoWork() {
            try {
			    SSTVDeModulator  = new SSTVDEM( new SYSSET(),
										        _oDataFormat.SampleRate, 
										        _oDataFormat.SampleRate, 
										        0 );
			    SSTVDraw         = new SSTVDraw( SSTVDeModulator );

                // Set the callbacks first since Start() will try to use the callback.
                SSTVDeModulator.Send_NextMode += new NextMode( SSTVDraw.ModeTransition );
                SSTVDraw       .Send_TvEvents += OnSSTVDrawEvent;
            } catch( Exception oEx ) {
                if( _rgInitErrors.IsUnhandled( oEx ) )
                    throw;

                _oMsgQueue.Enqueue( SSTVEvents.ThreadAbort );
                return;
            }

            do {
                try {
                    if( _oOutQueue.TryDequeue( out TVMessage oMsg ) ) {
                        switch( oMsg._eMsg ) {
                            case TVMessage.Message.ExitWorkThread:
                                _oMsgQueue.Enqueue( SSTVEvents.ThreadExit );
                                return;
                            case TVMessage.Message.TryNewMode:
                                if( oMsg._oParam is SSTVMode oMode ) {
                                    SSTVDeModulator.Start( oMode );
                                } else {
                                    if( oMsg._oParam == null )
                                        SSTVDeModulator.Reset();
                                    else
                                        _oMsgQueue.Enqueue( SSTVEvents.ThreadReadException );
                                }
                                break;
                        }
                    }
                    foreach( double dblValue in this )
                        SSTVDeModulator.Do( dblValue );

                    SSTVDraw.Process();

                    Thread.Sleep( 250 );
				} catch( Exception oEx ) {
                    if( _rgStdErrors.IsUnhandled( oEx ) )
                        throw;

                    _oMsgQueue.Enqueue( SSTVEvents.ThreadReadException );
                }
            } while( true );
        }

        public IEnumerator<double> GetEnumerator() {
            do {
                // Basically there should always be data. If not then we're done.
                if( _oDataQueue.TryDequeue( out double dblValue ) ) {
                    yield return dblValue;
                } else {
                    yield break;
                }
            } while( !_oDataQueue.IsEmpty );
        }

        IEnumerator IEnumerable.GetEnumerator() {
            return GetEnumerator();
        }
    }
}
