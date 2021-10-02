using System;
using System.Collections.Generic;

using SkiaSharp;

using Play.Interfaces.Embedding;
using Play.Sound;

namespace Play.SSTV {
	/// <summary>
	/// The demodulator converts the signal from the time to frequency domain. In the original
	/// code. It looks like it lives on it's own thread. I'm going to put it with the demodulator
	/// in the same thread. 
	/// </summary>
	/// <remarks>I've removed the disposable on this class because I'm going to lean on the GC 
	/// to clean up the unused bitmaps since they are shared with the UI thread. I'll see how 
	/// bad of a problem this is and deal with it if necessary. Rust language, here I come...</remarks>
    public class TmmSSTV {
		private            bool     _fDisposed = false;
        protected readonly CSSTVDEM _dp;
		protected          int      _iSyncOffset = 0; // 90 pd, 240 sc1, Use to correct image offset!!
		protected		   int      _iSyncCheck  = 4;

		public SSTVMode Mode => _dp.Mode;

        protected int      m_AY;
	    protected short[]  m_Y36 = new short[800];
	    protected short[,] m_D36 = new short[2,800];

		protected IPgStreamConsumer<short> _oStmSignal;

		short[] _pCalibration = null; // Not strictly necessary yet.

		public SKBitmap _pBitmapRX  { get; protected set; } 
		public SKBitmap _pBitmapD12 { get; } = new SKBitmap( 800, 616, SKColorType.Rgb888x, SKAlphaType.Unknown );
		// Looks like were only using grey scale on the D12. Look into turning into greyscale later.
		// Need to look into the greyscale calibration height of bitmap issue. (+16 scan lines)
		// The D12 bitmap must always be >= to the RX bmp height.

		public event SSTVPropertyChange ShoutTvEvents; // TODO: Since threaded version poles, we don't need this.

		protected readonly List<ColorChannel> _rgSlots = new List<ColorChannel>(10);

		protected SlidingWindow Slider { get; }

		public TmmSSTV( CSSTVDEM p_dp ) {
			_dp         = p_dp ?? throw new ArgumentNullException( "CSSTVDEM" );
			_oStmSignal = p_dp.CreateConsumer() ?? throw new InvalidProgramException( "Could not create stream consumer." );

			Slider = new( 3000, 30, p_dp.m_SLvl );

		  //StartOption() ...
		  //dp.sys.m_bCQ100 = FALSE;
		}

		/// <summary>
		/// Still tinkering with disposal methods. At present we don't call dispose and just
		/// hope the GC will clean up fast enough.
		/// </summary>
		/// <remarks>I read about this pattern for multithreaded objects and checking flags.
		/// I wish I could recall the paper. ^_^;; The idea is that if we get prempted and
		/// the other thread gets dispose called we'll pick up on that when we enter.</remarks>
		public void Dispose() {
			if( !_fDisposed ) {
				_fDisposed = true;
				if( !_fDisposed ) {
					_pBitmapD12?.Dispose();
					_pBitmapRX ?.Dispose();
				}
			}
		}

		/// <remarks>
		/// Unfortunately we don't presently know if we are in VIS detect mode
		/// and thus where SSTVSync() should be called.
		/// </remarks>
        public void Start() {
			m_AY			= -5;
			_iSyncCheck     =  4; // See SSTVSync()
			
			Slider.Reset( SpecWidthInSamples, (int)(Mode.WidthSyncInMS * _dp.SampFreq / 1000) );

			ShoutTvEvents?.Invoke(ESstvProperty.SSTVMode );

			if( _pBitmapRX == null ||
				_dp.Mode.Resolution.Width  != _pBitmapRX.Width ||
				_dp.Mode.Resolution.Height != _pBitmapRX.Height   )
			{
				// Don't dispose! The UI thread might be referencing the object for read. Instead
				// We'll just drop it and let the UI do that when it catches up and let the GC
				// clean up. Given we're not creating these like wild fire I think we won't have
				// too much memory floating around.
				//if( _pBitmapRX != null )
				//	_pBitmapRX.Dispose(); // <--- BUG: Right here, UI thread will be in trouble if still reading...
				_pBitmapRX = new SKBitmap( _dp.Mode.Resolution.Width, 
										   _dp.Mode.Resolution.Height, 
										   SKColorType.Rgb888x, 
										   SKAlphaType.Opaque );

				ShoutTvEvents?.Invoke(ESstvProperty.RXImageNew );
			}

			//::GetUTC(&m_StartTime);
        }

		/// <summary>
		/// Possibly corrected scan line width in samples.
		/// </summary>
		/// <seealso cref="SpecWidthInSamples"/>
		public double ScanWidthInSamples {
			get {
				try {
					return _rgSlots[_rgSlots.Count - 1 ].Min;
				} catch( ArgumentOutOfRangeException ) {
					return 0;
				}
			}
		}

		/// <summary>
		/// scan line width in samples returned by the spec. Sister to the 
		/// ScanWidthInSamples property except this is the UNCORRECTED value.
		/// This is a floating point number because the 
		/// signal is time based and so might not exactly align with the descrete
		/// time interval of the sample. By doing everything in floating point we won't slowly
		/// drift off due to rounding errors.
		/// </summary>
		/// <seealso cref="ScanWidthInSamples"/>
		public double SpecWidthInSamples {
			get {
				return Mode.ScanWidthInMS * _dp.SampFreq / 1000;
			}
		}

		/// <summary>
		/// Convert ip levels -16,384 (black) through 16,384 (white) to -128 to +128.
		/// </summary>
		/// <param name="ip">frequency as a level value.</param>
		/// <returns></returns>
		protected short GetPixelLevel(short ip)	{
			if( _dp.sys.m_DemCalibration && (_pCalibration != null) ){
				int d = (ip / 8) + 2048;
				if( d < 0 )
					d = 0;
				if( d > 4096 )
					d = 4096;
				return _pCalibration[d];
			} else {
				double d = ip - _dp.sys.m_DemOff;
				d *= ( d >= 0 ) ? _dp.sys.m_DemWhite : _dp.sys.m_DemBlack;
				return (short)d;
			}
		}

		protected static void YCtoRGB( out short R, out short G, out short B, int Y, int RY, int BY)
		{
			Y -= 16;
			R = (short)(1.164457*Y + 1.596128*RY );
			G = (short)(1.164457*Y - 0.813022*RY - 0.391786*BY );
			B = (short)(1.164457*Y + 2.017364*BY );

			LimitRGB( ref R, ref G, ref B);
		}

		protected static void LimitRGB( ref short R, ref short G, ref short B)
		{
			R = Limit256(R);
			G = Limit256(G);
			B = Limit256(B);
		}

		protected static short Limit256(short d) {
			if( d < 0 )
				d = 0;
			if( d > 255 )
				d = 255;

			return d;
		}

		protected static short Limit256(int d) {
			if( d < 0 )
				d = 0;
			if( d > 255 )
				d = 255;

			return (short)d;
		}

        public int PercentRxComplete { 
            get {
				return ( m_AY * 100 / Mode.Resolution.Height ) ;
            }
        }

		protected int LineMultiplier { get; set; }

		/// <summary>
		/// This is a whole hot mess of stuff and I'm not going to port it for now.
		/// </summary>
		/// <returns></returns>
		protected bool AutoStopJob() {
			return true;
		}

		/// <summary>
		/// Call this when reach end of file. This will also get called internally when we've
		/// decoded the entire image.
		/// </summary>
		public void Stop() {
			if( _dp.m_Sync ) {
				_dp.Stop();

				if( Slider.AlignLeastSquares( Mode.Resolution.Height, out double slope, out double intercept ) ) {
					//InitSlots( Mode.Resolution.Width, slope / SpecWidthInSamples );

					SKCanvas skCanvas = new SKCanvas(_pBitmapD12);
					SKPaint  skPaint  = new SKPaint () { Color = SKColors.Green, StrokeWidth = 2 };

					double dbD12XScale     = _pBitmapD12.Width / ScanWidthInSamples;
					double dblSyncExpected = _dp.Mode.OffsetInMS * _dp.SampFreq / 1000;
					float  flX             = (float)(intercept * dbD12XScale);
					
					skCanvas.DrawLine( new SKPoint( flX, 0f ), new SKPoint( flX, _pBitmapD12.Height ), skPaint );

					ShoutTvEvents?.Invoke( ESstvProperty.DownLoadFinished );
				}
			}
		}

		/// <summary>
		/// Call this to attempt a re-align of the image.
		/// </summary>
		/// <remarks>This is getting better, but it is still sensitive to spurious
		/// hsync hits. I'm going to try to filter out points that don't fit
		/// on the prevailing line.</remarks>
		protected bool Align() {
			if( Slider.AlignLeastSquares( m_AY, out double slope, out double intercept ) ) {
				InitSlots( Mode.Resolution.Width, slope / SpecWidthInSamples );

				double dblSyncExpected = _dp.Mode.OffsetInMS * _dp.SampFreq / 1000;
				double dblDiff         = intercept - dblSyncExpected;

				if( dblDiff > 0 ) {
					_dp.PageRReset( dblDiff );
				} else {
					_dp.PageRReset( dblDiff + ScanWidthInSamples );
				}
				Slider.Reset( /* slope, (int)(Mode.WidthSyncInMS * _dp.SampFreq / 1000) */ );
				return true;
			}
			return false;
		}

		/// <summary>
		/// Call this function periodically to collect the sstv scan lines.
		/// </summary>
		/// <remarks>This function will loop until the rPage catches up with the wPage. 
		/// </remarks>
		public void DrawProcess() {
			while( _dp.m_Sync && (_dp.m_wBase >=_dp.m_rBase + ScanWidthInSamples ) ){
				DrawScan();

				_dp.PageRIncrement( ScanWidthInSamples );

				if( (int)(_dp.m_wBase / ScanWidthInSamples ) % 20 == 19 ) {
					Align();
					break;
				}

				if( m_AY >= _dp.Mode.Resolution.Height ){ 
					Stop();
					break;
				} else {
					ShoutTvEvents?.Invoke( ESstvProperty.DownLoadTime );
				}
			}
		}

		/// <summary>
		/// This code both tracks the hsync signal, drawing it to the B12 diagnositc
		/// bitmap, AND pulls the RX signal, drawing that to the BRX bitmap.
		/// </summary>
		/// <remarks>
		/// Turns out we end up redoing both that work every time we do a alignment
		/// reset. That's unncessary. I'm going to make a new indexer so we can
		/// track each separately. Only the BRX image needs to be reset since 
		/// it benefits from the collection of sync data.</remarks>
		protected void DrawScan() {
			int    dx          = -1; // Saved X pos from the B12 buffer.
			int    rx          = -1; // Saved X pos from the Rx  buffer.
			int    ch          =  0; // current channel skimming the Rx buffer portion.
			double dbScanWidth = ScanWidthInSamples;
			int    iScanWidth  = (int)Math.Round( dbScanWidth );
			int    rBase       = (int)Math.Round( _dp.m_rBase );
			double dbD12XScale = _pBitmapD12.Width / dbScanWidth;
			int    iSyncWidth  = (int)(Mode.WidthSyncInMS * _dp.SampFreq / 1000 );

			try { 
				int iScanLine = (int)Math.Round(rBase/dbScanWidth);
				//if( Slider[iScanLine] > -1 ) -- This can only work if we pre process
				//	rBase = Slider[iScanLine]; -- the d12 first, then check this.

			    m_AY = iScanLine * LineMultiplier; // PD needs us to use Round (.999 is 1)
				if( (m_AY < 0) || (m_AY >= _pBitmapRX.Height) )
					return;

				for( int i = 0; i < iScanWidth; i++ ) { 
					int   idx = rBase + i;
					short d12 = _dp.SyncGet( idx );

					#region D12
					bool fHit = Slider.LogSync( idx, d12 );

					int x = (int)( i * dbD12XScale );
					if( (x != dx) && (x >= 0) && (x < _pBitmapD12.Width)){
						int d = Limit256((short)(d12 * 256F / 4096F));
						if( fHit ) {
							_pBitmapD12.SetPixel( x, m_AY, SKColors.Red );
						} else
							_pBitmapD12.SetPixel( x, m_AY, new SKColor( (byte)d, (byte)d, (byte)d ) );
						dx = x;
					}
					#endregion

					do {
						ColorChannel oChannel = _rgSlots[ch];
						if( i < oChannel.Max ) {
							if( oChannel.SetPixel != null ) {
								x = (int)((i - oChannel.Min) * oChannel.Scaling );
								if( (x != rx) && (x >= 0) && (x < _pBitmapRX.Width) ) {
									rx = x; oChannel.SetPixel( x, _oStmSignal.Read( idx ) );
								}
							}
							break;
						}
					} while( ++ch < _rgSlots.Count );
					// We'll throw an exception before ever getting to the bottom.
				} // End for
			} catch( Exception oEx ) {
				Type[] rgErrors = { typeof( ArgumentOutOfRangeException ),
									typeof( IndexOutOfRangeException ),
									typeof( NullReferenceException ),
									typeof( DivideByZeroException )
								  };
				if( rgErrors.IsUnhandled( oEx ) )
					throw;

				ShoutTvEvents?.Invoke( ESstvProperty.ThreadDrawingException );
			}
		}

		// I'm going to disable this for awhile, it moves outside the buffer
		// in some cases.
		/// <summary>Horizontal Offset correction. Not slant, but try to
		/// shift whole image left and right. Looking for the 1200hz tone,
		/// and then seeing if it is where we expect it. Looks like we
		/// sum over 4 lines of data in case we miss parts of the image.</summary>
		/// <remarks>There's a bug here where we make an offset so large we
		/// move out of the range of the input data. Trying to fix that.
		/// Given a scenario where we just start randomly in the image
		/// data, I'm not seeing how this works.</remarks>
		public void AlignHorizontal() {
            if( m_AY >= _iSyncCheck )  { // was >=
				int   iSW           = (int)ScanWidthInSamples;
                int[] bp            = new int[iSW]; // Array.Clear( bp, 0, bp.Length );
				int   iOffsExpected = (int)_dp.Mode.OffsetInMS * _dp.SampFreq / 1000; // result in samples offset.

				// sum into bp[] four scan lines of data.
                for( int pg = 0; pg < _iSyncCheck; pg++ ){
                	int  ip = pg * iSW;
                	for( int i = 0; i < iSW; i++ ){
						bp[i] += _dp.SyncGet(ip + i);
                	}
                }
				// then go back and look for the cumulative max.
                int iOffsFound = 0;
                int iSignalMax = 0;
                for( int i = 0; i < bp.Length; i++ ){
                	if( iSignalMax < bp[i] ){
                		iSignalMax = bp[i];
                		iOffsFound   = i;
                	}
                }
                iOffsFound -= iOffsExpected; // how much is too much?!
                if( _dp.m_Type == FreqDetect.Hilbert ) 
                	iOffsFound -= _dp.HillTaps/4; // BUG: Add instead of subtract?

				_iSyncCheck  = int.MaxValue; // Stop from trying again.
				_iSyncOffset = iOffsFound;
				m_AY         = 0;
				_dp.PageRReset();
				Slider.Reset();
                //SstvSet.SetOFS( n );
            }
        }

		protected void PixelSetGreen( int iX, short sValue ) {
			sValue      = (short)( GetPixelLevel(sValue) + 128 );
			m_D36[0,iX] = (short)Limit256(sValue);
		}

		protected void PixelSetBlue( int iX, short sValue ) {
			sValue      = (short)( GetPixelLevel(sValue) + 128 );
			m_D36[1,iX] = (short)Limit256(sValue);
		}

		/// <summary>
		/// Cache the Green and Blue values first and finish with this call.
		/// </summary>
		protected void PixelSetRed( int iX, short sValue ) {
			sValue = (short)( GetPixelLevel(sValue) + 128 );
			_pBitmapRX.SetPixel( iX, m_AY,  new SKColor( (byte)Limit256(sValue), 
				                                        (byte)m_D36[0,iX], 
														(byte)m_D36[1,iX] ) );
		}

		protected void PixelSetY1( int iX, short sValue ) {
			m_Y36[iX] = (short)( GetPixelLevel(sValue) + 128 );
		}

		protected void PixelSetRY( int iX, short sValue ) {
			m_D36[1,iX] = GetPixelLevel( sValue );
		}

		protected void PixelSetBY( int iX, short sValue ) {
			m_D36[0,iX] = GetPixelLevel( sValue );
		}

		/// <summary>
		/// Cache the RY and BY values first and finish with this call.
		/// </summary>
		protected void PixelSetY2( int iX, short sValue ) {
			short R, G, B;

			YCtoRGB( out R, out G, out B, m_Y36[iX], m_D36[1,iX], m_D36[0,iX]);
			_pBitmapRX.SetPixel( iX, m_AY,   new SKColor( (byte)R, (byte)G, (byte)B ) );

			sValue = (short)( GetPixelLevel(sValue) + 128 );

			YCtoRGB( out R, out G, out B, sValue,    m_D36[1,iX], m_D36[0,iX]);
			_pBitmapRX.SetPixel( iX, m_AY+1, new SKColor( (byte)R, (byte)G, (byte)B ) );
		}

		public setPixel ReturnColorFunction( ScanLineChannelType eDT ) {
			switch( eDT ) {
				case ScanLineChannelType.BY:          return PixelSetBY;
				case ScanLineChannelType.RY:          return PixelSetRY;
				case ScanLineChannelType.Y1:          return PixelSetY1;
				case ScanLineChannelType.Y2:          return PixelSetY2;
				case ScanLineChannelType.Blue  : return PixelSetBlue;
				case ScanLineChannelType.Red   : return PixelSetRed;
				case ScanLineChannelType.Green : return PixelSetGreen;
				default : return null;
			}
		}

		/// <summary>
		/// Change the image parsing mode we are in. 
		/// </summary>
		/// <param name="oMode"></param>
		public void ModeTransition( SSTVMode oMode ) {
			try {
				LineMultiplier = oMode.ScanMultiplier;

                _rgSlots.Clear();

                foreach( ScanLineChannel oChannel in oMode.ChannelMap ) {
                	_rgSlots.Add( new( oChannel.WidthInMs * _dp.SampFreq / 1000, ReturnColorFunction( oChannel.Type ) ) );
                }
                _rgSlots.Add( new() );

                InitSlots( oMode.Resolution.Width, 1 );
            } catch( Exception oEx ) {
				Type[] rgErrors = { typeof( NullReferenceException ),
									typeof( ArgumentOutOfRangeException ),
									typeof( ArgumentNullException ),
									typeof( ArgumentException ) };
				if( rgErrors.IsUnhandled( oEx ) )
					throw;

				// Uh oh.
			}

			Start(); // bitmap allocated in here.
		}

		/// <summary>
		/// This goes back and finishes out the slots so their Min and Max
		/// boundaries are assigned. Also set's the scale factor to align
		/// the bitmap width with the slot width (color channel width).
		/// </summary>
		/// <seealso cref="DemodTest.Write(int, uint, double)"/>
		protected void InitSlots( int iBmpWidth, double dbCorrection ) {
			double dbIdx = 0;
			for( int i = 0; i< _rgSlots.Count; ++i ) {
				dbIdx = _rgSlots[i].Reset( iBmpWidth, dbIdx, dbCorrection );
			}
		}
    } // End Class TmmSSTV

	public delegate void setPixel( int iX, short sLevel );

	/// <summary>
	/// This object represents one portion of the video scan line. String these
	/// together to create a complete scan line parse.
	/// </summary>
	public class ColorChannel {
		public double SpecWidthInSamples { get; } // The original specification.
		public double _dbScanWidthCorrected;      // Compensated value.

		public double   Min      { get; set; }
		public double   Max      { get; protected set; }
		public setPixel SetPixel { get; protected set; }
		public double   Scaling  { get; protected set; }

		public ColorChannel( double dbWidthInSamples, setPixel fnSet ) {
			SpecWidthInSamples = dbWidthInSamples;
			SetPixel           = fnSet;
		}

		public ColorChannel() {
			SpecWidthInSamples = double.MaxValue;
			SetPixel           = null;
		}

		public double Reset( int iBmpWidth, double dbStart, double dbCorrection ) {
			_dbScanWidthCorrected = SpecWidthInSamples * dbCorrection;

			Scaling = iBmpWidth / _dbScanWidthCorrected;

			Min = dbStart;
			Max = dbStart + ( _dbScanWidthCorrected );

			return Max;
		}

        public override string ToString() {
			string strType = SetPixel != null ? SetPixel.GetType().ToString() : string.Empty;
            return Max.ToString() + " " + strType;
        }
    }

}
