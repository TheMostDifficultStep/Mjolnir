using Play.Drawing;
using Play.Forms;
using Play.Interfaces.Embedding;

using SkiaSharp;

using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;
using System.Xml;

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

    /// <remarks>
    /// I reasarched this, and Canvas.DrawBitmap() just creates an image
    /// on every call, so I'll just create a new image whenever 
    /// the levels change
    /// </remarks>
    public class DocImageLevels : DocImageBase,
        IPgSaveUrl
    {
        public LevelProperties Properties { get; protected set; }
        protected LevelAdjuster LevelAdj     { get; set; }
        protected readonly Dictionary<int, LevelAdjuster.Names > _rgMapper = [];

        public bool   IsDirty { get; protected set; }
        public string Moniker { get; protected set; }

        SKBitmap _bmpTarget;
        SKBitmap _bmpSource;

        public class ImageLevelsSlot : 
			IPgBaseSite
		{
			readonly DocImageLevels _oDoc;

			public ImageLevelsSlot( DocImageLevels oDoc ) {
				_oDoc = oDoc ?? throw new ArgumentNullException( "Image document must not be null." );
			}

			public void LogError( string strMessage, string strDetails, bool fShow=true ) {
				_oDoc._oSiteBase.LogError( strMessage, "ImageWalker : " + strDetails );
			}

			public void Notify( ShellNotify eEvent ) {
			}

			public IPgParent Host => _oDoc;
		}

        public DocImageLevels(IPgBaseSite oSiteBase) : base(oSiteBase) {
            Properties = new LevelProperties( new ImageLevelsSlot( this ) );
            LevelAdj   = new LevelAdjuster  ( midTones:110 );

            _rgMapper.Add( (int)LevelProperties.Names.Input_Low,    LevelAdjuster.Names.Low );
            _rgMapper.Add( (int)LevelProperties.Names.Input_Medium, LevelAdjuster.Names.Mid );
            _rgMapper.Add( (int)LevelProperties.Names.Input_High,   LevelAdjuster.Names.High );
            _rgMapper.Add( (int)LevelProperties.Names.Output_Low,   LevelAdjuster.Names.OutLow );
            _rgMapper.Add( (int)LevelProperties.Names.Output_High,  LevelAdjuster.Names.OutHigh );
        }

        /// <summary>
        /// Copy the byte values out of the Adjuster to the Property page document.
        /// </summary>
        public void MoveAdjusterToProps() {
            using DocProperties.Manipulator oProperties = Properties.CreateManipulator();
            foreach( KeyValuePair< int, LevelAdjuster.Names > oPair in _rgMapper ) {
                oProperties.SetValue( oPair.Key, LevelAdj.Levels[(int)oPair.Value].ToString() );
            }
        }

        public void MovePropsToAdjuster() {
            foreach( KeyValuePair< int, LevelAdjuster.Names > oPair in _rgMapper ) {
                LevelAdj.Levels[(int)oPair.Value] = (byte)Properties.ValueAsInt( oPair.Key );
            }
        }

        protected override bool Initialize() {
            if( !base.Initialize() )
                return false;

            if( !Properties.InitNew() ) 
                return false;

            MoveAdjusterToProps();

            Properties.SubmitEvent += SubmitEvent_Properties;

            return true;
        }

        private void SubmitEvent_Properties(int[] obj) {
            MovePropsToAdjuster();
            LevelAdj.Level( _bmpSource, _bmpTarget );
            Image = SKImage.FromBitmap(_bmpTarget );
            Raise_ImageUpdated();
            IsDirty = true;
        }

        public bool Load( string strFileName ) {
            //Image = SKImage.FromEncodedData( oStream );
            Initialize();

            try {
                if( File.Exists( strFileName ) ) {
                    Moniker    = strFileName;
                    using Stream oStream = File.OpenRead( strFileName );
                    _bmpSource = SKBitmap.Decode( oStream );
                    _bmpTarget = new SKBitmap( _bmpSource.Info );

                    LevelAdj.Level( _bmpSource, _bmpTarget );

                    Image = SKImage.FromBitmap( _bmpTarget );
				    return true;
                }
			} catch( Exception oEx ) {
				if( _rgBmpLoadErrs.IsUnhandled( oEx ) )
					throw;

                _oSiteBase.LogError( "storage", "Couldn't read file..." + strFileName );
			} finally {
                Raise_ImageUpdated(); 
            }
            return false;
        }

        public bool Save() {
            // Probably should ask if ok to overright file, or make it a
            // property...
            return false;
        }

    }

    public class ViewLevelsProperties : WindowStandardProperties {
        public ViewLevelsProperties(IPgViewSite oSiteView, DocProperties oDocument) : 
            base(oSiteView, oDocument) 
        {
        }

        protected override void OnKeyDown(KeyEventArgs e) {
            if( e.KeyCode == Keys.Enter ) {
                Document.Raise_Submit();
                e.Handled = true;
            }
            base.OnKeyDown(e);
        }  

    }

    public class ViewLevels : 
        ViewSingleImage,
        IPgCommandView, 
        IPgSave<XmlDocumentFragment>,
        IPgLoad<XmlElement>
    {
    	public static Guid Guid { get; } = new Guid("38B6762A-3D04-415A-9EBD-D29DBB618C37");

        DocImageLevels DocLevels { get; }
        public ViewLevels(IPgViewSite oSiteView, DocImageLevels oDocSolo) : 
            base(oSiteView, oDocSolo) 
        {
            DocLevels = oDocSolo ?? throw new ArgumentNullException();
        }

        public override bool InitNew() {
            if( !base.InitNew() )
                return false;
            return true;
        }

        string IPgCommandView.Banner => "View Levels";

        SKImage IPgCommandView.Icon => null;

        Guid IPgCommandView.Catagory => Guid;

        public bool IsDirty => false;

        object IPgCommandView.Decorate(IPgViewSite oBaseSite, Guid sGuid) {
            if( sGuid.Equals( GlobalDecor.Properties ) ) {
                return new ViewLevelsProperties( oBaseSite, DocLevels.Properties );
            }
            return null;
        }

        /// <summary>
        /// I've never used this feature. I might add it to the standard properties
        /// window later...
        /// </summary>
        bool IPgCommandBase.Execute(Guid sGuid) {
            if( sGuid == GlobalCommands.Save ) {
                return true;
            }
            // Just testing. Cr should trigger on prop page...
            if( sGuid == GlobalCommands.Play ) {
                DocLevels.Properties.Raise_Submit();
                return true;
            }

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
