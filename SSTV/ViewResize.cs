﻿using System;
using System.Drawing;
using System.Windows.Forms;
using System.Collections.Generic;

using SkiaSharp;

using Play.Interfaces.Embedding;
using Play.Rectangles;
using Play.Edit;
using Play.ImageViewer;

namespace Play.SSTV {
    /// <summary>
    /// This is going to be our new resize window "dialog" box. It is nice in that it
    /// cannot be put behind it's owner window. If we click on any children in the owner,
    /// this window will still stay on top. It took me awhile to figure out this magic
    /// combo but now we have a child/sub window with a title that cannot be put behind
    /// our main form and this is EXACTLY what we want.
    /// </summary>
    public abstract class WindowChildForm :
        Form,
        IPgParent,
        IPgLoad
    {
        protected readonly IPgViewSite _oSiteView;

        public IPgParent Parentage => _oSiteView.Host;
        public IPgParent Services  => Parentage.Services;

        protected abstract LayoutRect MyLayout { get; }
        
        public WindowChildForm( IPgViewSite oSite ) { 
            _oSiteView = oSite ?? throw new ArgumentNullException( nameof( oSite ) );
        }

		protected class WinSlot :
			IPgViewSite,
			IPgShellSite,
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
				_oHost._oSiteView.LogError( strMessage, strDetails, fShow );
			}

			public void Notify( ShellNotify eEvent ) {
				_oHost._oSiteView.Notify( eEvent );
			}

            public void NotifyFocused(bool fSelect) {
				if( fSelect == true ) {
					_oHost._oSiteView.EventChain.NotifyFocused( fSelect );
				}
            }

            public bool IsCommandKey(CommandKey ckCommand, KeyBoardEnum kbModifier) {
                return _oHost._oSiteView.EventChain.IsCommandKey( ckCommand, kbModifier );
            }

            public bool IsCommandPress(char cChar) {
                return _oHost._oSiteView.EventChain.IsCommandPress( cChar );
            }

            public IPgViewNotify EventChain => this;

            #region IPgShellSite
            public object AddView( Guid guidViewType, bool fFocus ) {
                return null;
            }

            public void FocusMe() {
                throw new NotImplementedException();
            }

            public void FocusCenterView() {
                throw new NotImplementedException();
            }

            public IEnumerable<IPgCommandView> EnumerateSiblings => throw new NotImplementedException();

            public uint SiteID => throw new NotImplementedException();

            #endregion
        }

        public virtual bool InitNew() {
            Text        = "Select Image Portion";
            Owner       = (Form)Parentage.TopWindow;
            MinimizeBox = false;
            Size        = new Size( 500, 500 );

            return true;
        }

        protected void LogError( string strError ) {
            _oSiteView.LogError( "Resize Image", strError, fShow:true );
        }

		protected override void OnSizeChanged(EventArgs e) {
			base.OnSizeChanged(e);

			MyLayout.SetRect( 0, 0, ClientSize.Width, ClientSize.Height );
			MyLayout.LayoutChildren();

            //Invalidate();
		}

        protected override void OnShown(EventArgs e) {
            base.OnShown(e);
            OnSizeChanged( e );
        }
    }

    public class WindowImageResize : WindowChildForm {

		protected readonly WindowSoloImageNav _wnTxImageChoice;

        override protected LayoutRect MyLayout => _lyTxImageChoice;
        
        protected LayoutControl _lyTxImageChoice;
        
        protected readonly ViewTransmitDeluxe _oParent;

        protected DocSSTV _oDocSSTV;

        public WindowImageResize(IPgViewSite oSite, DocSSTV oDoc ) : base(oSite) {
            _oDocSSTV = oDoc ?? throw new ArgumentNullException( nameof( oDoc ) );

            _oParent = ((ViewTransmitDeluxe)oSite.Host);

			_wnTxImageChoice = new WindowSoloImageNav( new WinSlot( this, ChildID.TxImageChoice ), oDoc.TxImageList );

			_wnTxImageChoice.Parent = this;
			_wnTxImageChoice.SetBorderOn();
            _wnTxImageChoice.ToolSelect = 1;

            _lyTxImageChoice = new LayoutControl( _wnTxImageChoice, LayoutRect.CSS.Percent, 100 );
        }

        protected override void OnFormClosing( FormClosingEventArgs e ) {
			if( _wnTxImageChoice.Selection.IsEmpty() ) {
				_wnTxImageChoice.Execute( GlobalCommands.SelectAll );
			}

            _oParent.Selection.Copy = _wnTxImageChoice.Selection;
            _oParent.RenderComposite();

			_oDocSSTV.TxModeList.CheckedEvent -= OnCheckedEvent_TxModeList;
            _oDocSSTV.Send_TxImageAspect      -= OnTxImageAspect_SSTVDoc;

		    _wnTxImageChoice.Dispose();
        }

        public override bool InitNew() {
            if( !base.InitNew() )
                return false;

            if( !_wnTxImageChoice.InitNew() )
                return false;

            _oDocSSTV.TxImageList.ImageUpdated += OnImageUpdated_TxImageList;
			_oDocSSTV.TxModeList.CheckedEvent  += OnCheckedEvent_TxModeList;
            _oDocSSTV.Send_TxImageAspect       += OnTxImageAspect_SSTVDoc;

			_wnTxImageChoice.ToolSelect = 0; 
			_wnTxImageChoice.Aspect     = _oDocSSTV.TxResolution;
			_wnTxImageChoice.DragMode   = DragMode.FixedRatio;

            OnSizeChanged( new EventArgs() );

            return true;
        }

        private void OnImageUpdated_TxImageList() {
            _wnTxImageChoice.Execute( GlobalCommands.SelectAll );
        }

        private void OnTxImageAspect_SSTVDoc( SKPointI skAspect ) {
            _wnTxImageChoice.Aspect = skAspect;
        }

		protected void OnCheckedEvent_TxModeList( Line oLineChecked ) {
			try {
				_wnTxImageChoice.Aspect = _oDocSSTV.TxResolution;

				_oDocSSTV.TemplateSet( oLineChecked.At );

			    if( _wnTxImageChoice.Selection.IsEmpty() ) {
				    _wnTxImageChoice.Execute( GlobalCommands.SelectAll );
			    }

			    _oDocSSTV.RenderComposite( _wnTxImageChoice.Selection.SKRect );
            } catch( NullReferenceException ) {
            }
		}
    }
}
