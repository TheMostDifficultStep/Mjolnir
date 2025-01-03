﻿using System;

using Play.Interfaces.Embedding;
using Play.Edit;
using Play.Forms;

namespace Play.ImageViewer {
    /// <summary>
    /// This is our text viewer for the ImageWalkerDoc object. The EditWin object is overriden so that the
    /// FileList sub document can be accessed from within the ImageWalkerDoc.
    /// </summary>
    public class ViewImageText : EditWindow2 {
        protected readonly ImageWalkerDoc _oDocumentWalker; 
		public static readonly Guid _gViewType = new Guid( "{AD026867-FE9C-4F5F-9A50-3228B3716F9F}" );

        public ViewImageText( IPgViewSite oBaseSite, ImageWalkerDoc oDoc ) : base( oBaseSite, oDoc.FileList ) {
            _oDocumentWalker = oDoc ?? throw new ArgumentNullException( "View needs pointer to document" );
        }

        public override Guid Catagory {
            get {
                return Guid;
            }
        }

		public static Guid Guid => _gViewType;

        public override object Decorate( IPgViewSite oBaseSite, Guid sGuid ) {
            if( sGuid == GlobalDecor.Outline ) {
                return( new ImageViewIcons( oBaseSite, _oDocumentWalker ) );
            }
            if( sGuid.Equals( GlobalDecor.Properties ) ) {
                return( new WindowStandardProperties( oBaseSite, _oDocumentWalker.Properties ) );
            }

            return( null );
        }
    }
}
