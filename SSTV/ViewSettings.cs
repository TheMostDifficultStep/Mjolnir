using System;
using System.Xml;
using System.Drawing;

using SkiaSharp;

using Play.Forms;
using Play.Interfaces.Embedding;
using Play.Rectangles;
using Play.Edit;
using SkiaSharp.Views.Desktop;

namespace Play.SSTV {
    /// <summary>
    /// Implements a property page that can contain simple Line elements OR whole Editor's. 
    /// It does this by keeping a flat Editor containing properties, but if you have a
    /// multiline property, we don't use the Document.Settings_Values but create a cache
    /// element pointing to an edit window, instead of a standard cache line.
    /// </summary>
    /// <remarks>TODO: I probably should inherit from a ViewStandardProperties object and
    /// remove our direct DocSSTV dependency.</remarks>
    public class ViewSettings :
        WindowStandardProperties,
        IPgCommandView
    {
        public static Guid GUID {get;} = new Guid("{5B8AC3A1-A20C-431B-BA13-09314BA767FC}");

        private   readonly string _strViewIcon  = "Play.SSTV.Content.icons8_settings.png";

        public Guid      Catagory  => GUID; 
        public string    Banner    => "MySSTV Settings";
        public SKBitmap  Icon    { get; }
        public Image     Iconic => null;
        public override bool IsDirty => false;

        public DocSSTV SSTVDocument { get; }

        public ViewSettings( IPgViewSite oViewSite, DocSSTV oDocument ) :
            base( oViewSite, oDocument.Properties ) 
        {
            SSTVDocument = oDocument; // Don't bother check for null, will have thrown by now see above...
			Icon         = oDocument.CreateIconic( _strViewIcon );
        }

        public override void InitRows() {
            SSTVProperties.Names[] rgShow = { SSTVProperties.Names.Std_MnPort, 
                                              SSTVProperties.Names.Std_TxPort, 
                                              SSTVProperties.Names.Std_RxPort, 
                                              SSTVProperties.Names.Tx_MyCall,
                                              SSTVProperties.Names.Std_ImgQuality,
                                              SSTVProperties.Names.Std_MicGain,
                                              SSTVProperties.Names.Std_Frequency };

            foreach( SSTVProperties.Names eName in rgShow ) {
                switch( eName ) {
                    case SSTVProperties.Names.Std_TxPort:
                        PropertyInitRow( (int)eName, new CheckList( new WinSlot( this ), SSTVDocument.PortTxList ) { ScrollVisible = false } );
                        break;
                    case SSTVProperties.Names.Std_MnPort:
                        PropertyInitRow( (int)eName, new CheckList( new WinSlot( this ), SSTVDocument.MonitorList ) { ScrollVisible = false } );
                        break;
                    case SSTVProperties.Names.Std_RxPort:
                        PropertyInitRow( (int)eName, new CheckList( new WinSlot( this ), SSTVDocument.PortRxList ) { ScrollVisible = false } );
                        break;
                    default:
                        PropertyInitRow( (int)eName ); // This creates a regular cacheline.
                        break;
                }
            }
        }

        public object Decorate(IPgViewSite oBaseSite, Guid sGuid) {
            return null;
        }

        protected override void OnPaintSurface(SKPaintSurfaceEventArgs e) {
            base.OnPaintSurface(e);
        }

        protected override void OnSizeChanged(EventArgs e) {
            base.OnSizeChanged(e);
        }

        public bool Execute(Guid sGuid) {
            return false;
        }
    }
}
