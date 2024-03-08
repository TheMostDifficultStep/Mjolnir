using System;
using System.Collections.Generic;
using System.Windows.Forms;
using System.Drawing; // TODO: Working towards deleting this!
using System.Xml;
using System.Collections;

using SkiaSharp.Views.Desktop;
using SkiaSharp;

using Play.Interfaces.Embedding;
using Play.Rectangles;
using Play.Forms;
using Play.Edit;

namespace Play.ImageViewer {
	public class WindowSoloImage : 
		ImageViewSingle,
        IPgLoad<XmlElement>,
        IPgSave<XmlDocumentFragment>,
		IPgCommandView
	{
		public enum Tools : int {
			Select = 0,
			Navigate
		}

		protected readonly IPgShellSite _oSiteShell;
		protected          Tools        _eToolCurrent    = Tools.Select;
		protected readonly SmartSelect  _rcSelectionView = new SmartSelect(); // selection in View coords.

		protected SmartGrabDrag _oSmartDrag = null; // See the base class for the SmartGrab.
		private   SKPointI      _pntAspect  = new SKPointI( 320, 240 );

        public virtual Guid      Catagory	  => Guid.Empty;
        public virtual string    Banner		  => "Image Viewer";
		public         SKBitmap  Icon         { get; protected set; }
		public         Image     Iconic => null;
        public         uint      ID { get { return _oSiteShell.SiteID; } }

        public WindowSoloImage( IPgViewSite oBaseSite, ImageBaseDoc oDoc ) : base( oBaseSite, oDoc ) {
			_oSiteShell = oBaseSite as IPgShellSite; // not required to exist.

			_rcSelectionView.Hidden     = true;
			_rcSelectionView.Invertable = false;
        }

        public bool IsDirty => false;

        public override bool InitNew() {
            if( !base.InitNew() )
				return false;

            ContextMenuStrip oMenu = new ContextMenuStrip();
            oMenu.Items.Add( new ToolStripMenuItem("Copy", null, ClipboardCopyTo, Keys.Control | Keys.C));
		  //Enable this when I have a snip dialog!!
          //oMenu.Items.Add( new ToolStripMenuItem("Snip", null, SelectionSnip ));
            oMenu.Opened += new EventHandler(this.ContextMenuShowing);
            
			ContextMenuStrip = oMenu;

			return true;
        }

        public bool Load( XmlElement xmlRoot ) {
            if( !InitNew() )
                return( false );
            
            return( true );
        }

        public bool Save( XmlDocumentFragment xmlRoot ) {
            return( true );
        }

        /// <summary>
        /// This is copied from the mainwin of the shell. It needs to be a tool, but I'm not sure where I want to stick it yet.
        /// </summary>
        /// <param name="strText">The character(s) to make a bitmap out of.</param>
        /// <remarks>This will greatly benefit from my new FreeType font manager.</remarks>
        public static Bitmap BitmapCreateFromChar( string strText ) {
            Size   oSize = new Size( 16, 16 );
            Bitmap oBmp  = new Bitmap( oSize.Width, oSize.Height );

            using( Graphics g = Graphics.FromImage(oBmp) ) {
                using( Font oFont = new Font( "Segoe MDL2 Assets", 10f, FontStyle.Regular ) ) {
                    float  fPixels = oFont.SizeInPoints * oFont.FontFamily.GetCellDescent( FontStyle.Regular ) / oFont.FontFamily.GetEmHeight(FontStyle.Regular);
                    SKPoint oPointF = new SKPoint( 0, fPixels );

                    g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;

                    g.DrawString( strText, oFont, Brushes.Black, oPointF.ToDrawingPoint() );
                }
            }

            return( oBmp );
        }
        
		/// <summary>
		/// Set and get Aspect ratio needed for selection when enforced.
		/// </summary>
		public SKPointI Aspect { 
			get { return _pntAspect; } 
			set {
				if( value.X == 0 ) {
					LogError( "View Solo", "Attempt to set illegal aspect." );
					return;
				}

				float flOldSlope = _pntAspect.Y / (float)_pntAspect.X;
				float flNewSlope = value.Y      / (float)value.X;

				_pntAspect = value; 

				// Reset the selection to make sure it still matches the current aspect.
				if( flOldSlope != flNewSlope && !_rcSelectionView.Hidden ) {
					SKPointI      pntCorner  = _rcSelectionView.GetPoint( LOCUS.LOWERRIGHT );
					SmartGrabDrag oSmartDrag = _rcSelectionView.BeginAspectDrag( null, SET.STRETCH, SmartGrab.HIT.CORNER, 
																				 LOCUS.LOWERRIGHT, pntCorner.X, pntCorner.Y, Aspect );
					oSmartDrag.Move( pntCorner.X, pntCorner.Y );

					AlignBmpSelectionToViewSelection();
					Invalidate();
				}
			}
		} 

		/// <summary>
		/// If the selection rectangle on the view is hidden, ie the tool is not
		/// selected. The don't allow the context menu to have active items.
		/// </summary>
		protected void ContextMenuShowing(object sender, EventArgs e) {
			foreach( ToolStripItem oItem in ContextMenuStrip.Items ) {
				oItem.Enabled = !_rcSelectionView.Hidden;
			}
		}

		/// <remarks>This represents an interesting cunundrom. The old OLE stuff looks like
		/// it's dependent on the old style bitmap. BUG: Is this leaking?</remarks>
		[Obsolete]protected void ClipboardCopyTo(object sender, EventArgs e) {
			DataObject oDataObject = new DataObject();
			oDataObject.SetImage( Document.Bitmap.ToBitmap() );
		}

        /// <summary>
        /// This assigns a Selection in bmp (world) coordinates that matches the current
        /// view selection and aspect.
        /// </summary>
        /// <seealso cref="AlignViewSelectionToCurrentBmpSelection"/>
        protected void AlignBmpSelectionToViewSelection() {
			SKPoint pntAspect = new SKPoint( Document.Bitmap.Width  / (float)_rctViewPort.Width,
											 Document.Bitmap.Height / (float)_rctViewPort.Height );

			// Note: If you move the points top/left, bottom/right in two steps you might
			//       end up with an intermediate inverted rect which is illegal and we
			//       get scrambled values. Do it all in one step!!
			Selection.SetRect( LOCUS.UPPERLEFT,
							   (int)((_rcSelectionView.Left - _rctViewPort.Left ) * pntAspect.X ),
						       (int)((_rcSelectionView.Top  - _rctViewPort.Top  ) * pntAspect.Y ),
							   (int)( _rcSelectionView.Width   * pntAspect.X ),
							   (int)( _rcSelectionView.Height  * pntAspect.Y ) );
		}

		/// <summary>
		/// This is the inverse operation as it's sister method. Call this method
		/// when the viewport is resized and we need to get our viewport selection
		/// to map back to the current bmp selection.
		/// </summary>
		/// <seealso cref="AlignBmpSelectionToViewSelection"/>
		protected void AlignViewSelectionToCurrentBmpSelection() {
				SKPoint pntAspect = new SKPoint( _rctViewPort.Width  / (float)Document.Bitmap.Width,
											     _rctViewPort.Height / (float)Document.Bitmap.Height );

				_rcSelectionView.SetRect(LOCUS.UPPERLEFT,
								   (int)( Selection.Left * pntAspect.X ) + _rctViewPort.Left,
								   (int)( Selection.Top * pntAspect.Y ) + _rctViewPort.Top,
								   (int)( Selection.Width * pntAspect.X ),
								   (int)( Selection.Height * pntAspect.Y ));
		}

		protected override void OnPaint(PaintEventArgs oE) {
			base.OnPaint(oE);

			_rcSelectionView.Paint( oE.Graphics );
		}

        protected override void OnGotFocus(EventArgs e) {
            base.OnGotFocus( e );

			_rcSelectionView.Show = SHOWSTATE.Focused;
        }

        protected override void OnLostFocus(EventArgs e) {
            base.OnLostFocus(e);

			_rcSelectionView.Show = SHOWSTATE.Active;
        }

        protected override void OnMouseMove(MouseEventArgs e) {
            base.OnMouseMove(e);

            //POINT ePoint = _rctViewPort.IsWhere( e.X, e.Y );
			if( _oSmartDrag != null ) {
				this.Cursor = Cursors.Default;
				_oSmartDrag.Move( e.X, e.Y );
				Refresh();
				return;
			}

			switch( _eToolCurrent ) {
				case Tools.Select:
					if( !_rcSelectionView.Hidden &&
						_rcSelectionView.IsInside( e.X, e.Y ) ) {
						this.Cursor = Cursors.Hand;
					} else {
						this.Cursor = Cursors.Default;
					}
					break;
			}
        }

        protected override void OnMouseDown(MouseEventArgs e) {
            base.OnMouseDown(e);

			if( !Focused ) { 
				//_fSkipMouse = true;
				this.Select();
			}

			if( _eToolCurrent == Tools.Select ) {
				_rcSelectionView.Mode = DragMode;

				if( _rcSelectionView.Hidden ) {
					// We might be in the select mode but the selection is hidden at first, so...
					_rcSelectionView.Hidden = false;
					_rcSelectionView.Show   = SHOWSTATE.Focused;
					_rcSelectionView.SetRect( e.X-1, e.Y-1, e.X+1, e.Y+1 );
					// If selection hidden, we choose the lower right as the drag edge to get started.
					_oSmartDrag = _rcSelectionView.BeginAspectDrag( null, SET.STRETCH, SmartGrab.HIT.CORNER, LOCUS.LOWERRIGHT, e.X, e.Y, Aspect, _rctViewPort );
				} else {
					if( e.Button == MouseButtons.Left ) {
						_oSmartDrag = _rcSelectionView.BeginDrag( e.X, e.Y, Aspect, _rctViewPort );
					}
				}
			}
        }

        protected override void OnMouseUp(MouseEventArgs e) {
            base.OnMouseUp(e);


			if( _oSmartDrag != null && Document.Bitmap != null ) {
				_oSmartDrag.Dispose();
				_oSmartDrag = null;

				AlignBmpSelectionToViewSelection();
			}
        }

        /// <summary>
        /// Let's us set the selection drag option, free drag or fixed aspect drag.
        /// </summary>
        public DragMode DragMode { get; set; } = DragMode.FreeStyle;
		//	get {
		//		//if( DragOptions[0].CompareTo( "FixedRatio" ) == 0 )
		//			return DragMode.FixedRatio;

		//		//return DragMode.FreeStyle;
		//	}
		//}

		protected void SelectionSnip(object sender, EventArgs e) {
			if( !_rcSelectionView.Hidden ) {
				try {
					if( _oSiteShell != null ) {
						ViewSnipDialog oView = (ViewSnipDialog)_oSiteShell.AddView( ViewSnipDialog.Guid, fFocus:true );
						if( oView != null )
							oView.SnipMake(Selection, uiReturnID:ID );
					}
				} catch( Exception oEx ) {
					Type[] rgErrors = { typeof( InvalidCastException ),
										typeof( NullReferenceException ) };
					if( rgErrors.IsUnhandled( oEx ) )
						throw;

					LogError( "ViewSolo", "Problem with selection snip" );
				}
			}
		}

        public void FocusMe() {
			if( _oSiteShell != null ) {
				_oSiteShell.FocusMe();
			}
        }
		
		protected override void OnImageUpdated() {
			base.OnImageUpdated();

			if( !_rcSelectionView.Hidden ) {
				_rcSelectionView.Hidden = true;
			}
		}

		public void SelectOff() {
			_rcSelectionView.Hidden = true;
			Invalidate();
		}

        protected override void OnSizeChanged(EventArgs e) {
            base.OnSizeChanged(e);

			// Turns out we get called all the time and we might not have our bitmap set yet.
			if( Document.Bitmap == null )
				return;

			try {
				AlignViewSelectionToCurrentBmpSelection();
			} catch( NullReferenceException ) {
				LogError( "SizeChanged", "Null pointer in WindowSoloImage" );
			}

            Invalidate();
        }

		public virtual void SelectAll() {
			if( Document.Bitmap != null ) {
				_rcSelectionView.Show = SHOWSTATE.Focused;
			    _sBorder              = _sGrabBorder;  // Just in case not enough room.

				Selection.SetRect( LOCUS.UPPERLEFT ,0, 0, Document.Bitmap.Width, Document.Bitmap.Height );

				ViewPortSizeMax( _rctWorldPort, _rcSelectionView );
				ViewPortSizeMax( _rctWorldPort, _rctViewPort );
			} else {
				_rcSelectionView.Hidden = true;
			}
			Invalidate();
		}

        public virtual object Decorate( IPgViewSite oBaseSite, Guid sGuid ) {
            return( null );
        }

        public override bool Execute( Guid sGuid ) {
			if( sGuid == GlobalCommands.SelectAll ) {
				SelectAll();
				return true;
			}
			if( sGuid == GlobalCommands.SelectOff ) {
				SelectOff();
				return true;
			}

            return( base.Execute( sGuid ) );
        }

	}

    public class WindowSoloImageNav : 
		WindowSoloImage,
		IPgTools,
        IPgTextView, // TODO: Only one member is used. Can I simplify for our case?
		IPgPlayStatus,
		IEnumerable<ILineRange>,
		IReadableBag<Line>
    {
				 float          _flZoom = 1;
		readonly ImageWalkerDoc _oDocWalker;

		// We don't have multiple tool selections until this object
		// Our ansestor only offers Select.
		protected readonly List<string> _rgTools = new List<string>();

        public override string Banner => _oDocWalker.Banner;

      //TODO: Save these on the document.
      //Cursor _oCursorGrab;
        Cursor _oCursorLeft;
        Cursor _oCursorRight;

	  //protected bool _fSkipMouse = false; Helped with staggard view, but don't use now.

        readonly SmartRect _rctLeft        = new SmartRect( LOCUS.UPPERLEFT, 0, 0, 0, 0 );
        readonly SmartRect _rctRight       = new SmartRect( LOCUS.UPPERLEFT, 0, 0, 0, 0 );
        readonly SmartRect _rctBottomLeft  = new SmartRect( LOCUS.UPPERLEFT, 0, 0, 0, 0 );
        readonly SmartRect _rctBottomRight = new SmartRect( LOCUS.UPPERLEFT, 0, 0, 0, 0 );
        readonly SmartRect _rctTopLeft     = new SmartRect( LOCUS.UPPERLEFT, 0, 0, 0, 0 );
        readonly SmartRect _rctTopRight    = new SmartRect( LOCUS.UPPERLEFT, 0, 0, 0, 0 );

        readonly List<SmartRect> _rgLeft  = new List<SmartRect>(3);
        readonly List<SmartRect> _rgRight = new List<SmartRect>(3);

		protected virtual string IconResource => "icons8-portrait.png";

        public WindowSoloImageNav( IPgViewSite oBaseSite, ImageWalkerDoc oDoc ) : base( oBaseSite, oDoc ) {
			_oDocWalker = oDoc ?? throw new ArgumentNullException( "Document must not be null." );

			// BUG : This throws when a subclass in a DIFFERENT ASSEMBLY tries to
			//       load it's resourse. What a pain in the a$$.
			try {
				Icon = _oDocWalker.GetResource( IconResource );
			} catch( InvalidOperationException ) {
			}

            _rgLeft.Add( _rctLeft );
            _rgLeft.Add( _rctBottomLeft );
            _rgLeft.Add( _rctTopLeft );

            _rgRight.Add( _rctRight );
            _rgRight.Add( _rctBottomRight );
            _rgRight.Add( _rctTopRight );

			_rgTools.Add( "Select" );
			_rgTools.Add( "Navigate" );

			_eToolCurrent = Tools.Navigate;
        }

        public override bool InitNew() {
			if( !base.InitNew() )
				return false;

			SetBorderOn(); // Want room for grab handles at all times.

			ToolSelect = _rgTools.Count - 1;

            try {
                _oCursorLeft  = User32.CreateCursorNoResize( BitmapCreateFromChar( "\xE973" ), 16, 16 );
                _oCursorRight = User32.CreateCursorNoResize( BitmapCreateFromChar( "\xE974" ), 16, 16 );

                // Old way of doing it.
                //using (Stream oStream = assembly.GetManifestResourceStream( @"ImageViewer.Content.pan-left.png") ) {
                //    Bitmap oBmp = new Bitmap( oStream );
                //    _oCursorLeft  = User32.CreateCursorNoResize( oBmp, 16, 16 );
                //}
            } catch( ArgumentNullException ) {
            }

            _oDocWalker.MediaEvent += OnMediaEvent_DocWalker;

            return true;
        }

        private void OnMediaEvent_DocWalker(ShellNotify eEvent ) {
            _oViewSite.Notify( eEvent );
        }

		protected override void Dispose( bool fDisposing ) {
			if( fDisposing ) {
				if( !_fDisposed ) {
					// BUG: Do I need to displode the ContextMenuStrip?
					//if( ContextMenu != null ) {
					//	ContextMenu.Dispose();
					//	ContextMenu = null;
					//}
					if( Icon != null && Icon is IDisposable oDisp )
						oDisp.Dispose();

					if( _oCursorLeft != null ) {
						_oCursorLeft.Dispose();
						_oCursorLeft = null;
					}
					if( _oCursorRight != null ) {
						_oCursorRight.Dispose();
						_oCursorRight = null;
					}

					_oDocWalker.MediaEvent -= OnMediaEvent_DocWalker;
				}

			}
            base.Dispose( fDisposing );
        }

		/// <remarks>This represents an interesting cunundrom. The old OLE stuff looks like
		/// it's dependent on the old style bitmap. BUG: Is this leaking?</remarks>
		[Obsolete]protected void ClipboardCopyTo(object sender, EventArgs e) {
			DataObject oDataObject = new DataObject();
			oDataObject.SetImage( _oDocWalker.Bitmap.ToBitmap() );
		}

        protected override void OnSizeChanged(EventArgs e) {
            base.OnSizeChanged(e);

			// Turns out we get called all the time and we might not have our bitmap set yet.
			if( Document.Bitmap == null )
				return;

			try {
				// But we can set up or navigation hotspots w/o a bitmap.
				int iHalfWidth = Width / 2;
				int iNavWidth  = ( iHalfWidth > 50 ) ? 50 : iHalfWidth;

				_rctLeft .SetRect( LOCUS.UPPERLEFT,      0, 0, iNavWidth, Height );
				_rctRight.SetRect( LOCUS.UPPERRIGHT, Width, 0, iNavWidth, Height );

				_rctBottomLeft .SetRect( LOCUS.LOWERLEFT,  0,          this.Height, iHalfWidth, 50 );
				_rctBottomRight.SetRect( LOCUS.LOWERRIGHT, this.Width, this.Height, iHalfWidth, 50 );

				_rctTopLeft    .SetRect( LOCUS.UPPERLEFT,  0,          0,           iHalfWidth, 50 );
				_rctTopRight   .SetRect( LOCUS.UPPERRIGHT, this.Width, 0,           iHalfWidth, 50 );
			} catch( NullReferenceException ) {
				LogError( "SizeChanged", "Null pointer in ViewSolo" );
			}

            Invalidate();
        }

        enum ViewCursors {
            HAND,
            LEFT,
            RIGHT
        }

        /// <summary>
        /// Just an experiment. I really want to zoom based on position of mouse.
        /// and percentage of image. On sizes blasts this value, need to deal with that too.
        /// </summary>
        /// <param name="e"></param>
        protected override void OnMouseWheel(MouseEventArgs e) {
            base.OnMouseWheel( e );
           
            float     flWindowAspec = Width / (float)Height; 
            Size      oIncrement    = new Size();
			SmartRect rctNewWorld   = null;

            _flZoom += ( e.Delta > 0 ) ? (float).1 : (float)-.1;
			
			try {
				if( _flZoom > 1 ) {
					_flZoom = 1;
					_rctWorldPort.SetRect( LOCUS.UPPERLEFT, 0, 0, Document.Bitmap.Width, Document.Bitmap.Height );
				} else {
					_rctWorldPort.SetRect( LOCUS.UPPERLEFT, 0, 0, Document.Bitmap.Width, Document.Bitmap.Height );
				}

				if( Width > Height ) {
					oIncrement.Width  = (int)(_rctWorldPort.Width * _flZoom );
					oIncrement.Height = (int)(oIncrement.Width / flWindowAspec);
				} else {
					oIncrement.Height = (int)(_rctWorldPort.Height  * _flZoom );
					oIncrement.Width  = (int)(oIncrement.Height * flWindowAspec);
				}

				rctNewWorld = new SmartRect( _rctWorldPort );
				rctNewWorld.SetPoint( SET.INCR, LOCUS.ALL, oIncrement.Width, oIncrement.Height );

				if( rctNewWorld.Left < 0 )
					return;
				if( rctNewWorld.Right > Document.Bitmap.Width )
					return;
				if( rctNewWorld.Left >= rctNewWorld.Right )
					return;

				if( rctNewWorld.Top < 0 )
					return;
				if( rctNewWorld.Bottom > Document.Bitmap.Height )
					return;
				if( rctNewWorld.Top >= rctNewWorld.Bottom )
					return;
			} catch( NullReferenceException ) {
				return;
			}
            _rctWorldPort.Copy = rctNewWorld;
			
            Invalidate();
        }

        protected override void OnMouseMove(MouseEventArgs e) {
            base.OnMouseMove(e);

			if( _oSmartDrag != null ) {
				// Base class will have handled this.
				return;
			}

			switch( (Tools)ToolSelect ) {
				case Tools.Navigate:
					Cursor[] rgCursors = { Cursors.Default, _oCursorLeft, _oCursorRight };
					int      iCursor   = 0;

					foreach( SmartRect rctHot in _rgLeft ) {
						if( rctHot.IsInside( e.X, e.Y ) ) {
							iCursor = 1;
						}
					}
					foreach( SmartRect rctHot in _rgRight ) {
						if( rctHot.IsInside( e.X, e.Y ) ) {
							iCursor = 2;
						}
					}

					this.Cursor = rgCursors[iCursor];
					break;
			}
        }

        protected override void OnMouseDown(MouseEventArgs e) {
            base.OnMouseDown(e);
        }

        protected override void OnMouseUp(MouseEventArgs e) {
			// This is a little hacky. But we need to clear the
			// smart drag in the base and then exit so as not
			// to call the tools below...
			if( _oSmartDrag != null && Document.Bitmap != null ) {
				base.OnMouseUp(e);
				return;
			}

            base.OnMouseUp(e);

			if( ToolSelect == (int)Tools.Navigate ) {
				foreach( SmartRect rctHot in _rgLeft ) {
					if( rctHot.IsInside( e.X, e.Y ) ) {
						_oDocWalker.Next( -1 );
						return;
					}
				}

				foreach( SmartRect rctHot in _rgRight ) {
					if( rctHot.IsInside( e.X, e.Y ) ) {
						_oDocWalker.Next( +1 );
						return;
					}
				}
			}
        }

		/// <seealso cref="ImageViewIcons.OnKeyUp"
        protected override void OnKeyDown(KeyEventArgs e) {
            if( this.IsDisposed )
                return;

            e.Handled = true;

            switch( e.KeyCode ) {
                case Keys.Left:
                    if( e.Control )
                        _oViewNotify.IsCommandKey( CommandKey.Left, KeyBoardEnum.Control );
                    else
                        _oDocWalker.Next( -1 );
                    break;
                case Keys.Right:
                    if( e.Control )
                        _oViewNotify.IsCommandKey( CommandKey.Right, KeyBoardEnum.Control );
                    else
                        _oDocWalker.Next( +1 ); // Note: We'll get a OnNextImage() before we return!
                    break;
                case Keys.Tab:
                case Keys.Space:
                    _oDocWalker.Next( +1 ); 
                    break;

				case Keys.Return:
					_oDocWalker.PlayStart();
					break;
				case Keys.Escape:
					_oDocWalker.PlayStop();
					break;
                case Keys.Delete:
                    // We processon key UP in ImageViewIcons. It won't receive this message
					// when it is an adornment.
                    _oDocWalker.CurrentFileDelete();
                    break;

                default:
                    e.Handled = false;
                    break;
            }
       }

        protected override void OnKeyPress( KeyPressEventArgs e ) {
 	        if( e.KeyChar == 0x0009 ) { 
                // Next frame!! GIFS
            }
        }

		public override void SelectAll() {
			base.SelectAll();

			if( Document.Bitmap != null ) {
				ToolSelect = (int)Tools.Select;
				// BUG: need to send the shell an event.
			} 
		}

        public override object Decorate( IPgViewSite oBaseSite, Guid sGuid ) {
            if( sGuid == GlobalDecorations.Outline ) {
                return new ImageViewIcons( oBaseSite, _oDocWalker );
            }
            if( sGuid.Equals( GlobalDecorations.Properties ) ) {
                return new WindowStandardProperties( oBaseSite, _oDocWalker.Properties );
            }

            return( null );
        }

        public override bool Execute( Guid sGuid ) {
			if( sGuid == GlobalCommands.Play ) {
				_oDocWalker.PlayStart();
                _oViewSite.Notify( ShellNotify.MediaStatusChanged );
				return true;
			}
			if( sGuid == GlobalCommands.Pause ) {
				_oDocWalker.PlayPause();
				return true;
			}
			if( sGuid == GlobalCommands.Stop ) {
				_oDocWalker.PlayStop();
				return true;
			}


            return( base.Execute( sGuid ) );
        }

        public TextPosition Caret {
            get {
                try {              
                    return new TextPosition( _oDocWalker.CurrentElement.At, 0 ); 
                } catch( NullReferenceException ) {
                    return new TextPosition( 0, 0 ); 
                }
            }
        }

		public void ScrollTo( SCROLLPOS eEdge ) { }
        public void ScrollToCaret()        { }

        public bool SelectionSet( int iLine, int iOffset, int iLength ) { return( _oDocWalker.Given( iLine ) ); }
        public void SelectionClear() {  }

		public int ToolCount => _rgTools.Count;

		public int ToolSelect { 
			get { return (int)_eToolCurrent; }
			set {
				Tools eNextTool = (Tools)value;

				if( _eToolCurrent != eNextTool && 
					_eToolCurrent == Tools.Select ) {
					_rcSelectionView.Hidden = true;
					Invalidate();
				}

				_eToolCurrent = eNextTool; 
				_oViewSite.Notify( ShellNotify.ToolChanged );
			}
		}

        public bool IsPlaying { get { 
			switch( _oDocWalker.PlayStatus ) {
				case WorkerStatus.BUSY:
					return true;
				case WorkerStatus.PAUSED:
					return true;
			}
			return false;
		} }

        public SKColor BusyLight { get {
			switch( _oDocWalker.PlayStatus ) {
				case WorkerStatus.BUSY:
					return SKColors.LightGreen;
				case WorkerStatus.PAUSED:
					return SKColors.Yellow;
			}
			return SKColors.Empty;
		} }

        public int PercentCompleted => throw new NotImplementedException();

        public string ToolName(int iTool) {
			try {
				return _rgTools[iTool];
			} catch ( ArgumentOutOfRangeException ) {
				return string.Empty;
			}
		}

		public Image ToolIcon(int iTool) {
			return null;
		}

        public IEnumerator<ILineRange> GetEnumerator() {
			return _oDocWalker.CreateLineSearch();
        }

        IEnumerator IEnumerable.GetEnumerator() {
            return GetEnumerator();
        }

		public int ElementCount => _oDocWalker.ElementCount;

        public Line this[int index] => _oDocWalker[index];


    } // End Class
}
