using System;
using System.Collections.Generic;

using Play.Parse;
using Play.Parse.Impl;
using Play.Interfaces.Embedding;

namespace Play.Edit {
    public interface IPgLineFactory {
        public Line CreateLine( int iLine, string strValue );
    }

    /// <summary>
    /// This will be the backing history object. It will be
    /// a circular buffer. 
    /// </summary>
    public class EditorHistory :
		IPgParent,
		IPgLoad,
        IDisposable
    {
        protected readonly IPgBaseSite _oBaseSite;

        protected IPgLineFactory   _oLineFactory;
        protected int              _iCurrentLine;
        protected int              _iMaxHistory = 30;
        protected LinkedList<Line> _rgLines = new();

        public IReadOnlyList<Line> Lines { get; private set; }

		public IPgParent Parentage => _oBaseSite.Host;
		public IPgParent Services  => Parentage.Services; // Shortcut for Wordbreaker and ParseHandlerText

        public EditorHistory( IPgBaseSite oBaseSite ) {
            _oBaseSite = oBaseSite ?? throw new ArgumentNullException();
        }

        public IPgLineFactory LineFactory { 
            set {
                // Erase current contents.
                _oLineFactory = value;
            }
            get {
                return _oLineFactory;
            }
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
            // right now nothing special.
        }

        public LinkedListNode<Line> AddLine() {
            LinkedListNode<Line> oNode = _rgLines.AddFirst( new TextLine( 0, string.Empty ) );

            while( _rgLines.Count > _iMaxHistory ) {
                _rgLines.RemoveLast();
            }

            return oNode;
        }
    }
}
