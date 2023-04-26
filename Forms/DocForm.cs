using System;
using System.IO;
using System.Collections.Generic;

using Play.Interfaces.Embedding;
using Play.Edit;

using SkiaSharp;
using System.Collections;

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

    /// <summary>
    /// This is basically an editor that loads all the lines but doesn't offer
    /// line delete, so that the user doesn't accidently hit undo and remove forms items.
    /// This implementation is a bit obsolete since I'm leaning towards a values/labels
    /// version which separates writable items.
    /// .ps Tried labels and values in seperate documents, that version had a major
    /// window bug so that way won't work.
    /// </summary>
    [Obsolete("See the DocProperties object.")] public class FormsEditor : Editor {
        public FormsEditor( IPgBaseSite oSite ) : base( oSite ) {
        }

        public override bool Load( TextReader oReader ) {
            _iCumulativeCount = 0;
			HighLight         = null;
            
            try {
                int    iLine   = -1;
                string strLine = oReader.ReadLine();
                while( strLine != null ) {
                    ++iLine;
                    Line oLine;
                    if( iLine < _rgLines.ElementCount ) {
                        oLine = _rgLines[iLine];
                        oLine.TryDelete( 0, int.MaxValue, out string strRemoved );
                        oLine.TryAppend( strLine );
                        Raise_AfterLineUpdate( oLine, 0, strRemoved.Length, oLine.ElementCount );
                    } else { 
                        oLine = CreateLine( iLine, strLine );
                        _rgLines.Insert( _rgLines.ElementCount, oLine );
                        Raise_AfterInsertLine( oLine );
                    }
                        
                    _iCumulativeCount = oLine.Summate( iLine, _iCumulativeCount );
                    strLine = oReader.ReadLine();
                }
                while( _rgLines.ElementCount > iLine + 1 ) {
                    int iDelete = _rgLines.ElementCount - 1;
                    Raise_BeforeLineDelete( _rgLines[iDelete] );
                    _rgLines.RemoveAt( iDelete );
                }
            } catch( Exception oE ) {
                Type[] rgErrors = { typeof( IOException ),
                                    typeof( NullReferenceException ),
                                    typeof( ArgumentNullException ),
                                    typeof( ArgumentException ) };
                if( rgErrors.IsUnhandled( oE ) )
                    throw;

                _oSiteBase.LogError( "editor", "Unable to read stream (file) contents." );

                return( false );
            } finally {
                Raise_MultiFinished();
                Raise_BufferEvent( BUFFEREVENTS.LOADED );  
            }

            return (true);
        }
    }

    public struct LabelValuePair {
        public Line _oLabel;
        public Line _oValue;
    }

    public struct LabelAccessor {
        List<LabelValuePair> _rgProps;
        public LabelAccessor( List<LabelValuePair> rgProps ) {
            _rgProps = rgProps ?? throw new ArgumentNullException();
        }

        public Line this[ int i ] {
            get {
                return _rgProps[ i ]._oLabel;
            }
        }
    }

    /// <summary>
    /// Document for labels and values style form. Makes separating readable values
    /// from readonly values. Probably move this over to forms project at some time.
    /// </summary>
    public class DocProperties : IPgParent, IPgLoad, IDisposable {
        protected readonly IPgBaseSite _oSiteBase;

        public IPgParent Parentage => _oSiteBase.Host;
        public IPgParent Services  => Parentage.Services;
        public void      LogError( string strMessage ) { _oSiteBase.LogError( "Property Page Client", strMessage ); }

        //public LabelAccessor Property_Labels { get { return new LabelAccessor( PropertyPairs ); } }
        public Editor PropertyDoc { get; } // Got to be public so the form window can access.
        protected readonly List<IPgFormEvents> _rgFormEvents = new ();

        protected List<LabelValuePair> PropertyPairs { get; } = new List<LabelValuePair>();
        public    int                  PropertyCount => PropertyPairs.Count;

        // This lets us override the standard color for a property.
        // TODO: Give it two slots. Normal override and Error color...
        public Dictionary<int, SkiaSharp.SKColor> ValueBgColor { get; } = new Dictionary<int, SkiaSharp.SKColor >();

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

		public class Manipulator : 
            IEnumerable<Line>,
			IPgFormBulkUpdates,
			IDisposable
		{
			DocProperties  _oDocument;
            SortedSet<int> _rgSortedLines = new SortedSet<int>();

			public Manipulator( DocProperties oDoc ) 
			{
				_oDocument = oDoc ?? throw new ArgumentNullException();
			}

			public void Dispose() {
                foreach( IPgFormEvents oCall in _oDocument._rgFormEvents ) {
                    oCall.OnFormUpdate( this );
                }
                _rgSortedLines.Clear();
			}

			public int AddProperty( string strLabel ) {
                int iLine = _oDocument.PropertyCount;

                LabelValuePair oPair = _oDocument.CreatePropertyPair( strLabel );

                _rgSortedLines.Add( oPair._oLabel.At );
                _rgSortedLines.Add( oPair._oValue.At );

				return iLine;
			}

			public void SetValue( int iLine, string strValue ) {
                _oDocument.ValueUpdate( iLine, strValue );
                _rgSortedLines.Add( iLine );
			}

			public void SetLabel( int iProperty, string strName ) {
                try {
                    LabelValuePair oPair = _oDocument.GetPropertyPair( iProperty );
                    _rgSortedLines.Add( oPair._oLabel.At );
                    _oDocument.LabelUpdate( iProperty, strName );
                } catch( ArgumentOutOfRangeException ) {
					_oDocument.LogError( "Assign index out of range" );
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

        public DocProperties( IPgBaseSite oSiteBase ) {
            _oSiteBase = oSiteBase ?? throw new ArgumentNullException( "Site must not be null." );

            PropertyDoc = new Editor( new DocSlot( this ) );
        }

        public virtual bool InitNew() {
            if( !PropertyDoc.InitNew() )
                return false;

            return true;
        }

        public void ListenerAdd( IPgFormEvents oCallback ) {
            _rgFormEvents.Add( oCallback );
        }

        public void ListenerRemove( IPgFormEvents oCallback ) {
            _rgFormEvents.Remove( oCallback );
        }

        public Line this[int iIndex] { 
            get { 
                return PropertyPairs[iIndex]._oValue;
            }
        }

        public int ValueGetAsInt( int iIndex, int? iDefault = null ) {
            if( iDefault.HasValue ) {
                if( !int.TryParse( this[iIndex].ToString(), out int iValue ) ) {
                    iValue = iDefault.Value;
                }
                return iValue;
            }

            return int.Parse( this[iIndex].ToString() );
        }

        public string ValueGetAsStr( int iIndex ) {
            return this[iIndex].ToString();
        }

        public LabelValuePair GetPropertyPair( int iIndex ) {
            return PropertyPairs[(int)iIndex];
        }

        public virtual void ValuesEmpty() {
            foreach( LabelValuePair oProp in PropertyPairs ) {
                oProp._oValue.Empty();
            }
            PropertyDoc.Raise_BufferEvent( BUFFEREVENTS.MULTILINE ); 

            foreach( IPgFormEvents oCall in _rgFormEvents ) {
                oCall.OnFormUpdate( PropertyDoc );
            }
        }

        /// <summary>
        /// Clear out the form of all properties and labels.
        /// </summary>
        public virtual void Clear() {
            PropertyPairs.Clear();
            PropertyDoc  .Clear();

            PropertyDoc.Raise_BufferEvent( BUFFEREVENTS.MULTILINE ); 

            foreach( IPgFormEvents oCall in _rgFormEvents ) {
                oCall.OnFormClear();
            }
        }

        /// <summary>
        /// Sends an event to the views right away. Should make a manipulator for this.
        /// </summary>
        public void ValueUpdate( int iIndex, string strValue, bool Broadcast = false ) {
            Line oLine = PropertyPairs[iIndex]._oValue;

            oLine.Empty();
            oLine.TryAppend( strValue );

            if( Broadcast ) {
                // Need to look at this multi line call. s/b single line.
                PropertyDoc.Raise_BufferEvent( BUFFEREVENTS.MULTILINE ); // single line probably depends on the caret.
            }
        }

        public void ValueClear( int iIndex ) {
            PropertyPairs[iIndex]._oValue.Empty();
        }

        public void LabelUpdate( int iIndex, string strLabel, SKColor? skBgColor = null ) {
            PropertyPairs[iIndex]._oLabel.Empty();
            PropertyPairs[iIndex]._oLabel.TryAppend( strLabel );

            if( skBgColor.HasValue ) {
                ValueBgColor.Add( iIndex, skBgColor.Value );
            }
        }

        /// <summary>
        /// Use this when you've updated properties independently and want to finally notify the viewers.
        /// </summary>
        public void RaiseUpdateEvent() {
            PropertyDoc.CharacterCount( 0 );

            // Anyone using DocProperties object s/b using IPgFormEvents
            //PropertyDoc.Raise_BufferEvent( BUFFEREVENTS.MULTILINE ); 
            
            foreach( IPgFormEvents oCall in _rgFormEvents ) {
                oCall.OnFormUpdate( PropertyDoc );
            }
        }

        /// <summary>
        /// This is for the rare case where a form get's flushed and rebuilt during
        /// it's lifetime. Most forms are build once and the window shows up
        /// later and set's itself up to be used for the remainder. You could
        /// use this event in a scenario where the window is displayed before the
        /// property document has been constructed.
        /// </summary>
        public void RaiseLoadedEvent() {
            PropertyDoc.CharacterCount( 0 );
            
            foreach( IPgFormEvents oCall in _rgFormEvents ) {
                oCall.OnFormLoad();
            }
        }

        private void RaiseFormatedEvent() {
        }

        public LabelValuePair CreatePropertyPair( string strName = "", string strValue = "" ) {
            LabelValuePair oProp = new LabelValuePair();

            oProp._oLabel = PropertyDoc.LineAppend( strName,  fUndoable:false );
            oProp._oValue = PropertyDoc.LineAppend( strValue, fUndoable:false );

            PropertyPairs.Add( oProp );

            return oProp;
        }

        public void Dispose() {
            _rgFormEvents.Clear(); // Removes circular references.

            PropertyDoc.Dispose();
        }
    }

}
