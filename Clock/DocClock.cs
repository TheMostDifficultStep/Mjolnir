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

        public bool Is24Hour { get; set; } = false;

        public RowClock( string strLabel, int iOffset = 0, TimeZoneInfo oZone = null ) {
            _rgColumns    = new Line[3];
            _rgColumns[ColumnTime] = new TextLine( 0, string.Empty );
            _rgColumns[ColumnDate] = new TextLine( 1, string.Empty );
            _rgColumns[ColumnZone] = new TextLine( 2, strLabel );

            _iOffset = iOffset;
            _oZone   = oZone;
        }

        public void SetTime( DateTime sUTC ) {
            int iOffset = _iOffset;

            if( _oZone is not null && _oZone.IsDaylightSavingTime( sUTC ) ) {
                iOffset += 1;
            }
            DateTime dtOffset = sUTC.AddHours( iOffset );
            string   strTime;

            if( Is24Hour ) {
                strTime = dtOffset.Hour  .ToString( "D2" ) + ":" + 
                          dtOffset.Minute.ToString( "D2");
            } else {
                int    iHour     = dtOffset.Hour;
                string strMidDay = "am"; 

                if( iHour > 12 ) {
                    strMidDay = "pm";
                    iHour    -= 12;
                }
                if( iHour == 0 ) {
                    iHour    += 12;
                }
                strTime = iHour.ToString( "D2" ) + ":" +
                          dtOffset.Minute.ToString( "D2" ) + strMidDay;
            }

            Line oTime = this[0];
            oTime.Empty();
            oTime.TryAppend( strTime );

            Line oDate = this[1];
            oDate.Empty();
            oDate.TryAppend( dtOffset.ToShortDateString() );
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

        public void SetCheck( string strCheck ) {
            this[DCol.Check].TryReplace( strCheck );
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
                TimeZoneInfo oLocalZone = TimeZoneInfo.Local;

                foreach( TimeZoneInfo oZone in rgZones ) {
                    int    iOffset = oZone.BaseUtcOffset.Hours;
                    string strClip = oZone.DisplayName[12..];
                    // BUG: won't work in other languages.
                    if( !strClip.StartsWith( "Coordinated" ) ) {
                        RowZone oRowNew = new RowZone( strClip, iOffset, oZone );
                        if( oLocalZone.Equals( oZone ) ) {
                            oRowNew.SetCheck( CheckSetValue );
                        }
                        _rgRows.Add( oRowNew );
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
        IPgLoad
    {
        public DocumentClock( IPgBaseSite oSite ) :
            base( oSite )
        {
        }

        // Todo: use a manupulator in the future...
        public void Append( RowClock oNew ) {
            _rgRows.Add( oNew );
        }

        public void Clear() {
            _rgRows.Clear();
        }

        public bool InitNew(){
            return true;
        }
    }

    public class DocumentContainer :
        IDisposable,
        IPgParent,
        IPgLoad<XmlNode>,
        IPgSave<XmlNode>,
        IPgLoad<TextReader>
    {
        protected IPgRoundRobinWork _oWorkPlace;
        protected int               _iTimoutInMillisecs = 60000;

        public event Action ClockEvent;

        public bool      IsDirty   => false;
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

			_oWorkPlace = ((IPgScheduler)Services).CreateWorkPlace();
            _oWorkPlace.Queue( CreateWorker(), 0 );

            // ReLoad(); only need if no worker task

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

        /// <summary>
        /// BUG: Let's move the worker to the container doc so we
        /// can unify the time update code to one place.
        /// </summary>
        /// <returns></returns>
        public IEnumerator<int> CreateWorker() {
            while( true ) {
                ReLoad();

                ClockEvent?.Invoke();

                // Note: Changing this doesn't seem to effect anything.
                //       You need to restart the workplace.
                yield return TimoutInMillisecs;
            }
        }
        public void ReLoad() {
            DateTime oDT = DateTime.Now.ToUniversalTime();

            DocClock.Clear();

            RowClock oNew = new RowClock( "UTC", 0 );
            oNew.SetTime( oDT );
            DocClock.Append(oNew);

            foreach( Row oRow in DocZones ) {
                if( oRow is RowZone oRowZone && oRowZone.IsChecked ) {
                    if( int.TryParse( oRowZone[RowZone.DCol.Offset].AsSpan, out int iOffset ) ) {
                        string strZone = oRowZone[RowZone.DCol.Zone].ToString();
                        const int iMaxTitle = 10;
                        if( strZone.Length > iMaxTitle ) {
                            strZone = strZone[0..iMaxTitle];
                        }
                        oNew = new RowClock( strZone, iOffset, oRowZone.Zone );
                        oNew.SetTime( oDT );
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
