using System;
using System.Xml;
using System.Windows.Forms;

using SkiaSharp;

using Play.Rectangles;
using Play.Interfaces.Embedding;
using Play.Edit;

namespace Play.MorsePractice {
    class ViewQrz :
        Control,
        IPgParent,
        IPgLoad<XmlElement>,
        IPgSave<XmlDocumentFragment>,
        IPgTextView,
        IPgCommandView
    {
        public static readonly Guid _guidViewCategory = new Guid("{cfe013a0-d24b-407d-91d7-1b2acd2b0d0d}");

        readonly IPgViewSite  _oSiteView;
        readonly IPgShellSite _oSiteShell;
        readonly DocNotes     _oDocMorse;
        readonly LayoutStack  _rgLayout;

        protected bool _fDisposed = false;

        protected class ViewQrzSlot :
			IPgBaseSite,
		    IPgViewSite
		{
			protected readonly ViewQrz _oHost;

			public ViewQrzSlot(ViewQrz oHost ) {
				_oHost   = oHost ?? throw new ArgumentNullException();
			}

			public IPgParent Host => _oHost;

			public void LogError(string strMessage, string strDetails, bool fShow=true) {
				_oHost._oSiteView.LogError( strMessage, strDetails, fShow );
			}

			public virtual IPgViewNotify EventChain => _oHost._oSiteView.EventChain;

			public void Notify( ShellNotify eEvent ) {
			}
		} // End class

		protected class ViewQrzCommandSlot :
			ViewQrzSlot,
			IPgViewNotify
		{
			protected readonly IPgViewNotify _oHostEvent;

			public ViewQrzCommandSlot(ViewQrz oHost ) : base( oHost ) {
				_oHostEvent = oHost._oSiteView.EventChain;
			}

			public override IPgViewNotify EventChain => this;

			public void NotifyFocused(bool fSelect) {
				_oHostEvent.NotifyFocused( fSelect );
			}

			public bool IsCommandKey(CommandKey ckCommand,KeyBoardEnum kbModifier) {
				return _oHostEvent.IsCommandKey( ckCommand, kbModifier );
			}

			public bool IsCommandPress(char cChar) {
                if (cChar == '\r')
                    _oHost.Execute(GlobalCommands.Play);

                return _oHostEvent.IsCommandPress( cChar );
			}
		}

        public IPgParent Parentage => _oSiteView.Host;
        public IPgParent Services  => Parentage.Services;
		public Guid      Catagory  => _guidViewCategory;
		public string    Banner    => "View Qrz";
		public SKBitmap     Icon    => null;

		public Editor CallSign       { get { return _oDocMorse.CallSign; } }
        public Editor CallSignBio    { get { return _oDocMorse.CallSignBio; } }

        EditWindow2 ViewCallSign { get; }
		EditWindow2 ViewBiograph { get; }

        public bool IsDirty => false;

        public TextPosition Caret => ((IPgTextView)ViewBiograph).Caret;

        public object DocumentText => ((IPgTextView)ViewBiograph).DocumentText;

        public ViewQrz( IPgViewSite oSiteView, DocNotes oDocument ) {
            _oSiteView  = oSiteView ?? throw new ArgumentNullException();
            _oDocMorse  = oDocument ?? throw new ArgumentNullException();
            _oSiteShell = oSiteView as IPgShellSite ?? throw new ArgumentException("Parent view must provide IPgShellSite service");

			ViewCallSign = new EditWindow2( new ViewQrzCommandSlot( this ), CallSign, false, fSingleLine:true ) { Parent = this };
            ViewBiograph = new ViewBio    ( new ViewQrzSlot       ( this ), oDocument ) { Parent = this };

            _rgLayout = new LayoutStackVertical() {
                Spacing   = 15,
                Children = {
                    new LayoutStackHorizontal( 19, .1f ) {
                        Spacing = 15,
                        Children = {
                        new LayoutControl( ViewCallSign, LayoutRect.CSS.Pixels, 100 ),
                        new LayoutRect( LayoutRect.CSS.None )
                    }   },
                    new LayoutControl( ViewBiograph, LayoutRect.CSS.None,   100 )
                }
            };
        }

        protected override void Dispose( bool disposing ) {
            if( disposing && !_fDisposed ) {
                ViewCallSign.Dispose();
                ViewBiograph  .Dispose();

                _fDisposed = true;
            }

            base.Dispose(disposing);
        }

        public bool Load(XmlElement oStream) {
            return InitNew();
        }

        public bool InitNew() {
            if( !ViewCallSign.InitNew())
                return false;
            if( !ViewBiograph.InitNew())
                return false;

            return true;
        }
        
        public bool Save(XmlDocumentFragment oStream) {
            return true;
        }

        protected override void OnSizeChanged(EventArgs e) {
            base.OnSizeChanged(e);

            _rgLayout.SetRect(LOCUS.UPPERLEFT, 0, 0, Width, Height);
            _rgLayout.LayoutChildren();

            Invalidate();
        }

        public bool Execute(Guid sGuid)
        {
            if( sGuid == GlobalCommands.Play ) {
                _oDocMorse.StationLoad();
            }
            return false;
        }

        public object Decorate(IPgViewSite oBaseSite, Guid sGuid)
        {
            return null;
        }

        public void ScrollTo(EDGE eEdge)
        {
            ((IPgTextView)ViewBiograph).ScrollTo(eEdge);
        }

        public void ScrollToCaret()
        {
            ((IPgTextView)ViewBiograph).ScrollToCaret();
        }

        public bool SelectionSet(int iLine, int iOffset, int iLength)
        {
            return ((IPgTextView)ViewBiograph).SelectionSet(iLine, iOffset, iLength);
        }

        public void SelectionClear()
        {
            ((IPgTextView)ViewBiograph).SelectionClear();
        }

        public void StationLoad( string strStation )
        { 
            _oSiteShell.AddView(ViewQrz._guidViewCategory, fFocus: true);

            _oDocMorse.StationLoad( strStation );
        }

        protected override void OnGotFocus(EventArgs e)
        {
            ViewCallSign.SetFocus();
        }
    }
}
