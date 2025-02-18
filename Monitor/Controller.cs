using Play.Interfaces.Embedding;
using Play.Edit;
using z80;

namespace Monitor {
    public class ControllerFactory : 
        IControllerFactory 
    {
        public static Guid BBCBasicBinary = new Guid( "{5550F99D-C1D3-40EE-81F6-2D8B929BD112}" );
        public static Guid BBCBasicText   = new Guid( "{1E2F4285-E83B-4B1E-A28C-AA6CDDF99A23}" );
        public static Guid Z80Asm         = new Guid( "{51783F31-136A-484F-A02C-D9E0059BF24F}" );
        public ControllerFactory() {
        }

        public IPgController2 GetController( Guid sID ) {
            if( sID == BBCBasicBinary ) {
                return new BBCBasicBinaryController();
            }
            if( sID == BBCBasicText ) {
                return new BBCBasicTextController();
            }
            if( sID == Z80Asm ) {
                return new NewMonitorController();
            }

            throw new ArgumentOutOfRangeException();
        }
    }

    public class OldMonitorController : Controller {
        public OldMonitorController() {
            _rgExtensions.Add( ".asm" );
        }

        public override IDisposable CreateDocument(IPgBaseSite oSite, string strExtension) {
            return new Old_CPU_Emulator( oSite );
        }

        public override IDisposable CreateView(IPgViewSite oViewSite, object oDocument, Guid guidViewType) {
            if( oDocument is Old_CPU_Emulator oMonitorDoc ) {
			    try {
                    if( guidViewType == WindowFrontPanel.GUID )
                        return new WindowFrontPanel( oViewSite, oMonitorDoc );

                    // Service the GUID.Empty case too.
                    return new WindowFrontPanel( oViewSite, oMonitorDoc );
                } catch( Exception oEx ) {
                    Type[] rgErrors = { typeof( NullReferenceException ),
                                        typeof( InvalidCastException ),
                                        typeof( ArgumentNullException ),
									    typeof( ArgumentException ) };
                    if( rgErrors.IsUnhandled( oEx ) )
                        throw;
                }
            }

			throw new InvalidOperationException( "Controller couldn't create view for Monitor document." );
        }

        public override IEnumerator<IPgViewType> GetEnumerator() {
            yield return new ViewType( "ASM Monitor", WindowFrontPanel.GUID );
        }
    }

    public class BBCBasicController : Controller {
        public static Guid DumpWindowGUID { get; } = new Guid( "{247AE5B9-F6F1-4B6B-AB9C-81C83BC320B2}" );
        public override IDisposable CreateDocument(IPgBaseSite oSite, string strExtension) {
            return new BasicDocument( oSite, strExtension );
        }

        public override IDisposable CreateView(IPgViewSite oViewSite, object oDocument, Guid guidViewType) {
            if( oDocument is BasicDocument oMonitorDoc ) {
			    try {
                    if( guidViewType == WindowBasic.GUID )
					    return new WindowBasic( oViewSite, oMonitorDoc );
                    if( guidViewType == DumpWindowGUID )
                        return new EditWindow2( oViewSite, oMonitorDoc.DumpDocument );

                    // Service the GUID.Empty case too.
				    return new WindowBasic( oViewSite, oMonitorDoc );
                } catch( Exception oEx ) {
                    Type[] rgErrors = { typeof( NullReferenceException ),
                                        typeof( InvalidCastException ),
                                        typeof( ArgumentNullException ),
									    typeof( ArgumentException ) };
                    if( rgErrors.IsUnhandled( oEx ) )
                        throw;
                }
            }

			throw new InvalidOperationException( "Controller couldn't create view for Monitor document." );
        }

        public override IEnumerator<IPgViewType> GetEnumerator() {
            yield return new ViewType( "BBC Basic Edit", WindowBasic.GUID );
        }
    }
    public class BBCBasicBinaryController : BBCBasicController {
        public BBCBasicBinaryController() {
            _rgExtensions.Add( ".bas" ); // More generalized, but we'll give it a go.
            _rgExtensions.Add( ".bbc" ); // binary basic file (bbc basic for windows)
        }

        public override PgDocDescr Suitability(string strExtension) {
            foreach( string strExtn in _rgExtensions ) {
                if( string.Compare( strExtn, strExtension ) == 0 )
                    return new PgDocDescr( strExtension, typeof( IPgLoad<BinaryReader> ), (byte)255, this );
            }

            return new PgDocDescr( strExtension, typeof( IPgLoad<BinaryReader> ), (byte)0, this );
        }
    }

    public class BBCBasicTextController : BBCBasicController {
        public BBCBasicTextController() {
            _rgExtensions.Add( ".btx" );
            _rgExtensions.Add( ".tbtxt" );
        }

        public override PgDocDescr Suitability(string strExtension) {
            foreach( string strExtn in _rgExtensions ) {
                if( string.Compare( strExtn, strExtension ) == 0 )
                    return new PgDocDescr( strExtension, typeof( IPgLoad<TextReader> ), (byte)255, this );
            }

            return new PgDocDescr( strExtension, typeof( IPgLoad<TextReader> ), (byte)0, this );
        }
    }

    public class NewMonitorController : Controller {
        public NewMonitorController() {
            _rgExtensions.Add( ".asmprg" );
        }
        public override IDisposable CreateDocument(IPgBaseSite oSite, string strExtension) {
            return new DocumentMonitor( oSite );
        }

        protected static Guid _gViewDazzle   = new Guid( "{6F5EAD43-B191-404F-BC5D-F108FEB68205}" );
        public override IDisposable CreateView(IPgViewSite oViewSite, object oDocument, Guid guidViewType) {
            if( oDocument is DocumentMonitor oMonitorDoc ) {
			    try {
                    if( guidViewType == ViewDisassembly.GUID )
                        return new ViewDisassembly  ( oViewSite, oMonitorDoc );
                    if( guidViewType == _gViewDazzle )
                        return new ViewEmulatorImage( oViewSite, oMonitorDoc );
                    if( guidViewType == ViewTerminal._gViewTerminal )
                        return new ViewTerminal( oViewSite, oMonitorDoc.Doc_Terminal );

                    // Service the GUID.Empty case too.
                    return new ViewDisassembly( oViewSite, oMonitorDoc );
                } catch( Exception oEx ) {
                    Type[] rgErrors = { typeof( NullReferenceException ),
                                        typeof( InvalidCastException ),
                                        typeof( ArgumentNullException ),
									    typeof( ArgumentException ) };
                    if( rgErrors.IsUnhandled( oEx ) )
                        throw;
                }
            }

			throw new InvalidOperationException( "Controller couldn't create view for Monitor document." );
        }

        public override IEnumerator<IPgViewType> GetEnumerator() {
            yield return new ViewType( "Assembly Display", ViewDisassembly.GUID );
            yield return new ViewType( "Dazzle Display",   _gViewDazzle );
            yield return new ViewType( "Terminal",         ViewTerminal._gViewTerminal );
        }
    }

}