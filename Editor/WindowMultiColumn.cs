using System;
using System.Collections.Generic;
using System.Collections;
using System.Windows.Forms;
using System.Xml;
using System.Diagnostics;
using System.IO;
using System.Drawing;
using System.Collections.ObjectModel;

using SkiaSharp;
using SkiaSharp.Views.Desktop;

using Play.Interfaces.Embedding;
using Play.Rectangles;
using Play.Controls;
using Play.Parse;

namespace Play.Edit {
    public interface IPgDocTraits<T> {
        T           HighLight { get; set; }
        StdUIColors PlayHighlightColor { get; set; }
        bool        IsReadOnly   { get; set; } // Needed? IReadonlyBag vs....

        event Action<T> Event_HighLight; // Only one line is high lighted.
    }

    public static class DocOpExtender {
        public static bool TryReplaceAt( this IPgDocOperations<Row> oDoc, IPgCaretInfo<Row> oCaret, ReadOnlySpan<char> spText ) {
            return oDoc.TryReplaceAt( oCaret.Row, oCaret.Column, oCaret.Offset, oCaret.Length, spText );
        }
        public static bool TryReplaceAt( this IPgDocOperations<Row> oDoc, Row oRow, int iColumn, IMemoryRange oRange, ReadOnlySpan<char> spText ) {
            return oDoc.TryReplaceAt( oRow, iColumn, oRange.Offset, oRange.Length, spText );
        }
    }

    public interface IPgDocOperations<T> {
        void ListenerAdd   ( IPgEditEvents e );
        void ListenerRemove( IPgEditEvents e );

        bool TryReplaceAt( T oRow, int iColumn, int iSrcOff, int iSrcLen, ReadOnlySpan<char> spText );
        bool TryDeleteAt( T oRow, int iColumn, int iSrcOff, int iSrcLen ); // Would like to remove this one...
        bool RowDelete( T oRow );
        void RowDelete( IPgSelection oSelection );
    }

    /// <summary>
    /// An interface for radio box style check marks. In SingleCheck mode we send
    /// a SubmitCheckEvent right away. BUT if in multi check mode you will need to send a
    /// RaiseCheckEvent(). NOTE: You could make an array holding pointers to multiple
    /// columns. IReadableBag\<IPgDocCheckMark\> for example. Then you could have any
    /// number of columns for check marks!! I expect the standard usage to be single column
    /// of check marks.
    /// </summary>
    public interface IPgDocCheckMarks {
        bool   IsSingleCheck { get; set; } // Single check aka radio box...
        int    CheckColumn   { get; set; }
        string CheckSetValue { get; set; }
        string CheckClrValue { get; set; }
        Row    CheckedRow { get; }      // If in single check mode. Return the row.

        void   SetCheckAtRow( Row oRow );

        event Action<Row> Event_Check;
    }

    public interface IPgDocMultiCheck : IReadableBag<IPgDocCheckMarks> {
        /// <summary>
        /// if multi check. null is sent to the RegisterCheckEvent.
        /// </summary>
        void RaiseCheckEvent(); 
    }

    public interface IPgEditEvents {
        public enum EditType {
            Rows,   // rows were added or removed.
            Column  // single column edit.
          //Columns // multiple columns edited. (property page)
        }
        void           OnDocUpdateBegin();
        void           OnDocUpdateEnd  ( EditType eType, Row oRow );
        void           OnDocFormatted  (); 
        void           OnDocLoaded     (); // Give window opportunity to reset caret.
        IPgCaretInfo<Row> Caret2 { get; } // TODO: sort this out.
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
        public uint    UnwrappedWidth { get; protected set; } = 0;
        public bool    IsInvalid { get => false; set { } }

        public uint    FontID { get; set; } = uint.MaxValue;

        public CacheControl( Control oGuest ) {
            Guest = oGuest ?? throw new ArgumentNullException();
        }

        public Point GlyphOffsetToPoint(int iOffset) {
            return new Point( 0, 0 );
        }

        public int GlyphPointToOffset( SKPointI pntWorld) {
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
            if( iWidth < 0 )
                throw new ArgumentOutOfRangeException();

			Size szProposed = new Size( iWidth, (int)MaxHeight );
			Size szPrefered = Guest.GetPreferredSize( szProposed );

            Height         = Math.Min( szPrefered.Height, (int)MaxHeight );
            UnwrappedWidth = (uint)iWidth;
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

    /// <summary>
    /// Might want to add cache info to this struct too... ?
    /// </summary>
    public class ColumnInfo {
        public LayoutRect Bounds { get; protected set; }
        public int        DataIndex { get; }
        public bool       IsReadonly { get; set; } = false;
        public int        LayoutIndex { get; set; } = -1;
        public uint       OriginalTrack { get; }

        // These constructors are inverts of each other get rid of one...
        [Obsolete]public ColumnInfo( LayoutRect rcRect, int iDataColumn ) {
            Bounds        = rcRect ?? throw new ArgumentNullException();
            DataIndex     = iDataColumn; // data row column.
            OriginalTrack = rcRect.Track;
        }
        public ColumnInfo( int iDataColumn, LayoutRect rcRect ) {
            Bounds        = rcRect ?? throw new ArgumentNullException();
            DataIndex     = iDataColumn; // data row column.
            OriginalTrack = rcRect.Track;
        }
    }

    public class WindowMultiColumn :
        SKControl, 
        IPgParent,
        IPgLoad<XmlElement>,
        IPgSave<XmlDocumentFragment>,
        IPgEditEvents,
        IPgTextView,
        IPgCommandBase,
        IEnumerable<ILineRange>
    {
        protected readonly IPgViewSite   _oSiteView;
        protected readonly IPgViewNotify _oViewEvents;

        protected readonly IEnumerable     <Row> _oDocEnum;
        protected readonly IReadableBag    <Row> _oDocList;
        protected readonly IPgDocTraits    <Row> _oDocTraits;
        protected readonly IPgDocOperations<Row> _oDocOps;
        protected readonly IPgDocCheckMarks      _oDocChecks;

        protected readonly CacheMultiBase        _oCacheMan;
		protected readonly IPgStandardUI2        _oStdUI;
        protected readonly ScrollBar2            _oScrollBarVirt;
        protected readonly LayoutStack           _rgLayout;
        protected readonly List<ColumnInfo>      _rgTxtCol = new(); // Might not match document columns! O.o

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
        protected virtual ushort StdFace => StdUI.FaceCacheNew(@"C:\windows\fonts\seguiemj.ttf"); // consola
        protected virtual uint   StdFont => StdUI.FontCacheNew( StdFace, 12, InitializeDPI() );


        public uint CheckColumnWidth {get; } // BUG: Now that flex works, we don't need this...

        public bool IsScrollVisible { get; set; } = true;

        /// <summary>
        /// How much readonly can you get? Window only or doc level. :-/
        /// </summary>
        public bool IsReadOnly { 
            get {
                if( _oDocTraits.IsReadOnly )
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
                    if( _rgErrors.IsUnhandled( oEx ) )
                        throw;

                    LogError( "Tab Order", "Problem with tab list" );
                }

                return null;
            }
            public virtual int TabCount => _oHost._oDocList.ElementCount;

            public virtual Row GetNextTab( Row oRow, int iDir ) {
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

            public ReadOnlyCollection<ColumnInfo> TextColumns => _oHost._rgTxtCol.AsReadOnly();

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
                _oHost._oScrollBarVirt.Refresh( flVisiblePercent, flProgress );
				_oHost.Invalidate();
            }

            /// <summary>
            /// If you hide the caret, that seems to destroy it, so
            /// in that case we just move it off screen. :-/
            /// </summary>
            /// <remarks>Remember the cursor won't show until the
            /// window gets focus. So won't see it while searching.</remarks>
            /// <param name="pntCaret"></param>
            /// <param name="fVisible"></param>
            public virtual void OnCaretPositioned( SKPointI pntCaret, bool fVisible ) {
                if( _oHost.Focused ) {
                    User32.SetCaretPos( pntCaret.X, pntCaret.Y );
                }
                // Else don't mess with the caret! You might blast it when
                // we're not even the current owner (eg. when we're not focused! ^_^;;)
            }

            /// <summary>
            /// Typically we just need one FONT. Whatever face and size for standard,
            /// anything else you'll need more plumbing anyway.
            /// </summary>
            public uint FontStd => _oHost.StdFont;
            public void DoLayout() { _oHost._rgLayout.LayoutChildren(); }
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
            _oDocChecks = (IPgDocCheckMarks     )oDocument;

            _oSiteView   = oViewSite;
            _oViewEvents = oViewSite.EventChain ?? throw new ArgumentException( "Site.EventChain must support IPgViewSiteEvents" );

            _oStdUI         = oViewSite.Host.Services as IPgStandardUI2 ?? throw new ArgumentException( "Parent view must provide IPgStandardUI service" );
            _oScrollBarVirt = new ScrollBar2( new DocSlot( this ) );
            // Oh! The find window is a table. But this object looks like one but is not! O.o
            /// <seealso cref="EditWindow2"/> <seealso cref="Mjolnir.FindWindow"/>
            _rgLayout       = new LayoutStackHorizontal() { Spacing = 7, Units = LayoutRect.CSS.Flex};

            InitializeDPI();

            _oCacheMan = CreateCacheMan();

            // BUG: CacheManager set's the height. Cacheman defines the
            //      glyph but not via the EditMultiColumn.
            CheckColumnWidth = (uint)_oCacheMan.GlyphCheck.Coordinates.advance_x;

            Array.Sort<Keys>( _rgHandledKeys );

            Parent = _oSiteView.Host as Control;
        }

        protected virtual CacheMultiBase CreateCacheMan() {
            //uint uiStdText  = _oStdUI.FontCache( StdFace, 12, GetDPI() );
            return new CacheMultiColumn( new CacheManSite( this ) ); 
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
        /// This method adds the layout to the layout AND the text
        /// columns for caching. Position in the Layout is NOT the
        /// same as the position in the Text columns. Generally b/c
        /// the scroll bar is part of the layout but it is not a text
        /// column.
        /// Cache Columns match the text columns directly.
        /// </summary>
        protected void TextLayoutAdd( LayoutRect oLayout, int iDataColumn ) {
            TextLayoutAdd( new ColumnInfo( oLayout, iDataColumn ));
        }

        protected void TextLayoutAdd( ColumnInfo oInfo ) {
            oInfo.LayoutIndex = _rgLayout.Count;

            _rgLayout.Add( oInfo.Bounds );
            _rgTxtCol.Add( oInfo );
        }
        public void InitColumns( List<ColumnInfo> rgColumns ) {
            foreach( ColumnInfo oInfo in rgColumns ) {
                TextLayoutAdd( oInfo );
            }
        }

        /// <summary>
        /// Launches a browser process on the given url!
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

        /// <summary>This function actually gets the DPI. Then we cache it
        /// so we chug through this all again. But you could call again
        /// if perhaps you're in a weird multi mon situation? :-/
        /// </summary>
        /// <seealso cref="EditWindow2"/>
        private SKPoint InitializeDPI() {
            // The object we get from the interface has some standard screen dpi and size
            // values. We then attempt to override those values with our actual values.
            IPgMainWindow.PgDisplayInfo oInfo = new IPgMainWindow.PgDisplayInfo();
            if( _oSiteView.Host.TopWindow is IPgMainWindow oMainWin ) {
                oInfo = oMainWin.MainDisplayInfo;
            }

            DPI = new SKPoint( oInfo.pntDpi.X, oInfo.pntDpi.Y );

            return DPI;
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
            _oScrollBarVirt.Visible = IsScrollVisible;
            _oScrollBarVirt.Scroll += OnScrollBar; 

            if( IsScrollVisible ) {
                _rgLayout.Add( new LayoutControl( _oScrollBarVirt, 
                                                  LayoutRect.CSS.Pixels, 
                                                  (uint)(DPI.X * _szScrollBars.Width) ) );
            }

            _oDocOps   .ListenerAdd( this );
            _oDocTraits.Event_HighLight += DocTraits_OnHighLight;

            if( this.ContextMenuStrip == null ) {
                ContextMenuStrip oMenu = new ContextMenuStrip();
                oMenu.Items.Add( new ToolStripMenuItem( "Cut",   null, OnCut,   Keys.Control | Keys.X ) );
                oMenu.Items.Add( new ToolStripMenuItem( "Copy",  null, OnCopy,  Keys.Control | Keys.C ) );
                oMenu.Items.Add( new ToolStripMenuItem( "Paste", null, OnPaste, Keys.Control | Keys.V ) );
                this.ContextMenuStrip = oMenu;
            }

            return true;
        }

        private void DocTraits_OnHighLight(Row oRow) {
            if( Focused && oRow != null ) {
                _oCacheMan.SetCaretPositionAndScroll( oRow.At, 0, 0, 0, true );
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

        /// <summary>
        /// First go at providing this functionality. Note that the selection
        /// needs to be cleared BEFORE the TryReplace()!!! This is because 
        /// when the document finished the edit and sends the update to the window
        /// the formatting will remain on screen since that is what was copied to
        /// the cachemanager. DoParse() does eventually cause the formatting to
        /// be updated but that's 2 seconds later... :-/
        /// </summary>
        /// <remarks>I think this code would be cleaner if the caret was a first
        /// class object like the selection. We'll work on that soon.</remarks>
        public void OnCut(object o, EventArgs e ) {
            try {
                CacheMultiColumn.SelectionManager oSelector = _oCacheMan.Selector;
                if( oSelector.RowCount == 1 ) {
                    if( oSelector.IsSingleColumn( out int iColumn ) ) {
                        ClipboardCopyTo();

                        Row          oRow   = _oDocList[_oCacheMan.CaretAt];
                        IMemoryRange oRange = oSelector[iColumn];

                        _oCacheMan.CaretOffset = oRange.Offset;
                        oSelector.Clear(); // Do before TryReplace...

                        _oDocOps.TryReplaceAt( oRow, iColumn, oRange, string.Empty );
                    }
                }
            } catch( Exception oEx ) {
                Type[] rgErrors = { typeof( NullReferenceException ),
                                    typeof( ArgumentOutOfRangeException ),
                                    typeof( IndexOutOfRangeException ),
                                    typeof( ArgumentNullException ) };
                if( rgErrors.IsUnhandled( oEx ) )
                    throw;
            }
        }

        private void OnPaste(object o, EventArgs e ) {
            ClipboardCopyFrom( Clipboard.GetDataObject(), ClipboardOperations.Default );
        }

        public void OnCopy(object o, EventArgs e ) {
            ClipboardCopyTo();
        }

        public void ClipboardCutTo() {
            ClipboardCopyTo();

            SelectionDelete();
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

        public virtual void ClipboardCopyFrom( 
            object oDataSource, 
            Guid   sOperation 
        ) {
            if( IsReadOnly )
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

            if( sOperation == ClipboardOperations.Text ||
                sOperation == ClipboardOperations.Default 
              ) {
                if( !oDataObject.GetDataPresent(typeof(String)) ) {
                    LogError( "Clipboard type not understood" );
                    return;
                }

                string strPaste = oDataObject.GetData(typeof(String)) as string;

                try {
                    CacheMultiColumn.SelectionManager oSelector = _oCacheMan.Selector;
                    if( oSelector.RowCount == 1 ) {
                        if( oSelector.IsSingleColumn( out int iColumn ) ) {
                            Row          oRow   = _oDocList[_oCacheMan.CaretAt];
                            IMemoryRange oRange = oSelector[iColumn];

                            _oCacheMan.CaretOffset = oRange.Offset + 1;
                            oSelector.Clear(); // Do before TryReplace...

                            _oDocOps.TryReplaceAt( oRow, iColumn, oRange, strPaste );
                        }
                    }
                } catch( Exception oEx ) {
                    Type[] rgErrors = { typeof( NullReferenceException ),
                                        typeof( ArgumentOutOfRangeException ),
                                        typeof( IndexOutOfRangeException ),
                                        typeof( ArgumentNullException ) };
                    if( rgErrors.IsUnhandled( oEx ) )
                        throw;
                }
            }
        }

        /// <see cref="IPgEditEvents"/>
        public virtual void OnDocUpdateBegin() {
            _oCacheMan.OnDocUpdateBegin();
        }

        public virtual void OnDocUpdateEnd( IPgEditEvents.EditType eType, Row oRow ) {
            _oCacheMan.OnDocUpdateEnd( eType, oRow );
        }

        public virtual void OnDocFormatted() {
            _oCacheMan.OnDocFormatted();
            Invalidate();
        }

        public virtual void OnDocLoaded() {
            // Note: The file manager overrides this and calls update banner...
            _oCacheMan.OnDocLoaded();
        }

        public IPgCaretInfo<Row> Caret2 => _oCacheMan;

        public class SimpleRange :
            ILineRange 
        {
            public Line Line { get ; set ; }
            public int  At { get; set; }
            public int  ColorIndex => 0;
            public int  Offset { get ; set ; } = 0;
            public int  Length { get ; set ; } = 0;
        }

        /// <summary>
        /// Search the current column that the user is in. 
        /// </summary>
        /// <remarks>
        /// TODO: This is for the system text find dialog. However I notice
        /// I'm not starting at the cursor line but just at the top
        /// of the document. Probably should fix that...
        /// </remarks>
        public virtual IEnumerator<ILineRange> GetEnumerator() {
            SimpleRange oRange = new SimpleRange();

            foreach( Row oRow in _oDocEnum ) {
                if( _oCacheMan.CaretColumn < _rgTxtCol.Count ) {
                    Line oLine = oRow[_oCacheMan.CaretColumn];

                    oRange.Line   = oLine;
                    oRange.Offset = 0;
                    oRange.Length = oLine.ElementCount;
                    oRange.At     = oRow .At;

                    yield return oRange;
                }
            }
        }

        IEnumerator IEnumerable.GetEnumerator() {
            return GetEnumerator();
        }

        protected override void OnSizeChanged( EventArgs e ) {
            base.OnSizeChanged(e);

            // Might need to reconsider this now that the cacheman
            // can resize columns based on the text. Might be a waste
            // of time doing a layoutchildren here..
            _rgLayout.SetRect( 0, 0, Width, Height );
			_rgLayout.LayoutChildren();

            _oCacheMan.OnSizeChange( new SmartRect( 0, 0, Width, Height ) );
        }

        /// <summary>
        /// Basically assuming a vertical layout. Horizontally we just
        /// use the width we are given. 
        /// TODO: Let's try to calc the max width of everything w/o wrapping
        ///       and give that back as the width. b/c we can always use the
        ///       width we were given.
        /// </summary>
        /// <remarks>Would be nice to figure yout some huristic for
        /// balanced text horizontally too. But it's low priority.</remarks>
        public override Size GetPreferredSize( Size sProposed ) {
			_rgLayout.SetRect( 0, 0, sProposed.Width, sProposed.Height );
			_rgLayout.LayoutChildren();

            _oCacheMan.OnSizeChange( _rgLayout );

            sProposed.Height = _oCacheMan.HeightCached;

			return sProposed;
		}
			
        protected override void OnGotFocus(EventArgs e) {
            base.OnGotFocus(e);

            _oViewEvents.NotifyFocused( true );

            if( !IsReadOnly ) {
                User32.CreateCaret( Handle, IntPtr.Zero, 
                                    _oCacheMan.CaretSize.X, 
                                    _oCacheMan.CaretSize.Y );

                // Hiding the caret seems to destroy it so just
                // immediately show it, the cacheman will park it off screen
                // if it's not displayable on an active cache line.
                SKPointI pntCaret = _oCacheMan.CaretLocation;

                User32.SetCaretPos( pntCaret.X, pntCaret.Y );
                User32.ShowCaret  ( Handle );
            }

            Invalidate();
        }
        protected override void OnLostFocus(EventArgs e) {
            base.OnLostFocus( e );

            _oScrollBarVirt.Show( SHOWSTATE.Inactive );
            _oViewEvents   .NotifyFocused( false );

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
                SmartRect rctSpare = SparePaintingRect;
                foreach( CacheRow oCacheRow in _oCacheMan ) {
                    Extent sRowScrExt = _oCacheMan.RenderAt( oCacheRow, _rgLayout );

                    for( int iCacheCol=0; iCacheCol<oCacheRow.CacheColumns.Count; ++iCacheCol ) {
                        if( oCacheRow[iCacheCol] is IPgCacheRender oRender ) {
                            SmartRect rcColumn = _rgTxtCol[iCacheCol].Bounds;

                            rctSpare.SetRect( rcColumn.Left, sRowScrExt.Start, rcColumn.Right, sRowScrExt.Stop );

                            // Test pattern...
                            //skPaint.Color = iCache % 2 == 0 ? SKColors.Blue : SKColors.Green;
                            //skCanvas.DrawRect( rctSquare.SKRect, skPaint );
                            PaintSquareBG( skCanvas, skPaintBG, oCacheRow, iCacheCol, oRender, rctSpare );

                            oRender.Render(skCanvas, _oStdUI, skPaintTx, rctSpare, this.Focused );
                        }
                    }
                }
            } catch( Exception oEx ) {
                // TODO: Add some code to warn on every 100 errors so not so intrusive.
                Type[] rgErrors = { typeof( NullReferenceException ),
                                    typeof( ArgumentOutOfRangeException ),
                                    typeof( ArgumentNullException ),
                                    typeof( ArgumentException ),
                                    typeof( InvalidOperationException ) };
                if( rgErrors.IsUnhandled( oEx ) )
                    throw;
            }
        }

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
        /// <param name="iColumn">The text column in the cache manager. Which
        /// is typically NOT the same as the layout column. Which is not
        /// necessarily the same column in the actual Data row. O.o;; </param>
        protected bool HyperLinkFind( int iColumn, SKPointI pntLocation, bool fDoJump ) {
            try {
                if( _oCacheMan.PointToCache( iColumn, pntLocation, out int iLineOffset ) is CacheRow oCRow ) {
                //if( _oCacheMan.PointToRow( iColumn, pntLocation, out int iOff, out Row oRow ) ) {
                    return HyperLinkFind( oCRow.Row, iColumn, iLineOffset, fDoJump );
                }
            } catch( Exception oEx ) {
                Type[] rgErrors = { typeof( NullReferenceException ),
                                    typeof( ArgumentOutOfRangeException ),
                                    typeof( InvalidOperationException ) };
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

            for( int iTextColumn=0; iTextColumn<_rgTxtCol.Count; ++iTextColumn ) {
                SmartRect rcColumn = _rgTxtCol[iTextColumn].Bounds;
                int       iDataCol = _rgTxtCol[iTextColumn].DataIndex;
                if( rcColumn.IsInside( pntLocation.X, pntLocation.Y ) ) {
                    if( _oDocChecks.CheckColumn == iDataCol ) {
                        oNewCursor = Cursors.Hand;
                    } else {
                        oNewCursor = Cursors.IBeam;
                        if( eButton != MouseButtons.Left &&         // if not selecting.
                            ((ModifierKeys & Keys.Control) == 0 ) ) // if not editing...
                        { 
                            if( HyperLinkFind( iTextColumn, pntLocation, fDoJump:false ) )
                                oNewCursor = Cursors.Hand;
                            break;
                        }
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
                    if(  !IsReadOnly && _oCacheMan.CopyCaret() is CacheMultiColumn.CaretInfo oCaret ) {
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
                        if( !IsReadOnly ) {
                            //_oDocument.Undo();
                        }
                        return true;
                    case Keys.Control | Keys.Q:
                        if( !IsReadOnly ) { // Or column or elem locked...
                            if( _oCacheMan.CopyCaret() is CacheMultiColumn.CaretInfo oCaret ) {
                                _oDocOps.RowDelete( oCaret.Row );
                            }
                        }
                        return true;
                    case Keys.Delete: {
                        // The only way to get this event. Tho' a bit ambiguous between delete a character
                        // in a column or delete a row. 
                        if( !IsReadOnly ) {
                            if( _oCacheMan.Selector.RowCount == 0 ) {
                                if( _oCacheMan.CopyCaret() is CacheMultiColumn.CaretInfo oCaret ) {
                                    _oDocOps.TryDeleteAt(oCaret.Row, oCaret.Column, oCaret.Offset, 1);
                                }
                            } else {
                                // BUG: This won't delete multi char's in one row w/ one column selection.
                                SelectionDelete();
                            }
                        }
                        return true;
                    }
                }
            } 

            return base.ProcessCmdKey( ref msg, keyData );
        } // end method

        /// <summary>
        /// If you put the visible columns in a different order than the
        /// data, well then you've got to look 'em up.
        /// </summary>
        public bool IsCaretInCheckColumn {
            get {
                if( _oDocChecks.CheckColumn < 0 )
                    return false;
                if( _oCacheMan.CaretColumn < 0 )
                    return false;

                foreach( ColumnInfo oCol in _rgTxtCol ) {
                    if( oCol.DataIndex == _oCacheMan.CaretColumn )
                        return true;
                }
                return false;
            }
        }

        protected override void OnKeyPress(KeyPressEventArgs e) {
            if( IsDisposed )
                return;
            if( _oViewEvents.IsCommandPress( e.KeyChar ) )
                return;
            if( IsReadOnly )
                return;

            try {
                if( IsCaretInCheckColumn ) {
                    if( e.KeyChar == ' ' ) { // space bar.
                        Row oRow = _oDocList[_oCacheMan.CaretAt];
                        _oDocChecks.SetCheckAtRow( oRow ); // sends a check event if check moves.
                    }
                    e.Handled = true;
                    return;
                }
                if( !char.IsControl( e.KeyChar ) ) {
                    _oCacheMan.ScrollToCaret();

                    ReadOnlySpan<char>   rgInsert  = stackalloc char[1] { e.KeyChar };
                    CacheMultiColumn.
                        SelectionManager oSelector = _oCacheMan.Selector;
                    Row                  oRow      = _oDocList[_oCacheMan.CaretAt];

                    // TODO: I might be able to improve this by making it so I can use
                    // the selection at all times...
                    switch( oSelector.RowCount ) {
                        case 0:
                            _oDocOps.TryReplaceAt( oRow, 
                                                   _oCacheMan.CaretColumn, 
                                                   _oCacheMan.CaretOffset,
                                                   0,
                                                   rgInsert );
                            break;
                        case 1:
                            if( oSelector.IsSingleColumn( out int iColumn ) ) {
                                IMemoryRange oRange = oSelector[iColumn];

                                oSelector.Clear(); // Do before the TryReplace...
                                _oCacheMan.CaretOffset = oRange.Offset+1; // Want caret after ins text
                                _oDocOps.TryReplaceAt( oRow, iColumn, oRange, rgInsert );
                            }
                            break;
                        default: {
                            SelectionDelete();
                            _oDocOps.TryReplaceAt( oRow, 
                                                   _oCacheMan.CaretColumn, 
                                                   _oCacheMan.CaretOffset,
                                                   0,
                                                   rgInsert );
                            } break;
                    }

                    e.Handled = true;
                }
            } catch( Exception oEx ) {
                Type[] rgErrors = { typeof( NullReferenceException ),
                                    typeof( ArgumentOutOfRangeException ),
                                    typeof( IndexOutOfRangeException ),
                                    typeof( ArgumentNullException ) };
                if( rgErrors.IsUnhandled( oEx ) )
                    throw;
                LogError( "Caret is probably confused" );
            }
        }

        public static bool IsCtrl( Keys sKey ) {
            return (ModifierKeys & Keys.Control) != 0;
        }

        protected override void OnMouseDoubleClick( MouseEventArgs e ) {
            base.OnMouseDoubleClick(e);

            Select();
            Focus ();
            SKPointI pntClick = new SKPointI( e.X, e.Y );

            // Move the caret and reset the Advance.
            _oCacheMan.CaretAdvance( pntClick );

            try {
                // See if want to double click select a word.
                if( _oCacheMan.IsInside( pntClick, out int iTextColumn ) ) {
                    CacheMultiColumn.CaretInfo? sCaret = _oCacheMan.CopyCaret();

                    if( sCaret is CacheMultiColumn.CaretInfo oCaret ) {
                        int iDataCol = _rgTxtCol[iTextColumn].DataIndex;
                        if( oCaret.Row[iDataCol].FindFormattingUnderRange( oCaret ) is IMemoryRange oRange ) {
                            _oCacheMan.Selector.SetWord( oCaret, oRange );
                            _oCacheMan.ReColor();
                            Invalidate();
                        }
                    }
                }
            } catch( Exception oEx ) {
                Type[] rgErrors = { typeof( NullReferenceException ),
                                    typeof( ArgumentOutOfRangeException ),
                                    typeof( IndexOutOfRangeException ),
                                    typeof( ArgumentNullException ) };
                if( rgErrors.IsUnhandled( oEx ) )
                    throw;
            }
        }

        protected override void OnMouseUp(MouseEventArgs e) {
            base.OnMouseUp(e);

            _oCacheMan.EndSelect();
        }

        protected override void OnMouseDown(MouseEventArgs e) {
            base.OnMouseDown( e );

            Select();
            Focus ();
            SKPointI pntClick = new SKPointI( e.X, e.Y );
            bool     fInside  = _oCacheMan.IsInside( pntClick, out int iTextColumn );

            if( fInside ) {
                int iDataCol = _rgTxtCol[iTextColumn].DataIndex;
                if( iDataCol == _oDocChecks.CheckColumn ) {
                    if( _oCacheMan.PointToCache( iDataCol, pntClick, out int iLineOffset ) is CacheRow oCRow ) {
                  //if( _oCacheMan.PointToRow( iColumnM, pntClick, out int iOff, out Row oRow ) ) {
                        _oDocChecks.SetCheckAtRow( oCRow.Row ); // sends a check event if check moves.
                    }
                    return;
                }
            }

            // Move the caret and reset the Advance.
            _oCacheMan.CaretAdvance( pntClick );

            // Need to move this to mouse up, so I can detect a drag...
            if( e.Button == MouseButtons.Left && !IsCtrl( ModifierKeys ) ) {
                if( fInside ) {
                    int iDataCol = _rgTxtCol[iTextColumn].DataIndex;
                    if( !HyperLinkFind( iDataCol, pntClick, fDoJump:true ) ) {
                        _oCacheMan.BeginSelect();
                        _oCacheMan.ReColor();
                    }
                    Invalidate();
                }
            }
        }

        protected override void OnMouseMove(MouseEventArgs e) {
            base.OnMouseMove( e );

            SKPointI pntMouse = new SKPointI( e.X, e.Y );

            CursorUpdate( pntMouse, e.Button );

            if( _oCacheMan.IsSelecting ) {
                _oCacheMan.CaretAdvance(pntMouse);
                _oCacheMan.ReColor();
                Invalidate();
            }
        }

        #region IPgTextView
        /// <see cref="IPgTextView" />
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
                        _oCacheMan.SetCaretPositionAndScroll( 0, _oCacheMan.CaretColumn, _oDocList.First(), 0 );
                        break;
                    case SCROLLPOS.BOTTOM: 
                        _oCacheMan.SetCaretPositionAndScroll( 0, _oCacheMan.CaretColumn, _oDocList.Final(), 0 );
                        break; 
                }
            } catch( ArgumentOutOfRangeException ) {
                LogError( "Multi Column List is empty." );
            }
        }

        public bool SelectionSet(int iLine, int iColumn, int iOffset, int iLength) {
            return _oCacheMan.SetCaretPositionAndScroll( iLine, iColumn, iOffset, iLength );
        }

        public void SelectionClear() {
            _oCacheMan.Selector.Clear();
            _oCacheMan.ReColor();
            // BUG: Need to correct the Caret offset. It will be
            //      in the same column it was (for stream select)
        }

        public void SelectionDelete() {
            _oDocOps.RowDelete( _oCacheMan.Selection );
            SelectionClear();
        }

        #endregion

        public virtual bool Execute( Guid gCommand ) {
            if( gCommand == GlobalCommands.Copy ) {
                ClipboardCopyTo();
                return true;
            }
            if( gCommand == GlobalCommands.Paste ) {
                //ClipboardCopyFrom();
                return true;
            }
            if( gCommand == GlobalCommands.Delete ) {
                SelectionDelete();
                return true;
            }

            return false;
        }
    }
}
