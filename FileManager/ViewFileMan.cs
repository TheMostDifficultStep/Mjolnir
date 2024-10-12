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
        public string    Banner => "File Manager : " + _oDocument.CurrentURL;
		public SKBitmap  Icon { get; protected set; }
        public                  Guid Catagory => _sGuid;
        public           static Guid GUID     => _sGuid;
        private readonly static Guid _sGuid   = new( "{D257D1AA-AC3E-4A0F-83A3-97C95AE12782}" );

        protected readonly FileManager _oDocument;

        public ViewFileMan(IPgViewSite oViewSite, object oDocument) : base(oViewSite, oDocument) {
            _oDocument = (FileManager)oDocument;
			Icon	   = _oDocument.GetResource( "icons8-script-96.png" );
        }

        /// <remarks>
        /// I want to push layout to the init phase so we could potentially
        /// load our layout from a file! ^_^
        /// </remarks>
        protected override bool Initialize() {
            if( !base.Initialize() )
                return false;

            TextLayoutAdd( new LayoutRect( LayoutRect.CSS.Pixels,  120, 1L ), (int)FileManager.FMRow.Col.Type ); 
            TextLayoutAdd( new LayoutRect( LayoutRect.CSS.None,     80, 1L ), (int)FileManager.FMRow.Col.Name ); 
            TextLayoutAdd( new LayoutRect( LayoutRect.CSS.Pixels,  200, 1L ), (int)FileManager.FMRow.Col.Date );
            TextLayoutAdd( new LayoutRect( LayoutRect.CSS.Pixels,  100, 1L ), (int)FileManager.FMRow.Col.Size ); 

            HyperLinks.Add( "DirJump", OnCpuJump );

            return true;
        }

        static readonly Type[] _rgErrors = { typeof( NullReferenceException ),
                                             typeof( ArgumentOutOfRangeException ),
                                             typeof( IndexOutOfRangeException ),
                                             typeof( InvalidOperationException ) };

        protected void OnCpuJump( Row oRow, int iColumn, IPgWordRange oRange ) {
            try {
                Line oText = oRow[iColumn];

                string strPath = Path.Combine( _oDocument.CurrentURL, oText.ToString() );

                _oDocument.ReadDir( strPath );
            } catch( Exception oEx ) {
                if( _rgErrors.IsUnhandled(oEx) )
                    throw;
            }
        }
        public object Decorate(IPgViewSite oBaseSite, Guid sGuid) {
            return null;
        }

        public override bool Execute(Guid gCommand) {
            return false;// none of the base operations are applicable.
        }
    }
}
