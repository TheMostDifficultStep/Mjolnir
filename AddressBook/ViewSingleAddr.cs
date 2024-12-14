using System;
using System.Collections.Generic;
using System.Text;

using Play.Interfaces.Embedding;
using Play.Edit;
using Play.Rectangles;
using System.Windows.Forms;

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

            TextLayoutAdd( new LayoutRect( LayoutRect.CSS.Flex ) {Track=10 }, (int)DocOutline.Column.Check ); 
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

        public override bool Execute(Guid sGuid) {
            if( sGuid == GlobalCommands.JumpNext ) {
                Document.Jump( 1 ); 
                return true;
            }
            if( sGuid == GlobalCommands.JumpPrev ) {
                Document.Jump( -1 );
                return true;
            }

            return false;
        }

        /// <summary>
        /// Turns out this is a case where we don't want to allow 
        /// ANY typing! O.o I should do more work to override the
        /// other cases but not now. >_<;;
        /// </summary>
        protected override void OnKeyDown(KeyEventArgs e) {
            if( IsDisposed )
                return;

            e.Handled = true;

            switch( e.KeyCode ) {
                case Keys.PageDown:
                    Document.Jump( 1 );
                    break;
                case Keys.PageUp:
                    Document.Jump( -1 );
                    break;
            }
        }
    } // end class
}
