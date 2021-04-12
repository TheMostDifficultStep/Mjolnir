using System;
using System.Drawing;
using System.Xml;
using System.Reflection;
using System.Collections.Generic;

using Play.Forms;
using Play.Interfaces.Embedding;
using Play.Edit;
using Play.Rectangles;
using Play.Parse.Impl;

namespace Play.SSTV {
    public class ViewSettings :
        FormsWindow,
        IPgLoad<XmlElement>,
        IPgSave<XmlDocumentFragment>,
        IPgParent,
        IPgCommandView,
        IBufferEvents
    {
        public static Guid GUID {get;} = new Guid("{5B8AC3A1-A20C-431B-BA13-09314BA767FC}");

        private   readonly string      _strViewIcon  = "Play.SSTV.icons8_tv.png";
        protected readonly IPgViewSite _oViewSite;

        public Guid      Catagory  => GUID; 
        public string    Banner    => "MySSTV Settings";
        public Image     Iconic    { get; }
        public bool      IsDirty   => false;
        public IPgParent Parentage => _oViewSite.Host;
        public IPgParent Services  => Parentage.Services;

        protected DocSSTV Document   { get; }
        protected Editor  PortTxList { get; } 
        protected Editor  PortRxList { get; }

		protected class WinSlot :
			IPgViewSite
		{
			protected readonly ViewSettings _oHost;

			public WinSlot( ViewSettings oHost ) {
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

        public ViewSettings( IPgViewSite oViewSite, DocSSTV oDocSSTV ) :
            base( oViewSite, oDocSSTV.Settings ) 
        {
            Document   = oDocSSTV ?? throw new ArgumentNullException( "Clock document must not be null." );
            _oViewSite = oViewSite;

            PortTxList = new Editor( new WinSlot( this ) );
            PortRxList = new Editor( new WinSlot( this ) );

			Iconic     = ImageResourceHelper.GetImageResource( Assembly.GetExecutingAssembly(), _strViewIcon );
        }

        protected override void Dispose( bool disposing ) {
            if( disposing ) {
                DocForms.CaretRemove( Caret );
            }
            base.Dispose(disposing);
        }

        public void PropertyInitRow( SmartTable oLayout, int iIndex, EditWindow2 oEditWin = null ) {
            var oLayoutLabel = new LayoutSingleLine( new FTCacheWrap( Document.Settings[iIndex]   ), LayoutRect.CSS.Flex );
            LayoutRect oLayoutValue;
            
            if( oEditWin == null ) {
                oLayoutValue = new LayoutSingleLine( new FTCacheWrap( Document.Settings[iIndex+1] ), LayoutRect.CSS.Flex );
            } else {
                oEditWin.InitNew();
                oEditWin.Parent = this;
                oLayoutValue = new LayoutControl( oEditWin, LayoutRect.CSS.Pixels, 100 );
            }

            oLayout.AddRow( new List<LayoutRect>() { oLayoutLabel, oLayoutValue } );

            CacheList.Add( oLayoutLabel );
            if( oLayoutValue is LayoutSingleLine oLayoutSingle )
                CacheList.Add( oLayoutSingle );
        }

        public override bool InitNew() {
            if( !base.InitNew() ) 
                return false;

            if( !PortTxList.InitNew() )
                return false;
            if( !PortRxList.InitNew() ) 
                return false;


            PortRxList.LineAppend( "one" );
            PortRxList.LineAppend( "two" );
            PortRxList.LineAppend( "three" );
            PortRxList.LineAppend( "four" );

            SmartTable oLayout = new SmartTable( 5, LayoutRect.CSS.None );
            Layout2 = oLayout;

            oLayout.Add( new LayoutRect( LayoutRect.CSS.Percent, 40, 0 ) ); // Name.
            oLayout.Add( new LayoutRect( LayoutRect.CSS.Percent, 65, 0 ) ); // Value.

            PropertyInitRow( oLayout, 0, new CheckList( new WinSlot( this ), PortTxList ) );
            PropertyInitRow( oLayout, 2, new CheckList( new WinSlot( this ), PortRxList ) );
            PropertyInitRow( oLayout, 4 );
            PropertyInitRow( oLayout, 6 );
            PropertyInitRow( oLayout, 8 );
            
            Caret.Layout = CacheList[0];

            OnDocumentEvent( BUFFEREVENTS.MULTILINE );
            OnSizeChanged( new EventArgs() );

            return true;
        }

        public bool Load( XmlElement oStream )
        {
            return true;
        }

        public bool Save( XmlDocumentFragment oStream )
        {
            return true;
        }

        public object Decorate(IPgViewSite oBaseSite, Guid sGuid) {
            return null;
        }

        public bool Execute(Guid sGuid) {
            return false;
        }

        public void OnEvent(BUFFEREVENTS eEvent) {
            OnDocumentEvent( BUFFEREVENTS.MULTILINE );
            Invalidate();
        }
    }
}
