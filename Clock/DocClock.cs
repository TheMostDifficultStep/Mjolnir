using Play.Edit;
using Play.Forms;
using Play.Integration;
using Play.Interfaces.Embedding;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Security;
using System.Text;
using System.Xml;

namespace Play.Clock {
    public class RowClock : Row {
        public const int ColumnTime = 0;
        public const int ColumnDate = 1;
        public const int ColumnZone = 2;
        public const int ColumnDofW = 3;

        protected int          _iOffset;
        protected TimeZoneInfo _oZone; 

        public bool Is24Hour { get; set; } = false;

        protected void SetColumn( int iColumn, string strValue ) {
            _rgColumns[iColumn] = new TextLine( iColumn, strValue );
        }

        public RowClock( string strLabel, int iOffset = 0, TimeZoneInfo oZone = null ) {
            _rgColumns = new Line[4];
            
            SetColumn( ColumnTime, string.Empty );
            SetColumn( ColumnDate, string.Empty );
            SetColumn( ColumnZone, strLabel );
            SetColumn( ColumnDofW, string.Empty );

            _iOffset = iOffset;
            _oZone   = oZone;
        }

        public DateTime Time {
            set {
                int iOffset = _iOffset;

                if( _oZone is not null && _oZone.IsDaylightSavingTime( value ) ) {
                    iOffset += 1;
                }
                DateTime dtOffset = value.AddHours( iOffset );
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

                // Zone is constant.
                this[ColumnTime].TryReplace( strTime );
                this[ColumnDate].TryReplace( dtOffset.ToShortDateString() );
                this[ColumnDofW].TryReplace( dtOffset.DayOfWeek.ToString() );
            }
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

        public RowZone( string strTimeZone, TimeZoneInfo oZone ) {
            Zone = oZone ?? throw new ArgumentNullException();

            _rgColumns = new Line[ColumnCount];

            CreateColumn( DCol.Check,  GetCheck( false ) );
            CreateColumn( DCol.Offset, Offset.ToString() );
            CreateColumn( DCol.Zone,   strTimeZone );

        }

        public int Offset {
            get {
                if( Zone is not null )
                    return Zone.BaseUtcOffset.Hours;

                return 0;
            }
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
                    string strClip = oZone.DisplayName[12..];
                    // BUG: won't work in other languages.
                    if( !strClip.StartsWith( "Coordinated" ) ) {
                        RowZone oRowNew = new RowZone( strClip, oZone );
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

        public void SetCheck( RowZone oZone ) {
            oZone.SetCheck( CheckSetValue );
        }
    }
    public class DocumentClock :
        EditMultiColumn,
        IPgLoad
    {

        public event Action ClockEvent;

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

        public void UpdateTime() {
            DateTime oUtc = DateTime.UtcNow.ToUniversalTime();

            foreach( Row oRow in _rgRows ) {
                if( oRow is RowClock oCRow ) {
                    oCRow.Time = oUtc;
                }
            }

            ClockEvent?.Invoke();
        }

        public bool InitNew(){
            return true;
        }
    }

    public class DocumentSched :
        EditMultiColumn,
        IPgLoad<XmlNode>,
        IPgSave<XmlNode>
    {
        public class RowSched : Row {
            public enum DCol : int {
                Time =0,
                Freq,
                On,
                Desc
            }

            static int ColumnCount = Enum.GetValues(typeof(DCol)).Length;
            public Line this[DCol eValue] => this[(int)eValue];

            public TimeZoneInfo Zone {get; }

            public RowSched( 
                TimeZoneInfo oZone, string strTime, string strFreq,
                string strOn, string strDesc 
            ) {
                //Zone = oZone ?? throw new ArgumentNullException();

                _rgColumns = new Line[ColumnCount];

                CreateColumn( DCol.Time, strTime );
                CreateColumn( DCol.Freq, strFreq );
                CreateColumn( DCol.On,   strOn );
                CreateColumn( DCol.Desc, strDesc );
            }

            public int Offset {
                get {
                    if( Zone is not null )
                        return Zone.BaseUtcOffset.Hours;

                    return 0;
                }
            }


            /// <summary>
            /// I should make this templatized. I do the same thing in the fileman viewer.
            /// </summary>
            void CreateColumn( DCol eCol, string strValue ) {
			    _rgColumns[(int)eCol] = new TextLine( (int)eCol, strValue );
            }

        } // end class

        public DocumentSched(IPgBaseSite oSiteBase) : base(oSiteBase) {
        }

        public bool InitNew() {
            return true;
        }

        public bool Load(XmlNode oXmlRoot) {
            ArgumentNullException.ThrowIfNull( oXmlRoot );

            foreach( XmlNode oNode in oXmlRoot.SelectNodes( "Events/Event" ) ) {
                if( oNode is XmlElement oXmlNode ) {
                  //string strUtc  = oXmlNode.GetAttribute( "utc" );
                    string strTime = oXmlNode.GetAttribute( "time" );
                    string strFreq = oXmlNode.GetAttribute( "freq" );
                    string strOn   = oXmlNode.GetAttribute( "on" );
                    string strDesc = oXmlNode.InnerText;

                    _rgRows.Add( new RowSched( null, strTime, strFreq, strOn, strDesc ));
                }
            }
            RenumberAndSumate();
            Raise_DocLoaded  ();

            return true;
        }

        public bool Save(XmlNode oStream) {
            return true;
        }
    } // End class

    public class DocumentContainer :
        IDisposable,
        IPgParent,
        IPgLoad<XmlNode>,
        IPgSave<XmlNode>,
        IPgLoad<TextReader>
    {
        protected class TextSlot : IPgBaseSite {
            readonly DocumentContainer    _oHost; 
            protected IPgLoad<TextReader> _oGuestLoad;
            protected IPgSave<TextWriter> _oGuestSave;
            protected string              _strFileName;
            protected Encoding            _oEncoding;

            protected string FileName {
                get { return _strFileName; }
                set { _strFileName = value; }
            }

            public TextSlot( DocumentContainer oHost ) {
                _oHost = oHost ?? throw new ArgumentNullException();
            }

            public IPgLoad<TextReader> Guest { 
                get { return _oGuestLoad; }
                set { 
                    _oGuestLoad = value; 
                    _oGuestSave = (IPgSave<TextWriter>)value;
                }
            }

            public IPgParent Host => _oHost;

            protected void LogError( string strMessage ) {
                _oHost._oSiteBase.LogError( "schedule", strMessage );
            }

            public void Notify(ShellNotify eEvent) {
                _oHost._oSiteBase.Notify( eEvent );
            }

            public void LogError(string strMessage, string strDetails, bool fShow = true) {
                _oHost._oSiteBase.LogError( strMessage, strDetails );
            }

			public static readonly Type[] _rgFileErrors = { 
				typeof( ArgumentNullException ),
				typeof( ArgumentException ),
				typeof( NullReferenceException ),
				typeof( DirectoryNotFoundException ),
				typeof( IOException ),
				typeof( UnauthorizedAccessException ),
				typeof( PathTooLongException ),
				typeof( SecurityException ),
                typeof( InvalidOperationException ),
                typeof( NotSupportedException ),
                typeof( FileNotFoundException ) };

            public virtual bool Load( string strFileName ) {
                if( _oGuestLoad == null ) {
                    LogError( "Problem loading Schedule file" );
                    return( false );
                }

                Encoding utf8NoBom = new UTF8Encoding(false);

                try {
                    FileInfo oFile = new FileInfo(strFileName);

                    FileStream oByteStream = oFile.OpenRead(); // by default StreamReader closes the stream.
                    // Overridable versions of StreamReader can prevent that in higher versions of .net
                    using( StreamReader oReader = new StreamReader( oByteStream, utf8NoBom ) ) {
                        try {
							FileName = oFile.FullName; // Guests sometimes need this when loading.

                            if( _oGuestLoad.Load( oReader ) ) {
                                // Make sure you get the encoding AFTER you've read the file, else it'll be
                                // uninitialized. Not sure if encoding can change multiple times? This might
                                // bomb if I have a weird unicode file with no BOM.
                                bool fNoBOM = Equals(oReader.CurrentEncoding, utf8NoBom);
				                _oEncoding = fNoBOM ? utf8NoBom : oReader.CurrentEncoding;
                            }
						} catch( Exception oEx ) {
							if( _rgFileErrors.IsUnhandled( oEx ) )
								throw;

                            LogError( "Died trying to load : " + strFileName );
                            return false;
                        }
                    }
                } catch( Exception oEx ) {
					if( _rgFileErrors.IsUnhandled( oEx ) )
						throw;

                    LogError( "Could not find or session is currently open :" + strFileName );
                    return false;
                }

                return true;
            }
        } // end textslot

        protected IPgRoundRobinWork _oWorkPlace;
        protected int               _iTimoutInMillisecs = 60000;

        public bool      IsDirty   => false;
        public IPgParent Parentage => _oSiteBase.Host;
        public IPgParent Services  => Parentage;

        protected readonly IPgBaseSite _oSiteBase;
        protected readonly TextSlot    _oSlotSched; 

        public DocumentClock DocClock { get; }
        public DocumentZones DocZones { get; }
        public DocumentSched DocSched { get; }

        public struct EnumCheckedIDs : IEnumerable<string> {
            DocumentZones DocZones {get; }
            public EnumCheckedIDs( DocumentZones oDocZones ) {
                DocZones = oDocZones;
            }

            public IEnumerator<string> GetEnumerator() {
                foreach( Row oRow in DocZones ) {
                    if( oRow is RowZone oRowZone ) {
                        if( oRowZone.IsChecked ) {
                            yield return oRowZone.Zone.Id;
                        }
                    }
                }
            }

            IEnumerator IEnumerable.GetEnumerator() {
                return GetEnumerator();
            }
        }
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
            DocSched = new( new DocSlot( this ) );
        }

        public void Dispose() {
            DocClock.Dispose();
            DocZones.Dispose();
            DocSched.Dispose();
        }

        public bool Initialize() {
            if( !DocClock.InitNew() ) {
                return false;
            }

            if( !DocZones.InitNew() ) {
                return false;
            }

			_oWorkPlace = ((IPgScheduler)Services).CreateWorkPlace();
            _oWorkPlace.Queue( CreateWorker(), 0 );

            return true;
        }

        public bool InitNew() {
            if( !Initialize() )
                return false;

            if( !DocSched.InitNew() ) {
                return false;
            }

            Reset();

            return true;
        }

        public bool Load(TextReader oStream) {
            return InitNew();
        }

        public bool Load(XmlNode oXmlRoot ) {
            if( !Initialize() )
                return false;

            List<string> rgZoneIDs = new ();
            string       strFile   = string.Empty;

            foreach( XmlNode oNode in oXmlRoot.SelectNodes( "Zones/Zone" ) ) {
                if( oNode is XmlElement oXmlNode ) {
                    string strID = oXmlNode.GetAttribute( "id" );
                    if( !string.IsNullOrEmpty( strID ) ) {
                        rgZoneIDs.Add( strID );
                    }
                }
            }
            if( !DocSched.Load( oXmlRoot ) ) {
                return false;
            }

            if( rgZoneIDs.Count > 0 ) {
                foreach( Row oRow in DocZones ) {
                    if( oRow is RowZone oRowZone ) {
                        foreach( string strID in rgZoneIDs ) {
                            if( string.Compare( oRowZone.Zone.Id, strID ) == 0 ) {
                                DocZones.SetCheck( oRowZone );
                            }
                        }
                    }
                }
            }

            Reset();

            return true;
        }
        public EnumCheckedIDs ZoneClxn => new EnumCheckedIDs( DocZones );

        public bool Save(XmlNode oStream) {
            try {
                XmlDocument oRoot = oStream.OwnerDocument;

                XmlElement oXmlZones = oRoot.CreateElement( "Zones" );

                foreach( string strZoneID in ZoneClxn ) {
                    XmlElement oXmlZone = oRoot.CreateElement( "Zone" );
                    oXmlZone.SetAttribute( "id", strZoneID );
                    oXmlZones.AppendChild( oXmlZone );
                }

                oStream.AppendChild( oXmlZones );
                return true;
            } catch( NullReferenceException ) {
            }
            return false;
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
                DocClock.UpdateTime();

                // Note: Changing this doesn't seem to effect anything.
                //       You need to restart the workplace.
                yield return TimoutInMillisecs;
            }
        }

        /// <summary>
        /// Clear any existing zones being shown and set up again
        /// based on the selections in DocZones. Slightly expensive
        /// only do this when adding or removing zones. (and init)
        /// </summary>
        public void Reset() {
            DateTime oDT = DateTime.Now.ToUniversalTime();

            DocClock.Clear();
            DocClock.Append( new RowClock( "UTC", 0 ) { Time = oDT });

            foreach( Row oRow in DocZones ) {
                if( oRow is RowZone oRowZone && oRowZone.IsChecked ) {
                    string strZone = oRowZone[RowZone.DCol.Zone].ToString();
                    const int iMaxTitle = 10;
                    if( strZone.Length > iMaxTitle ) {
                        strZone = strZone[0..iMaxTitle];
                    }
                    DocClock.Append( new RowClock( strZone, 
                                                   oRowZone.Offset, 
                                                   oRowZone.Zone ) { Time = oDT } );
                }
            }

            DocClock.RenumberAndSumate();
            DocClock.UpdateTime       ();
        }
    } // end class
}
