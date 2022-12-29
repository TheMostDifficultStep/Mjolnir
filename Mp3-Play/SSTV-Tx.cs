using System;
using System.IO;
using System.Collections.Generic;
using System.Collections;
using System.Linq;

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
