﻿using System;
using System.Collections.Generic;

using Play.Interfaces.Embedding;

namespace Play.MusicWalker {
    public class ControllerFactory : 
        IControllerFactory 
    {
        public static Guid AlbumWalk = new Guid( "{D044478C-3351-4B62-B499-083EE9BE9482}" );
        public static Guid M3UWalk   = new Guid( "{2DE2D314-D302-48B3-931F-F0D6D815AC8F}" );
        public static Guid MP3Play   = new Guid( "{0187617E-ED67-424B-8B05-3A4D8E2F2FC4}" );
        public ControllerFactory() {
        }

        public IPgController2 GetController( Guid sID ) {
            if( sID == AlbumWalk ) {
                return new MusicWalkerController();
            }
            if( sID == M3UWalk ) {
                return new M3uController();
            }
            if( sID == MP3Play ) {
                return new MP3Controller();
            }

            throw new ArgumentOutOfRangeException();
        }
    }
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

	public class MP3Controller : Controller {
		public MP3Controller() : base() {
			_rgExtensions.Add( ".mp3" );
		}

        public override PgDocDescr Suitability(string strExtension) {
            byte bPriority = (byte)0;

			foreach( string strUs in _rgExtensions ) {
				if( string.Compare( strUs, strExtension, ignoreCase:true ) == 0 )
					bPriority = 255;
			}

            return new PgDocDescr( strExtension, 
                                             typeof( IPgLoadURL ), 
                                             bPriority, 
                                             this );
        }

		public override IDisposable CreateDocument(IPgBaseSite oSite, string strExtn ) {
			return new MP3Document( oSite );
		}

		public override IDisposable CreateView( IPgViewSite oBaseSite, object oDocument, Guid guidViewType ) {
			if( oDocument is not MP3Document oDocM3u )
				throw new ArgumentException("Argument must be an MP3Document");

			try {
				if( guidViewType == WinSoloMP3._guidViewImage )
					return( new WinSoloMP3( oBaseSite, oDocM3u ) );

				return( new WinSoloMP3( oBaseSite, oDocM3u ) );
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
 			yield return( new ViewType( "Player", WinSoloMP3._guidViewImage ) );
		}
	}

}
