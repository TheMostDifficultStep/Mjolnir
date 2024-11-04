using SkiaSharp;

using Play.Edit;
using Play.Interfaces.Embedding;
using Play.Rectangles;
using Play.Parse;

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

        public FileManager   Document { get; protected set; }
        protected readonly IPgMainWindow _oShellWin;

        public ViewFileMan(IPgViewSite oViewSite, object oDocument) : base(oViewSite, oDocument) {
            Document   = (FileManager)oDocument;
            _oShellWin = (IPgMainWindow)oViewSite.Host;

			Icon	   = Document.GetResource( "icons8-folder-94.png" );
        }

        protected override void Dispose( bool fDisposing ) {
            if( fDisposing ) {
                Icon?.Dispose();
            }
            base.Dispose( fDisposing );
        }

        /// <remarks>
        /// I want to push layout to the init phase so we could potentially
        /// load our layout from a file! ^_^
        /// </remarks>
        protected override bool Initialize() {
            if( !base.Initialize() )
                return false;

            int iTop = _rgLayout.Count;

            List<ColumnInfo> rgCols = new List<ColumnInfo> {
                new ColumnInfo( new LayoutRect( LayoutRect.CSS.Pixels,   (uint)_oCacheMan.GlyphCheck.Coordinates.advance_x, 
                                                                             1L ), (int)FileManager.FMRow.DCol.Chck ),
                new ColumnInfo( new LayoutRect( LayoutRect.CSS.Pixels,   50, 1L ), (int)FileManager.FMRow.DCol.Type ),       
                new ColumnInfo( new LayoutRect( LayoutRect.CSS.None,     80, 1L ), (int)FileManager.FMRow.DCol.Name ),
                new ColumnInfo( new LayoutRect( LayoutRect.CSS.Pixels,  110, 1L ) {Justify = Align.Right }, (int)FileManager.FMRow.DCol.Time ),
                new ColumnInfo( new LayoutRect( LayoutRect.CSS.Pixels,  130, 1L ) {Justify = Align.Right }, (int)FileManager.FMRow.DCol.Date ),
                new ColumnInfo( new LayoutRect( LayoutRect.CSS.Pixels,  130, 1L ) {Justify = Align.Right }, (int)FileManager.FMRow.DCol.Size )
            };

            // TODO: If you're missing a data column it won't show the dir.
            // Might be nice to know why?

            InitColumns( rgCols );

            HyperLinks.Add( "DirJump",  OnDirJump );
            HyperLinks.Add( "FileJump", OnFileJump );

            return true;
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
            if( sGuid == GlobalDecorations.Outline ) {
                return new ViewFManOutline( new ViewSlot( this ) );
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
