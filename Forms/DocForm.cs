using System;
using System.IO;
using System.Collections.Generic;

using Play.Interfaces.Embedding;
using Play.Edit;

namespace Play.Forms {
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
    /// undo, so that the user doesn't accidently hit undo and remove forms items.
    /// This implementation is a bit obsolete since I'm leaning towards a values/labels
    /// version which separates writable items.
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

    /// <summary>
    /// Document for labels and values style form. Makes separating readable values
    /// from readonly values. Probably move this over to forms project at some time.
    /// </summary>
    public class DocProperties : IPgParent, IPgLoad {
        protected readonly IPgBaseSite _oSiteBase;

        public IPgParent Parentage => _oSiteBase.Host;
        public IPgParent Services  => Parentage.Services;
        public void      LogError( string strMessage ) { _oSiteBase.LogError( "Property Page Client", strMessage ); }

        public Editor Property_Labels { get; }
        public Editor Property_Values { get; }

        // This lets us override the standard color for a property.
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

        public DocProperties( IPgBaseSite oSiteBase ) {
            _oSiteBase = oSiteBase ?? throw new ArgumentNullException( "Site must not be null." );

            Property_Labels = new Editor( new DocSlot( this ) );
            Property_Values = new Editor( new DocSlot( this ) );
        }

        public virtual bool InitNew() {
            if( !Property_Labels.InitNew() )
                return false;
            if( !Property_Values.InitNew() )
                return false;

            return true;
        }

        public Line this[int iIndex] { 
            get { 
                return Property_Values[iIndex];
            }
        }

        public int Count => Property_Values.ElementCount; 

        public virtual void Clear() {
            foreach( Line oLine in Property_Values ) {
                oLine.Empty();
            }
            Property_Values.Raise_BufferEvent( BUFFEREVENTS.MULTILINE ); 
        }

        /// <summary>
        /// Sends an event to the views right away. Should make a manipulator for this.
        /// </summary>
        public void ValueUpdate( int iIndex, string strValue, bool Broadcast = false ) {
            Line oLine = Property_Values[iIndex];
            oLine.Empty();
            oLine.TryAppend( strValue );

            if( Broadcast ) {
                // Need to look at this multi line call. s/b single line.
                Property_Values.Raise_BufferEvent( BUFFEREVENTS.MULTILINE ); // single line probably depends on the caret.
            }
        }

        /// <summary>
        /// Use this when you've updated properties independently and want to finally notify the viewers.
        /// </summary>
        public void RaiseBufferEvent() {
            Property_Values.Raise_BufferEvent( BUFFEREVENTS.MULTILINE ); 
        }
    }

}
