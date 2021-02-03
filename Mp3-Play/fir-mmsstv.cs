using System;
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

        void Create(int tap) {
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


        double Do( double[] hp, ref double d, out double j ) {
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

		static void MakeFilter( double[]HP, int tap, FirFilt type, 
			                    double fs,  double fcl, double fch, 
								double att, double gain )
		{
			FIR	fir = new FIR( tap, type, fs, fcl, fch, att, gain );

			fir.MakeFilter( HP );
		}
    } // End class
}
