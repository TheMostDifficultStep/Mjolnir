using System.Text;

using Play.Interfaces.Embedding;
using Play.Edit; 
using Play.Forms;
using Play.Integration;
using Play.Parse;
using Play.Parse.Impl;
using Play.ImageViewer;

using SkiaSharp;

namespace Kanji_Practice {
    public class KanjiScratch : ImageSoloDoc {
        public KanjiScratch(IPgBaseSite oSiteBase) : base(oSiteBase) {
        }

        public override bool InitNew() {
            if( !base.InitNew() )
                return false;

            Bitmap = new SKBitmap( 200, 150, SkiaSharp.SKColorType.Gray8, SkiaSharp.SKAlphaType.Opaque );

            return true;
        }

        public void Clear() {
            using SKCanvas oCanvas = new SKCanvas( Bitmap );

            oCanvas.Clear( SKColors.White );

            Raise_ImageUpdated();
        }

    }
    public class KanjiProperties : DocProperties {
		public enum Labels : int {
			Kanji = 0,
			Hiragana,
			Meaning,
            Scratch
		}

        public KanjiProperties(IPgBaseSite oSiteBase) : base(oSiteBase) {
        }

        public override bool InitNew() {
            if( !base.InitNew() )
				return false;

            foreach( Labels eLabel in Enum.GetValues(typeof(Labels))) {
				CreatePropertyPair( eLabel.ToString() );
            }

            //ValueUpdate( (int)Labels.Kanji,    "彼女"   );
			//ValueUpdate( (int)Labels.Hiragana, "かのじょ"    );
			//ValueUpdate( (int)Labels.Meaning,  "she, her, girlfriend​"   );

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
        public KanjiScratch     ScratchPad   { get; }

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
        protected List< CardInfo > _rgCard = new();

        public KanjiDocument( IPgBaseSite oSite ) {
            _oBaseSite = oSite ?? throw new ArgumentNullException( "Site to document must not be null." );

            FlashCardDoc  = new EditorWithParser( new DocSlot( this ) ); // The raw stack of flash cards.
            FrontDisplay  = new KanjiProperties ( new DocSlot( this ) ); // The basic form Kanji, Hiragana, Description.
            ScratchPad    = new KanjiScratch    ( new DocSlot( this ) ); // Practice writing area.

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
            public KanjiProperties.Labels Label    { get; }
            public string                 FormatID { get; }
            public IMemoryRange ?         Range    { get; set; }

            public CardInfo( string strFormatID, KanjiProperties.Labels eLabel ) {
                FormatID = strFormatID;
                Label    = eLabel;
                Range    = null;
            }

            public override string ToString() {
                StringBuilder sb = new StringBuilder();

                sb.Append( Label.ToString() );
                if( Range != null ) {
                    sb.Append( ' ' );
                    sb.Append( Range.ToString() );
                }

                return sb.ToString();
            }
        }

        public bool Initialize() {
            if( !FrontDisplay.InitNew() )
                return false;
            if( !ScratchPad.InitNew() )
                return false;

            _rgCard.Add( new CardInfo( "kanji",    KanjiProperties.Labels.Kanji    ) );
            _rgCard.Add( new CardInfo( "hiragana", KanjiProperties.Labels.Hiragana ) );
            _rgCard.Add( new CardInfo( "meaning" , KanjiProperties.Labels.Meaning  ) );

            FlashCardDoc.BufferEvent += FlashCardDoc_BufferEvent;

            return true;
        }

        private void FlashCardDoc_BufferEvent(BUFFEREVENTS eEvent) {
            Jump( 0 );
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

            Jump( 0 );

            return true;
        }

        public void Jump( int iDir, bool fShowAll = false ) {
            if( FlashCardDoc.IsHit( _iFlashLine + iDir ) ) {
                using DocProperties.Manipulator oBulk = new ( FrontDisplay );

                _iFlashLine += iDir;

                Line oCard = FlashCardDoc[_iFlashLine];

                FindFormatting( oCard );

                foreach( CardInfo oInfo in _rgCard ) {
                    IMemoryRange oRange = oInfo.Range!;
                    if( oRange != null ) {
                        string strValue = oCard.SubString( oRange.Offset, oRange.Length );
                        
                        if( !fShowAll ) {
                            if( oInfo.Label == KanjiProperties.Labels.Hiragana )
                                strValue = String.Empty;
                            if( oInfo.Label == KanjiProperties.Labels.Meaning )
                                strValue = String.Empty;
                        }

                        oBulk.SetValue( (int)oInfo.Label, strValue );
                    }
                }

                Line oKanji = FrontDisplay[ (int)KanjiProperties.Labels.Kanji];

                oKanji.Formatting.Clear();
                oKanji.Formatting.Add( new ColorRange( 0, oKanji.ElementCount, 2 ) );

                if( !fShowAll ) {
                    ScratchPad.Clear();
                }
            }
        }

        public void FindFormatting( Line oLine ) {
            try {
                foreach( CardInfo oClear in _rgCard ) {
                    oClear.Range = null;
                }
                foreach( IColorRange oRange in oLine.Formatting ) {
                    if( oRange is MemoryElem<char> oElem ) {
                        foreach( CardInfo oInfo in _rgCard ) {
                            if( string.Compare( oInfo.FormatID, oElem.ID ) == 0 ) {
                                oInfo.Range = oRange;
                            }
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
            if( sGuid == GlobalCommands.Play ) {
                Jump( 0, fShowAll:true );
                return true;
            }

            return false;
        }
    }
}
