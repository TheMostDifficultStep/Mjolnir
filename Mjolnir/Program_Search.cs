using System;
using System.Collections.Generic;

using Play.Edit;
using Play.FileManager;
using Play.Interfaces.Embedding;
using Play.Rectangles;
using Play.Parse;
using static Mjolnir.SearchResults;
using static Play.FileManager.FileFavorites;

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

            public ILineRange Source  { get; } // Pos in source.
            public int        SrcCol { get; }

            static int ColumnCount = Enum.GetValues(typeof(DCol)).Length;
            void CreateColumn( DCol eCol, string strValue ) {
				_rgColumns[(int)eCol] = new TextLine( (int)eCol, strValue );
            }

            public Line this[DCol eIndex] => _rgColumns[(int)eIndex];

            public ResultRow( int iRow, string strResult, ILineRange oRange, int iSrcCol ) {
                _rgColumns = new Line[ColumnCount];

                Source = oRange;
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
                        
                    ResultRow oResult  = new( oRange.At, strMessage, oRange, 0 );
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
        protected readonly MainWin       _oMainWin;

        public ViewSearchResults(IPgViewSite oViewSite, object oDocument) : 
            base(oViewSite, oDocument) 
        {
            _oDocument = (SearchResults)oDocument;
            _oMainWin  = (MainWin)_oSiteView.Host;
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

        /// <summary>
        /// The is row entered into the results list is a "ResultRow". Cast it to
        /// that type in order to retrieve the found text span as a result of the search.
        /// In the old days that would be the "extra" value on the Line object.
        /// </summary>
        /// <param name="oRow">The row in the results doc with the hyperlink.</param>
        /// <param name="iColumn">The column of the hyperlink.</param>
        /// <param name="oRange">The range of the hyperlink.</param>
        public void OnFileJump( Row oRow, int iColumn, IPgWordRange oRange ) {
            if( _oMainWin.ViewSiteSelected is Mjolnir.ViewSlot oSlot ) {
                if( oSlot.Guest is IPgTextView oTextView ) {
                    if( oRow is ResultRow oResult ) {
                        // TODO: We can get the column (SrcCol) from the result so in the
                        //       future when SelectionSet() takes a column. We can pass it along...
                        oTextView.SelectionSet( oResult.Source.At, oResult.Source.Offset, oResult.Source.Length );
                    }
                }
            }
        }
    }
}
