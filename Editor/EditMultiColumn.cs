using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Xml;

using Play.Parse;
using Play.Parse.Impl;
using Play.Interfaces.Embedding;

namespace Play.Edit {
    public interface IPgRowEvents {
        void OnRowEvent( BUFFEREVENTS eEvent, IEnumerable<Row> oEnum ); // Multi line events.
        void OnRowEvent( BUFFEREVENTS eEvent, Row oRow );               // Single Line events.
        void OnRowEvent( BUFFEREVENTS eEvent );                         // Every Line events.
    }

    /// <summary>
    /// This multi column document does not behave like a normal text stream
    /// document. Let's see how this goes... Also, the expectation is to
    /// have the load/save methods in a subclass. 
    /// </summary>
    /// <remarks>
    /// This is the start of a new document type. I'm no longer trying
    /// to extend my basic editor. While the former can allow multiple columns,
    /// only one is the text stream. The rest are line numbers, check boxes
    /// and things that are not selectable.
    /// </remarks>
    public abstract class EditMultiColumn :
        IPgParent,
        IEnumerable<Row>,
        IReadableBag<Row>,
        IDisposable 
    {
        protected IPgBaseSite _oSiteBase;
        protected List<Row>   _rgRows;
        protected Func<Row>   _fnRowCreator;
        protected List<bool>  _rgColumnWR; // is the column editable...
        public List<IPgRowEvents> EventCallbacks {get;} = new List<IPgRowEvents>();

        public IPgParent Parentage => _oSiteBase.Host;
        public IPgParent Services  => Parentage;

        public virtual bool IsDirty => false;

        public int ElementCount => _rgRows.Count;

        public Row this[int iIndex] => _rgRows[iIndex];

        public virtual void Dispose() {
            EventCallbacks.Clear();
        }

        protected void Raise_MultiRowEvent( BUFFEREVENTS eEvent, IEnumerable<Row> oEnum ) {
            foreach( IPgRowEvents oEvent in EventCallbacks ) {
                oEvent.OnRowEvent( eEvent, oEnum );
            }
        }

        protected void Raise_SinglRowEvent( BUFFEREVENTS eEvent, Row oRow ) {
            foreach( IPgRowEvents oEvent in EventCallbacks ) {
                oEvent.OnRowEvent( eEvent, oRow );
            }
        }

        protected void Raise_EveryRowEvent( BUFFEREVENTS eEvent ) {
            foreach( IPgRowEvents oEvent in EventCallbacks ) {
                oEvent.OnRowEvent( eEvent );
            }
        }
        public IEnumerator<Row> GetEnumerator() {
            return _rgRows.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() {
            return GetEnumerator();
        }

        public virtual void LogError( string strMessage ) { 
            _oSiteBase.LogError( "Multi Column Editor", strMessage );
        }

        public void RenumberRows() {
            for( int i=0; i< _rgRows.Count; i++ ) {
                _rgRows[i].At = i;
            }
        }
    }

}
