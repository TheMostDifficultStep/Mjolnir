using System;
using System.Collections.Generic;
using System.Collections;
using System.Windows.Forms;
using System.Xml;
using System.Drawing;

using SkiaSharp;
using SkiaSharp.Views.Desktop;

using Play.Interfaces.Embedding;
using Play.Rectangles;
using Play.Controls;
using static Play.Edit.EditWindow2; // gives us access to static function... :-P
using Play.Parse;

namespace Play.Edit {
    public interface IPgDocTraits<T> {
        T HighLight { get; set; }
        StdUIColors PlayHighlightColor { get; set; }
        bool        ReadOnly { get; set; }

        event Action<T> HighLightChanged; // Only one line is high lighted.
        event Action<T> CheckedEvent;     // Any number of rows can be checked.
    }
    public class WindowMultiColumn :
        SKControl, 
        IPgParent,
        IPgLoad<XmlElement>,
        IPgSave<XmlDocumentFragment>,
        IPgRowEvents,
        IEnumerable<ILineRange>
    {
        public static Guid _sGuid = new Guid( "{03F21BC8-F911-4FE4-931D-9EB9F7A15A10}" );

        protected readonly IPgViewSite   _oSiteView;
        protected readonly IPgViewNotify _oViewEvents;

        protected readonly IEnumerable<Row>      _oDocEnum;
        protected readonly IReadableBag<Row>     _oDocList;
        protected readonly IPgDocTraits<Row>     _oDocTraits;
        protected readonly CacheMultiColumn      _oCacheMan;
        protected readonly LayoutStackHorizontal _rgLayout;
		protected readonly IPgStandardUI2        _oStdUI;
        protected readonly ScrollBar2            _oScrollBarVirt;
        protected readonly List<SmartRect>       _rgColumns = new(); // Might not match document columns! O.o

        protected bool _fReadOnly = false;
        protected Dictionary<string, Action<Row, int, IPgWordRange>> HyperLinks { get; } = new ();

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

        public class CacheManSite :
            ICacheManSite
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

            /// <summary>
            /// Tiny safety check.
            /// </summary>
            int CheckBound( int iRow ) {
                if( iRow >= _oHost._oDocList.ElementCount )
                    iRow = _oHost._oDocList.ElementCount - 1;

                if( iRow < 0 ) // If elem count zero, above will give -1;
                    iRow = 0;

                return iRow;
            }

            public Row GetRowAtScroll() {
                if( _oHost._oDocList.ElementCount == 0 )
                    return null;

                try {
					int iRow = (int)(_oHost._oDocList.ElementCount * _oHost._oScrollBarVirt.Progress);

					return _oHost._oDocList[ CheckBound( iRow ) ];
                } catch( Exception oEx ) {
                    if( _rgErrors.IsUnhandled( oEx ) )
                        throw;

					LogError( "Multi Column Scrolling Problem", "I crashed while trying to use the caret. You are at the start of the document." );
                }

                return null;
            }

            public Row GetRowAtIndex(int iIndex) {
                if( iIndex >= _oHost._oDocList.ElementCount ) {
                    return null;
                }
                if( iIndex < 0 ) {
                    return null;
                }

                return _oHost._oDocList[iIndex];
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

            public void OnRefreshComplete( Row oRowBottom, int iRowCount ) {
				//_oHost.CaretIconRefreshLocation();

                try {
                    if( oRowBottom != null ) {
                        _oHost._oScrollBarVirt.Refresh( 
                            iRowCount     / (float)_oHost._oDocList.ElementCount,
                            oRowBottom.At / (float)_oHost._oDocList.ElementCount 
                        );
                    }
                } catch( Exception oEx ) {
                    Type[] rgErrors = { typeof( NullReferenceException ),
                                        typeof( ArithmeticException ) };
                    if( rgErrors.IsUnhandled( oEx ) )
                        throw;

                    _oHost.LogError( "Problem Updating Window Scrollbar." );
                }
				_oHost.Invalidate();
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
            _oDocEnum   = (IEnumerable <Row>)oDocument;
            _oDocList   = (IReadableBag<Row>)oDocument;
            _oDocTraits = (IPgDocTraits<Row>)oDocument;

            _oSiteView   = oViewSite;
            _oViewEvents = oViewSite.EventChain ?? throw new ArgumentException( "Site.EventChain must support IPgViewSiteEvents" );

            _oStdUI         = oViewSite.Host.Services as IPgStandardUI2 ?? throw new ArgumentException( "Parent view must provide IPgStandardUI service" );
            uint uiStdText  = _oStdUI.FontCache( _oStdUI.FaceCache( @"C:\windows\fonts\consola.ttf" ), 12, GetDPI() );
            _oScrollBarVirt = new ScrollBar2( new DocSlot( this ) );
            _rgLayout       = new LayoutStackHorizontal() { Spacing = 5, Units = LayoutRect.CSS.Flex};

            _rgLayout.Add( new LayoutControl( _oScrollBarVirt, LayoutRect.CSS.Pixels, 12 ) );
            _rgLayout.Add( new LayoutRect( LayoutRect.CSS.Percent, 20, 1L ) );
            _rgLayout.Add( new LayoutRect( LayoutRect.CSS.Percent, 30, 1L ) );
            _rgLayout.Add( new LayoutRect( LayoutRect.CSS.None ) );

            // Need to figure out how to match the columns of the Window vs Document...
            // TODO: I'm going to add the columns to the cache site so I can init the later instead of now.
            _rgColumns.Add( _rgLayout.Item( 1 ) );
            _rgColumns.Add( _rgLayout.Item( 2 ) );
            _rgColumns.Add( _rgLayout.Item( 3 ) );

            _oCacheMan = new CacheMultiColumn( new CacheManSite( this ), 
                                               _oStdUI.FontRendererAt( uiStdText ),
                                               _rgColumns ); 
        }

        public bool  IsDirty => true;
        protected override void Dispose( bool disposing ) {
            if( disposing ) {
                //_oDocument.CaretRemove( CaretPos );
                _oScrollBarVirt.Scroll -= OnScrollBar; 
                HyperLinks.Clear();
            }

            base.Dispose(disposing);
        }

        protected void LogError( string strMessage, bool fShow = false ) {
            _oSiteView.LogError( "Multi Column Window", strMessage, fShow );
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

        public IPgParent Parentage => _oSiteView.Host;

        public IPgParent Services => Parentage.Services;

        /// <summary>
        /// Where we really initialize.
        /// </summary>
        /// <seealso cref="InitNew"/>
        /// <seealso cref="Load"/>
        protected virtual bool Initialize() {
            if( _oSiteView.Host is Control oParent ) {
                this.Parent = oParent;
            }

            _oScrollBarVirt.Parent  = this;
            _oScrollBarVirt.Visible = true;
            _oScrollBarVirt.Scroll += OnScrollBar; 

            return true;
        }

        public bool InitNew() {
            if( !Initialize() )
                return false;

            return true;
        }

        public bool Load(XmlElement oStream) {
            if( !Initialize() )
                return false;

            return true;
        }

        public bool Save(XmlDocumentFragment oStream) {
            return true;
        }

        public void OnRowEvent(BUFFEREVENTS eEvent, Row oRow) {
            _oCacheMan.UpdateRow( oRow ); // Actually, only need to invalidate... hmmm.
            _oCacheMan.CacheResetFromThumb();
        }

        public void OnRowEvent(BUFFEREVENTS eEvent) {
            _oCacheMan.CacheResetFromThumb();
        }

        public class SimpleRange :
            ILineRange {
            public Line Line { get ; set ; }
            public int  At { get; set; }
            public int  ColorIndex => 0;
            public int  Offset { get ; set ; } = 0;
            public int  Length { get ; set ; } = 0;
        }

        public IEnumerator<ILineRange> GetEnumerator() {
            SimpleRange oRange = new SimpleRange();

            foreach( Row oRow in _oDocEnum ) {
                foreach( Line oLine in oRow ) {
                    oRange.Line   = oLine;
                    oRange.Offset = 0;
                    oRange.Length = oLine.ElementCount;
                    oRange.At     = oLine.At;

                    yield return oRange;
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

            Invalidate();
        }
        protected override void OnLostFocus(EventArgs e) {
            base.OnLostFocus( e );

            _oScrollBarVirt.Show( SHOWSTATE.Inactive );

            _oViewEvents.NotifyFocused( false );

            Invalidate();
        }

        /// <summary>
        /// Paint BG depending on various highlight modes for the row/column.
        /// </summary>
        protected void PaintSquareBG( 
            SKCanvas  skCanvas, 
            SKPaint   skPaint, 
            CacheRow  oCRow, 
            int       iColumn,
            SmartRect rctCRow 
        ) {
            StdUIColors eBg = StdUIColors.Max;

            try {
                if( iColumn == 1 )
                    eBg = StdUIColors.BGReadOnly;
                if( _oCacheMan.CaretRow.At == oCRow.At )
                    eBg = StdUIColors.BGWithCursor;
                if( _oDocTraits.HighLight != null && oCRow.At == _oDocTraits.HighLight.At)
                    eBg = _oDocTraits.PlayHighlightColor;

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

        /// <remarks>
        /// Used to do a simple refresh before painting, but no longer... O.o
        /// </remarks>
        protected override void OnPaintSurface(SKPaintSurfaceEventArgs e) {
            base.OnPaintSurface(e);

            try {
                SKSurface skSurface = e.Surface;
                SKCanvas  skCanvas  = skSurface.Canvas;

                using SKPaint skPaintBG = new SKPaint() {
                    BlendMode = SKBlendMode.Src,
                    Color     = _oStdUI.ColorsStandardAt(_fReadOnly ? StdUIColors.BGReadOnly : StdUIColors.BG)
                };
                using SKPaint skPaintTx = new SKPaint() { FilterQuality = SKFilterQuality.High };

                // Paint all window background. Note: We could get by without this if
                // there was no space between lines/columns.
                skCanvas.DrawRect( new SKRect( 0, 0, Width, Height ), skPaintBG);

                // Now paint the rows.
                SmartRect rctSquare = new();
                foreach( CacheRow oCacheRow in _oCacheMan ) {
                    for( int iCacheCol=0; iCacheCol<oCacheRow.CacheList.Count; ++iCacheCol ) {
                        FTCacheLine oCache  = oCacheRow.CacheList[iCacheCol];
                        SmartRect   oColumn = _rgColumns[iCacheCol];

                        rctSquare.SetRect( oColumn.Left, oCacheRow.Top, oColumn.Right, oCacheRow.Bottom );

                        // Test pattern...
                        //skPaint.Color = iCache % 2 == 0 ? SKColors.Blue : SKColors.Green;
                        //skCanvas.DrawRect( rctSquare.SKRect, skPaint );
                        PaintSquareBG( skCanvas, skPaintBG, oCacheRow, iCacheCol, rctSquare );

                        oCache.Render(skCanvas, _oStdUI, skPaintTx, rctSquare, this.Focused );
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

        protected bool HyperLinkFind( int iColumn, SKPointI pntLocation, bool fDoJump ) {
            try {
                if( _oCacheMan.PointToRange( iColumn, pntLocation, out int iOff, out int iRow ) ) {
                    return HyperLinkFind( _oDocList[iRow], iColumn, iOff, fDoJump );
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
        /// Update the caret. Right now we're written for browser mode.
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
                    if( eButton != MouseButtons.Left ) { // if not selecting.
                        if( HyperLinkFind( iColumn, pntLocation, fDoJump:false ) )
                            oNewCursor = Cursors.Hand;
                        break;
                    }
                }
            }

            Cursor = oNewCursor;
        }

        protected override void OnMouseMove(MouseEventArgs e) {
            base.OnMouseMove( e );

            CursorUpdate( new SKPointI( e.X, e.Y ), e.Button );
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
                    break;
                case Keys.Up:
                    break;
            }
        }

        protected override void OnMouseDown(MouseEventArgs e) {
            base.OnMouseDown( e );

            Select();

            Invalidate();
        }
    }
}
