using System;
using System.IO;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System.Text;

using SkiaSharp;

//-----------------------------------------------------------------------------------------------------------------------------------------------
// Copyright+LGPL
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

namespace Play.Sound {
    public enum GainIndx : int {
        Unused = 0,
        R = 1,
        G = 2,
        B = 3
    }

    public class CVCO {
	    double	m_c1;	// VCOの利得, VCO Gain.
	    double	m_c2;	// フリーランニング周波数, free running frequency
	    double	m_z;

	    double[] m_SinTable;
	    double	 m_FreeFreq;
	    double	 m_SampleFreq;

        public CVCO( double dbSampFreq, double dblToneOffset ) {
	        m_SampleFreq = dbSampFreq;
	        m_FreeFreq   = 1900.0 + dblToneOffset;
	        m_SinTable   = new double[(int)(dbSampFreq*2)];
	        m_c1         = m_SinTable.Length/16.0;
	        m_c2         = (int)( (double)m_SinTable.Length * m_FreeFreq / m_SampleFreq );
	        m_z          = 0;

	        InitTable();
        }

        void InitTable() {
		    double pi2t = 2 * Math.PI / (double)m_SinTable.Length;
		    for( int i = 0; i < m_SinTable.Length; i++ ){
			    m_SinTable[i] = Math.Sin((double)i * pi2t);
		    }
        }

        public void SetGain(double gain) {
	        m_c1 = (double)m_SinTable.Length * gain / m_SampleFreq;
        }

        public void SetSampleFreq(double f) {
	        m_SampleFreq = f;
	        int iNewSize = (int)(m_SampleFreq*2);
	        if( m_SinTable.Length != iNewSize ){
		        m_SinTable = new double[iNewSize];

		        InitTable();
	        }
	        SetFreeFreq(m_FreeFreq);
        }

        public void SetFreeFreq(double f) {
	        m_FreeFreq = f;
	        m_c2 = (double)m_SinTable.Length * m_FreeFreq / m_SampleFreq;
        }

        public void InitPhase() {
	        m_z = 0;
        }

        /// <summary>
        /// Calculate the sin of the frequence. Not eactly sure of the magic
        /// here. However, "d" is constant while generating samples of a particular
        /// frequency, so m_z keeps track of where we are in the sin wave generation.
        /// Normally, I would do something like wave(t) = sin( freq * t ).
        /// </summary>
        /// <returns>Sin of input value, range -1 to 1</returns>
        public double Do( double d ) {
	        m_z += (d * m_c1 + m_c2);
	        while( m_z >= m_SinTable.Length ) {
		        m_z -= m_SinTable.Length;
	        }
	        while( m_z < 0 ){
		        m_z += m_SinTable.Length;
	        }
	        return m_SinTable[(int)m_z];
        }
    }

    public interface IPgModulator {
        void Reset();
        int  Write( int iFrequency, uint uiGain, double dbTimeMS );
    }

    public class SSTVMOD : IPgModulator {
        readonly protected IPgBufferWriter<short> m_oWriter;
	    readonly protected CVCO                   m_vco;
        readonly protected double                 m_dblTxSampleFreq;

        protected double _dbWritten = 0;
        protected int    _iPos      = 0;

	    //public int         m_bpf;
	    //public int         m_bpftap;
	    //public int         m_lpf;
	    //public double      m_lpffq;


	    //public int         m_TuneFreq = 1750;
	    //public int         m_tune     = 0;
	    //public int         m_Lost     = 0;

	    //CFIR2		  m_BPF;
	    //CSmooz      avgLPF; // BUG: probably should add this one back.

        public double[]    m_rgOutGain = new double[4];
	    public bool        m_VariOut   = false;
        public UInt32[]    m_rgVariOut = new UInt32[4];

        /// <summary>
        /// Use this class to generate the tones necessary for an SSTV signal.
        /// </summary>
        /// <exception cref="ArgumentNullException" />
        public SSTVMOD( double dblToneOffset, double dblTxSampleFreq, IPgBufferWriter<short> oWriter, int iGain=10000 ) {
            m_oWriter = oWriter ?? throw new ArgumentNullException( "data writer must not be null" );
            m_dblTxSampleFreq = dblTxSampleFreq;
	        //m_bpf = 1;
	        //m_lpf = 0;
	        //m_lpffq = 2000;
	        //m_bpftap = 24;
	        //int lfq = 700 + (int)dblToneOffset;

          //  if( lfq < 100 ){
		        //m_BPF.Create(m_bpftap, ffLPF, dblTxSampleFreq, 2800 + dblToneOffset, 2800 + dblToneOffset, 40, 1.0);
          //  }
          //  else {
		        //m_BPF.Create(m_bpftap, ffBPF, dblTxSampleFreq, lfq, 2800 + dblToneOffset, 40, 1.0);
          //  }

            m_rgVariOut[(int)GainIndx.Unused] = 0;
	        m_rgVariOut[(int)GainIndx.R] = 298; // R
	        m_rgVariOut[(int)GainIndx.G] = 588; // G
	        m_rgVariOut[(int)GainIndx.B] = 110; // B

            if( iGain > 30000 )
                iGain = 30000;

	        m_rgOutGain[0] = iGain; // 24578.0; My gain seems high, I'll try turning it down a bit.

	        InitGain();

            m_vco = new CVCO( dblTxSampleFreq, dblToneOffset );

	        m_vco.SetSampleFreq( dblTxSampleFreq );
	        m_vco.SetFreeFreq  ( 1100 + dblToneOffset );
	        m_vco.SetGain      ( 2300 - 1100 );
        }

        /// <summary>
        /// Writes out a number of samples close to the given time.
        /// </summary>
        /// <remarks>It is hyper critical we mark samples in floating point since
        /// our time will invariably NOT be an integer multiple of sample frequency.
        /// We can correct over time if we summate in floating point.</remarks>
        /// <seealso cref="SSTVDEM.PageRIncrement"/>
        public int Write( int iFrequency, uint uiGain, double dbTimeMS ) {
	        double dbSamples = (dbTimeMS * m_dblTxSampleFreq)/1000.0;

            _dbWritten += dbSamples;

	        while( _iPos < (int)_dbWritten ) {
                m_oWriter.Write( (short)Process( iFrequency, uiGain ) );
                _iPos++;
            }

            return _iPos;
        }

        public void Reset() {
            _dbWritten = 0;
            _iPos      = 0;
        }

        /// <summary>
        /// From CSSTVMOD::Do(void), convert frequency signal to a time domain signal and adjust gain.
        /// </summary>
        /// <param name="iFrequency">1200->2300</param>
        /// <param name="uiGainIndex">Gain Selector for R, G & B</param>
        /// <returns></returns>
        protected double Process( int iFrequency, uint uiGainIndex ) {
	        double d;

		    if( iFrequency > 0 ){
			    d = (double)( iFrequency - 1100 )/(double)(2300-1100); // d: .083 --> 1
                // Looks like VCO inputs are basically from 0 to 1.
			    //if( m_lpf ) 
                //  d = avgLPF.Avg(d);
			    d = m_vco.Do(d); // Convert frequency to time domain.
		    } else {
			    d = 0;
		    }

		    if( !m_VariOut )
                uiGainIndex = 0;

            if( uiGainIndex > m_rgOutGain.Length )
                throw new ArgumentOutOfRangeException( "Gain is out of range." );

            d *= m_rgOutGain[uiGainIndex];

            return d;
        }

	    //void        WriteCWID(char c);
	    //void        WriteFSK(BYTE c);
	    //void        OpenTXBuf(int s);
	    //void        CloseTXBuf(void);

        protected void InitGain() {
            for( int i = 0; i< m_rgVariOut.Length; ++i ) {
	            if( m_rgVariOut[i] > 1000 )
                    m_rgVariOut[i] = 1000;
            }

            for( int i = 1; i < m_rgOutGain.Length; ++ i ) {
	            m_rgOutGain[i] = m_rgOutGain[0] * (double)m_rgVariOut[i] * 0.001;
            }
        }
    }

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

    public enum ScanLineChannelType : int {
        Sync = 0,
        Gap,
        Red,
        Green,
        Blue,
        Y1,
        Y2,
        RY,
        BY,
        Y,
        RYx2,
        BYx2,
        END
    }

    public class ScanLineChannel {
        public double              WidthInMs { get; }
        public ScanLineChannelType Type      { get; }

        public ScanLineChannel( double dblWidthInMs, ScanLineChannelType eType ) {
            WidthInMs = dblWidthInMs;
            Type      = eType;
        }
    }

    public abstract class SSTVMode {
                 public  double   WidthColorInMS { get; } // Time to relay all pixels of one color component.
        abstract public  double   WidthSyncInMS  { get; } // BUG: Does this get corrected????
        abstract public  double   WidthGapInMS   { get; }
                 public  double   ScanWidthInMS  { get; protected set; } // Time for complete scan line as per specification.

        virtual  public  int      ScanMultiplier { get; } = 1;

        readonly public  byte     VIS;
        readonly public  string   Version = string.Empty;

        readonly public  TVFamily Family;
        readonly public  AllModes LegacyMode;       // Legacy support.
        abstract public  string   FamilyName { get; }

        readonly public  List<ScanLineChannel> ChannelMap = new();

        protected abstract void Initialize();
        protected virtual void SetScanWidth() {
            foreach( ScanLineChannel oChannel in ChannelMap )
                ScanWidthInMS += oChannel.WidthInMs;
        }

        public enum Resolutions { 
            h128or160,
            h256or320,
            v128or120,
            v256or240,
        }

        /// <summary>
        /// Base class for the image reception descriptor.
        /// </summary>
        /// <param name="bVIS"></param>
        /// <param name="strName">Human readable name of mode.</param>
        /// <param name="dbColorWidthInMS">Tx width of one color block in the scan line in ms.</param>
        /// <param name="skSize">Do NOT include the top 16 scan line grey scale in the height value.</param>
        public SSTVMode( TVFamily tvMode, byte bVIS, string strName, 
                         double dbColorWidthInMS, SKSizeI skSize, AllModes eLegacy = AllModes.smEND ) 
        {
            VIS            = bVIS;
            Version        = strName;
            WidthColorInMS = dbColorWidthInMS;
            Family         = tvMode;
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
        /// Basically we're either square resoution or 1 x 1.3 mode. (&Half height modes?)
        /// 11??: 320 x 240 
        /// 10??: 320 x 120 
        /// 11??: 256 x 256 
        /// 10??: 256 x 128
        /// 00??: 160 x 120
        /// 00??: 128 x 128 
        /// 01??: 128 x 256
        /// 01??: 160 x 240
        /// </summary>
        public void VisResolution( ref Resolutions eHorz, ref Resolutions eVert ) {
            const int iHoriBits = 0x04;
            const int iVertbits = 0x08;

            eHorz  = ( VIS & iHoriBits ) == 0 ? Resolutions.h128or160 : Resolutions.h256or320;
            eVert  = ( VIS & iVertbits ) == 0 ? Resolutions.v128or120 : Resolutions.v256or240;
        }

        /// <summary>
        /// This is a little tricky. Scottie, Martin, and the PD modes all specify 16 scan lines
        /// of a grey scale calibration added to the output, HOWEVER, I don't see the code to send 
        /// that in MMSSTV. It looks like they use that area for image.
        /// </summary>
        public SKSizeI Resolution { get; protected set; }
    }

    /// <summary>
    /// Almost functional. Looks like I need to write some new ScanLineChannelType's
    /// and or rejigger them to share with the PD modes. Basically, I need a quiet
    /// Channel type that'll sweep up the Y, BY, BX buffers at the end and spit
    /// out the multiple bitmap lines.
    /// </summary>
    public class SSTVModeRobot422 : SSTVMode {
        public SSTVModeRobot422( byte bVIS, string strName, double dblSync, double dblGap, double dblClrWidth, SKSizeI skSize, AllModes eLegacy = AllModes.smEND) : 
            base( TVFamily.Robot, bVIS, strName, dblClrWidth, skSize, eLegacy ) 
        {
            WidthSyncInMS = dblSync;
            WidthGapInMS  = dblGap;
        }

        public override string FamilyName => "Robot";

        public override double WidthSyncInMS { get; }
        public override double WidthGapInMS  { get; }

        /// <summary>
        /// Looks like I need to create two horizontal pixels for RY & BY
        /// Plus, I'm not picking up the color since in PD Y2 fills the top
        /// and bottom bitmap line at the end of the scan line. But Y here is 
        /// at the start, and we don't have the smarts to pull out the buffered
        /// RY and BY.
        /// </summary>
        /// <exception cref="InvalidProgramException"></exception>
		protected override void Initialize() {
			if( Family != TVFamily.Robot )
				throw new InvalidProgramException( "Mode must be of Robot type" );

            ChannelMap.Clear(); // Just in case we get called again.

			ChannelMap.Add( new( WidthSyncInMS,    ScanLineChannelType.Sync ) );
			ChannelMap.Add( new( WidthGapInMS,     ScanLineChannelType.Gap  ) );
			ChannelMap.Add( new( WidthColorInMS,   ScanLineChannelType.Y    ) );

            ChannelMap.Add( new( WidthSyncInMS /2, ScanLineChannelType.Gap  ) );
			ChannelMap.Add( new( WidthGapInMS  /2, ScanLineChannelType.Gap  ) );
			ChannelMap.Add( new( WidthColorInMS/2, ScanLineChannelType.RYx2 ) );

			ChannelMap.Add( new( WidthSyncInMS /2, ScanLineChannelType.Gap  ) );
            ChannelMap.Add( new( WidthGapInMS  /2, ScanLineChannelType.Gap  ) );
			ChannelMap.Add( new( WidthColorInMS/2, ScanLineChannelType.BYx2 ) );

            SetScanWidth();
		}

        public static IEnumerator<SSTVMode> EnumAllModes() {
            List<SSTVModeRobot422> rgModes = new();

 	      //rgModes.Add( new SSTVModeRobot420( 0x00, "12", 7, 3,  60, new SKSizeI( 160, 120 ), AllModes.smR12 ) );
 	        rgModes.Add( new SSTVModeRobot422( 0x84, "24", 6, 2,  92, new SKSizeI( 160, 120 ), AllModes.smR24 ) );
          //rgModes.Add( new SSTVModeRobot420( 0x88, "36", 9, 3,  88, new SKSizeI( 320, 240 ), AllModes.smR36 ) );
 	        rgModes.Add( new SSTVModeRobot422( 0x0c, "72", 9, 3, 138, new SKSizeI( 320, 240 ), AllModes.smR72 ) );

            foreach( SSTVModeRobot422 oMode in rgModes ) {
                oMode.Initialize();
                yield return oMode;
            }
        }
    }

    /// <summary>
    /// This class doesn't work yet. Just the start of my thinking. If I go back and
    /// redo how the PD modes cache their values, I think I can share that code
    /// with this. But it going to be fiddly and I don't want to deal with it yet.
    /// </summary>
    public class SSTVModeRobot420 : SSTVModeRobot422 {
        public SSTVModeRobot420( byte bVIS, string strName, double dblSync, double dblGap, double dblClrWidth, SKSizeI skSize, AllModes eLegacy = AllModes.smEND) : 
            base( bVIS, strName, dblSync, dblGap, dblClrWidth, skSize, eLegacy ) 
        {
        }

        public override int ScanMultiplier => 2;

        /// <summary>
        /// 420 mode is going to be difficult since you need to start on an even scan line.
        /// </summary>
        /// <exception cref="InvalidProgramException"></exception>
		protected override void Initialize() {
			if( Family != TVFamily.Robot )
				throw new InvalidProgramException( "Mode must be of Robot type" );

            ChannelMap.Clear(); // Just in case we get called again.

			ChannelMap.Add( new( WidthSyncInMS,    ScanLineChannelType.Sync ) );
			ChannelMap.Add( new( WidthGapInMS,     ScanLineChannelType.Gap  ) );
			ChannelMap.Add( new( WidthColorInMS,   ScanLineChannelType.Y    ) );

			ChannelMap.Add( new( WidthSyncInMS /2, ScanLineChannelType.Gap  ) );
            ChannelMap.Add( new( WidthGapInMS  /2, ScanLineChannelType.Gap  ) );
			ChannelMap.Add( new( WidthColorInMS/2, ScanLineChannelType.BYx2 ) );

			ChannelMap.Add( new( WidthSyncInMS,    ScanLineChannelType.Sync ) );
			ChannelMap.Add( new( WidthGapInMS,     ScanLineChannelType.Gap  ) );
			ChannelMap.Add( new( WidthColorInMS,   ScanLineChannelType.Y    ) );

            ChannelMap.Add( new( WidthSyncInMS /2, ScanLineChannelType.Gap  ) );
			ChannelMap.Add( new( WidthGapInMS  /2, ScanLineChannelType.Gap  ) );
			ChannelMap.Add( new( WidthColorInMS/2, ScanLineChannelType.RYx2 ) );

            SetScanWidth();
		}
    }

    public class SSTVModePasokon : SSTVMode {
        public SSTVModePasokon( byte bVIS, string strName, double dblSync, double dblGap, double dblClrWidth, SKSizeI skSize, AllModes eLegacy = AllModes.smEND) : 
            base( TVFamily.Pasokon, bVIS, strName, dblClrWidth, skSize, eLegacy ) 
        {
            WidthSyncInMS = dblSync;
            WidthGapInMS  = dblGap;
        }

        public override double WidthSyncInMS { get; }
        public override double WidthGapInMS  { get; }

        public override string FamilyName => "Pasokon";

		protected override void Initialize() {
			if( Family != TVFamily.Pasokon )
				throw new InvalidProgramException( "Mode must be of Pasokon type" );

            ChannelMap.Clear(); // Just in case we get called again.

			ChannelMap.Add( new( WidthSyncInMS,  ScanLineChannelType.Sync  ) );
			ChannelMap.Add( new( WidthGapInMS,   ScanLineChannelType.Gap   ) );
			ChannelMap.Add( new( WidthColorInMS, ScanLineChannelType.Red   ) );
			ChannelMap.Add( new( WidthGapInMS,   ScanLineChannelType.Gap   ) );
			ChannelMap.Add( new( WidthColorInMS, ScanLineChannelType.Green ) );
			ChannelMap.Add( new( WidthGapInMS,   ScanLineChannelType.Gap   ) );
			ChannelMap.Add( new( WidthColorInMS, ScanLineChannelType.Blue  ) );
            ChannelMap.Add( new( WidthGapInMS,   ScanLineChannelType.Gap   ) );

            SetScanWidth();
		}

        ///<remarks>sstv-handbook has P3 as 320 wide bitmap, but mmsstv uses 640.
        /// Also the sstv-handbook sez the sync signal is "20 units" wide,
        /// and a gap is "5 units" wide with 1 pixel being a unit. But 
        /// 133.333/640 is .2083... and 5.208 => 4.167 + 1.042, which is a 
        /// sync + a gap. I think it's a bug in MMSSTV</remarks> 
        public static IEnumerator<SSTVMode> EnumAllModes() {
            List<SSTVModePasokon> rgModes = new();

 	        rgModes.Add( new SSTVModePasokon( 0x71, "3",   5.208, 1.042,    133.333, new SKSizeI( 640, 496 ), AllModes.smP3 ) );
            rgModes.Add( new SSTVModePasokon( 0x72, "5",   7.813, 1.562375, 200.000, new SKSizeI( 640, 496 ), AllModes.smP5 ) );
 	        rgModes.Add( new SSTVModePasokon( 0xf3, "7",  10.417, 2.083,    146.432, new SKSizeI( 640, 496 ), AllModes.smP7 ) );

            foreach( SSTVModePasokon oMode in rgModes ) {
                oMode.Initialize();
                yield return oMode;
            }
        }
    }

    public class SSTVModeMartin : SSTVMode {
        public SSTVModeMartin( byte bVIS, string strName, double dbTxWidth, SKSizeI skSize, AllModes eLegacy = AllModes.smEND) : 
            base( TVFamily.Martin, bVIS, strName, dbTxWidth, skSize, eLegacy ) 
        {
        }

        public override double WidthSyncInMS => 4.862;
        public override double WidthGapInMS  => 0.572;

        public override string FamilyName => "Martin";
		protected override void Initialize() {
			if( Family != TVFamily.Martin )
				throw new InvalidProgramException( "Mode must be of Martin type" );

			ChannelMap.Add( new( WidthSyncInMS,  ScanLineChannelType.Sync  ) );
			ChannelMap.Add( new( WidthGapInMS,   ScanLineChannelType.Gap   ) );
			ChannelMap.Add( new( WidthColorInMS, ScanLineChannelType.Green ) );
			ChannelMap.Add( new( WidthGapInMS,   ScanLineChannelType.Gap   ) );
			ChannelMap.Add( new( WidthColorInMS, ScanLineChannelType.Blue  ) );
			ChannelMap.Add( new( WidthGapInMS,   ScanLineChannelType.Gap   ) );
			ChannelMap.Add( new( WidthColorInMS, ScanLineChannelType.Red   ) );

            SetScanWidth();
        }

        /// <summary>
        /// Enumerate the modes we support. Updated to handbook values.
        /// </summary>
        /// <returns></returns>
        public static IEnumerator<SSTVMode> EnumAllModes() {
            List<SSTVModeMartin> rgModes = new();

 	        rgModes.Add( new SSTVModeMartin( 0xac, "1",  146.432, new SKSizeI( 320, 256 ), AllModes.smMRT1 ) );
            rgModes.Add( new SSTVModeMartin( 0x28, "2",   73.216, new SKSizeI( 320, 256 ), AllModes.smMRT2 ) );
 	        rgModes.Add( new SSTVModeMartin( 0x24, "3",  146.432, new SKSizeI( 160, 128 ), AllModes.smMRT3 ) );
            rgModes.Add( new SSTVModeMartin( 0xa0, "4",   73.216, new SKSizeI( 160, 128 ), AllModes.smMRT4 ) );

            foreach( SSTVModeMartin oMode in rgModes ) {
                oMode.Initialize();
                yield return oMode;
            }
        }
    }

    public class SSTVModeScottie : SSTVMode {
        public SSTVModeScottie( byte bVIS, string strName, double dbTxWidth, SKSizeI skSize, AllModes eLegacy = AllModes.smEND) : 
            base( TVFamily.Scottie, bVIS, strName, dbTxWidth, skSize, eLegacy ) 
        {
        }

        public override double WidthSyncInMS => 9.0;
        public override double WidthGapInMS  => 1.5;

        public override string FamilyName => "Scottie";

		protected override void Initialize() {
			if( Family != TVFamily.Scottie )
				throw new ArgumentOutOfRangeException( "Mode must be of Scottie type" );

			ChannelMap.Add( new ( WidthGapInMS,    ScanLineChannelType.Gap   ) );
			ChannelMap.Add( new ( WidthColorInMS,  ScanLineChannelType.Green ) );
			ChannelMap.Add( new ( WidthGapInMS,    ScanLineChannelType.Gap   ) );
			ChannelMap.Add( new ( WidthColorInMS,  ScanLineChannelType.Blue  ) );
			ChannelMap.Add( new ( WidthSyncInMS,   ScanLineChannelType.Sync  ) );
			ChannelMap.Add( new ( WidthGapInMS,    ScanLineChannelType.Gap   ) );
			ChannelMap.Add( new ( WidthColorInMS,  ScanLineChannelType.Red   ) );

            SetScanWidth();
		}

        public override double OffsetInMS => ( WidthGapInMS * 2 ) + ( WidthColorInMS * 2 ) + WidthSyncInMS;

        /// <summary>
        /// Enumerate the modes we support. Note that only Scotty 1 VIS code matches that
        /// from OK2MNM; Scottie S2 : 0x38 (vs b8=10111000), and Scottie DX : 0x4C (vs cc=11001100).
        /// This is because the MMSSTV number has the parity bit (pre)set accordingly.
        /// Note that the video resolution number doesn't seem to make sense for scottie 2.
        /// if you use OK2MNM's VIS table.
        /// </summary>
        /// <returns></returns>
        public static IEnumerator<SSTVMode> EnumAllModes() {
            List<SSTVModeScottie> rgModes = new();

 	        rgModes.Add( new SSTVModeScottie( 0x3c, "1",  138.240, new SKSizeI( 320, 256 ), AllModes.smSCT1  ) );
            rgModes.Add( new SSTVModeScottie( 0xb8, "2",   88.064, new SKSizeI( 320, 256 ), AllModes.smSCT2  ) );
            rgModes.Add( new SSTVModeScottie( 0xcc, "DX", 345.600, new SKSizeI( 320, 256 ), AllModes.smSCTDX ) );

            foreach( SSTVModeScottie oMode in rgModes ) {
                oMode.Initialize();
                yield return oMode;
            }
        }

    }

    /// <summary>
    /// Note that PD is 420 sub sampling. Still haven't got 422 sorted, but hopefully
    /// Robot can share with this one. I'm still working on that: 2/16/2022.
    /// </summary>
    public class SSTVModePD : SSTVMode {
        public SSTVModePD( byte bVIS, string strName, double dbTxWidth, SKSizeI skSize, AllModes eLegacy = AllModes.smEND) : 
            base( TVFamily.PD, bVIS, strName, dbTxWidth, skSize, eLegacy ) 
        {
        }

        public override double WidthSyncInMS => 20.0;
        public override double WidthGapInMS  => 2.08;
        public override int    ScanMultiplier { get; } = 2;

        public override string FamilyName => "PD";

        public override double OffsetInMS => WidthSyncInMS - 2.08; // This fixes it... why?


        protected override void Initialize() {
			if( Family != TVFamily.PD )
				throw new ArgumentOutOfRangeException( "Mode must be of PD type" );

			ChannelMap.Add( new( WidthSyncInMS,  ScanLineChannelType.Sync ) );
			ChannelMap.Add( new( WidthGapInMS,   ScanLineChannelType.Gap  ) );
			ChannelMap.Add( new( WidthColorInMS, ScanLineChannelType.Y1   ) );
			ChannelMap.Add( new( WidthColorInMS, ScanLineChannelType.RY   ) );
			ChannelMap.Add( new( WidthColorInMS, ScanLineChannelType.BY   ) );
			ChannelMap.Add( new( WidthColorInMS, ScanLineChannelType.Y2   ) );

            SetScanWidth();
		}
        /// <summary>So the scottie and martin modes I'm pretty confident are simply 320 horizontal lines
        /// But I know the PD modes are meant to be higher res and I got all the info straight from
        /// the inventor's web site. Which btw does not mention PD50 and PD290 modes. Also not I'm NOT
        /// presently generating the 16 scan line b/w scale. Note that all of them work.
        /// See also:  Martin Bruchanov OK2MNM SSTV-Handbook.
        /// </summary> 
        public static IEnumerator<SSTVMode> EnumAllModes() {
            List<SSTVModePD> rgModes = new();

            // these numbers come from https://www.classicsstv.com/pdmodes.php G4IJE the inventor.
 	        rgModes.Add( new SSTVModePD( 0xdd, " 50",   91.520, new SKSizeI( 320, 256 ), AllModes.smPD50  )); // see SSTV-Handbook.
            rgModes.Add( new SSTVModePD( 0x63, " 90",  170.240, new SKSizeI( 320, 256 ), AllModes.smPD90  )); // Only reliable one.
            rgModes.Add( new SSTVModePD( 0x5f, "120",  121.600, new SKSizeI( 640, 512 ), AllModes.smPD120 )); 
            rgModes.Add( new SSTVModePD( 0xe2, "160",  195.584, new SKSizeI( 512, 384 ), AllModes.smPD160 )); 
            rgModes.Add( new SSTVModePD( 0x60, "180",  183.040, new SKSizeI( 640, 512 ), AllModes.smPD180 ));
            rgModes.Add( new SSTVModePD( 0xe1, "240",  244.480, new SKSizeI( 640, 512 ), AllModes.smPD240 )); 
            rgModes.Add( new SSTVModePD( 0xde, "290",  228.800, new SKSizeI( 800, 600 ), AllModes.smPD290 )); // see SSTV-handbook.

            foreach( SSTVModePD oMode in rgModes ) {
                oMode.Initialize();
                yield return oMode;
            }
        }

    }

    public class SSTVModeBW : SSTVMode {
        public SSTVModeBW( byte bVIS, string strName, double dbTxWidth, SKSizeI skSize, AllModes eLegacy = AllModes.smEND) : 
            base( TVFamily.BW, bVIS, strName, dbTxWidth, skSize, eLegacy ) 
        {
        }

        public override double WidthSyncInMS => 6;
        public override double WidthGapInMS  => 2;

        public override string FamilyName => "BW";

		protected override void Initialize() {
			if( Family != TVFamily.BW )
				throw new InvalidProgramException( "Mode must be of BW type" );

			ChannelMap.Add( new( WidthSyncInMS,  ScanLineChannelType.Sync  ) );
			ChannelMap.Add( new( WidthGapInMS,   ScanLineChannelType.Gap   ) );
			ChannelMap.Add( new( WidthColorInMS, ScanLineChannelType.Y ) );

            SetScanWidth();
		}
        public static IEnumerator<SSTVMode> EnumAllModes() {
            List<SSTVModeBW> rgModes = new();

 	        rgModes.Add( new SSTVModeBW( 0x86, "12",     92.0, new SKSizeI( 160, 120 ), AllModes.smRM12 ) ); 
 	        rgModes.Add( new SSTVModeBW( 0x82, " 8", 58.89709, new SKSizeI( 160, 120 ), AllModes.smRM8  ) ); 

            foreach( SSTVModeBW oMode in rgModes ) {
                oMode.Initialize();
                yield return oMode;
            }
        }
    }

    public class SSTVModeWWV : SSTVMode {
        public SSTVModeWWV( byte bVIS, string strName, double dbTxWidth, SKSizeI skSize, AllModes eLegacy = AllModes.smEND) : 
            base( TVFamily.WWV, bVIS, strName, dbTxWidth, skSize, eLegacy ) 
        {
        }

        public override double WidthSyncInMS => 6;
        public override double WidthGapInMS  => 2;

        public override string FamilyName => "WWV";


		protected override void Initialize() {
			if( Family != TVFamily.WWV )
				throw new InvalidProgramException( "Mode must be of WWV type" );

			ChannelMap.Add( new( WidthSyncInMS,  ScanLineChannelType.Sync  ) );
			ChannelMap.Add( new( WidthColorInMS, ScanLineChannelType.Y ) );

            SetScanWidth();
		}
    }


    /// <summary>
    /// Generators are accessed by the main sstv document (DocSSTV) accessing the modulator.
    /// See GetModeEnumerator on each SSTVGenerator subclass.
    /// Unlike the Demodulator which can list the image types it supports. It makes
    /// sense, for now, since the VIS decoder is in the demodulator and must understand
    /// all the image types. Transmit types depend on the SSTVMode object and while the receive
    /// solely needs th SSTVMode. Those types are enumerated via the generators.
    /// </summary>
    /// <remarks>
    /// See TMmsstv::ToTX() in the orginal code  to find out the line modulation 
    /// needed for a subclass.
    /// </remarks>
    /// <seealso cref="super.GetModeEnumerator" />
    public abstract class SSTVGenerator :
        IEnumerable<int>
    {
        public SSTVMode Mode { get; }

        readonly private   SKBitmap      _oBitmap; // Do not dispose this, caller owns it.
        readonly private   IPgModulator  _oModulator;
        readonly protected List<SKColor> _rgRGBCache = new( 800 ); // Might be better to use an array.

        /// <summary>
        /// 
        /// </summary>
        /// <exception cref="ArgumentNullException" />
        public SSTVGenerator( SKBitmap oBitmap, IPgModulator oModulator, SSTVMode oMode ) {
            _oBitmap    = oBitmap    ?? throw new ArgumentNullException( "Bitmap must not be null." );
            _oModulator = oModulator ?? throw new ArgumentNullException( "Modulator must not be null." );
            Mode        = oMode      ?? throw new ArgumentNullException( "SSTV Mode must not be null." );

            // These are important since we use the values from the MODE to control transmission.
            if( oBitmap.Width < oMode.Resolution.Width )
                throw new ArgumentOutOfRangeException( "bitmap must be at least wide enough for the mode." );
            if( oBitmap.Height < oMode.Resolution.Height )
                throw new ArgumentOutOfRangeException( "bitmap must be at least high enough for the mode." );
        }

        protected static short ColorToFreq( byte bIntensity ) {
	        int iIntensity = bIntensity * (2300 - 1500) / 256; // convert 0->256 to 0->800.
	        return (short)( iIntensity + 1500 );               // offset  0->800 to 1500->2300.
        }

        protected SKColor GetPixel( int x, int y ) => _oBitmap.GetPixel( x, y );
        protected int     Height                   => Mode.Resolution.Height;
        protected int     Width                    => Mode.Resolution.Width;
        public    bool    IsDirty                  => true; // We're always happy to write.

        protected int     _iLine; // Use this value so we can track how far along we are.
        public    int     PercentTxComplete => (int)( _iLine * 100 / (float)Height );

        protected int Write( int iFrequency, GainIndx uiGainSelect, double dbTimeMS ) {
            return _oModulator.Write( iFrequency, (uint)uiGainSelect, dbTimeMS );
        }

        protected int Write( int iFrequency, double dbTimeMS ) {
            return _oModulator.Write( iFrequency, 0, dbTimeMS );
        }

        /// <summary>
        /// "The VIS contains digital code, the first and last bits are the start and stop bits with
        /// 1200 Hz frequency. The remaining 8 bits provide mode identification and contain
        /// one parity bit. Each bit is transmitted in order from the least significant bit" from
        /// Martin Bruchanov OK2MNM, "Image Communication on Short Waves"
        /// </summary>
        /// <param name="uiVIS"></param>
        /// <returns></returns>
        public virtual void WriteVIS( UInt16 uiVIS ) {
            _oModulator.Reset();

			Write( 1900, 300 );
			Write( 1200,  10 );
			Write( 1900, 300 );

			Write( 1200,  30 ); // Start bit.

            int iVISLenInBits = ( uiVIS >= 0x100 ) ? 16 : 8;

			for( int i = 0; i < iVISLenInBits; i++ ) {
				Write( (short)( ( uiVIS & 0x0001 ) != 0 ? 1100 : 1300 ), 0x0, 30 );
				uiVIS >>= 1;
			}
			
            Write( 1200,  30 ); // Stop bit.
        }

        /// <summary>
        /// Generate a line(s) of data.
        /// </summary>
        /// <param name="iLine">Which line to output.</param>
        /// <returns></returns>
        protected abstract void WriteLine( int iLine );

        IEnumerator IEnumerable.GetEnumerator() {
            return GetEnumerator();
        }

        public virtual IEnumerator<int> GetEnumerator() {
            WriteVIS( Mode.VIS );
            yield return 0;

            // Might need to add the 16 scan line grey scale bar.
            // All the doc's mention it, but I didn't see MMSSTV code for it.

            for( _iLine = 0; _iLine < Height; ++_iLine ) {
                WriteLine( _iLine );
                yield return _iLine;
            }
        }

        public bool Save( BinaryWriter oStream ) {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// SCT, Scottie S1, S3, DX
    /// </summary>
    public class GenerateScottie : SSTVGenerator {
        /// <exception cref="ArgumentOutOfRangeException" />
        public GenerateScottie( SKBitmap oBitmap, IPgModulator oModulator, SSTVMode oMode ) : 
            base( oBitmap, oModulator, oMode )
        {
        }

        /// <summary>
        /// See line 7120 in main.cpp of the MMSSTV project. He adds this one time sync signal at the start.
        /// But it doesn't seem to make sense, as it throws off the horizontal alignment (not the slant) of
        /// the recieved image in my system. All the modes work happily w/o this code as does MMSSTV. So punt.
        /// </summary>
        /// <param name="uiVIS"></param>
        //public override void WriteVIS( ushort uiVIS ) {
        //    base.WriteVIS(uiVIS);

        //    Write( 1200, 9 ); // One time Sync, rely on exact timing (in old days)
        //}

        /// <summary>
        /// TMmsstv::LineSCT, derived.
        /// </summary>
        /// <param name="iLine">The bitmap line to output.</param>
        /// <returns>How many samples written.</returns>
        /// <remarks>I'm not sure how important it is to cache the line from the bitmap.
        /// The original code does this. Saves a multiply I would guess.</remarks>
        protected override void WriteLine( int iLine ) {
	        double dbTimePerPixel = Mode.WidthColorInMS / 320.0; // Note: hard coded.

            if( iLine > Height )
                return;

            try {
                _rgRGBCache.Clear(); // MUST clear this else keeps using first line data!!
            
	            Write( 1500, GainIndx.G, 1.5 );      // gap (porch?)
	            for( int x = 0; x < 320; x++ ) {     // G
                    _rgRGBCache.Add( GetPixel( x, iLine ) );
		            Write( ColorToFreq( _rgRGBCache[x].Green ), GainIndx.G, dbTimePerPixel );
	            }
	            Write( 1500, GainIndx.B, 1.5 );
	            for( int x = 0; x < 320; x++ ) {     // B
		            Write( ColorToFreq( _rgRGBCache[x].Blue  ), GainIndx.B, dbTimePerPixel );
	            }
	            Write( 1200, 9 );                    // HSync in second half!!
	            Write( 1500, GainIndx.R, 1.5 );
	            for( int x = 0; x < 320; x++ ) {     // R
		            Write( ColorToFreq( _rgRGBCache[x].Red   ), GainIndx.R, dbTimePerPixel );
	            }
            } catch( Exception oEx ) {
                Type[] rgErrors = { typeof( AccessViolationException ),
                                    typeof( NullReferenceException ) };
                if( !rgErrors.Contains( oEx.GetType() ) )
                    throw;

                // This would be a good place to return an error.
            }
        }
    }

    public class GeneratePasokon : SSTVGenerator {

        public GeneratePasokon( SKBitmap oBitmap, IPgModulator oModulator, SSTVMode oMode ) : 
            base( oBitmap, oModulator, oMode )
        {
        }

        protected override void WriteLine( int iLine ) {
	        double dbTimePerPixel = Mode.WidthColorInMS / Mode.Resolution.Width; 

            if( iLine > Height )
                return;

            try {
                _rgRGBCache.Clear();

	            Write( 1200, Mode.WidthSyncInMS );

	            Write( 1500, GainIndx.R, Mode.WidthGapInMS );   // R gap
	            for( int x = 0; x < Mode.Resolution.Width; x++ ) {
                    _rgRGBCache.Add( GetPixel( x, iLine ) );       // Don't forget to add the cache line!!
		            Write( ColorToFreq(_rgRGBCache[x].Red  ), GainIndx.R, dbTimePerPixel );
	            }

	            Write( 1500, GainIndx.G, Mode.WidthGapInMS );   // G gap
	            for( int x = 0; x < Mode.Resolution.Width; x++ ) {     
		            Write( ColorToFreq(_rgRGBCache[x].Green), GainIndx.G, dbTimePerPixel );
	            }

	            Write( 1500, GainIndx.B, Mode.WidthGapInMS );   // B gap
	            for( int x = 0; x < Mode.Resolution.Width; x++ ) {
		            Write( ColorToFreq(_rgRGBCache[x].Blue ), GainIndx.B, dbTimePerPixel );
	            }

	            Write( 1500, Mode.WidthGapInMS );
            } catch( Exception oEx ) {
                Type[] rgErrors = { typeof( AccessViolationException ),
                                    typeof( NullReferenceException ),
                                    typeof( ArgumentOutOfRangeException ) };
                if( !rgErrors.Contains( oEx.GetType() ) )
                    throw;

                // This would be a good place to return an error.
            }
        }
    }

    /// <summary>
    /// MRT1, Martin 1, 2, 3, 4
    /// </summary>
    /// <remarks>Historical note. Martin was invented after Scottie.</remarks>
    public class GenerateMartin : SSTVGenerator {
        /// <exception cref="ArgumentOutOfRangeException" />
        public GenerateMartin( SKBitmap oBitmap, IPgModulator oModulator, SSTVMode oMode ) : 
            base( oBitmap, oModulator, oMode )
        {
        }

        /// <summary>
        /// TMmsstv::LineMRT, derived.
        /// </summary>
        /// <param name="iLine">The bitmap line to output.</param>
        /// <returns>How many samples written.</returns>
        /// <remarks>I'm not sure how important it is to cache the line from the bitmap.
        /// The original code does this. Saves a multiply I would guess.</remarks>
        protected override void WriteLine( int iLine ) {
	        double dbTimePerPixel = Mode.WidthColorInMS / Mode.Resolution.Width; 

            if( iLine > Height )
                return;

            try {
                _rgRGBCache.Clear();

	            Write( 1200, 4.862 );               // HSync on each line.

	            Write( 1500, GainIndx.G, 0.572 );   // G gap
	            for( int x = 0; x < Mode.Resolution.Width; x++ ) {     
                    _rgRGBCache.Add( GetPixel( x, iLine ) );
		            Write( ColorToFreq(_rgRGBCache[x].Green), GainIndx.G, dbTimePerPixel );
	            }

	            Write( 1500, GainIndx.B, 0.572 );   // B gap
	            for( int x = 0; x < Mode.Resolution.Width; x++ ) {
		            Write( ColorToFreq(_rgRGBCache[x].Blue ), GainIndx.B, dbTimePerPixel );
	            }

	            Write( 1500, GainIndx.R, 0.572 );   // R gap
	            for( int x = 0; x < Mode.Resolution.Width; x++ ) {
		            Write( ColorToFreq(_rgRGBCache[x].Red  ), GainIndx.R, dbTimePerPixel );
	            }
	            Write( 1200, 0.0);                  // Just a check to see how many samples sent!
            } catch( Exception oEx ) {
                Type[] rgErrors = { typeof( AccessViolationException ),
                                    typeof( NullReferenceException ) };
                if( !rgErrors.Contains( oEx.GetType() ) )
                    throw;

                // This would be a good place to return an error.
            }
        }
    }

    public abstract class SSTVCrCbYGenerator : SSTVGenerator {
        protected readonly List<Chrominance8Bit> _rgChromaCache = new(800);

        protected struct Chrominance8Bit {
            public byte  Y;
            public byte RY;
            public byte BY;
        }

        protected SSTVCrCbYGenerator(SKBitmap oBitmap, IPgModulator oModulator, SSTVMode oMode) : 
            base(oBitmap, oModulator, oMode) 
        {
        }

        public static byte Limit256( double d ) {
	        if( d < 0   ) d =   0;
	        if( d > 255 ) d = 255;

	        return (byte)d;
        }

        protected static Chrominance8Bit GetRY( SKColor skColor ) {
            Chrominance8Bit crColor;
            /*
            These are the values that make up the table below us. (Don't delete this comment!)

	        Y  =  16.0 + (.003906 * (( 65.738 * R) + (129.057 * G) + ( 25.064 * B)));
	        RY = 128.0 + (.003906 * ((112.439 * R) + (-94.154 * G) + (-18.285 * B)));
	        BY = 128.0 + (.003906 * ((-37.945 * R) + (-74.494 * G) + (112.439 * B)));
            */
	        crColor.Y  = Limit256(  16.0 + ( 0.256773*skColor.Red + 0.504097*skColor.Green + 0.097900*skColor.Blue) );
	        crColor.RY = Limit256( 128.0 + ( 0.439187*skColor.Red - 0.367766*skColor.Green - 0.071421*skColor.Blue) );
	        crColor.BY = Limit256( 128.0 + (-0.148213*skColor.Red - 0.290974*skColor.Green + 0.439187*skColor.Blue) );

            return crColor;
        }

    }

    public class GenerateRobot422 : SSTVCrCbYGenerator {
        /// <summary>
        /// Unfortunately for Robot, I'll have to choose between a 422 generator and a 420
        /// generator. That's a bummer, since it trashes up a pretty clean system heretofore.
        /// I've implemented the 422, that's easier, and I'll implement the 420 later.
        /// </summary>
        public GenerateRobot422( SKBitmap oBitmap, IPgModulator oModulator, SSTVMode oMode ) : 
            base( oBitmap, oModulator, oMode )
        {
        }

        protected override void WriteLine( int iLine ) {
	        double dbTimePerPixel = Mode.WidthColorInMS / Mode.Resolution.Width; 

            if( iLine > Height )
                return;

            try {
                _rgChromaCache.Clear(); // Clear the chromance line cache not the RGB one!!

	            Write( 1200, Mode.WidthSyncInMS );
                Write( 1500, Mode.WidthGapInMS  );

	            for( int x = 0; x < Width; x++ ) {      // Y(odd)
                    SKColor         skPixel = GetPixel( x, iLine );
                    Chrominance8Bit crPixel = GetRY   ( skPixel );

                    _rgChromaCache.Add( crPixel );

		            Write( ColorToFreq( crPixel.Y       ), dbTimePerPixel );
	            }

	            Write( 1500, Mode.WidthSyncInMS/2 );    // sync
                Write( 1900, Mode.WidthGapInMS /2 );    // gap
	            for( int x = 0; x < Width; x += 2 ) {   // R-Y
		            Write( ColorToFreq( _rgChromaCache[x].RY ), dbTimePerPixel );
	            }

	            Write( 2300, Mode.WidthSyncInMS/2 );    // sync
                Write( 1900, Mode.WidthGapInMS /2 );    // gap
	            for( int x = 0; x < Width; x += 2 ) {   // B-Y
		            Write( ColorToFreq( _rgChromaCache[x].BY ), dbTimePerPixel );
	            }
            } catch( Exception oEx ) {
                Type[] rgErrors = { typeof( AccessViolationException ),
                                    typeof( NullReferenceException ),
                                    typeof( ArgumentOutOfRangeException ) };
                if( !rgErrors.Contains( oEx.GetType() ) )
                    throw;

                // This would be a good place to return an error.
            }
        }
    }

    /// <summary>
    /// This class generates the PD modes. 
    /// </summary>
    public class GeneratePD : SSTVCrCbYGenerator {
        /// <exception cref="ArgumentOutOfRangeException" />
        public GeneratePD( SKBitmap oBitmap, IPgModulator oModulator, SSTVMode oMode ) : 
            base( oBitmap, oModulator, oMode )
        {
        }

        /// <summary>
        /// TMmsstv::LinePD, derived.
        /// </summary>
        /// <param name="iLine">The bitmap line to output.</param>
        /// <returns>How many samples written.</returns>
        /// <remarks>Note that you MUST override the default Generator iterator since
        /// this WriteLine uses TWO lines!!</remarks>
        protected override void WriteLine( int iLine ) {
	        double dbTimePerPixel = Mode.WidthColorInMS / (double)Width;

            if( iLine > Height )
                return;

            try {
                _rgChromaCache.Clear(); // Clear this one not the RGB cache.
            
                Write( 1200, 20.000 ); // Sync
	            Write( 1500,  2.080 ); // Porch

	            for( int x = 0; x < Width; x++ ) {     // Y(odd)
                    SKColor         skPixel = GetPixel( x, iLine );
                    Chrominance8Bit crPixel = GetRY   ( skPixel );

                    _rgChromaCache.Add( crPixel );

		            Write( ColorToFreq( crPixel.Y       ), dbTimePerPixel );
	            }
	            for( int x = 0; x < Width; x++ ) {     // R-Y
		            Write( ColorToFreq( _rgChromaCache[x].RY ), dbTimePerPixel );
	            }
	            for( int x = 0; x < Width; x++ ) {     // B-Y
                    Write( ColorToFreq( _rgChromaCache[x].BY ), dbTimePerPixel );
	            }
            
                ++iLine;
	            for( int x = 0; x < Width; x++ ) {     // Y(even)
                    SKColor         skPixel = GetPixel( x, iLine );
                    Chrominance8Bit crPixel = GetRY   ( skPixel );

		            Write( ColorToFreq( crPixel.Y ), dbTimePerPixel );
	            }
            } catch( Exception oEx ) {
                Type[] rgErrors = { typeof( AccessViolationException ),
                                    typeof( NullReferenceException ) };
                if( !rgErrors.Contains( oEx.GetType() ) )
                    throw;

                // This would be a good place to return an error.
            }
        }

        public override IEnumerator<int> GetEnumerator() {
            int iHeight = ( Height % 2 != 0 ) ? Height - 1 : Height;

            if( iHeight < 0 )
                yield break;

            WriteVIS( Mode.VIS );
            yield return 0;

            // Might need to add the 16 scan line grey scale bar.
            // All the doc's mention it, but I didn't see MMSSTV code for it.

            for( _iLine = 0; _iLine < Height; _iLine+=2 ) {
                WriteLine( _iLine );
                yield return _iLine;
            }
        }
    } 

    public class GenerateBW : SSTVCrCbYGenerator {
        public GenerateBW( SKBitmap oBitmap, IPgModulator oModulator, SSTVMode oMode ) : 
            base( oBitmap, oModulator, oMode )
        {
        }

        protected override void WriteLine(int iLine) {
	        double dbTimePerPixel = Mode.WidthColorInMS / Mode.Resolution.Width; 

	        Write( 1200, Mode.WidthSyncInMS );
	        Write( 1500, Mode.WidthGapInMS  );

	        for( int x = 0; x < Mode.Resolution.Width; x++ ) { 
                SKColor         skPixel = GetPixel( x, iLine );
                Chrominance8Bit crPixel = GetRY   ( skPixel );

		        Write( ColorToFreq( crPixel.Y ), dbTimePerPixel );
	        }
        }
    }

    public class GenerateWWV : SSTVGenerator {
        public GenerateWWV( SKBitmap oBitmap, IPgModulator oModulator, SSTVMode oMode ) : 
            base( oBitmap, oModulator, oMode )
        {
        }

        public static IEnumerator<SSTVMode> GetModeEnumerator() {
 	        yield return new SSTVModeBW( 0x00, "WWV", 1000, new SKSizeI( 320, 320 ), AllModes.smWWV ); 
        }

        protected override void WriteLine(int iLine) {
	        throw new NotImplementedException();
        }
    }

    /// <summary>
    /// New experimental buffer implementation. I'll move this over to
    /// the mp3-play project once I get it working. This object looks
    /// like a standard byte stream source (IPgReader) but it also
    /// implements IPgWriter for indirect on demand loading. This 
    /// implementation obviates the need to subclass this class's
    /// BufferReload() function.
    /// </summary>
    /// <remarks>
    /// Consider MemoryStream/BufferedStream, BinaryReader/BinaryWriter
    /// and UnmanagedMemoryStream for the future, not sure if they quite apply. 
    /// </remarks>
    /// <seealso cref="SetPump"/>
    public class BufferSSTV :
        IPgReader, 
        IPgBufferIterator<short> 
    {
        IEnumerator<int> _oDataPump  = null;
        short[]          _rgBuffer   = new short[32768]; // Large enough for a single scanline 
        uint             _uiBuffUsed = 0;
        uint             _uiBuffered = 0;
        uint             _uiAbsRead  = 0;
        uint             _uiAbsWrite = 0;

        public bool IsReading => _uiBuffered > 0;

        public Specification Spec { get; protected set; }

        public BufferSSTV( Specification oSpec ) {
            Spec = oSpec ?? throw new ArgumentNullException();
        }

        public void Dispose() {
            Clear();
            _oDataPump = null;
        }

        /// <summary>
        /// This object is called by the Buffer to request writer input.
        /// The return value of the iterator is not used. It will call
        /// the enumerator as many times as needed to provide data for the
        /// IPgReader user.
        /// </summary>
        /// <param name="oDataPump"></param>
        public IEnumerator<int> Pump {
            set {
                _oDataPump = value ?? throw new ArgumentNullException();
            }
            get {
                return _oDataPump;
            }
        }

        public void Write( short iValue ) {
            try {
                if( _uiBuffered >= _rgBuffer.Length ) {
                    Array.Resize<short>( ref _rgBuffer, _rgBuffer.Length * 2 );
                }
                _uiAbsWrite++;
                _rgBuffer[_uiBuffered++] = iValue;
            } catch( IndexOutOfRangeException ) {
            }
        }

        public void Clear() {
            _uiBuffered = 0;
            _uiBuffUsed = 0;
        }

        /// <summary>
        /// This is test reader for the SSTVDemodulator.
        /// </summary>
        /// <returns></returns>
        public short ReadOneSample() {
            int iAvailable = (int)_uiBuffered - (int)_uiBuffUsed;

            if( iAvailable <= 0 ) {
                if( BufferReload( 1 ) == 0 )
                    throw new IndexOutOfRangeException( "Done" );
            }
            _uiAbsRead++;
            return( _rgBuffer[_uiBuffUsed++] ); // If here, we always returned the amount asked for.
        }

        /// <summary>
        /// This is a new experimental Read implementation. If you look at my
        /// older buffer implementations they are more complicated. Let's see if
        /// if this works as well. Note: we're limited to 16 bit data stream.
        /// </summary>
        /// <param name="ipTarget">Pointer to unmanaged object</param>
        /// <param name="uiTargetBytesAsk">Number of bytes requested.</param>
        /// <returns></returns>
        public UInt32 Read( IntPtr ipTarget, UInt32 uiTargetBytesAsk ) {
            uint uiCopied    = 0;
            uint uiTargetAsk = uiTargetBytesAsk >> 1;

            do {
                unsafe {
                    uint   uiAvailable = _uiBuffered - _uiBuffUsed;
                    short* pTargOffset = (short*)ipTarget.ToPointer();
                    
                    pTargOffset += uiCopied;
                    uiTargetAsk -= uiCopied;

                    if( uiAvailable > uiTargetAsk ) {
                        for( int i = 0, iFrom = (int)_uiBuffUsed; i< uiTargetAsk; ++i, ++iFrom ) {
                            *pTargOffset++ = _rgBuffer[iFrom];
                        }
                        _uiBuffUsed += uiTargetAsk;
                        return( uiTargetBytesAsk ); // If here, we always returned the amount asked for.
                    }

                    if( uiAvailable > 0 ) {
                        for( int i = 0, iFrom = (int)_uiBuffUsed; i< uiAvailable; ++i, ++iFrom ) {
                            *pTargOffset++ = _rgBuffer[iFrom];
                        }
                    }

                    uiCopied += uiAvailable;

                    if( BufferReload( uiTargetAsk - uiCopied ) == 0 )
                        return uiCopied << 1;       // If here, we ran out of data.
                }
            } while( uiCopied < uiTargetAsk );

            return uiCopied << 1;
        }

        protected uint BufferReload( uint uiRequest ) {
            Clear();

            if( Pump == null )
                return 0;

            do {
                if( !Pump.MoveNext() )
                    break;
            } while( _uiBuffered < uiRequest );

            return _uiBuffered;
		}
    }

}
