using System;
using System.Collections.Generic;
using System.Text;

using Play.Interfaces.Embedding;
using Play.Edit;

namespace AddressBook {

    /// <summary>
    /// We'll just construct the address and put it in this editor.
    /// </summary>
    public class ViewSingleAddr : EditWindow2 {
		public static Guid ViewCategory {get;} = new Guid( "{CB6C6330-7152-4EB3-ABE3-9CFD93EA3B03}" );

        protected DocAddrBook Document { get; }

        public ViewSingleAddr( IPgViewSite oBaseSite, DocAddrBook oDocument ) : 
            base( oBaseSite, (BaseEditor)oDocument.Outline ) 
        {
            Document = oDocument ?? throw new ArgumentNullException();
        }
        public override object Decorate(IPgViewSite oBaseSite, Guid sGuid) {
            if( sGuid == GlobalDecor.Outline ) {
                return new EditWindow2( oBaseSite, Document.Outline );
            }

            return null;
        }
    } // end class
}
