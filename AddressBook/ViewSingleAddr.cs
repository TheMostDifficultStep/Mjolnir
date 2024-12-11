using System;
using System.Collections.Generic;
using System.Text;

using Play.Interfaces.Embedding;
using Play.Edit;
using Play.Rectangles;

namespace AddressBook {
    /// <summary>
    /// Use this to show all the names in the address book.
    /// </summary>
    public class ViewOutline : WindowMultiColumn 
    {
        public ViewOutline(IPgViewSite oViewSite, object oDocument) : base(oViewSite, oDocument) {
        }

        protected override bool Initialize() {
            if( !base.Initialize() )
                return false;

            TextLayoutAdd( new LayoutRect( LayoutRect.CSS.Flex ), (int)DocOutline.Column.LastName ); 
            TextLayoutAdd( new LayoutRect( LayoutRect.CSS.None ), (int)DocOutline.Column.FirstName ); 

            // Do this so we can return a desired height. O.o;;
            _oCacheMan.CacheRepair();

            return true;
        }
    }

    /// <summary>
    /// We'll just construct the address and put it in this editor.
    /// </summary>
    public class ViewSingleAddr : EditWindow2 {
		public static Guid ViewCategory {get;} = new Guid( "{CB6C6330-7152-4EB3-ABE3-9CFD93EA3B03}" );

        protected DocAddrBook Document { get; }

        public ViewSingleAddr( IPgViewSite oBaseSite, DocAddrBook oDocument ) : 
            base( oBaseSite, (BaseEditor)oDocument.Entry ) 
        {
            Document = oDocument ?? throw new ArgumentNullException();
        }
        public override object Decorate(IPgViewSite oBaseSite, Guid sGuid) {
            if( sGuid == GlobalDecor.Outline ) {
                return new ViewOutline( oBaseSite, Document.Outline );
            }

            return null;
        }
    } // end class
}
