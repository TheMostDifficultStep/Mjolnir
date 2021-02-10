using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Windows.Forms;
using System.Drawing;
using System.IO;

using SkiaSharp;
using SkiaSharp.Views.Desktop;

using Play.Interfaces.Embedding;
using Play.Edit;
using Play.Integration;
using Play.Rectangles;
using Play.Parse.Impl;

namespace Play.Forms {
    public class SimpleCacheCaret : IPgCacheCaret  {
        protected int              _iOffset = 0;
        protected LayoutSingleLine _oLayout;
        public    int              Advance { get; set; }

        public SimpleCacheCaret( LayoutSingleLine oLayout ) {
            _oLayout  = oLayout;
            _iOffset = 0;
        }

		public int ColorIndex {
			get { return( 0 ); }
		}

        public override string ToString() {
            return( "(" + _iOffset.ToString() + "...) " + Line.SubString( 0, 50 ) );
        }

        public int At {
            get { return Line.At; }
        }

        public int Offset {
            get { return _iOffset; }
            
            set {
                if( value > Line.ElementCount )
                    value = Line.ElementCount;
                if( value <= 0 )
                    value = 0;

                _iOffset = value;
            }
        }
        
        public int Length {
            get { return 0; }
            set { throw new ArgumentOutOfRangeException("Caret length is always zero" ); }
        }
        
        public Line Line {
            get { return Layout.Cache.Line; }
            set {
                if( value != Layout.Cache.Line )
                    throw new ApplicationException();
            }
        }

        public LayoutSingleLine Layout {
            get { return _oLayout; }
            // If the line is read only on the FTCacheLine then it kind of makes sense to
            // only allow updating the Cache element and not the Line.
            set { _oLayout = value ?? throw new ArgumentNullException(); }
        }
    }

    public class SimpleRange : ILineSelection {
        int _iStart  = 0;

        public int Start { 
           get { return _iStart; }
           set { _iStart = value; Offset = value; Length = 0; }
        }

        public int Offset { get; set; } = 0;
        public int Length { get; set; } = 0;

        public SimpleRange( int iStart ) {
            Start = iStart;
        }

        public override string ToString() {
            return "S:" + Start.ToString() + ", O:" + Offset.ToString() + ", L:" + Length.ToString();
        }

        /// <summary>
        /// Use this when the shift key is down to move one edget of
        /// the selection growing from our original start point.
        /// </summary>
        public int Grow {
            set {
                int iLength = value - Start;

                if (iLength > 0) {
                    Offset = Start;
                    Length = iLength;
                } else {
                    Offset = value;
                    Length = -iLength;
                }
            }
        }

        public bool IsEOLSelected { 
            get => false; 
            set => throw new NotImplementedException(); 
        }

        public SelectionTypes SelectionType => SelectionTypes.Start;

        public Line Line {
            get => throw new NotImplementedException(); 
            set => throw new NotImplementedException(); 
        }

        public int At         => throw new NotImplementedException();
        public int ColorIndex => -1;

        public bool IsHit( Line oLine ) {
            return true;
        }
    }
    /// <summary>
    /// This is slightly a misnomer in that a single Line can be wrapped. What
    /// we mean is a solitary Line. This object will be part of my new Forms object.
    /// </summary>
    public class LayoutSingleLine : LayoutRect {
        public FTCacheLine Cache { get; }
        public SimpleRange Selection { get; } = new SimpleRange(0);

        // Normally selection lives on the view, but I'll put it here for forms for now.
        protected ILineSelection[] _rgSelections = new ILineSelection[1];

        public LayoutSingleLine( FTCacheLine oCache, CSS eCSS ) : base( eCSS ) {
            Cache = oCache ?? throw new ArgumentNullException();
            _rgSelections[0] = Selection;
        }

        public void Paint( SKCanvas skCanvas, IPgStandardUI2 oStdUI, bool fFocused ) {
            SKPointI pntUL = this.GetPoint(LOCUS.UPPERLEFT);
            using SKPaint skPaint = new SKPaint() { Color = SKColors.LightBlue };

            skCanvas.DrawRect( this.Left, this.Top, this.Width, this.Height, skPaint );

            Cache.Render( skCanvas, oStdUI, new PointF( pntUL.X, pntUL.Y ), fFocused );
        }

        public override uint TrackDesired(TRACK eParentAxis, int uiRail) {
            switch( eParentAxis ) {
                case TRACK.HORIZ: return (uint)Cache.UnwrappedWidth;
                case TRACK.VERT : {
                    Cache.OnChangeSize( uiRail );
                    return (uint)Cache.Height;
                }
                default: throw new ArgumentOutOfRangeException();
            }
        }

        /// <summary>
        /// Need to sort out the OnSize vs Raise_OnSize conundrum.
        /// </summary>
        public override void Raise_OnSize(SmartRect p_rctOld) {
            base.Raise_OnSize(p_rctOld);
            Cache.OnChangeSize( Width );
        }

        public void OnChangeFormatting() {
            Cache.OnChangeFormatting( _rgSelections );
        }

        public SKPointI CaretWorldPosition( ILineRange oCaret ) {
            // Check that the line matches.
            Point oWorldLoc = Cache.GlyphOffsetToPoint( oCaret.Offset );
            return new SKPointI( oWorldLoc.X, oWorldLoc.Y );
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

    public class FormsEditor : Editor {
        public FormsEditor( IPgBaseSite oSite ) : base( oSite ) {
        }

        public override bool Load( TextReader oReader ) {
            _iCumulativeCount = 0;
			HighLight         = null;
            
            try {
                int    iLine   = -1;
                string strLine = oReader.ReadLine();
                while( strLine != null ) {
                    ++iLine;
                    Line oLine;
                    if( iLine < _rgLines.ElementCount ) {
                        oLine = _rgLines[iLine];
                        oLine.TryDelete( 0, int.MaxValue, out string strRemoved );
                        oLine.TryAppend( strLine );
                        Raise_AfterLineUpdate( oLine, 0, strRemoved.Length, oLine.ElementCount );
                    } else { 
                        oLine = CreateLine( iLine, strLine );
                        _rgLines.Insert( _rgLines.ElementCount, oLine );
                        Raise_AfterInsertLine( oLine );
                    }
                        
                    _iCumulativeCount = oLine.Summate( iLine, _iCumulativeCount );
                    strLine = oReader.ReadLine();
                }
                while( _rgLines.ElementCount > iLine + 1 ) {
                    int iDelete = _rgLines.ElementCount - 1;
                    Raise_BeforeLineDelete( _rgLines[iDelete] );
                    _rgLines.RemoveAt( iDelete );
                }
            } catch( Exception oE ) {
                Type[] rgErrors = { typeof( IOException ),
                                    typeof( NullReferenceException ),
                                    typeof( ArgumentNullException ),
                                    typeof( ArgumentException ) };
                if( rgErrors.IsUnhandled( oE ) )
                    throw;

                _oSiteBase.LogError( "editor", "Unable to read stream (file) contents." );

                return( false );
            } finally {
                Raise_MultiFinished();
                Raise_BufferEvent( BUFFEREVENTS.LOADED );  
            }

            return (true);
        }
    }

    public class FormsWindow : SKControl {
		protected readonly IPgViewSite   _oSiteView;
        protected readonly IPgViewNotify _oViewEvents; // Our site from the window manager (view interface).

        protected List<LayoutSingleLine> CacheList { get; }      = new List<LayoutSingleLine>();
        protected ParentRect             Layout2   { get; set; } = new LayoutStackHorizontal( 5 );

        protected Editor           DocForms { get; }
        protected SimpleCacheCaret Caret    { get; }
        public    uint             StdText  { get; set; }
        protected IPgStandardUI2   StdUI    { get; }

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
            if( disposing ) {
                DocForms.BufferEvent -= OnDocumentEvent;
                DocForms.CaretRemove( Caret );
            }

            base.Dispose(disposing);
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

            SKSize sResolution = new SKSize(96, 96);
            using (Graphics oGraphics = this.CreateGraphics()) {
                sResolution.Width  = oGraphics.DpiX;
                sResolution.Height = oGraphics.DpiY;
            }

            StdText = StdUI.FontCache(StdUI.FaceCache(@"C:\windows\fonts\consola.ttf"), 12, sResolution);

            return true;
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
        protected void OnDocumentEvent( BUFFEREVENTS eEvent ) {
            switch( eEvent ) {
                case BUFFEREVENTS.SINGLELINE:
                case BUFFEREVENTS.MULTILINE:
                    foreach( LayoutSingleLine oCache in CacheList ) {
                        oCache.Cache.Update( StdUI.FontRendererAt( StdText ) );
                        oCache.Cache.OnChangeSize( oCache.Width );
                    }
                    Invalidate();
                    break;
            }
        }

        public void CaretMove( Axis eAxis, int iDir ) {
            try {
                int iOffset  = Caret.Offset;
                int iAdvance = Caret.Advance;

                Caret.Layout.Selection.Length = 0;
                Caret.Layout.OnChangeFormatting();

                // If total miss, build a new screen based on the location of the caret.
                FTCacheLine oElem = Caret.Layout.Cache;

                if( iDir != 0 ) {
                    // First, see if we can navigate within the line we are currently at.
                    if( !oElem.Navigate( eAxis, iDir, ref iAdvance, ref iOffset ) ) {
                        iDir = iDir < 0 ? -1 : 1; // Only allow move one line up or down.

                        FTCacheLine oNext = null; // PreCache( oElem.At + iDir );
                        if( oNext != null ) {
                            iOffset = oNext.OffsetBound( eAxis, iDir * -1, iAdvance );
                            oElem   = oNext;
                        }
                    }
                    // If going up or down ends up null, we won't be moving the caret.
                    //Caret.Cache   = oElem;
                    Caret.Offset  = iOffset;
                    Caret.Advance = iAdvance;
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

        protected void CaretIconRefresh() {
            if( Focused != true )
                return;

            if( Caret.Layout != null ) {
                SKPointI pntCaretWorldLoc  = Caret.Layout.CaretWorldPosition( Caret ); 
                SKPointI pntCaretScreenLoc = new SKPointI( pntCaretWorldLoc.X + Caret.Layout.Left, 
                                                           pntCaretWorldLoc.Y + Caret.Layout.Top );
                if( Caret.Layout.IsInside( pntCaretWorldLoc.X, pntCaretWorldLoc.Y ) ) {
                    User32.SetCaretPos(pntCaretScreenLoc.X, pntCaretScreenLoc.Y);
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
                if( _oViewEvents.IsCommandPress( e.KeyChar ) )
                    return;

                if( !char.IsControl( e.KeyChar ) || e.KeyChar == 0x0009 ) { 
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

            foreach( LayoutSingleLine oCache in CacheList ) {
                skCanvas.Save();
                skCanvas.ClipRect(new SKRect(oCache.Left, oCache.Top, oCache.Right, oCache.Bottom), SKClipOperation.Intersect);
                oCache.Paint(e.Surface.Canvas, StdUI, this.Focused );
                skCanvas.Restore();
            }
            // Layout2.Paint( e.Surface.Canvas ); Use this to see what the columns look like.
        }

        protected override void OnSizeChanged( EventArgs e ) {
			Layout2.SetRect( 0, 0, Width, Height );
			Layout2.LayoutChildren();

            CaretIconRefresh();

            Invalidate();
        }

        protected override void OnGotFocus(EventArgs e) {
            base.OnGotFocus( e );

            _oSiteView.EventChain.NotifyFocused(true);

            // Not perfect but getting better...
            int iLineHeight = (int)(StdUI.FontRendererAt( StdText ).FontHeight * 1.5 );

            User32.CreateCaret( this.Handle, IntPtr.Zero, 2, iLineHeight );
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

                case Keys.Enter:
                    Submit();
                    break;
                case Keys.Tab:
                    //_oViewEvents.IsCommandKey( CommandKey.Tab, (KeyBoardEnum)e.Modifiers );
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
                    }
                }
            }

            CaretIconRefresh();
            Invalidate();
        }

        protected override void OnMouseMove(MouseEventArgs e) {
            base.OnMouseMove(e);

            foreach( LayoutSingleLine oCache in CacheList ) {
                if( oCache.IsInside( e.X, e.Y ) ) {
                    Cursor = Cursors.IBeam;
                    break;
                } else {
                    Cursor = Cursors.Arrow;
                }
            }

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

}
