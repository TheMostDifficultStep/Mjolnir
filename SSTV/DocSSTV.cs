using System;
using System.IO;
using System.Collections.Generic;
using System.Text;

using SkiaSharp;

using Play.Interfaces.Embedding;
using Play.Sound;
using Play.Sound.FFT;
using Play.Edit;
using Play.ImageViewer;

namespace Play.SSTV {
    public enum ESstvProperty {
        ALL,
        UploadTime,
        SSTVMode,
        FFT,
        TXImage
    }

    public delegate void SSTVPropertyChange( ESstvProperty eProp );

	/// <summary>
	/// It's a little bit of a misonmer that the demodulator doesn't include these bits to get the frequencey
	/// demodulated stuff encoded into a bitmap. But at least we have some factorization of the problem.
	/// </summary>
    public class TmmSSTV {
        protected readonly CSSTVDEM dp;
        protected readonly CSSTVSET SSTVSET;

#region variables
	    protected int m_RXW = 320, m_RXH = 256, m_RXPH = 256; // RXPH is the size NOT including greyscale. Not used yet, but maybe later.

	    protected double[]  m_Z = new double[3];
        protected int       m_AX, m_AY;

	    protected short[]  m_Y36 = new short[800];
	    protected short[,] m_D36 = new short[2,800];
        protected int      m_DSEL;

	    protected int     m_SyncPos, m_SyncRPos; // RPos Gets set in AutoStopJob() which we haven't implemented yet 
	    protected int     m_SyncMax, m_SyncMin;

	    protected int     m_SyncAccuracy = 1;
	    protected int     m_SyncAccuracyN;  // TODO: Gets used in TimerTimer, which isn't implented yet.

        //Auto
	    protected int     m_Mult;

	    protected int     m_AutoStopPos;
	    protected int[]   m_AutoStopAPos = new int[16];
	    protected int     m_AutoStopCnt;
	    protected int     m_AutoStopACnt;

	    protected int     m_AutoSyncCount;
	    protected int     m_AutoSyncPos;
	    protected int     m_AutoSyncDis;
	    protected int     m_AutoSyncDiff;

        //AutoSlant
	    int       m_ASBgnPos;
	    int       m_ASCurY;
	    int       m_ASDis;
	    int       m_ASBitMask;
	    double[]  m_ASLmt = new double[7];
	    int[]     m_ASPos = new int[4];
	    CSmooz    m_ASAvg = new CSmooz();

		short[] pCalibration = null; // Not strictly necessary yet.

		public SKBitmap pBitmapRX { get; protected set; } = new SKBitmap();
		public SKBitmap pBitmapD12 { get; } = new SKBitmap( 800, 600, SKColorType.Rgb888x, SKAlphaType.Unknown );
		// Looks like were only using grey scale on the D12. Look into turning into greyscale later.
#endregion

		protected readonly List<ColorChannel> _rgSlots = new List<ColorChannel>(5);

		public TmmSSTV( CSSTVDEM p_dp ) {
			dp      = p_dp         ?? throw new ArgumentNullException( "CSSTVDEM" );
			SSTVSET = p_dp.SSTVSET ?? throw new ArgumentNullException( "CSSTVSET" );

		  //StartOption() ...
		  //dp.sys.m_bCQ100 = FALSE;
		  //g_dblToneOffset = 0.0;
		}

		/// <summary>
		/// Convert ip levels -16,384 (black) through 16,384 (white) to -128 to +128.
		/// </summary>
		/// <param name="ip">frequency as a level value.</param>
		/// <returns></returns>
		protected int GetPixelLevel(short ip)
		{
			if( dp.sys.m_DemCalibration && (pCalibration != null) ){
				int d = (ip / 8) + 2048;
				if( d < 0 )
					d = 0;
				if( d > 4096 )
					d = 4096;
				return pCalibration[d];
			} else {
				double d = ip - dp.sys.m_DemOff;
				d *= ( d >= 0 ) ? dp.sys.m_DemWhite : dp.sys.m_DemBlack;
				return (int)d;
			}
		}

		protected int GetPictureLevel( short ip, int i )
		{
			if( dp.sys.m_UseRxBuff != 2 ) {
				int   d;
				short ip2 = dp.m_Buf[dp.m_rPage * dp.m_BWidth + i + SSTVSET.m_KSB ];

				// I'm going to guess we're trying to get past some sync signal.
				if( ip < ip2 ){
					d = GetPixelLevel(ip2);
				} else {
					d = GetPixelLevel(ip);
				}
				return d;
			}

			return GetPixelLevel(ip);
		}

		static void YCtoRGB( out int R, out int G, out int B, int Y, int RY, int BY)
		{
			Y -= 16;
			R = (int)(1.164457*Y + 1.596128*RY );
			G = (int)(1.164457*Y - 0.813022*RY - 0.391786*BY );
			B = (int)(1.164457*Y + 2.017364*BY );

			LimitRGB( ref R, ref G, ref B);
		}

		static void LimitRGB( ref int R, ref int G, ref int B)
		{
			R = Limit256(R);
			G = Limit256(G);
			B = Limit256(B);
		}

		protected static int Limit256(int d)
		{
			if( d < 0 )
				d = 0;
			if( d > 255 )
				d = 255;

			return d;
		}

        void InitAutoStop( double dbSampBase )
        {
	        //memset(m_AutoStopAPos, 0, sizeof(m_AutoStopAPos));
            Array.Clear( m_AutoStopAPos, 0, m_AutoStopAPos.Length );
	        m_AutoStopCnt  = 0;
	        m_AutoStopACnt = 0;
	        m_AutoStopPos  = 0;
	        m_ASBgnPos     = 0x7fffffff;
	        m_ASDis        = 0;
	        m_ASBitMask    = 0;
	        m_ASAvg.SetCount(16);

	        m_Z[0] = m_Z[1] = m_Z[2] = 0;

	        m_ASPos[0] = 64;
	        m_ASPos[1] = 128;
	        m_ASPos[2] = 160;
	        m_ASPos[3] = (SSTVSET.m_L - 36);

	        switch(SSTVSET.m_Mode){
		        case AllModes.smPD50:
		        case AllModes.smPD90:            // Max 128
		        case AllModes.smMP73:
		        case AllModes.smMP115:
		        case AllModes.smMP140:
		        case AllModes.smMP175:
		        case AllModes.smR24:
		        case AllModes.smRM8:
		        case AllModes.smRM12:
                case AllModes.smMN73:
                case AllModes.smMN110:
                case AllModes.smMN140:
			        m_ASPos[0] = 48;
			        m_ASPos[1] = 64;
			        m_ASPos[2] = 72;
			        m_ASPos[3] = 110;
			        break;
		        case AllModes.smPD160:           // Max 200
			        m_ASPos[0] = 48;
			        m_ASPos[1] = 80;
			        m_ASPos[2] = 126;
			        m_ASPos[3] = 160;
			        break;
		        case AllModes.smPD290:           // Max 308 Limit 288
			        m_ASPos[3] = 240;
			        break;
		        case AllModes.smP3:              // Max496
			        m_ASPos[1] = 200;
			        m_ASPos[2] = 360;
			        m_ASPos[3] = 496-48;
			        break;
		        case AllModes.smP5:              // Max496 Limit 439
			        m_ASPos[1] = 200;
			        m_ASPos[2] = 300;
			        m_ASPos[3] = 380;
			        break;
		        case AllModes.smP7:              // Max496 Limit 330
			        m_ASPos[1] = 128;
			        m_ASPos[2] = 220;
			        m_ASPos[3] = 280;
			        break;
	        }
	        m_AutoSyncPos  = 0x7fffffff;
	        m_Mult         = (int)(SSTVSET.m_TW / 320.0);
	        m_AutoSyncDiff = m_Mult * 3;

            int iSomeMagic = (int)(45 * dbSampBase / 11025);
	        if( m_AutoSyncDiff > iSomeMagic )
                m_AutoSyncDiff = iSomeMagic;
        }

		/// <summary>
		/// This is a whole hot mess of stuff and I'm not going to port it for now.
		/// </summary>
		/// <returns></returns>
		protected bool AutoStopJob() {
			return true;
		}

        /// <summary>
        /// Bunch of stuff to do when we stop, but I don't think it's necessary to
        /// sort it all right now.
        /// </summary>
        void AllStop() {
			//bool lost = dp.m_Lost;
			//if( dp.m_LoopBack != true ){
			//	WriteHistory(1);
			//} else {
			//	SBWHist->Enabled = TRUE;
			//}
			//TrackTxMode(0);
			//UpdateModeBtn();
			//if( lost )
            //    InfoRxLost();
			//UpdateUI();
         }

        void PrepViewers() {
			//if( KRSW->Checked ){
			//	if( pRxView != NULL ){
			//		if( pRxView->Handle != ::GetForegroundWindow() ){
			//			::SetForegroundWindow(pRxView->Handle);
			//		}
			//	}
			//	else if( m_MainPage != pgRX ){
			//		AdjustPage(pgRX);
			//	}
			//}

			//if( pRxView != NULL ){
			//	pRxView->UpdateTitle(SSTVSET.m_Mode, false );
			//}
			//if( pComm != NULL )
            //    pComm->SetScan(dp.m_Sync);

            // DispSyncStat();
        }

        void PrepDraw() {
			m_SyncRPos      = m_SyncPos = -1;
			m_SyncAccuracyN = 0;
			m_DSEL			= 0;
			m_AX			= -1;
			m_AY			= -5;

			//if( dp.m_ReqSave ){
			//	WriteHistory(1);
			//}
			SSTVSET.GetPictureSize( out m_RXW, out m_RXH, out m_RXPH, SSTVSET.m_Mode);
			if( pBitmapRX.Width != m_RXW ){
				pBitmapRX.Dispose();
				pBitmapRX = new SKBitmap( m_RXW, m_RXH, SKColorType.Rgb888x, SKAlphaType.Opaque );
				//PBoxRX->Invalidate();
			}
			//UpdateModeBtn();
			//::GetUTC(&m_StartTime);

            // TSpeedButton SBTX, looks like TX send button, down to send. 
            // but we want to be RX mode. See: TMmsstv::SBTXClick() && TMmsstv::ToRX()
            // Might be loop back shows what we're sending!! Maybe!
            dp.PrepDraw( /* SBTX->Down ? sys.m_echo : 0 */ false );
			//WaveStg.WInit();
			//RxHist.ClearAddFlag();
			//SBWHist->Enabled = FALSE;
			//KRH->Enabled = FALSE; // Copy to history.

            PrepViewers();
        }

		void ClearTVImages() {
			//if( KRD->Checked ){ // KRD: AutoClear
			//	ClearDraw(pBitmapRX, PBoxRX, sys.m_ColorRXB);
			//}
			//ClearDraw(pBitmapD12, PBoxD12, clWhite);
		}

		public void DrawSSTV()
		{
			// Go until the rPage catches up with the wPage.
			while( dp.m_Sync && (dp.m_wPage != dp.m_rPage) ){
				if( dp.m_wBgn != 0 ){
					if( dp.m_wBgn != 1 ){
                        PrepDraw();
					}
					dp.OnDrawBegin();
					//dp.SyncSSTV( m_SyncAccuracy ); // TODO: Double check this sync value.
					//if( dp.m_wBgn != 0 )
                    //    return;
					ClearTVImages();
					InitAutoStop( dp.SampBase );
					m_AutoSyncCount = m_AutoSyncDis = 0;
				}
				//int ip = dp.m_rPage * dp.m_BWidth;
                //dp.StorePage( ip );

				// DrawSSTVNormal();
				DrawSSTVNormal2();
				if( m_AY > SSTVSET.m_L ){
					if( dp.m_Sync ){
						dp.Stop();
                        AllStop();
					}
					break;
				}
                dp.RPageIncrement();
			}
		}

		protected void DrawSSTVNormal()
		{
			int R,G,B; int gp  = -1; int gp2 = -1; // Moving this int the loop causees black bitmap. hmmm....
			int n   = dp.m_rBase; // Increments in += SSTVSET.m_WD chunks.
			int bx  = -1;
			int ay  = -5;

			if( n < 0 ) 
				throw new ApplicationException( "m_rBase went negative" );

			int y = (int)(n/SSTVSET.m_TW); 
			if( ay != y ){
				m_AY = ay = y;
				if( (SSTVSET.m_Mode == AllModes.smSCT1)||
					(SSTVSET.m_Mode == AllModes.smSCT2)||
					(SSTVSET.m_Mode == AllModes.smSCTDX) ){
					if( (y > 0) && (y <= 256) ){
						gp = y-1;
					}
				}
				else if( ((SSTVSET.m_Mode >= AllModes.smPD50)&&(SSTVSET.m_Mode <= AllModes.smPD290)) ||
							((SSTVSET.m_Mode >= AllModes.smMP73)&&(SSTVSET.m_Mode <= AllModes.smMP175)) ||
							((SSTVSET.m_Mode >= AllModes.smMN73)&&(SSTVSET.m_Mode <= AllModes.smMN140)) ||
							((SSTVSET.m_Mode >= AllModes.smR24 )&&(SSTVSET.m_Mode <= AllModes.smRM12 ))
				){
					if( (y >= 0) && (y < SSTVSET.m_L) ){
						R   = y * 2;
						gp  = R;
						gp2 = R+1;
					}
				}
				else if( (y >= 0) && (y < pBitmapRX.Height) ){
					gp = y;
				}
			}

			// Looks like we read one scan line. n represents our position through the
			// entire stream. That is used to determine the "Y" coordinate at the start
			// then it is incremented to determine the "X" pos.
			for( int i = 0; i < SSTVSET.m_WD; i++, n++ ){
				short ip = dp.m_Buf[dp.m_rPage * dp.m_BWidth + i];
			 // Y calculation was here.

				double ps = n % (int)SSTVSET.m_TW; // fmod(double(n), SSTVSET.m_TW)

				short  sp = dp.m_B12[dp.m_rPage * dp.m_BWidth + i];
				if( (int)ps == 0 ){
					// KRSA, assigned sys.m_UseRxBuff ? TRUE : FALSE, see also GetPictureLevel()
					if( (dp.sys.m_AutoStop || dp.sys.m_AutoSync /* || KRSA->Checked */ ) && 
						dp.m_Sync && (m_SyncPos != -1) ){
						AutoStopJob();
					}
					m_SyncMin  = m_SyncMax = sp;
					m_SyncRPos = m_SyncPos;
				} else if( m_SyncMax < sp ){
					m_SyncMax  = sp;
					m_SyncPos  = (int)ps;
				} else if( m_SyncMin > sp ){
					m_SyncMin  = sp;
				}
				int d, x;
				x = (int)(ps * pBitmapD12.Width / SSTVSET.m_TW );
				if( (x != bx) && (x < pBitmapD12.Width) && (x >= 0) ){
					d = sp * 256 / 4096;
					d = Limit256(d);
					pBitmapD12.SetPixel( x, y, new SKColor( (byte)d ) );
					bx = x;
				}

				if( ps >= SSTVSET.m_OF ){
					ps -= SSTVSET.m_OF;
					switch(SSTVSET.m_Mode){
						case AllModes.smSCT1:
						case AllModes.smSCT2:
						case AllModes.smSCTDX:
							if( ps < SSTVSET.m_KS ){               // R : 9.0 hsync + 1.5 gap
								x = (int)(ps * pBitmapRX.Width / SSTVSET.m_KSS );
								if( (x != m_AX) && (x >= 0) && (x < 320) ){
									m_AX = x;
									if( SSTVSET.m_Mode == AllModes.smSCTDX ){
										d = GetPixelLevel  (ip);
									} else {
										d = GetPictureLevel(ip, i);
									}
									d += 128;
									d = Limit256(d);
									if( gp > -1 ){
										pBitmapRX.SetPixel( x, gp, new SKColor( (byte)d, (byte)m_D36[0,x], (byte)m_D36[1,x] ) );
									}
								}
							}
							else if( ps < SSTVSET.m_CG ){          // G
								ps -= SSTVSET.m_SG;
								x = (int)(ps * pBitmapRX.Width / SSTVSET.m_KSS);
								if( (x != m_AX) && (x >= 0) && (x < 320) ){
									m_AX = x;
									if( SSTVSET.m_Mode == AllModes.smSCTDX ){
										d = GetPixelLevel  (ip);
									}
									else {
										d = GetPictureLevel(ip, i);
									}
									d += 128;
									d = Limit256(d);
									m_D36[0,x] = (short)d;
								}
							}
							else if( ps < SSTVSET.m_CB ){          // B
								ps -= SSTVSET.m_SB;
								x = (int)(ps * pBitmapRX.Width / SSTVSET.m_KSS);
								if( (x != m_AX) && (x >= 0) && (x < 320) ){
									m_AX = x;
									if( SSTVSET.m_Mode == AllModes.smSCTDX ){
										d = GetPixelLevel  (ip);
									}
									else {
										d = GetPictureLevel(ip, i);
									}
									d += 128;
									d = Limit256(d);
									m_D36[1,x] = (short)d;
								}
							}
							break;
						case AllModes.smR36:
							if( ps < SSTVSET.m_KS ){               // ‹P“x
								x = (int)(ps * pBitmapRX.Width / SSTVSET.m_KSS);
								if( (m_AX != x) && (x >= 0) && (x < 320) ){
									m_AX = x;
									d = GetPictureLevel(ip, i);
									d += 128;
									d = Limit256(d);
									m_Y36[x] = (short)d;
								}
							}
							else if( ps < SSTVSET.m_CG ){          // TCS
								ps -= SSTVSET.m_SG;
								if( ps >= 0 ){
									d = GetPixelLevel(ip);
									if( (d >= 64) || (d < -64) ){
										m_DSEL = (d >= 0) ? 1 : 0;  // RY=1500 m_D36[0], BY=2300 m_D36[1]
									} else {
										m_DSEL = m_DSEL != 0 ? 0 : 1;
									}
								}
							}
							else if( ps < SSTVSET.m_CB ){          // F·
								ps -= SSTVSET.m_SB;
								x = (int)(ps * pBitmapRX.Width / SSTVSET.m_KS2S);
								if( (m_AX != x) && (x >= 0) && (x < 320) && (y < 240) ){
									m_AX = x;
									d = GetPixelLevel(ip);
									m_D36[m_DSEL,x] = (short)d;
									YCtoRGB( out R, out G, out B, m_Y36[x], m_D36[0,x], m_D36[1,x]);
									if( gp > -1 ){
										pBitmapRX.SetPixel( x, gp, new SKColor( (byte)R, (byte)G, (byte)B ) );
									}
								}
							}
							break;
						case AllModes.smR24:
						case AllModes.smR72:
						case AllModes.smMR73:
						case AllModes.smMR90:
						case AllModes.smMR115:
						case AllModes.smMR140:
						case AllModes.smMR175:
						case AllModes.smML180:
						case AllModes.smML240:
						case AllModes.smML280:
						case AllModes.smML320:
							if( ps < SSTVSET.m_KS ){               // 輝度 : Luminance, Y
								x = (int)(ps * pBitmapRX.Width / SSTVSET.m_KSS );
								if( (m_AX != x) && (x >= 0) && (x < pBitmapRX.Width) ){
									m_AX = x;
									d = GetPictureLevel(ip, i);
									d += 128;
									d = Limit256(d);
									m_Y36[x] = (short)d;
								}
							}
							else if( ps < SSTVSET.m_CG ){          // R-Y
								ps -= SSTVSET.m_SG;
								x = (int)(ps * pBitmapRX.Width / SSTVSET.m_KS2S );
								if( (m_AX != x) && (x >= 0) && (x < pBitmapRX.Width) ){
									m_AX = x;
									d = GetPixelLevel(ip);
									m_D36[1,x] = (short)d;
								}
							}
							else if( ps < SSTVSET.m_CB ){          // B-Y
								ps -= SSTVSET.m_SB;
								x = (int)(ps * pBitmapRX.Width / SSTVSET.m_KS2S);
								if( (m_AX != x) && (x >= 0) && (x < pBitmapRX.Width) && (y < SSTVSET.m_L) ){
									m_AX = x;
									d = GetPixelLevel(ip);
									YCtoRGB( out R, out G, out B, m_Y36[x], m_D36[1,x], d);
									if( gp > -1 ){
										pBitmapRX.SetPixel( x, gp, new SKColor( (byte)R, (byte)G, (byte)B ) );
										if( SSTVSET.m_Mode == AllModes.smR24 ){
											pBitmapRX.SetPixel( x, gp2, new SKColor( (byte)R, (byte)G, (byte)B ) );
										}
									}
								}
							}
							break;
						case AllModes.smPD50:
						case AllModes.smPD90:
						case AllModes.smPD120:
						case AllModes.smPD160:
						case AllModes.smPD180:
						case AllModes.smPD240:
						case AllModes.smPD290:
						case AllModes.smMP73:
						case AllModes.smMP115:
						case AllModes.smMP140:
						case AllModes.smMP175:
						case AllModes.smMN73:
						case AllModes.smMN110:
						case AllModes.smMN140:
							if( ps < SSTVSET.m_KS ){               // 輝度 : Luminance, Y
								x = (int)(ps * pBitmapRX.Width / SSTVSET.m_KSS);
								if( (m_AX != x) && (x >= 0) && (x < pBitmapRX.Width) ){
									m_AX = x;
									d = GetPictureLevel(ip, i);
									d += 128;
									d = Limit256(d);
									m_Y36[x] = (short)d;
								}
							}
							else if( ps < SSTVSET.m_CG ){          // R-Y
								ps -= SSTVSET.m_SG;
								x = (int)(ps * pBitmapRX.Width / SSTVSET.m_KSS);
								if( (m_AX != x) && (x >= 0) && (x < pBitmapRX.Width) ){
									m_AX = x;
									d = GetPixelLevel(ip);
									m_D36[1,x] = (short)d;
								}
							}
							else if( ps < SSTVSET.m_CB ){          // B-Y
								ps -= SSTVSET.m_SB;
								x = (int)(ps * pBitmapRX.Width / SSTVSET.m_KSS );
								if( (m_AX != x) && (x >= 0) && (x < pBitmapRX.Width) ){
									m_AX = x;
									d = GetPixelLevel(ip);
									m_D36[0,x] = (short)d;
								}
							}
							else if( ps < (SSTVSET.m_CB + SSTVSET.m_KS) ){          // Y(even)
								ps -= SSTVSET.m_CB;
								x = (int)(ps * pBitmapRX.Width / SSTVSET.m_KSS);
								if( (m_AX != x) && (x >= 0) && (x < pBitmapRX.Width) && (y < SSTVSET.m_L) ){
									m_AX = x;
									if( gp > -1 ){
										YCtoRGB( out R, out G, out B, m_Y36[x], m_D36[1,x], m_D36[0,x]);
										pBitmapRX.SetPixel( x, gp, new SKColor( (byte)R, (byte)G, (byte)B ) );

										d = GetPictureLevel(ip, i);
										d += 128;
										YCtoRGB( out R, out G, out B, d, m_D36[1,x], m_D36[0,x]);
										pBitmapRX.SetPixel( x, gp2, new SKColor( (byte)R, (byte)G, (byte)B ) );
									}
								}
							}
							break;
						case AllModes.smRM8:
						case AllModes.smRM12:
							if( ps < SSTVSET.m_KS ){               // 輝度 : Luminance, Y
								x = (int)(ps * pBitmapRX.Width / SSTVSET.m_KSS);
								if( (m_AX != x) && (x >= 0) && (x < pBitmapRX.Width) && (y < SSTVSET.m_L) ){
									m_AX = x;
									d = GetPictureLevel(ip, i);
									d *= (int)(256.0 / (256.0 - 32.0));
									d += 128;
									d = Limit256(d);
									if( gp > -1 ){
										pBitmapRX.SetPixel( x, gp,  new SKColor( (byte)d, (byte)d, (byte)d ) );
										pBitmapRX.SetPixel( x, gp2, new SKColor( (byte)d, (byte)d, (byte)d ) );
									}
								}
							}
							break;
						default:
							if( ps < SSTVSET.m_KS ){               // R or G(MRT)
								x = (int)(ps * pBitmapRX.Width / SSTVSET.m_KSS);
								if( (x != m_AX) && (x >= 0) && (x < pBitmapRX.Width) ){
									m_AX = x;
									d = GetPictureLevel(ip, i);
									d += 128;
									d = Limit256(d);
									m_D36[0,x] = (short)d;
								}
							}
							else if( ps < SSTVSET.m_CG ){          // G or B(MRT)
								ps -= SSTVSET.m_SG;
								x = (int)(ps * pBitmapRX.Width / SSTVSET.m_KSS);
								if( (x != m_AX) && (x >= 0) && (x < pBitmapRX.Width) ){
									m_AX = x;
									d = GetPictureLevel(ip, i);
									d += 128;
									d = Limit256(d);
									m_D36[1,x] = (short)d;
								}
							}
							else if( ps < SSTVSET.m_CB ){          // B or R(MRT)
								ps -= SSTVSET.m_SB;
								x = (int)(ps * pBitmapRX.Width / SSTVSET.m_KSS);
								if( (x != m_AX) && (x >= 0) && (x < pBitmapRX.Width) ){
									m_AX = x;
									d = GetPictureLevel(ip, i);
									d += 128;
									d = Limit256(d);
									if( gp > -1 ){
										if( (SSTVSET.m_Mode == AllModes.smMRT1)||
											(SSTVSET.m_Mode == AllModes.smMRT2) ){
											pBitmapRX.SetPixel( x, gp,  new SKColor( (byte)d, (byte)m_D36[0,x], (byte)m_D36[1,x] ) );
										} else {
											pBitmapRX.SetPixel( x, gp,  new SKColor( (byte)m_D36[0,x], (byte)m_D36[1,x], (byte)d ) );
										}
									}
								}
							}
							break;
					}
				}
			}
			// End for
		}


		protected void DrawSSTVNormal2() {
			int n  = dp.m_rBase; // Increments in += SSTVSET.m_WD chunks.
			int dx = -1;         // Saved X pos from the B12 buffer.
			int rx = -1;         // Saved X pos from the Rx  buffer.
			int ch = 0;          // current channel skimming the Rx buffer portion.

			if( n < 0 ) 
				throw new ApplicationException( "m_rBase went negative" );

			try {
				m_AY = (int)(n/SSTVSET.m_TW); 
				if( (m_AY < 0) || (m_AY >= pBitmapRX.Height) )
					return;

				// KRSA, assigned sys.m_UseRxBuff ? TRUE : FALSE, see also GetPictureLevel()
				if( (dp.sys.m_AutoStop || dp.sys.m_AutoSync /* || KRSA->Checked */ ) && 
					dp.m_Sync && (m_SyncPos != -1) ) {
					AutoStopJob();
				}
				m_SyncMin  = m_SyncMax = dp.m_B12[dp.m_rPage * dp.m_BWidth];
				m_SyncRPos = m_SyncPos;

				for( int i = 0; i < SSTVSET.m_WD; i++ /*, n++ */ ){
				  //double ps = n % (int)SSTVSET.m_TW; // fmod(double(n), SSTVSET.m_TW)
					short  ip = dp.m_Buf[dp.m_rPage * dp.m_BWidth + i];
					short  sp = dp.m_B12[dp.m_rPage * dp.m_BWidth + i];

					#region D12
					if( m_SyncMax < sp ) {
						m_SyncMax = sp;
						m_SyncPos = i; // was (int)ps
					} else if( m_SyncMin > sp ) {
						m_SyncMin = sp;
					}
					int x = (int)(i * pBitmapD12.Width / SSTVSET.m_TW ); // was ps, note TW == WD.
					if( (x != dx) && (x < pBitmapD12.Width) && (x >= 0) ){
						int d = sp * 256 / 4096;
						d = Limit256(d);
						pBitmapD12.SetPixel( x, m_AY, new SKColor( (byte)d ) );
						dx = x;
					}
					#endregion

					do {
						ColorChannel oChannel = _rgSlots[ch];
						if( i < oChannel.Max ) {
							if( oChannel.SetPixel != null ) {
								x = (int)((i-oChannel.Min) * pBitmapRX.Width / SSTVSET.m_KS);
								if( (x != rx) && (x >= 0) && (x < pBitmapRX.Width) ){
									rx = x;
									_rgSlots[ch].SetPixel( x, GetLevel256( ip ) );
								}
							}
							break;
						}
					} while( ++ch < _rgSlots.Count );
					// We'll throw an exception before ever getting to the bottom.
				} // End for
			} catch( Exception oEx ) {
				Type[] rgErrors = { typeof( ArgumentOutOfRangeException ),
									typeof( IndexOutOfRangeException ),
									typeof( NullReferenceException ),
									typeof( DivideByZeroException )
								  };
				if( rgErrors.IsUnhandled( oEx ) )
					throw;

				throw new ApplicationException( "Problem decoding scan line." );
			}
		}

		protected byte GetLevel256( short ip ) {
			int d;
			d = GetPixelLevel(ip);
			d += 128;
			return (byte)Limit256(d);
		}

		protected void InitSlots() {
			double dbStart = 0;
			for( int i = 0; i< _rgSlots.Count; ++i ) {
				_rgSlots[i].Min = dbStart;
				dbStart = _rgSlots[i].Max + 1/ (dp.SampFreq * 1000 );
			}
		}
    } // End Class

	public delegate void setPixel( int iX, byte bLevel );

	public class ColorChannel {
		public double   Min      { get; set; }
		public double   Max      { get; protected set; }
		public setPixel SetPixel { get; protected set; }

		public ColorChannel( double dbOffset, setPixel fnSet ) {
			Max      = dbOffset;
			SetPixel = fnSet;
		}

        public override string ToString() {
			string strType = SetPixel != null ? SetPixel.GetType().ToString() : string.Empty;
            return Max.ToString() + " " + strType;
        }
    }

	public class TmmMartin : TmmSSTV {
		void PixelSetGreen( int iX, byte bValue ) {
			m_D36[0,iX] = bValue;
		}

		void PixelSetBlue( int iX, byte bValue ) {
			m_D36[1,iX] = bValue;
		}

		void PixelSetRed( int iX, byte bValue ) {
			pBitmapRX.SetPixel( iX, m_AY,  new SKColor( bValue, (byte)m_D36[0,iX], (byte)m_D36[1,iX] ) );
		}

		public TmmMartin( CSSTVDEM p_dp ) : base( p_dp ) {
			double dbGap = .572 * p_dp.SampFreq / 1000.0;
			double dbIdx = 0;

			_rgSlots.Add( new ColorChannel( dbIdx += SSTVSET.m_OF, null ) );
			_rgSlots.Add( new ColorChannel( dbIdx += SSTVSET.m_KS, PixelSetGreen ) );
			_rgSlots.Add( new ColorChannel( dbIdx += dbGap,        null ) );
			_rgSlots.Add( new ColorChannel( dbIdx += SSTVSET.m_KS, PixelSetBlue ));
			_rgSlots.Add( new ColorChannel( dbIdx += dbGap,        null ) );
			_rgSlots.Add( new ColorChannel( dbIdx += SSTVSET.m_KS, PixelSetRed ));
			_rgSlots.Add( new ColorChannel( double.MaxValue,       null ) );

			InitSlots();
		}
	}

	public class TmmScottie : TmmSSTV {
		void PixelSetGreen( int iX, byte bValue ) {
			m_D36[0,iX] = bValue;
		}

		void PixelSetBlue( int iX, byte bValue ) {
			m_D36[1,iX] = bValue;
		}

		void PixelSetRed( int iX, byte bValue ) {
			pBitmapRX.SetPixel( iX, m_AY,  new SKColor( bValue, (byte)m_D36[0,iX], (byte)m_D36[1,iX] ) );
		}

		public TmmScottie( CSSTVDEM p_dp ) : base( p_dp ) {
			double dbGap   = 1.5 * p_dp.SampFreq / 1000.0;
			double dbHSync = 9.0 * p_dp.SampFreq / 1000.0;
			double dbIdx   = 0;

			_rgSlots.Add( new ColorChannel( dbIdx += dbGap,        null ) );
			_rgSlots.Add( new ColorChannel( dbIdx += SSTVSET.m_KS, PixelSetGreen ) );
			_rgSlots.Add( new ColorChannel( dbIdx += dbGap,        null ) );
			_rgSlots.Add( new ColorChannel( dbIdx += SSTVSET.m_KS, PixelSetBlue ));
			_rgSlots.Add( new ColorChannel( dbIdx += dbHSync,      null ) );
			_rgSlots.Add( new ColorChannel( dbIdx += dbGap,        null ) );
			_rgSlots.Add( new ColorChannel( dbIdx += SSTVSET.m_KS, PixelSetRed ));
			_rgSlots.Add( new ColorChannel( double.MaxValue,       null ) );

			InitSlots();
		}
	}
    public class DocSSTV :
        IPgParent,
        IPgLoad<TextReader>,
        IPgSave<TextWriter>,
        IDisposable
    {
        private bool disposedValue;

		public class GeneratorMode : Editor {
			public GeneratorMode(IPgBaseSite oSite) : base(oSite) {
				//new ParseHandlerText(this, "m3u");
			}

			public override WorkerStatus PlayStatus => ((DocSSTV)_oSiteBase.Host).PlayStatus;
		}

		protected class DocSlot :
			IPgBaseSite,
            IPgFileSite
		{
			protected readonly DocSSTV _oHost;

			public DocSlot( DocSSTV oHost ) {
				_oHost = oHost ?? throw new ArgumentNullException();
			}

			public IPgParent Host => _oHost;

            public void LogError(string strMessage, string strDetails, bool fShow=true) {
				_oHost.LogError( strDetails );
			}

			public void Notify( ShellNotify eEvent ) {
				// Might want this value when we close to save the current playing list!!
			}

            // Need these for the image viewer.
            public FILESTATS FileStatus   => FILESTATS.READONLY;
            public Encoding  FileEncoding => Encoding.Default;
            public string    FilePath     => "Not Implemented";
            public string    FileBase     => "Not Implemented";
		}

        protected readonly IPgBaseSite       _oSiteBase;
		protected readonly IPgRoundRobinWork _oWorkPlace;

        public IPgParent Parentage => _oSiteBase.Host;
        public IPgParent Services  => Parentage;
        public bool      IsDirty   => false;

        public event SSTVPropertyChange PropertyChange;

        public Specification  RxSpec          { get; protected set; } = new Specification( 44100, 1, 0, 16 );
        public GeneratorMode  ModeList        { get; protected set; }
        public ImageWalkerDir ImageList       { get; protected set; }
        public SKBitmap       Bitmap          => ImageList.Bitmap;

        protected readonly ImageSoloDoc  _oDocSnip;   // Clip the image.

        protected Mpg123FFTSupport FileDecoder   { get; set; }
        protected BufferSSTV       _oSSTVBuffer;
        protected CSSTVMOD         _oSSTVModulator;
        protected CSSTVDEM         _oSSTVDeModulator;
		protected IPgPlayer        _oPlayer;
        protected SSTVGenerator    _oSSTVGenerator;

        private DataTester _oDataTester;

		private double[] _rgFFTData; // Data input for FFT. Note: it get's destroyed in the process.
		private CFFT     _oFFT;
        public  int[]    FFTResult { get; protected set; }
        public  int      FFTResultSize { 
            get {
                if( _oFFT == null )
                    return 0;

                return _oFFT.Mode.TopBucket; // The 3.01kHz size we care about.
            }
        }

        /// <summary>
        /// Document type for SSTV transmit and recieve using audio i/o
        /// </summary>
        /// <exception cref="ArgumentNullException" />
        /// <exception cref="ApplicationException" />
        public DocSSTV( IPgBaseSite oSite ) {
            _oSiteBase  = oSite ?? throw new ArgumentNullException( "Site must not be null" );
            _oWorkPlace = ((IPgScheduler)Services).CreateWorkPlace() ?? throw new ApplicationException( "Couldn't create a worksite from scheduler.");

            ModeList  = new GeneratorMode ( new DocSlot( this ) );
            ImageList = new ImageWalkerDir( new DocSlot( this ) );
            _oDocSnip = new ImageSoloDoc  ( new DocSlot( this ) );
        }

        #region Dispose
        protected virtual void Dispose( bool disposing ) {
            if( !disposedValue ) {
                if( disposing ) {
                    if( _oDocSnip != null )
			            _oDocSnip.Dispose(); 

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

        public void LoadModulators( IEnumerator<SSTVMode> iterMode) {
            using BaseEditor.Manipulator oBulk = ModeList.CreateManipulator();

            while( iterMode.MoveNext() ) {
                SSTVMode oMode    = iterMode.Current;
                string   strValue = oMode.Name + " : " + oMode.Resolution.Width.ToString() + " x " + oMode.Resolution.Height.ToString();
                Line     oLine    = oBulk.LineAppendNoUndo( strValue );

                oLine.Extra = oMode;
            }
        }

        /// <summary>Find all the output devices available.</summary>
        /// <remarks>
        /// Trial run for when we actually need this.
        /// </remarks>
        public void FindOutputDevices() {
            IEnumerator<string> oIter  = MMHelpers.GetOutputDevices();
            List<string>        rgDevs = new List<string>();
            while( oIter.MoveNext() ) {
                rgDevs.Add( oIter.Current );
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

        /// <summary>
        /// Setup the output stream This only needs to be set when the Spec changes.
        /// </summary>
        /// <returns></returns>
        public bool OutputStreamInit() {
            try {
                // Help the garbage collector telling the buffer to unlink the pump (via dispose)
                if( _oSSTVBuffer != null )
                    _oSSTVBuffer.Dispose();

                _oSSTVBuffer    = new BufferSSTV( RxSpec );
                _oSSTVModulator = new CSSTVMOD( 0, RxSpec.Rate, _oSSTVBuffer );

                //_oDataTester = new DataTester( SSTVBuffer );
                //SSTVBuffer.Pump = _oDataTester.GetEnumerator();
            } catch( Exception oEx ) {
                Type[] rgErrors = { typeof( ArgumentOutOfRangeException ),
                                    typeof( NullReferenceException ) };
                if( rgErrors.IsUnhandled( oEx ) )
                    throw;

                return false;
            }

            return true;
        }

        public bool InitNew() {
            if( !ModeList.InitNew() ) // TODO: Set up hilight on TX!!
                return false;
            if( !OutputStreamInit() )  // Not really a cause for outright failure...
                return false;
			if( !_oDocSnip.InitNew() )
				return false;

            LoadModulators( GenerateMartin .GetModeEnumerator() );
            LoadModulators( GenerateScottie.GetModeEnumerator() );
            LoadModulators( GeneratePD     .GetModeEnumerator() );

            if( !ImageList.LoadURL( @"C:\Users\Frodo\Pictures") ) 
                return false;

            ImageList.ImageUpdated += Listen_ImageUpdated;

            // This only needs to change if the Spec or Device is updated.
            _oPlayer = new WmmPlayer(RxSpec, 1); 

            return true;
        }

        public bool Load( TextReader oStream ) {
            return InitNew();
        }

        public bool Save( TextWriter oStream ) {
            return true;
        }

        public int PercentFinished {
            get { 
                if( _oSSTVGenerator != null ) 
                    return _oSSTVGenerator.PercentComplete;

                return 0;
            } 
        }

        public SSTVMode TransmitMode { 
            get {
                if( _oSSTVGenerator != null )
                    return _oSSTVGenerator.Mode;

                return null;
            }
        }

        /// <summary>
        /// BUG: This is a bummer but, I use a point for the aspect ratio in my
        /// SmartRect code. I'll fix that later.
        /// </summary>
        /// <param name="iModeIndex">Index into the ModeList</param>
        /// <returns></returns>
        public SKPointI ResolutionAt( int iModeIndex ) {
            if( ModeList[iModeIndex].Extra is SSTVMode oMode )
                return new SKPointI( oMode.Resolution.Width, oMode.Resolution.Height );

            LogError( "Problem finding SSTVMode. Using default." );
            return new SKPointI( 320, 240 );
        }

        /// <summary>
        /// This sets up our transmit buffer and modulator to send the given image.
        /// </summary>
        /// <param name="iModeIndex">Index of the generator mode to use.</param>
        /// <param name="oTxImage">Image to display. It should match the generator mode requirements.</param>
        /// <returns></returns>
        public bool GeneratorSetup( SSTVMode oMode, SKBitmap oTxImage ) {
            if( oMode == null || oTxImage == null ) {
                LogError( "Mode or Image is null on Generator Setup." );
                return false;
            }
            if( _oSSTVModulator == null ) {
                LogError( "SSTV Modulator is not ready for Transmit." );
                return false;
            }
            if( _oWorkPlace.Status == WorkerStatus.BUSY ||
                _oWorkPlace.Status == WorkerStatus.PAUSED ) {
                LogError( "Stop playing current image, to begin the next." );
                return false;
            }

            _oSSTVGenerator = null;

            // Normally I'd use a guid to identify the class, but I thought I'd
            // try something a little different this time.
            try {
                if( oMode.Owner == typeof( GeneratePD ) )
                    _oSSTVGenerator = new GeneratePD     ( oTxImage, _oSSTVModulator, oMode );
                if( oMode.Owner == typeof( GenerateMartin ) )
                    _oSSTVGenerator = new GenerateMartin ( oTxImage, _oSSTVModulator, oMode );
                if( oMode.Owner == typeof( GenerateScottie ) )
                    _oSSTVGenerator = new GenerateScottie( oTxImage, _oSSTVModulator, oMode );

                if( _oSSTVGenerator != null )
                    _oSSTVBuffer.Pump = _oSSTVGenerator.GetEnumerator();
            } catch( Exception oEx ) {
                Type[] rgErrors = { typeof( ArgumentException ),
                                    typeof( ArgumentNullException ),
                                    typeof( ArgumentOutOfRangeException ),
                                    typeof( NullReferenceException ) };
                if( rgErrors.IsUnhandled( oEx ) )
                    throw;

                LogError( "Blew chunks trying to create Illudium Q-36 Video Modulator, Isn't that nice?" );
                _oSSTVGenerator = null;
            }

            Raise_PropertiesUpdated( ESstvProperty.ALL );

            return _oSSTVGenerator != null;
        }

        private void Listen_ImageUpdated() {
            Raise_PropertiesUpdated( ESstvProperty.TXImage );
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
                    uiWait = ( _oPlayer.Play( _oSSTVBuffer ) >> 1 ) + 1;
                } catch( Exception oEx ) {
                    Type[] rgErrors = { typeof( NullReferenceException ),
                                        typeof( ArgumentNullException ),
                                        typeof( MMSystemException ) };
                    if( rgErrors.IsUnhandled( oEx ) )
                        throw;

                    _oWorkPlace.Stop();
                    LogError( "Trouble playing in SSTV" );
                }
                Raise_PropertiesUpdated( ESstvProperty.UploadTime );
                yield return (int)uiWait;
            } while( _oSSTVBuffer.IsReading );

            ModeList.HighLight = null;
            // Set upload time to "finished" maybe even date/time!
        }

        /// <summary>
        /// Try to begin playing image.
        /// </summary>
        /// <param name="iMode">Which transmission type and mode.</param>
        /// <param name="skSelect">clip region in source bitmap coordinates.</param>
        /// <remarks>It's a little unfortnuate that we just don't pass the mode. But we 
        /// need to know what index it came from to set the hilight. HOWEVER, we
        /// count on the skSelect aspect ratio being set with respect to the aspect
        /// of the given SSTVMode.</remarks>
        public void PlayBegin( int iModeIndex, SKRectI skSelect ) {
            if( ModeList[iModeIndex].Extra is SSTVMode oMode ) {
                if( _oWorkPlace.Status == WorkerStatus.FREE ) {
			        // The DocSnip object retains ownership of it's generated bitmap and frees it on next load.
                    // Bug: I should check if the selection == the whole bitmap == required dimension
                    //      and I could skip the snip stage.
			        _oDocSnip.Load( Bitmap, skSelect, oMode.Resolution );
                    if( GeneratorSetup( oMode, _oDocSnip.Bitmap ) ) {
                        _oWorkPlace.Queue( GetPlayerTask(), 0 );
                        ModeList.HighLight = ModeList[iModeIndex];
                    }
                }
            }
            //while ( _oDataTester.ConsumeData() < 350000 ) {
            //}
        }

		private void SaveRxImage( TmmSSTV oSSTV ) {
			string strPath = Environment.GetFolderPath( Environment.SpecialFolder.MyPictures );

			using SKImage image  = SKImage.FromBitmap(oSSTV.pBitmapRX);
			using var     data   = image.Encode( SKEncodedImageFormat.Png, 80 );
            using var     stream = File.OpenWrite( Path.Combine( strPath, "testmeh.png") );

            data.SaveTo(stream);
		}

        public IEnumerator<int> GetRecorderTask() {
			TmmSSTV oRxSSTV;
            try {
                FFTControlValues oFFTMode = FFTControlValues.FindMode( RxSpec.Rate );
                SYSSET           sys      = new SYSSET  ( oFFTMode.SampFreq );
				CSSTVSET         oSetSSTV = new CSSTVSET( 0, oFFTMode.SampFreq, 0, sys.m_bCQ100 );

                _oSSTVDeModulator = new CSSTVDEM( oSetSSTV,
												  sys,
                                                  (int)oFFTMode.SampFreq, 
                                                  (int)oFFTMode.SampBase, 
                                                  0 );

				oRxSSTV = new TmmSSTV( _oSSTVDeModulator );
            } catch( Exception oEx ) {
                Type[] rgErrors = { typeof( NullReferenceException ),
                                    typeof( ArgumentNullException ),
                                    typeof( MMSystemException ) };
                if( rgErrors.IsUnhandled( oEx ) )
                    throw;
                LogError( "Trouble setting up decoder" );
                yield break;
            }

            do {
                try {
                    for( int i = 0; i< 500; ++i ) {
                        _oSSTVDeModulator.Do( _oSSTVBuffer.ReadOneSample() );
                    }
					oRxSSTV.DrawSSTV();
				} catch( Exception oEx ) {
                    Type[] rgErrors = { typeof( NullReferenceException ),
                                        typeof( ArgumentNullException ),
                                        typeof( MMSystemException ),
                                        typeof( InvalidOperationException ),
										typeof( ArithmeticException ) };
                    if( rgErrors.IsUnhandled( oEx ) )
                        throw;

					SaveRxImage( oRxSSTV );

                    LogError( "Trouble recordering in SSTV" );
                    // We can't call _oWorkPlace.Stop() b/c we're already in DoWork() which will
                    // try calling the _oWorker which will have been set to NULL!!
                    yield break;
                }
                yield return 0;
            } while( _oSSTVBuffer.IsReading );

            ModeList.HighLight = null;
            // Set upload time to "finished" maybe even date/time!
        }

		public IEnumerator<int> GetRecordTestTask() {
			IEnumerator<int> oIter   = _oSSTVGenerator.GetEnumerator();

			oIter            .MoveNext(); // skip the VIS for now.
			_oSSTVDeModulator.SSTVSET.SetMode( AllModes.smSCT1 );
			_oSSTVDeModulator.Start();

			TmmSSTV          oRxSSTV = new TmmScottie( _oSSTVDeModulator );

			while( oIter.MoveNext() ) {
				oRxSSTV.DrawSSTV();
				yield return 1;
			};
			SaveRxImage( oRxSSTV );
		}

        public void RecordBegin( int iModeIndex, SKRectI skSelect ) {
            if( ModeList[iModeIndex].Extra is SSTVMode oMode ) {
                if( _oWorkPlace.Status == WorkerStatus.FREE ) {
			        _oDocSnip.Load( Bitmap, skSelect, oMode.Resolution );

					FFTControlValues oFFTMode  = FFTControlValues.FindMode( 8000 ); // RxSpec.Rate
					SYSSET           sys       = new SYSSET   ( oFFTMode.SampFreq );
					CSSTVSET         oSetSSTV  = new CSSTVSET ( 0, oFFTMode.SampFreq, 0, sys.m_bCQ100 );
					DemodTest        oDemodTst = new DemodTest( oSetSSTV,
															    sys,
															    (int)oFFTMode.SampFreq, 
															    (int)oFFTMode.SampBase, 
															    0 );
					_oSSTVDeModulator = oDemodTst;
					_oSSTVModulator   = new CSSTVMOD      ( 0, oFFTMode.SampFreq, _oSSTVBuffer );
					_oSSTVGenerator   = new GenerateScottie( _oDocSnip.Bitmap, oDemodTst, oMode );

                    _oWorkPlace.Queue( GetRecordTestTask(), 0 );
                }
            }
        }

		public WorkerStatus PlayStatus => _oWorkPlace.Status;

        public void PlayStop() {
            ModeList.HighLight = null;

            _oWorkPlace.Stop();
        }

        /// <summary>
        /// Little bit of test code for the fft.
        /// </summary>
        public void PlaySegment() {
            try {
                FileDecoder.Read();

                _oFFT.Calc( _rgFFTData, 10, 0, FFTResult );

                Raise_PropertiesUpdated( ESstvProperty.FFT );
            } catch( Exception oEx ) {
                Type[] rgErrors = { typeof( NotImplementedException ),
                                    typeof( NullReferenceException ) };
                if( rgErrors.IsUnhandled( oEx ) )
                    throw;
            }
        }
    } // End class
}
