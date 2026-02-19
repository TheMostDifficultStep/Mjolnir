using System.Xml;
using System.Windows.Forms;

using Play.Interfaces.Embedding;
using Play.Forms;
using Play.Rectangles;
using Play.Edit;

using SkiaSharp;
using SkiaSharp.Views.Desktop;


namespace Monitor {
    public enum StatusReg : int {
        Negative,
        Zero,
        Carry,
        Overflow
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

            if( _oSiteBase.Host is not Old_CPU_Emulator oMonitorDoc )
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

    public class PropertyWindow : WindowStandardProperties {
        public PropertyWindow( IPgViewSite oViewSite, CpuProperties oPropDoc ) : base( oViewSite, oPropDoc ) {
        }
    }

    internal class WindowFrontPanel : SKControl,
        IPgParent,
        IPgCommandView,
        IPgLoad<XmlElement>,
        IPgSave<XmlDocumentFragment>
    {
        public static Guid GUID { get; } = new Guid( "{A28DDC95-EE48-4426-9D15-0B29F07D5F4A}" );

        protected Old_CPU_Emulator      MonitorDoc { get; }
        protected LayoutStackHorizontal MyLayout   { get; } = new LayoutStackHorizontal();
        protected EditWindow2           WinCommand { get; } // machine code..

        public IPgParent Parentage => _oSiteView.Host;
        public IPgParent Services  => Parentage.Services;

        protected IPgViewSite _oSiteView;

        public string Banner => "Nibble Monitor";

        public SKImage Icon => null;

        public Guid Catagory => Guid.Empty;

        public bool  IsDirty => MonitorDoc.IsDirty;

        protected class DocSlot :
			IPgBaseSite
		{
			protected readonly WindowFrontPanel _oHost;

			public DocSlot( WindowFrontPanel oHost ) {
				_oHost = oHost ?? throw new ArgumentNullException();
			}

			public IPgParent Host => _oHost;

			public void LogError(string strMessage, string strDetails, bool fShow=true) {
				_oHost._oSiteView.LogError( strMessage, strDetails, fShow );
			}

			public void Notify( ShellNotify eEvent ) {
			}
		} // End class

		protected class ViewSlot : DocSlot, IPgViewSite {
			public ViewSlot( WindowFrontPanel oHost ) : base( oHost ) {
			}

			public IPgViewNotify EventChain => _oHost._oSiteView.EventChain;
		}

        /// <remarks>This is the display for the assembly emulator. I used to share
        /// the screen with the basic display, but I've removed that. Just keeping
        /// this around for history at the moment.</remarks>
        public WindowFrontPanel( IPgViewSite oViewSite, Old_CPU_Emulator oMonitorDoc ) 
        {
            _oSiteView = oViewSite   ?? throw new ArgumentNullException();
            MonitorDoc = oMonitorDoc ?? throw new ArgumentNullException( "Monitor document must not be null!" );

            WinCommand  = new LineNumberWindow( new ViewSlot( this ), oMonitorDoc.TextCommands ) { Parent = this };
        }

        public virtual bool InitNew() {
            if( !WinCommand.InitNew() )
                return false;

            // Add the memory window and assembly.
            MyLayout.Add( new LayoutControl( WinCommand,  LayoutRect.CSS.Percent ) { Track = 30 } );

            WinCommand .Parent = this;

            OnSizeChanged( new EventArgs() );

            MonitorDoc.RefreshScreen += OnRefreshScreen_MonDoc;
            return true;
        }

        protected override void OnSizeChanged(EventArgs e) {
			base.OnSizeChanged(e);
            if( Width > 0 && Height > 0 ) {
			    MyLayout.SetRect( 0, 0, Width, Height );
			    MyLayout.LayoutChildren();
            }
        }

        // this isn't going to get called...
        protected override void OnKeyDown(KeyEventArgs e) {
            base.OnKeyDown(e);

            if( e.KeyCode == Keys.F2 ) {
                MonitorDoc.CompileAsm();
            }
        }

        public bool Load(XmlElement oStream) {
            return InitNew();
        }

        protected override void Dispose(bool disposing) {
            if( disposing ) {
                MonitorDoc.RefreshScreen -= OnRefreshScreen_MonDoc;
            }
            base.Dispose(disposing);
        }
        private void OnRefreshScreen_MonDoc(int obj) {
            Invalidate();
        }

        public bool Execute(Guid sGuid) {
            if( sGuid == GlobalCommands.Play ) {
                MonitorDoc.ProgramRun();
                return true;
            }
            if( sGuid == GlobalCommands.JumpNext ) {
                MonitorDoc.ProgramRun( fNotStep:false );
                return true;
            }
            if( sGuid == GlobalCommands.JumpParent ) {
                MonitorDoc.ProgramReset();
                return true;
            }
            return false;
        }

        public object Decorate(IPgViewSite oBaseSite, Guid sGuid) {
			if( sGuid.Equals(GlobalDecor.Properties) ) {
                return new PropertyWindow( oBaseSite, this.MonitorDoc.Properties );
            }
            return null;
        }

        public bool Save(XmlDocumentFragment oStream) {
            return true;
        }
    }
}
