﻿using Play.Interfaces.Embedding;

using Play.Edit;
using Play.ImageViewer;

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
            _rgExtensions.Add( ".asmprg" );
        }

        protected static Guid _gViewDazzle = new Guid( "{6F5EAD43-B191-404F-BC5D-F108FEB68205}" );

        public override IDisposable CreateView(IPgViewSite oViewSite, object oDocument, Guid guidViewType) {
            if( oDocument is Document_Monitor oMonitorDoc ) {
			    try {
                    if( guidViewType == ViewProgramDisplay.GUID )
                        return new ViewProgramDisplay( oViewSite, oMonitorDoc );
                    if( guidViewType == _gViewDazzle )
                        return new WindowSoloImage( oViewSite, oMonitorDoc.Doc_Display );

                    // Service the GUID.Empty case too.
                    return new ViewProgramDisplay( oViewSite, oMonitorDoc );
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
            yield return new ViewType( "Assembly Display", ViewProgramDisplay.GUID );
            yield return new ViewType( "Image Display", _gViewDazzle );
        }
        public override IDisposable CreateDocument(IPgBaseSite oSite, string strExtension) {
            return new Document_Monitor( oSite );
        }
    }

}