using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Windows.Forms;
using System.Text.RegularExpressions;

using Play.Interfaces.Embedding;
using Play.Edit;
using Play.Forms;
using Play.Integration;
using Play.Rectangles;

namespace Mjolnir {
    /// <remarks>
    /// TODO: 1/6/2017 : We are an interesting hybrid tool on a document since we require a sister view 
	/// to show our results. I'm slowly evolving this window to use a controller and forms document.
    /// </remarks>
    public partial class FindWindow : 
		FormsWindow,
		IEnumerable<ILineRange>, // Really belongs on a doc for the "find" window.
		IPgLoad,
		IPgParent
	{
        readonly MainWin _oWinMain; 

                 ViewChanged      _oViewChangedHandler;
        readonly ParseHandlerText _oParseEvents       = null;
        readonly Editor           _oDoc_SearchResults = null;

        IPgTextView             _oView; // This value changes when current view is switched.
        IEnumerator<ILineRange> _oEnumResults;
        TextPosition            _sEnumStart = new TextPosition( 0, 0 );

		public IPgParent Parentage => _oWinMain;
		public IPgParent Services  => Parentage.Services;

        public FindWindow( IPgViewSite oSiteView, MainWin oShell ) : 
            base( oSiteView, (Editor)oShell.Document.SearchSlot.Document )
        {
			_oWinMain = oShell ?? throw new ArgumentNullException( "Shell reference must not be null" );

            _oDoc_SearchResults = _oWinMain.Document.ResultsSlot.Document as Editor;

            InitializeComponent();

			_oParseEvents = null;// _oDoc_SearchKey.ParseHandler;
            if( _oParseEvents != null ) {
                _oParseEvents.DisableParsing = oSearchType.SelectedItem.ToString() != "Regex";
            }
        }

		/// <summary>
		/// This is where we should be setting up the callbacks and extra.
		/// </summary>
		/// <returns></returns>
		public override bool InitNew() {
            if( !base.InitNew() )
                return false;

            EventHandler oGotFocusHandler  = new EventHandler( this.OnChildFocus );
            EventHandler oLostFocusHandler = new EventHandler( this.OnChildBlur  );
            // Bind all the focus events. It would be nice if the children just
            // automatically called some parent event. But I think that's fantasy.
            foreach (Control oChild in Controls) {
                oChild.GotFocus  += oGotFocusHandler;
                oChild.LostFocus += oLostFocusHandler;
            }

            button1.Click += new System.EventHandler(this.Next_Click);
            button2.Click += new System.EventHandler(this.SearchAll_Click);

            // Whenever the search results tool window is navigated, I need to know.
            // TODO: We've got the PlayHilights line which would be perfect for this.
			if( _oWinMain.DecorSoloSearch( "matches" ) is EditWindow2 oEditMatchesWin ) {
				oEditMatchesWin.LineChanged += new Navigation( OnMatchNavigation );
			}

			// Accessing the SearchType dropdown, so it needs to be initialized, see above.
            oSearchType.SelectedIndexChanged  += new EventHandler(OnSearchTypesIndexChanged);

            // BUG: Poster child for event problem. The OnViewChanged event @ startup has already occured.
            //      so we don't know who's focused at the moment.
            _oViewChangedHandler   = new ViewChanged(OnViewChanged);
            _oWinMain.ViewChanged += _oViewChangedHandler;
            if( _oWinMain.ViewSiteSelected != null && 
                _oWinMain.ViewSiteSelected.Guest is IPgTextView oView ) 
            {
                _oView = oView;
            }

            LayoutSingleLine oLayoutSearchKey = new LayoutSingleLine( new FTCacheWrap( DocForms2.GetLine(0) ), LayoutRect.CSS.Flex) { Span = 4, BgColor=SkiaSharp.SKColors.White };
            CacheList.Add( oLayoutSearchKey );
            _iCaretAtLayout = CacheList.Count - 1;

            LayoutTable oTable = new LayoutTable( 5, LayoutRect.CSS.Flex );
            Layout = oTable;

            oTable.AddColumn( LayoutRect.CSS.Pixels, 50 );
            oTable.AddColumn( LayoutRect.CSS.Pixels, 50 );
            oTable.AddColumn( LayoutRect.CSS.Pixels, 50 );
            oTable.AddColumn( LayoutRect.CSS.Pixels, 50 );
            oTable.AddColumn( LayoutRect.CSS.None   , 0 );

            oTable.AddRow( new List<LayoutRect>() { oLayoutSearchKey } ); 

			oTable.AddRow( new List<LayoutRect>() {
			    new LayoutControl( oSearchType, LayoutRect.CSS.Flex ) { Span=1 },
			    new LayoutControl( oMatchCase,  LayoutRect.CSS.Flex ) { Span=1 }
            } );

			oTable.AddRow( new List<LayoutRect>() {
			    new LayoutRect( LayoutRect.CSS.Flex ),
			    new LayoutRect( LayoutRect.CSS.Flex ),
			    new LayoutControl( button1,  LayoutRect.CSS.Flex ),
			    new LayoutControl( button2,  LayoutRect.CSS.Flex ),
            } );

            //DocForms.BufferEvent += FindStringChanges;
            OnDocumentEvent( BUFFEREVENTS.MULTILINE );
            OnSizeChanged( new EventArgs() );

			return true;
		}

        void Reset() {
            _oEnumResults = null;
        }

		public MainWin Host {
			get { return _oWinMain; }
		}

        public string ResultsTitle { get { return( "Results" ); } }

		void OnMatchNavigation( int iLine ) {
            try {
                ILineRange oSearchResult = _oDoc_SearchResults[iLine].Extra as ILineRange;

                if( oSearchResult.Line.At > -1 ) {
				    _oView.SelectionSet( oSearchResult.Line.At, oSearchResult.Offset, oSearchResult.Length );
				    _oView.ScrollToCaret();
                }
            } catch( NullReferenceException ) {
            }
        }
        /// <summary>
        /// SOMETHING on the form changed, but we don't know what. It seems to
        /// be good enough to simply reset the search. We can use more specific
        /// events from the document if we find this isn't good enough.
        /// </summary>
        /// <remarks>
        /// If we are in "line" mode, we'll just jump to the line as soon as the user types.
        /// BUG: If the string stays the same and the cursor get's moved we don't 
        /// update the iterator's start position in the ALL case.
        /// </remarks>
        override protected void OnDocumentEvent( BUFFEREVENTS eEvent ) {
            base.OnDocumentEvent( eEvent );
            Reset();

            if( oSearchType.SelectedItem.ToString() == "Line" ) {
                MoveCaretToLineAndShow();
            }
        }

        void MoveCaretToLineAndShow() {
            try {
                int iRequestedLine = 0;
                if( int.TryParse( DocForms2.GetLine(0).ToString(), out iRequestedLine ) ) {
                    iRequestedLine -= 1;

					_oView.SelectionSet( iRequestedLine, 0, 0 );
					_oView.ScrollToCaret();
                }
            } catch( NullReferenceException ) {
            } catch( IndexOutOfRangeException ) {
            }
        }

        void OnKeyDown(object sender, KeyEventArgs e) {
            if (e.KeyCode == Keys.Enter) {
                Next_Click(sender, e);
            }
        }

        void OnSearchTypesIndexChanged(object sender, EventArgs e) {
            Reset();

            try {
                _oParseEvents.DisableParsing = oSearchType.SelectedItem.ToString() != "Regex";
            } catch( NullReferenceException ) {
            }
        }

		protected override void OnVisibleChanged(EventArgs e) {
			base.OnVisibleChanged(e);
		}

        /// <summary>
        /// Listening for the view changed events on the shell. The FindWindow only 
        /// cares if the incoming view is a IPgTextview.
        /// </summary>
        /// <param name="oView">At present this value can be null!</param>
        private void OnViewChanged( object oView ) {
            if( oView == null || !( oView is IPgTextView oViewText ) ) {
                this.Enabled = false;
                return;
            }

            Reset();

            _oView = oViewText;

             this.Enabled = true;
        }

        /// <summary>
        /// Argghgh, yet another line range. Need this so I can override the At operator
        /// which normally gets it from the line! What an amazing stroke of luck that
        /// I can do this!!
        /// </summary>
        public class LineRangeForMulti :
            ILineRange 
        {
            readonly Line _oLine;
            readonly int  _iOffset;
            readonly int  _iLength;

            public LineRangeForMulti( int iRowIndex, Line oLine, int iStart, int iLength ) {
                At       = iRowIndex;
                _oLine   = oLine ?? throw new ArgumentNullException();
                _iOffset = iStart;
                _iLength = iLength;
            }

            public Line Line { get => _oLine; set => throw new NotImplementedException(); }

            public int At { get; protected set; } // Row index.

            public int ColorIndex => 0;

            public int Offset { get => _iOffset; set => throw new NotImplementedException(); }
            public int Length { get => _iLength; set => throw new NotImplementedException(); }
        }

        /// <summary>
        /// Given a line range, find all the matches within. For example.
        /// "a test of a test here", search "test" will give 2 matches.
        /// </summary>
        /// <returns>A line range representing the match.</returns>
        protected struct TextMatches : IEnumerable<ILineRange> {
            readonly ILineRange _oRange;
            readonly string     _strFind;
            readonly bool       _fMatchCase;
            public TextMatches( ILineRange oRange, string strFind, bool fMatchCase = false ) {
                _oRange     = oRange  ?? throw new ArgumentNullException();
                _strFind    = strFind ?? throw new ArgumentNullException();
                _fMatchCase = fMatchCase;
            }

            public IEnumerator<ILineRange> GetEnumerator() {
                Line oLine  = _oRange.Line;
                int  iStart = _oRange.Offset;
                int  iMax   = _oRange.Offset + _oRange.Length;

			    while( iStart < iMax ) {
				    int iMatch = 0;
				    for( int j=0; j < _strFind.Length && j+iStart < iMax; ++j ) {
                        char cChar = oLine[iStart+j];

                        if( !_fMatchCase )
                            cChar = char.ToLower( cChar );

					    if ( _strFind[j] == cChar )
						    iMatch++;
					    else
						    break;
				    }
				    if( iMatch == _strFind.Length ) {
                        yield return new LineRangeForMulti( _oRange.At, oLine, iStart, iMatch );
                        iStart += iMatch;
                    } else {
                        iStart += 1;
                    }
			    }
            }

            IEnumerator IEnumerable.GetEnumerator() {
                return GetEnumerator();
            }
        }

        /// <summary>
        /// Finds the text based on the new IEnumerable<ILineRange> on any
        /// text capable view!!
        /// </summary>
        public IEnumerator<ILineRange> CreateTextFind( IEnumerable<ILineRange> oView, string strFind ) {
            if( oView == null )
                yield break;
			if( string.IsNullOrEmpty( strFind ) )
				yield break;

            if( !oMatchCase.Checked )
                strFind = strFind.ToLower();

            foreach( ILineRange oRange in oView ) {
                foreach( ILineRange oMatch in new TextMatches( oRange, strFind, oMatchCase.Checked ) ) {
                    yield return oMatch;
                }
            }
        }

        /// <summary>
        /// Sort of a misnomer. Since we simply return the same line everytime 'next' is pressed.
        /// But this object lets us us the same code for regex, and text searches. Just remember
        /// to set the caret position in the case of line number in the calling code.
        /// </summary>
        /// <returns></returns>
        public IEnumerator<ILineRange> CreateLineNumberEnum( object oView, string strFind )
        {
            if( oView == null )
                yield break;

            int  iRequestedLine;

            try {
                iRequestedLine = int.Parse( strFind ) - 1;
            } catch( Exception oEx ) {
                Type[] rgErrors = { typeof( ArgumentNullException ), 
					                typeof( FormatException ), 
									typeof( OverflowException ) };
                if( rgErrors.IsUnhandled( oEx ) )
                    throw;

                throw new InvalidOperationException( "Unexpected return in FindWindow Line Parse" );
            }

            if( oView is IReadableBag<Row> )
                yield return new LineRangeForMulti( iRequestedLine, null, 0, 0 );

            if( oView is IReadableBag<Line> oViewLines ) 
                yield return new LineRange( oViewLines[iRequestedLine], 0, 0, SelectionTypes.Empty );
        }

        public IEnumerator<ILineRange> CreateRegexFind( IEnumerable<ILineRange> oWin, string strFind ) {
            if( oWin == null )
                yield break;

            RegexOptions eOpts = RegexOptions.None;

            if( !oMatchCase.Checked )
                eOpts |= RegexOptions.IgnoreCase;

            Regex oReg = null;
            try {
                oReg = new Regex( strFind, eOpts );
            } catch( Exception oEx ) {
                Type[] rgErrors = { typeof( ArgumentException ),
                                    typeof( ArgumentNullException ),
                                    typeof( ArgumentOutOfRangeException ) };
                if( rgErrors.IsUnhandled( oEx ) )
                    throw;

                _oWinMain.LogError( null, "search", "Problem with regex search string" );
                yield break;
            }

            foreach( ILineRange oRange in oWin ) {
                Match oMatch = null;
                try {
                    // Still an allocation pain. Fix when Regex takes Span<char>
                    oMatch = oReg.Match( oRange.Line.ToString(), oRange.Offset, oRange.Length );
                } catch( Exception oEx ) {
                    Type[] rgErrors = { typeof( ArgumentException ),
                                        typeof( ArgumentNullException ),
                                        typeof( ArgumentOutOfRangeException ) };
                    if( rgErrors.IsUnhandled( oEx ) )
                        throw;

                    _oWinMain.LogError( null, "search", "Could not Regex search current line of text." );
                }

				if( oMatch != null && oMatch.Success ) {
                    if( oMatch.Groups.Count > 1 ) {
                        // This is effectively multi-selection on a line
                        for( int iGroup = 1; iGroup < oMatch.Groups.Count; ++iGroup ) {
                            Group oGroup = oMatch.Groups[iGroup];

                            yield return new LineRangeForMulti( oRange.At, oRange.Line, oGroup.Index, oGroup.Length );
                        }
                    } else {
                        yield return new LineRangeForMulti( oRange.At, oRange.Line, oMatch.Index, oMatch.Length );
                    }
                } 
            }
        }
        private IEnumerator<ILineRange> EnumSearchResults() {
			try {
                string strFind = DocForms2.GetLine(0).ToString();

				switch( oSearchType.SelectedItem.ToString() ) {
					case "Text":
						return CreateTextFind      ( _oView as IEnumerable<ILineRange>, strFind );
					case "Regex":
						return CreateRegexFind     ( _oView as IEnumerable<ILineRange>, strFind );
					case "Line":
						return CreateLineNumberEnum( _oView, strFind );
				}
			} catch( NullReferenceException ) {
                _oWinMain.LogError( null, "search", "Problem generating Search enumerators." );
			}
            return( null );
        }

        public IEnumerator<ILineRange> GetEnumerator() {
            return EnumSearchResults();
        }

        IEnumerator IEnumerable.GetEnumerator() {
            return EnumSearchResults();
        }

        /// <summary>
        /// Find the next instance of the search word. This function DOES NOT set the
        /// focus to the searched view. There is a goto hyperlink for that in the dialog.
        /// </summary>
        private void Next_Click( object sender, EventArgs e ) {
            Submit();
        }

        public override void Submit() {
            base.Submit();

            //_oDoc_SearchResults.Clear();

            try {
                if( _oEnumResults == null ) {
				    // Save our starting point.
                    _sEnumStart   = _oView.Caret;
                    _oEnumResults = EnumSearchResults();
                }
                if( _oEnumResults.MoveNext() ) {
                    ILineRange oRange = _oEnumResults.Current;

                    _oView.SelectionClear(); 
				    _oView.SelectionSet( oRange.At, oRange.Offset, oRange.Length );
				    _oView.ScrollToCaret();
                } else {
                    _oView.SelectionClear(); 
				    _oView.SelectionSet( _sEnumStart.RowIndex, _sEnumStart.Offset, 0 );
				    _oView.ScrollToCaret();
                    _oEnumResults = null;
                }
            } catch( NullReferenceException ) {
                LogError( "Select a document view. Implement IPgTextView..." );
            }
        }

        private void SearchAll_Click(object sender, EventArgs e) {
            try {
                _oDoc_SearchResults.Clear();

                using( BaseEditor.Manipulator oSearchManip = _oDoc_SearchResults.CreateManipulator() ) {
                    StringBuilder oMatchBuilder = new StringBuilder();

                    // oSearchType.SelectedItem.ToString() ;
                    string strFormat = "Location"; // Location or Table

                    foreach( ILineRange oRange in this ) {
                        int    iStart    = oRange.Offset > 10 ? oRange.Offset - 10 : 0;
                        int    iDiff     = oRange.Offset - iStart;
                        int    iPreamble = 0;
                        
                        if( strFormat == "Location" ) { // BUG: should be localized ^_^;
                            oMatchBuilder.Append( "(" );
                            oMatchBuilder.Append( string.Format( "{0,3}", oRange.At + 1 ) );
                            oMatchBuilder.Append( ") " );

                            iPreamble = oMatchBuilder.Length;
                            oMatchBuilder.Append( oRange.Line.SubString( iStart, 50 ) );
                        } else {
                            oMatchBuilder.Append( oRange.Line.SubString( oRange.Offset, oRange.Length ) );
                        }

                        bool fMulti = false;
                        if( fMulti ) {
                            // For regex groups, which we don't support at the moment. 
                            oMatchBuilder.Append( "\t" );
                        } else {
                            Line oNew = oSearchManip.LineAppend( oMatchBuilder.ToString() ); 
                            oMatchBuilder.Length = 0;
                            if( oNew != null ) {
							    //_oDoc_SearchResults.WordBreak( oNew, oNew.Formatting );
                                if( strFormat == "Location" ) {
                                    oNew.Formatting.Add( new ColorRange( iPreamble + iDiff, oRange.Length, _oWinMain.GetColorIndex( "red" ) ) );
                                }
                                oNew.Extra = oRange;
                            }
                        }
                    }; // end foreach
                } // end using

                if( _oDoc_SearchResults.ElementCount > 0 ) {
                    _oWinMain.DecorOpen( "matches", true );
                }
            } catch( Exception oEx ) {
                Type[] rgErrors = { typeof( NullReferenceException ),
                                    typeof( ArgumentOutOfRangeException ) };
                if( rgErrors.IsUnhandled( oEx ) )
                    throw;
            }
        }

        /// <summary>
        /// We are set up so that any child when getting the focus will call this method.
        /// </summary>
        /// <remarks>For some reason on boot the find window has a child
        /// can have the focus. Probably because it was the last object
        /// created. Some sort of side effect of creation.</remarks>
        private void OnChildFocus(object sender, EventArgs e)
        {
            OnGotFocus( e );

			SmartHerderBase oShepard = Host.Shepardfind( "find" );

			if( oShepard != null ) {
				oShepard.OnFocused();
			}

			_oWinMain.Invalidate();
        }

        private void OnChildBlur( object sender, EventArgs e )
        {
            OnLostFocus( e );

			SmartHerderBase oShepard = Host.Shepardfind( "find" );

			if( oShepard != null ) {
				oShepard.OnBlurred();
			}
			_oWinMain.Invalidate();
        }

        private void Goto_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e) {
            if( _oView != null ) {
                _oWinMain.CurrentView = _oView;
                _oView.ScrollToCaret();
            }
        }
        private void Results_LinkClicked( object sender, LinkLabelLinkClickedEventArgs e) {
            _oWinMain.DecorOpen( "matches", true );
        }

        /// <summary>
        /// This is our site for the search string.
        /// </summary>
        public class DocSlot : 
            IPgViewSite,
            IPgViewNotify,
            IEnumerable<ColorMap>
        {
            readonly FindWindow _oFindWindow; // Our owner.

            public DocSlot( FindWindow oFindWin ) {
                _oFindWindow = oFindWin;
            }

			public EditWindow2 Guest {
				set;
				get;
			}

			public bool InitNew() {
				return Guest.InitNew();
			}

            public void Notify( ShellNotify eEvent ) {
            }

            public virtual IPgParent     Host       => _oFindWindow;
            public virtual FILESTATS     FileStatus => FILESTATS.UNKNOWN;
			public         IPgViewNotify EventChain => this; 

			public virtual void LogError(string strMessage, string strDetails, bool fShow=true) {
                _oFindWindow.Host.LogError( this, strMessage, strDetails, fShow );
            }

            public virtual void OnDocDirty() {}

			/// <summary>
			/// Normally we would use a DecorSlot with the FindWindow to implement this
			/// but this window is really old and I haven't plumed that out yet so just
			/// put this little hack for now.
			/// </summary>
			/// <remarks>We don't worry about OnBlur since the center view when it
			/// gets focus it directly blurs all the herders.
			/// BUG: But what about move from decor to decor. Hmmm....</remarks>
            public void NotifyFocused(bool fSelect) { 
				SmartHerderBase oShepard = _oFindWindow.Host.Shepardfind( "find" );

				if( oShepard != null ) {
					if( fSelect )
						oShepard.OnFocused();
				}
			}

            public bool IsCommandKey(CommandKey ckCommand, KeyBoardEnum kbModifier) {
                bool fShift = Convert.ToBoolean(kbModifier & KeyBoardEnum.Shift);
                return ckCommand switch
                {
                    // BUG
                    CommandKey.Tab => (true),//_oFindWindow.SelectNextControl( _oFindWindow.ActiveControl, !fShift, true, false, true );
                    _ => (false),
                };
            }

            public bool IsCommandPress( char cChar ) {
                switch( cChar ) {
                    case '\t':
                        return( true );
                    case '\r':
                        _oFindWindow.Next_Click( null, null );
                        return( true );
                    case '\u001B': // ESC
                        _oFindWindow._oWinMain.SetFocusAtCenter();
                        return( true );
                }
                return( false );
            }

            public IEnumerator<ColorMap> GetEnumerator() {
                return _oFindWindow._oWinMain.SharedGrammarColors;
            }

            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
		}
    }
}
