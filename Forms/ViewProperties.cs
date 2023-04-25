using System;
using System.Collections.Generic;
using System.Windows.Forms;

using SkiaSharp;
using SkiaSharp.Views.Desktop;

using Play.Rectangles;
using Play.Interfaces.Embedding;
using Play.Edit;
using Play.Parse;

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
        IPgLoad
     {
        protected DocProperties Document { get; }
		protected readonly IPgStandardUI2 _oStdUI;

		public SKColor BgColorDefault { get; protected set; }

        public IPgParent Parentage => _oSiteView.Host;
        public IPgParent Services  => Parentage.Services;

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

        /// <remarks>
        /// TODO: Make the FormsWindow base take the DocForms.
        /// </remarks>
        public WindowStandardProperties( IPgViewSite oSiteView, DocProperties oDocument ) : 
            base( oSiteView, oDocument.PropertyDoc ) 
        {
            Document = oDocument ?? throw new ArgumentNullException( "ViewStandardProperties's Document is null." );
 			_oStdUI  = oSiteView.Host.Services as IPgStandardUI2 ?? throw new ArgumentException( "Parent view must provide IPgStandardUI service" );

			BgColorDefault = _oStdUI.ColorsStandardAt( StdUIColors.BG );
        }

        protected override void Dispose(bool disposing) {
            if( disposing && !_fDisposed ) {
                Document.ListenerRemove( this );
            }
            base.Dispose(disposing);
        }

        public override bool InitNew() {
            if( !base.InitNew() ) 
                return false;

            LayoutTable oLayout = new LayoutTable( 5, LayoutRect.CSS.Flex );
            Layout = oLayout;

            oLayout.AddColumn( LayoutRect.CSS.Flex, 30 ); // Name
            oLayout.AddColumn( LayoutRect.CSS.None, 70 ); // Value;

            InitRows();

            // The base formwindow already gets these, see the constructor.
            //Document.Property_Values.BufferEvent += OnBufferEvent_Doc_Property_Values;

            OnPropertyEvent( BUFFEREVENTS.MULTILINE );
            OnSizeChanged( new EventArgs() );

            // This certainly does not belong on the base form, but here
            // it is a little more reasonable.
            Links.Add( "callsign", OnCallSign );

            Document.ListenerAdd( this );

            return true;
        }

        public override int CaretHome { 
            get {
                return Document[0].At; // This is the first property value.
            } 
        }

        public void PropertyInitRow( LayoutTable oLayout, int iIndex, Control oWinValue = null ) {
            LabelValuePair sPropertyPair = Document.GetPropertyPair( iIndex );

            var oLayoutLabel = new LayoutSingleLine( new FTCacheWrap( sPropertyPair._oLabel ), LayoutRect.CSS.Flex );
            LayoutRect oLayoutValue;
            
            if( oWinValue == null ) {
                oLayoutValue = new LayoutSingleLine( new FTCacheWrap( sPropertyPair._oValue ), LayoutRect.CSS.Flex );
            } else { 
                // If the value is a multi-line value make an editor. And ignor the value line (if there is one).
                if( oWinValue is IPgLoad oWinLoad ) {
                    oWinLoad.InitNew();
                }
                oWinValue.Parent = this;
                oLayoutValue = new LayoutControl( oWinValue, LayoutRect.CSS.Flex, 100 );
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

        /// <summary>
        /// this is the default InitRows() function that set's everything. However InitNew() might
        /// get overriden and call the InitRows( int[] ) on a subset. 
        /// </summary>
        public virtual void InitRows() {
            if( Layout is not LayoutTable oTable ) {
                LogError( "Unexpected Layout for Property Page" );
                return;
            }

            List<int> rgTabOrder = new List<int>();
            for( int i = 0; i< Document.PropertyCount; ++i ) {
                PropertyInitRow( oTable, i );
                rgTabOrder.Add( i );
            }
            TabOrder = rgTabOrder.ToArray();
        }

        public virtual void InitRows( int[] rgShow ) {
            if( Layout is not LayoutTable oTable ) {
                LogError( "Unexpected Layout for Property Page" );
                return;
            }
            try {
                foreach( int iIndex in rgShow ) { 
                    PropertyInitRow( oTable, iIndex );
                }
                TabOrder = rgShow;
            } catch( Exception oEx ) {
                Type[] rgErrors = { typeof( IndexOutOfRangeException ),
                                    typeof( ArgumentOutOfRangeException ),
                                    typeof( NullReferenceException ),
                                    typeof( ArgumentNullException ) };
                if( rgErrors.IsUnhandled( oEx ) )
                    throw;

                LogError( "Bad property page index list" );
            }
        }

        protected void OnCallSign( Line oLine, IPgWordRange oRange ) {
            BrowserLink( "http://www.qrz.com/db/" +  oLine.SubString( oRange.Offset, oRange.Length) );
        }


        public void OnPropertyEvent( BUFFEREVENTS eEvent ) {
            OnDocumentEvent( BUFFEREVENTS.MULTILINE );
        }

        public override void OnFormLoad() {
            base.OnFormLoad();

            InitRows();
            OnDocumentEvent( BUFFEREVENTS.MULTILINE );
        }
    }
}
