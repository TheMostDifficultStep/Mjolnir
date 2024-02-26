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

	class ViewLogAndNotes :
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
		readonly DocNetHost  _oDocLog;
		readonly LayoutStack _rgLayout;

		EditWindow2 ViewNotes{ get; }
		ViewNetLog  ViewList { get; }

		public ViewLogAndNotes( IPgViewSite oSiteView, DocNetHost oDocument ) {
			_oSiteView = oSiteView ?? throw new ArgumentNullException();
			_oDocLog   = oDocument ?? throw new ArgumentNullException();

			ViewNotes = new EditWindow2( new ViewMorseSlot( this ), _oDocLog.Notes ) { Parent = this };
			ViewList  = new ViewNetLog ( new ViewMorseSlot( this ), _oDocLog.Log   ) { Parent = this };
			Icon      = SKImageResourceHelper.GetImageResource( Assembly.GetExecutingAssembly(), _strIcon );

			_rgLayout = new LayoutStackVertical( ) {
								Spacing  = 10,
								Children = {
									new LayoutControl( ViewNotes, LayoutRect.CSS.Flex ),
									new LayoutControl( ViewList,  LayoutRect.CSS.None )
								}
							};
		}

		bool _fDisposed = false;

		protected override void Dispose( bool disposing ) {
			if( disposing && !_fDisposed ) {
				ViewList .Dispose();
				ViewNotes.Dispose();

				_fDisposed = true;
			}

			base.Dispose( disposing );
		}

		public IPgParent Parentage => _oSiteView.Host;
		public IPgParent Services  => Parentage.Services;
		public bool      IsDirty   => _oDocLog.Log.IsDirty || _oDocLog.Notes.IsDirty;
		public Guid      Catagory  => ViewCategory;
		public string    Banner    => _oDocLog.FileName;
		public SKBitmap  Icon      { get; protected set; }

        protected override void OnSizeChanged(EventArgs e) {
			base.OnSizeChanged(e);

			_rgLayout.SetRect( LOCUS.UPPERLEFT, 0, 0, Width, Height );
			_rgLayout.LayoutChildren();

			Invalidate();
		}

		public object Decorate(IPgViewSite oBaseSite,Guid sGuid) {
            try {
                if (sGuid.Equals(GlobalDecorations.Outline)) {
                    return new EditWindow2(oBaseSite, _oDocLog.Log.Calls, fReadOnly: true, fSingleLine: false);
                }
				if( sGuid.Equals(GlobalDecorations.Properties )) {
					return new WindowStandardProperties( oBaseSite, _oDocLog.Props );
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
					_oDocLog.Props.ValueUpdate( (int)DocLogProperties.Names.TimeStart, 
						                        DateTime.Now.ToShortTimeString() );
					break;
				case var c when sGuid == GlobalCommands.Stop:
					_oDocLog.Props.ValueUpdate( (int)DocLogProperties.Names.TimeEnd, 
						                        DateTime.Now.ToShortTimeString() );
					break;
			}
			return false;
		}

		public bool InitNew() {
			if( !ViewList .InitNew() )
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
			return ViewList.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() {
            return GetEnumerator();
        }

        #region IPgTextView
        public TextPosition Caret => ViewList.Caret;

        public void ScrollTo(SCROLLPOS eEdge) {
            ViewList.ScrollTo(eEdge);
        }

        public void ScrollToCaret() {
            ViewList.ScrollToCaret();
        }

        public bool SelectionSet(int iLine, int iOffset, int iLength) {
            return ViewList.SelectionSet(iLine, iOffset, iLength);
        }

        public void SelectionClear() {
            ViewList.SelectionClear();
        }
        #endregion
    }
}
