using System;
using System.Collections.Generic;
using System.Linq;

///<remarks>This file contains the objects that enable undo/redo.</remarks>
namespace Play.Edit {
    abstract class UndoBase : IUndoUnit
    {
        public virtual int Line {
            get {
                return( 0 );
            }
        }

        /// <summary>
        /// You must override this implementation.
        /// </summary>
        /// <exception cref="ApplicationException" >
        public virtual int Offset {
            get {
                return ( 0 );
            }
            set {
                throw new InvalidOperationException( "base class implemention" );
            }
        }

        /// <summary>
        /// Return the length of this text change. Not valid on all undo units
        /// </summary>
        /// <exception cref="InvalidOperationException" />
        public virtual int Length {
            get {
                return ( 0 );
            }
            set {
                throw ( new InvalidOperationException("Not expecting Length set on this class instance.") );
            }
        }

        public void Summate( int iCumulative ) 
        {
            throw new NotImplementedException();
        }

        public abstract UndoType Type {
            get;
        }

        public abstract void Do( Editor.Manipulator oBulk );

        public virtual int ColorIndex {
            get { return ( -4 ); }
        }
    }

    abstract class Undo : UndoBase
    {
        protected int _iOffset;
        protected int _iLine;

        public Undo( int iLine, int iOffset ) 
        {
            _iLine   = iLine;
            _iOffset = iOffset;
        }

        public override int Line {
            get { return ( _iLine ); }
        }

        public override int Offset {
            get {
                return ( _iOffset );
            }
            set {
                _iOffset = value;
            }
        }
    }

    /// <summary>
    /// Undo the effects of an insert of text on this line.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException" />
    class UndoInsert : Undo {
        int _iLength;

        public UndoInsert( int iLine, int iOffset, int iLength ) :
            base( iLine, iOffset )
        {
            if( iLength <= 0 )
                throw new ArgumentOutOfRangeException( "A insert length should not be less than zero!" );
            _iLength = iLength;
        }

        public override int Length {
            get {
                return ( _iLength );
            }
            set {
                _iLength = value;
            }
        }

        public override void Do( Editor.Manipulator oBulk ) {
            oBulk.LineTextDelete( _iLine, this );
        }

        public override UndoType Type {
            get { return( UndoType.Insert ); }
        }
    }

    class UndoDelete : Undo {
        string _strRemoved;

        public UndoDelete( int iLine, int iOffset, string strRemoved ) :
            base( iLine, iOffset )
        {
            _strRemoved = strRemoved;
        }

        public override void Do( Editor.Manipulator oBulk ) {
            oBulk.LineTextInsert( _iLine, Offset, _strRemoved, 0, _strRemoved.Length );
        }

        public override int Length {
            get {
                return ( _strRemoved.Length );
            }
        }

        public override UndoType Type {
            get { return( UndoType.Delete ); }
        }
    }

    class UndoMerge : Undo {
        public UndoMerge( int iLine, int iOffset ) :
            base( iLine, iOffset )
        {
        }

        public override void Do( Editor.Manipulator oBulk ) {
            oBulk.LineSplit( _iLine, this.Offset );
        }

        public override UndoType Type {
            get { return( UndoType.Merge ); }
        }
    }

    class UndoSplit : Undo {
        public UndoSplit( int iLine, int iOffset ) :
            base(iLine, iOffset) 
        {
        }

        public override void Do( Editor.Manipulator oBulk ) {
            oBulk.LineMergeWithNext( _iLine );
        }

        public override UndoType Type {
            get { return ( UndoType.Split ); }
        }
    }

    class UndoLineInsert : Undo {
        public UndoLineInsert( int iLine ) :
            base( iLine, 0 )
        {
        }

        public override void Do( Editor.Manipulator oBulk ) {
            oBulk.LineDelete(_iLine);
        }

        public override UndoType Type {
            get { return( UndoType.LineInsert ); }
        }
    }

    class UndoLineDelete : Undo {
        string _strText;

        public UndoLineDelete( int iLine, string strText ) :
            base( iLine, 0 )
        {
            _strText = strText;
        }

        public override void Do( Editor.Manipulator oBulk ) {
            oBulk.LineInsert( _iLine, _strText );
        }

        public override UndoType Type {
            get { return( UndoType.LineRemove ); }
        }
    }

    class UndoMaster : 
        UndoBase,
        IRefCount,
        IDisposable
    {
        Stack<IUndoUnit> _rgOldStack;
        BaseEditor       _oEditor;
        int              _iRefCount = 1;

        /// <summary>
        /// At this point this undo unit must be already be on the old stack.
        /// TODO: Use the new bulk editing "manipulator" class.
        /// </summary>
        /// <param name="oEditor">The editor who's undo stack we are swapping out.</param>
        /// <exception cref="ArgumentNullException" />
        /// <seealso cref="UndoMaster.Dispose()"/>
        public UndoMaster( BaseEditor oEditor ) 
        {
            if( oEditor == null )
                throw new ArgumentNullException( "Need that editor, duh!" );

            _oEditor    = oEditor;
            _rgOldStack = oEditor.UndoStacks[oEditor.UndoMode];

            oEditor.UndoStacks[oEditor.UndoMode] = new Stack<IUndoUnit>();
        }

        public override void Do( Editor.Manipulator oIgnore ) {
            UndoMaster  oUndoMaster = new UndoMaster( _oEditor );
            Editor.Manipulator oBulk       = new Editor.Manipulator( _oEditor );

            using( oBulk ) {
                while( _rgOldStack.Count > 0 )
                {
                    IUndoUnit oUnit = _rgOldStack.Pop();

                    try {
                        oUnit.Do( oBulk );
                    } catch( Exception oEx ) {
                        Type[] rgErrors = { typeof( InvalidOperationException ),
                                            typeof( ArgumentOutOfRangeException )
                                          };
                        _oEditor.Site.LogError( "Internal Error", "Problem with undo in master undo element." );
                        if( !rgErrors.Contains( oEx.GetType() ) ) {
                            throw new ApplicationException( "Problems in undo land." );
                        }
                    }
                }
            }
        }

        public override UndoType Type {
            get { return( UndoType.Master ); }
        }

        /// <summary>
        /// When you are finished with your sub atomic operations, call this
        /// method to signal that event.
        /// </summary>
        private void DoRelease() {
            Stack<IUndoUnit> rgTemp = _oEditor.UndoStacks[_oEditor.UndoMode];
            _oEditor.UndoStacks[_oEditor.UndoMode] = _rgOldStack;
            _rgOldStack = rgTemp;
            _oEditor.UndoPush(this);
        }
    
        public void AddRef(string strIdentifier )
        {
 	        _iRefCount++;
        }

        public int Release(string strIdentifier )
        {
            int iNewCount = --_iRefCount;

            if( iNewCount == 0 ) {
                DoRelease();
            }
            if( iNewCount < 0 ) {
                throw new InvalidOperationException( "One too many releases" );
            }

            return( iNewCount );
        }


        public void Dispose()
        {
            _oEditor.UndoMasterEnd();
        }
    }
}
