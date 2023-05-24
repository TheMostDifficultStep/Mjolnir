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
    public class PropertyWindow : WindowStandardProperties {
        public PropertyWindow( IPgViewSite oViewSite, CpuProperties oPropDoc ) : base( oViewSite, oPropDoc ) {
        }
    }

    public class LineNumberWindow : EditWindow2 {
        public class FTCacheLineNumber : FTCacheWrap {
            Line _oGuest; // The line we are listing.

            public FTCacheLineNumber( Line oLine, Line oGuest ) : base( oLine ) {
                _oGuest = oGuest ?? throw new ArgumentNullException( "Guest line must not be null" );
            }

            public override void Update(IPgFontRender oFR, IMemoryRange oRange ) {
                Line.Empty();
                Line.TryAppend( _oGuest.At.ToString() );

                base.Update(oFR);
            }

            public override bool IsInvalid { get => _oGuest.At != Line.At; }
        }
        public class CacheManagerAsm : CacheManager2 {
            public CacheManagerAsm( CacheManagerAbstractSite oSite, IPgFontRender oFont, List<SmartRect> rgCols ) :
                base( oSite, oFont, rgCols ) {
            }

            protected override CacheRow CreateRow( Line oLine ) {
                CacheRow oRow = base.CreateRow( oLine );

                FTCacheLine oElem = new FTCacheLineNumber( new TextLine( oLine.At, oLine.At.ToString() ), oLine );

                ElemUpdate2( oElem, _rgColumns[1].Width );

                oRow.CacheList.Add( oElem );

                return oRow;
            }
        }

        protected readonly LayoutRect _rctLineNumbers = new LayoutRect( LayoutRect.CSS.Flex ) { Track = 30 };

        public LineNumberWindow( IPgViewSite oSite, Editor oEdit ) : base( oSite, oEdit ) {
        }

        protected override void InitColumns() {
            _oLayout  .Add( _rctLineNumbers );
            _oLayout  .Add( _rctTextArea );   // Main text area.

            _rgColumns.Add( _rctTextArea );   // Text is always the first cache element on a row.
            _rgColumns.Add( _rctLineNumbers );   // Even if later in the layout.
        }

        protected override CacheManager2 CreateCacheManager(uint uiStdText) {
            return new CacheManagerAsm( new CacheManSlot(this),
                                        _oStdUI.FontRendererAt(uiStdText),
                                        _rgColumns );
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

                ElemUpdate2( oElem, _rgColumns[2].Width );

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
                for( int i=0; i< oRow.CacheList.Count; i++ ) {
                    IMemoryRange oArg = null;

                    if( _rgColumnOffsets[i].Offset > -1 ) {
                        oArg = _rgColumnOffsets[i];
                    }

                    ElemUpdate3( oRow.CacheList[i], _rgColumns[i].Width, oArg );
                }
            }
        }
        protected readonly LayoutRect _rctLabelColumn = new LayoutRect( LayoutRect.CSS.Flex ) { Track = 1 };

        public NumberLabelWindow( IPgViewSite oSite, Editor oEdit ) : base( oSite, oEdit ) {
        }

        /// <seealso cref="CacheManager2.RowUpdate"/>
        protected override void InitColumns() {
            _oLayout  .Add( _rctLineNumbers );
            _oLayout  .Add( _rctLabelColumn );
            _oLayout  .Add( _rctTextArea );  

            _rgColumns.Add( _rctTextArea );      // Text is always the first cache element on a row.
            _rgColumns.Add( _rctLineNumbers );   // Even if later in the layout. The rest must align.
            _rgColumns.Add( _rctLabelColumn );   // btw layout and columns
        }

        protected override CacheManager2 CreateCacheManager(uint uiStdText) {
            return new CacheManagerLabel( new CacheManSlot(this),
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
        protected LayoutStackHorizontal MyLayout   { get; } = new LayoutStackHorizontal();
        protected EditWindow2           WinCommand { get; } // machine code..
        protected EditWindow2           WinAssembly{ get; }

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

            WinCommand  = new LineNumberWindow( new ViewSlot( this ), oMonitorDoc.TextCommands ) { Parent = this };
            WinAssembly = new LineNumberWindow( new ViewSlot( this ), oMonitorDoc.AssemblyDoc  ) { Parent = this };
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
