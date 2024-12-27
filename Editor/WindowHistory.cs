using System;
using System.Collections.Generic;
using System.Xml;

using SkiaSharp;
using SkiaSharp.Views.Desktop;

using Play.Interfaces.Embedding;

namespace Play.Edit {
    public class WindowHistory :
        SKControl,
        IPgLoad<XmlElement>,
        IPgSave<XmlDocumentFragment>,
        IPgCommandView, // TODO: Consider moving this behavior to the EditPresentation shell window.
        IPgTextView 
    {
        protected readonly IPgViewSite                 _oSiteView;
        protected readonly ICollection<ILineSelection> _rgSelection = new List<ILineSelection>( 2 );
        protected readonly string                      _strBanner;
        static readonly Guid _gCat = new Guid( "F254B7BB-2E10-4C91-AA82-51CFB2C30FD8" );
        public bool      IsDirty   => true;
        public IPgParent Parentage => _oSiteView.Host;
        public IPgParent Services  => Parentage.Services;

        public WindowHistory( IPgViewSite oSiteView, string strBanner ) {
            _oSiteView = oSiteView ?? throw new ArgumentNullException();
            _strBanner = strBanner;
        }

        public string Banner => _strBanner;

        public SKBitmap Icon => null;

        public Guid Catagory => _gCat;

        public TextPosition Caret => throw new NotImplementedException();

        public object Decorate(IPgViewSite oBaseSite, Guid sGuid) {
            return null;
        }

        public bool Execute(Guid sGuid) {
            return false;
        }

        public bool InitNew() {
            return true;
        }

        public bool Load(XmlElement oStream) {
            return true;
        }

        public bool Save(XmlDocumentFragment oStream) {
            return true;
        }

        public void ScrollTo(SCROLLPOS eEdge) {
            throw new NotImplementedException();
        }

        public void ScrollToCaret() {
            throw new NotImplementedException();
        }

        public void SelectionClear() {
            throw new NotImplementedException();
        }

        public bool SelectionSet(int iLine, int iColumn, int iOffset, int iLength) {
            throw new NotImplementedException();
        }
    }
}
