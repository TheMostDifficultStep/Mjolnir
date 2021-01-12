using System;
using System.Collections.Generic;

using Play.Interfaces.Embedding;

namespace Play.MusicWalker {
	public class MusicWalkerController : Controller {
        public readonly static Guid _guidViewPlayList = new Guid( "{E61DA3B2-1CD0-4B23-96E3-D4FEBDEEE5F2}" );

		public MusicWalkerController() {
			_rgExtensions.Add( ".music" );
		}

		public override IDisposable CreateDocument(IPgBaseSite oSite, string strExtension) {
			return( new MusicCollection( oSite ) );
		}

		public override IDisposable CreateView(IPgViewSite oBaseSite, object oDocument, Guid guidViewType) {
			try {
				MusicCollection oMusicDoc = (MusicCollection)oDocument;

				if( guidViewType == _guidViewPlayList )
					return( new PlayListWindow( oBaseSite, oMusicDoc ) ); // Subclass Execute & Decor functions.
				if( guidViewType == VisualizeWindow._gViewType )
					return( new VisualizeWindow( oBaseSite, oMusicDoc ) );

				return( new MusicWin( oBaseSite, oMusicDoc ) );
            } catch( Exception oEx ) {
				// TODO: Stuff errors collection into the base controller.
                Type[] rgErrors = { typeof( NullReferenceException ),
                                    typeof( InvalidCastException ),
                                    typeof( ArgumentNullException ),
									typeof( ArgumentException ) };
                if( rgErrors.IsUnhandled( oEx ) )
                    throw;

				throw new InvalidOperationException( "Controller couldn't create view for Music Collection document.", oEx );
            }
		}

		public override IEnumerator<IPgViewType> GetEnumerator() {
 	        yield return( new ViewType( "Albums", Guid.Empty ) );
 	        yield return( new ViewType( "Play List", _guidViewPlayList ) );
		  //yield return( new ViewType( "Spectrum", VisualizeWindow._gViewType ) );
		}
	}

    public class M3uController : Controller {
        public readonly static Guid _guidViewImage = Guid.Empty;
        public readonly static Guid _guidViewText  = new Guid( "{AD026867-FE9C-4F5F-9A50-3228B3716F9F}" );

		public M3uController() : base() {
			_rgExtensions.Add( ".m3u" );
		}

		public override IDisposable CreateDocument(IPgBaseSite oSite, string strExtn ) {
			return new M3UDocument( oSite );
		}

		public override IDisposable CreateView( IPgViewSite oBaseSite, object oDocument, Guid guidViewType ) {
            M3UDocument oDocM3u = oDocument as M3UDocument;

            if( oDocM3u == null )
                throw new ArgumentException( "Argument must be an EditorForM3u" );

			try {
				if( guidViewType == _guidViewImage )
					return( new WinMusicAlbum( oBaseSite, oDocM3u ) );
				if( guidViewType == _guidViewText )
					return( new WinMusicText( oBaseSite, oDocM3u ) );

				return( new WinMusicText( oBaseSite, oDocM3u ) );
            } catch( Exception oEx ) {
                Type[] rgErrors = { typeof( NullReferenceException ),
                                    typeof( InvalidCastException ),
                                    typeof( ArgumentNullException ),
									typeof( ArgumentException ),
									typeof( ApplicationException) };
                if( rgErrors.IsUnhandled( oEx ) )
                    throw;

				throw new InvalidOperationException( "Controller couldn't create new view for M3u document.", oEx );
            }
        }

        public override IEnumerator<IPgViewType> GetEnumerator() {
 	        yield return( new ViewType( "Album Art", _guidViewImage ) );
 	        yield return( new ViewType( "Playlist",  _guidViewText  ) );
        }
	}
}
