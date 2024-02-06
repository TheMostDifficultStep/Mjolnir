using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Xml;

using Play.Parse;
using Play.Parse.Impl;
using Play.Interfaces.Embedding;

namespace Play.Edit {
    /// <summary>
    /// This streamer streams the text from a particular column. If any edits
    /// occur, this object becomes invalid. We can handle this by caching 
    /// all of these created and then invalidate them when any edit occurs.
    /// </summary>
    public class RowStream : DataStream<char> {
        IList<Row> _rgRows;
        int        _iPos  = -1;
        int        _iRow  = 0;
        int        _iOffs = 0;
        char       _cChar = '\0';

        readonly int                   _iColumn;
        readonly int                   _iCharCount;
        readonly Action<string,string> _fnLogError;

        public RowStream( 
            IList<Row>            rgRows, 
            int                   iColumn, 
            int                   iCharCount, 
            Action<string,string> fnLogError
        ) {
            _rgRows     = rgRows     ?? throw new ArgumentNullException( "Row array must not be null" );
            _fnLogError = fnLogError ?? throw new ArgumentNullException();

            if( _rgRows.Count < 1 )
                throw new ArgumentException( "Empty document" );
            if( _rgRows[0][iColumn].ElementCount < 1 )
                throw new ArgumentException( "Empty document" );
            if( iColumn < 0 || iColumn >= _rgRows[0].Count )
                throw new ArgumentException( "Column out of bounds" );

            _iColumn    = iColumn;
            _iCharCount = iCharCount;
            _cChar      = _rgRows[0][iColumn][0];
        }

        public override bool InBounds(int p_iPos) {
            if( p_iPos < 0 )
                return false;

            return p_iPos <= _iCharCount;
        }

        public override int Position {
            get {
                return _iPos;
            }
            set {
                Seek( value );
            }
        }

        /// <summary>
        /// Seek our position pointer to be at the given position from the start of the
        /// stream.
        /// </summary>
        /// <remarks>I've noticed we re-seek the same position many many times.
        /// So I've optimized the call to return quickly if the next seek is the
        /// same as the last seek!</remarks>
        /// <param name="p_iPos">Stream position from start.</param>
        protected bool Seek( int p_iPos ) {
            if( p_iPos == _iPos ) // Most seeks are to the same position!! Optimize that!
                return true;

            // If lines get deleted since our last search we might be out of bounds.
            int _iTempRow = _iRow;

            try {
                int l_iOffs = p_iPos - _rgRows[_iTempRow][_iColumn].CumulativeLength;

                while( l_iOffs >= _rgRows[_iTempRow][_iColumn].ElementCount + 1 ) {
                    _iTempRow++;
                    if( _iTempRow >= _rgRows.Count )
                        return false;
                    if( _iTempRow < 0 )
                        return false;
                    l_iOffs = p_iPos - _rgRows[_iTempRow][_iColumn].CumulativeLength;
                }

                while( l_iOffs < 0 ) {
                    _iTempRow--;
                    if( _iTempRow >= _rgRows.Count )
                        return false;
                    if( _iTempRow < 0 )
                        return false;
                    l_iOffs = p_iPos - _rgRows[_iTempRow][_iColumn].CumulativeLength;
                }

                _iRow  = _iTempRow;
                _iOffs = l_iOffs;
                _iPos  = p_iPos;
                _cChar = _rgRows[_iRow][_iColumn][_iOffs];
            } catch( Exception oEx ) {
                Type[] rgErrors = { typeof( ArgumentOutOfRangeException ),
                                    typeof( NullReferenceException ) };
                if( rgErrors.IsUnhandled( oEx ) )
                    throw;
                                    
                _fnLogError( "Multi Column Editor", "Problem seeking within the document." );
                return false;
            }

            return true;
        }

        /// <summary>
        /// This function will work most efficiently if you seek very
        /// near your last position. Random seeking will be more expensive.
        /// </summary>
        /// <param name="iPos">The position to retrieve.</param>
        /// <returns>Character at this position.</returns>
        public override char this[int iPos] {
            get {
                if( Seek( iPos ) )
                    return _cChar;

                return '\0';
            }
        }

        /// <summary>
        /// This is not used by the parser and should be on a different interface.
        /// </summary>
        public override string SubString(int iPos, int iLen) {
            return string.Empty;
        }
        
        /// <summary>
        /// Get closest line at the given position. In the future I want to return an
        /// interface supporting. Formatting, At, ToString
        /// </summary>
        /// <param name="p_iPos"></param>
        /// <param name="p_iOffset"></param>
        public virtual Line SeekLine( int p_iPos, out int p_iOffset )
        {
            return _rgRows[ SeekIndex( p_iPos, out p_iOffset ) ][_iColumn];
        }

        public virtual Line SeekLine( int p_iPos )
        {
            int iOffset;
            return _rgRows[ SeekIndex( p_iPos, out iOffset ) ][_iColumn];
        }

        /// <summary>
        /// Seek closest line at given position. Internal position is modified
        /// </summary>
        /// <param name="iStreamOffset">Stream offset</param>
        /// <param name="p_iOffset">Corresponding Line offset</param>
        /// <returns>Row Index</returns>
        protected virtual int SeekIndex( int iStreamOffset, out int p_iOffset )
        {
            if( Seek( iStreamOffset ) ) {       
                p_iOffset = _iOffs;
                return _iRow;
            }
            p_iOffset = 0;
            return 0;
        }
    }

    /// <summary>
    /// This multi column document does not behave like a normal text stream
    /// document. Let's see how this goes... Also, the expectation is to
    /// have the load/save methods in a subclass. 
    /// </summary>
    /// <remarks>
    /// This is the start of a new document type. I'm no longer trying
    /// to extend my basic editor. The former can allow multiple columns,
    /// only one is the text stream. The rest are line numbers, check boxes
    /// and things that are not selectable.
    /// </remarks>
    public abstract class EditMultiColumn :
        IPgParent,
        IEnumerable<Row>,
        IReadableBag<Row>,
        IPgDocTraits<Row>,
        IPgDocOperations<Row>,
        IDisposable 
    {
        protected readonly IPgBaseSite _oSiteBase;

        protected struct TBucket {
            readonly public object                      _oOwner;
            readonly public IPgCaretInfo<Row> _oTracker;

            public TBucket( object oOwner, IPgCaretInfo<Row> oTracker ) {
                _oOwner   = oOwner   ?? throw new ArgumentNullException( "owner" );
                _oTracker = oTracker ?? throw new ArgumentNullException( "tracker" );
            }
        }

        protected Func<Row>     _fnRowCreator;
        protected List<Row>     _rgRows;
        protected List<bool>    _rgColumnWR; // is the column editable...
        protected Row           _oRowHighlight;
        protected StdUIColors   _ePlayColor;

        public event Action<Row> HighLightChanged;
        public event Action<Row> CheckedEvent;

        public List<IPgEditEvents<Row>> _rgListeners = new ();

        public IPgParent Parentage => _oSiteBase.Host;
        public IPgParent Services  => Parentage;

        public virtual bool IsDirty => false;

        public int ElementCount => _rgRows.Count;

        public Row HighLight { 
            get => _oRowHighlight; 
            set { _oRowHighlight = value; } // Send a window update event;
        }

        public StdUIColors PlayHighlightColor { 
            get => _ePlayColor; 
            set { _ePlayColor = value; }  // Send a window update event;
        }
        public bool ReadOnly { 
            get; 
            set; // Send a window update event;
        }

        /// <exception cref="ArgumentOutOfRangeException" </exception>
        /// <remarks>Should I return a dummy element? Probably not</remarks>
        public Row this[int iIndex] => _rgRows[iIndex];

        public EditMultiColumn( IPgBaseSite oSiteBase ) {
            _oSiteBase = oSiteBase;
            ReadOnly   = false;
        }

        public virtual void Dispose() {
            _rgListeners.Clear();
        }

        public IEnumerator<Row> GetEnumerator() {
            return _rgRows.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() {
            return GetEnumerator();
        }

        public virtual void LogError( string strMessage ) { 
            _oSiteBase.LogError( "Multi Column Editor", strMessage );
        }

        public void ListenerAdd( IPgEditEvents<Row> e ) {
            _rgListeners.Add( e );
        }

        public void ListenerRemove( IPgEditEvents<Row> e ) {
            _rgListeners.Remove( e );
        }

        public void RenumberRows() {
            for( int i=0; i< _rgRows.Count; i++ ) {
                _rgRows[i].At = i;
            }
        }

        /// <summary>
        /// This method is for small edits. Like typing characters.
        /// </summary>
        public bool TryInsertAt( Row oRow, int iColumn, int iOffset, Span<char> spText ) {
            try {
                Line oLine = oRow[iColumn];

                List<IPgDocEvent> rgTrackers = new();
                foreach( IPgEditEvents<Row> e in _rgListeners ) {
                    rgTrackers.Add( e.CreateDocEventObject() );
                }
                // BUG: Change the insert to take a Span! \^_^/
                if( oLine.TryInsert( iOffset, spText.ToString(), 0, spText.Length ) ) {
                    foreach( IPgDocEvent oListen in rgTrackers ) {
                        IPgCaretInfo<Row> oTracker = oListen as IPgCaretInfo<Row>;

                        if( oTracker.Column == iColumn &&
                            oTracker.Row    == oRow ) 
                        {
                            Marker.ShiftInsert( oTracker, iOffset, spText.Length );
                        }
                    }
                }
                foreach( IPgDocEvent oEvent in rgTrackers ) {
                    oEvent.OnUpdated( oRow );
                }
                return true;
            } catch( Exception oEx ) {
                Type[] rgErrors = { typeof( NullReferenceException ),
                                    typeof( IndexOutOfRangeException ),
                                    typeof( ArgumentOutOfRangeException ) };
                if( rgErrors.IsUnhandled( oEx ) )
                    throw;
            }
            return false;
        }
    }

}
