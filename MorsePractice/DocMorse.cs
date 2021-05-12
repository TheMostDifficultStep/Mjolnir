using System;
using System.IO;
using System.Reflection;
using System.Linq;
using System.Text;
using System.Net;
using System.Net.Http;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.IO.Ports;
using System.Collections.Concurrent;

//using System.Data.SqlClient;
//Microsoft.Data.SqlClient the new api for ms db's
//https://mariadb.com/kb/en/mariadb-and-net/ // Question and answer.
//https://docs.microsoft.com/en-us/azure/mysql/connect-csharp // good tutorial for starting up.
//https://mysqlconnector.net/ mit project for ado.net connectivity.

using Play.Interfaces.Embedding; 
using Play.Rectangles;
using Play.Edit;
using Play.Integration;
using Play.Parse.Impl;
using Play.Forms;

//using MySql.Data.MySqlClient;

namespace Play.MorsePractice {
    public class CallsDoc : Editor {
        public CallsDoc( IPgBaseSite oSite ) : base( oSite ) { }

		public override WorkerStatus PlayStatus {
			get { return( WorkerStatus.BUSY ); }
		}
    }

    /// <summary>
    /// This class is not dedicated to only the morse code operations.
    /// Factored out all the logging helpers from this class.
    /// </summary>
    /// <seealso cref="DocNotes"/>
	public class MorseDoc:
		IPgParent,
		IPgLoad<TextReader>,
		IPgSave<TextWriter>,
        IDisposable
	{
        readonly IPgScheduler      _oScheduler;

        protected class MorseDocSlot :
			IPgBaseSite,
            IPgFileSite
		{
			protected readonly MorseDoc _oHost;
            protected readonly string   _strName;

			public MorseDocSlot( MorseDoc oHost, string strName ) {
				_oHost   = oHost   ?? throw new ArgumentNullException();
                _strName = strName ?? throw new ArgumentNullException();
			}

			public IPgParent Host => _oHost;

            public void LogError(string strMessage, string strDetails, bool fShow=true) {
				_oHost.LogError( strMessage, strDetails, fShow );
			}

			public void Notify( ShellNotify eEvent ) {
			}

            FILESTATS IPgFileSite.FileStatus   => _oHost._oSiteFile.FileStatus;
            Encoding  IPgFileSite.FileEncoding => _oHost._oSiteFile.FileEncoding;
            string    IPgFileSite.FilePath     => _oHost._oSiteFile.FilePath;
            string    IPgFileSite.FileBase     => _oHost._oSiteFile.FileBase + " / " + _strName;
		}

		readonly string      _strMorseTable = @"Play.MorsePractice.Content.international-morse-code.txt";
		readonly IPgBaseSite _oSiteBase;
        readonly IPgFileSite _oSiteFile;

        // Stuff for the morse code pracice view.
		public Editor   Source { get; } // practice code tones generated from this editor.
		public Editor   Notes  { get; } // practice screen notes. and primary view.
		public Editor   Stats  { get; } // put stats or more audio reference here.
		public Editor   Morse  { get; } // Our reference to the morse values. Let's make this audio!!

		protected static readonly HttpClient _oHttpClient = new HttpClient(); 

        /// <summary>
        /// Document object for a little Morse Practice document.
        /// </summary>
        public MorseDoc( IPgBaseSite oSiteBase ) {
			_oSiteBase  = oSiteBase ?? throw new ArgumentNullException();
            _oSiteFile  = oSiteBase as IPgFileSite ?? throw new ArgumentException( "Host needs the IPgFileSite interface" );
            _oScheduler = Services as IPgScheduler ?? throw new ArgumentException("Host requries IPgScheduler");

            Source           = new Editor  ( new MorseDocSlot( this, "Source" ) ); // Morse code source for practice.
			Notes            = new Editor  ( new MorseDocSlot( this, "Notes"  ) ); // Notes for listening to morse, or log files.
			Stats            = new Editor  ( new MorseDocSlot( this, "Stats"  ) );
			Morse            = new Editor  ( new MorseDocSlot( this, "Ref"    ) ); // Refrence table of morse code letters.

            new ParseHandlerText   ( Notes,       "text" );
        }

        private bool _fDisposed = false;

		public void Dispose() {
			if( !_fDisposed ) {
				Source.Dispose();
				Notes .Dispose();
				Stats .Dispose();
                Morse .Dispose();

                _fDisposed = true;
			}
		}

		protected void LogError( string strMessage, string strDetails, bool fShow = false ) {
			_oSiteBase.LogError( strMessage, strDetails, fShow );
		}

		public bool      IsDirty   => Notes.IsDirty; // || Sources.IsDirty;
		public IPgParent Parentage => _oSiteBase.Host;
		public IPgParent Services  => Parentage.Services;

        /// <summary>
        /// Load Morse code reference text.
        /// </summary>
        /// <remarks>Not likely to fail once get working. So no return value.</remarks>
        protected void LoadMorse() { 
            Assembly oAssembly = Assembly.GetExecutingAssembly();

			try {
				using( Stream oStream = oAssembly.GetManifestResourceStream( _strMorseTable )) {
					using( TextReader oText = new StreamReader( oStream, true ) ) {
						Morse.Load( oText );
					}
				}
			} catch( Exception oEx ) {
				Type[] rgErrors = { typeof( NullReferenceException ), 
									typeof( ArgumentNullException ),
									typeof( ArgumentException ),
									typeof( FileLoadException ),
									typeof( BadImageFormatException ),
									typeof( NotImplementedException ) };
				if( rgErrors.IsUnhandled( oEx ) )
					throw;
			}
		}

        /// <summary>
        /// Both InitNew and Load call this base initialization function.
        /// </summary>
        protected bool Initialize() {
			if( !Source.InitNew() )
				return false;
			if( !Notes.InitNew() )
				return false;

			if( !Stats.InitNew() )
				return false;

            LoadMorse();
         // LoadStats(); We'll load the stats table here, it'll double as an audio reference.

			return true;
		}

        public bool InitNew() {
			if( !Initialize() ) 
				return false;

			return true;
		}

		public bool Load(TextReader oStream) {
			if( !Initialize() ) 
				return false;
			if( !Notes.Load( oStream ) )
				return false;

			return true;
		}

		public bool Save(TextWriter oStream) {
            Notes.Save( oStream );

            return true;
		}
    }

    /// <summary>
    /// this will be our new call logger and ic-705 communicator.
    /// </summary>
	public class DocNotes:
		IPgParent,
		IPgLoad<TextReader>,
		IPgSave<TextWriter>,
        IPgCiVEvents,
        IDisposable
	{
        readonly IPgScheduler      _oScheduler;
        readonly IPgRoundRobinWork _oTaskQrz;
        readonly IPgRoundRobinWork _oTaskCiv;
        readonly IPgRoundRobinWork _oTaskTimer;

        protected class MorseDocSlot :
			IPgBaseSite,
            IPgFileSite
		{
			protected readonly DocNotes _oHost;
            protected readonly string   _strName;

			public MorseDocSlot( DocNotes oHost, string strName ) {
				_oHost   = oHost   ?? throw new ArgumentNullException();
                _strName = strName ?? throw new ArgumentNullException();
			}

			public IPgParent Host => _oHost;

            public void LogError(string strMessage, string strDetails, bool fShow=true) {
				_oHost.LogError( strMessage, strDetails, fShow );
			}

			public void Notify( ShellNotify eEvent ) {
			}

            FILESTATS IPgFileSite.FileStatus   => _oHost._oSiteFile.FileStatus;
            Encoding  IPgFileSite.FileEncoding => _oHost._oSiteFile.FileEncoding;
            string    IPgFileSite.FilePath     => _oHost._oSiteFile.FilePath;
            string    IPgFileSite.FileBase     => _oHost._oSiteFile.FileBase + " / " + _strName;
		}

		readonly IPgBaseSite _oSiteBase;
        readonly IPgFileSite _oSiteFile;

        // Stuff for the morse code pracice view.
		public Editor        Notes      { get; } // practice screen notes. and primary view.
        public CallsDoc      Calls      { get; } // List of callsigns in left hand column of notes file.
        public DocProperties Properties { get; }

        public Editor CallSign        { get; }
        public Editor CallSignBio     { get; } // Text generated by walking the html stream.
        public Editor CallSignAddress { get; } // This is the mailing address displayed.
        public Editor CallSignBioHtml { get; } // base 64 converted HTML streaml;
        public Editor CallSignPageHtml{ get; } // This is the main page returned by qrz.

                 SerialPort              _oCiV; // Good cantidate for "init once"
        readonly ConcurrentQueue<byte[]> _oMsgQueue = new ConcurrentQueue<byte[]>();
        readonly Line                    _oDataGram = new TextLine( 0, string.Empty );
        readonly Grammer<char>           _oCiVGrammar;
        readonly DatagramParser          _oParse;

        public bool FlagScanCalls { get; set; } = true;  // Set up call back to buffer to parse calls.
        public bool FlagSComsOn   { get; set; } = false; // Turn on the com ports.

        Dictionary <int, RepeaterDir> _rgRepeaters = new Dictionary<int, RepeaterDir>();

		protected static readonly HttpClient _oHttpClient = new HttpClient(); 

        protected static Type[] CiVErrorList {
            get {
                Type[] rgErrors = { typeof( UnauthorizedAccessException ),
                                    typeof( IOException ),
                                    typeof( ArgumentOutOfRangeException ),
                                    typeof( ArgumentException ),
                                    typeof( InvalidOperationException ),
                                    typeof( NullReferenceException ),
                                    typeof( FileNotFoundException ) };
                return rgErrors;
            }
        }

        /// <summary>
        /// Document object for a little Morse Practice document.
        /// </summary>
        public DocNotes( IPgBaseSite oSiteBase ) {
			_oSiteBase  = oSiteBase ?? throw new ArgumentNullException();
            _oSiteFile  = oSiteBase as IPgFileSite ?? throw new ArgumentException( "Host needs the IPgFileSite interface" );
            _oScheduler = Services as IPgScheduler ?? throw new ArgumentException("Host requries IPgScheduler");
            _oTaskQrz   = _oScheduler.CreateWorkPlace() ?? throw new ApplicationException("No worksite for file downloader.");
            _oTaskCiv   = _oScheduler.CreateWorkPlace() ?? throw new ApplicationException("No worksite for Civ." );
            _oTaskTimer = _oScheduler.CreateWorkPlace() ?? throw new ApplicationException("No worksite for Keydown timer." );

			try {
				_oCiVGrammar = (Grammer<char>)((IPgGrammers)Services).GetGrammer( "civ" );
                _oParse      = new DatagramParser( new CharStream( _oDataGram ), _oCiVGrammar );
                _oParse.ListerAdd( this );
			} catch ( Exception oEx ) {
                Type[] rgErrors = { typeof( NullReferenceException ),
                                    typeof( InvalidCastException ),
                                    typeof( ArgumentNullException ),
									typeof( FileNotFoundException ),
									typeof( GrammerNotFoundException ) };
                if( !rgErrors.Contains( oEx.GetType() ) )
                    throw;

				LogError( "Morse", "Couldn't get grammar for Ci-V parser. But able to run elsewise." );
			}

			Notes            = new Editor       ( new MorseDocSlot( this, "Notes"  ) ); // Notes for listening to morse, or log files.
            Calls            = new CallsDoc     ( new MorseDocSlot( this, "Calls"  ) ); // document for outline, compiled list of stations
            Properties       = new DocProperties( new MorseDocSlot( this, "Properties" ) );

            CallSign         = new Editor  ( new MorseDocSlot( this, "CallSign" ) );
            CallSignPageHtml = new Editor  ( new MorseDocSlot( this, "PageSrc"  ) );
            CallSignBioHtml  = new Editor  ( new MorseDocSlot( this, "PageBioSrc"  ) );
            CallSignBio      = new Editor  ( new MorseDocSlot( this, "PageBio" ) );
            CallSignAddress  = new Editor  ( new MorseDocSlot( this, "CallAddr" ) );

            new ParseBioHTMLSkimmer( this );
            new ParseQrzHTMLSkimmer( this );
            new ParseHandlerText   ( Notes,       "text" );
            new ParseHandlerText   ( Calls,       "text" );
            new ParseHandlerText   ( CallSign,    "text" );
            new ParseHandlerText   ( CallSignBio, "text" );
        }

        private bool _fDisposed = false;

		public void Dispose() {
			if( !_fDisposed ) {
                _oCiV ?.Close();
                _oParse?.Dispose();

				Notes .Dispose();
                Calls .Dispose();

                CallSign        .Dispose();
                CallSignPageHtml.Dispose();
                CallSignBio     .Dispose();
                CallSignBioHtml .Dispose();
                CallSignAddress .Dispose();

                _fDisposed = true;
			}
		}

		protected void LogError( string strMessage, string strDetails, bool fShow = false ) {
			_oSiteBase.LogError( strMessage, strDetails, fShow );
		}

		public bool      IsDirty   => Notes.IsDirty; // || Sources.IsDirty;
		public IPgParent Parentage => _oSiteBase.Host;
		public IPgParent Services  => Parentage.Services;

        //public ICollection<ICollection<Line>> Rows { get; } = new List<ICollection<Line>>();

        public void CiVError( string strError ) {
            LogError( "Ci-V", strError );
        }

        public struct RepeaterDir {
            public RepeaterDir( int iFreq, int iOffs, int iTime, string strInfo = "", string strDesc = "" ) {
                _iFrequency = iFreq;
                _iOffset    = iOffs;

                Timeout     = iTime;
                Info        = new TextLine( 0, strInfo );
                Description = new TextLine( 0, strDesc );
            }

            private readonly int  _iFrequency;
            private readonly int  _iOffset;

            public int  Timeout     { get; }
            public Line Info        { get; }
            public Line Description { get; }

            public int  Output => _iFrequency;
            public int  Input  => _iFrequency + _iOffset;
        }

        /// <summary>
        /// It is interesting that we only get notice of transmission by the change
        /// of frequency from listening to TX freq. Only for (duplex) repeaters which is
        /// the only time we probably care about timing out the repeater. Convenient!
        /// </summary>
        /// <param name="iFrequency"></param>
        public void CiVFrequencyChange( int iFrequency ) {
            if( _rgRepeaters.TryGetValue( iFrequency, out RepeaterDir oRepeater ) ) {
                Properties.UpdateValue( 0, "Timer triggered..." );
                _oTaskTimer.Queue( ListenForTimout( oRepeater.Timeout ), 0 );
            } else {
                Properties.UpdateValue( 0, "Stopped." );
                _oTaskTimer.Stop(); // stopped talking most likely.
            }
        }

        public void CiVModeChange( string strMode, string strFilter ) {
        }

        /// <summary>
        /// Take the time and subtract 10 seconds because not so precise!! ^_^;;
        /// </summary>
        /// <param name="iTime">Time in seconds.</param>
        /// <returns></returns>
        IEnumerator<int> ListenForTimout( int iTime ) {
            DateTime oStart = DateTime.Now.AddSeconds( iTime - 10 );
            TimeSpan oSpan;

            do {
                oSpan = oStart.Subtract( DateTime.Now );
                if( oSpan.TotalSeconds < 0 )
                    break;

                Properties.UpdateValue( 0, "Ticking..." + Math.Round( oSpan.TotalSeconds ).ToString( "N0" ) );

                yield return ( oSpan.TotalSeconds > 10 ) ? 10000 : 1000;
            } while( true );

            Properties.UpdateValue( 0,  "TIMEOUT!" );
        }

        /// <summary>
        /// This is a task we use to monitor the serial port message queue.
        /// While I could make a parser that parses over 'byte' for ease of debugging
        /// I convert the byte stream to a human readable 'char' stream. Also I can stick
        /// the stream into my editor and see colorization, so it's a win / win.
        /// </summary>
        IEnumerator<int> ListenToCom() {
            while( true ) {
                if( _oMsgQueue.TryDequeue( out byte[] rgMsg ) ) {
                    SerialToDatagram( rgMsg );
                }
                yield return 1000; // Return after 1000 ms.
            }
        }

        /// <summary>
        /// this procedure displays the data as one datagram per line. I've
        /// written the ci-v grammar to parse this stream. You can use one
        /// of the other SerialTo... functions to debug things.
        /// </summary>
        /// <seealso cref="SerialToList"/>
        /// <seealso cref="SerialToOffset"/>
        protected void SerialToDatagram( byte[] rgMsg ) {
            _oDataGram.Empty();

            foreach( byte bByte in rgMsg ) {
                string strByte = bByte.ToString( "X2" );

                _oDataGram.TryAppend( strByte + " " );
            }

            _oParse?.Parse();
        }

        /// <summary>
        /// this procedure displays the data as one datagram per line in the
        /// Notes editor. Not currently in use.
        /// </summary>
        protected void SerialToList( byte[] rgMsg ) {
            StringBuilder rgBuilder = new StringBuilder();

            foreach( byte bByte in rgMsg ) {
                string strByte = bByte.ToString( "X2" );

                rgBuilder.Append( strByte );

                if( bByte == 0xfd ) {
                    Notes.LineAppend( rgBuilder.ToString() );
                    rgBuilder.Clear();
                } else {
                    rgBuilder.Append( " " );
                }
            }

            Notes.LineAppend( rgBuilder.ToString() );

            rgBuilder.Clear();
        }

        /// <summary>
        /// This procedure identifies each byte by it's offset and shows each
        /// on a single line in the Notes editor. Not currently in use.
        /// </summary>
        protected void SerialToOffset( byte[] rgMsg ) {
            int           iIndex    = 0;
            StringBuilder rgBuilder = new StringBuilder();

            foreach( byte bByte in rgMsg ) {
                rgBuilder.Append( "offset(" );
                rgBuilder.Append( iIndex.ToString( "D2" ) );
                rgBuilder.Append( ") :" );
                rgBuilder.Append( bByte.ToString( "X2" ));

                Notes.LineAppend( rgBuilder.ToString() );
                rgBuilder.Clear();

                if( bByte == 0xfd ) {
                    iIndex = 0;
                    Notes.LineAppend( string.Empty );
                } else {
                    iIndex++;
                }
            }
        }

        /// <summary>
        /// set up the COM port i/o.
        /// </summary>
        /// <remarks>
        /// Turns out we can always set ourselves up to listen to a COM port, BUT
        /// we might not be able to open it, or we don't want to open it right away.
        /// </remarks>
        protected void InitSerial() {
            // This is crude since we're polling even if no messages.
            // we'll fix this up later.
            _oTaskCiv.Queue( ListenToCom(), 100 );

            try {
                _oCiV = new SerialPort("COM4");
                _oCiV.BaudRate = 9600;
                _oCiV.Parity = Parity.None;
                _oCiV.StopBits = StopBits.One;
                _oCiV.DataBits = 8;
                _oCiV.Handshake = Handshake.None;

                _oCiV.DataReceived += CiV_DataReceived;
            } catch( IOException ) {
                LogError( "Morse COM access", "Unable to open COM port. Timer disabled" );
                Properties.UpdateValue( 1, "Failed Alloc" );
                return;
            }

            try {
                if( FlagSComsOn ) {
                    _oCiV.Open();
                    Properties.UpdateValue( 1, "On" );
                } else {
                    Properties.UpdateValue( 1, "Off" );
                }
            } catch( Exception oEx ) {
                if( CiVErrorList.IsUnhandled( oEx ) )
                    throw;

                _oCiV.Dispose();

                LogError( "Morse COM access", "Unable to open COM port. Timer disabled" );
                Properties.UpdateValue( 1, "Failed Open" );
            }
        }

        protected void InitRepeaters() {
            List< RepeaterDir > rgTemp = new List<RepeaterDir>();

            rgTemp.Add( new RepeaterDir( 146960000,  -600000, 180 ));
            rgTemp.Add( new RepeaterDir(  53170000, -1700000, 120 ));
            rgTemp.Add( new RepeaterDir( 146820000,  -600000, 120 ));
            rgTemp.Add( new RepeaterDir(  52870000, -1700000, 180 ));

            foreach( RepeaterDir oItem in rgTemp ) {
                _rgRepeaters.Add( oItem.Input, oItem );
            }
        }

        /// <summary>
        /// Properties are initialized first... BUT the values and where they
        /// come from might not be initialized yet. So those will have to come
        /// come later.
        /// </summary>
        protected void InitProperties() {
            Properties.AddLabel( "Timer" );
            Properties.AddLabel( "Timer Enable" );
        }

        /// <summary>
        /// This event comes in asynchronously on it's own thread. Queue up the message
        /// so that the foreground tread can pick it up.
        /// </summary>
        /// <remarks>Would be nice if we could post a message to the forground so that
        /// it spins up a task that can terminate once all the data has been pulled.</remarks>
        private void CiV_DataReceived( object sender, SerialDataReceivedEventArgs e ) {
            int iBytesWaiting = _oCiV.BytesToRead;
            if( iBytesWaiting > 0 ) {
                byte[] rgMsg = new byte[iBytesWaiting];

                _oCiV?.Read( rgMsg, 0, iBytesWaiting );
                _oMsgQueue.Enqueue( rgMsg );
            }
        }

        /// <summary>
        /// Both InitNew and Load call this base initialization function.
        /// </summary>
        protected bool Initialize() {
			if( !Notes.InitNew() )
				return false;
            if( !Calls.InitNew() )
                return false;
            if( !Properties.InitNew() )
                return false;

            // Note: Change this to trigger after a notes parse.
            //_oTaskSched.Queue( EnumCallsScanTask(), 3000 );
            if( FlagScanCalls ) {
                Notes.BufferEvent += Notes_BufferEvent;
            }

            if( !CallSign.InitNew())
                return false;
            if( !CallSignPageHtml.InitNew() )
                return false;
            if( !CallSignBioHtml.InitNew())
                return false;
            if( !CallSignBio.InitNew() )
                return false;
            if( !CallSignAddress.InitNew() )
                return false;

            InitProperties();
            InitSerial    ();
            InitRepeaters ();

            //for( int i=0; i<3; ++i ) {
            //    List<Line> rgRow = new List<Line>(7);

            //    for( int j=0; j<7; ++j ) {
            //        rgRow.Add( new TextLine( j, j.ToString() ) );
            //    }
            //    Rows.Add( rgRow );
            //}

			return true;
		}

        private void Notes_BufferEvent( BUFFEREVENTS eEvent ) {
            if( eEvent == BUFFEREVENTS.FORMATTED ) {
                ScanCallsigns();
            }
        }

        public bool InitNew() {
			if( !Initialize() ) 
				return false;

			return true;
		}

		public bool Load(TextReader oStream) {
			if( !Initialize() ) 
				return false;
			if( !Notes.Load( oStream ) )
				return false;

			return true;
		}

		public bool Save(TextWriter oStream) {
            Notes.Save( oStream );

            return true;
		}

        /// <summary>
        /// Scan the entire file for callsigns and pop them into the "Calls" editor.
        /// </summary>
        public void ScanCallsigns() {
            Calls.Clear();
            List< string > rgCallSigns = new List<string>();

            try {
                foreach( Line oLine in Notes ) {
                    // Note: When Formatting goes type IPgWordRange I can remove the filter.
                    foreach( IColorRange oColor in oLine.Formatting ) {
                        if( oColor is IPgWordRange oWord &&
                            string.Compare( oWord.StateName, "callsign" ) == 0 && oWord.Offset == 0 ) 
                        {
                            rgCallSigns.Add( oLine.SubString( oWord.Offset, oWord.Length ) );
                        }
                    }
                }
                IEnumerable<IGrouping<string, string>> dupes = rgCallSigns.GroupBy(x => x.ToLower() ).OrderBy( y => y.Key.ToLower() );

                foreach( IGrouping<string, string> foo in dupes ) {
                    Calls.LineAppend( foo.Key + " : " + foo.Count().ToString() );
                }
                Calls.LineInsert( "Operator Count : " + dupes.Count().ToString() );
            } catch( Exception oEx ) {
                Type[] rgErrors = { typeof( NullReferenceException ),
                                    typeof( ArgumentNullException ),
                                    typeof( InvalidCastException ) };
                if( rgErrors.IsUnhandled( oEx ) )
                    throw;
                LogError( "Serious Error", "ScanCallsigns suffered an error." );
            }
        }

        public IEnumerator<int> EnumCallsScanTask() {
            int iTimeInMs = 1000 * 60;
            while( true ) {
                ScanCallsigns();
                yield return iTimeInMs;
            }
        }

        public static void test() {
            //var builder = new MySqlConnectionStringBuilder
            //{
            //    Server = "YOUR-SERVER.mysql.database.azure.com",
            //    Database = "YOUR-DATABASE",
            //    UserID = "USER@YOUR-SERVER",
            //    Password = "PASSWORD",
            //    SslMode = MySqlSslMode.Required,
            //};

            //using (var conn = new MySqlConnection(builder.ConnectionString))
            //{
            //    Console.WriteLine("Opening connection");
            //    await conn.OpenAsync();

            //    using (var command = conn.CreateCommand())
            //    {
            //        command.CommandText = "SELECT * FROM inventory;";

            //        using (var reader = await command.ExecuteReaderAsync())
            //        {
            //            while (await reader.ReadAsync())
            //            {
            //                Console.WriteLine(string.Format(
            //                    "Reading from table=({0}, {1}, {2})",
            //                    reader.GetInt32(0),
            //                    reader.GetString(1),
            //                    reader.GetInt32(2)));
            //            }
            //        }
            //    }

            //    Console.WriteLine("Closing connection");
            //}

            //Console.WriteLine("Press RETURN to exit");
            //Console.ReadLine();
        }

        static readonly HttpClient client = new HttpClient();

        /// <summary>
        /// This is all obsolete and should use the async functions.
        /// </summary>
        public IEnumerator<int> EnumWatchTask(Task<HttpResponseMessage> oTask)
        {
            Task<Stream> oTask3 = null;

            while (oTask != null) {
                switch (oTask.Status) {
                    case TaskStatus.RanToCompletion:
                        oTask3 = oTask.Result.Content.ReadAsStreamAsync();
                        oTask.Dispose();
                        oTask = null;
                        break;
                    case TaskStatus.Faulted:
                    case TaskStatus.Canceled:
                        CallSignBioHtml.Clear();
                        CallSignBioHtml.LineAppend("Task Faulted: " + oTask.Exception.ToString());
                        oTask.Dispose();
                        yield break;
                    default:
                        yield return 250;
                        break;
                }
            }

            Stream oStream = null;

            while (oTask3 != null) {
                switch (oTask3.Status) {
                    case TaskStatus.RanToCompletion:
                        oStream = oTask3.Result;
                        oTask3.Dispose();
                        oTask3 = null;
                        break;
                    case TaskStatus.Faulted:
                    case TaskStatus.Canceled:
                        oTask3.Dispose();
                        yield break;
                    default:
                        yield return 250;
                        break;
                }
            }

            bool fLoaded = false;

            try { 
                using (StreamReader oReadQrz = new StreamReader(oStream, Encoding.UTF8, true, 1024, true )) {
                    // Just look for the first base64.decode and that should be it.
                    // Kind of gross to be burning strings. But it'll do in a pinch.
                    // Note: Looks like we assume the base64 blob is all on one line!
                    while (oReadQrz.Peek() >= 0) {
                        string strLine = oReadQrz.ReadLine();
                        string strSrch = "Base64.decode(\"";
                        int    iStart  = strLine.LastIndexOf(strSrch);
                        int    iEnd    = strLine.LastIndexOf("\"");

                        if (iStart > 0) {
                            byte[] rgBiography = null;
                            iStart += strSrch.Length;
                            if (iEnd > iStart + 20) { // Just an arbitrary extra.
                                try {
                                    rgBiography = Convert.FromBase64String(strLine.Substring(iStart, iEnd - iStart));
                                    MemoryStream stmBio = new MemoryStream(rgBiography);
                                    using (StreamReader oFoo = new StreamReader(stmBio)) {
                                        fLoaded = CallSignBioHtml.Load(oFoo);
                                    }
                                } catch (Exception oEx) {
                                    Type[] rgErrors = { typeof( ArgumentNullException ),
                                                        typeof( FormatException ) };
                                    if( rgErrors.IsUnhandled( oEx ))
                                        throw;
                                    // FromBase64String actually throws more exception types than documented. 
                                }
                            }
                            break;
                        }
                    }
                }
                // On this second pass we load the stream into the page html editor. We could 
                // probably do this first, THEN pull the base64 blob if we deal with the parse better.
                // The first scan is mostly hacky. ^_^;;
                oStream.Seek( 0, SeekOrigin.Begin );
                using (StreamReader oReadQrz = new StreamReader(oStream)) {
                    CallSignPageHtml.Load( oReadQrz );
                }
            } catch( Exception oEx ) {
                Type[] rgErrors = { typeof( ArgumentException ),
                                    typeof( NullReferenceException ),
                                    typeof( ArgumentNullException ),
                                    typeof( ObjectDisposedException ) };
                if( rgErrors.IsUnhandled( oEx ))
                    throw;

                oStream.Close();
            }

            if (!fLoaded) {
                PageReset();

                CallSignBio.Load("No Biography");
            }
        }

        protected void PageReset() {
            CallSignPageHtml.Clear();
            CallSignBioHtml .Clear();
            CallSignBio     .Clear();
            CallSignAddress .Clear();
        }

        /// <summary>
        /// New code to read the qrz station page. Not finished yet.
        /// </summary>
        public bool StationRead( Stream oStream ) {
            try { 
                Editor oTempEdit = new Editor( null );

                using( TextReader oReader = new StreamReader( oStream )) { 
                    if( !oTempEdit.Load( oReader ) ) { 
                        _oSiteBase.LogError( "Editor Load", "Could not read stream.", true );
                        return false;
                    }
                };
            } catch( Exception oEx ) { 
                Type[] rgErrors = { typeof( NullReferenceException),
                                    typeof( ArgumentException ),
                                    typeof( ArgumentNullException ) };
                if( rgErrors.IsUnhandled( oEx ))
                    throw;
            }

            return true;
        }

        public void StationLoad( string strCallsign ) { 
            CallSign.Clear();
            CallSign.Load( strCallsign );

            StationLoad();
        }

        public void StationLoad() {
            string strURL = "https://www.qrz.com/lookup";
            string strMediaType = "application/x-www-form-urlencoded";

            ServicePointManager.SecurityProtocol =
                SecurityProtocolType.Tls | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls12;

            StringBuilder oQuery = new StringBuilder();

            oQuery.Append("tquery=");
            oQuery.Append(CallSign[0]);
            oQuery.Append("&mode=callsign");

            if (_oTaskQrz.Status == WorkerStatus.FREE) {
                try {
                    StringContent oContent = new StringContent(oQuery.ToString(), System.Text.Encoding.UTF8, strMediaType);
                    Task<HttpResponseMessage> oTask = client.PostAsync(strURL, oContent);
                    _oTaskQrz.Queue(EnumWatchTask(oTask), 100);
                } catch (HttpRequestException) {
                    PageReset();

                    CallSignBio.LineAppend("Unable to process request");
                    return;
                }
            }
        }

        /// <summary>
        /// A way to sink SQL events. Unfinished.
        /// </summary>
        /// <param name="oEvent"></param>
        public void TableListenerAdd(IPgTableEvents oEvent) {
        }

        public void TableListenerRemove(IPgTableEvents oEvent) {
        }

        public bool Execute( Guid guidCmnd ) {
            if( guidCmnd == GlobalCommands.Play ) {
                try {
                    if( !_oCiV.IsOpen ) {
                        _oCiV.Open();
                        Properties.UpdateValue( 1, "On" );
                    } else {
                        Properties.UpdateValue( 1, "Already Opened" );
                    }
                } catch( Exception oEx ) {
                    if( CiVErrorList.IsUnhandled( oEx ) )
                        throw;

                    Properties.UpdateValue( 1, "Open Error" );
                    LogError( "Morse Com port", oEx.Message, fShow:false );
                }
                return true; // Handled, even if ended in error.
            }
            if( guidCmnd == GlobalCommands.Stop ) {
                try {
                    if( _oCiV.IsOpen ) {
                        _oCiV.Close();
                        Properties.UpdateValue( 1, "Off" );
                    } else {
                        Properties.UpdateValue( 1, "Already Closed" );
                    }
                } catch( Exception oEx ) {
                    if( CiVErrorList.IsUnhandled( oEx ) )
                        throw;

                    Properties.UpdateValue( 1, "Close Error" );
                    LogError( "Morse Com port", oEx.Message, fShow:false );
                }
                return true; // Handled, even if ended in error.
            }

            return false;
        }
    }

    public class DocProperties : IPgParent, IPgLoad {
        protected readonly IPgBaseSite _oSiteBase;

        public IPgParent Parentage => _oSiteBase.Host;
        public IPgParent Services  => Parentage.Services;
        public void      LogError( string strMessage ) { _oSiteBase.LogError( "Property Page Client", strMessage ); }

        public Editor Property_Labels { get; }
        public Editor Property_Values { get; }

		protected class DocSlot :
			IPgBaseSite
		{
			protected readonly DocProperties _oHost;

			public DocSlot( DocProperties oHost ) {
				_oHost = oHost ?? throw new ArgumentNullException( "Host" );
			}

			public IPgParent Host => _oHost;

            public void LogError(string strMessage, string strDetails, bool fShow=true) {
				_oHost.LogError( strDetails );
			}

			public void Notify( ShellNotify eEvent ) {
				// Might want this value when we close to save the current playing list!!
			}
		}

        public DocProperties( IPgBaseSite oSiteBase ) {
            _oSiteBase = oSiteBase ?? throw new ArgumentNullException( "Site must not be null." );

            Property_Labels = new Editor( new DocSlot( this ) );
            Property_Values = new Editor( new DocSlot( this ) );
        }

        public bool InitNew() {
            if( !Property_Labels.InitNew() )
                return false;
            if( !Property_Values.InitNew() )
                return false;

            return true;
        }

        public void AddLabel( string strLabel, string strValue = "" ) {
            Property_Labels.LineAppend( strLabel, fUndoable:false );
            Property_Values.LineAppend( strValue, fUndoable:false );
        }

        public Line this[int iIndex] { 
            get { 
                return Property_Values[iIndex];
            }
        }

        public void UpdateValue( int iIndex, string strValue ) {
            Line oLine = Property_Values[iIndex];
            oLine.Empty();
            oLine.TryAppend( strValue );

            Property_Values.Raise_BufferEvent( BUFFEREVENTS.MULTILINE ); // single line probably depends on the caret.
        }
    }

    public class ViewStandardProperties : 
        FormsWindow,
        IBufferEvents,
        IPgLoad
     {
        public static Guid GUID {get;} = new Guid("{80C855E0-C2F6-4641-9A7C-B6A8A53B3FDF}");

        protected DocProperties Document { get; }
		protected readonly IPgStandardUI2 _oStdUI;

        public ViewStandardProperties( IPgViewSite oSiteView, DocProperties oDocument ) : base( oSiteView, oDocument.Property_Values ) {
            Document = oDocument ?? throw new ArgumentNullException( "ViewStandardProperties's Document is null." );
 			_oStdUI  = oSiteView.Host.Services as IPgStandardUI2 ?? throw new ArgumentException( "Parent view must provide IPgStandardUI service" );
        }

        public void PropertyInitRow( SmartTable oLayout, int iIndex, EditWindow2 oEditWin = null ) {
            var oLayoutLabel = new LayoutSingleLine( new FTCacheWrap( Document.Property_Labels[iIndex]   ), LayoutRect.CSS.Flex );
            LayoutRect oLayoutValue;
            
            if( oEditWin == null ) {
                oLayoutValue = new LayoutSingleLine( new FTCacheWrap( Document.Property_Values[iIndex] ), LayoutRect.CSS.Flex );
            } else {
                oEditWin.InitNew();
                oEditWin.Parent = this;
                oLayoutValue = new LayoutControl( oEditWin, LayoutRect.CSS.Pixels, 100 );
            }

            oLayout.AddRow( new List<LayoutRect>() { oLayoutLabel, oLayoutValue } );

            oLayoutLabel.BgColor = _oStdUI.ColorsStandardAt( StdUIColors.BGReadOnly );

            CacheList.Add( oLayoutLabel );
            if( oLayoutValue is LayoutSingleLine oLayoutSingle ) {
                oLayoutSingle.BgColor = _oStdUI.ColorsStandardAt( StdUIColors.BG );
                CacheList.Add( oLayoutSingle );
            }
        }

        public override bool InitNew() {
            if( !base.InitNew() ) 
                return false;

            SmartTable oLayout = new SmartTable( 5, LayoutRect.CSS.None );
            Layout2 = oLayout;

            oLayout.Add( new LayoutRect( LayoutRect.CSS.Flex, 30, 0 ) ); // Name.
            oLayout.Add( new LayoutRect( LayoutRect.CSS.None, 70, 0 ) ); // Value.

            foreach( Line oLine in Document.Property_Labels ) {
                PropertyInitRow( oLayout, oLine.At );
            }

            Caret.Layout = CacheList[0];

            OnDocumentEvent( BUFFEREVENTS.MULTILINE );
            OnSizeChanged( new EventArgs() );

            return true;
        }

        public void OnEvent( BUFFEREVENTS eEvent ) {
            OnDocumentEvent( BUFFEREVENTS.MULTILINE );
            Invalidate();
        }
    }
}
