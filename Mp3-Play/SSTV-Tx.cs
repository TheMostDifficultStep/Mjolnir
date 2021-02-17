using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

using SkiaSharp;

using System.Collections;

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
        int Write( int iFrequency, uint uiGain, double dbTimeMS );
    }

    public class CSSTVMOD : IPgModulator {
        readonly protected IPgBufferWriter<short> m_oWriter;
	    readonly protected CVCO                   m_vco;
        readonly protected double                 m_dblTxSampleFreq;

	    //public int         m_bpf;
	    //public int         m_bpftap;
	    //public int         m_lpf;
	    //public double      m_lpffq;

        public double[]    m_rgOutGain = new double[4];

	    //public int         m_TuneFreq = 1750;
	    //public int         m_tune     = 0;
	    //public int         m_Lost     = 0;

	    //CFIR2		  m_BPF;
	    //CSmooz      avgLPF; // BUG: probably should add this one back.

	    public bool        m_VariOut = false;
        public UInt32[]    m_rgVariOut = new UInt32[4];

        /// <summary>
        /// Use this class to generate the tones necessary for an SSTV signal.
        /// </summary>
        /// <exception cref="ArgumentNullException" />
        public CSSTVMOD( double dblToneOffset, double dblTxSampleFreq, IPgBufferWriter<short> oWriter ) {
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

	        m_rgOutGain[0] = 20000; // 24578.0; My gain seems high, I'll try turning it down a bit.

	        InitGain();

            m_vco = new CVCO( dblTxSampleFreq, dblToneOffset );

	        m_vco.SetSampleFreq( dblTxSampleFreq );
	        m_vco.SetFreeFreq  ( 1100 + dblToneOffset );
	        m_vco.SetGain      ( 2300 - 1100 );
        }

        public int Write( int iFrequency, uint uiGain, double dbTimeMS ) {
	        double dbSamples = (dbTimeMS * m_dblTxSampleFreq)/1000.0;

            int iPos = 0;
	        while( iPos < (int)dbSamples ) {
                m_oWriter.Write( (short)Process( iFrequency, uiGain ) );
                iPos++;
            }

            return iPos;
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

    public class SSTVMode {
        readonly public  byte    VIS;
        readonly public  string  Name = string.Empty;
        readonly public  double  BlockWidthInMS; // Time to relay all pixels of one color component.
        readonly public  Type    Owner;
        readonly private SKSizeI RawRez;
        readonly public  bool    GreyCalibrate;
        readonly public  int     ExtraScanLine;

        public enum Resolutions { 
            h128or160,
            h256or320,
            v128or120,
            v256or240,
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="oOwner"></param>
        /// <param name="bVIS"></param>
        /// <param name="strName">Human readable name of mode.</param>
        /// <param name="dbTxWidth">Tx width of scan line in ms.</param>
        /// <param name="skSize">Do NOT include the top 16 scan line grey scale in the height value.</param>
        public SSTVMode( Type oOwner, byte bVIS, string strName, double dbTxWidth, SKSizeI skSize  ) {
            VIS           = bVIS;
            Name          = strName;
            BlockWidthInMS = dbTxWidth;
            Owner         = oOwner;
            RawRez        = skSize;

            ExtraScanLine = 16; // So far no mode I support is using the 8 scan line spec.
            GreyCalibrate = false;
        }

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
        public SKSizeI Resolution {
            get {
                if( !GreyCalibrate ) {
                    return new SKSizeI( RawRez.Width, RawRez.Height + ExtraScanLine );
                }
                return RawRez;
            }
        }

    }

    /// <summary>
    /// See TMmsstv::ToTX to find out the line modulation needed for a subclass.
    /// </summary>
    public abstract class SSTVGenerator :
        IEnumerable<int>
    {
        public SSTVMode Mode { get; }

        readonly private   SKBitmap      _oBitmap; // Do not dispose this, caller will deal with it.
        readonly private   IPgModulator     _oModulator;
        readonly protected List<SKColor> _rgCache = new List<SKColor>( 800 );

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

        protected short ColorToFreq( byte bIntensity ) {
	        int iIntensity = bIntensity * (2300 - 1500) / 256; // convert 0->256 to 0->800.
	        return (short)( iIntensity + 1500 );               // offset  0->800 to 1500->2300.
        }

        protected SKColor GetPixel( int x, int y ) => _oBitmap.GetPixel( x, y );
        protected int     Height                   => Mode.Resolution.Height;
        protected int     Width                    => Mode.Resolution.Width;
        public    bool    IsDirty                  => true; // We're always happy to write.

        protected int     _iLine; // Use this value so we can track how far along we are.
        public    int     PercentComplete => (int)( _iLine / (float)Height * 100 );

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
			Write( 1900, 0x0, 300 );
			Write( 1200, 0x0,  10 );
			Write( 1900, 0x0, 300 );

			Write( 1200, 0x0,  30 ); // Start bit.

            int iVISLenInBits = ( uiVIS >= 0x100 ) ? 16 : 8;

			for( int i = 0; i < iVISLenInBits; i++ ) {
				Write( (short)( ( uiVIS & 0x0001 ) != 0 ? 1100 : 1300), 0x0, 30 );
				uiVIS >>= 1;
			}
			
            Write(1200, 0x0, 30 ); // Sync
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
        /// Enumerate the modes we support. Note that only Scotty 1 VIS code matches that
        /// from OK2MNM; Scottie S2 : 0x38 (vs b8=10111000), and Scottie DX : 0x4C (vs cc=11001100).
        /// This is because the MMSSTV number has the parity bit (pre)set accordingly.
        /// Note that the video resolution number doesn't seem to make sense for scottie 2.
        /// if you use OK2MNM's VIS table.
        /// </summary>
        /// <returns></returns>
        public static IEnumerator<SSTVMode> GetModeEnumerator() {
            Type oOwner = typeof( GenerateScottie );

 	        yield return new SSTVMode( oOwner, /* 0 011 1100 */ 0x3c, "Scottie  1", 138.240, new SKSizeI( 320, 240 ));
            yield return new SSTVMode( oOwner, /* 1 011 1000 */ 0xb8, "Scottie  2",  88.064, new SKSizeI( 320, 240 ));
            yield return new SSTVMode( oOwner, /* 1 100 1100 */ 0xcc, "Scottie DX", 345.600, new SKSizeI( 320, 240 ));
        }

        /// <summary>
        /// We need to add some extra signal after the VIS for the Scottie case.
        /// See line 7120 in main.cpp of the MMSSTV project.
        /// </summary>
        /// <param name="uiVIS"></param>
        public override void WriteVIS( ushort uiVIS ) {
            base.WriteVIS(uiVIS);

            Write( 1200, 0x0, 9 ); // One time Sync, rely on exact timing (in old days)
        }

        /// <summary>
        /// TMmsstv::LineSCT, derived.
        /// </summary>
        /// <param name="iLine">The bitmap line to output.</param>
        /// <returns>How many samples written.</returns>
        /// <remarks>I'm not sure how important it is to cache the line from the bitmap.
        /// The original code does this. Saves a multiply I would guess.</remarks>
        protected override void WriteLine( int iLine ) {
	        double dbTimePerPixel = Mode.BlockWidthInMS / 320.0; // Note: hard coded.

            if( iLine > Height )
                return;

            try {
                _rgCache.Clear();
            
	            Write( 1500, GainIndx.G, 1.5 );      // gap (porch?)
	            for( int x = 0; x < 320; x++ ) {     // G
                    _rgCache.Add( GetPixel( x, iLine ) );
		            Write( ColorToFreq( _rgCache[x].Green ), GainIndx.G, dbTimePerPixel );
	            }
	            Write( 1500, GainIndx.B, 1.5 );
	            for( int x = 0; x < 320; x++ ) {     // B
		            Write( ColorToFreq( _rgCache[x].Blue  ), GainIndx.B, dbTimePerPixel );
	            }
	            Write( 1200, 9 );                    // HSync in second half!!
	            Write( 1500, GainIndx.R, 1.5 );
	            for( int x = 0; x < 320; x++ ) {     // R
		            Write( ColorToFreq( _rgCache[x].Red   ), GainIndx.R, dbTimePerPixel );
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

    /// <summary>
    /// MRT1, Martin 1 & 2
    /// </summary>
    /// <remarks>Historical note. Martin was invented after Scottie.</remarks>
    public class GenerateMartin : SSTVGenerator {
        /// <exception cref="ArgumentOutOfRangeException" />
        public GenerateMartin( SKBitmap oBitmap, IPgModulator oModulator, SSTVMode oMode ) : 
            base( oBitmap, oModulator, oMode )
        {
        }

        /// <summary>
        /// Enumerate the modes we support. 
        /// </summary>
        /// <returns></returns>
        public static IEnumerator<SSTVMode> GetModeEnumerator() {
            Type oOwner = typeof( GenerateMartin );

 	        yield return new SSTVMode( oOwner, /* 1010 1100 */ 0xac, "Martin 1",  146.432, new SKSizeI( 320, 240 ) );
            yield return new SSTVMode( oOwner, /* 0010 1000 */ 0x28, "Martin 2",   73.216, new SKSizeI( 320, 240 ) );
        }

        /// <summary>
        /// TMmsstv::LineMRT, derived.
        /// </summary>
        /// <param name="iLine">The bitmap line to output.</param>
        /// <returns>How many samples written.</returns>
        /// <remarks>I'm not sure how important it is to cache the line from the bitmap.
        /// The original code does this. Saves a multiply I would guess.</remarks>
        protected override void WriteLine( int iLine ) {
	        double dbTimePerPixel = Mode.BlockWidthInMS / 320.0; // Note: hard coded.

            if( iLine > Height )
                return;

            try {
                _rgCache.Clear();

	            Write( 1200, 4.862 );               // HSync on each line.

	            Write( 1500, GainIndx.G, 0.572 );   // G gap
	            for( int x = 0; x < 320; x++ ) {     
                    _rgCache.Add( GetPixel( x, iLine ) );
		            Write( ColorToFreq(_rgCache[x].Green), GainIndx.G, dbTimePerPixel );
	            }

	            Write( 1500, GainIndx.B, 0.572 );   // B gap
	            for( int x = 0; x < 320; x++ ) {
		            Write( ColorToFreq(_rgCache[x].Blue ), GainIndx.B, dbTimePerPixel );
	            }

	            Write( 1500, GainIndx.R, 0.572 );   // R gap
	            for( int x = 0; x < 320; x++ ) {
		            Write( ColorToFreq(_rgCache[x].Red  ), GainIndx.R, dbTimePerPixel );
	            }
	            Write( 1500, 0.572);
            } catch( Exception oEx ) {
                Type[] rgErrors = { typeof( AccessViolationException ),
                                    typeof( NullReferenceException ) };
                if( !rgErrors.Contains( oEx.GetType() ) )
                    throw;

                // This would be a good place to return an error.
            }
        }
    }

    /// <summary>
    /// This class generates the PD modes. Only the PD 90 modes works. >_<;;
    /// </summary>
    public class GeneratePD : SSTVGenerator {
        struct Chrominance8Bit {
            public byte  Y;
            public byte RY;
            public byte BY;
        }

        List<Chrominance8Bit> _rgChrome = new List<Chrominance8Bit>(800);

        /// <exception cref="ArgumentOutOfRangeException" />
        public GeneratePD( SKBitmap oBitmap, IPgModulator oModulator, SSTVMode oMode ) : 
            base( oBitmap, oModulator, oMode )
        {
        }

        /// <summary>So the scottie and martin modes I'm pretty confident are simply 320 horizontal lines
        /// But I know the PD modes are meant to be higher res and I got all the info straight from
        /// the inventor's web site. Which btw does not mention PD50 and PD290 modes. Also not I'm NOT
        /// presently generating the 16 scan line b/w scale. Note that all of them work. But only the
        /// PD90 works reliable. The rest require manual sync, and even then, are wavy. This ripple is
        /// probably what is causing the sync failure.
        /// See also:  Martin Bruchanov OK2MNM SSTV-Handbook.
        /// </summary> 
        public static IEnumerator<SSTVMode> GetModeEnumerator() {
            Type oOwner = typeof( GeneratePD );

            // these numbers come from https://www.classicsstv.com/pdmodes.php G4IJE the inventor.
 	        yield return new SSTVMode( oOwner, 0xdd, "PD  50",   91.520, new SKSizeI( 320, 240 ) ); // see SSTV-Handbook.
            yield return new SSTVMode( oOwner, 0x63, "PD  90",  170.240, new SKSizeI( 320, 240 ) ); // Only reliable one.
            yield return new SSTVMode( oOwner, 0x5f, "PD 120",  121.600, new SKSizeI( 640, 480 ) ); 
            yield return new SSTVMode( oOwner, 0xe2, "PD 160",  195.584, new SKSizeI( 512, 384 ) ); 
            yield return new SSTVMode( oOwner, 0x60, "PD 180",  183.040, new SKSizeI( 640, 480 ) );
            yield return new SSTVMode( oOwner, 0xe1, "PD 240",  244.480, new SKSizeI( 640, 480 ) ); 
            yield return new SSTVMode( oOwner, 0xde, "PD 290",  228.800, new SKSizeI( 800, 600 ) ); // see SSTV-handbook.
        }

        public byte Limit256( double d ) {
	        if( d < 0   ) d =   0;
	        if( d > 255 ) d = 255;

	        return (byte)d;
        }

        Chrominance8Bit GetRY( SKColor skColor ) {
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

        /// <summary>
        /// TMmsstv::LinePD, derived.
        /// </summary>
        /// <param name="iLine">The bitmap line to output.</param>
        /// <returns>How many samples written.</returns>
        /// <remarks>Note that you MUST override the default Generator iterator since
        /// this WriteLine uses TWO lines!!</remarks>
        protected override void WriteLine( int iLine ) {
	        double dbTimePerPixel = Mode.BlockWidthInMS / (double)Width;

            if( iLine > Height )
                return;

            try {
                _rgChrome.Clear();
            
                Write( 1200, 20.000 ); // Sync
	            Write( 1500,  2.080 ); // Porch

	            for( int x = 0; x < Width; x++ ) {     // Y(odd)
                    SKColor         skPixel = GetPixel( x, iLine );
                    Chrominance8Bit crPixel = GetRY   ( skPixel );

                    _rgChrome.Add( crPixel );

		            Write( ColorToFreq( crPixel.Y       ), dbTimePerPixel );
	            }
	            for( int x = 0; x < Width; x++ ) {     // R-Y
		            Write( ColorToFreq( _rgChrome[x].RY ), dbTimePerPixel );
	            }
	            for( int x = 0; x < Width; x++ ) {     // B-Y
                    Write( ColorToFreq( _rgChrome[x].BY ), dbTimePerPixel );
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
            if( iAvailable > 1 ) {
                return( _rgBuffer[_uiBuffUsed++] ); // If here, we always returned the amount asked for.
            } else {
                if( BufferReload( 1 ) == 0 )
                    throw new InvalidOperationException("Done" );

                return( _rgBuffer[_uiBuffUsed++] ); // If here, we always returned the amount asked for.
            }
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
