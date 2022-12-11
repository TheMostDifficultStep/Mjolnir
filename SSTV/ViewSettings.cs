﻿using System;
using System.Xml;
using System.Drawing;

using SkiaSharp;

using Play.Forms;
using Play.Interfaces.Embedding;
using Play.Rectangles;

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
        IPgLoad<XmlElement>,
        IPgSave<XmlDocumentFragment>,
        IPgParent,
        IPgCommandView
    {
        public static Guid GUID {get;} = new Guid("{5B8AC3A1-A20C-431B-BA13-09314BA767FC}");

        private   readonly string      _strViewIcon  = "Play.SSTV.Content.icons8_settings.png";
        protected readonly IPgViewSite _oViewSite;

        public Guid      Catagory  => GUID; 
        public string    Banner    => "MySSTV Settings";
        public SKBitmap  Icon    { get; }
        public Image     Iconic => null;
        public bool      IsDirty   => false;
        public IPgParent Parentage => _oViewSite.Host;
        public IPgParent Services  => Parentage.Services;

        public DocSSTV SSTVDocument { get; }

        public ViewSettings( IPgViewSite oViewSite, DocSSTV oDocument ) :
            base( oViewSite, oDocument.Properties ) 
        {
            SSTVDocument = oDocument; // Don't bother check for null, will have thrown by now see above...
            _oViewSite   = oViewSite;
			Icon         = oDocument.CreateIconic( _strViewIcon );
        }

        protected override void Dispose( bool disposing ) {
            if( disposing ) {
                DocForms.CaretRemove( Caret );
            }
            base.Dispose(disposing);
        }

        public override void InitRows() {
            SSTVProperties.Names[] rgShow = { SSTVProperties.Names.Std_MnPort, 
                                              SSTVProperties.Names.Std_TxPort, 
                                              SSTVProperties.Names.Std_RxPort, 
                                              SSTVProperties.Names.Tx_MyCall,
                                              SSTVProperties.Names.Std_ImgQuality,
                                              SSTVProperties.Names.Std_MicGain,
                                              SSTVProperties.Names.Std_Frequency };

            if( Layout is SmartTable oTable ) {
                foreach( SSTVProperties.Names eName in rgShow ) {
                    switch( eName ) {
                        case SSTVProperties.Names.Std_TxPort:
                            PropertyInitRow( oTable, (int)eName, new CheckList( new WinSlot( this ), SSTVDocument.PortTxList ) );
                            break;
                        case SSTVProperties.Names.Std_MnPort:
                            PropertyInitRow( oTable, (int)eName, new CheckList( new WinSlot( this ), SSTVDocument.MonitorList ) );
                            break;
                        case SSTVProperties.Names.Std_RxPort:
                            PropertyInitRow( oTable, (int)eName, new CheckList( new WinSlot( this ), SSTVDocument.PortRxList ) );
                            break;
                        default:
                            PropertyInitRow( oTable, (int)eName ); // This creates a regular cacheline.
                            break;
                    }
                }
            }
        }

        public override bool InitNew() {
            if( ! base.InitNew() )
                return false;

            Layout.Padding.SetRect( 5, 0, 5, 0 ); // Table's don't respond to this yet.

            return true;
        }

        public bool Load( XmlElement oStream ) {
            if( !InitNew() )
                return false;

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
