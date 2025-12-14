using Play.Edit;
using Play.ImageViewer;
using Play.Interfaces.Embedding;
using Play.Rectangles;
using SkiaSharp;
using System;
using System.Windows.Forms;
using System.Xml;

namespace Play.SSTV {
    /// <summary>
    /// This is going to be our new resize window "dialog" box. It is nice in that it
    /// cannot be put behind it's owner window. If we click on any children in the owner,
    /// this window will still stay on top. It took me awhile to figure out this magic
    /// combo but now we have a child/sub window with a title that cannot be put behind
    /// our main form and this is EXACTLY what we want.
    /// </summary>
    public abstract class WindowChildForm :
        Control,
        IPgParent,
        IPgLoad
    {
        protected readonly IPgViewSite   _oViewSite;
        protected readonly IPgViewNotify _oViewNotify;

        public IPgParent Parentage => _oViewSite.Host;
        public IPgParent Services  => Parentage.Services;

        protected abstract LayoutRect MyLayout { get; }
        
        public WindowChildForm( IPgViewSite oSite ) { 
            _oViewSite   = oSite ?? throw new ArgumentNullException( nameof( oSite ) );
            _oViewNotify = oSite.EventChain ?? throw new ArgumentException( "Site must support EventChain" );
        }

		protected class WinSlot :
			IPgViewSite,
			IPgViewNotify
		{
			protected readonly WindowChildForm _oHost;

			public ChildID ID { get;}

			public WinSlot(WindowChildForm oHost, ChildID eID ) {
				_oHost = oHost ?? throw new ArgumentNullException();
				ID     = eID;
			}

			public IPgParent Host => _oHost;

			public void LogError(string strMessage, string strDetails, bool fShow=true) {
				_oHost._oViewSite.LogError( strMessage, strDetails, fShow );
			}

			public void Notify( ShellNotify eEvent ) {
				_oHost._oViewSite.Notify( eEvent );
			}

            public void NotifyFocused(bool fSelect) {
				if( fSelect == true ) {
					_oHost._oViewSite.EventChain.NotifyFocused( fSelect );
				}
            }

            public bool IsCommandKey(CommandKey ckCommand, KeyBoardEnum kbModifier) {
                return _oHost._oViewSite.EventChain.IsCommandKey( ckCommand, kbModifier );
            }

            public bool IsCommandPress(char cChar) {
                return _oHost._oViewSite.EventChain.IsCommandPress( cChar );
            }

            public IPgViewNotify EventChain => this;
        }

        public virtual bool InitNew() {
            Text  = "Select Image Portion";

            return true;
        }

        protected void LogError( string strError ) {
            _oViewSite.LogError( "Resize Image", strError, fShow:true );
        }

		protected override void OnSizeChanged(EventArgs e) {
			base.OnSizeChanged(e);

			MyLayout.SetRect( 0, 0, ClientSize.Width, ClientSize.Height );
			MyLayout.LayoutChildren();

            //Invalidate();
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
    }

    /// <summary>
    /// 
    /// </summary>
    /// <seealso cref="ShowColorDialog"/>
    public class WindowImageResize : 
        WindowChildForm,
        IPgCommandView,
		IPgLoad<XmlElement>,
		IPgSave<XmlDocumentFragment>
    {

		public static Guid GUID { get; } = new Guid( "{7269F4D6-4C4B-4F8B-9FCA-0C99812F9D27}" );
		protected readonly WindowSoloImageNav _wnTxImageChoice;

        override protected LayoutRect MyLayout => _lyTxImageChoice;

        public string Banner => Text;

        public SKBitmap Icon => null;

        public Guid Catagory => GUID;

        public bool IsDirty => throw new NotImplementedException();

        protected LayoutControl _lyTxImageChoice;
        
        protected DocSSTV _oDocSSTV;

        public WindowImageResize( IPgViewSite oSite, DocSSTV oDoc ) : base(oSite) {
            _oDocSSTV = oDoc ?? throw new ArgumentNullException( nameof( oDoc ) );

            Parent = (Control)oSite.Host;

			_wnTxImageChoice = new WindowSoloImageNav( new WinSlot( this, ChildID.TxImageChoice ), oDoc.TxImageList );

			_wnTxImageChoice.Parent = this;
			_wnTxImageChoice.SetBorderOn();
            _wnTxImageChoice.ToolSelect = 1;
            _wnTxImageChoice.Aspect     = _oDocSSTV.TxImgLayoutAspect;
			_wnTxImageChoice.DragMode   = DragMode.FixedRatio;

            _lyTxImageChoice = new LayoutControl( _wnTxImageChoice, LayoutRect.CSS.Percent, 100 );
        }

        protected override void Dispose( bool fDisposing ) {
            if( _wnTxImageChoice is not null ) {
			    if( _wnTxImageChoice.Selection.IsEmpty() ) {
				    _wnTxImageChoice.Execute( GlobalCommands.SelectAll );
			    }
                _oDocSSTV.Selection.Copy = _wnTxImageChoice.Selection;

		        _wnTxImageChoice.Dispose();
            }

            _oDocSSTV.RenderComposite();

			_oDocSSTV.TxSSTVModeDoc.Event_Check        -= OnCheckedEvent_TxModeList;
            _oDocSSTV.TxImageList  .ImageUpdated       -= OnImageUpdated_TxImageList;
            _oDocSSTV.              Send_TxImageAspect -= OnTxImageAspect_SSTVDoc;

            base.Dispose( fDisposing );
        }

        public override bool InitNew() {
            if( !base.InitNew() )
                return false;

            if( !_wnTxImageChoice.InitNew() )
                return false;

			_oDocSSTV.TxSSTVModeDoc.Event_Check        += OnCheckedEvent_TxModeList;
            _oDocSSTV.TxImageList  .ImageUpdated       += OnImageUpdated_TxImageList;
            _oDocSSTV              .Send_TxImageAspect += OnTxImageAspect_SSTVDoc;

			_wnTxImageChoice.ToolSelect = 0; 

            OnSizeChanged( new EventArgs() );

            return true;
        }

        private void OnImageUpdated_TxImageList() {
            _wnTxImageChoice.Execute( GlobalCommands.SelectAll );
        }

        private void OnTxImageAspect_SSTVDoc( SKPointI skAspect ) {
            _wnTxImageChoice.Aspect = skAspect;

			if( _wnTxImageChoice.Selection.IsEmpty() ) {
				_wnTxImageChoice.Execute( GlobalCommands.SelectAll );
			}
        }

        /// <summary>
        /// TODO: Might be able to remove this now that I'm using the new
        /// Tx/Rx dropdowns... 
        /// </summary>
		protected void OnCheckedEvent_TxModeList( Row oRow ) {
			try {
                if( oRow is SSTVModeDoc.DDRow oCheckRow ) {
				    _wnTxImageChoice.Aspect = _oDocSSTV.TxResolution;

			        if( _wnTxImageChoice.Selection.IsEmpty() ) {
				        _wnTxImageChoice.Execute( GlobalCommands.SelectAll );
			        }

                    _oDocSSTV.Selection.Copy = _wnTxImageChoice.Selection;

                    // We don't know when the form will get it's OnCheckedEvent
                    // But if it get's it before us and Renders's we want it to
                    // do so again since we're changing the selection.
			        _oDocSSTV.RenderComposite();
                }
            } catch( NullReferenceException ) {
            }
		}

        public object Decorate(IPgViewSite oBaseSite, Guid sGuid) {
            return null;
        }

        public bool Execute(Guid sGuid) {
            return false;
        }

        public bool Load(XmlElement oStream) {
            return true;
        }

        public bool Save(XmlDocumentFragment oStream) {
            return true;
        }
    }
}
