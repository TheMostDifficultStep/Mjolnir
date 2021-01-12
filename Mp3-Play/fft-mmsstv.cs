//Copyright+LGPL

//-----------------------------------------------------------------------------------------------------------------------------------------------
// Copyright 2000-2013 Makoto Mori, Nobuyuki Oba
// (C) 2021 https://github.com/TheMostDifficultStep
//-----------------------------------------------------------------------------------------------------------------------------------------------
// This file is a MMSSTV fft.cpp port to C#, 

// MMSSTV is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of 
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU Lesser General Public License for more details.

// You should have received a copy of the GNU Lesser General Public License along with MMTTY.  If not, see 
// <http://www.gnu.org/licenses/>.
//-----------------------------------------------------------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;

namespace Play.Sound.FFT {
	//#define CM_FFT  WM_USER+403

	public enum FFTSampleType : int {
		NOOVERSAMPLE = 0,
		X2OVERSAMPLE = 1,
		X4OVERSAMPLE = 2
	};

	public class FFTControlValues {
		readonly static int    CLOCKMAX = 48500;
		readonly static double DEFFREQ  = 11025.0;  // Why this value? 44100/4? I don't know.

		public readonly int		SampType = 0;       // Identify mode we selected.                 But otherwise unused.
		public readonly double	SampFreq = DEFFREQ;	// サンプリング周波数, actual sampling frequency, given by user.
		public readonly double	SampBase = DEFFREQ; // Base frequency for a given mode.           But otherwise unused.
	  //public readonly bool	DemOver  = true;	// cleared if we find a freq bucket to use.
	  //public readonly int     SampSize = 2048;	// Sample size BEFORE Decimation, maybe. :-\

		public readonly FFTSampleType FFTSampType = FFTSampleType.NOOVERSAMPLE;
		public readonly double        FFTSampFreq = DEFFREQ; // Was named "FFTSamp".
		public readonly int		      FFTSize     = 2048;

		readonly static List<FFTControlValues> _rgModes = new List<FFTControlValues>(10);

		/// <summary>
		/// Initialize our table of FFT control modes. Note: I've kept the original out of order
		/// SampType number so if I ever need to compare with the original code I can do that, for now.
		/// </summary>
		static FFTControlValues() {
			// FFTControlValues: FFT Mode, FFT lower bound, FFT base freq, Sample Type, FFTSize.
			_rgModes.Add( new FFTControlValues( 9, 46000.0, 48000.0, FFTSampleType.X4OVERSAMPLE, 2048 ) );
			_rgModes.Add( new FFTControlValues( 8, 43000.0, 44100.0, FFTSampleType.X4OVERSAMPLE, 2048 ) );
			_rgModes.Add( new FFTControlValues( 7, 23000.0, 24000.0, FFTSampleType.X2OVERSAMPLE, 2048 ) );
			_rgModes.Add( new FFTControlValues( 6, 20000.0, 22050.0, FFTSampleType.X2OVERSAMPLE, 2048 ) );
			_rgModes.Add( new FFTControlValues( 5, 17000.0, 18000.0, FFTSampleType.X2OVERSAMPLE, 2048 ) );
			_rgModes.Add( new FFTControlValues( 4, 15000.0, 16000.0, FFTSampleType.NOOVERSAMPLE, 2048 ) );
			_rgModes.Add( new FFTControlValues( 3, 11600.0, 12000.0, FFTSampleType.NOOVERSAMPLE, 2048 ) );
			_rgModes.Add( new FFTControlValues( 0, 10000.0, 11025.0, FFTSampleType.NOOVERSAMPLE, 2048 ) );
			_rgModes.Add( new FFTControlValues( 1,  7000.0,  8000.0, FFTSampleType.NOOVERSAMPLE, 1024 ) ); 
			_rgModes.Add( new FFTControlValues( 2,  5000.0,  6000.0, FFTSampleType.NOOVERSAMPLE, 1024 ) );
		}

		/// <summary>
		/// This is our static constructor to set up or FFT configuration table.
		/// </summary>
		private FFTControlValues( int iSampType, double dbSampFreq, double dbSampBase, FFTSampleType eType, int iFFTSize ) {
			SampType    = iSampType;
			SampFreq    = dbSampFreq; 
			SampBase    = dbSampBase;
			FFTSampType = eType;
			FFTSampFreq = SampFreq / CalcDivisor( eType );
			FFTSize     = iFFTSize;
		}

		/// <summary>
		/// Generate our frequency divisor, 1, 2, or 4.
		/// </summary>
		/// <remarks>In the future we might be able to toss this. Need to go back and tinker
		/// with the FFTCollector subclass.</remarks>
		private static double CalcDivisor( FFTSampleType eType ) {
			if( eType == 0 ) {
				return 1;
			}
			return (double)eType * 2;
		}

		/// <summary>Check if the other instance shares the same property values.</summary>
		/// <remarks>
		/// This is basically for the test code, but I made it so I'm going to just keep it around.
		/// </remarks>
		/// <param name="other">the other object to compare to.</param>
		/// <returns>true if the two objects share the same properties.</returns>
        public bool IsEquivalent( FFTControlValues other ) {
            if( other.FFTSampFreq != FFTSampFreq )
				return false;
			if( other.FFTSampType != FFTSampType )
				return false;
			if( other.FFTSize != FFTSize )
				return false;
			if( other.SampBase != SampBase )
				return false;
			if( other.SampFreq != SampFreq )
				return false;
			if( other.SampType != SampType )
				return false;

			return true;
        }

        /// <summary>
        /// Call this when you select a sound card for RX. These are the values used for the receiving 
		/// sound card FFT. 
        /// </summary>
        /// <remarks>
        /// What's going on here: For SSTV we want a resulting frequency response 0 to ~3kHz wide. So the 
        /// best sampling rate is 6kHz. But if we have higher sampling rates we can modify the FFT and/or
        /// it's data to achieve the spectral responce we need. This is because with the FFT, higher sampling
        /// rates need MORE samples to retain fidelity. The FFT input size is larger for higher sampling 
        /// rates but the FFT collect code also DECIMATES the higher sample rates since there's more data there 
        /// than needed. 
        /// This code is from InitSampType() called in TMmsstv::ReadSampFreq() and TMmsstv constructor.</remarks>
        /// <param name="iSamplingFrequency">Read this value from the sound card.</param>
		/// <exception cref="ApplicationException" />
		public static FFTControlValues FindMode( double dblFrequency ) {
			if( (dblFrequency > CLOCKMAX) || (dblFrequency < 5000.0) ) 
				throw new ArgumentException( "Sampling Frequency Out of Range" );

			foreach( FFTControlValues oMode in _rgModes ) {
				if( dblFrequency >= oMode.SampFreq ) {
					return new FFTControlValues( oMode.SampType, dblFrequency, oMode.SampBase, oMode.FFTSampType, oMode.FFTSize );
				}
			}
			throw new ArgumentException( "Invalid Frequency" );
		}

		/*

		///<Summary>This is the test code I used to compare the values generated by the
		///original code to the spiffy new code
		///</summary>
		static public bool TestMe() {
			int iCount = 0;
			for( double i = 5010; i < 48000; i += 1 ) {
				++iCount;
				FFTControlValues oOld = new FFTControlValues( i );
				FFTControlValues oNew = FindMode( i );

				if( !oNew.IsEquivalent( oOld ) ) {
					Console.WriteLine( "Equivalence Tests : " + iCount.ToString() );
					Console.WriteLine( "One Failed" );
					return false;
				}
			}
			Console.WriteLine( "Equivalence Tests : " + iCount.ToString() );
			Console.WriteLine( "All Pass" );

			return true;
		}

        /// <summary>
        /// Obviously call this when you select a sound card for RX. These are the 
        /// values used for the receiving sound card FFT. 
        /// </summary>
        /// <remarks>
        /// What's going on here, is that for SSTV we want a resulting frequency response of ~3kHz wide. So the 
        /// best sampling rate is 6kHz. But if we have higher sampling rates we can modify the FFT and/or
        /// it's data to achieve the spectral responce we need. This is because with the FFT, higher sampling
        /// rates need MORE samples to retain fidelity. The FFT input size is larger for higher sampling 
        /// rates but the FFT input code also DECIMATES the higher sample rates since there's more data there 
        /// than needed. 
        /// Note that 44,100/4 = 11,025.
        /// This code is from InitSampType() called in TMmsstv::ReadSampFreq() and TMmsstv constructor.</remarks>
        /// <param name="iSamplingFrequency">Read this value from the sound card.</param>
        public FFTControlValues( double dblSamplingFrequency ) {
			SampFreq = dblSamplingFrequency;

			if( (SampFreq > CLOCKMAX) || (SampFreq < 5000.0) ) 
				throw new ArgumentException( "Sampling Frequency Out of Range" );

			if( SampFreq >= 46000.0 ){
				SampType    = 9;
				SampBase    = 48000.0;
				//SampSize    = (48000*2048)/11025; // 8916
				//DemOver     = false;
				FFTSampFreq = SampFreq * 0.25;
				FFTSampType = FFTSampleType.X4OVERSAMPLE;                // X4 over sampling
				FFTSize     = 2048;
			}
			else if( SampFreq >= 43000.0 ){
				SampType    = 8;
				SampBase    = 44100.0;
				//SampSize    = (44100*2048)/11025; // 8192
				//DemOver	    = false;
				FFTSampFreq = SampFreq * 0.25;
				FFTSampType = FFTSampleType.X4OVERSAMPLE;                // X4 over sampling
				FFTSize     = 2048;
			}
			else if( SampFreq >= 23000.0 ){
				SampType    = 7;
				SampBase    = 24000.0;
				//SampSize    = (24000*2048)/11025; // 4458
				//DemOver     = false;
				FFTSampFreq = SampFreq * 0.5;
				FFTSampType = FFTSampleType.X2OVERSAMPLE;                // X2 over sampling
				FFTSize     = 2048;
			}
			else if( SampFreq >= 20000.0 ){
				SampType    = 6;
				SampBase    = 22050.0;
				//SampSize    = (22050*2048)/11025; // 4096
				//DemOver	    = false;
				FFTSampFreq = SampFreq * 0.5;
				FFTSampType = FFTSampleType.X2OVERSAMPLE;                // X2 over sampling
				FFTSize     = 2048;
			}
			else if( SampFreq >= 17000.0 ){
				SampType    = 5;
				SampBase    = 18000.0;
				//SampSize    = (18000*2048)/11025; // 3344
				//DemOver     = false;
				FFTSampFreq = SampFreq * 0.5;
				FFTSampType = FFTSampleType.X2OVERSAMPLE;                // X2 over sampling
				FFTSize     = 2048;
			}
			else if( SampFreq >= 15000.0 ){
				SampType    = 4;
				SampBase    = 16000.0;
				//SampSize    = (16000*2048)/11025; // 2972
				//DemOver     = false;
				FFTSampFreq = SampFreq;
				FFTSampType = FFTSampleType.NOOVERSAMPLE;
				FFTSize     = 2048;
			}
			else if( SampFreq >= 11600.0 ){
				SampType    = 3;
				SampBase    = 12000.0;
				//SampSize    = (12000*2048)/11025; // 2229
				//DemOver     = false;
				FFTSampFreq = SampFreq;
				FFTSampType = FFTSampleType.NOOVERSAMPLE;
				FFTSize     = 2048;
			}
			else if( SampFreq >= 10000.0 ){
				SampType    = 0;
				SampBase    = 11025.0;
				//SampSize    = 2048;
				//DemOver     = false;
				FFTSampFreq = SampFreq;
				FFTSampType = FFTSampleType.NOOVERSAMPLE;
				FFTSize     = 2048;
			}
			else if( SampFreq >= 7000.0 ){
				SampType    = 1;
				SampBase    = 8000.0;
				//SampSize    = (8000*2048)/11025; // 1486. 1/4 second of data???
				//DemOver     = false;
				FFTSampFreq = SampFreq;
				FFTSampType = FFTSampleType.NOOVERSAMPLE;
				FFTSize     = 1024; 
			}
			else if( SampFreq >= 5000.0 ){
				SampType    = 2;
				SampBase    = 6000.0;
				//SampSize    = (6000*2048)/11025;
				//DemOver     = false;
				FFTSampFreq = SampFreq;
				FFTSampType = FFTSampleType.NOOVERSAMPLE;
				FFTSize     = 1024;
			}
			//if( SampSize % 2 != 0 ) // If it's odd,
			//	SampSize++;         // make it even.
			//if( SampSize > 8192 ) 
			//	SampSize = 8192;
		}
		*/

		/// <summary>
		/// Given any FFT there will be frequency responce above which the rest of the code does not care about.
		/// This method returns the hightest frequency bucket we care about.
		/// </summary>
		/// <remarks>This value comes from m_FFTWINDOW = (3010 * FFT_SIZE / FFTSamp); in main.cp class tmmsstv.
		/// Spectral resolution is FFTSampFreq / FFTSize. For example (8000 sample rate/1024 FFT input)= 7.8hz buckets.
		/// Or more understandibly, the top frequency for a 8000 sample rate is 4000 hz, and we'll have
		/// 512 buckets for our 1024 sample FFT thus 7.8hz per bucket!! The highest bucket
		/// this code cares about is for the 3Khz SSB audio signal, ie the 386'th bucket I"ve been
		/// seeing in code execution. Thus the desire for this number.
		/// And the 10 of 3010 looks like no coincindence. ^_^
		/// </remarks>
		public int TopBucket => (int)(3010 * FFTSize / FFTSampFreq);
	}

	public class CFFT {
		protected readonly FFTControlValues m_oFFTCtrl;

		// BUG! Looks like this needs to be based on the variable FFTSize!!!!
		private static readonly double PI2         = Math.PI * 2;

		static readonly double SCALEADJ_1 = -5.5;
		static readonly double SCALEADJ_2 = -6.5;
		static readonly double SCALEADJ_3 = -7.8;
		static readonly double SCALEADJ_4 = -9.0;
		static readonly double LOGADJ	  =  2.81458e4;

		//static readonly double  SCALEPOW_1 	 	= (1.0/4.0);
		//static readonly double  SCALEPOW_ADJ_1	= 0.0018;
		//static readonly double  SCALEPOW_2 	 	= (1.0/5.0);
		//static readonly double  SCALEPOW_ADJ_2	= 0.008;
		//static readonly double  SCALEPOW_3 	 	= (1.0/6.0);
		//static readonly double  SCALEPOW_ADJ_3	= 0.022;
		//static readonly double  SCALEPOW_4 	 	= (1.0/7.0);
		//static readonly double  SCALEPOW_ADJ_4	= 0.048;

		public int m_FFTGain = 0; // BUG: This comes from sys._mFFTGain. I'm just cutting corners...

		public int		      m_FFTDIS; // Perhaps the MATH routines are not multithread safe?
		readonly public int[] m_fft = new int[2048]; // BUG, this actually belongs outside, I put it here while testing.

		bool	m_StgSW;
		int		m_StgSize;
		double	m_StgScale;
		double	m_StgK;

		readonly double[] m_tSinCos0;
		readonly double[] m_tSinCos1; 
		readonly double[] m_tWindow;
		readonly double[] m_pStgBuf;
		readonly int[]    m_Work;

		/// <summary>
		/// Accessor for the control mode of the FFT.
		/// </summary>
		public FFTControlValues ControlMode => m_oFFTCtrl;

		protected virtual void PostMessage() {
			//if( m_Handle != NULL ){
			//		::PostMessage(m_Handle, CM_FFT, 0, 0);
			//}
		}

		static readonly double Atan_1 = Math.Atan(1.0);

		void makewt(int[]ip, double[]w) {
			int nw = w.Length;

			if(nw > 2){
				int    nwh   = nw >> 1; // half nw.
				double delta = Atan_1 / nwh;
				w[0] = 1;
				w[1] = 0;
				w[nwh] = Math.Cos(delta * nwh);
				w[nwh + 1] = w[nwh];
				for( int j = 2; j < nwh; j += 2){
					double x = Math.Cos(delta * j);
					double y = Math.Sin(delta * j);
					w[j] = x;
					w[j + 1] = y;
					w[nw - j] = y;
					w[nw - j + 1] = x;
				}
				bitrv2(nw, ip, w);
			}
		}

		void makect(double[] c) {
			int nch, j;
			double delta;
			int nc = c.Length;

			if(nc > 1){
				nch = nc >> 1;
				delta = Atan_1 / nch;
				c[0] = Math.Cos(delta * nch);
				c[nch] = 0.5 * c[0];
				for(j = 1; j < nch; j++){
					c[j] = 0.5 * Math.Cos(delta * j);
					c[nc - j] = 0.5 * Math.Sin(delta * j);
				}
			}
		}

		void bitrv2(int n, int[] ip, double[] a) {
			int j, j1, k, k1, l, m, m2;
			double xr, xi;

			ip[0] = 0;
			l = n;
			m = 1;
			while((m << 2) < l){
				l >>= 1;
				for (j = 0; j < m; j++) {
					ip[m + j] = ip[j] + l;
				}
				m <<= 1;
			}
			if((m << 2) > l){
				for (k = 1; k < m; k++) {
					for (j = 0; j < k; j++) {
						j1 = (j << 1) + ip[k];
						k1 = (k << 1) + ip[j];
						xr = a[j1];
						xi = a[j1 + 1];
						a[j1] = a[k1];
						a[j1 + 1] = a[k1 + 1];
						a[k1] = xr;
						a[k1 + 1] = xi;
					}
				}
			} else {
				m2 = m << 1;
				for(k = 1; k < m; k++){
					for(j = 0; j < k; j++){
						j1 = (j << 1) + ip[k];
						k1 = (k << 1) + ip[j];
						xr = a[j1];
						xi = a[j1 + 1];
						a[j1] = a[k1];
						a[j1 + 1] = a[k1 + 1];
						a[k1] = xr;
						a[k1 + 1] = xi;
						j1 += m2;
						k1 += m2;
						xr = a[j1];
						xi = a[j1 + 1];
						a[j1] = a[k1];
						a[j1 + 1] = a[k1 + 1];
						a[k1] = xr;
						a[k1 + 1] = xi;
					}
				}
			}
		}

		void cftfsub(int n, double[]a, double []w)	{
			int j, j1, j2, j3, l;
			double x0r, x0i, x1r, x1i, x2r, x2i, x3r, x3i;

			l = 2;
			if(n > 8){
				cft1st(n, a, w);
				l = 8;
				while((l << 2) < n){
					cftmdl(n, l, a, w);
					l <<= 2;
				}
			}
			if((l << 2) == n){
				for(j = 0; j < l; j += 2){
					j1 = j + l;
					j2 = j1 + l;
					j3 = j2 + l;
					x0r = a[j] + a[j1];
					x0i = a[j + 1] + a[j1 + 1];
					x1r = a[j] - a[j1];
					x1i = a[j + 1] - a[j1 + 1];
					x2r = a[j2] + a[j3];
					x2i = a[j2 + 1] + a[j3 + 1];
					x3r = a[j2] - a[j3];
					x3i = a[j2 + 1] - a[j3 + 1];
					a[j] = x0r + x2r;
					a[j + 1] = x0i + x2i;
					a[j2] = x0r - x2r;
					a[j2 + 1] = x0i - x2i;
					a[j1] = x1r - x3i;
					a[j1 + 1] = x1i + x3r;
					a[j3] = x1r + x3i;
					a[j3 + 1] = x1i - x3r;
				}
			} else {
				for(j = 0; j < l; j += 2){
					j1 = j + l;
					x0r = a[j] - a[j1];
					x0i = a[j + 1] - a[j1 + 1];
					a[j] += a[j1];
					a[j + 1] += a[j1 + 1];
					a[j1] = x0r;
					a[j1 + 1] = x0i;
				}
			}
		}

		void cft1st(int n, double[]a, double[]w) {
			int j, k1, k2;
			double wk1r, wk1i, wk2r, wk2i, wk3r, wk3i;
			double x0r, x0i, x1r, x1i, x2r, x2i, x3r, x3i;

			x0r = a[0] + a[2];
			x0i = a[1] + a[3];
			x1r = a[0] - a[2];
			x1i = a[1] - a[3];
			x2r = a[4] + a[6];
			x2i = a[5] + a[7];
			x3r = a[4] - a[6];
			x3i = a[5] - a[7];
			a[0] = x0r + x2r;
			a[1] = x0i + x2i;
			a[4] = x0r - x2r;
			a[5] = x0i - x2i;
			a[2] = x1r - x3i;
			a[3] = x1i + x3r;
			a[6] = x1r + x3i;
			a[7] = x1i - x3r;
			wk1r = w[2];
			x0r = a[8] + a[10];
			x0i = a[9] + a[11];
			x1r = a[8] - a[10];
			x1i = a[9] - a[11];
			x2r = a[12] + a[14];
			x2i = a[13] + a[15];
			x3r = a[12] - a[14];
			x3i = a[13] - a[15];
			a[8] = x0r + x2r;
			a[9] = x0i + x2i;
			a[12] = x2i - x0i;
			a[13] = x0r - x2r;
			x0r = x1r - x3i;
			x0i = x1i + x3r;
			a[10] = wk1r * (x0r - x0i);
			a[11] = wk1r * (x0r + x0i);
			x0r = x3i + x1r;
			x0i = x3r - x1i;
			a[14] = wk1r * (x0i - x0r);
			a[15] = wk1r * (x0i + x0r);
			k1 = 0;
			for(j = 16; j < n; j += 16){
				k1 += 2;
				k2 = k1 << 1;
				wk2r = w[k1];
				wk2i = w[k1 + 1];
				wk1r = w[k2];
				wk1i = w[k2 + 1];
				wk3r = wk1r - 2 * wk2i * wk1i;
				wk3i = 2 * wk2i * wk1r - wk1i;
				x0r = a[j] + a[j + 2];
				x0i = a[j + 1] + a[j + 3];
				x1r = a[j] - a[j + 2];
				x1i = a[j + 1] - a[j + 3];
				x2r = a[j + 4] + a[j + 6];
				x2i = a[j + 5] + a[j + 7];
				x3r = a[j + 4] - a[j + 6];
				x3i = a[j + 5] - a[j + 7];
				a[j] = x0r + x2r;
				a[j + 1] = x0i + x2i;
				x0r -= x2r;
				x0i -= x2i;
				a[j + 4] = wk2r * x0r - wk2i * x0i;
				a[j + 5] = wk2r * x0i + wk2i * x0r;
				x0r = x1r - x3i;
				x0i = x1i + x3r;
				a[j + 2] = wk1r * x0r - wk1i * x0i;
				a[j + 3] = wk1r * x0i + wk1i * x0r;
				x0r = x1r + x3i;
				x0i = x1i - x3r;
				a[j + 6] = wk3r * x0r - wk3i * x0i;
				a[j + 7] = wk3r * x0i + wk3i * x0r;
				wk1r = w[k2 + 2];
				wk1i = w[k2 + 3];
				wk3r = wk1r - 2 * wk2r * wk1i;
				wk3i = 2 * wk2r * wk1r - wk1i;
				x0r = a[j + 8] + a[j + 10];
				x0i = a[j + 9] + a[j + 11];
				x1r = a[j + 8] - a[j + 10];
				x1i = a[j + 9] - a[j + 11];
				x2r = a[j + 12] + a[j + 14];
				x2i = a[j + 13] + a[j + 15];
				x3r = a[j + 12] - a[j + 14];
				x3i = a[j + 13] - a[j + 15];
				a[j + 8] = x0r + x2r;
				a[j + 9] = x0i + x2i;
				x0r -= x2r;
				x0i -= x2i;
				a[j + 12] = -wk2i * x0r - wk2r * x0i;
				a[j + 13] = -wk2i * x0i + wk2r * x0r;
				x0r = x1r - x3i;
				x0i = x1i + x3r;
				a[j + 10] = wk1r * x0r - wk1i * x0i;
				a[j + 11] = wk1r * x0i + wk1i * x0r;
				x0r = x1r + x3i;
				x0i = x1i - x3r;
				a[j + 14] = wk3r * x0r - wk3i * x0i;
				a[j + 15] = wk3r * x0i + wk3i * x0r;
			}
		}

		void cftmdl(int n, int l, double[]a, double[]w)	{
			int j, j1, j2, j3, k, k1, k2, m, m2;
			double wk1r, wk1i, wk2r, wk2i, wk3r, wk3i;
			double x0r, x0i, x1r, x1i, x2r, x2i, x3r, x3i;

			m = l << 2;
			for(j = 0; j < l; j += 2){
				j1 = j + l;
				j2 = j1 + l;
				j3 = j2 + l;
				x0r = a[j] + a[j1];
				x0i = a[j + 1] + a[j1 + 1];
				x1r = a[j] - a[j1];
				x1i = a[j + 1] - a[j1 + 1];
				x2r = a[j2] + a[j3];
				x2i = a[j2 + 1] + a[j3 + 1];
				x3r = a[j2] - a[j3];
				x3i = a[j2 + 1] - a[j3 + 1];
				a[j] = x0r + x2r;
				a[j + 1] = x0i + x2i;
				a[j2] = x0r - x2r;
				a[j2 + 1] = x0i - x2i;
				a[j1] = x1r - x3i;
				a[j1 + 1] = x1i + x3r;
				a[j3] = x1r + x3i;
				a[j3 + 1] = x1i - x3r;
			}
			wk1r = w[2];
			for(j = m; j < l + m; j += 2){
				j1 = j + l;
				j2 = j1 + l;
				j3 = j2 + l;
				x0r = a[j] + a[j1];
				x0i = a[j + 1] + a[j1 + 1];
				x1r = a[j] - a[j1];
				x1i = a[j + 1] - a[j1 + 1];
				x2r = a[j2] + a[j3];
				x2i = a[j2 + 1] + a[j3 + 1];
				x3r = a[j2] - a[j3];
				x3i = a[j2 + 1] - a[j3 + 1];
				a[j] = x0r + x2r;
				a[j + 1] = x0i + x2i;
				a[j2] = x2i - x0i;
				a[j2 + 1] = x0r - x2r;
				x0r = x1r - x3i;
				x0i = x1i + x3r;
				a[j1] = wk1r * (x0r - x0i);
				a[j1 + 1] = wk1r * (x0r + x0i);
				x0r = x3i + x1r;
				x0i = x3r - x1i;
				a[j3] = wk1r * (x0i - x0r);
				a[j3 + 1] = wk1r * (x0i + x0r);
			}
			k1 = 0;
			m2 = m << 1;
			for(k = m2; k < n; k += m2){
				k1 += 2;
				k2 = k1 << 1;
				wk2r = w[k1];
				wk2i = w[k1 + 1];
				wk1r = w[k2];
				wk1i = w[k2 + 1];
				wk3r = wk1r - 2 * wk2i * wk1i;
				wk3i = 2 * wk2i * wk1r - wk1i;
				for(j = k; j < l + k; j += 2){
					j1 = j + l;
					j2 = j1 + l;
					j3 = j2 + l;
					x0r = a[j] + a[j1];
					x0i = a[j + 1] + a[j1 + 1];
					x1r = a[j] - a[j1];
					x1i = a[j + 1] - a[j1 + 1];
					x2r = a[j2] + a[j3];
					x2i = a[j2 + 1] + a[j3 + 1];
					x3r = a[j2] - a[j3];
					x3i = a[j2 + 1] - a[j3 + 1];
					a[j] = x0r + x2r;
					a[j + 1] = x0i + x2i;
					x0r -= x2r;
					x0i -= x2i;
					a[j2] = wk2r * x0r - wk2i * x0i;
					a[j2 + 1] = wk2r * x0i + wk2i * x0r;
					x0r = x1r - x3i;
					x0i = x1i + x3r;
					a[j1] = wk1r * x0r - wk1i * x0i;
					a[j1 + 1] = wk1r * x0i + wk1i * x0r;
					x0r = x1r + x3i;
					x0i = x1i - x3r;
					a[j3] = wk3r * x0r - wk3i * x0i;
					a[j3 + 1] = wk3r * x0i + wk3i * x0r;
				}
				wk1r = w[k2 + 2];
				wk1i = w[k2 + 3];
				wk3r = wk1r - 2 * wk2r * wk1i;
				wk3i = 2 * wk2r * wk1r - wk1i;
				for(j = k + m; j < l + (k + m); j += 2){
					j1 = j + l;
					j2 = j1 + l;
					j3 = j2 + l;
					x0r = a[j] + a[j1];
					x0i = a[j + 1] + a[j1 + 1];
					x1r = a[j] - a[j1];
					x1i = a[j + 1] - a[j1 + 1];
					x2r = a[j2] + a[j3];
					x2i = a[j2 + 1] + a[j3 + 1];
					x3r = a[j2] - a[j3];
					x3i = a[j2 + 1] - a[j3 + 1];
					a[j] = x0r + x2r;
					a[j + 1] = x0i + x2i;
					x0r -= x2r;
					x0i -= x2i;
					a[j2] = -wk2i * x0r - wk2r * x0i;
					a[j2 + 1] = -wk2i * x0i + wk2r * x0r;
					x0r = x1r - x3i;
					x0i = x1i + x3r;
					a[j1] = wk1r * x0r - wk1i * x0i;
					a[j1 + 1] = wk1r * x0i + wk1i * x0r;
					x0r = x1r + x3i;
					x0i = x1i - x3r;
					a[j3] = wk3r * x0r - wk3i * x0i;
					a[j3 + 1] = wk3r * x0i + wk3i * x0r;
				}
			}
		}

		void rftfsub(int n, double[]a, double[]c) {
			int j, k, kk, ks, m;
			double wkr, wki, xr, xi, yr, yi;
			double  d;
			int nc = c.Length;

			ks = (nc << 2) / n;
			kk = 0;
			m = n >> 1;
			j = n - 2;
			if(m_StgSW){
				for (k = 2; k <= m; k += 2, j -= 2 ){
					kk += ks;
					wkr = 0.5 - c[nc - kk];
					wki = c[kk];
					xr = a[k] - a[j];
					xi = a[k + 1] + a[j + 1];
					yr = wkr * xr - wki * xi;
					yi = wkr * xi + wki * xr;
					a[k] -= yr;
					xi = a[k]*a[k];
					a[k+1] -= yi;
					xi += ( a[k+1]*a[k+1]);
					a[j] += yr;
					xr = a[j]*a[j];
					a[j+1] -= yi;
					xr += (a[j+1]*a[j+1]);
					if( xi <= 0 ) xi = 0.0001;
					if( xi >= 1e38 ) xi = 1e38;
					if( xr <= 0 ) xr = 0.0001;
					if( xr >= 1e38 ) xr = 1e38;
					if( m_oFFTCtrl.FFTSize == 1024 ){
						xi *= 4;
						xr *= 4;
					}
					switch(m_FFTGain){
						case 0:
							m_pStgBuf[k] = m_StgK*m_pStgBuf[k] + m_StgScale*(Math.Log10(xi+LOGADJ) + SCALEADJ_1);
							m_pStgBuf[j] = m_StgK*m_pStgBuf[j] + m_StgScale*(Math.Log10(xr+LOGADJ) + SCALEADJ_1);
							break;
						case 1:
							m_pStgBuf[k] = m_StgK*m_pStgBuf[k] + m_StgScale*(Math.Log10(xi+LOGADJ) + SCALEADJ_2);
							m_pStgBuf[j] = m_StgK*m_pStgBuf[j] + m_StgScale*(Math.Log10(xr+LOGADJ) + SCALEADJ_2);
							break;
						case 2:
							m_pStgBuf[k] = m_StgK*m_pStgBuf[k] + m_StgScale*(Math.Log10(xi+LOGADJ) + SCALEADJ_3);
							m_pStgBuf[j] = m_StgK*m_pStgBuf[j] + m_StgScale*(Math.Log10(xr+LOGADJ) + SCALEADJ_3);
							break;
						case 3:
							m_pStgBuf[k] = m_StgK*m_pStgBuf[k] + m_StgScale*(Math.Log10(xi+LOGADJ) + SCALEADJ_4);
							m_pStgBuf[j] = m_StgK*m_pStgBuf[j] + m_StgScale*(Math.Log10(xr+LOGADJ) + SCALEADJ_4);
							break;
						case 4:
							d = xi * 32e-10;
							m_pStgBuf[k] = m_StgK*m_pStgBuf[k] + m_StgScale*d;
							d = xr * 32e-10;
							m_pStgBuf[j] = m_StgK*m_pStgBuf[j] + d;
							break;
						case 5:
							d = xi * 96e-10;
							m_pStgBuf[k] = m_StgK*m_pStgBuf[k] + m_StgScale*d;
							d = xr * 96e-10;
							m_pStgBuf[j] = m_StgK*m_pStgBuf[j] + d;
							break;
						case 6:
							d = xi * 256e-10;
							m_pStgBuf[k] = m_StgK*m_pStgBuf[k] + m_StgScale*d;
							d = xr * 256e-10;
							m_pStgBuf[j] = m_StgK*m_pStgBuf[j] + d;
							break;
						default:
							d = xi * 512e-10;
							m_pStgBuf[k] = m_StgK*m_pStgBuf[k] + m_StgScale*d;
							d = xr * 512e-10;
							m_pStgBuf[j] = m_StgK*m_pStgBuf[j] + d;
							break;
					}
				}
			} else {
				for (k = 2; k <= m; k += 2, j -= 2 ){
					kk += ks;
					wkr = 0.5 - c[nc - kk];
					wki = c[kk];
					xr = a[k] - a[j];
					xi = a[k + 1] + a[j + 1];
					yr = wkr * xr - wki * xi;
					yi = wkr * xi + wki * xr;
					a[k] -= yr;
					xi = a[k]*a[k];
					a[k+1] -= yi;
					xi += ( a[k+1]*a[k+1]);
					a[j] += yr;
					xr = a[j]*a[j];
					a[j+1] -= yi;
					xr += (a[j+1]*a[j+1]);
					if( xi <= 0 ) xi = 0.0001;
					if( xi >= 1e38 ) xi = 1e38;
					if( xr <= 0 ) xr = 0.0001;
					if( xr >= 1e38 ) xr = 1e38;
					if( m_oFFTCtrl.FFTSize == 1024 ){
						xi *= 4;
						xr *= 4;
					}
					switch(m_FFTGain){
						case 0:
							m_pStgBuf[k] = Math.Log10(xi+LOGADJ)+SCALEADJ_1;
							m_pStgBuf[j] = Math.Log10(xr+LOGADJ)+SCALEADJ_1;
							break;
						case 1:
							m_pStgBuf[k] = Math.Log10(xi+LOGADJ)+SCALEADJ_2;
							m_pStgBuf[j] = Math.Log10(xr+LOGADJ)+SCALEADJ_2;
							break;
						case 2:
							m_pStgBuf[k] = Math.Log10(xi+LOGADJ)+SCALEADJ_3;
							m_pStgBuf[j] = Math.Log10(xr+LOGADJ)+SCALEADJ_3;
							break;
						case 3:
							m_pStgBuf[k] = Math.Log10(xi+LOGADJ)+SCALEADJ_4;
							m_pStgBuf[j] = Math.Log10(xr+LOGADJ)+SCALEADJ_4;
							break;
						case 4:
							m_pStgBuf[k] = xi * 32e-10;
							m_pStgBuf[j] = xr * 32e-10;
							break;
						case 5:
							m_pStgBuf[k] = xi * 96e-10;
							m_pStgBuf[j] = xr * 96e-10;
							break;
						case 6:
							m_pStgBuf[k] = xi * 256e-10;
							m_pStgBuf[j] = xr * 256e-10;
							break;
						default:
							m_pStgBuf[k] = xi * 512e-10;
							m_pStgBuf[j] = xr * 512e-10;
							break;
					}
				}
			}
			m_pStgBuf[m_oFFTCtrl.FFTSize/2] = m_pStgBuf[(m_oFFTCtrl.FFTSize/2) - 2];
		}

		/// <summary>
		/// ＦＦＴ処理クラス
		/// </summary>
		/// <param name="oControl"></param>
		public CFFT( FFTControlValues oControl ) {
			m_oFFTCtrl = oControl ?? throw new ArgumentNullException( "Need control values." );
			m_FFTDIS   = 0;

			// This variable is bizzare in that FFT_BUFSIZE exists regardless of the fact FFTSize
			// is used in the code as well. 
			// My guess is that since the max value that FFTSize (FFT_SIZE) ever get's
			// set to is 2048. So we can use the const FFT_BUFSIZE which is set to 2048.
			// Given I used the C# array length instead of passing the length seperately, in various
			// places in this code, I'll make them the same value.
			int FFT_BUFSIZE = m_oFFTCtrl.FFTSize;

			m_Work = new int[(int)Math.Sqrt( FFT_BUFSIZE ) ]; // Was sqrt(fft_bufsize)+1 but doesnt seem to be needed.

			// Note: FFT_BUFSIZE was a define, but FFT_BUFSIZE is global and changes.
			m_tSinCos0 = new double[FFT_BUFSIZE/4];
			m_tSinCos1 = new double[(FFT_BUFSIZE/4) + 1]; // We gp fault unless I add one.
			m_tWindow  = new double[FFT_BUFSIZE];
			m_pStgBuf  = new double[FFT_BUFSIZE]; // This definitely has to be at least equal to FFTSize.

			InitFFT();
		}

		/// <summary>
		/// It's quite possible the original implentation allowed you to resize the FFT and then
		/// call InitFFT(); I'm going to require a you to toss the FFT and make a new one.
		/// </summary>
		protected virtual void InitFFT()	{
			m_Work.Initialize(); //memset(m_Work, 0, sizeof(int[SQRT_FFT_SIZE+2]));

			//m_Work[0] = m_oFFTCtrl.FFTSize/4;
			//m_Work[1] = m_oFFTCtrl.FFTSize/4;

			// Note: m_Work is sqrt of FFTSize in length.
			makewt( m_Work, m_tSinCos0 );
			makect( m_tSinCos1 );

			// Different window types possible, probably for test, but use the HANNING window. Not Hamming.
			// Note: The Blackman one looks wrong, .46 s/b .42?
			for(int i = 0; i < m_oFFTCtrl.FFTSize; i++){
				m_pStgBuf[i] = 1.0;
		//		m_tWindow[i] = 1.0; // Untapered input.
		//		m_tWindow[i] = (0.5 - 0.5*cos( (PI2*i)/(FFT_SIZE-1) ));	
				m_tWindow[i] = (0.5 - 0.5*Math.Cos( (PI2*i)/m_oFFTCtrl.FFTSize ));	// ハニング窓, Hanning window.
		//		m_tWindow[i] = 0.46 - 0.5*Math.Cos( (PI2*i)/m_oFFTCtrl.FFTSize ) + 0.08*Math.Cos(2*PI2*i/m_oFFTCtrl.FFTSize);	// Blackman window.
			}
			m_StgSize      = 1;
			m_StgScale     = 1.0;
			m_StgK         = 0.0;
			m_StgSW        = false;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="rgInBuf"></param>
		/// <param name="dbGain"></param>
		/// <param name="iStg">This value comes from m_FFTResp and looks like it can be 
		///					  0-Fast, 1-Medium, 2,-Slow</param>
		/// <param name="rgOutBuf"></param>
		public void Calc( double [] rgInBuf, double dbGain, int iStg, int[] rgOutBuf) {
			if( m_FFTDIS > 0 ) 
				return;		// for math error
			if( rgInBuf.Length != m_oFFTCtrl.FFTSize )
				throw new ArgumentException( "invalid input buff" );
			if( rgInBuf.Length > m_tWindow.Length )
				throw new ArgumentException( "Input buffer is too large." );
			if( m_oFFTCtrl.TopBucket > m_pStgBuf.Length / 2 )
				throw new ArgumentException( "FFT Window is incorrect" );
			if( iStg > 2 )
				throw new ArgumentException( "stg must not be greater than 2" );

			m_FFTDIS++;

			m_StgSW = ( iStg > 1 );
			m_StgSize = iStg;

			if( m_StgSW ){
				m_StgScale = 1.0 / (double)m_StgSize;
				m_StgK     = 1.0 - m_StgScale;
			} else {
				m_StgScale = 1.0;
				m_StgK     = 0.0;
			}

			try {
				// While our math is double our values max are float.
				// Here's where we apply the (Hanning) window to the data.
				for( int i=0; i < rgInBuf.Length; i++ ){
					if( rgInBuf[i] > 32768.0 ){
						rgInBuf[i] = 32768.0;
					}
					else if( rgInBuf[i] < -32768.0 ){
						rgInBuf[i] = -32768.0;
					}
					rgInBuf[i] *= m_tWindow[i]; // This is why we should probably alloc window size to FTTSize
				}

				bitrv2 ( m_oFFTCtrl.FFTSize, m_Work,  rgInBuf );
				cftfsub( m_oFFTCtrl.FFTSize, rgInBuf, m_tSinCos0 );
				rftfsub( m_oFFTCtrl.FFTSize, rgInBuf, m_tSinCos1 );

				for( int i = 0, dp = 0; i < m_oFFTCtrl.TopBucket; i++, dp += 2 ) {
					rgOutBuf[i] = (int)(dbGain * m_pStgBuf[dp]);
				}
			} catch( Exception oEx ) {
				Type[] rgErrors = { typeof( IndexOutOfRangeException ),
									typeof( ArgumentOutOfRangeException ),
									typeof( NullReferenceException ) };
				if( !rgErrors.Contains( oEx.GetType() )  )
					throw;
				// Would be nice to report the error.
			}

			m_FFTDIS--;
		}

	} // End class.

	/// <summary>
	/// The pagination of the data might be an attempt to keep the
	/// working data set within the L1 cache of circa 2000 Pentium™ of about 8K. Given
	/// my Ryzen™ 5 1600X cache is 576KB, this code might be totally bogus to keep around.
	/// This is an excellent reason to have this code as a subclass and I can try simplifying
	/// later.
	/// </summary>
	public class CFFTCollector : CFFT {
		readonly double[][]	m_CollectFFTBuf = new double[3][]; // Jagged array.

		int     m_CollectrPage    = 0;     // Which collect page we are reading. ( Which page sent to Calc() )
		int     m_CollectwPage	  = 0;     // Which collect page we are writing. ( Which page we are loading from data )
		int     m_CollectCount    = 0;
		bool	m_CollectFFT      = false; // Whether we are collecting or not.
		int		m_CollectFFTCount = 0;     // Looks like how much data we've collected for the FFT

		/// <summary>
		/// Is there any data to be collected.
		/// </summary>
		/// <returns></returns>
		public bool IsData() {
			return m_CollectCount > 0;
		}

		public CFFTCollector( FFTControlValues oValues ) : base( oValues ) {
			// Note: the FFTSize is either 1024 or 2048 currently.
			for( int i=0; i<m_CollectFFTBuf.Length; ++i ) {
				m_CollectFFTBuf[i] = new double[m_oFFTCtrl.FFTSize];
			};
		}

        protected override void InitFFT() {
            base.InitFFT();

			m_CollectFFT      = false;
			m_CollectFFTCount = 0;

			m_CollectCount    = 0; // See the weird dupication of CollectFFT in the original code.
        }

		/// <summary>
		/// データ収集（スレッド外で実行する）, Data collection (executed outside the thread)
		/// Honestly, this looks REALLY hacky. Obviously relies on some weird multithreaded behavior.
		/// My guess is that the thread is spinning on the FFT and this is used to turn it on and
		/// off. It's a little like how I use the buffer size int in my MP3 player. Honestly why
		/// can't they just start and stop the thread?
		/// </summary>
		public void TrigFFT() {
			m_CollectFFT      = true;	    // Looks like a cheap semaphore. Stop FFT we are collecting
			m_CollectFFTCount = 0;
			m_CollectrPage    = 0;
			m_CollectwPage    = 0;
			m_CollectCount    = 0;
			//m_fqc.m_fq        = ZEROFQ;

			m_CollectFFT      = false;		// 次の収集をトリガ, Trigger the next collection.
		}

        /// <summary>
        /// Reading data in some pages while running the FFT loaded page. maximum of (3) sets.
        /// </summary>
        /// <param name="gain"></param>
        /// <param name="stg">This value comes from m_FFTResp and looks like it can be 0-Fast, 1-Medium, 2,-Slow</param>
        public void CalcFFT( double gain, int stg) {
			Calc(m_CollectFFTBuf[m_CollectrPage], gain, stg, m_fft);

			m_CollectrPage++;
			if( m_CollectrPage >= 3 )
				m_CollectrPage = 0;

			if( m_CollectCount > 0 ) 
				m_CollectCount--;
		}

		/// <summary>
		/// Collect input from the sound card for the input to the fft. I this code
		/// is also performing "decimation" depending on the FFTSampType (Sample Type).
		/// It looks like it's a pagination system that is on one hand reading the pages
		/// from the sound card, and on the other side, writing to the FFT.
		/// </summary>
		/// <param name="iFFTPriority">from global sys.m_FFTPriority in original code. 0-4 levels.
		/// Looks like "Spectral response"/"Calculation priority" from the menu.</param>
		/// <param name="lp">Input from the sound source.</param>
		/// <param name="dsize">Size of input.</param>
		/// <remarks>I don't mind saying this is a hideous morass of code. But I'm going 
		/// to port it directly as possible and refine it should I ever get working.
		/// </remarks>
		/// <seealso cref="CFFTCollector"/>
		/// <exception cref="ApplicationException" />
		public void CollectFFT( int iFFTPriority, double[] lp, int dsize) {
			// Probably if Disable Paint or CollectFFT true, which I think means
			// it's triggered and thus calculating so don't grab anything.
			if( /* DisPaint || */ m_CollectFFT ) 
				return;

			if( iFFTPriority > 0 ) {
				int iSrcOffset = 0; // Keep track of where we are in the input "lp" buffer.
				while( (m_CollectCount < 3) && ( dsize > 0 ) ) {
					int size;
					switch( m_oFFTCtrl.FFTSampType ){
						case FFTSampleType.X4OVERSAMPLE: {
							size = dsize / 4;
							if( (size + m_CollectFFTCount) > m_oFFTCtrl.FFTSize ){
								size = m_oFFTCtrl.FFTSize - m_CollectFFTCount;
							}
							dsize -= (size * 4);
							//for( tp = &m_CollectFFTBuf[m_CollectwPage][m_CollectFFTCount], i = 0; i < size; i++, tp++, lp+=4 ){
							//	*tp = *lp;
							//}
							for( int i= 0; i< size; i++, iSrcOffset += 4 ) {
								m_CollectFFTBuf[m_CollectwPage][m_CollectFFTCount+i] = lp[iSrcOffset];
							}
							} break;
						case FFTSampleType.X2OVERSAMPLE: {
							size = dsize / 2;
							if( (size + m_CollectFFTCount) > m_oFFTCtrl.FFTSize ){
								size = m_oFFTCtrl.FFTSize - m_CollectFFTCount;
							}
							dsize -= (size * 2);
							//for( tp = &m_CollectFFTBuf[m_CollectwPage][m_CollectFFTCount], i = 0; i < size; i++, tp++, lp+=2 ){
							//	*tp = *lp;
							//}
							for( int i= 0; i< size; i++, iSrcOffset += 2 ) {
								m_CollectFFTBuf[m_CollectwPage][m_CollectFFTCount+i] = lp[iSrcOffset];
							}
							}  break;
						case FFTSampleType.NOOVERSAMPLE:
							size = dsize;
							if( (size + m_CollectFFTCount) > m_oFFTCtrl.FFTSize ){
								size = m_oFFTCtrl.FFTSize - m_CollectFFTCount;
							}
							dsize -= size;
							// memcpy(&m_CollectFFTBuf[m_CollectwPage][m_CollectFFTCount], lp, sizeof(double)*size);
							Array.Copy( lp, iSrcOffset, m_CollectFFTBuf[m_CollectwPage], m_CollectFFTCount,  size ); 
							//lp += size;
							iSrcOffset += size;
							break;
						default:
							throw new ApplicationException( "Invalid sampling type");
					}
					m_CollectFFTCount += size;
					if( m_CollectFFTCount >= m_oFFTCtrl.FFTSize ) {
						int iPrevPage = m_CollectwPage; // was "page"
						m_CollectwPage++;
						if( m_CollectwPage >= 3 )
							m_CollectwPage = 0;
						m_CollectCount++;

						// Not sure what this is doing yet. My guess is that we copy forward data depending on priority.
						switch( iFFTPriority ){
							case 1:
								m_CollectFFTCount = 0;
								break;
							case 2:
								m_CollectFFTCount = 128;
								//memcpy(m_CollectFFTBuf[m_CollectwPage], &m_CollectFFTBuf[page][_oFFTCtrl.FFTSize-128], sizeof(double)*128);
								Array.Copy( m_CollectFFTBuf[iPrevPage], m_oFFTCtrl.FFTSize-128, m_CollectFFTBuf[m_CollectwPage], 0, 128 );
								break;
							case 3:
								m_CollectFFTCount = 256;
								//memcpy(m_CollectFFTBuf[m_CollectwPage], &m_CollectFFTBuf[page][_oFFTCtrl.FFTSize-256], sizeof(double)*256);
								Array.Copy( m_CollectFFTBuf[iPrevPage], m_oFFTCtrl.FFTSize-256, m_CollectFFTBuf[m_CollectwPage], 0, 256 );
								break;
							case 0:
							case 4:
								m_CollectFFTCount = 512;
								//memcpy(m_CollectFFTBuf[m_CollectwPage], &m_CollectFFTBuf[page][_oFFTCtrl.FFTSize-512], sizeof(double)*512);
								Array.Copy( m_CollectFFTBuf[iPrevPage], m_oFFTCtrl.FFTSize-512, m_CollectFFTBuf[m_CollectwPage], 0, 512 );
								break;
							default:
								throw new ApplicationException( "Invalid FFT priority. Must be 0 through 4" );
						}
						PostMessage();
					}
				}
			}
			else if( m_CollectCount > 0 ){
				int size;
				switch( m_oFFTCtrl.FFTSampType ) {
					case FFTSampleType.X4OVERSAMPLE:
						size = dsize / 4;
						if( (size + m_CollectFFTCount) > m_oFFTCtrl.FFTSize ){
							size = m_oFFTCtrl.FFTSize - m_CollectFFTCount;
						}
						//for( tp = &m_CollectFFTBuf[m_CollectwPage][m_CollectFFTCount], i = 0; i < size; i++, tp++, lp+=4 ){
						//	*tp = *lp;
						//}
						for( int i= 0, srcOffs = 0; i< size; i++, srcOffs += 4 ) {
							m_CollectFFTBuf[m_CollectwPage][m_CollectFFTCount+i] = lp[srcOffs];
						}
						break;
					case FFTSampleType.X2OVERSAMPLE:
						size = dsize / 2;
						if( (size + m_CollectFFTCount) > m_oFFTCtrl.FFTSize ){
							size = m_oFFTCtrl.FFTSize - m_CollectFFTCount;
						}
						//for( tp = &m_CollectFFTBuf[m_CollectwPage][m_CollectFFTCount], i = 0; i < size; i++, tp++, lp+=2 ){
						//	*tp = *lp;
						//}
						for( int i= 0, srcOffs = 0; i< size; i++, srcOffs += 2 ) {
							m_CollectFFTBuf[m_CollectwPage][m_CollectFFTCount+i] = lp[srcOffs];
						}
						break;
					case FFTSampleType.NOOVERSAMPLE:
						size = dsize;
						if( (size + m_CollectFFTCount) > m_oFFTCtrl.FFTSize ){
							size = m_oFFTCtrl.FFTSize - m_CollectFFTCount;
						}
						//memcpy(&m_CollectFFTBuf[m_CollectwPage][m_CollectFFTCount], lp, sizeof(double)*size);
						Array.Copy( lp, 0, m_CollectFFTBuf[m_CollectwPage], m_CollectFFTCount,  size ); 
						break;
					default:
						throw new ApplicationException( "Invalid Sampling Type" );
				}
				m_CollectFFTCount += size;
				if( m_CollectFFTCount >= m_oFFTCtrl.FFTSize ) {
					m_CollectwPage++;
					if( m_CollectwPage >= 3 )
						m_CollectwPage = 0;

					m_CollectCount++;
					m_CollectFFTCount = 0;
				}
			}
		} // End Method
	} // End class
}
