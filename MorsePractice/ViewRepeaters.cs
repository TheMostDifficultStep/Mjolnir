using System;
using System.Reflection;

using SkiaSharp;

using Play.Interfaces.Embedding;
using Play.Rectangles;
using Play.Edit;
using Play.Parse;
using Play.Drawing;
using static Play.MorsePractice.DocStdLog;

namespace Play.MorsePractice {
    /// <summary>
    /// Repeaters list.
    /// </summary>
    public class DocRepeaters :
        EditMultiColumn,
		IPgLoad
    {
        public DocRepeaters( IPgBaseSite oSiteBase ) : base(oSiteBase) {
        }

		/// <summary>
		/// Right now we'll just load the hard coded list into us. In the future
		/// we'll get all that data from an XML file.
		/// </summary>
        public bool InitNew() {
			if( _oSiteBase.Host is DocStdLog oStdLog ) {
                using( BulkLoader oLoader = new( this ) ) {
					foreach( DocStdLog.RepeaterInfo oInfo in oStdLog.RepeatersInfo.Values ) {
						RepRow oRow = new RepRow( oInfo );
						oLoader.Append( oRow );
					}
				}
				return true;
			}
            return false;
        }

        public class RepRow : Row {
			public string URL {get;}
            public RepRow( DocStdLog.RepeaterInfo oInfo ) {
                _rgColumns = new Line[3];

				_rgColumns[0] = new TextLine( ColumnCallSign, oInfo.CallSign );
				_rgColumns[1] = new TextLine( ColumnName,     oInfo.Group );
				_rgColumns[2] = new TextLine( ColumnLocation, oInfo.Location );

				URL = oInfo.URL;
				if( !string.IsNullOrEmpty( URL ) ) {
					AddHyperLink( ColumnName );
				}
            }

			protected void AddHyperLink( int iColumn ) {
				_rgColumns[iColumn].Formatting.Add( new RepeaterHyperText( 1, 0, _rgColumns[iColumn].ElementCount, "website" ) );
			}

			public const int ColumnCallSign = 0;
            public const int ColumnName     = 1;
            public const int ColumnLocation = 2;
        }

        /// <summary>
        /// Since, at present, we only load the document once there is
		/// no reason to schedule a parse since this function will only
		/// be called once after the document is loaded.
        /// </summary>
		/// <seealso cref="DocLog.GetParseEnum"/>
		/// <seealso cref="DocLog.DoParse"/>
        public override void DoParse() {
            RenumberAndSumate();
            ParseColumn      ( RepRow.ColumnCallSign );

            Raise_DocFormatted();
        }

	}

	/// <summary>
	/// Multi column log viewer!! This is the inner columnar view beneath 
	/// the notes in the "ViewLogAndNotes" case.
	/// </summary>
	/// <seealso cref="ViewLogAndNotes"/>
	public class ViewRepeaters : 
		WindowMultiColumn,
		IPgCommandView
	{
		static public Guid ViewCatagory { get; } = new Guid("{D99E1EDF-5D06-4AB1-BBFF-29BC1312F6E6}");
        static readonly protected string _strIcon = @"Play.MorsePractice.Content.icons8-address-48.png";

		public Guid   Catagory => ViewCatagory;
		public string Banner   => "Repeater List Viewer";
		public SKImage Icon   { get; }

		protected DocRepeaters RepeaterDoc { get; }

		public ViewRepeaters( IPgViewSite oSiteView, DocRepeaters oDocument ) :
			base( oSiteView, oDocument )
		{
			RepeaterDoc = oDocument;
			Icon        = SKImageResourceHelper.GetImageResource( Assembly.GetExecutingAssembly(), _strIcon );
		}

		protected override bool Initialize() {
			if( !base.Initialize() )
				return false;

			TextLayoutAdd( new LayoutRect( LayoutRect.CSS.Flex ) { Track = 30 }, DocRepeaters.RepRow.ColumnCallSign );
			TextLayoutAdd( new LayoutRect( LayoutRect.CSS.Flex ) { Track = 60 }, DocRepeaters.RepRow.ColumnName );
			TextLayoutAdd( new LayoutRect( LayoutRect.CSS.None ),                DocRepeaters.RepRow.ColumnLocation );

            HyperLinks.Add( "website",  OnWebSite  );
			HyperLinks.Add( "callsign", OnCallSign );

			return true;
		}
        protected void OnWebSite( Row oRow, int iColumn, IPgWordRange oRange ) {
			if( oRow is DocRepeaters.RepRow oRepeater && !string.IsNullOrEmpty( oRepeater.URL ) ) {
				BrowserLink( oRepeater.URL );
			}
        }

        protected void OnCallSign( Row oRow, int iColumn, IPgWordRange oRange ) {
            BrowserLink( "http://www.qrz.com/db/" +  oRow[iColumn].SubString( oRange.Offset, oRange.Length) );
        }

        public object Decorate(IPgViewSite oBaseSite, Guid sGuid) {
			return null;
        }
    }
}

