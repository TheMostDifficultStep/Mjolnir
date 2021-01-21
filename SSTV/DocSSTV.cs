﻿using System;
using System.IO;
using System.Collections.Generic;

using SkiaSharp;

using Play.Interfaces.Embedding;
using Play.Edit;
using Play.Sound;
using Play.Sound.FFT;
using System.Collections;

namespace Play.SSTV {
    public delegate void FFTOutputEvent();

    public enum ESstvProperty {
        ALL,
        UploadTime,
        SSTVMode
    }

    public delegate void SSTVPropertyChange( ESstvProperty eProp );

    public enum GIdx : int {
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

    public class CSSTVMOD {
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

            m_rgVariOut[(int)GIdx.Unused] = 0;
	        m_rgVariOut[(int)GIdx.R] = 298; // R
	        m_rgVariOut[(int)GIdx.G] = 588; // G
	        m_rgVariOut[(int)GIdx.B] = 110; // B

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
        /// <param name="iFrequency">1500->2300</param>
        /// <param name="uiGainIndex">Gain Selector for R, G & B</param>
        /// <returns></returns>
        protected double Process( int iFrequency, uint uiGainIndex ) {
	        double d;

		    if( iFrequency > 0 ){
			    d = (double)( iFrequency - 1100 )/(double)(2300-1100); // d: .333 --> 1
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
        readonly public byte   VIS;
        readonly public string Name = string.Empty;
        readonly public double TxWidthInMS; // Single line.

        public SSTVMode( byte bVIS, string strName, double dbTxWidth ) {
            VIS         = bVIS;
            Name        = strName;
            TxWidthInMS = dbTxWidth;
        }
    }

    /// <summary>
    /// See TMmsstv::ToTX to find out the line modulation needed for a subclass.
    /// </summary>
    public abstract class SSTVGenerator :
        IEnumerable<int>
    {
        public SSTVMode Mode { get; }

        readonly private   SKBitmap      _oBitmap;
        readonly private   CSSTVMOD      _oModulator;
        readonly protected List<SKColor> _rgCache = new List<SKColor>( 800 );

        /// <summary>
        /// 
        /// </summary>
        /// <exception cref="ArgumentNullException" />
        public SSTVGenerator( SKBitmap oBitmap, CSSTVMOD oModulator, SSTVMode oMode ) {
            _oBitmap    = oBitmap    ?? throw new ArgumentNullException( "Bitmap must not be null." );
            _oModulator = oModulator ?? throw new ArgumentNullException( "Modulator must not be null." );
            Mode        = oMode      ?? throw new ArgumentNullException( "SSTV Mode must not be null." );
        }

        protected short ColorToFreq( byte bIntensity ) {
	        int iIntensity = bIntensity * (2300 - 1500) / 256; // convert 0->256 to 0->800.
	        return (short)( iIntensity + 1500 );               // convert 0->800 to 1500->2300.
        }

        protected SKColor GetPixel( int x, int y ) => _oBitmap.GetPixel( x, y );
        protected int     Height                   => _oBitmap.Height;
        protected int     Width                    => _oBitmap.Width;
        public    bool    IsDirty                  => true; // We're always happy to write.

        protected int Write( int iFrequency, uint uiGainSelect, double dbTimeMS ) {
            return _oModulator.Write( iFrequency, uiGainSelect, dbTimeMS );
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

            for( int iLine = 0; iLine < Height; ++iLine ) {
                WriteLine( iLine );
                yield return iLine;
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
        public GenerateScottie( SKBitmap oBitmap, CSSTVMOD oModulator, SSTVMode oMode ) : 
            base( oBitmap, oModulator, oMode )
        {
            if( oBitmap.Width != 320 )
                throw new ArgumentOutOfRangeException( "bitmap must be 320 pix wide." );
        }

        /// <summary>
        /// Enumerate the modes we support. Note that only Scotty 1 VIS code matches that
        /// from OK2MNM, Scottie S2 : 0x38 (vs b8=10111000), and Scottie DX : 0x4C (vs cc=11001100).
        /// This is because the MMSSTV number has the parity bit (pre)set accordingly.
        /// </summary>
        /// <returns></returns>
        public static IEnumerator<SSTVMode> GetModeEnumerator() {
 	        yield return new SSTVMode( 0x3c, "Scottie 1",  138.240 );
            yield return new SSTVMode( 0xb8, "Scottie 2",   88.064 );
            yield return new SSTVMode( 0xcc, "Scottie DX", 345.600 );
        }

        /// <summary>
        /// We need to add some extra signal after the VIS for the Scottie case.
        /// See line 7120 in main.cpp of the MMSSTV project.
        /// </summary>
        /// <param name="uiVIS"></param>
        public override void WriteVIS( ushort uiVIS ) {
            base.WriteVIS(uiVIS);

            Write( 1200, 0x0, 9 );
        }

        /// <summary>
        /// TMmsstv::LineSCT, derived.
        /// </summary>
        /// <param name="iLine">The bitmap line to output.</param>
        /// <returns>How many samples written.</returns>
        /// <remarks>I'm not sure how important it is to cache the line from the bitmap.
        /// The original code does this. Saves a multiply I would guess.</remarks>
        protected override void WriteLine( int iLine ) {
	        double dbTransmitWidth = Mode.TxWidthInMS / 320.0;

            if( iLine > Height )
                return;

            _rgCache.Clear();

	        Write( 1500, 0x2000, 1.5 );

	        for( int x = 0; x < 320; x++ ) {     // G
                _rgCache.Add( GetPixel( x, iLine ) );
		        Write( ColorToFreq( _rgCache[x].Green ), (uint)GIdx.G, dbTransmitWidth );
	        }
	        Write( 1500, 0x3000, 1.5 );
	        for( int x = 0; x < 320; x++ ) {     // B
		        Write( ColorToFreq( _rgCache[x].Blue  ), (uint)GIdx.B, dbTransmitWidth );
	        }
	        Write( 1200, 0, 9 );
	        Write( 1500, 0x1000, 1.5 );
	        for( int x = 0; x < 320; x++ ) {     // R
		        Write( ColorToFreq( _rgCache[x].Red   ), (uint)GIdx.R, dbTransmitWidth );
	        }
        }
    }

    /// <summary>
    /// MRT1, Martin 1 & 2
    /// </summary>
    public class GenerateMartin : SSTVGenerator {
        /// <exception cref="ArgumentOutOfRangeException" />
        public GenerateMartin( SKBitmap oBitmap, CSSTVMOD oModulator, SSTVMode oMode ) : 
            base( oBitmap, oModulator, oMode )
        {
            if( oBitmap.Width != 320 )
                throw new ArgumentOutOfRangeException( "bitmap must be 320 pix wide." );
        }

        /// <summary>
        /// Enumerate the modes we support. 
        /// </summary>
        /// <returns></returns>
        public static IEnumerator<SSTVMode> GetModeEnumerator() {
 	        yield return new SSTVMode( 0xac, "Martin 1",  146.432 );
            yield return new SSTVMode( 0x28, "Martin 2",   73.216 );
        }

        /// <summary>
        /// TMmsstv::LineMRT, derived.
        /// </summary>
        /// <param name="iLine">The bitmap line to output.</param>
        /// <returns>How many samples written.</returns>
        /// <remarks>I'm not sure how important it is to cache the line from the bitmap.
        /// The original code does this. Saves a multiply I would guess.</remarks>
        protected override void WriteLine( int iLine ) {
	        double dbTransmitWidth = Mode.TxWidthInMS / 320.0;

            if( iLine > Height )
                return;

            _rgCache.Clear();

	        Write( 1200, 4.862 );

	        Write( 1500, (uint)GIdx.G, 0.572 );   // G
	        for( int x = 0; x < 320; x++ ) {     
                _rgCache.Add( GetPixel( x, iLine ) );
		        Write( ColorToFreq(_rgCache[x].Green), (uint)GIdx.G, dbTransmitWidth );
	        }

	        Write( 1500, (uint)GIdx.B, 0.572 );   // B
	        for( int x = 0; x < 320; x++ ) {
		        Write( ColorToFreq(_rgCache[x].Blue ), (uint)GIdx.B, dbTransmitWidth );
	        }

	        Write( 1500, (uint)GIdx.R, 0.572 );   // R
	        for( int x = 0; x < 320; x++ ) {
		        Write( ColorToFreq(_rgCache[x].Red  ), (uint)GIdx.R, dbTransmitWidth );
	        }
	        Write( 1500, 0.572);
        }
    }

    /// <summary>
    /// This class generates the PD modes.
    /// </summary>
    public class GeneratePD : SSTVGenerator {
        struct Chrominance8Bit {
            public byte  Y;
            public byte RY;
            public byte BY;
        }

        List<Chrominance8Bit> _rgChrome = new List<Chrominance8Bit>(800);

        /// <exception cref="ArgumentOutOfRangeException" />
        public GeneratePD( SKBitmap oBitmap, CSSTVMOD oModulator, SSTVMode oMode ) : 
            base( oBitmap, oModulator, oMode )
        {
            // Note: We probably can do better.
            if( oBitmap.Width > 800 )
                throw new ArgumentOutOfRangeException( "bitmap must be 800 pix wide." );
        }

        public static IEnumerator<SSTVMode> GetModeEnumerator() {
 	        yield return new SSTVMode( 0xdd, "PD 50",    91.520 );
            yield return new SSTVMode( 0x63, "PD 90",   170.240 );
            yield return new SSTVMode( 0x5f, "PD 120",  121.600 );
            yield return new SSTVMode( 0xe2, "PD 160",  195.584 );
            yield return new SSTVMode( 0x60, "PD 180",  183.040 );
            yield return new SSTVMode( 0xe1, "PD 240",  244.480 );
            yield return new SSTVMode( 0xde, "PD 290",  228.800 );
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
        /// this WriteLine generates TWO lines!!</remarks>
        protected override void WriteLine( int iLine ) {
	        double dbTransmitWidth = Mode.TxWidthInMS / Width;

            if( iLine > Height )
                return;

            _rgChrome.Clear();
            
            Write( 1200, 20.000 );
	        Write( 1500,  2.080 );

	        for( int x = 0; x < Width; x++ ) {     // Y(odd)
                SKColor         skPixel = GetPixel( x, iLine );
                Chrominance8Bit crPixel = GetRY   ( skPixel );

                _rgChrome.Add( crPixel );

		        Write( ColorToFreq( crPixel.Y       ), dbTransmitWidth );
	        }
	        for( int x = 0; x < Width; x++ ) {     // R-Y
		        Write( ColorToFreq( _rgChrome[x].RY ), dbTransmitWidth );
	        }
	        for( int x = 0; x < Width; x++ ) {     // B-Y
                Write( ColorToFreq( _rgChrome[x].BY ), dbTransmitWidth );
	        }
            
            ++iLine;
	        for( int x = 0; x < Width; x++ ) {     // Y(even)
                SKColor         skPixel = GetPixel( x, iLine );
                Chrominance8Bit crPixel = GetRY   ( skPixel );

		        Write( ColorToFreq( crPixel.Y ), dbTransmitWidth );
	        }
        }

        public override IEnumerator<int> GetEnumerator() {
            int iHeight = ( Height % 2 != 0 ) ? Height - 1 : Height;

            if( iHeight < 0 )
                yield break;

            WriteVIS( Mode.VIS );
            yield return 0;

            for( int iLine = 0; iLine < Height; iLine+=2 ) {
                WriteLine( iLine );
                yield return iLine;
            }
        }
    } // End class

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
        /// This is a new experimental Read implementation. If you look at my
        /// older buffer implementations they are more complicated. Let's see if
        /// if this works as well.
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

    public class DocSSTV :
        IPgParent,
        IPgLoad<TextReader>,
        IPgSave<TextWriter>,
        IDisposable
    {
        private bool disposedValue;

        protected readonly IPgBaseSite       _oSiteBase;
		protected readonly IPgRoundRobinWork _oWorkPlace;

        public IPgParent Parentage => _oSiteBase.Host;
        public IPgParent Services  => Parentage;
        public bool      IsDirty   => false;

        public event SSTVPropertyChange PropertyChange;

        DataTester _oDataTester;

        public string BitmapFileName { get; } = @"C:\Users\Frodo\Documents\signals\test-images\tofu3-320-256.jpg"; 

        public SSTVMode TransmitMode { get; protected set; }

        protected Mpg123FFTSupport FileDecoder { get; set; }
        protected BufferSSTV       SSTVBuffer  { get; set; }
        public    SKBitmap         Bitmap      { get; protected set; }
		protected IPgPlayer        _oPlayer;

		double[] _rgFFTData; // Data input for FFT. Note: it get's destroyed in the process.
		CFFT     _oFFT;

        public int[] FFTResult { get; protected set; }
        public int   FFTResultSize { 
            get {
                if( _oFFT == null )
                    return 0;

                return _oFFT.Mode.TopBucket; // The 3.01kHz size we care about.
            }
        }

        public event FFTOutputEvent FFTOutputNotify;

        /// <summary>
        /// 
        /// </summary>
        /// <exception cref="ArgumentNullException" />
        /// <exception cref="ApplicationException" />
        public DocSSTV( IPgBaseSite oSite ) {
            _oSiteBase  = oSite ?? throw new ArgumentNullException( "Site must not be null" );
            _oWorkPlace = ((IPgScheduler)Services).CreateWorkPlace() ?? throw new ApplicationException( "Couldn't create a worksite from scheduler.");
        }

        #region Dispose
        protected virtual void Dispose( bool disposing ) {
            if( !disposedValue ) {
                if( disposing ) {
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

        protected bool LoadBitmap( string strFile ) {
            Bitmap = SKBitmap.Decode( strFile );

            return Bitmap != null;
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

        public bool InitNew() {
            if( !LoadBitmap( BitmapFileName ) )
                return false;

            try {
                Specification oSpec          = new Specification( 44100, 1, 0, 16 );
                              SSTVBuffer     = new BufferSSTV( oSpec );
                CSSTVMOD      oSSTVModulator = new CSSTVMOD( 0, oSpec.Rate, SSTVBuffer );

                //SSTVMode      oMode          = new SSTVMode( 0x3c, "Scotty 1",  138.240 );
                //SSTVGenerator oSSTVGenerator = new GenerateScottie( Bitmap, oSSTVModulator, oMode );

                //SSTVMode      oMode          = new SSTVMode( 0x28, "Martin 2",   73.216 );
                //SSTVGenerator oSSTVGenerator = new GenerateMartin(Bitmap, oSSTVModulator, oMode);

                TransmitMode = new SSTVMode( 0x63, "PD 90",   170.240 );
                SSTVGenerator oSSTVGenerator = new GeneratePD( Bitmap, oSSTVModulator, TransmitMode );

                SSTVBuffer.Pump = oSSTVGenerator.GetEnumerator();

                IEnumerator<string> oIter  = MMHelpers.GetOutputDevices();
                List<string>        rgDevs = new List<string>();
                while( oIter.MoveNext() ) {
                    rgDevs.Add( oIter.Current );
                }

                _oPlayer = new WmmPlayer(oSpec, 1); 

                //_oDataTester = new DataTester( SSTVBuffer );
                //SSTVBuffer.Pump = _oDataTester.GetEnumerator();

                // No point in raising any property change events, no view will be there to see them.
            }
            catch( ArgumentOutOfRangeException ) {
                return false;
            }

            return true;
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
                    uiWait = ( _oPlayer.Play( SSTVBuffer ) >> 1 ) + 1;
                } catch( Exception oEx ) {
                    Type[] rgErrors = { typeof( NullReferenceException ),
                                        typeof( ArgumentNullException ),
                                        typeof( MMSystemException ) };
                    if( rgErrors.IsUnhandled( oEx ) )
                        throw;

                    _oWorkPlace.Stop();
                    LogError( "Trouble playing in SSTV" );
                }
                yield return (int)uiWait;
            } while( SSTVBuffer.IsReading );
        }

        public bool Load( TextReader oStream ) {
            return InitNew();
        }

        public bool Save( TextWriter oStream ) {
            return true;
        }

        public void PlayBegin() {
            _oWorkPlace.Queue( GetPlayerTask(), 0 );
            //while ( _oDataTester.ConsumeData() < 350000 ) {
            //}
        }

        public void PlayStop() {
            _oWorkPlace.Stop();
        }

        public void PlaySegment() {
            try {
                FileDecoder.Read();

                _oFFT.Calc( _rgFFTData, 10, 0, FFTResult );

                FFTOutputNotify?.Invoke();
            } catch( Exception oEx ) {
                Type[] rgErrors = { typeof( NotImplementedException ),
                                    typeof( NullReferenceException ) };
                if( rgErrors.IsUnhandled( oEx ) )
                    throw;
            }
        }
    } // End class
}
