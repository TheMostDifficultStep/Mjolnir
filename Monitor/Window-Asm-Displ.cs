using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Play.Edit;
using Play.Interfaces.Embedding;

namespace Monitor {
    internal class Window_Program_Display : EditWindow2 {
        public static Guid GUID { get; } = new Guid( "{1DBE2048-619C-44EA-882C-024DF5087743}" );

        public Window_Program_Display( 
            IPgViewSite oSiteView, 
            BaseEditor  p_oDocument, 
            bool        fReadOnly   = false, 
            bool        fSingleLine = false) : 
            base( oSiteView, p_oDocument, fReadOnly, fSingleLine ) 
        {
        }
    }
}
