using System;
using System.Collections.Generic;

using Play.Interfaces.Embedding;

namespace Play.SSTV {
    public class ControllerFactory : 
        IControllerFactory 
    {
        public static Guid SSTV = new Guid( "{C01C4B93-2C9F-47D6-B9E6-0B0F38E2C1BE}" );
        public ControllerFactory() {
        }

        public IPgController2 GetController( Guid sID ) {
            if( sID == SSTV ) {
                return new MySSTVController();
            }

            throw new ArgumentOutOfRangeException();
        }
    }

	public class MySSTVController : Controller {
		public static Guid ViewTransmitModes { get; }  = new Guid( "{9F388C2E-EC54-4330-B7BD-07137D104819}" );
		public static Guid ViewTransmitImage { get; }  = new Guid( "{B5D3C976-DEFC-46F0-9459-148BABBBEFFE}" );

		public MySSTVController() {
			_rgExtensions.Add( ".mysstv" );
		}

		public override IDisposable CreateDocument(IPgBaseSite oSite, string strExtension) {
			return new DocSSTV( oSite );
		}

		public override IDisposable CreateView(IPgViewSite oBaseSite, object oDocument, Guid guidViewType) {
			try {
				DocSSTV oMySSTVDoc = (DocSSTV)oDocument;

				if( guidViewType == ViewFFT.GUID )
					return new ViewFFT( oBaseSite, oMySSTVDoc );

                if( guidViewType == WindowDiagnostics.GUID )
                    return new WindowDiagnostics(oBaseSite, oMySSTVDoc);

                if( guidViewType == ViewTransmitDeluxe.GUID )
					return new ViewTransmitDeluxe( oBaseSite, oMySSTVDoc );

				if( guidViewType == WindowDeviceViewer.GUID )
					return new WindowDeviceViewer( oBaseSite, oMySSTVDoc );

				if( guidViewType == WindowFileViewer.GUID )
					return new WindowFileViewer( oBaseSite, oMySSTVDoc );

				if( guidViewType == ViewSettings.GUID )
					return new ViewSettings( oBaseSite, oMySSTVDoc );

				if( guidViewType == WindowSSTVChooser.GUID )
					return new WindowSSTVChooser( oBaseSite, oMySSTVDoc );

				if( guidViewType == WindowSoloRx.GUID )
					return new WindowSoloRx( oBaseSite, oMySSTVDoc );

				if( guidViewType == WindowSSTVHistory.GUID )
					return new WindowSSTVHistory( oBaseSite, oMySSTVDoc );


				return new WindowSoloRx( oBaseSite, oMySSTVDoc );
            } catch( Exception oEx ) {
				// TODO: Stuff errors collection into the base controller.
                Type[] rgErrors = { typeof( NullReferenceException ),
                                    typeof( InvalidCastException ),
                                    typeof( ArgumentNullException ),
									typeof( ArgumentException ) };
                if( rgErrors.IsUnhandled( oEx ) )
                    throw;

				throw new InvalidOperationException( "Controller couldn't create view for MySSTV document.", oEx );
            }
		}

		public override IEnumerator<IPgViewType> GetEnumerator() {
		  //yield return new ViewType( "Spectrum",            ViewFFT      .ViewType );
			yield return new ViewType( "Display from Device", WindowDeviceViewer.GUID );
			yield return new ViewType( "Display from File",   WindowFileViewer  .GUID );
		    yield return new ViewType( "Transmit",            ViewTransmitDeluxe.GUID );
		    yield return new ViewType( "Diagnostics",         WindowDiagnostics .GUID );
			yield return new ViewType( "Chooser",             WindowSSTVChooser .GUID );
			yield return new ViewType( "History",			  WindowSSTVHistory .GUID );
			yield return new ViewType( "Settings",            ViewSettings      .GUID );
		}
	}

}
