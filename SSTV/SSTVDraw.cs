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
    public class SSTVDraw {
		private            bool    _fDisposed = false;
        protected readonly SSTVDEM _dp;

		public SSTVMode Mode => _dp.Mode;
		public DateTime StartTime { get; protected set; }

		protected double   _dblReadBaseSync = 0;
		protected double   _dblReadBaseSgnl = 0;
        protected int      _AY;
	    protected short[]  _Y36 = new short[800];
	    protected short[,] _D36 = new short[2,800];

		protected int      _iLastAlign = -1;
		protected int[]    _rgRasters = new int[800];

		short[] _pCalibration = null; // Not strictly necessary yet.

		SKCanvas _skCanvas;
		SKPaint  _skPaint;

		public SKBitmap _pBitmapRX  { get; protected set; } 
		public SKBitmap _pBitmapD12 { get; } = new SKBitmap( 800, 616, SKColorType.Rgb888x, SKAlphaType.Unknown );
		// Looks like were only using grey scale on the D12. Look into turning into greyscale later.
		// Need to look into the greyscale calibration height of bitmap issue. (+16 scan lines)
		// The D12 bitmap must always be >= to the RX bmp height.

		public event SSTVPropertyChange ShoutTvEvents; // TODO: Since threaded version poles, we don't need this.

		protected readonly List<ColorChannel> _rgSlots = new (10);

		protected SlidingWindow Slider { get; }

		/// <remarks>
		/// Techically we dont need all of the demodulator but only access to the signal
		/// and sync buffer and some signal levels. I'll see about that in the future.
		/// </remarks>
		public SSTVDraw( SSTVDEM p_dp ) {
			_dp = p_dp ?? throw new ArgumentNullException( "Demodulator must not be null to SSTVDraw." );

			Slider    = new( 3000, 30, p_dp.m_SLvl ); // Put some dummy values for now. Start() updates.

			_skCanvas = new( _pBitmapD12 );
			_skPaint  = new() { Color = SKColors.Red, StrokeWidth = 1 };
		}

		/// <summary>
		/// Still tinkering with disposal methods. We're designed that we can re-use this 
		/// object as long as the _dp object is valid for the current sampling frequency.
		/// if the frequency changes, then the demodulator is no longer valid.
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

		/// <summary>this method get's called to initiate the processing of
		/// a new image.</summary>
        public void Start() {
			_iLastAlign      = -1;
			_dblReadBaseSync =  0;
			_dblReadBaseSgnl =  0;
			_AY				 = -5;

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

			StartTime = DateTime.Now;
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
		/// signal is time based and so might not exactly align with the discrete
		/// time interval of the sample. By doing everything in floating point we 
		/// won't slowly drift off due to rounding errors.
		/// </summary>
		/// <seealso cref="ScanWidthInSamples"/>
		public double SpecWidthInSamples {
			get {
				return Mode.ScanWidthInMS * _dp.SampFreq / 1000;
			}
		}

		/// <summary>
		/// Size of the scan line width corrected image not including VIS preamble.
		/// </summary>
		public double ImageSizeInSamples {
			get {
				try {
					return ScanWidthInSamples * Mode.Resolution.Height / Mode.ScanMultiplier;
				} catch( NullReferenceException ) {
					return 1;
				}
			}
		}

		/// <summary>
		/// Convert ip levels -16,384 (black) through 16,384 (white) to -128 to +128.
		/// </summary>
		/// <param name="ip">frequency as a level value.</param>
		/// <returns></returns>
		protected short GetPixelLevel(short ip)	{
			if( _dp.Sys.m_DemCalibration && (_pCalibration != null) ){
				int d = (ip / 8) + 2048;
				if( d < 0 )
					d = 0;
				if( d > 4096 )
					d = 4096;
				return _pCalibration[d];
			} else {
				double d = ip - _dp.Sys.m_DemOff;
				d *= ( d >= 0 ) ? _dp.Sys.m_DemWhite : _dp.Sys.m_DemBlack;
				return (short)d;
			}
		}

		protected static void YCtoRGB( out short R, out short G, out short B, int Y, int RY, int BY ) {
			Y -= 16;
			R = (short)(1.164457*Y + 1.596128*RY );
			G = (short)(1.164457*Y - 0.813022*RY - 0.391786*BY );
			B = (short)(1.164457*Y + 2.017364*BY );

			LimitRGB( ref R, ref G, ref B);
		}

		protected static void LimitRGB( ref short R, ref short G, ref short B ) {
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

		/// <summary>
		/// Track how much data has been read into the buffer. Note that the
		/// processor will backtrack and re-read the buffer, as it adjusts
		/// for the slant, but this is how far the reception has proceeded.
		/// </summary>
        public int PercentRxComplete { 
            get {
				try {
					double dblProgress = _dp.m_wBase * 100 / ImageSizeInSamples;
					return (int)dblProgress;
				} catch( NullReferenceException ) {
					return 100;
				}
            }
        }

		/// <summary>
		/// This is a whole hot mess of stuff and I'm not going to port it for now.
		/// </summary>
		/// <returns></returns>
		protected bool AutoStopJob() {
			return true;
		}

		/// <summary>
		/// Quick and dirty alignment based on the estimated scan width and start point.
		/// </summary>
		/// <param name="iScanMax">Maximum Scan Lines.</param>
		/// <param name="dblSlope">Scan line width in samples per ms.</param>
		/// <param name="dblIntercept">First usable scan line stream point.</param>
		/// <remarks>I moved this from the SlidingWindow class since doesn't used
		/// anything but the slope/intercept and we might set it with user interface
		/// int the future and this would easily accomodate that.</remarks>
		public void Interpolate( int iScanMax, double dblSlope, double dblIntercept ) {
			try {
				for( int i=0; i<iScanMax; ++i ) {
					int iEstimatedOffset = (int)Math.Round( dblSlope * i + dblIntercept );

					_rgRasters[i] = iEstimatedOffset;
                }
			} catch( Exception oEx ) {
				Type[] rgErrors = { typeof( NullReferenceException ),
									typeof( IndexOutOfRangeException ) };
				if( rgErrors.IsUnhandled( oEx ) )
					throw;

				ShoutTvEvents?.Invoke( ESstvProperty.ThreadDrawingException );
			}
		}

		/// <summary>
		/// Call this when reach end of file. This will also get called internally when we've
		/// decoded the entire image.
		/// </summary>
		public void Stop() {
			if( _dp.m_Sync ) {
				_dp.Stop();

				double slope     = 0;
				double intercept = 0;

				if( Slider.AlignLeastSquares( ref slope, ref intercept ) ) {
					//InitSlots( Mode.Resolution.Width, slope / SpecWidthInSamples );
					SKPaint skPaint = new() { Color = SKColors.Yellow, StrokeWidth = 2 };

					double dbD12XScale     = _pBitmapD12.Width / SpecWidthInSamples;
					double dblSyncExpected = _dp.Mode.OffsetInMS * _dp.SampFreq / 1000;
					float  flX             = (float)( intercept * dbD12XScale );
					double dblOffset       = ( Mode.Resolution.Height - 1 ) * slope + intercept;
					float  flX2            = (float)( dblOffset % SpecWidthInSamples * dbD12XScale );

					SKPoint top = new( flX,  0 );
					SKPoint bot = new( flX2, Mode.Resolution.Height - 1);
					
					_skCanvas.DrawLine( top, bot, skPaint );

					ShoutTvEvents?.Invoke( ESstvProperty.DownLoadFinished );
				}
			}
		}

		/// <summary>
		/// Process the Sync data buffer. We make only one pass the the buffer now.
		/// Draw on the D12 buffer sync diagnostics so we can see what we are doing.
		/// </summary>
		/// <param name="dblBase">Where to start reading from, this value
		/// should be at less than the wBase + SpecLineWidthInSamples.</param>
		/// <returns>The input base plus the SpecLineWidthInSamples.</returns>
		/// <remarks>Usue the ScanWidthInSamples (versus SpecWidthInSamples) so
		/// we can pick up corrections to the width. That way we can calculate the
		/// intercept.</remarks>
		public double ProcessSync( double dblBase ) {
			int    iReadBase   = (int)dblBase;
			int    iScanWidth  = (int)ScanWidthInSamples; // Make sure this matches our controlling loop!!
			double dbD12XScale = _pBitmapD12.Width / ScanWidthInSamples;
			int    iSyncWidth  = (int)( Mode.WidthSyncInMS * _dp.SampFreq / 1000 * dbD12XScale );
			int    iScanLine   = (int)Math.Round( dblBase / ScanWidthInSamples );

			// If we can't advance we'll get stuck in an infinite loop on our caller.
			if( iReadBase + iScanWidth >= _dp.m_wBase )
				throw new InvalidProgramException( "Hit the rails on the ProcessSync" );

			try {
				for( int i = 0; i < iScanWidth; i++ ) { 
					int   idx = iReadBase + i;
					short d12 = _dp.SyncGet( idx );
					bool fHit = Slider.LogSync( idx, d12 );

					int x = (int)( i * dbD12XScale );
					if( (x >= 0) && (x < _pBitmapD12.Width)) {
						int d = Limit256((short)(d12 * 256F / 4096F));
						if( fHit ) {
							_skCanvas.DrawLine( new SKPointI( x - iSyncWidth, iScanLine ), new SKPointI( x, iScanLine ), _skPaint);
						} else {
							_pBitmapD12.SetPixel( x, iScanLine, new SKColor( (byte)d, (byte)d, (byte)d ) );
						}
					}
				}
			} catch( Exception oEx ) {
				Type[] rgErrors = { typeof( ArgumentOutOfRangeException ),
									typeof( NullReferenceException ) };
				if( rgErrors.IsUnhandled( oEx ) )
					throw;

				ShoutTvEvents?.Invoke( ESstvProperty.ThreadDrawingException );
			}

			return( dblBase + ScanWidthInSamples );
		}

		/// <summary>
		/// This the second new scan line processor. Unlike the ProcessSync() function
		/// this one will be re-started from the beginning after a handfull of
		/// sync lines are processed. The idea is that we slowly refine our 
		/// measurement of the image and so need to redraw it. The signal buffer is
		/// currently large enough to allow us to re-draw from the beginning.
		/// I might cut it down in the future. But it makes sense that you really
		/// can't draw the image properly until it has been received in it's entirety.
		/// </summary>
		protected void ProcessScan( int iScanLine ) {
			int rx         = -1; // Saved X pos from the Rx buffer.
			int ch         =  0; // current channel skimming the Rx buffer portion.
			int iScanWidth = (int)Math.Round( ScanWidthInSamples );

			try { 
				int rBase = _rgRasters[iScanLine];

			    _AY = iScanLine * Mode.ScanMultiplier; 
				if( (_AY < 0) || (_AY >= _pBitmapRX.Height) )
					return;
				// Sometimes we bump the rails. Haven't figured it out yet.
				if( rBase + iScanWidth >= _dp.m_wBase )
					return;

				for( int i = 0; i < iScanWidth; i++ ) { 
					int idx = rBase + i;

					do {
						ColorChannel oChannel = _rgSlots[ch];
						if( i < oChannel.Max ) {
							if( oChannel.SetPixel != null ) {
								int x = (int)((i - oChannel.Min) * oChannel.Scaling );
								if( (x != rx) && (x >= 0) && (x < _pBitmapRX.Width) ) {
									rx = x; oChannel.SetPixel( x, _dp.SignalGet( idx ) );
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

		/// <summary>
		/// This is our main processing entry point. The data is being loaded into the buffer
		/// and we read it out here.
		/// </summary>
		public void Process() {
			if( _dp.m_Sync ) {
				try {
					while( _dp.m_wBase > _dblReadBaseSync + ScanWidthInSamples ) {
						_dblReadBaseSync = ProcessSync( _dblReadBaseSync );

						ShoutTvEvents?.Invoke( ESstvProperty.DownLoadTime );
					}
				} catch( Exception oEx ) {
					Type[] rgErrors = { typeof( NullReferenceException ),
										typeof( InvalidProgramException ),
										typeof( IndexOutOfRangeException ) };
					if( rgErrors.IsUnhandled( oEx ) )
						throw;

					ShoutTvEvents?.Invoke( ESstvProperty.ThreadDrawingException );
				}

				try {
					int iScanLine = (int)(_dp.m_wBase / ScanWidthInSamples );
					if( iScanLine % 20 == 19 ) {
						double dblSlope     = 0;
						double dblIntercept = 0;
						int    iScanLines   = Mode.Resolution.Height / Mode.ScanMultiplier;

						if( _iLastAlign < iScanLine) {
							// Clear's the sync data and force re-read from the buffer.
							Slider.Shuffle( ScanWidthInSamples );
							if( Slider.AlignLeastSquares( ref dblSlope, ref dblIntercept ) ) {
								int iSyncWidth = (int)(Mode.OffsetInMS * _dp.SampFreq / 1000);
								dblIntercept -=iSyncWidth;
								Slider.Reset( dblSlope,  (int)(Mode.WidthSyncInMS * _dp.SampFreq / 1000) );

								Interpolate( iScanLines, dblSlope, dblIntercept );
								InitSlots  ( Mode.Resolution.Width, dblSlope / SpecWidthInSamples ); // updates the ScanWidthInSamples.
								_dblReadBaseSync = 0;
								_iLastAlign      = iScanLine;
							}
							for( int i = 0; i<iScanLine; ++i ) {
								ProcessScan( i );
							}
						}
					}
				} catch( Exception oEx ) {
					Type[] rgErrors = { typeof( NullReferenceException ),
										typeof( InvalidProgramException ),
										typeof( IndexOutOfRangeException ) };
					if( rgErrors.IsUnhandled( oEx ) )
						throw;

					ShoutTvEvents?.Invoke( ESstvProperty.ThreadDrawingException );
				}
			}
		}

		protected void PixelSetGreen( int iX, short sValue ) {
			sValue      = (short)( GetPixelLevel(sValue) + 128 );
			_D36[0,iX] = (short)Limit256(sValue);
		}

		protected void PixelSetBlue( int iX, short sValue ) {
			sValue      = (short)( GetPixelLevel(sValue) + 128 );
			_D36[1,iX] = (short)Limit256(sValue);
		}

		/// <summary>
		/// Cache the Green and Blue values first and finish with this call.
		/// </summary>
		protected void PixelSetRed( int iX, short sValue ) {
			sValue = (short)( GetPixelLevel(sValue) + 128 );
			_pBitmapRX.SetPixel( iX, _AY,  new SKColor( (byte)Limit256(sValue), 
				                                        (byte)_D36[0,iX], 
														(byte)_D36[1,iX] ) );
		}

		protected void PixelSetY1( int iX, short sValue ) {
			_Y36[iX] = (short)( GetPixelLevel(sValue) + 128 );
		}

		protected void PixelSetRY( int iX, short sValue ) {
			_D36[1,iX] = GetPixelLevel( sValue );
		}

		protected void PixelSetBY( int iX, short sValue ) {
			_D36[0,iX] = GetPixelLevel( sValue );
		}

		/// <summary>
		/// Cache the RY and BY values first and finish with this call.
		/// </summary>
		protected void PixelSetY2( int iX, short sValue ) {
			short R, G, B;

			YCtoRGB( out R, out G, out B, _Y36[iX], _D36[1,iX], _D36[0,iX]);
			_pBitmapRX.SetPixel( iX, _AY,   new SKColor( (byte)R, (byte)G, (byte)B ) );

			sValue = (short)( GetPixelLevel(sValue) + 128 );

			YCtoRGB( out R, out G, out B, sValue,    _D36[1,iX], _D36[0,iX]);
			_pBitmapRX.SetPixel( iX, _AY+1, new SKColor( (byte)R, (byte)G, (byte)B ) );
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
