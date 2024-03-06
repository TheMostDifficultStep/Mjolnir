using System;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.Xml;
using System.Drawing;
using System.Reflection;
using System.Collections.Generic;
using System.IO;

using SkiaSharp;

using Play.Drawing;
using Play.Interfaces.Embedding;
using Play.Rectangles;

namespace Mjolnir {
    public partial class MainWin {
		/// <summary>
		/// Implementation for the LayoutFlowSqure abstract class.
		/// </summary>
		/// <seealso cref="LayoutFlowSquare"/>
		public class LayoutFlowSquare_MainWin : LayoutFlowSquare {
			readonly MainWin _oControl;

			public LayoutFlowSquare_MainWin( MainWin oControl ) : base( CSS.None ) {
				_oControl = oControl ?? throw new ArgumentNullException();
			}

			public override int        Count  => _oControl._oDoc_ViewSelector.ElementCount;
			public override void       Clear() { }
			public override LayoutRect Item(int iIndex) {
				try {
					return _oControl._oDoc_ViewSelector[iIndex].Layout;
				} catch( Exception oEx ) {
					Type[] rgErrors = { typeof( NullReferenceException ),
										typeof( IndexOutOfRangeException ),
										typeof( ArgumentOutOfRangeException ) };
					if( rgErrors.IsUnhandled( oEx ) )
						throw;

					_oControl.LogError( null, "Square Layout Flow", "Problem accessing current a view's layout: " + iIndex.ToString() );

					return new LayoutRect( CSS.None );
				}
			}

			public override IEnumerator<LayoutRect> GetEnumerator() {
				foreach( ViewSlot oLine in _oControl._oDoc_ViewSelector ) {
					yield return oLine.Layout;
				}
			}
		} // End class
	}

    public static class MyExtensions {
        // https://docs.microsoft.com/en-us/windows/uwp/design/style/segoe-ui-symbol-font
        public const string strArrowsIn    = "\xe1d8";
        public const string strArrowsOut   = "\xe1d9";
        public const string strGoLeft      = "\xe100";
        public const string strGoRight     = "\xe101";
        public const string strGoParent    = "\xe197"; // wuz up arrow. "\xE183";
		public const string strPlay        = "\xe102";
		public const string strPause       = "\xe103";
		public const string strStop        = "\xe15b"; // e20d is nice too.
        public const string str3InchFloppy = "\xe105";
		public const string strNextView    = "\xe1c3"; // "\xe281"; // "\xe2fd";
		public const string strHome        = "\xe10f"; 
		public const string strRecycle     = "\xe117";

        [DllImport("user32.dll", CharSet = CharSet.Auto)] 
            public static extern bool DestroyIcon(IntPtr handle);
    } // End class

	/// <summary>
	/// This class lets us send MouseWheel messages to the window under the mouse cursor.
	/// That is not the normal behavior be we like it.
	/// </summary>
	/// <remarks>NOte: This is not likely to port to what ever Lunix thing we land in.</remarks>
    public class MouseWheelMessageFilter : IMessageFilter {
        [StructLayout(LayoutKind.Sequential)]
        private struct POINT {
            public int x;
            public int y;
        }

        [DllImport("user32.dll")]
        private static extern IntPtr WindowFromPoint([In] MouseWheelMessageFilter.POINT point);

        [DllImport("user32.dll")]
        public static extern int SendMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        private const int WM_MOUSEWHEEL = 0x020A;

        private uint LOWORD(IntPtr value) {
            return (uint)((((ulong)value.ToInt64()) & 0xffff));
        }

        private uint HIWORD(IntPtr value) {
            return (uint)((((ulong)value.ToInt64()) & 0xffff0000) >> 16);
        }

		/// <summary>
		/// Grab the mouse wheel event and dispatch it to the window under the cursor.
		/// </summary>
        public bool PreFilterMessage(ref Message m) {
            if (m.Msg == WM_MOUSEWHEEL)
            {
                uint screenX = LOWORD(m.LParam);
                uint screenY = HIWORD(m.LParam);

				POINT point = new MouseWheelMessageFilter.POINT {
					x = (int)screenX,
					y = (int)screenY
				};

				IntPtr hWnd = MouseWheelMessageFilter.WindowFromPoint(point);
                if (hWnd != IntPtr.Zero) {
					SendMessage(hWnd, (uint)m.Msg, m.WParam, m.LParam);
                }

                return true; // stop this message being dispatched
            }

            return false;
        }
    } // End class

    /// <summary>
    /// User AZDean on 
    /// http://stackoverflow.com/questions/3427696/windows-requires-a-click-to-activate-a-window-before-a-second-click-will-select
    /// No need to activate window before clicking. Simply look for any mouse UP event that is missing it's corresponding DOWN event. 
    /// </summary>
    public class MyMenuStrip : MenuStrip
    {
        const uint WM_LBUTTONDOWN = 0x201;
        const uint WM_LBUTTONUP   = 0x202;

        private bool down = false;

        protected override void WndProc(ref Message m)
        {
            if (m.Msg==WM_LBUTTONUP && !down) {
                m.Msg=(int)WM_LBUTTONDOWN; 
                base.WndProc(ref m);
                m.Msg=(int)WM_LBUTTONUP;
            }

            if (m.Msg==WM_LBUTTONDOWN) 
                down=true;
            if (m.Msg==WM_LBUTTONUP)   
                down=false;

            base.WndProc(ref m);
        }
    }

    public class ViewTypeMenuItem : ToolStripMenuItem {
        IPgViewType _oViewType;

        public ViewTypeMenuItem( IPgViewType oViewType, EventHandler delEvent ) : 
            base( oViewType.Name, null, delEvent )
        {
            _oViewType = oViewType;
        }

        public Guid ID {
            get{ return( _oViewType.ID ); }
        }
    }

    /// <summary>
    /// This is an implementation of the mysterious Keyboard class which is supposed to be in System.Windows.Input,
    /// but it isn't. Unless you're at v4 of the .net core and include some presentation manager stuff.
    /// But the heck with that. If I was using PM garbage I wouldn't need all this crap in the first place!
    /// Found on http://stackoverflow.com/questions/1100285/how-to-detect-the-currently-pressed-key
    /// which claims the code comes from... http://www.switchonthecode.com/tutorials/winforms-accessing-mouse-and-keyboard-state
    /// But that link seems to be dead as of 1/27/2017. I fixed up the class to be all static.
    /// </summary>
    public static class Keyboard
    {
        [Flags]
        private enum KeyStates
        {
            None = 0,
            Down = 1,
            Toggled = 2
        }

        [DllImport("user32.dll", CharSet = CharSet.Auto, ExactSpelling = true)]
        private static extern short GetKeyState(int keyCode);

        private static KeyStates GetKeyState(Keys key)
        {
            KeyStates state = KeyStates.None;

            short retVal = GetKeyState((int)key);

            //If the high-order bit is 1, the key is down
            //otherwise, it is up.
            if ((retVal & 0x8000) == 0x8000)
            state |= KeyStates.Down;

            //If the low-order bit is 1, the key is toggled.
            if ((retVal & 1) == 1)
            state |= KeyStates.Toggled;

            return state;
        }

        public static bool IsKeyDown(Keys key)
        { 
            return KeyStates.Down == (GetKeyState(key) & KeyStates.Down);
        }

        public static bool IsKeyToggled(Keys key)
        { 
            return KeyStates.Toggled == (GetKeyState(key) & KeyStates.Toggled);
        }
    } // End class

	public static class ResourceLoader {
		public static SKBitmap GetImageResource( Assembly oAssembly, string strResourceName ) {
			try {
                // Let's you peep in on all of them! ^_^
                // string[] rgStuff = oAssembly.GetManifestResourceNames();

				using( Stream oStream = oAssembly.GetManifestResourceStream( strResourceName )) {
					return( SKBitmap.Decode( oStream ) );
				}
			} catch( Exception oEx ) {
				Type[] rgErrors = { typeof( NullReferenceException ), 
									typeof( ArgumentNullException ),
									typeof( ArgumentException ),
									typeof( FileLoadException ),
									typeof( BadImageFormatException ),
									typeof( NotImplementedException ) };
				if( rgErrors.IsUnhandled( oEx ) )
					throw;

				throw new InvalidOperationException( "Could not retrieve given image resource : " + strResourceName );
			}
		}
	}

	/// <summary>
	/// Show that we can support aborted load document sites, we use this object to show that
	/// the document site is being bookmarked. While technically you might think this would be
	/// a document's view responsibility, that would be a lot of work on a lot of documents. This is a
	/// general purpose view that will work with any zombied site. This way we can see the zombied
	/// document site in the views menu item. Else there is no way to see the zombie short of looking
	/// in the actual .pvs file.
	/// </summary>
	public class ViewBookmark :
		Control,
		IPgParent ,
		IPgLoad<XmlElement>,
		IPgSave<XmlDocumentFragment>,
		IPgCommandView
	{
		readonly IPgViewSite _oViewSite;
		readonly static Guid _oCatagory = new Guid("{c1d9a247-c00c-4e98-89e4-390ca7bbcaea}");

		protected LayoutImage Bookmark;

		public ViewBookmark( IPgViewSite oViewSite ) {
			_oViewSite = oViewSite ?? throw new ArgumentNullException();
			// TODO: Could move this to the program so we don't duplicate loads.

			Icon = ResourceLoader.GetImageResource( Assembly.GetExecutingAssembly(), 
				                                    "Mjolnir.Content.icons8-bookmark-book-512.png" );
			Bookmark   = new LayoutImage( Icon );
		}

        protected override void Dispose( bool disposing ) {
			if( disposing ) {
				Bookmark.Dispose();
			}
            base.Dispose(disposing);
        }

        public IPgParent Parentage => _oViewSite.Host;
		public IPgParent Services  => _oViewSite.Host.Services;
		public string    Banner    => "Bookmark";
		public Guid      Catagory  => _oCatagory;
		public bool      IsDirty   => false; // Never can get dirty.
		public SKBitmap  Icon      { get; }

		protected override void OnPaint(PaintEventArgs e) {
			base.OnPaint(e);

			try {
				Bookmark.Paint( e.Graphics );
			} catch( NullReferenceException ) {
				_oViewSite.LogError( "Bookmark Paint", "Can't paint bitmap" );
			}
		}

		protected override void OnSizeChanged(EventArgs e) {
			base.OnSizeChanged(e);

			Bookmark.SetRect( 0, 0, Width, Height );
			Invalidate();
		}

		public object Decorate(IPgViewSite oBaseSite,Guid sGuid) {
			return null;
		}

		public bool Execute(Guid sGuid) {
			return false;
		}

		public bool InitNew() {
			return true;
		}

		public bool Load(XmlElement oStream) {
			return true;
		}

		public bool Save(XmlDocumentFragment oStream) {
			return true;
		}
	}

    /// <summary>
    /// This is an experimental view that shows all the views with a toolbar for creating new document.
    /// I stopped using it because I was having selected view issues. I might bring it back yet.
    /// </summary>
	public class ViewEverything :
		Control,
		IPgParent,
		IPgLoad,
		IPgCommandView
	{
		public static readonly Guid  _oCatagory  = new Guid("{b2765000-58f3-48fa-8d1f-3bcbcee361e4}");

		readonly IPgViewSite _oViewSite;
		readonly LayoutStack _oLayoutBot = new LayoutStackHorizontal( 50, 10 ) { Spacing = 5 };
		readonly LayoutStack _oLayout    = new LayoutStackVertical( ) { Spacing = 5 };
		readonly MainWin     _oHost;

		public ViewEverything( IPgViewSite oViewSite ) {
			_oViewSite = oViewSite ?? throw new ArgumentNullException();
			// TODO: Could move this variable to the program so we don't duplicate loads.
			Icon       = SKImageResourceHelper.GetImageResource( Assembly.GetExecutingAssembly(), 
				                                               "Mjolnir.Content.icons8-house-96.png" );
			_oHost     = _oViewSite.Host as MainWin ?? throw new InvalidOperationException( "must be hosted by MainWin" );
		}

		public IPgParent Parentage => _oViewSite.Host;
		public IPgParent Services  => _oViewSite.Host.Services;
		public string    Banner    => "Overview";
		public Guid      Catagory  => _oCatagory;
		public bool      IsDirty   => false; // Never can get dirty.
		public SKBitmap  Icon    { get; }

		protected string[] IconNames {
			get {
				string[] rgIconNames = { 
					"icons8-geography-96.png",
					"icons8-txt-96.png",
					"icons8-smart-playlist-96.png",
					"icons8-heart-outline-96.png" };

				return rgIconNames;
			}
		}

		public bool InitNew() {
			_oLayout.Add( new LayoutRect( LayoutRect.CSS.None ) ); // Place holder spot for now.
			_oLayout.Add( _oLayoutBot );

			_oLayoutBot.Add( new LayoutRect( LayoutRect.CSS.None ) );
			foreach( string strName in IconNames ) {
				string strPath = "Mjolnir.Content." + strName;
				_oLayoutBot.Add( new LayoutImage( ResourceLoader.GetImageResource( Assembly.GetExecutingAssembly(), strPath ), LayoutRect.CSS.Flex ) );
			}
			_oLayoutBot.Add( new LayoutRect( LayoutRect.CSS.None ) );

			Invalidate();

			return true;
		}

		protected override void OnPaint(PaintEventArgs e) {
            // First paint our whole screen.
            SmartRect oRectWhole = new SmartRect( LOCUS.UPPERLEFT, 0, 0, this.Width, this.Height );

			try {
				e.Graphics.FillRectangle( Brushes.White, oRectWhole.Rect ); 
			} catch( Exception oEx ) {
				Type[] rgErrors = { typeof( ArgumentNullException ),
									typeof( ArgumentException ),
									typeof( NullReferenceException ) };
				if( rgErrors.IsUnhandled( oEx ) )
					throw;
			}

			try {
				_oLayoutBot.Paint( e.Graphics );
			} catch( NullReferenceException ) {
				_oViewSite.LogError( "Everything", "Can't paint." );
			}
		}

		protected override void OnSizeChanged(EventArgs e) {
			base.OnSizeChanged(e);

			_oLayout.SetRect( 0, 0, Width, Height );
			_oLayout.LayoutChildren();

			Invalidate();
		}

		protected override void OnGotFocus(EventArgs e) {
			base.OnGotFocus(e);

			_oViewSite.EventChain.NotifyFocused( fSelect:true );
		}

		public object Decorate(IPgViewSite oBaseSite,Guid sGuid) {
			return null;
		}

		public bool Execute(Guid sGuid) {
			return false;
		}
	}
}
