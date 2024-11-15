using Play.Controls;
using Play.Edit;
using Play.Interfaces.Embedding;
using Play.Rectangles;

namespace Play.SSTV {
   public class ViewFamilyDDEditBox : ViewEditBox {
        public ViewFamilyDDEditBox(IPgViewSite oViewSite, object oDocument) : 
            base(oViewSite, oDocument) {
        }

        public override ViewDDPopup CreatePopup() {
            return new ViewSSTVFamilyPopup( new WinSlot( this ), _oDocBag );
        }
    }

    public class ViewSSTVFamilyPopup: ViewDDPopup {
        public ViewSSTVFamilyPopup( IPgViewSite oView, object oDocument ) : 
            base(oView, oDocument) 
        {
        }

        protected override bool Initialize() {
            if( !base.Initialize() )
                return false;

            // TODO: Check the width of a checkmark at the current font... :-/
            TextLayoutAdd( new LayoutRect( LayoutRect.CSS.Flex, CheckColumnWidth, 1L ), (int)SSTVRxFamilyDoc.Column.Check ); 
            TextLayoutAdd( new LayoutRect( LayoutRect.CSS.None, 20, 1L ), (int)SSTVRxFamilyDoc.Column.Family ); 

            // Do this so we can return a desired height. O.o;;
            _oCacheMan.CacheRepair();

            return true;
        }

    }

    public class ViewModeDDEditBox : ViewEditBox {
        public ViewModeDDEditBox(IPgViewSite oViewSite, object oDocument) : 
            base(oViewSite, oDocument) {
        }

        public override ViewDDPopup CreatePopup() {
            return new ViewSSTVModesPopup( new WinSlot( this ), _oDocBag );
        }
    }

    public class ViewSSTVModesPopup : ViewDDPopup {
        public ViewSSTVModesPopup( IPgViewSite oView, object oDocument ) : 
            base(oView, oDocument) 
        {
        }

        protected override bool Initialize() {
            if( !base.Initialize() )
                return false;

            // I'd really like to use flex. But that seems broken at present...
            TextLayoutAdd( new LayoutRect( LayoutRect.CSS.Flex,  CheckColumnWidth, 1L ), (int)SSTVModeDoc.Column.Check ); 
            TextLayoutAdd( new LayoutRect( LayoutRect.CSS.Flex, 20, 1L ), (int)SSTVModeDoc.Column.Version ); 
            TextLayoutAdd( new LayoutRect( LayoutRect.CSS.Flex, 20, 1L ), (int)SSTVModeDoc.Column.Time ); 
            TextLayoutAdd( new LayoutRect( LayoutRect.CSS.Flex, 20, 1L ), (int)SSTVModeDoc.Column.Width ); 
            TextLayoutAdd( new LayoutRect( LayoutRect.CSS.Flex, 20, 1L ), (int)SSTVModeDoc.Column.Height ); 

            // Do this so we can return a desired height. O.o;;
            _oCacheMan.CacheRepair();

            return true;
        }
    }

    public class ViewSSTVModesAsList : WindowMultiColumn {
        public ViewSSTVModesAsList(IPgViewSite oViewSite, object oDocument) : base(oViewSite, oDocument) {
            IsScrollVisible = false;
        }

        protected override bool Initialize() {
            if( !base.Initialize() )
                return false;

            // I'd really like to use flex. But that seems broken at present...
            TextLayoutAdd( new LayoutRect( LayoutRect.CSS.Flex,  CheckColumnWidth, 1L ), (int)SSTVModeDoc.Column.Check ); 
            TextLayoutAdd( new LayoutRect( LayoutRect.CSS.Flex, 20, 1L ), (int)SSTVModeDoc.Column.Version ); 
            TextLayoutAdd( new LayoutRect( LayoutRect.CSS.Flex, 20, 1L ), (int)SSTVModeDoc.Column.Time ); 
            TextLayoutAdd( new LayoutRect( LayoutRect.CSS.Flex, 20, 1L ), (int)SSTVModeDoc.Column.Width ); 
            TextLayoutAdd( new LayoutRect( LayoutRect.CSS.Flex, 20, 1L ), (int)SSTVModeDoc.Column.Height ); 

            // Do this so we can return a desired height. O.o;;
            _oCacheMan.CacheRepair();

            return true;
        }
    }
    public class ViewTxTemplatesDDEditBox : ViewEditBox {
        public ViewTxTemplatesDDEditBox(IPgViewSite oViewSite, object oDocument) : 
            base(oViewSite, oDocument) {
        }

        public override ViewDDPopup CreatePopup() {
            return new ViewTxTemplatesPopup( new WinSlot( this ), _oDocBag );
        }
    }

    public class ViewTxTemplatesPopup: ViewDDPopup {
        public ViewTxTemplatesPopup( IPgViewSite oView, object oDocument ) : 
            base(oView, oDocument) 
        {
        }

        protected override bool Initialize() {
            if( !base.Initialize() )
                return false;

            // TODO: Check the width of a checkmark at the current font... :-/
            TextLayoutAdd( new LayoutRect( LayoutRect.CSS.Flex, CheckColumnWidth, 1L ), (int)SSTVTxTemplatesDoc.Column.Check ); 
            TextLayoutAdd( new LayoutRect( LayoutRect.CSS.None, 20, 1L ), (int)SSTVTxTemplatesDoc.Column.Descr ); 

            // Do this so we can return a desired height. O.o;;
            _oCacheMan.CacheRepair();

            return true;
        }
    }
}
