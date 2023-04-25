using System;
using System.Drawing;

using SkiaSharp;

using Play.Rectangles;

namespace Play.Edit {
	public class LayoutText2 : LayoutRect {
		public FTCacheLine Cache { get; }
		public int         ColumnIndex { get; }

		public LayoutText2( FTCacheLine oCache, LayoutRect.CSS eUnits, uint uiTrack, int iCol ) :
			base( eUnits, uiTrack, 1 ) 
		{
			Cache = oCache ?? throw new ArgumentNullException();
			ColumnIndex = iCol;
		}

		/// <summary>
		/// I need to rethink the implementation of this.
		/// BUG!! : The width can be floating point now!!
		/// </summary>
		/// <param name="eParentAxis"></param>
		/// <param name="uiRail"></param>
		public override uint TrackDesired(TRACK eParentAxis, int uiRail) {
			float flValue = 0;
            
            if( eParentAxis == TRACK.HORIZ ) {
                flValue = Cache.UnwrappedWidth + 10; // BUG: need to add a bit, else it wraps.
			} else {
 				Cache.OnChangeSize( uiRail );
				flValue = Cache.Height;
			}

			if( flValue < 0 )
				return 0;

			return (uint)flValue;
		}

        public void Paint( SKCanvas skCanvas, IPgStandardUI2 oStdUI ) {
            if( !Hidden ) { 
				Cache.Render( skCanvas, oStdUI, new PointF( Left, Top ) );
            }
        }

		public override void Invalidate() {
			Cache.Invalidate();
		}
	}
}
