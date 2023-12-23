using System;
using System.Collections.Generic;
using System.Drawing;

using Play.Interfaces.Embedding;

namespace Play.Edit {
    public interface IPgSelectionHelper {
        bool   IsSelectionHit( Point pntTry );
        void   Raise_SelectionChanged();
        void   LogError( string strCatagory, string strError );
        object DocumentText { get; }
        void   SelectionClear();

        ICollection<ILineSelection> Selections { get; }
    }

    /// <summary>
    /// With this idea I can make an enumerator
    /// that returns the lines in the selection.
    /// </summary>
    class SelectMiddle : LineRange {
        ILineSelection _oStartRange;
        ILineSelection _oEndRange;

        public SelectMiddle( ILineSelection oStart, ILineSelection oEnd ) :
            base( null, 0, int.MaxValue, SelectionTypes.Middle ) 
        {
            if( oStart == null || oEnd == null )
                throw new ArgumentNullException( "SelectMiddle needs begin and end." );

            _oStartRange = oStart;
            _oEndRange   = oEnd;

            _fIsEOLSelected = true;
        }

        public override bool IsHit( Line oLine ) {
            if( _oStartRange.Line == null )
                return( false );
            if( _oEndRange.Line == null )
                return( false );

            return( oLine.At > _oStartRange.At &&
                    oLine.At < _oEndRange.At );
        }

        public Line StartLine {
            get { return( _oStartRange.Line ); }
        }

        public Line EndLine {
            get { return( _oEndRange.Line ); }
        }

        public override void Dispose()
        {
            base.Dispose();

            _oStartRange = null;
            _oEndRange   = null;
        }
    }

    class SelectAll : LineRange {
        public SelectAll() :
            base( null, 0, int.MaxValue, SelectionTypes.Middle ) 
        {
            _fIsEOLSelected = true;
        }

        public override bool IsHit( Line oLine ) {
            return( true );
        }
    }

    /// <summary>
    /// This is a class to hold all my routines for dealing with the selection.
    /// For selection we put all three types of selectors in one bag and then the
    /// line using a particular selector uses the color index for the one it needs!
    /// The way we only need one bag to provide a enumerator for the edit line
    /// rendering function instead of 3, one for each type of selection!
    /// </summary>
    public class TextSelector {
        readonly IPgSelectionHelper _oView;

        readonly ICollection<ILineSelection> _rgSelection; // Shortcut to the window's selection collection.

        readonly LineRange _oSelectStart  = new LineRange(null, 0, 0, SelectionTypes.Start );
        readonly LineRange _oSelectEnd    = new LineRange(null, 0, 0, SelectionTypes.End);
        readonly LineRange _oSelectMiddle = null;

        int _iStartOffset;
        Line _oLineOne;  // The first selected edit line might be < or > _iLineTwo.
        Line _oLineTwo;

        /// <summary></summary>
        public TextSelector( IPgSelectionHelper oEditWin ) {
            _oView         = oEditWin ?? throw new ArgumentNullException( "Got to have an Edit Window reference!" );
            _rgSelection   = oEditWin.Selections ?? throw new ArgumentException( "Editwin Selections object is null" );
            _oSelectMiddle = new SelectMiddle( _oSelectStart, _oSelectEnd );
        }

        // BUG: We should only stick our start and end when actually selecting anything.
        public void Reset( CaretPosition oCaret ) {
            Clear();

            if( oCaret == null )
                throw new ArgumentNullException( "Caret must not be null" );

            _iStartOffset = oCaret.Offset;
            _oLineOne     = oCaret.Line;
            _oLineTwo     = oCaret.Line;

			// These will get cleared by the Clear() call above.
			if( _oView.DocumentText is Editor oDocument ) {
				oDocument.CaretAdd(_oSelectStart);
				oDocument.CaretAdd(_oSelectEnd);
				// Middle selection goes for the ride using start/end carets.
			}

			_oSelectStart.Reset( _oLineOne, _iStartOffset, 0 );
            _oSelectEnd  .Reset( _oLineTwo, _iStartOffset, 0 );
        }

        /// <summary>
        /// Right now I just "release" all the selection objects. Tho perhaps
        /// later I should clear the screen selection? Need to look into that.
        /// Also this removes the selections from the Caret collection in the editor!
        /// </summary>
        public void Clear() {
            _oView.SelectionClear();
        }

        /// <summary>
        /// TODO: Now start is always top, end is always bottom no matter now 
        ///       drag the mouse. Need to make sure drag drop, still works.
        /// Call this function as you track the mouse.
        /// </summary>
        /// <remarks>Note only the start and end selections will have the line set.
        /// It doesn't make sense for the middle lines. This isn't a problem for stream
        /// selection but might be for box selection!</remarks>
        public bool NextLocation( ILineRange oCaret ) {
            try {
                if( oCaret.Line == null )
                    return( false );
                if( _oLineOne == null )
                    return( false );

                _oLineTwo = oCaret.Line;

                int iEdgeTwo = oCaret.Offset;

                _rgSelection.Clear();
                _rgSelection.Add( _oSelectStart );

                // Used to compare CumulativeHeight, but since array is sorted, line index will work!
                if( _oLineTwo.At > _oLineOne.At ) { 
                    _oSelectStart.Reset( _oLineOne, _iStartOffset, _oLineOne.ElementCount - _iStartOffset );
                    _oSelectStart.IsEOLSelected = true;

                    // If iEdgeTwo is 0, cursor at start of line, then we have zero selection 
                    _oSelectEnd  .Reset( _oLineTwo, 0, iEdgeTwo );
                    _oSelectEnd  .IsEOLSelected = false; 

                    _rgSelection.Add( _oSelectEnd );
                    if( _oLineTwo.At - _oLineOne.At > 1 )
                        _rgSelection.Add( _oSelectMiddle );
                } else if( _oLineTwo.At  < _oLineOne.At ) {
                    _oSelectStart.Reset( _oLineTwo, iEdgeTwo, _oLineTwo.ElementCount - iEdgeTwo );
                    _oSelectStart.IsEOLSelected = true;

                    _oSelectEnd  .Reset( _oLineOne, 0, _iStartOffset );
                    _oSelectEnd  .IsEOLSelected = false;

                    _rgSelection.Add( _oSelectEnd );
                    if( _oLineOne.At - _oLineTwo.At > 1 )
                        _rgSelection.Add( _oSelectMiddle );
                } else {
                    _oSelectStart.ResetExtent( _oLineOne, _iStartOffset, iEdgeTwo );
                    _oSelectStart.IsEOLSelected = false; // iEdgeTwo > oLine.Length;
                    _oSelectEnd  .Reset();
                }
            } catch( Exception oE ) {
                Type[] rgErrors = { typeof( ArgumentOutOfRangeException ),
                                    typeof( NullReferenceException ) };
                if( rgErrors.IsUnhandled( oE ) )
                    throw;

                _oView.LogError( "internal", "TextSelector::NextLocation() bombed."   );
            } finally {
                _oView.Raise_SelectionChanged();
            }

            return ( true );
        }

        /// <summary>
        /// Check if the given location is within the current selection.
        /// Not finished.
        /// </summary>
        /// <param name="pntLocation">The location to test.</param>
        /// <returns>True if the location is within the selection. Actually
        /// that looses a lot of information. Perhaps I sould return a
        /// caret of some sort?</returns>
        public bool IsHit(PointF pntLocationF ) {
            Point pntLocation = new Point( (int)pntLocationF.X, (int)pntLocationF.Y );

            return( _oView.IsSelectionHit( pntLocation ) );
        }

        /// <summary>
        /// See if nothing is selected in the given selection array.
        /// </summary>
        /// <param name="rgSelection">The three possible sections, start, end, middle.</param>
        /// <returns>true if we've got anything selected.</returns>
        public static bool IsSelected( ICollection<ILineSelection> rgSelection ) {
            int iLength = 0;
            foreach( ILineSelection oRange in rgSelection ) {
                if( oRange.SelectionType != SelectionTypes.Middle ) {
                    iLength += oRange.Length;
                } else {
                    ++iLength;
                }
            }
            return( iLength > 0 );
        }
    } // end class
} // end navigate
