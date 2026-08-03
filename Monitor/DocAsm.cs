using System.Xml;

using Play.Edit;
using Play.Interfaces.Embedding;
using z80;

namespace Monitor {
    public class AsmRow : Row {
        public AsmRow( string strAssembly, string strParams ) {
            _rgColumns = new Line[ColumnCount];

            Assign( ColumnAddr   ); 
            Assign( ColumnBytes  ); 
            Assign( ColumnCode   ); 
            Assign( ColumnBrkPnt ); 
            Assign( ColumnLabel  ); 
            Assign( ColumnInstr, strAssembly ); 
            Assign( ColumnParam, strParams   );
            Assign( ColumnComment ); 

            CheckForNulls();
        }

        protected void Assign( int iIndex, string strValue = "" ) {
            _rgColumns[iIndex] = new TextLine( iIndex, strValue );
        }

        public int AddressMap { get; set; } = -1;

        public Line Addr    => _rgColumns[ColumnAddr];
        public Line Code    => _rgColumns[ColumnCode];
        public Line Break   => _rgColumns[ColumnBrkPnt];
        public Line Label   => _rgColumns[ColumnLabel];
        public Line Instr   => _rgColumns[ColumnInstr];
        public Line Param   => _rgColumns[ColumnParam];
        public Line Comment => _rgColumns[ColumnComment];
        public Line Bytes   => _rgColumns[ColumnBytes];

        public const int ColumnAddr    = 0;
        public const int ColumnBytes   = 1;
        public const int ColumnCode    = 2;
        public const int ColumnBrkPnt  = 3;
        public const int ColumnLabel   = 4;
        public const int ColumnInstr   = 5;
        public const int ColumnParam   = 6;
        public const int ColumnComment = 7;

        public static int ColumnCount => 8;
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
            //public Row Insert( int iIndex, string strInstr, string strParam ) {
            //    AsmRow oAsmRow = new AsmRow( strInstr, strParam );

            //    InsertRow( iIndex, oAsmRow );

            //    return oAsmRow;
            //}

            public Row Append( string strInstr, string? strParam = null ) {
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

        public bool FindRowAtAddress( int iAddress, out AsmRow? oFind ) {
            int Look( Row oRow ) {
                if( oRow is not AsmRow oAsm )
                    throw new InvalidDataException();

                return iAddress - oAsm.AddressMap;
            }

            try {
                if( _rgRows.Count <= 0 ) {
                    oFind = null;
                    return false;
                }

                int iResult = FindStuff<Row>.BinarySearch( _rgRows, 0, _rgRows.Count - 1, Look );
                if( iResult >= 0 ) {
                    oFind = (AsmRow)_rgRows[iResult];
                    return true;
                }
            } catch( Exception oEx ) {
                Type[] rgErrors = { typeof( InvalidCastException ),
                                    typeof( NullReferenceException ) };
                if( rgErrors.IsUnhandled( oEx ) )
                    throw;

                LogError( "Bad row type in AsmEditor2, or empty document!" );
            }

            oFind = null;
            return false;
        }

        public void UpdateHighlightLine( int iRow ) {
            FindRowAtAddress( iRow, out AsmRow? _ );
        }
        public override WorkerStatus PlayStatus {
            
			get { 
                if( Parentage is DocumentMonitor oMon ) 
                    return oMon.PlayStatus; 

                return WorkerStatus.NOTIMPLEMENTED;
            }
		}

        /// <summary>
        /// Oh, I didn't think of this. While we are executing the program.
        /// We don't get an update on the memory!! I need to physically
        /// copy it back. I'm just going to hack this for now. I should just
        /// create a memory dump screen so you can track the live memory.
        /// </summary>
        public void Mirror( Z80Memory _oMem ) {
            // Re-dissassemble.
            Raise_DocFormatted();
        }
    }
}
