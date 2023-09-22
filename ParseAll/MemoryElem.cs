using System;
using System.Collections;
using System.Collections.Generic;

using Play.Interfaces.Embedding;

namespace Play.Parse.Impl
{
    public delegate void OnParseFinish<T>( MemoryState<T> oStart, DataStream<T> oStream );

    /// <summary>
    /// Base class for holding all positions in the buffer. This object aggregates the behavior
    /// of the state or terminal it contains. When a ProdElem succeeds IsEqual() this is the
    /// object that stores the stream offset when it was found.
    /// Used to save the entire parse tree but no longer. 
    /// And I would love NOT to inherit from ProdBase. But he binding system is using the
    /// parse stack. I'll have to give it some thought.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public abstract class MemoryElem<T> : 
        ProdBase<T>,
        IPgWordRange // IColorRange
    {
      //protected MemoryState<T> m_oParent; // 3/7/2017 I don't really want to build the entire tree.
        protected int m_iStart  = -1;
        protected int m_iLength =  0;
        protected int m_iOffs   = -1;
        protected int m_iColor  =  0;

        public ProdElem<T> _oProdElem = null; // Production Element we are an 'instance' of.

        /// <summary>
        /// Base implementation of all memory elements. Because we throw these straight into our
        /// line buffer we're more complicated than need be. I'll look into that in the future.
        /// </summary>
        /// <param name="p_oInst">An element from a production.</param>
        /// <param name="p_oParent">Might be null. We don't build resulting tree anymore.</param>
        protected MemoryElem(ProdElem<T> p_oInst, MemoryState<T> p_oParent) {
			_oProdElem = p_oInst ?? throw new ArgumentNullException();
        }

		/// <summary>
		/// By default prodbase<> is not a word. So we need to check the production instance we come from.
		/// </summary>
		public override bool IsWord    => _oProdElem.IsWord;
		public override bool IsVisible => _oProdElem.IsVisible;
		public override bool IsTerm    => _oProdElem.IsTerm;

		/// <summary>
		/// Save the position for a terminal when matched.
		/// </summary>
		public override bool IsEqual(
                int           p_iMaxStack,
                DataStream<T> p_oStream,
                bool          p_fLookAhead, 
                int           p_iPos,
            out int           p_iMatch,
            out Production<T> p_oProd
        ) {
            bool l_bRet = _oProdElem.IsEqual( p_iMaxStack, p_oStream, p_fLookAhead, p_iPos, out p_iMatch, out p_oProd );

            m_iStart = p_iPos; // Always record this position so we know where the error occurred.

            if( l_bRet ) {
                m_iLength = p_iMatch;
            }
            return ( l_bRet );
        }

        public override string ToString()
        {
 	         return _oProdElem.ToString();
        }

        public override string ID {
            get { return( _oProdElem.ID ); }
        }

        public virtual string StateName => string.Empty;

        /// <summary>
        /// BUG: Looking this over, the "Start" property is not really used once parsing is over.
        /// Technically I don't need to correct this value once the user begins editing the file.
        /// </summary>
        /// <param name="iCumulative"></param>
        public void Summate( int iCumulative ) {
            m_iStart = iCumulative + m_iOffs;
        }

		/// <summary>
		/// While this one is absolutely required while parsing, (stream based parsing),
		/// it's not after, since the colorization happens on a line by line basis.
		/// It would be great if we could "turn this off" after parsing is finished so 
		/// we don't need the "summate" call. Or only use on an "as needed" basis.
		/// </summary>
		/// <remarks>Maybe I should set this value negative when I put this element in
		/// the line formatting and stop calling summate(). That will save a lot of effort
		/// </remarks>
        virtual public int Start {
            get {
                return (m_iStart);
            }
        }

        /// <summary>
        /// Last I checked the "End" character is the first character following
        /// the range of characters in this element. This "End" character is
        /// outside of the selection.
        /// </summary>
        virtual public int End {
            get {
                return ( m_iStart + m_iLength ); 
            }
        }

        public int Offset {
            get {
                return (m_iOffs);
            }
            
            set {
                if( value <= 0 )
                    value = 0;

                m_iOffs  = value;
            }
        }

        public int Length {
            get {
                return ( m_iLength );
            }
            
            set {
                if( value <= 0 )
                    value = 0;
                    
                m_iLength = value;
            }
        }

        // Never try to figure out an error based on the values of the start and end,
        // I keep messing around with those. Just ask here.
        public bool IsError {
            get {
                return (m_iStart < 0);
            }
        }

        public virtual ProdElem<T> ProdElem {
            get {
                return (_oProdElem);
            }
        } 

        public virtual string DisplayName( DataStream<T> p_oStream ) {
            return (_oProdElem.ToString());
        }
        
        public virtual int ColorIndex 
        {
            get {
                return( m_iColor );
            }
            set {
                m_iColor = value;
            }
        }
    } // end class 

    public class MemoryTerminal<T> : MemoryElem<T> {
        public MemoryTerminal(ProdElem<T> p_oInst, MemoryState<T> p_oParent) :
            base(p_oInst, p_oParent) 
        {
            // States can have null parents. We've got to end the tree somewhere. However,
            // terminals must always have a parent.
        } // MemoryTerminal

        public override string DisplayName(DataStream<T> p_oStream) {
            if( p_oStream != null ) {
                if( IsError )
                    return "Error at Pos: " + m_iStart.ToString() + ", Attempt: " + _oProdElem.ToString();

                string strOut = p_oStream.SubString(m_iStart, Length);

                if (strOut.Length == 0)
                    return ("empty");

                return "'" + strOut + "'";
            }
            return _oProdElem.ToString();
        }
    } // end class

    public class MemoryState<T> : MemoryElem<T> {
        protected State<T> _oState;   // The state for which we are storing instance data.
        protected object[] _rgValues; // the values to store, some of which may be arrays. 
                                      
        /// <summary>
        /// A memory state should always come from a production elem instance. We create
        /// a dummy production elem instance for the start state so that m_oInst is always
        /// true. However, just because we have a production elem instance DOES NOT MEAN
        /// we have a variable defined to receive our value.
        /// </summary>
        /// <param name="p_oProdState"></param>
        /// <param name="p_oParent"></param>
        public MemoryState(ProdState<T> p_oProdState, MemoryState<T> p_oParent) :
            base( p_oProdState, p_oParent ) 
        {
            if( p_oProdState == null )
                throw ( new ArgumentNullException() );

            _oState = p_oProdState.Class;
            if( _oState == null )
                throw ( new ArgumentNullException( "Production Instance Class is null." ) );

            // Check how many bindings the state has and create a array 
            // to hold the elements as they are parsed.
            if( _oState.IsBinding )
                _rgValues = new object[_oState.Bindings.Count];
            else
                _rgValues = null;

            // Look at the local bindings and see which ones are arrays
            // and initialize the value with an array list.
            for( int iIndex = 0; iIndex < _oState.Bindings.Count; ++iIndex ) {
                if( _oState.Bindings.Values[iIndex].IsArray )
                    _rgValues[iIndex] = new ArrayList();
                else
                    _rgValues[iIndex] = null;
            }
        }

        /// <summary>
        /// Find a binding that we can use as a representative of this node. For
        /// example if we have a TAG state we want to use the TAGNAME as the
        /// representative.
        /// </summary>
        /// <returns></returns>
        public override string DisplayName(DataStream<T> p_oStream) {
            for( int i=0; i< _oState.Bindings.Count; ++i ) {
                BindingInfo oDecl = _oState.Bindings.Values[i];
                
                // use the first binding that is flagged as a display representative.
                if( oDecl.IsDisplay ) {
                    MemoryElem<T> l_oMem = _rgValues[i] as MemoryElem<T>;
                    if( l_oMem != null ) {
                        return ( l_oMem.DisplayName( p_oStream ) );
                    }
                }
            }
            return ( p_oStream.SubString( Start, Length ) );
            // return (base.DisplayName( p_oStream));
        }

        /// <summary>
        /// public member for binding children. This entry point is called from the parser stack.
        /// </summary>
        /// <param name="p_oProdChild">Some child element that needs to be bound.</param>
        public override bool Bind( ProdBase<T> p_oProdChild ) {
            MemoryElem<T> l_oMemChild = p_oProdChild as MemoryElem<T>;
            if( l_oMemChild != null ) {
                int  iIndex   = IndexOfBinding( p_oProdChild.ID );
                bool fIsBound = iIndex > -1;
                
                if( fIsBound ) {
                    BindingInfo oDecl = _oState.Bindings.Values[iIndex];
                    // Since not all terms (or states) have an "id" this isn't as good as
                    // some sort of top down approach for assigning the color index...
                    l_oMemChild.ColorIndex = oDecl.ColorIndex;

                    if (oDecl.IsArray) {
                        ArrayList rgValues = (ArrayList)_rgValues[iIndex];
                        if( rgValues != null ) {
                            rgValues.Add( l_oMemChild );
                        } else {
                            return( false );
                        }
                    } else {
                        _rgValues[iIndex] = l_oMemChild;
                    }
                    return( true );
                }
            }
            return (false); // Since not tracking up thru heirarchy just return true. Using stack now.
        }
        
        /// <summary>
        /// We stick a MemoryEnd Terminal in the list so that when the production ends
        /// we can tell the originating class (MemoryState) where it ended. It calls this 
        /// method.
        /// </summary>
        /// <param name="oRange">The ending element in the production</param>
        public override void SetEnd( int iEnd )
        {
            int iLength = iEnd - m_iStart;
            
            if( iLength < 0 )
                m_iLength = 0;

            m_iLength = iLength;
        }

        public override string StateName {
            get {
                return( _oState.Name );
            }
        }

        public State<T> Class {
            get {
                return (_oState);
            }
        }

        // This is a state that binds sub members.
        public override bool IsBinding {
            get { return _oState.IsBinding; }
        }

        public int IndexOfBinding( string strBinding ) {
            return _oState.Bindings.IndexOfKey( strBinding );
        }

        /// <summary>
        /// Get the value of a binding in a one-off fashion. If you're
        /// making multiple calls to a particular binding. It's better
        /// to get the binding index from the grammmer and use the
        /// int value of the binding.
        /// </summary>
        /// <param name="strBinding">The string name of the binding you
        /// to retrieve a singleton from.</param>
        /// <returns></returns>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        /// <exception cref="InvalidCastException"></exception>
        public MemoryElem<T> GetValue( string strBinding ) {
            int iBinding = Class.Bindings.Keys.IndexOf( strBinding );

            return GetValue( iBinding );
        }

        /// <param name="iBinding">Which binding you wish to access.</param>
        /// <param name="iIndex">Index of binding if an array.</param>
        /// <returns></returns>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        /// <exception cref="InvalidCastException"></exception>
        /// <exception cref="NullReferenceException" ></exception>
        public MemoryElem<T> GetValue( int iBinding, int iIndex = 0 ) {
            if( _oState.Bindings.Values[iBinding].IsArray ) {
                if( _rgValues == null )
                    throw new NullReferenceException( "No binding for request" );
                ArrayList rgList = (ArrayList)_rgValues[iBinding];

                return (MemoryElem<T>)rgList[iIndex];
            }

            return (MemoryElem<T>)_rgValues[iBinding];
        }

        protected struct BindingsEnumerable : IEnumerable<MemoryElem<T>> {
            readonly MemoryState<T> _oHost;
            readonly int            _iBinding;

            public BindingsEnumerable( MemoryState<T> oHost, int iBinding ) {
                _oHost = oHost ?? throw new ArgumentNullException( );
                _iBinding = iBinding;
            }

            public IEnumerator<MemoryElem<T>> GetEnumerator() {
                ArrayList rgValues = (ArrayList)_oHost._rgValues[_iBinding];
                for( int i=0; i< rgValues.Count; ++i ) {
                    yield return (MemoryElem<T>)rgValues[i];
                }
            }

            IEnumerator IEnumerable.GetEnumerator() {
                return GetEnumerator();
            }
        }

        public IEnumerable<MemoryElem<T>> EnumValues( int iBinding ) {
            if( !_oState.Bindings.Values[iBinding].IsArray )
                throw new ArgumentException( "The Binding is not an Array");

            return new BindingsEnumerable( this, iBinding );
        }

    } // End class 

    /// <summary>
    /// memory element end marker instance. Use this element after a recursive element in a
    /// production so that you can pick up it's end point in the stream. 
    /// </summary>
    public class MemoryBinder<T> : 
        ProdBase<T>
    {
        public    ProdBase<T> Target { get; }
        protected int         _iStart;
        protected int         _iEnd;

        public MemoryBinder( int iStart, ProdBase<T> p_oTarget) {
            if( p_oTarget == null )
                throw new ArgumentNullException( "Parent must not be null for the ElemEnd<T> terminal." );

            _iStart  = iStart;
            _iEnd    = iStart;
            Target   = p_oTarget;
        }

        /// <summary>
        /// Production end markers have no name. If they had an ID they the'll go thru
        /// the binding system, which is undesirable.
        /// </summary>
        public override string ID {
            get { return( string.Empty ); }
        }

        public override string ToString()
        {
            return ("Capture for: " + Target.ToString() );
        }

        public override bool IsBinding {
            get {
                return( Target.IsBinding );
            }
        }

		/// <summary>
		/// This is a weird one. Technically it is a terminal. But not for any of the reasons I'd
		/// be interested in this as a terminal. So I'm going to return false.
		/// </summary>
		public override bool IsTerm {
			get { return( false ); }
		}

        public override bool Bind( ProdBase<T> p_oChild ) {
            return( Target.Bind( p_oChild ) );
        }

        /// <summary>
        /// The element has at last drifted to the top of the stack when this get's called.
        /// Always return true. Saves the position to the target of the sentinal.
        /// </summary>
        /// <param name="p_iMatch">0 Always.</param>
        /// <param name="p_oProd">null Always.</param>
        /// <returns>true always.</returns>
        /// <remarks>Always going to have a problem at the end of file, since the sibling
        ///          will have consumed all available data we'll be out of bounds!</remarks>
        public override bool IsEqual(
            int               p_iMaxStack,
            DataStream<T>     p_oText, 
            bool              p_fLookAhead, 
            int               p_iPos, 
            out int           p_iMatch, 
            out Production<T> p_oProd )
        {
            p_oProd  = null;
            p_iMatch = 0;

            // At this time the current position is one past the last stream item captured
            // by the production in the state.
            Target.SetEnd( p_iPos );

            // It's legal for us to be be past the end of the stream since we are a null char.
            return( p_oText.InBounds( p_iPos - 1 ) );
        }

        public int Start {
            get {
                return ( _iStart );
            }
        }

        /// <summary>
        /// Last I checked the "End" character is the first character following
        /// the range of characters in this element. This "End" character is
        /// outside of the selection.
        /// </summary>
        public int End {
            get {
                return ( _iEnd ); 
            }
        }

    } // End Class
    public class OutlineBinder<T> : 
        ProdBase<T>
    {
        public    ProdState<T> Target { get; }
        protected int      _iStart;
        protected int      _iEnd;

        public OutlineBinder( int iStart, ProdState<T> p_oTarget) {
            if( p_oTarget == null )
                throw new ArgumentNullException( "Target must not be null." );

            _iStart = iStart;
            _iEnd   = iStart;
            Target  = p_oTarget;
        }

        /// <summary>
        /// Production end markers have no name. If they had an ID they the'll go thru
        /// the binding system, which is undesirable.
        /// </summary>
        public override string ID {
            get { return string.Empty; }
        }

        public override string ToString()
        { 
            return "Outline for: " + Target.Class.Name;
        }

        public override bool IsBinding {
            get {
                return true;
            }
        }

		/// <summary>
		/// This is a weird one. Technically it is a terminal. But not for any of the reasons I'd
		/// be interested in this as a terminal. So I'm going to return false.
		/// </summary>
		public override bool IsTerm {
			get { return false; }
		}

        /// <summary>
        /// Override this call and insert the child into the tree.
        /// </summary>
        /// <param name="p_oChild"></param>
        /// <returns></returns>
        public override bool Bind( ProdBase<T> p_oChild ) {
            bool fBind = p_oChild is ProdState<T> oChildState && oChildState == Target;
            return fBind;
        }

        /// <summary>
        /// The element has at last drifted to the top of the stack when this get's called.
        /// Always return true. Saves the position to the target of the sentinal.
        /// </summary>
        /// <param name="p_iMatch">0 Always.</param>
        /// <param name="p_oProd">null Always.</param>
        /// <returns>true always.</returns>
        /// <remarks>Always going to have a problem at the end of file, since the sibling
        ///          will have consumed all available data we'll be out of bounds!</remarks>
        public override bool IsEqual(
            int               p_iMaxStack,
            DataStream<T>     p_oText, 
            bool              p_fLookAhead, 
            int               p_iPos, 
            out int           p_iMatch, 
            out Production<T> p_oProd )
        {
            p_oProd  = null;
            p_iMatch = 0;

            // At this time the current position is one past the last stream item captured
            // by the production in the state.
            _iEnd = p_iPos;

            // It's legal for us to be be past the end of the stream since we are a null char.
            return p_oText.InBounds( p_iPos - 1 );
        }

        public int Start => _iStart;

        /// <summary>
        /// Last I checked the "End" character is the first character following
        /// the range of characters in this element. This "End" character is
        /// outside of the selection.
        /// </summary>
        public int End => _iEnd; 
    }
}