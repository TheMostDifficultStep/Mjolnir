using SkiaSharp;

using Play.Interfaces.Embedding;
using Play.ImageViewer;

namespace Monitor {
    public class DazzleDisplay :
        ImageSoloDoc
    {
        public enum ImageSizes {
            SixtyFour,
            ThirtyTwo
        }

        public DazzleDisplay( IPgBaseSite oSite ) : base( oSite ) { 
        }

        public void SetSize( ImageSizes eSize ) {
            BitmapDispose();

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
            Bitmap = new SKBitmap( sSize.Width, sSize.Height, SKColorType.Rgba8888, SKAlphaType.Opaque );
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

        protected SKColor GetColor( int iIndex ) {
            if( iIndex >= 16 )
                return SKColors.White;
            if( iIndex < 0 )
                return SKColors.Black;

            return( _rgTest[iIndex] );
        }

        public void Load( byte[] rgMemory, int iStart ) {
            if( Bitmap == null )
                return;

            try {
                int iBmpHalfWidth = Bitmap.Width / 2;

                for( int y = 0; y < Bitmap.Height; y++ ) {
                    for( int x = 0; x < Bitmap.Width; x += 2 ) {
                        int iAddr = iStart + ( y * iBmpHalfWidth ) + x/2;
                        int iLow  = rgMemory[iAddr] & 0x0f;
                        int iHigh = rgMemory[iAddr] & 0xf0 >> 4;

                        Bitmap.SetPixel( x,   y, GetColor( iLow ) );
                        Bitmap.SetPixel( x+1, y, GetColor( iHigh ) );
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
    }
}
