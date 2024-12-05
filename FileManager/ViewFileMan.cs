using SkiaSharp;

using Play.Edit;
using Play.Interfaces.Embedding;
using Play.Rectangles;
using Play.Parse;
using Play.Forms;

using DClmn = Play.FileManager.FileManager.FMRow.DCol;
using LCss  = Play.Rectangles.LayoutRect.CSS;

namespace Play.FileManager {
    public class FileManController : 
        Controller 
    {
        public FileManController() {
            _rgExtensions.Add( ".fileman" );
        }

        public override PgDocDescr Suitability(string strExtension) {
            if( strExtension.CompareTo( ".fileman" ) == 0 ) {
                return new PgDocDescr( ".fileman", 
                                       typeof( IPgLoadURL ), 
                                       255, 
                                       this );
            }

            return new PgDocDescr( ".fileman", 
                                    typeof( IPgLoadURL ), 
                                    0, 
                                    this );
        }
        public override IDisposable CreateDocument( IPgBaseSite oSite, string strExtension ) {
            if( string.Compare( strExtension, ".fileman", true ) == 0 )
                return new FileManager( oSite );

            return new FileManager( oSite );
        }

        public override IDisposable CreateView( IPgViewSite oBaseSite, object oDocument, Guid guidViewType ) {
            FileManager oDocFileMan = (FileManager)oDocument;

			try {
				if( guidViewType == ViewFileMan.GUID )
					return( new ViewFileMan( oBaseSite, oDocFileMan ) );
				if( guidViewType == Guid.Empty )
					return( new ViewFileMan( oBaseSite, oDocFileMan ) );

				return( new ViewFileMan( oBaseSite, oDocFileMan ) );
            } catch( Exception oEx ) {
                Type[] rgErrors = { typeof( NullReferenceException ),
                                    typeof( InvalidCastException ),
                                    typeof( ArgumentNullException ),
									typeof( ArgumentException ) };
                if( rgErrors.IsUnhandled( oEx ) )
                    throw;

				throw new InvalidOperationException( "Controller couldn't create view for Image document.", oEx );
            }
        }

        public override IEnumerator<IPgViewType> GetEnumerator() {
 	        yield return new ViewType( "List", ViewFileMan.GUID );
        }
    }

    public class ViewFileMan :
        WindowMultiColumn,
        IPgCommandView
    {
        public string    Banner => Document.CurrentURL;
		public SKBitmap  Icon { get; protected set; }
        public                  Guid Catagory => _sGuid;
        public           static Guid GUID     => _sGuid;
        private readonly static Guid _sGuid   = new( "{D257D1AA-AC3E-4A0F-83A3-97C95AE12782}" );

        protected readonly IPgMainWindow _oShellWin;
        public FileManager    Document { get; protected set; }
        public FileProperties DocProps { get; protected set; }

        protected class CachManSiteFM :
            CacheManSite 
        {
            ViewFileMan _oFMHost;
            public CachManSiteFM(WindowMultiColumn oHost) : base(oHost) {
                _oFMHost = (ViewFileMan)oHost;
            }

            public override void OnCaretPositioned(SKPointI pntCaret, bool fVisible) {
                base.OnCaretPositioned(pntCaret, fVisible);

                _oFMHost.OnCaretCheck();
            }
        }
        public ViewFileMan(IPgViewSite oViewSite, object oDocument) : base(oViewSite, oDocument) {
            Document   = (FileManager)oDocument;
            _oShellWin = (IPgMainWindow)oViewSite.Host;

            DocProps   = new( new DocSlot( this ) );
			Icon	   = Document.GetResource( "icons8-folder-94.png" );
        }

        protected override void Dispose( bool fDisposing ) {
            if( fDisposing ) {
                Icon?.Dispose();
            }
            base.Dispose( fDisposing );
        }

        protected override CacheMultiBase CreateCacheMan() {
            return new CacheMultiColumn( new CachManSiteFM( this ) ); 
        }

        /// <remarks>
        /// I want to push layout to the init phase so we could potentially
        /// load our layout from a file! ^_^
        /// </remarks>
        protected override bool Initialize() {
            if( !base.Initialize() )
                return false;

            List<ColumnInfo> rgCols = new List<ColumnInfo> {
                new ColumnInfo( (int)DClmn.Chck, new LayoutRect() { Style=LCss.Flex, Track=33 } ),
                new ColumnInfo( (int)DClmn.Type, new LayoutRect() { Style=LCss.Flex, Track=30 } ),       
                new ColumnInfo( (int)DClmn.Name, new LayoutRect() { Style=LCss.None } ),
                new ColumnInfo( (int)DClmn.Time, new LayoutRect() { Style=LCss.Flex, Track=100, Justify = Align.Right } ),
                new ColumnInfo( (int)DClmn.Date, new LayoutRect() { Style=LCss.Flex, Track=100, Justify = Align.Right } ),
                new ColumnInfo( (int)DClmn.Size, new LayoutRect() { Style=LCss.Flex, Track= 10, Justify = Align.Right } )
            };

            // TODO: If you're missing a data column it won't show the dir.
            // Might be nice to know why?

            InitColumns( rgCols );

            HyperLinks.Add( "DirJump",  OnDirJump  );
            HyperLinks.Add( "FileJump", OnFileJump );

            // At present the base window doesn't put the cursor anywhere, sooo...
            SelectionSet( 0, 0, 0 );

            return true;
        }

        protected void OnCaretCheck() {
            try {
                if( _oCacheMan.Caret2.Row is FileManager.FMRow oFMRow ) {
                    using FileProperties.BulkLoader2 oBulk = new( DocProps );

                    oBulk.ValueUpdate( FileProperties.Names.Date, 
                                       oFMRow[FileManager.FMRow.DCol.Date] );
                    oBulk.ValueUpdate( FileProperties.Names.Time,
                                       oFMRow[FileManager.FMRow.DCol.Time] );
                    oBulk.ValueUpdate( FileProperties.Names.Size,
                                       oFMRow[FileManager.FMRow.DCol.Size] );
                    oBulk.ValueUpdate( FileProperties.Names.Type,
                                       oFMRow[FileManager.FMRow.DCol.Type] );

                    // Debug code.
                    //if( _oCacheMan.CaretRow is CacheRow oCaretRow ) {
                    //    oBulk.ValueUpdate( FileProperties.Names.TmpTop,
                    //                       oCaretRow.Top.ToString() );
                    //    oBulk.ValueUpdate( FileProperties.Names.TmpBottom,
                    //                       oCaretRow.Bottom.ToString() );
                    //} else {
                    //    oBulk.ValueUpdate( FileProperties.Names.TmpTop,    "No Caret..." );
                    //    oBulk.ValueUpdate( FileProperties.Names.TmpBottom, "No Caret..." );
                    //}
                    //oBulk.ValueUpdate( FileProperties.Names.TmpRcTop,
                    //                   _oCacheMan.TextRect.Top.ToString() );
                    //oBulk.ValueUpdate( FileProperties.Names.TmpRcBottom,
                    //                   _oCacheMan.TextRect.Bottom.ToString() );
                }
            } catch( Exception oEx ) {
                if( _rgErrors.IsUnhandled( oEx ) )
                    throw;

                LogError( "View File Manager property display error." );
            }
        }

        public override void OnDocLoaded() {
            base.OnDocLoaded();
            _oSiteView.Notify( ShellNotify.BannerChanged );
        }

        static readonly Type[] _rgErrors = { typeof( NullReferenceException ),
                                             typeof( ArgumentOutOfRangeException ),
                                             typeof( IndexOutOfRangeException ),
                                             typeof( InvalidOperationException ) };

        protected void OnDirJump( Row oRow, int iColumn, IPgWordRange oRange ) {
            try {
                Line    oText  = oRow[(int)FileManager.FMRow.DCol.Name];
                string? strDir = oText.ToString();

                if( string.IsNullOrEmpty( strDir ) )
                    return;

                string strPath = Path.Combine( Document.CurrentURL, strDir );

                Document.ReadDir( strPath );
            } catch( Exception oEx ) {
                if( _rgErrors.IsUnhandled(oEx) )
                    throw;
            }
        }
        protected void OnFileJump( Row oRow, int iColumn, IPgWordRange oRange ) {
            try {
                Line    oText   = oRow[(int)FileManager.FMRow.DCol.Name];
                string? strFile = oText.ToString();

                if( string.IsNullOrEmpty( strFile ) )
                    return;

                string strPath = Path.Combine( Document.CurrentURL, strFile );

                _oShellWin.DocumentShow( strPath, Guid.Empty, fShow:true );
            } catch( Exception oEx ) {
                if( _rgErrors.IsUnhandled(oEx) )
                    throw;
            }
        }
        public object Decorate(IPgViewSite oBaseSite, Guid sGuid) {
            if( sGuid == GlobalDecor.Outline ) {
                return new ViewFManOutline( new ViewSlot( this ) );
            }
            if( sGuid == GlobalDecor.Properties ) {
                return new WindowStandardProperties( new ViewSlot( this ), DocProps );
            }
            return null;
        }

        public override bool Execute(Guid gCommand) {
            if( gCommand == GlobalCommands.JumpParent ) {
                Document.JumpToParentDir();
                return true;
            }
            if( gCommand == GlobalCommands.Copy ) {
                ClipboardCopyTo();
                return true;
            }
            return false;// none of the base operations are applicable.
        }
    }
}
