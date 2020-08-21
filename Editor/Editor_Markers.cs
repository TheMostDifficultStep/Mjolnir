using System.Collections.Generic;

using Play.Parse.Impl;

namespace Play.Edit
{
    /// <summary>
    /// Utility operations for markers in our editor and the are quite independent of the Line class.
    /// </summary>
    public static class Marker {
        /// <summary>
        /// See if this range intersects. 
        /// </summary>
        /// <param name="oTestRange"></param>
        /// <remarks>
        ///   0123456789...
        /// 1)  11111 +   "test"
        ///     + 22222   "this"
        /// ------------------
        /// 2)  - 11111
        ///     22222 -
        /// ------------------
        /// 3)  - 1111 +
        ///     22222222
        /// ------------------
        /// 4)  11111111
        ///     + 2222 -
        /// ------------------
        /// 5)  11111         (no overlap)
        ///           22222
        /// ------------------
        /// 6)        11111   (no overlap)
        ///     22222
        /// </remarks>
        public static bool Intersect(
            IMemoryRange oThis,
            IMemoryRange oTest 
        ) {
            int iTestEnd       = oTest.Offset + oTest.Length;
            int iThisEnd       = oThis.Offset + oThis.Length;
            int iGreatestStart = oThis.Offset >= oTest.Offset ? oThis.Offset : oTest.Offset;
            int iLeastEnd      = iThisEnd     <= iTestEnd ? iThisEnd : iTestEnd;
            int iOverlap       = iLeastEnd - iGreatestStart; // iGreatestStart >= iLeastEnd, no overlap.
            int iLeftLength    = 0;
            int iRightLength   = 0;

            if( iOverlap <= 0 )
                return ( false ); // No overlap.

            iLeftLength  = oThis.Offset - oTest.Offset;
            iRightLength = iTestEnd - iThisEnd;
            
            if( iLeftLength > 0 ) {
                oThis.Offset = oTest.Offset;
                if( iRightLength > 0 ) {
                    oThis.Length = iOverlap;
                } else {
                    oThis.Length = 0;
                }
            } else {
                oThis.Length -= iOverlap;
            }
            
            // modified.
            return ( true );
        }
        
        /// <summary>
        /// For all the non intersecting ranges past the deletion point, shift into the gap.
        /// </summary>
        /// <param name="oRange">The item to possibly move/shift.</param>
        /// <param name="oDelete">The range of text that was deleted.</param>
        public static void ShiftDelete( IMemoryRange oRange, IMemoryRange oDelete ) {
            if( !Intersect( oRange, oDelete ) )
            {
                // Move everything after the delete over.
                if( oRange.Offset > oDelete.Offset ) {
                    oRange.Offset -= oDelete.Length;
                }
            }
        }

        /// <summary>
        /// Update the given MemoryRange based on an edit somewhere. See the return for the rules!
        /// </summary>
        /// <param name="iIndex">The position where something has happened.</param>
        /// <param name="iLength">The length of the modification. Only positive values handled at present.</param>
        public static void ShiftInsert( IMemoryRange oRange, int iIndex, int iShift) {
            if( iShift > 0 ) {
                // either need to move the element, grow it or leave it alone.
                if( iIndex < oRange.Offset ) {
                    oRange.Offset += iShift;
                } else {
                    if( oRange.Length > 0 ) {
                        if( iIndex < oRange.Offset + oRange.Length ) {
                            // Check if the index is somewhere within.
                            oRange.Length += iShift;
                        }
                    } else {
                        if( iIndex == oRange.Offset ) {
                            oRange.Offset += iShift;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Split the formatting at the given index. Anything to the 'right' is put into
        /// the 'nextline' formatting bag.
        /// </summary>
        /// <param name="rgCurrLine">Formatting to split.</param>
        /// <param name="iIndex">The index to begin splitting.</param>
        /// <param name="rgNextLine">The bag to put the formatting on the right.</param>
        public static void SplitNext( 
            ICollection<IColorRange> rgCurrLine, 
            int                      iIndex, 
            ICollection<IColorRange> rgNextLine )
        {
            if( rgCurrLine.Count > 0 ) {
                // Add the shifted elements to the new line.
                foreach( IColorRange oRange in rgCurrLine ) {
                    if( oRange.Offset < iIndex && iIndex < oRange.Offset + oRange.Length ) {
                        IColorRange oSplit = new ColorRange(0, oRange.Offset + oRange.Length - iIndex, oRange.ColorIndex);

                        oRange.Length = iIndex - oRange.Offset;
                        rgNextLine.Add(oSplit);
                    }
                    if( oRange.Offset >= iIndex ) {
                        oRange.Offset -= iIndex;
                        rgNextLine.Add(oRange);
                    }
                }

                // Then remove the moved elements from the old line.
                // The elements that were split will be discarded!
                foreach( IColorRange oRange in rgNextLine ) {
                    rgCurrLine.Remove(oRange);
                }
            }
        }
    } // End class
}
