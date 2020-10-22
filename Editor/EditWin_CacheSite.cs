using System;
using System.Collections.Generic;
using System.Linq;

using Play.Interfaces.Embedding;
using Play.Parse.Impl;

namespace Play.Edit {
	public partial class EditWin {
		class CacheManSlot : CacheManagerAbstractSite {
			readonly EditWin _oHost;

			public CacheManSlot( EditWin oHost ) {
				_oHost = oHost ?? throw new ArgumentNullException( "Cache Manager needs a Edit Window as host." );
			}

			public override bool IsWrapped( int iLine ) {
				return( _oHost.Wrap );
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

			/// <summary>
			/// Just remember that the font is probably selected into the DC at this point.
			/// But I'm not going to make that a requirement.
			/// </summary>
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
			protected void NeighborhoodOfScroll( out Line oLine, out int iOffs ) {
				int iStreamOffset = 0;
				int iLine         = 0;

				oLine = null;
				iOffs = 0;

				try {
					iStreamOffset = (int)(_oHost._oDocument.Size * _oHost._oScrollBarVirt.Progress);
					iLine         = _oHost._oDocument.BinarySearchForLine( iStreamOffset );

					oLine = GetLine(iLine);

					if( oLine != null )
						iOffs = iStreamOffset - oLine.CumulativeLength; 

					if( iOffs < 0 )
						iOffs = 0;
				} catch ( Exception oEx ) {
					Type[] rgErrors = { typeof( NullReferenceException ),
										typeof( IndexOutOfRangeException ) };
					LogError( "Scrolling Problem", "Problem determining NeighborhoodOfScroll" );
					if( !rgErrors.Contains( oEx.GetType() ) )
						throw new InvalidProgramException( "trouble scrolling", oEx );
				}
			}

			protected void NeighborhoodOfCaret( out Line oLine, out int iOffs ) {
				try {
					oLine = GetLine( _oHost._oCaretPos.Line.At );
					iOffs = _oHost._oCaretPos.Offset; 

					if( iOffs < 0 )
						iOffs = 0;
				} catch( NullReferenceException ) {
					oLine = null;
					iOffs = 0; 

					LogError( "Scrolling Problem", "I crashed while trying to locate the caret. You are at the start of the document." );
				}
			}

			/// <summary>
			/// Return the line offset in the requested neighborhood.
			/// </summary>
			/// <remarks>Remember this function must not return the dummy line if editor is empty.</remarks>
			public override void Neighborhood( RefreshNeighborhood eHood, out Line oLine, out int iOffs ) {
				oLine = null;
				iOffs = 0;

				switch( eHood ) {
					case RefreshNeighborhood.SCROLL:
						NeighborhoodOfScroll( out oLine, out iOffs );
						break;
					case RefreshNeighborhood.CARET:
						NeighborhoodOfCaret( out oLine, out iOffs );
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
			/// <returns>Line if in bounds. null of not. This is important, never pass the a dummy line back.</returns>
			public override Line GetLine( int iLineAt ) {
				if( _oHost._oDocument == null )
					return( null );
				if( !_oHost._oDocument.IsHit( iLineAt ) )
					return( null );

				// Remember GetLine now always returns something. So we need to check
				// if the index is a hit first and return null if not!!
				return( _oHost._oDocument.GetLine( iLineAt ) );
			}

			public override void WordBreak( Line oLine, ICollection<IPgWordRange> rgWords ) {
				_oHost.WordBreak( oLine, rgWords );
			}
		} // end class CacheManSlot
	}

	public partial class EditWindow2 {
		/// <summary>
		/// There is only one Cache manager slot per edit window.
		/// </summary>
		class CacheManSlot : CacheManagerAbstractSite {
			readonly EditWindow2 _oHost;

			public CacheManSlot( EditWindow2 oHost ) {
				_oHost = oHost ?? throw new ArgumentNullException( "Cache Manager needs a Edit Window as host." );
			}

			public override bool IsWrapped( int iLine ) {
				return( _oHost.Wrap );
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

			/// <summary>
			/// Just remember that the font is probably selected into the DC at this point.
			/// But I'm not going to make that a requirement.
			/// </summary>
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
			protected void NeighborhoodOfScroll( out Line oLine, out int iOffs ) {
				int iStreamOffset = 0;
				int iLine         = 0;

				oLine = null;
				iOffs = 0;

				try {
					iStreamOffset = (int)(_oHost._oDocument.Size * _oHost._oScrollBarVirt.Progress);
					iLine         = _oHost._oDocument.BinarySearchForLine( iStreamOffset );

					oLine = GetLine(iLine);

					if( oLine != null )
						iOffs = iStreamOffset - oLine.CumulativeLength; 

					if( iOffs < 0 )
						iOffs = 0;
				} catch ( Exception oEx ) {
					Type[] rgErrors = { typeof( NullReferenceException ),
										typeof( IndexOutOfRangeException ) };
					if( rgErrors.IsUnhandled( oEx ) )
						throw;

					LogError( "Scrolling Problem", "Problem determining NeighborhoodOfScroll" );
				}
			}

			protected void NeighborhoodOfCaret( out Line oLine, out int iOffs ) {
				try {
					oLine = GetLine( _oHost.CaretPos.Line.At );
					iOffs = _oHost.CaretPos.Offset; 

					if( iOffs < 0 )
						iOffs = 0;
				} catch( NullReferenceException ) {
					oLine = null;
					iOffs = 0; 

					LogError( "Scrolling Problem", "I crashed while trying to locate the caret. You are at the start of the document." );
				}
			}

			/// <summary>
			/// Return the line offset in the requested neighborhood.
			/// </summary>
			/// <remarks>Remember this function must not return the dummy line if editor is empty.</remarks>
			public override void Neighborhood( RefreshNeighborhood eHood, out Line oLine, out int iOffs ) {
				oLine = null;
				iOffs = 0;

				switch( eHood ) {
					case RefreshNeighborhood.SCROLL:
						NeighborhoodOfScroll( out oLine, out iOffs );
						break;
					case RefreshNeighborhood.CARET:
						NeighborhoodOfCaret( out oLine, out iOffs );
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
						return( null );

					return( _oHost._oDocument.GetLine( iLineAt ) );
				} catch( NullReferenceException ) {
					return null;
				}
			}

			public override void WordBreak( Line oLine, ICollection<IPgWordRange> rgWords ) {
				_oHost.WordBreak( oLine, rgWords );
			}

		} // end class
	}

}
