using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Forms;
using System.Xml;

using SkiaSharp;

using Play.Interfaces.Embedding;
using System.Collections;

namespace Play.Edit {
    public class WindowMultiColumn :
        Control, 
        IPgLoad<XmlElement>,
        IPgSave<XmlDocumentFragment>,
        IPgCommandView,
        IPgRowEvents,
        IEnumerable<ILineRange>
    {
        public WindowMultiColumn( IPgViewSite oViewSite, object oDocument ) {
            _oDocEnum = (IEnumerable<Row> )oDocument;
            _oDocList = (IReadableBag<Row>)oDocument;

            _oSiteView = oViewSite;
        }

        readonly protected IPgViewSite _oSiteView;

        readonly protected IEnumerable<Row>  _oDocEnum;
        readonly protected IReadableBag<Row> _oDocList;

        public static Guid _sGuid = new Guid( "{03F21BC8-F911-4FE4-931D-9EB9F7A15A10}" );
        public bool IsDirty => true;

        public string Banner => "Multi Column";

        public SKBitmap Icon => null;

        public Guid Catagory => _sGuid;

        public object Decorate(IPgViewSite oBaseSite, Guid sGuid) {
            return null;
        }

        public bool Execute(Guid sGuid) {
            return false;
        }

        public bool InitNew() {
            return false;
        }

        public bool Load(XmlElement oStream) {
            return true;
        }

        public bool Save(XmlDocumentFragment oStream) {
            return true;
        }

        public void OnRowEvent(BUFFEREVENTS eEvent, IEnumerable<Row> oEnum) {
        }

        public void OnRowEvent(BUFFEREVENTS eEvent, Row oRow) {
        }

        public void OnRowEvent(BUFFEREVENTS eEvent) {
        }

        public class SimpleRange :
            ILineRange {
            public Line Line { get ; set ; }
            public int  At { get; set; }
            public int  ColorIndex => 0;
            public int  Offset { get ; set ; } = 0;
            public int  Length { get ; set ; } = 0;
        }

        public IEnumerator<ILineRange> GetEnumerator() {
            SimpleRange oRange = new SimpleRange();

            foreach( Row oRow in _oDocEnum ) {
                foreach( Line oLine in oRow ) {
                    oRange.Line   = oLine;
                    oRange.Offset = 0;
                    oRange.Length = oLine.ElementCount;
                    oRange.At     = oLine.At;

                    yield return oRange;
                }
            }
        }

        IEnumerator IEnumerable.GetEnumerator() {
            return GetEnumerator();
        }
    }
}
