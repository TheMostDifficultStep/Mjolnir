using System.Reflection;

using Play.Drawing;
using Play.Interfaces.Embedding;

namespace Play.Forms {
    public class DocumentDropDown : ImageBaseDoc {
        public DocumentDropDown(IPgBaseSite oSiteBase) : base(oSiteBase) {
            Bitmap = GetSKBitmapResource( Assembly.GetExecutingAssembly(), 
                                          "Play.Forms.Content.icons8-error-48.png" );
        }
    }
}
