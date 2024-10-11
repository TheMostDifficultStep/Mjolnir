using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using SkiaSharp;

using Play.Edit;
using Play.Interfaces.Embedding;
using Play.Rectangles;
using Play.Parse;

namespace Play.FileManager {
    public class ViewFileMan :
        WindowMultiColumn,
        IPgCommandView
    {
        public string    Banner => "File Manager : " + _oDocument.CurrentURL;
		public SKBitmap  Icon { get; protected set; }
        public                  Guid Catagory => _sGuid;
        public           static Guid GUID     => _sGuid;
        private readonly static Guid _sGuid   = new( "{D257D1AA-AC3E-4A0F-83A3-97C95AE12782}" );

        protected readonly FileManager _oDocument;

        public ViewFileMan(IPgViewSite oViewSite, object oDocument) : base(oViewSite, oDocument) {
            _oDocument = (FileManager)oDocument;
			Icon	   = _oDocument.GetResource( "icons8-script-96.png" );
        }

        /// <remarks>
        /// I want to push layout to the init phase so we could potentially
        /// load our layout from a file! ^_^
        /// </remarks>
        protected override bool Initialize() {
            if( !base.Initialize() )
                return false;

            TextLayoutAdd( new LayoutRect( LayoutRect.CSS.None,    80, 1L ), (int)FileManager.FMRow.Col.Name ); 
            TextLayoutAdd( new LayoutRect( LayoutRect.CSS.Percent, 10, 1L ), (int)FileManager.FMRow.Col.Type ); 
            TextLayoutAdd( new LayoutRect( LayoutRect.CSS.Percent, 10, 1L ), (int)FileManager.FMRow.Col.Size ); 
            TextLayoutAdd( new LayoutRect( LayoutRect.CSS.Percent, 10, 1L ), (int)FileManager.FMRow.Col.Date );

            HyperLinks.Add( "DirJump", OnCpuJump );

            return true;
        }

        static readonly Type[] _rgErrors = { typeof( NullReferenceException ),
                                             typeof( ArgumentOutOfRangeException ),
                                             typeof( IndexOutOfRangeException ),
                                             typeof( InvalidOperationException ) };

        protected void OnCpuJump( Row oRow, int iColumn, IPgWordRange oRange ) {
            try {
                Line oText = oRow[iColumn];

                string strPath = Path.Combine( _oDocument.CurrentURL, oText.ToString() );

                _oDocument.ReadDir( strPath );
            } catch( Exception oEx ) {
                if( _rgErrors.IsUnhandled(oEx) )
                    throw;
            }
        }
        public object Decorate(IPgViewSite oBaseSite, Guid sGuid) {
            return null;
        }

        public override bool Execute(Guid gCommand) {
            return false;// none of the base operations are applicable.
        }
    }
}
