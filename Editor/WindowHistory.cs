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
        protected IPgViewSite _oSiteView;
        readonly ICollection<ILineSelection> _rgSelection = new List<ILineSelection>( 2 );

        public bool IsDirty => true;
        public IPgParent Parentage => _oSiteView.Host;

        public IPgParent Services  => Parentage.Services;


        public string Banner => throw new NotImplementedException();

        public SKBitmap Icon => null;

        public Guid Catagory => throw new NotImplementedException();

        public TextPosition Caret => throw new NotImplementedException();

        public object DocumentText => null;

        public object Decorate(IPgViewSite oBaseSite, Guid sGuid) {
            throw new NotImplementedException();
        }

        public bool Execute(Guid sGuid) {
            throw new NotImplementedException();
        }

        public bool InitNew() {
            throw new NotImplementedException();
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

        public bool SelectionSet(int iLine, int iOffset, int iLength) {
            throw new NotImplementedException();
        }
    }
}
