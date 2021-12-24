using System;
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

            _oReader     = new AudioFileReader     ( _strFileName ); 
            _oProvider   = new WaveToSampleProvider( _oReader );

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

                    _oToUIQueue.Enqueue( new( SSTVEvents.ThreadException, (int)TxThreadErrors.ReadException ) );

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
                _oSSTVDeMo.Send_NextMode  += _oSSTVDraw.OnModeTransition_SSTVDeMo;
                _oSSTVDraw.Send_TvEvents  += OnTVEvents_SSTVDraw;
                _oSSTVDraw.Send_SavePoint += SaveFileDecode;

                // Note: SSTVDemodulator.Start() will try to use the callback(s) above.
                if( oMode != null ) {
                    _oSSTVDeMo.Start( oMode );
                }

                foreach( int i in this ) {
                    _oSSTVDraw.Process();
                }

                // Check if there's any leftover and if so, save it. Don't call
                // Stop()! That will happen automatically when the bitmap gets full.
                if( _oSSTVDraw.PercentRxComplete > 25 ) {
                    SaveFileDecode( _oSSTVDraw.Mode );
                }
            } catch( Exception oEx ) {
                Type[] rgErrors = { typeof( DirectoryNotFoundException ),
                                    typeof( NullReferenceException ),
                                    typeof( ApplicationException ),
                                    typeof( FileNotFoundException ) };
                if( rgErrors.IsUnhandled( oEx ) )
                    throw;

                // Never send TxThreadErrors.ThreadAbort, that's for the Device thread only.
                _oToUIQueue.Enqueue( new( SSTVEvents.ThreadException, (int)TxThreadErrors.WorkerException ) );
            }
        }

        /// <summary>
        /// Listen to the SSTVDraw object. 
        /// </summary>
        /// <seealso cref="OnNextMode_SSTVDemo"/>
        private void OnTVEvents_SSTVDraw( SSTVEvents eProp, int iParam ) {
            _oToUIQueue.Enqueue( new( eProp, iParam ) );
        }

        IEnumerator IEnumerable.GetEnumerator() {
            return GetEnumerator();
        }

        public void SaveFileDecode( SSTVMode tvMode ) {
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
                string strModeName = tvMode.Name.Replace( " ", string.Empty );

                string strSavePath = Path.Combine( strFilePath, strFileName + "_" + strModeName + "_" + _iDecodeCount.ToString() + ".jpg" );
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
        protected readonly ConcurrentQueue<double>      _oDataQueue; 
        protected readonly WaveFormat                   _oDataFormat;
        protected readonly ConcurrentQueue<TVMessage>   _oOutQueue;
        protected          string                       _strFilePath;   // path and img quality could potentially change
        protected readonly string                       _strFileName;
        protected readonly int                          _iImageQuality; // on the fly; yet doesn't seem mainline usage.

        protected readonly SSTVDraw _oSSTVDraw;
        protected readonly SSTVDEM  _oSSTVDeMo;

        // This is the errors we generally handle in our work function.
        protected static readonly Type[] _rgLoopErrors = { typeof( NullReferenceException ),
                                                           typeof( ArgumentNullException ),
                                                           typeof( MMSystemException ),
                                                           typeof( InvalidOperationException ),
										                   typeof( ArithmeticException ),
										                   typeof( IndexOutOfRangeException ),
                                                           typeof( InvalidDataException ) };

        /// <summary>
        /// Support the bg thread for the device listener.
        /// </summary>
        /// <remarks>We can re-unify the two thread state objects if we get the WaveIn
        /// servicing this object to be initialized within THIS object, instead of
        /// outboard. Then we don't need to pass in the sample rate here and we can
        /// init the DeMo and Draw objects in the base!! (maybe -_-)</remarks>
        /// <exception cref="ArgumentNullException"></exception>
        public DeviceListeningState( int                          iSampleRate,
                                     int                          iImageQuality,
                                     string                       strFilePath,
                                     string                       strFileName,
                                     ConcurrentQueue<SSTVMessage> oToUIQueue, 
                                     ConcurrentQueue<double>      oDataQueue, 
                                     ConcurrentQueue<TVMessage>   oOutQueue,
                                     SKBitmap                     oD12,
                                     SKBitmap                     oRx ) :
            base( oToUIQueue )
        {
            _oDataQueue    = oDataQueue  ?? throw new ArgumentNullException( nameof( oDataQueue  ) );
            _oOutQueue     = oOutQueue   ?? throw new ArgumentNullException( nameof( oOutQueue   ) );
            _strFilePath   = strFilePath ?? throw new ArgumentNullException( nameof( strFilePath ) );
            _iImageQuality = iImageQuality;
            _strFileName   = strFileName;

            _oSSTVDeMo = new SSTVDEM ( new SYSSET(), iSampleRate );
			_oSSTVDraw = new SSTVDraw( _oSSTVDeMo, oD12, oRx );
        }

        /// <summary>
        /// Listen to the SSTVDraw object. And forward those events outside our thread envelope.
        /// </summary>
        private void OnTvEvents_SSTVDraw( SSTVEvents eProp, int iParam ) {
            _oToUIQueue.Enqueue( new( eProp, iParam ) );
        }

        protected bool CheckMessages() {
            while( _oOutQueue.TryDequeue( out TVMessage oMsg ) ) {
                switch( oMsg._eMsg ) {
                    case TVMessage.Message.ExitWorkThread:
                        _oToUIQueue.Enqueue( new( SSTVEvents.ThreadExit, 0 ) );
                        return false;
                    case TVMessage.Message.TryNewMode:
                        if( oMsg._oParam is SSTVMode oMode ) {
                            _oSSTVDeMo.Start( oMode );
                        } else {
                            _oSSTVDeMo.Reset();
                        }
                        break;
                    case TVMessage.Message.ChangeDirectory:
                        _strFilePath = oMsg._oParam as string;
                        break;
                }
            }
            return true;
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
            try {
                _oSSTVDeMo.Send_NextMode  += _oSSTVDraw.OnModeTransition_SSTVDeMo;
                _oSSTVDraw.Send_TvEvents  += OnTvEvents_SSTVDraw;
                _oSSTVDraw.Send_SavePoint += SaveImage;
            } catch( Exception oEx ) {
                Type[] rgErrors = { typeof( NullReferenceException ),
                                    typeof( ApplicationException ) };
                if( rgErrors.IsUnhandled( oEx ) )
                    throw;

                _oToUIQueue.Enqueue( new( SSTVEvents.ThreadAbort, 0 ) );
                return;
            }

            try {
                while( CheckMessages() ) {
                    while( !_oDataQueue.IsEmpty ) {
                        if( _oDataQueue.TryDequeue( out double dblValue ) ) {
                            _oSSTVDeMo.Do( dblValue );
                        } else {
                            throw new InvalidDataException( "Can't Dequeue non empty Data Queue." );
                        }
                    }

                    _oSSTVDraw.Process();

                    Thread.Sleep( 200 );
                };
			} catch( Exception oEx ) {
                if( _rgLoopErrors.IsUnhandled( oEx ) )
                    throw;

                _oToUIQueue.Enqueue( new( SSTVEvents.ThreadAbort, 0 ) );
            }
        }

        /// <summary>
        /// I've just ported this directly over from DocSSTV, but I'd like
        /// to make this multi threaded too! ^_^;; Let's take that on later.
        /// It should work ok, the input buffer might get larger for a bit.
        /// </summary>
        /// <param name="tvMode"></param>
		public async void SaveImage( SSTVMode tvMode ) {
            if( tvMode == null ) {
                LogError( "Odd the current SSTVMode is null." );
                return;
            }

            Action oSaveAction = delegate () {
                // BE CAREFUL! We can only get away with this because the DocSlot only implements
                // the LogError and that is thread safe.
                try {
                    using ImageSoloDoc oSnipDoc = new( new DocSlot( this ) );
			        SKRectI rcWorldDisplay = new SKRectI( 0, 0, tvMode.Resolution.Width, tvMode.Resolution.Height );

                    // Need to snip the image since we might not be using the entire display image.
                    if( !oSnipDoc.Load( _oSSTVDraw._pBitmapRX, rcWorldDisplay, rcWorldDisplay.Size ) )
                        return;

                    // Figure out path and name of the file.
                    string strFileName = Path.GetFileNameWithoutExtension( _strFileName );

                    if( string.IsNullOrEmpty( strFileName ) ) {
                        strFileName = FileNameGenerate;
                    } else {
                        throw new NotImplementedException( "Need to count iterations" );
                    }
                
                    strFileName = FileNameCleanUp( strFileName );

                    string strFilePath = Path.Combine  ( _strFilePath, strFileName + ".jpg" );
                    using var stream   = File.OpenWrite( strFilePath );

                    oSnipDoc.Save( stream );
                } catch( Exception oEx ) {
                    Type[] rgErrors = { typeof( NullReferenceException ),
                                        typeof( IOException ),
                                        typeof( ArgumentException ),
                                        typeof( ArgumentNullException ),
                                        typeof( PathTooLongException ),
                                        typeof( DirectoryNotFoundException ), 
                                        typeof( NotSupportedException ) };
                    if( rgErrors.IsUnhandled( oEx ) )
                        throw;

                    LogError( "Exception in Device Image Save Thread." );
                }
            };

            Task oTask = new Task( oSaveAction );

            oTask.Start();

            await oTask;

            // Don't dispose task. It's a bit lengthy and we're not run
            // frequently enough to put pressure on the GC.
		}

    }
}
