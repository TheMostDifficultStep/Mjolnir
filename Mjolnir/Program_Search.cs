using System;
using System.Collections.Generic;

using Play.Edit;
using Play.FileManager;
using Play.Interfaces.Embedding;
using Play.Rectangles;
using Play.Parse;

namespace Mjolnir {
    public class TransientSlot : IPgBaseSite {
        Program _oHost;

        public TransientSlot( Program oHost ) { 
            _oHost = oHost;
        }
        public IPgParent Host => _oHost;

        public void LogError(string strMessage, string strDetails, bool fShow = true) {
            _oHost.LogError( this, strMessage, strDetails, fShow );
        }

        public void Notify(ShellNotify eEvent) {
        }
    }
    public class SearchResults : 
        EditMultiColumn,
        IPgLoad<IEnumerable<ILineRange>>,
        IPgSave
    {
        public class ResultRow : Row {
            public enum DCol {
                Row_Num = 0,
                Result
            }

            public ILineRange Range  { get; } // Pos in source.
            public int        SrcCol { get; }

            static int ColumnCount = Enum.GetValues(typeof(DCol)).Length;
            void CreateColumn( DCol eCol, string strValue ) {
				_rgColumns[(int)eCol] = new TextLine( (int)eCol, strValue );
            }

            public Line this[DCol eIndex] => _rgColumns[(int)eIndex];

            public ResultRow( int iRow, string strResult, ILineRange oRange, int iSrcCol ) {
                _rgColumns = new Line[ColumnCount];

                Range  = oRange;
                SrcCol = iSrcCol;

                CreateColumn( DCol.Row_Num, iRow.ToString() );
                CreateColumn( DCol.Result,  strResult );

                CheckForNulls();
            }
        }

        public SearchResults(IPgBaseSite oSiteBase) : base(oSiteBase) {
            IsDirty    = false;
            IsReadOnly = true;
        }

        public bool InitNew() {
            return true;
        }

        public bool Load(IEnumerable<ILineRange> rgResults ) {
            Clear();

            try {
                Program oProgram = (Program)_oSiteBase.Host;

                foreach( ILineRange oRange in rgResults) {
                    int    iStart     = oRange.Offset > 10 ? oRange.Offset - 10 : 0;
                    int    iDiff      = oRange.Offset - iStart;
                    string strMessage = oRange.Line.SubString( iStart, 50 );
                        
                    ResultRow    oResult  = new( oRange.At, strMessage, oRange, 0 );
                    FileRange oHotLink = new FileRange( iDiff, oRange.Length, oProgram.GetColorIndex( "red" ) );

                    oResult[ResultRow.DCol.Result].Formatting.Add( oHotLink );

                    _rgRows.Add( oResult );
                }
            } catch( Exception oEx ) {
                Type[] rgErrors = { typeof( NullReferenceException ),
                                    typeof( IndexOutOfRangeException ),
                                    typeof( ArgumentOutOfRangeException ),
                                    typeof( InvalidCastException ) };
                if( rgErrors.IsUnhandled( oEx ) )
                    throw;

                LogError( "Unable to load Search results." );
            }

            RenumberAndSumate();
            Raise_DocLoaded  ();

            return true;
        }
    }

    public class ViewSearchResults : WindowMultiColumn {
        protected readonly SearchResults _oDocument;

        public ViewSearchResults(IPgViewSite oViewSite, object oDocument) : 
            base(oViewSite, oDocument) 
        {
            _oDocument = (SearchResults)oDocument;
        }

        public override bool InitNew() {
            if( !base.InitNew() )
                return false;

            List<ColumnInfo> rgCols = new List<ColumnInfo> {
                new ColumnInfo( new LayoutRect( LayoutRect.CSS.Pixels, 50, 1L ){Justify = Align.Right }, (int)SearchResults.ResultRow.DCol.Row_Num ),       
                new ColumnInfo( new LayoutRect( LayoutRect.CSS.None,   80, 1L ), (int)SearchResults.ResultRow.DCol.Result ),
            };

            InitColumns( rgCols );

            HyperLinks.Add( "FileJump", OnFileJump );

            return true;
        }

        public void OnFileJump( Row oRow, int iColumn, IPgWordRange oRange ) {
            _oCacheMan.SetCaretPositionAndScroll( oRow.At, iColumn, oRange.Offset, oRange.Length );
        }
    }
}
