using System;
using System.Collections.Generic;
using System.IO;

using Play.Interfaces.Embedding;
using Play.Edit;

namespace Play.Clock {
    public class ClockRow : Row {
        public const int ColumnTime = 0;
        public const int ColumnDate = 1;
        public const int ColumnZone = 2;
        public ClockRow( string strDate, string strLabel ) {
            _rgColumns    = new Line[3];
            _rgColumns[0] = new TextLine( 0, string.Empty );
            _rgColumns[1] = new TextLine( 1, strDate );
            _rgColumns[2] = new TextLine( 2, strLabel );
        }

        public void SetTime( DateTime oDT ) {
            string strTime = oDT.Hour.ToString( "D2" ) + ":" + oDT.Minute.ToString( "D2");

            Line oTime = this[0];
            oTime.Empty();
            oTime.TryAppend( strTime );

            Line oDate = this[1];
            oDate.Empty();
            oDate.TryAppend( oDT.ToShortDateString() );
        }

        public void SetLocal12( DateTime oDT ) {
            int    iHour     = oDT.Hour;
            string strMidDay = "am"; 

            if( iHour > 12 ) {
                strMidDay = "pm";
                iHour    -= 12;
            }
            if( iHour == 0 ) {
                iHour    += 12;
            }

            Line oTime = this[0];
            oTime.Empty();
            oTime.TryAppend( iHour.ToString( "D2" ) + ":" + oDT.Minute.ToString( "D2" ) + strMidDay );
        }
    }

    public class DocumentClock :
        EditMultiColumn,
        IPgSave<TextWriter>,
        IPgLoad<TextReader>
    {
		public class DocSlot : 
			IPgBaseSite
		{
			protected readonly DocumentClock _oDoc;

			public DocSlot( DocumentClock oDoc ) {
				_oDoc = oDoc ?? throw new ArgumentNullException( "Document must not be null." );
			}

			public void LogError( string strMessage, string strDetails, bool fShow=true ) {
				_oDoc._oSiteBase.LogError( strMessage, "PropDocSlot : " + strDetails );
			}

			public void Notify( ShellNotify eEvent ) {
			}

			public IPgParent Host => _oDoc;
		}

        protected IPgRoundRobinWork _oWorkPlace;
        protected int               _iTimoutInMillisecs = 60000;

        public event Action ClockEvent;

        public DocumentClock( IPgBaseSite oSite ) :
            base( oSite )
        {
        }

        public bool InitNew(){
			try {
                _rgRows.Add( new ClockRow( string.Empty,  "utc"   ) );
                _rgRows.Add( new ClockRow( string.Empty,  "local" ) );
                _rgRows.Add( new ClockRow( "12 hr clock", "local" ) );

                RenumberAndSumate();

				_oWorkPlace = ((IPgScheduler)Services).CreateWorkPlace();
                _oWorkPlace.Queue( CreateWorker(), 0 );
			} catch( Exception oEx ) {
                Type[] rgErrors = { typeof( ArgumentNullException ),
                                    typeof( NullReferenceException ) };
                if( rgErrors.IsUnhandled( oEx ) )
                    throw;

				LogError( "Can't set up time update worker." );
			}

            return true;
        }

        public bool Load(TextReader oStream) {
            return true;
        }

        public bool Save(TextWriter oStream) {
            return true;
        }

        public enum ClockUpdateInterval {
            Slow,
            Fast
        }

        public void SetTimeout( ClockUpdateInterval eSpeed ) {
            switch( eSpeed ) {
                case ClockUpdateInterval.Slow:
                    _iTimoutInMillisecs = 60000;
                    break;
                case ClockUpdateInterval.Fast:
                    _iTimoutInMillisecs = 1000;
                    break;
            }
            _oWorkPlace.Start( _iTimoutInMillisecs );
        }

        public IEnumerator<int> CreateWorker() {
            while( true ) {
                DateTime oDT = DateTime.Now;
                
                foreach( ClockRow row in _rgRows ) {
                    switch( row.At ) {
                        case 0:
                            row.SetTime( oDT.ToUniversalTime() );
                            break;
                        case 1:
                            row.SetTime( oDT );
                            break;
                        case 2:
                            row.SetLocal12( oDT );
                            break;
                    }
                }

                ClockEvent?.Invoke();

                // Note: Changing this doesn't seem to effect anything.
                //       You need to restart the workplace.
                yield return _iTimoutInMillisecs;
            }
        }
    }
}
