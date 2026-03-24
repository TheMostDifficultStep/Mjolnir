using System.Xml;
using Play.Interfaces.Embedding;

namespace Play.FileManager {
    public class ControllerFactory : 
        IControllerFactory 
    {
        public static Guid Explorer = new Guid( "{4462E346-3776-4AE9-A3EE-961CFB3DDD09}" );
        public ControllerFactory() {
        }

        public IPgController2 GetController( Guid sID ) {
            if( sID == Explorer ) {
                return new FileManController();
            }

            throw new ArgumentOutOfRangeException();
        }
    }


    public class FileManController : 
        Controller 
    {
        public FileManController() {
            _rgExtensions.Add( ".fman" );
            _rgExtensions.Add( ".fileman" );
        }

        public override PgDocDescr Suitability(string strExtension) {
            if( strExtension.CompareTo( ".fman" ) == 0 ) {
                return new PgDocDescr( ".fman", 
                                       typeof( IPgLoad<XmlNode> ), 
                                       255, 
                                       this );
            }
            if( strExtension.CompareTo( ".fileman" ) == 0 ) {
                return new PgDocDescr( ".fileman", 
                                       typeof( IPgLoadUrl ), 
                                       255, 
                                       this );
            }


            return new PgDocDescr( ".fman", 
                                    typeof( IPgLoadUrl ), 
                                    0, 
                                    this );
        }
        public override IDisposable CreateDocument( IPgBaseSite oSite, string strExtension ) {
            if( string.Compare( strExtension, ".fman", true ) == 0 )
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

}
