﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Xml;

using Play.Edit;
using Play.Interfaces.Embedding;

namespace AddressBook {
    public class DocOutline :
        EditMultiColumn,
        IPgLoad<XmlNodeList?>
	{
        public enum Column : int {
            LastName = 0,
            FirstName
        }

        public class DDRow : Row {
            public static int ColumnCount => Enum.GetNames(typeof(Column)).Length;

			public Line this[ Column eIndex] {
				get { return _rgColumns[(int)eIndex]; }
				set { _rgColumns[(int)eIndex] = value; }
			}
            public DDRow( string strFirstName, string strLastName ) {
                _rgColumns = new Line[ColumnCount];

                this[Column.LastName  ] = new TextLine( (int)Column.LastName,  strLastName );
                this[Column.FirstName ] = new TextLine( (int)Column.FirstName, strFirstName );
            }
        }

        public DocOutline(IPgBaseSite oSiteBase) : base(oSiteBase) {
        }

        public bool InitNew() {
            return false;
        }

        public bool Load(XmlNodeList? rgEntries ) {
			if( rgEntries is null )
				throw new ArgumentNullException();

			try {
				// Load up the entries.
				foreach( XmlElement xmlEntry in rgEntries ) {
                    string strFirst = xmlEntry.GetAttribute( "first" );
                    string strLast  = xmlEntry.GetAttribute( "last" );

					_rgRows.Add( new DDRow( strFirst, strLast ) );
				}
				// TODO: I'd like to use span's but need to get System.MemoryExtensions working.
				_rgRows.Sort( (x,y) => string.Compare( x[(int)Column.LastName].ToString(),
													   y[(int)Column.LastName].ToString() ) );

				RenumberAndSumate();

				return true;
			} catch( Exception oEx ) {
				Type[] rgErrors = { typeof( NullReferenceException ) };
				if( rgErrors.IsUnhandled( oEx ) )
					throw;

				LogError( "Trouble reading address book." );
			}

			return false;
        }
    }
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

        public DocOutline Outline { get; }
		public Editor     Entry   { get; }

        public IPgParent Parentage => _oBaseSite.Host;
        public IPgParent Services  => Parentage.Services;

        protected IPgBaseSite      _oBaseSite;
		protected SortedList<string, XmlElement> _rgEntries = new();

        public DocAddrBook( IPgBaseSite oSite ) {
            _oBaseSite = oSite ?? throw new ArgumentNullException();

            Outline = new ( new DocSlot( this ) );
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

            try {
                xmlBook.Load( oStream );

				XmlNodeList? rgEntries = xmlBook.SelectNodes("book/entries/entry");

				if( !Outline.Load( rgEntries ) )
					return false;
            } catch( Exception oEx ) {
				Type[] rgErrors = { typeof( XmlException ) };
				if( rgErrors.IsUnhandled( oEx ) )
					throw;

                LogError( oEx, "Couldn't read xml config." );
				return false;
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
