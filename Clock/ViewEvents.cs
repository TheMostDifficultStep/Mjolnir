using Play.Drawing;
using Play.Edit;
using Play.Interfaces.Embedding;
using Play.Rectangles;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Metadata;
using System.Windows.Forms;

namespace Play.Clock {
    using DClmn = DocumentSched.RowSched.DCol;
    using LCss  = LayoutRect.CSS;
    public class ViewSchedule : WindowMultiColumn,
        IPgCommandView
    {
        public readonly DocumentContainer _oDocContainer;

        public static Guid Guid { get; } =  new Guid( "{E5A0A280-6BDB-40B3-B39D-BE16C4CAC215}" );

        public string Banner => "Schedule";

        public SKImage Icon { get; }

        public Guid Catagory => Guid;

        public ViewSchedule(IPgViewSite oViewSite, DocumentContainer oDocument) : 
            base(oViewSite,  oDocument.DocSched ) 
        {
            // embed our own just for starters.
            _oDocContainer = oDocument;

			Icon = SKImageResourceHelper.GetImageResource( Assembly.GetExecutingAssembly(), "Play.Clock.Content.icons8-schedule-64.png" );
        }

        protected override bool Initialize() {
            if( !base.Initialize() )
                return false;

            List<ColumnInfo> rgCols = [
                new ( (int)DClmn.Time, new LayoutRect() { Style=LCss.Flex } ),
                new ( (int)DClmn.Freq, new LayoutRect() { Style=LCss.Flex } ),       
                new ( (int)DClmn.Days, new LayoutRect() { Style=LCss.Flex } ),
                new ( (int)DClmn.Desc, new LayoutRect() { Style=LCss.None } ),
            ];

            InitColumns( rgCols );

            // At present the base window doesn't put the cursor anywhere, sooo...
            //SelectionSet( 0, (int)DClmn.Time, 0, 0 );

            return true;
        }

        protected override void OnKeyPress(KeyPressEventArgs e) {
            if( IsDisposed )
                return;
            if( _oViewEvents.IsCommandPress( e.KeyChar ) )
                return;
            if( IsReadOnly )
                return;

			switch( e.KeyChar ) {
				case '\t':
					int iDir = ModifierKeys == Keys.Shift ? -1 : 1;
					_oCacheMan.CaretTab( iDir );
					break;
				case '\r':
					// Not likely unset upon key press. But problematic if negative.
                    int iPosition = _oCacheMan.CaretAt + 1;
					if( _oCacheMan.CaretAt < 0 ) {
                        iPosition = 0;
                    }

					if( _oDocContainer.DocSched.InsertNew( iPosition ) is Row oRow ) {
						_oCacheMan.CaretReset( oRow, iColumn:0 );
					}
					break;
				default:
					base.OnKeyPress( e );
					break;
			}
		}

        protected override void OnKeyDown(KeyEventArgs e) {
            if( e.KeyCode == Keys.Delete && e.Shift ) {
                _oDocContainer.DocSched.RemoveAt( _oCacheMan.CaretAt );
            } else {
                base.OnKeyDown(e);
            }
        }

        protected override void OnMouseDown(MouseEventArgs e) {
            base.OnMouseDown(e);
        }

        public object Decorate(IPgViewSite oBaseSite, Guid sGuid) {
            return null;
        }

        public override bool Execute(Guid gCommand) {
            if( gCommand == GlobalCommands.Play ||
                gCommand == GlobalCommands.Recycle ) {
                _oDocContainer.DocSched.ReBuildWatchList( DateTime.Now );
            }
            return base.Execute(gCommand);
        }
    }
}
