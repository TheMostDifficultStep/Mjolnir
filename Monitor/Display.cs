using Play.Drawing;
using Play.Interfaces.Embedding;

using SkiaSharp;

namespace Monitor {
    public class DazzleDisplay :
        DocSurfaceBase
    {
        public enum ImageSizes {
            SixtyFour,
            ThirtyTwo
        }

        public DazzleDisplay( IPgBaseSite oSite ) : base( oSite ) { 
        }

        public int Address { get; set; } = 0x200;

        public bool InitNew() {
            return true;
        }

        public void SetSize( ImageSizes eSize ) {
            SKSizeI sSize = new SKSizeI();

            switch( eSize ) {
                case ImageSizes.SixtyFour:
                    sSize = new SKSizeI( 64, 64 );
                    break;
                case ImageSizes.ThirtyTwo:
                    sSize = new SKSizeI( 32, 32 );
                    break;
            }

            // world display is set to the new bitmap size.
            SKImageInfo oInfo = new SKImageInfo( sSize.Width, sSize.Height, SKColorType.Rgba8888, SKAlphaType.Opaque );
            Surface = SKSurface.Create( oInfo );
        }

        static SKColor[] _rgTest = { 
                SKColors.Red,
                SKColors.Blue,
                SKColors.Green,
                SKColors.Gray,
                SKColors.LightPink,
                SKColors.LightBlue,
                SKColors.LightGreen,
                SKColors.LightGray,
                SKColors.DarkRed,
                SKColors.DarkBlue,
                SKColors.DarkGreen,
                SKColors.DarkGray,
                SKColors.Yellow,
                SKColors.Orange,
                SKColors.Purple,
                SKColors.AliceBlue
        };


        // Decodes the original Cromemco Dazzler hardware color mapping

        private SKColor DecodeDazzlerColor(byte code)
        {
            // Bit 3 = Intensity (High/Low)
            // Bit 2 = Red, Bit 1 = Green, Bit 0 = Blue

            bool intensity = (code & 0x08) != 0;
            int r = ((code & 0x04) != 0) ? 1 : 0;
            int g = ((code & 0x02) != 0) ? 1 : 0;
            int b = ((code & 0x01) != 0) ? 1 : 0;

            // Apply multiplier based on intensity bit

            int mult = intensity ? 255 : 128;
            // Special case: true black when all RGB bits are 0

            if (r == 0 && g == 0 && b == 0) 
                return SKColors.Black;

            return GetColor(r * mult, g * mult, b * mult);
        }

        protected SKColor GetColor( int iR, int iG, int iB ) {
            return new SKColor( (byte)iR, (byte)iG, (byte)iB );
        }

        protected SKColor GetColor( int iIndex ) {
            if( iIndex >= 16 )
                return SKColors.White;
            if( iIndex < 0 )
                return SKColors.Black;

            return( _rgTest[iIndex] );
        }

        /// <summary>
        /// This shows the 4bit color display.
        /// </summary>
        /// <param name="rgMemory"></param>
        /// <param name="iStart"></param>
        public void Load( byte[] rgMemory ) {
            if( Surface == null )
                return;

            try {
                int iBmpHalfWidth = ImageSize.Width / 2;

                int a = Address;
                for( int y = 0; y < ImageSize.Height; y+=1 ) {
                    for( int x = 0; x < ImageSize.Width; x += 2 ) {
                        byte iLow  = (byte)(  rgMemory[a] & 0x0f );       // low  nibble.
                        byte iHigh = (byte)(( rgMemory[a] & 0xf0 ) >> 4); // high nibble.

                        Surface.Canvas.DrawPoint( x,   y, DecodeDazzlerColor( iLow  ) );
                        Surface.Canvas.DrawPoint( x+1, y, DecodeDazzlerColor( iHigh ) );

                        a++;
                    }
                }
                Raise_ImageUpdated();
            } catch( Exception oEx ) {
                Type[] rgErrors = { typeof( IndexOutOfRangeException ),
                                    typeof( ArgumentOutOfRangeException ),
                                    typeof( NullReferenceException ) };
                if( rgErrors.IsUnhandled( oEx ) )
                    throw;

                _oSiteBase.LogError( "Dazzle", "problem reading memory stream" );
            }
        }

        /// <summary>
        /// Clear the bitmap if one is in use. This might not have a valid
        /// image if we are not using the Dazzler.
        /// </summary>
        public void Clear() {
            if( Surface != null ) {
                SKPaint skPaint = new SKPaint();
                Surface.Canvas.DrawColor( SKColors.White );

                Raise_ImageUpdated();
            }
        }
    }
}
