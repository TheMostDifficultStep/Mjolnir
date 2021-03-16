using System;
using System.IO;
using System.Collections.Generic;
using System.Text;

using SkiaSharp;

using Play.Interfaces.Embedding;
using Play.Sound;
using Play.Sound.FFT;
using Play.Edit;
using Play.ImageViewer;

namespace Play.SSTV {
    public enum ESstvProperty {
        ALL,
        UploadTime,
        SSTVMode,
        FFT,
        TXImageChanged,
		RXImageNew,
		DownLoadTime
    }

    public delegate void SSTVPropertyChange( ESstvProperty eProp );

    public class DocSSTV :
        IPgParent,
        IPgLoad<TextReader>,
        IPgSave<TextWriter>,
        IDisposable
    {
        private bool disposedValue;

        /// <summary>
        /// This editor shows the list of modes we can modulate.
        /// </summary>
		public class GeneratorMode : Editor {
			public GeneratorMode(IPgBaseSite oSite) : base(oSite) {
				//new ParseHandlerText(this, "m3u");
			}

			public override WorkerStatus PlayStatus => ((DocSSTV)_oSiteBase.Host).PlayStatus;
		}

		protected class DocSlot :
			IPgBaseSite,
            IPgFileSite
		{
			protected readonly DocSSTV _oHost;

			public DocSlot( DocSSTV oHost ) {
				_oHost = oHost ?? throw new ArgumentNullException();
			}

			public IPgParent Host => _oHost;

            public void LogError(string strMessage, string strDetails, bool fShow=true) {
				_oHost.LogError( strDetails );
			}

			public void Notify( ShellNotify eEvent ) {
				// Might want this value when we close to save the current playing list!!
			}

            // Need these for the image viewer.
            public FILESTATS FileStatus   => FILESTATS.READONLY;
            public Encoding  FileEncoding => Encoding.Default;
            public string    FilePath     => "Not Implemented";
            public string    FileBase     => "Not Implemented";
		}

        protected readonly IPgBaseSite       _oSiteBase;
		protected readonly IPgRoundRobinWork _oWorkPlace;

        public IPgParent Parentage => _oSiteBase.Host;
        public IPgParent Services  => Parentage;
        public bool      IsDirty   => false;

        public event SSTVPropertyChange PropertyChange;

        public Specification  RxSpec          { get; protected set; } = new Specification( 44100, 1, 0, 16 );
        public GeneratorMode  ModeList        { get; protected set; }
        public ImageWalkerDir ImageList       { get; protected set; }
        public SKBitmap       Bitmap          => ImageList.Bitmap;

        protected readonly ImageSoloDoc  _oDocSnip;   // Clip the image.

        protected Mpg123FFTSupport FileDecoder   { get; set; }
        protected BufferSSTV       _oSSTVBuffer;
        protected CSSTVMOD         _oSSTVModulator;
        protected CSSTVDEM         _oSSTVDeModulator;
		protected IPgPlayer        _oPlayer;
        protected SSTVGenerator    _oSSTVGenerator;
		protected TmmSSTV          _oRxSSTV;

		public ImageSoloDoc ReceiveImage { get; protected set; }
		public ImageSoloDoc SyncImage    { get; protected set; }


        private DataTester _oDataTester;

		private double[] _rgFFTData; // Data input for FFT. Note: it get's destroyed in the process.
		private CFFT     _oFFT;
        public  int[]    FFTResult { get; protected set; }
        public  int      FFTResultSize { 
            get {
                if( _oFFT == null )
                    return 0;

                return _oFFT.Mode.TopBucket; // The 3.01kHz size we care about.
            }
        }

        /// <summary>
        /// Document type for SSTV transmit and recieve using audio i/o
        /// </summary>
        /// <exception cref="ArgumentNullException" />
        /// <exception cref="ApplicationException" />
        public DocSSTV( IPgBaseSite oSite ) {
            _oSiteBase  = oSite ?? throw new ArgumentNullException( "Site must not be null" );
            _oWorkPlace = ((IPgScheduler)Services).CreateWorkPlace() ?? throw new ApplicationException( "Couldn't create a worksite from scheduler.");

            ModeList  = new GeneratorMode ( new DocSlot( this ) );
            ImageList = new ImageWalkerDir( new DocSlot( this ) );
            _oDocSnip = new ImageSoloDoc  ( new DocSlot( this ) );

			ReceiveImage = new ImageSoloDoc( new DocSlot( this ) );
			SyncImage    = new ImageSoloDoc( new DocSlot( this ) );
        }

        #region Dispose
        protected virtual void Dispose( bool disposing ) {
            if( !disposedValue ) {
                if( disposing ) {
                    if( _oDocSnip != null )
			            _oDocSnip.Dispose(); 

                    // If init new fails then this won't get created.
                    if( FileDecoder != null )
                        FileDecoder.Dispose();
                    if( _oPlayer != null )
                        _oPlayer.Dispose();
                }

                // TODO: free unmanaged resources (unmanaged objects) and override finalizer
                // TODO: set large fields to null
                disposedValue = true;
            }
        }

        // // TODO: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
        // ~DocSSTV()
        // {
        //     // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        //     Dispose(disposing: false);
        // }

        public void Dispose() {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
        #endregion

        public void LogError( string strMessage ) {
            _oSiteBase.LogError( "SSTV", strMessage );
        }

        /// <summary>
        /// Set up a sample signal for the FFT. This is for test purposes. I should use
        /// one of my DSP-Goodies functions but I'm still sorting that all out.
        /// </summary>
        /// <param name="oCtrl"></param>
        /// <param name="rgData"></param>
        protected static void LoadData( FFTControlValues oCtrl, List<double> rgData ) {
            // We need FFTSize number of samples.
			rgData.Clear();
            for( double t = 0; rgData.Count < oCtrl.FFTSize; t += 1 / oCtrl.SampBase ) {
                double dbSample = 0;
                    
                dbSample += 80 * Math.Sin( Math.PI * 2 *  400 * t);
                dbSample += 80 * Math.Sin( Math.PI * 2 * 1200 * t);
                dbSample += 80 * Math.Sin( Math.PI * 2 * 2900 * t);

                rgData.Add(dbSample);
            }
        }

        public void LoadModulators( IEnumerator<SSTVMode> iterMode) {
            using BaseEditor.Manipulator oBulk = ModeList.CreateManipulator();

            while( iterMode.MoveNext() ) {
                SSTVMode oMode    = iterMode.Current;
                string   strValue = oMode.Name + " : " + oMode.Resolution.Width.ToString() + " x " + oMode.Resolution.Height.ToString();
                Line     oLine    = oBulk.LineAppendNoUndo( strValue );

                oLine.Extra = oMode;
            }
        }

        /// <summary>Find all the output devices available.</summary>
        /// <remarks>
        /// Trial run for when we actually need this.
        /// </remarks>
        public void FindOutputDevices() {
            IEnumerator<string> oIter  = MMHelpers.GetOutputDevices();
            List<string>        rgDevs = new List<string>();
            while( oIter.MoveNext() ) {
                rgDevs.Add( oIter.Current );
            }
        }

        public bool InitNew2() {
            _oFFT = new CFFT( FFTControlValues.FindMode( 8000 ) );

            List<double> rgFFTData = new List<double>();

			LoadData( _oFFT.Mode, rgFFTData );

            _rgFFTData = rgFFTData.ToArray();
			FFTResult  = new int   [_oFFT.Mode.FFTSize/2];

            return true;
        }

        public bool InitNew3() {
	        //string strSong = @"C:\Users\Frodo\Documents\signals\1kHz_Right_Channel.mp3"; // Max signal 262.
            //string strSong = @"C:\Users\Frodo\Documents\signals\sstv-essexham-image01-martin2.mp3";
            string strSong = @"C:\Users\Frodo\Documents\signals\sstv-essexham-image02-scottie2.mp3";

			try {
                //FileDecoder = _oSound.CreateSoundDecoder( strSong );
                FileDecoder = new Mpg123FFTSupport( strSong );
			} catch( Exception oEx ) {
				Type[] rgErrors = { typeof( FileNotFoundException ),
									typeof( FileLoadException ), 
									typeof( FormatException ),
									typeof( NullReferenceException ),
                                    typeof( InvalidOperationException ) };
				if( rgErrors.IsUnhandled( oEx ) ) {
					throw oEx;
				}
				LogError( "Couldn't find file, or play, or continue to play format of : \"" );
				LogError( strSong );
			}

            _oFFT = new CFFT( FFTControlValues.FindMode( FileDecoder.Spec.Rate ) );

            _rgFFTData      = new double[_oFFT.Mode.FFTSize];
			FFTResult       = new int   [_oFFT.Mode.FFTSize/2];

            FileDecoder.Target = _rgFFTData;
            FileDecoder.Init( _oFFT.Mode.Decimation, 1 );

            return true;
        }

        /// <summary>
        /// Setup the output stream This only needs to be set when the Spec changes.
        /// </summary>
        /// <returns></returns>
        public bool OutputStreamInit() {
            try {
                // Help the garbage collector telling the buffer to unlink the pump (via dispose)
                if( _oSSTVBuffer != null )
                    _oSSTVBuffer.Dispose();

                _oSSTVBuffer    = new BufferSSTV( RxSpec );
                _oSSTVModulator = new CSSTVMOD( 0, RxSpec.Rate, _oSSTVBuffer );

                //_oDataTester = new DataTester( SSTVBuffer );
                //SSTVBuffer.Pump = _oDataTester.GetEnumerator();
            } catch( Exception oEx ) {
                Type[] rgErrors = { typeof( ArgumentOutOfRangeException ),
                                    typeof( NullReferenceException ) };
                if( rgErrors.IsUnhandled( oEx ) )
                    throw;

                return false;
            }

            return true;
        }

		public int MaxOutputDevice {
			get {
				IEnumerator<string> iterOutput = MMHelpers.GetOutputDevices();
				int                 iCount     = -1;
				while( iterOutput.MoveNext() ) {
					++iCount;
				}
				return iCount;
			}
		}

        public bool InitNew() {
            if( !ModeList.InitNew() ) // TODO: Set up hilight on TX!!
                return false;
            if( !OutputStreamInit() )  // Not really a cause for outright failure...
                return false;
			if( !_oDocSnip.InitNew() )
				return false;

			if( !ReceiveImage.InitNew() )
				return false;
			if( !SyncImage.InitNew() )
				return false;

            LoadModulators( GenerateMartin .GetModeEnumerator() );
            LoadModulators( GenerateScottie.GetModeEnumerator() );
            LoadModulators( GeneratePD     .GetModeEnumerator() );

			string strPath = Environment.GetFolderPath( Environment.SpecialFolder.MyPictures );
            if( !ImageList.LoadURL( strPath ) ) {
				LogError( "Couldn't find pictures directory for SSTV" );
                return false;
			}

            ImageList.ImageUpdated += Listen_ImageUpdated;

            // BUG: Hard coded device.
			if( MaxOutputDevice >= 1 ) {
				_oPlayer = new WmmPlayer(RxSpec, 1); 
			} else {
				_oPlayer = new WmmPlayer(RxSpec, 0); 
			}

            return true;
        }

        public bool Load( TextReader oStream ) {
            return InitNew();
        }

        public bool Save( TextWriter oStream ) {
            return true;
        }

        public int PercentFinished {
            get { 
                if( _oSSTVGenerator != null ) 
                    return _oSSTVGenerator.PercentComplete;

                return 0;
            } 
        }

        public SSTVMode TransmitMode { 
            get {
                if( _oSSTVGenerator != null )
                    return _oSSTVGenerator.Mode;

                return null;
            }
        }

        /// <summary>
        /// BUG: This is a bummer but, I use a point for the aspect ratio in my
        /// SmartRect code. I'll fix that later.
        /// </summary>
        /// <param name="iModeIndex">Index into the ModeList</param>
        /// <returns></returns>
        public SKPointI ResolutionAt( int iModeIndex ) {
            if( ModeList[iModeIndex].Extra is SSTVMode oMode )
                return new SKPointI( oMode.Resolution.Width, oMode.Resolution.Height );

            LogError( "Problem finding SSTVMode. Using default." );
            return new SKPointI( 320, 240 );
        }

        /// <summary>
        /// This sets up our transmit buffer and modulator to send the given image.
        /// </summary>
        /// <param name="iModeIndex">Index of the generator mode to use.</param>
        /// <param name="oTxImage">Image to display. It should match the generator mode requirements.</param>
        /// <returns></returns>
        public bool GeneratorSetup( SSTVMode oMode, SKBitmap oTxImage ) {
            if( oMode == null || oTxImage == null ) {
                LogError( "Mode or Image is null on Generator Setup." );
                return false;
            }
            if( _oSSTVModulator == null ) {
                LogError( "SSTV Modulator is not ready for Transmit." );
                return false;
            }
            if( _oWorkPlace.Status == WorkerStatus.BUSY ||
                _oWorkPlace.Status == WorkerStatus.PAUSED ) {
                LogError( "Stop playing current image, to begin the next." );
                return false;
            }

            _oSSTVGenerator = null;

            // Normally I'd use a guid to identify the class, but I thought I'd
            // try something a little different this time.
            try {
				switch( oMode.Family ) {
					case TVFamily.PD:
						_oSSTVGenerator = new GeneratePD     ( oTxImage, _oSSTVModulator, oMode ); break;
					case TVFamily.Martin:
						_oSSTVGenerator = new GenerateMartin ( oTxImage, _oSSTVModulator, oMode ); break;
					case TVFamily.Scottie:
						_oSSTVGenerator = new GenerateScottie( oTxImage, _oSSTVModulator, oMode ); break;
					default:
						return false;
				}

                if( _oSSTVGenerator != null )
                    _oSSTVBuffer.Pump = _oSSTVGenerator.GetEnumerator();
            } catch( Exception oEx ) {
                Type[] rgErrors = { typeof( ArgumentException ),
                                    typeof( ArgumentNullException ),
                                    typeof( ArgumentOutOfRangeException ),
                                    typeof( NullReferenceException ) };
                if( rgErrors.IsUnhandled( oEx ) )
                    throw;

                LogError( "Blew chunks trying to create Illudium Q-36 Video Modulator, Isn't that nice?" );
                _oSSTVGenerator = null;
            }

            Raise_PropertiesUpdated( ESstvProperty.ALL );

            return _oSSTVGenerator != null;
        }

        private void Listen_ImageUpdated() {
            Raise_PropertiesUpdated( ESstvProperty.TXImageChanged );
        }

        protected void Raise_PropertiesUpdated( ESstvProperty eProp ) {
            PropertyChange?.Invoke( eProp );
        }

        /// <summary>
        /// This is our work iterator we use to play the audio.
        /// </summary>
        /// <returns>Amount of time to wait until we want call again, in Milliseconds.</returns>
        public IEnumerator<int> GetPlayerTask() {
            do {
                uint uiWait = 60000; // Basically stop wasting time here on error.
                try {
                    uiWait = ( _oPlayer.Play( _oSSTVBuffer ) >> 1 ) + 1;
                } catch( Exception oEx ) {
                    Type[] rgErrors = { typeof( NullReferenceException ),
                                        typeof( ArgumentNullException ),
                                        typeof( MMSystemException ) };
                    if( rgErrors.IsUnhandled( oEx ) )
                        throw;

                    _oWorkPlace.Stop();
                    LogError( "Trouble playing in SSTV" );
                }
                Raise_PropertiesUpdated( ESstvProperty.UploadTime );
                yield return (int)uiWait;
            } while( _oSSTVBuffer.IsReading );

            ModeList.HighLight = null;
            // Set upload time to "finished" maybe even date/time!
        }

        /// <summary>
        /// Try to begin playing image.
        /// </summary>
        /// <param name="iMode">Which transmission type and mode.</param>
        /// <param name="skSelect">clip region in source bitmap coordinates.</param>
        /// <remarks>It's a little unfortnuate that we just don't pass the mode. But we 
        /// need to know what index it came from to set the hilight. HOWEVER, we
        /// count on the skSelect aspect ratio being set with respect to the aspect
        /// of the given SSTVMode.</remarks>
        public void PlayBegin( int iModeIndex, SKRectI skSelect ) {
            if( ModeList[iModeIndex].Extra is SSTVMode oMode ) {
                if( _oWorkPlace.Status == WorkerStatus.FREE ) {
			        // The DocSnip object retains ownership of it's generated bitmap and frees it on next load.
                    // Bug: I should check if the selection == the whole bitmap == required dimension
                    //      and I could skip the snip stage.
			        _oDocSnip.Load( Bitmap, skSelect, oMode.Resolution );
                    if( GeneratorSetup( oMode, _oDocSnip.Bitmap ) ) {
                        _oWorkPlace.Queue( GetPlayerTask(), 0 );
                        ModeList.HighLight = ModeList[iModeIndex];
                    }
                }
            }
            //while ( _oDataTester.ConsumeData() < 350000 ) {
            //}
        }

		private void SaveRxImage() {
			if( ReceiveImage.Bitmap == null )
				return;

			string strPath = Environment.GetFolderPath( Environment.SpecialFolder.MyPictures );

			using SKImage image  = SKImage.FromBitmap(ReceiveImage.Bitmap);
			using var     data   = image.Encode( SKEncodedImageFormat.Png, 80 );
            using var     stream = File.OpenWrite( Path.Combine( strPath, "testmeh.png") );

            data.SaveTo(stream);
		}

		/// <summary>
		/// The decoder has determined the the incoming video mode. Set the 
		/// TmmSSTV to match the given mode. This call comes from CSSTVDEM::Start() 
		/// Start() resets the write buffer. More thread issues to
		/// keep in mind.
		/// </summary>
		/// <remarks>
		/// I switched to a TmmSSTV that understands all the modes and is switched
        /// between them on the fly. The benefit is that I don't need to set
        /// up the event hooks everytime a new image comes down in the case where I
        /// was alloc'ing TmmSSTV subclasses.</remarks>
        private void ListenNextRxMode( SSTVMode tvMode ) {
			ReceiveImage.Bitmap = null;
			SyncImage   .Bitmap = null;

            switch( tvMode.Family ) {
                case TVFamily.PD: 
					_oRxSSTV.InitPD     ( tvMode, _oSSTVDeModulator.SampFreq, 1 ); // _oSSTVBuffer.Spec.Rate
					break;
                case TVFamily.Martin: 
					_oRxSSTV.InitMartin ( tvMode, _oSSTVDeModulator.SampFreq, 1 ); 
					break;
                case TVFamily.Scottie: 
					_oRxSSTV.InitScottie( tvMode, _oSSTVDeModulator.SampFreq, 1 ); 
					break;

                default: 
					throw new ArgumentOutOfRangeException("Unrecognized Mode Type.");
            }

			tvMode.ScanLineWidthInSamples = (int)Math.Round( _oRxSSTV.ScanWidthInSamples );

            _oRxSSTV.PrepDraw(); // bitmap allocated in here.

			ReceiveImage.Bitmap = _oRxSSTV._pBitmapRX;
			SyncImage   .Bitmap = _oRxSSTV._pBitmapD12;

			Raise_PropertiesUpdated( ESstvProperty.RXImageNew );
        }

		/// <summary>
		/// Forward events coming from TmmSSTV
		/// </summary>
        private void ListenTvEvents( ESstvProperty eProp )
        {
            Raise_PropertiesUpdated( eProp );
        }

		/// <summary>
		/// Note that we don't take the mode in this task since we want it
		/// to be divined from the VIS by the CSSTVDEM object.
		/// </summary>
		/// <returns>Time to wait until next call in ms.</returns>
        public IEnumerator<int> GetRecorderTask() {
            _oSSTVDeModulator.ShoutNextMode += ListenNextRxMode;
            do {
                try {
                    for( int i = 0; i< 500; ++i ) {
                        _oSSTVDeModulator.Do( _oSSTVBuffer.ReadOneSample() );
                    }
					if( _oRxSSTV != null ) {
						_oRxSSTV.DrawSSTV();
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

					SaveRxImage();

					if( oEx.GetType() != typeof( IndexOutOfRangeException ) ) {
						LogError( "Trouble recordering in SSTV" );
					}
					// Don't call _oWorkPlace.Stop() b/c we're already in DoWork() which will
					// try calling the _oWorker which will have been set to NULL!!
                    break; // Drop down so we can unplug from our Demodulator.
                }
                yield return 0; // 44,100 hz is a lot to process, let's go as fast as possible. >_<;;
            } while( _oSSTVBuffer.IsReading );

			_oSSTVDeModulator.ShoutNextMode -= ListenNextRxMode;
			ModeList.HighLight = null;
            // Set upload time to "finished" maybe even date/time!
        }

        /// <summary>
        /// This is my 2nd test where we generate video and encode it to a 
        /// time varying signal to be decoded by the CSSTVDEM object! Because we're
        /// not intercepting the signal before the VCO we can use the normal 
        /// GeneratorSetup call.
        /// </summary>
        /// <param name="iModeIndex">TV Mode to use.</param>
        /// <param name="skSelect"></param>
        /// <remarks>Set CSSTVDEM::m_fFreeRun = true</remarks>
        public void RecordBegin2( int iModeIndex, SKRectI skSelect ) {
            if( ModeList[iModeIndex].Extra is SSTVMode oMode ) {
                if( _oWorkPlace.Status == WorkerStatus.FREE ) {
			        _oDocSnip.Load( Bitmap, skSelect, oMode.Resolution );
					if( GeneratorSetup( oMode, _oDocSnip.Bitmap ) ) {
						FFTControlValues oFFTMode  = FFTControlValues.FindMode( RxSpec.Rate ); 
						SYSSET           sys       = new SYSSET   ( oFFTMode.SampFreq );
						CSSTVSET         oSetSSTV  = new CSSTVSET ( oMode, 0, oFFTMode.SampFreq, 0, sys.m_bCQ100 );
						CSSTVDEM         oDemodTst = new CSSTVDEM ( oSetSSTV,
																	sys,
																	(int)oFFTMode.SampFreq, 
																	(int)oFFTMode.SampBase, 
																	0 );
						_oRxSSTV          = new TmmSSTV( oDemodTst );
						_oRxSSTV.ShoutTvEvents += ListenTvEvents;

						_oSSTVDeModulator = oDemodTst;

						_oWorkPlace.Queue( GetRecorderTask(), 0 );
					}
                }
            }
        }

        public void RecordBegin3() {
            if( ImageList.Bitmap != null ) {
                RecordBegin2( 0, new SKRectI( 0, 0, ImageList.Bitmap.Width, ImageList.Bitmap.Height ) );
            } else {
                LogError( "Please select a bitmap first" );
            }
        }

		/// <summary>
		/// In this 1'st test we skip the VIS and simply test video transmit and receive.
		/// The CSSTVDEM object is not used in these tests. The transmit and recieve
		/// are set from the given mode.
		/// </summary>
		/// <param name="oMode">User selected mode. Any should work.</param>
		/// <returns>Time in ms before next call wanted.</returns>
        public IEnumerator<int> GetRecordTestTask( SSTVMode oMode ) {
			if( oMode == null )
				throw new ArgumentNullException( "Mode must not be Null." );

			IEnumerator<int> oIter = _oSSTVGenerator.GetEnumerator();

			oIter            .MoveNext(); // skip the VIS for now.
			_oSSTVDeModulator.SstvSet.SetMode( oMode );
			_oSSTVDeModulator.Start( oMode );

            while( oIter.MoveNext() ) {
				_oRxSSTV.DrawSSTV();
				yield return 1;
			};
			SaveRxImage();
		}

		/// <summary>
		/// This 1'st test generates the the video signal but doesn't actually create audio
		/// tone stream but a fake stream of frequency data. Thus skipping the A/D coverter code
		/// we're just testing the video encode / decode. We do this by re-assigning
		/// the _oSSTVModulator with a new one set to our test frequency.
		/// </summary>
		/// <param name="iModeIndex">TV Format to use.</param>
		/// <param name="skSelect">Portion of the image we want to transmit.</param>
		/// <remarks>Use a low sample rate so it's easier to slog thru the data. 
		///          Set CSSTVDEM::m_fFreeRun to false!!</remarks>
		/// <seealso cref="InitNew" />
		/// <seealso cref="OutputStreamInit"/>
        public void RecordBegin( int iModeIndex, SKRectI skSelect ) {
            if( ModeList[iModeIndex].Extra is SSTVMode oMode ) {
                if( _oWorkPlace.Status == WorkerStatus.FREE ) {
			        _oDocSnip.Load( Bitmap, skSelect, oMode.Resolution );

					FFTControlValues oFFTMode  = FFTControlValues.FindMode( 8000 ); // RxSpec.Rate
					SYSSET           sys       = new SYSSET   ( oFFTMode.SampFreq );
					CSSTVSET         oSetSSTV  = new CSSTVSET ( oMode, 0, oFFTMode.SampFreq, 0, sys.m_bCQ100 );
					DemodTest        oDemodTst = new DemodTest( oSetSSTV,
															    sys,
															    (int)oFFTMode.SampFreq, 
															    (int)oFFTMode.SampBase, 
															    0 );
					_oSSTVDeModulator = oDemodTst;
					_oSSTVModulator   = new CSSTVMOD( 0, oFFTMode.SampFreq, _oSSTVBuffer );
					_oRxSSTV          = new TmmSSTV ( _oSSTVDeModulator );

					_oSSTVGenerator = oMode.Family switch {
						TVFamily.PD      => new GeneratePD     ( _oDocSnip.Bitmap, oDemodTst, oMode ),
						TVFamily.Martin  => new GenerateMartin ( _oDocSnip.Bitmap, oDemodTst, oMode ),
						TVFamily.Scottie => new GenerateScottie( _oDocSnip.Bitmap, oDemodTst, oMode ),

						_ => throw new ArgumentOutOfRangeException("Unrecognized Mode Type."),
					};

                    _oSSTVDeModulator.ShoutNextMode += ListenNextRxMode;

                    _oWorkPlace.Queue( GetRecordTestTask( oMode ), 0 );
                }
            }
        }

		public WorkerStatus PlayStatus => _oWorkPlace.Status;

        public void PlayStop() {
            ModeList.HighLight = null;

            _oWorkPlace.Stop();
        }

        /// <summary>
        /// Little bit of test code for the fft.
        /// </summary>
        public void PlaySegment() {
            try {
                FileDecoder.Read();

                _oFFT.Calc( _rgFFTData, 10, 0, FFTResult );

                Raise_PropertiesUpdated( ESstvProperty.FFT );
            } catch( Exception oEx ) {
                Type[] rgErrors = { typeof( NotImplementedException ),
                                    typeof( NullReferenceException ) };
                if( rgErrors.IsUnhandled( oEx ) )
                    throw;
            }
        }
    } // End class
}
