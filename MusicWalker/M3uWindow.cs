﻿using System;
using System.Drawing;
using System.Reflection;

using Play.Interfaces.Embedding;
using Play.Edit;
using Play.ImageViewer;
using SkiaSharp.Views.Desktop;

namespace Play.MusicWalker {
	/// <summary>
	/// This is an editor window to support our editor with album art in the outline. I'm
	/// a little premature replacing the edit window with my new FreeType window. It's not
	/// fully debugged in read/write mode.
	/// </summary>
	public class WinMusicText : EditWindow2 {
		readonly M3UDocument _oDocumentMusic;
		readonly string      _strMusicIcon = @"MusicWalker.Content.icons8-music-24.png";

		Bitmap _oAlbumArtIcon = null;

        public WinMusicText( IPgViewSite oBaseSite, M3UDocument p_oDocument, bool fReadOnly = false, bool fSingleLine = false ) : 
			base( oBaseSite, p_oDocument, fReadOnly, fSingleLine ) 
		{
			_oDocumentMusic = p_oDocument ?? throw new ArgumentNullException( "Music Edit Win needs Music Document." );
			_oAlbumArtIcon  = ImageResourceHelper.GetImageResource( Assembly.GetExecutingAssembly(), _strMusicIcon );
		}

		protected override bool InitInternal() {
			if( !base.InitInternal() )
				return( false );

			try {
				if( _oDocumentMusic.AlbumArt.Bitmap != null ) {
					// BUG: Temporary holdover until we convert 'em all.
					using( Bitmap oBmpCover = _oDocumentMusic.AlbumArt.Bitmap.ToBitmap() ) {
						Bitmap oNewCover = new Bitmap( oBmpCover, new Size( 16, 16 ) );

						_oAlbumArtIcon.Dispose();
						_oAlbumArtIcon = oNewCover;
					}
				}
			} catch( Exception oEx ) {
				Type[] rgErrors = { typeof( NullReferenceException ),
								    typeof( ArgumentException ),
									typeof( ArgumentNullException ),
									typeof( Exception ) };
				if( rgErrors.IsUnhandled( oEx ) )
					throw;

				_oSiteView.LogError( "embedding", "problem initializing album art icon." );
			}

			return( true );
		}

		public override object Decorate( IPgViewSite oBaseSite, Guid sGuid ) {
            if( sGuid.Equals( GlobalDecorations.Outline ) ) {
                return( new ImageViewSolo( oBaseSite, _oDocumentMusic.AlbumArt ) );
            }
            if( sGuid.Equals( GlobalDecorations.Properties ) ) {
                return( new PropWin( oBaseSite, _oDocumentMusic.AlbumProperties ) );
            }

            return( base.Decorate( oBaseSite, sGuid ) );
        }

		public override Image Iconic => _oAlbumArtIcon;

		public override bool Execute(Guid sGuid) {
			if (sGuid == GlobalCommands.Play) {
				_oDocumentMusic.Play(CaretPos.Line);
				return (true);
			}
			if (sGuid == GlobalCommands.Pause) {
				_oDocumentMusic.PlayPause();
				return (true);
			}
			if (sGuid == GlobalCommands.Stop) {
				_oDocumentMusic.PlayStop();
				return (true);
			}
			return base.Execute(sGuid);
		}

		protected override void Dispose(bool disposing) {
			if( disposing ) {
				if( _oAlbumArtIcon != null )
					_oAlbumArtIcon.Dispose();
			}
			base.Dispose(disposing);
		}
	}

	public class WinMusicAlbum : ImageViewSolo {
		readonly M3UDocument _oDocumentMusic;

		readonly string _strIconMusic = @"MusicWalker.Content.icon_album.gif";
		readonly Bitmap _oIconMusic   = null;

		string _strBanner  = string.Empty;
		int    _iCaratLine = 0;

		public override string Banner => _strBanner;
		public override Image  Iconic => _oIconMusic;

		public WinMusicAlbum( IPgViewSite oBaseSite, M3UDocument oDocument ) : base( oBaseSite, oDocument.AlbumArt ) {
			_oDocumentMusic = oDocument ?? throw new ArgumentNullException( "Music Image Win needs Music Document.");

			_oIconMusic = ImageResourceHelper.GetImageResource( Assembly.GetExecutingAssembly(), _strIconMusic ) ?? 
				throw new ApplicationException("Could not find music image resource.");

			_oDocumentMusic.HilightEvent += OnHighLightChanged;
			_strBanner = _oDocumentMusic[_iCaratLine].ToString();
		}

		protected override void Dispose( bool fDisposing ) {
			if( fDisposing ) {
				if( _oIconMusic != null ) {
					_oIconMusic.Dispose();
				}
			}

			base.Dispose( fDisposing );
		}
		protected virtual void OnHighLightChanged() {
			Line oSong = _oDocumentMusic.HighLight;

			if( oSong != null )
				_strBanner = oSong.ToString();
			else
				_strBanner = base.Banner;

			_oViewSite.Notify( ShellNotify.BannerChanged );
		}

		/// <summary>
		/// Track carat line changes.
		/// </summary>
		private void OutlineLineChanged(int iLine) {
			_iCaratLine = iLine;
		}

		public override object Decorate( IPgViewSite oBaseSite, Guid sGuid ) {
            if( sGuid.Equals( GlobalDecorations.Outline ) ) {
				EditWindow2 oWin = new WinMusicText( oBaseSite, _oDocumentMusic ); // use this so we track active song at load.
				oWin.LineChanged += OutlineLineChanged;
				oWin.Wrap        = false;
                return( oWin );
            }
			if( sGuid.Equals( GlobalDecorations.Properties ) ) {
                return( new PropWin( oBaseSite, _oDocumentMusic.AlbumProperties ) );
			}

            return( base.Decorate( oBaseSite, sGuid ) );
        }

		public override bool Execute(Guid sGuid) {
			if( sGuid == GlobalCommands.Play ) {
				_oDocumentMusic.Play( _oDocumentMusic[_iCaratLine] );
				return( true );
			}
			if( sGuid == GlobalCommands.Pause ) {
				_oDocumentMusic.PlayPause();
				return( true );
			}
			if( sGuid == GlobalCommands.Stop ) {
				_oDocumentMusic.PlayStop();
				return( true );
			}
			if( sGuid == GlobalCommands.JumpParent ) {
				return( false );
			}

			return( base.Execute( sGuid ) );
		}
	}
}
