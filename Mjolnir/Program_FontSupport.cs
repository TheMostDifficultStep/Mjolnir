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
        public FTGlyphPos Coordinates { get; }

        public GlyphInfo( uint uiFaceIndex, uint uiGlyph, SKBitmap skGlyphBmp, FTGlyphPos ftGlyphPos )
        {
            FaceIndex   = uiFaceIndex;
            Glyph       = uiGlyph;
            Image       = skGlyphBmp;
            Coordinates = ftGlyphPos;
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

        public void SetSize( uint uiSize, SKSize szDeviceRes ) { 
            int iError = 0;
            try {
                uint uiEmSizeY = uiSize * 64;
                iError = FreeType2API.PG_Face_SetCharSize(
                    Handle,
                    0, /* char_width  in 1/64th of points */
                    uiEmSizeY, /* char_height in 1/64th of points. 26.6 fractional points. */
                    (uint)szDeviceRes.Width,
                    (uint)szDeviceRes.Height);
                // iError = FreeType2API.PG_Set_Pixel_Sizes( Handle, 0, uiSize );
            } catch( Exception oEx ) {
                if( rgErrors.IsUnhandled( oEx ) )
                    throw;
            }
            if( iError != 0 )
                throw new ApplicationException( "Couldn't set FontFace Size" );
            CurrentHeight = uiSize;
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

        protected void GlyphGenerate( FT_Render_Mode uiMode, uint uiGlyphIndex ) {
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
        /// <remarks>It's a bit clunky that we pass both the Gliph index AND the Code Point, but
        /// we can expect to be given the Glyph index from a shaper and not the code point. We
        /// can also expect to have an externally generated set of GlyphCoordinates. Worse, we'll
        /// have to coordinate with the font used by the shaper. Worry about all that later.</remarks>
        /// <exception cref="ApplicationException" />
        public IPgGlyph GlyphLoad( uint uiGlyph ) {
            if( CurrentHeight == 0 )
                throw new ArgumentException( "Glyph height has not be set." );

            try {
                GlyphGenerate( FT_Render_Mode.FT_RENDER_MODE_NORMAL, uiGlyph );
                SKBitmap skGlyphBitmap = GlyphCopyCurrent( out FTGlyphPos oGlyphCoords );

                return new GlyphInfo( (uint)ID, uiGlyph, skGlyphBitmap, oGlyphCoords );
            } catch( Exception oEx ) {
                if( rgErrors.IsUnhandled( oEx ) )
                    throw;

                throw new ApplicationException( "Problem Creating & Rendering Font Face.", oEx );
            }
        }
    }

    public class FaceRender {
        public FTFace Face       { get; }
        public SKSize Resolution { get; }
        public uint   Height     { get; }
        public uint   ID         { get; }

        protected List<IPgGlyph> _rgRendered = new List<IPgGlyph>( 50 );

        public FaceRender( FTFace oFace, SKSize sResolution, uint uiHeight, uint uiID ) {
            Face       = oFace ?? throw new ArgumentNullException( "Font face must not be null." );
            Resolution = sResolution;
            Height     = uiHeight;
            ID         = uiID;
        }

        public uint RendererID { get => ID; }

        /// <summary>
        /// False return simply means the box character is being returned. If the element has been
        /// generated all ready we return that glyph. So any other instances will have the same
        /// coordinate information. But of course a shaper might want different coordinates set
        /// for that glyph depending on it's neighbors. You'll have to cache those coordinates elsewhere.
        /// </summary>
        /// <returns>True if glyph matches code, False if box return.</returns>
        /// <exception cref="ApplicationException" />
        /// <remarks>This is a bit wrong since normally we'd have a glyph index from the
        /// shaper calculated from a particular font.</remarks>
        public bool GlyphLoad( uint uiCode, out IPgGlyph oGlyph ) {
            uint uiGlyph = Face.GlyphFromCodePoint( uiCode );

            foreach( IPgGlyph oTry in _rgRendered ) {
                if( oTry.Glyph == uiGlyph ) {
                    oGlyph = oTry;
                    return oTry.Glyph != 0;
                }
            }

            oGlyph = Face.GlyphLoad( uiGlyph );

            _rgRendered.Add( oGlyph );

            return oGlyph.Glyph != 0;
        }

        /// <summary>
        /// Not used at present but probably the future when I use an external shaper.
        /// </summary>
        public bool GlyphLoad( uint uiGlyph, uint uiCode, out IPgGlyph oGlyph ) {
            if( uiGlyph != Face.GlyphFromCodePoint( uiCode ) )
                throw new ArgumentException( "Glyph does not exist for matching codepoint" );

            foreach( IPgGlyph oTry in _rgRendered ) {
                if( oTry.Glyph == uiGlyph ) {
                    oGlyph = oTry;
                    return oTry.Glyph != 0;
                }
            }

            oGlyph = Face.GlyphLoad( uiGlyph );

            _rgRendered.Add( oGlyph );

            return oGlyph.Glyph != 0;
        }

        public void SetSize() {
            Face.SetSize( Height, Resolution );
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
        protected readonly List<FaceRender> _rgRenders = new List<FaceRender>();
		public    IntPtr                    Handle { get; } // Main handle from the FreeType Init.

        /// <summary>BUG: This is probably pointless, unless i only allow one of these objects
        ///  to be created at a time. Else we have to check the size on every GlyphLoad()
        ///  call anyway (for any particular face at least).</summary> 
        public class FaceRenderStub : IPgFontRender, IDisposable {
            public FaceRender FaceRender { get; }
            public FTManager  Manager    { get; }
            public uint       FontHeight { get { return FaceRender.Height; } }

            public FaceRenderStub( FTManager oManage, FaceRender oRender ) {
                FaceRender = oRender ?? throw new ArgumentNullException( "Face Render must not be null" );
                Manager    = oManage ?? throw new ArgumentNullException( "Manager must not be null" );

                FaceRender.SetSize();
            }

            public uint RendererID => FaceRender.ID;

            public void Dispose() {
                // in the future we'll note which FaceRender is in operation and 
                // make sure the proper size is set on the font face.
            }

            /// <summary>
            /// Bogus again since we'd have the glyphindex from the shaper and not
            /// the code point. But I'll leave it for now.
            /// </summary>
            public IPgGlyph GetGlyph( uint uiCodePoint ) {
                return Manager.GlyphFrom( FaceRender, uiCodePoint );
            }
        }

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

        public static float InchPerMeter { get { return 39.3701f; } }

        public uint GetRenderer( uint uiFace, uint uiHeight, SKSize sResolution ) {
            uint   uiFoundFace = uint.MaxValue;
            FTFace oFace       = _rgFace[(int)uiFace];

            foreach( FaceRender oRenderTry in _rgRenders ) {
                if( oRenderTry.Face.ID == uiFace ) {
                    uiFoundFace = uiFace;
                    if( oRenderTry.Height     == uiHeight &&
                        oRenderTry.Resolution == sResolution ) {
                        return oRenderTry.ID;
                    }
                }
            }

            FaceRender oRender = new FaceRender( oFace, sResolution, uiHeight, (uint)_rgRenders.Count );

            _rgRenders.Add( oRender );

            oFace.SetSize( uiHeight, sResolution );

            return oRender.ID;
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

        /// <summary>For the given face, cache a font for the given height and resolution.</summary>
        /// <exception cref="ArgumentOutOfRangeException" />
        public uint FaceCacheSize( ushort uiFace, uint uiHeight, SKSize skResolution ) {
            foreach( FaceRender oRender in _rgRenders ) {
                if( oRender.Face.ID    == uiFace &&
                    oRender.Height     == oRender.Height &&
                    oRender.Resolution == oRender.Resolution )  
                {
                    return oRender.ID;
                }
            }

            FaceRender oNew = new FaceRender( _rgFace[uiFace], skResolution, uiHeight, (uint)_rgRenders.Count );

            _rgRenders.Add( oNew );

            return( oNew.ID );
        }

        /// <summary>
        /// Use this object to generate your font glyphs.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException" />
        public IPgFontRender GetFontRenderer( uint uiFaceRenderID ) {
            foreach( FaceRender oRender in _rgRenders ) {
                if( oRender.ID == uiFaceRenderID ) {
                    return new FaceRenderStub( this, oRender );
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
        /// <seealso cref="FaceRenderStub" />
        protected IPgGlyph GlyphFrom( FaceRender oRender, UInt32 uiCode ) {
            if( oRender == null )
                throw new ArgumentOutOfRangeException( "Bad Face Renderer" );

            // Try the load the glyph with the given font, if fail, we get the 
            // default empty glyph, but try the others for a better fit!!
            IPgGlyph oGlyphBest;
            if( !oRender.GlyphLoad( uiCode, out oGlyphBest ) ) {
                foreach( FaceRender oRenderFallback in _rgRenders ) {
                    if( oRenderFallback.ID         != oRender.ID &&
                        oRenderFallback.Height     == oRender.Height &&
                        oRenderFallback.Resolution == oRender.Resolution ) 
                    {
                        oRenderFallback.SetSize();
                        if( oRenderFallback.GlyphLoad( uiCode, out IPgGlyph oGlyphTry ) ) {
                            return oGlyphTry;
                        }
                    }
                }
                foreach( FTFace oFaceFallback in _rgFace ) {
                    if( oFaceFallback.ID != oRender.Face.ID ) {
                        if( oFaceFallback.GlyphFromCodePoint( uiCode ) != 0 ) {
                            FaceRender oRenderNew = new FaceRender( oFaceFallback, oRender.Resolution, oRender.Height, (uint)_rgRenders.Count );
                            oRenderNew.SetSize();
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
