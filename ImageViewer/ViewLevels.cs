using Play.Drawing;
using Play.Forms;
using Play.Interfaces.Embedding;

using SkiaSharp;

using System;
using System.Collections.Generic;
using System.Xml;
using static Play.ImageViewer.ImageLevelsDoc;

namespace Play.ImageViewer {
    public class LevelProperties : DocProperties {
        public enum Names : int {
            Input_Low,
            Input_Medium,
            Input_High,
            Output_Low,
            Output_High,
            MAX
        }

        readonly protected IPgRoundRobinWork _oWorkPlace; 

        public LevelProperties( IPgBaseSite oSiteBase ) : base( oSiteBase ) {
            IPgScheduler oSchedular = (IPgScheduler)Services;

            _oWorkPlace = oSchedular.CreateWorkPlace() ?? throw new InvalidOperationException( "Need the scheduler service in order to work. ^_^;" );
        }

        public override bool InitNew() {
            if( !base.InitNew() ) 
                return false;

            for( int i=0; i<(int)Names.MAX; ++i ) {
                CreatePropertyPair();
            }

            LabelUpdate( Names.Input_Low,    "Low" );
            LabelUpdate( Names.Input_Medium, "Medium", new SKColor( red:0xff, green:0xbf, blue:0 )  );
            LabelUpdate( Names.Input_High,   "High" );
            LabelUpdate( Names.Output_Low,   "Out Low" );
            LabelUpdate( Names.Output_High,  "Out High" );

            ValuesEmpty();

            return true;
        }

        public void LabelUpdate( Names eName, string strLabel, SKColor? skBgColor = null ) {
            LabelUpdate( (int)eName, strLabel, skBgColor );
        }

        public void ValueUpdate( Names eName, string strValue, bool Broadcast = false ) {
            ValueUpdate( (int)eName, strValue, Broadcast );
        }

        public DocProperties.Manipulator CreateManipulator() {
            return new DocProperties.Manipulator( this );
        }
        public override void DoParse() {
            _oWorkPlace.Queue( GetParseEnum(), iWaitMS:2000 );
        }

        public IEnumerator<int> GetParseEnum() {
            RenumberAndSumate();

            // We're not parsing but execute the levels
            // after two seconds.

            Raise_DocFormatted();

            yield return 0;
        }
    }

    public class ImageLevelsDoc : ImageSoloDoc {
        public ImageLevelsDoc(IPgBaseSite oSiteBase) : base(oSiteBase) {
        }

        protected override bool Initialize() {
            if( !base.Initialize() ) {
                return false;
            }

            return false;
        }
    }

    public class ViewLevels : 
        ViewSingleImage,
        IPgCommandView, 
        IPgSave<XmlDocumentFragment>,
        IPgLoad<XmlElement>
    {
    	public static Guid Guid { get; } = new Guid("38B6762A-3D04-415A-9EBD-D29DBB618C37");

        ImageLevelsDoc DocLevels { get; }
        public LevelProperties Properties { get; protected set; }

        public class ImageLevelsSlot : 
			IPgBaseSite
		{
			readonly ViewLevels _oDoc;

			public ImageLevelsSlot( ViewLevels oDoc ) {
				_oDoc = oDoc ?? throw new ArgumentNullException( "Image document must not be null." );
			}

			public void LogError( string strMessage, string strDetails, bool fShow=true ) {
				_oDoc.LogError( strMessage, "ImageWalker : " + strDetails );
			}

			public void Notify( ShellNotify eEvent ) {
			}

			public IPgParent Host => _oDoc;
		}
        public ViewLevels(IPgViewSite oSiteView, ImageLevelsDoc oDocSolo) : 
            base(oSiteView, oDocSolo) 
        {
            DocLevels = oDocSolo ?? throw new ArgumentNullException();
        }

        public override bool InitNew() {
            if( !base.InitNew() )
                return false;

            Properties = new LevelProperties( new ImageLevelsSlot( this ) );
            return true;
        }

        string IPgCommandView.Banner => "View Levels";

        SKImage IPgCommandView.Icon => null;

        Guid IPgCommandView.Catagory => Guid;

        public bool IsDirty => false;

        object IPgCommandView.Decorate(IPgViewSite oBaseSite, Guid sGuid) {
            if( sGuid.Equals( GlobalDecor.Properties ) ) {
                return new WindowStandardProperties( oBaseSite, Properties );
            }
            return null;
        }

        bool IPgCommandBase.Execute(Guid sGuid) {
            return false;
        }

        public bool Save(XmlDocumentFragment oStream) {
            return true;
        }

        public bool Load(XmlElement oStream) {
            return true;
        }
    }
}
