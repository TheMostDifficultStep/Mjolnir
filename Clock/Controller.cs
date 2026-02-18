using System;
using System.Collections.Generic;

using Play.Interfaces.Embedding; 

namespace Play.Clock {
    public class ControllerForSolar : 
        Controller 
    {
        public ControllerForSolar() {
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
    public class ControllerForClock : 
        Controller 
    {
        public ControllerForClock() {
			_rgExtensions.Add( ".clock" );
        }

        public override IDisposable CreateDocument( IPgBaseSite oSite, string strExtension ) {
			if( strExtension.ToLower() == ".clock" ) {
				return( new DocumentClock( oSite ) );
			}

			return null;
        }

        /// <exception cref="InvalidOperationException"></exception>
        /// <exception cref="InvalidCastException"></exception>
        public override IDisposable CreateView( IPgViewSite oBaseSite, object oDocument, Guid guidViewType ) {
            DocumentClock oDocClock = (DocumentClock)oDocument;

			try {
                switch( guidViewType ) {
                    case Guid r when r == ViewAnalogClock.Guid:
                        return new ViewAnalogClock( oBaseSite, oDocClock );
                    case Guid r when r == ViewDigitalClock.Guid:
                        return new ViewDigitalClock( oBaseSite, oDocClock );

                    default:
                        return new ViewDigitalClock( oBaseSite, oDocClock );
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
            yield return new ViewType( "Time Zones",   ViewDigitalClock    .Guid );
            yield return new ViewType( "Analog Clock", ViewAnalogClock.Guid );
        }
    }

    public class ControllerFactory : 
        IControllerFactory 
    {
        public static Guid Clock = new Guid( "{36A57A73-2296-4DFB-B430-882B72D98180}" );
        public static Guid Solar = new Guid( "{E42ED42F-3362-4C68-A393-2AEFCA1B2F9E}" );
        public ControllerFactory() {
        }

        public IPgController2 GetController( Guid guidID ) {
            if( guidID == Clock ) {
                return new ControllerForSolar();
            }
            if( guidID == Solar ) {
                return new ControllerForClock();
            }

            throw new ArgumentOutOfRangeException();
        }
    }}
