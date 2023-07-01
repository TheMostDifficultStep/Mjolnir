using System.Collections;
using System.Collections.Generic;

using Play.Interfaces.Embedding;
using Play.Forms;

using SkiaSharp;

namespace Monitor {
    public class MonitorController : Controller {
        public MonitorController() {
            _rgExtensions.Add( ".asm" );
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

    public enum StatusReg : int {
        Negative,
        Zero,
        Carry,
        Overflow
    }

    public enum AddrModes {
        Acc, // Non-Indexed,non memory
        Imm,
        Imp,

        Rel, // Non-Indexed memory ops
        Abs,
        Zpg,
        Ind,

        Abs_Indx, // Indexed memory ops
        Zpg_Indx,
        Indx_Indr,
        Indr_Indx
    }

    public class AsmInstruction {
        public readonly string    _strInst;
        public readonly AddrModes _eMode;
        public readonly bool      _fIndexed;
        public readonly bool      _fMemory;

        public AsmInstruction( string strInst, AddrModes eMode, bool fIndexed, bool fMemory ) {
            _strInst  = strInst;
            _eMode    = eMode;
            _fIndexed = fIndexed;
            _fMemory  = fMemory;
        }

        public override string ToString() {
            return _strInst + '-' + _eMode.ToString();
        }
    }

    public class CpuProperties : DocProperties {
        public enum Properties : int {
            Status,
            Overflow_Bit,
            Carry_Bit, 
            Zero_Bit, 
            Negative_Bit,
            Register_0,
            Register_1,
            Register_2,
            Register_3,
            Stack_Pointer,
            Program_Counter
        }

        public CpuProperties( IPgBaseSite oSiteBase ) : base( oSiteBase ) {
        }

        public override bool InitNew() {
            if( !base.InitNew() ) 
                return false;
            
            // Set up the parser so we get spiffy colorization on our text!! HOWEVER,
            // Some lines are not sending events to the Property_Values document and we
            // need to trigger the parse seperately.
            // new ParseHandlerText( Property_Values, "text" );

            if( _oSiteBase.Host is not MonitorDocument oMonitorDoc )
                return false;

            // Set up our basic list of values.
            foreach( Properties eName in Enum.GetValues(typeof(Properties)) ) {
                CreatePropertyPair( eName.ToString() );
            }

            // Set up human readable labels.
            LabelUpdate( (int)Properties.Register_0,      "0" );
            LabelUpdate( (int)Properties.Register_1,      "1" );
            LabelUpdate( (int)Properties.Register_2,      "2" ); // Readibility, strength, video
            LabelUpdate( (int)Properties.Register_3,      "3" );
            LabelUpdate( (int)Properties.Status,          "Status", SKColors.LightGreen );
            LabelUpdate( (int)Properties.Negative_Bit,    "Negative" );
            LabelUpdate( (int)Properties.Zero_Bit,        "Zero" );
            LabelUpdate( (int)Properties.Carry_Bit,       "Carry" );
            LabelUpdate( (int)Properties.Overflow_Bit,    "Overflow" );
            LabelUpdate( (int)Properties.Stack_Pointer,   "Stack" );
            LabelUpdate( (int)Properties.Program_Counter, "PC" );

            // Put some initial values if needed here... :-/
            ValueUpdate( (int)Properties.Program_Counter, "0" );

            return true;
        }
    }
}