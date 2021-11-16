using System;
using System.Collections.Generic;

using SkiaSharp;

using Play.Rectangles;
using Play.Interfaces.Embedding;
using Play.Edit;

namespace Play.Forms {
    /// <summary>
    /// View the DocProperties object. This makes two columns, label on the left
	/// and value on the right. It is capable of adding an editwindow for the values.
	/// We'll turn that into optionally a dropdown in the future.
    /// </summary>
    /// <seealso cref="DocProperties"/>
    public class WindowStandardProperties : 
        FormsWindow,
        IPgParent,
        IBufferEvents,
        IPgLoad
     {
        protected DocProperties Document { get; }
		protected readonly IPgStandardUI2 _oStdUI;

		public SKColor BgColorDefault { get; protected set; }

        public IPgParent Parentage => _oSiteView.Host;
        public IPgParent Services  => Parentage.Services;

        public WindowStandardProperties( IPgViewSite oSiteView, DocProperties oDocument ) : base( oSiteView, oDocument.Property_Values ) {
            Document = oDocument ?? throw new ArgumentNullException( "ViewStandardProperties's Document is null." );
 			_oStdUI  = oSiteView.Host.Services as IPgStandardUI2 ?? throw new ArgumentException( "Parent view must provide IPgStandardUI service" );

			BgColorDefault = _oStdUI.ColorsStandardAt( StdUIColors.BG );
        }

		protected class WinSlot :
			IPgViewSite
		{
			protected readonly WindowStandardProperties _oHost;

			public WinSlot( WindowStandardProperties oHost ) {
				_oHost = oHost ?? throw new ArgumentNullException();
			}

			public IPgParent Host => _oHost;

			public void LogError(string strMessage, string strDetails, bool fShow=true) {
				_oHost._oSiteView.LogError( strMessage, strDetails );
			}

			public void Notify( ShellNotify eEvent ) {
				_oHost._oSiteView.Notify( eEvent );
			}

            public IPgViewNotify EventChain => _oHost._oSiteView.EventChain;
        }

        public void PropertyInitRow( SmartTable oLayout, int iIndex, EditWindow2 oWinValue = null ) {
            var oLayoutLabel = new LayoutSingleLine( new FTCacheWrap( Document.Property_Labels[iIndex] ), LayoutRect.CSS.Flex );
            LayoutRect oLayoutValue;
            
            if( oWinValue == null ) {
                oLayoutValue = new LayoutSingleLine( new FTCacheWrap( Document.Property_Values[iIndex] ), LayoutRect.CSS.Flex );
            } else { // If the value is a multi-line value make an editor.
                oWinValue.InitNew();
                oWinValue.Parent = this;
                oLayoutValue = new LayoutControl( oWinValue, LayoutRect.CSS.Pixels, 100 );
            }

            oLayout.AddRow( new List<LayoutRect>() { oLayoutLabel, oLayoutValue } );

            oLayoutLabel.BgColor = _oStdUI.ColorsStandardAt( StdUIColors.BGReadOnly );

            CacheList.Add( oLayoutLabel );
            if( oLayoutValue is LayoutSingleLine oLayoutSingle ) {
				SKColor skBgColor = BgColorDefault;

				if( Document.ValueBgColor.TryGetValue( iIndex, out SKColor skBgColorOverride ) ) {
					skBgColor = skBgColorOverride;
				}
                oLayoutSingle.BgColor = skBgColor;
                CacheList.Add( oLayoutSingle );
            }
        }

        public override bool InitNew() {
            if( !base.InitNew() ) 
                return false;

            SmartTable oLayout = new SmartTable( 5, LayoutRect.CSS.None );
            Layout2 = oLayout;

            oLayout.Add( new LayoutRect( LayoutRect.CSS.Flex, 30, 0 ) ); // Name.
            oLayout.Add( new LayoutRect( LayoutRect.CSS.None, 70, 0 ) ); // Value.

            InitRows();

            Caret.Layout = CacheList[0];

            OnDocumentEvent( BUFFEREVENTS.MULTILINE );
            OnSizeChanged( new EventArgs() );

            return true;
        }

        public virtual void InitRows() {
            if( Layout2 is SmartTable oTable ) {
                foreach( Line oLine in Document.Property_Labels ) {
                    PropertyInitRow( oTable, oLine.At );
                }
            }
        }

        public void OnEvent( BUFFEREVENTS eEvent ) {
            OnDocumentEvent( BUFFEREVENTS.MULTILINE );
            Invalidate();
        }
    }
}
