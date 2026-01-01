using System;
using System.IO;
using System.Collections.Generic;

using Play.Edit;
using Play.Interfaces.Embedding;
using Play.Integration;
using Play.Rectangles;
using Play.Parse;

namespace Play.Forms {
    /// <summary>
    /// 1/4/2019 : This is kind of built backwards. My first music player efforts where built 
    /// before I had and integration project. Music doesn't need to be parsed out to be played. 
    /// For example an m3u file. BUT it's way more useful to parse and find music randomly an
    /// then play. So I'm not sure how this is going to sort out.
    /// Amusingly enough, the MusicWalker project, doesn't use the EditorWithParser or
    /// EditorWithMusic! So I think I can sort this out better.
    /// 5/19/2023 : Moved these into the forms package instead of Mjolnir so that they are more
    /// readily used by other projects.
    /// </summary>
    public class EditorWithParser : Editor { // was EditorWithMusic
        ParseHandlerText _oParseHandler;
        readonly Editor _oDoc_Productions;

        public EditorWithParser(IPgBaseSite oSite) : base(oSite) {
            // This would be a great place for us to implement linked documents!!!
            _oDoc_Productions = new Editor(new DocSlot(this));
        }

        public ParseHandlerText ParseHandler {
            get { return _oParseHandler; }
            set {
                _oParseHandler = value ?? throw new ArgumentNullException();
            }
        }

        public override bool ProductionsTrace {
            get { return _oParseHandler.ProductionsTrace; }
            set { _oParseHandler.ProductionsTrace = value; }
        }

        public override BaseEditor Productions {
            get {
                return ( _oDoc_Productions );
            }
        }

        public bool Initialize() {
            if( !_oDoc_Productions.InitNew() )
                return false;

            if( _oParseHandler is not null ) {
                if( !_oParseHandler.InitNew() )
                    return false;

                _oParseHandler.ProductionsEdit = _oDoc_Productions;
            }
            return true;
        }

        public override bool InitNew() {
            try {
                if( !base.InitNew() )
                    return false;

                if( !Initialize() )
                    return false;
            } catch( NullReferenceException ) {
                return ( false );
            }
            return ( true );
        }

        public override bool Load(TextReader oReader) {
            try {
                if( !base.Load(oReader) )
                    return false;

                if( !Initialize() )
                    return false;
            } catch( NullReferenceException ) {
                return false;
            }
            return true;
        }

        public override void Dispose() {
            _oParseHandler?.Dispose();

            _oDoc_Productions.Dispose();

            base.Dispose();
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

    public class LineNumberWindow : EditWindow2 {
        public class FTCacheLineNumber : FTCacheWrap {
            Line _oGuest; // The line we are listing.

            public FTCacheLineNumber( Line oLine, Line oGuest ) : base( oLine ) {
                _oGuest = oGuest ?? throw new ArgumentNullException( "Guest line must not be null" );
            }

            public override void Measure(IPgFontRender oFR ) {
                Line.TryReplace( 0, Line.ElementCount, _oGuest.At.ToString() );

                base.Measure(oFR);
            }

            public override bool IsInvalid { get => _oGuest.At != Line.At; }
        }
        public class CacheManagerAsm : CacheManager2 {
            public CacheManagerAsm( CacheManagerAbstractSite oSite, IPgFontRender oFont, List<SmartRect> rgCols ) :
                base( oSite, oFont, rgCols ) {
            }

            protected override CacheRow CreateRow( Line oLine ) {
                CacheRow oRow = base.CreateRow( oLine );

                FTCacheLine oElem = new FTCacheLineNumber( new TextLine( oLine.At, oLine.At.ToString() ), oLine );

                oRow.CacheColumns.Add( oElem );

                return oRow;
            }
        }

        protected readonly LayoutRect _rctLineNumbers = new LayoutRect( LayoutRect.CSS.Flex ) { Track = 60 };

        public LineNumberWindow( IPgViewSite oSite, BaseEditor oEdit ) : base( oSite, oEdit ) {
        }
        protected override CacheManager2 CreateCacheManager(uint uiStdText) {
            return new CacheManagerAsm( new CacheManSlot(this),
                                        _oStdUI.FontRendererAt(uiStdText),
                                        _rgCacheMap );
        }
        protected override void InitColumns() {
            _rgLayout  .Add( _rctLineNumbers );
            _rgLayout  .Add( _rctTextArea );   // Main text area.

            _rgCacheMap.Add( _rctTextArea    );   // Text is always the first cache element on a row.
            _rgCacheMap.Add( _rctLineNumbers );   // Even if later in the layout.
        }
    }
}
