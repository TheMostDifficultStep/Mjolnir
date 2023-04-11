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
        protected KanjiDocument       KanjiDoc   { get; }
        protected SmartTable          Blinken    { get; }
        protected LayoutStackVertical VertStack  { get; }

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

        /// <remarks>So this is an interesting case for our forms object. I would like the
        /// data and address lines to be two seperate text editors. The form which we 
        /// derive from really only understands one editor of editable elements. It comes
        /// down to who gets edit events. And how would undo work. It seems pretty
        /// special case and so I'll probably split the FormsWindow object for this.</remarks>
        /// <exception cref="ArgumentNullException"></exception>
        public ViewKanji( IPgViewSite oViewSite, KanjiDocument oMonitorDoc ) : 
            base( oViewSite, oMonitorDoc.FrontDisplay.Property_Values ) 
        {
            KanjiDoc = oMonitorDoc ?? throw new ArgumentNullException( "Monitor document must not be null!" );

            VertStack  = new LayoutStackVertical();
            Layout     = VertStack;

            Blinken     = new SmartTable( 5, LayoutRect.CSS.Percent ) { Track = 40 };
        }

        public override bool InitNew() {
            if( !base.InitNew() )
                return false;

            // First, add the columns to our table.
			Blinken.Add( new LayoutRect( LayoutRect.CSS.Flex ) );
			Blinken.Add( new LayoutRect( LayoutRect.CSS.Pixels, 60, .20f ) );
			Blinken.Add( new LayoutRect( LayoutRect.CSS.Pixels, 60, .20f ) );
			Blinken.Add( new LayoutRect( LayoutRect.CSS.Pixels, 60, .20f ) );
			Blinken.Add( new LayoutRect( LayoutRect.CSS.Pixels, 60, .20f ) );
			//Blinken.Add( new LayoutRect( LayoutRect.CSS.None ) );

            Editor oLabels = KanjiDoc.FlashCardDoc;

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

            Blinken.AddRow( rgStatusLabel );

            List<LayoutRect> rgBlankLine = new();
            rgBlankLine.Add( new LayoutSingleLine( new FTCacheLine( oLabels[3] ), LayoutRect.CSS.Flex )  );
            foreach( LayoutRect oRect in rgBlankLine ) {
                if( oRect is LayoutSingleLine oSingle ) {
                    CacheList.Add( oSingle );
                }
            }

            Blinken.AddRow( rgBlankLine );

            // Add the memory window and assembly.
            LayoutStackHorizontal oHoriz = new( ) { Track = 60, Units = LayoutRect.CSS.Percent };

            // complete final layout of table and command window.
            VertStack.Add( Blinken );
            VertStack.Add( oHoriz  );

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
