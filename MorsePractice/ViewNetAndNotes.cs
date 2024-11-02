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
	/*
	_rgLayout = new LayoutStackHorizontal() {
		Spacing = 15,
		Children = {
			new LayoutStackVertical( ) {
				Spacing = 15,
				Children = {
				new LayoutControl( ViewSource, LayoutRect.CSS.Percent, 40 ),
				new LayoutControl( ViewNotes,  LayoutRect.CSS.Percent, 60 )
				}
			},
			new LayoutStackVertical( 350, .3F ) {
				Spacing = 15,
				Children = {
				new LayoutControl( ViewCode, LayoutRect.CSS.Percent, 100 )
		}	}	}
	};
	*/

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
		ViewNetLog  ViewLog     { get; }
		public ViewOutline ViewOutline { get; set; }

		public ViewLogAndNotes( IPgViewSite oSiteView, DocNetHost oDocument ) {
			_oSiteView = oSiteView ?? throw new ArgumentNullException();
			_DocNetHost   = oDocument ?? throw new ArgumentNullException();

			ViewNotes = new EditWindow2( new ViewMorseSlot( this ), _DocNetHost.Notes ) { Parent = this };
			ViewLog   = new ViewNetLog ( new ViewMorseSlot( this ), _DocNetHost.Log   ) { Parent = this };
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

		static bool ArraysEqual( ReadOnlySpan<char> a1, ReadOnlySpan<char> a2)
		{
			if( a1.Length == a2.Length )	{
				for( int i = 0; i < a1.Length; i++ )	{
					if( a1[i] != a2[i] )	{
						return false;
					}
				}
				return true;
			}
			return false;
		}

		/// <param name="oRow">The row in the Log where the caret currently rests.</param>
		public void SetOutlineCaret( Row oRow ) {
			if( ViewOutline is not null ) {
				Row                oFoundRef  = null;
				ReadOnlySpan<char> spCallSign = oRow[0].AsSpan;

				foreach( Row oRefRow in _DocNetHost.Outline ) {
					if( ArraysEqual( spCallSign, oRefRow[0].AsSpan ) ) {
						oFoundRef = oRefRow;
					}
				}
				ViewOutline.SelectionSet( oFoundRef.At, 0, 0 );
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
			switch( sGuid ) {
				case var a when sGuid == GlobalCommands.Play:
					_DocNetHost.Props.ValueUpdate( (int)DocLogProperties.Names.LongDate,
												DateTime.Now.ToLongDateString () );
					_DocNetHost.Props.ValueUpdate( (int)DocLogProperties.Names.TimeStart, 
						                        DateTime.Now.ToShortTimeString() );
					return true;
				case var c when sGuid == GlobalCommands.Stop:
					_DocNetHost.Props.ValueUpdate( (int)DocLogProperties.Names.TimeEnd, 
						                        DateTime.Now.ToShortTimeString() );
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
