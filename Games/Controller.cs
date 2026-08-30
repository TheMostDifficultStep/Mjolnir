using Play.Interfaces.Embedding;
using Play.Spectrum;

namespace Play.Games {
    public class ControllerFactory : 
        IControllerFactory 
    {
        public static Guid TutTut = new Guid( "{CD0437CF-09F2-4824-9494-297AE5183233}" );
        public ControllerFactory() {
        }

        public IPgController2 GetController( Guid sID ) {
            if( sID == TutTut ) {
                return new TutTutController();
            }

            throw new ArgumentOutOfRangeException();
        }
    }

    public class TutTutController : Controller {
        public TutTutController() {
            _rgExtensions.Add( ".tut" );
        }
        public override IDisposable CreateDocument(IPgBaseSite oSite, string strExtension) {
            return new DocumentTutTut( oSite );
        }

        public override IDisposable CreateView(IPgViewSite oViewSite, object oDocument, Guid guidViewType) {
            if( oDocument is DocumentTutTut oTutDoc ) {
			    try {
                    // Service the GUID.Empty case too.
                    return new ViewTut( oViewSite, oTutDoc );
                } catch( Exception oEx ) {
                    Type[] rgErrors = { typeof( NullReferenceException ),
                                        typeof( InvalidCastException ),
                                        typeof( ArgumentNullException ),
									    typeof( ArgumentException ) };
                    if( rgErrors.IsUnhandled( oEx ) )
                        throw;
                }
            }

			throw new InvalidOperationException( "Controller couldn't create view for Monitor document." );
        }

        public override IEnumerator<IPgViewType> GetEnumerator() {
            yield return new ViewType( "Game Display", ViewTut.GUID );
        }
    }
}
