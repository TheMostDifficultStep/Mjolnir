using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;

using SkiaSharp;
using SkiaSharp.Views.Desktop;

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
                DateTime oDT = DateTime.Now.ToUniversalTime();

                FormattedTimeString( DateTime.Now.ToUniversalTime(), DocZones[0], DocZones[1] );
                FormattedTimeString( DateTime.Now,                   DocZones[3], DocZones[4] );

                ClockEvent( BUFFEREVENTS.MULTILINE );

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
        public Guid   Catagory => Guid.Empty; // Default view.
        public string Banner   => "World Clock";
        public Image  Iconic   => null;
        public bool   IsDirty  => false;

        protected readonly IPgViewSite       _oViewSite;

        public IPgParent Parentage => _oViewSite.Host;
        public IPgParent Services  => Parentage.Services;

        protected DocumentClock Document { get; }

        public ViewClock( IPgViewSite oViewSite, DocumentClock oDocClock ) : base( oViewSite, oDocClock.DocZones ) {
            Document   = oDocClock ?? throw new ArgumentNullException( "Clock document must not be null." );
            _oViewSite = oViewSite;
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

            oLayout.Add( new LayoutRect( LayoutRect.CSS.Percent, 25, 0 ) ); // time
            oLayout.Add( new LayoutRect( LayoutRect.CSS.Percent, 50, 0 ) ); // date
            oLayout.Add( new LayoutRect( LayoutRect.CSS.Percent, 25, 0 ) ); // zones.
            
            var oLayoutTimeUtc = new LayoutSingleLine( new FTCacheWrap( Document.DocZones[0] ), LayoutRect.CSS.Flex );
            var oLayoutDateUtc = new LayoutSingleLine( new FTCacheWrap( Document.DocZones[1] ), LayoutRect.CSS.Flex );
            var oLayoutZoneUtc = new LayoutSingleLine( new FTCacheWrap( Document.DocZones[2] ), LayoutRect.CSS.Flex );
            var oLayoutTimePst = new LayoutSingleLine( new FTCacheWrap( Document.DocZones[3] ), LayoutRect.CSS.Flex );
            var oLayoutDatePst = new LayoutSingleLine( new FTCacheWrap( Document.DocZones[4] ), LayoutRect.CSS.Flex );
            var oLayoutZonePst = new LayoutSingleLine( new FTCacheWrap( Document.DocZones[5] ), LayoutRect.CSS.Flex );

            oLayout.AddRow( new List<LayoutRect>() { oLayoutTimeUtc, oLayoutDateUtc, oLayoutZoneUtc } );
            oLayout.AddRow( new List<LayoutRect>() { oLayoutTimePst, oLayoutDatePst, oLayoutZonePst } );

            CacheList.Add( oLayoutTimeUtc );
            CacheList.Add( oLayoutDateUtc );
            CacheList.Add( oLayoutZoneUtc );
            CacheList.Add( oLayoutTimePst );
            CacheList.Add( oLayoutDatePst );
            CacheList.Add( oLayoutZonePst );

            Caret.Cache = oLayoutTimeUtc;

            OnDocument_BufferEvent( BUFFEREVENTS.MULTILINE );
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
            OnDocument_BufferEvent( BUFFEREVENTS.MULTILINE );
            Invalidate();
        }
    }
}
