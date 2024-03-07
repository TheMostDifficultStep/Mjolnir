using System;
using System.Collections.Generic;

using Play.Interfaces.Embedding; 

namespace Play.ImageViewer {
    /// <summary>
    /// Our image walker is a bit of an odd bird. It can both read a directory and load/save from
    /// a collection of image filenames. So it comes down to the shell's intention when it creates
    /// a new document. Here when we create a doc for a .scraps file, we assume a non directory 
    /// browsing mode. But if we create a doc on any of the image types, we assume a directory 
    /// browsing mode. Persisting in dir mode would still mean saving to a file of some sort. We currently
    /// don't have a file exten defined for that. Maybe ".browse" or something. Not there yet.
    /// 
    /// But there's still the matter of starting a browse session and assigning the directory. 
    /// from scratch. Note: we still can persist the dir via a TextWriter! I'm thinking I'll
    /// need two controllers for the two types. Effectively treating eash situation as a different
    /// type.
    /// 
    /// Right now the IsBrowsable helps the shell disambuguate the situation. 
    /// 
    /// We use ImageBrowserSite for the image types (non-persistable atm), and a normal TextSite for .scraps.
    /// TODO: Move to the ImageWalk project
    /// </summary>
    public class ImageBrowserBase : 
        Controller 
    {
        public readonly static Guid _guidViewImage = Guid.Empty;

        public ImageBrowserBase() {
        }

        public override IDisposable CreateDocument( IPgBaseSite oSite, string strExtension ) {
            if( string.Compare( strExtension, ".scraps", true ) == 0 )
                return( new ImageWalkerDoc( oSite ) );

            return( new ImageWalkerDir( oSite ) );
        }

        public override IDisposable CreateView( IPgViewSite oBaseSite, object oDocument, Guid guidViewType ) {
            ImageWalkerDoc oDocImageBrowser = oDocument as ImageWalkerDoc ?? throw new ArgumentException( "Argument must be an ImageWalkerDoc" );

			try {
				if( guidViewType == _guidViewImage )
					return( new WindowSoloImageNav( oBaseSite, oDocImageBrowser ) );
				if( guidViewType == ViewImageIconsMain.Guid )
					return( new ViewImageIconsMain( oBaseSite, oDocImageBrowser ) );
				if( guidViewType == ViewImageText.Guid )
					return( new ViewImageText     ( oBaseSite, oDocImageBrowser ) );
				if( guidViewType == ViewSnipDialog.Guid )
					return( new ViewSnipDialog    ( oBaseSite, oDocImageBrowser ) );

				return( new WindowSoloImageNav( oBaseSite, oDocImageBrowser ) );
            } catch( Exception oEx ) {
                Type[] rgErrors = { typeof( NullReferenceException ),
                                    typeof( InvalidCastException ),
                                    typeof( ArgumentNullException ),
									typeof( ArgumentException ) };
                if( rgErrors.IsUnhandled( oEx ) )
                    throw;

				throw new InvalidOperationException( "Controller couldn't create view for Image document.", oEx );
            }
        }

        public override IEnumerator<IPgViewType> GetEnumerator() {
            // TODO: Move the guid to the view's class at least.
 	        yield return new ViewType( "Image", _guidViewImage );
 	        yield return new ViewType( "Icons", ViewImageIconsMain.Guid );
 	        yield return new ViewType( "Text",  ViewImageText.Guid  );
			yield return new ViewType( "Snip",  ViewSnipDialog.Guid );
        }
    }

    public class ImageBrowserScrapsController : 
        ImageBrowserBase 
    {
        public ImageBrowserScrapsController( ) : base() {
            _rgExtensions.Add( ".scraps" );
        }
    }

    public class ImageBrowserDirController : 
        ImageBrowserBase 
    {
        public ImageBrowserDirController( ) : base() {
            _rgExtensions.Add( string.Empty );

            foreach( string strExtn in ImageWalkerDoc._rgFileExts )
                _rgExtensions.Add( strExtn );
        }

        /// <summary>
        /// Image viewers are less suitable than direct file editor for bitmaps
        /// so we'll return a lower priority. However they are high priority for
        /// directories (NULL OR EMPTY extensions!)
        /// </summary>
        /// <param name="strExtension"></param>
        /// <returns></returns>
        public override PgDocDescr Suitability(string strExtension) {
            if( string.IsNullOrEmpty( strExtension ) ) {
                return new PgDocDescr( strExtension, 
                                       typeof( IPgLoadURL ), 
                                       255, 
                                       this );
            }
            foreach( string strExtn in ImageWalkerDoc._rgFileExts ) {
                if( string.Compare( strExtn, strExtension, ignoreCase:true ) == 0 ) {
                    return new PgDocDescr( strExtension, 
                                           typeof( IPgLoadURL ), 
                                           200, 
                                           this );
                }
            }
            return new PgDocDescr( strExtension, 
                                   typeof( IPgLoadURL ), 
                                   0, 
                                   this );
        }
    }
}
