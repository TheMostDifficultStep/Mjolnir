﻿using System;
using System.Collections.Generic;
using System.Collections;
using System.Windows.Forms;
using System.Xml;
using System.Diagnostics;
using System.IO;
using System.Drawing;

using SkiaSharp;
using SkiaSharp.Views.Desktop;

using Play.Interfaces.Embedding;
using Play.Rectangles;
using Play.Controls;
using Play.Parse;

namespace Play.Edit {
    public interface IPgDocTraits<T> {
        T HighLight { get; set; }
        StdUIColors PlayHighlightColor { get; set; }
        bool        ReadOnly { get; set; }

        event Action<T> HighLightChanged; // Only one line is high lighted.
        event Action<T> CheckedEvent;     // Any number of rows can be checked.
    }

    public static class DocOpExtender {
        public static bool TryReplaceAt( this IPgDocOperations<Row> oDoc, IPgCaretInfo<Row> oCaret, ReadOnlySpan<char> spText ) {
            return oDoc.TryReplaceAt( oCaret.Row, oCaret.Column, oCaret.Offset, oCaret.Length, spText );
        }
    }

    public interface IPgDocOperations<T> {
        void ListenerAdd   ( IPgEditEvents e );
        void ListenerRemove( IPgEditEvents e );

        bool TryReplaceAt( T oRow, int iColumn, int iSrcOff, int iSrcLen, ReadOnlySpan<char> spText );
        bool TryDeleteAt( T oRow, int iColumn, int iSrcOff, int iSrcLen ); // Would like to remove this one...
        bool RowDeleteAt( T oRow );
    }

    public enum EditType {
        ModifyElem, // And TextLine char replace/delete
        DeleteRow,  // Entire row (or line in text editor) deleted.
        InsertRow   // Not particularly actionable on bulk inserts... :-/
    }

    public interface IPgEditHandler :
        IEnumerable<IPgCaretInfo<Row>>
    {
        // Basically we might have info at the time of the edit start
        // that we want to use when we are finishing up. So need this
        // here and not on the greater window.
        void OnUpdated( EditType eEdit, Row oRow ); 
    }

    public interface IPgEditEvents {
        IPgEditHandler CreateEditHandler(); // Any kind of edit.
        void           OnDocFormatted();    // Document gets formatted b/c of a parse...
    }

        /* Move these to viewform.cs later */
    public interface IPgCacheWindow {
        public Control Guest     { get; }
        public uint    MaxHeight { get; set; }
        public int     Height    { get; }
        public void    OnChangeSize( int iWidth );
        public void    MoveTo( SmartRect rcSquare );
    }

    /// <summary>
    /// Put a control in the place of an FtCacheLine.
    /// </summary>
    public class CacheControl :
        IPgCacheMeasures,
        IPgCacheWindow
    {
        public Control Guest     { get; }
        public uint    MaxHeight { get; set; } = 200;
        public int     Height    { get; protected set; }
        public int     LastOffset => 0;
                       
        public float   UnwrappedWidth { get; protected set; } = 0;
                       
        public bool    IsInvalid { get => false; set { } }



        public CacheControl( Control oGuest ) {
            Guest = oGuest ?? throw new ArgumentNullException();
        }

        public Point GlyphOffsetToPoint(int iOffset) {
            return new Point( 0, 0 );
        }

        public int GlyphPointToOffset(int iRowTop, SKPointI pntWorld) {
            return 0;
        }

        public bool Navigate(Axis eAxis, int iDir, ref float flAdvance, ref int iOffset) {
            return false;
        }

        public int OffsetBound(Axis eAxis, int iIncrement, float flAdvance) {
            return 0;
        }

        public void Colorize(ICollection<ILineSelection> rgSelections) {
        }
        public void Colorize(IColorRange rgColorRange ) {
        }

        /// <summary>
        /// Notice the 2 step nature of our sizing. Our width is not negotable.
        /// But our height can't be determined until the entire row has been
        /// queried. And not even Top is available, since we need to see what
        /// will fit on the screen first! O.o
        /// </summary>
        /// <remarks>So save our height as prefered, and then use it when 
        /// render comes along.</remarks>
        public void OnChangeSize( int iWidth ) {
			Size szProposed = new Size( iWidth, (int)MaxHeight );
			Size szPrefered = Guest.GetPreferredSize( szProposed );

            Height         = Math.Min( szPrefered.Height, (int)MaxHeight );
            UnwrappedWidth = iWidth;
        }

        public void Measure(IPgFontRender oRender) {
        }

        /// <summary>
        /// The square s/b at least our desired height (upto the max height)
        /// But it might be taller b/c of other elements on the line.
        /// </summary>
        public void MoveTo( SmartRect rcSquare ) {
            Guest.SetBounds( rcSquare.Left, rcSquare.Top, rcSquare.Width, Math.Min( Height, rcSquare.Height) );
        }
    }

    public class WindowMultiColumn :
        SKControl, 
        IPgParent,
        IPgLoad<XmlElement>,
        IPgSave<XmlDocumentFragment>,
        IPgEditEvents,
        IPgTextView,
        IEnumerable<ILineRange>
    {
        public static Guid _sGuid = new Guid( "{03F21BC8-F911-4FE4-931D-9EB9F7A15A10}" );

        protected readonly IPgViewSite   _oSiteView;
        protected readonly IPgViewNotify _oViewEvents;

        protected readonly IEnumerable     <Row> _oDocEnum;
        protected readonly IReadableBag    <Row> _oDocList;
        protected readonly IPgDocTraits    <Row> _oDocTraits;
        protected readonly IPgDocOperations<Row> _oDocOps;
        protected readonly CacheMultiColumn      _oCacheMan;
		protected readonly IPgStandardUI2        _oStdUI;
        protected readonly ScrollBar2            _oScrollBarVirt;
        protected readonly LayoutStack           _rgLayout;
        protected readonly List<SmartRect>       _rgColumns = new(); // Might not match document columns! O.o

        protected bool  _fReadOnly     = false;
        protected SizeF _szScrollBars  = new SizeF( .1875F, .1875F );
        protected Dictionary<string, Action<Row, int, IPgWordRange>> HyperLinks { get; } = new ();

        readonly static Keys[] _rgHandledKeys = { Keys.PageDown, Keys.PageUp, Keys.Down,
                                                  Keys.Up, Keys.Right, Keys.Left, Keys.Back,
                                                  Keys.Delete, Keys.Enter, Keys.Tab,
                                                  Keys.Shift | Keys.Tab,
                                                  Keys.Control | Keys.A,
                                                  Keys.Control | Keys.F };

        public IPgParent Parentage => _oSiteView.Host;
        public IPgParent Services  => Parentage.Services;
        public SKPoint   DPI { get; protected set; } 
        protected IPgStandardUI2 StdUI => _oStdUI;
        protected ushort         StdFace { get; }

        /// <summary>
        /// How much readonly can you get? Window only or doc level. :-/
        /// </summary>
        public bool ReadOnly { 
            get {
                if( _oDocTraits.ReadOnly )
                    return true;

                return _fReadOnly;
            }
        }

        protected class CacheManSite :
            ICacheManSite,
            IReadableBag<Row>
        {
            readonly WindowMultiColumn _oHost;

            // Standard errors.
            static readonly Type[] _rgErrors = 
                { typeof( NullReferenceException ),
                  typeof( ArgumentOutOfRangeException ),
                  typeof( IndexOutOfRangeException ) };

            public CacheManSite( WindowMultiColumn oHost ) {
                _oHost = oHost;
            }

            public IPgParent Host => _oHost;

            public int ElementCount => _oHost._oDocList.ElementCount;

            public virtual Row this[int iIndex] => _oHost._oDocList[iIndex];

            public virtual Row TabStop( int iIndex ) {
                try {
                    if( iIndex >= _oHost._oDocList.ElementCount )
                        return null;
                    if( iIndex < 0 )
                        return null;

                    return _oHost._oDocList[iIndex];
                } catch( Exception oEx ) {
                    Type[] rgErrors = { typeof( NullReferenceException ),
                                        typeof( ArgumentOutOfRangeException ) };
                    if( rgErrors.IsUnhandled( oEx ) )
                        throw;

                    LogError( "Tab Order", "Problem with tab list" );
                }

                return null;
            }
            public virtual int TabCount => _oHost._oDocList.ElementCount;

            public virtual Row TabOrder( Row oRow, int iDir ) {
                if( oRow == null )
                    return null;

                return TabStop( oRow.At + iDir );
            }

            public float GetScrollProgress {
                get {
                    float flProgress = _oHost._oScrollBarVirt.Progress;

                    if( flProgress > 1 )
                        flProgress = 1;
                    if( flProgress < 0 )
                        flProgress = 0;

                    return flProgress;
                }
            }

            public void LogError(string strMessage, string strDetails, bool fShow = true) {
                _oHost.LogError( strDetails, fShow );
            }

            public void Notify(ShellNotify eEvent) {
				switch( eEvent ) {
					case ShellNotify.DocumentDirty:
						_oHost.Invalidate();
						break;
				}
            }

            public void OnRefreshComplete(float flProgress, float flVisiblePercent) {
                _oHost._oScrollBarVirt.Refresh( flProgress, flVisiblePercent );
				_oHost.Invalidate();
            }

            /// <summary>
            /// If you hide the caret, that seems to destroy it.
            /// So the we just move it off screen. :-/
            /// </summary>
            /// <param name="pntCaret"></param>
            /// <param name="fVisible"></param>
            public void OnCaretPositioned( SKPointI pntCaret, bool fVisible ) {
                if( _oHost.Focused )
                    User32.SetCaretPos( pntCaret.X, pntCaret.Y );
            }

            public uint FontCache( uint uiHeight ) {
                return _oHost._oStdUI.FontCache( _oHost.StdFace, uiHeight, _oHost.GetDPI() );
            }
            public IPgFontRender FontUse( uint uiFont ) {
                return _oHost._oStdUI.FontRendererAt( uiFont );
            }

        }

        protected class DocSlot :
			IPgBaseSite
		{
			protected readonly WindowMultiColumn _oHost;

			public DocSlot( WindowMultiColumn oHost ) {
				_oHost = oHost ?? throw new ArgumentNullException();
			}

			public IPgParent Host => _oHost;

			public void LogError(string strMessage, string strDetails, bool fShow=true) {
				_oHost._oSiteView.LogError( strMessage, strDetails, fShow );
			}

			public void Notify( ShellNotify eEvent ) {
			}
		} // End class

        public WindowMultiColumn( IPgViewSite oViewSite, object oDocument ) {
            _oDocEnum   = (IEnumerable     <Row>)oDocument;
            _oDocList   = (IReadableBag    <Row>)oDocument;
            _oDocTraits = (IPgDocTraits    <Row>)oDocument;
            _oDocOps    = (IPgDocOperations<Row>)oDocument;

            _oSiteView   = oViewSite;
            _oViewEvents = oViewSite.EventChain ?? throw new ArgumentException( "Site.EventChain must support IPgViewSiteEvents" );

            _oStdUI         = oViewSite.Host.Services as IPgStandardUI2 ?? throw new ArgumentException( "Parent view must provide IPgStandardUI service" );
            _oScrollBarVirt = new ScrollBar2( new DocSlot( this ) );
            _rgLayout       = new LayoutStackHorizontal() { Spacing = 5, Units = LayoutRect.CSS.Flex};

            /// <seealso cref="EditWindow2"/>
            IPgMainWindow.PgDisplayInfo oInfo = new IPgMainWindow.PgDisplayInfo();
            if( _oSiteView.Host.TopWindow is IPgMainWindow oMainWin ) {
                oInfo = oMainWin.MainDisplayInfo;
            }
            DPI = new SKPoint( oInfo.pntDpi.X, oInfo.pntDpi.Y );

            StdFace = StdUI.FaceCache(@"C:\windows\fonts\consola.ttf");

            _oCacheMan = CreateCacheMan();

            Array.Sort<Keys>( _rgHandledKeys );

            Parent = _oSiteView.Host as Control;
        }

        protected virtual CacheMultiColumn CreateCacheMan() {
            //uint uiStdText  = _oStdUI.FontCache( StdFace, 12, GetDPI() );
            return new CacheMultiColumn( new CacheManSite( this ), _rgColumns ); 
        }

        public virtual bool IsDirty => true;
        protected override void Dispose( bool disposing ) {
            if( disposing ) {
                _oDocOps       .ListenerRemove  ( this );
                _oScrollBarVirt.Scroll -= OnScrollBar; 
                HyperLinks     .Clear();
                User32         .DestroyCaret();
            }

            base.Dispose(disposing);
        }

        protected void LogError( string strMessage, bool fShow = false ) {
            _oSiteView.LogError( "Multi Column Window", strMessage, fShow );
        }

        /// <summary>
        /// 
        /// </summary>
        /// <exception cref="ObjectDisposedException" />
        /// <exception cref="FileNotFoundException" />
        /// <exception cref="NullReferenceException" />
        /// <exception cref="System.ComponentModel.Win32Exception" />
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

        protected override bool IsInputKey(Keys keyData) {
            int iIndex = Array.BinarySearch<Keys>(_rgHandledKeys, keyData);

            if( iIndex >= 0 )
                return true;

            return base.IsInputKey( keyData );
        }

        public SKPoint GetDPI() {
            // The object we get from the interface has some standard screen dpi and size
            // values. We then attempt to override those values with our actual values.
            IPgMainWindow.PgDisplayInfo oInfo = new IPgMainWindow.PgDisplayInfo();
            if( _oSiteView.Host.TopWindow is IPgMainWindow oMainWin ) {
                oInfo = oMainWin.MainDisplayInfo;
            }
            return new SKPoint( oInfo.pntDpi.X, oInfo.pntDpi.Y );
        }

        /// <summary>
        /// Where we really initialize. Nice to have the scroll bar initialize size
        /// here since it would be cool if I reload the widths of things from
        /// persistance in the future!.
        /// </summary>
        /// <seealso cref="InitNew"/>
        /// <seealso cref="Load"/>
        protected virtual bool Initialize() {
            _oScrollBarVirt.Parent  = this;
            _oScrollBarVirt.Visible = true;
            _oScrollBarVirt.Scroll += OnScrollBar; 

            _rgLayout.Add( new LayoutControl( _oScrollBarVirt, 
                                              LayoutRect.CSS.Pixels, 
                                              (uint)(DPI.X * _szScrollBars.Width) ) );

            _oDocOps.ListenerAdd( this );
            _oDocTraits.HighLightChanged += _oDocTraits_HighLightChanged;

            if( this.ContextMenuStrip == null ) {
                ContextMenuStrip oMenu = new ContextMenuStrip();
                oMenu.Items.Add( new ToolStripMenuItem( "Cut",   null, OnCut,   Keys.Control | Keys.X ) );
                oMenu.Items.Add( new ToolStripMenuItem( "Copy",  null, OnCopy,  Keys.Control | Keys.C ) );
                oMenu.Items.Add( new ToolStripMenuItem( "Paste", null, OnPaste, Keys.Control | Keys.V ) );
              //oMenu.Items.Add( new ToolStripMenuItem( "Jump",  null, this.OnJump,  Keys.Control | Keys.J ) );
                this.ContextMenuStrip = oMenu;
            }


            //_oCacheMan.CaretReset( _oDocList[0], 0 );
            // can't reset, not ready yet, but might want the cursor at 0,0 so to speak... :-/
            return true;
        }

        private void _oDocTraits_HighLightChanged(Row oRow) {
            if( Focused && oRow != null ) {
                _oCacheMan.SetCaretPositionAndScroll( oRow.At, 0, 0, true );
            }
        }

        public virtual bool InitNew() {
            if( !Initialize() )
                return false;

            return true;
        }

        public virtual bool Load(XmlElement oStream) {
            if( !Initialize() )
                return false;

            return true;
        }

        public bool Save(XmlDocumentFragment oStream) {
            return true;
        }

        public void OnCut(object o, EventArgs e ) {
            if( _oCacheMan.SelectedRowCount == 1 ) {
                if( _oCacheMan.SelectedColCount == 1 ) {
                }
            }
        }

        public void OnPaste(object o, EventArgs e ) {
        }

        public void OnCopy(object o, EventArgs e ) {
            ClipboardCopyTo();
        }

        public void ClipboardCutTo() {
            ClipboardCopyTo();
            _oCacheMan.SelectionDelete();
        }

        public virtual void ClipboardCopyTo() {
            DataObject oDataObject = new DataObject();

			try {
                string strSelection = _oCacheMan.SelectionCopy();

                oDataObject.SetData( strSelection );
				Clipboard.SetDataObject( oDataObject );
			} catch( NullReferenceException ) {
			}
        }

        #region IPgEditEvents
        public IPgEditHandler CreateEditHandler() {
            return _oCacheMan.CreateDocEventObject();
        }

        /// <summary>
        /// We remeasure since parse is typically caused by a
        /// text change.
        /// </summary>
        public void OnDocFormatted() {
            _oCacheMan.CacheReMeasure();
            Invalidate();
        }

        public void OnDocUpdated() {
            _oCacheMan.CacheReMeasure();
            Invalidate();
        }
        #endregion

        public class SimpleRange :
            ILineRange {
            public Line Line { get ; set ; }
            public int  At { get; set; }
            public int  ColorIndex => 0;
            public int  Offset { get ; set ; } = 0;
            public int  Length { get ; set ; } = 0;
        }

        /// <summary>
        /// TODO: This is for the system text find dialog. However I notice
        /// I'm not starting at the cursor line but just at the top
        /// of the document. Probably should fix that...
        /// </summary>
        public IEnumerator<ILineRange> GetEnumerator() {
            SimpleRange oRange = new SimpleRange();

            foreach( Row oRow in _oDocEnum ) {
                if( _oCacheMan.CaretColumn < _rgColumns.Count ) {
                    foreach( Line oLine in oRow ) {
                        oRange.Line   = oLine;
                        oRange.Offset = 0;
                        oRange.Length = oLine.ElementCount;
                        oRange.At     = oRow .At;

                        yield return oRange;
                    }
                }
            }
        }

        IEnumerator IEnumerable.GetEnumerator() {
            return GetEnumerator();
        }

        protected override void OnSizeChanged( EventArgs e ) {
            base.OnSizeChanged(e);

            _rgLayout.SetRect( 0, 0, Width, Height );
			_rgLayout.LayoutChildren();

            // Almost ready to remove this. The TextRect is basically
            // the window display intersecting the union of all the columns.
            // Only the height really used. Columns come from layout.
            _oCacheMan.TextRect.SetRect( 0, 0, Width, Height );
            _oCacheMan.OnChangeSize();
        }

        protected override void OnGotFocus(EventArgs e) {
            base.OnGotFocus(e);

            _oViewEvents.NotifyFocused( true );

            if( !_fReadOnly ) {
                User32.CreateCaret( Handle, IntPtr.Zero, 
                                    _oCacheMan.CaretSize.X, 
                                    _oCacheMan.CaretSize.Y );

                // Hiding the caret seems to destroy it so just
                // immediately show, the cacheman will park it off screen
                // if it's not displayable on an active cache line.
                _oCacheMan.IsCaretVisible( out SKPointI pntCaret );

                User32.SetCaretPos( pntCaret.X, pntCaret.Y );
                User32.ShowCaret  ( Handle );
            }

            Invalidate();
        }
        protected override void OnLostFocus(EventArgs e) {
            base.OnLostFocus( e );

            _oScrollBarVirt.Show( SHOWSTATE.Inactive );

            _oViewEvents.NotifyFocused( false );
            User32.DestroyCaret();

            Invalidate();
        }

        /// <summary>
        /// Paint BG depending on various highlight modes for the row/column.
        /// </summary>
        protected void PaintSquareBG( 
            SKCanvas       skCanvas, 
            SKPaint        skPaint, 
            CacheRow       oCRow, 
            int            iColumn,
            IPgCacheRender oElemInfo,
            SmartRect      rctCRow 
        ) {
            try {
                StdUIColors eBg = StdUIColors.Max;

                if( _oCacheMan.CaretAt == oCRow.At )
                    eBg = StdUIColors.BGWithCursor;
                if( _oDocTraits.HighLight != null && oCRow.At == _oDocTraits.HighLight.At)
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

        protected void OnPaintSurface_Test(SKPaintSurfaceEventArgs e) {
            SKSurface skSurface = e.Surface;
            SKCanvas  skCanvas  = skSurface.Canvas;
            using SKPaint skPaint = new SKPaint {
                Color = SKColors.Blue
            };
            foreach( SmartRect rctColumn in _rgLayout ) {
                skCanvas.DrawRect( rctColumn.SKRect, skPaint);

                if( skPaint.Color == SKColors.Blue ) {
                    skPaint.Color = SKColors.Red;
                } else { 
                    if( skPaint.Color == SKColors.Red ) {
                        skPaint.Color = SKColors.Blue;
                    }
                }
            }
        }

        protected SmartRect SparePaintingRect { get; } = new();

        /// <remarks>
        /// Used to do a simple refresh before painting, but no longer... O.o
        /// </remarks>
        protected override void OnPaintSurface(SKPaintSurfaceEventArgs e) {
            base.OnPaintSurface(e);

            try {
                SKSurface skSurface = e.Surface;
                SKCanvas  skCanvas  = skSurface.Canvas;

                // BG of multi column window is always ReadOnly... O.o
                using SKPaint skPaintBG = new SKPaint() {
                    BlendMode = SKBlendMode.Src,
                    Color     = _oStdUI.ColorsStandardAt( StdUIColors.BGReadOnly )
                };
                using SKPaint skPaintTx = new SKPaint() { FilterQuality = SKFilterQuality.High };

                // Paint all window background. Note: We could get by without this if
                // there was no space between lines/columns.
                skCanvas.DrawRect( new SKRect( 0, 0, Width, Height ), skPaintBG);

                // Now paint the rows.
                SmartRect rctSquare = SparePaintingRect;
                foreach( CacheRow oCacheRow in _oCacheMan ) {
                    for( int iCacheCol=0; iCacheCol<oCacheRow.CacheList.Count; ++iCacheCol ) {
                        if( oCacheRow[iCacheCol] is IPgCacheRender oRender ) {
                            SmartRect oColumn = _rgColumns[iCacheCol];

                            rctSquare.SetRect( oColumn.Left, oCacheRow.Top, oColumn.Right, oCacheRow.Bottom );

                            // Test pattern...
                            //skPaint.Color = iCache % 2 == 0 ? SKColors.Blue : SKColors.Green;
                            //skCanvas.DrawRect( rctSquare.SKRect, skPaint );
                            PaintSquareBG( skCanvas, skPaintBG, oCacheRow, iCacheCol, oRender, rctSquare );

                            oRender.Render(skCanvas, _oStdUI, skPaintTx, rctSquare, this.Focused );
                        }
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

        public static bool IsCtrlKey( Keys oKey ) => ( oKey & Keys.Control ) != 0;
        public struct RangeStruct :
            IColorRange 
        {
            int _iOffset;
            int _iLength;

            public RangeStruct( int iOffset, int iLength ) {
                _iOffset = iOffset;
                _iLength = iLength;
            }

            public int ColorIndex => 0;

            public int Offset { get => _iOffset; 
                                set => throw new InvalidOperationException(); }
            public int Length { get => _iLength; 
                                set => throw new NotImplementedException(); }
        }
        protected bool HyperLinkFind( Row oRow, int iColumn, int iOffset, bool fDoJump ) {
            Line        oLine = oRow[iColumn];
            RangeStruct oFind = new RangeStruct( iOffset, 1 );

            IPgWordRange oRange = oLine.FindFormattingUnderRange( oFind );
            if( oRange != null ) { 
                foreach( KeyValuePair<string, Action<Row, int, IPgWordRange>> oPair in HyperLinks ) { 
                    if( oRange.StateName == oPair.Key ) {
                        if( fDoJump )
                            oPair.Value?.Invoke( oRow, iColumn, oRange );
                        return true;
                    }
                }
            }

            return false;
        }

        /// <remarks>
        /// Note that the Row returned from the PointToRow() function might be a
        /// 'mirror' row. See the property page code. The row.At value won't point
        /// do the row in the DocProperties object. row.At in that case is the
        /// TabOrder...
        /// </remarks>
        protected bool HyperLinkFind( int iColumn, SKPointI pntLocation, bool fDoJump ) {
            try {
                if( _oCacheMan.PointToRow( iColumn, pntLocation, out int iOff, out Row oRow ) ) {
                    return HyperLinkFind( oRow, iColumn, iOff, fDoJump );
                }
            } catch( Exception oEx ) {
                Type[] rgErrors = { typeof( NullReferenceException ),
                                    typeof( ArgumentOutOfRangeException ) };
                if( rgErrors.IsUnhandled( oEx ) )
                    throw;
                if( fDoJump ) // avoid throwing up a pile of messages on mouse move!
                    LogError( "Hyperlink exception" );
            }

            return false;
        }
        
        /// <summary>
        /// Update the cursor shape. Right now we're written for browser mode.
        /// So a mouse hovering over a hyperlink will show a hand.
        /// If left mouse down for select, then make I-beam with in 
        /// column.
        /// </summary>
        protected void CursorUpdate( SKPointI pntLocation, MouseButtons eButton ) {
            Cursor oNewCursor = Cursors.Arrow;

            for( int iColumn=0; iColumn<_rgColumns.Count; ++iColumn ) {
                SmartRect oColumn = _rgColumns[iColumn];
                if( oColumn.IsInside( pntLocation.X, pntLocation.Y ) ) {
                    oNewCursor = Cursors.IBeam;
                    if( eButton != MouseButtons.Left &&         // if not selecting.
                        ((ModifierKeys & Keys.Control) == 0 ) ) // if not editing...
                    { 
                        if( HyperLinkFind( iColumn, pntLocation, fDoJump:false ) )
                            oNewCursor = Cursors.Hand;
                        break;
                    }
                }
            }

            Cursor = oNewCursor;
        }

        protected override void OnMouseWheel(MouseEventArgs e) {
            base.OnMouseWheel(e);

            _oCacheMan.OnMouseWheel( e.Delta );
        }

        /// <summary>
        /// Event handler for the vertical or horizontal scroll bar.
        /// </summary>
        void OnScrollBar( ScrollEvents e ) {
            _oCacheMan.OnScrollBar_Vertical( e );
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
                    _oCacheMan.OnScrollBar_Vertical( ScrollEvents.LargeIncrement );
                    break;
                case Keys.PageUp:
                    _oCacheMan.OnScrollBar_Vertical( ScrollEvents.LargeDecrement );
                    break;
                case Keys.Down:
                    _oCacheMan.CaretMove( Axis.Vertical, 1 );
                    break;
                case Keys.Up:
                    _oCacheMan.CaretMove( Axis.Vertical, -1 );
                    break;
                case Keys.Right:
                    _oCacheMan.CaretMove( Axis.Horizontal, 1 );
                    break;
                case Keys.Left:
                    _oCacheMan.CaretMove( Axis.Horizontal, -1 );
                    break;
                case Keys.Back:
                    if(  !_fReadOnly && _oCacheMan.CopyCaret() is CacheMultiColumn.CaretInfo oCaret ) {
                        _oDocOps.TryDeleteAt( oCaret.Row, oCaret.Column, oCaret.Offset - 1, 1 );
                    }
                    break;
            }
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)	
        {
            if( this.IsDisposed )
                return false;

            const int WM_KEYDOWN    = 0x100;
            const int WM_SYSKEYDOWN = 0x104;
               
            if ((msg.Msg == WM_KEYDOWN) || (msg.Msg == WM_SYSKEYDOWN))
            {
                switch(keyData) {
                    case Keys.Control | Keys.F:
                        _oViewEvents.IsCommandKey( CommandKey.Find, KeyBoardEnum.Control );
                        return true;
                    case Keys.Control | Keys.A:
                        //SelectionSetAll();
                        Invalidate();
                        return true;

                    case Keys.Control | Keys.Z:
                        if( !_fReadOnly ) {
                            //_oDocument.Undo();
                        }
                        return true;
                    case Keys.Control | Keys.Q:
                        if( !_fReadOnly ) { // Or column or elem locked...
                            if( _oCacheMan.CopyCaret() is CacheMultiColumn.CaretInfo oCaret ) {
                                _oDocOps.RowDeleteAt( oCaret.Row );
                            }
                        }
                        return true;
                    case Keys.Delete: {
                        // The only way to get this event.
                        if( !_fReadOnly ) {
                            if( _oCacheMan.CopyCaret() is CacheMultiColumn.CaretInfo oCaret ) {
                                _oDocOps.TryDeleteAt( oCaret.Row, oCaret.Column, oCaret.Offset, 1 );
                            }
                        }
                        return true;
                    }
                }
            } 

            return base.ProcessCmdKey( ref msg, keyData );
        } // end method

        protected override void OnKeyPress(KeyPressEventArgs e) {
            if( IsDisposed )
                return;
            if( _oViewEvents.IsCommandPress( e.KeyChar ) )
                return;
            if( _fReadOnly )
                return;

            if( !char.IsControl( e.KeyChar ) ) {
                ReadOnlySpan<char> rgInsert = stackalloc char[1] { e.KeyChar };

                _oDocOps.TryReplaceAt( _oCacheMan.CopyCaret(), rgInsert );

                e.Handled = true;
            }
        }

        protected override void OnMouseMove(MouseEventArgs e) {
            base.OnMouseMove( e );

            SKPointI pntMouse = new SKPointI( e.X, e.Y );

            CursorUpdate( pntMouse, e.Button );
            if( _oCacheMan.IsSelecting ) {
                _oCacheMan.CacheReColor();
                _oCacheMan.CaretAdvance(pntMouse);
                Invalidate();
            }
        }

        protected override void OnMouseUp(MouseEventArgs e) {
            base.OnMouseUp(e);

            _oCacheMan.EndSelect();
        }

        public static bool IsCtrl( Keys sKey ) {
            return (ModifierKeys & Keys.Control) != 0;
        }

        protected override void OnMouseDown(MouseEventArgs e) {
            base.OnMouseDown( e );

            Select();
            SKPointI pntClick = new SKPointI( e.X, e.Y );

            // Move the caret and reset the Advance.
            _oCacheMan.CaretAdvance( pntClick );

            if( e.Button == MouseButtons.Left && !IsCtrl( ModifierKeys ) ) {
                bool fHyperLinked = false;
                for( int iColumn=0; iColumn<_rgColumns.Count; ++iColumn ) {
                    SmartRect oColumn = _rgColumns[iColumn];
                    if( oColumn.IsInside( e.X, e.Y ) ) {
                        fHyperLinked = HyperLinkFind( iColumn, pntClick, fDoJump:true );
                        break;
                    }
                }
                if( !fHyperLinked ) {
                    _oCacheMan.BeginSelect();
                    _oCacheMan.CacheReColor();
                }
            }

            Invalidate();
        }

        #region IPgTextView
        public TextPosition Caret {
            get {
                CacheMultiColumn.CaretInfo? sCaret = _oCacheMan.CopyCaret();

                if( sCaret is CacheMultiColumn.CaretInfo oCaretValue ) {
                    return new TextPosition( oCaretValue.Row.At, oCaretValue.Offset );
                }

                return new TextPosition( 0, 0 );
            }
        }

        public void ScrollTo(SCROLLPOS eEdge) {
            try {
                switch( eEdge ) {
                    case SCROLLPOS.CARET:
                        _oCacheMan.ScrollToCaret();
                        break;
                    case SCROLLPOS.TOP:
                        _oCacheMan.SetCaretPositionAndScroll( 0, _oCacheMan.CaretColumn, _oDocList.First() );
                        break;
                    case SCROLLPOS.BOTTOM: 
                        _oCacheMan.SetCaretPositionAndScroll( 0, _oCacheMan.CaretColumn, _oDocList.Final() );
                        break; 
                }
            } catch( ArgumentOutOfRangeException ) {
                LogError( "Multi Column List is empty." );
            }
        }

        public bool SelectionSet(int iLine, int iOffset, int iLength) {
            return _oCacheMan.SetCaretPositionAndScroll( iLine, _oCacheMan.CaretColumn, iOffset );
        }

        public void SelectionClear() {
        }
        #endregion
    }
}
