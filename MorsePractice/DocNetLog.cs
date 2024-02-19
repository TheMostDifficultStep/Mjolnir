using System;
using System.Collections.Generic;
using System.Text;

using Play.Interfaces.Embedding;
using Play.Edit;
using Play.Forms;
using Play.Parse;
using System.IO;

namespace Play.MorsePractice {
    /// <summary>
    /// I haven't rallied around a multi column set of events yet. Right
    /// now the base editor WILL track markers in the file. But won't tell
    /// you which row/line (combos) that have been touched. Still a work
    /// in progress, see the Kanji practice program for previous efforts.
    /// </summary>
    interface IPgLogEvents {
        void OnRowUpdate( Row oRow );
    }

    public class LogRow : Row {
        public LogRow() {
            _rgColumns = new Edit.Line[3];

            for( int i=0; i<_rgColumns.Length; i++ ) {
                _rgColumns[i] = new TextLine( i, string.Empty );
            }
        }
    }

    /// <summary>
    /// This is our new document to hold the net participants.
    /// </summary>
    public class DocMultiColumn :
        EditMultiColumn,
		IPgLoad<TextReader>,
		IPgSave<TextWriter>
    {
        public DocMultiColumn(IPgBaseSite oSiteBase) : base(oSiteBase) {
        }

        public Row InsertNew() {
            return InsertNew( _rgRows.Count );
        }

        public Row InsertNew( int iRow ) {
            try {
                Row oNew = new LogRow();

                _rgRows.Insert( iRow, oNew );

                RenumberRows();

                foreach( object oEvent in _rgListeners ) {
                    if( oEvent is IPgLogEvents oSend ) {
                        oSend.OnRowUpdate( oNew );
                    }
                }

                return oNew;
            } catch( ArgumentOutOfRangeException ) {
                LogError( "Row is out of bounds" );
            }
            return null;
        }

        public bool Load(TextReader oStream) {
            return true;
        }

        public bool InitNew() {
            InsertNew();
            return true;
        }

        public bool Save(TextWriter oStream) {
            return true;
        }

        /// <summary>
        /// Test a bulk loader. I think I'll move it to the base class
        /// and add a OnRowUpdate() to the IPgEditEvents interface...
        /// </summary>
        public class BulkLoader :
            IDisposable 
        {
            readonly DocMultiColumn _oHost;
                     bool           _fDisposed = false;
            public BulkLoader( DocMultiColumn oHost ) {
                _oHost = oHost ?? throw new ArgumentNullException();
            }

            public void Dispose() {
                if( !_fDisposed ) {
                    foreach( object oListener in _oHost._rgListeners ) {
                        if( oListener is IPgLogEvents oCall ) {
                            oCall.OnRowUpdate( null );
                        }
                    }
                    _oHost.RenumberRows();
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
    } // end class
}
