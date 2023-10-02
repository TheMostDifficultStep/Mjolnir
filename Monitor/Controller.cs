using System.Collections;
using System.Collections.Generic;

using Play.Interfaces.Embedding;
using Play.Forms;

using SkiaSharp;
using Play.Edit;

namespace Monitor {
    public abstract class BaseController : Controller {
        public override IDisposable CreateDocument(IPgBaseSite oSite, string strExtension) {
            return new MonitorDocument( oSite );
        }

    }
    public class MonitorController : BaseController {
        public MonitorController() {
            _rgExtensions.Add( ".asm" );
        }

        /*
         * Arggg... I don't want to commit to read/write in binary yet... 
         *          but the shell can support it now! ^_^;;
        public override PgDocumentDescriptor Suitability(string strExtension) {
            if( string.Compare( PrimaryExtension, strExtension ) == 0 )
                return new PgDocumentDescriptor( strExtension, typeof( IPgLoad<BinaryReader> ), (byte)255, this );

            return new PgDocumentDescriptor( strExtension, typeof( IPgLoad<BinaryReader> ), (byte)0, this );
        }
        */

        public static Guid DumpWindowGUID { get; } = new Guid( "{247AE5B9-F6F1-4B6B-AB9C-81C83BC320B2}" );


        public override IDisposable CreateView(IPgViewSite oViewSite, object oDocument, Guid guidViewType) {
            if( oDocument is MonitorDocument oMonitorDoc ) {
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
        public BBCBasicController() {
            _rgExtensions.Add( ".bas" ); // Unicode text basic file.
        }

        public static Guid DumpWindow { get; } = new Guid( "{247AE5B9-F6F1-4B6B-AB9C-81C83BC320B2}" );

        public override IDisposable CreateView(IPgViewSite oViewSite, object oDocument, Guid guidViewType) {
            if( oDocument is MonitorDocument oMonitorDoc ) {
			    try {
                    if( guidViewType == BasicLineWindow.GUID )
					    return new BasicLineWindow( oViewSite, oMonitorDoc );
                    if( guidViewType == DumpWindow )
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
            yield return new ViewType( "BBC Basic Text", BasicLineWindow.GUID );
        }
    }
}