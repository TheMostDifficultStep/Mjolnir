using System;
using System.Collections.Generic;
using System.Collections;
using System.Windows.Forms;
using System.Xml;

using SkiaSharp;

using Play.Interfaces.Embedding;
using Play.Rectangles;
using Play.Edit;
using Play.Forms;
using Play.Parse;

namespace Play.MorsePractice {
	/// <summary>
	/// Reviving this experiment, multi column log viewer!!
	/// </summary>
	public class ViewNetLog : 
		WindowMultiColumn,
		IPgCommandView,
        IPgLoad<XmlElement>,
		IPgSave<XmlDocumentFragment>
	{
		static public Guid ViewCatagory { get; } = new Guid("{BE243DE2-7763-4A44-9499-0EEDBC84D8A4}");

		public Guid   Catagory => ViewCatagory;
		public string Banner   => "Log Viewer";
		public SKBitmap Icon   => null;

		protected DocMultiColumn LogDoc { get; }

		public ViewNetLog( IPgViewSite oSiteView, object oDocument ) :
			base( oSiteView, oDocument )
		{
			LogDoc = (DocMultiColumn)oDocument;
		}

		protected override bool Initialize() {
			if( !base.Initialize() )
				return false;

			_rgLayout.Add( new LayoutRect( LayoutRect.CSS.Pixels, 100, 0.2F ) );
			_rgLayout.Add( new LayoutRect( LayoutRect.CSS.Pixels, 40,  0.1F ) );
			_rgLayout.Add( new LayoutRect( LayoutRect.CSS.None ) );

			_rgColumns.Add( _rgLayout.Item(1) );
			_rgColumns.Add( _rgLayout.Item(2) );
			_rgColumns.Add( _rgLayout.Item(3) );

            HyperLinks.Add( "callsign", OnCallSign );

			return true;
		}

        protected void OnCallSign( Row oRow, int iColumn, IPgWordRange oRange ) {
            BrowserLink( "http://www.qrz.com/db/" +  oRow[0].SubString( oRange.Offset, oRange.Length) );
        }

        protected override void OnKeyPress(KeyPressEventArgs e) {
            if( IsDisposed )
                return;
            if( _oViewEvents.IsCommandPress( e.KeyChar ) )
                return;
            if( _fReadOnly )
                return;

			switch( e.KeyChar ) {
				case '\t':
					int iDir = ModifierKeys == Keys.Shift ? -1 : 1;
					_oCacheMan.CaretTab( iDir );
					break;
				case '\r':
					if( LogDoc.InsertNew( _oCacheMan.CaretAt + 1 ) is Row oRow ) {
						_oCacheMan.CaretReset( oRow, iColumn:0 );
					}
					break;
				default:
					base.OnKeyPress( e );
					break;
			}
		}

		public bool Execute(Guid sGuid) {
			return false;
		}

		public object Decorate(IPgViewSite oBaseSite, Guid sGuid) {
			return null;
		}
    }


    /// <summary>
    /// Override to add our hyperlink.
	/// We'll turn that into optionally a dropdown in the future.
    /// </summary>
    /// <seealso cref="DocProperties"/>
    public class ViewRadioProperties : 
        WindowStandardProperties
     {
        public static Guid GUID {get;} = new Guid("{80C855E0-C2F6-4641-9A7C-B6A8A53B3FDF}");


        public ViewRadioProperties( IPgViewSite oSiteView, DocProperties oDocument ) : base( oSiteView, oDocument ) {
        }

        private void OnFrequencyJump( Row oRow, int iColumn, IPgWordRange oRange ) {
            try {
                string strValue = oRow[iColumn].SubString( oRange.Offset, oRange.Length );
            } catch( Exception oEx ) {
                Type[] rgErrors = { typeof( IndexOutOfRangeException ),
                                    typeof( NullReferenceException ),
                                    typeof( ArgumentNullException ),
                                    typeof( ArgumentException ) };
                if( rgErrors.IsUnhandled( oEx ) )
                    throw;
            }
        }

        public override bool InitNew() {
            if( !base.InitNew() ) 
                return false;

			HyperLinks.Add( "fjump", OnFrequencyJump );
            return true;
        }
    }
}
