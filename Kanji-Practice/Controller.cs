using Play.Interfaces.Embedding;
using Play.Forms;

namespace Kanji_Practice {
    public class ControllerFactory : 
        IControllerFactory 
    {
        public static Guid Kangi = new Guid( "{6D6AF151-4870-4407-A2A1-A8BE3510C7FC}" );
        public ControllerFactory() {
        }

        public IPgController2 GetController( Guid sID ) {
            if( sID == Kangi ) {
                return new KanjiController();
            }

            throw new ArgumentOutOfRangeException();
        }
    }

    public class KanjiController : Controller {

        public static Guid ViewPlainCardStackGuid = new Guid( "{3729DF52-4724-43B1-82E4-1148D84A9FDA}" );

        public KanjiController() {
            _rgExtensions.Add( ".deck" );
        }
        public override IDisposable CreateDocument(IPgBaseSite oSite, string strExtension) {
            return new KanjiDocument( oSite );
        }

        public override IDisposable CreateView(IPgViewSite oViewSite, object oDocument, Guid guidViewType) {
            if( oDocument is KanjiDocument oKanjiDoc ) {
			    try {
                    if( guidViewType == Guid.Empty ) {
				        return new ViewKanji( oViewSite, oKanjiDoc );
                    }
                    if( guidViewType == ViewPlainCardStackGuid ) {
                        return new LineNumberWindow( oViewSite, oKanjiDoc.FlashCardDoc );
                    }
                } catch( Exception oEx ) {
                    Type[] rgErrors = { typeof( NullReferenceException ),
                                        typeof( InvalidCastException ),
                                        typeof( ArgumentNullException ),
									    typeof( ArgumentException ) };
                    if( rgErrors.IsUnhandled( oEx ) )
                        throw;
                }
            }

			throw new InvalidOperationException( "Controller couldn't create view for Kanji Practice document." );
        }

        public override IEnumerator<IPgViewType> GetEnumerator() {
 	        yield return new ViewType( "MainView", Guid.Empty );
            yield return new ViewType( "Card Stack", ViewPlainCardStackGuid );
        }
    }
}