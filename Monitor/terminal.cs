using System;
using System.Collections.Generic;
using System.Text;
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
		public SKBitmap? Icon { get; protected set; }
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

            // Need to figure out how to match the columns of the Window vs Document...
            int iColumnTop = TextColumnTop;

            TextLayoutAdd( new LayoutRect( LayoutRect.CSS.None ) ); // Text

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
            if( _fReadOnly )
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
            Append( new TermRow( string.Empty ) );
            return true;
        }

        public bool Load(TextReader oStream) {
            Append( new TermRow( string.Empty ) );
            return true;
        }
        /// <summary>
        /// Appends a character at the end of the current last line.
        /// </summary>
        /// <param name="cChar"></param>
        public void AppendChar( char cChar ) {
            if( cChar == '\n' ) {
                Append( new TermRow( string.Empty ) );
                RenumberAndSumate();
                return;
            }
            if( cChar == '\r' )
                return;

            ReadOnlySpan<char> rgInsert = stackalloc char[1] { cChar };

            Row  oRow  = _rgRows[ElementCount-1];
            Line oLine = oRow[0];
            
            List<IPgEditHandler> rgHandlers = new List<IPgEditHandler>();
            foreach( IPgEditEvents oCall in _rgListeners ) {
                rgHandlers.Add( oCall.CreateEditHandler() );
            }
            // Tack the new character at the end of the line.
            oLine.TryReplace( oLine.ElementCount, 0, rgInsert );

            foreach( IPgEditHandler oCall in rgHandlers ) {
                oCall.OnUpdated( EditType.ModifyElem, oRow );
            }
        }
    }

}
