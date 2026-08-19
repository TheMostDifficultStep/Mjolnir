using System.Security;
using System.Text;
using System.Windows.Forms;

using SkiaSharp;

using Play.Edit;
using Play.Interfaces.Embedding;
namespace Monitor {
    public class ViewTerminal :
        EditWindow2,
        IPgCommandView 
    {
        public readonly static Guid GUID = new Guid( "{BE1E1F3D-6CE5-4FE2-9A52-EA2E5F72D3D5}" );
        public override string    Banner => "Simple Terminal";
		public override SKImage?  Icon { get; protected set; }
        public override Guid      Catagory => GUID;

        protected DocumentMonitor DocMon  { get; } 
        protected DocTerminal     DocTerm { get; }

        /// <summary>
        /// This object is a a bit weird since we want to send our keystokes
        /// to the CPU. Any return characters go straight into the buffer
        /// and we get an update call. We don't add characters straight into
        /// our buffer! O.o
        /// </summary>
        public ViewTerminal(IPgViewSite oViewSite, DocTerminal oDocument) : 
            base(oViewSite, oDocument) 
        {
            // any cast failure bails us out and system fails window create gracefully.
            DocTerm = oDocument;
            DocMon  = (DocumentMonitor)DocTerm.Parentage; 

			try {
				Icon = DocMon.GetResource( "icons8-terminal-58.png" );
			} catch( InvalidOperationException ) {
			}
        }


        public override object? Decorate(IPgViewSite oBaseSite, Guid sGuid) {
            return null;
        }

        protected bool Save() {
            string? strPath = DocTerm.CheckLocation( fNewLocation:false );

            if( string.IsNullOrEmpty( strPath ) )
                return false;

            if( !DocTerm.Save( strPath ) )
                return false;

            DocTerm.Moniker = strPath;

            return true;
        }

        public override bool Execute(Guid sGuid) {
            //if( sGuid == GlobalCommands.Save ) {
            //    return Save();
            //}
            return false;
        }

        protected override void OnKeyPress(KeyPressEventArgs e) {
            // Don't pass to our base....
            if( IsDisposed )
                return;
            if( _oViewEvents.IsCommandPress( e.KeyChar ) )
                return;
            if( IsReadOnly )
                return;

            if( !char.IsControl( e.KeyChar ) ||
                e.KeyChar == '\r' ||
                e.KeyChar == '\b'    ) 
            {
                DocMon.TerminalKeyPress( e.KeyChar );
                e.Handled = true;
            }
        }
    }

    /// <summary>
    /// Hmmm... this is no replacement for a simple text editor. I think
    /// I might use the old Editor class instead of the multi column...
    /// Not sure what I had in mind here...
    /// </summary>
    public class DocTerminal : 
        Editor,
        IPgLoad<TextReader>
    {
        public Queue<byte> Buffer { get; } = new Queue<byte>();
        public string Moniker {get; set; } = string.Empty;

		protected Encoding _oEncoding = new UTF8Encoding( false, true ); // Don't emit BOM, tho I think it ignores me anyway.

        public DocTerminal(IPgBaseSite oSite) : base(oSite) {
        }

        public override bool InitNew() {
            if( !base.InitNew() )
                return false;

            _rgLines.Insert( 0, new TextLine( 0, string.Empty ) );
            CharacterCount( 0 );

            return true;
        }

        public override bool Load(TextReader oStream) {
            if( !base.Load( oStream ) ) 
                return false;

            // If empty insert an empty line.

            return true;
        }
        /// <summary>
        /// Appends a character at the end of the current last line.
        /// </summary>
        public void AppendChar( char cChar ) {
            //Line oLine;

            if( cChar == '\n' ) {
                int  iStart  = _rgLines.Count;
                Line oInsert = new TextLine( iStart, string.Empty );

                _rgLines.Insert( iStart, oInsert );

                Raise_AfterInsertLine( oInsert );

                CharacterCount( iStart );
                Raise_BufferEvent( BUFFEREVENTS.SINGLELINE );  
                return;
            }
            if( cChar == '\r' )
                return;

            Line oLine = _rgLines[ElementCount-1];
            
            // Tack the new character at the end of the line.
            oLine.TryAppend( cChar );

            Raise_MultiFinished();
            Raise_BufferEvent( BUFFEREVENTS.LOADED );  
        }

        public static readonly Type[] _rgFileErrors = { 
			typeof( ArgumentNullException ),
			typeof( ArgumentException ),
			typeof( NullReferenceException ),
			typeof( DirectoryNotFoundException ),
			typeof( IOException ),
			typeof( UnauthorizedAccessException ),
			typeof( PathTooLongException ),
			typeof( SecurityException ),
            typeof( InvalidOperationException ),
            typeof( NotSupportedException ),
            typeof( FileNotFoundException ) };

        /// <seealso cref="CheckLocation(bool)" />
        public bool Save( string strPath ) {
            bool fSaved = false;

            try {
                // Note: By default StreamWriter closes a stream when provided. Newer versions of .net provide leaveOpen flag.
                //       Let's just use streamwriter with filename direcly since we're not dealing with binary objects yet. 
                using( StreamWriter oWriter = new StreamWriter( strPath, false, _oEncoding ) ) {
                    fSaved = Save( oWriter );
					oWriter.Flush();
                }
            } catch( Exception oEx ) {
				if( _rgFileErrors.IsUnhandled( oEx ) )
					throw;

                LogError( "Couldn't save Terminal Screen" );

                return false;
            }

            // If I don't call this, then the session (if using) doesn't
            // wipe the astrisk off of the title. 
            return true;
        }

        /// <summary>
        /// Check if we either have a proper filename, or go out
        /// and get one.
        /// </summary>
        /// <remarks>To put this on the base or not? Or on a view?
        /// I think it makes since here since the file name is
        /// a singleton. And on this subclass since I'm not sure
        /// I should be allowing "side" saves in the general case?
        /// </remarks>
        public string? CheckLocation( bool fNewLocation ) {
            string? strLastPath = string.Empty;

            // If we've got a filename try that path first. 
            if( string.IsNullOrEmpty( Moniker ) || 
                string.IsNullOrEmpty( Path.GetFileNameWithoutExtension( Moniker ) ) )
                fNewLocation = true;
            else
                strLastPath = Path.GetDirectoryName( Moniker );

            if( fNewLocation == true ) {
                SaveFileDialog oDialog = new() {
                    InitialDirectory = strLastPath
                };
                oDialog.ShowDialog();

                if(  oDialog.FileName        == null || 
                     oDialog.FileName.Length == 0    || 
                    !oDialog.CheckPathExists ) 
                {
                    LogError( "Please supply a valid file name for your next Save request. ^_^;" );
                    return null;
                }

                return oDialog.FileName;
            }

            return strLastPath;
        }

    } // End DocTerminal

}
