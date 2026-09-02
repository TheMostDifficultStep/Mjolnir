using Play.Drawing;
using Play.Interfaces.Embedding;

using SkiaSharp;

namespace Play.Spectrum {
    public class SpectrumAttrib {
        public bool _fFlash;
        public bool _fBright;
        public byte _bInk;
        public byte _bPaper;

        public SpectrumAttrib( byte iAttr ) {
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

    public class ScreenBlock {
        public ScreenBlock( int iImg, SpectrumAttrib oAttr ) {
            Img  = iImg;
            Attr = oAttr;
        }

        public SpectrumAttrib Attr { get; set; }
        public int       Img  { get; set; }
    }
    public class SpectrumGraphics :
        DocSurfaceBase,
        IPgLoad
    {
        public ScreenBlock[,] Screen  { get; } // Speccy 32,24 ascii display.
        public SKImage     [] Images  { get; } // Our constructed UDG's
        public SpectrumAttrib Attr    { get;set; } = new SpectrumAttrib(0);
        public bool           Over    { get; set; } = false;

        public SpectrumGraphics( IPgBaseSite oSite, string strMode ) : base( oSite ) {
            if( string.Compare( strMode, "std" ) != 0 ) {
                throw new ArgumentOutOfRangeException();
            }
            Screen  = new ScreenBlock  [32,24];
            Images  = new SKImage[256];
            Surface = SKSurface.Create( new SKImageInfo( 256, 192, SKColorType.Bgra8888 ) );
        }

        /// <summary>
        /// Fill out our screen black.
        /// </summary>
        public virtual bool InitNew() {
            SetGraphic2( 0, [0,0,0,0,0,0,0,0] ); // Gives us a solid block.

            for( int iY = 0; iY< Screen.GetLength(1); ++iY ) {
                for( int iX = 0; iX < Screen.GetLength(0); ++iX ) {
                    // Attribute 0 is black foreground and background.
                    Screen[iX, iY] = new ScreenBlock(0, new SpectrumAttrib( 0 ) );
                }
            }

            return true;
        }

        public void LogError( string strMessage ) {
            _oSiteBase.LogError( "Spectrum", strMessage );
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
                    byte bByteValue = ( bRow & 1<<(7-iX) ) == 0 ? (byte)0 : (byte)255; 

                    skBitmap.SetPixel( iX, iY, new SKColor( 0, 0, 0, bByteValue ));
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

            PutChrAt( iRow, iCol, (char)iUdg );
        }

        public void PutChrAt( int iRow, int iCol, char cChar ) {
            try {
                ScreenBlock oBlock = Screen[iCol, iRow];

                oBlock.Img  = cChar;
                oBlock.Attr = Attr;
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
        /// The image must be an Alpha8 with the bits 1 to show and the
        /// alpha channel 1 to show 0 not to show.
        /// </summary>
        /// <param name="oCanvas"></param>
        /// <param name="oPaint">Set color attribute you want for the image.</param>
        /// <param name="oRect">X, Y position of image. W&H s/b same as the image.</param>
        /// <param name="oImage">the image to draw.</param>
        protected virtual void DrawImage( 
            SKCanvas oCanvas, 
            SKPaint  oPaint, 
            SKRect   oRect, 
            SKImage  oImage
        ) {
            SKSamplingOptions oOptions = new SKSamplingOptions( SKFilterMode.Linear );

            // So XOR only works with alpha, which explains why my
            // Alpha8 bitmap works with this.
            oPaint .BlendMode = SKBlendMode.Xor;
            oCanvas.DrawImage( oImage, oRect.Left, oRect.Top, oOptions, oPaint );

            // So the BG is already the color we wanted, it get's XOR'd and
            // has a transparency set, then we draw our text colored rect...
            oPaint .BlendMode = SKBlendMode.DstOver;
            oCanvas.DrawRect( oRect, oPaint );
        }

        /// <summary>
        /// After you have loaded your UDG's, you may attempt
        /// to refresh and draw the screen.
        /// </summary>
        public void Refresh() {
            SKPaint  oPaint  = new SKPaint();
            SKCanvas oCanvas = Surface.Canvas;
            try {
                for( int iY = 0; iY<Screen.GetLength(1); ++iY ) {
                    for( int iX = 0; iX <Screen.GetLength(0); ++iX ) {
                        SKPoint pntLoc = new( iX*8, iY*8 );
                        ScreenBlock   oBlock = Screen[iX, iY];
                        SKImage oUdg   = Images[oBlock.Img];

                        if( oUdg is null ) {
                            oUdg = Images[0];
                        }
                        //Surface.Canvas.DrawImage( oUdg, pntLoc );

                        SKRect skRect  = new SKRect( pntLoc.X, pntLoc.Y, 
                                                     pntLoc.X + oUdg.Width, 
                                                     pntLoc.Y + oUdg.Height );
                        
                        // This sets our background image.
                        oPaint .BlendMode = SKBlendMode.Src;
                        oPaint .Color     = DecodeColor( oBlock.Attr._fBright, oBlock.Attr._bPaper );
                        oCanvas.DrawRect( skRect, oPaint );

                        // This does the XOR blit to display.
                        oPaint.Color = DecodeColor( oBlock.Attr._fBright, oBlock.Attr._bInk );
                        DrawImage( oCanvas, oPaint, skRect, oUdg );
                    }
                }
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
