﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using System.Reflection;

using SkiaSharp;

using Play.Drawing;
using Play.Interfaces.Embedding;
using Play.Rectangles;
using Play.Edit;

namespace Play.Clock {
    public class ClockRow : Row {
        public const int ColumnTime = 0;
        public const int ColumnDate = 1;
        public const int ColumnZone = 2;
        public ClockRow( string strDate, string strLabel ) {
            _rgColumns    = new Line[3];
            _rgColumns[0] = new TextLine( 0, string.Empty );
            _rgColumns[1] = new TextLine( 1, strDate );
            _rgColumns[2] = new TextLine( 2, strLabel );
        }

        public void SetTime( DateTime oDT ) {
            string strTime = oDT.Hour.ToString( "D2" ) + ":" + oDT.Minute.ToString( "D2");

            Line oTime = this[0];
            oTime.Empty();
            oTime.TryAppend( strTime );

            Line oDate = this[1];
            oDate.Empty();
            oDate.TryAppend( oDT.ToShortDateString() );
        }

        public void SetLocal12( DateTime oDT ) {
            int    iHour     = oDT.Hour;
            string strMidDay = "am"; 

            if( iHour > 12 ) {
                strMidDay = "pm";
                iHour    -= 12;
            }
            if( iHour == 0 ) {
                iHour    += 12;
            }

            Line oTime = this[0];
            oTime.Empty();
            oTime.TryAppend( iHour.ToString( "D2" ) + ":" + oDT.Minute.ToString( "D2" ) + strMidDay );
        }
    }

    public class DocumentClock :
        EditMultiColumn,
        IPgSave<TextWriter>,
        IPgLoad<TextReader>
    {
		public class DocSlot : 
			IPgBaseSite
		{
			protected readonly DocumentClock _oDoc;

			public DocSlot( DocumentClock oDoc ) {
				_oDoc = oDoc ?? throw new ArgumentNullException( "Document must not be null." );
			}

			public void LogError( string strMessage, string strDetails, bool fShow=true ) {
				_oDoc._oSiteBase.LogError( strMessage, "PropDocSlot : " + strDetails );
			}

			public void Notify( ShellNotify eEvent ) {
			}

			public IPgParent Host => _oDoc;
		}

        protected IPgRoundRobinWork _oWorkPlace;

        public event Action ClockEvent;

        public DocumentClock( IPgBaseSite oSite ) :
            base( oSite )
        {
        }

        public bool InitNew(){
			try {
                _rgRows.Add( new ClockRow( string.Empty,  "utc"   ) );
                _rgRows.Add( new ClockRow( string.Empty,  "local" ) );
                _rgRows.Add( new ClockRow( "12 hr clock", "local" ) );

                RenumberAndSumate();

				_oWorkPlace = ((IPgScheduler)Services).CreateWorkPlace();
                _oWorkPlace.Queue( CreateWorker(), 0 );
			} catch( Exception oEx ) {
                Type[] rgErrors = { typeof( ArgumentNullException ),
                                    typeof( NullReferenceException ) };
                if( rgErrors.IsUnhandled( oEx ) )
                    throw;

				LogError( "Can't set up time update worker." );
			}

            return true;
        }

        public bool Load(TextReader oStream) {
            return true;
        }

        public bool Save(TextWriter oStream) {
            return true;
        }

        public IEnumerator<int> CreateWorker() {
            while( true ) {
                DateTime oDT = DateTime.Now;
                
                foreach( ClockRow row in _rgRows ) {
                    switch( row.At ) {
                        case 0:
                            row.SetTime( oDT.ToUniversalTime() );
                            break;
                        case 1:
                            row.SetTime( oDT );
                            break;
                        case 2:
                            row.SetLocal12( oDT );
                            break;
                    }
                }

                ClockEvent?.Invoke();

                yield return 10000;
            }
        }
    }

    public class WindowClock :
        WindowMultiColumn,
        IPgLoad<XmlElement>,
        IPgSave<XmlDocumentFragment>,
        IPgParent,
        IPgCommandView
    {
        private   readonly string      _strViewIcon  = "Play.Clock.Content.icon_clock.gif";
        protected readonly IPgViewSite _oViewSite;

        public Guid      Catagory  => Guid.Empty; // Default view.
        public string    Banner    => "World Clock";
        public SKBitmap  Icon    { get; }

        protected DocumentClock Document { get; }

        public WindowClock( IPgViewSite oViewSite, DocumentClock oDocClock ) : 
            base( oViewSite, oDocClock ) 
        {
            Document   = oDocClock ?? throw new ArgumentNullException( "Clock document must not be null." );
            _oViewSite = oViewSite;

			Icon       = SKImageResourceHelper.GetImageResource( Assembly.GetExecutingAssembly(), _strViewIcon );
            _fReadOnly = true;
        }

        protected override void Dispose( bool disposing ) {
            if( disposing ) {
                Document.ClockEvent -= OnClockUpdated;
            }
            base.Dispose(disposing);
        }

        protected override bool Initialize() {
            if( !base.Initialize() ) 
                return false;

            Document.ClockEvent += OnClockUpdated;

            TextLayoutAdd( new LayoutRect( LayoutRect.CSS.Percent, 30, 1L ), ClockRow.ColumnTime); // time
            TextLayoutAdd( new LayoutRect( LayoutRect.CSS.Percent, 45, 1L ), ClockRow.ColumnDate); // date
            TextLayoutAdd( new LayoutRect( LayoutRect.CSS.Percent, 25, 1L ), ClockRow.ColumnZone); // zones.
            
            // Figure this out later...
            //foreach( LayoutSingleLine oCache in CacheList ) {
            //    oCache.BgColor = _oStdUI.ColorsStandardAt( StdUIColors.BGNoEditText );
            //}

            return true;
        }

        public object Decorate(IPgViewSite oBaseSite, Guid sGuid) {
            return null;
        }

        public override bool Execute(Guid sGuid) {
            return false;
        }

        public void OnClockUpdated() {
            // This remeasures all the items.
            OnSizeChanged( new EventArgs() );
        }
    }
}
