using System;
using System.Collections.Generic;
using System.Drawing;
using System.Xml;

using SkiaSharp;
using SkiaSharp.Views.Desktop;

using Play.Rectangles;
using Play.Edit;
using Play.Forms;
using Play.Interfaces.Embedding;

namespace Mjolnir {
    /// <summary>
    /// Experimental class for my tabs.
    /// </summary>
    /// <seealso cref="LayoutFlowSquare"/>
    public class LayoutFlowSquare_Fixed : LayoutFlowSquare {
		readonly List<LayoutRect> _rgLayout = new List<LayoutRect>();

		public LayoutFlowSquare_Fixed( Size szSize, uint uiMargin ) : base( szSize, uiMargin ) {
			ItemSize = szSize;
		}

		public LayoutFlowSquare_Fixed( CSS eUnits, uint uiMargin ) : base( eUnits, uiMargin ) {
		}

		public override void                    Clear()          => _rgLayout.Clear();
		public override LayoutRect              Item(int iIndex) => _rgLayout[iIndex];
		public override IEnumerator<LayoutRect> GetEnumerator()  => _rgLayout.GetEnumerator();
		public override int                     Count            => _rgLayout.Count;
        public virtual  void                    Remove( int i )  => _rgLayout.RemoveAt( i );

		public void Add( LayoutRect oItem ) => _rgLayout.Add( oItem );
        public override void ItemSizeCalculate() {
        }

	} // End class

    /// <summary>
    /// We don't inherit from FormsWindow b/c there is no need for the tab text to be editable.
    /// This is a test view for the new tabs control I'm working on.
    /// </summary>
    public abstract class TabControl : 
		SKControl,
		IPgLoad,
        ILineEvents,
        IDisposable
	{
        protected readonly IPgViewSite    _oSiteView;
		protected readonly IPgStandardUI2 _oStdUI;
        protected          uint           _uStdFont;

        protected BaseEditor Document { get; }
        
        protected readonly List<LayoutSingleLine> _rgTextCache = new List<LayoutSingleLine>();

        public new LayoutFlowSquare_Fixed Layout { get; }

        public TabControl(IPgViewSite oSiteView, BaseEditor oDoc ) 
        {
 			_oStdUI  = oSiteView.Host.Services as IPgStandardUI2 ?? throw new ArgumentException( "Parent view must provide IPgStandardUI service" );
            Document = oDoc ?? throw new ArgumentNullException( nameof( oDoc ) );

            Layout   = new LayoutFlowSquare_Fixed( new Size( 300, 40 ), 5 );
        }

        public virtual bool InitNew() {
            try {
                Document.ListenerAdd( this );

                // See also GetSystemMetricsForDpi() per monitor dpi aware
                SKSize sResolution = new SKSize(96, 96);
                using (Graphics oGraphics = this.CreateGraphics()) {
                    sResolution.Width  = oGraphics.DpiX;
                    sResolution.Height = oGraphics.DpiY;
                }

                _uStdFont = _oStdUI.FontCache(_oStdUI.FaceCache(@"C:\windows\fonts\consola.ttf"), 10, sResolution);

			    foreach( Line oLine in Document ) {
				    Layout.Add( CreateTab( oLine ) );
			    }

                OnEvent( BUFFEREVENTS.MULTILINE );

                return true;
            } catch( ArgumentOutOfRangeException ) {
                return false;
            }
        }

        protected override void Dispose( bool disposing ) {
            if( disposing ) {
                Document.ListenerRemove( this );
            }

            base.Dispose( disposing );
        }

        /// <summary>
        /// This gets called whenever the tab needs to be drawn.
        /// </summary>
        /// <param name="iID">Id of the tab to return the requested info.</param>
        /// <returns>Focus status</returns>
        public virtual SKColor TabStatus( object oID ) {
            return SKColors.White;
        }

        /// <summary>
        /// This gets called whenever the tab needs to be drawn.
        /// </summary>
        /// <param name="iID">Id of the tab to return the requested info.</param>
        /// <returns>Focus status</returns>
        public virtual SKColor TabBackground( object oID ) {
            return _oStdUI.ColorsStandardAt( StdUIColors.BGReadOnly );
        }

        /// <summary>
        /// This gets called when a Tab is being created.
        /// </summary>
        /// <param name="iID"></param>
        /// <returns>Return a bitmap</returns>
        public abstract SKBitmap TabIcon( object oID );

        protected virtual ParentRect CreateTab( Line oViewLine ) {
			LayoutIcon       oTabIcon = new( TabIcon( oViewLine ), LayoutRect.CSS.Flex );

			LayoutSingleLine oTabText = new LayoutSingleLine( new FTCacheWrap( oViewLine ), 
                                                              LayoutRect.CSS.None ) 
                                            { BgColor = SKColors.Transparent };
			_rgTextCache.Add(oTabText);

            LayoutPattern oTabStatus = new( LayoutRect.CSS.Pixels, 7, oViewLine, TabStatus );

            // Round up all the layouts into our tab object here.
			LayoutStackHorizontal oTab = new ( 5 ) { BackgroundColor = TabBackground, ID = oViewLine };
				
            oTab.Add( oTabStatus );
			oTab.Add( oTabIcon );
			oTab.Add( oTabText );

            return oTab;
        }

        protected override void OnPaintSurface(SKPaintSurfaceEventArgs e) {
            base.OnPaintSurface(e);

            SKCanvas skCanvas = e.Surface.Canvas;

            using SKPaint skPaint = new SKPaint() { Color = _oStdUI.ColorsStandardAt( StdUIColors.BGReadOnly ) };

            skCanvas.DrawRect( 0, 0, Width, Height, skPaint );

			foreach( LayoutRect oTab in Layout ) {
				oTab.Paint( skCanvas );
			}

            foreach( LayoutSingleLine oCache in _rgTextCache ) {
                skCanvas.Save();
                skCanvas.ClipRect( new SKRect(oCache.Left, oCache.Top, oCache.Right, oCache.Bottom ), SKClipOperation.Intersect);
                oCache  .Paint( e.Surface.Canvas, _oStdUI, this.Focused );
                skCanvas.Restore();
            }
		}

        protected override void OnSizeChanged(EventArgs e) {
            base.OnSizeChanged(e);

			Layout.SetRect( 0, 0, Width, Height );
			Layout.LayoutChildren();

            Invalidate();
        }

        public void OnLineNew(Line oLine) {
            Layout.Add( CreateTab( oLine ) );

            OnSizeChanged( new EventArgs() );
        }

        /// <summary>
        /// Need to check if the Line.At is constant or not. If not
        /// we'll need to update our ID's on the sub elements of
        /// the tab.
        /// </summary>
        public void OnLineDelete(Line oLine) {
            // find the tab and remove it.
            for( int i=0; i<Layout.Count; ++i ) {
                LayoutRect oChild = Layout.Item(i);
                if( oChild is LayoutStackHorizontal oTab ) {
                    if( oTab.ID == oLine ) {
                        Layout.Remove( i );
                    }
                }
            }
            // Find the text cache and remove it.
            for( int i=0; i<_rgTextCache.Count; ++i ) {
                if( _rgTextCache[i].Cache.Line == oLine ) {
                    _rgTextCache.RemoveAt( i );
                }
            }
            OnSizeChanged( new EventArgs() );
        }

        public void OnLineUpdated(Line oLine, int iOffset, int iOldLen, int iNewLen) {
        }

        public void OnEvent(BUFFEREVENTS eEvent) {
            switch( eEvent ) {
                case BUFFEREVENTS.FORMATTED:
                case BUFFEREVENTS.SINGLELINE:
                case BUFFEREVENTS.MULTILINE:
                    foreach( LayoutSingleLine oCache in _rgTextCache ) {
                        oCache.Cache.Update( _oStdUI.FontRendererAt( _uStdFont ) );
                        oCache.OnChangeFormatting();
                        oCache.Cache.OnChangeSize( oCache.Width );
                    }
                    Invalidate();
                    break;
            }
        }
    }
    public class MainWin_Tabs : 
		TabControl,
		IPgParent,
		IPgCommandView,
		IPgLoad<XmlElement>,
		IPgSave<XmlDocumentFragment>
	{
        public static readonly Guid ViewType = new Guid( "{18C24090-7BD4-401F-9B89-644CD9C14687}" );

        public Guid      Catagory  => ViewType;
        public string    Banner    => "Test Meh";
        public Image     Iconic    => null;
        public bool      IsDirty   => false;

        public IPgParent Parentage => _oSiteView.Host;
        public IPgParent Services  => Parentage.Services;

        public MainWin_Tabs(IPgViewSite oSiteView, Editor oDoc) : base(oSiteView, oDoc) {
        }

        /// <summary>
        /// This gets called whenever the tab needs to be drawn.
        /// </summary>
        /// <param name="iID">Id of the tab to return the requested info.</param>
        /// <returns>Focus status</returns>
        public override SKColor TabStatus( object oID ) {
            if( oID is Line oLine ) {
                return oLine.At == 1 ? SKColors.Blue : TabBackground( oLine.At );
            }
            
            return SKColors.White;
        }

        /// <summary>
        /// This gets called whenever the tab needs to be drawn.
        /// </summary>
        /// <param name="iID">Id of the tab to return the requested info.</param>
        /// <returns>Focus status</returns>
        public override SKColor TabBackground( object oID ) {
            SKColor skBG = _oStdUI.ColorsStandardAt( StdUIColors.BGReadOnly );

            if( oID is Line oLine ) {
                return oLine.At == 1 ? SKColors.LightCyan : skBG;
            }
            return skBG;
        }

        /// <summary>
        /// This gets called when a Tab is being created.
        /// </summary>
        /// <param name="iID"></param>
        /// <returns>Return a bitmap</returns>
        public override SKBitmap TabIcon( object oID ) {
            SKBitmap skIcon = new( 30, 30, SKColorType.Rgb888x, SKAlphaType.Opaque );

            using SKPaint  oPaint  = new () { Color = SKColors.Red };
			using SKCanvas oCanvas = new ( skIcon );

			oCanvas.DrawRect( 0, 0, skIcon.Width, skIcon.Height, oPaint );

            return skIcon;
        }

        public bool Save(XmlDocumentFragment oStream) {
            return true;
        }

        public bool Load(XmlElement oStream) {
            return true;
        }

        public object Decorate(IPgViewSite oBaseSite, Guid sGuid) {
            return null;
        }

        public bool Execute(Guid sGuid) {
            return false;
        }

    }
}
