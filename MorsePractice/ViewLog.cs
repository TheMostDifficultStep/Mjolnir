using System;
using System.Collections.Generic;
using System.Collections;
using System.Windows.Forms;
using System.Xml;
using System.Reflection;

using SkiaSharp;

using Play.Interfaces.Embedding;
using Play.Rectangles;
using Play.Edit;
using Play.Forms;
using Play.Parse;
using Play.Drawing;

namespace Play.MorsePractice {
	/// <summary>
	/// Multi column log viewer!! This is the inner columnar view beneath 
	/// the notes in the "ViewLogAndNotes" case.
	/// </summary>
	/// <seealso cref="ViewLogAndNotes"/>
	public class ViewLog : 
		WindowMultiColumn,
		IPgCommandView,
        IPgLoad<XmlElement>,
		IPgSave<XmlDocumentFragment>
	{
		static public Guid ViewCatagory { get; } = new Guid("{BE243DE2-7763-4A44-9499-0EEDBC84D8A4}");
        static readonly protected string _strIcon = @"Play.MorsePractice.Content.icons8-copybook-60.png";

		public Guid   Catagory => ViewCatagory;
		public string Banner   => "Log Viewer";
		public SKBitmap Icon   { get; protected set; }

		protected DocLog LogDoc { get; }

		public ViewLog( IPgViewSite oSiteView, DocLog oDocument ) :
			base( oSiteView, oDocument )
		{
			LogDoc = oDocument;
			Icon   = SKImageResourceHelper.GetImageResource( Assembly.GetExecutingAssembly(), _strIcon );
		}

		protected override bool Initialize() {
			if( !base.Initialize() )
				return false;

			TextLayoutAdd( new LayoutRect( LayoutRect.CSS.Flex ) { Track = 60 }, DocLog.LogRow.ColumnCall );
			TextLayoutAdd( new LayoutRect( LayoutRect.CSS.Flex ) { Track = 10 }, DocLog.LogRow.ColumnStat );
			TextLayoutAdd( new LayoutRect( LayoutRect.CSS.None ), DocLog.LogRow.ColumnNote );

            HyperLinks.Add( "callsign", OnCallSign );

			return true;
		}

        protected void OnCallSign( Row oRow, int iColumn, IPgWordRange oRange ) {
            BrowserLink( "http://www.qrz.com/db/" +  oRow[iColumn].SubString( oRange.Offset, oRange.Length) );
        }

        protected override void OnKeyPress(KeyPressEventArgs e) {
            if( IsDisposed )
                return;
            if( _oViewEvents.IsCommandPress( e.KeyChar ) )
                return;
            if( IsReadOnly )
                return;

			switch( e.KeyChar ) {
				case '\t':
					int iDir = ModifierKeys == Keys.Shift ? -1 : 1;
					_oCacheMan.CaretTab( iDir );
					break;
				case '\r':
					// Not likely unset upon key press. But problematic if negative.
					if( _oCacheMan.CaretAt < 0 )
						break;

					if( LogDoc.InsertNew( _oCacheMan.CaretAt + 1 ) is Row oRow ) {
						_oCacheMan.CaretReset( oRow, iColumn:0 );
					}
					break;
				default:
					base.OnKeyPress( e );
					break;
			}
		}

        protected override void OnKeyDown(KeyEventArgs e) {
            base.OnKeyDown(e);

			switch( e.KeyCode ) {
                case Keys.Down:
                case Keys.Up:
                case Keys.Right:
                case Keys.Left:
					SetOutlineHighlight();
					break;
			}
        }

		protected override void OnMouseUp( MouseEventArgs e ) {
			base.OnMouseUp( e );

			SetOutlineHighlight();
		}

        public object Decorate(IPgViewSite oBaseSite, Guid sGuid) {
			return null;
		}

		protected void SetOutlineHighlight() {
			if( Parentage is ViewLogAndNotes oParentView ) {
				oParentView.SetOutlineCaret( Caret2.Row );
			} 
		}
    }

	public class ViewOutline : 
		WindowMultiColumn
	{
		public ViewLogAndNotes Owner { get; private set; }
		public ViewOutline( IPgViewSite oSiteView, DocLogOutline oDocument, ViewLogAndNotes oOwner ) :
			base( oSiteView, oDocument )
		{
			Owner = oOwner;
		}

        protected override void Dispose(bool disposing) {
			if( disposing ) {
				// Unwind the circle of death. ;-)
				Owner.ViewOutline = null;
				Owner = null;
			}
            base.Dispose(disposing);
        }

        protected override bool Initialize() {
			if( !base.Initialize() )
				return false;

			TextLayoutAdd( new LayoutRect( LayoutRect.CSS.Flex ), (int)DocLogOutline.DictRow.DCol.Call );
			TextLayoutAdd( new LayoutRect( LayoutRect.CSS.None ), (int)DocLogOutline.DictRow.DCol.Refs );

            HyperLinks.Add( "LogReference", OnLogReference );

			Owner.ViewOutline = this;

			return true;
		}

        protected void OnLogReference( Row oRow, int iColumn, IPgWordRange oRange ) {
            if( oRow   is DocLogOutline.DictRow     oDictRow &&
				oRange is DocLogOutline.ReportRange oReptRange ) 
			{
				Row oLogRow = oDictRow.LogRefRows[oReptRange.LogRowIndex];

				Owner.SelectionSet( oLogRow.At, 0, 0, 0 );
				Owner.ScrollTo    ( SCROLLPOS.CARET  );
			}
        }
    }

    /// <summary>
    /// Override to add our hyperlink.
	/// We'll turn that into optionally a dropdown in the future.
    /// </summary>
    /// <seealso cref="RadioProperties"/>
    public class ViewRadioProperties : 
        WindowStandardProperties
     {
        public ViewRadioProperties( IPgViewSite oSiteView, DocProperties oDocument ) : 
			base( oSiteView, oDocument ) 
		{
        }

		/// <summary>
		/// I'm taking a leap of faith on the tone for the repeater, it might have
		/// different tone values for different frequencies. I might need to look
		/// it up in my table. Right now just assume it's the same for all channels
		/// of that organization...
		/// </summary>
        private void OnFrequencyJump( Row oRow, int iColumn, IPgWordRange oRange ) {
            try {
                string strFreqInMHz = oRow[iColumn].SubString( oRange.Offset, oRange.Length );

				if( Document.Parentage is DocStdLog oStdLog ) {
					int iFreqInHz = (int)(double.Parse( strFreqInMHz ) * Math.Pow( 10, 6 ) );
					oStdLog.DoFrequencyJump( iFreqInHz );
				}
            } catch( Exception oEx ) {
                Type[] rgErrors = { typeof( IndexOutOfRangeException ),
                                    typeof( NullReferenceException ),
                                    typeof( ArgumentNullException ),
                                    typeof( ArgumentException ),
									typeof( FormatException ),
									typeof( OverflowException ) };
                if( rgErrors.IsUnhandled( oEx ) )
                    throw;

				LogError( "Frequency read error..." );
            }
        }

        public override bool InitNew() {
            if( !base.InitNew() ) 
                return false;

			HyperLinks.Add( "alternate", OnFrequencyJump );

            return true;
        }
    }
}
