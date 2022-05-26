using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Play.Interfaces.Embedding;
using Play.ImageViewer;

namespace Play.SSTV {
    /// <summary>
    /// I'm going to put this image editor in my SSTV project for now. But it'll probably belong
    /// in it's own project in the ImageViewer project in the future. Let's see how it goes.
    /// </summary>
    public class ViewEditBitmap : ImageViewSingle {
        protected DocImageEdit _oDocComposite;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="oSiteBase">The usual</param>
        /// <param name="oDocSolo">While it gives us a single bitmap. It will be constructed
        /// from the composition of the layout. So we'll subclass that so we'll have
        /// access to the layers. But the ImageViewSingle shouldn't need those.</param>
        public ViewEditBitmap(IPgViewSite oSiteBase, DocImageEdit oDocSolo) : base(oSiteBase, oDocSolo) {
            _oDocComposite = oDocSolo ?? throw new ArgumentNullException(nameof(oDocSolo));
        }
    }
}
