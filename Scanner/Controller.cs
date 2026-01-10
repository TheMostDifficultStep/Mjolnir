using System;
using System.Collections.Generic;

using Play.Interfaces.Embedding; 
using Play.Edit;
using Play.ImageViewer;

namespace Scanner {
    public class ScannerController : 
        Controller 
    {
        public ScannerController() {
			_rgExtensions.Add( ".scan" );
        }

        public override IDisposable? CreateDocument( IPgBaseSite oSite, string strExtension ) {
			if( strExtension.ToLower() == ".scan" ) {
				return new DocScanner( oSite );
			}

			return null;
        }

        public override IDisposable CreateView( IPgViewSite oBaseSite, object oDocument, Guid guidViewType ) {
			try {
                DocScanner oDocNetHost = (DocScanner)oDocument;

                switch( guidViewType ) {
                    default:
                        return new WindowSoloImage(oBaseSite, oDocNetHost );
                }
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
            yield return new ViewType( "Image", Guid.Empty );
        }

        public override PgDocDescr Suitability(string strExtension) {
            foreach( string strExtn in _rgExtensions ) {
                if( string.Compare( strExtn, strExtension, ignoreCase:true ) == 0 ) {
                    return new PgDocDescr( strExtension, 
                                           typeof( IPgLoad<BinaryReader> ), 
                                           200, 
                                           this );
                }
            }
            return new PgDocDescr( strExtension, 
                                   typeof( IPgLoad<BinaryReader>  ), 
                                   0, 
                                   this );
        }
    }
}
