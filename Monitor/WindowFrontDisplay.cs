using System;
using System.Collections.Generic;
using System.Xml;

using Play.Interfaces.Embedding;
using Play.Forms;
using Play.Rectangles;
using Play.Edit;
using SkiaSharp;

namespace Monitor {
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

        public WindowFrontPanel( IPgViewSite oViewSite, MonitorDocument oMonitorDoc ) : 
            base( oViewSite, oMonitorDoc.FrontDisplay.Property_Values ) 
        {
            MonitorDoc = oMonitorDoc ?? throw new ArgumentNullException( "Monitor document must not be null!" );

            VertStack  = new LayoutStackVertical();
            Layout     = VertStack;

            WinCommand = new EditWindow2( new ViewSlot( this ), oMonitorDoc.TextCommands ) { Parent = this };
            Blinken    = new SmartTable( 5, LayoutRect.CSS.Percent ) { Track = 40 };
        }

        public override bool InitNew() {
            if( !base.InitNew() )
                return false;

            if( !WinCommand.InitNew() )
                return false;

            // First, add the columns to our table.
			Blinken.Add( new LayoutRect( LayoutRect.CSS.None ) );
			Blinken.Add( new LayoutRect( LayoutRect.CSS.Pixels, 50, .25f ) );
			Blinken.Add( new LayoutRect( LayoutRect.CSS.Pixels, 50, .25f ) );
			Blinken.Add( new LayoutRect( LayoutRect.CSS.Pixels, 50, .25f ) );
			Blinken.Add( new LayoutRect( LayoutRect.CSS.Pixels, 50, .25f ) );

            Editor           oValues = MonitorDoc.FrontDisplay.Property_Values;
            Editor           oLables = MonitorDoc.FrontDisplay.Property_Labels;
            List<LayoutRect> rgLayouts  = new();

            // Lable for the blinken lights.
            rgLayouts.Add( new LayoutSingleLine( new FTCacheLine( oLables[0] ), LayoutRect.CSS.Flex ) );
            // This is the lights.
            for( int i=0; i<4; ++i ) {
                rgLayouts.Add( new LayoutSingleLine( new FTCacheLine( oValues[i] ), LayoutRect.CSS.Flex ) );
            }
            // contravarience vs covariance. Have to load cachelist one by one
            // instead of using addrange. Darn.
            foreach( LayoutRect oRect in rgLayouts ) {
                if( oRect is LayoutSingleLine oSingle ) {
                    CacheList.Add( oSingle );
                }
            }

			Blinken.AddRow( rgLayouts );

            // complete final layout of table and command window.
            VertStack.Add( Blinken );
            VertStack.Add( new LayoutControl( WinCommand, LayoutRect.CSS.Percent ) { Track = 60 } );

            OnDocumentEvent( BUFFEREVENTS.MULTILINE );
            OnSizeChanged( new EventArgs() );

            return true;
        }

        public bool Execute(Guid sGuid) {
            throw new NotImplementedException();
        }

        public object Decorate(IPgViewSite oBaseSite, Guid sGuid) {
            return null;
        }

        public bool Load(XmlElement oStream) {
            return true;
        }

        public bool Save(XmlDocumentFragment oStream) {
            return true;
        }
    }
}
