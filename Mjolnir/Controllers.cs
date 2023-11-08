using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

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

        /// <summary>
        /// I really want this to load in the event there is NO doc type matching.
        /// regardless of file extension or extended matching extensions.
        /// </summary>
        public override PgDocDescr Suitability(string strExtension) {
            if( string.Compare( PrimaryExtension, strExtension, ignoreCase:true ) == 0 )
                return new PgDocDescr( strExtension, typeof( IPgLoad<TextReader> ), 255, this );

            return new PgDocDescr( strExtension, 
                                   typeof( IPgLoad<TextReader> ), 
                                   126, 
                                   this );
        }
    }

    /// <summary>
    /// Moved this out of the base editor project and stuck it out here since only primary 
    /// embedded documents in the shell really need this behavior.
    /// </summary>
	public class EditWinProductions : EditWindow2 {
		readonly BaseEditor _oDocumentOverride;

		public EditWinProductions( IPgViewSite oBaseSite, BaseEditor oDocument ) : 
			base( oBaseSite, oDocument.Productions, false, false ) 
		{
			_oDocumentOverride = oDocument ?? throw new ArgumentNullException( "Weird happenings in EdiwWinProperties!" );

			try {
				oDocument.ProductionsTrace = true;
			} catch( NotImplementedException oEx ) {
                // BUG: This might be too severe of a responce in a hetrogeneous documents open
                //      situation. Some windows might support this and some might not and we
                //      get annoying mssages.
				throw new ArgumentException( "Document must support productions to use this window!", oEx );
			}
		}

		/// BUG: I'm noticing this is getting called really late.
		/// I'm probably not dealing with closing addornments properly.
		protected override void Dispose(bool disposing) {
			if( disposing ) {
				_oDocumentOverride.ProductionsTrace = false;
			}
			base.Dispose(disposing);
		}
	}

    public class EditPresentation : EditWindow2 {
        DocProperties _oDoc_Properties;

        public EditPresentation(IPgViewSite oSiteView, BaseEditor p_oDocument, bool fReadOnly = false, bool fSingleLine = false) : 
            base(oSiteView, p_oDocument, fReadOnly, fSingleLine) {

            _oDoc_Properties = new DocProperties( new DocSlot( this ) );
        }

        protected override bool InitInternal() {
            // Props must be initialized first since the base will call on the bulk constructor
            // to make it's window properties, and it'll assert we're already loaded on init!!
            if( !_oDoc_Properties.InitNew() )
                return false;

            if( !base.InitInternal() )
                return false;

            return true;
        }

        protected IPgFormBulkUpdates CreateBulkLoader() {
            return new DocProperties.Manipulator( _oDoc_Properties );
        }

		protected override void DecorNavPropsInit() {
			using( IPgFormBulkUpdates oBulk = CreateBulkLoader( ) ) {
                foreach( EditNavigation eNav in Enum.GetValues( typeof( EditNavigation ) ) ) {
                    if( (int)eNav != oBulk.AddProperty( eNav.ToString() ) ) {
                        throw new InvalidProgramException( "Editor nav props missaligned" );
                    }
                }
				oBulk.SetLabel( (int)EditNavigation.Character_Count, "Character Count" );
				oBulk.SetLabel( (int)EditNavigation.File_Encoding,   "File Encoding" );
			}
		}

		/// <remarks>Note that List<T> throws ArgumentOutOfRangeException for the same cases 
		/// where arrays use IndexOutOfRangeException. It's a bitch I know.</remarks>
        protected override void DecorNavigatorUpdate( NavigationSource eSource, ILineRange oCaret ) {
            StringBuilder sbBuild = new StringBuilder();

            try {
                int iLine          = oCaret.At + 1;
                int iIndex         = oCaret.Offset;
                int iLineCharCount = oCaret.Line.ElementCount;
                int iGlyphCount    = 0;

                foreach( IPgGlyph oGlyph in _oCacheMan.EnumGrapheme( oCaret ) ) {
                    if( iGlyphCount++ > 0 )
                        sbBuild.Append( ", " );
                    sbBuild.Append( "0x" );
                    sbBuild.Append( oGlyph.CodePoint.ToString("x4") );
                }

                using (IPgFormBulkUpdates oBulk = CreateBulkLoader() ) {
                    oBulk.SetValue( (int)EditNavigation.Line,            iLine.ToString()  + " of " + _oDocument.ElementCount.ToString());
                    oBulk.SetValue( (int)EditNavigation.Column,          iIndex.ToString() + " of " + iLineCharCount.ToString());
                    oBulk.SetValue( (int)EditNavigation.Character,       sbBuild.ToString() );
                    oBulk.SetValue( (int)EditNavigation.Selection,       SelectionCount.ToString());
                    oBulk.SetValue( (int)EditNavigation.Character_Count, _oDocument.Size.ToString());
                    oBulk.SetValue( (int)EditNavigation.File_Encoding,   _oDocument.FileEncoding + " (" + _oDocument.FileStats + ")");
                }
			} catch( Exception oEx ) {
				Type[] rgErrors = { typeof( NullReferenceException ),
									typeof( IndexOutOfRangeException ),
									typeof( ArgumentOutOfRangeException ),
                                    typeof( ArgumentException ) };
				if( rgErrors.IsUnhandled( oEx ) )
					throw;

				LogError( "Nav properties", "Problem prepping data" );
            }
        }

        public override object Decorate( IPgViewSite oBaseSite, Guid sGuid ) {
			try {
				if ( sGuid.Equals( GlobalDecorations.Productions ) ) {
					return new EditWinProductions( oBaseSite, _oDocument );
				}
				if ( sGuid.Equals( GlobalDecorations.Properties ) ) {
					return new WindowStandardProperties( oBaseSite, _oDoc_Properties );
				}
			} catch ( Exception oEx ) {
				Type[] rgErrors = { typeof( NotImplementedException ),
									typeof( NullReferenceException ),
									typeof( ArgumentException ),
									typeof( ArgumentNullException ) };
				if( rgErrors.IsUnhandled( oEx ) )
					throw;

				LogError( "decor", "Couldn't create EditWin decor: " + sGuid.ToString() );
			}

            return base.Decorate( oBaseSite, sGuid );
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
				foreach( string strExtn in _oProgram.FileExtnList ) {
					_rgExtensions.Add( strExtn );
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
                    case var u when u == WindowTextTabs.ViewType:
                        return new WindowTextTabs( oBaseSite, (Editor)oDocument );
                     
                    case var r when r == EditWindow2.ViewType:
                    case var s when s == Guid.Empty:
                    default:
                        return new EditPresentation( oBaseSite, (Editor)oDocument );

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
            yield return new ViewType( "Test Tabs", WindowTextTabs.ViewType );
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
