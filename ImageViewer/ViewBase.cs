using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

using SkiaSharp;
using SkiaSharp.Views.Desktop;

using Play.Drawing;
using Play.Edit;
// https://docs.microsoft.com/en-us/dotnet/api/skiasharp.views.desktop?view=skiasharp-views-1.68.1

using Play.Interfaces.Embedding;
using Play.Rectangles;


namespace Play.ImageViewer {
    /// <summary>
    /// It's a pain. I still need both GDI and SKIA graphics. But I've
    /// managed to create a base class that puts most the work in one place. 
    /// </summary>
	/// <seealso cref="LayoutImageAbstract"/>
	public abstract class LayoutSimpleImage : LayoutRect {
		protected readonly SmartRect _rctViewPort = new SmartRect();

		public LayoutSimpleImage() {
			this.SizeEvent += OnSizeEvent;
		}
			
		/// <summary>
		/// What portion of the bitmap we want to show.
		/// </summary>
		public virtual SmartRect WorldCoordinates { get; } = new SmartRect();

		/// <summary>
		/// Amount of border around the view containing the portion
		/// of the image we are showing.
		/// </summary>
		public virtual Size Border { get; set; }

		public abstract float Aspect { get; }

		/// <summary>
		/// Back port this to LayoutImageView, It's unbelievably cool. 
		/// .ps remember to turn on CSS.Flex, or this doesn't get called.
		/// </summary>
		/// <seealso cref="LayoutImageView.TrackDesired(TRACK, int)"/>
		public override uint TrackDesired( TRACK eParentAxis, int iRailExtent ) {
			try {
				return ExtentDesired( Aspect, (uint)iRailExtent, eParentAxis );
			} catch( Exception oEx ) {
				Type[] rgErrors = { typeof( NullReferenceException ),
									typeof( DivideByZeroException ) };
				if( rgErrors.IsUnhandled( oEx ) )
					throw;

				return (uint)iRailExtent; // Try for square.
			}
		}

		/// <summary>
		/// We get set to some size or another, but the view on the bitmap is
		/// going to attempt to retain it's aspect, so It might be slightly 
		/// narrower or shorter than the Host LayoutRect.
		/// </summary>
		/// <param name="o">Old Size.</param>
		private void OnSizeEvent(SmartRect o) {
			ImageHelpers.ViewPortSizeMax( szBorder      : Border, 
										  szView        : new Size( Width, Height ), 
				                          rctBitmapWorld: WorldCoordinates,
										  rctViewPortOut: _rctViewPort );

			_rctViewPort.SetPoint( SET.RIGID, LOCUS.CENTER, 
								   Left + Width / 2, Top + Height / 2 );
		}
	}

	/// <summary>
	/// Think of this as a windowless view style control.
	/// Show the entire bitmap scaled and centered. We have
	/// the benefit of NOT replicating the base image for
	/// every view upon it.
	/// </summary>
	public class LayoutSKBitmap : LayoutSimpleImage {
		protected readonly ImageBaseDoc _oDocument;

		public LayoutSKBitmap( ImageBaseDoc oDocSolo ) : base() {
			_oDocument = oDocSolo ?? throw new ArgumentNullException();

			WorldCoordinates.SetRect( 0, 0, oDocSolo.Size.Width, oDocSolo.Size.Height );
		}
			
		/// <seealso cref="SmartRect.Paint(SKCanvas)"/>
		/// <seealso cref="LayoutImageReference.Paint(SKCanvas)"/>
		/// <seealso cref="ImageViewSingle.OnPaintSurface(SKPaintSurfaceEventArgs)"/>
		public override void Paint( SKCanvas skCanvas ) {
			if( _oDocument.Bitmap == null || Hidden )
                return;

            try {
				skCanvas.DrawBitmap( _oDocument.Bitmap, 
									 WorldCoordinates.SKRect,
									 _rctViewPort.SKRect
                                   );
            } catch( NullReferenceException ) {
            }
		}

        public override float Aspect => _oDocument.Aspect;
	}

    /// <remarks>
    /// There are probably a few places I'm using this control, when I could probably use the
    /// lighter weight LayoutImage class. And it turns out LayoutControl wraps a ImageViewSingle
    /// nicely and might just want to go to that...
    /// </remarks>
	/// <seealso cref="LayoutImage"/>
    /// <seealso cref="LayoutControl"/>
    public class LayoutImageView : LayoutRect {
		ImageViewSingle _oView;

		/// <summary>
		/// You can set Track using a constructor initializers, so this is a better constructor.
		/// </summary>
		public LayoutImageView( ImageViewSingle oView, LayoutRect.CSS eCSS ) : base( eCSS, 0, 1 ) {
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
					return (uint)iRailExtent; // Try for square.

				Size  oBmpSize = new Size( _oView.Document.Bitmap.Width, _oView.Document.Bitmap.Height);
				float flAspect = (float)oBmpSize.Width / (float)oBmpSize.Height;

				return ExtentDesired( flAspect, (uint)iRailExtent, eParentAxis );
			} catch( Exception oEx ) {
				Type[] rgErrors = { typeof( NullReferenceException ),
									typeof( DivideByZeroException ) };
				if( rgErrors.IsUnhandled( oEx ) )
					throw;

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

		// If the border is zero at first we used to bump it up for select all.
		// But now it seems I like it always on.
		protected                 Size _sBorder     = new Size(  0,  0 ); 
		protected readonly static Size _sGrabBorder = new Size( 14, 14 ); // 7 px all the way around X 2. match grab handles.

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

		/// <summary>
		/// This does not invalidate the windows. you must do that
		/// after calling this function. Id like this to be the case
		/// for everything, but icons don't need boarders so I
		/// can't have this on always.
		/// </summary>
		public void SetBorderOn() {
			_sBorder = _sGrabBorder;
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
			ImageHelpers.ViewPortSizeMax( _sBorder, new Size( Width, Height ), rctBitmap, rctViewPort );
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
                return false;

			if( oImage.Width > Width || oImage.Height > Height ) {
				ViewPortSizeMax( oImage, rctViewPort );
				return true;
			}

            rctViewPort.SetRect( LOCUS.CENTER, Width / 2, Height / 2, oImage.Width, oImage.Height );

            return true ;
		}
        protected bool ViewPortSizeCenter( SKImage oImage, SmartRect rctViewPort ) 
        {
            // If we don't have a bitmap we can't do much about setting our error viewport.
            if( oImage == null )
                return false ;

			if( oImage.Width > Width || oImage.Height > Height ) {
				SmartRect rcBitmap = new SmartRect( 0, 0, oImage.Width, oImage.Height );

				ViewPortSizeMax( rcBitmap, rctViewPort );

				return true;
			}

            rctViewPort.SetRect( LOCUS.CENTER, Width / 2, Height / 2, oImage.Width, oImage.Height );

            return true;
		}
    } // end class inherits SKControl

	public class ImageViewMulti : ImageViewBase {
        protected readonly ImageWalkerDoc _oDocument; 

		public ImageViewMulti( IPgViewSite oSiteBase, ImageWalkerDoc oDoc ) : base( oSiteBase ) {
            _oDocument = oDoc ??  throw new ArgumentNullException( "ImageView needs pointer to Image Document." );
		}

        public virtual string Banner => _oDocument.Banner;
        public virtual SKImage Icon => _oDocument.Icon;
        public virtual bool   Execute( Guid sGuid ) => _oDocument.Execute( sGuid );
	}
	
	public class ViewSinglePerportional : 
		ImageViewBase, 
		IPgParent
	{
		public bool _fDisposed { get; private set; } = false;

		public SmartRect Selection { get; } = new SmartRect();   // selection in Bmp coordinates.

        protected readonly SmartRect _rctViewPort    = new SmartRect( LOCUS.UPPERLEFT, 0, 0, 0, 0 );
        protected readonly SmartRect _rctWorldPort   = new SmartRect( LOCUS.UPPERLEFT, 0, 0, 0, 0 );

		IPgImageDocument _oDocBase;

		public ViewSinglePerportional( IPgViewSite oSiteBase, IPgImageDocument oDocBase ) : base( oSiteBase ) {
			_oDocBase = oDocBase ?? throw new ArgumentNullException( nameof( oDocBase ) );

			if( Parentage is Control oParent ) {
				Parent = oParent;
			}

		}

        public virtual bool InitNew() {
            if( _oDocBase.IsImageValid ) {
                _rctWorldPort.SetRect( LOCUS.UPPERLEFT, 0, 0, _oDocBase.ImageSize.Width, _oDocBase.ImageSize.Height);
            }
            _oDocBase.ImageUpdated += OnImageUpdated;

			OnImageUpdated();

			return( true );
		}

		protected override void Dispose( bool fDisposing ) {
			if( fDisposing && !_fDisposed ) {
				_oDocBase.ImageUpdated -= OnImageUpdated;
			}
			_fDisposed = true;
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
        protected override void OnMouseDown(MouseEventArgs e) {
            base.OnMouseDown(e);

            Select();
            Focus ();
		}

		protected override void OnGotFocus(EventArgs e) {
			base.OnGotFocus(e);

			Invalidate();
		}

        protected override void OnLostFocus(EventArgs e) {
            base.OnLostFocus(e);

			Invalidate();
        }

        public override Size GetPreferredSize(Size proposedSize) {
			SmartRect rctViewPort = new SmartRect();

			ImageHelpers.ViewPortFitWidth( new Size( 0, 0 ), 
										   proposedSize.Width, 
										   _rctWorldPort, 
										   rctViewPort );

            return new Size( rctViewPort.Width, rctViewPort.Height );
        }

        protected virtual void OnImageUpdated() {
            try {
				if( _oDocBase.IsImageValid ) {
					_rctWorldPort.CopyFrom( _oDocBase.WorldDisplay );
				} else {
					_rctWorldPort.SetRect( LOCUS.UPPERLEFT, 0, 0, 0, 0 );
				}
            } catch( Exception oEx ) {
				Type[] rgErrors = { typeof( AccessViolationException ),
									typeof( NullReferenceException ) };
				if( rgErrors.IsUnhandled( oEx ) )
					throw;

				LogError( "bitmap", "Serious memory error in image." );
            }
            OnSizeChanged( null );

            _oViewSite.Notify( ShellNotify.BannerChanged );
            Invalidate();
        }

        protected override void OnSizeChanged(EventArgs e) {
            base.OnSizeChanged(e);

			ViewPortSizeMax( _rctWorldPort, _rctViewPort );
            Invalidate();
        }
	}


	/// <summary>
	/// Though we inherit from control (at present) this viewer just shows a bitmap.
	/// There is no navigation control. In the future we would like inherit to from SmartRect.
	/// But that might not be possible for a variety of reasons.
	/// </summary>
	public class ImageViewSingle : ViewSinglePerportional {
		public ImageBaseDoc Document { get; }

		public ImageViewSingle( IPgViewSite oSiteView, ImageBaseDoc oDocSolo ) : base( oSiteView, oDocSolo ) {
			Document = oDocSolo ?? throw new ArgumentNullException( "Document must not be null." );
		}

		/// <seealso cref="LayoutImageView.Paint(SKCanvas)"/>
        protected override void OnPaintSurface( SKPaintSurfaceEventArgs e ) {
            base.OnPaintSurface(e);

			SKSurface         skSurface = e.Surface;
			SKCanvas          skCanvas  = skSurface.Canvas;

			using( SKPaint skPaint = new SKPaint() ) {
				if( Focused ) {
					skPaint.Color = _oStdUI.ColorsStandardAt(StdUIColors.BGSelectedLightFocus);
				} else { 
					skPaint.Color = _oStdUI.ColorsStandardAt(StdUIColors.BGReadOnly);
				}
				skCanvas.DrawRect( e.Info.Rect, skPaint );

                try {
                    if (Document.Bitmap != null) {
						using SKImage oImage = SKImage.FromBitmap( Document.Bitmap );
                        skCanvas.DrawImage( oImage,
										    new SKRect( _rctWorldPort.Left, _rctWorldPort.Top, _rctWorldPort.Right, _rctWorldPort.Bottom ),
										    new SKRect( _rctViewPort .Left, _rctViewPort .Top, _rctViewPort.Right,  _rctViewPort .Bottom ),
										    new SKSamplingOptions( SKFilterMode.Linear ),
										    skPaint );
                    } else {
                        if (Document.ErrorBmp != null) {
                            ViewPortSizeCenter( Document.ErrorBmp, _rctViewPort );
                            _rctWorldPort.SetRect( LOCUS.UPPERLEFT, 0, 0,
                                                   Document.ErrorBmp.Width,
                                                   Document.ErrorBmp.Height);
							skCanvas.DrawImage( Document.ErrorBmp,
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
										typeof( OverflowException ),
										typeof( AccessViolationException ) };
                    if (rgErrors.IsUnhandled(oEx))
                        throw;

                    LogError("Paint", "Solo Image viewer having problem painting.");
				} finally {
					skCanvas.Flush ();
                }
			}
        }

        protected override void OnSizeChanged(EventArgs e) {
            base.OnSizeChanged(e);

			try {
				if( !Document.IsImageValid ) {
					// Looks like we're hitting this when the shell shuts down.
					if( Document.ErrorBmp != null ) {
						ViewPortSizeCenter(Document.ErrorBmp, _rctViewPort);
						_rctWorldPort.SetRect( LOCUS.UPPERLEFT, 0, 0,
											   Document.ErrorBmp.Width,
											   Document.ErrorBmp.Height);
					}
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
            return Document.Execute( sGuid );
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

    public class ImageViewTextCompositor : 
		ImageViewSingle,
		IPgTools
	{
		protected enum TCTools : int {
						   EditText = 0,
						   SizeText
					   };
		protected Dictionary< TCTools, string> _rgTools = new();
		protected TCTools                      _eTool   = TCTools.EditText;
        public ImageViewTextCompositor(IPgViewSite oSiteBase, ImageSoloDoc oDocSolo) : 
			base(oSiteBase, oDocSolo) 
		{
			_rgTools.Add( TCTools.EditText, "Edit Text" );
			_rgTools.Add( TCTools.SizeText, "Size Text" );
        }

        public int ToolCount => _rgTools.Count;

        public int ToolSelect { 
			get => (int)_eTool;
			set => _eTool = (TCTools)value;
		}

        public Image ToolIcon(int iTool) {
            return null;
        }

        public string ToolName(int iTool) {
            return _rgTools[(TCTools)iTool];
        }
    }
}