using System;
using System.IO;
using System.Collections.Generic;

using SkiaSharp;

using Play.Interfaces.Embedding;
using Play.Sound;
using Play.Sound.FFT;

namespace Play.SSTV {
    public enum ESstvProperty {
        ALL,
        UploadTime,
        SSTVMode,
        FFT
    }

    public delegate void SSTVPropertyChange( ESstvProperty eProp );

    public class DocSSTV :
        IPgParent,
        IPgLoad<TextReader>,
        IPgSave<TextWriter>,
        IDisposable
    {
        private bool disposedValue;

        protected readonly IPgBaseSite       _oSiteBase;
		protected readonly IPgRoundRobinWork _oWorkPlace;

        public IPgParent Parentage => _oSiteBase.Host;
        public IPgParent Services  => Parentage;
        public bool      IsDirty   => false;

        public event SSTVPropertyChange PropertyChange;

        DataTester _oDataTester;

        public string BitmapFileName { get; } = @"C:\Users\Frodo\Documents\signals\test-images\tofu3-320-256.jpg"; 

        public SSTVMode TransmitMode { get; protected set; }

        protected Mpg123FFTSupport FileDecoder { get; set; }
        protected BufferSSTV       SSTVBuffer  { get; set; }
        public    SKBitmap         Bitmap      { get; protected set; }
		protected IPgPlayer        _oPlayer;

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
        /// 
        /// </summary>
        /// <exception cref="ArgumentNullException" />
        /// <exception cref="ApplicationException" />
        public DocSSTV( IPgBaseSite oSite ) {
            _oSiteBase  = oSite ?? throw new ArgumentNullException( "Site must not be null" );
            _oWorkPlace = ((IPgScheduler)Services).CreateWorkPlace() ?? throw new ApplicationException( "Couldn't create a worksite from scheduler.");
        }

        #region Dispose
        protected virtual void Dispose( bool disposing ) {
            if( !disposedValue ) {
                if( disposing ) {
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

        protected bool LoadBitmap( string strFile ) {
            Bitmap = SKBitmap.Decode( strFile );

            return Bitmap != null;
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

        public bool InitNew() {
            if( !LoadBitmap( BitmapFileName ) )
                return false;

            try {
                Specification oSpec          = new Specification( 44100, 1, 0, 16 );
                              SSTVBuffer     = new BufferSSTV( oSpec );
                CSSTVMOD      oSSTVModulator = new CSSTVMOD( 0, oSpec.Rate, SSTVBuffer );

                //SSTVMode      oMode          = new SSTVMode( 0x3c, "Scotty 1",  138.240 );
                //SSTVGenerator oSSTVGenerator = new GenerateScottie( Bitmap, oSSTVModulator, oMode );

                //SSTVMode      oMode          = new SSTVMode( 0x28, "Martin 2",   73.216 );
                //SSTVGenerator oSSTVGenerator = new GenerateMartin(Bitmap, oSSTVModulator, oMode);

                TransmitMode = new SSTVMode( 0x63, "PD 90",   170.240 );
                SSTVGenerator oSSTVGenerator = new GeneratePD( Bitmap, oSSTVModulator, TransmitMode );

                SSTVBuffer.Pump = oSSTVGenerator.GetEnumerator();

                IEnumerator<string> oIter  = MMHelpers.GetOutputDevices();
                List<string>        rgDevs = new List<string>();
                while( oIter.MoveNext() ) {
                    rgDevs.Add( oIter.Current );
                }

                _oPlayer = new WmmPlayer(oSpec, 1); 

                //_oDataTester = new DataTester( SSTVBuffer );
                //SSTVBuffer.Pump = _oDataTester.GetEnumerator();

                // No point in raising any property change events, no view will be there to see them.
            }
            catch( ArgumentOutOfRangeException ) {
                return false;
            }

            return true;
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
        }

        public bool Load( TextReader oStream ) {
            return InitNew();
        }

        public bool Save( TextWriter oStream ) {
            return true;
        }

        public void PlayBegin() {
            _oWorkPlace.Queue( GetPlayerTask(), 0 );
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
