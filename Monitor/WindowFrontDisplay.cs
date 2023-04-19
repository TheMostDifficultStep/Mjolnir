using System.Xml;
using System.Windows.Forms;

using Play.Interfaces.Embedding;
using Play.Forms;
using Play.Rectangles;
using Play.Edit;

using SkiaSharp;
using SkiaSharp.Views.Desktop;


namespace Monitor {
    public class PropertyWindow : WindowStandardProperties {
        public PropertyWindow( IPgViewSite oViewSite, CpuProperties oPropDoc ) : base( oViewSite, oPropDoc ) {
        }
    }

    public class AssemblyWindow : EditWindow2 {
        public class FTCacheLineNumber : FTCacheWrap {
            Line _oHost;
            int  _iAt;
            public FTCacheLineNumber( Line oLine, Line oHost ) : base( oLine ) {
                _oHost = oHost ?? throw new ArgumentNullException( nameof( oHost ) );
            }

            public override void Update(IPgFontRender oFR) {
                _iAt = _oHost.At;

                Line.Empty();
                Line.TryAppend( _oHost.At.ToString() );

                base.Update(oFR);
            }

            public override bool IsInvalid { get => _iAt != _oHost.At; }
        }
        public class CacheManagerAsm : CacheManager2 {
            public CacheManagerAsm( CacheManagerAbstractSite oSite, IPgFontRender oFont, List<SmartRect> rgCols ) :
                base( oSite, oFont, rgCols ) {
            }

            protected override CacheRow CreateRow( Line oLine ) {
                CacheRow oRow = base.CreateRow( oLine );

                FTCacheLine oElem = new FTCacheLineNumber( new TextLine( 0, string.Empty ), oLine ); 

                ElemUpdate2( oElem, _rgColumns[1].Width );

                oRow.CacheList.Add( oElem );

                return oRow;
            }
        }

        protected readonly LayoutRect _rctLabelColumns = new LayoutRect( LayoutRect.CSS.Flex ) { Track = 30 };

        public AssemblyWindow( IPgViewSite oSite, Editor oEdit ) : base( oSite, oEdit ) {
        }

        protected override void InitColumns() {
            _oLayout  .Add( _rctLabelColumns );
            _oLayout  .Add( _rctTextArea );   // Main text area.

            _rgColumns.Add( _rctTextArea );
            _rgColumns.Add( _rctLabelColumns );   
        }

        protected override CacheManager2 CreateCacheManager(uint uiStdText) {
            return new CacheManagerAsm( new CacheManSlot(this),
                                        _oStdUI.FontRendererAt(uiStdText),
                                        _rgColumns );
        }
    }

    internal class WindowFrontPanel : SKControl,
        IPgParent,
        IPgCommandView,
        IPgLoad<XmlElement>,
        IPgSave<XmlDocumentFragment>
    {
        protected MonitorDocument       MonitorDoc { get; }
        protected LayoutStackHorizontal Layout     { get; } = new LayoutStackHorizontal();
        protected EditWindow2           WinCommand { get; }
        protected EditWindow2           WinAssembly { get; }

        public IPgParent Parentage => _oSiteView.Host;
        public IPgParent Services  => Parentage.Services;

        protected IPgViewSite _oSiteView;

        public string Banner => "Nibble Monitor";

        public SKBitmap Icon => null;

        public Guid Catagory => Guid.Empty;

        public bool  IsDirty => false;

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

        /// <remarks>So turns out having seperate documents containing the various elements
        /// we want to display results in a bug in the main form window trying to manage the
        /// visual elements. Plus, undo is a nightmare. So punt on that and using a single
        /// property doc, but create outboard collections of the registers and status bits.</remarks>
        /// <exception cref="ArgumentNullException"></exception>
        public WindowFrontPanel( IPgViewSite oViewSite, MonitorDocument oMonitorDoc ) 
        {
            _oSiteView = oViewSite   ?? throw new ArgumentNullException();
            MonitorDoc = oMonitorDoc ?? throw new ArgumentNullException( "Monitor document must not be null!" );

            WinCommand  = new AssemblyWindow( new ViewSlot( this ), oMonitorDoc.TextCommands ) { Parent = this };
            WinAssembly = new AssemblyWindow( new ViewSlot( this ), oMonitorDoc.AssemblyDoc  ) { Parent = this };
        }

        public virtual bool InitNew() {
            if( !WinCommand.InitNew() )
                return false;

            if( !WinAssembly.InitNew() )
                return false;

            // Add the memory window and assembly.
            Layout.Add( new LayoutControl( WinCommand,  LayoutRect.CSS.Percent ) { Track = 30 } );
            Layout.Add( new LayoutControl( WinAssembly, LayoutRect.CSS.Percent ) { Track = 70 } );

            OnSizeChanged( new EventArgs() );

            MonitorDoc.RefreshScreen += OnRefreshScreen_MonDoc;
            return true;
        }

        protected override void OnSizeChanged(EventArgs e) {
			base.OnSizeChanged(e);
            if( Width > 0 && Height > 0 ) {
			    Layout.SetRect( 0, 0, Width, Height );
			    Layout.LayoutChildren();
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
			if( sGuid.Equals(GlobalDecorations.Properties) ) {
                return new PropertyWindow( oBaseSite, this.MonitorDoc.Properties );
            }
            return null;
        }

        public bool Save(XmlDocumentFragment oStream) {
            return true;
        }
    }
}
