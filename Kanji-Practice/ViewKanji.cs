using System.Xml;
using System.Windows.Forms;

using Play.Interfaces.Embedding;
using Play.Edit;
using Play.Forms;
using Play.Rectangles;
using Play.ImageViewer;
using Play.Drawing;

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
            /*
            SKPoint skCenter = new SKPoint( Document.Bitmap.Width  / 2,
                                            Document.Bitmap.Height / 2 );
            double dblRadius = skCenter.X < skCenter.Y ? skCenter.X : skCenter.Y;

            SKPoint skCurr = new SKPoint();
            SKPoint skPrev = new SKPoint( skCenter.X, skCenter.Y + (float)dblRadius ); // Cos(0)=1

            for( double dblAngle = 10; dblAngle <= 360; dblAngle += 10 ) {
                double dblRad = dblAngle / 180 * Math.PI;

                skCurr.X = (float)(skCenter.X + dblRadius * Math.Sin( dblRad ));
                skCurr.Y = (float)(skCenter.Y + dblRadius * Math.Cos( dblRad ));

                _oCanvas.DrawLine( skPrev, skCurr, _oPaint );

                skPrev = skCurr;
            }
            */
        }

        protected override void OnMouseDown(MouseEventArgs e) {
            base.OnMouseDown(e);

            if( e.Button == MouseButtons.Left ) {
                _pntPrevious = new SKPoint( e.X * _pntAspect.X, e.Y * _pntAspect.Y );
            }
        }

        protected override void OnMouseMove(MouseEventArgs e) {
            Cursor = Cursors.Arrow;
            if( e.Button == MouseButtons.Left ) {
                SKPoint pntNext = new SKPoint( e.X * _pntAspect.X, e.Y * _pntAspect.Y );

                _oCanvas.DrawLine( _pntPrevious, pntNext, _oPaint );
                _pntPrevious = pntNext;
                Invalidate();
            }
        }
    }

    internal class ViewKanjiProps : WindowStandardProperties {
        protected uint BigFont { get; } 
        protected ImageViewSingle ViewScratch { get; }
        protected EditWindow2     ViewMeaning { get; }

        public ViewKanjiProps( IPgViewSite oSite, KanjiDocument oKanjiDoc ) : 
            base( oSite, oKanjiDoc.Properties ) 
        {
            IPgMainWindow.PgDisplayInfo oInfo = new IPgMainWindow.PgDisplayInfo();
            if( _oSiteView.Host.TopWindow is IPgMainWindow oMainWin ) {
                oInfo = oMainWin.MainDisplayInfo;
            }
            BigFont = StdUI.FontCache( StdFace, 30, oInfo.pntDpi );

            ViewScratch = new ViewScratchPad( new WinSlot( this ), oKanjiDoc.ScratchPad );
            ViewMeaning = new EditWindow2   ( new WinSlot( this ), oKanjiDoc.Meanings ) { ScrollVisible = false };
        }

        public override void InitRows() {
			int[] rgShow = { 
				(int)KanjiPropEnum.Kanji,
                (int)KanjiPropEnum.Hiragana
			};

			base.InitRows( rgShow );

			try {
				PropertyInitRow( (int)KanjiPropEnum.Meaning, ViewMeaning );
				PropertyInitRow( (int)KanjiPropEnum.Scratch, ViewScratch );
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

            ViewScratch.Parent = this;
            ViewMeaning.Parent = this;
            
            _oScrollBarVirt.Visible = false;

            Line oKanjiLine = Document.ValueAsLine( (int)KanjiPropEnum.Kanji    );
            Line oHiragLine = Document.ValueAsLine( (int)KanjiPropEnum.Hiragana );

            if( _oCacheMan is CacheMultiFixed oCacheMulti ) {
                foreach( CacheRow oCRow in oCacheMulti.FixedCache ) {
                    if( oCRow[1] is FTCacheLine oCElem ) {
                        if( oCElem.Line == oKanjiLine ) {
                            oCElem.FontID = BigFont;
                        }
                        if( oCElem.Line == oHiragLine ) {
                            oCElem.FontID = BigFont;
                        }
                    }
                }
            }

            //OnDocumentEvent( BUFFEREVENTS.FORMATTED );
            Invalidate();

            return true;
        }
    }

    /// <summary>
    /// Basically a simple wrapper around the Property Page.
    /// </summary>
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

        public string    Banner => "Kanji Cards";
        public SKBitmap  Icon => null!;
        public Guid      Catagory => Guid.Empty;
        public bool      IsDirty => false;

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
            base( oViewSite, oMonitorDoc.Meanings /* just give it something... */) 
        {
            KanjiDoc      = oMonitorDoc ?? throw new ArgumentNullException( "Monitor document must not be null!" );

            Layout        = new LayoutStackHorizontal() { Units = LayoutRect.CSS.None };
            CenterDisplay = new ViewKanjiProps( new ViewSlot( this ), KanjiDoc ) ;

            CenterDisplay.Parent = this;
        }

        protected override void Dispose(bool disposing) {
            if( disposing ) {
                //KanjiDoc.RefreshScreen -= OnRefreshScreen_MonDoc;
            }
            base.Dispose(disposing);
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

            return true;
        }

        public bool Load(XmlElement oStream) {
            return InitNew();
        }

        protected override void OnKeyDown(KeyEventArgs e) {
            base.OnKeyDown(e);

            if( e.KeyCode == Keys.F2 ) {
            }
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
            return null!;
        }

        public bool Save(XmlDocumentFragment oStream) {
            return true;
        }
    }
}
