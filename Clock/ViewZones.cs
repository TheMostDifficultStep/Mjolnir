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
    using DClmn = RowZone.DCol;
    using LCss  = LayoutRect.CSS;
    public class ViewTimeZones : WindowMultiColumn,
        IPgCommandView
    {
        public readonly DocumentContainer _oDocContainer;

        public static Guid Guid { get; } = new Guid("{48FA5562-FC69-49F2-87F6-3A4C86A42D8B}");

        public string Banner => "All Time Zones";

        public SKImage Icon { get; }
        private string IconStr => "Play.Clock.Content.icons8-time-zone-64.png";

        public Guid Catagory => Guid;

        public ViewTimeZones(IPgViewSite oViewSite, object oDocument) : 
            base(oViewSite,  ((DocumentContainer)oDocument).DocZones ) 
        {
            // embed our own just for starters.
            _oDocContainer = (DocumentContainer)oDocument;

            Icon = SKImageResourceHelper.GetImageResource( Assembly.GetExecutingAssembly(), IconStr );
        }


        protected override bool Initialize() {
            if( !base.Initialize() )
                return false;

            List<ColumnInfo> rgCols = new List<ColumnInfo> {
                new ( (int)DClmn.Check,  new LayoutRect() { Style=LCss.Flex, Track=33 } ),
                new ( (int)DClmn.Offset, new LayoutRect() { Style=LCss.Flex, Track=30 } ),       
                new ( (int)DClmn.Zone,   new LayoutRect() { Style=LCss.None } ),
            };

            InitColumns( rgCols );

            // At present the base window doesn't put the cursor anywhere, sooo...
            SelectionSet( 0, (int)DClmn.Zone, 0, 0 );

            return true;
        }

        protected override void OnMouseDown(MouseEventArgs e) {
            base.OnMouseDown(e);
        }

        public object Decorate(IPgViewSite oBaseSite, Guid sGuid) {
            return null;
        }

        public override bool Execute(Guid gCommand) {
            if( gCommand == GlobalCommands.Recycle || 
                gCommand == GlobalCommands.Play ) {
                _oDocContainer.Reset();
            }

            return base.Execute(gCommand);
        }
    }
}
