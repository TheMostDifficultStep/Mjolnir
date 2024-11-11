using System.Windows.Forms;

using Play.Edit;
using Play.Interfaces.Embedding;
using Play.Rectangles;
using Play.ImageViewer;

namespace Play.FileManager {
    internal class ViewFavNames : WindowMultiColumn {

        public ViewFavNames( IPgViewSite oSite, FileFavorites oDocFavorites ) : 
            base( oSite, oDocFavorites ) 
        {
        }

        public override bool InitNew() {
            if( !base.InitNew() ) 
                return false;

            TextLayoutAdd( new LayoutRect( LayoutRect.CSS.Flex, 20, 1L ), (int)FileFavorites.DRow.Col.Type ); 
            TextLayoutAdd( new LayoutRect( LayoutRect.CSS.None, 10, 1L ), (int)FileFavorites.DRow.Col.ShortcutName ); 

            // Do this so we can return a desired height. O.o;;
            _oCacheMan.CacheRepair( null, true, true );

            return true;
        }
    }

	/// <summary>
	/// A Decor for showing the favorites. I used to have an icon
	/// at the top. But I'm not liking it so i've removed it
	/// But I'll leave this in case I add some icons for add/remove
	/// favorites.
	/// </summary>
	public class ViewFManOutline :
		Control,
		IPgParent,
		IPgLoad
	{
		protected LayoutStackVertical _rgLayout = new LayoutStackVertical() { Spacing = 5 };

		readonly ViewFileMan   _oOwner;
		readonly FileManager   _oDocFM;
		readonly IPgViewSite   _oViewSite;

	  //ImageViewSingle ViewArt   { get; }
		ViewFavNames    ViewFaves { get; }

		public IPgParent Parentage => _oViewSite.Host; 
		public IPgParent Services  => _oOwner.Services;

		protected class ViewSlot :
			IPgViewSite
		{
			protected readonly ViewFManOutline _oHost;

			public ViewSlot( ViewFManOutline oHost ) {
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

		public ViewFManOutline( IPgViewSite oViewSite ) {
			_oViewSite = oViewSite ?? throw new ArgumentNullException( "Site must not be null." );

			_oOwner   = (ViewFileMan)oViewSite.Host;
			_oDocFM   = _oOwner.Document;

			ViewFaves = new ( new ViewSlot(this), _oDocFM.DocFavs );
		  //ViewArt   = new ( new DecorSlot(this), _oDocFM.ImgFavs );

			ViewFaves.Parent = this;
	      //ViewArt  .Parent = this;

			_rgLayout.SetPoint( SET.STRETCH, LOCUS.UPPERLEFT, 0, 0 );
		  //_rgLayout.Add(new LayoutImageView( ViewArt  , LayoutRect.CSS.Pixels ) { Track = 100 });
			_rgLayout.Add(new LayoutControl  ( ViewFaves, LayoutRect.CSS.None   ) { Track =  70 });
		}

		protected override void Dispose(bool disposing) {
			if( disposing ) {
				ViewFaves.Dispose();
		      //ViewArt  .Dispose();
			}
			base.Dispose(disposing);
		}

		public bool InitNew() {
			if( !ViewFaves.InitNew() )
				return false;

			//if( !ViewArt.InitNew() )
			//	return false;

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

		protected override void OnSizeChanged(EventArgs e) {
			base.OnSizeChanged(e);

			_rgLayout.SetPoint( SET.STRETCH, LOCUS.LOWERRIGHT, Width, Height < 0 ? 0 : Height );
			_rgLayout.LayoutChildren();
		}
	}
}
