using System;
using System.Xml;
using System.Windows.Forms;
using System.Reflection;
using System.Collections.Generic;
using System.Collections;

using SkiaSharp;

using Play.Rectangles;
using Play.Interfaces.Embedding;
using Play.Edit;
using Play.Drawing;
using Play.Forms;

namespace Play.MorsePractice {
	/// <summary>
	/// This is the wrapper for the notes window and the operator log window.
	/// </summary>
	public class ViewLogAndNotes :
		Control,
		IPgParent,
		IPgLoad<XmlElement>,
		IPgSave<XmlDocumentFragment>,
		IPgCommandView,
		IPgTextView,
		IEnumerable<ILineRange>
	{
		public static Guid ViewCategory {get;} = new Guid( "{F308F2D9-5F57-4351-A145-C0AD45773D07}" );
        static readonly protected string _strIcon = @"Play.MorsePractice.Content.icons8-copybook-60.png";

		protected class ViewMorseSlot :
			IPgBaseSite,
		    IPgViewSite
		{
			protected readonly ViewLogAndNotes _oHost;

			public ViewMorseSlot( ViewLogAndNotes oHost ) {
				_oHost = oHost ?? throw new ArgumentNullException();
			}

			public IPgParent Host => _oHost;

			public void LogError(string strMessage, string strDetails, bool fShow=true) {
				_oHost._oSiteView.LogError( strMessage, strDetails, fShow );
			}

			public virtual IPgViewNotify EventChain => _oHost._oSiteView.EventChain;

			public void Notify( ShellNotify eEvent ) {
			}
		} // End class

		readonly IPgViewSite _oSiteView;
		readonly DocNetHost  _DocNetHost;
		readonly LayoutStack _rgLayout;
				 bool        _fDisposed = false;


		public IPgParent Parentage => _oSiteView.Host;
		public IPgParent Services  => Parentage.Services;
		public bool      IsDirty   => _DocNetHost.Log.IsDirty || _DocNetHost.Notes.IsDirty;
		public Guid      Catagory  => ViewCategory;
		public string    Banner    => string.IsNullOrEmpty( _DocNetHost.FileName ) ? "New Net Logger" : _DocNetHost.FileName;
		public SKBitmap  Icon      { get; protected set; }

		EditWindow2 ViewNotes   { get; }
		ViewLog  ViewLog     { get; }
		public ViewOutline ViewOutline { get; set; }

		public ViewLogAndNotes( IPgViewSite oSiteView, DocNetHost oDocument ) {
			_oSiteView = oSiteView ?? throw new ArgumentNullException();
			_DocNetHost   = oDocument ?? throw new ArgumentNullException();

			ViewNotes = new EditWindow2( new ViewMorseSlot( this ), _DocNetHost.Notes ) { Parent = this };
			ViewLog   = new ViewLog ( new ViewMorseSlot( this ), _DocNetHost.Log   ) { Parent = this };
			Icon      = SKImageResourceHelper.GetImageResource( Assembly.GetExecutingAssembly(), _strIcon );

			_rgLayout = new LayoutStackVertical( ) {
								Spacing  = 10,
								Children = {
									new LayoutControl( ViewNotes, LayoutRect.CSS.Flex ),
									new LayoutControl( ViewLog,   LayoutRect.CSS.None )
								}
							};
		}

		protected override void Dispose( bool disposing ) {
			if( disposing && !_fDisposed ) {
				ViewLog .Dispose();
				ViewNotes.Dispose();

				ViewOutline = null;

				_fDisposed = true;
			}

			base.Dispose( disposing );
		}

        protected override void OnSizeChanged(EventArgs e) {
			base.OnSizeChanged(e);

			_rgLayout.SetRect( LOCUS.UPPERLEFT, 0, 0, Width, Height );
			_rgLayout.LayoutChildren();

			Invalidate();
		}

		/// <remarks>
		/// Interesting that on a windows form. if the child gets the focus
		/// it will send an event to it's parent about that.
		/// In our case it might be nice to remember who had the focus
		/// last between the notes or log and hand them the focus back... :-/
		/// </remarks>
        protected override void OnGotFocus(EventArgs e) {
            base.OnGotFocus(e);

			ViewLog.Select();
        }

		/// <param name="oLogRow">The row in the Log where the caret currently rests.</param>
		public void SetOutlineCaret( Row oLogRow ) {
			if( ViewOutline is not null ) {
				ReadOnlySpan<char> oCall = oLogRow[0].AsSpan;

				foreach( Row oRefRow in _DocNetHost.Outline ) {
					if( oCall.IsEqual( oRefRow[0] ) ) {
						ViewOutline.SelectionSet( oRefRow.At, 0, 0 );
					}
				}
			}
		}

		public object Decorate(IPgViewSite oViewSite,Guid sGuid) {
            try {
                if (sGuid.Equals(GlobalDecorations.Outline)) {
					return new ViewOutline( oViewSite, _DocNetHost.Outline, this );
                }
				if( sGuid.Equals(GlobalDecorations.Properties )) {
					return new WindowStandardProperties( oViewSite, _DocNetHost.Props );
				}
            } catch (Exception oEx) {
                Type[] rgErrors = { typeof( NotImplementedException ),
                                    typeof( NullReferenceException ),
                                    typeof( ArgumentException ),
                                    typeof( ArgumentNullException ) };
                if (rgErrors.IsUnhandled(oEx))
                    throw;

                _oSiteView.LogError( "View log & notes", "Couldn't create EditWin decor: " + sGuid.ToString());
            }

            return null;
		}

		public bool Execute(Guid sGuid) {
			DocProperties oProps = _DocNetHost.Props;

			const int iDate = (int)DocLogProperties.Names.LogDate;
			const int iTime = (int)DocLogProperties.Names.TimeStart;
			const int iTEnd = (int)DocLogProperties.Names.TimeEnd;

			switch( sGuid ) {
				case var a when sGuid == GlobalCommands.Play:
					if( oProps.IsValueEmpty( iDate ) )
						oProps.ValueUpdate( iDate, DateTime.Now.ToLongDateString() );
					else
						_oSiteView.LogError( "Radio Log", "Date Already Set" );
					if( oProps.IsValueEmpty( iTime ) )
						oProps.ValueUpdate( iTime, DateTime.Now.ToShortTimeString() );
					else
						_oSiteView.LogError( "Radio Log", "Start Time Already Set" );

					return true;
				case var c when sGuid == GlobalCommands.Stop:
					if( oProps.IsValueEmpty( iTEnd ) )
						oProps.ValueUpdate( iTEnd, DateTime.Now.ToShortTimeString() );
					else
						_oSiteView.LogError( "Radio Log", "End Time Already Set" );
					return true;
				default:
					return ViewLog.Execute( sGuid );
			}
		}

		public bool InitNew() {
			if( !ViewLog .InitNew() )
				return false;
			if( !ViewNotes.InitNew() )
				return false;

			return true;
		}

		public bool Load(XmlElement oStream) {
			return InitNew();
		}

		public bool Save(XmlDocumentFragment oStream) {
			return true;
		}

        public IEnumerator<ILineRange> GetEnumerator() {
			return ViewLog.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() {
            return GetEnumerator();
        }

        #region IPgTextView
        public TextPosition Caret => ViewLog.Caret;

        public void ScrollTo(SCROLLPOS eEdge) {
            ViewLog.ScrollTo(eEdge);
        }

        public bool SelectionSet(int iLine, int iOffset, int iLength) {
            return ViewLog.SelectionSet(iLine, iOffset, iLength);
        }

        public void SelectionClear() {
            ViewLog.SelectionClear();
        }

		public Row LogEntryHighlight {
			get { return _DocNetHost.Log.HighLight; }
			set { _DocNetHost.Log.HighLight = value; }
		}
        #endregion
    }
}
