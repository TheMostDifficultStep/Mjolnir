using Play.Drawing;
using Play.Edit;
using Play.Interfaces.Embedding;
using Play.Rectangles;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Reflection;
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

            List<ColumnInfo> rgCols = new List<ColumnInfo> {
                new ( (int)DClmn.Time, new LayoutRect() { Style=LCss.Flex } ),
                new ( (int)DClmn.Freq, new LayoutRect() { Style=LCss.Flex } ),       
                new ( (int)DClmn.On,   new LayoutRect() { Style=LCss.Flex } ),
                new ( (int)DClmn.Desc, new LayoutRect() { Style=LCss.None } ),
            };

            InitColumns( rgCols );

            // At present the base window doesn't put the cursor anywhere, sooo...
            //SelectionSet( 0, (int)DClmn.Time, 0, 0 );

            return true;
        }

        protected override void OnMouseDown(MouseEventArgs e) {
            base.OnMouseDown(e);
        }

        public object Decorate(IPgViewSite oBaseSite, Guid sGuid) {
            return null;
        }

        public override bool Execute(Guid gCommand) {
            return base.Execute(gCommand);
        }
    }
}
