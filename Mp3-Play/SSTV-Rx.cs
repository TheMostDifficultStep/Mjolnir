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
using System.Collections.Generic;
using System.Text;

namespace Play.Sound {
	public class SYSSET {
		int     m_Priority;

		int		m_SoundPriority;
		int		m_SoundStereo;
		int		m_StereoTX;

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

		int		m_Palette;
		int     m_BitPixel;

		int     m_FFTType;
		int		m_FFTGain;
		int		m_FFTResp;
		int     m_FFTStg;
		int     m_FFTWidth;
		int		m_FFTAGC;
		int     m_FFTPriority;
		double	m_TxSampOff;
		public double	m_SampFreq { get; protected set; } // used

		int     m_TuneTXTime;
		int     m_TuneSat;

		public bool m_TestDem { get; protected set; } // used
		double   m_DemOff;
		double   m_DemWhite;
		double   m_DemBlack;
		int      m_DemCalibration;
		double[] m_Dem17 = new double[17];

		int      m_Way240;
		int		 m_AutoMargin;
				  
		public bool m_UseRxBuff { get; protected set; } // used
		int      m_AutoStop;
		int      m_AutoSync;
		int      m_CWID;
		int      m_CWIDFreq;
		string   m_CWIDText;
		int      m_CWIDSpeed;
		int		 m_CWIDWPM;
		string   m_MMVID;
		string	 m_CWText;
				  
		int      m_TXFSKID;

		int      m_FixedTxMode;
		string[] m_TextList = new string[16];

		int      m_PicSelRTM;
		int      m_PicSelSmooz;

		int      m_Sharp2D;
		int      m_Differentiator;
		double   m_DiffLevelP;
		double   m_DiffLevelM;

		public bool m_Repeater { get; protected set; } // used
		public int  m_RepSenseLvl { get; protected set; }  // トーン検出感度 : Tone detection sensitivity (used)
		string   m_RepAnsCW;
		public int m_RepTimeA { get; protected set; }     // トーン検出時間 : Tone detection time 
		public int m_RepTimeB { get; protected set; }     // トーン検出からAnsCW出力までの時間 : Time from tone detection to AnsCW output 
		public int m_RepTimeC { get; protected set; }     // 受信待機のタイムアウト : Receive wait timeout
		public int m_RepTimeD { get; protected set; }     // リプレイ送信の遅延時間 : Delay time for replay transmission 

		int      m_RepBeacon;
		int      m_RepBeaconMode;
		string   m_RepTempTX;
		string   m_RepTempBeacon;
		int      m_RepBottomAdj;
		int      m_RepQuietnessTime;
		int      m_RepBeaconFilter;
		string   m_RepFolder;

		int      m_UseB24;
		string   m_Msg;
		int		 m_DisFontSmooth;

		int			m_TempDelay;
		int			m_Temp24;
		public bool	m_bCQ100 { get; protected set; }
	}

	public enum AllModes {
		smR36 = 0,
		smR72,
		smAVT,
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

	class CSSTVSET {
		public AllModes m_Mode   { get; protected set; }
		public AllModes m_TxMode { get; protected set; }

		public double   m_TW     { get; protected set; }
		public double   m_KS	 { get; protected set; }
		public double   m_KS2	 { get; protected set; }
		public double	m_OF	 { get; protected set; }
		public double   m_OFP    { get; protected set; }
		public double   m_CG     { get; protected set; }
		public double   m_CB     { get; protected set; }
		public double   m_SG     { get; protected set; }
		public double   m_SB     { get; protected set; }
		public int      m_WD     { get; protected set; }
		public int      m_L      { get; protected set; }
		public int      m_LM     { get; protected set; }
		public int      m_OFS    { get; protected set; }
		public int		m_IOFS   { get; protected set; }

		public double  m_SampFreq { get; protected set; }

		public double  m_KSS { get; protected set; }
		public double  m_KS2S{ get; protected set; }
		public int     m_KSB { get; protected set; }

		public int     m_TWD { get; protected set; }
		public int     m_TL  { get; protected set; }
		public double  m_TTW { get; protected set; }
		public double  m_TxSampFreq { get; protected set; }

		public readonly UInt32[] m_MS = new UInt32[(int)AllModes.smEND];
		public UInt32   m_MSLL { get; protected set; }
		public UInt32   m_MSL  { get; protected set; }
		public UInt32   m_MSH  { get; protected set; }

		public int     m_AFCW { get; protected set; }
		public int     m_AFCB { get; protected set; }
		public int     m_AFCE { get; protected set; }

		public bool	m_fNarrow   { get; protected set; }
		public bool	m_fTxNarrow { get; protected set; }

		public readonly double g_dblToneOffset;
		public readonly double m_dbTxSampOffs;

		public double SampFreq => m_SampFreq;
		private readonly bool m_bCQ100;

		public static bool IsNarrowMode(AllModes mode)	{
			switch(mode){
				case AllModes.smMN73:
				case AllModes.smMN110:
				case AllModes.smMN140:
				case AllModes.smMC110:
				case AllModes.smMC140:
				case AllModes.smMC180:
        			return true;
				default:
        			return false;
			}
		}
		public CSSTVSET( double dbToneOffset, double dbSampFreq, double dbTxSampOffs, bool bCQ100 )
		{
			// These used to be globals, I'll see how much they change and if I need
			// to refactor initialization and such. Since SetSampFreq() get's called
			// randomly, will have to re-visit these.
			m_SampFreq      = dbSampFreq;
			m_dbTxSampOffs  = dbTxSampOffs;
			g_dblToneOffset = dbToneOffset;
			m_bCQ100        = bCQ100;

			m_fNarrow = m_fTxNarrow = false;
			m_TxMode  = AllModes.smSCT1;

			SetMode(AllModes.smSCT1);
			InitIntervalPara();
		}

		public void InitIntervalPara()
		{
			for( int i = 0; i < (int)AllModes.smEND; i++ ){
				m_MS[i] = (uint)(GetTiming((AllModes)i) * m_SampFreq / 1000.0 );
			}
			m_MS[2] = 0;                                 // AVT
		//    m_MSLL =  100.0 * m_SampFreq / 1000.0;       // Lowest
		//    m_MSL  =  147.0 * m_SampFreq / 1000.0;       // Lowest
		//    m_MSH  = 1050.0 * 3 * m_SampFreq / 1000.0;   // Highest
			m_MSLL = (uint)(50.0   * m_SampFreq / 1000.0 );        // Lowest
			m_MSL  = (uint)(63.0   * m_SampFreq / 1000.0 );        // Lowest
			m_MSH  = (uint)(1390.0 * 3 * m_SampFreq / 1000.0);     // Highest
		}

		/// <remarks>This gets called by the demodulator. Ick. This means
		/// we can't make the members here readonly.</remarks>
		public void SetMode( AllModes mode)
		{
			//m_SampFreq = sys.m_SampFreq;
			m_Mode    = mode;
			m_fNarrow = CSSTVSET.IsNarrowMode(mode);
			SetSampFreq();
			m_WD = (int)m_TW;
			m_LM = (int)((m_TW * m_L) + 1 );
		}

		void SetTxMode(AllModes mode)
		{
			m_TxSampFreq = m_SampFreq /* sys.m_SampFreq */ + m_dbTxSampOffs /* sys.m_TxSampOff */;
			m_TxMode = mode;
			m_fTxNarrow = CSSTVSET.IsNarrowMode(mode);
			SetTxSampFreq();
			m_TWD = (int)m_TTW;
		}

		void GetBitmapSize( out int w, out int h, AllModes mode) {
			switch(mode){
				case AllModes.smPD120:
				case AllModes.smPD180:
				case AllModes.smPD240:
				case AllModes.smP3:
				case AllModes.smP5:
				case AllModes.smP7:
				case AllModes.smML180:
				case AllModes.smML240:
				case AllModes.smML280:
				case AllModes.smML320:
					w = 640;
					h = 496;
					break;
				case AllModes.smPD160:
					w = 512;
					h = 400;
					break;
				case AllModes.smPD290:
					w = 800;
					h = 616;
					break;
				default:        // SCT1
					w = 320;
					h = 256;
					break;
			}
		}

		void GetPictureSize(out int w, out int h, out int hp, AllModes mode)
		{
			GetBitmapSize( out w, out h, mode);
			switch(mode){
				case AllModes.smRM8:
				case AllModes.smRM12:
				case AllModes.smR24:
				case AllModes.smR36:
				case AllModes.smR72:
				case AllModes.smAVT:
					hp = 240;
					break;
				default:
					hp = h;
					break;
			}
		}

		void SetSampFreq(){
			switch(m_Mode){
				case AllModes.smR36:
					m_KS = 88.0 * m_SampFreq / 1000.0;
					m_KS2 = 44.0 * m_SampFreq / 1000.0;
					m_OF = 12.0 * m_SampFreq / 1000.0;
		//            m_OFP = 10.8 * m_SampFreq / 1000.0;
					m_OFP = 10.7 * m_SampFreq / 1000.0;
					m_SG = (88.0 + 1.25) * m_SampFreq / 1000.0;
					m_CG = (88.0 + 3.5) * SampFreq /1000.0; 
					m_SB = 94.0 * m_SampFreq / 1000.0;
					m_CB = m_SB + m_KS2;
					m_L = 240;
					break;
				case AllModes.smR72:
					m_KS = 138.0 * m_SampFreq / 1000.0;
					m_KS2 = 69.0 * m_SampFreq / 1000.0;
					m_OF = 12.0 * m_SampFreq / 1000.0;
					m_OFP = 10.7 * m_SampFreq / 1000.0;
					m_SG = 144.0 * m_SampFreq / 1000.0;
					m_CG = m_SG + m_KS2;
					m_SB = 219.0 * m_SampFreq / 1000.0;
					m_CB = m_SB + m_KS2;
					m_L = 240;
					break;
				case AllModes.smAVT:
					m_KS = 125.0 * m_SampFreq / 1000.0;
					m_OF = 0.0 * m_SampFreq / 1000.0;
					m_OFP = 0.0 * m_SampFreq / 1000.0;
					m_SG = m_KS;
					m_CG = m_KS + m_SG;
					m_SB = m_SG + m_SG;
					m_CB = m_KS + m_SB;
					m_L = 240;
					break;
				case AllModes.smSCT2:
					m_KS = 88.064 * m_SampFreq / 1000.0;
					m_OF = 10.5 * m_SampFreq / 1000.0;
					m_OFP = 10.8 * m_SampFreq / 1000.0;
					m_SG = 89.564 * m_SampFreq / 1000.0;
					m_CG = m_KS + m_SG;
					m_SB = m_SG + m_SG;
					m_CB = m_KS + m_SB;
					m_L = 256;
					break;
				case AllModes.smSCTDX:
					m_KS = 345.6 * m_SampFreq / 1000.0;
					m_OF = 10.5 * m_SampFreq / 1000.0;
		//            m_OFP = 9.5 * m_SampFreq / 1000.0;
					m_OFP = 10.2 * m_SampFreq / 1000.0;
					m_SG = 347.1 * m_SampFreq / 1000.0;
					m_CG = m_KS + m_SG;
					m_SB = m_SG + m_SG;
					m_CB = m_KS + m_SB;
					m_L = 256;
					break;
				case AllModes.smMRT1:
					m_KS = 146.432 * m_SampFreq / 1000.0;
					m_OF = 5.434 * m_SampFreq / 1000.0;
		//            m_OFP = 7.3 * m_SampFreq / 1000.0;
					m_OFP = 7.2 * m_SampFreq / 1000.0;
					m_SG = 147.004 * m_SampFreq / 1000.0;
					m_CG = m_KS + m_SG;
					m_SB = m_SG + m_SG;
					m_CB = m_KS + m_SB;
					m_L = 256;
					break;
				case AllModes.smMRT2:
					m_KS = 73.216 * m_SampFreq / 1000.0;
					m_OF = 5.434 * m_SampFreq / 1000.0;
					m_OFP = 7.4 * m_SampFreq / 1000.0;
					m_SG = 73.788 * m_SampFreq / 1000.0;
					m_CG = m_KS + m_SG;
					m_SB = m_SG + m_SG;
					m_CB = m_KS + m_SB;
					m_L = 256;
					break;
				case AllModes.smSC2_180:
					m_KS = 235.0 * m_SampFreq / 1000.0;
					m_OF = 6.0437 * m_SampFreq / 1000.0;
		//            m_OFP = 7.5 * m_SampFreq / 1000.0;
					m_OFP = 7.8 * m_SampFreq / 1000.0;
					m_SG = m_KS;
					m_CG = m_KS + m_SG;
					m_SB = m_SG + m_SG;
					m_CB = m_KS + m_SB;
					m_L = 256;
					break;
				case AllModes.smSC2_120:
					m_KS = 156.5 * m_SampFreq / 1000.0;
					m_OF = 6.02248 * m_SampFreq / 1000.0;
					m_OFP = 7.5 * m_SampFreq / 1000.0;
					m_SG = m_KS;
					m_CG = m_KS + m_SG;
					m_SB = m_SG + m_SG;
					m_CB = m_KS + m_SB;
					m_L = 256;
					break;
				case AllModes.smSC2_60:
					m_KS = 78.128 * m_SampFreq / 1000.0;
					m_OF = 6.0006 * m_SampFreq / 1000.0;
					m_OFP = 7.9 * m_SampFreq / 1000.0;
					m_SG = m_KS;
					m_CG = m_KS + m_SG;
					m_SB = m_SG + m_SG;
					m_CB = m_KS + m_SB;
					m_L = 256;
					break;
				case AllModes.smPD50:
					m_KS = 91.520 * m_SampFreq / 1000.0;
					m_OF = 22.080 * m_SampFreq / 1000.0;
					m_OFP = 19.300 * m_SampFreq / 1000.0;
					m_SG = m_KS;
					m_CG = m_KS + m_SG;
					m_SB = m_SG + m_SG;
					m_CB = m_KS + m_SB;
					m_L = 128;
					break;
				case AllModes.smPD90:
					m_KS = 170.240 * m_SampFreq / 1000.0;
					m_OF = 22.080 * m_SampFreq / 1000.0;
					m_OFP = 18.900 * m_SampFreq / 1000.0;
					m_SG = m_KS;
					m_CG = m_KS + m_SG;
					m_SB = m_SG + m_SG;
					m_CB = m_KS + m_SB;
					m_L = 128;
					break;
				case AllModes.smPD120:
					m_KS = 121.600 * m_SampFreq / 1000.0;
					m_OF = 22.080 * m_SampFreq / 1000.0;
					m_OFP = 19.400 * m_SampFreq / 1000.0;
					m_SG = m_KS;
					m_CG = m_KS + m_SG;
					m_SB = m_SG + m_SG;
					m_CB = m_KS + m_SB;
					m_L = 248;
					break;
				case AllModes.smPD160:
					m_KS = 195.584 * m_SampFreq / 1000.0;
					m_OF = 22.080 * m_SampFreq / 1000.0;
					m_OFP = 18.900 * m_SampFreq / 1000.0;
					m_SG = m_KS;
					m_CG = m_KS + m_SG;
					m_SB = m_SG + m_SG;
					m_CB = m_KS + m_SB;
					m_L = 200;
					break;
				case AllModes.smPD180:
					m_KS = 183.04 * m_SampFreq / 1000.0;
					m_OF = 22.080 * m_SampFreq / 1000.0;
					m_OFP = 18.900 * m_SampFreq / 1000.0;
					m_SG = m_KS;
					m_CG = m_KS + m_SG;
					m_SB = m_SG + m_SG;
					m_CB = m_KS + m_SB;
					m_L = 248;
					break;
				case AllModes.smPD240:
					m_KS = 244.48 * m_SampFreq / 1000.0;
					m_OF = 22.080 * m_SampFreq / 1000.0;
					m_OFP = 18.900 * m_SampFreq / 1000.0;
					m_SG = m_KS;
					m_CG = m_KS + m_SG;
					m_SB = m_SG + m_SG;
					m_CB = m_KS + m_SB;
					m_L = 248;
					break;
				case AllModes.smPD290:
					m_KS = 228.80 * m_SampFreq / 1000.0;
					m_OF = 22.080 * m_SampFreq / 1000.0;
					m_OFP = 18.900 * m_SampFreq / 1000.0;
					m_SG = m_KS;
					m_CG = m_KS + m_SG;
					m_SB = m_SG + m_SG;
					m_CB = m_KS + m_SB;
					m_L = 616/2;
					break;
				case AllModes.smP3:
					m_KS = 133.333 * m_SampFreq / 1000.0;
					m_OF = (5.208 + 1.042) * m_SampFreq / 1000.0;
					m_OFP = 7.80 * m_SampFreq / 1000.0;
					m_SG = (133.333 + 1.042) * m_SampFreq / 1000.0;
					m_CG = m_KS + m_SG;
					m_SB = m_SG + m_SG;
					m_CB = m_KS + m_SB;
					m_L = 496;
					break;
				case AllModes.smP5:
					m_KS = 200.000 * m_SampFreq / 1000.0;
					m_OF = (7.813 + 1.562375) * m_SampFreq / 1000.0;
					m_OFP = 9.20 * m_SampFreq / 1000.0;
					m_SG = (200.000 + 1.562375) * m_SampFreq / 1000.0;
					m_CG = m_KS + m_SG;
					m_SB = m_SG + m_SG;
					m_CB = m_KS + m_SB;
					m_L = 496;
					break;
				case AllModes.smP7:
					m_KS = 266.667 * m_SampFreq / 1000.0;
					m_OF = (10.417 + 2.083) * m_SampFreq / 1000.0;
					m_OFP = 11.50 * m_SampFreq / 1000.0;
					m_SG = (266.667 + 2.083) * m_SampFreq / 1000.0;
					m_CG = m_KS + m_SG;
					m_SB = m_SG + m_SG;
					m_CB = m_KS + m_SB;
					m_L = 496;
					break;
				case AllModes.smMR73:
					//|--KS--|--KS2--|--KS2--|
					//      SG     CG=SB     CB
					m_KS = 138.0 * m_SampFreq / 1000.0;
					m_KS2 = m_KS * 0.5;
					m_OF = 10.0 * m_SampFreq / 1000.0;
					m_OFP = 10.6 * m_SampFreq / 1000.0;
					m_SG = m_KS + 0.1;
					m_CG = m_SG + m_KS2;
					m_SB = m_CG + 0.1;
					m_CB = m_SB + m_KS2;
					m_L = 256;
					break;
				case AllModes.smMR90:
					m_KS = 171.0 * m_SampFreq / 1000.0;
					m_KS2 = m_KS * 0.5;
					m_OF = 10.0 * m_SampFreq / 1000.0;
					m_OFP = 10.6 * m_SampFreq / 1000.0;
					m_SG = m_KS + 0.1;
					m_CG = m_SG + m_KS2;
					m_SB = m_CG + 0.1;
					m_CB = m_SB + m_KS2;
					m_L = 256;
					break;
				case AllModes.smMR115:
					m_KS = 220.0 * m_SampFreq / 1000.0;
					m_KS2 = m_KS * 0.5;
					m_OF = 10.0 * m_SampFreq / 1000.0;
					m_OFP = 10.6 * m_SampFreq / 1000.0;
					m_SG = m_KS + 0.1;
					m_CG = m_SG + m_KS2;
					m_SB = m_CG + 0.1;
					m_CB = m_SB + m_KS2;
					m_L = 256;
					break;
				case AllModes.smMR140:
					m_KS = 269.0 * m_SampFreq / 1000.0;
					m_KS2 = m_KS * 0.5;
					m_OF = 10.0 * m_SampFreq / 1000.0;
					m_OFP = 10.6 * m_SampFreq / 1000.0;
					m_SG = m_KS + 0.1;
					m_CG = m_SG + m_KS2;
					m_SB = m_CG + 0.1;
					m_CB = m_SB + m_KS2;
					m_L = 256;
					break;
				case AllModes.smMR175:
					m_KS = 337.0 * m_SampFreq / 1000.0;
					m_KS2 = m_KS * 0.5;
					m_OF = 10.0 * m_SampFreq / 1000.0;
					m_OFP = 10.6 * m_SampFreq / 1000.0;
					m_SG = m_KS + 0.1;
					m_CG = m_SG + m_KS2;
					m_SB = m_CG + 0.1;
					m_CB = m_SB + m_KS2;
					m_L = 256;
					break;
				case AllModes.smMP73:
					m_KS = 140.0 * m_SampFreq / 1000.0;
					m_OF = 10.0 * m_SampFreq / 1000.0;
					m_OFP = 10.5 * m_SampFreq / 1000.0;
					m_SG = m_KS;
					m_CG = m_KS + m_SG;
					m_SB = m_SG + m_SG;
					m_CB = m_KS + m_SB;
					m_L = 128;
					break;
				case AllModes.smMP115:
					m_KS = 223.0 * m_SampFreq / 1000.0;
					m_OF = 10.0 * m_SampFreq / 1000.0;
					m_OFP = 10.5 * m_SampFreq / 1000.0;
					m_SG = m_KS;
					m_CG = m_KS + m_SG;
					m_SB = m_SG + m_SG;
					m_CB = m_KS + m_SB;
					m_L = 128;
					break;
				case AllModes.smMP140:
					m_KS = 270.0 * m_SampFreq / 1000.0;
					m_OF = 10.0 * m_SampFreq / 1000.0;
					m_OFP = 10.5 * m_SampFreq / 1000.0;
					m_SG = m_KS;
					m_CG = m_KS + m_SG;
					m_SB = m_SG + m_SG;
					m_CB = m_KS + m_SB;
					m_L = 128;
					break;
				case AllModes.smMP175:
					m_KS = 340.0 * m_SampFreq / 1000.0;
					m_OF = 10.0 * m_SampFreq / 1000.0;
					m_OFP = 10.5 * m_SampFreq / 1000.0;
					m_SG = m_KS;
					m_CG = m_KS + m_SG;
					m_SB = m_SG + m_SG;
					m_CB = m_KS + m_SB;
					m_L = 128;
					break;
				case AllModes.smML180:
					m_KS = 176.5 * m_SampFreq / 1000.0;
					m_KS2 = m_KS * 0.5;
					m_OF = 10.0 * m_SampFreq / 1000.0;
					m_OFP = 10.6 * m_SampFreq / 1000.0;
					m_SG = m_KS + 0.1;
					m_CG = m_SG + m_KS2;
					m_SB = m_CG + 0.1;
					m_CB = m_SB + m_KS2;
					m_L = 496;
					break;
				case AllModes.smML240:
					m_KS = 236.5 * m_SampFreq / 1000.0;
					m_KS2 = m_KS * 0.5;
					m_OF = 10.0 * m_SampFreq / 1000.0;
					m_OFP = 10.6 * m_SampFreq / 1000.0;
					m_SG = m_KS + 0.1;
					m_CG = m_SG + m_KS2;
					m_SB = m_CG + 0.1;
					m_CB = m_SB + m_KS2;
					m_L = 496;
					break;
				case AllModes.smML280:
					m_KS = 277.5 * m_SampFreq / 1000.0;
					m_KS2 = m_KS * 0.5;
					m_OF = 10.0 * m_SampFreq / 1000.0;
					m_OFP = 10.6 * m_SampFreq / 1000.0;
					m_SG = m_KS + 0.1;
					m_CG = m_SG + m_KS2;
					m_SB = m_CG + 0.1;
					m_CB = m_SB + m_KS2;
					m_L = 496;
					break;
				case AllModes.smML320:
					m_KS = 317.5 * m_SampFreq / 1000.0;
					m_KS2 = m_KS * 0.5;
					m_OF = 10.0 * m_SampFreq / 1000.0;
					m_OFP = 10.6 * m_SampFreq / 1000.0;
					m_SG = m_KS + 0.1;
					m_CG = m_SG + m_KS2;
					m_SB = m_CG + 0.1;
					m_CB = m_SB + m_KS2;
					m_L = 496;
					break;
				case AllModes.smR24:
					m_KS = 92.0 * m_SampFreq / 1000.0;
					m_KS2 = 46.0 * m_SampFreq / 1000.0;
					m_OF = 8.0 * m_SampFreq / 1000.0;
					m_OFP = 8.1 * m_SampFreq / 1000.0;
					m_SG = m_KS + 4.0 * m_SampFreq / 1000.0;
					m_CG = m_SG + m_KS2;
					m_SB = m_CG + 4.0 * m_SampFreq / 1000.0;
					m_CB = m_SB + m_KS2;
					m_L = 120;
					break;
				case AllModes.smRM8:
					m_KS = 58.89709 * m_SampFreq / 1000.0;
					m_OF = 8.0 * m_SampFreq / 1000.0;
					m_OFP = 8.2 * m_SampFreq / 1000.0;
					m_SG = m_KS;
					m_CG = m_KS + m_SG;
					m_SB = m_SG + m_SG;
					m_CB = m_KS + m_SB;
					m_L = 120;
					break;
				case AllModes.smRM12:
					m_KS = 92.0 * m_SampFreq / 1000.0;
					m_OF = 8.0 * m_SampFreq / 1000.0;
					m_OFP = 8.0 * m_SampFreq / 1000.0;
					m_SG = m_KS;
					m_CG = m_KS + m_SG;
					m_SB = m_SG + m_SG;
					m_CB = m_KS + m_SB;
					m_L = 120;
					break;
				case AllModes.smMN73:
					m_KS = 140.0 * m_SampFreq / 1000.0;
					m_OF = 10.0 * m_SampFreq / 1000.0;
					m_OFP = 10.5 * m_SampFreq / 1000.0;
					m_SG = m_KS;
					m_CG = m_KS + m_SG;
					m_SB = m_SG + m_SG;
					m_CB = m_KS + m_SB;
					m_L = 128;
					break;
				case AllModes.smMN110:
					m_KS = 212.0 * m_SampFreq / 1000.0;
					m_OF = 10.0 * m_SampFreq / 1000.0;
					m_OFP = 10.5 * m_SampFreq / 1000.0;
					m_SG = m_KS;
					m_CG = m_KS + m_SG;
					m_SB = m_SG + m_SG;
					m_CB = m_KS + m_SB;
					m_L = 128;
					break;
				case AllModes.smMN140:
					m_KS = 270.0 * m_SampFreq / 1000.0;
					m_OF = 10.0 * m_SampFreq / 1000.0;
					m_OFP = 10.5 * m_SampFreq / 1000.0;
					m_SG = m_KS;
					m_CG = m_KS + m_SG;
					m_SB = m_SG + m_SG;
					m_CB = m_KS + m_SB;
					m_L = 128;
					break;
				case AllModes.smMC110:
					m_KS = 140.0 * m_SampFreq / 1000.0;
					m_OF = 8.0 * m_SampFreq / 1000.0;
					m_OFP = 8.95 * m_SampFreq / 1000.0;
					m_SG = m_KS;
					m_CG = m_KS + m_SG;
					m_SB = m_SG + m_SG;
					m_CB = m_KS + m_SB;
					m_L = 256;
					break;
				case AllModes.smMC140:
					m_KS = 180.0 * m_SampFreq / 1000.0;
					m_OF = 8.0 * m_SampFreq / 1000.0;
					m_OFP = 8.75 * m_SampFreq / 1000.0;
					m_SG = m_KS;
					m_CG = m_KS + m_SG;
					m_SB = m_SG + m_SG;
					m_CB = m_KS + m_SB;
					m_L = 256;
					break;
				case AllModes.smMC180:
					m_KS = 232.0 * m_SampFreq / 1000.0;
					m_OF = 8.0 * m_SampFreq / 1000.0;
					m_OFP = 8.75 * m_SampFreq / 1000.0;
					m_SG = m_KS;
					m_CG = m_KS + m_SG;
					m_SB = m_SG + m_SG;
					m_CB = m_KS + m_SB;
					m_L = 256;
					break;

		//        case smSCT1:
				default:        // SCT1
					m_KS = 138.24 * m_SampFreq / 1000.0;
					m_OF = 10.5 * m_SampFreq / 1000.0;
					m_OFP = 10.7 * m_SampFreq / 1000.0;
					m_SG = 139.74 * m_SampFreq / 1000.0;
					m_CG = m_KS + m_SG;
					m_SB = m_SG + m_SG;
					m_CB = m_KS + m_SB;
					m_L = 256;
					break;
			}
			m_TW = GetTiming(m_Mode) * m_SampFreq / 1000.0;
			switch(m_Mode){
				case AllModes.smPD120:
				case AllModes.smPD160:
				case AllModes.smPD180:
				case AllModes.smPD240:
				case AllModes.smPD290:
				case AllModes.smP3:
				case AllModes.smP5:
				case AllModes.smP7:
					m_KSS = (m_KS - m_KS/480.0);      // TW for Y or RGB mode
					m_KS2S = (m_KS2 - m_KS2/480.0);   // TW for Ry, By
					m_KSB = (int)(m_KSS / 1280.0);          // TW for black adjutment
					break;
				case AllModes.smMP73:
				case AllModes.smMN73:
				case AllModes.smSCTDX:
					m_KSS = (m_KS - m_KS/1280.0);      // TW for Y or RGB mode
					m_KS2S = (m_KS2 - m_KS2/1280.0);   // TW for Ry, By
					m_KSB = (int)(m_KSS / 1280.0 );
					break;
				case AllModes.smSC2_180:
				case AllModes.smMP115:
				case AllModes.smMP140:
				case AllModes.smMP175:
				case AllModes.smMR90:
				case AllModes.smMR115:
				case AllModes.smMR140:
				case AllModes.smMR175:
				case AllModes.smML180:
				case AllModes.smML240:
				case AllModes.smML280:
				case AllModes.smML320:
				case AllModes.smMN110:
				case AllModes.smMN140:
				case AllModes.smMC110:
				case AllModes.smMC140:
				case AllModes.smMC180:
					m_KSS = m_KS;                   // TW for Y or RGB mode
					m_KS2S = m_KS2;                 // TW for Ry, By
					m_KSB = (int)(m_KSS / 1280.0);
					break;
				case AllModes.smMR73:
					m_KSS = (m_KS - m_KS/640.0);      // TW for Y or RGB mode
					m_KS2S = (m_KS2 - m_KS2/1024.0);  // TW for Ry, By
					m_KSB = (int)(m_KSS / 1024.0);
					break;
				default:
					m_KSS = (m_KS - m_KS/240.0);      // TW for Y or RGB mode
					m_KS2S = (m_KS2 - m_KS2/240.0);   // TW for Ry, By
					m_KSB = (int)(m_KSS / 640.0);          // TW for black adjutment
					break;
			}
			switch(m_Mode){
				case AllModes.smMRT1:
				case AllModes.smMRT2:
				case AllModes.smSC2_60:
				case AllModes.smSC2_120:
				case AllModes.smSC2_180:
    			case AllModes.smMC110:
				case AllModes.smMC140:
				case AllModes.smMC180:
					m_AFCW = (int)(2.0 * SampFreq / 1000.0);
					m_AFCB = (int)(1.0 * SampFreq / 1000.0);
					break;
				default:
					m_AFCW = (int)(3.0 * SampFreq / 1000.0);
					m_AFCB = (int)(1.5 * SampFreq / 1000.0);
					break;
			}
			if( m_KSB > 0 ) 
				m_KSB++;

			if( m_bCQ100 ) { // Used to be a global.
    			double d = m_OFP * 1000.0 / SampFreq;
				m_OFP = (d + (1100.0/g_dblToneOffset)) * SampFreq / 1000.0;
			}
			m_AFCE = m_AFCB + m_AFCW;
		}

		double GetTiming(AllModes mode) {
			switch(mode){
				case AllModes.smR36:
					return 150.0;
				case AllModes.smR72:
					return 300.0;
				case AllModes.smAVT:
					return 375;
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

		void SetTxSampFreq() {
			int dm1, dm2, iTL;
			GetPictureSize( out dm1, out dm2, out iTL, m_TxMode);
			m_TL = iTL;
			m_TTW = GetTiming(m_TxMode) * m_TxSampFreq / 1000.0;
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


	class CSmooz{
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

		int SyncCheck( CSSTVSET oTVSet )
		{
			UInt32 deff = (UInt32)(3 * oTVSet.m_SampFreq / 1000.0);
			UInt32 w = m_MSyncList[MSYNCLINE-1];

			for( UInt32 k = 1; k <= 3; k++ ){
				UInt32 ww = w / k;
				if( (ww > oTVSet.m_MSL) && (ww < oTVSet.m_MSH) ){
					for( int i = 0; i < (int)AllModes.smEND; i++ ){
						if( oTVSet.m_MS[i] != 0 && (ww > (oTVSet.m_MS[i]-deff)) && (ww < (oTVSet.m_MS[i]+deff)) ){
							if( SyncCheckSub(oTVSet, (AllModes)i) ){
								return i + 1;
							}
						}
					}
				} else {
					break;
				}
			}
			return 0;
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

		public int SyncStart( CSSTVSET oTVSettings )
		{
			int ss = 0;
			if( m_MSyncIntMax != 0 ){
				if( (m_MSyncIntPos - m_MSyncACnt) > oTVSettings.m_MSLL ){
					m_MSyncACnt = m_MSyncIntPos - m_MSyncACnt;
					//memcpy(m_MSyncList, &m_MSyncList[1], sizeof(int) * (MSYNCLINE - 1));
					Array.Copy( m_MSyncList, 1, m_MSyncList, 0, MSYNCLINE - 1 );
					m_MSyncList[MSYNCLINE - 1] = m_MSyncACnt;
					if( m_MSyncACnt > oTVSettings.m_MSL ){
						ss = SyncCheck( oTVSettings );
					}
					m_MSyncACnt = m_MSyncIntPos;
				}
				m_MSyncIntMax = 0;
			}
			return ss;
		}
	}

	class CSSTVDEM {
		protected readonly SYSSET sys;

		readonly static int NARROW_SYNC		= 1900;
		readonly static int NARROW_LOW		= 2044;
		readonly static int NARROW_HIGH		= 2300;
		readonly static int NARROW_CENTER	= ((NARROW_HIGH+NARROW_LOW)/2);
		readonly static int NARROW_BW		= (NARROW_HIGH - NARROW_LOW);
		readonly static int NARROW_BWH		= (NARROW_BW/2);
		readonly static int NARROW_BPFLOW	= 1600;
		readonly static int NARROW_BPFHIGH	= 2500;
		readonly static int NARROW_AFCLOW	= 1800;
		readonly static int NARROW_AFCHIGH	= 1950;

		public readonly static int TAPMAX = 512; // BUG: Move to Fir or Fir2 later.
		readonly static int SSTVDEMBUFMAX = 24;

		readonly CSSTVSET SSTVSET;

		double[]  HBPF  = new double[TAPMAX+1];
		double[]  HBPFS = new double[TAPMAX+1];
		double[]  HBPFN = new double[TAPMAX+1];

		protected class REPSET {    // リピータ用の設定
			public REPSET( double dbSampFreq ) {
				m_iirrep = new CIIRTANK( dbSampFreq );
				m_lpfrep = new CIIR();
			}

			public CIIRTANK    m_iirrep; // BUG: create these.
			public CIIR        m_lpfrep;
			public CLMS        m_lmsrep;
		}

		CFIR2	m_BPF;

		double   m_ad;
		int      m_OverFlow;
		int      m_bpf;
		int      m_bpftap;
		int      m_Type;
		int      m_LevelType;

		CIIRTANK m_iir11;
		CIIRTANK m_iir12;
		CIIRTANK m_iir13;
		CIIRTANK m_iir19;
		CIIRTANK m_iirfsk;
		CIIR     m_lpf11;
		CIIR     m_lpf12;
		CIIR     m_lpf13;
		CIIR     m_lpf19;
		CIIR     m_lpffsk;
		CPLL	 m_pll;
		CFQC     m_fqc;
		CLVL     m_lvl;
		CSLVL    m_SyncLvl;
		CHILL    m_hill;

		REPSET   pRep;

		int         m_Skip;
		bool        m_Sync;
		bool        m_SyncRestart;
		int         m_SyncMode;
		int         m_SyncTime;
		int         m_SyncATime;
		int         m_VisData;
		int         m_VisCnt;
		int         m_VisTrig;
		int         m_SyncErr;
		AllModes    m_NextMode;
		bool        m_SyncAVT;

		int         m_wBase;
		int         m_wPage;
		int         m_rPage;
		int         m_wCnt;
		int         m_wLine;
		int         m_wBgn;
		int         m_rBase;

		int         m_ReqSave;
		int         m_LoopBack;

		int         m_wStgPage;
		int         m_wStgLine;

		int         m_Lost;

		public int         m_BWidth;
		public short[]     m_Buf;
		public short[]     m_B12;

		public int         m_RxBufAllocSize;
		public short[]     m_StgBuf;
		public short[]     m_StgB12;

		int         m_SenseLvl;
		double      m_SLvl;
		double      m_SLvl2;
		double      m_SLvl3;

		bool        m_ScopeFlag;
		CScope[]    m_Scope = new CScope[2];

		CTICK       pTick;
		int         m_Tick;
		int         m_TickFreq;

		double      m_CurSig;
		CSmooz      m_Avg;
		CSmooz      m_AFCAVG;
		int         m_afc;
		int         m_AFCCount;
		double      m_AFCData;
		double      m_AFCLock;
		double      m_AFCDiff;
		int         m_AFCFQ;
		int         m_AFCFlag;
		int         m_AFCGard;
		int         m_AFCDis;
		int         m_AFCInt;

		double		m_AFC_LowVal;		// (Center - SyncLow) * 16384 / BWH
		double		m_AFC_HighVal;		// (Center - SyncHigh) * 16384 / BWH
		double		m_AFC_SyncVal;		// (Center - Sync) * 16384 / BWH
		double		m_AFC_BWH;			// BWH / 16384.0;

		double		m_AFC_OFFSET;		// 128

		bool        m_MSync;
		CSYNCINT    m_sint1;
		CSYNCINT    m_sint2;
		CSYNCINT	m_sint3;

		readonly static int FSKGARD   = 100;
		readonly static int FSKINTVAL = 22;
		readonly static int FSKSPACE  = 2100;

		bool        m_fskdecode;
		int         m_fskrec;
		int         m_fskmode;
		int         m_fsktime;
		int         m_fskcnt; // This gets used between m_fskdata & m_fskNRS 
		int         m_fskbcnt;
		int         m_fsknexti;
		double      m_fsknextd;
		byte        m_fsks;
		byte        m_fskc;
		List<byte>  m_fskdata = new List<byte>(20);
		List<char>  m_fskcall = new List<char>(20);
		int			m_fskNRrec;
		int			m_fskNR;
		List<char>	m_fskNRS  = new List<char>(20);

		//------ リピータ (repeater)
		int         m_Repeater;
		int         m_RepSQ;
		int         m_RepTone;
		int         m_repmode;
		int         m_reptime;
		int         m_repcount;
		int         m_repsig;
		int         m_repANS;
		int         m_repRLY;
		int         m_repRX;
		int         m_repTX;

		int         m_RSLvl;
		int         m_RSLvl2;

		bool        m_fNarrow;

		double  m_d;
		double  m_dd;
		int     m_n;

		int SampFreq { get; }
		// This is a global in the original code, but it's pretty clear
		// that we can't handle it changing mid run. So now it's a readonly
		// member variable.
		readonly double g_dblToneOffset;

		public CSSTVDEM( SYSSET p_sys, int iSampFreq, double dbToneOffset ) {
			sys = p_sys ?? throw new ArgumentNullException( "sys must not be null." );

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

			SampFreq = iSampFreq;
			g_dblToneOffset = dbToneOffset;

			m_bpf = 1;      // wide
			m_ad  = 0;

			m_StgBuf = null;
			m_StgB12 = null;
			m_BWidth = 1400 * SampFreq / 1000;
			int n = SSTVDEMBUFMAX * m_BWidth;
			m_Buf = new short[n];
			m_B12 = new short[n];

			Array.Clear( m_Buf, 0, m_Buf.Length );
			Array.Clear( m_B12, 0, m_Buf.Length );

			m_pll.SetSampleFreq(SampFreq);
			m_pll.SetVcoGain(1.0);
			m_pll.SetFreeFreq(1500, 2300);
			m_pll.m_loopOrder = 1;
			m_pll.m_loopFC = 1500;
			m_pll.m_outOrder = 3;
			m_pll.m_outFC = 900;
			m_pll.MakeLoopLPF();
			m_pll.MakeOutLPF();

			Array.Clear( HBPF,  0, HBPF.Length );
			Array.Clear( HBPFS, 0, HBPFS.Length );
			Array.Clear( HBPFN, 0, HBPFN.Length );
		//	memset(Z, 0, sizeof(Z));
			CalcBPF();

			m_iir11 .SetFreq(1080 + g_dblToneOffset, SampFreq, 80.0);
			m_iir12 .SetFreq(1200 + g_dblToneOffset, SampFreq, 100.0);
			m_iir13 .SetFreq(1320 + g_dblToneOffset, SampFreq, 80.0);
			m_iir19 .SetFreq(1900 + g_dblToneOffset, SampFreq, 100.0);
			m_iirfsk.SetFreq(FSKSPACE + g_dblToneOffset, SampFreq, 100.0);

			m_lpf11 .MakeIIR(50, SampFreq, 2, 0, 0);
			m_lpf12 .MakeIIR(50, SampFreq, 2, 0, 0);
			m_lpf13 .MakeIIR(50, SampFreq, 2, 0, 0);
			m_lpf19 .MakeIIR(50, SampFreq, 2, 0, 0);
			m_lpffsk.MakeIIR(50, SampFreq, 2, 0, 0);

			pRep = null;

			m_wPage = m_rPage = 0;
			m_wBase = 0;
			m_wCnt = 0;
			m_rBase = 0;
			m_Skip = 0;
			m_Sync = false;
			m_SyncMode = 0;
			m_ScopeFlag = false;
			m_LoopBack = 0;
			m_Lost = 0;

			m_lvl.m_agcfast = 1;
			m_afc = 1;

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

			m_MSync       = true;                              // Sync remote start ON
			m_SyncRestart = true;
			m_SyncAVT     = false;

			m_SenseLvl = 1;
			SetSenseLvl();

			m_Type = 2;
			m_ReqSave = 0;
			m_LevelType = 0;

			m_fskrec = 0;
			m_fskNRrec = 0;
			m_fskdecode = false;
			m_fskmode = 0;

			m_Repeater = 0;
			m_RepSQ = 6000;
			m_RepTone = 1750;
			m_repmode = 0;
			m_repANS = m_repRLY = m_repRX = m_repTX = 0;
			InitRepeater();
			SetRepSenseLvl();
		}

		public void Dispose() {
			m_B12 = null;
			m_Buf = null;
			FreeRxBuff();
			if( pRep != null ){
				pRep = null;
			}
		}

		public void CalcBPF(double[] H1, double[] H2, double[] H3, ref int bpftap, int bpf, AllModes mode)
		{
			int lfq  = (int)((m_SyncRestart ? 1100 : 1200) + g_dblToneOffset );
			int lfq2 = (int)(400 + g_dblToneOffset );
			if( lfq2 < 50 ) 
				lfq2 = 50;
			switch(bpf){
				case 1:     // Wide
					bpftap = (int)(24 * SampFreq / 11025.0 );
					CFIR2.MakeFilter(H1, bpftap, FirFilt.ffBPF, SampFreq, lfq, 2600 + g_dblToneOffset, 20, 1.0);
					CFIR2.MakeFilter(H2, bpftap, FirFilt.ffBPF, SampFreq,  lfq2, 2500 + g_dblToneOffset, 20, 1.0);
		//			MakeFilter(H3, bpftap, ffBPF, SampFreq,  NARROW_BPFLOW-200, NARROW_BPFHIGH, 20, 1.0);
					break;
				case 2:     // Narrow
					bpftap = (int)(64 * SampFreq / 11025.0 );
					CFIR2.MakeFilter(H1, bpftap, FirFilt.ffBPF, SampFreq, lfq, 2500 + g_dblToneOffset, 40, 1.0);
					CFIR2.MakeFilter(H2, bpftap, FirFilt.ffBPF, SampFreq,  lfq2, 2500 + g_dblToneOffset, 20, 1.0);
		//			MakeFilter(H3, bpftap, ffBPF, SampFreq, NARROW_BPFLOW-100, NARROW_BPFHIGH, 40, 1.0);
					break;
				case 3:     // Very Narrow
					bpftap = (int)(96 * SampFreq / 11025.0 );
					CFIR2.MakeFilter(H1, bpftap, FirFilt.ffBPF, SampFreq, lfq, 2400 + g_dblToneOffset, 50, 1.0);
					CFIR2.MakeFilter(H2, bpftap, FirFilt.ffBPF, SampFreq,  lfq2, 2500 + g_dblToneOffset, 20, 1.0);
		//			MakeFilter(H3, bpftap, ffBPF, SampFreq,  NARROW_BPFLOW, NARROW_BPFHIGH, 50, 1.0);
					break;
				default:
					bpftap = 0;
					break;
			}
			CalcNarrowBPF(H3, bpftap, bpf, mode);
		}

		public void CalcNarrowBPF(double[] H3, int bpftap, int bpf, AllModes mode) {
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
			low  += (int)g_dblToneOffset;
			high += (int)g_dblToneOffset;
			switch(bpf){
				case 1:     // Wide
					CFIR2.MakeFilter(H3, bpftap, FirFilt.ffBPF, SampFreq,  low-200, high, 20, 1.0);
					break;
				case 2:     // Narrow
					CFIR2.MakeFilter(H3, bpftap, FirFilt.ffBPF, SampFreq, low-100, high, 40, 1.0);
					break;
				case 3:     // Very Narrow
					CFIR2.MakeFilter(H3, bpftap, FirFilt.ffBPF, SampFreq,  low, high, 50, 1.0);
					break;
				default:
					break;
			}
		}

		public void CalcBPF() {
			CalcBPF(HBPF, HBPFS, HBPFN, ref m_bpftap, m_bpf, SSTVSET.m_Mode);
			m_BPF.Create(m_bpftap);
		}

		void SetBPF(int bpf) {
			if( bpf != m_bpf ){
				int delay = m_bpftap;
				m_bpf = bpf;
				CalcBPF();
				if( m_Sync ){
					delay = (m_bpftap - delay) / 2;
					m_Skip = delay;
				}
			}
		}

		void FreeRxBuff() {
			if( m_StgBuf != null ){
				m_StgBuf = null;
				m_StgB12 = null;
				m_wStgLine = 0;
			}
		}

		public void OpenCloseRxBuff() {
			if( m_Sync ) return;

			if( sys.m_UseRxBuff ){
				if( m_StgBuf == null ){
					int n = 257 * 1100 * SampFreq / 1000;
					m_StgBuf = new short[n];
					m_StgB12 = new short[n];
					//Array.Clear( m_StgBuf, 0, m_StgBuf.Length );
					//Array.Clear( m_StgB12, 0, m_StgB12.Length );
					m_RxBufAllocSize = n;
					m_wStgLine = 0;
				}
			} else {
				FreeRxBuff();
			}
		}

		void Idle(double d)	{
			if( !sys.m_TestDem ) 
				m_lvl.Do(d);
		}

		void SetTickFreq(int f)	{
			if( f == 0 ) 
				f = 1200;

			m_iir12.SetFreq(f + g_dblToneOffset, SampFreq, 100.0);
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
				m_iir11.SetFreq(1080+dfq + g_dblToneOffset, SampFreq, 80.0);
				m_iir12.SetFreq(1200+dfq + g_dblToneOffset, SampFreq, 100.0);
				m_iir13.SetFreq(1320+dfq + g_dblToneOffset, SampFreq, 80.0);
				m_iir19.SetFreq(1900+dfq + g_dblToneOffset, SampFreq, 100.0);
				m_iirfsk.SetFreq(FSKSPACE+dfq + g_dblToneOffset, SampFreq, 100.0);
				m_AFCFQ = dfq;
			}
		}

		void SetWidth( bool fNarrow) {
			if( m_fNarrow != fNarrow ){
				m_fNarrow = fNarrow;
				m_hill.SetWidth(fNarrow);
    			m_fqc .SetWidth(fNarrow);
				m_pll .SetWidth(fNarrow);
			}
		}

		void Start() {
			SetWidth(CSSTVSET.IsNarrowMode(SSTVSET.m_Mode));

			InitAFC();
			m_fqc.Clear();
			m_SyncMode = -1;
			m_Sync = false;
			m_Skip = 0;
			m_wPage = m_rPage = 0;
			m_wBase = 0;
			m_wLine = 0;
			m_wCnt = 0;
			m_rBase = 0;
			OpenCloseRxBuff();
			m_wBgn = 2;
			m_Lost = 0;

			int eg = SSTVSET.m_WD + SSTVSET.m_KSB + SSTVSET.m_KSB;
			int i, j;
			for( i = 0; i < SSTVDEMBUFMAX; i++ ){
				for( j = SSTVSET.m_WD; j < eg; j++ ){
					m_Buf[i*m_BWidth + j] = -16384;
				}
			}

			m_Sync     = true;
			m_SyncMode = 0;
			SetWidth(m_fNarrow);
			if( m_fNarrow ) 
				CalcNarrowBPF(HBPFN, m_bpftap, m_bpf, SSTVSET.m_Mode);
		}

		void Start(AllModes mode, int f)	{
			m_fqc.Clear();
			m_sint1.Reset();
			m_sint2.Reset();
			m_sint3.Reset();
			m_wBgn  = 0;
			m_rBase = 0;
			m_SyncMode = 0;
			SSTVSET.SetMode(mode);
			m_Sync = false;
			SetWidth(CSSTVSET.IsNarrowMode(mode));

			if( f != 0 ){
				Start();
			} else {
				m_SyncMode = -1;
			}
		}

		void Stop()	{
			if( m_AFCFQ != 0 ){
				if( m_fskdecode ){
					m_iir11.SetFreq(1080 + g_dblToneOffset, SampFreq, 80.0);
					m_iir12.SetFreq(1200 + g_dblToneOffset, SampFreq, 100.0);
					m_iir13.SetFreq(1320 + g_dblToneOffset, SampFreq, 80.0);
				} else {
					InitTone(0);
				}
			}
			m_fqc.Clear();
			m_sint1.Reset();
			m_sint2.Reset();
			m_sint3.Reset();
			m_wBgn = 0;
			m_SyncMode = 512;
			m_Sync = false;
			m_SyncAVT = false;
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

		void Do(double s) {
			if( (s > 24578.0) || (s < -24578.0) ){
				m_OverFlow = 1;
			}
			double d = (s + m_ad) * 0.5;    // LPF
			m_ad = s;
			if( m_bpf != 0 ){
				if( m_Sync || (m_SyncMode >= 3) ){
					// BUG: Double check this.
					d = m_BPF.Do( m_fNarrow ? HBPFN : HBPF, ref d, out _ );
				} else {
					d = m_BPF.Do( HBPFS, ref d, out _ );
				}
			}
			m_lvl.Do(d);
			double ad = m_lvl.AGC(d);

			d = ad * 32;
			if( d > 16384.0 ) d = 16384.0;
			if( d < -16384.0 ) d = -16384.0;

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

			DecodeFSK( (int)d19, (int)dsp );

			if( m_Repeater != 0 && !m_Sync && (pRep != null) ){
				double dsp2;
				dsp2 = pRep.m_iirrep.Do(d);
				if( dsp2 < 0.0 ) 
					dsp2 = -dsp2;
				dsp2 = pRep.m_lpfrep.Do(dsp2);
				if( m_RepSQ != 0 ){
					m_repsig = pRep.m_lmsrep.Sig(m_ad);
				}
				Repeater((int)dsp2, (int)d12, (int)d19);
			}

			if( m_fNarrow ){
				if( m_ScopeFlag ){
					m_Scope[0].WriteData(d19);
				}
				if( m_LevelType != 0 ) 
					m_SyncLvl.Do(d19);
			} else {
				if( m_ScopeFlag ){
					m_Scope[0].WriteData(d12);
				}
				if( m_LevelType != 0 ) 
					m_SyncLvl.Do(d12);
			}
			if( m_Tick != 0 && (pTick != null) ){
				pTick.Write(d12);
				return;
			}

			if( !m_Sync || m_SyncRestart || m_SyncAVT ){
				m_sint1.SyncInc();
				m_sint2.SyncInc();
				m_sint3.SyncInc();
				d11 = m_iir11.Do(d);
				if( d11 < 0.0 ) d11 = -d11;
				d11 = m_lpf11.Do(d11);

				switch(m_SyncMode){
					case 0:                 // 自動開始 : Start automatically
						if( !m_Sync && m_MSync ){
							m_VisData = m_sint1.SyncStart( SSTVSET );
							if( m_VisData > 0 ){
								SSTVSET.SetMode(m_VisData-1);
								Start();
							}
							else if( (d12 > d19) && (d12 > m_SLvl2) && ((d12-d19) >= m_SLvl2) ){
								m_sint2.SyncMax( (int)d12);
							}
							else {
								m_VisData = m_sint2.SyncStart( SSTVSET );
								if( m_VisData > 0 ){
									m_VisData--;
									switch(m_VisData){
										case AllModes.smSCT1:
										case AllModes.smMRT1:
										case AllModes.smMRT2:
										case AllModes.smSC2_180:
											SSTVSET.SetMode(m_VisData);
											Start();
											break;
										default:
											break;
									}
								}
							}
		//#if NARROW_SYNC == 1900
							if( (d19 > d12) && (d19 > dsp) && (d19 > m_SLvl3) && ((d19-d12) >= m_SLvl3) && ((d19-dsp) >= m_SLvl) ){
								if( m_sint3.m_SyncPhase != 0 ){
									m_sint3.SyncMax ( (int)d19); // TODO: check the type of d19
								} else {
									m_sint3.SyncTrig( (int)d19);
									m_sint3.m_SyncPhase++;
								}
							}
							else if( m_sint3.m_SyncPhase != 0 ){
								m_sint3.m_SyncPhase = 0;
								m_VisData = m_sint3.SyncStart(SSTVSET);
								if( m_VisData > 0 ){
									m_VisData--;
									SSTVSET.SetMode(m_VisData);
									Start();
								}
							}
		//#endif
						}
						if( (d12 > d19) && (d12 > m_SLvl) && ((d12-d19) >= m_SLvl) ){
							m_SyncMode++;
							m_SyncTime = (int)(15 * sys.m_SampFreq/1000);
							if( !m_Sync && m_MSync ) m_sint1.SyncTrig( (int)d12);
						}
						break;
					case 1:                 // 1200Hz(30ms)‚ÌŒp‘±ƒ`ƒFƒbƒN
						if( !m_Sync && m_MSync ){
							if( (d12 > d19) && (d12 > m_SLvl2) && ((d12-d19) >= m_SLvl2) ){
								m_sint2.SyncMax( (int)d12);
							}
						}
						if( (d12 > d19) && (d12 > m_SLvl) && ((d12-d19) >= m_SLvl) ){
							if( !m_Sync && m_MSync ){
								m_sint1.SyncMax( (int)d12);
							}
							m_SyncTime--;
							if( m_SyncTime == 0 ){
								m_SyncMode++;
								m_SyncTime = (int)(30 * sys.m_SampFreq/1000);
								m_VisData = 0;
								m_VisCnt = 8;
							}
						}
						else {
							m_SyncMode = 0;
						}
						break;
					case 2:                 // Vis decode
					case 9:
						d13 = m_iir13.Do(d);
						if( d13 < 0.0 ) d13 = -d13;
						d13 = m_lpf13.Do(d13);
						m_SyncTime--;
						if( m_SyncTime == 0 ){
							if( ((d11 < d19) && (d13 < d19)) ||
								(Math.Abs(d11-d13) < (m_SLvl2)) ){
								m_SyncMode = 0;
							}
							else {
								m_SyncTime = (int)(30 * sys.m_SampFreq/1000 );
								m_VisData = m_VisData >> 1;
								if( d11 > d13 ) m_VisData |= 0x0080;
								m_VisCnt--;
								if( m_VisCnt == 0 ){
									if( m_SyncMode == 2 ){
										m_SyncMode++;
										switch(m_VisData){
											case 0x82:      // RM8
												m_NextMode = AllModes.smRM8;
												break;
											case 0x86:      // RM12
												m_NextMode = AllModes.smRM12;
												break;
											case 0x84:      // R24
												m_NextMode = AllModes.smR24;
												break;
											case 0x88:      // R36
												m_NextMode = AllModes.smR36;
												break;
											case 0x0c:      // R72
												m_NextMode = AllModes.smR72;
												break;
											case 0x44:      // AVT
												m_NextMode = AllModes.smAVT;
												break;
											case 0x3c:      // SCT1
												m_NextMode = AllModes.smSCT1;
												break;
											case 0xb8:      // SCT2
												m_NextMode = AllModes.smSCT2;
												break;
											case 0xcc:      // SCTDX
												m_NextMode = AllModes.smSCTDX;
												break;
											case 0xac:      // MRT1
												m_NextMode = AllModes.smMRT1;
												break;
											case 0x28:      // MRT2
												m_NextMode = AllModes.smMRT2;
												break;
											case 0xb7:      // SC2-180 $37 00110111
												m_NextMode = AllModes.smSC2_180;
												break;
											case 0x3f:      // SC2-120 $3f 00111111
												m_NextMode = AllModes.smSC2_120;
												break;
											case 0xbb:      // SC2-60 $3b 10111011
												m_NextMode = AllModes.smSC2_60;
												break;
											case 0xdd:      // PD50 $5d  01011101
												m_NextMode = AllModes.smPD50;
												break;
											case 0x63:      // PD90 $63  01100011
												m_NextMode = AllModes.smPD90;
												break;
											case 0x5f:      // PD120 $5f  01011111
												m_NextMode = AllModes.smPD120;
												break;
											case 0xe2:      // PD160 $62  11100010
												m_NextMode = AllModes.smPD160;
												break;
											case 0x60:      // PD180 $60  01100000
												m_NextMode = AllModes.smPD180;
												break;
											case 0xe1:      // PD240 $61  11100001
												m_NextMode = AllModes.smPD240;
												break;
											case 0xde:      // PD290 $5e  11011110
												m_NextMode = AllModes.smPD290;
												break;
											case 0x71:      // P3 $71  01110001
												m_NextMode = AllModes.smP3;
												break;
											case 0x72:      // P5 $71  01110010
												m_NextMode = AllModes.smP5;
												break;
											case 0xf3:      // P7 $73  11110011
												m_NextMode = AllModes.smP7;
												break;
											case 0x23:      // MM Šg’£ VIS
												m_SyncMode = 9;
												m_VisData = 0;
												m_VisCnt = 8;
												break;
											default:
												m_SyncMode = 0;
												break;
										}
									}
									else {          // Šg’£ VIS
										m_SyncMode = 3;
										switch(m_VisData){
											case 0x45:      // MR73
												m_NextMode = AllModes.smMR73;
												break;
											case 0x46:      // MR90
												m_NextMode = AllModes.smMR90;
												break;
											case 0x49:      // MR115
												m_NextMode = AllModes.smMR115;
												break;
											case 0x4a:      // MR140
												m_NextMode = AllModes.smMR140;
												break;
											case 0x4c:      // MR175
												m_NextMode = AllModes.smMR175;
												break;
											case 0x25:      // MP73
												m_NextMode = AllModes.smMP73;
												break;
											case 0x29:      // MP115
												m_NextMode = AllModes.smMP115;
												break;
											case 0x2a:      // MP140
												m_NextMode = AllModes.smMP140;
												break;
											case 0x2c:      // MP175
												m_NextMode = AllModes.smMP175;
												break;
											case 0x85:      // ML180
												m_NextMode = AllModes.smML180;
												break;
											case 0x86:      // ML240
												m_NextMode = AllModes.smML240;
												break;
											case 0x89:      // ML280
												m_NextMode = AllModes.smML280;
												break;
											case 0x8a:      // ML320
												m_NextMode = AllModes.smML320;
												break;
											default:
												m_SyncMode = 0;
												break;
										}
									}
								}
							}
						}
						break;
					case 3:                 // 1200Hz(30ms)‚Ìƒ`ƒFƒbƒN
						if( !m_Sync ){
							m_pll.Do(ad);
						}
						m_SyncTime--;
						if( m_SyncTime == 0 ){
							if( (d12 > d19) &&(d12 > m_SLvl) ){
								if( m_Sync ){
									if( m_rBase >= (SSTVSET.m_LM * 65/100) ){
										m_ReqSave = 1;
									}
								}
								if( m_NextMode == AllModes.smAVT ){
									m_SyncTime = (int)((9 + 910 + 910 + 5311.9424 + 0.30514375) * sys.m_SampFreq / 1000.0);
									m_SyncMode++;
									m_SyncAVT = true;
									m_Sync = false;
								}
								else {
									m_SyncMode = 256;
								}
								SSTVSET.SetMode(m_NextMode);
							}
							else {
								m_SyncMode = 0;
							}
						}
						break;
					case 4:                 // AVT‚Ì1900HzM†‘Ò‚¿
						m_SyncTime--;
						if( m_SyncTime == 0 ) { 
							m_SyncMode = 256; 
							break;
						}

						d = m_pll.Do(ad);
						if( (d >= -1000) && (d <= 1000) ){        // First atack
							m_SyncMode++;
							m_SyncATime = (int)(9.7646 * 0.5 * sys.m_SampFreq / 1000);
						}
						break;
					case 5:
						m_SyncTime--;
						if( m_SyncTime == 0 ) { 
							m_SyncMode = 256; 
							break;
						}

						d = m_pll.Do(ad);
						if( (d >= -800) && (d <= 800) ){        // 2nd atack
							m_SyncATime--;
							if( m_SyncATime == 0 ){
								m_SyncMode++;
								m_SyncATime = (int)(9.7646 * sys.m_SampFreq / 1000);
								m_VisData = 0;
								m_VisCnt = 16;
							}
						} else {
							m_SyncMode = 4;
						}
						break;
					case 6:
						m_SyncTime--;
						if( m_SyncTime == 0 ) { 
							m_SyncMode = 256; 
							break;
						}

						d = m_pll.Do(ad);
						m_SyncATime--;
						if( m_SyncATime == 0 ){
							if( (d >= 8000)||(d < -8000) ){
								m_SyncATime = (int)(9.7646 * sys.m_SampFreq / 1000);
								m_VisData = m_VisData << 1;
								if( d > 0 ) m_VisData |= 0x00000001;
								m_VisCnt--;
								if( m_VisCnt == 0 ){
									int l = m_VisData & 0x00ff;
									int h = (m_VisData >> 8) & 0x00ff;
									if( ((l + h) == 0x00ff) && (l >= 0xa0) && (l <= 0xbf) && (h >= 0x40) && (h <= 0x5f)  ){
										if( h != 0x40 ){
											m_SyncATime = (int)(9.7646 * 0.7 * sys.m_SampFreq / 1000);
											m_SyncTime =  (int)((((double)(h - 0x40) * 165.9982) - 0.8) * sys.m_SampFreq / 1000 );
											m_SyncMode++;
										} else {
											if( m_SyncTime == 0 || (m_SyncTime >= 9.7646 * SampFreq / 1000) ){
												m_SyncTime = (int)(((9.7646 * 0.5) - 0.8) * sys.m_SampFreq / 1000 );
											}
											m_SyncMode = 8;
										}
									} else {
										m_SyncMode = 4;
									}
								}
							} else {
								m_SyncMode = 4;
							}
						}
						break;
					case 7:         // “¯Šú
						d = m_pll.Do(ad);
						if( (d >= -1000) && (d <= 1000) ){        // First atack
							m_SyncMode = 5;
							m_SyncATime = (int)(9.7646 * 0.5 * sys.m_SampFreq / 1000);
						}
						else {
							m_SyncATime--;
							if( m_SyncATime == 0 ){
								m_SyncMode = 4;
							}
						}
						break;
					case 8:
						m_SyncMode--;
						if( m_SyncMode == 0 ){
							Start();
						}
						break;
					case 256:               // ‹­§ŠJŽn
						Start();
						break;
					case 512:               // 0.5s‚ÌƒEƒGƒCƒg
						m_SyncTime = (int)(SampFreq * 0.5);
						m_SyncMode++;
						break;
					case 513:
						m_SyncTime--;
						if( m_SyncTime == 0 ){
							m_SyncMode = 0;
						}
						break;
				}
			}
			if( m_Sync ){
				switch(m_Type){
					case 0:		// PLL
						if( m_afc && (m_lvl.m_CurMax > 16) && (SSTVSET.m_Mode != AllModes.smAVT) )
							SyncFreq(m_fqc.Do(m_lvl.m_Cur));
						d = m_pll.Do(m_lvl.m_Cur);
						break;
					case 1:		// Zero-crossing
						d = m_fqc.Do(m_lvl.m_Cur);
						if( m_afc && (m_lvl.m_CurMax > 16) && (SSTVSET.m_Mode != AllModes.smAVT) )
							SyncFreq(d);
						break;
					default:	// Hilbert
						d = m_hill.Do(m_lvl.m_Cur);
						if( m_afc && (m_lvl.m_CurMax > 16) && (SSTVSET.m_Mode != AllModes.smAVT) )
							SyncFreq(d);
						break;
				}
				if( m_afc != 0 ) 
					d += m_AFCDiff;
				if( m_Skip != 0 ) {
					if( m_Skip > 0 ){
						m_Skip--;
					}
					else {
						for( ; m_Skip != 0; m_Skip++ ){
							int n = m_wBase + m_wCnt;
							m_Buf[n] = (short)-d;
							m_B12[n] = 0;
							IncWP();
						}
					}
				}
				else {
					if( m_ScopeFlag ){
						m_Scope[1].WriteData(d);
					}
					int n = m_wBase + m_wCnt;
					m_Buf[n] = (short)-d;

		//#if NARROW_SYNC == 1200
		//			if( SSTVSET.m_Mode != AllModes.smAVT ){
		//				m_B12[n] = (short)d12;
		//			}
		//#else
					if( m_fNarrow ){
						m_B12[n] = (short)d19;
					}
					else if( SSTVSET.m_Mode != AllModes.smAVT ){
						m_B12[n] = (short)d12;
					}
		//#endif
					else {
						m_B12[n] = (short)((d + 16384) * 0.25);
					}
					IncWP();
				}
			}
			else if( sys.m_TestDem ){
				switch(m_Type){
					case 0:
						m_CurSig = m_Avg.Avg(m_pll.Do(m_lvl.m_Cur));
						break;
					case 1:
						m_CurSig = m_Avg.Avg(m_fqc.Do(m_lvl.m_Cur));
						break;
					default:
						m_CurSig = m_Avg.Avg(m_hill.Do(m_lvl.m_Cur));
						break;
				}
			}
		}

		void IncWP() {
			m_wCnt++;
			if( m_wCnt >= SSTVSET.m_WD ){
				m_wCnt = 0;
				m_wPage++;
				m_wLine++;
				m_wBase += m_BWidth;
				if( m_wPage >= SSTVDEMBUFMAX ){
					m_wPage = 0;
					m_wBase = 0;
				}
			}
		}

		void SyncFreq(double d) {
		/*
			double		m_AFC_LowVal;	// (Center - SyncLow) * 16384 / BWH
			double		m_AFC_HighVal;	// (Center - SyncHigh) * 16384 / BWH
			double		m_AFC_SyncVal;	// (Center - Sync) * 16384 / BWH
			double		m_AFC_BWH;		// BWH / 16384.0;
		*/
			d -= 128;

			if( (d <= m_AFC_LowVal) && (d >= m_AFC_HighVal) ){
				if( m_AFCDis == 0 && (m_AFCCount >= SSTVSET.m_AFCB) && (m_AFCCount <= SSTVSET.m_AFCE) ){
					m_AFCData = m_Avg.Avg(d);
					if( m_AFCCount == SSTVSET.m_AFCE ){
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
				if( (m_AFCCount >= SSTVSET.m_AFCB) && m_AFCGard != 0 ){
					m_AFCGard--;
					if( m_AFCGard == 0 ) 
						m_AFCAVG.SetData(m_AFCLock);
				}
				m_AFCCount = 0;
				if( m_AFCDis != 0 )
					m_AFCDis--;
			}
		}

		void DecodeFSK( int m, int s ) {
			int d;
			switch(m_fskmode){
				case 0:         // スペースキャリア検出
					d = Math.Abs(m - s);
					if( (s > m) && (d >= 2048) ){
						m_fsktime = (int)((FSKGARD/2) * SSTVSET.m_SampFreq/1000);
						m_fskmode++;
					}
					break;
				case 1:          // スペースキャリア検出(連続)
					d = Math.Abs(m - s);
					if( (s > m) && (d >= 2048) ){
						m_fsktime--;
						if( m_fsktime == 0 ){
							m_fsktime = (int)(FSKGARD * SSTVSET.m_SampFreq/1000);
							m_fskmode++;
						}
					}
					else {
						m_fskmode = 0;
					}
					break;
				case 2:         // スタートビットの検出
					d = Math.Abs(m - s);
					m_fsktime--;
					if( m_fsktime == 0 ){
						m_fskmode = 0;
					}
					else if( (m > s) && (d >= 2048) ){
						m_fsktime = (int)((FSKINTVAL/2) * SSTVSET.m_SampFreq/1000);
						m_fskmode++;
					}
					break;
				case 3:         // スタートビットの検出(中間点)
					m_fsktime--;
					if( m_fsktime == 0 ){
						d = Math.Abs(m - s);
						if( (m > s) && (d >= 2048) ){
							m_fsktime = 0;
							m_fsknextd = (double)((FSKINTVAL)/1000.0 * SSTVSET.m_SampFreq);
							m_fsknexti = (int)m_fsknextd;
							m_fskbcnt = 0;
							m_fskc = 0;
							m_fskmode++;
						}
						else {
							m_fskmode = 0;
						}
					}
					break;
				default:
					m_fsktime++;
					if( m_fsktime >= m_fsknexti ){
						d = Math.Abs(m - s);
						if( d < 2048 ){
							m_fskmode = 0;
						}
						else {
							m_fsknextd += (double)((FSKINTVAL)/1000.0 * SSTVSET.m_SampFreq);
							m_fsknexti = (int)m_fsknextd;
							m_fskc = (byte)(m_fskc >> 1);
							if( m > s ) m_fskc |= 0x20;
							m_fskbcnt++;
							if( m_fskbcnt >= 6 ){
								m_fskbcnt = 0;
								switch(m_fskmode){
									case 4:         // First SYNC 0x2A
										if( m_fskc == 0x2a ){
											m_fskcnt = 0;
											m_fskbcnt = 0;
											m_fsks = 0;
											m_fskc = 0;
											m_fskmode++;
										}
										else if( m_fskc == 0x2d ){
											m_fskcnt = 0;
											m_fskbcnt = 0;
											m_fsks = 0;
											m_fskc = 0;
											m_fskmode = 16;
										}
										else {
											m_fskmode = 0;
										}
										break;
									case 5:         // Store data
										if( m_fskc == 0x01 ){
											if( m_fskcnt >= 1 ){
												m_fskmode++;
											}
											else {
												m_fskmode = 0;
											}
										}
										else {
											m_fsks = (byte)(m_fskc ^ m_fsks); // XOR
											m_fskdata.Add( (byte)(m_fskc + 0x20) );
											if( m_fskdata.Count >= 17 ){
												m_fskmode = 0;
											}
										}
										break;
									case 6:         // Check XOR
										m_fsks &= 0x3f;
										if( (m_fskc == m_fsks) && m_fskdecode ){
											//m_fskdata[m_fskcnt] = 0;
											foreach( byte b in m_fskdata )
												m_fskcall.Add( (char)b );
											//StrCopy(m_fskcall, SkipSpace(LPCSTR(m_fskdata)), 16);
											//clipsp(m_fskcall);
											m_fskrec = 1;
											m_fskmode++;

											m_fskcnt = 0;
											m_fsks = 0;
											m_fskc = 0;
										} else {
											m_fskmode = 0;
										}
										break;
									case 7:         // Store data
										if( m_fskc == 0x01 ){
											if( m_fskcnt >= 1 ){
												m_fskmode++;
											}
											else {
												m_fskmode = 0;
											}
										}
										else if( m_fskc == 0x02 ){
											m_fsks = 0x02;
											m_fskNR = 0;
											m_fskmode = 9;
										}
										else if( m_fskc >= 0x10 ){
											m_fsks = (byte)(m_fskc ^ m_fsks );
											m_fskNRS.Add( (char)(m_fskc + 0x20) );
											m_fskcnt++;
											if( m_fskcnt >= 9 ){
												m_fskmode = 0;
											}
										}
										else {
											m_fskmode = 0;
										}
										break;
									case 8:         // Check XOR
										m_fsks &= 0x3f;
										if( (m_fskc == m_fsks) && m_fskdecode ){
											//m_fskNRS[m_fskcnt] = (char)0;
											//clipsp(m_fskNRS);
											m_fskNRrec = 1;
										}
										m_fskmode = 0;
										break;
									case 9:
										m_fsks  = (byte)(m_fskc ^ m_fsks);
										m_fskNR = m_fskNR << 6;
										m_fskNR += m_fskc;
										m_fskcnt++;
										if( m_fskcnt >= 2 ){
											m_fskmode++;
										}
										break;
									case 10:
										m_fsks &= 0x3f;
										if( m_fskc == m_fsks ){
											foreach( char c in m_fskNR.ToString() )
												m_fskNRS.Add( c );
											m_fskNRrec = 1;
										}
										m_fskmode = 0;
										break;
									case 16:
										m_fsks = (byte)(m_fskc ^ m_fsks);
										if( m_fskc == 0x15 ){
											m_fskmode++;
										}
										else {
											m_fskmode = 0;
										}
                            			break;
									case 17:
										m_fsks = (byte)(m_fskc ^ m_fsks);
										m_fskdata[0] = m_fskc;
										m_fskmode++;
                            			break;
									case 18:
										m_fsks &= 0x3f;
										if( m_fskc == m_fsks ){
											switch( m_fskdata[0] ) {
												case 0x02:
													m_NextMode = AllModes.smMN73;
													break;
												case 0x04:
													m_NextMode = AllModes.smMN110;
													break;
												case 0x05:
													m_NextMode = AllModes.smMN140;
													break;
												case 0x14:
													m_NextMode = AllModes.smMC110;
													break;
												case 0x15:
													m_NextMode = AllModes.smMC140;
													break;
												case 0x16:
													m_NextMode = AllModes.smMC180;
													break;
												default:
													m_NextMode = 0;
													break;
											}
											// BUG: You can never get to AllModes.smR36 because it is zero!!
											if( (m_SyncRestart || !m_Sync) && m_NextMode != 0 && (m_SyncMode >= 0) ){
												SSTVSET.SetMode(m_NextMode);
												Start();
											}
										}
										m_fskmode = 0;
										break;
								}
								m_fskc = 0;
							}
						}
					}
					break;
			}
		}

		// リピータ変数の初期化 : Initialization of repeater variables 
		void InitRepeater(){
			if( sys.m_Repeater ) {
				if( pRep == null )
					pRep = new REPSET( SampFreq );
				pRep.m_iirrep.SetFreq(m_RepTone + g_dblToneOffset, SampFreq, 100.0);
				pRep.m_lpfrep.MakeIIR(50, SampFreq, 2, 0, 0);
			} else {
				pRep = null;
			}
		}

		//  リピータのON/OFF
		void SetRepeater(int sw) {
			if( sw != m_Repeater ){
				m_repmode  = 0;
				m_Repeater = sw;
			}
		}

		// リピータトーンの検出感度設定 : Repeater tone detection sensitivity setting
		void SetRepSenseLvl() {
			switch(sys.m_RepSenseLvl){
				case 0:
					m_RSLvl = 3072;
					break;
				case 1:
					m_RSLvl = 4096;
					break;
				case 2:
					m_RSLvl = 6144;
					break;
				default:
					m_RSLvl = 8192;
					break;
			}
			m_RSLvl2 = m_RSLvl / 2;
		}

		// リピータトーンの検出処理
		void Repeater(int d17, int d12, int d19) {
			int d1 = Math.Abs(d17 - d12);
			int d2 = Math.Abs(d17 - d19);

			switch(m_repmode){
				case 0:     // トーン検出のトリガ
					if( (d1 > m_RSLvl) && (d2 > m_RSLvl2) ){
						m_reptime = (int)(sys.m_RepTimeA * SSTVSET.m_SampFreq / 1000);
						m_repmode++;
					}
					break;
				case 1:     // トーンの持続のチェック
					if( (d1 > m_RSLvl) && (d2 > m_RSLvl2) ){
						m_reptime--;
						if( m_reptime == 0 ) {
							m_repmode++;
							m_repcount = (int)(10000 * SSTVSET.m_SampFreq / 1000);
						}
					} else {
						m_repmode = 0;
					}
					break;
				case 2:     // ƒg[ƒ“‚ÌI—¹‚ÌŒŸo
					m_repcount--;
					if( m_repcount == 0 ){
						m_repmode = 0;
					}
					if( (d1 > m_RSLvl) && (d2 > m_RSLvl2) ){
						m_reptime  = (int)(sys.m_RepTimeB * SSTVSET.m_SampFreq / 1000);
						m_repcount = (int)(10000 * SSTVSET.m_SampFreq / 1000);
						if( m_reptime == 0 ) 
							m_reptime++;
					}
					else if( m_RepSQ != 0 && (m_repsig > m_RepSQ) ){
						m_reptime = (int)(sys.m_RepTimeB * SSTVSET.m_SampFreq / 1000);
						if( m_reptime == 0 )
							m_reptime++;
					}
					else {
						m_reptime--;
						if( m_reptime == 0 ){
							m_repmode++;
						}
					}
					break;
		//        case 3:     // 'K'‚Ì‘—M‘Ò‚¿
		//            break;
				case 4:
					m_reptime  = (int)(sys.m_RepTimeC * SSTVSET.m_SampFreq / 1000 );
					m_repcount = (int)(sys.m_RepTimeA * SSTVSET.m_SampFreq / 1000 );
					m_repmode++;
					break;
				case 5:       // 10[s]‚Ìƒ^ƒCƒ€ƒAƒEƒg‘Ò‚¿
					m_reptime--;
					if( m_reptime == 0 ) {       // ƒ^ƒCƒ€ƒAƒEƒg‚É‚æ‚é‘Ò‹@
						m_repmode = 0;
					}
					else if( (d1 > m_RSLvl) && (d2 > m_RSLvl2) ){
						m_repcount--;
						if( m_repcount == 0 ){
							m_repmode = 2;
						}
					} else {
						m_repcount = (int)(sys.m_RepTimeA * SSTVSET.m_SampFreq / 1000);
					}
					break;
		//        case 6:     // ŽóM’†
		//            break;
				case 7:
					m_reptime  = (int)(sys.m_RepTimeD * SSTVSET.m_SampFreq / 1000 );
					m_repcount = (int)(20000 * SSTVSET.m_SampFreq / 1000 );
					if( m_reptime == 0 ) m_reptime++;
					m_repmode++;
					break;
				case 8:        // リプレイ送信前のタイマ
					m_repcount--;
					if( m_repcount == 0 ){
						m_repmode = 0;
					}
					if( m_RepSQ != 0 && (m_repsig > m_RepSQ) ){
						m_reptime = (int)(sys.m_RepTimeD * SSTVSET.m_SampFreq / 1000);
						if( m_reptime == 0 ) 
							m_reptime++;
					} else {
						m_reptime--;
						if( m_reptime == 0 ) {
							m_repmode++;
						}
					}
					break;
		//        case 9:       // 送信トリガ
		//            break;
		//        case 10:      // リプレイ送信中
		//            break;
				default:
					break;
			}
		}
	}
}
