using System;
using System.Drawing;
using System.Windows.Forms;

using SkiaSharp.Views.Desktop;
using SkiaSharp;

using Play.Interfaces.Embedding;

namespace Play.SSTV {
    /// <summary>
    /// This is going to be our new resize window "dialog" box. It is nice in that it
    /// cannot be put behind it's owner window. If we click on any children in the owner,
    /// this window will still stay on top. It took me awhile to figure out this magic
    /// combo but now we have a child/sub window with a title that cannot be put behind
    /// our main form and this is EXACTLY what we want.
    /// </summary>
    public class WindowImageResize :
        Form,
        IPgParent,
        IPgLoad
    {
        protected readonly IPgViewSite _oSiteView;

        public IPgParent Parentage => _oSiteView.Host;
        public IPgParent Services  => Parentage.Services;

        public WindowImageResize( IPgViewSite oSite ) { 
            _oSiteView = oSite ?? throw new ArgumentNullException( nameof( oSite ) );
        }

        public bool InitNew() {
            Text = "Resize Image";

            Owner = (Form)Parentage.TopWindow;

            MinimizeBox = false;

            //AcceptButton = button1;
            //CancelButton = button2;

            Size = new Size( 300, 300 );

            return true;
        }

    }
}
