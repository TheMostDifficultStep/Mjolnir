using Play.Edit;
using Play.Forms;
using Play.ImageViewer;
using Play.Interfaces.Embedding;
using Play.Rectangles;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;

namespace Mjolnir {
    internal class MWHerderSlot : 
        IPgViewSite, 
        IPgViewNotify
 {
        readonly MainWin _oMainWin;
        public MWHerderSlot( MainWin oMainWin ) {
            _oMainWin = oMainWin;
        }
        public IPgViewNotify EventChain => this;

        public IPgParent Host => throw new NotImplementedException();

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
    /// Base class for a herder. We have two sub classes, one for herders that hold a single object only.
    /// And others that hold one per view. Since the "solo" case doesn't need an index to the given item
    /// we pass null. Solo objects looking for null should complain if an add comes with an index object
    /// Collection objects always need an index object and should complain if get a null index. Not that null
    /// doesn't seem like a fine index value, but it seems various .net collections are starting to throw
    /// exceptions. Alas, this conflict is because the tool windows for the shell might created per document
    /// or be single like for the shell main output window.
    /// </summary>
    internal abstract class SmartHerderBase2 : 
        LayoutRect,
        IDisposable
    {
        protected MainWin       _oHost;
                  Line          _oTitleText;
                  SHOWSTATE     _eViewState = SHOWSTATE.Inactive;
                  bool          _fHideTitle = false;
                  ShowImageSolo _oViewIcon;

        public    Guid          Decor { get; protected set; }

        IPgMenuVisibility _oMenuVis = null; // pointer to shell menu entry.

        protected readonly SmartRect _rcInner  = new SmartRect(); // The inner rect for our flock!
        protected readonly List<LayoutSingleLine> _rgTextCache  = new();
        protected readonly IPgFontRender          _oFontRender;
        protected readonly ImageSoloDoc           _oIconDoc;
        protected readonly LayoutRect             _oLayoutInner = new(); // BUG, placeholder
        protected readonly LayoutStackChoosy      _rgLayoutBar  = new();
        protected readonly LayoutStackChoosy      _rgLayoutBody = new();

        public SmartHerderBase2( MainWin       oMainWin, 
                                 string        strResource, 
                                 string        strTitle, 
                                 Guid          gDecor,
                                 IPgFontRender oFontRender ) :
			base( CSS.Percent )
        {
            Track = 30;

            _oHost       = oMainWin;
    		_oFontRender = oFontRender; 
            _oTitleText  = new TextLine( 0, strTitle );
            _oViewIcon   = new( strResource );
            Decor        = gDecor;
            _oIconDoc    = new( new MWHerderSlot( oMainWin ) );

            _oIconDoc.LoadResource( Assembly.GetExecutingAssembly(), strResource );

            LayoutBmpDoc     oViewIcon  = new LayoutBmpDoc( _oIconDoc ) 
                                            { Units = LayoutRect.CSS.Flex, Hidden = true };
			LayoutSingleLine oViewTitle = new LayoutSingleLine( new FTCacheWrap( _oTitleText ), LayoutRect.CSS.None ) 
                                            { BgColor = SKColors.Transparent };

			_rgTextCache.Add( oViewTitle );

            // When this horizontal...
            _rgLayoutBar.Add( oViewIcon );
            _rgLayoutBar.Add( oViewTitle );

            // ...This is vertical!
            _rgLayoutBody.Add( _rgLayoutBar );
            _rgLayoutBody.Add( _oLayoutInner );
        }

        /// <summary>
        /// Save the value and update or Direction...
        /// </summary>
        public SideIdentify Orientation { 
            get => throw new NotImplementedException();
            set => throw new NotImplementedException(); 
        }

        /// <summary>
        /// Normally it's the LayoutStack that has direction. But we're
        /// overloading that idea here.
        /// </summary>
		protected TRACK Direction { 
            set { _rgLayoutBody.Direction = value;
                  _rgLayoutBar .Direction = value == TRACK.HORIZ ? TRACK.VERT : TRACK.HORIZ; }
            get { return _rgLayoutBody.Direction; } 
        }

        public override bool LayoutChildren() {
            foreach( LayoutSingleLine oLayout in _rgTextCache ) {
                oLayout.Cache.Measure( _oFontRender );
                oLayout.OnChangeFormatting();
                oLayout.Cache.OnChangeSize( oLayout.Width );
            }

            return true;
        }

        public abstract bool TabStop {
            get; set;
        }

        public Guid Guid {
            get { return Decor; }
        }

        public bool HideTitle {
            set { _fHideTitle = value; }
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

            foreach( LayoutRect oLayout in _rgLayoutBar ) {
                oLayout.Paint( skCanvas );
            }

			// DEBUG Code.
            //p_oGraphics.DrawLine(Pens.Aquamarine,
            //          _rcInner.GetScalar(SCALAR.LEFT),
            //          _rcInner.GetScalar(SCALAR.TOP),
            //          _rcInner.GetScalar(SCALAR.RIGHT),
            //          _rcInner.GetScalar(SCALAR.BOTTOM));
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
        /// Add an adornment to this herd. This set's up the sinks for the
        /// focus events so we know when the adornment gets/loses focus.
        /// </summary>
        /// <param name="oSite">Associated site, may be null.</param>
        /// <param name="oControl">Control to add.</param>
        /// <remarks>Note: we're not actually keeping track of the children
        /// in us in this class. That's up to a subclass.</remarks>
		/// <exception cref="ArgumentNullException" />
		/// <exception cref="ArgumentException" />
        public virtual bool AdornmentAdd( object oSite, Control oControl ) {
            ArgumentNullException.ThrowIfNull(oControl);

            oControl.Parent  = _oHost;
            oControl.Bounds  = _rcInner.Rect; // Margin? Ignore for now.
            oControl.Visible = !Hidden;

            // Get the events from our guest control.
            //oControl.GotFocus  += _oGotFocusHandler;
            //oControl.LostFocus += _oLostFocusHandler;

            return( true );
        }

        /// <summary>
        /// Not that any adornments get removed but it's here if you want.
        /// </summary>
        public virtual void AdornmentRemove(object oViewSite) {
        }

		public abstract Control AdornmentFind( ViewSlot oViewSite);

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
            return( false );
        }

        #endregion

        public override bool IsInside(int p_iX, int p_iY)  {
            if( Hidden )
                return( false );

            return( base.IsInside( p_iX, p_iY ) );
        }
    }
}
