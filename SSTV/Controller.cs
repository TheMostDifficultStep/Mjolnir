using System;
using System.Collections.Generic;

using Play.Interfaces.Embedding;
using Play.Edit;

namespace Play.SSTV {
	public class MySSTVController : Controller {
		public static Guid ViewTransmitModes { get; }  = new Guid( "{9F388C2E-EC54-4330-B7BD-07137D104819}" );
		public static Guid ViewTransmitImage { get; }  = new Guid( "{B5D3C976-DEFC-46F0-9459-148BABBBEFFE}" );

		public MySSTVController() {
			_rgExtensions.Add( ".mysstv" );
		}

		public override IDisposable CreateDocument(IPgBaseSite oSite, string strExtension) {
			return( new DocSSTV( oSite ) );
		}

		public override IDisposable CreateView(IPgViewSite oBaseSite, object oDocument, Guid guidViewType) {
			try {
				DocSSTV oMySSTVDoc = (DocSSTV)oDocument;

				if( guidViewType == ViewFFT.GUID )
					return new ViewFFT( oBaseSite, oMySSTVDoc );
                if( guidViewType == ViewDiagnostics.GUID )
                    return new ViewDiagnostics(oBaseSite, oMySSTVDoc);
                if( guidViewType == ViewTransmitSolo.GUID )
					return new ViewTransmitSolo( oBaseSite, oMySSTVDoc );
				if( guidViewType == WindowReceiver.GUID )
					return new WindowReceiver( oBaseSite, oMySSTVDoc );
				if( guidViewType == ViewSettings.GUID )
					return new ViewSettings( oBaseSite, oMySSTVDoc );
				if( guidViewType == ViewTransmitModes ) {
					// Seealso ViewTransmit.Listen_ViewMode_LineChanged()
					return new CheckList( oBaseSite, oMySSTVDoc.RxModeList );
				}

				return new WindowReceiver( oBaseSite, oMySSTVDoc );
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
		  //yield return new ViewType( "Spectrum",       ViewFFT      .ViewType );
			yield return new ViewType( "Receive Image",  ViewRxImage     .GUID );
		    yield return new ViewType( "Diagnostics",    ViewDiagnostics .GUID );
		    yield return new ViewType( "Transmit Image", ViewTransmitSolo.GUID );
			yield return new ViewType( "Settings",       ViewSettings    .GUID );
		  //yield return new ViewType( "Mode Select",    ViewTransmitModes );
		}
	}

}
