using System;
using System.Collections.Generic;
using System.IO;

using Play.Interfaces.Embedding; 
using Play.Edit;
using Play.Forms;
using Play.Clock;
using Play.Integration;

namespace Mjolnir {
    public class ControllerForTopLevelWindows :
        Controller
    {
        protected readonly Program _oDocument;

        public ControllerForTopLevelWindows( Program oProgram ) {
			_oDocument = oProgram ?? throw new ArgumentNullException();
            _rgExtensions.Add( ".filedialog" );
            _rgExtensions.Add( ".mainwindow" );
        }
        
        public override IDisposable CreateDocument( IPgBaseSite oSite, string strFileExt) {
            if( string.Compare( strFileExt, ".clock", ignoreCase:true ) == 0 ) {
                return new DocumentClock( oSite );
            }

            return _oDocument;
        }

        public override IDisposable CreateView( IPgViewSite oBaseSite, object oDocument, Guid guidViewType ) {
            try {
                if( guidViewType == Program.Clock ) {
                    return new ViewClock( oBaseSite, oDocument as DocumentClock );
                }
				if( guidViewType == Program.FindDialog ) {
                    if( _oDocument.MainWindow == null )
                        throw new ApplicationException( "Main Window has not been created yet!" );
					return new FindWindow( oBaseSite, _oDocument.MainWindow ); // BUG: Maybe should get parent from basesite?...
                }
				if( guidViewType == Program.MainWin ) {
                    MainWin oMainWin = new MainWin( _oDocument );
					return oMainWin;
                }
            } catch( Exception oEx ) {
                Type[] rgErrors = { typeof( NullReferenceException ),
                                    typeof( InvalidCastException ),
                                    typeof( ArgumentNullException ) };
                if( rgErrors.IsUnhandled( oEx ) )
                    throw;

                throw new InvalidOperationException( "Could not create a view on this Shell top level window." );
            }

			throw new ArgumentOutOfRangeException( "Don't recognize top level window requested!" );
        }

        public override IEnumerator<IPgViewType> GetEnumerator() {
 	        yield return new ViewType( "Find",         Program.FindDialog  );
 	        yield return new ViewType( "Main Window",  Program.MainWin );
            yield return new ViewType( "Clock",        Program.Clock );
        }
    }

    /// <summary>
    /// This is an emergency controller for when we have no grammars and want a simple text editor.
    /// Using this in the Alerts since it must be initialized very early in the window sequence.
    /// Might be a nice default controller for the editor.
    /// </summary>
    public class ControllerForPlainText :
        Controller
    {
        protected readonly Program _oDocument;

        public ControllerForPlainText( Program oProgram ) {
			_oDocument = oProgram ?? throw new ArgumentNullException();
            _rgExtensions.Add( ".txt" );
        }
        
        public override IDisposable CreateDocument( IPgBaseSite oSite, string strFileExt) {
            return( new Editor( oSite ) );
        }

        public override IDisposable CreateView( IPgViewSite oBaseSite, object oDocument, Guid guidViewType ) {
            try {
                return( new EditWindow2( oBaseSite, (Editor)oDocument ) );
            } catch( Exception oEx ) {
                Type[] rgErrors = { typeof( NullReferenceException ),
                                    typeof( InvalidCastException ),
                                    typeof( ArgumentNullException ) };
                if( rgErrors.IsUnhandled( oEx ) )
                    throw;

                throw new InvalidOperationException( "Document must be a text editor type", oEx );
            }
        }
    }

	/// <summary>
	/// 1/4/2019 : This is kind of built backwards. My first music player efforts where built 
	/// before I had and integration project. Music doesn't need to be parsed out to be played. 
	/// For example an m3u file. BUT it's way more useful to parse and find music randomly an
	/// then play. So I'm not sure how this is going to sort out.
	/// Amusingly enough, the MusicWalker project, doesn't use the EditorWithParser or
	/// EditorWithMusic! So I think I can sort this out better.
	/// </summary>
    public class EditorWithParser : Editor { // was EditorWithMusic
        ParseHandlerText _oParseHandler;
		readonly Editor  _oDoc_Productions;

        public EditorWithParser( IPgBaseSite oSite ) : base( oSite ) {
			// This would be a great place for us to implement linked documents!!!
			_oDoc_Productions = new Editor( new DocSlot( this ) );
        }

        public ParseHandlerText ParseHandler {
            get { return( _oParseHandler ); }
            set { 
                _oParseHandler = value ?? throw new ArgumentNullException();
            }
        }

		public override bool ProductionsTrace {
			get { return( _oParseHandler.ProductionsTrace ); }
			set {  _oParseHandler.ProductionsTrace = value; }
		}

		public override BaseEditor Productions {
			get {
				return( _oDoc_Productions );
			}
		}

		public override bool InitNew() {
            try {
                if( !base.InitNew() )
                    return( false );

				if( !_oDoc_Productions.InitNew() )
					return( false );

                if( !_oParseHandler.InitNew() )
                    return( false );

				_oParseHandler.ProductionsEdit = _oDoc_Productions;
            } catch ( NullReferenceException ) {
                return( false );
            }
            return( true );
        }

        public override bool Load(TextReader oReader) {
            try {
                if( !base.Load(oReader) )
                    return( false );

				if( !_oDoc_Productions.InitNew() )
					return( false );

                if( !_oParseHandler.InitNew() )
                    return( false );

				_oParseHandler.ProductionsEdit = _oDoc_Productions;
            } catch ( NullReferenceException ) {
                return( false );
            }
            return( true );
        }

        public override void Dispose() {
            if( _oParseHandler != null )
                _oParseHandler.Dispose();
			if( _oDoc_Productions != null )
				_oDoc_Productions.Dispose();

            base.Dispose();
        }
	}

    /// <summary>
    /// Normally a controller will live in the document's namespace. The shell will create one to handle file
    /// types the document supports. But because my editor actually doesn't (or shouldn't) know much
    /// about the parser, I'm going to tie it together in the shell.
    /// </summary>
    public class ControllerForParsedText :
        Controller
    {
        protected readonly Program _oProgram;

        public ControllerForParsedText( Program oProgram, bool fDefaultExtensions = true ) {
            _oProgram = oProgram ?? throw new ArgumentNullException();
            
            // This is a little bogus loading from the config file since the user can modify it and
            // botch loading of types at startup.
			if( fDefaultExtensions ) {
				for( IEnumerator<string> oEnumExtn = _oProgram.EnumFileExtensions(); oEnumExtn.MoveNext(); ) {
					_rgExtensions.Add( oEnumExtn.Current );
				}
			}
        }
        
		protected virtual EditorWithParser CreateEditor( IPgBaseSite oSite ) {
			return( new EditorWithParser( oSite ) );
		}

        public override IDisposable CreateDocument( IPgBaseSite oSite, string strFileExt) {
            ExtensionMap oMap        = _oProgram.GetMapping( strFileExt );
            string       strMapError = null;

            if( oMap.IsEmpty ) {
                strMapError = "Could not find mapping for this file type: '" + strFileExt + "'. Trying '.txt' grammar.";
                oMap = _oProgram.GetMapping( ".txt" );
                if( oMap.IsEmpty )
                    throw new InvalidOperationException( "Couldn't find document type for this file extension." );
                
            }

            EditorWithParser oEditor = CreateEditor( oSite );

            if( strMapError != null )
                oEditor.LogError( "Could not find mapping for this file type: '" + strFileExt + "'. Trying '.txt' grammar." );

			try {
				// A parser is matched one per text document we are loading.
				oEditor.ParseHandler = new ParseHandlerText( oEditor, oMap._strGrammar );
			} catch( Exception oEx ) {
                Type[] rgErrors = { typeof( NullReferenceException ),
                                    typeof( InvalidCastException ),
                                    typeof( ArgumentNullException ),
									typeof( ArgumentException ),
									typeof( InvalidOperationException ),
									typeof( InvalidProgramException ) };
                if( rgErrors.IsUnhandled( oEx ) )
                    throw;

				oEditor.Dispose();
				throw new InvalidOperationException( "Couldn't create parse handler for given text.", oEx );
			}

            return( oEditor );
        }

        /// <summary>
        /// Since text only has one view type, this method is pretty simple.
        /// </summary>
        /// <returns></returns>
        public override IDisposable CreateView( IPgViewSite oBaseSite, object oDocument, Guid guidViewType ) {
            try {
                switch( guidViewType ) {
                    case var u when u == MainWin_Tabs.ViewType:
                        return new MainWin_Tabs( oBaseSite, (Editor)oDocument );
                     
                    case var r when r == EditWindow2.ViewType:
                    case var s when s == Guid.Empty:
                    default:
                        return new EditWindow2( oBaseSite, (Editor)oDocument );

                    case var t when t == EditWin.ViewType:
                        return new EditWin( oBaseSite, (Editor)oDocument );
                }
            } catch( Exception oEx ) {
                Type[] rgErrors = { typeof( NullReferenceException ),
                                    typeof( InvalidCastException ),
                                    typeof( ArgumentNullException ) };
                if( rgErrors.IsUnhandled( oEx ) )
                    throw;

				throw new InvalidOperationException( "Controller couldn't create view for M3U document.", oEx );
            }
        }

        public override IEnumerator<IPgViewType> GetEnumerator() {
 	        yield return new ViewType( "Default",   EditWindow2.ViewType );
            yield return new ViewType( "Old Style", EditWin    .ViewType );
            yield return new ViewType( "Test Tabs", MainWin_Tabs.ViewType );
        }

    }

    /// <summary>
    /// Parsing HTML is more complicated than simple text parsing since I have to spend extra
    /// effort building a document tree. So we'll split it out from normal text so the more
    /// complicated ParseHandlerHTML doesn't get used in the simpler case.
    /// </summary>
    /// <seealso cref="ParseHandlerHTML"/>
    /// <seealso cref="ParseHandlerText"/>
    public class ControllerForHtml : ControllerForParsedText
    {
        public ControllerForHtml(Program oProgram) : base(oProgram, false)
        {
            _rgExtensions.Add(".html");
            _rgExtensions.Add(".htm");
            _rgExtensions.Add(".xml");
            _rgExtensions.Add(".aspx");
            _rgExtensions.Add(".phree");
        }

        /// <summary>
        /// We're not using the file extension from the xml config program mappings because for 
        /// the extensions we support here we use the ParseHandlerHTML and not the standard text parser.
        /// Now it so happens this parser uses the text parser but differently than the 
        /// standard ParseHandlerText implementation.
        /// </summary>
        /// <param name="strFileExt">This parameter presently ignored. The extensions we
        /// support are hard coded in this controller.</param>
        /// <returns></returns>
        public override IDisposable CreateDocument(IPgBaseSite oSite, string strFileExt)
        {
            EditorWithParser oEditor = CreateEditor(oSite);

            try {
                // A parser is matched one per text document we are loading.
                oEditor.ParseHandler = new ParseHandlerHTML(oEditor);
            } catch (Exception oEx) {
                Type[] rgErrors = { typeof( NullReferenceException ),
                                    typeof( InvalidCastException ),
                                    typeof( ArgumentNullException ),
                                    typeof( ArgumentException ),
                                    typeof( InvalidOperationException ),
                                    typeof( InvalidProgramException ) };
                if (rgErrors.IsUnhandled(oEx))
                    throw;

                oEditor.Dispose();
                throw new ApplicationException( "Couldn't create parse handler for shell text editor.", oEx );
            }

            return (oEditor);
        }
    }

    public class ControllerForSearch :
        Controller
    {
        public ControllerForSearch() {
            _rgExtensions.Add(".search");
        }

        public override IDisposable CreateDocument( IPgBaseSite oSite, string strExtension ) {
            return new FormsEditor( oSite );
        }

        public override IDisposable CreateView( IPgViewSite oViewSite, object oDocument, Guid guidViewType ) {
            throw new NotImplementedException();
        }
    }

    public class ControllerForResults :
        Controller
    {
        public ControllerForResults() {
            _rgExtensions.Add(".results");
        }

        public override IDisposable CreateDocument( IPgBaseSite oSite, string strExtension ) {
            return new Editor( oSite );
        }

        public override IDisposable CreateView( IPgViewSite oViewSite, object oDocument, Guid guidViewType ) {
            return new EditWindow2( oViewSite, (Editor)oDocument, fReadOnly:true );
        }
    }
}
