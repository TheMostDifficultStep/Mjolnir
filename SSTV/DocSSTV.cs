using System;
using System.IO;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Text;
using System.Threading;

using SkiaSharp;
using NAudio.Wave;

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
    public class RxProperties : DocProperties {
        public enum Names : int {
			Mode,
            Width,
            Height,
            Progress,
            SaveHere
        }

        public RxProperties( IPgBaseSite oSiteBase ) : base( oSiteBase ) {
        }

        public override bool InitNew() {
            if( !base.InitNew() ) 
                return false;

            foreach( Names eName in Enum.GetValues(typeof(Names)) ) {
                Property_Labels.LineAppend( string.Empty, fUndoable:false );
                Property_Values.LineAppend( string.Empty, fUndoable:false );
            }

            LabelSet( Names.Mode,     "Mode", new SKColor( red:0xff, green:0xbf, blue:0 ) );
            LabelSet( Names.Width,    "Width" );
            LabelSet( Names.Height,   "Height" );
            LabelSet( Names.Progress, "Received" );
            LabelSet( Names.SaveHere, "Save At" );

            Clear();

            return true;
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
        /// <summary>
        /// Override the clear to only clear the specific repeater information. If you want to 
        /// clear all values, call the base method.
        /// </summary>
        public override void Clear() {
            string strSaveDir = Environment.GetFolderPath( Environment.SpecialFolder.MyPictures );
            string strEmpty   = "_";

            ValueUpdate( Names.Mode,     strEmpty,   Broadcast:false ); 
            ValueUpdate( Names.Width,    strEmpty,   Broadcast:false );  
            ValueUpdate( Names.Height,   strEmpty,   Broadcast:false ); 
            ValueUpdate( Names.Progress, strEmpty,   Broadcast:false );
            ValueUpdate( Names.SaveHere, strSaveDir, Broadcast:true );
        }
    }

    /// <summary>
    /// This subclass of the DocProperties let's us have static index values. This is advantageous because it
    /// allows us to re-arrange property values without scrambling their meaning. But it also means you can't
    /// use some kind of runtime forms generator since the indicies must have corresponding pre compiled enum's.
    /// </summary>
    public class TxProperties : DocProperties {
        public enum Names : int {
			Mode,
            Width,
            Height,
            Progress,
            FileName
        }

        public TxProperties( IPgBaseSite oSiteBase ) : base( oSiteBase ) {
        }

        public override bool InitNew() {
            if( !base.InitNew() ) 
                return false;

            foreach( Names eName in Enum.GetValues(typeof(Names)) ) {
                Property_Labels.LineAppend( string.Empty, fUndoable:false );
                Property_Values.LineAppend( string.Empty, fUndoable:false );
            }

            LabelSet( Names.Mode,     "Mode", new SKColor( red:0xff, green:0xbf, blue:0 ) );
            LabelSet( Names.Width,    "Width" );
            LabelSet( Names.Height,   "Height" );
            LabelSet( Names.Progress, "Sent" );
            LabelSet( Names.FileName, "FileName" );

            Clear();

            return true;
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

        /// <summary>
        /// Override the clear to only clear the specific repeater information. If you want to 
        /// clear all values, call the base method.
        /// </summary>
        public override void Clear() {
            ValueUpdate( Names.Mode,     "-", Broadcast:false ); 
            ValueUpdate( Names.Width,    "-", Broadcast:false ); 
            ValueUpdate( Names.Height,   "-", Broadcast:false ); 
            ValueUpdate( Names.Progress, "-", Broadcast:false );
            ValueUpdate( Names.FileName, "-", Broadcast:true );
        }
    }

    public class StdProperties : DocProperties {
        public enum Names : int {
			TxPort,
            RxPort,
            Quality,
            SaveDir
        }

        public StdProperties( IPgBaseSite oSiteBase ) : base( oSiteBase ) {
        }

        public override bool InitNew() {
            if( !base.InitNew() ) 
                return false;
            
            foreach( Names eName in Enum.GetValues(typeof(Names)) ) {
                Property_Labels.LineAppend( string.Empty, fUndoable:false );
                Property_Values.LineAppend( string.Empty, fUndoable:false );
            }

            LabelSet( Names.TxPort,        "Transmit to Device" );
            LabelSet( Names.RxPort,        "Receive from Device" );
            LabelSet( Names.Quality,       "Image Save Quality" );
            LabelSet( Names.SaveDir,       "Save Directory" );

            InitValues();

            return true;
        }

        /// <summary>
        /// These are our default values. We'll look for them from a save file in the future.
        /// </summary>
        public void InitValues() {
            string strMyPicDir = Environment.GetFolderPath( Environment.SpecialFolder.MyPictures );

            ValueUpdate( StdProperties.Names.Quality,        "80", false );
            ValueUpdate( StdProperties.Names.SaveDir, strMyPicDir, false );
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

    public enum SSTVEvents {
        ALL,
        UploadTime,
        SSTVMode,
        FFT,
        TXImageChanged,
		RXImageNew,
		DownLoadTime,
        DownLoadFinished,
        ThreadDrawingException,
        ThreadWorkerException,
        ThreadReadException,
        ThreadDiagnosticsException,
        ThreadAbort,
        ThreadExit
    }

    public delegate void SSTVPropertyChange( SSTVEvents eProp );

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

    public class DocSSTV :
        IPgParent,
        IPgLoad<TextReader>,
        IPgSave<TextWriter>,
        IDisposable
    {
        private bool disposedValue;
        readonly ConcurrentQueue<SSTVEvents> _rgBGtoUIQueue  = new ConcurrentQueue<SSTVEvents>(); // From BG thread to UI thread.
        readonly ConcurrentQueue<double>     _rgDataQueue    = new ConcurrentQueue<double>();        // From UI thread to BG thread.
        readonly ConcurrentQueue<TVMessage>  _rgUItoBGQueue  = new ConcurrentQueue<TVMessage>();

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

        public event SSTVPropertyChange PropertyChange;

        public StdProperties Properties { get; }
        public FileChooser   RecChooser { get; } // Recorded wave files.

        public Editor              PortTxList    { get; } 
        public Editor              PortRxList    { get; }
        public Editor              RxDirectory   { get; } // Files in the receive directory.
        public Specification       RxSpec        { get; protected set; } = new Specification( 44100, 1, 0, 16 ); // Syncronous rc test, tx image 
        public ModeEditor          RxModeList    { get; }
        public ModeEditor          TxModeList    { get; }
        public ImageWalkerDir      TxImageList   { get; }
        public RxProperties        RxProperties  { get; }
        public TxProperties        TxProperties  { get; }
        public SKBitmap            TxBitmap      => TxImageList.Bitmap;

        protected readonly ImageSoloDoc  _oDocSnip;   // Clip the image.

        protected Mpg123FFTSupport FileDecoder   { get; set; }
        protected BufferSSTV       _oSSTVBuffer; // BUG: Can't find where initialized!!
        protected SSTVMOD          _oSSTVModulator;
        protected SSTVDEM          _oSSTVDeModulator; // Only used by test code.
		protected IPgPlayer        _oPlayer;
        protected SSTVGenerator    _oSSTVGenerator;
		protected SSTVDraw         _oRxSSTV; // Only used by test code.

		public ImageSoloDoc ReceiveImage { get; protected set; }
		public ImageSoloDoc SyncImage    { get; protected set; }

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

            RxModeList   = new ModeEditor    ( new DocSlot( this, "SSTV Rx Modes" ) );
            TxModeList   = new ModeEditor    ( new DocSlot( this, "SSTV Tx Modes" ) );
            TxImageList  = new ImageWalkerDir( new DocSlot( this ) );
            _oDocSnip    = new ImageSoloDoc  ( new DocSlot( this ) );
            RxDirectory  = new Editor        ( new DocSlot( this ) );
            PortTxList   = new Editor        ( new DocSlot( this ) );
            PortRxList   = new Editor        ( new DocSlot( this ) );

			ReceiveImage = new ImageSoloDoc( new DocSlot( this ) );
			SyncImage    = new ImageSoloDoc( new DocSlot( this ) );

            RxProperties = new ( new DocSlot( this ) );
            TxProperties = new ( new DocSlot( this ) );
            Properties   = new ( new DocSlot( this ) );
            RecChooser   = new ( new DocSlot( this ) );
        }

        #region Dispose
        protected virtual void Dispose( bool disposing ) {
            if( !disposedValue ) {
                if( disposing ) {
                    if( _oDocSnip != null )
			            _oDocSnip.Dispose(); 

                    // If init new fails then this won't get created.
                    if( FileDecoder != null )
                        FileDecoder.Dispose();
                    if( _oPlayer != null )
                        _oPlayer.Dispose();

                    ReceiveLiveStop();
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
                    sbValue.Append( " x " );
                    sbValue.Append( oMode.Resolution.Height.ToString() );
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

        /// <summary>
        /// This is where we receive the event when the user selects a mode in the chooser. 
        /// Don't be confused that we send the mode name to the 'Tx Property viewers', but
        /// the 'Tx Viewers' use this event to update their aspect ratio for selection.
        /// </summary>
        /// <param name="oLine">The line in the modelist selected</param>
        /// <seealso cref="RxModeList"/>
        void OnCheckedEvent_RxModeList( Line oLine ) {
            TxProperties.ValueUpdate( TxProperties.Names.Mode, oLine.ToString(), Broadcast:true ); 
            Raise_PropertiesUpdated( SSTVEvents.SSTVMode );
        }

        public bool InitNew() {
            if( !RxModeList .InitNew() ) 
                return false;
            if( !TxModeList .InitNew() ) 
                return false;
			if( !_oDocSnip.InitNew() )
				return false;

			if( !ReceiveImage.InitNew() )
				return false;
			if( !SyncImage   .InitNew() )
				return false;
            if( !RxDirectory .InitNew() )
                return false;
            if( !PortTxList.InitNew() )
                return false;
            if( !PortRxList.InitNew() ) 
                return false;

            if( !Properties  .InitNew() )
                return false;
            if( !TxProperties.InitNew() )
                return false;
            if( !RxProperties.InitNew() )
                return false;
            
            
            new ParseHandlerText( Properties.Property_Values, "text" );

            SettingsInit();

            string strMyDocs = Environment.GetFolderPath( Environment.SpecialFolder.MyDocuments );
            if( !TxImageList.LoadURL( strMyDocs ) ) {
				LogError( "Couldn't find pictures directory for SSTV" );
                return false;
			}

            RxModeList.LineAppend( "Auto", fUndoable:false );
            LoadModes( SSTVDEM.EnumModes(), RxModeList, fAddResolution:false );
            LoadModes( SSTVDEM.EnumModes(), TxModeList, fAddResolution:true  );

            // Set this after TxImageList load since the CheckedLine call will 
            // call Listen_ModeChanged and that calls the properties update event.
            RxModeList   .CheckedEvent += OnCheckedEvent_RxModeList; // set checkmark AFTER load the modulators... ^_^;;
            RxModeList   .CheckedLine   = RxModeList[0];

            TxImageList.ImageUpdated += OnImageUpdated_TxImageList;

            return true;
        }

        protected void InitDeviceList() {
			IEnumerator<string> iterOutput = MMHelpers.GetOutputDevices();

			for( int iCount = -1; iterOutput.MoveNext(); ++iCount ) {
                PortTxList.LineAppend( iterOutput.Current, fUndoable:false );
			}

            IEnumerator<string> iterInput = MMHelpers.GetInputDevices();
			for( int iCount = -1; iterInput.MoveNext(); ++iCount ) {
                PortRxList.LineAppend( iterInput.Current, fUndoable:false );
			}
        }

        protected virtual void SettingsInit() {
            InitDeviceList();

            string strMyDocs = Environment.GetFolderPath( Environment.SpecialFolder.MyDocuments );

            // In the future, setting this value will send an event that will get forwarded to the chooser.
            RxProperties.ValueUpdate( RxProperties.Names.SaveHere, strMyDocs );

            RecChooser.LoadURL( strMyDocs );
        }

        protected void PropertiesTxReLoad() {
			string strFileName = Path.GetFileName( TxImageList.CurrentFileName );

            if( TxImageList.Bitmap != null ) {
                TxProperties.ValueUpdate( TxProperties.Names.Width,  TxImageList.Bitmap.Width .ToString() );
                TxProperties.ValueUpdate( TxProperties.Names.Height, TxImageList.Bitmap.Height.ToString() );
            } else {
                TxProperties.ValueUpdate( TxProperties.Names.Width,  "-" );
                TxProperties.ValueUpdate( TxProperties.Names.Height, "-" );
            }

            TxProperties.ValueUpdate( TxProperties.Names.Mode,     TxMode ); 
            TxProperties.ValueUpdate( TxProperties.Names.Progress, "0%" );
            TxProperties.ValueUpdate( TxProperties.Names.FileName, strFileName, Broadcast:true );
		}

		protected void PropertiesRxTime( int iPercent ) {
            RxProperties.ValueUpdate( RxProperties.Names.Progress, iPercent.ToString() + "%", Broadcast:true );
		}
		protected void PropertiesSendTime() {
            TxProperties.ValueUpdate( TxProperties.Names.Progress, PercentTxComplete.ToString() + "%", Broadcast:true );
		}

        public bool Load( TextReader oStream ) {
            return InitNew();
        }

        public bool Save( TextWriter oStream ) {
            return true;
        }

        public int PercentTxComplete {
            get { 
                if( _oSSTVGenerator != null ) 
                    return _oSSTVGenerator.PercentTxComplete;

                return 0;
            } 
        }

        public string TxMode { 
            get {
                if( _oSSTVGenerator != null && _oSSTVGenerator.Mode != null )
                    return _oSSTVGenerator.Mode.Name;

                return "-";
            }
        }

        /// <summary>
        /// BUG: This is a bummer but, I use a point for the aspect ratio in my
        /// SmartRect code. I'll fix that later.
        /// </summary>
        public SKPointI Resolution {
            get {
                try {
                    if( RxModeList.CheckedLine.Extra is SSTVMode oMode )
                        return new SKPointI( oMode.Resolution.Width, oMode.Resolution.Height );
                } catch( NullReferenceException ) {
                    LogError( "Problem finding SSTVMode. Using default." );
                }
                return new SKPointI( 320, 240 );
            }
        }

        /// <summary>
        /// This sets up our transmit buffer and modulator to send the given image.
        /// </summary>
        /// <param name="iModeIndex">Index of the generator mode to use.</param>
        /// <param name="oTxImage">Image to display. It should match the generator mode requirements.</param>
        /// <returns></returns>
        public bool GeneratorSetup( SSTVMode oMode, SKBitmap oTxImage ) {
            if( oMode == null || oTxImage == null ) {
                LogError( "Mode or Image is null on Generator Setup." );
                return false;
            }
            if( _oSSTVModulator == null ) {
                LogError( "SSTV Modulator is not ready for Transmit." );
                return false;
            }
            if( _oWorkPlace.Status == WorkerStatus.BUSY ||
                _oWorkPlace.Status == WorkerStatus.PAUSED ) {
                LogError( "Stop playing current image. Then begin the next." );
                return false;
            }

            _oSSTVGenerator = null;

            // Normally I'd use a guid to identify the class, but I thought I'd
            // try something a little different this time.
            try {
				switch( oMode.Family ) {
					case TVFamily.PD:
						_oSSTVGenerator = new GeneratePD     ( oTxImage, _oSSTVModulator, oMode ); break;
					case TVFamily.Martin:
						_oSSTVGenerator = new GenerateMartin ( oTxImage, _oSSTVModulator, oMode ); break;
					case TVFamily.Scottie:
						_oSSTVGenerator = new GenerateScottie( oTxImage, _oSSTVModulator, oMode ); break;
					default:
						return false;
				}

                if( _oSSTVGenerator != null )
                    _oSSTVBuffer.Pump = _oSSTVGenerator.GetEnumerator();
            } catch( Exception oEx ) {
                Type[] rgErrors = { typeof( ArgumentException ),
                                    typeof( ArgumentNullException ),
                                    typeof( ArgumentOutOfRangeException ),
                                    typeof( NullReferenceException ) };
                if( rgErrors.IsUnhandled( oEx ) )
                    throw;

                LogError( "Blew chunks trying to create Illudium Q-36 Video Modulator, Isn't that nice?" );
                _oSSTVGenerator = null;
            }

            Raise_PropertiesUpdated( SSTVEvents.ALL );

            return _oSSTVGenerator != null;
        }

        private void OnImageUpdated_TxImageList() {
            Raise_PropertiesUpdated( SSTVEvents.TXImageChanged );
        }

        /// <summary>
        /// This is a mess. I need to sort out the messaging. Only the
        /// thread based system has been tested recently.
        /// </summary>
        /// <param name="eProp"></param>
        protected void Raise_PropertiesUpdated( SSTVEvents eProp ) {
            switch( eProp ) {
                case SSTVEvents.UploadTime:
                    PropertiesSendTime();
                    break;
                default:
                    PropertiesTxReLoad();
                    break;
            }
            PropertyChange?.Invoke( eProp );
        }

        /// <summary>
        /// This is our work iterator we use to play the audio. It's my standandard player that
        /// queue's up a portion of sounds and then wait's half that time to return and top
        /// off the buffers again.
        /// </summary>
        /// <returns>Amount of time to wait until we want call again, in Milliseconds.</returns>
        public IEnumerator<int> GetTaskTransmiter() {
            do {
                uint uiWait = 60000; // Basically stop wasting time here on error.
                try {
                    uiWait = ( _oPlayer.Play( _oSSTVBuffer ) >> 1 ) + 1;
                } catch( Exception oEx ) {
                    Type[] rgErrors = { typeof( NullReferenceException ),
                                        typeof( ArgumentNullException ),
                                        typeof( MMSystemException ) };
                    if( rgErrors.IsUnhandled( oEx ) )
                        throw;

                    _oWorkPlace.Stop();
                    LogError( "Trouble playing in SSTV" );
                }
                Raise_PropertiesUpdated( SSTVEvents.UploadTime );
                yield return (int)uiWait;
            } while( _oSSTVBuffer.IsReading );

            TxModeList.HighLight = null;
            // Set upload time to "finished" maybe even date/time!
        }

        /// <summary>
        /// Begin transmitting the image.
        /// </summary>
        /// <param name="skSelect">clip region in source bitmap coordinates.</param>
        public void TransmitBegin( SKRectI skSelect ) {
            try {
                if( _oWorkPlace.Status != WorkerStatus.FREE ) {
                    LogError( "Already sending, receiving or paused." );
                    return;
                }
                if( PortTxList.ElementCount <= 0 ) {
                    LogError( "No sound device to send to" );
                    return;
                }
                if( PortTxList.CheckedLine == null )
                    PortTxList.CheckedLine = PortTxList[0];

                if( TxModeList.CheckedLine == null )
                    TxModeList.CheckedLine = TxModeList[0];

                if( TxModeList.CheckedLine.Extra is SSTVMode oMode ) {
			        // The DocSnip object retains ownership of it's generated bitmap and frees it on next load.
                    // Note: I could check if the selection == the whole bitmap == required dimension
                    //       and I could skip the snip stage.
                    Specification oTxSpec = new Specification( 11025, 1, 0, 16 );
			        _oDocSnip.Load( TxBitmap, skSelect, oMode.Resolution );

                    _oSSTVBuffer    = new BufferSSTV( oTxSpec );
					_oSSTVModulator = new SSTVMOD( 0, oTxSpec.Rate, _oSSTVBuffer );

                    if( GeneratorSetup( oMode, _oDocSnip.Bitmap ) ) {
                        if( _oPlayer == null ) {
                            _oPlayer = new WmmPlayer(oTxSpec, PortTxList.CheckedLine.At );
                        } else {
                            if( _oPlayer.DeviceID != PortTxList.CheckedLine.At ) {
                                _oPlayer.Dispose();
                                _oPlayer = new WmmPlayer(oTxSpec, PortTxList.CheckedLine.At ); 
                            }
                        }

                        _oWorkPlace.Queue( GetTaskTransmiter(), 0 );
                        TxModeList.HighLight = TxModeList.CheckedLine;
                    }
                }
                //while ( _oDataTester.ConsumeData() < 350000 ) {
                //}
            } catch( Exception oEx ) {
                Type[] rgErrors = { typeof( NullReferenceException ),
                                    typeof( MMSystemException ) };
                if( rgErrors.IsUnhandled( oEx ) )
                    throw;

                LogError( "Can't launch Transmit task!" );
            }
        }

        protected void DownloadFinished( string strFileName ) {
            PropertyChange?.Invoke( SSTVEvents.DownLoadFinished );
            RxModeList.HighLight = null;
            SaveRxImage( strFileName ); // Race condition possible, when image reused.
        }

        /// <summary>
        /// Poll the receive thread showing progress on the image download.
        /// Haven't set this up for continuous receive. This thread is just
        /// for the file decode right now.
        /// </summary>
        public IEnumerator<int> GetTaskThreadListener( ThreadWorkerBase oWorker ) {
            do {
                while( _rgBGtoUIQueue.TryDequeue( out SSTVEvents eResult ) ) {
                    switch( eResult ) {
                        case SSTVEvents.ThreadAbort:
                            if( _oThread == null ) {
                                LogError( "Unexpected Image Thread Abort." );
                            } else { 
                                LogError( "Image Thread Abort." );
                            }
                           _oThread = null;
                            yield break;
                        case SSTVEvents.RXImageNew:
                            // Previous image is Disposed!!
			                ReceiveImage.Bitmap = oWorker.SSTVDraw._pBitmapRX;
                            // Except this one since same image it is spared.
			                SyncImage   .Bitmap = oWorker.SSTVDraw._pBitmapD12;

                            PropertyChange?.Invoke( SSTVEvents.RXImageNew );
                            break;
                        case SSTVEvents.SSTVMode: 
                            {
                                SSTVMode oWorkerMode = oWorker.NextMode;
                                if( oWorkerMode == null )
                                    break;
                                foreach( Line oLine in RxModeList ) {
                                    if( oLine.Extra is SSTVMode oMode ) {
                                        if( oMode.LegacyMode == oWorkerMode.LegacyMode ) {
                                            RxModeList.HighLight = oLine;
                                            RxProperties.ValueUpdate( RxProperties.Names.Mode,   oMode.Name );
                                            RxProperties.ValueUpdate( RxProperties.Names.Width,  oMode.Resolution.Width .ToString() );
                                            RxProperties.ValueUpdate( RxProperties.Names.Height, oMode.Resolution.Height.ToString(), Broadcast:true );
                                        }
                                    }
                                }
                            } break;
                        case SSTVEvents.DownLoadTime: 
                            // Might be nice to send the % as a number in the message.
                            PropertiesRxTime( oWorker.SSTVDraw.PercentRxComplete );
                            PropertyChange?.Invoke( SSTVEvents.DownLoadTime );
                            break;
                        case SSTVEvents.DownLoadFinished:
                            // NOTE: This might never come along!
                            PropertiesRxTime( oWorker.SSTVDraw.PercentRxComplete );
                            DownloadFinished( oWorker.SuggestedFileName );
                            break;
                        case SSTVEvents.ThreadDiagnosticsException:
                            LogError( "Worker thread Diagnostics Exception" );
                            break;
                        case SSTVEvents.ThreadDrawingException:
                            LogError( "Worker thread Drawing Exception" );
                            break;
                        case SSTVEvents.ThreadWorkerException:
                            LogError( "Worker thread Exception" );
                            break;
                    }
                }
                yield return 250; // wait 1/4 of a second.
            } while( true );
        }

        /// <summary>
        /// This is our TRUE multithreading experiment! Looks like it works
        /// pretty well. The decoder and filters and all live in the bg thread.
        /// The foreground tread only polls the bitmap from time to time.
        /// </summary>
        /// <param name="DetectVIS">Just set the decoder for a particular SSTV mode. This is usefull
        /// if picking up the signal in the middle and you know the type a priori. I should
        /// just pass the mode if it's fixed, else autodetect.</param>
        public void ReceiveFileRead2Begin( string strFileName ) {
            if( string.IsNullOrEmpty( strFileName ) ) {
                LogError( "Invalid filename for SSTV image read" );
                return;
            }
            if( _oThread == null ) {
                SSTVMode oModeFixed = null;

                // Note that this ModeList is the TX mode list. I think I want an RX list.
                if( RxModeList.CheckedLine.Extra is SSTVMode oMode ) {
                    oModeFixed = oMode;
                }

                ThreadWorker oWorker        = new ThreadWorker( _rgBGtoUIQueue, strFileName, oModeFixed );
                ThreadStart  threadDelegate = new ThreadStart ( oWorker.DoWork );

                _oThread = new Thread( threadDelegate );
                _oThread.Start();

                _oWorkPlace.Queue( GetTaskThreadListener( oWorker ), 1 );
            }
        }

        public void RequestModeChange( SSTVMode oMode ) {
            _rgUItoBGQueue.Enqueue( new TVMessage( TVMessage.Message.TryNewMode, oMode ) );
        }

        public void ReceiveLiveStop() {
            RxModeList.HighLight = null;

            _rgUItoBGQueue.Enqueue( new TVMessage( TVMessage.Message.ExitWorkThread ) );
            if( _oWaveOut != null ) {
                _oWaveOut.Stop();
            }
            if( _oWaveIn != null ) {
                _oWaveIn.StopRecording();    
            }
            _oThread = null;
            _oWorkPlace.Stop();
        }

        /// <summary>
        /// This is our 2'nd threaded receive but straight from the device this time.
        /// </summary>
        public void ReceiveLiveBegin() {
            if( _oThread == null ) {
                try {
                    int  iMicrophone = -1; 
                    int  iSpeaker    = -1;

                    if( PortRxList.CheckedLine != null ) {
                        iMicrophone = PortRxList.CheckedLine.At;
                    } else {
                        LogError( "Please select an sound input device" );
                        return;
                    }
                    if( PortTxList.CheckedLine != null ) {
                        iSpeaker = PortTxList.CheckedLine.At;
                    } else {
                        LogError( "Please select an sound output device" );
                        return;
                    }

                    if( _oWaveIn == null ) {
                        _oWaveIn = new WaveIn( WaveCallbackInfo.FunctionCallback() );

                        _oWaveIn.BufferMilliseconds = 50;
                        _oWaveIn.DeviceNumber       = iMicrophone;
                        _oWaveIn.WaveFormat         = new WaveFormat( 8000, 16, 1 );
                        _oWaveIn.DataAvailable += OnDataAvailable_WaveIn;
                    }
                    if( _oWaveOut == null ) {
                        _oWaveOut = new WaveOut();
                        _oWaveOut.DeviceNumber = iSpeaker;
                        _oWaveBuf = new BufferedWaveProvider(_oWaveIn.WaveFormat);
                        _oWaveOut.Init( _oWaveBuf );
                    }
                    _oWaveReader = new BlockCopies( 1, 1, 0, _oWaveIn.WaveFormat.BitsPerSample );

                    _rgUItoBGQueue.Clear();
                    _rgBGtoUIQueue.Clear();
                    _rgDataQueue  .Clear();

                    // Note that this ModeList is the TX mode list. I think I want an RX list.
                    if( RxModeList.CheckedLine.Extra is SSTVMode oMode ) {
                        _rgUItoBGQueue.Enqueue( new TVMessage( TVMessage.Message.TryNewMode, RxModeList.CheckedLine.Extra ) );
                    }

                    ThreadWorker2 oWorker        = new ThreadWorker2( _oWaveIn.WaveFormat, _rgBGtoUIQueue, _rgDataQueue, _rgUItoBGQueue );
                    ThreadStart   threadDelegate = new ThreadStart  ( oWorker.DoWork );

                    _oThread = new Thread( threadDelegate );
                    _oThread.Start(); // Can send out of memory exception!

                    _oWorkPlace.Queue( GetTaskThreadListener( oWorker ), 1 );

                    _oWaveOut.Play();
                    _oWaveIn .StartRecording();
                } catch( Exception oEx ) {
                    Type[] rgErrors = { typeof( NullReferenceException ),
                                        typeof( ArgumentNullException ),
                                        typeof( FormatException ),
                                        typeof( OverflowException ),
                                        typeof( ThreadStateException ) };
                    if( rgErrors.IsUnhandled( oEx ) )
                        throw;

                    LogError( "Couldn't launch recording thread." );
                }
            }
        }

        /// <summary>
        /// Looks like dispose is not being called on us. But we're getting
        /// messages here. Need to look at that.
        /// </summary>
        private void OnDataAvailable_WaveIn( object sender, WaveInEventArgs e ) {
            try {
                // This is the one that can return InvalidOperationException
                _oWaveBuf.AddSamples( e.Buffer, 0, e.BytesRecorded );

                // No use stuffing the data queue if there's no thread to pick it up.
                if( _oThread != null ) {
                    for( IEnumerator<double>oIter = _oWaveReader.EnumAsSigned16Bit( e.Buffer, e.BytesRecorded ); oIter.MoveNext(); ) {
                        _rgDataQueue.Enqueue( oIter.Current );
                    }
                }
            } catch( InvalidOperationException ) {
                // Looks like I can't call wavein.stop() from within the callback.
            }
        }

		private void SaveRxImage( string strFileName ) {
            try {
                SKBitmap skBitmap = ReceiveImage.Bitmap;
			    if( skBitmap == null )
				    return;

                // Figure out path and name of the file.
                string strSaveDir = Path.GetDirectoryName( strFileName );
                if( string.IsNullOrEmpty( strSaveDir ) ) {
			        strSaveDir = Properties[StdProperties.Names.SaveDir];
                }
                // If Dir still null we should go straight to env variable.
                if( string.IsNullOrEmpty( strFileName ) ) {
                    strFileName = DateTime.Now.ToString();
                } else {
                    strFileName = Path.GetFileNameWithoutExtension( strFileName );
                }
                
                // Clean up the file name. Tacky, but better than nothing.
                TextLine oLine     = new( 0, strFileName );
                string   strIllegal = @" []$%&{}<>*?/\!:;+|=~`" + "\"\'";
                for( int i = 0; i< oLine.ElementCount; ++i ) {
                    foreach( char cBadChar in strIllegal ) {
                        if( oLine[i] == cBadChar )
                            oLine.Buffer[i] = '_';
                    }
                }
                strFileName = oLine.ToString();

                // Get image quality.
                if( !int.TryParse( Properties[StdProperties.Names.Quality], out int iQuality ) )
                    iQuality = 80;

                string strFilePath = Path.Combine( strSaveDir, strFileName + ".png" );

                // Can't use JPEG yet, bug in the SKIA to .net interface.
			    using SKData  data   = skBitmap.Encode( SKEncodedImageFormat.Png, iQuality );
                using var     stream = File.OpenWrite( strFilePath );

                data.SaveTo(stream);
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
			ReceiveImage.Bitmap = null;
			SyncImage   .Bitmap = null;

            _oRxSSTV.ModeTransition( tvMode ); // bitmap allocated in here. (may throw exception...)

			ReceiveImage.Bitmap = _oRxSSTV._pBitmapRX;
			SyncImage   .Bitmap = _oRxSSTV._pBitmapD12;

			Raise_PropertiesUpdated( SSTVEvents.RXImageNew );
            foreach( Line oLine in RxModeList ) {
                if( oLine.Extra is SSTVMode oLineMode ) {
                    if( oLineMode.LegacyMode == tvMode.LegacyMode )
                        RxModeList.HighLight = oLine;
                }
            }
        }

		/// <summary>
		/// Forward events coming from SSTVDraw. I really need to sort out
        /// the mess this has become. BUT this is only used by the TEST
        /// code. The new threaded code does not use this.
		/// </summary>
        private void OnTvEvents_RxSSTV( SSTVEvents eProp )
        {
            Raise_PropertiesUpdated( eProp );

            switch( eProp ) {
                case SSTVEvents.DownLoadFinished:
                    RxModeList.HighLight = null;
                    SaveRxImage( string.Empty );
                    break;
            }
        }

		/// <summary> Another initial test run, on the UI THREAD before 
        /// created the worker task.
		/// Note that we don't take the mode in this task since we want it
		/// to be divined from the VIS by the SSTVDEM object.
		/// </summary>
		/// <returns>Time to wait until next call in ms.</returns>
        /// <remarks>Probably going to delete this eventually.</remarks>
        public IEnumerator<int> GetTaskRecordTest2() {
            _oSSTVDeModulator.Send_NextMode += OnNextMode_SSTVDeMod; // BUG: no need to do every time.
            do {
                try {
                    for( int i = 0; i< 500; ++i ) {
                        _oSSTVDeModulator.Do( _oSSTVBuffer.ReadOneSample() );
                    }
					if( _oRxSSTV != null ) {
						_oRxSSTV.Process();
					}
				} catch( Exception oEx ) {
                    Type[] rgErrors = { typeof( NullReferenceException ),
                                        typeof( ArgumentNullException ),
                                        typeof( MMSystemException ),
                                        typeof( InvalidOperationException ),
										typeof( ArithmeticException ),
										typeof( IndexOutOfRangeException ) };
                    if( rgErrors.IsUnhandled( oEx ) )
                        throw;

					if( oEx.GetType() != typeof( IndexOutOfRangeException ) ) {
						LogError( "Trouble recordering in SSTV" );
					}
					// Don't call _oWorkPlace.Stop() b/c we're already in DoWork() which will
					// try calling the _oWorker which will have been set to NULL!!
                    break; // Drop down so we can unplug from our Demodulator.
                }
                yield return 0; // Let's go as fast as possible. >_<;;
            } while( _oSSTVBuffer.IsReading );

			_oSSTVDeModulator.Send_NextMode -= OnNextMode_SSTVDeMod;
        }

        /// <summary>
        /// This is my 2nd NON threaded test where we generate video and encode it to a 
        /// time varying signal to be decoded by the CSSTVDEM object! Because we're
        /// not intercepting the signal before the VCO we can use the normal 
        /// GeneratorSetup call.
        /// </summary>
        /// <param name="iModeIndex">TV Mode to use.</param>
        /// <param name="skSelect"></param>
        /// <remarks>Set CSSTVDEM::m_fFreeRun = true</remarks>
        public void ReceiveTest2Begin( SKRectI skSelect ) {
            try {
                if( RxModeList.CheckedLine == null )
                    RxModeList.CheckedLine = RxModeList[0];

                if( RxModeList.CheckedLine.Extra is SSTVMode oMode ) {
                    if( _oWorkPlace.Status == WorkerStatus.FREE ) {
			            _oDocSnip.Load( TxBitmap, skSelect, oMode.Resolution );
					    if( GeneratorSetup( oMode, _oDocSnip.Bitmap ) ) {
						    FFTControlValues oFFTMode = FFTControlValues.FindMode( RxSpec.Rate ); 
						    SYSSET           sys      = new();
						    SSTVDEM          oDemod   = new SSTVDEM( sys,
																	 oFFTMode.SampFreq, 
																	 oFFTMode.SampBase, 
																	 0 );
						    _oRxSSTV                = new SSTVDraw( oDemod );
						    _oRxSSTV.Send_TvEvents += OnTvEvents_RxSSTV; // Since non-threaded, this is ok.

						    _oSSTVDeModulator = oDemod;

						    _oWorkPlace.Queue( GetTaskRecordTest2(), 0 );
					    }
                    }
                }
            } catch( NullReferenceException ) {
                LogError( "Probably Buggered twice in the ModeList." );
            }
        }

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
		/// <param name="iModeIndex">TV Format to use.</param>
		/// <param name="skSelect">Portion of the image we want to transmit.</param>
		/// <remarks>Use a low sample rate so it's easier to slog thru the data. 
		///          Set CSSTVDEM::m_fFreeRun to false!!</remarks>
		/// <seealso cref="InitNew" />
        public void ReceiveTest1Begin( SKRectI skSelect ) {
            try {
                if( RxModeList.CheckedLine.Extra is SSTVMode oMode ) {
                    if( _oWorkPlace.Status == WorkerStatus.FREE ) {
			            _oDocSnip.Load( TxBitmap, skSelect, oMode.Resolution );

					    FFTControlValues oFFTMode  = FFTControlValues.FindMode( 8000 ); // RxSpec.Rate
					    SYSSET           sys       = new ();
					    DemodTest        oDemodTst = new DemodTest( sys,
															        oFFTMode.SampFreq, 
															        oFFTMode.SampBase, 
															        0 );
					    _oSSTVDeModulator = oDemodTst;
					    _oSSTVModulator   = new SSTVMOD( 0, oFFTMode.SampFreq, _oSSTVBuffer );
					    _oRxSSTV          = new SSTVDraw ( _oSSTVDeModulator );

					    _oSSTVGenerator = oMode.Family switch {
						    TVFamily.PD      => new GeneratePD     ( _oDocSnip.Bitmap, oDemodTst, oMode ),
						    TVFamily.Martin  => new GenerateMartin ( _oDocSnip.Bitmap, oDemodTst, oMode ),
						    TVFamily.Scottie => new GenerateScottie( _oDocSnip.Bitmap, oDemodTst, oMode ),

						    _ => throw new ArgumentOutOfRangeException("Unrecognized Mode Type."),
					    };

                        _oSSTVDeModulator.Send_NextMode += OnNextMode_SSTVDeMod;

                        _oWorkPlace.Queue( GetTaskRecordTest1( oMode ), 0 );
                    }
                }
            } catch( NullReferenceException ) {
                LogError( "Ooops didn't pick up mode (I think)" );
            }
        }

		public WorkerStatus PlayStatus => _oWorkPlace.Status;

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
