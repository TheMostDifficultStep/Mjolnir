using System;
using System.Collections.Generic;

using Play.Interfaces.Embedding;
using Play.Edit; 
using Play.Forms;
using System.Runtime.CompilerServices;
using SkiaSharp;

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
        protected int _iCurrent = 0; // We'll juse the program counter in the future.
        public bool IsDirty => false;

        public IPgParent Parentage => _oBaseSite.Host;
        public IPgParent Services  => Parentage.Services;

        public Editor        TextCommands { get; }
        public DocProperties FrontDisplay { get; }
        public List<Line>    AddrLine     { get; } = new();
        public List<Line>    DataLine     { get; } = new();
        public Editor        LablEdit     { get; }
        public List<List<Line>>    Registers    { get; } = new();

        public event Action<int> RefreshScreen;

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

            for( int i=0; i<4; ++i ) {
                Registers.Add( new List<Line>() );
            }
        }

        // See ca 1816 warning.
        public void Dispose() {
        }

        public bool InitNew() {
            if( !TextCommands.InitNew() )
                return false;
            if( !FrontDisplay.InitNew() )
                return false;

            if( !LablEdit.InitNew() )
                return false;

            //for( int i=0; i<16; ++i ) {
            //    TextCommands.LineAppend( "load" );
            //    TextCommands.LineAppend( "0" );
            //    TextCommands.LineAppend( i.ToString() );
            //}

            TextCommands.LineAppend( "load" );
            TextCommands.LineAppend( "0" );
            TextCommands.LineAppend( "5" );

            TextCommands.LineAppend( "load" );
            TextCommands.LineAppend( "1" );
            TextCommands.LineAppend( "3");

            TextCommands.LineAppend( "add");

            Editor PropValues = FrontDisplay.Property_Values;
            for( int i = 0; i<4; ++i ) {
                DataLine.Add( PropValues.LineAppend( "0", fUndoable:false ) );
            }
            for( int i = 0; i<8; ++i ) {
                AddrLine.Add( PropValues.LineAppend( "0", fUndoable:false ) );
            }
            for( int iRegister = 0; iRegister < Registers.Count; ++iRegister ) {
                for( int i = 0; i<4; ++i ) {
                    Registers[iRegister].Add( PropValues.LineAppend( "0", fUndoable:false ) );
                }
            }

            LablEdit.LineAppend( "Data",    fUndoable:false ); // 0
            LablEdit.LineAppend( "...",     fUndoable:false );
            LablEdit.LineAppend( "Address", fUndoable:false );
            LablEdit.LineAppend( "",        fUndoable:false );
            LablEdit.LineAppend( "High",    fUndoable:false );
            LablEdit.LineAppend( "Low",     fUndoable:false );

            LablEdit.LineAppend( "Register 0", fUndoable:false ); // 6
            LablEdit.LineAppend( "Register 1", fUndoable:false );
            LablEdit.LineAppend( "Register 2", fUndoable:false );
            LablEdit.LineAppend( "Register 3", fUndoable:false );

            return true;
        }

        public bool Save(TextWriter oStream) {
            return true;
        }

        public bool Load(TextReader oStream) {
            throw new NotImplementedException();
        }

        protected void RegisterLoad( int iRegister, string strData ) {
            List<Line> oRegister = Registers[iRegister];
            if( int.TryParse( strData, out int iData ) ) {
                for( int i=0; i<4; ++i ) {
                    Line oBit = oRegister[i];
                    bool bBit = ( iData & 1 ) > 0;

                    oBit.Empty();
                    oBit.TryAppend( bBit.ToString() );

                    iData = iData >> 1;
                }
                if( iData > 0 )
                    _oBaseSite.LogError( "Overflow", "register " + iRegister.ToString() );
            }
        }

        protected int RegisterRead( int iRegister ) {
            int        iData     = 0;
            List<Line> oRegister = Registers[iRegister];

            for( int i =0; i<oRegister.Count; ++i ) {
                if( oRegister[i].CompareTo( "True" ) == 0 )
                    iData += (int)Math.Pow( 2, i );
            }

            return iData;
        }

        public void ProgramReset() {
            _iCurrent = 0;
        }

        public void ProgramRun( bool fNotStep = true ) {
            try {
                if( _iCurrent >= TextCommands.ElementCount ) {
                    _oBaseSite.LogError( "Execution", "Program finished" );
                    return;
                }

                do {
                    Line oInst = TextCommands[_iCurrent];
                    if( oInst.CompareTo( "load" ) == 0 ) {
                        int    iRegister = int.Parse( TextCommands[++_iCurrent].ToString() );
                        string strData   = TextCommands[++_iCurrent].ToString();

                        RegisterLoad( iRegister, strData );
                        RefreshScreen(0);
                    } else
                    if( oInst.CompareTo( "add" ) == 0 ) {
                        int iA   = RegisterRead( 0 );
                        int iB   = RegisterRead( 1 );
                        int iSum = iA + iB;

                        RegisterLoad( 2, iSum.ToString() );
                        RefreshScreen(0);
                    }
                } while( ++_iCurrent < TextCommands.ElementCount && fNotStep );
            } catch( Exception oEx ) {
                Type[] rgErrors = { typeof( NullReferenceException ),
                                    typeof( ArgumentNullException ), 
                                    typeof( FormatException ), 
                                    typeof( OverflowException ) };
                if( rgErrors.IsUnhandled( oEx ) )
                    throw;
            }
        }

    }
}