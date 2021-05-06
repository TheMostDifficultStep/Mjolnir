﻿using System;
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
using Play.Edit;
using Play.Integration;
using Play.Parse.Impl.Text;
using Play.Parse.Impl;

//using MySql.Data.MySqlClient;

namespace Play.MorsePractice {
    public class CallsDoc : Editor {
        public CallsDoc( IPgBaseSite oSite ) : base( oSite ) { }

		public override WorkerStatus PlayStatus {
			get { return( WorkerStatus.BUSY ); }
		}
    }

	public class MorseDoc:
		IPgParent,
		IPgLoad<TextReader>,
		IPgSave<TextWriter>,
        IPgTableDocument,
        IDisposable
	{
        readonly IPgScheduler      _oScheduler;
        readonly IPgRoundRobinWork _oTaskPlace;

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
		public Editor   MorseReference  { get; } // Our reference to the morse values. Let's make this audio!!
        public CallsDoc Calls  { get; } // List of callsigns in left hand column of notes file.

        public Editor CallSign        { get; }
        public Editor CallSignBio     { get; } // Text generated by walking the html stream.
        public Editor CallSignAddress { get; } // This is the mailing address displayed.
        public Editor CallSignBioHtml { get; } // base 64 converted HTML streaml;
        public Editor CallSignPageHtml{ get; } // This is the main page returned by qrz.

                 SerialPort              _spCiV; // Good cantidate for "init once"
        readonly ConcurrentQueue<byte[]> _oMsgQueue = new ConcurrentQueue<byte[]>();
        readonly Line                    _oDataGrams = new TextLine( 0, string.Empty );
        readonly Grammer<char>           _oCiVGrammar;

		protected static readonly HttpClient _oHttpClient = new HttpClient(); 

        /// <summary>
        /// Document object for a little Morse Practice document.
        /// </summary>
        public MorseDoc( IPgBaseSite oSiteBase ) {
			_oSiteBase  = oSiteBase ?? throw new ArgumentNullException();
            _oSiteFile  = oSiteBase as IPgFileSite ?? throw new ArgumentException( "Host needs the IPgFileSite interface" );
            _oScheduler = Services as IPgScheduler ?? throw new ArgumentException("Host requries IPgScheduler");
            _oTaskPlace = _oScheduler.CreateWorkPlace() ?? throw new InvalidOperationException("Couldn't create a worksite from scheduler for file downloader.");

			try {
				_oCiVGrammar = (Grammer<char>)((IPgGrammers)Services).GetGrammer( "civ" );
			} catch ( Exception oEx ) {
                Type[] rgErrors = { typeof( NullReferenceException ),
                                    typeof( InvalidCastException ),
                                    typeof( ArgumentNullException ),
									typeof( FileNotFoundException ),
									typeof( GrammerNotFoundException ) };
                if( !rgErrors.Contains( oEx.GetType() ) )
                    throw;

				LogError( "Morse", "Couldn't get grammar for Ci-V parser." );
			}

            Source           = new Editor  ( new MorseDocSlot( this, "Source" ) ); // Morse code source for practice.
			Notes            = new Editor  ( new MorseDocSlot( this, "Notes"  ) ); // Notes for listening to morse, or log files.
			Stats            = new Editor  ( new MorseDocSlot( this, "Stats"  ) );
			MorseReference   = new Editor  ( new MorseDocSlot( this, "Ref"    ) ); // Refrence table of morse code letters.
            Calls            = new CallsDoc( new MorseDocSlot( this, "Calls"  ) ); // document for outline, compiled list of stations
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
                _spCiV?.Close();

				Source.Dispose();
				Notes .Dispose();
				Stats .Dispose();
                MorseReference .Dispose();
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

        public ICollection<ICollection<Line>> Rows { get; } = new List<ICollection<Line>>();

        /// <summary>
        /// Load Morse code reference text.
        /// </summary>
        /// <remarks>Not likely to fail once get working. So no return value.</remarks>
        protected void LoadMorse() { 
            Assembly oAssembly = Assembly.GetExecutingAssembly();

			try {
				using( Stream oStream = oAssembly.GetManifestResourceStream( _strMorseTable )) {
					using( TextReader oText = new StreamReader( oStream, true ) ) {
						MorseReference.Load( oText );
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
        /// This is a task we use to monitor the serial port message queue.
        /// While I could make a parser that parses over 'byte' for ease of debugging
        /// I convert the byte stream to a human readable 'char' stream. Also I can stick
        /// the stream into my editor and see colorization, so it's a win / win.
        /// </summary>
        IEnumerator<int> ListenToCom() {
            while( true ) {
                if( _oMsgQueue.TryDequeue( out byte[] rgMsg ) ) {
                    SerialToDatagram( rgMsg );

                    if( _oCiVGrammar != null ) {
                        CharStream     oStream = new CharStream( _oDataGrams );
                        DatagramParser oParse  = new DatagramParser( oStream, _oCiVGrammar );

                        oParse.Parse();
                    }
                }
                yield return 250;
            }
        }

        /// <summary>
        /// this procedure displays the data as one datagram per line. I've
        /// written the ci-v grammar to parse this stream
        /// </summary>
        protected void SerialToDatagram( byte[] rgMsg ) {
            _oDataGrams.Empty();

            foreach( byte bByte in rgMsg ) {
                string strByte = bByte.ToString( "X2" );

                _oDataGrams.TryAppend( strByte + " " );
            }
        }

        /// <summary>
        /// this procedure displays the data as one datagram per line in the
        /// Notes editor. 
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
        /// on a single line in the Notes editor.
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

        protected void InitSerial() {
            // this is crude since we're polling even if no messages.
            // we'll fix this up later.
            _oTaskPlace.Queue( ListenToCom(), 100 );

            _spCiV = new SerialPort("COM4");

            _spCiV.BaudRate  = 9600;
            _spCiV.Parity    = Parity.None;
            _spCiV.StopBits  = StopBits.One;
            _spCiV.DataBits  = 8;
            _spCiV.Handshake = Handshake.None;

            _spCiV.DataReceived += CiV_DataReceived; 
            _spCiV.Open();
        }

        /// <summary>
        /// This event comes in asynchronously on it's own thread. Queue up the message
        /// so that the foreground tread can pick it up.
        /// </summary>
        /// <remarks>Would be nice if we could post a message to the forground so that
        /// it spins up a task that can terminate once all the data has been pulled.</remarks>
        private void CiV_DataReceived( object sender, SerialDataReceivedEventArgs e ) {
            int iBytesWaiting = _spCiV.BytesToRead;
            if( iBytesWaiting > 0 ) {
                byte[] rgMsg = new byte[iBytesWaiting];

                _spCiV.Read( rgMsg, 0, iBytesWaiting );
                _oMsgQueue.Enqueue( rgMsg );
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
            if( !Calls.InitNew() )
                return false;

            // Note: Change this to trigger after a notes parse.
            //_oTaskSched.Queue( EnumCallsScanTask(), 3000 );
            Notes.BufferEvent += Notes_BufferEvent;

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
			if( !Stats.InitNew() )
				return false;

            InitSerial();

            for( int i=0; i<3; ++i ) {
                List<Line> rgRow = new List<Line>(7);

                for( int j=0; j<7; ++j ) {
                    rgRow.Add( new TextLine( j, j.ToString() ) );
                }
                Rows.Add( rgRow );
            }

            LoadMorse();
         // LoadStats(); We'll load the stats table here, it'll double as an audio reference.

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

            if (_oTaskPlace.Status == WorkerStatus.FREE) {
                try {
                    StringContent oContent = new StringContent(oQuery.ToString(), System.Text.Encoding.UTF8, strMediaType);
                    Task<HttpResponseMessage> oTask = client.PostAsync(strURL, oContent);
                    _oTaskPlace.Queue(EnumWatchTask(oTask), 100);
                } catch (HttpRequestException) {
                    PageReset();

                    CallSignBio.LineAppend("Unable to process request");
                    return;
                }
            }
        }

        public void TableListenerAdd(IPgTableEvents oEvent) {
        }

        public void TableListenerRemove(IPgTableEvents oEvent) {
        }
    }

    /// <summary>
    /// Parse the converted base64 HTML blob, CallSignSource, that we skimmed from the main document.
    /// </summary>
    public class ParseBioHTMLSkimmer : ParseHandlerHTML {
        MorseDoc Document { get; }

        Editor   Bio => Document.CallSignBio;

        public ParseBioHTMLSkimmer( MorseDoc oDocument ) : 
            base( oDocument.CallSignBioHtml ) 
        {
            Document = oDocument ?? throw new ArgumentNullException( "Document must not be null" );
        }

        /// <summary>
        /// The biography is a base64 blob we pull out of the main text. After it's parsed, we walk the collection
        /// to grab the text between the tags. It's sort of a crude text browser!! We don't actually parse the
        /// tag stream in this subclass we just walk it looking for text to stuff in our editor.
        /// </summary>
        public override void PassTwo() {
            try {
                Bio.Clear();

                if ( _rgTagBag.Count < 1 )
                    return;

                TagInfo       oLastTag       = _rgTagBag[0];
                string[]      rgLineBreakers = { "<p>", "<li>", "<br>", "<h1>", "<h2>", "<h3>", "<h4>" };
                string[]      rgLineBlocks   = { "<p>", "<h1>", "<h2>", "<h3>", "<h4>" };
                StringBuilder sbBioText      = new StringBuilder();
                
                for( int iTag = 1; iTag<_rgTagBag.Count; ++iTag ) {
                    TagInfo oTag   = _rgTagBag[iTag];
                    int     iStart = oLastTag._oMemState.End;
                    int     iEnd   = oTag    ._oMemState.Start - 1;
                    int     iLen   = iEnd - iStart + 1;

                    if( iLen > 0 ) {
                        int j, k=iEnd;
                        for( j=iStart; j<iStart+iLen; ++j ) {
                            if( !Char.IsWhiteSpace( _oStream[j] ) )
                                break;
                        }
                        //for( k=iEnd; k>=j; --k ) {
                        //    if (!Char.IsWhiteSpace( _oStream[k] ) )
                        //        break;
                        //}
                        int iTrim = k - j + 1;
                        if( iTrim > 0 ) 
                            sbBioText.Append( _oStream.SubString( j, iTrim ) );
                    }

                    if( oTag.MarkupType == MARKUPTYPE.ENTITY ) {
                        sbBioText.Append( oTag.Name );
                    }

                    if( rgLineBreakers.Contains( oTag.Name ) &&
                        oTag.MarkupType == MARKUPTYPE.START &&
                        sbBioText.Length > 0 ) 
                    {
                        string strNew = sbBioText.ToString();

                        if( !IsClearText( strNew ) ) {
                            Bio.LineAppend( strNew );
                            if( rgLineBlocks.Contains( oTag.Name ) )
                                Bio.LineAppend( string.Empty );
                            sbBioText.Clear();
                        }
                    }

                    oLastTag = oTag;
                }
            } catch( Exception oEx ) {
                Type[] rgErrors = { typeof( NullReferenceException ),
                                    typeof( ArgumentNullException ),
                                    typeof( ArgumentOutOfRangeException ),
                                    typeof( IndexOutOfRangeException ) };
                if( rgErrors.IsUnhandled( oEx ) )
                    throw;

                Bio.LineAppend( "Data Read Error..." );
            }
        }
    }

    /// <summary>
    /// Cheap attempt at skimming the address from the HTML. It doesn't work. 
    /// What I really need to do is to make a TAG grammer to pull this information.
    /// </summary>
    public class ParseQrzHTMLSkimmer : ParseHandlerHTML {
        MorseDoc Document { get; }

        Editor   Address => Document.CallSignAddress;
        Editor   QrzPage => Document.CallSignPageHtml;

        public ParseQrzHTMLSkimmer( MorseDoc oDocument ) : 
            base( oDocument.CallSignPageHtml ) 
        {
            Document = oDocument ?? throw new ArgumentNullException( "Document must not be null" );
        }

        public override void PassTwo() {
            try {
                Address.Clear();

                if ( _rgTagBag.Count < 1 )
                    return;

                TagInfo       oLastTag       = _rgTagBag[0];
                string[]      rgLineBreakers = { "<p>", "<li>", "<br>", "<h1>", "<h2>", "<h3>", "<h4>" };
                string[]      rgLineBlocks   = { "<p>", "<h1>", "<h2>", "<h3>", "<h4>" };
                string[]      rgParaStyles   = { "p8", "p7" };
                StringBuilder sbText         = new StringBuilder();
                DataStream<char> oPageStream = QrzPage.CreateStream();
                
                for( int iTag = 1; iTag<_rgTagBag.Count; ++iTag ) {
                    TagInfo oTag   = _rgTagBag[iTag];
                    int     iStart = oLastTag._oMemState.End;
                    int     iEnd   = oTag    ._oMemState.Start - 1;
                    int     iLen   = iEnd - iStart + 1;

                    if( iLen > 0 ) {
                        int j, k=iEnd;
                        for( j=iStart; j<iStart+iLen; ++j ) {
                            if( !Char.IsWhiteSpace( _oStream[j] ) )
                                break;
                        }
                        int iTrim = k - j + 1;
                        if( iTrim > 0 ) 
                            sbText.Append( _oStream.SubString( j, iTrim ) );
                    }

                    if( oTag.MarkupType == MARKUPTYPE.ENTITY ) {
                        sbText.Append( oTag.Name );
                    }

                    string strAttrib = oTag.GetAttributeValue(  oPageStream, "style" );

                    if( rgLineBreakers.Contains( oTag.Name ) &&
                        oTag.MarkupType == MARKUPTYPE.START &&
                        rgParaStyles.Contains( strAttrib ) &&
                        sbText.Length > 0 ) 
                    {
                        string strNew = sbText.ToString();

                        if( !IsClearText( strNew ) ) {
                            Address.LineAppend( strNew );
                            if( rgLineBlocks.Contains( oTag.Name ) )
                                Address.LineAppend( string.Empty );
                            sbText.Clear();
                        }
                    }

                    oLastTag = oTag;
                }
            } catch( Exception oEx ) {
                Type[] rgErrors = { typeof( NullReferenceException ),
                                    typeof( ArgumentNullException ),
                                    typeof( ArgumentOutOfRangeException ),
                                    typeof( IndexOutOfRangeException ) };
                if( rgErrors.IsUnhandled( oEx ) )
                    throw;

                Address.LineAppend( "Data Read Error..." );
            }
        }
    }
}
