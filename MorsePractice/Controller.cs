using System;
using System.Collections.Generic;

using Play.Interfaces.Embedding; 
using Play.Edit;

namespace Play.MorsePractice
{
    public class MorseController2 : 
        Controller 
    {
        protected readonly static Guid _guidRawBio     = new Guid( "{e6bfe197-9cbd-43cc-9098-4a8db5b19066}" );
        protected readonly static Guid _guidRawPage    = new Guid( "{2c71fdb9-c842-4df3-8f55-7fdffbb757bc}" );
        protected readonly static Guid _guidSchedule   = new Guid( "{7E7AAEE2-154F-4876-AD5B-7DA80E1A1055}" );

        public MorseController2() {
			_rgExtensions.Add( ".netlog" );
        }

        public override IDisposable CreateDocument( IPgBaseSite oSite, string strExtension ) {
			if( strExtension.ToLower() == ".netlog" ) {
				return new DocStdLog( oSite ) { FlagSComsOn = true };
			}

			return null;
        }

        public override IDisposable CreateView( IPgViewSite oBaseSite, object oDocument, Guid guidViewType ) {
            DocStdLog oMorsePractice = oDocument as DocStdLog ?? throw new ArgumentException( "Argument must be an ImageWalkerDoc" );

			try {
                switch( guidViewType ) {
                    case Guid r when r == ViewQrz._guidViewCategory:
                        return new ViewQrz(oBaseSite, oMorsePractice);

                    case Guid r when r == ViewNotes._guidViewCategory:
                        return new ViewNotes(oBaseSite, oMorsePractice);

                    case Guid r when r == _guidSchedule:
                        return new EditWindow2( oBaseSite, oMorsePractice.Calls, fReadOnly:true, fSingleLine:false );

                    case Guid r when r == _guidRawBio:
                        return new EditWindow2(oBaseSite, oMorsePractice.CallSignBioHtml, fReadOnly: true);

                    case Guid r when r == _guidRawPage:
                        return new EditWindow2(oBaseSite, oMorsePractice.CallSignPageHtml, fReadOnly: true);

                    default:
                        return new ViewNotes(oBaseSite, oMorsePractice );
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
            yield return new ViewType( "Notes",        ViewNotes._guidViewCategory );
            yield return new ViewType( "Schedule",     _guidSchedule );
            yield return new ViewType( "Qrz",          ViewQrz._guidViewCategory );
            yield return new ViewType( "Qrz Raw Bio",  _guidRawBio );
            yield return new ViewType( "Qrz Raw Page", _guidRawPage );
        }
    }

    public class MorseController3 : 
        Controller 
    {
        public MorseController3() {
			_rgExtensions.Add( ".stdlog" );
        }

        public override IDisposable CreateDocument( IPgBaseSite oSite, string strExtension ) {
			if( strExtension.ToLower() == ".stdlog" ) {
				return new DocStdLog( oSite ) { FlagScanCalls = false };
			}

			return null;
        }

        public override IDisposable CreateView( IPgViewSite oBaseSite, object oDocument, Guid guidViewType ) {
            DocStdLog oMorsePractice = oDocument as DocStdLog ?? throw new ArgumentException( "Argument must be an ImageWalkerDoc" );

			try {
                switch( guidViewType ) {
                    case Guid r when r == ViewSimple._guidViewCategory:
                        return new ViewSimple(oBaseSite, oMorsePractice);

                    default:
                        return new ViewSimple(oBaseSite, oMorsePractice );
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
            yield return new ViewType( "Notes", ViewSimple._guidViewCategory );
        }
    }

    public class MorseController4 : 
        Controller 
    {
        public static Guid ViewUniqueOperators { get; } = new Guid( "{09C47759-AF1C-45AB-A6F5-C60C3AC88E7E}");
        public MorseController4() {
			_rgExtensions.Add( ".netlogm" );
        }

        public override IDisposable CreateDocument( IPgBaseSite oSite, string strExtension ) {
			if( strExtension.ToLower() == ".netlogm" ) {
				return new DocNetHost( oSite );
			}

			return null;
        }

        public override IDisposable CreateView( IPgViewSite oBaseSite, object oDocument, Guid guidViewType ) {
			try {
                DocNetHost oDocNetHost = (DocNetHost)oDocument;
                switch( guidViewType ) {
                    case Guid r when r == ViewNetLog.ViewCatagory:
                        return new ViewLogAndNotes(oBaseSite, oDocNetHost );
                    case Guid r when r == ViewUniqueOperators:
                        return new EditWindow2(oBaseSite, oDocNetHost.Log.Calls, fReadOnly: true );

                    default:
                        return new ViewLogAndNotes(oBaseSite, (DocNetHost)oDocument );
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
            yield return new ViewType( "Log",       ViewLogAndNotes.ViewCategory );
            yield return new ViewType( "Operators", ViewUniqueOperators );
        }
    }
}
