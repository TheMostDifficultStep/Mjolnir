using Play.Edit;
using Play.Forms;
using Play.ImageViewer;
using Play.Interfaces.Embedding;
using Play.Rectangles;

using SkiaSharp;

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Windows.Forms;

namespace Mjolnir {
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
        public readonly struct AuPair {
            private readonly object   key; 
            private readonly Control  value; 

            public AuPair(object key, Control value) {
                this.key = key;
                this.value = value;
            }

            public object  Key   => key;
            public Control Value => value;
        }

		protected List<AuPair> _rgChildren   = new();
        protected int          _iMaxChildren = 0;

		public LayoutExclusive( bool fSolo ) {
            _iMaxChildren = fSolo ? 1 : int.MaxValue;
		}

        public int Count => _rgChildren.Count;

        protected override void OnSize() {
            foreach( AuPair oPair in _rgChildren ) {
                oPair.Value.Bounds = Rect;
            }
        }

        public void Add( object oKey, Control oControl ) {
            ArgumentNullException.ThrowIfNull(oControl);

            if( _rgChildren.Count >= _iMaxChildren )
                throw new InvalidOperationException("Solo herder cannot hold more than one control.");

            _rgChildren.Add( new AuPair( oKey, oControl ) );
        }

        public void Clear() {
            foreach( AuPair oPair in _rgChildren ) {
                oPair.Value.Dispose();
            }
            _rgChildren.Clear();
        }

        public IEnumerator<Control> GetEnumerator() {
			foreach( AuPair oPair in _rgChildren ) {
				yield return oPair.Value;
			}
        }

		public void Remove( object oKey ) {
			for( int i=0; i<_rgChildren.Count; ++i ) {
				AuPair oPair = _rgChildren[i];
                if( oPair.Key == oKey ) {
                    _rgChildren.RemoveAt(i);
                    return;
                }
			}
		}

        /// <summary>
        /// It's a little cheesy, but if we're a solo container, the key is
        /// the object itself, since we can't load a null into the dictionary
        /// (lame). So since the only way through a dictionary is iteration,
        /// just force return on the first element.
        /// </summary>
        /// <remarks>I could add an assert that if the key is null, the
        /// collection only contains one element. I'll see if I need it
        /// later.</remarks>
		public Control Find( object oKey ) {
			foreach( AuPair oPair in _rgChildren ) {
				if( oPair.Key == oKey )
					return oPair.Value;
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
            if( Find( oKey ) is Control oControl ) {
                oControl.Select();
            }
        }

        public void ToFront( object oKey ) {
            if( Find( oKey ) is Control oControl ) {
                oControl.BringToFront();
            }
        }
    }

    /// <summary>
    /// New implementation for a herder. Work in progress.
    /// </summary>
    public class SmartHerderBase2 : 
        LayoutRect,
        IDisposable
    {
        protected SideIdentify  _eSide;
        protected MainWin       _oHost;
                  Line          _oTitleText;
                  SHOWSTATE     _eViewState = SHOWSTATE.Inactive;

        public Guid Decor { get; protected set; }

        IPgMenuVisibility _oMenuVis = null; // pointer to shell menu entry.

        protected readonly IPgFontRender          _oFontRender;
        protected readonly ImageSoloDoc           _oDocIcon;
        protected readonly ImageSoloDoc           _oDocCloser; // BUG: Temp here for now.
        protected readonly List<LayoutSingleLine> _rgLayoutText  = new();
        protected readonly LayoutExclusive        _rgLayoutInner; 
        protected readonly LayoutStackVariable    _rgLayoutBar   = new();
        protected readonly LayoutStackVariable    _rgLayoutOuter = new();

        protected readonly bool _fSolo;

        public SmartHerderBase2(  MainWin       oMainWin, 
                                 string        strResource, 
                                 string        strTitle, 
                                 Guid          gDecor,
                                 IPgFontRender oFontRender, 
                                 bool          fSolo ) :
			base( CSS.Percent )
        {
            ArgumentNullException.ThrowIfNull( oMainWin );
            ArgumentNullException.ThrowIfNull( oFontRender );

            Track        = 30;
            Decor        = gDecor;

            _oHost       = oMainWin;
    		_oFontRender = oFontRender; 
            _oTitleText  = new TextLine( 0, strTitle );
            _oDocIcon    = new( new HerderSlot( oMainWin ) );

            _rgLayoutInner = new LayoutExclusive( fSolo );

            Assembly     oAsm = Assembly.GetExecutingAssembly();
          //string[] rgStrs = oAsm.GetManifestResourceNames();
            AssemblyName assemblyName = oAsm.GetName();
            string       strAsmName = assemblyName.Name;

            _oDocIcon.LoadResource( oAsm, assemblyName.Name + "." + strResource );

            _oDocCloser = new( new HerderSlot( oMainWin ) );

            _oDocCloser.LoadResource( oAsm, "Mjolnir.Content.icons8-close-window-94-2.png" );

            LayoutBmpDoc     oViewIcon  = new LayoutBmpDoc( _oDocIcon ) 
                                            { Units = LayoutRect.CSS.Flex, Hidden = true };
			LayoutSingleLine oViewTitle = new LayoutSingleLine( new FTCacheWrap( _oTitleText ), LayoutRect.CSS.None ) 
                                            { BgColor = SKColors.Transparent };
            LayoutBmpDoc     oViewKill  = new LayoutBmpDoc( _oDocCloser ) 
                                            { Units = LayoutRect.CSS.Flex, Hidden = true };

			_rgLayoutText.Add( oViewTitle );

            // When this horizontal...
            _rgLayoutBar.Add( oViewIcon );
            _rgLayoutBar.Add( oViewTitle );
            _rgLayoutBar.Add( oViewKill );

            // ...This is vertical!
            _rgLayoutOuter.Add( _rgLayoutBar );
            _rgLayoutOuter.Add( _rgLayoutInner );

            Orientation = SideIdentify.Left;
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
		protected TRACK Direction { 
            set { _rgLayoutOuter.Direction = value;
                  _rgLayoutBar .Direction = value == TRACK.HORIZ ? TRACK.VERT : TRACK.HORIZ; }
            get { return _rgLayoutOuter.Direction; } 
        }

        protected override void OnSize() {
            _rgLayoutOuter.SetRect( Left, Top, Right, Bottom );
        }

        public override bool LayoutChildren() {
            foreach( LayoutSingleLine oLayout in _rgLayoutText ) {
                oLayout.Cache.Measure( _oFontRender );
                oLayout.OnChangeFormatting();
                oLayout.Cache.OnChangeSize( oLayout.Width );
            }
            _rgLayoutOuter.LayoutChildren();

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

            foreach( LayoutRect oLayout in _rgLayoutOuter ) {
                oLayout.Paint( skCanvas );
            }

			// DEBUG Code.

            SKPaint oPaint = new SKPaint() { Color = SKColors.Aquamarine };
            skCanvas.DrawLine(
                      _rgLayoutInner.GetScalar(SCALAR.LEFT),
                      _rgLayoutInner.GetScalar(SCALAR.TOP),
                      _rgLayoutInner.GetScalar(SCALAR.RIGHT),
                      _rgLayoutInner.GetScalar(SCALAR.BOTTOM),
                      oPaint );
        }

        /// <summary>
        /// Bring to front the adornment that matches the view at the site given.
        /// </summary>
        /// <param name="oSite">The site for the view who's decor we want to show.</param>
        public bool AdornmentShuffle( object oKey ) {
            _rgLayoutInner.ToFront( oKey );
            return true;
        }
        /// <summary>
        /// This hides all the adornments but does not set the hidden property.
        /// </summary>
        public void AdornmentHideAll() {
            _rgLayoutOuter.Hidden = true;
        }
		public void AdornmentCloseAll() {
            _rgLayoutOuter.Hidden = true;
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

    public class SmartHerderClxn2 : SmartHerderBase2 {
        public SmartHerderClxn2( MainWin oMainWin, string strResource, 
                                 string strTitle, Guid guidName,
                                 IPgFontRender oFontRender ) :
            base( oMainWin, strResource, strTitle, guidName, oFontRender, fSolo:false )
        {
        }
    }
    public class SmartHerderSolo2 : SmartHerderBase2 {
        public SmartHerderSolo2( MainWin oMainWin, string strResource, 
                                 string strTitle, Guid guidName,
                                 IPgFontRender oFontRender ) :
            base( oMainWin, strResource, strTitle, guidName, oFontRender, fSolo:true )
        {
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
