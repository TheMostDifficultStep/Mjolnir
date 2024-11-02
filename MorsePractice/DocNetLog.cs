using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Play.Interfaces.Embedding;
using Play.Edit;
using Play.Parse;

namespace Play.MorsePractice {
    public class LogRow : Row {
        public LogRow() {
            _rgColumns = new Line[3];

            for( int i=0; i<_rgColumns.Length; i++ ) {
                _rgColumns[i] = new TextLine( i, string.Empty );
            }
        }

        public const int ColumnCall = 0;
        public const int ColumnStat = 1;
        public const int ColumnNote = 2;
    }

    /// <summary>
    /// This is our new document to hold the net participants. I keep this
    /// separate from the DocNetHost since it contains this, notes, outline and properties.
    /// Seems best to go this way.
    /// </summary>
    /// <seealso cref="DocNetHost"/>
    public class DocLogMultiColumn :
        EditMultiColumn,
		IPgLoad,
		IPgSave
    {
        readonly protected IPgRoundRobinWork _oWorkPlace; 
        readonly protected string            _strIcon = @"Play.MorsePractice.Content.icons8-copybook-60.jpg";

        // Crude way to get edit events. Mostly outsides want to know when rows are added I'm
        // blind to row removal, but that's actually pri 2 at the moment.
        public event Action<Row> Event_RowAdded;
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
        }

        public override void Dispose() {
            _oWorkPlace.Stop();
            base.Dispose();
        }

        public override WorkerStatus PlayStatus {
			get { 
                if( HighLight != null )
                    return WorkerStatus.BUSY;
                
                return WorkerStatus.FREE;
            }
		}

        public void InsertNew() {
            InsertNew( _rgRows.Count );
        }

        /// <summary>
        /// Note: It's perfectly legal to insert at the element count.
        /// This is effectively an append.
        /// </summary>
        /// <remarks>I could return an actual LogRow... :-/</remarks>
        /// <returns>Newly inserted LogRow</returns>
        public Row InsertNew( int iRow ) {
            try {
                Raise_DocUpdateBegin();

                Row oNew = new LogRow();

                _rgRows.Insert( iRow, oNew );

                RenumberAndSumate ();
                Raise_DocUpdateEnd( IPgEditEvents.EditType.Rows, oNew );

                // A little hacky but its how our outline can get some events.
                Event_RowAdded?.Invoke( oNew );

                DoParse();

                return oNew;
            } catch( ArgumentOutOfRangeException ) {
                LogError( "Row is out of bounds" );
            }
            return null;
        }

        public bool InitNew() {
            InsertNew();

            return true;
        }

        /// <summary>
        /// Scan the entire file for callsigns and pop them into the "Calls" editor.
        /// It is still advantageous to use the parsed callsign so I don't have to
        /// worry about padding, and now if there happened to be two callsigns in the
        /// column zero we'd pick 'em up. Kinda weird but ok...
        /// </summary>
        //public void ScanForCallsigns() {
        //    Calls.Clear();
        //    List< string > rgCallSigns = new List<string>();

        //    try {
        //        foreach( Row oRow in _rgRows ) {
        //            Line oLine = oRow[0];

        //            foreach( IColorRange oColor in oLine.Formatting ) {
        //                if( oColor is IPgWordRange oWord &&
        //                    string.Compare( oWord.StateName, "callsign" ) == 0 ) 
        //                {
        //                    rgCallSigns.Add( oLine.SubString( oWord.Offset, oWord.Length ) );
        //                }
        //            }
        //        }
        //        IEnumerable<IGrouping<string, string>> dupes = 
        //            rgCallSigns.GroupBy(x => x.ToLower() ).OrderBy( y => y.Key.ToLower() );

        //        foreach( IGrouping<string, string> foo in dupes ) {
        //            Calls.LineAppend( foo.Key + " : " + foo.Count().ToString() );
        //        }
        //        string strOperatorCount = dupes.Count().ToString();

        //        // Update the operator count property.
        //        if( _oSiteBase.Host is DocNetHost oNetDoc ) {
        //            oNetDoc.Props.ValueUpdate( (int)DocLogProperties.Names.Operator_Cnt, strOperatorCount );
        //            oNetDoc.Props.DoParse();
        //        }
        //    } catch( Exception oEx ) {
        //        Type[] rgErrors = { typeof( NullReferenceException ),
        //                            typeof( ArgumentNullException ),
        //                            typeof( ArgumentOutOfRangeException ),
        //                            typeof( InvalidCastException ) };
        //        if( rgErrors.IsUnhandled( oEx ) )
        //            throw;

        //        LogError( "Scan for callsigns suffered an error." );
        //    }
        //}

        /// <summary>
        /// This allows me to use my scheduler to delay the parse until
        /// (2 seconds) of time has passed since the last parse request.
        /// But then we just do the whole parse before returning.
        /// </summary>
        /// <returns></returns>
        public IEnumerator<int> GetParseEnum() {
            RenumberAndSumate();
            ParseColumn      ( 0 );
            ParseColumn      ( 2 );

            Raise_DocFormatted();

            yield return 0;
        }

        /// <summary>
        /// Schedule a reparse since we don't want to be parsing and updating
        /// right in the middle of typing EVERY character.
        /// </summary>
        /// <remarks>We'll have to keep this here, but we can move the rest.</remarks>
        public override void DoParse() {
            _oWorkPlace.Queue( GetParseEnum(), 2000 );
        }

    } // end class
}
