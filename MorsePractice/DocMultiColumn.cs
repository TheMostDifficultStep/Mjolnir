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
            _rgColumns = new Line[3];

            for( int i=0; i<_rgColumns.Length; i++ ) {
                _rgColumns[i] = new TextLine( i, string.Empty );
            }
        }
    }

    public class DocMultiColumn :
        EditMultiColumn,
		IPgLoad<TextReader>,
		IPgSave<TextWriter>
    {
        public DocMultiColumn(IPgBaseSite oSiteBase) : base(oSiteBase) {
        }

        protected virtual bool Initialize() {
            InsertNew();

            return true;
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
            return Initialize();
        }

        public bool InitNew() {
            return Initialize();
        }

        public bool Save(TextWriter oStream) {
            return true;
        }
    }
}
