using Play.Interfaces.Embedding;

namespace Kanji_Practice {
    public class KanjiController : Controller {
        public KanjiController() {
            _rgExtensions.Add( ".deck" );
        }
        public override IDisposable CreateDocument(IPgBaseSite oSite, string strExtension) {
            return new KanjiDocument( oSite );
        }

        public override IDisposable CreateView(IPgViewSite oViewSite, object oDocument, Guid guidViewType) {
            if( oDocument is KanjiDocument oKanjiDoc ) {
			    try {
				    return new ViewKanji( oViewSite, oKanjiDoc );
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
        }
    }
}