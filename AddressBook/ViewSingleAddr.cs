using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Forms;
using System.Drawing.Printing;
using System.Xml;
using System.Drawing;

using SkiaSharp.Views.Desktop;
using SkiaSharp;

using Play.Interfaces.Embedding;
using Play.Edit;
using Play.Rectangles;

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
        IPgParent,
        IDisposable,
        IPgLoad<XmlElement>,
        IPgCommandView,
        IPgSave<XmlDocumentFragment>
    {
        protected IPgViewSite _oViewSite;
        public static Guid    ViewCategory => new Guid( "{22AF6601-FB34-40CC-874D-E1925E6B251D}" );
        protected DocAddrBook Document { get; }
        protected DocEntry    ReturnAddr { get; }

        public string Banner => "Print Viewer";

        public SKBitmap Icon => null;

        public Guid Catagory => ViewCategory;

        public bool IsDirty => throw new NotImplementedException();

        public IPgParent Parentage => _oViewSite.Host;

        public IPgParent Services => Parentage.Services;

        IEnumerator<int>? _enuPage;


        protected class DocSlot :
			IPgBaseSite
		{
			protected readonly ViewLabel _oHost;

			public DocSlot( ViewLabel oHost ) {
				_oHost = oHost ?? throw new ArgumentNullException();
			}

			public IPgParent Host => _oHost;

			public void LogError(string strMessage, string strDetails, bool fShow=true) {
				_oHost._oViewSite.LogError( strMessage, strDetails, fShow );
			}

			public void Notify( ShellNotify eEvent ) {
			}
		} // End class

        public ViewLabel( IPgViewSite oViewSite, DocAddrBook oDocument ) {
            _oViewSite = oViewSite ?? throw new ArgumentNullException();
            Document   = oDocument ?? throw new ArgumentNullException();

            //Font = new Font("Arial", 10);
            ReturnAddr = new DocEntry( new DocSlot( this ) );
        }

        void LogError( string strMessage ) {
            _oViewSite.LogError( "Address Printing", strMessage );
        }

        /// <summary>
        /// So the forms api for printing was designed BY MORONS.
        /// Tell me why I need to retrieve the page size on a per
        /// page basis?? Tho' I can guess, since the printer to be
        /// used hasn't even been determined yet, the want to make
        /// the usage all in one call. Thus they send those params
        /// every time.
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

            Size sMaxLine = CalcMaxRetAddrWidth(oDC);

            //pntLoc.X = ( Width - sMaxLine.Width ) / 2;

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
            return InitNew();
        }

        public bool InitNew() {
            if( !ReturnAddr.Load( Document.Outline[0] ) ) {
                LogError( "Couldn't find return address." );
                throw new InvalidOperationException();
            }

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

        /// <summary>
        /// Alas I need the DC for the Dpi of the display.
        /// I'll fix it later so I don't need the DC all the time.
        /// </summary>
        /// <param name="oDC"></param>
        protected Size CalcMaxRetAddrWidth( Graphics oDC ) {
            SizeF sMaxAddrLine = new();

            foreach( Line oLine in Document.Entry ) {
                SizeF sSize = oDC.MeasureString( oLine.ToString(), Font );
                if( sSize.Width > sMaxAddrLine.Width )
                    sMaxAddrLine.Width = sSize.Width;
            }

            return new( (int)sMaxAddrLine.Width, (int)sMaxAddrLine.Height );
        }

        /// <summary>
        /// As much as it pains me to use the normal windows GDI
        /// it's way easier for the B/W case. HOWEVER, I still need to
        /// use the SKControl b/c it fixes a Width & Height bug where
        /// resize on a control does not update these value after
        /// the first w/h of the document display.
        /// </summary>
        protected override void OnPaint(PaintEventArgs e) {
            Graphics     oDC         = e.Graphics;
            Point        pntLoc      = new();
            StringFormat oFormat     = new();
            int          iFontHeight = (int)Font.GetHeight( oDC );

            foreach( Line oLine in ReturnAddr ) {
                oDC.DrawString( oLine.ToString(), Font, Brushes.Black,
                                pntLoc.X, pntLoc.Y, oFormat );
                pntLoc.Y += iFontHeight + 1;
            }
            Size sMaxLine = CalcMaxRetAddrWidth(oDC);

            pntLoc.X = ( Width - sMaxLine.Width ) / 2;
            // We'll put a warning in the printing case.
            if( pntLoc.X < 0 ) {
                pntLoc.X = 0;
            }

            foreach( Line oLine in Document.Entry ) {
                oDC.DrawString( oLine.ToString(), Font, Brushes.Black,
                                pntLoc.X, pntLoc.Y, oFormat );
                pntLoc.Y += iFontHeight + 1;
            }
        }
    }
}
