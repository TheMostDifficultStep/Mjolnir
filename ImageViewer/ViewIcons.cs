using System;
using System.Collections.Generic;
using System.Windows.Forms;
using System.IO;
using System.Drawing;
using System.Xml;

using Play.Interfaces.Embedding;
using Play.Rectangles;
using Play.Edit;
using Play.Controls;
using Play.Forms;

using SkiaSharp;
using SkiaSharp.Views.Desktop;

namespace Play.ImageViewer {
    public abstract class ImageLineBase : LayoutImageBase {
        public Line Source { get; }
        public ImageLineBase( Line oSource, SKSize szSize ) : base( szSize ) {
            Source = oSource ?? throw new ArgumentNullException( "Source must not be null" );
        }

        public abstract void Paint( SKCanvas skCanvas, IPgStandardUI2 oStdUI, SKPoint oTopLeft );
    }
    /// <summary>
    /// Since this object is being used as a view, we don't dispose of the bitmap!!
    /// that needs to be the job of the document, since many views might be sharing
    /// the thumbnail!!
    /// </summary>
    public class ImageLineRect : ImageLineBase
    {
        public override SKBitmap Icon => (SKBitmap)Source.Extra;

        /// <exception cref="ArgumentNullException" />
        /// <exception cref="InvalidCastException" />
        public ImageLineRect( Line oSource, SKSize skSize ) : base( oSource, skSize ) {
            World = new SKRectI( 0, 0, 0, 0 );

            this.SetRect( LOCUS.UPPERLEFT, 0, 0, (int)skSize.Width, (int)skSize.Height );
        }

		/// <summary>
		/// This is a special case draw for scrolling content. This way we don't need
		/// to update every element's position just because a scroll happened. Also
        /// includes the Text Colors so the call can be the same as the DirectoryRect's.
        /// TODO: Maybe I should pass a SKPaint object as well... hmmm.
		/// </summary>
		/// <param name="oTopLeft">Allows us to scroll.</param>
        public override void Paint( SKCanvas skCanvas, IPgStandardUI2 oStdUI, SKPoint oTopLeft ) {
            if( Icon == null ) {
                // Used to paint bg even if Icon is null, but I was getting weird paint errors
                // white rectangles over the normal working thumbs. Perhaps those where thumbs
                // not genderated yet and thus not layed out? 
			    PaintBackground( skCanvas, oTopLeft );

                return;
            }

            World = new SKRectI( 0, 0, Icon.Width, Icon.Height );
            ViewPortSizeMax( ViewPort ); // TODO: Need to look at placement of this call...

            try {
                SmartRect oTarget = 
                    new SmartRect( LOCUS.UPPERLEFT, 
                                   ViewPort.Left,
                                   ViewPort.Top - (int)oTopLeft.Y,
                                   ViewPort.Width, 
                                   ViewPort.Height );

				skCanvas.DrawBitmap( Icon, 
									 new SKRect( 0, 0, World.Width, World.Height ),
									 new SKRect( ViewPort.Left, ViewPort.Top - oTopLeft.Y, ViewPort.Right, ViewPort.Bottom - oTopLeft.Y ) );
            } catch( NullReferenceException ) {
            }
        }
    }

    public class DirectoryRect : ImageLineBase {
        public  FTCacheLine Text { get; }
		private readonly SKColor _oFolderColor;

        public DirectoryRect( FTCacheLine oCache, SKSize skSize, SKColor oFolderColor ) :
            base( oCache.Line, skSize ) 
        {
            Text          = oCache ?? throw new ArgumentNullException( "Text Cache must not be null!" );
			_oFolderColor = oFolderColor;
        }

        public override void Paint( SKCanvas skCanvas, IPgStandardUI2 oStdUI, SKPoint oTopLeft ) {
            SmartRect oRectCopy  = new SmartRect( this );
           
            oRectCopy.SetScalar( SET.RIGID, SCALAR.TOP, oRectCopy.Top - (int)oTopLeft.Y );

            using( SKPaint skPaint = new SKPaint() ) {
                skPaint .Color = _oFolderColor;
				skCanvas.DrawRect( new SKRect( oRectCopy.Left, oRectCopy.Top, oRectCopy.Right, oRectCopy.Bottom ), skPaint ); 

			    oRectCopy.Inflate( -1, 8 );
                skCanvas.Save();
                skCanvas.ClipRect( new SKRect( oRectCopy.Left, oRectCopy.Top, oRectCopy.Right, oRectCopy.Bottom ), SKClipOperation.Intersect );
                SKPointI pntUL = oRectCopy.GetPoint(LOCUS.UPPERLEFT);
                Text.Render( skCanvas, oStdUI, new PointF( pntUL.X, pntUL.Y ) );
                skCanvas.Restore();
            }
        }
    }

    public class ImageViewIcons : ImageViewMulti,
        IPgLoad<XmlElement>,
        IPgSave<XmlDocumentFragment>,
        IPgCommandView
    {
        protected readonly CacheBase2      _oTextCache     = new CacheBase2();
        protected readonly SmartRect       _oTextRect      = new SmartRect();
        protected readonly ScrollBar2      _oScrollBarVirt;
        protected readonly List<ImageLineBase> _rgThumbs       = new List<ImageLineBase>();
        protected          uint            _uiStdText      = 0;

        protected       int  _iImgHeight   = 100;
        protected const int  _iMarginLeft  = 5;
        protected const int  _iMarginRight = 5;
        protected const int  _iMarginTop   = 5;
        protected const int  _iMarginBot   = 5;
		protected    SKColor _clrFolder = new SKColor( 245, 220, 125 );
		protected    SKColor _clrFile   = new SKColor( 182, 255, 237 ); // We'll actually make an icon in the future.

        public ImageWalkerDoc Document { get { return _oDocument; }  }
        public override string Banner => "[" + _oDocument.Banner + "]";
        public virtual SKBitmap Icon { get; } = null;


	    protected class DocSlot :
		    IPgBaseSite
	    {
		    protected readonly ImageViewIcons _oHost;

		    public DocSlot( ImageViewIcons oHost ) {
			    _oHost = oHost ?? throw new ArgumentNullException();
		    }

		    public IPgParent Host => _oHost;

		    public void LogError(string strMessage, string strDetails, bool fShow=true) {
			    _oHost._oViewSite.LogError( strMessage, strDetails, fShow );
		    }

		    public void Notify( ShellNotify eEvent ) {
		    }
	    } // End class

        public ImageViewIcons( IPgViewSite oBaseSite, ImageWalkerDoc oDoc ) : base( oBaseSite, oDoc ) {
            Cursor = Cursors.Hand;

			//new LayoutFlowUniform( new Size( 50, 50 ), _rgThumbs );
            _oScrollBarVirt      = new ScrollBar2( new DocSlot( this ) );
            _oScrollBarVirt.Dock = DockStyle.Left;

            Controls.Add(_oScrollBarVirt);            
        }

		/// <remarks>Sadly, if we don't get disposed properly we won't dispose at all. Check out the new dispose pattern.
        /// Although, we're pretty good about disposing views from the main window.</remarks>
        protected override void Dispose(bool disposing) {
			if( disposing ) {
				_oDocument.ThumbsUpdated -= OnThumbsUpdated;
				_oDocument.ImageUpdated  -= OnImageUpdated;
				_oDocument.TextParsed    -= OnTextParsed;
                _oDocument.TextLoaded    -= OnTextLoaded;
				_oScrollBarVirt.Scroll   -= OnScrollBar;
			}
            base.Dispose(disposing);
        }

        public bool InitNew() {
			// This is a poster child example of why we don't set callbacks in the constructor.
			// If we throw an exception in the constructor, and we'll have a half constructed 
            // invalid object getting callbacks from outside!
            _oDocument.ThumbsUpdated += OnThumbsUpdated;
            _oDocument.ImageUpdated  += OnImageUpdated;
            _oDocument.TextParsed    += OnTextParsed;
            _oDocument.TextLoaded    += OnTextLoaded;
			_oScrollBarVirt.Scroll   += OnScrollBar;

            IPgMainWindow.PgDisplayInfo oInfo = new IPgMainWindow.PgDisplayInfo();
            if( _oViewSite.Host.TopWindow is IPgMainWindow oMainWin ) {
                oInfo = oMainWin.MainDisplayInfo;
            }

            SKSize sRez = new SKSize( oInfo.pntDpi.X, oInfo.pntDpi.Y );

            _uiStdText = _oStdUI.FontCache( _oStdUI.FaceCache( @"C:\windows\fonts\consola.ttf" ), 12, sRez );
            _oStdUI.FaceCache(@"C:\Windows\Fonts\UDDigiKyokashoN-R.ttc"); // BUG: Let's add this at program level for fallback.

            OnTextLoaded(); // the text is available, the thumbs will come along later.
            // BUG: But our size isn't set in our decor slot. So active element scroll doesn't work.

            return( true );
        }

        public bool Load( XmlElement xmlRoot ) {
            if( !InitNew() )
                return( false );

            return( true );
        }

        public bool IsDirty { 
            get { return( false ); } 
        }
        
        public bool Save( XmlDocumentFragment xmlRoot ) {
            return( true );
        }

		protected void ScrollActiveVisible() {
			// BUG: Note we call this same type of code on decor direction. Make
			//      sure we're not battling to update the image.
            try {
                foreach( ImageLineBase oImage in _rgThumbs ) {
                    if( oImage.Source == _oDocument.CurrentElement ) {
                        SmartRect oTarget = new SmartRect( oImage );

                        oTarget.Inflate( +1, 5 );

					    if( !_oTextRect.IsInside( oTarget )) {
						    _oTextRect.SetScalar(SET.RIGID, SCALAR.TOP, oTarget.Top );
					    }
                    }
                }
            } catch( NullReferenceException ) {
            }

            TextRectFixBounds();
		}

        public void OnImageUpdated() { 
            _oViewSite.Notify( ShellNotify.BannerChanged );

			ScrollActiveVisible();
            Invalidate();
        }

        /// <summary>
        /// Once the text has been loaded, we can set up our thumbs viewers even if the
        /// thumbs have not been loaded yet. But all we need to do is do a screen refresh
        /// after this.
        /// </summary>
        /// <see cref="OnThumbsUpdated"/>
        private void OnTextLoaded() {
            ThumbsPopulate();
            ThumbsLayout();
            ThumbsTextUpdate();

            _oTextRect.SetScalar(SET.RIGID, SCALAR.TOP, 0 );
		    ScrollActiveVisible();
            Invalidate();
        }

        public void OnThumbsUpdated() {
            // Bug: Maybe Populate and Layout?
            ThumbsTextUpdate();
            Invalidate();
        }

        /// <summary>
        /// 10/27/2020: We only need a repaint when the text get's parsed since that'll only
        /// affect the colorization of the text. We can't yet share Formatting collection
        /// with the Word collection. I'm liking the robustness of the seperated parses.
        /// </summary>
        public void OnTextParsed() {
            Invalidate();
        }

        public void OnDecorDirection( int iLine ) {
            try {
                _oDocument.ImageLoad( _oDocument[iLine] );
				ScrollActiveVisible();
            } catch( Exception ) {
                Type[] rgErrors = { typeof( NullReferenceException ),
                                    typeof( ArgumentOutOfRangeException ) };
            }
        }

        protected override void OnGotFocus(EventArgs e) {
            base.OnGotFocus( e );

            _oScrollBarVirt.Show( SHOWSTATE.Active );

            _oViewNotify.NotifyFocused( true );

            this.Invalidate();
        }

        protected override void OnLostFocus(EventArgs e) {
            base.OnLostFocus( e );

            _oScrollBarVirt.Show( SHOWSTATE.Inactive );

            _oViewNotify.NotifyFocused( false );

            this.Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e) {
            base.OnMouseDown(e);

            this.Select();
        }

        protected override void OnMouseUp(MouseEventArgs e) {
            base.OnMouseUp(e);

            if( e.Button == System.Windows.Forms.MouseButtons.Left ) {
				ImageLineBase oFound = null;
                foreach( ImageLineBase oThumb in _rgThumbs ) {
                    if( oThumb.IsInside( e.X, e.Y + _oTextRect.Top ) ) {
						oFound = oThumb;
                    }
                }
				if( oFound != null ) {
                    try {
					    if( oFound is DirectoryRect oDir ) {
						    _oDocument.LoadAgain( Path.Combine( _oDocument.CurrentDirectory, oDir.Source.ToString() ) );
					    } else {
						    _oDocument.ImageLoad( oFound.Source );
						    ShowSoloView();
					    }
                    } catch( Exception oEx ) {
                        Type[] rgErrors = { typeof( ApplicationException ),
                                            typeof( ArgumentException ),
                                            typeof( ArgumentNullException ),
                                            typeof( NullReferenceException ) };
                        if( rgErrors.IsUnhandled( oEx ) )
                            throw;

                        LogError( "viewicon", "Unable to process mouse event" );
                    }
				}
            }
        }

        protected override void OnMouseMove(MouseEventArgs e) {
            base.OnMouseMove(e);

            Cursor[] rgCursors = { Cursors.Arrow, Cursors.Hand };
            int      iCursor   = 0;

            // The thumbs are layed out in actual screen coordinates. That is,
            // the scroll bar occupies client space. I hate it, but there it is.
            foreach( SmartRect oThumb in _rgThumbs ) {
                if( oThumb.IsInside( e.X, e.Y + _oTextRect.Top ) ) {
                    iCursor = 1;
                }
            }

            this.Cursor = rgCursors[iCursor];
        }

        protected override void OnMouseWheel(MouseEventArgs e) {
            base.OnMouseWheel( e );
            
            int iTopOld = _oTextRect.GetScalar( SCALAR.TOP );
            int iTopNew = iTopOld - ( ( e.Delta / 120 ) * ( _iImgHeight + _iMarginBot + _iMarginTop ) );
            
            _oTextRect.SetScalar(SET.RIGID, SCALAR.TOP, iTopNew );

            TextRectFixBounds();
            RefreshScrollBar();

            Invalidate();
        }

        protected override void OnKeyDown(KeyEventArgs e) {
            if( this.IsDisposed )
                return;

            e.Handled = true;

            switch( e.KeyCode ) {
                case Keys.Left:
                    //if( e.Control )
                    //    _oViewSite.OnCommandKey( CommandKey.Left, KeyBoardEnum.Control );
                    //else
                        _oDocument.Next( -1 );
                    break;
                case Keys.Space:
                case Keys.Tab:
                case Keys.Right:
                    //if( e.Control )
                    //    _oViewSite.OnCommandKey( CommandKey.Right, KeyBoardEnum.Control );
                    //else
                        _oDocument.Next( +1 ); // Note: We'll get a OnNextImage() before we return!
                    break;

                default:
                    e.Handled = false;
                    break;
            }
       }

        protected override void OnKeyUp(KeyEventArgs e) {
            if( this.IsDisposed )
                return;

            e.Handled = true;

            switch( e.KeyCode ) {
                case Keys.Enter:
					if( _oDocument.CurrentElement is FileLine oFile ) {
						if( oFile._fIsDirectory ) {
                            try {
							    _oDocument.LoadAgain(Path.Combine(_oDocument.CurrentDirectory,oFile.ToString()));
                            } catch( Exception oEx ) {
                                Type[] rgErrors = { typeof( ApplicationException ),
                                                    typeof( ArgumentException ),
                                                    typeof( ArgumentNullException ),
                                                    typeof( NullReferenceException ) };
                                if( rgErrors.IsUnhandled( oEx ) )
                                    throw;

                                LogError( "viewicon", "Unable to process mouse event" );
                            }
						} else {
							FocusCenterView();
						}
					}
                    break;

                default:
                    e.Handled = false;
                    break;
			}
		}

        /// <summary>
        /// Make sure we haven't moved the rect above or below the displayable elements.
		/// BUG: Do this like the text screen so we scroll smoothly not jump to the top 
		///      as we scroll to the next screenfull.
        /// </summary>
        void TextRectFixBounds() {
            try {
                if( _oTextRect.Bottom > _rgThumbs[_rgThumbs.Count-1].Bottom + _iMarginBot ) {
                    _oTextRect.SetScalar( SET.RIGID, SCALAR.BOTTOM, _rgThumbs[_rgThumbs.Count-1].Bottom + _iMarginBot );
                }
                if( _oTextRect.Top < 0 ) {
                    _oTextRect.SetScalar( SET.RIGID, SCALAR.TOP, _rgThumbs[0].Top - _iMarginTop );
                }
            } catch( ArgumentOutOfRangeException ) {
            }
        }

        void TrackScrollThumb() {
            float flThumb = _oScrollBarVirt.Progress * _rgThumbs.Count;
            int   iIndex  = (int)flThumb;

            if( iIndex >= _rgThumbs.Count )
                iIndex = _rgThumbs.Count - 1;
            if( iIndex < 0 )
                iIndex = 0;

            _oTextRect.SetScalar( SET.RIGID, SCALAR.TOP, _rgThumbs[iIndex].Top - _iMarginTop );
        }

        /// <summary>
        /// Event handler for the vertical or horizontal scroll bar.
        /// </summary>
        void OnScrollBar( ScrollEvents e ) {
            int iHeight       = _oTextRect[ SCALAR.HEIGHT ];
            int iTopOld       = _oTextRect[ SCALAR.TOP    ];
            int iSafetyMargin = 10; // Probably should be some % of font height.

            switch( e ) {
                // These events move incrementally from where we were.
                case ScrollEvents.LargeDecrement:
                    _oTextRect.SetScalar(SET.RIGID, SCALAR.TOP, iTopOld - iHeight - iSafetyMargin );
                    break; 
                case ScrollEvents.SmallDecrement:
                    _oTextRect.SetScalar(SET.RIGID, SCALAR.TOP, iTopOld - _iImgHeight );
                    break;
                case ScrollEvents.LargeIncrement:
                    _oTextRect.SetScalar(SET.RIGID, SCALAR.TOP, iTopOld + iHeight - iSafetyMargin );
                    break;
                case ScrollEvents.SmallIncrement:
                    _oTextRect.SetScalar(SET.RIGID, SCALAR.TOP, iTopOld + _iImgHeight );
                    break;

                case ScrollEvents.First:
                    _oTextRect.SetScalar(SET.RIGID, SCALAR.TOP, 0 );
                    break;
                case ScrollEvents.Last:
                    if( _rgThumbs.Count > 0 ) {
                        _oTextRect.SetScalar(SET.RIGID, SCALAR.BOTTOM, _rgThumbs[_rgThumbs.Count-1].Bottom + _iMarginBot );
                    }
                    break;
                case ScrollEvents.ThumbPosition:
                    TrackScrollThumb();
                    break;
                case ScrollEvents.ThumbTrack:
                    TrackScrollThumb();
                    break;
            }

            TextRectFixBounds();
            RefreshScrollBar();

            Invalidate();
        }

		protected override void OnPaintSurface( SKPaintSurfaceEventArgs e ) {
            SKCanvas skCanvas         = e.Surface.Canvas;
            SKPoint  pntScreenTopLeft = new SKPoint( _oTextRect.Left, _oTextRect.Top );

            using SKPaint skPaint = new SKPaint();

            skPaint .Color = _oStdUI.ColorsStandardAt( StdUIColors.BGReadOnly );
            skCanvas.DrawRect( e.Info.Rect, skPaint );

            foreach( ImageLineBase oImage in _rgThumbs ) {
                if( oImage.IsIntersecting( _oTextRect ) ) {
                    try {
                        if( oImage.Source == _oDocument.CurrentElement ) { // If image is selected...draw contrasting background.
                            StdUIColors eColor  = ( this.Focused ) ? StdUIColors.BGSelectedFocus : StdUIColors.BGSelectedBlur;
                            SmartRect    rcFocus = new SmartRect( oImage );
                            rcFocus.SetScalar( SET.RIGID, SCALAR.TOP, rcFocus.Top - (int)pntScreenTopLeft.Y );
                            rcFocus.Inflate  ( 1, 5 );

                            skPaint .Color = _oStdUI.ColorsStandardAt( eColor );
                            skCanvas.DrawRect( new SKRect( rcFocus.Left, rcFocus.Top, rcFocus.Right, rcFocus.Bottom ), skPaint );
                        }
                        oImage.Paint( skCanvas, _oStdUI, pntScreenTopLeft );
                    } catch( Exception oEx ) {
                        Type[] rgErrors = { typeof( NullReferenceException ),
                                            typeof( ArgumentNullException ),
                                            typeof( ArgumentOutOfRangeException ),
                                            typeof( ArgumentException ) }; // this happens when image.bitmap got deleted and is invalid.

                        if( rgErrors.IsUnhandled( oEx ) )
                            throw;

                        if( _oDocument.ErrorBitmap == null ) {
                            LogError( "Image Icon Viewer", "Noticed that the error bitmap is not loaded.");
                        } else {
                            LayoutImage oError = new LayoutImage( _oDocument.ErrorBmp );

                            oError.SetPoint( SET.RIGID, LOCUS.UPPERLEFT, oImage.Top, oImage.Left );
                            oError.Paint( skCanvas );
                        }
                    }
                }
            }
        }

        void RefreshScrollBar() {
            float flVisible = 0;
            float flFirst   = -1;
            try {
                for( int i=0; i<_rgThumbs.Count; ++i ) {
                    ImageLineBase oImage = _rgThumbs[i];
                    if( oImage.IsIntersecting( _oTextRect ) ) {
                        ++flVisible;
                        if( flFirst < 0 )
                            flFirst = i;
                    }
                }
            } catch( NullReferenceException ) {
            }
            // BUG: This is wonky and needs fixing.
            _oScrollBarVirt.Refresh( flVisible / _rgThumbs.Count, flFirst / _rgThumbs.Count );
        }

        protected override void OnSizeChanged(EventArgs e) {
            base.OnSizeChanged(e);

            try {
                using( Graphics oGraphics = this.CreateGraphics() ) {
                    int iWidth = (int)(oGraphics.DpiX * 0.1875F ); 
                    _oTextRect.SetRect( LOCUS.UPPERLEFT, 
                                        iWidth + 1, _oTextRect.Top,
                                        this.Width - iWidth, this.Height );

                    _oScrollBarVirt.Width  = iWidth;
                    _oScrollBarVirt.Height = this.Height;
                }

                ThumbsLayout();
				TextRectFixBounds();
				RefreshScrollBar();

				Invalidate();
            } catch( ObjectDisposedException ) {
                // If I'm here, it's probably because I haven't shut down a view properly. 
                LogError( "internal", "ImageViewIcons improperly disposed!" );
            }
        }

        /// <summary>
        /// Look for new lines not represented by a thumbnail image and adds an ImageRect. 
        /// Note: When we load the view for the first time, the thumbs list will be empty,
        ///       even if the document's line has an associated thumb images preloaded.
        /// Note: This won't find or deal with duplicates. But there shouldn't be any.
        /// </summary>
        protected void ThumbsPopulate() {
            // Dump bitmap views that are no longer needed.
            ImageLineBase[] rgSortedThumb = new ImageLineBase[_oDocument.ElementCount];
            foreach( ImageLineBase oThumb in _rgThumbs ) {
                if( oThumb.Source.At > -1 )
                    rgSortedThumb[ oThumb.Source.At] = oThumb;
            }
            _rgThumbs  .Clear();
            _oTextCache.Clear();

            // Add a new bitmap view for lines we don't have a view on from the document.
            SKSize rcSize = new SKSize( _iImgHeight, _iImgHeight );
            for( int i=0; i < _oDocument.ElementCount; ++i ) {
                Line oLine = _oDocument[i];
                if( rgSortedThumb[i] == null && oLine is FileLine oFileLine ) {
                    ImageLineBase oNewItem = null;
                    if( oFileLine._fIsDirectory ) {
                        oNewItem = new DirectoryRect( new FTCacheWrap( oLine ), rcSize, _clrFolder );
                    } else {
						if( _oDocument.IsLineUnderstood( oLine ) ) {
							oNewItem = new ImageLineRect( oLine, rcSize );
						}
                    }
                    if( oNewItem != null ) // We need to create a unrecognized type too.
                        rgSortedThumb[i] = oNewItem;
                }
            }

            // Put our results back up in the working caches.
            foreach( ImageLineBase oThumb in rgSortedThumb ) {
                _rgThumbs.Add( oThumb );
				if (oThumb is DirectoryRect oRect) {
					_oTextCache.Add(oRect.Text);
				}
			}
        }

        protected void ThumbsTextUpdate() {
            using( IPgFontRender oFR = _oStdUI.FontRendererAt( _uiStdText ) ) {
                foreach( FTCacheLine oCache in _oTextCache ) {
                    Document.FileList.WordBreak(oCache.Line, oCache.Words); // BUG: Need to see why not see text on first boot.

                    oCache.Update( oFR );
                    oCache.OnChangeFormatting( null );
                    oCache.OnChangeSize( _iImgHeight - 16 ); // BUG remove hard coded deal.
                }
            }
        }

        /// <remarks>Should use the layout manager instead of this.</remarks>
        [Obsolete]void ThumbsLayout() {
            int iLeft   = _oTextRect.Left;
            int iTop    = _iMarginTop;
            int iHeight = 0;

            List<int> rgRow = new List<int>();

            // Try to figure out how many thumbs will fit on a row. We basically assume each
            // thumb is exactly the same width.
            try {
                for( int i = 0; i < _rgThumbs.Count; ) {
                    ImageLineBase oThumb = _rgThumbs[i];

                    iLeft += _iMarginLeft;

                    rgRow.Add( iLeft );

                    iLeft += oThumb.GetScalar(SCALAR.WIDTH);
                    iLeft += _iMarginRight;

                    iHeight = oThumb.GetScalar(SCALAR.HEIGHT);

                    if( ++i >= _rgThumbs.Count )
                        break;

                    oThumb = _rgThumbs[i];

                    if( iLeft + _iMarginLeft + oThumb.GetScalar(SCALAR.WIDTH) + _iMarginRight > _oTextRect.Right ) 
                        break;
                };
            } catch( NullReferenceException ) {
            }

            if( rgRow.Count < 1 )
                return;

            // This measures the columns to fit evenly with the space they fit in.
            float flDiff = _oTextRect.Right - iLeft;
            int   iIncr  = (int)(flDiff/(rgRow.Count+1));
            int   iAcum  = iIncr;

            for( int iCol = 0; iCol<rgRow.Count; ++iCol ) { 
                rgRow[iCol] += iAcum; 
                iAcum       += iIncr;
            }

            try {
                // Now apply our measurements to the thumbs.
                for( int i = 0; i < _rgThumbs.Count;  ) {
                    foreach( int iStart in rgRow ) {
                        _rgThumbs[i].SetPoint( SET.RIGID, LOCUS.UPPERLEFT, iStart, iTop );

                        if( ++i >= _rgThumbs.Count )
                            break;
                    }

                    iTop  += _iMarginTop;
                    iTop  +=  iHeight;
                    iTop  += _iMarginBot;
                };
            } catch( NullReferenceException ) {
            }

            RefreshScrollBar();
        }

        public object Decorate( IPgViewSite oBaseSite, Guid sGuid ) {
			try {
				if( sGuid == GlobalDecorations.Outline ) {
					//return( new TextWinReadOnly( this ) );
					return( new WindowSoloImageNav( oBaseSite, this._oDocument ) );
				}
				if( sGuid.Equals( GlobalDecorations.Properties ) ) {
					return( new WindowStandardProperties( oBaseSite, _oDocument.Properties ) );
				}
			} catch ( Exception oEx ) {
				Type[] rgErrors = { typeof( NotImplementedException ),
									typeof( NullReferenceException ),
									typeof( ArgumentException ),
									typeof( ArgumentNullException ) };
				if( rgErrors.IsUnhandled( oEx ) )
					throw;

				LogError( "decor", "Couldn't create EditWin decor: " + sGuid.ToString() );
			}

            return null;
        }

        public Guid Catagory {
            get {
                return( new Guid( "{BA3861FA-673A-4FD3-9B10-A403AB5F51BE}" ) );
            }
        }
    } // end class

    public class ImageViewIconsMain : ImageViewIcons {
		// Since ONLY the main ViewIcons window in the center has this open solo view behavior, where
		// the decor window does not, I think it's ok to have the dependency be on the view's site.
        protected readonly IPgShellSite _oSiteShell;
		protected static readonly Guid _gViewType = new Guid( "{BA3861FA-673A-4FD3-9B10-A403AB5F51BE}" );

        public ImageViewIconsMain( IPgViewSite oBaseSite, ImageWalkerDoc oDoc ) : base( oBaseSite, oDoc ) {
			_oSiteShell = oBaseSite as IPgShellSite ?? throw new ArgumentException( "Site must support IPgShellSite" );
        }

		protected override void ShowSoloView() {
			_oSiteShell.AddView( Guid.Empty, true );
		}

		protected override void FocusCenterView() {
			_oSiteShell.FocusCenterView();
		}

		public static Guid Guid => _gViewType;
    }

    /// <summary>
    /// Normally the outline for our icons window is the CurrentImage of the document.
    /// however I used to have a text view of all the file text. I might make this
    /// a different decor in the future.
    /// </summary>
    class TextWinReadOnly : EditWindow2 {
        Navigation _oNavDelegate;

        public TextWinReadOnly( IPgViewSite oBaseSite, ImageViewIcons oViewOwner ) :
			base( oBaseSite, oViewOwner.Document.FileList )
        {
            Cursor   = Cursors.Hand;
            ReadOnly = true;

            // We'll still get a line event in the owner view, but it'll be where we already are.
            CaretPos.Line   = oViewOwner.Document.CurrentElement;
            CaretPos.Offset = 0;
            
            _oNavDelegate = new Navigation( oViewOwner.OnDecorDirection );
            LineChanged += _oNavDelegate;
        }

        protected override void Dispose(bool disposing) {
            if( disposing ) {
                LineChanged -= _oNavDelegate;
            }

            base.Dispose(disposing);
        }
    }
} // end namespace 