using System.Xml;

using Play.Edit;
using Play.Interfaces.Embedding;

namespace Monitor {
    public class AsmRow : Row {
        public AsmRow( string strAssembly, string strParams ) {
            _rgColumns    = new Line[4];
            // Add a column for break points! O.o
            _rgColumns[0] = new TextLine( 0, string.Empty ); // label
            _rgColumns[1] = new TextLine( 1, strAssembly );  // instr
            _rgColumns[2] = new TextLine( 2, strParams );    // params
            _rgColumns[3] = new TextLine( 3, string.Empty ); // comments
        }

        public int AddressMap { get; set; } = -1;

        public Line Label => _rgColumns[0];
        public Line Instr => _rgColumns[1];
        public Line Param => _rgColumns[2];
        public Line Comment => _rgColumns[3];
    }

    public class AsmEditor : 
        EditMultiColumn
    {
        public enum AsmColumns {
            labels,
            assembly,
            comments
        }

		public class DocSlot : 
			IPgBaseSite
		{
			readonly AsmEditor _oDoc;

			public DocSlot( AsmEditor oDoc ) {
				_oDoc = oDoc ?? throw new ArgumentNullException();
			}

			public void LogError( string strMessage, string strDetails, bool fShow=true ) {
				_oDoc.LogError( strDetails );
			}

			public void Notify( ShellNotify eEvent ) {
			}

			public IPgParent Host => _oDoc;
		}

        public AsmEditor( IPgBaseSite oSiteBase ) : base( oSiteBase ) {
        }

        /// <summary>
        /// Move this to the EditMultiColumn later...
        /// </summary>
        public class Mangler : IDisposable {
            AsmEditor _oDoc;

            public Mangler( AsmEditor oDoc ) {
                _oDoc = oDoc;
            }

            public int RowIndex { get; set; } = 0;

            public void Dispose() {
                for( int i=0; i<_oDoc.ElementCount; ++i ) {
                    _oDoc[i].At = i;
                }
                //_oDoc.Raise_EveryRowEvent( DOCUMENTEVENTS.MODIFIED );
                _oDoc.RenumberAndSumate();
            }

            public void InsertRow( int iIndex, Row oDocRow ) {
                if( RowIndex < 0 )
                    throw new IndexOutOfRangeException( "Location must not be negative" );
                if( RowIndex > _oDoc.ElementCount )
                    throw new IndexOutOfRangeException( "Location must not be greater element count" );

                _oDoc._rgRows.Insert( iIndex, oDocRow );
            }

            public void Delete() {
                _oDoc._rgRows.RemoveAt( RowIndex );
            }

            /// <summary>
            /// Put this in a subclass later.
            /// </summary>
            public Row Insert( int iIndex, string strInstr, string strParam ) {
                AsmRow oAsmRow = new AsmRow( strInstr, strParam );

                InsertRow( iIndex, oAsmRow );

                return oAsmRow;
            }

            public Row Append( string strInstr, string strParam = null ) {
                AsmRow oAsmRow = new AsmRow( strInstr, strParam ?? string.Empty );

                InsertRow( _oDoc._rgRows.Count, oAsmRow );

                return oAsmRow;
            }
        }

        public bool InitNew() {
            //Raise_EveryRowEvent( DOCUMENTEVENTS.LOADED );

            return true;
        }

        public bool Save(TextWriter oStream) {
            return false; // This is a read only kind of deal...
        }

        public bool FindRowAtAddress( int iAddress, out AsmRow oFind ) {
            int Look( Row oRow ) {
                if( oRow is not AsmRow oAsm )
                    throw new InvalidDataException();

                return iAddress - oAsm.AddressMap;
            }

            try {
                int iResult = FindStuff<Row>.BinarySearch( _rgRows, 0, _rgRows.Count - 1, Look );
                if( iResult >= 0 ) {
                    oFind = (AsmRow)_rgRows[iResult];
                    return true;
                }
            } catch( InvalidDataException ) {
                LogError( "Bad row type in AsmEditor2" );
            }

            oFind = null;
            return false;
        }
    }

    public class SegmentRow : Row {
        public SegmentRow() {
            _rgColumns = new Line[2];
            _rgColumns[0] = new TextLine( 0, string.Empty ); // Start
            _rgColumns[1] = new TextLine( 1, string.Empty ); // Stop
        }

        public SegmentRow( string strStart, string strEnd ) {
            _rgColumns = new Line[2];
            _rgColumns[0] = new TextLine( 0, strStart ); 
            _rgColumns[1] = new TextLine( 1, strEnd );
        }

        public int AddressStart { get; set; } = -1;
        public int AddressStop  { get; set; } = -1;
    }

    public class DataSegmDoc :
        EditMultiColumn
    {   
        public DataSegmDoc( IPgBaseSite oSiteBase ) : base( oSiteBase )
        {
        }

        public void AppendRow() {
            SegmentRow oRow = new();

            _rgRows.Add( oRow );
        }

        /// <summary>
        /// Let's try creating an empty doc. Probably will blow up things..
        /// </summary>
        public bool InitNew() {
            return true;
        }

        /// <summary>
        /// Load ourselves from a fragment of XML data. We expect the root
        /// doc to be opened somewhere else..
        /// </summary>
        /// <returns></returns>
        public bool LoadSegments( XmlNodeList rgxCodeData ) {
            try {
                foreach( XmlNode xmlNode in rgxCodeData ) {
                    if( xmlNode is XmlElement xmlElem ) {
                        string strAddr = xmlElem.GetAttribute( "Start" );
                        string strType = xmlElem.GetAttribute( "End" ); // Code/Data

                        SegmentRow oRow = new SegmentRow( strAddr, strType );
                        _rgRows.Add( oRow );
                    }
                }
            } catch( Exception oEx ) {
                Type[] rgErrors = { typeof( IOException ),
                                    typeof( OutOfMemoryException ),
                                    typeof( ObjectDisposedException ),
                                    typeof( ArgumentOutOfRangeException ),
                                    typeof( XmlException ) };
                if( rgErrors.IsUnhandled( oEx ) )
                    throw;

                return false;
            }

            //DoParse();

            return true;
        }
    }
}
