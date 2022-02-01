using System;
using System.Collections.Generic;
using System.Xml;
using System.Reflection;

using SkiaSharp;
using SkiaSharp.Views.Desktop;

using Play.Drawing;
using Play.Interfaces.Embedding;
using Play.Rectangles;
using Play.ImageViewer;

namespace Play.Clock {
    class ViewSolar : SKControl,
        IPgParent,
        IPgLoad<XmlElement>,
		IPgSave<XmlDocumentFragment>,
		IPgCommandView
    {
        public static readonly Guid _guidViewCatagory = new Guid("{6DF011FB-4122-4171-B6F8-39B06211E728}" );

		protected readonly IPgViewSite   _oSiteView;
        protected readonly IPgViewNotify _oViewEvents; // Our site from the window manager (view interface).
        protected SolarDoc Document { get; }

		public IPgParent Parentage => _oSiteView.Host;
		public IPgParent Services  => Parentage.Services;

        protected List<LayoutRect>      CacheList { get; }      = new List<LayoutRect>();
        protected LayoutStackHorizontal Layout2   { get; set; } = new LayoutStackHorizontal() { Spacing = 5 };

        ImageViewSingle ViewSolarVhf { get; }
        ImageViewSingle ViewSolarMap { get; }

        public bool IsDirty => false;

        public Guid   Catagory => _guidViewCatagory;
        public string Banner   => "Solar Weather";
        public SKBitmap  Icon   { get; }

        protected class ViewSolarSlot :
			IPgBaseSite, IPgViewSite
		{
			protected readonly ViewSolar _oHost;

			public ViewSolarSlot( ViewSolar oHost ) {
				_oHost = oHost ?? throw new ArgumentNullException();
			}

			public IPgParent Host => _oHost;

			public void LogError(string strMessage, string strDetails, bool fShow=true) {
				_oHost._oSiteView.LogError( strMessage, strDetails, fShow );
			}

			public void Notify( ShellNotify eEvent ) {
			}

			public IPgViewNotify EventChain => _oHost._oSiteView.EventChain;
		} // End class

        public ViewSolar( IPgViewSite oSiteView, SolarDoc oDocSolar ) {
            Document     = oDocSolar ?? throw new ArgumentNullException( "Morse Doc on View Solar must not be null." );
			_oSiteView   = oSiteView ?? throw new ArgumentNullException( "Solar window needs a site!!" );
            _oViewEvents = oSiteView.EventChain ?? throw new ArgumentException("Site.EventChain must support IPgViewSiteEvents");

			ViewSolarVhf = new ImageViewSingle( new ViewSolarSlot(this), oDocSolar.SolarVhf );
			ViewSolarMap = new ImageViewSingle( new ViewSolarSlot(this), oDocSolar.SolarMap );

            // NOTE: Not currently disposed...
            Icon = SKImageResourceHelper.GetImageResource( Assembly.GetExecutingAssembly(),"Play.Clock.Content.icons8-solar-system-64.png" );
        }

        public bool Load( XmlElement oStream ) {
            return InitNew();
        }

        public bool Save( XmlDocumentFragment oStream ) {
            return true;
        }

        public bool InitNew() {
            ViewSolarVhf.InitNew();
            ViewSolarMap.InitNew();

            Layout2.Add( new LayoutRect( LayoutRect.CSS.None ) );
            Layout2.Add( new LayoutStackVertical( 450, 1F ) { 
                Spacing = 5,
                Children = { new LayoutImageView(ViewSolarMap, .5F ), new LayoutImageView(ViewSolarVhf, .5F) } 
                } );
            Layout2.Add( new LayoutRect( LayoutRect.CSS.None ) );

            OnSizeChanged( null );

            return true;
        }

        protected override void OnGotFocus(EventArgs e) {
            base.OnGotFocus( e );

            _oViewEvents.NotifyFocused( true );

            this.Invalidate();
        }

        protected override void OnLostFocus(EventArgs e) {
            base.OnLostFocus( e );

            _oViewEvents.NotifyFocused( false );

            this.Invalidate();
        }

        protected override void OnSizeChanged( EventArgs e ) {
			Layout2.SetRect( 0, 0, Width, Height );
			Layout2.LayoutChildren();

            Invalidate();
        }

        protected override void OnPaintSurface(SKPaintSurfaceEventArgs e) {
            base.OnPaintSurface(e);

            SKCanvas skCanvas = e.Surface.Canvas;

            foreach( LayoutRect oCache in CacheList ) {
                oCache.Paint( skCanvas );
            }
            Layout2.Paint( e.Surface.Canvas ); //Use this to see what the columns look like.
        }

        object IPgCommandView.Decorate( IPgViewSite oBaseSite, Guid sGuid ) {
            return null;
        }

        bool IPgCommandBase.Execute( Guid sGuid ) {
            if( sGuid == GlobalCommands.Play ) {
                Document.LoadSolar();
                return true;
            }

            return false;
        }
    }
}
