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
using System.IO.Ports;


using SkiaSharp;

using Play.Drawing;
using Play.Rectangles;
using Play.Interfaces.Embedding;
using Play.Sound;
using Play.Sound.FFT;
using Play.Edit;
using Play.ImageViewer;
using Play.Forms;

namespace Play.SSTV {
    /// <summary>
    /// This subclass of the DocProperties let's us have static index values. This is advantageous because it
    /// allows us to re-arrange property values without scrambling their meaning. But it also means you can't
    /// use some kind of runtime forms generator since the indicies must have corresponding pre compiled enum's.
    /// </summary>
    public class SSTVProperties : DocProperties, IDisposable {
        public enum Names : int {
			Rx_Mode,
            Rx_Width,
            Rx_Height,
            Rx_Progress,
            Rx_SaveDir,
            Rx_HistoryFile,
            Rx_Window,
            Rx_FamilySelect,
            Rx_ModeSelect,
            Rx_Diagnostic,
            Rx_HistoryIcons,
            Rx_SignalLevel,

            Tx_Progress,
            Tx_SrcDir,
            Tx_SrcFile,
            Tx_MyCall,
            Tx_TheirCall,
            Tx_RST,
            Tx_Message,
            Tx_ModeSent,
            Tx_FamilySelect,
            Tx_ModeSelect,
            Tx_LayoutSelect,

            Std_MnPort,
			Std_TxPort,
            Std_RxPort,
            Std_ImgQuality,
            Std_Process,
            Std_MicGain,
            Std_Frequency
        }

        readonly ParseFormText _oParser;
        readonly protected IPgRoundRobinWork _oWorkPlace; 

        public void CheckParse() {
            //_oParser.Parse();
        }

        public SSTVProperties( IPgRoundRobinWork oWorker, IPgBaseSite oSiteBase ) : base( oSiteBase ) {
            //_oParser = new ParseFormText( oWorker, PropertyDoc, "text" );
            IPgScheduler oSchedular = (IPgScheduler)Services;

            _oWorkPlace = oSchedular.CreateWorkPlace() ?? throw new InvalidOperationException( "Need the scheduler service in order to work. ^_^;" );
        }

        public override void Dispose() {
            //_oParser.Dispose(); // remove the parser sink on the form values.
            base.Dispose();
        }

        public void ParseAll() {
            //_oParser.ParseAll();
        }

        public override bool InitNew() {
            if( !base.InitNew() ) 
                return false;
            // Might want to look at disposing this. But not strictly nessesary.
            //if( !_oParser.InitNew() )
            //    return false;
            
            // Set up the parser so we get spiffy colorization on our text!! HOWEVER,
            // Some lines are not sending events to the Property_Values document and we
            // need to trigger the parse seperately.
            // new ParseHandlerText( Property_Values, "text" );

            if( _oSiteBase.Host is not DocSSTV oSSTVDoc )
                return false;

            // Set up our basic list of values.
            foreach( Names eName in Enum.GetValues(typeof(Names)) ) {
                CreatePropertyPair( eName.ToString() );
            }

            // TODO: Need a way to clear the monitor check if don't want to monitor.
            LabelUpdate( Names.Std_MnPort,     "Monitor with Device" );
            LabelUpdate( Names.Std_TxPort,     "Transmit  to Device" );
            LabelUpdate( Names.Std_RxPort,     "Receive from Device" );
            LabelUpdate( Names.Std_ImgQuality, "Image Save Quality" );
            LabelUpdate( Names.Std_Process,    "Rx Status" );
            LabelUpdate( Names.Std_MicGain,    "Output Gain < 30,000" );
            LabelUpdate( Names.Std_Frequency,  "Frequency" ); // TODO: Give it yellow if calibrated value different than base.

            LabelUpdate( Names.Tx_MyCall,       "My Call" );
            LabelUpdate( Names.Tx_TheirCall,    "Rx Call", SKColors.LightGreen );
            LabelUpdate( Names.Tx_RST,          "RSV" ); // Readibility, strength, video
            LabelUpdate( Names.Tx_Message,      "Message" );
            LabelUpdate( Names.Tx_Progress,     "Tx % Sent" );
            LabelUpdate( Names.Tx_SrcDir,       "Tx Source Dir" );
            LabelUpdate( Names.Tx_SrcFile,      "Tx Filename" );
            LabelUpdate( Names.Tx_FamilySelect, "Tx Family" );
            LabelUpdate( Names.Tx_ModeSelect,   "Tx Mode" );
            LabelUpdate( Names.Tx_LayoutSelect, "Layout" );

            LabelUpdate( Names.Rx_Mode,         "Rx Last", new SKColor( red:0xff, green:0xbf, blue:0 ) );
            LabelUpdate( Names.Rx_Width,        "Width" );
            LabelUpdate( Names.Rx_Height,       "Height" );
            LabelUpdate( Names.Rx_Progress,     "Received" );
            LabelUpdate( Names.Rx_SaveDir,      "Rx Save Dir" );
            LabelUpdate( Names.Rx_HistoryFile,  "Rx Filename" );
            LabelUpdate( Names.Rx_Window,       "Rx Window" );
            LabelUpdate( Names.Rx_FamilySelect, "Rx Family" );
            LabelUpdate( Names.Rx_ModeSelect,   "Rx Mode" );
            LabelUpdate( Names.Rx_Diagnostic,   "Diagnostics" );
            LabelUpdate( Names.Rx_HistoryIcons, "History" );
            LabelUpdate( Names.Rx_SignalLevel,  "Signal Lvl" );

            // I forget where the values get updated... :-/
            ValueUpdate( Names.Tx_SrcDir,      oSSTVDoc.TxImageList  .CurrentShowPath );
            ValueUpdate( Names.Tx_SrcFile,     oSSTVDoc.TxImageList  .CurrentShowFile );
            ValueUpdate( Names.Rx_SaveDir,     oSSTVDoc.RxHistoryList.CurrentShowPath );
            ValueUpdate( Names.Rx_HistoryFile, oSSTVDoc.RxHistoryList.CurrentShowFile );

            // Initialize these to reasonable values, the user can update and save.
            ValueUpdate( Names.Std_ImgQuality, "80" );
            ValueUpdate( Names.Std_MicGain,    "10000" ); // Out of 30,000
            ValueUpdate( Names.Std_Frequency,  "11025" ); 

            return true;
        }

        /// <summary>
        /// Let's not do any clearing for the moment.
        /// </summary>
        public override void ValuesEmpty() {
        }

        public void LabelUpdate( Names eName, string strLabel, SKColor? skBgColor = null ) {
            LabelUpdate( (int)eName, strLabel, skBgColor );
        }

        public void ValueUpdate( Names eName, string strValue, bool Broadcast = false ) {
            ValueUpdate( (int)eName, strValue, Broadcast );
        }

        public void ValueUpdate( Names eName, Line oValue, bool Broadcast = false ) {
            ValueUpdate( (int)eName, oValue.ToString(), Broadcast );
        }
        public string this[ Names eIndex ] {
            get {
                return ValueAsStr((int)eIndex);
            }
        }

        public bool ValueGetAsBool( Names eIndex ) {
            return string.Compare( this[eIndex], "true", ignoreCase:true ) == 0;
        }

        public int ValueGetAsInt( Names eIndex, int? iDefault = null ) {
            if( iDefault.HasValue ) {
                if( !int.TryParse( this[eIndex], out int iValue ) ) {
                    // There's a problem with this property. So let's hilight it.
                    iValue = iDefault.Value;
                    if( !ValueBgColor.ContainsKey( (int)eIndex ) ) {
                        ValueBgColor.Add( (int)eIndex, SKColors.LightPink ); // Sigh...Need some way to go back ^_^;
                    }
                } else {
                    // Else it's ok and so clear the value.
                    ValueBgColor.Remove( (int)eIndex );
                }

                return iValue;
            }

            return int.Parse( this[eIndex] );
        }

        public double ValueGetAsDbl( Names eIndex, double? dblDefault = null ) {
            string strProperty = ValueAsStr((int)eIndex);

            if( dblDefault.HasValue ) {
                if( !double.TryParse( strProperty, out double dblValue ) ) {
                    dblValue = dblDefault.Value;
                    ValueBgColor.Add( (int)eIndex, SKColors.LightPink ); 
                }

                return dblValue;
            }

            return double.Parse( strProperty );
        }

        /// <summary>
        /// This is ok to get us off the ground but. We're back to 
        /// the case where the parse keeps getting reset when the user
        /// is transmitting an image. So you can't type a callsign and
        /// get it parsed until the image is finished downloading.
        /// </summary>
        public override void DoParse() {
            _oWorkPlace.Queue( GetParseEnum(), iWaitMS:2000 );
        }

        public IEnumerator<int> GetParseEnum() {
            RenumberAndSumate();
            ParseColumn      ( 1 );

            Raise_DocFormatted();

            yield return 0;
        }
    }

    //public enum TxThreadErrors {
    //    DrawingException,
    //    WorkerException,
    //    BadDeviceException,
    //    ReadException,
    //    DiagnosticsException,
    //    StartException,
    //    StopException,
    //    DataOverflow
    //}

    public enum SSTVEvents {
        ModeChanged,
        FFT,
        UploadTime,
		DownLoadTime,
        DownLoadFinished,
        DownloadLevels,
        ImageUpdated,
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
            Frequency,
            Intercept,
            ClearImage,
        }

        public readonly Message _eMsg;
        public readonly object  _oParam;
        public readonly int     _iParam;

        public TVMessage( Message eMsg, object oParam = null, int iParam = 0 ) {
            _eMsg   = eMsg;
            _oParam = oParam;
            _iParam = iParam;
        }
    }

    // Arg, I wonder if I should unify with TVMessage. :-/
    public struct SSTVMessage {
        public SSTVMessage( SSTVEvents eEvent, string strMsg, object oParam2 = null ) {
            Event   = eEvent;
            Message = strMsg;
            Param   = -1;
            Param2  = oParam2;
        }

        public SSTVMessage( SSTVEvents eEvent, int iMsg ) {
            Event   = eEvent;
            Param   = iMsg;
            Message = string.Empty;
            Param2  = null;
        }

        public SSTVEvents Event  { get; }
        public string     Message{ get; }
        public int        Param  { get; }
        public object     Param2 { get; }
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
        readonly ConcurrentQueue<TVMessage>   _rgUItoBGQueue = new ConcurrentQueue<TVMessage>  ();

        Thread _oThread = null;
        Task   _oTxTask = null;

        public bool        StateTx => _oTxTask != null;
        public DocSSTVMode StateRx { get; protected set; }

        public event Action<SKPointI> Send_TxImageAspect;
        // Cheat for special layouts. Still working on this.
        protected SKSizeI Destination { get; set; } = new SKSizeI();
        // Shared selection. Last view to change it wins but I
        // expect only the resize view to attempt to change this.
        // These are in the bitmap's world coordinates.
        public SmartSelect Selection { get; } = new SmartSelect() { Mode = DragMode.FixedRatio };
		public SSTVMode    TransmitModeSelection { get; set; }

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
                FileName = strFileBase ?? throw new ArgumentNullException("File base string" );
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
            public string    FileName     { get; protected set; }
		}

        protected readonly IPgBaseSite       _oSiteBase;
		protected readonly IPgRoundRobinWork _oWorkPlace;
        protected readonly IPgStandardUI2    _oStdUI;


        public IPgParent Parentage => _oSiteBase.Host;
        public IPgParent Services  => Parentage;
        public bool      IsDirty   => false; // BUG: Time to implement this.

        public event SSTVPropertyEvent PropertyChange;

        public Editor              TemplateList  { get; }
        public Editor              MonitorList   { get; }

        public Editor              PortTxList    { get; } 
        public Editor              PortRxList    { get; }
        public Specification       RxSpec        { get; protected set; } = new Specification( 44100, 1, 0, 16 ); // Syncronous rc test, tx image 
        public SSTVFamilyDoc       RxSSTVFamilyDoc { get; }
        public SSTVModeDoc         RxSSTVModeDoc   { get; }
        public ModeEditor          TxModeList    { get; }
        public ImageWalkerDir      TxImageList   { get; }
        public ImageWalkerDir      RxHistoryList { get; }
        public SSTVProperties      Properties    { get; }
        internal DocImageEdit      TxBitmapComp  { get; }

        /// <summary>
        /// Used by the Resize dialog to make sure the selection matches the latest layout.
        /// </summary>
        public SKPointI TxImgLayoutAspect { get; protected set; } = new ( 1, 1 );

        protected Mpg123FFTSupport FileDecoder   { get; set; }

        // This is where our image and diagnostic image live.
		public ImageSoloDoc DisplayImage { get; protected set; }
		public ImageSoloDoc SyncImage    { get; protected set; }
        public ImageSoloDoc SignalLevel  { get; protected set; } // Don't really need bitmap, easy to use for now.

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
          //RxModeList    = new ModeEditor    ( new DocSlot( this, "SSTV Rx Modes" ) );
            TxModeList    = new ModeEditor    ( new DocSlot( this, "SSTV Tx Modes" ) );
            TxImageList   = new ImageWalkerDir( new DocSlot( this ) );
            RxHistoryList = new ImageWalkerDir( new DocSlot( this ) );
            TxBitmapComp  = new DocImageEdit  ( new DocSlot( this ) );
                          
            RxSSTVFamilyDoc = new SSTVFamilyDoc( new DocSlot(this) );
            RxSSTVModeDoc   = new SSTVModeDoc  ( new DocSlot(this) );

            PortTxList    = new Editor        ( new DocSlot( this ) );
            PortRxList    = new Editor        ( new DocSlot( this ) );
            MonitorList   = new Editor        ( new DocSlot( this ) );
                          
			DisplayImage  = new ImageSoloDoc( new DocSlot( this ) );
			SyncImage     = new ImageSoloDoc( new DocSlot( this ) );
            SignalLevel   = new ImageSoloDoc( new DocSlot( this ) );
                          
            Properties = new ( _oWorkPlace, new DocSlot( this ) );
            StateRx    = DocSSTVMode.Ready;
        }

        #region Dispose
        protected virtual void Dispose( bool disposing ) {
            if( !disposedValue ) {
                if( disposing ) {
                    // If init new fails then this won't get created.
                    if( FileDecoder != null )
                        FileDecoder.Dispose();

                    TransmitStop( true );
                    ReceiveLiveStop();

                    RxHistoryList.ImageUpdated -= OnImageUpdated_RxHistoryList;
                    TxImageList  .ImageUpdated -= OnImageUpdated_TxImageList;
                  //RxModeList   .CheckedEvent -= OnCheckedEvent_RxModeList;
                    TxModeList   .CheckedEvent -= OnCheckedEvent_TxModeList;
                    TemplateList .CheckedEvent -= OnCheckedEvent_TemplateList;

                    Properties.Dispose();
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

        public void LogError( string strMessage, Exception oEx ) {
            if( oEx != null ) {
                _oSiteBase.LogError( "SSTV", strMessage + ' ' + oEx.StackTrace );
            } else {
                LogError( strMessage );
            }
        }

        public string MyCall    => Properties.ValueAsStr((int)SSTVProperties.Names.Tx_MyCall).ToUpper();
        public string Message   => Properties.ValueAsStr((int)SSTVProperties.Names.Tx_Message);
        public string TheirCall => Properties.ValueAsStr((int)SSTVProperties.Names.Tx_TheirCall).ToUpper();
        public string RST       => Properties.ValueAsStr((int)SSTVProperties.Names.Tx_RST);

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
            TemplateList.LineAppend( "High Def Reply" );
            TemplateList.LineAppend( "High Def Reply Pnp" );
            
            return true;
        }

        public bool InitNew() {
            if( !TemplateList.InitNew() ) // Might need to init differently b/c of load.
                return false;
            if( !TxModeList  .InitNew() ) // Sends a "loaded" buffer event...
                return false;
            if( !TxBitmapComp.InitNew() )
                return false;

			if( !DisplayImage.InitNew() )
				return false;
			if( !SyncImage   .InitNew() )
				return false;
            if( !SignalLevel .InitNew() )
                return false;

            if( !MonitorList.InitNew() )
                return false;
            if( !PortTxList .InitNew() )
                return false;
            if( !PortRxList .InitNew() ) 
                return false;
            if( !RxSSTVModeDoc.InitNew() )
                return false;

            if( !Properties.InitNew() )
                return false;

            TemplateLoad();

            // Largest bitmap needed by any of the types I can decode.
            SKSizeI szMax = new( 800, 616 );
		    SyncImage   .Bitmap = new SKBitmap( szMax.Width, szMax.Height, SKColorType.Rgb888x, SKAlphaType.Unknown );
		    DisplayImage.Bitmap = new SKBitmap( szMax.Width, szMax.Height, SKColorType.Rgb888x, SKAlphaType.Opaque  );
            SignalLevel .Bitmap = new SKBitmap( 100, 10, SKColorType.Rgb888x, SKAlphaType.Opaque  );

            // Just set it up so it looks ok to start. Gets updated for each image downloaded.
			DisplayImage.WorldDisplay = new SKRectI( 0, 0, 320,         256 );
            SyncImage   .WorldDisplay = new SKRectI( 0, 0, szMax.Width, 256 );
            SignalLevel .WorldDisplay = new SKRectI( 0, 0, 100,          10 );

            SettingsInit(); // Loads up a bunch of properties here.

            string strMyDocs = Environment.GetFolderPath( Environment.SpecialFolder.MyDocuments );
            if( !TxImageList.LoadURL( strMyDocs ) ) {
				LogError( "Couldn't find pictures tx directory for SSTV" );
                return false;
			}

			RxSSTVFamilyDoc.Load( new SSTVDEM.EnumerateFamilies() );
            RxSSTVFamilyDoc.RegisterCheckEvent += OnCheckEvent_RxSSTVFamilyDoc;
			RxSSTVModeDoc  .Load( RxSSTVFamilyDoc.SelectedFamily.TvFamily );
            RxSSTVModeDoc  .RegisterCheckEvent += OnCheckEvent_RxSSTVModeDoc;

            // Set this after TxImageList load since the CheckedLine call will 
            // call Listen_ModeChanged and that calls the properties update event.
            //RxModeList.CheckedLine = RxModeList[0];

            // Get these set up so our stdproperties get the updates.
            TxImageList  .ImageUpdated += OnImageUpdated_TxImageList;
            RxHistoryList.ImageUpdated += OnImageUpdated_RxHistoryList;
            TxModeList   .CheckedEvent += OnCheckedEvent_TxModeList;
            TemplateList .CheckedEvent += OnCheckedEvent_TemplateList;

            // We'll get a callback from this before exiting!! O.o
            string strMyPics = Properties[SSTVProperties.Names.Rx_SaveDir];
            if( !RxHistoryList.LoadURL( strMyPics ) ) {
				LogError( "Couldn't find pictures history directory for SSTV" );
                return false;
            }

            RenderComposite(); // Duplicate. We'll possibly get call in callback above.

            Properties.ParseAll();
            _oWorkPlace.Queue( CreateTaskReceiver(), Timeout.Infinite );

            return true;
        }

        /// <summary>
        /// When a TV family is selected, load the modes for that family
        /// in the RxSSTVModeDoc. BUT if the family is "none" there are no
        /// modes defined for it. So we must enqueue the null message here.
        /// </summary>
        /// <param name="obj"></param>
        private void OnCheckEvent_RxSSTVFamilyDoc(Row obj) {
			try {
				if( RxSSTVFamilyDoc.SelectedFamily is SSTVDEM.SSTVFamily oNewFamily ) {
                    RxSSTVModeDoc.Load( oNewFamily.TvFamily ); 

                    if( oNewFamily.TvFamily == TVFamily.None ) {
                        // Go back to "auto" detect on the decoder...
                        _rgUItoBGQueue.Enqueue( new TVMessage( TVMessage.Message.TryNewMode, null ) );
                    }
				}
			} catch( Exception oEx ) {
				Type[] rgErrors = { typeof( NullReferenceException ),
									typeof( ArgumentOutOfRangeException ),
                                    typeof( InvalidOperationException ) };
				if( rgErrors.IsUnhandled( oEx ) )
					throw;

				LogError( "RXProperties OnSelectionChangeCommitted_Family unexpected." );
			}
        }

        private void OnCheckEvent_RxSSTVModeDoc(Row oRow) {
            if( oRow is SSTVModeDoc.DDRow oModeRow ) {
                _rgUItoBGQueue.Enqueue( new TVMessage( TVMessage.Message.TryNewMode, oModeRow.Mode ) );
            }
        }

        public void PostBGMessage( TVMessage.Message eMsg ) {
            _rgUItoBGQueue.Enqueue( new TVMessage( eMsg, null ) );
        }

        public void PostBGMessage( TVMessage.Message eMsg, int iParam ) {
            _rgUItoBGQueue.Enqueue( new TVMessage( eMsg, null, iParam ) );
        }

        public void InitDeviceList() {
			IEnumerator<string> iterOutput = MMHelpers.GetOutputDevices();

            PortTxList .Clear();
            MonitorList.Clear();

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
                          //PropertyLoadFromXml( RxModeList, oNode );
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
                XmlDocument oXDoc = new ();
                XmlElement  oRoot = oXDoc.CreateElement( "MySSTV" );
                oXDoc.AppendChild( oRoot );

                Action<string, SSTVProperties.Names> StringProperty = delegate ( string strName, SSTVProperties.Names eProperty ) { 
                    XmlElement oElem = oXDoc.CreateElement( strName );
                    oElem.InnerText = Properties.ValueAsStr( (int)eProperty ); // Safer than .ToString()...
                    oRoot.AppendChild( oElem ); 
                };
                Action<string, Editor> CheckProperty = delegate( string strName, Editor oEditor ) {
                    XmlElement oElem = oXDoc.CreateElement( strName );
                    if( oEditor.CheckedLine != null ) {
                        oElem.InnerText = oEditor.CheckedLine.ToString();
                        oRoot.AppendChild( oElem );
                    }
                };

                CheckProperty ( "RxDevice",       PortRxList );
                CheckProperty ( "TxDevice",       PortTxList );
                CheckProperty ( "MonitorDevice",  MonitorList );
              //CheckProperty ( "RxMode",         RxModeList );
                CheckProperty ( "TxMode",         TxModeList );
                CheckProperty ( "Template",       TemplateList );
                StringProperty( "ImageQuality",   SSTVProperties.Names.Std_ImgQuality );
                StringProperty( "DigiOutputGain", SSTVProperties.Names.Std_MicGain );
                StringProperty( "MyCall",         SSTVProperties.Names.Tx_MyCall );
                StringProperty( "Message",        SSTVProperties.Names.Tx_Message );
                StringProperty( "TxSrcDir",       SSTVProperties.Names.Tx_SrcDir );
                StringProperty( "Clock",          SSTVProperties.Names.Std_Frequency );

                oXDoc.Save( oStream );
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
            Properties.ValueUpdate( SSTVProperties.Names.Tx_SrcDir,  TxImageList.CurrentShowPath );
            Properties.ValueUpdate( SSTVProperties.Names.Tx_SrcFile, TxImageList.CurrentShowFile );

            if( TxImageList.Bitmap != null ) {
			    Selection.SetRect( 0, 0, TxImageList.Bitmap.Width, TxImageList.Bitmap.Height );
            } else {
                Selection.SetRect( 0, 0, 0, 0 );
            }
            TxBitmapComp.Clear(); // We have references to TxImageList.Bitmap we must clear;
            RenderComposite();
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
            Properties.ValueUpdate( SSTVProperties.Names.Rx_SaveDir,     RxHistoryList.CurrentShowPath );
            Properties.ValueUpdate( SSTVProperties.Names.Rx_HistoryFile, RxHistoryList.CurrentShowFile );

            if( StateRx == DocSSTVMode.DeviceRead ) {
                _rgUItoBGQueue.Enqueue( new TVMessage( TVMessage.Message.ChangeDirectory, RxHistoryList.CurrentDirectory ) );
            }
            TxBitmapComp.Clear(); // We have references to RxHistoryList.Bitmap we must clear;
            RenderComposite();
        }

        /// <summary>
        /// This is a mess. I need to sort out the messaging. Only the
        /// thread based system has been tested recently.
        /// </summary>
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
             // LogError( "No Rx Images Here." );
                return;
            } 

			try {
				TxBitmapComp.Clear();

				switch( iIndex ) {
					case 0: // PnP reply.
						TxBitmapComp.AddImage( LOCUS.LOWERRIGHT, 10, 10,  40.0, RxHistoryList.Bitmap, null );
						TxBitmapComp.AddText ( LOCUS.UPPERLEFT,   5,  5,  17.0, TxBitmapComp.StdFace, ForeColor, TemplateReplyFromProps() );
						TxBitmapComp.AddImage( LOCUS.CENTER,      0,  0, 100.0, TxImageList.Bitmap, Selection );

                        Send_TxImageAspect?.Invoke( new SKPointI( oMode.Resolution.Width, oMode.Resolution.Height ) );
                        TxImgLayoutAspect = new ( oMode.Resolution.Width, oMode.Resolution.Height );
						break;
					case 1: // General Message
						TxBitmapComp.AddText ( LOCUS.UPPERLEFT,   5,  5,  20.0, TxBitmapComp.StdFace, ForeColor, Message );
						TxBitmapComp.AddImage( LOCUS.CENTER,      0,  0, 100.0, TxImageList.Bitmap, Selection );

                        Send_TxImageAspect?.Invoke( new SKPointI( oMode.Resolution.Width, oMode.Resolution.Height ) );
                        TxImgLayoutAspect = new ( oMode.Resolution.Width, oMode.Resolution.Height );
						break;
                    case 2: // General Message PnP
						TxBitmapComp.AddImage( LOCUS.LOWERRIGHT, 10, 10,  40.0, RxHistoryList.Bitmap, null );
						TxBitmapComp.AddText ( LOCUS.UPPERLEFT,   5,  5,  15.0, TxBitmapComp.StdFace, ForeColor, Message );
						TxBitmapComp.AddImage( LOCUS.CENTER,      0,  0, 100.0, TxImageList.Bitmap, Selection );

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
                        TemplateSetHiDefMessage( oMode, TemplateReplyFromProps() );
                        break;
                    case 7:
                        TemplateSetHiDefReplyPnP( oMode );
                        break;
				}

                // Count so the stream object on the editor will seek correctly amoung the lines.
                TxBitmapComp.Layers.CharacterCount( 0 );
                TxBitmapComp.Layers.Raise_BufferEvent( BUFFEREVENTS.MULTILINE );
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
            LayoutSingleLine oSingle = new( new FTCacheWrap( oLine ) { Justify = Align.Center }, LayoutRect.CSS.None ) 
                                         { BgColor = SKColors.Transparent, 
                                           FgColor = fHighContrast ? SKColors.White : ForeColor };

            // Since we flex, do all this before layout children.
            uint      uiPoints = (uint)( uiPixHeight * iScreenPixPerInch / skEMsPerInch.Y );
            uint      uiFontID = _oStdUI.FontCache( TxBitmapComp.StdFace, uiPoints, skEMsPerInch );
            oSingle.Cache.Measure( _oStdUI.FontRendererAt( uiFontID ) );

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
            FTCacheWrap          oElem = new( oLine ) { Justify = Align.Center };
            LayoutSingleLine     oText = new( oElem, LayoutRect.CSS.Flex ) 
                                         { BgColor = SKColors.Black, FgColor = SKColors.White };

            // Since we flex, do all this before layout children.
            const double dblFractionalHeight = 18 / 100.0;
            SKPoint          skEMsPerInch    = new SKPoint(96, 96);
            const int      iScreenPixPerInch = 72;
            uint                 uiMsgHeight = (uint)((double)oMode.Resolution.Height * dblFractionalHeight );

            uint      uiPoints = (uint)( uiMsgHeight * iScreenPixPerInch / skEMsPerInch.Y );
            uint      uiFontID = _oStdUI.FontCache( TxBitmapComp.StdFace, uiPoints, skEMsPerInch );
            oText.Cache.Measure( _oStdUI.FontRendererAt( uiFontID ) );

            LayoutImage oImage = new LayoutImage( TxImageList.Bitmap, LayoutRect.CSS.None ) { Stretch = true };
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
            oElem.Measure( _oStdUI.FontRendererAt( uiFontID ) );
            oElem.OnChangeSize( oImage.Width );

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
            FTCacheWrap        oWrap = new FTCacheWrap( oLine ) { Justify = Align.Center };
            LayoutSingleLine oSingle = new( oWrap, LayoutRect.CSS.Flex ) 
                                         { Track = 60, BgColor = SKColors.Transparent, 
                                           FgColor = SKColors.White };

            // Since we flex, do all this before layout children.
            uint      uiPoints = (uint)( uiPixHeight * iScreenPixPerInch / skEMsPerInch.Y );
            uint      uiFontID = _oStdUI.FontCache( TxBitmapComp.StdFace, uiPoints, skEMsPerInch );
            oSingle.Cache.Measure( _oStdUI.FontRendererAt( uiFontID ) );
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
            oWrap.Measure( _oStdUI.FontRendererAt( uiFontID ) );
            oWrap.OnChangeSize( oSingle.Width );

            //SelectionAdjust( oImage1 );
            TxImgLayoutAspect = new ( oImage1.Width, oImage1.Height );

            TxBitmapComp.AddLayout( oVertiMain );
        }

        protected class TxState : IEnumerable<int>, IDisposable {
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

				switch( oMode.TvFamily ) {
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
                        switch( oMode.LegacyMode ) {
                            case AllSSTVModes.smR72:
                            case AllSSTVModes.smR24:
                                _oSSTVGenerator = new GenerateRobot422( skBitmap, _oSSTVModulator, oMode ); 
                                break;
                            case AllSSTVModes.smR36:
                                _oSSTVGenerator = new GenerateRobot420( skBitmap, _oSSTVModulator, oMode );
                                break;
                            default:
						        throw new ArgumentOutOfRangeException( nameof( oMode ) );
                        } break;
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
                _oBGtoFGQueue.Enqueue( new( SSTVEvents.ThreadException, "Worker Exception" ) );
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

            public void Dispose() {
                _oPlayer.Dispose();
            }
        }

        protected int MicrophoneGain => Properties.ValueGetAsInt( SSTVProperties.Names.Std_MicGain );

		public bool RenderComposite() {
            SSTVMode oMode = TransmitModeSelection;
			// sometimes we get events while we're sending. Let's block render for now.
			if( StateTx ) {
				LogError( "Already Playing" );
				return false;
			}

			if( oMode == null ) {
			    //LogError( "Problem prepping template for transmit." );
			    return false;
			}

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
		}

        /// <summary>
        /// Clears the Transmit task which will cause any bg TX task to exit.
        /// Wait until it's done and return. 
        /// </summary>
        /// <param name="fInExit">True if we are in the dispose and we don't
        /// want to generate any events.</param>
        public void TransmitStop( bool fInExit = false ) {
            Task oTxTask = _oTxTask;
            _oTxTask = null;         // this is the signal to the background task to abort.

            if( oTxTask != null ) {
                oTxTask.Wait();      // shouldn't take more than 2 seconds or so.
                oTxTask.Dispose();
            }

            if( !fInExit ) {
                TxModeList.HighLight = null;
                _oSiteBase.Notify( ShellNotify.MediaStatusChanged );
            }
        }

        /// <summary>
        /// Begin transmitting the image. We can stop but only after the 
        /// buffered transmission bleeds out.
        /// </summary>
        public void TransmitBegin( SSTVMode oMode ) {
            if( StateTx ) {
                LogError( "Already Transmitting" ); 
                return;
            }
            if( oMode == null || TxBitmapComp.Bitmap == null ) {
                LogError( "Transmit mode or image is not set." ); 
                return;
            }
            if( PortTxList.CheckedLine == null ) {
                LogError( "No sound device to send to" ); 
                return;
            }
            if( MonitorList.CheckedLine != null &&
                MonitorList.CheckedLine.At ==
                PortTxList .CheckedLine.At ) {
                LogError( "Transmit and Monitor sound devices must not be the same!!" ); 
                return;
            }
            if( StateRx != DocSSTVMode.DeviceRead ) {
                // we need the receive listener polling to get updates for
                // or tx progress. Starting the polling here might
                // get us in a weird mode. This is easier for now.
                LogError( "Start device receive first." );
                return;
            }

            void oTransmitAction() {
                SKBitmap bmpCopy = TxBitmapComp.Bitmap.Copy();
                double   dblFreq = Properties.ValueGetAsDbl(SSTVProperties.Names.Std_Frequency);
                TxState   oState = null;
                try {
                    oState = new TxState(oMode, dblFreq, MicrophoneGain,
                                            PortTxList.CheckedLine.At,
                                            bmpCopy, _rgBGtoUIQueue);
                } catch( Exception oEx ) {
                    // BUG: sometimes the device list needs updating.
                    Type[] rgErrors = { typeof( BadDeviceIdException ),
                                        typeof( NullReferenceException ),
                                        typeof( ArgumentException ),
                                        typeof( ArgumentNullException ) };
                    if( rgErrors.IsUnhandled(oEx) )
                        throw;

                    //LogError( "Problem talking to device." );
                    //Need to send a message.
                    bmpCopy.Dispose();

                    return;
                }

                // Use WWV to find the precise sample frequency of sound card. 
                // TODO: Port the tuner from MMSSTV and make it a property.

                foreach( uint uiWait in oState ) {
                    if( StateTx == false )
                        break;
                    Thread.Sleep((int)uiWait);
                }
                if( StateTx == true )
                    Thread.Sleep(1000); // Let the buffer bleed out a little.

                bmpCopy.Dispose();
            }

            _oTxTask = new Task( oTransmitAction );

            _oTxTask.Start();
            _oSiteBase.Notify( ShellNotify.MediaStatusChanged );
        }

        /// <summary>
        /// This is our task to poll the Background to UI Queue. It serves both
        /// the receive thread and the transmit thread. TX and RX can run
        /// concurrently with no problems.
        /// </summary>
        public IEnumerator<int> CreateTaskReceiver() {
            while( true ) {
                while( _rgBGtoUIQueue.TryDequeue( out SSTVMessage sResult ) ) {
                    switch( sResult.Event ) {
                        case SSTVEvents.DownloadLevels: {
                            SignalLevelRender( sResult );
                        } break;
                        case SSTVEvents.ModeChanged: {
                            SSTVMode oMode = RxSSTVModeDoc.GetDescriptor( (AllSSTVModes)sResult.Param );

                            if( oMode == null ) {
                                RxSSTVFamilyDoc.ResetFamily();
                            } else {
                                RxSSTVFamilyDoc.SelectFamily( oMode.TvFamily );

			                    DisplayImage.WorldDisplay = new SKRectI( 0, 0, oMode.Resolution.Width, oMode.Resolution.Height );
                                SyncImage   .WorldDisplay = new SKRectI( 0, 0, SyncImage.Bitmap.Width, oMode.Resolution.Height / oMode.ScanMultiplier );

                                Properties.ValueUpdate( SSTVProperties.Names.Rx_Mode,   oMode.FamilyName + ' ' + oMode.Version );
                                Properties.ValueUpdate( SSTVProperties.Names.Rx_Width,  oMode.Resolution.Width .ToString() );
                                Properties.ValueUpdate( SSTVProperties.Names.Rx_Height, oMode.Resolution.Height.ToString() );
                            }
				            if( RxSSTVFamilyDoc.SelectedFamily is SSTVDEM.SSTVFamily oNewFamily ) {
                                RxSSTVModeDoc.Load( oNewFamily.TvFamily, oMode is not null ? oMode.LegacyMode : AllSSTVModes.smEND );
				            }
                            PropertyChange?.Invoke( SSTVEvents.ModeChanged );
                        } break;
                        case SSTVEvents.ImageUpdated:
                            // A little bit skanky. First place we need public access to the Raise event...
                            DisplayImage.Raise_ImageUpdated();
                            SyncImage   .Raise_ImageUpdated();
                            break;
                        case SSTVEvents.UploadTime:
                            Properties.ValueUpdate( SSTVProperties.Names.Tx_Progress, sResult.Param.ToString( "D2" ) + "%", Broadcast:true );
                            break;
                        case SSTVEvents.DownLoadTime: 
                            Properties.ValueUpdate( SSTVProperties.Names.Rx_Progress, sResult.Param.ToString( "D2" ) + "%", Broadcast:true );
                            PropertyChange?.Invoke( SSTVEvents.DownLoadTime );
                            DisplayImage.Raise_ImageUpdated();
                            SyncImage   .Raise_ImageUpdated();
                            break;
                        case SSTVEvents.DownLoadFinished: // NOTE: This comes along unreliably in the device streaming case.
                            Properties.ValueUpdate( SSTVProperties.Names.Rx_Progress, sResult.Param.ToString( "D2" ) + "% - Complete", Broadcast:true );
                            PropertyChange?.Invoke( SSTVEvents.DownLoadFinished );

                            RxSSTVFamilyDoc.SelectFamily( TVFamily.None );
                            RxSSTVModeDoc.HighLight = null;

                            RxHistoryList.Refresh();
                            break;
                        case SSTVEvents.ImageSaved: 
                            PropertyChange?.Invoke( SSTVEvents.ImageSaved );
                            RxHistoryList.Refresh();
                            break;
                        case SSTVEvents.ThreadExit:
                            // If there's an abort, you'll get that message and then this one.
                            Properties.ValueUpdate( SSTVProperties.Names.Std_Process, "Rx Live: Stopped - " + sResult.Param.ToString(), true );
                            break;
                        case SSTVEvents.ThreadAbort:
                            if( _oThread == null ) {
                                LogError( "Image Thread Abort on Null." );
                            } else { 
                                LogError( "Image Thread Abort." );
                            }
                          //RxModeList.HighLight   = null;
                          //RxModeList.CheckedLine = RxModeList[0];
                            RxSSTVFamilyDoc.SelectFamily( TVFamily.None );
                            RxSSTVModeDoc.HighLight = null;

                            if( sResult.Param2 is Exception oEx ) {
                                LogError( oEx.StackTrace );
                            }

                            _oThread = null;
                            break; 
                        case SSTVEvents.ThreadException:
                            try {
                                LogError( sResult.Message + " halting device read" );
                                ReceiveLiveStop(); 

                                if( sResult.Param2 is Exception oExThread ) {
                                    LogError( oExThread.StackTrace );
                                    LogError( oExThread.Message );
                                }
                            } catch( NullReferenceException ) {
                                LogError( "General error in thread messaging." );
                            }
                            break;
                    }
                }

                if( _oTxTask != null && _oTxTask.IsCompleted ) {
                    _oSiteBase.Notify( ShellNotify.MediaStatusChanged );
                    _oTxTask.Dispose();
                    _oTxTask = null;
                }

                Properties.CheckParse();

                yield return 100; // wait 1/10 of a second. Makes the level indicator more responsive.
            };
        }

        protected void SignalLevelRender( SSTVMessage msg ) {
            if( SignalLevel.Bitmap != null &&
                msg.Param2 is SSTVDEM.Levels oLevel
            ) {
                using SKCanvas skCanvas = new( SignalLevel.Bitmap );
                using SKPaint  skPaint  = new() { Color = SKColors.Black };

                skCanvas.DrawRect( 0, 0, SignalLevel.Bitmap.Width, SignalLevel.Bitmap.Height, skPaint );

                skPaint.Color = oLevel.CurrColor;

                skCanvas.DrawRect( 0, 0, (float)(SignalLevel.Bitmap.Width * oLevel.Current / 100), SignalLevel.Bitmap.Height, skPaint );

                float flLineX = (float)(SignalLevel.Bitmap.Width * oLevel.Peak / 100);
                skPaint.Color = oLevel.PeakColor;
                skPaint.StrokeWidth = 2;

                skCanvas.DrawLine( flLineX, 0, flLineX, SignalLevel.Bitmap.Height, skPaint );

                SignalLevel.Raise_ImageUpdated();
            }
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

            //RxModeList.HighLight   = null;
            //RxModeList.CheckedLine = RxModeList[0]; see ReceiveLiveBegin. 

            RxHistoryList.LoadAgain( RxHistoryList.CurrentDirectory );

            _oWorkPlace.Pause();
            StateRx = DocSSTVMode.Ready;
            _rgBGtoUIQueue.Clear();
        }

        /// <summary>
        /// Reset to "auto". No longer actively decoding an sstv image and looking
        /// for the signal id to start download again.
        /// </summary>
        public void ResetMode() {
            try {
                if( !_oThread.IsAlive ) {
                    LogError( "Make sure you have stared listening mode: 'press play'." );
                    return;
                }
              //RxModeList.CheckedLine = RxModeList[0];
                RxSSTVFamilyDoc.SelectFamily( TVFamily.None );
            } catch( Exception oEx ) {
                Type[] rgErrors = { typeof( NullReferenceException ),
                                    typeof( ArgumentOutOfRangeException ) };
                if( rgErrors.IsUnhandled( oEx ) )
                    throw;

                LogError( "Problem setting RX mode to auto." );
            }
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
                    int iMicrophone = -1; 
                    int iMonitor    = -1;

                    // Technically this is optional, but I've got to sort that out.
                    // BUG: Monitor and TX device should not be the same.
                    if( MonitorList.CheckedLine != null ) {
                        iMonitor = MonitorList.CheckedLine.At;
                    } else {
                        LogError( "Please select an sound monitor device" );
                        return;
                    }
                    if( PortRxList.CheckedLine != null ) {
                        iMicrophone = PortRxList.CheckedLine.At;
                    } else {
                        LogError( "Please select an sound input device" );
                        return;
                    }

                    _rgUItoBGQueue.Clear();
                    _rgBGtoUIQueue.Clear();

                    // File reader used to clear this value on each run but I like keeping that
                    // value for similar reads. So Reset the mode on Device read begin. No events
                    // or our task will be getting an event we don't want.
                  //RxModeList.CheckedReset = RxModeList[0];
                    RxSSTVFamilyDoc.SelectFamily( TVFamily.None );

                    int    iQuality    = Properties.ValueGetAsInt( SSTVProperties.Names.Std_ImgQuality, 80 );
                    double dblFreq     = Properties.ValueGetAsDbl( SSTVProperties.Names.Std_Frequency,  11028 );
                    string strSaveDir  = Properties[ SSTVProperties.Names.Rx_SaveDir  ];

                    // Just note, if we do a file read, we might no longer be in the MyPictures path.
                    if( string.IsNullOrEmpty( strSaveDir ) ) {
			            strSaveDir = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
                        Properties.ValueUpdate( SSTVProperties.Names.Rx_SaveDir, strSaveDir, Broadcast:true );
                    }

                    DeviceListeningState oWorker = new DeviceListeningState(
                        iMonitor, iMicrophone,
                        dblFreq,
                        iQuality, strSaveDir, String.Empty,
                        _rgBGtoUIQueue, _rgUItoBGQueue,
                        SyncImage.Bitmap, DisplayImage.Bitmap);

                    //PortListening oWorker = new PortListening( 
                    //    5, iMonitor,
                    //    iQuality, strSaveDir,
                    //    _rgBGtoUIQueue, _rgUItoBGQueue, 
                    //    SyncImage.Bitmap, DisplayImage.Bitmap );

                    _oThread = new Thread( oWorker.DoWork ) { Priority = ThreadPriority.AboveNormal };
                    _oThread.Start(); // Can send out of memory exception!

                    _oWorkPlace.Start( 1 );
                    Properties.ValueUpdate( SSTVProperties.Names.Std_Process, "Rx Live: Started.", true );
                    StateRx = DocSSTVMode.DeviceRead;
                } catch( Exception oEx ) {
                    Type[] rgErrors = { typeof( NullReferenceException ),
                                        typeof( ArgumentNullException ),
                                        typeof( FormatException ),
                                        typeof( OverflowException ),
                                        typeof( ThreadStateException ),
                                        typeof( OutOfMemoryException ),
                                        typeof( InsufficientMemoryException ),
                                        typeof( BadDeviceIdException ) };
                    if( rgErrors.IsUnhandled( oEx ) )
                        throw;

                    Properties.ValueUpdate( SSTVProperties.Names.Std_Process, "Rx Live: Error!", true );
                    LogError( "Couldn't launch device listening thread." );
                }

                _oSiteBase.Notify( ShellNotify.MediaStatusChanged );
            }
        }

        public void ReceiveLiveStop() {
            //RxModeList.HighLight = null;
            RxSSTVModeDoc  .HighLight = null;
            RxSSTVFamilyDoc.HighLight = null;

            Properties.ValueUpdate( SSTVProperties.Names.Std_Process, "Stopping...", true );

            _rgUItoBGQueue.Enqueue( new TVMessage( TVMessage.Message.ExitWorkThread ) );
            _oThread = null;

            Properties.ValueUpdate( SSTVProperties.Names.Std_Process, "Stopped: All.", true );

            _oWorkPlace.Pause(); // TODO: flush the message buffers? Probably should.
            StateRx = DocSSTVMode.Ready;
            _oSiteBase.Notify( ShellNotify.MediaStatusChanged );
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
			            oDoc.TxBitmapComp.Load( oDoc.TxImageList.Bitmap, skSelect, oMode.Resolution );

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

					    _oSSTVGenerator = oMode.TvFamily switch {
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

                _oDoc.RxSSTVFamilyDoc.SelectFamily( tvMode.TvFamily );

                // the mode objects in the list might be same spec but copied or something
                // match them via their legacy modes. Set up equivanlance test later.
                //Line oFoundLine = null;
                //foreach( Line oLine in _oDoc.RxModeList ) {
                //    if( oLine.Extra is SSTVMode oLineMode ) {
                //        if( oLineMode.LegacyMode == tvMode.LegacyMode )
                //            oFoundLine = oLine;
                //    }
                //}
                //_oDoc.RxModeList.HighLight = oFoundLine;
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
