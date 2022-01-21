using System;
using System.Collections.Generic;
using System.Collections;
using System.Windows.Forms;
using System.Drawing;
using System.Xml;

using SkiaSharp;

using Play.Interfaces.Embedding;
using Play.Rectangles;
using Play.Edit;
using Play.Forms;
using Play.Parse;

namespace Play.MorsePractice {
	public interface IPgTableEvents {
		void OnTableRowChanged( int iRow );
		void OnTableDone( bool fChangedAll );
		void OnTableClear();
		void OnTableLoaded();
	}

    public interface IPgTableDocument {
		ICollection<ICollection<Line>> Rows { get; }

		void TableListenerAdd   ( IPgTableEvents oEvent );
		void TableListenerRemove( IPgTableEvents oEvent );
    }

	/// <summary>
	/// An experiment to read a log in an SQL file. Just a fragment and unfinished.
	/// </summary>
	public class ViewLog : Control,
		IPgCommandView,
        IPgLoad<XmlElement>,
		IPgSave<XmlDocumentFragment>,
		IPgTableEvents,
		IEnumerable<LayoutText> 
	{
		static public Guid ViewLogger { get; } = new Guid("{BE243DE2-7763-4A44-9499-0EEDBC84D8A4}");

		public Guid   Catagory => ViewLogger;
		public string Banner   => "Log Viewer";
		public SKBitmap Icon   => null;
		public bool   IsDirty  => false;

		readonly IPgViewSite      _oSiteView;
	    readonly IPgTableDocument _oDocument;
		readonly SmartTable       _oTable = new SmartTable( 20, LayoutRect.CSS.None ); // Add some slop. Not measuring well...
		readonly IPgStandardUI    _oStdUI;

        protected SCRIPT_FONTPROPERTIES _sDefFontProps = new SCRIPT_FONTPROPERTIES();
        protected IntPtr                _hScriptCache  = IntPtr.Zero;
		protected bool                  _fCacheInvalid = true;

		public ViewLog( IPgViewSite oSiteView, IPgTableDocument oDocument ) {
			_oSiteView = oSiteView ?? throw new ArgumentNullException( "Site must not be null" );
			_oDocument = oDocument ?? throw new ArgumentNullException( "Document must not be null" );
 			_oStdUI    = oSiteView.Host.Services as IPgStandardUI ?? throw new ArgumentException( "Parent view must provide IPgStandardUI service" );
			Font       = _oStdUI.FontStandard;
		}

		protected override void Dispose( bool fDisposing ) {
			if( fDisposing ) {
				_oDocument.TableListenerRemove( this );
			}
            base.Dispose(fDisposing);
		}

		protected void LogError( string strMessage, string strDetails ) {
			_oSiteView.LogError( strMessage, strDetails );
		}

		public bool InitNew() {
			_oTable.Add( new LayoutRect( LayoutRect.CSS.Flex, 0, 20 ) ); // freq
			_oTable.Add( new LayoutRect( LayoutRect.CSS.Flex, 0, 10 ) ); // time
			_oTable.Add( new LayoutRect( LayoutRect.CSS.Flex, 0, 20 ) ); // date
			_oTable.Add( new LayoutRect( LayoutRect.CSS.Flex, 0, 20 ) ); // station
			_oTable.Add( new LayoutRect( LayoutRect.CSS.Flex, 0, 10 ) ); //	mode
			_oTable.Add( new LayoutRect( LayoutRect.CSS.Flex, 0, 10 ) ); //	qso
			_oTable.Add( new LayoutRect( LayoutRect.CSS.None ) ); // net

			LoadTableView();

			_oDocument.TableListenerAdd( this );

			return true;
		}

		public bool Load( XmlElement oStream ) {
			return true;
		}

		public void OnTableRowChanged( int iRow ) {
			try {
				foreach( LayoutRect oLayout in _oTable.RowItem( iRow ) ) {
					oLayout.Invalidate();
				}
				_fCacheInvalid = true;
			} catch( Exception oEx ) {
				Type[] rgErrors = { typeof( ArgumentOutOfRangeException ), // Index out of range.
									typeof( InvalidCastException ),        // Unexpected table row contents.
									typeof( NullReferenceException ) };
				if( rgErrors.IsUnhandled( oEx ) )
					throw;
				LogError( "Property Changed", "Problem Invalidating given property row!" );
            }
        }

		public void OnTableDone( bool fChangedAll ) {
            Invalidate();
		}

		public void OnTableClear() {
			_oTable.Clear();

			Invalidate();
		}

		protected void LoadTableView() {
			foreach(ICollection<Line> oRow in _oDocument.Rows ) {
				List<LayoutRect> rgRow = new List<LayoutRect>( oRow.Count );
				foreach( Line oLine in oRow ) {
					LayoutText oName = new LayoutText( new CacheWrapped( oLine ), LayoutRect.CSS.Flex, 1 );

					// Load up the row with our display elements.
					rgRow.Add( oName );
				}
				_oTable.AddRow( rgRow );
			}
		}

		public void OnTableLoaded() {
			LoadTableView();

			_fCacheInvalid = true;

			Invalidate();
		}

		protected override void OnSizeChanged(EventArgs e) {
			base.OnSizeChanged(e);

			_oTable.SetRect( 0, 0, Width, Height );
			_oTable.LayoutChildren();

			Invalidate();
		}

		protected void CellsUpdateAll( IntPtr hDC ) {
			if( _hScriptCache == IntPtr.Zero )
				_sDefFontProps.Load( hDC, ref _hScriptCache );

			foreach( LayoutText oTextRect in this ) {
				CacheWrapped oCache = oTextRect.Cache;
				if( oCache.IsInvalid ) { 
					oCache.Update( hDC, ref _hScriptCache, 4, this.FontHeight, _sDefFontProps, null, oTextRect.Width, null );

					// This is super crude. But let's go for it at the moment.
					oCache.Words.Clear();
					//foreach (IColorRange oRange in oCache.Line.Formatting) {
					//	oCache.Words.Add(oRange);
					//}
				}
			}

			_fCacheInvalid = false;
		}

		protected override void OnPaint(PaintEventArgs oE) {
			base.OnPaint(oE);

			using( GraphicsContext oDC = new GraphicsContext( oE.Graphics ) ) {
				IntPtr hFont = _oStdUI.FontStandard.ToHfont();

				using( new ItemContext( oDC.Handle, hFont ) ) {
					if( _fCacheInvalid ) {
						CellsUpdateAll( oDC.Handle );
						_oTable.SetRect( 0, 0, Width, Height );
						_oTable.LayoutChildren();
					}

					foreach( LayoutText oTextRect in this ) {
						if( _oTable.IsIntersecting( oTextRect ) ) {
							oTextRect.Cache.Render( oDC.Handle, _hScriptCache, new PointF( oTextRect.Left, oTextRect.Top ), 0, null );
						}
					}
				}
			}
		}

		/// <summary>
		/// Paint our background to our standard background color.
		/// <param name="oE"></param>
		protected override void OnPaintBackground(PaintEventArgs oE) {
			base.OnPaintBackground(oE);

			try {
				using( Brush oBrush = new SolidBrush( LOGBRUSH.CreateColor( _oStdUI.ColorStandardPacked( StdUIColors.BG ) ) ) ) {
					oE.Graphics.FillRectangle( oBrush, new Rectangle( 0, 0, this.Width, this.Height ) ); 
				}
			} catch( Exception oEx ) {
                Type[] rgErrors = { typeof( NullReferenceException ),
                                    typeof( ArgumentOutOfRangeException ),
									typeof( ArgumentNullException ),
									typeof( ArgumentException ) };
				if( rgErrors.IsUnhandled( oEx ) )
					throw;

				LogError( "Paint", "Problem painting backgrounds." );
			}
		}

		protected override void OnGotFocus(EventArgs e) {
			base.OnGotFocus(e);

			_oSiteView.EventChain.NotifyFocused( true );

			Invalidate();
		}

		protected override void OnLostFocus(EventArgs e) {
			base.OnLostFocus(e);

			_oSiteView.EventChain.NotifyFocused( false );

			Invalidate();
		}

		public IEnumerator<LayoutText> GetEnumerator() {
			foreach( LayoutStack oRow in _oTable.Rows ) {
				foreach( LayoutRect oRect in oRow ) {
					if( oRect is LayoutText oTextRect ) {
						yield return oTextRect;
					}
				}
			}
		}

		IEnumerator IEnumerable.GetEnumerator() {
			return GetEnumerator();
		}

		public bool Execute(Guid sGuid) {
			return false;
		}

		public object Decorate(IPgViewSite oBaseSite, Guid sGuid) {
			return null;
		}

		public bool Save(XmlDocumentFragment oStream) {
			return true;
		}
	}


    /// <summary>
    /// Override to add our hyperlink.
	/// We'll turn that into optionally a dropdown in the future.
    /// </summary>
    /// <seealso cref="DocProperties"/>
    public class ViewRadioProperties : 
        WindowStandardProperties
     {
        public static Guid GUID {get;} = new Guid("{80C855E0-C2F6-4641-9A7C-B6A8A53B3FDF}");


        public ViewRadioProperties( IPgViewSite oSiteView, DocProperties oDocument ) : base( oSiteView, oDocument ) {
        }

        private void OnFrequencyJump( Line oLine, IPgWordRange oRange ) {
            try {
                string strValue = oLine.SubString( oRange.Offset, oRange.Length );
            } catch( Exception oEx ) {
                Type[] rgErrors = { typeof( IndexOutOfRangeException ),
                                    typeof( NullReferenceException ),
                                    typeof( ArgumentNullException ),
                                    typeof( ArgumentException ) };
                if( rgErrors.IsUnhandled( oEx ) )
                    throw;
            }
        }

        public override bool InitNew() {
            if( !base.InitNew() ) 
                return false;

			HyperLinks.Add( "fjump", OnFrequencyJump );
            return true;
        }
    }
}
