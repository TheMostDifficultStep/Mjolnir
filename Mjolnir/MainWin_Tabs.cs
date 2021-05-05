using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using System.Xml;

using SkiaSharp;

using Play.Interfaces.Embedding;
using Play.Rectangles;
using Play.Edit;
using Play.Forms;
using Play.ImageViewer;

namespace Mjolnir {
    /// <summary>
    /// Will need an instance per document per main window. Might be able to sneek by
    /// by simply tacking the Form document on the document site.
    /// </summary>
    public class Tabs : FormsWindow {
		readonly LayoutStack _rgLayoutTop = new LayoutStackHorizontal( 5 );

        public Tabs( IPgViewSite oSiteView, Editor oForm ) : base( oSiteView, oForm ) {
        }
    }
}
