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
        IEnumerable<ILineRange>,
        ICaretLocation, // The cache manager needs this.
        ILineRange      // The document uses this to update us.
    {
        public WindowMultiColumn( IPgViewSite oViewSite, object oDocument ) {
            _oDocEnum = (IEnumerable<Row> )oDocument;
            _oDocList = (IReadableBag<Row>)oDocument;

            _oSiteView = oViewSite;
        }

        readonly protected IPgViewSite _oSiteView;

        readonly protected IEnumerable<Row>  _oDocEnum;
        readonly protected IReadableBag<Row> _oDocList;

        protected float _flAdvance;
        protected int   _iOffset;
        protected Line  _oLine;
        protected Row   _oRow;

        public static Guid _sGuid = new Guid( "{03F21BC8-F911-4FE4-931D-9EB9F7A15A10}" );
        public bool  IsDirty => true;
        public string Banner => "Multi Column";
        public SKBitmap Icon => null;

        public Guid  Catagory    => _sGuid;

        public Row   CurrentRow  => _oRow;
        public Line  CurrentLine => _oLine;
        public int   CharOffset  => _iOffset;
        public float Advance     => _flAdvance;

        public Line Line { 
            get => _oLine; 
            set => throw new NotImplementedException(); // dummy line cleanup might call this..
        }

        public int At => _oRow.At;

        public int ColorIndex => 0;

        public int Length { get => 1; set { } }
        public int Offset { 
            get => _iOffset; 
            // Unlike your average formatting element, we are actually in the
            // position of being able to check the validity of this assignment. :-/
            set { _iOffset = value; } 
        }

        public bool SetCaretPosition(int iRow, int iColumn, int iOffset) {
            try {
                _oRow    = _oDocList[iRow];
                _oLine   = _oRow[iColumn];
                _iOffset = iOffset;
            } catch( Exception oEx ) {
                Type[] rgErrors = { typeof( ArgumentOutOfRangeException ),
                                    typeof( NullReferenceException ) };
                if( rgErrors.IsUnhandled( oEx ) )
                    throw;

                return false;
            }
            return true;
        }

        public bool SetCaretPosition(int iColumn, float fAdvance) {
            try {
                _flAdvance = fAdvance;
                _oLine     = CurrentRow[iColumn];
            } catch( Exception oEx ) {
                Type[] rgErrors = { typeof( ArgumentOutOfRangeException ),
                                    typeof( NullReferenceException ) };
                if( rgErrors.IsUnhandled( oEx ) )
                    throw;

                return false;
            }

            return true;
        }
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
