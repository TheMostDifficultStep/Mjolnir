using System;
using System.IO;
using System.Collections.Generic;

using SkiaSharp;

using Play.Interfaces.Embedding;
using Play.Sound;
using Play.Sound.FFT;
using Play.Edit;
using Play.ImageViewer;
using System.Text;

namespace Play.SSTV {
    public enum ESstvProperty {
        ALL,
        UploadTime,
        SSTVMode,
        FFT,
        TXImage
    }

    public delegate void SSTVPropertyChange( ESstvProperty eProp );

    public class DocSSTV :
        IPgParent,
        IPgLoad<TextReader>,
        IPgSave<TextWriter>,
        IDisposable
    {
        private bool disposedValue;

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

        public Specification  Spec           { get; protected set; } = new Specification( 44100, 1, 0, 16 );
        public SSTVMode       TransmitMode   { get; protected set; }
        public Editor         ModeList       { get; protected set; }
        public ImageWalkerDir ImageList      { get; protected set; }
        public SKBitmap       Bitmap         => ImageList.Bitmap;
        public string         BitmapFileName => ImageList.CurrentFileName; 

        protected readonly ImageSoloDoc  _oDocSnip;   // Clip the image.

        protected Mpg123FFTSupport FileDecoder   { get; set; }
        protected BufferSSTV       SSTVBuffer    { get; set; }
        protected CSSTVMOD         SSTVModulator { get; set; }
		protected IPgPlayer        _oPlayer;

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

            ModeList  = new Editor        ( new DocSlot( this ) );
            ImageList = new ImageWalkerDir( new DocSlot( this ) );
            _oDocSnip = new ImageSoloDoc  ( new DocSlot( this ) );
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
                Line oLine = oBulk.LineAppendNoUndo( iterMode.Current.Name );
                oLine.Extra = iterMode.Current;
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
                if( SSTVBuffer != null )
                    SSTVBuffer.Dispose();

                SSTVBuffer    = new BufferSSTV( Spec );
                SSTVModulator = new CSSTVMOD( 0, Spec.Rate, SSTVBuffer );

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

        public bool InitNew() {
            if( !ModeList.InitNew() ) // TODO: Set up hilight on TX!!
                return false;
            if( !OutputStreamInit() )  // Not really a cause for outright failure...
                return false;
			if( !_oDocSnip.InitNew() )
				return false;

            LoadModulators( GenerateMartin .GetModeEnumerator() );
            LoadModulators( GenerateScottie.GetModeEnumerator() );
            LoadModulators( GeneratePD     .GetModeEnumerator() );

            if( !ImageList.LoadURL( @"C:\Users\Frodo\Pictures") ) 
                return false;

            ImageList.ImageUpdated += Listen_ImageUpdated;

            // This only needs to change if the Spec or Device is updated.
            _oPlayer = new WmmPlayer(Spec, 1); 

            return true;
        }

        /// <summary>
        /// This sets up our transmit buffer and modulator to send the given image.
        /// </summary>
        /// <param name="iIndex">Index of the generator mode to use.</param>
        /// <param name="oTxImage">Image to display. It should match the generator mode requirements.</param>
        /// <returns></returns>
        public bool GeneratorSetup( int iIndex, SKBitmap oTxImage ) {
            TransmitMode = null;

            if( SSTVModulator == null ) {
                LogError( "SSTV Modulator is not ready for Transmit." );
                return false;
            }
            if( _oWorkPlace.Status == WorkerStatus.BUSY ||
                _oWorkPlace.Status == WorkerStatus.PAUSED ) {
                LogError( "Stop playing current image, to begin the next." );
                return false;
            }

            // Normally I'd use a guid to identify the class, but I thought I'd
            // try something a little different this time.
            try {
                SSTVGenerator oSSTVGenerator = null;

                if( ModeList[iIndex].Extra is SSTVMode oMode ) {
                    if( oMode.Owner == typeof( GeneratePD ) )
                        oSSTVGenerator = new GeneratePD     ( oTxImage, SSTVModulator, oMode );
                    if( oMode.Owner == typeof( GenerateMartin ) )
                        oSSTVGenerator = new GenerateMartin ( oTxImage, SSTVModulator, oMode );
                    if( oMode.Owner == typeof( GenerateScottie ) )
                        oSSTVGenerator = new GenerateScottie( oTxImage, SSTVModulator, oMode );

                    if( oSSTVGenerator != null )
                        TransmitMode = oMode;
                }

                if( oSSTVGenerator != null )
                    SSTVBuffer.Pump = oSSTVGenerator.GetEnumerator();
            } catch( Exception oEx ) {
                Type[] rgErrors = { typeof( ArgumentException ),
                                    typeof( ArgumentNullException ),
                                    typeof( ArgumentOutOfRangeException ),
                                    typeof( NullReferenceException ) };
                if( rgErrors.IsUnhandled( oEx ) )
                    throw;

                LogError( "Blew chunks trying to create Illudium Q-36 Space Modulator, Isn't that nice?" );
                TransmitMode = null;
            }

            return TransmitMode != null;
        }

        private void Listen_ImageUpdated() {
            Raise_PropertiesUpdated( ESstvProperty.TXImage );
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
                    uiWait = ( _oPlayer.Play( SSTVBuffer ) >> 1 ) + 1;
                } catch( Exception oEx ) {
                    Type[] rgErrors = { typeof( NullReferenceException ),
                                        typeof( ArgumentNullException ),
                                        typeof( MMSystemException ) };
                    if( rgErrors.IsUnhandled( oEx ) )
                        throw;

                    _oWorkPlace.Stop();
                    LogError( "Trouble playing in SSTV" );
                }
                yield return (int)uiWait;
            } while( SSTVBuffer.IsReading );

            TransmitMode = null;
            // Set upload time to "finished" maybe even date/time!
        }

        public bool Load( TextReader oStream ) {
            return InitNew();
        }

        public bool Save( TextWriter oStream ) {
            return true;
        }

        /// <summary>
        /// Try to begin playing image.
        /// </summary>
        /// <param name="iMode">Which transmission type and mode.</param>
        /// <param name="skSelect"></param>
        public void PlayBegin( int iMode, SKRectI skSelect ) {
            if( _oWorkPlace.Status == WorkerStatus.FREE ) {
			    // The DocSnip object retains ownership of it's generated bitmap and frees it on next load.
			    _oDocSnip.Load( Bitmap, skSelect, new SKSizeI( 320, 256 ) );
                if( GeneratorSetup( iMode, _oDocSnip.Bitmap ) ) {
                    _oWorkPlace.Queue( GetPlayerTask(), 0 );
                }
            }
            //while ( _oDataTester.ConsumeData() < 350000 ) {
            //}
        }

        public void PlayStop() {
            _oWorkPlace.Stop();
        }

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
