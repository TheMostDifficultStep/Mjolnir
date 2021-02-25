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

	/// <summary>
	/// The demodulator get's the signal from the time to frequency domain. It looks like it
	/// lives on it's own thread. This object then reads the buffer populated by the demodulator
	/// perhaps in yet another thread. 
	/// </summary>
    public class TmmSSTV : IDisposable {
        protected readonly CSSTVDEM dp;
        protected readonly CSSTVSET SSTVSET;

#region variables
		public int        QuickWidth { get; protected set; } // Call set ScanWidthInSamples to set this.

        protected int     m_AX, m_AY;

	    protected short[]  m_Y36 = new short[800];
	    protected short[,] m_D36 = new short[2,800];

	    protected int     m_SyncPos, m_SyncRPos; // RPos Gets set in AutoStopJob() which we haven't implemented yet 
	    protected int     m_SyncMax, m_SyncMin;

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
#endregion

		public event SSTVPropertyChange ShoutTvEvents;

		protected readonly List<ColorChannel> _rgSlots = new List<ColorChannel>(10);

		public TmmSSTV( CSSTVDEM p_dp ) {
			dp      = p_dp         ?? throw new ArgumentNullException( "CSSTVDEM" );
			SSTVSET = p_dp.SSTVSET ?? throw new ArgumentNullException( "CSSTVSET" );

			_pBitmapRX = new SKBitmap( dp.Mode.Resolution.Width, 
									   dp.Mode.Resolution.Height, 
									   SKColorType.Rgb888x, 
									   SKAlphaType.Opaque );
		  //StartOption() ...
		  //dp.sys.m_bCQ100 = FALSE;
		  //g_dblToneOffset = 0.0;
		}

		/// <summary>
		/// This is a good argument for long lived TmmSSTV since, I can potentially
		/// allocate once at constructor and then again at PrepDraw. I'll midigate that
		/// by using the current mode to creat the RX bitmap sos it shouldn't need
		/// realloc.
		/// </summary>
		/// <seealso cref="PrepDraw"/>
		public void Dispose() {
			_pBitmapD12.Dispose();
			_pBitmapRX .Dispose();
		}

		/// <summary>
		/// Return the scan line width in samples. Millisecond based.
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
		protected short GetPixelLevel(short ip)
		{
			if( dp.sys.m_DemCalibration && (_pCalibration != null) ){
				int d = (ip / 8) + 2048;
				if( d < 0 )
					d = 0;
				if( d > 4096 )
					d = 4096;
				return _pCalibration[d];
			} else {
				double d = ip - dp.sys.m_DemOff;
				d *= ( d >= 0 ) ? dp.sys.m_DemWhite : dp.sys.m_DemBlack;
				return (short)d;
			}
		}

		protected int GetPictureLevel( short ip, int i )
		{
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

		protected static short Limit256(short d)
		{
			if( d < 0 )
				d = 0;
			if( d > 255 )
				d = 255;

			return d;
		}

		protected static short Limit256(int d)
		{
			if( d < 0 )
				d = 0;
			if( d > 255 )
				d = 255;

			return (short)d;
		}

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
	        m_ASPos[3] = (dp.Mode.Resolution.Height - 36);

	        switch(dp.Mode.LegacyMode){
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
	        m_Mult         = (int)(dp.Mode.ScanLineWidthInSamples / 320.0);
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

        /// <summary>
        /// Bunch of stuff to do when we stop, but I don't think it's necessary to
        /// sort it all right now.
        /// </summary>
        void AllStop() {
			//bool lost = dp.m_Lost;
			//if( dp.m_LoopBack != true ){
			//	WriteHistory(1);
			//} else {
			//	SBWHist->Enabled = TRUE;
			//}
			//TrackTxMode(0);
			//UpdateModeBtn();
			//if( lost )
            //    InfoRxLost();
			//UpdateUI();
         }

        void PrepViewers() {
			//if( KRSW->Checked ){
			//	if( pRxView != NULL ){
			//		if( pRxView->Handle != ::GetForegroundWindow() ){
			//			::SetForegroundWindow(pRxView->Handle);
			//		}
			//	}
			//	else if( m_MainPage != pgRX ){
			//		AdjustPage(pgRX);
			//	}
			//}

			//if( pRxView != NULL ){
			//	pRxView->UpdateTitle(SSTVSET.m_Mode, false );
			//}
			//if( pComm != NULL )
            //    pComm->SetScan(dp.m_Sync);

            // DispSyncStat();
        }

        void PrepDraw() {
			m_SyncRPos      = m_SyncPos = -1;
			m_SyncAccuracyN = 0;
			m_AX			= -1;
			m_AY			= -5;

			//if( dp.m_ReqSave ){
			//	WriteHistory(1);
			//}
			if( _pBitmapRX == null ||
				dp.Mode.Resolution.Width  != _pBitmapRX.Width ||
				dp.Mode.Resolution.Height != _pBitmapRX.Height   )
			{
				if( _pBitmapRX != null )
					_pBitmapRX.Dispose();
				_pBitmapRX = new SKBitmap( dp.Mode.Resolution.Width, 
										   dp.Mode.Resolution.Height, 
										   SKColorType.Rgb888x, 
										   SKAlphaType.Opaque );
			}

			//UpdateModeBtn();
			//::GetUTC(&m_StartTime);

            // TSpeedButton SBTX, looks like TX send button, down to send. 
            // but we want to be RX mode. See: TMmsstv::SBTXClick() && TMmsstv::ToRX()
            // Might be loop back shows what we're sending!! Maybe!
            dp.PrepDraw( /* SBTX->Down ? sys.m_echo : 0 */ false );
			//WaveStg.WInit();
			//RxHist.ClearAddFlag();
			//SBWHist->Enabled = FALSE;
			//KRH->Enabled = FALSE; // Copy to history.

            PrepViewers();
        }

		void ClearTVImages() {
			//if( KRD->Checked ){ // KRD: AutoClear
			//	ClearDraw(pBitmapRX, PBoxRX, sys.m_ColorRXB);
			//}
			//ClearDraw(pBitmapD12, PBoxD12, clWhite);
		}

		public void DrawSSTV()
		{
			// Go until the rPage catches up with the wPage.
			// Process one full scan line at a time.
			while( dp.m_Sync && (dp.m_wPage != dp.m_rPage) ){
				if( dp.m_wBgn != 0 ){
					if( dp.m_wBgn != 1 ){
                        PrepDraw();
					}
					dp.OnDrawBegin();
					//dp.SyncSSTV( m_SyncAccuracy ); // TODO: Double check this sync value.
					ClearTVImages();
					InitAutoStop( dp.SampBase );
					m_AutoSyncCount = m_AutoSyncDis = 0;
				}
				//int ip = dp.m_rPage * dp.m_BWidth;
                //dp.StorePage( ip );

				// DrawSSTVNormal();
				DrawSSTVNormal2();
				ShoutTvEvents?.Invoke( ESstvProperty.DownLoadTime );

				if( m_AY > dp.Mode.Resolution.Height ){ // SSTVSET.m_L
					if( dp.m_Sync ){
						dp.Stop();
                        AllStop();
					}
					break;
				}
                dp.RPageIncrement();
			}
		}

		protected virtual int LineMultiplier => 1;

		protected void DrawSSTVNormal2() {
			int n         = dp.m_rBase; // Increments in += SSTVSET.m_WD chunks.
			int dx        = -1;         // Saved X pos from the B12 buffer.
			int rx        = -1;         // Saved X pos from the Rx  buffer.
			int ch        = 0;          // current channel skimming the Rx buffer portion.
			int rPageOffs = dp.m_rPage * dp.m_BWidth;

			// Color channel is the same width for each color so just do this once.
			// Will need slant correction at some point, so this will need updating...
			double dbClrBlock  = dp.Mode.BlockWidthInMS * dp.SampFreq / 1000.0; // Color block size in samples.
			double dbRxXScale  = _pBitmapRX.Width  / dbClrBlock;
			double dbD12XScale = _pBitmapD12.Width / (double)QuickWidth;

			if( n < 0 ) 
				throw new ApplicationException( "m_rBase went negative" );

			try { // Added the B12 height check b/c of PD290 error. Look into that.
				m_AY = (int)Math.Round(n/ScanWidthInSamples) * LineMultiplier; // PD needs us to use Round (.999 is 1)
				if( (m_AY < 0) || (m_AY >= _pBitmapRX.Height) || (m_AY >= _pBitmapD12.Height) )
					return;

				// KRSA, assigned sys.m_UseRxBuff ? TRUE : FALSE, see also GetPictureLevel()
				if( (dp.sys.m_AutoStop || dp.sys.m_AutoSync /* || KRSA->Checked */ ) && 
					dp.m_Sync && (m_SyncPos != -1) ) {
					AutoStopJob();
				}
				m_SyncMin  = m_SyncMax = dp.m_B12[rPageOffs];
				m_SyncRPos = m_SyncPos;

				for( int i = 0; i < QuickWidth; i++ /*, n++ */ ){ // SSTVSET.m_WD
				  //double ps = n % (int)SSTVSET.m_TW; // fmod(double(n), SSTVSET.m_TW)
					short  sp = dp.m_B12[rPageOffs + i];

					#region D12
					if( m_SyncMax < sp ) {
						m_SyncMax = sp;
						m_SyncPos = i; // was (int)ps
					} else if( m_SyncMin > sp ) {
						m_SyncMin = sp;
					}
					int x = (int)( i * dbD12XScale ); // "i" was ps, QW was TW, note TW == WD.
					if( (x != dx) && (x >= 0) && (x < _pBitmapD12.Width)){
						int d = sp * 256 / 4096;
						d = Limit256(d);
						_pBitmapD12.SetPixel( x, m_AY, new SKColor( (byte)d, (byte)d, (byte)d ) );
						dx = x;
					}
					#endregion

					do {
						ColorChannel oChannel = _rgSlots[ch];
						if( i < oChannel.Max ) {
							if( oChannel.SetPixel != null ) {
								x = (int)((i - oChannel.Min) * dbRxXScale );
								if( (x != rx) && (x >= 0) && (x < _pBitmapRX.Width) ) {
									short ip = dp.m_Buf[rPageOffs + i];
									rx = x; oChannel.SetPixel( x, ip );
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

				// In the future well write some error message to the screen & reset.
				throw new ApplicationException( "Problem decoding scan line.", oEx );
			}
		}

		protected void InitSlots() {
			double dbStart = 0;
			for( int i = 0; i< _rgSlots.Count; ++i ) {
				_rgSlots[i].Min = dbStart;
				dbStart = _rgSlots[i].Max + 1/ (dp.SampFreq * 1000 );
			}
			QuickWidth = (int)ScanWidthInSamples;
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
		/// <param name="iX"></param>
		/// <param name="sValue"></param>
		protected void PixelSetRed( int iX, short sValue ) {
			sValue = (short)( GetPixelLevel(sValue) + 128 );
			_pBitmapRX.SetPixel( iX, m_AY,  new SKColor( (byte)Limit256(sValue), 
				                                        (byte)m_D36[0,iX], 
														(byte)m_D36[1,iX] ) );
		}

    } // End Class

	public delegate void setPixel( int iX, short sLevel );

	public class ColorChannel {
		public double   Min      { get; set; }
		public double   Max      { get; protected set; }
		public setPixel SetPixel { get; protected set; }

		public ColorChannel( double dbOffset, setPixel fnSet ) {
			Max      = dbOffset;
			SetPixel = fnSet;
		}

        public override string ToString() {
			string strType = SetPixel != null ? SetPixel.GetType().ToString() : string.Empty;
            return Max.ToString() + " " + strType;
        }
    }

	public class TmmMartin : TmmSSTV {
		public TmmMartin( CSSTVDEM p_dp ) : base( p_dp ) {
			if( p_dp.Mode.Family != TVFamily.Martin )
				throw new ArgumentOutOfRangeException( "Mode must be of Martin type" );

			double dbClr = p_dp.Mode.BlockWidthInMS * p_dp.SampFreq / 1000.0;
			double dbSyc = 4.862 * p_dp.SampFreq / 1000.0;
			double dbGap = 0.572 * p_dp.SampFreq / 1000.0;
			double dbIdx = 0;

			_rgSlots.Add( new ColorChannel( dbIdx += dbSyc, null ) );
			_rgSlots.Add( new ColorChannel( dbIdx += dbGap, null ) );
			_rgSlots.Add( new ColorChannel( dbIdx += dbClr,  PixelSetGreen ) );
			_rgSlots.Add( new ColorChannel( dbIdx += dbGap,  null ) );
			_rgSlots.Add( new ColorChannel( dbIdx += dbClr,  PixelSetBlue ));
			_rgSlots.Add( new ColorChannel( dbIdx += dbGap,  null ) );
			_rgSlots.Add( new ColorChannel( dbIdx += dbClr,  PixelSetRed ));
			_rgSlots.Add( new ColorChannel( double.MaxValue, null ) );

			InitSlots();
		}
	}

	public class TmmScottie : TmmSSTV {
		public TmmScottie( CSSTVDEM p_dp ) : base( p_dp ) {
			if( p_dp.Mode.Family != TVFamily.Scottie )
				throw new ArgumentOutOfRangeException( "Mode must be of Scottie type" );

			double dbClr = p_dp.Mode.BlockWidthInMS * p_dp.SampFreq / 1000.0;
			double dbGap = 1.5 * p_dp.SampFreq / 1000.0;
			double dbSyc = 9.0 * p_dp.SampFreq / 1000.0;
			double dbIdx = 0;

			_rgSlots.Add( new ColorChannel( dbIdx += dbGap,  null ) );
			_rgSlots.Add( new ColorChannel( dbIdx += dbClr,  PixelSetGreen ) );
			_rgSlots.Add( new ColorChannel( dbIdx += dbGap,  null ) );
			_rgSlots.Add( new ColorChannel( dbIdx += dbClr,  PixelSetBlue ));
			_rgSlots.Add( new ColorChannel( dbIdx += dbSyc,  null ) );
			_rgSlots.Add( new ColorChannel( dbIdx += dbGap,  null ) );
			_rgSlots.Add( new ColorChannel( dbIdx += dbClr,  PixelSetRed ));
			_rgSlots.Add( new ColorChannel( double.MaxValue, null ) );

			InitSlots();
		}
	}

	public class TmmPD : TmmSSTV {
		public TmmPD( CSSTVDEM p_dp ) : base( p_dp ) {
			if( p_dp.Mode.Family != TVFamily.PD )
				throw new ArgumentOutOfRangeException( "Mode must be of PD type" );

			double dbClr   = p_dp.Mode.BlockWidthInMS * p_dp.SampFreq / 1000.0;
			double dbGap   = 2.08 * p_dp.SampFreq / 1000.0;
			double dbHSync = 20.0 * p_dp.SampFreq / 1000.0;
			double dbIdx   = 0;

			_rgSlots.Add( new ColorChannel( dbIdx += dbHSync, null ) );
			_rgSlots.Add( new ColorChannel( dbIdx += dbGap,   null ) );
			_rgSlots.Add( new ColorChannel( dbIdx += dbClr,   PixelSetY1 ) );
			_rgSlots.Add( new ColorChannel( dbIdx += dbClr,   PixelSetRY ) );
			_rgSlots.Add( new ColorChannel( dbIdx += dbClr,   PixelSetBY ) );
			_rgSlots.Add( new ColorChannel( dbIdx += dbClr,   PixelSetY2 ) );
			_rgSlots.Add( new ColorChannel( double.MaxValue,  null ) );

			InitSlots();
		}

		protected override int LineMultiplier => 2;

		void PixelSetY1( int iX, short sValue ) {
			m_Y36[iX] = (short)( GetPixelLevel(sValue) + 128 );
		}

		void PixelSetRY( int iX, short sValue ) {
			m_D36[1,iX] = GetPixelLevel( sValue );
		}

		void PixelSetBY( int iX, short sValue ) {
			m_D36[0,iX] = GetPixelLevel( sValue );
		}

		void PixelSetY2( int iX, short sValue ) {
			short R, G, B;

			YCtoRGB( out R, out G, out B, m_Y36[iX], m_D36[1,iX], m_D36[0,iX]);
			_pBitmapRX.SetPixel( iX, m_AY,    new SKColor( (byte)R, (byte)G, (byte)B ) );

			sValue    = (short)( GetPixelLevel(sValue) + 128 );

			YCtoRGB( out R, out G, out B, sValue,    m_D36[1,iX], m_D36[0,iX]);
			_pBitmapRX.SetPixel( iX, m_AY+1,  new SKColor( (byte)R, (byte)G, (byte)B ) );
		}
	}
    public class DocSSTV :
        IPgParent,
        IPgLoad<TextReader>,
        IPgSave<TextWriter>,
        IDisposable
    {
        private bool disposedValue;

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

		private void SaveRxImage( TmmSSTV oSSTV ) {
			if( oSSTV == null )
				return;

			string strPath = Environment.GetFolderPath( Environment.SpecialFolder.MyPictures );

			using SKImage image  = SKImage.FromBitmap(oSSTV._pBitmapRX);
			using var     data   = image.Encode( SKEncodedImageFormat.Png, 80 );
            using var     stream = File.OpenWrite( Path.Combine( strPath, "testmeh.png") );

            data.SaveTo(stream);
		}

		/// <summary>
		/// The decoder has determined the the incoming video mode. Create a
		/// Tmm??? to match the given mode. This call comes from CSSTVDEM::Start() 
		/// Start() resets the write buffer. Then later when DrawSSTV get's called
		/// the bitmaps get allocated noting the begin. More thread issues to
		/// keep in mind. We can grab the image pointer here since the mode
		/// won't change. But that won't be true if we re-use the TmmSSTV object.
		/// Then we'll need an event on TmSSTV.
		/// </summary>
		/// <remarks>
		/// I might need to toss this approach and just have a TmmSSTV that 
		/// understands all the modes. But let's see how far I can take this
		/// new approach. Certainly I'll have send a message if the
		/// decoder is in a different thread than the renderer.
		/// Also, it's clunky to set the bitmaps as I'm doing. Need to look
		/// into that more later.</remarks>
        private void ListenNextRxMode( SSTVMode tvMode )
        {
			if( _oRxSSTV != null ) {
				ReceiveImage.Bitmap = null;
				SyncImage   .Bitmap = null;
				_oRxSSTV.Dispose();
			}

            _oRxSSTV = tvMode.Family switch {
                TVFamily.PD      => new TmmPD     (_oSSTVDeModulator),
                TVFamily.Martin  => new TmmMartin (_oSSTVDeModulator),
                TVFamily.Scottie => new TmmScottie(_oSSTVDeModulator),

                _ => throw new ArgumentOutOfRangeException("Unrecognized Mode Type."),
            };

			tvMode.ScanLineWidthInSamples = _oRxSSTV.QuickWidth;

			ReceiveImage.Bitmap = _oRxSSTV._pBitmapRX;
			SyncImage   .Bitmap = _oRxSSTV._pBitmapD12;

			Raise_PropertiesUpdated( ESstvProperty.RXImageNew );
            _oRxSSTV.ShoutTvEvents += ListenTvEvents;
        }

		/// <summary>
		/// Forward events coming from TmmSSTV
		/// </summary>
        private void ListenTvEvents( ESstvProperty eProp )
        {
            Raise_PropertiesUpdated( eProp );
        }

		/// <summary>
		/// Note that we don't take the mode in this task since it's got to be
		/// devined from the VIS by the CSSTVDEM object.
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
										typeof( ArithmeticException ) };
                    if( rgErrors.IsUnhandled( oEx ) )
                        throw;

					SaveRxImage( _oRxSSTV );

                    LogError( "Trouble recordering in SSTV" );
                    // We can't call _oWorkPlace.Stop() b/c we're already in DoWork() which will
                    // try calling the _oWorker which will have been set to NULL!!
                    break; // Drop down so we can unplug from our Demodulator.
                }
                yield return 0; // 44,100 hz is slow, let's go as fast as possible. >_<;;
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
						_oSSTVDeModulator = oDemodTst;

						_oWorkPlace.Queue( GetRecorderTask(), 0 );
					}
                }
            }
        }

		/// <summary>
		/// In this 1'st test we skip the VIS and simply test video transmit and receive.
		/// The CSSTVDEM object is not used in these tests. The transmit and recieve
		/// are set from the given mode.
		/// </summary>
		/// <param name="oMode">User selected mode. Any should would.</param>
		/// <returns>Time in ms before next call wanted.</returns>
        public IEnumerator<int> GetRecordTestTask( SSTVMode oMode ) {
			if( oMode == null )
				throw new ArgumentNullException( "Mode must not be Null." );

			IEnumerator<int> oIter   = _oSSTVGenerator.GetEnumerator();

			oIter            .MoveNext(); // skip the VIS for now.
			_oSSTVDeModulator.SSTVSET.SetMode( oMode );
			_oSSTVDeModulator.Start( oMode );

			TmmSSTV oRxSSTV = null;

            oRxSSTV = oMode.Family switch {
                TVFamily.PD      => new TmmPD     (_oSSTVDeModulator),
                TVFamily.Martin  => new TmmMartin (_oSSTVDeModulator),
                TVFamily.Scottie => new TmmScottie(_oSSTVDeModulator),

                _ => throw new ArgumentOutOfRangeException("Unrecognized Mode Type."),
            };

			ReceiveImage.Bitmap = oRxSSTV._pBitmapRX;
			SyncImage   .Bitmap = oRxSSTV._pBitmapD12;

			oMode.ScanLineWidthInSamples = oRxSSTV.QuickWidth;

            while( oIter.MoveNext() ) {
				oRxSSTV.DrawSSTV();
				Raise_PropertiesUpdated( ESstvProperty.DownLoadTime );
				yield return 1;
			};
			SaveRxImage( oRxSSTV );
		}

		/// <summary>
		/// This test generates the the video signal but doesn't actually create audio
		/// tone but a fake stream of frequency data. Thus skipping the A/D coverter code
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

					_oSSTVGenerator = oMode.Family switch {
						TVFamily.PD      => new GeneratePD     ( _oDocSnip.Bitmap, oDemodTst, oMode ),
						TVFamily.Martin  => new GenerateMartin ( _oDocSnip.Bitmap, oDemodTst, oMode ),
						TVFamily.Scottie => new GenerateScottie( _oDocSnip.Bitmap, oDemodTst, oMode ),

						_ => throw new ArgumentOutOfRangeException("Unrecognized Mode Type."),
					};

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
