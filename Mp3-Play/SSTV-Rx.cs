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
	/// <summary>
	/// This object represents system settings that can change while the 
	/// SSTVDEM object is in use. Note, frequency is not one of the items.
	/// If you change the frequency you must re-alloc the SSTVDEM object.
	/// </summary>
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

		public bool   m_TestDem  { get; protected set; } = false; // used
		public double m_DemOff   { get; } = 0;
		public double m_DemWhite { get; } = 128.0/16384.0;
		public double m_DemBlack { get; } = 128.0/16384.0;
		public bool   m_DemCalibration { get; } = false; // see SSTVDraw.pCalibration

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

		public bool	m_bCQ100 { get; protected set; } = false;

		public SYSSET() {
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

		smMRT3,
		smMRT4,
		smEND,
	}

	public enum BandPass {
		Undefined = -1,
		Wide = 1,
		Narrow = 2,
		VeryNarrow = 3
	}

	public class SSTVSET {
		// I can probably get rid of these but I need to go
		// back and look at SSTVDem.SyncFreq, if needed or not.
		public int     m_AFCW    { get; protected set; }
		public int     m_AFCB    { get; protected set; }
		public int     m_AFCE    { get; protected set; }
		public bool	   m_fNarrow { get; protected set; }

		public readonly double g_dblToneOffset;
		public readonly double m_dbTxSampOffs;

		protected readonly double m_SampFreq;

		/// <summary>
		/// Should we ever support: smMN73,smMN110,smMN140,smMC110,smMC140, or smMC180,
		/// those are all narrow band.
		/// </summary>
		public static bool IsNarrowMode(TVFamily tvFamily)	{
			return false;
		}

		/// <summary>
		/// Object to hold the state of our audio inputs. SetMode() get's called when the
		/// SSTV receive mode changes.
		/// </summary>
		/// <param name="tvFamily">BUG: Work to get rid of this param. It gets overridden later.</param>
		/// <param name="dbTxSampOffs" >Not sure what this is for yet. Might delete it.</param>
		/// <remarks>This could probably live inside the SSTVDraw function.</remarks>
		public SSTVSET( TVFamily tvFamily, double dbToneOffset, double dbSampFreq, double dbTxSampOffs )
		{
			// These used to be globals, I'll see how much they change and if I need
			// to refactor initialization and such. Since SetSampFreq() get's called
			// randomly, will have to re-visit these.
			m_SampFreq      = dbSampFreq;
			m_dbTxSampOffs  = dbTxSampOffs;
			g_dblToneOffset = dbToneOffset;
			m_fNarrow       = false;  // Recieve width.

			SetMode( tvFamily ); 
		}

		/// <remarks>This gets called by the demodulator. Ick. This means
		/// we can't make the members here readonly.</remarks>
		public void SetMode( TVFamily tvFamily ) {
			m_fNarrow = IsNarrowMode( tvFamily );

			SetSampFreq( tvFamily );
		}

		void SetSampFreq(TVFamily tvFamily ){
			switch(tvFamily){
				case TVFamily.Martin:
					m_AFCW = (int)(2.0 * m_SampFreq / 1000.0);
					m_AFCB = (int)(1.0 * m_SampFreq / 1000.0);
					break;
				default:
					m_AFCW = (int)(3.0 * m_SampFreq / 1000.0);
					m_AFCB = (int)(1.5 * m_SampFreq / 1000.0);
					break;
			}

			// This is the "-i" option set in TMMSSTV::StartOption() if bCQ100 is
			// true then the offset is -1000. Else the offset is 0!!
			//if( m_bCQ100 ) { // Used to be a global.
    		//	double d = m_OFP * 1000.0 / SampFreq;
			//	m_OFP = (d + (1100.0/g_dblToneOffset)) * SampFreq / 1000.0;
			//}
			m_AFCE = m_AFCB + m_AFCW;
		}
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

		readonly double[]  Z = new double[HILLTAP+1];
		readonly double[]  H = new double[HILLTAP+1];
		readonly double[]  m_A = new double[4];

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

		public CHILL( bool bCQ100, double dbSampFreq, double dbSampBase, double dbToneOffset )
		{
			SampFreq = dbSampFreq;
			SampBase = dbSampBase;
			ToneOffs = dbToneOffset;

			SetWidth( bCQ100, false);

		    m_htap = m_tap / 2;
			MakeHilbert(H, m_tap, SampFreq, 100, SampFreq/2 - 100);
			m_A[0] = m_A[1] = m_A[2] = m_A[3] = 0;

			m_iir.MakeIIR(1800, SampFreq, 3, 0, 0);
		}

		//---------------------------------------------------------------------------
		// ＦＩＲフィルタ（ヒルベルト変換フィルタ）の設計 : (Hilbert transform filter) design
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

		public void SetWidth( bool bCQ100, bool fNarrow )
		{
			if( fNarrow ){
				m_OFF = (2 * Math.PI * (SSTVDEM.NARROW_CENTER + ToneOffs)) / SampFreq;
				m_OUT = 32768.0 * SampFreq / (2 * Math.PI * SSTVDEM.NARROW_BW);
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
			if( bCQ100 ){
				m_tap *= 3;
			}
		}

		//-------------------------------------------------
		// ＦＩＲフィルタのたたき込み演算 : FIR filter tapping operation
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
				d -= Math.PI*2;
			}
			else if( d <= -Math.PI ){
				d += Math.PI*2;
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
				m_BWH      = SSTVDEM.NARROW_BWH;
				m_CenterFQ = SSTVDEM.NARROW_CENTER + ToneOffs;
				m_HighFQ   = 2400.0 + ToneOffs;
				m_LowFQ    = SSTVDEM.NARROW_AFCLOW + ToneOffs;
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

	public enum FreqDetect {
		PLL = 0,
		FQC,
		Hilbert
	}

	public enum SeekPoint {
		Start,
		End,
		Current
	}

	public struct StgStat {
		public DateTime Time { get; private set; }
		public int      Size { get; private set; }

		public StgStat( DateTime sTime, int iSize ) {
			Time = sTime;
			Size = iSize;
		}
	}

	public interface IPgStreamConsumer<T> {
		T       Read();
		T		Read( int iOffset );
		int     Read( List<T> rgBuffer, int iLength );
		int     Seek( int iOffsset, SeekPoint ePoint );
		StgStat Stat { get; }

		IPgStreamConsumer<T> Clone();
	}

	/// <summary>
	/// SSTVSET changes it's state depending on which format it is re-initialized to. Thus,
	/// the Mode gets assigned everytime a new image comes down. You must make a new
	/// SSTVDEM if the sample frequency changes.
	/// </summary>
	/// <remarks>This object used to be able to store the frequency converted stream,
	/// to file, which is ok, but I'd rather have the original samples and stick that 
	/// in a WAV file. I'm working towards that. So I've removed the StorePage() code.
	/// </remarks>
	public class SSTVDEM :
		IEnumerable<SSTVMode> 
	{
		public SYSSET   Sys     { get; protected set; }
		public SSTVMode Mode    { get; protected set; } 
		public SSTVSET  SstvSet { get; protected set; }

		protected Dictionary<byte, SSTVMode > ModeDictionary { get; } = new Dictionary<byte, SSTVMode>();

		public event Action< SSTVMode, SSTVMode, int > Send_NextMode; // This is just a single event, with no indexer.

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

		readonly double[]  HBPF  = new double[TAPMAX+1];
		readonly double[]  HBPFS = new double[TAPMAX+1];
		readonly double[]  HBPFN = new double[TAPMAX+1];

		readonly CFIR2 m_BPF = new CFIR2();

		double   m_ad;
		int      m_OverFlow;
		BandPass m_bpf = BandPass.Undefined;
		int      m_bpftap;

		// looks like hilbert is broken. FQC & PLL seem to work fine!!
		public readonly FreqDetect m_Type      = FreqDetect.PLL; // FreqDetect.FQC; // BUG: This s/b parameter.
			   readonly bool       m_LevelType = false; // TODO: Probably sb param too. If make true, you must implement CSLVL class.

		readonly CIIRTANK m_iir11;
		readonly CIIRTANK m_iir12;
		readonly CIIRTANK m_iir13;
		readonly CIIRTANK m_iir19;
		readonly CIIR     m_lpf11;
		readonly CIIR     m_lpf12;
		readonly CIIR     m_lpf13;
		readonly CIIR     m_lpf19;

		readonly CLVL     m_lvl;
		readonly CSLVL    m_SyncLvl = new();

		// These three should inherit from a common interface.
		readonly CPLL	  m_pll;
		readonly CFQC     m_fqc;
		readonly CHILL    m_hill;

		public int HillTaps => m_hill.m_htap;

		public bool   Sync { get; protected set; }
		int           m_SyncMode;
		int           m_SyncTime;
		int           m_VisData;
		int           m_VisCnt;
		int           m_Skip;
		readonly bool m_SyncRestart;
		AllModes      m_NextMode;

		// Base pointer represent how far along in samples over the entire image we've gone. 
		// Write pos in samples stream. Moves forward by scanlinewidthinsamples chunks.
		// Always < size of buffer.Wraps around, see below for more details.
		public  int  m_wBase { get; protected set; } 
		public bool  m_Lost  { get; protected set; }

		protected short[] m_Buf;
		protected short[] m_B12;

		public    double m_SLvl  { get; protected set; }
		protected double m_SLvl2;

		bool              m_ScopeFlag;
		readonly CScope[] m_Scope = new CScope[2];

		CTICK       pTick;
		int         m_Tick;
		int         m_TickFreq;

		// More goodies that can probably live on their own class.
		readonly CSmooz m_Avg    = new ();
		readonly CSmooz m_AFCAVG = new ();
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

		readonly bool m_fskdecode = false; // A vestage of the fskDecode stuff.

		bool        m_fNarrow = false;

		// BUG: See if I can get these from CSSTVSET
		public double SampFreq { get; }
		public double SampBase { get; }
		// This is a global in the original code, but it's pretty clear
		// that we can't handle it changing mid run. So now it's a readonly
		// member variable.
		readonly double m_dblToneOffset;
		readonly double[] _rgSenseLevels = { 2400, 3500, 4800, 6000 };

		public SSTVDEM( SYSSET p_sys, double dblSampFreq, double dblSampBase = -1, double dbToneOffset=0 ) {
			Sys     = p_sys ?? throw new ArgumentNullException( "sys must not be null." );
			SstvSet = new( TVFamily.Martin, 0, dblSampFreq, 0 );

			m_dblToneOffset = dbToneOffset;
			SampFreq        = dblSampFreq;

			if( dblSampBase == -1 )
				SampBase	= dblSampFreq;
			else
				SampBase    = dblSampBase;

			// Find the biggest image type so our D12 image will be large enough.
			double dblMaxBufferInMs = 0;
			foreach( SSTVMode oMode in this ) {
				ModeDictionary.Add( oMode.VIS, oMode );
				double dblBufferInMs = oMode.ScanWidthInMS * ( oMode.Resolution.Width + 1 );
				if( dblMaxBufferInMs < dblBufferInMs )
					dblMaxBufferInMs = dblBufferInMs;
			}

			m_ad  = 0;

			// SSTVDEMBUFMAX is (24) lines. 
			int iBufSize = (int)(dblMaxBufferInMs * SampFreq / 1000.0 );
			m_Buf = new short[iBufSize];
			m_B12 = new short[iBufSize];

		  //Array.Clear( m_Buf, 0, m_Buf.Length );
		  //Array.Clear( m_B12, 0, m_Buf.Length );

			m_fqc  = new CFQC( SampFreq, dbToneOffset );
			m_hill = new CHILL( Sys.m_bCQ100, SampFreq, SampBase, dbToneOffset );

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

			m_iir11  = new CIIRTANK( dblSampFreq );
			m_iir12  = new CIIRTANK( dblSampFreq );
			m_iir13  = new CIIRTANK( dblSampFreq );
			m_iir19  = new CIIRTANK( dblSampFreq );
		//  m_iirfsk = new CIIRTANK( dblSampFreq );

			m_lpf11  = new CIIR();
			m_lpf12  = new CIIR();
			m_lpf13  = new CIIR();
			m_lpf19  = new CIIR();
		//  m_lpffsk = new CIIR();

			m_iir11 .SetFreq(1080     + m_dblToneOffset, SampFreq,  80.0);
			m_iir12 .SetFreq(1200     + m_dblToneOffset, SampFreq, 100.0);
			m_iir13 .SetFreq(1320     + m_dblToneOffset, SampFreq,  80.0);
			m_iir19 .SetFreq(1900     + m_dblToneOffset, SampFreq, 100.0);
		//  m_iirfsk.SetFreq(FSKSPACE + m_dblToneOffset, SampFreq, 100.0);

			m_lpf11 .MakeIIR(50, SampFreq, 2, 0, 0);
			m_lpf12 .MakeIIR(50, SampFreq, 2, 0, 0);
			m_lpf13 .MakeIIR(50, SampFreq, 2, 0, 0);
			m_lpf19 .MakeIIR(50, SampFreq, 2, 0, 0);
		//  m_lpffsk.MakeIIR(50, SampFreq, 2, 0, 0);

			m_wBase     = 0;
			m_Skip      = 0;
			Sync      = false;
			m_SyncMode  = 0;
			m_ScopeFlag = false;
			m_Lost      = false;

			m_lvl = new CLVL( (int)SampFreq, fAgcFast:true );
			m_afc = true;

			m_Tick   = 0;
			pTick    = null;
			m_Avg   .SetCount( (int)( 2.5*SampFreq/1000.0 ));
			m_AFCAVG.SetCount(15);
			m_AFCFQ  = 0;
			m_AFCInt = (int)(100 * SampFreq / 1000.0 );
			m_AFCDis = 0;

			m_SyncRestart = true;

			SetSenseLevel( 1 );
		}

		public void Dispose() {
			m_B12 = null;
			m_Buf = null;
		}

        protected class ShortConsumer : IPgStreamConsumer<short> {
			protected SSTVDEM _dp;
			protected int      _rBase = 0;

			public ShortConsumer( SSTVDEM dp ) {
				_dp = dp ?? throw new ArgumentNullException( "Demodulator must not be null" );
			}

			public ShortConsumer( SSTVDEM dp, int dblBase ) {
				_dp    = dp ?? throw new ArgumentNullException( "Demodulator must not be null" );
				_rBase = dblBase;
			}

            public StgStat Stat => new( DateTime.Now, _dp.m_Buf.Length );

            public IPgStreamConsumer<short> Clone() {
                return new ShortConsumer( _dp, _rBase );
            }

            public short Read() {
				return _dp.SignalGet( _rBase++ );
            }

			public short Read( int iOffset ) {
				_rBase = iOffset;
				return _dp.SignalGet( iOffset );
			}

            public int Read( List<short> rgBuffer, int iLength ) {
                throw new NotImplementedException();
            }

            public int Seek( int iOffset, SeekPoint ePoint ) {
				switch( ePoint ) {
					case SeekPoint.Current:
						_rBase += iOffset;
						return _rBase;
					case SeekPoint.Start:
						_rBase = iOffset;
						return _rBase;
					case SeekPoint.End:
						_rBase += _dp.m_wBase;
						return _rBase;

					default:
						return 0;
				}
                throw new NotImplementedException();
            }
        }

        public IPgStreamConsumer<short> CreateConsumer() {
			return new ShortConsumer( this );
		}

		/// <summary>
		/// this method gets called when we are ready to rock and roll. In the original
		/// system TmmSSTV would toss it's previous image and start a new one.
		/// </summary>
		public void Start( SSTVMode tvMode ) {
			int      iPrevBase = m_wBase;
			SSTVMode ePrevMode = Mode;

			Mode = tvMode;

			SetWidth(SSTVSET.IsNarrowMode( tvMode.Family ));
			InitAFC();

			m_fqc.Clear();
			m_Skip     = 0;
			m_wBase    = 0;
			m_Lost     = false;
			Sync       = true; // This is the only place we set to true!
			m_SyncMode = 0; 

			// OpenCloseRxBuff();

			// However, this combo makes sense. We go back for looking for sync signals at the
			// same time we're storing the image scan lines.
			SetWidth(m_fNarrow);

			Send_NextMode?.Invoke( tvMode, ePrevMode, iPrevBase );

			// Don't support narrow band modes.
			//if( m_fNarrow ) 
			//	CalcNarrowBPF(HBPFN, m_bpftap, m_bpf, SSTVSET.m_Mode);
		}

		public virtual void Reset()	{
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
			//m_sint1.Reset();
			//m_sint2.Reset();
			//m_sint3.Reset();

			m_SyncMode = 512;
			Sync     = false;

			SSTVMode oPrevMode = Mode;
			int      iPrevBase = m_wBase;

			m_wBase  = 0;
			m_Skip   = 0;
			SetWidth( false );

			Mode = null;

			Send_NextMode?.Invoke( Mode, oPrevMode, iPrevBase );
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="H1"></param>
		/// <param name="H2"></param>
		/// <param name="H3">UNITIALIZED. It's for narrow modes which we don't support.</param>
		/// <param name="bpftap"></param>
		/// <param name="bpf"></param>
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
		//			CFIR2.MakeFilter(H3, bpftap, ffBPF, SampFreq,  NARROW_BPFLOW-200, NARROW_BPFHIGH, 20, 1.0);
					break;
				case BandPass.Narrow:
					bpftap = (int)(64 * SampFreq / 11025.0 );
					CFIR2.MakeFilter(H1, bpftap, FirFilt.ffBPF, SampFreq,  lfq, 2500 + m_dblToneOffset, 40, 1.0);
					CFIR2.MakeFilter(H2, bpftap, FirFilt.ffBPF, SampFreq, lfq2, 2500 + m_dblToneOffset, 20, 1.0);
		//			CFIR2.MakeFilter(H3, bpftap, ffBPF, SampFreq, NARROW_BPFLOW-100, NARROW_BPFHIGH, 40, 1.0);
					break;
				case BandPass.VeryNarrow: 
					bpftap = (int)(96 * SampFreq / 11025.0 );
					CFIR2.MakeFilter(H1, bpftap, FirFilt.ffBPF, SampFreq,  lfq, 2400 + m_dblToneOffset, 50, 1.0);
					CFIR2.MakeFilter(H2, bpftap, FirFilt.ffBPF, SampFreq, lfq2, 2500 + m_dblToneOffset, 20, 1.0);
		//			CFIR2.MakeFilter(H3, bpftap, ffBPF, SampFreq,  NARROW_BPFLOW, NARROW_BPFHIGH, 50, 1.0);
					break;
				default:
					bpftap = 0;
					break;
			}
		  //CalcNarrowBPF(H3, bpftap, bpf, SSTVSET.m_Mode); If I add those modes I'll figure this out.
		}

		/// <summary>
		/// The MakeFilter( H3, ... in CalcBPF was always commented out with this code called at the bottom.
		/// But since I don't support narrow modes, I don't call this function either.
		/// </summary>
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
		/// This looks it compensates for a mid signal reception to changes between bandpass 
		/// values. It looks like skip is only called if the user changes settings
		/// while the image is being decoded else this function is called on setup.
		/// NOTE: That happens when we miss the VIS and try manually overriding the mode
		/// mid reception. We DEFINITELY need a rewrite in SSTVDraw to support that.
		/// NOTE: CalcNarrowBPF() normally gets called in Start() but I disabled that
		/// since I don't support narrow modes. If I did, I'd probably seperate out the
		/// sync == true side of this object into a set of subclasses for the narrowband
		/// stuff.
		/// </summary>
		/// <seealso cref="CalcNarrowBPF" />
		/// <seealso cref="Start" />
		void SetBPF(BandPass bpf) {
			if( bpf != m_bpf ){
				m_bpf = bpf;

				int delay = m_bpftap;
				CalcBPF();
				if( Sync ) {
					delay = (m_bpftap - delay) / 2;
					m_Skip = delay;
				}
			}
		}

		void Idle(double d)	{
			if( !Sys.m_TestDem ) 
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
				if( Sys.m_bCQ100 ) {
					m_AFC_LowVal  = (NARROW_CENTER - NARROW_SYNC - 50) * 16384.0 / NARROW_BWH;	// (Center - SyncLow) * 16384 / BWH
					m_AFC_HighVal = (NARROW_CENTER - NARROW_SYNC + 50) * 16384.0 / NARROW_BWH;	// (Center - SyncHigh) * 16384 / BWH
				}
			} else {
				m_AFC_LowVal  = (1900 - 1000) * 16384.0 / 400;	// (Center - SyncLow ) * 16384 / BWH
				m_AFC_HighVal = (1900 - 1325) * 16384.0 / 400;	// (Center - SyncHigh) * 16384 / BWH
				m_AFC_SyncVal = (1900 - 1200) * 16384.0 / 400;	// (Center - Sync    ) * 16384 / BWH
				m_AFC_BWH = 400 / 16384.0;						// BWH / 16384.0;
				if( Sys.m_bCQ100 ) {
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
			//  m_iirfsk.SetFreq(FSKSPACE+dfq + m_dblToneOffset, SampFreq, 100.0);
				m_AFCFQ = dfq;
			}
		}

		/// <remarks>
		/// You know it's probably dumb to pas the bCQ100 since it never is
		/// going to change for the lifetime of our object. If any system variables
		/// change we'd probably just re-instatiate CSSTVDEM and TmmSSTV. Look
		/// at this in the future. (Same goes for narrow too)
		/// </remarks>
		void SetWidth( bool fNarrow ) {
			if( m_fNarrow != fNarrow ) {
				m_fNarrow = fNarrow;
				m_hill.SetWidth( Sys.m_bCQ100, fNarrow);
    			m_fqc .SetWidth(fNarrow);
				m_pll .SetWidth(fNarrow);
			}
		}

		/// <summary>
		/// At present this is only called in the constructor. Maybe when I get further
		/// along I'll see about a dialog setting for this or something.
		/// </summary>
		/// <param name="iSenseLvl">1, 2, or 3 with 0 the default for out of range.</param>
		void SetSenseLevel( int iSenseLvl ) {
			try {
				m_SLvl = _rgSenseLevels[iSenseLvl];
			} catch( IndexOutOfRangeException ) {
				m_SLvl = _rgSenseLevels[0];
			}
			m_SLvl2 = m_SLvl * 0.5;
		}

		public void Do(double s) {
			if( (s > 24578.0) || (s < -24578.0) ){
				m_OverFlow = 1; // The grapher probably clears this.
			}
			double d = (s + m_ad) * 0.5;    // LPF
			m_ad = s;
			if( m_bpf != 0 ){
				if( Sync || (m_SyncMode >= 3) ){
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

			// These two go into the B12 buffer, depending on narrow or normal.
			double d12; // normal sync.
			double d19; // narrow sync

			d12 = m_iir12.Do(d);
			d12 = m_lpf12.Do( Math.Abs( d12 ) );

			d19 = m_iir19.Do(d);
			d19 = m_lpf19.Do( Math.Abs( d19 ) );

			// One of these days I'll figure out why we need both 19 & 12.
			double dHSync = m_fNarrow ? d19 : d12;

			//double dsp;
			//dsp = m_iirfsk.Do(d);
			//dsp = m_lpffsk.Do( Math.Aps( dsp ));
			// DecodeFSK( (int)d19, (int)dsp );

			if( m_ScopeFlag )
				m_Scope[0].WriteData( dHSync );
			if( m_LevelType ) 
				m_SyncLvl.Do( dHSync );

			if( m_Tick != 0 ) {
				pTick?.Write( d12 );
				return;
			}

			if( !Sync || m_SyncRestart ) {
				SSTVMode tvMode;
				//m_sint1.SyncInc();
				//m_sint2.SyncInc();
				//m_sint3.SyncInc();

				// The only time we care about this one is in VIS.
				double d11;
				double d13;

				d11 = m_iir11.Do( d );
				d11 = m_lpf11.Do( Math.Abs( d11 ));

				switch(m_SyncMode){
					case 0:                 // 自動開始 : Start automatically
						// The first 1900hz has been seen, and now we're going down to 1200 for 15 ms. (s/b 10)
						if( (d12 > d19) && (d12 > m_SLvl) && ((d12-d19) >= m_SLvl) ){
							m_SyncMode++;
							m_SyncTime = (int)(10 * SampFreq/1000); // this is probably the ~10 ms between each 1900hz tone.
							//if( !m_Sync /* && m_MSync */ ) 
							//	m_sint1.SyncTrig( (int)d12);
						}
						break;
					case 1:                 // 1200Hz(30ms)‚ の継続チェック: continuous check.
						//if( !m_Sync /* && m_MSync */ ){
						//	if( (d12 > d19) && (d12 > m_SLvl2) && ((d12-d19) >= m_SLvl2) ){
						//		m_sint2.SyncMax( (int)d12);
						//	}
						//}
						// the second 1900hz has been seen now down to 1200hz again for 30ms.
						if( (d12 > d19) && (d12 > m_SLvl) && ((d12-d19) >= m_SLvl) ){
							//if( !m_Sync /* && m_MSync */ ){
							//	m_sint1.SyncMax( (int)d12);
							//}
							m_SyncTime--;
							if( m_SyncTime == 0 ){
								m_SyncMode++;
								m_SyncTime = (int)(30 * SampFreq/1000); // Each bit is 30 ms!!
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
								m_SyncTime = (int)(30 * SampFreq/1000 ); // Get next bit.
								m_VisData >>= 1;
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
						if( !Sync ){
							m_pll.Do(ad);
						}
						m_SyncTime--;
						if( m_SyncTime == 0 ){
							if( (d12 > d19) && (d12 > m_SLvl) ){
								if( Sync ){
									// Looks like we request save when we're 65% the way thru an image,
									// and then suddenly get a new image. I'd do this back when the 
									// new mode was requested.
									//if( m_rBase >= (SstvSet.m_LM * 65/100.0) ){
									//	m_ReqSave = true;
									//}
								}
								tvMode = GetSSTVMode( m_NextMode );
								if( tvMode != null ) {
									SstvSet.SetMode( tvMode.Family ); 
									Start( tvMode );
								}
							} else {
								m_SyncMode = 0;
							}
						}
						break;
					case 256:                // 強制開始 : Forced start.
						tvMode = GetSSTVMode( m_NextMode );
						if( tvMode != null ) {
							SstvSet.SetMode( tvMode.Family ); 
							Start( tvMode );
						}
						break;
					case 512:               // 0.5sのウエイト : .5s wait.
						m_SyncTime = (int)(SampFreq * 0.5);
						m_SyncMode++;
						break;
					case 513:
						m_SyncTime--;
						if( m_SyncTime <= 0 ){
							m_SyncMode = 0;
						}
						break;
				}
			}
			if( Sync ){
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
							SignalSet( -d, 0 );
						}
					}
				} else {
					if( m_ScopeFlag ){
						m_Scope[1].WriteData( d );
					}
					SignalSet( -d, dHSync );
				}
			}
			else if( Sys.m_TestDem ){
				// This is used by the TOptionDlg::TimerTimer code for test.
				double m_CurSig; // I removed the member variable since it was not being used elsewhere.

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

		/// <summary>
		/// Legal values for the index are 0 to less than the buffer length.
		/// No negative numbers either. 
		/// </summary>
		/// <param name="iIndex">Check if this value is ok.</param>
		/// <returns>-1, 0, 1, with 0 being in bounds.</returns>
		public int BoundsCompare( int iIndex ) {
			if( iIndex >= m_wBase )
				return 1;
			if( m_wBase - iIndex >= m_Buf.Length )
				return -1;
			if( iIndex < 0 )
				return -1;

			return 0;
		}

		/// <summary>
		/// Get the value at the absolute offset.
		/// </summary>
		/// <param name="iIndex">absolute offset into the buffer</param>
		/// <returns>value of the m_Buf buffer.</returns>
		/// <exception cref="ArgumentOutOfRangeException" />
		public short SignalGet( int iIndex ) {
			int iCompare = BoundsCompare( iIndex );
			if( iCompare != 0 )
				throw new ArgumentOutOfRangeException( "Index is out of bounds: " + ( iCompare > 0 ? "Above" : "Below" ) );

			return m_Buf[ iIndex % m_Buf.Length ];
		}

		public short SyncGet( int iIndex ) {
			int iCompare = BoundsCompare( iIndex );
			if( iCompare != 0 )
				throw new ArgumentOutOfRangeException( "Index is out of bounds: " + ( iCompare > 0 ? "Above" : "Below" ) );

			return m_B12[ iIndex % m_Buf.Length ];
		}

		protected void SignalSet( double dblSignal, double dblSync ) {
			int iOffset = m_wBase++ % m_Buf.Length;

			m_Buf[ iOffset ] = (short)dblSignal;
			m_B12[ iOffset ] = (short)dblSync;
		}

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
			} else {
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
		/// Enumerate all the transmit modes we support. The generators come in three families.
		/// Each family has various variants, all map to the legacy modes.
		/// </summary>
        public IEnumerator<SSTVMode> GetEnumerator()
        {
            return EnumModes();
        }

		public static IEnumerator<SSTVMode> EnumModes() {
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

		/// <summary>
		/// Maps from the old legacy mode to the new SSTVMode. Basically we enum the families
		/// and in those check the legacy value from within the family. In the future we might
		/// be able to rid ourselves of the legacy states, but we'll see. 
		/// </summary>
		public SSTVMode GetSSTVMode( AllModes eLegacy ) {
			foreach( SSTVMode tvMode in this ) {
				if( tvMode.LegacyMode == eLegacy ) {
					return tvMode;
				}
			}
			return null;
		}
    }

	public class DemodTest : SSTVDEM, IPgModulator {
		double m_dbWPos;

		public DemodTest( 
			SYSSET   p_sys, 
			double   dblSampFreq, 
			double   dblSampBase, 
			double   dbToneOffset ) : 
			base( p_sys, dblSampFreq, (int)dblSampBase, dbToneOffset )
		{
		}

		public override void Reset() {
			base.Reset();

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
						m_B12[idx]  = (short)(m_SLvl + 1);
					}
				}
				// We need to track the floating point value, because the integer value accumulates
				// errors at too great of a rate on a per line basis! ^_^;;
				m_dbWPos += dbSamples;
				m_wBase  = (int)Math.Round( m_dbWPos );
			}

			if( Sync ) {
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
