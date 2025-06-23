using System;
using System.Collections.Generic;
using System.Collections;

using SkiaSharp;

using Play.Interfaces.Embedding;
using Play.Edit;


namespace Play.Forms {
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
            public const int ColumnLabel = 0;
            public const int ColumnValue = 1;

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

            public Line Label => this[ColumnLabel];
            public Line Value => this[ColumnValue];
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

        public class Manipulator : 
            IEnumerable<Line>,
			IPgFormBulkUpdates,
			IDisposable
		{
			DocProperties  _oHost;
            SortedSet<int> _rgUniqueLines = new SortedSet<int>(); // What propert(y/ies) got updated.

			public Manipulator( DocProperties oDoc ) 
			{
				_oHost = oDoc ?? throw new ArgumentNullException();
                _oHost.Raise_DocUpdateBegin();
			}

            ///<remarks>Normally this is where we would renumber the
            ///Row.At values here. But that's dangerous on a property page. 
            ///So dispence with that, but make sure the "AddProperty" 
            ///call set's the "At" member...
            ///<seealso cref="AddProperty(string)"/>
			public void Dispose() {
                _rgUniqueLines.Clear();

                _oHost.Raise_DocUpdateEnd( IPgEditEvents.EditType.Rows, null );
                _oHost.DoParse();
			}

            /// <summary>
            /// You need to be careful with this b/c the system will
            /// renumber the lines when it has finished with the properties.
            /// if you don't add properties in the same order as any
            /// enum access you might create. Or randomly add something
            /// in the middle, the Row.At values will not match your
            /// expectations.
            /// </summary>
            /// <remarks>TODO: We should actually be able to remove this member
            /// since the modern property pages don't do this. Only
            /// the ancient Text editor uses it for navigation prop page.</remarks>
            /// <seealso cref="Dispose"/>
			public int AddProperty( string strLabel ) {
                int iLine = _oHost.PropertyCount;

                _rgUniqueLines.Add( iLine );

                Row oNew = new PropertyRow( strLabel );
                oNew.At = _oHost._rgRows.Count;

                _oHost._rgRows.Add( oNew );

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

        public bool   IsValueEmpty( int iIndex ) {
            return _rgRows[iIndex][1].IsEmpty();
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

            sTrack.FinishUp( IPgEditEvents.EditType.Column, _rgRows[iIndex] );

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

            sTrack.FinishUp( IPgEditEvents.EditType.Rows, null );

            DoParse();
        }

        public void ValueUpdate( int iIndex, string strValue, bool Broadcast = false ) {
            if( _rgRows[iIndex] is PropertyRow oPair ) {
                TrackerEnumerable sTrack = new TrackerEnumerable( this );

                oPair.Value.TryReplace( 0, oPair.Value.ElementCount, strValue );

                //List<Row> rgTemp = new() { _rgRows[iIndex] }; // BUG: experiment...

                sTrack.FinishUp( IPgEditEvents.EditType.Column, _rgRows[iIndex] );
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

                sTrack.FinishUp( IPgEditEvents.EditType.Column, _rgRows[iIndex] );
                DoParse();
            }
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
