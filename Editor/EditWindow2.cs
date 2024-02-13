using System;
using System.Collections.Generic;
using System.Drawing;
using System.Xml;
using System.Runtime.InteropServices;
using System.Linq;
using System.Reflection;
using System.Diagnostics;
using System.IO;
using System.Xml.XPath;
using System.Text;
using System.Security;
using System.Windows.Forms;
using System.Collections;

using SkiaSharp;
using SkiaSharp.Views.Desktop;

using Play.Drawing;
using Play.Interfaces.Embedding;
using Play.Rectangles;
using Play.Controls;
using Play.Parse;
using Play.Parse.Impl;

namespace Play.Rectangles {
	/// <summary>
	/// It's a little bit weird, but I can't seem to get this to work in the "Drawing" project, 
    /// where I tried including System.Windows.Forms. However, here it works? Don't know why
    /// But since anyone doing anything is going to include the "Editor" project. It's OK.
	/// </summary>
    /// <remarks>Move into an editor window support file.</remarks>
	public class LayoutControl : LayoutRect {
		protected Control _oControl;

		public LayoutControl( Control oView, LayoutRect.CSS eUnits, uint uiTrack ) : base( eUnits, uiTrack, 0 ) {
			_oControl = oView ?? throw new ArgumentNullException();

			this.SizeEvent += OnSizeEvent; // BUG: Looks leak worthy. ^_^;;
		}

		public LayoutControl( Control oView, LayoutRect.CSS eUnits ) : base( eUnits, 0, 0 ) {
			_oControl = oView ?? throw new ArgumentNullException();

			this.SizeEvent += OnSizeEvent;
		}

		protected virtual void OnSizeEvent(SmartRect o) {
			_oControl.Bounds = this.Rect;
		}

		public override uint TrackDesired(TRACK eParentAxis, int uiRail) {
            if( Units == CSS.Pixels ) // Go with the set track size.
                return Track;

			Size szProposed = eParentAxis == TRACK.HORIZ ? new Size( 10000, uiRail ) : new Size( uiRail, 10000 );
			Size szPrefered = _oControl.GetPreferredSize( szProposed );

			int  iTrack     = eParentAxis == TRACK.HORIZ ? szPrefered.Width : szPrefered.Height;
            
			return (uint)iTrack;
		}

		public Control Guest {
			get { return _oControl; }
		}

        public override bool Hidden {
            // Hmmm... I'd like to use the Control value, but if I do, everything
            // is hidden. But if I used the Layout's value all is well. So stick
            // with that for now.
            //get { return _oControl.Visible; }
            set {
                if( value ) {
                    _oControl.Hide();
                } else {
                    _oControl.Show();
                }
            }
            get {
                return !_oControl.Visible;
            }
        }
    }

}

namespace Play.Edit {
    // https://www.freetype.org/freetype2/docs/documentation.html
	public class LayoutText : LayoutRect {
		public FTCacheLine Cache { get; }
		public int         ColumnIndex { get; }

		public LayoutText( FTCacheLine oCache, LayoutRect.CSS eUnits, uint uiTrack, int iCol ) :
			base( eUnits, uiTrack, 1 ) 
		{
			Cache = oCache ?? throw new ArgumentNullException();
			ColumnIndex = iCol;
		}

		/// <summary>
		/// I need to rethink the implementation of this.
		/// BUG!! : The width can be floating point now!!
		/// </summary>
		/// <param name="eParentAxis"></param>
		/// <param name="uiRail"></param>
		public override uint TrackDesired(TRACK eParentAxis, int uiRail) {
			float flValue = 0;
            
            if( eParentAxis == TRACK.HORIZ ) {
                flValue = Cache.UnwrappedWidth + 10; // BUG: need to add a bit, else it wraps.
			} else {
 				Cache.OnChangeSize( uiRail );
				flValue = Cache.Height;
			}

			if( flValue < 0 )
				return 0;

			return (uint)flValue;
		}

        public void Paint( SKCanvas skCanvas, IPgStandardUI2 oStdUI ) {
            if( !Hidden ) { 
				Cache.Render( skCanvas, oStdUI, new PointF( Left, Top ) );
            }
        }

		public override void Invalidate() {
			Cache.Invalidate();
		}
	}

    /// <summary>
    /// Move this to the forms package after I finish updating the edit window properties.
    /// </summary>
    public interface IPgFormBulkUpdates : IDisposable {
        int  AddProperty( string strPropertyName );
        void SetValue( int iIndex, string strValue );
        void SetLabel( int iIndex, string strLabel );
    }

    public delegate void TextLocate2( EditWindow2 sender, int iStream );

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    public struct FTGlyphPos {
        public short left;
        public short top;
        public short advance_em_x;
        public short advance_em_y;
        public short delta_lsb; // These two I haven't seen yet.
        public short delta_rsb;
    };

    /// <summary>
    /// This object is almost like the FTGlyphPos except, I'm going to convert the
    /// TT_Pos freetype EM value into a float, so I don't need to shift bits 
    /// everywhere outside of this module.
    /// </summary>
    public struct PgGlyphPos {
        public short left;
        public short top;
        public float advance_x;
        public float advance_y;
        public float delta_lsb; // These two I haven't seen yet.
        public float delta_rsb;

        public override string ToString() { 
            return "left: " + left.ToString() + " adv_x: " + advance_x.ToString();
        }
    }

    /// <summary>
    /// I bounce around on whether there should be a code point on this structure. It 
    /// depends on whether there is one glyph per code point. Need to understand better
    /// clusters and how they relate to UTF-32. It might be clusters define the relationship
    /// to a sequence of UTF-8. Which then makes sense. UTF-32 clusters would always be 1-1.
    /// Ok, this explains it...
    /// https://en.wikipedia.org/wiki/Unicode_equivalence
    /// Diacritics are examples of letters that can be both precomposed
    /// or two glyphs combined. So while in the NON precompsed case we'll have TWO code points, 
    /// we'll see only ONE character on the screen. 
    /// So my notion of PgCluster is the right approach. This represents a single editable unit.
    /// So the Glyph could be assigned to a code point, but we need a composition object to track
    /// the resulting multi code point object. Note this is different than the variable length
    /// multi byte values of UTF8 that can make up a codepoint! -_-;;
    /// </summary>
    public interface IPgGlyph {
        UInt32     FaceIndex   { get; } // index to the face index from freetype.
        UInt32     Glyph       { get; }
        SKBitmap   Image       { get; }
        PgGlyphPos Coordinates { get; }
        UInt32     CodePoint   { get; }
        int        CodeLength  { get; set; } // Encoded word length either utf16 or utf8 depending on implementation.
    }

    /// <summary>
    /// So it's a bit of a misnomer. This interface will have a primary font that it supports.
    /// HOWEVER, if the codepoint is not supported in the font, the system will scan other loaded
    /// faces to see if one will suffice and a font of the appropriate size will be generated.. 
    /// </summary>
    public interface IPgFontRender {
        uint     LineHeight { get; } // Ascenders + decenders.
        IPgGlyph GetGlyph( UInt32 uiCodePoint );
    }

    public interface IPgStandardUI2 : IPgStandardUI {
        ushort        FaceCache       ( string strFilePath ); // Enter the requested face
        uint          FontCache       ( ushort uiFaceID, uint uiHeightInPoints, SKPoint skResolution );
        IPgFontRender FontRendererAt  ( uint uiRenderID );
        IPgFontRender FontStandardAt  ( string strName, SKSize skResolution );
        SKColor       ColorsStandardAt( StdUIColors eColor );

        SKColor       GrammarTextColor( int iIndex ); // Index to the master grammar indexed colors.
    }

    public abstract class DataStream2<T> : DataStream<T> {
        protected int _iPos = 0;

        public abstract int  Count    { get; }
        public override int  Position { get =>_iPos; set { _iPos = value; } }

        public override bool InBounds( int p_iPos ) {
            if( p_iPos < 0 )
                return false;
            if( p_iPos >= Count )
                return false;

            return true;
        }

        public T Read() {
            return this[_iPos++];
        }

        /// <summary>
        /// A handful of functions use this on DataStream[T] so while I hate to add this
        /// to DataStream2, inheriting from DataStream makes this more useful. I'll look
        /// Into removing this method in the future.
        /// </summary>
		public override string SubString( int iPos, int iLen ) {
            throw new NotImplementedException( "No substring in DataStream2<T>" );
        }
    }

    public class ByteStream : DataStream2<byte>, IPgDataStream<byte> {
        public List<byte> ByteList { get; }

        public override int  Count          => ByteList.Count;
        public override byte this[int iPos] => ByteList[_iPos];

        public ByteStream( List<byte> rgBytes ) {
            ByteList = rgBytes ?? throw new ArgumentNullException();
        }
    }

    public class CharStream : DataStream2<char>, IPgDataStream<char> {
        public Line Line { get; }

        public override int  Count          => Line.ElementCount;
        public override char this[int iPos] => Line[iPos];

        public CharStream( Line oLine ) {
            Line = oLine ?? throw new ArgumentNullException();
        }

        public CharStream( Line oLine, int iOffset ) {
            Line     = oLine ?? throw new ArgumentException( "Line is null on line range" );
            Position = iOffset;
        }

		public override string SubString( int iPos, int iLen ) {
            return Line.SubString( iPos, iLen );
        }
    }

    public class CharStream2 : DataStream2<char>, IPgDataStream<char> {
        public Line Line { get; }

        protected Extent Bounds { get; }

        public override int  Count          => Line.ElementCount;
        public override char this[int iPos] => Line[iPos];

        public CharStream2( Line oLine ) {
            Line   = oLine ?? throw new ArgumentNullException();
            Bounds = new Extent( 0, oLine.ElementCount );
        }
        public CharStream2( Line oLine, IMemoryRange oRange ) {
            Line   = oLine ?? throw new ArgumentNullException();

            if( oRange == null ) {
                Bounds = new Extent( 0, oLine.ElementCount );
            } else {
                Bounds = new Extent( oRange.Offset, oRange.Offset + oRange.Length );
            }

            if( Bounds.Start < 0 )
                throw new ArgumentOutOfRangeException( "Start is too low" );
            if( Bounds.Stop > Line.ElementCount )
                throw new ArgumentOutOfRangeException( "Stop is too high" );
        }
        public override bool InBounds( int p_iPos ) {
            if( p_iPos  < Bounds.Start )
                return false;
            if( p_iPos >= Bounds.Stop )
                return false;

            return true;
        }

		public override string SubString( int iPos, int iLen ) {
            return Line.SubString( iPos, iLen );
        }
    }

    public delegate void HyperLink ( Line oLine, IPgWordRange oRange );

    public partial class EditWindow2 : 
        SKControl, 
		IPgLoad<XmlElement>,
		IPgSave<XmlDocumentFragment>,
        IPgCommandView, // TODO: Consider moving this behavior to the EditPresentation shell window.
        IPgTextView,
		IPgTools,
        ILineEvents,
        IPgSelectionHelper,
        IEnumerable<ILineRange>,
        IReadableBag<Line>
    {
        public static readonly Guid ViewType = new Guid( "{B9218737-4EC6-4E5F-BF2A-D41949CD07DA}" );
        
        protected readonly BaseEditor     _oDocument;   // Reference to the line editor we are using. 
        protected readonly IPgViewNotify  _oViewEvents; // Our site from the window manager (view interface).
        protected readonly IPgViewSite    _oSiteView;   // Our site from the window manager (base interface).
		protected readonly IPgStandardUI2 _oStdUI;

        int          _iCaretColumn  = 0; // This is the Cache Map Column, NOT the layout column.
        float        _iAdvance      = 0; // Horizontal prefered position of cursor, world units.
        SmartRect    _rctDragBounds = null; // TODO: Move this into the selector.
        SizeF        _szScrollBars  = new SizeF( .1875F, .1875F );
        public bool ScrollVisible { get; set; } = true;

        protected          LayoutRect            _rctCheques; // TODO: Move this to the subclass eventually.
        protected readonly LayoutRect            _rctTextArea = new LayoutRect( LayoutRect.CSS.None ); // Not same as the CacheMan text area!!
        protected readonly LayoutStackHorizontal _rgLayout    = new LayoutStackHorizontal() { Spacing = 5, Units = LayoutRect.CSS.Flex};
        protected readonly List<SmartRect>       _rgCacheMap   = new (); // We'll phase out _rctTextArea if we can.

        // Possible to change if move window from one screen to another. Right now only init at start.
        public SKPoint DPI { get; protected set; } // Might change to float if this is a problem.

        // see System.Collections.ReadOnlyCollectionBase for readonly collections.
        readonly static Keys[] _rgHandledKeys = { Keys.PageDown, Keys.PageUp, Keys.Down,
                                                  Keys.Up, Keys.Right, Keys.Left, Keys.Back,
                                                  Keys.Delete, Keys.Enter, Keys.Tab,
                                                  Keys.Control | Keys.A, Keys.Control | Keys.F };

        readonly ICollection<ILineSelection> _rgSelectionTypes = new List<ILineSelection>( 3 );

        protected readonly LineRange     _oLastCursor = new LineRange(); // A spare for use with the hyperlink stuff.
        protected readonly CacheManager2 _oCacheMan;
        protected          bool          _fReadOnly;
        protected readonly bool          _fSingleLine; // Little hack until I make single line editors.

        /// <summary>
        /// This is the collection of hyperlink callbacks. Any Word Range found
        /// will be matched against the statename and the hyper link callback.
        /// </summary>
        public Dictionary<string, HyperLink> HyperLinks { get; } = new Dictionary<string, HyperLink>();

        // These values must be updated on edits which will destroy list nodes.
        readonly TextSelector  _oTextSelector;

        /// <seealso cref="EditWindow2.Caret"/>
        protected          CaretPosition CaretPos { get; set; }

        readonly ScrollBar2 _oScrollBarVirt;

        public event Navigation LineChanged;

		IPgAnonymousWorker    _oMorse;
		readonly List<string> _rgTools = new List<string>();
		protected int         _iSelectedTool = 0;

		public IPgParent Parentage    => _oSiteView.Host;
        public object    DocumentText => _oDocument; // IPgSelectionHelper
		public IPgParent Services     => Parentage.Services;

        protected class ChooserHyperLink : IPgWordRange
        {
            public ChooserHyperLink() {
            }

            public bool IsWord => true;
            public bool IsTerm => true;

            public string StateName => "chooser";
            public int    ColorIndex => 0;

            public int Offset { get => 0; set => throw new NotImplementedException(); }
            public int Length { get => int.MaxValue; set => throw new NotImplementedException(); }
        }

        protected class DocSlot :
			IPgBaseSite
		{
			protected readonly EditWindow2 _oHost;

			public DocSlot( EditWindow2 oHost ) {
				_oHost = oHost ?? throw new ArgumentNullException();
			}

			public IPgParent Host => _oHost;

			public void LogError(string strMessage, string strDetails, bool fShow=true) {
				_oHost._oSiteView.LogError( strMessage, strDetails, fShow );
			}

			public void Notify( ShellNotify eEvent ) {
			}
		} // End class

        /// <summary>
        /// Basic setup for text viewer. You must call Load() or InitNew() to make this object usable.
        /// Make sure we don't do anything (like set event sinks) until AFTER we've
        /// called InitNew(). Else we'll blow up. Plus if the constructor fails other objects will point
		/// to this destroyed object!
        /// </summary>
		/// <exception cref="ArgumentNullException" />
		/// <exception cref="ArgumentException" />
        public EditWindow2( IPgViewSite oSiteView, BaseEditor p_oDocument, bool fReadOnly = false, bool fSingleLine = false ) {
			_oSiteView   = oSiteView ?? throw new ArgumentNullException( "Site must not be null!" );
			_oViewEvents = oSiteView.EventChain ?? throw new ArgumentException( "Site.EventChain must support IPgViewSiteEvents" );
 			_oStdUI      = oSiteView.Host.Services as IPgStandardUI2 ?? throw new ArgumentException( "Parent view must provide IPgStandardUI service" );
			_oDocument   = p_oDocument ?? throw new ArgumentNullException();

            _oScrollBarVirt = new ScrollBar2( new DocSlot( this ) );

			//Parent = oSiteView.Host as Control;

            // BUG: The GetLine can crash with some custom line objects. If the document is empty and an empty
            //      line is attempted to be created but fails due to implementation error.
            CaretPos       = new CaretPosition( p_oDocument.GetLine(0) );
            _oTextSelector = new TextSelector( this );
            _fReadOnly     = fReadOnly;
            _fSingleLine   = fSingleLine;
            Wrap           = true;

			// https://icons8.com/
			Icon = SKImageResourceHelper.GetImageResource( Assembly.GetExecutingAssembly(), @"Editor.Content.icon8-doc.png" );

            Array.Sort<Keys>( _rgHandledKeys );

            AllowDrop      = true;
            Cursor         = Cursors.IBeam;
            DoubleBuffered = true;

            // This changed from ContextMenu to ContextMenuStrip in .net 5
            if( this.ContextMenuStrip == null ) {
                ContextMenuStrip oMenu = new ContextMenuStrip();
                oMenu.Items.Add( new ToolStripMenuItem( "Cut",   null, this.OnCut,   Keys.Control | Keys.X ) );
                oMenu.Items.Add( new ToolStripMenuItem( "Copy",  null, this.OnCopy,  Keys.Control | Keys.C ) );
                oMenu.Items.Add( new ToolStripMenuItem( "Paste", null, this.OnPaste, Keys.Control | Keys.V ) );
                oMenu.Items.Add( new ToolStripMenuItem( "Jump",  null, this.OnJump,  Keys.Control | Keys.J ) );
                this.ContextMenuStrip = oMenu;
            }

            Controls.Add(_oScrollBarVirt);

			_rgTools.Add( "Edit" );   // Standard text editing.
			_rgTools.Add( "Browse" ); // More like a browser where you jump on hyperlinks.
            _rgTools.Add( "Choose" ); // Chooser for check box lists.
			_rgTools.Add( "Morse" );  // plays morse code.

            // The object we get from the interface has some standard screen dpi and size
            // values. We then attempt to override those values with our actual values.
            IPgMainWindow.PgDisplayInfo oInfo = new IPgMainWindow.PgDisplayInfo();
            if( _oSiteView.Host.TopWindow is IPgMainWindow oMainWin ) {
                oInfo = oMainWin.MainDisplayInfo;
            }
            DPI = new SKPoint( oInfo.pntDpi.X, oInfo.pntDpi.Y );

            uint uiStdText = _oStdUI.FontCache( _oStdUI.FaceCache( @"C:\windows\fonts\consola.ttf"  ), 12, DPI );
            uint uiStdUI   = _oStdUI.FontCache( _oStdUI.FaceCache( @"C:\windows\fonts\seguisym.ttf" ), 12, DPI );
          //uint uiEmojID  = _oStdUI.FontCache( _oStdUI.FaceCache( @"C:\Users\Frodo\AppData\Local\Microsoft\Windows\Fonts\NotoEmoji-Regular.ttf" ), 12, sResolution );

			_oCacheMan = CreateCacheManager( uiStdText );
        }

        protected override void Dispose( bool disposing ) {
            if( disposing ) {
                _oDocument.CaretRemove( CaretPos );
                _oScrollBarVirt.Scroll -= OnScrollBar; 
                HyperLinks.Clear();
            }

            base.Dispose(disposing);
        }

        /// <summary>
        /// I want to make it so we can add extra columns between the scroll bar and the text.
        /// Might want right hand columns in the future, but let's start with this.
        /// TODO: Add the scroll bar to the layout.
        /// </summary>
        protected virtual void InitColumns() {
            _rgLayout  .Add( _rctTextArea );   // Main text area.
            _rgCacheMap.Add( _rctTextArea );
        }

        /// <summary>
        /// Slight hackery, but this will allow us to remove the special column
        /// painting routine for check marks...
        /// </summary>
        /// <returns>Returns the currently checked line given by the document</returns>
        public Line CheckedLine() { return _oDocument.CheckedLine; }

        /// <remarks>
        /// This object illustrates some of the problems of overriding behavior for objects that send events.
		/// If I use InitNew within Load, Load might set our carat, but InitNew would have initialized already
		/// and we send two events.
        /// </remarks>
        protected virtual bool InitInternal() {
			DecorNavPropsInit();

            _oScrollBarVirt.Visible = ScrollVisible;
            _oScrollBarVirt.Scroll += OnScrollBar; 

            ScrollBarRefresh();
            CaretIconRefreshLocation();

            int iWidth        = (int)(DPI.X * _szScrollBars.Width);
            var oLayoutSBVirt = new LayoutControl( _oScrollBarVirt, LayoutRect.CSS.Pixels, (uint)iWidth);

            _rgLayout.Add( oLayoutSBVirt );   // Scrollbar

            InitColumns(); // Columns arent the same as the layout quite yet.

			_rgLayout.SetRect( 0, 0, Width, Height );
			_rgLayout.LayoutChildren();

			// Kind of evil since we 'might' get called back even before we exit this proc!
			_oDocument.ListenerAdd(this);  // TODO, consider making this a normal .net event.
			_oDocument.CaretAdd(CaretPos); // Document moves our caret and keeps it in sync.

			_oDocument.HilightEvent += OnHighLightChanged;
			_oDocument.BufferEvent  += OnBufferEvent;
            _oDocument.CheckedEvent += OnCheckedEvent;

            HyperLinks.Add( "url",       OnBrowserLink );
            HyperLinks.Add( "callsign",  OnCallSign );
            HyperLinks.Add( "localpath", OnLocalPath );

            Invalidate();

            return ( true );
        }

        public virtual SKBitmap Icon { get; }

        public string Banner {
            get { 
                try {
                    // Find first non blank character.
                    Line oLine  = CaretPos.Line;
                    int  iStart = 0;

                    while( iStart < oLine.ElementCount ) {
                        if( !char.IsWhiteSpace( oLine[iStart] ) )
                            break;
                        ++iStart;
                    }

                    StringBuilder sbBanner = new StringBuilder();

                    if( string.IsNullOrEmpty( _oDocument.FileBase ) ) {
                        sbBanner.Append( "<Unknown Location>" );
                    } else {
                        sbBanner.Append( _oDocument.FileBase );
                    }

                    string strCurrentLine = oLine.SubString( iStart, 25 );

                    sbBanner.Append( " @ " );

                    if( string.IsNullOrEmpty( strCurrentLine ) ) {
                        sbBanner.Append( "<Empty Line>" );
                    } else {
                        foreach( char oChar in strCurrentLine ) {
                            if( oChar == '\t' )
                                sbBanner.Append( " " );
                            else
                                sbBanner.Append( oChar );
                        }
                    }

                    return sbBanner.ToString() ;
                } catch( NullReferenceException ) {
                    return( string.Empty );
                }
            }
        }

        /// <summary>
        /// Not super relevent but see https://devblogs.microsoft.com/xamarin/styling-for-multiple-device-resolutions/ for 
        /// other ways to get device resolution.
        /// </summary>
		protected virtual CacheManager2 CreateCacheManager( uint uiStdText ) {
			return new CacheManager2( new CacheManSlot( this ), _oStdUI.FontRendererAt( uiStdText ), _rgCacheMap );
		}

        /// <summary>
        /// New dot net 5 way of spinning up a process. 
        /// </summary>
        /// <param name="strUrl"></param>
        private void BrowserLink( string strUrl ) {
            try {
                ProcessStartInfo psi = new ProcessStartInfo {
                    FileName        = strUrl,
                    UseShellExecute = true
                };
                Process.Start( psi );
            } catch( Exception oEx ) {
                Type[] rgErrors = { typeof( ObjectDisposedException ), 
                                    typeof( FileNotFoundException ),
                                    typeof( NullReferenceException ),
                                    typeof( System.ComponentModel.Win32Exception ) };
                if( rgErrors.IsUnhandled( oEx ) ) 
                    throw;
            }
        }

        protected void OnLocalPath( Line oLine, IPgWordRange oRange ) {
            string strFile = oLine.SubString( oRange.Offset, oRange.Length);

            if( Parentage.TopWindow is IPgMainWindow oMainWin ) {
                oMainWin.DocumentShow( strFile, Guid.Empty, fShow:true );
            }
        }
        
        /// <summary>
        /// Capture the URL hyperlink. differientiats between the http(s) & file protocol. Hacks
        /// the .pdf so that displays in the browser instead of our text editor trying to handle
        /// the file .pdf case.
        /// </summary>
        protected void OnBrowserLink( Line oLine, IPgWordRange oRange ) {
            try {
                if( oRange is MemoryState<char> oState ) {
                    if( oState.GetValue( "protocol" ) is MemoryElem<char> oProto ) {
                        string strProto = oLine.SubString( oProto.Offset, oProto.Length );
                        if( string.Compare( strProto, "file", ignoreCase:true ) == 0 ) {
                            if( oState.GetValue( "urlremaining" ) is MemoryElem<char> oPath ) {
                                string strPath = oLine.SubString( oPath.Offset, oPath.Length);
                                string strExtn = Path.GetExtension(strPath);

                                if( string.Compare( strExtn, ".pdf", ignoreCase:true ) != 0 ) {
                                    // Bug: if we can't handle type we shouldn't succeed. Opening, but
                                    //      we do and end up with a blank bookmark. Need to work on DocumentShow.
                                    if( Parentage.TopWindow is IPgMainWindow oMainWin ) {
                                        oMainWin.DocumentShow( strPath, Guid.Empty, fShow:true ); // Open in ourselves.
                                        return;
                                    }
                                }
                            }
                        }
                    }
                }

                BrowserLink( oLine.SubString( oRange.Offset, oRange.Length) );
            } catch( Exception oEx ) {
                Type[] rgErrors = { typeof( IndexOutOfRangeException ),
                                    typeof( NullReferenceException ),
                                    typeof( ArgumentNullException ),
                                    typeof( ArgumentException ) };
                if( rgErrors.IsUnhandled( oEx ) )
                    throw;
            }
        }

        private void OnCallSign( Line oLine, IPgWordRange oRange ) {
            BrowserLink( "http://www.qrz.com/db/" +  oLine.SubString( oRange.Offset, oRange.Length) );
        }

        public virtual WorkerStatus PlayStatus {
			get { return _oDocument.PlayStatus; }
		}

        /// <summary>
        /// Normally we'd set these values value the site. But the one case I really need this, for the
        /// find window search string, we set the site in this window, but tell the site it's guess
        /// is that of the parent of this window. Kind of messed up. So I'll stick this in here for
        /// awhile until I sort this out.
        /// </summary>
        public float ScrollBarWidths {
            set {
                _szScrollBars = new SizeF(value, value);
            }
        }

        public bool ReadOnly {
            get { return( _fReadOnly ); }
            set { _fReadOnly = value; }
        }

        // If a sub-site is generating an error and calling up we would potentially want to know 
        // which before forwarding on up. Especially if there are bunches of them. But since not, 
        // we'll just go with this.
        public void LogError( string strCatagory, string strDetails ) {
            _oSiteView.LogError( strCatagory, strDetails );
        }

		private void OnBufferEvent(BUFFEREVENTS eEvent) {
			switch( eEvent ) {
				case BUFFEREVENTS.LOADED:
                    // Carat might be a zombie. Let's update it...
                    CaretPos.Line = _oDocument.GetLine( CaretPos.Line.At );

					Raise_Navigated( NavigationSource.API, CaretPos );
					break;
			}
		}

        protected virtual void OnCheckedEvent( Line oLineChecked ) { 
            Invalidate();
        }

        public bool InitNew() {
			if( !InitInternal( ))
				return false;

			Raise_Navigated( NavigationSource.API, CaretPos );

			return true;
		}

		public bool Load( XmlElement xmlRoot ) {
            try {
				if( !InitInternal( ) )
					return( false );

				if (xmlRoot.SelectSingleNode("cursor") is XmlElement xmlCursor) {
					CaretPos.Offset = int.Parse(xmlCursor.GetAttribute("column"));
					CaretPos.Line   = _oDocument.GetLine(int.Parse(xmlCursor.GetAttribute("line")));

					// Note: If I have no width or height, cache loading will loose
					// it's place and we end up at the top. 
					this.ScrollToCaret();
				}
                if( xmlRoot.SelectSingleNode("keylock" ) is XmlElement xmlKeyLock ) {
                    _fReadOnly = bool.Parse( xmlKeyLock.GetAttribute("readonly" ));
                }
			} catch( Exception oEx ) {
				Type[] rgErrors = { typeof( XPathException ),
									typeof( NullReferenceException ),
									typeof( ArgumentNullException ),
									typeof( FormatException ) };
				if( rgErrors.IsUnhandled( oEx ) )
					throw;

				CaretPos.Offset = 0;
				CaretPos.Line   = _oDocument.GetLine( 0 );

				Raise_Navigated( NavigationSource.API, CaretPos );
			}
            return true;
        }

        public bool Save(XmlDocumentFragment xmlRoot )
        {
            XmlElement xmlCursor = xmlRoot.OwnerDocument.CreateElement( "cursor" );

            xmlCursor.SetAttribute( "line",   CaretPos.At    .ToString() );
            xmlCursor.SetAttribute( "column", CaretPos.Offset.ToString() );

            xmlRoot.AppendChild( xmlCursor );

            XmlElement xmlKeyboardLock = xmlRoot.OwnerDocument.CreateElement( "keylock" );

            xmlKeyboardLock.SetAttribute( "readonly", _fReadOnly.ToString() );

            xmlRoot.AppendChild(xmlKeyboardLock);

            return true;
        }

        /// <summary>
        /// So we used to try to get information for the cache manager here when we were uniscribe.
        /// However, it's dangerous since it's not created in a timely manner. This call might happen
        /// while we are in the middle of another call! Like InitNew(). I had this call happening when
        /// I called the code to get a Graphics to measure the DPI for the later _oCacheMan assignment!!
        /// So the ScrollBarRefresh() and CaretIconRefeshLocation() calls had to check if _oCacheMan was null.
        /// </summary>
        /// <remarks>I'm going to keep this so I can check in on this.</remarks>
        //protected override void OnHandleCreated(EventArgs e) {
        //    try {
        //        base.OnHandleCreated(e);
        //    } catch( InvalidOperationException ) {
        //        // Seeing weird failure now and again. Could be a delegate bailing.
        //        // Since I don't know of any delegates of my own. I'm going to punt.
        //    }
        //}

        /// <summary>
        /// Always return true. I don't keep track of view specific data.
        /// </summary>
        /// <returns>true</returns>
        public bool IsDirty => true;

		public bool Wrap {
            get;
            set;
        }

        public ICollection<ILineSelection> Selections {
            get { return( _rgSelectionTypes ); }
        }

        public bool SetFocus() {
            return( this.Focus() );
        }
        
        private void OnJump( object sender, EventArgs e ) {
            HyperLinkFind( CaretPos, fDoJump:true );
        }

        protected bool HyperLinkFind( ILineRange oPosition, bool fDoJump ) {
            IPgWordRange oRange = oPosition.Line.FindFormattingUnderRange( oPosition );
            if( oRange != null ) { 
                foreach( KeyValuePair<string, HyperLink> oPair in HyperLinks ) { 
                    if( oRange.StateName == oPair.Key ) {
                        if( fDoJump )
                            oPair.Value?.Invoke( CaretPos.Line, oRange );
                        return true;
                    }
                }
            }
            return false;
        }

        protected bool HyperLinkFind( SKPointI oLocation, bool fDoJump ) {
            if( ClientToWorld(oLocation, out WorldLocator pntWorldLoc ) ) {
                if( _oCacheMan.GlyphPointToRange( ref pntWorldLoc, _oLastCursor ) != null ) {
                    return HyperLinkFind( _oLastCursor, fDoJump );
                }
            }

            return false;
        }
        
        private void OnCut( object o, EventArgs e ) {
            ClipboardCutTo();
        }

        private void OnCopy( object o, EventArgs e ) {
            ClipboardCopyTo();
        }

        private void OnPaste( object o, EventArgs e ) {
            ClipboardPasteFrom( Clipboard.GetDataObject(), ClipboardOperations.Default );
        }

        public void ClipboardCutTo() {
            ClipboardCopyTo();
            SelectionDelete();
        }

        public virtual void ClipboardCopyTo() {
            string     strSelection = string.Empty;
            DataObject oDataObject  = new DataObject();

			// If I've got a selection use it. Else use current caret pos. Note,
			// right mouse button press, removes any selection, moves the caret 
			// AND brings up the context menu. In the future we'll want to give the
			// option to choose the desired portion of any complex object.
			try {
				if( SelectionCount > 0 ) {
					strSelection = this.SelectionCopy();
				} else {
					IMemoryRange oSelection = CaretPos.Line.FindFormattingUnderRange( CaretPos );
					if( oSelection != null ) {
						strSelection = CaretPos.Line.SubString( oSelection.Offset, oSelection.Length );
					}
 				}

				oDataObject.SetData( strSelection );
				Clipboard.SetDataObject( oDataObject );
			} catch( NullReferenceException ) {
			}
        }

        public void ClipboardPasteFrom( 
            object oDataSource, 
            Guid   sOperation ) 
        {
            if( _fReadOnly )
                return;

            if( oDataSource == null ) {
                _oSiteView.LogError( "Edit", "Data source argument null!" );
                return;
            }
            IDataObject oDataObject = oDataSource as IDataObject;
            if( oDataObject == null ) {
                _oSiteView.LogError( "Edit", "Caller must support IDataObject!" );
                return;
            }

            // TODO: This might be a dummy line. So we need dummies to be at 0.
            //       Still a work in progress. See oBulk.LineInsert() below...
            int iLine = CaretPos.At;

            if( sOperation == ClipboardOperations.Text ||
                sOperation == ClipboardOperations.Default 
              ) {
                if( oDataObject.GetDataPresent(typeof(System.String)) ) {
                    string strPaste = oDataObject.GetData(typeof(System.String)) as string;

                    using( Editor.Manipulator oBulk = new Editor.Manipulator( _oDocument ) ) {
                        SelectionDelete(); // TODO: Check effect of interleaved insert/delete.
                        if( CaretPos.Line != null ) {
                            using( TextReader oReader = new StringReader(strPaste)  ) {
                                oBulk.StreamInsert(CaretPos.At, CaretPos.Offset, oReader );
                            }
                        }
                    }
                }
                if( oDataObject.GetDataPresent(DataFormats.FileDrop) ) {
                    string[] rgFileDrop = oDataObject.GetData(DataFormats.FileDrop) as string[];

                    using( Editor.Manipulator oBulk = new Editor.Manipulator( _oDocument ) ) {
                        foreach( string strFile in rgFileDrop ) {
                            oBulk.LineInsert(iLine++, Path.GetFileName(strFile) );
                        }
                    }
                }                  
            }
            if( sOperation == ClipboardOperations.FileDrop ) {
                string[] rgFileDrop = oDataObject.GetData( DataFormats.FileDrop ) as string[];

                using( Editor.Manipulator oBulk = new Editor.Manipulator( _oDocument ) ) {
                    foreach( string strFile in rgFileDrop ) {
                        StringBuilder oNewLine = new StringBuilder();
                        oNewLine.Append( "<img src=\"" );
                        oNewLine.Append( Path.GetFileName( strFile ) );
                        oNewLine.Append( "\" " );
                        oNewLine.Append( "alt=\"hello\" " );
                        oNewLine.Append( "style=\"width:600;\" " );
                        oNewLine.Append( "/>" );

                        oBulk.LineInsert( iLine++, oNewLine.ToString() );
                    }
                }
            }
            if( sOperation == ClipboardOperations.Base64 ) {
                if( oDataObject.GetDataPresent(typeof(System.String)) ) {
                }
                if( oDataObject.GetDataPresent(DataFormats.FileDrop) ) {
                    PasteAsFileDropToBase64( oDataObject );
                }
            }
        }

        /// <summary>
        /// Convert a file drop into base64 URI! might be nice to do text too....
        /// </summary>
        protected void PasteAsFileDropToBase64( IDataObject oDataObject ) {
            if(  oDataObject == null ) 
                return;
            if( _fReadOnly )
                return;

            string[] rgFileDrop = oDataObject.GetData(DataFormats.FileDrop) as string[];
            using( Editor.Manipulator oBulk = new Editor.Manipulator( _oDocument ) ) {
                foreach( string strFile in rgFileDrop ) {
                    try {
                        string   strExtension = Path.GetExtension( strFile ).Replace( ".", string.Empty ).ToLower();
                        string[] rgExtensions = { "gif", "jpg", "jpeg", "png" };

                        if( !rgExtensions.Contains( strExtension ) )
                            break;

                        using( Stream oStream = new FileStream( strFile, FileMode.Open ) ) {
                            if( oStream.Length > 10000 ) {
                                _oSiteView.LogError( "Edit", "File is too big for inline base 64 insert: " + strFile );
                                break;
                            }
                            byte[] rgBS = new byte[oStream.Length];
                            oStream.Read( rgBS, 0, (int)oStream.Length );

                            // It's BS that I need to hand this the whole byte stream when I've got streams.
                            string strResult = Convert.ToBase64String( rgBS );

                            // BUG: Get the mime type from the file.
                            strResult = "data:image/" + 
                                        Path.GetExtension( strFile ).Replace( ".", string.Empty ) + 
                                        ";base64," + strResult;

                            using( TextReader oReader = new StringReader(strResult)  ) {
                                // BUG: Get the file extension and create scheme wrapper.
                                oBulk.StreamInsert(CaretPos.At, CaretPos.Offset, oReader );
                            }
                        }
                    } catch( Exception oEx ) {
                        Type[] rgErrors = { typeof( NotSupportedException ),      typeof( SecurityException ),
                                            typeof( FileNotFoundException ),      typeof( IOException ),
                                            typeof( DirectoryNotFoundException ), typeof( ArgumentOutOfRangeException ) };

                        _oSiteView.LogError( "Edit", "Can't complete request: " + oEx.GetType().ToString() + " for file: " + strFile );
                        if( rgErrors.IsUnhandled( oEx ) ) {
                            _oSiteView.LogError( "Edit", "Unexpected problem pasting file as base 64" );
                            throw new ApplicationException( "Can't handle file read request", oEx );
                        }
                    }
                }
            }                    
        }

        public virtual void Raise_SelectionChanged() {
            _oDocument.Raise_BufferEvent( BUFFEREVENTS.FORMATTED );  
            // ScrollToCaret(); race and syncronization issues.
        }

        /// <summary>
        /// Refresh the carat visual position. If it's not in the range of the cached screen than
        /// park the physical cursor just off screen.
        /// </summary>
        private void CaretIconRefreshLocation() {
            if( Focused != true )
                return;
                
            if( _oCacheMan.GlyphLineToPoint( _iCaretColumn, CaretPos, out Point pntCaretWorldLoc ) ) {
                SmartRect oTextColumn       = _rgCacheMap[_iCaretColumn];
                // this changes from world coordinates to client coordinates.
                SKPointI  pntWorldTopLeft   = _oCacheMan.TextRect.GetPoint(LOCUS.UPPERLEFT);
                SKPointI  pntCaretScreenLoc = new SKPointI( pntCaretWorldLoc.X - pntWorldTopLeft.X + oTextColumn.Left, 
                                                            pntCaretWorldLoc.Y - pntWorldTopLeft.Y + oTextColumn.Top  );

                User32.SetCaretPos(pntCaretScreenLoc.X, pntCaretScreenLoc.Y);
            } else {
                if( _oCacheMan.Count == 0 ) 
                    User32.SetCaretPos( _oScrollBarVirt.Width, 0 );
                else
                    User32.SetCaretPos( -10, -_oCacheMan.LineHeight ); // Park it off screen.
            }
        }

		protected virtual void OnHighLightChanged() {
			Invalidate();
		}

        /// <summary>
        /// Offset from our world coordinates to screen (output window) coord's
        /// </summary>
        /// <param name="oCache"></param>
        /// <returns></returns>
        protected virtual PointF RenderAt( CacheRow oCache, SmartRect rcColumn ) {
            return( _oCacheMan.RenderAt( oCache, new Point( rcColumn.Left, rcColumn.Top ) ) );
        }

        /// <summary>Paint just the background of just this cache row. And only
        /// if a special color. Else the normal bg color previously set ok. If there was no space
        /// between lines we could omit the previous all screen clear and just paint here!</summary>
        /// <remarks>If we wanted to paint transparent we could probably omit this and go with
        /// whatever is the existing background. Like a bitmap or something.</remarks>
        protected void PaintBackground( SKCanvas skCanvas, SKPaint skPaint, CacheRow oRow ) {
            StdUIColors eBg    = StdUIColors.Max;
            PointF      pntUL  = RenderAt( oRow, _rctTextArea );
            SKRect      skRect = new SKRect( _rctTextArea.Left, pntUL.Y, _rctTextArea.Right, pntUL.Y + oRow.Height );

            if( CaretPos.Line != null && oRow.At == CaretPos.At && !_fSingleLine)
                eBg = StdUIColors.BGWithCursor;
            if( _oDocument.HighLight != null && oRow.At == _oDocument.HighLight.At)
                eBg = _oDocument.PlayHighlightColor;
            if( _iSelectedTool == 2 ) {
                Point pntMouse    = this.PointToClient( MousePosition );
                if( ClientToWorld( new SKPointI( pntMouse.X, pntMouse.Y ), 
                                   out WorldLocator pntWorld ) ) {
                    if( oRow.Top    <= pntWorld.Y &&
                        oRow.Bottom >= pntWorld.Y ) 
                    {
                        eBg = StdUIColors.BGWithCursor;
                    }
                }
            }

            if( eBg != StdUIColors.Max ) {
                skPaint .BlendMode = SKBlendMode.Src;
                skPaint .Color     = _oStdUI.ColorsStandardAt( eBg );
                skCanvas.DrawRect( skRect, skPaint );
            }
        }

        /// <summary>
        /// This is a simple little glyph draw for the left hand column check mark.
        /// </summary>
        /// <remarks>
        /// Would be nice to remove this and use the
        /// </remarks>
        /// <seealso cref="FTCacheLine.DrawGlyph(SKCanvas, SKPaint, float, float, IPgGlyph)"/>
        public static void DrawGlyph( 
            SKCanvas      skCanvas, 
            SKPaint       skPaint,
            float         flX, 
            float         flY, 
            IPgGlyph      oGlyph
        ) {
            if( oGlyph.Image.Width  <= 0 ||
                oGlyph.Image.Height <= 0 )
                return;

            SKRect skRect = new SKRect( flX, flY, 
                                        flX + oGlyph.Image.Width, 
                                        flY + oGlyph.Image.Height );
            // So XOR only works with alpha, which explains why my
            // Alpha8 bitmap works with this.
            skPaint .BlendMode = SKBlendMode.Xor;
            skCanvas.DrawBitmap(oGlyph.Image, flX, flY, skPaint);

            // So the BG is already the color we wanted, it get's XOR'd and
            // has a transparency set, then we draw our text colored rect...
            skPaint .BlendMode = SKBlendMode.DstOver;
            skCanvas.DrawRect(skRect, skPaint);
        }

        /// <remarks>
        /// The CacheMap is list of rectangles that map 1->1 to the CacheList in the
        /// Row object of the cache manager. Unfortunately there might be layout objects
        /// that are NOT part of the cache map. Like the scroll bar.
        /// </remarks>
        protected override void OnPaintSurface( SKPaintSurfaceEventArgs e ) {
            base.OnPaintSurface(e);

            try {
                _oCacheMan.Refresh( RefreshType.SIMPLE, RefreshNeighborhood.CARET );

                SKSurface skSurface = e.Surface;
                SKCanvas  skCanvas  = skSurface.Canvas;
                using SKPaint skPaint2 = new SKPaint {
                    Color = SKColors.Blue
                };
                using SKPaint skPaint = new SKPaint() {
                    BlendMode = SKBlendMode.Src,
                    Color     = _oStdUI.ColorsStandardAt(_fReadOnly ? StdUIColors.BGReadOnly : StdUIColors.BG)
                };
                // Paint all window background. Note: We could get by without this if
                // there was no space between lines/columns.
                skCanvas.DrawRect( new SKRect( 0, 0, Width, Height ), skPaint);

                // Now paint the rows.
                foreach( CacheRow oRow in _oCacheMan ) {
                    PaintBackground(skCanvas, skPaint, oRow);

                    for( int iCache=0; iCache<oRow.CacheList.Count; ++iCache ) {
                        FTCacheLine oCache = oRow.CacheList[iCache];

                        oCache.Render(skCanvas, _oStdUI, RenderAt(oRow, _rgCacheMap[iCache] ), iCache == 0 ? this.Focused : false );
                    }
                }
            } catch( Exception oEx ) {
                Type[] rgErrors = { typeof( NullReferenceException ),
                                    typeof( ArgumentOutOfRangeException ),
                                    typeof( ArgumentNullException ),
                                    typeof( ArgumentException ),
                                    typeof( InvalidOperationException ) };
                if( rgErrors.IsUnhandled( oEx ) )
                    throw;
            }
        }

        /// <summary>
        /// We can get the focus even before we've painted for the first time.
        /// </summary>
        protected override void OnGotFocus(EventArgs e) {
            base.OnGotFocus( e );

            if( CaretPos.Line == null ) {
                _iAdvance = 0;
            }

            User32.CreateCaret( this.Handle, IntPtr.Zero, 2, _oCacheMan.LineHeight );
            CaretIconRefreshLocation(); 
            User32.ShowCaret  ( this.Handle );

            _oScrollBarVirt.Show( SHOWSTATE.Active );

            _oViewEvents.NotifyFocused( true );

            this.Invalidate();
        }

        protected override void OnLostFocus(EventArgs e) {
            base.OnLostFocus( e );

            User32.DestroyCaret();

            _oScrollBarVirt.Show( SHOWSTATE.Inactive );

            if( _oViewEvents != null )
                _oViewEvents.NotifyFocused( false );

            this.Invalidate();
        }

        /// <remarks>
        /// Since we track the cumulative count at the top of the cache, we never reach 100% as
        /// the percentage of the way thru the document via the top. The extent isn't precicely
        /// calculated either and so we're going to have small inaccuracies.
        /// </remarks>
        private void ScrollBarRefresh() {
            int iCharCount    = 0;
            int iStreamOffset = _oCacheMan.Count <= 0 ? 0 : int.MaxValue;
          //int iLocalWidth   = 0;
            int iLocalHeight  = 1;

            foreach( CacheRow oCache in _oCacheMan ) {
                // Count all the buffered characters.
                iCharCount   += oCache.Line.ElementCount;
                iLocalHeight += oCache.Height;

                // Find the lowest character stream offset.
                if( iStreamOffset > oCache.Line.CumulativeLength )
                    iStreamOffset = oCache.Line.CumulativeLength;

                // In unwrapped mode we want the widest line.
              //if( iLocalWidth < oCache.UnwrappedWidth )
              //    iLocalWidth = oCache.UnwrappedWidth;
            }

            // BUG: Hacky business for setting our scroll bar height. I can do better.
            float flMultiplyer  = (float)_oCacheMan.TextRect[SCALAR.HEIGHT] / iLocalHeight;
            if( _oCacheMan.Count > 0 )
                _oScrollBarVirt.Refresh( TextShowing( iCharCount ) * flMultiplyer, 
                                         TextShowing( iStreamOffset ) );
        }

        private float TextShowing( int iCount ) {
            return( _oDocument.Size > 0 ? (float)iCount / _oDocument.Size : (float)0 );
        }
        
        private void CacheRefresh( RefreshType eRefreshType, RefreshNeighborhood eNeighborhood ) {
            if( this.IsDisposed )
                return;

            _oCacheMan.Refresh( eRefreshType, eNeighborhood );
        }

		// <summary>
		/// Return the full height of the cached area. Remember height depends on width.
        /// So you must send dependable width value. When embedding a edit window in a
        /// horizontally running track, you need to give the remaining track possibly 
        /// available as the width to get good height value from this routine. 
		/// </summary>
		public override Size GetPreferredSize( Size sProposed ) {
			_rgLayout.SetRect( 0, 0, sProposed.Width, sProposed.Height );
			_rgLayout.LayoutChildren();

            _oCacheMan.OnChangeSize( sProposed );
			CacheRefresh( RefreshType.COMPLEX, RefreshNeighborhood.SCROLL );

            int iHeight = 0;

			foreach( CacheRow oCache in _oCacheMan ) {
				//if( sSize.Width < oCache.UnwrappedWidth )
				//	sSize.Width = (int)oCache.UnwrappedWidth;
				iHeight += (int)oCache.Height;
			}

            if( iHeight < _oCacheMan.LineHeight )
                iHeight = _oCacheMan.LineHeight;

            sProposed.Height = iHeight;

			return sProposed;
		}
			

        /// <remarks>
		/// When you look at it. Refreshing the Cache after the size doesn't allow us to measure
		/// the text in case we want to set the size of the window! In the normal case, this is
		/// ok, but in the case of table layout it is a problem.
        /// Remember we need to use the _rctTextArea which is setup by the layout engine,
        /// do not simply subtract off the scroll bar from the window width!!
		/// </remarks>
        protected override void OnSizeChanged( EventArgs e ) {
            base.OnSizeChanged(e);

            _rgLayout.SetRect( 0, 0, Width, Height );
			_rgLayout.LayoutChildren();

            // TODO: We can get rid of this reference by putting the scroll
            //       bar in the layout and painting accordingly.
            _oCacheMan.OnChangeSize( new Size( _rctTextArea.Width, _rctTextArea.Height ) );
            CacheRefresh( RefreshType.COMPLEX, RefreshNeighborhood.SCROLL );
        }

        /// <summary>
        /// Event handler for the vertical or horizontal scroll bar.
        /// </summary>
        void OnScrollBar( ScrollEvents e ) {
            switch( e ) {
                case ScrollEvents.ThumbPosition:
                    CacheRefresh( RefreshType.RESET, RefreshNeighborhood.SCROLL );
                    break;
                case ScrollEvents.ThumbTrack:
                    break;
                case ScrollEvents.First:
                case ScrollEvents.Last:
                    _oCacheMan.OnScrollBar_Vertical( e );
                    CacheRefresh( RefreshType.RESET, RefreshNeighborhood.SCROLL );
                    break;
                default:
                    _oCacheMan.OnScrollBar_Vertical( e );
                    CacheRefresh( RefreshType.COMPLEX, RefreshNeighborhood.SCROLL );
                    break;
            }
        }

        protected override void OnMouseWheel(MouseEventArgs e) {
            base.OnMouseWheel( e );
            
            _oCacheMan.OnMouseWheel( e.Delta );

            CacheRefresh( RefreshType.COMPLEX, RefreshNeighborhood.SCROLL );
        }

        /// <summary>
        /// Advance tells us how far along graphically, we are in the text stream
        /// from the left hand side, so if the cursor moves up or down we can try 
        /// to hit that same advance point. Updates the caret pos as a side effect.
        /// </summary>
        /// <param name="pntWorld">World Coordinates.</param>
        /// <remarks>Advance is modulo in the wrapped text case.</remarks>
        public void CaretAndAdvanceReset( ref WorldLocator sWorld ) {
            _oCacheMan.CaretAndAdvanceReset( ref sWorld, CaretPos, ref _iAdvance );
        }

        protected override void OnMouseDoubleClick( MouseEventArgs e ) {
            base.OnMouseDoubleClick(e);

            Select();

            if( !ClientToWorld( new SKPointI( e.Location.X, e.Location.Y ), 
                                out WorldLocator sWorldLoc ) )
                return;

            CaretAndAdvanceReset( ref sWorldLoc );

            _oTextSelector.Clear();

            if( sWorldLoc._iColumn != 0 )
                return;

            // Double click, so look at the formatting, to see how much to select.
            if( CaretPos.Line.FindFormattingUnderRange( CaretPos ) is IMemoryRange oRange ) {
                // TODO: This code might be better as a flyweight version of a TextSelect 
                // class. Then I could use each interchangibly depending on I want to auto 
                // select like here or manual select like in TextSelector.
                ILineSelection oSelStart = new LineRange( CaretPos.Line, 
                                                          oRange.Offset, 
                                                          oRange.Length, 
                                                          SelectionTypes.Start );
                _rgSelectionTypes.Add( oSelStart );

                // Grow the selection if range is affect by another window.
                _oDocument.CaretAdd( oSelStart ); // BUGBUG: Never get's removed. Use the TextSelector class!!!
                _oCacheMan.OnChangeSelection( _rgSelectionTypes );
            }

            Invalidate();
            Update();
        } // end method
        
        /// <summary>
        /// Optionally initiate a drag drop operation. We only begin the drag
        /// after the user has moved the mouse outside the rectangle.
        /// </summary>
        /// <param name="e"></param>
        protected override void OnMouseDown(MouseEventArgs e) {
            base.OnMouseDown( e );

            if( !ClientToWorld( new SKPointI( e.Location.X, e.Location.Y ), 
                                out WorldLocator sWorldLoc ) );
            this.Select();

            CaretAndAdvanceReset( ref sWorldLoc );

            if( sWorldLoc._iColumn != 0 )
                return;

            _rctDragBounds = null;
            
            if( ModifierKeys == Keys.Shift ) {
                // TODO: handle case when selection is empty but new
                //       cursor position is far enough to make a new selection.
                UpdateSelection( new SKPointI( e.Location.X, e.Location.Y ) );
            } else {
                if( _oTextSelector.IsHit( new Point( sWorldLoc.X, sWorldLoc.Y ) ) ) {
                    if( e.Button == MouseButtons.Left ) {
                    // If there is a selection then this gets us ready to begin dragging.
                    Size oDragSize = SystemInformation.DragSize;

                    _rctDragBounds = new SmartRect( LOCUS.CENTER, 
                                                    (int)e.Location.X, 
                                                    (int)e.Location.Y, 
                                                    oDragSize.Width,
                                                    oDragSize.Height );
                    }
                } else {
                    _oTextSelector.Reset( CaretPos );
                }
            }
            CaretIconRefreshLocation();

            //Raise_Navigated( _oCaretPos );

            this.Invalidate();
        }

        public struct WorldLocator {
            public readonly int      _iColumn;
            public readonly SKPointI _pntLocation;
            public Line Row { get; set; } = null;

            public WorldLocator( int iColumn, SKPointI pntLoc ) {
                _iColumn     = iColumn;
                _pntLocation = pntLoc;
            }

            public int X => _pntLocation.X;
            public int Y => _pntLocation.Y;
        }

        /// <summary>
        /// Assist converting the mouse position to a cache elem world position.
        /// </summary>
        /// <param name="pntMouse">Mouse pos in client coordinates./param>
        /// <param name="pntLocation">Mouse pos in world coordinates.</param>
        /// <returns>index to the cacheman element. 0 is always the document
        /// line by default.</returns>
        /// <remarks>This represents the sliding world window. We can't currently handle
            // scrolling left/right.</remarks>
        protected bool ClientToWorld( SKPointI pntMouse, out WorldLocator sWC ) {
            SKPointI pntWorldTL = _oCacheMan.TextRect.GetPoint(LOCUS.UPPERLEFT);

            //SKPointI pntLocation = new SKPointI( pntMouse.X - TopLeft.X + pntWorldTL.X,
            //                                     pntMouse.Y - TopLeft.Y + pntWorldTL.Y);

            //return pntLocation;
            for( int i=0; i<_rgCacheMap.Count; ++i ) {
                SmartRect oColumn = _rgCacheMap[i];
                if( oColumn.IsInside( pntMouse.X, pntMouse.Y ) ) {
                    SKPointI pntColumnTL = oColumn.GetPoint( LOCUS.UPPERLEFT );

                    sWC = new( i, 
                               new SKPointI( pntMouse.X - pntColumnTL.X + pntWorldTL.X,
                                             pntMouse.Y - pntColumnTL.Y + pntWorldTL.Y) );

                    return true;
                }
            }

            sWC = new( -1, new SKPointI( -10, -10 ) );

            return false;
        }

        protected bool ClientToWorld( out WorldLocator sWC ) {
            SKPointI pntWorldTL = _oCacheMan.TextRect.GetPoint(LOCUS.UPPERLEFT);

            if( _iCaretColumn < _rgCacheMap.Count &&
                _iCaretColumn > -1 ) 
            {
                SmartRect oColumn = _rgCacheMap[_iCaretColumn];

                SKPointI pntColumnTL = oColumn.GetPoint( LOCUS.UPPERLEFT );

                sWC = new( _iCaretColumn, 
                           new SKPointI( (int)_iAdvance - pntColumnTL.X + pntWorldTL.X,
                                         pntColumnTL.Y + pntWorldTL.Y) );

                return true;
            }

            sWC = new( -1, new SKPointI( -10, -10 ) );

            return false;
        }

        /// <summary>
        /// Helper function to handle the mouse move and mouse up calls. If there is 
        /// a selection we move the cursor to show where the drop will occur. 
        /// </summary>
        protected void UpdateSelection( SKPointI pntMouse ) {
            if( !ClientToWorld( new SKPointI( pntMouse.X, pntMouse.Y ), 
                                out WorldLocator sWorldLoc ) )
                return;

            CaretAndAdvanceReset( ref sWorldLoc );

            if( sWorldLoc._iColumn != 0 )
                return;

            _oTextSelector.NextLocation( CaretPos ); // Change what is selected.

            CaretIconRefreshLocation();
        }

        protected override void OnMouseLeave( EventArgs e ) {
            base.OnMouseLeave(e);
            if( _iSelectedTool == 2 ) {
                Invalidate();
            }
        }

        public static bool IsCtrlKey( Keys oKey ) => ( oKey & Keys.Control ) != 0;

        /// <summary>
        /// This is our basic cursor status updater. We need to check it in a number of situations,
        /// so it's all here, instead of scattered around.
        /// </summary>
        /// <seealso cref="OnMouseMove"/>
        /// <seealso cref="OnKeyUp"/>
        /// <seealso cref="OnKeyDown" />
        protected virtual void CursorUpdate( Point sLocation, MouseButtons eButton ) {
            bool fProcessed = false;

            switch( _iSelectedTool ) {
                case 2:
                    if( IsCtrlKey( ModifierKeys ) ) {
                        Cursor = Cursors.IBeam;
                    } else {
                        Cursor oNewCursor = Cursors.Arrow;
                        if( HyperLinkFind( new SKPointI( sLocation.X, sLocation.Y ), fDoJump:false ) )
                            oNewCursor = Cursors.Hand;

                        Cursor = oNewCursor;
                    }
                    //Invalidate();
                    fProcessed = true;
                    break;
                case 1: // Browse Mode. Left button defeats the hyperlink.
                    if( !IsCtrlKey( ModifierKeys ) && ( eButton != MouseButtons.Left ) ) {
                        Cursor oNewCursor = Cursors.IBeam;
                        if( HyperLinkFind( new SKPointI( sLocation.X, sLocation.Y ), fDoJump:false ) )
                            oNewCursor = Cursors.Hand;

                        Cursor = oNewCursor;
                        fProcessed = true;
                    }
                    break;
                case 0: // Text Edit Mode. Left button defeats the hyperlink.
                    if( IsCtrlKey( ModifierKeys ) && ( eButton != MouseButtons.Left ) ) {
                        Cursor oNewCursor = Cursors.IBeam;
                        if( HyperLinkFind( new SKPointI( sLocation.X, sLocation.Y ), fDoJump:false ) )
                            oNewCursor = Cursors.Hand;

                        Cursor = oNewCursor;
                        fProcessed = true;
                    }
                    break;
            }

            if( !fProcessed ) {
                if( _rctTextArea.IsInside( sLocation.X, sLocation.Y ) ) {
                    if( Cursor != Cursors.IBeam )
                        Cursor = Cursors.IBeam;
                }
                if( _rctCheques != null && _rctCheques.IsInside( sLocation.X, sLocation.Y ) ) {
                    if( Cursor != Cursors.Arrow )
                        Cursor = Cursors.Arrow;
                }
            }
        }

        /// <summary>
        /// Call this function when you just want the state of affairs outside of any mouse event.
        /// </summary>
        /// <remarks>IMPORTANT! In OnKeyUp or OnKeyDown, are obviously only applied to the FOCUSED window!
        /// So if the cursor is overing over a non selected window, the keystroke is sent to
        /// the selected window and we won't see any cursor updates in the hovered window!! That can be
        /// confusing, so don't be confused. Not sure how or if I want to fix... :-/
        /// ALSO: If I can figure out a way to get the mouse button state I'll do that so
        /// we have a true picture of the current state of affairs.</remarks>
        protected void CursorUpdate() {
            CursorUpdate( PointToClient( MousePosition ), MouseButtons.None );
        }

        /// <summary>
        /// Begin the drag drop if the cursor moves outside the _rctDragging rect
        /// while the left button stays down.
        /// </summary>
        /// <remarks>
        /// We don't put anything on the clipboard during a drag drop operation.
        /// Only cut and past adds to the clipboard. As a matter of fact, if a clipboard
        /// operations occurs before our drag drop. That item will still be there after
        /// the drag drop.
        /// </remarks>
        protected override void OnMouseMove(MouseEventArgs e) {
            base.OnMouseMove( e );

            CursorUpdate( e.Location, e.Button );

            if( ( e.Button & MouseButtons.Left ) == MouseButtons.Left &&
                e.Clicks == 0 ) 
            {
                if( _rctDragBounds != null ) {
                    // We have a selection and we pressed the left mouse key within it now we 
                    // are checking if mouse has moved far enough to warrent a drag drop operation.
                    if( !_rctDragBounds.IsInside( e.Location.X, e.Location.Y ) ) {
                        DataObject      oDataObject = new DataObject();
                      //DragDropEffects eEffect     = DragDropEffects.Move;

                        using( _oDocument.UndoMasterBegin() ) {
                            oDataObject.SetData( SelectionCopy() );

                            DragDropEffects eResult = DoDragDrop( oDataObject, DragDropEffects.Move );
                            if( ( eResult & DragDropEffects.Move ) != 0 ) {
                                SelectionDelete();
                            }
                        }

                        _oTextSelector.Clear();
                        Update();
                    } 
                } else {
                    // No matter what mode we are in. If we're showing a cursor, allow select.
                    if( Cursor == Cursors.IBeam ) {
                        // We had no selection or pressed left mouse key with the cursor outside of that 
                        // section and now we are creating a new selection.
                        UpdateSelection( new SKPointI( e.Location.X, e.Location.Y ) );
                        Update();
                    }
                }
            }
        }

        /// <summary>If we are over a hyperlink and there is no selection: jump if the
        /// left button pressed, jump if browse mode or control key pressed.</summary>
        /// <remarks>Whether we dropped or not we need to clear the drag bounds rect.
        /// So don't clear it in the OnDragDrop() event, we need to clear it
        /// here too in case we are not dragging.
        /// </remarks>
        protected override void OnMouseUp(MouseEventArgs e) {
            base.OnMouseUp( e );

            bool fFoundHyperLink = false;

            if( _iSelectedTool == 2 ) {
                if( e.Button == MouseButtons.Left &&
                    ((ModifierKeys & Keys.Control) == 0) 
                  ) {
                    fFoundHyperLink = HyperLinkFind(new SKPointI(e.Location.X, e.Location.Y), fDoJump:true);
                }
            } else {
                if( ( e.Button == MouseButtons.Left &&
                      ( _iSelectedTool == 1) || ((ModifierKeys & Keys.Control) != 0) ) &&
                    !TextSelector.IsSelected(Selections) // This saves us from jumping if we had pressed the left mouse button.
                ) {
                    fFoundHyperLink = HyperLinkFind( new SKPointI( e.Location.X, e.Location.Y ), fDoJump:true );
                }
            }

            _rctDragBounds = null;

            CaretIconRefreshLocation();

            // This system allows only one checked line. 
            if( _rctCheques != null && _rctCheques.IsInside( e.Location.X, e.Location.Y ) ) {
                _oDocument.CheckedLine = CaretPos.Line;
                Raise_SelectionChanged();
                Invalidate();
            }
            if( _iSelectedTool == 2 &&
                fFoundHyperLink == false &&
                ((ModifierKeys & Keys.Control) == 0) &&
                _rctTextArea.IsInside( e.Location.X, e.Location.Y ) ) 
            {
                TextAreaChecked( CaretPos.Line );
            }

            // We ONLY navigate on the MouseUp portion of the mouse messages.
            Raise_Navigated( NavigationSource.UI, CaretPos );
        }

        /// <summary>
        /// When a possible check is comming to the text area of a line, 
        /// this method provides an opportunity to override default behavior.
        /// </summary>
        protected virtual void TextAreaChecked( Line oLine ) {
            _oDocument.CheckedLine = oLine;
            Raise_SelectionChanged();
            Invalidate();
        }

        static int LMOUSEBTN = 1;
      //static int RMOUSEBTN = 2;
        static int SHIFT     = 4;
        static int CTRL      = 8;
      //static int MMOUSEBTN = 16;
      //static int ALT       = 32;

        struct DropEffect {
            int             _iKeyState;
            DragDropEffects _eEffect;

            public DropEffect( int iKeyState, DragDropEffects eEffect ) {
                _iKeyState = iKeyState;
                _eEffect   = eEffect;
            }

            public bool IsHit( DragEventArgs oArg ) 
            {
                return( (( oArg.KeyState      & _iKeyState ) != 0 ) &&
                        (( oArg.AllowedEffect & _eEffect   ) != 0 ) );
            }

            public DragDropEffects Effect {
                get { return( _eEffect ); }
            }
        }

        DropEffect[] rgDropEffects = { //new DropEffect( CTRL + ALT, DragDropEffects.Link ), 
                                       //new DropEffect( ALT, DragDropEffects.Link ),
                                       new DropEffect( LMOUSEBTN | SHIFT, DragDropEffects.Move ),
                                       new DropEffect( LMOUSEBTN | CTRL,  DragDropEffects.Copy ),
                                       new DropEffect( LMOUSEBTN,         DragDropEffects.Move ) };

        protected override void OnDragOver( DragEventArgs oArg ) {
            base.OnDragOver(oArg);

            oArg.Effect = DragDropEffects.None;

            Point pntLocation  = PointToClient( new Point(oArg.X, oArg.Y));

            if( !ClientToWorld( new SKPointI( pntLocation.X, pntLocation.Y ), 
                                              out WorldLocator sWorldLoc ) );
            CaretAndAdvanceReset( ref sWorldLoc );
            CaretIconRefreshLocation(); // BUG: We need to move the cursor when draging on ourself.
            
            if( sWorldLoc._iColumn != 0 )
                return;

            // If we are both the source and target don't allow the drop if
            // we are dropping within our current selection.
            if( _oTextSelector.IsHit( new PointF( sWorldLoc.X, sWorldLoc.Y ) ) ) {
                return;
            }

            // Can we handle the drop data type?
            if( !oArg.Data.GetDataPresent( "System.String", true) &&
                !oArg.Data.GetDataPresent( DataFormats.FileDrop, true ) ) {
                return;
            }

            // Can we handle the drop effect?
            foreach( DropEffect sDropEffect in rgDropEffects ) {
                if( sDropEffect.IsHit( oArg ) )
                {
                    oArg.Effect = sDropEffect.Effect;
                }
            }
            // Did we get anything? Narf!
        }

        /// <summary>
        /// The gods of DnD have ordained to drop onto us!
        /// </summary>
        /// <param name="oArg"></param>
        protected override void OnDragDrop( DragEventArgs oArg ) {
            base.OnDragDrop(oArg);

            if( oArg.Data.GetDataPresent(typeof(System.String)) ) {
                string item = oArg.Data.GetData(typeof(System.String)) as string;

                // Ok, I'm getting data, and I'm supposed to add it BEFORE
                // I've removed it from the source. Only a big deal if source and
                // target are the same document eh?
                if( ( oArg.Effect & DragDropEffects.Copy ) != 0 ||
                    ( oArg.Effect & DragDropEffects.Move ) != 0    )
                {
                    Point pntTemp = this.PointToClient( new Point(oArg.X, oArg.Y) );

                    if( !ClientToWorld( new SKPointI( pntTemp.X, pntTemp.Y ), out WorldLocator sWorldLoc ) )
                        return;

                    FTCacheLine oCacheHit = null;
                    int         iRowTop   = 0;

                    // Target's always going to be in the cache since it's the only place we can drop!
                    // BUG: Need to check if in the TextArea...
                    foreach( CacheRow oRow in _oCacheMan ) {
                        if( oRow.IsHit( new Point( sWorldLoc.X, sWorldLoc.Y ) ) ) {
                            FTCacheLine oCache = oRow.CacheList[0];
                            oCacheHit = oCache;
                            iRowTop   = oRow.Top;
                            break;
                        }
                    }
                    // Got to do the insert AFTER walking the _oCacheMan b/c stream insert adds 
                    // lines to cache. See OnLineNew() callback! 
                    if( oCacheHit != null ) {
                        int  iEdge = oCacheHit.GlyphPointToOffset( iRowTop, sWorldLoc._pntLocation );

                        // We can add in front of the EOL character.
                        //Debug.Assert( iEdge <= oLine.ElementCount );

                        using( Editor.Manipulator oBulk = new Editor.Manipulator( _oDocument ) ) {
                            oBulk.StreamInsert( oCacheHit.At, iEdge, new StringReader( item ) ); // TODO: Check line count good.
                        }
                    }
                }
            //} else if( oArg.Data.GetDataPresent(DataFormats.FileDrop) ) {
            //    string[] rgItems = oArg.Data.GetData(typeof(System.String)) as string[];
            //    MainWin  oHost   = (MainWin)_oSite.Host;

            //    if( ( oArg.Effect & DragDropEffects.Copy ) != 0 ) {
            //        foreach( string strItem in rgItems ) {
            //            oHost.CreateTextEditor( strItem );
            //        }
            //    }
            }

            this.Invalidate();
        }

        /// <summary>
        /// Select all. Doesn't invalidate window or refresh the cash, tho it does
        /// send a OnChangeFormatting() message.
        /// </summary>
        /// <remarks>If the user adds lines in another window, we should track them
        /// nicely with the new architecture. Need to give it the dual view test.</remarks>
        public void SelectionSetAll() {
            LineRange oSelectMiddle = new SelectAll();

            Selections.Clear();
            Selections.Add( oSelectMiddle );

            _oCacheMan.OnChangeSelection( _rgSelectionTypes );

            Raise_Navigated( NavigationSource.UI, CaretPos );
        }

        /// <summary>
        /// This doesn't mess with any TextSelector objects but it does clear the
        /// selections that they set on the edits. Removes the selection from
        /// the caret's collection too. 
        /// </summary>
        /// <remarks>TODO: I'm not reliably removing these carets when they are no
        /// longer in use. I need a better way of clearing them. I think passing
        /// the view as an object identifier might be a better way to mark these.
        /// Then I can remove them all in one blow.</remarks>
        public void SelectionClear() {
            foreach( ILineSelection oSelect in _rgSelectionTypes ) {
                _oDocument.CaretRemove(oSelect);
            }

            _rgSelectionTypes.Clear();
            //_oTextSelector.Clear(); TODO: Text selector calls SelectionClear() chicken/egg problem.

            _oCacheMan.OnChangeSelection( _rgSelectionTypes );

            //Raise_Navigated( _oCaretPos );
            Invalidate();
        }

        /// <summary>
        /// I HATE to say it, but this way of defining enumerators, kicks ass over trying to create
        /// some sort of FSA. Might want to move this method to the text selector class. 
        /// </summary>
        IEnumerable<ILineSelection> GetSelections() {
            Dictionary<SelectionTypes, ILineSelection> rgRanges = new Dictionary<SelectionTypes, ILineSelection>();

            foreach( ILineSelection oRange in _rgSelectionTypes ) {
                rgRanges.Add( oRange.SelectionType, oRange );
            }

            if( rgRanges.ContainsKey(SelectionTypes.Start)) 
                yield return( rgRanges[SelectionTypes.Start] );

            if( rgRanges.ContainsKey(SelectionTypes.Middle ) ) {
                SelectMiddle oMiddle = rgRanges[SelectionTypes.Middle] as SelectMiddle;

                if( oMiddle != null ) {
                    for( int iLine=oMiddle.StartLine.At + 1; iLine < oMiddle.EndLine.At; ++iLine ) {
                        oMiddle.Line = _oDocument.GetLine(iLine);
                        yield return( oMiddle );
                    }
                } else {
                    if( rgRanges[SelectionTypes.Middle] is SelectAll oAll ) {
                        for( int i=0; i<_oDocument.ElementCount; ++i ) {
                            oAll.Line = _oDocument[i];
                            yield return( oAll );
                        }
                    }
                }
            }
            if( rgRanges.ContainsKey(SelectionTypes.End)) 
                yield return( rgRanges[SelectionTypes.End] );
        }

        /// <summary>
        /// This is a bit more efficient than calling GetSelections() since we don't actually
        /// need to visit all the nodes in the "selectall" case.
        /// </summary>
        /// <returns>Count of charactes in selection including cr/lf.</returns>
        protected long SelectionCount 
        {
            get {
                long iCharCount = 0;
                int  iEOLLen    = _oDocument.EOL.Length;

                foreach( ILineSelection oRange in _rgSelectionTypes ) {
                    if( oRange.SelectionType == SelectionTypes.Start ||
                        oRange.SelectionType == SelectionTypes.End ) {
                        iCharCount += oRange.Length;

                        if( oRange.IsEOLSelected )
                            iCharCount += iEOLLen;
                    }
                    if( oRange.SelectionType == SelectionTypes.Middle ) {
                        SelectMiddle oRangeSel = oRange as SelectMiddle;
                        SelectAll    oRangeAll = oRange as SelectAll;
                        if( oRangeSel != null ) {
                            // If we've got a range but it's not a SelectMiddle, that's a problem.
                            for( int i = oRangeSel.EndLine.At - 1; i>oRangeSel.StartLine.At; --i ) {
                                iCharCount += _oDocument[i].ElementCount;
                            }
                        } else if( oRangeAll != null ) {
                             iCharCount += _oDocument.Size;
                        }
                    }
                }

                return( iCharCount );
            }
        }

        /// <summary>
        /// Build up a string based on the line selection. Multi-select not
        /// currently handled. Note: we don't care if it's a stream or column
        /// select! Both should work fine.
        /// </summary>
        /// <remarks>If any line length approaches Int32.MaxInt we might 
        /// blow up! Offset + Length needs to be less than Int32.MaxInt.
        /// Selection MIGHT include the mythical EOL character plus 1,
        /// meaning ONLY the EOL is desired. Perhaps instead I should
        /// put an property on the selection indicating EOL included instead.
        /// </remarks>
        public string SelectionCopy() {
            StringBuilder oBuilder = new StringBuilder();

            using( StringWriter oStream = new StringWriter( oBuilder ) ) {
                foreach( ILineSelection oSel in GetSelections() ) {
                    ILineSelection oRange = oSel;
                    Line           oLine  = oSel.Line; 

                    if( oRange.Offset <= oLine.ElementCount ) {
                        int  iRangeLen = oRange.Length;
                        int  iRangeOff = oRange.Offset;

                        // For the 'any' line length ranges, check before any
                        // arithmetic on the variable, else we might overflow!
                        if( iRangeLen > oLine.ElementCount )
                            iRangeLen = oLine.ElementCount;

                        if( iRangeOff < 0 )
                            iRangeOff = 0;

                        if( iRangeOff + iRangeLen > oLine.ElementCount ) {
                            iRangeLen = oLine.ElementCount - iRangeOff;
                        }

                        if( iRangeLen > 0 ) 
                            oLine.Save( oStream, iRangeOff, iRangeLen );

                        if( oRange.IsEOLSelected )
                            oStream.WriteLine();
                    }
                }

                oStream.Flush();

                if( oBuilder.Length == 0 ) {
                    if( CaretPos.Line != null ) {
                        CaretPos.Line.Save( oStream );
                    }
                }

                oStream.Flush();
            }

            return( oBuilder.ToString() );
        }

        /// <summary>
        /// Trim the end line but don't delete it even, if the whole line is selected. EOL can't be selected
        /// else it's a middle selection. Delete the middle lines first. Lastly the trim the start 
        /// which may merge with the remainder of the end which will now be trailing the start, but it's
        /// Line.At values will be wrong and must be re-computed, this usually done by the manipulator.
        /// It might be usefull to define a reverse enumerable method somewhat like GetSelection(), but
        /// of course since we are deleting lines, that makes such an enumerator trickier.
        /// </summary>
        public void SelectionDelete() {
            if( _fReadOnly ) {
                SelectionClear();
                return;
            }

            Dictionary<SelectionTypes, ILineSelection> _rgRanges = new Dictionary<SelectionTypes, ILineSelection>();
            foreach( ILineSelection oRange in _rgSelectionTypes ) {
                _rgRanges.Add( oRange.SelectionType, oRange );
            }
            using( Editor.Manipulator oBulk = _oDocument.CreateManipulator() ) {
                if( _rgRanges.ContainsKey( SelectionTypes.End ) ) {
                    ILineSelection oRange = _rgRanges[SelectionTypes.End];

                    oBulk.LineTextDelete( oRange.At, oRange );
                }
                if( _rgRanges.ContainsKey(SelectionTypes.Middle ) ) {
                    SelectMiddle oRangeSel = _rgRanges[SelectionTypes.Middle] as SelectMiddle;
                    SelectAll    oRangeAll = _rgRanges[SelectionTypes.Middle] as SelectAll;
                    // BUG/TODO: If all middle selection then start line will be a middle? I can
                    //           fix this by redefining the SelectMiddle extents to be middle lines only.
                    if( oRangeSel != null ) {
                        // If we've got a range but it's not a SelectMiddle, that's a problem.
                        for( int i = oRangeSel.EndLine.At - 1; i>oRangeSel.StartLine.At; --i ) {
                            oBulk.LineDelete( i ); 
                        }
                    } else if( oRangeAll != null ) {
                         oBulk.DeleteAll();
                    }

                }
                if( _rgRanges.ContainsKey(SelectionTypes.Start ) ) {
                    ILineSelection oRange = _rgRanges[SelectionTypes.Start];

                    if( oRange.Offset < oRange.Line.ElementCount )
                        oBulk.LineTextDelete( oRange.At, oRange );
                    if( oRange.IsEOLSelected )
                        oBulk.LineMergeWithNext( oRange.At );
                }

                SelectionClear();
            } // end Bulk.

            Invalidate();
        }

        // Let Forms know what keys we want sent our way.
        protected override bool IsInputKey(Keys keyData) {
            int iIndex = Array.BinarySearch<Keys>(_rgHandledKeys, keyData);

            if (iIndex >= 0)
                return (true);

            return base.IsInputKey( keyData );
        }

        /// <summary>
        /// Send an event when the line changed.
        /// </summary>
        protected virtual void Raise_Navigated( NavigationSource eSource, ILineRange oCarat ) {
            // TODO: only want to signal next two when line is actually changed.
            // Note: Currently used by FindWindow and ViewSiteLine objects.
            LineChanged?.Invoke( oCarat.At );

            _oSiteView.Notify( ShellNotify.BannerChanged );

            DecorNavigatorUpdate( eSource, oCarat );
        }

        public enum EditNavigation : int {
            Line = 0,
            Column,
            Character,
            Selection,
            Character_Count,
            File_Encoding
        }

        /// <seealso cref="DecorNavigatorUpdate" />
		protected virtual void DecorNavPropsInit() {
            // don't do anything.
		}

        protected virtual void DecorNavigatorUpdate( NavigationSource eSource, ILineRange oCaret ) {
            // don't do anything in the default case. Only the shell edit window needs this.
        }

        /// <remarks>It's a bummer that I have to call this method whenever the key's are pressed
        /// in order to have the scroll position track correctly to keep the cursor on screen.
        /// It would be nicer if it was part of the callback for the buffer change. Yet we would
        /// only update our scroll position if our view originated the keystroke. This is also
        /// a consideration if we have macro's that are view based versus document based.</remarks>
        /// <param name="iDir">-1, 1, and 0</param>
        protected void OnKeyDown_Arrows( Axis eAxis, int iDir ) {
            if( this.IsDisposed )
                return;

            _oTextSelector.Clear();

            switch( _oCacheMan.MoveCaret( eAxis, iDir, ref _iAdvance, CaretPos ) ) {
                case CaretMove.MISS:   // Complete miss.
                    CacheRefresh( RefreshType.RESET, RefreshNeighborhood.CARET );
                    break;
                case CaretMove.NEARBY: // Nearby but off screen.
                    CacheRefresh( RefreshType.COMPLEX, RefreshNeighborhood.CARET );
                    break;
                case CaretMove.LOCAL:  // Still on screen.
                    CaretIconRefreshLocation();
                    ScrollBarRefresh();
                    Invalidate(); // Repaints the background behind the new cursor position.
                    break;
            }

            Raise_Navigated( NavigationSource.UI, CaretPos );
        } // end method

        /// <summary>
        /// Paging is a vertical aspect thing. So don't need this same code for horizontal.
        /// </summary>
        protected void OnKeyDown_Page( ScrollEvents e ) {
            if( this.IsDisposed )
                return;

            _oCacheMan.OnScrollBar_Vertical( e );
            CacheRefresh( RefreshType.COMPLEX, RefreshNeighborhood.SCROLL );

            // Only care about the Y coordinate. X we're going to override to the advance value.
            if( !ClientToWorld( out WorldLocator sWC ) )
                return;

            if( _oCacheMan.GlyphPointToRange( ref sWC, CaretPos ) != null ) {
                CaretIconRefreshLocation();

                // While I don't REALLY know if the caret moved. It is as good as moved.
                Raise_Navigated( NavigationSource.UI, CaretPos );
            }
        }

        protected void OnKeyDelete( bool fBackSpace ) {
            if( _fReadOnly )
                return;

            if( TextSelector.IsSelected( _rgSelectionTypes ) ) {
                using( _oDocument.UndoMasterBegin() ) {
                    SelectionDelete();
                    //SelectionClear();
                }
            } else {
                if( CaretPos.TrimOffset() ) {
                    using( Editor.Manipulator oBulk = _oDocument.CreateManipulator() ) {
                        if( fBackSpace ) {
                            if( CaretPos.Offset == 0 ) {
                                if( oBulk.IsHit( CaretPos.At - 1 ) ) {
                                    oBulk.LineMergeWithNext( CaretPos.At - 1 );
                                }
                            } else {
                                oBulk.LineTextDelete( CaretPos.At, new ColorRange( CaretPos.Offset - 1, 1 ) );
                            }
                        } else {
                            if(  CaretPos.Offset ==  CaretPos.Line.ElementCount )
                                oBulk.LineMergeWithNext( CaretPos.At );
                            else
                                oBulk.LineTextDelete( CaretPos.At, new ColorRange( CaretPos.Offset, 1 ) );
                            }
                        }
                    }
                }
            OnKeyDown_Arrows( Axis.Horizontal, 0 );
        }

        protected void OnKeyEnter(KeyEventArgs e) {
            if( _oViewEvents.IsCommandKey( CommandKey.Enter, (KeyBoardEnum)e.Modifiers ) )
                return;
            if( _fReadOnly || _fSingleLine )
                return;

            using( Editor.Manipulator oBulk = _oDocument.CreateManipulator() ) {
                if( TextSelector.IsSelected( _rgSelectionTypes ) ) {
                    using( _oDocument.UndoMasterBegin() ) {
                        SelectionDelete();
                        //SelectionClear();
                        CaretPos.Split( oBulk );
                    }
                } else {
                    CaretPos.Split( oBulk );
                }
            }
            OnKeyDown_Arrows( Axis.Horizontal, 0 );
        }
        
        /// <summary>
        /// Note: The difference between scroll bar scrolling and Page-Up/Down scrolling is a
        /// usability issue. We keep the caret on screen when we use the keyboard.
        /// </summary>
        protected override void OnKeyDown(KeyEventArgs e) {
            if( IsDisposed )
                return;

            //base.OnKeyDown( e ); // Not sure this is really needed for the control beneath. Probably bad actually.
            
            e.Handled = true;

            switch( e.KeyCode ) {
                case Keys.PageDown:
                    OnKeyDown_Page( ScrollEvents.LargeIncrement );
                    break;
                case Keys.PageUp:
                    OnKeyDown_Page( ScrollEvents.LargeDecrement );
                    break;
                case Keys.Down:
                    OnKeyDown_Arrows( Axis.Vertical, 1);
                    break;
                case Keys.Up:
                    OnKeyDown_Arrows( Axis.Vertical, -1);
                    break;
                case Keys.Right:
                    OnKeyDown_Arrows( Axis.Horizontal, 1);
                    break;
                case Keys.Left:
                    OnKeyDown_Arrows( Axis.Horizontal, -1);
                    break;
                case Keys.Home:
                    ScrollTo( SCROLLPOS.TOP );
                    break;
                case Keys.End:
                    ScrollTo( SCROLLPOS.BOTTOM);
                    break;
                    
                case Keys.Back:
                case Keys.Delete:
                    OnKeyDelete( e.KeyCode == Keys.Back );
                    break;

                case Keys.Enter:
                    OnKeyEnter( e );
                    break;
                case Keys.Tab:
                    _oViewEvents.IsCommandKey( CommandKey.Tab, (KeyBoardEnum)e.Modifiers );
                    break;
                case Keys.ControlKey:
                    // Note: This comes in occasionally even if keep pressing ctrl.
                    CursorUpdate();
                    break;
            }
        }

        protected override void OnKeyUp(KeyEventArgs e)
        {
            if (IsDisposed)
                return;

            e.Handled = true;

            switch( e.KeyCode ) {
                case Keys.ControlKey:
                    CursorUpdate();
                    break;
            }
        }

        /// <summary>
        /// Handle basic typography. Basic text going in, complete with repeating keys and etc. Special
        /// cases are handled by OnKeyDown()
        /// </summary>
        /// <remarks>We're getting this call even tho we sent handled on some of the keydown events.</remarks>
        /// <param name="e"></param>
        protected override void OnKeyPress(KeyPressEventArgs e)
        {
            if( IsDisposed )
               return;

 	        //base.OnKeyPress(e);

            // Ask the shell if this character is a command of some sort. Like when we're used in a dialog.
            // Tab is a shitter, for example g and G are two different characters. We don't need to know
            // the state of the shift key! But tab doesn't have that luxury. Sometimes computers suck!
            if( _oViewEvents.IsCommandPress( e.KeyChar ) )
                return;
            if( _fReadOnly )
                return;
 	         
            // Filter what we can handle... Is it a normal keystroke, we can handle TAB too.
 	        if( !char.IsControl( e.KeyChar ) || e.KeyChar == 0x0009 ) { 
                if( CaretPos.TrimOffset()  ){
                    if( TextSelector.IsSelected( _rgSelectionTypes ) ) {
                       using( _oDocument.UndoMasterBegin() ) {
                           SelectionDelete(); // Blast whatever was selected and replace with this char.
                           _oDocument.LineCharInsert( CaretPos.At, 
                                                      CaretPos.Offset, 
                                                      e.KeyChar);
                       }
                    } else {
                       _oDocument.LineCharInsert( CaretPos.At, 
                                                  CaretPos.Offset, 
                                                  e.KeyChar);
                    }
                }
                OnKeyDown_Arrows( Axis.Horizontal, 0 );
            }
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)	
        {
            if( this.IsDisposed )
                return( false );

            const int WM_KEYDOWN    = 0x100;
            const int WM_SYSKEYDOWN = 0x104;
               
            if ((msg.Msg == WM_KEYDOWN) || (msg.Msg == WM_SYSKEYDOWN))
            {
                switch(keyData) {
                    case Keys.Control | Keys.F:
                        _oViewEvents.IsCommandKey( CommandKey.Find, KeyBoardEnum.Control );
                        return( true );
                    case Keys.Control | Keys.A:
                        SelectionSetAll();
                        Invalidate();
                        return( true );

                    case Keys.Control | Keys.Z:
                        if( !_fReadOnly ) {
                            _oDocument.Undo();
                        }
                        return( true );

                    case Keys.Control | Keys.E: {
                        //using( Graphics oGraph = this.CreateGraphics() ) {
                        //    float flDipX = oGraph.DpiX; 
                        //    using( GraphicsContext oDC = new GraphicsContext( oGraph ) ) {
                        //        EnumFontsHelper.Test2( oDC.Handle, flDipX );
                        //    }
                        //}
                        return( true );
                    }
                    case Keys.Control | Keys.R: {
                        foreach ( FontFamily oneFontFamily in FontFamily.Families ) {
                            LogError( "Font", oneFontFamily.Name );
                        }
                        return( true );
                    }
                    case Keys.Delete: {
                        OnKeyDelete( false );
                        return( true );
                    }
                }
            } 

            return base.ProcessCmdKey( ref msg, keyData );
        } // end method

        public void OnLineNew( Line oLine )
        {
            if( this.IsDisposed )
                return;

            _oCacheMan.OnLineAdded( oLine );
        }

        public void OnLineDelete( Line oLine )
        {
            if( this.IsDisposed )
                return;

            _oCacheMan.OnLineDeleted( oLine );
        }
        
        public void OnLineUpdated( Line oLine, int iOffset, int iOldLen, int iNewLen ) {
            if( this.IsDisposed )
                return;

            _oCacheMan.OnLineUpdated( oLine );
        }

        /// <remarks>
        /// Note: Don't Raise caret movement events here. We don't know if the caret really moved
        ///       and that can cause problems with focus management in the EditorViewSites implementation.
        /// When line updated events happen, the system updates the line regardless of IsValid state.
        /// But MultiFinished events are broad and might not have accompanying line events. So I
        /// Invalidate all before calling CacheRefresh(). This will now slow us down a bit.
        /// Which is a drag since only one operation really needs this and that is BBC Basic Line renumbering.
        /// </remarks>
        protected void OnMultiFinished() {
            // BUG: If our document get's closed but somebody still points to it, it can
            // do an update on us. This window will be disposed and unusable. Need to
            // unlink the document events when this view get closed.
            if( IsDisposed )
                return;

            _oCacheMan.Invalidate();

            CacheRefresh( RefreshType.COMPLEX, RefreshNeighborhood.SCROLL );

            Invalidate();
        }

        protected void OnSingleFinished() {
            if( IsDisposed )
                return;

            CacheRefresh( RefreshType.SIMPLE, RefreshNeighborhood.SCROLL );

            Invalidate();
        }

        /// <summary>
        /// Parses roll in after the user changes the text.
        /// </summary>
        protected void OnFormatChanged() {
            _oCacheMan.OnChangeFormatting( this.Selections, _rctTextArea.Width );

            this.Invalidate();
        }

        protected void OnWordsCounted() {
            DecorNavigatorUpdate( NavigationSource.API, CaretPos );
        }

        public virtual void OnEvent( BUFFEREVENTS eEvent ) {
            switch( eEvent ) {
                case BUFFEREVENTS.FORMATTED:
                    OnFormatChanged();
                    break;
                case BUFFEREVENTS.WORDSUPDATED:
                    OnWordsCounted();
                    break;
                case BUFFEREVENTS.MULTILINE:
                    OnMultiFinished();
                    break;
                case BUFFEREVENTS.SINGLELINE:
                    OnSingleFinished();
                    break;
                case BUFFEREVENTS.LOADED:
                    break;
            }
        }

        #region IPgTextView Members

        public bool Hidden {
            get {
                return( this.Visible );
            }

            set {
                throw new Exception("The method or operation is not implemented.");
            }
        }

        public SHOWSTATE ShowAs {
            get {
                if( this.Focused )
                    return( SHOWSTATE.Focused );
                else
                    return( SHOWSTATE.Inactive );
                // Need to clarify Active state.
            }
            set {
                throw new Exception("The method or operation is not implemented.");
            }
        }

        /// <summary>
        /// Need to think about this one. I recognize the need for straight up word breaking.
        /// But prsently I simply rely on the main file type parser for all formatting info.
        /// </summary>
		public void WordBreak( Line oLine, ICollection< IPgWordRange > rgWords ) {
			_oDocument.WordBreak( oLine, rgWords );
		}

        public TextPosition Caret {
            get { 
                try {
                    return new TextPosition( CaretPos.At, CaretPos.Offset );
                } catch( NullReferenceException ) {
                    _oSiteView.LogError( "View", "CaretLine accessor exception" );
                    return new TextPosition( 0, 0 );
                }
            }
        }

        /// <summary>
        /// Only updates the screen if the caret is not already on screen.
        /// </summary>
        public void ScrollToCaret()
        {
            if( !_oCacheMan.IsHit( CaretPos ) ) {
                CacheRefresh( RefreshType.RESET, RefreshNeighborhood.CARET);
                CaretIconRefreshLocation();

                Raise_Navigated( NavigationSource.API, CaretPos );

                Invalidate();
            }
        }

        /// <summary>
        /// This is a little different than ScrollToCaret(). We're actually moving the caret,
        /// which requires us to update the _iAdvance.
        /// </summary>
		/// <seealso cref="CaretAndAdvanceReset"/>
        public void ScrollTo( SCROLLPOS eEdge )
        {
            Line oLine   = null;
            int  iOffset = 0;
                
            switch( eEdge ) {
                case SCROLLPOS.TOP:
                    oLine   = _oDocument.GetLine( 0 );
                    iOffset = 0;
                    break;
                case SCROLLPOS.BOTTOM:
                    oLine   = _oDocument.GetLine( _oDocument.ElementCount - 1 );
                    iOffset = oLine.ElementCount;
                    break;
            }

            SelectionClear();
            if( oLine != null ) {
                CaretPos.Line   = oLine;
                CaretPos.Offset = iOffset;
            }

            ScrollToCaret();

            if( _oCacheMan.GlyphLineToPoint( _iCaretColumn, CaretPos, out Point pntCaretWorldLoc ) ) {
                _iAdvance = pntCaretWorldLoc.X;
            }
            CaretIconRefreshLocation();
        }

        /// <summary>
        /// Do selection based on Line/Offset position. But only selects up to the end of the line 
		/// where selection starts. This command moves the Caret.
        /// </summary>
        /// <returns>True if anything is attempted to select. No need to
        /// call document refresh for this operation.</returns>
        public bool SelectionSet( int iLine, int iOffset, int iLength ) {
            Line oLine = _oDocument.GetLine(iLine);

            // Remember caret length is always zero.
            CaretPos.Line   = oLine;
            CaretPos.Offset = iOffset + iLength;

            SelectionClear();

            _rgSelectionTypes.Add( new LineRange( oLine, iOffset, iLength, SelectionTypes.Start ) );
            _oCacheMan.OnChangeSelection( _rgSelectionTypes ); // SelectionClear calls this too! Grrrr...

            // Used to do a cache refresh but I don't think it's necessary.
            Invalidate();

            Raise_Navigated( NavigationSource.API, CaretPos );

            return( true );
        }

        #endregion

        /// <summary>
        /// Find if the given world coordiate is within the selection of primary text.
        /// </summary>
        /// <param name="pntLocation">Point in world coordiates.</param>
        /// <returns>True if the value is within the current selection if there is one!</returns>
        public bool IsSelectionHit( Point pntWorld ) {
            foreach( CacheRow oCache in _oCacheMan ) {
                foreach( LineRange oSelection in _rgSelectionTypes ) {
                    if( oSelection.IsHit( oCache.Line ) ) {
                        if( oCache.IsHit( pntWorld ) ) {
                            int iEdge = oCache.CacheList[0].GlyphPointToOffset( oCache.Top, new SKPointI( pntWorld.X, pntWorld.Y ) );

                            if( oSelection.SelectionType == SelectionTypes.Middle ) {
                                if( iEdge >= oSelection.Offset &&
                                    iEdge <= oSelection.Offset + oCache.Line.ElementCount ) 
                                {
                                    return( true );
                                }
                            } else {
                                if( iEdge >= oSelection.Offset &&
                                    iEdge <  oSelection.Offset + oSelection.Length )
                                {
                                    return( true );
                                }
                            }
                        }
                    }
                }
            } // end for
            return( false );
        }

        public virtual bool Execute(Guid sGuid) {
			switch( sGuid ) {
				case var r when r == GlobalCommands.Delete:
					SelectionDelete();
					return true;
				case var r when r == GlobalCommands.Cut:
					ClipboardCutTo();
					return true;
				case var r when r == GlobalCommands.Copy:
					ClipboardCopyTo();
					return true;
				case var r when r == GlobalCommands.Paste:
					// If I ever need a clipboard 'param' I can get it via a site/host method.
					ClipboardPasteFrom( Clipboard.GetDataObject(), ClipboardOperations.Default );
					return true;
				case var r when r == GlobalCommands.StepLeft:
					OnKeyDown_Page( ScrollEvents.LargeDecrement );
					return true;
				case var r when r == GlobalCommands.StepRight:
					OnKeyDown_Page( ScrollEvents.LargeIncrement );
					return true;
				case var r when r == GlobalCommands.PasteAsImg:
					ClipboardPasteFrom( Clipboard.GetDataObject(), ClipboardOperations.FileDrop );
					return true;
				case var r when r == GlobalCommands.PasteAsText:
					ClipboardPasteFrom( Clipboard.GetDataObject(), ClipboardOperations.Text );
					return true;
				case var r when r == GlobalCommands.PasteAsBase64:
					ClipboardPasteFrom( Clipboard.GetDataObject(), ClipboardOperations.Base64 );
					return true;
				case var r when r == GlobalCommands.Undo:
					_oDocument.Undo();
					return true;
				case var r when r == GlobalCommands.SelectAll:
					SelectionSetAll();
					Invalidate();
					return true;
				case var r when r == GlobalCommands.Play:
					PlayMorse();
					return true;
                case var r when r == GlobalCommands.ReadWrite:
                    _fReadOnly = false;
                    Invalidate();
                    return true;
                case var r when r == GlobalCommands.ReadOnly:
                    _fReadOnly = true;
                    Invalidate();
                    return true;
                case var r when r == GlobalCommands.Stop:
					if( _oMorse != null ) {
						_oMorse.Stop();
						_oMorse = null;
					}
					return true;
			}

			if( _oMorse != null ) {
				return _oMorse.Execute( sGuid );
			}

            return( false );
        }

        /// <summary>
        /// Using this at all is probably not a good thing. I'm going to add
        /// the property pages and production viewer to the main mjolnir editor
        /// window subclass so I can use the forms objects.
        /// </summary>
        public virtual object Decorate( IPgViewSite oBaseSite, Guid sGuid ) {
            return( null );
        }

		public virtual Guid Catagory {
            get {
                return( Guid.Empty );
            }
        }

		public virtual string ToolName(int iTool) {
			try {
				return _rgTools[iTool];
			} catch( ArgumentOutOfRangeException ) {
				return "-out of bounds-";
			}
		}

		public Image ToolIcon(int iTool) {
			return null;
		}

		public virtual int ToolCount => _rgTools.Count;

		public virtual int ToolSelect { 
			get => _iSelectedTool; 
			set { 
				_iSelectedTool = value; 
				_oSiteView.Notify( ShellNotify.ToolChanged );
			}
		}

        protected void PlayMorse() {
			if( _oMorse != null ) {
				switch( _oMorse.Status ) {
					case WorkerStatus.FREE:
						_oMorse = null;
						break;
					case WorkerStatus.PAUSED:
						_oMorse.Start( 0 );
						return;
					case WorkerStatus.BUSY:
						return;
				}
			}

			try {
				IPgMorseWorkerCreater oCreate = (IPgMorseWorkerCreater)_oSiteView.Host.Services;
				_oMorse = oCreate.CreateMorseWorker( _oDocument[CaretPos.At].ToString() );
			} catch( Exception oEx ) {
				Type[] rgErrors = { typeof( NullReferenceException ),
									typeof( InvalidCastException ) };
				if( rgErrors.IsUnhandled( oEx ) )
					throw;
			}
		}

        /// <summary>
        /// Enumerate starting on rhs of cursor, down/around, then lhs of cursor.
        /// </summary>
        public IEnumerator<ILineRange> GetEnumerator() {
            return _oDocument.CreateLineSearch( Caret.Line, Caret.Offset );
        }

        IEnumerator IEnumerable.GetEnumerator() {
            return GetEnumerator();
        }

        public int ElementCount => _oDocument.ElementCount;

        public Line this[int iIndex] => _oDocument[iIndex];

    }

	/// <summary>
	/// A little subclass of the editwindow to turn on the check marks. turn on readonly and have multiline.
    /// I haven't finished moving all the checklist column behavior here yet, but it'll do for now.
	/// </summary>
	public class CheckList : EditWindow2 {
        protected readonly IPgGlyph _oCheque = null;
        public class FTCacheCheck : FTCacheWrap {
            readonly        Func<Line> _oCheckedLine;
            readonly static string     _strCheckMark = "\x2714";
            Line                       _oGuest; // The line we are listing.
            public FTCacheCheck( Line oLine, Line oGuest, Func<Line> oCheckedLine ) : base( oLine ) {
                _oCheckedLine = oCheckedLine ?? throw new ArgumentNullException();
                _oGuest       = oGuest       ?? throw new ArgumentNullException();
            }

            public override void Update(IPgFontRender oFR ) {
                Line.Empty();

                if( _oCheckedLine() == _oGuest ) {
                    Line.TryReplace( 0, Line.ElementCount, _strCheckMark );
                }

                base.Update(oFR);
            }

            public override bool IsInvalid { 
                get {
                    bool fWeAreChecked  = Line[0] == _strCheckMark[0];
                    bool fTheyreChecked = _oCheckedLine() == _oGuest;
                
                    return fWeAreChecked != fTheyreChecked;
                } 
            }
        }

        public class CacheManagerChk : CacheManager2 {
            public CacheManagerChk( CacheManagerAbstractSite oSite, IPgFontRender oFont, List<SmartRect> rgCols ) :
                base( oSite, oFont, rgCols ) {
            }

            protected override CacheRow CreateRow( Line oLine ) {
                CacheRow oRow = base.CreateRow( oLine );

                Func<Line> oCheckedLine = null;

                if( _oSite.Host is EditWindow2 oWin ) {
                    oCheckedLine = new ( oWin.CheckedLine );
                }

                FTCacheLine oElem = new FTCacheCheck( new TextLine( oLine.At, string.Empty ), oLine, oCheckedLine );

                oRow.CacheList.Add( oElem );

                return oRow;
            }
        }

		public CheckList( IPgViewSite oSite, BaseEditor oEditor, bool fReadOnly = false ) : 
            base( oSite, oEditor, fReadOnly:true, fSingleLine:false )     
        {
			ToolSelect = 2; // BUG: change this to an enum in the future.

            uint uiStdUI = _oStdUI.FontCache( _oStdUI.FaceCache( @"C:\windows\fonts\seguisym.ttf" ), 12, DPI );

            IPgFontRender oRender = _oStdUI.FontRendererAt( uiStdUI );
            
            _oCheque = oRender.GetGlyph(0x2714); 
		}

        /// <remarks>
        /// Also, now it should be easy(ier) to make a list of random checkbox's,
        /// as opposed to the radio buttons here, since we can use the 
        /// Line.Extra or the new system for the multi line document I'm considering!!
        /// </remarks>
        protected override void InitColumns() {
            // Need this here for now. The _oCheque doesn't get cached until the cache manager
            // is set up. We can fix that but I'll do it later.
            _rctCheques = new LayoutRect( LayoutRect.CSS.Pixels, (uint)_oCheque.Coordinates.advance_x, 0 );

            _rgLayout  .Add( _rctCheques );  // New select column!!
            _rgLayout  .Add( _rctTextArea ); // Main text area.

            _rgCacheMap.Add( _rctTextArea ); // Text is always the first cache element on a row.
            _rgCacheMap.Add( _rctCheques );
        }

        protected override CacheManager2 CreateCacheManager(uint uiStdText) {
            return new CacheManagerChk( new CacheManSlot(this),
                                        _oStdUI.FontRendererAt(uiStdText),
                                        _rgCacheMap );
        }

        /// <summary>
        /// Currently, I re-measure the code point each time there is a screen
        /// update. With the old system, I was using the Glyph directly and so
        /// didn't need to re-measure. Of course, if we ever dynamically change
        /// the face size, then this Glyph approach would need work...
        /// </summary>
        //protected override void OnPaintExtraColumn( SKCanvas skCanvas, SKPaint skPaint, CacheRow oRow ) {
        //    if( _oDocument.CheckedLine == oRow.Line )
        //        DrawGlyph(skCanvas, skPaint, _rctCheques.Left, RenderAt(oRow, _rctTextArea).Y, _oCheque );
        //}
	}
}
