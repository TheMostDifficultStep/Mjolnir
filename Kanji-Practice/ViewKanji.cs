using System.Xml;
using System.Windows.Forms;


using Play.Interfaces.Embedding;
using Play.Edit; 
using Play.Forms;
using Play.Rectangles;
using Play.Integration;
using Play.Parse;
using Play.Parse.Impl;

using SkiaSharp;

namespace Kanji_Practice {
    internal class ViewKanji: FormsWindow,
        IPgParent,
        IPgCommandView,
        IPgLoad<XmlElement>,
        IPgSave<XmlDocumentFragment>
    {
        protected KanjiDocument            KanjiDoc     { get; }
        protected WindowStandardProperties CenterDisplay{ get; }

        public IPgParent Parentage => _oSiteView.Host;
        public IPgParent Services  => Parentage.Services;

        public string Banner => "Kanji Cards";

        public SKBitmap Icon => null;

        public Guid Catagory => Guid.Empty;

        public bool  IsDirty => false;

        protected class DocSlot :
			IPgBaseSite
		{
			protected readonly ViewKanji _oHost;

			public DocSlot( ViewKanji oHost ) {
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
			public ViewSlot( ViewKanji oHost ) : base( oHost ) {
			}

			public IPgViewNotify EventChain => _oHost._oSiteView.EventChain;
		}

        public ViewKanji( IPgViewSite oViewSite, KanjiDocument oMonitorDoc ) : 
            base( oViewSite, oMonitorDoc.FrontDisplay.PropertyDoc ) 
        {
            KanjiDoc      = oMonitorDoc ?? throw new ArgumentNullException( "Monitor document must not be null!" );

            Layout        = new LayoutStackHorizontal() { Units = LayoutRect.CSS.Flex };
            CenterDisplay = new WindowStandardProperties( new ViewSlot( this ), KanjiDoc.FrontDisplay );

            CenterDisplay.Parent = this;
        }

        public override bool InitNew() {
            if( !base.InitNew() )
                return false;

            if( !CenterDisplay.InitNew() )
                return false;

            // complete final layout of table and command window.
            if( Layout is LayoutStack oStack ) {
                oStack.Add( new LayoutRect   ( LayoutRect.CSS.Pixels ) { Track = 50 } );
                oStack.Add( new LayoutControl( CenterDisplay, LayoutRect.CSS.None ) );
                oStack.Add( new LayoutRect   ( LayoutRect.CSS.Pixels ) { Track = 50 } );
            }

            OnDocumentEvent( BUFFEREVENTS.MULTILINE );

            //KanjiDoc.RefreshScreen += OnRefreshScreen_MonDoc;
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
            }
        }

        public bool Load(XmlElement oStream) {
            return InitNew();
        }

        protected override void Dispose(bool disposing) {
            if( disposing ) {
                //KanjiDoc.RefreshScreen -= OnRefreshScreen_MonDoc;
            }
            base.Dispose(disposing);
        }
        private void OnRefreshScreen_MonDoc(int obj) {
            OnDocumentEvent(BUFFEREVENTS.MULTILINE );
            Invalidate();
        }

        public bool Execute(Guid sGuid) {
            if( sGuid == GlobalCommands.JumpNext ) {
                KanjiDoc.Jump(1);
            }
            if( sGuid == GlobalCommands.JumpPrev ) {
                KanjiDoc.Jump(-1);
            }
            return false;
        }

        public object Decorate(IPgViewSite oBaseSite, Guid sGuid) {
			try {
			} catch ( Exception oEx ) {
				Type[] rgErrors = { typeof( NotImplementedException ),
									typeof( NullReferenceException ),
									typeof( ArgumentException ),
									typeof( ArgumentNullException ) };
				if( rgErrors.IsUnhandled( oEx ) )
					throw;

				//LogError( "decor", "Couldn't create EditWin decor: " + sGuid.ToString() );
			}

            return( null );
        }

        public bool Save(XmlDocumentFragment oStream) {
            return true;
        }
    }
}
