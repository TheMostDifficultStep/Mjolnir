using System;
using System.Xml;
using System.Windows.Forms;

using SkiaSharp;

using Play.Rectangles;
using Play.Interfaces.Embedding;
using Play.Edit;

namespace Play.MorsePractice {
	class ViewMorse :
		Control,
		IPgParent,
		IPgTextView,
		IPgLoad<XmlElement>,
		IPgSave<XmlDocumentFragment>,
		IPgCommandView
	{
		public static readonly Guid _guidViewCategory = new Guid( "{F308F2D9-5F57-4351-A145-C0AD45773D07}" );

		protected class ViewMorseSlot :
			IPgBaseSite,
		    IPgViewSite
		{
			protected readonly ViewMorse _oHost;

			public ViewMorseSlot( ViewMorse oHost ) {
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

		protected class ViewMorseNotesSlot :
			ViewMorseSlot,
			IPgViewNotify
		{
			protected readonly IPgViewNotify _oHostEvent;

			public ViewMorseNotesSlot( ViewMorse oHost ) : base( oHost ) {
				_oHostEvent = oHost._oSiteView.EventChain;
			}

			public override IPgViewNotify EventChain => this;

			public void NotifyFocused(bool fSelect) {
				_oHostEvent.NotifyFocused( fSelect );
			}

			public bool IsCommandKey(CommandKey ckCommand,KeyBoardEnum kbModifier) {
				return _oHostEvent.IsCommandKey( ckCommand, kbModifier );
			}

			public bool IsCommandPress(char cChar) {
				if( cChar == '\r' )
					_oHost.Execute( GlobalCommands.Play );
				
				return _oHostEvent.IsCommandPress( cChar );
			}
		}

		readonly IPgViewSite _oSiteView;
		readonly MorseDoc    _oDocMorse;
		readonly LayoutStack _rgLayout;

		EditWindow2 ViewSource { get; }
		EditWindow2 ViewNotes  { get; }
		EditWindow2 ViewCode   { get; }

		public ViewMorse( IPgViewSite oSiteView, MorseDoc oDocument ) {
			_oSiteView = oSiteView ?? throw new ArgumentNullException();
			_oDocMorse = oDocument ?? throw new ArgumentNullException();

			ViewSource = new EditWindow2( new ViewMorseSlot     ( this ), _oDocMorse.Source ) { Parent = this };
			ViewNotes  = new EditWindow2( new ViewMorseNotesSlot( this ), _oDocMorse.Notes  ) { Parent = this };
			ViewCode   = new EditWindow2( new ViewMorseSlot     ( this ), _oDocMorse.Morse, fReadOnly:true ) { Parent = this };

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
			_rgLayout.Spacing = 5;
		}

		bool _fDisposed = false;

		protected override void Dispose( bool disposing ) {
			if( disposing && !_fDisposed ) {
				ViewSource.Dispose();
				ViewNotes .Dispose();
				ViewCode .Dispose();

				_fDisposed = true;
			}

			base.Dispose( disposing );
		}

		public IPgParent    Parentage    => _oSiteView.Host;
		public IPgParent    Services     => Parentage.Services;
		public object       DocumentText => _oDocMorse.Source;
		public bool         IsDirty      => _oDocMorse.Source.IsDirty || _oDocMorse.Notes.IsDirty;
		public Guid         Catagory     => _guidViewCategory;
		public string       Banner       => "Practice Morse Code";
		public SKBitmap     Icon         => null;

		public TextPosition Caret        => throw new NotImplementedException();

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
					ViewSource.Execute( sGuid );
					break;
			}
			return false;
		}

		public bool InitNew() {
			if( !ViewSource.InitNew() )
				return false;
			if( !ViewNotes.InitNew() )
				return false;
			if( !ViewCode.InitNew() )
				return false;

			return true;
		}

		public bool Load(XmlElement oStream) {
			return InitNew();
		}

		public bool Save(XmlDocumentFragment oStream) {
			return true;
		}

		public void ScrollTo(EDGE eEdge) {
		}

		public void ScrollToCaret() {
		}

		public void SelectionClear() {
		}

		public bool SelectionSet(int iLine,int iOffset,int iLength) {
			return false;
		}
	}
}
