using System.Xml;
using System.Windows.Forms;

using Play.Interfaces.Embedding;
using Play.Forms;
using Play.Rectangles;
using Play.Edit;
using Play.Parse;
using Play.Parse.Impl;

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

    public class PropertyWindow : WindowStandardProperties {
        public PropertyWindow( IPgViewSite oViewSite, CpuProperties oPropDoc ) : base( oViewSite, oPropDoc ) {
        }
    }


    public class NumberLabelWindow : LineNumberWindow {
        public class CacheManagerLabel : CacheManagerAsm {
            List<IMemoryRange> _rgColumnOffsets = new();

            public CacheManagerLabel( CacheManagerAbstractSite oSite, IPgFontRender oFont, List<SmartRect> rgCols ) :
                base( oSite, oFont, rgCols ) {

                for( int i=0; i<4; ++i ) {
                    _rgColumnOffsets.Add( new ColorRange( 0, 0, 0 ) );
                }
            }

            protected override CacheRow CreateRow( Line oLine ) {
                CacheRow oRow = base.CreateRow( oLine );

                FTCacheLine oElem = new FTCacheWrap( oLine ); 

                oRow.CacheList.Add( oElem );

                return oRow;
            }

            protected void ElemUpdate3( FTCacheLine oElem, int iWidth, IMemoryRange oRange ) {
			    try {
				    oElem.Update            ( Font, oRange );
                    oElem.OnChangeFormatting( null );
                    oElem.OnChangeSize      ( iWidth );
			    } catch( Exception oEx ) {
				    Type[] rgErrors = { typeof( NullReferenceException ),
									    typeof( ArgumentNullException ),
                                        typeof( ArgumentOutOfRangeException ) };
				    if( !rgErrors.Contains( oEx.GetType() ))
					    throw;

                    _oSite.LogError( "view cache", "Update request on empty element" );
			    }
            }

            protected void Organize( CacheRow oRow ) {
                foreach( ColorRange oRange in _rgColumnOffsets ) {
                    oRange.Offset = 0;
                    oRange.Length = 0;
                }

                foreach( IColorRange oRange in oRow.Line.Formatting ) {
                    if( oRange is MemoryElem<char> oWord ) {
                        if( string.Compare(oWord.ID, "labelblock") == 0 ) {
                            _rgColumnOffsets[1].Offset = oRange.Offset;
                            _rgColumnOffsets[1].Length = oRange.Length;
                        }
                        if( string.Compare(oWord.ID, "instrblock") == 0 ) {
                            _rgColumnOffsets[2].Offset = oRange.Offset;
                            _rgColumnOffsets[2].Length = oRange.Length;
                        }
                        // Would love this kind of comment to right hand side go clear to right window edge.
                        if( string.Compare(oWord.ID, "commentline") == 0 ) { 
                            _rgColumnOffsets[2].Offset = oRange.Offset;
                            _rgColumnOffsets[2].Length = oRange.Length;
                        }
                        if( string.Compare(oWord.ID, "comment") == 0 ) {
                            _rgColumnOffsets[3].Offset = oRange.Offset;
                            _rgColumnOffsets[3].Length = oRange.Length;
                        }
                    }
                }

                _rgColumnOffsets[2].Offset = _rgColumnOffsets[1].Offset + _rgColumnOffsets[0].Length;
            }

            protected override void RowUpdate( CacheRow oRow ) {
                Organize( oRow );

                for( int i=0; i<_rgCacheMap.Count; ++i ) {
                    IMemoryRange oArg = null;

                    if( _rgColumnOffsets[i].Offset > -1 ) {
                        oArg = _rgColumnOffsets[i];
                    }

                    ElemUpdate3( oRow.CacheList[i], _rgCacheMap[i].Width, oArg );
                }
            }
        }
        protected readonly LayoutRect _rctLabelColumn = new LayoutRect( LayoutRect.CSS.Flex ) { Track = 1 };

        public NumberLabelWindow( IPgViewSite oSite, Editor oEdit ) : base( oSite, oEdit ) {
        }

        /// <seealso cref="CacheManager2.RowUpdate"/>
        protected override void InitColumns() {
            _rgLayout  .Add( _rctLineNumbers );
            _rgLayout  .Add( _rctLabelColumn );
            _rgLayout  .Add( _rctTextArea );  

            _rgCacheMap.Add( _rctTextArea );      // Text is always the first cache element on a row.
            _rgCacheMap.Add( _rctLineNumbers );   // Even if later in the layout. The rest must align.
            _rgCacheMap.Add( _rctLabelColumn );   // btw layout and columns
        }

        protected override CacheManager2 CreateCacheManager(uint uiStdText) {
            return new CacheManagerLabel( new CacheManSlot(this),
                                        _oStdUI.FontRendererAt(uiStdText),
                                        _rgCacheMap );
        }
    }

    public class BasicLineWindow : EditWindow2 {
        public static Guid GUID { get; } = new Guid( "{3D3B82AF-49FA-469E-865F-F35DD8CF11FB}" );
        public override Guid Catagory => GUID;

        protected MonitorDocument Monitor { get; }

        public class FTCacheLineNumber : FTCacheWrap {
            Line _oGuest; // The line we are listing.

            public FTCacheLineNumber( Line oLine, Line oGuest ) : base( oLine ) {
                _oGuest = oGuest ?? throw new ArgumentNullException( "Guest line must not be null" );
            }
        }
        public class CacheManagerBasic : CacheManager2 {
            public CacheManagerBasic( CacheManagerAbstractSite oSite, IPgFontRender oFont, List<SmartRect> rgCols ) :
                base( oSite, oFont, rgCols ) {
            }

            protected override CacheRow CreateRow( Line oLine ) {
                CacheRow oRow = base.CreateRow( oLine );

                if( oLine.Extra is Line oLineNumber ) {
                    FTCacheLine oElem = new FTCacheLineNumber( oLineNumber, oLine );

                    oRow.CacheList.Add( oElem );
                }

                return oRow;
            }
        }

        protected readonly LayoutRect _rctLineNumbers = new LayoutRect( LayoutRect.CSS.Flex ) { Track = 40 };

        public BasicLineWindow( IPgViewSite oSite, MonitorDocument oDoc ) : base( oSite, oDoc.AssemblyDoc ) {
            Monitor = oDoc ?? throw new ArgumentNullException( ); // We'll die before reaching this... :-/
        }
        protected override CacheManager2 CreateCacheManager(uint uiStdText) {
            return new CacheManagerBasic( new CacheManSlot(this),
                                        _oStdUI.FontRendererAt(uiStdText),
                                        _rgCacheMap );

        }
        protected override void InitColumns() {
            _rgLayout  .Add( _rctLineNumbers );
            _rgLayout  .Add( _rctTextArea );   // Main text area.

            _rgCacheMap.Add( _rctTextArea    );   // Text is always the first cache element on a row.
            _rgCacheMap.Add( _rctLineNumbers );   // Even if later in the layout.
        }

        public override bool Execute( Guid sGuidCommand ) {
            if( sGuidCommand == GlobalCommands.Insert ) {
                Monitor.LoadDialog();
                return true;
            }
            if( sGuidCommand == GlobalCommands.Pause ) {
                Monitor.Renumber();
                return true;
            }
            if( sGuidCommand == GlobalCommands.JumpPrev ) {
                Monitor.Test();
                return true;
            }
            return base.Execute( sGuidCommand );
        }
    }

    internal class WindowFrontPanel : SKControl,
        IPgParent,
        IPgCommandView,
        IPgLoad<XmlElement>,
        IPgSave<XmlDocumentFragment>
    {
        public static Guid GUID { get; } = new Guid( "{A28DDC95-EE48-4426-9D15-0B29F07D5F4A}" );

        protected MonitorDocument       MonitorDoc { get; }
        protected LayoutStackHorizontal MyLayout   { get; } = new LayoutStackHorizontal();
        protected EditWindow2           WinCommand { get; } // machine code..
        protected EditWindow2           WinAssembly{ get; } // Assembly, now BBC basic.

        public IPgParent Parentage => _oSiteView.Host;
        public IPgParent Services  => Parentage.Services;

        protected IPgViewSite _oSiteView;

        public string Banner => "Nibble Monitor";

        public SKBitmap Icon => null;

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

        /// <remarks>So turns out having seperate documents containing the various elements
        /// we want to display results in a bug in the main form window trying to manage the
        /// visual elements. Plus, undo is a nightmare. So punt on that and using a single
        /// property doc, but create outboard collections of the registers and status bits.</remarks>
        /// <exception cref="ArgumentNullException"></exception>
        public WindowFrontPanel( IPgViewSite oViewSite, MonitorDocument oMonitorDoc ) 
        {
            _oSiteView = oViewSite   ?? throw new ArgumentNullException();
            MonitorDoc = oMonitorDoc ?? throw new ArgumentNullException( "Monitor document must not be null!" );

            WinCommand  = new LineNumberWindow( new ViewSlot( this ), oMonitorDoc.TextCommands ) { Parent = this };
            WinAssembly = new BasicLineWindow ( new ViewSlot( this ), oMonitorDoc  ) { Parent = this };
        }

        public virtual bool InitNew() {
            if( !WinCommand.InitNew() )
                return false;

            if( !WinAssembly.InitNew() )
                return false;

            // Add the memory window and assembly.
            MyLayout.Add( new LayoutControl( WinCommand,  LayoutRect.CSS.Percent ) { Track = 30 } );
            MyLayout.Add( new LayoutControl( WinAssembly, LayoutRect.CSS.Percent ) { Track = 70 } );

            WinCommand .Parent = this;
            WinAssembly.Parent = this;

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
            if( sGuid == GlobalCommands.Insert ) {
                MonitorDoc.LoadDialog();
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
