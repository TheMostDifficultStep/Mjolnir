using System;
using System.Collections.Generic;
using System.Text;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Collections;

using Play.Parse;

namespace Play.Edit {
    [StructLayout(LayoutKind.Sequential) ]
    public struct RECT {
        public Int32 _left;
        public Int32 _top;
        public Int32 _right;
        public Int32 _bottom;

        public RECT( Int32 left, Int32 top, Int32 right, Int32 bottom ) {
            _left   = left;
            _top    = top;
            _right  = right;
            _bottom = bottom;
        }
    }

    [StructLayout(LayoutKind.Sequential) ]
    public struct ABC {
        public Int32  A;
        public UInt32 B;
        public Int32  C;
    }

    [StructLayout(LayoutKind.Sequential) ]
    public struct GOFFSET {
        public Int32 du;
        public Int32 dv;
    }

    [StructLayout(LayoutKind.Sequential) ]
    public struct SCRIPT_VISATTR {
        public UInt16 _uiVisattr;

        public byte JustificationClass {
            get { return( (byte)(_uiVisattr & 0x000F ) ); }
        }

        public bool ClusterStart {
            get { return( ( _uiVisattr & 0x10 ) != 0 ); }
        }

        public bool Diacritic {
            get { return( ( _uiVisattr & 0x20 ) != 0 ); }
        }

        public bool ZeroWidth {
            get { return( ( _uiVisattr & 0x40 ) != 0 ); }
        }
    }

    [StructLayout(LayoutKind.Sequential) ]
    public struct SCRIPT_CONTROL {
        public UInt32 uiControl;
    }

    [StructLayout(LayoutKind.Sequential) ]
    public struct SCRIPT_STATE {
      public UInt16 uiState;
    }
    
    [StructLayout(LayoutKind.Sequential) ]
    public struct SCRIPT_ANALYSIS {
        public UInt16       uiAnalysis;
        public SCRIPT_STATE sState;
        public const int SCRIPT_UNDEFINED = 0;

        public Int32 Script { // eScript
            get { return uiAnalysis & 0x03ff; }
        }
        public void SetScriptUndefined() {
            uiAnalysis &= ( 0xff00 );
        }
        public bool RtoL {
            get { return ( uiAnalysis & 0x0400 ) != 0 ; }
        }
    }

    [StructLayout(LayoutKind.Sequential) ]
    public struct SCRIPT_ITEM {
        public Int32           iCharPos;
        public SCRIPT_ANALYSIS sAnalysis;
    }

    /// <summary>
    /// Documentation lifted from http://msdn.microsoft.com/en-us/library/dd374041(VS.85).aspx
    /// </summary>
    [StructLayout(LayoutKind.Sequential) ]
    public struct SCRIPT_LOGATTR {
        byte _bAttrs;
        
        /// <summary>
        /// Value indicating if breaking the line in front of the character, called a 
        /// "soft break", is valid. Possible values are defined in the following table. 
        /// This member is set on the first character of Southeast Asian words.
        /// </summary>
        public bool SoftBreak {
            get {
                return( ( _bAttrs & 0x01 ) != 0 );
            }
        }

        /// <summary>
        /// Value indicating if the character is one of the many Unicode characters classified 
        /// as breakable white space. Possible values are defined in the following table. Breakable 
        /// white space can break a word. All white space is breakable except nonbreaking space (NBSP) 
        /// and zero-width nonbreaking space (ZWNBSP).
        /// </summary>
        public bool WhiteSpace {
            get {
                return( ( _bAttrs & 0x02 ) != 0 );
            }
        }

        /// <summary>
        /// Value indicating if the character is a valid position for showing the caret upon a character 
        /// movement keyboard action. Possible values are defined in the following table. This member 
        /// is set for most characters, but not on code points inside Indian and Southeast 
        /// Asian character clusters. This member can be used to implement LEFT ARROW and RIGHT ARROW 
        /// operations in editors.
        /// </summary>
        public bool CharStop {
            get {
                return( ( _bAttrs & 0x04 ) != 0);
            }
        }

        /// <summary>
        /// Value indicating the valid position for showing the caret upon a word movement keyboard action, 
        /// such as CTRL+LEFT ARROW and CTRL+RIGHT ARROW. Possible values are defined in the following 
        /// table. This member can be used to implement the CTRL+LEFT ARROW and CTRL+RIGHT ARROW operations in editors.
        /// </summary>
        public bool WordStop {
            get { 
                return( ( _bAttrs & 0x08 ) != 0 );
            }
        }

        /// <summary>
        /// Value used to mark characters that form an invalid or undisplayable combination. Possible values 
        /// are defined in the following table. A script that can set this member has the fInvalidLogAttr 
        /// member set in its SCRIPT_PROPERTIES structure.
        /// </summary>
        public bool Invalid {
            get { 
                return( ( _bAttrs & 0x10 ) != 0 );
            }
        }
    }

    [StructLayout(LayoutKind.Sequential) ]
    public struct SCRIPT_FONTPROPERTIES {
        Int32  cBytes;
        UInt16 wgBlank;
        UInt16 wgDefault;
        UInt16 wgInvalid;
        UInt16 wgKashida;
        Int32  iKashidaWidth;

        public bool Load( IntPtr hDC, ref IntPtr hScriptCache ) {
            cBytes = 16;
            return( PgUniscribe.ScriptGetFontProperties( hDC, ref hScriptCache, ref this ) == HRESULT.S_OK );
        }

        public bool IsGlyphRendered( UInt16 uiGlyph ) {
            return(  uiGlyph == wgDefault ||
                   ( uiGlyph == wgInvalid &&
                     uiGlyph != wgBlank      ));
        }
    }

    //public const Int32 DEFAULT_PITCH = 0;
    //public const Int32 FIXED_PITCH             1
    //public const Int32 VARIABLE_PITCH          2
    //public const Int32 MONO_FONT               8

    class CHARSET {
        public const Int32 ANSI_CHARSET            = 0;
        public const Int32 DEFAULT_CHARSET         = 1;
        public const Int32 SYMBOL_CHARSET          = 2;
        public const Int32 SHIFTJIS_CHARSET        = 128;
        public const Int32 HANGEUL_CHARSET         = 129;
        public const Int32 HANGUL_CHARSET          = 129;
        public const Int32 GB2312_CHARSET          = 134;
        public const Int32 CHINESEBIG5_CHARSET     = 136;
        public const Int32 OEM_CHARSET             = 255;
        public const Int32 JOHAB_CHARSET           = 130;
        public const Int32 HEBREW_CHARSET          = 177;
        public const Int32 ARABIC_CHARSET          = 178;
        public const Int32 GREEK_CHARSET           = 161;
        public const Int32 TURKISH_CHARSET         = 162;
        public const Int32 VIETNAMESE_CHARSET      = 163;
        public const Int32 THAI_CHARSET            = 222;
        public const Int32 EASTEUROPE_CHARSET      = 238;
        public const Int32 RUSSIAN_CHARSET         = 204;
    } // end class

    [StructLayout(LayoutKind.Sequential) ]
    public struct SCRIPT_PROPERTIES {
        Int64 _uiProperties;

        public SCRIPT_PROPERTIES(Int64 value) {
            _uiProperties = value;
        }

        public Int32 LangID {
            get { return( (Int32)( 0xffff & _uiProperties ) ); }
        }
        public bool IsNumeric {
            get { return( ( 0x00010000 & _uiProperties ) != 0 ); }
        }
        public bool IsComplex {
            get { return( ( 0x00020000 & _uiProperties ) != 0 ); }
        }
        public bool NeedsWordBreaking {
            get { return( ( 0x00040000 & _uiProperties ) != 0 ); }
        }
        public bool NeedsCaretInfo {
            get { return( ( 0x00080000 & _uiProperties ) != 0 ); }
        }
        public byte CharSet {
            get { return( (byte)(( _uiProperties >> 20  ) & 0xff )); }
        }
        public bool Control {
            get { return( ( 0x10000000 & _uiProperties ) != 0 ); }
        }
        public bool PrivateUseArea {
            get { return( ( 0x20000000 & _uiProperties ) != 0 ); }
        }
        public bool NeedsCharacterJustify {
            get { return( ( 0x40000000 & _uiProperties ) != 0 ); }
        }
        public bool IsInvalidGlyph {
            get { return( ( 0x80000000 & _uiProperties ) != 0 ); }
        }
        public bool IsInvalidLogAttr {
            get { return( ( 0x000100000000 & _uiProperties ) != 0 ); }
        }
        public bool CDM {
            get { return( ( 0x000200000000 & _uiProperties ) != 0 ); }
        }
        public bool IsAmbiguousCharSet {
            get { return( ( 0x000400000000 & _uiProperties ) != 0 ); }
        }
        public bool ClusterSizeVaries {
            get { return( ( 0x000800000000 & _uiProperties ) != 0 ); }
        }
        public bool RejectInvalid {
            get { return( ( 0x001000000000 & _uiProperties ) != 0 ); }
        }
    }

    unsafe public class PgUniscribe {
        [DllImport( "usp10.DLL")]
        public static extern Int32 ScriptItemize( //[MarshalAs(UnmanagedType.LPWStr)] string strInChars, 
                                                  char * pText,
                                                  int iInChars, 
                                                  int iMaxItems, 
                                                  ref SCRIPT_CONTROL sSControl, ref SCRIPT_STATE sSState, 
                                                  [In,Out] SCRIPT_ITEM[] rgItems, out int iItemCount );

        [DllImport( "usp10.DLL", CharSet = CharSet.Unicode) ]
        public static extern Int32 ScriptShape( IntPtr hDC, ref IntPtr hScriptCache, 
                                                 char * rgChars, int iCharCount,
                                                 int iMaxGlyphs, 
                                                 ref SCRIPT_ANALYSIS sSAnalysis, 
                                                 [Out] UInt16 * pGlyphs, 
                                                 [Out] UInt16 * pClust, 
                                                 [Out] SCRIPT_VISATTR * pVisattr, 
                                                 ref int iGlyphs );

        [DllImport( "usp10.DLL")]
        public static extern Int32 ScriptPlace( IntPtr hDC, ref IntPtr hScriptCache, 
                                                 [In] UInt16 * pGlyphs, int iGlyphCount,
                                                 [In] SCRIPT_VISATTR * pVisattr, 
                                                 ref SCRIPT_ANALYSIS sSAnalysis,
                                                 [Out] int * rgAdvance, 
                                                 [Out] GOFFSET * rgGOffset, 
                                                 [Out] ABC * pAbc );

        [DllImport( "usp10.DLL")]
        public static extern Int32 ScriptTextOut( IntPtr hDC, ref IntPtr hScriptCache, int iX, int iY,
                                                   UInt32 uiOptions, [In] RECT * sRect, 
                                                   [In] SCRIPT_ANALYSIS * sSAnalysis,
                                                   [MarshalAs(UnmanagedType.LPWStr)] string strReserved, int iReservedCount, 
                                                   [In] UInt16 * pGlyphs, int iGlyphsCount,
                                                   [In] int * pAdvance, 
                                                   [In] [MarshalAs(UnmanagedType.LPArray)] int[] iJustify,
                                                   [In] GOFFSET * rgGOffset
                                                  );

        [DllImport( "usp10.DLL")]
        public static extern Int32 ScriptBreak( [MarshalAs(UnmanagedType.LPWStr)] string strChars, int iChars, 
                                                 ref SCRIPT_ANALYSIS sSAnalysis,
                                                 [Out] [MarshalAs(UnmanagedType.LPArray)] SCRIPT_LOGATTR rgSla );
        
        [DllImport( "usp10.DLL")]
        public static extern Int32 ScriptFreeCache( ref IntPtr hScriptCache );

        [DllImport( "usp10.DLL")]
        public static extern Int32 ScriptIsComplex( string strInChars, int iInChars, UInt32 dwFlags );

        [DllImport( "usp10.DLL")]
        public static extern Int32 ScriptXtoCP( int iX, int cChars, int cGlyphs, 
                                                 UInt16 * pwLogClust, SCRIPT_VISATTR *psva,
                                                 int * piAdvance, ref SCRIPT_ANALYSIS psa,
                                                 ref int iCP, ref int iTrailing );

        [DllImport( "Gdi32.DLL")]
        public static extern Int32 ExtTextOutW( IntPtr hDC, int iX, int iY, UInt32 uiFlags, 
                                                 RECT * pRect, 
                                                 [In] UInt16 * puiGlyphs, UInt32 uiGlyphCount, [In] Int32 * uiDx );

        [DllImport( "usp10.DLL")]
        public static extern Int32 ScriptGetFontProperties( IntPtr hdc,
                                                            ref IntPtr hScriptCache,
                                                            ref SCRIPT_FONTPROPERTIES sfp );

        [DllImport( "usp10.DLL" )]
        public static extern Int32 ScriptGetProperties( out IntPtr ppProperties, out Int32 piMaxProps );

        public const UInt32 SIC_COMPLEX    = 1; // Treat complex script letters as complex
        public const UInt32 SIC_ASCIIDIGIT = 2; // Treat digits U+0030 through U+0039 as complex
        public const UInt32 SIC_NEUTRAL    = 4; // Treat neutrals as complex

        public const UInt32 ETO_OPAQUE  = 0x2;
        public const UInt32 ETO_CLIPPED = 0x4;
        public const UInt32 ETO_GLYPH_INDEX = 0x10;

        public const Int32 USP_E_SCRIPT_NOT_IN_FONT = -2147220992; // 0x80040200;

        // ScriptShape can execute without returning USP_E_SCRIPT_NOT_IN_FONT
        // yet some characters render to default, invalid, or blank glyphs.
        // Check for those in the glyph buffer.
        public static bool IsGlyphRendered( UInt16[]              rgGlyphs,
                                            int                   iStart,
                                            int                   iLength,
                                            SCRIPT_FONTPROPERTIES sFontProps )
        {
            for( int i = iStart; i < iStart + iLength; ++i ) {
                UInt16 uiGlyph = rgGlyphs[i];
                if( sFontProps.IsGlyphRendered( uiGlyph ) ) {
                    return true;
                }
            }
            return false;
        }
    } // end class

    /// <summary>
    /// Code converted from Michael Kaplan's blog of random 
    /// stuff of dubious value, http://blogs.msdn.com/michkap
    /// </summary>
    internal class ScriptProperties {
        internal static readonly SCRIPT_PROPERTIES[] m_rgScriptProperties;

        static ScriptProperties() {
            IntPtr ipScriptProperties = IntPtr.Zero;
            Int32  iScripts;
            Int32  hr = PgUniscribe.ScriptGetProperties(out ipScriptProperties, out iScripts);

            Debug.Assert(hr == HRESULT.S_OK);

            if( hr == HRESULT.S_OK ) {
                m_rgScriptProperties = new SCRIPT_PROPERTIES[iScripts];

                for (Int32 i = 0; i < iScripts; i++) {
                    IntPtr ipProp = Marshal.ReadIntPtr(ipScriptProperties, i * IntPtr.Size);
                    long   lProp  = Marshal.ReadInt64 (ipProp);

                    m_rgScriptProperties[i] = new SCRIPT_PROPERTIES(lProp);
                }
            }
        }

        /// <summary>
        /// Go to the global array if scripts and grab one.
        /// </summary>
        /// <param name="iScript">Which script to return.</param>
        /// <returns>The script we want.</returns>
        /// <exception cref="IndexOutOfRange"
        internal static SCRIPT_PROPERTIES GetProperty(Int32 iScript) {
            return m_rgScriptProperties[iScript];
        }
    } // end class

    public struct NEWTEXTMETRIC {
        public Int32  tmHeight;
        public Int32  tmAscent;
        public Int32  tmDescent;
        public Int32  tmInternalLeading;
        public Int32  tmExternalLeading;
        public Int32  tmAveCharWidth;
        public Int32  tmMaxCharWidth;
        public Int32  tmWeight;
        public Int32  tmOverhang;
        public Int32  tmDigitizedAspectX;
        public Int32  tmDigitizedAspectY;
        public char tmFirstChar;
        public char tmLastChar;
        public char tmDefaultChar;
        public char tmBreakChar;
        public byte  tmItalic;
        public byte  tmUnderlined;
        public byte  tmStruckOut;
        public byte  tmPitchAndFamily;
        public byte  tmCharSet;
        public UInt32 ntmFlags;
        public UInt16 ntmSizeEM;
        public UInt16 ntmCellHeight;
        public UInt16 ntmAvgWidth;
    }

    public struct FONTSIGNATURE {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst=4)]
        public UInt32[] fsUsb;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst=2)]
        public UInt32[] fsCsb;
    }
    
    public struct NEWTEXTMETRICEX {
        public NEWTEXTMETRIC ntmTm;
        public FONTSIGNATURE ntmFontSig;
    };

    // Platform Invoke Tutorial http://msdn.microsoft.com/en-us/library/aa288468.aspx
    public struct LOGFONT {
        public const int   LF_FACESIZE = 32;
        public const Int32 OUT_TT_ONLY_PRECIS = 7;

        public Int32 lfHeight;
        public Int32 lfWidth;
        public Int32 lfEscapement;
        public Int32 lfOrientation;
        public Int32 lfWeight;
        public byte  lfItalic;
        public byte  lfUnderline;
        public byte  lfStrikeOut;
        public byte  lfCharSet;
        public byte  lfOutPrecision;
        public byte  lfClipPrecision;
        public byte  lfQuality;
        public byte  lfPitchAndFamily;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst=LF_FACESIZE)]
        public string lfFaceName; 
    };

    public struct ENUMLOGFONTEX {
        public const int LF_FULLFACESIZE = 64;
        public const int LF_FACESIZE     = 32;

        public LOGFONT elfLogFont;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst=LF_FULLFACESIZE)]
        public string elfFullName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst=LF_FACESIZE)]
        public string elfStyle;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst=LF_FACESIZE)]
        public string elfScript;
    };

    // Implementing Callback Functions http://msdn.microsoft.com/en-us/library/d186xcf0(VS.71).aspx
    public delegate bool CallBack(int hwnd, int lParam);
    public delegate int  EnumFontFamExProc( 
                           ref ENUMLOGFONTEX lpelfe,
                           ref NEWTEXTMETRICEX lpntme,
                           UInt32 uiFontType,
                           Int32 lParam
                         );

    /// <summary>
    /// Work in progress. Mostly finished code to enumerate fonts. This will be for the
    /// font fallback code!
    /// </summary>
    public class EnumFontsHelper {
        // WIKI for Pinvoke! http://www.pinvoke.net/default.aspx/gdi32/EnumFontFamiliesEx.html
        [DllImport("Gdi32.dll")]
        public static extern int EnumFontFamiliesEx( IntPtr hDC,
                                                     [In] ref LOGFONT lpLogfont,
                                                     EnumFontFamExProc oEnumFontFamExProc,
                                                     IntPtr lParam,
                                                     UInt32 dwFlags );

        public static int MyEnumFont( ref ENUMLOGFONTEX ELFE, 
                                      ref NEWTEXTMETRICEX NTME, 
                                      UInt32 uiFontType, 
                                      Int32 lParam ) {
            return( 1 );
        }

        // Get the DpiX from the graphics object that spawned the DC. TODO: I probably can get
        // that measurement from the DC but I'll look that up later.
        public static void Test2( IntPtr hDC, float flDpiX )
        {
            EnumFontFamExProc myCallback = new EnumFontFamExProc(EnumFontsHelper.MyEnumFont);
            LOGFONT           guideFont  = new LOGFONT();

            guideFont.lfHeight = -(Int32)(10f * flDpiX / 72f);
            guideFont.lfCharSet = 0;
            guideFont.lfOutPrecision = LOGFONT.OUT_TT_ONLY_PRECIS;

            EnumFontFamiliesEx( hDC, ref guideFont, myCallback, IntPtr.Zero, 0 );
        }
    } // end class

    /// <summary>
    /// This is a single line of cached data.
    /// </summary>
    public class UniscribeCache : 
        IDisposable,
		IEnumerable<IColorRange>
    {
        protected readonly Line _oLine; // The line we are caching.

        // Glyph tracking
        protected SCRIPT_ITEM[]     _rgItems;
        protected int               _iGlyphTotal = 0;
        protected int[]             _rgGlyphItems;

        // Glyph buffers.
        protected GOFFSET[]         _rgGOffset;
        protected UInt16[]          _rgGlyphs;      // Main glyph buffer. 
        protected UInt16[]          _rgClusters;    // Where the characters of the logical text end up in the glyph text.
                                                    // يُساوِيzzzzz
                                                    //     ^-- first position in rtl string. Two overlaping glyphs show up last in glyph sequence.
                                                    //  ^----- Second set of overlapping glyphs.
                                                    // 6 6 4 3 2 2 0
                                                    // ^ ^ 0th and 1st logical string elements make up a cluster at 5 & 6 in glyph list.
        protected SCRIPT_VISATTR[]  _rgVisAttrs;
        protected int[]             _rgAdvanceWidths;  // How far to advance from one character to the next.
        protected int[]             _rgAdvanceAbs;  // Left coord of glyph.
        protected ABC               _sABCWidth;     // Actually as many as items in the run but, just need one for now.
        protected float             _flFontHeight = 0;
        protected int               _iTop = 0;
        protected bool              _fNeedsUpdate = true;

                  readonly ICollection<IPgWordRange> _rgWords        = new List<IPgWordRange>( 1 ); // Mixed use with Line formatting objects so need IColorRange, vs IPgMemoryRange
        protected readonly ICollection<IColorRange>  _rgSlicedColor  = new List<IColorRange>(); // Segmented Color info.

        public UniscribeCache( Line oLine ) {
            _oLine = oLine ?? throw new ArgumentNullException();

            _rgWords.Add( new WordRange( 0, int.MaxValue, 0 ) );
        }

        /// <summary>
        /// This is for the debugger. Never use this for line output.
        /// </summary>
        public override string ToString() {
            StringBuilder sbBuild = new StringBuilder();

            sbBuild.Append( Top );
            sbBuild.Append( "->" );
            sbBuild.Append( Bottom );
            sbBuild.Append( "@" );
            sbBuild.Append( _oLine.At.ToString() );
            sbBuild.Append( ":" );
            sbBuild.Append( _oLine.ToString(), 0, _oLine.ElementCount > 50 ? 50 : _oLine.ElementCount );

            return( sbBuild.ToString() );
        }

        public Line Line {
            get { return( _oLine ); }
        }

        public int At {
            get { return ( _oLine.At ); }
        }

        virtual public int Height {
            get { return( (int)( _flFontHeight ) ); }
        }

        public int Top {
            get { return( _iTop ); }
            set { _iTop = value; }
        }

        public int Bottom {
            get { return( Top + Height ); }
            set { _iTop = value - Height; }
        }

        /// <summary>
        /// Total width of the line UNWRAPPED.
        /// </summary>
        public int Width { 
            get { 
                if( _iGlyphTotal > 0 )
                    return( _rgAdvanceAbs[_iGlyphTotal - 1] );
                
                return( 0 );
            }
        }

        /// <summary>
        /// Is the point location vertically within our element? Anywhere on the left or
        /// right of the valid vertical position will return true.
        /// </summary>
        /// <param name="pntLocation">Test, value in world coordinates.</param>
        /// <returns>true if the point is with our cached element location.</returns>
        public bool IsHit( Point pntLocation ) {
            return( pntLocation.Y >= Top && pntLocation.Y < Bottom );
        }

        /// <summary>
        /// Line breaker formatting.
        /// </summary>
        public virtual ICollection<IPgWordRange> Words {
            get {
                return( _rgWords );
            }
        }

        /// <summary>
        /// The collection of Color and Selection ranges reformated and
        /// divided to fit the wrap segments.
        /// </summary>
        public virtual ICollection<IColorRange> Color {
            get {
                return( _rgSlicedColor );
            }
        }

		/// <summary>
		/// Shortcut to the line formatting info.
		/// </summary>
		public ICollection<IColorRange> Formatting {
			get {
				return( _oLine.Formatting );
			}
		}

		/// <summary>
		/// Return a single color range that is the entire line for black. The idea is that we always
        /// will blit the black text and then add colorizing. Innaccuracies in my layout kind of mess
        /// the idea up.
		/// </summary>
		/// <returns></returns>
		public IEnumerator<IColorRange> EnumColorEmpty() {
			//yield break;
			return EnumBlack();
		}

		public IEnumerator<IColorRange> EnumBlack() {
			yield return(  new ColorRange( 0, Line.ElementCount, 0 ) );
		}

		[Obsolete] public IEnumerator<IColorRange> EnumColor( int iColor ) {
            // Selection is in this bag in the wrapped case. Uniline version might need fixing.
            if( iColor != 0 && Color.Count > 0 )
                return( Color.GetEnumerator() );
            // Just print everything, later passes will recolor.
            if( iColor == 0 ) 
                return( Words.GetEnumerator() ); 

			return( EnumColorEmpty() );
		}

        public virtual void Dispose() {
            // These get reallocated via ReAllocGlyphBuffers()
            _rgGlyphs         = null;
            _rgVisAttrs       = null;
            _rgAdvanceWidths  = null;
            _rgAdvanceAbs     = null;
            _rgGOffset        = null;

            // These get allocated in Update()
            _rgGlyphItems = null;
            _rgClusters   = null;
            _rgItems      = null;

            _iGlyphTotal  = 0;
        }

        /// <summary>
        /// Allocate the buffers we are going to need. 
        /// </summary>
        /// <param name="iCount">Must be greater than Zero.</param>
        /// <returns>Size of Glyph buffer. And consequently the other buffers.</returns>
        /// <remarks>If the Array is pinned, we'll need to unpin and re-pin after this call.</remarks>
        public virtual int ReAllocGlyphBuffers( int p_iGlyphCount ) {
            if( p_iGlyphCount < 0 ) {
                return( 0 );
            }

            try {
                // If new size is more than array can hold, I assume it allocates new buffer and blits old into it.
                // Our old buffer will remain pinned. We'll need to un-pin and re-pin the newly alloc'd array.
                Array.Resize<UInt16>        ( ref _rgGlyphs,      p_iGlyphCount );
                Array.Resize<SCRIPT_VISATTR>( ref _rgVisAttrs,    p_iGlyphCount );
                Array.Resize<int>           ( ref _rgAdvanceWidths,  p_iGlyphCount );
                Array.Resize<int>           ( ref _rgAdvanceAbs,  p_iGlyphCount + 1 );
                Array.Resize<GOFFSET>       ( ref _rgGOffset,     p_iGlyphCount );
            } catch( OutOfMemoryException ) {
                return( 0 );
            } catch( ArgumentOutOfRangeException ) {
                return( 0 );
            }

            return( p_iGlyphCount );
        } // end method

        /// <summary>
        /// Duz what it sez.
        /// </summary>
        /// <returns>This method demonstrates the use of our new pinning strategy. Since our buffer
        /// may contain either char buffer OR string, I want to pin either structure, but use
        /// uniformly. See the GCHandle code for how we do it.
        protected bool Itemize( out int iItems ) {
            if( _rgItems == null ) {
                _rgItems = new SCRIPT_ITEM[3]; // Must be greater than or equal to 2 to start.
            }

            iItems = 0;

            // Since line is readonly, it should never be null, but if it is, we'll fail gracefully.
            try {
                if( _oLine.ElementCount == 0 )
                    return( false );
            } catch( NullReferenceException ) {
                return( false );
            }

            int            iTryItems      = _rgItems.Length;
            SCRIPT_CONTROL sScriptControl = new SCRIPT_CONTROL();
            SCRIPT_STATE   sScriptState   = new SCRIPT_STATE();
            Int32          hr = HRESULT.E_OUTOFMEMORY;

            GCHandle? oText = null;
            if( _oLine.Buffer == null )
                oText = GCHandle.Alloc( _oLine.ToString(), GCHandleType.Pinned );
            else
                oText = GCHandle.Alloc( _oLine.Buffer, GCHandleType.Pinned );

            try {
                unsafe {
                    char * pText = (char*)oText.Value.AddrOfPinnedObject();

                    while( iTryItems < 60000 ) { // TODO: Magic number. Need a way to break up long lines.
                        hr = PgUniscribe.ScriptItemize( pText, _oLine.ElementCount, _rgItems.Length, 
                                                        ref sScriptControl, ref sScriptState, 
                                                        _rgItems, out iItems );
                        if( hr == HRESULT.E_OUTOFMEMORY ) {
                            iTryItems += 100; // Bump up the buffer size. Is this a good number? 
                            _rgItems = new SCRIPT_ITEM[iTryItems];
                        } else
                            break;
                    }
                }
            } finally {
                if( oText != null )
                    oText.Value.Free();
            }

            return( hr == HRESULT.S_OK );
        } // end method

        public void Invalidate() {
            _fNeedsUpdate = true;
        }

        public bool IsInvalid {
            get {
				if( _rgGlyphItems == null )
					return( true );

				return( _fNeedsUpdate ); 
			}
        }

        public virtual void Update (
            IntPtr hDC, ref IntPtr hScriptCache, int iTabWidth, float flFontHeight,
            SCRIPT_FONTPROPERTIES sDefFontProps, CacheManager oManager,
            int iWidth, ICollection<ILineSelection> rgSelection
        ) {
            Update( hDC, ref hScriptCache, iTabWidth, flFontHeight, sDefFontProps );

            OnChangeFormatting( iWidth, rgSelection );
        }

        /// <summary>
        /// Measure the line.
        /// </summary>
        /// <remarks>
        /// 1) 1/21/2016 : Turns out there's this weird behavior where ExtTextOut is offsetting the start
        ///    of an item run by -1 pixel. It's interesting in that the first ABC run for a string
        ///    has a leading A width of 1. The rest don't and it's on the rest I have a problem.
        ///    So what we do is add 1 to the width of the last character of every item run.
        ///    Then in ExtTextOut() we pass our AdvanceWidths to it and it works well enough!!
        ///    For porportional fonts we're a little out of alignment, but it's good enough for government work.
        /// </remarks>
        protected bool Update( 
            IntPtr        hDC, 
            ref IntPtr    hScriptCache,
            int           iTabWidth, 
            float         flFontHeight,
            SCRIPT_FONTPROPERTIES sFontProps
        ) {
            _flFontHeight = flFontHeight;
            _iGlyphTotal  = 0;

            Int32  hr      = 0;
            int    iItems  = 0;

            if( !Itemize( out iItems ) ) {
                return( false );
            }

            if( iItems > 0  ) {
                _rgClusters   = new ushort[_oLine.ElementCount];  
                _rgGlyphItems = new int[iItems+1];          // Track the offsets into the glyph buffers.

                // Allocate glyph buffer based on an estimated size needed.
                int iGlyphAlloc = ReAllocGlyphBuffers( (int)(1.5 * (double)_oLine.ElementCount + 16) );

                GCHandle? oText = null;
                try {
                    if( _oLine.Buffer == null )
                        oText = GCHandle.Alloc( _oLine.ToString(), GCHandleType.Pinned );
                    else
                        oText = GCHandle.Alloc( _oLine.Buffer, GCHandleType.Pinned );

                    for( int iItem=0; iItem<iItems; ++iItem ) {
                        int iCharStart  = _rgItems[iItem  ].iCharPos;              // Text (logical) offset of the run.
                        int iCharCount  = _rgItems[iItem+1].iCharPos - iCharStart; // Text (logical) length of the run.
                        int iGlyphStart = _rgGlyphItems[iItem]; // Glyph offset of the run. Relative to main Glyph buffer.
                        int iGlyphCount = 0;                    // Number of glyphs in the run.

                        unsafe {
                            char * pText = (char*)oText.Value.AddrOfPinnedObject();

                            fixed( UInt16 * pClusters = &_rgClusters[iCharStart] ) { // Clusters are logical (text) length based.
                                int iTryShape = 4; // Runs tend to be short, I don't expect to need more than one retry in general.
                                do {
                                    // Pin these within the loop in case we re-alloc, we'll unlock old, mem and re-pin new stuff.
                                    fixed( UInt16 * pGlyphs = &_rgGlyphs[iGlyphStart] ) {
                                        fixed( SCRIPT_VISATTR * pVisAttrs = &_rgVisAttrs[iGlyphStart] ) {
                                            hr = PgUniscribe.ScriptShape( hDC, ref hScriptCache,
                                                                            &pText[iCharStart], iCharCount, 
                                                                            _rgGlyphs.Length - iGlyphStart, 
                                                                            ref _rgItems[iItem].sAnalysis, 
                                                                            /*out*/ pGlyphs, /*out*/ pClusters, /*out*/ pVisAttrs, 
                                                                            ref iGlyphCount );
                                            switch( hr ) {
                                                case HRESULT.E_OUTOFMEMORY:
                                                    iGlyphAlloc = ReAllocGlyphBuffers( iGlyphAlloc + 25 );
                                                    if( iGlyphAlloc <= 0 ) {
                                                        Dispose();
                                                        return( false );
                                                    }
                                                    break;
                                                case HRESULT.E_PENDING:
                                                    // Need to load the font. This shouldn't happen, as of the writing we load the font.
                                                    break;
                                                case PgUniscribe.USP_E_SCRIPT_NOT_IN_FONT:
                                                    // Said that we didn't find the font but we might try again as
                                                    // part of our font fall back.
                                                    _rgItems[iItem].sAnalysis.SetScriptUndefined();
                                                    break;
                                                default: {
                                                    if( PgUniscribe.IsGlyphRendered( _rgGlyphs, iGlyphStart, iGlyphCount, sFontProps ) ) {
                                                        int               iScript = _rgItems[iItem].sAnalysis.Script;
                                                        SCRIPT_PROPERTIES sProp   = ScriptProperties.GetProperty(iScript);
                                                        int               iCharSet; // consider langid too.

                                                        if( sProp.IsAmbiguousCharSet )
                                                            iCharSet = CHARSET.DEFAULT_CHARSET;
                                                        else
                                                            iCharSet = sProp.CharSet;
                                                        // At this point I want to enum fonts and set iTryShape to the count of the
                                                        // number of fonts I find plus one to allow for OUTOFMEMORY.
                                                    }

                                                    iTryShape = 0;
                                                } break;
                                            }
                                            // The other error cases can probably be just as bad and I should code for them too.
                                            // But I'm thinking in those cases I'll just see little boxes.
                                            if( hr != HRESULT.S_OK ) {
                                                Dispose();
                                                return( false );
                                            }
                                        } // End VisAttr
                                    } // End Glyph
                                } while( --iTryShape > 0 );
                            } // End Cluster
                            fixed( UInt16 * pGlyphs = &_rgGlyphs[iGlyphStart] ) {
                                fixed( SCRIPT_VISATTR * pVisAttrs = &_rgVisAttrs[iGlyphStart] ) {
                                    fixed( int * pAdvance = &_rgAdvanceWidths[iGlyphStart] ) {
                                        fixed( GOFFSET * pGOffset = &_rgGOffset[iGlyphStart] ) {
                                            fixed( ABC * pABC = &_sABCWidth ) {
                                                hr = PgUniscribe.ScriptPlace( hDC, ref hScriptCache, 
                                                                              pGlyphs, iGlyphCount, 
                                                                              pVisAttrs, ref _rgItems[iItem].sAnalysis,
                                                                              pAdvance, pGOffset, null /* pABC */ );
                                            }
                                        }
                                    }
                                    _rgGlyphItems[iItem+1] = iGlyphStart + iGlyphCount; 
                                    _iGlyphTotal      += iGlyphCount;
                                } // End VisAttr
                            } // End Glyph
                        } // end unsafe
                        // Our clusters are lined up to the offsets of the glyphs created IN THIS RUN! Add
                        // the glyph run start to each so we know where glyphs are in the entire glyph buffer.
                        // Also, look for tabs in the logical stream and make sure we set their width in
                        // the Glyph stream. Use the _rgAdvanceOff array to make sure we match the corrected
                        // indexes. Fix it up before we end this run calculation.
                        for( int iLogical = iCharStart; iLogical< ( iCharStart+iCharCount ); ++iLogical ) {
                            _rgClusters[iLogical] += (ushort)iGlyphStart;
                            if( _oLine[iLogical] == 0x9 ) {
                                // If one logical tab is two glyphs we'll have problems, but I'm not expecting that.
                                _rgAdvanceWidths[_rgClusters[iLogical]] = iTabWidth;
                            }
                        }
                    } // end for items
                } finally {
                    if( oText != null )
                        oText.Value.Free();
                }
                // 1) Workaround, see above.
                for( int i=1; i<iItems; ++i ) {
                    _rgAdvanceWidths[_rgItems[i].iCharPos-1] += 1;
                }
                // Compute our absolute offset which we can use for locating things
                int iTemp = 0;
                for( int i=0; i<_rgAdvanceWidths.Length && i < _rgAdvanceAbs.Length; ++i ) {
                    _rgAdvanceAbs[i] = iTemp;
                    iTemp += _rgAdvanceWidths[i];
                }
            } 

            _fNeedsUpdate = false;

            return( true );
        } // end method

        protected virtual void DiceAColor( IColorRange oElem ) {
            _rgSlicedColor.Add( oElem );
		}

        /// <summary>
        /// Place holder for word wrapping implementation.
        /// </summary>
        /// <param name="iDisplayWidth">Width in "pixels" of the view</param>
        /// <param name="oFormatting">Formatting information.</param>
        internal virtual void OnChangeSize( ICollection<ILineSelection> rgSelections, int iWidth ) {
        }

        /// <summary>
        /// Basic degenerate operation. If the editor is in a no grammer running mode I'll be creating unwrapped
        /// lines. If I have a color range enumerator made for each selection type
        /// I could use it directly in the render function, instead of mixing the line formatting and the
        /// selection here. 
        /// </summary>
        /// <param name="rgSelections">Formatting information for selection.</param>
        internal virtual void OnChangeFormatting( int iWidth, ICollection<ILineSelection> rgSelections ) {
            _rgSlicedColor.Clear();

            // Selection is annoying since we render it in normal case, then when setup of for
			// selection (bg color) we have to do it all over. I would like to separate selection 
			// since it happens on a separate pass anyway.
            if( rgSelections != null ) {
                foreach( ILineSelection oSelect in rgSelections ) {
                    if( oSelect.IsHit( _oLine ) ) {
                        DiceAColor( oSelect );
                    }
                }
            }

            // Slice up normal color info for the words. If there is no formatting then we didn't get a parse
			// and we'll rely on the Words parser.
			if( Formatting.Count > 0 ) {
				foreach( IColorRange oColor in Formatting ) {
					DiceAColor( oColor );
				}
			} else {
				if( Words.Count > 0 ) {
					foreach( IColorRange oColor in Words ) {
						DiceAColor( oColor );
					}
				}
			}
        } // end method

        /// <summary>
        /// Plunder the given enumerator to render the text with the proper color.
        /// Note: We rely on the font height of this line cached in EditWinLine class
        ///       we can't use that and need to figure that out in this class instead.
        /// </summary>
        /// <param name="hDC">Display Context</param>
        /// <param name="pntEditAt">Origin of the Edit.</param>
        /// <param name="iColor">Color we are currently displaying.</param>
        /// <param name="oEnum">Bag of colors to choose from.</param>
        public virtual void Render(
            IntPtr    hDC, 
            IntPtr    hScriptCache,
            PointF    pntEditAt, 
            int       iColor,
            RECT ? rcClip )
        {
            if( _rgGlyphs == null ) // If this is null, then nothing will be ready.
                return;

            uint uiFlags = PgUniscribe.ETO_GLYPH_INDEX;
            RECT rcClip2 = new RECT();

            if( rcClip.HasValue ) { 
                uiFlags |= PgUniscribe.ETO_CLIPPED;
                rcClip2 = rcClip.Value;
            }

            unsafe {
                fixed( UInt16 * pGlyphs = &_rgGlyphs[0] ) {
                    fixed( int * pAdvanceOff = &_rgAdvanceWidths[0] ) {
						foreach( IColorRange oElem in this ) {
                            if( oElem.ColorIndex == iColor ) {
                                int iMaxLength = _iGlyphTotal - oElem.Offset;
                                int iUseLength = oElem.Length > iMaxLength ? iMaxLength : oElem.Length;

                                if( oElem.Length > 0 && oElem.Offset < _iGlyphTotal ) { 
                                    PgUniscribe.ExtTextOutW( 
                                        hDC, 
                                        (Int32)( pntEditAt.X + _rgAdvanceAbs[oElem.Offset] ), 
                                        (Int32)( pntEditAt.Y ), 
                                        uiFlags, 
                                        &rcClip2, 
                                        &pGlyphs[oElem.Offset], 
                                        (uint)iUseLength, 
                                        &pAdvanceOff[oElem.Offset] );
                                }
                            }
                        } // end for
                    } // end fixed
                } // end fixed
            } // end unsafe
        } // end method


        /// <summary>Render the End Of Line character.</summary>
        /// <param name="hDC">Handle to a DC.</param>
        /// <param name="pntEditAt">Top left of the cache element on screen.</param>
        public void RenderEOL( IntPtr hDC, PointF pntEditAt )
        {
            Point pntOffset = GlyphOffsetToPoint( Line.ElementCount );
            unsafe {
                fixed( char* pwcText = "<" ) {
                    Gdi32.TextOut( 
                            hDC,
                            (Int32)( pntEditAt.X + pntOffset.X ),
                            (Int32)( pntEditAt.Y + pntOffset.Y ),
                            pwcText,
                            1 );
                }
            }
        } // end method

        /// <summary>
        /// Render the little underline's to show where an error has occured.
        /// </summary>
        /// <param name="hDC">handle to a display context.</param>
        /// <param name="pntEditAt">Top left of the cache element on screen.</param>
        public void RenderLinks(IntPtr hDC, PointF pntEditAt ) 
        {
			try {
				using( IEnumerator<IColorRange> oEnum = Color.GetEnumerator() ) {
					while( oEnum.MoveNext() ) {
						IColorRange oElem = oEnum.Current;
						if( oElem.ColorIndex == -4 ) {
							unsafe {
								Point pntElemOffset = GlyphOffsetToPoint( oElem.Offset );
								Gdi32.MoveToEx( hDC,
												(Int32)( pntEditAt.X + pntElemOffset.X ),
												(Int32)( pntEditAt.Y + pntElemOffset.Y + _flFontHeight - 1 ),
												null);
								pntElemOffset = GlyphOffsetToPoint(  oElem.Offset + oElem.Length );
								Gdi32.LineTo  ( hDC,
												(Int32)( pntEditAt.X + pntElemOffset.X ),
												(Int32)( pntEditAt.Y + pntElemOffset.Y + _flFontHeight - 1 ));
							}
						}
					}
				}
			} catch( NullReferenceException ) {
			}
        } // end method

       /// <summary>
        /// Find the character offset of the mouse position. This won't work until
        /// we merge it with the line wrapping code. Basically, it's temporary
        /// experimental code.
        /// </summary>
        /// <param name="iX">Location relative to the upper left of entire line 
        /// collection which is 0, 0. Remove any window positioning before passing
        /// coordinates to us.</param>
        /// <returns>Character offset of the given location. 0 if left of the
        /// first element or an error of some sort.</returns>
        public int GlyphCoordinateToOffset2( Point pntLocal) {
            if( _rgGlyphs == null )
                return( 0 );

            int iTemp = Array.BinarySearch<Int32>( _rgAdvanceAbs, 0, _iGlyphTotal, pntLocal.X );
            
            if( iTemp < 0 )
                iTemp = -iTemp; // Most of the time we miss. The inverse is always the higher number.
            else
                iTemp++; // The only time it picks the correct element is if it's exact match. So add one.

            // Search for which run the item ends up in so that we can call 
            // ScriptXtoCP() for that run.
            int iRun = -1;
            for( int i = 0; i<_rgGlyphItems.Length - 1; ++i ) {
                if( iTemp >= _rgGlyphItems[i] &&
                    iTemp <  _rgGlyphItems[i+1] ) {
                    iRun = iTemp;
                    break;
                }
            }

            if( iRun < 0 )
                return( 0 );

            int iTrailing = 0;
            int iIndex    = 0;

            int iGlyphIndex = _rgGlyphItems[iRun];
            int iCharStart  = _rgItems[iRun  ].iCharPos;
            int iCharCount  = _rgItems[iRun+1].iCharPos - iCharStart;
            int iGlypCount  = _rgGlyphItems[iRun+1] - _rgGlyphItems[iRun];
            int hr          = 0;

            unsafe {
                fixed( UInt16 * pGlyphs = &_rgGlyphs[iGlyphIndex] ) {
                    fixed( int * pAdvanceOff = &_rgAdvanceWidths[iGlyphIndex] ) {
                        // Loop over the runs to find which contains the iX position.
                            fixed( SCRIPT_VISATTR * pVisAttrs = &_rgVisAttrs[iGlyphIndex] ) {
                                fixed( UInt16 * pClusters = &_rgClusters[iGlyphIndex] ) {
                                    hr = PgUniscribe.ScriptXtoCP( pntLocal.X, iCharCount, iGlypCount, 
                                                                  pClusters, pVisAttrs, pAdvanceOff,
                                                                  ref _rgItems[iRun].sAnalysis, ref iIndex, ref iTrailing );
                                }
                            }
                    }
                }
            }

            if( hr != 0 )
                return( 0 );

            return( iIndex + iTrailing );
        }

        /// <summary>
        /// Find the character offset of the mouse position. TODO: This is a crude
        /// re-factoring for the word wrap version. I can do a better job. See
        /// GlyphPointToOffset2() for the beginnings of a replacement.
        /// </summary>
        /// <param name="pntLocal">Location relative to the upper left of line 
        /// collection which is 0, 0. Remove any window positioning before passing
        /// coordinates to us.</param>
        /// <returns>Character offset of the given location. 0 if left of the
        /// first element.</returns>
        public virtual int GlyphPointToOffset( Point pntWorld ) {
            if( _iGlyphTotal <= 1 )
                return( 0 );
            if( pntWorld.X > _rgAdvanceAbs[_iGlyphTotal-1] + _rgAdvanceWidths[_iGlyphTotal - 1] )
                return( _iGlyphTotal );
            if( pntWorld.X < 0 )
                return( 0 );

            int iIndex = Array.BinarySearch<Int32>( _rgAdvanceAbs, 0, _iGlyphTotal, pntWorld.X );
            
            if( iIndex < 0 )
                iIndex = ~iIndex; // Most of the time we miss. The inverse is always the higher number.
            else
                iIndex+=1; // The only time it picks the correct element is if it's exact match. So add one.

            int iAdvanceHalf = _rgAdvanceAbs[iIndex - 1];
            iAdvanceHalf += (int)(( _rgAdvanceAbs[iIndex] - iAdvanceHalf ) / 2 );

            if( pntWorld.X < iAdvanceHalf )
                iIndex -= 1;

            return( iIndex );
        }

        /// <summary>
        /// Return the position of the glyph relative to an upper left position
        /// that would be 0, 0 for this cache element.
        /// </summary>
        /// <param name="iOffset"></param>
        /// <returns></returns>
        public virtual Point GlyphOffsetToPoint( int iOffset )
        {
            if( iOffset > _iGlyphTotal )
                iOffset = _iGlyphTotal;
            if( iOffset <= 0 )
                iOffset = 0;

            if( _rgAdvanceAbs == null )
                return( new Point( 0, Top ) );

            return( new Point( _rgAdvanceAbs[iOffset], 0 ) );
        } // end method

        /// <summary>
        /// Move left or right on this line.
        /// </summary>
        /// <param name="iIncrement">+/- number of glyphs to move.</param>
        /// <param name="iAdvance">Current graphics position on the line.</param>
        /// <param name="iOffset">Current logical position on the line.</param>
        /// <returns>True if able to move. False if positioning will move out of bounds.</returns>
        /// <remarks>TODO: In the future we must use the cluster info.</remarks>
        protected virtual bool NavigateHorizontal( int iDir, ref int iAdvance, ref int iOffset ) {
            int iNext = iOffset + iDir;

            if( iNext >= 0 && iNext <= _iGlyphTotal ) {
                iOffset = iNext;

                iAdvance = _rgAdvanceAbs[iOffset];
                return( true );
            }

            return( false );
        }

        /// <summary>
        /// Move up or down based on the previous advance. For a non-wrapped line it always fails
        /// to move internally.
        /// </summary>
        /// <param name="iIncrement">Direction of travel, positive is down, negative is up.</param>
        /// <param name="iAdvance">Previous "pixel" offset on a given line. The wrapped value.</param>
        /// <param name="iOffset">Closest character we can find to the given offset on a line above or below.</param>
        /// <returns>false, always since one cannot navigate vertically on a non-wrapped line.</returns>
        protected virtual bool NavigateVertical( int iDir, int iAdvance, ref int iOffset ) {
            return( false );
        }

        public bool Navigate( Axis eAxis, int iDir, ref int iAdvance, ref int iOffset ) {
            // See if we can navigate within the line we are currently at.
            switch( eAxis ) {
                case Axis.Horizontal:
                    return( NavigateHorizontal( iDir, ref iAdvance, ref iOffset ) );
                case Axis.Vertical:
                    return( NavigateVertical( iDir, iAdvance, ref iOffset ) );
            }

            throw new ArgumentOutOfRangeException( "expecting only horizontal or vertical" );
        }
        /// <summary>
        /// Get the minimum or maximum glyph position available on this line.
        /// </summary>
        /// <param name="iIncrement">0 or positive returns MAX, negative returns MIN.</param>
        /// <returns></returns>
        protected int OffsetHorizontalBound( int iIncrement ) {
            if( iIncrement >= 0 )
                return( _iGlyphTotal );

            return( 0 );
        }
        
        /// <summary>
        /// Find the nearest glyph offset to the given advance at the current line.
        /// </summary>
        /// <param name="iIncrement">Ignore the line wrap directive</param>
        /// <param name="iAdvance">Now many pixels to the right starting from left.</param>
        protected virtual int OffsetVerticalBound( int iIncrement, int iAdvance ) {
            int i = _iGlyphTotal;

            while( i > 0 && _rgAdvanceAbs[i] > iAdvance ) {
                --i;
            }

            return( i );
        }

        public int OffsetBound( Axis eAxis, int iIncrement, int iAdvance ) {
            switch( eAxis ) {
                case Axis.Horizontal:
                    return( OffsetHorizontalBound( iIncrement ) );
                case Axis.Vertical:
                    return( OffsetVerticalBound( iIncrement, iAdvance ) );
            }
            throw new ArgumentOutOfRangeException( "Not horizontal or vertical axis" );
        }

		/// <summary>
		/// Source of all color information for this cache element. Kewl 'eh?
		/// </summary>
		/// <remarks>
		/// If we don't parse, then Color contains selection information ONLY when there
		/// is a selection. Then we get only the selection colored. If there is no selection
		/// Color is empty and we use words. arrrgghgh. 
		/// Words and Formatting are more primative (parse data only) and never contain selection.
		/// We correct this problem in OnChangeFormatting().
		/// </remarks>
		/// <seealso cref="EnumColor(int)"/>
		/// <seealso cref="CacheWrapped.DiceAColor(IColorRange)"/>
		/// <seealso cref="OnChangeFormatting(List{int}, ICollection{ILineSelection})"/>
		public IEnumerator<IColorRange> GetEnumerator() {
			if (Color.Count > 0)
				return (Color.GetEnumerator());
			if (Words.Count > 0)
				return (Words.GetEnumerator());

			return (EnumColorEmpty());
	      //return( EnumBlack() ); // Comment out above and set this to debug things.
		}

		IEnumerator IEnumerable.GetEnumerator() {
			return( GetEnumerator() );
		}
	} // end class

}