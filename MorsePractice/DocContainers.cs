using Play.Edit;
using Play.Forms;
using Play.Integration;
using Play.Interfaces.Embedding;
using Play.Parse;
using Play.Parse.Impl;
//using System.Data.SqlClient;
//Microsoft.Data.SqlClient the new api for ms db's
//https://mariadb.com/kb/en/mariadb-and-net/ // Question and answer.
//https://docs.microsoft.com/en-us/azure/mysql/connect-csharp // good tutorial for starting up.
//https://mysqlconnector.net/ mit project for ado.net connectivity.

using SkiaSharp;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

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
    /// <seealso cref="DocStdLog"/>
	public class MorseDoc:
		IPgParent,
		IPgLoad<TextReader>,
		IPgSave<TextWriter>,
        IDisposable
	{
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
            string    IPgFileSite.FileName     => _oHost._oSiteFile.FileName + " / " + _strName;
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
			_oSiteBase = oSiteBase ?? throw new ArgumentNullException();
            _oSiteFile = (IPgFileSite)oSiteBase;

            Source     = new Editor  ( new MorseDocSlot( this, "Source" ) ); // Morse code source for practice.
			Notes      = new Editor  ( new MorseDocSlot( this, "Notes"  ) ); // Notes for listening to morse, or log files.
			Stats      = new Editor  ( new MorseDocSlot( this, "Stats"  ) );
			Morse      = new Editor  ( new MorseDocSlot( this, "Ref"    ) ); // Refrence table of morse code letters.

            new ParseHandlerText( Notes, "text" );
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
                LogError( "Initialize Morse Table", "Couldn't load Morse table" );
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
			if( !Source.Load( oStream ) )
				return false;

			return true;
		}

		public bool Save(TextWriter oStream) {
            return Source.Save( oStream ); // Save the source not the notes. That's the practice sheet.
		}
    }

    /// <summary>
    /// Our new net logger that uses standard editor for the Notes, and 
    /// the multicolumn editor for the logger...
    /// </summary>
    /// <seealso cref="DocStdLog"/>
	public class DocNetHost:
		IPgParent,
		IPgLoad<TextReader>,
		IPgSave<TextWriter>,
        IDisposable
	{
        protected class DocNetHostSlot :
			IPgBaseSite,
            IPgFileSite
		{
			protected readonly DocNetHost _oHost;
            protected readonly string   _strName;

			public DocNetHostSlot( DocNetHost oHost, string strName ) {
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
            string    IPgFileSite.FileName     => _oHost._oSiteFile.FileName + " / " + _strName;
		}

		readonly IPgBaseSite _oSiteBase;
        readonly IPgFileSite _oSiteFile;

		public bool      IsDirty   => Notes.IsDirty || Log.IsDirty;
		public IPgParent Parentage => _oSiteBase.Host;
		public IPgParent Services  => Parentage.Services;

        // Stuff for the morse code pracice view.
		public Editor         Notes { get; } // pointers to net info...
		public DocLogMultiColumn Log   { get; } // actual log

        public DocNetHost( IPgBaseSite oSiteBase ) {
			_oSiteBase  = oSiteBase ?? throw new ArgumentNullException();
            _oSiteFile  = (IPgFileSite )oSiteBase;

			Notes       = new Editor        ( new DocNetHostSlot( this, "Notes" ) ); // Notes for running the net.
			Log         = new DocLogMultiColumn( new DocNetHostSlot( this, "Log"   ) ); // Log the operators.

            new ParseHandlerText( Notes, "text" );
        }

        private bool _fDisposed = false;

		public void Dispose() {
			if( !_fDisposed ) {
				Notes.Dispose();
				Log  .Dispose();

                _fDisposed = true;
			}
		}

        public string FileName => _oSiteFile.FileName;

		protected void LogError( string strMessage, string strDetails, bool fShow = false ) {
			_oSiteBase.LogError( strMessage, strDetails, fShow );
		}

        public bool InitNew() {
			if( !Notes.InitNew() )
				return false;
			if( !Log  .InitNew() )
				return false;

			return true;
		}

        protected bool LoadLogXml( TextReader oInput ) {
            XmlDocument  xmlDoc   = new XmlDocument();
            List<string> rgTokens = new ();

            void ParseTokens( string strLine ) {
                rgTokens.Clear();
                int iStart   = 0;

                for( int i=0; i<strLine.Length; ++i ) {
                    if( strLine[i] == '\t' ) {
                        rgTokens.Add( strLine.Substring( iStart, i-iStart ) );
                        iStart = i+1;
                    }
                }
                if( iStart < strLine.Length ) {
                    rgTokens.Add( strLine.Substring( iStart, strLine.Length-iStart ) );
                }
            }
            try {
                xmlDoc.Load( oInput );

                if( xmlDoc.SelectSingleNode( "//Root/Notes" ) is XmlNode xmlNotes ) {
                    StringReader oReader = new StringReader( xmlNotes.InnerText );

                    while( oReader.ReadLine() is string strLine ) {
                        Notes.LineAppend( strLine, fUndoable:false );
                    }
                }
                if( xmlDoc.SelectSingleNode( "//Root/Log" ) is XmlNode xmlLog ) {
                    StringReader                    oReader = new ( xmlLog.InnerText );
                    using DocLogMultiColumn.BulkLoader oLoader = new ( Log );

                    while( oReader.ReadLine() is string strLine ) {
                        LogRow oRow = new LogRow();
                        ParseTokens( strLine );
                        for( int i=0; i<rgTokens.Count; ++i ) {
                            if( i<3 )
                                oRow[i].TryReplace( 0, 0, rgTokens[i] );
                            else 
                                oRow[2].TryReplace( oRow[2].ElementCount, 0, rgTokens[i] );
                        }
                        oLoader.Append( oRow );
                    }
                }

                Notes.ClearDirty();
                if( Log.ElementCount <= 0 ) {
                    Log.InsertNew();
                }
                return true;
            } catch( Exception oEx ) {
                if( rgStdErrors.IsUnhandled( oEx ) ) 
                    throw;

                LogError( "DocNetHost", "Problem loading file check if it is XML." );
            }

            return false;
        }

        protected static Type[] rgStdErrors = 
            { typeof( XmlException ),
              typeof( NullReferenceException ),
              typeof( ArgumentNullException ),
              typeof( ArgumentOutOfRangeException ),
              typeof( XmlException ) };

		public bool Load(TextReader oStream) {
            if( !Notes.InitNew() )
                return false;

            if( !LoadLogXml( oStream ) ) 
                return false;

			return true;
		}

        /// <remarks>
        /// Saving to XML is a tad crude since I'm holding the entire thing
        /// in memory. A database would be nicer. But... :-/
        /// </remarks>
		public bool Save(TextWriter oStream) {
            XmlDocument xmlDoc = new XmlDocument();
            try {
                if( xmlDoc.CreateElement( "Root" ) is XmlElement xmlRoot ) {
                    xmlDoc.AppendChild( xmlRoot );
                    if( xmlDoc.CreateElement( "Notes" ) is XmlElement xmlNotes ) {
                        StringBuilder sbBuilder = new();

                        int iCount = 0;
                        foreach( Line oLine in Notes ) {
                            if( iCount++ > 0 )
                                sbBuilder.Append( "\r\n" );

                            sbBuilder.Append( oLine.ToString() );
                        }
                        xmlNotes.InnerText = sbBuilder.ToString();
                        xmlRoot.AppendChild( xmlNotes );
                    }
                    if( xmlDoc.CreateElement( "Log" ) is XmlElement xmlLog ) {
                        StringBuilder sbBuilder = new();

                        foreach( Row oRow in Log ) {
                            sbBuilder.Append( oRow[0] );
                            sbBuilder.Append( '\t' );
                            sbBuilder.Append( oRow[1] );
                            sbBuilder.Append( '\t' );
                            sbBuilder.Append( oRow[2] );
                            sbBuilder.Append( "\r\n" );
                        }
                        xmlLog.InnerText = sbBuilder.ToString();
                        xmlRoot.AppendChild( xmlLog );
                    }
                }

                xmlDoc.Save( oStream );

                Notes.ClearDirty();
                Log.IsDirty = false;
            } catch( Exception oEx ) {
                if( rgStdErrors.IsUnhandled( oEx ) ) 
                    throw;
                LogError( "DocNetHost", "Problem saving file" );
                return false;
            }

            return true;
		}
    }

    /// <summary>
    /// this will be our new call logger and ic-705 communicator.
    /// </summary>
	public class DocStdLog:
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
			protected readonly DocStdLog _oHost;
            protected readonly string    _strName;

			public MorseDocSlot( DocStdLog oHost, string strName ) {
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
            string    IPgFileSite.FileName     => _oHost._oSiteFile.FileName + " / " + _strName;
		}

		readonly IPgBaseSite _oSiteBase;
        readonly IPgFileSite _oSiteFile;

        // Stuff for the morse code pracice view.
		public Editor          Notes      { get; } // practice screen notes. and primary view.
        public CallsDoc        Calls      { get; } // List of callsigns in left hand column of notes file.
        public RadioProperties Properties { get; }

        public Editor CallSign        { get; }
        public Editor CallSignBio     { get; } // Text generated by walking the html stream.
        public Editor CallSignAddress { get; } // This is the mailing address displayed.
        public Editor CallSignBioHtml { get; } // base 64 converted HTML streaml;
        public Editor CallSignPageHtml{ get; } // This is the main page returned by qrz.

                 SerialPort              _oCiV; // Good cantidate for "init once"
        readonly ConcurrentQueue<byte[]> _oMsgQueue = new ConcurrentQueue<byte[]>(); // events to our forground thread.
        readonly Edit.Line                    _oDataGram = new TextLine( 0, string.Empty );
        readonly Grammer<char>           _oCiVGrammar;
        readonly DatagramParser          _oParse;

        byte     TransmitterAddress { get; set; } = 0xA4;
        byte     ControllerAddress  { get; set; } = 0xE0;

        public bool FlagScanCalls { get; set; } = true;  // Set up call back to buffer to parse calls.
        public bool FlagSComsOn   { get; set; } = false; // Turn on the com ports.

        readonly Dictionary <int, RepeaterDir>     _rgRepeatersIn   = new Dictionary<int, RepeaterDir>();
        readonly Dictionary <int, RepeaterDir>     _rgRepeatersOut  = new Dictionary<int, RepeaterDir>();
        readonly Dictionary <string, RepeaterInfo> _rgRepeatersInfo = new Dictionary<string, RepeaterInfo>();
        protected int    _iRadioFrequency = 0;
        protected double _dblRadioTone    = 0;

		protected static readonly HttpClient _oHttpClient = new HttpClient(); 

        protected static Type[] CiVErrorList {
            get {
                Type[] rgErrors = { typeof( UnauthorizedAccessException ),
                                    typeof( IOException ),
                                    typeof( ArgumentOutOfRangeException ),
                                    typeof( ArgumentNullException ),
                                    typeof( ArgumentException ),
                                    typeof( InvalidOperationException ),
                                    typeof( NullReferenceException ),
                                    typeof( FileNotFoundException ),
                                    typeof( TimeoutException ),
                                    typeof( OperationCanceledException ) };
                return rgErrors;
            }
        }

        /// <summary>
        /// Document object for a little Morse Practice document.
        /// </summary>
        public DocStdLog( IPgBaseSite oSiteBase ) {
			_oSiteBase  = oSiteBase ?? throw new ArgumentNullException();
            _oSiteFile  = oSiteBase as IPgFileSite ?? throw new ArgumentException( "Host needs the IPgFileSite interface" );
            _oScheduler = Services as IPgScheduler ?? throw new ArgumentException("Host requries IPgScheduler");
            _oTaskQrz   = _oScheduler.CreateWorkPlace() ?? throw new ApplicationException("No worksite for file downloader.");
            _oTaskCiv   = _oScheduler.CreateWorkPlace() ?? throw new ApplicationException("No worksite for Civ." );
            _oTaskTimer = _oScheduler.CreateWorkPlace() ?? throw new ApplicationException("No worksite for Frequency Change Listener." );

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

			Notes            = new Editor         ( new MorseDocSlot( this, "Notes"  ) ); // Notes for listening to morse, or log files.
            Calls            = new CallsDoc       ( new MorseDocSlot( this, "Calls"  ) ); // document for outline, compiled list of stations
            Properties       = new RadioProperties( new MorseDocSlot( this, "Properties" ) );

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

        public struct RepeaterInfo {
            public RepeaterInfo( string strLocation, string strGroup, string strUrl = "" ) {
                Location = strLocation;
                Group    = strGroup;
                Grid     = string.Empty;
                URL      = strUrl;
            }

            public string Location { get; }
            public string Group    { get; }
            public string Grid     { get; }
            public string URL      { get; }
        }

        public struct RepeaterDir {
            /// <summary>
            /// 
            /// </summary>
            /// <param name="flFreq">Frequency in mHz</param>
            /// <param name="iOffs">Offset in kHz</param>
            /// <param name="iTime">Time in seconds</param>
            /// <param name="strCall">Repeater</param>
            /// <param name="strDesc">Description</param>
            /// <remarks>Tried using float for the flFreq, but I'd get inexact values when mult by 10^6. Double
            /// seems to be free of the problem.</remarks>
            public RepeaterDir( double flFreq, int iOffs, int iTime, string strCall = "", string strTone = "" ) {
                _iFrequency = (int)(flFreq * Math.Pow( 10, 6 ));
                _iOffset    = iOffs  * 1000;

                Timeout  = iTime;
                CallSign = strCall;
                Tone     = strTone;
            }

            private readonly int _iFrequency;
            private readonly int   _iOffset;

            public int    Timeout     { get; }
            public string CallSign    { get; }
            public string Tone        { get; }

            public int  Output => _iFrequency;
            public int  Input  => _iFrequency + _iOffset;

            public override string ToString() {
                return CallSign;
            }
        }

        /// <summary>
        /// Ignore the reference repeater.
        /// </summary>
        public List<RepeaterDir> RepeaterBandsFind( RepeaterDir oRepeater ) {
            List<RepeaterDir> rgRepeaters = new List<RepeaterDir>();

            foreach( KeyValuePair<int,RepeaterDir> oPair in _rgRepeatersIn ) {
                if( string.Compare( oPair.Value.CallSign, oRepeater.CallSign, ignoreCase:true ) == 0 ) {
                    if( oRepeater.Output != oPair.Value.Output ) {
                        rgRepeaters.Add( oPair.Value );
                    }
                }
            }

            return rgRepeaters;
        }

        public class RepeaterHyperText : IPgWordRange {
            int _iColor = 1;
            int _iStart;
            int _iLength;
            string _strAlternate;

            public RepeaterHyperText( int iColor, int iStart, int iLength, string strAlt = "Alternate" ) {
                _iColor  = iColor;
                _iStart  = iStart;
                _iLength = iLength;
                _strAlternate = strAlt;
            }

            public bool   IsWord     => true;
            public bool   IsTerm     => true;
            public string StateName  => _strAlternate;
            public int    ColorIndex => _iColor;

            public int Offset { get => _iStart;  set => throw new NotImplementedException(); }
            public int Length { get => _iLength; set => throw new NotImplementedException(); }
        }

        /// <summary>
        /// Populate the URL property setting the URL value and it's formatting        /// </summary>
        /// <remarks>. You know, I could probably just parse the properties document as a text (or 
        /// modified text grammer) document and get all the formatting for free.
        /// </remarks>
        public void PopulateURL( RepeaterInfo oInfo ) {
            Edit.Line oLine = Properties.ValueAsLine((int)RadioProperties.Names.Repeater_URL);

            oLine.Empty();
            oLine.Formatting.Clear();

            oLine.TryAppend( oInfo.URL );
            oLine.Formatting.Add( new RepeaterHyperText( 1, 0, oInfo.URL.Length, "URL" ) );
        }

        public void PopulateAlternates( RepeaterDir oRepeater ) {
            List<RepeaterDir> rgRepeaters = RepeaterBandsFind( oRepeater );

            Edit.Line oLine = Properties.ValueAsLine( (int)RadioProperties.Names.Alternates );

            oLine.Empty();
            oLine.Formatting.Clear();

            if( rgRepeaters.Count == 0 ) {
                oLine.TryAppend( "None" );
            } else {
                foreach( RepeaterDir oDir in rgRepeaters ) {
                    try {
                        double dblFreqInMhz = (double)oDir.Output / Math.Pow( 10, 6 );
                        string strFreqInMhz = dblFreqInMhz.ToString();
                        int    iStart       = oLine.ElementCount;

                        oLine.TryAppend( strFreqInMhz );
                        oLine.TryAppend( " " );
                        oLine.Formatting.Add( new RepeaterHyperText( 1, iStart, strFreqInMhz.Length ) );
                    } catch( Exception oEx ) {
                        Type[] rgErrors = { typeof( NotImplementedException ),
                                            typeof( NullReferenceException ) };
                        if( rgErrors.IsUnhandled( oEx ) )
                            throw;
                    }
                } 
            }
            Properties.RaiseUpdateEvent();
       }

        /// <summary>
        /// It is interesting that we only get notice of transmission by the change
        /// of frequency from listening to TX freq. Only for (duplex) repeaters, which
        /// is a little bit of a bummer since I can't tell how long I've been talking
        /// on simplex. Tho' that's not as important.
        /// </summary>
        /// <param name="iFrequency"></param>
        /// <param name="fRequested">If we sent a 03 freqency request, we'll have fRequest = true,
        /// if the frequency change is just because we turned the dial or started transmitting fRequest = false.</param>
        public void CiVFrequencyChange( bool fRequested, int iFrequency ) {
            RepeaterDir oRepeater;
            double      dblFreqInMhz = (double)iFrequency / Math.Pow( 10, 6 );

            _iRadioFrequency = iFrequency;

            Properties.ValuesEmpty();

            Properties.ValueUpdate( RadioProperties.Names.Timer, "Stopped" );
            Properties.ValueUpdate( RadioProperties.Names.Radio_Link, "On" );
            Properties.ValueUpdate( RadioProperties.Names.Frequency, dblFreqInMhz.ToString() + " mHz" );

            if( _rgRepeatersIn.TryGetValue( iFrequency, out oRepeater ) ) {
                if( !fRequested ) {
                    Properties.ValueUpdate( RadioProperties.Names.Timer,   "Timer start..." );
                    Properties.ValueUpdate( RadioProperties.Names.Callsign, oRepeater.CallSign );
                    _oTaskTimer.Queue( ListenForTimout( oRepeater.Timeout ), 0 );
                }
            } else {
                if( _rgRepeatersOut.TryGetValue( iFrequency, out oRepeater ) ) {
                    Properties.ValueUpdate( RadioProperties.Names.Timer,    "Timer stop..." );
                    Properties.ValueUpdate( RadioProperties.Names.Callsign, oRepeater.CallSign );
                }
                _oTaskTimer.Stop(); // stopped talking most likely.
            }

            if( !string.IsNullOrEmpty( oRepeater.CallSign ) ) { 
                RepeaterInfo oInfo;
                if( _rgRepeatersInfo.TryGetValue( oRepeater.CallSign, out oInfo ) ) {
                    Properties.ValueUpdate( RadioProperties.Names.Location,      oInfo.Location );
                    Properties.ValueUpdate( RadioProperties.Names.Group,         oInfo.Group );
                    Properties.ValueUpdate( RadioProperties.Names.Repeater_Tone, oRepeater.Tone );
                    PopulateURL( oInfo );
                }
                PopulateAlternates( oRepeater );
            }

            SendCommand( 0x1B, 0x00 ); // request tone freq.
            SendCommand( 0x16, 0x42 ); // request repeter tone squelch.
            SendCommand( 0x14, 0x0A ); // request power level.
        }

        public void CiVRepeaterToneReport( double dblTone, ToneType eType ) {
            _dblRadioTone = dblTone;
        }

        /// <summary>
        /// Note that this value is not sent when the user updates the value on the
        /// rig. And that is a real bummer. This is only sent in response to a query 
        /// for this value. (cmd 16, sub 42)
        /// </summary>
        public void CiVRepeaterToneEnable( bool fValue ) {
            Properties.ValueUpdate( RadioProperties.Names.Rptr_Tone_Enable, fValue.ToString() );

            // this stuff we really only want to do as the result
            // of processing a frequency change, and we stop spinning
            // the dial for a few seconds or so.

            /*
            if( _rgRepeatersOut.TryGetValue( _iRadioFrequency, out RepeaterDir oInfo ) ) {
                if( !string.IsNullOrEmpty( oInfo.Tone ) ) {
                    double.TryParse( Properties[6].ToString(), out double dblRptrTone );
                    if( dblRptrTone != _dblRadioTone ) {
                        LogError( "Alert", "Radio tone does not match repeater.", true );
                    } else {
                        if( fValue == false ) {
                            LogError( "Alert", "Radio tone must be enabled!", true );
                        }
                    }
                }
            }
            */
        }

        public void CiVModeChange( string strMode, string strFilter ) {
            Properties.ValueUpdate( RadioProperties.Names.Mode, strMode );
        }

        public void CiVPowerLevel( int iLevel ) {
            float flPercent = ( iLevel ) / 255F;
            Properties.ValueUpdate( RadioProperties.Names.Power_Level, flPercent.ToString( "P2" ) );
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

                Properties.ValueUpdate( RadioProperties.Names.Timer, "Ticking..." + Math.Round( oSpan.TotalSeconds ).ToString( "N0" ) );

                yield return ( oSpan.TotalSeconds > 10 ) ? 10000 : 1000;
            } while( true );

            Properties.ValueUpdate( RadioProperties.Names.Timer,  "TIMEOUT!" );
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
                if( _oMsgQueue.IsEmpty )
                    yield return 1000; // Return after 1000 ms.
                else 
                    yield return 1; // Return right away.
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

            //SerialToList( rgMsg ); // Debug things..

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
        /// This handles command 03 (read frequency), which requires no sub command or data.
        /// Probably will work for 02 and 04 as well.
        /// </summary>
        /// <param name="bCmnd"></param>
        /// <remarks>read: Controller asks for some value from the radio.
        ///          send: Controller sets some value on the radio.</remarks>
        protected void SendCommand( byte bCmnd ) {
            List<byte> rgCommand = new List<byte>();

            rgCommand.Add( 0xfe );
            rgCommand.Add( 0xfe );
            rgCommand.Add( TransmitterAddress );
            rgCommand.Add( ControllerAddress );
            rgCommand.Add( bCmnd );
            rgCommand.Add( 0xfd );

            try {
                _oCiV.Write( rgCommand.ToArray(), 0, rgCommand.Count );
            } catch( Exception oEx ) {
                Type[] rgException = { typeof( ArgumentNullException ),
                                       typeof( InvalidOperationException ),
                                       typeof( ArgumentOutOfRangeException ),
                                       typeof( ArgumentException ),
                                       typeof( OperationCanceledException ) };
                if( rgException.IsUnhandled( oEx ) )
                    throw;

                LogError( "CiV", "CiV Send Command Error" );
            }
        }

        protected void SendCommand( byte bCmnd, Byte bSub ) {
            List<byte> rgCommand = new List<byte>();

            rgCommand.Add( 0xfe );
            rgCommand.Add( 0xfe );
            rgCommand.Add( TransmitterAddress );
            rgCommand.Add( ControllerAddress );
            rgCommand.Add( bCmnd );
            rgCommand.Add( bSub  );
            rgCommand.Add( 0xfd );

            try {
                _oCiV.Write( rgCommand.ToArray(), 0, rgCommand.Count );
            } catch( Exception oEx ) {
                Type[] rgException = { typeof( ArgumentNullException ),
                                       typeof( InvalidOperationException ),
                                       typeof( ArgumentOutOfRangeException ),
                                       typeof( ArgumentException ),
                                       typeof( OperationCanceledException ) };
                if( rgException.IsUnhandled( oEx ) )
                    throw;

                LogError( "CiV", "CiV Send Command Error" );
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
                string strPortNumber = Properties[(int)RadioProperties.Names.COM_Port].ToString();
                string strPortID     = "COM";

                if( string.IsNullOrEmpty( strPortNumber ) ) {
                    strPortID += 4;
                } else {
                    strPortID += strPortNumber;
                }

                // Byte.Parse assumes base ten string. So use Convert.ToByte( str, 16 )
                TransmitterAddress = Convert.ToByte( Properties[(int)RadioProperties.Names.Address_Radio     ].ToString(), 16 );
                ControllerAddress  = Convert.ToByte( Properties[(int)RadioProperties.Names.Address_Controller].ToString(), 16 );

                _oCiV = new SerialPort( strPortID );
                _oCiV.BaudRate  = 115200;
                _oCiV.Parity    = Parity.None;
                _oCiV.StopBits  = StopBits.One;
                _oCiV.DataBits  = 8;
                _oCiV.Handshake = Handshake.None;

                _oCiV.DataReceived += CiV_DataReceived;
            } catch( Exception oEx ) {
                Type[] rgErrors = { typeof( IOException ),
                                    typeof( FormatException ),
                                    typeof( OverflowException ),
                                    typeof( NullReferenceException ),
                                    typeof( ArgumentNullException ),
                                    typeof( ArgumentException ) };
                if( rgErrors.IsUnhandled( oEx ) )
                    throw;

                LogError( "Morse COM access", "Unable to open COM port. Timer disabled" );
                Properties.ValueUpdate( RadioProperties.Names.Timer, "Failed Create" );
                return;
            }

            try {
                if( FlagSComsOn ) {
                    _oCiV.Open();
                    Properties.ValueUpdate( RadioProperties.Names.Timer, "On" );
                } else {
                    Properties.ValueUpdate( RadioProperties.Names.Timer, "Off" );
                }
            } catch( Exception oEx ) {
                if( CiVErrorList.IsUnhandled( oEx ) )
                    throw;

                _oCiV.Dispose();

                LogError( "Morse COM access", "Unable to open COM port. Timer disabled" );
                Properties.ValueUpdate( RadioProperties.Names.Timer, "Failed Open" );
            }
        }

        protected void InitRepeaters() {
            List< RepeaterDir > rgTemp = new List<RepeaterDir>();

            rgTemp.Add( new RepeaterDir(  53.17,  -1700, 180, "k7lwh",     "100.0" )); 
            rgTemp.Add( new RepeaterDir(  52.87,  -1700, 180, "ww7psr",    "103.5" )); 
                                                                          
            rgTemp.Add( new RepeaterDir( 146.96,   -600, 180, "ww7psr",    "103.5" )); 
            rgTemp.Add( new RepeaterDir( 146.82,   -600, 120, "k7led",     "103.5" ));
            rgTemp.Add( new RepeaterDir( 145.49,   -600, 180, "k7lwh",     "103.5" ));
            rgTemp.Add( new RepeaterDir( 146.92,   -600, 120, "wa7dem",    "123.0" ));
            rgTemp.Add( new RepeaterDir( 147.16,   +600, 120, "w7mir",     "146.2" ));
            rgTemp.Add( new RepeaterDir( 147.34,   +600, 120, "k6rfk",     "100.0" ));
            rgTemp.Add( new RepeaterDir( 147.08,   +600, 120, "w7wwi",     "110.9" ));
            rgTemp.Add( new RepeaterDir( 146.4125, +600, 180, "k7lwh/c",   "103.5" )); // Dstar
            rgTemp.Add( new RepeaterDir( 146.62,   -600, 120, "ww7ra",     "103.5" ));
            rgTemp.Add( new RepeaterDir( 145.43,   -600, 120, "kd7wdg",    "88.5"  ));
            rgTemp.Add( new RepeaterDir( 145.33,   -600, 120, "k7nws",     "179.9" ));
            rgTemp.Add( new RepeaterDir( 146.900,  -600, 120, "w7srz",     "103.5" ));
            rgTemp.Add( new RepeaterDir( 147.240,  -600, 120, "k7sye",     "123"   ));
            rgTemp.Add( new RepeaterDir( 444.6375, 5000, 120, "wa7hjr/b"           ));
            rgTemp.Add( new RepeaterDir( 444.650,  5000, 120, "wa7hjr/rm", "131.8" ));
            rgTemp.Add( new RepeaterDir( 444.55,   5000, 120, "ww7sea",    "141.3" ));
            rgTemp.Add( new RepeaterDir( 441.075,  5000, 180, "k7lwh",     "103.5" ));
            rgTemp.Add( new RepeaterDir( 442.875,  5000, 120, "w7aux",     "103.5" ));
            rgTemp.Add( new RepeaterDir( 149.995,  -600, 120, "w7rnk/c"            ));
            rgTemp.Add( new RepeaterDir( 147.280,  +600, 120, "w7dk",      "103.5" ));
            rgTemp.Add( new RepeaterDir( 147.180,  +600, 120, "wa7law",    "103.5" ));
            rgTemp.Add( new RepeaterDir( 445.575,  5000, 120, "wa7law",    "103.5" ));

            _rgRepeatersInfo.Add( "k7lwh",  new RepeaterInfo( "Kirkland", "Lake Washington Ham Club", "http://www.lakewashingtonhamclub.org/" ));
            _rgRepeatersInfo.Add( "ww7psr", new RepeaterInfo( "Seattle", "Puget Sound Repeater Group", "http://psrg.org/" ));
            _rgRepeatersInfo.Add( "k7led",  new RepeaterInfo( "Tiger Mountain East", "Mike & Key ARC", "http://www.mikeandkey.org/index.php" ));
            _rgRepeatersInfo.Add( "wa7dem", new RepeaterInfo( "Granite Falls", "Snohomish Co. ACS/ARES" ));
            _rgRepeatersInfo.Add( "w7mir",  new RepeaterInfo( "Mercer island", "Mercer Island Radio Operators" ));
            _rgRepeatersInfo.Add( "k6rfk",  new RepeaterInfo( "Woodinville", "" ));
            _rgRepeatersInfo.Add( "w7wwi",  new RepeaterInfo( "Tiger Mtn East", "Sea-Tac Repeater Association" ));
            _rgRepeatersInfo.Add( "k7sye",  new RepeaterInfo( "Auburn", "Auburn Valley Repeater Group" ));
            _rgRepeatersInfo.Add( "wa7hjr", new RepeaterInfo( "Issaquah", "Tiger Mountain East", "http://wa7hjr.org/" ) );
            _rgRepeatersInfo.Add( "w7dk"  , new RepeaterInfo( "Tacoma", "Radio Club of Tacoma" ));
            _rgRepeatersInfo.Add( "wa7law", new RepeaterInfo( "Everett", "Snohomish County Hams Club", "http://www.wa7law.org/" ));

            foreach ( RepeaterDir oItem in rgTemp ) {
                _rgRepeatersIn .Add( oItem.Input, oItem );
                _rgRepeatersOut.Add( oItem.Output, oItem );
            }
        }

        /// <summary>
        /// This event comes in asynchronously on it's own thread. Queue up the message
        /// so that the foreground tread can pick it up.
        /// </summary>
        /// <remarks>TODO: Would be nice if we could post a message to the forground so that
        /// it spins up a task that can terminate once all the data has been pulled.</remarks>
        private void CiV_DataReceived( object sender, SerialDataReceivedEventArgs e ) {
            int iBytesWaiting = _oCiV.BytesToRead;
            if( iBytesWaiting > 0 ) {
                byte[] rgMsg = new byte[iBytesWaiting];

                try {
                    _oCiV?.Read( rgMsg, 0, iBytesWaiting );
                    _oMsgQueue.Enqueue( rgMsg );
                } catch( Exception oEx ) {
                    if( CiVErrorList.IsUnhandled( oEx ) )
                        throw;
                    LogError( "CiV", "CiV Data Receive Error" );
                }
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
        /// This is only needed for the old ".netlog" file type that was using 
        /// this. 
        /// </summary>
        public void ScanCallsigns() {
            Calls.Clear();
            List< string > rgCallSigns = new List<string>();

            try {
                foreach( Edit.Line oLine in Notes ) {
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
        /// In the StdLog mode we're retrieving the frequency whenever the user
        /// spins the dial. So all we need to do is grab the property and 
        /// add to the notes.
        /// </summary>
        /// <remarks>It's a little bit of a bummer that the properties are the
        /// human readable values. So if we change things like units on the
        /// power or frequency, it might become inconsistant with this function.</remarks>
        public void InsertFreqDateTime() {
            try {
                StringBuilder sbLine = new StringBuilder();
                DateTime   dtNow  = DateTime.UtcNow;
                string    strFreq = Properties[ (int)RadioProperties.Names.Frequency ].ToString();
                string   strPower = Properties[ (int)RadioProperties.Names.Power_Level ].ToString();
                string    strMode = Properties[ (int)RadioProperties.Names.Mode ].ToString();

                sbLine.Append( String.IsNullOrEmpty( strFreq ) ? "?mHz" : strFreq );
                sbLine.Append( '\t' ); // tab
                sbLine.Append( dtNow.ToString("HH:mm") );
                sbLine.Append( "z\t" ); // tab
                sbLine.Append( dtNow.ToShortDateString() );
                sbLine.Append( '\t' ); // tab
                sbLine.Append( String.IsNullOrEmpty( strPower ) ? "?%" : strPower );
                sbLine.Append( "w " );
                sbLine.Append( String.IsNullOrEmpty( strMode ) ? "?Mode" : strMode );

                Notes.LineAppend( sbLine.ToString() );
            } catch( Exception oEx ) {
                Type[] rgErrors = { typeof( ArgumentOutOfRangeException ),
                                    typeof( NullReferenceException ) };
                if( rgErrors.IsUnhandled( oEx ) )
                    throw;
            }
        }

        public bool Execute( Guid guidCmnd ) {
            if( guidCmnd == GlobalCommands.Play ) {
                try {
                    if( !_oCiV.IsOpen ) {
                        _oCiV.Open();
                    } else {
                        // This will get cleared, but leave it for now. Need to have some
                        // properties that have a function that gets called on clear.
                        Properties.ValueUpdate( RadioProperties.Names.Timer, "Already Opened" ); 
                    }
                    SendCommand( 0x03 );       // Read Frequency.
                    SendCommand( 0x1B, 0x00 ); // Read tone.
                    SendCommand( 0x04 );       // Read Mode.
                } catch( Exception oEx ) {
                    if( CiVErrorList.IsUnhandled( oEx ) )
                        throw;

                    Properties.ValueUpdate( RadioProperties.Names.Timer, "Open Error", Broadcast:true );
                    LogError( "Morse", oEx.Message, fShow:false );
                }
                return true; // Handled, even if ended in error.
            }
            if( guidCmnd == GlobalCommands.Stop ) {
                try {
                    if( _oCiV.IsOpen ) {
                        _oCiV.Close();
                        Properties.ValueUpdate( RadioProperties.Names.Timer, "Off" );
                    } else {
                        Properties.ValueUpdate( RadioProperties.Names.Timer, "Already Closed" );
                    }
                } catch( Exception oEx ) {
                    if( CiVErrorList.IsUnhandled( oEx ) )
                        throw;

                    Properties.ValueUpdate( 1, "Close Error" );
                    LogError( "Morse", oEx.Message, fShow:false );
                }
                return true; // Handled, even if ended in error.
            }
            if( guidCmnd == GlobalCommands.Pause ) {
                SendCommand( 0x03 );
            }

            return false;
        }
    }

    /// <summary>
    /// This subclass of the DocProperties let's us have static index values. This is advantageous because it
    /// allows us to re-arrange property values without scrambling their meaning. But it also means you can't
    /// use some kind of runtime forms generator since the indicies must have corresponding pre compiled enum's.
    /// </summary>
    public class RadioProperties : DocProperties {
        public enum Names : int {
            Radio_Link,
            Timer,
            Frequency,
            Callsign,
            Location,
            Group,
            Repeater_Tone,
            Rptr_Tone_Enable,
            Repeater_URL,
            Power_Level,
            Alternates,
            COM_Port,
            Address_Radio,
            Address_Controller,
            Mode,
            MAX
        }

        public RadioProperties( IPgBaseSite oSiteBase ) : base( oSiteBase ) {
        }

        public override bool InitNew() {
            if( !base.InitNew() ) 
                return false;

            for( int i=0; i<(int)Names.MAX; ++i ) {
                CreatePropertyPair();
            }

            LabelUpdate( Names.Radio_Link,         "Radio Link" );
            LabelUpdate( Names.Timer,              "Timer" );
            LabelUpdate( Names.Frequency,          "Frequency", new SKColor( red:0xff, green:0xbf, blue:0 ) );
            LabelUpdate( Names.Callsign,           "Callsign" );
            LabelUpdate( Names.Location,           "Location" );
            LabelUpdate( Names.Group,              "Group" );
            LabelUpdate( Names.Repeater_Tone,      "Repeater Tone" );
            LabelUpdate( Names.Rptr_Tone_Enable,   "Tone Enable" );
            LabelUpdate( Names.Repeater_URL,       "Repeater URL" );
            LabelUpdate( Names.Power_Level,        "Power Level" );
            LabelUpdate( Names.Alternates,         "Alternates" );
            LabelUpdate( Names.COM_Port,           "COM Port" );
            LabelUpdate( Names.Address_Radio,      "Radio Addr" );
            LabelUpdate( Names.Address_Controller, "Controller Addr" );
            LabelUpdate( Names.Mode,               "Mode" ); // Rx or Tx??

            // We'll actually initialize the serial port with these values! but they're not changable after that yet.
            ValueUpdate( Names.COM_Port,           "4" );    // While a property,
            ValueUpdate( Names.Address_Radio,      "0xa4" ); // Hex address.
            ValueUpdate( Names.Address_Controller, "0xe0" );

            return true;
        }

        public void LabelUpdate( Names eName, string strLabel, SKColor? skBgColor = null ) {
            LabelUpdate( (int)eName, strLabel, skBgColor);
        }

        public void ValueUpdate( Names eName, string strValue, bool Broadcast = true ) {
            ValueUpdate( (int)eName, strValue, Broadcast );
        }

        public void ValueClear( Names eName ) {
            ValueClear( (int)eName );
        }

        /// <summary>
        /// Override the clear to only clear the specific repeater information. If you want to 
        /// clear all repeater values. Do NOT call the base, which will clear everything.
        /// </summary>
        public override void ValuesEmpty() {
            ValueClear( Names.Frequency        );
            ValueClear( Names.Callsign         );
            ValueClear( Names.Location         );
            ValueClear( Names.Group            );
            ValueClear( Names.Repeater_Tone    );
            ValueClear( Names.Rptr_Tone_Enable );
            ValueClear( Names.Power_Level      );
            ValueClear( Names.Alternates       );
            ValueClear( Names.Repeater_URL     );

            //PropertyDoc.Raise_BufferEvent( BUFFEREVENTS.MULTILINE );
            RaiseUpdateEvent();
        }
    }
}
