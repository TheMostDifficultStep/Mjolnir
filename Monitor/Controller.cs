using System;
using System.Collections.Generic;

using Play.Interfaces.Embedding;
using Play.Edit; 
using Play.Forms;

namespace Monitor {
    public class MonitorController : Controller {
        public MonitorController() {
            _rgExtensions.Add( ".nibble" );
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
        protected Dictionary< string, CPU_Instructions> _dctInstructions = new();
        public bool IsDirty => false;

        public IPgParent Parentage => _oBaseSite.Host;
        public IPgParent Services  => Parentage.Services;

        public Editor        TextCommands { get; }
        public DocProperties FrontDisplay { get; }
        public List<Line>    AddrLine     { get; } = new();
        public List<Line>    DataLine     { get; } = new();
        public Editor        LablEdit     { get; }
        public List<Line>    Registers    { get; } = new();

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

        public enum CPU_Instructions {
            load_Imm, load_abs, save, add, halt
        }

        public class ProgramFile : Editor {
            public ProgramFile( IPgBaseSite oBaseSite ) : base( oBaseSite ) { }

            public override WorkerStatus PlayStatus => ( _oSiteBase.Host as MonitorDocument ).PlayStatus;
        }

        public WorkerStatus PlayStatus { 
            get { 
                if( _iCurrent == 0 )
                    return WorkerStatus.FREE; 
                if( _iCurrent < TextCommands.ElementCount )
                    return WorkerStatus.BUSY; 

                return WorkerStatus.PAUSED;
            } 
        }
        public MonitorDocument( IPgBaseSite oSite ) {
            _oBaseSite = oSite ?? throw new ArgumentNullException( "Site to document must not be null." );

            TextCommands = new ProgramFile  ( new DocSlot( this ) );
            FrontDisplay = new DocProperties( new DocSlot( this ) );
            LablEdit     = new Editor       ( new DocSlot( this ) );

            _dctInstructions.Add( "load-imm", CPU_Instructions.load_Imm );
            _dctInstructions.Add( "load-abs", CPU_Instructions.load_abs );
            _dctInstructions.Add( "add",      CPU_Instructions.add );
            _dctInstructions.Add( "save",     CPU_Instructions.save );
            _dctInstructions.Add( "halt",     CPU_Instructions.halt );
        }

        // See ca 1816 warning.
        public void Dispose() {
        }

        public bool Initialize() {
            if( !FrontDisplay.InitNew() )
                return false;

            if( !LablEdit.InitNew() )
                return false;

            Editor PropValues = FrontDisplay.Property_Values;
            for( int i = 0; i<4; ++i ) {
                DataLine.Add( PropValues.LineAppend( "0", fUndoable:false ) );
            }
            for( int i = 0; i<8; ++i ) {
                AddrLine.Add( PropValues.LineAppend( "0", fUndoable:false ) );
            }
            for( int iRegister = 0; iRegister < Registers.Count; ++iRegister ) {
                Registers[iRegister] = PropValues.LineAppend( "0", fUndoable:false );
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

        public bool InitNew() {
            if( !TextCommands.InitNew() )
                return false;

            if( !Initialize() ) 
                return false;

            return true;
        }

        public bool Save(TextWriter oStream) {
            return TextCommands.Save( oStream );
        }

        public bool Load(TextReader oStream) {
            if( !TextCommands.Load( oStream ) )
                return false;

            if( !Initialize() ) 
                return false;

            return true;
        }

        protected void RegisterLoad( int iRegister, string strData ) {
            Line oRegister = Registers[iRegister];
            if( int.TryParse( strData, out int iData ) ) {
                oRegister.Empty();
                oRegister.TryAppend( strData );
                return;
            }
            _oBaseSite.LogError( "Program", "Attempted load of Illegal value to register" );
            throw new InvalidOperationException();
        }

        protected int RegisterRead( int iRegister ) {
            Line oRegister = Registers[iRegister];

            if( int.TryParse( oRegister.ToString(), out int iData ) ) {
                return iData;
            }

            _oBaseSite.LogError( "Program", "Illegal value in register" );
            throw new InvalidOperationException();
        }

        // this won't run, just keeping it for reference.
        protected void Old_RegisterLoad( int iRegister, string strData ) {
            List<Line> oRegister = new(); // = Registers[iRegister];
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

        // this won't run, just keeping it for reference.
        protected int Old_RegisterRead( int iRegister ) {
            int        iData     = 0;
            List<Line> oRegister = new(); // Registers[iRegister];

            for( int i =0; i<oRegister.Count; ++i ) {
                if( oRegister[i].CompareTo( "True" ) == 0 )
                    iData += (int)Math.Pow( 2, i );
            }

            return iData;
        }
        public void ProgramReset() {
            _iCurrent = 0;
            TextCommands.Raise_BufferEvent( BUFFEREVENTS.FORMATTED );
        }

        public void ProgramRun( bool fNotStep = true ) {
            try {
                if( _iCurrent >= TextCommands.ElementCount ) {
                    _oBaseSite.LogError( "Execution", "Program finished" );
                    return;
                }

                do {
                    Line oInst = TextCommands[_iCurrent];
                    if( !_dctInstructions.TryGetValue( oInst.ToString(), out CPU_Instructions eInst ) ) {
                        _oBaseSite.LogError( "Execution", "Illegal instruction" );
                        return;
                    }

                    TextCommands.HighLight = TextCommands[_iCurrent];

                    switch( eInst ) {
                        case CPU_Instructions.load_Imm: {
                            int    iRegister = int.Parse( TextCommands[++_iCurrent].ToString() );
                            string strData   = TextCommands[++_iCurrent].ToString();

                            RegisterLoad( iRegister, strData );
                            RefreshScreen(0);
                        } break;
                        case CPU_Instructions.add:
                            int iA   = RegisterRead( 0 );
                            int iB   = RegisterRead( 1 );
                            int iSum = iA + iB;

                            RegisterLoad( 2, iSum.ToString() );
                            RefreshScreen(0);
                            break;
                        case CPU_Instructions.load_abs: {
                            int    iRegister = int.Parse( TextCommands[++_iCurrent].ToString() );
                            string strAddr   = TextCommands[++_iCurrent].ToString();
                            if( int.TryParse( strAddr, out int iAddr ) ) {
                                string strData = TextCommands[iAddr].ToString();
                                RegisterLoad( iRegister, strData );
                            }
                            RefreshScreen(0);
                        } break;
                        case CPU_Instructions.halt:
                            _iCurrent = TextCommands.ElementCount;
                            break;
                        case CPU_Instructions.save: {
                            int iRegister = int.Parse( TextCommands[++_iCurrent].ToString() );
                            int iAddress  = int.Parse( TextCommands[++_iCurrent].ToString() );
                            int iData     = RegisterRead( iRegister );
                            using( Editor.Manipulator oBulk = TextCommands.CreateManipulator() ) {
                                string strData = iData.ToString();
                                oBulk.LineTextDelete( iAddress, null );
                                oBulk.LineTextInsert( iAddress, 0, strData, 0, strData.Length );
                            }
                            RefreshScreen(0);
                        } break;
                    }
                } while( ++_iCurrent < TextCommands.ElementCount && fNotStep );
            } catch( Exception oEx ) {
                Type[] rgErrors = { typeof( NullReferenceException ),
                                    typeof( ArgumentNullException ), 
                                    typeof( FormatException ), 
                                    typeof( OverflowException ),
                                    typeof( InvalidOperationException ) };
                if( rgErrors.IsUnhandled( oEx ) )
                    throw;
            }
        }

    }
}