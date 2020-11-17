using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Windows.Forms;
using System.Text.RegularExpressions;
using System.Drawing;
using System.IO;

using SkiaSharp;
using SkiaSharp.Views.Desktop;

using Play.Interfaces.Embedding;
using Play.Edit;
using Play.Integration;
using Play.Rectangles;
using Play.Parse.Impl;

namespace Mjolnir {
    public class SimpleCacheCaret : IPgCacheCaret  {
        protected int         _iOffset = 0;
        protected LayoutSingleLine _oCache;
        public    int         Advance { get; set; }

        public SimpleCacheCaret( LayoutSingleLine oCache ) {
            _oCache  = oCache;
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
            get { return Cache.Cache.Line; }
            set {
                if( value != Cache.Cache.Line )
                    throw new ApplicationException(); 
            }
        }

        public LayoutSingleLine Cache {
            get { return _oCache; }
            // We'll remove this later.
            set { _oCache = value ?? throw new ArgumentNullException(); }
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

        public void Paint( SKCanvas skCanvas, IPgStandardUI2 oStdUI ) {
            SKPointI pntUL = this.GetPoint(LOCUS.UPPERLEFT);
            using SKPaint skPaint = new SKPaint() { Color = SKColors.LightBlue };

            skCanvas.DrawRect( this.Left, this.Top, this.Width, this.Height, skPaint );

            Cache.Render( skCanvas, oStdUI, new PointF( pntUL.X, pntUL.Y ) );
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

        protected readonly List<LayoutSingleLine> _rgCacheList = new List<LayoutSingleLine>();
        protected ParentRect Layout2 { get; set; } = new LayoutStackHorizontal( 5 );

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
            _oViewEvents = _oSiteView.EventChain ?? throw new ArgumentException("Site.EventChain must support IPgViewSiteEvents");
            DocForms     = oDocForms ?? throw new ArgumentNullException( "Forms needes a text buffer" );
            StdUI        = (IPgStandardUI2)oSiteView.Host.Services;

            Array.Sort<Keys>(_rgHandledKeys);

            Caret = new SimpleCacheCaret( null );
        }

        protected override void Dispose( bool disposing ) {
            DocForms.CaretRemove( Caret ); // Unplug it so we won't get any callbacks when we're dead.
            DocForms.BufferEvent -= Document_BufferEvent;

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

            DocForms.BufferEvent += Document_BufferEvent;
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
        protected void Document_BufferEvent( BUFFEREVENTS eEvent ) {
            switch( eEvent ) {
                case BUFFEREVENTS.SINGLELINE:
                case BUFFEREVENTS.MULTILINE:
                    foreach( LayoutSingleLine oCache in _rgCacheList ) {
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

                Caret.Cache.Selection.Length = 0;
                Caret.Cache.OnChangeFormatting();

                // If total miss, build a new screen based on the location of the caret.
                FTCacheLine oElem = Caret.Cache.Cache;

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

            if( Caret.Cache != null ) {
                SKPointI pntCaretWorldLoc  = Caret.Cache.CaretWorldPosition( Caret ); 
                SKPointI pntCaretScreenLoc = new SKPointI( pntCaretWorldLoc.X + Caret.Cache.Left, 
                                                           pntCaretWorldLoc.Y + Caret.Cache.Top );
                if( Caret.Cache.IsInside( pntCaretWorldLoc.X, pntCaretWorldLoc.Y ) ) {
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
                if( Caret.Cache == null )
                    return false;

                return( Caret.Cache.Selection.Length > 0 );
            }
        }

        public void OnKey_Delete( bool fBackSpace ) {
            try {
                using Editor.Manipulator oBulk = DocForms.CreateManipulator();
                if( IsSelection ) {
                    oBulk.LineTextDelete( Caret.At, Caret.Cache.Selection );
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
                            oBulk.LineTextDelete( Caret.At, Caret.Cache.Selection );
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

            foreach( LayoutSingleLine oCache in _rgCacheList ) {
                skCanvas.Save();
                skCanvas.ClipRect(new SKRect(oCache.Left, oCache.Top, oCache.Right, oCache.Bottom), SKClipOperation.Intersect);
                oCache.Paint(e.Surface.Canvas, StdUI);
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

            // Set the caret for sure if hit. If not just leave it where ever it was.
            foreach( LayoutSingleLine oCache in _rgCacheList ) {
                if( oCache.IsInside( e.X, e.Y ) ) {
                    Caret.Cache = oCache;

                    oCache.SelectHead( Caret, e.Location, ModifierKeys == Keys.Shift );
                }
            }

            CaretIconRefresh();
            Invalidate();
        }

        protected override void OnMouseMove(MouseEventArgs e) {
            base.OnMouseMove(e);

            foreach( LayoutSingleLine oCache in _rgCacheList ) {
                if( oCache.IsInside( e.X, e.Y ) ) {
                    Cursor = Cursors.IBeam;
                    break;
                } else {
                    Cursor = Cursors.Arrow;
                }
            }

            if ((e.Button & MouseButtons.Left) == MouseButtons.Left && e.Clicks == 0 ) {
                if( Caret.Cache != null ) {
                    Caret.Cache.SelectNext( Caret, e.Location );

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
                    oBulk.LineTextDelete( Caret.At, Caret.Cache.Selection );
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
					oSelection = Caret.Cache.Selection;
				} else {
					oSelection = new ColorRange( 0, Caret.Cache.Cache.Line.ElementCount, 0 );
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

            if( Caret.Cache == null )
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
                            oBulk.LineTextDelete( Caret.At, Caret.Cache.Selection );
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

    /// <remarks>
    /// TODO: 1/6/2017 : We are an interesting hybrid tool on a document since we require a sister view 
	/// to show our results. I should really create a "find" document & controller to centralize all this.
    /// </remarks>
    public partial class FindWindow : 
		FormsWindow,
		IEnumerable<ILineRange>,
		IPgLoad,
		IPgParent
	{
        readonly MainWin _oWinMain; 

                 ViewChanged      _oViewChangedHandler;
        readonly ParseHandlerText _oParseEvents       = null;
        readonly Editor           _oDoc_SearchResults = null;

        IPgTextView             _oView; // This value changes when current view is switched.
        IEnumerator<ILineRange> _oEnumResults;
        TextPosition            _sEnumStart = new TextPosition( 0, 0 );

		public IPgParent Parentage => _oWinMain;
		public IPgParent Services  => Parentage.Services;

        public FindWindow( IPgViewSite oSiteView, MainWin oShell ) : 
            base( oSiteView, (Editor)oShell.Document.SearchSlot.Document )
        {
			_oWinMain = oShell ?? throw new ArgumentNullException( "Shell reference must not be null" );

            DocForms.LineAppend( string.Empty, fUndoable:false );

            _oDoc_SearchResults = _oWinMain.Document.ResultsSlot.Document as Editor;

            InitializeComponent();

			_oParseEvents = null;// _oDoc_SearchKey.ParseHandler;
            if( _oParseEvents != null ) {
                _oParseEvents.DisableParsing = oSearchType.SelectedItem.ToString() != "Regex";
            }
        }

		/// <summary>
		/// This is where we should be setting up the callbacks and extra.
		/// </summary>
		/// <returns></returns>
		public override bool InitNew() {
            if( !base.InitNew() )
                return false;

            EventHandler oGotFocusHandler  = new EventHandler( this.OnChildFocus );
            EventHandler oLostFocusHandler = new EventHandler( this.OnChildBlur  );
            // Bind all the focus events. It would be nice if the children just
            // automatically called some parent event. But I think that's fantasy.
            foreach (Control oChild in Controls) {
                oChild.GotFocus  += oGotFocusHandler;
                oChild.LostFocus += oLostFocusHandler;
            }

            button1.Click += new System.EventHandler(this.Next_Click);
            button2.Click += new System.EventHandler(this.SearchAll_Click);

            // Whenever the search results tool window is navigated, I need to know.
            // TODO: We've got the PlayHilights line which would be perfect for this.
			if( _oWinMain.DecorSoloSearch( "matches" ) is EditWindow2 oEditWin ) {
				oEditWin.LineChanged += new Navigation( OnMatchNavigation );
			}

			// Accessing the SearchType dropdown, so it needs to be initialized, see above.
            oSearchType.SelectedIndexChanged  += new EventHandler(OnSearchTypesIndexChanged);

            // BUG: Poster child for event problem. The OnViewChanged event @ startup has already occured.
            //      so we don't know who's focused at the moment.
            _oViewChangedHandler = new ViewChanged(OnViewChanged);
            _oWinMain.ViewChanged += _oViewChangedHandler;

            LayoutSingleLine oLayoutSearchKey = new LayoutSingleLine( new FTCacheWrap( DocForms[0] ), LayoutRect.CSS.Flex) { Span = 4 };
            Caret.Cache = oLayoutSearchKey;
            _rgCacheList.Add( oLayoutSearchKey );

            SmartTable oTable = new SmartTable( 5, LayoutRect.CSS.None );
            Layout2 = oTable;

            // If we the columns get too narrow, Labels might request double height,
            // Even if they don't use it!
			oTable.Add( new LayoutRect( LayoutRect.CSS.Pixels, 50, .25f ) );
			oTable.Add( new LayoutRect( LayoutRect.CSS.Pixels, 50, .25f ) );
			oTable.Add( new LayoutRect( LayoutRect.CSS.Pixels, 50, .25f ) );
			oTable.Add( new LayoutRect( LayoutRect.CSS.Pixels, 50, .25f ) );
			oTable.Add( new LayoutRect( LayoutRect.CSS.None ) );

            oTable.AddRow( new List<LayoutRect>() { oLayoutSearchKey } ); 

			oTable.AddRow( new List<LayoutRect>() {
			    new LayoutControl( oSearchType, LayoutRect.CSS.Flex ) { Span=1 },
			    new LayoutControl( oMatchCase,  LayoutRect.CSS.Flex ) { Span=1 }
            } );

			oTable.AddRow( new List<LayoutRect>() {
			    new LayoutRect( LayoutRect.CSS.Flex ),
			    new LayoutRect( LayoutRect.CSS.Flex ),
			    new LayoutControl( button1,  LayoutRect.CSS.Flex ),
			    new LayoutControl( button2,  LayoutRect.CSS.Flex ),
            } );

            Document_BufferEvent( BUFFEREVENTS.MULTILINE );
            OnSizeChanged( new EventArgs() );

			return true;
		}

        void Reset() {
            _oEnumResults = null;
        }

		public MainWin Host {
			get { return _oWinMain; }
		}

        public string ResultsTitle { get { return( "Results" ); } }

		void OnMatchNavigation( int iLine ) {
            try {
                ILineRange oSearchResult = _oDoc_SearchResults[iLine].Extra as ILineRange;

                if( oSearchResult.Line.At > -1 ) {
				    _oView.SelectionSet( oSearchResult.Line.At, oSearchResult.Offset, oSearchResult.Length );
				    _oView.ScrollToCaret();
                }
            } catch( NullReferenceException ) {
            }
        }
        // This is the case when we're using and instance of my editor for the find string.
        // If we are in "line" mode, we'll just jump to the line as soon as the user types.
        // BUG: if the string stays the same and the cursor get's moved we don't 
        // update the iterator's start position in the ALL case.
        void FindStringChanges( BUFFEREVENTS eEvent ) {
            Reset();

            if( oSearchType.SelectedItem.ToString() == "Line" ) {
                MoveCaretToLineAndShow();
            }
        }

        void Regex_SelectedIndexChanged(object sender, System.EventArgs e) {
            Reset();
        }

        void MoveCaretToLineAndShow() {
            try {
                int iRequestedLine = 0;
                if( int.TryParse( DocForms[0].ToString(), out iRequestedLine ) ) {
                    iRequestedLine -= 1;

					_oView.SelectionSet( iRequestedLine, 0, 0 );
					_oView.ScrollToCaret();
                }
            } catch( NullReferenceException ) {
            } catch( IndexOutOfRangeException ) {
            }
        }

        void OnKeyDown(object sender, KeyEventArgs e) {
            if (e.KeyCode == Keys.Enter) {
                Next_Click(sender, e);
            }
        }

        void OnSearchTypesIndexChanged(object sender, EventArgs e) {
            Reset();

            try {
                _oParseEvents.DisableParsing = oSearchType.SelectedItem.ToString() != "Regex";
            } catch( NullReferenceException ) {
            }
        }

		protected override void OnVisibleChanged(EventArgs e) {
			base.OnVisibleChanged(e);
		}

        /// <summary>
        /// Listening for the view changed events on the shell.
        /// </summary>
        /// <param name="oView">At present this value can be null!</param>
        private void OnViewChanged(IPgTextView oView) {
            if( oView == null ) {
                this.Enabled = false;
                return;
            }

            Reset();

            _oView = oView;

             this.Enabled = true;
        }

		//protected override void OnSizeChanged(EventArgs e) {
		//	Layout2.SetRect( 0, 0, Width, Height );
		//	Layout2.LayoutChildren();

  //          CaretIconRefresh();

  //          Invalidate();

		//	//base.OnSizeChanged(e);
		//}

        /// <summary>
        /// Look for a match in the section of the line given.
        /// </summary>
        private ILineRange TextMatch( 
            string strFind, 
            Line   oLine, 
            int    iOffsetStart ) 
        {
            try {
			    while( iOffsetStart < oLine.ElementCount ) {
				    int iMatch = 0;
				    for( int j=0; j<strFind.Length; ++j ) {
                        char cChar = oLine[iOffsetStart+j];

                        if( !oMatchCase.Checked )
                            cChar = char.ToLower( cChar );

					    if ( strFind[j] == cChar )
						    iMatch++;
					    else
						    break;
				    }
				    if( iMatch == strFind.Length ) {
                        return( new LineRange( oLine, iOffsetStart, iMatch, SelectionTypes.Empty ) );
                    }
				    iOffsetStart++;
			    }
            } catch( Exception oEx ) {
                Type[] rgErrors = { typeof( ArgumentOutOfRangeException ),
                                    typeof( NullReferenceException ) };
                if( rgErrors.IsUnhandled( oEx ) )
                    throw;

                _oWinMain.LogError( null, "search", "Tried to walk off the end of a buffer in the Find Window." );
            }

            return( null );
        }

        public IEnumerator<ILineRange> CreateTextEnum( IReadableBag<Line> oDoc, string strFind, int iLine ) {
            if( oDoc == null )
                yield break;
			if( string.IsNullOrEmpty( strFind ) )
				yield break;

            int iCount  = 0;
            int iOffset = 0;

            if( !oMatchCase.Checked )
                strFind = strFind.ToLower();

            while( true ) {
                if( iCount++ >= oDoc.ElementCount ) {
                    break;
                }
                do {
                    ILineRange oRange = TextMatch( strFind, oDoc[ iLine ], iOffset );
                    if( oRange == null )
                        break;
                    
                    iOffset = oRange.Offset + oRange.Length; // Do now, in case range is tampered with.
                    yield return( oRange );
                } while( iOffset <  oDoc[ iLine ].ElementCount ); 

                iOffset = 0;
                iLine   = ++iLine % oDoc.ElementCount;
            } // End while
        } // End method

        /// <summary>
        /// Sort of a misnomer. Since we simply return the same line everytime 'next' occures.
        /// But this object lets us use the same code for regex, and text searches. Just remember
        /// to set the caret position in the case of line number in the calling code.
        /// </summary>
        /// <returns></returns>
        public IEnumerator<ILineRange> CreateLineNumberEnum( IReadableBag<Line> oDoc, string strFind )
        {
            if( oDoc == null )
                yield break;

            int  iRequestedLine;

            try {
                iRequestedLine = int.Parse( strFind ) - 1;
            } catch( Exception oEx ) {
                Type[] rgErrors = { typeof( ArgumentNullException ), 
					                typeof( FormatException ), 
									typeof( OverflowException ) };
                if( rgErrors.IsUnhandled( oEx ) )
                    throw;

                throw new InvalidOperationException( "Unexpected return in FindWindow Line Parse" );
            }

            yield return( new LineRange( oDoc[iRequestedLine], 0, 0, SelectionTypes.Empty ) );
        }

        public IEnumerator<ILineRange> CreateRegexEnum( IReadableBag<Line> oDoc, string strFind, int iLine )
        {
            if( oDoc == null )
                yield break;

            RegexOptions eOpts = RegexOptions.None;

            if( !oMatchCase.Checked )
                eOpts |= RegexOptions.IgnoreCase;

            Regex oReg = null;
            try {
                oReg = new Regex( strFind, eOpts );
            } catch( ArgumentException ) {
                _oWinMain.LogError( null, "search", "Problem with regex search string" );
                yield break;
            }

            int  iCount = 0;

            while( true ) {
                if( iCount++ >= oDoc.ElementCount )
                    yield break;

                int    iStart  = 0;
                Line   oLine   = oDoc[ iLine ];
                string strLine = oLine.ToString(); // BUG: Pretty evil actually. Need to go all string based lines. 
                Match  oMatch  = null;

                do {
                    try {
                        oMatch = oReg.Match( strLine, iStart ); // Can throw exceptions.
                    } catch( ArgumentOutOfRangeException ) {
                        _oWinMain.LogError( null, "search", "Could not Regex search current line of text." );
                    }

				    if( oMatch != null && oMatch.Success ) {
                        if( oMatch.Groups.Count > 1 ) {
                            // This is effectively multi-selection on a line
                            for( int iGroup = 1; iGroup < oMatch.Groups.Count; ++iGroup ) {
                                Group oGroup = oMatch.Groups[iGroup];

                                yield return( new LineRange( oLine, oGroup.Index, oGroup.Length, SelectionTypes.Empty ) );
                            }
                        } else {
                            yield return( new LineRange( oLine, oMatch.Index, oMatch.Length, SelectionTypes.Empty ) );
                        }
                        iStart = oMatch.Index + oMatch.Length;
                    } // if match
                } while( oMatch != null && oMatch.Success && iStart < oLine.ElementCount );

                iLine = ++iLine % oDoc.ElementCount;
            } // End while
        }

        private IEnumerator<ILineRange> EnumSearchResults() {
			try {
				switch( oSearchType.SelectedItem.ToString() ) {
					case "Text":
						return CreateTextEnum( _oView.DocumentText as IReadableBag<Line>, DocForms[0].ToString(), _oView.Caret.Line );
					case "Regex":
						return CreateRegexEnum( _oView.DocumentText as IReadableBag<Line>, DocForms[0].ToString(), _oView.Caret.Line );
					case "Line":
						return CreateLineNumberEnum( _oView.DocumentText as IReadableBag<Line>, DocForms[0].ToString() );
				}
			} catch( NullReferenceException ) {
                _oWinMain.LogError( null, "search", "Problem generating Search enumerators." );
			}
            return( null );
        }

        public IEnumerator<ILineRange> GetEnumerator() {
            return( EnumSearchResults() );
        }

        IEnumerator IEnumerable.GetEnumerator() {
            return( EnumSearchResults() );
        }

        /// <summary>
        /// Find the next instance of the search word. This function DOES NOT set the
        /// focus to the searched view. There is a goto hyperlink for that in the dialog.
        /// </summary>
        private void Next_Click( object sender, EventArgs e ) {
            Submit();
        }

        public override void Submit() {
            base.Submit();

            _oDoc_SearchResults.Clear();

            if( _oEnumResults == null ) {
				// Save our starting point.
                _sEnumStart   = _oView.Caret;
                _oEnumResults = EnumSearchResults();
            }
            if( _oEnumResults.MoveNext() ) {
                ILineRange oRange = _oEnumResults.Current;

                _oView.SelectionClear(); 
				_oView.SelectionSet( oRange.Line.At, oRange.Offset, oRange.Length );
				_oView.ScrollToCaret();
            } else {
                _oView.SelectionClear(); 
				_oView.SelectionSet( _sEnumStart.Line, _sEnumStart.Offset, 0 );
				_oView.ScrollToCaret();
                _oEnumResults = null;
            }
        }

        private void SearchAll_Click(object sender, EventArgs e) {
            try {
                _oDoc_SearchResults.Clear();

                using( Editor.Manipulator oSearchManip = _oDoc_SearchResults.CreateManipulator() ) {
                    StringBuilder oMatchBuilder = new StringBuilder();

                    // oSearchType.SelectedItem.ToString() ;
                    string strFormat = "Location"; // Location or Table

                    foreach( ILineRange oRange in this ) {
                        int    iStart    = oRange.Offset > 10 ? oRange.Offset - 10 : 0;
                        int    iDiff     = oRange.Offset - iStart;
                        int    iPreamble = 0;
                        
                        if( strFormat == "Location" ) { // BUG: should be localized ^_^;
                            oMatchBuilder.Append( "(" );
                            oMatchBuilder.Append( string.Format( "{0,3}", oRange.At + 1 ) );
                            oMatchBuilder.Append( ") " );

                            iPreamble = oMatchBuilder.Length;
                            oMatchBuilder.Append( oRange.Line.SubString( iStart, 50 ) );
                        } else {
                            oMatchBuilder.Append( oRange.Line.SubString( oRange.Offset, oRange.Length ) );
                        }

                        bool fMulti = false;
                        if( fMulti ) {
                            // For regex groups, which we don't support at the moment. 
                            oMatchBuilder.Append( "\t" );
                        } else {
                            Line oNew = oSearchManip.LineAppend( oMatchBuilder.ToString() ); 
                            oMatchBuilder.Length = 0;
                            if( oNew != null ) {
							    //_oDoc_SearchResults.WordBreak( oNew, oNew.Formatting );
                                if( strFormat == "Location" ) {
                                    oNew.Formatting.Add( new ColorRange( iPreamble + iDiff, oRange.Length, _oWinMain.GetColorIndex( "red" ) ) );
                                }
                                oNew.Extra = oRange;
                            }
                        }
                    }; // end foreach
                } // end using

                if( _oDoc_SearchResults.ElementCount > 0 ) {
                    _oWinMain.DecorOpen( "matches", true );
                }
            } catch( Exception oEx ) {
                Type[] rgErrors = { typeof( NullReferenceException ),
                                    typeof( ArgumentOutOfRangeException ) };
                if( rgErrors.IsUnhandled( oEx ) )
                    throw;
            }
        }

        /// <summary>
        /// We are set up so that any child when getting the focus will call this method.
        /// </summary>
        /// <remarks>For some reason on boot the find window has a child
        /// can have the focus. Probably because it was the last object
        /// created. Some sort of side effect of creation.</remarks>
        private void OnChildFocus(object sender, EventArgs e)
        {
            OnGotFocus( e );

			SmartHerderBase oShepard = Host.Shepardfind( "find" );

			if( oShepard != null ) {
				oShepard.OnFocused();
			}

			_oWinMain.Invalidate();
        }

        private void OnChildBlur( object sender, EventArgs e )
        {
            OnLostFocus( e );

			SmartHerderBase oShepard = Host.Shepardfind( "find" );

			if( oShepard != null ) {
				oShepard.OnBlurred();
			}
			_oWinMain.Invalidate();
        }

        private void Goto_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e) {
            if( _oView != null ) {
                _oWinMain.CurrentView = _oView;
                _oView.ScrollToCaret();
            }
        }
        private void Results_LinkClicked( object sender, LinkLabelLinkClickedEventArgs e) {
            _oWinMain.DecorOpen( "matches", true );
        }

        /// <summary>
        /// This is our site for the search string.
        /// </summary>
        public class DocSlot : 
            IPgViewSite,
            IPgViewNotify,
            IEnumerable<ColorMap>
        {
            readonly FindWindow _oFindWindow; // Our owner.

            public DocSlot( FindWindow oFindWin ) {
                _oFindWindow = oFindWin;
            }

			public EditWin Guest {
				set;
				get;
			}

			public bool InitNew() {
				return Guest.InitNew();
			}

            public void Notify( ShellNotify eEvent ) {
            }

            public virtual IPgParent     Host       => _oFindWindow;
            public virtual FILESTATS     FileStatus => FILESTATS.UNKNOWN;
			public         IPgViewNotify EventChain => this; 

			public virtual void LogError(string strMessage, string strDetails, bool fShow=true) {
                _oFindWindow.Host.LogError( this, strMessage, strDetails, fShow );
            }

            public virtual void OnDocDirty() {}

			/// <summary>
			/// Normally we would use a DecorSlot with the FindWindow to implement this
			/// but this window is really old and I haven't plumed that out yet so just
			/// put this little hack for now.
			/// </summary>
			/// <remarks>We don't worry about OnBlur since the center view when it
			/// gets focus it directly blurs all the herders.
			/// BUG: But what about move from decor to decor. Hmmm....</remarks>
            public void NotifyFocused(bool fSelect) { 
				SmartHerderBase oShepard = _oFindWindow.Host.Shepardfind( "find" );

				if( oShepard != null ) {
					if( fSelect )
						oShepard.OnFocused();
				}
			}

            public bool IsCommandKey(CommandKey ckCommand, KeyBoardEnum kbModifier) {
                bool fShift = Convert.ToBoolean(kbModifier & KeyBoardEnum.Shift);
                return ckCommand switch
                {
                    // BUG
                    CommandKey.Tab => (true),//_oFindWindow.SelectNextControl( _oFindWindow.ActiveControl, !fShift, true, false, true );
                    _ => (false),
                };
            }

            public bool IsCommandPress( char cChar ) {
                switch( cChar ) {
                    case '\t':
                        return( true );
                    case '\r':
                        _oFindWindow.Next_Click( null, null );
                        return( true );
                    case '\u001B': // ESC
                        _oFindWindow._oWinMain.SetFocusAtCenter();
                        return( true );
                }
                return( false );
            }

            public IEnumerator<ColorMap> GetEnumerator() {
                return _oFindWindow._oWinMain.SharedGrammarColors;
            }

            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
		}
    }
}
