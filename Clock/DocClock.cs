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

namespace Clock {
    public class DocumentClock :
        IPgParent,
        IPgSave<TextWriter>,
        IPgLoad<TextReader>,
        IDisposable
    {
        IPgBaseSite Site { get; }

        public DocumentClock( IPgBaseSite oSite ) {
            Site = oSite ?? throw new ArgumentNullException("Document site must not be null." );
        }

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

        public void Dispose() {
        }

        public bool IsDirty => false;

        public bool InitNew(){
            DocZones = new Editor( new DocSlot( this ) );
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
        SKControl,
        IPgLoad<XmlElement>,
        IPgSave<XmlDocumentFragment>,
        IPgCommandView,
        IBufferEvents
    {
        readonly LayoutStackVertical _rgLayout = new LayoutStackVertical( 5 );

        public Guid   Catagory => Guid.Empty; // Default view.
        public string Banner   => "World Clock";
        public Image  Iconic   => null;
        public bool   IsDirty  => false;

        public bool InitNew() {
            _rgLayout.Add(new LayoutText2  ( null, LayoutRect.CSS.Percent, 40 ) ); // time
            _rgLayout.Add(new LayoutControl( null, LayoutRect.CSS.Percent, 60 ) ); // zones.

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
