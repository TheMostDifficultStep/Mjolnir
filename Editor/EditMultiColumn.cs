using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

using Play.Parse;
using Play.Interfaces.Embedding;
using Play.Parse.Impl;

namespace Play.Edit {
    public static class BagOperations {
        public static int Final( this IReadableBag<Row> rgList ) {
            if( rgList.ElementCount <= 0 )
                throw new ArgumentOutOfRangeException();

            return rgList.ElementCount - 1;
        }
        public static int First( this IReadableBag<Row> rgList ) {
            if( rgList.ElementCount <= 0 )
                throw new ArgumentOutOfRangeException();

            return 0;
        }
    }

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

        readonly int _iColumn;
        readonly int _iCharCount;
        Action<string> LogError { get; } // Report our errors here.

        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="ArgumentException"></exception>
        public RowStream( 
            IList<Row>     rgRows, 
            int            iColumn, 
            int            iCharCount, 
            Action<string> fnLogError
        ) {
            _rgRows  = rgRows     ?? throw new ArgumentNullException( "Row array must not be null" );
            LogError = fnLogError ?? throw new ArgumentNullException();

            if( _rgRows.Count < 1 )
                throw new ArgumentException( "Empty document" );
            if( iColumn < 0 || iColumn >= _rgRows[0].Count )
                throw new ArgumentOutOfRangeException( "Column out of bounds" );

            _iColumn    = iColumn;
            _iCharCount = iCharCount;
            _cChar      = _rgRows[0][iColumn][0]; // Get \r for the character!
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
                                    
                LogError( "Problem stream seeking within the multi column doc." );
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

	public class ParseColumnText : IParseEvents<char> {
		protected readonly RowStream   _oStream;
		protected readonly State<char> _oStart;
        protected Action<string> LogError {get; }

		public ParseColumnText( RowStream      oStream, 
                                Grammer<char>  oLanguage, 
                                Action<string> oLogError ) 
        {
            _oStream = oStream   ?? throw new ArgumentNullException();
            LogError = oLogError ?? throw new ArgumentNullException();

			if( oLanguage == null )
				throw new ArgumentNullException( "Language must not be null" );

			_oStart  = oLanguage.FindState("start") ?? throw new InvalidOperationException( "Couldn't find start state" );
		}

		public void OnMatch( ProdBase<char> oElem, int iStream, int iLength ) {
            MemoryElem<char> oMemElem  = oElem as MemoryElem<char>;
            if( oMemElem != null ) { // Capture's both terms and states.
                Line oLine = _oStream.SeekLine( oMemElem.Start, out int iLineOffset);

                // Parser is totally stream based, talking to the base class. So have to do this here.
                oMemElem.Offset = iLineOffset;
                oLine.Formatting.Add(oMemElem);
            }
		}

		public void OnParserError(ProdBase<char> p_oMemory, int p_iStart) {
            StringBuilder sbMessage = new StringBuilder( 100 );

            sbMessage.Append( "Markup Error! " ); 

			if( p_iStart < 0 ) {
				sbMessage.Append( "Parse underflow." );
			}
			if( _oStream.InBounds( p_iStart ) ) {
				Line oLine  = _oStream.SeekLine(p_iStart, out int iOffset);

                sbMessage.Append( "Line : " );
				sbMessage.Append( oLine.At.ToString() ); 
				sbMessage.Append( ", Offset: " );
				sbMessage.Append( iOffset.ToString() );
			} else {
				sbMessage.Append( "Parse overflow." );
			}

            // The terminal passed us might be a memory term or a prod term.
            // If it's a memory term, go to it's host to find the production it came from.
            MemoryElem<char> l_oMemory = p_oMemory as MemoryElem<char>;
            ProdElem<char>   oProdElem = p_oMemory as ProdElem<char>;

            if( l_oMemory != null ) {
                oProdElem = l_oMemory.ProdElem;
            }

            int iElem = 0;

            if( oProdElem != null ) {
                Production<char> oProd = oProdElem.Host;

                if( oProd != null ) {
                    // We have the production in the state and now find the particular
                    // element that failed so we can report it relative to it's state.
                    for( iElem =0; iElem < oProd.Count; ++iElem ) {
                        if( oProd[iElem] == oProdElem ) {
                            iElem++;
                            break;
                        }
                    }
                    sbMessage.Append( ". State: \"" );
                    sbMessage.Append( oProd.StateName );
                    sbMessage.Append( "\". Production: " ); 
                    sbMessage.Append( oProd.Index.ToString() );
                    sbMessage.Append( ". Element: " );
                    sbMessage.Append( iElem.ToString() );
                }
            }

			LogError( sbMessage.ToString() );
		}

		public virtual bool Parse() {
			try {
				MemoryState<char>   oMStart = new MemoryState<char>( new ProdState<char>( _oStart ), null );
				ParseIterator<char> oParser = new ParseIterator<char>( _oStream, this, oMStart );

				while( oParser.MoveNext() );
			    return true;
			} catch( NullReferenceException ) {
                LogError( "Couldn't parse text column." );
			}
			return false;
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

        protected readonly List<Row>   _rgRows = new();
        protected List<bool>           _rgColumnWR; // is the column editable...
        protected Row                  _oRowHighlight;
        protected StdUIColors          _ePlayColor;
        protected List<IPgEditHandler> _rgTemp = new();
        protected List<IPgEditEvents>  _rgListeners = new ();

        public event Action<Row> HighLightChanged;
        public event Action<Row> CheckedEvent;

        public IPgParent Parentage => _oSiteBase.Host;
        public IPgParent Services  => Parentage.Services;

        public virtual bool IsDirty { get; set; }

        public int ElementCount => _rgRows.Count;
        /// <exception cref="ArgumentOutOfRangeException" </exception>
        /// <remarks>Don't return dummy element for out of bounds
        /// else we don't know when a caret has reached the top or 
        /// bottom of the file.</remarks>
        public Row this[int iIndex] => _rgRows[iIndex];

        public Row HighLight { 
            get => _oRowHighlight; 
            set { 
                _oRowHighlight = value; 
                Raise_DocFormatted();
            }
        }

		public virtual WorkerStatus PlayStatus {
			get { return( WorkerStatus.NOTIMPLEMENTED ); }
		}

		public StdUIColors PlayHighlightColor {
			get {
				switch( PlayStatus ) {
					case WorkerStatus.BUSY:
						return StdUIColors.MusicLine;
					case WorkerStatus.PAUSED:
						return StdUIColors.MusicLinePaused ;

					default:
						return StdUIColors.BGReadOnly;
				}
			}
            set { _ePlayColor = value; }  // Wouldn't expect this...
		}
  
        public bool ReadOnly { 
            get; 
            set; // Send a window update event;
        }

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

        public void ListenerAdd( IPgEditEvents e ) {
            _rgListeners.Add( e );
        }

        public void ListenerRemove( IPgEditEvents e ) {
            _rgListeners.Remove( e );
        }

        /// <summary>
        /// This is turning into our OnDocUpdated style call but it's
        /// document local versus the per view updates that come from
        /// IPgEditEvents. You can update the text immediately and then
        /// schedule a parse.
        /// </summary>
        public virtual void DoParse() {
        }

        /// <summary>
        /// Call this AFTER a parse has occured.
        /// </summary>
        protected void Raise_DocFormatted() {
            foreach( IPgEditEvents oListener in _rgListeners ) {
                oListener.OnDocFormatted();
            }
        }

        /// <summary>
        /// Renumber the rows and columns on each row. Also
        /// sums up the cumulative count on a column basis for parsing.
        /// </summary>
        /// <exception cref="InvalidProgramException"></exception>
        public void RenumberAndSumate() {
            if( _rgRows.Count <= 0 )
                return;
            if( _rgRows[0].Count > 100 )
                throw new InvalidProgramException( "Rows column count seems too large" );

            try {
                Span<int> rgTotals = stackalloc int[_rgRows[0].Count];

                for( int iRow=0; iRow< _rgRows.Count; iRow++ ) {
                    Row oRow = _rgRows[iRow];

                    oRow.At = iRow;

                    for( int iCol=0; iCol<oRow.Count; ++iCol ) {
                        rgTotals[iCol] = oRow[iCol].Summate( iCol, rgTotals[iCol] );
                    }
                }
            } catch ( Exception oEx ) {
                Type[] rgErrors = { typeof( ArgumentOutOfRangeException ),
                                    typeof( IndexOutOfRangeException ),
                                    typeof( NullReferenceException ) };
                if( rgErrors.IsUnhandled( oEx ) )
                    throw;

                LogError( "Problem in renumber rows." );
            }
        }

        /// <summary>
        /// Make sure the cumulative counts are upto date before making this call...
        /// </summary>
        public RowStream CreateColumnStream( int iColumn ) {
            try {
                if( _rgRows.Count <= 0 )
                    return null;

                Line oLastLine = _rgRows[_rgRows.Count-1][iColumn];
                int iMaxStream = oLastLine.CumulativeLength + oLastLine.ElementCount;

                return new RowStream( _rgRows, iColumn, iMaxStream, LogError );
            } catch( Exception oEx ) {
                Type[] rgErrors = { typeof( ArgumentOutOfRangeException ),
                                    typeof( ArgumentException ), // empty doc can send this.
                                    typeof( ArgumentNullException ),
                                    typeof( NullReferenceException ),
                                    typeof( IndexOutOfRangeException ) };
                if( rgErrors.IsUnhandled( oEx ) )
                    throw;

                LogError( "Unable to creat column stream." );
            }

            return null;
        }

        public void ParseColumn( int iColumn, string strGrammar = "text" ) {
            try {
                IPgGrammers oGServ = (IPgGrammers)Services;
                if( oGServ.GetGrammer( strGrammar ) is Grammer<char> oGrammar ) {
                    RowStream        oStream       = CreateColumnStream( iColumn );
                    ParseColumnText  oParseHandler = new ParseColumnText( oStream, oGrammar, LogError );

                    foreach( Row oRow in _rgRows ) {
                        oRow[iColumn].Formatting.Clear();
                    }

                    oParseHandler.Parse();
                }
            } catch( Exception oEx ) {
                Type[] rgErrors = { typeof( InvalidCastException ),
                                    typeof( ArgumentOutOfRangeException ),
                                    typeof( ArgumentNullException ),
                                    typeof( NullReferenceException ) };
                if( rgErrors.IsUnhandled( oEx ) )
                    throw;

                LogError( "Trouble setting up column parse." );
            }
        }

        /// <summary>
        /// The cache manager might have multiple objects that might be
        /// affected by an edit. In order for us to better handle before/after
        /// changes, we start the edit by getting a handler from it.
        /// The handler enumerates all the elements that need to track
        /// the edits on one window.
        /// </summary>
        protected struct TrackerEnumerable :         
            IEnumerable<IPgCaretInfo<Row>>
        {
            readonly List<IPgEditHandler> _rgHandlers;
            readonly EditMultiColumn      _oDoc;

            public TrackerEnumerable(EditMultiColumn oHost ) {
                _oDoc = oHost ?? throw new ArgumentNullException();

                // Save us from creating this everytime...
                _rgHandlers = oHost._rgTemp;
                _rgHandlers.Clear();

                foreach( IPgEditEvents oListener in oHost._rgListeners ) {
                    _rgHandlers.Add( oListener.CreateEditHandler() );
                }
            }

            public IEnumerator<IPgCaretInfo<Row>> GetEnumerator() {
                foreach( IPgEditHandler oHandler in _rgHandlers ) {
                    foreach( IPgCaretInfo<Row> oTracker in oHandler ) {
                        yield return oTracker;
                    }
                }
            }

            IEnumerator IEnumerable.GetEnumerator() {
                throw new NotImplementedException();
            }

            public void FinishUp( EditType eEdit, Row oRow = null ) {
                foreach( IPgEditHandler oHandler in _rgHandlers ) {
                    oHandler.OnUpdated( eEdit, oRow );
                }
                _rgHandlers.Clear();
                _oDoc      .DoParse(); // At the very least invalidate. Maybe this should be OnUpdated... hmmm...
                _oDoc      .IsDirty = true;
            }
        }

        /// <summary>
        /// This method is for small edits. Like typing characters. Simply an insert
        /// if srclen is 0.
        /// </summary>
        public bool TryReplaceAt( Row oRow, int iColumn, int iSrcOff, int iSrcLen, ReadOnlySpan<char> spText ) {
            try {
                Line oLine = oRow[iColumn];

                TrackerEnumerable oTE = new TrackerEnumerable( this );

                if( oLine.TryReplace( iSrcOff, iSrcLen, spText ) ) {
                    foreach( IPgCaretInfo<Row> oTracker in oTE ) {
                        if( oTracker.Column == iColumn &&
                            oTracker.Row    == oRow ) 
                        {
                            Marker.ShiftInsert( oTracker, iSrcOff, spText.Length - iSrcLen );
                        }
                    }
                }

                oTE.FinishUp( EditType.ModifyElem, oRow );

                return true;
            } catch( Exception oEx ) {
                Type[] rgErrors = { typeof( NullReferenceException ),
                                    typeof( IndexOutOfRangeException ),
                                    typeof( ArgumentOutOfRangeException ) };
                if( rgErrors.IsUnhandled( oEx ) )
                    throw;
                _oSiteBase.LogError( "Multi Column Edit", "Error in TryReplaceAt" );
            }
            return false;
        }

        public bool TryDeleteAt( Row oRow, int iColumn, int iSrcOff, int iSrcLen ) {
            try {
                string strRemoved = string.Empty;
                Line   oLine      = oRow[iColumn];

                TrackerEnumerable oTE = new TrackerEnumerable( this );

                if( oLine.TryDelete( iSrcOff, iSrcLen, out strRemoved ) ) {
                    IMemoryRange oDeleted = new ColorRange( iSrcOff, iSrcLen ); // BUG use struct...
                    foreach( IColorRange oFormat in oLine.Formatting ) {
                        Marker.ShiftDelete( oFormat, oDeleted );
                    }
                    foreach( IPgCaretInfo<Row> oTracker in oTE ) {
                        if( oTracker.Row    == oRow &&
                            oTracker.Column == iColumn )
                        {
                            if( oTracker.Offset >= iSrcOff ) {
                                oTracker.Offset = iSrcOff;
                            }
                        }
                    }
                }

                oTE.FinishUp( EditType.ModifyElem, oRow );

                return true;
            } catch( Exception oEx ) {
                Type[] rgErrors = { typeof( NullReferenceException ),
                                    typeof( IndexOutOfRangeException ),
                                    typeof( ArgumentOutOfRangeException ) };
                if( rgErrors.IsUnhandled( oEx ) )
                    throw;

                _oSiteBase.LogError( "Multi Column Edit", "Error in TryDeleteAt" );
            }

            return false;
        }

        public bool RowDeleteAt( Row oRow ) {
            try {
                TrackerEnumerable oTE = new TrackerEnumerable( this );

                _rgRows.Remove( oRow ); // Faster if use index... probably...

                RenumberAndSumate(); // Huh... This fixes all my bugs. :-/

                oTE.FinishUp( EditType.DeleteRow, oRow );

                return true;
            } catch( Exception oEx ) {
                Type[] rgErrors = { typeof( NullReferenceException ),
                                    typeof( IndexOutOfRangeException ),
                                    typeof( ArgumentOutOfRangeException ) };
                if( rgErrors.IsUnhandled( oEx ) )
                    throw;

                _oSiteBase.LogError( "Multi Column Edit", "Error in RowDeleteAt" );
            }
            return false;
        }

        /// <summary>
        /// This bulk loader might overlap with DocProperties loaders. Might
        /// look at that in the future.
        /// </summary>
        /// <remarks>
        /// It is slightly confusing that there is not an OnUpdateRow on the
        /// IPgEditEvents interface. Instead we just tell ourselves to reparse,
        /// which is basically all a "document" really needs to do. The window's
        /// cache manager gets OnUpdate event by the CreateHandler()
        /// You could keep a spare handlers list, but since this isn't some
        /// mega traffic object why bother...
        /// </remarks>
        public class BulkLoader :
            IDisposable 
        {
            readonly EditMultiColumn      _oHost;
                     bool                 _fDisposed  = false;
            readonly List<IPgEditHandler> _rgHandlers = new List<IPgEditHandler>();
            public BulkLoader( EditMultiColumn oHost ) {
                _oHost = oHost ?? throw new ArgumentNullException();
                foreach( IPgEditEvents oCall in _oHost._rgListeners ) {
                    _rgHandlers.Add( oCall.CreateEditHandler() );
                }
            }

            public void Dispose() {
                if( !_fDisposed ) {
                    _oHost.RenumberAndSumate();
                    foreach( IPgEditHandler oCall in _rgHandlers ) {
                        oCall.OnUpdated( EditType.InsertRow, null );
                    }
                    _oHost.DoParse();
                    _fDisposed = true;
                }
            }

            public void InsertAt( int iRow, Row oNew ) {
                _oHost._rgRows.Insert( iRow, oNew );
            }

            public void Append( Row oNew ) {
                _oHost._rgRows.Insert( _oHost._rgRows.Count, oNew );
            }
        }

        public void Clear() {
            _rgRows.Clear();

            foreach( IPgEditEvents oCall in _rgListeners ) {
                IPgEditHandler oHandle = oCall.CreateEditHandler();

                oHandle.OnUpdated( EditType.DeleteRow, null );
            }
        }
    }
}
