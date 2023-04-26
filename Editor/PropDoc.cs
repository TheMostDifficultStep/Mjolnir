using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;

using Play.Interfaces.Embedding;
using Play.Parse.Impl;
using Play.Parse;

namespace Play.Edit {
	public interface IPgPropertyChanges {
		void OnPropertyChanged( int iLine );
		void OnPropertyDone   ( bool fChangedAll );
		void OnPropertiesClear();
		void OnPropertiesLoad ();
	}

	/// <summary>
	/// This could TOTALLY be a tuple but, I'm not high enough framework yet...
	/// </summary>
	public struct PropertyItem {
		public PropertyItem( Line oName, Line oValue ) {
			Name  = oName  ?? throw new ArgumentNullException();
			Value = oValue ?? throw new ArgumentNullException();
		}

		public Line Name;
		public Line Value;
	}

	/// <seealso cref="SubStream" />
	public class SimpleStream : DataStream<char> {
		Line _oLine;
		int  _iPos;

		public SimpleStream( Line oLine ) {
			_oLine = oLine ?? throw new ArgumentNullException();
		}

		public override char this[int iPos] => _oLine[iPos];

		public override int Position { get => _iPos; set => _iPos = value; }

		public override bool InBounds(int p_iPos) {
			if( p_iPos < 0 )
				return false;

			if( p_iPos >= _oLine.ElementCount )
				return false;

			return true;
		}

		public override string SubString(int iPos, int iLen) {
			return _oLine.SubString( iPos, iLen );
		}
	}

	[Obsolete ("Use DocProperties from the Forms project." )]public class PropDoc : 
		IPgParent,
		IEnumerable<PropertyItem>,
		IPgLoad<TextReader>,
		IDisposable
	{
		readonly IPgBaseSite              _oSiteBase;
		readonly List<PropertyItem>       _rgProperties = new List<PropertyItem>();
		readonly List<IPgPropertyChanges> _rgChangeEvents = new List<IPgPropertyChanges>();
		readonly Grammer<char>            _oTextGrammar;

		public class DocSlot : 
			IPgBaseSite
		{
			protected readonly PropDoc _oDoc;

			public DocSlot( PropDoc oDoc ) {
				_oDoc = oDoc ?? throw new ArgumentNullException( "Image document must not be null." );
			}

			public void LogError( string strMessage, string strDetails, bool fShow=true ) {
				_oDoc.LogError( strMessage, "PropDocSlot : " + strDetails );
			}

			public void Notify( ShellNotify eEvent ) {
			}

			public IPgParent Host => _oDoc;
		}

		/// <summary>
		/// Sometimes I surprise myself.
		/// </summary>
		public class ParseSetSlot : DocSlot,
			IParseEvents<char>
		{
			public Line Line { get; set; }

			public ParseSetSlot( PropDoc oDoc ) : base( oDoc ) {
			}

			public void OnMatch(ProdBase<char> oElem, int iStart, int iLength) {
				if( iLength <= 0 )
					return;

				if( oElem is ProdBase<char> && oElem.IsTerm && oElem.IsVisible ) {
					Line.Formatting.Add( new WordRange( iStart, iLength, 0 ) );
				}
			}

			public void OnParserError(ProdBase<char> p_oMemory, int p_iStart) {
				LogError( "Property Parsing", "Couldn't parse property.", fShow:false );
			}
		}

		public class Manipulator : 
			IPgFormBulkUpdates,
			IDisposable
		{
			PropDoc _oDoc;
			bool    _fChangedAll = false;

			public Manipulator( PropDoc oDoc ) 
			{
				_oDoc = oDoc ?? throw new ArgumentNullException();
			}

			public void Dispose() {
				// TODO: In the future, we'll just parse the lines that change.
				_oDoc.ParsePropertyValues();
				foreach( IPgPropertyChanges oEvent in _oDoc._rgChangeEvents ) {
					oEvent.OnPropertyDone( _fChangedAll );
				}
			}

			public int AddProperty( string strLabel ) {
				int iLine = _oDoc._rgProperties.Count;

				_oDoc._rgProperties.Add( new PropertyItem (new TextLine( iLine, strLabel ), new TextLine( iLine, string.Empty ) ) );

				foreach( IPgPropertyChanges oEvent in _oDoc._rgChangeEvents ) {
					oEvent.OnPropertyChanged( iLine );
				}

				return iLine;
			}

			public void SetValue(int iLine, string strValue) {
				try {
					Line oLine = _oDoc._rgProperties[iLine].Value;

					oLine.Empty();
					oLine.TryInsert( 0, strValue, 0, strValue.Length );

					foreach( IPgPropertyChanges oEvent in _oDoc._rgChangeEvents ) {
						oEvent.OnPropertyChanged( iLine );
					}
				} catch( ArgumentOutOfRangeException ) {
					_oDoc.LogError( "property value", "Assign index out of range" );
				}
			}

			public void SetLabel( int iProperty, string strName ) {
				try {
					PropertyItem oItem = _oDoc._rgProperties[iProperty];

					oItem.Name.Empty();
					oItem.Name.TryAppend( strName );
				} catch( ArgumentOutOfRangeException ) {
					_oDoc.LogError( "property label", "Assign index out of range" );
				}
			}
		} // end class

		public IPgParent Parentage => _oSiteBase.Host;
		public IPgParent Services  => Parentage.Services;

	  //public WordBreakerHandler ParseWords { get; } // A basic word breaker/counter for wrapped views.

		public PropDoc( IPgBaseSite oSiteBase ) {
			_oSiteBase = oSiteBase ?? throw new ArgumentNullException();

			//Grammer<char> oLineBreakerGrammar = LineBreakerGrammar;
			//if( oLineBreakerGrammar != null ) {
			//	ParseWords = new WordBreakerHandler( LineBreakerGrammar );
			//}
			
			try {
				_oTextGrammar = (Grammer<char>)((IPgGrammers)Services).GetGrammer( "text" );
			} catch ( Exception oEx ) {
                Type[] rgErrors = { typeof( NullReferenceException ),
                                    typeof( InvalidCastException ),
                                    typeof( ArgumentNullException ),
									typeof( GrammerNotFoundException ) };
                if( !rgErrors.IsUnhandled( oEx ) )
                    throw;

				LogError( "Property Parsing", "Couldn't get grammar for property assignment parsing." );
			}
		}

		public void Dispose() {
			_rgProperties.Clear();

			foreach( IPgPropertyChanges oEvent in _rgChangeEvents ) {
				oEvent.OnPropertiesClear();
			}

			_rgChangeEvents.Clear();
		}

		public void Clear() {
			_rgProperties.Clear();

			foreach( IPgPropertyChanges oEvent in _rgChangeEvents ) {
				oEvent.OnPropertiesClear();
			}
		}

        public virtual Grammer<char> LineBreakerGrammar {
            get {
				try {
					IPgGrammers oGrammars = Services as IPgGrammers;
					return( oGrammars.GetGrammer("line_breaker") as Grammer<char> ); 
				} catch( Exception oEx ) {
					Type[] rgErrors = { typeof( NullReferenceException ),
										typeof( InvalidCastException ),
										typeof( FileNotFoundException ),
										typeof( GrammerNotFoundException ),
										typeof( NotImplementedException ) };
					if( rgErrors.IsUnhandled( oEx ))
						throw;

					return( null );
				}
            }
        }

		//public void WordBreak( Line oLine, ICollection<IPgWordRange> rgWords ) {
        //    if( ParseWords != null )
		//	    ParseWords.Parse( oLine.GetStream(), rgWords );
		//	oLine.WordCount = rgWords.Count;
		//}

		/// <summary>
		/// While it parses, the formatting isn't making it into the Words 
		/// collection of the Cache element. Mainly because word breaking 
		/// currently ONLY occurs for the visible (cached) lines. Need to
		/// sort this all out.
		/// </summary>
		public void ParsePropertyValues() {
			ParseSetSlot oSlot = new ParseSetSlot( this );
			
			if( _oTextGrammar == null )
				return;
			
			foreach( PropertyItem oProperty in _rgProperties ) {
				if( oProperty.Value.ElementCount > 0 ) {
					oSlot.Line = oProperty.Value;
					oSlot.Line.Formatting.Clear();

					State<char>         oStart  = _oTextGrammar.FindState("start");
					DataStream<char>    oStream = new SimpleStream( oProperty.Value );
					MemoryState<char>   oMStart = new MemoryState<char>( new ProdState<char>( oStart ), null );
					ParseIterator<char> oParser = new ParseIterator<char>( oStream, oSlot, oMStart );

					while( oParser.MoveNext() );
				}
			}
		}

		/// <summary>Call this once to set up the properties,
		/// this method will throw an exception if called after that.</summary>
		/// <exception cref="InvalidOperationException" />
		/// <remarks>We cold allow labels to be added or edited, but
		/// we don't support the events for all that yet.</remarks>
		public Manipulator EditProperties {
			get {
				return new Manipulator( this );
			}
		}

		protected void LogError( string strMessage, string strDetail ) {
			_oSiteBase.LogError( strMessage, strDetail );
		}

		public void ListenerAdd( IPgPropertyChanges oEvent ) {
			_rgChangeEvents.Add( oEvent );
		}

		public void ListenerRemove( IPgPropertyChanges oEvent ) {
			_rgChangeEvents.Remove( oEvent );
		}

		public IEnumerator<PropertyItem> GetEnumerator() {
			foreach( PropertyItem oItem in _rgProperties ) {
				yield return oItem;
			}
		}

		IEnumerator IEnumerable.GetEnumerator() {
			return GetEnumerator();
		}

		public bool InitNew() {
			return true;
		}

		public class ParseLoadSlot : DocSlot,
			IParseEvents<char>
		{
			DataStream<char> _oStream;
			int              _iLastProperty;

			public ParseLoadSlot( PropDoc oDoc, DataStream<char> oStream ) : base( oDoc ) {
				_oStream = oStream ?? throw new ArgumentNullException();
			}

			public void OnMatch(ProdBase<char> oElem, int iStart, int iLength) {
				if( oElem is MemoryBinder<char> oBind ) {
					// TODO: Would be nice to do a line to line copy, but for now this will do.
					switch( oBind.Target.ID ) {
						case "pn": {
							int    iLine  = _oDoc._rgProperties.Count;
							string strNew = _oStream.SubString( oBind.Start, iStart - oBind.Start );

							_oDoc._rgProperties.Add( new PropertyItem (new TextLine( iLine, strNew ), new TextLine( iLine, string.Empty ) ) );
							_iLastProperty = iLine;
							break;
							}
						case "pv": {
							Line   oLine  = _oDoc._rgProperties[_iLastProperty].Value;
							string strNew = _oStream.SubString( oBind.Start, iStart - oBind.Start );

							oLine.Empty();
							oLine.TryInsert( 0, strNew, 0, strNew.Length );
							break;
							}
					}
				}
			}

			public void OnParserError(ProdBase<char> p_oMemory, int p_iStart) {
				LogError( "Property Parsing", "Couldn't parse property file." );
			}
		}

		/// <summary>
		/// In this case, we'll property parse a tab delimited text file and 
		/// construct the property array automagically. We're kind of doing a
		/// lot in a load and we could potentially use the task scheduler. 
		/// </summary>
		/// <remarks>BUG: Need to make sure the properties are at least accessible in the main 
		/// program grammer resources. Like "linebreaker" and "text" bnf's</remarks>
		public bool Load(TextReader oFileStream) {
			Grammer<char> oPropertyGrammar;

			Clear();

			try {
				oPropertyGrammar = (Grammer<char>)((IPgGrammers)Services).GetGrammer( "properties" );

				if( oPropertyGrammar == null ) {
					return false;
				}
			} catch ( Exception oEx ) {
                Type[] rgErrors = { typeof( NullReferenceException ),
                                    typeof( InvalidCastException ),
                                    typeof( ArgumentNullException ),
									typeof( GrammerNotFoundException ) };
                if( !rgErrors.IsUnhandled( oEx ) )
                    throw;

				LogError( "Property Parsing", "Couldn't get grammar for property assignment parsing." );
				return false;
			}

			Editor oPropFile = new Editor( new DocSlot( this ) );
			
			if( !oPropFile.Load( oFileStream ) ) {
				LogError( "Property Parsing", "Couldn't read file for property assignment parsing." );
				return false;
			}

			State<char>         oStart  = oPropertyGrammar.FindState("start");
			DataStream<char>    oStream = oPropFile.CreateStream();
			MemoryState<char>   oMStart = new MemoryState<char>( new ProdState<char>( oStart ), null );
			ParseLoadSlot       oSlot   = new ParseLoadSlot( this, oStream );
			ParseIterator<char> oParser = new ParseIterator<char>( oStream, oSlot, oMStart );

			while( oParser.MoveNext() );

			ParsePropertyValues();

			foreach( IPgPropertyChanges oEvent in _rgChangeEvents ) {
				oEvent.OnPropertiesLoad();
			}

			return false;
		}
	}
}
