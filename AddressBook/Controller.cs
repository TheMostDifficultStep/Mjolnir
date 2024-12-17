using System;
using System.Collections.Generic;

using Play.Interfaces.Embedding; 
using Play.Edit;

namespace AddressBook {
    public class Controller :
        Play.Interfaces.Embedding.Controller 
    {
        public Controller() {
			_rgExtensions.Add( ".addr" );
        }

        public override IDisposable CreateDocument( IPgBaseSite oSite, string strExtension ) {
			return new DocAddrBook( oSite );
        }

        public override IDisposable CreateView( IPgViewSite oBaseSite, object oDocument, Guid guidViewType ) {
            DocAddrBook oMorsePractice = oDocument as DocAddrBook ?? throw new ArgumentException( "Argument must be an ImageWalkerDoc" );

			try {
                switch( guidViewType ) {
                    case Guid r when r == ViewSingleAddr.ViewCategory:
                        return new ViewSingleAddr(oBaseSite, oMorsePractice);
                    case Guid r when r == ViewLabel.ViewCategory:
                        return new ViewLabel(oBaseSite, oMorsePractice);

                    default:
                        return new ViewSingleAddr(oBaseSite, oMorsePractice );
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
            yield return new ViewType( "Address", ViewSingleAddr.ViewCategory );
            yield return new ViewType( "Print",   ViewLabel     .ViewCategory );
        }
    }
}
