using System;
using System.Collections.Generic;
using System.Linq;

using Play.Parse.Impl;

namespace Play.Edit {
	public delegate void CreateEntry( IMemoryRange oRange );

	public static class SoundExtensions {
		/// <summary>
		/// Find the songs on the line and stuff 'em into the list.
		/// </summary>
		/// <param name="oLine">Line from text buffer to inspect.</param>
		/// <param name="rgSongs">Create a song entry in this collection if found.</param>
		/// <returns></returns>
		public static bool FindSong( this Line oLine, CreateEntry oSongFactory, string strToken ) {
			if( oLine == null )
				throw new ArgumentNullException( "Line object argument is null." );

			uint iCount = 0;
			foreach( IMemoryRange oRange in oLine.Formatting ) {
				try {
					// Note: Check the new way of casting...
					if (oRange is MemoryState<char> oRangeState && string.Compare(oRangeState.StateName, strToken ) == 0) {
						oSongFactory( oRange );
						++iCount;
					}
				} catch( Exception oEx ) {
					Type[] rgErrors = { typeof( ArgumentOutOfRangeException ),
										typeof( NullReferenceException ) };
					if( !rgErrors.Contains( oEx.GetType() ) )
						throw;
				}
			}
			return( iCount > 0 );
		} // End method

		//public static bool FindMusic( this IEnumerable<ILineSelection> rgSelection, CreateEntry oSongFactory, ICollection<SongEntry> rgSongs, string strToken ) {
		//	if( rgSongs == null )
		//		throw new ArgumentNullException( "Need a container to hold songs." );
		//	if( rgSelection == null )
		//		throw new ArgumentNullException( "Selection object is null." );

		//	// BUG : The song might be outside of the selection on the left or right.
		//	//       I'll deal with that later.
		//	foreach( ILineSelection oSel in rgSelection ) {
		//		oSel.Line.FindSong( oSongFactory, rgSongs, strToken );
		//	}

		//	return( rgSongs.Count > 0 );
		//} // end method

		//public static bool FindMusic( this BaseEditor oEditor, Line oStart, CreateEntry oSongFactory, ICollection<SongEntry> rgSongs, string strToken ) {
		//	if( rgSongs == null )
		//		throw new ArgumentNullException( "Need a container to hold songs." );

		//	int iStartAt = ( oStart != null ) ? oStart.At : 0;

		//	foreach( Line oLine in oEditor ) {
		//		if( oLine.At >= oStart.At )
		//			oLine.FindSong( oSongFactory, rgSongs, strToken );
		//	}

		//	return( rgSongs.Count > 0 );
		//} // end method
	}

	public class SongEntry {
		Line	     _oLine;
		IMemoryRange _oElem;

		public SongEntry( Line oLine, IMemoryRange oElem ) {
			_oLine = oLine ?? throw new ArgumentNullException( "Line element must not be null!" );
			_oElem = oElem;
		}

		/// <summary>
		/// Return the raw entry as collected from the parser.
		/// </summary>
		/// <returns></returns>
		public override string ToString() {
			if( _oElem == null )
				return( _oLine.ToString() );

			return _oLine.SubString( _oElem.Offset, _oElem.Length );
		}

		public Line Line {
			get { return( _oLine ); }
		}

		public IMemoryRange Elem {
			get { return( _oElem ); }
		}
	}

}
