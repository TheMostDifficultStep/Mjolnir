using System;
using System.Collections.Generic;
using System.IO;

using Play.Interfaces.Embedding;
using Play.Edit;
using Play.Parse.Impl;

namespace Play.MorsePractice {
    public class LogRow : Row {
        public LogRow() {
            _rgColumns = new Line[3];

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
        readonly protected IPgRoundRobinWork _oWorkPlace; 

        /// <exception cref="InvalidOperationException" />
        /// <exception cref="InvalidCastException" />
        /// <exception cref="NullReferenceException" />
        public DocMultiColumn(IPgBaseSite oSiteBase) : base(oSiteBase) {
            IPgScheduler oSchedular = (IPgScheduler)_oSiteBase.Host.Services;

            _oWorkPlace = oSchedular.CreateWorkPlace() ?? throw new InvalidOperationException( "Need the scheduler service in order to work. ^_^;" );
        }

        public override void Dispose() {
            _oWorkPlace.Stop();
            base.Dispose();
        }

        public Row InsertNew() {
            return InsertNew( _rgRows.Count );
        }

        public Row InsertNew( int iRow ) {
            try {
                Row oNew = new LogRow();

                _rgRows.Insert( iRow, oNew );

                RenumberRows();
                DoParse     ();

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
                    RowStream        oStream       = CreateColumnStream( iColumn:0 );
                    ParseColumnText  oParseHandler = new ParseColumnText( oStream, oGrammar, LogError );

                    oParseHandler.Parse();
                }
            } catch( InvalidCastException ) {
                LogError( "Likely services retrieval error" );
            }
        }

        public IEnumerator<int> GetParseEnum() {
            ParseColumn( 0 );

            foreach( object oListener in _rgListeners ) {
                if( oListener is IPgEditEvents oCall ) {
                    oCall.OnDocFormatted();
                }
            }

            yield return 0;
        }

        /// <summary>
        /// Schedule a reparse since we don't want to be parsing and updating
        /// right in the middle of typing EVERY character.
        /// </summary>
        /// <remarks>We'll have too keep this here, but we can move the rest.</remarks>
        public override void DoParse() {
            _oWorkPlace.Queue( GetParseEnum(), 2000 );
        }

        /// <summary>
        /// Test a bulk loader. I think I'll move it to the base class.
        /// It is slightly confusing that there is not an OnUpdateRow on the
        /// IPgEditEvents interface. Instead we just tell ourselves to reparse,
        /// which is basically all a "document" really needs to do. The window's
        /// cache manager get's it by the CreateHandler()
        /// </summary>
        public class BulkLoader :
            IDisposable 
        {
            readonly DocMultiColumn _oHost;
                     bool           _fDisposed = false;
            List<IPgEditHandler>    _rgHandlers = new List<IPgEditHandler>();
            public BulkLoader( DocMultiColumn oHost ) {
                _oHost = oHost ?? throw new ArgumentNullException();
                foreach( IPgEditEvents oCall in _oHost._rgListeners ) {
                    _rgHandlers.Add( oCall.CreateEditHandler() );
                }
            }

            public void Dispose() {
                if( !_fDisposed ) {
                    _oHost.RenumberRows();
                    foreach( IPgEditHandler oCall in _rgHandlers ) {
                        oCall.OnUpdated( null );
                    }
                    _oHost.DoParse();
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
