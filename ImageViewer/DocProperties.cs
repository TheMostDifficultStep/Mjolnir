using System;

using Play.Forms;
using Play.Interfaces.Embedding;

namespace Play.ImageViewer {

    internal class ImageSnipProperties : DocProperties {
		public enum Labels : int {
			Format = 0,
			Quality,
			Width,
			Height,
			Fixed,
			FilePath,
			FileName
		}

        public ImageSnipProperties(IPgBaseSite oSiteBase) : base(oSiteBase) {
        }

        public override bool InitNew() {
            if( !base.InitNew() )
				return false;

            foreach( Labels eLabel in Enum.GetValues(typeof(Labels))) {
				CreatePropertyPair( eLabel.ToString() );
            }

            ValueUpdate( (int)Labels.Format,  "jpg"   );
			ValueUpdate( (int)Labels.Quality, "80"    );
			ValueUpdate( (int)Labels.Height,  "100"   );
			ValueUpdate( (int)Labels.Width,   "100"   );
			ValueUpdate( (int)Labels.Fixed,   "Width" );

			return true;
        }
    }
}
