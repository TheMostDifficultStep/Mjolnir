using System;
using System.Windows.Forms;
using System.Drawing;

using SkiaSharp;
using SkiaSharp.Views.Desktop;
// https://docs.microsoft.com/en-us/dotnet/api/skiasharp.views.desktop?view=skiasharp-views-1.68.1

using Play.Interfaces.Embedding;
using Play.Rectangles;
using Play.Edit;

namespace Play.ImageViewer {
	/// <remarks>
	/// There are probably a few places I'm using this control, when I could probably use the
	/// lighter weight LayoutImage class.
	/// </remarks>
	/// <seealso cref="LayoutImage"/>
	public class LayoutImageView : LayoutRect {
		ImageViewSingle _oView;

		/// <summary>
		/// Uses the flex option on the image. Image will only ask for as much track preserving
		/// aspect, as the rails will allow.
		/// </summary>
		public LayoutImageView( ImageViewSingle oView, float flMaxFraction ) : base( LayoutRect.CSS.Flex, 0, flMaxFraction ) {
			_oView = oView ?? throw new ArgumentNullException();

			this.SizeEvent += OnSizeEvent;
		}

		/// <summary>
		/// Share all the available space with the other layout elements.
		/// </summary>
		public LayoutImageView( ImageViewSingle oView ) : base( LayoutRect.CSS.None, 0, 1 ) {
			_oView = oView ?? throw new ArgumentNullException();

			this.SizeEvent += OnSizeEvent;
		}

		public override uint TrackDesired( TRACK eParentAxis, int iRailExtent ) {
			try {
				if( iRailExtent <= 0 )
					return 0;

				if( _oView.Document.Bitmap == null )
					return (uint)iRailExtent;

				Size  oBmpSize = new Size( _oView.Document.Bitmap.Width, _oView.Document.Bitmap.Height);
				float flAspect = (float)oBmpSize.Width / (float)oBmpSize.Height;

				return ExtentDesired( flAspect, (uint)iRailExtent, eParentAxis );
			} catch( Exception oEx ) {
				Type[] rgErrors = { typeof( NullReferenceException ),
									typeof( DivideByZeroException ) };
				if( rgErrors.IsUnhandled( oEx ) )
					throw oEx;

				_oView.LogError( "math", "Could not compute ExtentDesired for this image view." );

				return 0;
			}
		}

		private void OnSizeEvent(SmartRect o) {
			_oView.Bounds = this.Rect;
		}
	}

    public class ImageViewBase : SKControl, IPgParent {
        protected readonly IPgViewSite    _oViewSite;
        protected readonly IPgViewNotify  _oViewNotify;
		protected readonly IPgStandardUI2 _oStdUI;
		protected          Size           _whBorder = new Size( 0, 0 ); // 7 px all the way around X 2.

        readonly static Keys[] _rgHandledKeys = { Keys.PageDown, Keys.PageUp, Keys.Down,
                                                  Keys.Up, Keys.Right, Keys.Left, Keys.Back,
                                                  Keys.Delete, Keys.Enter, Keys.Tab, 
                                                  Keys.Control | Keys.A, Keys.Control | Keys.F };

        public IPgParent Parentage => _oViewSite.Host;
        public IPgParent Services  => Parentage.Services;

        public ImageViewBase( IPgViewSite oViewSite ) {
            DoubleBuffered = true;

            _oViewSite   = oViewSite ?? throw new ArgumentNullException( "Site must not be null" );
            _oViewNotify = oViewSite.EventChain ?? throw new ArgumentException( "Site must support EventChain" );
			_oStdUI      = oViewSite.Host.Services as IPgStandardUI2 ?? throw new ArgumentException( "Site must support IPgStandardUISite" );

            Array.Sort<Keys>( _rgHandledKeys );
        }

        protected override bool IsInputKey(Keys keyData) {
            int iIndex = Array.BinarySearch<Keys>( _rgHandledKeys, keyData );
            
            if( iIndex >= 0 )
                return true;
                
            return base.IsInputKey( keyData );
        }

        protected virtual void ShowSoloView   () { } // When in center, open full view on icon.
		protected virtual void FocusCenterView() { } // When we are decor, send focus back to center.

        public void LogError( string strCatagory, string strDetails ) {
            _oViewSite.LogError( strCatagory, strDetails );
        }

        protected override void OnGotFocus(EventArgs e) {
            base.OnGotFocus( e );

            _oViewNotify.NotifyFocused( true );

            Invalidate();
        }

        protected override void OnLostFocus(EventArgs e) {
            base.OnLostFocus(e);

            _oViewNotify.NotifyFocused( false );
        }
        
        protected bool ViewPortSizeMax( Bitmap oBitmap, SmartRect rctViewPort ) {
            if( oBitmap == null )
                return false;

			SmartRect rcBitmap = new SmartRect( 0, 0, oBitmap.Width, oBitmap.Height );

			ViewPortSizeMax( rcBitmap, rctViewPort );

			return true;
		}

        protected bool ViewPortSizeMax( SKBitmap oBitmap, SmartRect rctViewPort ) {
            if( oBitmap == null )
                return false;

			SmartRect rcBitmap = new SmartRect( 0, 0, oBitmap.Width, oBitmap.Height );

			ViewPortSizeMax( rcBitmap, rctViewPort );

			return true;
		}

		/// <summary>
        /// The viewport is the largest rectangle that will fit in the window provided but still
        /// have the same aspect ratio of the bitmap. But when we zoom, we want the aspect to match
        /// the window. I haven't got that fully sorted yet.
        /// </summary>
        /// <returns></returns>
        protected virtual void ViewPortSizeMax( SmartRect rctBitmap, SmartRect rctViewPort ) 
        {
            // If we don't have a bitmap we can't do much about setting our viewport.
			Size  whWinSize      = new Size( this.Width - _whBorder.Width, this.Height - _whBorder.Height );
            float flImageAspect  = rctBitmap.Width / (float)rctBitmap.Height;
            float flWindowAspect = whWinSize.Width / (float)whWinSize.Height;

			// Calculate the new image viewport.
			Size pntBmpSize = new Size( rctBitmap.Width, rctBitmap.Height );

			if ( flWindowAspect > flImageAspect ) {
                // Window is wide and squat compared to bitmap.
                if( pntBmpSize.Height > whWinSize.Height ) {
                    // image takes up entire height.
                    pntBmpSize.Height = whWinSize.Height;
                    pntBmpSize.Width  = (int)(whWinSize.Height * flImageAspect);
                }
            } else {
                // Window is tall and narrow compared to bitmap.
                if( pntBmpSize.Width > whWinSize.Width ) {
                    // image takes up entire width.
                    pntBmpSize.Width  = whWinSize.Width;
                    pntBmpSize.Height = (int)(whWinSize.Width / flImageAspect);
                }
            }

			Point pntUpperLeft = new Point {
				X = ((whWinSize.Width  - pntBmpSize.Width  + _whBorder.Width  ) / 2 ),
				Y = ((whWinSize.Height - pntBmpSize.Height + _whBorder.Height ) / 2 )
			};

			rctViewPort.SetRect( LOCUS.UPPERLEFT, pntUpperLeft.X, pntUpperLeft.Y, pntBmpSize.Width, pntBmpSize.Height );
        }

        /// <summary>
        /// Just center the viewport in the middle of the window, same size as bitmap.
        /// </summary>
        protected bool ViewPortSizeCenter( Bitmap oImage, SmartRect rctViewPort ) 
        {
            // If we don't have a bitmap we can't do much about setting our error viewport.
            if( oImage == null )
                return( false );

			if( oImage.Width > Width || oImage.Height > Height ) {
				ViewPortSizeMax( oImage, rctViewPort );
				return( true );
			}

            rctViewPort.SetRect( LOCUS.CENTER, Width / 2, Height / 2, oImage.Width, oImage.Height );

            return( true );
        }

        protected bool ViewPortSizeCenter( SKBitmap oImage, SmartRect rctViewPort ) 
        {
            // If we don't have a bitmap we can't do much about setting our error viewport.
            if( oImage == null )
                return( false );

			if( oImage.Width > Width || oImage.Height > Height ) {
				ViewPortSizeMax( oImage, rctViewPort );
				return( true );
			}

            rctViewPort.SetRect( LOCUS.CENTER, Width / 2, Height / 2, oImage.Width, oImage.Height );

            return( true );
		}
    } // end class

	public class ImageViewMulti : ImageViewBase {
        protected readonly ImageWalkerDoc _oDocument; 

		public ImageViewMulti( IPgViewSite oSiteBase, ImageWalkerDoc oDoc ) : base( oSiteBase ) {
            _oDocument = oDoc ??  throw new ArgumentNullException( "ImageView needs pointer to Image Document." );
		}

        public virtual string Banner => _oDocument.Banner;
        public virtual Image  Iconic => _oDocument.Iconic;
        public virtual bool   Execute( Guid sGuid ) => _oDocument.Execute( sGuid );
	}
	
	/// <summary>
	/// Though we inherit from control (at present) this viewer just shows a bitmap.
	/// There is no navigation control. In the future we would like inherit to from SmartRect.
	/// But that might not be possible for a variety of reasons.
	/// </summary>
	public class ImageViewSingle : ImageViewBase, IPgParent {
		protected bool _fDisposed = false;

		public SmartRect Selection { get; } = new SmartRect();   // selection in Bmp coordinates.

        protected readonly SmartRect _rctViewPort    = new SmartRect( LOCUS.UPPERLEFT, 0, 0, 0, 0 );
        protected readonly SmartRect _rctWorldPort   = new SmartRect( LOCUS.UPPERLEFT, 0, 0, 0, 0 );

		public ImageSoloDoc Document { get; }
	  //public IPgParent    Parentage => _oViewSite.Host;
	  //public IPgParent    Services  => Document.Parentage.Services;

		public ImageViewSingle( IPgViewSite oSiteBase, ImageSoloDoc oDocSolo ) : base( oSiteBase ) {
			Document = oDocSolo ?? throw new ArgumentNullException( "Document must not be null." );
		}

        public virtual bool InitNew() {
			if( Parentage is Control oParent ) {
				Parent = oParent;
			}

            if( Document.Bitmap != null ) {
                _rctWorldPort.SetRect( LOCUS.UPPERLEFT, 0, 0, Document.Bitmap.Width, Document.Bitmap.Height);
            }
            Document.ImageUpdated += OnImageUpdated;

			return( true );
		}

		protected override void Dispose( bool fDisposing ) {
			if( fDisposing && !_fDisposed ) {
				Document.ImageUpdated -= OnImageUpdated;

				_fDisposed = true;
			}
			base.Dispose( fDisposing );
		}

 		public SmartRect WorldCoordinates {
			set {
				_rctWorldPort.Copy = value; 
				ViewPortSizeMax( _rctWorldPort, _rctViewPort );
				Invalidate();
			}
			get {
				return _rctWorldPort.Copy;
			}
		}

        protected virtual void OnImageUpdated() {
            try {
				if( Document.Bitmap != null ) {
					_rctWorldPort.SetRect( LOCUS.UPPERLEFT, 0, 0, Document.Bitmap.Width, Document.Bitmap.Height);
				} else {
					_rctWorldPort.SetRect( LOCUS.UPPERLEFT, 0, 0, 0, 0 );
				}
            } catch( NullReferenceException ) {
            }
            OnSizeChanged( null );

            _oViewSite.Notify( ShellNotify.BannerChanged );
            Invalidate();
        }

		protected override void OnGotFocus(EventArgs e) {
			base.OnGotFocus(e);
		}

        protected override void OnPaintSurface( SKPaintSurfaceEventArgs e ) {
            base.OnPaintSurface(e);

			SKSurface skSurface = e.Surface;
			SKCanvas  skCanvas  = skSurface.Canvas;

			using( SKPaint skPaint = new SKPaint() ) {
				skPaint .Color = _oStdUI.ColorsStandardAt(StdUIColors.BGReadOnly);
				skCanvas.DrawRect( e.Info.Rect, skPaint );

                try {
                    if (Document.Bitmap != null) {
						skPaint.FilterQuality = SKFilterQuality.High;
                        skCanvas.DrawBitmap( Document.Bitmap,
											 new SKRect( _rctWorldPort.Left, _rctWorldPort.Top, _rctWorldPort.Right, _rctWorldPort.Bottom ),
											 new SKRect( _rctViewPort .Left, _rctViewPort .Top, _rctViewPort.Right,  _rctViewPort .Bottom ),
											 skPaint );
                    } else {
                        if (Document.ErrorBmp != null) {
                            ViewPortSizeCenter(Document.ErrorBmp, _rctViewPort);
                            _rctWorldPort.SetRect(LOCUS.UPPERLEFT, 0, 0,
                                                   Document.ErrorBmp.Width,
                                                   Document.ErrorBmp.Height);
							skCanvas.DrawBitmap( Document.ErrorBmp,
												 new SKRect( _rctWorldPort.Left, _rctWorldPort.Top, _rctWorldPort.Right, _rctWorldPort.Bottom ),
												 new SKRect( _rctViewPort.Left, _rctViewPort.Top, _rctViewPort.Right, _rctViewPort.Bottom ) );
                        } else {
                            LogError("Paint", "Couldn't paint error bitmap");
                        }
                    }
                } catch (Exception oEx) {
                    Type[] rgErrors = { typeof( ArgumentNullException ),
										typeof( ArgumentException ),
										typeof( NullReferenceException ),
										typeof( OverflowException ) };
                    if (rgErrors.IsUnhandled(oEx))
                        throw;

                    LogError("Paint", "Solo Image viewer having problem painting.");
				} finally {
					skCanvas.Flush ();
                }
			}
        }

		//protected void OnPaint( PaintEventArgs oE ) {
  //          // First paint our whole screen.
  //          SmartRect oRectWhole = new SmartRect( POINT.UPPERLEFT, 0, 0, this.Width, this.Height );

		//	try {
		//		using( Brush oBrush = new SolidBrush( LOGBRUSH.CreateColor( _oStdUI.ColorStandard( StdLineColor.BG ) ) ) ) {
		//			oE.Graphics.FillRectangle( oBrush, oRectWhole.Rect ); 
		//		}
		//	} catch( Exception oEx ) {
		//		Type[] rgErrors = { typeof( ArgumentNullException ),
		//							typeof( ArgumentException ),
		//							typeof( NullReferenceException ) };
		//		if( rgErrors.IsUnhandled( oEx ) )
		//			throw;

		//		LogError( "paint", "Solo Image viewe having problem painting the background." );
		//	}

  //          SmartRect rctTempView = _rctViewPort;

  //          //if( IsZoomed ) {
  //          //    rctTempView = oRectWhole;
  //          //}

		//	try {
		//		// Then Blit image.
		//		if( Document.Bitmap != null ) {
		//			oE.Graphics.DrawImage( Document.Bitmap,
		//								   rctTempView.Rect,
		//								   _rctWorldPort.Rect,
		//								   GraphicsUnit.Pixel);
		//		} else {
		//			if( Document.ErrorBmp != null ) {
		//				ViewPortSizeCenter( Document.ErrorBmp, _rctViewPort );
		//				_rctWorldPort.SetRect( POINT.UPPERLEFT,0,0,
		//									   Document.ErrorBmp.Width,
		//									   Document.ErrorBmp.Height);
		//				oE.Graphics.DrawImage( Document.ErrorBitmap,
		//									   _rctViewPort.Rect,
		//									   _rctWorldPort.Rect,
		//									   GraphicsUnit.Pixel);
		//			} else {
		//				LogError( "Paint", "Couldn't paint error bitmap" );
		//			}
		//		}
		//	} catch( Exception oEx ) {
		//		Type[] rgErrors = { typeof( ArgumentNullException ),
		//							typeof( ArgumentException ),
		//							typeof( NullReferenceException ),
		//							typeof( OverflowException ) };
		//		if( rgErrors.IsUnhandled( oEx ) )
		//			throw;

		//		LogError( "Paint", "Solo Image viewer having problem painting." );
		//	}
  //      }

        protected override void OnSizeChanged(EventArgs e) {
            base.OnSizeChanged(e);

			try {
				if( Document.Bitmap != null ) {
					ViewPortSizeMax( _rctWorldPort, _rctViewPort );
				} else {
					ViewPortSizeCenter(Document.ErrorBitmap, _rctViewPort);
					_rctWorldPort.SetRect( LOCUS.UPPERLEFT, 0, 0,
										   Document.ErrorBitmap.Width,
										   Document.ErrorBitmap.Height);
				}
			} catch( Exception oEx ) {
				Type[] rgErrors = { typeof( NullReferenceException ),
									typeof( ArithmeticException )
								  };
				if( rgErrors.IsUnhandled( oEx )) {
					LogError( "viewbase", "Resize error." );
				}
			}
            Invalidate();
        }

        public virtual bool Execute( Guid sGuid ) {
            return( Document.Execute( sGuid ) );
        }
	}

	public class ImageViewButton : ImageViewSingle {
		public Guid CommandClick     { get; set; }
		public Guid CommandWheelUp   { get; set; }
		public Guid CommandWheelDown { get; set; }

		readonly IPgCommandView _oHostCommand;

		public ImageViewButton( IPgViewSite oSiteView, ImageSoloDoc oDocument ) : 
			base( oSiteView, oDocument ) 
		{
			_oHostCommand = (IPgCommandView)oSiteView.Host;

			CommandClick  = Guid.Empty;

			Cursor = Cursors.Hand;
		}

		protected override void OnMouseClick( MouseEventArgs e ) {
			base.OnMouseClick( e );
			_oHostCommand.Execute( CommandClick );
		}

		protected override void OnMouseWheel(MouseEventArgs e) {
			base.OnMouseWheel( e );
			
			if( e.Delta > 0 )
				_oHostCommand.Execute( CommandWheelDown );
			else 
				_oHostCommand.Execute( CommandWheelUp );
		}
	}
}