using System;

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
	public class CHILL : IFrequencyConverter 
	{
		public static int HILLTAP = 48;

		readonly double[]  Z = new double[HILLTAP+1];
		readonly double[]  H = new double[HILLTAP+1];
		readonly double[]  m_A = new double[4];

		double	  m_OFF;
		double	  m_OUT;
	    public int m_htap { get; protected set; }
		int		  m_df;
		int		  m_tap;
		readonly CIIR m_iir = new CIIR();
		public void Clear() { }
		double SampFreq { get; }
		double SampBase { get; }
		double ToneOffs { get; }
		public double OffsetCorrect( double dblSampFreq ) {
			double dblHill  = m_htap / 4.0;
            double dblMagic = dblHill* dblSampFreq / 1000;

			return dblMagic + 10;
		}

		public CHILL(double dbSampFreq, double dbSampBase, double dbToneOffset, FrequencyLookup rgFreqTable )
		{
			SampFreq = dbSampFreq;
			SampBase = dbSampBase;
			ToneOffs = dbToneOffset;

			SetWidth( rgFreqTable );

		    m_htap = m_tap / 2;
			MakeHilbert(H, m_tap, SampFreq, 100, SampFreq/2 - 100);
			m_A[0] = m_A[1] = m_A[2] = m_A[3] = 0;

			m_iir.MakeIIR(1800, SampFreq, 3, CIIR.FilterType.Butterworth, 0);
		}

		public void SetWidth( FrequencyLookup rgFreqTable )
		{
			if( rgFreqTable == null )
				throw new ArgumentNullException( nameof( rgFreqTable ) );

			m_OFF = 2 * Math.PI * (rgFreqTable.CENTER + ToneOffs) / SampFreq;
			m_OUT = 32768.0 * SampFreq / (2 * Math.PI * rgFreqTable.BW);

			if( SampBase >= 40000 ){
				m_OFF *= 4;
				m_OUT *= 0.25;
				m_tap = 48; //48
				m_df  = 2;
			} else if( SampBase >= 16000 ){
				m_OFF *= 2;
				m_OUT *= 0.5;
				m_tap = 24; //24
				m_df  = 1;
			} else {
				m_tap = 12; //12
				m_df = 0;
			}
			if( rgFreqTable.CQ100 ){
				m_tap *= 3;
			}
		}

		//---------------------------------------------------------------------------
		// ＦＩＲフィルタ（ヒルベルト変換フィルタ）の設計 : (Hilbert transform filter) design
		//
		static void MakeHilbert(double [] H, int N, double fs, double fc1, double fc2)
		{
			int    L  = N / 2;
			double T  = 1.0 / fs;
			double W1 = 2 * Math.PI * fc1;
			double W2 = 2 * Math.PI * fc2;

			double w, x1, x2;
			for( int n = 0; n <= N; n++ ) {
				if( n == L ){
					x1 = x2 = 0.0;
				} else if( (n - L) != 0 ){
					x1 = (n - L) * W1 * T;
					x1 = Math.Cos(x1) / x1;
					x2 = (n - L) * W2 * T;
					x2 = Math.Cos(x2) / x2;
				} else {
					x1 = x2 = 1.0;
				}
				w = 0.54 - 0.46 * Math.Cos(2*Math.PI*n/(N));
				H[n] = -(2 * fc2 * T * x2 - 2 * fc1 * T * x1) * w;
			}

			if( N < 8 ) {
				w = 0;
    			for( int n = 0; n <= N; n++ ){
					w += Math.Abs(H[n]);
    			}
				if( w != 0 ){
					w = 1.0 / w;
					for( int n = 0; n <= N; n++ ){
						H[n] *= w;
					}
    			}
			}
		}

		//-------------------------------------------------
		// ＦＩＲフィルタのたたき込み演算 : FIR filter tapping operation
		static double DoFIR(double []hp, double []zp, double d, int tap)
		{
			Array.Copy( zp, 1, zp, 0, tap ); 
			zp[tap] = d;
			d = 0.0;
			for( int i = 0; i <= tap; i++ ){
				d += zp[i] * hp[i];
			}
			return d;
		}

		public double Do(double d)
		{
			d = DoFIR(H, Z, d, m_tap);

			double a = Z[m_htap]; 
			if( a != 0 )
				a = Math.Atan2(d, a);

			d = a - m_A[0];
			switch(m_df){
				case 1:
					m_A[0] = m_A[1];
					m_A[1] = a;
					break;
				case 2:
					m_A[0] = m_A[1];
					m_A[1] = m_A[2];
					m_A[2] = m_A[3];
					m_A[3] = a;
					break;
				default:
					m_A[0] = a;
					break;
			}
			if( d >= Math.PI ) {
				d -= Math.PI*2;
			}
			else if( d <= -Math.PI ){
				d += Math.PI*2;
			}
			d += m_OFF;
			return m_iir.Do(d * m_OUT);
		}
	}

	class CFQC : IFrequencyConverter {
		protected static double ZEROFQ	= (-1900.0/400.0);

		int     m_Count;
		double  m_ACount;
		double  m_d;
		double  m_fq;
		double  m_out;
		double  m_SampFreq;

		double	m_CenterFQ;
		double	m_HighFQ;
		double	m_LowFQ;

		double	m_HighVal;
		double	m_LowVal;
		double	m_BWH;

		int     m_Type;
		bool    m_Limit;
		CIIR    m_iir;

		double  m_SmoozFq;
		CSmooz  m_fir;

		int     m_outOrder;
		double  m_outFC;

		int		m_Timer;
		int		m_STimer;

		double SampFreq { get; }
		double ToneOffs { get; }
		public double OffsetCorrect( double dblAdjustedFrequency ) { return 0; }

		public CFQC( double dbSampFreq, double dbToneOffs, FrequencyLookup rgFreqTable )
		{
			SampFreq     = dbSampFreq;
			ToneOffs     = dbToneOffs;

			m_Type = 0;

			m_iir = new CIIR();
			m_fir = new CSmooz();

			m_Limit    = true;
			m_d        = 0;
			m_Count    = 0;
			m_ACount   = 0;
			m_fq       = ZEROFQ;
			m_d        = 0;
			m_out      = 0;
			m_outOrder = 3;
			m_outFC    = 900;
			m_SmoozFq  = 2200;
			m_SampFreq = SampFreq;
			m_Timer    = m_STimer = (int)SampFreq; // int(m_SampFreq);

			SetWidth( rgFreqTable );
			CalcLPF ();
		}

		public void SetWidth( FrequencyLookup rgFreqTable )
		{
			if( rgFreqTable == null ) 
				throw new ArgumentNullException( nameof( rgFreqTable ) );

			m_BWH      = rgFreqTable.BWHalf;
			m_CenterFQ = rgFreqTable.CENTER + ToneOffs;
			m_HighFQ   = 2400.0 + ToneOffs;
			m_LowFQ    = rgFreqTable.AFCLOW + ToneOffs;

			m_HighVal  = (m_HighFQ - m_CenterFQ) / m_BWH;
			m_LowVal   = (m_LowFQ  - m_CenterFQ) / m_BWH;
		}

		public void Clear()
		{
			m_d      = 0;
			m_Count  = 0;
			m_ACount = 0;
			m_fq     = ZEROFQ;
			m_d      = 0;
			m_out    = 0;
			m_Timer  = m_STimer;
		}

		void SetSampFreq(double fq)
		{
			m_SampFreq = fq;
			CalcLPF();
			m_STimer = (int)SampFreq;
		}

		void CalcLPF()
		{
			m_iir.MakeIIR(m_outFC, m_SampFreq, m_outOrder, CIIR.FilterType.Butterworth, 0);
			if( m_SmoozFq < 500 ) 
				m_SmoozFq = 500.0;
			m_fir.SetCount( (int)(SampFreq/m_SmoozFq) );
		}

		public double Do(double d)
		{
			double count;
			double offset;

			if( d >= 0 ){
				if( m_d < 0 ){
					count = m_Count - m_ACount;
					offset = d/(d - m_d);
					m_ACount = m_Count - offset;
					count -= offset;
					if( count >= 1.0 ){
						m_fq = m_SampFreq * 0.5 / count;
						if( m_Limit ){
							if( m_fq > m_HighFQ ){
								m_fq = m_HighVal;
							} else if( m_fq < m_LowFQ ){
								m_fq = m_LowVal;
							} else {
								m_fq -= m_CenterFQ;
								m_fq /= m_BWH;
							}
						} else {
							m_fq -= m_CenterFQ;
							m_fq /= m_BWH;
							m_Timer = m_STimer;
						}
					}
				}
			} else {
				if( m_d >= 0 ){
					count = m_Count - m_ACount;
					offset = d/(d - m_d);
					m_ACount = m_Count - offset;
					count -= offset;
					if( count >= 1.0 ){
						m_fq = m_SampFreq * 0.5 / count;
						if( m_Limit ){
							if( m_fq > m_HighFQ ){
								m_fq = m_HighVal;
							} else if( m_fq < m_LowFQ ){
								m_fq = m_LowVal;
							} else {
								m_fq -= m_CenterFQ;
								m_fq /= m_BWH;
							}
						} else {
							m_fq -= m_CenterFQ;
							m_fq /= m_BWH;
							m_Timer = m_STimer;
						}
					}
				}
			}
			if( !m_Limit && m_Timer > 0 ){
				m_Timer--;
				if( m_Timer > 0 )
					m_fq = -m_CenterFQ/m_BWH;
			}
			switch(m_Type){
				case 0:     // IIR
					m_out = m_iir.Do(m_fq);
					break;
				case 1:     // FIR
					m_out = m_fir.Avg(m_fq);
					break;
				default:    // OFF
					m_out = m_fq;
					break;
			}
			m_d = d;
			m_Count++;
			return -(m_out * 16384);
		}
	}


	/// <remarks>
	/// TODO: This can probably be moved to the SSTV-Rx.cs file. Look at that later.
	/// </remarks>
	class CPLL : IFrequencyConverter {
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
		public void   Clear() { }
		public double OffsetCorrect( double dblAdjustedFrequency ) { return 0; }
		public double DoWarmUp( double s ) { return Do( s ); }

		/// <summary>
		/// If Sample frequency or ToneOffset changes we'll need to re-create this object
		/// </summary>
		public CPLL( double dbSampFreq, double dbToneOffset, FrequencyLookup rgFreqTable )
		{
			vco = new CVCO( dbSampFreq, dbToneOffset );

			m_err        = 0;
			m_out        = 0;
			m_vcoout     = 0;
			m_vcogain    = 1.0;
			m_outgain    = 32768.0 * m_vcogain;
			m_SampleFreq = dbSampFreq;
			m_ToneOffset = dbToneOffset;

			SetWidth     ( rgFreqTable );
			SetSampleFreq(m_SampleFreq);

			m_Max  = 1.0;
			m_Min  = -1.0;
			m_d    = 0;
			m_agc  = 1.0;
			m_agca = 0.0;
		}

		public void SetWidth( FrequencyLookup rgFrequency )
		{
			if( rgFrequency == null )
				throw new ArgumentNullException( nameof( rgFrequency ) );

			m_Shift    = rgFrequency.BW;
			m_FreeFreq = rgFrequency.CENTER;
			SetFreeFreq( rgFrequency.LOW, rgFrequency.HIGH);

			SetVcoGain(m_vcogain);
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

			loopLPF.MakeIIR(m_loopFC, m_SampleFreq, m_loopOrder, CIIR.FilterType.Butterworth, 0);
		}

		public void MakeOutLPF( int iLoopOrder, int iLoopFreq )
		{
			m_outOrder = iLoopOrder;
			m_outFC    = iLoopFreq;

			outLPF.MakeIIR(m_outFC, m_SampleFreq, m_outOrder, CIIR.FilterType.Butterworth, 0);
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

	class PhaseCombo : IFrequencyConverter {
		CPLL _pll;
		CFQC _fqc;
		public PhaseCombo( CPLL pll, CFQC fqc ) {
			_pll = pll ?? throw new ArgumentNullException( nameof( pll ) );
			_fqc = fqc ?? throw new ArgumentNullException( nameof( fqc ) );
		}

		public void Clear() {
			_fqc.Clear();
		}

		public double Do(double s) {
			return _pll.Do( _fqc.Do( s ) );
		}

		public double OffsetCorrect(double dblAdjustedFrequency) {
			return 0;
		}

		public void SetWidth(FrequencyLookup look) {
			_fqc.SetWidth( look );
			_pll.SetWidth( look );
		}
	}
}