﻿using System;
using System.Collections.Generic;
using System.Text;

namespace Play.Sound {
	public enum FirFilt {
		ffLPF,
		ffHPF,
		ffBPF,
		ffBEF,
		ffUSER,
		ffLMS,
	};

	public class FIR {
		public readonly FirFilt	typ;

		readonly int    n; // taps
		readonly double fs;
		readonly double fcl;
		readonly double fch;
		readonly double att;
		readonly double gain;
		readonly double fc;

		readonly double[] hp = new double[CSSTVDEM.TAPMAX+1];		/* 係数配列 : Coefficient array */

		public FIR( int tap, FirFilt type, double fs, double fcl, double fch, double att, double gain)
		{
			typ = type;
			n   = tap;
			this.fs   = fs;
			this.fcl  = fcl;
			this.fch  = fch;
			this.att  = att;
			this.gain = gain;

			if( n > CSSTVDEM.TAPMAX )
				throw new ArgumentOutOfRangeException( "Tap is too large" );

			if( typ == FirFilt.ffHPF ){
				fc = 0.5*fs - fcl;
			} else if( typ != FirFilt.ffLPF ){
				fc = (fch - fcl)/2.0;
			} else {
				fc = fcl;
			}
		}

		static double I0( double x ) {
			double sum = 1.0;
			double xj  = 1.0;
			int    j   = 1;
			while(true){
				xj *= ((0.5 * x) / (double)j);
				sum += (xj*xj);
				j++;
				if( ((0.00000001 * sum) - (xj*xj)) > 0 ) 
					break;
			}
			return sum;
		}

		public void MakeFilter( double [] HP ) {
			int		j, m;
			double	alpha, win, fm, w0, sum;
			
			if( HP == hp )
				throw new ArgumentException( "array must not be our internal array" );

			if( att >= 50.0 ){
				alpha = 0.1102 * (att - 8.7);
			}
			else if( att >= 21 ){
				alpha = (0.5842 * Math.Pow(att - 21.0, 0.4)) + (0.07886 * (att - 21.0));
			}
			else {
				alpha = 0.0;
			}

			sum = Math.PI*2.0*fc/fs;
			if( att >= 21 ){ // インパルス応答と窓関数を計算 : Calculate impulse response and window function 
				for( j = 0; j <= (n/2); j++ ){
					fm = (double)(2 * j)/(double)n;
					win = I0(alpha * Math.Sqrt(1.0-(fm*fm)))/I0(alpha);
					if( j == 0 ){
						hp[j] = fc * 2.0/fs;
					} else {
						hp[j] = (1.0/(Math.PI*(double)j))*Math.Sin((double)j*sum)*win;
					}
				}
			} else {		// インパルス応答のみ計算 : Calculate impulse response only 
				for( j = 0; j <= (n/2); j++ ){
					if( j == 0 ){
						hp[j] = fc * 2.0/fs;
					}
					else {
						hp[j] = (1.0/(Math.PI*(double)j))*Math.Sin((double)j*sum);
					}
				}
			}
			sum = hp[0];
			for( j = 1; j <= (n/2); j++ ) {
				sum += 2.0 * hp[j];
			}
			if( sum > 0.0 ){
				for( j = 0; j <= (n/2); j++ ) 
					hp[j] /= sum;
			}

			// 周波数変換 : Frequency conversion.

			if( typ == FirFilt.ffHPF ){
				for( j = 0; j <= (n/2); j++ ) 
					hp[j] *= Math.Cos((double)j*Math.PI);
			}
			else if( typ != FirFilt.ffLPF ){
				w0 = Math.PI * (fcl + fch) / fs;
				if( typ == FirFilt.ffBPF ){
					for( j = 0; j <= (n/2); j++ ) 
						hp[j] *= 2.0*Math.Cos((double)j*w0);
				}
				else {
					hp[0] = 1.0 - (2.0 * hp[0]);
					for( j = 1; j <= (n/2); j++ ) 
						hp[j] *= -2.0*Math.Cos((double)j*w0);
				}
			}
			int i;
			for( i=0, m = n/2, j = m; j >= 0; j--, i++ ) {
				HP[i] = hp[m] * gain;
			}
			for( i=0, j = 1; j <= (n/2); j++, i++ ) {
				HP[i] = hp[j] * gain;
			}
		}
	}

	class CFIR2 {
	    int		 m_Tap;
	    double[] m_pZ;
        double[] m_pH;

        int	m_W;
        int	m_TapHalf;

        public CFIR2(){
            m_W  = 0;
            m_Tap = 0;
        }

        public void Create(int tap) {
	        if( tap == 0 ) {
		        m_pZ = null;
            } else { 
                if( (m_Tap != tap) || m_pZ == null ) {
		            m_pZ = new double[(tap+1)*2];
		            m_W  = 0;
                }
            }
	        m_Tap     = tap;
            m_TapHalf = tap/2;
        }

        void Create(int tap, FirFilt type, double fs, double fcl, double fch, double att, double gain)
        {
	        if( (m_Tap != tap) || m_pZ == null || m_pH == null ){
		        m_pZ = new double[(tap+1)*2];
		        m_pH = new double[tap+1];
		        m_W = 0;
            }
	        m_Tap     = tap;
            m_TapHalf = tap/2;

	        MakeFilter(m_pH, tap, type, fs, fcl, fch, att, gain);
        }

        void Clear()
        {
	        if( m_pZ != null )
                Array.Clear( m_pZ, 0, m_pZ.Length );
        }
        
        /* This is another dupe, but here we use our internal
         * "m_hp" array. Just use our 3 param Do() and pass m_hp.
         * see Do( double d ) below this one.
        public double Do(double d)
        {
	        double *dp1 = &m_pZ[m_W+m_Tap+1];
	        m_pZP = dp1;
	        *dp1 = d;
            m_pZ[m_W] = d;
            d = 0;
            double *hp = m_pH;
            for( int i = 0; i <= m_Tap; i++ ){
		        d += (*dp1--) * (*hp++);
            }
            m_W++;
	        if( m_W > m_Tap ) m_W = 0;
            return d;
        }
        */

        /* This looks ike a dupe of the three param Do() which returns
         * "j", I'm going to try to use the new "discards" feature of
         * c 7.0 .
         * 
        double Do(double d, double *hp)
        {
	        double *dp1 = &m_pZ[m_W+m_Tap+1];
	        m_pZP = dp1;
	        *dp1 = d;
            m_pZ[m_W] = d;
            d = 0;
            for( int i = 0; i <= m_Tap; i++ ){
		        d += (*dp1--) * (*hp++);
            }
            m_W++;
	        if( m_W > m_Tap ) 
                m_W = 0;
            return d;
        }
        */

        /// <summary>
        /// 
        /// </summary>
        /// <remarks>This looks like used to depend on side effect of
        /// m_pZP being set to &m_pZ[m_W+m_Tap+1]. I've removed that.
        /// But it depends on m_W & m_Tap being unchanged in the interrum.
        /// Not clear if that's a safe assumption yet.</remarks>
        double Do( double[] hp ) {
            int    iZP = m_W + m_Tap + 1;
            double d   = 0;

            if( iZP > m_pZ.Length )
                throw new InvalidOperationException();

            for( int i = 0; i <= m_Tap; i++ ){
		        d += m_pZ[iZP--] * hp[i];
            }

            return d;
        }

        public double Do( double d ) {
            return Do( m_pH, ref d, out _ );
        }


        public double Do( double[] hp, ref double d, out double j ) {
            int    iZP = m_W + m_Tap + 1;
            double dd  = 0;

            if( iZP > m_pZ.Length )
                throw new InvalidOperationException();

	        m_pZ[iZP] = d;
            m_pZ[m_W] = d;
            for( int i = 0; i <= m_Tap; i++ ){
		        dd += m_pZ[iZP--] * hp[i];
            }
            j = dd;
            d = m_pZ[m_W + m_TapHalf + 1];

            m_W++;
	        if( m_W > m_Tap ) 
                m_W = 0;

            return dd;
        }

		public static void MakeFilter( double[]HP, int tap, FirFilt type, 
			                    double fs,  double fcl, double fch, 
								double att, double gain )
		{
			FIR	fir = new FIR( tap, type, fs, fcl, fch, att, gain );

			fir.MakeFilter( HP );
		}
    } // End class

	public class CLMS
	{
		private static int LMSTAP = 192;

		double[] Z;					// FIR Z Application
		double[] D;					// LMS Delay;

		double	m_lmsADJSC;			// ƒXƒP[ƒ‹’²®’l
		double	m_lmsErr;			// LMS Œë·ƒf[ƒ^
		double	m_lmsMErr;			// LMS Œë·ƒf[ƒ^i~‚QƒÊj
		double  m_D;

		public int		m_Tap    { get; protected set; }
	//	int		m_lmsInv;			// LMS InvOutput
	//	int		m_lmsDelay;			// LMS Delay
		public int		m_lmsAGC { get; protected set; }	// LMS AGC
		public double	m_lmsMU2 { get; protected set; }	// LMS 2ƒÊ
		public double	m_lmsGM  { get; protected set; }	// LMS ƒÁ
		public double	[]H      { get; protected set; }	// ƒAƒvƒŠƒP[ƒVƒ‡ƒ“ƒtƒBƒ‹ƒ^‚ÌŒW”

		int     m_Tap_N;
		int     m_lmsDelay_N;
		double  m_lmsMU2_N;
		double  m_lmsGM_N;

		public CLMS( int iSampBase )
		{
			Z = new double[LMSTAP+1];
			H = new double[LMSTAP+1];
			D = new double[LMSTAP+1];
			m_D = 0;

			m_lmsADJSC = 1.0 / (double)(32768 * 32768);		// ƒXƒP[ƒ‹’²®’l
			m_lmsErr = m_lmsMErr = 0;

			m_Tap = (int)((4 * iSampBase/11025) + 0.5);
		//	m_Tap = int((8 * SampBase/11025) + 0.5);
			if( m_Tap > LMSTAP ) m_Tap = LMSTAP;
			m_lmsMU2 = 0.003;			// LMS 2ƒÊ
			m_lmsGM = 0.9999;			// LMS ƒÁ

			m_Tap_N = (int)((48 * iSampBase/11025) + 0.5);
			if( m_Tap_N > LMSTAP ) m_Tap_N = LMSTAP;
		//	m_lmsMU2_N = 0.00018;			// LMS 2ƒÊ
		//	m_lmsGM_N = 0.999999;			// LMS ƒÁ
			m_lmsMU2_N = 0.00018;			// LMS 2ƒÊ
			m_lmsGM_N = 0.999999;			// LMS ƒÁ
			m_lmsDelay_N = (int)((12 * iSampBase/11025) + 0.5);
			if( m_lmsDelay_N > LMSTAP ) m_lmsDelay_N = LMSTAP;
		}

		// “K‰žƒtƒBƒ‹ƒ^‚Ì‰‰ŽZ
		public double Do(double d)
		{
			double a = 0.0;
			int i;

			// ƒgƒ‰ƒ“ƒXƒo[ƒTƒ‹ƒtƒBƒ‹ƒ^
			//memcpy(Z, &Z[1], sizeof(double)*m_Tap);
			Array.Copy( Z, 1, Z, 0, m_Tap );
			Z[m_Tap] = m_D;
			for( i = 0; i <= m_Tap; i++ ){
				a += Z[i] * H[i];
			}
			// Œë·ŒvŽZ
			m_lmsErr = d - a;
			m_lmsMErr = m_lmsErr * m_lmsMU2 * m_lmsADJSC;	// lmsADJSC = 1/(32768 * 32768) ƒXƒP[ƒŠƒ“ƒO’²®’l

			// ’x‰„Ší‚ÌˆÚ“®
			m_D = d;
			// ŒW”XV
			double sum = 0;
			for( i = 0; i <= m_Tap; i++ ){
				H[i] = (m_lmsMErr * (Z[i])) + (H[i] * m_lmsGM);
				if( H[i] >= 0 ){
					sum += H[i];
				} else {
					sum -= H[i];
				}
			}
			if( sum > 0 ) a /= sum;
			return a;
		}

		// “K‰žƒtƒBƒ‹ƒ^‚Ì‰‰ŽZ
		public double DoN(double d)
		{
			double a = 0.0;
			int i;

			// ƒgƒ‰ƒ“ƒXƒo[ƒTƒ‹ƒtƒBƒ‹ƒ^
			Array.Copy( Z, 1, Z, 0, m_Tap_N );
			//memcpy(Z, &Z[1], sizeof(double)*m_Tap_N);
			Z[m_Tap_N] = D[0];
			for( i = 0; i <= m_Tap_N; i++ ){
				a += Z[i] * H[i];
			}
			// Œë·ŒvŽZ
			m_lmsErr = d - a;
			m_lmsMErr = m_lmsErr * m_lmsMU2_N * m_lmsADJSC;	// lmsADJSC = 1/(32768 * 32768) ƒXƒP[ƒŠƒ“ƒO’²®’l

			// ’x‰„Ší‚ÌˆÚ“®
			Array.Copy( D, 1, D, 0, m_lmsDelay_N );
			//memcpy(D, &D[1], sizeof(double)*m_lmsDelay_N);
			D[m_lmsDelay_N] = d;

			// ŒW”XV
			for( i = 0; i <= m_Tap_N; i++ ){
				H[i] = (m_lmsMErr * Z[i]) + (H[i] * m_lmsGM_N);
			}
			return m_lmsErr;
		}

		// “K‰žƒtƒBƒ‹ƒ^‚Ì‰‰ŽZ
		public void SetAN( int iSampBase, int sw)
		{
			m_Tap_N = (int)((48 * iSampBase/11025) + 0.5);
			if( m_Tap_N > LMSTAP ) 
				m_Tap_N = LMSTAP;

			m_lmsDelay_N = (int)((12 * iSampBase/11025) + 0.5);
			if( m_lmsDelay_N > LMSTAP ) 
				m_lmsDelay_N = LMSTAP;

			Array.Clear( Z, 0, Z.Length );
			Array.Clear( H, 0, Z.Length );
			Array.Clear( D, 0, Z.Length );

			switch(sw){
				case 1:
					m_lmsMU2_N = 0.00018;			// LMS 2ƒÊ
					m_lmsGM_N  = 0.999998;			// LMS ƒÁ
					break;
				default:
					m_lmsMU2_N = 0.00005;			// LMS 2ƒÊ
					m_lmsGM_N  = 0.9999985;			// LMS ƒÁ
					break;
			}
		}

		//-------------------------------------------------
		// ‘ŠŠÖŒvŽZ‚Ì‰‰ŽZ
		public int Sig(double d)
		{
			double a = 0.0;
			int i;

			// ƒgƒ‰ƒ“ƒXƒo[ƒTƒ‹ƒtƒBƒ‹ƒ^
			//memcpy(Z, &Z[1], sizeof(double)*m_Tap);
			Array.Copy( Z, 1, Z, 0, m_Tap );
			Z[m_Tap] = m_D;

			for( i = 0; i <= m_Tap; i++ ){
				a += Z[i] * H[i];
			}
			// Œë·ŒvŽZ
			m_lmsErr = d - a;
			m_lmsMErr = m_lmsErr * m_lmsMU2 * m_lmsADJSC;	// lmsADJSC = 1/(32768 * 32768) ƒXƒP[ƒŠƒ“ƒO’²®’l

			// ’x‰„Ší‚ÌˆÚ“®
			m_D = d;
			// ŒW”XV
			double sum = 0;
			for( i = 0; i <= m_Tap; i++ ){
				H[i] = (m_lmsMErr * Z[i]) + (H[i] * m_lmsGM);
				if( H[i] >= 0 ){
					sum += H[i];
				} else {
					sum -= H[i];
				}
			}
			return (int)(sum * 32768.0);
		}

	}

	/// <summary>This IIRTank thing is probably a simple parallel RLC tank circuit, BANDPASS filter.</summary>
	/// <remarks>
	/// Fred J. Taylor, in Encyclopedia of Physical Science and Technology (Third Edition), 2003:  First, if magnitude frequency 
	/// response is to be sharp or abrupt in terms of a transition between passbands and stopbands (called the filter skirt), 
	/// the IIR is the design of choice. If phase performance is the design objective, an FIR should be chosen. The IIRs are
	/// generally of low order (N ≤ 16), while the FIRs are usually of high order (N ≥ 16).
	/// </remarks>
	public class CIIRTANK {
		double	z1, z2;  // past 1 or 2 ago values

		double	a0;      // Coefficients for the filter.
		double	b1, b2;

		/// <summary>
		/// Sample frequency comes from the global value.
		/// B's are coeff's applied to the input. A's are coeff's to past values of the output.
		/// </summary>
		public CIIRTANK( double p_dbSampFreq )
		{
			b1 = b2 = a0 = z1 = z2 = 0;
			SetFreq(2000.0, p_dbSampFreq, 50.0); // dummy values.
		}

		/// <summary>
		/// Since the sampling frequency is all that changes as we this object we really could save
		/// our frequency responce and bandwidth. But that's a small thing.
		/// Note: ω = 2 π f
		/// </summary>
		/// <param name="f"  >The frequency we are looking for! Yay!</param>
		/// <param name="smp">Sampling frequency</param>
		/// <param name="bw" >Bandwidth perhaps??</param>
		/// <remarks>Why the lb? and la0 rigmarole? The world may never know.</remarks>
		public void SetFreq(double f, double smp, double bw)
		{
			double lb1, lb2, la0;
			lb1 = 2 * Math.Exp(  -Math.PI * bw/smp) * Math.Cos(2 * Math.PI * f/smp);
			lb2 =    -Math.Exp(-2*Math.PI * bw/smp);

			if( bw != 0 ){
				//const double _gt[]={18.0, 26.0, 20.0, 20.0};
				//la0 = sin(2 * PI * f/smp) / (_gt[SampType] * 50 / bw);
				la0 = Math.Sin(2 * Math.PI * f/smp) / ((smp/6.0) / bw);
			} else {
				la0 = Math.Sin(2 * Math.PI * f/smp);
			}
			b1 = lb1; b2 = lb2; a0 = la0;
		}

		public double Do(double d)
		{
			d *= a0;
			d += (z1 * b1);
			d += (z2 * b2);
			z2 = z1;
			if( Math.Abs(d) < 1e-37 ) 
				d = 0.0;
			z1 = d;
			return d;
		}
	}

	/// <remarks>
	/// This one is the most complicated port of the bunch. Probably should give it extra attention.
	/// </remarks>
	public class CIIR {
		protected static int IIRMAX = 16;

		readonly double[] Z = new double[IIRMAX*2];
		readonly double[] A = new double[IIRMAX*3];
		readonly double[] B = new double[IIRMAX*2];
		int		 m_order = 0;
		int		 m_bc;
		double	 m_rp;

		public CIIR() {
		}

		void Clear()
		{
			Array.Clear( Z, 0, Z.Length );
		}

		public void MakeIIR(double fc, double fs, int order, int bc, double rp)
		{
			m_order = order;
			m_bc = bc;
			m_rp = rp;
			MakeIIR(A, B, fc, fs, order, bc, rp);
		}

		// bc : 0-バターワース, 1-チェビシフ : 0-Butterworth, 1-Chevisif
		// rp : 通過域のリップル : Ripple in the pass area
		public static void MakeIIR(double []A, double[]B, double fc, double fs, int order, int bc, double rp)
		{
			double	w0, wa, u=0, zt, x;
			int		j, n;

			if( bc != 0 ){		// チェビシフ
				u = 1.0/(double)((order) * Math.Asinh(1.0/Math.Sqrt(Math.Pow(10.0,0.1*rp)-1.0)));
			}
			wa = Math.Tan(Math.PI*fc/fs);
			w0 = 1.0;
			n = (order & 1) + 1;
			int pA = 0;
			int pB = 0;
			double d1, d2;
			for( j = 1; j <= order/2; j++, pA+=3, pB+=2 ){
				if( bc != 0 ){	// チェビシフ
					d1 = Math.Sinh(u)*Math.Cos(n*Math.PI/(2*order));
					d2 = Math.Cosh(u)*Math.Sin(n*Math.PI/(2*order));
					w0 = Math.Sqrt(d1 * d1 + d2 * d2);
					zt = Math.Sinh(u)*Math.Cos(n*Math.PI/(2*order))/w0;
				}
				else {		// バターワース
					w0 = 1.0;
					zt = Math.Cos(n*Math.PI/(2*order));
				}
				A[pA] = 1 + wa*w0*2*zt + wa*w0*wa*w0;
				A[pA+1] = -2 * (wa*w0*wa*w0 - 1)/A[pA];
				A[pA+2] = -(1.0 - wa*w0*2*zt + wa*w0*wa*w0)/A[pA];
				B[pB] = wa*w0*wa*w0 / A[pA];
				B[pB+1] = 2*B[pB];
				n += 2;
			}
			if( bc != 0 && (order & 1) == 0 ){
				x = Math.Pow( 1.0/Math.Pow(10.0,rp/20.0), 1/(double)(order/2) );
				pB = 0;
				for( j = 1; j <= order/2; j++, pB+=2 ){
					B[pB] *= x;
					B[pB+1] *= x;
				}
			}
			if( ( order & 1 ) != 0 ){
				if( bc != 0 ) 
					w0 = Math.Sinh(u);
				j = (order / 2);
				pA = j*3;
				pB = j*2;
				A[pA] = 1 + wa*w0;
				A[pA+1] = -(wa*w0 - 1)/A[pA];
				B[pB] = wa*w0/A[pA];
				B[pB+1] = B[pB];
			}
		}

		public double Do(double d) {
			int pA = 0;
			int pB = 0;
			int pZ = 0;
			double o;

			for( int i = 0; i < m_order/2; i++, pA+=3, pB+=2, pZ+=2 ){
				d += Z[pZ] * A[pA+1] + Z[pZ+1] * A[pA+2];
				o = d * B[pB] + Z[pZ] * B[pB+1] + Z[pZ+1] * B[pB];
				Z[pZ+1] = Z[pZ];
				if( Math.Abs(d) < 1e-37 ) 
					d = 0.0;
				Z[pZ] = d;
				d = o;
			}
			if( ( m_order & 1 ) != 0 ){
				d += Z[pZ] * A[pA+1];
				o = d * B[pB] + Z[pZ] * B[pB];
				if( Math.Abs(d) < 1e-37 )
					d = 0.0;
				Z[pZ] = d;
				d = o;
			}
			return d;
		}
	}

	class CPLL {
		CIIR	loopLPF = new CIIR();
		CIIR	outLPF  = new CIIR();

		double	m_err;
		double	m_out;
		double	m_vcoout;
		double	m_SampleFreq;
		double  m_ToneOffset;
		double	m_FreeFreq;
		double	m_Shift;

		double  m_Max;
		double  m_Min;
		double  m_d;
		double  m_agc, m_agca;

		public int		m_loopOrder { get; protected set; }
		public double	m_loopFC    { get; protected set; }
		public int		m_outOrder  { get; protected set; }
		public double	m_outFC     { get; protected set; }
		public double	m_vcogain   { get; protected set; }
		public double   m_outgain   { get; protected set; }

		CVCO	vco;

		public double GetErr(){return m_err*32768;}	// Phase Det
		public double GetOut(){return m_out*32768;}	// Phase Det
		public double GetVco(){return m_vcoout;}

		/// <summary>
		/// If Sample frequency or ToneOffset changes we'll need to re-create this object
		/// </summary>
		public CPLL( double dbSampFreq, double dbToneOffset )
		{
			vco = new CVCO( dbSampFreq, dbToneOffset );

			m_err        = 0;
			m_out        = 0;
			m_vcoout     = 0;
			m_vcogain    = 1.0;
			m_outgain    = 32768.0 * m_vcogain;
			m_SampleFreq = dbSampFreq;
			m_ToneOffset = dbToneOffset;

			SetWidth     (false);
			SetSampleFreq(m_SampleFreq);

			m_Max  = 1.0;
			m_Min  = -1.0;
			m_d    = 0;
			m_agc  = 1.0;
			m_agca = 0.0;
		}

		void SetSampleFreq(double f)
		{
			m_SampleFreq = f;
			vco.SetSampleFreq(f);
			vco.SetFreeFreq(m_FreeFreq + m_ToneOffset); // Wuz global tone.
			SetVcoGain(1.0);
			MakeLoopLPF( 1, 1500 );
			MakeOutLPF ( 3,  900 );
		}

		public void SetWidth( bool fNarrow)
		{
			if( fNarrow ){
				m_Shift    = CSSTVDEM.NARROW_BW;
				m_FreeFreq = CSSTVDEM.NARROW_CENTER;
				SetFreeFreq( CSSTVDEM.NARROW_LOW, CSSTVDEM.NARROW_HIGH);
			} else {
				m_Shift = 800.0;
				m_FreeFreq = (1500 + 2300)*0.5;
				SetFreeFreq(1500, 2300);
			}
			SetVcoGain(m_vcogain);
		}

		public void SetVcoGain(double g)
		{
			m_vcogain = g;
			vco.SetGain(-m_Shift * g);
			m_outgain = 32768.0 * m_vcogain;
		}

		public void MakeLoopLPF( int iLoopOrder, int iLoopFreq )
		{
			m_loopOrder = iLoopOrder;
			m_loopFC    = iLoopFreq;

			loopLPF.MakeIIR(m_loopFC, m_SampleFreq, m_loopOrder, 0, 0);
		}

		public void MakeOutLPF( int iLoopOrder, int iLoopFreq )
		{
			m_outOrder = iLoopOrder;
			m_outFC    = iLoopFreq;

			outLPF.MakeIIR(m_outFC, m_SampleFreq, m_outOrder, 0, 0);
		}

		public void SetFreeFreq(double f1, double f2)
		{
			m_FreeFreq = (f1 + f2)/2.0;
			m_Shift = (f2 - f1);
			vco.SetFreeFreq(m_FreeFreq + m_ToneOffset); // Wuz global tone.
			vco.SetGain(-m_Shift * m_vcogain);
		}

		public double Do(double d)
		{
			if( m_Max < d ) m_Max = d;
			if( m_Min > d ) m_Min = d;
			if( (d >= 0) && (m_d < 0) ){
				m_agc  = m_Max - m_Min;
				m_d    = (5.0/m_agc);
				m_agc  = (m_agca + m_d) * 0.5;
				m_agca = m_d;
				m_Max  = 1.0;
				m_Min  = -1.0;
			}
			m_d = d;
			d *= m_agc;

			m_out = loopLPF.Do(m_err);
			if( m_out > 1.5 ){
				m_out = 1.5;
			}
			else if( m_out < -1.5 ){
				m_out = -1.5;
			}
			m_vcoout = vco.Do(m_out);
		// ˆÊ‘Š”äŠr
			m_err = m_vcoout * d;
			return outLPF.Do(m_out) * m_outgain;
		}
	}
}
