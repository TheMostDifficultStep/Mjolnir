using System;
using System.Collections.Generic;

using SkiaSharp;

//-----------------------------------------------------------------------------------------------------------------------------------------------
// Copyright+LGPL
// Copyright 2000-2013 Makoto Mori, Nobuyuki Oba
// (c) 2021 https://github.com/TheMostDifficultStep
//-----------------------------------------------------------------------------------------------------------------------------------------------
// This file is a port of MMSSTV. You would not know by looking at it but yeah.

// MMSSTV is free software: you can redistribute it and/or modify it under the terms of the GNU Lesser General Public License
// as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later version.

// This code is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of 
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU Lesser General Public License for more details.

// You should have received a copy of the GNU Lesser General Public License along with MMTTY.  If not, see 
// <http://www.gnu.org/licenses/>.
//-----------------------------------------------------------------------------------------------------------------------------------------------

namespace Play.Sound {

    public enum ScanLineChannelType : int {
        Sync = 0,
        Gap,
        Red,
        Green,
        Blue,
        Y1,
        Y2,
        RY,
        BY,
        Y,
        RYx2,
        BYx2,
        R36Y2,
        R36By,
        R36Cln,
        END
    }

    public class ScanLineChannel {
        public double              WidthInMs { get; }
        public ScanLineChannelType Type      { get; }

        public ScanLineChannel( double dblWidthInMs, ScanLineChannelType eType ) {
            WidthInMs = dblWidthInMs;
            Type      = eType;
        }
    }

    public class SSTVModeRobot422 : SSTVMode {
        public SSTVModeRobot422( byte bVIS, string strName, double dblSync, double dblGap, double dblClrWidth, SKSizeI skSize, AllModes eLegacy = AllModes.smEND) : 
            base( TVFamily.Robot, bVIS, strName, dblClrWidth, skSize, eLegacy ) 
        {
            WidthSyncInMS = dblSync;
            WidthGapInMS  = dblGap;
        }

        public override string FamilyName => "Robot";

        public override double WidthSyncInMS { get; }
        public override double WidthGapInMS  { get; }

        /// <summary>
        /// Uses two horizontal pixels for one RY & BY value.
        /// </summary>
        /// <exception cref="InvalidProgramException"></exception>
		protected override void Initialize() {
			if( Family != TVFamily.Robot )
				throw new InvalidProgramException( "Mode must be of Robot type" );

            ChannelMap.Clear(); // Just in case we get called again.

			ChannelMap.Add( new( WidthSyncInMS,    ScanLineChannelType.Sync ) );
			ChannelMap.Add( new( WidthGapInMS,     ScanLineChannelType.Gap  ) );
			ChannelMap.Add( new( WidthColorInMS,   ScanLineChannelType.Y    ) );

            ChannelMap.Add( new( WidthSyncInMS /2, ScanLineChannelType.Gap  ) );
			ChannelMap.Add( new( WidthGapInMS  /2, ScanLineChannelType.Gap  ) );
			ChannelMap.Add( new( WidthColorInMS/2, ScanLineChannelType.RYx2 ) );

			ChannelMap.Add( new( WidthSyncInMS /2, ScanLineChannelType.Gap  ) );
            ChannelMap.Add( new( WidthGapInMS  /2, ScanLineChannelType.Gap  ) );
			ChannelMap.Add( new( WidthColorInMS/2, ScanLineChannelType.BYx2 ) );

            SetScanWidth();
		}

        public static IEnumerator<SSTVMode> EnumAllModes() {
            List<SSTVModeRobot422> rgModes = new();

 	      //rgModes.Add( new SSTVModeRobot420( 0x00, "12", 7, 3,  60, new SKSizeI( 160, 120 ), AllModes.smR12 ) );
 	        rgModes.Add( new SSTVModeRobot422( 0x84, "24", 6, 2,  92, new SKSizeI( 160, 120 ), AllModes.smR24 ) );
            rgModes.Add( new SSTVModeRobot420( 0x88, "36", 9, 3,  88, new SKSizeI( 320, 240 ), AllModes.smR36 ) );
 	        rgModes.Add( new SSTVModeRobot422( 0x0c, "72", 9, 3, 138, new SKSizeI( 320, 240 ), AllModes.smR72 ) );

            foreach( SSTVModeRobot422 oMode in rgModes ) {
                oMode.Initialize();
                yield return oMode;
            }
        }
    }

    /// <summary>
    /// This class parses the Robot 36 data type.
    /// </summary>
    public class SSTVModeRobot420 : SSTVModeRobot422 {
        public SSTVModeRobot420( byte bVIS, string strName, double dblSync, double dblGap, double dblClrWidth, SKSizeI skSize, AllModes eLegacy = AllModes.smEND) : 
            base( bVIS, strName, dblSync, dblGap, dblClrWidth, skSize, eLegacy ) 
        {
        }
        public override int ScanMultiplier { get; } = 2;

        /// <summary>
        /// 420 mode is going to be pecular since you need know if you are on 
        /// an even or odd scan line. If we catch the VIS we're ok. Else if
        /// guessing in the middle we might be off by one. As it is I'm cheating
        /// and merging the two scan lines so that my parallel thread system can deal
        /// with Y1, By, Y2, Ry as a single (super) scanline. Since Ry or By is being
        /// used from the previous scan. Turns out it doesn't seem to affect the
        /// slant detection code! Thank goodness.
        /// </summary>
        /// <exception cref="InvalidProgramException"></exception>
		protected override void Initialize() {
			if( Family != TVFamily.Robot )
				throw new InvalidProgramException( "Mode must be of Robot type" );

            ChannelMap.Clear(); // Just in case we get called again.

			ChannelMap.Add( new( WidthSyncInMS,    ScanLineChannelType.Sync   ) );
			ChannelMap.Add( new( WidthGapInMS,     ScanLineChannelType.Gap    ) );
			ChannelMap.Add( new( WidthColorInMS,   ScanLineChannelType.Y1     ) );
                                                                              
			ChannelMap.Add( new( WidthSyncInMS /2, ScanLineChannelType.Gap    ) );
            ChannelMap.Add( new( WidthGapInMS  /2, ScanLineChannelType.Gap    ) );
			ChannelMap.Add( new( WidthColorInMS/2, ScanLineChannelType.R36By  ) );

			ChannelMap.Add( new( WidthSyncInMS,    ScanLineChannelType.Sync   ) );
			ChannelMap.Add( new( WidthGapInMS,     ScanLineChannelType.Gap    ) );
			ChannelMap.Add( new( WidthColorInMS,   ScanLineChannelType.R36Y2  ) );
                                                                              
			ChannelMap.Add( new( WidthSyncInMS /2, ScanLineChannelType.Gap    ) );
            ChannelMap.Add( new( WidthGapInMS  /2, ScanLineChannelType.Gap    ) );
			ChannelMap.Add( new( WidthColorInMS/2, ScanLineChannelType.R36Cln ) );

            SetScanWidth();
		}
    }

    public class SSTVModePasokon : SSTVMode {
        public SSTVModePasokon( byte bVIS, string strName, double dblSync, double dblGap, double dblClrWidth, SKSizeI skSize, AllModes eLegacy = AllModes.smEND) : 
            base( TVFamily.Pasokon, bVIS, strName, dblClrWidth, skSize, eLegacy ) 
        {
            WidthSyncInMS = dblSync;
            WidthGapInMS  = dblGap;
        }

        public override double WidthSyncInMS { get; }
        public override double WidthGapInMS  { get; }

        public override string FamilyName => "Pasokon";

		protected override void Initialize() {
			if( Family != TVFamily.Pasokon )
				throw new InvalidProgramException( "Mode must be of Pasokon type" );

            ChannelMap.Clear(); // Just in case we get called again.

			ChannelMap.Add( new( WidthSyncInMS,  ScanLineChannelType.Sync  ) );
			ChannelMap.Add( new( WidthGapInMS,   ScanLineChannelType.Gap   ) );
			ChannelMap.Add( new( WidthColorInMS, ScanLineChannelType.Red   ) );
			ChannelMap.Add( new( WidthGapInMS,   ScanLineChannelType.Gap   ) );
			ChannelMap.Add( new( WidthColorInMS, ScanLineChannelType.Green ) );
			ChannelMap.Add( new( WidthGapInMS,   ScanLineChannelType.Gap   ) );
			ChannelMap.Add( new( WidthColorInMS, ScanLineChannelType.Blue  ) );
            ChannelMap.Add( new( WidthGapInMS,   ScanLineChannelType.Gap   ) );

            SetScanWidth();
		}

        ///<remarks>sstv-handbook has P3 as 320 wide bitmap, but mmsstv uses 640.
        /// Also the sstv-handbook sez the sync signal is "20 units" wide,
        /// and a gap is "5 units" wide with 1 pixel being a unit. But 
        /// 133.333/640 is .2083... and 5.208 => 4.167 + 1.042, which is a 
        /// sync + a gap. I think it's a bug in MMSSTV</remarks> 
        public static IEnumerator<SSTVMode> EnumAllModes() {
            List<SSTVModePasokon> rgModes = new();

 	        rgModes.Add( new SSTVModePasokon( 0x71, "3",   5.208, 1.042,    133.333, new SKSizeI( 640, 496 ), AllModes.smP3 ) );
            rgModes.Add( new SSTVModePasokon( 0x72, "5",   7.813, 1.562375, 200.000, new SKSizeI( 640, 496 ), AllModes.smP5 ) );
 	        rgModes.Add( new SSTVModePasokon( 0xf3, "7",  10.417, 2.083,    146.432, new SKSizeI( 640, 496 ), AllModes.smP7 ) );

            foreach( SSTVModePasokon oMode in rgModes ) {
                oMode.Initialize();
                yield return oMode;
            }
        }
    }

    public class SSTVModeMartin : SSTVMode {
        public SSTVModeMartin( byte bVIS, string strName, double dbTxWidth, SKSizeI skSize, AllModes eLegacy = AllModes.smEND) : 
            base( TVFamily.Martin, bVIS, strName, dbTxWidth, skSize, eLegacy ) 
        {
        }

        public override double WidthSyncInMS => 4.862;
        public override double WidthGapInMS  => 0.572;

        public override string FamilyName => "Martin";
		protected override void Initialize() {
			if( Family != TVFamily.Martin )
				throw new InvalidProgramException( "Mode must be of Martin type" );

			ChannelMap.Add( new( WidthSyncInMS,  ScanLineChannelType.Sync  ) );
			ChannelMap.Add( new( WidthGapInMS,   ScanLineChannelType.Gap   ) );
			ChannelMap.Add( new( WidthColorInMS, ScanLineChannelType.Green ) );
			ChannelMap.Add( new( WidthGapInMS,   ScanLineChannelType.Gap   ) );
			ChannelMap.Add( new( WidthColorInMS, ScanLineChannelType.Blue  ) );
			ChannelMap.Add( new( WidthGapInMS,   ScanLineChannelType.Gap   ) );
			ChannelMap.Add( new( WidthColorInMS, ScanLineChannelType.Red   ) );

            SetScanWidth();
        }

        /// <summary>
        /// Enumerate the modes we support. Updated to handbook values.
        /// </summary>
        /// <returns></returns>
        public static IEnumerator<SSTVMode> EnumAllModes() {
            List<SSTVModeMartin> rgModes = new();

 	        rgModes.Add( new SSTVModeMartin( 0xac, "1",  146.432, new SKSizeI( 320, 256 ), AllModes.smMRT1 ) );
            rgModes.Add( new SSTVModeMartin( 0x28, "2",   73.216, new SKSizeI( 320, 256 ), AllModes.smMRT2 ) );
 	        rgModes.Add( new SSTVModeMartin( 0x24, "3",  146.432, new SKSizeI( 160, 128 ), AllModes.smMRT3 ) );
            rgModes.Add( new SSTVModeMartin( 0xa0, "4",   73.216, new SKSizeI( 160, 128 ), AllModes.smMRT4 ) );

            foreach( SSTVModeMartin oMode in rgModes ) {
                oMode.Initialize();
                yield return oMode;
            }
        }
    }

    public class SSTVModeScottie : SSTVMode {
        public SSTVModeScottie( byte bVIS, string strName, double dbTxWidth, SKSizeI skSize, AllModes eLegacy = AllModes.smEND) : 
            base( TVFamily.Scottie, bVIS, strName, dbTxWidth, skSize, eLegacy ) 
        {
        }

        public override double WidthSyncInMS => 9.0;
        public override double WidthGapInMS  => 1.5;

        public override string FamilyName => "Scottie";

		protected override void Initialize() {
			if( Family != TVFamily.Scottie )
				throw new ArgumentOutOfRangeException( "Mode must be of Scottie type" );

			ChannelMap.Add( new ( WidthGapInMS,    ScanLineChannelType.Gap   ) );
			ChannelMap.Add( new ( WidthColorInMS,  ScanLineChannelType.Green ) );
			ChannelMap.Add( new ( WidthGapInMS,    ScanLineChannelType.Gap   ) );
			ChannelMap.Add( new ( WidthColorInMS,  ScanLineChannelType.Blue  ) );
			ChannelMap.Add( new ( WidthSyncInMS,   ScanLineChannelType.Sync  ) );
			ChannelMap.Add( new ( WidthGapInMS,    ScanLineChannelType.Gap   ) );
			ChannelMap.Add( new ( WidthColorInMS,  ScanLineChannelType.Red   ) );

            SetScanWidth();
		}

        public override double OffsetInMS => ( WidthGapInMS * 2 ) + ( WidthColorInMS * 2 ) + WidthSyncInMS;

        /// <summary>
        /// Enumerate the modes we support. Note that only Scotty 1 VIS code matches that
        /// from OK2MNM; Scottie S2 : 0x38 (vs b8=10111000), and Scottie DX : 0x4C (vs cc=11001100).
        /// This is because the MMSSTV number has the parity bit (pre)set accordingly.
        /// Note that the video resolution number doesn't seem to make sense for scottie 2.
        /// if you use OK2MNM's VIS table.
        /// </summary>
        /// <returns></returns>
        public static IEnumerator<SSTVMode> EnumAllModes() {
            List<SSTVModeScottie> rgModes = new();

 	        rgModes.Add( new SSTVModeScottie( 0x3c, "1",  138.240, new SKSizeI( 320, 256 ), AllModes.smSCT1  ) );
            rgModes.Add( new SSTVModeScottie( 0xb8, "2",   88.064, new SKSizeI( 320, 256 ), AllModes.smSCT2  ) );
            rgModes.Add( new SSTVModeScottie( 0xcc, "DX", 345.600, new SKSizeI( 320, 256 ), AllModes.smSCTDX ) );

            foreach( SSTVModeScottie oMode in rgModes ) {
                oMode.Initialize();
                yield return oMode;
            }
        }

    }

    /// <summary>
    /// Note that PD is 420 sub sampling. 
    /// </summary>
    public class SSTVModePD : SSTVMode {
        public SSTVModePD( byte bVIS, string strName, double dbTxWidth, SKSizeI skSize, AllModes eLegacy = AllModes.smEND) : 
            base( TVFamily.PD, bVIS, strName, dbTxWidth, skSize, eLegacy ) 
        {
        }

        public override double WidthSyncInMS => 20.0;
        public override double WidthGapInMS  => 2.08;
        public override int    ScanMultiplier { get; } = 2;

        public override string FamilyName => "PD";

        public override double OffsetInMS => WidthSyncInMS - 2.08; // This fixes it... why?


        protected override void Initialize() {
			if( Family != TVFamily.PD )
				throw new ArgumentOutOfRangeException( "Mode must be of PD type" );

			ChannelMap.Add( new( WidthSyncInMS,  ScanLineChannelType.Sync ) );
			ChannelMap.Add( new( WidthGapInMS,   ScanLineChannelType.Gap  ) );
			ChannelMap.Add( new( WidthColorInMS, ScanLineChannelType.Y1   ) );
			ChannelMap.Add( new( WidthColorInMS, ScanLineChannelType.RY   ) );
			ChannelMap.Add( new( WidthColorInMS, ScanLineChannelType.BY   ) );
			ChannelMap.Add( new( WidthColorInMS, ScanLineChannelType.Y2   ) );

            SetScanWidth();
		}
        /// <summary>So the scottie and martin modes I'm pretty confident are simply 320 horizontal lines
        /// But I know the PD modes are meant to be higher res and I got all the info straight from
        /// the inventor's web site. Which btw does not mention PD50 and PD290 modes. Also not I'm NOT
        /// presently generating the 16 scan line b/w scale. Note that all of them work.
        /// See also:  Martin Bruchanov OK2MNM SSTV-Handbook.
        /// </summary> 
        public static IEnumerator<SSTVMode> EnumAllModes() {
            List<SSTVModePD> rgModes = new();

            // these numbers come from https://www.classicsstv.com/pdmodes.php G4IJE the inventor.
 	        rgModes.Add( new SSTVModePD( 0xdd, " 50",   91.520, new SKSizeI( 320, 256 ), AllModes.smPD50  )); // see SSTV-Handbook.
            rgModes.Add( new SSTVModePD( 0x63, " 90",  170.240, new SKSizeI( 320, 256 ), AllModes.smPD90  )); // Only reliable one.
            rgModes.Add( new SSTVModePD( 0x5f, "120",  121.600, new SKSizeI( 640, 512 ), AllModes.smPD120 )); 
            rgModes.Add( new SSTVModePD( 0xe2, "160",  195.584, new SKSizeI( 512, 384 ), AllModes.smPD160 )); 
            rgModes.Add( new SSTVModePD( 0x60, "180",  183.040, new SKSizeI( 640, 512 ), AllModes.smPD180 ));
            rgModes.Add( new SSTVModePD( 0xe1, "240",  244.480, new SKSizeI( 640, 512 ), AllModes.smPD240 )); 
            rgModes.Add( new SSTVModePD( 0xde, "290",  228.800, new SKSizeI( 800, 600 ), AllModes.smPD290 )); // see SSTV-handbook.

            foreach( SSTVModePD oMode in rgModes ) {
                oMode.Initialize();
                yield return oMode;
            }
        }

    }

    public class SSTVModeBW : SSTVMode {
        public SSTVModeBW( byte bVIS, string strName, double dbTxWidth, SKSizeI skSize, AllModes eLegacy = AllModes.smEND) : 
            base( TVFamily.BW, bVIS, strName, dbTxWidth, skSize, eLegacy ) 
        {
        }

        public override double WidthSyncInMS => 6;
        public override double WidthGapInMS  => 2;

        public override string FamilyName => "BW";

		protected override void Initialize() {
			if( Family != TVFamily.BW )
				throw new InvalidProgramException( "Mode must be of BW type" );

			ChannelMap.Add( new( WidthSyncInMS,  ScanLineChannelType.Sync  ) );
			ChannelMap.Add( new( WidthGapInMS,   ScanLineChannelType.Gap   ) );
			ChannelMap.Add( new( WidthColorInMS, ScanLineChannelType.Y ) );

            SetScanWidth();
		}
        public static IEnumerator<SSTVMode> EnumAllModes() {
            List<SSTVModeBW> rgModes = new();

 	        rgModes.Add( new SSTVModeBW( 0x86, "12",     92.0, new SKSizeI( 160, 120 ), AllModes.smRM12 ) ); 
 	        rgModes.Add( new SSTVModeBW( 0x82, " 8", 58.89709, new SKSizeI( 160, 120 ), AllModes.smRM8  ) ); 

            foreach( SSTVModeBW oMode in rgModes ) {
                oMode.Initialize();
                yield return oMode;
            }
        }
    }

    public class SSTVModeWWV : SSTVMode {
        public SSTVModeWWV( byte bVIS, string strName, double dbTxWidth, SKSizeI skSize, AllModes eLegacy = AllModes.smEND) : 
            base( TVFamily.WWV, bVIS, strName, dbTxWidth, skSize, eLegacy ) 
        {
        }

        public override double WidthSyncInMS => 6;
        public override double WidthGapInMS  => 2;

        public override string FamilyName => "WWV";


		protected override void Initialize() {
			if( Family != TVFamily.WWV )
				throw new InvalidProgramException( "Mode must be of WWV type" );

			ChannelMap.Add( new( WidthSyncInMS,  ScanLineChannelType.Sync  ) );
			ChannelMap.Add( new( WidthColorInMS, ScanLineChannelType.Y ) );

            SetScanWidth();
		}
    }
}

