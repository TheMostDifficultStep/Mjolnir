using System;
using System.Drawing;
using System.Reflection;
using System.Xml;

using Play.Drawing;
using Play.Interfaces.Embedding;
using Play.Edit;
using Play.ImageViewer;
using Play.Forms;

using SkiaSharp;
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

		SKBitmap _oAlbumArtIcon = null;

        public WinMusicText( IPgViewSite oBaseSite, M3UDocument p_oDocument, bool fReadOnly = false, bool fSingleLine = false ) : 
			base( oBaseSite, p_oDocument, fReadOnly, fSingleLine ) 
		{
			_oDocumentMusic = p_oDocument ?? throw new ArgumentNullException( "Music Edit Win needs Music Document." );
			_oAlbumArtIcon  = SKImageResourceHelper.GetImageResource( Assembly.GetExecutingAssembly(), _strMusicIcon );
		}

		protected override bool InitInternal() {
			if( !base.InitInternal() )
				return( false );

			//try {
			//	if( _oDocumentMusic.AlbumArt.Bitmap != null ) {
			//		// BUG: Temporary holdover until we convert 'em all.
			//		SKBitmap oNewCover = new SKBitmap( 16, 16, true );

			//		_oAlbumArtIcon.Dispose();
			//		_oAlbumArtIcon = oNewCover;
			//	}
			//} catch( Exception oEx ) {
			//	Type[] rgErrors = { typeof( NullReferenceException ),
			//					    typeof( ArgumentException ),
			//						typeof( ArgumentNullException ),
			//						typeof( Exception ) };
			//	if( rgErrors.IsUnhandled( oEx ) )
			//		throw;

			//	_oSiteView.LogError( "embedding", "problem initializing album art icon." );
			//}

			return( true );
		}

		public override object Decorate( IPgViewSite oBaseSite, Guid sGuid ) {
            if( sGuid.Equals( GlobalDecorations.Outline ) ) {
                return( new WindowSoloImageNav( oBaseSite, _oDocumentMusic.AlbumArt ) );
            }
            if( sGuid.Equals( GlobalDecorations.Properties ) ) {
                return( new WindowStandardProperties( oBaseSite, _oDocumentMusic.AlbumProperties ) );
            }

            return( base.Decorate( oBaseSite, sGuid ) );
        }

		public override SKBitmap Icon => _oAlbumArtIcon;

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

	public class WinMusicAlbum : WindowSoloImageNav {
		readonly M3UDocument _oDocumentMusic;

		protected override string IconResource => @"MusicWalker.Content.icon_album.gif";

		string _strBanner  = string.Empty;
		int    _iCaratLine = 0;

		public override string Banner => _strBanner;

		public WinMusicAlbum( IPgViewSite oBaseSite, M3UDocument oDocument ) : base( oBaseSite, oDocument.AlbumArt ) {
			_oDocumentMusic = oDocument ?? throw new ArgumentNullException( "Music Image Win needs Music Document.");

			_oDocumentMusic.HilightEvent += OnHighLightChanged;
			_strBanner = _oDocumentMusic[_iCaratLine].ToString();
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
                return( new WindowStandardProperties( oBaseSite, _oDocumentMusic.AlbumProperties ) );
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

	/// <summary>
	/// Standard window for playing a solo mp3 file. Maybe I'll add some property pane
	/// like stuff that can be used by the m3u player too.
	/// </summary>
	public class WinSoloMP3 :
		SKControl, 
		IPgLoad<XmlElement>,
		IPgSave<XmlDocumentFragment>,
        IPgCommandView
	{
        public readonly static Guid _guidViewImage = Guid.Empty;
		readonly string _strIconMusic = @"MusicWalker.Content.icon_album.gif";

		readonly MP3Document _oDocument;

        public bool IsDirty => false;

        public Guid   Catagory => _guidViewImage;
        public string Banner   => "Solo MP3 Player" + ( string.IsNullOrEmpty( _oDocument.CurrentURL ) ? string.Empty : " : " + _oDocument.CurrentURL );
        public SKBitmap  Icon   { get; }

		public WinSoloMP3( IPgViewSite oBaseSite, MP3Document oDocument ) {
			_oDocument = oDocument ?? throw new ArgumentNullException( "Music Image Win needs Music Document.");

			Icon = SKImageResourceHelper.GetImageResource( Assembly.GetExecutingAssembly(), _strIconMusic ) ?? 
				throw new ApplicationException("Could not find music image resource.");
		}

        public bool InitNew() {
            return true;
        }

        public bool Load( XmlElement oStream ) {
            return true;
        }

        public bool Save( XmlDocumentFragment oStream ) {
            return true;
        }

        public object Decorate( IPgViewSite oBaseSite, Guid sGuid )  {
            return null;
        }

        public bool Execute( Guid sGuid ) {
            if( sGuid == GlobalCommands.Play ) {
				_oDocument.Play();
				return true;
			}
			if( sGuid == GlobalCommands.Stop ) {
				_oDocument.Stop();
				return true;
			}
			if( sGuid == GlobalCommands.Pause ) {
				_oDocument.Pause();
				return true;
			}

			return false;
        }
    }
}
