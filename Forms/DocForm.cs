using System;
using System.Collections.Generic;
using System.Collections;

using SkiaSharp;

using Play.Interfaces.Embedding;
using Play.Edit;
using Play.Parse;


namespace Play.Forms {
    /// Form windows will listen to these events.
    /// </summary>
    public interface IPgFormEvents {
        void OnFormUpdate( IEnumerable<Line> rgUpdates ); // Line contents have been updated. remeasure
        void OnFormFormat( IEnumerable<Line> rgUpdates ); // Line formatting has been changed. repaint
        void OnFormClear(); // All properties/labels are to be removed.
        void OnFormLoad (); // All properties/labels have been created.
    }

    public class SimpleCacheCaret : IPgCacheCaret  {
        protected int              _iOffset = 0;
        protected LayoutSingleLine _oLayout;
        public    int              Advance { get; set; }

        public SimpleCacheCaret( LayoutSingleLine oLayout ) {
            _oLayout  = oLayout;
            _iOffset = 0;
        }

		public int ColorIndex {
			get { return( 0 ); }
		}

        public override string ToString() {
            return( "(" + _iOffset.ToString() + "...) " + Line.SubString( 0, 50 ) );
        }

        public int At {
            get { return Line.At; }
        }

        public int Offset {
            get { return _iOffset; }
            
            set {
                if( value > Line.ElementCount )
                    value = Line.ElementCount;
                if( value <= 0 )
                    value = 0;

                _iOffset = value;
            }
        }
        
        public int Length {
            get { return 0; }
            set { throw new ArgumentOutOfRangeException("Caret length is always zero" ); }
        }
        
        public Line Line {
            get { return Layout.Cache.Line; }
            set {
                if( value != Layout.Cache.Line )
                    throw new ApplicationException();
            }
        }

        /// <summary>
        /// Caret's always point to a single line, set that line here. It's a little
        /// weird that we're pointing to the property layout and not the cache element
        /// directly. Might want to revisit that. Turns out we can get a line set
        /// by a editor that has had all it's elements cleared. 
        /// </summary>
        public LayoutSingleLine Layout {
            get { return _oLayout; }
            // If the line is read only on the FTCacheLine then it kind of makes sense to
            // only allow updating the Cache element and not the Line.
            set { _oLayout = value ?? throw new ArgumentNullException(); }
        }
    }

    public class SimpleRange : ILineSelection {
        int _iStart  = 0;

        public int Start { 
           get { return _iStart; }
           set { _iStart = value; Offset = value; Length = 0; }
        }

        public int Offset { get; set; } = 0;
        public int Length { get; set; } = 0;

        public SimpleRange( int iStart ) {
            Start = iStart;
        }

        public override string ToString() {
            return "S:" + Start.ToString() + ", O:" + Offset.ToString() + ", L:" + Length.ToString();
        }

        /// <summary>
        /// Use this when the shift key is down to move one edget of
        /// the selection growing from our original start point.
        /// </summary>
        public int Grow {
            set {
                int iLength = value - Start;

                if (iLength > 0) {
                    Offset = Start;
                    Length = iLength;
                } else {
                    Offset = value;
                    Length = -iLength;
                }
            }
        }

        public bool IsEOLSelected { 
            get => false; 
            set => throw new NotImplementedException(); 
        }

        public SelectionTypes SelectionType => SelectionTypes.Start;

        public Line Line {
            get => throw new NotImplementedException(); 
            set => throw new NotImplementedException(); 
        }

        public int At         => throw new NotImplementedException();
        public int ColorIndex => -1;

        public bool IsHit( Line oLine ) {
            return true;
        }
    }

    public struct LabelValuePair {
        public Line _oLabel;
        public Line _oValue;

        public LabelValuePair( Row oRow ) {
            _oLabel = oRow[0];
            _oValue = oRow[1];
        }
    }

    /// <summary>
    /// Document for labels and values style form. Makes separating readable values
    /// from readonly values. Probably move this over to forms project at some time.
    /// </summary>
    
    public class DocProperties : IPgParent, IPgLoad, IDisposable {
        protected readonly IPgBaseSite _oSiteBase;

        public IPgParent Parentage => _oSiteBase.Host;
        public IPgParent Services => Parentage.Services;
        protected void LogError(string strMessage) { _oSiteBase.LogError("Property Page Client", strMessage); }

        //public LabelAccessor Property_Labels { get { return new LabelAccessor( PropertyPairs ); } }
        public Editor PropertyDoc { get; } // Got to be public so the form window can access.
        protected readonly List<IPgFormEvents> _rgFormEvents = new();

        private List<LabelValuePair> PropertyPairs { get; } = new List<LabelValuePair>();
        public int PropertyCount => PropertyPairs.Count;

        // This lets us override the standard color for a property.
        // TODO: Give it two slots. Normal override and Error color...
        public Dictionary<int, SkiaSharp.SKColor> ValueBgColor { get; } = new Dictionary<int, SkiaSharp.SKColor>();

        protected class DocSlot :
            IPgBaseSite {
            protected readonly DocProperties _oHost;

            public DocSlot(DocProperties oHost) {
                _oHost = oHost ?? throw new ArgumentNullException("Host");
            }

            public IPgParent Host => _oHost;

            public void LogError(string strMessage, string strDetails, bool fShow = true) {
                _oHost.LogError(strDetails);
            }

            public void Notify(ShellNotify eEvent) {
                // Might want this value when we close to save the current playing list!!
            }
        }

        public class Manipulator :
            IEnumerable<Line>,
            IPgFormBulkUpdates,
            IDisposable {
            DocProperties _oDocument;
            SortedSet<int> _rgSortedLines = new SortedSet<int>();

            public Manipulator(DocProperties oDoc) {
                _oDocument = oDoc ?? throw new ArgumentNullException();
            }

            public void Dispose() {
                foreach( IPgFormEvents oCall in _oDocument._rgFormEvents ) {
                    oCall.OnFormUpdate(this);
                }
                _rgSortedLines.Clear();
            }

            public int AddProperty(string strLabel) {
                int iLine = _oDocument.PropertyCount;

                LabelValuePair oPair = _oDocument.CreatePropertyPair(strLabel);

                _rgSortedLines.Add(oPair._oLabel.At);
                _rgSortedLines.Add(oPair._oValue.At);

                return iLine;
            }

            public void SetValue(int iLine, string strValue) {
                _oDocument.ValueUpdate(iLine, strValue);
                _rgSortedLines.Add(iLine);
            }

            public void SetLabel(int iProperty, string strName) {
                try {
                    LabelValuePair oPair = _oDocument.GetPropertyPair(iProperty);
                    _rgSortedLines.Add(oPair._oLabel.At);
                    _oDocument.LabelUpdate(iProperty, strName);
                } catch( ArgumentOutOfRangeException ) {
                    _oDocument.LogError("Assign index out of range");
                }
            }

            public IEnumerator<Line> GetEnumerator() {
                foreach( int iLine in _rgSortedLines ) {
                    yield return _oDocument.PropertyDoc[iLine];
                }
            }

            IEnumerator IEnumerable.GetEnumerator() {
                return GetEnumerator();
            }
        } // end class

        // Seems like we dont need this since the base form sends
        // events but I've got to check why the property screens can't
        // resize when the contents change.
        //      public event BufferEvent PropertyEvents;

        public DocProperties(IPgBaseSite oSiteBase) {
            _oSiteBase = oSiteBase ?? throw new ArgumentNullException("Site must not be null.");

            PropertyDoc = new Editor(new DocSlot(this));
        }

        public virtual bool InitNew() {
            if( !PropertyDoc.InitNew() )
                return false;

            return true;
        }

        public void ListenerAdd(IPgFormEvents oCallback) {
            _rgFormEvents.Add(oCallback);
        }

        public void ListenerRemove(IPgFormEvents oCallback) {
            _rgFormEvents.Remove(oCallback);
        }

        public Line this[int iIndex] {
            get {
                return PropertyPairs[iIndex]._oValue;
            }
        }

        public int ValueGetAsInt(int iIndex, int? iDefault = null) {
            if( iDefault.HasValue ) {
                if( !int.TryParse(this[iIndex].ToString(), out int iValue) ) {
                    iValue = iDefault.Value;
                }
                return iValue;
            }

            return int.Parse(this[iIndex].ToString());
        }

        public string ValueGetAsStr(int iIndex) {
            return this[iIndex].ToString();
        }

        public LabelValuePair GetPropertyPair(int iIndex) {
            return PropertyPairs[(int)iIndex];
        }

        public virtual void ValuesEmpty() {
            foreach( LabelValuePair oProp in PropertyPairs ) {
                oProp._oValue.Empty();
            }
            PropertyDoc.Raise_BufferEvent(BUFFEREVENTS.MULTILINE);

            foreach( IPgFormEvents oCall in _rgFormEvents ) {
                oCall.OnFormUpdate(PropertyDoc);
            }
        }

        /// <summary>
        /// Clear out the form of all properties and labels.
        /// </summary>
        public virtual void Clear() {
            PropertyPairs.Clear();
            PropertyDoc.Clear();

            PropertyDoc.Raise_BufferEvent(BUFFEREVENTS.MULTILINE);

            foreach( IPgFormEvents oCall in _rgFormEvents ) {
                oCall.OnFormClear();
            }
        }

        /// <summary>
        /// Sends an event to the views right away. Should make a manipulator for this.
        /// </summary>
        public void ValueUpdate(int iIndex, string strValue, bool Broadcast = false) {
            Line oLine = PropertyPairs[iIndex]._oValue;

            oLine.Empty();
            oLine.TryAppend(strValue);

            if( Broadcast ) {
                // Need to look at this multi line call. s/b single line.
                PropertyDoc.Raise_BufferEvent(BUFFEREVENTS.MULTILINE); // single line probably depends on the caret.
            }
        }

        public void ValueClear(int iIndex) {
            PropertyPairs[iIndex]._oValue.Empty();
        }

        public void LabelUpdate(int iIndex, string strLabel, SKColor? skBgColor = null) {
            PropertyPairs[iIndex]._oLabel.Empty();
            PropertyPairs[iIndex]._oLabel.TryAppend(strLabel);

            if( skBgColor.HasValue ) {
                ValueBgColor.Add(iIndex, skBgColor.Value);
            }
        }

        /// <summary>
        /// Use this when you've updated properties independently and want to finally notify the viewers.
        /// </summary>
        public void RaiseUpdateEvent() {
            PropertyDoc.CharacterCount(0);

            // Anyone using DocProperties object s/b using IPgFormEvents
            //PropertyDoc.Raise_BufferEvent( BUFFEREVENTS.MULTILINE ); 

            foreach( IPgFormEvents oCall in _rgFormEvents ) {
                oCall.OnFormUpdate(PropertyDoc);
            }
        }

        /// <summary>
        /// This is for the rare case where a form get's flushed and rebuilt during
        /// it's lifetime. Most forms are build once and the window shows up
        /// later and set's itself up to be used for the remainder. You could
        /// use this event in a scenario where the window is displayed before the
        /// property document has been constructed.
        /// </summary>
        protected void RaiseLoadedEvent() {
            PropertyDoc.CharacterCount(0);

            foreach( IPgFormEvents oCall in _rgFormEvents ) {
                oCall.OnFormLoad();
            }
        }

        public LabelValuePair CreatePropertyPair(string strName = "", string strValue = "") {
            LabelValuePair oProp = new LabelValuePair();

            oProp._oLabel = PropertyDoc.LineAppend(strName, fUndoable: false);
            oProp._oValue = PropertyDoc.LineAppend(strValue, fUndoable: false);

            PropertyPairs.Add(oProp);

            return oProp;
        }

        public void Dispose() {
            _rgFormEvents.Clear(); // Removes circular references.

            PropertyDoc.Dispose();
        }
    }
    
    /*
    public class DocProperties :
        EditMultiColumn,
        IPgLoad
    {
        public class PropertyRow : Row {
            public PropertyRow( string strLabel ) {
                _rgColumns = new Line[2];

                if( strLabel == null )
                    strLabel = string.Empty;

                _rgColumns[0] = new TextLine( 0, strLabel );
                _rgColumns[1] = new TextLine( 1, string.Empty );
            }

            public PropertyRow( string strLabel, string strValue ) {
                _rgColumns = new Line[2];

                _rgColumns[0] = new TextLine( 0, strLabel );
                _rgColumns[1] = new TextLine( 1, strValue );
            }

            public Line Label => this[0];
            public Line Value => this[1];
        }

        protected readonly List<IPgFormEvents> _rgFormEvents = new ();
        public int PropertyCount => _rgRows.Count;

        // This lets us override the standard color for a property.
        // TODO: Give it two slots. Normal override and Error color...
        public Dictionary<int, SKColor> ValueBgColor { get; } = new Dictionary<int, SKColor >();

		protected class DocSlot :
			IPgBaseSite
		{
			protected readonly DocProperties _oHost;

			public DocSlot( DocProperties oHost ) {
				_oHost = oHost ?? throw new ArgumentNullException( "Host" );
			}

			public IPgParent Host => _oHost;

            public void LogError(string strMessage, string strDetails, bool fShow=true) {
				_oHost.LogError( strDetails );
			}

			public void Notify( ShellNotify eEvent ) {
				// Might want this value when we close to save the current playing list!!
			}
		}

        public DocProperties( IPgBaseSite oSite ) : base( oSite ) 
        { }

        /// <remarks>
        /// Consider making this abstract, if I can make class abstract...
        /// </remarks>
        public virtual bool InitNew() {
            return true;
        }

		public class Manipulator : 
            IEnumerable<Line>,
			IPgFormBulkUpdates,
			IDisposable
		{
			DocProperties  _oHost;
            SortedSet<int> _rgSortedLines = new SortedSet<int>();

			public Manipulator( DocProperties oDoc ) 
			{
				_oHost = oDoc ?? throw new ArgumentNullException();
			}

			public void Dispose() {
                foreach( IPgFormEvents oCall in _oHost._rgFormEvents ) {
                    oCall.OnFormUpdate( this );
                }
                _rgSortedLines.Clear();
			}

			public int AddProperty( string strLabel ) {
                int iLine = _oHost.PropertyCount;

                _rgSortedLines.Add( iLine );

                _oHost._rgRows.Add( new PropertyRow( strLabel ) );

				return iLine;
			}

			public void SetValue( int iLine, string strValue ) {
                try {
                    Line oValueLine = _oHost[ iLine ][1];

                    oValueLine.Empty();
                    oValueLine.TryAppend( strValue );

                    _rgSortedLines.Add( iLine );
                } catch( ArgumentOutOfRangeException ) {
					_oHost.LogError( "Property assign index out of range" );
                }
			}

			public void SetLabel( int iProperty, string strLabel ) {
                try {
                    Line oLabelLine = _oHost[ iProperty ][0];

                    oLabelLine.Empty();
                    oLabelLine.TryAppend( strLabel );

                    _rgSortedLines.Add( iProperty );
                } catch( ArgumentOutOfRangeException ) {
					_oHost.LogError( "Property assign index out of range" );
                }
			}

            public IEnumerator<Line> GetEnumerator() {
                foreach( int iLine in _rgSortedLines ) { 
                    yield return _oHost[iLine][1]; // Enum the values.
                }
            }

            IEnumerator IEnumerable.GetEnumerator() {
                return GetEnumerator();
            }
        } // end Manipulator

        public void ListenerAdd( IPgFormEvents oCallback ) {
            _rgFormEvents.Add( oCallback );
        }

        public void ListenerRemove( IPgFormEvents oCallback ) {
            _rgFormEvents.Remove( oCallback );
        }

        public ReadOnlySpan<char> ValueGetAsSpan( int iIndex ) {
            return _rgRows[iIndex][1].AsSpan;
        }

        public string ValueGetAsStr( int iIndex ) {
            return _rgRows[iIndex][1].ToString();
        }

        public Double ValueAsDouble( int iIndex, double? dblDefault ) {
            return _rgRows[iIndex][1].GetAsDouble( dblDefault );
        }
        public Double ValueAsInt( int iIndex, int? iDefault ) {
            return _rgRows[iIndex][1].GetAsDouble( iDefault );
        }

        public bool   ValueAsBool( int iIndex ) {
            return _rgRows[iIndex][1].GetAsBool();
        }

        public Line   ValueAsLine( int iIndex ) {
            return _rgRows[iIndex][1];
        }

        public LabelValuePair GetPropertyPair( int iIndex ) {
            return new LabelValuePair( this[iIndex] );
        }

        protected struct ValueEnumerator :
            IEnumerable<Line> 
        {
            readonly List<Row> _rgRow;

            public ValueEnumerator( List<Row> rgRow ) {
                _rgRow = rgRow ?? throw new ArgumentNullException();
            }

            public IEnumerator<Line> GetEnumerator() {
                foreach( PropertyRow oPair in _rgRow ) {
                    yield return oPair.Value;
                }
            }

            IEnumerator IEnumerable.GetEnumerator() {
                return GetEnumerator();
            }
        }

        public void ValueClear( int iIndex ) {
            if( _rgRows[iIndex] is PropertyRow oPair )
                oPair.Value.Empty();
        }

        public virtual void ValuesEmpty() {
            foreach( Row oRow in this ) {
                if( oRow is PropertyRow oPair )
                    oPair.Value.Empty();
            }

            // PropertyDoc.Raise_BufferEvent( BUFFEREVENTS.MULTILINE );  

            foreach( IPgFormEvents oCall in _rgFormEvents ) {
                oCall.OnFormUpdate( new ValueEnumerator( _rgRows ) );
            }
        }

        public void ValueUpdate( int iIndex, string strValue, bool Broadcast = false ) {
            if( _rgRows[iIndex] is PropertyRow oPair ) {
                oPair.Value.Empty();
                oPair.Value.TryAppend( strValue );

                if( Broadcast ) {
                    // Need to look at this multi line call. s/b single line.
                    // Single line probably depends on the caret.
                    // PropertyDoc.Raise_BufferEvent( BUFFEREVENTS.MULTILINE ); 

                    List<Row> rgTemp = new() { _rgRows[iIndex] }; // BUG: experiment...

                    foreach( IPgFormEvents oEvent in _rgFormEvents ) {
                        oEvent.OnFormUpdate( new ValueEnumerator( rgTemp ) );
                    }
                }
            }
        }

        public void LabelUpdate(int iIndex, string strLabel, SKColor? skBgColor = null) {
            Line oLabel = _rgRows[iIndex][0];

            oLabel.Empty();
            oLabel.TryAppend(strLabel);

            if( skBgColor.HasValue ) {
                ValueBgColor.Add(iIndex, skBgColor.Value);
            }
        }

        /// <summary>
        /// Use this when you've updated properties independently and want to finally notify the viewers.
        /// </summary>
        public void RaiseUpdateEvent() {
            RenumberRows();

            // Anyone using DocProperties object s/b using IPgFormEvents
            //PropertyDoc.Raise_BufferEvent( BUFFEREVENTS.MULTILINE ); 
            
            // Make this a OnFormUpdate() w/ no params.
            foreach( IPgFormEvents oCall in _rgFormEvents ) {
                oCall.OnFormUpdate( new ValueEnumerator( _rgRows ) );
            }
        }

        /// <summary>
        /// This is for the rare case where a form get's flushed and rebuilt during
        /// it's lifetime. Most forms are build once and the window shows up
        /// later and set's itself up to be used for the remainder. You could
        /// use this event in a scenario where the window is displayed before the
        /// property document has been constructed.
        /// </summary>
        protected void RaiseLoadedEvent() {
            RenumberRows(); // Kind of evil in this case. Might not want to do this...
            
            foreach( IPgFormEvents oCall in _rgFormEvents ) {
                oCall.OnFormLoad();
            }
        }

        public virtual void Clear() {
            foreach( Row oRow in this ) {
                oRow.Empty();
            }

            //PropertyDoc.Raise_BufferEvent( BUFFEREVENTS.MULTILINE ); 

            foreach( IPgFormEvents oCall in _rgFormEvents ) {
                oCall.OnFormClear();
            }
        }

        public void CreatePropertyPair( string strLabel = "", string strValue = "" ) {
            _rgRows.Add( new PropertyRow( strLabel, strValue ) );

            RenumberRows();
        }

    }
    */
}
