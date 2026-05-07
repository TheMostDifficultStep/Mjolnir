using System;
using System.Collections.Generic;
using System.IO;

using Play.Interfaces.Embedding;
using Play.Edit;
using System.Xml;

namespace Play.Clock {
    public class RowClock : Row {
        public const int ColumnTime = 0;
        public const int ColumnDate = 1;
        public const int ColumnZone = 2;

        protected int          _iOffset;
        protected TimeZoneInfo _oZone; 
        public RowClock( string strDate, string strLabel, int iOffset = 0, TimeZoneInfo oZone = null ) {
            _rgColumns    = new Line[3];
            _rgColumns[0] = new TextLine( 0, string.Empty );
            _rgColumns[1] = new TextLine( 1, strDate );
            _rgColumns[2] = new TextLine( 2, strLabel );

            _iOffset = iOffset;
            _oZone   = oZone;
        }

        public void SetTime( DateTime oDT ) {
            int iOffset = _iOffset;

            if( _oZone is not null && _oZone.IsDaylightSavingTime( oDT ) ) {
                iOffset += 1;
            }
            DateTime dtOffset = oDT.AddHours( iOffset );

            string strTime = dtOffset.Hour  .ToString( "D2" ) + ":" + 
                             dtOffset.Minute.ToString( "D2");

            Line oTime = this[0];
            oTime.Empty();
            oTime.TryAppend( strTime );

            Line oDate = this[1];
            oDate.Empty();
            oDate.TryAppend( dtOffset.ToShortDateString() );
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

    public class RowZone : Row {
        public enum DCol : int {
            Check =0,
            Offset,
            Zone,
        }

        static int ColumnCount = Enum.GetValues(typeof(DCol)).Length;
        public Line this[DCol eValue] => this[(int)eValue];

        public TimeZoneInfo Zone {get; }

        public static string CheckMarkValue {get;} = "\x2714";

        public RowZone( string strTimeZone, int iOffset, TimeZoneInfo oZone ) {
            _rgColumns = new Line[ColumnCount];

            CreateColumn( DCol.Check,  GetCheck( false ) );
            CreateColumn( DCol.Offset, iOffset.ToString() );
            CreateColumn( DCol.Zone,   strTimeZone );

            Zone = oZone ?? throw new ArgumentNullException();
        }

        /// <summary>
        /// I should make this templatized. I do the same thing in the fileman viewer.
        /// </summary>
        void CreateColumn( DCol eCol, string strValue ) {
			_rgColumns[(int)eCol] = new TextLine( (int)eCol, strValue );
        }

        public static string GetCheck( bool fValue ) {
            return fValue ? CheckMarkValue : string.Empty;
        }

        public bool IsChecked {
            get {
                return !this[DCol.Check].IsEmpty;
            }
        }
    }

    public class DocumentZones :
        EditMultiColumn,
        IPgLoad<XmlNode>,
        IPgSave<XmlNode>
    {
        public DocumentZones(IPgBaseSite oSiteBase) : base(oSiteBase) {
            CheckColumn   = (int)RowZone.DCol.Check;
            CheckSetValue = RowZone.CheckMarkValue;
            CheckClrValue = string.Empty;

            _bIsSingleCheck = false;
        }

        public bool InitNew() {
			try {
                var rgZones = TimeZoneInfo.GetSystemTimeZones();
                foreach( TimeZoneInfo oZone in rgZones ) {
                    int    iOffset = oZone.BaseUtcOffset.Hours;
                    string strClip = oZone.DisplayName[12..];
                    // BUG: won't work in other languages.
                    if( !strClip.StartsWith( "Coordinated" ) ) {
                        _rgRows.Add( new RowZone( strClip, iOffset, oZone ) );
                    }
                }

                RenumberAndSumate();

                return true;
			} catch( Exception oEx ) {
                Type[] rgErrors = { typeof( ArgumentNullException ),
                                    typeof( NullReferenceException ) };
                if( rgErrors.IsUnhandled( oEx ) )
                    throw;
			}

            return false;
        }

        public bool Load(XmlNode oStream) {
            return InitNew();
        }

        public bool Save(XmlNode oStream) {
            return true;
        }
    }
    public class DocumentClock :
        EditMultiColumn,
        IPgSave<TextWriter>,
        IPgLoad<TextReader>,
        IPgLoad<XmlNode>,
        IPgSave<XmlNode>
    {
        protected IPgRoundRobinWork _oWorkPlace;
        protected int               _iTimoutInMillisecs = 60000;

        public event Action ClockEvent;

        public DocumentClock( IPgBaseSite oSite ) :
            base( oSite )
        {
        }

        public void Reset( DateTime oDT ) {
            _rgRows.Clear();

            _rgRows.Add( new RowClock( string.Empty,  "utc"   ) );
            _rgRows.Add( new RowClock( string.Empty,  "local" ) );
            _rgRows.Add( new RowClock( "12 hr clock", "local" ) );

            foreach( Row oRow in _rgRows ) {
                if( oRow is RowClock oRClock ) {
                    SetTimeOnRow( oDT, oRClock );
                }
            }
        }

        // Todo: use a manuplator in the future...
        public void Append( RowClock oNew ) {
            _rgRows.Add( oNew );
        }

        public bool InitNew(){
			try {
                Reset( DateTime.Now );
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
            return InitNew();
        }

        public bool Save(TextWriter oStream) {
            return true;
        }

        /// <summary>
        /// At present we don't do much more than init.
        /// </summary>
        /// <param name="oStream">It's ok for this one to be null!</param>
        public bool Load(XmlNode oStream) {
            return InitNew();
        }

        public bool Save(XmlNode oStream) {
            return true;
        }

        public enum ClockUpdateInterval {
            Slow,
            Fast
        }

        public int TimoutInMillisecs {
            set {
                _iTimoutInMillisecs = value;
                _oWorkPlace.Start( _iTimoutInMillisecs );
            }
            get {
                return _iTimoutInMillisecs;
            }
        }

        /// <summary>
        /// Speed up the clock updates if focused. Else
        /// slow down.
        /// </summary>
        /// <remarks>BUG: This actually doesn't make sense on the
        /// document level. The views should set their own pace.
        /// I'll fix that later...</remarks>
        public void SetTimeout( ClockUpdateInterval eSpeed ) {
            switch( eSpeed ) {
                case ClockUpdateInterval.Slow:
                    TimoutInMillisecs = 60000;
                    break;
                case ClockUpdateInterval.Fast:
                    TimoutInMillisecs = 1000;
                    break;
            }
        }

        public static void SetTimeOnRow( DateTime oDt, RowClock oRow ) {
            switch( oRow.At ) {
                default:
                case 0:
                    oRow.SetTime( oDt.ToUniversalTime() );
                    break;
                case 1:
                    oRow.SetTime( oDt );
                    break;
                case 2:
                    oRow.SetLocal12( oDt );
                    break;
            }
        }

        public IEnumerator<int> CreateWorker() {
            while( true ) {
                DateTime oDT = DateTime.Now;
                
                foreach( RowClock oRow in _rgRows ) {
                    SetTimeOnRow( oDT, oRow );
                }

                ClockEvent?.Invoke();

                // Note: Changing this doesn't seem to effect anything.
                //       You need to restart the workplace.
                yield return TimoutInMillisecs;
            }
        }
    }

    public class DocumentContainer :
        IDisposable,
        IPgParent,
        IPgLoad<XmlNode>,
        IPgSave<XmlNode>,
        IPgLoad<TextReader>
    {
        public bool IsDirty => false;

        public IPgParent Parentage => _oSiteBase.Host;

        public IPgParent Services  => Parentage;

        protected readonly IPgBaseSite _oSiteBase;

        public DocumentClock DocClock { get; }
        public DocumentZones DocZones { get; }

        public class DocSlot : 
			IPgBaseSite
		{
			protected readonly DocumentContainer _oDoc;

			public DocSlot( DocumentContainer oDoc ) {
				_oDoc = oDoc ?? throw new ArgumentNullException( "Document must not be null." );
			}

			public void LogError( string strMessage, string strDetails, bool fShow=true ) {
				_oDoc._oSiteBase.LogError( strMessage, "PropDocSlot : " + strDetails );
			}

			public void Notify( ShellNotify eEvent ) {
			}

			public IPgParent Host => _oDoc;
		}

        public DocumentContainer( IPgBaseSite oSiteBase ) {
            _oSiteBase = oSiteBase ?? throw new ArgumentNullException();

            DocClock = new( new DocSlot( this ) );
            DocZones = new( new DocSlot( this ) );
        }

        public void Dispose() {
            DocClock.Dispose();
            DocZones.Dispose();
        }

        public bool InitNew() {
            if( !DocClock.InitNew() ) {
                return false;
            }

            if( !DocZones.InitNew() ) {
                return false;
            }

            return true;
        }

        public bool Load(XmlNode oStream) {
            return InitNew();
        }
        public bool Load(TextReader oStream) {
            return InitNew();
        }
        public bool Save(XmlNode oStream) {
            return true;
        }

        public void ReLoad() {
            DateTime oDT = DateTime.Now;

            DocClock.Reset(oDT);

            foreach( Row oRow in DocZones ) {
                if( oRow is RowZone oZone && oZone.IsChecked ) {
                    if( int.TryParse( oZone[RowZone.DCol.Offset].AsSpan, out int iOffset ) ) {
                        string strZone = oZone[RowZone.DCol.Zone].ToString();
                        const int iMax = 10;
                        if( strZone.Length > iMax ) {
                            strZone = strZone[0..iMax];
                        }
                        RowClock oNew = new RowClock( string.Empty, 
                                                      strZone,
                                                      iOffset, 
                                                      oZone.Zone );
                        DocumentClock.SetTimeOnRow(oDT, oNew );
                        DocClock.Append( oNew );
                    } else {
                        _oSiteBase.LogError( "Clock", "Bad Zone offset" );
                    }
                }
            }

            DocClock.RenumberAndSumate();
            DocClock.Raise_DocLoaded  ();
        }

    }
}
