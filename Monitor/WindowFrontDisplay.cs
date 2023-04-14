using System.Xml;
using System.Windows.Forms;

using Play.Interfaces.Embedding;
using Play.Forms;
using Play.Rectangles;
using Play.Edit;

using SkiaSharp;

namespace Monitor {
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

    internal class WindowFrontPanel : FormsWindow,
        IPgParent,
        IPgCommandView,
        IPgLoad<XmlElement>,
        IPgSave<XmlDocumentFragment>
    {
        protected MonitorDocument     MonitorDoc { get; }
        protected SmartTable          Blinken    { get; }
        protected LayoutStackVertical VertStack  { get; }
        protected EditWindow2         WinCommand { get; }
        protected EditWindow2         WinAssembly { get; }

        public IPgParent Parentage => _oSiteView.Host;
        public IPgParent Services  => Parentage.Services;

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

        /// <remarks>So this is an interesting case for our forms object. I would like the
        /// data and address lines to be two seperate text editors. The form which we 
        /// derive from really only understands one editor of editable elements. It comes
        /// down to who gets edit events. And how would undo work. It seems pretty
        /// special case and so I'll probably split the FormsWindow object for this.</remarks>
        /// <exception cref="ArgumentNullException"></exception>
        public WindowFrontPanel( IPgViewSite oViewSite, MonitorDocument oMonitorDoc ) : 
            base( oViewSite, oMonitorDoc.FrontDisplay.PropertyDoc ) 
        {
            MonitorDoc = oMonitorDoc ?? throw new ArgumentNullException( "Monitor document must not be null!" );

            VertStack  = new LayoutStackVertical();
            Layout     = VertStack;

            WinCommand  = new AssemblyWindow( new ViewSlot( this ), oMonitorDoc.TextCommands ) { Parent = this };
            WinAssembly = new AssemblyWindow( new ViewSlot( this ), oMonitorDoc.AssemblyDoc  ) { Parent = this };
            Blinken     = new SmartTable( 5, LayoutRect.CSS.Percent ) { Track = 40 };
        }

        public override bool InitNew() {
            if( !base.InitNew() )
                return false;

            if( !WinCommand.InitNew() )
                return false;

            if( !WinAssembly.InitNew() )
                return false;

            // First, add the columns to our table.
			Blinken.Add( new LayoutRect( LayoutRect.CSS.Flex ) );
			Blinken.Add( new LayoutRect( LayoutRect.CSS.Pixels, 60, .20f ) );
			Blinken.Add( new LayoutRect( LayoutRect.CSS.Pixels, 60, .20f ) );
			Blinken.Add( new LayoutRect( LayoutRect.CSS.Pixels, 60, .20f ) );
			Blinken.Add( new LayoutRect( LayoutRect.CSS.Pixels, 60, .20f ) );
			//Blinken.Add( new LayoutRect( LayoutRect.CSS.None ) );

            Editor oLabels = MonitorDoc.LablEdit;

            // Status lights top labels...
            List<LayoutRect> rgStatusLabel = new();
            rgStatusLabel.Add( new LayoutSingleLine( new FTCacheLine( oLabels[3] ), LayoutRect.CSS.Flex ) /* { Span = 4 } */ );

            rgStatusLabel.Add( new LayoutSingleLine( new FTCacheLine( oLabels[15] ), LayoutRect.CSS.Flex ) );
            rgStatusLabel.Add( new LayoutSingleLine( new FTCacheLine( oLabels[14] ), LayoutRect.CSS.Flex ) );
            rgStatusLabel.Add( new LayoutSingleLine( new FTCacheLine( oLabels[13] ), LayoutRect.CSS.Flex ) );
            rgStatusLabel.Add( new LayoutSingleLine( new FTCacheLine( oLabels[12] ), LayoutRect.CSS.Flex ) ); // N
            foreach( LayoutRect oRect in rgStatusLabel ) {
                if( oRect is LayoutSingleLine oSingle ) {
                    CacheList.Add( oSingle );
                }
            }

            // Status Label for the blinken lights row.
            List<LayoutRect> rgStatusLayout = new();
            rgStatusLayout.Add( new LayoutSingleLine( new FTCacheLine( oLabels[2] ), LayoutRect.CSS.Flex )  );

            //rgStatusLayout.Add( new LayoutSingleLine( new FTCacheLine( oLabels[3] ), LayoutRect.CSS.Flex ) { Span = 3 } );
            // Status Values blinken lights.
            for( int i=0; i<4; ++i ) {
                rgStatusLayout.Add( new LayoutSingleLine( new FTCacheLine( MonitorDoc.StatusLine[i] ), LayoutRect.CSS.Flex ) );
            }
            foreach( LayoutRect oRect in rgStatusLayout ) {
                if( oRect is LayoutSingleLine oSingle ) {
                    CacheList.Add( oSingle );
                }
            }

            Blinken.AddRow( rgStatusLabel );
            Blinken.AddRow( rgStatusLayout );

            List<LayoutRect> rgBlankLine = new();
            rgBlankLine.Add( new LayoutSingleLine( new FTCacheLine( oLabels[3] ), LayoutRect.CSS.Flex )  );
            foreach( LayoutRect oRect in rgBlankLine ) {
                if( oRect is LayoutSingleLine oSingle ) {
                    CacheList.Add( oSingle );
                }
            }

            Blinken.AddRow( rgBlankLine );

            // Stuff the registers onto the same amount of blinken lines.
            for( int i=0; i< MonitorDoc.Registers.Count; ++i ) {
                List<LayoutRect> rgLayout  = new();
                Line             oRegister = MonitorDoc.Registers[i];
                Line             oLabel    = MonitorDoc.LablEdit[i+6]; // I forgot why I'm not using the property page labels.

                LayoutSingleLine oLayName = new LayoutSingleLine( new FTCacheLine( oLabel ), LayoutRect.CSS.Flex );
                rgLayout .Add( oLayName );
                CacheList.Add( oLayName );

                LayoutSingleLine oLayLine = new LayoutSingleLine( new FTCacheLine( oRegister ), LayoutRect.CSS.Flex );
                rgLayout .Add( oLayLine );
                CacheList.Add( oLayLine );

                Blinken.AddRow( rgLayout );
            }

            // Add the memory window and assembly.
            LayoutStackHorizontal oHoriz = new( ) { Track = 60, Units = LayoutRect.CSS.Percent };

            oHoriz.Add( new LayoutControl( WinCommand,  LayoutRect.CSS.Percent ) { Track = 20 } );
            oHoriz.Add( new LayoutControl( WinAssembly, LayoutRect.CSS.Percent ) { Track = 80 } );

            // complete final layout of table and command window.
            VertStack.Add( Blinken );
            VertStack.Add( oHoriz  );

            OnDocumentEvent( BUFFEREVENTS.MULTILINE );
//          OnSizeChanged( new EventArgs() );

            MonitorDoc.RefreshScreen += OnRefreshScreen_MonDoc;
            return true;
        }

        protected override void OnSizeChanged(EventArgs e) {
            if( Width > 0 && Height > 0 ) {
                base.OnSizeChanged(e);
            }
        }

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
            OnDocumentEvent(BUFFEREVENTS.MULTILINE );
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
            return null;
        }

        public bool Save(XmlDocumentFragment oStream) {
            return true;
        }
    }
}
