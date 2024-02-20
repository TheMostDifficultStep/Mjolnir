using System;
using System.Collections.Generic;
using System.Text;

using Play.Interfaces.Embedding;
using Play.Edit;
using Play.Forms;
using Play.Parse;
using System.IO;
using Microsoft.VisualBasic.Logging;
using Play.Parse.Impl;

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
    /// TODO: I can probably move most of this into the EditMultiColumn class.
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

                ReParse();

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

        protected void ParseColumn( int iColumn ) {
            try {
                IPgGrammers oGServ = (IPgGrammers)Services;
                if( oGServ.GetGrammer( "text" ) is Grammer<char> oGrammar ) {
                    RowStream        oStream       = CreateColumnStream( 0 );
                    ParseColumnText  oParseHandler = new ParseColumnText( oStream, oGrammar, LogError );

                    oParseHandler.Parse();
                }
            } catch( InvalidCastException ) {
                LogError( "Likely services retrieval error" );
            }
        }

        /// <summary>
        /// Normally I would schedule a re-parse but let's just do it 
        /// all right now...
        /// </summary>
        /// <remarks>We'll have too keep this here, but we can move the rest.</remarks>
        public virtual void ReParse() {
            ParseColumn( 0 );

            foreach( object oListener in _rgListeners ) {
                if( oListener is IPgEditEvents oCall ) {
                    oCall.OnDocFormatted();
                }
            }
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
                    _oHost.RenumberRows();
                    foreach( object oListener in _oHost._rgListeners ) {
                        if( oListener is IPgLogEvents oCall ) {
                            oCall.OnRowUpdate( null );
                        }
                    }
                    _oHost.ReParse();
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
