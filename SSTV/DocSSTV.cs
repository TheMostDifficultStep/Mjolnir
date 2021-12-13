using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;

using SkiaSharp;
using NAudio.Wave;

using Play.Rectangles;
using Play.Interfaces.Embedding;
using Play.Sound;
using Play.Sound.FFT;
using Play.Edit;
using Play.ImageViewer;
using Play.Forms;
using Play.Integration;

namespace Play.SSTV {
    /// <summary>
    /// This subclass of the DocProperties let's us have static index values. This is advantageous because it
    /// allows us to re-arrange property values without scrambling their meaning. But it also means you can't
    /// use some kind of runtime forms generator since the indicies must have corresponding pre compiled enum's.
    /// </summary>
    public class SSTVProperties : DocProperties {
        public enum Names : int {
			Rx_Mode,
            Rx_Width,
            Rx_Height,
            Rx_Progress,
            Rx_SaveDir,
            Rx_SaveName,

            Tx_Progress,
            Tx_SrcDir,
            Tx_SrcFile,
            Tx_MyCall,
            Tx_TheirCall,
            Tx_RST,
            Tx_Message,

            Std_MnPort,
			Std_TxPort,
            Std_RxPort,
            Std_ImgQuality,
            Std_Process,
            Std_MicGain
        }

        public SSTVProperties( IPgBaseSite oSiteBase ) : base( oSiteBase ) {
        }

        public override bool InitNew() {
            if( !base.InitNew() ) 
                return false;
            
            // Set up the parser so we get spiffy colorization on our text!! HOWEVER,
            // Some lines are not sending events to the Property_Values document and we
            // need to trigger the parse seperately.
            new ParseHandlerText( Property_Values, "text" );

            if( _oSiteBase.Host is not DocSSTV oSSTVDoc )
                return false;

            foreach( Names eName in Enum.GetValues(typeof(Names)) ) {
                Property_Labels.LineAppend( string.Empty, fUndoable:false );
                switch( eName ) {
                    case Names.Tx_SrcDir:
                        Property_Values.LineInsertNoUndo( Property_Values.ElementCount, oSSTVDoc.TxImageList.CurrentShowPath );
                        break;
                    case Names.Tx_SrcFile:
                        Property_Values.LineInsertNoUndo( Property_Values.ElementCount, oSSTVDoc.TxImageList.CurrentShowFile );
                        break;
                    case Names.Rx_SaveDir:
                        Property_Values.LineInsertNoUndo( Property_Values.ElementCount, oSSTVDoc.RxHistoryList.CurrentShowPath );
                        break;
                    default:
                        Property_Values.LineAppend( string.Empty, fUndoable:false );
                        break;
                }
            }

            // TODO: Need a way to clear the monitor check if don't want to monitor.
            LabelSet( Names.Std_MnPort,     "Monitor with Device" );
            LabelSet( Names.Std_TxPort,     "Transmit  to Device" );
            LabelSet( Names.Std_RxPort,     "Receive from Device" );
            LabelSet( Names.Std_ImgQuality, "Image Save Quality" );
            LabelSet( Names.Std_Process,    "Task Status" );
            LabelSet( Names.Std_MicGain,    "Output Gain < 30,000" );

            LabelSet( Names.Tx_MyCall,    "My    Call" );
            LabelSet( Names.Tx_TheirCall, "Their Call" );
            LabelSet( Names.Tx_RST,       "RST" );
            LabelSet( Names.Tx_Message,   "Message" );
            LabelSet( Names.Tx_Progress,  "Sent" );
            LabelSet( Names.Tx_SrcDir,    "Tx Source Dir" );
            LabelSet( Names.Tx_SrcFile,   "Filename" );

            LabelSet( Names.Rx_Mode,     "Mode", new SKColor( red:0xff, green:0xbf, blue:0 ) );
            LabelSet( Names.Rx_Width,    "Width" );
            LabelSet( Names.Rx_Height,   "Height" );
            LabelSet( Names.Rx_Progress, "Received" );
            LabelSet( Names.Rx_SaveName, "Filename" );
            LabelSet( Names.Rx_SaveDir,  "Save Dir" );

            InitValues();

            return true;
        }

        /// <summary>
        /// These are our default values. We'll look for them from a save file in the future.
        /// </summary>
        public void InitValues() {
            ValueUpdate( Names.Std_ImgQuality, "80", true );
            ValueUpdate( Names.Std_MicGain,    "10000" ); // Out of 30,000
        }

        /// <summary>
        /// Let's not do any clearing for the moment.
        /// </summary>
        public override void Clear() {
        }

        public void LabelSet( Names eName, string strLabel, SKColor? skBgColor = null ) {
            Property_Labels[(int)eName].TryAppend( strLabel );

            if( skBgColor.HasValue ) {
                ValueBgColor.Add( (int)eName, skBgColor.Value );
            }
        }

        public void ValueUpdate( Names eName, string strValue, bool Broadcast = false ) {
            ValueUpdate( (int)eName, strValue, Broadcast );
        }

        public string this[ Names eIndex ] {
            get {
                return Property_Values[(int)eIndex].ToString();
            }
        }

        public bool ValueAsBool( Names eIndex ) {
            return string.Compare( Property_Values[(int)eIndex].ToString(), "true", ignoreCase:true ) == 0;
        }

        public int ValueAsInt( Names eIndex ) {
            return int.Parse( Property_Values[(int)eIndex].ToString() );
        }
    }

    public enum TxThreadErrors {
        DrawingException,
        WorkerException,
        ReadException,
        DiagnosticsException,
    }

    public enum SSTVEvents {
        SSTVMode,
        FFT,
        UploadTime,
		DownLoadTime,
        DownLoadFinished,
        ThreadException,
        ThreadAbort,
        ThreadExit
    }

    public delegate void SSTVPropertyEvent( SSTVEvents eProp ); // Document properties change.

    public class TVMessage {
        public enum Message {
            TryNewMode,
            ExitWorkThread
        }

        public readonly Message _eMsg;
        public readonly object  _oParam;

        public TVMessage( Message eMsg, object oParam = null ) {
            _eMsg   = eMsg;
            _oParam = oParam;
        }
    }

    public struct SSTVMessage {
        public SSTVMessage( SSTVEvents eEvent, int iParam ) {
            Event = eEvent;
            Param = iParam;
        }

        public SSTVEvents Event;
        public int        Param;
    }

    public class DocSSTV :
        IPgParent,
        IPgLoad<TextReader>,
        IPgSave<TextWriter>,
        IDisposable
    {
        private bool disposedValue;

        readonly ConcurrentQueue<SSTVMessage> _rgBGtoUIQueue = new ConcurrentQueue<SSTVMessage>(); // From BG thread to UI thread.
        readonly ConcurrentQueue<double>      _rgDataQueue   = new ConcurrentQueue<double>();     // From UI thread to BG thread.
        readonly ConcurrentQueue<TVMessage>   _rgUItoBGQueue = new ConcurrentQueue<TVMessage>();

        Thread               _oThread  = null;
        WaveIn               _oWaveIn  = null;
        WaveOut              _oWaveOut = null;
        BufferedWaveProvider _oWaveBuf = null;
        BlockCopies          _oWaveReader  = null;

        /// <summary>
        /// This editor shows the list of modes we can modulate.
        /// </summary>
		public class ModeEditor : Editor {
			public ModeEditor(IPgBaseSite oSite) : base(oSite) {
				//new ParseHandlerText(this, "m3u");
			}

			public override WorkerStatus PlayStatus => ((DocSSTV)_oSiteBase.Host).PlayStatus;

            public SSTVMode ChosenMode {
                get {
                    if( CheckedLine?.Extra is SSTVMode oMode ) {
                        return oMode;
                    }
                    return null;
                }
            }
		}

		protected class DocSlot :
			IPgBaseSite,
            IPgFileSite
		{
			protected readonly DocSSTV _oHost;

			public DocSlot( DocSSTV oHost, string strFileBase = "Not Implemented" ) {
				_oHost = oHost ?? throw new ArgumentNullException( "Host" );
                FileBase = strFileBase ?? throw new ArgumentNullException("File base string" );
			}

			public IPgParent Host => _oHost;

            public void LogError(string strMessage, string strDetails, bool fShow=true) {
				_oHost.LogError( strDetails );
			}

			public void Notify( ShellNotify eEvent ) {
				// Might want this value when we close to save the current playing list!!
			}

            // Need these for the image viewer.
            public FILESTATS FileStatus   => FILESTATS.READONLY;
            public Encoding  FileEncoding => Encoding.Default;
            public string    FilePath     => "Not Implemented";
            public string    FileBase     { get; protected set; }
		}

        protected readonly IPgBaseSite       _oSiteBase;
		protected readonly IPgRoundRobinWork _oWorkPlace;

        public IPgParent Parentage => _oSiteBase.Host;
        public IPgParent Services  => Parentage;
        public bool      IsDirty   => false;

        public event SSTVPropertyEvent PropertyChange;

        public Editor              TemplateList  { get; }
        public Editor              MonitorList   { get; }
        public Editor              PortTxList    { get; } 
        public Editor              PortRxList    { get; }
        public Specification       RxSpec        { get; protected set; } = new Specification( 44100, 1, 0, 16 ); // Syncronous rc test, tx image 
        public ModeEditor          RxModeList    { get; }
        public ModeEditor          TxModeList    { get; }
        public ImageWalkerDir      TxImageList   { get; }
        public ImageWalkerDir      RxHistoryList { get; }
        public SSTVProperties      StdProperties { get; }
        public SKBitmap            TxBitmap      => TxImageList.Bitmap;
        internal ImageSoloDoc      TxBitmapSnip  { get; }  
        internal DocImageEdit      TxBitmapComp  { get; }

        protected Mpg123FFTSupport FileDecoder   { get; set; }

        // This is where our image and diagnostic image live.
		public ImageSoloDoc ReceiveImage { get; protected set; }
		public ImageSoloDoc SyncImage    { get; protected set; }

        // Some test stuff. 
        private DataTester _oDataTester;

		private double[] _rgFFTData; // Data input for FFT. Note: it get's destroyed in the process.
		private CFFT     _oFFT;
        public  int[]    FFTResult { get; protected set; }
        public  int      FFTResultSize { 
            get {
                if( _oFFT == null )
                    return 0;

                return _oFFT.Mode.TopBucket; // The 3.01kHz size we care about.
            }
        }

        /// <summary>
        /// Document type for SSTV transmit and receive using audio i/o
        /// </summary>
        /// <exception cref="ArgumentNullException" />
        /// <exception cref="ApplicationException" />
        public DocSSTV( IPgBaseSite oSite ) {
            _oSiteBase  = oSite ?? throw new ArgumentNullException( "Site must not be null" );
            _oWorkPlace = ((IPgScheduler)Services).CreateWorkPlace() ?? throw new ApplicationException( "Couldn't create a worksite from scheduler.");

            TemplateList  = new Editor        ( new DocSlot( this ) );
            RxModeList    = new ModeEditor    ( new DocSlot( this, "SSTV Rx Modes" ) );
            TxModeList    = new ModeEditor    ( new DocSlot( this, "SSTV Tx Modes" ) );
            TxImageList   = new ImageWalkerDir( new DocSlot( this ) );
            RxHistoryList = new ImageWalkerDir( new DocSlot( this ) );
            TxBitmapSnip  = new ImageSoloDoc  ( new DocSlot( this ) );
            TxBitmapComp  = new DocImageEdit  ( new DocSlot( this ) );
                          
            PortTxList    = new Editor        ( new DocSlot( this ) );
            PortRxList    = new Editor        ( new DocSlot( this ) );
            MonitorList   = new Editor        ( new DocSlot( this ) );
                          
			ReceiveImage  = new ImageSoloDoc( new DocSlot( this ) );
			SyncImage     = new ImageSoloDoc( new DocSlot( this ) );
                          
            StdProperties = new ( new DocSlot( this ) );
          //RecChooser    = new ( new DocSlot( this ) );
        }

        #region Dispose
        protected virtual void Dispose( bool disposing ) {
            if( !disposedValue ) {
                if( disposing ) {
                    if( TxBitmapSnip != null )
			            TxBitmapSnip.Dispose(); 

                    // If init new fails then this won't get created.
                    if( FileDecoder != null )
                        FileDecoder.Dispose();

                    ReceiveLiveStop();

                    RxHistoryList.ImageUpdated -= OnImageUpdated_RxImageList;
                    TxImageList  .ImageUpdated -= OnImageUpdated_TxImageList;
                }

                // TODO: free unmanaged resources (unmanaged objects) and override finalizer
                // TODO: set large fields to null
                disposedValue = true;
            }
        }

        // // TODO: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
        // ~DocSSTV()
        // {
        //     // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        //     Dispose(disposing: false);
        // }

        public void Dispose() {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method

            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
        #endregion

        public void LogError( string strMessage ) {
            _oSiteBase.LogError( "SSTV", strMessage );
        }

        /// <summary>
        /// Set up a sample signal for the FFT. This is for test purposes. I should use
        /// one of my DSP-Goodies functions but I'm still sorting that all out.
        /// </summary>
        /// <param name="oCtrl"></param>
        /// <param name="rgData"></param>
        protected static void LoadData( FFTControlValues oCtrl, List<double> rgData ) {
            // We need FFTSize number of samples.
			rgData.Clear();
            for( double t = 0; rgData.Count < oCtrl.FFTSize; t += 1 / oCtrl.SampBase ) {
                double dbSample = 0;
                    
                dbSample += 80 * Math.Sin( Math.PI * 2 *  400 * t);
                dbSample += 80 * Math.Sin( Math.PI * 2 * 1200 * t);
                dbSample += 80 * Math.Sin( Math.PI * 2 * 2900 * t);

                rgData.Add(dbSample);
            }
        }

        /// <summary>
        /// Walk the iterator of SSTVModes and populate the ModeList. This sets
        /// up the human readable names and maps to the associated mode.
        /// TODO: Move to the ModeEditor class.
        /// </summary>
        /// <param name="iterMode"></param>
        protected static void LoadModes( IEnumerator<SSTVMode> iterMode, Editor oEditor, bool fAddResolution=true) {
            using BaseEditor.Manipulator oBulk = oEditor.CreateManipulator();
            StringBuilder sbValue = new();

            while( iterMode.MoveNext() ) {
                SSTVMode oMode = iterMode.Current;

                sbValue.Clear();
                sbValue.Append( oMode.Name );
                if( fAddResolution ) {
                    sbValue.Append( " : " );
                    sbValue.Append( oMode.Resolution.Width.ToString() );
                    sbValue.Append( "x" );
                    sbValue.Append( oMode.Resolution.Height.ToString() );
                    sbValue.Append( " @ " );
                    sbValue.Append( ( oMode.ScanWidthInMS * oMode.Resolution.Height / oMode.ScanMultiplier / 1000 ).ToString( "0." ) );
                    sbValue.Append( "s" );
                }
                Line oLine = oBulk.LineAppendNoUndo( sbValue.ToString() );

                oLine.Extra = oMode;
            }
        }

        /// <summary>Find all the output devices available.</summary>
        /// <remarks>
        /// Trial run for when we actually need this.
        /// </remarks>
        public void FindOutputDevices() {
            IEnumerator<string> oIter  = MMHelpers.GetOutputDevices();
            List<string>        rgDevs = new List<string>();
            while( oIter.MoveNext() ) {
                rgDevs.Add( oIter.Current );
            }
        }

        public bool InitNew2() {
            _oFFT = new CFFT( FFTControlValues.FindMode( 8000 ) );

            List<double> rgFFTData = new List<double>();

			LoadData( _oFFT.Mode, rgFFTData );

            _rgFFTData = rgFFTData.ToArray();
			FFTResult  = new int   [_oFFT.Mode.FFTSize/2];

            return true;
        }

        /// <summary>
        /// This was an experiment to read a signal into the fft and take a look
        /// at the results. No longer in use. I'll archive it somehow later.
        /// </summary>
        /// <returns></returns>
        public bool InitNew3() {
	        //string strSong = @"C:\Users\Frodo\Documents\signals\1kHz_Right_Channel.mp3"; // Max signal 262.
            //string strSong = @"C:\Users\Frodo\Documents\signals\sstv-essexham-image01-martin2.mp3";
            string strSong = @"C:\Users\Frodo\Documents\signals\sstv-essexham-image02-scottie2.mp3";

			try {
                //FileDecoder = _oSound.CreateSoundDecoder( strSong );
                FileDecoder = new Mpg123FFTSupport( strSong );
			} catch( Exception oEx ) {
				Type[] rgErrors = { typeof( FileNotFoundException ),
									typeof( FileLoadException ), 
									typeof( FormatException ),
									typeof( NullReferenceException ),
                                    typeof( InvalidOperationException ) };
				if( rgErrors.IsUnhandled( oEx ) ) {
					throw oEx;
				}
				LogError( "Couldn't find file, or play, or continue to play format of : \"" );
				LogError( strSong );
			}

            _oFFT = new CFFT( FFTControlValues.FindMode( FileDecoder.Spec.Rate ) );

            _rgFFTData      = new double[_oFFT.Mode.FFTSize];
			FFTResult       = new int   [_oFFT.Mode.FFTSize/2];

            FileDecoder.Target = _rgFFTData;
            FileDecoder.Init( _oFFT.Mode.Decimation, 1 );

            return true;
        }

		public int MaxOutputDevice {
			get {
				IEnumerator<string> iterOutput = MMHelpers.GetOutputDevices();
				int                 iCount     = -1;
				while( iterOutput.MoveNext() ) {
					++iCount;
                    
				}
				return iCount;
			}
		}

        public bool InitNew() {
            if( !TemplateList.InitNew() ) // Might need to init differently b/c of load.
                return false;
            if( !RxModeList .InitNew() ) 
                return false;
            if( !TxModeList .InitNew() ) 
                return false;
			if( !TxBitmapSnip.InitNew() )
				return false;
            if( !TxBitmapComp.InitNew() )
                return false;

			if( !ReceiveImage.InitNew() )
				return false;
			if( !SyncImage   .InitNew() )
				return false;

            if( !MonitorList.InitNew() )
                return false;
            if( !PortTxList .InitNew() )
                return false;
            if( !PortRxList .InitNew() ) 
                return false;

            if( !StdProperties.InitNew() )
                return false;

            // Get these set up so our stdproperties get the updates.
            TxImageList  .ImageUpdated += OnImageUpdated_TxImageList;
            RxHistoryList.ImageUpdated += OnImageUpdated_RxImageList;

            TemplateList.LineAppend( "CQ" );
            TemplateList.LineAppend( "PnP Reply" );
            TemplateList.LineAppend( "General Msg" );
            
		    SyncImage   .Bitmap = new SKBitmap( 800, 616, SKColorType.Rgb888x, SKAlphaType.Unknown );
		    ReceiveImage.Bitmap = new SKBitmap( 800, 616, SKColorType.Rgb888x, SKAlphaType.Opaque  );

            SettingsInit();

            string strMyDocs = Environment.GetFolderPath( Environment.SpecialFolder.MyDocuments );
            if( !TxImageList.LoadURL( strMyDocs ) ) {
				LogError( "Couldn't find pictures tx directory for SSTV" );
                return false;
			}
            string strMyPics = StdProperties[SSTVProperties.Names.Rx_SaveDir].ToString();
            if( !RxHistoryList.LoadURL( strMyPics ) ) {
				LogError( "Couldn't find pictures history directory for SSTV" );
                return false;
            }

            RxModeList.LineAppend( "Auto", fUndoable:false );
            LoadModes( SSTVDEM.EnumModes(), RxModeList, fAddResolution:false );
            LoadModes( SSTVDEM.EnumModes(), TxModeList, fAddResolution:true  );

            // Set this after TxImageList load since the CheckedLine call will 
            // call Listen_ModeChanged and that calls the properties update event.
            RxModeList.CheckedLine = RxModeList[0];

            return true;
        }

        protected void InitDeviceList() {
			IEnumerator<string> iterOutput = MMHelpers.GetOutputDevices();

			for( int iCount = -1; iterOutput.MoveNext(); ++iCount ) {
                PortTxList .LineAppend( iterOutput.Current, fUndoable:false );
                MonitorList.LineAppend( iterOutput.Current, fUndoable:false );
			}

            IEnumerator<string> iterInput = MMHelpers.GetInputDevices();
			for( int iCount = -1; iterInput.MoveNext(); ++iCount ) {
                PortRxList.LineAppend( iterInput.Current, fUndoable:false );
			}
        }

        protected virtual void SettingsInit() {
            InitDeviceList();

            string strMyPics = Environment.GetFolderPath( Environment.SpecialFolder.MyPictures );

            // In the future, setting this value will send an event that will get forwarded to the chooser.
            StdProperties.ValueUpdate( SSTVProperties.Names.Rx_SaveDir, strMyPics );

            //RecChooser.LoadURL( strMyDocs );
        }

        protected void PropertiesTxReLoad() {
			string strFileName = TxImageList.CurrentFileName;
            string strFilePath = TxImageList.CurrentDirectory;

            //TxProperties.ValueUpdate( TxProperties.Names.Mode,     TxMode ); 
            StdProperties.ValueUpdate( SSTVProperties.Names.Tx_Progress, "0%" );
            //TxProperties.ValueUpdate( TxProperties.Names.FileName, strFileName, Broadcast:true );
		}

		protected void PropertiesRxTime( int iPercent ) {
            StdProperties.ValueUpdate( SSTVProperties.Names.Rx_Progress, iPercent.ToString() + "%", Broadcast:true );
		}

        protected void PropertyLoadFromXml( Editor rgList, XmlNode oElem ) {
            if( oElem == null )
                throw new ArgumentNullException( "oElem" );

            foreach( Line oLine in rgList ) {
                if( oElem.InnerText != null && oLine.Compare( oElem.InnerText ) == 0 ) {
                    rgList.CheckedLine = oLine;
                    break;
                }
            }
        }
        public bool Load( TextReader oStream ) {
            if( !InitNew() )
                return false;

            try {
                XmlDocument oDoc = new();

                oDoc.Load( oStream );

                XmlNode oRoot = oDoc.DocumentElement;
                foreach( XmlNode oNode in oRoot.ChildNodes ) { 
                    switch( oNode.Name ) {
                        case "RxDevice":
                            PropertyLoadFromXml( PortRxList, oNode );
                            break;
                        case "TxDevice":
                            PropertyLoadFromXml( PortTxList, oNode );
                            break;
                        case "MonitorDevice":
                            PropertyLoadFromXml( MonitorList, oNode );
                            break;
                        case "RxMode":
                            PropertyLoadFromXml( RxModeList, oNode );
                            break;
                        case "TxMode":
                            PropertyLoadFromXml( TxModeList, oNode );
                            break;
                        case "Template":
                            PropertyLoadFromXml( TemplateList, oNode );
                            break;
                        case "ImageQuality":
                            StdProperties.ValueUpdate( SSTVProperties.Names.Std_ImgQuality, oNode.InnerText );
                            break;
                        case "DigiOutputGain":
                            StdProperties.ValueUpdate( SSTVProperties.Names.Std_MicGain, oNode.InnerText );
                            break;
                        case "MyCall":
                            StdProperties.ValueUpdate( SSTVProperties.Names.Tx_MyCall, oNode.InnerText );
                            break;
                        case "Message":
                            StdProperties.ValueUpdate( SSTVProperties.Names.Tx_Message, oNode.InnerText );
                            break;
                        case "TxSrcDir":
                            TxImageList.LoadAgain( oNode.InnerText );
                            break;
                    }
                }
			} catch( Exception oEx ) {
                Type[] rgErrors = { typeof( NullReferenceException ),
                                    typeof( ArgumentNullException ),
                                    typeof( ArgumentException ),
                                    typeof( InvalidOperationException ),
                                    typeof( InvalidCastException ),
                                    typeof( XmlException ) };
                if( rgErrors.IsUnhandled( oEx ) ) 
                    throw;

				LogError( "Trouble loading MySSTV" );
			}
    
            return true;
        }

        public bool Save( TextWriter oStream ) {
			try {
                XmlDocument oDoc  = new ();
                XmlElement  oRoot = oDoc.CreateElement( "MySSTV" );
                oDoc.AppendChild( oRoot );

                Action<string, SSTVProperties.Names> StringProperty = delegate ( string strName, SSTVProperties.Names eProperty ) { 
                    XmlElement oElem = oDoc.CreateElement( strName );
                    oElem.InnerText = StdProperties[(int)eProperty].ToString();
                    oRoot.AppendChild( oElem ); 
                };
                Action<string, Editor> CheckProperty = delegate( string strName, Editor oEditor ) {
                    XmlElement oElem = oDoc.CreateElement( strName );
                    if( oEditor.CheckedLine != null ) {
                        oElem.InnerText = oEditor.CheckedLine.ToString();
                        oRoot.AppendChild( oElem );
                    }
                };

                CheckProperty ( "RxDevice",       PortRxList );
                CheckProperty ( "TxDevice",       PortTxList );
                CheckProperty ( "MonitorDevice",  MonitorList );
                CheckProperty ( "RxMode",         RxModeList );
                CheckProperty ( "TxMode",         TxModeList );
                CheckProperty ( "Template",       TemplateList );
                StringProperty( "ImageQuality",   SSTVProperties.Names.Std_ImgQuality );
                StringProperty( "DigiOutputGain", SSTVProperties.Names.Std_MicGain );
                StringProperty( "MyCall",         SSTVProperties.Names.Tx_MyCall );
                StringProperty( "Message",        SSTVProperties.Names.Tx_Message );
                StringProperty( "TxSrcDir",       SSTVProperties.Names.Tx_SrcDir );

                oDoc.Save( oStream );
			} catch( Exception oEx ) {
                Type[] rgErrors = { typeof( NullReferenceException ),
                                    typeof( ArgumentNullException ),
                                    typeof( ArgumentException ),
                                    typeof( InvalidOperationException ),
                                    typeof( XmlException )  };
                if( rgErrors.IsUnhandled( oEx ) ) 
                    throw;

				LogError( "Trouble saving MySSTV" );
			}
    
            return true;
        }

        /// <summary>
        /// BUG: This is a bummer but, I use a point for the aspect ratio in my
        /// SmartRect code. It should be a SKPointI too. I'll fix that later.
        /// </summary>
        public SKPointI TxResolution {
            get {
                try {
                    if( TxModeList.CheckedLine != null && TxModeList.CheckedLine.Extra is SSTVMode oMode )
                        return new SKPointI( oMode.Resolution.Width, oMode.Resolution.Height );
                } catch( NullReferenceException ) {
                    LogError( "Problem finding SSTVMode. Using default." );
                }
                return new SKPointI( 320, 240 );
            }
        }

        private void OnImageUpdated_TxImageList() {
            StdProperties.RaiseBufferEvent();
        }

        /// <summary>
        /// Strictly speaking we're not getting the event we really need here. 
        /// Look at this again later.
        /// </summary>
        private void OnImageUpdated_RxImageList() {
            // BUG: Need to make the RxProp the one that gets changed and we catch an event to LoadAgain();
			StdProperties.RaiseBufferEvent();
        }

        /// <summary>
        /// This is a mess. I need to sort out the messaging. Only the
        /// thread based system has been tested recently.
        /// </summary>
        /// <param name="eProp"></param>
        protected void Raise_PropertiesUpdated( SSTVEvents eProp ) {
            PropertiesTxReLoad();

            PropertyChange?.Invoke( eProp );
        }

        public string MyCall    => StdProperties[(int)SSTVProperties.Names.Tx_MyCall].ToString().ToUpper();
        public string Message   => StdProperties[(int)SSTVProperties.Names.Tx_Message].ToString();
        public string TheirCall => StdProperties[(int)SSTVProperties.Names.Tx_TheirCall].ToString().ToUpper();
        public string RST       => StdProperties[(int)SSTVProperties.Names.Tx_RST].ToString();

		public void TemplateSet( int iIndex ) {
			try {
				TxBitmapComp.Clear();

				switch( iIndex ) {
					case 0:
						TxBitmapComp.AddImage   ( LOCUS.CENTER,      0,  0, 100.0, TxBitmapSnip );
                        TxBitmapComp.AddGradient( LOCUS.TOP,                 24.0, SKColors.Blue, SKColors.Green );
						TxBitmapComp.AddText    ( LOCUS.UPPERLEFT,   5,  5,  24.0, TxBitmapComp.StdFace, "CQ de " + MyCall );
						break;
					case 1:
						TxBitmapComp.AddImage( LOCUS.CENTER,      0,  0, 100.0, TxBitmapSnip );
						TxBitmapComp.AddText ( LOCUS.UPPERLEFT,   5,  5,  15.0, TxBitmapComp.StdFace, TheirCall + " de " + MyCall + " " + RST );
						TxBitmapComp.AddImage( LOCUS.LOWERRIGHT, 10, 10,  30.0, RxHistoryList );
						break;
					case 2:
						TxBitmapComp.AddImage( LOCUS.CENTER,      0,  0, 100.0, TxBitmapSnip );
						TxBitmapComp.AddText ( LOCUS.UPPERLEFT,   5,  5,  20.0, TxBitmapComp.StdFace, Message );
						break;
				}

                // Count so the stream object on the editor will seek correctly amoung the lines.
                TxBitmapComp.Text.CharacterCount( 0 );
                TxBitmapComp.Text.Raise_BufferEvent( BUFFEREVENTS.MULTILINE );
			} catch( Exception oEx ) {
				Type[] rgErrors = { typeof( NullReferenceException ),
									typeof( ArgumentOutOfRangeException ) };

				if( rgErrors.IsUnhandled( oEx ) ) 
					throw;

				LogError( "Could apply image template." );
			}
		}

        protected class TxState : IEnumerable<int> {
            protected BufferSSTV    _oSSTVBuffer;      
            protected SSTVMOD       _oSSTVModulator;
            protected SSTVGenerator _oSSTVGenerator;
		    protected IPgPlayer     _oPlayer;

            protected ConcurrentQueue<SSTVMessage> _oBGtoFGQueue;

            public TxState( SSTVMode oMode, int iGain, int iDevice, SKBitmap skBitmap, ConcurrentQueue<SSTVMessage> oBGtoFGQueue ) {
                if( oMode == null )
                    throw new ArgumentNullException( nameof( oMode ) );
                if( skBitmap == null )
                    throw new ArgumentNullException( nameof( skBitmap ) );

                _oBGtoFGQueue = oBGtoFGQueue ?? throw new ArgumentNullException( nameof( oBGtoFGQueue ) );

                Specification oTxSpec = new( 11025, 1, 0, 16 );

                _oSSTVBuffer    = new BufferSSTV( oTxSpec );
				_oSSTVModulator = new SSTVMOD( 0, oTxSpec.Rate, _oSSTVBuffer, iGain );

				switch( oMode.Family ) {
					case TVFamily.PD:
						_oSSTVGenerator = new GeneratePD     ( skBitmap, _oSSTVModulator, oMode ); break;
					case TVFamily.Martin:
						_oSSTVGenerator = new GenerateMartin ( skBitmap, _oSSTVModulator, oMode ); break;
					case TVFamily.Scottie:
						_oSSTVGenerator = new GenerateScottie( skBitmap, _oSSTVModulator, oMode ); break;
					default:
						throw new ArgumentOutOfRangeException( nameof( oMode ) );
				}

                _oSSTVBuffer.Pump = _oSSTVGenerator.GetEnumerator();

                _oPlayer = new WmmPlayer(oTxSpec, iDevice );
            }

            protected void Raise_UploadPercent( int iPercent ) {
                _oBGtoFGQueue.Enqueue( new( SSTVEvents.UploadTime, iPercent ) );
            }
            protected void Raise_SendError() {
                _oBGtoFGQueue.Enqueue( new( SSTVEvents.ThreadException, (int)TxThreadErrors.WorkerException ) );
            }

            public IEnumerator<int> GetEnumerator() {
                do {
                    uint uiWait;
                    try {
                        uiWait = ( _oPlayer.Play( _oSSTVBuffer ) >> 1 ) + 1;
                    } catch( Exception oEx ) {
                        Type[] rgErrors = { typeof( NullReferenceException ),
                                            typeof( ArgumentNullException ),
                                            typeof( MMSystemException ) };
                        if( rgErrors.IsUnhandled( oEx ) )
                            throw;

                        Raise_SendError( );
                        yield break;
                    }
                    Raise_UploadPercent( _oSSTVGenerator.PercentTxComplete );

                    yield return (int)uiWait;
                } while( _oSSTVBuffer.IsReading );
            }

            IEnumerator IEnumerable.GetEnumerator() {
                return GetEnumerator();
            }
        }

        public WorkerStatus Status => _oWorkPlace.Status;

        protected int MicrophoneGain => StdProperties.ValueAsInt( SSTVProperties.Names.Std_MicGain );

        /// <summary>
        /// Begin transmitting the image.
        /// </summary>
        public async void TransmitBegin( SSTVMode oMode ) {
            if( PortTxList.CheckedLine == null ) {
                LogError( "No sound device to send to" );
                return;
            }
            if( oMode == null || TxBitmapComp.Bitmap == null ) {
                LogError( "Transmit mode or image is not set." );
                return;
            }

            Action oTransmitAction = delegate () {
                SKBitmap bmpCopy = TxBitmapComp.Bitmap.Copy();
                TxState oState   = new TxState( oMode, MicrophoneGain, PortTxList.CheckedLine.At, 
                                                bmpCopy, _rgBGtoUIQueue );
                foreach( uint uiWait in oState ) {
                    Thread.Sleep( (int)uiWait );
                }
                Thread.Sleep( 2000 ); // Let the buffer bleed out.
                bmpCopy.Dispose();
            };

            Task oTask = new Task( oTransmitAction );

            oTask.Start();

            await oTask;
        }

        /// <summary>
        /// I should probably save the image in the background thread just so that
        /// we don't have any race to see if the bg starts drawing on the image.
        /// </summary>
        /// <param name="strFileName"></param>
        protected void DownloadFinished() {
            SaveRxImage(); // Race condition possible, when image reused.

            PropertyChange?.Invoke( SSTVEvents.DownLoadFinished );
            StdProperties.ValueUpdate( SSTVProperties.Names.Rx_Progress, "Done", Broadcast:true );
            RxModeList.HighLight   = null;
            RxModeList.CheckedLine = RxModeList[0];

            // Might need to change this directory depending on listening to device
            // or loading from some random file location.
            // RxProperties[RxProperties.Names.SaveDir] vs
            // RecChooser.CurrentFullPath

            RxHistoryList.LoadAgain( RxHistoryList.CurrentDirectory );
        }

        protected readonly string[] _rgThreadStrings = { "Drawing Exception", "WorkerException", "ReadException", "DiagnosticsException" };

        /// <summary>
        /// This is our task to poll the Background to UI Queue. It services both
        /// the receive thread and the transmit thread. Technically they can run
        /// concurrently with no problems.
        /// </summary>
        public IEnumerator<int> GetTaskReceiver( ThreadWorkerBase oWorker ) {
            while( true ) {
                while( _rgBGtoUIQueue.TryDequeue( out SSTVMessage sResult ) ) {
                    switch( sResult.Event ) {
                        case SSTVEvents.ThreadAbort:
                            if( _oThread == null ) {
                                LogError( "Unexpected Image Thread Abort." );
                            } else { 
                                LogError( "Image Thread Abort." );
                            }
                            RxModeList.HighLight   = null;
                            RxModeList.CheckedLine = RxModeList[0];
                           _oThread = null;
                            yield break; // Bail out.
                        case SSTVEvents.SSTVMode: 
                            {
                                // TODO: this is a little evil asking the worker the mode that might
                                // be different by the time we process the event. Might want a
                                // parameter on the message. Try using the sResult.Param
                                SSTVMode oWorkerMode = oWorker.NextMode;
                                if( oWorkerMode == null ) {
                                    // We catch a null we're going back to listen mode.
                                    RxModeList.HighLight    = null;
                                    RxModeList.CheckedReset = RxModeList[0]; 
                                    StdProperties.ValueUpdate( SSTVProperties.Names.Rx_Mode,     "-" );
                                    StdProperties.ValueUpdate( SSTVProperties.Names.Rx_Width,    "-" );
                                    StdProperties.ValueUpdate( SSTVProperties.Names.Rx_Height,   "-" );
                                    StdProperties.ValueUpdate( SSTVProperties.Names.Rx_SaveName, string.Empty, Broadcast:true );
                                    break;
                                }

			                    ReceiveImage.WorldDisplay = new SKRectI( 0, 0, oWorkerMode.Resolution.Width, oWorkerMode.Resolution.Height );

                                string strFileName = Path.GetFileName( oWorker.SuggestedFileName );

                                StdProperties.ValueUpdate( SSTVProperties.Names.Rx_Mode,     oWorkerMode.Name );
                                StdProperties.ValueUpdate( SSTVProperties.Names.Rx_Width,    oWorkerMode.Resolution.Width .ToString() );
                                StdProperties.ValueUpdate( SSTVProperties.Names.Rx_Height,   oWorkerMode.Resolution.Height.ToString() );
                                StdProperties.ValueUpdate( SSTVProperties.Names.Rx_SaveName, strFileName, Broadcast:true );

                                foreach( Line oLine in RxModeList ) {
                                    if( oLine.Extra is SSTVMode oMode ) {
                                        if( oMode.LegacyMode == oWorkerMode.LegacyMode ) {
                                            RxModeList.HighLight = oLine;
                                        }
                                    }
                                }
                            } break;
                        case SSTVEvents.DownLoadTime: 
                            // Might be nice to send the % as a number in the message.
                            PropertiesRxTime( sResult.Param );
                            PropertyChange?.Invoke( SSTVEvents.DownLoadTime );
                            break;
                        case SSTVEvents.UploadTime:
                            StdProperties.ValueUpdate( SSTVProperties.Names.Tx_Progress, sResult.Param.ToString( "D2" ) + "%", true );
                            break;
                        case SSTVEvents.DownLoadFinished:
                            // NOTE: This might never come along!
                            DownloadFinished();
                            if( !oWorker.IsForever ) { 
                                _oThread = null;
                                yield break; // Bail out of this worker.
                            }
                            break;
                        case SSTVEvents.ThreadException:
                            try {
                                LogError( _rgThreadStrings[sResult.Param] );
                            } catch( IndexOutOfRangeException ) {
                                LogError( "General Thread Exception " + sResult.Param.ToString() );
                            }
                            break;
                    }
                }
                yield return 250; // wait 1/4 of a second.
            };
        }

        /// <summary>
        /// This is our TRUE multithreading experiment! Looks like it works
        /// pretty well. The decoder and filters and all live in the bg thread.
        /// The foreground tread only polls the bitmap from time to time.
        /// </summary>
        /// <param name="DetectVIS">Just set the decoder for a particular SSTV mode. This is usefull
        /// if picking up the signal in the middle and you know the type a priori. I should
        /// just pass the mode if it's fixed, else autodetect.</param>
        public void ReceiveFileReadBgThreadBegin( string strFileName, SSTVMode oMode ) {
            if( string.IsNullOrEmpty( strFileName ) ) {
                LogError( "Invalid filename for SSTV image read" );
                return;
            }
            if( _oThread != null ) {
                if( _oWorkPlace.Status == WorkerStatus.BUSY ) {
                    // If we're in the middle of an image, this'll just flash by.
                    StdProperties.ValueUpdate( SSTVProperties.Names.Rx_Progress, "Busy.", true );
                } else {
                    StdProperties.ValueUpdate( SSTVProperties.Names.Rx_Progress, "Not Busy... ^_^;", true );
                }
            }
            if( _oThread == null && _oWorkPlace.Status == WorkerStatus.FREE ) {
                ThreadWorker oWorker        = new ThreadWorker( _rgBGtoUIQueue, strFileName, oMode, SyncImage.Bitmap, ReceiveImage.Bitmap );
                ThreadStart  threadDelegate = new ThreadStart ( oWorker.DoWork );

                _oThread = new Thread( threadDelegate );
                _oThread.Start();

                _oWorkPlace.Queue( GetTaskReceiver( oWorker ), 1 );
                StdProperties.ValueUpdate( SSTVProperties.Names.Rx_Progress, "Start: File Read...", true );
            }
        }

        public void RequestModeChange( SSTVMode oMode ) {
            _rgUItoBGQueue.Enqueue( new TVMessage( TVMessage.Message.TryNewMode, oMode ) );
        }

        public void ReceiveLiveStop() {
            RxModeList.HighLight = null;

            StdProperties.ValueUpdate( SSTVProperties.Names.Std_Process, "Stopping...", true );

            _rgUItoBGQueue.Enqueue( new TVMessage( TVMessage.Message.ExitWorkThread ) );
            if( _oWaveOut != null ) {
                _oWaveOut.Stop();
                _oWaveOut = null;
            }
            if( _oWaveIn != null ) {
                _oWaveIn.StopRecording();   
                _oWaveIn = null;
            }

            StdProperties.ValueUpdate( SSTVProperties.Names.Std_Process, "Stopped: Wav I/O.", true );
            _oThread = null;
            _oWorkPlace.Stop();

            StdProperties.ValueUpdate( SSTVProperties.Names.Std_Process, "Stopped: All.", true );
        }

        /// <summary>
        /// This is our 2'nd threaded receive but straight from the device this time.
        /// </summary>
        public void ReceiveLiveBegin() {
            if( _oThread == null || !_oThread.IsAlive ) {
                try {
                    int  iMicrophone = -1; 
                    int  iMonitor    = -1;

                    if( PortRxList.CheckedLine != null ) {
                        iMicrophone = PortRxList.CheckedLine.At;
                    } else {
                        LogError( "Please select an sound input device" );
                        return;
                    }
                    if( MonitorList.CheckedLine != null ) {
                        iMonitor = MonitorList.CheckedLine.At;
                    }

                    if( _oWaveIn == null ) {
                        _oWaveIn = new WaveIn( WaveCallbackInfo.FunctionCallback() );

                        _oWaveIn.BufferMilliseconds = 250;
                        _oWaveIn.DeviceNumber       = iMicrophone;
                        _oWaveIn.WaveFormat         = new WaveFormat( 8000, 16, 1 );
                        _oWaveIn.NumberOfBuffers    = 2;
                        _oWaveIn.DataAvailable += OnDataAvailable_WaveIn;
                    }
                    if( _oWaveBuf == null ) {
                        _oWaveBuf = new BufferedWaveProvider(_oWaveIn.WaveFormat);
                    }
                    if( _oWaveOut != null && iMonitor < 0 ) {
                        if( _oWaveOut.PlaybackState == PlaybackState.Playing )
                            _oWaveOut.Stop();
                        _oWaveOut = null;
                    }

                    if( _oWaveOut == null && iMonitor >= 0 ) {
                        _oWaveOut = new WaveOut();
                        _oWaveOut.DeviceNumber = iMonitor;
                        _oWaveOut.Init( _oWaveBuf );
                    }
                    _oWaveReader = new BlockCopies( 1, 1, 0, _oWaveIn.WaveFormat.BitsPerSample );

                    _rgUItoBGQueue.Clear();
                    _rgBGtoUIQueue.Clear();
                    _rgDataQueue  .Clear();

                    // Note that this ModeList is the TX mode list. I think I want an RX list.
                    if( RxModeList.CheckedLine.Extra is SSTVMode oMode ) {
                        RequestModeChange( oMode );
                    }

                    ThreadWorker2 oWorker        = new ThreadWorker2( _oWaveIn.WaveFormat, _rgBGtoUIQueue, _rgDataQueue, 
                                                                      _rgUItoBGQueue, SyncImage.Bitmap, ReceiveImage.Bitmap );
                    ThreadStart   threadDelegate = new ThreadStart  ( oWorker.DoWork );

                    _oThread = new Thread( threadDelegate );
                    _oThread.Start(); // Can send out of memory exception!

                    _oWorkPlace.Queue( GetTaskReceiver( oWorker ), 1 );

                    _oWaveBuf?.ClearBuffer();
                    if( _oWaveOut != null ) {
                        if( _oWaveOut.PlaybackState != PlaybackState.Playing )
                            _oWaveOut?.Play();
                    }
                    if( _oWaveIn != null ) {
                        _oWaveIn.StopRecording();
                        _oWaveIn.StartRecording();
                    }

                    StdProperties.ValueUpdate( SSTVProperties.Names.Std_Process, "Start: Live.", true );
                } catch( Exception oEx ) {
                    Type[] rgErrors = { typeof( NullReferenceException ),
                                        typeof( ArgumentNullException ),
                                        typeof( FormatException ),
                                        typeof( OverflowException ),
                                        typeof( ThreadStateException ) };
                    if( rgErrors.IsUnhandled( oEx ) )
                        throw;

                    StdProperties.ValueUpdate( SSTVProperties.Names.Std_Process, "Start: Error.", true );
                    LogError( "Couldn't launch recording thread." );
                }
            }
        }

        /// <summary>
        /// Be sure to implement dispose on this object and make sure it get's
        /// called b/c this method will get called if the still living bg thread
        /// doesn't get an abort message.
        /// </summary>
        private void OnDataAvailable_WaveIn( object sender, WaveInEventArgs e ) {
            try {
                // This is the one that can return InvalidOperationException
                _oWaveBuf?.AddSamples( e.Buffer, 0, e.BytesRecorded );

                // No use stuffing the data queue if there's no thread to pick it up.
                if( _oThread != null ) {
                    for( IEnumerator<double>oIter = _oWaveReader.EnumAsSigned16Bit( e.Buffer, e.BytesRecorded ); oIter.MoveNext(); ) {
                        _rgDataQueue.Enqueue( oIter.Current );
                    }
                }
            } catch( InvalidOperationException ) {
                // Looks like I can't call wavein.stop() from within the callback. AND
                // it looks like NAudio is calling us from its non foreground thread!!!
                // Safest way to handle this is post an event to ourselves.
                _rgBGtoUIQueue.Enqueue( new( SSTVEvents.ThreadException, (int)TxThreadErrors.WorkerException ) );
            }
        }

        public static string GenerateFileName  {
            get {
                DateTime      sNow   = DateTime.Now.ToUniversalTime();
                StringBuilder sbName = new();

                sbName.Append( sNow.Year  .ToString( "D4" ) );
                sbName.Append( '-' );
                sbName.Append( sNow.Month .ToString( "D2" ) );
                sbName.Append( '-' );
                sbName.Append( sNow.Day   .ToString( "D2" ) );
                sbName.Append( '_' );
                sbName.Append( sNow.Hour  .ToString( "D2" ) );
                sbName.Append( sNow.Minute.ToString( "D2" ) );
                sbName.Append( 'z' );
               
                return sbName.ToString();
            }
        }

		public void SaveRxImage() {
            try {
                using ImageSoloDoc oSnipDoc = new( new DocSlot( this ) );

                if( !oSnipDoc.Load( ReceiveImage.Bitmap, ReceiveImage.WorldDisplay, ReceiveImage.Size ) )
                    return;

                // Figure out path and name of the file.
                string strFileName = StdProperties[ SSTVProperties.Names.Rx_SaveName ];
                string strSaveDir  = StdProperties[ SSTVProperties.Names.Rx_SaveDir  ];

                if( string.IsNullOrEmpty( strSaveDir ) ) {
			        strSaveDir = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
                }
                if( string.IsNullOrEmpty( strFileName ) ) {
                    strFileName = GenerateFileName;
                } else {
                    strFileName = Path.GetFileNameWithoutExtension( strFileName );
                }
                
                // Clean up the file name. Tacky, but better than nothing.
                TextLine oLine      = new( 0, strFileName );
                string   strIllegal = @" []$%&{}<>*?/\!:;+|=~`" + "\"\'";
                for( int i = 0; i< oLine.ElementCount; ++i ) {
                    foreach( char cBadChar in strIllegal ) {
                        if( oLine[i] == cBadChar )
                            oLine.Buffer[i] = '_';
                    }
                }
                strFileName = oLine.ToString();

                if( !int.TryParse( StdProperties[SSTVProperties.Names.Std_ImgQuality], out int iQuality ) )
                    iQuality = 80;

                string strFilePath = Path.Combine  ( strSaveDir, strFileName + ".jpg" );
                using var stream   = File.OpenWrite( strFilePath );

                oSnipDoc.Save( stream );
            } catch( Exception oEx ) {
                Type[] rgErrors = { typeof( NullReferenceException ),
                                    typeof( IOException ),
                                    typeof( ArgumentException ),
                                    typeof( ArgumentNullException ),
                                    typeof( PathTooLongException ),
                                    typeof( DirectoryNotFoundException ), 
                                    typeof( NotSupportedException ) };
                if( rgErrors.IsUnhandled( oEx ) )
                    throw;

                LogError( "Exception in Save" );
            }
		}

        /// <summary>
        /// This class contains the fragents of an early TV generation test. Because
        /// I've moved the transmission components to the TxState object this code
        /// was broken on the main DocSSTV object. I've moved it here to archive the
        /// test. But it will need work to make function again.
        /// </summary>
        private class RxTest {
            protected BufferSSTV _oSSTVBuffer;      
		    protected SSTVDraw   _oRxSSTV;          // Only used by test code.
            protected SSTVDEM    _oSSTVDeModulator; // Only used by test code.
            protected SSTVMOD    _oSSTVModulator;
            protected SSTVGenerator _oSSTVGenerator;
            protected DocSSTV _oDoc;

		    /// <summary>
		    /// In this 1'st test we skip the VIS and simply test video transmit and receive.
		    /// The CSSTVDEM object is not used in these tests. The transmit and receive
		    /// are set from the given mode.
		    /// </summary>
		    /// <param name="oMode">User selected mode. Any should work. Tho' only
            /// one will decode properly of course.</param>
		    /// <returns>Time in ms before next call wanted.</returns>
            public IEnumerator<int> GetTaskRecordTest1( SSTVMode oMode ) {
			    if( oMode == null )
				    throw new ArgumentNullException( "Mode must not be Null." );

			    IEnumerator<int> oIter = _oSSTVGenerator.GetEnumerator();

			    oIter            .MoveNext(); // skip the VIS for now.
			    _oSSTVDeModulator.SstvSet.SetMode( oMode.Family ); // BUG: Move this into Start();
			    _oSSTVDeModulator.Start( oMode );

                while( oIter.MoveNext() ) {
				    _oRxSSTV.Process();
				    yield return 1;
			    };
		    }

		    /// <summary>
		    /// This 1'st test generates the the video signal but doesn't actually create audio
		    /// tone stream but a fake stream of frequency data. Thus skipping the A/D coverter code
		    /// we're just testing the video encode / decode. We do this by re-assigning
		    /// the _oSSTVModulator with a new one set to our test frequency. It takes a snip of
            /// the current TxBitmap and uses that to test with.
		    /// </summary>
		    /// <remarks>Set CSSTVDEM::m_fFreeRun to false!!</remarks>
            public void ReceiveTestBegin( SKRectI skSelect, SSTVMode oMode, DocSSTV oDoc, IPgRoundRobinWork oWorkPlace ) {
                _oDoc = oDoc ?? throw new ArgumentNullException( nameof( oDoc ) );

                try {
                    if( oWorkPlace.Status == WorkerStatus.FREE ) {
			            oDoc.TxBitmapSnip.Load( oDoc.TxBitmap, skSelect, oMode.Resolution );

                        // Use a low sample rate so it's easier to slog thru the data. 
                        Specification oTxSpec = new( 8000, 1, 0, 16 );

					    FFTControlValues oFFTMode  = FFTControlValues.FindMode( oTxSpec.Rate ); // RxSpec.Rate
					    SYSSET           sys       = new ();
					    DemodTest        oDemodTst = new DemodTest( sys,
															        oFFTMode.SampFreq, 
															        oFFTMode.SampBase, 
															        0 );

                        _oSSTVBuffer      = new BufferSSTV( oTxSpec );
					    _oSSTVDeModulator = oDemodTst;
					    _oSSTVModulator   = new SSTVMOD( 0, oFFTMode.SampFreq, _oSSTVBuffer );
					    _oRxSSTV          = new SSTVDraw ( _oSSTVDeModulator, oDoc.SyncImage.Bitmap, oDoc.ReceiveImage.Bitmap );

					    _oSSTVGenerator = oMode.Family switch {
						    TVFamily.PD      => new GeneratePD     ( oDoc.TxBitmapSnip.Bitmap, oDemodTst, oMode ),
						    TVFamily.Martin  => new GenerateMartin ( oDoc.TxBitmapSnip.Bitmap, oDemodTst, oMode ),
						    TVFamily.Scottie => new GenerateScottie( oDoc.TxBitmapSnip.Bitmap, oDemodTst, oMode ),

						    _ => throw new ArgumentOutOfRangeException("Unrecognized Mode Type."),
					    };

                        _oSSTVDeModulator.Send_NextMode += OnNextMode_SSTVDeMod;

                        oWorkPlace.Queue( GetTaskRecordTest1( oMode ), 0 );
                    }
                } catch( NullReferenceException ) {
                    LogError( "Ooops didn't pick up mode (I think)" );
                }
            }

            void LogError( string strMessage ) {
            }

		    /// <summary>
		    /// This is used by the simple non-threaded test code. TODO: I can probably
            /// roll this into ListenTvEvents below there...
		    /// </summary>
		    /// <remarks>
		    /// I switched to a TmmSSTV that understands all the modes and is switched
            /// between them on the fly. The benefit is that I don't need to set
            /// up the event hooks everytime a new image comes down in the case where I
            /// was alloc'ing TmmSSTV subclasses.</remarks>
            /// <seealso cref="OnTvEvents_RxSSTV"/>
            private void OnNextMode_SSTVDeMod( SSTVMode tvMode ) {
			    _oDoc.ReceiveImage.Bitmap = null;
			    _oDoc.SyncImage   .Bitmap = null;

                _oRxSSTV.OnModeTransition_SSTVMod( tvMode ); // bitmap allocated in here. (may throw exception...)

			    _oDoc.ReceiveImage.WorldDisplay = new SKRectI( 0, 0, tvMode.Resolution.Width, tvMode.Resolution.Height );

                foreach( Line oLine in _oDoc.RxModeList ) {
                    if( oLine.Extra is SSTVMode oLineMode ) {
                        if( oLineMode.LegacyMode == tvMode.LegacyMode )
                            _oDoc.RxModeList.HighLight = oLine;
                    }
                }
            }
	        /// <summary>
	        /// Forward events coming from SSTVDraw. I really need to sort out
            /// the mess this has become. BUT this is only used by the TEST
            /// code. The new threaded code does not use this.
	        /// </summary>
            private void OnTvEvents_RxSSTV( SSTVEvents eProp ) {
                _oDoc.Raise_PropertiesUpdated( eProp );

                switch( eProp ) {
                    case SSTVEvents.DownLoadFinished:
                        _oDoc.RxModeList.HighLight = null;
                        _oDoc.SaveRxImage();
                        break;
                }
            }

        }

		public WorkerStatus PlayStatus {
            get {
                return _oWorkPlace.Status;
            }
        }

        public void TransmitStop() {
            TxModeList.HighLight = null;

            _oWorkPlace.Stop();
        }

        /// <summary>
        /// Little bit of test code for the fft.
        /// </summary>
        public void PlaySegment() {
            try {
                FileDecoder.Read();

                _oFFT.Calc( _rgFFTData, 10, 0, FFTResult );

                Raise_PropertiesUpdated( SSTVEvents.FFT );
            } catch( Exception oEx ) {
                Type[] rgErrors = { typeof( NotImplementedException ),
                                    typeof( NullReferenceException ) };
                if( rgErrors.IsUnhandled( oEx ) )
                    throw;
            }
        }
    } // End class
}
