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
using System.Text;

using SkiaSharp;

namespace Play.Sound {
	/// <summary>
	/// This object represents system settings that can change while the 
	/// SSTVDEM object is in use. Note, frequency is not one of the items.
	/// If you change the frequency you must re-alloc the SSTVDEM object.
	/// </summary>
	public class SYSSET {
#if false
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
#endif

	  //public bool   m_TestDem  { get; protected set; } = false; // used
		public double m_DemOff   { get; } = 0;
		public double m_DemWhite { get; } = 128.0/16384.0;
		public double m_DemBlack { get; } = 128.0/16384.0;
		public bool   m_DemCalibration { get; } = false; // see SSTVDraw.pCalibration

		// UseRxBuff : can be CWaveStrage: 2 or m_StgBuf: 1 or off.
		public int  m_UseRxBuff { get; protected set; } = 2; 
		public bool m_AutoStop  { get; protected set; } = false; // used
		public bool m_AutoSync  { get; protected set; } = true;
				  
#if faose
		int      m_TXFSKID;

		string[] m_TextList = new string[16];

		int      m_PicSelRTM;
		int      m_PicSelSmooz;

		int      m_Differentiator;
		double   m_DiffLevelP;
		double   m_DiffLevelM;
#endif

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

	public enum AllSSTVModes {
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
		smWWV
	}

	public enum BandPass {
		Undefined = -1,
		Wide = 1,
		Narrow = 2,
		VeryNarrow = 3
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
	/// Ported over in preparation for the input level display. I might
	/// turn this one back into a class so I can implement a dummy in the case
	/// that the Level Display is showing the Receipt signals and not the Sync.
	/// </summary>
	struct CSLVL {
		double  m_Max;
		double  m_Min;
		public double  m_Lvl { get; private set; } 

		         int m_Cnt;
		readonly int m_CntMax;

		public CSLVL( double dblSampFreq ){
			m_CntMax = (int)(dblSampFreq * 100 / 1000.0);

			m_Max = 0;
			m_Min = 16384;
			m_Lvl = 0;
			m_Cnt = 0;
		}
		public void Do(double d){
			if( m_Max < d ) 
				m_Max = d;
			if( m_Min > d ) 
				m_Min = d;

			m_Cnt++;
		}
		public void Fix() {
			if( m_Cnt < m_CntMax ) 
				return;	// did not store yet
			m_Cnt = 0;
			m_Lvl = m_Max - m_Min;
			m_Max = 0;
			m_Min = 16384;
		}

		double GetLvl(){
			return m_Lvl;
		}
	}

	public enum LevelDisplay {
		Receipt,
		Sync
	}

	struct CLVL	{
		public double m_PeakMax { get; private set; } 
		double m_PeakAGC;
		double m_Peak;
		public double m_CurMax { get; private set; }
		double m_Max;
		double m_agc;
		int    m_CntPeak;
		bool   m_agcfast;

		int    m_Cnt;
		int    m_CntMax;

		public CLVL( int iSampFreq, bool fAgcFast = false ){
			m_agcfast = fAgcFast;
			m_CntMax  = (int)(iSampFreq * 100 / 1000.0);

			m_PeakMax = 0;
			m_PeakAGC = 0;
			m_Peak    = 0;
		    m_CurMax  = 0.0;
			m_Max     = 0;
			m_agc     = 1.0;
			m_CntPeak = 0;
			m_Cnt     = 0;
		}

		public void Do(double d ){
			if( d < 0.0 ) 
				d = -d;
			if( m_Max < d ) 
				m_Max = d;

			m_Cnt++;
		}

		public void Fix(){
			if( m_Cnt < m_CntMax ) 
				return;	// did not store yet

			m_Cnt = 0;
			m_CntPeak++;
			if( m_Peak < m_Max ) 
				m_Peak = m_Max;

			if( m_CntPeak >= 5 ) {
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

	public class FrequencyLookup {
		public int SYNC	   { get; protected set; }
		public int LOW	   { get; protected set; }
		public int HIGH    { get; protected set; }
	  //public int BPFLOW  { get; protected set; }
	  //public int BPFHIGH { get; protected set; }
		public int AFCLOW  { get; protected set; }
		public int AFCHIGH { get; protected set; }
		public double CENTER  { get; protected set; } // Next three MUST be double precision.
		public double BW      { get; protected set; } // we loose fidelity if not.
		public double BWHalf  { get; protected set; }

		private readonly double _dblQuarterSample = 16384.0;
		public bool CQ100 { get; private set; }

		public FrequencyLookup( bool bCQ100 ) {
			CQ100 = bCQ100;
		}

		public void InitNew() {
			CENTER = (HIGH + LOW)/2;
			BW	   =  HIGH - LOW;
			BWHalf = BW/2;
		}

		public double AFC_SyncVal => ( CENTER - SYNC ) * _dblQuarterSample / BWHalf;
		public double AFC_BWH     => BWHalf / _dblQuarterSample;

		public double AFC_LowVal { 
			get {			
				if( CQ100 ) {
					return (CENTER - SYNC - 50) * _dblQuarterSample / BWHalf;
				} else {
					return (CENTER - AFCLOW   ) * _dblQuarterSample / BWHalf;
				}
			}
		}
		public double AFC_HighVal { 
			get {			
				if( CQ100 ) {
					return (CENTER - SYNC + 50) * _dblQuarterSample / BWHalf;
				} else {
					return (CENTER - AFCHIGH  ) * _dblQuarterSample / BWHalf;
				}
			}
		}
	}

	public class LookupNormal : FrequencyLookup {
		public LookupNormal( bool bCQ100 ) : base( bCQ100 ) {
			SYNC	= 1200; 
			LOW		= 1500; // Band pass of video info.
			HIGH	= 2300; 
			AFCLOW	= 1000; // Not sure about this one yet.
			AFCHIGH	= 1325;

			InitNew();
		}
	}

	public class LookupNarrow : FrequencyLookup {
		public LookupNarrow( bool bCQ100 ) : base( bCQ100 ) {
			SYNC	= 1900;
			LOW		= 2044; 
			HIGH	= 2300;
			AFCLOW	= 1800; 
			AFCHIGH	= 1950;

			InitNew();
		}
	}

	public class SYNCINT {
		class ScanInfo {
			public ScanInfo( SSTVMode oMode ) {
				LegacyMode = oMode.LegacyMode;
				SyncWidth  = (uint)oMode.ScanWidthInMS;
				e          = 0;
				BailOut    = true;
			}

			public readonly AllSSTVModes LegacyMode;
			public readonly uint     SyncWidth;
			public          int      e;
			public          bool     BailOut; // Don't check this one.

			/// <summary>
			/// So MMSSTV used to walk this everytime a sample comes through.
			/// Turns out it only needs to be initialized once and then only
			/// changed if we go into narrow modes.
			/// </summary>
			/// <param name="iSyncCount">m_rgMSyncList.Length</param>
			public void ResetE( bool fNarrow, int iSyncCount ) {
				BailOut = false;
				switch( LegacyMode ){
					case AllSSTVModes.smSC2_60:
					case AllSSTVModes.smSC2_120:
						BailOut = true;
						break;
					case AllSSTVModes.smR24:
					case AllSSTVModes.smR36:
					case AllSSTVModes.smMRT2:
					case AllSSTVModes.smPD50:
					case AllSSTVModes.smPD240:
						if( fNarrow ) 
							BailOut = true;
						e = iSyncCount - 4;
						break;
					case AllSSTVModes.smRM8:
					case AllSSTVModes.smRM12:
						if( fNarrow ) 
							BailOut = true;
						e = 0;
						break;
					case AllSSTVModes.smMN73:
					case AllSSTVModes.smMN110:
					case AllSSTVModes.smMN140:
					case AllSSTVModes.smMC110:
					case AllSSTVModes.smMC140:
					case AllSSTVModes.smMC180:
						if( !fNarrow ) 
							BailOut = true;
						e = iSyncCount - 5;
        				break;
					default:
						if( fNarrow ) 
							BailOut = true;
						e = iSyncCount - 3;
						break;
				}
			}
		}

		readonly List<ScanInfo> m_MScanInfo;

		readonly uint m_MSLL;
		readonly uint m_MSL;
		readonly uint m_MSH;
		readonly long m_lDeff;

		readonly uint[] m_rgMSyncList = new uint[8];

		uint m_MSyncCnt;
		uint m_MSyncACnt;
		uint m_MSyncIntPos;
		uint m_MSyncIntMax;
		bool m_fNarrow;

		readonly double m_SampPerMs;

		/// <summary>
		/// This is the port of the CSYNCINT class from C++. This code helps us
		/// determine the image type in the case we miss the VIS. 
		/// </summary>
		/// <remarks>
		/// So the MS array which has the scan line timing information, used to
		/// live on the CSSTVSET object. I think it belongs here where it is used.
		/// Also, when you think about it, it seems that values are not something 
		/// that would change... We can't calculate slant until we've seen enough 
		/// of the signal. But slant detection only happens after VIS OR the SYNCINT 
		/// code executes. That makes me think we only need to update this object 
		/// if you change your sampling frequency. Hence the readonly array.
		/// </remarks>
		public SYNCINT( SSTVDEM oDemod ){
			m_fNarrow   = false;
			m_SampPerMs = oDemod.SampFreq / 1000;
			m_lDeff     = (long)(3 * m_SampPerMs);
			m_MScanInfo = new List<ScanInfo>( (int)AllSSTVModes.smEND );

			Reset( false );

			// From CSSTVSET::InitIntervalPara() see remarks above...
			foreach( SSTVMode oMode in oDemod ) {
				ScanInfo sInfo = new ScanInfo( oMode );

				sInfo.ResetE( m_fNarrow, m_rgMSyncList.Length );

				m_MScanInfo.Add( sInfo );
			}

		//  m_MSLL = 100.0 * m_SampFreq / 1000.0;         // Lowest
		//  m_MSL  = 147.0 * m_SampFreq / 1000.0;         // Lowest
		//  m_MSH  = 1050.0 * 3 * m_SampFreq / 1000.0;    // Highest
			m_MSLL = (uint)(50.0 *       m_SampPerMs);    // Lowest
			m_MSL  = (uint)(63.0 *       m_SampPerMs);    // Lowest
			m_MSH  = (uint)(1390.0 * 3 * m_SampPerMs);    // Highest
			// End CSSTVSET::InitIntervalPara()
		}

		public void Reset( bool fNarrow ){
			Array.Clear( m_rgMSyncList );

			if( fNarrow != m_fNarrow ) {
				foreach( ScanInfo sInfo in m_MScanInfo ) {
					sInfo.ResetE( fNarrow, m_rgMSyncList.Length );
				}
			}

			m_fNarrow     = fNarrow;
			m_MSyncACnt   = 0;
			m_MSyncCnt    = 0;
			m_MSyncIntMax = 0;
			m_MSyncIntPos = 0;
		}

		private bool CheckSub( ScanInfo sInfo ) {
			int    i = m_rgMSyncList.Length - 1;
			long cmh = sInfo.SyncWidth + m_lDeff;
			long cml = sInfo.SyncWidth - m_lDeff;

			for( i--; i >= sInfo.e; i-- ){
				long w = m_rgMSyncList[i];
				bool f = false;
				if( w > m_MSL ) {
					int km = m_fNarrow ? 2 : 3;
					for( int k = 1; k <= km; k++ ){
						long ww = w / k;
						if( (ww > cml) && (ww < cmh) )
							f = true;
					}
				}
				if( !f ) 
					return false;
			}
			return true;
		}

		public bool Check( out AllSSTVModes eModeReturned ) {
			uint w = m_rgMSyncList[m_rgMSyncList.Length -1];
			for( int k = 1; k <= 3; k++ ){
				uint ww = (uint)(w / k);

				if( (ww > m_MSL) && (ww < m_MSH) ) {
					foreach( ScanInfo sInfo in m_MScanInfo ) {
						if( sInfo.SyncWidth > 0 && sInfo.BailOut != true &&
							(ww > (sInfo.SyncWidth-m_lDeff)) && 
							(ww < (sInfo.SyncWidth+m_lDeff)) )
						{
							if( CheckSub( sInfo ) ) {
								eModeReturned = sInfo.LegacyMode;
								return true;
							}
						}
					}
				} else {
					break;
				}
			}
			eModeReturned = AllSSTVModes.smEND;
			return false;
		}

		public void Inc() {
			m_MSyncCnt++;
		}

		public uint Trig {
			set {
				m_MSyncIntMax = value;
				m_MSyncIntPos = m_MSyncCnt;
			}
		}

		public uint Max {
			set {
				if( m_MSyncIntMax < value ){
					m_MSyncIntMax = value;
					m_MSyncIntPos = m_MSyncCnt;
				}
			}
		}

		public bool Start( out AllSSTVModes ss ) {
			bool fResult = false;
			ss = AllSSTVModes.smEND;

			if( m_MSyncIntMax != 0 ) {
				if( (m_MSyncIntPos - m_MSyncACnt) > m_MSLL ){
					m_MSyncACnt = m_MSyncIntPos - m_MSyncACnt;
					//memcpy( dest, src, size );
					//memcpy(m_MSyncList, &m_MSyncList[1], sizeof(int) * (m_MSyncList.Length - 1));
					// Shift everything left one. Circular linked list might be nicer...
					Array.Copy( m_rgMSyncList, 1, m_rgMSyncList, 0, m_rgMSyncList.Length - 1 );
					m_rgMSyncList[m_rgMSyncList.Length - 1] = m_MSyncACnt;
					if( m_MSyncACnt > m_MSL ){
						fResult = Check( out ss );
					}
					m_MSyncACnt = m_MSyncIntPos;
				}
				m_MSyncIntMax = 0;
			}

			return fResult;
		}
	} // end class.

	public struct AFCStuff {
		readonly CSmooz m_AFCAVG = new ();

		int         m_AFCB; // Moved from the old SSTVSET object in MMSSTV
		int         m_AFCE; // ditto..
		int         m_AFCCount;
		double      m_AFCData;
		double      m_AFCLock;
		public double m_AFCDiff { private set; get; }
		public int    m_AFCFQ   { private set; get; }

		int          m_AFCFlag;
		int          m_AFCGard;
		int          m_AFCDis;
		readonly int m_AFCInt;

		double		m_AFC_LowVal;		// (Center - SyncLow ) * 16384 / BWH
		double		m_AFC_HighVal;		// (Center - SyncHigh) * 16384 / BWH
		double		m_AFC_SyncVal;		// (Center - Sync    ) * 16384 / BWH
		double		m_AFC_BWH;			// BWH / 16384.0;

		readonly CSmooz m_Avg = new ();

		public AFCStuff( double dblInitSampFreq ) {
			m_Avg   .SetCount( (int)( 2.5*dblInitSampFreq/1000.0 ));
			m_AFCAVG.SetCount(15);

			m_AFCInt = (int)(100 * dblInitSampFreq / 1000.0 );
			m_AFCDis = 0;
			m_AFCFQ  = -1;

			m_AFCE = 0;
			m_AFCB = 0;

			m_AFCAVG.SetCount(m_AFCAVG.Max);

			m_AFCData  = m_AFCLock = 0;
			m_AFCFlag  = 0;
			m_AFCDiff  = 0.0;
			m_AFCGard  = 10;
			m_AFCCount = 0;
			m_AFCDis   = 0;

			m_AFC_LowVal  = 0;
			m_AFC_HighVal = 0;
			m_AFC_SyncVal = 0;
			m_AFC_BWH     = 0;
		}

		public bool SetAFCFQ( int dfq ) {
			if( m_AFCFQ != dfq ){
				m_AFCFQ = dfq;
				return true;
			}

			return false;
		}

		/// <summary>
		/// This probaby Adaptive Forward Cancellation. As opposed to 
		/// Least Mean Squares (LMS) for periodic disturbance cancellation.
		/// Call this at the start of each image decoding run. Might make
		/// sence to call after each slant correct too.
		/// </summary>
		public void InitAFC( TVFamily eFamily, double dblSampFreq, FrequencyLookup rgFreqTable ) {
			// This used to live outboard in teh SSTVSET object and it seems
			// silly to be there when it can be with all it's friends.
			int  iAFCW;

			switch( eFamily ){
				case TVFamily.Martin:
					iAFCW  = (int)(2.0 * dblSampFreq / 1000.0);
					m_AFCB = (int)(1.0 * dblSampFreq / 1000.0);
					break;
				default:
					iAFCW  = (int)(3.0 * dblSampFreq / 1000.0);
					m_AFCB = (int)(1.5 * dblSampFreq / 1000.0);
					break;
			}

			// This is the "-i" option set in TMMSSTV::StartOption() if bCQ100 is
			// true then the offset is -1000. Else the offset is 0!!
			//if( m_bCQ100 ) { // Used to be a global.
    		//	double d = m_OFP * 1000.0 / SampFreq;
			//	m_OFP = (d + (1100.0/g_dblToneOffset)) * SampFreq / 1000.0;
			//}

			m_AFCE = m_AFCB + iAFCW;

			// Original InitAfc starts here.
			m_AFCAVG.SetCount(m_AFCAVG.Max);

			m_AFCData  = m_AFCLock = rgFreqTable.AFC_SyncVal;
			m_AFCFlag  = 0;
			m_AFCDiff  = 0.0;
			m_AFCGard  = 10;
			m_AFCCount = 0;
			m_AFCDis   = 0;

			m_AFC_LowVal  = rgFreqTable.AFC_LowVal;
			m_AFC_HighVal = rgFreqTable.AFC_HighVal;
			m_AFC_SyncVal = rgFreqTable.AFC_SyncVal;
			m_AFC_BWH     = rgFreqTable.AFC_BWH;

		}

		public double Avg( double d ) {
			return m_Avg.Avg( d );
		}

		public int Tone => (int)( m_AFCDiff * m_AFC_BWH );

		public bool SyncFreq(double d) {
		/*
			double		m_AFC_LowVal;	// (Center - SyncLow ) * 16384 / BWH
			double		m_AFC_HighVal;	// (Center - SyncHigh) * 16384 / BWH
			double		m_AFC_SyncVal;	// (Center - Sync    ) * 16384 / BWH
			double		m_AFC_BWH;		// BWH / 16384.0;
		*/
			d -= 128;

			if( (d <= m_AFC_LowVal) && (d >= m_AFC_HighVal) ){
				if( m_AFCDis == 0 && (m_AFCCount >= m_AFCB) && (m_AFCCount <= m_AFCE) ){
					m_AFCData = m_Avg.Avg(d);
					if( m_AFCCount == m_AFCE ){
						if( m_AFCGard != 0 ) {
							m_AFCLock = m_AFCAVG.SetData(m_AFCData);
							m_AFCGard = 0;
						} else {
							m_AFCLock = m_AFCAVG.Avg(m_AFCData);
						}
						m_AFCDiff = m_AFC_SyncVal - m_AFCLock;
						m_AFCFlag = 15;
						m_AFCDis  = m_AFCInt;
						return true;
					}
				}
				m_AFCCount++;
			} else {
				if( (m_AFCCount >= m_AFCB) && m_AFCGard != 0 ){
					m_AFCGard--;
					if( m_AFCGard == 0 ) 
						m_AFCAVG.SetData(m_AFCLock);
				}
				m_AFCCount = 0;
				if( m_AFCDis != 0 )
					m_AFCDis--;
			}

			return false;
		}
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
		protected AFCStuff _AFC;
		public SYSSET   Sys { get; protected set; }
		public SSTVMode Mode{ get; protected set; } 

		protected Dictionary<byte, SSTVMode > ModeDictionary { get; } = new Dictionary<byte, SSTVMode>();

		public Action< SSTVMode, SSTVMode, int > Send_NextMode; // This is just a single event, with no indexer.

		public const int TAPMAX = 512; // BUG: Move to Fir or Fir2 later.

		readonly double[] HBPF  = new double[TAPMAX+1];
		readonly double[] HBPFS = new double[TAPMAX+1];
		readonly double[] HBPFN = new double[TAPMAX+1];

		double _AgcD;  // agc value of d
		// These two go into the B12 (sync signal) buffer, depending on narrow or normal.
		double d12; // normal sync & Unused for narrow band.
		double d19; // narrow sync & VIS pulse for normal bandwidth.
				
		double d11; // The only time we care about these is in VIS.
		double d13;
		bool   _fVisExtended;

		readonly CFIR2 m_BPF = new CFIR2();

		double   _LpfS;
		int      m_OverFlow;
		BandPass m_bpf = BandPass.Undefined;
		int      m_bpftap;

		readonly CIIRTANK m_iir11;
		readonly CIIRTANK m_iir12;
		readonly CIIRTANK m_iir13;
		readonly CIIRTANK m_iir19;
		readonly CIIR     m_lpf11;
		readonly CIIR     m_lpf12;
		readonly CIIR     m_lpf13;
		readonly CIIR     m_lpf19;

		CLVL     m_Rcptlvl; // if you make this readonly struct, demodulation doesn't work!
		CSLVL    m_SyncLvl; // The program will compile but member variables won't update.
		readonly LevelDisplay m_LevelType = LevelDisplay.Receipt; // see mmsstv menu: "View/Level indicator/(receipt | sync)"

		public readonly FreqDetect FilterType = FreqDetect.Hilbert; // Hilbert, PLL, FQC; // BUG: This s/b parameter.

		readonly IFrequencyConverter _oConverter;

		public double OffsetCorrect( double dblAdjFreq ) {
			return _oConverter.OffsetCorrect( dblAdjFreq );
		}

		public bool   Synced { get; protected set; }
		Action        _oSyncState;
		int           _iSyncTime;
		int           _iVisData;
		int           _iVisCnt;
		int           m_Skip;
		readonly bool m_SyncRestart;
		SSTVMode      _tvNextMode;

		// Base pointer represent how far along in samples over the entire image we've gone. 
		// Write pos in samples stream. Moves forward by scanlinewidthinsamples chunks.
		// Always < size of buffer.Wraps around, see below for more details.
		public  int  m_wBase { get; protected set; } 
		public bool  m_Lost  { get; protected set; }

		protected short[] m_Buf;
		protected short[] m_B12;

		public    double m_SLvl  { get; protected set; }
		protected double m_SLvlHalf;

		bool              m_ScopeFlag;
		readonly CScope[] m_Scope = new CScope[2];

		CTICK       _oTick;
		int         _iTick;
		int         m_TickFreq;

		readonly bool m_fskdecode = false; // A vestage of the fskDecode stuff.
		readonly bool m_afc;

		protected static FrequencyLookup _rgFreqTable;
	  //#define FSKSPACE    2100

		public double SampFreq { get; }
		public double SampBase { get; }
		// This is a global in the original code, but it's pretty clear
		// that we can't handle it changing mid run. So now it's a readonly
		// member variable.
		readonly double   _dblToneOffset;
		readonly double[] _rgSenseLevels = { 2400, 3500, 4800, 6000 };

		readonly SYNCINT m_sint1;

		public SSTVDEM( SYSSET p_sys, double dblSampFreq, double dblSampBase = -1, double dbToneOffset=0 ) {
			Sys             = p_sys ?? throw new ArgumentNullException( nameof( p_sys ) );

			_dblToneOffset  = dbToneOffset;
			SampFreq        = dblSampFreq;

			if( dblSampBase == -1 )
				SampBase	= dblSampFreq;
			else
				SampBase    = dblSampBase;

			_AFC = new AFCStuff( dblSampFreq );

			// Find the biggest image type so our D12 image will be large enough.
			double dblMaxBufferInMs = 0;
			foreach( SSTVMode oMode in this ) {
				ModeDictionary.Add( oMode.VIS, oMode );
				double dblBufferInMs = oMode.ScanWidthInMS * ( oMode.Resolution.Width + 1 );
				if( dblMaxBufferInMs < dblBufferInMs )
					dblMaxBufferInMs = dblBufferInMs;
			}

			_LpfS  = 0; // For Low pass filter.

			int iBufSize = (int)(dblMaxBufferInMs * SampFreq / 1000.0 );
			m_Buf = new short[iBufSize];
			m_B12 = new short[iBufSize];

			_rgFreqTable = new LookupNormal( Sys.m_bCQ100 );

			// TODO: FilterType Should be an input parameter. But leave it for now.
			switch( FilterType ) { 
				case FreqDetect.FQC:
					_oConverter = new CFQC( SampFreq, dbToneOffset, _rgFreqTable );
					break;
				case FreqDetect.PLL: {
					CFQC fqc = new CFQC( SampFreq, dbToneOffset, _rgFreqTable ); 
					CPLL pll = new CPLL( SampFreq, dbToneOffset, _rgFreqTable );

					pll.SetVcoGain ( 1.0 );
					pll.SetFreeFreq( _rgFreqTable.LOW, _rgFreqTable.HIGH );
					pll.MakeLoopLPF( iLoopOrder:1, iLoopFreq: _rgFreqTable.LOW );
					pll.MakeOutLPF ( iLoopOrder:3, iLoopFreq: 900 ); // probably should be 800.

					_oConverter = new PhaseCombo( pll, fqc );
					} break;
				case FreqDetect.Hilbert:
					_oConverter = new CHILL( SampFreq, SampBase, dbToneOffset, _rgFreqTable );
					break;
				default:
					throw new InvalidProgramException();
			}

			Array.Clear( HBPF,  0, HBPF .Length );
			Array.Clear( HBPFS, 0, HBPFS.Length );
			Array.Clear( HBPFN, 0, HBPFN.Length );
			SetBPF( BandPass.Wide );
		//  SetBPF calls CalcBPF() so no need to call twice.

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

			m_afc = true;

			_AFC.InitAFC( TVFamily.None, SampFreq, _rgFreqTable );
			InitTone(0);

			m_lpf11 .MakeIIR(50, SampFreq, 2, CIIR.FilterType.Butterworth, 0);
			m_lpf12 .MakeIIR(50, SampFreq, 2, CIIR.FilterType.Butterworth, 0);
			m_lpf13 .MakeIIR(50, SampFreq, 2, CIIR.FilterType.Butterworth, 0);
			m_lpf19 .MakeIIR(50, SampFreq, 2, CIIR.FilterType.Butterworth, 0);
		//  m_lpffsk.MakeIIR(50, SampFreq, 2, CIIR.FilterType.Butterworth, 0);

			m_wBase     = 0;
			m_Skip      = 0;
			Synced      = false;
			m_ScopeFlag = false;
			m_Lost      = false;
			_oSyncState = StateAutoStart;

			m_Rcptlvl   = new CLVL ( (int)SampFreq, fAgcFast:true );
			m_SyncLvl   = new CSLVL( SampFreq );

			_iTick = 0;
			_oTick = null;

			// TODO: I'm starting to think this should be false... this will prevent a
			// strong signal from resetting us, but will result in a double either way.
			m_SyncRestart = false; 

			SetSenseLevel( 1 );

			// A little evil doing this here. But needs to enum the modes. Look
			// into perhaps separating out the modes enumeration from the decoder.
		    m_sint1 = new SYNCINT( this );
		}

		public void Dispose() {
			m_B12 = null;
			m_Buf = null;
		}

        protected class ShortConsumer : IPgStreamConsumer<short> {
			protected SSTVDEM _dp;
			protected int     _rBase = 0;

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
		/// this method gets called after VIS received. Get ready to receive image.
		/// </summary>
		public void Start( SSTVMode oMode ) {
			int      iPrevBase = m_wBase;
			SSTVMode ePrevMode = Mode;

			Mode = oMode;

			// If PrevMode is non null, the System/user is trying to guess the image mode.
			// So keep the buffer so we can re-render from the beginning.
			// TODO: You know, it looks like you need to preselect if you are in
			//       narrow mode, so you'll pickup the right VIS and the bandbass will
			//       be in the right mode.
			if( ePrevMode == null ) {
				// We don't support narrow, so always false. But might change in the future.
				SetBandWidth( false ); // SSTVSET.IsNarrowMode( tvMode.Family )
				_AFC.InitAFC( oMode.TvFamily, SampFreq, _rgFreqTable );
				InitTone(0);

				_oConverter.Clear();

				//m_sint1.Reset( false );
				m_Skip     = 0;
				m_wBase    = 0;
				m_Lost     = false;
			}

			Synced      = true; // This is the only place we set to true!
			_oSyncState = StateAutoStart; 

			// Don't support narrow band modes. (yet ;-)
			//if( m_fNarrow ) 
			//	CalcNarrowBPF(HBPFN, m_bpftap, m_bpf, SSTVSET.m_Mode);

			Send_NextMode?.Invoke( oMode, ePrevMode, iPrevBase );
		}

		/// <summary>
		/// Call this when we are going from a Synced mode back to listening
		/// for the VIS.
		/// </summary>
		public virtual void Reset()	{
			if( _AFC.m_AFCFQ != 0 ){
				if( m_fskdecode ){
					m_iir11.SetFreq(1080 + _dblToneOffset, SampFreq,  80.0);
					m_iir12.SetFreq(1200 + _dblToneOffset, SampFreq, 100.0);
					m_iir13.SetFreq(1320 + _dblToneOffset, SampFreq,  80.0);
				} else {
					InitTone(0);
				}
			}
			_oConverter.Clear();

			//m_sint1.Reset( false );
			//m_sint2.Reset();
			//m_sint3.Reset();

			_iSyncTime  = (int)(SampFreq * 0.5);
			_oSyncState = StateWaitReset; // Wait for the above time.
			Synced      = false;

			SSTVMode oPrevMode = Mode;
			int      iPrevBase = m_wBase;

			Mode     = null;
			m_wBase  = 0;
			m_Skip   = 0;

			// Go back to standard bandwidth so can listen for VIS.
		    SetBandWidth( false );

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
		protected int CalcBPF(double[] H1, double[] H2, double[] H3, BandPass bpf)
		{
			int lfq  = (int)((m_SyncRestart ? 1100 : 1200) + _dblToneOffset );
			int lfq2 = (int)(400 + _dblToneOffset );
			if( lfq2 < 50 ) 
				lfq2 = 50;
			int bpftap = 0;
			switch(bpf){
				case BandPass.Wide:
					bpftap = (int)(24 * SampFreq / 11025.0 );
					CFIR2.MakeFilter(H1, bpftap, FirFilt.ffBPF, SampFreq,  lfq, 2600 + _dblToneOffset, 20, 1.0);
					CFIR2.MakeFilter(H2, bpftap, FirFilt.ffBPF, SampFreq, lfq2, 2500 + _dblToneOffset, 20, 1.0);
		//			CFIR2.MakeFilter(H3, bpftap, ffBPF, SampFreq,  NARROW_BPFLOW-200, NARROW_BPFHIGH, 20, 1.0);
					break;
				case BandPass.Narrow:
					bpftap = (int)(64 * SampFreq / 11025.0 );
					CFIR2.MakeFilter(H1, bpftap, FirFilt.ffBPF, SampFreq,  lfq, 2500 + _dblToneOffset, 40, 1.0);
					CFIR2.MakeFilter(H2, bpftap, FirFilt.ffBPF, SampFreq, lfq2, 2500 + _dblToneOffset, 20, 1.0);
		//			CFIR2.MakeFilter(H3, bpftap, ffBPF, SampFreq, NARROW_BPFLOW-100, NARROW_BPFHIGH, 40, 1.0);
					break;
				case BandPass.VeryNarrow: 
					bpftap = (int)(96 * SampFreq / 11025.0 );
					CFIR2.MakeFilter(H1, bpftap, FirFilt.ffBPF, SampFreq,  lfq, 2400 + _dblToneOffset, 50, 1.0);
					CFIR2.MakeFilter(H2, bpftap, FirFilt.ffBPF, SampFreq, lfq2, 2500 + _dblToneOffset, 20, 1.0);
		//			CFIR2.MakeFilter(H3, bpftap, ffBPF, SampFreq,  NARROW_BPFLOW, NARROW_BPFHIGH, 50, 1.0);
					break;
				default:
					bpftap = 0;
					break;
			}

		  //CalcNarrowBPF(H3, bpftap, bpf, SSTVSET.m_Mode); If I add those modes I'll figure this out.
			return bpftap;
		}

		/// <summary>
		/// The MakeFilter( H3, ... in CalcBPF was always commented out with this code called at the bottom.
		/// But since I don't support narrow modes, I don't call this function either.
		/// </summary>
		public void CalcNarrowBPF(double[] H3, int bpftap, BandPass bpf, AllSSTVModes mode) {
			int low, high;
			switch(mode){
				case AllSSTVModes.smMN73:
					low = 1600; high = 2500;
					break;
				case AllSSTVModes.smMN110:
					low = 1600; high = 2500;
        			break;
				case AllSSTVModes.smMN140:
					low = 1700; high = 2400;
        			break;
				case AllSSTVModes.smMC110:
					low = 1600; high = 2500;
        			break;
				case AllSSTVModes.smMC140:
					low = 1650; high = 2500;
        			break;
				case AllSSTVModes.smMC180:
					low = 1700; high = 2400;
        			break;
				default:
					low = 1600; high = 2500;
        			break;
			}
			low  += (int)_dblToneOffset;
			high += (int)_dblToneOffset;

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

		/// <summary>
		/// This compensates for a mid image signal reception to changes between 
		/// bandpass values. Skip is only called if the user changes settings
		/// while the image is in the middle of VIS decoding.
		/// </summary>
		/// <remarks>So originally the code could change this value at any time.
		/// thus it has this m_Skip rigamarole. But since I don't allow you to
		/// change the filter bandwidth while the demodulator is running all that
		/// code is obviated! Huh, I might remove that later...</remarks>
		/// <seealso cref="CalcNarrowBPF" />
		/// <seealso cref="Start" />
		void SetBPF(BandPass bpf) {
			if( bpf != m_bpf ){
				int delay = m_bpftap; // save original value;

				m_bpf    = bpf;
				m_bpftap = CalcBPF(HBPF, HBPFS, HBPFN, m_bpf );

				m_BPF.Create(m_bpftap);

				if( Synced ) {
					m_Skip = (m_bpftap - delay) / 2;;
				}
			}
		}

		void Idle(double d)	{
			//if( !Sys.m_TestDem ) 
				m_Rcptlvl.Do(d);
		}

		void SetTickFreq(int f)	{
			if( f == 0 ) 
				f = 1200;

			m_iir12.SetFreq(f + _dblToneOffset, SampFreq, 100.0);
			m_TickFreq = f;
		}

		void InitTone(int dfq) {
			if( _AFC.SetAFCFQ( dfq ) ) {
				m_iir11.SetFreq(1080+dfq + _dblToneOffset, SampFreq, 80.0);
				m_iir12.SetFreq(1200+dfq + _dblToneOffset, SampFreq, 100.0);
				m_iir13.SetFreq(1320+dfq + _dblToneOffset, SampFreq, 80.0);
				m_iir19.SetFreq(1900+dfq + _dblToneOffset, SampFreq, 100.0);
			//  m_iirfsk.SetFreq(FSKSPACE+dfq + m_dblToneOffset, SampFreq, 100.0);
			}
		}

		/// <remarks>
		/// Moved the bCQ100 into the FrequencyLookup class. If any system variables
		/// change we just re-instatiate CSSTVDEM and SSTVDraw. 
		/// </remarks>
		void SetBandWidth( bool fNarrow ) {
			// Only need to update if we're not already in the setup needed.
			if( Sys.m_bCQ100 == _rgFreqTable.CQ100 ) {
				if( _rgFreqTable is LookupNarrow && fNarrow )
					return;
				if( _rgFreqTable is LookupNormal && !fNarrow )
					return;
			}

			// Go ahead and make the changes.
			_rgFreqTable = fNarrow ? new LookupNarrow(Sys.m_bCQ100) : new LookupNormal(Sys.m_bCQ100);

			_oConverter.SetWidth( _rgFreqTable );
		}

		int MStoSamples( int iMilliSeconds ) {
			return (int)(iMilliSeconds * SampFreq/1000);
		}

		/// <summary>
		/// At present this is only called in the constructor. Maybe when I get further
		/// along I'll see about a dialog setting for this or something.
		/// 0 - Lowest
		/// 1 - Lower
		/// 2 - Highter
		/// 3 - Highest
		/// </summary>
		/// <param name="iSquelchLvl">0, 1, 2, or 3 with 0 the default for out of range.</param>
		void SetSenseLevel( int iSquelchLvl ) {
			try {
				m_SLvl = _rgSenseLevels[iSquelchLvl];
			} catch( IndexOutOfRangeException ) {
				m_SLvl = _rgSenseLevels[0];
			}
			m_SLvlHalf = m_SLvl * 0.5;
		}

		void StateAutoStart() {
			//if( m_sint1.Start( out m_NextMode ) ){
			//	tvMode = GetSSTVMode( m_NextMode );
			//	if( tvMode != null ) {
			//		Start( tvMode );
			//		break;
			//	}
			//}
			// The first 1900hz has been seen, and now we're going down to 1200. (was 15, s/b 10)
			if( (d12 > d19) && (d12 > m_SLvl) && ((d12-d19) >= m_SLvl) ){
				_oSyncState = StateContinuousChk;
				_iSyncTime = MStoSamples( 10 ); // this is probably the ~10 ms between each 1900hz tone.
				// m_sint2.SyncMax = d12;
				//m_sint1.Trig = (uint)d12;
			}
		}

		void StateContinuousChk() {
			//if( !m_Sync /* && m_MSync */ ){
			//	if( (d12 > d19) && (d12 > m_SLvl2) && ((d12-d19) >= m_SLvl2) ){
			//		m_sint2.SyncMax( (int)d12);
			//	}
			//}
			// the second 1900hz has been seen now down to 1200hz again for 30ms.
			if( (d12 > d19) && (d12 > m_SLvl) && ((d12-d19) >= m_SLvl) ){
				//if( !m_Sync /* && m_MSync */ ){
				//m_sint1.Max = (uint)d12;
				//}
				if( --_iSyncTime == 0 ){
					_oSyncState   = StateVisDecode;
					_iSyncTime    = MStoSamples( 30 ); // Each bit is 30 ms!!
					_iVisData     = 0; // Init value
					_iVisCnt      = 8; // Start counting down the 8 bits, (after 30ms).
					_tvNextMode   = null; // just to be sure.
					_fVisExtended = false;
				}
			} else {
				_oSyncState = StateAutoStart;
			}
		}

		public void StateVisDecode() {
			if( --_iSyncTime <= 0 ) {
				if( ((d11 < d19) && (d13 < d19)) || (Math.Abs(d11-d13) < m_SLvlHalf) ) {
					// Start over? this is happening at the end of ve5kc test files.
					_oSyncState = StateAutoStart; 
				} else {
					_iSyncTime = MStoSamples(30); // Get next bit.
					_iVisData >>= 1; // we shift right to make room for next.
					if( d11 > d13 ) 
						_iVisData |= 0x0080; // Set the 8th bit to 1.(else it's 0)
					if( --_iVisCnt == 0 ){
						// Note: we've picked up the last bit to determine the VIS, but we need
						//       to walk over the 30ms STOP bit.
						_oSyncState = StateStopBit;
						if( _fVisExtended == false ) { // change to a bit, extend or not.
							if( !ModeDictionary.TryGetValue((byte)_iVisData, out _tvNextMode ) ) {
								if( _iVisData == 0x23 ) {      // MM 拡張 VIS : Expanded (16bit) VIS!!
									_fVisExtended = true;
									_iVisData     = 0;
									_iVisCnt      = 8;
									_oSyncState   = StateVisDecode;
								} else {
									_oSyncState = StateAutoStart;
								}
							}
						} else {          
							// 拡張 VIS : Vis Expansion. This is all the narrow mode stuff
							//           we don't support anyway.
							//if( ModeDictionary.TryGetValue((byte)_iVisData, out SSTVMode tvModeFound ) ) {
							//	_tvNextMode = tvModeFound.LegacyMode;
							//} else {
								_oSyncState = StateAutoStart;
							//}
						}
					}
				}
			}
		}

		void StateStopBit() {
			if( !Synced ){
				_oConverter.DoWarmUp(_AgcD); // This is for the pll filter.
			}
			if( --_iSyncTime == 0 ){
				if( (d12 > d19) && (d12 > m_SLvl) ){
					if( _tvNextMode != null ) {
						Start( _tvNextMode );
					}
				}
				_oSyncState = StateAutoStart; 
				// Start() also puts in back to StateAutoStart.
			}
		}

		/// <summary>
		/// Make sure you set the SyncTime before moving to this state.
		/// </summary>
		void StateWaitReset() {
			if( --_iSyncTime <= 0 ){
				_oSyncState = StateAutoStart;
			}
		}

		/// <summary>
		/// Either cul the VIS from the signal or the image data.
		/// </summary>
		/// <param name="s">A single sample</param>
		/// <exception cref="NullReferenceException">most likely we get in a problem with
		/// our finite state automata.</exception>
		public void Do( in double s) {
			if( (s > 24578.0) || (s < -24578.0) ){
				m_OverFlow = 1; // The grapher probably clears this.
			}
			double d = (s + _LpfS) * 0.5; // LPF, weird considering BPF below?!
			_LpfS = s;
			if( m_bpf != BandPass.Undefined ) {
				if( Synced /*||  (m_SyncMode >= 3) */ ){
					// We don't support narrow band modes.... yet.
					d = m_BPF.Do( /* m_fNarrow ? HBPFN : */ HBPF, d );
				} else {
					d = m_BPF.Do( HBPFS, d );
				}
			}
			m_Rcptlvl.Do(d);
			double od = d;            // Original value of d, for the converter.
			_AgcD = m_Rcptlvl.AGC(d); // Agc value of d;
		    m_Rcptlvl.Fix(); // This was in TMmsstv::DrawLvl, no analog to that here yet...

			d = _AgcD * 32;
			if( d >  16384.0 ) 
				d =  16384.0;
			if( d < -16384.0 ) 
				d = -16384.0;

			d12 = m_iir12.Do(d);
			d12 = m_lpf12.Do( Math.Abs( d12 ) );
			d19 = m_iir19.Do(d);
			d19 = m_lpf19.Do( Math.Abs( d19 ) );

			double dHSync = _rgFreqTable is LookupNormal ? d12 : d19;

			//double dsp;
			//dsp = m_iirfsk.Do(d);
			//dsp = m_lpffsk.Do( Math.Aps( dsp ));
			//DecodeFSK( (int)d19, (int)dsp );

			// Seems to me, this section belongs in the Synced area.
			if( m_ScopeFlag ) 
				m_Scope[0].WriteData( dHSync );
			if( m_LevelType == LevelDisplay.Sync ) 
				m_SyncLvl.Do( dHSync );

			if( _iTick != 0 ) {
				_oTick?.Write( d12 );
				return;
			}

			if( !Synced || m_SyncRestart ) {
				//m_sint1.Inc();
				//m_sint2.SyncInc();
				//m_sint3.SyncInc();

				// I'm going to guess these need to be live so that they're picking up 
				// single even before the value is actually used. Always lagging.
				d11 = m_iir11.Do( d );
				d11 = m_lpf11.Do( Math.Abs( d11 ));

				d13 = m_iir13.Do(d);
				d13 = m_lpf13.Do( Math.Abs( d13 ));

				_oSyncState();

			//  Comment this out if you ever want to run this test. Disable SyncRestart
			//	if( Sys.m_TestDem ) {
			//		// This is used by the TOptionDlg::TimerTimer code for test.
			//		double dblCurSig = _AFC.Avg(_oConverter.Do( od ));
			//	}
			}
			if( Synced ) {
				double freq = _oConverter.Do(od);
				if( m_afc ) {
					if( m_Rcptlvl.m_CurMax > 16 ) {
						if( _AFC.SyncFreq(freq) )
							InitTone(_AFC.Tone);
					}
					freq += _AFC.m_AFCDiff;
				}
				// Skip() was here, scope flag stuff inside.
				if( m_ScopeFlag ){
					m_Scope[1].WriteData( freq );
				}
				SignalSet( -freq, dHSync );
			}
		}

		/// <summary>
		/// This code used to be after the _AFC portion of code
		/// in the synced portion of Do(). There's no need for this
		/// because at present you can't change the bandwidth while 
		/// the demodulator is operating.
		/// </summary>
		/// <param name="freq">converted frequency value.</param>
		/// <param name="dHSync">horizontal sync from chosen iir filter.</param>
		/// <see cref="SetBPF"/>
		void Skip( double freq, double dHSync ) {
			if( m_Skip != 0 ) {
				if( m_Skip > 0 ) { // Ignore this data
					m_Skip--;
				} else {          // Pad the overshoot.
					for( ; m_Skip != 0; m_Skip++ ) {
						SignalSet(-freq, 0);
					}
				}
			} else {
				if( m_ScopeFlag ) {
					m_Scope[1].WriteData(freq);
				}
				SignalSet(-freq, dHSync);
			}
		}

		/// <summary>
		/// Legal values for the index are 0 to less than the buffer length.
		/// No negative numbers either. 
		/// </summary>
		/// <param name="iIndex">Check if this value is ok.</param>
		/// <returns>-1, 0, 1, with 0 being in bounds.</returns>
		public int BoundsCompare( int iIndex ) {
			int wBase = m_wBase; // multithread precaution

			if( iIndex >= wBase )
				return 1;
			if( wBase - iIndex >= m_Buf.Length )
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

		/// <summary>
		/// Enumerate all the transmit modes we support. The generators come in a
		/// variety of families. Each family has various variants, all map to
		/// the legacy modes.
		/// </summary>
		/// <remarks>This is the code for the old flat outlines on the RX and TX
		/// windows. Probably obsolete. But let's leave it until my Shell can
		/// implement dropdown windows. For Family/Submode lists.</remarks>
		public static IEnumerator<SSTVMode> EnumModes() {
            IEnumerator<SSTVMode> itrMode = SSTVModeMartin.EnumAllModes();
			while( itrMode.MoveNext() ) 
				yield return itrMode.Current;
			
            itrMode = SSTVModeScottie.EnumAllModes();
			while( itrMode.MoveNext() ) 
				yield return itrMode.Current;

            itrMode = SSTVModePD     .EnumAllModes();
			while( itrMode.MoveNext() ) 
				yield return itrMode.Current;

			itrMode = SSTVModeBW     .EnumAllModes();
			while( itrMode.MoveNext() )
				yield return itrMode.Current;

			itrMode = SSTVModePasokon.EnumAllModes();
			while( itrMode.MoveNext() )
				yield return itrMode.Current;

			itrMode = SSTVModeRobot422.EnumAllModes();
			while( itrMode.MoveNext() )
				yield return itrMode.Current;
		}

		public static IEnumerable<SSTVMode> GenerateAllModes {
			get => new ModeEnumerable();
		}

        protected struct ModeEnumerable : IEnumerable<SSTVMode> {
            public IEnumerator<SSTVMode> GetEnumerator() {
                return EnumModes();
            }

            IEnumerator IEnumerable.GetEnumerator() {
                return GetEnumerator();
            }
        }

        public struct EnumerateFamilies : IEnumerable<SSTVFamily> {
			public EnumerateFamilies() {
			}
            public IEnumerator<SSTVFamily> GetEnumerator() {
                return EnumFamilies();
            }

            IEnumerator IEnumerable.GetEnumerator() {
                return GetEnumerator();
            }
        }

        public class SSTVFamily :
			IEnumerable<SSTVMode> 
		{
			public SSTVFamily( TVFamily eFamily, string strName, Type typClass ) {
				TvFamily  = eFamily;
				_strName  = strName;
				_typClass = typClass;
			}
			public readonly string   _strName;
			public readonly Type     _typClass;
			public TVFamily TvFamily { get; }

            public override string ToString() {
                return _strName;
            }

            public IEnumerator<SSTVMode> GetEnumerator() {
                switch( TvFamily ) {
					case TVFamily.None:
						return SSTVModeNone.    EnumAllModes();
                    case TVFamily.Martin:
                        return SSTVModeMartin  .EnumAllModes();
                    case TVFamily.Scottie:
                        return SSTVModeScottie .EnumAllModes();
                    case TVFamily.PD:
                        return SSTVModePD      .EnumAllModes();
                    case TVFamily.BW:
                        return SSTVModeBW      .EnumAllModes();
                    case TVFamily.Pasokon:
                        return SSTVModePasokon .EnumAllModes();
                    case TVFamily.Robot:
                        return SSTVModeRobot422.EnumAllModes();
                }
				throw new NotImplementedException();
            }

            IEnumerator IEnumerable.GetEnumerator() {
                return GetEnumerator();
            }
        }

		public static IEnumerator<SSTVFamily> EnumFamilies () {
			yield return new SSTVFamily( TVFamily.Martin,  "Martin",  typeof( SSTVModeMartin ) );
			yield return new SSTVFamily( TVFamily.Scottie, "Scottie", typeof( SSTVModeScottie ) );
			yield return new SSTVFamily( TVFamily.PD,      "PD",      typeof( SSTVModePD ) );
			yield return new SSTVFamily( TVFamily.BW,      "BW",      typeof( SSTVModeBW ) );
			yield return new SSTVFamily( TVFamily.Pasokon, "Pasokon", typeof( SSTVModePasokon ) );
		    yield return new SSTVFamily( TVFamily.Robot,   "Robot",   typeof( SSTVModeRobot422 ) );
		}

        public IEnumerator<SSTVMode> GetEnumerator()
        {
            return EnumModes();
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
		public SSTVMode GetSSTVMode( AllSSTVModes eLegacy ) {
			foreach( SSTVMode tvMode in this ) {
				if( tvMode.LegacyMode == eLegacy ) {
					return tvMode;
				}
			}
			return null;
		}

		public class Levels {
			public double Current { get; }
			public double Peak    { get; }

			public SKColor CurrColor { get; }
			public SKColor PeakColor { get; }

			public Levels( double dblCurr, double dblPeak, SKColor clrCurr, SKColor clrPeak ) {
				Current = LimitZero( dblCurr );
				Peak    = LimitZero( dblPeak );

				CurrColor = clrCurr;
				PeakColor = clrPeak;
			}

			static double LimitZero( double dblValue ) {
				if( dblValue < 0 )
					return 0;
				if( dblValue > 100 )
					return 100;

				return dblValue;
			}
		}

		/// <summary>
		/// Don't actually do any drawing here. We'll need to send a message
		/// to the foreground thread. I'd like to make this a struct, but
		/// in order to return the value with my current message queue it
		/// needs to be a memory reference. I shouldn't be generating a lot
		/// of these quickly, so this might be ok. Color should probably be
		/// an enum with the values generated outside, but yeah...
		/// TODO: let's just set struct that can be read by message receiver.
		/// </summary>
		public Levels CalcLevel( bool fTransmitting ) {
			int YB = 100; // Percentage.

			m_Rcptlvl.Fix(); // Fix here, get's used in a couple of places.

			// Need to track these magic numbers down.
			const double dblRcptMax = 24578;
			const double dblSyncMax = 16384;

			double  dblScale;
			double  dblCurrent;
			SKColor clrCurrent = SKColors.Black;


			// This is the main level display.
			if( m_LevelType == LevelDisplay.Sync ){
				m_SyncLvl.Fix();
				dblScale = YB / dblSyncMax;
				dblCurrent = m_SyncLvl.m_Lvl * dblScale;
			} else {
				dblScale = YB / dblRcptMax;
				dblCurrent = m_Rcptlvl.m_CurMax * dblScale;
			}
			if( fTransmitting ){
				clrCurrent = Synced ? new SKColor(0x00ffff00) : SKColors.Yellow;
			}
			else if( m_Rcptlvl.m_CurMax >= dblRcptMax ){
				clrCurrent = SKColors.Red;
			}
			else if( Synced ){
				clrCurrent = SKColors.Lime;
			}
			else {
				clrCurrent = SKColors.Gray;
			}

			double  dblPeak      = 0;
			SKColor clrPeakColor = new SKColor();

			// This is simply a line showing the last peak max.
			if( m_LevelType == LevelDisplay.Receipt ){
				dblPeak = m_Rcptlvl.m_PeakMax * dblScale;

				if( fTransmitting ){
					clrPeakColor = SKColors.White;
				}
				else if( m_Rcptlvl.m_PeakMax < dblRcptMax ){
					clrPeakColor = SKColors.White;
				}
				else {
					clrPeakColor = SKColors.Red;
				}
			}

			return new Levels( dblCurrent, dblPeak, clrCurrent, clrPeakColor );
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

			if( Synced ) {
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

    /// <summary>
    /// List of known modes. I could do something like my controllers in the future
    /// where you register your controller and the controller is called to create
    /// an instance. But this is easiest for now.
    /// </summary>
    public enum TVFamily : int {
        None = 0,
        Martin,
        Scottie,
        PD, 
        BW,
        Pasokon,
        Robot,
        WWV
    }

	/// <summary>
	/// Which comes first, Reception or Transmission?! Perhaps this belongs in it's own file. But
	/// I'll put it at the bottom of the RX file now. It used to be in the TX file.
	/// </summary>
    public abstract class SSTVMode {
                 public  double   WidthColorInMS { get; } // Time to relay all pixels of one color component.
        abstract public  double   WidthSyncInMS  { get; } // BUG: Does this get corrected????
        abstract public  double   WidthGapInMS   { get; }
                 public  double   ScanWidthInMS  { get; protected set; } // Time for complete scan line as per specification.

        virtual  public  int      ScanMultiplier { get; } = 1;

        readonly public  byte     VIS;
        readonly public  string   Version = string.Empty;

        readonly public  TVFamily TvFamily;
        readonly public  AllSSTVModes LegacyMode;       // Legacy support.
        abstract public  string   FamilyName { get; }

        readonly public  List<ScanLineChannel> ChannelMap = new();

        protected abstract void Initialize();
        protected virtual void SetScanWidth() {
            foreach( ScanLineChannel oChannel in ChannelMap )
                ScanWidthInMS += oChannel.WidthInMs;
        }

        /// <summary>
        /// Base class for the image reception descriptor.
        /// </summary>
        /// <param name="bVIS"></param>
        /// <param name="strName">Human readable name of mode.</param>
        /// <param name="dbColorWidthInMS">Tx width of one color block in the scan line in ms.</param>
        /// <param name="skSize">Do NOT include the top 16 scan line grey scale in the height value.</param>
        public SSTVMode( TVFamily tvMode, byte bVIS, string strName, 
                         double dbColorWidthInMS, SKSizeI skSize, AllSSTVModes eLegacy = AllSSTVModes.smEND ) 
        {
            VIS            = bVIS;
            Version        = strName;
            WidthColorInMS = dbColorWidthInMS;
            TvFamily         = tvMode;
            Resolution     = skSize;
            LegacyMode     = eLegacy;
        }

        public override string ToString() {
            StringBuilder sbValue = new();

            sbValue.Append( FamilyName );
            sbValue.Append( ' ' );
            sbValue.Append( Version );

            sbValue.Append( " : " );
            sbValue.Append( Resolution.Width.ToString() );
            sbValue.Append( "x" );
            sbValue.Append( Resolution.Height.ToString() );
            sbValue.Append( " @ " );
            sbValue.Append( ( ScanWidthInMS * Resolution.Height / ScanMultiplier / 1000 ).ToString( "0." ) );
            sbValue.Append( "s" );

            return sbValue.ToString();
        }

        /// <summary>
        /// This is the offset from the start of the scan line to the end
        /// of the horizontal sync signal in millseconds. Used for aligning
        /// the horizontal offset of the image.
        /// </summary>
        public virtual double OffsetInMS => WidthSyncInMS;

        /// <summary>
        /// This is a little tricky. Scottie, Martin, and the PD modes all specify 16 scan lines
        /// of a grey scale calibration added to the output, HOWEVER, I don't see the code to send 
        /// that in MMSSTV. It looks like they use that area for image.
        /// </summary>
        public SKSizeI Resolution { get; protected set; }
    }

}

