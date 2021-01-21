using System;
using System.Collections.Generic;

using Play.Interfaces.Embedding;

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

				if( guidViewType == WindowFFT.ViewType )
					return( new WindowFFT( oBaseSite, oMySSTVDoc ) );
				if( guidViewType == WindowTransmit.ViewType )
					return( new WindowTransmit( oBaseSite, oMySSTVDoc ) );

				return( new WindowFFT( oBaseSite, oMySSTVDoc ) );
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
		    yield return( new ViewType( "Spectrum", WindowFFT._gViewType ) );
		}
	}

}
