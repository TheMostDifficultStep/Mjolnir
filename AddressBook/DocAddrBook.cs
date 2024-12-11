using System;
using System.Collections.Generic;
using System.Text;
using System.Xml;

using Play.Interfaces;
using Play.Edit;
using Play.Interfaces.Embedding;

namespace AddressBook {
    public class DocAddrBook :
		IDisposable,
		IPgParent,
		IPgLoad<TextReader>
	{
		protected class DocSlot :
			IPgBaseSite
		{
			protected readonly DocAddrBook _oHost;

			public DocSlot( DocAddrBook oHost ) {
				_oHost = oHost ?? throw new ArgumentNullException();
			}

			public IPgParent Host => _oHost;

			public void LogError(string strMessage, string strDetails, bool fShow=true) {
				_oHost._oBaseSite.LogError( strMessage, strDetails, fShow );
			}

			public void Notify( ShellNotify eEvent ) {
			}
		} // End class

        public Editor Outline { get; }
		public Editor Entry   { get; }

        public IPgParent Parentage => _oBaseSite.Host;
        public IPgParent Services  => Parentage.Services;

        protected IPgBaseSite      _oBaseSite;
		protected List<XmlElement> _rgEntries = new();

        public DocAddrBook( IPgBaseSite oSite ) {
            _oBaseSite = oSite ?? throw new ArgumentNullException();

            Outline = new Editor( new DocSlot( this ) );
			Entry   = new Editor( new DocSlot( this ) );
        }

        public void Dispose() {
			Outline.Dispose();
        }

		public void LogError( Exception oEx, string strMessage ) {
			_oBaseSite.LogError( "Address Book", strMessage );
		}

		protected bool Initialize() {
			if( !Outline.InitNew() )
				return false;
			if( !Entry.InitNew() )
				return false;

			return true;
		}

        public bool Load(TextReader oStream) {
            XmlDocument  xmlBook   = new XmlDocument();
			XmlNodeList? rgEntries;

            try {
                xmlBook.Load( oStream );

				rgEntries = xmlBook.SelectNodes("book/entries/entry");
            } catch( Exception oEx ) {
				Type[] rgErrors = { typeof( XmlException ) };
				if( rgErrors.IsUnhandled( oEx ) )
					throw;

                LogError( oEx, "Couldn't read xml config." );
				return false;
            }

			if( rgEntries is null )
				return false;

			try {
				using Editor.Manipulator oBulkOutl = Outline.CreateManipulator();

				// Load up the entries.
				foreach( XmlElement xmlEntry in rgEntries ) {
                    string strFirst = xmlEntry.GetAttribute( "first" );
                    string strLast  = xmlEntry.GetAttribute( "last" );

					Line oLine = oBulkOutl.LineAppend( strLast + ": " + strFirst );
					_rgEntries.Add( xmlEntry );
					oLine.Extra = oLine;
				}
			} catch( Exception oEx ) {
				Type[] rgErrors = { typeof( NullReferenceException ) };
				if( rgErrors.IsUnhandled( oEx ) )
					throw;

				LogError( oEx, "Trouble reading address book." );
			}

			return true;
        }

		/// <summary>
		/// Tho' we have no facility for entering new values yet. You have
		/// to edit the xml file directly! >_<;;
		/// </summary>
        public bool InitNew() {
            return true;
        }
    }
}
