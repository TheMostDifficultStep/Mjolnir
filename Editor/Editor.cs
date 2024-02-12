using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;

using Play.Parse;
using Play.Parse.Impl;
using Play.Interfaces.Embedding;

// These classes could be part of an editor sub namespace perhaps. Then the 
// Line definitions could be part of a lighter module. Just trying to sort out
// how the Find Window get's at text views.
namespace Play.Edit {
	public enum UndoType {
        Insert,
        Delete,
        Split,
        Merge,
        LineInsert,
        LineRemove,
        Master
    }

    public interface IUndoUnit : IColorRange {
        UndoType Type { get; }
        int      Line{ get; } 
        void     Do( Editor.Manipulator oBulk );
    }

    public enum SelectionTypes : int {
        Empty  = 0,
        Start  = -1,
        End    = -2,
        Middle = -3,
    }

    public interface ILineRange : IColorRange {
        Line Line { get; set; }
        int  At   { get; }
    }

    public interface ILineSelection : ILineRange {
        bool           IsEOLSelected { get; set; }
        SelectionTypes SelectionType { get; }
        bool           IsHit( Line oLine ); // TODO: Might want to re-think this with dummy lines floating about.
    }

    public interface IRefCount {
        void AddRef ( string strID );
        int  Release( string strID );
    }

    public interface IBufferEvents {
        void OnEvent( BUFFEREVENTS eEvent );
    }
    
    // I used to pass an integer for the line number. But there would be a lot of side effects
    // regarding the line number (At) in the document and that of the screen line cache where in
    // a multi edit case the screen would lose track of lines. Passing the line object is more fool proof.
    public interface ILineEvents : IBufferEvents {
        void OnLineNew    ( Line oLine );
        void OnLineDelete ( Line oLine );
        void OnLineUpdated( Line oLine, int iOffset, int iOldLen, int iNewLen );
    }

    public enum NavigationSource {
        API,
        UI
    }

    public enum Axis {
        Horizontal,
        Vertical
    }

	public delegate void HilightEvent();
    public delegate void CheckedEvent( Line oLineChecked );

	/// <summary>
	/// Tiny special purpose interface for music players that are going to message what current line
	/// they are playing. Implement it on your document site.
	/// </summary>
	public interface IPgSiteHilight {
		void OnLineCurrent( Line oLine, IMemoryRange oElem );
		void OnLineClear  ();
	}

    public interface IPgFormInterface {
        Line GetLine( int iIndex ); // Replaces this[iIndex] { get; }
        Line LineAppend( string strValue, bool fUndoable = true );

        /// <summary>
        /// Should be able to Delete, Insert (char/str), replace all in one!!
        /// </summary>
        bool LineTextReplace( int iLine, IMemoryRange mrDelete, ReadOnlySpan<char> spInsert );
        void Undo();
        void Raise_Submit();
        void CaretAdd   ( ILineRange oRange );
        void CaretRemove( ILineRange oRange );

        event BufferEvent BufferEvent;
    }

    /// <summary>
    /// This is the Document for the editing control. It is basically line based,
    /// but it supports stream reading objects (for the parser). I should build
    /// in a line length limit but haven't worked on those details yet.
    /// </summary>
    public abstract partial class BaseEditor :
		IPgParent,
        IPgSave<TextWriter>,
        IPgLoad<TextReader>,
		IPgLoad<string>,
        IPgLoad<IEnumerable<string>>,
        IEnumerable<Line>,
        IReadableBag<Line>,
        IPgFormInterface,
        IDisposable
     {
		public class DocSlot:
			IPgBaseSite,
			IPgSiteHilight,
            IPgFileSite
		{
			protected BaseEditor _oHost;

			public DocSlot( BaseEditor oHost ) {
				_oHost = oHost ?? throw new ArgumentNullException();
			}

			public IPgParent Host => _oHost;

            FILESTATS IPgFileSite.FileStatus => FILESTATS.UNKNOWN;

            Encoding IPgFileSite.FileEncoding => Encoding.UTF8;

            string IPgFileSite.FilePath => "Foo";
            string IPgFileSite.FileName => "Bar";

            public void LogError(string strMessage, string strDetails, bool fShow=true) {
				_oHost._oSiteBase.LogError( strMessage, strDetails, fShow );
			}

			public void Notify( ShellNotify eEvent ) {
			}

			public void OnLineCurrent(Line oLine, IMemoryRange oElem) {
				_oHost.HighLight = oLine;
			}

			public void OnLineClear() {
				_oHost.HighLight = null;
			}
		} // End class

        protected readonly IPgBaseSite _oSiteBase;
        protected readonly IPgFileSite _oSiteFile;

        protected bool _fIsDirty         = false;
        protected int  _iCumulativeCount = 0;

        IRefCount _oUndoMaster      = null;
        string    _strEOL           = "\r\n";
        int       _iUndoMode        = 0;
        long      _lWordCount       = 0;
		Line      _oLineHighLight   = null;
        Line      _oCheckedLine     = null;

        /// <summary>
        /// Setting this value sends a CheckedEvent
        /// </summary>
        /// <seealso cref="CheckedReset"/>
        public Line CheckedLine { 
            get{ return _oCheckedLine; }
            set{ _oCheckedLine = value; CheckedEvent?.Invoke( value ); }
        }

        /// <summary>
        /// Use this property to Reset the UI without sending a CheckedEvent.
        /// </summary>
        /// <seealso cref="CheckedEvent"/>
        public Line CheckedReset {
            set { _oCheckedLine = value; }
        }

        protected readonly List<Line>     _rgLines           = new List<Line>();
        readonly ICollection<ILineEvents> _rgBufferCallbacks = new List<ILineEvents>();
        readonly ICollection<ILineRange>  _rgCursors         = new List<ILineRange>();
        readonly Stack<IUndoUnit>[]       _rgUndoStacks      = new Stack<IUndoUnit>[2];

		readonly WordBreakerHandler _oParseWords; // A basic word breaker/counter for wrapped views.
        
        public event BufferEvent   BufferEvent;
		public event HilightEvent  HilightEvent;
        public event CheckedEvent  CheckedEvent;
        public event Action<int[]> SubmitEvent;

        public BaseEditor( IPgBaseSite oSite ) {
            _oSiteBase = oSite;                // Ok if this is null.
            _oSiteFile = oSite as IPgFileSite; // Ok if this is null also, informative more than imperative.

            // If ignore setup, we can init before we've read the grammars and use this for errors reporting.
			//Grammer<char> oLineBreakerGrammar = LineBreakerGrammar;
			//if( oLineBreakerGrammar != null ) {
			//	_oParseWords = new WordBreakerHandler( LineBreakerGrammar );
			//}
			
			for( int i=0; i<_rgUndoStacks.Length; ++i ) {
                _rgUndoStacks[i] = new Stack<IUndoUnit>();
            }
        }

        protected abstract Line CreateLine( int iLine, string strValue );

        /// <summary>
        /// This is returning the title. It's tempting to return a string of the entire 
		/// contents but impractical in general.
        /// </summary>
        /// <returns></returns>
        public override string ToString() {
            return _oSiteBase.ToString();
        }

		public IPgParent Parentage => _oSiteBase.Host;
		public IPgParent Services  => Parentage.Services; // Shortcut for Wordbreaker and ParseHandlerText

		/// <summary>
		/// Set a single line to be hilighted. But you know I think this is something
		/// I need to potentially expand to every line.
		/// </summary>
		public Line HighLight {
			get { return _oLineHighLight; }
			set {
				_oLineHighLight = value;
				HilightEvent?.Invoke();
			}
		}

		/// <summary>
		/// Raise the High Light event. For external users.
		/// </summary>
		public void HighLight_Raise() {
			HilightEvent?.Invoke();
		}

        /// <summary>
        /// This is a little different than the IsDirty on the main editor object.
        /// IsDirty can happen when lines are moved and deleted as well as lines
        /// typed into.
        /// Individual lines marked dirty do not contribute to the IsDirty state
        /// of the document. Line.IsDirty is used to identified changed lines when
        /// the submit on a form is pressed (Enter Key).
        /// The two concepts are similiar but I don't want to attempt to combine them yet.
        /// </summary>
        public void Raise_Submit() {
            List<int> rgDirty = new List<int>();

            foreach( Line oLine in this ) {
                if( oLine.IsDirty ) {
                    rgDirty.Add( oLine.At );
                }
                oLine.ClearDirty();
            }
            SubmitEvent?.Invoke( rgDirty.ToArray() );
        }


        /// <summary>
        /// 7/29/09 : Right now the shell loads all the grammers because for the most part they are
        /// bound to the editors by the shell. The line breaker is the only one that doesn't
        /// really have to live there. It's nice to have the word breaker there since I can load 
        /// up the grammars all in one place via the "config/languages/grammars" but it makes 
        /// the editor completely dependent on it's shell to run with word wrapping. Makes sense
        /// in the multi-editor case, but a bit of a drag if just creating one, since I could load
        /// the grammar in the editor.
		/// So I'm making this virtual so you can easily override where the grammar comes from.
        /// </summary>
        public virtual Grammer<char> LineBreakerGrammar {
            get {
				try {
					IPgGrammers oGrammars = Services as IPgGrammers;
					return( oGrammars.GetGrammer("line_breaker") as Grammer<char> ); 
				} catch( Exception oEx ) {
					Type[] rgErrors = { typeof( NullReferenceException ),
										typeof( InvalidCastException ),
										typeof( FileNotFoundException ) };
					if( rgErrors.IsUnhandled( oEx ))
						throw;

					return( null );
				}
            }
        }

		public void WordBreak( Line oLine, ICollection<IPgWordRange> rgWords ) {
            if( _oParseWords != null )
			    _oParseWords.Parse( oLine.GetStream(), rgWords );
			oLine.WordCount = rgWords.Count;
		}

        public string Banner {
            get { return( ToString() ); }
        }

		/// <summary>
		/// Get the productions list of available.
		/// </summary>
		/// <exception cref="NotImplementedException" />
		public virtual BaseEditor Productions {
			get { throw new NotImplementedException( "Productions are implemented in subclasses of this class." ); }
		}

		public virtual bool ProductionsTrace {
			get { return( false ); }
			set { throw new NotImplementedException(); }
		}

        public void ClearFormatting() {
            for( int i=0; i<_rgLines.Count; ++i ) {
                _rgLines[i].Formatting.Clear();
            }
            Raise_BufferEvent( BUFFEREVENTS.CLEARFORMATTING );  
        }

        public string FileEncoding {
            get { 
				string strDefault = Encoding.Default.ToString();
                try {
					if( _oSiteFile == null )
						return strDefault;

                    return _oSiteFile.FileEncoding.WebName;
                } catch( NullReferenceException ) {
                    return strDefault;
                }
            }
        }

        /// <summary>
        /// BUG: This needs to be localized.
        /// </summary>
        public string FileStats {
            get { 
                string strStat = "?";

                if( _oSiteFile != null ) {
					switch( _oSiteFile.FileStatus ) {
						case FILESTATS.READONLY:
							strStat = "r/o";
							break;
						case FILESTATS.WRITEONLY:
							strStat = "w/o";
							break;
						case FILESTATS.READWRITE:
							strStat = "r/w";
							break;
					}
                }

                return( strStat ); 
            }
        }

		/// <summary>
		/// Not sure what I'll return in the embedded object case. ^_^;;
		/// </summary>
		public string FilePath {
			get { 
                if( _oSiteFile != null )
					return _oSiteFile.FilePath; 

                return string.Empty;
			}
		}

		public string FileBase {
			get { 
                if( _oSiteFile != null )
					return _oSiteFile.FileName; 

                return string.Empty;
			}
		}

        public void LogError( string strMessage ) {
            if( _oSiteBase != null ) {
                _oSiteBase.LogError( "editor", strMessage );
            }
        }

        public long WordCountUpdate() {
            _lWordCount = 0;
            for( int i=0; i<_rgLines.Count; ++i ) {
                _lWordCount += _rgLines[i].WordCount;
            }

            Raise_BufferEvent( BUFFEREVENTS.WORDSUPDATED );

            return( _lWordCount );
        }

        public long WordCount {
            get { return( _lWordCount ); }
        }

        /// <summary>
        /// Recount the total number of characters in the buffer.
        /// </summary>
        /// <param name="iStartLine">A line to start counting from.
        /// We only need to sumate lines beyond the modified line.</param>
        /// <returns>Returns the count of characters in the buffer. 0 on error.</returns>
        /// <seealso cref="Refresh()"/>
        public long CharacterCount( int iStartLine )
        {
            _iCumulativeCount = 0;

            if( _rgLines.Count == 0 ) {
                return( 0 );
            }
            if( !IsHit( iStartLine ) ) {
                LogError( "Counting problem, Line: " + iStartLine.ToString() + "." );
                return( 0 );
            }

			if( iStartLine <= _rgLines.Count ) {
				// Try to backup one from given line so can catch cumulative count.
				// else make sure we clear cumulative count of the new first line
				if( iStartLine > 0 )
					iStartLine--;
				else
					_rgLines[0].Summate( 0, 0 );
			}

            _iCumulativeCount = _rgLines[iStartLine].CumulativeLength;

            //int j = 0;

            for( int i = iStartLine; i < _rgLines.Count; ++i ) {
                _iCumulativeCount = _rgLines[i].Summate( i, _iCumulativeCount );

                // Redo the stream position values in the local area, in case
                // a reparse occurs. That gives us a starting point for our stream reader.
                // This only fixes 2 lines after at the iStartLine line.
				// TODO: I'll probably chuck this. I don't see anyone using "Start" after parse.
      //          if( j < 2 ) {
      //              foreach( object oRangeObj in _rgLines[i].Formatting ) {
						//if( oRangeObj is IColorRange2 oRange ) {
						//	oRange.Summate( _rgLines[i].CumulativeLength );
						//}
      //              }
      //              ++j;
      //          }
            }

            // We recount whenever there's a change so, this here is probably fine.
            if( _oSiteBase != null )
                _oSiteBase.Notify( ShellNotify.DocumentDirty );
            
            return( _iCumulativeCount );
        }

        public int Size {
            get { 
                if( _iCumulativeCount <= 0 ) {
                    // hacky. I need to re investigate
                    CharacterCount( 0 );
                }

                return( _iCumulativeCount );
            }
        }

        public int ElementCount {
            get { return( _rgLines.Count ); }
        }

        /// <summary>
        /// Return the line object at the position requested.
        /// </summary>
        /// <param name="iLineAt">Index of the desired line.</param>
        /// <returns>The line object at the index given, or an empty line if out of bounds.</returns>
        /// <remarks>TODO: I could return the same dummy line. But I'd want a line that doesn't allow anyone
        /// to set it's buffer. Wait until I abstract the notion of a line a bit more.</remarks>
        public virtual Line GetLine( int iIndex ) {
            Line oTry;

            if( !IsHit( iIndex ) ) {
                oTry = CreateLine( 0, string.Empty );
            } else {
                oTry = _rgLines[iIndex];
            }

            return oTry;
        }

        public Line this[int iIndex] { 
            get { 
                return GetLine( iIndex );
            }
        }            

        public bool IsHit( int iLine ) {
            return( IsWhere( DocumentPosition.INSIDE, iLine ) == 0 );
        }

        /// <summary>
        /// Locate the line in relation to the given coordinate. I've made this private since I would like
        /// to move to a linked list implementation of the document. The editwin no longer needs this.
        /// The last array dependency I can think of is the do/undo feature.
        /// </summary>
        /// <param name="eEdge">Top of the document or bottom.</param>
        /// <param name="iLine">Location you are interested in.</param>
        /// <returns>-1 below the given location,
        ///          +1 above the given location,
        ///           0 at    the given location.</returns>
        private int IsWhere( DocumentPosition eEdge, int iLine ) {
            switch( eEdge ) {
                case DocumentPosition.TOP:
                    return( iLine );
                case DocumentPosition.BOTTOM:
                    return( _rgLines.Count - iLine - 1 );
                case DocumentPosition.INSIDE:
                    if( iLine < 0 )
                        return( -1 );
                    if( iLine > _rgLines.Count - 1 )
                        return( 1 );

                    return( 0 );
            }
            return( -1 );
        }

        public void Clear( bool fSendEvents = true ) {
            using( Editor.Manipulator oManip = new Editor.Manipulator( this, fSendEvents ) ) {
                oManip.DeleteAll();
            }
        }
        
        /// <summary>
        /// Creates a read only stream object to traverse the buffer.
        /// It is most efficient if you seek incrementally from your last
        /// position. I'll make this return an interface sometime.
        /// </summary>
        /// <returns>A LineStream object.</returns>
        public BaseEditor.LineStream CreateStream()
        {
            return( new LineStream( this ) );
        }

        /// <summary>
        /// Bulk edits on the editor!
        /// </summary>
        /// <returns>A manipulator object. Call Dispose() when finished!</returns>
        public Editor.Manipulator CreateManipulator() {
            return ( new Editor.Manipulator( this ) );
        }

        /// <summary>
        /// The value we use for the End of Line.
        /// </summary>
        public string EOL {
            get{ return( _strEOL ); }
        }
        
        /// <summary>
        /// Find the line containing stream offset we are looking for.
        /// </summary>
        /// <param name="rgLines"></param>
        /// <param name="iStreamOffset"></param>
        /// <returns>Line index closest to the given stream offset.</returns>
        /// <remarks>I probably could just use generics at this point.
        /// I lifted this implementation directly from 'binary_search' in wikipedia.</remarks>
        static int BinarySearch( IList<Line> rgLines, int iStreamOffset ) 
        {
            if( rgLines.Count == 0 )
                return( 0 );

            int iLow  = 0;
            int iHigh = rgLines.Count - 1;
            int iMid  = 0;

            while( iHigh >= iLow ) {
                iMid = iLow + (( iHigh - iLow ) / 2 );
                if( rgLines[iMid].CumulativeLength < iStreamOffset )
                    iLow  = iMid + 1;
                else if( rgLines[iMid].CumulativeLength > iStreamOffset )
                    iHigh = iMid - 1;
                else
                    return( iMid );
            }

            // We don't want the next line up but the line including the stream pos.
            if( rgLines[iMid].CumulativeLength > iStreamOffset && iMid > 0 )
                --iMid;

            return( iMid );
        }

        public int BinarySearchForLine( int iStreamOffset )
        {
            return( BinarySearch( _rgLines, iStreamOffset ) );
        }

        /// <summary>
        /// We like to append to the end of the editor all the time. Let's make that case easy.
        /// </summary>
        /// <param name="strText">Insert this text as a new line.</param>
		/// <remarks>BUG: I should parse the text looking for control characters. See the
		/// clipboard copy version.</remarks>
        public Line LineAppend( string strText, bool fUndoable = true ) {
            using( Manipulator oBulk = this.CreateManipulator() ) {
                return oBulk.LineInsert( this.ElementCount, strText, fUndoable );
            }
        }

        /// <summary>
        /// Insert a line at the top of the file.
        /// </summary>
        /// <param name="strText"></param>
        public void LineInsert( string strText ) {
            using( Manipulator oBulk = this.CreateManipulator() ) {
                oBulk.LineInsert( 0, strText );
            }
        }

        /// <summary>
        /// Move the line. This is not an "undoable" action.
        /// </summary>
        /// <param name="iIndex">New location of the line. (After the element is removed)</param>
        /// <param name="oLine"></param>
        /// <remarks>I could be a bit more efficient by shifting the elements myself.
        /// But it's good enough for now.</remarks>
        public void LineMove( int iIndex, Line oLine ) { 
            int iStartLine = iIndex < oLine.At ? iIndex : oLine.At;

            _rgLines.RemoveAt( oLine.At );
            _rgLines.Insert( iIndex, oLine );

            CharacterCount( iStartLine);

            SetDirty();
        }

        /// <summary>
        /// Accept a single character at the given index.
        /// </summary>
        /// <param name="iIndex">Where to insert the character.</param>
        /// <param name="cChar">The character to insert.</param>
        /// <remarks> This is the only line operation you won't find on 
        /// the bulk editor since it's never used as part of the undo 
        /// operations.</remarks>
        /// <seealso cref="FormsWindow.Line"/>
        public void LineCharInsert( int iLine, int iIndex, char cChar) {
            Line oLine;

            if( !IsHit( iLine ) ) {
                if( iLine <= 0 ) 
                    iLine = 0;

                if( iLine >= _rgLines.Count )
                    iLine = _rgLines.Count;

                _rgLines.Insert( iLine, CreateLine( iLine, string.Empty ) );
            }

            oLine = _rgLines[iLine];

            if( oLine.TryInsert( iIndex, cChar ) ) {
                foreach( ILineRange oCaret in CaretEnumerable ) {
                    Line oRangeLine = oCaret.Line;

                    if( oRangeLine != null ) {
                        // Reset any dummy lines. Brilliant, but sleasy. ^_^;;
                        // NOTE: Our forms labels & values MUST NOT be from 
                        // different documents!! Argghghg!
                        if( oRangeLine.At == oLine.At && oRangeLine != oLine ) {
                            oCaret.Line = oLine;
                        }
                        // Make sure the line is assigned on the line range, second 
                        // Check the two lines are EQUAL (but not necessarily the same reference)
                        if( oCaret.At == oLine.At ) {
                            Marker.ShiftInsert( oCaret, iIndex, 1 );
                        }
                    }
                }

                // We special case insert for a single character
                // next to our last edit so we don't have piles of
                // undo units on the undo stack.
                IUndoUnit oUnit = UndoPeek() as UndoInsert;
                if( oUnit != null &&
                    oUnit.Type == UndoType.Insert &&
                    oUnit.Line == iLine &&
                    oUnit.Offset <= iIndex &&
                    oUnit.Offset + oUnit.Length >= iIndex ) 
                {
                    oUnit.Length += 1;
                } else {
                    UndoPush( new UndoInsert( iLine, iIndex, 1 ) );
                }
                SetDirty();
                CharacterCount( iLine );

                Raise_AfterLineUpdate( oLine, iIndex, 0, 1 );
            }
            //Raise_ChangesFinished();
            Raise_BufferEvent( BUFFEREVENTS.SINGLELINE ); // TODO: Need to assert there is no bulk upload going on.
        }

        /// <summary>
        /// At present this is only used for my form documents, (search & property pages)
        /// it does not support UNDO. That will be added by the time I attempt to
        /// remove the deprecated line operations which are in use with the standard
        /// text editor.
        /// </summary>
        /// <remarks>It's ok to send a del range that is out of bounds of the line,
        /// this function will return false, but throw no exceptions or error messages
        /// </remarks>
        /// <param name="iLine">The line to operate on.</param>
        /// <param name="rgDel">The range to delete.</param>
        /// <param name="spIns">The span to insert in place of the delete.</param>
        /// <returns>true if successful, false if not.</returns>
        public bool LineTextReplace( int iLine, IMemoryRange rgDel, ReadOnlySpan<char> spIns ) {
            if( !IsHit( iLine ) )
                return false;

            if( rgDel.Offset < 0 )
                return false;

            if( spIns == null ) {
                spIns = string.Empty;
            }

            Line oLine = _rgLines[iLine];

            if( oLine.TryReplace( rgDel.Offset, rgDel.Length, spIns ) ) {
                foreach( ILineRange oCaret in CaretEnumerable ) {
                    Line oRangeLine = oCaret.Line;

                    if( oRangeLine != null ) {
                        // Reset any dummy lines. Brilliant, but sleasy. ^_^;;
                        // NOTE: Our forms labels & values MUST NOT be from 
                        // different documents!! Argghghg!
                        if( oRangeLine.At == oLine.At && oRangeLine != oLine ) {
                            oCaret.Line = oLine;
                        }
                        // Make sure the line is assigned on the line range, second 
                        // Check the two lines are EQUAL (but not necessarily the same reference)
                        if( oCaret.At == oLine.At ) {
                            Marker.ShiftReplace( oCaret, rgDel.Offset, rgDel.Length, spIns.Length - rgDel.Length );
                        }
                    }
                }

                SetDirty();
                CharacterCount( iLine );

                Raise_AfterLineUpdate( oLine, rgDel.Offset, 0, 1 );
            }
            Raise_BufferEvent( BUFFEREVENTS.SINGLELINE ); // TODO: Need to assert there is no bulk upload going on.
            return true;
        }

        public void CaretAdd( ILineRange oCaret )
        {
            if( !_rgCursors.Contains( oCaret ) )
                _rgCursors.Add( oCaret );
        }
        
        public void CaretRemove( ILineRange oCaret )
        {
            _rgCursors.Remove( oCaret );
        }

        public IEnumerable<ILineRange> CaretEnumerable
        {
            get {
                return( _rgCursors );
            }
        }

        public void ListenerAdd( ILineEvents oListener ) {
            if( _rgBufferCallbacks.Contains( oListener ) )
                return;

            _rgBufferCallbacks.Add( oListener );
        }
        
        public void ListenerRemove( ILineEvents oListener ) {
            _rgBufferCallbacks.Remove( oListener );
        }
        
        public void Raise_AfterLineUpdate( Line oLine, int iIndex, int iOldLen, int iNewLen ) {
            foreach( ILineEvents oEvents in _rgBufferCallbacks ) {
                oEvents.OnLineUpdated( oLine, iIndex, iOldLen, iNewLen );
            }
        }

        public void Raise_AfterInsertLine( Line oLine )
        {
            foreach( ILineEvents oEvents in _rgBufferCallbacks ) {
                oEvents.OnLineNew( oLine );
            }
        }
        
        public void Raise_BeforeLineDelete( Line oLine )
        {
            foreach( ILineEvents oEvents in _rgBufferCallbacks ) {
                oEvents.OnLineDelete( oLine );
            }
       }
        
        // NOTE: This is a buffer event and NOT a caret moving event. You should not trigger caret movement
        //       events based on this event. Causes problems in the EditorViewSite implementation.
        public void Raise_MultiFinished()
        {
            // this is mainly for the views.
            foreach( IBufferEvents oEvent in _rgBufferCallbacks ) {
                oEvent.OnEvent( BUFFEREVENTS.MULTILINE );
            }

			// this is typically for the controllers.
			BufferEvent?.Invoke(BUFFEREVENTS.MULTILINE);
		}
        
        public void Raise_BufferEvent( BUFFEREVENTS eEvent )
        {
            foreach( IBufferEvents oEvent in _rgBufferCallbacks ) {
                oEvent.OnEvent( eEvent );
            }

			BufferEvent?.Invoke(eEvent);
		}

#region UndoStuff
        public int UndoMode
        {
            get {
                return( _iUndoMode );
            }
        }

        /// <summary>
        /// Undo the last action on the editor.
        /// </summary>
        /// <remarks>If I fail the undo I should probably empty the undo stack. It's not
        /// valid from the point of failure, if any of the actions fail.</remarks>
        public void Undo() {
            if( _rgUndoStacks[0].Count > 0 ) {
                IUndoUnit oUnit = _rgUndoStacks[0].Pop();
                _iUndoMode = 1;
                
                using( Editor.Manipulator oBulk = new Editor.Manipulator( this ) ) {
                    try {
                        oUnit.Do( oBulk );
                    } catch( Exception oE ) {
                        Type[] rgErrors = { typeof( ArgumentOutOfRangeException ),
                                            typeof( IndexOutOfRangeException ) };
                        if( rgErrors.IsUnhandled( oE ) )
                            throw;

                        _rgUndoStacks[0].Clear();
                        _rgUndoStacks[1].Clear();

                        if( _oSiteBase != null ) {
                            _oSiteBase.LogError( oE.Message, oE.StackTrace );
                        }
                    } finally {
                        _iUndoMode = 0;
                    }
                }
            }
        }

        public Stack<IUndoUnit>[] UndoStacks {
            get {
                return( _rgUndoStacks );
            }
        }

        // This is a set of methods that must be available to the collection of objects that represent
        // the editor. However, they must most not be exposed to outsiders using the object. Alas, .net
        // doesn't do a good job here so they're public. But I will not expose these via any interface.
        public IDisposable UndoMasterBegin() {
            if( _oUndoMaster == null )
                _oUndoMaster = new UndoMaster( this );
            else
                _oUndoMaster.AddRef( "UndoMasterBegin()" );

            return( _oUndoMaster as IDisposable );
        }

        public void UndoMasterEnd() {
            if( _oUndoMaster != null ) {
                if( 0 == _oUndoMaster.Release( "UndoMasterBegin()" ) ) {
                    _oUndoMaster = null;
                  //Raise_MultiFinished();
                }
            }
        }

        public void UndoPush( IUndoUnit oUnit ) {
            _rgUndoStacks[_iUndoMode].Push( oUnit );
        }

        public IUndoUnit UndoPeek() {
            if( _rgUndoStacks[_iUndoMode].Count == 0 )
                return( null );
            return( _rgUndoStacks[_iUndoMode].Peek() );
        }

        public IUndoUnit UndoPop() {
            if( _rgUndoStacks[_iUndoMode].Count == 0 )
                return ( null );
            return ( _rgUndoStacks[_iUndoMode].Pop() );
        }
#endregion // UndoStuff

		public virtual WorkerStatus PlayStatus {
			get { return( WorkerStatus.NOTIMPLEMENTED ); }
		}

		public StdUIColors PlayHighlightColor {
			get {
				switch( PlayStatus ) {
					case WorkerStatus.BUSY:
						return( StdUIColors.MusicLine );
					case WorkerStatus.PAUSED:
						return( StdUIColors.MusicLinePaused );

					default:
						return( StdUIColors.BGReadOnly );
				}
			}
		}
  
		/// <summary>
        /// We are being closed. 
        /// BUGBUG: I notice I'm not checking if my views have been shut down.
        /// </summary>
        public virtual void Dispose() {
            _rgCursors.Clear();
            _rgBufferCallbacks.Clear();
            _rgLines.Clear();
        }

#region IPgPersistStream 

        public virtual bool InitNew() {
            if (_rgLines.Count != 0) {
                _oSiteBase.LogError( "editor", "Already Loaded" );
                return (false);
            }

            // NOTE: EOF is ^Z or 0x1A, \u001A (26)

            Raise_BufferEvent( BUFFEREVENTS.LOADED );  

            return (true);
        }

		public bool Load( string strText ) {
			LineAppend( strText );
			return true;
		}

        protected void Reset() {
			// This makes us reloadable.
            if( _rgLines.Count > 0 ) {
				using( Manipulator oManip = CreateManipulator() ) {
					oManip.DeleteAll( false );
				}
            }

            _iCumulativeCount = 0;
			_oLineHighLight   = null;
        }
	
        /// <summary>
        /// Read from a file, whatever encoding. Not good at reporting errors yet. It is
        /// OK to re-load the editor.
        /// </summary>
        public virtual bool Load( TextReader oReader ) {
			Reset();
            
            string strLine;
            try {
                //UndoMasterBegin(); // BUG: Load undoable? That's weird. 3/16/2019 commented out.
                do {
                    // I can't get a version that returns a character array! Only string, Kind
                    // wierd when you think about it. Now I could go thru all the
                    // work to try to read the buffer into a char array. But I'll end up blitting
                    // stuff all over anyway. So just use the string. TextLine will copy it to an array.
                    strLine = oReader.ReadLine();
                    if( strLine != null ) {
                        int  iLine = _rgLines.Count;
                        Line oLine = CreateLine( iLine, strLine );
                        
                        // BUG: if string length > 10K we should use a StringBuilderLine style line...
                        // and modify the edit win to show a generic message on that line.
                        if( strLine.Length > 64000 ) {
                            _oSiteBase.LogError( "editor", "Warning! There is a long line which won't be viewable in this editor. Line: " + ( iLine + 1 ).ToString() );
                            return false;
                        }

                        _iCumulativeCount = oLine.Summate( _rgLines.Count, _iCumulativeCount );
                        
                        _rgLines.Insert( _rgLines.Count, oLine );
                        Raise_AfterInsertLine( oLine );
                    }
                } while( strLine != null );
            } catch( Exception oE ) {
                Type[] rgErrors = { typeof( IOException ),
                                    typeof( NullReferenceException ),
                                    typeof( ArgumentNullException ),
                                    typeof( ArgumentException ) };
                if( rgErrors.IsUnhandled( oE ) )
                    throw;

                _oSiteBase.LogError( "editor", "Unable to read stream (file) contents." );

                return( false );
            } finally {
                // 7/10/2023, why do both???
                Raise_MultiFinished();
                Raise_BufferEvent( BUFFEREVENTS.LOADED );  

                //UndoMasterEnd();
            }

            return (true);
        }


        public bool Load(IEnumerable<string> oClxn ) {
			Reset();
            
            try {
                foreach( string strLine in oClxn ) {
                    int  iLine = _rgLines.Count;
                    Line oLine = CreateLine( iLine, strLine );
                        
                    if( strLine.Length > 64000 ) {
                        _oSiteBase.LogError( "editor", "Warning! There is a long line which won't be viewable in this editor. Line: " + ( iLine + 1 ).ToString() );
                    }

                    _iCumulativeCount = oLine.Summate( _rgLines.Count, _iCumulativeCount );
                        
                    _rgLines.Insert( _rgLines.Count, oLine );
                    Raise_AfterInsertLine( oLine );
                }
            } catch( Exception oE ) {
                Type[] rgErrors = { typeof( IOException ),
                                    typeof( NullReferenceException ),
                                    typeof( ArgumentNullException ),
                                    typeof( ArgumentException ) };
                if( rgErrors.IsUnhandled( oE ) )
                    throw;

                _oSiteBase.LogError( "editor", "Unable to read line iterator contents." );

                return false;
            } finally {
                Raise_BufferEvent( BUFFEREVENTS.LOADED );  
            }

            return true;
        }

        /// <summary>
        /// Save to the given stream. This can be a random stream. If it is a stream
        /// opened on our file things might break.
        /// </summary>
        /// <param name="oStream"></param>
        /// <returns>true if successful.</returns>
        public virtual bool Save( TextWriter oStream )
        {
            if( _oSiteBase == null )
                return( false );

            bool fReturn = false;

            try {
                int i=0;
                while( i<_rgLines.Count-1 ) {
                    _rgLines[i].Save( oStream );
                    oStream.WriteLine();
                    ++i;
                }
                if( i<_rgLines.Count ) {
                    _rgLines[i].Save( oStream );
                }
                fReturn   = true;
                _fIsDirty = false;
            } catch( Exception oE ) {
                Type[] rgErrors = {
                    typeof( FormatException ),
                    typeof( IOException ),
                    typeof( ObjectDisposedException ),
                    typeof( ArgumentNullException )
                };

                if( rgErrors.IsUnhandled( oE ) )
                    throw;

                _oSiteBase.LogError( "editor", oE.Message );
            } finally {
                oStream.Flush();
            }

            return( fReturn );
        }

        /// <summary>
        /// Has the file changed since last saved? 
        /// </summary>
        public bool IsDirty {
            get { return _fIsDirty; }
        }

        protected void SetDirty() {
            _fIsDirty = true;
        }

        public void ClearDirty() {
            _fIsDirty = false;
        }

        public IPgBaseSite Site {
            get {
                return( _oSiteBase );
            }
        }


#endregion // IPersistStream

        // This probably belongs on the subclassed editor so I can return the
        // proper type. EditorEnumLines
        IEnumerator<Line> IEnumerable<Line>.GetEnumerator() {
            return( CreateLineEnum() );
        }

        IEnumerator IEnumerable.GetEnumerator() {
            return( CreateLineEnum() );
        }

        public IEnumerator<Line> CreateLineEnum() {
            int iIndex=0;

            if( _rgLines.Count > 0 ) {
                yield return _rgLines[iIndex];
            }

            while( ++iIndex < _rgLines.Count ) {
                yield return _rgLines[iIndex];
            }
        }

        /// <summary>
        /// This will create line search ranges starting with everything to
        /// the right of the cursor. Through all lines, and then lastly 
        /// anything that was on the left of the cursor!! O.o
        /// </summary>
        /// <param name="iIndex"></param>
        /// <param name="iOffset"></param>
        public IEnumerator<ILineRange> CreateLineSearch( int iIndex, int iOffset ) {
            if( _rgLines.Count <=0 )
                yield break;

            Line      oStart   = _rgLines[iIndex];
            int       iLength  = oStart.ElementCount;

            LineRange oRange   = new LineRange( null, 0, 0, SelectionTypes.Middle );
            LineRange oFirst   = new LineRange( oStart, iOffset, iLength - iOffset, SelectionTypes.Start );
            LineRange oLast    = new LineRange( oStart, 0, iOffset, SelectionTypes.End );

            yield return oFirst;

            for( int i=iIndex+1, j = 0; 
                 j < _rgLines.Count - 1; 
                 ++j, i = ++i % _rgLines.Count ) 
            {
                oRange.Line   = _rgLines[i];
                oRange.Offset = 0;
                oRange.Length = _rgLines[i].ElementCount;

                yield return oRange;
            }

            yield return oLast;
        }

        public void LineDirtyClear() {
            foreach( Line oLine in this ) {
                oLine.ClearDirty();
            }
        }
    }

    /// <summary>
    /// You know, I think this would be cooler if these functions were an interface that
    /// you plug into the side of the BaseEditor! Need to think about that...
    /// </summary>
    public class Editor : BaseEditor
    {
        public Editor( IPgBaseSite oSite ) : base( oSite ) {
        }

        protected override Line CreateLine( int iLine, string strValue )
        {
            return( new TextLine( iLine, strValue ) );
        }
    }

}
