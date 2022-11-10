using System;
using System.Collections.Generic;

using Play.Interfaces.Embedding;
using Play.Edit; 
using Play.Forms;

namespace Monitor {
    public class MonitorController : Controller {
        public MonitorController() {
            _rgExtensions.Add( ".fourbit" );
        }
        public override IDisposable CreateDocument(IPgBaseSite oSite, string strExtension) {
            return new MonitorDocument( oSite );
        }

        public override IDisposable CreateView(IPgViewSite oViewSite, object oDocument, Guid guidViewType) {
            if( oDocument is MonitorDocument oMonitorDoc ) {
			    try {
				    if( guidViewType == Guid.Empty )
					    return new WindowFrontPanel( oViewSite, oMonitorDoc );

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
 	        yield return new ViewType( "MainView", Guid.Empty );
        }
    }

    public class MonitorDocument :
        IPgParent,
		IDisposable,
        IPgLoad<TextReader>,
        IPgSave<TextWriter>
    {
        protected readonly IPgBaseSite _oBaseSite;
        public bool IsDirty => false;

        public IPgParent Parentage => _oBaseSite.Host;
        public IPgParent Services  => Parentage.Services;

        public Editor        TextCommands { get; }
        public DocProperties FrontDisplay { get; }
        public Editor        AddrEdit     { get; }
        public Editor        DataEdit     { get; }
        public Editor        LablEdit     { get; }

        public class DocSlot :
            IPgBaseSite
        {
            protected readonly MonitorDocument _oHost;

            public DocSlot( MonitorDocument oHost ) {
                _oHost = oHost;
            }
            public IPgParent Host => _oHost;

            public void LogError(string strMessage, string strDetails, bool fShow = true) {
                _oHost._oBaseSite.LogError(strMessage, strDetails, fShow);
            }

            public void Notify(ShellNotify eEvent) {
                _oHost._oBaseSite.Notify( eEvent );
            }
        }

        public MonitorDocument( IPgBaseSite oSite ) {
            _oBaseSite = oSite ?? throw new ArgumentNullException( "Site to document must not be null." );

            TextCommands = new Editor       ( new DocSlot( this ) );
            FrontDisplay = new DocProperties( new DocSlot( this ) );
            LablEdit     = new Editor       ( new DocSlot( this ) );
            AddrEdit     = new Editor       ( new DocSlot( this ) );
            DataEdit     = new Editor       ( new DocSlot( this ) );
        }

        // See ca 1816 warning.
        public void Dispose() {
        }

        public bool InitNew() {
            if( !TextCommands.InitNew() )
                return false;
            if( !FrontDisplay.InitNew() )
                return false;

            if( !AddrEdit.InitNew() )
                return false;
            if( !DataEdit.InitNew() )
                return false;
            if( !LablEdit.InitNew() )
                return false;

            TextCommands.LineAppend( "Hello" );
            TextCommands.LineAppend( "Exit" );

            Editor PropValues = FrontDisplay.Property_Values;
            for( int i = 0; i<4; ++i ) {
                AddrEdit.LineAppend( "0", fUndoable:false );
            }
            for( int i = 0; i<8; ++i ) {
                DataEdit.LineAppend( "0", fUndoable:false );
            }

            LablEdit.LineAppend( "Data",    fUndoable:false );
            LablEdit.LineAppend( "...",     fUndoable:false );
            LablEdit.LineAppend( "Address", fUndoable:false );
            LablEdit.LineAppend( "",        fUndoable:false );
            LablEdit.LineAppend( "High",    fUndoable:false );
            LablEdit.LineAppend( "Low",     fUndoable:false );

            return true;
        }

        public bool Save(TextWriter oStream) {
            return true;
        }

        public bool Load(TextReader oStream) {
            throw new NotImplementedException();
        }
    }
}