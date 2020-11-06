using System;
using System.Drawing;
using System.Diagnostics;

using SkiaSharp;

using Play.Parse.Impl;
using Play.Interfaces.Embedding;

namespace Play.Edit {
    /// <summary>
    /// I don't strictly need this object since a line Range would work minus the Advance
    /// information. I can always look up the advance using the offset. So I'm going to
    /// think about this for a bit.
    /// </summary>
    /// <summary>
    /// Cache a single line of info but allow for wrap around when displaying on a narrow window.
    /// </summary>
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
			WrapSegmentsCreate( iWidth );
        }

        /// <summary>
        /// Move up or down based on the previous advance. 
        /// </summary>
        /// <param name="iIncrement">Direction of travel, positive is down, negative is up.</param>
        /// <param name="iAdvance">Previous "pixel" offset on a given line. The wrapped value.</param>
        /// <param name="iOffset">Closest character we can find to the given offset on a line above or below.</param>
        /// <returns></returns>
        protected override bool NavigateVertical( int iIncrement, int iAdvance, ref int iOffset ) {
            try {
                // Any movement will put us out of this set of wrapped lines.
                if( _rgClusters.Count < 1 )
                    return( false );

                int iWrapSegm = _rgClusters[iOffset].Segment;

                if( iWrapSegm + iIncrement < 0 )
                    return( false );
                if( iWrapSegm + iIncrement > _iWrapCount )
                    return( false );

                iOffset = FindNearestOffset( iWrapSegm + iIncrement, iAdvance );
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
        /// <param name="iAdvance">The horizontal distance in "pixels" we are interested in.</param>
        public int FindNearestOffset( int iSegment, int iAdvance ) {
            if( _rgClusters.Count < 1 )
                return 0;

            if( iSegment > _iWrapCount )
                iSegment = _iWrapCount;
            if( iSegment < 0 )
                iSegment = 0;

            try {
                int iAdvanceLeftEm = iAdvance << 6;
                int ClusterCompare( PgCluster oTry ) {
                    if( iSegment < oTry.Segment )
                        return -1;
                    if( iSegment > oTry.Segment )
                        return  1;
                    if( iAdvanceLeftEm < oTry.AdvanceLeftEm )
                        return -1;
                    if( iAdvanceLeftEm >= oTry.AdvanceLeftEm + ( oTry.AdvanceOffsEm >> 1 ) )
                        return 1;
                    return 0;
                }
                int iIndex = FindStuff<PgCluster>.BinarySearch( _rgClusters, 0, _rgClusters.Count - 1, ClusterCompare );

                if( iIndex < 0 )
                    iIndex = ~iIndex; // But if miss, this element is on the closest edge.

				return _rgClusters[iIndex].Source.Offset; 
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
        /// Find the character offset of the mouse position. I really should be
        /// using ScriptXtoCP(), but I hate the regular behavor of selection
        /// across ltr through rtl regions. At present our selection code doesn't
        /// check the direction of the block. We need to check the boundaries of
        /// a selection avoid copy/cut problems.
        /// </summary>
        /// <param name="pntWorld">Location relative to the upper left of line 
        /// collection which is 0, 0. Remove any window positioning before passing
        /// coordinates to us.</param>
        /// <returns>Character offset of the given location. 0 if left of the
        /// first element.</returns>
        public override int GlyphPointToOffset( SKPointI pntWorld ) {
			try {
				int iSegment;

				// Since the lines are wrapped, find which segment we think
                // the desired offset is in.
				for( iSegment = 0; iSegment <= _iWrapCount; ++iSegment ) {
					if( ( Top + ( iSegment + 1 ) * base.Height ) > pntWorld.Y )
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
                int       iYOffset    = (int)base.Height * oCluster.Segment;
				Point     pntLocation = new Point( oCluster.AdvanceLeftEm >> 6, iYOffset );
                                             
				return( pntLocation );
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
        /// Load the current color range into the clusters list as much as possible.
        /// </summary>
        /// <remarks>Our formatting info can contain a fake EOL word that is outside the cluster limits.</remarks>
        private bool LoadWord( IColorRange oWord, int iDisplayWidth, ref int iAdvance, ref int iIndex ) {
            while( iIndex < oWord.Offset + oWord.Length ) {
                if( iIndex >= _rgClusters.Count )
                    return true;

                PgCluster oCluster = _rgClusters[iIndex];

                // Put at least ONE character on a line when advance is zero.
                if( oCluster.IsVisible && iAdvance + oCluster.AdvanceOffsEm > iDisplayWidth && iAdvance > 0 )
                    return false;

                iAdvance = oCluster.Increment( iAdvance, _iWrapCount );
                ++iIndex;
            }
            return true;
        }

        public override void WrapSegmentsCreate( int iDisplayWidth ) {
            if( _rgClusters.Count < 1 )
                return;

            // We could actually just wrap without word info, look into that later.
            if( Words.Count == 0 ) {
                base.WrapSegmentsCreate( iDisplayWidth );
                return;
            }

            int iAdvance  = 0;
            iDisplayWidth <<= 6;
            _iWrapCount   = 0;

            try {
				foreach( IPgWordRange oRange in Words ) {
                    int iIndex = oRange.Offset;
                    int iPass  = _iWrapCount;
                    while( !LoadWord( oRange, iDisplayWidth, ref iAdvance, ref iIndex ) ) {
                        if( iPass == _iWrapCount )  // Reset only for the first wrap.
                            iIndex = oRange.Offset;
                        iAdvance = 0;
                        _iWrapCount++;
                    }
                }
                // Don't forget to patch up our trailing glyph that isn't in the source.
                _rgClusters[_rgClusters.Count-1].AdvanceLeftEm = iAdvance;
                _rgClusters[_rgClusters.Count-1].Segment       = _iWrapCount;
            } catch( Exception oEx ) {
                Type[] rgError = { typeof( IndexOutOfRangeException ),
                                   typeof( ArgumentOutOfRangeException ),
                                   typeof( NullReferenceException ),
                                   typeof( ArgumentOutOfRangeException ) };
                if( rgError.IsUnhandled(oEx) )
                    throw;

                base.WrapSegmentsCreate( iDisplayWidth );
            }
        }
    } // end class
}
