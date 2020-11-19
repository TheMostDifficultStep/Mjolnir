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

        public Editor DocZones { get; protected set; }

        public DocumentClock( IPgBaseSite oSite ) {
            Site = oSite ?? throw new ArgumentNullException("Document site must not be null." );
        }

        public void Dispose() {
        }

        public bool IsDirty => false;

        public bool InitNew(){
            DocZones = new Editor( new DocSlot( this ) );

            DocZones.LineAppend( "8:00" );
            DocZones.LineAppend( "utc" );

            return true;
        }

        public bool Load(TextReader oStream) {
            return true;
        }

        public bool Save(TextWriter oStream) {
            return true;
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

        protected readonly IPgViewSite _oViewSite;

        public IPgParent Parentage => _oViewSite.Host;
        public IPgParent Services  => Parentage.Services;

        protected DocumentClock Document { get; }

        public ViewClock( IPgViewSite oViewSite, DocumentClock oDocClock ) : base( oViewSite, oDocClock.DocZones ) {
            Document   = oDocClock ?? throw new ArgumentNullException( "Clock document must not be null." );
            _oViewSite = oViewSite;
        }

        public override bool InitNew() {
            if( !base.InitNew() ) 
                return false;

            SmartTable oLayout = new SmartTable( 5, LayoutRect.CSS.None );
            Layout2 = oLayout;

            oLayout.Add( new LayoutRect( LayoutRect.CSS.Percent, 60, 0 ) ); // time
            oLayout.Add( new LayoutRect( LayoutRect.CSS.Percent, 40, 0 ) ); // zones.

            LayoutSingleLine oLayoutTime = new LayoutSingleLine( new FTCacheWrap( DocForms[0] ), LayoutRect.CSS.Flex);
            LayoutSingleLine oLayoutZone = new LayoutSingleLine( new FTCacheWrap( DocForms[1] ), LayoutRect.CSS.Flex);

            oLayout.AddRow( new List<LayoutRect>() { oLayoutTime, oLayoutZone } );

            CacheList.Add( oLayoutTime );
            CacheList.Add( oLayoutZone );

            Caret.Cache = oLayoutTime;

            Document_BufferEvent( BUFFEREVENTS.MULTILINE );
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
            throw new NotImplementedException();
        }
    }
}
