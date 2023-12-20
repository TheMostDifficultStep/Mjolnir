using System;
using System.Collections.Generic;

using Play.Parse;
using Play.Parse.Impl;
using Play.Interfaces.Embedding;

namespace Play.Edit {
    public interface IPgLineFactory {
        public Line CreateLine( int iLine, string strValue );
    }

    public interface IPgLineHistory {
        void LineNew( Line oLine );
        void LineUpdated( Line oLine );
    }

    /// <summary>
    /// A little experiment for a history listing object.
    /// It just takes values and holds them to view. No
    /// Editing. Just Write and WriteLine.
    /// </summary>
    public class EditorHistory :
		IPgParent,
		IPgLoad,
        IDisposable
    {
        protected readonly IPgBaseSite _oBaseSite;

        protected IPgLineFactory       _oLineFactory;
        protected int                  _iMaxHistory = 30;
        protected LinkedList<Line>     _rgLines = new();
        protected CaretPosition        _oCaret;
        protected List<IPgLineHistory> _rgEvents = new();

        public IReadOnlyList<Line> Lines { get; private set; }

		public IPgParent Parentage => _oBaseSite.Host;
		public IPgParent Services  => Parentage.Services; // Shortcut for Wordbreaker and ParseHandlerText

        public EditorHistory( IPgBaseSite oBaseSite ) {
            _oBaseSite = oBaseSite ?? throw new ArgumentNullException();
        }

        public int MaxHistory { 
            get { return _iMaxHistory; }
            set {
                if( value == 0 )
                    throw new ArgumentOutOfRangeException( "Must be greater than zero" );

                _iMaxHistory = value;
            }
        }

        public int ElementCount => _rgLines.Count;

        public bool InitNew() {
            _rgLines.AddFirst( new TextLine( 0, string.Empty ) );
            return true;
        }

        public void Dispose() {
            _rgEvents.Clear();
        }

        public void EventSetHistory( IPgLineHistory oEvent ) {
            _rgEvents.Add( oEvent );
        }

        public void EventClrHistory( IPgLineHistory oEvent ) {
            _rgEvents.Remove( oEvent );
        }

        public void Write( string strText ) {
            if( _rgLines.First == null ) {
                _rgLines.AddFirst( new TextLine( 0, strText ) );
            } else {
                _rgLines.First.Value.TryAppend( strText );
            }
        }

        public void WriteLine( string strText ) {
            try {
                _rgLines.First.Value.TryAppend( strText );
            
                Line oLine = null;

                // Look for the line about to fall off.
                // Try reuse it's line.
                if( _rgLines.Count == _iMaxHistory ) {
                    oLine = _rgLines.Last.Value;
                    _rgLines.Last.Value = null;
                }
                if( oLine == null ) {
                    oLine = new TextLine( 0, string.Empty );
                } else {
                    oLine.Empty();
                }

                // Old line is recycled here.
                LinkedListNode<Line> oNode = _rgLines.AddFirst( oLine );
                oNode.Value = oLine;

                while( _rgLines.Count > _iMaxHistory ) {
                    _rgLines.RemoveLast();
                }

                Raise_NewLineEvent( _rgLines.First.Value );
            } catch( NullReferenceException ) {
                _oBaseSite.LogError( "Error", "History insert error." );
            }
        }

        /// <summary>
        /// There is a new line at the bottom of the screen.
        /// </summary>
        public void Raise_NewLineEvent( Line oLine ) {
            foreach( IPgLineHistory oEvent in _rgEvents ) {
                oEvent.LineNew( oLine );
            }
        }

        /// <summary>
        /// The bottom screen line has been appended to.
        /// </summary>
        public void Raise_LineUpdate( Line oLine ) {
            foreach( IPgLineHistory oEvent in _rgEvents ) {
                oEvent.LineUpdated( oLine );
            }
        }
    }
}
