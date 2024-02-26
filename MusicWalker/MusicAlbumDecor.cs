using System;
using System.Windows.Forms;
using System.Xml;

using Play.Rectangles;
using Play.Interfaces.Embedding;
using Play.Edit;
using Play.ImageViewer;

namespace Play.MusicWalker {
	/// <summary>
	/// A Decor for showing the album art and songs list.
	/// </summary>
	public class MusicAlbumDecor :
		Control,
		IPgParent,
		IPgLoad<XmlElement>
	{
		protected LayoutStackVertical _rgLayout = new LayoutStackVertical() { Spacing = 5 };

		readonly MusicWin      _oHost;
		readonly IPgViewSite   _oViewSite;
        readonly IPgViewNotify _oViewEvents;

		EditWindow2     ViewAlbumSongs { get; }
		ImageViewSingle ViewAlbumArt   { get; }

		public IPgParent Parentage => _oViewSite.Host; 
		public IPgParent Services  => _oHost.Services;

		protected class MusicAlbumDecorSlot :
			IPgViewSite
		{
			protected readonly MusicAlbumDecor _oHost;

			public MusicAlbumDecorSlot( MusicAlbumDecor oHost ) {
				_oHost = oHost ?? throw new ArgumentNullException();
			}

			public IPgParent     Host       => _oHost;
			public IPgViewNotify EventChain => _oHost._oViewSite.EventChain;

			public void LogError(string strMessage, string strDetails, bool fShow=true) {
				_oHost._oViewSite.LogError( strMessage, strDetails, fShow );
			}

			public void Notify( ShellNotify eEvent ) {
			}
		} // End class

		public MusicAlbumDecor( IPgViewSite oViewSite, MusicWin oMusicWin ) {
			_oHost       = oMusicWin ?? throw new ArgumentNullException( "Owning window must not be null." );
			_oViewSite   = oViewSite ?? throw new ArgumentNullException( "Site must not be null." );
			_oViewEvents = oViewSite.EventChain ?? throw new ArgumentException( "Site must support EventChain" );

			ViewAlbumSongs = new EditWindow2    ( new MusicAlbumDecorSlot(this), oMusicWin.AlbumCurrent, true, false ) { Wrap = false };
			ViewAlbumArt   = new ImageViewSingle( new MusicAlbumDecorSlot(this), oMusicWin.AlbumArtCurrent );

			ViewAlbumSongs.Parent = this;
			ViewAlbumArt  .Parent = this;

			_rgLayout.SetPoint( SET.STRETCH, LOCUS.UPPERLEFT, 0, 0 );
			_rgLayout.Add(new LayoutImageView( ViewAlbumArt  , LayoutRect.CSS.Percent ) { Track = 50 } );
			_rgLayout.Add(new LayoutControl  ( ViewAlbumSongs, LayoutRect.CSS.Percent ) { Track = 50 });
		}

		protected override void Dispose(bool disposing) {
			if( disposing ) {
				ViewAlbumArt.Document.ImageUpdated -= AlbumArt_ImageUpdated;
				ViewAlbumSongs.LineChanged         -= AlbumSongs_LineChanged;
				_oHost.Document.SongStopped        -= OnSongStopped;

				ViewAlbumSongs.Dispose();
				ViewAlbumArt  .Dispose();
			}
			base.Dispose(disposing);
		}

		public bool InitNew() {
			if( !ViewAlbumSongs.InitNew() )
				return false;

			if( !ViewAlbumArt.InitNew() )
				return false;

			ViewAlbumArt.Document.ImageUpdated += AlbumArt_ImageUpdated;
			ViewAlbumSongs.LineChanged         += AlbumSongs_LineChanged;

			_oHost.Document.SongStopped        += OnSongStopped;

			return true;
		}

		/// <summary>
		/// This fixes a very unusual bug. As we shuffle, this control and it's children
		/// are getting hidden. The Image is unaffected b/c it is not a (child) control! O.o
		/// So if you size Mjolnir, with our Parent view NOT selected OnSizeChanged() 
		/// gets a layout with only the Image having a non-zero size. Then when our
		/// Parent view is selected and our Outline is shuffled into view, the layout is 
		/// for the closed state! So we Relayout, when not hidden. 
		/// </summary>
        protected override void OnVisibleChanged(EventArgs e) {
            base.OnVisibleChanged(e);

			if( Visible ) {
				_rgLayout.SetPoint( SET.STRETCH, LOCUS.LOWERRIGHT, Width, Height < 0 ? 0 : Height );
				_rgLayout.LayoutChildren();
			}
        }

        private void OnSongStopped( int iAlbum, int iSong ) {
			try {
				SongCredentials oCurrentSong = _oHost.Document.SongCurrent;

				if( iAlbum == _oHost.CurrentAlbumIndex ) {
					ViewAlbumSongs.SelectionSet( iSong, 0, 0 );
				}
			} catch( NullReferenceException ) {
			}
		}

		private void AlbumSongs_LineChanged(int iLine) {
			_oHost.OnOutlineCaratMoved( iLine );
		}

		protected override void OnSizeChanged(EventArgs e) {
			base.OnSizeChanged(e);

			_rgLayout.SetPoint( SET.STRETCH, LOCUS.LOWERRIGHT, Width, Height < 0 ? 0 : Height );
			_rgLayout.LayoutChildren();
		}

		private void AlbumArt_ImageUpdated() {
			_oHost.OnOutlineCaratMoved( 0 ); // BUG: Crude but will work for now.
			_rgLayout.LayoutChildren();
			Invalidate();
		}

		public bool Load(XmlElement xmlRoot ) {
			throw new NotImplementedException();
		}

        protected override void OnGotFocus(EventArgs e) {
            base.OnGotFocus( e );

            _oViewEvents.NotifyFocused( true );

            this.Invalidate();
        }

		protected override void OnLostFocus(EventArgs e) {
			base.OnLostFocus(e);
			_oViewEvents.NotifyFocused( false );
			this.Invalidate();
		}
	}
}
