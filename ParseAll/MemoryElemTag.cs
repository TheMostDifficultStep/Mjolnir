using Play.Interfaces.Embedding;

namespace Play.Parse.Impl.Text
{
    // Base class for holding all positions in the buffer. This object aggregates the behavior
    // of the state or terminal it contains.
    public class MemoryElemTag : 
        MemoryElem<TagInfo>
    {
        public MemoryElemTag( MemoryStateTag oParent, ProdElem<TagInfo> oElem ) :
            base( oElem, oParent ) {
        }

		/// <summary>
		/// BUG: This already looks implemented in MemoryElem base class.
		/// 11/24/2015 commented it out.
		/// </summary>
        //public override bool IsEqual(
        //        int                 p_iMaxStack,
        //        DataStream<TagInfo> p_oStream, 
        //        bool                p_fLookAhead, 
        //        int                 p_iPos, 
        //    out int                 p_iMatch, 
        //    out Production<TagInfo> p_oProd
        //) {
        //    bool fResult = m_oInst.IsEqual( p_iMaxStack, p_oStream, p_fLookAhead, p_iPos, 
        //                                    out p_iMatch, out p_oProd );
        //    if( fResult ) {
        //        m_iStart  = p_iPos;
        //        m_iLength = p_iMatch;
        //    }

        //    return( fResult );
        //}
    }

    public class MemoryTermTag : MemoryElemTag {
        public MemoryTermTag( MemoryStateTag oParent, ProdTermTag oElem ) :
            base( oParent, oElem )
        {
        }
    }

    public class MemoryEndTermTag : MemoryElemTag {
        public MemoryEndTermTag( MemoryStateTag oParent, ProdElem<TagInfo> oElem ) :
            base( oParent, oElem )
        {
        }
    }

    /// <summary>
    /// This terminal is a little different than the others. It is used for basic container
    /// ship without regard to the tagname. It binds to a tagname right at IsEqual() as long
    /// as the markuptype matches. It reports this name to the parent so the following
    /// end tag matches.
    /// </summary>
    public class MemoryStateTag : MemoryState<TagInfo> {
        //protected ProdState<TagInfo>   _oElemPState; // "interface" on _oElem (same instance as)
        //protected object[]       _rgValues;
        //protected State<TagInfo> _oState;

        public MemoryStateTag( MemoryStateTag oParent, ProdState<TagInfo> oElem ) :
            base( oElem, oParent )
        {
        }
    }

}
