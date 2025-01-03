﻿using System;
using System.IO;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using System.Text;

using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using SkiaSharp;

using Play.Interfaces.Embedding;
using Play.Sound;
using Play.ImageViewer;
using Play.Sound.FFT;
using System.Collections;
using System.IO.Ports;

namespace Play.SSTV {
    public abstract class BGThreadState :
        IPgParent
    {
        protected readonly ConcurrentQueue<SSTVMessage> _oToUIQueue;

        public IPgParent Parentage => throw new NotImplementedException();
        public IPgParent Services  => throw new NotImplementedException();

        protected class DocSlot :
			IPgBaseSite
		{
			protected readonly BGThreadState _oHost;

			public DocSlot( BGThreadState oHost, string strFileBase = "Not Implemented" ) {
				_oHost = oHost ?? throw new ArgumentNullException( "Host" );
			}

			public IPgParent Host => _oHost;

            public void LogError(string strMessage, string strDetails, bool fShow=true) {
				_oHost.LogError( strDetails );
			}

			public void Notify( ShellNotify eEvent ) {
			}
		}

        public BGThreadState( ConcurrentQueue<SSTVMessage> oToUIQueue )
        {
            _oToUIQueue  = oToUIQueue ?? throw new ArgumentNullException( nameof( oToUIQueue ) );
        }

        /// <summary>
        /// I'll change the ToUIQueue to pass strings accross. I don't know why
        /// I didn't just do that originally. I'm an old C programmer that's why!
        /// </summary>
        public void LogError( string strMessage ) {
            _oToUIQueue.Enqueue( new SSTVMessage(SSTVEvents.ThreadException, -1 ) );
        }

        public static string FileNameGenerate  {
            get {
                DateTime      sNow   = DateTime.Now.ToUniversalTime();
                StringBuilder sbName = new();

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

        public static string FileNameCleanUp( string strFileName ) {
            char[] rgLine    = strFileName.ToCharArray();
            char[] rgIllegal = Path.GetInvalidFileNameChars();

            for( int i = 0; i< rgLine.Length; ++i ) {
                foreach( char cBadChar in rgIllegal ) {
                    if( rgLine[i] == cBadChar )
                        rgLine[i] = '_';
                }
            }
            return new string( rgLine ); // So rgLine.ToString() doesn't work b/c array is ref type.
        }

        public abstract void SaveImage( SSTVMode tvMode );
    }

    /// <summary>
    /// Encapsulate everything I need to decode a SSTV image from a WAV file. It's
    /// different enough from the device read that it's a completely different
    /// object.
    /// </summary>
    /// <remarks>The down side of separating the Device Listener and the file read state
    /// objects is that the sound buffer is duplicated between the both of them.
    /// It's not the end of the world, since you're not likely to do both file read
    /// and device listen all at the same time. But it is a consideration.</remarks>
    /// <seealso cref="DeviceListeningState"/>
    public class FileReadingState : 
        BGThreadState,
        IEnumerable<int>
    {
        protected          int                  _iDecodeCount = 0;
        protected readonly string               _strFileName;
        protected readonly WaveToSampleProvider _oProvider;
        protected readonly AudioFileReader      _oReader;

        protected readonly SSTVDraw _oSSTVDraw;
        protected readonly SSTVDEM  _oSSTVDeMo;

        public FileReadingState( ConcurrentQueue<SSTVMessage> oToUIQueue, string strFileName, SKBitmap oD12, SKBitmap oRx ) :
            base( oToUIQueue )
        {
            _strFileName = strFileName ?? throw new ArgumentNullException( nameof( strFileName ) );

            if( oD12 == null )
                throw new ArgumentNullException( nameof( oD12 ) );
            if( oRx  == null )
                throw new ArgumentNullException( nameof( oRx  ) );

            _oReader     = new AudioFileReader     ( _strFileName ); // BUG: throws if not find file.
            _oProvider   = new WaveToSampleProvider( _oReader );
            
            // No need for calibrated clock rate since reading data from a file.
            // TODO: this might be a way for me to coordinated the calibrated
            // value with the specified value for the Device tx and rx...
			FFTControlValues oFFTMode  = FFTControlValues.FindMode( _oReader.WaveFormat.SampleRate );

            _oSSTVDeMo = new SSTVDEM ( new SYSSET(), oFFTMode.SampFreq );
			_oSSTVDraw = new SSTVDraw( _oSSTVDeMo, oD12, oRx );
        }

        public IEnumerator<int> GetEnumerator() {
            int     iChannels = _oReader.WaveFormat.Channels;
            int     iBits     = _oReader.WaveFormat.BitsPerSample; 
            float[] rgBuff    = new float[1500]; // TODO: Make this scan line sized in the future.
            int     iRead     = 0;

            double From32to16( int i ) => rgBuff[i] * 32768;
            double From16to16( int i ) => rgBuff[i];

            Func<int, double> ConvertInput = iBits == 16 ? From16to16 : (Func<int, double>)From32to16;

            do {
                try {
                    iRead = _oProvider.Read( rgBuff, 0, rgBuff.Length );
                    for( int i = 0; i< iRead; ++i ) {
                        _oSSTVDeMo.Do( ConvertInput(i) );
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

                    _oToUIQueue.Enqueue( new( SSTVEvents.ThreadException, "File Read Exception" ) );

                    break; // Drop down so we can unplug from our Demodulator.
                }
                yield return 0; 
            } while( iRead == rgBuff.Length && iRead > 0 );
        }

        /// <summary>
        /// This is the entry point for our new thread. We load and use the decoder and 
        /// converter from this thread. The UI thread looks at the RX and 12 bitmaps
        /// from time to time. Errors are passed via message to the UI.
        /// </summary>
        public void DoWork( SSTVMode oMode ) {
            try {
                _oSSTVDeMo.Send_NextMode  = _oSSTVDraw.OnModeTransition_SSTVDeMo;
                _oSSTVDraw.Send_TvMessage = OnTVEvents_SSTVDraw;
                _oSSTVDraw.Send_SavePoint = SaveImage;

                // Note: SSTVDemodulator.Start() will try to use the callback(s) above.
                if( oMode != null ) {
                    _oSSTVDeMo.Start( oMode );
                }

                foreach( int i in this ) {
                    _oSSTVDraw.Process();
                }

		        _oSSTVDraw.ProcessAll();

                // Check if there's any leftover and if so, save it. Don't call
                // Stop()! That will happen automatically when the bitmap gets full.
                if( _oSSTVDraw.PercentRxComplete > 25 ) {
                    SaveImage( _oSSTVDraw.Mode );
                }
            } catch( Exception oEx ) {
                Type[] rgErrors = { typeof( DirectoryNotFoundException ),
                                    typeof( NullReferenceException ),
                                    typeof( ApplicationException ),
                                    typeof( FileNotFoundException ),
                                    typeof( ArgumentOutOfRangeException ) };
                if( rgErrors.IsUnhandled( oEx ) )
                    throw;

                // Never send TxThreadErrors.ThreadAbort, that's for the Device thread only.
                _oToUIQueue.Enqueue( new( SSTVEvents.ThreadException, "Decoding Thread Exception", oEx ) );
            }
        }

        /// <summary>
        /// Listen to the SSTVDraw object. 
        /// </summary>
        /// <seealso cref="OnNextMode_SSTVDemo"/>
        private void OnTVEvents_SSTVDraw( SSTVMessage sEvent ) {
            _oToUIQueue.Enqueue( sEvent );
        }

        IEnumerator IEnumerable.GetEnumerator() {
            return GetEnumerator();
        }

        public override void SaveImage( SSTVMode tvMode ) {
            if( tvMode == null ) {
                LogError( "Odd the current SSTVMode is null." );
                return;
            }

            try {
                using ImageSoloDoc oSnipDoc = new( new DocSlot( this ) );
			    SKRectI rcWorldDisplay = new SKRectI( 0, 0, tvMode.Resolution.Width, tvMode.Resolution.Height );

                // Need to snip the image since we might not be using the entire display image.
                if( !oSnipDoc.Load( _oSSTVDraw._pBitmapRX, rcWorldDisplay, rcWorldDisplay.Size ) )
                    return;

                _iDecodeCount++;

                // Figure out path and name of the file.
                string strFilePath = Path.GetDirectoryName( _strFileName );
                string strFileName = Path.GetFileNameWithoutExtension( _strFileName );
                string strModeName = tvMode.FamilyName + tvMode.Version.Replace( " ", string.Empty );
                string strSavePath = Path.Combine( strFilePath, strFileName + "_" + strModeName + "_" + _iDecodeCount.ToString() + ".jpg" );

                // Overrite any existing file!!
                using var stream   = File.OpenWrite( strSavePath );

                oSnipDoc.Save( stream );
            } catch( Exception oEx ) {
                Type[] rgErrors = { typeof( NullReferenceException ),
                                    typeof( IOException ),
                                    typeof( ArgumentException ),
                                    typeof( ArgumentNullException ),
                                    typeof( PathTooLongException ),
                                    typeof( DirectoryNotFoundException ), 
                                    typeof( NotSupportedException ),
                                    typeof( NullReferenceException ) };
                if( rgErrors.IsUnhandled( oEx ) )
                    throw;

                LogError( "Exception in File Decode Save" );
            }
        }
    }

    /// <summary>Use this object to hold the state of our device listening activities.</summary>
    /// <seealso cref="FileReadingState"/>
    public class DeviceListeningState : BGThreadState
    {
      //protected readonly ConcurrentQueue<double>      _oDataQueue; 
        protected readonly WaveFormat                   _oDataFormat;
        protected readonly ConcurrentQueue<TVMessage>   _oInputQueue;
        protected          string                       _strFilePath;   // path and img quality could potentially change
        protected readonly string                       _strFileName;
        protected readonly int                          _iImageQuality; // on the fly; yet doesn't seem mainline usage.

        protected          SSTVMode _oLastMode; // Help with image save.

        protected readonly SSTVDraw _oSSTVDraw;
        protected readonly SSTVDEM  _oSSTVDeMo;

        readonly double _dblSampRate;
        readonly int    _iSpeaker;
        readonly int    _iMicrophone;

        // This is the errors we generally handle in our work function.
        // But we will bail out of the loop.
        protected static readonly Type[] _rgLoopErrors = { typeof( NullReferenceException ),
                                                           typeof( ArgumentNullException ),
                                                           typeof( InvalidOperationException ),
										                   typeof( ArithmeticException ),
										                   typeof( IndexOutOfRangeException ),
                                                           typeof( InvalidDataException ),
                                                           typeof( ArgumentOutOfRangeException ),
                                                           typeof( KeyNotFoundException ),
                                                           typeof( ArgumentException ),
                                                           typeof( BadDeviceIdException ),
                                                           typeof( InvalidHandleException ),
                                                           typeof( MMSystemException ),
                                                           typeof( ThreadStateException ),
                                                           typeof( IOException )
                                                         };

        /// <summary>
        /// Support the bg thread for the device listener.
        /// </summary>
        /// <remarks>We can re-unify the two thread state objects if we get the WaveIn
        /// servicing this object to be initialized within THIS object, instead of
        /// outboard. Then we don't need to pass in the sample rate here and we can
        /// init the DeMo and Draw objects in the base!! (maybe -_-)
        /// 
        /// NOTE: I'm creating this object in the main thread and BUT the audio
        /// device is instantiated in the BG thread. The MMSystem sound devices
        /// MUST be created and used on the same (background) thread.
        /// </remarks>
        /// <exception cref="ArgumentNullException"></exception>
        public DeviceListeningState( int                          iMonitor,
                                     int                          iMicrophone,
                                     double                       dblSampleRate,
                                     int                          iImageQuality,
                                     string                       strFilePath,
                                     string                       strFileName,
                                     ConcurrentQueue<SSTVMessage> oToUIQueue, 
                                     ConcurrentQueue<TVMessage>   oInputQueue,
                                     SKBitmap                     oD12,
                                     SKBitmap                     oRx,
                                     int                          iThreadCnt ) :
            base( oToUIQueue )
        {
            _oInputQueue   = oInputQueue ?? throw new ArgumentNullException( nameof( oInputQueue   ) );
            _strFilePath   = strFilePath ?? throw new ArgumentNullException( nameof( strFilePath ) );
            _iImageQuality = iImageQuality;
            _strFileName   = strFileName;

            _iSpeaker      = iMonitor;
            _iMicrophone   = iMicrophone;
            _dblSampRate   = dblSampleRate;

            _oSSTVDeMo = new SSTVDEM ( new SYSSET(), dblSampleRate );
			_oSSTVDraw = new SSTVDraw( _oSSTVDeMo, oD12, oRx, iThreadCnt );
        }

        /// <summary>
        /// Listen to the SSTVDraw object. And forward those events outside our thread envelope.
        /// </summary>
        private void OnTvEvents_SSTVDraw( SSTVMessage sMessage ) {
            if( sMessage.Event == SSTVEvents.ModeChanged ) {
                foreach( SSTVMode oMode in _oSSTVDeMo ) {
                    if( oMode.LegacyMode == (AllSSTVModes)sMessage.Param ) {
                        _oLastMode = oMode;
                    }
                }
            }
            _oToUIQueue.Enqueue( sMessage );
        }

        /// <summary>
        /// Check our message queue. We try to dequeue all Frequency up/down
        /// message en mass if possible. I probably could even allow interleaved
        /// messages, but I'd need to store those lower pri messages on a 
        /// seperate temp queue. But in general, there isn't a lot of messages 
        /// coming thru, so we should be fine as we are.
        /// </summary>
        /// <returns></returns>
        protected bool CheckMessages() {
            while( _oInputQueue.TryDequeue( out TVMessage oMsg ) ) {
                // Try to fast dequeue any frequency up down messages.
                double dblSlant   = 0;
                int    iIntercept = 0;
                bool   fTally     = true;
                do {
                    switch( oMsg._eMsg ) {
                        case TVMessage.Message.Frequency:
                            dblSlant += oMsg._iParam / 100.0;
                            break;
                        case TVMessage.Message.Intercept:
                            iIntercept += oMsg._iParam;
                            break;
                        default:
                            fTally = false;
                            break;
                    }
                } while( fTally && _oInputQueue.TryDequeue( out oMsg ) );

                // If we picked up any slant correction, process that.
                if( dblSlant != 0 ) 
                    _oSSTVDraw.ManualSlopeAdjust( dblSlant );
                if( iIntercept != 0 )
                    _oSSTVDraw.ManualInterceptAdjust( iIntercept );

                // If we didn't get any more messages, just bail.
                if( oMsg == null )
                    break;

                switch( oMsg._eMsg ) {
                    case TVMessage.Message.ExitWorkThread:
                        _oToUIQueue.Enqueue( new( SSTVEvents.ThreadExit, 0 ) );
                        return false;
                    case TVMessage.Message.TryNewMode:
                        if( oMsg._oParam is SSTVMode oMode ) {
                            _oSSTVDeMo.Start( oMode );
                            _oSSTVDraw.ProcessAll();
                        } else {
                            _oSSTVDeMo.Reset();
                        }
                        break;
                    case TVMessage.Message.ChangeDirectory:
                        _strFilePath = oMsg._oParam as string;
                        break;
                    case TVMessage.Message.SaveNow:
                        SaveImage( _oLastMode );
                        _oToUIQueue.Enqueue( new( SSTVEvents.ImageSaved, 0 ) );
                        break;
                    case TVMessage.Message.Frequency:
                        _oSSTVDraw.ManualSlopeAdjust( oMsg._iParam / 100.0 );
                        break;
                    case TVMessage.Message.Intercept:
                        _oSSTVDraw.ManualInterceptAdjust( oMsg._iParam );
                        break;
                    case TVMessage.Message.ClearImage:
                        _oSSTVDraw.ClearImage();
                        break;
                }
            }
            return true;
        }

        //public void Consumer( short sValue ) {
        //    _oQueue.Enqueue( sValue );
        //}

        public void InitCallbacks() {
            try {
                _oSSTVDeMo.Send_NextMode  = _oSSTVDraw.OnModeTransition_SSTVDeMo;
                _oSSTVDraw.Send_TvMessage = OnTvEvents_SSTVDraw;
                _oSSTVDraw.Send_SavePoint = SaveImage;
            } catch( Exception oEx ) {
                Type[] rgErrors = { typeof( NullReferenceException ),
                                    typeof( ApplicationException ) };
                if( rgErrors.IsUnhandled( oEx ) )
                    throw;

                _oToUIQueue.Enqueue( new( SSTVEvents.ThreadAbort, 0 ) );
                return;
            }

        }

        /// <summary>
        /// This is the entry point for our device listener thread.
        /// Events within here are posted via message and are read in UI thread.
        /// </summary>
        /// <remarks>At present we rely on NAudio to pick up the sound samples
        /// in the UI thread. It sux but that's how they work. So that's why
        /// we need the DataQueue. In the future I'll write my own code to
        /// read from the sound card and I'll be able to punt that code.</remarks>
        public void DoWork() {
            InitCallbacks();

            ConcurrentQueue<short> oTVAudio = new();
            SoundHandler           oSound   = new( _iSpeaker, _iMicrophone, _dblSampRate, _oToUIQueue, oTVAudio );
            Thread                 oThread  = new( oSound.DoWork ) { Priority = ThreadPriority.AboveNormal };

            try {
                oThread.Start();

                while( CheckMessages() ) {
                    // Our sound thread can throw an exception and exit.
                    if( !oThread.IsAlive ) {
                        _oToUIQueue.Enqueue( new( SSTVEvents.ThreadAbort, 10 ) );
                        break;
                    }
                    while( oTVAudio.TryDequeue( out short dblValue ) ) {
                        _oSSTVDeMo.Do( (double)dblValue );
                    }

                    _oSSTVDraw .Process();
                    _oToUIQueue.Enqueue( new( SSTVEvents.DownloadLevels, "Levels", _oSSTVDeMo.CalcLevel( false ) ) );

                    // TODO: We can make this more responsive by using a semaphore.
                    Thread.Sleep( 100 );
                }

			} catch( Exception oEx ) {
                if( _rgLoopErrors.IsUnhandled( oEx ) )
                    throw;

                _oToUIQueue.Enqueue( new( SSTVEvents.ThreadAbort, 0 ) );
            }
            oSound._fContinue = false;

            _oToUIQueue.Enqueue( new( SSTVEvents.ThreadExit, 0 ) );
        }

        /// <summary>
        /// Save the image. 
        /// </summary>
		public async override void SaveImage( SSTVMode tvMode ) {
            if( tvMode == null ) {
                LogError( "Odd the current SSTVMode is null." );
                return;
            }

            Action oSaveAction = delegate () {
                // BE CAREFUL! We can only get away with this because the DocSlot only implements
                // the LogError and that is thread safe.
                // BUG: We're back in a race condition: If the bitmap get's blitzed right away by
                // a new transmission we might not have snipped it yet. I could just have a dedicated
                // SnipDoc to create the bitmap, then pass it to some custom save code. Let's see how it goes.
                try {
                    using ImageSoloDoc oSnipDoc = new( new DocSlot( this ) );
			        SKRectI rcWorldDisplay = new SKRectI( 0, 0, tvMode.Resolution.Width, tvMode.Resolution.Height );

                    // Need to snip the image since we might not be using the entire display image.
                    if( !oSnipDoc.Load( _oSSTVDraw._pBitmapRX, rcWorldDisplay, rcWorldDisplay.Size ) )
                        return;

                    // I could get the name of the file from the settings, HOWEVER then I have to deal
                    // with the file name possibly existing, and figure out all name overlap issues.
                    // So I'm going to punt for now and ignore the passed in file name. We still might
                    // collide but it's less likely.
                    // Path.GetFileNameWithoutExtension( _strFileName )
                
                    string strFileName = FileNameCleanUp( FileNameGenerate );
                    string strModeName = tvMode.FamilyName + tvMode.Version.Replace( " ", string.Empty );
                    string strFilePath = Path.Combine  ( _strFilePath, strFileName + "_" + strModeName + ".jpg" );
                    using var stream   = File.OpenWrite( strFilePath );

                    oSnipDoc.Save( stream );
                } catch( Exception oEx ) {
                    Type[] rgErrors = { typeof( NullReferenceException ),
                                        typeof( IOException ),
                                        typeof( ArgumentException ),
                                        typeof( ArgumentNullException ),
                                        typeof( PathTooLongException ),
                                        typeof( DirectoryNotFoundException ), 
                                        typeof( NotSupportedException ),
                                        typeof( NotImplementedException ) };
                    if( rgErrors.IsUnhandled( oEx ) )
                        throw;

                    LogError( "Exception in Device Image Save Thread: " + oEx.Message );
                }
            };

            Task oTask = new Task( oSaveAction );

            oTask.Start();

            await oTask;

            // Don't dispose task. It's a bit lengthy and we're not run
            // frequently enough to put pressure on the GC.
		}

    }

    /// <summary>
    /// This sound handler will read out sound from the sound card,
    /// load those values into a concurrent queue of short (monoaural)
    /// and play the sound through the speakers.
    /// </summary>
    public class SoundHandler {
        readonly ConcurrentQueue<SSTVMessage> _oToUIQueue;
        readonly ConcurrentQueue<short>       _oQueue;

        readonly double _dblSampRate;
        readonly int    _iSpeaker;
        readonly int    _iMicrophone;

        public bool _fContinue = true;
        public bool _fMonitor  = true;

        protected static readonly Type[] _rgLoopErrors = { typeof( NullReferenceException ),
                                                           typeof( ArgumentNullException ),
                                                           typeof( InvalidOperationException ),
										                   typeof( ArithmeticException ),
										                   typeof( IndexOutOfRangeException ),
                                                           typeof( InvalidDataException ),
                                                           typeof( ArgumentOutOfRangeException ),
                                                           typeof( ArgumentException ),
                                                           typeof( BadDeviceIdException ),
                                                           typeof( InvalidHandleException ),
                                                           typeof( MMSystemException )
                                                         };
        public SoundHandler( int    iSpeaker,
                             int    iMicrophone,
                             double dblSampleRate,
                             ConcurrentQueue<SSTVMessage> oToUIQueue,
                             ConcurrentQueue<short>       oQueue )
        {
            _oToUIQueue    = oToUIQueue ?? throw new ArgumentNullException( nameof( oToUIQueue ) );
            _oQueue        = oQueue     ?? throw new ArgumentNullException( nameof( oQueue ) );
            _iSpeaker      = iSpeaker;
            _iMicrophone   = iMicrophone;

            _dblSampRate   = dblSampleRate;
        }

        /// <summary>
        /// Turns out there's a bit of art to sampling sound. Too fast and waste time checking
        /// buffers that aren't loaded yet. Too slow and our monitor gets way behind and we
        /// risk buffers getting filled.
        /// </summary>
        public void DoWork() {
            int iMaxQueue = (int)Math.Pow( 10, 5 ); // = for const, .= for assign O.o

            // The player and/or reader MUST be created and used on the same thread.
            // Else if a sound device shuts down first randomly... We hang in
            // waveOutWrite()!!
            Specification oAudio  = new ( (long)_dblSampRate, 1, 0, 16 );
            BufferSSTV    oBuffer = new ( oAudio );
            WmmPlayer     oPlayer;
            WmmReader     oReader;

            try {
                oPlayer = new ( oAudio, _iSpeaker );
                oReader = new ( oAudio, _iMicrophone );
            } catch( Exception oEx ) {
                Type[] rgErrors = { typeof( BadDeviceIdException ),
                                    typeof( ArgumentException ),
                                    typeof( InvalidHandleException ) };
                if( rgErrors.IsUnhandled( oEx ) )
                    throw;

                _oToUIQueue.Enqueue( new( SSTVEvents.ThreadException, "Bad Device. Try another") );
                return;
            }

            // If we return samples slower than this, our audio level visuals are sluggish.
            const int iBestSleepInMs = 100;

            void MonitorOn( short s ) { 
                _oQueue.Enqueue( s );
                oBuffer.Write  ( s );
            };

            void MonitorOff( short s ) {
                _oQueue.Enqueue( s );
            }

            void MonitorOnly( short s ) {
                oBuffer.Write( s );
            }

            try {
                oReader.RecordStart();

                while( _fContinue ) {
                    Action<short> dConsumer = _fMonitor ? MonitorOn : MonitorOff;

                    // Check if the buffer is getting too full
                    if( _oQueue.Count > iMaxQueue ) {
                        if( _fMonitor )
                            dConsumer = MonitorOnly;
                        else
                            break; // Monitor is off, queue consumer not consuming, so....
                    }

                    int iReadSleepInMs = oReader.Read( dConsumer );
                    oPlayer.Play( oBuffer );

                    int iBufSleep = ( iReadSleepInMs >> 1 ) + 1;
                    int iMinSleep = iBufSleep > iBestSleepInMs ? iBestSleepInMs : iBufSleep;

                    Thread.Sleep( iMinSleep );
                }
            } catch ( Exception oEx ) {  
                if( _rgLoopErrors.IsUnhandled( oEx ) )
                    throw;

                _oToUIQueue.Enqueue( new( SSTVEvents.ThreadException, "General Sound Handle Error.", oEx ) );
            }
            // If we've bailed because of an exception in the loop
            // we'll hit it again when we try to stop.
            try {
                oReader.RecordStop();
            } catch( Exception oEx ) {
                if( _rgLoopErrors.IsUnhandled( oEx ) )
                    throw;
            }

            // And again when we try to close. Plow thru so we can close up.
            try {
                oReader.Dispose();
            } catch( Exception oEx ) {
                if( _rgLoopErrors.IsUnhandled( oEx ) )
                    throw;
            }
            // this is less likely. But in case we have a usb speaker problem..
            try {
                oPlayer.Dispose();
            } catch( Exception oEx ) {
                if( _rgLoopErrors.IsUnhandled( oEx ) )
                    throw;
            }

            _oToUIQueue.Enqueue( new( SSTVEvents.ThreadExit, 1 ) );
        }
    }

    /// <summary>
    /// Little experiment to attempt to read audio from my 7300. 
    /// Looks like only the CI-V control is showing up as COM5.
    /// but no COM port for usb audio... So this is currently untested..
    /// </summary>
    public class PortListening : DeviceListeningState {
        readonly BlockCopies _oWaveReader;
                 byte[]      _rgRawAudio; // Test value for the moment...
                 int         _iRawAudioLength = 0;
        readonly int         _iSleepMS = 100;
        readonly int         _iBitRate = 115200; // per second
        readonly int         _iPortIn;

        public PortListening( int iPort, int iMonitor, int iImageQuality,
                              string strFilePath,ConcurrentQueue<SSTVMessage> oToUIQueue,
                              ConcurrentQueue<TVMessage> oInputQueue, SKBitmap oD12, SKBitmap oRx, int iThreadCnt=3 ) : 
            base( iMonitor, -1, 0.0, iImageQuality, strFilePath, string.Empty, oToUIQueue,
                  oInputQueue, oD12, oRx, iThreadCnt )
        {
            int iBytesNeeded = (_iBitRate / 8) * (_iSleepMS / 1000) + 1000;

            _oWaveReader = new( iDecimation:1, iChannels:1, iChannel:0 );
            _rgRawAudio  = new byte[iBytesNeeded];
            _iPortIn     = iPort;
        }

        public void GetData( object sender, SerialDataReceivedEventArgs e ) {
            if( sender is SerialPort oPort ) {
                GetData( oPort );
            }
        }

        public bool GetData( SerialPort oPort ) {
            _iRawAudioLength = 0;
            int iRead = oPort.BytesToRead;

            if( iRead > 0 ) {
                try {
                    _iRawAudioLength = oPort.Read( _rgRawAudio, 0, iRead );
                } catch( Exception oEx ) {
                    if( _rgLoopErrors.IsUnhandled( oEx ) )
                        throw;

                    _oInputQueue.Enqueue( new TVMessage( TVMessage.Message.ExitWorkThread ) );
                    _oToUIQueue .Enqueue( new( SSTVEvents.ThreadAbort, 10 ) ); // BUG: I've got an enum somewhere...
                }
            } 

            return _iRawAudioLength > 0;
        }

        /// <summary>
        /// Alas, the IC-7300 is only showing CI-V on com5. The audio
        /// is not showing up as a com port. Tho' it does snow up as
        /// a USB Audio Codec (obviously, see the MMAudio stuff).
        /// The IC-705 shows two COM ports. >_<;;
        /// </summary>
        /// <remarks>
        /// So the dot net serial port example shows them create the
        /// serial port in the main thread and then read from it in a
        /// background thread. So I might be able to move the object 
        /// out...
        /// </remarks>
        public new void DoWork() {
            SerialPort oAudioPort;

            try {
                oAudioPort = new SerialPort( "COM" + _iPortIn.ToString() );
            } catch( IOException ) {
                _oToUIQueue.Enqueue( new( SSTVEvents.ThreadAbort, 11 ) );
                return;
            }

            try {
                oAudioPort.BaudRate       = _iBitRate;
                oAudioPort.Parity         = Parity.None;
                oAudioPort.StopBits       = StopBits.One;
                oAudioPort.DataBits       = 8;
                oAudioPort.Handshake      = Handshake.None;
                oAudioPort.ReadBufferSize = _rgRawAudio.Length;

                //oAudioPort.ReceivedBytesThreshold = oAudioPort.ReadBufferSize / 2;
                //oAudioPort.DataReceived           += GetData;

                oAudioPort.Open();

                while( CheckMessages() ) {
                    if( GetData( oAudioPort ) ) {
					    foreach( short sSample in new BlockCopies.SampleEnumerable( _oWaveReader, _rgRawAudio, _iRawAudioLength ) ) {
						    _oSSTVDeMo.Do( (double)sSample );
					    }

                        _oSSTVDraw .Process();
                        _oToUIQueue.Enqueue( new( SSTVEvents.DownloadLevels, "Levels", _oSSTVDeMo.CalcLevel( false ) ) );
                    }

                    Thread.Sleep( _iSleepMS );
                }
			} catch( Exception oEx ) {
                if( _rgLoopErrors.IsUnhandled( oEx ) )
                    throw;

                _oToUIQueue.Enqueue( new( SSTVEvents.ThreadAbort, 0 ) );
            } finally {
                 oAudioPort.Close();
            }

            _oToUIQueue.Enqueue( new( SSTVEvents.ThreadExit, 0 ) );
        }
    }
}
