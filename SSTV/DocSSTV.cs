using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Reflection;

using SkiaSharp;
using NAudio.Wave;

using Play.Drawing;
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
            Std_MicGain,
            Std_Frequency,
            Std_Time
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
            LabelSet( Names.Std_Frequency,  "Frequency" ); // TODO: Give it yellow if calibrated value different than base.
            LabelSet( Names.Std_Time,       "Zulu Time" );

            LabelSet( Names.Tx_MyCall,    "My Call" );
            LabelSet( Names.Tx_TheirCall, "Rx Call" );
            LabelSet( Names.Tx_RST,       "RSV" ); // Readibility, strength, video
            LabelSet( Names.Tx_Message,   "Message" );
            LabelSet( Names.Tx_Progress,  "Sent", new SKColor( red:0xff, green:0xbf, blue:0 ) );
            LabelSet( Names.Tx_SrcDir,    "Tx Source Dir" );
            LabelSet( Names.Tx_SrcFile,   "Filename" );

            LabelSet( Names.Rx_Mode,     "Mode", new SKColor( red:0xff, green:0xbf, blue:0 ) );
            LabelSet( Names.Rx_Width,    "Width" );
            LabelSet( Names.Rx_Height,   "Height" );
            LabelSet( Names.Rx_Progress, "Received" );
            LabelSet( Names.Rx_SaveDir,  "Rx Save Dir" );

            // Initialize these to reasonable values, the user can update and save.
            ValueUpdate( Names.Std_ImgQuality, "80" );
            ValueUpdate( Names.Std_MicGain,    "10000" ); // Out of 30,000
            ValueUpdate( Names.Std_Frequency,  "11028" ); // Calibrated value. 11023.72 for me ^_^.

            return true;
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

        public bool GetValueAsBool( Names eIndex ) {
            return string.Compare( Property_Values[(int)eIndex].ToString(), "true", ignoreCase:true ) == 0;
        }

        public int GetValueAsInt( Names eIndex, int? iDefault = null ) {
            if( iDefault.HasValue ) {
                if( !int.TryParse( Property_Values[(int)eIndex].ToString(), out int iValue ) ) {
                    iValue = iDefault.Value;
                    ValueBgColor.Add( (int)eIndex, SKColors.LightPink ); // Sigh...Need some way to go back ^_^;
                }

                return iValue;
            }

            return int.Parse( Property_Values[(int)eIndex].ToString() );
        }

        public double GetValueAsDbl( Names eIndex, double? dblDefault = null ) {
            Line oProperty = Property_Values[(int)eIndex];

            if( dblDefault.HasValue ) {
                if( !double.TryParse( oProperty.ToString(), out double dblValue ) ) {
                    dblValue = dblDefault.Value;
                    ValueBgColor.Add( (int)eIndex, SKColors.LightPink ); 
                }

                return dblValue;
            }

            return double.Parse( oProperty.ToString() );
        }
    }

    public enum TxThreadErrors {
        DrawingException,
        WorkerException,
        ReadException,
        DiagnosticsException,
        DataOverflow
    }

    public enum SSTVEvents {
        ModeChanged,
        FFT,
        UploadTime,
		DownLoadTime,
        DownLoadFinished,
        ImageSaved,
        ThreadException,
        ThreadAbort,
        ThreadExit
    }

    public delegate void SSTVPropertyEvent( SSTVEvents eProp ); // Document properties change.

    public class TVMessage {
        public enum Message {
            SaveNow,
            TryNewMode,
            ChangeDirectory,
            ExitWorkThread,
            FrequencyUp,
            FrequencyDown,
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

    /// <summary>
    /// This is the main document object for my SSTV receiver.
    /// </summary>
    public class DocSSTV :
        IPgParent,
        IPgLoad<TextReader>,
        IPgSave<TextWriter>,
        IDisposable
    {
        private bool disposedValue;

        public enum DocSSTVMode {
            Ready,
            FileRead,
            DeviceRead
        }

        readonly ConcurrentQueue<SSTVMessage> _rgBGtoUIQueue = new ConcurrentQueue<SSTVMessage>(); // From BG thread to UI thread.
        readonly ConcurrentQueue<double>      _rgDataQueue   = new ConcurrentQueue<double>();      // From UI thread to BG thread.
        readonly ConcurrentQueue<TVMessage>   _rgUItoBGQueue = new ConcurrentQueue<TVMessage>();

        Thread               _oThread  = null;
        WaveIn               _oWaveIn  = null;
        BlockCopies          _oWaveReader  = null;

        public bool        StateTx { get; protected set; }
        public DocSSTVMode StateRx { get; protected set; }

        public event Action<SKPointI> Send_TxImageAspect;
        // Cheat for special layouts. Still working on this.
        protected SKSizeI Destination { get; set; } = new SKSizeI();
        // Shared selection. Last view to change it wins but I
        // expect only the resize view to attempt to change this.
        // These are in the bitmap's world coordinates.
        public SmartSelect Selection { get; } = new SmartSelect() { Mode = DragMode.FixedRatio };

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
        protected readonly IPgStandardUI2    _oStdUI;
        protected          DateTime          _dtLastTime;



        public IPgParent Parentage => _oSiteBase.Host;
        public IPgParent Services  => Parentage;
        public bool      IsDirty   => false; // BUG: Time to implement this.

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
        public SSTVProperties      Properties    { get; }
        public SKBitmap            TxBitmap      => TxImageList.Bitmap;
        internal DocImageEdit      TxBitmapComp  { get; }

        /// <summary>
        /// Used by the Resize dialog to make sure the selection matches the latest layout.
        /// </summary>
        public SKPointI TxImgLayoutAspect { get; protected set; } = new ( 1, 1 );

        protected Mpg123FFTSupport FileDecoder   { get; set; }

        // This is where our image and diagnostic image live.
		public ImageSoloDoc DisplayImage { get; protected set; }
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
        /// <exception cref="InvalidCastException" />
        public DocSSTV( IPgBaseSite oSite ) {
            _oSiteBase  = oSite ?? throw new ArgumentNullException( "Site must not be null" );
            _oWorkPlace = ((IPgScheduler)Services).CreateWorkPlace() ?? throw new ApplicationException( "Couldn't create a worksite from scheduler.");
            _oStdUI     = (IPgStandardUI2)Services;

            TemplateList  = new Editor        ( new DocSlot( this ) );
            RxModeList    = new ModeEditor    ( new DocSlot( this, "SSTV Rx Modes" ) );
            TxModeList    = new ModeEditor    ( new DocSlot( this, "SSTV Tx Modes" ) );
            TxImageList   = new ImageWalkerDir( new DocSlot( this ) );
            RxHistoryList = new ImageWalkerDir( new DocSlot( this ) );
            TxBitmapComp  = new DocImageEdit  ( new DocSlot( this ) );
                          
            PortTxList    = new Editor        ( new DocSlot( this ) );
            PortRxList    = new Editor        ( new DocSlot( this ) );
            MonitorList   = new Editor        ( new DocSlot( this ) );
                          
			DisplayImage  = new ImageSoloDoc( new DocSlot( this ) );
			SyncImage     = new ImageSoloDoc( new DocSlot( this ) );
                          
            Properties = new ( new DocSlot( this ) );

            StateRx = DocSSTVMode.Ready;
            StateTx = false;

            _dtLastTime = DateTime.UtcNow.AddMinutes( -1.0 );
        }

        #region Dispose
        protected virtual void Dispose( bool disposing ) {
            if( !disposedValue ) {
                if( disposing ) {
                    // If init new fails then this won't get created.
                    if( FileDecoder != null )
                        FileDecoder.Dispose();

                    ReceiveLiveStop();

                    RxHistoryList.ImageUpdated -= OnImageUpdated_RxHistoryList;
                    TxImageList  .ImageUpdated -= OnImageUpdated_TxImageList;
                    RxModeList   .CheckedEvent -= OnCheckedEvent_RxModeList;
                    TxModeList   .CheckedEvent -= OnCheckedEvent_TxModeList;
                    TemplateList .CheckedEvent -= OnCheckedEvent_TemplateList;
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

        public string MyCall    => Properties[(int)SSTVProperties.Names.Tx_MyCall].ToString().ToUpper();
        public string Message   => Properties[(int)SSTVProperties.Names.Tx_Message].ToString();
        public string TheirCall => Properties[(int)SSTVProperties.Names.Tx_TheirCall].ToString().ToUpper();
        public string RST       => Properties[(int)SSTVProperties.Names.Tx_RST].ToString();

        public SKColor ForeColor { get; set; } = SKColors.Red;

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
                sbValue.Append( oMode.FamilyName );
                sbValue.Append( ' ' );
                sbValue.Append( oMode.Version );
                if( fAddResolution ) {
                    sbValue.Append( " : " );
                    sbValue.Append( oMode.Resolution.Width.ToString() );
                    sbValue.Append( 'x' );
                    sbValue.Append( oMode.Resolution.Height.ToString() );
                    sbValue.Append( " @ " );
                    sbValue.Append( ( oMode.ScanWidthInMS * oMode.Resolution.Height / oMode.ScanMultiplier / 1000 ).ToString( "0." ) );
                    sbValue.Append( 's' );
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
					throw;
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

        /// <summary>
        /// Not a true load since we already initialized the Template List.
        /// But I wanted it separated out so it's easier to find.
        /// </summary>
        /// <returns></returns>
        public bool TemplateLoad() {
            TemplateList.LineAppend( "Reply PnP" );
            TemplateList.LineAppend( "General Msg" );
            TemplateList.LineAppend( "General Msg Pnp" );
            TemplateList.LineAppend( "CQ Color Gradient" );
            TemplateList.LineAppend( "High Def Message" );
            TemplateList.LineAppend( "High Def CQ" );
            TemplateList.LineAppend( "High Def From Me" );
            TemplateList.LineAppend( "High Def Reply" );
            TemplateList.LineAppend( "High Def Reply Pnp" );
            
            return true;
        }

        public bool InitNew() {
            if( !TemplateList.InitNew() ) // Might need to init differently b/c of load.
                return false;
            if( !RxModeList .InitNew() ) 
                return false;
            if( !TxModeList .InitNew() ) 
                return false;
            if( !TxBitmapComp.InitNew() )
                return false;

			if( !DisplayImage.InitNew() )
				return false;
			if( !SyncImage   .InitNew() )
				return false;

            if( !MonitorList.InitNew() )
                return false;
            if( !PortTxList .InitNew() )
                return false;
            if( !PortRxList .InitNew() ) 
                return false;

            if( !Properties.InitNew() )
                return false;

            TemplateLoad();

            // Largest bitmap needed by any of the types I can decode.
		    SyncImage   .Bitmap = new SKBitmap( 800, 616, SKColorType.Rgb888x, SKAlphaType.Unknown );
		    DisplayImage.Bitmap = new SKBitmap( 800, 616, SKColorType.Rgb888x, SKAlphaType.Opaque  );

            SettingsInit();

            string strMyDocs = Environment.GetFolderPath( Environment.SpecialFolder.MyDocuments );
            if( !TxImageList.LoadURL( strMyDocs ) ) {
				LogError( "Couldn't find pictures tx directory for SSTV" );
                return false;
			}
            string strMyPics = Properties[SSTVProperties.Names.Rx_SaveDir].ToString();
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

            // Get these set up so our stdproperties get the updates.
            TxImageList  .ImageUpdated += OnImageUpdated_TxImageList;
            RxHistoryList.ImageUpdated += OnImageUpdated_RxHistoryList;
            RxModeList   .CheckedEvent += OnCheckedEvent_RxModeList;
            TxModeList   .CheckedEvent += OnCheckedEvent_TxModeList;
            TemplateList .CheckedEvent += OnCheckedEvent_TemplateList;

            RenderComposite();

            _oWorkPlace.Queue( CreateTaskReceiver(), Timeout.Infinite );

            return true;
        }

        public void PostBGMessage( TVMessage.Message msg ) {
            _rgUItoBGQueue.Enqueue( new TVMessage( msg, null ) );
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
            Properties.ValueUpdate( SSTVProperties.Names.Rx_SaveDir, strMyPics );

            //RecChooser.LoadURL( strMyDocs );
        }

        protected void PropertiesTxReLoad() {
			string strFileName = TxImageList.CurrentFileName;
            string strFilePath = TxImageList.CurrentDirectory;

            //TxProperties.ValueUpdate( TxProperties.Names.Mode,     TxMode ); 
            Properties.ValueUpdate( SSTVProperties.Names.Tx_Progress, "0%" );
            //TxProperties.ValueUpdate( TxProperties.Names.FileName, strFileName, Broadcast:true );
		}

        protected void PropertyLoadFromXml( Editor rgList, XmlNode oElem, bool fLoadMissing = false ) {
            if( oElem == null )
                throw new ArgumentNullException( "oElem" );

            if( oElem.InnerText != null ) {
                bool fFound = !fLoadMissing;
                foreach( Line oLine in rgList ) {
                    if( oLine.Compare( oElem.InnerText ) == 0 ) {
                        rgList.CheckedLine = oLine;
                        fFound = true;
                        break;
                    }
                }
                if( !fFound ) {
                    // this will be an invalid device, but in the list.
                    // be nice if I could make it not selectable. Or an X
                    // in the check box area?
                    Line oNew = rgList.LineAppend( oElem.InnerText, false );
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
                            PropertyLoadFromXml( PortRxList, oNode, fLoadMissing:true );
                            break;
                        case "TxDevice":
                            PropertyLoadFromXml( PortTxList, oNode, fLoadMissing:true );
                            break;
                        case "MonitorDevice":
                            PropertyLoadFromXml( MonitorList, oNode, fLoadMissing:true );
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
                            Properties.ValueUpdate( SSTVProperties.Names.Std_ImgQuality, oNode.InnerText );
                            break;
                        case "DigiOutputGain":
                            Properties.ValueUpdate( SSTVProperties.Names.Std_MicGain, oNode.InnerText );
                            break;
                        case "MyCall":
                            Properties.ValueUpdate( SSTVProperties.Names.Tx_MyCall, oNode.InnerText );
                            break;
                        case "Message":
                            Properties.ValueUpdate( SSTVProperties.Names.Tx_Message, oNode.InnerText );
                            break;
                        case "TxSrcDir":
                            TxImageList.LoadAgain( oNode.InnerText );
                            break;
                        case "Clock":
                            Properties.ValueUpdate( SSTVProperties.Names.Std_Frequency, oNode.InnerText );
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
                    oElem.InnerText = Properties[(int)eProperty].ToString();
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
                StringProperty( "Clock",          SSTVProperties.Names.Std_Frequency );

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
        /// SmartRect code. It should be a SKSizeI too. I'll fix that later.
        /// </summary>
        /// <remarks>Aspect for the main image is related to layout. So this isn't
        /// really right when a layout carves up the image space. Need to work on that.
        /// I'm having a chicken and egg problem with the layout needing the bitmap
        /// but I can't create the bitmap until I have the layout! -_-;; </remarks>
        public SKPointI TxResolution {
            get {
                try {
                    if( TxModeList.CheckedLine != null && TxModeList.CheckedLine.Extra is SSTVMode oMode )
                        return new SKPointI( oMode.Resolution.Width, oMode.Resolution.Height );
                } catch( NullReferenceException ) {
                    LogError( "Problem finding SSTVMode. Using default." );
                }
                return new SKPointI( 320, 256 );
            }
        }

        private void OnImageUpdated_TxImageList() {
            if( TxImageList.Bitmap != null ) {
			    Selection.SetRect( 0, 0, TxImageList.Bitmap.Width, TxImageList.Bitmap.Height );
            } else {
                Selection.SetRect( 0, 0, 0, 0 );
            }
            TxBitmapComp.Clear(); // We have references to TxImageList.Bitmap we must clear;
            RenderComposite();
            Properties.RaiseBufferEvent();
        }

        private void OnCheckedEvent_TxModeList(Line oLineChecked) {
            RenderComposite();
        }

        private void OnCheckedEvent_TemplateList(Line oLineChecked) {
            RenderComposite();
        }

        /// <summary>
        /// Strictly speaking we're not getting the event we really need here. 
        /// Look at this again later.
        /// </summary>
        private void OnImageUpdated_RxHistoryList() {
            // BUG: Need to make the RxProp the one that gets changed and we catch an event to LoadAgain();
			Properties.RaiseBufferEvent();
            if( StateRx == DocSSTVMode.DeviceRead ) {
                _rgUItoBGQueue.Enqueue( new TVMessage( TVMessage.Message.ChangeDirectory, RxHistoryList.CurrentDirectory ) );
            }
            TxBitmapComp.Clear(); // We have references to RxHistoryList.Bitmap we must clear;
            RenderComposite();
        }

        private void OnCheckedEvent_RxModeList(Line oLineChecked) {
            _rgUItoBGQueue.Enqueue( new TVMessage( TVMessage.Message.TryNewMode, oLineChecked.Extra ) );
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

        protected string TemplateReplyFromProps() {
            StringBuilder sbText       = new StringBuilder();
            string        strTheirCall = TheirCall;
            string        strRST       = RST;
            string        strMessage   = Message;

            if( !string.IsNullOrEmpty( strTheirCall ) ) {
                sbText.Append( strTheirCall );
                sbText.Append( " de " );
            }
            sbText.Append( MyCall );
            if( !string.IsNullOrEmpty( strRST ) ) {
                sbText.Append( ' ' );
                sbText.Append( strRST );
            }
            if( !string.IsNullOrEmpty( strMessage ) ) {
                if( !string.IsNullOrEmpty( strRST ) )
                    sbText.Append( " : " );
                else
                    sbText.Append( ' ' );
                sbText.Append( strMessage );
            }

            return sbText.ToString();
        }

        /// <summary>
        /// The world selection is always the entire image so it stretches to
        /// the given layout. Then when you resize, the aspect of the layout
        /// for the main image is applied to the world selection (on the main image)
        /// so that the aspects are the same.
        /// </summary>
        /// <param name="iIndex"></param>
		public void TemplateSet( SSTVMode oMode, int iIndex ) {
			if( oMode == null ) {
                LogError( "Set a transmit mode first." );
                return;
            } 
			if( TxImageList.Bitmap == null ) {
                LogError( "No Tx Images Here." );
                return;
            } 
			if( RxHistoryList.Bitmap == null ) {
                LogError( "No Rx Images Here." );
                return;
            } 

			try {
				TxBitmapComp.Clear();

				switch( iIndex ) {
					case 0: // PnP reply.
						TxBitmapComp.AddImage( LOCUS.CENTER,      0,  0, 100.0, TxBitmap, Selection );
						TxBitmapComp.AddText ( LOCUS.UPPERLEFT,   5,  5,  17.0, TxBitmapComp.StdFace, ForeColor, TemplateReplyFromProps() );
						TxBitmapComp.AddImage( LOCUS.LOWERRIGHT, 10, 10,  40.0, RxHistoryList.Bitmap, null );

                        Send_TxImageAspect?.Invoke( new SKPointI( oMode.Resolution.Width, oMode.Resolution.Height ) );
                        TxImgLayoutAspect = new ( oMode.Resolution.Width, oMode.Resolution.Height );
						break;
					case 1: // General Message
						TxBitmapComp.AddImage( LOCUS.CENTER,      0,  0, 100.0, TxBitmap, Selection );
						TxBitmapComp.AddText ( LOCUS.UPPERLEFT,   5,  5,  20.0, TxBitmapComp.StdFace, ForeColor, Message );

                        Send_TxImageAspect?.Invoke( new SKPointI( oMode.Resolution.Width, oMode.Resolution.Height ) );
                        TxImgLayoutAspect = new ( oMode.Resolution.Width, oMode.Resolution.Height );
						break;
                    case 2: // General Message PnP
						TxBitmapComp.AddImage( LOCUS.CENTER,      0,  0, 100.0, TxBitmap, Selection );
						TxBitmapComp.AddText ( LOCUS.UPPERLEFT,   5,  5,  15.0, TxBitmapComp.StdFace, ForeColor, Message );
						TxBitmapComp.AddImage( LOCUS.LOWERRIGHT, 10, 10,  40.0, RxHistoryList.Bitmap, null );

                        Send_TxImageAspect?.Invoke( new SKPointI( oMode.Resolution.Width, oMode.Resolution.Height ) );
                        TxImgLayoutAspect = new ( oMode.Resolution.Width, oMode.Resolution.Height );
                        break;
                    case 3:
                        TemplateSetCQLayout( oMode, false );
                        break;
                    case 4:
                        TemplateSetHiDefMessage( oMode, Message );
                        break;
                    case 5:
                        TemplateSetCQLayout( oMode, true );
                        break;
                    case 6:
                        TemplateSetHiDefMessage( oMode, "from " + MyCall );
                        break;
                    case 7:
                        TemplateSetHiDefMessage( oMode, TemplateReplyFromProps() );
                        break;
                    case 8:
                        TemplateSetHiDefReplyPnP( oMode );
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

				LogError( "Could not apply image template." );
			}
		}

        /// <summary>
        /// Adjust the selection to match the given Layout Aspect (if different)
        /// </summary>
        /// <param name="lyImage">The layout after LayoutChild() has been called.</param>
        protected void SelectionAdjust( LayoutImageReference lyImage ) {
			float flOldSlope = Selection.Width / (float)Selection.Height;
			float flNewSlope = lyImage  .Width / (float)lyImage  .Height;

			if( flOldSlope != flNewSlope  ) {
			    SKPointI      pntCorner  = Selection.GetPoint( LOCUS.LOWERRIGHT );
			    SmartGrabDrag oSmartDrag = Selection.BeginAspectDrag( null, SET.STRETCH, SmartGrab.HIT.CORNER, 
																	  LOCUS.LOWERRIGHT, pntCorner.X, pntCorner.Y, 
                                                                      new SKPointI( lyImage.Width, lyImage.Height ) );
			    oSmartDrag.Move( pntCorner.X, pntCorner.Y );

                lyImage.World.Copy = Selection;
            }
        }

        /// <summary>
        /// Sort of an experimental layout system. Normal layout assumes everything is relative to
        /// the entire image size. But this allows us to break that up and layout relative to the
        /// children. It's cool but more difficult to represent with screen userinterface, which
        /// hasn't been written yet.
        /// </summary>
        /// <param name="oMode">SSTV mode we are sending in.</param>
        /// <param name="fHighContrast">if false, use color. if true using BW.</param>
        protected void TemplateSetCQLayout( SSTVMode oMode, bool fHighContrast ) {
            LayoutStackVertical oStack = new();
            LayoutStack         oHoriz;
            const double        dblFractionalHeight = 20 / 100.0;

            SKPoint        skEMsPerInch = new( 96, 96 ); 
            const int iScreenPixPerInch = 72;
            uint            uiPixHeight = (uint)((double)oMode.Resolution.Height * dblFractionalHeight );
            
            if( fHighContrast ) { 
                Func< object, SKColor > oFunc = delegate( object x )  { return SKColors.Black; };

                oHoriz = new LayoutStackHorizontal() { Layout = LayoutRect.CSS.Pixels, Track = uiPixHeight, BackgroundColor = oFunc };
            } else {
                oHoriz = new LayoutStackBgGradient( TRACK.HORIZ ) { 
                        Layout = LayoutRect.CSS.Pixels, 
                        Track  = uiPixHeight,
                        Colors = { SKColors.Green, SKColors.Yellow, SKColors.Blue } 
                };
            }

            Line               oLine = TxBitmapComp.Text.LineAppend( "CQ de " + MyCall, fUndoable:false );
            LayoutSingleLine oSingle = new( new FTCacheWrap( oLine ) { Justify = FTCacheLine.Align.Center }, LayoutRect.CSS.None ) 
                                         { BgColor = SKColors.Transparent, 
                                           FgColor = fHighContrast ? SKColors.White : ForeColor };

            // Since we flex, do all this before layout children.
            uint      uiPoints = (uint)( uiPixHeight * iScreenPixPerInch / skEMsPerInch.Y );
            uint      uiFontID = _oStdUI.FontCache( TxBitmapComp.StdFace, uiPoints, skEMsPerInch );
            oSingle.Cache.Update( _oStdUI.FontRendererAt( uiFontID ) );

            oHoriz.Add( oSingle );

            oStack.Add( oHoriz );

            LayoutImageReference oImage = new LayoutIcon( TxImageList.Bitmap, LayoutRect.CSS.None ) { Stretch = true };
            oImage.World.Copy = Selection;
            oStack.Add( oImage );

            // Need this to calc image aspect to bubble up.
            oStack.SetRect( 0, 0, oMode.Resolution.Width, oMode.Resolution.Height );
            oStack.LayoutChildren();

            // After the layout send the aspect out to the listeners. In case we want to re-select.
            Send_TxImageAspect?.Invoke( new SKPointI( oImage.Width, oImage.Height ) );

            //SelectionAdjust( lyImage );
            TxImgLayoutAspect = new ( oImage.Width, oImage.Height );

            TxBitmapComp.AddLayout( oStack );
        }

        protected void TemplateSetHiDefMessage( SSTVMode oMode, string strMessage ) {
            Func< object, SKColor > oFunc = delegate( object x )  { return SKColors.Black; };

            LayoutStackVertical oStack = new();
            Editor               oEdit = TxBitmapComp.Text;
            Line                 oLine = oEdit.LineAppend( strMessage, fUndoable:false );
            FTCacheWrap          oElem = new( oLine ) { Justify = FTCacheLine.Align.Center };
            LayoutSingleLine     oText = new( oElem, LayoutRect.CSS.Flex ) 
                                         { BgColor = SKColors.Black, FgColor = SKColors.White };

            // Since we flex, do all this before layout children.
            const double dblFractionalHeight = 18 / 100.0;
            SKPoint          skEMsPerInch    = new SKPoint(96, 96);
            const int      iScreenPixPerInch = 72;
            uint                 uiMsgHeight = (uint)((double)oMode.Resolution.Height * dblFractionalHeight );

            uint      uiPoints = (uint)( uiMsgHeight * iScreenPixPerInch / skEMsPerInch.Y );
            uint      uiFontID = _oStdUI.FontCache( TxBitmapComp.StdFace, uiPoints, skEMsPerInch );
            oText.Cache.Update( _oStdUI.FontRendererAt( uiFontID ) );

            LayoutImage oImage = new LayoutImage( TxBitmap, LayoutRect.CSS.None ) { Stretch = true };
            oImage.World.Copy = Selection;

            oStack.Add( oImage );
            oStack.Add( oText );

            // Need this to calc image aspect to bubbleup.
            oStack.SetRect( 0, 0, oMode.Resolution.Width, oMode.Resolution.Height );
            oStack.LayoutChildren();
                            
            // After the layout send the aspect out to the listeners. In case we want to re-select.
            Send_TxImageAspect?.Invoke( new SKPointI( oImage.Width, oImage.Height ) );

            // After the layout now we can word wrap the text.
            //oEdit.WordBreak( oElem.Line, oElem.Words ); 
            oElem.Update( _oStdUI.FontRendererAt( uiFontID ) );
            oElem.WrapSegments( oImage.Width );

            //SelectionAdjust( oImage );
            TxImgLayoutAspect = new ( oImage.Width, oImage.Height );

            TxBitmapComp.AddLayout( oStack );

        }

        protected void TemplateSetHiDefReplyPnP( SSTVMode oMode ) {
            Func< object, SKColor > oFunc = delegate( object x )  { return SKColors.Black; };

            LayoutStackVertical oVertiMain;
            LayoutStack         oHorizImgs;
            const double        dblFractionalHeight = 18 / 100.0;

            SKPoint        skEMsPerInch = new( 96, 96 ); 
            const int iScreenPixPerInch = 72;
            uint            uiPixHeight = (uint)((double)oMode.Resolution.Height * dblFractionalHeight );
            
            oHorizImgs = new LayoutStackHorizontal() { Layout = LayoutRect.CSS.None, BackgroundColor = oFunc, Spacing = 5 };
            oVertiMain = new LayoutStackVertical  () { Layout = LayoutRect.CSS.None, BackgroundColor = oFunc };

            Editor             oEdit = TxBitmapComp.Text;
            Line               oLine = oEdit.LineAppend( TemplateReplyFromProps(), fUndoable:false );
            FTCacheWrap        oWrap = new FTCacheWrap( oLine ) { Justify = FTCacheLine.Align.Center };
            LayoutSingleLine oSingle = new( oWrap, LayoutRect.CSS.Flex ) 
                                         { Track = 60, BgColor = SKColors.Transparent, 
                                           FgColor = SKColors.White };

            // Since we flex, do all this before layout children.
            uint      uiPoints = (uint)( uiPixHeight * iScreenPixPerInch / skEMsPerInch.Y );
            uint      uiFontID = _oStdUI.FontCache( TxBitmapComp.StdFace, uiPoints, skEMsPerInch );
            oSingle.Cache.Update( _oStdUI.FontRendererAt( uiFontID ) );
            oSingle.Padding.SetRect( 5, 0, 5, 0 ); // BUG: Doesn't seem to work for text.

            oVertiMain.Add( oSingle );

            LayoutImageReference oImage1 = new LayoutIcon( TxImageList  .Bitmap, LayoutRect.CSS.None ) { Stretch = true };
            oImage1.World.Copy = Selection;

            LayoutImageReference oImage2 = new LayoutIcon( RxHistoryList.Bitmap, LayoutRect.CSS.None ) { Stretch = true };
            oHorizImgs.Add( oImage1 );
            oHorizImgs.Add( oImage2 );

            oVertiMain.Add( oHorizImgs );
            oVertiMain.Padding.SetRect( 5, 5, 5, 5 );

            // Need this to calc image aspect to bubble up.
            oVertiMain.SetRect( 0, 0, oMode.Resolution.Width, oMode.Resolution.Height );
            oVertiMain.LayoutChildren();

            // After the layout send the aspect out to the listeners. In case we want to re-select.
            Send_TxImageAspect?.Invoke( new SKPointI( oImage1.Width, oImage1.Height ) );

            // After the layout now we can word wrap the text.
            //oEdit.WordBreak( oWrap.Line, oWrap.Words ); 
            oWrap.Update( _oStdUI.FontRendererAt( uiFontID ) );
            oWrap.WrapSegments( oSingle.Width );

            //SelectionAdjust( oImage1 );
            TxImgLayoutAspect = new ( oImage1.Width, oImage1.Height );

            TxBitmapComp.AddLayout( oVertiMain );
        }

        protected class TxState : IEnumerable<int> {
            protected BufferSSTV    _oSSTVBuffer;      
            protected SSTVMOD       _oSSTVModulator;
            protected SSTVGenerator _oSSTVGenerator;
		    protected IPgPlayer     _oPlayer;

            protected ConcurrentQueue<SSTVMessage> _oBGtoFGQueue;

            public TxState( SSTVMode oMode, double dblSampFreq, int iGain, int iDevice, SKBitmap skBitmap, ConcurrentQueue<SSTVMessage> oBGtoFGQueue ) {
                if( oMode == null )
                    throw new ArgumentNullException( nameof( oMode ) );
                if( skBitmap == null )
                    throw new ArgumentNullException( nameof( skBitmap ) );

                _oBGtoFGQueue = oBGtoFGQueue ?? throw new ArgumentNullException( nameof( oBGtoFGQueue ) );

                // TODO: change sample frequency in spec object to be a double precision?
                Specification oTxSpec = new( (long)dblSampFreq, 1, 0, 16 );

                _oSSTVBuffer    = new BufferSSTV( oTxSpec );
				_oSSTVModulator = new SSTVMOD( 0, dblSampFreq, _oSSTVBuffer, iGain );

				switch( oMode.Family ) {
					case TVFamily.PD:
						_oSSTVGenerator = new GeneratePD      ( skBitmap, _oSSTVModulator, oMode ); break;
					case TVFamily.Martin:
						_oSSTVGenerator = new GenerateMartin  ( skBitmap, _oSSTVModulator, oMode ); break;
					case TVFamily.Scottie:
						_oSSTVGenerator = new GenerateScottie ( skBitmap, _oSSTVModulator, oMode ); break;
					case TVFamily.BW:
						_oSSTVGenerator = new GenerateBW      ( skBitmap, _oSSTVModulator, oMode ); break;
                    case TVFamily.Pasokon:
                        _oSSTVGenerator = new GeneratePasokon ( skBitmap, _oSSTVModulator, oMode ); break;
                    case TVFamily.Robot:
                        _oSSTVGenerator = new GenerateRobot422( skBitmap, _oSSTVModulator, oMode ); break;
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

        protected int MicrophoneGain => Properties.GetValueAsInt( SSTVProperties.Names.Std_MicGain );

        /// <summary>
        /// Get the currently checked line in the TxModeList or set it to the first item.
        /// </summary>
        /// <seealso cref="ViewTransmitDeluxe.SSTVModeSelection"/>
		public SSTVMode TransmitModeSelection { 
			get {
                if( TxModeList.CheckedLine == null ) {
                    if( RxModeList.CheckedLine == null )
                        TxModeList.CheckedLine = TxModeList[0];
                    else
                        TxModeList.CheckedLine = TxModeList[RxModeList.CheckedLine.At];
                }

                if( TxModeList.CheckedLine.Extra is SSTVMode oMode )
					return oMode;

				return null;
			}
		}

		public bool RenderComposite() {
            SSTVMode oMode = TransmitModeSelection;
			// sometimes we get events while we're sending. Let's block render for now.
			if( StateTx ) {
				LogError( "Already Playing" );
				return false;
			}

			if( oMode != null ) {
			    if( Selection.IsEmpty() && TxImageList.Bitmap != null ) {
				    Selection.SetRect ( 0, 0,
									    TxImageList.Bitmap.Width,
									    TxImageList.Bitmap.Height );
			    }

				TxBitmapComp.Load( oMode.Resolution ); 

				int iTemplate = TemplateList.CheckedLine is Line oChecked ? oChecked.At : 0;

				TemplateSet( oMode, iTemplate );
				TxBitmapComp.RenderImage();

				return true;
			} else {
				LogError( "Problem prepping template for transmit." );
			}

			return false;
		}

        public void TransmitStop() {
            StateTx              = false;
            TxModeList.HighLight = null;
        }

        /// <summary>
        /// Begin transmitting the image. We can stop but only after the 
        /// buffered transmission bleeds out.
        /// </summary>
        public async void TransmitBegin() {
			SSTVMode oMode = TransmitModeSelection;

            if( oMode == null || TxBitmapComp.Bitmap == null ) {
                LogError( "Transmit mode or image is not set." ); return;
            }
            if( PortTxList.CheckedLine == null ) {
                LogError( "No sound device to send to" ); return;
            }
            if( StateTx ) {
                LogError( "Already Transmitting" ); return;
            }
            if( StateRx != DocSSTVMode.DeviceRead ) {
                // we need the receive listener polling to get updates for
                // or tx progress. Starting the polling here mighte
                // get us in a weird mode. This is easier for now.
                LogError( "Start device receive first." );
            }
                
            StateTx = true;

            Action oTransmitAction = delegate () {
                // Use WWV to find the precise sample frequency of sound card. 
                // TODO: Port the tuner from MMSSTV and make it a property.
                SKBitmap bmpCopy = TxBitmapComp.Bitmap.Copy();
                double   dblFreq = Properties.GetValueAsDbl( SSTVProperties.Names.Std_Frequency );
                TxState   oState = new TxState( oMode, dblFreq, MicrophoneGain, 
                                                PortTxList.CheckedLine.At, 
                                                bmpCopy, _rgBGtoUIQueue );
                foreach( uint uiWait in oState ) {
                    if( StateTx == false )
                        break;
                    Thread.Sleep( (int)uiWait );
                }
                if( StateTx == true )
                    Thread.Sleep( 2000 ); // Let the buffer bleed out.

                bmpCopy.Dispose();
            };

            Task oTask = new Task( oTransmitAction );

            oTask.Start();
            _oSiteBase.Notify( ShellNotify.MediaStatusChanged );

            await oTask;

            oTask.Dispose(); // this is a little lengthy.
            StateTx = false;
            _oSiteBase.Notify( ShellNotify.MediaStatusChanged );
        }

        /// <summary>
        /// I should probably save the image in the background thread just so that
        /// we don't have any race to see if the bg starts drawing on the image.
        /// </summary>
        /// <param name="strFileName"></param>
        protected void DownloadFinished() {
        }

        protected readonly string[] _rgThreadExStrings = { "Drawing Exception", "WorkerException", "ReadException", "DiagnosticsException" };

        /// <summary>
        /// This is our task to poll the Background to UI Queue. It services both
        /// the receive thread and the transmit thread. Technically TX and RX can run
        /// concurrently with no problems.
        /// </summary>
        public IEnumerator<int> CreateTaskReceiver() {
            while( true ) {
                while( _rgBGtoUIQueue.TryDequeue( out SSTVMessage sResult ) ) {
                    switch( sResult.Event ) {
                        case SSTVEvents.ModeChanged: {
                            SSTVMode oMode = null;

                            foreach( Line oLine in RxModeList ) {
                                if( oLine.Extra is SSTVMode oTryMode ) {
                                    if( oTryMode.LegacyMode == (AllModes)sResult.Param ) {
                                        RxModeList.HighLight = oLine;
                                        oMode = oTryMode;
                                    }
                                }
                            }
                            if( oMode == null ) {
                                // We catch a null we're going back to listen mode. This is an error mode
                                // in the file read case.
                                RxModeList.HighLight    = null;
                                RxModeList.CheckedReset = RxModeList[0]; 
                                // Let's not clear the Mode, Width, Height, SaveName. Nice to have them around.
                            } else {
			                    DisplayImage.WorldDisplay = new SKRectI( 0, 0, oMode.Resolution.Width, oMode.Resolution.Height );

                                Properties.ValueUpdate( SSTVProperties.Names.Rx_Mode,   oMode.FamilyName + ' ' + oMode.Version );
                                Properties.ValueUpdate( SSTVProperties.Names.Rx_Width,  oMode.Resolution.Width .ToString() );
                                Properties.ValueUpdate( SSTVProperties.Names.Rx_Height, oMode.Resolution.Height.ToString() );
                            }
                            PropertyChange?.Invoke( SSTVEvents.ModeChanged );
                        } break;
                        case SSTVEvents.UploadTime:
                            Properties.ValueUpdate( SSTVProperties.Names.Tx_Progress, sResult.Param.ToString( "D2" ) + "%", Broadcast:true );
                            break;
                        case SSTVEvents.DownLoadTime: 
                            Properties.ValueUpdate( SSTVProperties.Names.Rx_Progress, sResult.Param.ToString( "D2" ) + "%", Broadcast:true );
                            PropertyChange?.Invoke( SSTVEvents.DownLoadTime );
                            break;
                        case SSTVEvents.DownLoadFinished: // NOTE: This comes along unreliably in the device streaming case.
                            Properties.ValueUpdate( SSTVProperties.Names.Rx_Progress, sResult.Param.ToString( "D2" ) + "% - Complete", Broadcast:true );
                            PropertyChange?.Invoke( SSTVEvents.DownLoadFinished );

                            RxModeList.HighLight   = null;
                            RxModeList.CheckedLine = RxModeList[0];

                            RxHistoryList.Refresh();
                            break;
                        case SSTVEvents.ImageSaved: 
                            PropertyChange?.Invoke( SSTVEvents.ImageSaved );
                            RxHistoryList.Refresh();
                            break;
                        case SSTVEvents.ThreadAbort:
                            if( _oThread == null ) {
                                LogError( "Unexpected Image Thread Abort." );
                            } else { 
                                LogError( "Image Thread Abort." );
                            }
                            RxModeList.HighLight   = null;
                            RxModeList.CheckedLine = RxModeList[0];
                            _oThread = null;
                            break; 
                        case SSTVEvents.ThreadException:
                            try {
                                LogError( _rgThreadExStrings[sResult.Param] );

                                if( sResult.Param == (int)TxThreadErrors.DataOverflow ) {
                                    LogError( "Data Overflow, halting device read" );
                                    ReceiveLiveStop(); 
                                }
                            } catch( IndexOutOfRangeException ) {
                                LogError( "General Thread Exception " + sResult.Param.ToString() );
                            }
                            break;
                    }
                }
                DateTime dtNow = DateTime.UtcNow;
                if( _dtLastTime.AddMinutes( 1.0 ) < dtNow ) {
                    Properties.ValueUpdate( SSTVProperties.Names.Std_Time, dtNow.ToString( "g" ), Broadcast:true );
                    // This gets it so we're closer to the actual H:M:0 second mark.
                    _dtLastTime = dtNow.AddSeconds( -dtNow.Second );
                }

                yield return 250; // wait 1/4 of a second.
            };
        }

        public void ReceiveSave() {
			_rgUItoBGQueue.Enqueue( new TVMessage( TVMessage.Message.SaveNow ) );
        }

        /// <summary>
        /// Read the file given. If the mode is not null use that as the starting mode for
        /// the demodulator, in case you missed the VIS. You can actually run this while
        /// transmitting. But that would be weird.
        /// </summary>
        /// <remarks>This is our new thread pool version. No way to cancel, might look
        /// into the thread cancellation token stuff.</remarks>
        public async void ReceiveFileRead( string strFileName, SSTVMode oMode ) {
            if( string.IsNullOrEmpty( strFileName ) ) {
                LogError( "Invalid filename for SSTV image read." );
                return;
            }
            if( StateRx != DocSSTVMode.Ready ) {
                LogError( "Busy right now." );
                return;
            }
            switch( _oWorkPlace.Status ) {
                case WorkerStatus.NOTIMPLEMENTED:
                    LogError( "File Read Listener error." );
                    return;
                case WorkerStatus.PAUSED:
                case WorkerStatus.FREE:
                    _oWorkPlace.Start( 1 );
                    break;
            }

            StateRx = DocSSTVMode.FileRead;

			RxHistoryList.LoadAgain( strFileName );
            Properties.ValueUpdate( SSTVProperties.Names.Std_Process, "File Read Started." );
            Properties.ValueUpdate( SSTVProperties.Names.Rx_Progress, "0" );

            // this causes problems when we switch between device receive and file decode. 
          //Properties.ValueUpdate( SSTVProperties.Names.Rx_SaveName, strFileName, true ); // BUG: Should be the image name not the wav file.

            Action oFileReadAction = delegate () {
                FileReadingState oWorker = new ( _rgBGtoUIQueue, strFileName, SyncImage.Bitmap, DisplayImage.Bitmap );
                
                oWorker.DoWork( oMode );
            };

            Task oTask = new( oFileReadAction );

            oTask.Start();

            await oTask;

            // So we might not have received the last message from our bg thread even
            // though it is done. But we will have received the image(s). Pumping the
            // queue here is possibly re-entrant. Maybe some sort of ACK NACK is in order.
            Properties.ValueUpdate( SSTVProperties.Names.Std_Process, "File Read Complete.", true );

            // Download finished is sort of changing it's meaning in the file read case.
            PropertyChange?.Invoke( SSTVEvents.DownLoadFinished );

            RxModeList.HighLight   = null;
            //RxModeList.CheckedLine = RxModeList[0]; see ReceiveLiveBegin. 

            RxHistoryList.LoadAgain( RxHistoryList.CurrentDirectory );

            _oWorkPlace.Pause();
            StateRx = DocSSTVMode.Ready;
            _rgBGtoUIQueue.Clear();
       }

        public void ReceiveLiveStop() {
            RxModeList.HighLight = null;

            Properties.ValueUpdate( SSTVProperties.Names.Std_Process, "Stopping...", true );

            _rgUItoBGQueue.Enqueue( new TVMessage( TVMessage.Message.ExitWorkThread ) );
            _oThread = null;

            if( _oWaveIn != null ) {
                _oWaveIn.StopRecording();   
                _oWaveIn = null;
            }

            Properties.ValueUpdate( SSTVProperties.Names.Std_Process, "Stopped: All.", true );

            _oWorkPlace.Pause(); // TODO: flush the message buffers? Probably should.
            StateRx = DocSSTVMode.Ready;
            _oSiteBase.Notify( ShellNotify.MediaStatusChanged );
        }

        /// <summary>
        /// 3'rd generation reader.
        /// </summary>
        /// <remarks>3'rd generation reader. I'v separated the File read from the Device read
        /// code. This lets me use the tread pool for the transient file read case and now
        /// we have only one dedicated thread for reading from the device... forever! (until
        /// you turn it off.)</remarks>
        public void ReceiveLiveBegin() {
            if( _oThread != null ) {
                if( _oThread.IsAlive ) {
                    LogError( "Already Listening." );
                } else {
                    LogError( "Unexpected Listening state." );
                }
                return;
            }
            if( StateRx != DocSSTVMode.Ready ) {
                LogError( "Busy right now." );
                return;
            }
            if( _oThread == null ) {
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

                        // System works best if frequency here is not the calibrated (clock) value.
                        // Makes sense since it's not calibrated! But need to coordinate with desired
                        // (clock) frequency some how. TODO... ^_^;;
                        _oWaveIn.BufferMilliseconds = 250;
                        _oWaveIn.DeviceNumber       = iMicrophone;
                        _oWaveIn.WaveFormat         = new WaveFormat( 11028, 16, 1 );
                        _oWaveIn.NumberOfBuffers    = 2;
                        _oWaveIn.DataAvailable += OnDataAvailable_WaveIn;
                    }
                    _oWaveReader = new BlockCopies( 1, 1, 0, _oWaveIn.WaveFormat.BitsPerSample );

                    _rgUItoBGQueue.Clear();
                    _rgBGtoUIQueue.Clear();
                    _rgDataQueue  .Clear();

                    // File reader used to clear this value on each run but I like keeping that
                    // value for similar reads. So Reset the mode on Device read begin. No events
                    // or our task will be getting an event we don't want.
                    RxModeList.CheckedReset = RxModeList[0];

                    // See CheckedReset above.
                    //if( RxModeList.CheckedLine.Extra is SSTVMode oMode ) {
                    //    _rgUItoBGQueue.Enqueue( new TVMessage( TVMessage.Message.TryNewMode, oMode ) );
                    //}

                    int    iQuality    = Properties.GetValueAsInt( SSTVProperties.Names.Std_ImgQuality, 80 );
                    double dblFreq     = Properties.GetValueAsDbl( SSTVProperties.Names.Std_Frequency,   11028 );
                    string strSaveDir  = Properties[ SSTVProperties.Names.Rx_SaveDir  ];

                    // Just note, if we do a file read, we might no longer be in the MyPictures path.
                    if( string.IsNullOrEmpty( strSaveDir ) ) {
			            strSaveDir = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
                        Properties.ValueUpdate( SSTVProperties.Names.Rx_SaveDir, strSaveDir, Broadcast:true );
                    }

                    DeviceListeningState oWorker = new DeviceListeningState( 
                        dblFreq, 
                        iQuality, strSaveDir, String.Empty,
                        _rgBGtoUIQueue, _rgDataQueue, 
                        _rgUItoBGQueue, SyncImage.Bitmap, DisplayImage.Bitmap );
                    ThreadStart   threadDelegate = new ThreadStart( oWorker.DoWork );

                    _oThread = new Thread( threadDelegate );
                    _oThread.Start(); // Can send out of memory exception!

                    if( _oWaveIn != null ) {
                        _oWaveIn.StopRecording();
                        _oWaveIn.StartRecording();
                    }

                    _oWorkPlace.Start( 1 );
                    Properties.ValueUpdate( SSTVProperties.Names.Std_Process, "Start: Live.", true );
                    StateRx = DocSSTVMode.DeviceRead;
                } catch( Exception oEx ) {
                    Type[] rgErrors = { typeof( NullReferenceException ),
                                        typeof( ArgumentNullException ),
                                        typeof( FormatException ),
                                        typeof( OverflowException ),
                                        typeof( ThreadStateException ),
                                        typeof( InsufficientMemoryException ) };
                    if( rgErrors.IsUnhandled( oEx ) )
                        throw;

                    Properties.ValueUpdate( SSTVProperties.Names.Std_Process, "Start: Error.", true );
                    LogError( "Couldn't launch device listening thread." );
                }

                _oSiteBase.Notify( ShellNotify.MediaStatusChanged );
            }
        }

        /// <summary>
        /// Be sure to implement dispose on this object and make sure it get's
        /// called b/c this method will get called if the still living bg thread
        /// doesn't get an abort message.
        /// </summary>
        /// <remarks>Looks like I can't call wavein.stop() from within the callback. AND
        /// it looks like NAudio is calling us from its non foreground thread!!!</remarks>
        private void OnDataAvailable_WaveIn( object sender, WaveInEventArgs e ) {
            try {
                if( _rgDataQueue.Count > 1e6 ) {
                    _rgBGtoUIQueue.Enqueue( new( SSTVEvents.ThreadException, (int)TxThreadErrors.DataOverflow ) );
                } else {
                    // No use stuffing the data queue if there's no thread to pick it up.
                    if( _oThread != null ) {
                        for( IEnumerator<double>oIter = _oWaveReader.EnumAsSigned16Bit( e.Buffer, e.BytesRecorded ); oIter.MoveNext(); ) {
                            _rgDataQueue.Enqueue( oIter.Current );
                        }
                    }
                }
            } catch( NullReferenceException ) {
                // Used to catch an InvalidOperationException from an NAudio buffer I was using here.
                // I'm not using hat any more, but I'll keep the try block for now.
                // Safest way to report this is post an event to ourselves.
                _rgBGtoUIQueue.Enqueue( new( SSTVEvents.ThreadException, (int)TxThreadErrors.WorkerException ) );
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
                        // borrow the Composite Bitmap for this test.
			            oDoc.TxBitmapComp.Load( oDoc.TxBitmap, skSelect, oMode.Resolution );

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
					    _oRxSSTV          = new SSTVDraw ( _oSSTVDeModulator, oDoc.SyncImage.Bitmap, oDoc.DisplayImage.Bitmap );

					    _oSSTVGenerator = oMode.Family switch {
						    TVFamily.PD      => new GeneratePD     ( oDoc.TxBitmapComp.Bitmap, oDemodTst, oMode ),
						    TVFamily.Martin  => new GenerateMartin ( oDoc.TxBitmapComp.Bitmap, oDemodTst, oMode ),
						    TVFamily.Scottie => new GenerateScottie( oDoc.TxBitmapComp.Bitmap, oDemodTst, oMode ),

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
            private void OnNextMode_SSTVDeMod( SSTVMode tvMode, SSTVMode tvPrev, int iPrevBase ) {
			    _oDoc.DisplayImage.Bitmap = null;
			    _oDoc.SyncImage   .Bitmap = null;

                _oRxSSTV.OnModeTransition_SSTVDeMo( tvMode, tvPrev, iPrevBase ); // bitmap allocated in here. (may throw exception...)

			    _oDoc.DisplayImage.WorldDisplay = new SKRectI( 0, 0, tvMode.Resolution.Width, tvMode.Resolution.Height );

                // the mode objects in the list might be same spec but copied or something
                // match them via their legacy modes. Set up equivanlance test later.
                Line oFoundLine = null;
                foreach( Line oLine in _oDoc.RxModeList ) {
                    if( oLine.Extra is SSTVMode oLineMode ) {
                        if( oLineMode.LegacyMode == tvMode.LegacyMode )
                            oFoundLine = oLine;
                    }
                }
                _oDoc.RxModeList.HighLight = oFoundLine;
            }
        }

        /// <summary>
        /// The workplace is used for the receive operations.
        /// </summary>
		public WorkerStatus PlayStatus {
            get {
                return _oWorkPlace.Status;
            }
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

        public SKBitmap CreateIconic( string strResource ) {
            return SKImageResourceHelper.GetImageResource( Assembly.GetExecutingAssembly(), strResource );
        }
    } // End class
}
