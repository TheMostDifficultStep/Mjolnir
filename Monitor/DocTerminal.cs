using Play.Edit;
using Play.Interfaces.Embedding;
using Play.Rectangles;

using SkiaSharp;
using SkiaSharp.Views.Desktop;

using System.Collections;
using System.Drawing;
using System.Security;
using System.Text;
using System.Windows.Forms;
using System.Xml;

namespace Monitor {
    public class TerminalDocument :
        IReadableBag<Line>,
        IPgLoad
    {
        public Queue<byte> Buffer { get; } = new Queue<byte>();

        protected     Line[] _rgLines = new Line[24];
        public Line? this[int iIndex] => _rgLines[iIndex];

        public int       ElementCount => _rgLines.Length;

        public Line CaretLine   { get; protected set; }
        public int  CaretOffset { get; protected set; }

        public string Moniker {get; set; } = string.Empty;
        protected Encoding _oEncoding = new UTF8Encoding( false, true ); // Don't emit BOM, tho I think it ignores me anyway.

        public bool   IsDirty => true;

        public event Action<Line?>? LineEvent;

        protected IPgBaseSite _oSite;

        public TerminalDocument( IPgBaseSite oSite ) {
            _oSite = oSite?? throw new ArgumentNullException(nameof(oSite));

            int iSum = 0;
            for( int i=0; i<_rgLines.Length; ++i ) {
                _rgLines[i] = new TextLine( i, string.Empty );
                iSum = _rgLines[i].Summate( i, iSum );
            }

            CaretLine   = _rgLines[0];
            CaretOffset = 0;
        }

        public void Clear() {
            int iSum = 0;
            for( int i=0; i<_rgLines.Length; ++i ) {
                _rgLines[i].Empty();
                _rgLines[i].Formatting.Clear();
                iSum = _rgLines[i].Summate( i, iSum );
            }
            LineEvent?.Invoke( null );
        }
        public bool InitNew() {
            return true;
        }

        /// <summary>
        /// Appends a character at the end of the current last line.
        /// </summary>
        public void AppendChar( char cChar ) {
            if( cChar == '\r' ) {
                if( CaretLine.At == 23 ) {
                    Line oBottom = _rgLines[0];
                    int  iSum    = 0;

                    for( int i=0; i<_rgLines.Length-1; ++i ) {
                        _rgLines[i] = _rgLines[i+1];
                        iSum = _rgLines[i].Summate( i, iSum );
                    }

                    _rgLines[_rgLines.Length-1] = oBottom;
                    oBottom.Empty();
                    oBottom.Summate( _rgLines.Length-1, iSum );

                    CaretLine   = oBottom;
                    CaretOffset = 0;
                    LineEvent?.Invoke( null );
                } else {
                    CaretLine   = _rgLines[CaretLine.At + 1];
                    CaretOffset = 0;
                    LineEvent?.Invoke( CaretLine );
                }
                return;
            }
            if( cChar == '\n' )
                return;

            if( cChar == (char)Keys.Back ) {
                if( CaretLine.TryDelete( CaretOffset-1, 1, out _ ) ) {
                    CaretOffset--;
                    LineEvent?.Invoke( CaretLine ); 
                }
                return;
            }

            CaretLine.TryInsert( CaretOffset++, cChar );
            
            LineEvent?.Invoke( CaretLine ); 
        }

        void LogError( string strMessage ) {
            _oSite.LogError( "Terminal", strMessage );
        }

        public static readonly Type[] _rgFileErrors = { 
			typeof( ArgumentNullException ),
			typeof( ArgumentException ),
			typeof( NullReferenceException ),
			typeof( DirectoryNotFoundException ),
			typeof( IOException ),
			typeof( UnauthorizedAccessException ),
			typeof( PathTooLongException ),
			typeof( SecurityException ),
            typeof( InvalidOperationException ),
            typeof( NotSupportedException ),
            typeof( FileNotFoundException ) };

        /// <seealso cref="CheckLocation(bool)" />
        public bool Save( string strPath ) {
            bool fSaved = false;

            try {
                // Note: By default StreamWriter closes a stream when provided. Newer versions of .net provide leaveOpen flag.
                //       Let's just use streamwriter with filename direcly since we're not dealing with binary objects yet. 
                using( StreamWriter oWriter = new StreamWriter( strPath, false, _oEncoding ) ) {
                    fSaved = Save( oWriter );
                    oWriter.WriteLine();

					oWriter.Flush();
                }
            } catch( Exception oEx ) {
				if( _rgFileErrors.IsUnhandled( oEx ) )
					throw;

                LogError( "Couldn't save Terminal Screen" );

                return false;
            }

            // If I don't call this, then the session (if using) doesn't
            // wipe the astrisk off of the title. 
            return true;
        }

        /// <summary>
        /// Check if we either have a proper filename, or go out
        /// and get one.
        /// </summary>
        /// <remarks>To put this on the base or not? Or on a view?
        /// I think it makes since here since the file name is
        /// a singleton. And on this subclass since I'm not sure
        /// I should be allowing "side" saves in the general case?
        /// </remarks>
        public string? CheckLocation( bool fNewLocation ) {
            string? strLastPath = string.Empty;

            // If we've got a filename try that path first. 
            if( string.IsNullOrEmpty( Moniker ) || 
                string.IsNullOrEmpty( Path.GetFileNameWithoutExtension( Moniker ) ) )
                fNewLocation = true;
            else
                strLastPath = Path.GetDirectoryName( Moniker );

            if( fNewLocation == true ) {
                SaveFileDialog oDialog = new() {
                    InitialDirectory = strLastPath
                };
                oDialog.ShowDialog();

                if(  oDialog.FileName        == null || 
                     oDialog.FileName.Length == 0    || 
                    !oDialog.CheckPathExists ) 
                {
                    LogError( "Please supply a valid file name for your next Save request. ^_^;" );
                    return null;
                }

                Moniker = oDialog.FileName;
                return Moniker;
            }

            return strLastPath;
        }

        public bool Save( TextWriter oWriter ) {
            LogError( "Terminal screen save not implemented yet" );
            return false;
        }
    }
    public interface ITCSite : IPgBaseSite {
        public IReadableBag<Line>          Lines { get; }
        public ICollection<ILineSelection> Selections{ get; }
        public void OnRefreshComplete();
    }

    public class TerminalCache :         
        IEnumerable<CacheRowSingle>
    {
        protected readonly ITCSite _oSite;
        // World coordinates of our view port. Do not confuse these with the
        // layout columns, those are different.
        readonly SmartRect                  _oTextRect  = new ();
        protected List<CacheRowSingle>      _rgOldCache = new ();
        protected readonly List<SmartRect>  _rgCacheMap = [];
        protected IReadableBag<Line>        _rgDocLines;
        protected bool                      _fIsWrapped;

        protected IPgFontRender Font       { get; }
        public    int           LineHeight { get; } // Helps us determine scrolling distances.

        // TODO: Get the font from the site instead of from the constructor? Maybe?
        /// <remarks>Need to sort out the LineHeight accessor since the cache elements might be
        /// variable height, Need to make sure I'm using this correctly. Consider calling it
        /// "LineScroll"</remarks>
        public TerminalCache( ITCSite oSite, IPgFontRender oFont, bool fIsWrapped ) :
			base() 
		{
			Font        = oFont     ?? throw new ArgumentNullException( "Need a font to get things rolling." );
			_oSite      = oSite     ?? throw new ArgumentNullException( "Cache manager is a sited object.");
            _rgDocLines = oSite.Lines;
            _fIsWrapped = fIsWrapped;

            LineHeight = (int)Font.LineHeight; // BUG: Cache elem's are variable height in general.

            // Always a fixed number of lines. Stick with 40 for the moment.
            for( int i=0; i<_rgDocLines.ElementCount; ++i ) {
                CacheRowSingle? oNew = CreateRow( _rgDocLines[ i ] );
                if( oNew is null ) {
                    throw new InvalidProgramException();
                }
                _rgOldCache.Add( oNew );
            }
            // Figure out a standard width...
            _rgCacheMap.Add( _oTextRect ); 
        }

        public IEnumerator<CacheRowSingle> GetEnumerator() {
            return _rgOldCache.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() {
            return _rgOldCache.GetEnumerator();
        }

        protected void LogError( string strDetails ) {
            _oSite.LogError( "Text Manager", strDetails );
        }

        /// <summary>
        /// Going to have to be ultra careful before enabling this. Up to now we've assumed 1 space
        /// between lines and line height given by the font.
        /// </summary>
        public int LineSpacing { get; set; } = 1;

        /// <summary>
        /// Count of number of CacheRow objects inside the manager.
        /// </summary>
        public int Count {
            get { return _rgOldCache.Count; }
        }

        public SmartRect TextRect {
            get { return _oTextRect; }
        }

        /// <summary>
        /// Find new gaps and create new cache elements for them.
        /// Note: Can't search for the line with the binary search since we're thrashing the order of the cache.
        /// </summary>
        /// <returns>True if the cache has any elements.</returns>
        protected bool CacheRefresh( ) {
            // Build downwards towards larger line numbers.
            int iTop = 0;
            foreach( CacheRowSingle oRow in _rgOldCache ) {
                oRow.Top = iTop;
                RowUpdate( oRow );
                iTop += oRow.Height;
            };

            return _rgOldCache.Count != 0;
        } // end method

        /// <summary>
        /// Remeasure the text, get all the colors, and finally
        /// calculate each element height based on the provided width.
        /// </summary>
        /// <remarks>Note that the CacheList length MIGHT be 
        /// less than the CacheMap length! NOTE: If one of the elements throw it
        /// messes us up for the whole row. :-/ </remarks>
        /// <seealso cref="CheckList"/>
        protected virtual void RowUpdate( CacheRow oRow ) {
			try {
                IPgCacheMeasures oElem = oRow.CacheColumns[0];

				oElem.Measure     ( Font );
                oElem.Colorize    ( _oSite.Selections );
                oElem.OnChangeSize( _rgCacheMap[0].Width  );
			} catch( Exception oEx ) {
				Type[] rgErrors = { typeof( NullReferenceException ),
									typeof( ArgumentNullException ),
                                    typeof( ArgumentOutOfRangeException ) };
				if( rgErrors.IsUnhandled( oEx ) )
					throw;

                _oSite.LogError( "view cache", "Error on row update request. Row: " + oRow.At.ToString() );
			}
        }

        /// <summary>Invalidate ALL window cash elements.</summary>
        /// <remarks>
        /// Sort of odd. I found out that on the OnMultiFinished is not marking the lines
        /// as invalid, even tho the implication is that everthing is updated. Probably
        /// never noticed b/c the line change events would invalidate lines, but for
        /// the new BBC basic line renumber, none of that is used. (at present 7/9/2023)
        /// </remarks>
        /// <seealso cref="EditWindow2.OnMultiFinished"/>
        public void Invalidate() {
            foreach( CacheRow oRow in this ) {
                foreach( FTCacheLine oElem in oRow.CacheColumns ) {
                    oElem.IsInvalid = true;
                }
            }
        }

        /// <summary>
        /// Call this function when the user has scrolled or resized the screen.
        /// Or lines got inserted or deleted from the document. 
        /// Note: At present, we don't really know when a cut or paste has occured. This is kind of a drag.
        /// </summary>
        public void Refresh() {
            CacheRefresh();

            _oSite.OnRefreshComplete();
        } // end method

        /// <summary>
        /// Create a cached line element. There are a lot of dependencies on stuff in this object
        /// and so we create the element here and pass it out to be used.
        /// </summary>
        /// <param name="oLine">The line we are trying to display.</param>
        /// <returns>A new row enough information to display the line on the screen.</returns>
        /// <remarks> Be sure to call RowUpdate()
        /// after this call so that the lines can be measured.</remarks>
        /// <seealso cref="RowUpdate"/>
        protected virtual CacheRowSingle? CreateRow( Line? oLine ) {
            if( oLine is null ) {
                _oSite.LogError( "view cache", "Guest line must not be null for screen cache element." );
                return null;
            }

            CacheRowSingle oRow = new CacheRowSingle( oLine );

            FTCacheLine oElem;

			if( _fIsWrapped ) {
				oElem = new FTCacheWrap( oLine ); // Heavy duty guy.
			} else {
				oElem = new FTCacheLine( oLine ); // Simpler object.
			}

            oRow.CacheColumns.Add( oElem );

            return oRow;
        }

        public struct GraphemeCollection : IEnumerable<IPgGlyph> {
            FTCacheLine _oCache;
            PgCluster   _oCluster;
            public GraphemeCollection( FTCacheLine oCache, PgCluster oCluster ) {
                _oCache   = oCache   ?? throw new ArgumentNullException();
                _oCluster = oCluster ?? throw new ArgumentNullException();
            }

            public IEnumerator<IPgGlyph> GetEnumerator() {
                return _oCache.ClusterCharacters( _oCluster );
            }

            IEnumerator IEnumerable.GetEnumerator() {
                return GetEnumerator();
            }
        }

        public static IPgGlyph[] _rgEmptyGlyph = new IPgGlyph[0];

        /// <summary>List all the codepoints that make up this character.</summary>
        /// <remarks>In this brave new world. A single grapheme can be made up of
        /// many codepoints! This brings up the issue of editing these multi point
        /// grapheme's but I'll side step that for the moment.</remarks>
        /// <exception cref="ArgumentException" />
        public IEnumerable<IPgGlyph> EnumGrapheme( ILineRange oCaret ) {
            if( oCaret == null || oCaret.Line == null )
                throw new ArgumentException( "Caret or Line on Caret is empty" );

            CacheRow oRow = CacheLocate( oCaret.At );

            if( oRow != null && oRow[0] is FTCacheLine oCache ) {
                PgCluster oCluster = oCache.ClusterAt( oCaret.Offset );

                if( oCluster != null )
                    return new GraphemeCollection( oCache, oCluster );
            }

            return _rgEmptyGlyph;
        }

        /// <summary>
        /// We used to simply call oCache.Update(), however, word wrapping doesn't work
        /// unless we call the resize too. So call RowUpdate for completeness.
        /// </summary>
        /// <remarks>Note: We just update and don't check if any of the elements are Invalid.</remarks>
        /// <seealso cref="RowUpdate"/>
        public void OnLineUpdated( Line oLine ) {
            foreach( CacheRow oCacheRow in _rgOldCache ) {
                if( oCacheRow.Line == oLine ) {
                    RowUpdate( oCacheRow );
                }
            }
        }

        ///<summary>When formatting changes, that's typically because of a text change.</summary>
        ///<remarks>In CacheRefresh we get the Selections from the CacheMan Site. But here
        ///         we require it as a parameter. Need to think about that.
        ///         Currently updating the entire cache, only the line who's text has
        ///         changed, needs the "update"</remarks> 
        public void OnChangeFormatting( ICollection<ILineSelection> rgSelection, int iWidth ) {
            foreach( CacheRow oRow in _rgOldCache ) {
                IPgCacheMeasures oCache = oRow.CacheColumns[0];
              //oCache.Update( Font ); Just can't call this here. Too slow.
                oCache.Colorize( rgSelection );
                oCache.OnChangeSize( iWidth );
            }
        }

        /// <remarks>
        /// Not again we're only updating the text area formatting.
        /// </remarks>
        public void OnChangeSelection( ICollection<ILineSelection> rgSelection ) {
            foreach( CacheRow oRow in _rgOldCache ) {
                oRow.CacheColumns[0].Colorize( rgSelection );
            }
        }

        /// <summary>
        /// Find the requested line.
        /// </summary>
        /// <param name="iLine">Line identifier. Technically for use, this does not need to be an
        /// array index. But just a unique value sitting at the "At" property on the line.</param>
        /// <returns>The cache element representing that line.</returns>
        public CacheRow CacheLocate( int iLine ) {
            foreach( CacheRow oCache in _rgOldCache ) {
                if( oCache.At == iLine ) {
                    return( oCache );
                }
            }

            return null;
        }

        /// <summary>
        /// Return the line offset position converted to World coordinates. CumulativeHeight
        /// and Cumulative Width. This is used to position the carat.
        /// </summary>
        /// <remarks>Again hard coded for text area.</remarks>
        /// <param name="oCaratPos">The Line and offset see are seeking.</param>
        /// <param name="pntWorld">world relative graphics coordinates.</param>
        public bool GlyphLineToPoint( int iCacheColumn, ILineRange oCaratPos, out Point pntWorld ) {
            CacheRow oRow = CacheLocate( oCaratPos.At );

            if( oRow != null ) {
                // This one returns local row/col in points(pixels) 0,0 ul of FTCacheLine
                pntWorld = oRow.CacheColumns[iCacheColumn].GlyphOffsetToPoint( oCaratPos.Offset );
                // This adds the vertical offset of the world.
                pntWorld.Y += oRow.Top;
                return true;
            } else {
                pntWorld = new Point( 0, 0 );
            }
            
            return false;
        }

        /// <summary>
        /// Given a point location attempt to locate the nearest line/glyph.
        /// </summary>
        /// <remarks>At this point we've probably located the textarea column the mouse click
        /// has occurred and we want to find which FTCacheLine it hits.
        /// If we want to edit in other columns we can do so by passing an
        /// argument.</remarks>
        /// <param name="oWorldLoc">Graphics location of interest in world coordinates. Basically
        ///                         where the mouse clicked.</param>
        /// <param name="oCaret">This object line offset is updated to the closest line offset.</param>
        public IPgCacheMeasures GlyphPointToRange( ref EditWindow2.WorldLocator oWorldLoc, ILineRange oCaret ) {
            try {
                foreach( CacheRow oCRow in _rgOldCache ) {
                    if( oCRow.Top    <= oWorldLoc.Y &&
                        oCRow.Bottom >= oWorldLoc.Y ) 
                    {
                        // Just bail on the first one. Something is up...
                        if( oWorldLoc._iColumn < 0 || 
                            oWorldLoc._iColumn >= oCRow.CacheColumns.Count )
                            return null;
                        // Note there is no left/right scroll. So World X == Local X
                        IPgCacheMeasures oCache   = oCRow.CacheColumns[oWorldLoc._iColumn];
                        SKPointI         pntLocal = new( oWorldLoc._pntLocation.X, oWorldLoc._pntLocation.Y - oCRow.Top );

                        oCaret.Line   = oCRow.Line;
                        oCaret.Offset = oCache.GlyphPointToOffset( pntLocal );

                        return oCache;
                    }
                }
            } catch( Exception oEx ) {
                Type[] rgErrors = { typeof( ArgumentOutOfRangeException ),
                                    typeof( ArgumentException ),
                                    typeof( NullReferenceException ) };
                if( rgErrors.IsUnhandled( oEx ) )
                    throw;
                LogError( "CacheManager2.GlyphPointToRange exception" );
            }

            return null;
        }

        /// <summary>
        /// Find the location to render primary textarea text.
        /// Note: Currently not actually being used! O.o
        /// </summary>
        /// <seealso cref="RenderAt( CacheRow oCache, SmartRect rcColumn )"/>
        public PointF RenderAt( CacheRow oCache, Point pntScreenTL ) {
            SKPointI pntWorldTopLeft  = TextRect.GetPoint(LOCUS.UPPERLEFT);
            PointF   pntRenderAt      = new PointF( pntScreenTL.X - pntWorldTopLeft.X, 
                                                    pntScreenTL.Y + oCache.Top - pntWorldTopLeft.Y );

            return pntRenderAt;
        }

		/// <summary>
        /// Advance tells us how far along graphically, we are in the text stream
        /// from the left hand side, so if the cursor moves up or down we can try 
        /// to hit that same advance point. Updates the caret pos as a side effect.
        /// </summary>
        /// <param name="pntWorld">World Coordinates.</param>
        /// <remarks>Advance is modulo in the wrapped text case.</remarks>
        public void CaretAndAdvanceReset( ref EditWindow2.WorldLocator sWorldLoc, ILineRange oCaretPos, ref float flAdvance ) {
            IPgCacheMeasures oCache = GlyphPointToRange( ref sWorldLoc, oCaretPos );
            if( oCache != null ) {
                Point oNewLocation = oCache.GlyphOffsetToPoint( oCaretPos.Offset );

                flAdvance = oNewLocation.X; 
            }
        }
    } // end class

    public class ViewTerminal :
        SKControl,
        IPgParent,
        IPgLoad<XmlElement>,
        IPgSave<XmlDocumentFragment>,
        IPgCommandView 
    {
        public bool IsDirty => false;

        public string Banner => "Simple Terminal";

        public SKImage? Icon => null;

        public Guid Catagory => GUID;

        public IPgParent Parentage => _oSite.Host;

        public IPgParent Services  => Parentage.Services;

        public static readonly Guid GUID = new( "{85031D86-3A4F-4D5B-9A1C-FC2A08BE7947}" );
        protected readonly IPgBaseSite _oSite;
        protected List<ILineSelection> _rgSelections = [];
        protected TerminalCache?       _oCacheMan;
        protected readonly IPgStandardUI2 _oStdUI;
        protected readonly TerminalDocument _oTerm;

        public uint FontStd => _oStdUI.StdFontAt( StdUIFaces.Retro );

        protected class DocSlot :
			IPgBaseSite,
            ITCSite
		{
			protected readonly ViewTerminal _oHost;

			public DocSlot( ViewTerminal oHost ) {
				_oHost = oHost ?? throw new ArgumentNullException();
			}

			public IPgParent Host => _oHost;

            public ICollection<ILineSelection> Selections => _oHost._rgSelections;

            public IReadableBag<Line> Lines => _oHost._oTerm;

            public void LogError(string strMessage, string strDetails, bool fShow=true) {
				_oHost._oSite.LogError( strMessage, strDetails, fShow );
			}

			public void Notify( ShellNotify eEvent ) {
			}

            public void OnRefreshComplete() {
                _oHost.Invalidate();
            }
        } // End class

        public ViewTerminal( IPgViewSite oSite, TerminalDocument oDoc ) {
            _oSite  = oSite ?? throw new ArgumentNullException();
            _oStdUI = Services as IPgStandardUI2 ?? throw new ArgumentException( "Parent view must provide IPgStandardUI service" );
            _oTerm  = oDoc ?? throw new ArgumentNullException();

			//Icon = DocMon.GetResource( "icons8-terminal-58.png" );
        }


        protected override void Dispose( bool fDisposing ) {
            _oTerm.LineEvent -= OnLineEvent_Term;

            base.Dispose( fDisposing );
        }

        public bool Initialize() {
            DocSlot oSlot = new DocSlot( this );

            _oCacheMan = new TerminalCache( oSlot, _oStdUI.FontRendererAt( FontStd ), false );

            _oTerm.LineEvent += OnLineEvent_Term;

            return true;
        }

        private void OnLineEvent_Term(Line? obj) {
            _oCacheMan.Refresh();
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

        protected override void OnKeyPress(KeyPressEventArgs e) {
            if( IsDisposed )
                return;

            _oTerm.Buffer.Enqueue( Convert.ToByte( e.KeyChar ) );
            e.Handled = true;
        }

        protected override void OnSizeChanged(EventArgs e) {
            base.OnSizeChanged(e);
            _oCacheMan.TextRect.SetRect( 0, 0, Width, Height );
        }

        protected readonly SmartRect _rcSpare = new SmartRect();

        /// <remarks>
        /// The CacheMap is list of rectangles that map 1->1 to the CacheList in the
        /// Row object of the cache manager. Unfortunately there might be layout objects
        /// that are NOT part of the cache map. Like the scroll bar.
        /// </remarks>
        protected override void OnPaintSurface( SKPaintSurfaceEventArgs e ) {
            base.OnPaintSurface(e);

            try {
                SKSurface skSurface = e.Surface;
                SKCanvas  skCanvas  = skSurface.Canvas;
                using SKPaint skPaint2 = new SKPaint {
                    Color = SKColors.Blue
                };
                using SKPaint skPaint = new SKPaint() {
                    BlendMode = SKBlendMode.Src,
                    Color     = _oStdUI.ColorsStandardAt( StdUIColors.BG)
                };
                // Paint all window background. Note: We could get by without this if
                // there was no space between lines/columns.
                skCanvas.DrawRect( new SKRect( 0, 0, Width, Height ), skPaint);

                // Now paint the rows.
                foreach( CacheRow oCache in _oCacheMan ) {
                    PaintBackground( skCanvas, skPaint, oCache );

                    _rcSpare.SetRect( 0, oCache.Top, Width, oCache.Bottom );

                    for( int iCache=0; iCache<oCache.CacheColumns.Count; ++iCache ) {
                        if( oCache[iCache] is IPgCacheRender oRender ) {
                            oRender.Render( skCanvas, 
                                            _oStdUI, 
                                            skPaint,
                                            _rcSpare, 
                                            iCache == 0 ? this.Focused : false );
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

        /// <summary>Paint just the background of just this cache row. And only
        /// if a special color. Else the normal bg color previously set ok. If there was no space
        /// between lines we could omit the previous all screen clear and just paint here!</summary>
        /// <remarks>If we wanted to paint transparent we could probably omit this and go with
        /// whatever is the existing background. Like a bitmap or something.</remarks>
        protected void PaintBackground( SKCanvas skCanvas, SKPaint skPaint, CacheRow oCache ) {
            StdUIColors eBg    = StdUIColors.Max;
            SKRect      skRect = new SKRect( 0,     oCache.Top, 
                                             Width, oCache.Bottom );

            if( _oTerm.CaretLine == oCache.Line )
                eBg = StdUIColors.BGWithCursor;

            if( eBg != StdUIColors.Max ) {
                skPaint .BlendMode = SKBlendMode.Src;
                skPaint .Color     = _oStdUI.ColorsStandardAt( eBg );
                skCanvas.DrawRect( skRect, skPaint );
            }
        }

        /// <summary>
        /// TODO: This doesn't look like it's doing a proper job with
        /// the moniker. Check up on that.
        /// </summary>
        protected bool Save() {
            string? strPath = _oTerm.CheckLocation( fNewLocation:false );

            if( string.IsNullOrEmpty( strPath ) )
                return false;

            if( !_oTerm.Save( strPath ) )
                return false;

            _oTerm.Moniker = strPath;

            return true;
        }

        public virtual bool Execute(Guid sGuid) {
            if( sGuid == GlobalCommands.Save ) {
                return Save();
            }
            return false;
        }

        public object? Decorate(IPgViewSite oBaseSite, Guid sGuid) {
            return null;
        }

    }

}
