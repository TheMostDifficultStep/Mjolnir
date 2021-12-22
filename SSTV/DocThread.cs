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
using Play.ImageViewer;
using Play.Sound.FFT;
using System.Collections;

namespace Play.SSTV {
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
        IPgParent,
        IEnumerable<int>
    {
        protected readonly ConcurrentQueue<SSTVMessage> _oToUIQueue;

        protected readonly SSTVDraw _oSSTVDraw;
        protected readonly SSTVDEM  _oSSTVDeMo;

        protected          int                  _iDecodeCount = 0;
        protected          SSTVMode             _tvMode;
        protected readonly string               _strFileName;
        protected readonly WaveToSampleProvider _oProvider;
        protected readonly AudioFileReader      _oReader;

        public IPgParent Parentage => throw new NotImplementedException();

        public IPgParent Services => throw new NotImplementedException();

        protected class DocSlot :
			IPgBaseSite//,
            //IPgFileSite
		{
			protected readonly FileReadingState _oHost;

			public DocSlot( FileReadingState oHost, string strFileBase = "Not Implemented" ) {
				_oHost = oHost ?? throw new ArgumentNullException( "Host" );
                //FileBase = strFileBase ?? throw new ArgumentNullException("File base string" );
			}

			public IPgParent Host => _oHost;

            public void LogError(string strMessage, string strDetails, bool fShow=true) {
				_oHost.LogError( strDetails );
			}

			public void Notify( ShellNotify eEvent ) {
			}

            //// Need these for the image viewer.
            //public FILESTATS FileStatus   => FILESTATS.READONLY;
            //public Encoding  FileEncoding => Encoding.Default;
            //public string    FilePath     => "Not Implemented";
            //public string    FileBase     { get; protected set; }
		}

        public FileReadingState( ConcurrentQueue<SSTVMessage> oToUIQueue, string strFileName, SKBitmap oD12, SKBitmap oRx ) 
        {
            _oToUIQueue  = oToUIQueue  ?? throw new ArgumentNullException( nameof( oToUIQueue ) );
            _strFileName = strFileName ?? throw new ArgumentNullException( nameof( strFileName ) );

            _oReader     = new AudioFileReader     ( _strFileName ); 
            _oProvider   = new WaveToSampleProvider( _oReader );

			FFTControlValues oFFTMode  = FFTControlValues.FindMode( _oReader.WaveFormat.SampleRate );

            _oSSTVDeMo = new SSTVDEM ( new SYSSET(), oFFTMode.SampFreq );;
			_oSSTVDraw = new SSTVDraw( _oSSTVDeMo, oD12, oRx );
        }

        public void LogError( string strMessag ) {
            _oToUIQueue.Enqueue( new SSTVMessage(SSTVEvents.ThreadException, -1 ) );
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
                // Note: SSTVDemodulator.Start() will try to use the callback.
                _oSSTVDeMo.Send_NextMode += OnNextMode_SSTVDemo;
                _oSSTVDraw.Send_TvEvents += OnTVEvents_SSTVDraw;

                if( oMode != null ) {
                    _oSSTVDeMo.Start( oMode );
                }

                foreach( int i in this ) {
                    _oSSTVDraw.Process();
                }

                // Check if there's any leftover and if so, save it. Don't call
                // Stop()! That will happen automatically when the bitmap gets full.
                if( _oSSTVDraw.PercentRxComplete > 25 ) {
                    SaveFileDecode();
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
        /// Listen to the decoder when it spots a new image. DO NOT 
        /// enqueue ESstvProperty.SSTVMode to the UI msg queue. TmmSSTV
        /// will Shout that as a TvEvent.
        /// </summary>
        /// <remarks>The bitmap only changes when the mode changes and
        /// the next image isn't necessarily a different mode. I need to
        /// separate out those events.</remarks>
        private void OnNextMode_SSTVDemo( SSTVMode tvMode, int iPrevBase ) {
            try {
                double dblPrevSize = _oSSTVDraw.ImageSizeInSamples;

                if( _tvMode != null && dblPrevSize != 0 ) {
			        double dblProgress = iPrevBase * 100 / dblPrevSize;

                    if( dblProgress > 25 )
                        SaveFileDecode();
                }
                _oSSTVDraw.OnModeTransition_SSTVMod( tvMode, iPrevBase ); 
                _tvMode = tvMode;
            } catch( ArgumentOutOfRangeException ) {
            }
        }

        /// <summary>
        /// Listen to the SSTVDraw object. I can probably stop listening to the decoder
        /// and have it do that directly.
        /// </summary>
        /// <seealso cref="OnNextMode_SSTVDemo"/>
        private void OnTVEvents_SSTVDraw( SSTVEvents eProp, int iParam ) {
            _oToUIQueue.Enqueue( new( eProp, iParam ) );

            // It's hacky. but I need this here as well as in OnNexMode_SSTVDemo
            // I'll need to straighten that out.
            if( eProp == SSTVEvents.DownLoadFinished ) {
                SaveFileDecode();
            }
        }

        IEnumerator IEnumerable.GetEnumerator() {
            return GetEnumerator();
        }

        public void SaveFileDecode() {
            if( _tvMode == null )
                return;

            try {
                using ImageSoloDoc oSnipDoc = new( new DocSlot( this ) );
			    SKRectI rcWorldDisplay = new SKRectI( 0, 0, _tvMode.Resolution.Width, _tvMode.Resolution.Height );

                // Need to snip the image since we might not be using the entire display image.
                if( !oSnipDoc.Load( _oSSTVDraw._pBitmapRX, rcWorldDisplay, rcWorldDisplay.Size ) )
                    return;

                _iDecodeCount++;

                // Figure out path and name of the file.
                string strFilePath = Path.GetDirectoryName( _strFileName );
                string strFileName = Path.GetFileNameWithoutExtension( _strFileName );
                string strNodeName = _tvMode.Name.Replace( " ", string.Empty );

                string strSavePath = Path.Combine( strFilePath, strFileName + "_" + strNodeName + "_" + _iDecodeCount.ToString() + ".jpg" );
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
    public class DeviceListeningState 
    {
        protected readonly ConcurrentQueue<SSTVMessage> _oToUIQueue;
        protected readonly ConcurrentQueue<double>      _oDataQueue; 
        protected readonly WaveFormat                   _oDataFormat;
        protected readonly ConcurrentQueue<TVMessage>   _oOutQueue;

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

        public DeviceListeningState( int                          iSampleRate, 
                                     ConcurrentQueue<SSTVMessage> oToUIQueue, 
                                     ConcurrentQueue<double>      oDataQueue, 
                                     ConcurrentQueue<TVMessage>   oOutQueue,
                                     SKBitmap                     oD12,
                                     SKBitmap                     oRx )
        {
            _oToUIQueue  = oToUIQueue ?? throw new ArgumentNullException( nameof( oToUIQueue ) );
            _oDataQueue  = oDataQueue ?? throw new ArgumentNullException( nameof( oDataQueue ) );
            _oOutQueue   = oOutQueue  ?? throw new ArgumentNullException( nameof( oOutQueue  ) );

            if( oD12 == null )
                throw new ArgumentNullException( nameof( oD12 ) );
            if( oRx  == null )
                throw new ArgumentNullException( nameof( oRx  ) );

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
                }
            }
            return true;
        }

        /// <summary>
        /// This is the entry point for our device listener thread.
        /// Events within here are posted via message and are read in UI thread.
        /// </summary>
        /// <remarks>At present we rely on NAudio to pick up the sound samples
        /// in the UI thread. It sux but that's how it works. So that's whey
        /// we need the DataQueu. In the future I'll write my own code to
        /// read from the sound card and I'll be able to put that code.</remarks>
        public void DoWork() {
            try {
                _oSSTVDeMo.Send_NextMode += new NextMode( _oSSTVDraw.OnModeTransition_SSTVMod );
                _oSSTVDraw.Send_TvEvents += OnTvEvents_SSTVDraw;
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

                    Thread.Sleep( 250 );
                };
			} catch( Exception oEx ) {
                if( _rgLoopErrors.IsUnhandled( oEx ) )
                    throw;

                _oToUIQueue.Enqueue( new( SSTVEvents.ThreadAbort, 0 ) );
            }
        }
    }
}
