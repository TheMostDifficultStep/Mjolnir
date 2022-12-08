using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

using SkiaSharp;

using Play.Rectangles;
using Play.Interfaces.Embedding;

namespace Mjolnir {
    public interface IPgMenuVisibility {
        bool Checked { get; set; }
        SmartHerderBase Shepard { get; }
        SideIdentify  Orientation { get; }
    }

	public class MenuItemHerder : 
        ToolStripMenuItem,
        IPgMenuVisibility
    {
		SmartHerderBase _oShepard;

		public MenuItemHerder( SmartHerderBase oShepard, EventHandler oHandler ) :
			base( oShepard.Title, null, oHandler ) {
			_oShepard = oShepard;
            _oShepard.MenuVisiblityObject = this;
		}

		public SmartHerderBase Shepard {
			get {
				return( _oShepard );
			}
		}

        bool IPgMenuVisibility.Checked {
            get {
                return( base.Checked );
            }
            set {
                base.Checked = value;
            }
        }

        public SideIdentify Orientation {
            get { return( _oShepard.Orientation ); }
        }
	}

    /// <summary>
    /// Base class for a herder. We have two sub classes, one for herders that hold a single object only.
    /// And others that hold one per view. Since the "solo" case doesn't need an index to the given item
    /// we pass null. Solo objects looking for null should complain if an add comes with an index object
    /// Collection objects always need an index object and should complain if get a null index. Not that null
    /// doesn't seem like a fine index value, but it seems various .net collections are starting to throw
    /// exceptions. Alas, this conflict is because the tool windows for the shell might created per document
    /// or be single like for the shell main output window.
    /// </summary>
    public abstract class SmartHerderBase : 
        LayoutRect,
        IDisposable
    {
        protected readonly SmartRect _rcInner  = new SmartRect(); // The inner rect for our flock!
        protected readonly SmartRect _rcTitle  = new SmartRect(); // The rect around the title area!
        protected readonly SmartRect _rcMargin = new SmartRect();

        protected MainWin   _oHost;
                  string    _strTitle;
                  Bitmap    _bmpIcon;
                  SHOWSTATE _eViewState = SHOWSTATE.Inactive;
                  bool      _fHideTitle = false;

        readonly  string    _strName;
        readonly  Guid      _guidDecor;

        IPgMenuVisibility _oMenuVis = null; // pointer to shell menu entry.

        public SmartHerderBase(MainWin oMainWin, Bitmap oBitmap, string strName, string strTitle, Guid guidDecor ) :
			base( CSS.Percent )
        {
            _oHost     = oMainWin;
            _strTitle  = strTitle;
            _bmpIcon   = oBitmap;
            _strName   = strName;
            _guidDecor = guidDecor;
        }

        public abstract bool TabStop {
            get; set;
        }

        public SmartRect Margin {
            get { return( _rcMargin ); }
            set { 
				_rcMargin.Copy = value; 
				//Raise_OnSize( this ); 
				OnSize();
			}
        }

        public string Name {
            get { return( _strName ); }
        }
        
        public Guid Guid {
            get { return( _guidDecor ); }
        }

        public bool HideTitle {
            set { _fHideTitle = value; }
        }

        // Debug helper.
        public override string ToString()
        {
            return _strName + "@" + Orientation.ToString();
        }

        public bool DesiresVisiblity {
            get {
                if( _oMenuVis == null )
                    return( true );

                return( _oMenuVis.Checked );
            }
        }

        public IPgMenuVisibility MenuVisiblityObject {
            set {
                _oMenuVis = value;
            }
        }

        /// <summary>
        /// Essentially which side we are in. Probably should use the SideIdentifier enumeration at some point.
        /// </summary>
        public SideIdentify Orientation { get; set; }

        // XFORM     _oXForm = new XFORM();

        /// <summary>
        /// Clockwise rotation.
        /// </summary>
        //protected void SetWorldTransform( double dAngle, Point oOrigin ) {
        //    double dRadians = dAngle * ( Math.PI / 180 );

        //    _oXForm.eM11 = (float)Math.Cos(dRadians);
        //    _oXForm.eM12 = (float)Math.Sin(dRadians);
        //    _oXForm.eM21 = -_oXForm.eM12;
        //    _oXForm.eM22 =  _oXForm.eM11;

        //    _oXForm.eDx = oOrigin.X;
        //    _oXForm.eDy = oOrigin.Y;
        //}

        public void Measure( Graphics oE ) {
        }

        /// <summary>
        /// GotFocus is going to blow our value. Why do I have an 'set' accessor.
        /// </summary>
        public virtual SHOWSTATE Show {
            get {
                return ( _eViewState );
            }

            set {
				Hidden     = false;
                _eViewState = value;
            }
        }

        public override void Paint( Graphics p_oGraphics )
        {
            if( Hidden ) 
                return;

 	        base.Paint( p_oGraphics );
            PaintTitle( p_oGraphics );

			// DEBUG Code.
            //p_oGraphics.DrawLine(Pens.Aquamarine,
            //          _rcInner.GetScalar(SCALAR.LEFT),
            //          _rcInner.GetScalar(SCALAR.TOP),
            //          _rcInner.GetScalar(SCALAR.RIGHT),
            //          _rcInner.GetScalar(SCALAR.BOTTOM));
        }

        // https://stackoverflow.com/questions/45077047/rotate-photo-with-skiasharp
        /// <summary>
        /// I'm just tinkering here. I've got some of the GDI32 code to rotate the
        /// text but the image is the crappy .net GDI functions. I need to implement a bit blit
        /// interop. And a rectangle filling function. Plus, there are hard coded values
        /// that can't stay.
        /// </summary>
        /// <param name="p_oGraphics"></param>
        protected void PaintTitle( Graphics p_oGraphics )
        {
            if( this.Rect.Width <= 0 || this.Rect.Height <= 0 /* || Orientation == Top */ 
                || _fHideTitle || _oHost.DecorFont == null )
                return; // Nothing to do.

            // Need this to clip the bitmap.
            SmartRect oRect = new SmartRect( this );
            oRect.SetScalar( SET.RIGID, SCALAR.LEFT, 0 );
            oRect.SetScalar( SET.RIGID, SCALAR.TOP, 0 );
            Region oRgn     = new Region( oRect.Rect );
            Color  oBgColor = _oHost.BackColor; // Color.LightGray;

            using( oRgn ) {
                SKPointI oPointImage = this.GetPoint(LOCUS.UPPERLEFT);
                Region   oOldRgn     = p_oGraphics.Clip;

                p_oGraphics.TranslateTransform( oPointImage.X, oPointImage.Y );
                p_oGraphics.Clip = oRgn;

                if( _eViewState == SHOWSTATE.Focused ) {
                    oBgColor = _oHost.ToolsBrushActive.Color;
                    p_oGraphics.FillRectangle( _oHost.ToolsBrushActive,
                                               0,
                                               0, 
                                               this.GetScalar(SCALAR.WIDTH) + 1,
                                               this.GetScalar(SCALAR.HEIGHT) + 1);
                } else {
                    p_oGraphics.FillRectangle( Brushes.LightGray,
                                               0,
                                               0, 
                                               this.GetScalar(SCALAR.WIDTH) + 1,
                                               this.GetScalar(SCALAR.HEIGHT) + 1);
                }

                p_oGraphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

                int iBoxWidth = _rcTitle[SCALAR.WIDTH] < _rcTitle[SCALAR.HEIGHT] ? _rcTitle[SCALAR.WIDTH] : _rcTitle[SCALAR.HEIGHT];
                if( _bmpIcon != null ) {
                    float left = ( iBoxWidth - _bmpIcon.Width ) / 2;
                    p_oGraphics.DrawImage(_bmpIcon, left, left ); // box width and height are the same.
                }

                int   iFontHeight = _oHost.DecorFont.Height;
                Point pntTemp     = new Point( iBoxWidth, iBoxWidth );
                switch( Orientation ) {
                    case SideIdentify.Left:
                    case SideIdentify.Right: 
                        pntTemp.Y = pntTemp.Y / 2 - iFontHeight / 2;
                        break;
                    case SideIdentify.Bottom: 
                    default:
                        p_oGraphics.RotateTransform( 90 );
                        pntTemp = new Point( pntTemp.X, - ( iFontHeight + (int)( (float)( iBoxWidth - iFontHeight ) / 2 ) ) );
                        break;

                }

                p_oGraphics.DrawString( _strTitle, _oHost.DecorFont, Brushes.Black, pntTemp );
                p_oGraphics.ResetTransform();

                p_oGraphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.Default;

                p_oGraphics.Clip = oOldRgn;
            }
        }

        public string Title {
            get { return( _strTitle ); }
        }

        /// <summary>
        /// Bring to front the adornment that matches the view at the site given.
        /// </summary>
        /// <param name="oSite">The site for the view who's decor we want to show.</param>
        public abstract bool AdornmentShuffle( object oSite );
        /// <summary>
        /// This hides all the adornments but does not set the hidden property.
        /// </summary>
        public abstract void AdornmentHideAll();
		public abstract void AdornmentCloseAll();

        /// <summary>
        /// Set the focus to the tool window associated with the given document.
        /// Will fail if there are no windows held by the herder.
        /// </summary>
        /// <param name="oSite">The document who's tools we want to show.</param>
        public abstract void AdornmentFocus( object oSite );

        public virtual bool IsContained( ViewSlot oSite ) {
            return( true );
        }

        /// <summary>
        /// DecorSite calls this function in the ViewSite.EventChain.OnFocus event.
        /// </summary>
        public void OnFocused()
        {
            _oHost.InsideShow = SHOWSTATE.Inactive;
            _eViewState       = SHOWSTATE.Focused;
			// BUG: Might need to set hidden := false;
        }

		/// <summary>
		/// Shell calls this function on all Herders when the center gets the focus.
		/// </summary>
		/// <remarks>This is a good example of why VIEWSTATE is inferior to SHOWSTATE.
		/// If a hidden herders got the on blur event I could only set the state
		/// if it was NOT hidden. Now I just set it whether it's shown or not!!</remarks>
        public void OnBlurred()
        {
			_eViewState = SHOWSTATE.Inactive;
        }

        /// <summary>
        /// Add an adornment to this herd. This set's up the sinks for the
        /// focus events so we know when the adornment gets/looses focus.
        /// </summary>
        /// <param name="oSite">Associated site, may be null.</param>
        /// <param name="oControl">Control to add.</param>
        /// <remarks>Note: we're not actually keeping track of the children
        /// in us in this class. That's up to a subclass.</remarks>
		/// <exception cref="ArgumentNullException" />
		/// <exception cref="ArgumentException" />
        public virtual bool AdornmentAdd( object oSite, Control oControl ) {
			if( oControl == null )
				throw new ArgumentNullException( "Control must not be null." );

            SmartRect oRect = new SmartRect( _rcInner );
            oRect.Inflate( -1, _rcMargin );

            oControl.Parent  = _oHost;
            oControl.Bounds  = oRect.Rect; 
            oControl.Visible = !Hidden;

            // Get the events from our guest control.
            //oControl.GotFocus  += _oGotFocusHandler;
            //oControl.LostFocus += _oLostFocusHandler;

            return( true );
        }

        /// <summary>
        /// Not that any adornments get removed but it's here if you want.
        /// </summary>
        public virtual void AdornmentRemove(object oViewSite)
        {
        }

		public abstract Control AdornmentFind( ViewSlot oViewSite);

        /// <summary>
        /// The base class calls the event, let's intercept
        /// here and resize our children.
        /// </summary>
        protected override void OnSize() {
            base.OnSize();

            SKPointI oPoint;
            int   iIconMargin = (int)(_oHost.DecorFont.Height * 1.5); // _bmpIcon.Width + 8

            if( _fHideTitle )
                iIconMargin = 0;

            //if( this._bmpIcon.Width < 2 ) {
            //    iIconMargin = 0;
            //}

            switch( Orientation ) {
                case SideIdentify.Left:
                case SideIdentify.Right:
                    oPoint = this.GetPoint(LOCUS.LOWERLEFT);
                    _rcInner.SetRect(LOCUS.LOWERLEFT, 
                                    oPoint.X, 
                                    oPoint.Y, 
                                    this.GetScalar(SCALAR.WIDTH),
                                    this.GetScalar(SCALAR.HEIGHT) - iIconMargin);
                    oPoint = this.GetPoint(LOCUS.UPPERLEFT);
                    _rcTitle.SetRect(LOCUS.LOWERLEFT, 
                                    oPoint.X, 
                                    oPoint.Y, 
                                    this.GetScalar(SCALAR.WIDTH),
                                    iIconMargin);
                    break;
                default: // top
                    oPoint = this.GetPoint(LOCUS.UPPERRIGHT);
                    _rcInner.SetRect(LOCUS.UPPERRIGHT,
                                    oPoint.X,
                                    oPoint.Y,
                                    this.GetScalar(SCALAR.WIDTH),
                                    this.GetScalar(SCALAR.HEIGHT));
                    oPoint = this.GetPoint(LOCUS.UPPERLEFT);
                    _rcTitle.SetRect(LOCUS.LOWERLEFT, 
                                    oPoint.X, 
                                    oPoint.Y, 
                                    0,
                                    0);
                    break;
                case SideIdentify.Bottom: 
                    oPoint = this.GetPoint(LOCUS.UPPERRIGHT);
                    _rcInner.SetRect(LOCUS.UPPERRIGHT,
                                    oPoint.X,
                                    oPoint.Y,
                                    this.GetScalar(SCALAR.WIDTH) - iIconMargin,
                                    this.GetScalar(SCALAR.HEIGHT));
                    oPoint = this.GetPoint(LOCUS.UPPERLEFT);
                    _rcTitle.SetRect(LOCUS.LOWERLEFT, 
                                    oPoint.X, 
                                    oPoint.Y, 
                                    iIconMargin,
                                    this.GetScalar(SCALAR.HEIGHT));
                    break;
            }
        }

        public virtual void Dispose() {
            _bmpIcon.Dispose();
            _oMenuVis = null; // probably not strictly necessary. But break the loop.
        }

        #region ISmartDragGuest Members

        public void HoverStop() {
        }

        public bool Hovering {
            get {
                return( false );
            }
        }

        /// <summary>
        /// This is our chance to provide a visual que for what we are doing.
        /// </summary>
        /// <param name="p_iX">Mouse X pos</param>
        /// <param name="p_iY">Mouse Y pos</param>
        /// <returns></returns>
        public bool Hover( int p_iX, int p_iY, out bool fChanged ) {
			fChanged = false;
            return( false );
        }

        #endregion

        public override bool IsInside(int p_iX, int p_iY)
        {
            if( Hidden )
                return( false );

            return( base.IsInside( p_iX, p_iY ) );
        }

    }

    public class SmartHerderClxn : SmartHerderBase
    {
        Dictionary<object, Control > _rgFlock = new Dictionary<object, Control>(); // The controls we are herding.
        bool                         _fTabStop = false;

        public SmartHerderClxn(MainWin oMainWin, Bitmap oBitmap, string strName, string strTitle, Guid guidName ) :
            base( oMainWin, oBitmap, strName, strTitle, guidName )
        {
        }

        public override bool TabStop
        {
            get
            {
                return( _fTabStop ); // Could add a verification step too.
            }
            set
            {
                foreach( KeyValuePair<object, Control> oPair in _rgFlock ) {
                    oPair.Value.TabStop = value;
                }
            }
        }

        /// <summary>
        /// Try to match the incoming site to a contained decoration matching the
        /// view or document site on that view.
        /// </summary>
        public override bool IsContained(ViewSlot oSite ) {
			foreach( KeyValuePair<object, Control> oPair in _rgFlock ) {
				if( oSite == oPair.Key )
                    return( true );
                if( oSite.DocumentSite == oPair.Key )
                    return( true );
            }
            return( false );
        }

		public override Control AdornmentFind( ViewSlot oSite ) {
			foreach( KeyValuePair<object, Control> oPair in _rgFlock ) {
				if( oSite == oPair.Key )
                    return oPair.Value;
                if( oSite.DocumentSite == oPair.Key )
                    return oPair.Value;
            }
			return null;
		}

        public override bool AdornmentShuffle( object oSite )
        {
            // Not sure if they ever could, but collections can't index via null object. Seems weird but 
            // we just have to inforce it anyway.
            if( oSite == null )
                return( false );

            bool fFound = false;

            // Go thru and hide anything that's not the key. Only show the key
            // value if we're not being hidden.
			foreach( KeyValuePair<object, Control> oPair in _rgFlock ) {
			    if( oSite == oPair.Key && DesiresVisiblity ) {
					oPair.Value.Show();
					oPair.Value.BringToFront();
                    fFound = true;

                    // Shell sets all tabstops to false, we turn it on only if we are visible.
                    oPair.Value.TabStop = this[ SCALAR.WIDTH ] > 0 || this[ SCALAR.HEIGHT] > 0;
			    } else {
					oPair.Value.Hide();
                }
			}

            return( fFound );
        }

        public override void AdornmentHideAll() {
			foreach( KeyValuePair<object, Control> oPair in _rgFlock ) {
		        oPair.Value.Hide();
			}
        }

		public override void AdornmentCloseAll() {
			foreach( KeyValuePair<object, Control> oPair in _rgFlock ) {
		        oPair.Value.Hide();
				_oHost.Controls.Remove( oPair.Value );
				oPair.Value.Dispose();
			}
			_rgFlock.Clear();
		}

		/// <summary>
		/// Set the focus to the tool window associated with the given document.
		/// Will fail if there are now windows held by the herder.
		/// </summary>
		/// <param name="oSite">The document who's tools we want to show.</param>
		public override void AdornmentFocus( object oSite ) {
            if( oSite == null )
                return;

			if( !Hidden ) {
				foreach( KeyValuePair<object, Control> oPair in _rgFlock ) {
					if( object.Equals( oSite, oPair.Key ) ) {
						oPair.Value.Show ();
						oPair.Value.Focus();
					}
				}
            }
        }

        /// <summary>
        /// Add an adornment to this herd. This set's up the sinks for the
        /// focus events so we know when the adornment gets/looses focus.
        /// </summary>
        /// <param name="oSite"></param>
        /// <param name="oControl"></param>
		/// <exception cref="ArgumentNullException" />
		/// <exception cref="ArgumentException" />
        public override bool AdornmentAdd( object oSite, Control oControl ) {
            if( oSite == null )
                throw new ArgumentNullException( "key must not be null" );

            if( _rgFlock.ContainsKey( oSite ) )
                throw new ArgumentException( "key already contained" );

            if( !base.AdornmentAdd( oSite, oControl ) )
                return( false );

            _oHost.Controls.Add( oControl );
            _rgFlock.Add(oSite, oControl);

            return( true );
        }

        public override void AdornmentRemove(object oViewSite)
        {
            if( oViewSite == null ) 
                return;

            foreach( KeyValuePair<object, Control> oPair in _rgFlock ) {
                if( oViewSite == oPair.Key ) {
                    Control oAdornment = oPair.Value;

                    oAdornment.Hide();
                    _oHost.Controls.Remove( oAdornment );
                    oAdornment.Parent = null;
                    break;
                }
            }

            _rgFlock.Remove(oViewSite);
        }

        protected override void OnSize()
        {
 	        base.OnSize();

            foreach( KeyValuePair<object, Control> oPair in _rgFlock ) {
                SmartRect oRect = new SmartRect( _rcInner );
                oRect.Inflate( -1, _rcMargin );

                oPair.Value.Bounds = oRect.Rect;
            }
        }

		public override bool Hidden { 
			set {
				base.Hidden = value; 

				if( Hidden ) {
					AdornmentHideAll();
				}
			}
		}

        public override void Dispose()
        {
 	         base.Dispose();
            _rgFlock.Clear();
        }
    }

    public class SmartHerderSolo : SmartHerderBase
    {
        Control _oControl;

        public SmartHerderSolo(MainWin oMainWin, Bitmap oBitmap, string strName, string strTitle, Guid guidName ) :
            base(oMainWin, oBitmap, strName, strTitle, guidName )
        {
        }

        /// <summary>
        /// Not quite sure why I called this function "AdornmentShow" since it's the
        /// window we are hosting that we want to show...
        /// </summary>
        /// <param name="oSite">The document who's tools we want to show. Just ignore it since this type of adornment shows for all windows.</param>
        public override bool AdornmentShuffle(object oSite)
        {
            if( _oControl != null ) {
                if( !Hidden ) {
                    _oControl.Show();
                     // Shell sets all tabstops to false, we turn it on only if we are visible.
                    _oControl.TabStop = this[ SCALAR.WIDTH ] > 0 || this[ SCALAR.HEIGHT] > 0;

                    return( true );
                } else {
				    _oControl.Hide();
                }
            }

            return( false );
        }

        public override void AdornmentHideAll() {
            if( _oControl != null ) {
				_oControl.Hide();
            }
        }

		public override void AdornmentCloseAll() {
			// BUG : We can't close solo's yet. No way to regen them.
			AdornmentHideAll();
		}

		/// <summary>
		/// Set the focus to the tool window associated with the given document.
		/// </summary>
		/// <param name="oSite">It's probably a bug if we suddenly start seing sites when we are the solo heard.</param>
		public override void AdornmentFocus(object oSite)
        {
            if( _oControl != null && !Hidden ) {
                _oControl.Focus();
            }
        }

        /// <summary>
        /// Add an adornment to this herd. This set's up the sinks for the
        /// focus events so we know when the adornment gets/looses focus.
        /// </summary>
        /// <param name="oSite">ignored, should be null</param>
        /// <param name="oControl">Control to host. Should be parented by this point.</param>
        public override bool AdornmentAdd(object oSite, Control oControl) {
            if( _oControl != null ) {
                _oHost.LogError( null, "internal", "This dock already has an addornment: " + this.Title );
                return( false );
            }

            _oControl = oControl ?? throw new ArgumentNullException( "Control must not be null" );

            base.AdornmentAdd( oSite, oControl );

            _oHost.Controls.Add( oControl );

            return( true );
        }

		public override Control AdornmentFind(ViewSlot oViewSite) {
			return _oControl;
		}

		public override void AdornmentRemove(object oViewSite)
        {
            // The herders are either solo or clxn. They all get the request to delete adornments
            // for a departing view. But solo's should ignore the request.
            if( oViewSite != null )
                return;

            _oHost.Controls.Remove( _oControl );

            _oControl = null;
        }

        /// <summary>
        /// Return our only control we are herding.
        /// </summary>
        public Control Adornment {
            get {
                return( _oControl );
            }
        }

        public override bool TabStop
        {
            get
            {
                if( _oControl != null )
                    return( _oControl.TabStop );

                return( false );
            }
            set
            {
                if( _oControl != null )
                    _oControl.TabStop = value;
            }
        }

        protected override void OnSize()
        {
            base.OnSize();

            if( _oControl != null ) {
                SmartRect oRect = new SmartRect( _rcInner );
                oRect.Inflate( -1, _rcMargin );

                _oControl.Bounds = oRect.Rect;
            }
        }

		public override bool Hidden { 
			set {
				base.Hidden = value;
				if( _oControl != null ) {
					if( Hidden ) {
						_oControl.Hide();
					} else {
						_oControl.Show();
					}
				}
			}
		}

        public override void Dispose()
        {
            base.Dispose();
            _oControl.Dispose();
        }
    } // End Class.

    public class SmartHerderDrag : SmartGrabDrag
    {
        /// <summary>
        /// This constructor is used for the herder dragging case. I'm not going to toss
        /// this class yet, since I might need it when implement SystemInformation.DragSize
        /// to control when the drag starts since herders are focusable.
        /// </summary>
        /// <param name="p_oFinished">Delegate to call when finished</param>
        /// <param name="p_rcGuest">Guest that we are moving.</param>
        /// <param name="p_iX">Starting Origin X.</param>
        /// <param name="p_iY">Starting Origin Y.</param>
        public SmartHerderDrag(
            DragFinished    p_oFinished,
            SmartHerderBase p_herderGuest,
            int             p_iX,
            int             p_iY ) : 
            base( p_oFinished, p_herderGuest, SET.RIGID, LOCUS.UPPERLEFT, p_iX, p_iY )
        {
            if( p_oFinished == null )
                throw new ArgumentNullException( "Need the 'finished' delegate" );
        }
    }

}
