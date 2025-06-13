using System;

using Play.Sound;


namespace Play.SSTV {
	public interface ISstvAdjust {
		public void SetSampFreq( double dblSampFreq );
		public double SampFreq { get; }
		public double TW { get; }
		public int    WD { get; }

	}

	/// <summary>
	/// Class to use to test the slant correction.
	/// </summary>
	public class FrequencySetting : ISstvAdjust {
		double m_SampFreq;

		readonly int    m_iOffset;
		readonly int    m_iTuning;
		readonly double m_dblOrigTW;

		public double TW { get; protected set; } // Width of scan line in ms.
		public int    WD { get; protected set; } // int version of TW.

		FrequencySetting( int iOffset, int iTuning ) {
			m_iOffset = iOffset;
			m_iTuning = iTuning;

			SetSampFreq( 11025 );
			m_dblOrigTW = (int)TW;
		}

		/// <summary>
		/// So in the original code you can update the sample frequency,
		/// and that of course will affect the width of a scan line (in samples).
		/// So we need a function to update both.
		/// </summary>
		/// <param name="dblSampFreq"></param>
		public void SetSampFreq( double dblSampFreq ) {
			const double dblTestWidth = 490;

			m_SampFreq = dblSampFreq;
			TW         = m_SampFreq / 1000 * dblTestWidth;
			WD         = (int)Math.Round( TW );

			//std::cout << "freq: " << m_SampFreq << " wd" << m_TW << "\n";
		}

		/// <summary>
		/// This is our fake width. used to generate data. You would like
		/// the syncronized TD to be close to this value after correction.
		/// It gets called once at the start based on the start settings.
		/// </summary>
		public int TestWD() {
			return (int)Math.Round( m_dblOrigTW + m_iTuning );
		}

		public int TestOffset() {
			return m_iOffset;
		}

		public double SampFreq { get { return m_SampFreq; } }

		public bool CompareResults( int iOffset ) {
			if( TestWD() - WD > 2 )
				return false;

			//if (iOffset != m_iOffset)
			//	return false;

			return true;
		}
	}

	public class SSTVSlant {
		readonly ISstvAdjust _oSetting;
		readonly SSTVDEM     _dp;

		int m_wStgLine;
		int[] _bp;

		public SSTVSlant( SSTVDEM dp, ISstvAdjust oSettings )  {
			_dp       = dp        ?? throw new ArgumentNullException( nameof( dp ) );
			_oSetting = oSettings ?? throw new ArgumentNullException( nameof( oSettings ) );

			m_wStgLine = _dp.m_wBase / _oSetting.WD;
			m_wStgLine -= 1;

			if( m_wStgLine < 0 )
				m_wStgLine = 0;

			int wd = (int)(_oSetting.TW * 1.5);
			_bp = new int[wd];
		}

		/// <summary>
		/// This code is very much like a offset correction code elsewhere in mmsstv. But
		/// this one doesn't look like it's always spot on for offset correction. I'm
		/// still looking into that.
		/// </summary>
		/// <returns>Base position for use by the CorrectSlant algorithm.</returns>
		protected int BasePosition() {
			int wd = (int)_oSetting.TW;

			if( _bp.Length < wd ) {
				_bp = new int[(int)(wd * 1.5)];
			}

			Array.Clear( _bp );

			// 基準位置を探す, Find a reference position

			int n = 0;
			for (int i = 0; i < m_wStgLine && (i < 32); i++) {
				for (int j = 0; j < _oSetting.WD; j++) {
					_bp[n] += _dp.SyncGet(i * _oSetting.WD + j);
					n++;
					if (n >= wd) 
						n = 0;
				}
			}

			int bpos = 0;
			int max  = 0;
			for (int i = 0; i < wd; i++) {
				if (max < _bp[i]) {
					max = _bp[i];
					bpos = i;
				}
			}

			return bpos;
		}

		/// <summary>
		/// This class encapsulates some behavior that was inside of the main CorrectSlant
		/// loop. By having this class I can get rid of an annoying GOTO exiting the loop
		/// right in the middle!
		/// </summary>
		class NX {
			protected double _T, _L, _TT, _TL;

			protected int _m;
			public int N { get; protected set; }

			public void Check( double ps, int y ) {
				if (N >= 2) {
					_T  += y;
					_L  += ps;
					_TT += y * y;
					_TL += y * ps;
					_m++;
				}
				N++;
			}

			public bool Done( ISstvAdjust oSetting ) {
				double fq = oSetting.SampFreq;

				if( _m >= 6 ) {
					fq = oSetting.SampFreq + (K0() * oSetting.SampFreq / oSetting.TW);

					//char bf[64];
					//sprintf(bf, "%lf, %lf", fq, k0);
					//EditNote->Text = bf;

					fq = NormalSampFreq(fq, 100);
				}

				bool fDone = (fq - oSetting.SampFreq) < (0.1 / 11025.0 * oSetting.SampFreq );

				oSetting.SetSampFreq(fq);

				return fDone;
			}

			public NX() {
				N   = 0;
				_m  = 0;
				_T  = 0;
				_L  = 0;
				_TT = 0;
				_TL = 0;
			}

			protected double K0() {
				return (_m * _TL - _L * _T) / (_m * _TT - _T * _T);
			}

			public static double NormalSampFreq(double d, double m)	{
				d = (double)((int)((d * m) + 0.5) / m);

				return d;
			}
		}

		/// <summary>
		/// This is my spiffy improved implementation of the slant corrector.
		/// No more goto's. No more pointer arithmetic.
		/// </summary>
		/// <returns></returns>
		protected bool CorrectInner( ref double LW, double bpos ) {
			// 傾き調整, Tilt adjustment
			int    iBase = 0;
			int    y     = 0;
			int    max   = 0;
			int    min   = 16384;
			double ps    = 0;
			NX     nx    = new NX();

			for( int i = 0; i < m_wStgLine; i++) {

				for( int j = 0; j < _oSetting.WD; j++, iBase++) {
					int   sp_idx = i * _oSetting.WD + j;
					// BUG!! SyncGet is going out of bounds from time to time!!
					//       so check the bounds first and simply bail of problem.
					if( _dp.BoundsCompare( sp_idx ) != 0 )
						break;
					short sp = _dp.SyncRaw( sp_idx );
					int   yy = (int)( iBase / _oSetting.TW );

					if (yy != y) {
						if (bpos < 0) {
							if (ps >= (_oSetting.TW / 4)) {     // 左方向への周りこみ, Around to the left
								ps -= _oSetting.TW;
							}
							else if (ps >= (_oSetting.TW / 8)) {
								LW *= 0.5;
								return nx.Done(_oSetting);
							}
						}
						else if (bpos >= _oSetting.TW) {        // 右方向への周りこみ, Around to the right
							if (ps < (_oSetting.TW * 3 / 4)) {
								ps += _oSetting.TW;
							}
							else if (ps < (_oSetting.TW * 7 / 8)) {
								LW *= 0.5;
								return nx.Done(_oSetting);
							}
						}
						else if (bpos >= (_oSetting.TW * 3 / 4)) {  // 右側, Right side
							if (ps < _oSetting.TW / 4 ) {
								ps += _oSetting.TW;
							}
						}
						else if (bpos <= (_oSetting.TW / 4)) {      // 左側, left side.
							if (ps >= (_oSetting.TW * 3 / 4)) {
								ps -= _oSetting.TW;
							}
						}
						// Note: 4800 is sense level 2, (0,1,2)...
						if ((y >= 0) && ((max - min) >= 4800) && (Math.Abs(ps - bpos) <= LW)) {
							bpos = ps;
							nx.Check(ps, y);
							if (nx.N >= m_wStgLine) {
								LW *= 0.5;
								return nx.Done(_oSetting);
							}
						}
						y   = yy;
						max = 0;
						min = 16384;
						ps  = 0;
					}
					if (max < sp) {
						max = sp;
						ps  = iBase % _oSetting.TW;
					}
					if (min > sp) {
						min = sp;
					}
				}
			}

			return nx.Done(_oSetting);
		}

		/// <summary>
		/// Ok I know a little more about this bugger. bpos is sync signal "base position" as you
		/// might expect. It assumes the signal starts at time zero. So even if you're not
		/// giving it a signal, it will adjust the base as if there was signal back
		/// at the beginning of time (zero in the buffer).  
		/// Now the TW (scan line width in samples) is co dependent on the Sample Frequency. 
		/// This algoritm is attempting to adjust both the TW and the SampFreq to be syncronized. 
		/// This is why SetSampFreq() has to be called so that TW gets updated as it guesses the frequency.
		/// NOTE: The base position is turning out slightly off. I think this is because unlike MMSSTV I'm not
		/// reseting my filters and AFC data structures in the demodulator like MMSSTV does. So I'm slightly off.
		/// I'll look into that more in the future.
		/// </summary>
		/// <seealso cref="BasePosition"/>
		public int CorrectSlant() {
			int    bpos = 0;
			double LW   = _oSetting.TW * 0.1; // 10% の 揺れを許容, Allows shaking

			for( int z = 0; z < 5; z++) {
				bpos = BasePosition();

				if( CorrectInner( ref LW, bpos ) )
					break;
			}

			return bpos;
		}
	}
}
