using System;
using System.Collections.Generic;
using System.Drawing;
using System.Diagnostics;

using SkiaSharp;

using Play.Parse.Impl;
using Play.Interfaces.Embedding;
using System.Text;

namespace Play.Edit {
    /// <summary>
    /// I don't strictly need this object since a line Range would work minus the Advance
    /// information. I can always look up the advance using the offset. So I'm going to
    /// think about this for a bit.
    /// </summary>
    /// <summary>
    /// Cache a single line of info but allow for wrap around when displaying on a narrow window.
    /// </summary>
    public class FTCacheWrap : FTCacheLine
    {
        int _iWrapCount = 0;

        public FTCacheWrap( Line oLine ) : base( oLine ) 
        {
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
            if( oWord == null )
                return true;

            while( iIndex < oWord.Offset + oWord.Length ) {
                if( iIndex >= _rgClusters.Count )
                    return true;
                // We only fail if the index is non-zero. That'll wrap us around and
                // then our index will be zero where we always put at least one word up.
                if( iAdvance + _rgClusters[iIndex].AdvanceOffsEm > iDisplayWidth && iIndex != 0 )
                    return false;

                _rgClusters[iIndex].Segment       = _iWrapCount;
                _rgClusters[iIndex].AdvanceLeftEm = iAdvance;

                iAdvance += _rgClusters[iIndex].AdvanceOffsEm;
                ++iIndex;
            }
            return true;
        }

        /// <summary>
        /// We can use any rules to generate the formatting collection BUT the range's 
        /// must be in order.
        /// </summary>
		/// <remarks>This algorithm can break up long words. If a long word is encountered,
        /// it is reset to the left and wrapped continuiously from that point on.</remarks>
        /// <param name="iDisplayWidth">At present constant for whole paragraph, but if we wanted
        /// interesting text effects like wrap around objects this might be variable on a
        /// per line basis.</param>
        /// <remarks>TODO: Our line breaking is limited by the word breaks. That's not as good
        /// as spacing, since periods are getting placed on a new line. I'll probably go
        /// back to a word break; parser. But not yet.</remarks>
        public virtual void WrapSegmentsCreateOld( int iDisplayWidth ) {
            if( _rgClusters.Count < 1 )
                return;

            iDisplayWidth <<= 6;

            if( iDisplayWidth <= 0 )
                return;

            IEnumerator<IColorRange> eWords   = GetEnumerator();
			int                      iAdvance = 0;

			try {
                _iWrapCount = 0;
                eWords.MoveNext();

                int iIndex = 0; // It's possible for one word to span many lines.
                while( true ) {
                    int iWordCount = 0;
                    while( true ) { 
                        if( LoadWord(  eWords.Current, iDisplayWidth, ref iAdvance, ref iIndex ) ) {
                            if( !eWords.MoveNext() ) {
                                ++_iWrapCount;
                                return;
                            }
                            iWordCount++;
                        } else {
                            // If there are already words on the line, Reset the index and 
                            // re-write current word out on the next line.
                            if( iWordCount > 1 )
                                iIndex =  eWords.Current.Offset;
                            break;
                        }
                    }
                    iAdvance = 0;
                    ++_iWrapCount;
                    if( _iWrapCount > 1000 )
                        throw new ApplicationException( "Just stop at some arbitrary 'too big' value." );
                }
			} catch( Exception oEx ) {
				Type[] rgError = { typeof( IndexOutOfRangeException ),
					               typeof( ArgumentOutOfRangeException ),
								   typeof( NullReferenceException ),
                                   typeof( InvalidCastException ),
                                   typeof( ApplicationException ) };
				if( rgError.IsUnhandled( oEx ))
					throw;
			}
        } // end method

        public virtual void WrapSegmentsCreate( int iDisplayWidth ) {
            if( _rgClusters.Count < 1 )
                return;

            iDisplayWidth <<= 6;

            _iWrapCount = 0;
            int iAdvance   = 0;
            int iBreak     = 1;

            try {
                for( int iCluster = 0; iCluster < _rgClusters.Count; ++iCluster ) {
                    PgCluster oCluster = _rgClusters[iCluster];
                    if( Rune.IsWhiteSpace( (Rune)_rgGlyphs[oCluster.Glyphs.Offset].CodePoint ) ) 
                        iBreak = iCluster;

                    if( iAdvance + oCluster.AdvanceOffsEm > iDisplayWidth && iCluster - iBreak > 0 ) {
                        _iWrapCount++;
                        iAdvance = 0;
                        for( int iSub = iBreak + 1; iSub < iCluster; ++iSub ) {
                            iAdvance = _rgClusters[iSub].Increment( iAdvance, _iWrapCount );
                        }
                        iBreak = iCluster;
                    }

                    iAdvance = oCluster.Increment( iAdvance, _iWrapCount );
                }
                //_rgWrapSegment.Add( _rgClusters.Count );
            } catch( Exception oEx ) {
                Type[] rgError = { typeof( IndexOutOfRangeException ),
                                   typeof( ArgumentOutOfRangeException ),
                                   typeof( NullReferenceException ),
                                   typeof( ArgumentOutOfRangeException ) };
                if( rgError.IsUnhandled(oEx) )
                    throw;

                for( int iCluster = 0; iCluster < _rgClusters.Count; ++iCluster ) {
                    iAdvance = _rgClusters[iCluster].Increment( iAdvance, 0 );
                }
            }
        }
    } // end class
}
