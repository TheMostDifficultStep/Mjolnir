using System;
using System.Collections.Generic;
using System.Windows.Forms;
using System.Drawing;
using System.Diagnostics;
using System.IO;

using SkiaSharp;
using SkiaSharp.Views.Desktop;

using Play.Interfaces.Embedding;
using Play.Edit;
using Play.Rectangles;
using Play.Parse;

namespace Play.Forms {
    /// <summary>
    /// This is slightly a misnomer in that a single Line can be wrapped. What
    /// we mean is a solitary Line. This object will be part of my new Forms object.
    /// </summary>
    /// <seealso cref="LayoutText"/>
    public class LayoutSingleLine : LayoutRect {
        public FTCacheLine Cache { get; }
        public SimpleRange Selection { get; } = new SimpleRange(0);
        public SKColor     BgColor { get; set; } = SKColors.LightGray;
        public SKColor     FgColor { get; set; } = SKColors.Red;

        // Normally selection lives on the view, but I'll put it here for forms for now.
        protected ILineSelection[] _rgSelections = new ILineSelection[1];

        public LayoutSingleLine( FTCacheLine oCache, CSS eCSS ) : base( eCSS ) {
            Cache = oCache ?? throw new ArgumentNullException();
            _rgSelections[0] = Selection;
        }

        public void Paint( SKCanvas skCanvas, IPgStandardUI2 oStdUI, bool fFocused ) {
            SKPointI pntUL = this.GetPoint(LOCUS.UPPERLEFT);

            // Center the text if our rect is bigger than the cache height.
            if( Cache.Height < Height ) {
                pntUL.Y = pntUL.Y + Height / 2 - Cache.Height / 2;
            }

            PaintBackground( skCanvas );

            // Draw text.
            Cache.Render( skCanvas, oStdUI, new PointF( pntUL.X, pntUL.Y ), fFocused );
        }

        /// <summary>
        /// Allow you to override the default background paint.
        /// </summary>
        public override void PaintBackground( SKCanvas skCanvas ) {
            if( BgColor != SKColors.Transparent ) {
                using SKPaint skPaint = new SKPaint() { Color = BgColor };
                skCanvas.DrawRect( this.Left, this.Top, this.Width, this.Height, skPaint );
            }
        }

        /// <summary>
        /// Mostly test code. The color of the text is just the foreground color.
        /// </summary>
        public override void Paint( SKCanvas skCanvas ) {
            using SKPaint skPaint = new SKPaint();
            SKPointI pntUL = this.GetPoint(LOCUS.UPPERLEFT);

            // Center the text if our rect is bigger than the cache height.
            if( Cache.Height < Height ) {
                pntUL.Y = pntUL.Y + Height / 2 - Cache.Height / 2;
            }

            PaintBackground( skCanvas );

            skPaint.Color = FgColor;

            // Draw text.
            Cache.Render( skCanvas, skPaint, new PointF( pntUL.X, pntUL.Y ) );
        }

        /// <seealso cref="FTCacheLine.Update"/>
        /// <seealso cref="FTCacheLine.UnwrappedWidth"/>
        public override uint TrackDesired(TRACK eParentAxis, int uiRail) {
            // Looks like unwrapped width of each character is not being summated for the cache
            // element on Update(); In any event, it might just be best to recalc here anyway.
            Cache.OnChangeSize( uiRail );

            switch( eParentAxis ) {
                case TRACK.HORIZ: return (uint)Cache.UnwrappedWidth;
                case TRACK.VERT : return (uint)Cache.Height;
                default: throw new ArgumentOutOfRangeException();
            }
        }

        protected override void OnSize() {
            base.OnSize();
            Cache.OnChangeSize( Width );
        }

        public void OnChangeFormatting() {
            Cache.OnChangeFormatting( _rgSelections );
        }

        public SKPointI CaretWorldPosition( ILineRange oCaret ) {
            // Check that the line matches.
            Point oWorldLoc = Cache.GlyphOffsetToPoint( oCaret.Offset );
            return new SKPointI( Left + oWorldLoc.X, Top + oWorldLoc.Y );
        }

        public SKPointI ClientToWorld( SKPointI pntClientMouse ) {
            SKPointI pntLocation = new SKPointI( pntClientMouse.X - Left,
                                                 pntClientMouse.Y - Top  );

            return ( pntLocation );
        }

        public bool SelectionIsHit( Point pntClient ) { 
            if( this.IsInside( pntClient.X, pntClient.Y ) ) {
                SKPointI pntWorld = new SKPointI( pntClient.X - Left,
                                                  pntClient.Y - Top);
                int iEdge = Cache.GlyphPointToOffset( pntWorld );

                return( iEdge >= Selection.Offset &&
                        iEdge <  Selection.Offset + Selection.Length );
            }
            return false;
        }

        public void SelectHead( IPgCacheCaret oCaret, Point pntClient, bool fGrow ) {
            SKPointI pntWorld = new SKPointI( pntClient.X - Left,
                                              pntClient.Y - Top);
            int iEdge = Cache.GlyphPointToOffset( pntWorld );

            oCaret.Offset  = iEdge;
            oCaret.Advance = pntWorld.X;

            if ( fGrow ) {
                Selection.Grow  = iEdge;
            } else {
                Selection.Start = iEdge;
            }
            Cache.OnChangeFormatting( _rgSelections );
        }

        public void SelectNext( IPgCacheCaret oCaret, Point pntClient ) {
            SKPointI pntWorld = new SKPointI( pntClient.X - Left,
                                              pntClient.Y - Top);
            int iEdge = Cache.GlyphPointToOffset( pntWorld );

            Selection.Grow = iEdge;
            oCaret.Offset  = iEdge;
            oCaret.Advance = Cache.GlyphOffsetToPoint( oCaret.Offset ).X;

            Cache.OnChangeFormatting( _rgSelections );
        }
    }

    public class LayoutColorBgLine : LayoutSingleLine {
        public List<SKColor> Colors { get; } = new List<SKColor>();
        public List<float>   Points { get; } = new List<float>();
        public LayoutColorBgLine( FTCacheLine oCache, CSS eCSS ) : base( oCache, eCSS ) {
        }

        public override void PaintBackground(SKCanvas skCanvas) {
            if( Colors.Count <= 0 ) {
                base.PaintBackground( skCanvas );
                return;
            }

            using SKPaint skPaint = new() { BlendMode = SKBlendMode.SrcATop, IsAntialias = true };

            if( Points.Count <= 0 ) {
                for( int i = 0; i < Colors.Count; ++i ) {
                    float flPoint = i / ( Colors.Count - 1 );
                    Points.Add( flPoint );
                }
            }

            // Create linear gradient from left to Right
            skPaint.Shader = SKShader.CreateLinearGradient(
                                new SKPoint( Left,  Top),
                                new SKPoint( Right, Bottom),
                                Colors.ToArray(),
                                Points.ToArray(),
                                SKShaderTileMode.Repeat );

            try {
                skCanvas.DrawRect( Left, Top, Width, Height, skPaint );
            } catch( Exception oEx ) {
                Type[] rgErrors = { typeof( ArgumentNullException ),
									typeof( ArgumentException ),
									typeof( NullReferenceException ),
									typeof( OverflowException ),
									typeof( AccessViolationException ) };
                if( rgErrors.IsUnhandled( oEx ) )
                    throw;
            }
        }
    }

    public class HyperlinkCollection {
        public Dictionary<string, HyperLink> HyperLinks { get; } = new Dictionary<string, HyperLink>();

        public void Add( string strName, HyperLink oLink ) {
            HyperLinks.Add( strName, oLink );
        }
        /// <summary>
        /// Stolen from EditWindow2, we'll port this back soon.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public static IPgWordRange FindFormattingUnderRange( Line oLine, int iOffset ) {
            if( oLine == null )
                return( null );
            if( iOffset < 0 )
                throw new ArgumentOutOfRangeException( nameof( iOffset ) );

            IPgWordRange oTerminal = null;

            try { 
                foreach(IPgWordRange oTry in oLine.Formatting ) {
                    if( oTry is IPgWordRange oRange &&
                        iOffset >= oRange.Offset &&
                        iOffset  < oRange.Offset + oRange.Length )
                    {
						// The first word we find is the best choice.
						if( oRange.IsWord ) {
							return oRange;
						}
						// The term under the carat is OK, But keep trying for better...
						if( oRange.IsTerm ) {
							oTerminal = oRange;
						}
                    }
                }
            } catch( Exception oEx ) { 
                Type[] rgErrors = { typeof( NullReferenceException ), 
                                    typeof( InvalidCastException )};
                if( rgErrors.IsUnhandled( oEx ))
                    throw;
            }
            return( oTerminal );
        }

        public bool Find( Line oLine, int iPosition, bool fDoJump ) {
            IPgWordRange oRange = FindFormattingUnderRange( oLine, iPosition );
            if( oRange != null ) { 
                foreach( KeyValuePair<string, HyperLink> oPair in HyperLinks ) { 
                    if( oRange.StateName == oPair.Key ) {
                        if( fDoJump )
                            oPair.Value?.Invoke( oLine, oRange );
                        return true;
                    }
                }
            }
            return false;
        }
    }

    /// <summary>
    /// This forms window gives us text without having to have numerous sub
    /// text windows to do that job. Text is held in single line cache elements.
    /// And this window can layout any LayoutRect object.
    /// </summary>
    /// <seealso cref="LayoutRect"/>
    public class FormsWindow : SKControl {
        protected bool _fDisposed { get; private set; }

		protected readonly IPgViewSite   _oSiteView;
        protected readonly IPgViewNotify _oViewEvents; // Our site from the window manager (view interface).

        // Just blast the old forms Layout member with my prefered implementation.
        protected new ParentRect         Layout    { get; set; } = new LayoutStackHorizontal() { Spacing = 5 };
        protected List<LayoutSingleLine> CacheList { get; }      = new List<LayoutSingleLine>();

        protected Editor           DocForms { get; }
        protected SimpleCacheCaret Caret    { get; }
        public    uint             StdText  { get; set; }
        protected IPgStandardUI2   StdUI    { get; }

        public HyperlinkCollection Links { get; } = new ();

        readonly static Keys[] _rgHandledKeys = { Keys.PageDown, Keys.PageUp, Keys.Down,
                                                  Keys.Up, Keys.Right, Keys.Left, Keys.Back,
                                                  Keys.Delete, Keys.Enter, Keys.Tab,
                                                  Keys.Control | Keys.A, Keys.Control | Keys.F };

        public FormsWindow( IPgViewSite oSiteView, Editor oDocForms ) {
			_oSiteView   = oSiteView ?? throw new ArgumentNullException( "Find window needs a site!!" );
            _oViewEvents = oSiteView.EventChain ?? throw new ArgumentException("Site.EventChain must support IPgViewSiteEvents");
            DocForms     = oDocForms ?? throw new ArgumentNullException( "Forms needs a text buffer" );
            StdUI        = (IPgStandardUI2)oSiteView.Host.Services;

            Array.Sort<Keys>(_rgHandledKeys);

            Caret = new SimpleCacheCaret( null );
        }
        
        /// <summary>
        /// Unplug events so we won't get any callbacks when we're dead.
        /// </summary>
        protected override void Dispose( bool disposing ) {
            if( disposing && !_fDisposed ) {
                DocForms.BufferEvent -= OnDocumentEvent;
                DocForms.CaretRemove( Caret );

                _fDisposed = true;
            }

            base.Dispose(disposing);
        }

        protected void LogError( string strMessage ) {
            _oSiteView.LogError( "Form", strMessage );
        }

        public virtual bool InitNew() {
            // This changed from ContextMenu to ContextMenuStrip in .net 5
            if( this.ContextMenuStrip == null ) {
                ContextMenuStrip oMenu = new ContextMenuStrip();
                oMenu.Items.Add( new ToolStripMenuItem( "Cut",   null, new EventHandler( this.OnCut    ), Keys.Control | Keys.X ) );
                oMenu.Items.Add( new ToolStripMenuItem( "Copy",  null, new EventHandler( this.OnCopy   ), Keys.Control | Keys.C ) );
                oMenu.Items.Add( new ToolStripMenuItem( "Paste", null, new EventHandler( this.OnPaste  ), Keys.Control | Keys.V ) );
                this.ContextMenuStrip = oMenu;
            }

            DocForms.BufferEvent += OnDocumentEvent;
            DocForms.CaretAdd( Caret ); // Document moves our caret and keeps it in sync.

            IPgMainWindow.PgDisplayInfo oInfo = new IPgMainWindow.PgDisplayInfo();
            if( _oSiteView.Host.TopWindow is IPgMainWindow oMainWin ) {
                oInfo = oMainWin.MainDisplayInfo;
            }

            StdText = StdUI.FontCache(StdUI.FaceCache(@"C:\windows\fonts\consola.ttf"), 12, oInfo.pntDpi);

            return true;
        }

        protected void BrowserLink( string strUrl ) {
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

        public virtual void Submit() {
        }

        // Let Forms know what keys we want sent our way.
        protected override bool IsInputKey(Keys keyData) {
            int iIndex = Array.BinarySearch<Keys>(_rgHandledKeys, keyData);

            if (iIndex >= 0)
                return (true);

            return base.IsInputKey( keyData );
        }

        /// <summary>
        /// Just update the entire cache. We'll get more selective in the future.
        /// </summary>
        protected virtual void OnDocumentEvent( BUFFEREVENTS eEvent ) {
            switch( eEvent ) {
                case BUFFEREVENTS.FORMATTED:
                case BUFFEREVENTS.SINGLELINE:
                case BUFFEREVENTS.MULTILINE:
                    foreach( LayoutSingleLine oCache in CacheList ) {
                        oCache.Cache.Update( StdUI.FontRendererAt( StdText ) );
                        oCache.OnChangeFormatting();
                        oCache.Cache.OnChangeSize( oCache.Width );
                    }
                  //OnSizeChanged( new EventArgs() ); need to figure out why this doesn't work.
                    Invalidate();
                    break;
            }
        }

        protected FTCacheLine ElemNext( FTCacheLine oElem, int iDir ) {
            if( oElem.Line.At + iDir < 0 ||
                oElem.Line.At >= DocForms.ElementCount ) {
                return null;
            }
            Line oNext = DocForms[ oElem.Line.At + iDir ];

            foreach( LayoutSingleLine oLayout in CacheList ) {
                if( oLayout.Cache.Line == oNext ) {
                    Caret.Layout = oLayout;

                    //oLayout.SelectHead( Caret, e.Location, ModifierKeys == Keys.Shift );
                    // select all.
                    return oLayout.Cache;
                }
            }
            return null;
        }

        public void CaretMove( Axis eAxis, int iDir, bool fJumpLine = false ) {
            try {
                int   iOffset   = Caret.Offset;
                float flAdvance = Caret.Advance;

                Caret.Layout.Selection.Length = 0;
                Caret.Layout.OnChangeFormatting();

                // If total miss, build a new screen based on the location of the caret.
                FTCacheLine oElem = Caret.Layout.Cache;

                if( iDir != 0 ) {
                    // First, see if we can navigate within the line we are currently at.
                    if( !oElem.Navigate( eAxis, iDir, ref flAdvance, ref iOffset ) || fJumpLine ) {
                        iDir = iDir < 0 ? -1 : 1; // Only allow move one line up or down.

                        FTCacheLine oNext = ElemNext( oElem, iDir );
                        if( oNext != null ) {
                            iOffset = oNext.OffsetBound( eAxis, iDir * -1, flAdvance );
                            oElem   = oNext;
                            // set the advance...
                        }
                    }
                    // If going up or down ends up null, we won't be moving the caret.
                    //Caret.Cache   = oElem;
                    Caret.Offset  = iOffset;
                    Caret.Advance = (int)flAdvance;
                }

                CaretLocal( oElem, iOffset );
                CaretIconRefresh();
            } catch( Exception oEx ) {
                Type[] rgErrors = { typeof( ApplicationException ),
                                    typeof( ArgumentOutOfRangeException ),
                                    typeof( NotImplementedException ),
                                    typeof( NullReferenceException ) };
                if( rgErrors.IsUnhandled( oEx ) )
                    throw;

                _oSiteView.LogError( "Editing", "Problem moving the cursor" );
            }
        }

        /// <summary>
        /// Use this function to deal with scrolling the visible portion of the form.
        /// </summary>
        public virtual void CaretLocal( FTCacheLine oElem, int iOffset ) {
            Invalidate();
        }

        /// <summary>
        /// Reposition the caret.
        /// </summary>
        protected void CaretIconRefresh() {
            if( Focused != true )
                return;

            if( Caret.Layout != null ) {
                SKPointI pntCaretWorldLoc  = Caret.Layout.CaretWorldPosition( Caret ); 
                if( Caret.Layout.IsInside( pntCaretWorldLoc.X, pntCaretWorldLoc.Y ) ) {
                    User32.SetCaretPos(pntCaretWorldLoc.X, pntCaretWorldLoc.Y);
                } else {
                    User32.SetCaretPos( -10, -10 ); // Park it off screen.
                }
            }
        }

        /// <summary>
        /// See if there is non zero selection on the cache element pointed to by the caret.
        /// </summary>
        public bool IsSelection {
            get {
                if( Caret.Layout == null )
                    return false;

                return( Caret.Layout.Selection.Length > 0 );
            }
        }

        public void OnKey_Delete( bool fBackSpace ) {
            try {
                using Editor.Manipulator oBulk = DocForms.CreateManipulator();
                if( IsSelection ) {
                    oBulk.LineTextDelete( Caret.At, Caret.Layout.Selection );
                } else {
                    if( fBackSpace ) {
                        if( Caret.Offset > 0 ) {
                            oBulk.LineTextDelete( Caret.At, new ColorRange( Caret.Offset - 1, 1 ) );
                        }
                    } else {
                        oBulk.LineTextDelete( Caret.At, new ColorRange( Caret.Offset, 1 ) );
                    }
                }
                OnKeyDown_Arrows( Axis.Horizontal, 0 );
            } catch( Exception oEx ) {
                Type[] rgErrors = { typeof( ApplicationException ),
                                    typeof( ArgumentOutOfRangeException ),
                                    typeof( NotImplementedException ),
                                    typeof( NullReferenceException ) };
                if( rgErrors.IsUnhandled( oEx ) )
                    throw;
                _oSiteView.LogError( "Forms", "Unable to delete character." );
            }
        }

        protected override void OnKeyPress(KeyPressEventArgs e) {
            if( this.IsDisposed )
               return;

            try {
                // Ask the shell if this character is a command of some sort. Like when we're used in a dialog.
                // Tab is a bummer: for example g and G are two different characters, we don't need to know
                // the state of the shift key! But tab doesn't have that luxury. Sometimes computers suck!
                if( _oViewEvents.IsCommandPress( e.KeyChar ) ) {
                    return;
                }
                if( e.KeyChar == 0x0009 ) {
                    CaretMove( Axis.Vertical, 1, fJumpLine:true );
                    return;
                }

                if( !char.IsControl( e.KeyChar )  ) { 
                    if( IsSelection ) {
                        using( Editor.Manipulator oBulk = DocForms.CreateManipulator() ) {
                            oBulk.LineTextDelete( Caret.At, Caret.Layout.Selection );
                            oBulk.LineCharInsert( Caret.At, Caret.Offset, e.KeyChar );
                        }
                    } else {
                        DocForms.LineCharInsert( Caret.At, Caret.Offset, e.KeyChar);
                    }
                    // Find the new carat position and update its screen location. 
                    OnKeyDown_Arrows( Axis.Horizontal, 0 );
                }
            } catch( Exception oEx ) {
                Type[] rgErrors = { typeof( ApplicationException ),
                                    typeof( ArgumentOutOfRangeException ),
                                    typeof( NotImplementedException ),
                                    typeof( NullReferenceException ) };
                if( rgErrors.IsUnhandled( oEx ) )
                    throw;
                _oSiteView.LogError( "Forms", "Unable to accept character." );
            }
        }

        protected override void OnPaintSurface(SKPaintSurfaceEventArgs e) {
            base.OnPaintSurface(e);

            SKCanvas skCanvas = e.Surface.Canvas;

            using SKPaint skPaint = new SKPaint() { Color = StdUI.ColorsStandardAt(StdUIColors.BGReadOnly)};
            skCanvas.DrawRect( new SKRect( 0, 0, Width, Height ), skPaint );

            foreach( LayoutSingleLine oCache in CacheList ) {
                skCanvas.Save();
                skCanvas.ClipRect(new SKRect(oCache.Left, oCache.Top, oCache.Right, oCache.Bottom), SKClipOperation.Intersect);
                oCache.Paint(e.Surface.Canvas, StdUI, this.Focused );
                skCanvas.Restore();
            }
            // Layout2.Paint( e.Surface.Canvas ); Use this to see what the columns look like.
        }

        protected override void OnSizeChanged( EventArgs e ) {
			Layout.SetRect( 0, 0, Width, Height );
			Layout.LayoutChildren();

            CaretIconRefresh();

            Invalidate();
        }

        protected override void OnGotFocus(EventArgs e) {
            base.OnGotFocus( e );

            _oSiteView.EventChain.NotifyFocused(true);

            // Not perfect but getting better...
            int iCaratHeight = (int)(StdUI.FontRendererAt( StdText ).FontHeight );

            User32.CreateCaret( this.Handle, IntPtr.Zero, 2, iCaratHeight );
            CaretIconRefresh(); 
            User32.ShowCaret  ( this.Handle );

            Point oMouse = PointToClient( MousePosition );

            this.Invalidate();
        }

        protected override void OnLostFocus(EventArgs e) {
            base.OnLostFocus( e );

            User32.DestroyCaret();
            _oSiteView.EventChain.NotifyFocused(false);

            this.Invalidate();
        }

        protected void OnKeyDown_Arrows( Axis eAxis, int iDir ) {
            CaretMove( eAxis, iDir );
        }

        protected override void OnKeyDown(KeyEventArgs e) {
            if( this.IsDisposed )
                return;

            e.Handled = true;

            switch( e.KeyCode ) {
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
                    
                case Keys.Back:
                case Keys.Delete:
                    OnKey_Delete( e.KeyCode == Keys.Back );
                    break;

                case Keys.Tab:
                    break;

                case Keys.Enter:
                    Submit();
                    break;
            }
            CaretIconRefresh();
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData) {
            if( this.IsDisposed )
                return( false );

            const int WM_KEYDOWN    = 0x100;
            const int WM_SYSKEYDOWN = 0x104;
               
            if ((msg.Msg == WM_KEYDOWN) || (msg.Msg == WM_SYSKEYDOWN)) {
                switch(keyData) {
                    case Keys.Control | Keys.F:
                        _oViewEvents.IsCommandKey( CommandKey.Find, KeyBoardEnum.Control );
                        return( true );
                    case Keys.Control | Keys.A:
                        //SelectionSetAll();
                        Invalidate();
                        return( true );
                    case Keys.Control | Keys.Z:
                        DocForms.Undo();
                        return( true );
                    case Keys.Control | Keys.V:
                        ClipboardPasteFrom( Clipboard.GetDataObject(), ClipboardOperations.Default );
                        return true;
                    case Keys.Control | Keys.C:
                        ClipboardCopyTo();
                        return true;
                    case Keys.Delete: {
                        OnKey_Delete( false );
                        return( true );
                    }
                }
            } 

            return base.ProcessCmdKey( ref msg, keyData );
        } // end method

        protected override void OnMouseDown(MouseEventArgs e) {
            base.OnMouseDown( e );
            Select();

            if( e.Button == MouseButtons.Left ) {
                // Set the caret for sure if hit. If not just leave it where ever it was.
                foreach( LayoutSingleLine oCache in CacheList ) {
                    if( oCache.IsInside( e.X, e.Y ) ) {
                        Caret.Layout = oCache;

                        oCache.SelectHead( Caret, e.Location, ModifierKeys == Keys.Shift );
                        Links.Find( Caret.Line, Caret.Offset, true );
                    }
                }
            }

            CaretIconRefresh();
            Invalidate();
        }

        protected override void OnMouseMove(MouseEventArgs e) {
            base.OnMouseMove(e);

            Cursor oCursor = Cursors.Arrow;

            foreach( LayoutSingleLine oLayout in CacheList ) {
                if( oLayout.IsInside( e.X, e.Y ) ) {
                    SKPointI pntWorld = new SKPointI( e.X - oLayout.Left,
                                                      e.Y - oLayout.Top);
                    int iEdge = oLayout.Cache.GlyphPointToOffset( pntWorld );
                    if( Links.Find( oLayout.Cache.Line, iEdge, false ) ) {
                        oCursor = Cursors.Hand;
                    } else {
                        oCursor = Cursors.IBeam;
                    }
                    break;
                }
            }
            Cursor = oCursor;

            if ((e.Button & MouseButtons.Left) == MouseButtons.Left && e.Clicks == 0 ) {
                if( Caret.Layout != null ) {
                    Caret.Layout.SelectNext( Caret, e.Location );

                    CaretIconRefresh();
                    Invalidate();
                    Update();
                }
            }
        }

        protected override void OnMouseUp(MouseEventArgs e) {
            base.OnMouseUp( e );

            CaretIconRefresh();
        }

        private void OnCut( object o, EventArgs e ) {
            ClipboardCutTo();
        }

        public void ClipboardCutTo() {
            ClipboardCopyTo();
            SelectionDelete();
        }

        protected void SelectionDelete() {
            if( IsSelection ) {
                using( Editor.Manipulator oBulk = DocForms.CreateManipulator() ) {
                    oBulk.LineTextDelete( Caret.At, Caret.Layout.Selection );
                }
            }
        }

        private void OnCopy( object o, EventArgs e ) {
            ClipboardCopyTo();
        }

        private void OnPaste( object o, EventArgs e ) {
            ClipboardPasteFrom( Clipboard.GetDataObject(), ClipboardOperations.Default );
        }

        public virtual void ClipboardCopyTo() {
            DataObject   oDataObject  = new DataObject();
            IMemoryRange oSelection;

			// If I've got a selection use it. Else use current caret pos. Note,
			// right mouse button press, removes any selection, moves the caret 
			// AND brings up the context menu. In the future we'll want to give the
			// option to choose the desired portion of any complex object.
			try {
				if( IsSelection ) {
					oSelection = Caret.Layout.Selection;
				} else {
					oSelection = new ColorRange( 0, Caret.Layout.Cache.Line.ElementCount, 0 );
 				}
				if( oSelection != null ) {
					string strSelection = Caret.Line.SubString( oSelection.Offset, oSelection.Length );

				    oDataObject.SetData      ( strSelection );
				    Clipboard  .SetDataObject( oDataObject );
				}
			} catch( NullReferenceException ) {
			}
        }

        public void ClipboardPasteFrom( 
            object oDataSource, 
            Guid   sOperation ) 
        {
            //if( _fReadOnly )
            //    return;

            if( Caret.Layout == null )
                return;

            try {
                IDataObject oDataObject = oDataSource as IDataObject;

                // TODO: This might be a dummy line. So we need dummies to be at -1.
                //       Still a work in progress. See oBulk.LineInsert() below...
                if( sOperation == ClipboardOperations.Text ||
                    sOperation == ClipboardOperations.Default 
                  ) {
                    string strPaste = oDataObject.GetData(typeof(System.String)) as string;
                    using( Editor.Manipulator oBulk = new Editor.Manipulator( DocForms ) ) {
                        if( IsSelection ) {
                            oBulk.LineTextDelete( Caret.At, Caret.Layout.Selection );
                        }
                        oBulk.LineTextInsert( Caret.At, Caret.Offset, strPaste, 0, strPaste.Length );
                    }
                }
            } catch( Exception oEx ) {
                Type[] rgErrors = { typeof( NullReferenceException ),
                                    typeof( InvalidCastException ) };
                if( rgErrors.IsUnhandled( oEx ) )
                    throw;
            }
        }


    }

	public class ContextMenuTest : SKControl {
		public IPgBaseSite    PgSite { get; }
		public IPgStandardUI2 StdUI  { get; }

		protected FTCacheLine Cache  { get; }

		protected uint _uiStdText; // Font index;

		public ContextMenuTest( IPgBaseSite oSiteBase ) {
			PgSite = oSiteBase ?? throw new ArgumentNullException( "Site for form control must not be null" );

 			StdUI      = PgSite.Host.Services as IPgStandardUI2 ?? throw new ArgumentException( "Parent view must provide IPgStandardUI service" );
            _uiStdText = StdUI.FontCache( StdUI.FaceCache( @"C:\windows\fonts\consola.ttf" ), 12, new SKPoint( 96, 96 ) );

			Cache = new FTCacheLine( new TextLine( 0, "hello" ) );
			Cache.Update( StdUI.FontRendererAt( _uiStdText ) );
			Cache.OnChangeSize( 300 );
		}

		protected override void OnPaintSurface( SKPaintSurfaceEventArgs e ) {
			base.OnPaintSurface( e );

			using SKPaint skPaint = new SKPaint() { Color=SKColors.LightCoral };

			e.Surface.Canvas.DrawRect( 0, 0, Width, Height, skPaint );
			Cache.Render( e.Surface.Canvas, StdUI, new PointF( 0, 0 ) );
		}

        protected override void OnMouseDown( MouseEventArgs e ) {
            base.OnMouseDown(e);

			Hide();
        }
    }

}
