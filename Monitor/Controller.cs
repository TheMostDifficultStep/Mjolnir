using Play.Interfaces.Embedding;
using Play.Forms;

using SkiaSharp;
using Play.Edit;

namespace Monitor {
    public abstract class BaseController : Controller {
        public static Guid DumpWindowGUID { get; } = new Guid( "{247AE5B9-F6F1-4B6B-AB9C-81C83BC320B2}" );

        public override IDisposable CreateDocument(IPgBaseSite oSite, string strExtension) {
            return new Old_CPU_Emulator( oSite );
        }

    }
    public class OldMonitorController : BaseController {
        public OldMonitorController() {
            _rgExtensions.Add( ".asm" );
        }

        public override IDisposable CreateView(IPgViewSite oViewSite, object oDocument, Guid guidViewType) {
            if( oDocument is Old_CPU_Emulator oMonitorDoc ) {
			    try {
                    if( guidViewType == WindowFrontPanel.GUID )
                        return new WindowFrontPanel( oViewSite, oMonitorDoc );
                    if( guidViewType == DumpWindowGUID )
                        return new EditWindow2( oViewSite, oMonitorDoc.DumpDocument );

                    // Service the GUID.Empty case too.
                    return new EditWindow2( oViewSite, oMonitorDoc.DumpDocument );
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

    public class BBCBasicController : BaseController {
        public override IDisposable CreateView(IPgViewSite oViewSite, object oDocument, Guid guidViewType) {
            if( oDocument is Old_CPU_Emulator oMonitorDoc ) {
			    try {
                    if( guidViewType == BasicLineWindow.GUID )
					    return new BasicLineWindow( oViewSite, oMonitorDoc );
                    if( guidViewType == DumpWindowGUID )
                        return new EditWindow2( oViewSite, oMonitorDoc.DumpDocument );

                    // Service the GUID.Empty case too.
				    return new BasicLineWindow( oViewSite, oMonitorDoc );
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
            yield return new ViewType( "BBC Basic Edit", BasicLineWindow.GUID );
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
        }
    }

    public class NewMonitorController : Controller {
        public NewMonitorController() {
            _rgExtensions.Add( ".asm" );
        }

        public override IDisposable CreateView(IPgViewSite oViewSite, object oDocument, Guid guidViewType) {
            if( oDocument is Document_Monitor oMonitorDoc ) {
			    try {
                    if( guidViewType == Window_Program_Display.GUID )
                        return new Window_Program_Display( oViewSite, oMonitorDoc.Doc_Displ );
                    if( guidViewType == NumberLabelWindow.GUID )
                        return new NumberLabelWindow( oViewSite, oMonitorDoc.Doc_Asm );

                    // Service the GUID.Empty case too.
                    return new NumberLabelWindow( oViewSite, oMonitorDoc.Doc_Asm );
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
            yield return new ViewType( "Assembly Display", Window_Program_Display.GUID );
        }
        public override IDisposable CreateDocument(IPgBaseSite oSite, string strExtension) {
            return new Document_Monitor( oSite );
        }
    }

}