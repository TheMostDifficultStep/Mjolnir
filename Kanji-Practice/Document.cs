using System;
using System.Collections.Generic;
using System.Text;

using Play.Interfaces.Embedding;
using Play.Edit; 
using Play.Forms;
using Play.Integration;
using Play.Parse;
using Play.Parse.Impl;

namespace Kanji_Practice {
    public class KanjiProperties : DocProperties {
		public enum Labels : int {
			Kanji = 0,
			Hiragana,
			Meaning,
		}

        public KanjiProperties(IPgBaseSite oSiteBase) : base(oSiteBase) {
        }

        public override bool InitNew() {
            if( !base.InitNew() )
				return false;

            foreach( Labels eLabel in Enum.GetValues(typeof(Labels))) {
				CreatePropertyPair( eLabel.ToString() );
            }

            ValueUpdate( (int)Labels.Kanji,    "彼女"   );
			ValueUpdate( (int)Labels.Hiragana, "かのじょ"    );
			ValueUpdate( (int)Labels.Meaning,  "she, her, girlfriend​"   );

			return true;
        }
    }
    public class KanjiDocument :
        IPgParent,
		IDisposable,
        IPgCommandBase,
        IPgLoad<TextReader>,
        IPgSave<TextWriter>
    {
        protected readonly IPgBaseSite _oBaseSite;

        public bool IsDirty => FlashCardDoc.IsDirty;

        public IPgParent Parentage => _oBaseSite.Host;
        public IPgParent Services  => Parentage.Services;

        public EditorWithParser FlashCardDoc  { get; }
        public DocProperties    FrontDisplay { get; }

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

        protected int _iFlashLine = 0;
        protected Dictionary<string, CardInfo> _rgCard = new();

        public KanjiDocument( IPgBaseSite oSite ) {
            _oBaseSite = oSite ?? throw new ArgumentNullException( "Site to document must not be null." );

            FlashCardDoc  = new EditorWithParser( new DocSlot( this ) ); // The raw stack of flash cards.
            FrontDisplay  = new KanjiProperties ( new DocSlot( this ) ); // The basic form Kanji, Hiragana, Description.

			try {
				// A parser is matched one per text document we are loading.
				FlashCardDoc.ParseHandler = new ParseHandlerText( FlashCardDoc, "flashcard" );
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

        public class CardInfo {
            readonly public int _iLabel;

            public IMemoryRange ? _oRange;

            public CardInfo( KanjiProperties.Labels eLabel ) {
                _iLabel = (int)eLabel;
                _oRange = null;
            }

            public override string ToString() {
                StringBuilder sb = new StringBuilder();

                sb.Append( _iLabel.ToString() );
                if( _oRange != null ) {
                    sb.Append( ' ' );
                    sb.Append( _oRange.ToString() );
                }

                return sb.ToString();
            }
        }

        public bool Initialize() {
            if( !FrontDisplay.InitNew() )
                return false;

            _rgCard.Add( "kanji",    new CardInfo( KanjiProperties.Labels.Kanji ) );
            _rgCard.Add( "hiragana", new CardInfo( KanjiProperties.Labels.Hiragana ) );
            _rgCard.Add( "meaning",  new CardInfo( KanjiProperties.Labels.Meaning ) );

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
            return FlashCardDoc.Save( oStream );
        }

        public bool Load(TextReader oStream) {
            if( !FlashCardDoc.Load( oStream ) )
                return false;

            if( !Initialize() ) 
                return false;

            return true;
        }

        public void Jump( int iDir ) {
            using DocProperties.Manipulator oBulk = new ( FrontDisplay );

            if( FlashCardDoc.IsHit( _iFlashLine + iDir ) ) {
                _iFlashLine += iDir;

                Line oCard = FlashCardDoc[_iFlashLine];

                FindFormatting( oCard );

                foreach( KeyValuePair<string,CardInfo> oInfo in _rgCard ) {
                    IMemoryRange oRange = oInfo.Value._oRange;
                    if( oRange != null ) {
                        oBulk.SetValue( oInfo.Value._iLabel, oCard.SubString( oRange.Offset, oRange.Length ) );
                    }
                }

                Line oKanji = FrontDisplay[ (int)KanjiProperties.Labels.Kanji];

                oKanji.Formatting.Clear();
                oKanji.Formatting.Add( new ColorRange( 0, oKanji.ElementCount, 2 ) );
            }
        }

        public void FindFormatting( Line oLine ) {
            try {
                foreach( KeyValuePair<string,CardInfo> oClear in _rgCard ) {
                    oClear.Value._oRange = null;
                }
                foreach( IColorRange oRange in oLine.Formatting ) {
                    if( oRange is MemoryElem<char> oElem ) {
                        if( _rgCard.TryGetValue( oElem.ID, out CardInfo oCard ) ) {
                            oCard._oRange = oRange;
                        }
                    }
                }
            } catch( Exception oEx ) {
                Type[] rgErrors = { typeof( ArgumentNullException ),
                                    typeof( KeyNotFoundException ),
                                    typeof( NullReferenceException ),
                                    typeof( ArgumentOutOfRangeException ) };
                if( rgErrors.IsUnhandled( oEx ) )
                    throw;
            }
        }

        public bool Execute(Guid sGuid) {
            if( sGuid == GlobalCommands.JumpNext ) {
                Jump( 1 );
                return true;
            }
            if( sGuid == GlobalCommands.JumpPrev ) {
                Jump( -1 );
                return true;
            }

            return false;
        }
    }
}
