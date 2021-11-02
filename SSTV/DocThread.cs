using System;
using System.IO;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Threading;
using System.Text;

using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using SkiaSharp;

using Play.Interfaces.Embedding;
using Play.Sound;
using Play.Sound.FFT;
using System.Collections;

namespace Play.SSTV {
    public class ThreadWorkerBase {
        protected readonly ConcurrentQueue<SSTVEvents> _oToUIQueue;

        public SSTVMode NextMode => SSTVDraw.Mode;

        public SSTVDraw SSTVDraw        { get; protected set; }
        public SSTVDEM  SSTVDeModulator { get; protected set; }

        public virtual string SuggestedFileName => string.Empty;
        public virtual bool   IsForever         => false;

        protected SKBitmap _oD12Bmp;
        protected SKBitmap _oRxBmp;

        public ThreadWorkerBase( ConcurrentQueue<SSTVEvents> oToUIQueue, SKBitmap oD12Bmp, SKBitmap oRxBmp ) {
            _oToUIQueue = oToUIQueue ?? throw new ArgumentNullException( "Queue is null" );
            _oD12Bmp    = oD12Bmp    ?? throw new ArgumentNullException( "D12 Bmp is null" );
            _oRxBmp     = oRxBmp     ?? throw new ArgumentNullException( "Rx Bmp is null" );
        }
    }

    /// <summary>
    /// Encapsulate everything I need to decode a SSTV image from a WAV file. In the
    /// future I'll make a version of this which is reading from an audio input device.
    /// </summary>
    public class ThreadWorker : ThreadWorkerBase {
        public readonly string   _strFileName;
        public readonly SSTVMode _oStartMode;

        public override string SuggestedFileName => _strFileName;

        public ThreadWorker( ConcurrentQueue<SSTVEvents> oMsgQueue, string strFileName, SSTVMode oMode, SKBitmap oD12, SKBitmap oRx ) : 
            base( oMsgQueue, oD12, oRx )
        {
            _strFileName = strFileName ?? throw new ArgumentNullException( "Filename is null" );
            _oStartMode  = oMode;
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

            if( _oStartMode != null ) {
                SSTVDeModulator.Start( _oStartMode );
            }

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

                    _oToUIQueue.Enqueue( SSTVEvents.ThreadReadException );

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

                SSTVDeModulator = new SSTVDEM ( new SYSSET(), oFFTMode.SampFreq );;
			    SSTVDraw        = new SSTVDraw( SSTVDeModulator, _oD12Bmp, _oRxBmp );

                SSTVDeModulator.Send_NextMode += Listen_NextRxMode;
                SSTVDraw       .Send_TvEvents += Listen_TvEvents;

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

                _oToUIQueue.Enqueue( SSTVEvents.ThreadWorkerException );
            }
        }

        /// <summary>
        /// Listen to the SSTVDraw object. I can probably stop listening to the decoder
        /// and have it do that directly.
        /// </summary>
        /// <seealso cref="Listen_NextRxMode"/>
        private void Listen_TvEvents( SSTVEvents eProp ) {
            _oToUIQueue.Enqueue( eProp );
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
                SSTVDraw.OnModeTransition_SSTVMod( tvMode ); // bitmap allocated in here.
            } catch( ArgumentOutOfRangeException ) {
            }
        }
    }

    public class ThreadWorker2 : ThreadWorkerBase, IEnumerable<double> {
        protected readonly ConcurrentQueue<double>    _oDataQueue; 
        protected readonly WaveFormat                 _oDataFormat;
        protected readonly ConcurrentQueue<TVMessage> _oOutQueue;

        public override bool IsForever => true;

        public override string SuggestedFileName {
            get {
                DateTime sNow = DateTime.Now.ToUniversalTime();
                StringBuilder sbName = new StringBuilder();

                sbName.Append( sNow.Year  .ToString( "D4" ) );
                sbName.Append( '-' );
                sbName.Append( sNow.Month .ToString( "D2" ) );
                sbName.Append( '-' );
                sbName.Append( sNow.Day   .ToString( "D2" ) );
                sbName.Append( '_' );
                sbName.Append( sNow.Hour  .ToString( "D2" ) );
                sbName.Append( sNow.Minute.ToString( "D2" ) );
                sbName.Append( 'z' );
               
                return sbName.ToString();
            }
        }

        // This is the errors we generally handle in our work function.
        protected static Type[] _rgLoopErrors = { typeof( NullReferenceException ),
                                                  typeof( ArgumentNullException ),
                                                  typeof( MMSystemException ),
                                                  typeof( InvalidOperationException ),
										          typeof( ArithmeticException ),
										          typeof( IndexOutOfRangeException ),
                                                  typeof( InvalidDataException ) };

        protected static Type[] _rgInitErrors = { typeof( DirectoryNotFoundException ),
                                                  typeof( NullReferenceException ),
                                                  typeof( ApplicationException ),
                                                  typeof( FileNotFoundException ) };

        public ThreadWorker2( WaveFormat oFormat, 
                              ConcurrentQueue<SSTVEvents> oMsgQueue, 
                              ConcurrentQueue<double>     oDataQueue, 
                              ConcurrentQueue<TVMessage>  oOutQueue,
                              SKBitmap                    oD12,
                              SKBitmap                    oRx ) : 
            base( oMsgQueue, oD12, oRx )
        {
            _oDataQueue  = oDataQueue ?? throw new ArgumentNullException( "oDataQueue" );
            _oDataFormat = oFormat    ?? throw new ArgumentNullException( "oDataFormat" );
            _oOutQueue   = oOutQueue  ?? throw new ArgumentNullException( "oOutQueue" );
        }

        /// <summary>
        /// Listen to the SSTVDraw object. And forward those events outside our
        /// thread envelope.
        /// </summary>
        /// <seealso cref="Listen_NextRxMode"/>
        private void OnTvEvents_SSTVDraw( SSTVEvents eProp ) {
            _oToUIQueue.Enqueue( eProp );
        }

        /// <summary>
        /// We actually see a "ran out of data" when the shell is closed and we don't get
        /// a dispose event on the document which then would shut down this thread.
        /// The thread is still running and then finds itself starved for data.
        /// By sleeping the thread first, we give the system a chance to fill the
        /// data buffer when we spin it up. And this thread stays alive until receiving a 
        /// ExitWorkThread message, or if the thread becomes data starved.
        /// </summary>
        /// <returns></returns>
        public IEnumerator<double> GetEnumerator() {
            while( !_oDataQueue.IsEmpty ) {
                // Basically there should always be data. If not then we're done.
                if( _oDataQueue.TryDequeue( out double dblValue ) ) {
                    yield return dblValue;
                } else {
                    throw new InvalidDataException( "Ran out of data" );
                }
            }
        }

        protected void CheckMessages() {
            while( _oOutQueue.TryDequeue( out TVMessage oMsg ) ) {
                switch( oMsg._eMsg ) {
                    case TVMessage.Message.ExitWorkThread:
                        _oToUIQueue.Enqueue( SSTVEvents.ThreadExit );
                        return;
                    case TVMessage.Message.TryNewMode:
                        if( oMsg._oParam is SSTVMode oMode ) {
                            SSTVDeModulator.Start( oMode );
                        } else {
                            SSTVDeModulator.Reset();
                        }
                        break;
                }
            }
        }

        /// <summary>
        /// This is the entry point for our new thread. We load and use the decoder and 
        /// converter from this thread. The UI thread looks at the RX and 12 bitmaps
        /// from time to time. Errors are passed via message to the UI.
        /// </summary>
        /// <remarks>I'll move this to the base class after I sort things out.</remarks>
        public void DoWork() {
            try {
			    SSTVDeModulator  = new SSTVDEM ( new SYSSET(), _oDataFormat.SampleRate );
			    SSTVDraw         = new SSTVDraw( SSTVDeModulator, _oD12Bmp, _oRxBmp );

                // Set the callbacks first since Start() will try to use the callback.
                SSTVDeModulator.Send_NextMode += new NextMode( SSTVDraw.OnModeTransition_SSTVMod );
                SSTVDraw       .Send_TvEvents += OnTvEvents_SSTVDraw;
            } catch( Exception oEx ) {
                if( _rgInitErrors.IsUnhandled( oEx ) )
                    throw;

                _oToUIQueue.Enqueue( SSTVEvents.ThreadAbort );
                return;
            }

            try {
                do {
                    Thread.Sleep( 250 );

                    CheckMessages();

                    foreach( double dblValue in this )
                        SSTVDeModulator.Do( dblValue );

                    SSTVDraw.Process();
                } while( true );
			} catch( Exception oEx ) {
                if( _rgLoopErrors.IsUnhandled( oEx ) )
                    throw;

                _oToUIQueue.Enqueue( SSTVEvents.ThreadAbort );
            }
        }

        IEnumerator IEnumerable.GetEnumerator() {
            return GetEnumerator();
        }
    }
}
