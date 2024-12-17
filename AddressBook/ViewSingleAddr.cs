using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Forms;
using System.Drawing.Printing;
using System.IO;
using System.Xml;

using SkiaSharp.Views.Desktop;

using Play.Interfaces.Embedding;
using Play.Edit;
using Play.Rectangles;
using SkiaSharp;
using System.Drawing;
using System.Collections;

namespace AddressBook {
    /// <summary>
    /// Use this to show all the names in the address book.
    /// </summary>
    public class ViewOutline : WindowMultiColumn 
    {
        public ViewOutline(IPgViewSite oViewSite, object oDocument) : base(oViewSite, oDocument) {
        }

        protected override bool Initialize() {
            if( !base.Initialize() )
                return false;

            TextLayoutAdd( new LayoutRect( LayoutRect.CSS.Flex ) {Track=10 }, (int)DocOutline.Column.Check ); 
            TextLayoutAdd( new LayoutRect( LayoutRect.CSS.Flex ), (int)DocOutline.Column.LastName ); 
            TextLayoutAdd( new LayoutRect( LayoutRect.CSS.None ), (int)DocOutline.Column.FirstName ); 

            // Do this so we can return a desired height. O.o;;
            _oCacheMan.CacheRepair();

            return true;
        }
    }

    /// <summary>
    /// We'll just construct the address and put it in this editor.
    /// </summary>
    public class ViewSingleAddr : EditWindow2 {
		public static Guid ViewCategory {get;} = new Guid( "{CB6C6330-7152-4EB3-ABE3-9CFD93EA3B03}" );

        protected DocAddrBook Document { get; }

        public ViewSingleAddr( IPgViewSite oBaseSite, DocAddrBook oDocument ) : 
            base( oBaseSite, (BaseEditor)oDocument.Entry ) 
        {
            Document = oDocument ?? throw new ArgumentNullException();
        }
        public override object Decorate(IPgViewSite oBaseSite, Guid sGuid) {
            if( sGuid == GlobalDecor.Outline ) {
                return new ViewOutline( oBaseSite, Document.Outline );
            }

            return null;
        }

        public override bool Execute(Guid sGuid) {
            if( sGuid == GlobalCommands.JumpNext ) {
                Document.Jump( 1 ); 
                return true;
            }
            if( sGuid == GlobalCommands.JumpPrev ) {
                Document.Jump( -1 );
                return true;
            }

            return false;
        }

        /// <summary>
        /// Turns out this is a case where we don't want to allow 
        /// ANY typing! O.o I should do more work to override the
        /// other cases but not now. >_<;;
        /// </summary>
        protected override void OnKeyDown(KeyEventArgs e) {
            if( IsDisposed )
                return;

            e.Handled = true;

            switch( e.KeyCode ) {
                case Keys.PageDown:
                    Document.Jump( 1 );
                    break;
                case Keys.PageUp:
                    Document.Jump( -1 );
                    break;
            }
        }
    } // end class

    // https://learn.microsoft.com/en-us/dotnet/api/system.drawing.printing.printdocument?view=windowsdesktop-9.0
    public class ViewLabel : 
        SKControl,
        IDisposable,
        IPgLoad<XmlElement>,
        IPgCommandView,
        IPgSave<XmlDocumentFragment>
    {
        protected IPgViewSite _oViewSite;
        public static Guid    ViewCategory => new Guid( "{22AF6601-FB34-40CC-874D-E1925E6B251D}" );
        protected DocAddrBook Document { get; }

        public string Banner => "Print Viewer";

        public SKBitmap Icon => null;

        public Guid Catagory => ViewCategory;

        public bool IsDirty => throw new NotImplementedException();

        IEnumerator<int>? _enuPage;

        public ViewLabel( IPgViewSite oViewSite, DocAddrBook oDocument ) {
            _oViewSite = oViewSite ?? throw new ArgumentNullException();
            Document   = oDocument ?? throw new ArgumentNullException();
        }

        /// <summary>
        /// So the forms api for printing was designed BY MORONS.
        /// Tell me why I need to retrieve the page size on a per
        /// page basis?? 
        /// This is just a little experiment until I make my own 
        /// code for this problem.
        /// </summary>
        protected void DoPrint() {
            PrintDocument   oPrintDoc = new(); 
          //PrinterSettings oPrinter; // printer chosen

            oPrintDoc.PrintPage += Page_OnPrint;
            oPrintDoc.Print();
        }

        public IEnumerator<int> GetEnumerator( PrintPageEventArgs oEV ) {
            int       leftMargin = oEV.MarginBounds.Left;
            int       topMargin  = oEV.MarginBounds.Top;
            Graphics  oDC        = oEV.Graphics;

            if( oDC is null )
                throw new ArgumentException( "Null graphics in PrintArgs" );

            // Calculate the number of lines per page.
            // ev.MarginBounds.Height 
                
            SmartRect    rcRect  = new( LOCUS.UPPERLEFT, leftMargin, topMargin, 100, 10 );
            StringFormat oFormat = new StringFormat();

            for( int i = 0; i<10; ++i ) {
                oDC.FillRectangle( Brushes.Black, rcRect.Rect );
                //oDC.DrawString   ( "hello", printFont, Brushes.Black,
                //                   rcRect.Left, rcRect.Top, oFormat );
                rcRect.Top += ( i * rcRect.Height ) + 1;
                yield return i;
            }
        }

        private void Page_OnPrint( object o, PrintPageEventArgs oEvent ) {
            try {
                if( _enuPage is null )
                    _enuPage = GetEnumerator( oEvent );

                oEvent.HasMorePages = _enuPage.MoveNext();

                if( !oEvent.HasMorePages ) {
                    _enuPage = null;
                }
            } catch( Exception oEx ) {
                Type[] rgErrors = { typeof( ArgumentException ),
                                    typeof( ArgumentNullException ),
                                    typeof( NullReferenceException ) };
                if( rgErrors.IsUnhandled( oEx ) )
                    throw;
            }
        }

        public bool Load(XmlElement oElem) {
            return true;
        }

        public bool InitNew() {
            return true;
        }

        public object Decorate(IPgViewSite oBaseSite, Guid sGuid) {
            return null;
        }

        public bool Execute(Guid sGuid) {
            if( sGuid == GlobalCommands.Play ) {
                DoPrint();
            }

            return false;
        }

        public bool Save(XmlDocumentFragment oStream) {
            return true;
        }
    }
}
