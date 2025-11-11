using Play.Edit;
using Play.Forms;
using Play.ImageViewer;
using Play.Interfaces.Embedding;
using Play.Rectangles;

using SkiaSharp;
using SkiaSharp.Views.Desktop;

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
	/// Similar to the stack EXCEPT we only show one of the 
	/// children at a time. Each child is the same size.
	/// </summary>
	public class LayoutExclusive : LayoutRect {
		protected Dictionary<object, Control > _rgChildren = new();
		public LayoutExclusive() {
		}

        public int Count => _rgChildren.Count;

        public void Clear() {
            foreach( KeyValuePair<object, Control> oPair in _rgChildren ) {
                oPair.Value.Dispose();
            }
            _rgChildren.Clear();
        }

        public IEnumerator<Control> GetEnumerator() {
			foreach( KeyValuePair< object, Control > oPair in _rgChildren ) {
				yield return oPair.Value;
			}
        }

		public void Remove( object oKey ) {
			_rgChildren.Remove( oKey );
		}

		public Control Find( object oKey ) {
			foreach( KeyValuePair<object, Control> oPair in _rgChildren ) {
				if( oPair.Key == oKey )
					return oPair.Value;
			}
			return null;
		}

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
    }

    /// <summary>
    /// New implementation for a herder. Work in progress.
    /// </summary>
    internal abstract class SmartHerderBase2 : 
        LayoutRect,
        IDisposable
    {
        protected SideIdentify  _eSide;
        protected MainWin       _oHost;
                  Line          _oTitleText;
                  SHOWSTATE     _eViewState = SHOWSTATE.Inactive;

        public    Guid          Decor { get; protected set; }

        IPgMenuVisibility _oMenuVis = null; // pointer to shell menu entry.

        protected readonly List<LayoutSingleLine> _rgTextCache  = new();
        protected readonly IPgFontRender          _oFontRender;
        protected readonly ImageSoloDoc           _oIconDoc;
        protected readonly ImageSoloDoc           _oCloserDoc; // BUG: Temp for now.
        protected readonly LayoutExclusive        _oLayoutInner  = new(); 
        protected readonly LayoutStackVariable    _rgLayoutBar   = new();
        protected readonly LayoutStackVariable    _rgLayoutOuter = new();

        public SmartHerderBase2( MainWin       oMainWin, 
                                 string        strResource, 
                                 string        strTitle, 
                                 Guid          gDecor,
                                 IPgFontRender oFontRender ) :
			base( CSS.Percent )
        {
            ArgumentNullException.ThrowIfNull( oMainWin );
            ArgumentNullException.ThrowIfNull( oFontRender );

            Track = 30;

            _oHost       = oMainWin;
    		_oFontRender = oFontRender; 
            _oTitleText  = new TextLine( 0, strTitle );
            Decor        = gDecor;
            _oIconDoc    = new( new HerderSlot( oMainWin ) );

            Assembly oAsm = Assembly.GetExecutingAssembly();

            _oIconDoc.LoadResource( oAsm, strResource );

            _oCloserDoc = new( new HerderSlot( oMainWin ) );

            _oCloserDoc.LoadResource( oAsm, 
                                      "Mjolnir.Content.icons8-close-window-94-2.png" );

            LayoutBmpDoc     oViewIcon  = new LayoutBmpDoc( _oIconDoc ) 
                                            { Units = LayoutRect.CSS.Flex, Hidden = true };
			LayoutSingleLine oViewTitle = new LayoutSingleLine( new FTCacheWrap( _oTitleText ), LayoutRect.CSS.None ) 
                                            { BgColor = SKColors.Transparent };
            LayoutBmpDoc     oViewKill  = new LayoutBmpDoc( _oCloserDoc ) 
                                            { Units = LayoutRect.CSS.Flex, Hidden = true };

			_rgTextCache.Add( oViewTitle );

            // When this horizontal...
            _rgLayoutBar.Add( oViewIcon );
            _rgLayoutBar.Add( oViewTitle );
            _rgLayoutBar.Add( oViewKill );

            // ...This is vertical!
            _rgLayoutOuter.Add( _rgLayoutBar );
            _rgLayoutOuter.Add( _oLayoutInner );

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
        public void AdornmentHideAll() {
            _rgLayoutOuter.Hidden = true;
        }
		public void AdornmentCloseAll() {
            _oLayoutInner.Clear();
        }

        /// <summary>
        /// Set the focus to the tool window associated with the given document.
        /// Will fail if there are no windows held by the herder.
        /// </summary>
        /// <param name="oSite">The document who's tools we want to show.</param>
        public abstract void AdornmentFocus( object oSite );

        public virtual bool IsContained( ViewSlot oSite ) {
            return true;
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
            oControl.Bounds  = _oLayoutInner.Rect; // Margin? Ignore for now.
            oControl.Visible = !Hidden;

            // Get the events from our guest control.
            //oControl.GotFocus  += _oGotFocusHandler;
            //oControl.LostFocus += _oLostFocusHandler;

            return( true );
        }

        /// <summary>
        /// Not that any adornments get removed but it's here if you want.
        /// </summary>
        public void AdornmentRemove( object oViewSite ) {
            _oLayoutInner.Remove( oViewSite );
        }

		public Control AdornmentFind( ViewSlot oViewSite ) {
            return _oLayoutInner.Find( oViewSite );
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
