using System;
using System.Windows.Forms;
using System.IO;
using System.Collections.Generic;
using System.Collections;

using Play.Interfaces.Embedding;
using Play.Edit;
using Play.Parse;

// The view selector uses my subclassable type editor, while spiffy, it's
// obsolete and I want to replace with a home view.

namespace Mjolnir {
    public class VLHyperText : IPgWordRange {
        int _iColor = 4;

        public VLHyperText( int iColor ) {
            _iColor = iColor;
        }

        public bool   IsWord     => true;
        public bool   IsTerm     => true;
        public string StateName  => "ViewSwitch";
        public int    ColorIndex => _iColor;

        public int Offset { get => 0; set => throw new NotImplementedException(); }
        public int Length { get => 4; set { } }
    }

   
    public class ViewsLineArray : IArray<Line>, IArray<ViewSlot> {
        List<ViewSlot> _rgLines = new List<ViewSlot>();

        public int  ElementCount { get { return( _rgLines.Count ); } }
        public void RemoveAt( int iIndex ) { _rgLines.RemoveAt( iIndex ); }
        public void Clear() { _rgLines.Clear(); }

        /// <summary>
        /// Keep lizards out of my array! We are a gated communitity.
        /// </summary>
        public bool Insert( int iIndex, Line oValue ) {
			if( oValue is ViewSlot oNewLine ) {
                _rgLines.Insert( iIndex, oNewLine ); 
				return true;
            }
            return false;
        }

        public bool Insert( int iIndex, ViewSlot oValue ) { 
            _rgLines.Insert( iIndex, oValue ); 
            return true;
        }

        Line      IReadableBag<Line     >.this[int iIndex] { get{ return( _rgLines[iIndex] ); } }
        ViewSlot IReadableBag<ViewSlot>.this[int iIndex] { get{ return( _rgLines[iIndex] ); } }

        public ViewSlot this[int iIndex] => _rgLines[iIndex];

        public IEnumerator<ViewSlot> GetEnumerator() {
            return( _rgLines.GetEnumerator() );
        }
    }

	/// <summary>
	/// An editor for view sites. This is a bit of an experiment with my non mutiple buffer
	/// version of the editor.
	/// </summary>
    public class ViewsEditor : BaseEditor {
        public ViewsEditor( IPgBaseSite oSite ) : base( oSite ) {}

        readonly ViewsLineArray _rgContents = new ViewsLineArray();

        protected override IArray<Line> CreateLineArray { 
            get { return( _rgContents as IArray<Line> ); }
        }

        /// <summary>
        /// Make the editor happy. It does not understand the concept of an empty document.
        /// </summary>
        protected override Line CreateLine( int iLine, string strValue ) {
            return new TextLine( iLine, strValue );
        }

        public IEnumerator<ViewSlot> GetEnumerator() {
            return( _rgContents.GetEnumerator() );
        }

        /// <remarks>I could potentially obviate the need for inserted element to be
        /// "Viewsite" class if I take as a "object" then pass it to the CreateLine(), 
        /// inside the method can cast for the type it requires and assign to 
        /// its line type. Some refactoring of the manpulator looks called for.</remarks>
        public bool Add( ViewSlot oViewSite ) {
            int  iLine = this.ElementCount;
            Line oLine = oViewSite;

            // BUG: Technically to set these colors I need to find the language on the editor
            //      and get it from the grammar. But this will do for now.
            oLine.Formatting.Add( new VLHyperText( 1 ) );
            oLine.Formatting.Add( new ColorRange( 0, int.MaxValue, 0 ) );

            _rgContents.Insert( iLine, oLine );

            SetDirty();

            CharacterCount( iLine );
            Raise_AfterInsertLine( oLine );
            Raise_BufferEvent( BUFFEREVENTS.MULTILINE );

            return true;
        }

        /// <remarks>Typically we've got the cursor somewhere where we want to remove a line.
        /// But in the viewsite case we can get a delete view froma variety of sources and the
        /// main thing is to delete a particular viewsite. It's interesting how this just why
        /// you want Remove() on the ICollection</remarks>
        public bool Remove( ViewSlot oViewSite ) {
            for( int i=0; i<_rgContents.ElementCount; ++i ) {
                ViewSlot oLine = _rgContents[i];
                if( oLine == oViewSite ) {
                    Raise_BeforeLineDelete( oLine );
                    _rgContents.RemoveAt( i );
                    oLine.Dispose();

                    foreach( ILineRange oCaret in CaretEnumerable ) {
                        if( oCaret.Line != null && oCaret.Line.At == i ) {
                            // Hmmm... probably should be the NEXT line not the prev...
                            Line oNewLine = null;
                            if( i + 1 < this.ElementCount  )
                                oNewLine = this[i+1];
                            else if( i - 1 > -1 && this.ElementCount > 0 )
                                oNewLine = this[i-1];

                            if( oNewLine != null ) {
                                oCaret.Line   = oNewLine;
                                oCaret.Offset = 0;
                            }
                        }
                    }

                    SetDirty();

                    if( i < this.ElementCount )
                        CharacterCount( i );

                    Raise_BufferEvent( BUFFEREVENTS.MULTILINE );
                    return true;
                }
            }
            return false;
        }

        public bool Contains( ViewSlot oViewSite ) {
            foreach( ViewSlot oLine in this ) {
                if( oLine == oViewSite ) {
                    return true;
                }
            }
            return false;
        }

        public bool Find( ViewSlot oViewSite, out int iLine ) {
            foreach( ViewSlot oLine in this ) {
                if( oLine == oViewSite ) {
                    iLine = oLine.At;
                    return( true );
                }
            }
            iLine = -1;
            return( false );
        }

        public ViewSlot FindFirstView( IDocSlot oDocSite ) {
            foreach( ViewSlot oVSLine in this ) {
                if( oVSLine.DocumentSite == oDocSite ) {
                    return oVSLine;
                }
            }
            return null;
        }

        public new ViewSlot this[int iLine] { 
            get {
                if( !IsHit( iLine ) )
                    throw new IndexOutOfRangeException();

                return _rgContents[iLine]; 
            }
        }

		public override WorkerStatus PlayStatus {
			get { return WorkerStatus.BUSY; }
		}
    }
    
    /// <summary>
    /// This little object gives us a way to match a ToolStripMenuItem back to the
    /// document line it's tracking
    /// </summary>
	public class MenuItemWithLine : 
        ToolStripMenuItem
    {
		Line _oLine;

		public MenuItemWithLine( Line oLine, EventHandler oHandler ) :
			base( oLine.ToString(), null, oHandler ) 
        {
            _oLine = oLine;
		}

		public Line DocumentLine {
			get {
				return( _oLine );
			}
		}
	}
    
    public partial class MainWin {
		public IEnumerable<IPgCommandView> GetViewsOnSelectedDocument() {
			return( new EnumSisterViews( this ) );
		}

		/// <summary>
		/// This class provides an enumerator that will enumerate the open views on the currently
		/// Selected document.
		/// </summary>
		public class EnumSisterViews : IEnumerable<IPgCommandView> {
			readonly MainWin _oMainWin; 

			public EnumSisterViews( MainWin oMainWin ) {
				_oMainWin = oMainWin ?? throw new ArgumentNullException("Need main window pointer.");
			}

			public IEnumerator<IPgCommandView> GetEnumerator() {
				if( _oMainWin._oSelectedDocSite == null )
					yield break;

				foreach( ViewSlot oSite in _oMainWin._oDoc_ViewSelector ) {
					if( oSite.DocumentSite.Document == _oMainWin._oSelectedDocSite.Document &&
						oSite.Guest is IPgCommandView oViewCommand )
						yield return( oViewCommand ); 
				}
			}

			IEnumerator IEnumerable.GetEnumerator() {
				return( GetEnumerator() );
			}
		}

		private class MainWin_Recent : ILineEvents {
            readonly MainWin _oMainWin;

            public MainWin_Recent( MainWin oMainWin ) {
                _oMainWin = oMainWin;
            }

            /// <summary>
            /// Popping the tool menu item on the line.extra won't scale. We can't do that trick for multiple
            /// menus if we wanted. Breaks our MVC style rules. So instead stick a line on each menu item
            /// and then we can track back.
            /// </summary>
            /// <param name="iLine"></param>
            /// <remarks>We insert the line at the top of the menu now!</remarks>
            public void OnLineNew(Line oLine)
            {
                ToolStripMenuItem oMenuItem = new MenuItemWithLine( oLine, new EventHandler(_oMainWin.OnDocFavorites) );

                _oMainWin._miRecentsMenu.DropDownItems.Insert( 0, oMenuItem );
            }

            public void OnLineDelete(Line oLine)
            {
                foreach( MenuItemWithLine oMIDoc in _oMainWin._miRecentsMenu.DropDownItems ) {
                    if( oMIDoc.DocumentLine == oLine ) {
                        _oMainWin._miRecentsMenu.DropDownItems.Remove( oMIDoc );
                        break;
                    }
                }
            }

            public void OnLineUpdated(Line oLine, int iOffset, int iOldLen, int iNewLen)
            {
                foreach( MenuItemWithLine oMIDoc in _oMainWin._miRecentsMenu.DropDownItems ) {
                    if( oMIDoc.DocumentLine == oLine ) {
                        oMIDoc.Text = oLine.ToString();
                        break;
                    }
                }
            }

            public void OnEvent(BUFFEREVENTS eEvent)
            {
            }
        }

    } // End MainWin partial class.
} // End Namespace
