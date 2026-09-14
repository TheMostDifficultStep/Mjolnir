using Play.Drawing;
using Play.Interfaces.Embedding;

using SkiaSharp;

namespace Play.Spectrum {
    public class Attribs {
        public bool _fFlash;
        public bool _fBright;
        public byte _bInk;
        public byte _bPaper;

        public Attribs( byte iAttr ) {
            Value = iAttr;
        }

        public byte Value { 
            set {
                _fFlash  = ( value & 0x80 ) > 0;
                _fBright = ( value & 0x40 ) > 0;
                _bPaper  = (byte)(( value & 0x38 ) >> 3 );
                _bInk    = (byte)(value & 0x7 );
            }

            // might be best to store the byte and break
            // it out when we set the value... :-/
            get {
                int iReturn = 0;

                if( _fFlash )
                    iReturn |= 0x80;
                if( _fBright )
                    iReturn |= 0x40;

                iReturn |= _bPaper << 3;
                iReturn |= _bInk;

                return (byte)iReturn;
            }
        }
    }

    /// <summary>
    /// In Skia, the SKBlendMode.Xor operation does not perform a bitwise XOR (^) 
    /// on the underlying pixel integers. Instead, it executes the classic 
    /// Porter-Duff XOR algebraic compositing operator. 
    /// [1] (https://stackoverflow.com/questions/8951679/drawing-with-xor-in-quartz), 
    /// [2] (https://groups.google.com/g/skia-discuss/c/EPLuQbg64Kc/m/2uDXFIGhAwAJ), 
    /// [3] (https://learn.microsoft.com/en-us/dotnet/api/skiasharp.skblendmode?view=skiasharp)
    /// For a BGRA_8888 color type surface, Skia internally abstracts the 
    /// 8-bit channels into standardized floating-point ranges from 0.0 to 1.0 
    /// and computes the blend using premultiplied alpha values. 
    /// [1] (https://skia.org/docs/user/api/skblendmode_overview/), 
    /// [2] (https://api.skia.org/SkBlendMode_8h.html), 
    /// [3] (https://skia.org/docs/user/color/)
    /// The Mathematical FormulaThe final color component (r) and final 
    /// alpha channel (ra) are determined by the following unified 
    /// Porter-Duff formula: [1] (https://api.skia.org/SkBlendMode_8h.html)
    /// r=s * (1-da)+d * (1-sa)
    /// Where:
    ///  s = Source color component (premultiplied Red, Green, or Blue)
    ///  d = Destination color component (premultiplied Red, Green, or Blue)
    /// sa = Source Alpha componentda = Destination Alpha component 
    /// [1] (https://api.skia.org/SkBlendMode_8h.html)
    /// </summary>

    public class SpectrumGraphics :
        DocSurfaceBase,
        IPgLoad
    {
        public Attribs[,] Attribs { get; } // Speccy 32,24 'ascii' display.
        public SKImage[]  Images  { get; } // Our constructed UDG's
        public Attribs    Attr    { get;set; } = new Attribs(0);
        public bool       Over    { get; set; } = false;
        public SKSurface  Mask    { get; protected set; }

        public SpectrumGraphics( IPgBaseSite oSite, string strMode ) : base( oSite ) {
            if( string.Compare( strMode, "std" ) != 0 ) {
                throw new ArgumentOutOfRangeException();
            }
            Attribs = new Attribs[32,24];
            Images  = new SKImage[256];
            Surface = SKSurface.Create( new SKImageInfo( 256, 192, SKColorType.Bgra8888 ) );
            Mask    = SKSurface.Create( new SKImageInfo( 256, 192, SKColorType.Alpha8 ) ); // was bgra8888
        }

        public void LogError( string strMessage ) {
            _oSiteBase.LogError( "Spectrum", strMessage );
        }

        /// <summary>
        /// Fill out our screen black.
        /// </summary>
        public virtual bool InitNew() {
            SetGraphic2( 0, [0,0,0,0,0,0,0,0] ); // Gives us a solid block.

            Clr();

            return true;
        }

        /// <summary>
        /// Assume that Image[0] is our standard solid black block.
        /// Give each screen element it's own attrib so changing one
        /// doesn't affect them all! ^_^;
        /// </summary>
        public void Clr() {
            for( int iY = 0; iY< Attribs.GetLength(1); ++iY ) {
                for( int iX = 0; iX < Attribs.GetLength(0); ++iX ) {
                    // Attribute 0 is black foreground and background.
                    Attribs[iX, iY] = new Attribs( 0 );
                }
            }
            SKPaint oPaint = new() { Color = SKColors.Black };
            Mask.Canvas.DrawRect( 0, 0, 256, 192, oPaint );
        }

        /// <summary>Create the Image that backs the Graphics 
        /// block. I'll set them in right at the index 
        /// and you as the programmer will set the UDG's 
        /// starting at 0x90 (UDG 'A')
        /// </summary>
        /// <remarks>
        /// In the future, I'll make a bulk loader so I can just
        /// create the Scratch surface during the load.
        /// I've depricated this code. But it might work with the
        /// new rendering code by set the color as 
        /// SKColor( FF, FF, FF, bByteValue );
        /// </remarks>
        /// <param name="i">Index to the image. Basically an ASCII offset.</param>
        public void SetBWGraphic( SKSurface oSurface, int i, byte[] rgUdg ) {
            ArgumentNullException.ThrowIfNull( rgUdg ); 

            for( int iY = 0; iY<8; ++iY ) {
                byte bRow = rgUdg[iY];
                for( int iX = 0; iX < 8; ++iX ) {
                    // Highest bit is the lowest X value...
                    SKColor sColor = ( bRow & 1<<(7-iX) ) > 0 ? SKColors.White : SKColors.Black;

                    Surface.Canvas.DrawPoint( iX, iY, sColor );
                }
            }
            Images[i] = oSurface.Snapshot();
        }

        /// <summary>
        /// This set's our 1 bit pixel image to a color display.
        /// </summary>
        /// <param name="i"></param>
        /// <param name="rgUdg"></param>
        public void SetGraphic2( int i, byte[] rgUdg ) {
            using SKBitmap skBitmap = new SKBitmap( 8, 8, SKColorType.Alpha8, SKAlphaType.Opaque );

            for( int iY = 0; iY<8; ++iY ) {
                byte bRow = rgUdg[iY];
                for( int iX = 0; iX < 8; ++iX ) {
                    // Highest bit is the lowest X value...
                    byte bAlpha = ( bRow & 1<<(7-iX) ) == 0 ? (byte)0 : (byte)255; 

                    skBitmap.SetPixel( iX, iY, new SKColor( 0, 0, 0, bAlpha ));
                }
            }
            Images[i] = SKImage.FromBitmap( skBitmap );
        }

        /// <summary>
        /// Set's a udg character on screen. 
        /// </summary>
        /// <param name="iRow"></param>
        /// <param name="iCol"></param>
        /// <param name="cUdg">Graphic block starting at 'A'</param>
        public void PutUDGAt( int iRow, int iCol, char cUdg ) {
            int iUdg = (byte)( (Int16)cUdg - 'A' + 0x90);

            PutChrOn( iRow, iCol, (char)iUdg );
        }

        public void PutChrOn( int iRow, int iCol, char cChar ) {
            try {
                SKRectI           sLoc     = new ( iCol*8, iRow*8, iCol*8+8, iRow*8+8);
                SKSamplingOptions oOptions = new SKSamplingOptions( SKFilterMode.Nearest );
                SKImage           oUdg     = Images[cChar];

                if( oUdg is null ) {
                    oUdg = Images[0];
                }
                Mask.Canvas.DrawImage( oUdg, sLoc, oOptions );

                Attribs[iCol, iRow] = Attr;
            } catch( Exception oEx ) {
                Type[] rgErrors = [ 
                    typeof( NullReferenceException ),
                    typeof( IndexOutOfRangeException ) ];

                if( rgErrors.IsUnhandled( oEx ) )
                    throw;

                LogError( "Couldn't set UDG on screen." );
            }
        }

        protected SKColor GetColor( int iR, int iG, int iB ) {
            return new SKColor( (byte)iR, (byte)iG, (byte)iB );
        }

        /// <summary>
        /// The spectrum zx is a GRB 1 bit color palette!
        /// </summary>
        /// <param name="iCode">0-7</param>
        private SKColor DecodeColor( bool iIntensity, byte iCode)
        {
            int iMono  = iIntensity ? 0xFF : 0xCD; // Either bright or normal.

            int g = ( ( iCode & 0x04 ) != 0 ) ? iMono : 0;
            int r = ( ( iCode & 0x02 ) != 0 ) ? iMono : 0;
            int b = ( ( iCode & 0x01 ) != 0 ) ? iMono : 0;

            // Special case: true black when all RGB bits are 0
            if (r == 0 && g == 0 && b == 0) 
                return SKColors.Black;

            return GetColor( r, g, b );
        }

        /// <summary>
        /// So now we have a true "1 bit" display with backing 32x24 attribs.
        /// We blit in 8x8 chunks so we'll get proper color clash! ^_^;;
        /// </summary>
        public void Refresh() {
            SKPaint           oPaint   = new SKPaint();
            SKCanvas          oCanvas  = Surface.Canvas;
            SKImage           oMask    = Mask.Snapshot();
            SKSamplingOptions oOptions = new SKSamplingOptions( SKFilterMode.Nearest );

            try {
                for( int iY = 0; iY<Attribs.GetLength(1); ++iY ) {
                    for( int iX = 0; iX <Attribs.GetLength(0); ++iX ) {
                        SKPoint pntLoc = new( iX*8, iY*8 );
                        Attribs oAttr  = Attribs[iX, iY];
                        SKRect skRect  = new SKRect( pntLoc.X, pntLoc.Y, 
                                                     pntLoc.X + 8,
                                                     pntLoc.Y + 8 );
                        
                        // This sets our background image.
                        oPaint .BlendMode = SKBlendMode.Src;
                        oPaint .Color     = DecodeColor( oAttr._fBright, oAttr._bPaper );
                        oCanvas.DrawRect( skRect, oPaint );

                        // So see the new class summary.
                        oPaint .BlendMode = SKBlendMode.Xor; 
                        oPaint .Color     = DecodeColor( oAttr._fBright, oAttr._bInk );
                        oCanvas.DrawImage( oMask, skRect, skRect, oOptions, oPaint );

                        // So the BG is already the color we wanted, it get's XOR'd and
                        // has a transparency set, then we draw our text colored rect...
                        oPaint.BlendMode = SKBlendMode.DstOver;
                        oCanvas.DrawRect(skRect, oPaint);
                    }
                }
                // this is a heavy duty call when just the bits change but
                // not the size or any other attribute.
                Raise_ImageUpdated();
            } catch( Exception oEx ) {
                Type[] rgErrors = [ 
                    typeof( NullReferenceException ),
                    typeof( IndexOutOfRangeException ) ];

                if( rgErrors.IsUnhandled( oEx ) )
                    throw;

                LogError( "Couldn't Refresh Screen." );
            }
        }
    }

}
