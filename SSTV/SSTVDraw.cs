using System;
using System.Collections.Generic;
using System.Collections;
using System.Threading.Tasks;

using SkiaSharp;

using Play.Interfaces.Embedding;
using Play.Sound;

namespace Play.SSTV {
	public struct SSTVPosition {
		public double Position { get; init; }
		public int    ScanLine { get; init; }
	}

	/// <summary>
	/// The demodulator converts the signal from the time to frequency domain. In the original
	/// code. It looks like it lives on it's own thread. I'm going to put it with the demodulator
	/// in the same thread. The actual audio retrival can still live in another thread if we want.
	/// </summary>
    public class SSTVDraw : 
		IEnumerable<SSTVPosition>
	{
		/// <summary>
		/// So this is super cool. I can make an enumerator from this object but 
		/// there is no need to use the heap to point to this object!!
		/// Now I can pass all or some of the collection to the Scan Line processor.
		/// </summary>
		protected struct ScanLineEnumerable : IEnumerable<SSTVPosition>{
			int      _iStart, _iEnd;
			SSTVDraw _oDraw;

			public ScanLineEnumerable( SSTVDraw oDraw, int iStart, int iEnd) {
				_oDraw  = oDraw ?? throw new ArgumentNullException(nameof (oDraw));
				_iStart = iStart;
				_iEnd   = iEnd;
			}

			public IEnumerator<SSTVPosition> GetEnumerator() {
				return _oDraw.GetEnumerator(_iStart, _iEnd);
			}

			IEnumerator IEnumerable.GetEnumerator() {
				return GetEnumerator();
			}
		}
		/// <summary>
		/// This will allow us to ProcessScanLine() parallel processed. But with my
		/// 6 core machine performance bogs at anything higher than 3 tasks.
		/// That number should probably be something that can be controlled by the user.
		/// </summary>
		/// <seealso cref="ProcessScanLine(ScanBuffers, double, int)"/>
		class ScanBuffers {
			protected int      _AY;
			protected short[]  _Y36 = new short[800];
			protected short[]  _CRy = new short[800]; // D36[1,iX]
			protected short[]  _CBy = new short[800]; // D36[0,iX]

			public setPixel[] Writers { get; }

			public ScanBuffers( SSTVDraw oHost ) {
				if( oHost == null )
					throw new ArgumentNullException( nameof( oHost ) );
				if( oHost._pBitmapRX == null )
					throw new InvalidProgramException( "SSTV Target bitmap is null!" );

				_pBitmapRX = oHost._pBitmapRX;

				var rgWriterEnum = typeof( ScanLineChannelType ).GetEnumValues();

				Writers = new setPixel[rgWriterEnum.Length];

				foreach( ScanLineChannelType eType in rgWriterEnum ) {
					Writers[(int)eType ] = ReturnColorFunction( eType );
				}
			}

			protected readonly SKBitmap _pBitmapRX;

			public bool Reset( int iRasterLine ) {
			    _AY = iRasterLine;

				if( (_AY < 0) || (_AY >= _pBitmapRX.Height) )
					return false;

				return true;
			}
			protected void PixelSetGreen( int iX, short sValue ) {
				_CBy[iX] = Limit256(sValue + 128 );
			}

			protected void PixelSetBlue( int iX, short sValue ) {
				_CRy[iX] = Limit256(sValue + 128 );
			}

			/// <summary>
			/// Cache the Green and Blue values first and finish with this call.
			/// </summary>
			protected void PixelSetRed( int iX, short sValue ) {
				_pBitmapRX.SetPixel( iX, _AY,  new SKColor( (byte)Limit256(sValue + 128 ), 
															(byte)_CBy[iX], 
															(byte)_CRy[iX] ) );
			}

			/// <summary>
			/// Notice the value doesn't have Limit256() called on it. That will get
			/// called when the YRyBy value get's converted to RGB in YCtoRGB()
			/// </summary>
			/// <seealso cref="YCtoRGB"/>
			protected void PixelSetY1( int iX, short sValue ) {
				_Y36[iX] = (short)(sValue + 128);
			}

			protected void PixelSetRY( int iX, short sValue ) {
				_CRy[iX] = sValue;
			}

			protected void PixelSetBY( int iX, short sValue ) {
				_CBy[iX] = sValue;
			}

			/// <summary>
			/// Cache the RY and BY values first and finish with this call.
			/// </summary>
			protected void PixelSetY2( int iX, short sValue ) {
				short R, G, B;

				YCtoRGB( out R, out G, out B, _Y36[iX], _CRy[iX], _CBy[iX]);
				_pBitmapRX.SetPixel( iX, _AY,   new SKColor( (byte)R, (byte)G, (byte)B ) );

				YCtoRGB( out R, out G, out B, sValue + 128,   _CRy[iX], _CBy[iX]);
				_pBitmapRX.SetPixel( iX, _AY+1, new SKColor( (byte)R, (byte)G, (byte)B ) );
			}

			protected void PixelSetY( int iX, short sValue ) {
				sValue += 128;
				_pBitmapRX.SetPixel( iX, _AY, new SKColor( (byte)sValue, (byte)sValue, (byte)sValue ) );
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

		}

        readonly SSTVDEM _dp;

		public SSTVMode Mode => _dp.Mode;
		public DateTime StartTime { get; protected set; }
		// This let's us offset our start point if the sync is NOT the first bit of scanline data. ie Scotty.
		public double   SyncOffsetInSamples { get; protected set; } // The channel entries for these do get updated.

		readonly List<ColorChannel> _rgSlots        = new(10); // see also scan line buffers.
        readonly List<ScanBuffers>  _rgBuffers      = new();
		readonly List<Task>         _rgTasks        = new();
		const    int                _iBucketSize    = 20;
		readonly List<double>       _rgSlopeBuckets = new();

		bool     _fAuto          = false;
		double   _dblSlope       = 0;
		double   _dblIntercept   = 0;

		short[]  _pCalibration = null; // Not strictly necessary yet.
		SKCanvas _skD12Canvas;

		public SKBitmap _pBitmapRX  { get; } 
		public SKBitmap _pBitmapD12 { get; }
		// Looks like we're only using grey scale on the D12. Look into turning into greyscale later.
		// Need to look into the greyscale calibration height of bitmap issue. (+16 scan lines)
		// The D12 bitmap must always be >= to the RX bmp height.

		// There's only one consumer of these events so these are just a delegate
		// onto the listener.
		public Action<SSTVEvents, int> Send_TvEvents;
		public Action<SSTVMode >       Send_SavePoint;

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
		/// Also note, 8/14/2012: We're sending the D12 and Rx bitmaps in here, we write to them.
		/// But the documents they live in don't know they've updated and so won't send
		/// messages to any listening views!! Grrr.. How best to fix this? I'm going to make
		/// the Raise_ImageUpdated() event public. We raise the SSTVEvents here and the
		/// receiver of those events will call the image document Raise event.
		/// </remarks>
		public SSTVDraw( SSTVDEM p_dp, SKBitmap oD12, SKBitmap oRx ) {
			_dp         = p_dp ?? throw new ArgumentNullException( "Demodulator must not be null to SSTVDraw." );
			_pBitmapD12 = oD12 ?? throw new ArgumentNullException( "D12 bmp must not be null" );
			_pBitmapRX  = oRx  ?? throw new ArgumentNullException( "D12 bmp must not be null" );

			_skD12Canvas = new( _pBitmapD12 );

            for( int i = 0; i < 3; ++i ) {
                _rgBuffers.Add(new ScanBuffers(this));
            }

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
			_rgDiagnosticColors.Add( ScanLineChannelType.END,   new( SKColors.Aquamarine, 3 ) );
		}

		/// <summary>this method get's called to initiate the processing of
		/// a new image.</summary>
		/// <seealso cref="OnModeTransition_SSTVDeMo"/>
		/// <seealso cref="InitSlots"/>
        public void Start() {
			try {
				_fAuto        = true;
				_dblSlope     = SpecWidthInSamples;
				_dblIntercept = 0;

				_rgSlopeBuckets.Clear();

				// BUG: This might need futzing based on slope correction. ie if the
				//      sender is miscalibrated! See InitSlots()
				SyncOffsetInSamples = Mode.OffsetInMS * _dp.SampFreq / 1000;

				// If the delegate is null, no way to send the error up!!
				Send_TvEvents( SSTVEvents.ModeChanged, (int)Mode.LegacyMode );
				Send_TvEvents( SSTVEvents.DownLoadTime, 0 );

				// This is a little dangerous. But since the main thread never messes with the
				// bitmap we'll probably be ok.
				using SKCanvas sKCanvas = new(_pBitmapRX);
				sKCanvas.Clear(SKColors.Gray);
				_skD12Canvas.Clear();

				StartTime = DateTime.Now;
			} catch( NullReferenceException ) {
				Send_TvEvents?.Invoke( SSTVEvents.ThreadException, (int)TxThreadErrors.StartException );
			}
        }

		/// <summary>
		/// Call this when we've filled the target bitmap. Technically the user could send
		/// forever, but we're done once we fill our bitmap. ^_^;
		/// </summary>
		/// <remarks>Right now I only -stop- if i'm in sync'd mode. Might just want to do
		/// this whenever. We'll tinker a bit.</remarks>
		public void Stop() {
			try {
				// Need to send regardless, but might get a bum image if not
				// includes vis and we guess a wrong start state.
				Send_TvEvents ( SSTVEvents.DownLoadFinished, PercentRxComplete );
				Send_SavePoint( Mode ); // _dp hasn't been reset yet! Wheeww!

				if( _dp.Synced ) {
					// Send download finished BEFORE reset so we can save image
					// before the SSTVEvents.SSTVMode comes and obliterates the
					// past values (mode/filename/wBase etc).
					_dp.Reset();
				}
			} catch( Exception oEx ) {
				Type[] rgErrors = { typeof( NullReferenceException ),
									typeof( KeyNotFoundException ) };
				if( rgErrors.IsUnhandled( oEx ) )
					throw;

				Send_TvEvents?.Invoke( SSTVEvents.ThreadException, (int)TxThreadErrors.StopException );
			}
		}

		/// <summary>
		/// Possibly corrected scan line width in samples.
		/// </summary>
		/// <seealso cref="SpecWidthInSamples"/>
		public double ScanWidthInSamples {
			get {
				try {
					return _rgSlots[_rgSlots.Count - 1].Min;
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
		/// <seealso cref="PercentRxComplete"/>
		public double ImageSizeInSamples {
			get {
				try {
					SSTVMode oMode = Mode;
					if( oMode != null )
						return ScanWidthInSamples * Mode.Resolution.Height / (double)Mode.ScanMultiplier;
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

		/// <summary>
		/// BUG: 3/25/2022, This is probably broken with the new slant correction implementation.
		/// </summary>
		protected void DiagnosticsOverlay() {
			SKPaint skPaint = new() { Color = SKColors.Yellow, StrokeWidth = 3 };

			double dbD12XScale       = _pBitmapD12.Width / _dblSlope;
			double dblStartIndex     = StartIndex;
			float  flScaledIntercept = (float)( dblStartIndex * dbD12XScale );
			//double dblOffset       = ( Mode.Resolution.Height - 1 ) * _dblSlope + _dblIntercept;
			//float  flX2            = (float)( dblOffset % _dblSlope * dbD12XScale );

			SKPoint top = new( flScaledIntercept, 0 );
			SKPoint bot = new( flScaledIntercept, _pBitmapD12.Height );
			_skD12Canvas.DrawLine( top, bot, skPaint );

			float[] rgIntervals = { 2f, 5f };
			using SKPathEffect skDash = SKPathEffect.CreateDash( rgIntervals, 0 );

			foreach( ColorChannel oSlot in _rgSlots ) {
				double dblXCh = ( dblStartIndex + oSlot.Min ) * dbD12XScale;
				dblXCh %= _pBitmapD12.Width;

				DiagnosticPaint sPaint = _rgDiagnosticColors[oSlot.ChannelType];

				skPaint.Color       = sPaint.Color;
				skPaint.StrokeWidth = sPaint.StrokeWidth;

				if( oSlot.ChannelType == ScanLineChannelType.Gap ||
					oSlot.ChannelType == ScanLineChannelType.END ) {
					skPaint.PathEffect = skDash;
				} else {
					skPaint.PathEffect = null;
				}

				_skD12Canvas.DrawLine( new SKPoint( (int)dblXCh, 0 ), 
									   new SKPoint( (int)dblXCh, _pBitmapD12.Height), 
									   skPaint );
			}
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
		void ProcessScanLine( ScanBuffers oBuff, double dblBase, int iScanLine ) {
			int    rx          = -1; // Saved X pos from the Rx buffer.
			int    ch          =  0; // current channel skimming the Rx buffer portion.
			int    iScanWidth  = (int)Math.Round( ScanWidthInSamples );
			double dbD12XScale = _pBitmapD12.Width / ScanWidthInSamples;

			try { 
				// Used to try to track scan line start. This seems better see above.
				int rBase = (int)Math.Round( dblBase );

				// See remarks: starting a scanline in the middle.
				if( _dp.BoundsCompare( rBase ) != 0 )
					return;
				// Sometimes we bump the rails. Haven't figured it out yet.
				if( _dp.BoundsCompare( rBase + iScanWidth ) != 0 )
					return;
				// Convert from scan line to bitmap offset.
				if( !oBuff.Reset( iScanLine * Mode.ScanMultiplier ) )
					return;

				for( int i = 0; i < iScanWidth; i++ ) { 
					int   iSx = rBase + i;                // Offset into the data @ _dp
					int iD12x = (int)( i * dbD12XScale ); // Offset onto the D12 (sync) bitmamp

					if( iD12x < _pBitmapD12.Width) {
						int d = Limit256((short)(_dp.SyncGet( iSx ) * 256F / 4096F));
						_skD12Canvas.DrawPoint( iD12x, iScanLine, new SKColor( (byte)d, (byte)d, (byte)d ) );
					}

					do {
						ColorChannel oChannel = _rgSlots[ch];
						setPixel     oWriter  = oBuff.Writers[(int)oChannel.ChannelType];
						if( i < oChannel.Max ) {
							if( oWriter != null ) {
								int x = (int)((i - oChannel.Min) * oChannel.Scaling );
								if( (x != rx) && (x >= 0) && (x < _pBitmapRX.Width) ) {
									rx = x; oWriter( x, (short)(GetPixelLevel( _dp.SignalGet( iSx ) ) ) );
								}
							} // else null and just do-nothing/skip this data value.
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

		public class Adjuster : ISstvAdjust {
					 double m_SampFreq;
			readonly double m_dblScanWidthInMS;

			public double TW { get; protected set; } // Width of scan line in samples.
			public int    WD { get; protected set; } // int version of TW.

			public Adjuster( double dblSampFreq, double dblScanLineInMS ) {
				m_dblScanWidthInMS = dblScanLineInMS;

				SetSampFreq( dblSampFreq );
			}

			/// <summary>
			/// So in the original code you can update the sample frequency,
			/// and that of course will affect the width of a scan line (in samples).
			/// So we need a function to update both.
			/// </summary>
			/// <param name="dblSampFreq"></param>
			public void SetSampFreq( double dblSampFreq ) {
				const double dblMsPerSecond = 1000;

				m_SampFreq = dblSampFreq;
				TW         = m_SampFreq / dblMsPerSecond * m_dblScanWidthInMS;
				WD         = (int)Math.Round( TW );

				//std::cout << "freq: " << m_SampFreq << " wd" << m_TW << "\n";
			}

			public double SampFreq { get { return m_SampFreq; } }
		}

		/// <summary>
		/// This is our main processing entry point. The data is being loaded into the buffer
		/// and we read it out here. _dp.Mode might be null after exit of this call.
		/// </summary>
		public void Process() {
			if( _dp.Synced ) {
				try {
                    // BUG: this s/b encoded scan line and not the bitmap y value.
                    int iScanLine = (int)( ( _dp.m_wBase - StartIndex ) / ScanWidthInSamples );

					Send_TvEvents?.Invoke( SSTVEvents.DownLoadTime, PercentRxComplete );
					if( iScanLine >= ( _rgSlopeBuckets.Count + 1 ) * _iBucketSize ) {

						int iStartLine = _rgSlopeBuckets.Count == 0 ? 0 : iScanLine - _iBucketSize - 1;
						if( _fAuto ) {
							Adjuster  oAdjust = new Adjuster( _dp.SampFreq, Mode.ScanWidthInMS );
							SSTVSlant oSlant  = new SSTVSlant( _dp, oAdjust );

							int n = oSlant.CorrectSlant();

							// BUG: A little bit of hackery.
							n -= (int)_dp.OffsetCorrect( oAdjust.SampFreq );

                            _dblIntercept = n;
							_dblSlope     = oAdjust.TW;
                        }
						_rgSlopeBuckets.Add( _dblSlope );

						ProcessCollection( new ScanLineEnumerable( this, iStartLine, iScanLine ) );
					}
					// Bail on current image when we've processed expected image size.
					if( _dp.m_wBase > ImageSizeInSamples ) {
						ProcessAll();
						Stop();
					}
				} catch( Exception oEx ) {
					Type[] rgErrors = { typeof( NullReferenceException ),
										typeof( InvalidProgramException ),
										typeof( IndexOutOfRangeException ),
										typeof( ArgumentOutOfRangeException ) };
					if( rgErrors.IsUnhandled( oEx ) )
						throw;

					Send_TvEvents?.Invoke( SSTVEvents.ThreadException, (int)TxThreadErrors.DrawingException );
				}
			}
		}

		public void ManualSlopeAdjust( double dblDir ) {
			_fAuto     = false;
			_dblSlope += dblDir;

			ProcessAll();
		}

		/// <summary>
		/// This will re-read/render the entire buffer.
		/// </summary>
		public void ProcessAll() {
            //ScanBuffers oBuffer = new ScanBuffers( this );

            //foreach( SSTVPosition sSample in this ) {
            //    ProcessScan( oBuffer, sSample.Position, sSample.ScanLine);
            //}

			ProcessCollection( this );

            if( _dp.Synced ) {
				DiagnosticsOverlay();
			}
			Send_TvEvents?.Invoke( SSTVEvents.DownLoadTime, 100 );
		}

		/// <summary>
		/// OMG I can use the same processor for all or part of the scanlines! 
		/// </summary>
		/// <remarks>I've seen entries that StartNew() is a heavy duty thing and
		/// Task.Run() is the preferable way to go. I can probably fix that easily.
		/// </remarks>
		/// <seealso cref="ScanLineEnumerable"/>
		protected void ProcessCollection( IEnumerable<SSTVPosition> oClxn ) {
			_rgTasks.Clear();
			foreach( SSTVPosition oIndex in oClxn ) {
                // Make copies outside of the delegate so iterator doesn't change while in thread!!!
                double dblPosition  = oIndex.Position;
                int    iScanline    = oIndex.ScanLine;
                int    iBufferIndex = _rgTasks.Count;

                _rgTasks.Add(Task.Factory.StartNew(() => {
                    ProcessScanLine(_rgBuffers[iBufferIndex], dblPosition, iScanline);
                }));

				if( _rgTasks.Count >= 3 ) {
					Task.WaitAll(_rgTasks.ToArray());
					_rgTasks.Clear();
				}
			}
			Task.WaitAll(_rgTasks.ToArray());
			_rgTasks.Clear();
		}

		/// <summary>
		/// New approach in enumerating the scan lines. This will allow me to have
		/// variable scan line widths over the image. And at least it encapsulates the
		/// scan line traversal here.
		/// So we want to use the slope to calc the next position up to the ScanStart.
		/// where upon we go to the next bucket.
		/// </summary>
		public IEnumerator< SSTVPosition > foo () {
            double dblIndex  = _dblIntercept - SyncOffsetInSamples;
			int    iScanLine = 0;

			foreach( double dblSlope in _rgSlopeBuckets ) {
				InitSlots( Mode.Resolution.Width, dblSlope / SpecWidthInSamples ); 
				for( int i = 0; i<_iBucketSize; ++i ) {
					yield return new SSTVPosition() { Position=dblIndex, ScanLine=iScanLine };
					dblIndex  += dblSlope;
					iScanLine += 1;
				}
			}
		}

		/// <summary>
		/// This is the starting position for the unprocessed scan data that makes up the image. 
		/// </summary>
		public double StartIndex => _dblIntercept - SyncOffsetInSamples;

		/// <summary>
		/// This is the old implementation. It enumerates all the scan lines using a single
		/// slope value.
		/// </summary>
		public IEnumerator< SSTVPosition > GetEnumerator () {
            double dblIndex  = StartIndex;
			int    iScanLine = 0;

			if( Mode == null )
				yield break;

			InitSlots( Mode.Resolution.Width, _dblSlope / SpecWidthInSamples ); 

			while( dblIndex < ImageSizeInSamples ) {
				yield return new SSTVPosition() { Position=dblIndex, ScanLine=iScanLine };
				dblIndex  += _dblSlope;
				iScanLine += 1;
			}
		}

		/// <summary>
		/// This is the non non-linear scan line enumerator. This is not using the frequency
		/// drift buckets system I'm working on for the websdr's. These are scan lines and not
		/// raster lines so just be aware! 
		/// </summary>
		/// <param name="iStart">Starting scan line</param>
		/// <param name="iEnd">End scan line inclusive.</param>
		/// <returns></returns>
		public IEnumerator< SSTVPosition> GetEnumerator( int iStart, int iEnd ) {
			if( Mode == null )
				yield break;

			InitSlots( Mode.Resolution.Width, _dblSlope / SpecWidthInSamples ); 

			double dblPosition = _dblSlope * iStart + StartIndex;

			for( int iScanLine = iStart; 
				 iScanLine <= iEnd && dblPosition < ImageSizeInSamples; 
				 ++iScanLine, dblPosition += _dblSlope) 
			{
				 yield return new SSTVPosition() { Position=dblPosition, ScanLine=iScanLine };
			}
		}

        IEnumerator IEnumerable.GetEnumerator() {
			return GetEnumerator();
        }

		/// <summary>
		/// Catch the mode change event from the SSTVDemodulator.
		/// </summary>
		/// <seealso cref="Start" />
		public void OnModeTransition_SSTVDeMo( SSTVMode oCurrMode, SSTVMode oPrevMode, int iPrevBase ) {
			if( oCurrMode == null ) {
				DiagnosticsOverlay();
				Send_TvEvents?.Invoke( SSTVEvents.ModeChanged, -1 );
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
                	_rgSlots.Add( new( oChannel.WidthInMs * _dp.SampFreq / 1000, oChannel.Type ) );
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
		/// <seealso cref="Start"/>
		protected void InitSlots( int iBmpWidth, double dbCorrection ) {
			double dbIdx = 0;
			foreach( ColorChannel oSlot in _rgSlots ) {
				dbIdx = oSlot.Reset( iBmpWidth, dbIdx, dbCorrection );
			}

			double dblSamplesPerMs = _dp.SampFreq / 1000 * dbCorrection;

			SyncOffsetInSamples = Mode.OffsetInMS * dblSamplesPerMs;
		}

    } // End Class TmmSSTV

    public delegate void setPixel( int iX, short sLevel );

	/// <summary>
	/// This object represents one portion of the video scan line. String these
	/// together to create a complete scan line parse.
	/// </summary>
	/// <remarks>We could simplify the ProcessScan() call by ensuring that
	/// the SetPixel delegate is non-null but then we'll end up wasting time
	/// calculating and measuring the x position for no reason during horizontal
	/// scan values on the scan line.</remarks>
	public class ColorChannel {
		public double SpecWidthInSamples { get; } // The original specification.
		public double _dbScanWidthCorrected;      // Compensated value.

		public ScanLineChannelType ChannelType { get; }

		public double   Min      { get; protected set; }
		public double   Max      { get; protected set; }
		public double   Scaling  { get; protected set; }

		public ColorChannel( double dbWidthInSamples, ScanLineChannelType eType ) {
			SpecWidthInSamples = dbWidthInSamples;
			ChannelType        = eType;
		}

		public ColorChannel() {
			SpecWidthInSamples = double.MaxValue;
			ChannelType        = ScanLineChannelType.END;
		}

		public double Reset( int iBmpWidth, double dbStart, double dbCorrection ) {
			_dbScanWidthCorrected = SpecWidthInSamples * dbCorrection;

			Scaling = iBmpWidth / _dbScanWidthCorrected;

			Min = dbStart;
			Max = dbStart + ( _dbScanWidthCorrected );

			return Max;
		}

        public override string ToString() {
            return Max.ToString() + " " + ChannelType.ToString();
        }
    }

}
