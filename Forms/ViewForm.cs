﻿using System;
using System.Collections.Generic;
using System.Windows.Forms;
using System.Drawing;
using System.Diagnostics;
using System.IO;
using System.Text;

using SkiaSharp;
using SkiaSharp.Views.Desktop;

using Play.Interfaces.Embedding;
using Play.Rectangles;
using Play.Parse;
using Play.Edit;

namespace Play.Forms {
    /// <summary>
    /// This could actually go in our general layout library. But I'm sort
    /// of testing it out for now.
    /// </summary>
    public class LayoutCenter : LayoutControl {
        int     _iPreferedRail = -1;

		public LayoutCenter( Control oView, LayoutRect.CSS eUnits, uint uiTrack ) : base( oView, eUnits, uiTrack ) {
		}
		protected override void OnSizeEvent(SmartRect o) {
            if( _iPreferedRail > 0 && this.Width > _iPreferedRail ) {
                int iLeft = this.Left  + ( Width - _iPreferedRail ) / 2;

                Rectangle rcRect = new Rectangle( iLeft, this.Top, _iPreferedRail, this.Height );

                _oControl.Bounds = rcRect;
                return;
            }

			_oControl.Bounds = this.Rect;
		}

		public override uint TrackDesired(TRACK eParentAxis, int uiRail) {
            if( Units == CSS.Pixels ) // Go with the set track size.
                return Track;

			Size szProposed = eParentAxis == TRACK.HORIZ ? new Size( Width, uiRail ) : new Size( uiRail, Height );
			Size szPrefered = _oControl.GetPreferredSize( szProposed );
			int  iTrack     = eParentAxis == TRACK.HORIZ ? szPrefered.Width : szPrefered.Height;
            
             _iPreferedRail = eParentAxis == TRACK.VERT ? szPrefered.Width : szPrefered.Height;

			return (uint)iTrack;
		}
    }

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
        public uint        FontID  { get; set; } = uint.MaxValue;
        public Line        Line    => Cache.Line; // Someday line won't be available from cache...

        // Normally selection lives on the view, but I'll put it here for forms for now.
        protected ILineSelection[] _rgSelections = new ILineSelection[1];

        public LayoutSingleLine( FTCacheLine oCache, CSS eCSS ) : base( eCSS ) {
            Cache = oCache ?? throw new ArgumentNullException();

            _rgSelections[0] = Selection;
        }

        public override string ToString() {
            try {
                StringBuilder sbBuild = new StringBuilder();

                sbBuild.Append( "F" );
                sbBuild.Append( Line.Formatting.Count.ToString() );
                sbBuild.Append( "@" );
                sbBuild.Append( Line.At.ToString() );
                sbBuild.Append( ":" );
                sbBuild.Append( Line.ToString(), 0, Line.ElementCount > 50 ? 50 : Line.ElementCount );

                return sbBuild.ToString();
            } catch( NullReferenceException ) {
                return "Layout Single Line : TS Error";
            }
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

        /// <seealso cref="FTCacheLine.Measure"/>
        /// <seealso cref="FTCacheLine.UnwrappedWidth"/>
        public override uint TrackDesired(TRACK eParentAxis, int uiRail) {
            // Looks like unwrapped width of each character is not being summated for the cache
            // element on Update(); In any event, it might just be best to recalc here anyway.
            Cache.OnChangeSize( uiRail );

            switch( eParentAxis ) {
                case TRACK.HORIZ: return       Cache.UnwrappedWidth;
                case TRACK.VERT : return (uint)Cache.Height;
                default: throw new ArgumentOutOfRangeException();
            }
        }

        protected override void OnSize() {
            base.OnSize();
            Cache.OnChangeSize( Width );
        }

        public void OnChangeFormatting() {
            Cache.Colorize( _rgSelections );
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
                SKPointI pntLocal = new SKPointI( pntClient.X - Left,
                                                  pntClient.Y - Top);
                int iEdge = Cache.GlyphPointToOffset( pntLocal );

                return( iEdge >= Selection.Offset &&
                        iEdge <  Selection.Offset + Selection.Length );
            }
            return false;
        }

        public void SelectHead( IPgCacheCaret oCaret, Point pntClient, bool fGrow ) {
            SKPointI pntLocal = new SKPointI( pntClient.X - Left,
                                              pntClient.Y - Top);
            int iEdge = Cache.GlyphPointToOffset( pntLocal );

            oCaret.Offset  = iEdge;
            oCaret.Advance = pntLocal.X;

            if ( fGrow ) {
                Selection.Grow  = iEdge;
            } else {
                Selection.Start = iEdge;
            }
            Cache.Colorize( _rgSelections );
        }

        public void SelectNext( IPgCacheCaret oCaret, Point pntClient ) {
            SKPointI pntLocal = new SKPointI( pntClient.X - Left,
                                              pntClient.Y - Top );
            int iEdge = Cache.GlyphPointToOffset( pntLocal );

            Selection.Grow = iEdge;
            oCaret.Offset  = iEdge;
            oCaret.Advance = Cache.GlyphOffsetToPoint( oCaret.Offset ).X;

            Cache.Colorize( _rgSelections );
        }

        public void SelectAll( IPgCacheCaret oCaret ) {
            Point pntWorld = Cache.GlyphOffsetToPoint( oCaret.Line.ElementCount );

            oCaret.Offset  = 0;

            Selection.Start  = 0;
            Selection.Length = oCaret.Line.ElementCount;

            Cache.Colorize( _rgSelections );
        }

        public void SelectClear() {
            Selection.Length = 0;
            Cache.Colorize( _rgSelections );
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
    /// And this window can layout any LayoutRect object. Uses the old fashioned
    /// line editor.
    /// </summary>
    /// <seealso cref="LayoutRect"/>
    /// <seealso cref="BaseEditor"/>
    public class FormsWindow : 
        SKControl,
        IPgCacheCaret
    {
        protected bool _fDisposed { get; private set; }

        protected int  _iCaretAtLayout = -1;

		protected readonly IPgViewSite   _oSiteView;
        protected readonly IPgViewNotify _oViewEvents; // Our site from the window manager (view interface).

        // Just blast the old forms Layout member with my prefered implementation.
        protected new ParentRect         Layout    { get; set; } = new LayoutStackHorizontal() { Spacing = 5 };
        protected List<LayoutSingleLine> CacheList { get; }      = new List<LayoutSingleLine>();

        protected IPgFormInterface       DocForms2 { get; }     
        
        //protected SimpleCacheCaret Caret    { get; }
        public    uint             StdFont  { get; }
        public    ushort           StdFace  { get; }
        protected IPgStandardUI2   StdUI    { get; }

        public    virtual uint     StdFontSize { get; set; } = 12;

        public HyperlinkCollection Links { get; } = new ();
        public int[]               TabOrder { get; set; } = null;

        readonly static Keys[] _rgHandledKeys = { Keys.PageDown, Keys.PageUp, Keys.Down,
                                                  Keys.Up, Keys.Right, Keys.Left, Keys.Back,
                                                  Keys.Delete, Keys.Enter, Keys.Tab, Keys.Tab | Keys.Shift,
                                                  Keys.Control | Keys.A, Keys.Control | Keys.F };

        public FormsWindow( IPgViewSite oSiteView, IReadableBag<Line> oDocForms ) {
			_oSiteView   = oSiteView ?? throw new ArgumentNullException( "Find window needs a site!!" );
            _oViewEvents = oSiteView.EventChain ?? throw new ArgumentException("Site.EventChain must support IPgViewSiteEvents");
            DocForms2    = (IPgFormInterface)oDocForms;
            StdUI        = (IPgStandardUI2)oSiteView.Host.Services;

            StdFace = StdUI.FaceCache(@"C:\windows\fonts\consola.ttf");

            IPgMainWindow.PgDisplayInfo oInfo = new IPgMainWindow.PgDisplayInfo();
            if( _oSiteView.Host.TopWindow is IPgMainWindow oMainWin ) {
                oInfo = oMainWin.MainDisplayInfo;
            }

            StdFont = StdUI.FontCache( StdFace, StdFontSize, oInfo.pntDpi );

            Array.Sort<Keys>(_rgHandledKeys);
        }
        
        /// <summary>
        /// Unplug events so we won't get any callbacks when we're dead.
        /// </summary>
        protected override void Dispose( bool disposing ) {
            if( disposing && !_fDisposed ) {
                DocForms2.BufferEvent -= OnDocumentEvent;
                DocForms2.CaretRemove( this );

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

            DocForms2.BufferEvent += OnDocumentEvent;
            DocForms2.CaretAdd( this ); // Document moves our caret and keeps it in sync.

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
            DocForms2.Raise_Submit();
        }

        // Let Forms know what keys we want sent our way.
        protected override bool IsInputKey(Keys keyData) {
            int iIndex = Array.BinarySearch<Keys>(_rgHandledKeys, keyData);

            if (iIndex >= 0)
                return (true);

            return base.IsInputKey( keyData );
        }

        /// <summary>
        /// Pretty clear the TRACK direction should come from the LayoutControl
        /// object we would be embedded in. I'm going to do this now just to see
        /// if I can get it off the ground. And then we'll add a direction to the
        /// form and make a layout control subclass that takes the form.
        /// </summary>
		public override Size GetPreferredSize( Size sProposed ) {
            uint uiTrack = Layout.TrackDesired( TRACK.VERT, sProposed.Width );

            Size szSize = new Size( sProposed.Width, (int)uiTrack );

            return szSize;
        }

        protected IPgFontRender GetFont( uint uiFontID ) {
            if( uiFontID == uint.MaxValue )
                return StdUI.FontRendererAt( StdFont );

            return StdUI.FontRendererAt( uiFontID );
        }

        /// <summary>
        /// Just update the entire cache. TODO: Just update the items that
        /// have changed.
        /// </summary>
        protected virtual void OnDocumentEvent( BUFFEREVENTS eEvent ) {
            switch( eEvent ) {
                case BUFFEREVENTS.FORMATTED:
                case BUFFEREVENTS.SINGLELINE:
                case BUFFEREVENTS.MULTILINE:
                    {
                        // IPgFontRender oStdRender = StdUI.FontRendererAt( StdFont );
                        foreach( LayoutSingleLine oLayout in CacheList ) {
                            oLayout.Cache.Measure( GetFont( oLayout.FontID ) );
                            oLayout.OnChangeFormatting();
                            oLayout.Cache.OnChangeSize( oLayout.Width );
                        }
                        // No need to parse anything for word wrapping since I
                        // have implemented new word wrap code in the editor code.
                        OnSizeChanged( new EventArgs() ); 
                    }
                    break;
            }
        }

        /// <summary>
        /// Walk around in our new tab order array based on whether we are tabbing forward
        /// or backward. We need this chicanery since we make property dialogs that are subsets
        /// of the entire property set on a document (or the Property Values Document to be exact.)
        /// This subset is stored in the TabOrder array.
        /// </summary>
        /// <param name="iAt">The cache element we are currently at.</param>
        /// <param name="iDir">The direction to go.</param>
        /// <returns>The next cache element to move the cursor to.</returns>
        protected FTCacheLine ElemNext( int iAt, int iDir ) {
            if( TabOrder == null )
                return null;

            int iNext = -2;
            for( int iTab = 0; iTab < TabOrder.Length; ++iTab ) {
                if( TabOrder[iTab] == iAt ) {
                    iNext = iTab + iDir;
                }
            }
            if( iNext < 0 ) {
                iNext = 0;
            } else {
                if( iNext >= TabOrder.Length ) {
                    iNext = TabOrder.Length - 1;
                }
            }

            Line oNext = DocForms2.GetLine( TabOrder[iNext] );

            // Got to keep the carat in our PropertyValues form.
            for( int i = 0; i< CacheList.Count; ++i ) {
                if( CacheList[i].Line == oNext ) {
                    _iCaretAtLayout = i;

                    //oLayout.SelectHead( Caret, e.Location, ModifierKeys == Keys.Shift );
                    //TODO: Select All.
                    return CacheList[i].Cache;
                }
            }
            return null;
        }

        public virtual int CaretHome => 0;

        public int CaretSanitized {
            get {
                if( _iCaretAtLayout < 0 ) {
                    if( CacheList.Count > 0 ) {
                        try {
                            _iCaretAtLayout = CaretHome; 
                        } catch( ArgumentOutOfRangeException ) {
                            // A little dubious. But let's give it a go.
                            _iCaretAtLayout = 0;
                        }
                    }
                }
                return _iCaretAtLayout;
            }
        }

        /// <summary>
        /// BUG: This was lifted from the scrolling text editor, but it's pretty broken
        /// behavor for a form. You need to move based on layout position not caret
        /// advance position. And in a twist of fate, I had to design such a thing for
        /// the mighty M$ 20 years ago!!</summary>
        /// <remarks>
        /// This is getting called on keyboard operations. I think I'd like to reset
        /// the cursor. Seems to be a better idea than locking the user out, especially
        /// when you can move to a valid position via mouse.
        /// </remarks>
        /// <seealso cref="CaretHome"/>
        public void CaretMove( Axis eAxis, int iDir, bool fJumpLine = false ) {
            try {
                int              iOffset   = Offset;
                float            flAdvance = Advance;
                LayoutSingleLine oLayout   = CacheList[CaretSanitized];

                oLayout.Selection.Length = 0;
                oLayout.OnChangeFormatting();

                // If total miss, build a new screen based on the location of the caret.
                FTCacheLine oElem = oLayout.Cache;

                if( iDir != 0 ) {
                    // First, see if we can navigate within the line we are currently at.
                    if( !oElem.Navigate( eAxis, iDir, ref flAdvance, ref iOffset ) || fJumpLine ) {
                        iDir = iDir < 0 ? -1 : 1; // Only allow move one line up or down.

                        FTCacheLine oNext = ElemNext( oLayout.Line.At, iDir );
                        if( oNext != null ) {
                            iOffset = oNext.OffsetBound( eAxis, iDir * -1, flAdvance );
                            oElem   = oNext;
                            // TODO: Set the advance...maybe?
                            oLayout.SelectAll( this );
                        }
                    }
                    // If going up or down ends up null, we won't be moving the caret.
                    //Caret.Cache   = oElem;
                    Offset  = iOffset;
                    Advance = (int)flAdvance;
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

        private static Type[] _rgStdErrors = { typeof( ApplicationException ),
                                               typeof( ArgumentOutOfRangeException ),
                                               typeof( NotImplementedException ),
                                               typeof( NullReferenceException ),
                                               typeof( InvalidCastException ) };

        /// <summary>
        /// Reposition the caret. If not on an invalid position we park the
        /// caret off screen.
        /// </summary>
        /// <seealso cref="OnMouseDown(MouseEventArgs)"
        protected void CaretIconRefresh() {
            if( Focused != true )
                return;

            // This can happen on first mouse touch, and the caret wasn't any
            // element. The form gets selected and this get's called. The caret
            // is updated at the exit of the MouseDown call.
            if( _iCaretAtLayout < 0 ) {
                User32.SetCaretPos( -10, -10 ); // Park it off screen.
                return;
            }

            try {
                int              iOffset   = Offset;
                float            flAdvance = Advance;
                LayoutSingleLine oLayout   = CacheList[_iCaretAtLayout];

                SKPointI pntCaretWorldLoc  = oLayout.CaretWorldPosition( this ); 
                if( oLayout.IsInside( pntCaretWorldLoc.X, pntCaretWorldLoc.Y ) ) {
                    User32.SetCaretPos(pntCaretWorldLoc.X, pntCaretWorldLoc.Y);
                } else {
                    User32.SetCaretPos( -10, -10 ); // Park it off screen.
                }
            } catch( Exception oEx ) {
                if( _rgStdErrors.IsUnhandled( oEx ) )
                    throw;

                _oSiteView.LogError( "Editing", "Problem moving the cursor" );
            }
        }

        /// <summary>
        /// See if there is non zero selection on the cache element pointed to by the caret.
        /// </summary>
        public bool IsSelection {
            get {
                try {
                    return CacheList[_iCaretAtLayout].Selection.Length > 0;
                } catch( Exception oEx ) {
                    if( _rgStdErrors.IsUnhandled( oEx ) )
                        throw;

                    _oSiteView.LogError( "Editing", "Problem moving the cursor" );
                    return false;
                }
            }
        }

        #region IPgCacheCaret
        protected LayoutSingleLine GetLayoutAtCaret => CacheList[_iCaretAtLayout];

        public int Advance { get; set; } = 0;

        /// <remarks>
        /// It probably would be nice to get rid of this accessor since we have a lot
        /// more ability to reset the caret here at the view level than we did when the
        /// caret was a simple stand alone struct. We really don't need this.
        /// </remarks>
        /// <see cref="BaseEditor.LineCharInsert"/>
        public Line Line { 
            get { 
                try {
                    // Normally we check if the line given is null or not. See
                    // BaseEditor::LineCharInsert(); See also "At" below.
                    if( _iCaretAtLayout < 0 )
                        return null;

                    return GetLayoutAtCaret.Line;
                } catch( Exception oEx ) {
                    if( _rgStdErrors.IsUnhandled( oEx ) )
                        throw;

                    _oSiteView.LogError( "Editing", "Problem reporting caret Line." );
                    return DocForms2.GetLine(0);
                }
            }
            set { 
                _iCaretAtLayout = -1;
                for( int i = 0; i< CacheList.Count; ++i ) {
                    if( CacheList[i].Line == value ) {
                        _iCaretAtLayout = i;
                        break;
                    }                        
                }
                if( _iCaretAtLayout > -1 ) {
                    Advance = 0;
                }
            }
        }

        public int At { 
            get { 
                try {
                    // Technically this is zero. But at present some windows if
                    // not visited for the first time have carets set to -1.
                    if( _iCaretAtLayout < 0 )
                        return -1;

                    return GetLayoutAtCaret.Line.At;
                } catch( Exception oEx ) {
                    if( _rgStdErrors.IsUnhandled( oEx ) )
                        throw;

                    _oSiteView.LogError( "Editing", "Problem reporting caret position." );
                    return -1;
                }
            }
        }
        public int ColorIndex { get { throw new NotImplementedException(); } }

        public int Offset { get; set; } = 0;
        public int Length { set { } get => 0; }

        public IPgCaretInfo<Row> Caret2 => throw new NotImplementedException();
        #endregion

        public void OnKey_Delete( bool fBackSpace ) {
            try {
                LayoutSingleLine oLayout = GetLayoutAtCaret;
                int              iLineAt = oLayout.Line.At;

                if( IsSelection ) {
                    DocForms2.LineTextReplace( iLineAt, oLayout.Selection, null );
                    oLayout.SelectClear();
                } else {
                    if( fBackSpace ) {
                        DocForms2.LineTextReplace( iLineAt, new ColorRange( Offset - 1, 1 ), null );
                    } else {
                        DocForms2.LineTextReplace( iLineAt, new ColorRange( Offset, 1 ), null );
                    }
                }
                OnKeyDown_Arrows( Axis.Horizontal, 0 );
            } catch( Exception oEx ) {
                if( _rgStdErrors.IsUnhandled( oEx ) )
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
                    // We handle this in OnKeyDown now...
                    return;
                }
                if( !char.IsControl( e.KeyChar )  ) { 
                    LayoutSingleLine   oLayout = GetLayoutAtCaret;
                    int                iLineAt = oLayout.Line.At;
                    ReadOnlySpan<char> spChar  = stackalloc char[1] { e.KeyChar };

                    if( IsSelection ) {
                        DocForms2.LineTextReplace( iLineAt, oLayout.Selection, spChar );
                        oLayout.SelectClear();
                    } else {
                        DocForms2.LineTextReplace( iLineAt, this, spChar );
                    }
                    // Find the new carat position and update its screen location. 
                    OnKeyDown_Arrows( Axis.Horizontal, 0 );
                }
            } catch( Exception oEx ) {
                if( _rgStdErrors.IsUnhandled( oEx ) )
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
            if( Width > 0 && Height > 0 ) {
			    Layout.SetRect( 0, 0, Width, Height );
			    Layout.LayoutChildren();
            }

            CaretIconRefresh();

            Invalidate();
        }

        protected override void OnGotFocus(EventArgs e) {
            base.OnGotFocus( e );

            _oSiteView.EventChain.NotifyFocused(true);

            // Not perfect but getting better...
            int iCaratHeight = (int)(StdUI.FontRendererAt( StdFont ).LineHeight );

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
                    int iDir = e.Modifiers == Keys.Shift ? -1 : 1; 
                    CaretMove( Axis.Vertical, iDir, fJumpLine:true );
                    break;
                case Keys.Escape:
                    try {
                        GetLayoutAtCaret.SelectClear();
                        Invalidate();
                    } catch( Exception oEx ) {
                        if( _rgStdErrors.IsUnhandled( oEx ) )
                            throw;
                    }
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
                        DocForms2.Undo();
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
                try {
                    // Set the caret for sure if hit. If not just leave it where ever it was.
                    for( int i=0; i<CacheList.Count; i++ ) {
                        LayoutSingleLine oLayout = CacheList[i];
                        // NOTE: This is why we have to have all Line objects in a single Editor.
                        if( oLayout.IsInside( e.X, e.Y )  ) {
                            if( _iCaretAtLayout == i ) {
                                GetLayoutAtCaret.SelectHead( this, e.Location, ModifierKeys == Keys.Shift );
                            } else {
                                _iCaretAtLayout = i;
                                GetLayoutAtCaret.SelectHead( this, e.Location, false );
                            }
                            // BUG: Shouldn't call this if there is a selection on the link.
                            Links.Find( GetLayoutAtCaret.Line, Offset, true );
                        }
                    }
                } catch( Exception oEx ) {
                    if( _rgStdErrors.IsUnhandled( oEx ) )
                        throw;
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
                    SKPointI pntLocal = new SKPointI( e.X - oLayout.Left,
                                                      e.Y - oLayout.Top);
                    int iEdge = oLayout.Cache.GlyphPointToOffset( pntLocal );
                    if( Links.Find( oLayout.Line, iEdge, false ) ) {
                        oCursor = Cursors.Hand;
                    } else {
                        oCursor = Cursors.IBeam;
                    }
                    break;
                }
            }
            Cursor = oCursor;

            // This is a bug since we're just calling SelectNext with no clue if the
            // item was previously selected in the first place!!
            if ((e.Button & MouseButtons.Left) == MouseButtons.Left && e.Clicks == 0 ) {
                try {
                    GetLayoutAtCaret.SelectNext( this, e.Location );

                    CaretIconRefresh();
                    Invalidate();
                    Update();
                } catch( Exception oEx ) {
                    if( _rgStdErrors.IsUnhandled( oEx ) )
                        throw;
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
                try {
                    LayoutSingleLine oLayout = GetLayoutAtCaret;

                    DocForms2.LineTextReplace( oLayout.Line.At, oLayout.Selection, null );
                } catch( Exception oEx ) {
                    if( _rgStdErrors.IsUnhandled( oEx ) )
                        throw;
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
                LayoutSingleLine oLayout = GetLayoutAtCaret;
				if( IsSelection ) {
					oSelection = oLayout.Selection;
				} else {
					oSelection = new ColorRange( 0, oLayout.Line.ElementCount, 0 );
 				}
				if( oSelection != null ) {
					string strSelection = oLayout.Line.SubString( oSelection.Offset, oSelection.Length );

				    oDataObject.SetData      ( strSelection );
				    Clipboard  .SetDataObject( oDataObject );
				}
            } catch( Exception oEx ) {
                if( _rgStdErrors.IsUnhandled( oEx ) )
                    throw;
                _oSiteView.LogError( "Clipboard", "Copy to clipboard problem" );
			}
        }

        public void ClipboardPasteFrom( 
            object oDataSource, 
            Guid   sOperation ) 
        {
            //if( _fReadOnly )
            //    return;

            try {
                LayoutSingleLine oLayout = GetLayoutAtCaret;
                int              iLineAt = oLayout.Line.At;
                IDataObject      oData   = oDataSource as IDataObject;

                // TODO: This might be a dummy line. So we need dummies to be at -1.
                //       Still a work in progress. See oBulk.LineInsert() below...
                if( sOperation == ClipboardOperations.Text ||
                    sOperation == ClipboardOperations.Default 
                  ) {
                    if( oData.GetData(typeof(System.String)) is string strPaste ) {
                        if( IsSelection ) {
                            DocForms2.LineTextReplace( iLineAt, oLayout.Selection, strPaste );
                            oLayout.SelectClear();
                        } else {
                            DocForms2.LineTextReplace( iLineAt, this, strPaste );
                        }
                    }
                }
            } catch( Exception oEx ) {
                if( _rgStdErrors.IsUnhandled( oEx ) )
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
			Cache.Measure( StdUI.FontRendererAt( _uiStdText ) );
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
