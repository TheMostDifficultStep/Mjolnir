﻿using System;
using System.Collections.Generic;

using SkiaSharp;

using Play.Interfaces.Embedding;
using Play.Sound;

namespace Play.SSTV
{
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

#region variables
        protected int     m_AX, m_AY;

	    protected short[]  m_Y36 = new short[800];
	    protected short[,] m_D36 = new short[2,800];

	    protected int     m_SyncPos, m_SyncRPos; // RPos Also gets set in AutoStopJob() which we haven't implemented yet 
	    protected int     m_SyncMax, m_SyncMin;
		protected int     m_SyncHit, m_SyncLast;
		readonly protected List<SKPointI> _rgSyncDetect = new List<SKPointI>(256);

	    protected int     m_SyncAccuracy = 1;
	    protected int     m_SyncAccuracyN;  // TODO: Gets used in TimerTimer, which isn't implemented yet.

        //Auto
	    protected int     m_Mult;

	    protected int     m_AutoStopPos;
	    protected int[]   m_AutoStopAPos = new int[16];
	    protected int     m_AutoStopCnt;
	    protected int     m_AutoStopACnt;

	    protected int     m_AutoSyncCount;
	    protected int     m_AutoSyncPos;
	    protected int     m_AutoSyncDis;
	    protected int     m_AutoSyncDiff;

        //AutoSlant
	    int       m_ASBgnPos;
	    int       m_ASCurY;
	    int       m_ASDis;
	    int       m_ASBitMask;
	    double[]  m_ASLmt = new double[7];
	    int[]     m_ASPos = new int[4];
	    CSmooz    m_ASAvg = new CSmooz();

		short[] _pCalibration = null; // Not strictly necessary yet.

		public SKBitmap _pBitmapRX  { get; protected set; } 
		public SKBitmap _pBitmapD12 { get; } = new SKBitmap( 800, 616, SKColorType.Rgb888x, SKAlphaType.Unknown );
		// Looks like were only using grey scale on the D12. Look into turning into greyscale later.
		// Need to look into the greyscale calibration height of bitmap issue. (+16 scan lines)
		public int      ScanLine => m_AY;
#endregion

		public event SSTVPropertyChange ShoutTvEvents; // TODO: Since threaded version poles, we don't need this.

		protected readonly List<ColorChannel> _rgSlots = new List<ColorChannel>(10);

		public TmmSSTV( CSSTVDEM p_dp ) {
			_dp = p_dp ?? throw new ArgumentNullException( "CSSTVDEM" );

		  //StartOption() ...
		  //dp.sys.m_bCQ100 = FALSE;
		  //g_dblToneOffset = 0.0;
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

		/// <summary>
		/// Return the scan line width in samples, possibly corrected. Samples per Millisecond based.
		/// </summary>
		public double ScanWidthInSamples {
			get {
				if( _rgSlots.Count < 1 )
					return 0;

				return _rgSlots[_rgSlots.Count - 1 ].Min;
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

		protected int GetPictureLevel( short ip, int i ) {
			//if( dp.sys.m_UseRxBuff != 2 ) {
			//	int   d;
			//	short ip2 = dp.m_Buf[dp.m_rPage * dp.m_BWidth + i + SSTVSET.m_KSB ];

			//	// I'm going to guess we're trying to get past some sync signal.
			//	if( ip < ip2 ){
			//		d = GetPixelLevel(ip2);
			//	} else {
			//		d = GetPixelLevel(ip);
			//	}
			//	return d;
			//}

			return GetPixelLevel(ip);
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
				if( _pBitmapRX != null ) {
					return ( m_AY * 100 / _pBitmapRX.Height ) ;
				}
                return 0;
            }
        }

		protected int LineMultiplier { get; set; }

		/// <summary>
		/// Note: if we retune the slot timing, we'll need to call this again.
		/// </summary>
        void InitAutoStop( double dbSampBase )
        {
            Array.Clear( m_AutoStopAPos, 0, m_AutoStopAPos.Length );

	        m_AutoStopCnt  = 0;
	        m_AutoStopACnt = 0;
	        m_AutoStopPos  = 0;
	        m_ASBgnPos     = 0x7fffffff;
	        m_ASDis        = 0;
	        m_ASBitMask    = 0;

	        m_ASAvg.SetCount(16);

	        m_ASPos[0] = 64;
	        m_ASPos[1] = 128;
	        m_ASPos[2] = 160;
	        m_ASPos[3] = (_dp.Mode.Resolution.Height - 36);

	        switch(_dp.Mode.LegacyMode){
		        case AllModes.smPD50:
		        case AllModes.smPD90:            // Max 128
		        case AllModes.smMP73:
		        case AllModes.smMP115:
		        case AllModes.smMP140:
		        case AllModes.smMP175:
		        case AllModes.smR24:
		        case AllModes.smRM8:
		        case AllModes.smRM12:
                case AllModes.smMN73:
                case AllModes.smMN110:
                case AllModes.smMN140:
			        m_ASPos[0] = 48;
			        m_ASPos[1] = 64;
			        m_ASPos[2] = 72;
			        m_ASPos[3] = 110;
			        break;
		        case AllModes.smPD160:           // Max 200
			        m_ASPos[0] = 48;
			        m_ASPos[1] = 80;
			        m_ASPos[2] = 126;
			        m_ASPos[3] = 160;
			        break;
		        case AllModes.smPD290:           // Max 308 Limit 288
			        m_ASPos[3] = 240;
			        break;
		        case AllModes.smP3:              // Max496
			        m_ASPos[1] = 200;
			        m_ASPos[2] = 360;
			        m_ASPos[3] = 496-48;
			        break;
		        case AllModes.smP5:              // Max496 Limit 439
			        m_ASPos[1] = 200;
			        m_ASPos[2] = 300;
			        m_ASPos[3] = 380;
			        break;
		        case AllModes.smP7:              // Max496 Limit 330
			        m_ASPos[1] = 128;
			        m_ASPos[2] = 220;
			        m_ASPos[3] = 280;
			        break;
	        }
	        m_AutoSyncPos  = 0x7fffffff;
	        m_Mult         = (int)(ScanWidthInSamples / 320.0);
	        m_AutoSyncDiff = m_Mult * 3;

			// I would love to know why this requires SampBase!! instead of SampFreq!!
            int iSomeMagic = (int)(45 * dbSampBase / 11025);
	        if( m_AutoSyncDiff > iSomeMagic )
                m_AutoSyncDiff = iSomeMagic;
        }

		/// <summary>
		/// This is a whole hot mess of stuff and I'm not going to port it for now.
		/// </summary>
		/// <returns></returns>
		protected bool AutoStopJob() {
			return true;
		}

        public void Start() {
			m_SyncRPos      = m_SyncPos = -1;
			m_SyncAccuracyN = 0;
			m_AX			= -1;
			m_AY			= -5;
			m_SyncHit       = -1;
			m_SyncLast		= 0;
			_iSyncCheck     = 4;

			_rgSyncDetect.Clear();

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

			// TODO: I'll probably need to call these if re-draw for slant correction.
			InitAutoStop( _dp.SampBase );
			m_AutoSyncCount = m_AutoSyncDis = 0;
        }

		/// <summary>
		/// Call this function periodically to collect the sstv scan lines.
		/// </summary>
		/// <remarks>This function will loop until the rPage catches up with the wPage. 
		/// </remarks>
		public void SSTVDraw() {
			while( _dp.m_Sync && (_dp.m_wBase >=_dp.m_rBase + ScanWidthInSamples + _iSyncOffset ) ){
                //dp.StorePage();

				if( (_dp.sys.m_AutoStop || _dp.sys.m_AutoSync ) && _dp.m_Sync && (m_SyncPos != -1) ) {
					AutoStopJob();
				}
				SSTVDrawNormal();
				_dp.PageRIncrement( ScanWidthInSamples );

				SSTVSync();

				if( m_AY >= _dp.Mode.Resolution.Height ){ 
					_dp.Stop();
					ShoutTvEvents?.Invoke( ESstvProperty.DownLoadFinished );
					break;
				} else {
					ShoutTvEvents?.Invoke( ESstvProperty.DownLoadTime );
				}
			}
		}

		protected void SSTVDrawNormal() {
			int    dx          = -1;          // Saved X pos from the B12 buffer.
			int    rx          = -1;          // Saved X pos from the Rx  buffer.
			int    ch          = 0;           // current channel skimming the Rx buffer portion.
			double dbScanWidth = ScanWidthInSamples;
			int    iScanWidth  = (int)Math.Round( dbScanWidth );
			int    rBase       = (int)Math.Round( _dp.m_rBase );
			double dbD12XScale = _pBitmapD12.Width / dbScanWidth;

			try { 
				m_AY = (int)Math.Round(rBase/dbScanWidth) * LineMultiplier; // PD needs us to use Round (.999 is 1)
				if( (m_AY < 0) || (m_AY >= _pBitmapRX.Height) || (m_AY >= _pBitmapD12.Height) )
					return;

				int idx1 = rBase % _dp.m_Buf.Length;
				m_SyncMin  = m_SyncMax = _dp.m_B12[idx1]; // Reset sync detect for next pass
				m_SyncRPos = m_SyncPos;                   // Save the last detected sync.
				if( m_SyncHit > -1 ) {
					int iOffs = m_SyncHit - m_SyncLast;
					_rgSyncDetect.Add( new SKPointI( m_SyncHit, iOffs ) );
                    //if( m_AY == 7 ) {
                    //    InitSlots( _dp.Mode.Resolution.Width, iOffs / dbScanWidth);
					//    //_dp.m_rPage = m_SyncHit;
					//}
                    m_SyncLast = m_SyncHit;
					m_SyncHit  = -1;
				}

				for( int i = 0; i < iScanWidth; i++ ){ 
					int    idx = ( rBase + i + _iSyncOffset ) % _dp.m_Buf.Length;
					short  sp  = _dp.m_B12[idx];

					#region D12
					if( sp > _dp.m_SLvl ) {
						m_SyncHit = rBase + i + _iSyncOffset; // Save the absolute position of the sync.
					}
					int x = (int)( ( i + _iSyncOffset ) * dbD12XScale );
					if( (x != dx) && (x >= 0) && (x < _pBitmapD12.Width)){
						int d = Limit256((short)(sp * 256F / 4096F));
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
									rx = x; oChannel.SetPixel( x, _dp.m_Buf[idx] );
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

		/// <summary>This call tries to determine the start the image data
		/// after the VIS. Offset correction.</summary>
		/// <remarks>I see two ways to correct slant. MMSSTV has the CSTVSET values static
		/// so they must change the page width. Since they were using for the distance
		/// along the scan line (ps = n%width) they then get corrected.
		/// I'm going to change my parse values by updated a copy of the mode.</remarks>
		public void SSTVSync() {
            if( m_AY >= _iSyncCheck )  { // was >=

				int   iSW       = (int)ScanWidthInSamples;
                int[] bp        = new int[iSW]; // Array.Clear( bp, 0, bp.Length );
				int   iOffsExpd = (int)_dp.Mode.Offset * _dp.SampFreq / 1000;

				// sum into bp[] four scan lines of data.
                for( int pg = 0; pg < _iSyncCheck; pg++ ){
                	int  ip = pg * iSW;
                	for( int i = 0; i < iSW; i++ ){
						bp[i] += _dp.m_B12[ip + i];
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
                iOffsFound -= iOffsExpd;  
                if( _dp.m_Type == FreqDetect.Hilbert ) 
                	iOffsFound -= _dp.HillTaps/4; // BUG: Add instead of subtract?

				_iSyncCheck  = int.MaxValue; // Stop from trying again.
				_iSyncOffset = iOffsFound;
				_dp.PageRReset();
                //SstvSet.SetOFS( n );
				//_dp.m_rBase = n;
                //m_wBgn  = 0; redraw.
            }
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
			_pBitmapRX.SetPixel( iX, m_AY,    new SKColor( (byte)R, (byte)G, (byte)B ) );

			sValue    = (short)( GetPixelLevel(sValue) + 128 );

			YCtoRGB( out R, out G, out B, sValue,    m_D36[1,iX], m_D36[0,iX]);
			_pBitmapRX.SetPixel( iX, m_AY+1,  new SKColor( (byte)R, (byte)G, (byte)B ) );
		}

		public void InitMartin( SSTVMode oMode, int iSampFreq, double dbCorrection ) {
			if( oMode == null )
				throw new ArgumentNullException( "Mode must not be null." );
			if( oMode.Family != TVFamily.Martin )
				throw new ArgumentOutOfRangeException( "Mode must be of Martin type" );

			LineMultiplier = 1;

			double dbClr = oMode.BlockWidthInMS * iSampFreq / 1000.0;
			double dbSyc = 4.862 * iSampFreq / 1000.0;
			double dbGap = 0.572 * iSampFreq / 1000.0;

			_rgSlots.Clear();

			_rgSlots.Add( new ColorChannel( dbSyc, null ) );
			_rgSlots.Add( new ColorChannel( dbGap, null ) );
			_rgSlots.Add( new ColorChannel( dbClr, PixelSetGreen ) );
			_rgSlots.Add( new ColorChannel( dbGap, null ) );
			_rgSlots.Add( new ColorChannel( dbClr, PixelSetBlue ));
			_rgSlots.Add( new ColorChannel( dbGap, null ) );
			_rgSlots.Add( new ColorChannel( dbClr, PixelSetRed ));
			_rgSlots.Add( new ColorChannel( double.MaxValue, null ) );

			InitSlots( oMode.Resolution.Width, dbCorrection );
		}

		public void InitScottie( SSTVMode oMode, int iSampFreq, double dbCorrection ) {
			if( oMode == null )
				throw new ArgumentNullException( "Mode must not be null." );
			if( oMode.Family != TVFamily.Scottie )
				throw new ArgumentOutOfRangeException( "Mode must be of Scottie type" );

			LineMultiplier = 1;

			double dbClr = oMode.BlockWidthInMS * iSampFreq / 1000.0;
			double dbGap = 1.5 * iSampFreq / 1000.0;
			double dbSyc = 9.0 * iSampFreq / 1000.0;

			_rgSlots.Clear();

			_rgSlots.Add( new ColorChannel( dbGap,  null ) );
			_rgSlots.Add( new ColorChannel( dbClr,  PixelSetGreen ) );
			_rgSlots.Add( new ColorChannel( dbGap,  null ) );
			_rgSlots.Add( new ColorChannel( dbClr,  PixelSetBlue ));
			_rgSlots.Add( new ColorChannel( dbSyc,  null ) );
			_rgSlots.Add( new ColorChannel( dbGap,  null ) );
			_rgSlots.Add( new ColorChannel( dbClr,  PixelSetRed ));
			_rgSlots.Add( new ColorChannel( double.MaxValue, null ) );

			InitSlots( oMode.Resolution.Width, dbCorrection );
		}

		public void InitPD( SSTVMode oMode, int iSampFreq, double dbCorrection ) {
			if( oMode == null )
				throw new ArgumentNullException( "Mode must not be null." );
			if( oMode.Family != TVFamily.PD )
				throw new ArgumentOutOfRangeException( "Mode must be of PD type" );

			LineMultiplier = 2;

			double dbClr   = oMode.BlockWidthInMS * iSampFreq / 1000.0;
			double dbHSync = 20.0 * iSampFreq / 1000.0;
			double dbGap   = 2.08 * iSampFreq / 1000.0;

			_rgSlots.Clear();

			_rgSlots.Add( new ColorChannel( dbHSync, null ) );
			_rgSlots.Add( new ColorChannel( dbGap,   null ) );
			_rgSlots.Add( new ColorChannel( dbClr,   PixelSetY1 ) );
			_rgSlots.Add( new ColorChannel( dbClr,   PixelSetRY ) );
			_rgSlots.Add( new ColorChannel( dbClr,   PixelSetBY ) );
			_rgSlots.Add( new ColorChannel( dbClr,   PixelSetY2 ) );
			_rgSlots.Add( new ColorChannel( double.MaxValue,  null ) );

			InitSlots( oMode.Resolution.Width, dbCorrection );
		}

		/// <summary>
		/// Change the image parsing mode we are in. 
		/// </summary>
		/// <param name="tvMode"></param>
		public void ModeTransition( SSTVMode tvMode ) {
            switch( tvMode.Family ) {
                case TVFamily.PD: 
					InitPD     ( tvMode, _dp.SampFreq, 1 ); 
					break;
                case TVFamily.Martin: 
					InitMartin ( tvMode, _dp.SampFreq, 1 ); 
					break;
                case TVFamily.Scottie: 
					InitScottie( tvMode, _dp.SampFreq, 1 ); 
					break;

                default: 
					throw new ArgumentOutOfRangeException("Unrecognized Mode Type.");
            }

            Start(); // bitmap allocated in here.
		}
    } // End Class TmmSSTV

	public delegate void setPixel( int iX, short sLevel );

	public class ColorChannel {
		public readonly double _dbScanWidth;           // The original specification.
		public double          _dbScanWidthCorrected;  // Compensated value.

		public double   Min      { get; set; }
		public double   Max      { get; protected set; }
		public setPixel SetPixel { get; protected set; }
		public double   Scaling  { get; protected set; }

		public ColorChannel( double dbWidthInSamples, setPixel fnSet ) {
			_dbScanWidth = dbWidthInSamples;
			SetPixel     = fnSet;
		}

		public double Reset( int iBmpWidth, double dbStart, double dbCorrection ) {
			_dbScanWidthCorrected = _dbScanWidth * dbCorrection;

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
