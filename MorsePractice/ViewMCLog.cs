using System;
using System.Xml;
using System.Windows.Forms;

using SkiaSharp;

using Play.Rectangles;
using Play.Interfaces.Embedding;
using Play.Edit;

namespace Play.MorsePractice {

	/*
		_rgLayout = new LayoutStackHorizontal() {
			Spacing = 15,
			Children = {
				new LayoutStackVertical( ) {
					Spacing = 15,
					Children = {
					new LayoutControl( ViewSource, LayoutRect.CSS.Percent, 40 ),
					new LayoutControl( ViewNotes,  LayoutRect.CSS.Percent, 60 )
					}
				},
				new LayoutStackVertical( 350, .3F ) {
					Spacing = 15,
					Children = {
					new LayoutControl( ViewCode, LayoutRect.CSS.Percent, 100 )
			}	}	}
		};
	*/

	class ViewLogAndNotes :
		Control,
		IPgParent,
		IPgLoad<XmlElement>,
		IPgSave<XmlDocumentFragment>,
		IPgCommandView
	{
		public static Guid ViewCategory {get;} = new Guid( "{F308F2D9-5F57-4351-A145-C0AD45773D07}" );

		protected class ViewMorseSlot :
			IPgBaseSite,
		    IPgViewSite
		{
			protected readonly ViewLogAndNotes _oHost;

			public ViewMorseSlot( ViewLogAndNotes oHost ) {
				_oHost = oHost ?? throw new ArgumentNullException();
			}

			public IPgParent Host => _oHost;

			public void LogError(string strMessage, string strDetails, bool fShow=true) {
				_oHost._oSiteView.LogError( strMessage, strDetails, fShow );
			}

			public virtual IPgViewNotify EventChain => _oHost._oSiteView.EventChain;

			public void Notify( ShellNotify eEvent ) {
			}
		} // End class

		readonly IPgViewSite _oSiteView;
		readonly DocNetHost  _oDocLog;
		readonly LayoutStack _rgLayout;

		EditWindow2 ViewNotes{ get; }
		ViewLog     ViewList { get; }

		public ViewLogAndNotes( IPgViewSite oSiteView, DocNetHost oDocument ) {
			_oSiteView = oSiteView ?? throw new ArgumentNullException();
			_oDocLog   = oDocument ?? throw new ArgumentNullException();

			ViewNotes = new EditWindow2( new ViewMorseSlot( this ), _oDocLog.Notes ) { Parent = this };
			ViewList  = new ViewLog    ( new ViewMorseSlot( this ), _oDocLog.Log   ) { Parent = this };

			_rgLayout = new LayoutStackVertical( ) {
								Spacing = 5,
								Children = {
									new LayoutControl( ViewNotes, LayoutRect.CSS.Percent, 30 ),
									new LayoutControl( ViewList,  LayoutRect.CSS.Percent, 70 )
								}
							};
			_rgLayout.Spacing = 5;
		}

		bool _fDisposed = false;

		protected override void Dispose( bool disposing ) {
			if( disposing && !_fDisposed ) {
				ViewList .Dispose();
				ViewNotes.Dispose();

				_fDisposed = true;
			}

			base.Dispose( disposing );
		}

		public IPgParent    Parentage    => _oSiteView.Host;
		public IPgParent    Services     => Parentage.Services;
		public bool         IsDirty      => _oDocLog.Log.IsDirty || _oDocLog.Notes.IsDirty;
		public Guid         Catagory     => ViewCategory;
		public string       Banner       => "Practice Morse Code";
		public SKBitmap     Icon         => null;

		protected override void OnSizeChanged(EventArgs e) {
			base.OnSizeChanged(e);

			_rgLayout.SetRect( LOCUS.UPPERLEFT, 0, 0, Width, Height );
			_rgLayout.LayoutChildren();

			Invalidate();
		}

		public object Decorate(IPgViewSite oBaseSite,Guid sGuid) {
			return null;
		}

		public bool Execute(Guid sGuid) {
			switch( sGuid ) {
				case var a when sGuid == GlobalCommands.Play:
				case var b when sGuid == GlobalCommands.Pause:
				case var c when sGuid == GlobalCommands.Stop:
					ViewNotes.Execute( sGuid );
					break;
			}
			return false;
		}

		public bool InitNew() {
			if( !ViewList.InitNew() )
				return false;
			if( !ViewNotes.InitNew() )
				return false;

			return true;
		}

		public bool Load(XmlElement oStream) {
			return InitNew();
		}

		public bool Save(XmlDocumentFragment oStream) {
			return true;
		}

	}
}
