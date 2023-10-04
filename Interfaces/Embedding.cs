using System;
using System.Collections.Generic;
using System.Collections;
using System.Text;
using System.Drawing; // BUG: this needs to go.
using System.IO;
using System.Runtime.InteropServices;
using System.Reflection;
using System.Linq;

using SkiaSharp;

namespace Play.Interfaces.Embedding {
    public delegate int FindPredicate<T>(T obj); // Crazy magic!! ^_^

    public static class FindStuff<T> {
        /// <summary>
        /// Find the line containing stream offset we are looking for.
        /// </summary>
        /// <returns>Matching index, else inverse of closest match.</returns>
        /// <exception cref="ArgumentOutOfRangeException" />
        /// <exception cref="NullReferenceException" />
        public static int BinarySearch( IList<T> rgLines, int iLow, int iHigh, FindPredicate<T> delCompare ) 
        {
            if( iLow > iHigh )
                throw new ArgumentException( "Low > High" ); // Our one out of bounds return condition.

            int iMid = iLow;
            int iCmp;

            while( iLow <= iHigh ) {
                iMid = iLow + (( iHigh - iLow ) >> 1 ); // Divide by 2.
                iCmp = delCompare( rgLines[iMid] );

                if(      iCmp > 0 )
                    iLow  = iMid + 1;
                else if( iCmp < 0 )
                    iHigh = iMid - 1;
                else
                    return iMid;
            }

            return ~iMid;
        }
    }

    // This probably belongs in my Play.Draw includes. Move it sometime.
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto) ]
    public class LOGBRUSH {
        public LOGBRUSH( UInt32 uiStyle, UInt32 uiColor, Int32 iHatch ) {
            _uiStyle = uiStyle;
            _uiColor = uiColor;
            _iHatch  = iHatch;
        }
        public UInt32 _uiStyle;
        public UInt32 _uiColor;
        public Int32  _iHatch;

        public static Color CreateColor( UInt32 uiColor ) {
            return( Color.FromArgb( 255, 
                                    (int)((0x000000ff & uiColor ) >>  0 ), 
                                    (int)((0x0000ff00 & uiColor ) >>  8 ), 
                                    (int)((0x00ff0000 & uiColor ) >> 16 ) ) );
        }
    };

	public class ImageResourceHelper {
		/// <summary>
		/// Get the specified resource from the currently executing assembly.
		/// </summary>
		/// <exception cref="InvalidOperationException" />
        /// <remarks>Consider changing the exception to ApplicationException</remarks>
		public static Bitmap GetImageResource( Assembly oAssembly, string strResourceName ) {
			try {
                // Let's you peep in on all of them! ^_^
                // string[] rgStuff = oAssembly.GetManifestResourceNames();

				using( Stream oStream = oAssembly.GetManifestResourceStream( strResourceName )) {
					return( new Bitmap( oStream ) );
				}
			} catch( Exception oEx ) {
				Type[] rgErrors = { typeof( NullReferenceException ), 
									typeof( ArgumentNullException ),
									typeof( ArgumentException ),
									typeof( FileLoadException ),
									typeof( BadImageFormatException ),
									typeof( NotImplementedException ) };
				if( !rgErrors.Contains( oEx.GetType() ) )
					throw;

				throw new InvalidOperationException( "Could not retrieve given image resource : " + strResourceName );
			}
		}
	}

    /// <summary>
    /// This class is an exact copy of the one from the win forms. But because I hate win forms I want to minimize
    /// what I require from it, in the hopes of dumping it some day.
    /// </summary>
    public enum KeyBoardEnum {
        Empty = 0,

        Modifiers = -65536, // The bitmask to extract modifiers from a key value.
        
        Shift = 65536,
        Control = 131072,
        Alt = 262144,
    }

    public struct ColorMap {
        public readonly string  _strName;  // Like "function" describing the element.
        public readonly string  _strValue; // the human readable value based on the program defs.
        public readonly SKColor _sColor;   // the actual color.

        public ColorMap( string strName, string strValue, SKColor sColor ) {
            _strName  = strName;
            _strValue = strValue;
            _sColor   = sColor;
        }

        public override string ToString() {
            return _strName;
        }
    }

    public enum DocumentPosition 
    {
        TOP    = 0,
        BOTTOM = 1,
        INSIDE = 2,
    }

    public enum ScrollEvents
    {
        SmallDecrement = 0,
        SmallIncrement = 1,
        LargeDecrement = 2,
        LargeIncrement = 3,
        ThumbPosition = 4,
        ThumbTrack = 5,
        First = 6,
        Last = 7,
        EndScroll = 8,
    }

    // Defining events in this manner, we can add to the list and not break everyone
    // versus adding new methods to our interface.
    public enum BUFFEREVENTS : int {
        MULTILINE       = 1, // Multiple lines edited and/or inserts
        SINGLELINE      = 2, // Basically character edit on a single line
        FORMATTED       = 4,
        WORDSUPDATED    = 8,
        LOADED		    = 16,
        CLEARFORMATTING = 32
		//, CLOSED (DISPOSED)
    }

    public delegate void BufferEvent( BUFFEREVENTS eEvent );

    public interface IReadableBag<T>  {
        int  ElementCount { get; } 
        T this[int index] { get; }
    }

	[Obsolete("replace with exception handling.")]
    public enum GetMappedGrammerErrors {
        OK,
        NotMappedExtn,         // No file extn mappings!
        NotMappedLang,         // Not in the list of language types.
        NotLoadable,           // Couldn't load grammar.
        NoGrammars,            // There are no grammar's in the list!
        UnrecognizedStreamType // The stream type we don't support.
    }

    public interface IPgGrammers {
		/// <exception cref="FileNotFoundException" />
		/// <exception cref="ArgumentNullException" />
        object GetGrammer( string strName );
    }

    /// <summary>
    /// Hosts using our grammers must implement this interface for the grammar loader.
    /// The grammars can use this site interface when hosting states. We could remove the
	/// need for this by adding these accessors to the IPgStandardUI.
    /// </summary>
    public interface IPgGrammarSite {
        int AddColor     ( string strName, string strValue );
        int GetColorIndex( string strName );
    }

	public interface IPgParent {
		IPgParent Parentage   { get; }
	    IPgParent Services    { get; } // App level services.
        IPgParent TopWindow   { get => Parentage.TopWindow; }
	  //string    Identifer   { get; } // something we can use for error messages.
	}

    public enum EditorShowEnum {
        SILENT,
        [Obsolete]BRINGTOTOP,
        FOCUS,
    }
    
    public interface IPgMainWindow {
        public struct PgDisplayInfo {
            public SKPointI pntSize;
            public SKPoint  pntDpi;

            public PgDisplayInfo() {
                pntSize = new SKPointI( 1920, 1080 );
                pntDpi  = new SKPoint ( 96, 96 );
            }
        }

        int DocumentShow( string strFileName, Guid guidViewType, bool fShow );

        PgDisplayInfo MainDisplayInfo { get; }
    }

    /// <summary>
    /// Pretty much any complex object, such as a Document or a Window must have access to
	/// this interface from it's parent. Keep it light weight since loads of people implement it!
    /// </summary>
    public interface IPgBaseSite {
        void      Notify( ShellNotify eEvent );
		IPgParent Host { get; }
        void      LogError( string strMessage, string strDetails, bool fShow = true );
    }

    /// <summary>
    /// 1/19/2016, Turns out this is a real problem. Making an object Load via StreamReader
    /// makes it totally difficult to use for anything but file operations because
    /// of the CurrentEncoding member is not available on StringReader for oiconne. 
    /// For example: embedding in XML file is a pain of reading the string into a
    /// MemoryStream via a StreamWriter then handing a StreamReader to the document 
    /// to load from. So instead just I implement this interface for the document 
    /// object, so it can display file status. However a document object should work
    /// just fine if its host does not supply this interface. Views should never need this,
    /// because their data is generally embedded in a text or xml stream.
    /// </summary>
    public interface IPgFileSite {
        FILESTATS FileStatus   { get; }
        Encoding  FileEncoding { get; }
		string    FilePath     { get; } // Path only.
        string    FileBase     { get; } // File name plus extention. No path.
    }

    public enum FILESTATS {
        READONLY,
        WRITEONLY,
        READWRITE,
        UNKNOWN
    }

    // TODO: Add image for icon in menu!! ^_^
    public interface IPgViewType {
        Guid   ID   { get; }
        string Name { get; }
    }

    public class PgDocumentDescriptor {
        public string         FileExtn   { get; }
        public Type           StgReqmnt  { get; }
        public byte           Priority   { get; }
        public IPgController2 Controller { get; }

        public PgDocumentDescriptor( string strExtn, Type oStg, byte bPri, IPgController2 oOwner ) {
            FileExtn   = strExtn ?? throw new ArgumentNullException( "File Extention arg" );
            Controller = oOwner  ?? throw new ArgumentNullException( "Controller arg" );
            StgReqmnt  = oStg    ?? throw new ArgumentNullException( "Storage Requirement" );
            Priority   = bPri;
        }

        /// <remarks>
        /// The Suitability() call generates our object, only the Priority value is comparable.
        /// The rest are descriptors of the object and are not comparible!!
        /// </remarks>
        /// <returns></returns>
        public int CompareTo( PgDocumentDescriptor oOther ) {
            return this.Priority - oOther.Priority;
        }
    }

    public interface IPgController2 {
        IDisposable CreateDocument( IPgBaseSite oSite, string strExtension );

		/// <exception cref="InvalidOperationException" />
		/// <exception cref="ArgumentException" />
		/// <exception cref="ArgumentNullException" />
		/// <exception cref="InvalidCastException" />
        IDisposable CreateView    ( IPgViewSite oViewSite, object oDocument, Guid guidViewType );

        string      PrimaryExtension { get; }
        PgDocumentDescriptor Suitability( string strExtension );
    }

    //public enum PLAYORDER {
    //    SEQUENTIAL = 0,
    //    RANDOM     = 1,
    //    SOLO       = 2,
    //}

    public static class GlobalCommands {
        // Clipboard functions
        public static readonly Guid Cut           = new Guid( "{77979857-142B-438E-81D7-A43EC42C7C03}" );
        public static readonly Guid Copy          = new Guid( "{A5D74A2C-E8E1-40C7-9667-677E0AB07BEC}" );
        public static readonly Guid Paste         = new Guid( "{D158B90D-6F05-4076-8A04-9D5654F1D7C5}" );
        public static readonly Guid PasteAsText   = new Guid( "{50DD25B0-143A-49F8-927D-7336F435D7D6}" );
        public static readonly Guid PasteAsImg    = new Guid( "{AC04A6FE-B564-483D-8E8D-C7D913B48934}" );
        public static readonly Guid PasteAsBase64 = new Guid( "{A284DCA5-94F6-4066-B480-9151B3AA6611}" );

        // Generic Edit insert file: link/embedding for the app to decide.
        public static readonly Guid Insert        = new Guid( "{1D8E24B1-3496-4701-BB32-46E3F0E5DDD2}" );

        // Generic commands
        public static readonly Guid Undo          = new Guid( "{DF4F0393-493D-4226-8472-1B72ED4DD9DB}" );
        public static readonly Guid SelectAll     = new Guid( "{3C87D970-1B40-41A8-B31E-4E858AF62FB1}" );
		public static readonly Guid SelectOff	  = new Guid( "{7e143d1d-3a6c-413f-8fa2-393536d7e450}");
        public static readonly Guid Delete        = new Guid( "{1D49B01A-7135-44F6-B8CD-1ACDEB0B926A}" );
		public static readonly Guid Save		  = new Guid( "{2E612615-3708-43C6-A16C-C98DE145C664}" );
		public static readonly Guid SaveAs		  = new Guid( "{E81A62D5-30AA-4703-83D7-903BD7824BB2}" );

        // Navigation
        public static readonly Guid StepUp     = new Guid( "{6AD95E80-6938-4D4F-A9F8-AB43C0561E0E}" );
        public static readonly Guid StepDown   = new Guid( "{397220E3-8FC0-4DDF-86C3-8E2F499313E4}" );
        public static readonly Guid StepLeft   = new Guid( "{F36DD8C6-225D-4B81-A74B-038F58C160E8}" );
        public static readonly Guid StepRight  = new Guid( "{43015BB5-0E21-4DFC-B670-AC4DA7BF4CDE}" );
        public static readonly Guid JumpNext   = new Guid( "{D13342D1-4906-4564-8413-49765DA2424B}" );
        public static readonly Guid JumpPrev   = new Guid( "{1274901F-E3CF-40B1-A30E-16053FF0270D}" );
        public static readonly Guid JumpParent = new Guid( "{99FA07A0-C146-4515-A24F-45EE1127AA8B}" );

        // music/photo players.
        public static readonly Guid Play        = new Guid( "{1A8B2D6B-46BE-4C7E-AC83-A83CAF342885}" );
        public static readonly Guid Stop        = new Guid( "{1491A12A-CA2D-4B79-99F7-4D18F980861E}" );
        public static readonly Guid Pause       = new Guid( "{FB5BCFF2-7D58-4A65-80B8-489CD5016A25}" );
		public static readonly Guid PlayingShow = new Guid( "{6553A578-14E7-4478-8EE8-1710F162EB28}" );
		public static readonly Guid VolumeUp    = new Guid( "{3ACE5890-D966-4C18-BB8B-E7BFA629184E}" );
		public static readonly Guid VolumeDown  = new Guid( "{BD5C7FAC-E84D-4BA6-90BA-F30ABB1AD946}" );
		public static readonly Guid PlayToggle  = new Guid( "{9CE4F07A-B608-4C80-962D-7737F2412F6A}" );

        // text 
        public static readonly Guid EditWrapOn  = new Guid( "{F2B682DD-684E-4D68-BFA7-890C893F67C2}" );
        public static readonly Guid EditWrapOff = new Guid( "{88E72180-CFC9-441D-B106-07632D628EFA}" );
        public static readonly Guid ReadOnly    = new Guid( "{A2D8ED0F-6AC9-4606-91E0-5A501DDF7EDC}" );
        public static readonly Guid ReadWrite   = new Guid( "{09B051A5-5A5E-417F-B49B-56EDC1B23CF1}" );
    }

    public static class GlobalFonts {
        public static readonly Guid FontStdText = new Guid( "{6191E9C7-EF86-4101-BA74-A1A7118A0893}" );
        public static readonly Guid FontStdMenu = new Guid( "{FE111207-061D-4485-BE52-486522247DCD}");
    }

    // I don't think I need a GetTypeInfo() since I can derive the T by reflection. Here's
    // a snippit of what I think I need. 
    // See... http://stackoverflow.com/questions/1121834/finding-out-if-a-type-implements-a-generic-interface
    // public static Type GetCommandType(Type type)
    // {
    //   if ( type.IsInterface && type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IPgCommand<> )
    //   {
    //      return( null );
    //   }
    //   return type.GetGenericArguments().First(); // generic arguments for the original interface.
    // }

    public interface IPgTextStorage {
        TextWriter StreamWrite( string strName, bool fCreate );
        TextReader StreamRead ( string strName );

        IEnumerator<string> EnumStreams { get; }
        bool                DestroyStream( string strName );
        FILESTATS           Stat { get; }
    }
    
    public static class GlobalDecorations {
        public static Guid Properties  = new Guid( "{1509A246-5CB0-41B1-A6D2-572D38EEC9C5}" );
        public static Guid Syntax      = new Guid( "{7FF3A2FC-459D-49B1-A2C1-B01853DDE2F0}" );
        public static Guid Productions = new Guid( "{266BE614-5336-4B45-A132-43640E659F69}" );
        public static Guid Outline     = new Guid( "{64EC31FE-F28E-49A6-A12A-9194214DD0D6}" );
		public static Guid Options     = new Guid( "{6AECF17A-D91B-452F-9B67-840144446DDB}" );
        public static Guid ToolIcons   = new Guid( "{83F0CB89-16BC-4DA8-8A79-A6F13DF57DA9}" );
    }

    /// <summary>
    /// Split this out from the IPgCommandView so we can attach this interface to documents. Super handy!!
    /// </summary>
    public interface IPgCommandBase {
        bool     Execute ( Guid sGuid );
    }

    /// <summary date="12/18/2015" >
    /// Instead of having IPgClipboard & IPgPlay & IPgAdornment, this is a more versatile interface
    /// for view objects responding to user input.
	/// </summary>
    public interface IPgCommandView : IPgCommandBase {
        string   Banner{ get; } 
        SKBitmap Icon  { get; }
        Guid    Catagory { get; } // This is the View Type guid. Only used in one place. See if I can factor this out.
        object  Decorate( IPgViewSite oBaseSite, Guid sGuid );
    }

    public interface IPgPlayStatus {
        bool    IsPlaying        { get; }
        SKColor BusyLight        { get; }

        int     PercentCompleted { get; }

    }

	/// <summary date="4/14/2019" >
	/// Views that support this interface will have a tools menu in the shell. Still working on this idea.
	/// </summary>
	public interface IPgTools {
		int    ToolCount { get; }
		string ToolName( int iTool );
		Image  ToolIcon( int iTool );
		int    ToolSelect { get; set; }
	}

    public delegate void ToolEvent( object sender, int iIndex );

    public interface IPgTools2 : IPgTools {
        event ToolEvent ToolSelectChanged;
    }

    public enum StdUIColors : int {
        BGWithCursor = 0,     // Unselected text bg w/ cursor. (Light grey)
        BGSelectedFocus,      // Text BG when selected and focused.
        BGSelectedBlur,       // Text BG when selected and unfocused.
        BG,                   // Text background. (White)
		BGReadOnly,           // Text background readonly. Std bg color for all of window.
        Text,                 // Color of text elements.
        TextSelected,         // Selected text color.
		MusicLine,            
		MusicLinePaused,      
        TitleBoxBlur,         // Unfocusted title and grab bar.
        TitleBoxFocus,        // Focused title and grab bar.
        BGNoEditText,         // Slightely darker than std bg color.
        BGSelectedLightFocus, // For image bg in focused center frame.
        Max
    };

    // A copy of popular keys from windows forms.
    public enum CommandKey : int {
        Tab = 9,
        Enter = 13,
        Return = 13,
        Escape = 27,
        PageUp = 33,
        PageDown = 34,
        End = 35,
        Home = 36,
        Left = 37,
        Up = 38,
        Right = 39,
        Down = 40,
        Print = 42,
        Insert = 45,
        Delete = 46,
        Find   = 300
    }

	public enum DragMode {
		FreeStyle,
		FixedRatio,
		FixedSize
	}

	public interface IPgStandardUI {
        [Obsolete]Font   FontStandard { get; }
        [Obsolete]UInt32 ColorStandardPacked( StdUIColors eColor );
	}

    public interface IPgViewNotify  {
        void NotifyFocused  ( bool fSelect );
        bool IsCommandKey   ( CommandKey ckCommand, KeyBoardEnum kbModifier );
        bool IsCommandPress ( char cChar );
    }

	public enum ShellNotify {
		DocumentDirty,
		BannerChanged,
		ToolChanged,
		ToolCollectionChanged,
        MediaStatusChanged
	}

	public interface IPgViewSite : IPgBaseSite {
		IPgViewNotify EventChain{ get; }
	}

    public interface IPgShellSite {
        object AddView( Guid guidViewType, bool fFocus );
        IEnumerable<IPgCommandView> EnumerateSiblings { get; }
        void   FocusMe();
        void   FocusCenterView();
        void   FocusTo( IPgCommandView oView );

        uint   SiteID { get; }
    }

    public interface IPgScheduler {
        IPgRoundRobinWork CreateWorkPlace();
	  //IPgRoundRobinWork CreateWorkDummy();
    }

	public enum WorkerStatus {
		BUSY,
		FREE,
		PAUSED,
		NOTIMPLEMENTED
	}

	public interface IPgAnonymousWorker {
		bool Execute( Guid guidCommand );
        void Stop();
		void Start( long iWaitInMs );
		void Pause();

		WorkerStatus Status { get; }
	}

    public interface IPgRoundRobinWork : IPgAnonymousWorker {
        void Queue( IEnumerator<int> oEnum, long iWait );
    }

	public interface IPgMorseWorkerCreater {
		IPgAnonymousWorker CreateMorseWorker( IEnumerable<char> oText );
	}

    public interface IPgSave {
        bool IsDirty { get; }
    }

    public interface IPgSave<T> : IPgSave {
        bool Save( T oStream );
    }

    /// <summary>
    /// So in the case of our directory browsers, it doesn't make sense to allow
    /// Save( string dir ) to save whatever was was being done to a DIFFERENT dir. 
    /// </summary>
    public interface IPgSaveURL : IPgSave {
        bool Save();
    }

    /// <summary>
    /// 1/25/2016
    /// I keep going back and forth on InitNew() having a parameter or not. But think of it like this,
    /// If we're using this interface on document types... then I either have a file, or I want to
    /// create one from scratch. So even something as complicated as my text parsing system can boot
    /// as long as I know the text type I'm going to create. All the right objects get picked and then
    /// InitNew() get's them all connected up and running w/o the need of a parameter.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public interface IPgLoad {
        bool InitNew();
    }

    public interface IPgLoad<T> : IPgLoad {
        bool Load( T oStream );
    }

    public interface IPgLoadURL : IPgLoad {
        bool   LoadURL( string strLocation );
        string CurrentURL { get; }
    }

    public interface IPgLoadTwoStage<T> {
        bool Load( T oStream );
        bool Bind( T oStream );
    }

    public enum SHOWSTATE  {
        Active,
        Focused,
        Inactive
    }

    /// <summary>
    /// The new and better way to go. Mainly because I want to be able to hide an object but
    /// not affect messages being sent about it's visibility since those are going to get sent
    /// to the object as long as it is in the food chain.
    /// </summary>
    public interface IPgVisibleObject2 {
        SHOWSTATE ShowAs { get; set; }
        bool      Hidden { get; set; }
    }

    /// <summary>
    /// This is a little different than standard clipboard data, in that this is the
    /// intent of the operation, regardless of what's actually on the clipboard. Eg.
    /// FileDrop --> Text   : List the files
    /// FileDrop --> Base64 : List the files as data URI.
    /// Default allows the view to decide how to interrogate the data object. For example
    /// Text viewer is going to look for text first, Img viewer look for images.
    /// </summary>
    public static class ClipboardOperations {
        public static Guid Default  { get { return( new Guid( "{BB9562AA-82CE-44C1-895A-E14A26E4158A}" ) ); } }
        public static Guid FileDrop { get { return( new Guid( "{0529DFE4-CBF1-4002-9EFD-B46A0F8EA6A3}" ) ); } }
        public static Guid Text     { get { return( new Guid( "{2706F839-60B3-4D87-BC54-5EA2D2CC0084}" ) ); } }
        public static Guid Base64   { get { return( new Guid( "{9B7F654C-1FF3-4D0C-96CA-7749993D0FFC}" ) ); } }
    }

    public struct TextLocation {
        public int _iLine;
        public int _iOffset; 

        public TextLocation( int iLine, int iOffset ) {
            _iLine   = iLine;
            _iOffset = iOffset;
        }
    }

    public enum EDGE {
        LEFT   = 0,
        TOP    = 1,
        RIGHT  = 2,
        BOTTOM = 3
    }

    public delegate void Navigation( int iLine );

	/// <summary>
	/// Might make this the base class of CaretPosition in the future for
	/// tighter integration witht the editor line positions and carats.
	/// </summary>
	public class TextPosition {
		public int Line   { get; }
		public int Offset { get; }

		public TextPosition( int iLine, int iOffset ) {
			Line   = iLine;
			Offset = iOffset;
		}
	}

    /// <summary>
    /// Basically any kind of text editing object which uses the Line class. 
    /// This interface allows you to work with the FindWindow.
    /// </summary>
    public interface IPgTextView : IPgParent { 
        TextPosition Caret { get; }

        void ScrollTo( EDGE eEdge ); 
        void ScrollToCaret();

        bool SelectionSet( int iLine, int iOffset, int iLength );
        void SelectionClear();

		// I'd love to return an enumerable but then I end up having to
		// have the return type, and Line is specific to the editor.
		// Then the ImageViewer and MusicPlayer need to pull in the Edit
		// namespace in places they don't really have to. They use the
		// text editor, but they don't have to.
        object DocumentText { get; }
    }

    public delegate void ViewChanged( object oView );

    /// <summary>
    /// So far, I've been able to derive all data streams from this
    /// abstract class with no problems. I think this is better than 
    /// changing to an interface since the object behavior is being 
    /// exported and not imported from the user.
    /// </summary>
    /// <typeparam name="T">Type of data to be streamed.</typeparam>
    public abstract class DataStream<T>
	{
        public abstract bool InBounds(int p_iPos); // less than EOF.
		public abstract int Position { get;	set; }
		public abstract T this [int iPos ] { get; }
		public abstract string SubString( int iPos, int iLen ); 
	}

    public interface IPgDataStream<T> {
        bool InBounds(int p_iPos); // less than EOF.
		int  Position { get; set; }
		T    this [ int iPos ] { get; }
        T    Read(); // Read a single element and advance pointer.
	}

    public static class ErrorReporting {
		/// <summary>
		/// The new improved error handler. We don't actually handle exceptions we
		/// can't handle. ^_^;;
		/// </summary>
		public static bool IsUnhandled( this Type[] rgErrors, Exception oEx ) {
            foreach( Type curType in rgErrors ) {
                if( oEx.GetType() == curType ) {
                   return( false );
                }
            }

			return( true );
		}
    }

    public enum ToneType {
        Tone, TSQL, DTCS, CSQL
    }

    public interface IPgCiVEvents {
        void CiVError             ( string strError );
        void CiVFrequencyChange   ( bool fRequest, int iFrequency );
        void CiVModeChange        ( string strMode, string strFilter );
        void CiVRepeaterToneReport( double dblTone, ToneType eType );
        void CiVRepeaterToneEnable( bool fValue );
        void CiVPowerLevel        ( int iLevel ); // 0 - 255;
    }

    public class ViewType : IPgViewType {
        readonly protected Guid   _guidID;
        readonly protected string _strName;

        public ViewType( string strName, Guid guidID ) {
            _guidID  = guidID;
            _strName = strName;
        }

        public string Name {
            get { return( _strName ); }
        }

        public Guid ID {
            get { return( _guidID ); }
        }
    }

    /// <summary>
    /// TODO: It would be very cool if controllers read from the session file to assign suitability.
    /// </summary>
    abstract public class Controller :
        IPgController2,
        IEnumerable<IPgViewType>
    {
        protected readonly List<string> _rgExtensions = new List<string>();

		public Controller() {
		}

        public abstract IDisposable CreateDocument( IPgBaseSite oSite, string strExtension );
        public abstract IDisposable CreateView( IPgViewSite oViewSite, object oDocument, Guid guidViewType );

        public virtual IEnumerator<IPgViewType> GetEnumerator() {
            yield break; // this is an empty enumerator.
        }

        IEnumerator IEnumerable.GetEnumerator() {
            return( GetEnumerator() );
        }

        public string PrimaryExtension {
            get {
                if( _rgExtensions.Count > 0 )
                    return( _rgExtensions[0] );

                return( string.Empty );
            }
        }

        /// <summary>
        /// By default the most suitable extension is the primary extension. All others are 1/2
        /// as effective. That lets each file have at least one primary document associated with
        /// it. After that, I'll have to add code to deal with equivalent suitibilities.
        /// </summary>
        public virtual PgDocumentDescriptor Suitability(string strExtension) {
            if( string.Compare( PrimaryExtension, strExtension ) == 0 )
                return new PgDocumentDescriptor( strExtension, typeof( IPgLoad<TextReader> ), (byte)255, this );

            return new PgDocumentDescriptor( strExtension, 
                                             typeof( IPgLoad<TextReader> ), 
                                             _rgExtensions.Contains( strExtension ) ? (byte)125 : (byte)0, 
                                             this );
        }
    }
}
