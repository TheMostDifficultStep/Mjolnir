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

        /// <remarks>So this is an interesting case for our forms object. I would like the
        /// data and address lines to be two seperate text editors. The form which we 
        /// derive from really only understands one editor of editable elements. It comes
        /// down to who gets edit events. And how would undo work. It seems pretty
        /// special case and so I'll probably split the FormsWindow object for this.</remarks>
        /// <exception cref="ArgumentNullException"></exception>
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
			Blinken.Add( new LayoutRect( LayoutRect.CSS.Pixels, 60, .10f ) );
			Blinken.Add( new LayoutRect( LayoutRect.CSS.Pixels, 60, .10f ) );
			Blinken.Add( new LayoutRect( LayoutRect.CSS.Pixels, 60, .10f ) );
			Blinken.Add( new LayoutRect( LayoutRect.CSS.Pixels, 60, .10f ) );
			Blinken.Add( new LayoutRect( LayoutRect.CSS.Pixels, 60, .10f ) );
			Blinken.Add( new LayoutRect( LayoutRect.CSS.Pixels, 60, .10f ) );
			Blinken.Add( new LayoutRect( LayoutRect.CSS.Pixels, 60, .10f ) );
			Blinken.Add( new LayoutRect( LayoutRect.CSS.Pixels, 60, .10f ) );

            Editor oLabels = MonitorDoc.LablEdit;

            // Top row lables for the columns.
            List<LayoutRect> rgLablLayout = new ();
            rgLablLayout.Add( new LayoutSingleLine( new FTCacheLine( oLabels[3] ), LayoutRect.CSS.Flex ) );
            rgLablLayout.Add( new LayoutSingleLine( new FTCacheLine( oLabels[4] ), LayoutRect.CSS.Flex ) {Span=3 });
            rgLablLayout.Add( new LayoutSingleLine( new FTCacheLine( oLabels[5] ), LayoutRect.CSS.Flex ) {Span=3 });
            // contravarience vs covariance. Have to load cachelist one by one
            // instead of using addrange. Darn.
            foreach( LayoutRect oRect in rgLablLayout ) {
                if( oRect is LayoutSingleLine oSingle ) {
                    CacheList.Add( oSingle );
                }
            }

            List<LayoutRect> rgDataLayout = new();
            // Labels for the data blinken lights.
            rgDataLayout.Add( new LayoutSingleLine( new FTCacheLine( oLabels[0] ), LayoutRect.CSS.Flex ) );
            rgDataLayout.Add( new LayoutSingleLine( new FTCacheLine( oLabels[1] ), LayoutRect.CSS.Flex ) {Span=3 });

            // This is the data lights.
            for( int i=0; i<4; ++i ) {
                rgDataLayout.Add( new LayoutSingleLine( new FTCacheLine( MonitorDoc.DataLine[i] ), LayoutRect.CSS.Flex ) );
            }
            foreach( LayoutRect oRect in rgDataLayout ) {
                if( oRect is LayoutSingleLine oSingle ) {
                    CacheList.Add( oSingle );
                }
            }

            List<LayoutRect> rgAddrLayout = new();
            // Label for the address blinken lights.
            rgAddrLayout.Add( new LayoutSingleLine( new FTCacheLine( oLabels[2] ), LayoutRect.CSS.Flex ) );

            // This is the address lights
            for( int i=0; i<8; ++i ) {
                rgAddrLayout.Add( new LayoutSingleLine( new FTCacheLine( MonitorDoc.AddrLine[i] ), LayoutRect.CSS.Flex ) );
            }
            foreach( LayoutRect oRect in rgAddrLayout ) {
                if( oRect is LayoutSingleLine oSingle ) {
                    CacheList.Add( oSingle );
                }
            }

            Blinken.AddRow( rgLablLayout );
			Blinken.AddRow( rgDataLayout );
            Blinken.AddRow( rgAddrLayout );

            // Stuff the registers onto the same amount of blinken lines.
            for( int i=0; i< 4; ++i ) {
                MonitorDoc.Registers.Add( MonitorDoc.FrontDisplay.Property_Values.LineAppend( "0", false ) );

                List<LayoutRect> rgLayout  = new();
                Line             oRegister = MonitorDoc.Registers[i];
                Line             oLabel    = MonitorDoc.LablEdit[i+6]; // I forgot why I'm not using the property page labels.

                LayoutSingleLine oLayName = new LayoutSingleLine( new FTCacheLine( oLabel ), LayoutRect.CSS.Flex );
                rgLayout .Add( oLayName );
                CacheList.Add( oLayName );

                LayoutSingleLine oLayBlnk = new LayoutSingleLine( new FTCacheLine( MonitorDoc.LablEdit[3] ), LayoutRect.CSS.Flex ) { Span=3 };
                rgLayout .Add( oLayBlnk );
                CacheList.Add( oLayBlnk );

                LayoutSingleLine oLayLine = new LayoutSingleLine( new FTCacheLine( oRegister ), LayoutRect.CSS.Flex );
                rgLayout .Add( oLayLine );
                CacheList.Add( oLayLine );

                Blinken.AddRow( rgLayout );
            }

            // complete final layout of table and command window.
            VertStack.Add( Blinken );
            VertStack.Add( new LayoutControl( WinCommand, LayoutRect.CSS.Percent ) { Track = 60 } );

            OnDocumentEvent( BUFFEREVENTS.MULTILINE );
            OnSizeChanged( new EventArgs() );

            MonitorDoc.RefreshScreen += OnRefreshScreen_MonDoc;
            return true;
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
