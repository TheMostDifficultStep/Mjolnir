using System;
using System.Collections.Generic;
using System.Linq;
using System.Drawing;
using System.Diagnostics;

using Play.Parse;

// This file contains our line wrapping logic and it works in conjunction with the editor.

namespace Play.Edit {
    /// <summary>
    /// I don't strictly need this object since a line Range would work minus the Advance
    /// information. I can always look up the advance using the offset. So I'm going to
    /// think about this for a bit.
    /// </summary>
    public class WrapSegment : IMemoryRange {
                 private int _iOffset;
                 private int _iLength;
        readonly private int _iAdvance;
		readonly private int _iWrap;

        public WrapSegment( int iOffset, int iLength, int iAdvance, int iWrap ) {
            _iOffset  = iOffset;
            _iLength  = iLength;
            _iAdvance = iAdvance;
			_iWrap    = iWrap;
        }

		public override string ToString() {
			return( "(" + _iOffset.ToString() + ":" + _iLength + ")");
		}

		public int Advance {
            get {
                return( _iAdvance );
            }
        }

		public int Wrap {
			get {
				return( _iWrap );
			}
		}

        #region IColorRange Members

        public int ColorIndex {
            get { return( 0 ); }
        }

        public void Summate( int iCumulativeCount ) {
            throw new NotImplementedException();
        }

        public int Offset {
            get {
                return( _iOffset );
            }
            set {
                _iOffset = value;
            }
        }

        public int Length {
            get {
                return( _iLength );
            }
            set {
                _iLength = value;
            }
        }

        #endregion
    }

    /// <summary>
    /// Cache a single line of info but allow for wrap around when displaying on a narrow window.
    /// </summary>
    public class CacheWrapped :
        UniscribeCache
    {
        // Wrapping info.
        readonly List<WrapSegment> _rgWrapSegment  = new List<WrapSegment>(); // same size as number of wrapped lines. Value to subtract from AdvanceAbs
        readonly List<IPgWordRange> _rgWordBreaking = new List<IPgWordRange>(); // Word breaking info.
		protected int[]            _rgWrapOffsets; // Given the glyph index, report what line segment it belongs on!

        public CacheWrapped( Line oLine ) : base( oLine ) 
        {
        }

        public override void Dispose() {
            base.Dispose();

            _rgWrapOffsets = null;
        }
        override public int Height {
            get { 
                return( (int)( _rgWrapSegment.Count < 1 ? _flFontHeight : _rgWrapSegment.Count * _flFontHeight ) );
            }
        }

        public override ICollection<IPgWordRange> Words {
            get {
                return( _rgWordBreaking );
            }
        }

        /// <summary>
        /// The collection of Color and Selection ranges reformated and
        /// divided to fit the wrap segments.
        /// </summary>
        public override ICollection<IColorRange> Color {
            get {
                return( _rgSlicedColor );
            }
        }

        /// <remarks>I should make most of those parameters, just an interface back to the cache manager.</remarks>
        public override void Update( 
            IntPtr                      hDC, 
            ref IntPtr                  hScriptCache, 
            int                         iTabWidth,
            float                       flFontHeight,
            SCRIPT_FONTPROPERTIES       sDefFontProps, 
            CacheManager                oManager,
            int                         iWidth, 
            ICollection<ILineSelection> rgSelection 
        ) {
            base.Update( hDC, ref hScriptCache, iTabWidth, flFontHeight,
                         sDefFontProps, oManager, iWidth, rgSelection );

            // In the olden days, Update would return "false" if the buffer was emptied by an edit
            // But we still need to clear the WrapSegments from whatever they were. We always
            // call the Parse/Wrap functions now.

			// BUG: There's a base class of the manager which isn't sited. Might need to change that. 
			if( oManager != null )
				oManager.WordBreak( this );

            WrapSegmentsCreate( iWidth );

            if( rgSelection != null )
                OnChangeFormatting( iWidth, rgSelection );
        }
        
        /// <summary>
        /// Simple "greedy" algorithm for computing word wrapping and filling the 
        /// _rgWrapOffsets and _rgWrapDiff arrays. When size changes, recalc things.
        /// </summary>
        /// <param name="iDisplayWidth"></param>
        /// <param name="oFormatting"></param>
        internal override void OnChangeSize( ICollection<ILineSelection> rgSelections, int iWidth ) {
            OnChangeFormatting( iWidth, rgSelections );
        }

        /// <summary>
        /// Find the character offset of the mouse position. I really should be
        /// using ScriptXtoCP(), but I hate the regular behavor of selection
        /// across ltr through rtl regions. At present our selection code doesn't
        /// check the direction of the block. We need to check the boundaries of
        /// a selection avoid copy/cut problems.
        /// </summary>
        /// <param name="pntLocation">Location relative to the upper left of line 
        /// collection which is 0, 0. Remove any window positioning before passing
        /// coordinates to us.</param>
        /// <returns>Character offset of the given location. 0 if left of the
        /// first element.</returns>
        public override int GlyphPointToOffset( Point pntWorld ) {
			try {
				if( _iGlyphTotal < 1 )
					return( 0 );
				if( pntWorld.X > _rgAdvanceAbs[_iGlyphTotal-1] + _rgAdvanceWidths[_iGlyphTotal-1] )
					return( _iGlyphTotal );
				if( pntWorld.X < 0 )
					return( 0 );

				int iYDiff = pntWorld.Y - Top;
				int iYOffs;

				// Since the lines are wrapped. We need to inspect the _rgWrapDiff array to
				// find on which wrapped segment to begin searching for the X offset.
				for( iYOffs = 0; iYOffs <_rgWrapSegment.Count; ++iYOffs ) {
					if( ( Top + ( iYOffs + 1 ) * _flFontHeight ) > pntWorld.Y )
						break;
				}

				if( iYOffs >= _rgWrapSegment.Count )
					return( _iGlyphTotal );

				int iIndex = Array.BinarySearch<Int32>( _rgAdvanceAbs, 
					0, _iGlyphTotal, pntWorld.X + _rgWrapSegment[iYOffs].Advance );
            
				if( iIndex < 0 )
					iIndex = ~iIndex; // When we miss. The inverse is always the higher number.
				else
					iIndex+=1; // The only time it picks the correct element is if it's exact match. So add one.

				int iAdvanceLeft  = _rgAdvanceAbs[iIndex - 1] - _rgWrapSegment[iYOffs].Advance;
				int iAdvanceRight = _rgAdvanceAbs[iIndex    ] - _rgWrapSegment[iYOffs].Advance;
				int iAdvanceHalf  = (( iAdvanceRight - iAdvanceLeft ) >> 1 );

				if( pntWorld.X < iAdvanceLeft + iAdvanceHalf )
					iIndex -= 1;

				return( iIndex );
			} catch( IndexOutOfRangeException ) {
                Debug.Assert( false, "GlyphPointToOffset1() index exception." );
				return( 0 );
			}
        }

        /// <summary>
        /// Move left or right on this line.
        /// </summary>
        /// <param name="iIncrement">+/- number of glyphs to move.</param>
        /// <param name="iAdvance">Current graphics position on the line.</param>
        /// <param name="iOffset">Current logical position on the line.</param>
        /// <returns>True if able to move. False if positioning will move out of bounds.</returns>
        /// <remarks>TODO: In the future we must use the cluster info.</remarks>
        protected override bool NavigateHorizontal( int iIncrement, ref int iAdvance, ref int iOffset ) {
            int iNext = iOffset + iIncrement;

            if( _rgWrapOffsets == null ) {
                iAdvance = 0;
                iOffset  = 0;
                return( false ); // was true, but can't nav horizontal to next line!
            }

            try {
                if( iNext >= 0 && iNext <= _iGlyphTotal ) {
                    iOffset = iNext;

                    iAdvance = _rgAdvanceAbs[iOffset] - _rgWrapSegment[ _rgWrapOffsets[iOffset] ].Advance;
                    return( true );
                }
            } catch( IndexOutOfRangeException ) {
                Debug.Assert( false, "Either a segment or offset access is out of bounds. Log a bug and continue your work." );
            }

            return( false );
        }

        /// <summary>
        /// Move up or down based on the previous advance. 
        /// </summary>
        /// <param name="iIncrement">Direction of travel, positive is down, negative is up.</param>
        /// <param name="iAdvance">Previous "pixel" offset on a given line. The wrapped value.</param>
        /// <param name="iOffset">Closest character we can find to the given offset on a line above or below.</param>
        /// <returns></returns>
        protected override bool NavigateVertical( int iIncrement, int iAdvance, ref int iOffset ) {
            // Any movement will put us out of this set of wrapped lines.
            if( _iGlyphTotal <= 0 )
                return( false );

            // TODO: Make all offsets unsigned ints.
            if( _rgWrapOffsets == null || iOffset >= _rgWrapOffsets.Length )
                return( false );

            int iWrapSegm = _rgWrapOffsets[iOffset];

            if( iWrapSegm + iIncrement < 0 )
                return( false );
            if( iWrapSegm + iIncrement >= _rgWrapSegment.Count )
                return( false );

            iOffset = FindNearestOffset( iWrapSegm + iIncrement, iAdvance );
            
            return( true );
        }

        /// <summary>
        /// Find the nearest offset to the given advance on the given wrap segment.
        /// This advance is essentially the X coordinate on the screen translated
        /// to the world coordinates. The maximim value is the screen width.
        /// </summary>
        /// <param name="iWrapSegm">The particular wrapped line we are interested in.</param>
        /// <param name="iAdvance">The horizontal distance in "pixels" we are interested in.</param>
        /// <param name="iOffset">The glyph offset closest to the given advance.</param>
        public int FindNearestOffset( int iWrapSegm, int iAdvance ) {
            if( _rgWrapSegment.Count == 0 ) {
                return( 0 );
            }

            if( iWrapSegm >= _rgWrapSegment.Count )
                iWrapSegm = _rgWrapSegment.Count - 1;
            if( iWrapSegm < 0 )
                iWrapSegm = 0;

            int iAbsAdvance = _rgWrapSegment[iWrapSegm].Advance + iAdvance;

            int i = _iGlyphTotal;
            while( i > 0 && _rgAdvanceAbs[i] > iAbsAdvance ) {
                --i;
            }
            
            return( i );
        }

        /// <summary>
        /// Find the nearest offset to the given advance at the top or bottom most wrapped line.
        /// </summary>
        /// <param name="iIncrement">-1, top most line. +1 bottom most line.</param>
        /// <param name="iAdvance">Now many pixels to the right starting from left.</param>
        /// <param name="iOffset">Resulting glyph offset.</param>
        protected override int OffsetVerticalBound( int iIncrement, int iAdvance ) {
            int iOffset = 0;

            if( iIncrement > 0 ) {
                iOffset = FindNearestOffset( int.MaxValue, iAdvance );
            } else {
                iOffset = FindNearestOffset( int.MinValue, iAdvance );
            }

            return( iOffset );
        }
        /// <summary>
        /// Given a glyph offset return the graphics location. Return both X and Y
        /// graphics positions since the Y value is affected by any line wrapping that
        /// might be happening.
        /// </summary>
        /// <param name="iOffset">The glyph location we are interested in.</param>
        /// <returns>Returns the position of the glypn relative to the line.
        /// So, if you're at the top, the Y value is ZERO!!</returns>
        /// <remarks>Basically we are tracking the
        /// caret position and since the previous lines might be bigger than this line, when
        /// the user arrows up and down, we can expect overflow. Need to guard for that.
        /// </remarks>
        public override Point GlyphOffsetToPoint( int iOffset ) {
            if( _rgWrapOffsets == null )
                return( new Point( 0, 0 ) );

            Debug.Assert( _iGlyphTotal + 1 <= _rgWrapOffsets.Length );

            if( iOffset > _iGlyphTotal )
                iOffset = _iGlyphTotal;
            if( iOffset <= 0 )
                iOffset = 0;
			
			try {
				int iXDiff = 0;
				int iYDiff = 0;
				int iWLine = _rgWrapOffsets[iOffset];

				if( _rgWrapSegment.Count > iWLine ) { // small safety check.
					iXDiff = _rgWrapSegment[iWLine].Advance;
					iYDiff = (int)_flFontHeight * iWLine;
				}

				Point pntLocation = new Point( _rgAdvanceAbs[iOffset] - iXDiff, iYDiff );
                                             
				return( pntLocation );
			} catch( IndexOutOfRangeException ) {
                Debug.Assert( false, "GlyphOffsetToPoint() IndexOutOfRangeException exception." );
                return( new Point( 0, 0 ) );
			}
        } // end method

        /// <summary>
        /// Parses roll in sometime after the user changes the text so we must
        /// re-check all the color information. 
        /// TODO: We will need to remove selections and then re-add them as the 
        /// user changes selection. It's kind of stupid to re-do all color info 
        /// as we are forced to do now.
        /// </summary>
        /// <param name="rgSelections"></param>
        internal override void OnChangeFormatting( int iWidth, ICollection<ILineSelection> rgSelections ) {
			WrapSegmentsCreate( iWidth );
			
			base.OnChangeFormatting( iWidth, rgSelections );
        } // end method

        /// <summary>
        /// This is the old word wrapping algorithm. While not greedy it can't force 
		/// wrap long strings with no breaks;
        /// </summary>
        public bool WrapSegmentsCreateOld( int iDisplayWidth ) {
            if( _rgWrapOffsets == null )
                return( false );
            if( _rgAdvanceAbs == null )
                return( false );

            _rgWrapSegment.Clear();
            _rgWrapOffsets.Clear();

            // This won't work unless the words are in offset order. Greedy Algorithm.
            int iPrevWordCount = 0;
            int iWidthOffset   = 0; // Running window width offset.
            foreach( IColorRange oRange in Words ) {
                WrapSegment oSgmOld  = _rgWrapSegment[_rgWrapSegment.Count-1];
                int         iAdvance = oRange.Offset + oRange.Length;
                if( iAdvance < _rgAdvanceAbs.Length &&
                     _rgAdvanceAbs[iAdvance] > iWidthOffset + iDisplayWidth &&
                    iPrevWordCount > 0 ) // Only add new segment if we've got a word on the previous line.
                {
                    // Add a new wrap segment if the current word is too long
                    WrapSegment oSgmNew = new WrapSegment( oRange.Offset, _iGlyphTotal - oRange.Offset, _rgAdvanceAbs[oRange.Offset], 0 ); 

                    oSgmOld.Length  = oSgmNew.Offset - oSgmOld.Offset; 
                    iWidthOffset    = _rgAdvanceAbs[oRange.Offset];

                    _rgWrapSegment.Add( oSgmNew );
                    iPrevWordCount = 0;
                }
                ++iPrevWordCount;
            }

            // Fill in the WrapOffsets array. Set the whole word with the line offset, 
            // since color offsets may be within the word and completely different bounds.
            int iLength = 0;
            for( int iSegment=0; iSegment<_rgWrapSegment.Count; ++iSegment ) {
                IMemoryRange oSegm = _rgWrapSegment[iSegment];
                for( int i= oSegm.Offset; i < oSegm.Offset + oSegm.Length; ++i ) {
                    _rgWrapOffsets[i] = iSegment;
                }
                iLength += oSegm.Length;
            }
            Debug.Assert( iLength == _iGlyphTotal ); 
            Debug.Assert( _rgWrapSegment.Count > 0 );

            _rgWrapOffsets[_iGlyphTotal] = _rgWrapSegment.Count - 1;

            return( true );
        } // end method

        /// <summary>
        /// Use the Words formatting collection to calculate the word wrapping.
        /// We can use any rules to generate the words formatting collection.
        /// </summary>
		/// <remarks>This algorithm can break up long words.</remarks>
        /// <param name="iDisplayWidth">At present constant for whole paragraph, but if we wanted
        /// interesting text effects like wrap around objects this might be variable on a
        /// per line basis.</param>
        public virtual bool WrapSegmentsCreate( int iDisplayWidth) {
            if( _rgWrapOffsets == null )
                return( false );
            if( _rgAdvanceAbs == null )
                return( false );

            _rgWrapOffsets.Clear();
            _rgWrapSegment.Clear();

			int iSegment = 0; int iAdvanceLeft = 0; int iStart = 0; int iWordCount = 0; int iStopCount = 0;
			try {
				foreach( IColorRange oRange in Words ) {
					for( int iOffset = oRange.Offset; iOffset < oRange.Offset + oRange.Length; ++iOffset ) {
						if( _rgAdvanceAbs[iOffset + 1] - iAdvanceLeft > iDisplayWidth ) {
							// If start is in same word as last then we're trying to break a long word.
							int iLength = 0;
							if( iWordCount == iStopCount ) {
								iLength = iOffset - iStart;
								//if( iLength > 1 )
								//	iLength--;
							} else {
								iLength = oRange.Offset - iStart;
								//if( iOffset == oRange.Offset )
								//	iLength--;
							}
							_rgWrapSegment.Add( new WrapSegment( iStart, iLength, _rgAdvanceAbs[iStart], iSegment++ ) );

							iStopCount    = iWordCount;
							iStart       += iLength;
							iAdvanceLeft  = _rgAdvanceAbs[iStart];
						}
					}
					iWordCount++;
				}
			} catch( Exception oEx ) {
				Type[] rgError = { typeof( IndexOutOfRangeException ),
					               typeof( ArgumentOutOfRangeException ),
								   typeof( NullReferenceException ) };
				if( !rgError.Contains( oEx.GetType() ))
					throw;
			}
			// pick up the last part of the string.
			if (iStart < _iGlyphTotal) {
				_rgWrapSegment.Add(new WrapSegment(iStart,_iGlyphTotal - iStart,_rgAdvanceAbs[iStart],iSegment));
			}
			// stick up a default wrap if we found nothing.
			if ( _rgWrapSegment.Count == 0 ) {
				_rgWrapSegment.Add( new WrapSegment( 0, _iGlyphTotal, 0, 0 ) );
			}
			// populate the offset lookup stream.
            foreach( WrapSegment oSegm in _rgWrapSegment ) {
                for( int i= oSegm.Offset; i < oSegm.Offset + oSegm.Length; ++i ) {
                    _rgWrapOffsets[i] = oSegm.Wrap;
                }
            }
			// finally satisfy the +1 case.
            _rgWrapOffsets[_iGlyphTotal] = _rgWrapSegment.Count > 0 ? _rgWrapSegment.Count - 1 : 0;

            return( true );
        } // end method

        /// <summary>
        /// Take the color information and redistribute it among the wrapped lines.
        /// Called only by OnSize(). Most smaller elements won't need splitting, just
        /// those near the (right) edge. TODO: Perhaps I should make a cache of these split
        /// line range elements. Are they a stress on the memory manager?
        /// </summary>
        /// <param name="oElem">An element that might need splitting.</param>
        protected override void DiceAColor( IColorRange oElem ) {
            // Current color strategy relies on the leaves and not the trunks. Since we want
            // to allow selection == middle, we need to let the length of the input element
            // to be unrestrained. But offset needs to be checked.
            if( _rgWrapOffsets == null )
                return;

            int iRemainder = oElem.Length;
            do {
                if( ( oElem.Length < 0 ) || 
                    ( oElem.Offset < 0 ) ||
                    ( oElem.Offset >= _rgWrapOffsets.Length ) )
                    return;

                int iLine = _rgWrapOffsets[oElem.Offset];
                if( iLine >= _rgWrapSegment.Count )
                    return;

                IMemoryRange oSegm = _rgWrapSegment[iLine];

                if( oSegm.Offset > oElem.Offset )
                    return; // BUG: Getting errors once in awhile. Since I changed from stream to line/offset selection. Debug this.
                
                // It's possible we have a malformed element with an offset beginning beyond
                // the end of the line. Catch that here and bail out.
                int iMaxLength = oSegm.Length - ( oElem.Offset - oSegm.Offset );
                if( iMaxLength < 1 || iMaxLength > oSegm.Length )
                    return;

                if( oElem.Length > iMaxLength ) {
                    IColorRange oNext = new ColorRange( oElem.Offset, iMaxLength, oElem.ColorIndex );
                    _rgSlicedColor.Add( oNext );
                    // Create a new element that we need to loop back around and take care of remainder.
                    iRemainder -= iMaxLength;
                    oElem = new ColorRange( oNext.Offset + iMaxLength, iRemainder, oNext.ColorIndex );
                } else {
                    // Just add the unmodified element directly into the sliced color bag.
                    iRemainder = 0;
                    _rgSlicedColor.Add( oElem );
                }
            } while( iRemainder > 0 );
        } // end method

        /// <summary>
        /// Plunder the given enumerator to render the text with the proper color. The assumption
        /// is that any one IColorRange is going to be on a single line, and will never straddle
        /// multiple lines. See DiceAColor
        /// </summary>
        /// <param name="hDC">Display Context</param>
        /// <param name="pntEditAt">Origin of the Edit.</param>
        /// <param name="iColor">Color we are currently displaying.</param>
        public override void Render(
            IntPtr                   hDC, 
            IntPtr                   hScriptCache,
            PointF                   pntEditAt, 
            int                      iColor,
            RECT ?                   rcClip )
        {
            if( _rgGlyphs == null ) // If this is null, then nothing will be ready.
                return;

            RECT rcClip2 = new RECT();
            uint uiFlags = PgUniscribe.ETO_GLYPH_INDEX;

            if( rcClip.HasValue ) { 
                uiFlags |= PgUniscribe.ETO_CLIPPED;
                rcClip2 = rcClip.Value;
            }

            unsafe {
                fixed( UInt16 * pGlyphs = &_rgGlyphs[0] ) {
                    fixed( int * pAdvanceOff = &_rgAdvanceWidths[0] ) {
                        fixed( GOFFSET * pGOffset = &_rgGOffset[0] ) {
							foreach( IColorRange oElem in this ) {
                                if( oElem.ColorIndex == iColor ) {
                                    int iMaxLength = _iGlyphTotal - oElem.Offset;
                                    int iUseLength = oElem.Length > iMaxLength ? iMaxLength : oElem.Length;

                                    if( iUseLength > 0 && oElem.Offset < _iGlyphTotal ) { 
                                        int iXDiff = 0;
                                        int iYDiff = 0;
                                        int iWLine = _rgWrapOffsets[oElem.Offset];
                                        if( _rgWrapSegment.Count > iWLine ) { // small safety check.
                                            iXDiff = _rgWrapSegment[iWLine].Advance;
                                            iYDiff = (int)_flFontHeight * iWLine;
                                        }
                                        PgUniscribe.ExtTextOutW( 
                                            hDC, 
                                            (Int32)( pntEditAt.X + _rgAdvanceAbs[oElem.Offset] - iXDiff ), 
                                            (Int32)( pntEditAt.Y + iYDiff ), 
                                            uiFlags, 
                                            &rcClip2, 
                                            &pGlyphs[oElem.Offset], 
                                            (uint)iUseLength, // NOTE: Can't exceed 8192
                                            &pAdvanceOff[oElem.Offset] ); 
                                        //PgUniscribe.ScriptTextOut(
                                        //    hDC,
                                        //    ref hScriptCache,
                                        //    (Int32)( pntEditAt.X + _rgAdvanceAbs[oElem.Offset] - iXDiff ),
                                        //    (Int32)( pntEditAt.Y + iYDiff ),
                                        //    0,
                                        //    null, // prect
                                        //    null, // script analysis
                                        //    null,
                                        //    0,
                                        //    &pGlyphs[oElem.Offset],
                                        //    iUseLength,
                                        //    &pAdvanceOff[oElem.Offset],
                                        //    null,
                                        //    pGOffset
                                        //);
                                        
                                    }
                                }
                            } // end while
                        } // end fixed
                    }
                }// end fixed
            } // end unsafe
        } // end method

        public override int ReAllocGlyphBuffers( int p_iCount ) {
            if( p_iCount < 0 ) {
                return( 0 );
            }

            if( base.ReAllocGlyphBuffers( p_iCount ) != p_iCount )
                return( 0 );

            try {
                Array.Resize<int>( ref _rgWrapOffsets, p_iCount + 1 );
            } catch( OutOfMemoryException ) {
                return( 0 );
            } catch( ArgumentOutOfRangeException ) {
                return( 0 );
            }

            return( p_iCount );
        }
    }
}
