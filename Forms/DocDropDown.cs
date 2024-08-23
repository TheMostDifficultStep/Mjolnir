using System.Reflection;

using Play.Drawing;
using Play.Interfaces.Embedding;
using Play.Edit;
using System.Collections.Generic;
using System;

namespace Play.Forms {
    public class ImageForDropDown : ImageBaseDoc {
        public ImageForDropDown(IPgBaseSite oSiteBase) : base(oSiteBase) {
            Bitmap = GetSKBitmapResource( Assembly.GetExecutingAssembly(), 
                                          "Play.Forms.Content.icons8-list-64.png" );
        }
    }

}
