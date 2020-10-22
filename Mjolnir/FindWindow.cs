using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Windows.Forms;
using System.Text.RegularExpressions;

using Play.Interfaces.Embedding;
using Play.Edit;
using Play.Integration;
using Play.Rectangles;

namespace Mjolnir {
    /// <remarks>
    /// TODO: 1/6/2017 : We are an interesting hybrid tool on a document since we require a sister view 
	/// to show our results. I should really create a "find" document & controller to centralize all this.
    /// </remarks>
    public partial class FindWindow : 
		Control,
		IEnumerable<ILineRange>,
		IPgLoad,
		IPgParent
	{
        readonly MainWin          _oWinMain; 
		readonly IPgViewSite      _oSiteView;
        readonly ViewChanged      _oViewChangedHandler;
        readonly ParseHandlerText _oParseEvents       = null;
        readonly Editor           _oDoc_SearchResults = null;
		readonly EditorWithParser _oDoc_SearchKey     = null;
		readonly SmartTable       _oLayout            = new SmartTable(5, LayoutRect.CSS.None );

        IPgTextView             _oView; // This value changes when current view is switched.
        IEnumerator<ILineRange> _oEnumResults;
        TextPosition            _sEnumStart = new TextPosition( 0, 0 );

		/// <remarks>Look into moving more into the initnew() call. Too many event sinks happening in here!!</remarks>
        public FindWindow( IPgViewSite oSiteView, MainWin oShell ) {
			_oWinMain  = oShell    ?? throw new ArgumentNullException( "Shell reference must not be null" );
			_oSiteView = oSiteView ?? throw new ArgumentNullException( "Find window needs a site!!" );

            // Whenever the string changes I need to know about it.
			// BUG: Move to InitNew(). If we fail construction, the searckey doc will still have the callback!!!
			_oDoc_SearchKey = (EditorWithParser)_oWinMain.Document.SearchSlot.Document;
            _oDoc_SearchKey.BufferEvent += new BufferEvent( this.FindStringChanges ); // BUG: no way to remove yet.

			// Exciting new colorized window.
			DocSlot oSearchKeySlot = new DocSlot( this );
			oSearchKeySlot.Guest = new EditWin( oSearchKeySlot, _oDoc_SearchKey, false, true) {
				ScrollBarWidths = 0F
			};

            // TODO: Until I make colors go thru the document we need a site. Need to make a FindSite site.
            oSearchKeySlot.InitNew();

            _oWin_SearchKey        = oSearchKeySlot.Guest;
			_oWin_SearchKey.Parent = this;
            _oDoc_SearchResults    = _oWinMain.Document.ResultsSlot.Document as Editor;

            // Whenever the search results tool window is navigated, I need to know.
            // TODO: We've got the PlayHilights line which would be perfect for this.
			// BUG: Move to InitNew().
			if( oShell.DecorSoloSearch( "matches" ) is EditWin oEditWin ) {
				oEditWin.LineChanged += new Navigation( OnMatchNavigation );
			}

            // BUG: Poster child for event problem. The OnViewChanged event @ startup has already occured.
            //      so we don't know who's focused at the moment. As a matter of fact, we're not even ready
            //      for the event until we exit here!!!
			//      We'll make an InitNew() for this window eventually. Stick all the hook up there!
            _oViewChangedHandler = new ViewChanged(OnViewChanged);

            InitializeComponent();

			_oParseEvents = _oDoc_SearchKey.ParseHandler;
            if( _oParseEvents != null ) {
                _oParseEvents.DisableParsing = oSearchType.SelectedItem.ToString() != "Regex";
            }

			if( _oDoc_SearchKey[0].ElementCount > 0 ) {
				// Don't select all since we don't want to grab the EOL character.
				oSearchKeySlot.Guest.SelectionSet( 0, 0, _oDoc_SearchKey[0].ElementCount );
			}
        }

		/// <summary>
		/// This is where we should be setting up the callbacks and extra.
		/// </summary>
		/// <returns></returns>
		public bool InitNew() {
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

			// Accessing the SearchType dropdown, so it needs to be initialized, see above.
			// TODO: Event calbacks are better set up in the InitNew(), and not in constructor.
            oSearchType.SelectedIndexChanged  += new EventHandler(OnSearchTypesIndexChanged);

            _oWinMain.ViewChanged += _oViewChangedHandler;

			// If we get too narrow, Labels might request double height,
			// Even if they don't use it!
			_oLayout.Add( new LayoutRect( LayoutRect.CSS.Pixels, 43, .25f ) );
			_oLayout.Add( new LayoutRect( LayoutRect.CSS.Pixels, 43, .25f ) );
			_oLayout.Add( new LayoutRect( LayoutRect.CSS.Pixels, 43, .25f ) );
			_oLayout.Add( new LayoutRect( LayoutRect.CSS.Pixels, 43, .25f ) );
			_oLayout.Add( new LayoutRect( LayoutRect.CSS.None ) );

			uint uiHeight = 25;

			List<LayoutRect> _rgRowOne = new List<LayoutRect>();
			_rgRowOne.Add( new LayoutControl( _oWin_SearchKey, LayoutRect.CSS.Pixels, uiHeight ) { Span = 4 } );

			_oLayout.AddRow( _rgRowOne );

			List<LayoutRect> _rgRowTwo = new List<LayoutRect>();
			_rgRowTwo.Add( new LayoutControl( oSearchType, LayoutRect.CSS.Flex ) { Span=1 } );
			_rgRowTwo.Add( new LayoutControl( oMatchCase,  LayoutRect.CSS.Flex ) { Span=1 } );

			_oLayout.AddRow( _rgRowTwo );

			List<LayoutRect> _rgRowThree = new List<LayoutRect>();
			_rgRowThree.Add( new LayoutControl( button1,  LayoutRect.CSS.Flex ) );
			_rgRowThree.Add( new LayoutControl( button2,  LayoutRect.CSS.Flex ) );
			_rgRowThree.Add( new LayoutControl( oGoto,    LayoutRect.CSS.Flex ) );
			_rgRowThree.Add( new LayoutControl( oResults, LayoutRect.CSS.Flex ) );

			_oLayout.AddRow( _rgRowThree );

			_oLayout.SetRect( 0, 0, Width, Height );
			_oLayout.LayoutChildren();

			return true;
		}

        void Reset() {
            _oEnumResults = null;
            this.oResults.Text = ResultsTitle;
        }

		public MainWin Host {
			get { return _oWinMain; }
		}

        public string ResultsTitle { get { return( "Results" ); } }

		public IPgParent Parentage => _oWinMain;
		public IPgParent Services  => Parentage.Services;

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
        // This is the case when we're using and instance of my editor for the find string.
        // If we are in "line" mode, we'll just jump to the line as soon as the user types.
        // BUG: if the string stays the same and the cursor get's moved we don't 
        // update the iterator's start position in the ALL case.
        void FindStringChanges( BUFFEREVENTS eEvent ) {
            Reset();

            if( oSearchType.SelectedItem.ToString() == "Line" ) {
                MoveCaretToLineAndShow();
            }
        }

        void Regex_SelectedIndexChanged(object sender, System.EventArgs e)
        {
            Reset();
        }

        void MoveCaretToLineAndShow()
        {
            try {
                int iRequestedLine = 0;
                if( int.TryParse( _oDoc_SearchKey.GetLine( 0 ).ToString(), out iRequestedLine ) ) {
                    iRequestedLine -= 1;

					_oView.SelectionSet( iRequestedLine, 0, 0 );
					_oView.ScrollToCaret();
                }
            } catch( NullReferenceException ) {
            } catch( IndexOutOfRangeException ) {
            }
        }

        void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                Next_Click(sender, e);
            }
        }

        void OnSearchTypesIndexChanged(object sender, EventArgs e)
        {
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
        /// Listening for the view changed events on the shell.
        /// </summary>
        /// <param name="oView">At present this value can be null!</param>
        private void OnViewChanged(IPgTextView oView) {
            if( oView == null ) {
                this.Enabled = false;
                return;
            }

            Reset();

            _oView = oView;

             this.Enabled = true;
        }

		protected override void OnSizeChanged(EventArgs e) {
			_oLayout.SetRect( 0, 0, Width, Height );
			_oLayout.LayoutChildren();

			//base.OnSizeChanged(e);
		}

        /// <summary>
        /// Look for a match in the section of the line given.
        /// </summary>
        private ILineRange TextMatch( 
            string strFind, 
            Line   oLine, 
            int    iOffsetStart ) 
        {
            try {
			    while( iOffsetStart < oLine.ElementCount ) {
				    int iMatch = 0;
				    for( int j=0; j<strFind.Length; ++j ) {
                        char cChar = oLine[iOffsetStart+j];

                        if( !oMatchCase.Checked )
                            cChar = char.ToLower( cChar );

					    if ( strFind[j] == cChar )
						    iMatch++;
					    else
						    break;
				    }
				    if( iMatch == strFind.Length ) {
                        return( new LineRange( oLine, iOffsetStart, iMatch, SelectionTypes.Empty ) );
                    }
				    iOffsetStart++;
			    }
            } catch( Exception oEx ) {
                Type[] rgErrors = { typeof( ArgumentOutOfRangeException ),
                                    typeof( NullReferenceException ) };
                if( rgErrors.IsUnhandled( oEx ) )
                    throw;

                _oWinMain.LogError( null, "search", "Tried to walk off the end of a buffer in the Find Window." );
            }

            return( null );
        }

        public IEnumerator<ILineRange> CreateTextEnum( IReadableBag<Line> oDoc, string strFind, int iLine ) {
            if( oDoc == null )
                yield break;
			if( string.IsNullOrEmpty( strFind ) )
				yield break;

            int iCount  = 0;
            int iOffset = 0;

            if( !oMatchCase.Checked )
                strFind = strFind.ToLower();

            while( true ) {
                if( iCount++ >= oDoc.ElementCount ) {
                    break;
                }
                do {
                    ILineRange oRange = TextMatch( strFind, oDoc[ iLine ], iOffset );
                    if( oRange == null )
                        break;
                    
                    iOffset = oRange.Offset + oRange.Length; // Do now, in case range is tampered with.
                    yield return( oRange );
                } while( iOffset <  oDoc[ iLine ].ElementCount ); 

                iOffset = 0;
                iLine   = ++iLine % oDoc.ElementCount;
            } // End while
        } // End method

        /// <summary>
        /// Sort of a misnomer. Since we simply return the same line everytime 'next' occures.
        /// But this object lets us use the same code for regex, and text searches. Just remember
        /// to set the caret position in the case of line number in the calling code.
        /// </summary>
        /// <returns></returns>
        public IEnumerator<ILineRange> CreateLineNumberEnum( IReadableBag<Line> oDoc, string strFind )
        {
            if( oDoc == null )
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

            yield return( new LineRange( oDoc[iRequestedLine], 0, 0, SelectionTypes.Empty ) );
        }

        public IEnumerator<ILineRange> CreateRegexEnum( IReadableBag<Line> oDoc, string strFind, int iLine )
        {
            if( oDoc == null )
                yield break;

            RegexOptions eOpts = RegexOptions.None;

            if( !oMatchCase.Checked )
                eOpts |= RegexOptions.IgnoreCase;

            Regex oReg = null;
            try {
                oReg = new Regex( strFind, eOpts );
            } catch( ArgumentException ) {
                _oWinMain.LogError( null, "search", "Problem with regex search string" );
                yield break;
            }

            int  iCount = 0;

            while( true ) {
                if( iCount++ >= oDoc.ElementCount )
                    yield break;

                int    iStart  = 0;
                Line   oLine   = oDoc[ iLine ];
                string strLine = oLine.ToString(); // BUG: Pretty evil actually. Need to go all string based lines. 
                Match  oMatch  = null;

                do {
                    try {
                        oMatch = oReg.Match( strLine, iStart ); // Can throw exceptions.
                    } catch( ArgumentOutOfRangeException ) {
                        _oWinMain.LogError( null, "search", "Could not Regex search current line of text." );
                    }

				    if( oMatch != null && oMatch.Success ) {
                        if( oMatch.Groups.Count > 1 ) {
                            // This is effectively multi-selection on a line
                            for( int iGroup = 1; iGroup < oMatch.Groups.Count; ++iGroup ) {
                                Group oGroup = oMatch.Groups[iGroup];

                                yield return( new LineRange( oLine, oGroup.Index, oGroup.Length, SelectionTypes.Empty ) );
                            }
                        } else {
                            yield return( new LineRange( oLine, oMatch.Index, oMatch.Length, SelectionTypes.Empty ) );
                        }
                        iStart = oMatch.Index + oMatch.Length;
                    } // if match
                } while( oMatch != null && oMatch.Success && iStart < oLine.ElementCount );

                iLine = ++iLine % oDoc.ElementCount;
            } // End while
        }

        private IEnumerator<ILineRange> EnumSearchResults() {
			try {
				switch( oSearchType.SelectedItem.ToString() ) {
					case "Text":
						return( CreateTextEnum( _oView.DocumentText as IReadableBag<Line>, _oWin_SearchKey.Text, _oView.Caret.Line ) );
					case "Regex":
						return( CreateRegexEnum( _oView.DocumentText as IReadableBag<Line>, _oWin_SearchKey.Text, _oView.Caret.Line ) );
					case "Line":
						return( CreateLineNumberEnum( _oView.DocumentText as IReadableBag<Line>, _oWin_SearchKey.Text ) );
				}
			} catch( NullReferenceException ) {
                _oWinMain.LogError( null, "search", "Problem generating Search enumerators." );
			}
            return( null );
        }

        public IEnumerator<ILineRange> GetEnumerator() {
            return( EnumSearchResults() );
        }

        IEnumerator IEnumerable.GetEnumerator() {
            return( EnumSearchResults() );
        }

        /// <summary>
        /// Find the next instance of the search word. This function DOES NOT set the
        /// focus to the searched view. There is a goto hyperlink for that in the dialog.
        /// </summary>
        private void Next_Click( object sender, EventArgs e ) {
            _oDoc_SearchResults.Clear();

            if( _oEnumResults == null ) {
				// Save our starting point.
                _sEnumStart   = _oView.Caret;
                _oEnumResults = EnumSearchResults();
            }
            if( _oEnumResults.MoveNext() ) {
                ILineRange oRange = _oEnumResults.Current;

                _oView.SelectionClear(); 
				_oView.SelectionSet( oRange.Line.At, oRange.Offset, oRange.Length );
				_oView.ScrollToCaret();
            } else {
                _oView.SelectionClear(); 
				_oView.SelectionSet( _sEnumStart.Line, _sEnumStart.Offset, 0 );
				_oView.ScrollToCaret();
                _oEnumResults = null;
            }

            this.oResults.Text = ResultsTitle;
        }

        private void SearchAll_Click(object sender, EventArgs e) {
            _oDoc_SearchResults.Clear();

            using( Editor.Manipulator oSearchManip = _oDoc_SearchResults.CreateManipulator() ) {
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
            try {
                // Have to check since not everyone is an editor in our system. eg ImageViewer!! ^_^;;
                if( _oView.DocumentText is BaseEditor oViewDoc ) {
                    this.oResults.Text = ResultsTitle + " (" + _oDoc_SearchResults.ElementCount.ToString() + 
                                         " in " + oViewDoc.ElementCount.ToString() + " lines)";
                }
            } catch( NullReferenceException ) {
                try {
                    this.oResults.Text = ResultsTitle;
                } catch ( NullReferenceException ) {
                }
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

		protected override void OnGotFocus(EventArgs e) {
			base.OnGotFocus(e);

			_oSiteView.EventChain.NotifyFocused( true );
            _oWin_SearchKey.Select();

			Invalidate();
		}

		protected override void OnLostFocus(EventArgs e) {
			base.OnLostFocus(e);

			_oSiteView.EventChain.NotifyFocused( false );

			Invalidate();
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

        private void Goto_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
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

			public EditWin Guest {
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
                switch( ckCommand ) {
                    case CommandKey.Tab: // BUG
                        //_oFindWindow.SelectNextControl( _oFindWindow.ActiveControl, !fShift, true, false, true );
                        return( true );
                }
                return( false );
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
