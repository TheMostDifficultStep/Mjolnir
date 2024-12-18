using System.Text;
using System.Xml;

using Play.Edit;
using Play.Interfaces.Embedding;

namespace AddressBook {
	/// <summary>
	/// Use this to construct a simple entry from the XML.
	/// </summary>
    public class DocEntry : Editor, IPgLoad<Row>{
        public DocEntry(IPgBaseSite oSite) : base(oSite) {
        }

		/// <summary>
		/// Since this object is embedded within the address
		/// book document we need to override this so we'll 
		/// see the filename in the normal EditWindow2 being
		/// used to show that document.
		/// </summary>
		public override string FileBase {
			get { 
                if( _oSiteBase.Host is DocAddrBook oEdit )
					return oEdit.FileBase; 

                return string.Empty;
			}
		}

        public bool Load( Row oRow ) {
			Clear();

			if( oRow is not DocOutline.DDRow oElem )
				return false;

			using Manipulator oBulk   = CreateManipulator();
			StringBuilder     sbBuild = new StringBuilder();

			XmlNodeList? rgAddrs = oElem.Entry.SelectNodes( "a" );

			try {
				sbBuild.Append( oElem.FirstName );
				sbBuild.Append( ' ' );
				sbBuild.Append( oElem.LastName  );

				oBulk.LineAppend( sbBuild.ToString() );

				if( rgAddrs is not null && rgAddrs.Count > 0 ) {
					XmlElement xmlAddr = (XmlElement)rgAddrs[0];

					oBulk.LineAppend( xmlAddr.InnerText );

					sbBuild.Clear();
					sbBuild.Append( xmlAddr.GetAttribute("city") );
					sbBuild.Append( ' ' );
					sbBuild.Append( xmlAddr.GetAttribute("st") );
					sbBuild.Append( ' ' );
					sbBuild.Append( xmlAddr.GetAttribute("zip") );
					
					oBulk.LineAppend( sbBuild.ToString() );
				}
			} catch( Exception oEx ) {
				Type[] rgErrors = { typeof( NullReferenceException ),
									typeof( InvalidCastException ) };
				if( rgErrors.IsUnhandled( oEx ) )
					throw;

				return false;
			}

			return true;
        }
    }
    public class DocOutline :
        EditMultiColumn,
        IPgLoad<XmlNodeList?>
	{
        public enum Column : int {
            LastName = 0,
            FirstName,
			Check
        }

        public class DDRow : Row {
            public static int ColumnCount => Enum.GetNames(typeof(Column)).Length;
			
			public XmlElement Entry { get; }

			public Line this[ Column eIndex] {
				get { return _rgColumns[(int)eIndex]; }
				set { _rgColumns[(int)eIndex] = value; }
			}

			public string LastName  => this[Column.LastName ].ToString();
			public string FirstName => this[Column.FirstName].ToString();
			public ReadOnlySpan<char> Check => this[Column.Check].AsSpan;

            public DDRow( string strFirstName, string strLastName, XmlElement xmlEntry ) {
                _rgColumns = new Line[ColumnCount];

                this[Column.LastName  ] = new TextLine( (int)Column.LastName,  strLastName );
                this[Column.FirstName ] = new TextLine( (int)Column.FirstName, strFirstName );
				this[Column.Check     ] = new TextLine( (int)Column.Check, string.Empty );

				Entry = xmlEntry ?? throw new ArgumentNullException();
            }
        }

        public DocOutline(IPgBaseSite oSiteBase) : base(oSiteBase) {
			CheckColumn = (int)Column.Check;
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

					_rgRows.Add( new DDRow( strFirst, strLast, xmlEntry ) );
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
		public DocEntry   Entry   { get; }

        public IPgParent Parentage => _oBaseSite.Host;
        public IPgParent Services  => Parentage.Services;

        protected IPgBaseSite      _oBaseSite;
		protected IPgFileSite      _oFileSite;
		protected SortedList<string, XmlElement> _rgEntries = new();

        public DocAddrBook( IPgBaseSite oSite ) {
            _oBaseSite = oSite ?? throw new ArgumentNullException();
			_oFileSite = (IPgFileSite)oSite;

            Outline = new ( new DocSlot( this ) );
			Entry   = new ( new DocSlot( this ) );
        }

        public void Dispose() {
            Outline.Event_Check -= OnCheck_Outline;
			Outline.Dispose();
        }

		public void LogError( Exception oEx, string strMessage ) {
			_oBaseSite.LogError( "Address Book", strMessage );
		}

		public string FileBase {
			get { 
                return _oFileSite.FileName; 
			}
		}

		protected bool Initialize() {
			if( !Outline.InitNew() )
				return false;
			if( !Entry.InitNew() )
				return false;

			return true;
		}

        public bool Load(TextReader oStream) {
            XmlDocument xmlBook = new();

            try {
                xmlBook.Load( oStream );

				if( !Outline.Load( xmlBook.SelectNodes("book/entries/entry" ) ) )
					return false;

				if( Outline.ElementCount > 0 ) {
					Entry.Load( Outline[0] );
				}
				Outline.SetCheckAtRow( Outline[0] );
                Outline.Event_Check += OnCheck_Outline;
            } catch( Exception oEx ) {
				Type[] rgErrors = { typeof( XmlException ), typeof( NullReferenceException ) };
				if( rgErrors.IsUnhandled( oEx ) )
					throw;

                LogError( oEx, "Couldn't read Address Book." );
				return false;
            }


			return true;
        }

        private void OnCheck_Outline( Row oRow ) {
            Entry.Load( oRow );
        }

        /// <summary>
        /// Tho' we have no facility for entering new values yet. You have
        /// to edit the xml file directly! >_<;;
        /// </summary>
        public bool InitNew() {
            return true;
        }

		public bool IsRowIndexInside( int iRow ) {
			return iRow > 0 && iRow < Outline.ElementCount;
		}

		public void Jump( int iDir ) {
			if( Math.Abs( iDir ) != 1 )
				throw new ArgumentOutOfRangeException();

			if( Outline.CheckedRow is null ) {
				Outline.SetCheckAtRow( Outline[0] );
				return;
			}

			int iNext = iDir + Outline.CheckedRow.At;
			if( IsRowIndexInside( iNext ) ) {
				Outline.SetCheckAtRow( Outline[iNext] );
			}
		}
    }
}
