using System;
using System.Drawing;
using System.Xml;
using System.Collections.Generic;
using System.Windows.Forms;
using System.IO;

using SkiaSharp;

using Play.Interfaces.Embedding;
using Play.Edit;

namespace Play.SSTV {

	public class WindowTransmit:
		Control,
		IPgParent,
		IPgLoad<XmlElement>,
		IPgSave<XmlDocumentFragment>,
		IPgCommandView,
		IDisposable
	{
		public static Guid ViewType { get; }  = new Guid( "{CED824F5-2C17-418C-9559-84D6B4F571FC}" );

		protected IPgViewSite _oViewSite;
		protected DocSSTV     _oDocSSTV;

		public PropDoc ImageProperties { get; } // Container for properties to show for this window.

		protected class SSTVWinSlot :
			IPgBaseSite
		{
			protected readonly WindowTransmit _oHost;

			public SSTVWinSlot( WindowTransmit oHost ) {
				_oHost = oHost ?? throw new ArgumentNullException();
			}

			public IPgParent Host => _oHost;

			public void LogError(string strMessage, string strDetails, bool fShow=true) {
				_oHost.LogError( strDetails );
			}

			public void Notify( ShellNotify eEvent ) {
				// Might want this value when we close to save the current playing list!!
			}
		}

		public WindowTransmit( IPgViewSite oViewSite, DocSSTV oDocument ) {
			_oViewSite = oViewSite ?? throw new ArgumentNullException( "View requires a view site." );
			_oDocSSTV  = oDocument ?? throw new ArgumentNullException( "View requires a document." );

            ImageProperties = new PropDoc( new SSTVWinSlot( this ) );
		}

		public IPgParent Parentage => _oViewSite.Host;
		public IPgParent Services  => Parentage.Services;
		public bool      IsDirty   => false;
		public string    Banner    => "Frequency Space";
		public Image     Iconic    => null;
		public Guid      Catagory  => ViewType;

		public void LogError( string strMessage ) {
			_oViewSite.LogError( "SSTV View", strMessage );
		}

		protected override void Dispose(  bool disposing ) {
			_oDocSSTV.PropertyChange -= Listen_PropertyChange;

			base.Dispose( disposing );
		}

		public bool InitNew() {
			if( !ImageProperties.InitNew() )
                return false;

			DecorPropertiesInit();

			return true;
		}

		protected virtual void DecorPropertiesInit() {
            _oDocSSTV.PropertyChange += Listen_PropertyChange;
			using( PropDoc.Manipulator oBulk = ImageProperties.EditProperties ) {
				oBulk.Add( "Width" );
				oBulk.Add( "Height" );
				oBulk.Add( "Encoding" );
				oBulk.Add( "Sent" );
				oBulk.Add( "Name" );
			}
		}

		/// <summary>
		/// This is our event sink for property changes on the SSTV document.
		/// </summary>
		/// <remarks>Right now just update all, but we can just update the
		/// specific property in the future. You know, a color coded property, 
		/// light red or yellow on change would be a cool feature.</remarks>
        private void Listen_PropertyChange( ESstvProperty eProp ) {
            DecorPropertiesReLoad();
        }

        protected void DecorPropertiesReLoad() {
			using (PropDoc.Manipulator oBulk = ImageProperties.EditProperties) {
				string strWidth  = string.Empty;
				string strHeight = string.Empty;
				string strName   = Path.GetFileName( _oDocSSTV.BitmapFileName );
				string strMode   = "Unassigned";

				if( _oDocSSTV.Bitmap != null ) {
					strWidth  = _oDocSSTV.Bitmap.Width .ToString();
					strHeight = _oDocSSTV.Bitmap.Height.ToString();
				}
				if( _oDocSSTV.TransmitMode != null ) {
					strMode = _oDocSSTV.TransmitMode.Name;
				}

                oBulk.Set( 0, strWidth  );
                oBulk.Set( 1, strHeight );
                oBulk.Set( 3, "0%"      );
                oBulk.Set( 2, strMode   );
				oBulk.Set( 4, strName   );
            }
		}

        public bool Load(XmlElement oStream) {
			InitNew();
			return true;
		}

		public bool Save(XmlDocumentFragment oStream) {
			return true;
		}

		protected override void OnSizeChanged(EventArgs e) {
			base.OnSizeChanged(e);
			Invalidate();
		}

		public object Decorate(IPgViewSite oBaseSite,Guid sGuid) {
			try {
				if (sGuid.Equals(GlobalDecorations.Properties)) {
					DecorPropertiesReLoad();
					return new PropWin( oBaseSite, ImageProperties );
				}
				return false;
			} catch ( Exception oEx ) {
				Type[] rgErrors = { typeof( NotImplementedException ),
									typeof( NullReferenceException ),
									typeof( ArgumentException ),
									typeof( ArgumentNullException ) };
				if( rgErrors.IsUnhandled( oEx ) )
					throw;

				LogError( "Couldn't create SSTV decor: " + sGuid.ToString() );
			}

            return( null );
		}

		public bool Execute(Guid sGuid) {
			if( sGuid == GlobalCommands.Play ) {
				_oDocSSTV.PlayBegin();
				return true;
			}
			if( sGuid == GlobalCommands.Stop ) {
				_oDocSSTV.PlayStop();
				return true;
			}

			return false;
		}

        protected override void OnKeyDown(KeyEventArgs e) {
            if( this.IsDisposed )
                return;

            e.Handled = true;

            switch( e.KeyCode ) {
                case Keys.Right:
                case Keys.Enter:
                    break;
            }
        }
    }

}
