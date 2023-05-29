using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

using SkiaSharp;

using Play.Interfaces.Embedding;
using Play.Edit;

namespace Mjolnir {
    // Copied from freetype.h if it changes, we're boned.
    public enum FT_Render_Mode : ushort {
        FT_RENDER_MODE_NORMAL = 0,
        FT_RENDER_MODE_LIGHT,
        FT_RENDER_MODE_MONO,
        FT_RENDER_MODE_LCD,
        FT_RENDER_MODE_LCD_V,

        FT_RENDER_MODE_MAX
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    public struct FTGlyphBmp {
        public ushort  rows;
        public ushort  width;
        public short   pitch;
        public ushort  num_grays;
        public byte    pixel_mode;
        public IntPtr  bits;
    }

    unsafe public class FreeType2API {
        const string _dllLocation = @"FontManager.dll";

        [DllImport( _dllLocation, EntryPoint = "PG_FreeType_Init", SetLastError = true )]
        public static extern int PG_FreeType_Init( IntPtr * pHandle );
        [DllImport( _dllLocation, EntryPoint = "PG_FreeType_Done", SetLastError = true )]
        public static extern int PG_FreeType_Done( IntPtr hFreeType);

        [DllImport( _dllLocation, EntryPoint = "PG_Face_New", SetLastError = true )]
        public static extern int PG_Face_New( IntPtr library, [In]byte* filepathname, IntPtr* aface );
        [DllImport( _dllLocation, EntryPoint = "PG_Face_Done", SetLastError = true )]
        public static extern int PG_Face_Done( IntPtr ipFace );

        [DllImport( _dllLocation, EntryPoint = "PG_Face_SetCharSize", SetLastError = true )]
        public static extern int PG_Face_SetCharSize( IntPtr face,
                                                      UInt64 char_width,
                                                      UInt64 char_height,
                                                      uint   horz_resolution,
                                                      uint   vert_resolution );

        [DllImport( _dllLocation, EntryPoint = "PG_Set_Pixel_Sizes", SetLastError = true )]
        public static extern int PG_Set_Pixel_Sizes( IntPtr face, uint x, uint y );

        [DllImport( _dllLocation, EntryPoint = "PG_Face_GetCharIndex", SetLastError = true )]
        public static extern uint PG_Face_GetCharIndex( IntPtr face, UInt32 uiCodePoint );

        [DllImport( _dllLocation, EntryPoint = "PG_Face_GenerateGlyph", SetLastError = true )]
        public static extern int PG_Face_GenerateGlyph( IntPtr face, FT_Render_Mode uiMode, UInt32 uiGlyphIndex );

        [DllImport( _dllLocation, EntryPoint = "PG_Face_CurrentGlyphMapData", SetLastError = true )]
        public static extern int PG_Face_CurrentGlyphMapData( IntPtr face, FTGlyphPos * pGlyphPos, FTGlyphBmp * pGlyphBmp );
    }

    public class GlyphInfo : IPgGlyph {
        public uint FaceIndex { get; }
        public uint Glyph     { get; }

        public SKBitmap   Image       { get; }
        public PgGlyphPos Coordinates { get; }
        public UInt32     CodePoint   { get; }
        public int        CodeLength  { get; set; }

        /// <summary>
        /// Under FreeType, scaled pixel positions are all expressed in the 26.6 fixed 
        /// float format (made of a 26-bit integer mantissa, and a 6-bit fractional part). 
        /// In other words, all coordinates are MULTIPLIED by 64. The grid lines along 
        /// the integer pixel positions, are multiples of 64, like (0,0), (64,0), (0,64), 
        /// (128,128), etc., while the pixel centers lie at middle coordinates 
        /// (32 modulo 64) like (32,32), (96,32), etc.   
        /// I've seen references to EM's where are simple integers, but have not encountered
        /// them so I don't know the context I might have to convert them. But it will happen
        /// here somehow.
        /// </summary>
        public GlyphInfo( uint uiFaceIndex, uint uiCodePoint, uint uiGlyph, SKBitmap skGlyphBmp, FTGlyphPos ftGlyphPos )
        {
            FaceIndex   = uiFaceIndex;
            Glyph       = uiGlyph;
            CodePoint   = uiCodePoint;
            Image       = skGlyphBmp;
            CodeLength  = 1; // default set UTF32 length. 1 32 bit value.

            PgGlyphPos sTranslate;

            sTranslate.left      = ftGlyphPos.left;
            sTranslate.top       = ftGlyphPos.top;
            sTranslate.advance_x = ftGlyphPos.advance_em_x / 64; // Convert the FT_Pos EM 26.6 to a float.
            sTranslate.advance_y = ftGlyphPos.advance_em_y / 64;
            sTranslate.delta_lsb = ftGlyphPos.delta_lsb    / 64;
            sTranslate.delta_rsb = ftGlyphPos.delta_rsb    / 64;

            Coordinates = sTranslate;
        }
    }

    public class FTFace {
        static Type[] rgErrors = { typeof( EntryPointNotFoundException ),
                                   typeof( BadImageFormatException ),
                                   typeof( ArgumentOutOfRangeException ),
                                   typeof( ArgumentNullException ),
                                   typeof( NullReferenceException ),
                                   typeof( IndexOutOfRangeException ),
                                   typeof( ArgumentOutOfRangeException ), 
                                   typeof( ApplicationException ) };
        public IntPtr Handle   { get; }
        public string FilePath { get; }
        public int    ID       { get; }
        public uint   CurrentHeight { get; protected set; } // Current glyph generation setting.
        static byte[] GammaTable = new byte[256];

        static FTFace() {
            for( int i=0; i<GammaTable.Length; ++i ) {
                GammaTable[i] = GammaEncode( (byte)i );
            }
        }

        /// <param name="ipFace">Int pointer to a unmanaged freetype font face.</param>
        /// <param name="rgFilePath">utf8 path to file. Back to the future!</param>
        /// <param name="uiID">ID for this element.</param>
        public FTFace( IntPtr ipFace, string strFilePath, int uiID ) {
            if( ipFace == IntPtr.Zero )
                throw new ArgumentNullException( "Face Handle must not be null." );
            Handle   = ipFace;
            FilePath = strFilePath ?? throw new ArgumentNullException( "Face filepath must not be null." );
            ID       = uiID;
        }

        public static double GammaValue { get; } = 1.7;

        public static byte GammaEncode( byte i ) { 
            double d      = i/(double)255;
            double r      = Math.Pow( d, 1/GammaValue );
            byte   result = (byte)Math.Round( r * 255);

            return result;
        }

        public static byte GammaDecode( byte i ) {
            double d      = i/(double)255;
            double r      = Math.Pow( d, GammaValue );
            byte   result = (byte)Math.Round( r * 255);

            return result;
        }

        public void SetSize( uint uiSizeInPoints, SKPoint szDeviceRes ) { 
            int iError = 0;
            try {
                uint uiEmSizeY = uiSizeInPoints * 64;
                iError = FreeType2API.PG_Face_SetCharSize(
                    Handle,
                    0,         /* char_width  in 1/64th of points. Zero means square */
                    uiEmSizeY, /* char_height in 1/64th of points. 26.6 fractional points. */
                    (uint)szDeviceRes.X,
                    (uint)szDeviceRes.Y);
                // iError = FreeType2API.PG_Set_Pixel_Sizes( Handle, 0, uiSize );
            } catch( Exception oEx ) {
                if( rgErrors.IsUnhandled( oEx ) )
                    throw;
            }
            if( iError != 0 )
                throw new ApplicationException( "Couldn't set FontFace Size" );
            CurrentHeight = uiSizeInPoints;
        }

        public uint GlyphFromCodePoint( uint uiCodePoint ) {
            try {
                return FreeType2API.PG_Face_GetCharIndex( Handle, uiCodePoint );
            } catch( Exception oEx ) {
                if( rgErrors.IsUnhandled( oEx ) )
                    throw;
                return 0;
            }
        }

        public void GlyphGenerate( FT_Render_Mode uiMode, uint uiGlyphIndex ) {
            int iError = 0;
            try {
                iError = FreeType2API.PG_Face_GenerateGlyph( Handle, uiMode, uiGlyphIndex );
            } catch( Exception oEx ) {
                if( rgErrors.IsUnhandled( oEx ) )
                    throw;
            }
            if( iError != 0 )
                throw new ApplicationException( "Couldn't generate the glyph from the given index: " + iError.ToString() );
        }

        public bool GlyphSampleCurrent( ref FTGlyphPos ftCoords, ref SKSizeI skBmpSize ) {
            unsafe {
                int iError = 0;
                try {
                    FTGlyphBmp ftBitmap;
                    fixed( FTGlyphPos * pCoords = &ftCoords ) {
                        iError = FreeType2API.PG_Face_CurrentGlyphMapData( Handle, pCoords, &ftBitmap );
                        skBmpSize = new SKSizeI( ftBitmap.width, ftBitmap.rows );
                    }
                    return true;
                } catch( Exception oEx ) {
                    if( rgErrors.IsUnhandled( oEx ) )
                        throw;
                    return false;
                }
            }
        }
        /// <summary>
        /// First generate the glyph, then call this function to retrieve it.
        /// </summary>
        /// <exception cref="ApplicationException"></exception>
        protected SKBitmap GlyphCopyCurrent( out FTGlyphPos ftCoords ) {
            unsafe {
                int iError = 0;
                try {
                    FTGlyphBmp ftBitmap;
                    fixed( FTGlyphPos * pCoords = &ftCoords ) {
                        iError = FreeType2API.PG_Face_CurrentGlyphMapData( Handle, pCoords, &ftBitmap );
                    }

                    if( iError == 0 ) {
                      //SKBitmap skBitmap = new SKBitmap( ftBitmap.pitch, ftBitmap.rows, SKColorType.Gray8, SKAlphaType.Opaque );
                      //SKBitmap skBitmap = new SKBitmap( ftBitmap.pitch, ftBitmap.rows, SKColorType.Rgba8888, SKAlphaType.Unpremul );
                        SKBitmap skBitmap = new SKBitmap( ftBitmap.width, ftBitmap.rows, SKColorType.Alpha8, SKAlphaType.Opaque );
                        IntPtr   ipPixels = skBitmap.GetPixels();

                        byte* pPixel = (byte*)ftBitmap.bits.ToPointer();

                        switch( ftBitmap.pixel_mode ) {
                            case 2:
                                for( int iY = 0; iY < skBitmap.Height; ++iY ) {
                                    byte* pRow = pPixel + (ftBitmap.pitch * iY);
                                    for( int iX = 0; iX < skBitmap.Width; ++iX ) {
                                        byte bAlpha    = *(pRow + iX);
                                      //byte bNotAlpha = (byte)~bAlpha; // reversed value.
                                        byte bNotAlpha = (byte)(255 - bAlpha);

                                        //byte bGammaCorrectNa  = GammaTable[bNotAlpha];
                                        //byte bGammaCorrectNa2 = (byte)~GammaTable[bAlpha];

                                        skBitmap.SetPixel( iX, iY, new SKColor( 255,255,255, bAlpha ));
                                    }
                                }
                                break;

                            case 1:
                                // Monochrome on bit per pixel! but promoted to 8 bit mono.
                                for( int iY = 0; iY < skBitmap.Height; ++iY ) {
                                    byte* pRow = pPixel + (ftBitmap.pitch * iY);
                                    for( int iX = 0; iX < skBitmap.Width; ++iX ) {
                                        int  iByteIndex = iX / 8;
                                        int  iBitIndex  = 7 - ( iX % 8 ); // since we are a "bit" map.
                                        bool fBitValue  = (pRow[iByteIndex] & (1 << iBitIndex)) != 0;
                                        byte bByteValue = fBitValue ? (byte)0 : (byte)255; // Reverse the value.

                                        skBitmap.SetPixel( iX, iY, new SKColor( 0, 0, 0, bByteValue ));
                                    }
                                }
                                break;
                        }

                        //Buffer.MemoryCopy(ftBitmap.bits.ToPointer(),
                        //                   ipPixels.ToPointer(),
                        //                   skBitmap.ByteCount,            // Is this radical or what!
                        //                   ftBitmap.rows * ftBitmap.pitch // This isn't shabby either. 
                        //                 );
                        return skBitmap;
                    }
                } catch( Exception oEx ) {
                    if( rgErrors.IsUnhandled( oEx ) )
                        throw;
                }
                throw new ApplicationException( "Couldn't get current gen'd glyph: " + iError.ToString() );
            }
        }
        /// <summary>Call SetSize first to set the going size of the glyphs being generated.</summary>
        /// <seealso cref="SetSize" />
        /// <remarks>I'm hacking the tab character here. If I allowed the glyph coordinates to be 
        /// modified I might be able to get around that, but I like them readonly.</remarks>
        /// <param name="iDescender">This is a slightly hacky way for us to align the free type font
        /// with the containing box in my simple system which does not deal with ascenders and
        /// descenders. We'll just push the top of the gliph up by our best calculation of the
        /// descender for the font (face @ particular size) in question.</param>
        /// <exception cref="ApplicationException" />
        public IPgGlyph GlyphLoad( uint uiCode, short iDescender ) {
            if( CurrentHeight == 0 )
                throw new ArgumentException( "Glyph height has not be set." );

            uint uiGlyph = GlyphFromCodePoint(uiCode == 9 ? 32 : uiCode );
            
            try {
                GlyphGenerate( FT_Render_Mode.FT_RENDER_MODE_NORMAL, uiGlyph );
                SKBitmap skGlyphBitmap = GlyphCopyCurrent( out FTGlyphPos oGlyphCoords );

                if( uiCode == 9 )
                    oGlyphCoords.advance_em_x *= 4;

                oGlyphCoords.top += iDescender;

                return new GlyphInfo( (uint)ID, uiCode, uiGlyph, skGlyphBitmap, oGlyphCoords );
            } catch( Exception oEx ) {
                if( rgErrors.IsUnhandled( oEx ) )
                    throw;

                throw new ApplicationException( "Problem Creating & Rendering Font Face.", oEx );
            }
        }
    }


    /// <summary>
    /// Render a font at the given height from the given face. HOWEVER, we will
    /// also perform a font fall back looking for a alternate font that is of the same size
    /// if the current font does not support the character (square box). That font character
    /// will be stored in the corresponding FontRenderer for it.
    /// </summary>
    /// <remarks>I used to use a stub seperately which implemented the IPgFontRenderer to
    /// provide font fallback. But it really didn't do much that this object could not
    /// do if I simply give it a pointer to the face manager.</remarks>
    public class FontRender : IPgFontRender {
        public FTManager Manager    { get; }
        public FTFace    Face       { get; }
        public SKPoint   Resolution { get; }
        public uint      ID         { get; }
        public uint      FontHeight { get; }          // Raw height from the request.
        public uint      LineHeight => _uiLineHeight; // Assender and decender compensated height.

        public short Ascender  { get; protected set; } = 0;
        public short Descender { get; protected set; } = 0;

        readonly protected List<IPgGlyph> _rgRendered = new List<IPgGlyph>( 50 );

        uint _uiLineHeight = 0;

        public FontRender( FTManager oManager, FTFace oFace, SKPoint sResolution, uint uiHeight, uint uiID ) {
            Manager = oManager ?? throw new ArgumentNullException( "Font manager must not be null." );;
            Face    = oFace    ?? throw new ArgumentNullException( "Font face must not be null." );

            FontHeight    = uiHeight;
            _uiLineHeight = uiHeight;
            Resolution    = sResolution;
            ID            = uiID;

            oFace.SetSize( uiHeight, sResolution);
        }

        /// <summary>Call this function to get a better guestimate of the actual
        /// height of the font. This will help us center the line better.</summary>
        /// <exception cref="ApplicationException"
        public void InitNew() {
            string  strRef   = "aghijklpqrxyzAQRST10,/[]{}";
            SKSizeI skSize   = new SKSizeI();

            foreach( char c in strRef ) {
                uint uiGlyph = Face.GlyphFromCodePoint( c );
                FTGlyphPos ftCoords = new FTGlyphPos();

                Face.GlyphGenerate( FT_Render_Mode.FT_RENDER_MODE_NORMAL, uiGlyph );

                if( Face.GlyphSampleCurrent( ref ftCoords, ref skSize ) ) {
                    int iDesc = skSize.Height - ftCoords.top;
                    int iAsc  = ftCoords.top;

                    if( iDesc > Descender )
                        Descender = (short)iDesc;
                    if( iAsc > Ascender )
                        Ascender = (short)iAsc;
                }
            }

            _uiLineHeight = (uint)( Ascender + Descender );
        }

        /// <summary>
        /// If the element has been generated already, we return that glyph. 
        /// Any other instances will have the same coordinate information. 
        /// But of course a shaper might want different coordinates set
        /// for that glyph depending on it's neighbors. You'll have to cache 
        /// those coordinates elsewhere.
        /// </summary>
        /// <returns>True if glyph matches code, False if box return. Always
        /// returns a font character even if it is the box character.</returns>
        /// <exception cref="ApplicationException" />
        /// <remarks>This is an internal function for the manager. You don't
        /// get font fall back here.</remarks>
        internal bool GlyphLoad( uint uiCode, out IPgGlyph oGlyph ) {
            // First see if we've already rendered one.
            foreach( IPgGlyph oTry in _rgRendered ) {
                if( oTry.CodePoint == uiCode) {
                    oGlyph = oTry;
                    return oTry.Glyph != 0;
                }
            }

            // Got to try a new render.
            Face.SetSize( FontHeight, Resolution );

            oGlyph = Face.GlyphLoad( uiCode, Descender );

            _rgRendered.Add( oGlyph );

            // got something, it is zero if it is a box.
            return oGlyph.Glyph != 0;
        }

        public IPgGlyph GetGlyph(uint uiCodePoint) {
            return Manager.GlyphFrom( this, uiCodePoint );
        }
    }

    public class FTManager : IDisposable {
        static Type[] _rgErrors = { typeof( EntryPointNotFoundException ),
                                    typeof( BadImageFormatException ),
                                    typeof( ArgumentOutOfRangeException ),
                                    typeof( ArgumentNullException ),
                                    typeof( NullReferenceException ) };

        private   bool                      _fDisposedValue;
        protected readonly List<FTFace>     _rgFace    = new List<FTFace>();
        protected readonly List<FontRender> _rgRenders = new List<FontRender>();
		public    IntPtr                    Handle { get; } // Main handle from the FreeType Init.

        public FTManager() {
			unsafe {
				IntPtr ipHandle = IntPtr.Zero;
                int    iError = 0;
                try {
                    iError = FreeType2API.PG_FreeType_Init( &ipHandle );
                } catch( Exception oEx ) {
                    if( _rgErrors.IsUnhandled( oEx ) )
                        throw;
                }
				if( iError != 0 )
                    throw new ApplicationException( "Cannot initialized Font System" );

				Handle = ipHandle;
			}
		}

		protected virtual void DisposeNative() {
            try {
			    FreeType2API.PG_FreeType_Done( Handle );
            } catch( Exception oEx ) {
                if( _rgErrors.IsUnhandled( oEx ) )
                    throw;
            }
		}

        protected virtual void DisposeFace( FTFace oFace ) {
            int iError = 0;
            try {
                iError = FreeType2API.PG_Face_Done( oFace.Handle );
            } catch( Exception oEx ) {
                if( _rgErrors.IsUnhandled( oEx ) )
                    throw;
            }
            if( iError != 0 )
                throw new ApplicationException( "Couldn't dispose current font: " + iError.ToString() );
        }

        protected virtual void Dispose( bool disposing ) {
            if (!_fDisposedValue) {
                if (disposing) {
                    // TODO: dispose managed state (managed objects)
                    foreach( FTFace oFace in _rgFace ) 
                        DisposeFace( oFace );
                }

				DisposeNative();
                _fDisposedValue = true;
            }
        }

        /// <summary>For the given face, cache a font for the given height and resolution.</summary>
        /// <exception cref="ArgumentOutOfRangeException" />
        public uint FaceCacheSize( ushort uiFace, uint uiHeightInPoints, SKPoint skResolution ) {
            // Try find the font if it has already been cached.
            foreach( FontRender oRenderTry in _rgRenders ) {
                if( oRenderTry.Face.ID    == uiFace &&
                    oRenderTry.FontHeight == uiHeightInPoints &&
                    oRenderTry.Resolution == skResolution )  
                {
                    return oRenderTry.ID;
                }
            }

            // Didn't find it so create a renderer for the new size/resolution.
            FontRender oRender = new FontRender( this, _rgFace[uiFace], skResolution, uiHeightInPoints, (uint)_rgRenders.Count );
            oRender.InitNew();

            _rgRenders.Add( oRender );

            return( oRender.ID );
        }

        /// <summary>
        /// Notify the manager what font you want to use. The font will be cached so repeat
        /// calls will returned the cached face id value.
        /// </summary>
        /// <param name="strPath">path to the font to use.</param>
        /// <returns>a FaceID. use this value to get a font of the proper size.</returns>
        /// <exception cref="ApplicationException" />
        public UInt16 FaceCache( string strPath ) {
            int    iError     = 0;
            IntPtr ipFace     = IntPtr.Zero;
            byte[] rgFileName = null;

            foreach( FTFace oLook in _rgFace ) {
                if( String.Compare( oLook.FilePath, strPath ) == 0 ) {
                    return (UInt16)oLook.ID;
                }
            }

            unsafe {
                try {
                    rgFileName = Encoding.ASCII.GetBytes( strPath );
                    fixed( byte * pFileName = rgFileName ) {
                        iError = FreeType2API.PG_Face_New( Handle, pFileName, &ipFace);
                    }
                } catch( Exception oEx ) {
                    if( _rgErrors.IsUnhandled( oEx ) )
                        throw;
                }
            }
            if( iError != 0 )
                throw new ApplicationException( "Couldn't create new FontFace : " + iError.ToString() );

            FTFace oFace = new FTFace( ipFace, strPath, _rgFace.Count );

            _rgFace.Add( oFace );

            return (UInt16)( oFace.ID );
        }

        /// <summary>
        /// Use this object to generate your font glyphs.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException" />
        public IPgFontRender GetFontRenderer( uint uiFaceRenderID ) {
            foreach( FontRender oRender in _rgRenders ) {
                if( oRender.ID == uiFaceRenderID ) {
                    return oRender; // new FaceRenderStub( this, oRender );
                }
            }
            throw new KeyNotFoundException( "The face renderer was not found." );
        }

        /// <summary>
        /// Generate a glyph using any of the currently cached fonts. Starts with
        /// the given render and then FALLS BACK to other fonts if the glyph cannot
        /// be found.
        /// This is somewhat wishfull thinking since we should get the glyph index 
        /// from the shaper and would have to change fonts and ask it again. I'll 
        /// just keep this for fun until I've fleshed out my interactions with a real shaper.
        /// </summary>
        /// <remarks>This function is called by the implementation of IPgFontRender</remarks>
        public IPgGlyph GlyphFrom( FontRender oRender, UInt32 uiCode ) {
            if( oRender == null )
                throw new ArgumentOutOfRangeException( "Bad Face Renderer" );

            // Try the load the glyph with the given font, if fail, we get the 
            // default empty glyph, but try the others for a better fit!!
            IPgGlyph oGlyphBest;
            if( !oRender.GlyphLoad( uiCode, out oGlyphBest ) ) {
                // Look for an existing renderer to do the job...
                foreach( FontRender oRenderFallback in _rgRenders ) {
                    if( oRenderFallback.ID         != oRender.ID &&
                        oRenderFallback.FontHeight == oRender.FontHeight &&
                        oRenderFallback.Resolution == oRender.Resolution ) 
                    {
                        if( oRenderFallback.GlyphLoad( uiCode, out IPgGlyph oGlyphTry ) ) {
                            return oGlyphTry;
                        }
                    }
                }
                // Go through the other existing faces and see if the code point is supported
                // use the first face match as the new font renderer.
                foreach( FTFace oFaceFallback in _rgFace ) {
                    if( oFaceFallback.ID != oRender.Face.ID ) {
                        if( oFaceFallback.GlyphFromCodePoint( uiCode ) != 0 ) {
                            FontRender oRenderNew = new FontRender( this, oFaceFallback, oRender.Resolution, oRender.FontHeight, (uint)_rgRenders.Count );
                            oRenderNew.InitNew();
                            oRenderNew.GlyphLoad( uiCode, out IPgGlyph oGlyphTry );
                            _rgRenders.Add( oRenderNew );
                            return oGlyphTry;
                        }
                    }
                }
            }

            return oGlyphBest;
        }

        // TODO: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
        ~FTManager() {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: false);
        }

        public void Dispose() {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
