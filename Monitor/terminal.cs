using System;
using System.Windows.Forms;

using SkiaSharp;

using Play.Edit;
using Play.Interfaces.Embedding;
using Play.Rectangles;

namespace Monitor {
    public class ViewTerminal :
        WindowMultiColumn,
        IPgCommandView 
    {
        public readonly static Guid _gViewTerminal = new Guid( "{BE1E1F3D-6CE5-4FE2-9A52-EA2E5F72D3D5}" );
        public string    Banner => "Simple Terminal";
		public SKImage?  Icon { get; protected set; }
        public Guid      Catagory => _gViewTerminal;

        protected DocumentMonitor DocMon { get; } 

        /// <summary>
        /// This object is a a bit weird since we want to send our keystokes
        /// to the CPU. Any return characters go straight into the buffer
        /// and we get an update call. We don't add characters straight into
        /// our buffer! O.o
        /// </summary>
        public ViewTerminal(IPgViewSite oViewSite, object oDocument) : 
            base(oViewSite, oDocument) 
        {
            // any cast failure bails us out and system fails window create gracefully.
            IPgParent oDocParent = (IPgParent)oDocument;

            DocMon = (DocumentMonitor)oDocParent.Parentage; 

			try {
				Icon = DocMon.GetResource( "icons8-terminal-58.png" );
			} catch( InvalidOperationException ) {
			}
        }
        protected override bool Initialize() {
            if( !base.Initialize() )
                return false;

            TextLayoutAdd( new LayoutRect( LayoutRect.CSS.None ), 0 ); // Text

            return true;
        }

        public object Decorate(IPgViewSite oBaseSite, Guid sGuid) {
            return null;
        }

        public bool Execute(Guid sGuid) {
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

    public class Terminal : 
        EditMultiColumn,
        IPgLoad<TextReader>
    {
        public class TermRow : Row {
            public TermRow( string strValue ) {
                _rgColumns = new Line[1];

                _rgColumns[0] = new TextLine( 0, strValue );
            }

        }

        public Terminal(IPgBaseSite oSite) : base(oSite) {
        }

        public bool InitNew() {
            _rgRows.Insert( _rgRows.Count, new TermRow( string.Empty ) );
            return true;
        }

        public bool Load(TextReader oStream) {
            _rgRows.Insert( _rgRows.Count, new TermRow( string.Empty ) );
            return true;
        }
        /// <summary>
        /// Appends a character at the end of the current last line.
        /// </summary>
        public void AppendChar( char cChar ) {
            if( cChar == '\n' ) {
                Raise_DocUpdateBegin();
                _rgRows.Insert( _rgRows.Count, new TermRow( string.Empty ) );
                RenumberAndSumate();
                Raise_DocUpdateEnd( IPgEditEvents.EditType.Rows, null );
                return;
            }
            if( cChar == '\r' )
                return;

            ReadOnlySpan<char> rgInsert = stackalloc char[1] { cChar };
            Row                oRow     = _rgRows[ElementCount-1];
            Line               oLine    = oRow[0];
            
            Raise_DocUpdateBegin();

            // Tack the new character at the end of the line.
            oLine.TryReplace( oLine.ElementCount, 0, rgInsert );

            Raise_DocUpdateEnd( IPgEditEvents.EditType.Column, oRow );
        }
    }

}
