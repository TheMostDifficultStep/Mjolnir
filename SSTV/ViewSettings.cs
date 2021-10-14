using System;
using System.Drawing;
using System.Xml;
using System.Reflection;

using Play.Forms;
using Play.Interfaces.Embedding;
using Play.Edit;
using Play.Rectangles;

namespace Play.SSTV {
    /// <summary>
    /// Implements a property page that can contain simple Line elements OR whole Editor's. 
    /// It does this by keeping a flat Editor containing properties, but if you have a
    /// multiline property, we don't use the Document.Settings_Values but create a cache
    /// element pointing to an edit window, instead of a standard cache line.
    /// </summary>
    /// <remarks>TODO: I probably inherit from a ViewStandardProperties object and remove our direct 
    /// DocSSTV dependency.</remarks>
    public class ViewSettings :
        ViewStandardProperties,
        IPgLoad<XmlElement>,
        IPgSave<XmlDocumentFragment>,
        IPgParent,
        IPgCommandView,
        IBufferEvents
    {
        public static Guid GUID {get;} = new Guid("{5B8AC3A1-A20C-431B-BA13-09314BA767FC}");

        private   readonly string      _strViewIcon  = "Play.SSTV.icons8_settings.png";
        protected readonly IPgViewSite _oViewSite;

        public Guid      Catagory  => GUID; 
        public string    Banner    => "MySSTV Settings";
        public Image     Iconic    { get; }
        public bool      IsDirty   => false;
        public IPgParent Parentage => _oViewSite.Host;
        public IPgParent Services  => Parentage.Services;

        public DocSSTV SSTVDocument { get; }

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

        public ViewSettings( IPgViewSite oViewSite, DocSSTV oDocument ) :
            base( oViewSite, oDocument.Properties ) 
        {
            SSTVDocument = oDocument; // Don't bother check for null, will have thrown by now see above...
            _oViewSite   = oViewSite;
			Iconic       = ImageResourceHelper.GetImageResource( Assembly.GetExecutingAssembly(), _strViewIcon );
        }

        protected override void Dispose( bool disposing ) {
            if( disposing ) {
                DocForms.CaretRemove( Caret );
            }
            base.Dispose(disposing);
        }

        public override void InitRows() {
            if( Layout2 is SmartTable oTable ) {
                foreach( StdProperties.Names eName in Enum.GetValues(typeof(StdProperties.Names)) ) {
                    switch( eName ) {
                        case StdProperties.Names.TxPort:
                            PropertyInitRow( oTable, (int)eName, new CheckList( new WinSlot( this ), SSTVDocument.PortTxList ) );
                            break;
                        case StdProperties.Names.RxPort:
                            PropertyInitRow( oTable, (int)eName, new CheckList( new WinSlot( this ), SSTVDocument.PortRxList ) );
                            break;
                        default:
                            PropertyInitRow( oTable, (int)eName );
                            break;
                    }
                }
            }
        }

        public bool Load( XmlElement oStream ) {
            return true;
        }

        public bool Save( XmlDocumentFragment oStream ) {
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
