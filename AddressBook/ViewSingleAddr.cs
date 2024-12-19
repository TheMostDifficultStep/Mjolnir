using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Forms;
using System.Drawing.Printing;
using System.Xml;
using System.Drawing;
using System.Reflection;

using SkiaSharp.Views.Desktop;
using SkiaSharp;

using Play.Interfaces.Embedding;
using Play.Edit;
using Play.Rectangles;
using Play.Drawing;
using Play.Forms;

namespace AddressBook {
    public class ViewPrinterList : WindowMultiColumn {
        public ViewPrinterList(IPgViewSite oViewSite, object oDocument) : 
            base(oViewSite, oDocument) 
        {
            IsScrollVisible = false;
        }

        protected override bool Initialize() {
            if( !base.Initialize() )
                return false;

            TextLayoutAdd( new LayoutRect( LayoutRect.CSS.Flex,  CheckColumnWidth, 1L ), (int)DocPrinters.Column.Check ); 
            TextLayoutAdd( new LayoutRect( LayoutRect.CSS.None ), (int)DocPrinters.Column.PrinterName ); 

            // Do this so we can return a desired height. O.o;;
            _oCacheMan.CacheRepair();

            return true;
        }
    }
	/// <summary>
	/// Property viewer for our address book. 
	/// </summary>
	public class ViewAddrProperties : 
        WindowStandardProperties
     {
        public DocAddrBook AddrDoc { get; }

		public ViewAddrProperties( IPgViewSite oViewSite, DocAddrBook docAddr ) : 
            base( oViewSite, docAddr.Properties ) 
        {
			AddrDoc = docAddr ?? throw new ArgumentNullException( nameof( docAddr ) );
		}

        public override void InitRows() {
            int[] rgShow = {
                (int)DocAddrProps.Names.Width,
                (int)DocAddrProps.Names.Height,
            };

            base.InitRows(rgShow);

            try {
				PropertyInitRow( (int)DocAddrProps.Names.Printers,
								 new ViewPrinterList( new WinSlot( this ), AddrDoc.Printers ) );
				//PropertyInitRow( (int)AddressProperties.Names.Group,
				//				 new ViewSSTVModesAsList( new WinSlot( this ), AddrDoc.RxSSTVModeDoc ) );

                //SSTVDocument.RxSSTVModeDoc.Event_Loaded += OnDocLoaded_RxSSTVModeDoc;
			} catch ( Exception oEx ) {
				Type[] rgErrors = { typeof( NullReferenceException ),
									typeof( ArgumentOutOfRangeException ),
									typeof( ApplicationException ) };
				if( rgErrors.IsUnhandled( oEx ) )
					throw;
				LogError( "Unable to set up receive mode selectors" );
			}
        }

		/// <summary>
		/// Tell our overall property page to resize because our mode list is 
		/// a different size.
		/// </summary>
       // private void OnDocLoaded_RxSSTVModeDoc() {
			//OnSizeChanged( new EventArgs() );
       // }
	}
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

        public override SKBitmap Icon => SKImageResourceHelper.GetImageResource( 
                Assembly.GetExecutingAssembly(), 
                @"AddressBook.Content.icons8-address-book-96.png" );

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
        protected readonly IPgViewSite   _oViewSite;
        protected readonly IPgViewNotify _oViewEvents; 
        public static Guid    ViewCategory => new Guid( "{22AF6601-FB34-40CC-874D-E1925E6B251D}" );
        protected DocAddrBook Document { get; }
        protected DocEntry    ReturnAddr { get; }

        public string Banner => "Print Viewer";

        public SKBitmap Icon => SKImageResourceHelper.GetImageResource( 
                Assembly.GetExecutingAssembly(), 
                @"AddressBook.Content.icons8-print-96.png" );
        public SKBitmap Stamp => SKImageResourceHelper.GetImageResource( 
                Assembly.GetExecutingAssembly(),
                @"AddressBook.Content.icons8-postage-stamp-64.png" );
        public Bitmap StampBmp { get; }


        public Guid Catagory => ViewCategory;

        public bool IsDirty => throw new NotImplementedException();

        public IPgParent Parentage => _oViewSite.Host;

        public IPgParent Services => Parentage.Services;

        IEnumerator<int>? _enuPage;

        readonly static Keys[] _rgHandledKeys = { Keys.PageDown, Keys.PageUp, Keys.Down,
                                                  Keys.Up, Keys.Right, Keys.Left, Keys.Back,
                                                  Keys.Delete, Keys.Enter, Keys.Tab,
                                                  Keys.Control | Keys.A, Keys.Control | Keys.F };

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

        protected class ViewSlot :
            DocSlot,
            IPgViewSite
        {
            public ViewSlot(ViewLabel oHost) : base(oHost) {
            }

            public IPgViewNotify EventChain => _oHost._oViewSite.EventChain;
        }

        public ViewLabel( IPgViewSite oViewSite, DocAddrBook oDocument ) {
            _oViewSite   = oViewSite ?? throw new ArgumentNullException();
            _oViewEvents = oViewSite.EventChain ?? throw new ArgumentException("Site.EventChain must support IPgViewSiteEvents");
            Document     = oDocument ?? throw new ArgumentNullException();

            //Font = new Font("Arial", 10);
            ReturnAddr = new DocEntry( new DocSlot( this ) );

            StampBmp = Stamp.ToBitmap();

            Array.Sort<Keys>( _rgHandledKeys );
        }

        void LogError( string strMessage ) {
            _oViewSite.LogError( "Address Printing", strMessage );
        }

        protected override bool IsInputKey(Keys keyData) {
            int iIndex = Array.BinarySearch<Keys>(_rgHandledKeys, keyData);

            if (iIndex >= 0)
                return (true);

            return base.IsInputKey( keyData );
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
            Rectangle rcDisplay  = new ( oEV.MarginBounds.Left,
                                         oEV.MarginBounds.Top,
                                         oEV.MarginBounds.Width,
                                         oEV.MarginBounds.Height );

            if( oDC is null )
                throw new ArgumentException( "Null graphics in PrintArgs" );

            DoPaint( rcDisplay, oDC );

            yield return 1;
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

        protected override void Dispose(bool disposing) {
            Document.Entry.BufferEvent -= OnBufferEvent_Entry;

            base.Dispose(disposing);
        }

        public bool Load(XmlElement oElem) {
            return InitNew();
        }

        public bool InitNew() {
            if( !ReturnAddr.Load( Document.Outline[0] ) ) {
                LogError( "Couldn't find return address." );
                throw new InvalidOperationException();
            }
            Document.Entry.BufferEvent += OnBufferEvent_Entry;

            return true;
        }

        private void OnBufferEvent_Entry(BUFFEREVENTS eEvent) {
            Invalidate();
        }

        public object Decorate(IPgViewSite oBaseSite, Guid sGuid) {
            if( sGuid == GlobalDecor.Outline ) {
                return new ViewOutline( oBaseSite, Document.Outline );
            }
            if( sGuid == GlobalDecor.Properties ) {
                return new ViewAddrProperties( new ViewSlot( this ), Document );
            }

            return null;
        }

        public bool Execute(Guid sGuid) {
            if( sGuid == GlobalCommands.Play ) {
                DoPrint();
                return true;
            }
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

        public bool Save(XmlDocumentFragment oStream) {
            return true;
        }

        /// <summary>
        /// Alas I need the DC for the Dpi of the display.
        /// I'll fix it later so I don't need the DC all the time.
        /// </summary>
        /// <param name="oDC"></param>
        protected Size GetAddrSize( Graphics oDC, Editor oAddr ) {
            SizeF sMaxAddrLine = new();
            int   iFontHeight  = (int)Font.GetHeight( oDC );

            foreach( Line oLine in oAddr ) {
                SizeF sSize = oDC.MeasureString( oLine.ToString(), Font );
                if( sSize.Width > sMaxAddrLine.Width )
                    sMaxAddrLine.Width = sSize.Width;

                sMaxAddrLine.Height += iFontHeight + 1;
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
            Graphics  oDC       = e.Graphics;
            Rectangle rcDisplay = new( 0, 0, Width, Height );

            DoPaint( rcDisplay, oDC );
        }

        protected void DoPaint( Rectangle rcDisplay, Graphics oDC, bool fShowStamp = true ) {
            Point        pntLoc      = new();
            StringFormat oFormat     = new();
            int          iFontHeight = (int)Font.GetHeight( oDC );

            pntLoc.X = rcDisplay.Left;
            pntLoc.Y = rcDisplay.Top;

            foreach( Line oLine in ReturnAddr ) {
                oDC.DrawString( oLine.ToString(), Font, Brushes.Black,
                                pntLoc.X, pntLoc.Y, oFormat );
                pntLoc.Y += iFontHeight + 1;
            }

            if( fShowStamp ) {
                oDC.DrawImage( StampBmp, new PointF( rcDisplay.Right - StampBmp.Width, 0 ));
            }

            Size sMaxLine = GetAddrSize( oDC, Document.Entry );
            pntLoc.X = ( rcDisplay.Width  - sMaxLine.Width  ) / 2;
            pntLoc.Y = ( rcDisplay.Height - sMaxLine.Height ) / 2;

            // We'll put a warning in the printing case.
            if( pntLoc.X < 0 ) {
                pntLoc.X = 0;
            }
            if( pntLoc.Y < 0 ) {
                pntLoc.Y = 0;
            }

            foreach( Line oLine in Document.Entry ) {
                oDC.DrawString( oLine.ToString(), Font, Brushes.Black,
                                pntLoc.X, pntLoc.Y, oFormat );
                pntLoc.Y += iFontHeight + 1;
            }
        }

        protected override void OnGotFocus(EventArgs e) {
            base.OnGotFocus( e );

            _oViewEvents.NotifyFocused( true );

            this.Invalidate();
        }

        protected override void OnLostFocus(EventArgs e) {
            base.OnLostFocus( e );

            _oViewEvents.NotifyFocused( false );

            this.Invalidate();
        }
        protected override void OnKeyDown(KeyEventArgs e) {
            if( IsDisposed )
                return;

            e.Handled = true;

            Document.Jump( e.KeyCode );
        }
    }
}
