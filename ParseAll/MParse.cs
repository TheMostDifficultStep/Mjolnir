﻿using System;
using System.Collections.Generic;
using System.Text;

using Play.Interfaces.Embedding;
using Play.Parse.Impl;

//[assembly:System.Reflection.AssemblyKeyNameAttribute("fooparse.fnn")]
// The file was created using "sn.exe -k fooparse.snk"
// use regasm.exe on the dll(assembly) we create to register for COM usage.
// use tlbexp.exe on the dll(assembly) we create to get at type library.
// use oleview.exe on the typelib to view it.
// use tlbimp srcedit.tlb /keyfile:srcedit_key.snk to generate assempbly 
// from tlb: srcedit.dll. Generate the extra snk file as above.
//   add a reference in the project system to that dll (assembly)
//   and now you can use COM interfaces in C# objects.
// To use it make sure the assembly is in the directory of the calling program.
// use the import like so: #import "file:typelib.tlb" no_namespace raw_interfaces_only 
//[assembly:System.Reflection.AssemblyKeyFileAttribute("fooparse.snk")]

namespace Play.Parse
{
    /// <summary>
    /// This is the base object of the all the parse non/terminals. I'd like this to be an
    /// Interface but classes have advantages and this seems to be flexible enough for me.
    /// </summary>
    /// <typeparam name="T">T is the type of elements in the parse stream that we can handle.</typeparam>
	public abstract class Nonterminal<T>
	{
		public abstract bool IsEqual( int iMaxStack, DataStream<T> p_oStream, bool p_fLookAhead, int p_iPos, 
                                      out int p_iMatch, out Production<T> p_oProd );
	}
	
    /// <summary>
    /// This interface allows you to interact with the parser
    /// as it does its work. 
    /// </summary>
    /// <typeparam name="T">T is the type of elements in the parse stream that we can handle.</typeparam>
	public interface IParseEvents<T> 
	{
        void OnMatch( ProdBase<T> p_oMemTerm, int p_lStart, int p_lLength);
		void OnParserError( ProdBase<T> p_oMemory, int p_iStart );
	}

    public abstract class AbstractState<T> : Nonterminal<T> {
        protected string m_strName = null;

        /// <summary>
        /// This is the name of the state. But it is not the "ID" which is the value when used in
        /// a production as a production element.
        /// </summary>
        public string Name {
            get { return m_strName; }
        }

        /// <summary>
        /// Return a collection of productions for this state. Each production is a
        /// sequence of terminals and non terminals.
        /// </summary>
        /// <remarks>ICollection is more elegant but I have to create an enumerator
        /// every time to walk the collection. I don't want to waste the memory.</remarks>
        public abstract Production<T>[] Productions
        {
            get;
        }
        
        // Normally a state would NOT want to return the number of matches since in the main stack
        // we did not distinguish between terminals and states as we call this function. However,
        // only terminals should advance the input position! We advance the position if a production
        // is NOT returned. Else, we should modify the main Parse() stacks so that only
        // terminal types may advance the input pointer.
        public override bool IsEqual(
                int           p_iMaxStack,  // When this reaches Zero we've got to stop.
                DataStream<T> p_oStream,
                bool          p_fLookAhead,
                int           p_iPos,
            out int           p_iMatch,
            out Production<T> p_oProd
        ) {
            p_iMatch = -1;   // The length of the longest match. (Empty matches 0)
            p_oProd  = null; 

            if( --p_iMaxStack < 0 ) // Blocks recursions that are getting too deep. 
                return( false );
            
            for( int i = 0; i< Productions.Length; ++i ) { 
                Production<T> oProd        = Productions[i];
                Production<T> oDummyProd   = null; // Don't return this one!!!
                int           iStreamMatch = 0;
                int           iLookCount   = p_fLookAhead ? oProd.Count : oProd.LookAhead;
                // Check each element in the production upto the LookAhead length.
                for (int jElem = 0; jElem < iLookCount; ++jElem) {
                    int  l_iRecognize = 0;
                    bool l_fLookAhead = p_fLookAhead;
                        
                    if( !l_fLookAhead )                    // Once true we never make it false again.
                        l_fLookAhead = jElem + 1 < iLookCount; // Only force look for children on this sibling if next sibling is a force.

                    if (oProd[jElem].IsEqual( p_iMaxStack, p_oStream, l_fLookAhead, p_iPos + iStreamMatch, out l_iRecognize, out oDummyProd) ) {
                        iStreamMatch += l_iRecognize;
                    } else {
                        iStreamMatch = -2; // Never a match if any part doesn't match. Must be one less than p_iMatch initial value.
                        break;
                    }
                }
                // When we've reached the end of the production bail out.
                // Else keep looking as long as we are looking at terminals.
                // If we hit a non terminal only proceed if we haven't finished looking ahead.
                if (iStreamMatch > p_iMatch) {
                    p_oProd  = oProd;
                    p_iMatch = iStreamMatch;
                }
            }
            return (p_oProd != null);
        } // End Method
    } // End Class

    // TODO: We can store the binder here and forget about the Bind() method on the
    //       ProdBase<T> interface!! But my wrists are sore so I'll do that some
    //       other day.
    public struct FMarker<U> {
        U   _oValue;

        public FMarker( U oValue ) {
            _oValue = oValue;
        }

        public override string ToString() {
            return( _oValue.ToString() );
        }

        public U Value {
            get { return( _oValue ); }
        }
    }

    public class MyStack<T> {
        List<FMarker<T>> _rgStack = new List<FMarker<T>>();
        int              _iTop = -1;
       
        public void Push( T oValue ) {
            if( oValue == null )
                throw new ArgumentNullException( "not expecting null on the stack" );
            _iTop++;
            if( _iTop >= _rgStack.Count )
                _rgStack.Add( new FMarker<T>( oValue ) );
            else
                _rgStack[_iTop] = new FMarker<T>( oValue );
        }

        public T Pop() {
            if( _iTop <= -1 )
                throw new InvalidOperationException( "The Stack is empty." );

            FMarker<T> sReturn = _rgStack[_iTop];
            _rgStack[_iTop] = default(FMarker<T>); // Zero out that old position.
            --_iTop;

            return( sReturn.Value );
        }

        public FMarker<T> At( int iIndex ) {
            return _rgStack[iIndex];
        }

        public int Count {
            get {
                return( _iTop + 1 );
            }
        }

        internal MyStackEnum GetEnumerator() {
            return( new MyStackEnum( this ) );
        }

        /// <summary>
        /// This enumerator does NOT follow the defined enumerator pattern for .net.
        /// Namely it remains valid even after the collection it enumerates has changed.
        /// But only if no changes occur during enumeration. Calling Reset() after a
        /// changes resets the enumerator.
        /// We iterate from the most recently inserted element to the first inserted.
        /// Current returns NULL of the stack is empty or if you call Current() before MoveNext.
        /// </summary>
        /// <remarks>Need to implement a callback so I can watch the stack when it changes.
        /// Since I use this pretty specifically I'm not going to bother right now.</remarks>
        internal class MyStackEnum {
            MyStack<T> _oStack;
            int        _iPos;
            FMarker<T> _oCurrent = default(FMarker<T>);

            public MyStackEnum( MyStack<T> oStack ) {
                _oStack = oStack;
                _iPos   = _oStack.Count;
            }

            public FMarker<T> Current {
                get {
                    return( _oCurrent );
                }
            }

            public bool MoveNext()
            {
                if( _iPos > 0 ) {
                    _iPos--;
                    _oCurrent = _oStack._rgStack[_iPos];
                    return( true );
                }
 	            return ( false );
            }

            public void Reset()
            {
 	            _iPos = _oStack.Count;
            }
        }
    }

    public delegate void OnProduction<T>( Production<T> p_oProd, int p_lStart ); 
    public delegate void OnParserException( Exception oEx, int lStart );

    /// <summary>
    /// An object to enumerate the parser. See the remarks for more info.
    /// Left handed grammers will make us loop forever! Be careful!
    /// </summary>
    /// <typeparam name="ST">The type of the elements in the stream we handle.</typeparam>
    /// <remarks>This object is basically an enumerator. I'll make it an actual
    /// enumerator some time. Current would be the last matched terminal.</remarks>
	public class ParseIterator<T> 
	{
        MyStack<ProdBase<T>>             _oStack;
        MyStack<ProdBase<T>>.MyStackEnum _oStackEnum;

		DataStream<T>   _oStream;
		int             _iInput;
		IParseEvents<T> _oParseEvents;

        public OnProduction<T>   ProductionEvent;
        public OnParserException ExceptionEvent;

		public ParseIterator( 
            DataStream<T>   p_oStream, 
            IParseEvents<T> p_oParseEvents,
            MemoryState<T>  p_oMemStart ) 
		{
			_oParseEvents = p_oParseEvents ?? throw new ArgumentNullException();
			_oStream      = p_oStream      ?? throw new ArgumentNullException();

            _iInput       = 0;
            _oStack       = new MyStack<ProdBase<T>>();
            _oStackEnum   = _oStack.GetEnumerator();

            if( p_oMemStart == null ) {
                _oParseEvents.OnParserError( null, 0 );
            } else {
                _oStack.Push( new MemoryBinder<T>( 0, p_oMemStart ) );
                _oStack.Push( p_oMemStart );
            }
        } // ParseIterator()

		/// <remarks>
		/// Looks handy, especially since I don't actually have a proper site. But
		/// don't think I'm actually using this! ^_^;;
		/// </remarks>
        protected void LogException( Exception oEx, int iInput ) {
			ExceptionEvent?.Invoke(oEx, iInput);
		}

        public int Current => _iInput;

        /// <summary>
        /// Attempt to bind a production element to a parent production state.
        /// </summary>
        /// <param name="iFrame"></param>
        /// <param name="oProdBase"></param>
        protected void Bind( ProdBase<T> oProdBase ) 
        {
            _oStackEnum.Reset();
            while( _oStackEnum.MoveNext() ) {
                try {
                    if( _oStackEnum.Current.Value.Bind( oProdBase ) )
                        break;
                } catch( NullReferenceException oEx ) {
                    LogException( oEx, _iInput );
                }
            }
        }

        /// <summary>
        /// 3/8/2017 : Now that memory elements are all created here we might be able to
        /// further improve by pushing memory elements creation to the document, so that
        /// they'll optimized for the document type. For example when the parser is
        /// not used with my editor! ^_^;;
        /// Note: I could potentially get rid of the ProdState cast if I allow prod elems
        /// to return a pointer to the state. But I really don't know if that saves any time.
        /// </summary>
        /// <param name="p_oProduction">New production to push onto the stack.</param>
        /// <param name="p_oParent">The state from which the production originates. This can be
        /// either a memorystate or a prodstate.</param>
        protected void Push( Production<T> p_oProduction, ProdBase<T> p_oParent )
        {
            try {
                if( !string.IsNullOrEmpty( p_oParent.ID ) || p_oParent.IsBinding || p_oParent.IsWord ) {
                    // Note : I don't even need the memorystate for a state if it's not bound or binding:
                    //        I save the start position in the memorybinder.
                    _oStack.Push( new MemoryBinder<T>( _iInput, p_oParent ) );
                }

                for( int iProdElem = p_oProduction.Count - 1; iProdElem >= 0; --iProdElem ) {
                    ProdBase<T> oMemElem = p_oProduction[iProdElem];

					if( oMemElem is ProdState<T> oProdState ) {
						// Don't push the memory binder at this point, if we do, we have to
						// wade thru inactive sibling binders by counting frames and etc. Waist of time.
						if (!string.IsNullOrEmpty(oProdState.ID) || oProdState.IsBinding || oProdState.IsWord) {
							oMemElem = new MemoryState<T>(oProdState, p_oParent as MemoryState<T>);
						}
					} else {
						// At present the listeners are just tossing these into the line color info.
						// And we might be capturing them as well (if ID is non-null).
						oMemElem = new MemoryTerminal<T>(p_oProduction[iProdElem], p_oParent as MemoryState<T>);
					}

					if( oMemElem == null ) {
                        StringBuilder sbError = new StringBuilder();

                        sbError.AppendLine( "Problem with production..." );
                        sbError.Append( "State: " );
                        sbError.AppendLine( p_oProduction.StateName );
                        sbError.Append( "Production #: " );
                        sbError.AppendLine( p_oProduction.Index.ToString() );
                        sbError.Append( "Element #: " );
                        sbError.Append( iProdElem.ToString() );
                        sbError.AppendLine( "." );

                        throw new InvalidOperationException( sbError.ToString() );
                    }

                    _oStack.Push( oMemElem );
                }
            } catch( Exception oEx ) {
                Type[] rgErrors = { typeof( ArgumentNullException ),
                                    typeof( NullReferenceException ),
                                    typeof( InvalidOperationException ) };
				if( rgErrors.IsUnhandled( oEx ) )
					throw;
                LogException( oEx, _iInput );
            }
        }

        /// <summary>
        /// Parse the next element.
        /// </summary>
        /// <returns>True, while there is more input to be parsed.</returns>
		public bool MoveNext() {
            try {
                if( _oStack.Count < 1 )
                    return( false );

                ProdBase<T>   oNonTerm    = _oStack.Pop(); 
                Production<T> oProduction = null;
		        int           iMatch      = 0;

		        if( oNonTerm.IsEqual( 30, _oStream, false, _iInput, out iMatch, out oProduction) ) {
                    if( !string.IsNullOrEmpty( oNonTerm.ID ) ) {
                        Bind( oNonTerm );
                    }
				    _oParseEvents.OnMatch( oNonTerm, _iInput, iMatch );

                    if( oProduction == null ) { // it's a terminal or a binder
					    _iInput += iMatch;
				    } else {                    // it's a state.
					    Push( oProduction, oNonTerm );
					    ProductionEvent?.Invoke( oProduction, _iInput );
				    }
                } else {
				    _oParseEvents.OnParserError( oNonTerm, _iInput );
					// This won't stop infinte loops on the (empty) terminal. But will stop other errors.
					if( !_oStream.InBounds( _iInput ) )
						return( false );
			    }
            } catch( Exception oEx ) {
                Type[] rgErrors = { typeof( ArgumentNullException ),     // MemoryEnd<T> can throw it on grammar bugs.
                                    typeof( InvalidOperationException ), // Push had problem with a production
                                    typeof( NullReferenceException ) };  // Usually this means one of the callbacks had a problem! ^_^
				if( rgErrors.IsUnhandled( oEx ) )
					throw;

                LogException( oEx, _iInput );
                return( false);
            }
			return( true );
        } // MoveNext()
    } // End Class ParseIterator


}
