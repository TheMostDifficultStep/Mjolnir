using System;
using System.Collections.Generic;

using Play.Interfaces.Embedding;

namespace Play.Edit {
	public partial class EditWindow2 {
		/// <summary>
		/// There is only one Cache manager slot per edit window.
		/// </summary>
		protected class CacheManSlot : CacheManagerAbstractSite {
			readonly EditWindow2 _oHost;

			public CacheManSlot( EditWindow2 oHost ) {
				_oHost = oHost ?? throw new ArgumentNullException( "Cache Manager needs a Edit Window as host." );
			}

			/// <remarks>
			/// This used to be on a per line basis, but after all these years
			/// it's pretty much an all or nothing kind of thing. TODO: I should just
			/// remove this and set it as a property of the CacheManager..
			/// </remarks>
			public override bool IsWrapped() {
				return _oHost.Wrap;
			}

			public override IPgParent Host => _oHost;

			public override void LogError(string strMessage, string strDetails, bool fShow=true) {
				_oHost.LogError( strMessage, strDetails );
			}

			/// <summary>
			/// At present nothing calls this. But if the cache calls this, trigger a
			/// repaint. Tho I expect I should refresh the buffer too. But so far it's not needed.
			/// </summary>
			public override void Notify( ShellNotify eEvent ) {
				switch( eEvent ) {
					case ShellNotify.DocumentDirty:
						_oHost.Invalidate();
						break;
				}
			}

			public override void OnRefreshComplete()
			{
				_oHost.ScrollBarRefresh();
				_oHost.CaretIconRefreshLocation();

				_oHost.Invalidate();
			}

			/// <summary>
			/// Based on the current scroll bar progress percentage compute what line
			/// and offset we will be on. 
			/// </summary>
			/// <param name="oLine">The line we are on.</param>
			/// <param name="iOffs">Offset into the line.</param>
			/// <remarks>I currently haven't implemented the offset portion.</remarks>
			protected void NeighborhoodOfScroll( out Line oLine ) {
				oLine = null;

				try {
					int iStreamOffset = (int)(_oHost._oDocument.Size * _oHost._oScrollBarVirt.Progress);
					int iLine         = _oHost._oDocument.BinarySearchForLine( iStreamOffset );

					oLine = GetLine(iLine);
				} catch ( Exception oEx ) {
					Type[] rgErrors = { typeof( NullReferenceException ),
										typeof( IndexOutOfRangeException ) };
					if( rgErrors.IsUnhandled( oEx ) )
						throw;

					LogError( "Scrolling Problem", "Problem determining NeighborhoodOfScroll" );
				}
			}

			protected void NeighborhoodOfCaret( out Line oLine ) {
				try {
					oLine = GetLine( _oHost.CaretPos.At );
				} catch( NullReferenceException ) {
					oLine = null;
					LogError( "Scrolling Problem", "I crashed while trying to locate the caret. You are at the start of the document." );
				}
			}

			/// <summary>
			/// Return the line in the requested neighborhood.
			/// </summary>
			/// <remarks>Remember this function must not return 
			/// the dummy line if editor is empty.</remarks>
			public override void Neighborhood( RefreshNeighborhood eHood, out Line oLine ) {
				oLine = null;

				switch( eHood ) {
					case RefreshNeighborhood.SCROLL:
						NeighborhoodOfScroll( out oLine );
						break;
					case RefreshNeighborhood.CARET:
						NeighborhoodOfCaret( out oLine );
						break;
				}
			}

			public override ICollection<ILineSelection> Selections {
				get {
					return( _oHost._rgSelectionTypes );
				}
			}

			/// <summary>
			/// Get the requested line.
			/// </summary>
			/// <remarks>
			/// I'm trying to insulate internal knowledge of the document structure
			/// from the cache manager if at all possible. This kind of accessor on a
			/// site can be efficient since we can assume locality of reference, if need be,
			/// in the event that we change the document from an array implementation.
			/// </remarks>
			public override Line GetLine( int iLineAt ) {
				try {
					if( !_oHost._oDocument.IsHit( iLineAt ) )
						return null;

					return _oHost._oDocument.GetLine( iLineAt );
				} catch( NullReferenceException ) {
					return null;
				}
			}
		} // end class
	}

}
