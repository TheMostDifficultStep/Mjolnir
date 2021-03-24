//Copyright+LGPL

//-----------------------------------------------------------------------------------------------------------------------------------------------
// Copyright 2000-2013 Makoto Mori, Nobuyuki Oba
// (c) 2021 https://github.com/TheMostDifficultStep
//-----------------------------------------------------------------------------------------------------------------------------------------------
// This file is a port of MMSSTV.

// MMSSTV is free software: you can redistribute it and/or modify it under the terms of the GNU Lesser General Public License
// as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later version.

// This code is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of 
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU Lesser General Public License for more details.

// You should have received a copy of the GNU Lesser General Public License along with MMTTY.  If not, see 
// <http://www.gnu.org/licenses/>.
//-----------------------------------------------------------------------------------------------------------------------------------------------

using System;
using System.Collections;
using System.Collections.Generic;

namespace Play.Sound {

	public class SYSSET {
		int     m_Priority = 0;

		int		m_SoundPriority = 1;
		int		m_SoundStereo   = 0;
		int		m_StereoTX      = 0;

		int		m_TxRxLock;
		int     m_RTSonRX;

		int		m_lmsbpf;
		int		m_echo;

		int		m_AutoTimeOffset;
		int		m_TimeOffset;
		int		m_TimeOffsetMin;
		int		m_LogLink;

		int		m_SoundFifoRX;
		int		m_SoundFifoTX;

		public bool   m_TestDem  { get; protected set; } = false; // used
		public double m_DemOff   { get; } = 0;
		public double m_DemWhite { get; } = 128.0/16384.0;
		public double m_DemBlack { get; } = 128.0/16384.0;
		public bool   m_DemCalibration { get; } = false; // see TmmSSTV.pCalibration

		//int     m_FFTType;
		//int	  m_FFTGain;
		//int	  m_FFTResp;
		//int     m_FFTStg;
		//int     m_FFTWidth;
		//int	  m_FFTAGC;
		//int     m_FFTPriority;
		//double  m_TxSampOff;

		public double	m_SampFreq { get; protected set; } // used

		int      m_TuneTXTime;
		int      m_TuneSat;

		int      m_Way240;
		int		 m_AutoMargin;
				  
		// UseRxBuff : can be CWaveStrage: 2 or m_StgBuf: 1 or off.
		public int  m_UseRxBuff { get; protected set; } = 2; 
		public bool m_AutoStop  { get; protected set; } = false; // used
		public bool m_AutoSync  { get; protected set; } = true;
				  
		int      m_TXFSKID;

		string[] m_TextList = new string[16];

		int      m_PicSelRTM;
		int      m_PicSelSmooz;

		int      m_Differentiator;
		double   m_DiffLevelP;
		double   m_DiffLevelM;

		public bool m_Repeater { get; protected set; } = false; // used
		public int  m_RepSenseLvl { get; protected set; }  // トーン検出感度 : Tone detection sensitivity (used)
		public int  m_RepTimeA { get; protected set; }     // トーン検出時間 : Tone detection time 
		public int  m_RepTimeB { get; protected set; }     // トーン検出からAnsCW出力までの時間 : Time from tone detection to AnsCW output 
		public int  m_RepTimeC { get; protected set; }     // 受信待機のタイムアウト : Receive wait timeout
		public int  m_RepTimeD { get; protected set; }     // リプレイ送信の遅延時間 : Delay time for replay transmission 

		string   m_Msg;

	  //int			m_TempDelay;
		int			m_Temp24;
		public bool	m_bCQ100 { get; protected set; } = false;

		public SYSSET( double dbSampFreq ) {
			m_SampFreq = dbSampFreq;

			// See TMMSSTV::StartOption();
			// Ver1.13 : Added an option that lowers the tone frequency by 1000Hz (use -i option on start)
			//else if( as == "-i" ){
			//	sys.m_bCQ100 = TRUE;
			//	g_dblToneOffset = -1000.0;
			//}
		}
	}

	public enum AllModes {
		smR36 = 0,
		smR72,
		smAVT_obsolete,
		smSCT1,
		smSCT2,
		smSCTDX,
		smMRT1,
		smMRT2,
		smSC2_180,
		smSC2_120,
		smSC2_60,
		smPD50,
		smPD90,
		smPD120,
		smPD160,
		smPD180,
		smPD240,
		smPD290,
		smP3,
		smP5,
		smP7,
		smMR73,
		smMR90,
		smMR115,
		smMR140,
		smMR175,
		smMP73,
		smMP115,
		smMP140,
		smMP175,
		smML180,
		smML240,
		smML280,
		smML320,
		smR24,
		smRM8,
		smRM12,
		smMN73,
		smMN110,
		smMN140,
		smMC110,
		smMC140,
		smMC180,
		smEND,
	}

	public enum BandPass {
		Undefined = -1,
		Wide = 1,
		Narrow = 2,
		VeryNarrow = 3
	}

	public class CSSTVSET {
		public double   m_OFP    { get; protected set; } // Looks used to help correct slant.
		public int      m_OFS    { get; protected set; }
		public int		m_IOFS   { get; protected set; }

		public double  m_SampFreq { get; protected set; }

		public readonly UInt32[] m_MS = new UInt32[(int)AllModes.smEND];
		public UInt32   m_MSLL { get; protected set; }
		public UInt32   m_MSL  { get; protected set; }
		public UInt32   m_MSH  { get; protected set; }

		public int     m_AFCW { get; protected set; }
		public int     m_AFCB { get; protected set; }
		public int     m_AFCE { get; protected set; }

		public bool	m_fNarrow   { get; protected set; }
	  //public bool	m_fTxNarrow { get; protected set; }

		public readonly double g_dblToneOffset;
		public readonly double m_dbTxSampOffs;

		public double SampFreq => m_SampFreq;
		private readonly bool m_bCQ100;

		/// <summary>
		/// Should we ever support: smMN73,smMN110,smMN140,smMC110,smMC140, or smMC180,
		/// those are all narrow band.
		/// </summary>
		public static bool IsNarrowMode(SSTVMode mode)	{
			return false;
		}

		public CSSTVSET( SSTVMode oMode, double dbToneOffset, double dbSampFreq, double dbTxSampOffs, bool bCQ100 )
		{
			// These used to be globals, I'll see how much they change and if I need
			// to refactor initialization and such. Since SetSampFreq() get's called
			// randomly, will have to re-visit these.
			m_SampFreq      = dbSampFreq;
			m_dbTxSampOffs  = dbTxSampOffs;
			g_dblToneOffset = dbToneOffset;
			m_bCQ100        = bCQ100;
			m_fNarrow       = false;  // Recieve width.

			SetMode( oMode ); 
			InitIntervalPara();
		}

		public void InitIntervalPara()
		{
			for( int i = 0; i < (int)AllModes.smEND; i++ ){
				m_MS[i] = (uint)(GetTiming((AllModes)i) * m_SampFreq / 1000.0 );
			}
			m_MS[2] = 0;                                 // AVT
			m_MSLL = (uint)(50.0   *     m_SampFreq / 1000.0 );    // Lowest
			m_MSL  = (uint)(63.0   *     m_SampFreq / 1000.0 );    // Lowest
			m_MSH  = (uint)(1390.0 * 3 * m_SampFreq / 1000.0 );    // Highest
		}

		public void SetOFS( int iOFS ) {
			m_IOFS = m_OFS = iOFS;
		}

		/// <remarks>This gets called by the demodulator. Ick. This means
		/// we can't make the members here readonly.</remarks>
		public void SetMode( SSTVMode tvMode )
		{
			//m_SampFreq = sys.m_SampFreq; <-- this gets set in the constructor now.
			m_fNarrow = CSSTVSET.IsNarrowMode( tvMode );
			SetSampFreq( tvMode );
		}

		void SetSampFreq(SSTVMode tvMode){
			//m_TW = GetTiming(m_Mode) * m_SampFreq / 1000.0;
			switch(tvMode.Family){
				case TVFamily.Martin:
					m_AFCW = (int)(2.0 * SampFreq / 1000.0);
					m_AFCB = (int)(1.0 * SampFreq / 1000.0);
					break;
				default:
					m_AFCW = (int)(3.0 * SampFreq / 1000.0);
					m_AFCB = (int)(1.5 * SampFreq / 1000.0);
					break;
			}

			// This is the "-i" option set in TMMSSTV::StartOption() if bCQ100 is
			// true then the offset is -1000. Else the offset is 0!!
			if( m_bCQ100 ) { // Used to be a global.
    			double d = m_OFP * 1000.0 / SampFreq;
				m_OFP = (d + (1100.0/g_dblToneOffset)) * SampFreq / 1000.0;
			}
			m_AFCE = m_AFCB + m_AFCW;
		}

		/// <summary>
		/// Note: This is used by SyncCheck and might be removable when I get my own synchronizer working.
		/// </summary>
		double GetTiming(AllModes mode) {
			switch(mode){
				case AllModes.smR36:
					return 150.0;
				case AllModes.smR72:
					return 300.0;
				case AllModes.smSCT2:
					return 277.692;
				case AllModes.smSCTDX:
					return 1050.3;
				case AllModes.smMRT1:
					return 446.446;
				case AllModes.smMRT2:
					return 226.798;
				case AllModes.smSC2_180:
					return 711.0437;
				case AllModes.smSC2_120:
					return 475.52248;
				case AllModes.smSC2_60:
					return 240.3846;
				case AllModes.smPD50:
					return 388.160;
				case AllModes.smPD90:
					return 703.040;
				case AllModes.smPD120:
					return 508.480;
				case AllModes.smPD160:
					return 804.416;
				case AllModes.smPD180:
					return 754.24;
				case AllModes.smPD240:
					return 1000.00;
				case AllModes.smPD290:
					return 937.28;
				case AllModes.smP3:
					return 409.375;
				case AllModes.smP5:
					return 614.0625;
				case AllModes.smP7:
					return 818.75;
				case AllModes.smMR73:
					return 286.3;
				case AllModes.smMR90:
					return 352.3;
				case AllModes.smMR115:
					return 450.3;
				case AllModes.smMR140:
					return 548.3;   //269*2 + 10;
				case AllModes.smMR175:
					return 684.3;   //337*2 + 10;
				case AllModes.smMP73:
					return 570.0;
				case AllModes.smMP115:
					return 902.0;
				case AllModes.smMP140:
					return 1090.0;
				case AllModes.smMP175:
					return 1370.0;
				case AllModes.smML180:
					return 363.3;
				case AllModes.smML240:
					return 483.3;
				case AllModes.smML280:
					return 565.3;
				case AllModes.smML320:
					return 645.3;
				case AllModes.smR24:
					return 200.0;
				case AllModes.smRM8:
					return 66.89709;
				case AllModes.smRM12:
					return 100.0;
				case AllModes.smMN73:
					return 570.0;
				case AllModes.smMN110:
					return 858.0;
				case AllModes.smMN140:
					return 1090.0;
				case AllModes.smMC110:
					return 428.5;
				case AllModes.smMC140:
        			return 548.5;
				case AllModes.smMC180:
					return 704.5;
				default:    // smSCT1
					return 428.22;
			}
		}

		//void SetTxSampFreq() {
		//	int dm1, dm2, iTL;
		//	GetPictureSize( out dm1, out dm2, out iTL, m_TxMode);
		//	m_TL = iTL;
		//	m_TTW = GetTiming(m_TxMode) * m_TxSampFreq / 1000.0;
		//}
	}

	/// <summary>
	/// Just a dummy until I get more stuff running.
	/// </summary>
	class CScope {
		public void WriteData( double d ) { }
	}

	/// <summary>
	/// Another dummy for now.
	/// </summary>
	class CTICK {
		public void Write( double d ) { }
	}

	/// <summary>
	/// Another dummy for now.
	/// </summary>
	class CSLVL {
		public void Do( double d ) { }
	}

	public class CHILL
	{
		public static int HILLTAP = 48;

		double[]  Z = new double[HILLTAP+1];
		double[]  H = new double[HILLTAP+1];
		double[]  m_A = new double[4];

		double	  m_OFF;
		double	  m_OUT;
	  //double[]  m_ph;
	    public int m_htap { get; protected set; }
		int		  m_df;
		int		  m_tap;
		readonly CIIR m_iir = new CIIR();

		double SampFreq { get; }
		double SampBase { get; }
		double ToneOffs { get; }

		public CHILL( SYSSET sys, double dbSampFreq, double dbSampBase, double dbToneOffset )
		{
			SampFreq = dbSampFreq;
			SampBase = dbSampBase;
			ToneOffs = dbToneOffset;

			SetWidth( sys, false);

		    m_htap = m_tap / 2;
			MakeHilbert(H, m_tap, SampFreq, 100, SampFreq/2 - 100);
			m_A[0] = m_A[1] = m_A[2] = m_A[3] = 0;

			m_iir.MakeIIR(1800, SampFreq, 3, 0, 0);
		}

		//---------------------------------------------------------------------------
		//‚e‚h‚qƒtƒBƒ‹ƒ^iƒqƒ‹ƒxƒ‹ƒg•ÏŠ·ƒtƒBƒ‹ƒ^j‚ÌÝŒv
		//
		static void MakeHilbert(double [] H, int N, double fs, double fc1, double fc2)
		{
			int    L  = N / 2;
			double T  = 1.0 / fs;
			double W1 = 2 * Math.PI * fc1;
			double W2 = 2 * Math.PI * fc2;

			double w;
			int n;
			double x1, x2;
			for( n = 0; n <= N; n++ ){
				if( n == L ){
					x1 = x2 = 0.0;
				} else if( (n - L) > 0 ){
					x1 = ((n - L) * W1 * T);
					x1 = Math.Cos(x1) / x1;
					x2 = ((n - L) * W2 * T);
					x2 = Math.Cos(x2) / x2;
				} else {
					x1 = x2 = 1.0;
				}
				w = 0.54 - 0.46 * Math.Cos(2*Math.PI*n/(N));
				H[n] = -(2 * fc2 * T * x2 - 2 * fc1 * T * x1) * w;
			}

			if( N < 8 ){
				w = 0;
    			for( n = 0; n <= N; n++ ){
					w += Math.Abs(H[n]);
    			}
				if( w != 0 ){
					w = 1.0 / w;
					for( n = 0; n <= N; n++ ){
						H[n] *= w;
					}
    			}
			}
		}

		public void SetWidth( SYSSET sys, bool fNarrow )
		{
			if( fNarrow ){
				m_OFF = (2 * Math.PI * (CSSTVDEM.NARROW_CENTER + ToneOffs)) / SampFreq;
				m_OUT = 32768.0 * SampFreq / (2 * Math.PI * CSSTVDEM.NARROW_BW);
			} else {
				m_OFF = (2 * Math.PI * (1900 + ToneOffs)) / SampFreq;
				m_OUT = 32768.0 * SampFreq / (2 * Math.PI * 800);
			}
			if( SampBase >= 40000 ){
				m_OFF *= 4;
				m_OUT *= 0.25;
				m_tap = 48; //48
				m_df  = 2;
			} else if( SampBase >= 16000 ){
				m_OFF *= 2;
				m_OUT *= 0.5;
				m_tap = 24; //24
				m_df  = 1;
			} else {
				m_tap = 12; //12
				m_df = 0;
			}
			if( sys.m_bCQ100 ){
				m_tap *= 3;
			}
		}

		//-------------------------------------------------
		// ‚e‚h‚qƒtƒBƒ‹ƒ^‚Ì‚½‚½‚«ž‚Ý‰‰ŽZ
		static double DoFIR(double []hp, double []zp, double d, int tap)
		{
			//memcpy(zp, &zp[1], sizeof(double)*tap);
			Array.Copy( zp, 1, zp, 0, tap );
			zp[tap] = d;
			d = 0.0;
			for( int i = 0; i <= tap; i++ ){
				d += zp[i] * hp[i];
			}
			return d;
		}

		public double Do(double d)
		{
			DoFIR(H, Z, d, m_tap);

			double a = 0; // *m_ph;
			//if( a )
			//	a = Math.Atan2(d, a);

			d = a - m_A[0];
			switch(m_df){
				case 1:
					m_A[0] = m_A[1];
					m_A[1] = a;
					break;
				case 2:
					m_A[0] = m_A[1];
					m_A[1] = m_A[2];
					m_A[2] = m_A[3];
					m_A[3] = a;
					break;
				default:
					m_A[0] = a;
					break;
			}
			if( d >= Math.PI ){
				d = d - Math.PI*2;
			}
			else if( d <= -Math.PI ){
				d = d + Math.PI*2;
			}
			d += m_OFF;
			return m_iir.Do(d * m_OUT);
		}
	}

	class CFQC {
		protected static double ZEROFQ	= (-1900.0/400.0);

		int     m_Mode;
		int     m_Count;
		double  m_ACount;
		double  m_d;
		double  m_fq;
		double  m_out;
		double  m_SampFreq;

		double	m_CenterFQ;
		double	m_HighFQ;
		double	m_LowFQ;

		double	m_HighVal;
		double	m_LowVal;
		double	m_BWH;

		int     m_Type;
		bool    m_Limit;
		CIIR    m_iir;

		double  m_SmoozFq;
		CSmooz  m_fir;

		int     m_outOrder;
		double  m_outFC;

		int		m_Timer;
		int		m_STimer;

		double SampFreq { get; }
		double ToneOffs { get; }

		public CFQC( double dbSampFreq, double dbToneOffs )
		{
			SampFreq = dbSampFreq;
			ToneOffs = dbToneOffs;

			m_Type = 0;

			m_iir = new CIIR();
			m_fir = new CSmooz();

			m_Limit    = true;
			m_d        = 0;
			m_Count    = 0;
			m_ACount   = 0;
			m_fq       = ZEROFQ;
			m_d        = 0;
			m_out      = 0;
			m_outOrder = 3;
			m_outFC    = 900;
			m_SmoozFq  = 2200;
			m_SampFreq = SampFreq;
			m_Timer    = m_STimer = (int)SampFreq; // int(m_SampFreq);

			SetWidth( false );
			CalcLPF();
		}

		public void SetWidth(bool fNarrow)
		{
			if( fNarrow ){
				m_BWH      = CSSTVDEM.NARROW_BWH;
				m_CenterFQ = CSSTVDEM.NARROW_CENTER + ToneOffs;
				m_HighFQ   = 2400.0 + ToneOffs;
				m_LowFQ    = CSSTVDEM.NARROW_AFCLOW + ToneOffs;
			}
			else {
				m_BWH      = 400.0;
				m_CenterFQ = 1900.0 + ToneOffs;
				m_HighFQ   = 2400.0 + ToneOffs;
				m_LowFQ    = 1000.0 + ToneOffs;
			}
			m_HighVal = (m_HighFQ - m_CenterFQ) / m_BWH;
			m_LowVal  = (m_LowFQ  - m_CenterFQ) / m_BWH;
		}

		public void Clear()
		{
			m_d      = 0;
			m_Count  = 0;
			m_ACount = 0;
			m_fq     = ZEROFQ;
			m_d      = 0;
			m_out    = 0;
			m_Timer  = m_STimer;
		}

		void SetSampFreq(double fq)
		{
			m_SampFreq = fq;
			CalcLPF();
			m_STimer = (int)SampFreq;
		}

		void CalcLPF()
		{
			m_iir.MakeIIR(m_outFC, m_SampFreq, m_outOrder, 0, 0);
			if( m_SmoozFq < 500 ) 
				m_SmoozFq = 500.0;
			m_fir.SetCount( (int)(SampFreq/m_SmoozFq) );
		}

		public double Do(double d)
		{
			double count;
			double offset;

			if( d >= 0 ){
				if( m_d < 0 ){
					count = m_Count - m_ACount;
					offset = d/(d - m_d);
					m_ACount = m_Count - offset;
					count -= offset;
					if( count >= 1.0 ){
						m_fq = m_SampFreq * 0.5 / count;
						if( m_Limit ){
							if( m_fq > m_HighFQ ){
								m_fq = m_HighVal;
							} else if( m_fq < m_LowFQ ){
								m_fq = m_LowVal;
							} else {
								m_fq -= m_CenterFQ;
								m_fq /= m_BWH;
							}
						} else {
							m_fq -= m_CenterFQ;
							m_fq /= m_BWH;
							m_Timer = m_STimer;
						}
					}
				}
			} else {
				if( m_d >= 0 ){
					count = m_Count - m_ACount;
					offset = d/(d - m_d);
					m_ACount = m_Count - offset;
					count -= offset;
					if( count >= 1.0 ){
						m_fq = m_SampFreq * 0.5 / count;
						if( m_Limit ){
							if( m_fq > m_HighFQ ){
								m_fq = m_HighVal;
							} else if( m_fq < m_LowFQ ){
								m_fq = m_LowVal;
							} else {
								m_fq -= m_CenterFQ;
								m_fq /= m_BWH;
							}
						} else {
							m_fq -= m_CenterFQ;
							m_fq /= m_BWH;
							m_Timer = m_STimer;
						}
					}
				}
			}
			if( !m_Limit && m_Timer > 0 ){
				m_Timer--;
				if( m_Timer > 0 )
					m_fq = -m_CenterFQ/m_BWH;
			}
			switch(m_Type){
				case 0:     // IIR
					m_out = m_iir.Do(m_fq);
					break;
				case 1:     // FIR
					m_out = m_fir.Avg(m_fq);
					break;
				default:    // OFF
					m_out = m_fq;
					break;
			}
			m_d = d;
			m_Count++;
			return -(m_out * 16384);
		}
	}

	class CLVL
	{
		public double m_Cur { get; protected set; }

		double m_PeakMax;
		double m_PeakAGC;
		double m_Peak;
		public double m_CurMax { get; protected set; }
		double m_Max;
		double m_agc;
		int    m_CntPeak;
		bool   m_agcfast;

		int    m_Cnt;
		int    m_CntMax;

		public CLVL( int iSampFreq, bool fAgcFast = false ){
			m_agcfast = fAgcFast;
			m_CntMax = (int)(iSampFreq * 100 / 1000.0);
			Init();
		}

		void Init(){
			m_PeakMax = 0;
			m_PeakAGC = 0;
			m_Peak = 0;
			m_Cur = 0;
			m_CurMax = 0.0;
			m_Max = 0;
			m_agc = 1.0;
			m_CntPeak = 0;
			m_Cnt = 0;
		}

		public void Do(double d ){
			m_Cur = d;
			if( d < 0.0 ) d = -d;
			if( m_Max < d ) m_Max = d;
			m_Cnt++;
		}

		void Fix(){
			if( m_Cnt < m_CntMax ) return;	// did not store yet
			m_Cnt = 0;
			m_CntPeak++;
			if( m_Peak < m_Max ) m_Peak = m_Max;
			if( m_CntPeak >= 5 ){
				m_CntPeak = 0;
				m_PeakMax = m_Max;
				m_PeakAGC = (m_PeakAGC + m_Max) * 0.5;
				m_Peak = 0;
				if( !m_agcfast ){
					if( (m_PeakAGC > 32) && m_PeakMax != 0 ){
						m_agc = 16384.0 / m_PeakMax;
					}
					else {
						m_agc = 16384.0 / 32.0;
					}
				}
			} else {
				if( m_PeakMax < m_Max ) m_PeakMax = m_Max;
			}
			m_CurMax = m_Max;
			if( m_agcfast ){
				if( m_CurMax > 32 ){
					m_agc = 16384.0 / m_CurMax;
				}
				else {
					m_agc = 16384.0 / 32.0;
				}
			}
			m_Max = 0;
		}

		public double AGC(double d){
			return d * m_agc;
		}
	}

	public class CSmooz{
		double [] bp;
		int       Wp;

		public int Max { get; protected set; }
		public int Cnt { get; protected set; }

		void IncWp(){
			Wp++;
			if( Wp >= Max )
				Wp = 0;
		}

		double Avg(){
			double	d = 0.0;
			int		i;
			for( i = 0; i < Cnt; i++ ){
				d += bp[i];
			}
			if( Cnt != 0 ){
				return d/(double)(Cnt);
			} else {
				return 0;
			}
		}

		public CSmooz(int max = 2){
			Max = max;
			bp  = new double[max];
			Cnt = 0;
			Wp  = 0;
		}

		public void SetCount(int n){
			if( n == 0 ) 
				n = 1;

			if( Max < n ) {
				bp  = new double[n];
				Max = n;
			}
			Cnt = Wp = 0;
		}

		public double SetData(double d){
			for( int i = 0; i < Max; i++ ){
				bp[i] = d;
			}
			Wp  = 0;
			Cnt = Max;
			return d;
		}

		public double Avg(double d){
			bp[Wp] = d;
			IncWp();
			if( Cnt < Max ){
				Cnt++;
			}
			return Avg();
		}
	}

	/// <summary>
	/// This one looks reusable since I pass the CSSTVSET as a param
	/// to the resetting methods.
	/// </summary>
	class CSYNCINT {
		static readonly int MSYNCLINE = 8;

		UInt32[] m_MSyncList = new UInt32[MSYNCLINE];

		public UInt32 m_MSyncCnt    { get; protected set; }
		public UInt32 m_MSyncACnt   { get; protected set; }
		public int	  m_MSyncTime   { get; protected set; }
		public UInt32 m_MSyncIntPos { get; protected set; }
		public int    m_MSyncIntMax { get; protected set; }
		public bool   m_fNarrow     { get; protected set; }
		public int    m_SyncPhase   { get; set; }

		public CSYNCINT(){
			m_fNarrow = false;
			Reset();
		}

		public void Reset( bool fNarrow = false ) {
			Array.Clear( m_MSyncList, 0, m_MSyncList.Length );

			m_fNarrow     = fNarrow;

			m_MSyncACnt   = m_MSyncCnt = 0;
			m_MSyncIntMax = 0;
			m_MSyncIntPos = 0;
			m_SyncPhase   = 0;
		}

		bool SyncCheckSub( CSSTVSET SSTVSET, AllModes am)
		{
			int i = MSYNCLINE-1;
			int e;

			switch(am){
				case AllModes.smSC2_60:
				case AllModes.smSC2_120:
					return false;
				case AllModes.smR24:
				case AllModes.smR36:
				case AllModes.smMRT2:
				case AllModes.smPD50:
				case AllModes.smPD240:
					if( m_fNarrow ) 
						return false;
					e = MSYNCLINE - 4;
					break;
				case AllModes.smRM8:
				case AllModes.smRM12:
					if( m_fNarrow ) 
						return false;
					e = 0;
					break;
				case AllModes.smMN73:
				case AllModes.smMN110:
				case AllModes.smMN140:
				case AllModes.smMC110:
				case AllModes.smMC140:
				case AllModes.smMC180:
					if( !m_fNarrow ) 
						return false;
					e = MSYNCLINE - 5;
        			break;
				default:
					if( m_fNarrow ) 
						return false;
					e = MSYNCLINE - 3;
					break;
			}
			UInt32 deff = (UInt32)(3 * SSTVSET.m_SampFreq / 1000.0); // Was global SampFreq
			UInt32 cml	= SSTVSET.m_MS[(int)am];
			UInt32 cmh	= cml + deff;
			cml -= deff;
			UInt32 w;
			for( i--; i >= e; i-- ){
				w = m_MSyncList[i];
				int f = 0;
				if( w > SSTVSET.m_MSL ){
					if( m_fNarrow ){
						for( UInt32 k = 1; k <= 2; k++ ){
							UInt32 ww = w / k;
							if( (ww > cml) && (ww < cmh) ) f = 1;
						}
					}
					else {
						for( UInt32 k = 1; k <= 3; k++ ){
							UInt32 ww = w / k;
							if( (ww > cml) && (ww < cmh) ) f = 1;
						}
					}
				}
				if( f == 0 )
					return false;
			}
			return true;
		}

		/// <returns>Used to add 1 plus the AllModes value or 0.</returns>
		/// <remarks>This is seriously lame use of a return code. Either hack the enumeration
		/// so that zero is not one of the valid modes, or make a return value and pass 
		/// an out variable to return the mode.</remarks>
		bool SyncCheck( CSSTVSET oTVSet, out AllModes oMode )
		{
			UInt32 deff = (UInt32)(3 * oTVSet.m_SampFreq / 1000.0);
			UInt32 w = m_MSyncList[MSYNCLINE-1];

			for( UInt32 k = 1; k <= 3; k++ ){
				UInt32 ww = w / k;
				if( (ww > oTVSet.m_MSL) && (ww < oTVSet.m_MSH) ){
					for( int i = 0; i < (int)AllModes.smEND; i++ ){
						if( oTVSet.m_MS[i] != 0 && (ww > (oTVSet.m_MS[i]-deff)) && (ww < (oTVSet.m_MS[i]+deff)) ){
							if( SyncCheckSub(oTVSet, (AllModes)i) ){
								oMode = (AllModes)i;
								return true;
							}
						}
					}
				} else {
					break;
				}
			}
			oMode = AllModes.smEND;
			return false;
		}

		public void SyncInc()
		{
			m_MSyncCnt++;
		}

		public void SyncTrig(int d)
		{
			m_MSyncIntMax = d;
			m_MSyncIntPos = m_MSyncCnt;
		}

		public void SyncMax(int d)
		{
			if( m_MSyncIntMax < d ){
				m_MSyncIntMax = d;
				m_MSyncIntPos = m_MSyncCnt;
			}
		}

		/// <remarks>Fix this to return a bool and have an out param for AllMode.</remarks>
		/// <param name="oTVSettings"></param>
		/// <returns></returns>
		public bool SyncStart( CSSTVSET oTVSettings, out AllModes ss )
		{
			if( m_MSyncIntMax != 0 ){
				if( (m_MSyncIntPos - m_MSyncACnt) > oTVSettings.m_MSLL ){
					m_MSyncACnt = m_MSyncIntPos - m_MSyncACnt;
					//memcpy(m_MSyncList, &m_MSyncList[1], sizeof(int) * (MSYNCLINE - 1));
					Array.Copy( m_MSyncList, 1, m_MSyncList, 0, MSYNCLINE - 1 );
					m_MSyncList[MSYNCLINE - 1] = m_MSyncACnt;
					if( m_MSyncACnt > oTVSettings.m_MSL ){
						return SyncCheck( oTVSettings, out ss );
					}
					m_MSyncACnt = m_MSyncIntPos;
				}
				m_MSyncIntMax = 0;
			}
			ss = AllModes.smEND;
			return false;
		}
	}

	public enum FreqDetect {
		PLL = 0,
		FQC,
		Hilbert
	}

	public delegate void NextMode( SSTVMode tvMode );

	public struct SyncCoordinate {
		public int Start { get; private set; }
		public int Width { get; private set; }

		public SyncCoordinate( int iStart, int iWidth ) {
			Start = iStart;
			Width = iWidth;
		}
	}

	/// <summary>
	/// So my SSTVMode object is a bit different than the CSSTVSET one. I have a different mode object per
	/// TV format. CSSTVSET changes it's state depending on which format it is re-initialized to. Thus,
	/// the Mode gets assigned every time a new image comes down.
	/// </summary>
	public class CSSTVDEM : IEnumerable<SSTVMode> {
		public SYSSET   sys  { get; protected set; }
		public SSTVMode Mode { get; protected set; } 
		public CSSTVSET SstvSet { get; protected set; }

		protected Dictionary<byte, SSTVMode > ModeDictionary { get; } = new Dictionary<byte, SSTVMode>();

		public event NextMode ShoutNextMode; // This will need to be an message.

		public readonly static int NARROW_SYNC		= 1900;
		public readonly static int NARROW_LOW		= 2044;
		public readonly static int NARROW_HIGH		= 2300;
		public readonly static int NARROW_CENTER	= ((NARROW_HIGH+NARROW_LOW)/2);
		public readonly static int NARROW_BW		= (NARROW_HIGH - NARROW_LOW);
		public readonly static int NARROW_BWH		= (NARROW_BW/2);
		public readonly static int NARROW_BPFLOW	= 1600;
		public readonly static int NARROW_BPFHIGH	= 2500;
		public readonly static int NARROW_AFCLOW	= 1800;
		public readonly static int NARROW_AFCHIGH	= 1950;

		public readonly static int TAPMAX = 512; // BUG: Move to Fir or Fir2 later.
		readonly static int SSTVDEMBUFMAX = 24;

		readonly double[]  HBPF  = new double[TAPMAX+1];
		readonly double[]  HBPFS = new double[TAPMAX+1];
		readonly double[]  HBPFN = new double[TAPMAX+1];

		readonly CFIR2 m_BPF = new CFIR2();

		double   m_ad;
		int      m_OverFlow;
		BandPass m_bpf = BandPass.Undefined;
		int      m_bpftap;

		readonly FreqDetect m_Type      = FreqDetect.FQC; // BUG: This s/b parameter.
		readonly bool       m_LevelType = false; // TODO: Probably sb param too. If make true, you must implement CSLVL class.

		readonly CIIRTANK m_iir11;
		readonly CIIRTANK m_iir12;
		readonly CIIRTANK m_iir13;
		readonly CIIRTANK m_iir19;
		readonly CIIRTANK m_iirfsk;
		readonly CIIR     m_lpf11;
		readonly CIIR     m_lpf12;
		readonly CIIR     m_lpf13;
		readonly CIIR     m_lpf19;
		readonly CIIR     m_lpffsk;
		readonly CLVL     m_lvl;
		readonly CSLVL    m_SyncLvl = new CSLVL();

		// These three should inherit from a common interface.
		readonly CPLL	  m_pll;
		readonly CFQC     m_fqc;
		readonly CHILL    m_hill;

		public bool   m_Sync { get; protected set; }
		int           m_SyncMode;
		int           m_SyncTime;
		int           m_VisData;
		int           m_VisCnt;
		int           m_Skip;
		readonly bool m_SyncRestart;
		readonly int  m_SyncErr;
		AllModes      m_NextMode;

		readonly protected List<SyncCoordinate> _rgSyncDetect = new List<SyncCoordinate>(256); // Dup of the one in TmmSSTV for a bit.
		protected int     m_SyncHit, m_SyncLast;

	  //         int m_wLine;                         // Only used by the old SyncSSTV call. Might remove later.
      //public   int  m_wPage { get; protected set; } // This determines WHEN we read into the Rx & R12 buffers. Where the writer is.
	  //public   int  m_rPage { get; protected set; } // this determines WHEN we read from the Rx & R12 buffers. Where the reader is.
		protected int m_wCnt;                         // How far along on a the scan line we are receiving the image.

		// Base pointer represent how far along in samples over the entire image we've gone. 
		public    int    m_wBase { get; protected set; } // Write pos in samples stream. Moves forward by scanlinewidthinsamples chunks. Always < size of buffer.
		public    double m_rBase { get; protected set; } // Read  pos in samples stream, Moves forward by scanlinewidthinsamples chunks. Entire image scanlines.

	    public bool  m_ReqSave  { get; protected set; }
		public bool  m_Lost     { get; protected set; }

		public short[] m_Buf;
		public short[] m_B12;

		readonly int  m_SenseLvl;
		public double m_SLvl { get; protected set; }
		double      m_SLvl2;
		double      m_SLvl3;

		bool        m_ScopeFlag;
		readonly CScope[]    m_Scope = new CScope[2];

		CTICK       pTick;
		int         m_Tick;
		int         m_TickFreq;

		// More goodies that can probably live on their own class.
		double      m_CurSig; // Not sure who uses this, or why it's there.
		readonly CSmooz m_Avg    = new CSmooz();
		readonly CSmooz m_AFCAVG = new CSmooz();
		readonly bool   m_afc;
		int         m_AFCCount;
		double      m_AFCData;
		double      m_AFCLock;
		double      m_AFCDiff;
		int         m_AFCFQ;
		int         m_AFCFlag;
		int         m_AFCGard;
		int         m_AFCDis;
		readonly int m_AFCInt;

		double		m_AFC_LowVal;		// (Center - SyncLow ) * 16384 / BWH
		double		m_AFC_HighVal;		// (Center - SyncHigh) * 16384 / BWH
		double		m_AFC_SyncVal;		// (Center - Sync    ) * 16384 / BWH
		double		m_AFC_BWH;			// BWH / 16384.0;

		//double		m_AFC_OFFSET;		// 128

	  //readonly bool       m_MSync;
		readonly CSYNCINT   m_sint1 = new CSYNCINT();
		readonly CSYNCINT   m_sint2 = new CSYNCINT();
		readonly CSYNCINT	m_sint3 = new CSYNCINT();

		readonly static int FSKGARD   = 100;
		readonly static int FSKINTVAL = 22;
		readonly static int FSKSPACE  = 2100;

		// This set of goodies could probably to on it's own struct or class.
		readonly bool m_fskdecode;
		int         m_fskrec;
		int         m_fskmode;
		int         m_fsktime;
		int         m_fskcnt; // This gets used between m_fskdata & m_fskNRS 
		int         m_fskbcnt;
		int         m_fsknexti;
		double      m_fsknextd;
		byte        m_fsks;
		byte        m_fskc;
		readonly List<byte>  m_fskdata = new List<byte>(20);
		readonly List<char>  m_fskcall = new List<char>(20);
		int			m_fskNRrec;
		int			m_fskNR;
		readonly List<char>	m_fskNRS  = new List<char>(20);

		bool        m_fNarrow = false;

		//double  m_d;
		//double  m_dd;
		//int     m_n;

		// BUG: See if I can get these from CSSTVSET
		public int SampFreq { get; }
		public int SampBase { get; }
		// This is a global in the original code, but it's pretty clear
		// that we can't handle it changing mid run. So now it's a readonly
		// member variable.
		readonly double m_dblToneOffset;

		public CSSTVDEM( CSSTVSET p_oSSTVSet, SYSSET p_sys, int iSampFreq, int iSampBase, double dbToneOffset ) {
			sys     = p_sys      ?? throw new ArgumentNullException( "sys must not be null." );
			SstvSet = p_oSSTVSet ?? throw new ArgumentNullException( "CSSTVSSET must not be null" );

			SampFreq        = iSampFreq;
			SampBase        = iSampBase;
			m_dblToneOffset = dbToneOffset;

			foreach( SSTVMode oMode in this ) {
				ModeDictionary.Add( oMode.VIS, oMode );
			}

			m_ad  = 0;

			// Our buffer only holds SSTVDEMBUFMAX (24) lines. 
			int iBufWidth = (int)(1400 * SampFreq / 1000.0 ); // samples width (MAX).
			int iBufSize  = SSTVDEMBUFMAX * iBufWidth;
			m_Buf = new short[iBufSize];
			m_B12 = new short[iBufSize];

		  //Array.Clear( m_Buf, 0, m_Buf.Length );
		  //Array.Clear( m_B12, 0, m_Buf.Length );

			m_fqc  = new CFQC( SampFreq, dbToneOffset );
			m_hill = new CHILL( sys, SampFreq, SampBase, dbToneOffset );

			m_pll = new CPLL( SampFreq, dbToneOffset );
			m_pll.SetVcoGain ( 1.0 );
			m_pll.SetFreeFreq( 1500, 2300 );
			m_pll.MakeLoopLPF( iLoopOrder:1, iLoopFreq:1500 );
			m_pll.MakeOutLPF ( iLoopOrder:3, iLoopFreq: 900 );

			Array.Clear( HBPF,  0, HBPF .Length );
			Array.Clear( HBPFS, 0, HBPFS.Length );
			Array.Clear( HBPFN, 0, HBPFN.Length );
			SetBPF( BandPass.Wide );
			//CalcBPF();

			m_iir11  = new CIIRTANK( iSampFreq );
			m_iir12  = new CIIRTANK( iSampFreq );
			m_iir13  = new CIIRTANK( iSampFreq );
			m_iir19  = new CIIRTANK( iSampFreq );
			m_iirfsk = new CIIRTANK( iSampFreq );

			m_lpf11  = new CIIR();
			m_lpf12  = new CIIR();
			m_lpf13  = new CIIR();
			m_lpf19  = new CIIR();
			m_lpffsk = new CIIR();

			m_iir11 .SetFreq(1080     + m_dblToneOffset, SampFreq,  80.0);
			m_iir12 .SetFreq(1200     + m_dblToneOffset, SampFreq, 100.0);
			m_iir13 .SetFreq(1320     + m_dblToneOffset, SampFreq,  80.0);
			m_iir19 .SetFreq(1900     + m_dblToneOffset, SampFreq, 100.0);
			m_iirfsk.SetFreq(FSKSPACE + m_dblToneOffset, SampFreq, 100.0);

			m_lpf11 .MakeIIR(50, SampFreq, 2, 0, 0);
			m_lpf12 .MakeIIR(50, SampFreq, 2, 0, 0);
			m_lpf13 .MakeIIR(50, SampFreq, 2, 0, 0);
			m_lpf19 .MakeIIR(50, SampFreq, 2, 0, 0);
			m_lpffsk.MakeIIR(50, SampFreq, 2, 0, 0);

			m_wBase     = 0;
			m_wCnt      = 0;
			m_rBase     = 0;
			m_Skip      = 0;
			m_Sync      = false;
			m_SyncMode  = 0;
			m_ScopeFlag = false;
			m_Lost      = false;

			m_lvl = new CLVL( SampFreq, fAgcFast:true );
			m_afc = true;

			m_Tick   = 0;
			pTick    = null;
			m_Avg   .SetCount( (int)( 2.5*SampFreq/1000.0 ));
			m_AFCAVG.SetCount(15);
			m_AFCFQ  = 0;
			m_AFCInt = (int)(100 * SampFreq / 1000.0 );
			m_AFCDis = 0;

			m_sint1.Reset();
			m_sint2.Reset();
			m_sint3.Reset( fNarrow:true);

			m_SyncRestart = true;

			m_SenseLvl = 1;
			SetSenseLvl();

		    m_ReqSave   = false;

			m_fskrec    = 0;
			m_fskNRrec  = 0;
			m_fskdecode = false;
			m_fskmode   = 0;
		}

		public void Dispose() {
			m_B12 = null;
			m_Buf = null;
		}

		/// <summary>
		/// Moved from TmmSSTV. Note that ip == sp in the case of index offset and NOT short* to array.
		/// </summary>
		public void StorePage( int ip ) {
			//short *ip = &m_Buf[m_rPage * m_BWidth];
			//short *sp = &m_B12[m_rPage * m_BWidth];
			//if( dp.m_StgBuf ){
			//	if( ((dp.m_wStgLine + 1) * SSTVSET.m_WD) < dp.m_RxBufAllocSize ){
			//		memcpy(&dp->m_StgBuf[dp->m_wStgLine * SSTVSET.m_WD], ip, SSTVSET.m_WD*sizeof(short));
			//		memcpy(&dp->m_StgB12[dp->m_wStgLine * SSTVSET.m_WD], sp, SSTVSET.m_WD*sizeof(short));
			//		dp.m_wStgLine++;
			//		if( dp.m_wStgLine == 16 )
            //            UpdateSBTO();
			//	}
			//}
			//else if( WaveStg.IsOpen() ){
			//	WaveStg.Write(ip, SSTVSET.m_WD*sizeof(short));
			//	WaveStg.Write(sp, SSTVSET.m_WD*sizeof(short));
			//	dp.m_wStgLine++;
			//	if( dp->m_wStgLine == 16 )
            //        UpdateSBTO();
			//}
		}

		public void CalcBPF(double[] H1, double[] H2, double[] H3, ref int bpftap, BandPass bpf)
		{
			int lfq  = (int)((m_SyncRestart ? 1100 : 1200) + m_dblToneOffset );
			int lfq2 = (int)(400 + m_dblToneOffset );
			if( lfq2 < 50 ) 
				lfq2 = 50;
			switch(bpf){
				case BandPass.Wide:
					bpftap = (int)(24 * SampFreq / 11025.0 );
					CFIR2.MakeFilter(H1, bpftap, FirFilt.ffBPF, SampFreq,  lfq, 2600 + m_dblToneOffset, 20, 1.0);
					CFIR2.MakeFilter(H2, bpftap, FirFilt.ffBPF, SampFreq, lfq2, 2500 + m_dblToneOffset, 20, 1.0);
		//			MakeFilter(H3, bpftap, ffBPF, SampFreq,  NARROW_BPFLOW-200, NARROW_BPFHIGH, 20, 1.0);
					break;
				case BandPass.Narrow:
					bpftap = (int)(64 * SampFreq / 11025.0 );
					CFIR2.MakeFilter(H1, bpftap, FirFilt.ffBPF, SampFreq,  lfq, 2500 + m_dblToneOffset, 40, 1.0);
					CFIR2.MakeFilter(H2, bpftap, FirFilt.ffBPF, SampFreq, lfq2, 2500 + m_dblToneOffset, 20, 1.0);
		//			MakeFilter(H3, bpftap, ffBPF, SampFreq, NARROW_BPFLOW-100, NARROW_BPFHIGH, 40, 1.0);
					break;
				case BandPass.VeryNarrow: 
					bpftap = (int)(96 * SampFreq / 11025.0 );
					CFIR2.MakeFilter(H1, bpftap, FirFilt.ffBPF, SampFreq,  lfq, 2400 + m_dblToneOffset, 50, 1.0);
					CFIR2.MakeFilter(H2, bpftap, FirFilt.ffBPF, SampFreq, lfq2, 2500 + m_dblToneOffset, 20, 1.0);
		//			MakeFilter(H3, bpftap, ffBPF, SampFreq,  NARROW_BPFLOW, NARROW_BPFHIGH, 50, 1.0);
					break;
				default:
					bpftap = 0;
					break;
			}
		  //CalcNarrowBPF(H3, bpftap, bpf, SSTVSET.m_Mode); If I add those modes I'll figure this out.
		}

		public void CalcNarrowBPF(double[] H3, int bpftap, BandPass bpf, AllModes mode) {
			int low, high;
			switch(mode){
				case AllModes.smMN73:
					low = 1600; high = 2500;
					break;
				case AllModes.smMN110:
					low = 1600; high = 2500;
        			break;
				case AllModes.smMN140:
					low = 1700; high = 2400;
        			break;
				case AllModes.smMC110:
					low = 1600; high = 2500;
        			break;
				case AllModes.smMC140:
					low = 1650; high = 2500;
        			break;
				case AllModes.smMC180:
					low = 1700; high = 2400;
        			break;
				default:
					low = 1600; high = 2500;
        			break;
			}
			low  += (int)m_dblToneOffset;
			high += (int)m_dblToneOffset;
			switch(bpf){
				case BandPass.Wide:
					CFIR2.MakeFilter(H3, bpftap, FirFilt.ffBPF, SampFreq,  low-200, high, 20, 1.0);
					break;
				case BandPass.Narrow:
					CFIR2.MakeFilter(H3, bpftap, FirFilt.ffBPF, SampFreq, low-100, high, 40, 1.0);
					break;
				case BandPass.VeryNarrow:
					CFIR2.MakeFilter(H3, bpftap, FirFilt.ffBPF, SampFreq,  low, high, 50, 1.0);
					break;
				default:
					break;
			}
		}

		public void CalcBPF() {
			CalcBPF(HBPF, HBPFS, HBPFN, ref m_bpftap, m_bpf );
			m_BPF.Create(m_bpftap);
		}

		/// <summary>
		/// This looks it can help with image offset issue but it only
		/// looks like skip is only called via mouse operations or if this
		/// function is called on setup.
		/// </summary>
		/// <param name="bpf">1:wide, 2:narrow, 3:very narrow</param>
		void SetBPF(BandPass bpf) {
			if( bpf != m_bpf ){
				m_bpf = bpf;

				int delay = m_bpftap;
				CalcBPF();
				if( m_Sync ){
					delay = (m_bpftap - delay) / 2;
					m_Skip = delay;
				}
			}
		}

		void Idle(double d)	{
			if( !sys.m_TestDem ) 
				m_lvl.Do(d);
		}

		void SetTickFreq(int f)	{
			if( f == 0 ) 
				f = 1200;

			m_iir12.SetFreq(f + m_dblToneOffset, SampFreq, 100.0);
			m_TickFreq = f;
		}

		void InitAFC(){
			m_AFCAVG.SetCount(m_AFCAVG.Max);
			if( m_fNarrow ){
				m_AFCData = m_AFCLock = (NARROW_CENTER-NARROW_SYNC)*16384/NARROW_BWH;
			} else {
				m_AFCData = m_AFCLock = ((1900-1200)*16384)/400.0;
			}
			m_AFCFlag = 0;
			m_AFCDiff = 0.0;
			m_AFCGard = 10;
			m_AFCCount = 0;
			m_AFCDis = 0;
			InitTone(0);
			if( m_fNarrow ){
				m_AFC_LowVal  = (NARROW_CENTER - NARROW_AFCLOW ) * 16384.0 / NARROW_BWH;	// (Center - SyncLow) * 16384 / BWH
				m_AFC_HighVal = (NARROW_CENTER - NARROW_AFCHIGH) * 16384.0 / NARROW_BWH;	// (Center - SyncHigh) * 16384 / BWH
				m_AFC_SyncVal = (NARROW_CENTER - NARROW_SYNC   ) * 16384.0 / NARROW_BWH;	// (Center - Sync) * 16384 / BWH
				m_AFC_BWH = NARROW_BWH / 16384.0;											// BWH / 16384.0;
				if( sys.m_bCQ100 ) {
					m_AFC_LowVal  = (NARROW_CENTER - NARROW_SYNC - 50) * 16384.0 / NARROW_BWH;	// (Center - SyncLow) * 16384 / BWH
					m_AFC_HighVal = (NARROW_CENTER - NARROW_SYNC + 50) * 16384.0 / NARROW_BWH;	// (Center - SyncHigh) * 16384 / BWH
				}
			} else {
				m_AFC_LowVal  = (1900 - 1000) * 16384.0 / 400;	// (Center - SyncLow ) * 16384 / BWH
				m_AFC_HighVal = (1900 - 1325) * 16384.0 / 400;	// (Center - SyncHigh) * 16384 / BWH
				m_AFC_SyncVal = (1900 - 1200) * 16384.0 / 400;	// (Center - Sync    ) * 16384 / BWH
				m_AFC_BWH = 400 / 16384.0;						// BWH / 16384.0;
				if( sys.m_bCQ100 ) {
					m_AFC_LowVal  = (1900 - 1200 - 50) * 16384.0 / 400;	// (Center - SyncLow) * 16384 / BWH
					m_AFC_HighVal = (1900 - 1200 + 50) * 16384.0 / 400;	// (Center - SyncHigh) * 16384 / BWH
				}
			}
		}

		void InitTone(int dfq) {
			if( m_AFCFQ != dfq ){
				m_iir11.SetFreq(1080+dfq + m_dblToneOffset, SampFreq, 80.0);
				m_iir12.SetFreq(1200+dfq + m_dblToneOffset, SampFreq, 100.0);
				m_iir13.SetFreq(1320+dfq + m_dblToneOffset, SampFreq, 80.0);
				m_iir19.SetFreq(1900+dfq + m_dblToneOffset, SampFreq, 100.0);
				m_iirfsk.SetFreq(FSKSPACE+dfq + m_dblToneOffset, SampFreq, 100.0);
				m_AFCFQ = dfq;
			}
		}

		void SetWidth( bool fNarrow) {
			if( m_fNarrow != fNarrow ){
				m_fNarrow = fNarrow;
				m_hill.SetWidth( sys, fNarrow);
    			m_fqc .SetWidth(fNarrow);
				m_pll .SetWidth(fNarrow);
			}
		}

		public void PrepDraw( bool fLoopBack ) {
			m_fskcall.Clear();

			m_ReqSave  = false;
		}

		/// <summary>
		/// this method gets called when we are ready to rock and roll. In the original
		/// system TmmSSTV would toss it's previous image and start a new one.
		/// In the new system I'll try tossing TmmSSTV and create a new one.
		/// </summary>
		public void Start( SSTVMode tvMode ) {
			Mode = tvMode;
			SetWidth(CSSTVSET.IsNarrowMode( tvMode ));

			InitAFC();
			m_fqc.Clear();
			m_SyncMode = -1; // Here and then...
			m_Sync  = false;
			m_Skip  = 0;
			m_wBase = 0;
			m_wCnt  = 0;
			m_rBase = 0;
		  //OpenCloseRxBuff();
			m_Lost  = false;
			m_SyncHit       = -1;
			m_SyncLast		= 0;

			m_Sync     = true;
			m_SyncMode = 0; // Here? This kills me. Probably due to multi threaded stuff.
			// However, this combo makes sense. We go back for looking for sync signals at the
			// same time we're storing the image scan lines.
			SetWidth(m_fNarrow);

			ShoutNextMode?.Invoke( tvMode );
			// Don't support narrow band modes.
			//if( m_fNarrow ) 
			//	CalcNarrowBPF(HBPFN, m_bpftap, m_bpf, SSTVSET.m_Mode);
		}

		//void Start(SSTVMode tvMode, int f)	{
		//	m_fqc.Clear();
		//	m_sint1.Reset();
		//	m_sint2.Reset();
		//	m_sint3.Reset();
		//	m_wBgn     = 0;
		//	m_rBase    = 0;
		//	m_SyncMode = 0;
		//	SSTVSET.SetMode(tvMode);
		//	m_Sync     = false;
		//	SetWidth(CSSTVSET.IsNarrowMode(tvMode));

		//	if( f != 0 ){
		//		Start( tvMode );
		//	} else {
		//		m_SyncMode = -1;
		//	}
		//}

		public void Stop()	{
			if( m_AFCFQ != 0 ){
				if( m_fskdecode ){
					m_iir11.SetFreq(1080 + m_dblToneOffset, SampFreq,  80.0);
					m_iir12.SetFreq(1200 + m_dblToneOffset, SampFreq, 100.0);
					m_iir13.SetFreq(1320 + m_dblToneOffset, SampFreq,  80.0);
				} else {
					InitTone(0);
				}
			}
			m_fqc  .Clear();
			m_sint1.Reset();
			m_sint2.Reset();
			m_sint3.Reset();

			m_SyncMode = 512;
			m_Sync     = false;

			m_Skip = 0;
			SetWidth( false );
		}

		void SetSenseLvl() {
			switch(m_SenseLvl){
				case 1:
					m_SLvl = 3500;
					m_SLvl2 = m_SLvl * 0.5;
					m_SLvl3 = 5700;
					break;
				case 2:
					m_SLvl = 4800;
					m_SLvl2 = m_SLvl * 0.5;
					m_SLvl3 = 6800;
					break;
				case 3:
					m_SLvl = 6000;
					m_SLvl2 = m_SLvl * 0.5;
					m_SLvl3 = 8000;
					break;
				default:
					m_SLvl = 2400;
					m_SLvl2 = m_SLvl * 0.5;
					m_SLvl3 = 5000;
					break;
			}
		}

		public void Do(double s) {
			if( (s > 24578.0) || (s < -24578.0) ){
				m_OverFlow = 1; // The grapher probably clears this.
			}
			double d = (s + m_ad) * 0.5;    // LPF
			m_ad = s;
			if( m_bpf != 0 ){
				if( m_Sync || (m_SyncMode >= 3) ){
					// BUG: Double check this _ stuff.
					// We don't support narrow band modes.
					d = m_BPF.Do( /* m_fNarrow ? HBPFN : */ HBPF, ref d, out _ );
				} else {
					d = m_BPF.Do( HBPFS, ref d, out _ );
				}
			}
			m_lvl.Do(d);
			double ad = m_lvl.AGC(d);

			d = ad * 32;
			if( d >  16384.0 ) 
				d =  16384.0;
			if( d < -16384.0 ) 
				d = -16384.0;

			double d11;
			double d12;
			double d13;
			double d19;
			double dsp;

			d12 = m_iir12.Do(d);
			if( d12 < 0.0 ) 
				d12 = -d12;
			d12 = m_lpf12.Do(d12);

			d19 = m_iir19.Do(d);
			if( d19 < 0.0 ) 
				d19 = -d19;
			d19 = m_lpf19.Do(d19);

			dsp = m_iirfsk.Do(d);
			if( dsp < 0.0 ) 
				dsp = -dsp;
			dsp = m_lpffsk.Do(dsp);

			// DecodeFSK( (int)d19, (int)dsp );

			if( m_fNarrow ){
				if( m_ScopeFlag ){
					m_Scope[0].WriteData(d19);
				}
				if( m_LevelType ) 
					m_SyncLvl.Do(d19);
			} else {
				if( m_ScopeFlag ){
					m_Scope[0].WriteData(d12);
				}
				if( m_LevelType ) 
					m_SyncLvl.Do(d12);
			}
			if( m_Tick != 0 && (pTick != null) ){
				pTick.Write(d12);
				return;
			}

			if( !m_Sync || m_SyncRestart ){
				SSTVMode tvMode;
				m_sint1.SyncInc();
				m_sint2.SyncInc();
				m_sint3.SyncInc();

				// The only time we care about this one is in VIS.
				d11 = m_iir11.Do(d);
				if( d11 < 0.0 )
					d11 = -d11;
				d11 = m_lpf11.Do(d11);

				switch(m_SyncMode){
					case 0:                 // 自動開始 : Start automatically
						// The first 1900hz has been seen, and now we're going down to 1200 for 15 ms. (s/b 10)
						if( (d12 > d19) && (d12 > m_SLvl) && ((d12-d19) >= m_SLvl) ){
							m_SyncMode++;
							m_SyncTime = (int)(15 * sys.m_SampFreq/1000); // this is probably the ~10 ms between each 1900hz tone.
							if( !m_Sync /* && m_MSync */ ) 
								m_sint1.SyncTrig( (int)d12);
						}
						break;
					case 1:                 // 1200Hz(30ms)‚ の継続チェック: continuous check.
						if( !m_Sync /* && m_MSync */ ){
							if( (d12 > d19) && (d12 > m_SLvl2) && ((d12-d19) >= m_SLvl2) ){
								m_sint2.SyncMax( (int)d12);
							}
						}
						// the second 1900hz has been seen now down to 1200hz again for 30ms.
						if( (d12 > d19) && (d12 > m_SLvl) && ((d12-d19) >= m_SLvl) ){
							if( !m_Sync /* && m_MSync */ ){
								m_sint1.SyncMax( (int)d12);
							}
							m_SyncTime--;
							if( m_SyncTime == 0 ){
								m_SyncMode++;
								m_SyncTime = (int)(30 * sys.m_SampFreq/1000); // Each bit is 30 ms!!
								m_VisData  = 0; // Init value
								m_VisCnt   = 8; // Start counting down the 8 bits, (after 30ms).
							}
						} else {
							m_SyncMode = 0;
						}
						break;
					case 2:                 // Vis decode
					case 9:                 // Expanded VIS decode.
						d13 = m_iir13.Do(d);
						if( d13 < 0.0 ) 
							d13 = -d13;
						d13 = m_lpf13.Do(d13);
						m_SyncTime--;
						if( m_SyncTime == 0 ){
							if( ((d11 < d19) && (d13 < d19)) || (Math.Abs(d11-d13) < (m_SLvl2)) ) {
								m_SyncMode = 0; // Start over?
							} else {
								m_SyncTime = (int)(30 * sys.m_SampFreq/1000 ); // Get next bit.
								m_VisData = m_VisData >> 1;
								if( d11 > d13 ) 
									m_VisData |= 0x0080; // Set the 8th bit and we shift right for next.
								m_VisCnt--;
								if( m_VisCnt == 0 ){
									// Note: we've picked up the last bit to determine the VIS, but we need
									//       to walk over the 30ms STOP bit.
									if( m_SyncMode == 2 ){
										m_SyncMode++;

										if( ModeDictionary.TryGetValue((byte)m_VisData, out SSTVMode tvModeFound ) ) {
											m_NextMode = tvModeFound.LegacyMode;
											m_SyncTime += (int)(7 * sys.m_SampFreq/1000.0 ); // HACK: This fixes us for all modes. But why?
										} else {
											if( m_VisData == 0x23 ) {      // MM 拡張 VIS : Expanded (16bit) VIS!!
											//	m_SyncMode = 9;
											//	m_VisData  = 0;
											//	m_VisCnt   = 8;
											//} else {
												m_SyncMode = 0;
											}
										}
									} else {          // 拡張 VIS : Vis Expansion not supported.
										m_SyncMode = 0;
									}
								}
							}
						}
						break;
					case 3:                 // 1200Hz(30ms)‚のチェック : check. 30ms STOP bit.
						if( !m_Sync ){
							m_pll.Do(ad);
						}
						m_SyncTime--;
						if( m_SyncTime == 0 ){
							if( (d12 > d19) && (d12 > m_SLvl) ){
								if( m_Sync ){
									// Looks like we request save when we're 65% the way thru an image,
									// and then suddenly get a new image. I'd do this back when the 
									// new mode was requested.
									//if( m_rBase >= (SstvSet.m_LM * 65/100.0) ){
									//	m_ReqSave = true;
									//}
								}
								tvMode = GetSSTVMode( m_NextMode );
								if( tvMode != null ) {
									SstvSet.SetMode( tvMode );
									Start( tvMode );
								}
							} else {
								m_SyncMode = 0;
							}
						}
						break;
				}
			}
			if( m_Sync ){
				switch(m_Type){
					case FreqDetect.PLL:		// PLL
						if( m_afc && (m_lvl.m_CurMax > 16) )
							SyncFreq(m_fqc.Do(m_lvl.m_Cur));
						d = m_pll.Do(m_lvl.m_Cur);
						break;
					case FreqDetect.FQC:		// Zero-crossing
						d = m_fqc.Do(m_lvl.m_Cur);
						if( m_afc && (m_lvl.m_CurMax > 16) )
							SyncFreq(d);
						break;
					case FreqDetect.Hilbert:	// Hilbert
						d = m_hill.Do(m_lvl.m_Cur);
						if( m_afc && (m_lvl.m_CurMax > 16) )
							SyncFreq(d);
						break;
					default:
						throw new NotImplementedException( "Unrecognized Frequency Detector" );
				}
				if( m_afc ) 
					d += m_AFCDiff;
				if( m_Skip != 0 ) {
					if( m_Skip > 0 ){ // Ignore this data
						m_Skip--;
					} else {          // Pad the overshoot.
						for( ; m_Skip != 0; m_Skip++ ){
							int n = m_wBase + m_wCnt;
							m_Buf[n] = (short)-d;
							m_B12[n] = 0;
							WCntIncrement();
						}
					}
				} else {
					if( m_ScopeFlag ){
						m_Scope[1].WriteData(d);
					}
					int n = m_wCnt;
					m_Buf[n] = (short)-d;

					if( d12 > m_SLvl ) {
						m_SyncHit = m_wBase;
					}
					if( m_fNarrow ){
						m_B12[n] = (short)d19;
					} else {
						m_B12[n] = (short)d12;
					}
					WCntIncrement();
				}
			}
			else if( sys.m_TestDem ){
				// This is used by the TOptionDlg::TimerTimer code for test.
				switch(m_Type){
					case FreqDetect.PLL:
						m_CurSig = m_Avg.Avg(m_pll.Do(m_lvl.m_Cur));
						break;
					case FreqDetect.FQC:
						m_CurSig = m_Avg.Avg(m_fqc.Do(m_lvl.m_Cur));
						break;
					case FreqDetect.Hilbert:
						m_CurSig = m_Avg.Avg(m_hill.Do(m_lvl.m_Cur));
						break;
					default:
						throw new NotImplementedException( "Unrecognized Frequency Detector" );
				}
			}
		}

		/// <summary>Increment to next scan line. This is the scan line not including VIS and horiz sync.</summary>
		/// <remarks>
		/// My guess is that since we reset the m_wCnt when > m_WD we're not attempting to
		/// resync on the horizontal sync signal. Might be a nice improvement if we ever get that far.
		/// </remarks>
		protected void WCntIncrement() {
			m_wCnt ++;      // This is the only place we bump up the (x) position along the frequency scan line.
			m_wBase++;
			if( m_wCnt >= m_Buf.Length ){ 
				m_wCnt = 0;
			}
		}

		/// <summary>
		/// Bump up our page read position.
		/// </summary>
		/// <param name="dbWidthInSamples">read page increment by samples in double precision.</param>
		/// <remarks>It is hyper critical that we count the samples in floating point!! Else as in 
		/// all cases, the error per scan builds up and we begin to slant!!</remarks>
		/// <seealso cref="CSSTVMOD.Write(int, uint, double)"/>
		public void PageRIncrement( double dbWidthInSamples ) {
			m_rBase    += dbWidthInSamples;
			m_SyncLast = m_SyncHit;
			m_SyncHit  = -1;
		}

		/// <remarks>This method used to live on the TMmsstv object, but that doesn't
		/// make sense there. Moved it to the demodulator.</remarks>
		/// <param name="fSyncAccuracy">fSyncAccuracy was m_SyncAccuracy on TMmsstv</param>
		/// <remarks>I see two ways to correct slant. MMSSTV has the CSTVSET values static
		/// so they must change the page width. Since they were using for the distance
		/// along the scan line (ps = n%width) they then get corrected.
		/// I'm going to change my parse values by updated a copy of the mode.</remarks>
		//public void SyncSSTV( int iSyncAccuracy )
		//{
		//	int e = 4;
		//	if( iSyncAccuracy != 0 && sys.m_UseRxBuff != 0 && (Mode.ScanLineWidthInSamples >= SstvSet.m_SampFreq) ) 
		//		e = 3;
		//	if( m_wLine >= e ) {
		//		int    n = 0;
		//		int   wd = (int)(Mode.ScanLineWidthInSamples + 2);
		//		int[] bp = new int[wd];

		//		Array.Clear( bp, 0, bp.Length );
		//		//memset(bp, 0, sizeof(int)*(wd));
		//		for( int pg = 0; pg < e; pg++ ){
		//		  //short []sp = &m_B12[pg * m_BWidth];
		//			int     ip = pg * m_BWidth;
		//			for( int i = 0; i < Mode.ScanLineWidthInSamples; i++ ){
		//				int x = n % Mode.ScanLineWidthInSamples; 
		//			  //bp[x] += *sp;
		//				bp[x] += m_B12[ip + i];
		//				n++;
		//			}
		//		}
		//		n = 0;
		//		int max = 0;
		//		for( int i = 0; i < wd; i++ ){
		//			if( max < bp[i] ){
		//				max = bp[i];
		//				n = i;
		//			}
		//		}
		//		n -= (int)SstvSet.m_OFP;
		//		n = -n;
		//		if( Mode.Family == TVFamily.Scottie ) {
		//			if( n < 0 ) 
		//				n += Mode.ScanLineWidthInSamples;
		//		}
		//		if( m_Type == FreqDetect.Hilbert ) 
		//			n -= m_hill.m_htap/4;

		//		SstvSet.SetOFS( n );
		//		m_rBase = n;
		//		m_wBgn  = 0;
		//	}
		//}

		void SyncFreq(double d) {
		/*
			double		m_AFC_LowVal;	// (Center - SyncLow ) * 16384 / BWH
			double		m_AFC_HighVal;	// (Center - SyncHigh) * 16384 / BWH
			double		m_AFC_SyncVal;	// (Center - Sync    ) * 16384 / BWH
			double		m_AFC_BWH;		// BWH / 16384.0;
		*/
			d -= 128;

			if( (d <= m_AFC_LowVal) && (d >= m_AFC_HighVal) ){
				if( m_AFCDis == 0 && (m_AFCCount >= SstvSet.m_AFCB) && (m_AFCCount <= SstvSet.m_AFCE) ){
					m_AFCData = m_Avg.Avg(d);
					if( m_AFCCount == SstvSet.m_AFCE ){
						if( m_AFCGard != 0 ) {
							m_AFCLock = m_AFCAVG.SetData(m_AFCData);
							m_AFCGard = 0;
						} else {
							m_AFCLock = m_AFCAVG.Avg(m_AFCData);
						}
						m_AFCDiff = m_AFC_SyncVal - m_AFCLock;
						m_AFCFlag = 15;
						InitTone((int)(m_AFCDiff * m_AFC_BWH));
						m_AFCDis = m_AFCInt;
					}
				}
				m_AFCCount++;
			}
			else {
				if( (m_AFCCount >= SstvSet.m_AFCB) && m_AFCGard != 0 ){
					m_AFCGard--;
					if( m_AFCGard == 0 ) 
						m_AFCAVG.SetData(m_AFCLock);
				}
				m_AFCCount = 0;
				if( m_AFCDis != 0 )
					m_AFCDis--;
			}
		}

		/// <summary>
		/// Enumerate all the transmit modes we support.
		/// </summary>
        public IEnumerator<SSTVMode> GetEnumerator()
        {
            IEnumerator<SSTVMode> itrMode = GenerateMartin .GetModeEnumerator();
			while( itrMode.MoveNext() ) 
				yield return itrMode.Current;
			
            itrMode = GenerateScottie.GetModeEnumerator();
			while( itrMode.MoveNext() ) 
				yield return itrMode.Current;

            itrMode = GeneratePD     .GetModeEnumerator();
			while( itrMode.MoveNext() ) 
				yield return itrMode.Current;
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

		protected SSTVMode GetSSTVMode( AllModes eLegacy ) {
			foreach( SSTVMode tvMode in this ) {
				if( tvMode.LegacyMode == eLegacy ) {
					return tvMode;
				}
			}
			return null;
		}
    }

	public class DemodTest : CSSTVDEM, IPgModulator {
		double m_dbWPos;

		public DemodTest( 
			CSSTVSET p_oSSTVSet, 
			SYSSET   p_sys, 
			int      iSampFreq, 
			int      iSampBase, 
			double   dbToneOffset ) : 
			base( p_oSSTVSet, p_sys, iSampFreq, iSampBase, dbToneOffset )
		{
		}

		public void Reset() {
			m_dbWPos = 0;
		}

		/// <summary>
		/// Convert from frequency to level. This is a test harness function. Helps us
		/// make sure we have all our ducks in a row and understand the problem!!
		/// When this works it means, we generate and recieve precisly the same data.
		/// The Rx and Tx sections understand one another.
		/// </summary>
		/// <exception cref="ApplicationException" />
		public int Write( int iFrequency, uint uiGain, double dbTimeMS )
        {
			void WriteMeh() {
				double dbSamples = (dbTimeMS * SampFreq)/1000.0;
				// Convert 1500 to -16,384 and 2300 to 16,384.
				double foo = Math.Pow( 2, 15 ) / ( 2300 - 1500 );
				double d   = ( iFrequency - 1900 ) * foo;
				int    n   = m_wBase;
				for( int i = 0; i < dbSamples; ++i, ++n ) {
					int    idx = n % m_Buf.Length;
					m_Buf[idx] = (short)d;
					// This next bit simulates a hsync hit. 
					if( iFrequency < 1500 ) {
						//if( Mode.Family == TVFamily.Martin ) // S/B zero when we're seeing the hsync. 
						//	throw new ApplicationException("Buffer alignment problem.");
						m_SyncHit = n;
						m_B12[idx]  = (short)(m_SLvl + 1);
					}
				}
				// We need to track the floating point value, because the integer value accumulates
				// errors at too great of a rate on a per line basis! ^_^;;
				m_dbWPos += dbSamples;
				m_wBase  = (int)Math.Round( m_dbWPos );
			}

			if( m_Sync ) {
				//Martin way, using hsync signal.
				//if( iFrequency >= 1500 ) {
				//	// This is picture data...
				//	WriteMeh();
				//} else {
				//	// We'll assume it's the 1200hz HSync signal.
				//	// BUG: since called at SOL instead of EOL, we're mess'n up the m_wPage and m_wBase values.
				//	m_dbWPos = 0;
				//	PageWIncrement();
				//	WriteMeh();
				//}

				WriteMeh();
			}
			return 0;
        }

    } // end Class


}
