using System;
using System.Collections.Generic;

using SkiaSharp;

using Play.Interfaces.Embedding;
using Play.Rectangles;
using Play.Edit;
using Play.Forms;
using Play.ImageViewer;

namespace Mjolnir {
    /// <summary>
    /// Will need an instance per document per main window. Might be able to sneek by
    /// by simply tacking the Form document on the document site.
    /// </summary>
    public class Tabs : FormsWindow {
		readonly LayoutStack _rgLayoutTop = new LayoutStackHorizontal( 5 );

        public class Tab : LayoutRect, IPgParent {
            LayoutStack Layout { get; }

            public IPgParent Parentage => throw new NotImplementedException();
            public IPgParent Services  => Parentage.Services;

            protected Tabs Parent { get; }

            protected class ViewSlot :
			    IPgViewSite
		    {
			    protected readonly Tab _oHost;

			    public ViewSlot( Tab oHost ) {
				    _oHost = oHost ?? throw new ArgumentNullException();
			    }

			    public IPgParent Host => _oHost;

			    public void LogError(string strMessage, string strDetails, bool fShow=true) {
				    _oHost.Parent.LogError( strDetails );
			    }

			    public void Notify( ShellNotify eEvent ) {
				    _oHost.Parent._oSiteView.Notify( eEvent );
			    }

                public IPgViewNotify EventChain => _oHost.Parent._oSiteView.EventChain;
            }

            public Tab( Tabs oParent, CSS eLayout, TRACK eDir ) : base( eLayout ) {
                Parent = oParent ?? throw new ArgumentNullException( "Parent must not be null" );

                if( eDir == TRACK.HORIZ )
                    Layout = new LayoutStackHorizontal( 5 );
                else
                    Layout = new LayoutStackVertical( 5 );
            }

            /// <summary>
            /// Note that this can be a bit confusing after working with table and list objects
            /// where the layout is different from the rows. For the tabs there is only the layout.
            /// </summary>
            public bool InitNew( LayoutSingleLine oSingle, ImageSoloDoc oIcon ) {
                Layout.Add( oSingle );
                Layout.Add( new LayoutImageView ( new ImageViewSingle( new ViewSlot( this ), oIcon ) ) );

                return true;
            }
        }

        public Tabs( IPgViewSite oSiteView, Editor oForm ) : base( oSiteView, oForm ) {
        }

        public override bool InitNew() {
            GenerateTabs();

            DocForms.BufferEvent += Listen_FormEvent;

            return true;
        }

        private void Listen_FormEvent( BUFFEREVENTS eEvent ) {
            switch( eEvent ) {
                case BUFFEREVENTS.MULTILINE:
                    GenerateTabs();
                    break;
                case BUFFEREVENTS.SINGLELINE:
                    Invalidate();
                    break;
            }
        }

        public void GenerateTabs() {
            CacheList.Clear();

            foreach( Line oLine in DocForms ) {
                FTCacheLine      oElem   = new FTCacheLine( oLine );
                LayoutSingleLine oSingle = new LayoutSingleLine( oElem, LayoutRect.CSS.None );

		        _rgLayoutTop.Add( new Tab( this, LayoutRect.CSS.Flex, TRACK.HORIZ ) );

                CacheList.Add( oSingle );
            }

            OnSizeChanged( new EventArgs() );
        }
    }
}
