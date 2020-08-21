using System;
using System.Drawing;
using System.Collections.Generic;

using Play.Rectangles;
using Play.Edit;

namespace Mjolnir
{
    /// <summary>
    /// This is a layout that contains the title bar (icon and text) and the guest view.
    /// Since it access the guest, it cannot be created in the ViewSlot's constructor!!
    /// </summary>
    /// <remarks>Consider moving this into the MainWin implementation. Plus, we'll move the
    /// CacheText element out and let the main window hold that so we can reuse the
    /// CacheText for all sibling views.</remarks>
    public class FrameletForView : LayoutStackVertical
    {
        readonly LayoutStackHorizontal _rgLayoutTitleBar;
        readonly LayoutText            _oLayoutText;

        public FrameletForView( ViewSlot oGuestSlot, uint uiTitleHeight ) : base(0) {
            if( oGuestSlot == null )
                throw new ArgumentNullException();

            _oLayoutText = new LayoutText( oGuestSlot.CacheTitle, CSS.None, 0 );

            _rgLayoutTitleBar = new LayoutStackHorizontal( 3 ) { Units = CSS.Pixels, Track = uiTitleHeight, Hidden = false };

            // BUG: This bombs out if the Slot doesn't have an Iconic image. Convert to SKBitmap.
         // _rgLayoutTitleBar.Add( new LayoutImage( oGuestSlot.Iconic, CSS.Flex ) { Track = uiTitleHeight } );
            _rgLayoutTitleBar.Add( _oLayoutText );

            this             .Add( _rgLayoutTitleBar );
            this             .Add( new LayoutControl( oGuestSlot.Guest, CSS.None ) );
        }

        public void Render( IntPtr hDC, IntPtr hScriptCache ) {
            if( !_oLayoutText.Hidden ) {
                _oLayoutText.Paint( hDC, hScriptCache, 0 );
            }
        }

        public bool TitleHidden { 
            get { return _rgLayoutTitleBar.Hidden; }
            set { _rgLayoutTitleBar.Hidden = value; }
        }
    }
}
