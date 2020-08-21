using System;
using System.Collections.Generic;
using System.Text;

using Play.Interfaces.Embedding;

namespace Play.Parse.Impl.Text
{
    public class TagStream : 
        DataStream<TagInfo>, 
        IEnumerable<TagInfo> 
    {
        List<TagInfo> _rgTags;
        int           _iPos   = 0;

        public TagStream( List<TagInfo> rgTags ) {
            _rgTags = rgTags;
        }

        public override bool InBounds(int p_iPos) {
            return( p_iPos < _rgTags.Count );
        }

		public override int Position 
		{
			get {
                return( _iPos ); 
            }
			set { 
                if( value < 0 || value > _rgTags.Count )
                    throw new ArgumentOutOfRangeException();

                _iPos = value; 
            }
		}

        /// <summary>
        /// TODO: If the list is empty, we will have problems. Need to
        /// think about that.
        /// </summary>
        /// <param name="iPos">Element to look up.</param>
        /// <returns>The reference at the given position.</returns>
		public override TagInfo this [int iPos ] 
		{
			get{ 
                _iPos = iPos;
                try {
                    return( _rgTags[iPos] ); 
                } catch( ArgumentOutOfRangeException ) {
                }
                return( null );
            }
		}

		public override string SubString( int iPos, int iLen ) 
        {
            throw new NotImplementedException();
        }

        public IEnumerator<TagInfo> GetEnumerator()
        {
            foreach( TagInfo oTag in _rgTags ) {
                yield return( oTag );
            }
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return( GetEnumerator() );
        }
    }
}
