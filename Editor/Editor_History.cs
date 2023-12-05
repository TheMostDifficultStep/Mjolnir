using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;

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
    public class EditorHistory {
        protected readonly IPgBaseSite _oBaseSite;

        protected IPgLineFactory _oLineFactory;
        protected int            _iCurrentLine;
        protected List<Line>     _rgLines = new();

        public IReadOnlyList<Line> Lines { get; private set; }


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

        public int HistoryCapacity { 
            get { return _rgLines.Capacity; }
            set {
                // Shuffle contents.
                _rgLines.Capacity = value;
            }
        }
    }
}
