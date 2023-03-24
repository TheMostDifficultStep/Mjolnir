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

        //protected override void Dispose( bool disposing ) {
        //    if( disposing && !_fDisposed ) {
        //        Document.PropertyEvents -= OnPropertyEvent;
        //    }
        //    base.Dispose( disposing );
        //}

        public void PropertyInitRow( SmartTable oLayout, int iIndex, Control oWinValue = null ) {
            var oLayoutLabel = new LayoutSingleLine( new FTCacheWrap( Document.Property_Labels[iIndex] ), LayoutRect.CSS.Flex );
            LayoutRect oLayoutValue;
            
            if( oWinValue == null ) {
                oLayoutValue = new LayoutSingleLine( new FTCacheWrap( Document.Property_Values[iIndex] ), LayoutRect.CSS.Flex );
            } else { // If the value is a multi-line value make an editor.
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

        public override bool InitNew() {
            if( !base.InitNew() ) 
                return false;

            SmartTable oLayout = new SmartTable( 5, LayoutRect.CSS.None );
            Layout = oLayout;

            oLayout.Add( new LayoutRect( LayoutRect.CSS.Flex, 30, 0 ) ); // Name.
            oLayout.Add( new LayoutRect( LayoutRect.CSS.None, 70, 0 ) ); // Value.

            InitRows();

            // NOTE: This is happening when there is no album art.
            if( CacheList.Count > 0 ) {
                // ALSO: Need to keep the caret on the DocForms (Property_Values)
                foreach( LayoutSingleLine oTest in CacheList ) {
                    if( oTest.Cache.Line == DocForms[oTest.Cache.Line.At] ) {
                        Caret.Layout = oTest;
                        break;
                    }
                }
            }

            // The base formwindow already gets these, see the constructor.
            //Document.Property_Values.BufferEvent += OnBufferEvent_Doc_Property_Values;

            OnPropertyEvent( BUFFEREVENTS.MULTILINE );
            OnSizeChanged( new EventArgs() );

            // This certainly does not belong on the base form, but here
            // it is a little more reasonable.
            Links.Add( "callsign", OnCallSign );

            return true;
        }

        public virtual void InitRows() {
            if( Layout is not SmartTable oTable ) {
                LogError( "Unexpected Layout for Property Page" );
                return;
            }

            List<int> rgTabOrder = new List<int>();
            foreach( Line oLine in Document.Property_Labels ) {
                PropertyInitRow( oTable, oLine.At );
                rgTabOrder.Add( oLine.At );
            }
            TabOrder = rgTabOrder.ToArray();
        }

        public virtual void InitRows( int[] rgShow ) {
            if( Layout is not SmartTable oTable ) {
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
    }
}
