using System;
using System.Collections.Generic;

using Play.Interfaces.Embedding;
using Play.Edit;
using Play.ImageViewer;

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

				if( guidViewType == ViewFFT     .ViewType )
					return new ViewFFT( oBaseSite, oMySSTVDoc );
                //if( guidViewType == ViewTransmit.ViewType )
                //	return new ViewTransmit( oBaseSite, oMySSTVDoc );
                if( guidViewType == ViewRecieve.ViewType )
                    return new ViewRecieve(oBaseSite, oMySSTVDoc);
                if( guidViewType == SSTVTransmitSelect.ViewType )
					return new SSTVTransmitSelect( oBaseSite, oMySSTVDoc );
				if( guidViewType == SSTVReceiveImage.ViewType )
					return new SSTVReceiveImage( oBaseSite, oMySSTVDoc );
				if( guidViewType == ViewTransmitModes ) {
					// Seealso ViewTransmit.Listen_ViewMode_LineChanged()
					EditWindow2 oView = new SSTVModeView( oBaseSite, oMySSTVDoc.ModeList );
					return oView;
				}

				return new SSTVReceiveImage( oBaseSite, oMySSTVDoc );
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
		  //yield return new ViewType( "Spectrum",       ViewFFT           .ViewType );
		  //yield return new ViewType( "Tx Screen",      ViewTransmit      .ViewType );
			yield return new ViewType( "Rx Image",       SSTVReceiveImage  .ViewType );
		    yield return new ViewType( "Rx Dual Screen", ViewRecieve       .ViewType );
		    yield return new ViewType( "Tx Image",       SSTVTransmitSelect.ViewType );
			yield return new ViewType( "Tx Modes",       ViewTransmitModes );
		}
	}

}
