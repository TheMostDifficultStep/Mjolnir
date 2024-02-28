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
using Play.Drawing;
using System.Reflection;

namespace Play.MorsePractice {
	/// <summary>
	/// Reviving this experiment, multi column log viewer!! This is the 
	/// inner columnar view beneath the notes in the "ViewLogAndNotes" case.
	/// </summary>
	/// <seealso cref="ViewLogAndNotes"/>
	public class ViewNetLog : 
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

		protected DocLogMultiColumn LogDoc { get; }

		public ViewNetLog( IPgViewSite oSiteView, object oDocument ) :
			base( oSiteView, oDocument )
		{
			LogDoc = (DocLogMultiColumn)oDocument;
			Icon   = SKImageResourceHelper.GetImageResource( Assembly.GetExecutingAssembly(), _strIcon );
		}

		protected override bool Initialize() {
			if( !base.Initialize() )
				return false;

			_rgLayout.Add( new LayoutRect( LayoutRect.CSS.Pixels, 90, 0.2F ) );
			_rgLayout.Add( new LayoutRect( LayoutRect.CSS.Pixels, 40, 0.1F ) );
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
    /// <seealso cref="RadioProperties"/>
    public class ViewRadioProperties : 
        WindowStandardProperties
     {
        public static Guid GUID {get;} = new Guid("{80C855E0-C2F6-4641-9A7C-B6A8A53B3FDF}");


        public ViewRadioProperties( IPgViewSite oSiteView, DocProperties oDocument ) : base( oSiteView, oDocument ) {
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
