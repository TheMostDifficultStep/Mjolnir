using System.Collections.Generic;

using Play.Parse;

namespace Play.Edit
{
    /// <summary>
    /// Utility operations for markers in our editor and the are quite independent of the Line class.
    /// </summary>
    public static class Marker {
        /// <summary>
        /// See if this range intersects. 
        /// </summary>
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
        /// <param name="oThis"></param>
        /// <param name="oTest"></param>
        public static bool Intersect(
            IMemoryRange oThis,
            IMemoryRange oTest 
        ) {
            int iTestEnd       = oTest.Offset + oTest.Length;
            int iThisEnd       = oThis.Offset + oThis.Length;
            int iGreatestStart = oThis.Offset >= oTest.Offset ? oThis.Offset : oTest.Offset;
            int iLeastEnd      = iThisEnd     <= iTestEnd ? iThisEnd : iTestEnd;
            int iOverlap       = iLeastEnd - iGreatestStart; // iGreatestStart >= iLeastEnd, no overlap.

            if( iOverlap <= 0 )
                return ( false ); // No overlap.

            int iLeftLength  = oThis.Offset - oTest.Offset;
            int iRightLength = iTestEnd - iThisEnd;
            
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
        /// One marker shifter to rule them all! This should be able to replace all
        /// over marker shifters.
        /// </summary>
        /// <param name="oRange">The marker in question.</param>
        /// <param name="iDelOff">The start of the deleted section or caret.</param>
        /// <param name="iDelLen">Length of section, 0 is ok.</param>
        /// <param name="iDiff">If inserted text is less length than deleted text,
        /// this value is negative, else it is positive the difference.</param>
        public static void ShiftReplace( IMemoryRange oRange, int iDelOff, int iDelLen, int iDiff ) {
            // If the Range RHS is less than iDelOff then need to do nothing.
            int iRangeEnd = oRange.Offset + oRange.Length;
            int iDelEnd   = iDelOff + iDelLen;

            if( iRangeEnd >= iDelOff && oRange.Offset < iDelEnd ) {
                // ... there is overlap
                if( oRange.Offset < iDelOff ) {
                    // ...Range start is less than the delete start
                    if( iRangeEnd >= iDelEnd + iDiff ) 
                        oRange.Length += iDiff;                      // End was outside the delete
                    else
                        oRange.Length = iDelOff - oRange.Offset;     // End was inside the delete
                } else {
                    // ... Range start is somewhere inside of the delete.
                    oRange.Offset = iDelEnd + iDiff;
                    if( iRangeEnd < iDelEnd + iDiff )
                        oRange.Length = 0;                           // End was inside the delete
                    else
                        oRange.Length = iRangeEnd - iDelEnd + iDiff; // End was outside the delete
                }
                // Safety check...
                if( oRange.Length < 0 )
                    oRange.Length = 0;
            } else {
                // Anything to right of delete must be shifted.
                if( oRange.Offset >= iDelEnd )
                    oRange.Offset += iDiff;
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
