using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing; 
using System.ComponentModel;
using System.Linq;

using SkiaSharp;

using Play.Interfaces.Embedding;
using Play.Parse.Impl;
using Play.Rectangles;

namespace Play.Edit {
	public enum UNICACHEHIT : int {
        Hit,   // the line was found, coordinates were returned.
        Miss,  // the line is not in the cache.
    }

    public enum CaretMove {
        LOCAL,
        MISS,
        NEARBY,
    }

    public enum RefreshType {
        RESET,
        COMPLEX,
        SIMPLE
    }

    public class CacheBase :
        IEnumerable<UniscribeCache>,
        IDisposable
    {
        public    IntPtr                _hScriptCache  = IntPtr.Zero;
        protected List<UniscribeCache>  _rgOldCache    = new List<UniscribeCache>();
        protected SCRIPT_FONTPROPERTIES _sDefFontProps = new SCRIPT_FONTPROPERTIES();
        protected int                   _iTabWidth     = 10; // Px. Just a nice starter value. Need a font to really calculate.

        private   readonly List<ILineSelection>  _rgSelections  = new List<ILineSelection>();

		public CacheBase() { 
		}

        /// <summary>
        /// With the default font loaded in the DC, call this function to get
        /// the font properties.
        /// </summary>
        /// <remarks>Need to look at this since we need to think of font fallback.
        /// TODO: Probably pull the Font and Grammer here from the site.
        /// <param name="hDC"></param>
        public virtual void InitNew( IntPtr hDC, int iSpacesPerTab ) {
            _sDefFontProps.Load( hDC, ref _hScriptCache );
            _iTabWidth = iSpacesPerTab * MeasureSpaceWidth( hDC );

            _rgOldCache.Clear();
        }

        public void Dispose() {
            if( _hScriptCache != IntPtr.Zero ) {
                PgUniscribe.ScriptFreeCache( ref _hScriptCache );
                _hScriptCache = IntPtr.Zero;
            }
        }

        protected virtual void LogError( string strDetails ) {
        }

        public IEnumerator<UniscribeCache> GetEnumerator() {
            return( _rgOldCache.GetEnumerator() );
        }

        IEnumerator IEnumerable.GetEnumerator() {
            return( _rgOldCache.GetEnumerator() );
        }

		public int Width { get; set; }

        /// <summary>
        /// Get the width of the space character. This works
        /// fine for monospacefonts to determing how big to make the tabs.
        /// Only need to call once when a DC is available or when
        /// the font changes. Font needs to be selected into DC before this call.
        /// </summary>
        /// <param name="hDC">Display Context</param>
        protected virtual int MeasureSpaceWidth( IntPtr hDC ) {
            int        iSpaceWidth = 10; // Just a magic number so we won't blow up if an exception is caught!
            AbcFloat[] rgAbcWidths = new AbcFloat[1];

            try {
                Gdi32.GetCharABCWidthsFloat(hDC, 32, 32, rgAbcWidths);
                iSpaceWidth = (Int32)( rgAbcWidths[0].flA +
                                       rgAbcWidths[0].flB +
                                       rgAbcWidths[0].flC );
            } catch( Win32Exception ) { // System.ComponentModel (our only reference)
                LogError( "MeasureSpaceWidth is blowing chunks!" );
            }

            return( iSpaceWidth );
        }

        public void Add( UniscribeCache oCache ) {
            _rgOldCache.Add( oCache );
        }

        public void Clear() {
            foreach( UniscribeCache oElem in _rgOldCache ) {
                oElem.Dispose();
            }
            _rgOldCache.Clear();
        }
    }

    /// <summary>
    /// This is the primary object that holds all the measured lines for the currently displayed portion of text.
    /// </summary>
	/// <remarks>I'm not sure where I got the idea to break up the CacheManager/CacheBase but it is important
	/// to initialize the manager's _oTextRect</remarks>
    public class CacheManager : CacheBase 
    {
        readonly CacheManagerAbstractSite _oSite     = null;
        Font                              _oFont     = null; // Right now we only handle one font. The future who knows?
        readonly SmartRect                _oTextRect = new SmartRect(); // World coordinates of our view port.

        // TODO: Get the grammar and the font from the site instead of from the constructor.
        public CacheManager( CacheManagerAbstractSite oSite, Font oFont, Size oSize ) :
			base() 
		{
			_oFont = oFont ?? throw new ArgumentNullException( "Need a font to get things rolling." );
			_oSite = oSite ?? throw new ArgumentNullException( "Cache manager is a sited object.");

			_oTextRect.SetPoint(SET.RIGID, LOCUS.UPPERLEFT | LOCUS.EXTENT, oSize.Width, oSize.Height );

			Width = _oTextRect.Width;
        }

		// BUG: Use the Dispose() pattern for this... we get away with this because the script cache is 
		//      unmanaged. If it were managed it can get destroyed before this call!!!
        ~CacheManager() {
            if( _hScriptCache != IntPtr.Zero ) {
                PgUniscribe.ScriptFreeCache( ref _hScriptCache );
            }
        }

		public void WordBreak(UniscribeCache oCache) {
			_oSite.WordBreak( oCache );
		}

        protected override void LogError( string strDetails ) {
            _oSite.LogError( "Text Manager", strDetails );
        }

        /// <summary>
        /// This of course won't cut it if we have more than one font on a line.
        /// </summary>
        public int LineSpacing {
            get { return( _oFont.Height ); }
        }

        /// <summary>
        /// Count of number of UniscribeCache objects inside the manager.
        /// </summary>
        public int Count {
            get { return( _rgOldCache.Count ); }
        }

        public SmartRect TextRect {
            get { return( _oTextRect ); }
        }

        public void OnMouseWheel( int iDelta ) {
            int iTopOld = _oTextRect[ SCALAR.TOP ];
            int iTopNew = iTopOld - ( 4 * iDelta / (int)_oFont.GetHeight() );
            
            _oTextRect.SetScalar(SET.RIGID, SCALAR.TOP, iTopNew );
        }

        public void OnScrollBar_Vertical( ScrollEvents e ) {
            int iHeight       = _oTextRect[ SCALAR.HEIGHT ];
            int iTopOld       = _oTextRect[ SCALAR.TOP    ];
            int iSafetyMargin = 10; // Probably should be some % of font height.

            switch( e ) {
                // These events move incrementally from where we were.
                case ScrollEvents.LargeDecrement:
                    _oTextRect.SetScalar(SET.RIGID, SCALAR.TOP, iTopOld - iHeight - iSafetyMargin );
                    break; 
                case ScrollEvents.SmallDecrement:
                    _oTextRect.SetScalar(SET.RIGID, SCALAR.TOP, iTopOld - (int)_oFont.GetHeight() );
                    break;
                case ScrollEvents.LargeIncrement:
                    _oTextRect.SetScalar(SET.RIGID, SCALAR.TOP, iTopOld + iHeight - iSafetyMargin );
                    break;
                case ScrollEvents.SmallIncrement:
                    _oTextRect.SetScalar(SET.RIGID, SCALAR.TOP, iTopOld + (int)_oFont.GetHeight() );
                    break;

                // We can potentialy render less until this final end scroll comes in.
                case ScrollEvents.EndScroll:
                    break;

                // These events reset the coordinate system. NOTE: Scroll reset does this too. Probably redundant.
                case ScrollEvents.First:
                    _oTextRect.SetScalar(SET.RIGID, SCALAR.TOP, 0 );
                    break;
                case ScrollEvents.Last:
                    // TODO: Probably dangerous setting MaxValue. I could simply move it past the last screen full.
                    _oTextRect.SetScalar(SET.RIGID, SCALAR.BOTTOM, int.MaxValue - iHeight );
                    break;
                case ScrollEvents.ThumbPosition:
                    break;
                case ScrollEvents.ThumbTrack:
                    break;
            }
        }

        /// <summary>
        /// This function behaves like the OnScroll() and OnMouse() events. It modifies the 
        /// position of the cache rectangle. You must call Refresh() on the cache 
        /// after making this call.
        /// </summary>
        /// <param name="rgSize">The new size of the rectangle.</param>
        public void OnViewSized( Size oSize ) {
            _oTextRect.SetPoint(SET.RIGID, LOCUS.UPPERLEFT | LOCUS.EXTENT, oSize.Width, oSize.Height );
			Width = _oTextRect[SCALAR.WIDTH];

            // We've got to make sure we're sorted properly so we don't scramble the screen
            // when aligning. Sort by line is self healing but makes us dependant on line number.
            // But Sort by Top obviates that line# dependancy. Since Refresh() is self healing
            // I'm going with sort by Top here, so I can try to remove my line number dependancy
            // in all of the manager.
            // Using overload for Comparisson<T> not IComparer.
            _rgOldCache.Sort( (x, y) => x.Top.CompareTo( y.Top ) ); 

            if( _rgOldCache.Count == 0 )
                return;

            // The height of the elements might change because of the width change
            // and so the following siblings need to have their top's reset!
            int iTop = _rgOldCache[0].Top;
            foreach( UniscribeCache oElem in this ) {
                oElem.OnChangeSize( _oSite.Selections, Width );
                oElem.Top = iTop;

                iTop = oElem.Bottom + 1; // Aligning.
            }
        }

        /// <summary>
        /// Look up the line that the cache element is currently pointing to. It's the value
        /// returned from the .Line.At method.
        /// </summary>
        /// <param name="rgCache">sorted array to search</param>
        /// <param name="iLine">Line on the uniscribecache line element I'm looking for.</param>
        /// <returns>The element if found</returns>
        /// <remarks>
        /// I should parameterize the search function and pass it in as a delegate but
        /// I'm too lazy for that now.
        /// </remarks>
        //public static UniscribeCache BinarySearch( List<UniscribeCache> rgCache, int iLine ) 
        //{
        //    int iLow  = 0;
        //    int iHigh = rgCache.Count - 1;
        //    int iMid  = 0;

        //    while( iHigh >= iLow ) {
        //        iMid = iLow + (( iHigh - iLow ) / 2 );
        //        if(      rgCache[iMid].Line.At < iLine )
        //            iLow  = iMid + 1;
        //        else if( rgCache[iMid].Line.At > iLine )
        //            iHigh = iMid - 1;
        //        else
        //            return( rgCache[iMid] );
        //    }

        //    // Did we find what we wanted or simply got close?
        //    return( null );
        //}

        /// <summary>
        /// This effectively resets the cache manager. We create a seed element
        /// chosen by rough scroll position or cursor as specified by the neighborhood.
        /// The buffer rectangle is reset so it's top is zero and the top of the new cache
        /// element is zero. Old cache is untouched and will need to be cleared.
        /// </summary>
        /// <param name="hDC"></param>
        /// <param name="iLine"></param>
        protected UniscribeCache CacheReset( 
            IntPtr              hDC,
            RefreshNeighborhood eNeighborhood
        ) {
            if( hDC == IntPtr.Zero )
                return( null );

            // Where to set the first cache elem TOP? 0 or somewhere in the middle of the screen?
            // I think 0 is fine since we can go negative no problem.
            _oTextRect.SetPoint(SET.RIGID, LOCUS.UPPERLEFT, 0, 0 );
			Width = _oTextRect.Width; // TODO: Not strictly needed.

			// Ask our site for locating ourselves. Either based our our scroll
			// position or carat depending on how we were called.
			_oSite.Neighborhood( eNeighborhood, out Line oLine, out int iOffs);
			if ( oLine == null )
                return( null );

            UniscribeCache oElem = _rgOldCache.Find( item => item.At == oLine.At ); // TODO: Reconsider change back to item.Line == oLine
            if (oElem == null) // If can't find matching elem, create it.
                oElem = CreateElement( hDC, oLine );

            if( oElem == null )
                return( null );

            // Text rect is reset to UL => 0,0. Now set this element's top down a bit and build around it.
            switch( eNeighborhood ) {
                case RefreshNeighborhood.CARET:
                    oElem.Top = LineSpacing * 2; // Match the slop in RefreshCache() else new elem will be outside rect on first load() and get flushed!
                    break;
                case RefreshNeighborhood.SCROLL:
                    oElem.Top = 0; // If this is bottom line. We'll accumulate backwards to fix up cache.
                    break;
            }

            return( oElem );
        }

        /// <summary>
        /// Simply trying to find the topmost cache element we can use. No need to be sorted.
        /// If the cache is missing lines (insert for example) we won't notice.
        /// Deleted lines are removed from the cache the minute they are deleted. 
        /// </summary>
        /// <returns>Highest previously cached element that looks valid.</returns>
        protected UniscribeCache FindLeast()
        {
            if( _rgOldCache.Count == 0 )
                return( null );

            // Find the closest valid line to the top of the TextRect.
            UniscribeCache oLeast   = null;
            int            iMinDist = int.MaxValue;

            for( int iElem = 0; iElem < _rgOldCache.Count; ++iElem ) {
                UniscribeCache oCurrLine = _rgOldCache[iElem];
                // If the element overlaps the textrect or is inside, use it. Might be
                // nice to add a check if the line is still in the buffer or has been deleted.
                if( oCurrLine.Top    <= TextRect[SCALAR.BOTTOM] + LineSpacing * 2 &&
                    oCurrLine.Bottom >= TextRect[SCALAR.TOP   ] - LineSpacing * 2 ) 
                { 
                    // Is element's top closest to the top of the textrect.
                    int iDistance = Math.Abs( oCurrLine.Top - TextRect[SCALAR.TOP] );
                    if( iDistance < iMinDist ) {
                        oLeast   = oCurrLine;
                        iMinDist = iDistance;
                    }
                    // Is element's bottom closest to the top of the textrect.
                    iDistance = Math.Abs( oCurrLine.Bottom - TextRect[SCALAR.TOP] );
                    if( iDistance < iMinDist ) {
                        oLeast   = oCurrLine;
                        iMinDist = iDistance;
                    }
                }
            }

            return( oLeast );
        }

        /// <summary>
        /// Find the next line after the line pointed to by the current cache element.
        /// Use an old element or create a new element and stack in the given direction.
        /// </summary>
        /// <param name="hDC">Display context.</param>
        /// <param name="oElem">Element where we are building up the cache from.</param>
        /// <param name="iDir">Which direction to try.</param>
        /// <returns>New or recycled cache element stacked above or below previous.</returns>
        protected UniscribeCache RefreshNext( IntPtr hDC, UniscribeCache oElem, int iDir ) {
            if( oElem == null )
                return( null );

            UniscribeCache oPrev = oElem;
            int            iLine = oElem.At + iDir;

            // See if we already the line computations. Given the old cache is sorted by
            // line I could potentially do a b-search. This find is a linear operation.
            oElem = _rgOldCache.Find( item => item.At == iLine );
            if (oElem == null) { // If can't find matching elem, create it.
                Line oLine = _oSite.GetLine(iLine);

                // If we reach the top or bottom, the next line is going to be null.
                if( oLine == null ) {
                    return( null );
                }

                oElem = CreateElement( hDC, oLine );
            }

            if( oElem != null ) {
                if( oElem.IsInvalid ) {
                    ElemUpdate( hDC, oElem );
                }
                if( iDir < 0 ) {
                    oElem.Top = oPrev.Top - oElem.Height - 1;
                } else {
                    oElem.Top = oPrev.Bottom + 1;
                }
            }

            return( oElem );
        }
        
        /// <summary>
        /// Find new gaps and create new cache elements for them. I've got the DC so I can calculate font stuffs, JOY!
        /// Note: Can't search for the line with the binary search since we're thrashing the order of the cache.
        /// </summary>
        /// <remarks></remarks>
        /// <param name="hDC">A display context.</param>
        /// <returns>True if the cache has any elements.</returns>
        protected bool CacheRefresh(
            IntPtr         hDC,
            UniscribeCache oTop
        ) {
            if( oTop == null ) {
                _oSite.LogError( "view cache", "Unable to find least or reseed new cache! We're hosed!" );
                return( false );
            }

            if( oTop.IsInvalid ) {
                ElemUpdate( hDC, oTop );
            }

            List<UniscribeCache> rgNewCache = new List<UniscribeCache>();

            rgNewCache.Add( oTop ); // Stick our seed in the new cache.

            UniscribeCache oElem = oTop; // Current starting point.

            // Build downwards towards larger line numbers.
            while( oElem != null && oElem.Bottom < TextRect[SCALAR.BOTTOM] ) {
                UniscribeCache oPrev = oElem;
                oElem = RefreshNext( hDC, oElem, +1 );
                if( oElem == null ) {
                    // We're at the bottom, Re-shift the rect bottom to match bottom line so we don't have gap.
                    // This way we don't keep scrolling elements forever out of view.
                    TextRect.SetScalar(SET.RIGID, SCALAR.BOTTOM, oPrev.Bottom );
                } else {
                    rgNewCache.Add( oElem );
                }
            };

            UniscribeCache oBottom = oElem; // Last downward element we saved.
            oElem = oTop;

            // Build upwards towards smaller line numbers.
            while( oElem != null && oElem.Top > TextRect[SCALAR.TOP ] ) {
                UniscribeCache oPrev = oElem;
                oElem = RefreshNext( hDC, oElem, -1 );
                if( oElem == null ) {
                    // We're at the top. Re-shift the rect top to match top line so we don't have a gap.
                    // this way we don't keep scrolling elements forever out of view.
                    TextRect.SetScalar(SET.RIGID, SCALAR.TOP, oPrev.Top );
                } else {
                    rgNewCache.Add( oElem );
                }
            }

            oElem = oBottom;

            // Go down one last time in case the rect was moved up by the upward building loop. That
            // would create a gap that the downward builder didn't know about.
            while( oElem != null && oElem.Bottom < TextRect[SCALAR.BOTTOM] ) {
                UniscribeCache oPrev = oElem;
                oElem = RefreshNext( hDC, oElem, +1 );
                if( oElem != null ) {
                    rgNewCache.Add( oElem );
                }
            };
            
			// Sort by line order in case we forget the array order isn't always the window position order.
           _rgOldCache = rgNewCache;
           _rgOldCache.Sort( (x, y) => x.At - y.At ); 

            return( _rgOldCache.Count != 0 );
        } // end method

		/// <remarks>Need to get this in the base class. But the text rect doesn't exist there.
		/// Nor the font height. Work in progress.</remarks>
        protected void ElemUpdate( IntPtr hDC, UniscribeCache oElem ) {
			try {
				oElem.Update( hDC, ref _hScriptCache, _iTabWidth, _oFont.Height, 
							  _sDefFontProps, this, Width, _oSite.Selections );
			} catch( Exception oEx ) {
				Type[] rgErrors = { typeof( NullReferenceException ),
									typeof( ArgumentNullException ) };
				if( !rgErrors.Contains( oEx.GetType() ))
					throw;

                _oSite.LogError( "view cache", "Update request on empty element" );
			}
        }

        /// <summary>
        /// Simply update invalid elements and restack. Use this when simply editing within one line.
		/// We redo the elem.top in case a line grows in height.
        /// </summary>
        /// <param name="hDC"></param>
        protected void CacheValidate( IntPtr hDC )
        {
			try {
				UniscribeCache oPrev = null;
				foreach( UniscribeCache oElem in _rgOldCache ) {
                    if( oElem.Color.Count > 0 ) {
					    oElem.Words.Clear();
                        foreach( IColorRange oRange in oElem.Line.Formatting ) {
                            oElem.Words.Add( oRange );
                        }
                    }
                    if( oElem.Words.Count < 1 ) {
                        oElem.Words.Add(new ColorRange(0,oElem.Line.ElementCount,0));
				    }
					if( oElem.IsInvalid )
						ElemUpdate( hDC, oElem );

					// NOTE: if the elements aren't stacked in line order, we kinda have a problem.
					if( oPrev != null )
						oElem.Top = oPrev.Bottom + 1;

					oPrev = oElem;
				}
			} catch( Exception oEx ) {
				Type[] rgErrors = { typeof( NullReferenceException ),
									typeof( ArgumentNullException ) };
				if( !rgErrors.Contains( oEx.GetType() ))
					throw;

				_oSite.LogError( "view cache", "Couldn't validate cache elements." );
			}
        }

        /// <summary>
        /// Call this function when the user has scrolled or resized the screen.
        /// Or lines got inserted or deleted from the document. 
        /// Note: At present, we don't really know when a cut or paste has occured. This is kind of a drag.
        /// </summary>
        public void Refresh( 
            IntPtr              hDC,
            RefreshType         eReset,
            RefreshNeighborhood eNeighborhood
        ) {
			Width = _oTextRect.Width;

            // Sort cache by line number so we can possibly b-search. I know, it's line number dependency.
            // Of course, finding holes is going to depend on the document lines getting renumberd
            // before our call.
            _rgOldCache.Sort( (x, y) => x.At - y.At ); 

            UniscribeCache oStart = null;

            switch( eReset ) {
                case RefreshType.RESET:
                    oStart = CacheReset( hDC, eNeighborhood );
                    break;
                case RefreshType.COMPLEX:
                    oStart = FindLeast();
                    break;
                case RefreshType.SIMPLE:
                    CacheValidate( hDC );
                    return;
            }

            // Couldn't find anything fall back to scroll pos! This can happen if lines are deleted.
            if( oStart == null ) {
                oStart = CacheReset( hDC, RefreshNeighborhood.SCROLL );
            }
            if( oStart != null ) {
                CacheRefresh( hDC, oStart );
            }

            _oSite.OnRefreshComplete();
        } // end method

        /// <summary>
        /// Create a cached line element. There are a lot of dependencies on stuff in this object
        /// and so we create the element here and pass it out to be used.
        /// </summary>
        /// <param name="hDC">Display context</param>
        /// <param name="oLine">The line we are trying to display.</param>
        /// <returns>A cache element with enough information to display the line on the screen.</returns>
        private UniscribeCache CreateElement( IntPtr hDC, Line oLine ) {
            if( oLine == null ) {
                _oSite.LogError( "view cache", "Guest line must not be null for screen cache element." );
                return( null );
            }

            UniscribeCache oElem = null;

			// oElem = new CacheTable( oLine ); // Experimental columnular table.
			if( _oSite.IsWrapped( oLine.At ) ) {
				oElem = new CacheWrapped( oLine );   // Heavy duty guy.
			} else {
				oElem = new UniscribeCache( oLine ); // Simpler object.
			}

            ElemUpdate( hDC, oElem );

            return( oElem );
        }

        /// <summary>
        /// Precaching elements that I need measurements from, like next and
        /// previous lines for caret movement. For example, if the caret is
        /// one inch from the left margin and I attempt to move up to a wrapped
        /// line which has not been cached yet. 
        /// 
        /// The only hitch is that we sort of assume precached line
        /// actually stays in the cache on refresh or we wasted some effort.
        /// 
        /// TODO: If the line isn't in the cache, I could attempt to move
        /// the text rect by one line height, note: CaratLocal()
        /// based on current font, refreshing the cache. But that seems
        /// a bit like overkill given I know I'm only crawling up or down.
        /// one line. Or perhaps better, simply make sure I've got a cached elem
        /// above and/or below the view port at all times.
        /// </summary>
        public UniscribeCache PreCache( int iLineAt, IntPtr hDC ) {
            // Check if the given edit is in the cache.
            foreach( UniscribeCache oTemp in _rgOldCache ) {
                if( oTemp.At == iLineAt )
                    return( oTemp );
            }

            // Try to find the requested line in the editor.
            Line oLine = _oSite.GetLine( iLineAt );
            if( oLine == null )
                return( null );

            // Line is not currently in cache, make a new elem.
            UniscribeCache oCache = CreateElement( hDC, oLine );

            if( oCache == null )
                return( null );

            // We sort of assume the new element is going to be at the top
            // or bottom of the view and set only it's vertical placement.
            // Also assumes Line.At implies order in the buffer.
            foreach( UniscribeCache oTemp in _rgOldCache ) {
                if( oTemp.At == oCache.At + 1 ) { // Is the cache elem above?
                    oCache.Bottom = oTemp.Top - 1;
                    break;
                }
                if( oTemp.At == oCache.At - 1 ) { // Is the cache elem below?
                    oCache.Top    = oTemp.Bottom + 1;
                    break;
                }
            }

            _rgOldCache.Add( oCache );

            return( oCache );
        }

        /// <summary>
        /// Move the given caret offset on glyph in the direction specified. There are no side effects.
        /// </summary>
        /// <param name="eAxis">Horizontal or Vertical</param>
        /// <param name="iDir">How much to move by, usually +1 or -1.</param>
        /// <param name="iAdvance">Distance from left to maintain when moving vertically. This value can be updated.</param>
        /// <param name="oCaret">The caret to be updated.</param>
        /// <returns>Instructions on how to refresh the cache after this movement.</returns>
        public CaretMove MoveCaret( Axis eAxis, int iDir, ref int iAdvance, ILineRange oCaret ) {
            if( oCaret == null ) {
                _oSite.LogError( "view cache", "Caret pointer must not be null." );
                return( CaretMove.LOCAL );
            }

            int iOffset = oCaret.Offset;

            // If total miss, build a new screen based on the location of the caret.
            UniscribeCache oElem = CacheLocate( oCaret.Line.At );
            if( oElem == null ) {
                return( CaretMove.MISS );
            }

            if( iDir != 0 ) {
                // First, see if we can navigate within the line we are currently at.
                if( !oElem.Navigate( eAxis, iDir, ref iAdvance, ref iOffset ) ) {
                    iDir = iDir < 0 ? -1 : 1; // Only allow move one line up or down.

					// _rgLeft[_rgLeft.Count-1] = _oTextRect.GetScalar( SCALAR.WIDTH );

                    UniscribeCache oNext = PreCache( oElem.At + iDir, IntPtr.Zero  );
                    if( oNext != null ) {
                        // Find out where to place the cursor as it moves to the next line.
                        iOffset = oNext.OffsetBound( eAxis, iDir * -1, iAdvance );
                        oElem   = oNext;
                    }
                }

                // If going up or down ends up null, we won't be moving the caret.
                oCaret.Line   = oElem.Line;
                oCaret.Offset = iOffset;
            }

            return( CaretLocal( oElem, iOffset ) );
        }

        public bool IsHit( ILineRange oCaret ) {
            UniscribeCache oCache = CacheLocate( oCaret.Line.At );

            if( oCache != null ) {
                Point pntCaretLoc = oCache.GlyphOffsetToPoint( oCaret.Offset );

                pntCaretLoc.Y += oCache.Top;

                bool fTopIn = _oTextRect.IsInside( pntCaretLoc.X, pntCaretLoc.Y );
                bool fBotIn = _oTextRect.IsInside( pntCaretLoc.X, pntCaretLoc.Y + _oFont.Height );

                return( fTopIn || fBotIn );
            }

            return( false );
        }

        /// When the carat moves we try to determine what to do to keep the screen tracking
        /// it. But thinking about this, it's odd. Since we know the line it's on. Seems we
        /// could do a better job tracking with line instead of the world location which can
        /// be indeterminant if the carat line bounces out of the cache!! Need to think about this.

        /// Because we can resize the window. It's possible that the cursor can end up well out of
        /// the cache buffer. Also, for big fonts, the height of the cursor is an issue since a sliver
        /// of the top can be visible at the bottom. but we really want better than that.
        /// Also note, the cursor can potentially hit on the top and bottom!

        /// <summary>
        /// Update the sliding window based the offset into the given cache element.
        /// If calling this the caret is already nearby, so try to locate.
        /// </summary>
        /// <param name="oCache">The cache element to consider.</param>
        /// <param name="iOffset">Offset into the given cache element.</param>
        public CaretMove CaretLocal( UniscribeCache oCache, int iOffset ) {
            CaretMove eMove       = CaretMove.LOCAL;
            Point     pntCaretLoc = oCache.GlyphOffsetToPoint( iOffset );

            pntCaretLoc.Y += oCache.Top;

            LOCUS eHitTop = _oTextRect.IsWhere( pntCaretLoc.X, pntCaretLoc.Y );
            LOCUS eHitBot = _oTextRect.IsWhere( pntCaretLoc.X, pntCaretLoc.Y + _oFont.Height );

            if( ( eHitTop & LOCUS.TOP    ) != 0 ) {
                _oTextRect.SetScalar(SET.RIGID, SCALAR.TOP,    pntCaretLoc.Y );
                eMove = CaretMove.NEARBY;
            }
            if( ( eHitBot & LOCUS.BOTTOM ) != 0 ) {
                _oTextRect.SetScalar(SET.RIGID, SCALAR.BOTTOM, pntCaretLoc.Y + _oFont.Height );
                eMove = CaretMove.NEARBY;
            }

            return( eMove );
        }

        /// <summary>
        /// Line has been updated. We'll need to give the cache access to a DC for the font works,
        /// But right now the font cache seems to be working well enough.
        /// </summary>
        /// <param name="oLine">the line that changed.</param>
        public void OnLineUpdated( Line oLine ) {
            foreach( UniscribeCache oCache in _rgOldCache ) {
                if( oCache.Line == oLine ) {
                    oCache.Invalidate();
                }
            }
        }

        /// <summary>
        /// TODO: Should invalidate our host window.
        /// </summary>
        public void OnLineAdded( Line oLine ) {
        }

        /// <summary>
        /// TODO: This is lame for bulk deletes but is fine for a single line. Look to see
        /// if the new line is sequentially somewhere within our cached lines.
        /// </summary>
        public void OnLineDeleted( Line oLine ) {
            for( int i = 0; i<_rgOldCache.Count; ++i ) {
                if( _rgOldCache[i].Line == oLine ) {
                    _rgOldCache.RemoveAt( i );
                    break;
                }
            }
        }

        /// <see cref="CacheManager2" />
        public void OnChangeFormatting( ICollection<ILineSelection> rgSelection ) {
            foreach ( UniscribeCache oCache in _rgOldCache ) {
                oCache.OnChangeFormatting( Width, rgSelection );
            }
        }

        /// <summary>
        /// Find the requested line.
        /// </summary>
        /// <param name="iLine">Line identifier. Technically for use, this does not need to be an
        /// array index. But just a unique value sitting at the "At" property on the line.</param>
        /// <returns>The cache element representing that line.</returns>
        public UniscribeCache CacheLocate( int iLine ) {
            foreach( UniscribeCache oCache in _rgOldCache ) {
                if( oCache.At == iLine ) {
                    return( oCache );
                }
            }

            return( null );
        }

        /// <summary>
        /// Return the line offset position converted to World coordinates. CumulativeHeight
        /// and Cumulative Width.
        /// </summary>
        /// <param name="oLine">The line we are searching for.</param>
        /// <param name="iOffset">Offset within the line.</param>
        /// <param name="pntLocation">return world relative graphics coordinates.</param>
        /// <returns></returns>
        public bool GlyphLineToPoint( ILineRange oSelection, out Point pntWorld ) {
            UniscribeCache oCache = CacheLocate( oSelection.Line.At );

            if( oCache != null ) {
                pntWorld = oCache.GlyphOffsetToPoint( oSelection.Offset );
                pntWorld.Y += oCache.Top;
                return( true );
            } else {
                pntWorld = new Point( 0, 0 );
            }
            
            return( false );
        }

        /// <summary>
        /// Given a point location attempt to locate the nearest line/glyph.
        /// </summary>
        /// <param name="pntLocation">Graphics location of interest in world coordinates.</param>
        public UniscribeCache GlyphPointToRange( Point pntLocation, ILineRange oCaret ) {
            foreach( UniscribeCache oElem in _rgOldCache ) {
                if( oElem.Top    <= pntLocation.Y &&
                    oElem.Bottom >= pntLocation.Y ) 
                {
                    oCaret.Line   = oElem.Line;
                    oCaret.Offset = oElem.GlyphPointToOffset( pntLocation );

                    return( oElem );
                }
            }

            return( null );
        }

        public PointF RenderAt( UniscribeCache oCache, Point pntScreenTL ) {
            SKPointI  pntWorldTL  = TextRect.GetPoint(LOCUS.UPPERLEFT);
            PointF pntRenderAt = new PointF( pntScreenTL.X - pntWorldTL.X, 
                                             pntScreenTL.Y + oCache.Top - pntWorldTL.Y );

            return( pntRenderAt );
        }

        public void RenderEOL( IntPtr hDC, ICollection<ILineSelection> rgSelectionTypes, Point pntScreenTL ) 
        {
            foreach( UniscribeCache oCache in this ) {
                foreach( ILineSelection oSelection in rgSelectionTypes ) {
                    if( oSelection.IsEOLSelected && 
                        oSelection.IsHit( oCache.Line ) )
                    {
                        oCache.RenderEOL(hDC, RenderAt( oCache, pntScreenTL ) );
                    }
                }
            }
        } // end method

        /// <summary>
        /// Links are those little "error here" marks.
        /// </summary>
        /// <param name="hDC">IntPtr to a display context.</param>
        /// <param name="pntTopLeft">Top left of all the edits. We add the vertical extents
        /// to arrive at our position.</param>
        public void RenderErrorMark( IntPtr hDC, Point pntScreenTL )
        {
            foreach( UniscribeCache oCache in this ) {
                oCache.RenderLinks(hDC, RenderAt( oCache, pntScreenTL ) );
            }
        } // end method

		/// <summary>
        /// Advance tells us how far along graphically, we are in the text stream
        /// from the left hand side, so if the cursor moves up or down we can try 
        /// to hit that same advance point. Updates the caret pos as a side effect.
        /// </summary>
        /// <param name="pntWorld">World Coordinates.</param>
        /// <remarks>Advance is modulo in the wrapped text case.</remarks>
        public void CaretAndAdvanceReset( Point pntWorld, ILineRange oCaretPos, ref int iAdvance ) {
            UniscribeCache oCache = GlyphPointToRange( pntWorld, oCaretPos );
            if( oCache != null ) {
                Point oNewLocation = oCache.GlyphOffsetToPoint( oCaretPos.Offset );

                iAdvance = oNewLocation.X; // This doesn't deal with non-wrapped text case.
            }
        }
   } // end class
} // end namespace
