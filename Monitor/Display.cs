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

        protected SKColor GetColor( int iR, int iG, int iB ) {
            return new SKColor( (byte)iR, (byte)iG, (byte)iB );
        }

        public void GenerateTestPattern( byte[] rgMemory ) {
            if( !IsImageValid ) {
                _oSiteBase.LogError( "Dazzle Display", "Initialize Board First" );
                return;
            }

            int iHalf = ImageSize.Width / 2;
            int iSize = iHalf * ImageSize.Height;
            int iTop  = Address + iSize;

            for( int y = 0; y < ImageSize.Height; ++y ) {
                for( int x = 0; x < iHalf; x += 1 ) {
                    int iAddr = x + ( y * iHalf ) + Address;
                    int iMod8 = x % 8;

                    byte lowNibble  = (byte)iMod8;          
                    byte highNibble = (byte)((lowNibble | 0x08) << 4 );    

                    rgMemory[iAddr] = (byte)(highNibble | lowNibble);
                }
            }
        }

        /// <summary>
        /// Decodes the original Cromemco Dazzler hardware color mapping
        /// </summary>
        protected virtual SKColor DecodeDazzlerColor(byte code)
        {
            // Bit 3 = Intensity (High/Low)
            // Bit 2 = Red, Bit 1 = Green, Bit 0 = Blue

            bool intensity =  (code & 0x08) != 0;
            int  r         = ((code & 0x04) != 0) ? 1 : 0;
            int  g         = ((code & 0x02) != 0) ? 1 : 0;
            int  b         = ((code & 0x01) != 0) ? 1 : 0;

            // Apply multiplier based on intensity bit
            int mult = intensity ? 255 : 128;

            // Special case: true black when all RGB bits are 0
            if (r == 0 && g == 0 && b == 0) 
                return SKColors.Black;

            return GetColor(r * mult, g * mult, b * mult);
        }


        public virtual void Load( byte[] rgMemory ) {
            if( Surface == null )
                return;

            try {
                for( int iQuad = 0; iQuad < 4; iQuad+=1 ) {
                    LoadQuad( rgMemory, iQuad );
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

        readonly SKPointI[] _rgStarts = 
            [ new SKPointI(  0,  0 ),
              new SKPointI( 32,  0 ),
              new SKPointI(  0, 32 ),
              new SKPointI( 32, 32 ) ];

        /// <summary>
        /// Dazzler has a strange memory map. 
        /// </summary>
        /// <param name="rgMemory">Our flat memory space.</param>
        /// <param name="iQuad">Which quad to paint.</param>
        protected void LoadQuad( byte[] rgMemory, int iQuad ) {
            try {
                // q would be 0x80 or 128 for 32x32
                int q = 0x200; // quad size. 512, hard coded for 64x64.
                int a = Address + iQuad * q; 
                int y = _rgStarts[iQuad].Y;
                for( int j = 0; j < ImageSize.Height / 2; ++j ) {
                    int x = _rgStarts[iQuad].X;
                    for( int i = 0; i < ImageSize.Width / 2; i += 2 ) {
                        byte iLow  = (byte)(  rgMemory[a] & 0x0f );       // low  nibble.
                        byte iHigh = (byte)(( rgMemory[a] & 0xf0 ) >> 4); // high nibble.

                        Surface.Canvas.DrawPoint( x+i,   y+j, DecodeDazzlerColor( iLow  ) );
                        Surface.Canvas.DrawPoint( x+i+1, y+j, DecodeDazzlerColor( iHigh ) );

                        a++;
                    }
                }
            } catch( Exception oEx ) {
                Type[] rgErrors = { typeof( IndexOutOfRangeException ),
                                    typeof( ArgumentOutOfRangeException ),
                                    typeof( NullReferenceException ) };
                if( rgErrors.IsUnhandled( oEx ) )
                    throw;

                _oSiteBase.LogError( "Dazzle", "problem reading quad" );
            }
        }

        /// <summary>
        /// Clear the bitmap if one is in use. This might not have a valid
        /// image if we are not using the Dazzler.
        /// </summary>
        public void Clear() {
            if( Surface != null ) {
                Surface.Canvas.DrawColor( SKColors.Black );

                Raise_ImageUpdated();
            }
        }
    } // End method

    public class FlatDazzler : DazzleDisplay {
        public FlatDazzler(IPgBaseSite oSite) : base(oSite) {
        }
        protected override SKColor DecodeDazzlerColor(byte code)
        {
            if ( code == 0 ) 
                return SKColors.Black;

            return GetColor(255, 255, 255);
        }

        /// <summary>
        /// I'm going to pretend we have an 8 bit per pixel display.
        /// </summary>
        /// <param name="rgMemory">Raw memory starting at zero.</param>
        public override void Load( byte[] rgMemory ) {
            if( Surface == null )
                return;

            try {
                int a = Address;
                for( int y = 0; y < ImageSize.Height; ++y ) {
                    for( int x = 0; x < ImageSize.Width; ++x ) {
                        Surface.Canvas.DrawPoint( x, y, DecodeDazzlerColor( rgMemory[a]  ) );

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

    }
}
