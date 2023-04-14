using System;
using System.Collections.Generic;
using System.Text;

using Play.Interfaces.Embedding;
using Play.Edit; 
using Play.Forms;
using Play.Integration;
using Play.Parse.Impl;

namespace Kanji_Practice {
    public class KanjiDocument :
        IPgParent,
		IDisposable,
        IPgLoad<TextReader>,
        IPgSave<TextWriter>
    {
        protected readonly IPgBaseSite _oBaseSite;

        protected readonly Grammer<char> _oGrammer;
        public bool IsDirty => false;

        public IPgParent Parentage => _oBaseSite.Host;
        public IPgParent Services  => Parentage.Services;

        public Editor        FlashCardDoc  { get; }

        public DocProperties FrontDisplay { get; }

        public class DocSlot :
            IPgBaseSite
        {
            protected readonly KanjiDocument _oHost;

            public DocSlot( KanjiDocument oHost ) {
                _oHost = oHost;
            }
            public IPgParent Host => _oHost;

            public void LogError(string strMessage, string strDetails, bool fShow = true) {
                _oHost._oBaseSite.LogError(strMessage, strDetails, fShow);
            }

            public void Notify(ShellNotify eEvent) {
                _oHost._oBaseSite.Notify( eEvent );
            }
        }

        public KanjiDocument( IPgBaseSite oSite ) {
            _oBaseSite = oSite ?? throw new ArgumentNullException( "Site to document must not be null." );

            FlashCardDoc  = new Editor       ( new DocSlot( this ) ); // The raw stack of flash cards.
            FrontDisplay  = new DocProperties( new DocSlot( this ) ); // The basic form Kanji, Hiragana, Description.

			try {
				// A parser is matched one per text document we are loading.
				ParseHandlerText oParser = new ParseHandlerText( FlashCardDoc, "asm" );
                _oGrammer = oParser.Grammer;
			} catch( Exception oEx ) {
                Type[] rgErrors = { typeof( NullReferenceException ),
                                    typeof( InvalidCastException ),
                                    typeof( ArgumentNullException ),
									typeof( ArgumentException ),
									typeof( InvalidOperationException ),
									typeof( InvalidProgramException ) };
                if( rgErrors.IsUnhandled( oEx ) )
                    throw;

				throw new InvalidOperationException( "Couldn't create parse handler for given text.", oEx );
			}
        }

        // See ca 1816 warning.
        public void Dispose() {
        }

        public bool Initialize() {
            if( !FrontDisplay.InitNew() )
                return false;

            return true;
        }

        public bool InitNew() {
            if( !FlashCardDoc.InitNew() )
                return false;

            if( !Initialize() ) 
                return false;

            return true;
        }

        public bool Save(TextWriter oStream) {
            return true;
        }

        public bool Load(TextReader oStream) {
            if( !Initialize() ) 
                return false;

            return true;
        }
    }
}
