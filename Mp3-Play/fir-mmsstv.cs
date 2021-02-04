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

		double[] hp = new double[CSSTVDEM.TAPMAX+1];		/* 係数配列 : Coefficient array */

		public FIR( int tap, FirFilt type, double fs, double fcl, double fch, double att, double gain)
		{
			typ = type;
			n   = tap;
			this.fs   = fs;
			this.fcl  = fcl;
			this.fch  = fch;
			this.att  = att;
			this.gain = gain;

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
        /// Not clear if that's a safe assumpyion yet.</remarks>
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

	class CIIRTANK {
		double	z1, z2;

		public double	a0;
		public double	b1, b2;

		/// <summary>
		/// Sample frequency comes from the global value.
		/// </summary>
		public CIIRTANK( double p_dbSampFreq )
		{
			b1 = b2 = a0 = z1 = z2 = 0;
			SetFreq(2000.0, p_dbSampFreq, 50.0);
		}

		public void SetFreq(double f, double smp, double bw)
		{
			double lb1, lb2, la0;
			lb1 = 2 * Math.Exp(-Math.PI * bw/smp) * Math.Cos(2 * Math.PI * f / smp);
			lb2 = -Math.Exp(-2*Math.PI*bw/smp);

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
			if( Math.Abs(d) < 1e-37 ) d = 0.0;
			z1 = d;
			return d;
		}
	}

	class CIIR {
		protected static int IIRMAX = 16;

		double [] Z;

		public double	[] A;
		public double	[] B;
		public int		m_order;
		public int		m_bc;
		public double	m_rp;

		public CIIR() {
			m_order = 0;
			A = new double[IIRMAX*3];
			B = new double[IIRMAX*2];
			Z = new double[IIRMAX*2];
		}

		void Clear()
		{
			Array.Clear( Z, 0, Z.Length );
		}

		void MakeIIR(double fc, double fs, int order, int bc, double rp)
		{
			m_order = order;
			m_bc = bc;
			m_rp = rp;
			MakeIIR(A, B, fc, fs, order, bc, rp);
		}

		// bc : 0-ƒoƒ^[ƒ[ƒX, 1-ƒ`ƒFƒrƒVƒt
		// rp : ’Ê‰ßˆæ‚ÌƒŠƒbƒvƒ‹
		public static void MakeIIR(double []A, double[]B, double fc, double fs, int order, int bc, double rp)
		{
			double	w0, wa, u, zt, x;
			int		j, n;

			if( bc ){		// ƒ`ƒFƒrƒVƒt
				u = 1.0/(double)((order) * Math.Asinh(1.0/Math.Sqrt(Math.Pow(10.0,0.1*rp)-1.0)));
			}
			wa = Math.Tan(Math.PI*fc/fs);
			w0 = 1.0;
			n = (order & 1) + 1;
			double *pA = A;
			double *pB = B;
			double d1, d2;
			for( j = 1; j <= order/2; j++, pA+=3, pB+=2 ){
				if( bc ){	// ƒ`ƒFƒrƒVƒt
					d1 = sinh(u)*cos(n*PI/(2*order));
					d2 = cosh(u)*sin(n*PI/(2*order));
					w0 = sqrt(d1 * d1 + d2 * d2);
					zt = sinh(u)*cos(n*PI/(2*order))/w0;
				}
				else {		// ƒoƒ^[ƒ[ƒX
					w0 = 1.0;
					zt = cos(n*PI/(2*order));
				}
				pA[0] = 1 + wa*w0*2*zt + wa*w0*wa*w0;
				pA[1] = -2 * (wa*w0*wa*w0 - 1)/pA[0];
				pA[2] = -(1.0 - wa*w0*2*zt + wa*w0*wa*w0)/pA[0];
				pB[0] = wa*w0*wa*w0 / pA[0];
				pB[1] = 2*pB[0];
				n += 2;
			}
			if( bc && !(order & 1) ){
				x = pow( 1.0/pow(10.0,rp/20.0), 1/double(order/2) );
				pB = B;
				for( j = 1; j <= order/2; j++, pB+=2 ){
					pB[0] *= x;
					pB[1] *= x;
				}
			}
			if( order & 1 ){
				if( bc ) w0 = sinh(u);
				j = (order / 2);
				pA = A + (j*3);
				pB = B + (j*2);
				pA[0] = 1 + wa*w0;
				pA[1] = -(wa*w0 - 1)/pA[0];
				pB[0] = wa*w0/pA[0];
				pB[1] = pB[0];
			}
		}

		double Do(double d)
		{
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
}
