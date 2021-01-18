﻿using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

using SkiaSharp;

using Play.Interfaces.Embedding;
using Play.Sound;
using Play.Sound.FFT;
using System.Collections;

namespace Play.SSTV {
    public interface IPgBufferWriter<T> {
        void Write( T tValue );
    }

    public interface IPgBufferIterator<T> : IPgBufferWriter<T> {
        void Clear();
        IEnumerator<int> Pump { get; set; }
    }
    public delegate void FFTOutputEvent();

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
        /// Return the Sin of the frequency, compensated by phase.
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
	    public double      m_outgain;
	    public double      m_outgainG;
	    public double      m_outgainB;
	    public double      m_outgainR;

	    //public int         m_TuneFreq = 1750;
	    //public int         m_tune     = 0;
	    //public int         m_Lost     = 0;

	    //CFIR2		  m_BPF;
	    //CSmooz      avgLPF; // BUG: probably should add this one back.

	    public bool        m_VariOut = false;
	    public UInt32      m_VariR   = 298;
	    public UInt32      m_VariG   = 588;
	    public UInt32      m_VariB   = 110;

        /// <summary>
        /// 
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

	        m_outgain = 24578.0;
	        InitGain();

            m_vco = new CVCO( dblTxSampleFreq, dblToneOffset );

	        m_vco.SetSampleFreq( dblTxSampleFreq );
	        m_vco.SetFreeFreq  ( 1100 + dblToneOffset );
	        m_vco.SetGain      ( 2300 - 1100 );
        }

        public int Write( int iFrequency, int iGain, double dbTimeMS ) {
	        double dbTime = (dbTimeMS * m_dblTxSampleFreq)/1000.0;

            int iPos = 0;
	        while( iPos < (int)dbTime ) {
                m_oWriter.Write( (short)Process( iFrequency, iGain ) );
                iPos++;
            }

            return iPos;
        }

        /// <summary>
        /// From CSSTVMOD::Do(void), convert frequency signal to a time domain signal and adjust gain.
        /// </summary>
        /// <param name="iFrequency">1500->2300</param>
        /// <param name="iGain">Gain Selector for R, G & B</param>
        /// <returns></returns>
        protected double Process( int iFrequency, int iGain ) {
	        double d;

		    if( iFrequency > 0 ){
			    d = (double)( iFrequency - 1100 )/(double)(2300-1100); // d: .333 -> 1
			    //if( m_lpf ) 
                //  d = avgLPF.Avg(d);
			    d = m_vco.Do(d); // Convert frequency to time domain.
		    } else {
			    d = 0;
		    }
		    if( m_VariOut ) {
                // Lame: This should be an array. I'll fix that some time. Assuming I get anything working. >_<;;
			    switch( iGain ){
				    case 0x1000:
					    d *= m_outgainR;
					    break;
				    case 0x2000:
					    d *= m_outgainG;
					    break;
				    case 0x3000:
					    d *= m_outgainB;
					    break;
				    default:
					    d *= m_outgain;
					    break;
			    }
		    } else {
		        d *= m_outgain;
            }
            return d;
        }

	    //void        WriteCWID(char c);
	    //void        WriteFSK(BYTE c);
	    //void        OpenTXBuf(int s);
	    //void        CloseTXBuf(void);

        // Lame: This should be an array. I'll fix that some time.
        protected void InitGain() {
	        if( m_VariR > 1000 ) m_VariR = 1000;
	        if( m_VariG > 1000 ) m_VariG = 1000;
	        if( m_VariB > 1000 ) m_VariB = 1000;

	        m_outgainR = m_outgain * (double)m_VariR * 0.001;
	        m_outgainG = m_outgain * (double)m_VariG * 0.001;
	        m_outgainB = m_outgain * (double)m_VariB * 0.001;
        }
    }

    public class SSTVMode {
        readonly public byte   VIS;
        readonly public string Name = string.Empty;
        readonly public double SyncInMS;
        readonly public double TxWidthInMS;

        public SSTVMode( byte bVIS, string strName, double dbSync, double dbTxWidth ) {
            VIS         = bVIS;
            Name        = strName;
            SyncInMS    = dbSync;
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
        public    bool    IsDirty                  => true; // We're always happy to write.

        protected int Write( int iFrequency, int iGain, double dbTimeMS ) {
            return _oModulator.Write( iFrequency, iGain, dbTimeMS );
        }

        /// <summary>
        /// "The VIS contains digital code, the first and last bits are the start and stop bits with
        /// 1200 Hz frequency. The remaining 8 bits provide mode identification and contain
        /// one parity bit. Each bit is transmitted in order from the least significant bit" from
        /// Martin Bruchanov OK2MNM, "Image Communication on Short Waves"
        /// </summary>
        /// <param name="uiVIS"></param>
        /// <returns></returns>
        public void WriteVIS( UInt16 uiVIS ) {
			Write( 1900, 0x0, 300 );
			Write( 1200, 0x0,  10 );
			Write( 1900, 0x0, 300 );

			Write( 1200, 0x0,  30 ); // Start bit.

            int iVISLenInBits = ( uiVIS >= 0x100 ) ? 16 : 8;

			for( int i = 0; i < iVISLenInBits; i++ ) {
				Write( (short)( ( uiVIS & 0x0001 ) != 0 ? 1100 : 1300), 0x0, 30 );
				uiVIS >>= 1;
			}
			
            Write(1200, 0x0, 30); // Sync
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
    public class ScottieGenerator : SSTVGenerator {

        /// <exception cref="ArgumentOutOfRangeException" />
        public ScottieGenerator( SKBitmap oBitmap, CSSTVMOD oModulator, SSTVMode oMode ) : 
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
 	        yield return new SSTVMode( 0x3c, "Scotty 1",  -1, 138.240 );
            yield return new SSTVMode( 0xb8, "Scotty 2",  -1,  88.064 );
            yield return new SSTVMode( 0xcc, "Scotty DX", -1, 345.600 );
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

	        Write( 1500, 0x2000, 1.5 );
            _rgCache.Clear();

	        for( int x = 0; x < 320; x++ ) {     // G
                _rgCache.Add( GetPixel( x, iLine ) );
		        Write( ColorToFreq( _rgCache[x].Green ), 0x2000, dbTransmitWidth );
	        }
	        Write( 1500, 0x3000, 1.5 );
	        for( int x = 0; x < 320; x++ ) {     // B
		        Write( ColorToFreq( _rgCache[x].Blue  ), 0x3000, dbTransmitWidth );
	        }
	        Write( 1200, 0, 9 );
	        Write( 1500, 0x1000, 1.5 );
	        for( int x = 0; x < 320; x++ ) {     // R
		       Write( ColorToFreq( _rgCache[x].Red   ), 0x1000, dbTransmitWidth );
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
        short[]          _rgBuffer   = new short[32768];
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
                uint uiAvailable = _uiBuffered - _uiBuffUsed;
                uiTargetAsk -= uiCopied;
                if( uiAvailable > uiTargetAsk ) {
                    Marshal.Copy( _rgBuffer, (int)_uiBuffUsed, ipTarget, (int)uiTargetAsk );
                    _uiBuffUsed += uiTargetAsk;
                    return( uiTargetAsk << 1 );
                }

                if( uiAvailable > 0 )
			        Marshal.Copy( _rgBuffer, (int)_uiBuffUsed, ipTarget + (int)(uiCopied * sizeof(short)), (int)uiAvailable );

                uiCopied += uiAvailable;

                if( BufferReload( uiTargetAsk - uiCopied ) == 0 )
                    return 0;
            } while( uiCopied < uiTargetAsk );

            return uiCopied;
        }

        protected uint BufferReload( uint uiRequest ) {
            Clear();

            if( Pump == null )
                return 0;

            while( _uiBuffered < uiRequest ) {
                if( !Pump.MoveNext() )
                    break;
            }

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
        protected readonly IPgSound          _oSound;

        public IPgParent Parentage => _oSiteBase.Host;
        public IPgParent Services  => Parentage;
        public bool      IsDirty   => false;

        protected Mpg123FFTSupport FileDecoder { get; set; }
        protected BufferSSTV       SSTVBuffer  { get; set; }
        protected SKBitmap         Bitmap      { get; set; }
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
			_oSound     = ((IPgSound)Services) ?? throw new ArgumentNullException( "Guest requires IPgSound from host." );
        }

        #region Dispose
        protected virtual void Dispose( bool disposing ) {
            if( !disposedValue ) {
                if( disposing ) {
                    // If init new fails then this won't get created.
                    if( FileDecoder != null )
                        FileDecoder.Dispose();
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

        protected bool LoadBitmap( string strFile ) {
            Bitmap = SKBitmap.Decode( strFile );

            return Bitmap != null;
        }

        public bool InitNew() {
            string strImage = @"C:\Users\Frodo\Documents\signals\test-images\tofu2-320-256.jpg"; 
            if( !LoadBitmap( strImage ) )
                return false;

            try {
                Specification oSpec          = new Specification( 44100, 1, 0, 16 );
                              SSTVBuffer     = new BufferSSTV( oSpec );

                CSSTVMOD      oSSTVModulator = new CSSTVMOD( 0, oSpec.Rate, SSTVBuffer );

                SSTVMode      oMode          = new SSTVMode( 0xb8, "Scotty 2",  -1,  88.064 );
                SSTVGenerator oSSTVGenerator = new ScottieGenerator( Bitmap, oSSTVModulator, oMode );

                SSTVBuffer.Pump = oSSTVGenerator.GetEnumerator();

                _oPlayer = new WmmPlayer( oSpec, 1); // _oSound.CreateSoundPlayer( oSpec );
            } catch( ArgumentOutOfRangeException ) {
                return false;
            }

            return true;
        }

        public IEnumerator<int> GetPlayerTask() {
            do {
                uint uiWait = ( _oPlayer.Play( SSTVBuffer ) >> 1 ) + 1;
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
