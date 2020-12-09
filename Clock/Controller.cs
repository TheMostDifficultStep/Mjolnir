using System;
using System.Collections.Generic;

using Play.Interfaces.Embedding; 

namespace Play.Clock {
    public class SolarController : 
        Controller 
    {
        public SolarController() {
			_rgExtensions.Add( ".weather" );
        }

        public override IDisposable CreateDocument( IPgBaseSite oSite, string strExtension ) {
			if( strExtension.ToLower() == ".weather" ) {
				return( new SolarDoc( oSite ) );
			}

			return null;
        }

        public override IDisposable CreateView( IPgViewSite oBaseSite, object oDocument, Guid guidViewType ) {
            SolarDoc oMorsePractice = oDocument as SolarDoc ?? throw new ArgumentException( "Argument must be an SolarDoc." );

			try {
                switch( guidViewType ) {
                    case Guid r when r == ViewSolar._guidViewCatagory:
                        return new ViewSolar( oBaseSite, oMorsePractice );

                    default:
                        return new ViewSolar( oBaseSite, oMorsePractice );
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
            yield return new ViewType( "Solar Weather", ViewSolar._guidViewCatagory );
        }
    }
}
