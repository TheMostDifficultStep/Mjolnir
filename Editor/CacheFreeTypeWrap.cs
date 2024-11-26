using System;
using System.Drawing;
using System.Diagnostics;

using SkiaSharp;

using Play.Interfaces.Embedding;
using Play.Rectangles;

namespace Play.Edit {
    /// <summary>
    /// Cache a single line of info but allow for wrap around when displaying on a narrow window.
    /// </summary>
    /// <remarks>
    /// I don't strictly need this object since a line Range would work minus the Advance
    /// information. I can always look up the advance using the offset. So I'm going to
    /// think about this for a bit.
    /// </remarks>
    public class FTCacheWrap : FTCacheLine {
        int _iWrapCount = 0;

        public FTCacheWrap( Line oLine ) : base( oLine ) {
        }

        override public int Height {
            get { 
                return( (int)(_iWrapCount < 0 ? base.Height : ( _iWrapCount + 1 ) * base.Height ) );
            }
        }

        /// <summary>
        /// Feed the Cache line the new width of the display area. Rewrap words.
        /// </summary>
        public override void OnChangeSize( int iWidth ) {
			WrapSegments( iWidth );
        }

        /// <summary>
        /// Move up or down based on the previous advance. 
        /// </summary>
        /// <param name="iIncrement">Direction of travel, positive is down, negative is up.</param>
        /// <param name="flAdvance">Previous "pixel" offset on a given line. The wrapped value.</param>
        /// <param name="iOffset">Closest character we can find to the given offset on a line above or below.</param>
        /// <returns></returns>
        protected override bool NavigateVertical( int iIncrement, float flAdvance, ref int iOffset ) {
            try {
                // Any movement will put us out of this set of wrapped lines.
                if( _rgClusters.Count < 1 )
                    return( false );

                int iWrapSegm = _rgClusters[iOffset].Segment;

                if( iWrapSegm + iIncrement < 0 )
                    return( false );
                if( iWrapSegm + iIncrement > _iWrapCount )
                    return( false );

                iOffset = FindNearestOffset( iWrapSegm + iIncrement, flAdvance );
            } catch( Exception oEx ) {
                Type[] rgErrors = { typeof( ArgumentOutOfRangeException ),
                                    typeof( NullReferenceException ) };
                if( rgErrors.IsUnhandled( oEx ) )
                    throw;
            }
            
            return( true );
        }

        /// <summary>
        /// Find the nearest offset to the given advance on the given wrap segment.
        /// This advance is essentially the X coordinate on the screen translated
        /// to the world coordinates. The maximim value is the screen width.
        /// </summary>
        /// <param name="iSegment">The particular segment we are interested in.</param>
        /// <param name="flAdvance">The horizontal distance in "pixels" we are interested in.</param>
        protected int FindNearestOffset( int iSegment, float flAdvance ) {
            if( _rgClusters.Count < 1 )
                return 0;

            if( iSegment > _iWrapCount )
                iSegment = _iWrapCount;
            if( iSegment < 0 )
                iSegment = 0;

            try {
                float flAdvanceLeft = flAdvance;
                int ClusterCompare( PgCluster oTry ) {
                    if( iSegment < oTry.Segment )
                        return -1;
                    if( iSegment > oTry.Segment )
                        return  1;
                    if( flAdvanceLeft < oTry.AdvanceLeft )
                        return -1;
                    if( flAdvanceLeft >= oTry.AdvanceLeft + ( oTry.AdvanceOffs / 2 ) )
                        return 1;
                    return 0;
                }
                int iIndex = FindStuff<PgCluster>.BinarySearch( _rgClusters, 0, _rgClusters.Count - 1, ClusterCompare );

                if( iIndex < 0 )
                    iIndex = ~iIndex; // But if miss, this element is on the closest edge.

				return _rgClusters[iIndex].SourceRange.Offset; 
            } catch( Exception oEx ) {
                Type[] rgErrors = { typeof( ArgumentOutOfRangeException ),
                                    typeof( NullReferenceException ) };
                if( rgErrors.IsUnhandled( oEx ) )
                    throw;

                return 0;
            }
        }

        /// <summary>
        /// Find the nearest offset to the given advance at the top or bottom most wrapped line.
        /// </summary>
        /// <param name="iIncrement">-1, top most line. +1 bottom most line.</param>
        /// <param name="flAdvance">Now many pixels to the right starting from left.</param>
        /// <param name="iOffset">Resulting glyph offset.</param>
        protected override int OffsetVerticalBound( int iIncrement, float flAdvance ) {
            int iOffset = 0;

            if( iIncrement > 0 ) {
                iOffset = FindNearestOffset( int.MaxValue, flAdvance );
            } else {
                iOffset = FindNearestOffset( int.MinValue, flAdvance );
            }

            return( iOffset );
        }

        /// <summary>
        /// Find the character offset of the mouse position.
        /// I hate the regular behavor of selection
        /// across ltr through rtl regions. At present our selection code doesn't
        /// check the direction of the block. We need to check the boundaries of
        /// a selection avoid copy/cut problems.
        /// </summary>
        /// <param name="pntWorld">Location relative to the upper left of line 
        /// collection which is 0, 0. Remove any window positioning before passing
        /// coordinates to us.</param>
        /// <param name="iTop">Top (left) of our cache element, relative to the
        /// pntWorld.Y</param>
        /// <returns>Character offset of the given location. 0 if left of the
        /// first element.</returns>
        public override int GlyphPointToOffset( int iTop, SKPointI pntWorld ) {
			try {
				int iSegment;

				// Since the lines are wrapped, find which segment we think
                // the desired offset is in.
				for( iSegment = 0; iSegment <= _iWrapCount; ++iSegment ) {
					if( ( iTop + ( iSegment + 1 ) * base.Height ) > pntWorld.Y )
						break;
				}

                return FindNearestOffset( iSegment, pntWorld.X );
            } catch( Exception oEx ) {
                Type[] rgErrors = { typeof( ArgumentOutOfRangeException ),
                                    typeof( NullReferenceException ),
                                    typeof( ArgumentException ) };
                if( rgErrors.IsUnhandled( oEx ) )
                    throw;

                Debug.Fail( "GlyphPointToOffset() index exception." );
				return 0;
			}
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
            if( _rgClusters.Count < 1 || _rgClusterMap.Count < 1 )
                return new Point( 0, 0 );

            if( iOffset > _rgClusterMap.Count - 1 )
                iOffset = _rgClusterMap.Count - 1; // BUG, Set the carat to the right of the last character.
            if( iOffset < 0 )
                iOffset = 0;
			
			try {
                PgCluster oCluster    = _rgClusters[_rgClusterMap[iOffset]];
                int       iYOffset    = base.Height * oCluster.Segment;
				Point     pntLocation = new Point( (int)oCluster.AdvanceLeft, iYOffset );
                                             
				return pntLocation;
            } catch( Exception oEx ) {
                Type[] rgErrors = { typeof( ArgumentOutOfRangeException ),
                                    typeof( NullReferenceException ) };
                if( rgErrors.IsUnhandled( oEx ) )
                    throw;

                Debug.Fail( "GlyphOffsetToPoint() IndexOutOfRangeException exception." );
                return new Point( 0, 0 );
			}
        } // end method

        /// <summary>
        /// Just a dumb wrap when any character overshoots the display
        /// visible or not word wrapper. Just wrap whenever hit an edge.
        /// We special case the first cluster since we want to advance AFTER we've set it's 
        /// first column valuse. Each column must have at least ONE character on it.
        /// </summary>
        [Obsolete]protected void WrapSegmentSimple( int iDisplayWidth ) {
            float flAdvance = 0;
            _iWrapCount  = 0;

            if( _rgClusters.Count > 0 ) {
                flAdvance = _rgClusters[0].Increment( flAdvance, _iWrapCount );
            }

            for( int iCluster = 1; iCluster < _rgClusters.Count; ++iCluster ) {
                if( flAdvance + _rgClusters[iCluster].AdvanceOffs > iDisplayWidth ) {
                    flAdvance = 0;
                    _iWrapCount++;
                }
                flAdvance = _rgClusters[iCluster].Increment( flAdvance, _iWrapCount );
            }
        }

        protected void JustifyDone( Span<float> rgStart ) {
            foreach( PgCluster oCluster in _rgClusters ) {
                if( oCluster.Segment >= rgStart.Length )
                    break;
                if( rgStart[oCluster.Segment] > 0 ) {
                    oCluster.AdvanceLeft += rgStart[oCluster.Segment];
                }
            }
        }

        protected void JustifyLine( Span<float> rgStart, int iSegment, 
                                    int iDisplayWidth, float flAdvance ) 
        {
            if( iSegment >= rgStart.Length )
                return;

            float flOffset = 0;

            switch( Justify ) {
                case Align.Right:
                    flOffset = iDisplayWidth - flAdvance ;
                    break;
                case Align.Center:
                    flOffset = ( iDisplayWidth - flAdvance ) / 2F;
                    break;
            }

            if( flOffset < 0 )
                flOffset = 0;

            rgStart[iSegment] = flOffset;
        }

        /// <summary>
        /// Start reading a cluster run, keep at it as long as the visibility remains the same
        /// and the text width is less than the DisplayWidth.
        /// </summary>
        /// <remarks>No matter you slice it, you have to read ahead and discard some
        /// results at times. The world builder helps acccomplish that.</remarks>
        /// <returns>Length of the run.</returns>
        private int WordBuilder( int iStart, float flAdvance, int iDisplayWidth, int iPush,
                                 out float flWidth, out bool fVisible
        ) {
            flWidth = 0;
            fVisible = _rgClusters[iStart].IsVisible;

            int iIndex = iStart;
            while( iIndex < _rgClusters.Count ) {
                PgCluster oCluster = _rgClusters[iIndex];

                if( fVisible != oCluster.IsVisible )
                    break;

                // Allow one character when in 0th column of wrap.
                if( flAdvance + oCluster.AdvanceOffs > iDisplayWidth && iIndex - iStart > iPush )
                    break;
                // If we've got more than one character, break on punctuation.
                if( oCluster.IsPunctuation && iIndex - iStart > 1 )
                    break;

                oCluster.AdvanceLeft = flAdvance;
                oCluster.Segment     = _iWrapCount;

                flWidth   += oCluster.AdvanceOffs;
                flAdvance += oCluster.AdvanceOffs;
                fVisible  &= oCluster.IsVisible;

                iIndex++;

                // Punctuation is always a single "word", run is 1.
                if( oCluster.IsPunctuation )
                    break;
            }

            return iIndex - iStart;
        }

        /// <summary>
        /// Nice new Wrapper, works without my text parser! Also can justify
        /// upto 10 logical rows of wrapped text per physical line. Ignores
        /// trailing white space. Not having to set up a (complex) parser is a
        /// big deal, so I'm going with this.
        /// </summary>
        /// <remarks> ">=" is an example of a punctuation that I would like to
        /// keep together upto the point where the display width is on character
        /// wide. Even my (complex) parser at present, does not recognize 
        /// greater than or equal as a single unit, so it's a wash.
        /// </remarks>
        /// <param name="iDisplayWidth"></param>
        protected override void WrapSegments( int iDisplayWidth ) {
            try {
                _iWrapCount = 0;

                if( _rgClusters.Count < 1 )
                    return;

                float       flAdvance = 0;
                float       flLastVis = 0;
                Span<float> rgStart   = stackalloc float[10];

                for( int iIndex = 0; iIndex < _rgClusters.Count;  ) {
                    bool  fVisible = true;
                    float flExtent = 0;

                    // There must be at least one char in the first column of the wrapped text.
                    // The world builder won't pass back a "word" wider than the display width.
                    // It will break and pass what will fit.
                    int iRun = WordBuilder( iIndex, flAdvance, iDisplayWidth, 0, out flExtent, out fVisible );

                    if( fVisible || iIndex == 0 ) {
                        flAdvance += flExtent;
                        flLastVis = flAdvance;
                    }
                    iIndex += iRun;

                    // From here on out, grab the rest. If it won't fit we'll bail from this loop.
                    // And try again in the first column of the next wrapped row.
                    while( iIndex < _rgClusters.Count ) {
                        iRun = WordBuilder( iIndex, flAdvance, iDisplayWidth, int.MaxValue, out flExtent, out fVisible );

                        if( flAdvance + flExtent > iDisplayWidth ) {
                            JustifyLine( rgStart, _iWrapCount, iDisplayWidth, flLastVis );
                            _iWrapCount++;
                            flAdvance = 0;
                            flLastVis = 0;
                            break;
                        }
                        flAdvance += flExtent;
                        if( fVisible )
                            flLastVis = flAdvance;

                        iIndex += iRun;
                    }
                }
                // Don't forget to patch up our trailing EOL glyph that isn't in the source. See Update()
                _rgClusters[_rgClusters.Count-1].AdvanceLeft = flAdvance;
                _rgClusters[_rgClusters.Count-1].Segment     = _iWrapCount;

                JustifyLine( rgStart, _iWrapCount, iDisplayWidth, flLastVis );
                JustifyDone( rgStart );
            } catch( Exception oEx ) {
                Type[] rgError = { typeof( IndexOutOfRangeException ),
                                   typeof( ArgumentOutOfRangeException ),
                                   typeof( NullReferenceException ),
                                   typeof( ArgumentOutOfRangeException ) };
                if( rgError.IsUnhandled(oEx) )
                    throw;

                base.WrapSegments( iDisplayWidth );
            }
        }
    } // end class
}
