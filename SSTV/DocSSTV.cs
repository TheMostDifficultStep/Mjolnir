using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;

using Play.Interfaces.Embedding;
using Play.Sound;
using Play.Sound.FFT;

namespace Play.SSTV {
    public delegate void FFTOutputEvent();

    public class DocSSTV :
        IPgParent,
        IPgLoad<TextReader>,
        IPgSave<TextWriter>,
        IDisposable
    {
        private bool disposedValue;

        protected readonly IPgBaseSite _oSiteBase;

        public IPgParent Parentage => _oSiteBase.Host;
        public IPgParent Services  => throw new NotImplementedException();
        public bool      IsDirty   => false;

        public IPgReader FileDecoder { get; protected set; }

		readonly IPgSound _oSound;

		double[] _rgFFTData;      // Data in double precision point.
        byte[]   _rgFFTDataBytes; // Data in bytes. Gross, we'll fix this in the future.
		CFFT     _oFFT;

        public int[] FFTResult { get; protected set; }
        public int   FFTResultSize => _oFFT.ControlMode.TopBucket;

        public event FFTOutputEvent FFTOutputNotify;

        public DocSSTV( IPgBaseSite oSite ) {
            _oSiteBase = oSite ?? throw new ArgumentNullException( "Site must not be null" );
			_oSound    = oSite.Host.Services as IPgSound ?? throw new ArgumentNullException( "Host requires IPgSound." );
        }

        #region Dispose
        protected virtual void Dispose( bool disposing ) {
            if( !disposedValue ) {
                if( disposing ) {
                    FileDecoder.Dispose();
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

        protected static void LoadData( FFTControlValues oCtrl, List<double> rgData ) {
            // We need FFTSize number of samples.
			rgData.Clear();
            for( double t = 0; rgData.Count < oCtrl.FFTSize; t += 1 / oCtrl.SampBase ) {
                double dbSample = 0;
                    
                dbSample += 80 * Math.Sin( Math.PI * 2 *  400 * t);
                dbSample += 20 * Math.Sin( Math.PI * 2 *  200 * t);
                dbSample += 20 * Math.Sin( Math.PI * 2 * 1200 * t);

                rgData.Add(dbSample);
            }
        }

        public bool InitNew2() {
            _oFFT = new CFFT( FFTControlValues.FindMode( 8000 ) );
			FFTResult = new int[_oFFT.ControlMode.FFTSize/2 + 1];

            List<double> rgFFTData = new List<double>();

			LoadData( _oFFT.ControlMode, rgFFTData );

			_oFFT.Calc( rgFFTData.ToArray(), 30, 0, FFTResult );

            return true;
        }

        public bool InitNew() {
			string strSong = @"C:\Users\Frodo\Documents\signals\1kHz_Left_Channel.mp3";

			try {
                FileDecoder = _oSound.CreateSoundDecoder( strSong );
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

            int iFFTSizeInSrcBytes = (int)_oFFT.ControlMode.FFTSize * (int)( FileDecoder.Spec.BitsPerSample / 8 ) * FileDecoder.Spec.Channels;

            _rgFFTDataBytes = new byte[iFFTSizeInSrcBytes];
			FFTResult       = new int[_oFFT.ControlMode.FFTSize/2];
            _rgFFTData      = new double[_oFFT.ControlMode.FFTSize];

            Array.Clear( _rgFFTDataBytes, 0, _rgFFTDataBytes.Length );

            return true;
        }

        public bool Load( TextReader oStream ) {
            return InitNew();
        }

        public bool Save( TextWriter oStream ) {
            return true;
        }

        /// <summary>
        /// Depending on the spec we have to determine how much
        /// of the data stream to read, stereo or mono.
        /// </summary>
        protected void ReadData() {
            int iDataLen     = _rgFFTData.Length;
            int iChannelUsed = 0;
            int iChannels    = FileDecoder.Spec.Channels;

            // This would be a great opportunity for reader interface that returned
            // different types per sample, byte, short, long, double...
            uint iBytesRead = FileDecoder.Read( _rgFFTDataBytes, 0, (uint)_rgFFTDataBytes.Length );

            if( iBytesRead < _rgFFTDataBytes.Length )
                return;
            if( iChannels == 1 ) // If data is mono, channel used needs to be zero.
                iChannelUsed = 0;

	        if( FileDecoder.Spec.BitsPerSample == 16 ){	
                unsafe {
                    fixed( double * pFFTTrg = _rgFFTData ) {
                        fixed( void * pFFTSrc = _rgFFTDataBytes ) {
                            short * pShortSrc = (short*)pFFTSrc;
			                for( int i = 0, j=iChannelUsed; i < iDataLen; i++, j+=iChannels ) {
				                pFFTTrg[i] = (double)pShortSrc[j];
			                }
                        }
                    }
                }
	        } else {
                throw new NotImplementedException("Opps need to implement.");
            }
        }

        public void PlaySegment() {
            ReadData();

            _oFFT.Calc( _rgFFTData.ToArray(), 30, 0, FFTResult );

            FFTOutputNotify?.Invoke();
        }
    }
}
