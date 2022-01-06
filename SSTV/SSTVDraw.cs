﻿using System;
using System.Collections.Generic;
using System.Linq;

using SkiaSharp;

using Play.Interfaces.Embedding;
using Play.Sound;

namespace Play.SSTV {
	public class SlidingWindow {
		protected class MyDatum {
			public MyDatum( double Bucket, int Position ) {
				this.Bucket   = Bucket;
				this.Position = Position;
			}
			public double  Bucket { get; set; }
			public int     Position { get; }

			public override string ToString() { return Position.ToString(); }
		}

		protected struct RasterEntry {
			public RasterEntry( MyDatum oDatum, int iCount ) {
				Datum = oDatum;
				Count = iCount;
			}

			public MyDatum Datum { get; set; }
			public int     Count { get; set; }

			public override string ToString() { return Count.ToString(); }
		}

		int    _iWindowSizeInSamples  = 0;
		int    _iWindowSum   = 0;
		double _iWindowHit; 
		int    _iW           = 0;
		double _SLvl         = 0;
		int[]  _rgWindow;
		bool   _fTriggered   = false;

		readonly List<MyDatum> _rgData = new(1000);
		readonly RasterEntry[] _rgRasters = new RasterEntry[850];

		public SlidingWindow( int iWindowSizeInSamples, double dblSLvl ) {
			_SLvl       = dblSLvl;
			_rgWindow   = new int[iWindowSizeInSamples*2];

			Reset( iWindowSizeInSamples );
		}

		public void Reset( int iWindowSizeInSamples ) {
			_iWindowSizeInSamples = iWindowSizeInSamples;

			int iNewSize = iWindowSizeInSamples*2;

			if( _rgWindow.Length < iNewSize )
				_rgWindow = new int[iNewSize];

			Reset();
		}

		public void Reset() {
			_fTriggered = false;

			Array.Clear( _rgWindow, 0, _rgWindow .Length );

			_rgData.Clear();

			_iWindowSum = 0;
			_iW         = _iWindowSizeInSamples;
			_iWindowHit = Math.Round( (double)_iWindowSizeInSamples );
		}

		/// <summary>
		/// We used to save all sync hits but basically if there is more than one
		/// hit I should probably just toss the line. As it is I ignore
		/// lines with more than one hit.
		/// </summary>
		/// <param name="iBucket"></param>
		/// <param name="oDatum"></param>
		protected void RasterAdd( int iBucket, MyDatum oDatum ) {
			if( _rgRasters[iBucket].Datum == null ) {
				_rgRasters[iBucket].Datum = oDatum;
				_rgRasters[iBucket].Count = 1;
			} else {
				_rgRasters[iBucket].Datum = oDatum;
				_rgRasters[iBucket].Count++;
			}
		}

		/// <summary>
		/// Log the sync channel. If the d12 signal is above the threshold we save
		/// the offset, modulo, the scan line width. We save the first offset that
		/// satisfies the window constraint. That way if we subtract the expected
		/// sync width we are at the start of a scan line!
		/// </summary>
		/// <remarks>We save 2x the window samples so we can always go back to the
		/// start of the window to subtract that contribution from the sum. This 
		/// makes us O(n) operation instead of O(n^2), if we had to re-sum the
		/// previous signals in the window. Techically we don't need to save
		/// the values b/c we have the buffer. But I'm happy I figured out this
		/// so I'm going to leave it for now.</remarks>
		/// <param name="iOffset">Offset into the samples.</param>
		/// <param name="d12">Hsync signal from the filter.</param>
		/// <returns></returns>
		public bool LogSync( int iOffset, double d12 ) {
			int iSig = d12 > _SLvl ? 1 : 0;

			try {
				int i2Xl  = _iWindowSizeInSamples * 2;
				int iLast = (_iW + _iWindowSizeInSamples) % ( i2Xl );

				_iWindowSum   -= _rgWindow[iLast];
				_iWindowSum   += iSig;
				_rgWindow[_iW] = iSig;
				_iW            = (_iW + 1) % i2Xl; 

				if( _iWindowSum >= _iWindowHit ) {
					// Only catch on a rising trigger!!
					if( _fTriggered == false ) {
						_fTriggered = true;
						MyDatum oDatum = new ( Bucket:0, Position:iOffset );
						_rgData.Add( oDatum );
						return true;
					}
				} else {
					// When we finally exit the window re-set the trigger.
					_fTriggered = false;
				}
			} catch( Exception oEx ) {
				Type[] rgErrors = { typeof( NullReferenceException ),
									typeof( IndexOutOfRangeException ),
									typeof( ArithmeticException ) };
				if( !rgErrors.Contains( oEx.GetType() ) )
					throw;

				// BUG: need to send an error message out.
			}

			return false;
		}

		/// <summary>
		/// Calculate the scan line start asynchronousely using the sync pulses.
		/// This is useful if you have a signal source that pauses occassionally
		/// but does not advance the data. Thus the only way to repair the damage
		/// is to use the sync pulses to re-align.
		/// Use the AlignLeastSquares method to get a guess at the slope intercept for a
		/// a rough start, then this algorithm crawls the sync pulses to determine the
		/// start for each scan line.</summary>
		/// <param name="dblSlope">Width of the scan line in samples</param>
		/// <param name="iIntercept">Start of the scan lines.</param>
		/// <param name="iMaxScanLine">Stop at this scan line.</param>
		/// <seealso cref="AlignLeastSquares" />
		IEnumerator<int> EnumRasterStart( double dblSlope, double dblIntercept, int iMaxScanLine ) {
			double dblLast = dblIntercept;
			if( _rgRasters[0].Count == 1 ) {
				dblLast = _rgRasters[0].Datum.Position;
			}
			yield return (int)dblLast;

			int iCount = 1;              // count of scan lines since last sync seen.
			for( int i=1; i<iMaxScanLine; ++i ) {
				if( _rgRasters[i].Count == 1 ) {
					dblLast = _rgRasters[i].Datum.Position;
					iCount  = 1;
					yield return (int)dblLast;
				} else {
					yield return (int)( dblLast + iCount++ * dblSlope );
				}
			}
		}

		/// <summary>
		/// Call Shuffle() before calling this function!
		/// This is the beating heart of the slant correction code. Right now we use
		/// the internal raster to compute the scan line width and start point.
		/// This is NOT the same as the rasters used for displaying the data so we
		/// don't corrupt the output drawing process. The two are operating in an
		/// interleaved fashion.
		/// </summary>
		/// <param name="dblSlope">Estimated width in samples of scanline.</param>
		/// <param name="dblIntercept">Estimated END of first sync signal.</param>
		/// <returns>True if enough data to return a slope and intercept.</returns>
		/// <seealso cref="Shuffle(double)"/>
		public bool AlignLeastSquares( ref double dblSlope, ref double dblIntercept, int iMaxRaster = -1 ) {
			double meanx  = 0, meany = 0;
			int    iCount = 0;
			int    iLength = iMaxRaster > 0 ? iMaxRaster : _rgRasters.Length;

			// DO NOT UPDATE Slope and Intercept unless you've calculated the values
			// if they are initialized to zero, the drawing code will have no
			// scan line width to use calculate the scan line starts.
			int iRaster = 1; // BUG: First line is always messed up. Ignore until figure out.
			try {
				for( int i = iRaster; i<iLength; ++i ) {
					if( _rgRasters[i].Count == 1 ) {
						meanx += i;
						meany += _rgRasters[i].Datum.Position;
						++iCount;
					}
				}
				if( iCount < 3 )
					return false;

				meanx /= (double)iCount;
				meany /= (double)iCount;

				double dxsq = 0;
				double dxdy = 0;

				for( int i = iRaster; i < iLength; ++i ) {
					if( _rgRasters[i].Count == 1 ) {
						double dx = (double)i - meanx;
						double dy = (double)_rgRasters[i].Datum.Position - meany;

						dxdy += dx * dy;
						dxsq += Math.Pow( dx, 2 );
					}
				}

				if( dxsq == 0 )
					return false;

				dblSlope     = dxdy / dxsq;
				dblIntercept = meany - dblSlope * meanx;

				if( dblIntercept < 0 )
					dblIntercept += dblSlope;
			} catch( Exception oEx ) {
				Type[] rgErrors = { typeof( ArithmeticException ),
									typeof( NullReferenceException ),
									typeof( ArgumentOutOfRangeException ),
									typeof( IndexOutOfRangeException ) };
				if( !rgErrors.Contains( oEx.GetType() ) )
					throw;

				return false;
			}

			return true;
		}

		/// <summary>
		/// Clear the rasters and reset the bucket on each datm
		/// based on the new scan width parameter. Then reload
		/// the rasters. If it's the first try we just load based on the given
		/// slope. After the first try we use the slope and intercept
		/// to attempt to dump values that seem to off.
		/// </summary>
		/// <param name="fNextTry">true if we have a slope + intercept from a previous try.</param>
		/// <param name="dblIntercept">Previously calculated intercept.</param>
		/// <param name="dblSlope">Previously calculated slope (samples per scan line)</param>
		/// <remarks>300 is just a magic number. Would be nice to make this set (by the user)
		/// in some intellegent manner. I should do some calculations to make a better guess.
		/// We used to attempt this interpolation action in a separate step. Collecting ALL
		/// sync hits and the sifting through them. Might be a waste of time. Looks like keeping
		/// a count of the hit's on the scan line is useful, but pointing to all of them is a waste.
		/// </remarks>
		public void Shuffle( bool fNextTry, double dblSlope, double dblIntercept ) {
			try {
				for( int i=0; i<_rgRasters.Length; ++i ) {
					_rgRasters[i].Datum = null;
					_rgRasters[i].Count = 0;
				}
				foreach( MyDatum oDatum in _rgData ) {
					oDatum.Bucket = (int)( oDatum.Position / dblSlope );
					if( fNextTry ) {
						double dblEstimatedPosition = dblSlope * oDatum.Bucket + dblIntercept;

						if( Math.Abs(oDatum.Position - dblEstimatedPosition ) < 300 )
							RasterAdd( (int)oDatum.Bucket, oDatum );
					} else {
						RasterAdd( (int)oDatum.Bucket, oDatum );
					}
				}
			} catch( Exception oEx ) {
				Type[] rgErrors = { typeof( NullReferenceException ),
									typeof( IndexOutOfRangeException ) };
				if( !rgErrors.Contains( oEx.GetType() ) )
					throw;
			}
		}
	}

	/// <summary>
	/// The demodulator converts the signal from the time to frequency domain. In the original
	/// code. It looks like it lives on it's own thread. I'm going to put it with the demodulator
	/// in the same thread. The actual audio retrival can still live in another thread if we want.
	/// </summary>
    public class SSTVDraw {
        protected readonly SSTVDEM _dp;

		public SSTVMode Mode => _dp.Mode;

		public DateTime StartTime { get; protected set; }

		protected double   _dblReadBaseSync = 0; // Sync signal reader progress
		protected double   _dblReadBaseSgnl = 0; // Image signal reader progress
        protected int      _AY;
	    protected short[]  _Y36 = new short[800];
	    protected short[,] _D36 = new short[2,800];

		protected int      _iLastAlign     = -1;
		protected double   _dblSlope       = 0;
		protected double   _dblIntercept   = 0;
		protected double   _dblMagicOffset = 3.5; // Our iir filters are slow picking up the sync vs Freq detect.
		// BUG: I'll probably need to adjust this number depending on the "FreqDetect" filter (PLL, FQC, Hilbert)

		short[] _pCalibration = null; // Not strictly necessary yet.

		SKCanvas _skD12Canvas;
		SKPaint  _skPaint;

		public SKBitmap _pBitmapRX  { get; } 
		public SKBitmap _pBitmapD12 { get; }
		// Looks like we're only using grey scale on the D12. Look into turning into greyscale later.
		// Need to look into the greyscale calibration height of bitmap issue. (+16 scan lines)
		// The D12 bitmap must always be >= to the RX bmp height.

		// TODO: You know there's only one consumer of these events. I should just make them an 
		// interface or a delegate onto the listener.
		public event Action<SSTVEvents, int> Send_TvEvents;
		public event Action<SSTVMode >       Send_SavePoint;

		protected readonly List<ColorChannel> _rgSlots = new (10);
		
		public double SyncWidthInSamples  { get; protected set; } // These don't get updated like the channels.
		public double SyncOffsetInSamples { get; protected set; } // The channel entries for these do get updated.

		protected SlidingWindow Slider { get; }

		struct DiagnosticPaint {
			public SKColor Color;
			public int     StrokeWidth;

			public DiagnosticPaint( SKColor skColor, int iStrokeWidth ) {
				Color       = skColor;
				StrokeWidth = iStrokeWidth;
			}
		}
		Dictionary<ScanLineChannelType, DiagnosticPaint> _rgDiagnosticColors = new();

		/// <remarks>
		/// Techically we dont need all of the demodulator but only access to the signal
		/// and sync buffer and some signal levels. I'll see about that in the future.
		/// </remarks>
		public SSTVDraw( SSTVDEM p_dp, SKBitmap oD12, SKBitmap oRx ) {
			_dp         = p_dp ?? throw new ArgumentNullException( "Demodulator must not be null to SSTVDraw." );
			_pBitmapD12 = oD12 ?? throw new ArgumentNullException( "D12 bmp must not be null" );
			_pBitmapRX  = oRx  ?? throw new ArgumentNullException( "D12 bmp must not be null" );

			_skD12Canvas = new( _pBitmapD12 );
			_skPaint  = new() { Color = SKColors.Red, StrokeWidth = 1 };

			Slider    = new( 30, p_dp.m_SLvl ); // Put some dummy values for now. Start() updates.

			_rgDiagnosticColors.Add( ScanLineChannelType.Sync,  new( SKColors.White, 2 ) );
			_rgDiagnosticColors.Add( ScanLineChannelType.Gap,   new( SKColors.Brown, 1 ) );
			_rgDiagnosticColors.Add( ScanLineChannelType.Red,   new( SKColors.Red,   1 ) );
			_rgDiagnosticColors.Add( ScanLineChannelType.Green, new( SKColors.Green, 1 ) );
			_rgDiagnosticColors.Add( ScanLineChannelType.Blue,  new( SKColors.Blue,  1 ) );
			_rgDiagnosticColors.Add( ScanLineChannelType.BY,    new( SKColors.Blue,  1 ) );
			_rgDiagnosticColors.Add( ScanLineChannelType.RY,    new( SKColors.Red,   1 ) );
			_rgDiagnosticColors.Add( ScanLineChannelType.Y1,    new( SKColors.Gray,  1 ) );
			_rgDiagnosticColors.Add( ScanLineChannelType.Y2,    new( SKColors.Gray,  1 ) );
			_rgDiagnosticColors.Add( ScanLineChannelType.Y,     new( SKColors.Gray,  1 ) );
		}

		/// <summary>
		/// Still tinkering with disposal methods. We're designed that we can re-use this 
		/// object as long as the _dp object is valid for the current sampling frequency.
		/// if the frequency changes, then the demodulator is no longer valid.
		/// </summary>
		/// <remarks>I read about this pattern for multithreaded objects and checking flags.
		/// I wish I could recall the paper. ^_^;; The idea is that if we get prempted and
		/// the other thread gets dispose called we'll pick up on that when we enter.
		/// BUUUUT, if we dispose the images, the UI thread might choke. So just let 'em
		/// go. As long as we're not generating a bunch of these quickly they'll get
		/// cleaned up eventually anyway.</remarks>
		//public void Dispose() {
		//	if( !_fDisposed ) {
		//		_fDisposed = true;
		//		if( !_fDisposed ) {
		//			_pBitmapD12?.Dispose();
		//			_pBitmapRX ?.Dispose();
		//		}
		//	}
		//}

		/// <summary>this method get's called to initiate the processing of
		/// a new image.</summary>
		/// <seealso cref="OnModeTransition_SSTVDeMo"/>
        public void Start() {
			_iLastAlign      = -1;
			_dblReadBaseSync =  0;
			_dblReadBaseSgnl =  0;
			_AY				 = -5;

			_dblSlope     = SpecWidthInSamples;
			_dblIntercept = 0;

			SyncWidthInSamples  = Mode.WidthSyncInMS * _dp.SampFreq / 1000;
			SyncOffsetInSamples = Mode.OffsetInMS    * _dp.SampFreq / 1000;

			Slider.Reset( (int)SyncWidthInSamples );

			Send_TvEvents?.Invoke(SSTVEvents.SSTVMode, (int)Mode.LegacyMode );

            using SKCanvas sKCanvas = new(_pBitmapRX);
            sKCanvas.Clear(SKColors.Gray);
            _skD12Canvas.Clear();

			StartTime = DateTime.Now;
        }

		/// <summary>
		/// Call this when we've filled the target bitmap. Technically the user could send
		/// forever, we're done. ^_^;
		/// </summary>
		/// <remarks>Right now I only -stop- if i'm in sync'd mode. Might just want to do
		/// this whenever. We'll tinker a bit.</remarks>
		public void Stop() {
			try {
				// Need to send regardless, but might get a bum image if not
				// includes vis and we guess a wrong start state.
				Send_TvEvents?.Invoke( SSTVEvents.DownLoadFinished, PercentRxComplete );
				Send_SavePoint?.Invoke( Mode ); // _dp hasn't been reset yet! Wheeww!

				if( _dp.Synced ) {
					// Send download finished BEFORE reset so we can save image
					// before the SSTVEvents.SSTVMode comes and obliterates the
					// past values (mode/filename/wBase etc).
					_dp.Reset();

					RenderDiagnosticsOverlay();
				}
			} catch( Exception oEx ) {
				Type[] rgErrors = { typeof( NullReferenceException ),
									typeof( KeyNotFoundException ) };
				if( rgErrors.IsUnhandled( oEx ) )
					throw;

				Send_TvEvents?.Invoke( SSTVEvents.ThreadException, (int)TxThreadErrors.DiagnosticsException );
			}
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
		/// This will get called on the UI thread. Because of PercentRxComplete
		/// </summary>
		/// <seealso cref="PercentRxComplete"/>
		public double ImageSizeInSamples {
			get {
				try {
					SSTVMode oMode = Mode;
					if( oMode != null )
						return ScanWidthInSamples * Mode.Resolution.Height / Mode.ScanMultiplier;
					else
						return 0;
				} catch( NullReferenceException ) {
					return 0;
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
		/// Never returns more than 100%. Even if we've got a but and we're
		/// looping forever. So just beware.
		/// </summary>
        public int PercentRxComplete { 
            get {
				try {
					double dblProgress = _dp.m_wBase * 100 / ImageSizeInSamples;
					if( dblProgress > 100 )
						dblProgress = 100;

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

		protected void RenderDiagnosticsOverlay() {
			SKPaint skPaint = new() { Color = SKColors.Yellow, StrokeWidth = 3 };

			double dbD12XScale       = _pBitmapD12.Width / _dblSlope;
			double dblSyncExpected   = SyncOffsetInSamples;
			float  flScaledIntercept = (float)( _dblIntercept * dbD12XScale );
			//double dblOffset       = ( Mode.Resolution.Height - 1 ) * _dblSlope + _dblIntercept;
			//float  flX2            = (float)( dblOffset % _dblSlope * dbD12XScale );

			SKPoint top = new( flScaledIntercept, 0 );
			SKPoint bot = new( flScaledIntercept, _pBitmapD12.Height );
			_skD12Canvas.DrawLine( top, bot, skPaint );

			foreach( ColorChannel oSlot in _rgSlots ) {
				double dblXCh = ( _dblIntercept + oSlot.Min - dblSyncExpected ) * dbD12XScale;
				dblXCh %= _pBitmapD12.Width;

				DiagnosticPaint sPaint = _rgDiagnosticColors[oSlot.ChannelType];

				skPaint.Color       = sPaint.Color;
				skPaint.StrokeWidth = sPaint.StrokeWidth;

				_skD12Canvas.DrawLine( new SKPoint( (int)dblXCh, 0 ), 
									new SKPoint( (int)dblXCh, _pBitmapD12.Height), 
									skPaint );
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
		protected double ProcessSync( double dblBase ) {
			try {
			int    iReadBase   = (int)dblBase;
			int    iScanWidth  = (int)ScanWidthInSamples; // Make sure this matches our controlling loop!!
			double dbD12XScale = _pBitmapD12.Width / ScanWidthInSamples;
			int    iSyncWidth  = (int)( SyncWidthInSamples * dbD12XScale );
			int    iScanLine   = (int)Math.Round( dblBase / ScanWidthInSamples );

			// If we can't advance we'll get stuck in an infinite loop on our caller.
			if( iReadBase + iScanWidth >= _dp.m_wBase )
				throw new InvalidProgramException( "Hit the rails on the ProcessSync" );

			// This can happen when we start in the middle of an image. And we go
			// back to the top of the picture trying to draw the first partial scan line.
			if( _dp.BoundsCompare( iReadBase ) != 0 )
				return( dblBase + ScanWidthInSamples ); // Just skip it.

				for( int i = 0; i < iScanWidth; i++ ) { 
					int   idx = iReadBase + i;
					short d12 = _dp.SyncGet( idx );
					bool fHit = Slider.LogSync( idx, d12 );
					short dRx = _dp.SignalGet( idx );

					int x = (int)( i * dbD12XScale );
					if( (x >= 0) && (x < _pBitmapD12.Width)) {
						int d = Limit256((short)(dRx * 256F / 4096F));
						if( fHit ) {
							_skD12Canvas.DrawLine( new SKPointI( x - iSyncWidth, iScanLine ), new SKPointI( x, iScanLine ), _skPaint);
						} else {
							_skD12Canvas.DrawPoint( x, iScanLine, new SKColor( (byte)d, (byte)d, (byte)d ) );
						}
					}
				}
			} catch( Exception oEx ) {
				Type[] rgErrors = { typeof( ArgumentOutOfRangeException ),
									typeof( NullReferenceException ) };
				if( rgErrors.IsUnhandled( oEx ) )
					throw;

				Send_TvEvents?.Invoke( SSTVEvents.ThreadException, (int)TxThreadErrors.DrawingException );
			}

			return( dblBase + ScanWidthInSamples );
		}

		/// <summary>
		/// This the second new scan line processor. Unlike the ProcessSync() function
		/// this one will be re-started from the beginning after a handful of
		/// sync lines are processed. The idea is that we slowly refine our 
		/// measurement of the image and so need to redraw it. The signal buffer is
		/// currently large enough to allow us to re-draw from the beginning.
		/// I might cut it down in the future. But it makes sense that you really
		/// can't draw the image properly until it has been received in it's entirety.
		/// </summary>
		/// <remarks>So it would be nice to capture a partial scan line, in the case
		/// we just walk into a going signal, but that involves special casing the 
		/// first scan line and right now I don't think it's worth all the effort.
		/// </remarks>
		protected void ProcessScan( int iScanLine ) {
			int rx         = -1; // Saved X pos from the Rx buffer.
			int ch         =  0; // current channel skimming the Rx buffer portion.
			int iScanWidth = (int)Math.Round( ScanWidthInSamples );

			try { 
				// Used to try to track scan line start. This seems better see above.
				int rBase = (int)Math.Round( _dblSlope * iScanLine + _dblIntercept - SyncOffsetInSamples );

				// See remarks: starting a scanline in the middle.
				if( _dp.BoundsCompare( rBase ) != 0 )
					return;
				// Sometimes we bump the rails. Haven't figured it out yet.
				if( _dp.BoundsCompare( rBase + iScanWidth ) != 0 )
					return;

			    _AY = iScanLine * Mode.ScanMultiplier; 
				if( (_AY < 0) || (_AY >= _pBitmapRX.Height) )
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

				Send_TvEvents?.Invoke( SSTVEvents.ThreadException, (int)TxThreadErrors.DrawingException );
			}
		}

		/// <summary>
		/// This is our main processing entry point. The data is being loaded into the buffer
		/// and we read it out here.
		/// </summary>
		public void Process() {
			if( _dp.Synced ) {
				try {
					while( _dp.m_wBase > _dblReadBaseSync + ScanWidthInSamples ) {
						_dblReadBaseSync = ProcessSync( _dblReadBaseSync );

						Send_TvEvents?.Invoke( SSTVEvents.DownLoadTime, PercentRxComplete );
					}
				} catch( Exception oEx ) {
					Type[] rgErrors = { typeof( NullReferenceException ),
										typeof( InvalidProgramException ),
										typeof( IndexOutOfRangeException ) };
					if( rgErrors.IsUnhandled( oEx ) )
						throw;

					Send_TvEvents?.Invoke( SSTVEvents.ThreadException, (int)TxThreadErrors.DrawingException );
				}

				try {
					int iScanLine = (int)(_dp.m_wBase / ScanWidthInSamples );

					if( iScanLine % 20 == 19 ) {
						if( _iLastAlign < iScanLine) {
							Slider.Shuffle( _iLastAlign > 0, _dblSlope, _dblIntercept );

							// If can Align, clear the sync data and force re-read from the buffer
							if( Slider.AlignLeastSquares( ref _dblSlope, ref _dblIntercept ) ) {
								_dblIntercept -= _dblMagicOffset * _dp.SampFreq / 1000;
								InitSlots   ( Mode.Resolution.Width, _dblSlope / SpecWidthInSamples ); // updates the ScanWidthInSamples.
								Slider.Reset( (int)SyncWidthInSamples );

								_dblReadBaseSync = 0;
								_iLastAlign      = iScanLine;

							}
							for( int i = 0; i<iScanLine; ++i ) {
								ProcessScan( i );
							}
						}
					}
					// We should also bail if we're not catching enough sync signals.
					int iScanMax =  Mode.Resolution.Height / Mode.ScanMultiplier;
					if( iScanLine > iScanMax ) {
						for( int i = 0; i<iScanMax; ++i ) {
							ProcessScan( i );
						}
						Stop();
					}
				} catch( Exception oEx ) {
					Type[] rgErrors = { typeof( NullReferenceException ),
										typeof( InvalidProgramException ),
										typeof( IndexOutOfRangeException ) };
					if( rgErrors.IsUnhandled( oEx ) )
						throw;

					Send_TvEvents?.Invoke( SSTVEvents.ThreadException, (int)TxThreadErrors.DrawingException );
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

		protected void PixelSetY( int iX, short sValue ) {
			short iY = (short)( GetPixelLevel(sValue) + 128 );
			_pBitmapRX.SetPixel( iX, _AY+1, new SKColor( (byte)iY, (byte)iY, (byte)iY ) );
		}


		public setPixel ReturnColorFunction( ScanLineChannelType eDT ) {
			switch( eDT ) {
				case ScanLineChannelType.BY    : return PixelSetBY;
				case ScanLineChannelType.RY    : return PixelSetRY;
				case ScanLineChannelType.Y1    : return PixelSetY1;
				case ScanLineChannelType.Y2    : return PixelSetY2;
				case ScanLineChannelType.Blue  : return PixelSetBlue;
				case ScanLineChannelType.Red   : return PixelSetRed;
				case ScanLineChannelType.Green : return PixelSetGreen;
				case ScanLineChannelType.Y     : return PixelSetY;
				default : return null;
			}
		}

		/// <summary>
		/// Catch the mode change event from the SSTVDemodulator.
		/// </summary>
		/// <seealso cref="Start" />
		public void OnModeTransition_SSTVDeMo( SSTVMode oCurrMode, SSTVMode oPrevMode, int iPrevBase ) {
			if( oCurrMode == null ) {
				RenderDiagnosticsOverlay();
				Send_TvEvents?.Invoke( SSTVEvents.SSTVMode, -1 );
				return;
			}

			// Technically we shouldn't get a transition while in a given SSTVMode.
			// if the demodulator has the mode, we'll save the image (fragment)
            if( ImageSizeInSamples != 0 ) {
			    double dblProgress = iPrevBase * 100 / ImageSizeInSamples;

                if( dblProgress > 25 )
                    Send_SavePoint?.Invoke( oPrevMode );
            }

			try {
                _rgSlots.Clear();

                foreach( ScanLineChannel oChannel in oCurrMode.ChannelMap ) {
                	_rgSlots.Add( new( oChannel.WidthInMs * _dp.SampFreq / 1000, ReturnColorFunction( oChannel.Type ), oChannel.Type ) );
                }
                _rgSlots.Add( new() );

                InitSlots( oCurrMode.Resolution.Width, 1 );
            } catch( Exception oEx ) {
				Type[] rgErrors = { typeof( NullReferenceException ),
									typeof( ArgumentOutOfRangeException ),
									typeof( ArgumentNullException ),
									typeof( ArgumentException ) };
				if( rgErrors.IsUnhandled( oEx ) )
					throw;

				// Uh oh.
			}

			Start(); 
		}

		/// <summary>
		/// This goes back and finishes out the slots so their Min and Max
		/// boundaries are assigned. Also set's the scale factor to align
		/// the bitmap width with the slot width (color channel width).
		/// </summary>
		/// <seealso cref="DemodTest.Write(int, uint, double)"/>
		protected void InitSlots( int iBmpWidth, double dbCorrection ) {
			double dbIdx = 0;
			foreach( ColorChannel oSlot in _rgSlots ) {
				dbIdx = oSlot.Reset( iBmpWidth, dbIdx, dbCorrection );
			}

			double dblSamplesPerMs = _dp.SampFreq / 1000 * dbCorrection;

			SyncWidthInSamples  = ( Mode.WidthSyncInMS * dblSamplesPerMs );
			SyncOffsetInSamples = ( Mode.OffsetInMS    * dblSamplesPerMs );
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

		public ScanLineChannelType ChannelType { get; }

		public double   Min      { get; set; }
		public double   Max      { get; protected set; }
		public setPixel SetPixel { get; protected set; }
		public double   Scaling  { get; protected set; }

		public ColorChannel( double dbWidthInSamples, setPixel fnSet, ScanLineChannelType eType ) {
			SpecWidthInSamples = dbWidthInSamples;
			SetPixel           = fnSet;
			ChannelType        = eType;
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
