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
using Play.ImageViewer;

namespace Mjolnir {
    /// <summary>
    /// I should probably rename all the layout objects in the image walker b/c they
    /// are customized to work on the line objects of that one.
    /// </summary>
    class LayoutIcon : LayoutImageBase {
		public override SKBitmap Icon { get; }
        public LayoutIcon( SKBitmap skBmp, CSS eLayout = CSS.None) : 
            base(new SKSize( skBmp.Width, skBmp.Height), eLayout) 
        {
            Icon = skBmp;
        }
    }
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

		public void Add( LayoutRect oItem ) => _rgLayout.Add( oItem );
        public override void ItemSizeCalculate() {
        }

	} // End class
    public class MainWin_Tabs : 
		FormsWindow,
		IPgParent,
		IPgCommandView,
		IPgLoad<XmlElement>,
		IPgSave<XmlDocumentFragment>
	{
        public static readonly Guid ViewType = new Guid( "{18C24090-7BD4-401F-9B89-644CD9C14687}" );
        
		protected Editor TitleEditor { get; } // Need this dummy for now.

		protected readonly IPgStandardUI2 _oStdUI;

		public SKColor BgColorDefault { get; protected set; }

        // TODO: Look into moving to base class.
        public IPgParent Parentage => _oSiteView.Host;
        public IPgParent Services  => Parentage.Services;

        public bool  IsDirty => false;

        public Guid Catagory => ViewType;

        public string Banner => "Test Meh";

        public Image  Iconic => null;

        protected class WinSlot :
			IPgViewSite
		{
			protected readonly MainWin_Tabs _oHost;

			public WinSlot( MainWin_Tabs oHost ) {
				_oHost = oHost ?? throw new ArgumentNullException();
			}

			public IPgParent Host => _oHost;

			public void LogError(string strMessage, string strDetails, bool fShow=true) {
				_oHost._oSiteView.LogError( strMessage, strDetails );
			}

			public void Notify( ShellNotify eEvent ) {
				_oHost._oSiteView.Notify( eEvent );
			}

            public IPgViewNotify EventChain => _oHost._oSiteView.EventChain;
        }

        public MainWin_Tabs(IPgViewSite oSiteView, Editor oPropEd ) : 
            base(oSiteView, oPropEd) 
        {
 			_oStdUI        = oSiteView.Host.Services as IPgStandardUI2 ?? throw new ArgumentException( "Parent view must provide IPgStandardUI service" );
			BgColorDefault = _oStdUI.ColorsStandardAt( StdUIColors.BG );
        }

        public override bool InitNew() {
            if( !base.InitNew() ) 
                return false;

            LayoutFlowSquare_Fixed oLayout = new( new Size( 300, 50 ), 5 );
            Layout = oLayout;

            InitTabs( oLayout );

            Caret.Layout = CacheList[0];

            DocForms.BufferEvent += OnBufferEvent_ViewsEditor;

            OnBufferEvent_ViewsEditor( BUFFEREVENTS.MULTILINE );

            return true;
        }

        public void OnBufferEvent_ViewsEditor( BUFFEREVENTS eEvent ) {
            OnDocumentEvent( BUFFEREVENTS.MULTILINE );
        }

		protected void InitTabs( LayoutFlowSquare_Fixed oLayout ) {
			using SKPaint oPaint = new SKPaint() { Color = SKColors.Red };

			foreach( Line oViewLine in DocForms ) {
				LayoutIcon oIconLayout = new( new SKBitmap( 50, 50, SKColorType.Rgb888x, SKAlphaType.Opaque ),
                                              LayoutRect.CSS.Flex );
				using SKCanvas oCanvas = new SKCanvas( oIconLayout.Icon );

				oCanvas.DrawRect( 0, 0, oIconLayout.Icon.Width, oIconLayout.Icon.Height, oPaint );

				LayoutStackHorizontal oTab = new ( 5, 300, 100 );
				
				LayoutSingleLine oSingle = new LayoutSingleLine( new FTCacheWrap( oViewLine ), LayoutRect.CSS.None );
				CacheList.Add(oSingle);

				oTab.Add( oIconLayout );
				oTab.Add( oSingle );

				oLayout.Add( oTab );
			}
		}

        protected override void OnPaintSurface(SKPaintSurfaceEventArgs e) {
            base.OnPaintSurface(e);

            SKPaint skPaint = new SKPaint() { Color = SKColors.Aqua };

			foreach( LayoutRect oRect in Layout ) {
                //SKRectI skRect = oRect.SKRect;
                //skRect.Bottom += 10;
                //e.Surface.Canvas.DrawRect( skRect, skPaint );
				oRect.Paint( e.Surface.Canvas );
			}
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
