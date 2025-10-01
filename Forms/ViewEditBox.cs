using System;
using System.Collections.Generic;
using System.Collections;
using System.Drawing;
using System.Windows.Forms;
using System.Reflection;

using SkiaSharp.Views.Desktop;
using SkiaSharp;

using Play.Interfaces.Embedding;
using Play.Rectangles;
using Play.Edit;
using Play.Drawing;
using Play.Parse;

namespace Play.Controls {
    /// <summary>
    /// Normally, I go thru a two step initialization. But here, the image
    /// document is ready the moment it's feet hit the ground.
    /// </summary>
    /// <remarks>Its a shame I'm creating a bitmap for every dropdown instantiated.</remarks>
    public class ImageForDropDown : ImageBaseDoc {
        public ImageForDropDown(IPgBaseSite oSiteBase) : base(oSiteBase) {
            Bitmap = GetSKBitmapResource( Assembly.GetExecutingAssembly(), 
                                          "Play.Forms.Content.icons8-triangle-96.png" );
        }
    }

    public interface IPgViewChild : IPgViewSite {
        void OnChildDisposed();
    }

    /// <summary>
    /// This is basically a single line editor. Right now it's specialized to show
    /// a single line of text and a bitmap on the right.
    /// </summary>
    public abstract class ViewEditBox :
        SKControl,
        IPgParent,
        IPgLoad,
        IPgCommandBase,
        IPgEditEvents
    {
        public static Guid _sGuid = new Guid( "{B14BF576-61AE-4987-9618-439B6CF8AAA7}" );

        protected readonly IPgViewSite    _oSiteView;
        protected readonly IPgViewNotify  _oViewEvents;
		protected readonly IPgStandardUI2 _oStdUI;
        protected ushort StdFace { get; }
        protected uint   StdFont { get; }


        protected readonly LayoutStack           _rgLayout = new LayoutStackHorizontal() { Spacing = 5, Units = LayoutRect.CSS.Flex };
        protected readonly FTCacheLine           _oCacheLine; // this is our single text we are displaying.
        protected readonly Line                  _oTextLine;
        protected readonly IPgDocTraits<Row>     _oDocTraits; // Document holding our list.
        protected readonly IPgDocCheckMarks      _oDocChecks;
        protected readonly IPgDocOperations<Row> _oDocOps;
        protected readonly IReadableBag<Row>     _oDocBag;
        protected readonly ImageBaseDoc          _oBmpButton; // Bitmap for the button.
        protected readonly SmartRect             _rctWorldPort = new(); // The part of the bitmap we want to show.
        protected readonly SmartRect             _rctViewPort  = new(); // where to show the part of the bmp we want to show!

        public IPgParent Parentage => _oSiteView.Host;
        public IPgParent Services  => Parentage.Services;
        protected IPgFontRender FontRender => _oStdUI.FontRendererAt( StdFont );

        protected Control Popup { get; set; } = null;

        public abstract ViewDDPopup CreatePopup();

		protected class WinSlot :
			IPgViewSite,
            IPgViewChild
		{
			protected readonly ViewEditBox _oHost;

            public WinSlot( ViewEditBox oHost ) {
				_oHost = oHost ?? throw new ArgumentNullException();

                // We check if the popup isopen before we attempt to create
                // it so if it is true now we're in a weird state...
                if( _oHost.Popup is not null ) {
                    throw new InvalidOperationException( "Popup confusion. T_T" );
                }
			}

			public IPgParent Host => _oHost;

			public void LogError(string strMessage, string strDetails, bool fShow=true) {
				_oHost._oSiteView.LogError( strMessage, strDetails );
			}

			public void Notify( ShellNotify eEvent ) {
				_oHost._oSiteView.Notify( eEvent );
			}

            /// <summary>
            /// this might happen because either 
            /// 1) the popup closed itself "esc key"
            /// 2) We pressed the dropdown button again causing focus to be lost and
            ///    the control distroys itself.
            /// 3) select somewhere else in the document, causing focus to be lost.
            /// </summary>

            public IPgViewNotify EventChain => _oHost._oSiteView.EventChain;

            public void OnChildDisposed() {
                _oHost.Popup = null;
            }
        }


        /// <summary>
        /// This is my implementation of a dropdown control. At present the text is not editable
        /// and I expect I won't need editing for some time.
        /// </summary>
        /// <param name="oViewSite">Site from our parent.</param>
        /// <param name="oDocument">A multiline/multicolumn doc holding our list.</param>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="ArgumentException"></exception>
        public ViewEditBox( IPgViewSite oViewSite, object oDocument ) {
            _oSiteView = oViewSite ?? throw new ArgumentNullException();
            _oStdUI    = Services as IPgStandardUI2 ?? throw new ArgumentException( "Parent view must provide IPgStandardUI service" );

            _oDocTraits = (IPgDocTraits<Row>    )oDocument;
            _oDocChecks = (IPgDocCheckMarks     )oDocument;
            _oDocBag    = (IReadableBag<Row>    )oDocument;
            _oDocOps    = (IPgDocOperations<Row>)oDocument;
            _oBmpButton = new ImageForDropDown( new WinSlot(this) );

            IPgMainWindow.PgDisplayInfo oInfo = new IPgMainWindow.PgDisplayInfo();
            if( _oSiteView.Host.TopWindow is IPgMainWindow oMainWin ) {
                oInfo = oMainWin.MainDisplayInfo;
            }
            SKPoint pntDPI = new SKPoint( oInfo.pntDpi.X, oInfo.pntDpi.Y );

            // BUG: This is a hot mess.
            StdFace = _oStdUI.FaceCache(@"C:\windows\fonts\consola.ttf");
            StdFont = _oStdUI.FontCache( StdFace, 12, pntDPI ); 

            _oTextLine  = new TextLine( 0, "-no selection-" );
            _oCacheLine = new FTCacheWrap( _oTextLine );
        }

        protected override CreateParams CreateParams {
            get {
                CreateParams oCP = base.CreateParams;

                //oCP.Style &= ~unchecked((int)(WindowStyles.WS_CLIPCHILDREN));

                oCP.ExStyle |= (int)(WindowStyles.WS_EX_TOOLWINDOW );

                return oCP;
            }
        }
        protected void LogError( string strMessage ) {
            _oSiteView.LogError( "Drop Down", strMessage );
        }

        public bool InitNew() {
            if( _oBmpButton.Bitmap == null )
                return false;

            _oDocOps.ListenerAdd( this ); // Look for check mark move via this.

            _rgLayout.Add( new LayoutRect( LayoutRect.CSS.None   )); // Text.
            _rgLayout.Add( new LayoutRect( LayoutRect.CSS.Pixels, 15, (float)0.1 ) ); // Arrow bitmap.

            // Show the whole bitamp. Don't look for changes, not a high pri thing.
            _rctWorldPort.SetRect( 0, 0, _oBmpButton.Bitmap.Width, _oBmpButton.Bitmap.Height );

            // Set our text value to the checked line...
            OnDocLoaded();

            return true;
        }

        protected override void Dispose(bool disposing) {
            _oDocOps.ListenerRemove( this );

            base.Dispose(disposing);
        }

        protected override void OnSizeChanged( EventArgs e ) {
            base.OnSizeChanged(e);

            _rgLayout.SetRect( 0, 0, Width, Height );
			_rgLayout.LayoutChildren();

            ImageHelpers.ViewPortSizeMax( new( 0, 0 ), new( _rgLayout.Item(1).Width, Height ), _rctWorldPort, _rctViewPort );

            ReMeasureText();
        }

        public override Size GetPreferredSize(Size proposedSize) {
            return new Size( proposedSize.Width, (int)FontRender.LineHeight + 4 );
        }

        /// <summary>
        /// Our sole text element is changed, so remeasure everything...
        /// </summary>
        protected void ReMeasureText() {
			_oCacheLine.Measure     ( FontRender );
            _oCacheLine.Colorize    ( (ILineRange)null ); // Add selection when have it.
            _oCacheLine.OnChangeSize( _rgLayout.Item(0).Width );
        }

        /// <summary>
        /// Paint BG depending on various highlight modes for the row/column.
        /// </summary>
        protected void PaintSquareBG( 
            SKCanvas       skCanvas, 
            IPgCacheRender oElemInfo,
            SmartRect      rctCRow 
        ) {
            try {
                using SKPaint skPaint = new SKPaint() {
                    BlendMode = SKBlendMode.Src,
                    Color     = _oStdUI.ColorsStandardAt( StdUIColors.BGReadOnly )
                };
                skCanvas.DrawRect( new SKRect( 0, 0, Width, Height ), skPaint);

                StdUIColors eBg = StdUIColors.Max;

                if( Focused )
                    eBg = StdUIColors.BGWithCursor;
                 // Since our textline is NOT from the doc list this works
                 // b/c we set the Line.At to the Row.At position!! ^_^
                if( _oDocTraits.HighLight?.At == _oCacheLine.Line.At )
                    eBg = _oDocTraits.PlayHighlightColor;

                if( oElemInfo.BgColor != SKColors.Transparent ) {
                    skPaint .BlendMode = SKBlendMode.Src;
                    skPaint .Color     = oElemInfo.BgColor;
                    skCanvas.DrawRect( rctCRow.SKRect, skPaint );
                    return;
                }
                if( eBg != StdUIColors.Max ) {
                    skPaint .BlendMode = SKBlendMode.Src;
                    skPaint .Color     = _oStdUI.ColorsStandardAt( eBg );
                    skCanvas.DrawRect( rctCRow.SKRect, skPaint );
                }
            } catch( NullReferenceException ) {
                LogError( "Problem painting row/col bg" );
            }
        }

        protected override void OnPaintSurface(SKPaintSurfaceEventArgs e) {
            base.OnPaintSurface(e);

            try {
                SKSurface skSurface = e.Surface;
                SKCanvas  skCanvas  = skSurface.Canvas;

                // Test pattern...
                //skPaint.Color = iCache % 2 == 0 ? SKColors.Blue : SKColors.Green;
                //skCanvas.DrawRect( rctSquare.SKRect, skPaint );
                PaintSquareBG( skCanvas, _oCacheLine, _rgLayout.Item(0) ); // Backgrounds.

                using SKPaint skPaintTx = new SKPaint() { FilterQuality = SKFilterQuality.High };

                _oCacheLine.Render(skCanvas, _oStdUI, skPaintTx, _rgLayout.Item(0), this.Focused );

                // Some kind of side effect going on with the Paint object in the Render() function...
                // So just use a new one... :-/
                using SKPaint skPaint = new SKPaint() { FilterQuality = SKFilterQuality.High,
                                                        BlendMode     = SKBlendMode.Src };

				skCanvas.DrawRect( _rgLayout.Item(1).SKRect, skPaint );
                skCanvas.DrawBitmap( _oBmpButton.Bitmap, _rctWorldPort.SKRect, _rgLayout.Item(1).SKRect,skPaint );
                
                // BG of multi column window is always ReadOnly... O.o

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

        /// <remarks>
        /// Sometimes you have the case where you need to check the
        /// position of the mouse w/o any movement. But I can't think of a 
        /// case where that will matter at the moment.
        /// </remarks>
        /// <param name="e"></param>
        protected override void OnMouseMove(MouseEventArgs e) {
            base.OnMouseMove( e );

            Cursor = Cursors.Arrow;

            if( _rgLayout.Item(1).IsInside( e.Location.X, e.Location.Y ) ) {
                Cursor = Cursors.Hand;
            }
        }

        /// <summary>
        /// https://learn.microsoft.com/en-us/windows/win32/winmsg/window-features#pop-up-windows
        /// </summary>
        /// <remarks>You MUST capture after any window was created. Else you lose capture. 
        /// And you MUST capture on mouse up. If on mouse down, then the newly created window
        /// which is now the active window catches the next up and kills itself!!</remarks>
        protected override void OnMouseUp( MouseEventArgs e ) { 
            base.OnMouseDown( e ); 

            if( Popup is not null ) {
                Popup.Capture = true;
            }
        }

        protected override void OnMouseDown( MouseEventArgs e ) { 
            base.OnMouseDown( e ); 

            if( Popup is null ) {
                ViewDDPopup oPopup = CreatePopup();

                PreparePopup( oPopup );
                Popup = oPopup;
                Capture = true;
            }
        }

        /// <summary>
        /// Take the newly created popup, Initialize it and position it.
        /// </summary>
        protected void PreparePopup( ViewDDPopup oPopup ) {
            oPopup.Parent = this;
            oPopup.InitNew();

            Size oPrefSize = oPopup.GetPreferredSize( new Size( Width, 10000 ) ); 
            
            SmartRect oRect  = new SmartRect( LOCUS.UPPERRIGHT, Right, Bottom, oPrefSize.Width, oPrefSize.Height );

            // Where the popup should be in coords of parent of our VDD control.
            Point oTopLeft  = new Point( oRect.Left, oRect.Top );
            // Popup's are in screen coordinates.
            oPopup.Location = this.Parent.PointToScreen( oTopLeft );
            oPopup.Size     = new Size( oRect.Width, oRect.Height ); 

            oPopup.Show();
        }

        public bool Execute(Guid sGuid) {
            return false;
        }

        /// <see cref="IPgEditEvents"/>

        /// <summary>
        /// At present we are NOT an editble dropdown. So if there are
        /// any changes it's due to the primary document being edited
        /// in some manner.
        /// </summary>
        /// <seealso cref="InitNew()"/>
        public virtual void OnDocFormatted() {
            _oCacheLine.Colorize( (ILineRange)null ); // Add selection when have it.

            Invalidate();
        }

        public void OnDocUpdateBegin() {
        }


        public void OnDocUpdateEnd( IPgEditEvents.EditType eType, Row oRowIn ) {
            try {
                Row oRow = _oDocChecks.CheckedRow;
            
                if( oRow != null ) {
                    _oTextLine.TryReplace( 0, _oTextLine.ElementCount, oRow[1].AsSpan ); // replace the text.
                    _oTextLine.Summate( oRow.At, 0 );
                } else {
                    _oTextLine.TryReplace( 0, _oTextLine.ElementCount, "-no selection-" );
                    _oTextLine.Summate( -2, 0 );
                }

                ReMeasureText();
                Invalidate   ();
            } catch( Exception oEx ) {
                Type[] rgErrors = { typeof( NullReferenceException ),
                                    typeof( IndexOutOfRangeException ),
                                    typeof( ArgumentOutOfRangeException ) };
                if( rgErrors.IsUnhandled(oEx) )
                    throw;

                LogError( "ViewDropDown Edit Handler Error" );
            }
        }

        public void OnDocLoaded() {
            OnDocUpdateEnd( IPgEditEvents.EditType.Rows, null );
        }

        protected class DumbCaret : IPgCaretInfo<Row> {
            public Row Row    { get => null; set { } }
            public int Column => 0;
            public int Offset { get; set;}
            public int Length {
                get => 0;
                set { throw new NotImplementedException(); }
            }
        }

        static DumbCaret _oCaret = new DumbCaret(); 

        public IPgCaretInfo<Row> Caret2 => _oCaret;
    }

    public enum WindowStyles : UInt32 {
        WS_POPUP             = 0x80000000,
        WS_VISIBLE           = 0x10000000,
        WS_CLIPSIBLINGS      = 0x04000000,
        WS_CLIPCHILDREN      = 0x02000000,
        WS_MAXIMIZEBOX       = 0x00010000,
        WS_BORDER            = 0x00800000,
        WS_CHILD             = 0x40000000,
                             
        WS_EX_LEFT           = 0x00000000,
        WS_EX_LTRREADING     = 0x00000000,
        WS_EX_RIGHTSCROLLBAR = 0x00000000,
        WS_EX_TOPMOST        = 0x00000008,
        WS_EX_TOOLWINDOW     = 0x00000080,
    }

    public enum MouseActivate : int {
        MA_ACTIVATE         = 1, //	Activates the window, and does not discard the mouse message.
        MA_ACTIVATEANDEAT   = 2, //	Activates the window, and discards the mouse message.
        MA_NOACTIVATE       = 3, // Does not activate the window, and does not discard the mouse message.
        MA_NOACTIVATEANDEAT = 4
    }
    
    /// <summary>
    /// This is our popup for the drop down control. Its like a secondary view in that
    /// it uses the same document as the ViewDropDown object
    /// </summary>
    /// <seealso cref="ViewEditBox"/>
    public abstract class ViewDDPopup :
        WindowMultiColumn
    {
        private const int WM_ACTIVATE      = 0x0006;
        private const int WM_MOUSEACTIVATE = 0x0021;

        protected IPgDocCheckMarks _oDocCheckMark;

        // This is how we tell the owner window we're dead.
        protected readonly IPgViewChild _oSiteCtrl;

        public ViewDDPopup( IPgViewSite oView, object oDocument ) : base( oView, oDocument ) {
            _oDocCheckMark = (IPgDocCheckMarks)oDocument;
            _oSiteCtrl     = (IPgViewChild)_oSiteView;

            IsScrollVisible = false;
        }

        protected override void Dispose(bool disposing) {
            if( disposing ) {
                _oSiteCtrl.OnChildDisposed();
            }
            base.Dispose(disposing);
        }

        protected override bool Initialize() {
            if( !base.Initialize() )
                return false;


            return true;
        }

        protected override CreateParams CreateParams {
            get {
                CreateParams createParams = base.CreateParams;

                createParams.Style = unchecked((int)(
                                         WindowStyles.WS_POPUP |
                                         WindowStyles.WS_VISIBLE |
                                         //WindowStyles.WS_CLIPSIBLINGS |
                                         //WindowStyles.WS_CLIPCHILDREN |
                                         WindowStyles.WS_BORDER ));
                createParams.ExStyle = (int)(WindowStyles.WS_EX_TOPMOST |
                                             WindowStyles.WS_EX_TOOLWINDOW );
                //                           WindowStyles.WS_EX_LEFT
                //                           WindowStyles.WS_EX_LTRREADING |
                //                           WindowStyles.WS_EX_RIGHTSCROLLBAR  

                createParams.Parent  = HostHandle;

                return createParams;
            }
        }

        /// <summary>
        /// This seems to interfere with setting the capture of the mouse
        /// so I'm commenting it out. It's the recommended way to design a popup
        /// but I don't see how it helps in anyway. :-/
        /// </summary>
        //protected override void WndProc(ref Message m) {
        //    switch( m.Msg ) {
        //        case WM_ACTIVATE: {
        //            if( (int)m.WParam == 1 ) {
        //                // window is being activated
        //                if( HostHandle != IntPtr.Zero ) {
        //                    User32.SetActiveWindow(HostHandle);
        //                }
        //            }
        //            break;
        //        }
        //        case WM_MOUSEACTIVATE: {
        //            m.Result = new IntPtr((int)MouseActivate.MA_NOACTIVATE);
        //            return;
        //        }
        //    }
        //    base.WndProc(ref m);
        //}

        protected override void OnKeyUp(KeyEventArgs e) {
            base.OnKeyUp(e);
            e.Handled = true;

            if( e.KeyCode == Keys.Escape ) {
                Hide   ();
                Dispose();
            }
        }

        /// <summary>Make sure to set the capture to this control. </summary>
        /// <remarks>Must process the check in MOUSE UP. Because if we dispose ourselves on
        /// mouse down. Then the window underneath gets the mouse up!!!!
        /// This means you probably get weird effects if you mouse down in on window
        /// and them mouse up in another!! O.o
        /// Alas. Only the active window can capture the mouse. And if
        /// we're preventing ourself to be activated that is a problem...
        /// So I've disabled that code.</remarks>
        protected override void OnMouseUp( MouseEventArgs e ) {
            // Assuming we've captured the mouse...
            SmartRect rcClient = new SmartRect( 0, 0, Width, Height );

            if( rcClient.IsInside( e.X, e.Y ) ) {
                if( _oCacheMan.IsRowHit( new SKPointI( e.X, e.Y ), out _ ) is CacheRow oCRow ) {
                    _oDocCheckMark.SetCheckAtRow( oCRow.Row );
                    // BUG: Need to ignore scroll bar activity...
                }
            }
            Hide   ();
            Dispose();
        }

        protected IntPtr HostHandle {
            get { 
                if( _oSiteView.Host is Control oParent ) {
                    return oParent.Handle;
                }
                return IntPtr.Zero;
            }
        }
    }
}
