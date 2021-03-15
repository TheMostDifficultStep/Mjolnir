using System;
using System.Collections.Generic;

using Play.Interfaces.Embedding;
using Play.ImageViewer;

namespace Play.SSTV {
	public class MySSTVController : Controller {
		public MySSTVController() {
			_rgExtensions.Add( ".mysstv" );
		}

		public override IDisposable CreateDocument(IPgBaseSite oSite, string strExtension) {
			return( new DocSSTV( oSite ) );
		}

		public override IDisposable CreateView(IPgViewSite oBaseSite, object oDocument, Guid guidViewType) {
			try {
				DocSSTV oMySSTVDoc = (DocSSTV)oDocument;

				if( guidViewType == ViewFFT     .ViewType )
					return new ViewFFT( oBaseSite, oMySSTVDoc );
				if( guidViewType == ViewTransmit.ViewType )
					return new ViewTransmit( oBaseSite, oMySSTVDoc );
				//if( guidViewType == ViewRecieve .ViewType )
				//	return new ViewRecieve( oBaseSite, oMySSTVDoc );
				if( guidViewType == SSTVReceiveImage.ViewRX )
					return new SSTVReceiveImage( oBaseSite, oMySSTVDoc );

				// Make the receive the default in the future.
				return new ViewTransmit( oBaseSite, oMySSTVDoc );
            } catch( Exception oEx ) {
				// TODO: Stuff errors collection into the base controller.
                Type[] rgErrors = { typeof( NullReferenceException ),
                                    typeof( InvalidCastException ),
                                    typeof( ArgumentNullException ),
									typeof( ArgumentException ) };
                if( rgErrors.IsUnhandled( oEx ) )
                    throw;

				throw new InvalidOperationException( "Controller couldn't create view for Music Collection document.", oEx );
            }
		}

		public override IEnumerator<IPgViewType> GetEnumerator() {
		    yield return new ViewType( "Spectrum",  ViewFFT     .ViewType );
			yield return new ViewType( "Tx Screen", ViewTransmit.ViewType );
		  //yield return new ViewType( "Rx Screen", ViewRecieve .ViewType );
			yield return new ViewType( "Rx Image",  SSTVReceiveImage.ViewRX );
		}
	}

}
