using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

using Play.Edit;
using Play.Interfaces.Embedding;
using Play.Rectangles;
using Play.ImageViewer;

namespace Play.FileManager {
	/// <summary>
	/// A Decor for showing the album art and songs list.
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

		ImageViewSingle ViewArt   { get; }
		EditWindow2     ViewFaves { get; }

		public IPgParent Parentage => _oViewSite.Host; 
		public IPgParent Services  => _oOwner.Services;

		protected class DecorSlot :
			IPgViewSite
		{
			protected readonly ViewFManOutline _oHost;

			public DecorSlot( ViewFManOutline oHost ) {
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

			ViewFaves = new EditWindow2    ( new DecorSlot(this), _oDocFM.DocFavs, true, false ) { Wrap = false };
			ViewArt   = new ImageViewSingle( new DecorSlot(this), _oDocFM.ImgFavs );

			ViewFaves.Parent = this;
			ViewArt  .Parent = this;

			_rgLayout.SetPoint( SET.STRETCH, LOCUS.UPPERLEFT, 0, 0 );
			_rgLayout.Add(new LayoutImageView( ViewArt  , LayoutRect.CSS.Percent ) { Track = 30 } );
			_rgLayout.Add(new LayoutControl  ( ViewFaves, LayoutRect.CSS.Percent ) { Track = 70 });
		}

		protected override void Dispose(bool disposing) {
			if( disposing ) {
				ViewFaves.Dispose();
				ViewArt  .Dispose();
			}
			base.Dispose(disposing);
		}

		public bool InitNew() {
			if( !ViewFaves.InitNew() )
				return false;

			if( !ViewArt.InitNew() )
				return false;

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
