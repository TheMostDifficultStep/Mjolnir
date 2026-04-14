using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

using Play.ImageViewer;
using Play.Interfaces.Embedding;
using Play.Drawing;

namespace Scanner {
    public class DocScanner :
        ImageBaseDoc,
        IPgLoad<TextReader>
    {
        HttpClient oHttpClient = new HttpClient();

        public class StdJob {
            public int    xResolution        = 300;
            public int    yResolution        = 300;
            public int    xStart             = 0;    
            public int    yStart             = 0;
            public int    width              = 2550; 
            public int    height             = 3300;
            public string documentFormat     = "image/jpeg"; 
            public int    compressionQFactor = 25;
            public string colorMode          = "RGB24";  
            public int    bitDepth           = 8; 
            public int    gamma              = 1000; 
            public int    brightness         = 1000; 
            public int    contrast           = 1000;
            public int    highlight          = 179; 
            public int    shadow             = 25; 

            public StdJob() {
            }
        }

        public DocScanner(IPgBaseSite oSiteBase) : base(oSiteBase) {
        }

        public override void Dispose() {
            oHttpClient.Dispose();
            base.Dispose();
        }

		protected override bool Initialize() {

 			return true;
		}

        public bool Load(TextReader oStream) {
            return Initialize();
        }

        public bool InitNew() {
            return Initialize();
        }
    } // end class
}
