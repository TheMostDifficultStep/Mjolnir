using Play.Edit;
using Play.Forms;
using Play.ImageViewer;
using Play.Interfaces.Embedding;
using Play.Rectangles;

using SkiaSharp;

using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

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
	/// Since the form is laying out the herders we need something
    /// that can paint a bitmap with GDI Graphics object. So here
    /// we are. Unfortunatly unlike LayoutSKBitmap, we create an
    /// instance of the bitmap for every view. It's not worth fixing
    /// until maybe the main window can stop inheriting from "Form"
    /// </summary>
    /// <seealso cref="LayoutSKBitmap"/>
    public class LayoutGdiBitmap :
        LayoutSimpleImage
    {
        readonly Image _oBitmap;

        public LayoutGdiBitmap( Assembly oAsm, string strResourceName ) {
            try {
                using Stream oStream = oAsm.GetManifestResourceStream( strResourceName );

                _oBitmap = Bitmap.FromStream( oStream );
            } catch( Exception oE ) {
                Type[] rgErrors = { typeof( KeyNotFoundException ), // This error if the user errored on the attribute name or value.
                                    typeof( ArgumentException ) };  // This error if we didn't embed resource.
                if( rgErrors.IsUnhandled( oE ) )
                    throw;

                _oBitmap = new Bitmap( 1, 1 ); 
            }
			WorldCoordinates.SetRect( 0, 0, _oBitmap.Width, _oBitmap.Height );
        }

        public override void Paint(Graphics p_oGraphics) {
			if( _oBitmap == null )
                return;

            try {
				p_oGraphics.DrawImage( _oBitmap, 
									   _rctViewPort.Rect,
									   WorldCoordinates.Rect,
									   GraphicsUnit.Pixel
                                   );
            } catch( NullReferenceException ) {
            }
        }

        public override float Aspect => _oBitmap.Width / (float)_oBitmap.Height;
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

 internal class HerderSlot : 
        IPgViewSite, 
        IPgViewNotify
 {
        readonly MainWin _oMainWin;
        public HerderSlot( MainWin oMainWin ) {
            _oMainWin = oMainWin;
        }
        public IPgViewNotify EventChain => this;

        public IPgParent Host => _oMainWin;

        public bool IsCommandKey(CommandKey ckCommand, KeyBoardEnum kbModifier) {
            return false;
        }

        public bool IsCommandPress(char cChar) {
            return false;
        }

        public void LogError(string strMessage, string strDetails, bool fShow = true) {
            _oMainWin.LogError( this, strMessage, strDetails, fShow );
        }

        public void Notify(ShellNotify eEvent) {
        }

        public void NotifyFocused(bool fSelect) {
        }
    }

	/// <summary>
	/// Similar to the stack EXCEPT we only show one of the children 
    /// at a time. Each child is the same size. At present we expect
    /// only controls inside, but we could make it so we have sub
    /// layouts (windowless). That seems like overkill atm.
	/// </summary>
	public class LayoutExclusive : LayoutRect {
        /// <summary>
        /// So you can't create a KeyValuePair by simply "new". 
        /// You have to call some dumb ass "Create" static method
        /// because of some dumb ass reflection going on there.
        /// So just create one myself.
        /// </summary>
        public readonly struct MyPair {
            private readonly object   key; 
            private readonly Control  value; 

            public MyPair(object key, Control value) {
                this.key = key;
                this.value = value;
            }

            public object  Key   => key;
            public Control Value => value;
        }

		protected List<MyPair> _rgChildren   = new();
        protected int          _iMaxChildren = 0;

		public LayoutExclusive( bool fSolo ) {
            _iMaxChildren = fSolo ? 1 : int.MaxValue;
		}

        public int Count => _rgChildren.Count;

        protected override void OnSize() {
            //SmartRect oRect = new SmartRect( this );
            //oRect.Inflate( -1, _rcMargin );

            foreach( MyPair oPair in _rgChildren ) {
                oPair.Value.Bounds = Rect;
            }
        }

        /// <summary>
        /// No check for duplicate keys. So be careful.
        /// </summary>
        /// <exception cref="InvalidOperationException">You'll get this
        /// if you attempt to put too many elements in a solo container.</exception>
        /// <exception cref="NullReferenceException" />
        public void Add( object oKey, Control oControl ) {
            ArgumentNullException.ThrowIfNull(oControl);

            if( _rgChildren.Count >= _iMaxChildren )
                throw new InvalidOperationException("Solo herder cannot hold more than one control.");

            _rgChildren.Add( new MyPair( oKey, oControl ) );
        }

        public void Clear() {
            foreach( MyPair oPair in _rgChildren ) {
                oPair.Value.Dispose();
            }
            _rgChildren.Clear();
        }

        public IEnumerator<Control> GetEnumerator() {
			foreach( MyPair oPair in _rgChildren ) {
				yield return oPair.Value;
			}
        }

		public void Remove( object oKey ) {
			for( int i=0; i<_rgChildren.Count; ++i ) {
				MyPair oPair = _rgChildren[i];
                if( oPair.Key == oKey ) {
                    _rgChildren.RemoveAt(i);
                    return;
                }
			}
		}

        /// <summary>
        /// NOTE! If we are a solo container null or the current view
        /// is legal!! Kind of an oversight on my part. 
        /// </summary>
		public Control Find( object oKey ) {
            try {
                if( _iMaxChildren == 1 )
                    return _rgChildren[0].Value;;

			    foreach( MyPair oPair in _rgChildren ) {
				    if( oPair.Key == oKey )
					    return oPair.Value;
			    }
            } catch( NullReferenceException ) {
            }
			return null;
		}

        /// <summary>
        /// Note that the Control::Hide() is a utility function that
        /// calls the Visible property.
        /// </summary>
		public override bool Hidden { 
			set {
				base.Hidden = value;
				foreach( Control oControl in this ) {
					oControl.Visible = !value;
				}
			}
		}

        public void Focus( object oKey ) {
            if( Hidden )
                return;

            if( Find( oKey ) is Control oControl ) {
                oControl.Select();
                oControl.Focus ();
            }
        }

        public void ToFront( object oKey ) {
            if( Find( oKey ) is Control oControl ) {
                oControl.BringToFront();
            }
        }

        public bool Shuffle( object oKey, bool fDesireVisible ) {
            bool fFound = false;

            if( _iMaxChildren == 1 )
                oKey = null;

            // Go thru and hide anything that's not the key. Only show the key
            // value if we're not being hidden.
			foreach( LayoutExclusive.MyPair oPair in _rgChildren ) {
			    if( oKey == oPair.Key && fDesireVisible ) {
					oPair.Value.BringToFront();
					oPair.Value.Show();
                    fFound = true;

                    // Shell sets all tabstops to false, we turn it on only if we are visible.
                    oPair.Value.TabStop = this[ SCALAR.WIDTH ] > 0 || this[ SCALAR.HEIGHT] > 0;
			    } else {
					oPair.Value.Hide();
                }
			}

            return fFound;
        }
    }

    /// <summary>
    /// New implementation for a herder. Work in progress.
    /// </summary>
    public class SmartHerderBase : 
        LayoutStackVariable,
        IDisposable
    {
        protected SideIdentify  _eSide;
        protected MainWin       _oHost;
                  Line          _oTitleText;
                  SHOWSTATE     _eViewState = SHOWSTATE.Inactive;

        public Guid Decor { get; protected set; }

        IPgMenuVisibility _oMenuVis = null; // pointer to shell menu entry.

        protected readonly IPgFontRender          _oFontRender;
      //protected readonly ImageSoloDoc           _oDocIcon;
        protected readonly ImageSoloDoc           _oDocCloser; // BUG: Temp here for now.
        protected readonly List<LayoutSingleLine> _rgLayoutText  = new();
        protected readonly LayoutExclusive        _rgLayoutInner; 
        protected readonly LayoutStackVariable    _rgLayoutBar   = new() { Track = 30, Style=LayoutRect.CSS.Pixels };

        LayoutGdiBitmap     _oViewIcon;

        protected readonly bool _fSolo;

        public SmartHerderBase(  MainWin       oMainWin, 
                                 string        strResource, 
                                 string        strTitle, 
                                 Guid          gDecor,
                                 IPgFontRender oFontRender, 
                                 bool          fSolo ) :
			base(  )
        {
            ArgumentNullException.ThrowIfNull( oMainWin );
            ArgumentNullException.ThrowIfNull( oFontRender );

            Decor        = gDecor;
            Spacing      = 5;

            _oHost       = oMainWin;
    		_oFontRender = oFontRender; 
            _oTitleText  = new TextLine( 0, strTitle );
          //_oDocIcon    = new( new HerderSlot( oMainWin ) );

            _rgLayoutInner = new LayoutExclusive( fSolo );

            Assembly     oAsm = Assembly.GetExecutingAssembly();
          //string[] rgStrs = oAsm.GetManifestResourceNames();
            AssemblyName assemblyName = oAsm.GetName();
            string       strAsmName   = assemblyName.Name;
            string       strFullName  = assemblyName.Name + "." + strResource;

          //_oDocIcon.LoadResource( oAsm, assemblyName.Name + "." + strResource );

            _oDocCloser = new( new HerderSlot( oMainWin ) );

            _oDocCloser.LoadResource( oAsm, "Mjolnir.Content.icons8-close-window-94-2.png" );

            /*LayoutBmpDoc */    _oViewIcon  = new LayoutGdiBitmap( oAsm, strFullName ) 
                                            { Units = LayoutRect.CSS.Flex, Hidden = false, Border = new Size( 0, 0 ) };
			LayoutSingleLine oViewTitle = new LayoutSingleLine( new FTCacheWrap( _oTitleText ), LayoutRect.CSS.Flex ) 
                                            { BgColor = SKColors.Transparent };
            LayoutSKBitmap     oViewKill  = new LayoutSKBitmap( _oDocCloser ) 
                                            { Units = LayoutRect.CSS.Flex, Hidden = false };

			_rgLayoutText.Add( oViewTitle );

            // When this horizontal...
            _rgLayoutBar.Add( _oViewIcon );
            _rgLayoutBar.Add( oViewTitle );
            _rgLayoutBar.Add( oViewKill );

            // ...This is vertical!
            this.Add( _rgLayoutBar );
            this.Add( _rgLayoutInner );

            Orientation = SideIdentify.Left;
            Style=LayoutRect.CSS.Percent;
        }

        /// <summary>
        /// Save the value and update the Direction...
        /// </summary>
        public SideIdentify Orientation { 
            get => _eSide;
            set { Direction = ToDirection( value );
                     _eSide = value;
                }
        }

        /// <summary>
        /// Maybe this belongs on the main window... :-/
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public static TRACK ToDirection( SideIdentify eSite ) {
            switch( eSite ) {
                case SideIdentify.Left:
                case SideIdentify.Right: 
                    return TRACK.VERT;
                case SideIdentify.Bottom: 
                    return TRACK.HORIZ;
                case SideIdentify.Tools:
                    return TRACK.VERT;
                case SideIdentify.Options:
                    return TRACK.HORIZ;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        /// <summary>
        /// The (title) bar is always orthoginal direction to the body.
        /// </summary>
		public override TRACK Direction { 
            set { base.Direction = value;
                  _rgLayoutBar.Direction = value == TRACK.HORIZ ? TRACK.VERT : TRACK.HORIZ; 
                }
        }

        protected override void OnSize() {
            LayoutChildren();
        }

        public override bool LayoutChildren() {
            foreach( LayoutSingleLine oLayout in _rgLayoutText ) {
                oLayout.Cache.Measure( _oFontRender );
                oLayout.OnChangeFormatting();
                oLayout.Cache.OnChangeSize( oLayout.Width );
            }
            base.LayoutChildren();

            return true;
        }

        public string Title => _oTitleText.ToString();

        public bool TabStop {
            get; set;
        }

        public Guid Guid {
            get { return Decor; }
        }

        // Debug helper.
        public override string ToString() {
            return _oTitleText + "@" + Orientation.ToString();
        }

        public bool DesiresVisiblity {
            get {
                if( _oMenuVis == null )
                    return true;

                return _oMenuVis.Checked;
            }
        }

        public IPgMenuVisibility MenuVisiblityObject {
            set {
                _oMenuVis = value;
            }
        }

        /// <summary>
        /// GotFocus is going to blow our value. Why do I have an 'set' accessor.
        /// </summary>
        public virtual SHOWSTATE Show {
            get {
                return _eViewState;
            }

            set {
				Hidden      = false;
                _eViewState = value;
            }
        }

        public override void Paint( SKCanvas skCanvas ) {
            if( Hidden ) 
                return;

            foreach( LayoutRect oLayout in this ) {
                oLayout.Paint( skCanvas );
            }

			// DEBUG Code.

            //SKPaint oPaint = new SKPaint() { Color = SKColors.Aquamarine };
            //skCanvas.DrawLine(
            //          _rgLayoutInner.GetScalar(SCALAR.LEFT),
            //          _rgLayoutInner.GetScalar(SCALAR.TOP),
            //          _rgLayoutInner.GetScalar(SCALAR.RIGHT),
            //          _rgLayoutInner.GetScalar(SCALAR.BOTTOM),
            //          oPaint );
        }

        // https://stackoverflow.com/questions/45077047/rotate-photo-with-skiasharp
        /// <summary>
        /// Going to try to add this backwards compat to my new herder implementation.
        /// </summary>
        /// <param name="p_oGraphics"></param>
        public override void Paint( Graphics p_oGraphics )
        {
            if( this.Rect.Width <= 0 || this.Rect.Height <= 0 /* || Orientation == Top */ 
                || Hidden || _oHost.DecorFont == null )
                return; // Nothing to do.

            try {
                // Need this to clip the bitmap.
                SmartRect oRect = new SmartRect( this );
                oRect.SetScalar( SET.RIGID, SCALAR.LEFT, 0 );
                oRect.SetScalar( SET.RIGID, SCALAR.TOP,  0 );
                Region oRgn     = new Region( oRect.Rect );

                using( oRgn ) {
                    SKPointI oPointImage = _rgLayoutBar.GetPoint(LOCUS.UPPERLEFT);
                    Region   oOldRgn     = p_oGraphics.Clip;

                    Brush oBrush = _eViewState == SHOWSTATE.Focused ? 
                                                    _oHost.ToolsBrushActive : Brushes.LightGray;
                    p_oGraphics.FillRectangle( oBrush,
                                                _rgLayoutBar.Left,
                                                _rgLayoutBar.Top, 
                                                _rgLayoutBar.Width  + 1,
                                                _rgLayoutBar.Height + 1);

                    p_oGraphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

                    _oViewIcon.Paint( p_oGraphics );

                    p_oGraphics.TranslateTransform( _rgLayoutBar.Left, _rgLayoutBar.Top );
                    p_oGraphics.Clip = oRgn;

                    int   iBoxWidth   = _rgLayoutBar.Width < _rgLayoutBar.Height ? 
                                        _rgLayoutBar.Width : _rgLayoutBar.Height;
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

                    p_oGraphics.DrawString( _oTitleText.ToString(), _oHost.DecorFont, Brushes.Black, pntTemp );
                    p_oGraphics.ResetTransform();

                    p_oGraphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.Default;

                    p_oGraphics.Clip = oOldRgn;
                }
            } catch( OverflowException ) {
                // Probably rectangle inside out...
            }
        }

        /// <summary>
        /// Bring to front the adornment that matches the view at the site given.
        /// </summary>
        /// <param name="oSite">The site for the view who's decor we want to show.</param>
        public bool AdornmentShuffle( object oKey )
        {
            return _rgLayoutInner.Shuffle( oKey, DesiresVisiblity );
        }
        /// <summary>
        /// This hides all the adornments but does not set the hidden property.
        /// </summary>
        public void AdornmentHideAll() {
            Hidden = true;
        }
		public void AdornmentCloseAll() {
            Hidden = true;
            _rgLayoutInner.Clear();
        }

        public void AdornmentFocus( object oKey ) {
            _rgLayoutInner.Focus( oKey );
        }

        public virtual bool IsContained( ViewSlot oSite ) {
            return _rgLayoutInner.Find( oSite ) is not null;
        }

        /// <summary>
        /// DecorSite calls this function in the ViewSite.EventChain.OnFocus event.
        /// </summary>
        public void OnFocused() {
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
        public void OnBlurred() {
			_eViewState = SHOWSTATE.Inactive;
        }

        /// <summary>
        /// In the singleton case I'm allowing the key to be the control
        /// itself for backward compat. I'll check if everything has a site
        /// and if so change the key param
        /// </summary>
        /// <param name="oKey">Associated site, may be null. For singletons
        ///                    this might be the control.</param>
        /// <param name="oControl">Control to add.</param>
		/// <exception cref="ArgumentNullException" />
		/// <exception cref="InvalidOperationException" />
        public virtual void AdornmentAdd( object oKey, Control oControl ) {
            oControl.Parent  = _oHost;
            oControl.Bounds  = _rgLayoutInner.Rect; // Margin? Ignore for now.
            oControl.Visible = !Hidden;

            _rgLayoutInner.Add( oKey, oControl );

            // Get the events from our guest control.
            //oControl.GotFocus  += _oGotFocusHandler;
            //oControl.LostFocus += _oLostFocusHandler;
        }

        /// <summary>
        /// Not that any adornments get removed but it's here if you want.
        /// </summary>
        public void AdornmentRemove( object oKey ) {
            _rgLayoutInner.Remove( oKey );
        }

		public Control AdornmentFind( ViewSlot oKey ) {
            return _rgLayoutInner.Find( oKey );
        }

        public virtual void Dispose() {
            _oMenuVis = null; // probably not strictly necessary. But break the loop.
        }

        #region ISmartDragGuest Members

        public void HoverStop() {
        }

        public bool Hovering {
            get {
                return false;
            }
        }

        /// <summary>
        /// This is our chance to provide a visual cue for what we are doing.
        /// </summary>
        public bool Hover( int p_iX, int p_iY, out bool fChanged ) {
			fChanged = false;
            return false;
        }

        #endregion

        public override bool IsInside(int p_iX, int p_iY)  {
            if( Hidden )
                return false;

            return base.IsInside( p_iX, p_iY );
        }
    }

    public class SmartHerderClxn : SmartHerderBase {
        public SmartHerderClxn( MainWin oMainWin, string strResource, 
                                 string strTitle, Guid guidName,
                                 IPgFontRender oFontRender ) :
            base( oMainWin, strResource, strTitle, guidName, oFontRender, fSolo:false )
        {
        }
    }
    public class SmartHerderSolo : SmartHerderBase {
        public SmartHerderSolo( MainWin oMainWin, string strResource, 
                                 string strTitle, Guid guidName,
                                 IPgFontRender oFontRender ) :
            base( oMainWin, strResource, strTitle, guidName, oFontRender, fSolo:true )
        {
        }
    }

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
            base( p_oFinished, p_herderGuest, SET.RIGID, LOCUS.UPPERLEFT, p_iX, p_iY, null )
        {
            if( p_oFinished == null )
                throw new ArgumentNullException( "Need the 'finished' delegate" );
        }
    }

    /// <summary>
    /// Tack this onto the main window and we should be
    /// good to go after I make the main window an SKControl.
    /// </summary>
    public interface IPgShutdownNotify : IDisposable {
        bool IsDisposed       { get; }
        bool RecreatingHandle { get; }

        event EventHandler WinHandleDestroyed;
    }

    /// <summary>
    /// Turns out you don't need a Form window to hand to the
    /// Application.Run() method. You can also pass it an
    /// application context. It is a bit of a bummer that all
    /// my subclass needs to include Form class baggage, but
    /// I think I've coded all that out with this class. 
    /// </summary>
    public class MyApplicationContext : ApplicationContext
    {
        private readonly IPgShutdownNotify _myForm;

        /// <summary>
        ///  Creates a new ApplicationContext with the specified mainForm.
        ///  If OnMainFormClosed is not overridden, the thread's message
        ///  loop will be terminated when mainForm is closed.
        /// </summary>
        public MyApplicationContext( IPgShutdownNotify mainForm ) : base()
        {
            _myForm = mainForm ?? throw new ArgumentException();

            _myForm.WinHandleDestroyed += OnMainFormDestroy;
        }

        protected override void Dispose(bool disposing) {
            if( disposing ) {
                if( !_myForm.IsDisposed ) {
                    _myForm.Dispose();
                }
            }

            base.Dispose( disposing );

            // If you are adding releasing unmanaged resources code here (disposing == false), you need to:
            // 1. remove GC.SuppressFinalize from constructor of this class and from all of its subclasses
            // 2. remove ApplicationContext_Subclasses_SuppressFinalizeCall test
            // 3. modify ~ApplicationContext() description.
        }

        /// <summary>
        ///  Called when the mainForm is closed. The default implementation
        ///  of this will call ExitThreadCore.
        /// </summary>
        private void OnMainFormDestroy( object sender, EventArgs e ) {
            if( sender is IPgShutdownNotify oShutter ) {
                if( !oShutter.RecreatingHandle ) {
                    oShutter.WinHandleDestroyed -= OnMainFormDestroy;
                    ExitThreadCore();
                } 
            }
        }
    }
}
