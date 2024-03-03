using Play.Edit;
using Play.Interfaces.Embedding;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using z80;

namespace Monitor {

    public class AsmRow : Row {
        public AsmRow( string strAssembly, string strParams ) {
            _rgColumns    = new Line[4];
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

    public class AsmEditor2 : 
        EditMultiColumn
    {
        public    MonitorProperties _docProperties;
        protected Z80Memory?        _rgMemory;
        protected Z80Definitions    _rgZ80Definitions;

        public enum AsmColumns {
            labels,
            assembly,
            comments
        }

		public class DocSlot : 
			IPgBaseSite
		{
			readonly AsmEditor2 _oDoc;

			public DocSlot( AsmEditor2 oDoc ) {
				_oDoc = oDoc ?? throw new ArgumentNullException();
			}

			public void LogError( string strMessage, string strDetails, bool fShow=true ) {
				_oDoc.LogError( strDetails );
			}

			public void Notify( ShellNotify eEvent ) {
			}

			public IPgParent Host => _oDoc;
		}

        public AsmEditor2( IPgBaseSite oSiteBase ) : base( oSiteBase ) {
            _docProperties    = new MonitorProperties(new DocSlot(this));
            _rgZ80Definitions = new Z80Definitions();
        }

        /// <summary>
        /// Move this to the EditMultiColumn later...
        /// </summary>
        public class Hacker : IDisposable {
            AsmEditor2 _oDoc;

            public Hacker( AsmEditor2 oDoc ) {
                _oDoc = oDoc;
            }

            public int RowIndex { get; set; } = 0;

            public void Dispose() {
                for( int i=0; i<_oDoc.ElementCount; ++i ) {
                    _oDoc[i].At = i;
                }
                //_oDoc.Raise_EveryRowEvent( DOCUMENTEVENTS.MODIFIED );
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

        public void Dissassemble( Editor.Manipulator oBulkOutl ) {
            if( _rgMemory == null ) {
                LogError( "Load a binary first." );
                return;
            }

            try {
                using Hacker oBulkAsm = new Hacker( this );

                using Z80Dissambler oDeCompile = 
                        new Z80Dissambler( _rgZ80Definitions, 
                                           _rgMemory,
                                           oBulkAsm,
                                           oBulkOutl,
                                           this );

                oDeCompile.Dissassemble();
                RenumberAndSumate();
            } catch( Exception oEx ) {
                Type[] rgErrors = { typeof( NullReferenceException ),
                                    typeof( ArgumentNullException ) };
                if( rgErrors.IsUnhandled( oEx ) )
                    throw;

                LogError( "Null Ref Exception in Dissassembler." );
            }
        }

        public bool InitNew() {
            if( !_docProperties.InitNew() )
                return false;
            
            //Raise_EveryRowEvent( DOCUMENTEVENTS.LOADED );

            return true;
        }

        /// <summary>
        /// This is an unusual document in that it is simply an XML
        /// file with pointers to the actual files we are interested in:
        /// 1) The binary which we will dissassemble.
        /// 2) The source comments we are adding.
        /// 3) Markers for the code/data portions of the binary file.
        /// </summary>
        /// <param name="oStream"></param>
        /// <returns></returns>
        public bool Load(Stream oStream, BaseEditor.Manipulator oBulkOutline ) {
            if( oStream == null )
                throw new ArgumentNullException();

            if( !_docProperties.InitNew() )
                return false;

            // This isn't necessarily the z80 emulator memory. Let's
            // see how this turns out. We can get the memory size needed by
            // reading the first "ld sp, nn" instruction. But fake it for now.
            byte[] rgRWRam = new byte[4096];
            int    iCount  = 0x100;

            for( int iByte = oStream.ReadByte();
                 iByte != -1;
                 iByte = oStream.ReadByte() ) 
            {
                rgRWRam[iCount++] = (byte)iByte;
            }

            _rgMemory = new Z80Memory( rgRWRam, (ushort)iCount );

            //Raise_EveryRowEvent( DOCUMENTEVENTS.LOADED );

            Dissassemble( oBulkOutline );

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
        public bool Load( XmlNodeList rgxCodeData ) {
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
