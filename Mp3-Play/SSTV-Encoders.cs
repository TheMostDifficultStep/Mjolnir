using System;
using System.Collections.Generic;
using System.Linq;

using SkiaSharp;

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
    /// <summary>
    /// SCT, Scottie S1, S3, DX
    /// </summary>
    public class GenerateScottie : SSTVGenerator {
        /// <exception cref="ArgumentOutOfRangeException" />
        public GenerateScottie( SKBitmap oBitmap, IPgModulator oModulator, SSTVMode oMode ) : 
            base( oBitmap, oModulator, oMode )
        {
        }

        /// <summary>
        /// See line 7120 in main.cpp of the MMSSTV project. He adds this one time sync signal at the start.
        /// But it doesn't seem to make sense, as it throws off the horizontal alignment (not the slant) of
        /// the recieved image in my system. All the modes work happily w/o this code as does MMSSTV. So punt.
        /// </summary>
        /// <param name="uiVIS"></param>
        //public override void WriteVIS( ushort uiVIS ) {
        //    base.WriteVIS(uiVIS);

        //    Write( 1200, 9 ); // One time Sync, rely on exact timing (in old days)
        //}

        /// <summary>
        /// TMmsstv::LineSCT, derived.
        /// </summary>
        /// <param name="iLine">The bitmap line to output.</param>
        /// <returns>How many samples written.</returns>
        /// <remarks>I'm not sure how important it is to cache the line from the bitmap.
        /// The original code does this. Saves a multiply I would guess.</remarks>
        protected override void WriteLine( int iLine ) {
	        double dbTimePerPixel = Mode.WidthColorInMS / 320.0; // Note: hard coded.

            if( iLine > Height )
                return;

            try {
                _rgRGBCache.Clear(); // MUST clear this else keeps using first line data!!
            
	            Write( 1500, GainIndx.G, 1.5 );      // gap (porch?)
	            for( int x = 0; x < 320; x++ ) {     // G
                    _rgRGBCache.Add( GetPixel( x, iLine ) );
		            Write( ColorToFreq( _rgRGBCache[x].Green ), GainIndx.G, dbTimePerPixel );
	            }
	            Write( 1500, GainIndx.B, 1.5 );
	            for( int x = 0; x < 320; x++ ) {     // B
		            Write( ColorToFreq( _rgRGBCache[x].Blue  ), GainIndx.B, dbTimePerPixel );
	            }
	            Write( 1200, 9 );                    // HSync in second half!!
	            Write( 1500, GainIndx.R, 1.5 );
	            for( int x = 0; x < 320; x++ ) {     // R
		            Write( ColorToFreq( _rgRGBCache[x].Red   ), GainIndx.R, dbTimePerPixel );
	            }
            } catch( Exception oEx ) {
                Type[] rgErrors = { typeof( AccessViolationException ),
                                    typeof( NullReferenceException ) };
                if( !rgErrors.Contains( oEx.GetType() ) )
                    throw;

                // This would be a good place to return an error.
            }
        }
    }

    public class GeneratePasokon : SSTVGenerator {

        public GeneratePasokon( SKBitmap oBitmap, IPgModulator oModulator, SSTVMode oMode ) : 
            base( oBitmap, oModulator, oMode )
        {
        }

        protected override void WriteLine( int iLine ) {
	        double dbTimePerPixel = Mode.WidthColorInMS / Mode.Resolution.Width; 

            if( iLine > Height )
                return;

            try {
                _rgRGBCache.Clear();

	            Write( 1200, Mode.WidthSyncInMS );

	            Write( 1500, GainIndx.R, Mode.WidthGapInMS );   // R gap
	            for( int x = 0; x < Mode.Resolution.Width; x++ ) {
                    _rgRGBCache.Add( GetPixel( x, iLine ) );       // Don't forget to add the cache line!!
		            Write( ColorToFreq(_rgRGBCache[x].Red  ), GainIndx.R, dbTimePerPixel );
	            }

	            Write( 1500, GainIndx.G, Mode.WidthGapInMS );   // G gap
	            for( int x = 0; x < Mode.Resolution.Width; x++ ) {     
		            Write( ColorToFreq(_rgRGBCache[x].Green), GainIndx.G, dbTimePerPixel );
	            }

	            Write( 1500, GainIndx.B, Mode.WidthGapInMS );   // B gap
	            for( int x = 0; x < Mode.Resolution.Width; x++ ) {
		            Write( ColorToFreq(_rgRGBCache[x].Blue ), GainIndx.B, dbTimePerPixel );
	            }

	            Write( 1500, Mode.WidthGapInMS );
            } catch( Exception oEx ) {
                Type[] rgErrors = { typeof( AccessViolationException ),
                                    typeof( NullReferenceException ),
                                    typeof( ArgumentOutOfRangeException ) };
                if( !rgErrors.Contains( oEx.GetType() ) )
                    throw;

                // This would be a good place to return an error.
            }
        }
    }

    /// <summary>
    /// MRT1, Martin 1, 2, 3, 4
    /// </summary>
    /// <remarks>Historical note. Martin was invented after Scottie.</remarks>
    public class GenerateMartin : SSTVGenerator {
        /// <exception cref="ArgumentOutOfRangeException" />
        public GenerateMartin( SKBitmap oBitmap, IPgModulator oModulator, SSTVMode oMode ) : 
            base( oBitmap, oModulator, oMode )
        {
        }

        /// <summary>
        /// TMmsstv::LineMRT, derived.
        /// </summary>
        /// <param name="iLine">The bitmap line to output.</param>
        /// <returns>How many samples written.</returns>
        /// <remarks>I'm not sure how important it is to cache the line from the bitmap.
        /// The original code does this. Saves a multiply I would guess.</remarks>
        protected override void WriteLine( int iLine ) {
	        double dbTimePerPixel = Mode.WidthColorInMS / Mode.Resolution.Width; 

            if( iLine > Height )
                return;

            try {
                _rgRGBCache.Clear();

	            Write( 1200, 4.862 );               // HSync on each line.

	            Write( 1500, GainIndx.G, 0.572 );   // G gap
	            for( int x = 0; x < Mode.Resolution.Width; x++ ) {     
                    _rgRGBCache.Add( GetPixel( x, iLine ) );
		            Write( ColorToFreq(_rgRGBCache[x].Green), GainIndx.G, dbTimePerPixel );
	            }

	            Write( 1500, GainIndx.B, 0.572 );   // B gap
	            for( int x = 0; x < Mode.Resolution.Width; x++ ) {
		            Write( ColorToFreq(_rgRGBCache[x].Blue ), GainIndx.B, dbTimePerPixel );
	            }

	            Write( 1500, GainIndx.R, 0.572 );   // R gap
	            for( int x = 0; x < Mode.Resolution.Width; x++ ) {
		            Write( ColorToFreq(_rgRGBCache[x].Red  ), GainIndx.R, dbTimePerPixel );
	            }
	            Write( 1200, 0.0);                  // Just a check to see how many samples sent!
            } catch( Exception oEx ) {
                Type[] rgErrors = { typeof( AccessViolationException ),
                                    typeof( NullReferenceException ) };
                if( !rgErrors.Contains( oEx.GetType() ) )
                    throw;

                // This would be a good place to return an error.
            }
        }
    }

    public abstract class SSTVCrCbYGenerator : SSTVGenerator {
        protected readonly List<Chrominance8Bit> _rgChromaCache = new(800);

        protected struct Chrominance8Bit {
            public byte  Y;
            public byte RY;
            public byte BY;
        }

        protected SSTVCrCbYGenerator(SKBitmap oBitmap, IPgModulator oModulator, SSTVMode oMode) : 
            base(oBitmap, oModulator, oMode) 
        {
        }

        public static byte Limit256( double d ) {
	        if( d < 0   ) d =   0;
	        if( d > 255 ) d = 255;

	        return (byte)d;
        }

        protected static Chrominance8Bit GetRY( SKColor skColor ) {
            Chrominance8Bit crColor;
            /*
            These are the values that make up the table below us. (Don't delete this comment!)

	        Y  =  16.0 + (.003906 * (( 65.738 * R) + (129.057 * G) + ( 25.064 * B)));
	        RY = 128.0 + (.003906 * ((112.439 * R) + (-94.154 * G) + (-18.285 * B)));
	        BY = 128.0 + (.003906 * ((-37.945 * R) + (-74.494 * G) + (112.439 * B)));
            */
	        crColor.Y  = Limit256(  16.0 + ( 0.256773*skColor.Red + 0.504097*skColor.Green + 0.097900*skColor.Blue) );
	        crColor.RY = Limit256( 128.0 + ( 0.439187*skColor.Red - 0.367766*skColor.Green - 0.071421*skColor.Blue) );
	        crColor.BY = Limit256( 128.0 + (-0.148213*skColor.Red - 0.290974*skColor.Green + 0.439187*skColor.Blue) );

            return crColor;
        }

    }

    public class GenerateRobot422 : SSTVCrCbYGenerator {
        /// <summary>
        /// 422 generator (I think). One horzontal luminance for one horzontal pixel,
        /// Two pixels shared per color horzontal. 1/2 color rez.
        /// </summary>
        public GenerateRobot422( SKBitmap oBitmap, IPgModulator oModulator, SSTVMode oMode ) : 
            base( oBitmap, oModulator, oMode )
        {
        }

        /// <summary>
        /// I can't figure out MMSSTV's implementation of Robot 24 it thinks the
        /// image is 320 wide. BUT it actually sends/receives a 160 x 120 image.
        /// So we'll go with this implementation.
        /// </summary>
        /// <param name="iLine"></param>
        protected override void WriteLine( int iLine ) {
	        double dbTimePerPixel = Mode.WidthColorInMS / Mode.Resolution.Width; 

            if( iLine > Height )
                return;

            try {
                _rgChromaCache.Clear(); // Clear the chromance line cache not the RGB one!!

	            Write( 1200, Mode.WidthSyncInMS );
                Write( 1500, Mode.WidthGapInMS  );

	            for( int x = 0; x < Width; x++ ) {      // Y(odd)
                    SKColor         skPixel = GetPixel( x, iLine );
                    Chrominance8Bit crPixel = GetRY   ( skPixel );

                    _rgChromaCache.Add( crPixel );

		            Write( ColorToFreq( crPixel.Y       ), dbTimePerPixel );
	            }

	            Write( 1500, Mode.WidthSyncInMS/2 );    // sync
                Write( 1900, Mode.WidthGapInMS /2 );    // gap
	            for( int x = 0; x < Width; x += 2 ) {   // R-Y
		            Write( ColorToFreq( _rgChromaCache[x].RY ), dbTimePerPixel );
	            }

	            Write( 2300, Mode.WidthSyncInMS/2 );    // sync
                Write( 1900, Mode.WidthGapInMS /2 );    // gap
	            for( int x = 0; x < Width; x += 2 ) {   // B-Y
		            Write( ColorToFreq( _rgChromaCache[x].BY ), dbTimePerPixel );
	            }
            } catch( Exception oEx ) {
                Type[] rgErrors = { typeof( AccessViolationException ),
                                    typeof( NullReferenceException ),
                                    typeof( ArgumentOutOfRangeException ) };
                if( !rgErrors.Contains( oEx.GetType() ) )
                    throw;

                // This would be a good place to return an error.
            }
        }
    }

    public class GenerateRobot420 : SSTVCrCbYGenerator {
        /// <summary>
        /// 420 generator (sorta). One horzontal luminance for one horzontal pixel,
        /// Two pixels shared per color horzontal. 1/2 color rez. AND two pixels shared
        /// per vertical!
        /// </summary>
        public GenerateRobot420( SKBitmap oBitmap, IPgModulator oModulator, SSTVMode oMode ) : 
            base( oBitmap, oModulator, oMode )
        {
        }

        /// <summary>
        /// I can't figure out MMSSTV's implementation of Robot 24 it thinks the
        /// image is 320 wide. BUT it actually sends/receives a 160 x 120 image.
        /// So we'll go with this implementation.
        /// </summary>
        /// <param name="iLine"></param>
        protected override void WriteLine( int iLine ) {
	        double dbTimePerPixel = Mode.WidthColorInMS / Mode.Resolution.Width; 

            if( iLine > Height )
                return;

            try {
                _rgChromaCache.Clear(); // Clear the chromance line cache not the RGB one!!

	            Write( 1200, Mode.WidthSyncInMS );
                Write( 1500, Mode.WidthGapInMS  );

	            for( int x = 0; x < Width; x++ ) {      // Y(odd)
                    SKColor         skPixel = GetPixel( x, iLine );
                    Chrominance8Bit crPixel = GetRY   ( skPixel );

                    _rgChromaCache.Add( crPixel );

		            Write( ColorToFreq( crPixel.Y       ), dbTimePerPixel );
	            }

                if( (iLine & 1 ) == 0 ) { // Even lines R-Y
	                Write( 1500, Mode.WidthSyncInMS/2 );    // sync
                    Write( 1900, Mode.WidthGapInMS /2 );    // gap
	                for( int x = 0; x < Width; x += 2 ) {   // R-Y
		                Write( ColorToFreq( _rgChromaCache[x].RY ), dbTimePerPixel );
	                }
                } else {                  // Odd  lines B-Y
	                Write( 2300, Mode.WidthSyncInMS/2 );    // sync
                    Write( 1900, Mode.WidthGapInMS /2 );    // gap
	                for( int x = 0; x < Width; x += 2 ) {   // B-Y
		                Write( ColorToFreq( _rgChromaCache[x].BY ), dbTimePerPixel );
	                }
                }
            } catch( Exception oEx ) {
                Type[] rgErrors = { typeof( AccessViolationException ),
                                    typeof( NullReferenceException ),
                                    typeof( ArgumentOutOfRangeException ) };
                if( !rgErrors.Contains( oEx.GetType() ) )
                    throw;

                // This would be a good place to return an error.
            }
        }
    }

    /// <summary>
    /// This class generates the PD modes. 
    /// </summary>
    public class GeneratePD : SSTVCrCbYGenerator {
        /// <exception cref="ArgumentOutOfRangeException" />
        public GeneratePD( SKBitmap oBitmap, IPgModulator oModulator, SSTVMode oMode ) : 
            base( oBitmap, oModulator, oMode )
        {
        }

        /// <summary>
        /// TMmsstv::LinePD, derived.
        /// </summary>
        /// <param name="iLine">The bitmap line to output.</param>
        /// <returns>How many samples written.</returns>
        /// <remarks>Note that you MUST override the default Generator iterator since
        /// this WriteLine uses TWO lines!!</remarks>
        protected override void WriteLine( int iLine ) {
	        double dbTimePerPixel = Mode.WidthColorInMS / (double)Width;

            if( iLine > Height )
                return;

            try {
                _rgChromaCache.Clear(); // Clear this one not the RGB cache.
            
                Write( 1200, 20.000 ); // Sync
	            Write( 1500,  2.080 ); // Porch

	            for( int x = 0; x < Width; x++ ) {     // Y(odd)
                    SKColor         skPixel = GetPixel( x, iLine );
                    Chrominance8Bit crPixel = GetRY   ( skPixel );

                    _rgChromaCache.Add( crPixel );

		            Write( ColorToFreq( crPixel.Y       ), dbTimePerPixel );
	            }
	            for( int x = 0; x < Width; x++ ) {     // R-Y
		            Write( ColorToFreq( _rgChromaCache[x].RY ), dbTimePerPixel );
	            }
	            for( int x = 0; x < Width; x++ ) {     // B-Y
                    Write( ColorToFreq( _rgChromaCache[x].BY ), dbTimePerPixel );
	            }
            
                ++iLine;
	            for( int x = 0; x < Width; x++ ) {     // Y(even)
                    SKColor         skPixel = GetPixel( x, iLine );
                    Chrominance8Bit crPixel = GetRY   ( skPixel );

		            Write( ColorToFreq( crPixel.Y ), dbTimePerPixel );
	            }
            } catch( Exception oEx ) {
                Type[] rgErrors = { typeof( AccessViolationException ),
                                    typeof( NullReferenceException ) };
                if( !rgErrors.Contains( oEx.GetType() ) )
                    throw;

                // This would be a good place to return an error.
            }
        }

        public override IEnumerator<int> GetEnumerator() {
            int iHeight = ( Height % 2 != 0 ) ? Height - 1 : Height;

            if( iHeight < 0 )
                yield break;

            WriteVIS( Mode.VIS );
            yield return 0;

            // Might need to add the 16 scan line grey scale bar.
            // All the doc's mention it, but I didn't see MMSSTV code for it.

            for( _iLine = 0; _iLine < Height; _iLine+=2 ) {
                WriteLine( _iLine );
                yield return _iLine;
            }
        }
    } 

    public class GenerateBW : SSTVCrCbYGenerator {
        public GenerateBW( SKBitmap oBitmap, IPgModulator oModulator, SSTVMode oMode ) : 
            base( oBitmap, oModulator, oMode )
        {
        }

        protected override void WriteLine(int iLine) {
	        double dbTimePerPixel = Mode.WidthColorInMS / Mode.Resolution.Width; 

	        Write( 1200, Mode.WidthSyncInMS );
	        Write( 1500, Mode.WidthGapInMS  );

	        for( int x = 0; x < Mode.Resolution.Width; x++ ) { 
                SKColor         skPixel = GetPixel( x, iLine );
                Chrominance8Bit crPixel = GetRY   ( skPixel );

		        Write( ColorToFreq( crPixel.Y ), dbTimePerPixel );
	        }
        }
    }

    public class GenerateWWV : SSTVGenerator {
        public GenerateWWV( SKBitmap oBitmap, IPgModulator oModulator, SSTVMode oMode ) : 
            base( oBitmap, oModulator, oMode )
        {
        }

        public static IEnumerator<SSTVMode> GetModeEnumerator() {
 	        yield return new SSTVModeBW( 0x00, "WWV", 1000, new SKSizeI( 320, 320 ), AllModes.smWWV ); 
        }

        protected override void WriteLine(int iLine) {
	        throw new NotImplementedException();
        }
    }

}