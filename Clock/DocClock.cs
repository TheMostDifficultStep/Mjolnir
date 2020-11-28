using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using System.Reflection;

using Play.Interfaces.Embedding;
using Play.Rectangles;
using System.Drawing;
using Play.Edit;
using Play.Forms;

namespace Play.Clock {
    public class DocumentClock :
        IPgParent,
        IPgSave<TextWriter>,
        IPgLoad<TextReader>,
        IDisposable
    {
        IPgBaseSite Site { get; }

		public class DocSlot : 
			IPgBaseSite
		{
			protected readonly DocumentClock _oDoc;

			public DocSlot(DocumentClock oDoc ) {
				_oDoc = oDoc ?? throw new ArgumentNullException( "Document must not be null." );
			}

			public void LogError( string strMessage, string strDetails, bool fShow=true ) {
				_oDoc.Site.LogError( strMessage, "PropDocSlot : " + strDetails );
			}

			public void Notify( ShellNotify eEvent ) {
			}

			public IPgParent Host => _oDoc;
		}

        public IPgParent Parentage => Site.Host;
        public IPgParent Services  => Parentage;
        public bool      IsDirty   => false;

        protected IPgRoundRobinWork _oWorkPlace;

        public event BufferEvent  ClockEvent;
        public       Editor       DocZones { get; }

        public DocumentClock( IPgBaseSite oSite ) {
            Site = oSite ?? throw new ArgumentNullException("Document site must not be null." );

            DocZones = new FormsEditor( new DocSlot( this ) );
        }

        public void Dispose() {
        }

        public bool InitNew(){
            DocZones.LineAppend( string.Empty, fUndoable:false );
            DocZones.LineAppend( string.Empty, fUndoable:false );
            DocZones.LineAppend( "utc",        fUndoable:false );
            DocZones.LineAppend( string.Empty, fUndoable:false );
            DocZones.LineAppend( string.Empty, fUndoable:false );
            DocZones.LineAppend( "local",      fUndoable:false );
            DocZones.LineAppend( string.Empty, fUndoable:false );
            DocZones.LineAppend( "12hr clock", fUndoable:false );
            DocZones.LineAppend( "local",      fUndoable:false );

			try {
				_oWorkPlace = ((IPgScheduler)Services).CreateWorkPlace();
                _oWorkPlace.Queue( CreateWorker(), 1000 );
			} catch( InvalidCastException ) {
				Site.LogError( "Clock Doc", "Can't set up time update" );
			}

            return true;
        }

        public bool Load(TextReader oStream) {
            return true;
        }

        public bool Save(TextWriter oStream) {
            return true;
        }

        protected static void FormattedTimeString( DateTime oDT, Line oTime, Line oDate ) {
            string strTime = oDT.Hour.ToString( "D2" ) + ":" + oDT.Minute.ToString( "D2");

            oTime.Empty();
            oTime.TryAppend( strTime );

            oDate.Empty();
            oDate.TryAppend( oDT.ToShortDateString() );
        }

        public IEnumerator<int> CreateWorker() {
            while( true ) {
                DateTime oDT = DateTime.Now;

                FormattedTimeString( oDT.ToUniversalTime(), DocZones[0], DocZones[1] );
                FormattedTimeString( oDT,                   DocZones[3], DocZones[4] );

                int    iHour     = oDT.Hour;
                string strMidDay = "am"; 

                if( iHour > 12 ) {
                    strMidDay = "pm";
                    iHour    -= 12;
                }

                DocZones[6].Empty();
                DocZones[6].TryAppend( iHour.ToString( "D2" ) + ":" + oDT.Minute.ToString( "D2" ) + strMidDay );

                ClockEvent?.Invoke( BUFFEREVENTS.MULTILINE );

                yield return 10000;
            }
        }
    }

    public class ViewClock :
        FormsWindow,
        IPgLoad<XmlElement>,
        IPgSave<XmlDocumentFragment>,
        IPgParent,
        IPgCommandView,
        IBufferEvents
    {
        private   readonly string      _strViewIcon  = "Play.Clock.Content.icon_clock.gif";
        protected readonly IPgViewSite _oViewSite;

        public Guid      Catagory  => Guid.Empty; // Default view.
        public string    Banner    => "World Clock";
        public Image     Iconic    { get; }
        public bool      IsDirty   => false;
        public IPgParent Parentage => _oViewSite.Host;
        public IPgParent Services  => Parentage.Services;

        protected DocumentClock Document { get; }

        public ViewClock( IPgViewSite oViewSite, DocumentClock oDocClock ) : base( oViewSite, oDocClock.DocZones ) {
            Document   = oDocClock ?? throw new ArgumentNullException( "Clock document must not be null." );
            _oViewSite = oViewSite;

			Iconic = ImageResourceHelper.GetImageResource( Assembly.GetExecutingAssembly(), _strViewIcon );
        }

        protected override void Dispose( bool disposing ) {
            if( disposing ) {
                Document.ClockEvent -= OnEvent;
                DocForms.CaretRemove( Caret );
            }
            base.Dispose(disposing);
        }

        public override bool InitNew() {
            if( !base.InitNew() ) 
                return false;

            Document.ClockEvent += OnEvent;

            SmartTable oLayout = new SmartTable( 5, LayoutRect.CSS.None );
            Layout2 = oLayout;

            oLayout.Add( new LayoutRect( LayoutRect.CSS.Percent, 30, 0 ) ); // time
            oLayout.Add( new LayoutRect( LayoutRect.CSS.Percent, 45, 0 ) ); // date
            oLayout.Add( new LayoutRect( LayoutRect.CSS.Percent, 25, 0 ) ); // zones.
            
            var oLayoutTimeUtc = new LayoutSingleLine( new FTCacheWrap( Document.DocZones[0] ), LayoutRect.CSS.Flex );
            var oLayoutDateUtc = new LayoutSingleLine( new FTCacheWrap( Document.DocZones[1] ), LayoutRect.CSS.Flex );
            var oLayoutZoneUtc = new LayoutSingleLine( new FTCacheWrap( Document.DocZones[2] ), LayoutRect.CSS.Flex );
            var oLayoutTimePst = new LayoutSingleLine( new FTCacheWrap( Document.DocZones[3] ), LayoutRect.CSS.Flex );
            var oLayoutDatePst = new LayoutSingleLine( new FTCacheWrap( Document.DocZones[4] ), LayoutRect.CSS.Flex );
            var oLayoutZonePst = new LayoutSingleLine( new FTCacheWrap( Document.DocZones[5] ), LayoutRect.CSS.Flex );
            var oLayoutTime12h = new LayoutSingleLine( new FTCacheWrap( Document.DocZones[6] ), LayoutRect.CSS.Flex );
            var oLayoutDate12h = new LayoutSingleLine( new FTCacheWrap( Document.DocZones[7] ), LayoutRect.CSS.Flex );
            var oLayoutZone12h = new LayoutSingleLine( new FTCacheWrap( Document.DocZones[8] ), LayoutRect.CSS.Flex );

            oLayout.AddRow( new List<LayoutRect>() { oLayoutTimeUtc, oLayoutDateUtc, oLayoutZoneUtc } );
            oLayout.AddRow( new List<LayoutRect>() { oLayoutTimePst, oLayoutDatePst, oLayoutZonePst } );
            oLayout.AddRow( new List<LayoutRect>() { oLayoutTime12h, oLayoutDate12h, oLayoutZone12h } );

            CacheList.Add( oLayoutTimeUtc );
            CacheList.Add( oLayoutDateUtc );
            CacheList.Add( oLayoutZoneUtc );
            CacheList.Add( oLayoutTimePst );
            CacheList.Add( oLayoutDatePst );
            CacheList.Add( oLayoutZonePst );
            CacheList.Add( oLayoutTime12h );
            CacheList.Add( oLayoutDate12h );
            CacheList.Add( oLayoutZone12h );

            Caret.Cache = oLayoutTimeUtc;

            OnDocumentEvent( BUFFEREVENTS.MULTILINE );
            OnSizeChanged( new EventArgs() );

            return true;
        }

        public bool Load(XmlElement oStream) {
            return true;
        }

        public bool Save(XmlDocumentFragment oStream) {
            return true;
        }

        public object Decorate(IPgViewSite oBaseSite, Guid sGuid) {
            return null;
        }

        public bool Execute(Guid sGuid) {
            return false;
        }

        public void OnEvent(BUFFEREVENTS eEvent) {
            OnDocumentEvent( BUFFEREVENTS.MULTILINE );
            Invalidate();
        }
    }
}
