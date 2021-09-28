///<summary>
///  Copyright (c) https://github.com/TheMostDifficultStep
///
///  Permission is hereby granted, free of charge, to any person obtaining
///  a copy of this software and associated documentation files (the
///  "Software"), to deal in the Software without restriction, including
///  without limitation the rights to use, copy, modify, merge, publish,
///  distribute, sublicense, and/or sell copies of the Software, and to
///  permit persons to whom the Software is furnished to do so, subject to
///  the following conditions:
///
///  The above copyright notice and this permission notice shall be
///  included in all copies or substantial portions of the Software.
///
///  THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
///  EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
///  MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
///  NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS
///  BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN
///  ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN
///  CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
///  SOFTWARE
///</summary>

using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

using Play.Sound.FFT;

namespace Play.Sound {
	class MainClass	{
		public static IPgPlayer CreatePlayer( Specification oSpec ) {
			// oPlayer = new WmmPlayer( oSpec, -1 );
            // oPlayer = new LibAO( oSpec );
			// return( new Alsa( oSpec ) );
			return( new WmmPlayer( oSpec, -1 ));
		}

		public static IPgPlayer SetPlayer( IPgPlayer oPlayer, Specification oSpec ) {
			if( oPlayer == null ) {
				oPlayer = CreatePlayer( oSpec );
			} else {
				if( !oPlayer.Spec.CompareTo( oSpec ) ) {
					oPlayer.Dispose(); // There's a sleep in there.
					oPlayer = CreatePlayer( oSpec );
				}
			}
			return( oPlayer );
		}

        /// <summary>
        /// Puts the thread to sleep approximately half the expected play time.
        /// </summary>
        /// <param name="uiMaxTimeInMS">Max play time in milliseconds.</param>
		public static void SleepMeh( uint uiMaxTimeInMS ) {
			if( uiMaxTimeInMS > 0 )
				Thread.Sleep((int)(uiMaxTimeInMS >> 1) + 1);
		}

		public static void PlaySongs( Mpg123Factory oFactory, List<string> rgSongs ) 
		{
			IPgPlayer oPlayer = null;
			
			try {
				foreach( string strSong in rgSongs ) {
					try {
						using (IPgReader oDecoder = oFactory.CreateFor(strSong)) {
                            Console.WriteLine( strSong );
							oPlayer = SetPlayer( oPlayer, oDecoder.Spec );
							do {
								SleepMeh( oPlayer.Play( oDecoder ) );
							} while( oDecoder.IsReading );
						}
					} catch( Exception oEx ) {
						Type[] rgErrors = { typeof( FileNotFoundException ),
										 	typeof( FileLoadException ), 
											typeof( FormatException ),
											typeof( DllNotFoundException ),
											typeof( MMSystemException ),
											typeof( InvalidOperationException ),
											typeof( NullReferenceException ) };
						if( !rgErrors.Contains( oEx.GetType() ) ) {
							throw oEx;
						}
						Console.Write( "Couldn't find file, or play, or continue to play format of : \"" );
						Console.Write( strSong );
						Console.WriteLine( "\" Skipping." );
					}
				}
			} finally {
				SleepMeh( oPlayer.Busy ); // Bleed off rest of song

				if( oPlayer != null )
					oPlayer.Dispose();
			}
		}

		public static void PlaySong( Mpg123Factory oFactory, string strFileName ) 
		{
			try {
				using( IPgReader oDecoder = oFactory.CreateFor( strFileName ) ) {
                    Console.WriteLine( strFileName );
					using( IPgPlayer oPlayer = SetPlayer( null, oDecoder.Spec ) ) {
						do {
							SleepMeh( oPlayer.Play( oDecoder ) );
						} while( oDecoder.IsReading );

						SleepMeh( oPlayer.Busy );
					}
				}
			} catch( Exception oEx ) {
				Type[] rgErrors = { typeof( FileNotFoundException ),
									typeof( FileLoadException ), 
									typeof( FormatException ),
									typeof( DllNotFoundException ),
									typeof( MMSystemException ),
									typeof( InvalidOperationException ) };
				if( !rgErrors.Contains( oEx.GetType() ) ) {
					throw oEx;
				}
				Console.Write( "Couldn't find file, or play, or continue to play format of : \"" );
				Console.Write( strFileName );
				Console.WriteLine( "\" Skipping." );
				Console.WriteLine( oEx.Message );
			}
		}

		/// <summary>
		/// Reads the m3u. An m3u is simply a text file with the songs listed within.
		/// </summary>
		/// <param name="strFileName">file name.</param>
		/// <param name="rgPlay">Load this structure with the songs in the given file.</param>
		public static void ReadM3u( string strFileName, List<string> rgPlay )
		{
			try {
				FileInfo   oFile   = new FileInfo( strFileName );
				FileStream oStream = oFile.OpenRead();
                string     strDir  = Path.GetDirectoryName( strFileName );

				using( StreamReader oReader = new StreamReader( oStream, new UTF8Encoding( false ) ) ) {
					do {
						string strLine = oReader.ReadLine();
						if( strLine == null )
							break;
						if( strLine.Length > 0 ) {
							// Convert any windows paths to linux. Probably will work for simple relative
							// path corrections. But path with windows volumes will still gag.
							strLine = strLine.Replace( '\\', '/' );
							rgPlay.Add( Path.Combine( strDir, strLine ) );
						}
					} while( true );
				}
			} catch( Exception oEx ) {
				Type[] rgErrors = { typeof( DirectoryNotFoundException ),
									typeof( FileNotFoundException ),
									typeof( IOException ),
									typeof( ArgumentException ),
									typeof( NullReferenceException ) };
				if( !rgErrors.Contains( oEx.GetType() ) ) {
					throw oEx;
				}
				throw new InvalidProgramException( "Something weird going on during m3u read", oEx );
			}
		}

		public static void Main( string[] args ) {
			Specification oSpec = new Specification(44100,1,0,16);

			using (IPgPlayer oPlayer = CreatePlayer(oSpec)) {
				using (GenerateMorse oReader = new GenerateMorse(oSpec)) {
					oReader.Signal = "This is a test.  " +
									 "This is a second test.  " +
									 "No we see what we are up to.  " +
									 "Here is more test data.";

					do {
						SleepMeh( oPlayer.Play( oReader ) );
					} while( oPlayer.Busy > 0 );
				}
			}
		}

		public static void Main2 (string[] args)
		{
			if (args.Length < 1)
				return;

			string       strFileName = args [0];
			string       strExt      = string.Empty;
			List<string> rgPlay      = new();

			try {
				strExt = Path.GetExtension( strFileName );
			} catch( Exception oEx ) {
				Type[] rgErrors = { typeof( ArgumentException ),
					                typeof( ArgumentNullException ) };

				if( !rgErrors.Contains( oEx.GetType() ) ) {
					throw;
				}
				Console.WriteLine( "Problem with playlist or file path." );
			}

			switch( strExt.ToLower() ) {
				case ".mp3":
					rgPlay.Add( strFileName );
					break;
				case ".m3u":
					ReadM3u( strFileName, rgPlay );
					break;
			}

			if( rgPlay.Count <= 0 ) {
				Console.WriteLine( "Didn't find anything I can try to play!" );
				return;
			}

			using( Mpg123Factory oMpgFactory = new() ) {
				PlaySongs( oMpgFactory, rgPlay );
			}
		}
	} // end Class

    class Program2 {
        static void Main2(string[] args) {
            Console.WriteLine("Hello World!");

            Test4();
        }

        protected static void Test1() {
            // Example data:
            List<int> data = new List<int>() {
                 14, 30, 35, 34, 34, 40, 46, 45, 30, 4,  -26,  -48,  -55,  -49,  -37,
                -28,  -24,  -22,  -13,  6,  32, 55, 65, 57, 38, 17, 1,  -6, -11,  -19,  -34,
                -51,  -61,  -56,  -35,  -7, 18, 32, 35, 34, 35, 41, 46, 43, 26, -2, -31,  -50,
                -55,  -47,  -35,  -27,  -24,  -21,  -10,  11, 37, 58, 64, 55, 34, 13, -1, -7
            };
            // Answer should be: 4, 9, 28, 23, 13 (peak data, if frequency is 64)

            //EasyFFT oFFT = new EasyFFT(6);

            //oFFT.Peaks(oFFT.FFT(data, 100));

            //PrintPeaks( oFFT );
        }

        //protected static void PrintPeaks( EasyFFT oFFT ) {
        //    for (int i = 0; i < oFFT.f_peaks.Length; ++i) {
        //        Console.WriteLine(oFFT.f_peaks[i].ToString());
        //    }
        //}

        protected static void Test3() {
            FFTControlValues oCtrl    = FFTControlValues.FindMode( 11025 );
            int              iSamples = (int)oCtrl.FFTSize;
            List<double>     data     = new List<double>();
            float            dStep    = (float)Math.PI * 2 / (float)iSamples; // 2 * pi is 360 degrees.

            for ( float rad = 0; data.Count < iSamples; rad += dStep ) {
                double dbSample = 40 * MathF.Sin(rad * 400F);
                
                dbSample += 20 * Math.Sin(rad * 200F);

                data.Add(dbSample);
            }

            CFFT oFFT = new CFFT( oCtrl );

            oFFT.Calc( data.ToArray(), 30, 0, oFFT.m_fft );

            Console.WriteLine( "Samples: " + iSamples.ToString() );
        }

        /// <summary>
        /// Generate enough samples to fill the FFT sample size. In this 
        /// function we're generating a signal that might get "decimated" by the FFT loader.
        /// This is the function you want to use to test the FFTLoader and it's "decimation" modes.
        /// </summary>
        /// <param name="oCtrl">The control values for the FFT.</param>
        /// <param name="data">Generated data stream.</param>
        protected static void LoadData( FFTControlValues oCtrl, List<double> data ) {
            // We need FFTSize number of samples.
            for( double t = 0; data.Count < oCtrl.FFTSize; t += 1 / oCtrl.SampBase ) {
                double dbSample = 0;
                    
                dbSample += 40 * Math.Sin( Math.PI * 2 * 400 * t);
                dbSample += 20 * Math.Sin( Math.PI * 2 * 200 * t);

                data.Add(dbSample);
            }
        }

        /// <summary>
        /// Trying to generate enough samples to fill the FFT sample size. And
        /// Raw FFTSampleFreq. This data will not be correct for the "decimated"
        /// modes of the FFT loader, since FFTSampFreq represents the frequency
        /// of the "decimated" signal.
        /// </summary>
        /// <param name="oCtrl">The control values for the FFT.</param>
        /// <param name="data">Generated data stream.</param>
        protected static void LoadDataRaw( FFTControlValues oCtrl, List<double> data ) {
            // We need FFTSize number of samples.
            for( double t = 0; data.Count < oCtrl.FFTSize; t += 1 / oCtrl.FFTSampFreq ) {
                double dbSample = 0;
                    
                dbSample += 40 * Math.Sin( Math.PI * 2 * 400 * t);
                dbSample += 20 * Math.Sin( Math.PI * 2 * 200 * t);

                data.Add(dbSample);
            }
        }

        protected static void Test4() {
            FFTControlValues oCtrl  = FFTControlValues.FindMode( 18000.0 );
            List<double>     rgData = new List<double>();
            CFFTCollector    oFFT   = new CFFTCollector( oCtrl );

            LoadData( oCtrl, rgData );

            //oFFT.Calc( rgData.ToArray(), 30, 0, oFFT.m_fft );
            //Array.Clear( oFFT.m_fft, 0, oFFT.m_fft.Length );

            oFFT.CollectFFT( 4, rgData.ToArray(), rgData.Count );
            oFFT.CalcFFT( 30, 0 );
            oFFT.CalcFFT( 30, 0 );

            Console.WriteLine( "Samples: " + rgData.Count.ToString() );
        }

        protected static void Test5() {
            //FFTControlValues.TestMe();
        }
    }
}
