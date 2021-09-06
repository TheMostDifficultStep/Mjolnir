using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
//using System.Web.MimeMapping;  BCL in .NET Framework 4.5. Need to find this.

using Play.Interfaces.Embedding;
using Play.Parse;

namespace Play.Edit {
	[Obsolete( "You can use linq now." )]
    public static class MyExtensions {
		public static bool Find(this bool[] rgList, bool fContinue, out int iIndex ) {
			if( fContinue ) {
				for( iIndex=0; iIndex< rgList.Length; ++iIndex ) {
					if (rgList[iIndex]) {
						return (true);
					}
				}
			}
			iIndex = -1;
			return (false);
		}

		public static void Clear(this int[] rgList) {
			Array.Clear(rgList,0,rgList.Length);
		}
	}

    // BUG: This is looking a bit bogus these days. Need to rethink.
    public delegate void TextLocate( EditWin sender, int iStream );
    
    public enum RefreshNeighborhood {
        SCROLL,
        CARET
    }

    public abstract class CacheManagerAbstractSite :
        IPgBaseSite
    {
        public abstract IPgParent Host { get; }
        public abstract void   LogError( string s1, string s2, bool fShow=true );
        public virtual  void   Notify( ShellNotify eEvent ) {}

        public abstract void Neighborhood( RefreshNeighborhood eHood, out Line oLine, out int iOffset );
        public abstract Line GetLine  ( int iLineAt );
        public abstract bool IsWrapped( int iLineAt );

        public abstract ICollection<ILineSelection> Selections{ get; }
        public abstract void OnRefreshComplete();

        public virtual FILESTATS FileStatus { get { return( FILESTATS.UNKNOWN ); } }

        public virtual void WordBreak( Line oLine, ICollection<IPgWordRange> rgWords ) { }
    }

    public static class WM {
        [Obsolete]public const Int32 WM_USER = 1024; // 0x400 
        public const Int32 WM_APP = 32768; // 0x8000				
    }

    static class HRESULT {
        public const Int32 E_OUTOFMEMORY = -2147024882; // 0x8007000E;
        public const Int32 E_INVALIDARG  = -2147024809; // 0x80070057;
        public const Int32 E_UNEXPECTED  = -2147418113; // 0x8000FFFF;
        public const Int32 E_NOTIMPL     = -2147467263; // 0x80004001;
        public const Int32 E_NOINTERFACE = -2147467262; // 0x80004002;
        public const Int32 E_POINTER     = -2147467261; // 0x80004003;
        public const Int32 E_HANDLE      = -2147024890; // 0x80070006;
        public const Int32 E_ABORT       = -2147467260; // 0x80004004;
        public const Int32 E_FAIL        = -2147467259; // 0x80004005;
        public const Int32 E_PENDING     = -2147483638; // 0x8000000A;
        public const Int32 S_OK          = 0;
        public const Int32 S_FALSE       = 1;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    public struct AbcFloat {
        public float flA;
        public float flB;
        public float flC;

        public float Default {
            set{ flA = value; flB = value; flC = value; }
        }
    };

    enum BRUSHSTYLES : uint {
        BS_SOLID=            0,
        BS_NULL=             1,
        BS_HOLLOW=           1,
        BS_HATCHED=          2,
        BS_PATTERN=          3,
        BS_INDEXED=          4,
        BS_DIBPATTERN=       5,
        BS_DIBPATTERNPT=     6,
        BS_PATTERN8X8=       7,
        BS_DIBPATTERN8X8=    8,
        BS_MONOPATTERN=      9
    }

    public enum BKMODES : int {
        TRANSPARENT=1,
        OPAQUE     =2
    }

    [FlagsAttribute]
    enum PENSTYLES : uint {
        PS_SOLID=         0x00000000,
        PS_DASH=          0x00000001,
        PS_DOT=           0x00000002,
        PS_DASHDOT=       0x00000003,
        PS_DASHDOTDOT=    0x00000004,
        PS_NULL=          0x00000005,
        PS_INSIDEFRAME=   0x00000006,
        PS_USERSTYLE=     0x00000007,
        PS_ALTERNATE=     0x00000008,
        PS_STYLE_MASK=    0x0000000f,

        PS_ENDCAP_ROUND=  0x00000000,
        PS_ENDCAP_SQUARE= 0x00000100,
        PS_ENDCAP_FLAT=   0x00000200,
        PS_ENDCAP_MASK=   0x00000f00,

        PS_JOIN_ROUND=    0x00000000,
        PS_JOIN_BEVEL=    0x00001000,
        PS_JOIN_MITER=    0x00002000,
        PS_JOIN_MASK=     0x0000f000,

        PS_COSMETIC=      0x00000000,
        PS_GEOMETRIC=     0x00010000,
        PS_TYPE_MASK=     0x000f0000
    }

    enum HATCHSTYLES : uint {
        HS_HORIZONTAL=       0,
        HS_VERTICAL=         1,
        HS_FDIAGONAL=        2,
        HS_BDIAGONAL=        3,
        HS_CROSS =           4,
        HS_DIAGCROSS=        5
    }

    unsafe public class User32 {
        [DllImport( "User32.DLL", EntryPoint = "GetDC", SetLastError = true )]
        public static extern IntPtr GetDC(IntPtr hWnd);
        [DllImport( "User32.DLL", EntryPoint = "ReleaseDC", SetLastError = true )]
        public static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

        [DllImport( "User32.DLL", EntryPoint = "CreateCaret", SetLastError = true )]
        public static extern bool CreateCaret(IntPtr hWnd, IntPtr hBitmap, Int32 nWidth, Int32 nHeight);
        [DllImport( "User32.DLL", EntryPoint = "DestroyCaret", SetLastError = true )]
        public static extern bool DestroyCaret();
        [DllImport( "User32.DLL", EntryPoint = "SetCaretPos", SetLastError = true )]
        public static extern bool SetCaretPos(Int32 X, Int32 Y);
        [DllImport( "User32.DLL", EntryPoint = "HideCaret", SetLastError = true )]
        public static extern bool HideCaret(IntPtr hWnd);
        [DllImport( "User32.DLL", EntryPoint = "ShowCaret", SetLastError = true )]
        public static extern bool ShowCaret(IntPtr hWnd);
        [DllImport("User32.DLL", EntryPoint = "TabbedTextOutW", SetLastError = true)]
        public static extern long TabbedTextOut( IntPtr hDC, Int32 iXStart, Int32 iYStart, char* pwcStart, Int32 iLength,
                                                  Int32 iTabPosns, [In]Int32[] lpTabs, Int32 iTabOrigin );
        [DllImport("User32.DLL", EntryPoint = "PostMessage", SetLastError = true)]
        public static extern bool PostMessage( IntPtr hWnd, uint uiMsg, IntPtr lParam, IntPtr WParam );
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    public class XFORM {
       public float eM11;
       public float eM12;
       public float eM21;
       public float eM22;
       public float eDx;
       public float eDy; 
    }; 

    unsafe public class Gdi32 {
        public static float flInchesPerPoint = .013837F; // Approx 1/72

        /// <summary> 
        /// The intensity for each argument is in the range 0 through 255. 
        /// If all three intensities are zero, the result is black. If all 
        /// three intensities are 255, the result is white. 
        /// </summary>
        /// <remarks>
        /// This function is directly from WinGDI.h
        /// </remarks>
        public static UInt32 SetRGB(byte r, byte g, byte b) {
            return ( (UInt32)( r | ( (UInt16)g << 8 ) ) | ( ( (UInt32)b << 16 ) ) );
        }

        [DllImport("gdi32.DLL", EntryPoint = "GetTextExtentExPointW", SetLastError = true)]
        public static extern bool GetTextExtentExPoint( IntPtr hDC, [In] char* pwcStart, Int32 iLength, Int32 iMaxExtent, Int32 * iMaxCount, [Out] Int32[] rgDX, [Out] Point[] pntSize );
        [DllImport("gdi32.DLL", EntryPoint = "TextOutW", SetLastError = true)]
        public static extern bool TextOut( IntPtr hDC, Int32 iXStart, Int32 iYStart, char* pwcStart, Int32 iLength );
        [DllImport("gdi32.DLL", EntryPoint = "SelectObject", SetLastError = true)]
        public static extern IntPtr SelectObject(IntPtr hDC, IntPtr hGDIObject);
        [DllImport( "gdi32.DLL", EntryPoint = "GetCharABCWidthsFloatW", SetLastError = true )]
        public static extern bool GetCharABCWidthsFloat(IntPtr hDC, UInt32 iFirst, UInt32 iLast, [In, Out] AbcFloat[] rgAbc);
        [DllImport( "gdi32.DLL", EntryPoint = "CreateSolidBrush", SetLastError = true )]
        public static extern IntPtr CreateSolidBrush(UInt32 argbColor);
        [DllImport( "gdi32.DLL", EntryPoint = "DeleteObject", SetLastError = true )]
        public static extern bool DeleteObject(IntPtr ipBrush);
        [DllImport( "gdi32.DLL", EntryPoint = "SetTextColor", SetLastError = true )]
        public static extern UInt32 SetTextColor(IntPtr hDC, UInt32 uiColor);
        [DllImport( "gdi32.DLL", EntryPoint = "SetBkColor", SetLastError = true )]
        public static extern UInt32 SetBackColor(IntPtr hDC, UInt32 uiColor);

        [DllImport("gdi32.DLL", EntryPoint = "MoveToEx", SetLastError = true)]
        public static extern bool MoveToEx( IntPtr hDC, Int32 iX, Int32 iY, void* pPoint );
        [DllImport("gdi32.DLL", EntryPoint = "LineTo", SetLastError = true)]
        public static extern bool LineTo( IntPtr hDC, Int32 iX, Int32 iY );
        [DllImport("gdi32.DLL", EntryPoint = "ExtCreatePen", SetLastError = true)]
        public static extern IntPtr ExtCreatePen( UInt32 uPenStyle, UInt32 uPenWidth, LOGBRUSH oBrushAttribs, UInt32 uCustomStyleCount, UInt32[] rgCustomStyles);
        [DllImport("gdi32.DLL", EntryPoint = "SetBkMode", SetLastError = true)]
        public static extern BKMODES SetBkMode( IntPtr hDC, BKMODES eBkMode );
        [DllImport("gdi32.dll", EntryPoint = "CreateRectRgn", SetLastError = true )]
        public static extern IntPtr CreateRectRgn( Int32 ulX, Int32 ulY, Int32 rbX, Int32 rbY );
        [DllImport("gdi32.dll", EntryPoint = "SelectClipRgn", SetLastError = true )]
        public static extern Int32 SelectClipRgn( IntPtr hDC, IntPtr hRgn );

        public static UInt32 GM_ADVANCED = 2;

        [DllImport( "gdi32.DLL", EntryPoint= "SetGraphicsMode", SetLastError= true)]
        public static extern UInt32 SetGraphicsMode( IntPtr hDC, UInt32 uiMode );
        [DllImport("gdi32.DLL", EntryPoint = "SetWorldTransform", SetLastError = true)]
        public static extern bool SetWorldTransform( IntPtr hDC, [In] XFORM pXForm );
        [DllImport("gdi32.dll", EntryPoint = "GetCharWidth32W", SetLastError = true)]
        public static extern bool GetCharWidth32( IntPtr hDC, UInt32 uiStart, UInt32 uiLast, Int32 * piBuffer );
    }

    public abstract class Scope< T > : IDisposable {
        protected T      _hOwner;
        protected IntPtr _hHandle;

        // Don't bother with a finalizer until we have ref counting
        // on the DisplayContext.

        public IntPtr Handle {
            get {
                return (_hHandle);
            }
        }

        public abstract void Dispose();
    }

    /// <summary>
    /// A little struct to deal with handles. This probably exists somewhere in .net.
    /// But I don't know about it. Since we typically only want this used while in
    /// scope I'm making it a class so there's no reference.
    /// </summary>
    public class DisplayContext : Scope<IntPtr>
    {
        public DisplayContext( Control oWindow ) {
            _hOwner = oWindow.Handle;

            if( _hOwner == IntPtr.Zero )
                throw new ArgumentException( "Couldn't retrieve windows handle from control." );

            _hHandle = User32.GetDC( oWindow.Handle );

            if( _hHandle == IntPtr.Zero )
                throw( new ArgumentException( "Couldn't retrieve DC from the control's handle." ) );
        }

        public override void Dispose() {
            if( _hHandle != IntPtr.Zero ) {
                User32.ReleaseDC( _hOwner, _hHandle );
                _hHandle = IntPtr.Zero;
            }
        }
    }

    public class GraphicsContext : Scope<Graphics>
    {
        public GraphicsContext( Graphics oG ) {
            _hOwner  = oG;
            _hHandle = _hOwner.GetHdc();
        }

        public override void Dispose() {
            if( _hHandle != IntPtr.Zero ) {
                _hOwner.ReleaseHdc();
                _hHandle = IntPtr.Zero;
            }
        }
    }

    public class ItemContext : Scope<IntPtr>
    {
        readonly IntPtr _hItemOld;

        public ItemContext( IntPtr hDC, IntPtr hItemNew ) {
            _hOwner   = hDC;
            _hHandle  = hItemNew;
            _hItemOld = Gdi32.SelectObject( _hOwner, hItemNew );
        }

        public override void Dispose() {
            if( _hHandle != IntPtr.Zero ) {
                Gdi32.SelectObject( _hOwner, _hItemOld );
                _hHandle = IntPtr.Zero;
            }
        }
    }

    /// <summary>
    /// An object to hold my pen. Right now it's just for my red dashed pen.
    /// I can add some color params and such later if needed.
    /// </summary>
    public class DashedPen : IDisposable {
        IntPtr _hPen;

        public DashedPen() {
            _hPen = Gdi32.ExtCreatePen( (uint)( PENSTYLES.PS_COSMETIC | PENSTYLES.PS_ALTERNATE ), 
										1, new LOGBRUSH( (uint)BRUSHSTYLES.BS_SOLID, 
														 Gdi32.SetRGB( 255, 0, 0 ),
														 0
													   ),
										0, null );
        }

        public IntPtr Handle => _hPen;

		/// <remarks>BUG: Hmmm... I should use the unmanaged object pattern. Double check this one.</remarks>
        ~DashedPen() {
            Dispose();
        }

        public void Dispose() {
            if( _hPen != IntPtr.Zero )
                Gdi32.DeleteObject( _hPen );

            _hPen = IntPtr.Zero;
        }
    }
}
