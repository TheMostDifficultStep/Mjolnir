using System.Xml;
using System.Windows.Forms;

using Play.Interfaces.Embedding;
using Play.Forms;
using Play.Rectangles;
using Play.ImageViewer;

using SkiaSharp;

namespace Kanji_Practice {
    public class ViewScratchPad : ImageViewSingle {
        readonly SKCanvas _oCanvas;
        readonly SKPaint  _oPaint;
        protected SKPoint _pntAspect   = SKPoint.Empty;
        protected SKPoint _pntPrevious = SKPoint.Empty;
        public ViewScratchPad(IPgViewSite oSiteBase, ImageBaseDoc oDocSolo) : base(oSiteBase, oDocSolo) {
            _oCanvas = new SKCanvas( oDocSolo.Bitmap );
            _oPaint  = new SKPaint() { Color = SKColors.Black };
        }

        protected override void Dispose(bool fDisposing) {
            if( !_fDisposed ) {
                _oCanvas.Dispose();
                _oPaint .Dispose();
            }
            base.Dispose(fDisposing);
        }

        protected override void OnSizeChanged(EventArgs e) {
            base.OnSizeChanged(e);

			_pntAspect = new SKPoint( Document.Bitmap.Width  / (float)_rctViewPort.Width,
									  Document.Bitmap.Height / (float)_rctViewPort.Height );
        }

        protected override void OnMouseDown(MouseEventArgs e) {
            base.OnMouseDown(e);

            if( e.Button == MouseButtons.Left ) {
                _pntPrevious = new SKPoint( e.X * _pntAspect.X, e.Y * _pntAspect.Y );
            }
        }

        protected override void OnMouseMove(MouseEventArgs e) {
            if( e.Button == MouseButtons.Left ) {
                SKPoint pntNext = new SKPoint( e.X * _pntAspect.X, e.Y * _pntAspect.Y );

                _oCanvas.DrawLine( _pntPrevious, pntNext, _oPaint );
                _pntPrevious = pntNext;
                Invalidate();
            }
        }
    }

    internal class KanjiMagnify : WindowStandardProperties {
        protected uint BigFont { get; } 
        protected ImageViewSingle ViewScratch { get; }

        public KanjiMagnify( IPgViewSite oSite, DocProperties oProperties, KanjiScratch oScratchDoc ) : base( oSite, oProperties ) {
            IPgMainWindow.PgDisplayInfo oInfo = new IPgMainWindow.PgDisplayInfo();
            if( _oSiteView.Host.TopWindow is IPgMainWindow oMainWin ) {
                oInfo = oMainWin.MainDisplayInfo;
            }
            BigFont = StdUI.FontCache( StdFace, 30, oInfo.pntDpi );

            ViewScratch = new ViewScratchPad( new WinSlot( this ), oScratchDoc );
            ViewScratch.Parent = this;
        }

        public override void InitRows() {
			int[] rgShow = { 
				(int)KanjiProperties.Labels.Kanji,
                (int)KanjiProperties.Labels.Hiragana,
                (int)KanjiProperties.Labels.Meaning
			};

			base.InitRows( rgShow );

			try {
				PropertyInitRow( Layout as LayoutTable, 
								 (int)KanjiProperties.Labels.Scratch, 
								 ViewScratch );
			} catch ( Exception oEx ) {
				Type[] rgErrors = { typeof( NullReferenceException ),
									typeof( ArgumentOutOfRangeException ) };
				if( rgErrors.IsUnhandled( oEx ) )
					throw;
				LogError( "Unable to set up receive mode selectors" );
            }
        }

        public override bool InitNew() {
            if( !base.InitNew() ) 
                return false;

            LabelValuePair oKanjiPair = Document.GetPropertyPair( (int)KanjiProperties.Labels.Kanji );
            LabelValuePair oHiragPair = Document.GetPropertyPair( (int)KanjiProperties.Labels.Hiragana );

            foreach( LayoutSingleLine oLayout in CacheList ) {
                if( oLayout.Cache.Line == oKanjiPair._oValue   ) {
                    oLayout.FontID = BigFont;
                }
                if( oLayout.Cache.Line == oHiragPair._oValue   ) {
                    oLayout.FontID = BigFont;
                }
            }

            OnDocumentEvent( BUFFEREVENTS.FORMATTED );

            return true;
        }
    }

    internal class ViewKanji: FormsWindow,
        IPgParent,
        IPgCommandView,
        IPgLoad<XmlElement>,
        IPgSave<XmlDocumentFragment>
    {
        protected KanjiDocument            KanjiDoc     { get; }
        protected WindowStandardProperties CenterDisplay{ get; }

        public IPgParent Parentage => _oSiteView.Host;
        public IPgParent Services  => Parentage.Services;

        public string Banner => "Kanji Cards";

        public SKBitmap Icon => null;

        public Guid Catagory => Guid.Empty;

        public bool  IsDirty => false;

        protected class DocSlot :
			IPgBaseSite
		{
			protected readonly ViewKanji _oHost;

			public DocSlot( ViewKanji oHost ) {
				_oHost = oHost ?? throw new ArgumentNullException();
			}

			public IPgParent Host => _oHost;

			public void LogError(string strMessage, string strDetails, bool fShow=true) {
				_oHost._oSiteView.LogError( strMessage, strDetails, fShow );
			}

			public void Notify( ShellNotify eEvent ) {
			}
		} // End class

		protected class ViewSlot : DocSlot, IPgViewSite {
			public ViewSlot( ViewKanji oHost ) : base( oHost ) {
			}

			public IPgViewNotify EventChain => _oHost._oSiteView.EventChain;
		}

        public ViewKanji( IPgViewSite oViewSite, KanjiDocument oMonitorDoc ) : 
            base( oViewSite, oMonitorDoc.FrontDisplay.PropertyDoc ) 
        {
            KanjiDoc      = oMonitorDoc ?? throw new ArgumentNullException( "Monitor document must not be null!" );

            Layout        = new LayoutStackHorizontal() { Units = LayoutRect.CSS.Flex };
            CenterDisplay = new KanjiMagnify( new ViewSlot( this ), KanjiDoc.FrontDisplay, KanjiDoc.ScratchPad ) ;

            CenterDisplay.Parent = this;
        }

        public override bool InitNew() {
            if( !base.InitNew() )
                return false;

            if( !CenterDisplay.InitNew() )
                return false;

            // complete final layout of table and command window.
            if( Layout is LayoutStack oStack ) {
                oStack.Add( new LayoutRect   ( LayoutRect.CSS.Pixels ) { Track = 50 } );
                oStack.Add( new LayoutControl( CenterDisplay, LayoutRect.CSS.None ) );
                oStack.Add( new LayoutRect   ( LayoutRect.CSS.Pixels ) { Track = 50 } );
            }

            //KanjiDoc.RefreshScreen += OnRefreshScreen_MonDoc;
            return true;
        }

        protected override void OnSizeChanged(EventArgs e) {
            if( Width > 0 && Height > 0 ) {
                base.OnSizeChanged(e);
            }
        }

        protected override void OnKeyDown(KeyEventArgs e) {
            base.OnKeyDown(e);

            if( e.KeyCode == Keys.F2 ) {
            }
        }

        public bool Load(XmlElement oStream) {
            return InitNew();
        }

        protected override void Dispose(bool disposing) {
            if( disposing ) {
                //KanjiDoc.RefreshScreen -= OnRefreshScreen_MonDoc;
            }
            base.Dispose(disposing);
        }
        private void OnRefreshScreen_MonDoc(int obj) {
            OnDocumentEvent(BUFFEREVENTS.MULTILINE );
            Invalidate();
        }

        public bool Execute(Guid sGuid) {
            if( sGuid == GlobalCommands.JumpNext ) {
                KanjiDoc.Jump(1);
            }
            if( sGuid == GlobalCommands.JumpPrev ) {
                KanjiDoc.Jump(-1);
            }
            if( sGuid == GlobalCommands.Play ) {
                KanjiDoc.Jump( 0, fShowAll:true );
            }
            return false;
        }

        public object Decorate(IPgViewSite oBaseSite, Guid sGuid) {
			try {
			} catch ( Exception oEx ) {
				Type[] rgErrors = { typeof( NotImplementedException ),
									typeof( NullReferenceException ),
									typeof( ArgumentException ),
									typeof( ArgumentNullException ) };
				if( rgErrors.IsUnhandled( oEx ) )
					throw;

				//LogError( "decor", "Couldn't create EditWin decor: " + sGuid.ToString() );
			}

            return( null! );
        }

        public bool Save(XmlDocumentFragment oStream) {
            return true;
        }
    }
}
