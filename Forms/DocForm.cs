using System;
using System.Collections.Generic;
using System.Collections;

using SkiaSharp;

using Play.Interfaces.Embedding;
using Play.Edit;


namespace Play.Forms {
    /// <summary>
    /// This is the old way of doing things, migrate to the IPgEditEvents. 
    /// </summary>
    [Obsolete]public interface IPgFormEvents {
        void OnFormUpdate( IEnumerable<Line> rgUpdates ); // Line contents have been updated. remeasure
        void OnFormClear(); // All properties/labels are to be removed.
        void OnFormLoad (); // All properties/labels have been created.
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
    /// Spiffy new Multi Column based DocProperties. Use this for all
    /// your property page needs.
    /// </summary>
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

            public PropertyRow( Row oOrig ) {
                _rgColumns = new Line[2];

                _rgColumns[0] = oOrig[0];
                _rgColumns[1] = oOrig[1];
            }

            public Line Label => this[0];
            public Line Value => this[1];
        }

        public event Action<int[]>  SubmitEvent;
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

        /// <summary>
        /// This is turning into our OnDocUpdated style call but it's more
        /// document global versus the per view updates that come from
        /// IPgEditEvents. You can update the text immediately and then
        /// schedual a parse.
        /// </summary>
        public override void DoParse() {
            // Parse columns as you see fit in a subclass.
        }

        public class Manipulator : 
            IEnumerable<Line>,
			IPgFormBulkUpdates,
			IDisposable
		{
			DocProperties        _oHost;
            SortedSet<int>       _rgUniqueLines = new SortedSet<int>(); // What propert(y/ies) got updated.
            List<IPgEditHandler> _rgHandlers = new List<IPgEditHandler>();

			public Manipulator( DocProperties oDoc ) 
			{
				_oHost = oDoc ?? throw new ArgumentNullException();
                foreach( IPgEditEvents oCall in _oHost._rgListeners ) {
                    _rgHandlers.Add( oCall.CreateEditHandler() );
                }
			}

			public void Dispose() {
                foreach( IPgEditHandler oCall in _rgHandlers ) {
                    oCall.OnUpdated( EditType.ModifyElem, null );
                }
                _rgUniqueLines.Clear();
                _oHost.DoParse();
			}

			public int AddProperty( string strLabel ) {
                int iLine = _oHost.PropertyCount;

                _rgUniqueLines.Add( iLine );

                _oHost._rgRows.Add( new PropertyRow( strLabel ) );

				return iLine;
			}

			public void SetValue( int iLine, string strValue ) {
                try {
                    Line oValueLine = _oHost[ iLine ][1];

                    oValueLine.Empty();
                    oValueLine.TryReplace( 0, oValueLine.ElementCount, strValue );

                    _rgUniqueLines.Add( iLine );
                } catch( ArgumentOutOfRangeException ) {
					_oHost.LogError( "Property assign index out of range" );
                }
			}

			public void SetLabel( int iProperty, string strLabel ) {
                try {
                    Line oLabelLine = _oHost[ iProperty ][0];

                    oLabelLine.Empty();
                    oLabelLine.TryReplace( 0, oLabelLine.ElementCount, strLabel );

                    _rgUniqueLines.Add( iProperty );
                } catch( ArgumentOutOfRangeException ) {
					_oHost.LogError( "Property assign index out of range" );
                }
			}

            public IEnumerator<Line> GetEnumerator() {
                foreach( int iLine in _rgUniqueLines ) { 
                    yield return _oHost[iLine][1]; // Enum the values.
                }
            }

            IEnumerator IEnumerable.GetEnumerator() {
                return GetEnumerator();
            }
        } // end Manipulator

        public ReadOnlySpan<char> ValueGetAsSpan( int iIndex ) {
            return _rgRows[iIndex][1].AsSpan;
        }

        public string ValueAsStr( int iIndex ) {
            return _rgRows[iIndex][1].ToString();
        }

        public Double ValueAsDouble( int iIndex, double? dblDefault ) {
            return _rgRows[iIndex][1].GetAsDouble( dblDefault );
        }
        public int    ValueAsInt( int iIndex, int? iDefault ) {
            return _rgRows[iIndex][1].GetAsInt( iDefault );
        }

        public bool   ValueAsBool( int iIndex ) {
            return _rgRows[iIndex][1].GetAsBool();
        }

        public Line   ValueAsLine( int iIndex ) {
            return _rgRows[iIndex][1];
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
            TrackerEnumerable sTrack = new TrackerEnumerable( this );

            if( _rgRows[iIndex] is PropertyRow oPair )
                oPair.Value.Empty();

            foreach( IPgCaretInfo<Row> oCaret in sTrack ) {
                if( oCaret.Row == _rgRows[iIndex] && oCaret.Column == 1 )
                    oCaret.Offset = 0;
            }

            sTrack.FinishUp( EditType.ModifyElem, _rgRows[iIndex] );

            DoParse();
        }

        public virtual void ValuesEmpty() {
            TrackerEnumerable sTrack = new TrackerEnumerable( this );

            foreach( Row oRow in this ) {
                if( oRow is PropertyRow oPair ) {
                    oPair.Value.Empty();

                    foreach( IPgCaretInfo<Row> oCaret in sTrack ) {
                        if( oCaret.Row == oRow && oCaret.Column == 1 ) {
                            oCaret.Offset = 0;
                        }
                    }
                }
            }

            sTrack.FinishUp( EditType.ModifyElem, null );

            DoParse();
        }

        public void ValueUpdate( int iIndex, string strValue, bool Broadcast = false ) {
            if( _rgRows[iIndex] is PropertyRow oPair ) {
                TrackerEnumerable sTrack = new TrackerEnumerable( this );

                oPair.Value.TryReplace( 0, oPair.Value.ElementCount, strValue );

                List<Row> rgTemp = new() { _rgRows[iIndex] }; // BUG: experiment...

                sTrack.FinishUp( EditType.ModifyElem, _rgRows[iIndex] );
                DoParse();
            }
        }

        public void LabelUpdate( int iIndex, string strLabel, SKColor? skBgColor = null ) {
            if( _rgRows[iIndex] is PropertyRow oPair ) {
                TrackerEnumerable sTrack = new TrackerEnumerable( this );

                oPair.Label.TryReplace( 0, oPair.Label.ElementCount, strLabel );

                if( skBgColor.HasValue ) {
                    ValueBgColor.Add(iIndex, skBgColor.Value);
                }

                sTrack.FinishUp( EditType.ModifyElem, _rgRows[iIndex] );
                DoParse();
            }
        }

        /// <summary>
        /// This is for the rare case where a form get's flushed and rebuilt during
        /// it's lifetime. Most forms are build once and the window shows up
        /// later and set's itself up to be used for the remainder. You could
        /// use this event in a scenario where the window is displayed before the
        /// property document has been constructed.
        /// </summary>
        [Obsolete]protected void RaiseLoadedEvent() {
            RenumberAndSumate(); // Kind of evil in this case. Might not want to do this...
            
            foreach( object oCall in _rgListeners ) {
                if( oCall is IPgFormEvents oEvent ) {
                    oEvent.OnFormLoad();
                }
            }
            // this call will parse any updated text so we get color.
            DoParse();
        }

        public void Raise_Submit() {
            try {
                List<int> rgDirty = new List<int>();

                foreach( Row oRow in this ) {
                    if( oRow[1].IsDirty ) {
                        rgDirty.Add( oRow.At );
                    }
                    oRow[1].ClearDirty();
                }
                SubmitEvent?.Invoke( rgDirty.ToArray() );
            } catch( Exception oEx ) {
                Type[] rgErrors = { typeof( NullReferenceException ),
                                    typeof( ArgumentOutOfRangeException ) };
                if( rgErrors.IsUnhandled( oEx ) )
                    throw;
                LogError( "Problem raising properties submit" );
            }
        }

        public void CreatePropertyPair( string strLabel = "", string strValue = "" ) {
            _rgRows.Add( new PropertyRow( strLabel, strValue ) );

            RenumberAndSumate();
            // this call will parse any updated text so we get color.
            DoParse();
        }

    }
    
}
