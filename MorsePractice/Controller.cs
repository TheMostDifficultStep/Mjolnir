using System;
using System.Collections.Generic;

using Play.Interfaces.Embedding; 
using Play.Edit;

namespace Play.MorsePractice
{
    /// <summary>
    /// This is the simple more code practice document. Which was the original reason I wrote
    /// all this.
    /// </summary>
    public class MorseController : 
        Controller 
    {
        protected readonly static Guid _guidNotes      = new Guid( "{4C5B0677-1DD9-4692-897D-49ACC7DA902D}" );
		protected readonly static Guid _guidMorseTable = new Guid( "{47929B37-BCAB-41D5-81EE-D60FD092F90C}" );
        protected readonly static Guid _guidSchedule   = new Guid( "{7E7AAEE2-154F-4876-AD5B-7DA80E1A1055}" );

        public MorseController() {
			_rgExtensions.Add( ".morse" );
        }

        public override IDisposable CreateDocument( IPgBaseSite oSite, string strExtension ) {
			if( strExtension.ToLower() == ".morse" ) {
				return( new MorseDoc( oSite ) );
			}

			return null;
        }

        public override IDisposable CreateView( IPgViewSite oBaseSite, object oDocument, Guid guidViewType ) {
            MorseDoc oMorsePractice = oDocument as MorseDoc ?? throw new ArgumentException( "Argument must be an ImageWalkerDoc" );

			try {
                switch( guidViewType ) {
				    case Guid r when r == ViewMorse._guidViewCategory:
					    return new ViewMorse( oBaseSite, oMorsePractice );

                    case Guid r when r == _guidNotes:
                        return new EditWindow2( oBaseSite, oMorsePractice.Notes );

                    case Guid r when r == _guidMorseTable:
					    return new EditWindow2( oBaseSite, oMorsePractice.Morse, fReadOnly:true );

                    default:
					    return new ViewMorse( oBaseSite, oMorsePractice );
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
 	        yield return new ViewType( "Practice",  ViewMorse._guidViewCategory );
            yield return new ViewType( "Notes",     _guidNotes );
            yield return new ViewType( "Reference", _guidMorseTable);
        }
    }

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
				return new DocNotes( oSite ) { FlagSComsOn = true };
			}

			return null;
        }

        public override IDisposable CreateView( IPgViewSite oBaseSite, object oDocument, Guid guidViewType ) {
            DocNotes oMorsePractice = oDocument as DocNotes ?? throw new ArgumentException( "Argument must be an ImageWalkerDoc" );

			try {
                switch( guidViewType ) {
                    case Guid r when r == ViewQrz._guidViewCategory:
                        return new ViewQrz(oBaseSite, oMorsePractice);

                    case Guid r when r == ViewNotes._guidViewCategory:
                        return new ViewNotes(oBaseSite, oMorsePractice);

                    //case Guid r when r == ViewLog.ViewLogger:
                    //    return new ViewLog( oBaseSite, oMorsePractice );

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
          //yield return new ViewType( "Logger",       ViewLog.ViewLogger ); broken for now.
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
				return new DocNotes( oSite ) { FlagScanCalls = false };
			}

			return null;
        }

        public override IDisposable CreateView( IPgViewSite oBaseSite, object oDocument, Guid guidViewType ) {
            DocNotes oMorsePractice = oDocument as DocNotes ?? throw new ArgumentException( "Argument must be an ImageWalkerDoc" );

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
        public MorseController4() {
			_rgExtensions.Add( ".netlogm" );
        }

        public override IDisposable CreateDocument( IPgBaseSite oSite, string strExtension ) {
			if( strExtension.ToLower() == ".netlogm" ) {
				return new DocMultiColumn( oSite );
			}

			return null;
        }

        public override IDisposable CreateView( IPgViewSite oBaseSite, object oDocument, Guid guidViewType ) {
			try {
                switch( guidViewType ) {
                    case Guid r when r == ViewLog.ViewCatagory:
                        return new ViewLog(oBaseSite, oDocument );

                    default:
                        return new ViewLog(oBaseSite, oDocument );
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
            yield return new ViewType( "Log", ViewLog.ViewCatagory );
        }
    }
}
