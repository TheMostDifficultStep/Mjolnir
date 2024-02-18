using System.Text;

using SkiaSharp;

using Play.Interfaces.Embedding;
using Play.Edit; 
using Play.Forms;
using Play.Integration;
using Play.Parse;
using Play.Parse.Impl;
using Play.ImageViewer;

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

	public enum KanjiPropEnum : int {
		Kanji = 0,
		Hiragana,
		Meaning,
        Scratch
	}

    public class KanjiProperties : 
        DocProperties
    {
        public KanjiProperties(IPgBaseSite oSite) : base(oSite) {
        }

        public PropertyRow this[KanjiPropEnum eIndex] => (PropertyRow)this[(int)eIndex];

        public override bool InitNew() {
            if( !base.InitNew() ) 
                return false;

            foreach( string strProp in Enum.GetNames(typeof(KanjiPropEnum)) ) {
                _rgRows.Add( new PropertyRow( strProp ) );
            }

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
        protected readonly IPgFileSite _oFileSite;

        public bool IsDirty => FlashCardDoc.IsDirty;

        public IPgParent Parentage => _oBaseSite.Host;
        public IPgParent Services  => Parentage.Services;

        public EditorWithParser FlashCardDoc { get; }
        public KanjiProperties  Properties   { get; }
        public KanjiScratch     ScratchPad   { get; }
        public Editor           Meanings     { get; }

        public class DocSlot :
            IPgBaseSite,
            IPgFileSite
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

            public FILESTATS FileStatus => FILESTATS.UNKNOWN;

            public Encoding FileEncoding => Encoding.UTF8;

            public string FilePath => string.Empty;

            /// <summary>
            /// So this is a little evil. I'd like the file to be real. But I have
            /// no notation for a file that is an embedding!! Basically embedding is
            /// something like this "file.txt!obj1!.."
            /// </summary>
            public virtual string FileName { 
                get {
                    return _oHost._oFileSite.FileName + "!embedding";
            }   }
        }

        public class PriDocSlot : DocSlot {
            public PriDocSlot(KanjiDocument oHost) : base(oHost) {
            }
            public override string FileName { 
                get {
                    return _oHost._oFileSite.FileName;
            }   }
        }

        protected int _iFlashLine = 0;
        protected List< CardInfo >     _rgCard     = new();
        protected List< IMemoryRange > _rgMeanings = new();

        public KanjiDocument( IPgBaseSite oSite ) {
            _oBaseSite = oSite ?? throw new ArgumentNullException( "Site to document must not be null." );
            _oFileSite = oSite as IPgFileSite ?? throw new InvalidCastException( "IPgFileSite not supported" );

            FlashCardDoc = new EditorWithParser( new PriDocSlot( this ) ); // The raw stack of flash cards.
            Properties   = new KanjiProperties ( new DocSlot   ( this ) ); // The basic form Kanji, Hiragana, Description.
            ScratchPad   = new KanjiScratch    ( new DocSlot   ( this ) ); // Practice writing area.
            Meanings     = new Editor          ( new DocSlot   ( this ) ); // multi value meanings.

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
            public KanjiPropEnum  Property    { get; }
            public string         FormatID { get; }
            public IMemoryRange ? Range    { get; set; }

            public CardInfo( string strFormatID, KanjiPropEnum eLabel ) {
                FormatID = strFormatID;
                Property    = eLabel;
                Range    = null;
            }

            public override string ToString() {
                StringBuilder sb = new StringBuilder();

                sb.Append( Property.ToString() );
                if( Range != null ) {
                    sb.Append( ' ' );
                    sb.Append( Range.ToString() );
                }

                return sb.ToString();
            }
        }

        private void FlashCardDoc_BufferEvent(BUFFEREVENTS eEvent) {
            Jump( 0 );
        }

        public bool Initialize() {
            if( !Properties.InitNew() )
                return false;
            if( !ScratchPad.InitNew() )
                return false;
            if( !Meanings  .InitNew() )
                return false;

            _rgCard.Add( new CardInfo( "kanji",    KanjiPropEnum.Kanji    ) );
            _rgCard.Add( new CardInfo( "hiragana", KanjiPropEnum.Hiragana ) );

            FlashCardDoc.BufferEvent += FlashCardDoc_BufferEvent;

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

            Jump( 0 );

            return true;
        }

        public void Jump( int iDir, bool fShowAll = false ) {
            try {
                if( FlashCardDoc.IsHit( _iFlashLine + iDir ) ) {
                    _iFlashLine += iDir;

                    Line oCard = FlashCardDoc[_iFlashLine];

                    FindFormatting( oCard );

                    using DocProperties.Manipulator oBulk = new DocProperties.Manipulator( Properties );
                    foreach( CardInfo oInfo in _rgCard ) {
                        if( oInfo.Range is IMemoryRange oRange ) {
                            string strValue = oCard.SubString( oRange.Offset, oRange.Length );
                        
                            if( !fShowAll ) {
                                if( oInfo.Property == KanjiPropEnum.Hiragana )
                                    strValue = String.Empty;
                                if( oInfo.Property == KanjiPropEnum.Meaning )
                                    strValue = String.Empty;
                            }

                            oBulk.SetValue( (int)oInfo.Property, strValue );
                        }
                    }
                    if( _rgMeanings.Count > 0 && fShowAll ) {
                        using Editor.Manipulator oAddMeaning = Meanings.CreateManipulator();

                        foreach( IMemoryRange oRange in _rgMeanings ) {
                            if( oRange.Length > 0 ) {
                                oAddMeaning.LineAppend( oCard.SubString( oRange.Offset, oRange.Length ) );
                            }
                        }
                    }

                    // We depend on the fact that the property page has already been loaded!!
                    // At this point we are simply updating the values as we encounter new cards.
                    Line oKanji = Properties[KanjiPropEnum.Kanji].Value;

                    oKanji.Formatting.Clear();
                    oKanji.Formatting.Add( new ColorRange( 0, oKanji.ElementCount, 2 ) );

                    if( !fShowAll ) {
                        ScratchPad.Clear();
                    }
                }
            } catch( Exception oEx ) {
                Type[] rgErrors = { typeof( NullReferenceException ),
                                    typeof( ArgumentNullException ),
                                    typeof( ArgumentOutOfRangeException ) };
                if( rgErrors.IsUnhandled( oEx ) )
                    throw;

                _oBaseSite.LogError( "Kanji", "problem loading up new kanji properties" );
            }
        }

        public void FindFormatting( Line oLine ) {
            try {
                foreach( CardInfo oClear in _rgCard ) {
                    oClear.Range = null;
                }
                _rgMeanings.Clear();
                Meanings.Clear();

                foreach( IColorRange oRange in oLine.Formatting ) {
                    if( oRange is MemoryElem<char> oElem ) {
                        foreach( CardInfo oInfo in _rgCard ) {
                            if( string.Compare( oInfo.FormatID, oElem.ID ) == 0 ) {
                                oInfo.Range = oRange;
                            }
                        }
                        if( oElem.ID == "meaning" ) {
                            _rgMeanings.Add( oElem );
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
