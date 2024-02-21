﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Play.Interfaces.Embedding;
using Play.Edit;
using Play.Parse.Impl;
using Play.Parse;

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
    public class DocLogMultiColumn :
        EditMultiColumn,
		IPgLoad<TextReader>,
		IPgSave<TextWriter>
    {
        readonly protected IPgRoundRobinWork _oWorkPlace; 

        public CallsDoc Calls { get; } // List of callsigns in left hand column of notes file.

		protected class DocSlot :
			IPgBaseSite
		{
			protected readonly DocLogMultiColumn _oHost;

			public DocSlot( DocLogMultiColumn oHost ) {
				_oHost = oHost ?? throw new ArgumentNullException( "Host" );
			}

			public IPgParent Host => _oHost;

            public void LogError(string strMessage, string strDetails, bool fShow=true) {
				_oHost.LogError( strDetails );
			}

			public void Notify( ShellNotify eEvent ) {
			}
		}

        /// <exception cref="InvalidOperationException" />
        /// <exception cref="InvalidCastException" />
        /// <exception cref="NullReferenceException" />
        public DocLogMultiColumn(IPgBaseSite oSiteBase) : base(oSiteBase) {
            IPgScheduler oSchedular = (IPgScheduler)_oSiteBase.Host.Services;

            _oWorkPlace = oSchedular.CreateWorkPlace() ?? throw new InvalidOperationException( "Need the scheduler service in order to work. ^_^;" );
            Calls       = new CallsDoc( new DocSlot( this ) ); // document for outline, compiled list of stations
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
            if( !Calls.InitNew() )
                return false;

            return true;
        }

        public bool InitNew() {
            if( !Calls.InitNew() )
                return false;

            InsertNew();

            return true;
        }

        public bool Save(TextWriter oStream) {
            return true;
        }


        /// <summary>
        /// Scan the entire file for callsigns and pop them into the "Calls" editor.
        /// It is still advantageous to use the parsed callsign so I don't have to
        /// worry about padding, and now if there happened to be two callsigns in the
        /// column zero we'd pick 'em up. Kinda weird but ok...
        /// </summary>
        public void ScanForCallsigns() {
            Calls.Clear();
            List< string > rgCallSigns = new List<string>();

            try {
                foreach( Row oRow in _rgRows ) {
                    Line oLine = oRow[0];

                    foreach( IColorRange oColor in oLine.Formatting ) {
                        if( oColor is IPgWordRange oWord &&
                            string.Compare( oWord.StateName, "callsign" ) == 0 ) 
                        {
                            rgCallSigns.Add( oLine.SubString( oWord.Offset, oWord.Length ) );
                        }
                    }
                }
                IEnumerable<IGrouping<string, string>> dupes = 
                    rgCallSigns.GroupBy(x => x.ToLower() ).OrderBy( y => y.Key.ToLower() );

                foreach( IGrouping<string, string> foo in dupes ) {
                    Calls.LineAppend( foo.Key + " : " + foo.Count().ToString() );
                }
                Calls.LineInsert( "Operator Count : " + dupes.Count().ToString() );
            } catch( Exception oEx ) {
                Type[] rgErrors = { typeof( NullReferenceException ),
                                    typeof( ArgumentNullException ),
                                    typeof( InvalidCastException ) };
                if( rgErrors.IsUnhandled( oEx ) )
                    throw;

                LogError( "Scan for callsigns suffered an error." );
            }
        }

        protected void ParseColumn( int iColumn ) {
            try {
                IPgGrammers oGServ = (IPgGrammers)Services;
                if( oGServ.GetGrammer( "text" ) is Grammer<char> oGrammar ) {
                    RowStream        oStream       = CreateColumnStream( iColumn:0 );
                    ParseColumnText  oParseHandler = new ParseColumnText( oStream, oGrammar, LogError );

                    foreach( Row oRow in _rgRows ) {
                        oRow[iColumn].Formatting.Clear();
                    }

                    oParseHandler.Parse();
                }
            } catch( Exception oEx ) {
                Type[] rgErrors = { typeof( InvalidCastException ),
                                    typeof( ArgumentOutOfRangeException ),
                                    typeof( ArgumentNullException ) };
                if( rgErrors.IsUnhandled( oEx ) )
                    throw;

                LogError( "Trouble setting up column parse." );
            }
        }

        /// <summary>
        /// This allows use to use my scheduler to delay the parse until
        /// (2 seconds) of time has passed since the last parse request.
        /// </summary>
        /// <returns></returns>
        public IEnumerator<int> GetParseEnum() {
            ParseColumn( 0 );
            ScanForCallsigns();

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
            readonly DocLogMultiColumn _oHost;
                     bool           _fDisposed = false;
            List<IPgEditHandler>    _rgHandlers = new List<IPgEditHandler>();
            public BulkLoader( DocLogMultiColumn oHost ) {
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
